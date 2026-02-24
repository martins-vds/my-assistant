using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Tests.Entities;

public class TaskNoteTests
{
    [Fact]
    public void Constructor_CreatesStandaloneNote()
    {
        var note = new TaskNote("Remember to check staging");

        Assert.Equal("Remember to check staging", note.Content);
        Assert.Null(note.TaskId);
        Assert.True(note.IsStandalone);
        Assert.NotEqual(Guid.Empty, note.Id);
    }

    [Fact]
    public void Constructor_WithTaskId_AttachesToTask()
    {
        var taskId = Guid.NewGuid();
        var note = new TaskNote("Update the schema", taskId);

        Assert.Equal(taskId, note.TaskId);
        Assert.False(note.IsStandalone);
    }

    [Fact]
    public void Constructor_TrimsContent()
    {
        var note = new TaskNote("  trimmed  ");
        Assert.Equal("trimmed", note.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyContent_Throws(string? content)
    {
        Assert.Throws<ArgumentException>(() => new TaskNote(content!));
    }

    [Fact]
    public void AttachToTask_SetsTaskId()
    {
        var note = new TaskNote("standalone note");
        var taskId = Guid.NewGuid();

        note.AttachToTask(taskId);

        Assert.Equal(taskId, note.TaskId);
        Assert.False(note.IsStandalone);
    }
}
