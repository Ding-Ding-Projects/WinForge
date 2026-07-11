# Feature Handoff: File History

## Purpose

File history commits selected file/folder content to the same local Git repo without requiring a `.7z` archive on every run. It supports manual snapshots, polling auto-history, commit list display, commit details, archive creation, and restore.

## User-facing surfaces

- CLI: `watch`, `history`, `details`, and `restore`.
- Python GUI History tab: `Refresh History`, `Snapshot Now`, `Start Auto History`, `Stop Auto History`, `Create Archive`, `Restore Selected`, `Open Source`, `Open Repo`.
- Windows Explorer folder actions: `dew encryption start file history` and `dew encryption file history manager`.
- Linux Nautilus scripts: `dew encryption start file history` and `dew encryption file history manager`.

## Code path

- `snapshot(paths, label=None)` creates/updates the Git repo and commits copied content.
- `watch(paths, interval, once)` repeatedly calls `snapshot` and prints new commits.
- `history(repo, limit)` formats recent Git commits.
- `commit_details(repo, commit)` returns commit metadata and changed-file rows.
- `restore_commit(repo, commit, source)` uses `git archive` to extract the committed `files/<source.name>` tree and replace the source.

## Restore behavior

- Directory restore clears existing children except the archive directory, then copies restored children into place.
- File restore creates the parent directory if needed and copies the committed file back.
- Restore asks for GUI confirmation before replacing files.

## Change checklist

- Treat restore as high-risk: add tests before changing replacement semantics.
- Keep history output tab-delimited unless all callers are updated.
- Auto-history is polling based; changing intervals or debouncing affects GUI and shell watchers.
