# Handoff: Bitwarden Vault (bw CLI front-end)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/bitwarden · CLI: `bw` (winget `Bitwarden.CLI`) |
| **License** | Bitwarden clients are GPL-3.0 (CLI/desktop); SDK components AGPL/Bitwarden License. We only *invoke* the published `bw` binary, no source vendored. |
| **Proposed module** | Bitwarden Vault · Security & Privacy group · Tag `module.bitwarden` |
| **Effort** | M — no native crypto to write; effort is in CLI orchestration, secure session handling, and a clean vault UI. |

## What the user asked for
A native WinUI vault front-end over the Bitwarden CLI: log in / unlock (session key), list and search items, view/copy username/password/TOTP, create/edit items, generate passwords, and sync. Install `bw` via winget; optionally the desktop app. Ties into secrets export (handoff 03).

## Recommended approach
**CLI wrap (Hybrid front-end).** Per global strategy rule 2, Bitwarden's vault crypto, sync protocol and account model are large and security-critical — reimplementing them natively is out of scope and irresponsible. We wrap the official `bw` CLI and build a rich WinUI vault UI around it. Everything stays inside WinForge; no external redirects beyond optionally launching the Bitwarden desktop app.

**v1 scope:** unlock an already-configured account, browse/search/copy items, view TOTP, generate passwords, sync, and add/edit logins. Account creation, attachments, org/collection management and full item-type editing are later.

## Features to implement (v1 → later)
- v1: Status banner (`bw status` -> unauthenticated / locked / unlocked). Login with email+master password (and 2FA prompt). Unlock -> capture `BW_SESSION` key in memory. List + live search (`bw list items --search`). Item detail: copy username/password (auto-clear clipboard after ~20s), reveal TOTP via `bw get totp <id>` with countdown. Password generator (`bw generate` with length/symbols/numbers toggles). Manual `bw sync`. Add/edit a login item.
- later: Folders/collections filter, secure notes/cards/identities, attachments, send, org vaults, biometric/auto-unlock, push generated/copied creds into the secrets-export module (handoff 03).

## Integration plan (WinForge specifics)
- New files: `Services/BitwardenService.cs` (wraps `bw` via `ShellRunner.Capture`, parses `--response`/JSON, holds the session key for the process lifetime only), `Pages/BitwardenModule.xaml(.cs)` (status bar, search box, item list, detail pane, generator flyout), `Catalog/BitwardenOperations.cs` (optional: sync / lock / logout as TweakCard actions).
- Nav wiring: add `NavigationViewItem Tag="module.bitwarden"` in MainWindow.xaml under Security & Privacy; add a `ModuleRegistry` entry (EN "Bitwarden Vault" / ZH "Bitwarden 密碼庫") for master search; wire `module.bitwarden` in MainWindow.xaml.cs `MapType` + `NavView_SelectionChanged`; add to `ApplyStartPage` for `--page bitwarden`.
- Engine/install: winget id `Bitwarden.CLI` via `EngineBars.AutoInstallButton("Bitwarden.CLI", "Install Bitwarden CLI", "安裝 Bitwarden CLI", recheck, rescan)`. Optionally offer `Bitwarden.Bitwarden` (desktop) as a secondary AutoInstallButton.
- Key APIs/CLIs to call: `bw status`, `bw login`/`bw unlock --raw` (returns session key — pass as `--session <key>` or `BW_SESSION` env on every subsequent call; never log it), `bw list items [--search]`, `bw get item|password|username|totp <id>`, `bw generate`, `bw create item <base64-json>`, `bw edit item <id> <base64-json>`, `bw sync`, `bw lock`, `bw logout`. Prefer JSON output and parse with `System.Text.Json`.

## Dependencies & risks
- Session key is a secret: keep only in memory, never persist to disk/logs/settings; clear on lock/exit. Pass via stdin/env, not a visible arg where avoidable.
- Master password / 2FA prompts: `bw` may go interactive — drive via env (`BW_PASSWORD`) / `--passwordenv` and `--method`/`--code` flags rather than a TTY.
- Clipboard hygiene: auto-clear copied secrets after a timeout.
- Self-hosted servers need `bw config server <url>` before login — expose a settings field.
- Version drift in `bw` output schema; gate features on `bw --version`.
- FileDialogs (Win32 COM) for any import/export file paths — never WinRT pickers.

## Acceptance criteria
- Builds clean (Debug + Release x64); "Bitwarden Vault" appears in nav and master search; unlock -> list -> copy password / view TOTP / generate / sync all work; secrets never written to disk or logs and clipboard auto-clears; every user-facing string is bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
