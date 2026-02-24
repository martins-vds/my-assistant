using FocusAssistant.Domain.Entities;
using FocusAssistant.Infrastructure.Persistence;

namespace FocusAssistant.Infrastructure.Tests.Persistence;

public class FileDailyPlanRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileDailyPlanRepository _repo;

    public FileDailyPlanRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "focus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _repo = new FileDailyPlanRepository(Path.Combine(_tempDir, "plans.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByDateAsync_ReturnsPlan()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var plan = new DailyPlan(today);

        await _repo.SaveAsync(plan);
        var result = await _repo.GetByDateAsync(today);

        Assert.NotNull(result);
        Assert.Equal(plan.Id, result!.Id);
        Assert.Equal(today, result.DateFor);
    }

    [Fact]
    public async Task GetByDateAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.GetByDateAsync(DateOnly.FromDateTime(DateTime.Today));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsMostRecentPlan()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var today = DateOnly.FromDateTime(DateTime.Today);

        await _repo.SaveAsync(new DailyPlan(yesterday));
        await _repo.SaveAsync(new DailyPlan(today));

        var latest = await _repo.GetLatestAsync();

        Assert.NotNull(latest);
        Assert.Equal(today, latest!.DateFor);
    }

    [Fact]
    public async Task GetLatestAsync_Empty_ReturnsNull()
    {
        var result = await _repo.GetLatestAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllPlansOrderedByDateDescending()
    {
        var day1 = DateOnly.FromDateTime(DateTime.Today.AddDays(-2));
        var day2 = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var day3 = DateOnly.FromDateTime(DateTime.Today);

        await _repo.SaveAsync(new DailyPlan(day1));
        await _repo.SaveAsync(new DailyPlan(day3));
        await _repo.SaveAsync(new DailyPlan(day2));

        var all = await _repo.GetAllAsync();

        Assert.Equal(3, all.Count);
        Assert.Equal(day3, all[0].DateFor);
        Assert.Equal(day2, all[1].DateFor);
        Assert.Equal(day1, all[2].DateFor);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingPlan()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var plan = new DailyPlan(today);
        await _repo.SaveAsync(plan);

        plan.AddNote("Updated note");
        await _repo.SaveAsync(plan);

        var all = await _repo.GetAllAsync();
        Assert.Single(all);
        Assert.Single(all[0].Notes);
    }

    [Fact]
    public async Task DataPersistsAcrossInstances()
    {
        var filePath = Path.Combine(_tempDir, "persist-plans.json");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var repo1 = new FileDailyPlanRepository(filePath);
        await repo1.SaveAsync(new DailyPlan(today));

        var repo2 = new FileDailyPlanRepository(filePath);
        var all = await repo2.GetAllAsync();

        Assert.Single(all);
        Assert.Equal(today, all[0].DateFor);
    }
}
