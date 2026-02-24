using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class CompleteTaskUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private async Task<(CompleteTaskUseCase UseCase, TaskTrackingService Service)> CreateAsync()
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
        return (new CompleteTaskUseCase(service), service);
    }

    [Fact]
    public async Task Execute_CompletesCurrentTask()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("My Task");
        var result = await sut.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("My Task", result.TaskName);
    }

    [Fact]
    public async Task Execute_CompletesByName()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");

        var result = await sut.ExecuteAsync("First");

        Assert.True(result.IsSuccess);
        Assert.Equal("First", result.TaskName);
    }

    [Fact]
    public async Task Execute_NoCurrentTask_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync();

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_ReturnsPausedTaskSuggestions()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");
        service.SwitchTask("Third");

        // Complete the current (Third), should suggest First and Second as paused
        var result = await sut.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("First", result.PausedTaskSuggestions);
        Assert.Contains("Second", result.PausedTaskSuggestions);
    }

    [Fact]
    public async Task Execute_NonexistentName_ReturnsError()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Existing");
        var result = await sut.ExecuteAsync("Missing");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_SavesStateAfterCompletion()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Task");
        await sut.ExecuteAsync();

        await _taskRepo.Received().SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>());
    }
}
