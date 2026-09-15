# Deployment

## Desktop targets

Foldara is intended to ship on:

- Linux as a systemd user service or another distribution-appropriate background service plus a desktop application.
- Windows as a Windows Service or per-user background process plus a desktop application.
- macOS as a launchd agent plus a desktop application.

Whether the daemon is installed system-wide or per-user is still open. A per-user model is likely to simplify OAuth credentials, file permissions, and desktop integration.

## Packaging goals

- Produce reproducible, signed artifacts for each supported architecture.
- Keep daemon and UI version compatibility explicit.
- Support safe upgrades without losing configuration or synchronization state.
- Provide uninstall behavior that clearly distinguishes binaries, settings, credentials, and cached data.
- Generate checksums and a software bill of materials in release automation.

Platform-specific scripts and packaging definitions belong under `deploy/`. General development automation belongs under `scripts/`.

## Candidate formats

- Linux: distribution packages and, after evaluation, a portable format such as Flatpak or AppImage.
- Windows: MSIX or a signed installer appropriate for the daemon lifecycle.
- macOS: signed and notarized application bundle and installer image/package.

No packaging format is committed yet. Each choice must be validated against background-service installation, auto-update expectations, code signing, and WebView requirements.

## Android

Android deployment will be designed separately. It will use Android-supported background work and storage access rather than packaging the desktop daemon unchanged.
