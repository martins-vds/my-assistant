using System.ComponentModel;
using FocusAssistant.Application.UseCases;
using Microsoft.Extensions.AI;

namespace FocusAssistant.Cli.Agent;

/// <summary>
/// Defines Copilot tools (AI functions) that map user intents to use cases.
/// Each tool is thin — validates input, delegates to a use case, and formats the response.
/// </summary>
public static class ToolDefinitions
{
    public static IList<AIFunction> CreateTaskTrackingTools(
        CreateTaskUseCase createTask,
        SwitchTaskUseCase switchTask,
        CompleteTaskUseCase completeTask,
        RenameTaskUseCase renameTask,
        DeleteTaskUseCase deleteTask,
        MergeTasksUseCase mergeTasks,
        GetOpenTasksUseCase getOpenTasks)
    {
        return
        [
            AIFunctionFactory.Create(
                async ([Description("The name of the task to create")] string name,
                       [Description("Create even if a task with the same name exists")] bool force = false) =>
                {
                    var result = await createTask.ExecuteAsync(name, force);
                    if (result.IsDuplicate)
                        return $"A task named '{result.TaskName}' already exists (status: {result.ExistingStatus}). " +
                               $"Say 'switch to {name}' to resume it, or confirm to create a new one.";
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to create task.";

                    var msg = $"Created and started '{result.TaskName}'.";
                    if (result.PausedTaskName is not null)
                        msg += $" Paused '{result.PausedTaskName}'.";
                    return msg;
                },
                new AIFunctionFactoryOptions { Name = "create_task", Description = "Create a new task and start working on it. Auto-pauses any current task." }),

            AIFunctionFactory.Create(
                async ([Description("The name of the task to switch to")] string name) =>
                {
                    var result = await switchTask.ExecuteAsync(name);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to switch task.";

                    var msg = $"Switched to '{result.TaskName}'.";
                    if (result.PreviousTaskName is not null)
                        msg += $" Paused '{result.PreviousTaskName}'.";
                    if (result.LastNote is not null)
                        msg += $" Last note: {result.LastNote}";
                    return msg;
                },
                new AIFunctionFactoryOptions { Name = "switch_task", Description = "Switch to an existing task by name, or create and start a new one if it doesn't exist." }),

            AIFunctionFactory.Create(
                async ([Description("The name of the task to complete (optional, defaults to current task)")] string? name = null) =>
                {
                    var result = await completeTask.ExecuteAsync(name);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to complete task.";

                    var msg = $"Completed '{result.TaskName}'.";
                    if (result.PausedTaskSuggestions.Count > 0)
                        msg += $" You still have paused: {string.Join(", ", result.PausedTaskSuggestions)}.";
                    return msg;
                },
                new AIFunctionFactoryOptions { Name = "complete_task", Description = "Mark a task as completed. If no name is given, completes the current task." }),

            AIFunctionFactory.Create(
                async ([Description("The current name of the task")] string oldName,
                       [Description("The new name for the task")] string newName) =>
                {
                    var result = await renameTask.ExecuteAsync(oldName, newName);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to rename task.";
                    return $"Renamed '{result.OldName}' to '{result.NewName}'.";
                },
                new AIFunctionFactoryOptions { Name = "rename_task", Description = "Rename an existing task." }),

            AIFunctionFactory.Create(
                async ([Description("The name of the task to delete")] string name,
                       [Description("Whether the user has confirmed deletion")] bool confirmed = false) =>
                {
                    var result = await deleteTask.ExecuteAsync(name, confirmed);
                    if (result.RequiresConfirmation)
                        return $"Are you sure you want to delete '{result.TaskName}'? This cannot be undone.";
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to delete task.";
                    return $"Deleted '{result.TaskName}'.";
                },
                new AIFunctionFactoryOptions { Name = "delete_task", Description = "Delete a task permanently. Requires confirmation." }),

            AIFunctionFactory.Create(
                async ([Description("The name of the task to merge from (will be deleted)")] string sourceName,
                       [Description("The name of the task to merge into (will be kept)")] string targetName) =>
                {
                    var result = await mergeTasks.ExecuteAsync(sourceName, targetName);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to merge tasks.";
                    return $"Merged '{result.SourceName}' into '{result.TargetName}'. Notes and time logs combined.";
                },
                new AIFunctionFactoryOptions { Name = "merge_tasks", Description = "Merge source task into target task, combining notes and time logs. Source task is deleted." }),

            AIFunctionFactory.Create(
                async () =>
                {
                    var result = await getOpenTasks.ExecuteAsync();
                    if (result.Tasks.Count == 0)
                        return "No open tasks. Create a new task to get started.";

                    var lines = result.Tasks.Select(t =>
                    {
                        var status = t.IsCurrent ? "▶ IN PROGRESS" : "⏸ PAUSED";
                        var time = t.TimeSpentToday > TimeSpan.Zero
                            ? $" ({t.TimeSpentToday:h\\:mm} today)"
                            : "";
                        var priority = t.PriorityRanking.HasValue ? $" [P{t.PriorityRanking}]" : "";
                        return $"  {status}{priority} {t.Name}{time}";
                    });

                    return $"Open tasks:\n{string.Join("\n", lines)}";
                },
                new AIFunctionFactoryOptions { Name = "get_open_tasks", Description = "List all open (in-progress and paused) tasks with status and time spent today." }),

            AIFunctionFactory.Create(
                async () =>
                {
                    var result = await getOpenTasks.ExecuteAsync();
                    var current = result.Tasks.FirstOrDefault(t => t.IsCurrent);
                    if (current is null)
                        return "No task currently in progress.";

                    var time = current.TimeSpentToday > TimeSpan.Zero
                        ? $" Time today: {current.TimeSpentToday:h\\:mm}."
                        : "";
                    return $"Currently working on '{current.Name}'.{time}";
                },
                new AIFunctionFactoryOptions { Name = "get_current_task", Description = "Get the name and status of the currently active task." })
        ];
    }

