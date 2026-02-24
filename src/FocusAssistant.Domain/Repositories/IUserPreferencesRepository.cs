using FocusAssistant.Domain.Entities;

namespace FocusAssistant.Domain.Repositories;

public interface IUserPreferencesRepository
{
    Task<UserPreferences?> GetAsync(CancellationToken ct = default);
    Task SaveAsync(UserPreferences preferences, CancellationToken ct = default);
    Task<bool> ExistsAsync(CancellationToken ct = default);
}
