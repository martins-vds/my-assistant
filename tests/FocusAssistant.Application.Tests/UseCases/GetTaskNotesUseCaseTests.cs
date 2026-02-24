using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class GetTaskNotesUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly INoteRepository _noteRepo = Substitute.For<INoteRepository>();

    private async Task<(GetTaskNotesUseCase UseCase, TaskTrackingService Tracking)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();
        var useCase = new GetTaskNotesUseCase(tracking, _noteRepo);
        return (useCase, tracking);
    }

    [Fact]
    public async Task Execute_ByTaskName_ReturnsNotesForThatTask()
    {
        var (useCase, tracking) = await CreateAsync();
        var task = tracking.CreateTask("API work");

        var expectedNotes = new List<TaskNote>
        {
            new("First note", task.Id),
            new("Second note", task.Id)
        };
        _noteRepo.GetByTaskIdAsync(task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(expectedNotes));

        var result = await useCase.ExecuteAsync("API work");

        Assert.True(result.IsSuccess);
        Assert.Equal("API work", result.TaskName);
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public async Task Execute_TaskNotFound_ReturnsError()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync("nonexistent");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_NoTaskName_WithCurrentTask_ReturnsCurrentTaskNotes()
    {
        var (useCase, tracking) = await CreateAsync();
        var task = tracking.CreateTask("Current work");

        var notes = new List<TaskNote> { new("My note", task.Id) };
        _noteRepo.GetByTaskIdAsync(task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(notes));

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Current work", result.TaskName);
        Assert.Single(result.Notes);
    }

    [Fact]
    public async Task Execute_NoTaskName_NoCurrentTask_ReturnsStandaloneNotes()
    {
        var (useCase, _) = await CreateAsync();

        var standalone = new List<TaskNote> { new("General thought") };
        _noteRepo.GetStandaloneNotesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(standalone));

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Null(result.TaskName);
        Assert.Single(result.Notes);
    }

    [Fact]
    public async Task Execute_TaskWithNoNotes_ReturnsEmptyList()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Empty task");

        _noteRepo.GetByTaskIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(Array.Empty<TaskNote>()));

        var result = await useCase.ExecuteAsync("Empty task");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Notes);
    }
}
