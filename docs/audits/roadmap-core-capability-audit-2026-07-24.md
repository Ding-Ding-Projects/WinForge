# Core roadmap capability audit — 2026-07-24 · 核心路線圖功能審核

## Outcome · 結果

This audit reconciles eight stale sections in `docs/ROADMAP.md` against the .NET WinUI 3 application. The original classification was revalidated against source; the 2026-07-24 Browser Control and Media completions closed eleven evidence-backed gaps each, and the Windows/System plus Maintenance delivery closed the remaining eight gaps in those two sections. Of 115 roadmap entries, **104 now have complete source-backed delivery evidence** and **11 remain unchecked** because implementation is absent or partial in other sections.

今次審核將 `docs/ROADMAP.md` 八個過時章節同 .NET WinUI 3 app 原始碼逐項核對；瀏覽器控制同 Media 各自補齊十一個有證據缺口，Windows／System 加 Maintenance 亦補齊兩節餘下八項。115 項之中，**104 項有完整原始碼交付證據**，**11 項因其他章節未有實作或者只做咗一部分而繼續留空**。

| Section · 章節 | Audited · 審核 | Shipped `[x]` · 已交付 | Remaining `[ ]` · 餘下 |
|---|---:|---:|---:|
| Windows 11 | 13 | 13 | 0 |
| ViveTool | 15 | 15 | 0 |
| Media | 15 | 15 | 0 |
| Maintenance | 15 | 15 | 0 |
| Dev & Terminal | 15 | 9 | 6 |
| Home Assistant | 14 | 13 | 1 |
| Archives | 14 | 10 | 4 |
| Browser Control | 14 | 14 | 0 |
| **Total · 總數** | **115** | **104** | **11** |

## Evidence standard · 證據標準

An entry is checked only when all of the following are present:

1. A reachable user-facing control: a dedicated page/button/handler, or a catalog `TweakDefinition` rendered by `CategoryPage` / `SettingsHubModule` through `ControlRowList.SetTweaks(...)`.
2. A concrete mechanism: the handler calls a service, API, registry binding, or command runner that performs the advertised capability. A label, placeholder, or neighbouring feature is not enough.
3. Documentation or verification evidence: generated feature documentation under `docs/features/`, generated module/button pages under `docs/wiki/`, or a focused static/source test.

只有同時有可達控制、實際執行機制，同埋文件／驗證證據先會剔選。`Controls/ControlRowList.cs` is the shared binding proof for catalog rows: action buttons invoke `RunAsync`, toggles invoke `SetIsOn`, and choice/slider controls invoke their registered setters. Dedicated modules are registered in `Services/ModuleRegistry.cs`, mapped/deep-linked in `MainWindow.xaml.cs`, and documented by generated module/button pages.

The focused guard is `tools/Test-RoadmapCoreAudit.ps1`. It asserts section totals and checked counts, confirms that every one of the 115 exact roadmap titles appears in this audit, verifies Browser Control, Media, and System Maintenance implementation markers, and locks the aggregate **104/115** result. `tests/MediaWorkflowCore.Tests` separately covers two-pass sequencing, parsers, argument boundaries, cancellation, staged-output preservation, and owned scratch cleanup; `tests/SystemMaintenanceCore.Tests` validates bounded plans without mutating the host.

## Windows 11 · Windows 11

### Shipped — 13 · 已交付 — 13

