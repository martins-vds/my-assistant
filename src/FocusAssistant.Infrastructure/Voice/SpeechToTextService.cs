using System.Diagnostics;
using System.Text.Json;
using FocusAssistant.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Vosk;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Speech-to-text service using Vosk for offline speech recognition.
/// Captures audio from the system microphone via platform-appropriate tools
/// (arecord on Linux, ffmpeg on Windows/macOS) and transcribes it using Vosk's
/// recognizer. Detects end-of-speech via silence timeout.
/// Falls back to stdin text input if audio capture is unavailable.
/// </summary>
public sealed class SpeechToTextService : IVoiceInputService, IDisposable
{
    private readonly ILogger<SpeechToTextService> _logger;
    private readonly string _modelPath;
    private readonly bool _useTextMode;
    private Model? _model;

    private const int SampleRate = 16000;
    private const int BufferSize = 4000; // ~250ms at 16kHz mono 16-bit

    /// <summary>
    /// Duration of silence (no speech detected) before finalizing transcription.
    /// </summary>
    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Maximum time to wait for the user to start speaking before giving up.
    /// </summary>
    private static readonly TimeSpan InitialSilenceTimeout = TimeSpan.FromSeconds(8);

    /// <summary>
    /// Maximum recording duration per utterance to prevent runaway capture.
    /// </summary>
    private static readonly TimeSpan MaxRecordingDuration = TimeSpan.FromSeconds(30);

    public SpeechToTextService(ILogger<SpeechToTextService> logger, string modelPath, bool useTextMode = false)
    {
        _logger = logger;
        _modelPath = modelPath;
        _useTextMode = useTextMode;
    }

    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        if (_useTextMode)
            return await ListenTextModeAsync(ct);

        return await ListenVoiceModeAsync(ct);
    }

    public Task<bool> WaitForWakeWordAsync(CancellationToken ct = default)
    {
        if (_useTextMode)
        {
            // In text mode, skip wake word — always ready
            return Task.FromResult(true);
        }

        // In voice mode, wake word detection is handled by WakeWordDetector.
        // This method on the STT service always returns true (VoiceListenerService uses the detector).
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        _model?.Dispose();
        _model = null;
    }

    /// <summary>
    /// Voice mode: capture audio via arecord + Vosk transcription with silence detection.
    /// </summary>
    private async Task<string?> ListenVoiceModeAsync(CancellationToken ct)
    {
        EnsureModel();

        Process? captureProcess = null;
        try
        {
            // Retry starting capture a few times — on Windows, DirectShow may
            // still hold the device exclusively from the previous capture process.
            const int maxStartAttempts = 3;
            for (var startAttempt = 0; startAttempt < maxStartAttempts; startAttempt++)
            {
                captureProcess = AudioCaptureHelper.StartCapture();
                if (captureProcess?.StandardOutput.BaseStream is not null)
                    break;

                _logger.LogWarning("Failed to start audio capture for STT (attempt {Attempt}/{Max})",
                    startAttempt + 1, maxStartAttempts);

                if (startAttempt < maxStartAttempts - 1)
                {
                    try { await Task.Delay(500, ct); }
                    catch (OperationCanceledException) { return null; }
                }
            }

            if (captureProcess?.StandardOutput.BaseStream is null)
            {
                _logger.LogError("Failed to start audio capture for STT after {Max} attempts", maxStartAttempts);
                return null;
            }

            using var recognizer = new VoskRecognizer(_model!, SampleRate);
            recognizer.SetMaxAlternatives(0);
            recognizer.SetWords(true);

            var buffer = new byte[BufferSize];
            var stream = captureProcess.StandardOutput.BaseStream;
            var lastSpeechTime = DateTime.UtcNow;
            var startTime = DateTime.UtcNow;
            var hasSpeech = false;
            var segments = new List<string>();

            _logger.LogInformation("Listening for speech...");

            while (!ct.IsCancellationRequested)
            {
                // Check timeouts
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed > MaxRecordingDuration)
                {
                    _logger.LogDebug("Max recording duration reached");
                    break;
                }

                // If no speech detected at all within InitialSilenceTimeout, stop waiting
                if (!hasSpeech && elapsed > InitialSilenceTimeout)
                {
                    _logger.LogInformation("No speech detected within {Seconds}s — giving up",
                        InitialSilenceTimeout.TotalSeconds);
                    break;
                }

                if (hasSpeech && DateTime.UtcNow - lastSpeechTime > SilenceTimeout)
                {
                    _logger.LogDebug("Silence timeout — finalizing transcription");
                    break;
                }

                int bytesRead;
                try
                {
                    bytesRead = await stream.ReadAsync(buffer, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (bytesRead == 0) break;

                if (recognizer.AcceptWaveform(buffer, bytesRead))
                {
                    // AcceptWaveform returning true means a segment is finalized.
                    // Result() returns and CONSUMES the text — we must save it now
                    // because FinalResult() only returns text accumulated AFTER
                    // the last Result() call.
                    var text = ExtractText(recognizer.Result());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _logger.LogDebug("Segment recognized: {Text}", text);
                        segments.Add(text);
                        hasSpeech = true;
                        lastSpeechTime = DateTime.UtcNow;
                    }
                }
                else
                {
                    // Check partial result for speech activity
                    var partial = ExtractPartial(recognizer.PartialResult());
                    if (!string.IsNullOrWhiteSpace(partial))
                    {
                        hasSpeech = true;
                        lastSpeechTime = DateTime.UtcNow;
                    }
                }
            }

            // Get any remaining text not yet consumed by Result()
            var finalText = ExtractText(recognizer.FinalResult());
            if (!string.IsNullOrWhiteSpace(finalText))
                segments.Add(finalText);

            if (segments.Count > 0)
            {
                var fullText = string.Join(" ", segments);
                _logger.LogInformation("Transcribed: {Text}", fullText);
                return fullText;
            }

            _logger.LogInformation("No speech transcribed");
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error during speech recognition");
            return null;
        }
        finally
        {
            StopProcess(captureProcess);
        }
    }

    /// <summary>
    /// Text mode: reads from stdin (CLI testing).
    /// Uses Task.WhenAny so cancellation returns null immediately,
    /// even though Console.ReadLine itself is not cancellable.
    /// </summary>
    private static async Task<string?> ListenTextModeAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return null;

        var tcs = new TaskCompletionSource<string?>();
        using var reg = ct.Register(() => tcs.TrySetResult(null));

        var readTask = Task.Run(() => Console.ReadLine());
        var completed = await Task.WhenAny(readTask, tcs.Task);

        if (completed == tcs.Task)
            return null;

        return await readTask;
    }

    private void EnsureModel()
    {
        if (_model is not null) return;

        if (!Directory.Exists(_modelPath))
            throw new DirectoryNotFoundException(
                $"Vosk model not found at '{_modelPath}'. Download a model from https://alphacephei.com/vosk/models");

        Vosk.Vosk.SetLogLevel(-1);
        _model = new Model(_modelPath);
        _logger.LogInformation("Vosk STT model loaded from '{ModelPath}'", _modelPath);
    }


    private static string? ExtractText(string voskJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(voskJson);
            if (doc.RootElement.TryGetProperty("text", out var textProp))
                return textProp.GetString()?.Trim();
        }
        catch (JsonException) { }
        return null;
    }

    private static string? ExtractPartial(string voskJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(voskJson);
            if (doc.RootElement.TryGetProperty("partial", out var partialProp))
                return partialProp.GetString()?.Trim();
        }
        catch (JsonException) { }
        return null;
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
