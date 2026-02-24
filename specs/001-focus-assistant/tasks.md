# Tasks: Focus Assistant

**Input**: Design documents from `/specs/001-focus-assistant/`
**Prerequisites**: plan.md (required), spec.md (required for user stories)

**Tests**: TDD is mandatory per project constitution (Principle II). Test tasks are included in each phase — tests MUST be written and confirmed to fail before implementation code per Red-Green-Refactor cycle.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Clean Architecture .NET**: `src/FocusAssistant.{Layer}/` at repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, solution structure, and dependency configuration

- [X] T001 Create .NET solution file at `FocusAssistant.sln` and project scaffolding for `src/FocusAssistant.Domain/FocusAssistant.Domain.csproj`, `src/FocusAssistant.Application/FocusAssistant.Application.csproj`, `src/FocusAssistant.Infrastructure/FocusAssistant.Infrastructure.csproj`, `src/FocusAssistant.Cli/FocusAssistant.Cli.csproj`
- [X] T002 Configure project references in `src/FocusAssistant.Cli/FocusAssistant.Cli.csproj`, `src/FocusAssistant.Application/FocusAssistant.Application.csproj`, `src/FocusAssistant.Infrastructure/FocusAssistant.Infrastructure.csproj`: Cli → Application, Infrastructure, Domain; Application → Domain; Infrastructure → Domain
- [X] T003 [P] Add NuGet dependencies: `GitHub.Copilot.SDK` and `Microsoft.Extensions.AI` to `src/FocusAssistant.Cli/FocusAssistant.Cli.csproj`, `Microsoft.Extensions.Hosting` to `src/FocusAssistant.Cli/FocusAssistant.Cli.csproj`, `System.Text.Json` to `src/FocusAssistant.Infrastructure/FocusAssistant.Infrastructure.csproj`
- [X] T004 [P] Create `.editorconfig` and `Directory.Build.props` at repo root for consistent code style and shared build properties
- [X] T005 [P] Create `src/FocusAssistant.Cli/Program.cs` with minimal `Host.CreateDefaultBuilder` setup, DI container, and hosted service registration (placeholder services)
- [X] T085 [P] Create test project scaffolding: `tests/FocusAssistant.Domain.Tests/FocusAssistant.Domain.Tests.csproj`, `tests/FocusAssistant.Application.Tests/FocusAssistant.Application.Tests.csproj`, `tests/FocusAssistant.Infrastructure.Tests/FocusAssistant.Infrastructure.Tests.csproj` with project references to their corresponding source projects
- [X] T086 [P] Add NuGet dependencies to test projects: `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, `NSubstitute` to all test `.csproj` files
- [X] T087 [P] Create Architecture Decision Records: `docs/adr/001-copilot-sdk.md` (AI runtime selection), `docs/adr/002-json-file-persistence.md` (storage strategy), `docs/adr/003-voice-library-selection.md` (wake word / STT / TTS choices), `docs/adr/004-clean-architecture-structure.md` (layer organization)

**Checkpoint**: Solution builds, projects reference each other correctly, test projects reference source projects, `dotnet build` and `dotnet test` succeed

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain entities, value objects, repository interfaces, and shared infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 Create `TaskStatus` value object as an enumeration (InProgress, Paused, Completed) in `src/FocusAssistant.Domain/ValueObjects/TaskStatus.cs`
- [X] T007 [P] Create `TimeLogEntry` value object (start time, end time, duration) in `src/FocusAssistant.Domain/ValueObjects/TimeLogEntry.cs`
- [X] T008 [P] Create `ReminderInterval` value object (duration, per-task override flag) in `src/FocusAssistant.Domain/ValueObjects/ReminderInterval.cs`
- [X] T009 Create `FocusTask` entity with name, status, creation time, time-per-day log, priority ranking, and reminder interval in `src/FocusAssistant.Domain/Entities/FocusTask.cs`
- [X] T010 [P] Create `TaskNote` entity with content, timestamp, and parent task reference in `src/FocusAssistant.Domain/Entities/TaskNote.cs`
- [X] T011 [P] Create `WorkSession` entity with start time, end time, task IDs worked on, and reflection summary in `src/FocusAssistant.Domain/Entities/WorkSession.cs`
- [X] T012 [P] Create `DailyPlan` entity with date, ordered task list, and notes/reminders in `src/FocusAssistant.Domain/Entities/DailyPlan.cs`
- [X] T013 Create `TaskAggregate` root that manages FocusTask lifecycle and enforces invariants (only one task in-progress at a time, auto-pause on switch) in `src/FocusAssistant.Domain/Aggregates/TaskAggregate.cs`
- [X] T014 [P] Create domain events: `TaskCreatedEvent`, `TaskSwitchedEvent`, `TaskCompletedEvent` in `src/FocusAssistant.Domain/Events/`
- [X] T015 Create `ITaskRepository` interface with CRUD operations, find-by-status, and find-by-name in `src/FocusAssistant.Domain/Repositories/ITaskRepository.cs`
- [X] T016 [P] Create `ISessionRepository` interface in `src/FocusAssistant.Domain/Repositories/ISessionRepository.cs`
- [X] T017 [P] Create `IDailyPlanRepository` interface in `src/FocusAssistant.Domain/Repositories/IDailyPlanRepository.cs`
- [X] T018 [P] Create `IUserPreferencesRepository` interface (default reminder interval, idle threshold, reflection time) in `src/FocusAssistant.Domain/Repositories/IUserPreferencesRepository.cs`
- [X] T019 Create `JsonFileStore<T>` generic file persistence helper (read, write, atomic save) in `src/FocusAssistant.Infrastructure/Persistence/JsonFileStore.cs`
- [X] T020 Create `FileTaskRepository` implementing `ITaskRepository` using `JsonFileStore` in `src/FocusAssistant.Infrastructure/Persistence/FileTaskRepository.cs`
- [X] T021 [P] Create `FileSessionRepository` implementing `ISessionRepository` in `src/FocusAssistant.Infrastructure/Persistence/FileSessionRepository.cs`
- [X] T022 [P] Create `FileDailyPlanRepository` implementing `IDailyPlanRepository` in `src/FocusAssistant.Infrastructure/Persistence/FileDailyPlanRepository.cs`
- [X] T023 [P] Create `FileUserPreferencesRepository` implementing `IUserPreferencesRepository` in `src/FocusAssistant.Infrastructure/Persistence/FileUserPreferencesRepository.cs`
- [X] T024 Create `ServiceCollectionExtensions` for registering all Infrastructure services (repositories, voice services) into DI in `src/FocusAssistant.Infrastructure/Extensions/ServiceCollectionExtensions.cs`
- [X] T025 Create `SystemPromptBuilder` that constructs the Copilot system message defining the assistant persona, capabilities, and conversation rules in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`
- [X] T026 Create `CopilotAgentSession` that initializes `CopilotClient`, creates a session with system prompt and tools, handles events, and exposes `SendCommandAsync(string text)` in `src/FocusAssistant.Cli/Agent/CopilotAgentSession.cs`
- [X] T088 Create `UserPreferences` entity with default reminder interval, idle check-in threshold, optional reflection time, and wake word in `src/FocusAssistant.Domain/Entities/UserPreferences.cs`

