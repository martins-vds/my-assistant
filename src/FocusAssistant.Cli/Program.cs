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

        var host = Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Information);
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

                    var allTools = tools.Concat(reminderTools).Concat(noteTools).Concat(reflectionTools).Concat(briefingTools).Concat(preferenceTools).ToList();

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
}
