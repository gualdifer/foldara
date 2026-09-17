# Security

## Principles

- Request the minimum provider scopes required by enabled features.
- Use the system browser and OAuth 2.0 Authorization Code flow with PKCE.
- Store tokens in the operating system credential store or another platform-appropriate protected store.
- Never store tokens or secrets in plain-text configuration, SQLite rows, diagnostics, or crash reports.
- Restrict permissions on local state and IPC endpoints.
- Authenticate every local API client; loopback binding alone is not authentication.
- Treat file names, paths, metadata, and remote responses as untrusted input.

## Local API

If loopback HTTP is used, the daemon should bind only to loopback, select or securely reserve its endpoint, and require a high-entropy per-installation credential. Browser-origin protections and cross-origin policies must be explicit. Unix sockets and named pipes must use restrictive operating-system permissions.

## File operations

Path normalization must prevent traversal outside configured roots. Writes should use temporary files and atomic replacement when available. Destructive plans require explicit policy and should integrate provider trash or retention features where possible.

## Synchronization diagnostics

Technical synchronization logs contain correlation identifiers, operation kinds, outcomes, durations, and provider-neutral error codes. They must not contain configured root paths, entry paths, plan reasons, file contents, raw provider messages, exception objects, credentials, or tokens. User-visible operation results use fixed sanitized messages rather than provider exception text.

## Client-side encryption

Encryption is a separate product feature, not an incidental transfer option. It requires a documented key-management, recovery, naming, metadata, deduplication, and compatibility design before implementation.
