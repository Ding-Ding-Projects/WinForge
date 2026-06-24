# Handoff: Windhawk (mod manager)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/ramensoftware/windhawk (C++ injection platform) |
| **License** | GPL-3.0 (Windhawk app is open source; individual mods carry their own, mostly permissive, licenses) |
| **Proposed module** | Windhawk Mods · Customize / Tweaks group · Tag `module.windhawk` |
| **Effort** | M — mostly a winget installer + launcher + curated catalog front-end; no engine to reimplement. |

## What the user asked for
Provide a WinForge module that installs Windhawk (winget `RamenSoftware.Windhawk`), launches it, and lets the
user browse and enable a curated list of popular Windhawk mods from inside WinForge. It is a manager/launcher
front-end, not a clone.

## Recommended approach
**CLI/binary wrap (manager front-end).** Per the global strategy, Windhawk genuinely cannot be reimplemented:
it is a C++ platform that compiles mods and injects DLLs into target processes (explorer.exe, taskbar, etc.)
via a system-wide hook engine and an elevated service. Cloning that is far out of scope and unsafe. So WinForge
should install the official binary via winget, launch its UI for the actual mod authoring/config experience,
and add value with a curated, bilingual catalog of popular mods plus deep-links. A realistic v1 = detect/install
Windhawk, launch it, and show a curated mod gallery whose "Open in Windhawk" buttons deep-link to each mod's
page (Windhawk supports `windhawk:` protocol / web mod pages at windhawk.net). Enabling/disabling mods
programmatically is possible but version-fragile (see risks); keep v1 launch-and-deep-link.

## Features to implement (v1 → later)
- v1: Detect Windhawk install (registry/`%ProgramFiles%\Windhawk`); AutoInstallButton via winget if missing;
  "Launch Windhawk" button; curated TweakCard gallery (~12 popular mods: Taskbar height/icon size, Taskbar
  Clock customization, Windows 11 Start Menu Styler, Aero Tray, Better File Sizes in Explorer, Disable rounded
  corners, Classic Taskbar, etc.) each with description, author, and "Open in Windhawk" deep-link.
- later: Read installed-mod state from Windhawk's `engine.ini`/settings folder to show enabled/disabled badges;
  one-click enable via Windhawk CLI if a stable interface exists; search across the full windhawk.net catalog;
  export/import a WinForge "mod profile".

## Integration plan (WinForge specifics)
- New files: `Services/WindhawkService.cs` (detect install path, version, launch, build `windhawk:` deep-links),
  `Pages/WindhawkModule.xaml(.cs)`, `Catalog/WindhawkMods.cs` (curated mod list as data-driven TweakDefinitions
  built with `Tweak.Action`/`Tweak.Shell` rendered by `Controls/TweakCard`).
- Nav wiring: `MainWindow.xaml` NavigationViewItem `Tag="module.windhawk"` under the Customize/Tweaks group;
  `Services/ModuleRegistry.cs` entry for master search; `MapType` + `NavView_SelectionChanged` + optional
  `ApplyStartPage` (`--page windhawk`) in `MainWindow.xaml.cs`.
- Engine/install: winget id `RamenSoftware.Windhawk` via `EngineBars.AutoInstallButton("RamenSoftware.Windhawk",
  en, zh, recheck, rescan)` shown when detection fails.
- Key APIs/CLIs to call: `ShellRunner.Run` to launch `Windhawk.exe`; `ShellRunner.Capture`/registry read for
  detection; launch `windhawk:` protocol URLs (or windhawk.net mod pages) via ShellRunner for deep-links. Use
  `FileDialogs` only if a "locate Windhawk.exe" fallback picker is needed.

## Dependencies & risks
- Windhawk requires elevation and installs a service; winget install may prompt UAC — surface that in the InfoBar.
- No documented stable public CLI for toggling mods; programmatic enable/disable is version-fragile — keep v1 to
  launch + deep-link, treat state-reading/toggling as best-effort later work.
- Curated mod list can go stale; store it as data in `Catalog/WindhawkMods.cs` so it is easy to update.
- GPL-3.0 binary is fine to install/launch (no linking); do not bundle or fork Windhawk code.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Customize; AutoInstallButton installs Windhawk
  when absent; "Launch Windhawk" works; curated mod gallery renders with working "Open in Windhawk" deep-links;
  all user-facing strings bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
