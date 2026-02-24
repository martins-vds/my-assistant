using System.Diagnostics;
using System.Runtime.InteropServices;
using FocusAssistant.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Cross-platform text-to-speech service.
/// Linux: spawns espeak-ng as a child process.
/// Windows: uses PowerShell with System.Speech.Synthesis (SAPI).
/// macOS: uses the built-in 'say' command.
/// Supports barge-in via StopAsync (kills the running process to immediately stop speaking).
/// Falls back to stdout text output if TTS is not available.
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
    /// On Windows/macOS this is ignored (system default voice is used).
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

            _currentProcess = StartTtsProcess(text);
            if (_currentProcess is null)
            {
                // Fallback: print to stdout if TTS is not available
                _logger.LogWarning("TTS engine not available — falling back to text output");
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
            _logger.LogWarning(ex, "Error stopping TTS process");
        }
        finally
        {
            _currentProcess.Dispose();
            _currentProcess = null;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the platform-appropriate TTS process.
    /// </summary>
    private Process? StartTtsProcess(string text)
    {
        try
        {
            ProcessStartInfo psi;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Use PowerShell with .NET System.Speech (built into Windows)
                var escaped = EscapeForPowerShell(text);
                psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Speech; $s = New-Object System.Speech.Synthesis.SpeechSynthesizer; $s.Rate = {PwshRate()}; $s.Speak('{escaped}')\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "say",
                    Arguments = $"-r {SpeechRate} -- \"{EscapeText(text)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                // Linux: espeak-ng
                psi = new ProcessStartInfo
                {
                    FileName = "espeak-ng",
                    Arguments = $"-v {Voice} -s {SpeechRate} -- \"{EscapeText(text)}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            var tool = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "PowerShell/SAPI"
                     : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "say"
                     : "espeak-ng";
            _logger.LogError(ex, "Failed to start {Tool}. Ensure TTS is available on your system", tool);
            return null;
        }
    }

    /// <summary>
    /// Converts words-per-minute rate to PowerShell SpeechSynthesizer.Rate (-10 to 10 scale).
    /// 160 wpm ≈ 0 (default), 200 wpm ≈ 2, 120 wpm ≈ -2, etc.
    /// </summary>
    private int PwshRate()
    {
        var rate = (SpeechRate - 160) / 20;
        return Math.Clamp(rate, -10, 10);
    }

    /// <summary>
    /// Escapes text for safe shell argument passing (Linux/macOS).
    /// </summary>
    private static string EscapeText(string text)
    {
        return text
            .Replace("\"", "'")
            .Replace("\\", "")
            .Replace("\n", " ")
            .Replace("\r", "");
    }

    /// <summary>
    /// Escapes text for safe embedding in a PowerShell single-quoted string.
    /// </summary>
    private static string EscapeForPowerShell(string text)
    {
        return text
            .Replace("'", "''")
            .Replace("\n", " ")
            .Replace("\r", "");
    }
}
