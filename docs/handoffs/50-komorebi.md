# Handoff: Komorebi Tiling Window Manager

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/LGUG2Z/komorebi (Rust; `komorebic` CLI) |
| **License** | Open source — komorebi is published under a custom "Komorebi License" (source-available, free for individuals/small orgs; commercial sponsorship required for larger orgs). Note: NOT a standard OSI license — verify redistribution terms before bundling. We only invoke the user-installed binary, so this is low-risk. |
| **Proposed module** | Tiling WM (Komorebi) · Desktop / Window Management group · Tag `module.komorebi` |
| **Effort** | L — wrapping a stateful daemon, many CLI verbs, and a TOML/JSON config editor is broad, but each piece is straightforward shell-out work. |

## What the user asked for
Integrate the Komorebi tiling window manager into WinForge: install it via winget, start/stop the tiling daemon, switch layouts, manage workspaces and window rules, and edit the config — all from a WinUI control panel built over `komorebic`.

## Recommended approach
**CLI/binary wrap.** Komorebi is a substantial Rust project that hooks the Win32 windowing system and runs a long-lived daemon — reimplementing it natively in C# is far out of scope (per global strategy rule 2). The right move is a rich WinUI front-end over the `komorebic` CLI. A realistic v1 ships daemon lifecycle control, live status, layout switching, and workspace navigation. Deep config authoring (full schema editor) is later work.

## Features to implement (v1 → later)
- v1: Detect install (`komorebic --version`); AutoInstallButton for `LGUG2Z.komorebi`. Start/stop/restart daemon (`komorebic start --await-configuration` / `komorebic stop`). Live status via `komorebic state` (JSON) shown as monitors → workspaces → windows tree. Layout switcher per focused workspace (`komorebic change-layout <bsp|columns|rows|vertical-stack|horizontal-stack|ultrawide-vertical-stack|grid>`). Workspace focus/move (`komorebic focus-workspace <n>`, `move-to-workspace <n>`). Toggle tiling/float/monocle/pause (`komorebic toggle-tiling|toggle-float|toggle-monocle|toggle-pause`).
- later: Window rules manager (`komorebic float-rule`, `workspace-rule`, `ignore-rule`) with an add/list/remove UI. Config editor for `komorebi.json` (open via FileDialogs, validate, reload with `komorebic reload-configuration`). Autostart toggle (`komorebic enable-autostart`). Gaps/padding sliders (`komorebic workspace-padding`, `container-padding`). komorebi-bar integration note.

## Integration plan (WinForge specifics)
- New files: `Services/KomorebiService.cs` (wraps ShellRunner.Capture for `komorebic`, deserializes `komorebic state` JSON into monitor/workspace/window models, exposes IsInstalled/IsRunning/Start/Stop/SetLayout/FocusWorkspace), `Pages/KomorebiModule.xaml(.cs)` (status InfoBar + daemon controls + layout/workspace panels + state tree), optionally `Catalog/KomorebiOperations.cs` for the list of toggle/layout ops rendered via Controls/TweakCard.
- Nav wiring: add `NavigationViewItem` Tag `module.komorebi` in `MainWindow.xaml` under the window-management group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (MapType, NavView_SelectionChanged, and ApplyStartPage for `--page komorebi`).
- Engine/install: winget id `LGUG2Z.komorebi` via `EngineBars.AutoInstallButton("LGUG2Z.komorebi", "Install Komorebi", "安裝 Komorebi", recheck, rescan)`. Note: `komorebic` requires the `KOMOREBI_CONFIG_HOME` env / a config file; surface a "create default config" action if none exists.
- Key APIs/CLIs to call: `komorebic start/stop/restart`, `komorebic state` (parse JSON), `komorebic change-layout`, `komorebic focus-workspace`, `komorebic toggle-*`, `komorebic reload-configuration`, `komorebic enable-autostart`. Run all through ShellRunner; never block the UI thread — daemon start can take a second.

## Dependencies & risks
- Komorebi needs Windows borderless/animation tweaks (`komorebic global-state` assumes certain settings); a misconfigured daemon may rearrange the user's windows unexpectedly — show a clear warning before first start.
- License is source-available, not OSI; safe because we only launch the user-installed binary, but do not redistribute the binary.
- `komorebic state` JSON schema changes between versions — parse defensively; degrade gracefully if fields are missing.
- Requires AHK/whkd or built-in hotkeys for full UX; WinForge only manages the daemon, not keybindings (note this to users).
- Bilingual strings required throughout (English + 粵語) for every label, button, and InfoBar message.

## Acceptance criteria
- Builds clean (Debug + Release x64); `module.komorebi` appears in the left nav and master search; AutoInstallButton installs Komorebi when missing; daemon start/stop/restart works and status reflects reality; layout switch and workspace focus take effect; all user-facing strings are bilingual; FileDialogs (not WinRT pickers) used for any config-file picking.
