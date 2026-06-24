# Handoff: ViaProxy (Minecraft version proxy)

| | |
|---|---|
| **Status** | Not started |
| **Source** | https://github.com/codingmachineedge/ViaProxy (upstream: github.com/ViaVersion/ViaProxy, Java) |
| **License** | GPL-3.0 (open source) — bundling/redistributing the jar must respect GPL; prefer download-on-demand over committing the jar |
| **Proposed module** | ViaProxy · Gaming / Minecraft group · Tag `module.viaproxy` |
| **Effort** | M — jar-wrap with a config form; the start/stop/log plumbing already exists in `MinecraftService` and can be mirrored |

## What the user asked for
Wrap the ViaProxy Java jar inside WinForge: download/run `ViaProxy.jar` (it needs Java), expose its core config (bind port, target server, online/offline mode, auth) in a WinUI form, start/stop the proxy, and stream its logs. It should sit alongside the existing Minecraft tooling.

## Recommended approach
**CLI/binary wrap.** Per the global strategy, ViaProxy is a large Java/Netty protocol-translation codebase (ViaVersion/ViaBackwards/ViaRewind) that re-implements every Minecraft network protocol version — re-writing that in C# is far out of scope. So wrap the jar and build a rich WinUI front-end. This mirrors the existing `Services/MinecraftService.cs` exactly: locate `java.exe` (JAVA_HOME → PATH → common JDK roots), offer a winget JDK install, run the jar as a tracked process with `RedirectStandardOutput/Error`, and surface live logs + start/stop. v1 scope: download the latest `ViaProxy.jar` to a WinForge app-data folder, run it headless via CLI flags (`--no-gui` with `--bind-address`, `--target-address`, `--target-version`, `--auth-method`), a config form, a log pane, and start/stop. ViaProxy also ships a Swing GUI; do **not** launch that — WinForge drives it headlessly and provides its own UI (no external redirects).

## Features to implement (v1 → later)
- v1: Java detection + winget JDK auto-install; one-click download of latest `ViaProxy.jar` (GitHub releases API); config form (bind port, target server host:port, target MC version dropdown, online/offline mode, auth method: none/account/openauthmod); start/stop tracked process; live log pane; "connect Minecraft to localhost:<port>" hint.
- later: Microsoft account login flow (ViaProxy account manager), proxy/SOCKS upstream, legacy/classic version support toggles, saved server profiles, copy-to-clipboard of the local address, persisting last config via `SettingsStore`.

## Integration plan (WinForge specifics)
- New files: `Services/ViaProxyService.cs` (clone the structure of `Services/MinecraftService.cs`: `FindJava`/`HasJava`/`AutoInstallJdk`, jar download to `%LOCALAPPDATA%\WinForge\viaproxy`, `RunOptions`, `Start`/`Stop`/`IsRunning`, bilingual `TweakResult`); `Pages/ViaProxyModule.xaml(+.cs)` (config form using bound TextBox/ComboBox/ToggleSwitch + log `TextBlock`/`ScrollViewer`).
- Nav wiring: add `NavigationViewItem` Tag `module.viaproxy` in `MainWindow.xaml` (Gaming/Minecraft group); add a `ModuleRegistry` entry (`Services/ModuleRegistry.cs`) for master search; wire the Tag in `MainWindow.xaml.cs` (`MapType`, `NavView_SelectionChanged`, optionally `ApplyStartPage` for `--page module.viaproxy`).
- Engine/install: winget id `Microsoft.OpenJDK.21` via `EngineBars.AutoInstallButton` (reuse `MinecraftService.AutoInstallJdk` / `PackageService.AutoInstall`). The jar itself has no winget id — download from GitHub releases.
- Key APIs/CLIs to call: `java -jar ViaProxy.jar --no-gui --bind-address 127.0.0.1:25568 --target-address <host:port> --target-version <ver> --auth-method none` (verify exact flag names against the bundled jar's `--help`); GitHub releases endpoint for the latest asset; use `FileDialogs` (never WinRT pickers) if the user picks a custom jar/output folder.

## Dependencies & risks
- Requires a JDK (Java 17+/21) — covered by the existing winget install path.
- ViaProxy CLI flag names differ across versions; confirm against the downloaded jar's `--help` before hardcoding. Headless flags exist but evolve.
- GPL-3.0: download the jar at runtime rather than redistributing it; show the license/source link in the UI.
- Online-mode/auth requires a Microsoft account login; v1 can default to offline/none and defer account flow.
- Port conflicts (25565 default) — let the user set the bind port and report bind failures from the log.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; user can install Java, download the jar, set target server + version, start the proxy, see live logs, and stop it; Minecraft can connect to the local bind address; all user-facing strings are bilingual (English + 粵語); no WinRT pickers (FileDialogs only).
