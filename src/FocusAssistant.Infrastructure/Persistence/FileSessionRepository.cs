using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;

namespace FocusAssistant.Infrastructure.Persistence;

public sealed class FileSessionRepository : ISessionRepository
{
    private readonly JsonFileStore<WorkSession> _store;

    public FileSessionRepository()
    {
        _store = new JsonFileStore<WorkSession>(DataDirectory.GetFilePath("sessions.json"));
    }

    public FileSessionRepository(string filePath)
    {
        _store = new JsonFileStore<WorkSession>(filePath);
    }

    public async Task<WorkSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var sessions = await _store.ReadAllAsync(ct);
        return sessions.FirstOrDefault(s => s.Id == id);
    }

    public async Task<WorkSession?> GetLatestAsync(CancellationToken ct = default)
    {
        var sessions = await _store.ReadAllAsync(ct);
        return sessions.OrderByDescending(s => s.StartTime).FirstOrDefault();
    }

    public async Task<IReadOnlyList<WorkSession>> GetAllAsync(CancellationToken ct = default)
    {
        var sessions = await _store.ReadAllAsync(ct);
        return sessions.AsReadOnly();
    }

    public async Task SaveAsync(WorkSession session, CancellationToken ct = default)
    {
        var sessions = await _store.ReadAllAsync(ct);
        var index = sessions.FindIndex(s => s.Id == session.Id);
        if (index >= 0)
            sessions[index] = session;
        else
            sessions.Add(session);

        await _store.WriteAllAsync(sessions, ct);
    }
}
