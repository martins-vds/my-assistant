# Implementation Plan: Focus Assistant

**Branch**: `001-focus-assistant` | **Date**: 2026-02-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-focus-assistant/spec.md`

## Summary

A voice-driven personal task-tracking assistant that helps a tech worker manage context switches throughout the workday. Built with .NET 8 using the GitHub Copilot SDK for natural language understanding and intent routing. The assistant listens for a wake word, interprets voice commands via Copilot, manages task state through DDD domain entities, persists data to local disk, and provides proactive reminders, task notes, end-of-day reflection, and morning briefings — all via CLI with a background listener service.

## Bounded Context

This project operates within a single **Focus Management** bounded context. All domain concepts (FocusTask, TaskNote, WorkSession, DailyPlan, UserPreferences) belong to this context and share a unified ubiquitous language. No cross-context communication is required at this stage.

**Ubiquitous Language**:
- **FocusTask**: A unit of work the user is tracking (prefixed to avoid ambiguity with system tasks)
- **TaskNote**: A timestamped piece of context attached to a FocusTask
- **WorkSession**: A continuous period of interaction (typically one workday)
- **DailyPlan**: Prioritized task list generated during end-of-day reflection
- **UserPreferences**: Configuration for reminder intervals, idle thresholds, and reflection time
- **TaskAggregate**: The aggregate root managing FocusTask lifecycle and enforcing invariants

If future features introduce distinct concerns (e.g., team collaboration, analytics), they MUST be modeled as separate bounded contexts with explicit integration events.

## Technical Context

**Language/Version**: C# / .NET 8.0  
**Primary Dependencies**:
- `GitHub.Copilot.SDK` — AI agent runtime for NLU, intent routing, and tool invocation
- `Microsoft.Extensions.Hosting` — Background service hosting and DI container
- `Microsoft.Extensions.AI` — `AIFunctionFactory` for defining tools exposed to Copilot
- `System.Text.Json` — Serialization for file-based persistence
- Speech-to-text: OS-level or third-party library (e.g., Vosk for offline, or Azure Speech SDK)
- Text-to-speech: OS-level or third-party library (e.g., `espeak` on Linux, or Azure Speech SDK)
- Wake-word detection: Lightweight keyword spotter (e.g., Porcupine, or OpenWakeWord)

**Storage**: Local filesystem (JSON files), behind repository abstractions for future DB migration  
**Testing**: `xunit`, `Moq` or `NSubstitute` for unit tests  
**Target Platform**: Linux (primary)  
**Project Type**: CLI application with background hosted service  
**Performance Goals**: Voice command response < 3 seconds end-to-end  
**Constraints**: Must run reliably for 8+ hours continuously; all data persisted locally  
**Scale/Scope**: Single user, single machine

### Performance Budgets

| Metric                           | Target         | Measurement                      |
| -------------------------------- | -------------- | -------------------------------- |
| Voice command E2E response       | < 3s at p95    | Wake-word detection to TTS start |
| CLI command response (text mode) | < 500ms at p95 | Input to output timer            |
| Application startup              | < 2s           | `dotnet run` to ready            |
| Memory (8hr session)             | < 200 MB RSS   | Process monitoring               |
| Memory growth rate               | < 5 MB/hour    | RSS monitoring over 8hr          |
| File I/O per command             | < 50ms         | Persistence timing               |
| CPU idle (between commands)      | < 2%           | Profiler sampling                |

## Project Structure

### Documentation (this feature)

```text
specs/001-focus-assistant/
├── plan.md              # This file
├── spec.md              # Feature specification
├── checklists/          # Quality checklists
└── tasks.md             # Task breakdown (generated next)
```

### Source Code (repository root)

```text
src/
├── FocusAssistant.Domain/
│   ├── Entities/
│   │   ├── FocusTask.cs
│   │   ├── TaskNote.cs
│   │   ├── WorkSession.cs
│   │   ├── DailyPlan.cs
│   │   └── UserPreferences.cs
│   ├── ValueObjects/
│   │   ├── TaskStatus.cs
│   │   ├── TimeLogEntry.cs
│   │   └── ReminderInterval.cs
│   ├── Aggregates/
│   │   └── TaskAggregate.cs
│   ├── Repositories/
│   │   ├── ITaskRepository.cs
│   │   ├── ISessionRepository.cs
│   │   ├── IDailyPlanRepository.cs
│   │   └── IUserPreferencesRepository.cs
│   ├── Events/
│   │   ├── TaskCreatedEvent.cs
│   │   ├── TaskSwitchedEvent.cs
│   │   └── TaskCompletedEvent.cs
│   └── FocusAssistant.Domain.csproj
│
├── FocusAssistant.Application/
│   ├── UseCases/
│   │   ├── CreateTaskUseCase.cs
│   │   ├── SwitchTaskUseCase.cs
│   │   ├── CompleteTaskUseCase.cs
│   │   ├── RenameTaskUseCase.cs
│   │   ├── DeleteTaskUseCase.cs
│   │   ├── MergeTasksUseCase.cs
│   │   ├── AddNoteUseCase.cs
│   │   ├── GetOpenTasksUseCase.cs
│   │   ├── GetTaskNotesUseCase.cs
│   │   ├── SetReminderUseCase.cs
│   │   ├── StartReflectionUseCase.cs
│   │   ├── SetPrioritiesUseCase.cs
│   │   └── GetMorningBriefingUseCase.cs
│   ├── Services/
│   │   ├── TaskTrackingService.cs
│   │   ├── ReminderScheduler.cs
│   │   └── ReflectionService.cs
│   ├── Interfaces/
│   │   ├── IVoiceInputService.cs
│   │   └── IVoiceOutputService.cs
│   └── FocusAssistant.Application.csproj
│
├── FocusAssistant.Infrastructure/
│   ├── Persistence/
│   │   ├── FileTaskRepository.cs
│   │   ├── FileSessionRepository.cs
│   │   ├── FileDailyPlanRepository.cs
│   │   ├── FileUserPreferencesRepository.cs
│   │   └── JsonFileStore.cs
│   ├── Voice/
│   │   ├── WakeWordDetector.cs
│   │   ├── SpeechToTextService.cs
│   │   └── TextToSpeechService.cs
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs
│   └── FocusAssistant.Infrastructure.csproj
│
└── FocusAssistant.Cli/
    ├── Program.cs
    ├── HostedServices/
    │   ├── VoiceListenerService.cs
    │   └── ReminderBackgroundService.cs
    ├── Agent/
    │   ├── CopilotAgentSession.cs
    │   ├── ToolDefinitions.cs
    │   └── SystemPromptBuilder.cs
    └── FocusAssistant.Cli.csproj

