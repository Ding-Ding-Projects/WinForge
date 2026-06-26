<div align="center">

# WinForge · 視窗鑄造

**An all-in-one, fully bilingual Windows 11 control center — every module is real and working — crowned by a hyper-realistic flagship nuclear-reactor simulator.**
**一個全方位、全程雙語嘅 Windows 11 控制中心 — 每個模組都係真正用得 — 仲有一個超寫實嘅旗艦核反應堆模擬器坐鎮。**

`WinUI 3 · .NET 11` · `English + 繁體中文／粵語` · `x64` · `138 in-app modules` · `everything runs in-app`

</div>

---

## 🌏 Overview · 概覽

**EN —** WinForge is an all-in-one, fully bilingual control center for Windows 11. It gathers 138 real, working modules — system tweaking, files & disks, media & capture, developer tooling, networking, package management, AI, window management, PowerToys-style utilities, security vaults, virtualization and gaming — into a single **WinUI 3 / .NET 11** app where every English label is paired with **繁體中文／粵語** and every action actually changes the system. Its flagship is a **hyper-realistic Pressurized Water Reactor (PWR) control-room simulator** with full point-kinetics physics, a fuel-and-waste fuel cycle, a water-treatment plant, and Westinghouse-style safety systems.

**粵語 —** WinForge 係一個畀 Windows 11 用嘅全方位、全程雙語控制中心。佢將 138 個真正用得嘅模組 — 系統調校、檔案與磁碟、媒體與擷取、開發者工具、網絡、套件管理、AI、視窗管理、PowerToys 式工具、安全保險庫、虛擬化同遊戲 — 全部集合喺一個 **WinUI 3 / .NET 11** app 入面，每個英文標籤都配上**繁體中文／粵語**，而且每個動作都真正改到部機。佢嘅旗艦係一個**超寫實壓水堆（PWR）控制室模擬器**，附完整點動力學物理、燃料與廢料燃料循環、水處理廠同西屋式保護系統。

---

## 📑 Table of contents · 目錄

