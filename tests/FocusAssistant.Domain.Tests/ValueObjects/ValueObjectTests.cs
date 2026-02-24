using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Domain.Tests.ValueObjects;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

public class TaskStatusTests
{
    [Fact]
    public void TaskStatus_HasExpectedValues()
    {
        Assert.Equal(0, (int)TaskStatus.InProgress);
        Assert.Equal(1, (int)TaskStatus.Paused);
        Assert.Equal(2, (int)TaskStatus.Completed);
        Assert.Equal(3, (int)TaskStatus.Archived);
    }

    [Fact]
    public void TaskStatus_HasExactlyFourValues()
    {
        var values = Enum.GetValues<TaskStatus>();
        Assert.Equal(4, values.Length);
    }
}

public class TimeLogEntryTests
{
    [Fact]
    public void Constructor_SetsStartTime()
    {
        var start = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var entry = new TimeLogEntry(start);

        Assert.Equal(start, entry.StartTime);
        Assert.Null(entry.EndTime);
        Assert.True(entry.IsActive);
    }

    [Fact]
    public void Constructor_WithEndTime_SetsEndTime()
    {
        var start = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 10, 30, 0, DateTimeKind.Utc);
        var entry = new TimeLogEntry(start, end);

        Assert.Equal(end, entry.EndTime);
        Assert.False(entry.IsActive);
    }

    [Fact]
    public void Constructor_EndBeforeStart_Throws()
    {
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => new TimeLogEntry(start, end));
    }

    [Fact]
    public void Duration_WithEndTime_ReturnsCorrectDuration()
    {
        var start = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 10, 30, 0, DateTimeKind.Utc);
        var entry = new TimeLogEntry(start, end);

        Assert.Equal(TimeSpan.FromMinutes(90), entry.Duration);
    }

    [Fact]
    public void Duration_WithoutEndTime_ReturnsElapsedSinceStart()
    {
        var start = DateTime.UtcNow.AddMinutes(-30);
        var entry = new TimeLogEntry(start);

        // Duration should be approximately 30 minutes (±5 seconds tolerance)
        Assert.InRange(entry.Duration.TotalMinutes, 29.9, 30.2);
    }

    [Fact]
    public void Stop_ReturnsNewEntryWithEndTime()
    {
        var start = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var entry = new TimeLogEntry(start);
        var endTime = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var stopped = entry.Stop(endTime);

        Assert.Equal(endTime, stopped.EndTime);
        Assert.False(stopped.IsActive);
        Assert.True(entry.IsActive); // Original unchanged (record)
    }

    [Fact]
    public void Stop_EndBeforeStart_Throws()
    {
        var start = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var entry = new TimeLogEntry(start);
        var endTime = new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Utc);

        Assert.Throws<ArgumentException>(() => entry.Stop(endTime));
    }
}

public class ReminderIntervalTests
{
    [Fact]
    public void Default_IsOneHour()
    {
        Assert.Equal(TimeSpan.FromHours(1), ReminderInterval.Default.Duration);
        Assert.False(ReminderInterval.Default.IsPerTaskOverride);
    }

    [Fact]
    public void Constructor_SetsProperties()
    {
        var interval = new ReminderInterval(TimeSpan.FromMinutes(45), true);

        Assert.Equal(TimeSpan.FromMinutes(45), interval.Duration);
        Assert.True(interval.IsPerTaskOverride);
    }

    [Fact]
    public void Constructor_ZeroDuration_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReminderInterval(TimeSpan.Zero));
    }

    [Fact]
    public void Constructor_NegativeDuration_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ReminderInterval(TimeSpan.FromMinutes(-5)));
    }

    [Fact]
    public void FromHours_CreatesCorrectInterval()
    {
        var interval = ReminderInterval.FromHours(2, true);

        Assert.Equal(TimeSpan.FromHours(2), interval.Duration);
        Assert.True(interval.IsPerTaskOverride);
    }

    [Fact]
    public void FromMinutes_CreatesCorrectInterval()
    {
        var interval = ReminderInterval.FromMinutes(30);

        Assert.Equal(TimeSpan.FromMinutes(30), interval.Duration);
        Assert.False(interval.IsPerTaskOverride);
    }
}
