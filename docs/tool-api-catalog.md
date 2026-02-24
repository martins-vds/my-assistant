# Tool API Catalog

All tools are AI functions registered with the Copilot SDK. They are invoked by the LLM during a conversation session.

---

## Task Tracking Tools

### create_task
Creates a new task and starts time tracking. Auto-pauses any current in-progress task.

| Parameter | Type   | Required | Description                                |
| --------- | ------ | -------- | ------------------------------------------ |
| name      | string | Yes      | Short descriptive task name                |
| force     | bool   | No       | Skip duplicate-name check (default: false) |

**Returns:** Confirmation message with task name, or duplicate warning.

### switch_task
Switches to an existing task. Pauses the current task and resumes the target.

| Parameter | Type   | Required | Description                   |
| --------- | ------ | -------- | ----------------------------- |
| name      | string | Yes      | Name of the task to switch to |

**Returns:** Confirmation with previous task paused, new task active, and latest note if available.

### complete_task
Marks a task as completed and stops time tracking.

| Parameter | Type   | Required | Description                       |
| --------- | ------ | -------- | --------------------------------- |
| name      | string | No       | Task name (default: current task) |

**Returns:** Confirmation with list of paused tasks as next-step suggestions.

### rename_task
Renames an existing task.

| Parameter | Type   | Required | Description       |
| --------- | ------ | -------- | ----------------- |
| oldName   | string | Yes      | Current task name |
| newName   | string | Yes      | New task name     |

### delete_task
Deletes a task. Requires explicit confirmation.

| Parameter | Type   | Required | Description                                      |
| --------- | ------ | -------- | ------------------------------------------------ |
| name      | string | Yes      | Task to delete                                   |
| confirmed | bool   | No       | Must be true to actually delete (default: false) |

### merge_tasks
Merges two tasks, combining notes and time logs into the target.

| Parameter  | Type   | Required | Description                          |
| ---------- | ------ | -------- | ------------------------------------ |
| sourceName | string | Yes      | Task to merge from (will be deleted) |
| targetName | string | Yes      | Task to merge into                   |

### get_current_task
Returns the currently in-progress task with time spent today.

**Returns:** Task name, status, and time spent today, or "no active task" message.

### get_open_tasks
Returns all non-completed, non-archived tasks with status and time.

**Returns:** List of tasks (grouped summary if >20 tasks).

---

## Reminder Tools

### set_reminder
Sets a per-task reminder interval or changes the global default.

| Parameter | Type   | Required | Description                           |
| --------- | ------ | -------- | ------------------------------------- |
| minutes   | double | Yes      | Interval in minutes (1–1440)          |
| taskName  | string | No       | Specific task (null = global default) |

---

## Note Tools

### add_note
Attaches a timestamped note to a task.

| Parameter         | Type   | Required | Description                           |
| ----------------- | ------ | -------- | ------------------------------------- |
| content           | string | Yes      | Note text                             |
| taskName          | string | No       | Target task (default: current task)   |
| storeAsStandalone | bool   | No       | Store without a task (default: false) |

### get_task_notes
Retrieves notes for a task in chronological order.

| Parameter | Type   | Required | Description                                     |
| --------- | ------ | -------- | ----------------------------------------------- |
| taskName  | string | No       | Task name (default: current task or standalone) |

---

## Reflection Tools

### start_reflection
Triggers end-of-day reflection with a daily summary.

**Returns:** Completed tasks, open tasks with time, total time worked, and task count.

### set_priorities
Sets priority order for tasks for the next day.

| Parameter        | Type     | Required | Description             |
| ---------------- | -------- | -------- | ----------------------- |
| orderedTaskNames | string[] | Yes      | Tasks in priority order |
| note             | string   | No       | Optional planning note  |

### get_daily_summary
Returns a summary of today's work without entering reflection mode.

---

## Briefing Tools

### get_morning_briefing
Generates a morning briefing with carry-over tasks, priorities, and task ages.

**Returns:** Greeting type, open tasks, yesterday's priorities, task ages, and standalone notes.

---

## Preference Tools

### save_preferences
Saves or updates all user preferences at once.

| Parameter               | Type   | Required | Description                            |
| ----------------------- | ------ | -------- | -------------------------------------- |
| reminderIntervalMinutes | double | No       | Paused-task reminder interval (1–1440) |
| idleThresholdMinutes    | double | No       | Idle check-in threshold (1–1440)       |
| reflectionTime          | string | No       | HH:mm format or "none"                 |
| wakeWord                | string | No       | Voice activation phrase                |

### update_preferences
Updates a single preference by name.

| Parameter   | Type   | Required | Description                                                                 |
| ----------- | ------ | -------- | --------------------------------------------------------------------------- |
| settingName | string | Yes      | Setting name: reminder_interval, idle_threshold, reflection_time, wake_word |
| value       | string | Yes      | New value                                                                   |

### get_preferences
Returns current preference values as a formatted summary.

---

## Archive Tools

### archive_tasks
Archives old completed tasks to clean up the active list.

| Parameter     | Type | Required | Description                                       |
| ------------- | ---- | -------- | ------------------------------------------------- |
| olderThanDays | int  | No       | Only archive tasks completed more than N days ago |

**Returns:** Count and names of archived tasks.
