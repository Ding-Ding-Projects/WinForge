# Handoff: RustDesk (Remote Desktop)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/rustdesk/rustdesk · Rust core + Flutter UI · CLI: `rustdesk` |
| **License** | AGPL-3.0 (open source). WinForge wrapper code is original C# launching the unmodified binary, so no license conflict (no static linking/derivation). |
| **Proposed module** | RustDesk · Apps / Remote group · Tag `module.rustdesk` |
| **Effort** | M — no engine to write; the work is a launcher/config front-end plus parsing/writing RustDesk's config files and driving its CLI. |

## What the user asked for
A WinForge module to install, launch, and manage RustDesk: show this machine's ID/permanent-password, quick-connect to a remote peer by ID, and configure the relay / self-hosted server (ID server, relay server, public key) — a manager/launcher front-end, not a reimplementation.

## Recommended approach
**CLI/binary wrap (launcher + config front-end).** RustDesk is a large Rust core (libs for screen capture, codec, hole-punching) with a Flutter UI; per global strategy step 2 a native C# clone of the remote-desktop engine is infeasible and pointless. Instead install `RustDesk.RustDesk` via winget and build a rich WinUI surface that (a) reads/writes RustDesk's config, (b) shows local ID/password, and (c) shells out to the `rustdesk.exe` CLI for connect/launch. No external redirects beyond launching RustDesk itself.

Realistic v1: detect/install RustDesk, show this PC's ID, set a permanent password, point it at a self-hosted server, and quick-connect to a peer ID.

## Features to implement (v1 → later)
- v1: Detect install (winget); show local **ID** and set **permanent password**; **Quick connect** field (peer ID, optional "view only") that runs `rustdesk.exe --connect <id>`; **Server settings** form (ID server, relay server, API server, public key) written to config; **Launch RustDesk** button; status InfoBar (running / not installed).
- later: saved peers / address book list with one-click connect; generate/rotate password; service install/start toggle (`--install-service`); read connection log; import server config from a `.txt`/QR string; unattended-access toggle.

## Integration plan (WinForge specifics)
- New files: `Services/RustDeskService.cs` (locate `rustdesk.exe` under `%ProgramFiles%\RustDesk`; read local ID via `ShellRunner.Capture("rustdesk.exe","--get-id")`; set password `--password <pw>`; connect `--connect <id>`; read/write `%AppData%\RustDesk\config\RustDesk2.toml` and `RustDesk.toml` for server/relay/key; IsInstalled/IsRunning helpers). `Pages/RustDeskModule.xaml(.cs)` (ID + password card, quick-connect bar, server-settings form, launch/status footer). Optional `Catalog/RustDeskOperations.cs` only if exposing service-install/start as TweakCards.
- Nav wiring: add `NavigationViewItem Tag="module.rustdesk"` in `MainWindow.xaml` under an Apps / Remote group; add a `ModuleRegistry` entry (Services/ModuleRegistry.cs) with bilingual title/keywords (RustDesk, remote desktop, 遠端桌面, 遙距桌面) for master search; wire the Tag in `MainWindow.xaml.cs` `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page rustdesk`.
- Engine/install: winget id `RustDesk.RustDesk` surfaced via `EngineBars.AutoInstallButton("RustDesk.RustDesk", en, zh, recheck, rescan)`. On load, detect install; if missing, show the install InfoBar.
- Key APIs/CLIs to call: `rustdesk.exe` CLI (`--get-id`, `--password`, `--connect <id>`, `--install-service`). Config TOML files under `%AppData%\RustDesk\config`. Use `ShellRunner.Run/Capture` for the CLI. Use FileDialogs (Services/FileDialogs.cs) for any config import — never WinRT pickers.

## Dependencies & risks
- CLI flag surface varies across RustDesk versions; confirm `--get-id` / `--connect` / `--password` against the installed build and degrade gracefully (parse TOML for ID as fallback).
- Config keys (`relay-server`, `custom-rendezvous-server`, `key`, `api-server`) live in `RustDesk2.toml`; writing while RustDesk runs may be overwritten — write while the app/service is stopped, or restart it after.
- Permanent-password set may require elevation / the RustDesk service running.
- Self-hosted server values are sensitive-ish (store in app settings, not logs); validate before writing.
- AGPL: only launch the unmodified binary; do not bundle modified RustDesk sources.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav and master search; install via AutoInstallButton works; local ID displays; permanent password can be set; server/relay/key settings persist to config; quick-connect by ID launches a session; all user-facing strings bilingual (English + 粵語); FileDialogs used for any file picking, no WinRT pickers.
