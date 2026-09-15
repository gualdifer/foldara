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
- Test projects live under `test/` and use a `.Tests` suffix.
- Project names use the `Foldara.*` namespace prefix.
- Build output and generated files are not committed.
- Scripts must fail on errors and document required environment variables.
- Credentials and local environment files must never be committed.

## Current state

The initial projects are intentionally minimal. Their first purpose is to establish dependency boundaries before implementation begins. External UI, database, and provider packages should only be added as the corresponding proof of concept or feature is developed.
