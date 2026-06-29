# Handoff 58 — Fully custom Minecraft launcher with Microsoft auth

| | |
|---|---|
| **Status** | Not started |
| **Type** | New module |
| **Module** | new `module.minecraftlauncher` · Games group |
| **Effort** | L–XL — full MSA→Minecraft auth chain, version/asset/library downloader, and a launch-command builder. |
| **Sits beside** | existing Minecraft modules: `module.minecraftserver`, `module.minecraftworldtools`, `module.amulet`, `module.viaproxy` |

## What the user asked for

A **fully custom** Minecraft launcher (our own UI + logic, not a wrapper around the official launcher) that
signs the user in with **Microsoft authentication**, downloads the game, and launches it — supporting
**multiple instances/profiles**.

## Recommended approach — pure managed C#

A launcher's job *is* to download manifests/assets and start `java`, so doing that is in-scope (this is not
"shelling out to the program we reimplement" — there is no Minecraft binary to reimplement; we orchestrate
the official Mojang files + a JVM). Everything else stays managed C#: HTTP + `System.Text.Json`.

### 1. Microsoft auth chain (the core)
Standard MSA → Minecraft flow, all HTTP/JSON:
1. **Microsoft OAuth2** → access token. Reuse the in-app WebView2 login (`Services/WebLoginService.cs` /
   `Controls/LoginDialog.xaml`) for the **auth-code** flow, or implement **device-code** flow (show a code +
   `https://microsoft.com/link`). Patterns already exist in `Services/MailOAuthService.cs`.
2. **Xbox Live** — `POST user.auth.xboxlive.com/user/authenticate` → XBL token.
3. **XSTS** — `POST xsts.auth.xboxlive.com/xsts/authorize` → XSTS token + `uhs` (userhash). Handle the
   common error codes (2148916233 = no Xbox account, 2148916238 = child account).
4. **Minecraft** — `POST api.minecraftservices.com/authentication/login_with_xbox` → Minecraft bearer token.
5. **Entitlements + profile** — `GET .../entitlements/mcstore` and `.../minecraft/profile` → confirm
   ownership, get the **UUID + username + skin**. (No copy: if unowned, say so.)
- **Azure app registration caveat (call this out loud):** Minecraft auth only works for an Azure AD
  application that has been **approved by Mojang for Minecraft sign-in**, with redirect URI and the
  `XboxLive.signin offline_access` scopes. The implementer must register/obtain a client ID; document this
  as a prerequisite — it cannot be skipped, and the public `00000000402b5328` (official launcher) id is not
  for third-party use.
- **Tokens:** store the MSA refresh token via the **DPAPI** store (per [CLAUDE.md](../../CLAUDE.md)); keep
  Minecraft/XSTS access tokens in memory; auto-refresh; never log any token.

### 2. Game install (download pipeline)
- **Version manifest:** `GET launchermeta.mojang.com/mc/game/version_manifest_v2.json` → list releases /
  snapshots; per-version JSON gives libraries, asset index, the client jar, and the Java major version.
- Download with hash (SHA1) verification + parallelism + resume into the standard `.minecraft` layout
  (`versions/`, `libraries/`, `assets/objects/<2hex>/<hash>`). Extract **natives** for the OS.
- **JVM:** download a matching JRE (Adoptium/Temurin) per the version's `javaVersion`, or let the user point
  at an installed one. Don't assume a system Java.

### 3. Launch-command builder
- Build the classpath (libraries honoring `rules`/OS filters + client jar), JVM args (natives path, memory
  `-Xmx`, log config), and game args (substitute `${auth_player_name}`, `${auth_uuid}`,
  `${auth_access_token}`, `${version_name}`, `${game_directory}`, `${assets_root}`, etc.).
- Launch `java` via `Process` (this is the module's function). **Apply [54](54-rich-text-toolbar-rollout.md)
  §4b**: for a GUI game launch, do **not** pipe stdio in a way that hangs it — stream the log only if it
  helps, off the UI thread (see the Amulet frozen-exe lesson in handoff 58's sibling fix — redirected stdio
  on a windowed child can look like "won't launch").

### 4. Instances / profiles (multi-instance, isolated)
- Multiple named **instances**, each with its own version, JVM, memory, game directory, and (optionally)
  account — fully independent state (same per-instance isolation rule as handoffs 54/55; no `static`
  current-profile state). Optionally a `TabView` (reference `Pages/GitHubModule.xaml`) or a profile list.

## Integration plan (WinForge specifics)

- **Files:** `Services/MinecraftAuthService.cs` (MSA→Minecraft chain), `Services/MinecraftLauncherService.cs`
  (manifest/download/launch), models for version JSON + profile; `Pages/MinecraftLauncherModule.xaml(.cs)`.
  Then the standard "touch 4 places" registration ([CLAUDE.md](../../CLAUDE.md)): `ModuleRegistry`,
  `MapType`, `ApplyStartPage` (`--page minecraftlauncher`), `MainWindow.xaml` nav item (Games group).
- **Reuse:** WebView2 login + DPAPI secret store + `FileDialogs` (folder/JRE pick, never WinRT).
- **Licensing:** we ship no Mojang assets — everything is downloaded from official endpoints at runtime;
  note the Minecraft EULA in the module.

## Acceptance criteria

- [ ] Microsoft sign-in completes the full XBL→XSTS→Minecraft chain; ownership/profile detected; child/no-
      Xbox errors handled with clear bilingual messages.
- [ ] Refresh token stored via DPAPI only; no token ever logged; auto-refresh works.
- [ ] Can download a chosen version (release + snapshot) with hash verification and a matching JRE, and
      **launch the game** successfully.
- [ ] **Multiple** instances/profiles with independent settings; no shared/static state.
- [ ] Azure-app-registration prerequisite documented in-module; robust per §4b (guarded handlers, fail open,
      downloads/launch off the UI thread, never hang).
- [ ] Bilingual strings (English + 粵語); FileDialogs only; `dotnet build … -p:Platform=x64` → 0 errors.

---

*Created session 58. Reuse: `WebLoginService`/`LoginDialog` (WebView2 auth), `MailOAuthService` (OAuth
pattern), DPAPI store, `DockerService`-style multi-instance isolation. Sibling fix this session: Amulet
frozen-exe launch (don't pipe stdio of a windowed child).*
