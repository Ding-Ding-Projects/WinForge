# Handoff: btop4win-style Resource Monitor

| | |
|---|---|
| **Status** | Not started |
| **Source** | Inspiration only (no binary): https://github.com/aristocratos/btop4win (C++) |
| **License** | btop4win is Apache-2.0. We are cloning the *look & behavior* natively, not shipping any of its code — no license obligation, but credit it as inspiration. |
| **Proposed module** | Extend existing "System Monitor" · System / Performance group · existing Tag `module.sysmon` (no new module) |
| **Effort** | M — all data already exists in `Services/SystemMonitor.cs`; effort is the btop-style XAML/graph rendering and process-table UX. |

## What the user asked for
Recreate the btop4win resource-monitor experience natively inside WinForge by extending the existing System Monitor module with btop-style visuals: per-core CPU bars, memory/swap meters, disk activity, network up/down graphs, and a sortable process list with tree view and kill — all driven by data WinForge already collects.

## Recommended approach
**Native C# clone (extend, don't add a module).** Per the global strategy, btop is a small TUI whose value is purely presentation over OS metrics — there is nothing here that needs a C++ binary. WinForge already has the engine: `Services/SystemMonitor.cs` exposes `CpuPercent()`, `Memory()`, `Network(seconds)`, `Sample(n, byCpu)` (per-PID CPU% + working set), `Kill`, `SetPriority`, `SetAffinity`, `SetEfficiency`, plus `LibreHardwareMonitorLib 0.9.4` (already referenced, used by `Services/BatteryThermal.cs`) for per-core load and temperatures. So v1 is a UI/visualization job layered on existing services, not new data plumbing. Realistic v1: a btop-styled dashboard inside `SystemMonitorModule` — braille/block-style sparkline graphs, per-core bars, colored meters, and a richer process grid.

## Features to implement (v1 → later)
- v1: Per-core CPU bars (one bar/core) + overall CPU sparkline; RAM + page-file meters; per-adapter network down/up sparklines with rate labels; sortable process grid (CPU/mem/PID/name) with right-click Kill / priority / efficiency-mode actions (services already exist); btop color theme (green→yellow→red gradient) honoring light/dark; adjustable refresh interval.
- later: process **tree** view (parent/child via PPID); CPU temperature + clock from LibreHardwareMonitor; per-disk I/O and free-space bars; filter/search box; pause/freeze; export snapshot; selectable color presets mirroring btop themes.

## Integration plan (WinForge specifics)
- New files: `Controls/SparklineGraph.cs` (lightweight `Canvas`/`Polyline` history graph) and `Controls/CoreBars.xaml(.cs)` (per-core bar strip). Reuse rather than duplicate where the existing module already binds metrics.
- Edit existing: `Pages/SystemMonitorModule.xaml(.cs)` — add the btop-style layout and process grid; keep the current `DispatcherTimer` sampling loop. Extend `Services/SystemMonitor.cs` only if needed (e.g. add a `PerCoreLoad()` helper backed by LibreHardwareMonitor, and PPID/parent for tree view).
- Nav wiring: **none new** — reuse existing `module.sysmon` nav item, `ModuleRegistry` entry, and `MapType`/`NavView_SelectionChanged`. Optionally add search keywords ("btop", "resource monitor", "監察") to the existing `ModuleRegistry` entry.
- Engine/install: **n/a** — no external binary; do not use `EngineBars.AutoInstallButton`.
- Key APIs to call: `SystemMonitor.Sample/Memory/Network/CpuPercent/Kill/SetPriority/SetEfficiency/SetAffinity`; `LibreHardwareMonitor` `Computer`/`Hardware`/`Sensor` (see `BatteryThermal.cs`) for per-core + temps.

## Dependencies & risks
- No new NuGet deps. LibreHardwareMonitor per-core/temp sensors require elevation on some machines — degrade gracefully (hide temp, keep `GetSystemTimes` CPU%) when unavailable.
- Per-frame `Process.GetProcesses()` is moderately heavy; cap refresh (default ~1s) and reuse the existing single timer.
- Smooth graphs in XAML: prefer cheap `Polyline`/`Win2D`-free drawing over per-tick layout thrash; cap history length.

## Acceptance criteria
- Builds clean (Debug + Release x64); the existing System Monitor module shows the new btop-style dashboard (per-core bars, meters, network graphs, process grid with working Kill/priority/efficiency).
- No new module/nav entry needed; deep-link `--page sysmon` still works.
- Every user-facing string is bilingual (English + 粵語); any file/folder access uses `FileDialogs` (no WinRT pickers); no external binary or winget install.
