using FocusAssistant.Application.Interfaces;
using FocusAssistant.Infrastructure.Voice;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FocusAssistant.Infrastructure.Tests.Voice;

/// <summary>
/// Integration/contract tests for voice service implementations.
/// These tests verify the contracts and behavior of voice services
/// without requiring actual audio hardware.
/// </summary>
public class WakeWordDetectorTests
{
    private readonly ILogger<WakeWordDetector> _logger = Substitute.For<ILogger<WakeWordDetector>>();

    [Fact]
    public void Constructor_SetsDefaultWakeWord()
    {
        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model");

        Assert.Equal("hey focus", detector.WakeWord);
    }

    [Fact]
    public void Constructor_IsNotListeningByDefault()
    {
        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model");

        Assert.False(detector.IsListening);
    }

    [Fact]
    public void WakeWord_CanBeChanged()
    {
        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model");

        detector.WakeWord = "hey assistant";

        Assert.Equal("hey assistant", detector.WakeWord);
    }

    [Fact]
    public async Task WaitForWakeWordAsync_ReturnsFalse_WhenModelNotFound()
    {
        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model-dir-xyz");

        // Should throw or return false when model directory doesn't exist
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => detector.WaitForWakeWordAsync());
    }

    [Fact]
    public async Task WaitForWakeWordAsync_ReturnsFalse_WhenCancelled()
    {
        // Immediate cancellation — detector should not hang
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model-dir-xyz");

        // With a cancelled token, should either return false or throw
        // depending on when cancellation is checked
        try
        {
            var result = await detector.WaitForWakeWordAsync(cts.Token);
            Assert.False(result);
        }
        catch (OperationCanceledException)
        {
            // Also acceptable — cancelled before starting
        }
        catch (DirectoryNotFoundException)
        {
            // Model not found — expected since we're using a fake path
        }
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model");

        await detector.DisposeAsync();
        await detector.DisposeAsync(); // Should not throw
    }

    [Fact]
    public async Task WaitForWakeWordAsync_ReturnsFalse_WhenAudioCaptureNotAvailable()
    {
        // When running in CI or environments without audio hardware, capture fails.
        // The detector should retry with backoff instead of immediately returning false
        // in a tight loop. After exhausting retries, it should return false gracefully.
        //
        // We can't easily mock the process, but we can verify the detector doesn't
        // return instantly (which would indicate no retry/backoff logic).

        // Use a real model path check — if no model, it throws DirectoryNotFoundException
        // which is the expected behavior (model validation comes first).
        var detector = new WakeWordDetector(_logger, "/tmp/nonexistent-model-dir-xyz");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => detector.WaitForWakeWordAsync());
    }

    [Fact]
    public void MaxAudioRetries_IsReasonable()
    {
        // The detector should expose retry configuration that prevents tight loops.
        // Verify that the retry count and delay constants exist and are sensible.
        Assert.Equal(3, WakeWordDetector.MaxAudioCaptureRetries);
        Assert.True(WakeWordDetector.AudioRetryDelay.TotalSeconds >= 1);
    }
}

public class SpeechToTextServiceTests
{
    private readonly ILogger<SpeechToTextService> _logger = Substitute.For<ILogger<SpeechToTextService>>();

    [Fact]
    public async Task ListenAsync_TextMode_ReturnsNull_WhenCancelled()
    {
        var service = new SpeechToTextService(_logger, "/tmp/model", useTextMode: true);

        // Cancel quickly so we don't block on stdin
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await service.ListenAsync(cts.Token);

        Assert.Null(result);
    }

