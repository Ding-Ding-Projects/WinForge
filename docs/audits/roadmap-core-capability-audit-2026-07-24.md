# Core roadmap capability audit — 2026-07-24 · 核心路線圖功能審核

## Outcome · 結果

This audit reconciles eight stale sections in `docs/ROADMAP.md` against the .NET WinUI 3 application. The original classification was revalidated against source, then the 11 Media gaps were implemented through reachable controls and a focused workflow harness. Of 115 roadmap entries, **85 now have complete source-backed delivery evidence** and **30 remain unchecked** because implementation is absent or partial in other sections.

今次審核將 `docs/ROADMAP.md` 八個過時章節同 .NET WinUI 3 app 原始碼逐項核對，再將 Media 原本 11 個缺口實作成可達控制同專項測試。115 項之中，**85 項有完整原始碼交付證據**，**30 項因其他章節未有實作或者只做咗一部分而繼續留空**。

| Section · 章節 | Audited · 審核 | Shipped `[x]` · 已交付 | Remaining `[ ]` · 餘下 |
|---|---:|---:|---:|
| Windows 11 | 13 | 10 | 3 |
| ViveTool | 15 | 15 | 0 |
| Media | 15 | 15 | 0 |
| Maintenance | 15 | 10 | 5 |
| Dev & Terminal | 15 | 9 | 6 |
| Home Assistant | 14 | 13 | 1 |
| Archives | 14 | 10 | 4 |
| Browser Control | 14 | 3 | 11 |
| **Total · 總數** | **115** | **85** | **30** |

## Evidence standard · 證據標準

An entry is checked only when all of the following are present:

1. A reachable user-facing control: a dedicated page/button/handler, or a catalog `TweakDefinition` rendered by `CategoryPage` / `SettingsHubModule` through `ControlRowList.SetTweaks(...)`.
2. A concrete mechanism: the handler calls a service, API, registry binding, or command runner that performs the advertised capability. A label, placeholder, or neighbouring feature is not enough.
3. Documentation or verification evidence: generated feature documentation under `docs/features/`, generated module/button pages under `docs/wiki/`, or a focused static/source test.

只有同時有可達控制、實際執行機制，同埋文件／驗證證據先會剔選。`Controls/ControlRowList.cs` is the shared binding proof for catalog rows: action buttons invoke `RunAsync`, toggles invoke `SetIsOn`, and choice/slider controls invoke their registered setters. Dedicated modules are registered in `Services/ModuleRegistry.cs`, mapped/deep-linked in `MainWindow.xaml.cs`, and documented by generated module/button pages.

The focused guard is `tools/Test-RoadmapCoreAudit.ps1`. It asserts section totals and checked counts, confirms that every one of the 115 exact roadmap titles appears in this audit, and verifies the aggregate **85/115** result. `tests/MediaWorkflowCore.Tests` separately covers two-pass sequencing, parsers, argument boundaries, cancellation, staged-output preservation, and owned scratch cleanup.

## Windows 11 · Windows 11

### Shipped — 10 · 已交付 — 10

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

### Remaining gaps — 3 · 餘下缺口 — 3

| Unchecked roadmap capability | Factual reason it remains unchecked |
|---|---|
| **Configure Storage Sense Cadence & Recycle Bin Purge** | `w11p.storagenotif.storage-sense` and `recyclebin-retention` implement values `01` and `256`, but there is no control for cadence value `2048` or Downloads retention value `512`. |
| **Enable Filter Keys / Slow Keys for Accessibility** | `w11p.inputintl.filter-keys` only changes `Flags`; no controls exist for `DelayBeforeAcceptance`, `AutoRepeatDelay`, `AutoRepeatRate`, or `BounceTime`, and no `SPI_SETFILTERKEYS` live apply path is present. |
| **Export / Import Default App Associations (machine-wide)** | No page/handler runs DISM `/Export-DefaultAppAssociations` or `/Import-DefaultAppAssociations`, and no protected per-user/manual-association workflow is implemented. |

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

### Shipped — 10 · 已交付 — 10

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

### Remaining gaps — 5 · 餘下缺口 — 5