| Roadmap capability | Concrete implementation evidence | Documentation evidence |
|---|---|---|
| **Disable Wallpaper JPEG Compression (Import Quality 100)** | `Catalog/AppearanceTweaks.cs`, `appearance.wallpaper-quality`: `CustomToggle` writes/deletes `JPEGImportQuality=100` and calls `WallpaperHelper.ReapplyCurrentWallpaper()`; rendered by `ControlRowList`. | `docs/features/appearance-personalisation/appearance.wallpaper-quality.md` |
| **Enable Verbose Startup/Shutdown Status Messages** | `Catalog/SystemTweaks.cs`, `system.verbose-status`: HKLM `VerboseStatus` toggle with admin and sign-out metadata. | `docs/features/system-boot/system.verbose-status.md` |
| **Toggle Cloud Clipboard Sync Across Devices** | `Catalog/SystemTweaks.cs`, `system.clipboard-history` and `system.cloud-clipboard`; `Services/ClipboardService.cs` also reads/writes `CloudClipboardAutomaticUpload`. | Generated feature pages for both catalog IDs. |
| **Disable Mouse Pointer Acceleration (Enhance Pointer Precision)** | `Catalog/Win11ProTweaks.cs`: `w11p.inputintl.mouse-accel`, `mouse-threshold1`, and `mouse-threshold2` bind the three required HKCU mouse values. | Generated Windows 11 Advanced feature pages for all three IDs. |
| **Set Keyboard Repeat Delay & Repeat Rate to Fastest** | `Catalog/Win11ProTweaks.cs`: `keyboard-delay` (0–3) and `keyboard-speed` (0–31) sliders write the exact string values. | Generated Windows 11 Advanced feature pages for both IDs. |
| **Toggle Notifications / Configure Focus & Quiet-Hours Rules** | `Catalog/Win11ProTweaks.cs`: master notification/sound/lock-screen toggles plus `open-focus` and `open-notifications` controls. | Generated feature pages under `docs/features/windows-11-advanced/`. |
| **Tune Snap Assist & Snap Layout Behavior** | `Catalog/Win11ProTweaks.cs`: `snap-assist`, `snap-fill`, `joint-resize`, `snap-bar`, `snap-flyout`, `window-arrangement`, and `snap-suggestions` bind the advertised Explorer/Desktop values. | Generated feature pages for the Snap controls. |
| **Change Regional First Day of Week & Short Date Format** | `Catalog/Win11ProTweaks.cs`: `first-day-week` and `short-date` choices write `iFirstDayOfWeek` and `sShortDate`. | Generated feature pages for both IDs. |
| **Enable 'End Task' on Taskbar Right-Click** | `Catalog/WinaeroTweaks.cs`, `winaero.desktop-explorer.taskbar-end-task`: registry toggle for `TaskbarDeveloperSettings\TaskbarEndTask`. | Generated Winaero feature page. |
| **Restore Classic (Win10) Right-Click Context Menu in Explorer** | `Catalog/ExplorerTweaks.cs`, `explorer.classic-menu`: creates/removes the `{86ca1aa0-...}\InprocServer32` override. | Generated Explorer feature page. |
| **Configure Storage Sense Cadence & Recycle Bin Purge** | `Pages/SystemDoctorsModule.xaml.cs::BuildStorageSenseDoctor` exposes enablement, cadence (`0/1/7/30`), Recycle Bin retention, and Downloads retention. `SystemMaintenanceService.ApplyStorageSense` validates and writes `01`, `2048`, `256`, and `512` under the current-user StoragePolicy key. | `docs/features/system-maintenance/storage-sense-policy.md` and the System Doctors wiki guide. |
| **Enable Filter Keys / Slow Keys for Accessibility** | `BuildFilterKeysDoctor` exposes enablement plus all four bounded timings. `SystemMaintenanceService` reads the live `FILTERKEYS` structure, preserves unrelated accessibility bits, and persists all timings through `SPI_SETFILTERKEYS` with live notification; the catalog toggle reuses that path. | `docs/features/system-maintenance/filter-keys.md`; the pure contract harness verifies flag preservation and timing bounds. |
| **Export / Import Default App Associations (machine-wide)** | `BuildDefaultAssociationsDoctor` uses the repository COM file dialogs and an explicit import decision gate. `SystemMaintenanceService` validates an absolute `.xml` path and invokes DISM `/Export-DefaultAppAssociations:` or `/Import-DefaultAppAssociations:` through a real argument vector at administrator integrity. Copy explains that the template applies to new profiles and does not bypass protected per-user `UserChoice`. | `docs/features/system-maintenance/default-app-associations.md`; focused tests cover path/extension/existence validation. |

### Remaining gaps — 0 · 餘下缺口 — 0

No unchecked Windows 11 item remains in this audited section. · 呢個已審核 Windows 11 章節冇剩低未交付項目。

## ViveTool · ViveTool

### Shipped — 15 · 已交付 — 15

Common evidence: `Pages/ViveToolModule.xaml(.cs)` is the reachable UI; `Services/ViveToolService.cs` resolves and runs the real executable; `Services/ViveDictionary.cs` explicitly treats dictionary IDs as labels and intersects named groups with the live `/query` store. Generated evidence lives in `docs/wiki/features/system/vivetool.md` and its 11 button pages.

