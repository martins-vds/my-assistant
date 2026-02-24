namespace FocusAssistant.Application.Interfaces;

/// <summary>
/// Detects a wake word from continuous audio input.
/// </summary>
public interface IWakeWordDetector : IAsyncDisposable
{
    /// <summary>
    /// Starts listening for the wake word. Returns true when detected, false if cancelled.
    /// </summary>
    Task<bool> WaitForWakeWordAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets or sets the wake word to listen for.
    /// </summary>
    string WakeWord { get; set; }

    /// <summary>
    /// Whether the detector is currently running and listening.
    /// </summary>
    bool IsListening { get; }
}
