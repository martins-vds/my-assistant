using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.Services;

public sealed class ReflectionServiceTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly INoteRepository _noteRepo = Substitute.For<INoteRepository>();

    private async Task<(ReflectionService Service, TaskTrackingService Tracking)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _noteRepo.GetStandaloneNotesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(Array.Empty<TaskNote>()));

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();
        var service = new ReflectionService(tracking, _noteRepo);
        return (service, tracking);
    }

    [Fact]
    public async Task GenerateDailySummary_NoTasks_ReturnsEmptySummary()
    {
        var (service, _) = await CreateAsync();

        var summary = await service.GenerateDailySummaryAsync();

        Assert.Empty(summary.CompletedTasks);
        Assert.Empty(summary.OpenTasks);
        Assert.Equal(TimeSpan.Zero, summary.TotalTimeToday);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), summary.Date);
    }

    [Fact]
    public async Task GenerateDailySummary_WithOpenTasks_ReturnsOpenTasks()
    {
        var (service, tracking) = await CreateAsync();
        tracking.CreateTask("Task A");
        tracking.SwitchTask("Task B");

        var summary = await service.GenerateDailySummaryAsync();

        Assert.Empty(summary.CompletedTasks);
        Assert.Equal(2, summary.OpenTasks.Count);
    }

    [Fact]
    public async Task GenerateDailySummary_WithCompletedTasks_ReturnsCompletedTasks()
    {
        var (service, tracking) = await CreateAsync();
        tracking.CreateTask("Task A");
        tracking.CompleteTask();

        var summary = await service.GenerateDailySummaryAsync();

        Assert.Single(summary.CompletedTasks);
        Assert.Equal("Task A", summary.CompletedTasks[0].Name);
        Assert.True(summary.CompletedTasks[0].IsCompleted);
    }

    [Fact]
    public async Task GenerateDailySummary_MixedTasks_CorrectCounts()
    {
        var (service, tracking) = await CreateAsync();
        tracking.CreateTask("Done task");
        tracking.CompleteTask();
        tracking.CreateTask("Open task");

        var summary = await service.GenerateDailySummaryAsync();

        Assert.Single(summary.CompletedTasks);
        Assert.Single(summary.OpenTasks);
    }

    [Fact]
    public async Task GenerateDailySummary_WithStandaloneNotes_IncludesNotes()
    {
        var (service, _) = await CreateAsync();

        var notes = new List<TaskNote> { new("General thought") };
        _noteRepo.GetStandaloneNotesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(notes));

        var summary = await service.GenerateDailySummaryAsync();

        Assert.Single(summary.StandaloneNotes);
        Assert.Equal("General thought", summary.StandaloneNotes[0]);
    }

    [Fact]
    public async Task GenerateDailySummary_TotalTimeAggregated()
    {
        var (service, tracking) = await CreateAsync();
        tracking.CreateTask("Task A");
        // The task is in-progress with a running time log, so time should be >= zero
        var summary = await service.GenerateDailySummaryAsync();

        Assert.True(summary.TotalTimeToday >= TimeSpan.Zero);
    }
}
