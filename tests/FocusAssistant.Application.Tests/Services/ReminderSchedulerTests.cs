using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;
using NSubstitute;

namespace FocusAssistant.Application.Tests.Services;

public sealed class ReminderSchedulerTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private async Task<(ReminderScheduler Scheduler, TaskTrackingService Tracking)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();
        var scheduler = new ReminderScheduler(tracking, _prefsRepo);
        return (scheduler, tracking);
    }

    [Fact]
    public void RecordInteraction_ResetsIdleTime()
    {
        var tracking = new TaskTrackingService(
            Substitute.For<ITaskRepository>(),
            Substitute.For<ISessionRepository>(),
            Substitute.For<IUserPreferencesRepository>());
        var scheduler = new ReminderScheduler(tracking, _prefsRepo);

        scheduler.RecordInteraction();

        Assert.True(scheduler.GetIdleTime() < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task IsIdleCheckInDue_WhenNotIdle_ReturnsFalse()
    {
        var (scheduler, _) = await CreateAsync();

        scheduler.RecordInteraction();
        var result = await scheduler.IsIdleCheckInDueAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsIdleCheckInDue_WhenFocusSuppressed_ReturnsFalse()
    {
        var (scheduler, _) = await CreateAsync();

        // Set up a very short threshold so we'd normally trigger
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(
                new UserPreferences(idleCheckInThreshold: TimeSpan.FromMilliseconds(1))));

        await Task.Delay(5); // Exceed threshold

        scheduler.SuppressDuringFocus();
        var result = await scheduler.IsIdleCheckInDueAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsIdleCheckInDue_WithActiveTask_ReturnsFalse()
    {
        var (scheduler, tracking) = await CreateAsync();

        // Very short threshold
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(
                new UserPreferences(idleCheckInThreshold: TimeSpan.FromMilliseconds(1))));

        tracking.CreateTask("Active");
        await Task.Delay(5);

        var result = await scheduler.IsIdleCheckInDueAsync();

        // Has an active task, so no idle check-in
        Assert.False(result);
    }

    [Fact]
    public async Task IsIdleCheckInDue_IdleNoTask_ReturnsTrue()
    {
        var (scheduler, _) = await CreateAsync();

        // Very short threshold
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(
                new UserPreferences(idleCheckInThreshold: TimeSpan.FromMilliseconds(1))));

        await Task.Delay(5);

        var result = await scheduler.IsIdleCheckInDueAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task ResumeFocusReminders_AllowsCheckInsAgain()
    {
        var (scheduler, _) = await CreateAsync();

        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(
                new UserPreferences(idleCheckInThreshold: TimeSpan.FromMilliseconds(1))));

        await Task.Delay(5);

        scheduler.SuppressDuringFocus();
        Assert.False(await scheduler.IsIdleCheckInDueAsync());

        scheduler.ResumeFocusReminders();
        Assert.True(await scheduler.IsIdleCheckInDueAsync());
    }

    [Fact]
    public async Task GetDueReminders_NoPausedTasks_ReturnsEmpty()
    {
        var (scheduler, _) = await CreateAsync();

        var reminders = await scheduler.GetDueRemindersAsync();

        Assert.Empty(reminders);
    }

    [Fact]
    public async Task GetDueReminders_WhenFocusSuppressed_ReturnsEmpty()
    {
        var (scheduler, tracking) = await CreateAsync();

        tracking.CreateTask("Task A");
        tracking.SwitchTask("Task B");

        scheduler.SuppressDuringFocus();
        var reminders = await scheduler.GetDueRemindersAsync();

        Assert.Empty(reminders);
    }

    [Fact]
    public async Task GetDueReminders_RecentlyPausedTask_ReturnsEmpty()
    {
        var (scheduler, tracking) = await CreateAsync();

        // Default interval is 1 hour, task was just paused
        tracking.CreateTask("Task A");
        tracking.SwitchTask("Task B");

        var reminders = await scheduler.GetDueRemindersAsync();

        // Task A was just paused, so it's not due yet (1 hour default)
        Assert.Empty(reminders);
    }

    [Fact]
    public async Task GetDueReminders_PerTaskOverride_Respected()
    {
        var (scheduler, tracking) = await CreateAsync();

        var taskA = tracking.CreateTask("Task A");
        // Set a very short per-task reminder (0.001 min = 60 ms)
        taskA.SetReminderInterval(ReminderInterval.FromMinutes(0.001, isPerTaskOverride: true));
        tracking.SwitchTask("Task B");

        // Wait enough for the interval to elapse (60ms interval + margin)
        await Task.Delay(150);

        var reminders = await scheduler.GetDueRemindersAsync();

        // Very short interval, should be due now
        Assert.Single(reminders);
        Assert.Equal("Task A", reminders[0].TaskName);
    }

    [Fact]
    public void GetIdleTime_ReturnsPositiveValue()
    {
        var tracking = new TaskTrackingService(
            Substitute.For<ITaskRepository>(),
            Substitute.For<ISessionRepository>(),
            Substitute.For<IUserPreferencesRepository>());
        var scheduler = new ReminderScheduler(tracking, _prefsRepo);

        var idle = scheduler.GetIdleTime();

        Assert.True(idle >= TimeSpan.Zero);
    }
}
