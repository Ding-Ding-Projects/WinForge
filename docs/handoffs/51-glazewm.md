# Handoff: GlazeWM Tiling Window Manager

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/glzr-io/glazewm (C# core daemon + Rust components) · CLI: `glazewm` |
| **License** | GPL-3.0 (open source) |
| **Proposed module** | "GlazeWM Tiling" · left-nav group **Window Management** (alongside AltSnap / FancyZones / Komorebi) · Tag `module.glazewm` |
| **Effort** | M — wrapping install/start/stop/reload is small; the in-app config editor (keybindings, gaps, workspaces) is the bulk of the work. |

## What the user asked for
Wrap GlazeWM inside WinForge: install it via winget, start/stop the daemon, edit its config (keybindings, gaps, workspaces) in-app, and reload — all behind a WinUI control panel. Group it with the other window-management tools.

## Recommended approach
**CLI/binary wrap** (per global strategy rule 2). GlazeWM is a tiling window manager that hooks low-level Win32 window events and runs as a persistent background daemon — reimplementing that natively in C#/WinUI is out of scope and pointless when the official binary is mature. Wrap it instead: install via winget, control the process, and build a rich WinUI front-end over its config file plus its `glazewm` CLI/IPC.

Realistic v1: a control panel that detects/installs GlazeWM, shows running state, starts/stops/reloads it, and provides a structured editor for the most-used config sections (gaps, keybindings, workspaces) with a raw-text fallback for everything else.

## Features to implement (v1 → later)
- v1: Detect install (where `glazewm.exe` lives / winget presence). AutoInstallButton for `glzr-io.glazewm`.
- v1: Process control — Start (`glazewm.exe`), Stop (kill process / `glazewm command wm-exit`), Reload (`glazewm command wm-reload-config`). Show running status InfoBar.
- v1: Config editor — locate `%USERPROFILE%/.glzr/glazewm/config.yaml`, parse YAML, expose structured controls for `gaps.inner_gap`/`outer_gap`, `general.focus_on_hover`, `general.startup_commands`, and a workspaces list editor. Save + auto-reload.
- v1: Raw YAML text editor with Save (always available for unmodeled keys).
- later: Keybinding editor grid (bindings -> commands) with conflict detection.
- later: "Start with Windows" toggle (Startup folder / Task Scheduler) and live state via the WebSocket IPC (`ws://localhost:6123`) to show current workspace/focused window.

## Integration plan (WinForge specifics)
- New files: `Services/GlazeWmService.cs` (locate exe, install check, start/stop/reload via ShellRunner, read/write config path), `Pages/GlazeWmModule.xaml(.cs)`, optionally `Catalog/GlazeWmOperations.cs` (TweakDefinitions for the start/stop/reload/toggle-startup ops rendered via TweakCard).
- Nav wiring: add NavigationViewItem `Tag="module.glazewm"` in MainWindow.xaml under Window Management; add ModuleRegistry entry (Services/ModuleRegistry.cs) for master search; wire Tag in MainWindow.xaml.cs (MapType, NavView_SelectionChanged, and ApplyStartPage for `--page glazewm`).
- Engine/install: winget id `glzr-io.glazewm` via `EngineBars.AutoInstallButton("glzr-io.glazewm", en, zh, recheck, rescan)`.
- File picking: if user wants a non-default config path, use **FileDialogs** (never WinRT pickers) — works under elevation.
- Key APIs/CLIs: `glazewm.exe` (launch daemon), `glazewm command wm-reload-config`, `glazewm command wm-exit`, `glazewm query ...` for state; config at `~/.glzr/glazewm/config.yaml`; live IPC WebSocket on port 6123 (later).
- Use ShellRunner.Run/Capture for all CLI calls.

## Dependencies & risks
- Config format is YAML — needs a YAML parser (YamlDotNet) or careful manual edit; round-tripping must preserve comments/unmodeled keys, so prefer a "structured + raw" hybrid rather than full re-serialization.
- Config path and CLI command names have shifted across major versions — detect installed version and degrade gracefully.
- GLP-3.0: shipping the binary in-tree is discouraged; install via winget only, do not bundle.
- GlazeWM aggressively manages all windows once started — warn the user before first start.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the Window Management nav group.
- Core flow works: install via winget button, start/stop/reload daemon, edit a gap value and a workspace, save, daemon picks up changes.
- All user-facing strings bilingual (English + 粵語).
- No WinRT pickers — FileDialogs only.
