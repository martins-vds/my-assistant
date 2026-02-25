using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Domain.Tests.Entities;

public class UserPreferencesTests
{
    [Fact]
    public void Constructor_DefaultValues()
    {
        var prefs = new UserPreferences();

        Assert.Equal(ReminderInterval.Default.Duration, prefs.DefaultReminderInterval.Duration);
        Assert.Equal(TimeSpan.FromMinutes(5), prefs.IdleCheckInThreshold);
        Assert.Null(prefs.AutomaticReflectionTime);
        Assert.Equal("Hey Focus", prefs.WakeWord);
        Assert.NotEqual(Guid.Empty, prefs.Id);
    }

    [Fact]
    public void Constructor_CustomValues()
    {
        var interval = ReminderInterval.FromMinutes(30);
        var idle = TimeSpan.FromMinutes(10);
        var reflection = new TimeOnly(17, 30);

        var prefs = new UserPreferences(interval, idle, reflection, "OK Computer");

        Assert.Equal(TimeSpan.FromMinutes(30), prefs.DefaultReminderInterval.Duration);
        Assert.Equal(TimeSpan.FromMinutes(10), prefs.IdleCheckInThreshold);
        Assert.Equal(new TimeOnly(17, 30), prefs.AutomaticReflectionTime);
        Assert.Equal("OK Computer", prefs.WakeWord);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyWakeWord_Throws(string? wakeWord)
    {
        Assert.Throws<ArgumentException>(
            () => new UserPreferences(wakeWord: wakeWord!));
    }

    [Fact]
    public void SetDefaultReminderInterval_Updates()
    {
        var prefs = new UserPreferences();
        var newInterval = ReminderInterval.FromHours(2);
        var beforeUpdate = prefs.UpdatedAt;

        prefs.SetDefaultReminderInterval(newInterval);

        Assert.Equal(TimeSpan.FromHours(2), prefs.DefaultReminderInterval.Duration);
        Assert.True(prefs.UpdatedAt >= beforeUpdate);
    }

    [Fact]
    public void SetDefaultReminderInterval_Null_Throws()
    {
        var prefs = new UserPreferences();
        Assert.Throws<ArgumentNullException>(() => prefs.SetDefaultReminderInterval(null!));
    }

    [Fact]
    public void SetIdleCheckInThreshold_Updates()
    {
        var prefs = new UserPreferences();

        prefs.SetIdleCheckInThreshold(TimeSpan.FromMinutes(20));

        Assert.Equal(TimeSpan.FromMinutes(20), prefs.IdleCheckInThreshold);
    }

    [Fact]
    public void SetIdleCheckInThreshold_ZeroOrNegative_Throws()
    {
        var prefs = new UserPreferences();
        Assert.Throws<ArgumentException>(() => prefs.SetIdleCheckInThreshold(TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => prefs.SetIdleCheckInThreshold(TimeSpan.FromMinutes(-1)));
    }

    [Fact]
    public void SetAutomaticReflectionTime_SetsAndClears()
    {
        var prefs = new UserPreferences();

        prefs.SetAutomaticReflectionTime(new TimeOnly(17, 0));
        Assert.Equal(new TimeOnly(17, 0), prefs.AutomaticReflectionTime);

        prefs.SetAutomaticReflectionTime(null);
        Assert.Null(prefs.AutomaticReflectionTime);
    }

    [Fact]
    public void SetWakeWord_Updates()
    {
        var prefs = new UserPreferences();

        prefs.SetWakeWord("  OK Assistant  ");

        Assert.Equal("OK Assistant", prefs.WakeWord);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SetWakeWord_EmptyWakeWord_Throws(string? wakeWord)
    {
        var prefs = new UserPreferences();
        Assert.Throws<ArgumentException>(() => prefs.SetWakeWord(wakeWord!));
    }
}
