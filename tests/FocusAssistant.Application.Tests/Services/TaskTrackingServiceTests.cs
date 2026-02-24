using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.Services;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

public sealed class TaskTrackingServiceTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private TaskTrackingService CreateService()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        return new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
    }

    [Fact]
    public async Task InitializeAsync_LoadsExistingTasks()
    {
        var existing = new FocusTask("Existing Task");
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(new[] { existing }));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await sut.InitializeAsync();

        Assert.True(sut.HasTaskWithName("Existing Task"));
    }

    [Fact]
    public async Task InitializeAsync_ResumesActiveSession()
    {
        var session = new WorkSession();
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(session));

        var sut = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await sut.InitializeAsync();

        // Should not create a new session
        await _sessionRepo.Received(0).SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_CreatesNewSessionWhenNoActiveSession()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await sut.InitializeAsync();

        await _sessionRepo.Received(1).SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTask_ReturnsNewTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        var task = sut.CreateTask("New Task");

        Assert.NotNull(task);
        Assert.Equal("New Task", task.Name);
        Assert.Equal(TaskStatus.InProgress, task.Status);
    }

    [Fact]
    public async Task CreateTask_SetsAsCurrentTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        var task = sut.CreateTask("New Task");

        Assert.Equal(task, sut.GetCurrentTask());
    }

    [Fact]
    public async Task SwitchTask_PausesPreviousTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        var first = sut.CreateTask("First");
        sut.SwitchTask("Second");

        Assert.Equal(TaskStatus.Paused, first.Status);
    }

    [Fact]
    public async Task CompleteTask_CompletesCurrentTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        var task = sut.CreateTask("Task");
        var completed = sut.CompleteTask();

        Assert.Equal(TaskStatus.Completed, completed.Status);
        Assert.Null(sut.GetCurrentTask());
    }

    [Fact]
    public async Task CompleteTask_ByName_CompletesSpecificTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("First");
        sut.SwitchTask("Second");

        var completed = sut.CompleteTask("First");

        Assert.Equal("First", completed.Name);
        Assert.Equal(TaskStatus.Completed, completed.Status);
    }

    [Fact]
    public async Task GetOpenTasks_ReturnsInProgressAndPaused()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("First");
        sut.SwitchTask("Second");
        sut.CreateTask("Third");

        var open = sut.GetOpenTasks();

        Assert.Equal(3, open.Count);
    }

    [Fact]
    public async Task GetPausedTasks_ReturnsOnlyPaused()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("First");
        sut.SwitchTask("Second");

        var paused = sut.GetPausedTasks();

        Assert.Single(paused);
        Assert.Equal("First", paused[0].Name);
    }

    [Fact]
    public async Task RenameTask_ChangesTaskName()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("Old Name");
        var renamed = sut.RenameTask("Old Name", "New Name");

        Assert.Equal("New Name", renamed.Name);
        Assert.False(sut.HasTaskWithName("Old Name"));
        Assert.True(sut.HasTaskWithName("New Name"));
    }

    [Fact]
    public async Task DeleteTask_RemovesTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("Doomed");
        sut.SwitchTask("Other");
        sut.DeleteTask("Doomed");

        Assert.False(sut.HasTaskWithName("Doomed"));
    }

    [Fact]
    public async Task MergeTasks_CombinesIntoTarget()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("Source");
        sut.SwitchTask("Target");

        var merged = sut.MergeTasks("Source", "Target");

        Assert.Equal("Target", merged.Name);
        Assert.False(sut.HasTaskWithName("Source"));
    }

    [Fact]
    public async Task SaveAsync_PersistsToRepositories()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("Task");
        await sut.SaveAsync();

        await _taskRepo.Received(1).SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>());
        await _sessionRepo.Received().SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FindTaskByName_ReturnsMatchingTask()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        sut.CreateTask("Find Me");

        var found = sut.FindTaskByName("Find Me");

        Assert.NotNull(found);
        Assert.Equal("Find Me", found.Name);
    }

    [Fact]
    public async Task FindTaskByName_ReturnsNullForMissing()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        var found = sut.FindTaskByName("Nonexistent");

        Assert.Null(found);
    }

    [Fact]
    public async Task GetCompletedTasks_ReturnsCompletedOnly()
    {
        var sut = CreateService();
        await sut.InitializeAsync();

        var task = sut.CreateTask("Done");
        sut.CompleteTask();

        var completed = sut.GetCompletedTasks();

        Assert.Single(completed);
        Assert.Equal("Done", completed[0].Name);
    }
}