**TDD**: The following test tasks MUST be written before their corresponding implementation tasks above (Red-Green-Refactor). Write the failing test → implement the entity/service → refactor.

- [X] T089 Write unit tests for `TaskStatus`, `TimeLogEntry`, `ReminderInterval` value objects in `tests/FocusAssistant.Domain.Tests/ValueObjects/`
- [X] T090 Write unit tests for `FocusTask` entity (create, pause, resume, complete, rename, time logging) in `tests/FocusAssistant.Domain.Tests/Entities/FocusTaskTests.cs`
- [X] T091 [P] Write unit tests for `TaskNote` entity in `tests/FocusAssistant.Domain.Tests/Entities/TaskNoteTests.cs`
- [X] T092 [P] Write unit tests for `WorkSession` entity in `tests/FocusAssistant.Domain.Tests/Entities/WorkSessionTests.cs`
- [X] T093 [P] Write unit tests for `DailyPlan` entity in `tests/FocusAssistant.Domain.Tests/Entities/DailyPlanTests.cs`
- [X] T094 [P] Write unit tests for `UserPreferences` entity in `tests/FocusAssistant.Domain.Tests/Entities/UserPreferencesTests.cs`
- [X] T095 Write unit tests for `TaskAggregate` invariants (single in-progress, auto-pause, lifecycle rules) in `tests/FocusAssistant.Domain.Tests/Aggregates/TaskAggregateTests.cs`
- [X] T096 Write integration tests for `JsonFileStore<T>` (read, write, atomic save, concurrent access) in `tests/FocusAssistant.Infrastructure.Tests/Persistence/JsonFileStoreTests.cs`
- [X] T097 Write integration tests for `FileTaskRepository` in `tests/FocusAssistant.Infrastructure.Tests/Persistence/FileTaskRepositoryTests.cs`
- [X] T098 [P] Write integration tests for `FileSessionRepository` in `tests/FocusAssistant.Infrastructure.Tests/Persistence/FileSessionRepositoryTests.cs`
- [X] T099 [P] Write integration tests for `FileDailyPlanRepository` in `tests/FocusAssistant.Infrastructure.Tests/Persistence/FileDailyPlanRepositoryTests.cs`
- [X] T100 [P] Write integration tests for `FileUserPreferencesRepository` in `tests/FocusAssistant.Infrastructure.Tests/Persistence/FileUserPreferencesRepositoryTests.cs`

