using FocusAssistant.Application.Interfaces;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Infrastructure.Persistence;
using FocusAssistant.Infrastructure.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace FocusAssistant.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Infrastructure services: file-based repositories and voice service stubs.
    /// </summary>
    public static IServiceCollection AddFocusAssistantInfrastructure(this IServiceCollection services)
    {
        // Repositories (singleton — they manage their own file-level concurrency via SemaphoreSlim)
        services.AddSingleton<ITaskRepository, FileTaskRepository>();
        services.AddSingleton<ISessionRepository, FileSessionRepository>();
        services.AddSingleton<IDailyPlanRepository, FileDailyPlanRepository>();
        services.AddSingleton<IUserPreferencesRepository, FileUserPreferencesRepository>();
        services.AddSingleton<INoteRepository, FileNoteRepository>();

        // Voice services (stubs — will be replaced with real implementations in US6)
        services.AddSingleton<IVoiceInputService, SpeechToTextService>();
        services.AddSingleton<IVoiceOutputService, TextToSpeechService>();

        return services;
    }
}
