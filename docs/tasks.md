# Prioritized Task Backlog

This document tracks the implementation backlog at a level suitable for planning. Product direction remains in [Product Scope and Roadmap](product-scope.md), while architectural constraints remain in [Architecture](architecture.md).

## Conventions

### Priority

- **P0 — Foundation:** required to build a safe, testable synchronization pipeline.
- **P1 — MVP:** required for the first useful local-to-local release.
- **P2 — Google Drive:** required for the first remote-provider release.
- **P3 — Productization:** required for distributable desktop applications.
- **P4 — Future:** valuable after the initial desktop product is stable.

Priority expresses implementation order and dependency, not urgency. A lower-numbered task normally blocks higher-numbered work.

### Status

- **Not started**
- **In progress**
- **Blocked**
- **Done**

Update the status and notes whenever a task materially changes. Split a task when it becomes too large to complete and review independently.

## P0 — Foundation

| ID | Task | Status | Depends on | Completion criteria |
| --- | --- | --- | --- | --- |
| FND-001 | Define normalized storage paths and path validation rules. | Done | — | Paths reject traversal, distinguish roots from relative paths, and have unit tests for Windows, Linux, and macOS cases. |
| FND-002 | Define storage entries and metadata types. | Not started | FND-001 | Files, folders, timestamps, sizes, identifiers, hashes, revisions, and provider-specific metadata are represented without provider SDK types. |
| FND-003 | Define provider contracts and capability discovery. | Not started | FND-001, FND-002 | Contracts support enumeration and streaming operations, expose capabilities, accept cancellation, and document error semantics. |
| FND-004 | Define immutable synchronization plan and operation types. | Not started | FND-001, FND-002 | Copy, create-directory, replace, move, skip, conflict, and delete candidates can be represented and serialized for diagnostics. |
| FND-005 | Establish shared build configuration. | Done | — | `global.json`, `Directory.Build.props`, and `Directory.Packages.props` define SDK policy, nullable analysis, warnings policy, deterministic builds, and common package versions. |
| FND-006 | Establish automated test conventions and reusable fixtures. | Not started | FND-001–FND-004 | Tests can create isolated directory trees and compare plans deterministically without using real cloud accounts. |
| FND-007 | Add CI for build and tests. | Not started | FND-005, FND-006 | Pull requests build and test on Linux, Windows, and macOS using the supported .NET SDK. |

## P1 — Local-to-Local MVP

| ID | Task | Status | Depends on | Completion criteria |
| --- | --- | --- | --- | --- |
| LOC-001 | Implement the local file-system provider. | Not started | FND-003 | Provider enumerates directories and reads/writes streams safely, exposes platform capabilities, and passes integration tests. |
| LOC-002 | Implement deterministic source and destination scanning. | Not started | LOC-001 | Scans produce normalized snapshots, support cancellation, and handle inaccessible or changing files predictably. |
| LOC-003 | Implement one-way comparison and planning. | Not started | FND-004, LOC-002 | New, unchanged, and modified files produce deterministic plans with no automatic deletions. |
| LOC-004 | Implement dry-run output. | Not started | LOC-003 | A plan can be inspected without mutating either side and includes reasons for every operation or skip. |
| LOC-005 | Implement safe plan execution. | Not started | LOC-003 | Transfers use temporary files and atomic replacement where available; execution is cancellation-aware and safely repeatable. |
| LOC-006 | Detect files modified during transfer. | Not started | LOC-005 | The executor detects inconsistent source reads and reports a retryable result instead of committing corrupt state. |
| LOC-007 | Add structured logging and operation results. | Not started | LOC-003, LOC-005 | Each run and operation has correlation identifiers, duration, outcome, and sanitized error details. |
| LOC-008 | Add end-to-end local synchronization tests. | Not started | LOC-001–LOC-007 | Tests cover nested folders, empty files, updates, cancellation, partial failure, name differences, and rerunning a completed plan. |

## P2 — State, Scheduling, and Google Drive

