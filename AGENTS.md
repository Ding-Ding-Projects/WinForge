# AGENTS.md — WinForge

Guidance for AI agents working in this repo. Keep it current.

## What this is
WinForge (a.k.a. 視窗調校) is an all-in-one, **fully bilingual (English + 繁體中文/粵語)** Windows 11 control center — 315 registered in-app modules plus a large Windows-tweak catalog — with a **hyper-realistic nuclear-reactor simulator** as its flagship. **.NET `net11.0-windows10.0.26100.0`, WinUI 3**, self-contained, unpackaged.

## Build / run / drive
- **Compile check:** `dotnet build WinForge.sln -c Debug -p:Platform=x64` (must finish with 0 errors; do not hard-code a warning count).
- **Run it:** a plain Debug build is framework-dependent and only shows an *"install .NET"* dialog here — you must **publish self-contained**:
  `dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true`
- **Easiest path — use the skill `.agents/skills/run-winforge/`** (SKILL.md + driver.ps1): builds-if-needed, launches any page via `--page <alias>`, screenshots the window. e.g.
  `powershell -ExecutionPolicy Bypass -File .agents/skills/run-winforge/driver.ps1 -Page reactor -Out shot.png`
- **Reactor/dependent headless tests:** `dotnet run --project tests/ReactorSim.Tests -c Debug` (**63/63**).
- The app is NOT a Start-menu app, so desktop/computer-use screenshot tools mask it — capture via the driver (DWM bounds + `CopyFromScreen`).

- **SDK selection in this workspace:** the WinForge app build/publish targets .NET 11. The machine-wide dotnet command can resolve to SDK 10; before direct app build or publish commands set DOTNET_ROOT to USERPROFILE\.dotnet and prepend it to PATH, or invoke USERPROFILE\.dotnet\dotnet.exe directly. The run-winforge driver performs this selection automatically. The ReactorSim focused harness targets net8.0-windows, so run it with the system dotnet/runtime or clear DOTNET_ROOT first.

## Architecture & conventions (follow these)
- **Add a module = touch 4 places:** `Pages/<X>Module.xaml(.cs)` (class `<X>Module : Page`, namespace `WinForge.Pages`) + logic in `Services/<X>Service.cs`; then register in **(1)** `Services/ModuleRegistry.cs` (Tag `module.xxx`, En, Zh, Glyph, Keywords), **(2)** `MainWindow.xaml.cs` `MapType()` (tag→type), **(3)** `MainWindow.xaml.cs` `ApplyStartPage()` (deep-link aliases for `--page`), **(4)** `MainWindow.xaml` `NavigationViewItem`.
- **Language modes:** all user-facing strings use `Models/Core.cs` `LocalizedText(en, zh)` or `Services/Loc.cs` `Loc.I.Pick(en, zh)`. Cantonese (粵語), not Mandarin. The UI supports Bilingual, Cantonese, and English modes.
- **UI:** build **rich, control-based, animated UI** from real controls (ToggleSwitch/Slider/ComboBox/editors), mirroring `Pages/AwakeModule`. **`Controls/TweakCard` is fully removed** — never reintroduce it. To render `TweakDefinition`s, use **`Controls/ControlRowList`** (`SetTweaks(...)` / `Clear()`), which maps definitions to real control rows with one persistent `InfoBar` and `EntranceThemeTransition`. For bespoke modules see `Pages/RainmeterModule` / `Pages/CloudflareModule`; for catalog-driven pages see `Pages/SettingsHubModule`. Use `Services/FileDialogs.cs` (never WinRT pickers). WebView2 is available. Pitfalls: classic `{Binding}` rather than `x:Bind` in DataTemplates; qualify `Windows.UI.Color`; add to the read-only `RadialGradientBrush.GradientStops`; use `ScrollViewer.*ScrollBarVisibility` attached props; use a named `Loc.I.LanguageChanged` handler and unsubscribe on `Unloaded` (inline lambdas leak).
- **Pure managed C#** for module functionality; do not shell out to / launch the upstream program a module reimplements. Managed NuGet wrappers are fine (note them).
- **Features too large for one WinUI page → WinForge companion apps.** Keep the in-app module GUI and add `Header.ActionContent = HeaderActions.FullFeaturesButton("<id>")`. Web companions live under `WebApps/<id>/index.html` and run in `Pages/CompanionWindows.cs`; native C++ companions live under `native/<id>/main.cpp` and are compiled/cached by `Services/CompanionAppService.cs`. Native sources must compile with both MinGW g++ and MSVC cl; MinGW builds use `-static` so no toolchain DLLs are required beside the exe. Register companions in `CompanionAppService.All`; bridge saves carry a `requestId`, the host replies with matching `saveDone`, and the host also pushes `language` / `theme`. Preparation and interactive launches fail closed while WinForge itself is elevated.
- **Apps that cannot be reimplemented → External App Launcher.** Add a data-only `ExternalAppSpec` to `Catalog/ExternalApps.cs`; `Services/ExternalAppService.cs` resolves, installs the ordered winget dependency chain, and launches the genuine app only while WinForge is at normal integrity. Use `Controls/AppLauncherCard.cs`, `Pages/AppLauncherWindow.cs`, and the `module.applauncher` hub. Wrapper modules cross-link with `HeaderActions.NativeWindowButton("<id>")`. Correct winget IDs and install paths only in `ExternalApps.cs`. This is the deliberate opt-in exception to the upstream-launch rule.
- **Self-updates:** auto-update is normal-integrity only. `WinForgeUpdater` verifies GitHub's SHA-256 asset digest, copies the single-file `WinForgeLauncher` helper outside the install directory, exits, and lets that helper run Inno Setup with persistent logs before relaunching.
- **Secrets:** DPAPI (`ProtectedData`) via the existing stores; never log secrets.

