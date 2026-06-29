# Handoff 56 — Host a folder out over FTP / SFTP via Docker

| | |
|---|---|
| **Status** | Not started |
| **Builds on** | existing FTP/SFTP **client** (`Pages/FileZillaModule`, `Services/FtpService.cs`, `Services/FtpSiteStore.cs`) and `Services/DockerService.cs` |
| **Module** | extend `module.filezilla` (FTP / SFTP) with a **Server / Host** section, or a sibling `module.fileserver` |
| **Effort** | M — Docker orchestration + a small share-management UI; no protocol code to write. |

## What the user asked for

Let the user **host one of their folders out over FTP and/or SFTP** by running a server in **Docker** —
point at a local folder, pick a protocol/port and credentials, start the container, and hand out the
connection details. Today WinForge only has an FTP/SFTP **client**; this adds the **server** side.

## Reuse, don't rewrite

- **`Services/DockerService.cs`** does all container work: `PullImageAsync`, `ComposeUpAsync(ComposeProject,
  …)`, `ComposeDownAsync`, `ListContainersAsync`, `Start/StopContainerAsync`, `CreateVolumeAsync`. Drive
  the server containers through these — **never** shell out to `docker`.
- The Vaultwarden-via-Docker pattern in [55-bitwarden-vault-revamp.md](55-bitwarden-vault-revamp.md) (layer
  B) is the template: one compose project per share, distinct host port, bind-mount the chosen folder.
- The FTP/SFTP **client** module can connect to the share you just hosted, for a nice round-trip demo.

## Recommended approach

A "Hosted shares" panel that manages server containers, one row per share:

- **SFTP** → image `atmoz/sftp` (simple, popular). Bind-mount the user's folder to
  `/home/<user>/<share>`, pass `user:pass:UID:GID` (or a public key), map host port → 22.
- **FTP/FTPS** → image `delfer/alpine-ftp-server` (or `stilliard/pure-ftpd`). Bind-mount the folder, set
  `USERS`/credentials env, map control port 21 + a **passive port range** (must publish the PASV range and
  set `ADDRESS`/`MIN/MAX_PORT` so passive mode works through the port mapping — this is the classic FTP
  gotcha; document it and pick a small range like 21000–21010).
- Each share = its own compose project name + **auto-picked free host port(s)** so **many shares run at
  once** without collision (same multi-instance rule as handoff 55).
- Per row: Start / Stop / Remove, status/health, and a **copy connection string**
  (`sftp://user@host:port/…` or `ftp://…`) + the local LAN IP for reaching it from another machine.
- Persist share definitions (folder path, protocol, port, username — **not** the password in plaintext;
  use the DPAPI store per [CLAUDE.md](../../CLAUDE.md)).

## Integration plan (WinForge specifics)

- **Files:** `Services/FileServerService.cs` (Docker lifecycle for SFTP/FTP shares on top of
  `DockerService`); a `HostedShare` model (path/protocol/port/user, no plaintext secret); UI either as a new
  pivot/section inside `Pages/FileZillaModule` or a new `Pages/FileServerModule.xaml(.cs)` (then do the
  standard "touch 4 places" registration from CLAUDE.md).
- **Folder pick:** `Services/FileDialogs.cs` `OpenFolderAsync` only (never WinRT pickers).
- **Engine:** reuse the Docker module's engine detection/install; if Docker is down, show a banner — never
  hang.

## Security & correctness

- Secrets (FTP/SFTP passwords, private keys) via the DPAPI store — never plaintext settings or logs.
- Bind-mounting a folder grants the container read/write to it — show the exact host path and warn before
  exposing sensitive directories; default to a single explicitly chosen folder, not a drive root.
- Warn that FTP (plain) is unencrypted; prefer SFTP or FTPS; only bind to LAN unless the user opts in.
- Firewall: opening a listening port may require a Windows Firewall rule — surface this, don't fail silently.
- FTP passive range must be published and advertised correctly or transfers hang — verify a real
  upload/download round-trip.

## Error handling (apply [54](54-rich-text-toolbar-rollout.md) §4b)

Every UI handler guarded; every `ShowAsync` wrapped; **fail open** (a Docker/port error shows a banner,
never crashes/exits/hangs); pulls and compose-up run off the UI thread with `CancellationToken` and a
visible log; one failing share never affects the others.

## Acceptance criteria

- [ ] Can host a chosen local folder over **SFTP** and over **FTP/FTPS** via Docker; start/stop/remove.
- [ ] **Multiple** shares run simultaneously (distinct auto-picked ports), each isolated.
- [ ] Copyable connection string + LAN address; FTP passive transfers actually work end-to-end.
- [ ] Passwords/keys via DPAPI only — never plaintext/logs; clear host-path + plain-FTP warnings shown.
- [ ] Robust per §4b; dead Docker engine shows a banner, never hangs.
- [ ] Bilingual strings (English + 粵語); FileDialogs only; `dotnet build … -p:Platform=x64` → 0 errors.

---

*Created session 56. Related: [55-bitwarden-vault-revamp.md](55-bitwarden-vault-revamp.md) (Docker-hosting
pattern), Docker module / `Services/DockerService.cs`, FTP/SFTP client (`Pages/FileZillaModule`).*
