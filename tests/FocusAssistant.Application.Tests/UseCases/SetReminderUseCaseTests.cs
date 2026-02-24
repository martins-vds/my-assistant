using FocusAssistant.Application.Services;
using FocusAssistant.Application.UseCases;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;
using NSubstitute;

namespace FocusAssistant.Application.Tests.UseCases;

public sealed class SetReminderUseCaseTests
{
    private readonly ITaskRepository _taskRepo = Substitute.For<ITaskRepository>();
    private readonly ISessionRepository _sessionRepo = Substitute.For<ISessionRepository>();
    private readonly IUserPreferencesRepository _prefsRepo = Substitute.For<IUserPreferencesRepository>();

    private async Task<(SetReminderUseCase UseCase, TaskTrackingService Service)> CreateAsync()
    {
        _taskRepo.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<FocusTask>>(Array.Empty<FocusTask>()));
        _sessionRepo.GetLatestAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<WorkSession?>(null));
        _sessionRepo.SaveAsync(Arg.Any<WorkSession>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _taskRepo.SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(null));
        _prefsRepo.SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var service = new TaskTrackingService(_taskRepo, _sessionRepo, _prefsRepo);
        await service.InitializeAsync();
        return (new SetReminderUseCase(service, _prefsRepo), service);
    }

    [Fact]
    public async Task Execute_ZeroMinutes_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync(0);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_NegativeMinutes_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync(-5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Execute_GlobalDefault_SetsPreferences()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync(30);

        Assert.True(result.IsSuccess);
        Assert.Null(result.TaskName);
        Assert.Contains("30", result.Message);
        await _prefsRepo.Received(1).SaveAsync(Arg.Any<UserPreferences>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_GlobalDefault_UpdatesExistingPreferences()
    {
        var (sut, _) = await CreateAsync();

        // Re-configure mock after CreateAsync to return existing prefs
        var existingPrefs = new UserPreferences();
        _prefsRepo.GetAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<UserPreferences?>(existingPrefs));

        var result = await sut.ExecuteAsync(45);

        Assert.True(result.IsSuccess);
        await _prefsRepo.Received().SaveAsync(existingPrefs, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Execute_PerTask_SetsReminderOnTask()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("My Task");
        var result = await sut.ExecuteAsync(15, "My Task");

        Assert.True(result.IsSuccess);
        Assert.Equal("My Task", result.TaskName);
        Assert.Contains("15", result.Message);
    }

    [Fact]
    public async Task Execute_PerTask_NonexistentTask_ReturnsError()
    {
        var (sut, _) = await CreateAsync();

        var result = await sut.ExecuteAsync(15, "Missing");

        Assert.False(result.IsSuccess);
        Assert.Contains("No task named", result.ErrorMessage);
    }

    [Fact]
    public async Task Execute_PerTask_SavesState()
    {
        var (sut, service) = await CreateAsync();

        service.CreateTask("Reminder Task");
        await sut.ExecuteAsync(20, "Reminder Task");

        await _taskRepo.Received().SaveAllAsync(Arg.Any<IEnumerable<FocusTask>>(), Arg.Any<CancellationToken>());
    }
}