| Roadmap capability | Concrete handler/service evidence |
|---|---|
| **Feature flag searchbar (query all states)** | `DetectAndLoad` / `Reload` call `QueryAsync`; `ApplyFilter` and `Filter_TextChanged` filter parsed live-store rows by ID/name/state. |
| **Enable feature by human-readable name** | `Toggle_Click` resolves dictionary candidates against `_all` and shows the resolved IDs before calling `EnableMany`. |
| **Disable / reset a feature flag** | Row actions in `Actions_Click` call `ViveToolService.Disable` and `.Reset`; named toggles call `ResetMany`. |
| **Full reset (clear all overrides)** | `FullReset_Click` presents a decision dialog, then calls `ViveToolService.FullReset()` (`/fullreset`). |
| **Export / import flag profiles** | `Export_Click` / `Import_Click` use `FileDialogs` and call `/export` / `/import`. |
| **Show Last Known Good rollback status** | `Lkg_Click` calls `LkgStatus()` (`/lkgstatus`) and displays raw output. |
| **Toggle File Explorer tabs / duplicate tab** | Named group in `ViveDictionary.NamedToggles`; live-store intersection in `Toggle_Click`, then `EnableMany` / `ResetMany`. |
| **Toggle new Start menu (scrollable, categories, Phone panel)** | Named multi-ID Start group uses the same live-store resolution path. |
| **Toggle modern context menus / command bar** | Named context-menu/command-bar group uses live-store resolution and shell apply. |
| **Toggle taskbar 'End Task' on right-click** | Named End Task group uses live-store resolution before mutation. |
| **Toggle seconds in the system clock** | Named system-clock group uses live-store resolution before mutation. |
| **Toggle new Snap Layouts / suggested groupings** | Named multi-ID Snap group uses live-store resolution before mutation. |
| **Toggle desktop / always-on Energy Saver** | Named Energy Saver group uses live-store resolution before mutation. |
| **Toggle AI actions / Click to Do surfaces** | Named AI/Click-to-Do group uses live-store resolution before mutation. |
| **Scan available-but-disabled experiments + restart-explorer helper** | `Scan_Click` calls `ScanAvailableDisabled`; `RestartExplorer_Click`, `Reboot_Click`, and `OfferApply` expose explicit apply helpers. |

### Remaining gaps — 0 · 餘下缺口 — 0

No unchecked ViveTool item remains in this audited section. · 呢個已審核章節冇剩低未交付項目。

## Media · Media

### Shipped — 15 · 已交付 — 15

| Roadmap capability | Concrete implementation evidence | Documentation evidence |
|---|---|---|
| **Normalize loudness to broadcast standard (EBU R128)** | `NormalizeR128Btn` calls `MediaWorkflowService.NormalizeLoudnessAsync`; `MediaWorkflowExecutor` measures `input_i/input_tp/input_lra/input_thresh`, maps them to all four `measured_*` fields, and runs the second linear pass into a staged output. | `docs/features/media-capture/media-studio-workflows.md`; focused harness cases 1–2. |
| **Auto-trim silence from start/end and gaps** | `TrimSilenceBtn` calls the executor's full `silenceremove` filter with `start_periods=1` and `stop_periods=-1`, so leading, trailing, and internal silence are covered; the executor rejects video containers before launch to prevent A/V desynchronization. | Media workflow guide; focused harness case 3. |
| **Make high-quality GIF (two-pass palette)** | `Pages/GifLabModule.xaml.cs::Export_Click` calls `GifLabService.Export`; its GIF branch runs separate `palettegen` and `paletteuse` ffmpeg passes with a temporary palette file. | `docs/wiki/features/media-capture/giflab.md` and its generated Export button page. |
| **Stabilize shaky video (vidstab two-pass)** | `StabilizeBtn` runs `vidstabdetect`, verifies the GUID-scoped transform exists, then runs `vidstabtransform`; the owned workspace is deleted in `finally`. | Media workflow guide; focused harness case 4. |
| **Auto-detect and crop black bars** | `AutoCropBtn` samples 200 frames through `cropdetect=round=2`, parses the final valid crop rectangle, then applies that rectangle in a second staged encode. | Media workflow guide; focused harness case 5. |
| **Lossless cut on keyframes (no re-encode)** | `Pages/MediaModule.xaml` wires `TrimCopyBtn`; `TrimCopy_Click` accepts user start/duration and calls ffmpeg with `-ss`, `-t`, and `-c copy`. | Generated Media module/button documentation. |
| **Concat / join clips without re-encoding** | `ChooseConcatBtn` accepts an ordered multi-selection; `ConcatCopyAsync` writes an escaped UTF-8 concat list and executes `-f concat -safe 0 -c copy -avoid_negative_ts make_zero`. | Media workflow guide; focused harness case 6. |
| **GPU hardware encode with NVENC** | `DetectNvencBtn` parses ffmpeg's encoder list and performs a real one-frame hardware probe for each NVENC codec; `EncodeNvencBtn` re-probes the selected `h264_nvenc`/`hevc_nvenc`/`av1_nvenc` codec before encoding with preset/tune/RC/CQ controls. | Media workflow guide; focused harness cases 7–8. |
| **Two-pass target-size encode (Discord/email cap)** | `TargetSizeBtn` takes MiB/audio-kbps inputs; ffprobe supplies duration, `ComputeTargetVideoBitrateKbps` applies the documented formula and bounds, and two x264 passes share a GUID-scoped passlog. | Media workflow guide; focused harness cases 9–10. |
| **Burn-in or soft-mux subtitles (SRT/ASS)** | The subtitle picker accepts SRT/ASS and exposes libass burn-in or soft mux; filter paths are escaped, MP4 uses `mov_text`, other compatible containers copy, and soft tracks receive `language=yue`. | Media workflow guide; focused harness case 11. |
| **Extract chapters and split video by chapter** | `ReadChaptersBtn` parses `ffprobe -show_chapters` JSON; `SplitChaptersBtn` bounds the set to 200, sanitizes/collision-proofs filenames, and makes one stream-copy output per valid interval. | Media workflow guide; focused harness case 12. |
| **Contact sheet / storyboard thumbnails** | `Catalog/MediaOperations.cs`, `media.contact-sheet`, runs ffmpeg `select`, `scale`, and `tile` filters. | Generated feature page for `media.contact-sheet`. |
| **Convert HEIC/JPEG-XL photos to JPG/PNG (batch)** | The folder workflow enumerates HEIC/HEIF/JXL locally, limits the batch to 500, requires a separate output folder, collision-proofs stems, and converts one frame per input to JPG or PNG. | Media workflow guide; focused harness case 13. |
| **Strip EXIF/GPS metadata from photos** | `StripMetadataBtn` calls `-map_metadata -1 -map_metadata:s -1 -c:v copy` through a staged same-format output, removing metadata without re-encoding image pixels. | Media workflow guide; focused harness case 14. |
| **Make animated WebP from video (smaller than GIF)** | `Catalog/MediaOperations.cs`, `media.to-animated-webp`, invokes `-vf "fps=15,scale=480:-1:flags=lanczos" -c:v libwebp -loop 0`; it does not set an explicit quality value. | Generated feature page for `media.to-animated-webp`. |

