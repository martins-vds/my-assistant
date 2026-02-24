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
                services.AddFocusAssistantInfrastructure();

                // Application services
                services.AddSingleton<TaskTrackingService>();
                services.AddSingleton<CreateTaskUseCase>();
                services.AddSingleton<SwitchTaskUseCase>();
                services.AddSingleton<CompleteTaskUseCase>();
                services.AddSingleton<RenameTaskUseCase>();
                services.AddSingleton<DeleteTaskUseCase>();
                services.AddSingleton<MergeTasksUseCase>();
                services.AddSingleton<GetOpenTasksUseCase>();

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
                    ).ToList();

                    return new CopilotAgentSession(
                        sp.GetRequiredService<ILogger<CopilotAgentSession>>(),
                        tools);
                });

                // Hosted services
                services.AddHostedService<VoiceListenerService>();
            })
            .Build();

        // Initialize task tracking with persisted data
        var tracking = host.Services.GetRequiredService<TaskTrackingService>();
        await tracking.InitializeAsync();

        await host.RunAsync();
    }
}
