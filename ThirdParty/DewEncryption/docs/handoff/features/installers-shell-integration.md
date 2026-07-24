# Feature Handoff: Installers and Shell Integration

## Purpose

Installer and shell integration files expose the CLI and GUI through Windows Explorer, Linux desktop launchers, Nautilus scripts, and optional elevated scheduled-task setup.

## Windows components

- `installer/install.ps1`: automated dependency/repo/package/context-menu setup.
- `installer/install-context-menu.ps1`: HKCU Explorer menu registration.
- `installer/uninstall-context-menu.ps1`: removes Explorer actions.
- `installer/create-elevated-tasks.ps1` and `remove-elevated-tasks.ps1`: explicit admin-consented scheduled task setup/removal.
- `installer/DewEncryption.iss`: Inno Setup script for full installer.
- `scripts/build-windows-installer.ps1`: build helper.

## Linux components

- `linux/install.sh`: user install, desktop file, icon, Nautilus scripts.
- `linux/uninstall.sh`: user uninstall.
- `linux/dew-encryption.desktop`: desktop launcher.
- `linux/nautilus-scripts/*`: right-click script actions.

## Change checklist

- Any CLI subcommand rename must be propagated to all shell scripts and installer command strings.
- Explorer registrations are HKCU and non-admin by design.
- Elevated tasks should remain explicit and UAC-consented; do not silently bypass UAC.
