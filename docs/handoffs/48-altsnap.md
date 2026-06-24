# Handoff: AltSnap (modifier-drag window move/resize)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/RamonUnch/AltSnap (C, native low-level mouse/keyboard hooks) |
| **License** | GPL-2.0-or-later (open source). Fine to install/launch via winget; do NOT fork or relink its code into WinForge. |
| **Proposed module** | AltSnap · Tweaks & Input (調校與輸入) group · Tag `module.altsnap` |
| **Effort** | M — no engine to reimplement; mostly winget install + launcher + run-at-startup + a config front-end (parsing/writing AltSnap.ini). |

## What the user asked for
Bring AltSnap into WinForge as a manager module: install it (winget `RamonUnch.AltSnap`), launch/quit it, enable run-at-startup, and edit its configuration from a WinForge front-end. AltSnap lets you move and resize any window by holding a modifier (Alt by default) and dragging anywhere in the window — classic Linux-style "alt-drag." Group it next to the existing Window Manager and other window/input tools.

## Recommended approach
**CLI/binary wrap (manager front-end).** Per the global strategy, AltSnap genuinely cannot be cleanly reimplemented in managed C#: its whole value is a global low-level mouse hook (`WH_MOUSE_LL`) plus keyboard-state tracking that must intercept and consume input system-wide, in-process, with tight latency — doing that from a WinUI 3 app would be fragile, would fight UIPI/elevation boundaries, and would duplicate years of edge-case handling (per-monitor DPI, snap zones, multi-window, transparency, blacklists). So WinForge installs the official binary via winget, controls its lifecycle (launch/quit/restart, run-at-startup), and adds value with a bilingual front-end over its INI config. v1 = detect/install, launch & run-at-startup toggle, and a curated editor for the most-used `AltSnap.ini` keys.

## Features to implement (v1 → later)
- v1: Detect install (registry uninstall key + `%ProgramFiles%\AltSnap\AltSnap.exe` probe); `EngineBars.AutoInstallButton` when missing; Launch / Quit / Restart buttons; "Run at startup" toggle (HKCU `...\Run` value or Startup-folder shortcut); a config panel editing the high-value `AltSnap.ini` keys (modifier key, MoveUp/ResizeUp/right-click action, AeroTopMaximizes, AutoSnap, snap threshold, multi-monitor, blacklist) rendered as `TweakCard`s.
- later: Full INI section browser with raw-edit fallback; import/export config via `FileDialogs`; "is AltSnap running?" live status; profile presets; restart-on-save; surface AltSnap's own GUI settings as a "Open advanced settings" launch.

## Integration plan (WinForge specifics)
- New files: `Services/AltSnapService.cs` (detect path/version, Launch/Quit/Restart via `ShellRunner.Run`, run-at-startup read/write via `Microsoft.Win32.Registry`, locate + read/write `AltSnap.ini`), `Pages/AltSnapModule.xaml(.cs)`, `Catalog/AltSnapOptions.cs` (curated INI keys as data-driven `TweakDefinition`s built with `Tweak.Action`, rendered by `Controls/TweakCard`).
- Nav wiring: add `NavigationViewItem Content="AltSnap · Alt 拖曳視窗" Tag="module.altsnap"` in `MainWindow.xaml` under the **Tweaks & Input · 調校與輸入** group (near `module.windows`); add a `ModuleRegistry` entry in `Services/ModuleRegistry.cs`; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.altsnap`).
- Engine/install: winget id `RamonUnch.AltSnap` via `EngineBars.AutoInstallButton("RamonUnch.AltSnap", en, zh, recheck, rescan)` shown when detection fails.
- Key APIs/CLIs to call: `ShellRunner.Run` to launch/quit `AltSnap.exe` (it accepts CLI/IPC flags such as quit/elevate); `ShellRunner.Capture`/registry for detection; INI read/write (config typically at the install dir or `%APPDATA%`); `FileDialogs` only for any locate/import/export picker — never WinRT pickers.

## Dependencies & risks
- AltSnap needs to run elevated to control elevated/admin windows; winget install and elevated launch may trigger UAC — surface this in the InfoBar.
- INI schema can change between releases; keep curated keys in `Catalog/AltSnapOptions.cs` as data and offer a raw-edit fallback so the module degrades gracefully.
- Config changes require an AltSnap restart to take effect — wire a restart-after-save path.
- Only one hook owner at a time; warn if a conflicting tool (e.g. legacy AltDrag) is detected.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Tweaks & Input; AutoInstallButton installs AltSnap when absent; Launch/Quit/Restart and Run-at-startup toggle work; curated `AltSnap.ini` options edit and persist via `TweakCard`; degrades gracefully when not installed; all user-facing strings bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
