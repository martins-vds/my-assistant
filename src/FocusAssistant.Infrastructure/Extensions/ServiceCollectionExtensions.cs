using FocusAssistant.Application.Interfaces;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Infrastructure.Persistence;
using FocusAssistant.Infrastructure.Voice;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Infrastructure services: file-based repositories and voice services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="useTextMode">
    /// When true, voice services use stdin/stdout (CLI text mode).
    /// When false, voice services use Vosk STT + espeak-ng TTS + wake word detection.
    /// </param>
    /// <param name="voskModelPath">
    /// Path to the Vosk model directory. Required when useTextMode is false.
    /// </param>
    public static IServiceCollection AddFocusAssistantInfrastructure(
        this IServiceCollection services,
        bool useTextMode = true,
        string? voskModelPath = null)
    {
        // Repositories (singleton — they manage their own file-level concurrency via SemaphoreSlim)
        services.AddSingleton<ITaskRepository, FileTaskRepository>();
        services.AddSingleton<ISessionRepository, FileSessionRepository>();
        services.AddSingleton<IDailyPlanRepository, FileDailyPlanRepository>();
        services.AddSingleton<IUserPreferencesRepository, FileUserPreferencesRepository>();
        services.AddSingleton<INoteRepository, FileNoteRepository>();

        // Voice services
        var resolvedModelPath = voskModelPath
            ?? Environment.GetEnvironmentVariable("VOSK_MODEL_PATH")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".focus-assistant", "models", "vosk-model-small-en-us-0.15");

        services.AddSingleton<IVoiceInputService>(sp =>
            new SpeechToTextService(
                sp.GetRequiredService<ILogger<SpeechToTextService>>(),
                resolvedModelPath,
                useTextMode));

        services.AddSingleton<IVoiceOutputService>(sp =>
            new TextToSpeechService(
                sp.GetRequiredService<ILogger<TextToSpeechService>>(),
                useTextMode));

        services.AddSingleton<IWakeWordDetector>(sp =>
            new WakeWordDetector(
                sp.GetRequiredService<ILogger<WakeWordDetector>>(),
                resolvedModelPath));

        return services;
    }
}
