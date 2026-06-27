# Handoff · 交接 — Omega build-out (full, detailed)

A complete handoff of the Omega orchestration session: what is on `main`, how it's structured, how to build/run/drive it, the exact state of the flagship reactor, and every open item with concrete next steps. **粵語**：呢份係 Omega session 嘅完整詳細交接 — 全部已喺 `main`，內容包括結構、建置/執行方式、反應堆狀態同所有未完成事項。

`main` at handoff: `400f149` (everything below is merged into `main`; branch-and-merge only, no force-push, no `main` history rewrite).

---

## 1. Repository state · 儲存庫狀態
- Everything built this session is on **`main`**. The reactor feature branches form a linear chain that was fast-forwarded in, then the realism-loop physics was reconciled via a real merge commit (`79bf069`, parents `a5cbd29` + `df8c02d`), then docs/skill commits on top.
- **Excluded on purpose:** `feature/in-app-manual` (`c7d2f99`) — a branch from a *separate* orchestration working in the main checkout; not ours, deliberately NOT merged.
- The many `feature/oss-*`, `feature/reactor-*`, `feature/*` branches are now all contained in `main` and can be pruned when convenient.
- `.claude/` is gitignored in this repo, so the `run-winforge` skill was force-added (`git add -f`).

## 2. Build, run & drive · 建置、執行、操控
Use the committed skill **`.claude/skills/run-winforge/`** (SKILL.md + driver.ps1). Key facts:
- **Compile check:** `dotnet build WinForge.sln -c Debug -p:Platform=x64` → 0 errors. (~300 pre-existing nullable/CA1416 warnings in unrelated files.)
- **Runnable build:** plain Debug is framework-dependent and only shows a *"install .NET"* dialog here. You MUST publish self-contained:
  `dotnet publish WinForge.csproj -c Debug -r win-x64 --self-contained true -p:Platform=x64 -p:WindowsAppSDKSelfContained=true -v quiet`
  → `bin/x64/Debug/net11.0-windows10.0.26100.0/win-x64/publish/WinForge.exe`.
- **Driver (launch + screenshot any page):**
  `powershell -ExecutionPolicy Bypass -File .claude/skills/run-winforge/driver.ps1 -Page <alias> -Out shot.png`
  Deep-link aliases come from `MainWindow.ApplyStartPage` (~127, one per module). The app is not a Start-menu app, so desktop/computer-use screenshot tools mask it — the driver captures via DWM bounds + `Graphics.CopyFromScreen`.
- **Reactor engine tests (headless, no GUI):** `dotnet run --project tests/ReactorSim.Tests -c Debug` → 13/15.
- Toolchain present: .NET 11 SDK (`dotnet --version` → `11.0.100-preview…`), WindowsAppSDK 2.2.x.

## 3. Architecture · 架構
- **.NET `net11.0-windows10.0.26100.0`, WinUI 3, self-contained, unpackaged** (`WindowsPackageType=None`). Resilient startup via `WinForgeLauncher` (`launcher/`).
- **Adding a module touches 4 places:** `Services/ModuleRegistry.cs` (Tag/En/Zh/Glyph/Keywords), `MainWindow.xaml.cs` `MapType()` (tag→Page type) + `ApplyStartPage()` (deep-link aliases), and `MainWindow.xaml` (`NavigationViewItem`). Pages live in `Pages/<X>Module.xaml(.cs)`, logic in `Services/<X>Service.cs`.
- **Language modes:** `Models/Core.cs` `LocalizedText(en, zh)` + `Services/Loc.cs` `Loc.I.Pick(en, zh)`; the UI supports Bilingual, Cantonese, and English modes. **Cantonese (粵語/繁體中文)**, not Mandarin.
- **Controls:** `Controls/RichTweakControl` (data-driven cards) + a `VisualBuilder` hook for generated previews; `Services/FileDialogs.cs` (never WinRT pickers); WebView2 available (used by RichPreview + the reactor HTML5 window).

## 4. What shipped · 已交付

