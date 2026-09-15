# Synchronization Model

## Processing pipeline

```text
Scan source and destination
            |
            v
Normalize provider metadata
            |
            v
Compare against the last known baseline
            |
            v
Build an immutable synchronization plan
            |
            v
Preview or approve the plan
            |
            v
Execute idempotent operations
            |
            v
Persist results and the new baseline
```

Planning and execution are separate concerns. A plan gives the CLI or UI a stable representation for dry runs, auditing, review, testing, and retry behavior.

## Initial behavior

The first implementation is one-way and non-destructive by default. It copies new and changed files from a source to a destination. Destination deletions are not performed automatically until deletion semantics and recovery guarantees are explicitly designed.

Local-to-local synchronization is the first end-to-end implementation. Google Drive is added after the planner and executor are covered by tests.

## Correctness requirements

The design must account for:

- files changing while they are scanned or transferred;
- interrupted transfers and resumable operations;
- timestamp precision and clock differences;
- case sensitivity and invalid names;
- symbolic links and cycles;
- atomic replacement where supported;
- hashes that are unavailable or provider-specific;
- rate limits, transient failures, retry delays, and offline operation;
- renames, deletions, and concurrent edits;
- Google-native documents, which are not ordinary downloadable files.

Executors should be cancellation-aware and idempotent. A crash after a remote operation but before local state is committed must be recoverable without silently duplicating or losing data.

## Conflict handling

Bidirectional conflict behavior is intentionally unspecified for the initial milestone. Before it is implemented, the project must define how it detects concurrent modification and whether it keeps both versions, prefers one side, asks the user, or supports configurable policies.
