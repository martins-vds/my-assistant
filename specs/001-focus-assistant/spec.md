# Feature Specification: Focus Assistant — Personal Task & Context Tracker

**Feature Branch**: `001-focus-assistant`  
**Created**: 2026-02-23  
**Status**: Draft  
**Input**: User description: "Build me a personal assistant to help me focus on my work. I struggle with a lot of context switching in my tech work. I start multiple tasks in parallel, switch between then a lot and often loose track of them. My assistant must be able to keep track my tasks, ask me what i am currently working on to help me not lose track of my ongoing tasks, remind me of tasks that i have started. My assistant must also be able to takes notes related to a certain task. At the end of my day, the assistant must help reflect on my progress for the day. The next day, my assistant must be able to tell me which tasks i still have to complete. i must be able to talk to my assistant and my assitant my be able to listen constantly and ask me questions according to the requirements above"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Voice-Based Task Tracking (Priority: P1)

As a tech worker who frequently context-switches, I want to tell my assistant what I'm working on using my voice so that my tasks are tracked without interrupting my workflow.

I speak naturally to the assistant throughout my day. When I say something like "I'm starting work on the login page bug," the assistant creates or updates a task. When I switch to something else and say "Now I'm looking at the deployment pipeline," it records that switch and keeps the previous task as in-progress. At any point I can ask "What am I working on?" or "What tasks do I have open?" and get an immediate spoken answer.

**Why this priority**: This is the core value proposition — tracking tasks through voice so the user never loses track of what they're doing. Without this, no other feature matters.

**Independent Test**: Can be fully tested by speaking task starts and switches to the assistant, then asking it to list current and open tasks. Delivers immediate value by making context switches visible.

**Acceptance Scenarios**:

1. **Given** the assistant is listening, **When** I say "I'm starting work on the API refactor," **Then** the assistant creates a new task called "API refactor" with status "in-progress" and confirms verbally.
2. **Given** I have a task "API refactor" in-progress, **When** I say "Switching to review pull requests," **Then** the assistant creates a new task "Review pull requests" as in-progress, moves "API refactor" to "paused," and confirms the switch verbally.
3. **Given** I have three open tasks, **When** I ask "What are my current tasks?", **Then** the assistant reads back all open tasks with their statuses (in-progress, paused) and how long I've spent on each today.
4. **Given** the assistant is listening, **When** I say "I finished the login page bug," **Then** the assistant marks that task as "completed" and confirms.
5. **Given** the assistant misheard and created a task called "API defector," **When** I say "Rename that task to API refactor," **Then** the assistant renames the task and confirms the correction.
6. **Given** I have a duplicate or accidental task, **When** I say "Delete the task called API defector," **Then** the assistant asks for confirmation and deletes it upon approval.

---

### User Story 2 — Proactive Check-Ins & Reminders (Priority: P2)

As someone who loses track of paused tasks, I want the assistant to proactively ask me about my progress and remind me of tasks I've left unfinished so I don't forget about them.

The assistant periodically checks in with me. If I've been silent for a while, it asks what I'm working on. If a task has been paused for a configurable amount of time, it reminds me about it. It doesn't interrupt me during deep focus but gently nudges when there's a natural pause.

**Why this priority**: Proactive reminders are what distinguish this from a simple task list. This feature directly addresses the user's core problem of forgetting started tasks.

**Independent Test**: Can be tested by starting tasks, pausing them, and waiting for the assistant to remind about them. Delivers value by preventing tasks from being forgotten.

**Acceptance Scenarios**:

1. **Given** I have been idle (no voice interaction) for more than 15 minutes, **When** the idle threshold is reached, **Then** the assistant asks "What are you working on right now?" and waits for my response.
2. **Given** task "Database migration" has been paused for longer than its reminder interval (per-task override or default), **When** the reminder threshold is reached, **Then** the assistant says "You started 'Database migration' earlier — are you still planning to get back to it today?" and continues reminding at the configured interval until the user acts on the task.
3. **Given** I'm in a focused session on a task, **When** I've been actively working on the same task without switching, **Then** the assistant does not interrupt me.
4. **Given** I have tasks paused from earlier today, **When** I complete my current task, **Then** the assistant suggests which paused task to return to.
5. **Given** I pause a task, **When** I say "Remind me about this in 2 hours," **Then** the assistant sets a per-task reminder interval of 2 hours for that task, overriding the default.