**Checkpoint**: Domain compiles with all entities and interfaces. Infrastructure compiles with file-based repositories. Copilot agent session can be instantiated. All domain unit tests and infrastructure integration tests pass. `dotnet build` and `dotnet test` succeed across all projects.

---

## Phase 3: User Story 1 — Voice-Based Task Tracking (Priority: P1) 🎯 MVP

**Goal**: User can create tasks, switch between tasks, complete tasks, rename/delete/merge tasks, and query open tasks — all via voice through Copilot tools.

**Independent Test**: Start the CLI, speak task commands via simulated text input, verify tasks are created, switched, completed, and queryable.

### Implementation for User Story 1

- [X] T027 [US1] Create `TaskTrackingService` with methods: CreateTask, SwitchTask, CompleteTask, GetCurrentTask, GetOpenTasks, GetCompletedTasks in `src/FocusAssistant.Application/Services/TaskTrackingService.cs`
- [X] T028 [US1] Create `CreateTaskUseCase` that creates a new task, auto-pauses any current in-progress task, and returns confirmation in `src/FocusAssistant.Application/UseCases/CreateTaskUseCase.cs`
- [X] T029 [US1] Create `SwitchTaskUseCase` that pauses current task, resumes or creates target task, updates time logs in `src/FocusAssistant.Application/UseCases/SwitchTaskUseCase.cs`
- [X] T030 [US1] Create `CompleteTaskUseCase` that marks a task as completed, stops time tracking, and returns confirmation in `src/FocusAssistant.Application/UseCases/CompleteTaskUseCase.cs`
- [X] T031 [P] [US1] Create `RenameTaskUseCase` in `src/FocusAssistant.Application/UseCases/RenameTaskUseCase.cs`
- [X] T032 [P] [US1] Create `DeleteTaskUseCase` (with confirmation flag) in `src/FocusAssistant.Application/UseCases/DeleteTaskUseCase.cs`
- [X] T033 [P] [US1] Create `MergeTasksUseCase` that combines notes and time logs in `src/FocusAssistant.Application/UseCases/MergeTasksUseCase.cs`
- [X] T034 [US1] Create `GetOpenTasksUseCase` that returns all non-completed tasks with status and time spent today in `src/FocusAssistant.Application/UseCases/GetOpenTasksUseCase.cs`
- [X] T035 [US1] Define Copilot tools for US1 (`create_task`, `switch_task`, `complete_task`, `rename_task`, `delete_task`, `merge_tasks`, `get_current_task`, `get_open_tasks`) using `AIFunctionFactory.Create` in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [X] T036 [US1] Register US1 tools in `CopilotAgentSession` session config and update system prompt with task tracking instructions in `src/FocusAssistant.Cli/Agent/CopilotAgentSession.cs`
- [X] T037 [US1] Implement `IVoiceInputService` interface in `src/FocusAssistant.Application/Interfaces/IVoiceInputService.cs` and stub `SpeechToTextService` in `src/FocusAssistant.Infrastructure/Voice/SpeechToTextService.cs` (initially reads from stdin for CLI testing)
- [X] T038 [US1] Implement `IVoiceOutputService` interface in `src/FocusAssistant.Application/Interfaces/IVoiceOutputService.cs` and stub `TextToSpeechService` in `src/FocusAssistant.Infrastructure/Voice/TextToSpeechService.cs` (initially writes to stdout for CLI testing)
- [X] T039 [US1] Create `VoiceListenerService` as `BackgroundService` that loops: listen for wake word → capture speech → send to `CopilotAgentSession` → speak response in `src/FocusAssistant.Cli/HostedServices/VoiceListenerService.cs`
- [X] T040 [US1] Wire up all US1 services and hosted services in `Program.cs` DI registration and update `ServiceCollectionExtensions` in `src/FocusAssistant.Cli/Program.cs`
- [X] T041 [US1] Handle duplicate task name edge case in `src/FocusAssistant.Application/UseCases/CreateTaskUseCase.cs` to check for existing task with same name and return a disambiguation prompt

