# Domain Model Reference

## Entities

### FocusTask
The core work unit being tracked.

| Property         | Type                 | Description                                   |
| ---------------- | -------------------- | --------------------------------------------- |
| Id               | Guid                 | Unique identifier                             |
| Name             | string               | Short descriptive name                        |
| Status           | TaskStatus           | Current lifecycle state                       |
| CreatedAt        | DateTime             | When the task was created (UTC)               |
| PriorityRanking  | int?                 | Priority for tomorrow (set during reflection) |
| ReminderInterval | ReminderInterval?    | Per-task reminder override                    |
| TimeLogs         | List\<TimeLogEntry\> | Time tracking entries                         |
| NoteIds          | List\<Guid\>         | References to attached notes                  |

**Lifecycle methods:** `Start()`, `Pause()`, `Complete()`, `Archive()`, `Rename()`, `SetPriority()`, `SetReminderInterval()`, `ClearReminderInterval()`, `AddNoteId()`, `MergeFrom()`, `GetTimeSpentToday()`, `GetTotalTimeSpent()`

### TaskNote
A timestamped note attached to a task (or standalone).

| Property  | Type     | Description                               |
| --------- | -------- | ----------------------------------------- |
| Id        | Guid     | Unique identifier                         |
| Content   | string   | Note text                                 |
| CreatedAt | DateTime | When the note was created (UTC)           |
| TaskId    | Guid?    | Owner task ID (null for standalone notes) |

### WorkSession
Represents a work period (typically a day).

| Property          | Type         | Description                   |
| ----------------- | ------------ | ----------------------------- |
| Id                | Guid         | Unique identifier             |
| StartTime         | DateTime     | Session start (UTC)           |
| EndTime           | DateTime?    | Session end (UTC)             |
| TaskIdsWorkedOn   | List\<Guid\> | Tasks touched in this session |
| ReflectionSummary | string?      | End-of-day reflection text    |

### DailyPlan
A priority-ordered plan for a given date.

| Property       | Type           | Description                          |
| -------------- | -------------- | ------------------------------------ |
| Id             | Guid           | Unique identifier                    |
| Date           | DateOnly       | The date this plan covers            |
| TaskPriorities | List\<Guid\>   | Ordered list of task IDs by priority |
| Notes          | List\<string\> | Planning notes                       |

### UserPreferences
User-configurable settings, created during onboarding.

| Property                | Type             | Description                                              |
| ----------------------- | ---------------- | -------------------------------------------------------- |
| Id                      | Guid             | Unique identifier                                        |
| DefaultReminderInterval | ReminderInterval | How often to remind about paused tasks (default: 60 min) |
| IdleCheckInThreshold    | TimeSpan         | How long before idle check-in (default: 15 min)          |
| AutomaticReflectionTime | TimeOnly?        | Scheduled daily reflection time (null = disabled)        |
| WakeWord                | string           | Voice activation phrase (default: "Hey Focus")           |

---

## Value Objects

### TaskStatus (enum)
- `InProgress` — Currently being worked on
- `Paused` — Temporarily set aside
- `Completed` — Finished
- `Archived` — Old completed task, removed from active list

### TimeLogEntry
Immutable record of a time period spent on a task.

| Property  | Type      | Description                                      |
| --------- | --------- | ------------------------------------------------ |
| StartTime | DateTime  | When work started                                |
| EndTime   | DateTime? | When work stopped (null = still active)          |
| Duration  | TimeSpan  | Computed: end - start (or now - start if active) |
| IsActive  | bool      | True if EndTime is null                          |

### ReminderInterval
How often to be reminded about a paused task.

| Property          | Type     | Description                    |
| ----------------- | -------- | ------------------------------ |
| Duration          | TimeSpan | The reminder interval          |
| IsPerTaskOverride | bool     | True if set on a specific task |

**Factory methods:** `FromMinutes()`, `FromHours()`, `ReminderInterval.Default` (60 min)

---

## Aggregates

### TaskAggregate
Root aggregate managing the task lifecycle. Enforces the invariant that **only one task can be in-progress at a time** — creating or switching to a task automatically pauses the current one.

**Key methods:** `CreateTask()`, `SwitchToTask()`, `CompleteTask()`, `CompleteCurrentTask()`, `RenameTask()`, `DeleteTask()`, `MergeTasks()`, `GetOpenTasks()`, `GetPausedTasks()`, `GetCompletedTasks()`, `FindTaskByName()`, `HasTaskWithName()`

---

## Domain Events

| Event              | Fields                                 | When                 |
| ------------------ | -------------------------------------- | -------------------- |
| TaskCreatedEvent   | TaskId, TaskName                       | New task created     |
| TaskSwitchedEvent  | PreviousTaskId, NewTaskId, NewTaskName | User switched tasks  |
| TaskCompletedEvent | TaskId, TaskName                       | Task marked complete |

---

## Repository Interfaces

| Interface                  | Entity          | Key Methods                                                    |
| -------------------------- | --------------- | -------------------------------------------------------------- |
| ITaskRepository            | FocusTask       | GetById, GetByName, GetAll, GetByStatus, Save, Delete, SaveAll |
| ISessionRepository         | WorkSession     | GetLatest, GetByDate, Save                                     |
| IDailyPlanRepository       | DailyPlan       | GetByDate, GetLatest, Save                                     |
| IUserPreferencesRepository | UserPreferences | Get, Save, Exists                                              |
| INoteRepository            | TaskNote        | GetByTaskId, GetStandaloneNotes, Save                          |
