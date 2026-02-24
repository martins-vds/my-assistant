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
}
