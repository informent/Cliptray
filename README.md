# Cliptray

Cliptray is a local-first Windows clipboard manager. It keeps searchable clipboard history on the device, with no cloud account and no telemetry.

Clipboard history is bounded to prevent silent database growth, while pinned entries are preserved. Private-key blocks, common secret assignments, and checksum-valid payment-card numbers are excluded before persistence. History writes are atomic and retain a recovery copy so a damaged primary database does not erase the last known-good history.

Version 1.1 serializes database mutations across Cliptray processes and reloads the latest valid history inside the lock before every change. Two running instances can no longer silently overwrite each other's clips, abandoned process locks recover safely, and a damaged primary file is never copied over the last valid recovery database.

Version 1.2 binds history rows to stable clip identities, so adding a new clip or refreshing the filtered list cannot redirect selection-based preview, pin, or copy actions to a different item.

## Private history controls in 1.4

Use **Delete selected** to remove one clip, including a pinned clip, without clearing the rest of your history. Selection stays attached to the clip identity when another instance adds history. Deletion and clearing remove the recovery copy before reporting success; an inaccessible recovery file leaves the primary history unchanged and reports the failure.

New captures are limited to 65,536 characters, 500 items, and 8 MiB of UTF-8 text. Old unpinned clips make room first. Pinned clips are preserved, and an entry that cannot fit is rejected. Existing pinned data is preserved even when it exceeds a limit. Database reads above 64 MiB are refused and recovery is attempted. Null or invalid rows are ignored safely. If neither history file can be read, capture pauses and existing files are preserved for recovery.

Secret exclusion also recognizes RSA, EC, DSA, encrypted and PGP private-key blocks, shell secret assignments, and quoted secret keys. Detection is best effort; pause capture before handling confidential text. Clipboard history remains local plaintext and is not encrypted at rest.

Clipboard capture now starts off unless enabled in the window. The preference persists across restarts, and a missing or unreadable preference defaults to off. The existing Pause capture control remains available for a temporary pause.

MIT licensed.

## Name change

Cliptray replaces OpenClip. New builds use Cliptray.exe and Cliptray.csproj. Existing releases remain available under their original names.

Existing settings and data locations retain the OpenClip folder name for compatibility. Keep these folders when upgrading; no automatic move or data format change is required.

## Automatic setup

Windows setup packages install Cliptray for your current account, bundle the .NET runtime, create a Start menu shortcut and register an uninstaller. A desktop shortcut is optional. No administrator password or manual file copying is required. Existing settings and saved data stay in their current locations. The portable ZIP remains available.

Download `Cliptray-v1.4.0-setup.exe` from Releases and run it.

For unattended deployment:

```powershell
Start-Process .\Cliptray-v1.4.0-setup.exe -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-' -Wait -PassThru
```

Build from source with the [open-source Inno Setup compiler](https://github.com/jrsoftware/issrc) installed:

```powershell
pwsh -File scripts/Build-Setup.ps1 -Verify
```

The verification option installs into a temporary test folder, checks reinstall and launch, then uninstalls. It runs on the hosted Windows runner during CI. It does not remove existing user settings or backups.
