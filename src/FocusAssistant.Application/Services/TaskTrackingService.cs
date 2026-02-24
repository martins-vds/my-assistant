using FocusAssistant.Domain.Aggregates;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Application.Services;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

/// <summary>
/// Central service for managing the task lifecycle. Orchestrates the TaskAggregate
/// and coordinates persistence via repositories.
/// </summary>
public sealed class TaskTrackingService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserPreferencesRepository _preferencesRepository;
    private readonly TaskAggregate _aggregate;
    private WorkSession? _currentSession;

    /// <summary>
    /// True if the current session was started today (a new session was created at initialization).
    /// Used to trigger the morning briefing.
    /// </summary>
    public bool IsNewSession { get; private set; }

    /// <summary>
    /// True if this is the first time the application has been launched (no preferences file exists).
    /// Used to trigger the onboarding flow.
    /// </summary>
    public bool NeedsOnboarding { get; private set; }

    public TaskTrackingService(
        ITaskRepository taskRepository,
        ISessionRepository sessionRepository,
        IUserPreferencesRepository preferencesRepository)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
        _aggregate = new TaskAggregate();
    }

    /// <summary>
    /// Load existing tasks from persistence and start (or resume) a work session.
    /// Also checks whether onboarding is needed (no preferences file).
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var tasks = await _taskRepository.GetAllAsync(ct);
        _aggregate.LoadTasks(tasks);

        // First-use detection: check if preferences file exists
        NeedsOnboarding = !await _preferencesRepository.ExistsAsync(ct);

        _currentSession = await _sessionRepository.GetLatestAsync(ct);
        if (_currentSession is null || !_currentSession.IsActive)
        {
            _currentSession = new WorkSession();
            IsNewSession = true;
            await _sessionRepository.SaveAsync(_currentSession, ct);
        }
        else
        {
            // Check if the session is from a different day
            IsNewSession = _currentSession.StartTime.Date < DateTime.UtcNow.Date;
        }
    }

    public FocusTask CreateTask(string name)
    {
        var task = _aggregate.CreateTask(name);
        RecordCurrentTaskInSession(task);
        return task;
    }

    public FocusTask SwitchTask(string name)
    {
        var task = _aggregate.SwitchToTask(name);
        RecordCurrentTaskInSession(task);
        return task;
    }

    public FocusTask CompleteTask(string? name = null)
    {
        return name is not null
            ? _aggregate.CompleteTask(name)
            : _aggregate.CompleteCurrentTask();
    }

    public FocusTask? GetCurrentTask() => _aggregate.CurrentTask;

    public IReadOnlyList<FocusTask> GetOpenTasks() => _aggregate.GetOpenTasks();

    public IReadOnlyList<FocusTask> GetCompletedTasks() => _aggregate.GetCompletedTasks();

    public IReadOnlyList<FocusTask> GetPausedTasks() => _aggregate.GetPausedTasks();

    public FocusTask RenameTask(string oldName, string newName) => _aggregate.RenameTask(oldName, newName);

    public void DeleteTask(string name) => _aggregate.DeleteTask(name);

    public FocusTask MergeTasks(string sourceName, string targetName) => _aggregate.MergeTasks(sourceName, targetName);

    public FocusTask? FindTaskByName(string name) => _aggregate.FindTaskByName(name);

    public bool HasTaskWithName(string name) => _aggregate.HasTaskWithName(name);

    /// <summary>
    /// Persist current aggregate state to disk.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        await _taskRepository.SaveAllAsync(_aggregate.Tasks, ct);

        if (_currentSession is not null)
            await _sessionRepository.SaveAsync(_currentSession, ct);

        _aggregate.ClearEvents();
    }

    private void RecordCurrentTaskInSession(FocusTask task)
    {
        _currentSession?.RecordTaskWorkedOn(task.Id);
    }
}
