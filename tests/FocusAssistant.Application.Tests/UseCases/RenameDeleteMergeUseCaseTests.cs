using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class RenameTaskUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();

    private async Task<(RenameTaskUseCase UseCase, TaskTrackingService Service)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new TaskTrackingService(_taskRepo, _sessionRepo);
        await service.InitializeAsync();
        return (new RenameTaskUseCase(service), service);
    }

    [Fact]
    public async Task Execute_RenamesTask()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Old Name");
        var result = await sut.ExecuteAsync("Old Name", "New Name");

        Assert.True(result.IsSuccess);
        Assert.Equal("Old Name", result.OldName);
        Assert.Equal("New Name", result.NewName);
    }

    [Fact]
    public async Task Execute_EmptyOldName_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("", "New");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_EmptyNewName_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("Old", "");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_DuplicateNewName_ReturnsError()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");

        var result = await sut.ExecuteAsync("First", "Second");

        Assert.False(result.IsSuccess);
        Assert.Contains("already exists", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_NonexistentTask_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("Missing", "New Name");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }
}

public sealed class DeleteTaskUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();

    private async Task<(DeleteTaskUseCase UseCase, TaskTrackingService Service)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new TaskTrackingService(_taskRepo, _sessionRepo);
        await service.InitializeAsync();
        return (new DeleteTaskUseCase(service), service);
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
    public async Task Execute_NonexistentTask_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("Missing");

        Assert.False(result.IsSuccess);
        Assert.Contains("No task named", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_WithoutConfirmation_RequiresConfirmation()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("To Delete");
        service.SwitchTask("Other");

        var result = await sut.ExecuteAsync("To Delete");

        Assert.True(result.RequiresConfirmation);
        Assert.Equal("To Delete", result.TaskName);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Execute_WithConfirmation_DeletesTask()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("To Delete");
        service.SwitchTask("Other");

        var result = await sut.ExecuteAsync("To Delete", confirmed: true);

        Assert.True(result.IsSuccess);
        Assert.Equal("To Delete", result.TaskName);
        Assert.False(service.HasTaskWithName("To Delete"));
    }

    [Fact]
    public async Task Execute_Confirmed_SavesState()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("To Delete");
        service.SwitchTask("Other");

        await sut.ExecuteAsync("To Delete", confirmed: true);

        await _taskRepo.Received().SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>());
    }
}

public sealed class MergeTasksUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();

    private async Task<(MergeTasksUseCase UseCase, TaskTrackingService Service)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new TaskTrackingService(_taskRepo, _sessionRepo);
        await service.InitializeAsync();
        return (new MergeTasksUseCase(service), service);
    }

    [Fact]
    public async Task Execute_MergesSourceIntoTarget()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Source");
        service.SwitchTask("Target");

        var result = await sut.ExecuteAsync("Source", "Target");

        Assert.True(result.IsSuccess);
        Assert.Equal("Source", result.SourceName);
        Assert.Equal("Target", result.TargetName);
    }

    [Fact]
    public async Task Execute_EmptySourceName_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("", "Target");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_EmptyTargetName_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync("Source", "");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_SameSourceAndTarget_ReturnsError()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Same");

        var result = await sut.ExecuteAsync("Same", "Same");

        Assert.False(result.IsSuccess);
        Assert.Contains("itself", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_NonexistentSource_ReturnsError()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Target");

        var result = await sut.ExecuteAsync("Missing", "Target");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_SavesStateAfterMerge()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Source");
        service.SwitchTask("Target");

        await sut.ExecuteAsync("Source", "Target");

        await _taskRepo.Received().SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>());
    }
}