**TDD**: Write failing tests before implementing use cases above.

- [X] T101 [US1] Write unit tests for `TaskTrackingService` in `tests/FocusAssistant.Application.Tests/Services/TaskTrackingServiceTests.cs`
- [X] T102 [US1] Write unit tests for `CreateTaskUseCase` (create, auto-pause, duplicate name handling) in `tests/FocusAssistant.Application.Tests/UseCases/CreateTaskUseCaseTests.cs`
- [X] T103 [US1] Write unit tests for `SwitchTaskUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/SwitchTaskUseCaseTests.cs`
- [X] T104 [US1] Write unit tests for `CompleteTaskUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/CompleteTaskUseCaseTests.cs`
- [X] T105 [US1] [P] Write unit tests for `RenameTaskUseCase`, `DeleteTaskUseCase`, `MergeTasksUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/`
- [X] T106 [US1] Write unit tests for `GetOpenTasksUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/GetOpenTasksUseCaseTests.cs`

**Checkpoint**: User can run the CLI, type commands (simulated voice), and create/switch/complete/rename/delete/query tasks. Data persists to disk across restarts. All US1 unit tests pass with ≥90% branch coverage.

---

## Phase 4: User Story 2 — Proactive Check-Ins & Reminders (Priority: P2)

**Goal**: The assistant proactively asks what the user is working on after idle periods and reminds about paused tasks at configurable intervals.

**Independent Test**: Start tasks, pause them, wait for configured intervals, and verify the assistant speaks check-in/reminder prompts.

### Implementation for User Story 2

