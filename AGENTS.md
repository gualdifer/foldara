# Foldara Contributor Guide

Foldara is a cross-platform folder synchronization application. The synchronization engine runs independently from the user interface and supports storage providers through explicit abstractions.

## Repository layout

- `src/` contains production C# projects.
- `test/` contains automated test projects.
- `docs/` contains architecture and engineering documentation.
- `scripts/` contains local development and maintenance scripts.
- `deploy/` contains packaging, installation, and deployment assets.

## Documentation index

- [Product scope and roadmap](docs/product-scope.md)
- [Architecture](docs/architecture.md)
- [Technology stack](docs/technology-stack.md)
- [Synchronization model](docs/synchronization-model.md)
- [Security](docs/security.md)
- [Deployment](docs/deployment.md)
- [Development workflow](docs/development.md)

## Working agreements

- Write source code, identifiers, documentation, commit messages, and user-facing diagnostic messages in English.
- Keep the synchronization domain independent of UI frameworks and provider SDKs.
- Put provider-specific behavior behind storage abstractions, but expose provider capabilities instead of assuming all providers behave like a file system.
- Separate synchronization planning from execution. Every destructive operation must be visible in a plan before execution.
- Prefer idempotent operations, cancellation support, structured logging, and explicit error handling.
- Never log access tokens, refresh tokens, secrets, or file contents.
- Add or update tests whenever behavior changes.
- Update the relevant document when making an architectural or deployment decision.

## Validation

From the repository root, run:

```bash
dotnet build Foldara.sln
dotnet test Foldara.sln
```

Platform-specific packaging commands will be added under `deploy/` as those workflows are implemented.
