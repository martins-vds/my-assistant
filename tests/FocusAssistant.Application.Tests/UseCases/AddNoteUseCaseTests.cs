using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class AddNoteUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly INoteRepository _noteRepo = Substitute.For<INoteRepository>();

    private async Task<(AddNoteUseCase UseCase, TaskTrackingService Tracking)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _noteRepo.SaveAsync(Arg.Any<TaskNote>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();
        var useCase = new AddNoteUseCase(tracking, _noteRepo);
        return (useCase, tracking);
    }

    [Fact]
    public async Task Execute_EmptyContent_ReturnsError()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync("");

        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_WithCurrentTask_AttachesToCurrentTask()
    {
        var (useCase, tracking) = await CreateAsync();
        var task = tracking.CreateTask("API work");

        var result = await useCase.ExecuteAsync("Remember to update auth");

        Assert.True(result.IsSuccess);
        Assert.Equal("API work", result.TaskName);
        Assert.NotNull(result.NoteId);
        Assert.Single(task.NoteIds);
        await _noteRepo.Received(1).SaveAsync(Arg.Any<TaskNote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithSpecificTaskName_AttachesToNamedTask()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Task A");
        tracking.SwitchTask("Task B");

        var result = await useCase.ExecuteAsync("Note for A", "Task A");

        Assert.True(result.IsSuccess);
        Assert.Equal("Task A", result.TaskName);
    }

    [Fact]
    public async Task Execute_TaskNameNotFound_ReturnsError()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync("Some note", "nonexistent");

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_NoActiveTask_RequiresTaskSelection()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync("Orphan note");

        Assert.False(result.IsSuccess);
        Assert.True(result.RequiresTaskSelection);
        Assert.Equal("Orphan note", result.Content);
    }

    [Fact]
    public async Task Execute_NoActiveTask_StandaloneFlag_SavesStandalone()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync("General thought", storeAsStandalone: true);

        Assert.True(result.IsSuccess);
        Assert.Null(result.TaskName);
        Assert.NotNull(result.NoteId);
        await _noteRepo.Received(1).SaveAsync(
            Arg.Is<TaskNote>(n => n.IsStandalone),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_MultipleNotes_AllAttachedToTask()
    {
        var (useCase, tracking) = await CreateAsync();
        var task = tracking.CreateTask("Task A");

        await useCase.ExecuteAsync("First note");
        await useCase.ExecuteAsync("Second note");

        Assert.Equal(2, task.NoteIds.Count);
        await _noteRepo.Received(2).SaveAsync(Arg.Any<TaskNote>(), Arg.Any<CancellationToken>());
    }
}