- [ ] T042 [US2] Create `ReminderScheduler` service that tracks idle time and paused-task durations, emits reminder events in `src/FocusAssistant.Application/Services/ReminderScheduler.cs`
- [ ] T043 [US2] Create `SetReminderUseCase` for setting per-task reminder intervals in `src/FocusAssistant.Application/UseCases/SetReminderUseCase.cs`
- [ ] T044 [US2] Create `ReminderBackgroundService` as `BackgroundService` that periodically checks `ReminderScheduler` and sends proactive prompts via `CopilotAgentSession` in `src/FocusAssistant.Cli/HostedServices/ReminderBackgroundService.cs`
- [ ] T045 [US2] Add idle-detection logic in `VoiceListenerService` — track last interaction timestamp and report to `ReminderScheduler` in `src/FocusAssistant.Cli/HostedServices/VoiceListenerService.cs`
- [ ] T046 [US2] Define Copilot tool `set_reminder` for per-task reminder override in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [ ] T047 [US2] Update system prompt with reminder behavior rules: idle check-in, paused-task reminders, no interruption during focus, suggest paused task after completion in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`
- [ ] T048 [US2] Add post-completion suggestion logic: when `CompleteTaskUseCase` runs, query paused tasks and include suggestion in response in `src/FocusAssistant.Application/UseCases/CompleteTaskUseCase.cs`
- [ ] T049 [US2] Wire up `ReminderScheduler` and `ReminderBackgroundService` in DI registration in `src/FocusAssistant.Cli/Program.cs`

**TDD**: Write failing tests before implementing use cases above.

- [ ] T107 [US2] Write unit tests for `ReminderScheduler` (idle detection, reminder intervals, per-task overrides, suppression during focus) in `tests/FocusAssistant.Application.Tests/Services/ReminderSchedulerTests.cs`
- [ ] T108 [US2] Write unit tests for `SetReminderUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/SetReminderUseCaseTests.cs`

**Checkpoint**: Assistant proactively checks in after idle periods, reminds about paused tasks, does not interrupt during focus, and suggests paused tasks after completion. All US2 unit tests pass.

---

## Phase 5: User Story 3 — Task-Related Notes (Priority: P3)

**Goal**: User can dictate notes attached to tasks, notes are read back on task resumption, and notes can be queried by task name.

**Independent Test**: Add notes to a task, pause the task, resume it, and verify notes are read back.

### Implementation for User Story 3

- [ ] T050 [US3] Create `AddNoteUseCase` that attaches a timestamped note to the current or specified task in `src/FocusAssistant.Application/UseCases/AddNoteUseCase.cs`
- [ ] T051 [US3] Create `GetTaskNotesUseCase` that retrieves all notes for a task in chronological order in `src/FocusAssistant.Application/UseCases/GetTaskNotesUseCase.cs`
- [ ] T052 [US3] Update `SwitchTaskUseCase` to read back the most recent note when resuming a previously paused task in `src/FocusAssistant.Application/UseCases/SwitchTaskUseCase.cs`
- [ ] T053 [US3] Handle standalone notes edge case: when no task is active, `AddNoteUseCase` returns prompt asking which task to attach to (or stores as standalone) in `src/FocusAssistant.Application/UseCases/AddNoteUseCase.cs`
- [ ] T054 [US3] Define Copilot tools `add_note` and `get_task_notes` in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [ ] T055 [US3] Update system prompt with note-taking instructions and automatic note readback behavior in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`

**TDD**: Write failing tests before implementing use cases above.

- [ ] T109 [US3] Write unit tests for `AddNoteUseCase` (attach to task, standalone note handling, timestamp) in `tests/FocusAssistant.Application.Tests/UseCases/AddNoteUseCaseTests.cs`
- [ ] T110 [US3] Write unit tests for `GetTaskNotesUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/GetTaskNotesUseCaseTests.cs`

**Checkpoint**: Notes can be added, retrieved, and are automatically read back on task resumption. All US3 unit tests pass.

---

## Phase 6: User Story 4 — End-of-Day Reflection (Priority: P4)

**Goal**: User can trigger an end-of-day reflection that summarizes the day, allows setting priorities for tomorrow, and stores the plan.

**Independent Test**: Work on several tasks during a session, say "let's wrap up", and verify the reflection covers all tasks with time spent and allows priority setting.

### Implementation for User Story 4

