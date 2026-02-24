using FocusAssistant.Application.Interfaces;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Stub voice input: reads from stdin for CLI testing.
/// Will be replaced with real STT (Vosk/Azure) in Phase 8.
/// </summary>
public sealed class SpeechToTextService : IVoiceInputService
{
    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                return Console.ReadLine();
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }, ct);
    }

    public Task<bool> WaitForWakeWordAsync(CancellationToken ct = default)
    {
        // In stub mode, we skip wake word detection — always ready to listen
        return Task.FromResult(true);
    }
}
