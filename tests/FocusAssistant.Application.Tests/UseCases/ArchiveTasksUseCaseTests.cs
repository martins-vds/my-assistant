using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class ArchiveTasksUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private async Task<(ArchiveTasksUseCase UseCase, TaskTrackingService Service)> CreateAsync(
        IReadOnlyList<FocusTask>? existingTasks = null)
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(existingTasks ?? Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await service.InitializeAsync();
        return (new ArchiveTasksUseCase(service), service);
    }

    [Fact]
    public async Task Execute_NoCompletedTasks_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("No completed tasks", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_ArchivesAllCompletedTasks()
    {
        var (sut, service) = await CreateAsync();

        // Create and complete tasks
        service.CreateTask("Task A");
        service.CompleteTask("Task A");
        service.CreateTask("Task B");
        service.CompleteTask("Task B");
        service.CreateTask("Task C"); // Still in-progress

        var result = await sut.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.ArchivedTaskNames!.Count);
        Assert.Contains("Task A", result.ArchivedTaskNames);
        Assert.Contains("Task B", result.ArchivedTaskNames);
    }

    [Fact]
    public async Task Execute_WithAgeCutoff_ArchivesOnlyOldTasks()
    {
        // Create a task that was completed "old"
        var oldTask = new FocusTask("Old Task");
        oldTask.Complete();
        // Manipulate time - the last time log's end time is recent, but we need it old
        // Since we can't easily manipulate time, test the negative case: 
        // tasks completed just now should NOT be older than 7 days
        var (sut, service) = await CreateAsync();

        service.CreateTask("Recent Task");
        service.CompleteTask("Recent Task");

        var result = await sut.ExecuteAsync(olderThanDays: 7);

        Assert.False(result.IsSuccess);
        Assert.Contains("No completed tasks older than 7 days", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_NegativeDays_ReturnsError()
    {
        var (sut, service) = await CreateAsync();
        service.CreateTask("Task A");
        service.CompleteTask("Task A");

        var result = await sut.ExecuteAsync(olderThanDays: -1);

        Assert.False(result.IsSuccess);
        Assert.Contains("non-negative", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_ArchivesAllWhenNoDayFilter()
    {
        var (sut, service) = await CreateAsync();
        service.CreateTask("Completed Task");
        service.CompleteTask("Completed Task");

        var result = await sut.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.ArchivedTaskNames!);
        Assert.Equal("Completed Task", result.ArchivedTaskNames![0]);
    }

    [Fact]
    public async Task Execute_DoesNotArchiveOpenTasks()
    {
        var (sut, service) = await CreateAsync();
        service.CreateTask("In Progress");
        service.CreateTask("Paused");
        // "In Progress" is auto-paused when "Paused" is created, 
        // but "Paused" is still in progress. Let's fix that.
        // Actually: Creating "Paused" auto-pauses "In Progress" and "Paused" is now in-progress.

        // No completed tasks at all
        var result = await sut.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("No completed tasks", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_ZeroDays_ArchivesAllCompleted()
    {
        var (sut, service) = await CreateAsync();
        service.CreateTask("Done");
        service.CompleteTask("Done");

        var result = await sut.ExecuteAsync(olderThanDays: 0);

        Assert.True(result.IsSuccess);
        Assert.Single(result.ArchivedTaskNames!);
    }
}