---

### User Story 3 — Task-Related Notes (Priority: P3)

As a tech worker, I want to dictate notes about a specific task so that when I return to it I remember where I left off and what I was thinking.

While working on a task, I can say "Note: the issue is in the authentication middleware" and that note gets attached to the current task. When I resume a paused task, the assistant reads back my most recent notes so I can pick up where I left off.

**Why this priority**: Notes are essential for reducing the cost of context switching. Knowing what you were doing and thinking when you paused a task dramatically speeds up resumption.

**Independent Test**: Can be tested by adding notes to a task, pausing it, resuming it, and verifying the notes are read back. Delivers value by preserving task context.

**Acceptance Scenarios**:

1. **Given** I'm working on task "API refactor," **When** I say "Note: need to update the response schema for the /users endpoint," **Then** the assistant confirms the note and attaches it to the "API refactor" task with a timestamp.
2. **Given** task "API refactor" has 3 notes, **When** I resume work on this task, **Then** the assistant reads back the most recent note and offers to read earlier notes.
3. **Given** I want to review notes for a specific task, **When** I say "What are my notes on the API refactor?", **Then** the assistant reads all notes for that task in chronological order.
4. **Given** I'm not currently working on any task, **When** I say "Note: remember to check the staging environment," **Then** the assistant asks which task to attach it to, or creates a standalone note.

---

### User Story 4 — End-of-Day Reflection (Priority: P4)

As a tech worker finishing my day, I want the assistant to help me reflect on what I accomplished, what's still open, and what I should prioritize tomorrow so I can close out my day with clarity.

At the end of the workday, I tell the assistant I'm wrapping up or it prompts me at a configured time. It walks me through a reflection: what tasks I completed, what's still open, how much time I spent on different tasks, and asks me to set priorities for the next day.

**Why this priority**: Reflection helps build awareness of context-switching patterns and ensures nothing falls through the cracks overnight.

**Independent Test**: Can be tested by completing and pausing several tasks during a session, then triggering the end-of-day reflection. Delivers value by providing a structured daily summary.

**Acceptance Scenarios**:

1. **Given** I say "Let's wrap up for the day," **When** the assistant enters reflection mode, **Then** it summarizes: tasks completed today, tasks still in progress, tasks paused, and approximate time spent on each.
2. **Given** I have 3 open tasks at end of day, **When** the reflection is in progress, **Then** the assistant asks me to rank which tasks I want to tackle first tomorrow.
3. **Given** I set priorities during reflection, **When** the reflection is complete, **Then** the assistant confirms my plan for tomorrow and stores it.
4. **Given** the user has configured automatic reflection time (e.g., 5:30 PM), **When** that time is reached, **Then** the assistant prompts "It's almost end of day — would you like to do your daily reflection?"

---

### User Story 5 — Next-Day Task Resumption (Priority: P5)

As a tech worker starting a new day, I want the assistant to brief me on what's still open from yesterday and what I planned to prioritize so I can start my day with focus.

When I greet the assistant or start a new work session, it provides a morning briefing: tasks carried over from the previous day, the priorities I set during reflection, and any reminders I left for myself.

**Why this priority**: Starting the day with a clear picture prevents the "where was I?" problem and reduces morning ramp-up time.

**Independent Test**: Can be tested by ending a day with open tasks and priorities, then starting a new session and verifying the briefing. Delivers value by eliminating morning disorientation.

**Acceptance Scenarios**:

1. **Given** I had 3 open tasks and set priorities yesterday, **When** I say "Good morning" or start a new session, **Then** the assistant greets me and reads my top priorities for the day.
2. **Given** I left notes on a paused task yesterday, **When** I decide to resume that task, **Then** the assistant reads back my last notes from the previous day.
3. **Given** multiple days have passed since my last session, **When** I start a new session, **Then** the assistant summarizes all outstanding tasks across all previous days, noting how long each has been open.

