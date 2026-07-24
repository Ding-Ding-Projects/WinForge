# Repository Scan Handoff

## Scan scope

This document summarizes the entire repository as scanned on the `work` branch. The scan included tracked files, local/all branches reported by Git, and the current reachable commit history.

## Branches and history

`git branch --all` reported only the current local branch:

```text
* work
```

`git log --oneline --decorate --all --max-count=30` reported these reachable commits:

```text
f90196f (HEAD -> work) Merge pull request #3 from codingmachineedge/codex/add-features-for-encryption-containers
1f025dc Merge branch 'main' into codex/add-features-for-encryption-containers
5b6144f Add C# GUI target
21e73ec Merge pull request #2 from codingmachineedge/codex/add-features-for-encryption-containers
bd322ad Update docs screenshots and pages
f145542 Merge pull request #1 from codingmachineedge/codex/add-features-for-encryption-containers
20ccc5a Add container file history management
c117c11 Support multi select VeraCrypt actions
ddfbde3 Fix elevated task installer launchers
5368ef7 Add highest privilege task setup actions
efa4265 Remember VeraCrypt settings and add portable mode
bbf2eea Add VeraCrypt encrypt decrypt context actions
6669c74 Add Windows installer Linux support and generated icons
fb36002 Add Explorer folder history manager actions
5c21455 Add file history browser and auto snapshots
49a1e86 Add automated installer and GitHub Pages
77a03ad Initial Dew Encryption app
```

## Top-level areas

- `.github/workflows/`: CI, release, and GitHub Pages automation.
- `assets/icons/`: PNG/ICO assets for the app and shell actions.
- `csharp/DewEncryption.Gui/`: Avalonia/.NET GUI shell that invokes the Python CLI.
- `dew_encryption/`: Python package with core CLI logic and Tkinter GUI.
- `docs/`: public web documentation plus this handoff package.
- `installer/`: Windows context-menu, scheduled-task, install, uninstall, and Inno Setup scripts.
- `linux/`: Linux installer, desktop file, and Nautilus scripts.
- `scripts/`: Windows installer build helper.

## Entry points

- Python CLI script: `dew-encryption = dew_encryption.core:main`.
- Python GUI script: `dew-encryption-gui = dew_encryption.gui:main`.
- Module entry: `python -m dew_encryption`, which forwards to `dew_encryption.core.main`.
- C# GUI entry: `csharp/DewEncryption.Gui/Program.cs` starts the Avalonia app and main window.

## Maintainer notes

- Most product behavior lives in `dew_encryption/core.py`; keep CLI behavior backward compatible because GUI, installer, and shell integrations call it.
- The Python Tkinter GUI is the mature GUI; the C# Avalonia project is a native shell target that currently delegates to the CLI.
- Shell integrations are thin launchers; changes to CLI subcommand names must be mirrored in Windows registry scripts, Inno Setup, Linux Nautilus scripts, README, and CI smoke tests.