| Unchecked roadmap capability | Factual reason it remains unchecked |
|---|---|
| **Pause / resume Windows Update** | Update controls scan/download/install/reset services, but no handler writes/removes the pause expiry/start/end registry values or exposes a resume operation. |
| **Driver list / export / rollback hints** | Listing and a text export of `pnputil /enum-drivers` exist, but there is no driver-package export (`pnputil /export-driver`), rollback control, or guided rollback workflow. |
| **Startup impact / autoruns audit** | Startup Apps lists and toggles Run/StartupApproved entries, but it does not calculate startup impact or audit the broader Autoruns locations. |
| **Component store cleanup (WinSxS / ResetBase)** | `Dism /StartComponentCleanup` exists; the roadmap explicitly includes `/ResetBase`, which is absent and irreversible-state guidance is not implemented. |
| **Reset / re-register a stuck Store app** | `SystemDoctors` re-registers two fixed shell packages only; there is no user-selected Store-app reset/re-register workflow. |

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

### Shipped — 3 · 已交付 — 3

`Catalog/BrowserTweaks.cs` is registered in the Browser Control category and rendered by `ControlRowList`; generated feature docs live under `docs/features/browser-control/`.

| Roadmap capability | Concrete implementation evidence |
|---|---|
| **Open in incognito / InPrivate window** | `br.chrome.incognito` uses `--incognito`; `br.edge.inprivate` uses `--inprivate`. |
| **Open the Windows default-apps picker for a browser** | `br.edge.set-default`, `br.profiles.set-default-browser`, and `br.profiles.open-default-apps` launch `ms-settings:defaultapps`. |
| **Apply enterprise browser policy** | `br.policies.*` rows bind real ADMX-backed HKLM values under `SOFTWARE\Policies\Google\Chrome` and `SOFTWARE\Policies\Microsoft\Edge`. |

### Remaining gaps — 11 · 餘下缺口 — 11

| Unchecked roadmap capability | Factual reason it remains unchecked |
|---|---|
| **Launch site as desktop app window** | Chrome and Edge app-mode rows hard-code Google/example.com. There is no URL input, so the user cannot launch a chosen site as requested. |
| **Launch full-screen kiosk URL** | Kiosk rows hard-code Google/example.com and expose no URL input. |
| **Pick and launch a specific browser profile** | Profile directories can be listed and the fixed `Default` profile can be launched, but there is no `Local State` display-name mapping or selected-profile binding into `--profile-directory`. |
| **List and launch installed PWAs** | A catalog action lists Start-menu PWA shortcuts, but no action parses and launches a selected `--app-id`/profile target. |
| **Open internal flags & policy pages** | Flags pages exist, but `chrome://policy` and `edge://policy` controls are absent; the combined roadmap capability is incomplete. |
| **Clear browsing cache for a profile** | Clear actions are fixed to the Default profile's `Cache` directory and omit `Code Cache`; no profile picker or safe browser-closed validation exists. |
| **Set per-launch proxy server** | The only proxy row hard-codes `127.0.0.1:8080`; no proxy/bypass input is exposed. |
| **Launch isolated throwaway browser sandbox** | Safe mode reuses `%TEMP%\chrome-safe`; it does not create a GUID-scoped directory or delete it after use, so it is not throwaway. |
| **Force-enable a hidden browser feature flag** | No action accepts a feature name or invokes `--enable-features` / `--disable-features`. |
| **Open URL with remote debugging port** | No action invokes `--remote-debugging-port` with an isolated user-data directory. |
| **Install/update a browser via winget** | No Browser Control action exposes verified browser package install/upgrade commands. |

## Verification disposition · 驗證處置

- Focused contract: `powershell -ExecutionPolicy Bypass -File tools/Test-RoadmapCoreAudit.ps1`.
- Source/route checks: `.agents/skills/winforge-exhaustive-smoke/scripts/Test-WinForgeSourceSurfaceAudit.ps1`, XAML literal safety, focused roadmap consistency, and docs-only site generation are run as part of this audit handoff.
- Media's former 4/11 disposition is now 15/0. The four pre-existing items remain source-accurate, including animated WebP's `libwebp` command without an explicit quality value; all eleven former gaps have dedicated page handlers, executor evidence, and focused tests.
- The Media WinUI surface changed. A fresh process-owned live-tree capture was inspected and promoted to `docs/screenshot-media.png` and `docs/wiki/images/screenshot-media.png` (SHA-256 `F89886CE200DA522E8C956B67B363A847E8E9DC0AC2926DFF382E9E52B870900`); LowLevel MCP was present only as repository guidance and was not callable in this session.
- The remaining 30 unchecked entries are intentional product gaps in other sections, not audit failures. Future work should check an entry only after its specific reason is resolved and the focused contract is updated deliberately.

今次 Media WinUI 有改，已用 repo driver 擷取同檢查最新畫面；另外 30 項未剔選係其他章節刻意保留嘅真實產品缺口，唔係審核漏咗。
