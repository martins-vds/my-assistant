# System Prompt Design Notes

## Overview

The system prompt (`SystemPromptBuilder.cs`) defines the AI assistant's behavior across 12 distinct sections. It is modular — each feature area has its own section, and the onboarding section is conditionally appended only during first-use.

## Architecture

```
SystemPromptBuilder.Build(needsOnboarding: bool) → string
```

The prompt is composed by concatenating named `const string` sections. This design makes each behavior independently reviewable and testable.

## Section Purpose

| Section                | Purpose                                                                                                                  |
| ---------------------- | ------------------------------------------------------------------------------------------------------------------------ |
| **Persona**            | Identity, tone, and personality — concise, supportive, conversational                                                    |
| **Capabilities**       | Lists available tool categories so the LLM knows what it can do                                                          |
| **ConversationRules**  | 7 rules governing interaction: brevity, confirmation, disambiguation, no interruptions, natural language, error recovery |
| **TaskTrackingRules**  | When to create/switch/complete tasks, auto-pause behavior, large list handling, archive suggestions                      |
| **ReminderBehavior**   | Idle check-in, paused-task reminders, focus suppression, post-completion suggestions, escalating suppression             |
| **NoteBehavior**       | Auto-attachment, readback on switch, querying, long-content extraction, standalone notes                                 |
| **ReflectionBehavior** | Trigger phrases, structured walkthrough, priority setting, scheduled prompts                                             |
| **MorningBriefing**    | Auto-trigger on new session, greeting, task ages, transition to first task                                               |
| **VoiceClarification** | Low-confidence handling, similar names, short utterances, numbers, background noise                                      |
| **Preferences**        | How to view/change settings using preference tools                                                                       |
| **ArchiveBehavior**    | When to suggest archiving, age filters, preservation semantics                                                           |
| **Onboarding**         | First-use guided 6-step setup flow (conditional)                                                                         |

## Design Decisions

### 1. No Interruption During Focus
The most important behavioral rule: the assistant never speaks spontaneously while the user is actively working (in-progress task + recent interaction). Reminders are only delivered during idle periods or for paused tasks that exceed their interval.

### 2. Escalating Suppression
If the user ignores a reminder for a paused task, the system suppresses further reminders for that task in the current session. This prevents nagging and respects user attention.

### 3. Natural Language Interpretation
The prompt instructs the LLM to interpret casual speech ("I'm done with this" = complete, "let me work on X" = switch). This is critical for voice input where commands are rarely precise.

### 4. Conditional Onboarding
The onboarding section is appended only when `needsOnboarding=true` (first launch, no preferences file). Once preferences are set, subsequent sessions get the normal prompt without the onboarding flow.

### 5. Tool-Action Correspondence
The prompt explicitly states: "Never pretend to do something without calling the corresponding tool." This prevents the LLM from hallucinating actions and ensures all operations go through the use case layer.

### 6. Large Task List Handling
When >20 open tasks exist, `get_open_tasks` returns a grouped summary instead of listing all tasks. The prompt instructs the LLM to present this naturally and offer to drill into specific groups.

## Maintenance Guidelines

- Add new behavior sections as `const string` fields
- Include new sections in `Build()` concatenation
- Keep each section focused on one tool group or behavior
- Use imperative bullet points ("When X, do Y")
- Mention specific tool names so the LLM knows which function to call
- Test prompt changes by verifying the built string includes all sections