### Remaining gaps — 0 · 餘下缺口 — 0

No unchecked Media item remains in this audited section. The new workflows preserve argument boundaries through `ProcessStartInfo.ArgumentList`, stage outputs before promotion, bound batches and chapter counts, and clean owned scratch files on failure or cancellation. · 呢個已審核 Media 章節冇剩低未交付項目；新工作流程會保留參數邊界、成功先升格暫存輸出、限制批次／章節數量，失敗或取消都會清理自家 scratch 檔。

## Maintenance · Maintenance

### Shipped — 15 · 已交付 — 15

| Roadmap capability | Concrete implementation evidence | Documentation evidence |
|---|---|---|
| **Services Manager (start/stop/startup type)** | `Pages/ServicesModule.xaml.cs` lists services and invokes `ServiceManager.Start`, `.Stop`, `.Restart`, and `.SetStartup`. | `docs/wiki/features/system/services.md` plus generated button pages. |
| **SMART / disk health & wear counters** | `Catalog/MaintenanceTweaks.cs` invokes `Get-PhysicalDisk` and `Get-StorageReliabilityCounter`; the Disk Health module supplies a richer reachable surface. | Generated maintenance and Disk Health docs. |
| **Retrim SSD / optimize drives (TRIM)** | `maint.disk-retrim` runs `Optimize-Volume -ReTrim` through the shared action path. | `docs/features/maintenance-diagnostics/maint.disk-retrim.md` |
| **Create / list restore points** | Maintenance catalog exposes `Checkpoint-Computer`, `Get-ComputerRestorePoint`, and protection enablement operations. | Generated maintenance feature pages. |
| **Scheduled-task browser (query / run / disable)** | `Pages/ScheduledTasksModule.xaml.cs` invokes `TaskSchedulerManager` list/run/stop/enable/disable methods. | Generated module/button docs. |
| **Event-log error/warning digest** | `maint.recent-critical-events` queries recent Critical/Error/Warning events; Event Viewer offers the broader in-app reader. | Generated maintenance/Event Viewer docs. |
| **Generate energy / battery / sleep report** | `maint.energy-report`, `maint.battery-report`, and `maint.sleep-study` invoke the corresponding `powercfg` report commands. | Generated feature pages for all report IDs. |
| **Diagnose what blocks sleep / wakes the PC** | Maintenance operations run `powercfg /requests`, `/lastwake`, and `/devicequery wake_armed`; `SystemDoctors` contains parsed diagnostics too. | Generated maintenance/power documentation. |
| **System file & image integrity repair** | `maint.sfc-scannow` and `maint.dism-restorehealth` run the real SFC/DISM repair commands with admin metadata. | Generated feature pages for both repair operations. |
| **Bulk update all apps** | Package Manager Updates view gathers eligible updates across supported managers and queues per-manager/all-manager batches through its shared coordinator. | Existing checked roadmap note plus Package Manager module/button docs. |
| **Pause / resume Windows Update** | `BuildWindowsUpdateDoctor` offers bounded 7–35 day pauses and resume. `SystemMaintenanceService.PauseWindowsUpdate` writes feature/quality/global start, end, and expiry values plus pause flags under HKLM; resume removes every present value and reports any failed deletion. | `docs/features/system-maintenance/windows-update-pause.md`; focused tests lock supported durations and UTC timestamp format. |
| **Driver list / export / rollback hints** | `BuildDriverRollbackDoctor` lists actual `%WINDIR%\INF\oem*.inf` identities, exports one or all with `pnputil /export-driver`, restores exported INFs with `/add-driver ... /subdirs /install`, and enables conservative `/delete-driver <oem#.inf> /uninstall` rollback only after that exact package exported successfully in the current session. It never adds `/force` or `/reboot`. | `docs/features/system-maintenance/driver-backup-rollback.md`; focused tests cover validation, argument boundaries, restore switches, and the conservative rollback contract. |
| **Startup impact / autoruns audit** | `BuildStartupAuditDoctor` calls `SystemMaintenanceService.AuditStartupAsync`, which inventories Run, RunOnce, both Startup folders, Winlogon, AppInit DLLs, automatic services, and boot/logon scheduled tasks. It labels a documented source-risk impact rather than inventing boot-time telemetry and renders commands locally for review. | `docs/features/system-maintenance/startup-autoruns-audit.md`; focused tests lock every source-to-impact classification. |
| **Component store cleanup (WinSxS / ResetBase)** | `BuildComponentStoreDoctor` shows a persistent irreversible warning, requires a separate acknowledgement, and then shows a decision dialog before `dism /Online /Cleanup-Image /StartComponentCleanup /ResetBase`. Smoke verification does not execute the mutation. | `docs/features/system-maintenance/component-store-resetbase.md`; focused tests lock the exact argument vector. |
| **Reset / re-register a stuck Store app** | `BuildStoreAppDoctor` loads a user-selected non-framework Store app through `UninstallManager`. Reset uses that identity with `Reset-AppxPackage` behind a destructive decision; re-register validates its installed `AppXManifest.xml` before `Add-AppxPackage -DisableDevelopmentMode -Register`. | `docs/features/system-maintenance/store-app-repair.md`; focused tests validate package identities and both PowerShell contracts. |

