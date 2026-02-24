using FocusAssistant.Domain.Entities;
using FocusAssistant.Infrastructure.Persistence;

namespace FocusAssistant.Infrastructure.Tests.Persistence;

using TaskStatus = FocusAssistant.Domain.ValueObjects.TaskStatus;

public class FileTaskRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileTaskRepository _repo;

    public FileTaskRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "focus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _repo = new FileTaskRepository(Path.Combine(_tempDir, "tasks.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_ReturnsTask()
    {
        var task = new FocusTask("Test Task");

        await _repo.SaveAsync(task);
        var result = await _repo.GetByIdAsync(task.Id);

        Assert.NotNull(result);
        Assert.Equal(task.Id, result!.Id);
        Assert.Equal("Test Task", result.Name);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllTasks()
    {
        await _repo.SaveAsync(new FocusTask("Task 1"));
        await _repo.SaveAsync(new FocusTask("Task 2"));
        await _repo.SaveAsync(new FocusTask("Task 3"));

        var result = await _repo.GetAllAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task GetByStatusAsync_FiltersCorrectly()
    {
        var active = new FocusTask("Active");
        active.Start();
        var paused = new FocusTask("Paused");
        paused.Start();
        paused.Pause();

        await _repo.SaveAsync(active);
        await _repo.SaveAsync(paused);

        var inProgress = await _repo.GetByStatusAsync(TaskStatus.InProgress);
        var pausedResult = await _repo.GetByStatusAsync(TaskStatus.Paused);

        Assert.Single(inProgress);
        Assert.Equal("Active", inProgress[0].Name);
        Assert.Single(pausedResult);
        Assert.Equal("Paused", pausedResult[0].Name);
    }

    [Fact]
    public async Task GetByNameAsync_CaseInsensitive()
    {
        await _repo.SaveAsync(new FocusTask("API Refactor"));

        var result = await _repo.GetByNameAsync("api refactor");

        Assert.NotNull(result);
        Assert.Equal("API Refactor", result!.Name);
    }

    [Fact]
    public async Task GetByNameAsync_ExcludesArchived()
    {
        var task = new FocusTask("Archived Task");
        task.Start();
        task.Complete();
        task.Archive();
        await _repo.SaveAsync(task);

        var result = await _repo.GetByNameAsync("Archived Task");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByNameAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.GetByNameAsync("nonexistent");
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingTask()
    {
        var task = new FocusTask("Original");
        await _repo.SaveAsync(task);

        task.Rename("Updated");
        await _repo.SaveAsync(task);

        var all = await _repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Updated", all[0].Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesTask()
    {
        var task = new FocusTask("To Delete");
        await _repo.SaveAsync(task);

        await _repo.DeleteAsync(task.Id);

        var result = await _repo.GetByIdAsync(task.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNotThrow()
    {
        await _repo.DeleteAsync(Guid.NewGuid()); // Should not throw
    }

    [Fact]
    public async Task SaveAllAsync_SavesMultipleTasks()
    {
        var tasks = new[]
        {
            new FocusTask("Task 1"),
            new FocusTask("Task 2"),
            new FocusTask("Task 3")
        };

        await _repo.SaveAllAsync(tasks);

        var all = await _repo.GetAllAsync();
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task SaveAllAsync_UpdatesExistingAndAddsNew()
    {
        var existing = new FocusTask("Existing");
        await _repo.SaveAsync(existing);

        existing.Rename("Updated");
        var newTask = new FocusTask("New");
        await _repo.SaveAllAsync(new[] { existing, newTask });

        var all = await _repo.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Contains(all, t => t.Name == "Updated");
        Assert.Contains(all, t => t.Name == "New");
    }

    [Fact]
    public async Task DataPersistsAcrossInstances()
    {
        var filePath = Path.Combine(_tempDir, "persist.json");
        var repo1 = new FileTaskRepository(filePath);

        await repo1.SaveAsync(new FocusTask("Persisted"));

        var repo2 = new FileTaskRepository(filePath);
        var all = await repo2.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("Persisted", all[0].Name);
    }
}
