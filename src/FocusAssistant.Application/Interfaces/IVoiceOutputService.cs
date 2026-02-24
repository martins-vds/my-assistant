namespace FocusAssistant.Application.Interfaces;

/// <summary>
/// Speaks text output to the user.
/// </summary>
public interface IVoiceOutputService
{
    /// <summary>
    /// Speak the given text aloud to the user.
    /// </summary>
    Task SpeakAsync(string text, CancellationToken ct = default);

    /// <summary>
    /// Stop any in-progress speech output (barge-in support).
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
}
