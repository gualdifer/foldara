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

A plan may record a destination-only entry as a `DeleteCandidate` for diagnostics. A delete candidate is not an executable deletion and does not authorize the executor to remove the entry.

Local scans enumerate provider directories recursively and then order the resulting snapshot by normalized path. A directory that disappears between observation and enumeration is omitted from that snapshot; cancellation and all other provider failures abort the scan without returning partial results.

Symbolic links are not followed by the initial local provider. Encountering one produces an explicit unsupported-operation error until a dedicated link policy is designed.

## One-way comparison

The planner matches source paths using destination case-sensitivity rules. Multiple source paths that would map to the same case-insensitive destination are conflicts.

Files are considered unchanged when comparable content hashes match, or when their reported sizes and last-modified timestamps match. A size, hash, or timestamp difference produces a replacement. When available metadata cannot prove equality, the planner conservatively produces a replacement.

Entries found only at the destination are recorded as non-executable `DeleteCandidate` operations. They remain untouched while automatic deletion is disabled.

## Dry runs

A dry run scans both providers and builds the same immutable plan used by execution, but it never invokes provider mutation operations. Plans can be rendered as stable text or provider-neutral JSON. Text output includes an operation summary and emits the source path, destination path, and reason for every applicable plan entry; untrusted values are escaped before display.

## Execution

The executor validates the complete plan before applying its first mutation. Conflicts and unsupported move operations reject the plan; skips and delete candidates never mutate storage.

File copies and replacements stream through provider write sessions and become visible only after commit. An interrupted or failed transfer abandons its staging data. Repeating a completed copy verifies that the destination content matches the source, while directory creation, replacement, and move handling are designed to tolerate retry after partial completion.

Before and after each file transfer, the executor observes the source metadata. It compares size, timestamps, stable item identifier, revision, and content hash when the provider reports them, and verifies the transferred byte count against the reported size. If these observations are inconsistent, the staged write is abandoned before commit and execution reports a retryable source-change failure. Detection is necessarily limited to the version signals exposed by the source provider.

Every execution returns an immutable run result. Runs and planned operations receive separate correlation identifiers and record their duration and terminal outcome. Failed and cancelled results contain a provider-neutral error code, a sanitized message, and retry guidance; operations not reached after rejection, failure, or cancellation are recorded as `NotRun`.

The executor emits structured start and completion events through `ILogger<SynchronizationPlanExecutor>`. Log events correlate runs and operations and include outcome, duration, error code, and retryability, but omit paths, plan reasons, exception objects, and provider error messages. Persisted synchronization history remains a separate concern from technical logs.

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
