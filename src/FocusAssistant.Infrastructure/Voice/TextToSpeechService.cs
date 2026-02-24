using FocusAssistant.Application.Interfaces;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Stub voice output: writes to stdout for CLI testing.
/// Will be replaced with real TTS (espeak/Azure) in Phase 8.
/// </summary>
public sealed class TextToSpeechService : IVoiceOutputService
{
    public Task SpeakAsync(string text, CancellationToken ct = default)
    {
        Console.WriteLine(text);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        // No-op for text output
        return Task.CompletedTask;
    }
}
