# Handoff: Packer (image builder)

| | |
|---|---|
| **Status** | Not started |
| **Source** | Fork: https://github.com/codingmachineedge/packer (upstream: https://github.com/hashicorp/packer) · CLI: `packer` |
| **License** | BUSL-1.1 (Business Source License 1.1 — source-available, not OSI open source). The CLI binary is redistributed via winget; WinForge only shells out to it. |
| **Proposed module** | Packer (Image Builder) · Developer / DevOps group · Tag `module.packer` |
| **Effort** | L — CLI wrap is mechanical, but a rich template editor, variable/plugin management, and streaming build console make it a sizable WinUI surface. |

## What the user asked for
Wrap the `packer` CLI inside WinForge: manage HCL/JSON templates (`.pkr.hcl` / `.json`), run `packer init / validate / fmt / build`, edit variables and plugins, and stream build output into the in-app console. Install the CLI via winget `Hashicorp.Packer`.

## Recommended approach
**CLI/binary wrap.** Per the global strategy, Packer is a large Go codebase whose entire value is its plugin ecosystem (AWS, Azure, vSphere, QEMU, Docker, etc.) talking to external build infrastructure — reimplementing that in C# is out of scope and pointless. Instead build a first-class WinUI front-end over the `packer` binary. v1 scope: pick a working directory / template, run the four core subcommands, and surface streamed stdout/stderr in a live console. Native cloning is not appropriate here; the wrap IS the right call.

## Features to implement (v1 -> later)
- v1: Select working folder (FileDialogs); list `*.pkr.hcl` / `*.json` templates; buttons for `packer init`, `validate`, `fmt`, `build`; live streaming output console with cancel; auto-install of the binary if missing; show `packer version`.
- v1: Pass `-var key=value` and `-var-file=path.pkrvars.hcl` from a simple key/value editor.
- later: Built-in HCL text editor with syntax highlighting; `-only` / `-except` build target selection parsed from `packer inspect`; plugin management (`packer plugins installed/install/remove`); template scaffolding/snippets; per-build history and log export.

## Integration plan (WinForge specifics)
- New files: `Services/PackerService.cs` (locate `packer.exe`, build arg lists, run via ShellRunner with streamed capture, version check), `Pages/PackerModule.xaml(.cs)`, optionally `Catalog/PackerOperations.cs` for the init/validate/fmt/build actions as TweakDefinitions rendered by Controls/TweakCard.
- Nav wiring: add `NavigationViewItem Tag="module.packer"` in `MainWindow.xaml` under the Developer/DevOps group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page packer` deep-linking).
- Engine/install: winget id `Hashicorp.Packer` via `EngineBars.AutoInstallButton("Hashicorp.Packer", en, zh, recheck, rescan)` shown in an InfoBar when the binary is absent.
- Key CLIs to call: `packer version`, `packer init <dir>`, `packer fmt <dir>`, `packer validate [-var ...] <dir>`, `packer build [-var ...] [-only=...] <dir>`, `packer inspect <template>`, `packer plugins installed`. Run through `ShellRunner` (use Capture/streaming variant so output lands in the console).
- File access: ALWAYS use `Services/FileDialogs.cs` (Win32 COM) for folder/template/var-file pickers — never WinRT pickers (must work elevated).

## Dependencies & risks
- Requires `Hashicorp.Packer` on PATH; handle absent/old versions gracefully.
- Builds can be long-running and interactive (plugin downloads, cloud credentials/SSH); must support cancel and clear non-zero exit-code reporting. Never block the UI thread.
- Plugins need their own `packer init`; surface init failures clearly.
- BUSL-1.1: do not vendor/modify Packer source — only invoke the official binary.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; selecting a template and running init/validate/fmt/build streams output to the console with working cancel; auto-install InfoBar appears when the binary is missing; all user-facing strings are bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
