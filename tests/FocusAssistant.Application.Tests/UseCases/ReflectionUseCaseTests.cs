using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class StartReflectionUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly INoteRepository _noteRepo = Substitute.For<INoteRepository>();

    private async Task<(StartReflectionUseCase UseCase, TaskTrackingService Tracking)> CreateAsync()
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
        var reflection = new ReflectionService(tracking, _noteRepo);
        var useCase = new StartReflectionUseCase(reflection, tracking);
        return (useCase, tracking);
    }

    [Fact]
    public async Task Execute_NoTasks_ReturnsSuccessWithAllCaughtUp()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Summary);
        Assert.Contains("caught up", result.Summary);
        Assert.False(result.HasOpenTasks);
    }

    [Fact]
    public async Task Execute_WithCompletedTasks_ListsThem()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("API refactor");
        tracking.CompleteTask();

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("API refactor", result.Summary!);
        Assert.Contains("Completed", result.Summary!);
    }

    [Fact]
    public async Task Execute_WithOpenTasks_ListsThemAndAsksPriorities()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Open task");

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("Open task", result.Summary!);
        Assert.Contains("priorities", result.Summary!, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.HasOpenTasks);
        Assert.Contains("Open task", result.OpenTaskNames);
    }

    [Fact]
    public async Task Execute_MixedTasks_ShowsBothSections()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Done");
        tracking.CompleteTask();
        tracking.CreateTask("Still going");

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("Completed", result.Summary!);
        Assert.Contains("Open", result.Summary!);
    }
}

public sealed class SetPrioritiesUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly IDailyPlanRepository _planRepo = Substitute.For<IDailyPlanRepository>();

    private async Task<(SetPrioritiesUseCase UseCase, TaskTrackingService Tracking)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _planRepo.GetByDateAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DailyPlan?>(null));
        _planRepo.SaveAsync(Arg.Any<DailyPlan>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();
        var useCase = new SetPrioritiesUseCase(tracking, _planRepo);
        return (useCase, tracking);
    }

    [Fact]
    public async Task Execute_EmptyList_ReturnsError()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync(Array.Empty<string>());

        Assert.False(result.IsSuccess);
        Assert.Contains("No tasks", result.ErrorMessage!);
    }

    [Fact]
    public async Task Execute_TaskNotFound_ReturnsError()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync(new[] { "nonexistent" });

        Assert.False(result.IsSuccess);
        Assert.Contains("not found", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_ValidTasks_SetsPrioritiesAndSaves()
    {
        var (useCase, tracking) = await CreateAsync();
        var taskA = tracking.CreateTask("Task A");
        tracking.SwitchTask("Task B");
        var taskB = tracking.FindTaskByName("Task B")!;

        var result = await useCase.ExecuteAsync(new[] { "Task B", "Task A" });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.OrderedTaskNames.Count);
        Assert.Equal("Task B", result.OrderedTaskNames[0]);
        Assert.Equal("Task A", result.OrderedTaskNames[1]);
        Assert.Equal(1, taskB.PriorityRanking);
        Assert.Equal(2, taskA.PriorityRanking);
        await _planRepo.Received(1).SaveAsync(Arg.Any<DailyPlan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_WithNote_IncludesNoteInPlan()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Task A");

        var result = await useCase.ExecuteAsync(new[] { "Task A" }, "Focus on tests first");

        Assert.True(result.IsSuccess);
        await _planRepo.Received(1).SaveAsync(
            Arg.Is<DailyPlan>(p => p.Notes.Count > 0 && p.Notes[0] == "Focus on tests first"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PlanDateIsTomorrow()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Task A");

        var result = await useCase.ExecuteAsync(new[] { "Task A" });

        var expected = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        Assert.Equal(expected, result.PlanDate);
    }
}
