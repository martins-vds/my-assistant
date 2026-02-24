using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Tests.Entities;

public class WorkSessionTests
{
    [Fact]
    public void Constructor_CreatesActiveSession()
    {
        var session = new WorkSession();

        Assert.True(session.IsActive);
        Assert.Null(session.EndTime);
        Assert.Empty(session.TaskIdsWorkedOn);
        Assert.Null(session.ReflectionSummary);
        Assert.NotEqual(Guid.Empty, session.Id);
    }

    [Fact]
    public void Constructor_WithStartTime_SetsStartTime()
    {
        var start = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var session = new WorkSession(start);

        Assert.Equal(start, session.StartTime);
    }

    [Fact]
    public void RecordTaskWorkedOn_AddsTaskId()
    {
        var session = new WorkSession();
        var taskId = Guid.NewGuid();

        session.RecordTaskWorkedOn(taskId);

        Assert.Contains(taskId, session.TaskIdsWorkedOn);
    }

    [Fact]
    public void RecordTaskWorkedOn_DuplicateId_DoesNotAddTwice()
    {
        var session = new WorkSession();
        var taskId = Guid.NewGuid();

        session.RecordTaskWorkedOn(taskId);
        session.RecordTaskWorkedOn(taskId);

        Assert.Single(session.TaskIdsWorkedOn);
    }

    [Fact]
    public void End_SetsEndTimeAndReflection()
    {
        var session = new WorkSession();

        session.End("Good day");

        Assert.False(session.IsActive);
        Assert.NotNull(session.EndTime);
        Assert.Equal("Good day", session.ReflectionSummary);
    }

    [Fact]
    public void End_WithoutReflection_SetsEndTime()
    {
        var session = new WorkSession();

        session.End();

        Assert.False(session.IsActive);
        Assert.NotNull(session.EndTime);
        Assert.Null(session.ReflectionSummary);
    }

    [Fact]
    public void End_AlreadyEnded_Throws()
    {
        var session = new WorkSession();
        session.End();

        Assert.Throws<InvalidOperationException>(() => session.End());
    }

    [Fact]
    public void SetReflectionSummary_UpdatesSummary()
    {
        var session = new WorkSession();

        session.SetReflectionSummary("Completed 3 tasks");

        Assert.Equal("Completed 3 tasks", session.ReflectionSummary);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetReflectionSummary_EmptySummary_Throws(string? summary)
    {
        var session = new WorkSession();
        Assert.Throws<ArgumentException>(() => session.SetReflectionSummary(summary!));
    }
}
