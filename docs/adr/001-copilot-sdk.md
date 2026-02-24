# ADR-001: GitHub Copilot SDK as AI Runtime

**Status**: Accepted
**Date**: 2026-02-23

## Context

The Focus Assistant requires natural language understanding (NLU) to interpret free-form voice commands and route them to the correct domain operations. We need to choose between building custom intent classification, using a generic LLM API, or leveraging a purpose-built agent SDK.

## Decision

Use the **GitHub Copilot SDK for .NET** (`GitHub.Copilot.SDK` NuGet package) as the AI runtime for NLU, intent routing, and tool invocation.

## Rationale

- **Tool-calling architecture**: The Copilot SDK supports defining custom tools via `AIFunctionFactory.Create` (from `Microsoft.Extensions.AI`), allowing us to expose domain operations as tools that the AI calls based on natural language input.
- **System prompt control**: Full control over the assistant persona, conversation style, and behavioral rules via system prompts.
- **Session management**: Built-in session support maintains conversation context within a workday.
- **Streaming support**: Real-time response streaming for text-to-speech integration.
- **.NET native**: First-class .NET support aligns with our tech stack.
- **Maintained by GitHub**: Actively maintained with regular releases.

## Alternatives Considered

- **Raw OpenAI API**: More flexibility but requires building tool-calling infrastructure ourselves.
- **Semantic Kernel**: Heavier framework with more abstractions than needed for our use case.
- **Custom intent classification**: Would require training data and NLU model — significant upfront effort for conversational voice commands.

## Consequences

- The assistant's intelligence depends on Copilot's LLM capabilities.
- Requires a valid GitHub Copilot token/subscription for operation.
- Tool definitions must be carefully designed to give the AI enough context to route correctly.