- [Build & Run · 建置與執行](#-build--run--建置與執行)
- [Highlights · 重點](#-highlights--重點)
- [Flagship: Nuclear Reactor · 旗艦：核反應堆](#-flagship-nuclear-reactor--旗艦核反應堆)
- [Module Catalog · 模組目錄](#-module-catalog--模組目錄)
- [Documentation & Wiki · 文件與 Wiki](#-documentation--wiki--文件與-wiki)
- [License · 授權條款](#-license--授權條款)

---

## 🔨 Build & Run · 建置與執行

**EN —** Requirements: the **.NET 11 SDK** and the **Windows App SDK** workload (Visual Studio 2022 with *.NET Desktop* + *Windows App SDK*, or the SDK on its own). Build the whole solution:

**粵語 —** 需求：**.NET 11 SDK** 同 **Windows App SDK** 工作負載（Visual Studio 2022 加 *.NET 桌面* + *Windows App SDK*，或者淨係裝 SDK）。建置成個方案：

```powershell
# Build the solution (Debug, x64) · 建置方案（Debug、x64）
dotnet build WinForge.sln -c Debug -p:Platform=x64
```

**EN —** To **run** a real, distributable build, publish self-contained — the Windows App SDK runtime is bundled, so no separate runtime install is needed:

**粵語 —** 想**執行**一個可發佈嘅版本，請用自包含方式 publish — 已內附 Windows App SDK 執行階段，唔使另外安裝：

```powershell
# Self-contained publish (x64) for running · 自包含 publish（x64）以供執行
dotnet publish WinForge.csproj -c Release -p:Platform=x64 -r win-x64 ^
  --self-contained true -p:WindowsAppSDKSelfContained=true -p:WindowsPackageType=None

# Then run the published WinForge.exe · 然後執行 publish 出嚟嘅 WinForge.exe
.\WinForge.exe
```

> **Open a single module directly · 直接開單一模組:** `WinForge.exe --page <alias>` (every alias is listed in the [Module Catalog](#-module-catalog--模組目錄) below). · 每個別名都喺下面嘅[模組目錄](#-module-catalog--模組目錄)。

---

## ✨ Highlights · 重點

**EN —**
- **All-in-one control center** — 138 modules in one app; OSS-inspired additions are remade as native WinForge tabs instead of installer-only launchers. · **全方位控制中心** — 一個 app 有 138 個模組；受開源 app 啟發嘅新增功能會重製成 WinForge 原生分頁，而唔係只做安裝／啟動器。
- **Fully bilingual** — English and 繁體中文／粵語 appear together on every surface; choose which language leads, UI updates live. · **全程雙語** — 每個介面同時顯示英文同繁體中文／粵語；可揀邊種語言排前，介面即時更新。
- **Accessible app shell** — main navigation, search, tabs and new native OSS pages expose screen-reader names, heading levels, visible focus paths and keyboard accelerators. · **無障礙 app 外殼** — 主要導航、搜尋、分頁同新原生開源頁面提供螢幕閱讀器名稱、標題層級、清楚焦點路徑同鍵盤捷徑。
- **Real engines, real effects** — wraps git/gh, ffmpeg, 7-Zip, yt-dlp, cloudflared, winget, libVLC, Docker and more, plus native Windows APIs — no fake toggles. · **真實引擎、真實效果** — 包住 git/gh、ffmpeg、7-Zip、yt-dlp、cloudflared、winget、libVLC、Docker 等同原生 Windows API — 冇假開關。
- **Master search** — find and launch any module from the Dashboard. · **總搜尋** — 喺概覽頁搵到同啟動任何模組。
- **Hyper-realistic flagship** — a full PWR nuclear-reactor control-room simulator (see below). · **超寫實旗艦** — 一個完整嘅 PWR 核反應堆控制室模擬器（見下）。
- **WinUI 3 · .NET 11 · x64** — modern, self-contained, runs windowed or full-screen and hides to the tray. · **WinUI 3 · .NET 11 · x64** — 現代化、自包含，可視窗或全螢幕，並收埋去系統匣。

---

## ★ Flagship: Nuclear Reactor · 旗艦：核反應堆

**EN —** The headline module is a **hyper-realistic Pressurized Water Reactor (PWR) control-room simulator**, rendered in WinUI 3 with a pop-out HTML5 window. It is a simulation / training toy — it controls no real hardware. It models 6-group point kinetics, reactivity feedback (Doppler / moderator / boron / xenon), thermal-hydraulics, a steam/turbine secondary plant, a Westinghouse-style protection system (2-out-of-4 coincidence trips), synthesized control-room audio and a live plant mimic.

**粵語 —** 重點模組係一個**超寫實壓水堆（PWR）控制室模擬器**，用 WinUI 3 繪製，附可彈出嘅 HTML5 視窗。佢只係模擬／訓練玩具，唔會控制任何真實硬件。佢模擬六組點動力學、反應性回饋（都卜勒／緩和劑／硼／氙）、熱工水力、蒸汽渦輪二次側、西屋式保護系統（2-of-4 一致跳脫）、合成控制室音效同即時機組流程圖。

| Feature · 功能 | What it does · 內容 |
|---|---|
| **Rooms & HTML5 window** · 房間與 HTML5 視窗 | Walk through plant rooms; pop the control room out into its own dedicated HTML5 window for a full-screen view. <br> 行勻機組各個房間；將控制室彈出去自己嘅 HTML5 視窗睇全螢幕。 |
| **Fuel factory + waste cycle** · 燃料工廠 + 廢料循環 | A full fuel cycle — fabricate fuel in the fuel factory, burn it in the core, then route spent fuel through the waste cycle. <br> 完整燃料循環 — 喺燃料工廠製造燃料、喺爐心燃燒，再將乏燃料送入廢料循環。 |
| **Water-treatment plant** · 水處理廠 | A water-treatment plant feeds and conditions the plant's water systems. <br> 水處理廠為機組水系統供水同處理水質。 |
| **Status API** · 狀態 API | A live status API exposes the running plant state for external readers and widgets. <br> 即時狀態 API 對外公開機組運行狀態，供外部讀取同小工具使用。 |
| **Safety** · 安全 | **Meltdown → real PC shutdown is OFF by default** — a meltdown only shows a simulated overlay unless you explicitly arm "ARM REAL SHUTDOWN" (an abortable 10 s Windows shutdown). <br> **熔毀真實關機預設 OFF** — 熔毀只播模擬畫面，除非你明確開啟「ARM REAL SHUTDOWN」（可中止嘅 10 秒 Windows 關機）。 |

> Open it directly · 直接開啟: `WinForge.exe --page reactor`. Full procedures (startup-to-shutdown, scenarios, protection system) are in the [Nuclear Reactor Operating Manual](docs/wiki/Nuclear-Reactor-Operating-Manual.md). · 完整程序見[核反應堆操作手冊](docs/wiki/Nuclear-Reactor-Operating-Manual.md)。

---

## 🧩 Module Catalog · 模組目錄

**EN —** Every module is reachable from the **master search** on the Dashboard, or directly with `WinForge.exe --page <alias>`. Grouped by category below, each with a one-line bilingual description.

**粵語 —** 每個模組都可以喺概覽頁嘅**總搜尋**搵到，或者用 `WinForge.exe --page <alias>` 直接開啟。下面按分類分組，每個都有一句雙語說明。

### System & Tweaks · 系統與調校

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Dashboard · 概覽** | Master search and home overview across every module. <br> 跨晒所有模組嘅總搜尋同主頁概覽。 | `dashboard` |
| **Registry Editor · 登錄編輯器** | Browse and edit the live Windows registry (hives, keys, values). <br> 瀏覽同編輯實時 Windows 登錄檔（hive、機碼、值）。 | `regedit` |
| **System Doctors · 系統醫生** | One-click repairs for spooler, DNS, taskbar, search, icons and more. <br> 一鍵修復列印、DNS、工作列、搜尋、圖示等問題。 | `doctors` |
| **Services · 服務** | Start, stop and set the startup type of Windows services. <br> 啟動、停止同設定 Windows 服務嘅啟動類型。 | `services` |
| **Scheduled Tasks · 排程工作** | View and run entries in the Windows Task Scheduler. <br> 檢視同執行 Windows 排程工作。 | `tasks` |
| **Devices · 裝置** | Enable, disable and inspect hardware devices and drivers. <br> 啟用、停用同檢視硬件裝置同驅動程式。 | `devices` |
| **ViVeTool · 功能旗標** | Toggle hidden Windows feature flags via ViVeTool. <br> 用 ViVeTool 切換隱藏嘅 Windows 功能旗標。 | `vivetool` |
| **Startup Apps · 開機程式** | Manage logon and startup items that run at boot. <br> 管理開機同登入時自動執行嘅項目。 | `startup` |
| **Environment Variables · 環境變數** | Edit user/system environment variables with a dedicated PATH editor. <br> 編輯使用者／系統環境變數，附專用 PATH 編輯器。 | `envvars` |
| **Event Viewer · 事件檢視器** | Read Windows system and application event logs. <br> 讀取 Windows 系統同應用程式事件記錄。 | `events` |
| **System Info (Winfetch) · 系統資訊** | neofetch-style read-out of OS, CPU, GPU, memory and specs. <br> neofetch 風格顯示作業系統、CPU、顯示卡、記憶體等規格。 | `winfetch` |
| **System Monitor · 系統監察** | Live CPU, RAM and network monitor with priority and affinity control. <br> 即時監察 CPU、記憶體同網絡，可調優先權同核心親和性。 | `monitor` |
| **Process Explorer · 程序總管** | Process-tree task manager with command line, threads and kill controls. <br> 程序樹工作管理員，顯示命令列、執行緒，可結束程序。 | `procexp` |
| **Battery & Thermal · 電池與散熱** | Battery wear/health, temperatures, fan and powercfg energy report. <br> 電池耗損健康、溫度、風扇同 powercfg 能源報告。 | `battery` |
| **Volume Mixer · 音量混合器** | Per-app volume and mute control via WASAPI. <br> 用 WASAPI 逐個應用程式調音量同靜音。 | `mixer` |
| **Context Menu · 右鍵選單** | Add and remove shell right-click verbs. <br> 新增同移除右鍵選單動詞。 | `contextmenu` |
| **Explorer Right-Click · 檔案總管右鍵選單** | Toggle native and PowerToys Explorer right-click integrations. <br> 切換原生同 PowerToys 嘅檔案總管右鍵整合。 | `shellmenu` |
| **Nilesoft Shell · Nilesoft 右鍵選單** | Modern, themeable, customizable context menu via Nilesoft Shell. <br> 用 Nilesoft Shell 整現代、可換主題嘅自訂右鍵選單。 | `nilesoftshell` |
| **Awake · 保持喚醒** | Keep the PC awake without changing power settings. <br> 唔改電源設定都令電腦保持唔瞓。 | `awake` |
| **Settings & Control Panel · 設定與控制台** | In-app launcher for ms-settings pages and Control Panel applets. <br> app 內啟動 ms-settings 頁面同控制台小程式。 | `settingshub` |
| **Native Utilities · 原生工具** | Wi-Fi passwords, SMB shares, brightness, certificates, Bluetooth and more. <br> Wi-Fi 密碼、SMB 共享、亮度、憑證、藍牙等原生雜錦。 | `native` |
| **PowerToys Extras · PowerToys 額外工具** | Image Resizer, OCR text extractor and always-on-top helpers. <br> 圖片縮放、OCR 文字擷取同視窗置頂工具。 | `powertoys` |
| **World Monitor · 世界監察** | News, geopolitics, finance and instability-index intelligence dashboard. <br> 新聞、地緣政治、金融同不穩定指數嘅情報儀表板。 | `worldmonitor` |
| **Activity Timeline · 活動時間軸** | Default-on foreground-window time tracking with crash-safe local recovery and per-app usage insights. <br> 預設開啟前景視窗時間追蹤，有本機防閃退復原同逐個應用程式使用量。 | `timelens` |

### Files & Disks · 檔案與磁碟

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Archives · 壓縮檔** | Compress, extract and test ZIP/7z/RAR/TAR archives. <br> 壓縮、解壓同測試 ZIP／7z／RAR／TAR 壓縮檔。 | `archives` |
| **Batch Rename · 批次改名** | Bulk rename files with regex and sequence patterns. <br> 用正規表示式同序號樣式批次改名。 | `rename` |
| **Bulk File Ops · 批次檔案操作** | Mass move, copy, delete and set attributes on files. <br> 批量移動、複製、刪除同設定檔案屬性。 | `bulkops` |
| **New+ · 範本新增** | Create files and folders from templates in the New menu. <br> 由範本喺「新增」選單建立檔案同資料夾。 | `newplus` |
| **Duplicate Finder · 重複檔案搜尋** | Find and dedupe files by size and hash. <br> 按大小同雜湊搵出同清除重複檔案。 | `duplicates` |
| **Instant File Search · 即時檔案搜尋** | Instant filename search over the NTFS master file table (Everything). <br> 用 NTFS 主檔案表做即時檔名搜尋（Everything）。 | `everything` |
| **File Locksmith · 檔案鎖偵測** | Find which process is locking a file or folder and unlock it. <br> 搵出邊個程序鎖住檔案或資料夾並解鎖。 | `filelocksmith` |
| **Disk Analyser · 磁碟分析** | Visualize folder sizes with a disk-space treemap. <br> 用樹狀圖顯示資料夾佔用磁碟空間。 | `disk` |
| **Hex Editor · 十六進位編輯器** | View and edit binary files byte-by-byte with hashing and search. <br> 逐位元組檢視同編輯二進位檔，附雜湊同搜尋。 | `hex` |
| **Drives · 磁碟機** | Manage volumes, format drives and toggle BitLocker. <br> 管理磁碟區、格式化磁碟機同切換 BitLocker。 | `drives` |
| **Disk Health (SMART) · 硬碟健康（SMART）** | Read SMART attributes, temperature, wear and failure prediction. <br> 讀取 SMART 屬性、溫度、耗損同故障預測。 | `diskhealth` |
| **Disk Benchmark · 硬碟速度測試** | CrystalDiskMark-style sequential and random read/write benchmarks. <br> CrystalDiskMark 式循序同隨機讀寫速度測試。 | `diskbench` |
| **TestDisk / PhotoRec Recovery · TestDisk / PhotoRec 資料救援** | Recover lost partitions and carve deleted files. <br> 救援遺失分割區同雕刻復原已刪除檔案。 | `testdisk` |
| **Peek · 快速預覽** | Quick-Look style instant file preview for images, text and more. <br> Quick-Look 式即時預覽圖片、文字等檔案。 | `peek` |
| **Rich Preview · 豐富預覽** | Explorer preview-pane add-ons for SVG, Markdown, code and more. <br> 檔案總管預覽窗格增益，支援 SVG、Markdown、程式碼等。 | `richpreview` |
| **OneDrive · OneDrive** | Manage Files On-Demand pinning, dehydration and Storage Sense. <br> 管理隨選檔案釘選、脫水同儲存體感知。 | `onedrive` |
| **Font Manager · 字型管理** | Install, preview and uninstall TTF/OTF fonts. <br> 安裝、預覽同移除 TTF／OTF 字型。 | `fonts` |
| **FTP / SFTP · FTP／SFTP 檔案傳輸** | Dual-pane FTP/SFTP/FTPS file transfers with a site manager. <br> 雙窗格 FTP／SFTP／FTPS 檔案傳輸，附站台管理。 | `filezilla` |
| **Config & Backup · 設定與備份** | Snapshot, restore and export encrypted configuration bundles. <br> 快照、還原同匯出加密設定包。 | `configbackup` |

### Media & Capture · 媒體與擷取

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Media · 媒體** | ffmpeg-powered video/audio convert, trim and GIF making. <br> 用 ffmpeg 轉檔、剪裁影音同整 GIF。 | `media` |
| **Audio Editor · 音訊編輯器** | In-app waveform recording, trimming and effects. <br> App 內波形錄音、剪裁同效果處理。 | `audioeditor` |
| **Audio Tagger · 音訊標籤編輯器** | Batch-edit ID3/audio metadata and cover art. <br> 批次編輯 ID3／音訊中繼資料同封面圖。 | `tags` |
| **Media Player · 媒體播放器** | libVLC player with streams, subtitles and snapshots. <br> libVLC 播放器，支援串流、字幕同截圖。 | `mediaplayer` |
| **Media Downloader · 媒體下載器** | yt-dlp video/audio downloads with quality and subtitle options. <br> yt-dlp 下載影音，可選畫質同字幕。 | `ytdlp` |
| **Document Converter · 文件轉換器** | Headless LibreOffice batch conversion between Office and PDF formats. <br> 用無介面 LibreOffice 批次轉換 Office 同 PDF 格式。 | `libreoffice` |
| **PDF Toolkit · PDF 工具箱** | Merge, split, rotate, watermark, encrypt and extract from PDFs. <br> 合併、分割、旋轉、加浮水印、加密同抽取 PDF。 | `pdf` |
| **Screen Recorder · 螢幕錄影** | Record the desktop with ffmpeg gdigrab. <br> 用 ffmpeg gdigrab 錄製桌面畫面。 | `recorder` |
| **Capture Studio · 擷取工作室** | Snip regions, screenshot, make GIFs and OCR text. <br> 擷取區域、截圖、整 GIF 同 OCR 認字。 | `capture` |
| **Text Extractor (OCR) · 原生文字辨識** | Extract text from any screen region using the native Windows OCR engine. <br> 用原生 Windows OCR 引擎由螢幕區域抽字。 | `ocr` |
| **GIF Studio · 螢幕轉 GIF** | Screen-to-GIF recording with a built-in frame editor. <br> 螢幕轉 GIF 錄製，附內建畫面格編輯器。 | `giflab` |
| **Crop And Lock · 裁切與鎖定** | Crop a window into an always-on-top floating live thumbnail. <br> 將視窗裁切成置頂浮動即時縮圖。 | `cropandlock` |
| **ZoomIt · 螢幕放大與標註** | On-screen zoom, annotation and presentation break timer. <br> 螢幕放大、標註同簡報小休倒數計時。 | `zoomit` |
| **Voice & Read-Aloud · 語音朗讀** | SAPI text-to-speech read-aloud with WAV export. <br> SAPI 文字轉語音朗讀，可匯出 WAV。 | `voice` |
| **PA Announcements · 喇叭語音廣播** | Public-address voice broadcasts with chimes, queue and priority. <br> 公共廣播語音播報，附叮咚、排隊同優先權。 | `announce` |
| **Pixel Editor · 像素畫編輯器** | Aseprite-style pixel-art editor with layers and animation frames. <br> Aseprite 式像素畫編輯器，支援圖層同動畫影格。 | `pixeleditor` |
| **Image Editor · 點陣圖影像編輯器** | Raster photo editor with filters, layers and adjustments. <br> 點陣圖相片編輯器，附濾鏡、圖層同調整功能。 | `imageeditor` |
| **Blender (3D / Render) · Blender（3D／算圖）** | Headless Blender render and animation queue. <br> 無介面 Blender 算圖同動畫佇列。 | `blender` |

### Developer · 開發者

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **VS Code · VS Code 編輯器** | Drive the VS Code CLI to open files, diffs and manage extensions. <br> 驅動 VS Code CLI 開檔、比對同管理擴充功能。 | `vscode` |
| **Windows Terminal · Windows 終端機** | Edit Windows Terminal profiles and run an embedded shell. <br> 編輯 Windows 終端機設定檔同執行內嵌殼層。 | `terminal` |
| **SSH Toolset · SSH 工具** | SSH/SFTP/SCP profiles, key generation and passwordless deploy. <br> SSH／SFTP／SCP 設定檔、金鑰產生同免密碼部署。 | `ssh` |
| **quicktype · JSON 轉型別** | Generate types and code from JSON for many languages. <br> 由 JSON 為多種語言產生型別同程式碼。 | `quicktype` |
| **API Client · REST API 用戶端** | Postman-style REST client with collections and environments. <br> Postman 式 REST 用戶端，支援集合同環境變數。 | `api` |
| **Diff & Merge (WinMerge) · 比對與合併** | Side-by-side file/folder diff and merge with patch export. <br> 並排比對同合併檔案／資料夾，可匯出修補檔。 | `diff` |
| **Diagram Editor · 圖表編輯器** | draw.io-style flowchart and diagram editor with PNG/JSON export. <br> draw.io 式流程圖同圖表編輯器，可匯出 PNG／JSON。 | `diagram` |
| **.NET Decompiler · .NET 反編譯器** | Browse and decompile .NET assemblies to C# (ILSpy-style). <br> 瀏覽同反編譯 .NET 組件成 C#（ILSpy 式）。 | `decompiler` |
| **Postgres Tool · Postgres 工具 / pgAdmin** | Connect to and query PostgreSQL databases. <br> 連接同查詢 PostgreSQL 資料庫。 | `pgadmin` |
| **SQLite Browser · SQLite 資料庫瀏覽器** | Browse, query and edit SQLite databases. <br> 瀏覽、查詢同編輯 SQLite 資料庫。 | `sqlite` |
| **Packer (Image Builder) · Packer（映像建置器）** | Build machine images from HCL templates with HashiCorp Packer. <br> 用 HashiCorp Packer 由 HCL 範本建置機器映像。 | `packer` |
| **AWS CLI · AWS 命令列** | Drive the AWS CLI for S3, EC2, IAM, Lambda and more. <br> 驅動 AWS CLI 操作 S3、EC2、IAM、Lambda 等。 | `aws` |
| **Website Cloner · 網站複製器** | Scrape, download assets and rebuild a website. <br> 抓取、下載資源同重建網站。 | `webcloner` |
| **Resume Writer · 履歷與求職信寫手** | AI-assisted resume and cover-letter writer with export. <br> AI 輔助履歷同求職信寫手，可匯出。 | `resume` |

### Network · 網絡

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Connections · 連線** | Live TCP/UDP socket list with owning processes (netstat/TCPView). <br> 即時 TCP／UDP 連線清單同擁有程序（netstat／TCPView）。 | `connections` |
| **Hosts Editor · hosts 編輯器** | Edit the hosts file and block domains. <br> 編輯 hosts 檔案同封鎖網域。 | `hosts` |
| **Packet Capture · 封包擷取** | Capture and filter packets with tshark/dumpcap (pcap). <br> 用 tshark／dumpcap 擷取同過濾封包（pcap）。 | `wireshark` |
| **Nmap Scanner · 網絡掃描** | Scan hosts, ports, services and OS with Nmap. <br> 用 Nmap 掃描主機、端口、服務同作業系統。 | `nmap` |
| **VPN & Mesh · VPN 與網狀網** | Manage NordVPN and Tailscale mesh connections. <br> 管理 NordVPN 同 Tailscale 網狀網連線。 | `vpn` |
| **RustDesk · 遠端桌面** | Self-hostable remote desktop control (TeamViewer alternative). <br> 可自架嘅遠端桌面控制（TeamViewer 替代品）。 | `rustdesk` |
| **Cloudflare & Tunnel · Cloudflare 與 Tunnel** | Cloudflared tunnels, DNS routing, Access, DoH and WARP. <br> Cloudflared 隧道、DNS 路由、Access、DoH 同 WARP。 | `cloudflare` |
| **Home Assistant · 家居助理** | Drive the Home Assistant REST API for scenes, lights and more. <br> 驅動 Home Assistant REST API 控制場景、燈光等。 | `homeassistant` |
| **In-App Login · 內置登入** | Shared WebView2 OAuth and sign-in for connected services. <br> 共用 WebView2 OAuth 同登入連接服務。 | `weblogin` |

### Apps, Git & Packages · 應用程式、Git 與套件

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Git & GitHub · Git 與 GitHub** | Multi-repo workbench for git and gh operations with a chunked uploader. <br> 多儲存庫工作台，操作 git 同 gh，附分塊上傳器。 | `git` |
| **Package Manager · 套件管理** | One front-end over winget, scoop, choco, pip, npm and more. <br> 統一前端操作 winget、scoop、choco、pip、npm 等。 | `packages` |
| **Native OSS Clones · 開源原生分頁** | Map of open-source app ideas remade as native C# WinForge tabs. <br> 將開源 app 想法重製成 WinForge 原生 C# 分頁嘅索引。 | `ossapps` |
| **Cake Factory & Farm · 蛋糕工廠與農場** | HTML5 reactor-powered cake factory game with finite supplies, manual HACCP gates and signed `.cake` files. <br> HTML5 反應堆供電蛋糕工廠遊戲，附有限補給、手動 HACCP 放行關卡同已簽署 `.cake` 檔。 | `cakefactory` |
| **Feed Reader · RSS 閱讀器** | Native RSS/Atom reader inspired by QuiteRSS and Fluent Reader. <br> 受 QuiteRSS 同 Fluent Reader 啟發嘅原生 RSS／Atom 閱讀器。 | `rss` |
| **App Uninstaller · 應用程式解除安裝** | Remove apps and Appx packages via winget. <br> 用 winget 移除應用程式同 Appx 套件。 | `uninstall` |
| **Android (ADB) · Android（ADB）** | adb devices, APK install, shell, logcat and scrcpy mirroring. <br> adb 裝置、安裝 APK、shell、logcat 同 scrcpy 鏡像。 | `adb` |
| **Fastboot / Flasher · Fastboot／刷機** | Unlock bootloaders and flash factory/boot images. <br> 解鎖 bootloader 同刷入原廠／boot 映像。 | `fastboot` |
| **Android Emulator & SDK · Android 模擬器與 SDK** | Manage AVDs and the Android SDK manager. <br> 管理 AVD 虛擬裝置同 Android SDK 管理員。 | `emulator` |
| **qBittorrent · 種子下載** | Drive the qBittorrent Web API for torrents. <br> 驅動 qBittorrent Web API 做種子下載。 | `qbittorrent` |
| **Native Torrent · 原生種子下載** | In-process managed BitTorrent engine for magnets and downloads. <br> 內建受控 BitTorrent 引擎，處理磁力同下載。 | `torrent` |
| **Communications · 通訊** | Mail, Teams, Discord and Telegram deep links and quick actions. <br> 信件、Teams、Discord、Telegram 深層連結同快速動作。 | `comms` |
| **Mail · 電郵** | IMAP/SMTP mail client with compose, reply and attachments. <br> IMAP／SMTP 電郵客戶端，可撰寫、回覆同附件。 | `mail` |

### AI · 人工智能

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **AI Agents · AI 代理** | Install and launch terminal AI coding agents (Claude Code, Codex and more). <br> 安裝同啟動終端機 AI 編程代理（Claude Code、Codex 等）。 | `ai` |
| **AI Chat · AI 聊天** | OpenWebUI-style chat over local and cloud LLMs. <br> OpenWebUI 式聊天，連接本機同雲端大模型。 | `aichat` |
| **Ollama · 本地大模型** | Pull, serve and chat with local GGUF models via Ollama. <br> 用 Ollama 下載、提供同對話本機 GGUF 模型。 | `ollama` |

### Window Management · 視窗管理

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Window Manager · 視窗管理** | Tile, cascade and pin windows always-on-top. <br> 並排、層疊同置頂視窗。 | `windows` |
| **Workspaces · 工作區** | Capture and relaunch named app layouts and window positions. <br> 擷取同還原具名應用程式佈局同視窗位置。 | `workspaces` |
| **FancyZones · 視窗分區** | Zone editor and snap layouts for window tiling. <br> 分區編輯器同貼齊版面做視窗排版。 | `fancyzones` |
| **AltSnap · Alt 拖曳視窗** | Move and resize windows with a modifier key from anywhere. <br> 用修飾鍵喺任何位置拖曳同縮放視窗。 | `altsnap` |
| **Komorebi (Tiling WM) · Komorebi 平鋪視窗管理** | Drive the komorebi tiling window manager daemon. <br> 驅動 komorebi 平鋪視窗管理守護程序。 | `komorebi` |
| **GlazeWM Tiling · GlazeWM 平鋪視窗** | Drive the GlazeWM tiling window manager. <br> 驅動 GlazeWM 平鋪視窗管理員。 | `glazewm` |

### PowerToys-style Utilities · PowerToys 式工具

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Keyboard Remapper · 鍵盤重新對應** | Remap keys via the Scancode Map (SharpKeys-style). <br> 用 Scancode Map 重新對應按鍵（SharpKeys 式）。 | `keyboard` |
| **Hotkey & Macro Runner · 熱鍵與巨集** | Run hotkeys, macros and text expansion snippets. <br> 執行熱鍵、巨集同文字展開片語。 | `hotkeys` |
| **Shortcut Guide · 快捷鍵指南** | Hold-Win overlay cheat sheet of Windows shortcuts. <br> 揿住 Win 鍵顯示快捷鍵速查覆蓋層。 | `shortcutguide` |
| **Command Palette · 指令面板** | Global launcher and Run box for apps, calc and system actions. <br> 全域啟動器同執行框，啟動應用程式、計算同系統動作。 | `cmdpalette` |
| **Color Picker · 螢幕取色** | System-wide color picker with hex/RGB/HSL output. <br> 全系統取色器，輸出 hex／RGB／HSL。 | `colorpicker` |
| **Screen Ruler · 螢幕間尺** | Measure distances and pixels on screen. <br> 喺螢幕量度距離同像素。 | `screenruler` |
| **Mouse Utilities · 滑鼠工具** | Find My Mouse, highlighter, crosshairs and pointer jump. <br> 搵滑鼠、點擊標示、十字線同指標跳轉。 | `mouseutils` |
| **Mouse & Pointer · 滑鼠與指標** | Adjust pointer speed, acceleration and behaviour. <br> 調整指標速度、加速同行為。 | `mouse` |
| **Mouse Without Borders · 無界滑鼠** | Share one keyboard and mouse across multiple PCs (software KVM). <br> 跨多部電腦共享一套鍵盤滑鼠（軟件 KVM）。 | `mwb` |
| **Quick Accent · 快速重音符** | Insert accented and special characters by holding a letter. <br> 揿住字母快速插入重音同特殊字元。 | `quickaccent` |
| **Command Not Found · 搵唔到指令** | Suggest a winget package for a missing PowerShell command. <br> 為搵唔到嘅 PowerShell 指令建議 winget 套件。 | `cmdnotfound` |
| **Clipboard · 剪貼簿** | Richer clipboard history with QR-code generation. <br> 更豐富嘅剪貼簿歷史，附二維碼產生。 | `clipboard` |
| **Advanced Paste · 進階貼上** | Paste-as transforms: plain text, Markdown, JSON, OCR and AI. <br> 貼上轉換：純文字、Markdown、JSON、OCR 同 AI。 | `advancedpaste` |
| **Taskbar Tweaker · 工作列調校** | Tweak taskbar alignment, button combining, tray and clock. <br> 調校工作列對齊、合併按鈕、系統匣同時鐘。 | `taskbar-tweaker` |
| **Windhawk Mods · Windhawk 模組** | Manage Windhawk mods that customize the taskbar, clock and shell. <br> 管理 Windhawk 模組，自訂工作列、時鐘同殼層。 | `windhawk` |
| **LightSwitch (Auto Dark Mode) · 自動深淺色** | Automatically switch light/dark theme on a sunrise/sunset schedule. <br> 按日出日落排程自動切換深淺色主題。 | `lightswitch` |
| **Rainmeter Widgets · Rainmeter 桌面小工具** | Install and toggle Rainmeter desktop skins and widgets. <br> 安裝同切換 Rainmeter 桌面皮膚同小工具。 | `taskbar` |
| **Time & Unit Tools · 時間與單位工具** | World clock, time-zone converter and unit converters. <br> 世界時鐘、時區換算同單位換算。 | `time` |
| **Flashcards · 間隔重複記憶卡** | Spaced-repetition flashcard study with SM-2 scheduling. <br> 用 SM-2 排程嘅間隔重複記憶卡學習。 | `flashcards` |

### Virtualization & Containers · 虛擬化與容器

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Docker · Docker 容器管理** | Manage Docker containers, images, volumes and networks locally. <br> 本機管理 Docker 容器、映像、磁碟區同網路。 | `docker` |
| **Docker over SSH · 透過 SSH 控制 Docker** | Control containers on a remote Docker host over SSH. <br> 透過 SSH 控制遠端 Docker 主機上嘅容器。 | `dockerssh` |
| **WSL & VM Launcher · WSL 與 VM 啟動器** | Launch WSL distros, Windows Sandbox and virtual machines. <br> 啟動 WSL 發行版、Windows 沙盒同虛擬機。 | `wsl` |
| **VirtualBox Manager · VirtualBox 管理** | Drive VBoxManage for VMs, snapshots and clones. <br> 驅動 VBoxManage 管理虛擬機、快照同複製。 | `virtualbox` |
| **Proxmox VE · Proxmox VE 虛擬化** | Manage Proxmox VE nodes, QEMU VMs and LXC containers via the REST API. <br> 用 REST API 管理 Proxmox VE 節點、QEMU 虛擬機同 LXC 容器。 | `proxmox` |

### Security & Vaults · 安全與保險庫

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **WinForge Vault · WinForge 保險庫** | On-the-fly encrypted volume containers (VeraCrypt-derived). <br> 即時加密嘅磁碟區容器（源自 VeraCrypt）。 | `vault-volumes` |
| **Bitwarden Vault · Bitwarden 密碼庫** | Drive the Bitwarden CLI for logins, TOTP and generators. <br> 驅動 Bitwarden CLI 管理登入、TOTP 同密碼產生。 | `bitwarden` |
| **KeePass Vault · 密碼保險庫** | Local offline KeePass (kdbx) password vault, natively encrypted. <br> 本機離線 KeePass（kdbx）密碼庫，原生加密。 | `keepass` |

### Gaming & Emulation · 遊戲與模擬

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Minecraft World Editor (Amulet) · Minecraft 世界編輯器（Amulet）** | Launch the Amulet Minecraft world/map editor with backups. <br> 啟動 Amulet Minecraft 世界／地圖編輯器，附備份。 | `amulet` |
| **Minecraft Server · Minecraft 伺服器** | Set up and run a Paper/Spigot server with plugins and console. <br> 建立同執行 Paper／Spigot 伺服器，附外掛同主控台。 | `minecraftserver` |
| **ViaProxy · Minecraft 版本代理** | Cross-version Minecraft protocol bridge (ViaVersion-based). <br> 跨版本 Minecraft 協定橋接（基於 ViaVersion）。 | `viaproxy` |
| **Imaging & Game Tools · 燒錄與遊戲工具** | USB/SD flashing (Rufus, Pi Imager) and Minecraft world downloader. <br> USB／SD 燒錄（Rufus、Pi Imager）同 Minecraft 世界下載器。 | `imaging` |

### Nuclear Reactor · 核反應堆

| Module · 模組 | Description · 說明 | `--page` |
|---|---|---|
| **Nuclear Reactor · 核反應堆** | Hyper-realistic PWR control-room simulator with full physics and safety systems. <br> 超寫實壓水堆控制室模擬器，附完整物理同保護系統。 | `reactor` |

---

## 📚 Documentation & Wiki · 文件與 Wiki

**EN —** The wiki is a categorized index of every module, with deeper pages for the headline ones — start at **[docs/wiki/Home.md](docs/wiki/Home.md)**. The flagship has its own full **[Nuclear Reactor Operating Manual](docs/wiki/Nuclear-Reactor-Operating-Manual.md)**, and the HTML5 cake simulator has a full **[Cake Factory & Farm manual](docs/wiki/Cake-Factory-and-Farm.md)**.

**粵語 —** Wiki 係所有模組嘅分類索引，重點模組仲有專頁 — 由 **[docs/wiki/Home.md](docs/wiki/Home.md)** 開始。旗艦有自己完整嘅**[核反應堆操作手冊](docs/wiki/Nuclear-Reactor-Operating-Manual.md)**，HTML5 蛋糕模擬器亦有完整嘅**[蛋糕工廠與農場手冊](docs/wiki/Cake-Factory-and-Farm.md)**。

---

## 📄 License · 授權條款

**EN —** Released under the [MIT License](LICENSE). Provided "as is", without warranty of any kind.

**粵語 —** 以 [MIT 授權條款](LICENSE) 發佈。按「現狀」提供，不附任何形式嘅保證。

---

<div align="center">

Made with WinUI 3 · 用 WinUI 3 製作 · `English + 繁體中文／粵語`

</div>
