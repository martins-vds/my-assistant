# ADR-004: Clean Architecture with DDD

**Status**: Accepted
**Date**: 2026-02-23

## Context

The Focus Assistant is a .NET application with a CLI presentation layer, AI agent integration, domain logic, and persistence. We need an architecture that separates concerns, allows independent testing of each layer, and supports future changes (UI addition, database migration).

## Decision

Use **Clean Architecture** with **Domain-Driven Design (DDD)** organized into four .NET projects:

1. **FocusAssistant.Domain** — Entities, value objects, aggregates, repository interfaces, domain events. Zero external dependencies.
2. **FocusAssistant.Application** — Use cases, application services, voice I/O interfaces. Depends only on Domain.
3. **FocusAssistant.Infrastructure** — Repository implementations (JSON files), voice service implementations. Depends on Domain.
4. **FocusAssistant.Cli** — Composition root with DI, Copilot SDK integration, hosted services. Depends on all layers.

## Rationale

- **Dependency inversion**: Domain defines interfaces; infrastructure implements them. Swapping persistence or voice I/O requires no domain changes.
- **Testability**: Domain and application layers have no I/O dependencies and can be unit-tested with mocked repositories.
- **DDD alignment**: Entities encapsulate behavior, aggregates enforce invariants, value objects ensure type safety.
- **Future extensibility**: Adding a GUI only requires a new presentation project referencing Application — no changes to existing code.
- **Copilot SDK boundary**: Tools in CLI/Agent layer delegate to Application use cases, keeping AI concerns out of business logic.

## Layer Rules

| Layer          | May Reference | May NOT Reference       |
| -------------- | ------------- | ----------------------- |
| Domain         | (nothing)     | Application, Infra, CLI |
| Application    | Domain        | Infrastructure, CLI     |
| Infrastructure | Domain        | Application, CLI        |
| CLI            | All layers    | (composition root)      |

## Consequences

- More projects and files than a monolithic approach — acceptable for maintainability.
- Requires discipline to prevent dependency rule violations.
- Each use case is a separate class — may feel verbose for simple operations but ensures single responsibility.
