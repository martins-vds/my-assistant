using FocusAssistant.Domain.Entities;
using FocusAssistant.Domain.Repositories;
using FocusAssistant.Domain.ValueObjects;

namespace FocusAssistant.Application.UseCases;

/// <summary>
/// Saves or updates user preferences. Used during onboarding (first-use) and
/// when the user wants to change settings at any time.
/// </summary>
public sealed class SavePreferencesUseCase
{
    private readonly IUserPreferencesRepository _preferencesRepository;

    public SavePreferencesUseCase(IUserPreferencesRepository preferencesRepository)
    {
        _preferencesRepository = preferencesRepository ?? throw new ArgumentNullException(nameof(preferencesRepository));
    }

    /// <summary>
    /// Save preferences during onboarding or create defaults.
    /// </summary>
    public async Task<SavePreferencesResult> ExecuteAsync(
        double? reminderIntervalMinutes = null,
        double? idleThresholdMinutes = null,
        string? reflectionTime = null,
        string? wakeWord = null,
        CancellationToken ct = default)
    {
        try
        {
            var existing = await _preferencesRepository.GetAsync(ct);

            if (existing is not null)
            {
                // Update existing preferences
                return await UpdateExistingAsync(existing, reminderIntervalMinutes, idleThresholdMinutes, reflectionTime, wakeWord, ct);
            }

            // Create new preferences (onboarding)
            var reminderInterval = reminderIntervalMinutes.HasValue
                ? new ReminderInterval(TimeSpan.FromMinutes(reminderIntervalMinutes.Value))
                : null;

            var idleThreshold = idleThresholdMinutes.HasValue
                ? TimeSpan.FromMinutes(idleThresholdMinutes.Value)
                : (TimeSpan?)null;

            TimeOnly? reflectionTimeValue = null;
            if (!string.IsNullOrWhiteSpace(reflectionTime) && TimeOnly.TryParse(reflectionTime, out var parsed))
            {
                reflectionTimeValue = parsed;
            }

            var preferences = new UserPreferences(
                defaultReminderInterval: reminderInterval,
                idleCheckInThreshold: idleThreshold,
                automaticReflectionTime: reflectionTimeValue,
                wakeWord: wakeWord ?? "Hey Focus");

            await _preferencesRepository.SaveAsync(preferences, ct);

            return new SavePreferencesResult(true, "Preferences saved successfully.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SavePreferencesResult(false, $"Failed to save preferences: {ex.Message}");
        }
    }

    /// <summary>
    /// Update a specific preference field.
    /// </summary>
    public async Task<SavePreferencesResult> UpdateAsync(
        string settingName,
        string value,
        CancellationToken ct = default)
    {
        try
        {
            var preferences = await _preferencesRepository.GetAsync(ct);
            if (preferences is null)
            {
                return new SavePreferencesResult(false, "No preferences found. Please complete onboarding first.");
            }

            switch (settingName.ToLowerInvariant().Replace(" ", "").Replace("_", ""))
            {
                case "reminderinterval":
                case "defaultreminderinterval":
                    if (double.TryParse(value, out var minutes))
                    {
                        preferences.SetDefaultReminderInterval(new ReminderInterval(TimeSpan.FromMinutes(minutes)));
                    }
                    else
                    {
                        return new SavePreferencesResult(false, $"Invalid reminder interval: '{value}'. Please provide a number of minutes.");
                    }
                    break;

                case "idlethreshold":
                case "idlecheckin":
                case "idlecheckinthreshold":
                    if (double.TryParse(value, out var idleMinutes))
                    {
                        preferences.SetIdleCheckInThreshold(TimeSpan.FromMinutes(idleMinutes));
                    }
                    else
                    {
                        return new SavePreferencesResult(false, $"Invalid idle threshold: '{value}'. Please provide a number of minutes.");
                    }
                    break;

                case "reflectiontime":
                case "automaticreflectiontime":
                    if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
                    {
                        preferences.SetAutomaticReflectionTime(null);
                    }
                    else if (TimeOnly.TryParse(value, out var time))
                    {
                        preferences.SetAutomaticReflectionTime(time);
                    }
                    else
                    {
                        return new SavePreferencesResult(false, $"Invalid reflection time: '{value}'. Use a time like '17:00' or 'none' to disable.");
                    }
                    break;

                case "wakeword":
                    preferences.SetWakeWord(value);
                    break;

                default:
                    return new SavePreferencesResult(false,
                        $"Unknown setting: '{settingName}'. Available settings: reminder_interval, idle_threshold, reflection_time, wake_word.");
            }

            await _preferencesRepository.SaveAsync(preferences, ct);
            return new SavePreferencesResult(true, $"Updated {settingName} to '{value}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new SavePreferencesResult(false, $"Failed to update preference: {ex.Message}");
        }
    }

    /// <summary>
    /// Get current preferences as a formatted summary.
    /// </summary>
    public async Task<GetPreferencesResult> GetCurrentAsync(CancellationToken ct = default)
    {
        var preferences = await _preferencesRepository.GetAsync(ct);
        if (preferences is null)
        {
            return new GetPreferencesResult(false, "No preferences configured yet.");
        }

        var summary = $"Current preferences:\n" +
                      $"  Reminder interval: {preferences.DefaultReminderInterval.Duration.TotalMinutes} minutes\n" +
                      $"  Idle check-in threshold: {preferences.IdleCheckInThreshold.TotalMinutes} minutes\n" +
                      $"  Reflection time: {(preferences.AutomaticReflectionTime.HasValue ? preferences.AutomaticReflectionTime.Value.ToString("HH:mm") : "not set")}\n" +
                      $"  Wake word: {preferences.WakeWord}";

        return new GetPreferencesResult(true, summary);
    }

    private async Task<SavePreferencesResult> UpdateExistingAsync(
        UserPreferences preferences,
        double? reminderIntervalMinutes,
        double? idleThresholdMinutes,
        string? reflectionTime,
        string? wakeWord,
        CancellationToken ct)
    {
        if (reminderIntervalMinutes.HasValue)
            preferences.SetDefaultReminderInterval(new ReminderInterval(TimeSpan.FromMinutes(reminderIntervalMinutes.Value)));

        if (idleThresholdMinutes.HasValue)
            preferences.SetIdleCheckInThreshold(TimeSpan.FromMinutes(idleThresholdMinutes.Value));

        if (reflectionTime is not null)
        {
            if (string.Equals(reflectionTime, "none", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reflectionTime, "off", StringComparison.OrdinalIgnoreCase))
            {
                preferences.SetAutomaticReflectionTime(null);
            }
            else if (TimeOnly.TryParse(reflectionTime, out var parsed))
            {
                preferences.SetAutomaticReflectionTime(parsed);
            }
        }

        if (wakeWord is not null)
            preferences.SetWakeWord(wakeWord);

        await _preferencesRepository.SaveAsync(preferences, ct);
        return new SavePreferencesResult(true, "Preferences updated successfully.");
    }
}

public record SavePreferencesResult(bool IsSuccess, string? Message = null);

public record GetPreferencesResult(bool IsSuccess, string? Summary = null);