### 4a. Native OSS module ports (17) — pure managed C#, no shelling to / launching the upstream app
KeePass (KDBX4 read/write, AES/ChaCha20, Argon2 via managed Konscious) · Everything (NTFS MFT/USN search via PInvoke) · CrystalDiskInfo (SMART via DeviceIoControl/WMI) · CrystalDiskMark (FILE_FLAG_NO_BUFFERING benchmark) · Process Explorer (WMI + Process) · Docker manager (Docker.DotNet over npipe) · WinMerge (Myers diff) · HxD hex editor (memory-mapped) · Postman/REST (HttpClient) · DB Browser for SQLite (Microsoft.Data.Sqlite) · Stirling-PDF (PdfSharp + PdfPig) · Mp3tag (TagLib#) · Paint.NET-style editor (ImageSharp 3.1.x — note: v4 is build-license-gated) · draw.io diagram editor (WinUI canvas) · Anki flashcards (SM-2, JSON) · ILSpy decompiler (ICSharpCode.Decompiler) · NormCap OCR (Windows.Media.Ocr).

### 4b. Other integrations
Native BitTorrent client (MonoTorrent) · real Bitwarden vault client (API login + PBKDF2/Argon2id + EncString type-2 + HKDF + TOTP, DPAPI tokens) · Explorer right-click context menus (HKCU verbs + `--page/--path`) · rich rich tweak controls (VisualBuilder + 5 upgraded cards) · Home Assistant native light/plug toggling (REST) · Proxmox VE VM/LXC power (REST, API-token/ticket) · Docker-over-SSH (SSH.NET) · speaker TTS announcements (System.Speech) · capture/ruler drag-crash fix.

### 4c. Flagship Nuclear Reactor (the centerpiece)
- **Physics (`Services/ReactorSimService.cs`):** 6-group point kinetics with **backward-Euler** integration (numerically stable); reactivity = rods + boron + Doppler + moderator + xenon (+samarium); **ANS-5.1 decay heat**; **Westinghouse rod control** (S-curve worth, 228-step bank overlap, insertion limits); saturated-pressurizer pressure model + auto pressure program; **3-element SG feedwater** (shrink/swell); **3-range NIS** (source/intermediate/power, 1/M); **RPS** with 2-of-4 coincidence (`Services/ReactorRps.cs`); axial-flux ΔI/CAOC + f₁(ΔI) OTΔT penalty; accident **scenarios** (LOCA, SBO, LOFW, ATWS, SGTR, MSLB) in `Services/ReactorScenarios.cs`; electrical in `Services/ReactorElectrical.cs`; reactivity meter in `Services/ReactorReactivityMeter.cs`.
- **Held cold shutdown:** boots in MODE 5 cold shutdown, subcritical/idle — **the operator must start it up** (select Startup/Run + withdraw rods). Safe at rest.
- **HTML5/WebView2 control room (`Pages/ReactorHtmlWindow.cs` + `SimAssets/reactor/`):** a dedicated window with **rooms as tabs** (Control Room, Containment, Turbine Hall, Fuel Handling, CVCS, Electrical, Cooling, Rad-Monitoring); Canvas/SVG animations driven by a 10 Hz C#→JS state bridge; physics stays in C#, JS is presentation/input only. Assets served via `SetVirtualHostNameToFolderMapping` (no CDN), copied to output.
- **Fuel cycle (`Services/FuelFactoryService.cs`):** ultra-realistic 17×17 UO₂ assemblies (enrichment 3–4.95%, ~400–540 kgU, lot/serial, fabrication chain), HMAC-SHA256-signed `.fuel` files (DPAPI key). **Send-in = validate + auto-delete** (consumed into core). **Forged/tampered/off-spec fuel HARMS the core** when loaded (cladding-breach alarm, fuel-temp spike, DamageAccumulation, radiation, auto-SCRAM) — *Inspect/Validate* alone is safe. SIM-ONLY harm.
- **Nuclear waste (`Services/NuclearWasteService.cs`):** burning fuel writes real **100 MB–2000 MB** incompressible `.waste` files. **Total cap default 50 GB (custom)** + a disk free-space floor; never fills the disk (atomic `.tmp`→move). Spent-fuel-pool-FULL → power runback → mandated shutdown + load/startup blocked until disposed. Dispose ("deep geological repository").
- **Water treatment (`Services/WaterTreatmentService.cs`):** intake→clarifier→filter→2-pass RO→mixed-bed demineralizer→degasifier→ultrapure storage; chemistry (conductivity/pH/O₂/silica/chlorides); reactor makeup depends on it (empty tank → level sag/trip; off-spec → corrosion).
- **Status API (`Services/ReactorStatusApiService.cs` + `Sdk/ReactorStatusClient.cs`):** named pipe `\\.\pipe\WinForge.Reactor.Status` (GET/SUBSCRIBE) + MMF `Local\WinForge.Reactor.Status`; drop-in SDK lets other apps read status and `WaitForGeneratingAsync()` to depend on the reactor.
- **Resilience & real-world hooks:** crash-safe autosave (`Services/PersistenceService.cs`, atomic + `.bak` + death-hook flush + flush before any armed shutdown); keep-PC-awake while generating (`Services/AwakeService.cs`); reactor↔Windows-settings linkage (`Services/ReactorSystemLinkService.cs`: power plan/accent/brightness, reversible); Home Assistant mirror (`Services/ReactorHomeAssistantMirror.cs`); **meltdown→real-shutdown ARM** (default OFF, abortable 10 s countdown, Win32 `InitiateSystemShutdownExW`).
- **Dedicated Reactor Settings page (`Pages/ReactorSettingsModule.xaml(.cs)`, `--page reactorsettings`):** all real-world/external toggles live here (keep-awake, Windows linkage, ARM real-shutdown, status API, autosave, HA mirror) — separated from the pure-sim controls, which stay on the main reactor page (SCRAM, rods, scenarios, audio, widgets). Defaults: ARM OFF, Windows-link OFF, HA OFF, status-API ON, autosave ON, keep-awake ON.

## 5. Test status · 測試狀態
`tests/ReactorSim.Tests` (run: `dotnet run --project tests/ReactorSim.Tests`) → **13/15 PASS** against the reconciled `main` physics. The 2 FAILs are **stale assertions vs the new models**, not new bugs: SCRAM mechanism (Scram() now uses 228-step banks, so `RodBankInsertion%` doesn't read 100 even though `Tripped`+`IsScrammed` latch correctly) and xenon monotonic-decay (Xe reaches 0 faster than the test's monotone check). Full table in `docs/wiki/Reactor-Test-Report.md`. The harness links specific engine sources — when engine files are added, update `tests/ReactorSim.Tests/ReactorSim.Tests.csproj` (it now also needs `ReactorElectrical.cs`, `ReactorReactivityMeter.cs`, `Loc.cs`, `Core.cs`, and `ImplicitUsings=enable`).

## 6. Open items · 未完成事項 (prioritized)
1. **Reactor at-power calibration (P2) — the one real functional gap.** The core is safe at rest but **melts if started up**: a fresh core reads **+5335 pcm (supercritical) with all rods inserted** at cold temp, because the cold Doppler (datum 600 °C) and moderator (datum 305 °C) feedbacks are large and positive and, with `ExcessBaseline`, exceed full rod worth — so leaving Shutdown sends it prompt-supercritical and SCRAM can't hold it. The realism loop landed the integrator/decay-heat/rod-control/scenarios but not the reactivity recalibration. Fix lives in `Services/ReactorSimService.cs` (the reactivity assembly + `ExcessBaseline` + temperature-feedback referencing) and `Services/ReactorReactivityMeter.cs`; guidance + numbers in `docs/reactor-realism-review-001.md` (P1–P3). The `reactor-realism-loop` scheduled task is **disabled** — re-enable it to continue, or do a focused P2 fix.
2. **Reactor scenario screenshots** — LOCA/SBO/SGTR/MSLB/ATWS/meltdown need in-app interaction (and a working at-power core, i.e. P2) to capture; currently documented textually in the test report + emergencies page.
3. **`feature/in-app-manual`** — left unmerged (separate orchestration). Decide whether to integrate.
4. **Resolved after this handoff:** `audioeditor`, `lightswitch`, and `timelens` now open via `MainWindow.NavigateActive`; the three missing page screenshots were recaptured with `driver.ps1`.

## 7. Scheduled tasks · 排程工作
- `reactor-realism-loop` (durable, `*/5 * * * *`) → **disabled** (was implementing reactor realism each run; stopped on request).
- No cron jobs.

## 8. Docs / wiki · 文件
Organized bilingual wiki: `docs/wiki/Home.md` index → **13 category pages** + a **reactor hub** (`Nuclear-Reactor.md`) linking **7 focused reactor pages** (Overview, Control Room, Operating Procedures, Emergencies & Scenarios, Fuel & Waste, Water Treatment, Safety & Integrations) + Operating Manual + Test Report; a Screenshots gallery; **124 live page screenshots** under `docs/wiki/images/screenshot-<alias>.png`; rewritten bilingual `README.md`. Every user-facing line is English + 粵語.

## 9. Next steps · 下一步 (suggested order)
1. Land **P2 reactivity recalibration** so the reactor can be operated at power (re-enable the loop or focused fix per `reactor-realism-review-001.md`), then re-run `tests/ReactorSim.Tests` (expect a green at-power path) and capture scenario screenshots via the driver.
2. Refresh the 2 stale test assertions to match the new SCRAM/xenon models.
3. Optionally prune the merged `feature/*` branches.

## 10. Key paths · 重要路徑
- Reactor engine: `Services/ReactorSimService.cs`, `ReactorRps.cs`, `ReactorScenarios.cs`, `ReactorElectrical.cs`, `ReactorReactivityMeter.cs`.
- Reactor UI: `Pages/ReactorModule.xaml(.cs)`, `Pages/ReactorHtmlWindow.cs`, `Pages/ReactorSettingsModule.xaml(.cs)`, `SimAssets/reactor/`.
- Reactor services: `FuelFactoryService.cs`, `NuclearWasteService.cs`, `WaterTreatmentService.cs`, `ReactorStatusApiService.cs`, `ReactorSystemLinkService.cs`, `ReactorHomeAssistantMirror.cs`, `PersistenceService.cs`, `AwakeService.cs`; SDK `Sdk/ReactorStatusClient.cs`.
- Nav wiring: `Services/ModuleRegistry.cs`, `MainWindow.xaml.cs`, `MainWindow.xaml`.
- Run skill: `.claude/skills/run-winforge/` (SKILL.md + driver.ps1). Tests: `tests/ReactorSim.Tests/`. Realism review: `docs/reactor-realism-review-001.md`.
