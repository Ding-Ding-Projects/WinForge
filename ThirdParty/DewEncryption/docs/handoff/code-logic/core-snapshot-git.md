# Code Logic Handoff: Core Snapshot and Git

## Functions

- `selected_root`: resolves selected files/folders and chooses the root for archive placement.
- `unique_paths`: de-duplicates path inputs with case-insensitive behavior on Windows.
- `archive_output_dir`: creates `Dew Encryption Archives`.
- `repo_for_source`: returns `Dew Encryption Archives/.dew-encryption-repo`.
- `ensure_repo`: initializes Git and local commit identity.
- `copy_selection`: rebuilds repo `files/` from selected paths.
- `commit_repo`: stages all changes and commits only when status is dirty.
- `compress_repo`: invokes 7-Zip with timestamped output and optional password/header encryption.
- `process`: full archive snapshot orchestration.
- `snapshot`: history-only snapshot orchestration.
- `history`, `commit_details`, `restore_commit`: read and restore Git history.

## Invariants

- The working tree for captured files is always `repo/files`.
- `commit_repo` returns the current commit even if no new commit was needed.
- Restore targets `files/<source.name>` inside a commit, so selection naming matters.
