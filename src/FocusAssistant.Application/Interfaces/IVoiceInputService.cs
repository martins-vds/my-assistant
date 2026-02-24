namespace FocusAssistant.Application.Interfaces;

/// <summary>
/// Captures voice input and converts it to text.
/// </summary>
public interface IVoiceInputService
{
    /// <summary>
    /// Listen for speech and return the transcribed text.
    /// </summary>
    Task<string?> ListenAsync(CancellationToken ct = default);

    /// <summary>
    /// Wait for the wake word to be detected.
    /// Returns true when the wake word is heard, false if cancelled.
    /// </summary>
    Task<bool> WaitForWakeWordAsync(CancellationToken ct = default);
}
