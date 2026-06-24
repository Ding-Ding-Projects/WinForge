# Changelog · 變更紀錄

_Cleaned-up project history. Contributor / author names intentionally omitted._

## Latest — dialogs fix, crash-proofing, Cloudflare, AI agents, settings & more

- **File/folder dialogs now open** — replaced every WinRT picker (which fails silently when the app
  runs as administrator) with a Win32 COM `IFileDialog` helper (`Services/FileDialogs.cs`) that works
  elevated or not. Migrated ~30 modules.
- **Crash-proofing** — global unhandled-exception handlers log to `crash.log` and keep the app alive
  (`Services/CrashLogger.cs`); **OCR no longer crashes** (the full-screen capture is now downscaled to
  the OCR engine's max dimension before recognition).
- **Cloudflare & Tunnel module** — cloudflared named/quick tunnels, route DNS, Cloudflare Access,
  DNS-over-HTTPS and WARP, all in-app.
- **AI Agents module** — install / configure / launch **Claude Code, OpenAI Codex, opencode, Pi,
  OpenClaw and Hermes Agent** (npm / winget / official installers, API-key setup, one-click launch).
- **Run on startup** — Clipboard (and the suite) can launch at login, minimized to the tray.
- **Media GUI remade** — working file dialogs, live ffprobe info, trim / GIF / frame-grab panels,
  more quick conversions.
- **UniGetUI parity** — added **vcpkg**, package **Details**, advanced **install options** (version /
  scope / architecture / interactive / custom args), **Sources** view, and **ignored-updates**.
- **Settings & Control Panel revamped** — change settings **in-app** (the live, current-state-reading
  tweak catalog, grouped into categories) and **Open in Windows** (every applet, grouped & searchable).

## Latest — Git/GitHub workbench + UniGetUI multi-manager

A full revamp of the Git & GitHub module and the Package Manager, generated and adversarially
verified through an ultracode multi-agent workflow:

- **Multi-repository list** — add a folder, scan a parent tree for repos, or clone a URL; every
  repo is saved to a switchable list (`Services/RepoStore.cs`). Selecting one drives every action.
- **Complete git CLI** — the full porcelain + plumbing surface as data-driven operations
  (`Catalog/GitCliOperations.cs`), on top of the existing common ops.
- **Complete GitHub** — everything GitHub exposes through `gh` and `gh api {owner}/{repo}`:
  repos, issues, PRs, Actions, releases, gists, secrets, labels, projects, codespaces, stars,
  notifications, webhooks, collaborators, branch protection, traffic, Dependabot/code-scanning
  (`Catalog/GitHubOperations.cs`). Aggregated + de-duplicated in `Catalog/GitCatalog.cs`.
- **In-GUI workbench** — repo list, stage/commit/branch switch & create, chunked uploader,
  a free-form `git`/`gh` command runner with a console, and a filterable operation library
  (All / Git / GitHub).
- **UniGetUI clone** — one front-end over **8 package managers** (winget, Scoop, Chocolatey,
  pip, npm, .NET tools, PowerShell Gallery, Cargo) via `Services/PackageManagers.cs`:
  Discover / Updates / Installed / Bundles (export-import) / Setup, with per-manager filtering,
  batch update, and one-click engine bootstrap.

## Latest — 22-module roadmap fan-out

One isolated build per roadmap category, merged together:

- ViVeTool feature-flag manager
- Home Assistant REST control
- Communications deep-link launcher
- Config & Backup (settings snapshots)
- System Doctors
- Native System32 utilities
- PowerToys extras (Image Resizer / OCR / Always-On-Top)
- Capture Studio (region record / snip / OCR)
- Battery & Thermal dashboard
- WSL & VM Launcher
- Clipboard QR generator
- Font Manager
- Hotkey & Macro Runner
- OneDrive Files-On-Demand
- Time & Unit Tools
- Voice & Read-Aloud (TTS)
- Settings & Control Panel hub
- VPN follow-ups (exit-node / Meshnet / WireGuard / SSMS)
- Android tools (scrcpy / fastboot / emulator)
- Imaging & game tools (Pi Imager / Minecraft)
- Windows 11 tweak gaps
- Encryption / Archive gap ops

## Commit history

- `2026-06-23` — Merge: 22 new feature modules — roadmap fan-out
- `2026-06-23` — docs: check off Archives advanced Create options in ROADMAP
- `2026-06-23` — feat: Archives — split volumes, SFX, header encryption, solid & multi-thread options
- `2026-06-23` — merge: vpn-followups module
- `2026-06-23` — merge: voice-tts module
- `2026-06-23` — merge: clipboard-qr module
- `2026-06-23` — merge: encryption-archive-gaps module
- `2026-06-23` — feat: auto-install everywhere — one-click engine install for Media/Recorder/Archives + more deps
- `2026-06-23` — feat: Smart App Uninstaller — real icons + on-disk size + deep uninstall
- `2026-06-23` — feat(ux): Network Pro — all 16 tabular tweaks render as native grids (Tweak.Table sweep)
- `2026-06-23` — feat(ux): tabular command output → native column grid (Tweak.Table + CSV renderer)
- `2026-06-23` — feat(ux): Devices — labeled "Actions" dropdown (Enable/Disable + confirm) + empty state
- `2026-06-23` — docs: add per-feature HANDOFF.md (every module: deep-link, files, engine, status) docs: 新增逐項功能 HANDOFF.md（每個模組：深層連結、檔案、引擎、狀態）
- `2026-06-23` — feat(ux): Services — unified "Actions" dropdown (incl. startup type) + empty state
- `2026-06-23` — feat(ux): Scheduled Tasks — labeled "Actions" dropdown + empty state
- `2026-06-23` — feat(ux): Env Vars — per-entry PATH editor (reorder / add / remove / browse)
- `2026-06-23` — feat(ux): TweakCard action output → full monospace scrollable pane + Copy/Save
- `2026-06-23` — feat: clipboard history as a local git repo + opencode AI commit messages
- `2026-06-23` — fix: Volume Mixer slider now auto-unmutes on drag (like Windows) fix: 音量混合器拉 slider 會自動取消靜音（同 Windows 一樣）
- `2026-06-23` — feat(ux): windowed mode + F11, touchless auto-install of missing engines
- `2026-06-23` — feat: VPN & Mesh — NordVPN + Tailscale CLI GUIs
- `2026-06-23` — test: full 34-page smoke test (all PASS) + montage; wire --page about/settings test: 全 34 頁煙霧測試（全部通過）+ 拼圖；接上 --page about/settings
- `2026-06-23` — feat: Android ADB console (wraps adb)
- `2026-06-23` — feat: Package Manager (UniGetUI-style, wraps winget) + auto-install deps
- `2026-06-23` — docs+ux: organize feature docs into per-module subfolders, CLI reference, README refresh, roadmap
- `2026-06-23` — feat: background Clipboard manager + system tray (keep running when closed)
- `2026-06-23` — feat(ux): collapsible grouped nav + master search with live, working setting toggles
- `2026-06-23` — feat: Winaero Tweaker functions — 45 verified advanced tweaks
- `2026-06-23` — feat: PowerToys in WinForge — batch 1: Awake, Color Picker, Environment Variables
- `2026-06-23` — feat: in-app Context Menu Editor (add/remove right-click verbs)
- `2026-06-23` — feat: in-app per-app Volume Mixer (Core Audio / WASAPI)
- `2026-06-23` — feat: in-app Event Viewer (replaces eventvwr.msc redirect)
- `2026-06-23` — feat: in-app Connections viewer (TCPView-style, iphlpapi)
- `2026-06-23` — fix(ci): correct Inno OutputDir + robust release asset lookup fix(ci): 修正 Inno OutputDir 同更穩陣咁搵發佈檔案
- `2026-06-23` — feat: bilingual audit fixes + EcoQoS/affinity + CI release pipeline
- `2026-06-23` — feat: System Monitor — per-process CPU% + set priority (Process Lasso-style)
- `2026-06-23` — feat: in-app System Monitor (live CPU/RAM/network + end task)
- `2026-06-23` — feat: in-app Screen Recorder (ffmpeg gdigrab, whole desktop)
- `2026-06-23` — feat: in-app Mouse & Pointer settings (live SystemParametersInfo)
- `2026-06-23` — feat: in-app Hosts Editor (replaces the Notepad redirect)
- `2026-06-23` — feat: in-app Keyboard Remapper (SharpKeys-style Scancode Map)
- `2026-06-23` — feat: in-app Window Manager (FancyZones-style snap, pure Win32)
- `2026-06-23` — feat: in-app App Uninstaller for Store/UWP apps
- `2026-06-23` — feat: one-click "Calm Windows" + debloat Recipes
- `2026-06-23` — feat: in-app Drives module — overview + mount/create disk images
- `2026-06-23` — feat: in-app Disk Analyser (folder sizes + largest files, pure C#)
- `2026-06-23` — feat: in-app Duplicate File Finder (SHA-256, pure C#)
- `2026-06-23` — feat: in-app Bulk File Operations (pure C#)
- `2026-06-23` — feat: Debloat & Annoyances category — 32 forum pain-point toggles
- `2026-06-23` — feat: in-app Media module (ffmpeg, 60 ops)
- `2026-06-23` — feat: in-app Batch Rename (PowerRename-style, pure C#)
- `2026-06-23` — feat: in-app Startup-apps Manager (no Task Manager/Settings redirect)
- `2026-06-23` — feat: in-app Devices Manager (no devmgmt.msc redirect)
- `2026-06-23` — feat: in-app Scheduled-Tasks Manager (no taskschd.msc redirect)
- `2026-06-23` — feat: in-app Services Manager (no services.msc redirect)
- `2026-06-23` — feat: in-app Registry Editor (no regedit.exe redirect) feat: 應用程式內登錄編輯器（唔使叫出 regedit.exe）
- `2026-06-23` — feat: one-click Recipes, settings import/export, and per-feature Markdown docs feat: 一鍵流程、設定匯入匯出，同逐項功能 Markdown 文件
- `2026-06-23` — feat: add 500 features across 5 new modules + Tools nav group feat: 一次過加 500 項功能、5 個新模組 + 「工具」導覽分組
- `2026-06-23` — feat: add Archives module (7-Zip, 100 ops) + grow roadmap
- `2026-06-23` — feat: add Maintenance & Diagnostics module (102 ops) + grow roadmap
- `2026-06-23` — docs: add living bilingual ROADMAP — 175 discovered features, 22 modules docs: 加入會生長嘅雙語路線圖 — 發掘到 175 項功能、22 個模組
- `2026-06-23` — feat: evolve WinForge into a kiosk suite + Git/GitHub module (iteration 0) feat: 將 WinForge 進化成 kiosk 套件 + Git/GitHub 模組（第 0 次迭代）
- `2026-06-23` — feat: WinForge — bilingual Windows 11 control center (164 features) feat: WinForge — 雙語 Windows 11 控制中心（164 項功能）
