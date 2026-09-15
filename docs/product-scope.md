# Product Scope and Roadmap

## Vision

Foldara is a cross-platform application that keeps folders synchronized with other folders and cloud storage providers. The initial desktop targets are Linux, Windows, and macOS. Android is a future target.

## Initial scope

The first useful release should support:

1. Local-folder to local-folder synchronization.
2. Local-folder to Google Drive synchronization.
3. One-way synchronization.
4. A dry-run mode that previews all planned operations.
5. Detailed structured logs and an operation history.
6. Safe defaults, with automatic deletion disabled initially.

Starting with local-to-local synchronization lets the core planning and execution pipeline mature without depending on a remote API. Google Drive should then be implemented as the first remote provider.

## Future scope

- Bidirectional synchronization.
- Additional providers such as S3-compatible object storage and WebDAV.
- Configurable deletion propagation and retention policies.
- Conflict detection and resolution workflows.
- Client-side encryption as an optional, separately designed feature.
- An Android host that reuses the domain and provider code where practical.

## Product decisions still required

- Whether the main product mode is synchronization, mirroring, backup, or a clearly separated combination of them.
- Exact rename, deletion, and conflict semantics.
- The treatment of symbolic links and ignored files.
- The behavior for Google-native documents.
- Retention, trash, and versioning policies.
- Whether encryption belongs in the first major release.

These decisions affect the data model and synchronization algorithm and should be recorded before bidirectional synchronization is implemented.
