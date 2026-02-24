using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class CreateTaskUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private async Task<(CreateTaskUseCase UseCase, TaskTrackingService Service)> CreateAsync()
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
        return (new CreateTaskUseCase(service), service);
    }

    [Fact]
    public async Task Execute_CreatesNewTask()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("Build feature");

        Assert.True(result.IsSuccess);
        Assert.Equal("Build feature", result.TaskName);
    }

    [Fact]
    public async Task Execute_EmptyName_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_WhitespaceName_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("   ");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_DuplicateName_ReturnsDuplicate()
    {
        var (sut, _) = await CreateAsync();

        await sut.ExecuteAsync("Existing Task");
        var result = await sut.ExecuteAsync("Existing Task");

        Assert.True(result.IsDuplicate);
        Assert.Equal("Existing Task", result.TaskName);
    }

    [Fact]
    public async Task Execute_DuplicateNameWithForce_CreatesTask()
    {
        var (sut, _) = await CreateAsync();

        await sut.ExecuteAsync("Existing Task");
        var result = await sut.ExecuteAsync("Existing Task", force: true);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Execute_PausesCurrentTask_ReportsInResult()
    {
        var (sut, _) = await CreateAsync();

        await sut.ExecuteAsync("First Task");
        var result = await sut.ExecuteAsync("Second Task");

        Assert.True(result.IsSuccess);
        Assert.Equal("Second Task", result.TaskName);
        // PausedTaskName should name the just-paused task
        Assert.Equal("First Task", result.PausedTaskName);
    }

    [Fact]
    public async Task Execute_SavesStateAfterCreation()
    {
        var (sut, _) = await CreateAsync();

        await sut.ExecuteAsync("New Task");

        await _taskRepo.Received().SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>());
    }
}