- [ ] T056 [US4] Create `ReflectionService` that generates daily summary: completed tasks, open tasks, time per task in `src/FocusAssistant.Application/Services/ReflectionService.cs`
- [ ] T057 [US4] Create `StartReflectionUseCase` that triggers reflection mode and returns the daily summary in `src/FocusAssistant.Application/UseCases/StartReflectionUseCase.cs`
- [ ] T058 [US4] Create `SetPrioritiesUseCase` that saves user's priority ranking for next day as a `DailyPlan` entity in `src/FocusAssistant.Application/UseCases/SetPrioritiesUseCase.cs`
- [ ] T059 [US4] Add scheduled reflection prompt: extend `ReminderBackgroundService` to check configured reflection time and send prompt in `src/FocusAssistant.Cli/HostedServices/ReminderBackgroundService.cs`
- [ ] T060 [US4] Define Copilot tools `start_reflection`, `set_priorities`, `get_daily_summary` in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [ ] T061 [US4] Update system prompt with reflection mode behavior: structured walkthrough, priority setting, confirmation in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`

**TDD**: Write failing tests before implementing use cases above.

- [ ] T111 [US4] Write unit tests for `ReflectionService` (daily summary, task time aggregation) in `tests/FocusAssistant.Application.Tests/Services/ReflectionServiceTests.cs`
- [ ] T112 [US4] Write unit tests for `StartReflectionUseCase` and `SetPrioritiesUseCase` in `tests/FocusAssistant.Application.Tests/UseCases/`

**Checkpoint**: End-of-day reflection works via both user trigger and scheduled prompt. Priorities are saved. All US4 unit tests pass.

---

## Phase 7: User Story 5 — Next-Day Task Resumption (Priority: P5)

**Goal**: The assistant provides a morning briefing with open tasks, yesterday's priorities, and notes when starting a new session.

**Independent Test**: End a session with open tasks and priorities, start a new session, and verify the morning briefing is complete and accurate.

### Implementation for User Story 5

- [ ] T062 [US5] Create `GetMorningBriefingUseCase` that loads previous day's plan, open tasks, and task ages in `src/FocusAssistant.Application/UseCases/GetMorningBriefingUseCase.cs`
- [ ] T063 [US5] Add session detection logic: on startup, detect if this is a new day or multi-day gap and trigger briefing in `src/FocusAssistant.Application/Services/TaskTrackingService.cs`
- [ ] T064 [US5] Update `SwitchTaskUseCase` to read back notes from prior day when resuming a task from a previous session in `src/FocusAssistant.Application/UseCases/SwitchTaskUseCase.cs`
- [ ] T065 [US5] Define Copilot tool `get_morning_briefing` in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [ ] T066 [US5] Update system prompt with morning briefing rules: auto-trigger on session start, greet user, read priorities in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`
- [ ] T067 [US5] Wire session start detection in `VoiceListenerService` or `CopilotAgentSession` to automatically invoke morning briefing on first interaction of a new session in `src/FocusAssistant.Cli/HostedServices/VoiceListenerService.cs`

**TDD**: Write failing tests before implementing use cases above.

- [ ] T113 [US5] Write unit tests for `GetMorningBriefingUseCase` (single-day, multi-day gap, no prior session) in `tests/FocusAssistant.Application.Tests/UseCases/GetMorningBriefingUseCaseTests.cs`

**Checkpoint**: Morning briefing fires on new-day session start with complete carry-over data. All US5 unit tests pass.

---

## Phase 8: User Story 6 — Always-Listening Voice Interface (Priority: P6)

**Goal**: The assistant listens continuously via wake-word detection, processes spoken commands, and responds with synthesized speech.

**Independent Test**: Run the assistant, speak the wake word followed by commands at various intervals over an extended period, verify reliable detection and response.

### Implementation for User Story 6

