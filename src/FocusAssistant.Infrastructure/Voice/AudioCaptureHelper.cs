using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FocusAssistant.Infrastructure.Voice;

/// <summary>
/// Platform-aware helper that starts an external audio capture process.
/// Linux: uses arecord (ALSA).
/// Windows: uses ffmpeg with DirectShow audio capture (auto-detects device).
/// macOS: uses ffmpeg with AVFoundation audio capture.
/// All produce raw 16-bit LE PCM at 16kHz mono on stdout.
/// </summary>
public static class AudioCaptureHelper
{
    /// <summary>
    /// Cached Windows DirectShow audio device name (detected once, reused).
    /// </summary>
    private static string? _cachedWindowsDevice;

    /// <summary>
    /// Returns the human-readable name of the audio capture tool for the current platform.
    /// </summary>
    public static string ToolName =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "arecord" : "ffmpeg";

    /// <summary>
    /// Returns install instructions for the current platform.
    /// </summary>
    public static string InstallInstructions
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Install ffmpeg: winget install ffmpeg  (or download from https://ffmpeg.org)";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "Install ffmpeg: brew install ffmpeg";
            return "Install arecord: sudo apt-get install alsa-utils  (or your distro's equivalent)";
        }
    }

    /// <summary>
    /// Starts a platform-appropriate audio capture process producing raw 16kHz mono 16-bit LE PCM on stdout.
    /// Returns null if the process cannot be started.
    /// </summary>
    /// <param name="deviceName">
    /// Optional device name override. On Windows this is a DirectShow audio device name.
    /// On Linux/macOS null uses the system default.
    /// </param>
    public static Process? StartCapture(string? deviceName = null)
    {
        ProcessStartInfo psi;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi = new ProcessStartInfo
            {
                FileName = "arecord",
                Arguments = "-q -r 16000 -c 1 -f S16_LE -t raw",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // ffmpeg DirectShow capture → raw PCM on stdout.
            // DirectShow does NOT support "default" — we must detect the real device name.
            var device = deviceName ?? DetectWindowsAudioDevice();
            if (device is null)
                return null; // No audio input device found

            psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-f dshow -i audio=\"{device}\" -ar 16000 -ac 1 -f s16le -acodec pcm_s16le -loglevel error pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // ffmpeg AVFoundation capture → raw PCM on stdout
            var device = deviceName ?? ":0";
            psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-f avfoundation -i \"{device}\" -ar 16000 -ac 1 -f s16le -acodec pcm_s16le -loglevel error pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }
        else
        {
            // Unknown platform — fall back to ffmpeg
            psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-f s16le -ar 16000 -ac 1 -loglevel error pipe:1",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
        }

        return Process.Start(psi);
    }

    /// <summary>
    /// Detects the first available DirectShow audio input device on Windows by
    /// running <c>ffmpeg -list_devices true -f dshow -i dummy</c> and parsing
    /// the stderr output for audio device names.
    /// Returns null if no audio device is found.
    /// </summary>
    internal static string? DetectWindowsAudioDevice()
    {
        if (_cachedWindowsDevice is not null)
            return _cachedWindowsDevice;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-list_devices true -f dshow -i dummy",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            // Device list is written to stderr, not stdout
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            return ParseDirectShowAudioDevice(stderr);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses ffmpeg DirectShow device listing output to find the first audio input device.
    /// The output format is:
    /// <code>
    /// [dshow @ ...] "Device Name" (audio)
    /// </code>
    /// We look for lines containing "(audio)" and extract the quoted device name.
    /// </summary>
    internal static string? ParseDirectShowAudioDevice(string ffmpegOutput)
    {
        if (string.IsNullOrWhiteSpace(ffmpegOutput))
            return null;

        var inAudioSection = false;
        foreach (var line in ffmpegOutput.Split('\n'))
        {
            var trimmed = line.Trim();

            // Detect the section header: DirectShow audio devices
            if (trimmed.Contains("DirectShow audio devices"))
            {
                inAudioSection = true;
                continue;
            }

            // End of audio section when a new section starts
            if (inAudioSection && trimmed.Contains("DirectShow video devices"))
                break;

            // Look for "(audio)" marker in any line (works even without section headers)
            if (trimmed.Contains("(audio)"))
            {
                var deviceName = ExtractQuotedDeviceName(trimmed);
                if (deviceName is not null)
                {
                    _cachedWindowsDevice = deviceName;
                    return deviceName;
                }
            }

            // Within audio section, also match lines with quoted device names
            // (some ffmpeg versions don't include the (audio) tag on device lines)
            if (inAudioSection && !trimmed.Contains("Alternative name"))
            {
                var deviceName = ExtractQuotedDeviceName(trimmed);
                if (deviceName is not null)
                {
                    _cachedWindowsDevice = deviceName;
                    return deviceName;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts a device name from a line like: [dshow @ 0x...] "Microphone (Realtek Audio)" (audio)
    /// </summary>
    private static string? ExtractQuotedDeviceName(string line)
    {
        var firstQuote = line.IndexOf('"');
        if (firstQuote < 0) return null;

        var secondQuote = line.IndexOf('"', firstQuote + 1);
        if (secondQuote <= firstQuote + 1) return null;

        return line.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
    }

    /// <summary>
    /// Clears the cached Windows audio device name. Useful for testing or when
    /// devices change at runtime.
    /// </summary>
    internal static void ClearDeviceCache() => _cachedWindowsDevice = null;

    /// <summary>
    /// Tests whether audio capture is available by attempting to start and immediately stop the capture process.
    /// Returns true if the capture tool exists and can be invoked.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            var process = StartCapture();
            if (process is null) return false;

            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }
            finally
            {
                process.Dispose();
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
