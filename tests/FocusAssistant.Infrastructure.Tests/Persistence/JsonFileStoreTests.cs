using FocusAssistant.Infrastructure.Persistence;

namespace FocusAssistant.Infrastructure.Tests.Persistence;

public class JsonFileStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public JsonFileStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "focus-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _filePath = Path.Combine(_tempDir, "test-store.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ReadAllAsync_NoFile_ReturnsEmptyList()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);

        var result = await store.ReadAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task WriteAllAsync_ThenReadAllAsync_RoundTrips()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);
        var items = new List<TestEntity>
        {
            new() { Id = Guid.NewGuid(), Name = "First" },
            new() { Id = Guid.NewGuid(), Name = "Second" }
        };

        await store.WriteAllAsync(items);
        var result = await store.ReadAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("First", result[0].Name);
        Assert.Equal("Second", result[1].Name);
    }

    [Fact]
    public async Task WriteAllAsync_OverwritesExistingData()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);
        var original = new List<TestEntity> { new() { Id = Guid.NewGuid(), Name = "Original" } };
        await store.WriteAllAsync(original);

        var updated = new List<TestEntity> { new() { Id = Guid.NewGuid(), Name = "Updated" } };
        await store.WriteAllAsync(updated);

        var result = await store.ReadAllAsync();
        Assert.Single(result);
        Assert.Equal("Updated", result[0].Name);
    }

    [Fact]
    public async Task WriteSingleAsync_ThenReadSingleAsync_RoundTrips()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);
        var item = new TestEntity { Id = Guid.NewGuid(), Name = "Single" };

        await store.WriteSingleAsync(item);
        var result = await store.ReadSingleAsync();

        Assert.NotNull(result);
        Assert.Equal(item.Id, result!.Id);
        Assert.Equal("Single", result.Name);
    }

    [Fact]
    public async Task ReadSingleAsync_NoFile_ReturnsNull()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);

        var result = await store.ReadSingleAsync();

        Assert.Null(result);
    }

    [Fact]
    public void Exists_NoFile_ReturnsFalse()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);

        Assert.False(store.Exists());
    }

    [Fact]
    public async Task Exists_AfterWrite_ReturnsTrue()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);
        await store.WriteAllAsync(new List<TestEntity> { new() { Id = Guid.NewGuid(), Name = "X" } });

        Assert.True(store.Exists());
    }

    [Fact]
    public async Task AtomicWrite_NoTempFileLeftBehind()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);
        await store.WriteAllAsync(new List<TestEntity> { new() { Id = Guid.NewGuid(), Name = "Atomic" } });

        Assert.False(File.Exists(_filePath + ".tmp"));
        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public async Task ConcurrentAccess_DoesNotCorruptData()
    {
        var store = new JsonFileStore<TestEntity>(_filePath);
        var tasks = new List<Task>();

        for (var i = 0; i < 10; i++)
        {
            var name = $"Task-{i}";
            tasks.Add(Task.Run(async () =>
            {
                var items = await store.ReadAllAsync();
                items.Add(new TestEntity { Id = Guid.NewGuid(), Name = name });
                await store.WriteAllAsync(items);
            }));
        }

        await Task.WhenAll(tasks);

        // With semaphore-based locking, final state should be valid JSON
        var result = await store.ReadAllAsync();
        Assert.NotNull(result);
        Assert.True(result.Count > 0, "Should have at least one item after concurrent writes");
    }

    [Fact]
    public async Task WriteAllAsync_CreatesDirectoryIfMissing()
    {
        var nestedPath = Path.Combine(_tempDir, "sub", "dir", "store.json");
        var store = new JsonFileStore<TestEntity>(nestedPath);

        await store.WriteAllAsync(new List<TestEntity> { new() { Id = Guid.NewGuid(), Name = "Nested" } });

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public async Task ReadAllAsync_EmptyFile_ReturnsEmptyList()
    {
        await File.WriteAllTextAsync(_filePath, "");
        var store = new JsonFileStore<TestEntity>(_filePath);

        var result = await store.ReadAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadAllAsync_WhitespaceFile_ReturnsEmptyList()
    {
        await File.WriteAllTextAsync(_filePath, "   ");
        var store = new JsonFileStore<TestEntity>(_filePath);

        var result = await store.ReadAllAsync();

        Assert.Empty(result);
    }

    [Fact]
    public void Constructor_NullFilePath_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new JsonFileStore<TestEntity>(null!));
    }

    public class TestEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