- [ ] T068 [US6] Implement `WakeWordDetector` with a real wake-word detection library (e.g., Porcupine or OpenWakeWord) that listens to microphone and emits detection events in `src/FocusAssistant.Infrastructure/Voice/WakeWordDetector.cs`
- [ ] T069 [US6] Implement real `SpeechToTextService` using a speech recognition engine (e.g., Vosk for offline or Azure Speech SDK) replacing the stdin stub in `src/FocusAssistant.Infrastructure/Voice/SpeechToTextService.cs`
- [ ] T070 [US6] Implement real `TextToSpeechService` using a TTS engine (e.g., `espeak` on Linux or Azure Speech SDK) replacing the stdout stub in `src/FocusAssistant.Infrastructure/Voice/TextToSpeechService.cs`
- [ ] T071 [US6] Update `VoiceListenerService` to integrate wake-word detection → speech capture → Copilot → TTS pipeline with barge-in support (stop speaking when user starts) in `src/FocusAssistant.Cli/HostedServices/VoiceListenerService.cs`
- [ ] T072 [US6] Add graceful shutdown and long-running stability handling: ensure `CopilotClient` auto-restarts on crash, voice services recover from audio device errors in `src/FocusAssistant.Cli/HostedServices/VoiceListenerService.cs`
- [ ] T073 [US6] Update system prompt with voice-specific clarification rules: ask for clarification on low-confidence transcriptions in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`

**TDD**: Write failing tests for voice service contracts.

- [ ] T114 [US6] Write integration tests for `WakeWordDetector`, `SpeechToTextService`, `TextToSpeechService` contracts in `tests/FocusAssistant.Infrastructure.Tests/Voice/`

**Checkpoint**: Full voice pipeline operational: wake word → speech-to-text → Copilot → text-to-speech, running reliably for extended sessions. All voice integration tests pass.

---

## Phase 9: First-Use Onboarding

**Purpose**: Guide the user through initial preference setup on first launch
**Depends on**: Phase 3 (US1) — T074 modifies `TaskTrackingService` created in T027

- [ ] T074 Create first-use detection logic: check if preferences file exists; if not, flag onboarding needed in `src/FocusAssistant.Application/Services/TaskTrackingService.cs`
- [ ] T075 Define Copilot tool `save_preferences` for persisting user preferences (default reminder interval, idle threshold, reflection time) in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [ ] T076 Update system prompt with onboarding instructions: on first use, walk user through preference setup before normal operation in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`
- [ ] T077 Define Copilot tool `update_preferences` for changing preferences at any time via voice in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`

**TDD**: Write failing tests for onboarding flow.

- [ ] T115 Write unit tests for first-use detection and preference persistence in `tests/FocusAssistant.Application.Tests/Services/OnboardingTests.cs`

**Checkpoint**: First launch triggers guided preference setup; preferences persist and are changeable at any time. All onboarding tests pass.

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Error handling, edge cases, robustness improvements

- [ ] T078 [P] Add error handling and logging throughout all use cases and services using `ILogger<T>` in `src/FocusAssistant.Application/`
- [ ] T079 [P] Add crash recovery: ensure `JsonFileStore` uses atomic writes (write-to-temp then rename) to prevent data corruption in `src/FocusAssistant.Infrastructure/Persistence/JsonFileStore.cs`
- [ ] T080 Handle large task list edge case: when >20 tasks, `GetOpenTasksUseCase` groups by status and summarizes rather than listing all in `src/FocusAssistant.Application/UseCases/GetOpenTasksUseCase.cs`
- [ ] T081 [P] Handle escalating reminder suppression: limit reminders for tasks the user has been reminded about but not acted on (check-in once then wait for session end) in `src/FocusAssistant.Application/Services/ReminderScheduler.cs`
- [ ] T082 [P] Add archive/bulk-delete support: create `ArchiveTasksUseCase` for cleaning up old completed tasks in `src/FocusAssistant.Application/UseCases/ArchiveTasksUseCase.cs`
- [ ] T083 Define Copilot tool `archive_tasks` for bulk archival in `src/FocusAssistant.Cli/Agent/ToolDefinitions.cs`
- [ ] T084 Final review of system prompt for completeness: ensure all edge cases, conversation rules, and tool descriptions are documented in `src/FocusAssistant.Cli/Agent/SystemPromptBuilder.cs`
- [ ] T116 [P] Add input validation and sanitization for all external input (voice-transcribed text, CLI input) at the application service boundary in `src/FocusAssistant.Application/Services/`
- [ ] T117 [P] Document public APIs, domain models, and key architectural decisions in `docs/` (domain model reference, tool API catalog, system prompt design notes)
- [ ] T118 [P] Configure default data directory `~/.focus-assistant/data/` with environment variable override `FOCUS_ASSISTANT_DATA_DIR` in `src/FocusAssistant.Infrastructure/Persistence/JsonFileStore.cs`

**Checkpoint**: All edge cases handled, data is crash-safe, logging is comprehensive, archive support is available. Input validation in place. Documentation complete.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — this is the MVP
- **User Story 2 (Phase 4)**: Depends on Foundational + US1 (needs task lifecycle in place)
- **User Story 3 (Phase 5)**: Depends on Foundational + US1 (needs tasks to attach notes to)
- **User Story 4 (Phase 6)**: Depends on Foundational + US1 (needs task data for reflection)
- **User Story 5 (Phase 7)**: Depends on US4 (needs DailyPlan created during reflection)
- **User Story 6 (Phase 8)**: Depends on US1 (needs CLI pipeline working to add real voice)
- **Onboarding (Phase 9)**: Depends on US1/Phase 3 (modifies TaskTrackingService from T027); recommend after US2 (reminder prefs relevant)
- **Polish (Phase 10)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: After Foundational — no story dependencies. **MVP target.**
- **US2 (P2)**: After US1 — needs task lifecycle operations
- **US3 (P3)**: After US1 — needs tasks to exist; can run parallel with US2
- **US4 (P4)**: After US1 — needs task data; can run parallel with US2/US3
- **US5 (P5)**: After US4 — needs DailyPlan from reflection
- **US6 (P6)**: After US1 — replaces stubs with real voice; can run parallel with US2-US5

### Within Each User Story

- Application use cases before tool definitions
- Tool definitions before system prompt updates
- Core implementation before edge case handling
- Story complete and testable before moving to next priority

### Parallel Opportunities

- All Foundational tasks marked [P] can run in parallel (value objects, entities, repositories)
- US2, US3, US4 can proceed in parallel after US1 is complete (different files/concerns)
- US6 can proceed in parallel with US2-US5 (voice concerns are separate from business logic)
- All Polish tasks marked [P] can run in parallel

---

## Parallel Example: Foundational Phase

```bash
# Launch all value objects in parallel:
Task T007: "Create TimeLogEntry value object"
Task T008: "Create ReminderInterval value object"

