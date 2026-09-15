# Technology Stack

## Baseline

- C# and .NET 10 LTS for production code.
- ASP.NET Core for the daemon's local API.
- `Microsoft.Extensions.Hosting` for daemon lifecycle and dependency injection.
- SQLite with `Microsoft.Data.Sqlite` for durable local state.
- LINQ to DB for typed data access without change tracking.
- FluentMigrator for explicit, versioned database migrations.
- Serilog as the host logging provider behind `Microsoft.Extensions.Logging` abstractions.
- Avalonia UI with `FluentTheme` for the desktop user interface.
- Google Drive API with OAuth 2.0 and PKCE.
- xUnit for automated tests.

The repository targets .NET 10. A `global.json`, `Directory.Build.props`, and `Directory.Packages.props` should define the supported SDK policy, shared compiler settings, and package versions before feature dependencies are introduced.

## Desktop UI

The desktop application uses Avalonia directly rather than embedding a web application. Views are written in AXAML and C#, use `FluentTheme`, and communicate with the daemon through its local API. Blazor, Electron, Photino, WebView-based shells, and .NET MAUI are not part of the initial implementation.

The visual design should be native-aware rather than attempting to reproduce every operating system toolkit. It should:

- follow the operating system light or dark preference;
- use the operating system accent color where available;
- use native file and folder pickers, menus, notifications, and tray integration where Avalonia supports them;
- keep a consistent Foldara layout and identity across platforms;
- introduce platform-specific styles only when they materially improve usability;
- use a responsive Android layout in the future rather than forcing the desktop layout onto a small screen.

Avalonia also provides a future path for sharing views and view models with an Android host. Android will still need platform-specific storage access, lifecycle, and background execution.

## Persistence

LINQ to DB is used directly in focused persistence services such as job, baseline, run, and operation stores. A generic repository abstraction is not required.

FluentMigrator owns the schema. Migrations run before the daemon accepts work, and the application must refuse to use a database whose schema is newer than it supports. SQLite configuration should enable foreign keys, use WAL mode where appropriate, set a busy timeout, and keep write transactions short.

## Logging

Application code depends on `ILogger<T>`. Serilog is configured only at executable composition roots. Development initially logs to the console; production adds bounded rolling files.

Technical logs and user-visible synchronization history are different concerns. Operation history is structured application data stored in SQLite and must not be reconstructed from log files. Neither store may contain credentials, tokens, or file contents.

## Simplicity rules

- Use ASP.NET Core Minimal APIs before introducing additional API frameworks.
- Use Server-Sent Events for one-way progress updates before considering SignalR.
- Do not introduce MediatR, AutoMapper, a generic repository, CQRS infrastructure, an event bus, or microservices without a demonstrated requirement.
- Keep the daemon and its local API in one installed process. `Foldara.Api` contains API contracts and endpoint composition; it is not a separately deployed service.
- Add a dependency only when the task that needs it begins.
- Prefer an explicit implementation over a speculative abstraction, then refactor when a second concrete use case proves the abstraction useful.

## Dependency policy

- Keep external dependencies out of `Foldara.Core` where possible.
- Wrap provider SDKs inside provider-specific projects.
- Prefer framework facilities for hosting, configuration, logging, and HTTP.
- Prefer permissively licensed dependencies and generate third-party notices and an SBOM for releases.
- Evaluate licenses, maintenance status, platform support, and native dependencies before adoption.
- Record consequential technology decisions in the documentation, or in Architecture Decision Records when introduced.
