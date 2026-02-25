using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Cli.Agent;

/// <summary>
/// Manages the Copilot SDK client and session lifecycle.
/// Initializes CopilotClient, creates a session with system prompt and tools,
/// handles events, and exposes SendCommandAsync for the voice pipeline.
/// </summary>
public sealed class CopilotAgentSession : IAsyncDisposable
{
    private readonly ILogger<CopilotAgentSession> _logger;
    private readonly IReadOnlyList<AIFunction> _tools;
    private CopilotClient? _client;
    private CopilotSession? _session;
    private IDisposable? _eventSubscription;
    private bool _disposed;

    /// <summary>
    /// Per-call state used to capture the assistant response from the event stream.
    /// Set before SendAsync, completed by the SessionIdleEvent handler.
    /// </summary>
    private volatile PendingResponse? _pending;

    /// <summary>
    /// Fired when the assistant produces a complete message.
    /// </summary>
    public event Action<string>? OnAssistantMessage;

    /// <summary>
    /// Fired for each streaming delta chunk (for real-time TTS).
    /// </summary>
    public event Action<string>? OnAssistantMessageDelta;

    public CopilotAgentSession(
        ILogger<CopilotAgentSession> logger,
        IEnumerable<AIFunction> tools)
    {
        _logger = logger;
        _tools = tools.ToList().AsReadOnly();
    }

    /// <summary>
    /// Starts the Copilot client and creates a new session with the system prompt and tools.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Initializing Copilot agent session...");

        _client = new CopilotClient(new CopilotClientOptions { Logger = _logger });

        // Verify GitHub authentication before creating a session.
        // Without auth the CLI accepts messages but never produces model responses.
        await VerifyGitHubAuthAsync(ct);

        var systemPrompt = SystemPromptBuilder.Build();

        var config = new SessionConfig
        {
            Model = "gpt-4.1",
            Tools = _tools.ToList(),
            InfiniteSessions = new InfiniteSessionConfig
            {
                Enabled = true,
                BackgroundCompactionThreshold = 0.80,
                BufferExhaustionThreshold = 0.95
            },
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            Streaming = true,
            OnPermissionRequest = PermissionHandler.ApproveAll,
            Hooks = new SessionHooks
            {
                OnPreToolUse = async (input, invocation) =>
                {
                    _logger.LogDebug("Pre-tool-use hook: allowing {ToolName}", input.ToolName);
                    return new PreToolUseHookOutput
                    {
                        PermissionDecision = "allow"
                    };
                },
                OnErrorOccurred = async (input, invocation) =>
                {
                    _logger.LogError("Hook error: {Context} - {Error}", input.ErrorContext, input.Error);
                    return new ErrorOccurredHookOutput
                    {
                        ErrorHandling = "abort"
                    };
                }
            },
            OnUserInputRequest = async (request, invocation) =>
            {
                // This is called when the session needs user input (e.g. clarification).
                // In our architecture, VoiceListenerService drives input via SendCommandAsync,
                // so we return an empty response and let the caller manage the input loop.
                return new UserInputResponse { Answer = string.Empty };
            }
        };

        _session = await _client.CreateSessionAsync(config, ct);

        // Subscribe to session events for logging, streaming, and response capture.
        // The official SDK pattern is: SendAsync (fire-and-forget) → capture content
        // from AssistantMessageEvent → signal completion on SessionIdleEvent.
        _eventSubscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent messageEvt:
                    _logger.LogDebug("Assistant message: {Content}", messageEvt.Data.Content);
                    OnAssistantMessage?.Invoke(messageEvt.Data.Content);
                    // Capture the response for the pending SendCommandAsync caller
                    _pending?.SetContent(messageEvt.Data.Content);
                    break;

                case AssistantMessageDeltaEvent deltaEvt:
                    _logger.LogDebug("Assistant message delta: {DeltaContent}", deltaEvt.Data.DeltaContent);
                    if (deltaEvt.Data.DeltaContent is not null)
                        OnAssistantMessageDelta?.Invoke(deltaEvt.Data.DeltaContent);
                    break;

                case ToolExecutionStartEvent toolStartEvt:
                    _logger.LogDebug("Tool execution starting: {ToolName}", toolStartEvt.Data.ToolName);
                    break;

                case ToolExecutionCompleteEvent toolCompleteEvt:
                    _logger.LogDebug("Tool execution complete: {Success}", toolCompleteEvt.Data.Success);
                    break;

