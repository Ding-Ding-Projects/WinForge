# AGENTS.md — WinForge

Guidance for AI agents working in this repo. Keep it current.

## What this is
WinForge (a.k.a. 視窗調校) is an all-in-one, **fully bilingual (English + 繁體中文/粵語)** Windows 11 control center — ~130 in-app modules plus a large Windows-tweak catalog — with a **hyper-realistic nuclear-reactor simulator** as its flagship. **.NET `net11.0-windows10.0.26100.0`, WinUI 3**, self-contained, unpackaged.

## Build / run / drive
- **Compile check:** `dotnet build WinForge.sln -c Debug -p:Platform=x64` (0 errors; ~300 pre-existing warnings).
- **Run it:** a plain Debug build is framework-dependent and only shows a *"install .NET"* dialog here — you must **publish self-contained**:
  `dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true`
- **Easiest path — use the skill `.Codex/skills/run-winforge/`** (SKILL.md + driver.ps1): builds-if-needed, launches any page via `--page <alias>`, screenshots the window. e.g.
  `powershell -ExecutionPolicy Bypass -File .Codex/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png`
- **Reactor engine tests (headless):** `dotnet run --project tests/ReactorSim.Tests` (13/15).
- The app is NOT a Start-menu app, so desktop/computer-use screenshot tools mask it — capture via the driver (DWM bounds + `CopyFromScreen`).

## Architecture & conventions (follow these)
- **Add a module = touch 4 places:** `Pages/<X>Module.xaml(.cs)` (class `<X>Module : Page`, namespace `WinForge.Pages`) + logic in `Services/<X>Service.cs`; then register in **(1)** `Services/ModuleRegistry.cs` (Tag `module.xxx`, En, Zh, Glyph, Keywords), **(2)** `MainWindow.xaml.cs` `MapType()` (tag→type), **(3)** `MainWindow.xaml.cs` `ApplyStartPage()` (deep-link aliases for `--page`), **(4)** `MainWindow.xaml` `NavigationViewItem`.
- **Bilingual everywhere:** all user-facing strings use `Models/Core.cs` `LocalizedText(en, zh)` or `Services/Loc.cs` `Loc.I.Pick(en, zh)`. Cantonese (粵語), not Mandarin. The UI shows both languages.
- **UI:** prefer `Controls/TweakCard` (data-driven; has a `VisualBuilder` hook for generated previews). Use `Services/FileDialogs.cs` (never WinRT pickers). WebView2 is available.
- **Pure managed C#** for module functionality; do not shell out to / launch the upstream program a module reimplements. Managed NuGet wrappers are fine (note them).
- **Secrets:** DPAPI (`ProtectedData`) via the existing stores; never log secrets.

## The flagship reactor (most complex area)
- Engine: `Services/ReactorSimService.cs` (+ `ReactorRps.cs`, `ReactorScenarios.cs`, `ReactorElectrical.cs`, `ReactorReactivityMeter.cs`). UI: `Pages/ReactorModule.xaml(.cs)`, `Pages/ReactorHtmlWindow.cs` (HTML5/WebView2 rooms-as-tabs, assets in `SimAssets/reactor/`), `Pages/ReactorSettingsModule.xaml(.cs)` (real-world/external toggles).
- Services: `FuelFactoryService`, `NuclearWasteService`, `WaterTreatmentService`, `ReactorStatusApiService` (+ `Sdk/ReactorStatusClient.cs`), `ReactorSystemLinkService`, `ReactorHomeAssistantMirror`, `PersistenceService`, `AwakeService`.
- **Safety invariants — keep these:** meltdown→real-shutdown is **default OFF** (abortable); reactor boots **held in MODE 5 cold shutdown** (operator must start it up); waste writes respect a disk free-space floor + a (default 50 GB) cap; all real-world side-effects are opt-in & reversible.
- **Known gap:** at-power **reactivity calibration (P2) is unfinished** — the core is safe at rest but melts if started up. See `docs/reactor-realism-review-001.md` and `docs/handoffs/52-omega-session-handoff.md`.

## Docs
Bilingual wiki under `docs/wiki/` (Home → 13 category pages + reactor hub with 7 sub-pages + manual + test report + screenshots gallery; 124 page screenshots in `docs/wiki/images/`). Full session state & open items: **`docs/handoffs/52-omega-session-handoff.md`**.

## Gotchas
- 3 module pages crash on open (`audioeditor`, `lightswitch`, `timelens`) — throw in `MainWindow.NavigateActive` at load.
- `--page <alias>` is reliable; bare `--reactor` can land on the Dashboard if a session was restored.
- Reactor restoring a stale/melted autosave → delete `%LOCALAPPDATA%\WinForge\state` (+ `…\session\tabs.json`).
- `.Codex/` is gitignored — the run-winforge skill is tracked via `git add -f`.
- Git: branch + merge into `main`; never force-push; never touch any `cafepromenade/WinTune` remote. Commit messages are bilingual.
