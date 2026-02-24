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

        var options = new CopilotClientOptions();
        _client = new CopilotClient(options);
        await _client.StartAsync(ct);

        var systemPrompt = SystemPromptBuilder.Build();

        var config = new SessionConfig
        {
            Model = "gpt-4o",
            Tools = _tools.ToList(),
            SystemMessage = new SystemMessageConfig
            {
                Mode = SystemMessageMode.Replace,
                Content = systemPrompt
            },
            Streaming = true,
            OnUserInputRequest = async (request, invocation) =>
            {
                // This is called when the session needs user input (e.g. clarification).
                // In our architecture, VoiceListenerService drives input via SendCommandAsync,
                // so we return an empty response and let the caller manage the input loop.
                return new UserInputResponse { Answer = string.Empty };
            }
        };

        _session = await _client.CreateSessionAsync(config, ct);

        // Subscribe to session events for logging and streaming callbacks
        _eventSubscription = _session.On(evt =>
        {
            switch (evt)
            {
                case AssistantMessageEvent messageEvt:
                    _logger.LogDebug("Assistant message: {Content}", messageEvt.Data.Content);
                    OnAssistantMessage?.Invoke(messageEvt.Data.Content);
                    break;

                case AssistantMessageDeltaEvent deltaEvt:
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
                    break;

                case SessionErrorEvent errorEvt:
                    _logger.LogError("Session error: {ErrorType} - {Message}",
                        errorEvt.Data.ErrorType, errorEvt.Data.Message);
                    break;
            }
        });

        _logger.LogInformation("Copilot agent session initialized with {ToolCount} tools", _tools.Count);
    }

    /// <summary>
    /// Sends a text command to the Copilot session and returns the assistant's response.
    /// SendAsync directly returns the response string.
    /// </summary>
    public async Task<string> SendCommandAsync(string text, CancellationToken ct = default)
    {
        if (_session is null)
            throw new InvalidOperationException("Session not initialized. Call InitializeAsync first.");

        _logger.LogDebug("Sending command: {Command}", text);

        // SendAsync returns Task<string> — the assistant's response text
        var response = await _session.SendAsync(new MessageOptions { Prompt = text }, ct);
        return response ?? string.Empty;
    }

    /// <summary>
    /// Sends a text command and waits for the full AssistantMessageEvent.
    /// Useful when you need metadata beyond just the text response.
    /// </summary>
    public async Task<AssistantMessageEvent?> SendAndWaitAsync(
        string text, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        if (_session is null)
            throw new InvalidOperationException("Session not initialized. Call InitializeAsync first.");

        _logger.LogDebug("Sending command (wait): {Command}", text);

        return await _session.SendAndWaitAsync(
            new MessageOptions { Prompt = text }, timeout, ct);
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
}
