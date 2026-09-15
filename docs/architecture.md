# Architecture

## Overview

Foldara separates the synchronization engine from the desktop UI.

```text
Desktop UI
    |
    | Local authenticated API / IPC
    v
Daemon
    |
    +-- Synchronization engine
    +-- Persistent state
    +-- Local file system provider
    +-- Google Drive provider
    +-- Future providers
```

The daemon must continue running when the UI is closed or restarted. The UI is a client of the daemon and must not contain synchronization rules.

## Project responsibilities

| Project | Responsibility |
| --- | --- |
| `Foldara.Core` | Domain types, paths, entries, capabilities, plans, and shared rules. |
| `Foldara.Storage` | Provider contracts and provider-neutral transfer abstractions. |
| `Foldara.Storage.Local` | Local file-system adapter. |
| `Foldara.Storage.GoogleDrive` | Google Drive adapter and API-specific behavior. |
| `Foldara.Engine` | Scanning, comparison, planning, execution, retries, and orchestration. |
| `Foldara.Daemon` | Long-running host, scheduling, lifecycle, local API hosting, and composition root. |
| `Foldara.Api` | API contracts and endpoint composition embedded in the daemon. It is not a separate deployed service. |
| `Foldara.Desktop` | Avalonia desktop application using AXAML, C#, and `FluentTheme`. |

Dependencies should point inward. Provider implementations and hosts can reference contracts and domain projects; the domain must not reference provider SDKs, ASP.NET Core, database implementations, or UI frameworks.

## Provider model

The engine works through storage contracts rather than directly using Google Drive or file-system APIs. The abstraction must not pretend that every backend has identical semantics.

Providers advertise capabilities such as:

- stable item identifiers;
- atomic moves;
- change feeds;
- resumable uploads;
- server-side hashes;
- trash and versioning;
- case sensitivity and path rules.

The planner uses those capabilities to choose valid operations or report unsupported behavior. S3-compatible storage, for example, is an object store with prefix-based hierarchy rather than a conventional file system.

## Daemon communication

The API implementation uses ASP.NET Core hosted inside the daemon. Candidate transports are:

- Unix domain sockets on Linux and macOS;
- named pipes on Windows;
- an authenticated loopback HTTP endpoint with a random port and per-installation secret.

The transport is not yet fixed. Server-Sent Events are preferred for initial live progress because the daemon-to-UI update flow is one-way. SignalR should only be introduced if a concrete bidirectional requirement emerges.

## Persistent state

SQLite will store configuration, observed metadata, synchronization baselines, pending operations, and operation history. LINQ to DB provides typed data access and FluentMigrator owns schema migrations. Secrets must remain outside the database unless protected with an appropriate platform credential mechanism.

The persisted file index is expected to include stable remote identifiers, relative paths, sizes, observed timestamps, hashes when available, provider revisions or ETags, last synchronized state, and pending operation state.

## Android considerations

Android should reuse the domain model, planner, provider code, and appropriate Avalonia views and view models where platform APIs permit. It will require a separate Avalonia Android host because Android background execution, file access, and power-management constraints differ from desktop operating systems. The desktop daemon design must not assume that the same process model will run unchanged on Android.

## Simplicity

Architecture follows current requirements rather than anticipated scale. Foldara begins as a small set of projects in one solution, one daemon process, one desktop process, and one SQLite database per user profile. New layers and abstractions require a concrete use case and must make behavior easier to understand or test.
