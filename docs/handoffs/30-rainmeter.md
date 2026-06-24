# Handoff: Rainmeter (desktop widgets)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/rainmeter/rainmeter (C++) · CLI: `Rainmeter.exe` |
| **License** | GPL-2.0 (open source). WinForge only wraps the binary — no source linkage, so GPL does not infect WinForge. |
| **Proposed module** | Rainmeter Widgets · Personalization / Desktop group · Tag `module.rainmeter` |
| **Effort** | M — wrapping is straightforward; skin discovery + bang command UX is the bulk of the work. |

## What the user asked for
Add a WinForge module to install Rainmeter, manage installed skins (load/unload), browse and install skin packs, and toggle individual widgets — a manager front-end over the Rainmeter binary.

## Recommended approach
**CLI/binary wrap.** Rainmeter is a mature C++ desktop-rendering engine (~hundreds of KLOC, custom Lua, GDI+/Direct2D). Per the global strategy this is firmly in the "cannot reimplement in reasonable scope" bucket, so we install the official binary via winget and build a rich WinUI front-end around its command-line "!bang" interface.

Rainmeter is controlled by passing bang commands to `Rainmeter.exe` (a running instance receives them, e.g. `Rainmeter.exe !ActivateConfig "illustro\Clock" "Clock.ini"`). v1 scope: detect/install Rainmeter, enumerate skins from the Skins folder, list which configs are active, and run activate/deactivate/refresh/toggle bangs. Skin-pack browsing in v1 = a curated list of known `.rmskin` URLs the user can download + install; a full online catalog is "later."

## Features to implement (v1 -> later)
- v1: Detect install (registry `HKLM\SOFTWARE\Rainmeter` / winget); AutoInstallButton if missing.
- v1: Enumerate skins by scanning `%USERPROFILE%\Documents\Rainmeter\Skins\<Config>\*.ini`; show active vs inactive.
- v1: Load/unload skin via `!ActivateConfig` / `!DeactivateConfig`; `!RefreshApp`; toggle visibility via `!Hide`/`!Show`/`!ToggleConfig`.
- v1: Install a `.rmskin` pack chosen via FileDialogs by launching `SkinInstaller.exe "<path>"`.
- later: Curated online skin-pack catalog with download; per-skin variable editing; layout save/load (`!LoadLayout`); position/monitor placement; favorites.

## Integration plan (WinForge specifics)
- New files: `Services/RainmeterService.cs` (locate exe, parse Skins tree, run bangs via ShellRunner.Capture, parse `Rainmeter.ini` for active configs), `Pages/RainmeterModule.xaml(.cs)` (skin list + toggle buttons + install controls), optionally `Catalog/RainmeterOperations.cs` for common one-shot ops (Refresh All, Manage, Edit Settings) as Tweak.Cmd/Shell actions rendered by TweakCard.
- Nav wiring: add `NavigationViewItem Tag="module.rainmeter"` in MainWindow.xaml; add ModuleRegistry entry; wire Tag in MainWindow.xaml.cs MapType + NavView_SelectionChanged; add ApplyStartPage case for `--page rainmeter`.
- Engine/install: winget id `Rainmeter.Rainmeter` via `EngineBars.AutoInstallButton("Rainmeter.Rainmeter", "Install Rainmeter", "安裝 Rainmeter", recheck, rescan)`.
- Key CLIs/APIs: `Rainmeter.exe !<Bang> ...`, `SkinInstaller.exe`, ShellRunner.Run/Capture; FileDialogs for `.rmskin` picking; read active configs from `%APPDATA%\Rainmeter\Rainmeter.ini` (or Documents path).

## Dependencies & risks
- Rainmeter must be running for bangs to apply; service should launch it (or `!RefreshApp`) first.
- Skins folder path varies (Documents vs portable install) — resolve via registry `SkinPath`/`SettingsPath`, do not hardcode.
- Bangs fail silently if config/path is wrong; verify by re-reading Rainmeter.ini state rather than trusting exit code.
- No machine-readable skin API; online catalog (later) needs scraping/curation.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; can install Rainmeter, list skins, load/unload and toggle a skin, and install a `.rmskin`; all strings bilingual (English + 粵語); uses FileDialogs (no WinRT pickers).
