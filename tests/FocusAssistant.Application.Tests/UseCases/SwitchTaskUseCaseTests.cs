using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class SwitchTaskUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly INoteRepository _noteRepo = Substitute.For<INoteRepository>();

    private async Task<(SwitchTaskUseCase UseCase, TaskTrackingService Service)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _noteRepo.GetByTaskIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(Array.Empty<TaskNote>()));

        var service = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await service.InitializeAsync();
        return (new SwitchTaskUseCase(service, _noteRepo), service);
    }

    [Fact]
    public async Task Execute_SwitchesToExistingTask()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        service.SwitchTask("Second");

        var result = await sut.ExecuteAsync("First");

        Assert.True(result.IsSuccess);
        Assert.Equal("First", result.TaskName);
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
    public async Task Execute_ReportsPreviousTaskName()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("First");
        // Now switch away from "First"
        var result = await sut.ExecuteAsync("Second");

        Assert.True(result.IsSuccess);
        Assert.Equal("First", result.PreviousTaskName);
    }

    [Fact]
    public async Task Execute_CreatesNewTaskIfNotFound()
    {
        var (sut, service) = await CreateAsync();

        var result = await sut.ExecuteAsync("Brand New");

        Assert.True(result.IsSuccess);
        Assert.Equal("Brand New", result.TaskName);
        Assert.True(result.WasCreated);
    }

    [Fact]
    public async Task Execute_ReadsBackLastNote_WhenTaskHasNotes()
    {
        var (sut, service) = await CreateAsync();

        // Create task and add note
        var task = service.CreateTask("With Notes");
        var note = new TaskNote("Remember this");
        note.AttachToTask(task.Id);
        task.AddNoteId(note.Id);
        await service.SaveAsync();

        // Switch away and back
        service.SwitchTask("Other");

        _noteRepo.GetByTaskIdAsync(task.Id, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(new[] { note }));

        var result = await sut.ExecuteAsync("With Notes");

        Assert.True(result.IsSuccess);
        Assert.Equal("Remember this", result.LastNote);
    }

    [Fact]
    public async Task Execute_NoNote_WhenTaskHasNoNotes()
    {
        var (sut, service) = await CreateAsync();

        var result = await sut.ExecuteAsync("Fresh Task");

        Assert.True(result.IsSuccess);
        Assert.Null(result.LastNote);
    }
}
