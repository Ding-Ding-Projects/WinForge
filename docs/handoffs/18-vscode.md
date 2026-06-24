# Handoff: VS Code Integration

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `code` (Visual Studio Code shell command); repo github.com/codingmachineedge/vscode (upstream: github.com/microsoft/vscode, Electron/TypeScript) |
| **License** | VS Code source (microsoft/vscode) = MIT (open source). Note: the official Microsoft-branded *binary* is under the proprietary Microsoft Software License; winget installs that build. We only shell out to its `code` CLI, so no relicensing concern. |
| **Proposed module** | "VS Code" (粵語: VS Code 編輯器) · Dev / Tools group · Tag `module.vscode` |
| **Effort** | S — thin wrapper over the `code` CLI plus a WinUI front-end; no embedding or native engine work. |

## What the user asked for
Integrate VS Code into WinForge by wrapping the `code` CLI: open files/folders/workspaces, install/list/uninstall extensions, diff two files, open a new window or the integrated terminal, and tie into the Git module ("open this repo in VS Code"). Install the editor via winget `Microsoft.VisualStudioCode` when missing.

## Recommended approach
**CLI/binary wrap.** VS Code is a large Electron/TypeScript application — it cannot be reimplemented natively in C# in any reasonable scope, so per the global strategy we wrap its first-class `code` command line and build a rich WinUI front-end around it. No external redirect beyond launching the editor the user already runs. All actions go through `ShellRunner` calling `code` (resolved on PATH, or via the Programs install path `%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd`).

Realistic v1: an Actions page that opens files/folders/workspaces (picked via `FileDialogs`), opens a new window, opens the integrated terminal at a folder, diffs two picked files, and an Extensions panel that lists installed extensions and installs/uninstalls by ID. Detect-and-install VS Code via `EngineBars.AutoInstallButton` when `code` is absent.

## Features to implement (v1 → later)
- v1: Open file / folder / `.code-workspace` (FileDialogs pickers) → `code <path>`; open in new window (`code -n`), reuse window (`code -r`); open integrated terminal at a folder (`code <dir>` then user runs terminal, or launch with `--`); diff two files (`code --diff a b`); goto file:line (`code -g file:line`); Extensions list (`code --list-extensions --show-versions`), install (`code --install-extension <id>`), uninstall (`code --uninstall-extension <id>`); detect missing `code` and surface AutoInstallButton.
- later: Profiles (`--profile`), portable/Insiders toggle (`code-insiders`), `code tunnel` (remote dev) controls, extension search via Marketplace API with one-click install, settings.json / keybindings.json quick-edit, recent-folders list, export/import extension set (read list → batch `--install-extension`).

## Integration plan (WinForge specifics)
- New files: `Services/VsCodeService.cs` (resolve `code` path, all CLI calls via `ShellRunner.Capture`/`Run`, parse `--list-extensions` output); `Pages/VsCodeModule.xaml` + `.cs` (Actions pane + Extensions list with install/uninstall); optionally `Catalog/VsCodeOperations.cs` for the open/diff/new-window ops as `TweakCard`-rendered actions (`Tweak.Action`/`Tweak.Cmd`).
- Nav wiring: add `NavigationViewItem Tag="module.vscode"` in `MainWindow.xaml` under the Dev/Tools group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.vscode`).
- Engine/install: winget id **`Microsoft.VisualStudioCode`** via `EngineBars.AutoInstallButton("Microsoft.VisualStudioCode", "Install VS Code", "安裝 VS Code", recheck, rescan)`; the `recheck` predicate should test whether `code` resolves on PATH / Programs path.
- Git module tie-in: add an "Open in VS Code" action in the Git module that calls `VsCodeService.OpenFolder(repoPath)` (`code <repoPath>`); reuse the same service so both modules share path resolution and the missing-`code` guard.
- Key APIs/CLIs: `code <path>`, `-n`/`-r`/`-g file:line`/`--diff a b`/`--wait`, `--list-extensions [--show-versions]`, `--install-extension <id>`, `--uninstall-extension <id>`, `--version`; `Services/FileDialogs.cs` for all file/folder/workspace selection.

## Dependencies & risks
- `code` may not be on PATH even when VS Code is installed (user-scope install) — resolve via `%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\code.cmd` fallback; handle Insiders separately.
- `code` is `code.cmd` (a batch shim) — invoke through `cmd /c` / `ShellRunner.RunCmd` rather than CreateProcess directly.
- Some `code` invocations return immediately (launch the GUI); use `--wait` only where blocking is intended (diff-as-tool).
- winget install needs the source available and may require a re-scan/PATH refresh before `code` resolves — `rescan` callback must re-probe.
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); "VS Code" module appears in the nav and master search.
- Core flow works: open a file/folder via FileDialogs, open new window, diff two files, list installed extensions, install and uninstall an extension by ID.
- Missing `code` is detected and the winget AutoInstallButton installs `Microsoft.VisualStudioCode`, after which actions work.
- Git module shows a working "Open in VS Code" action backed by the shared `VsCodeService`.
- All UI strings bilingual (English + Cantonese); file/folder/workspace selection uses `FileDialogs` (Win32 COM), never WinRT pickers.
