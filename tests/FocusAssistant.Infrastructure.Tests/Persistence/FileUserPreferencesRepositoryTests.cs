using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.ValueObjects;
using FocusAssistant.Infrastructure.Persistence;

namespace FocusAssistant.Infrastructure.Tests.Persistence;

public class FileUserPreferencesRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileUserPreferencesRepository _repo;

    public FileUserPreferencesRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "focus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _repo = new FileUserPreferencesRepository(Path.Combine(_tempDir, "prefs.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GetAsync_NoPreferences_ReturnsNull()
    {
        var result = await _repo.GetAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_RoundTrips()
    {
        var prefs = new UserPreferences();
        prefs.SetWakeWord("Hey Buddy");
        prefs.SetDefaultReminderInterval(ReminderInterval.FromMinutes(30));

        await _repo.SaveAsync(prefs);
        var result = await _repo.GetAsync();

        Assert.NotNull(result);
        Assert.Equal("Hey Buddy", result!.WakeWord);
        Assert.Equal(ReminderInterval.FromMinutes(30), result.DefaultReminderInterval);
    }

    [Fact]
    public async Task ExistsAsync_NoFile_ReturnsFalse()
    {
        var exists = await _repo.ExistsAsync();
        Assert.False(exists);
    }

    [Fact]
    public async Task ExistsAsync_AfterSave_ReturnsTrue()
    {
        await _repo.SaveAsync(new UserPreferences());
        var exists = await _repo.ExistsAsync();
        Assert.True(exists);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExisting()
    {
        var prefs1 = new UserPreferences();
        prefs1.SetWakeWord("First");
        await _repo.SaveAsync(prefs1);

        var prefs2 = new UserPreferences();
        prefs2.SetWakeWord("Second");
        await _repo.SaveAsync(prefs2);

        var result = await _repo.GetAsync();
        Assert.NotNull(result);
        Assert.Equal("Second", result!.WakeWord);
    }

    [Fact]
    public async Task DataPersistsAcrossInstances()
    {
        var filePath = Path.Combine(_tempDir, "persist-prefs.json");
        var repo1 = new FileUserPreferencesRepository(filePath);
        var prefs = new UserPreferences();
        prefs.SetWakeWord("Persisted");
        await repo1.SaveAsync(prefs);

        var repo2 = new FileUserPreferencesRepository(filePath);
        var result = await repo2.GetAsync();

        Assert.NotNull(result);
        Assert.Equal("Persisted", result!.WakeWord);
    }
}