---

### User Story 6 — Always-Listening Voice Interface (Priority: P6)

As a user, I want the assistant to be always listening so I can interact with it hands-free at any time without needing to press a button or open an app.

The assistant runs continuously and listens for my voice. It can distinguish between commands directed at it and background noise or conversations. It responds conversationally and naturally.

**Why this priority**: The always-on voice interface is the delivery mechanism for all other features. It's ranked P6 because the above stories define WHAT the system does, while this defines HOW the user interacts with it. Implementation of the voice channel can evolve (wake word, continuous, push-to-talk) without changing the core logic.

**Independent Test**: Can be tested by leaving the assistant running and speaking to it at various intervals. Delivers value by enabling hands-free, low-friction interaction.

**Acceptance Scenarios**:

1. **Given** the assistant is running, **When** I say the wake word followed by a command, **Then** it correctly interprets and responds within 3 seconds.
2. **Given** there is background noise (music, other people talking), **When** I say the wake word followed by a command, **Then** it activates only on the wake word and correctly processes the command.
3. **Given** the assistant is listening, **When** I have a conversation with a colleague nearby without using the wake word, **Then** the assistant does not interpret that conversation as commands.
4. **Given** the assistant has been running for 8+ hours, **When** I speak a command, **Then** it responds just as reliably as at the start of the session.

---

### Edge Cases

- What happens when the user starts a task with the same name as an existing task? The assistant should ask if they mean the existing task or want to create a new one.
- What happens when the user gives a command while the assistant is reading back information? The assistant should stop speaking and listen to the new command.
- What happens when the assistant doesn't understand a spoken command? It should ask for clarification rather than guessing.
- What happens when the assistant mishears a task name (speech-to-text error)? The user can rename, delete, or merge the incorrectly created task via voice. Deletion requires verbal confirmation to prevent accidental data loss.
- What happens if the user doesn't interact for several hours (e.g., in meetings without voice access)? The assistant should not bombard with reminders; it should gently check in once and wait.
- What happens when there are no open tasks? The assistant should acknowledge a clean slate and ask if the user wants to start something new.
- What happens if the user creates many tasks (20+) and asks for a summary? The assistant should group or prioritize rather than reading an overwhelming list.
- What happens on first use before preferences are set? The assistant guides the user through initial setup (default reminder interval, idle threshold, optional reflection time) before normal operation begins.
- What happens if the application crashes or restarts? All task data and notes must be persisted and recoverable.
- What happens when the user wants to clean up old tasks? The assistant must support explicit delete or archive commands (e.g., "Archive all completed tasks older than a month").

## Requirements *(mandatory)*

### Functional Requirements

#### Task Management
- **FR-001**: System MUST allow users to create tasks via voice by describing what they are starting to work on.
- **FR-002**: System MUST track task status with at minimum the following states: in-progress, paused, completed, archived.
- **FR-003**: System MUST automatically pause the current task when the user starts or switches to a different task.
- **FR-004**: System MUST allow users to explicitly mark tasks as completed via voice.
- **FR-005**: System MUST allow users to query their current task, all open tasks, and completed tasks via voice.
- **FR-006**: System MUST track approximate time spent on each task per day.
- **FR-007**: System MUST persist all task data across sessions and application restarts.
- **FR-032**: System MUST allow users to rename a task via voice (e.g., "Rename that task to API refactor").
- **FR-033**: System MUST allow users to delete a task via voice (e.g., "Delete the task called API defector"), with a verbal confirmation before permanent deletion.
- **FR-034**: System MUST allow users to merge two tasks via voice (e.g., "Merge 'API defector' into 'API refactor'"), combining their notes and time logs into the target task.

