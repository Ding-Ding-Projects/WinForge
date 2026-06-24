# Handoff: Minecraft Server Setupper (Paper/Spigot + plugins)

| | |
|---|---|
| **Status** | Not started |
| **Source** | PaperMC Fill v3 API (`https://fill.papermc.io/v3`) · Spigot BuildTools (`https://hub.spigotmc.org/jenkins/job/BuildTools/lastSuccessfulBuild/artifact/target/BuildTools.jar`) · plugin git repos |
| **License** | WinForge-native code (project license). Paper = GPLv3/MIT (binary download only, no redistribution). Spigot = built locally by user via BuildTools (no jar redistributed). Plugins = each repo's own license. |
| **Proposed module** | Minecraft Server · *Developer / Tools* group · Tag `module.minecraftserver` |
| **Effort** | L — multiple subsystems (HTTP download, BuildTools build, long-lived console process with stdin, plugin source builds), but most process/JDK plumbing already exists in `MinecraftService`. |

## What the user asked for
A module to stand up a Minecraft server: pick **Paper** (download a build via the PaperMC API) or **Spigot** (run `BuildTools.jar` to compile from source), choose a version, accept the EULA, configure `server.properties` + memory + start scripts, and start/stop the server with a live console. Plus a **plugin catalog** that can **build plugins from git source** (clone + Maven/Gradle) and drop the jar into `plugins/`.

## Recommended approach
**Hybrid (native C# orchestration + wrapped Java runtime).** The server *is* a Java jar, so it cannot be reimplemented in C#; per the global strategy we wrap the Java binary and build a rich WinUI front-end. Everything else is native C#: HTTP calls to the PaperMC API, writing config files, generating start scripts, running BuildTools/Maven/Gradle, and managing the server process with a console. No external redirects. **v1 scope:** Paper download + EULA + server.properties editor + memory/start-script + console with command input; Spigot via BuildTools; one or two plugin builds from git. Realistic and self-contained.

## Features to implement (v1 → later)
- **v1:**
  - Server folder picker (FileDialogs) + persisted path in `SettingsStore` (`mc.server.dir`).
  - Paper: `GET /v3/projects/paper` → versions; `GET /v3/projects/paper/versions/{ver}/builds` → latest stable build; download jar from the build's `downloads.server:default.url` to `server.jar`.
  - EULA toggle (writes `eula=true`).
  - `server.properties` editor (key fields: port, MOTD, gamemode, difficulty, max-players, online-mode, level-seed) + raw text fallback.
  - Memory sliders (`-Xms`/`-Xmx`) + generate `start.bat` (Aikar's flags optional).
  - Start/Stop with **live console** + a command input box that writes to the server's **stdin** (e.g. `stop`, `say`, `op`).
  - Spigot: locate/download `BuildTools.jar`, run `java -jar BuildTools.jar --rev <ver>`, copy `spigot-*.jar` to `server.jar`.
  - Plugin builds: clone git repo, detect `pom.xml` (Maven) or `gradlew`/`build.gradle` (Gradle), build, copy produced jar into `plugins/`.
- **later:** plugin catalog presets (LuckPerms, EssentialsX, ViaVersion, WorldEdit); RCON client; auto-restart/crash-watch; backup/restore world; Modrinth/Hangar download for prebuilt plugins; Velocity/proxy support.

## Integration plan (WinForge specifics)
- **New files:** `Services/MinecraftServerService.cs` (Paper API, BuildTools, config, start scripts, process + stdin console, plugin build), `Pages/MinecraftServerModule.xaml(.cs)` (Paper/Spigot pickers, properties form, memory, console pane, plugin list). Optionally `Catalog/MinecraftPluginCatalog.cs` (list of `name + git url + build system` for the plugin presets).
- **Reuse:** `Services/MinecraftService.cs` already has `FindJava()`, `HasJava()`, `AutoInstallJdk()`, `FindMaven()`, `HasMaven()`, `BuildJar()`, and a tracked start/stop pattern with stdout/stderr streaming — lift/share these (consider promoting Java/Maven helpers to a shared static). Add Gradle (`gradlew.bat`) detection alongside Maven; add `RedirectStandardInput=true` for the console command box (the existing `Start` does not redirect stdin).
- **Nav wiring:** add `NavigationViewItem Tag="module.minecraftserver"` in `MainWindow.xaml`; add a `ModuleRegistry` entry (EN "Minecraft Server", ZH "Minecraft 伺服器") for master search; wire the tag in `MainWindow.xaml.cs` `MapType` + `NavView_SelectionChanged` (+ `ApplyStartPage` for `--page minecraftserver`).
- **Engine/install:** winget id `Microsoft.OpenJDK.21` via `EngineBars.AutoInstallButton(...)` when `MinecraftService.HasJava()` is false. For plugin builds needing Maven, surface `Apache.Maven` similarly; Gradle projects use the bundled `gradlew` wrapper (no install).
- **Key APIs/CLIs:** PaperMC Fill v3 REST (HttpClient + System.Text.Json); `java -jar BuildTools.jar --rev <ver>`; `git clone`; `mvn -DskipTests package` / `gradlew shadowJar`; server launch `java -Xms<n> -Xmx<n> -jar server.jar nogui`.

## Dependencies & risks
- Java is mandatory (modern Minecraft needs JDK 21) — gate the UI behind `HasJava()`.
- BuildTools and Maven/Gradle builds are slow (minutes) and verbose — run async, stream output, never block UI; offer cancellation (CancellationToken like `BuildJar`).
- EULA acceptance must be an explicit user action (legal); do not auto-accept silently.
- Long-lived server process: ensure clean shutdown (send `stop` to stdin first, then kill tree on timeout) and detach on app exit if desired.
- Untrusted plugin source builds run arbitrary Maven/Gradle scripts — note this; consider an opt-in confirmation.
- Bilingual: every InfoBar, button, and status string needs EN + 粵語.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in nav; can download a Paper server, accept EULA, edit `server.properties`, start it, see live console, send a `stop` command, and stop it; Spigot path builds via BuildTools; at least one plugin builds from git and lands in `plugins/`; all strings bilingual; only FileDialogs used (no WinRT pickers).
