# Button Handoff: C# Avalonia GUI

## Header

| Button | Handler | CLI call |
|---|---|---|
| Open CLI Help | `OpenHelp_Click` | `dew-encryption --help` |

## Files tab

| Button | Handler | Behavior |
|---|---|---|
| Add files | `AddFiles_Click` | Uses Avalonia file picker and adds selected file paths. |
| Add folder | `AddFolder_Click` | Uses Avalonia folder picker and adds one folder path. |
| Remove selected | `RemoveSelected_Click` | Removes selected `DewPathItem` rows. |
| Run Dew Encryption | `RunSelected_Click` | Calls `dew-encryption <selected paths...>`. |

## History tab

| Button | Handler | CLI call |
|---|---|---|
| Refresh history | `RefreshHistory_Click` | `dew-encryption history <first selected path>` |
| Snapshot now | `SnapshotHistory_Click` | `dew-encryption <first selected path>` |
| Open Python history manager | `OpenPythonHistoryGui_Click` | `dew-encryption-gui <first selected path> --history` |

## Containers tab

| Button | Handler | CLI call / behavior |
|---|---|---|
| Register container | `RegisterContainer_Click` | Adds a draft label to the in-memory list and log. |
| Snapshot history | `SnapshotContainer_Click` | `dew-encryption container-history <container> --snapshot` |
| Test open hooks | `TestOpenHooks_Click` | `dew-encryption container-hooks open <container> [--mount-path <path>]` |
| Test close hooks | `TestCloseHooks_Click` | `dew-encryption container-hooks close <container> [--mount-path <path>]` |
