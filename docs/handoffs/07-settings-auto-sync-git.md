# Handoff: Scheduled settings auto-sync to a local git repo

| | |
|---|---|
| **Status** | Not started |
| **Source** | Local: `Services/ConfigBackupService.cs` (+ system `git`) |
| **License** | First-party WinForge code (project license). `git` is GPLv2 (invoked as an external CLI; no linking). |
| **Proposed module** | Extends existing **Config & Backup · 設定與備份** · System/Backup group · Tag `module.configbackup` (no new module) |
| **Effort** | M — service plumbing + a DispatcherTimer + new UI section; reuses existing snapshot/git code wholesale. |

## What the user asked for
Add scheduling to Config & Backup auto-sync: an interval unit (minute / hour / day) plus a numeric count (e.g. every 15 minutes), each tick writing the current config snapshot into a LOCAL git repo (`git init` if missing) and making a timestamped commit. Persist the schedule, run it via a `DispatcherTimer` while the app is open (and/or a scheduled task for background), optionally push to a remote, and add a manual "Sync now" button.

## Recommended approach
**Native C# clone (extend the existing module).** All plumbing already exists in `ConfigBackupService`: `InitSnapshotRepo`, `TakeSnapshot(message)`, `ListSnapshots`, plus the `Git(args)` helper that runs `git` via `ShellRunner.RunIn(SnapshotsDir, ...)`. The repo lives at `%LOCALAPPDATA%\WinForge\snapshots`. There is already a *daily* `schtasks` job (`ScheduleDailyBackup`/`DailyTaskName`) — this feature generalizes scheduling to an arbitrary interval and adds an in-process timer. v1 scope: interval scheduling + persisted config + in-app timer + Sync now + optional remote push. Background-while-closed continues to reuse the existing `--snapshot` schtasks path (no new background service needed).

## Features to implement (v1 → later)
- v1: New "Auto-sync schedule" UI block (unit ComboBox minute/hour/day + numeric NumberBox count + Enable toggle); persist via `SettingsStore.Set`; `DispatcherTimer` that calls `TakeSnapshot("auto-sync")` on tick; "Sync now" button; "last synced" status line.
- v1: Optional remote — text box for remote URL; `Git("remote add/set-url origin <url>")` + `Git("push -u origin HEAD")` after commit, with failures surfaced non-fatally.
- later: per-tick robocopy mirror; retry/backoff on push; commit only on change is already handled (TakeSnapshot returns Ok "No changes" when nothing to commit); credential handling via git credential manager.

## Integration plan (WinForge specifics)
- New files: none required. Add methods to `Services/ConfigBackupService.cs`: `SyncNow(remoteUrl?)`, `PushToRemote(url)`, `ScheduleAutoSync(unit, count)` (rewrites a single schtasks job, e.g. `/SC MINUTE /MO <n>` or `/SC HOURLY`/`/SC DAILY`). Add the timer + UI handlers to `Pages/ConfigBackupModule.xaml(.cs)` (extend the existing "Automate & mirror" section).
- Persist keys in `SettingsStore`: `backup.autosync.enabled`, `backup.autosync.unit`, `backup.autosync.count`, `backup.autosync.remote`, `backup.autosync.lastrun` (all stored as strings — SettingsStore is `Dictionary<string,string>`).
- Nav wiring: none — module already registered (ModuleRegistry line ~66, MapType + NavView_SelectionChanged cases for `module.configbackup` in `MainWindow.xaml.cs`). Update the ModuleRegistry `Keywords` to add `auto-sync interval push remote 自動同步`.
- Engine/install: winget id `n/a`. Detect `git` via `ShellRunner`; if missing, show `EngineBars.AutoInstallButton("Git.Git", "Install Git", "安裝 Git", recheck, rescan)` in an InfoBar.
- Key APIs/CLIs to call: existing `ConfigBackupService.TakeSnapshot` / `Git(...)`; `DispatcherTimer` (interval = count × unit; clamp minimum to ~1 min to avoid commit storms); `FileDialogs` only if exporting (not needed here).

## Dependencies & risks
- `git` must be on PATH; gate UI on detection and offer AutoInstallButton.
- DispatcherTimer only fires while the app runs; document that background coverage requires the schtasks job (reuse `--snapshot`). Keep both in sync so they don't double-commit.
- Remote push needs configured credentials; treat push failure as a warning, not a sync failure (commit already succeeded locally).
- Very short intervals create many empty-diff commits — TakeSnapshot already no-ops on "nothing to commit", so this is safe but note it in UI copy.

## Acceptance criteria
- Builds clean (Debug + Release x64); Config & Backup module shows the new auto-sync controls; enabling a schedule persists and survives restart; timer ticks produce timestamped git commits visible in the snapshot list; "Sync now" commits immediately; optional remote push works when a URL is set; every new string is bilingual (English + Cantonese); all pickers use `FileDialogs` (no WinRT pickers).
