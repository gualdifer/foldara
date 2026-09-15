# Technology Stack

## Baseline

- C# and .NET 10 for production code.
- ASP.NET Core for the daemon's local API.
- `Microsoft.Extensions.Hosting` for daemon lifecycle and dependency injection.
- SQLite for durable local state.
- Blazor for a C#-centric web user interface.
- Google Drive API with OAuth 2.0 and PKCE.
- xUnit for automated tests.

The repository is initially pinned conceptually to .NET 10. A `global.json` should be added when CI and developer SDK roll-forward policy are decided.

## Desktop shell

The intended UI is a web application embedded in a desktop shell. Avalonia with a suitable WebView integration is the leading option because it leaves room for native desktop components. Photino.NET remains a lightweight alternative.

The final shell choice is deliberately deferred until a small proof of concept validates:

- Linux, Windows, and macOS WebView availability and packaging;
- communication with the daemon;
- OAuth browser handoff;
- accessibility, tray integration, and application lifecycle;
- installer size and update behavior.

.NET MAUI is not the default desktop choice because Linux is not an officially supported target.

## Dependency policy

- Keep external dependencies out of `Foldara.Core` where possible.
- Wrap provider SDKs inside provider-specific projects.
- Prefer framework facilities for hosting, configuration, logging, and HTTP.
- Evaluate licenses, maintenance status, platform support, and native dependencies before adoption.
- Record consequential technology decisions in the documentation, or in Architecture Decision Records when introduced.