# Launch all entities in parallel (after value objects):
Task T010: "Create TaskNote entity"
Task T011: "Create WorkSession entity"
Task T012: "Create DailyPlan entity"

# Launch all repository interfaces in parallel:
Task T016: "Create ISessionRepository"
Task T017: "Create IDailyPlanRepository"
Task T018: "Create IUserPreferencesRepository"

# Launch all file repository implementations in parallel:
Task T021: "Create FileSessionRepository"
Task T022: "Create FileDailyPlanRepository"
Task T023: "Create FileUserPreferencesRepository"
```

---

## Parallel Example: After US1 Complete

```bash
# These can all start simultaneously:
Phase 4 (US2): T042 "Create ReminderScheduler"
Phase 5 (US3): T050 "Create AddNoteUseCase"
Phase 6 (US4): T056 "Create ReflectionService"
Phase 8 (US6): T068 "Implement WakeWordDetector"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (including test project scaffolding and ADRs)
2. Complete Phase 2: Foundational + domain/infrastructure tests (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 + use case tests (TDD: write tests first)
4. **STOP and VALIDATE**: Run full test suite, test task tracking via CLI text input
5. Deploy/demo if ready — this is the MVP

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready
2. Add US1 → Test independently → **MVP!** (task tracking via CLI)
3. Add US2 → Test independently → Proactive reminders working
4. Add US3 → Test independently → Notes attached and recalled
5. Add US4 → Test independently → End-of-day reflection working
6. Add US5 → Test independently → Morning briefing working
7. Add US6 → Test independently → Full voice pipeline operational
8. Add Onboarding + Polish → Production-ready

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (MVP — highest priority)
3. Once US1 is done:
   - Developer A: User Story 2 (reminders)
   - Developer B: User Story 3 (notes)
   - Developer C: User Story 4 (reflection) + User Story 6 (voice)
4. Once US4 is done:
   - Developer A: User Story 5 (morning briefing)
5. Onboarding + Polish as a team

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable (except US5 needing US4)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- The Copilot SDK tools act as the interface between AI and business logic — keep tool implementations thin (delegate to use cases)
- For MVP (US1), voice I/O is stubbed (stdin/stdout) — real voice comes in US6
- File paths use `~/.focus-assistant/data/` for persistence by default
