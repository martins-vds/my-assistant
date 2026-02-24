using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class GetOpenTasksUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private async Task<(GetOpenTasksUseCase UseCase, TaskTrackingService Service)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await service.InitializeAsync();
        return (new GetOpenTasksUseCase(service), service);
    }

    [Fact]
    public async Task Execute_NoTasks_ReturnsEmpty()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Tasks);
        Assert.Null(result.CurrentTaskName);
    }

    [Fact]
    public async Task Execute_ReturnsOpenTasks()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");

        var result = await sut.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Tasks.Count);
    }

    [Fact]
    public async Task Execute_MarksCurrentTask()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");

        var result = await sut.ExecuteAsync();

        var current = result.Tasks.Single(t => t.IsCurrent);
        Assert.Equal("Second", current.Name);
    }

    [Fact]
    public async Task Execute_ReturnsCurrentTaskName()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Active Task");

        var result = await sut.ExecuteAsync();

        Assert.Equal("Active Task", result.CurrentTaskName);
    }

    [Fact]
    public async Task Execute_ExcludesCompletedTasks()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Done");
        service.CompleteTask();
        service.CreateTask("Active");

        var result = await sut.ExecuteAsync();

        Assert.Single(result.Tasks);
        Assert.Equal("Active", result.Tasks[0].Name);
    }

    [Fact]
    public async Task Execute_IncludesTaskStatus()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");

        var result = await sut.ExecuteAsync();

        var first = result.Tasks.Single(t => t.Name == "First");
        var second = result.Tasks.Single(t => t.Name == "Second");

        Assert.Equal("Paused", first.Status);
        Assert.Equal("InProgress", second.Status);
    }

    [Fact]
    public async Task Execute_IncludesTimeSpent()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Working");
        // Task was just created, so TimeSpentToday might be 0 or very small
        var result = await sut.ExecuteAsync();

        var task = result.Tasks.Single();
        Assert.True(task.TimeSpentToday >= TimeSpan.Zero);
        Assert.True(task.TotalTimeSpent >= TimeSpan.Zero);
    }
}