| ID | Task | Status | Depends on | Completion criteria |
| --- | --- | --- | --- | --- |
| REM-001 | Design the SQLite schema and migration strategy. | Not started | FND-002, FND-004 | LINQ to DB mappings and FluentMigrator migrations cover configurations, baselines, observed entries, runs, and operations; migrations are versioned and tested. |
| REM-002 | Persist runs, plans, and synchronization baselines. | Not started | REM-001, LOC-007 | Interrupted runs are diagnosable and the next scan can compare against the last committed baseline. |
| REM-003 | Implement daemon job lifecycle and scheduling. | Not started | LOC-008, REM-002 | Jobs can be started, cancelled, scheduled, and recovered after process restart without overlapping the same configuration. |
| GDR-001 | Create a Google Cloud development setup guide. | Not started | — | Documentation covers API enablement, OAuth client configuration, redirect behavior, scopes, and safe local configuration. |
| GDR-002 | Implement Google OAuth with PKCE and secure token storage. | Not started | GDR-001 | Sign-in uses the system browser, requests minimal scopes, refreshes tokens, and never stores or logs secrets in plain text. |
| GDR-003 | Implement Google Drive enumeration and metadata mapping. | Not started | FND-003, GDR-002 | Drive items map to provider-neutral entries with stable IDs, revisions, hashes when available, and explicit Google-native file handling. |
| GDR-004 | Implement Google Drive download. | Not started | GDR-003 | Binary files download through streams with cancellation, retry classification, and integrity checks where supported. |
| GDR-005 | Implement resumable Google Drive upload. | Not started | GDR-003 | Large uploads resume safely and do not create silent duplicates after retry or restart. |
| GDR-006 | Integrate Drive into one-way planning and execution. | Not started | REM-002, GDR-004, GDR-005 | Local-to-Drive dry runs and executions work through generic engine contracts and pass integration tests. |
| GDR-007 | Handle rate limits, transient failures, and offline recovery. | Not started | GDR-006 | Retry policy honors server guidance, applies bounded backoff with jitter, and exposes actionable job state. |

## P3 — API, Desktop UI, and Distribution

| ID | Task | Status | Depends on | Completion criteria |
| --- | --- | --- | --- | --- |
| APP-001 | Select and document the local IPC transport. | Not started | REM-003 | A proof of concept validates authentication, permissions, streaming progress, and all three desktop operating systems. |
| APP-002 | Define the local API contract. | Not started | APP-001 | API supports configuration, plan preview, execution, cancellation, status, history, and version negotiation. |
| APP-003 | Implement local API authentication and authorization. | Not started | APP-001, APP-002 | Untrusted local processes and browser origins cannot control the daemon without the installation credential. |
| APP-004 | Build an Avalonia UI proof of concept. | Not started | APP-002 | An AXAML and C# UI using `FluentTheme` can create a local job, preview a plan, run it, cancel it, and display live progress and errors. |
| APP-005 | Implement native-aware desktop integration. | Not started | APP-004 | UI follows system light/dark and accent preferences and validates file pickers, native menus, tray, notifications, accessibility, and OAuth browser handoff on all desktop targets. |
| APP-006 | Implement production desktop lifecycle. | Not started | APP-005 | Avalonia UI discovers or starts the daemon, handles version mismatch, supports tray behavior, and exits without interrupting active jobs. |
| DEP-001 | Package the daemon and UI for Linux. | Not started | APP-006 | Install, upgrade, service lifecycle, and uninstall are tested on supported Linux distributions. |
| DEP-002 | Package the daemon and UI for Windows. | Not started | APP-006 | Signed installer supports safe install, upgrade, service lifecycle, and uninstall. |
| DEP-003 | Package the daemon and UI for macOS. | Not started | APP-006 | Signed and notarized package supports safe install, upgrade, launchd lifecycle, and uninstall. |
| DEP-004 | Implement release automation. | Not started | DEP-001–DEP-003 | Tagged releases produce versioned artifacts, checksums, SBOMs, release notes, and signature verification instructions. |

## P4 — Future Capabilities

| ID | Task | Status | Depends on | Completion criteria |
| --- | --- | --- | --- | --- |
| FUT-001 | Specify deletion and retention semantics. | Not started | REM-002 | Behavior is documented for deletion propagation, trash, restoration, retention, and unsupported provider capabilities. |
| FUT-002 | Specify and implement bidirectional synchronization. | Not started | FUT-001 | Concurrent edits, renames, deletions, and conflicts are detected against a persisted baseline and resolved by explicit policy. |
| FUT-003 | Add S3-compatible object storage. | Not started | Stable provider contracts | Prefix, rename, multipart upload, versioning, and consistency differences are handled without file-system assumptions. |
| FUT-004 | Add ignore rules and symbolic-link policy. | Not started | LOC-008 | Matching rules are deterministic and symlinks cannot escape configured roots or create traversal cycles. |
| FUT-005 | Design optional client-side encryption. | Not started | Stable synchronization semantics | Threat model, key management, recovery, metadata leakage, naming, and compatibility are documented before implementation. |
| FUT-006 | Design the Avalonia Android host. | Not started | Stable desktop engine and UI | Android storage access, background work, power limits, user consent, responsive navigation, and reusable Avalonia views and view models are documented and prototyped. |

## Recommended Next Tasks

Work should begin in this order:

1. `FND-002` — storage entries;
2. `FND-003` — provider contracts and capabilities;
3. `FND-004` — synchronization plans;
4. `FND-006` — shared test foundations;
5. `LOC-001` — local provider.

Avoid beginning Google Drive, UI, or packaging work before the local planner and executor have reliable automated coverage. Add only the packages needed by the task currently being implemented.
