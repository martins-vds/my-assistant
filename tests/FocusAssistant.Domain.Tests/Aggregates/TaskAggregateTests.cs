using FocusAssistant.Domain.Aggregates;
using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Events;

namespace FocusAssistant.Domain.Tests.Aggregates;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

public class TaskAggregateTests
{
    [Fact]
    public void CreateTask_CreatesAndStartsTask()
    {
        var aggregate = new TaskAggregate();

        var task = aggregate.CreateTask("API refactor");

        Assert.Equal("API refactor", task.Name);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Single(aggregate.Tasks);
        Assert.Equal(task, aggregate.CurrentTask);
    }

    [Fact]
    public void CreateTask_AutoPausesCurrentTask()
    {
        var aggregate = new TaskAggregate();
        var first = aggregate.CreateTask("First task");

        var second = aggregate.CreateTask("Second task");

        Assert.Equal(TaskStatus.Paused, first.Status);
        Assert.Equal(TaskStatus.InProgress, second.Status);
        Assert.Equal(second, aggregate.CurrentTask);
    }

    [Fact]
    public void CreateTask_EmitsCreatedAndSwitchedEvents()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("First task");
        aggregate.ClearEvents();

        aggregate.CreateTask("Second task");

        Assert.Equal(2, aggregate.DomainEvents.Count);
        Assert.IsType<TaskSwitchedEvent>(aggregate.DomainEvents[0]);
        Assert.IsType<TaskCreatedEvent>(aggregate.DomainEvents[1]);
    }

    [Fact]
    public void SwitchToTask_ExistingPausedTask_ResumesIt()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("First");
        aggregate.CreateTask("Second"); // First is now paused

        var result = aggregate.SwitchToTask("First");

        Assert.Equal(TaskStatus.InProgress, result.Status);
        Assert.Equal("First", result.Name);
        Assert.Equal(result, aggregate.CurrentTask);
    }

    [Fact]
    public void SwitchToTask_SameTask_ReturnsCurrentWithoutChange()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("Current");
        aggregate.ClearEvents();

        var result = aggregate.SwitchToTask("Current");

        Assert.Same(task, result);
        Assert.Empty(aggregate.DomainEvents); // No events emitted
    }

    [Fact]
    public void SwitchToTask_NonExistingTask_CreatesNew()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Existing");

        var result = aggregate.SwitchToTask("Brand New");

        Assert.Equal("Brand New", result.Name);
        Assert.Equal(TaskStatus.InProgress, result.Status);
        Assert.Equal(2, aggregate.Tasks.Count);
    }

    [Fact]
    public void SwitchToTask_CaseInsensitive()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("API Refactor");
        aggregate.CreateTask("Other"); // Pauses API Refactor

        var result = aggregate.SwitchToTask("api refactor");

        Assert.Same(task, result);
        Assert.Equal(TaskStatus.InProgress, result.Status);
    }

    [Fact]
    public void SwitchToTask_DoesNotSwitchToCompletedTask()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Done");
        aggregate.CompleteCurrentTask();

        var result = aggregate.SwitchToTask("Done");

        // Should create a new task since the existing one is completed
        Assert.NotEqual(aggregate.Tasks.First(t => t.Status == TaskStatus.Completed).Id, result.Id);
    }

    [Fact]
    public void CompleteTask_ByName_CompletesAndEmitsEvent()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Test task");
        aggregate.ClearEvents();

        var result = aggregate.CompleteTask("Test task");

        Assert.Equal(TaskStatus.Completed, result.Status);
        Assert.Single(aggregate.DomainEvents);
        Assert.IsType<TaskCompletedEvent>(aggregate.DomainEvents[0]);
    }

    [Fact]
    public void CompleteTask_NotFound_Throws()
    {
        var aggregate = new TaskAggregate();
        Assert.Throws<InvalidOperationException>(() => aggregate.CompleteTask("nonexistent"));
    }

    [Fact]
    public void CompleteCurrentTask_CompletesInProgressTask()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("Current");

        var result = aggregate.CompleteCurrentTask();

        Assert.Same(task, result);
        Assert.Equal(TaskStatus.Completed, result.Status);
    }

    [Fact]
    public void CompleteCurrentTask_NoCurrentTask_Throws()
    {
        var aggregate = new TaskAggregate();
        Assert.Throws<InvalidOperationException>(() => aggregate.CompleteCurrentTask());
    }

    [Fact]
    public void RenameTask_RenamesExistingTask()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Old Name");

        var result = aggregate.RenameTask("Old Name", "New Name");

        Assert.Equal("New Name", result.Name);
    }

    [Fact]
    public void RenameTask_NotFound_Throws()
    {
        var aggregate = new TaskAggregate();
        Assert.Throws<InvalidOperationException>(() => aggregate.RenameTask("nonexistent", "new"));
    }

    [Fact]
    public void DeleteTask_RemovesTask()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("To Delete");
        aggregate.CreateTask("To Keep");

        aggregate.DeleteTask("To Delete");

        Assert.Single(aggregate.Tasks);
        Assert.Equal("To Keep", aggregate.Tasks[0].Name);
    }

    [Fact]
    public void DeleteTask_NotFound_Throws()
    {
        var aggregate = new TaskAggregate();
        Assert.Throws<InvalidOperationException>(() => aggregate.DeleteTask("nonexistent"));
    }

    [Fact]
    public void MergeTasks_CombinesSourceIntoTarget()
    {
        var aggregate = new TaskAggregate();
        var source = aggregate.CreateTask("Source");
        source.AddNoteId(Guid.NewGuid());
        var target = aggregate.CreateTask("Target");
        target.AddNoteId(Guid.NewGuid());

        var result = aggregate.MergeTasks("Source", "Target");

        Assert.Equal("Target", result.Name);
        Assert.Equal(2, result.NoteIds.Count);
        Assert.DoesNotContain(aggregate.Tasks, t => t.Name == "Source");
    }

    [Fact]
    public void MergeTasks_SourceNotFound_Throws()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Target");

        Assert.Throws<InvalidOperationException>(
            () => aggregate.MergeTasks("Nonexistent", "Target"));
    }

    [Fact]
    public void MergeTasks_TargetNotFound_Throws()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Source");

        Assert.Throws<InvalidOperationException>(
            () => aggregate.MergeTasks("Source", "Nonexistent"));
    }

    [Fact]
    public void GetOpenTasks_ReturnsInProgressAndPaused()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Paused");
        aggregate.CreateTask("InProgress");
        // First is now Paused, second is InProgress

        var open = aggregate.GetOpenTasks();

        Assert.Equal(2, open.Count);
    }

    [Fact]
    public void GetOpenTasks_ExcludesCompletedAndArchived()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("To Complete");
        aggregate.CompleteCurrentTask();
        aggregate.CreateTask("Active");

        var open = aggregate.GetOpenTasks();

        Assert.Single(open);
        Assert.Equal("Active", open[0].Name);
    }

    [Fact]
    public void GetPausedTasks_ReturnsOnlyPaused()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("A");
        aggregate.CreateTask("B"); // A is now paused

        var paused = aggregate.GetPausedTasks();

        Assert.Single(paused);
        Assert.Equal("A", paused[0].Name);
    }

    [Fact]
    public void GetCompletedTasks_ReturnsOnlyCompleted()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Done");
        aggregate.CompleteCurrentTask();
        aggregate.CreateTask("Active");

        var completed = aggregate.GetCompletedTasks();

        Assert.Single(completed);
        Assert.Equal("Done", completed[0].Name);
    }

    [Fact]
    public void FindTaskByName_CaseInsensitive()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("API Refactor");

        Assert.Same(task, aggregate.FindTaskByName("api refactor"));
        Assert.Same(task, aggregate.FindTaskByName("API REFACTOR"));
    }

    [Fact]
    public void FindTaskByName_ExcludesArchived()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("Old Task");
        task.Archive();

        Assert.Null(aggregate.FindTaskByName("Old Task"));
    }

    [Fact]
    public void FindTaskById_ReturnsCorrectTask()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("Test");

        Assert.Same(task, aggregate.FindTaskById(task.Id));
        Assert.Null(aggregate.FindTaskById(Guid.NewGuid()));
    }

    [Fact]
    public void HasTaskWithName_ReturnsTrueForExisting()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Test");

        Assert.True(aggregate.HasTaskWithName("Test"));
        Assert.True(aggregate.HasTaskWithName("test"));
        Assert.False(aggregate.HasTaskWithName("Nonexistent"));
    }

    [Fact]
    public void HasTaskWithName_ExcludesArchived()
    {
        var aggregate = new TaskAggregate();
        var task = aggregate.CreateTask("Archived");
        task.Archive();

        Assert.False(aggregate.HasTaskWithName("Archived"));
    }

    [Fact]
    public void LoadTasks_ReplacesExistingTasks()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Old");

        var newTasks = new[] { new FocusTask("New1"), new FocusTask("New2") };
        aggregate.LoadTasks(newTasks);

        Assert.Equal(2, aggregate.Tasks.Count);
        Assert.Contains(aggregate.Tasks, t => t.Name == "New1");
        Assert.Contains(aggregate.Tasks, t => t.Name == "New2");
    }

    [Fact]
    public void ClearEvents_RemovesAllEvents()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("Test");

        Assert.NotEmpty(aggregate.DomainEvents);

        aggregate.ClearEvents();

        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public void SingleInProgressInvariant_Maintained()
    {
        var aggregate = new TaskAggregate();
        aggregate.CreateTask("A");
        aggregate.CreateTask("B");
        aggregate.CreateTask("C");

        // Only one task should be InProgress
        var inProgressCount = aggregate.Tasks.Count(t => t.Status == TaskStatus.InProgress);
        Assert.Equal(1, inProgressCount);
        Assert.Equal("C", aggregate.CurrentTask!.Name);
    }
}