    /// <summary>
    /// Creates AI function tools for the reminder system (US2).
    /// </summary>
    public static IList<AIFunction> CreateReminderTools(SetReminderUseCase setReminder)
    {
        return
        [
            AIFunctionFactory.Create(
                async ([Description("Reminder interval in minutes")] double minutes,
                       [Description("Task name to set reminder for (optional, sets global default if omitted)")] string? taskName = null) =>
                {
                    var result = await setReminder.ExecuteAsync(minutes, taskName);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to set reminder.";
                    return result.Message ?? "Reminder set.";
                },
                new AIFunctionFactoryOptions { Name = "set_reminder", Description = "Set a reminder interval for a specific task or the global default. Interval is in minutes." })
        ];
    }

    /// <summary>
    /// Creates AI function tools for task notes (US3).
    /// </summary>
    public static IList<AIFunction> CreateNoteTools(AddNoteUseCase addNote, GetTaskNotesUseCase getTaskNotes)
    {
        return
        [
            AIFunctionFactory.Create(
                async ([Description("The note content to record")] string content,
                       [Description("Task name to attach note to (optional, defaults to current task)")] string? taskName = null,
                       [Description("Store as standalone note if no task is active")] bool standalone = false) =>
                {
                    var result = await addNote.ExecuteAsync(content, taskName, standalone);
                    if (result.RequiresTaskSelection)
                        return "No task is currently active. Which task should I attach this note to? Or say 'standalone' to save it without a task.";
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to add note.";
                    return result.TaskName is not null
                        ? $"Note added to '{result.TaskName}'."
                        : "Standalone note saved.";
                },
                new AIFunctionFactoryOptions { Name = "add_note", Description = "Add a timestamped note to the current task, a specific task by name, or as a standalone note." }),

            AIFunctionFactory.Create(
                async ([Description("Task name to get notes for (optional, defaults to current task)")] string? taskName = null) =>
                {
                    var result = await getTaskNotes.ExecuteAsync(taskName);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to get notes.";
                    if (result.Notes.Count == 0)
                    {
                        var target = result.TaskName ?? "standalone";
                        return $"No notes found for {target}.";
                    }

                    var header = result.TaskName is not null
                        ? $"Notes for '{result.TaskName}':"
                        : "Standalone notes:";

                    var lines = result.Notes.Select(n =>
                        $"  [{n.CreatedAt:HH:mm}] {n.Content}");

                    return $"{header}\n{string.Join("\n", lines)}";
                },
                new AIFunctionFactoryOptions { Name = "get_task_notes", Description = "Get all notes for a task or the current task. Returns standalone notes if no task is active." })
        ];
    }

