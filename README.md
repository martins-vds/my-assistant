# Focus Assistant

A voice-driven personal task-tracking assistant that helps you manage context switches throughout your workday. Speak naturally to create tasks, switch between them, take notes, and get reminders — all hands-free.

Built with .NET 8, the [GitHub Copilot SDK](https://github.com/github/copilot-sdk), and [Vosk](https://alphacephei.com/vosk/) for offline speech recognition.

## What It Does

Focus Assistant runs continuously in the background, listening for the wake word **"Hey Focus"**. When activated, it captures your speech, sends it to a Copilot-powered AI agent, and speaks the response back to you.

### Core Features

- **Voice-based task tracking** — Create, switch, pause, complete, rename, delete, and merge tasks by speaking naturally. Time spent on each task is tracked automatically.
- **Proactive check-ins** — If you've been idle for a while, the assistant asks what you're working on. Paused tasks trigger reminders at configurable intervals.
- **Task notes** — Dictate notes attached to the current task. When you resume a paused task, the assistant reads back your last notes so you remember where you left off.
- **End-of-day reflection** — Summarizes your day (tasks completed, time spent, what's still open) and lets you set priorities for tomorrow.
- **Morning briefing** — On a new session, the assistant greets you with yesterday's carry-over tasks and the priorities you set.
- **First-use onboarding** — Guides you through setting preferences on first launch (reminder intervals, idle threshold, reflection time).

### How It Works

```
Microphone → Wake Word ("Hey Focus") → Speech-to-Text (Vosk) → raw text
  → Copilot AI Agent (interprets intent, calls tools)
    → Application Use Cases → Domain Entities → JSON File Persistence
  → AI formats response → Text-to-Speech (espeak-ng / SAPI) → Speaker
```

The Copilot SDK acts as the "brain" — it interprets free-form voice commands and routes them to the correct tool (e.g., `create_task`, `switch_task`, `add_note`). Tools are thin wrappers that delegate to application-layer use cases.

## Prerequisites

| Dependency                      | Linux                                                              | Windows                                          |
| ------------------------------- | ------------------------------------------------------------------ | ------------------------------------------------ |
| **.NET 8 SDK**                  | [Install](https://dotnet.microsoft.com/download)                   | [Install](https://dotnet.microsoft.com/download) |
| **GitHub CLI (`gh`)**           | `apt install gh` / `dnf install gh`                                | `winget install GitHub.cli`                      |
| **GitHub Copilot subscription** | [github.com/features/copilot](https://github.com/features/copilot) | Same                                             |
| **espeak-ng** (TTS)             | `apt install espeak-ng`                                            | — (uses Windows SAPI)                            |
| **alsa-utils** (audio capture)  | `apt install alsa-utils`                                           | — (uses ffmpeg/DirectShow)                       |
| **ffmpeg** (audio capture)      | —                                                                  | `winget install Gyan.FFmpeg`                     |

### Automated Setup

The setup scripts install all dependencies and download the Vosk speech model:

**Linux:**
```bash
cd scripts
./setup.sh
```

**Windows (PowerShell):**
```powershell
cd scripts
.\setup.ps1
```

### GitHub Authentication

The assistant requires an authenticated GitHub account with a Copilot subscription:

```bash
gh auth login
```

Follow the prompts to authenticate. The assistant verifies authentication on startup and will show a clear error if it's missing.

## Running the Assistant

### Development Mode

From the `scripts/` directory:

**Linux:**
```bash
./run.sh
```

**Windows:**
```powershell
.\run.ps1
```

The run scripts auto-detect your audio hardware. If a microphone is found, the assistant starts in **voice mode** (wake word listening). Otherwise, it falls back to **text mode** (type commands in the terminal).

### Command-Line Options

```
dotnet run --project src/FocusAssistant.Cli -- [options]

Options:
  --text       Run in text mode (keyboard input instead of voice)
  --verbose    Enable debug-level logging
  --quiet      Suppress all logs except errors
  --test       Run a diagnostic test (checks auth, SDK, model connectivity)
```

You can also set `FOCUS_ASSISTANT_LOG_LEVEL=Debug` (or `Information`, `Warning`, `Error`) as an environment variable.

### Publishing a Standalone Executable

Build a self-contained single-file executable:

```bash
cd scripts

./publish.sh              # Build for current OS
./publish.sh linux        # Build for Linux x64
./publish.sh windows      # Build for Windows x64
./publish.sh both         # Build for both platforms
```

Executables are output to the `publish/` directory.

## Usage

Once running, say **"Hey Focus"** followed by a command:

| You say                                                | What happens                                                  |
| ------------------------------------------------------ | ------------------------------------------------------------- |
| "Hey Focus, I'm starting work on the API refactor"     | Creates a task called "API refactor" and starts tracking time |
| "Hey Focus, switching to review pull requests"         | Pauses current task, starts "review pull requests"            |
| "Hey Focus, what are my current tasks?"                | Lists all open tasks with status and time spent today         |
| "Hey Focus, I finished the login bug"                  | Marks that task as completed                                  |
| "Hey Focus, note: the issue is in the auth middleware" | Attaches a timestamped note to the current task               |
| "Hey Focus, what are my notes on the API refactor?"    | Reads back all notes for that task                            |
| "Hey Focus, let's wrap up for the day"                 | Starts end-of-day reflection                                  |
| "Hey Focus, set my reminder interval to 2 hours"       | Updates default reminder interval                             |
| "Hey Focus, remind me about this in 30 minutes"        | Sets a per-task reminder override                             |

### Text Mode

If running with `--text`, type your commands directly (no wake word needed):

```
> I'm starting work on the API refactor
Created and started 'API refactor'.

> What are my current tasks?
You have 1 task in progress:
  • API refactor (in-progress, 12 min today)
```

## Data Storage

All data is stored locally as JSON files under `~/.focus-assistant/data/` by default:

```
~/.focus-assistant/
├── data/
│   ├── tasks.json
│   ├── sessions.json
│   ├── daily-plans.json
│   └── preferences.json
└── models/
    └── vosk-model-small-en-us-0.15/
```

Override the data directory with the `FOCUS_ASSISTANT_DATA_DIR` environment variable.

## Architecture

Clean Architecture with Domain-Driven Design (DDD):

```
src/
├── FocusAssistant.Domain/          # Entities, value objects, aggregates, repository interfaces
├── FocusAssistant.Application/     # Use cases, services, application interfaces
├── FocusAssistant.Infrastructure/  # File persistence, voice I/O (Vosk, espeak-ng)
└── FocusAssistant.Cli/             # Composition root, Copilot agent, hosted services
```

All dependencies point inward. Domain has zero external references. Infrastructure and CLI implement abstractions defined in Domain and Application.

### Key Components

- **CopilotAgentSession** — Manages the Copilot SDK client/session lifecycle. Serializes concurrent `SendCommandAsync` calls via semaphore.
- **VoiceListenerService** — Background service: wake word → STT → Copilot → TTS loop.
- **ReminderBackgroundService** — Background service: checks idle time and paused-task durations every 30 seconds.
- **ToolDefinitions** — Maps AI tool calls to application use cases (`create_task`, `switch_task`, etc.).
- **WakeWordDetector** — Vosk-based continuous audio monitoring for the wake phrase.

## Running Tests

```bash
dotnet test
```

The test suite includes 338 tests across three projects:

| Project                             | Tests | Coverage                            |
| ----------------------------------- | ----- | ----------------------------------- |
| FocusAssistant.Domain.Tests         | 114   | Entities, value objects, aggregates |
| FocusAssistant.Application.Tests    | 134   | Use cases, services                 |
| FocusAssistant.Infrastructure.Tests | 90    | File persistence, voice contracts   |

## Troubleshooting

| Problem                                   | Solution                                                                                                  |
| ----------------------------------------- | --------------------------------------------------------------------------------------------------------- |
| "Not authenticated" error on startup      | Run `gh auth login` and authenticate with a Copilot-enabled account                                       |
| Wake word not detected                    | Check microphone: `arecord -d 3 -r 16000 -c 1 -f S16_LE test.wav && aplay test.wav`                       |
| No audio device — falls back to text mode | Ensure `alsa-utils` (Linux) or `ffmpeg` (Windows) is installed; check that your mic is the default device |
| Assistant hangs after speaking            | Run with `--verbose` to see debug logs; check `gh auth status`                                            |
| Vosk model not found                      | Re-run `./setup.sh` (or `.\setup.ps1`) to download the model                                              |

## License

This is a personal project — no license specified.
