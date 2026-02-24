using System.Diagnostics;
using System.Text.Json;
using FocusAssistant.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Vosk;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Detects a configurable wake word using continuous audio capture (ALSA arecord)
/// and Vosk speech recognition in keyword-spotting mode.
/// Listens to the microphone, runs audio through a small Vosk model, and matches
/// partial/final results against the configured wake word.
/// </summary>
public sealed class WakeWordDetector : IWakeWordDetector
{
    private readonly ILogger<WakeWordDetector> _logger;
    private readonly string _modelPath;
    private Model? _model;
    private bool _disposed;

    /// <summary>
    /// Sample rate for audio capture (16kHz mono, 16-bit PCM).
    /// </summary>
    private const int SampleRate = 16000;
    private const int BufferSize = 4000; // ~250ms of audio at 16kHz mono 16-bit

    /// <summary>
    /// Maximum number of times to retry starting audio capture before giving up.
    /// </summary>
    public const int MaxAudioCaptureRetries = 3;

    /// <summary>
    /// Delay between audio capture retry attempts.
    /// </summary>
    public static readonly TimeSpan AudioRetryDelay = TimeSpan.FromSeconds(2);

    public string WakeWord { get; set; } = "hey focus";
    public bool IsListening { get; private set; }

    public WakeWordDetector(ILogger<WakeWordDetector> logger, string modelPath)
    {
        _logger = logger;
        _modelPath = modelPath;
    }

    /// <summary>
    /// Waits for the wake word to be detected in the audio stream.
    /// Returns true when the wake word is heard, false if cancelled.
    /// </summary>
    public async Task<bool> WaitForWakeWordAsync(CancellationToken ct = default)
    {
        EnsureModel();
        IsListening = true;

        try
        {
            for (var attempt = 0; attempt < MaxAudioCaptureRetries; attempt++)
            {
                if (ct.IsCancellationRequested)
                    return false;

                Process? arecord = null;
                try
                {
                    arecord = StartAudioCapture();
                    if (arecord?.StandardOutput.BaseStream is null)
                    {
                        _logger.LogError("Failed to start audio capture process (attempt {Attempt}/{Max})",
                            attempt + 1, MaxAudioCaptureRetries);
                        await DelayBeforeRetry(attempt, ct);
                        continue;
                    }

                    using var recognizer = new VoskRecognizer(_model!, SampleRate);
                    recognizer.SetMaxAlternatives(0);
                    recognizer.SetWords(true);

                    var buffer = new byte[BufferSize];
                    var stream = arecord.StandardOutput.BaseStream;

                    _logger.LogDebug("Wake word detector listening for '{WakeWord}'...", WakeWord);

                    while (!ct.IsCancellationRequested)
                    {
                        int bytesRead;
                        try
                        {
                            bytesRead = await stream.ReadAsync(buffer, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            return false;
                        }

                        if (bytesRead == 0)
                        {
                            // Stream ended — arecord process died. Capture stderr for diagnostics.
                            var stderr = await ReadProcessStderrAsync(arecord);
                            _logger.LogWarning(
                                "Audio capture stream ended unexpectedly (attempt {Attempt}/{Max}).{StdErr}",
                                attempt + 1, MaxAudioCaptureRetries,
                                string.IsNullOrWhiteSpace(stderr) ? "" : $" arecord stderr: {stderr}");
                            break; // Break inner loop to retry
                        }

                        // Feed audio to Vosk — check both partial and final results
                        if (recognizer.AcceptWaveform(buffer, bytesRead))
                        {
                            var result = recognizer.Result();
                            if (ContainsWakeWord(result))
                            {
                                _logger.LogInformation("Wake word detected (final): '{WakeWord}'", WakeWord);
                                return true;
                            }
                        }
                        else
                        {
                            var partial = recognizer.PartialResult();
                            if (ContainsWakeWord(partial))
                            {
                                _logger.LogInformation("Wake word detected (partial): '{WakeWord}'", WakeWord);
                                return true;
                            }
                        }
                    }

                    // If we broke out of the inner while due to cancellation, don't retry
                    if (ct.IsCancellationRequested)
                        return false;

                    // Wait before retrying audio capture
                    await DelayBeforeRetry(attempt, ct);
                }
                finally
                {
                    StopProcess(arecord);
                }
            }

            _logger.LogError(
                "Audio capture failed after {Max} attempts. Check that a microphone is available and arecord works.",
                MaxAudioCaptureRetries);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during wake word detection");
            return false;
        }
        finally
        {
            IsListening = false;
        }
    }

    /// <summary>
    /// Waits before retrying audio capture with a simple linear backoff.
    /// </summary>
    private static async Task DelayBeforeRetry(int attempt, CancellationToken ct)
    {
        var delay = AudioRetryDelay * (attempt + 1);
        try
        {
            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException)
        {
            // Shutting down — that's fine
        }
    }

    /// <summary>
    /// Reads stderr from the arecord process to provide diagnostic info.
    /// </summary>
    private static async Task<string> ReadProcessStderrAsync(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return await process.StandardError.ReadToEndAsync();
            }
        }
        catch
        {
            // Best-effort
        }
        return string.Empty;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _model?.Dispose();
        _model = null;
        await ValueTask.CompletedTask;
    }

    private void EnsureModel()
    {
        if (_model is not null) return;

        if (!Directory.Exists(_modelPath))
            throw new DirectoryNotFoundException(
                $"Vosk model not found at '{_modelPath}'. Download a model from https://alphacephei.com/vosk/models");

        Vosk.Vosk.SetLogLevel(-1); // Suppress Vosk internal logs
        _model = new Model(_modelPath);
        _logger.LogInformation("Vosk model loaded from '{ModelPath}'", _modelPath);
    }

    /// <summary>
    /// Starts arecord (ALSA) process to capture 16kHz mono 16-bit PCM audio from the default mic.
    /// </summary>
    private Process? StartAudioCapture()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "arecord",
                Arguments = "-q -r 16000 -c 1 -f S16_LE -t raw",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process is null)
                _logger.LogError("Failed to start arecord process");

            return process;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start audio capture. Ensure 'arecord' (alsa-utils) is installed");
            return null;
        }
    }

    /// <summary>
    /// Checks if Vosk result JSON contains the wake word.
    /// Vosk returns JSON like {"text": "hey focus"} or {"partial": "hey focus"}.
    /// </summary>
    private bool ContainsWakeWord(string voskJson)
    {
        if (string.IsNullOrWhiteSpace(voskJson))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(voskJson);
            var root = doc.RootElement;

            // Check "text" field (final result)
            if (root.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString();
                if (!string.IsNullOrEmpty(text) &&
                    text.Contains(WakeWord, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check "partial" field (partial result)
            if (root.TryGetProperty("partial", out var partialProp))
            {
                var partial = partialProp.GetString();
                if (!string.IsNullOrEmpty(partial) &&
                    partial.Contains(WakeWord, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch (JsonException)
        {
            // Malformed JSON from Vosk — ignore
        }

        return false;
    }

    private void StopProcess(Process? process)
    {
        if (process is null || process.HasExited) return;

        try
        {
            process.Kill(entireProcessTree: true);
            process.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping audio capture process");
        }
    }
}