### Remaining gaps — 0 · 餘下缺口 — 0

No unchecked Maintenance item remains in this audited section. · 呢個已審核 Maintenance 章節冇剩低未交付項目。

## Dev & Terminal · 開發同終端機

### Shipped — 9 · 已交付 — 9

| Roadmap capability | Concrete implementation evidence | Documentation evidence |
|---|---|---|
| **Manage PATH entries (user/system)** | `Pages/PathDoctorModule.xaml.cs` provides scope selection, reorder/add/remove/dedupe/dead-path cleanup and Apply via `PathDoctorService`. | `docs/wiki/features/dev-helpers/pathdoctor.md` plus nine button pages. |
| **Edit user & system environment variables** | `Pages/EnvVarsModule.xaml.cs` adds/edits/deletes both scopes through `Services/EnvVarService.cs`. | Generated Environment Variables module/button docs. |
| **Export & restore package sets** | Package Manager Bundles exports/imports editable JSON/`.ubundle`, YAML, and XML while preserving manager, ID, version, source, and options. | Existing checked roadmap note and Package Manager docs. |
| **Upgrade all outdated packages** | Package Manager implements upgrade batches across its available WinGet, Scoop, Chocolatey, pip, npm, .NET tool, PowerShell, Cargo, Bun, and vcpkg engines. | Existing checked roadmap note and Package Manager docs. |
| **Docker container & image dashboard** | `Pages/DockerModule.xaml.cs` uses `DockerService` / Docker.DotNet over the Engine API for containers, logs, exec, stats, images, volumes, networks, and Compose. | `docs/wiki/features/apps-git-git/docker.md` plus 22 button pages. |
| **Run the real Claude / Codex / OpenCode CLI** | `Pages/AiAgentsModule.xaml.cs` builds installed-agent cards; `AiAgentService` defines the real `claude`, `codex`, and `opencode` CLIs and launches them in the terminal with a selected working directory. | Generated AI Agents module/button docs. |
| **Generate & copy SSH key for Git** | `Pages/SshModule.xaml.cs` exposes Generate/copy actions; `SshService` locates and invokes `ssh-keygen.exe` and returns public-key text. | `docs/wiki/features/apps-git-git/ssh.md` plus generated button pages. |
| **Open WSL distro management** | `Pages/WslVmModule.xaml.cs` and `WslVmService` implement list/online install, launch, set default, terminate, unregister, export, import, and shutdown. | `docs/wiki/features/apps-git-git/wsl.md` plus generated button pages. |
| **Tunnel a local port (share dev server)** | The reachable Cloudflare module exposes `cf.quick-tunnel`; its catalog handler launches the real `cloudflared tunnel --url http://localhost:8080` command, which supplies a usable local-port sharing action. | Generated Cloudflare module/operation documentation. |