tests/
├── FocusAssistant.Domain.Tests/
│   └── FocusAssistant.Domain.Tests.csproj
├── FocusAssistant.Application.Tests/
│   └── FocusAssistant.Application.Tests.csproj
└── FocusAssistant.Infrastructure.Tests/
    └── FocusAssistant.Infrastructure.Tests.csproj
```

**Structure Decision**: Clean Architecture with 4 projects following DDD. Domain has zero external dependencies. Application depends only on Domain. Infrastructure implements repository interfaces and voice I/O. CLI is the composition root with DI, Copilot SDK integration, and hosted services. The Copilot SDK custom tools act as the bridge: the AI calls tools that invoke application-layer use cases, which operate on domain entities, which are persisted via repository abstractions.

## Architecture Overview

### Request Flow

```
Microphone → WakeWordDetector → SpeechToTextService → raw text
    ↓
CopilotAgentSession.SendAsync(raw text)
    ↓
Copilot AI (with system prompt defining assistant persona)
    ↓
Copilot calls custom tool (e.g., "create_task", "switch_task", "get_open_tasks")
    ↓
ToolDefinitions → Application Use Case → Domain Entities → Repository
    ↓
Tool returns result string → Copilot formats response
    ↓
TextToSpeechService → Speaker
```

### Layer Dependencies (Clean Architecture)

```
CLI (composition root)
  → Application (use cases)
    → Domain (entities, repositories interfaces, value objects)
  → Infrastructure (implements Domain interfaces)
  → Copilot SDK (presentation/agent layer)
```

All dependencies point inward. Domain has zero references to other projects. Infrastructure and CLI reference Domain. Application references only Domain. DI wiring happens in CLI's `Program.cs`.

### Copilot SDK Integration

The Copilot SDK serves as the "brain" of the assistant:
- **System prompt** defines the assistant's persona, capabilities, and conversation style
- **Custom tools** (via `AIFunctionFactory.Create`) expose domain operations to the AI
- **Session** maintains conversation context within a workday
- **Streaming** enables real-time response for text-to-speech
- The AI decides which tool to call based on the user's natural language input
- Tools return structured results; the AI formats them into conversational responses

### Key Design Decisions

1. **Copilot SDK as NLU engine**: Instead of building intent classification, we leverage Copilot's LLM to interpret free-form voice commands and route them to the correct tool.
2. **Custom tools as application boundary**: Each tool maps to an application use case, keeping AI concerns separate from business logic.
3. **File-based persistence**: JSON files in `~/.focus-assistant/data/` behind repository interfaces. Swappable to SQLite/PostgreSQL later by implementing new repository classes.
4. **Background hosted services**: `VoiceListenerService` for continuous wake-word detection and command capture; `ReminderBackgroundService` for timed check-ins and reminders.
5. **First-use onboarding**: Detected by checking for preferences file; if absent, the system prompt instructs Copilot to guide the user through setup before normal operation.
6. **Session lifecycle**: `WorkSession` entities are implicitly managed — created on `VoiceListenerService` startup, ended on graceful shutdown or end-of-day reflection. No explicit create/end session user command is needed; the session tracks tasks worked on and stores the reflection summary.
7. **Data directory**: All persistent data stored under `~/.focus-assistant/data/` by default, configurable via `FOCUS_ASSISTANT_DATA_DIR` environment variable. Directory is created on first write.
