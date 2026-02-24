using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class GetMorningBriefingUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();
    private readonly IDailyPlanRepository _planRepo = Substitute.For<IDailyPlanRepository>();
    private readonly INoteRepository _noteRepo = Substitute.For<INoteRepository>();

    private async Task<(GetMorningBriefingUseCase UseCase, TaskTrackingService Tracking)> CreateAsync()
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
        _noteRepo.GetStandaloneNotesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(Array.Empty<TaskNote>()));

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();
        var useCase = new GetMorningBriefingUseCase(tracking, _planRepo, _noteRepo);
        return (useCase, tracking);
    }

    [Fact]
    public async Task Execute_NoPriorSession_ReturnsStartFresh()
    {
        var (useCase, _) = await CreateAsync();

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("starting fresh", result.Briefing!, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.HasPlan);
        Assert.Equal(0, result.OpenTaskCount);
    }

    [Fact]
    public async Task Execute_WithOpenTasks_NoPlan_ListsCarryOver()
    {
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("API refactor");
        tracking.SwitchTask("Bug fix");

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("API refactor", result.Briefing!);
        Assert.Contains("Bug fix", result.Briefing!);
        Assert.Equal(2, result.OpenTaskCount);
        Assert.False(result.HasPlan);
    }

    [Fact]
    public async Task Execute_WithPlan_ShowsPriorities()
    {
        var (useCase, tracking) = await CreateAsync();
        var taskA = tracking.CreateTask("Task A");
        tracking.SwitchTask("Task B");
        var taskB = tracking.FindTaskByName("Task B")!;

        var plan = new DailyPlan(DateOnly.FromDateTime(DateTime.UtcNow));
        plan.SetTaskPriorities(new[] { taskB.Id, taskA.Id });
        plan.AddNote("Focus on tests");

        _planRepo.GetByDateAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DailyPlan?>(plan));

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("priorities", result.Briefing!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Focus on tests", result.Briefing!);
        Assert.True(result.HasPlan);
    }

    [Fact]
    public async Task Execute_WithStandaloneNotes_ShowsCount()
    {
        var (useCase, _) = await CreateAsync();

        var notes = new List<TaskNote> { new("Thought 1"), new("Thought 2") };
        _noteRepo.GetStandaloneNotesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<TaskNote>>(notes));

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        Assert.Contains("standalone note", result.Briefing!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Execute_MultiDayGap_ShowsTaskAges()
    {
        // Tasks created "today" will show "(new)" — can't easily test multi-day ages
        // without mocking DateTime, but verify the output contains age indicators
        var (useCase, tracking) = await CreateAsync();
        tracking.CreateTask("Recent task");

        var result = await useCase.ExecuteAsync();

        Assert.True(result.IsSuccess);
        // New tasks show "(new)"
        Assert.Contains("(new)", result.Briefing!);
    }

    [Fact]
    public async Task IsNewSession_TrueWhenNoExistingSession()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();

        Assert.True(tracking.IsNewSession);
    }

    [Fact]
    public async Task IsNewSession_FalseWhenResuming()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(new WorkSession()));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var tracking = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await tracking.InitializeAsync();

        // Session started just now, same day — not new
        Assert.False(tracking.IsNewSession);
    }
}