### Remaining gaps — 6 · 餘下缺口 — 6

| Unchecked roadmap capability | Factual reason it remains unchecked |
|---|---|
| **Kill process on port** | One fixed example finds port 8080 and separate actions kill by PID/name; there is no single user-entered-port flow that resolves and terminates the owning process. |
| **Switch Node version (per-shell)** | No fnm/nvm/Volta detection, version list/install, or per-terminal selection is implemented. |
| **Enable Corepack for pnpm/yarn** | No Corepack enable/prepare/status controls or command bindings exist. |
| **Add Windows Defender dev-folder exclusions** | Only fixed example paths/processes exist in catalog data; there is no folder picker and reviewed add/list/remove flow for the developer-selected project. |
| **Widen ephemeral ports & tune TIME_WAIT** | No controls invoke `netsh ... dynamicport` or manage `TcpTimedWaitDelay`. |
| **Clean dev caches (npm/pnpm/pip/docker)** | npm and Docker cleanup examples exist, but the combined inspected cleanup omits pnpm and pip and does not show reclaimable sizes before mutation. |

## Home Assistant · Home Assistant

### Shipped — 13 · 已交付 — 13

Common evidence: `Pages/HomeAssistantModule.xaml(.cs)` exposes the handlers below; `Services/HomeAssistantService.cs` maps them to the documented REST endpoints. Generated evidence is in `docs/wiki/features/apps-git-git/homeassistant.md` and its 42 button pages.

| Roadmap capability | Concrete handler/service evidence |
|---|---|
| **Render a Jinja template against live state** | `TplRun_Click` → `RenderTemplate` → `POST /api/template`. |
| **Plot 24h entity history sparkline** | `Hist_Click` → `History(id, 24)`; `DrawSpark` renders returned points. |
| **Reload one integration without a full restart** | `ReloadEntry_Click` / `ReloadDomain_Click` call `ReloadConfigEntry` / `ReloadDomain`. |
| **Set a custom in-memory state on any entity** | `SetState_Click` validates input and calls `SetState`. |
| **Snapshot a camera frame to disk** | `Snap_Click` fetches `CameraSnapshot`; `SaveSnap_Click` persists the captured image. |
| **Run a scene or script on demand** | `Scene_Click` / `Script_Click` call `RunScene` / `RunScript` for selected entities. |
| **Fire a custom event into automations** | `Event_Click` validates JSON and calls `FireEvent`. |
| **Browse today's calendar events** | `LoadCals_Click` and `Today_Click` call `Calendars` / `CalendarEvents` for the local-day window. |
| **Tail the HA error log** | `Tail_Click` calls `ErrorLog`; `CopyLog_Click` copies displayed output. |
| **Set light brightness and colour temperature** | `LightOn_Click` calls `SetLight` with the brightness and temperature controls; row brightness actions are also wired. |
| **Set thermostat target temp and HVAC mode** | `SetTemp_Click` / `SetHvac_Click` call the climate service methods for the selected entity. |
| **Push a notification to phones** | `LoadTargets_Click` discovers notify targets; `Notify_Click` calls `Notify`. |
| **Trigger a parameterized voice intent by text** | `Intent_Click` validates slot JSON and calls `HandleIntent` (`POST /api/intent/handle`). |

### Remaining gaps — 1 · 餘下缺口 — 1

| Unchecked roadmap capability | Factual reason it remains unchecked |
|---|---|
| **Validate config before restarting HA** | `CheckCfg_Click` and `Restart_Click` are separate. Restart only shows a confirmation and directly calls `_ha.Restart()`; it does not require or retain a successful `CheckConfig()` result, so the safety gate promised by the roadmap is not present. |

