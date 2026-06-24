# Handoff: SSH Toolset (terminal, profiles, passwordless deploy)

| | |
|---|---|
| **Status** | Not started |
| **Source** | CLI: `ssh` / `ssh-keygen` (OpenSSH, ships with Win11) + library: SSH.NET (Renci.SshNet); ConPTY (`CreatePseudoConsole`) for terminal hosting |
| **License** | OpenSSH = BSD-style (permissive); SSH.NET = MIT. Both freely bundlable. |
| **Proposed module** | "SSH" (粵語: SSH 工具) · Dev/Network group · Tag `module.ssh` |
| **Effort** | L — terminal rendering (ConPTY/ShellStream) + encrypted profile store + SFTP browser are each non-trivial; passwordless deploy is the riskiest UX. |

## What the user asked for
A full SSH module: saved connection profiles (host/port/user/auth) with credentials encrypted at rest (DPAPI), an in-app terminal, key management (generate + list `~/.ssh`), one-click passwordless deploy (generate keypair if needed, then push the public key to the remote `authorized_keys`), and SFTP browsing. Secrets must be exportable (see handoff 03).

## Recommended approach
**Hybrid, leaning native.** Use **SSH.NET** (MIT, pure managed) as the core engine — it gives `SshClient` (exec + `ShellStream`), `SftpClient`, and key handling in process, so no external binary is strictly required for connect/SFTP/deploy. Implement the **interactive terminal** two ways: v1 renders an `SSH.NET` `ShellStream` into a custom terminal control (TextBlock/RichEditBox + ANSI parser), which is simpler and self-contained; defer true **ConPTY hosting of `ssh.exe`** (`CreatePseudoConsole` + reading the pty) to a later pass for full PTY fidelity (resize, full escape sequences, `top`/`vim`). Key generation uses SSH.NET (`SshKeyGenerator`-style) or shells out to bundled `ssh-keygen.exe` via `ShellRunner.Capture`. This satisfies the global strategy: SSH itself is reimplementable in managed C#, so we clone rather than redirect.

## Features to implement (v1 → later)
- v1: Profile list (CRUD: name, host, port, user, auth = password or private-key path); DPAPI-encrypted secret store; connect → interactive shell tab via `ShellStream` with basic ANSI color/cursor handling; key management (generate ed25519/RSA, list/read `%USERPROFILE%\.ssh\*.pub`); one-click passwordless deploy (ensure keypair → open password session → append pubkey to remote `~/.ssh/authorized_keys`, `chmod 700 ~/.ssh && chmod 600 authorized_keys`); SFTP browser (dual-pane list, upload/download, mkdir/delete).
- later: True ConPTY-hosted `ssh.exe` terminal with resize + full escape support; multiple concurrent session tabs; jump-host/`ProxyJump`; known_hosts management + host-key TOFU prompt; port forwarding (`-L`/`-R`); SCP fallback; tmux-friendly keepalive.

## Integration plan (WinForge specifics)
- New files: `Services/SshService.cs` (SSH.NET connect/exec/ShellStream, SFTP, key gen, deploy), `Services/SshProfileStore.cs` (JSON profiles + DPAPI `ProtectedData` for secrets — mirror existing Vault/DPAPI usage in `Catalog/VaultTweaks.cs`), `Controls/TerminalView.xaml(.cs)` (ANSI render + input), `Pages/SshModule.xaml(.cs)` (profiles | terminal | keys | SFTP tabs).
- Nav wiring: add `NavigationViewItem Tag="module.ssh"` in `MainWindow.xaml` (Dev/Network group); add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.ssh`).
- Engine/install: winget id **n/a** — OpenSSH client ships with Win11. If absent on older builds, surface an `EngineBars.AutoInstallButton` that enables the "OpenSSH.Client" optional feature (or note it); SSH.NET is a NuGet ref so no install bar needed.
- Key APIs/CLIs: SSH.NET `SshClient`, `ShellStream`, `SftpClient`, `PrivateKeyFile`, `KeyboardInteractiveAuthenticationMethod`; `System.Security.Cryptography.ProtectedData` (DPAPI, `CurrentUser` scope); `ssh-keygen.exe` via `ShellRunner.Capture`; use `Services/FileDialogs.cs` for picking key files / SFTP local targets (never WinRT pickers).

## Dependencies & risks
- **Secrets at rest**: DPAPI `CurrentUser` ties blob to the user account — fine, but export (handoff 03) must round-trip the encrypted form or re-encrypt under the export password; coordinate the secret schema with handoff 03.
- **Host-key verification**: SSH.NET does not prompt by default — implement a TOFU known-hosts check or connections are MITM-exposed.
- **Terminal fidelity**: a `ShellStream` + hand-rolled ANSI parser will not perfectly render full-screen TUIs; set v1 expectations (line-oriented shells) and flag ConPTY for later.
- **NuGet footprint / trimming**: ensure SSH.NET works under the app's publish settings (AOT/trim) on x64.
- **Elevation**: SSH client ops are per-user; do not require admin.

## Acceptance criteria
- Builds clean (Debug + Release x64); "SSH" module appears in nav and master search; create a profile, connect, run a command in the in-app terminal; generate a key and list `.ssh`; passwordless deploy succeeds then login needs no password; SFTP upload/download works; all secrets stored DPAPI-encrypted; every user-facing string has English + Cantonese; FileDialogs used throughout (no WinRT pickers).
