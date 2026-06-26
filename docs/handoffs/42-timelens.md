# Handoff: TimeLens (Activity Timeline)

| | |
|---|---|
| **Status** | Implemented; default-on tracking and crash-safe open-segment recovery added. |
| **Source** | https://github.com/0pandadev/timelens (Rust + Tauri tray client for the hosted timelens.wierway.ch service) |
| **License** | No LICENSE file in repo (treated as "all rights reserved"); we are *not* reusing its code, only cloning the concept — no license obligation. |
| **Proposed module** | Activity Timeline · "Productivity / Insights" nav group · Tag `module.timelens` |
| **Effort** | M — active-window polling + idle detection + SQLite store + timeline UI are all standard .NET/WinUI work; no kernel or cloud pieces needed for v1. |

## What the user asked for
Bring TimeLens-style automatic time tracking into WinForge: silently record which app/window is in the foreground over the day and present a readable timeline plus per-app totals, so the user can see where their time goes — all local, inside the WinForge GUI.

## Recommended approach
**Native C# clone.** Per the global strategy, this is squarely reimplementable. TimeLens itself is a thin Tauri tray client whose only real job is "poll the active window, log it, and sync to a proprietary hosted backend." The hosted backend (timelens.wierway.ch) is closed, account-gated, and not something we want WinForge depending on. So we clone the *local* value (passive activity capture + visualization) natively and **drop the cloud sync** entirely — data stays on-device in SQLite. No wrap, no binary, no winget dependency.

Realistic v1: a foreground-app sampler running while WinForge is open, writing time segments to a local store, with a day view showing a stacked timeline bar and a sorted per-app totals list. The window-close path hides WinForge to the tray so tracking continues; explicit Quit, sleep, shutdown, or a crash flushes/recover-checkpoints the open segment. Honest limit: capturing time after the WinForge process is fully stopped still requires a background agent/scheduled task — defer that to "later." Per-document/URL detail and cross-device sync are explicitly out of scope.

## Features to implement (v1 → later)
- v1: Foreground-window sampler (poll every ~2–5 s) capturing process name + window title; idle detection (pause logging after N minutes of no input); local JSONL persistence of time segments plus an open-segment checkpoint; "Today" timeline view (stacked bar by hour) + sorted per-app totals; default-on start/stop tracking toggle and a privacy "pause" button; date picker to review past days.
- later: Background tracking when WinForge is closed (Scheduled Task / lightweight tray agent); category rules (map apps → Work/Leisure tags); weekly/monthly rollups and CSV export; optional self-hosted sync; idle-time "what were you doing?" prompts.

## Integration plan (WinForge specifics)
- Files: `Services/ActivityTrackerService.cs` (singleton sampler + idle detection + crash/sleep/session-ending flush hooks), `Services/ActivityStore.cs` (daily JSONL files plus `current.json` checkpoint under `%LOCALAPPDATA%\WinForge\activity\`), `Pages/TimeLensModule.xaml(.cs)` (timeline + totals UI).
- Nav wiring: add `NavigationViewItem Tag="module.timelens"` in `MainWindow.xaml` under the Productivity/Insights group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` (`--page timelens`).
- Engine/install: winget id **n/a** — no external binary; no `EngineBars.AutoInstallButton` needed.
- Key APIs to call: Win32 `GetForegroundWindow` + `GetWindowThreadProcessId` (P/Invoke) → `Process.GetProcessById` for process name; `GetWindowText` for title; `GetLastInputInfo` for idle detection; a `DispatcherTimer`/`PeriodicTimer` for the poll loop; `Microsoft.Data.Sqlite` for storage. For CSV export use **FileDialogs (Services/FileDialogs.cs)** — never WinRT pickers.

## Dependencies & risks
- Privacy: window titles can contain sensitive text — store locally only, keep the default-on posture obvious in the UI, provide pause/off controls, and provide a clear-history action.
- `Microsoft.Data.Sqlite` adds a NuGet dependency; confirm it builds clean under .NET 11 / WinUI 3 x64.
- Sampling is best-effort: locked screen, UAC desktop, and full-screen exclusive apps may report empty/odd titles — guard against nulls and bucket as "Idle/Unknown."
- Background tracking after the WinForge process is explicitly stopped (later) needs a separate process or Scheduled Task; do not promise it in v1.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and via `--page timelens`; tracking starts by default unless disabled; toggling tracking records foreground segments and they persist across app restarts; the open segment is flushed on normal shutdown and recovered after unexpected close; "Today" timeline and per-app totals render correctly; idle periods are excluded; CSV export uses FileDialogs (no WinRT pickers); every user-facing string is bilingual (English + 粵語, e.g. "Activity Timeline" / "活動時間軸", "Tracking paused" / "已暫停追蹤", "Idle" / "閒置").
