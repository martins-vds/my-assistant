using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Tests.Entities;

public class DailyPlanTests
{
    [Fact]
    public void Constructor_CreatesWithDate()
    {
        var date = new DateOnly(2024, 1, 15);
        var plan = new DailyPlan(date);

        Assert.Equal(date, plan.DateFor);
        Assert.Empty(plan.OrderedTaskIds);
        Assert.Empty(plan.Notes);
        Assert.NotEqual(Guid.Empty, plan.Id);
    }

    [Fact]
    public void SetTaskPriorities_SetsOrderedIds()
    {
        var plan = new DailyPlan(DateOnly.FromDateTime(DateTime.UtcNow));
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        plan.SetTaskPriorities(ids);

        Assert.Equal(3, plan.OrderedTaskIds.Count);
        Assert.Equal(ids[0], plan.OrderedTaskIds[0]);
        Assert.Equal(ids[1], plan.OrderedTaskIds[1]);
        Assert.Equal(ids[2], plan.OrderedTaskIds[2]);
    }

    [Fact]
    public void SetTaskPriorities_ReplacesExisting()
    {
        var plan = new DailyPlan(DateOnly.FromDateTime(DateTime.UtcNow));
        plan.SetTaskPriorities(new[] { Guid.NewGuid() });

        var newIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        plan.SetTaskPriorities(newIds);

        Assert.Equal(2, plan.OrderedTaskIds.Count);
    }

    [Fact]
    public void AddNote_AddsNote()
    {
        var plan = new DailyPlan(DateOnly.FromDateTime(DateTime.UtcNow));

        plan.AddNote("Focus on deployment");

        Assert.Single(plan.Notes);
        Assert.Equal("Focus on deployment", plan.Notes[0]);
    }

    [Fact]
    public void AddNote_TrimsNote()
    {
        var plan = new DailyPlan(DateOnly.FromDateTime(DateTime.UtcNow));

        plan.AddNote("  trimmed  ");

        Assert.Equal("trimmed", plan.Notes[0]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddNote_EmptyNote_Throws(string? note)
    {
        var plan = new DailyPlan(DateOnly.FromDateTime(DateTime.UtcNow));
        Assert.Throws<ArgumentException>(() => plan.AddNote(note!));
    }
}
