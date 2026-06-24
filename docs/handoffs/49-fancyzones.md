# Handoff: FancyZones (PowerToys zones)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/microsoft/PowerToys (FancyZones module, C++/C#) · winget `Microsoft.PowerToys` |
| **License** | MIT (PowerToys is open source; install/launch the official binary — do not fork the zone engine) |
| **Proposed module** | FancyZones · Customize / Tweaks group · Tag `module.fancyzones` |
| **Effort** | M — installer + launcher + deep-links + a small settings reader; no engine to reimplement. |

## What the user asked for
Add a WinForge module for FancyZones (window-tiling zones from PowerToys): install/launch PowerToys via winget
(`Microsoft.PowerToys`), enable the FancyZones module, open the zone editor, and document the layouts and snap
hotkeys. A manager/launcher front-end — the FancyZones zone engine itself stays native PowerToys.

## Recommended approach
**CLI/binary wrap (manager front-end).** Per the global strategy, FancyZones genuinely cannot be reimplemented:
it is a C++ Win32 module that hooks low-level mouse/keyboard input, draws zone overlays during drag, and snaps
windows via `SetWindowPos` across virtual desktops — all driven by the PowerToys runner and an elevated host.
Cloning that overlay/snap engine is out of scope and would duplicate a maintained MIT tool. Note WinForge already
has a separate native "PowerToys Extras" module (`Pages/PowerToysExtrasModule.xaml`) that clones *small* utilities
(Image Resizer, OCR, Always-On-Top, Paste-Plain) — FancyZones is the opposite case, so wrap it rather than extend
that file. Realistic v1 = detect/install PowerToys, launch it, open the FancyZones editor directly, toggle the
module on, and present a bilingual layouts + hotkeys reference.

## Features to implement (v1 → later)
- v1: Detect PowerToys install (registry `HKLM/HKCU ...\Uninstall` or `%ProgramFiles%\PowerToys\PowerToys.exe`);
  `EngineBars.AutoInstallButton` via winget when missing; "Launch PowerToys", "Open Zone Editor", and "Enable
  FancyZones" buttons; a bilingual reference card listing built-in layouts (Focus, Columns, Rows, Grid, Priority
  Grid) and snap hotkeys (hold **Shift** while dragging; **Win+Ctrl+Arrow** to move between zones).
- later: Read/write `%LOCALAPPDATA%\Microsoft\PowerToys\FancyZones\*.json` to show enabled state and saved
  custom layouts; toggle FancyZones via `settings.json`; import/export layout JSON through WinForge.

## Integration plan (WinForge specifics)
- New files: `Services/FancyZonesService.cs` (detect install path/version, launch PowerToys, open the zone editor
  via `PowerToys.exe` / settings deep-link, read FancyZones JSON), `Pages/FancyZonesModule.xaml(.cs)`. Optional
  `Catalog/FancyZonesOperations.cs` if the toggles/layout docs are expressed as data-driven `TweakDefinition`s
  rendered by `Controls/TweakCard`.
- Nav wiring: `MainWindow.xaml` NavigationViewItem `Tag="module.fancyzones"` under the Customize/Tweaks group;
  `Services/ModuleRegistry.cs` entry for master search; `MapType` + `NavView_SelectionChanged` (+ optional
  `ApplyStartPage` `--page fancyzones`) in `MainWindow.xaml.cs`.
- Engine/install: winget id `Microsoft.PowerToys` via `EngineBars.AutoInstallButton("Microsoft.PowerToys", en, zh,
  recheck, rescan)` shown when detection fails.
- Key APIs/CLIs to call: `ShellRunner.Run` to launch `PowerToys.exe`; `ShellRunner.Capture`/registry read for
  detection; open the zone editor (PowerToys exposes a FancyZones editor launch; fall back to launching PowerToys
  Settings on the FancyZones page). Use `FileDialogs` (never WinRT pickers) for any "locate PowerToys.exe" or
  layout-JSON import/export.

## Dependencies & risks
- PowerToys installs an elevated runner; winget install may prompt UAC — surface that in the InfoBar.
- No stable public CLI to toggle modules or open the editor; editor-launch and settings paths are version-fragile,
  so guard with detection and keep v1 to launch + deep-link + docs. Treat JSON read/write as best-effort later.
- FancyZones config JSON schema can change across PowerToys releases — parse defensively, never hard-fail.
- MIT binary is fine to install/launch; do not bundle or vendor PowerToys source into WinForge.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under Customize; AutoInstallButton installs PowerToys
  when absent; "Launch PowerToys" and "Open Zone Editor" work; layouts/hotkeys reference renders; all user-facing
  strings bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