#### Proactive Check-Ins & Reminders
- **FR-008**: System MUST proactively ask the user what they are working on after a configurable idle period (default: 15 minutes with no voice interaction).
- **FR-009**: System MUST remind the user about paused tasks at a configurable interval. Users can set a per-task reminder interval (e.g., "remind me about this in 2 hours"). When no per-task interval is specified, the system MUST use the user's configured default reminder interval (initial default: 1 hour). Reminders repeat at the configured interval until the user acts on the task or ends the session.
- **FR-010**: System MUST NOT interrupt the user while they are actively engaged on a single task (no context switches detected).
- **FR-011**: System MUST suggest returning to a paused task when the user completes their current task.
- **FR-031**: On first use, system MUST guide the user through an initial preference setup that includes at minimum: default reminder interval for paused tasks, idle check-in threshold, and optional end-of-day reflection time. These preferences MUST be changeable at any time via voice command (e.g., "Set my default reminder to 2 hours").

#### Task Notes
- **FR-012**: System MUST allow users to dictate notes and attach them to the currently active task.
- **FR-013**: System MUST timestamp all notes.
- **FR-014**: System MUST read back the most recent note when a user resumes a previously paused task.
- **FR-015**: System MUST allow users to request all notes for a specific task by name.
- **FR-016**: System MUST handle notes spoken when no task is active by asking the user which task to attach the note to, or storing it as a standalone note.

#### End-of-Day Reflection
- **FR-017**: System MUST provide an end-of-day reflection when triggered by the user (e.g., "Let's wrap up").
- **FR-018**: System MUST support an optional configurable automatic reflection prompt at a scheduled time.
- **FR-019**: End-of-day reflection MUST include: tasks completed today, tasks still open, approximate time spent per task.
- **FR-020**: System MUST allow the user to set task priorities for the next day during reflection.
- **FR-021**: System MUST store the next-day plan for recall the following morning.

#### Next-Day Resumption
- **FR-022**: System MUST provide a morning briefing when a new session begins, including open tasks and priorities from the previous day.
- **FR-023**: System MUST detect when multiple days have passed and summarize all outstanding tasks with age information.
- **FR-024**: System MUST read back previous notes when the user resumes a task from a prior day.

#### Voice Interface
- **FR-025**: System MUST listen continuously for a wake word (default: "Hey Focus") without requiring a physical button press. Upon detecting the wake word, the system enters active listening mode to capture the user's command. The wake word is configurable via UserPreferences.
- **FR-026**: System MUST use wake-word detection to distinguish between commands directed at the assistant and background speech/noise. Only audio following wake-word detection is transcribed and interpreted.
- **FR-027**: System MUST respond to commands verbally (spoken output) and operate reliably during extended sessions (8+ hours).
- **FR-028**: System MUST stop speaking and listen when the user begins speaking mid-response (barge-in). Implementation: TTS output is monitored alongside microphone input; when voice activity is detected during TTS playback, TTS stops immediately and the system enters active listening mode.
- **FR-029**: System MUST ask for clarification when it cannot confidently interpret a command.
- **FR-030**: System MUST retain all task data (completed, paused, and in-progress) indefinitely until the user explicitly deletes or archives tasks. No automatic purging or expiration.

### Key Entities

- **FocusTask**: Represents a unit of work (named "FocusTask" to avoid ambiguity with system tasks). Key attributes: name/description, status (in-progress, paused, completed, archived), creation time, time-per-day log, associated notes, priority ranking, per-task reminder interval.
- **TaskNote**: A timestamped piece of context attached to a FocusTask. Key attributes: content (transcribed text), timestamp, optional parent task reference (null for standalone notes).
- **WorkSession**: A continuous period of interaction (typically one workday). Key attributes: start time, end time, task IDs worked on, reflection summary. Lifecycle is implicit — created on service startup, ended on shutdown or reflection.
- **DailyPlan**: The user's prioritized task list generated during end-of-day reflection. Key attributes: date for, ordered list of task IDs, notes/reminders.
- **UserPreferences**: Configuration set during onboarding and adjustable at any time. Key attributes: default reminder interval, idle check-in threshold, optional automatic reflection time, wake word (default: "Hey Focus").

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: User can create a task, switch tasks, and query open tasks entirely by voice in under 5 seconds per interaction.
- **SC-002**: 90% of spoken commands are correctly interpreted and acted upon on the first attempt. *(Measured via command success/failure logging in the application.)*
- **SC-003**: User reports no forgotten or lost tasks after one full week of daily use. *(Qualitative — validated via user feedback.)*
- **SC-004**: Paused tasks are surfaced via reminders within the configured time window 100% of the time.
- **SC-005**: End-of-day reflection takes less than 5 minutes and covers all tasks worked on that day.
- **SC-006**: Morning briefing accurately recalls all open tasks and priorities from the previous session.
- **SC-007**: Notes are correctly attached to the right task at least 95% of the time.
- **SC-008**: The assistant runs continuously for a full 8-hour workday without crashes or degraded responsiveness.
- **SC-009**: User self-reports a reduction in context-switching anxiety and task-tracking overhead after one week of use. *(Qualitative — validated via user feedback.)*

