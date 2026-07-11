# Code Logic Handoff: Installer and Integration Flow

## Windows context menu flow

1. `install-context-menu.ps1` computes project root and Python executable.
2. It builds command strings for files, folders, folder backgrounds, VeraCrypt actions, history manager, watcher, and elevated task setup/removal.
3. It creates HKCU registry keys and command subkeys.
4. It assigns action-specific icons and multi-select behavior for container actions.

## Elevated task flow

- Launcher scripts start PowerShell with `-Verb RunAs` so Windows shows a normal UAC prompt.
- Task scripts create/remove clearly named `DewEncryption.*.Elevated` tasks.

## Linux install flow

1. Validate `python3`, `git`, and `7z`; warn if VeraCrypt is missing.
2. Install the Python package for the current user.
3. Copy desktop launcher, icon, and Nautilus scripts.
4. Update desktop database/icon cache when tools are available.
