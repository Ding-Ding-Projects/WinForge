# Handoff: Windows Terminal Integration (profiles editor + embedded ConPTY terminal)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/microsoft/terminal (C++/C++/WinRT); CLI: `wt.exe`; settings file: `settings.json`; ConPTY via Win32 `CreatePseudoConsole` |
| **License** | microsoft/terminal = MIT (open source). The Microsoft-branded Store/winget build is redistributed under the Microsoft Software License, but we only launch `wt.exe`, edit its `settings.json`, and use the OS ConPTY API — no relicensing concern. |
| **Proposed module** | "Windows Terminal" (粵語: Windows 終端機) · Dev / Tools group · Tag `module.terminal` |
| **Effort** | L — the `settings.json` editor is M, but the embedded ConPTY terminal (PTY hosting + ANSI rendering + input/resize) is L and should be shared with the SSH module (handoff 02). |

## What the user asked for
WinForge already shells out to `wt.exe`. Go deeper: (1) a structured editor for Windows Terminal **profiles** (read/write `settings.json` — name, command line, starting dir, color scheme, font, icon, hidden), and (2) an **in-app terminal** that embeds a real shell (pwsh / cmd / wsl) via **ConPTY**, sharing the terminal control built for the SSH module. Install Windows Terminal via winget `Microsoft.WindowsTerminal` when missing.

## Recommended approach
**Hybrid.** Two parts with different strategies:

- **Profiles editor = native C# clone.** `settings.json` is plain JSON; we can fully read/edit it natively with `System.Text.Json` (use a DOM / `JsonNode` round-trip so unknown keys survive). No need to reimplement Windows Terminal itself — just a rich WinUI editor over its config. This satisfies the global strategy (clone the editable surface in C#).
- **Embedded terminal = ConPTY wrap.** A real terminal emulator is not worth reimplementing from scratch; instead host a child shell through the OS ConPTY API (`CreatePseudoConsole` / `ResizePseudoConsole`) and render its output in a shared WinUI terminal control. **Reuse `Controls/TerminalView` from handoff 02 (SSH)** — same ANSI parser + input + resize, just fed by a local PTY instead of an SSH `ShellStream`. Do not depend on Windows Terminal's own renderer; ConPTY is an OS feature independent of the `wt.exe` install.

Realistic v1: profiles editor + a single embedded ConPTY tab launching the user's chosen shell.

## Features to implement (v1 → later)
- v1: Locate `settings.json` (`%LOCALAPPDATA%\Packages\Microsoft.WindowsTerminal_8wekyb3d8bbwe\LocalState\settings.json`, plus unpackaged/Preview paths); list profiles with key fields (name, commandline, startingDirectory, colorScheme, font.face, icon, hidden); add / edit / delete / duplicate a profile; set default profile; pick startingDirectory/icon via `FileDialogs`; safe save (backup + atomic write, preserve unknown keys). Embedded terminal: one ConPTY session hosting a selected shell (pwsh/cmd/wsl) rendered in shared `TerminalView`; launch buttons. Detect-and-install Windows Terminal via AutoInstallButton.
- later: Color-scheme editor with live preview; multiple terminal tabs/panes; drag-reorder profiles; import/export profile fragments (`settings.d` JSON fragments); keybindings/actions editor; "open this folder/repo in Windows Terminal" tie-in from File/Git modules (`wt -d <dir> -p <profile>`); WSL distro detection to auto-generate profiles.

## Integration plan (WinForge specifics)
- New files: `Services/WindowsTerminalService.cs` (resolve/parse/save `settings.json` with `JsonNode`, enumerate profiles, locate `wt.exe`, build `wt` launch args); `Services/ConPtySession.cs` (P/Invoke `CreatePseudoConsole`/`ResizePseudoConsole`/`ClosePseudoConsole`, pipe I/O, spawn shell via `CreateProcess` + `STARTUPINFOEX`) — share with handoff 02; `Pages/TerminalModule.xaml(.cs)` (Profiles tab | Embedded terminal tab); optionally `Catalog/TerminalOperations.cs` for quick `wt` launch actions as `TweakCard`s.
- Nav wiring: add `NavigationViewItem Tag="module.terminal"` in `MainWindow.xaml` (Dev / Tools group); add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.terminal`).
- Engine/install: winget id **`Microsoft.WindowsTerminal`** via `EngineBars.AutoInstallButton("Microsoft.WindowsTerminal", "Install Windows Terminal", "安裝 Windows 終端機", recheck, rescan)`; `recheck` tests whether `wt.exe` resolves and/or `settings.json` exists. Note: the embedded ConPTY terminal does NOT require WT installed — gate only the profiles editor / `wt` launch on it.
- Key APIs/CLIs: `wt.exe` (`-p <profile>`, `-d <dir>`, `nt`/`sp` for tabs/panes); `kernel32!CreatePseudoConsole`, `ResizePseudoConsole`, `ClosePseudoConsole`, `CreatePipe`, `CreateProcess` with `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`; `System.Text.Json` `JsonNode`/`JsonDocument`; `Services/FileDialogs.cs` for all path/icon picks (never WinRT pickers).

## Dependencies & risks
- `settings.json` is hand-editable JSON with comments/trailing data and user-defined keys — must round-trip via DOM and preserve unknown fields, or you corrupt the user's config. Always back up before save and write atomically.
- Packaged vs unpackaged vs Preview install each use a different `settings.json` path and package family name — probe all; if Windows Terminal isn't installed there may be no file (offer to create one or disable the profiles tab).
- ConPTY correctness (resize, escape sequences, encoding, process teardown) is the hard part — coordinate the implementation with handoff 02 so both modules share one tested `ConPtySession` + `TerminalView`; mismatched ownership leaks console handles/processes.
- Editing `settings.json` while Windows Terminal is open: WT hot-reloads; ensure our write doesn't race its watcher (atomic replace, not in-place rewrite).
- Elevation: ConPTY child shell inherits WinForge's token — note that launching from an elevated WinForge yields an elevated shell.
- Every user-facing string needs English + Hong Kong Cantonese (粵語).

## Acceptance criteria
- Builds clean (Debug + Release x64); "Windows Terminal" module appears in nav and master search.
- Profiles tab loads `settings.json`, lists profiles, and add/edit/delete/duplicate + set-default round-trips correctly while preserving unknown keys (verified by diffing); a backup is written before save.
- Embedded terminal tab launches pwsh/cmd/wsl via ConPTY, renders output, accepts input, and resizes; the terminal control is the shared one from handoff 02.
- Missing Windows Terminal is detected and `EngineBars.AutoInstallButton` installs `Microsoft.WindowsTerminal`, after which the profiles tab works.
- All UI strings bilingual (English + Cantonese); all path/icon selection uses `FileDialogs` (Win32 COM), never WinRT pickers.
