# Development Workflow

## Prerequisites

- .NET 10 SDK.
- Git.
- Platform tooling required by Avalonia or the selected packaging target.

## Common commands

Run these commands from the repository root:

```bash
dotnet restore Foldara.sln
dotnet build Foldara.sln
dotnet test Foldara.sln
```

## Repository conventions

- Production projects live under `src/`.
- Executable test projects live under `test/` and use a `.Tests` suffix. Shared test infrastructure lives in `Foldara.Testing`.
- Project names use the `Foldara.*` namespace prefix.
- Build output and generated files are not committed.
- Scripts must fail on errors and document required environment variables.
- Credentials and local environment files must never be committed.

## Test conventions

- Use `TemporaryDirectory` from `Foldara.Testing` for file-system tests. Dispose it in the test that creates it; do not share writable directory trees between tests.
- Build fixture paths with `StoragePath` so traversal and platform-specific separators cannot enter provider tests accidentally.
- Compare complete synchronization plans with `SynchronizationPlanComparer.Instance`. Plan order, operation kind, source path, destination path, and reason are all significant.
- Keep provider integration tests deterministic and local by default. Tests that require a real cloud account must be separately classified and must never run as part of the standard test command.
- Do not place credentials, access tokens, or user file contents in test output or committed fixtures.

## Current state

The initial projects are intentionally minimal. Their first purpose is to establish dependency boundaries before implementation begins. External UI, database, and provider packages should only be added as the corresponding proof of concept or feature is developed.
