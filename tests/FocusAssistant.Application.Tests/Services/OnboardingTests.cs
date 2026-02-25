using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;
using NSubstitute;

namespace FocusAssistant.Application.Tests.Services;

public class OnboardingDetectionTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    [Fact]
    public async Task NeedsOnboarding_True_WhenNoPreferencesExist()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _prefsRepo.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();

        Assert.True(tracking.NeedsOnboarding);
    }

    [Fact]
    public async Task NeedsOnboarding_False_WhenPreferencesExist()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _prefsRepo.ExistsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();

        Assert.False(tracking.NeedsOnboarding);
    }
}

public class SavePreferencesUseCaseTests
{
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    [Fact]
    public async Task ExecuteAsync_CreatesNewPreferences_WhenNoneExist()
    {
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.ExecuteAsync(
            reminderIntervalMinutes: 30,
            idleThresholdMinutes: 10,
            reflectionTime: "17:00",
            wakeWord: "Hey Assistant");

        Assert.True(result.IsSuccess);
        await _prefsRepo.Received(1).SaveAsync(Arg.Is<UserPreferences>(p =>
            p.DefaultReminderInterval.Duration == TimeSpan.FromMinutes(30) &&
            p.IdleCheckInThreshold == TimeSpan.FromMinutes(10) &&
            p.AutomaticReflectionTime == new TimeOnly(17, 0) &&
            p.WakeWord == "Hey Assistant"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UsesDefaults_WhenNoValuesProvided()
    {
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        await _prefsRepo.Received(1).SaveAsync(Arg.Is<UserPreferences>(p =>
            p.DefaultReminderInterval == ReminderInterval.Default &&
            p.IdleCheckInThreshold == TimeSpan.FromMinutes(5) &&
            p.WakeWord == "Hey Focus"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_UpdatesExistingPreferences()
    {
        var existing = new UserPreferences(
            defaultReminderInterval: new ReminderInterval(TimeSpan.FromMinutes(60)),
            idleCheckInThreshold: TimeSpan.FromMinutes(5));
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(existing));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.ExecuteAsync(reminderIntervalMinutes: 45);

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMinutes(45), existing.DefaultReminderInterval.Duration);
    }

    [Fact]
    public async Task UpdateAsync_ChangesReminderInterval()
    {
        var prefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("reminder_interval", "45");

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMinutes(45), prefs.DefaultReminderInterval.Duration);
    }

    [Fact]
    public async Task UpdateAsync_ChangesIdleThreshold()
    {
        var prefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("idle_threshold", "20");

        Assert.True(result.IsSuccess);
        Assert.Equal(TimeSpan.FromMinutes(20), prefs.IdleCheckInThreshold);
    }

    [Fact]
    public async Task UpdateAsync_ChangesReflectionTime()
    {
        var prefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("reflection_time", "17:30");

        Assert.True(result.IsSuccess);
        Assert.Equal(new TimeOnly(17, 30), prefs.AutomaticReflectionTime);
    }

    [Fact]
    public async Task UpdateAsync_DisablesReflectionTime_WithNone()
    {
        var prefs = new UserPreferences(automaticReflectionTime: new TimeOnly(17, 0));
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("reflection_time", "none");

        Assert.True(result.IsSuccess);
        Assert.Null(prefs.AutomaticReflectionTime);
    }

    [Fact]
    public async Task UpdateAsync_ChangesWakeWord()
    {
        var prefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("wake_word", "Hey Buddy");

        Assert.True(result.IsSuccess);
        Assert.Equal("Hey Buddy", prefs.WakeWord);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsError_ForUnknownSetting()
    {
        var prefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("unknown_setting", "value");

        Assert.False(result.IsSuccess);
        Assert.Contains("Unknown setting", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsError_WhenNoPreferencesExist()
    {
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("reminder_interval", "30");

        Assert.False(result.IsSuccess);
        Assert.Contains("No preferences found", result.Message);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsError_ForInvalidReminderInterval()
    {
        var prefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.UpdateAsync("reminder_interval", "not-a-number");

        Assert.False(result.IsSuccess);
        Assert.Contains("Invalid reminder interval", result.Message);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsPreferencesSummary()
    {
        var prefs = new UserPreferences(
            defaultReminderInterval: new ReminderInterval(TimeSpan.FromMinutes(30)),
            idleCheckInThreshold: TimeSpan.FromMinutes(10),
            automaticReflectionTime: new TimeOnly(17, 0),
            wakeWord: "Hey Focus");
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(prefs));

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.GetCurrentAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("30 minutes", result.Summary);
        Assert.Contains("10 minutes", result.Summary);
        Assert.Contains("17:00", result.Summary);
        Assert.Contains("Hey Focus", result.Summary);
    }

    [Fact]
    public async Task GetCurrentAsync_ReturnsError_WhenNoPreferences()
    {
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.GetCurrentAsync();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_ParsesReflectionTime_Correctly()
    {
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var useCase = new SavePreferencesUseCase(_prefsRepo);
        var result = await useCase.ExecuteAsync(reflectionTime: "16:30");

        Assert.True(result.IsSuccess);
        await _prefsRepo.Received(1).SaveAsync(Arg.Is<UserPreferences>(p =>
            p.AutomaticReflectionTime == new TimeOnly(16, 30)), Arg.Any<CancellationToken>());
    }
}
