# Handoff: 7+ Taskbar Tweaker (registry tweaks panel + optional wrap)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/m417z/7-Taskbar-Tweaker (C++, runtime DLL injection/hooks into explorer.exe) |
| **License** | Freeware, closed-source (binary only); no public source license. Do NOT bundle/redistribute the installer — launch it only if the user already installed it. |
| **Proposed module** | Taskbar Tweaker · Customization / Taskbar group · Tag `module.taskbar-tweaker` |
| **Effort** | S–M — the registry tweaks already exist in `Catalog/TaskbarTweaks.cs`; this is mostly a dedicated page + a detect/launch helper for the real tool. |

## What the user asked for
Bring 7+ Taskbar Tweaker's functionality into WinForge. Surface the registry-backed taskbar tweaks WinForge can actually do (combine buttons, alignment, small icons, tray, multi-monitor, etc.), and honestly document that the deep runtime behaviours (middle-click to close, double-click to show desktop, scroll-to-switch, drag-reorder hooks) cannot be reimplemented in C# and require the real tool or Windhawk mods (see handoff 29).

## Recommended approach
**Hybrid (native subset + optional wrap).** 7+ Taskbar Tweaker works by injecting a DLL into `explorer.exe` and patching the taskbar's internal window procedures at runtime — there is no registry/API surface for most of its features, so they are genuinely not reimplementable in managed C#. Per the global strategy we therefore: (1) build a **native** WinForge panel for every tweak that maps to a real registry value or `ms-settings:` command — these already live in `Catalog/TaskbarTweaks.cs`; and (2) detect whether 7+TT (or Windhawk) is installed and offer to **launch** it for the deep behaviours, with no external redirect beyond that launch. v1 = a clean dedicated page rendering the existing tweaks via `TweakCard`, plus a detect-and-launch InfoBar.

## Features to implement (v1 → later)
- v1: Dedicated `Taskbar Tweaker` page rendering all `TaskbarTweaks.All()` tweaks through `Controls/TweakCard` (alignment, combine buttons, search mode, Task View / Widgets / Copilot, End Task, tray icons, multi-monitor, seconds clock, Start menu toggles, "Open Taskbar settings"). Detect 7+TT install (registry uninstall key + default install path) and Windhawk; show an InfoBar with a Launch button if present.
- later: A "deep behaviours need a runtime hook" explainer card cross-linking handoff 29 (Windhawk mods); per-tweak "applied" badges; an `EngineBars.AutoInstallButton` for Windhawk via winget; restart-Explorer convenience button (already supported via `RestartScope.Explorer`).

## Integration plan (WinForge specifics)
- New files: `Pages/TaskbarTweakerModule.xaml(+.cs)` (an `ItemsControl`/`ItemsRepeater` of `TweakCard` bound to `TaskbarTweaks.All()`, plus a top InfoBar); `Services/TaskbarTweakerService.cs` (detect 7+TT/Windhawk via `Microsoft.Win32.Registry` uninstall keys + path probe; `Launch()` using `ShellRunner.Run`). Reuse existing `Catalog/TaskbarTweaks.cs` as-is — do not duplicate the tweaks.
- Nav wiring: add `NavigationViewItem` Tag `module.taskbar-tweaker` in `MainWindow.xaml` (Customization/Taskbar group); add a `ModuleRegistry` entry in `Services/ModuleRegistry.cs`; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.taskbar-tweaker`).
- Engine/install: 7+TT has no winget id (`n/a`) and is freeware — never auto-install or bundle it. Optionally offer Windhawk via `EngineBars.AutoInstallButton` (winget id `RamenSoftware.Windhawk`) for the deep behaviours.
- Key APIs/CLIs to call: `ShellRunner.Run` to launch a detected `7+ Taskbar Tweaker.exe`; `Registry` reads for detection; existing `TweakCard` state-read logic. If a file picker is ever needed, use `FileDialogs` — never WinRT pickers.

## Dependencies & risks
- Deep features are impossible natively (runtime explorer injection) — be explicit in the UI so users aren't misled.
- 7+TT is closed-source freeware and unmaintained for newer Windows 11 builds; do not rely on it, only detect+launch.
- Some registry tweaks need an Explorer restart to take effect (handled by `RestartScope.Explorer`).
- Avoid duplicating tweaks already shown elsewhere if `TaskbarTweaks` is surfaced in another module.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; all `TaskbarTweaks` tweaks render and apply via `TweakCard`; 7+TT/Windhawk detection + launch works when installed and degrades gracefully when not; deep-behaviour limitation is clearly documented in-app with a link to handoff 29; all user-facing strings bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