## The flagship reactor (most complex area)
- Engine: `Services/ReactorSimService.cs` (+ `ReactorRps.cs`, `ReactorScenarios.cs`, `ReactorElectrical.cs`, `ReactorReactivityMeter.cs`). UI: `Pages/ReactorModule.xaml(.cs)`, `Pages/ReactorHtmlWindow.cs` (HTML5/WebView2 rooms-as-tabs, assets in `SimAssets/reactor/`), `Pages/ReactorSettingsModule.xaml(.cs)` (real-world/external toggles).
- Services: `FuelFactoryService`, `NuclearWasteService`, `WaterTreatmentService`, `ReactorStatusApiService` (+ `Sdk/ReactorStatusClient.cs`), `ReactorSystemLinkService`, `ReactorHomeAssistantMirror`, `PersistenceService`, `AwakeService`.
- **Safety invariants — keep these:** meltdown→real-shutdown is **default OFF** (abortable); reactor boots **held in MODE 5 cold shutdown** (operator must start it up); waste writes respect a disk free-space floor + a default 50 GB cap; all real-world side-effects are opt-in and reversible.
- **Foundational realism review P1–P5 is resolved and test-verified.** Backward-Euler kinetics is stable; a fresh fully-rodded core is subcritical (**−1018 pcm**); the corrected fuel/SG heat balance sustains a high-power equilibrium (**0.836→0.835 RTP, ~992.5 °C fuel, ~293.4 °C Tavg, 15.46 MPa RCS**) without emergency cooling, SCRAM, or meltdown; decay heat, xenon, saturated-pressurizer behavior, 2-of-4 RPS, OTΔT/OPΔT, SG low-low/AFW, turbine-trip cascade, 109% overpower, and 1/M are implemented. The full harness is **63/63 green**. Historical background: `docs/reactor-realism-review-001.md`; current evidence: `docs/wiki/Reactor-Test-Report.md`.

## Docs
Bilingual wiki under `docs/wiki/` (Home → category pages, generated feature/button references, reactor hub with seven focused pages, manual, test report, and screenshot gallery). Canonical page screenshots live under `docs/`; selected wiki-local assets live under `docs/wiki/images/`.

## Mandatory task completion, docs, screenshots, and Git
- **Every agent task, however small, ends with a commit and push.** Make an intentional bilingual commit for the task, push its branch, merge it into `main` when the task is complete, and push `main`. Never leave completed work only in the working tree.
- **Verify integration before cleanup.** Do not delete a task branch or worktree until a fresh fetch confirms its commit is reachable from `origin/main` and the remote `main` push succeeded. Only then delete the merged local/remote branch and remove its clean worktree; never delete unmerged work.
- **Documentation is part of every feature or page change.** Before declaring a task complete, update the relevant `README.md`, `docs/wiki/`, `docs/`, and GitHub Pages content under `design/content/wiki/` (plus any generated page/reference content that the change affects). Keep English and Cantonese text consistent with the shipped UI.
- **Visual changes require fresh evidence.** Launch every changed page with `.agents/skills/run-winforge/driver.ps1`, capture a high-detail current screenshot, inspect it, and replace the old canonical screenshot in `docs/` and any corresponding GitHub Pages/wiki image. Do not retain stale screenshots for a changed page. If capture is blocked, document the exact blocker in the task handoff and do not claim visual verification.
- **Use a final documentation commit if needed.** If the functional implementation was already committed/pushed before docs or screenshots were refreshed, make and push a second bilingual documentation/screenshot commit; the task is not complete until both code and documentation are on `main`.

## Exhaustive smoke verification
- **Whole-app or feature-completeness requests must use .agents/skills/winforge-exhaustive-smoke/.** Generate its route/page/control manifest before testing; the live registry, deep links, navigation, companions, external launchers, source files, test projects, and documentation form the coverage universe.
- **Keep evidence dimensions separate.** A build is not a feature pass: record static routing, build, focused tests, launch, inspected screenshot, behavior, side-effect safety disposition, and documentation evidence independently. Never label visual verification as passed when capture is blocked.
- **Review changed code honestly at source level.** Map meaningful methods and branches to a test, safe manual exercise, or explicit exemption. Do not claim every source line executed without instrumentation.
- **Default to safe execution.** Use fixtures, dry-runs, validation paths, and reversible probes for privileged, destructive, network, credential, system-tweak, package, and real-world integration features unless the user specifically authorizes live side effects.

## Gotchas
- `audioeditor`, `lightswitch`, and `timelens` were fixed after the original Omega audit; if one captures blank via the driver, rerun with a longer `-WaitMs`.
- The current self-contained runtime has thrown `XamlParseException` for typed `NumberBox.Value` and `ToggleSwitch.IsOn` literals. Set those defaults in code-behind under the page's suppression guard, then deep-link smoke-test the page.
- `--page <alias>` is reliable; bare `--reactor` can land on the Dashboard if a session was restored.
- Reactor restoring a stale/melted autosave → delete `%LOCALAPPDATA%\WinForge\state` (+ `…\session\tabs.json`).
- Git: branch + merge into `main`; never force-push; never touch unrelated personal remotes. Commit messages are bilingual.
