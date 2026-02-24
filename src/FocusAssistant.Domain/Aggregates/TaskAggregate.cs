using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Events;

namespace FocusAssistant.Domain.Aggregates;

using TaskStatus = ValueObjects.TaskStatus;

/// <summary>
/// Aggregate root managing FocusTask lifecycle and enforcing invariants.
/// Key invariant: At most one task can be InProgress at any time.
/// </summary>
public sealed class TaskAggregate
{
    private readonly List<FocusTask> _tasks = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public IReadOnlyList<FocusTask> Tasks => _tasks.AsReadOnly();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public FocusTask? CurrentTask => _tasks.FirstOrDefault(t => t.Status == TaskStatus.InProgress);

    public void LoadTasks(IEnumerable<FocusTask> tasks)
    {
        _tasks.Clear();
        _tasks.AddRange(tasks);
    }

    /// <summary>
    /// Creates a new task and starts it. Auto-pauses any currently in-progress task.
    /// </summary>
    public FocusTask CreateTask(string name)
    {
        var current = CurrentTask;
        if (current != null)
        {
            current.Pause();
            _domainEvents.Add(new TaskSwitchedEvent(current.Id, current.Name));
        }

        var task = new FocusTask(name);
        _tasks.Add(task);
        _domainEvents.Add(new TaskCreatedEvent(task.Id, task.Name));
        return task;
    }

    /// <summary>
    /// Switches to an existing task by name. Pauses the current task and resumes the target.
    /// If the target doesn't exist, creates a new one.
    /// </summary>
    public FocusTask SwitchToTask(string taskName)
    {
        var current = CurrentTask;
        var target = _tasks.FirstOrDefault(t =>
            t.Name.Equals(taskName, StringComparison.OrdinalIgnoreCase) &&
            t.Status != TaskStatus.Completed &&
            t.Status != TaskStatus.Archived);

        if (current != null && target != null && current.Id == target.Id)
            return current; // Already working on this task

        if (current != null)
        {
            current.Pause();
            _domainEvents.Add(new TaskSwitchedEvent(current.Id, current.Name));
        }

        if (target != null)
        {
            target.Start();
            return target;
        }

        // Create new task if not found
        return CreateTask(taskName);
    }

    public FocusTask CompleteTask(string taskName)
    {
        var task = FindTaskByName(taskName)
            ?? throw new InvalidOperationException($"Task '{taskName}' not found.");

        task.Complete();
        _domainEvents.Add(new TaskCompletedEvent(task.Id, task.Name));
        return task;
    }

    public FocusTask CompleteCurrentTask()
    {
        var current = CurrentTask
            ?? throw new InvalidOperationException("No task is currently in progress.");

        current.Complete();
        _domainEvents.Add(new TaskCompletedEvent(current.Id, current.Name));
        return current;
    }

    public FocusTask RenameTask(string currentName, string newName)
    {
        var task = FindTaskByName(currentName)
            ?? throw new InvalidOperationException($"Task '{currentName}' not found.");

        task.Rename(newName);
        return task;
    }

    public FocusTask DeleteTask(string taskName)
    {
        var task = FindTaskByName(taskName)
            ?? throw new InvalidOperationException($"Task '{taskName}' not found.");

        _tasks.Remove(task);
        return task;
    }

    public FocusTask MergeTasks(string sourceName, string targetName)
    {
        var source = FindTaskByName(sourceName)
            ?? throw new InvalidOperationException($"Source task '{sourceName}' not found.");
        var target = FindTaskByName(targetName)
            ?? throw new InvalidOperationException($"Target task '{targetName}' not found.");

        target.MergeFrom(source);
        _tasks.Remove(source);
        return target;
    }

    public IReadOnlyList<FocusTask> GetOpenTasks()
    {
        return _tasks
            .Where(t => t.Status == TaskStatus.InProgress || t.Status == TaskStatus.Paused)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<FocusTask> GetPausedTasks()
    {
        return _tasks
            .Where(t => t.Status == TaskStatus.Paused)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<FocusTask> GetCompletedTasks()
    {
        return _tasks
            .Where(t => t.Status == TaskStatus.Completed)
            .ToList()
            .AsReadOnly();
    }

    public FocusTask? FindTaskByName(string name)
    {
        return _tasks.FirstOrDefault(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            t.Status != TaskStatus.Archived);
    }

    public FocusTask? FindTaskById(Guid id)
    {
        return _tasks.FirstOrDefault(t => t.Id == id);
    }

    public bool HasTaskWithName(string name)
    {
        return _tasks.Any(t =>
            t.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            t.Status != TaskStatus.Archived);
    }

    public void ClearEvents()
    {
        _domainEvents.Clear();
    }
}
