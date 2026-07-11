# Dew Encryption Handoff Index

This handoff folder is organized so a new maintainer can scan the project by feature, button/action, user step, and implementation path.

## Repository and branch scan

- Current working branch at scan time: `work`.
- Local branches at scan time: `work`.
- Reachable history at scan time includes the merge commits and feature commits listed in [`repo-scan.md`](repo-scan.md).
- The repository contains a Python CLI/core, a Tkinter GUI, a C# Avalonia GUI shell, Windows and Linux shell integrations, installer scripts, web docs, icons, and CI/release/page workflows.

## Handoff document map

### Feature documents

- [Archive snapshots](features/archive-snapshots.md)
- [File history](features/file-history.md)
- [VeraCrypt containers](features/veracrypt-containers.md)
- [Container manager themes and hooks](features/container-manager-themes-hooks.md)
- [Portable settings](features/portable-settings.md)
- [Installers and shell integration](features/installers-shell-integration.md)
- [C# Avalonia GUI target](features/csharp-avalonia-gui.md)
- [Docs, CI, release, and pages](features/docs-ci-release.md)

### Button/action documents

- [Python GUI buttons](buttons/python-gui-buttons.md)
- [C# GUI buttons](buttons/csharp-gui-buttons.md)
- [Windows Explorer menu actions](buttons/windows-explorer-actions.md)
- [Linux Nautilus script actions](buttons/linux-nautilus-actions.md)
- [CLI commands](buttons/cli-commands.md)

### Step documents

- [End-user archive and history steps](steps/end-user-archive-history.md)
- [VeraCrypt encrypt/decrypt steps](steps/veracrypt-encrypt-decrypt.md)
- [Container profile and hook steps](steps/container-profile-hooks.md)
- [Install, build, and release steps](steps/install-build-release.md)

### Code-logic documents

- [Core snapshot and Git logic](code-logic/core-snapshot-git.md)
- [VeraCrypt and container-history logic](code-logic/veracrypt-container-history.md)
- [Settings, profiles, and hooks logic](code-logic/settings-profiles-hooks.md)
- [Python GUI event flow](code-logic/python-gui-event-flow.md)
- [C# GUI CLI bridge flow](code-logic/csharp-cli-bridge-flow.md)
- [Installer and integration logic](code-logic/installer-integration-flow.md)
