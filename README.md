# Cliptray

Cliptray is a local-first Windows clipboard manager. It keeps searchable clipboard history on the device, with no cloud account and no telemetry.

Clipboard history is bounded to prevent silent database growth, while pinned entries are preserved. Private-key blocks, common secret assignments, and checksum-valid payment-card numbers are excluded before persistence. History writes are atomic and retain a recovery copy so a damaged primary database does not erase the last known-good history.

Version 1.1 serializes database mutations across Cliptray processes and reloads the latest valid history inside the lock before every change. Two running instances can no longer silently overwrite each other's clips, abandoned process locks recover safely, and a damaged primary file is never copied over the last valid recovery database.

Version 1.2 binds history rows to stable clip identities, so adding a new clip or refreshing the filtered list cannot redirect selection-based preview, pin, or copy actions to a different item.

MIT licensed.

## Name change

Cliptray replaces OpenClip. New builds use Cliptray.exe and Cliptray.csproj. Existing releases remain available under their original names.

Existing settings and data locations retain the OpenClip folder name for compatibility. Keep these folders when upgrading; no automatic move or data format change is required.
