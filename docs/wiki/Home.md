# WinForge Wiki · 視窗鑄造 Wiki

**EN —** WinForge is a fully bilingual (English + 繁體中文／粵語) Windows convenience suite built on **WinUI 3 / .NET 11**. It bundles **112 in-app modules** plus a ~1,140-entry Windows-11 tweak catalog. This wiki is a **categorized index of every module**, with deeper pages for the headline ones. The canonical image set lives in the repo under [`docs/`](https://github.com/codingmachineedge/WinForge/tree/main/docs).

**粵語 —** WinForge 係一個全程雙語（英文 + 繁體中文／粵語）嘅 Windows 便利套件，用 **WinUI 3 / .NET 11** 整。佢內含 **112 個 app 內模組**，加埋一個約 1,140 項嘅 Windows 11 調校目錄。呢個 wiki 係**所有模組嘅分類索引**，重點模組仲有專頁。正式圖檔放喺儲存庫嘅 [`docs/`](https://github.com/codingmachineedge/WinForge/tree/main/docs)。

> **Wiki ↔ README sync · wiki 與 README 同步**
> These pages mirror the *Module catalog* in [`README.md`](https://github.com/codingmachineedge/WinForge/blob/main/README.md). The source of truth for the module list is [`Services/ModuleRegistry.cs`](https://github.com/codingmachineedge/WinForge/blob/main/Services/ModuleRegistry.cs); for images it is `docs/screenshot-<key>.png`. README uses repo-relative paths; wiki pages use absolute `raw.githubusercontent.com` URLs because the wiki is a separate Git repo.
> 呢啲頁對應 [`README.md`](https://github.com/codingmachineedge/WinForge/blob/main/README.md) 入面嘅「模組目錄」。模組清單嘅權威來源係 [`Services/ModuleRegistry.cs`](https://github.com/codingmachineedge/WinForge/blob/main/Services/ModuleRegistry.cs)；圖檔權威來源係 `docs/screenshot-<key>.png`。README 用相對路徑；wiki 因為係另一個 Git repo，所以用絕對 `raw.githubusercontent.com` 連結。

## Download · 下載

**EN —** Get the latest **`WinForge-Setup.exe`** (installer) or **`WinForge-portable-x64-1.0.x.zip`** (portable, currently v1.0.8) from [GitHub Releases](https://github.com/codingmachineedge/WinForge/releases). Both are self-contained x64. Build from source: `dotnet build -c Release -p:Platform=x64`.

**粵語 —** 喺 [GitHub Releases](https://github.com/codingmachineedge/WinForge/releases) 下載最新嘅 **`WinForge-Setup.exe`**（安裝程式）或 **`WinForge-portable-x64-1.0.x.zip`**（免安裝，現為 v1.0.8）。兩者都係自包含 x64。由原始碼建置：`dotnet build -c Release -p:Platform=x64`。

---

## Module index · 模組索引

**EN —** All 112 modules, grouped by area. Linked names have a dedicated wiki page; the rest open in-app via `WinForge.exe --page <Tag>`.
**粵語 —** 全部 112 個模組，按範疇分組。有連結嘅名有專屬 wiki 頁；其餘可用 `WinForge.exe --page <Tag>` 喺 app 內打開。

### System & Tweaks · 系統與調校
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| [Dashboard](Dashboard) | `dashboard` | Master search across every module · 跨所有模組嘅總搜尋 |
| Registry Editor · 登錄編輯器 | `module.regedit` | Browse/edit the live registry · 瀏覽／編輯實時登錄檔 |
| System Doctors · 系統醫生 | `module.doctors` | One-click repairs (spooler, DNS, taskbar, search…) · 一鍵修復 |
| Services · 服務 | `module.services` | Start/stop & startup type · 啟動／停止同啟動類型 |
| Scheduled Tasks · 排程工作 | `module.tasks` | View & run scheduled tasks · 檢視同執行排程工作 |
| Devices · 裝置 | `module.devices` | Enable/disable hardware & drivers · 啟用／停用硬件同驅動 |
| ViVeTool · 功能旗標 | `module.vivetool` | Toggle hidden Windows feature flags · 切換隱藏功能旗標 |
| Startup Apps · 開機程式 | `module.startup` | Manage logon/startup items · 管理開機項目 |
| Environment Variables · 環境變數 | `module.envvars` | Edit env vars + PATH editor · 編輯環境變數同 PATH |
| Event Viewer · 事件檢視器 | `module.events` | Read Windows event logs · 讀取事件記錄 |
| System Info (Winfetch) · 系統資訊 | `module.winfetch` | neofetch-style specs read-out · neofetch 式規格 |
| [System Monitor](System-Monitor) | `module.monitor` | Live CPU/RAM/network, priority & affinity · 即時監察 |
| Battery & Thermal · 電池與散熱 | `module.battery` | Wear/health, temps, fan, power report · 耗損、溫度、風扇 |
| Volume Mixer · 音量混合器 | `module.mixer` | Per-app volume/mute (WASAPI) · 逐個應用程式音量 |
| Context Menu · 右鍵選單 | `module.contextmenu` | Add/remove shell verbs · 新增／移除右鍵動詞 |
| Nilesoft Shell · 右鍵選單 | `module.nilesoftshell` | Modern customizable context menu · 現代可自訂右鍵選單 |
| Awake · 保持喚醒 | `module.awake` | Keep the PC awake · 令電腦唔瞓 |
| [Settings & Control Panel](Settings-and-Control-Panel) | `module.settingshub` | In-app settings + applet launcher · app 內設定同 applet |
| Native Utilities · 原生工具 | `module.native` | Wi-Fi pw, SMB, brightness, certs, BT… · 原生雜錦 |
| PowerToys Extras · PowerToys 額外工具 | `module.powertoys` | Image Resizer, OCR, always-on-top · 縮放、OCR、置頂 |
| World Monitor · 世界監察 | `module.worldmonitor` | News/geopolitics/finance dashboard · 情報儀表板 |
| Activity Timeline · 活動時間軸 | `module.timelens` | Foreground-window time tracking · 前景視窗時間追蹤 |

### Files & Disks · 檔案與磁碟
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| Archives · 壓縮檔 | `module.archives` | 7-Zip compress/extract/test · 7-Zip 壓縮／解壓 |
| Batch Rename · 批次改名 | `module.rename` | Regex/sequence bulk rename · 正則／序號批次改名 |
| Bulk File Ops · 批次檔案操作 | `module.bulkops` | Mass move/copy/recycle/attributes · 批量檔案操作 |
| New+ · 範本新增 | `module.newplus` | Create from templates (New menu) · 由範本新增 |
| Duplicate Finder · 重複檔案搜尋 | `module.duplicates` | Size+hash dedupe · 按大小同雜湊去重複 |
| File Locksmith · 檔案鎖偵測 | `module.filelocksmith` | Find what's locking a file · 搵出鎖住檔案嘅程序 |
| Disk Analyser · 磁碟分析 | `module.disk` | Folder-size treemap · 資料夾大小樹狀圖 |
| Drives · 磁碟機 | `module.drives` | Volumes, format, mount ISO/VHD · 磁碟區、格式化、掛載 |
| TestDisk / PhotoRec · 資料救援 | `module.testdisk` | Recover partitions & files · 救援分割區同檔案 |
| Peek · 快速預覽 | `module.peek` | Quick-Look file preview · 快速預覽檔案 |
| Rich Preview · 豐富預覽 | `module.richpreview` | Explorer preview-pane add-ons · 預覽窗格增益 |
| OneDrive · OneDrive | `module.onedrive` | Files On-Demand, Storage Sense · 隨選檔案 |
| Font Manager · 字型管理 | `module.fonts` | Install/preview/uninstall fonts · 安裝／預覽字型 |
| FTP / SFTP (FileZilla) · 檔案傳輸 | `module.filezilla` | Dual-pane FTP/SFTP transfers · 雙窗格檔案傳輸 |
| Config & Backup · 設定與備份 | `module.configbackup` | Snapshot/restore + encrypted bundles · 快照與加密匯出 |

### Media & Capture · 媒體與擷取
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| [Media](Media) | `module.media` | ffmpeg convert/trim/GIF · ffmpeg 轉檔／剪裁／GIF |
| Audio Editor · 音訊編輯器 | `module.audioeditor` | Audacity-style waveform editing · 波形編輯 |
| Media Player · 媒體播放器 | `module.mediaplayer` | libVLC player + streams/subtitles · libVLC 播放器 |
| Media Downloader · 媒體下載器 | `module.ytdlp` | yt-dlp video/audio download · yt-dlp 下載 |
| Document Converter · 文件轉換器 | `module.libreoffice` | LibreOffice headless batch convert · 批次文件轉換 |
| Screen Recorder · 螢幕錄影 | `module.recorder` | ffmpeg gdigrab desktop record · 桌面錄影 |
| Capture Studio · 擷取工作室 | `module.capture` | Snip, screenshot, GIF, OCR · 擷取、截圖、OCR |
| GIF Studio · 螢幕轉 GIF | `module.giflab` | Screen-to-GIF + frame editor · 螢幕轉 GIF |
| Crop And Lock · 裁切與鎖定 | `module.cropandlock` | Crop a window to a floating thumbnail · 裁切置頂浮窗 |
| Voice & Read-Aloud · 語音朗讀 | `module.voice` | SAPI text-to-speech · 文字轉語音 |
| Pixel Editor · 像素畫編輯器 | `module.pixeleditor` | Aseprite-style pixel-art editor · 像素畫編輯 |
| Blender (3D / Render) · Blender | `module.blender` | Headless render/animation queue · 算圖佇列 |

### Developer · 開發者
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| VS Code · VS Code 編輯器 | `module.vscode` | Drive the `code` CLI · 驅動 `code` CLI |
| Windows Terminal · Windows 終端機 | `module.terminal` | Edit profiles + embedded shell · 設定檔同內嵌殼層 |
| SSH Toolset · SSH 工具 | `module.ssh` | SSH/SFTP/SCP, keygen, deploy · SSH 工具 |
| quicktype · JSON 轉型別 | `module.quicktype` | JSON → types/code generator · JSON 轉型別 |
| Postgres Tool / pgAdmin · Postgres 工具 | `module.pgadmin` | Connect & query PostgreSQL · 連接同查詢 Postgres |
| Packer (Image Builder) · Packer | `module.packer` | HashiCorp Packer HCL builds · Packer 映像建置 |
| AWS CLI · AWS 命令列 | `module.aws` | Drive the AWS CLI · 驅動 AWS CLI |
| WSL & VM Launcher · WSL 與 VM | `module.wslvm` | Launch WSL/Sandbox/VMs · 啟動 WSL／沙盒／VM |
| VirtualBox Manager · VirtualBox 管理 | `module.virtualbox` | Drive VBoxManage · 驅動 VBoxManage |
| Website Cloner · 網站複製器 | `module.webcloner` | Scrape & rebuild a site · 抓取重建網站 |
| Resume Writer · 履歷與求職信寫手 | `module.resume` | AI resume/cover-letter writer · AI 履歷寫手 |

### Network · 網絡
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| [Connections](Connections) | `module.connections` | Live TCP/UDP sockets + owners · 即時連線同擁有者 |
| Hosts Editor · hosts 編輯器 | `module.hosts` | Edit hosts + block domains · 編輯 hosts、封鎖網域 |
| Packet Capture (Wireshark) · 封包擷取 | `module.wireshark` | tshark/dumpcap capture & filters · 封包擷取 |
| Nmap Scanner · 網絡掃描 | `module.nmap` | Port/host/service/OS scan · 網絡掃描 |
| VPN & Mesh · VPN 與網狀網 | `module.vpn` | NordVPN + Tailscale mesh · NordVPN 同 Tailscale |
| RustDesk · 遠端桌面 | `module.rustdesk` | Self-hostable remote desktop · 可自架遠端桌面 |
| [Cloudflare & Tunnel](Cloudflare-and-Tunnel) | `module.cloudflare` | Tunnels, DNS routing, Access, DoH, WARP · cloudflared |
| Home Assistant · 家居助理 | `module.homeassistant` | Drive the HA REST API · 驅動 HA REST API |
| In-App Login · 內置登入 | `module.weblogin` | Shared WebView2 OAuth/sign-in · 共用內置登入 |

### Apps, Git & Packages · 應用程式、Git 與套件
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| [Git & GitHub](Git-and-GitHub) | `module.git` | Multi-repo, chunked uploader, git/gh ops · Git 工作台 |
| [Package Manager](Package-Manager) | `module.packages` | One front-end over 8+ package managers · 套件管理 |
| App Uninstaller · 應用程式解除安裝 | `module.uninstall` | Remove apps/Appx (winget) · 移除應用程式 |
| Android (ADB) · Android（ADB） | `module.adb` | adb devices/APK/shell/logcat · adb 工具 |
| Fastboot / Flasher · Fastboot／刷機 | `module.fastboot` | Unlock & flash images · 解鎖同刷機 |
| Android Emulator & SDK · 模擬器與 SDK | `module.emulator` | AVDs + Android SDK manager · 模擬器同 SDK |
| qBittorrent · 種子下載 | `module.qbittorrent` | Drive qBittorrent Web API · 種子下載 |
| Communications · 通訊 | `module.comms` | Mail/Teams/Discord deep links · 通訊深層連結 |
| Mail · 電郵 | `module.mail` | IMAP/SMTP mail client · 電郵客戶端 |

### AI · 人工智能
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| [AI Agents](AI-Agents) | `module.aiagents` | Install/launch terminal AI coding agents · AI 編程代理 |
| AI Chat · AI 聊天 | `module.aichat` | OpenWebUI-style chat over local/cloud LLMs · AI 聊天 |
| Ollama · 本地大模型 | `module.ollama` | Pull/serve/chat local GGUF models · 本地大模型 |

### Window Management · 視窗管理
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| Window Manager · 視窗管理 | `module.windows` | Tile/cascade/always-on-top · 並排／層疊／置頂 |
| Workspaces · 工作區 | `module.workspaces` | Capture & relaunch app layouts · 擷取同還原佈局 |
| FancyZones · 視窗分區 | `module.fancyzones` | Zone editor + snap layouts · 分區編輯器 |
| AltSnap · Alt 拖曳視窗 | `module.altsnap` | Modifier-key window drag/resize · 修飾鍵拖曳視窗 |
| Komorebi (Tiling WM) · 平鋪視窗管理 | `module.komorebi` | Drive komorebi tiling WM · 平鋪視窗管理 |
| GlazeWM Tiling · 平鋪視窗 | `module.glazewm` | Drive GlazeWM tiling · GlazeWM 平鋪 |

### PowerToys-style utilities · PowerToys 式工具
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| Keyboard Remapper · 鍵盤重新對應 | `module.keyboard` | Remap keys (Scancode Map) · 重新對應按鍵 |
| Hotkey & Macro Runner · 熱鍵與巨集 | `module.hotkeys` | Hotkeys, macros, text expansion · 熱鍵同巨集 |
| Shortcut Guide · 快捷鍵指南 | `module.shortcutguide` | Hold-Win shortcut overlay · 快捷鍵覆蓋層 |
| Command Palette · 指令面板 | `module.cmdpalette` | Global launcher/Run · 全域啟動器 |
| Color Picker · 螢幕取色 | `module.colorpicker` | System-wide color picker · 螢幕取色 |
| Screen Ruler · 螢幕間尺 | `module.screenruler` | Measure pixels on screen · 螢幕量度 |
| ZoomIt · 螢幕放大與標註 | `module.zoomit` | Zoom, annotate, break timer · 放大同標註 |
| Mouse Utilities · 滑鼠工具 | `module.mouseutils` | Find My Mouse, highlighter… · 滑鼠工具 |
| Mouse & Pointer · 滑鼠與指標 | `module.mouse` | Pointer speed/behaviour · 指標速度 |
| Mouse Without Borders · 無界滑鼠 | `module.mwb` | Share keyboard/mouse across PCs · 無界滑鼠 |
| Quick Accent · 快速重音符 | `module.quickaccent` | Insert accented characters · 插入重音字元 |
| Command Not Found · 搵唔到指令 | `module.cmdnotfound` | Suggest winget pkg for missing cmd · 建議套件 |
| Clipboard · 剪貼簿 | `module.clipboard` | Richer clipboard history + QR · 剪貼簿歷史 |
| Advanced Paste · 進階貼上 | `module.advancedpaste` | Paste-as transform / OCR / AI · 進階貼上 |
| Taskbar Tweaker · 工作列調校 | `module.taskbar-tweaker` | Taskbar tweaks (align/combine…) · 工作列調校 |
| Windhawk Mods · Windhawk 模組 | `module.windhawk` | Manage Windhawk mods · 管理 Windhawk 模組 |
| LightSwitch (Auto Dark Mode) · 自動深淺色 | `module.lightswitch` | Auto light/dark scheduling · 自動深淺色 |
| Rainmeter Widgets · 桌面小工具 | `module.rainmeter` | Rainmeter desktop skins · Rainmeter 皮膚 |
| Time & Unit Tools · 時間與單位工具 | `module.timeunit` | World clock + unit converters · 世界時鐘同換算 |

### Security & Vaults · 安全與保險庫
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| WinForge Vault · WinForge 保險庫 | `module.vault-volumes` | Encrypted volumes (VeraCrypt-derived) · 加密磁碟區 |
| Bitwarden Vault · Bitwarden 密碼庫 | `module.bitwarden` | Drive the Bitwarden CLI · 驅動 Bitwarden CLI |

### Gaming & Emulation · 遊戲與模擬
| Module · 模組 | `--page` | Summary · 摘要 |
|---|---|---|
| Minecraft World Editor (Amulet) · 世界編輯器 | `module.amulet` | Launch the Amulet map editor · Amulet 地圖編輯 |
| Minecraft Server · Minecraft 伺服器 | `module.minecraftserver` | Paper/Spigot server + plugins · 伺服器同外掛 |
| ViaProxy · 版本代理 | `module.viaproxy` | Cross-version protocol bridge · 版本協定橋接 |
| Imaging & Game Tools · 燒錄與遊戲工具 | `module.imaging` | USB/SD flashing, world downloader · USB 燒錄 |

---

See [Screenshots](Screenshots) for the full image set and capture status.
睇 [Screenshots](Screenshots) 了解完整圖檔同擷取狀態。

---
*Made with WinUI 3 · 用 WinUI 3 製作 · `English + 繁體中文／粵語`*
