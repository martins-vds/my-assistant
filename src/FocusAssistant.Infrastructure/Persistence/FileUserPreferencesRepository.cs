using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Infrastructure.Persistence;

public sealed class FileUserPreferencesRepository : IUserPreferencesRepository
{
    private readonly JsonFileStore<UserPreferences> _store;

    public FileUserPreferencesRepository()
    {
        _store = new JsonFileStore<UserPreferences>(DataDirectory.GetFilePath("user-preferences.json"));
    }

    public FileUserPreferencesRepository(string filePath)
    {
        _store = new JsonFileStore<UserPreferences>(filePath);
    }

    public async Task<UserPreferences?> GetAsync(CancellationToken ct = default)
    {
        return await _store.ReadSingleAsync(ct);
    }

    public async Task SaveAsync(UserPreferences preferences, CancellationToken ct = default)
    {
        await _store.WriteSingleAsync(preferences, ct);
    }

    public async Task<bool> ExistsAsync(CancellationToken ct = default)
    {
        return _store.Exists();
    }
}
