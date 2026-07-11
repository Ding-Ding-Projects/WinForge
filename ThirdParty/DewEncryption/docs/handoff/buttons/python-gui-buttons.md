# Button Handoff: Python GUI

## Files tab

| Button | Handler | Behavior |
|---|---|---|
| Add Files | `add_files` | Opens a file picker and appends selected files to the selected-path table. |
| Add Folder | `add_folder` | Opens a directory picker and appends the selected folder. |
| Remove | `remove_selected` | Removes selected rows from the GUI and internal selected-path list. |
| Run Dew Encryption | `run_job` | Starts a background thread that calls `process` and logs archive/repo/commit output. |

## History tab

| Button | Handler | Behavior |
|---|---|---|
| Refresh History | `refresh_history` | Loads recent commits for the active selected path repo. |
| Snapshot Now | `snapshot_now` | Commits the first selected path to history immediately. |
| Start Auto History | `start_watch` | Starts a polling background thread that snapshots selected paths every five seconds. |
| Stop Auto History | `stop_watch` | Signals the polling thread to stop. |
| Create Archive | `create_archive` | Compresses the active history repo into a `.7z` archive. |
| Restore Selected | `restore_selected` | Confirms, then restores the active source to the selected commit. |
| Open Source | `open_source` | Opens the selected source path in the platform file manager. |
| Open Repo | `open_repo` | Opens the active `.dew-encryption-repo` path. |

## VeraCrypt tab

| Button | Handler | Behavior |
|---|---|---|
| Save VeraCrypt Settings | `save_veracrypt_settings` | Validates numeric fields, updates settings, and writes settings JSON. |

## Containers tab

| Button | Handler | Behavior |
|---|---|---|
| New | `new_container_profile` | Clears profile inputs and restores theme defaults. |
| Add Action | `add_container_hook` | Adds an enabled hook to the selected/saved profile. |
| Save | `save_container_profile` | Saves a profile and preserves existing hooks on update. |
| Test Open Hooks | `test_container_hooks("open")` | Runs matching open hooks for the selected profile. |
| Test Close Hooks | `test_container_hooks("close")` | Runs matching close hooks for the selected profile. |
| Snapshot History | `snapshot_container_history` | Commits the current container file into its dedicated history repo. |
| Refresh History | `refresh_container_history` | Reloads container history rows. |
| Open History Repo | `open_container_history_repo` | Opens the container history repo folder. |