    /// <summary>
    /// Creates AI function tools for end-of-day reflection (US4).
    /// </summary>
    public static IList<AIFunction> CreateReflectionTools(
        StartReflectionUseCase startReflection,
        SetPrioritiesUseCase setPriorities,
        GetOpenTasksUseCase getOpenTasks)
    {
        return
        [
            AIFunctionFactory.Create(
                async () =>
                {
                    var result = await startReflection.ExecuteAsync();
                    return result.Summary ?? "Unable to generate daily summary.";
                },
                new AIFunctionFactoryOptions { Name = "start_reflection", Description = "Start an end-of-day reflection. Returns a summary of completed tasks, open tasks, time spent, and standalone notes." }),

            AIFunctionFactory.Create(
                async ([Description("Ordered list of task names by priority (highest first)")] string[] taskNames,
                       [Description("Optional note to add to the plan")] string? note = null) =>
                {
                    var result = await setPriorities.ExecuteAsync(taskNames, note);
                    if (!result.IsSuccess)
                        return result.ErrorMessage ?? "Failed to set priorities.";
                    return $"Priorities set for {result.PlanDate:yyyy-MM-dd}: {string.Join(" → ", result.OrderedTaskNames)}";
                },
                new AIFunctionFactoryOptions { Name = "set_priorities", Description = "Set priority order for open tasks for tomorrow. Task names should be ordered from highest to lowest priority." }),

            AIFunctionFactory.Create(
                async () =>
                {
                    var result = await getOpenTasks.ExecuteAsync();
                    if (result.Tasks.Count == 0)
                        return "No tasks today. Nothing to summarize.";

                    var lines = result.Tasks.Select(t =>
                    {
                        var status = t.IsCurrent ? "IN PROGRESS" : "PAUSED";
                        var time = t.TimeSpentToday > TimeSpan.Zero
                            ? $" ({t.TimeSpentToday:h\\:mm} today)"
                            : "";
                        return $"  [{status}] {t.Name}{time}";
                    });

                    return $"Daily summary:\n{string.Join("\n", lines)}";
                },
                new AIFunctionFactoryOptions { Name = "get_daily_summary", Description = "Get a summary of today's tasks with status and time spent." })
        ];
    }

    /// <summary>
    /// Creates AI function tools for morning briefing (US5).
    /// </summary>
    public static IList<AIFunction> CreateBriefingTools(GetMorningBriefingUseCase getMorningBriefing)
    {
        return
        [
            AIFunctionFactory.Create(
                async () =>
                {
                    var result = await getMorningBriefing.ExecuteAsync();
                    return result.Briefing ?? "Unable to generate morning briefing.";
                },
                new AIFunctionFactoryOptions { Name = "get_morning_briefing", Description = "Get the morning briefing with yesterday's priorities, open tasks, and carry-over information." })
        ];
    }

    /// <summary>
    /// Creates AI function tools for preference management (Onboarding).
    /// </summary>
    public static IList<AIFunction> CreatePreferenceTools(SavePreferencesUseCase savePreferences)
    {
        return
        [
            AIFunctionFactory.Create(
                async ([Description("Default reminder interval in minutes (e.g., 30)")] double? reminderIntervalMinutes = null,
                       [Description("Idle check-in threshold in minutes (e.g., 15)")] double? idleThresholdMinutes = null,
                       [Description("Automatic reflection time in HH:mm format (e.g., '17:00') or 'none' to disable")] string? reflectionTime = null,
                       [Description("Wake word phrase (e.g., 'Hey Focus')")] string? wakeWord = null) =>
                {
                    var result = await savePreferences.ExecuteAsync(reminderIntervalMinutes, idleThresholdMinutes, reflectionTime, wakeWord);
                    return result.Message ?? (result.IsSuccess ? "Preferences saved." : "Failed to save preferences.");
                },
                new AIFunctionFactoryOptions { Name = "save_preferences", Description = "Save user preferences during onboarding or update all preferences at once. All parameters are optional — only provided values are changed." }),

            AIFunctionFactory.Create(
                async ([Description("Name of the setting to update: reminder_interval, idle_threshold, reflection_time, or wake_word")] string settingName,
                       [Description("New value for the setting")] string value) =>
                {
                    var result = await savePreferences.UpdateAsync(settingName, value);
                    return result.Message ?? (result.IsSuccess ? "Setting updated." : "Failed to update setting.");
                },
                new AIFunctionFactoryOptions { Name = "update_preferences", Description = "Update a single user preference by name. Available settings: reminder_interval (minutes), idle_threshold (minutes), reflection_time (HH:mm or 'none'), wake_word." }),

            AIFunctionFactory.Create(
                async () =>
                {
                    var result = await savePreferences.GetCurrentAsync();
                    return result.Summary ?? "No preferences configured yet.";
                },
                new AIFunctionFactoryOptions { Name = "get_preferences", Description = "Get the current user preferences including reminder interval, idle threshold, reflection time, and wake word." })
        ];
    }
}
