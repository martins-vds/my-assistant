using FocusAssistant.Application.Services;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Sets a per-task reminder interval override, or updates the global default.
/// </summary>
public sealed class SetReminderUseCase
{
    private readonly TaskTrackingService _tracking;
    private readonly IUserPreferencesRepository _preferencesRepository;

    public SetReminderUseCase(TaskTrackingService tracking, IUserPreferencesRepository preferencesRepository)
    {
        _tracking = tracking ?? throw new ArgumentNullException(nameof(tracking));
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
    }

    /// <summary>
    /// Sets a reminder interval. If taskName is null, sets the global default.
    /// </summary>
    public async Task<SetReminderResult> ExecuteAsync(double minutes, string? taskName = null, CancellationToken ct = default)
    {
        if (minutes <= 0)
            return SetReminderResult.Error("Reminder interval must be a positive number of minutes.");

        var interval = ReminderInterval.FromMinutes(minutes, isPerTaskOverride: taskName is not null);

        if (taskName is not null)
        {
            // Per-task override
            var task = _tracking.FindTaskByName(taskName);
            if (task is null)
                return SetReminderResult.Error($"No task named '{taskName}' found.");

            task.SetReminderInterval(interval);
            await _tracking.SaveAsync(ct);

            return SetReminderResult.Success($"Set reminder for '{task.Name}' to every {minutes} minutes.", task.Name);
        }
        else
        {
            // Global default
            var prefs = await _preferencesRepository.GetAsync(ct);
            if (prefs is null)
            {
                prefs = new Domain.Entities.UserPreferences(defaultReminderInterval: interval);
            }
            else
            {
                prefs.SetDefaultReminderInterval(interval);
            }

            await _preferencesRepository.SaveAsync(prefs, ct);

            return SetReminderResult.Success($"Set default reminder interval to every {minutes} minutes.", null);
        }
    }
}

public sealed record SetReminderResult
{
    public bool IsSuccess { get; init; }
    public string? Message { get; init; }
    public string? TaskName { get; init; }
    public string? ErrorMessage { get; init; }

    public static SetReminderResult Success(string message, string? taskName) => new()
    {
        IsSuccess = true,
        Message = message,
        TaskName = taskName
    };

    public static SetReminderResult Error(string message) => new()
    {
        ErrorMessage = message
    };
}