                case SessionIdleEvent:
                    _logger.LogDebug("Session is idle");
                    // Session finished processing — unblock the SendCommandAsync caller
                    _pending?.Complete();
                    break;

                case SessionErrorEvent errorEvt:
                    _logger.LogError("Session error: {ErrorType} - {Message}",
                        errorEvt.Data.ErrorType, errorEvt.Data.Message);
                    _pending?.Fault(
                        new InvalidOperationException(
                            $"Copilot error: {errorEvt.Data.ErrorType} - {errorEvt.Data.Message}"));
                    break;

                case PendingMessagesModifiedEvent:
                    // Ephemeral housekeeping event — the CLI's internal message queue changed.
                    // No action needed; just acknowledge so it doesn't hit the default/unhandled log.
                    _logger.LogTrace("Pending messages modified");
                    break;

                default:
                    _logger.LogDebug("Unhandled session event: {EventType}", evt.GetType().Name);
                    break;
            }
        });

        _logger.LogInformation("Copilot agent session initialized with {ToolCount} tools", _tools.Count);
    }

    /// <summary>
    /// Sends a text command to the Copilot session and returns the assistant's response text.
    /// Uses the official SDK pattern: SendAsync (non-blocking) → capture content from
    /// AssistantMessageEvent → await SessionIdleEvent as the completion signal.
    /// This has no fixed timeout — it waits as long as the agent needs to execute tools.
    /// </summary>
    public async Task<string> SendCommandAsync(string text, CancellationToken ct = default)
    {
        if (_session is null)
            throw new InvalidOperationException("Session not initialized. Call InitializeAsync first.");

        _logger.LogDebug("Sending command: {Command}", text);

        // Set up per-call state to capture the event-delivered response
        var pending = new PendingResponse();
        _pending = pending;

        // Register cancellation so we don't wait forever if the caller cancels
        using var ctr = ct.Register(() => pending.Cancel(ct));

        try
        {
            // SendAsync fires the request — response arrives via the On() event subscription
            await _session.SendAsync(new MessageOptions { Prompt = text }, ct);
            _logger.LogDebug("SendAsync completed, waiting for session idle...");

            // Wait for SessionIdleEvent to signal completion (or error/cancellation)
            var content = await pending.Task;

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Copilot returned empty response for command: {Command}", text);
                return string.Empty;
            }

            _logger.LogDebug("Copilot response: {Response}", content);
            return content;
        }
        finally
        {
            _pending = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _eventSubscription?.Dispose();

        if (_session is not null)
        {
            try
            {
                await _session.AbortAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error aborting session");
            }
        }

        if (_client is not null)
        {
            try
            {
                await _client.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping client");
            }
        }
    }

    /// <summary>
    /// Verifies that the Copilot CLI can authenticate with GitHub.
    /// Calls ListModelsAsync which requires a valid auth token.
    /// Throws a clear error message if auth is missing.
    /// </summary>
    private async Task VerifyGitHubAuthAsync(CancellationToken ct)
    {
        _logger.LogDebug("Verifying GitHub authentication...");
        try
        {
            var models = await _client!.ListModelsAsync(ct);
            if (models is null || models.Count == 0)
            {
                _logger.LogWarning("No models returned — Copilot subscription may be inactive");
            }
            else
            {
                _logger.LogInformation("GitHub auth OK — {Count} models available", models.Count);
            }
        }
        catch (Exception ex) when (ex.InnerException?.Message?.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase) == true
                                 || ex.Message.Contains("Not authenticated", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "GitHub Copilot is not authenticated. Please run 'gh auth login' first, then retry.", ex);
        }
    }

    /// <summary>
    /// Holds per-call state for capturing the assistant response from the event stream.
    /// Content is set by AssistantMessageEvent, completion signalled by SessionIdleEvent.
    /// </summary>
    private sealed class PendingResponse
    {
        private readonly TaskCompletionSource<string> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string? _content;

        public Task<string> Task => _tcs.Task;

        public void SetContent(string content) => _content = content;

        public void Complete() => _tcs.TrySetResult(_content ?? string.Empty);

        public void Fault(Exception ex) => _tcs.TrySetException(ex);

        public void Cancel(CancellationToken ct) => _tcs.TrySetCanceled(ct);
    }
}