## Archives · Archives

### Shipped — 10 · 已交付 — 10

All catalog entries below are rendered by the Archives surface through `ControlRowList`; `Catalog/ArchiveTweak.cs` binds them to `ArchiveService.Run` / `RunRar`, and generated feature docs live under `docs/features/archives/`. The bespoke Create panel also calls the extended `ArchiveService.Create` overload.

| Roadmap capability | Concrete implementation evidence |
|---|---|
| **Encrypt archive headers (hide file names)** | `arc.create-7z-encrypted` uses `-p -mhe=on`; the Create panel passes `encryptHeader`, and `ArchiveService.Create` appends `-mhe=on` for 7z. |
| **Hash files / folders (CRC32, CRC64, SHA-256, SHA-1, BLAKE2sp, XXH64)** | `arc.hash-all` / `arc.inspect.hash-all` invoke `7z h -scrc*`; SHA-256 and SHA-1 variants are separately exposed. |
| **Benchmark compression / crypto codecs (MIPS rating)** | `arc.benchmark` invokes `7z b`; `arc.benchmark-dict` exposes the dictionary variant. |
| **Update archive (refresh only changed / newer files)** | `arc.mod-update` invokes `7z u {archive} {src}` and `arc.mod-fresh` exposes update-state options. |
| **Split into volumes / re-join (multi-part archive)** | Create panel passes user `volumeSize`; `ArchiveService.Create` appends `-v...`; extracting a `.001` uses normal 7z multi-volume discovery. |
| **Make self-extracting EXE (SFX)** | Create panel passes `sfx`; `ArchiveService.Create` appends `-sfx`; `arc.make-sfx` also exposes the SFX stub command. |
| **Test archive integrity** | `arc.inspect.test` and password variant invoke `7z t`. |
| **List archive contents with technical detail** | `arc.inspect.list-technical` / `arc.list-slt` invoke `7z l -slt`. |
| **Repair corrupted RAR via recovery record** | `arc.rar-repair` routes `unrar r {archive}` through `ArchiveService.RunRar`; `arc.rar-extract-keepbroken` exposes `x -kb`. |
| **Set LZMA2 dictionary & word size for max ratio** | `arc.create-7z-lzma2` exposes LZMA2 and `arc.create-7z-max-combo` combines 7z ultra/solid mode with `-md=64m -mfb=273`; large-dictionary variants are also present. |

### Remaining gaps — 4 · 餘下缺口 — 4

| Unchecked roadmap capability | Factual reason it remains unchecked |
|---|---|
| **Delete files from inside an archive without re-packing** | `arc.mod-delete` is hard-wired to `*.tmp`; there is no entry picker or user-editable name/mask input for arbitrary in-archive deletion. |
| **Delete source files after successful packing (move-to-archive)** | Neither the Create panel nor catalog includes `-sdel`; no post-pack integrity gate/source deletion path exists. |
| **Filter by file mask & exclude junk into archive** | Catalog offers fixed `*.log` exclude and `*.txt` include examples only; no user-configurable include/exclude mask controls exist. |
| **Preserve NTFS timestamps & don't bump Last-Access** | `arc.create-7z-keep-time` supplies only `-mtc=on`; required `-mta=on`, `-mtm=on`, and `-ssp` controls are absent. |

## Browser Control · Browser Control

### Shipped — 14 · 已交付 — 14

`Controls/BrowserControlPanel.xaml(.cs)` is reachable at `--page browser` above the catalog rows. `Services/BrowserControlCore.cs` owns bounded validation, discovery, launch planning, containment, and cleanup contracts; `BrowserControlService.cs` performs argument-vector execution. Existing `Catalog/BrowserTweaks.cs` rows remain available as quick actions.

