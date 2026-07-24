# Step Handoff: End-User Archive and History

## Create an archive snapshot

1. Select one or more files/folders in Explorer, Nautilus, CLI, or the GUI.
2. Launch `dew encryption` / `dew-encryption <paths...>` / `Run Dew Encryption`.
3. The app creates `Dew Encryption Archives/.dew-encryption-repo`.
4. The app copies selected content under repo `files/`.
5. The app commits changed content.
6. The app creates a timestamped `.7z` archive beside the repo.
7. The user can keep the repo for ongoing history and the `.7z` file for portable archival.

## Browse history

1. Open the GUI History tab or run `dew-encryption history <repo>`.
2. Select or identify a commit.
3. Use GUI details or `dew-encryption details <repo> <commit>` to inspect metadata and changed files.

## Restore history

1. Identify the repo, source path, and commit.
2. In the GUI, select the commit and click `Restore Selected`; on CLI run `restore`.
3. Confirm the GUI warning if using the GUI.
4. The selected source path is replaced with the committed content.