## Clarifications

### Session 2026-02-23

- Q: How does the assistant know when it's being spoken to (wake word, continuous intent classification, or hybrid)? → A: Wake word — user says a trigger phrase (e.g., "Hey Focus") before each command.
- Q: How long is completed task data retained? → A: Manual cleanup only — user explicitly deletes or archives old tasks; no automatic purging.
- Q: What happens after the first reminder for a paused task if the user doesn't act on it? → A: Configurable repeat interval — user can set per-task reminders (e.g., "remind me about this in 2 hours"). A default reminder interval applies when no per-task interval is set. On first use, the assistant asks the user to configure preferences including default reminder interval.
- Q: Can the user correct, rename, delete, or merge tasks created by mistake or misheard by voice recognition? → A: Yes — support rename, delete, and merge commands for tasks via voice.
- Q: What is the target platform for the assistant? → A: CLI + background service on Linux. Built with .NET using the [Copilot SDK](https://github.com/github/copilot-sdk). A UI will be added later.

### Session 2026-02-23 (Technical Constraints)

- SDK: [Copilot SDK for .NET](https://github.com/github/copilot-sdk)
- Architecture: Clean Architecture with Domain-Driven Design (DDD)
- Delivery: CLI with a background process (voice listener daemon). UI to be added later.
- Dependency injection throughout
- Persistence: Local disk initially (file-based), designed to migrate to a database later
- Platform: Linux (primary)

## Technical Constraints

- **TC-001**: System MUST be built using .NET with the [Copilot SDK](https://github.com/github/copilot-sdk).
- **TC-002**: System MUST follow Clean Architecture (presentation/application/domain/infrastructure layers with dependency inversion).
- **TC-003**: System MUST apply Domain-Driven Design (DDD) — domain entities, value objects, aggregates, and repository abstractions in the domain layer.
- **TC-004**: System MUST use dependency injection for all cross-cutting concerns and layer dependencies.
- **TC-005**: Initial delivery MUST be a CLI application with a background service/daemon for continuous voice listening. A graphical UI will be added in a future iteration.
- **TC-006**: Persistence MUST initially use local disk (file-based storage) behind a repository abstraction, designed so that the persistence implementation can be swapped to a database without changing domain or application layers.
- **TC-007**: Primary target platform is Linux.

## Assumptions

- The assistant is a CLI application with a background listener service, running on Linux (primary target platform). Built with .NET and the Copilot SDK.
- The user works at a personal workstation with a microphone and speaker available throughout the day.
- The user works primarily alone or in a quiet-enough environment for voice recognition to function reliably.
- The assistant is a single-user system — no multi-user support is needed.
- "Always listening" means the application runs continuously during the workday; it does not need to run overnight or during non-work hours.
- Idle detection is based on voice interaction silence, not system activity.
- Time tracking is approximate (based on task start/pause/resume events) and does not need to be precise to the second.
- The assistant communicates primarily through voice but may optionally have a visual display for task lists and notes (not required for MVP).
- Data is stored locally on the user's machine (file-based) initially; persistence layer is abstracted behind repository interfaces to allow future migration to a database. No cloud sync is required for the initial version.
- The application must run as a background process/service on Linux with direct microphone access, independent of a browser.
- The system uses Clean Architecture with DDD: presentation (CLI), application (use cases), domain (entities, aggregates), infrastructure (persistence, voice I/O). All dependencies point inward via dependency injection.
