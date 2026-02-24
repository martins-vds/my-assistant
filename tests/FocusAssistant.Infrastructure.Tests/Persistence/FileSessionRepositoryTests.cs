using FocusAssistant.Domain.Entities;
using FocusAssistant.Infrastructure.Persistence;

namespace FocusAssistant.Infrastructure.Tests.Persistence;

public class FileSessionRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSessionRepository _repo;

    public FileSessionRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "focus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _repo = new FileSessionRepository(Path.Combine(_tempDir, "sessions.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_ReturnsSession()
    {
        var session = new WorkSession();

        await _repo.SaveAsync(session);
        var result = await _repo.GetByIdAsync(session.Id);

        Assert.NotNull(result);
        Assert.Equal(session.Id, result!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsMostRecentSession()
    {
        var older = new WorkSession();
        await _repo.SaveAsync(older);

        // Small delay to ensure different StartTime
        await Task.Delay(10);
        var newer = new WorkSession();
        await _repo.SaveAsync(newer);

        var latest = await _repo.GetLatestAsync();

        Assert.NotNull(latest);
        Assert.Equal(newer.Id, latest!.Id);
    }

    [Fact]
    public async Task GetLatestAsync_Empty_ReturnsNull()
    {
        var result = await _repo.GetLatestAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllSessions()
    {
        await _repo.SaveAsync(new WorkSession());
        await _repo.SaveAsync(new WorkSession());

        var all = await _repo.GetAllAsync();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingSession()
    {
        var session = new WorkSession();
        await _repo.SaveAsync(session);

        session.RecordTaskWorkedOn(Guid.NewGuid());
        await _repo.SaveAsync(session);

        var all = await _repo.GetAllAsync();
        Assert.Single(all);
        Assert.Single(all[0].TaskIdsWorkedOn);
    }

    [Fact]
    public async Task DataPersistsAcrossInstances()
    {
        var filePath = Path.Combine(_tempDir, "persist-sessions.json");
        var repo1 = new FileSessionRepository(filePath);
        await repo1.SaveAsync(new WorkSession());

        var repo2 = new FileSessionRepository(filePath);
        var all = await repo2.GetAllAsync();

        Assert.Single(all);
    }
}
