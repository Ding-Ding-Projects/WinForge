# Feature Handoff: Archive Snapshots

## Purpose

Archive snapshots let a user select one or more files/folders, copy the selection into a local Git working tree, commit changed content, and compress the repository into a timestamped `.7z` archive.

## User-facing surfaces

- CLI default command: `dew-encryption <paths...> [--password <password>]`.
- Python GUI Files tab: `Add Files`, `Add Folder`, `Remove`, and `Run Dew Encryption`.
- Windows Explorer: `dew encryption` on files, folders, and folder backgrounds.
- Linux Nautilus: `dew encryption` script.

## Code path

1. The CLI parser falls through to the default command and calls `process([Path(...)], password=args.password)`.
2. `process` resolves Git and 7-Zip, identifies the selected root, creates/opens the archive repo, copies the selection, commits it, and compresses the repo.
3. The output directory is named `Dew Encryption Archives` beside a selected file or inside a selected folder.
4. The Git repo is `Dew Encryption Archives/.dew-encryption-repo`.
5. The archive name is `dew-encryption-YYYYMMDD-HHMMSS.7z`.

## Important implementation details

- Multi-selection common-root logic uses file parents for files and the directory itself for directories.
- Snapshot content is copied under a repo `files/` folder.
- Folder copies ignore `.git`, `.dew-encryption-repo`, and `Dew Encryption Archives` to avoid recursive archive capture.
- Compression can be password-protected; a password enables 7-Zip header encryption.

## Change checklist

- If output naming changes, update README, installer expectations, docs pages, and smoke tests.
- If CLI flags change, update Python GUI, C# GUI, Explorer registry commands, Linux scripts, and CI.
- If copy/ignore behavior changes, add smoke tests for nested archive folders and multi-select paths.