| Roadmap capability | Concrete implementation evidence |
|---|---|
| **Launch site as desktop app window** | `UrlBox` feeds `BuildAppModePlan`; the bounded HTTP(S) URL leaves the app as one `--app=<url>` `ArgumentList` item. |
| **Open in incognito / InPrivate window** | `br.chrome.incognito` uses `--incognito`; `br.edge.inprivate` uses `--inprivate`. |
| **Launch full-screen kiosk URL** | `KioskBtn` emits Chrome `--kiosk <url>` or Edge `--kiosk <url> --edge-kiosk-type=fullscreen --kiosk-idle-timeout-minutes=0`. |
| **Pick and launch a specific browser profile** | `DiscoverProfiles` reads real directories plus `Local State/profile.info_cache`; `ProfileBox` binds the selected display name/directory to a containment-checked `--profile-directory=` plan. |
| **List and launch installed PWAs** | `DiscoverPwas` reads user/common Start-menu `.lnk` files through `IShellLinkW`, validates/deduplicates runtime app IDs and profiles, and launches the resolved installed browser with those values. |
| **Open the Windows default-apps picker for a browser** | `br.edge.set-default`, `br.profiles.set-default-browser`, and `br.profiles.open-default-apps` launch `ms-settings:defaultapps`. |
| **Open internal flags & policy pages** | `FlagsBtn` and `PolicyBtn` reach `chrome://flags`, `chrome://policy`, `edge://flags`, and `edge://policy` through discrete arguments. |
| **Clear browsing cache for a profile** | `ClearCacheBtn` requires a decision; `ClearProfileCaches` rejects running browser processes, validates the selected profile path, rejects reparse points, and deletes only `Cache` and `Code Cache`. |
| **Set per-launch proxy server** | Bounded proxy/bypass inputs feed independent switches in a GUID-isolated session; whitespace/control/quote injection is rejected. |
| **Launch isolated throwaway browser sandbox** | `CreateEphemeralDirectory` creates a new GUID path below `%TEMP%\WinForge\BrowserSessions`; the owned process exit handler retries contained deletion and later launches retry stale owned sessions. |
| **Force-enable a hidden browser feature flag** | `FeatureBox` plus enable/disable selection validates up to 16 names and emits one `--enable-features=` or `--disable-features=` argument in an isolated profile. |
| **Apply enterprise browser policy** | `br.policies.*` rows bind real ADMX-backed HKLM values under `SOFTWARE\Policies\Google\Chrome` and `SOFTWARE\Policies\Microsoft\Edge`. |
| **Open URL with remote debugging port** | `DebugPortBox` bounds 1024–65535; the plan binds `127.0.0.1`, supplies the port, and always uses a fresh isolated user-data directory. |
| **Install/update a browser via winget** | Review-first buttons call `ShellRunner.RunArguments` with exact `Google.Chrome` / `Microsoft.Edge` package IDs, install/upgrade verbs, agreement flags, silent mode, and disabled interactivity. |

### Remaining gaps — 0 · 餘下缺口 — 0

No Browser Control entries remain open. · 瀏覽器控制冇剩低未完成項目。

## Verification disposition · 驗證處置

- Focused contract: `powershell -ExecutionPolicy Bypass -File tools/Test-RoadmapCoreAudit.ps1`.
- Source/route checks: `.agents/skills/winforge-exhaustive-smoke/scripts/Test-WinForgeSourceSurfaceAudit.ps1`, XAML literal safety, focused roadmap consistency, and docs-only site generation are run as part of this audit handoff.
- The historical adversarial review remains valid for Media and Archives; Browser Control subsequently advanced from 3/14 to 14/14 through the parameterized workbench and focused harness.
- `tests/BrowserControl.Tests` covers 23 URL, profile, PWA, internal-page, cache, proxy, ephemeral-lifecycle, feature, debug-port, and winget plan contracts. Fresh Browser route visual evidence is required because `CategoryPage` and its layout changed.
- Media's former 4/11 disposition is now 15/0. The four pre-existing items remain source-accurate, including animated WebP's `libwebp` command without an explicit quality value; all eleven former gaps have dedicated page handlers, executor evidence, and focused tests.
- The Media WinUI surface changed. A fresh process-owned live-tree capture was inspected and promoted to `docs/screenshot-media.png` and `docs/wiki/images/screenshot-media.png` (SHA-256 `F89886CE200DA522E8C956B67B363A847E8E9DC0AC2926DFF382E9E52B870900`); LowLevel MCP was present only as repository guidance and was not callable in this session.
- The Windows/System + Maintenance follow-up changes the live System Doctors page. Fresh self-contained LowLevel headless captures cover the normal, narrow, expanded Storage Sense, and expanded ResetBase-warning states; destructive system operations remained unexecuted during visual verification.
- The remaining 11 unchecked entries are intentional product gaps in other sections, not audit failures. Future work should check an entry only after its specific reason is resolved and the focused contract is updated deliberately.

今次瀏覽器控制由 3/14 推進到 14/14，Media 由 4/15 推進到 15/15，Windows／System 同 Maintenance 亦全部補齊；相關 WinUI 都有最新已檢視畫面。系統醫生擷取冇執行破壞性系統操作，其餘 11 項未剔選係其他章節刻意保留嘅真實產品缺口，唔係審核漏咗。
