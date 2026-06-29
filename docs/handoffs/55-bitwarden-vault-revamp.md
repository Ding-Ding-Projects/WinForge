# Handoff 55 — Bitwarden Vault revamp + self-hosted (Vaultwarden) via Docker, unlimited instances with tabs

| | |
|---|---|
| **Status** | Not started |
| **Builds on** | [41-bitwarden.md](41-bitwarden.md) (original vault), existing `Pages/BitwardenModule.xaml(.cs)` + `Services/BitwardenService.cs` (native HTTP client, already self-host aware) |
| **Module** | `module.bitwarden` · Security & Privacy group |
| **Effort** | L — UI revamp + Docker orchestration for a self-hosted server + a multi-instance tabbed shell with per-instance isolation. |

## What the user asked for

1. **Revamp** the Bitwarden Vault UI.
2. **Allow a self-hosted instance via Docker** — let the user spin up their own server (Vaultwarden, the
   lightweight Bitwarden-compatible server) as a Docker container from inside WinForge, then connect to it.
3. **Host unlimited instances with tab management** — run/connect to **any number** of vaults/servers at
   once, each in its **own tab**, with **independent state** (one tab's unlock/lock/server/selection must
   never affect another). Same per-instance-isolation principle as the rich-text-toolbar work
   ([54](54-rich-text-toolbar-rollout.md) §3): no shared/`static` session or selection state.

## What already exists (reuse, don't rewrite)

- **`Services/BitwardenService.cs`** is a **native HTTP** vault client (not just a `bw` CLI wrap): it does
  prelogin, protected-key handling, and already branches for **self-hosted / Vaultwarden** base URLs
  (`{base}/api`, `{base}/identity`). It exposes `StatusInfo(Status, ServerUrl, UserEmail, LastSync)`,
  login/unlock, list/search, copy, TOTP, generate, sync. **The crypto and protocol are done** — the revamp
  is UI + multi-instance + Docker, not re-implementing the vault.
- **`Services/DockerService.cs`** already has everything needed to host a server:
  `PullImageAsync`, `ComposeUpAsync(ComposeProject, …)`, `ComposeDownAsync`, `ListContainersAsync`,
  `Start/StopContainerAsync`, `CreateVolumeAsync`, `CreateNetworkAsync`. Drive Vaultwarden through these —
  **do not** shell out to `docker` directly.
- **`Pages/GitHubModule.xaml(.cs)`** is the in-repo **`TabView`** reference — copy its tab add/close/select
  wiring for the multi-instance shell.
- The "touch 4 places to add/extend a module" rule and bilingual-string rule are in
  [CLAUDE.md](../../CLAUDE.md).

## Recommended architecture

Split the module into **two cooperating layers** so a connection is independent of a server:

### A. Connections (vault sessions) — the tabs
- A `TabView` where **each tab is one `BitwardenVaultSession`** (its own `BitwardenService` instance + its
  own UI state: status, search text, selected item, unlock key-in-memory). **Unlimited** tabs.
- Per-instance state lives in the session object's **instance fields** — never `static`. Locking tab A
  must not lock tab B even if they point at the same server.
- "New connection" flow: pick/enter a **server base URL** (Bitwarden cloud, an EU host, or a self-hosted
  URL — including one you just started in layer B), email + master password, optional 2FA → opens a tab.
- Persist the **list of connections** (server URL + email + display name) so tabs can be restored; **never**
  persist master passwords or session/unlock keys (secrets stay in memory only — see Security below).
- Clipboard hygiene: auto-clear copied secrets after a timeout (carry over from handoff 41).

