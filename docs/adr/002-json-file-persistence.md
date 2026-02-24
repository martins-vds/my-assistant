# ADR-002: JSON File-Based Persistence

**Status**: Accepted
**Date**: 2026-02-23

## Context

The Focus Assistant needs to persist task data, notes, sessions, daily plans, and user preferences across application restarts. We need a storage mechanism that is simple, requires no external dependencies, and can be swapped for a database later.

## Decision

Use **local JSON files** stored under `~/.focus-assistant/data/` (configurable via `FOCUS_ASSISTANT_DATA_DIR` environment variable) as the persistence layer, accessed through a generic `JsonFileStore<T>` helper behind repository abstractions.

## Rationale

- **Zero external dependencies**: No database server or service required.
- **Human-readable**: JSON files can be inspected and manually edited if needed.
- **Simple backup**: File copy is sufficient for data backup.
- **Repository pattern**: All persistence is behind `ITaskRepository`, `ISessionRepository`, etc., so switching to SQLite/PostgreSQL only requires new repository implementations — no domain or application layer changes.
- **Atomic writes**: Using write-to-temp-then-rename pattern prevents data corruption on crashes.
- **Single user**: No concurrency concerns for a single-user desktop application.

## Alternatives Considered

- **SQLite**: More robust for queries but adds a native dependency and is overengineered for the initial scope.
- **LiteDB**: .NET-native document DB — reasonable alternative but adds a dependency without significant benefit for the simple data model.
- **In-memory only**: Would lose data on restart — unacceptable per FR-007.

## Consequences

- File I/O on every write; acceptable for the expected operation frequency (~1 write/minute).
- No complex query support — all filtering happens in-memory after loading.
- Must implement atomic writes to prevent corruption.
- Data directory must be created on first write.
