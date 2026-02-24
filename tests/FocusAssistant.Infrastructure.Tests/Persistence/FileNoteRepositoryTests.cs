using FocusAssistant.Domain.Entities;
using FocusAssistant.Infrastructure.Persistence;

namespace FocusAssistant.Infrastructure.Tests.Persistence;

public class FileNoteRepositoryTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileNoteRepository _repo;

    public FileNoteRepositoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "focus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _repo = new FileNoteRepository(Path.Combine(_tempDir, "notes.json"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByIdAsync_ReturnsNote()
    {
        var note = new TaskNote("Test note content");

        await _repo.SaveAsync(note);
        var result = await _repo.GetByIdAsync(note.Id);

        Assert.NotNull(result);
        Assert.Equal(note.Id, result!.Id);
        Assert.Equal("Test note content", result.Content);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        var result = await _repo.GetByIdAsync(Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task GetByTaskIdAsync_ReturnsNotesForTask()
    {
        var taskId = Guid.NewGuid();
        var note1 = new TaskNote("Note 1");
        note1.AttachToTask(taskId);
        var note2 = new TaskNote("Note 2");
        note2.AttachToTask(taskId);
        var unrelated = new TaskNote("Unrelated");
        unrelated.AttachToTask(Guid.NewGuid());

        await _repo.SaveAsync(note1);
        await _repo.SaveAsync(note2);
        await _repo.SaveAsync(unrelated);

        var result = await _repo.GetByTaskIdAsync(taskId);

        Assert.Equal(2, result.Count);
        Assert.All(result, n => Assert.Equal(taskId, n.TaskId));
    }

    [Fact]
    public async Task GetByTaskIdAsync_OrderedByCreatedAt()
    {
        var taskId = Guid.NewGuid();
        var note1 = new TaskNote("First");
        note1.AttachToTask(taskId);
        await _repo.SaveAsync(note1);

        await Task.Delay(10);
        var note2 = new TaskNote("Second");
        note2.AttachToTask(taskId);
        await _repo.SaveAsync(note2);

        var result = await _repo.GetByTaskIdAsync(taskId);

        Assert.Equal("First", result[0].Content);
        Assert.Equal("Second", result[1].Content);
    }

    [Fact]
    public async Task GetStandaloneNotesAsync_ReturnsOnlyStandalone()
    {
        var standalone = new TaskNote("Standalone");
        var attached = new TaskNote("Attached");
        attached.AttachToTask(Guid.NewGuid());

        await _repo.SaveAsync(standalone);
        await _repo.SaveAsync(attached);

        var result = await _repo.GetStandaloneNotesAsync();

        Assert.Single(result);
        Assert.Equal("Standalone", result[0].Content);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllNotes()
    {
        await _repo.SaveAsync(new TaskNote("A"));
        await _repo.SaveAsync(new TaskNote("B"));
        await _repo.SaveAsync(new TaskNote("C"));

        var result = await _repo.GetAllAsync();

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task SaveAsync_UpdatesExistingNote()
    {
        var note = new TaskNote("Original");
        await _repo.SaveAsync(note);

        note.AttachToTask(Guid.NewGuid());
        await _repo.SaveAsync(note);

        var all = await _repo.GetAllAsync();
        Assert.Single(all);
        Assert.False(all[0].IsStandalone);
    }

    [Fact]
    public async Task DeleteAsync_RemovesNote()
    {
        var note = new TaskNote("To Delete");
        await _repo.SaveAsync(note);

        await _repo.DeleteAsync(note.Id);

        var result = await _repo.GetByIdAsync(note.Id);
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteAsync_NonExistent_DoesNotThrow()
    {
        await _repo.DeleteAsync(Guid.NewGuid()); // Should not throw
    }

    [Fact]
    public async Task DataPersistsAcrossInstances()
    {
        var filePath = Path.Combine(_tempDir, "persist-notes.json");
        var repo1 = new FileNoteRepository(filePath);
        await repo1.SaveAsync(new TaskNote("Persisted"));

        var repo2 = new FileNoteRepository(filePath);
        var all = await repo2.GetAllAsync();

        Assert.Single(all);
        Assert.Equal("Persisted", all[0].Content);
    }
}
