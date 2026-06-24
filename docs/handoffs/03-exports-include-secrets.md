# Handoff: Config exports include secrets

| | |
|---|---|
| **Status** | Not started |
| **Source** | Local path: `Services/ConfigBackupService.cs` (+ `Pages/ConfigBackupModule.xaml(.cs)`) |
| **License** | In-repo WinForge code — same license as the WinForge project (no third-party code introduced) |
| **Proposed module** | Extends existing **Config & Backup · 設定與備份** · same left-nav group · Tag `module.configbackup` |
| **Effort** | M — new crypto helper + a few UI controls + export/import plumbing, no new external binary |

## What the user asked for
Extend the Config & Backup export so it can OPTIONALLY include secrets — saved SSH profiles/keys, stored credentials, AI Agent API keys/env vars, package bundles, etc. Add a clearly-labelled "Include secrets" toggle, encrypt the secrets portion with a user-supplied password (AES), decrypt on import, and make the danger explicit in the UI.

## Recommended approach
**Native C# clone (extend the existing module).** This is purely WinForge-internal data, so the global strategy's "native C# first" rule applies cleanly — no tool to wrap. Today `ExportBundle` only writes `settings.json` + `manifest.json` + `checksums.txt`, and `ExportRegistry` already dumps `HKCU\Environment`. The realistic v1 scope: collect known secret sources into one `secrets.json`, encrypt it with AES-GCM under a password-derived key (PBKDF2/Rfc2898), and add it to the same .zip as `secrets.enc` only when the toggle is on. Import detects `secrets.enc`, prompts for the password, decrypts, and re-applies. The default export stays secret-free, so existing bundles are unaffected.

Concrete secret sources that exist today:
- **AI Agent API keys** — `AiAgentService` stores them as User-scope env vars (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, …); enumerate via the agent list's `EnvKey` + `GetApiKey`.
- **settings.json values** — may contain tokens; treat the whole file as sensitive when secrets are included.
- **User environment variables** — already captured in `ExportRegistry` (`HKCU\Environment`); fold into the encrypted blob when the toggle is on instead of plaintext .reg.
- **SSH profiles/keys** — no dedicated WinForge store yet; v1 can optionally bundle `%USERPROFILE%\.ssh` (config, known_hosts, id_* keys) via `FileDialogs`-confirmed opt-in.

## Features to implement (v1 → later)
- v1: "Include secrets (encrypted)" `ToggleSwitch` + password `PasswordBox` (with confirm) + a red warning `InfoBar`; AES-GCM encrypt `secrets.json` → `secrets.enc`; gather API-key env vars, settings.json, `.ssh` folder; decrypt-on-import with password prompt; wrong-password = clear bilingual error.
- later: per-category checkboxes (pick which secret types), winget/scoop package bundle capture, optional DPAPI (machine-bound) mode, redaction preview before export, audit note of what was included.

## Integration plan (WinForge specifics)
- New files: `Services/SecretsCrypto.cs` (AES-GCM + Rfc2898DeriveBytes, salt+nonce prepended to ciphertext). Reuse `System.Security.Cryptography` (already imported in `ConfigBackupService.cs`).
- Edit `ConfigBackupService.cs`: overload `ExportBundle(zipPath, bool includeSecrets, string? password)` and `ImportBundle(zipPath, string? password)`; add a `GatherSecrets()` helper.
- Edit `Pages/ConfigBackupModule.xaml(.cs)`: add toggle/password/warning to the "Portable settings bundle" card; route `ExportBundle_Click` / `ImportBundle_Click` through them. Use `FileDialogs.SaveFileAsync`/`OpenFileAsync` (NEVER WinRT pickers — already correct here).
- Nav wiring: none — reuses existing `module.configbackup` (MainWindow.xaml item, ModuleRegistry entry, MapType + NavView_SelectionChanged already present). Optionally add keywords (`secrets ssh api key encrypt 加密 密鑰`) to the ModuleRegistry entry.
- Engine/install: winget id `n/a` (no external binary; .NET crypto only).
- Key APIs/CLIs: `AesGcm`, `Rfc2898DeriveBytes`, `RandomNumberGenerator`; `AiAgentService` agent list (`EnvKey`/`GetApiKey`); `ZipFile`; existing `SettingsStore`.

## Dependencies & risks
- Password loss = unrecoverable secrets (by design) — state this in the UI.
- Never write secrets to disk in plaintext, including temp staging; encrypt before zipping and scrub temp.
- A bundle with secrets is dangerous if shared — explicit red warning + distinct filename suffix (e.g. `-with-secrets`).
- Importing secrets writes User env vars / `.ssh` files — confirm before overwriting existing keys.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; default export remains secret-free; toggle+password produces an encrypted `secrets.enc` inside the .zip; import with correct password restores secrets and with a wrong password fails gracefully; all new strings bilingual (English + 粵語); no WinRT pickers.