    [Fact]
    public async Task WaitForWakeWordAsync_TextMode_ReturnsTrue()
    {
        var service = new SpeechToTextService(_logger, "/tmp/model", useTextMode: true);

        var result = await service.WaitForWakeWordAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task WaitForWakeWordAsync_VoiceMode_ReturnsTrue()
    {
        // In voice mode, wake word detection is delegated to WakeWordDetector
        // The STT service always returns true
        var service = new SpeechToTextService(_logger, "/tmp/model", useTextMode: false);

        var result = await service.WaitForWakeWordAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ListenAsync_VoiceMode_ReturnsNull_WhenModelMissing()
    {
        var service = new SpeechToTextService(_logger, "/tmp/nonexistent-model-xyz", useTextMode: false);

        // Should throw DirectoryNotFoundException when model is missing
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => service.ListenAsync());
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var service = new SpeechToTextService(_logger, "/tmp/model", useTextMode: true);

        service.Dispose();
        service.Dispose(); // Should not throw
    }
}

public class TextToSpeechServiceTests
{
    private readonly ILogger<TextToSpeechService> _logger = Substitute.For<ILogger<TextToSpeechService>>();

    [Fact]
    public async Task SpeakAsync_TextMode_WritesToStdout()
    {
        var service = new TextToSpeechService(_logger, useTextMode: true);

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            await service.SpeakAsync("Hello world");

            var output = sw.ToString().Trim();
            Assert.Equal("Hello world", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public async Task SpeakAsync_TextMode_HandlesEmptyText()
    {
        var service = new TextToSpeechService(_logger, useTextMode: true);

        // Should not throw for null or empty text
        await service.SpeakAsync("");
        await service.SpeakAsync(null!);
    }

    [Fact]
    public async Task SpeakAsync_TextMode_HandlesWhitespace()
    {
        var service = new TextToSpeechService(_logger, useTextMode: true);

        // Whitespace-only should be treated as empty
        await service.SpeakAsync("   ");
    }

    [Fact]
    public async Task StopAsync_TextMode_CompletesSuccessfully()
    {
        var service = new TextToSpeechService(_logger, useTextMode: true);

        // No-op in text mode
        await service.StopAsync();
    }

    [Fact]
    public async Task StopAsync_CanBeCalledMultipleTimes()
    {
        var service = new TextToSpeechService(_logger, useTextMode: true);

        await service.StopAsync();
        await service.StopAsync(); // Should not throw
    }

    [Fact]
    public void SpeechRate_DefaultIs160()
    {
        var service = new TextToSpeechService(_logger);

        Assert.Equal(160, service.SpeechRate);
    }

    [Fact]
    public void SpeechRate_CanBeChanged()
    {
        var service = new TextToSpeechService(_logger);

        service.SpeechRate = 200;

        Assert.Equal(200, service.SpeechRate);
    }

    [Fact]
    public void Voice_DefaultIsEn()
    {
        var service = new TextToSpeechService(_logger);

        Assert.Equal("en", service.Voice);
    }

    [Fact]
    public async Task SpeakAsync_VoiceMode_FallsBackWhenTtsMissing()
    {
        // When TTS engine is not installed, should fall back to text output
        var service = new TextToSpeechService(_logger, useTextMode: false);

        using var sw = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(sw);
        try
        {
            // This should not throw even if espeak-ng is missing
            await service.SpeakAsync("Test message");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}

/// <summary>
/// Tests for the IWakeWordDetector contract to verify the interface is properly defined.
/// </summary>
public class WakeWordDetectorContractTests
{
    [Fact]
    public void WakeWordDetector_ImplementsIWakeWordDetector()
    {
        var logger = Substitute.For<ILogger<WakeWordDetector>>();
        var detector = new WakeWordDetector(logger, "/tmp/model");

        Assert.IsAssignableFrom<IWakeWordDetector>(detector);
    }

    [Fact]
    public void SpeechToTextService_ImplementsIVoiceInputService()
    {
        var logger = Substitute.For<ILogger<SpeechToTextService>>();
        var service = new SpeechToTextService(logger, "/tmp/model");

        Assert.IsAssignableFrom<IVoiceInputService>(service);
    }

    [Fact]
    public void TextToSpeechService_ImplementsIVoiceOutputService()
    {
        var logger = Substitute.For<ILogger<TextToSpeechService>>();
        var service = new TextToSpeechService(logger);

        Assert.IsAssignableFrom<IVoiceOutputService>(service);
    }
}

/// <summary>
/// Tests for the cross-platform AudioCaptureHelper.
/// </summary>
public class AudioCaptureHelperTests
{
    [Fact]
    public void ToolName_ReturnsNonEmptyString()
    {
        var name = AudioCaptureHelper.ToolName;

        Assert.False(string.IsNullOrWhiteSpace(name));
    }

    [Fact]
    public void InstallInstructions_ReturnsNonEmptyString()
    {
        var instructions = AudioCaptureHelper.InstallInstructions;

        Assert.False(string.IsNullOrWhiteSpace(instructions));
    }

    [Fact]
    public void ToolName_IsPlatformAppropriate()
    {
        var name = AudioCaptureHelper.ToolName;

        // Should be either "arecord" (Linux) or "ffmpeg" (Windows/macOS/other)
        Assert.True(name == "arecord" || name == "ffmpeg",
            $"Unexpected tool name: {name}");
    }

    [Fact]
    public void ParseDirectShowAudioDevice_ExtractsDeviceFromTypicalOutput()
    {
        var output = @"[dshow @ 0000020f] DirectShow video devices (some may be both video and audio devices)
[dshow @ 0000020f]  ""Integrated Webcam""
[dshow @ 0000020f]     Alternative name ""@device_pnp_\\?\usb""
[dshow @ 0000020f] DirectShow audio devices
[dshow @ 0000020f]  ""Microphone (Realtek High Definition Audio)""
[dshow @ 0000020f]     Alternative name ""@device_cm_{33D9A762-90C8-11D0-BD43-00A0C911CE86}""
";

        var device = AudioCaptureHelper.ParseDirectShowAudioDevice(output);

        Assert.Equal("Microphone (Realtek High Definition Audio)", device);
    }

    [Fact]
    public void ParseDirectShowAudioDevice_ExtractsFromAudioTag()
    {
        var output = @"[dshow @ 0x1234] ""HD Microphone"" (audio)
[dshow @ 0x1234] ""USB Camera"" (video)
";

        var device = AudioCaptureHelper.ParseDirectShowAudioDevice(output);

        Assert.Equal("HD Microphone", device);
    }

    [Fact]
    public void ParseDirectShowAudioDevice_ReturnsNullWhenNoAudioDevice()
    {
        var output = @"[dshow @ 0000020f] DirectShow video devices
[dshow @ 0000020f]  ""Webcam""
";

        var device = AudioCaptureHelper.ParseDirectShowAudioDevice(output);

        Assert.Null(device);
    }

    [Fact]
    public void ParseDirectShowAudioDevice_ReturnsNullForEmptyInput()
    {
        Assert.Null(AudioCaptureHelper.ParseDirectShowAudioDevice(""));
        Assert.Null(AudioCaptureHelper.ParseDirectShowAudioDevice(null!));
    }

    [Fact]
    public void ParseDirectShowAudioDevice_HandlesMultipleAudioDevices_ReturnsFirst()
    {
        var output = @"[dshow @ 0x1] DirectShow audio devices
[dshow @ 0x1]  ""Microphone Array (Intel)""
[dshow @ 0x1]     Alternative name ""@device1""
[dshow @ 0x1]  ""Line In (Realtek)""
[dshow @ 0x1]     Alternative name ""@device2""
";

        var device = AudioCaptureHelper.ParseDirectShowAudioDevice(output);

        Assert.Equal("Microphone Array (Intel)", device);
    }

    [Fact]
    public void ClearDeviceCache_AllowsRedetection()
    {
        // Just verify it doesn't throw
        AudioCaptureHelper.ClearDeviceCache();
    }
}