### B. Self-hosted servers (Vaultwarden) — the Docker side
- A "Servers" pane/tab that manages **Vaultwarden containers** via `DockerService`:
  - **Create instance:** pull `vaultwarden/server:latest`, then `ComposeUpAsync` a project with a named
    **volume** for `/data`, a chosen **host port**, and env (`DOMAIN`, `ADMIN_TOKEN`,
    `SIGNUPS_ALLOWED`, optional `WEBSOCKET_ENABLED`). Each instance = its own compose project name +
    volume + port, so you can run **many at once** without collision (auto-pick a free port).
  - **List / Start / Stop / Remove** instances (map to container/compose lifecycle calls). Show health and
    the local URL (`http://localhost:<port>`).
  - **One click → connect:** after an instance is up, offer "Open a connection to this server" which seeds
    a layer-A tab with that base URL.
- Requires Docker Desktop / engine reachable — reuse the Docker module's engine-detection / install
  guidance; show a clear banner if the engine is down (don't hang).

## Integration plan (WinForge specifics)

- **Files:** revamp `Pages/BitwardenModule.xaml(.cs)` into the tabbed shell; add
  `Services/BitwardenInstanceService.cs` (Vaultwarden Docker lifecycle on top of `DockerService`) and a
  `BitwardenVaultSession` view-model holding one `BitwardenService` + its UI state; optionally
  `Models/BitwardenConnection.cs` (persisted connection metadata, **no secrets**).
- **Reuse**, don't duplicate: all vault calls go through the existing `BitwardenService`; all container
  calls through `DockerService`.
- **Nav/registry:** module already registered (`module.bitwarden`); just keep the deep-link `--page
  bitwarden` working after the revamp.
- **Pickers:** `Services/FileDialogs.cs` only (never WinRT) for any import/export/attachment paths.

## Security (carry these — they are invariants)

- Master passwords, session keys, and unlock/protected keys: **in memory only**, never written to disk,
  settings, or logs; cleared on lock/tab-close/exit. Per-tab keys are isolated.
- `ADMIN_TOKEN` for Vaultwarden is a secret — if stored, use the DPAPI-backed store (per CLAUDE.md), never
  plaintext settings; prefer generating a strong one and letting the user copy it.
- Self-hosted over `http://localhost` is fine; warn before connecting to a **remote** self-hosted URL over
  plain HTTP.
- Default Vaultwarden `SIGNUPS_ALLOWED` thoughtfully (e.g. allow on first run so the user can create their
  account, then surface a toggle to disable it).

## Error handling & robustness (apply [54](54-rich-text-toolbar-rollout.md) §4b)

- Every UI callback wrapped (a throwing sync or `async void` handler crashes the app); every
  `ContentDialog.ShowAsync` through a safe wrapper; **fail open** — a Docker/network error must surface a
  banner, never crash, exit, or freeze the module.
- All Docker pulls / compose-up and all vault HTTP calls run off the UI thread with `CancellationToken`
  support and a visible progress/log area; closing a tab cancels its in-flight work.
- One failing instance/tab must not take down the others or the app.

## Acceptance criteria

- [ ] Revamped tabbed Bitwarden module; **unlimited** connection tabs, each with fully independent state
      (lock/unlock/search/selection isolated; no `static` session state).
- [ ] Can create, start, stop and remove **multiple** self-hosted **Vaultwarden** instances via
      `DockerService` (distinct ports + volumes), and connect to them in one click.
- [ ] Connects to Bitwarden cloud **and** self-hosted/Vaultwarden URLs through the existing
      `BitwardenService`.
- [ ] No secret (master password, session/unlock key, `ADMIN_TOKEN`) ever hits disk or logs; clipboard
      auto-clears; tab state isolated.
- [ ] Robust per §4b: every handler guarded, `ShowAsync` wrapped, fails open, long work off the UI thread;
      a dead Docker engine shows a banner, never hangs.
- [ ] All user-facing strings bilingual (English + 粵語); FileDialogs only.
- [ ] `dotnet build WinForge.sln -c Debug -p:Platform=x64` → 0 errors.

---

*Created session 55. Prior: [41-bitwarden.md](41-bitwarden.md) (vault), [54-rich-text-toolbar-rollout.md](54-rich-text-toolbar-rollout.md)
(per-instance isolation + error-handling rules), Docker module / `Services/DockerService.cs`.*
