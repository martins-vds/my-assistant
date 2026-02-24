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
            captureProcess = AudioCaptureHelper.StartCapture();
            if (captureProcess?.StandardOutput.BaseStream is null)
            {
                _logger.LogError("Failed to start audio capture for STT");
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

            _logger.LogDebug("Listening for speech...");

            while (!ct.IsCancellationRequested)
            {
                // Check timeouts
                var elapsed = DateTime.UtcNow - startTime;
                if (elapsed > MaxRecordingDuration)
                {
                    _logger.LogDebug("Max recording duration reached");
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
                    // Final result for this utterance segment
                    var text = ExtractText(recognizer.Result());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
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

            // Get final transcription
            var finalText = ExtractText(recognizer.FinalResult());
            if (!string.IsNullOrWhiteSpace(finalText))
            {
                _logger.LogDebug("Transcribed: {Text}", finalText);
                return finalText;
            }

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
