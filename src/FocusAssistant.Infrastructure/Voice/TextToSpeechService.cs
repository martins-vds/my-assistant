using System.Diagnostics;
using FocusAssistant.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Text-to-speech service using espeak-ng on Linux.
/// Spawns espeak-ng as a child process. Supports barge-in via StopAsync
/// (kills the running espeak-ng process to immediately stop speaking).
/// Falls back to stdout text output if espeak-ng is not available.
/// </summary>
public sealed class TextToSpeechService : IVoiceOutputService
{
    private readonly ILogger<TextToSpeechService> _logger;
    private readonly bool _useTextMode;
    private Process? _currentProcess;
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Speech rate in words per minute (default: 160, range: 80-500).
    /// </summary>
    public int SpeechRate { get; set; } = 160;

    /// <summary>
    /// Voice variant for espeak-ng (e.g., "en", "en-us", "en+f3").
    /// </summary>
    public string Voice { get; set; } = "en";

    public TextToSpeechService(ILogger<TextToSpeechService> logger, bool useTextMode = false)
    {
        _logger = logger;
        _useTextMode = useTextMode;
    }

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (_useTextMode)
        {
            Console.WriteLine(text);
            return;
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Stop any currently playing speech (barge-in)
            await StopInternalAsync();

            _currentProcess = StartEspeakProcess(text);
            if (_currentProcess is null)
            {
                // Fallback: print to stdout if espeak-ng is not available
                _logger.LogWarning("espeak-ng not available — falling back to text output");
                Console.WriteLine(text);
                return;
            }

            try
            {
                await _currentProcess.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                await StopInternalAsync();
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            await StopInternalAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task StopInternalAsync()
    {
        if (_currentProcess is null || _currentProcess.HasExited)
        {
            _currentProcess = null;
            return Task.CompletedTask;
        }

        try
        {
            _currentProcess.Kill(entireProcessTree: true);
            _logger.LogDebug("Stopped TTS playback (barge-in)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping espeak-ng process");
        }
        finally
        {
            _currentProcess.Dispose();
            _currentProcess = null;
        }

        return Task.CompletedTask;
    }

    private Process? StartEspeakProcess(string text)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "espeak-ng",
                Arguments = $"-v {Voice} -s {SpeechRate} -- \"{EscapeText(text)}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start espeak-ng. Ensure 'espeak-ng' is installed");
            return null;
        }
    }

    /// <summary>
    /// Escapes text for safe shell argument passing to espeak-ng.
    /// </summary>
    private static string EscapeText(string text)
    {
        // Remove characters that could break the shell command
        return text
            .Replace("\"", "'")
            .Replace("\\", "")
            .Replace("\n", " ")
            .Replace("\r", "");
    }
}
