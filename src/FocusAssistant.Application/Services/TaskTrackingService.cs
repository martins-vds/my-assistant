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
    private readonly TaskAggregate _aggregate;
    private WorkSession? _currentSession;

    public TaskTrackingService(ITaskRepository taskRepository, ISessionRepository sessionRepository)
    {
        _taskRepository = taskRepository ?? throw new ArgumentNullException(nameof(taskRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _aggregate = new TaskAggregate();
    }

    /// <summary>
    /// Load existing tasks from persistence and start (or resume) a work session.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var tasks = await _taskRepository.GetAllAsync(ct);
        _aggregate.LoadTasks(tasks);

        _currentSession = await _sessionRepository.GetLatestAsync(ct);
        if (_currentSession is null || !_currentSession.IsActive)
        {
            _currentSession = new WorkSession();
            await _sessionRepository.SaveAsync(_currentSession, ct);
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
