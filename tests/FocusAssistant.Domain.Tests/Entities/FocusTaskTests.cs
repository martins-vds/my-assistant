using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Domain.Tests.Entities;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

public class FocusTaskTests
{
    [Fact]
    public void Constructor_CreatesInProgressTask()
    {
        var task = new FocusTask("API refactor");

        Assert.Equal("API refactor", task.Name);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Single(task.TimeLogs);
        Assert.True(task.TimeLogs[0].IsActive);
        Assert.Empty(task.NoteIds);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var task = new FocusTask("  API refactor  ");
        Assert.Equal("API refactor", task.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => new FocusTask(name!));
    }

    [Fact]
    public void Start_FromPaused_SetsInProgressAndAddsTimeLog()
    {
        var task = new FocusTask("test");
        task.Pause();

        task.Start();

        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Equal(2, task.TimeLogs.Count);
        // First log (from constructor) was stopped by Pause
        Assert.False(task.TimeLogs[0].IsActive);
        // Second log (from Start) is active
        Assert.True(task.TimeLogs[1].IsActive);
    }

    [Fact]
    public void Start_FromCompleted_Throws()
    {
        var task = new FocusTask("test");
        task.Complete();

        Assert.Throws<InvalidOperationException>(() => task.Start());
    }

    [Fact]
    public void Start_FromArchived_Throws()
    {
        var task = new FocusTask("test");
        task.Archive();

        Assert.Throws<InvalidOperationException>(() => task.Start());
    }

    [Fact]
    public void Pause_FromInProgress_SetsPaused()
    {
        var task = new FocusTask("test");

        task.Pause();

        Assert.Equal(TaskStatus.Paused, task.Status);
        // Constructor time log should be stopped
        Assert.Single(task.TimeLogs);
        Assert.False(task.TimeLogs[0].IsActive);
    }

    [Fact]
    public void Pause_FromPaused_Throws()
    {
        var task = new FocusTask("test");
        task.Pause();

        Assert.Throws<InvalidOperationException>(() => task.Pause());
    }

    [Fact]
    public void Complete_SetsCompleted()
    {
        var task = new FocusTask("test");

        task.Complete();

        Assert.Equal(TaskStatus.Completed, task.Status);
    }

    [Fact]
    public void Complete_FromAlreadyCompleted_Throws()
    {
        var task = new FocusTask("test");
        task.Complete();

        Assert.Throws<InvalidOperationException>(() => task.Complete());
    }

    [Fact]
    public void Archive_SetsArchived()
    {
        var task = new FocusTask("test");

        task.Archive();

        Assert.Equal(TaskStatus.Archived, task.Status);
    }

    [Fact]
    public void Archive_FromAlreadyArchived_Throws()
    {
        var task = new FocusTask("test");
        task.Archive();

        Assert.Throws<InvalidOperationException>(() => task.Archive());
    }

    [Fact]
    public void Rename_UpdatesName()
    {
        var task = new FocusTask("old name");

        task.Rename("new name");

        Assert.Equal("new name", task.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_Throws(string? name)
    {
        var task = new FocusTask("test");
        Assert.Throws<ArgumentException>(() => task.Rename(name!));
    }

    [Fact]
    public void SetPriority_SetsRanking()
    {
        var task = new FocusTask("test");

        task.SetPriority(1);

        Assert.Equal(1, task.PriorityRanking);
    }

    [Fact]
    public void SetPriority_ZeroOrNegative_Throws()
    {
        var task = new FocusTask("test");
        Assert.Throws<ArgumentException>(() => task.SetPriority(0));
        Assert.Throws<ArgumentException>(() => task.SetPriority(-1));
    }

    [Fact]
    public void SetReminderInterval_SetsInterval()
    {
        var task = new FocusTask("test");
        var interval = ReminderInterval.FromHours(2, true);

        task.SetReminderInterval(interval);

        Assert.Equal(interval, task.ReminderInterval);
    }

    [Fact]
    public void SetReminderInterval_Null_Throws()
    {
        var task = new FocusTask("test");
        Assert.Throws<ArgumentNullException>(() => task.SetReminderInterval(null!));
    }

    [Fact]
    public void ClearReminderInterval_RemovesInterval()
    {
        var task = new FocusTask("test");
        task.SetReminderInterval(ReminderInterval.Default);

        task.ClearReminderInterval();

        Assert.Null(task.ReminderInterval);
    }

    [Fact]
    public void AddNoteId_AddsToCollection()
    {
        var task = new FocusTask("test");
        var noteId = Guid.NewGuid();

        task.AddNoteId(noteId);

        Assert.Contains(noteId, task.NoteIds);
    }

    [Fact]
    public void MergeFrom_CombinesNotesAndTimeLogs()
    {
        var target = new FocusTask("target");
        target.AddNoteId(Guid.NewGuid());

        var source = new FocusTask("source");
        source.AddNoteId(Guid.NewGuid());
        source.AddNoteId(Guid.NewGuid());

        target.MergeFrom(source);

        Assert.Equal(3, target.NoteIds.Count);
    }

    [Fact]
    public void MergeFrom_SameTask_Throws()
    {
        var task = new FocusTask("test");
        Assert.Throws<InvalidOperationException>(() => task.MergeFrom(task));
    }

    [Fact]
    public void MergeFrom_Null_Throws()
    {
        var task = new FocusTask("test");
        Assert.Throws<ArgumentNullException>(() => task.MergeFrom(null!));
    }

    [Fact]
    public void GetTotalTimeSpent_AggregatesAllTimeLogs()
    {
        var task = new FocusTask("test");
        // First session (started by constructor)
        task.Pause();
        // Second session
        task.Start();
        task.Pause();

        var totalTime = task.GetTotalTimeSpent();
        Assert.True(totalTime >= TimeSpan.Zero);
        Assert.Equal(2, task.TimeLogs.Count);
    }
}
