using GitHub.Copilot.SDK;
using FocusAssistant.Application.Interfaces;
using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Cli.Agent;
using FocusAssistant.Cli.HostedServices;
using FocusAssistant.Infrastructure.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Cli;

public class Program
{
    public static async Task Main(string[] args)
    {
        // Determine voice mode from args or environment
        var useTextMode = args.Contains("--text") ||
            Environment.GetEnvironmentVariable("FOCUS_ASSISTANT_TEXT_MODE") == "1";

        // Determine log verbosity: --verbose / -v → Debug, --quiet / -q → Warning, default → Information
        // Can also be set via FOCUS_ASSISTANT_LOG_LEVEL environment variable (e.g. "Debug", "Warning")
        var logLevel = LogLevel.Information;
        if (args.Contains("--verbose") || args.Contains("-v"))
            logLevel = LogLevel.Debug;
        else if (args.Contains("--quiet") || args.Contains("-q"))
            logLevel = LogLevel.Warning;
        else if (Environment.GetEnvironmentVariable("FOCUS_ASSISTANT_LOG_LEVEL") is { } envLevel
            && Enum.TryParse<LogLevel>(envLevel, ignoreCase: true, out var parsed))
            logLevel = parsed;

        // Quick diagnostic: --test bypasses the full app and tests bare Copilot CLI connectivity
        if (args.Contains("--test"))
        {
            await RunCopilotDiagnosticAsync(logLevel);
            return;
        }

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(logLevel);
            })
            .ConfigureServices((context, services) =>
            {
                // Infrastructure registrations (repositories, voice services)
                services.AddFocusAssistantInfrastructure(useTextMode: useTextMode);

                // Application services
                services.AddSingleton<TaskTrackingService>();
                services.AddSingleton<ReminderScheduler>();
                services.AddSingleton<CreateTaskUseCase>();
                services.AddSingleton<SwitchTaskUseCase>();
                services.AddSingleton<CompleteTaskUseCase>();
                services.AddSingleton<RenameTaskUseCase>();
                services.AddSingleton<DeleteTaskUseCase>();
                services.AddSingleton<MergeTasksUseCase>();
                services.AddSingleton<GetOpenTasksUseCase>();
                services.AddSingleton<SetReminderUseCase>();
                services.AddSingleton<AddNoteUseCase>();
                services.AddSingleton<GetTaskNotesUseCase>();
                services.AddSingleton<ReflectionService>();
                services.AddSingleton<StartReflectionUseCase>();
                services.AddSingleton<SetPrioritiesUseCase>();
                services.AddSingleton<GetMorningBriefingUseCase>();
                services.AddSingleton<SavePreferencesUseCase>();
                services.AddSingleton<ArchiveTasksUseCase>();

                // Agent session with tools
                services.AddSingleton<CopilotAgentSession>(sp =>
                {
                    var tools = ToolDefinitions.CreateTaskTrackingTools(
                        sp.GetRequiredService<CreateTaskUseCase>(),
                        sp.GetRequiredService<SwitchTaskUseCase>(),
                        sp.GetRequiredService<CompleteTaskUseCase>(),
                        sp.GetRequiredService<RenameTaskUseCase>(),
                        sp.GetRequiredService<DeleteTaskUseCase>(),
                        sp.GetRequiredService<MergeTasksUseCase>(),
                        sp.GetRequiredService<GetOpenTasksUseCase>()
                    );

                    var reminderTools = ToolDefinitions.CreateReminderTools(
                        sp.GetRequiredService<SetReminderUseCase>()
                    );

                    var noteTools = ToolDefinitions.CreateNoteTools(
                        sp.GetRequiredService<AddNoteUseCase>(),
                        sp.GetRequiredService<GetTaskNotesUseCase>()
                    );

                    var reflectionTools = ToolDefinitions.CreateReflectionTools(
                        sp.GetRequiredService<StartReflectionUseCase>(),
                        sp.GetRequiredService<SetPrioritiesUseCase>(),
                        sp.GetRequiredService<GetOpenTasksUseCase>()
                    );

                    var briefingTools = ToolDefinitions.CreateBriefingTools(
                        sp.GetRequiredService<GetMorningBriefingUseCase>()
                    );

                    var preferenceTools = ToolDefinitions.CreatePreferenceTools(
                        sp.GetRequiredService<SavePreferencesUseCase>()
                    );

                    var archiveTools = ToolDefinitions.CreateArchiveTools(
                        sp.GetRequiredService<ArchiveTasksUseCase>()
                    );

                    var allTools = tools.Concat(reminderTools).Concat(noteTools).Concat(reflectionTools).Concat(briefingTools).Concat(preferenceTools).Concat(archiveTools).ToList();

                    return new CopilotAgentSession(
                        sp.GetRequiredService<ILogger<CopilotAgentSession>>(),
                        allTools);
                });

                // Voice listener hosted service with mode configuration
                services.AddSingleton<VoiceListenerService>(sp =>
                    new VoiceListenerService(
                        sp.GetRequiredService<IVoiceInputService>(),
                        sp.GetRequiredService<IVoiceOutputService>(),
                        sp.GetRequiredService<IWakeWordDetector>(),
                        sp.GetRequiredService<CopilotAgentSession>(),
                        sp.GetRequiredService<ReminderScheduler>(),
                        sp.GetRequiredService<TaskTrackingService>(),
                        sp.GetRequiredService<GetMorningBriefingUseCase>(),
                        sp.GetRequiredService<ILogger<VoiceListenerService>>(),
                        useTextMode));
                services.AddHostedService(sp => sp.GetRequiredService<VoiceListenerService>());

                services.AddHostedService<ReminderBackgroundService>();
            })
            .Build();

        // Initialize task tracking with persisted data
        var tracking = host.Services.GetRequiredService<TaskTrackingService>();
        await tracking.InitializeAsync();

        await host.RunAsync();
    }

    /// <summary>
    /// Multi-step Copilot diagnostic to isolate connectivity, auth, and model issues.
    /// </summary>
    private static async Task RunCopilotDiagnosticAsync(LogLevel logLevel)
    {
        using var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(logLevel);
        });
        var logger = loggerFactory.CreateLogger("CopilotDiag");

        logger.LogInformation("=== Copilot CLI Diagnostic ===");

        // Step 1: Create client with CLI-level debug logging
        logger.LogInformation("Step 1: Creating CopilotClient...");
        await using var client = new CopilotClient(new CopilotClientOptions
        {
            Logger = loggerFactory.CreateLogger("CopilotSDK"),
            LogLevel = "debug"   // CLI-side verbose logging
        });
        logger.LogInformation("Client created.");

        // Step 2: Ping to verify connectivity
        logger.LogInformation("Step 2: Ping test...");
        try
        {
            var ping = await client.PingAsync("diag");
            logger.LogInformation("Ping OK — message: {Msg}, proto: {Proto}",
                ping.Message, ping.ProtocolVersion);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ping FAILED — CLI likely not running");
            return;
        }

        // Step 3: List available models (tests API auth)
        logger.LogInformation("Step 3: Listing models (tests API auth)...");
        try
        {
            var models = await client.ListModelsAsync();
            if (models is null || models.Count == 0)
            {
                logger.LogWarning("No models returned — check Copilot subscription/auth");
            }
            else
            {
                foreach (var m in models)
                    logger.LogInformation("  Model: {Id}", m.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ListModels FAILED — API auth issue?");
        }

        // Step 4: Create session with explicit model
        logger.LogInformation("Step 4: Creating session with Model=gpt-4o...");
        await using var session = await client.CreateSessionAsync(new SessionConfig
        {
            Model = "gpt-4o",
            OnPermissionRequest = PermissionHandler.ApproveAll
        });
        logger.LogInformation("Session created: {SessionId}", session.SessionId);

        // Step 5: Subscribe to events with full detail
        logger.LogInformation("Step 5: Subscribing to events...");
        using var _ = session.On(evt =>
        {
            logger.LogInformation("[event] {EventType}", evt.GetType().Name);
            if (evt is AssistantMessageEvent msg)
                logger.LogInformation("[message] {Content}", msg.Data.Content);
            if (evt is AssistantMessageDeltaEvent delta)
                logger.LogInformation("[delta] {Content}", delta.Data.DeltaContent);
            if (evt is SessionErrorEvent err)
                logger.LogError("[error] {Type}: {Msg}", err.Data.ErrorType, err.Data.Message);
        });

        // Step 6: Send message with 2-minute timeout using TaskCompletionSource
        logger.LogInformation("Step 6: Sending 'Say hello in one sentence'...");

        var done = new TaskCompletionSource<string>();
        string? captured = null;
        using var sub = session.On(evt =>
        {
            if (evt is AssistantMessageEvent msg2)
                captured = msg2.Data.Content;
            else if (evt is SessionIdleEvent)
                done.TrySetResult(captured ?? "(no content)");
            else if (evt is SessionErrorEvent err2)
                done.TrySetException(new Exception($"{err2.Data.ErrorType}: {err2.Data.Message}"));
        });

        var messageId = await session.SendAsync(new MessageOptions { Prompt = "Say hello in one sentence" });
        logger.LogInformation("SendAsync returned messageId: {Id}", messageId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        cts.Token.Register(() => done.TrySetCanceled());

        try
        {
            var result = await done.Task;
            logger.LogInformation("Reply: {Content}", result);
        }
        catch (TaskCanceledException)
        {
            logger.LogError("TIMED OUT after 2 minutes — no SessionIdleEvent received");
            logger.LogError("This means the CLI cannot get a model response. Check:");
            logger.LogError("  1. Run 'gh auth status' — are you logged in with Copilot access?");
            logger.LogError("  2. Run 'gh copilot suggest hello' — does that work?");
            logger.LogError("  3. Check firewall/proxy blocking api.github.com");
        }

        logger.LogInformation("=== Diagnostic complete ===");
    }
}
