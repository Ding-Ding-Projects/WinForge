# Handoff: Nilesoft Shell (Context Menu Customizer)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/moudey/shell (native C++ Windows shell extension) |
| **License** | MIT (open source) |
| **Proposed module** | Nilesoft Shell · "Customization" / "Shell & Explorer" group · Tag `module.nilesoftshell` |
| **Effort** | M — wrapping + a focused config editor; no native reimplementation needed |

## What the user asked for
Provide a WinForge module that installs Nilesoft Shell (winget `Nilesoft.Shell`), can register/unregister it,
and lets the user edit its `shell.nss` configuration through templates and snippets. It should tie into the
existing Context Menu module.

## Recommended approach
**CLI/binary wrap + config editor.** Per the global strategy, Nilesoft Shell is a native C++ shell extension that
hooks into the Windows Explorer context menu — it cannot be reimplemented in C#/WinUI (the menu-replacement
behavior is the whole product, and it lives in `shell.dll` injected by Explorer). So we wrap the binary and build
a rich WinUI front-end around its install/register lifecycle and its `shell.nss` script. v1 scope: install via
winget, register/unregister/reload the extension, and a templated editor for `shell.nss` with curated snippets.
We are NOT writing an `.nss` language parser — we treat the file as text plus insertable snippet blocks.

## Features to implement (v1 → later)
- v1: Detect install (EngineBars + winget); AutoInstallButton for `Nilesoft.Shell`.
- v1: Register / Unregister / Reload buttons calling `shell.exe -register -treat -restart`, `-unregister`, and reload.
- v1: Locate `shell.nss` (default `%ProgramFiles%\Nilesoft Shell\shell.nss` or the install dir); open in a TextBox editor with Save + automatic timestamped backup before write.
- v1: Snippet/template gallery — common entries (Copy as path, Open PowerShell here, Take ownership, Run as admin, theme tweaks like `theme.dark`, `theme.modern`); insert at cursor.
- v1: "Restore default config" and "Backup / Restore" buttons.
- later: Syntax highlighting for `.nss`; live preview note; import/export config profiles; integration hooks so the existing Context Menu module can hand off to this editor.

## Integration plan (WinForge specifics)
- New files: `Services/NilesoftShellService.cs` (detect, register/unregister/reload, find + read/write `shell.nss`, backup), `Pages/NilesoftShellModule.xaml(.cs)`, `Catalog/NilesoftShellOperations.cs` (TweakDefinitions for register/unregister/reload/restart-explorer via `Tweak.Shell`/`Tweak.Action`, rendered with `Controls/TweakCard`).
- Nav wiring: add `NavigationViewItem Tag="module.nilesoftshell"` in `MainWindow.xaml` near the Context Menu module; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` in `MainWindow.xaml.cs`.
- Engine/install: winget id `Nilesoft.Shell` via `EngineBars.AutoInstallButton("Nilesoft.Shell", en, zh, recheck, rescan)` shown when not installed.
- Run CLIs through `ShellRunner.Run`/`RunCmd`/`Capture` (e.g. `shell.exe -register -treat -restart`). Use `FileDialogs` (never WinRT pickers) for "open a different .nss / pick backup folder". Install path can come via `PackageService`/`PackageManagerRegistry`.

## Dependencies & risks
- Register/unregister and restarting Explorer require elevation; surface a clear elevation prompt and tolerate the launching shell being elevated already.
- `shell.nss` lives under `%ProgramFiles%` — writes need admin; ALWAYS back up before overwrite.
- Reload/restart of Explorer briefly closes Explorer windows — warn the user via InfoBar.
- Install path may vary; resolve dynamically (winget query / registry) rather than hardcoding.
- Bilingual: every button, InfoBar, dialog, and snippet description needs English + Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav under the customization group.
- AutoInstallButton installs `Nilesoft.Shell`; Register/Unregister/Reload work and reflect current state.
- `shell.nss` opens, edits, backs up, and saves; snippets insert correctly; restore-default works.
- All user-facing strings are bilingual (English + Cantonese); no WinRT pickers (FileDialogs only).
