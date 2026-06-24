using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>一個應用程式內模組（頁面）· One in-app module (page) for page-search.</summary>
public sealed class ModuleInfo
{
    public string Tag { get; init; } = "";
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    public string Glyph { get; init; } = "";
    public string Keywords { get; init; } = "";

    public string Haystack => $"{En} {Zh} {Keywords}".ToLowerInvariant();
}

/// <summary>
/// 所有模組頁面嘅登記（畀搜尋用）· Registry of every module page, used by the master/page search.
/// </summary>
public static class ModuleRegistry
{
    public static readonly List<ModuleInfo> All = new()
    {
        new() { Tag = "dashboard", En = "Dashboard", Zh = "概覽", Glyph = "", Keywords = "home overview start 主頁 概覽" },
        new() { Tag = "module.git", En = "Git & GitHub", Zh = "Git 與 GitHub", Glyph = "", Keywords = "git github commit push pull fetch repo repos list clone branch tag merge rebase stash remote worktree submodule uploader issue pull request pr actions workflow release gist secret label star fork notifications gh cli 版本控制 儲存庫 分支 標籤" },
        new() { Tag = "module.vscode", En = "VS Code", Zh = "VS Code 編輯器", Glyph = ((char)0xE943).ToString(), Keywords = "vscode vs code visual studio code editor cli open file folder workspace new window reuse diff merge goto line extension install uninstall list profile insiders tunnel remote settings keybindings code-workspace 編輯器 擴充功能 比對 合併 設定 遠端 隧道" },
        new() { Tag = "module.git", En = "Git & GitHub", Zh = "Git 與 GitHub", Glyph = "", Keywords = "git github commit push pull fetch repo repos list clone branch tag merge rebase stash remote worktree submodule uploader issue pull request pr actions workflow release gist secret label star fork notifications gh cli gitty up checkpoint restore alias undo share workflow 版本控制 儲存庫 分支 標籤 工作流程 別名 撤回 檢查點" },
        new() { Tag = "module.archives", En = "Archives", Zh = "壓縮檔", Glyph = "", Keywords = "zip 7z rar tar gzip compress extract 解壓 壓縮" },
        new() { Tag = "module.media", En = "Media", Zh = "媒體", Glyph = "", Keywords = "ffmpeg video audio convert trim gif 影片 音訊 轉檔" },
        new() { Tag = "module.audioeditor", En = "Audio Editor", Zh = "音訊編輯器", Glyph = ((char)0xE8D6).ToString(), Keywords = "audio editor audacity waveform record mic microphone play playback trim fade normalize gain volume speed tempo pitch shift noise reduction denoise reverb echo compressor eq equalizer mix concat export wav mp3 flac 音訊 編輯 波形 錄音 播放 剪裁 淡入 淡出 正規化 增益 變速 變調 降噪 混音 匯出" },
        new() { Tag = "module.mediaplayer", En = "Media Player", Zh = "媒體播放器", Glyph = ((char)0xE714).ToString(), Keywords = "vlc libvlc media player play video audio movie music stream url playlist subtitle audio track snapshot fullscreen seek volume transcode convert mp4 mp3 webm wav 播放器 媒體 影片 音樂 串流 播放清單 字幕 截圖 全螢幕 轉檔" },
        new() { Tag = "module.ytdlp", En = "Media Downloader", Zh = "媒體下載器", Glyph = ((char)0xE896).ToString(), Keywords = "yt-dlp ytdlp youtube download downloader video audio mp3 m4a playlist subtitles subs format quality 1080p 720p thumbnail metadata sponsorblock cookies twitch vimeo soundcloud bilibili 下載 影片 音訊 字幕 播放清單 畫質 縮圖" },
        new() { Tag = "module.blender", En = "Blender (3D / Render)", Zh = "Blender（3D／算圖）", Glyph = ((char)0xE7F4).ToString(), Keywords = "blender 3d render rendering cycles eevee headless animation frame gltf fbx obj python script batch queue cpu gpu samples output 算圖 渲染 動畫 影格 匯出 批次 佇列" },
        new() { Tag = "module.libreoffice", En = "Document Converter", Zh = "文件轉換器", Glyph = ((char)0xE8A5).ToString(), Keywords = "libreoffice soffice document converter convert batch pdf docx xlsx odt ods pptx csv txt office writer calc impress headless 文件 轉換 轉檔 批次 辦公" },
        new() { Tag = "module.regedit", En = "Registry Editor", Zh = "登錄編輯器", Glyph = "", Keywords = "registry regedit hive key value 登錄檔" },
        new() { Tag = "module.doctors", En = "System Doctors", Zh = "系統醫生", Glyph = ((char)0xE95E).ToString(), Keywords = "doctor repair fix rescue printer spooler dns network sleep wake taskbar start search index explorer icon thumbnail cache ownership permissions 修復 醫生 救援 列印 網絡 睡眠 喚醒 工作列 搜尋 圖示 縮圖 擁有權 權限" },
        new() { Tag = "module.services", En = "Services", Zh = "服務", Glyph = "", Keywords = "services start stop startup type 服務" },
        new() { Tag = "module.tasks", En = "Scheduled Tasks", Zh = "排程工作", Glyph = "", Keywords = "scheduled task scheduler run 排程" },
        new() { Tag = "module.devices", En = "Devices", Zh = "裝置", Glyph = "", Keywords = "device manager hardware driver 裝置 驅動" },
        new() { Tag = "module.vivetool", En = "ViVeTool", Zh = "功能旗標", Glyph = ((char)0xE9D5).ToString(), Keywords = "vivetool vive feature flag experiment hidden file explorer tabs new start menu modern context menu snap layouts energy saver click to do 功能 旗標 實驗 隱藏 分頁 開始功能表" },
        new() { Tag = "module.startup", En = "Startup Apps", Zh = "開機程式", Glyph = "", Keywords = "startup autostart logon run 開機 自啟動" },
        new() { Tag = "module.rename", En = "Batch Rename", Zh = "批次改名", Glyph = "", Keywords = "rename bulk powerrename regex 改名 批次" },
        new() { Tag = "module.bulkops", En = "Bulk File Ops", Zh = "批次檔案操作", Glyph = "", Keywords = "bulk file move copy delete attributes 批次 檔案" },
        new() { Tag = "module.duplicates", En = "Duplicate Finder", Zh = "重複檔案搜尋", Glyph = "", Keywords = "duplicate hash find dedupe 重複" },
        new() { Tag = "module.disk", En = "Disk Analyser", Zh = "磁碟分析", Glyph = "", Keywords = "disk space treemap analyse folder size 磁碟 空間" },
        new() { Tag = "module.drives", En = "Drives", Zh = "磁碟機", Glyph = "", Keywords = "drive volume format bitlocker 磁碟機" },
        new() { Tag = "module.testdisk", En = "TestDisk / PhotoRec Recovery", Zh = "TestDisk / PhotoRec 資料救援", Glyph = ((char)0xE7BA).ToString(), Keywords = "testdisk photorec recovery carve undelete partition recover data lost deleted 資料救援 救援 復原 還原 分割區 救回 刪除 檔案" },
        new() { Tag = "module.uninstall", En = "App Uninstaller", Zh = "應用程式解除安裝", Glyph = "", Keywords = "uninstall remove app program winget 解除安裝" },
        new() { Tag = "module.windows", En = "Window Manager", Zh = "視窗管理", Glyph = "", Keywords = "window tile cascade always on top 視窗" },
        new() { Tag = "module.keyboard", En = "Keyboard Remapper", Zh = "鍵盤重新對應", Glyph = "", Keywords = "keyboard remap key sharpkeys 鍵盤" },
        new() { Tag = "module.hotkeys", En = "Hotkey & Macro Runner", Zh = "熱鍵與巨集", Glyph = ((char)0xE765).ToString(), Keywords = "hotkey macro shortcut chord registerhotkey send keys autohotkey text expander snippet trigger expand abbreviation 熱鍵 巨集 快捷鍵 文字展開 片語 縮寫" },
        new() { Tag = "module.hosts", En = "Hosts Editor", Zh = "hosts 編輯器", Glyph = "", Keywords = "hosts block domain dns 封鎖" },
        new() { Tag = "module.mouse", En = "Mouse & Pointer", Zh = "滑鼠與指標", Glyph = "", Keywords = "mouse pointer acceleration speed 滑鼠 指標" },
        new() { Tag = "module.recorder", En = "Screen Recorder", Zh = "螢幕錄影", Glyph = "", Keywords = "record screen capture gdigrab 錄影" },
        new() { Tag = "module.capture", En = "Capture Studio", Zh = "擷取工作室", Glyph = ((char)0xE722).ToString(), Keywords = "capture snip screenshot region gif ocr text recognize clipboard 截圖 擷取 區域 文字辨識 認字" },
        new() { Tag = "module.monitor", En = "System Monitor", Zh = "系統監察", Glyph = "", Keywords = "cpu ram memory network task manager priority affinity 監察 工作管理員" },
        new() { Tag = "module.winfetch", En = "System Info (Winfetch)", Zh = "系統資訊", Glyph = ((char)0xE7F4).ToString(), Keywords = "winfetch neofetch fetch system info os host kernel uptime packages shell resolution gpu cpu memory disk ascii logo specs about machine 系統資訊 規格 開機時間 解像度 記憶體 磁碟 顯示卡 標誌" },
        new() { Tag = "module.monitor", En = "System Monitor", Zh = "系統監察", Glyph = "", Keywords = "cpu ram memory network task manager priority affinity btop btop4win resource monitor per-core swap sparkline efficiency 監察 工作管理員 資源監控 每核心" },
        new() { Tag = "module.battery", En = "Battery & Thermal", Zh = "電池與散熱", Glyph = ((char)0xE83E).ToString(), Keywords = "battery thermal temperature wear health cpu gpu fan powercfg batteryreport energy 電池 溫度 散熱 風扇 耗損 健康" },
        new() { Tag = "module.connections", En = "Connections", Zh = "連線", Glyph = "", Keywords = "tcp udp connections netstat tcpview port 連線" },
        new() { Tag = "module.wireshark", En = "Packet Capture", Zh = "封包擷取", Glyph = ((char)0xEDA3).ToString(), Keywords = "wireshark packet capture tshark dumpcap pcap pcapng sniff npcap interface bpf capture filter display filter protocol tcp udp http dns follow stream conversation endpoint statistics 封包 擷取 抓包 嗅探 過濾 協定 統計" },
        new() { Tag = "module.nmap", En = "Nmap Scanner", Zh = "網絡掃描", Glyph = ((char)0xE9D2).ToString(), Keywords = "nmap port scan network security host service os version cidr subnet npcap zenmap nse script ping sweep 掃描 端口 連接埠 網絡 安全 主機 服務 作業系統 子網" },
        new() { Tag = "module.events", En = "Event Viewer", Zh = "事件檢視器", Glyph = "", Keywords = "event log viewer system application 事件 記錄" },
        new() { Tag = "module.mixer", En = "Volume Mixer", Zh = "音量混合器", Glyph = "", Keywords = "volume mixer audio per-app mute 音量 靜音" },
        new() { Tag = "module.contextmenu", En = "Context Menu", Zh = "右鍵選單", Glyph = "", Keywords = "context menu right click verb 右鍵 選單" },
        new() { Tag = "module.taskbar-tweaker", En = "Taskbar Tweaker", Zh = "工作列調校", Glyph = ((char)0xE71D).ToString(), Keywords = "taskbar tweaker 7+ taskbar tweaker windhawk align combine buttons small icons tray system tray multi monitor seconds clock search task view widgets copilot end task start menu explorer 工作列 調校 對齊 合併 系統匣 多螢幕 秒 時鐘 搜尋 開始功能表 結束工作" },
        new() { Tag = "module.nilesoftshell", En = "Nilesoft Shell", Zh = "Nilesoft 右鍵選單", Glyph = ((char)0xE7BA).ToString(), Keywords = "nilesoft shell context menu nss shell.nss register unregister reload theme modern dark snippet template explorer customize right click 右鍵 選單 主題 註冊 設定 範本 片語 客製化" },
        new() { Tag = "module.awake", En = "Awake", Zh = "保持喚醒", Glyph = "", Keywords = "awake keep awake no sleep caffeine 唔瞓 喚醒" },
        new() { Tag = "module.colorpicker", En = "Color Picker", Zh = "螢幕取色", Glyph = "", Keywords = "color picker hex rgb hsl eyedropper 取色 顏色" },
        new() { Tag = "module.pixeleditor", En = "Pixel Editor", Zh = "像素畫編輯器", Glyph = ((char)0xE790).ToString(), Keywords = "pixel editor aseprite sprite pixel art draw paint canvas palette layers frames animation gif png pencil eraser fill bucket eyedropper undo redo 像素 像素畫 精靈 繪圖 畫布 調色盤 圖層 影格 動畫 鉛筆 橡皮 填色 吸色" },
        new() { Tag = "module.envvars", En = "Environment Variables", Zh = "環境變數", Glyph = "", Keywords = "environment variables path user system env 環境變數" },
        new() { Tag = "module.clipboard", En = "Clipboard", Zh = "剪貼簿", Glyph = ((char)0xE77F).ToString(), Keywords = "clipboard history text image file convert win+v qr qrcode qr code plain text paste 剪貼簿 歷史 二維碼 純文字" },
        new() { Tag = "module.packages", En = "Package Manager", Zh = "套件管理", Glyph = ((char)0xECAA).ToString(), Keywords = "winget package install uninstall upgrade update scoop choco chocolatey pip python npm node dotnet tool powershell gallery psgallery cargo rust dependencies unigetui discover bundle export import 套件 安裝 更新 解除安裝 相依 清單" },
        new() { Tag = "module.adb", En = "Android (ADB)", Zh = "Android（ADB）", Glyph = ((char)0xE8EA).ToString(), Keywords = "android adb apk logcat shell screenshot reboot fastboot scrcpy push pull file backup mirror 手機 安卓 鏡像 備份" },
        new() { Tag = "module.fastboot", En = "Fastboot / Flasher", Zh = "Fastboot／刷機", Glyph = ((char)0xE7BA).ToString(), Keywords = "fastboot flash flasher bootloader unlock boot.img factory image sideload ota pixelflasher 刷機 解鎖" },
        new() { Tag = "module.emulator", En = "Android Emulator & SDK", Zh = "Android 模擬器與 SDK", Glyph = ((char)0xE8EA).ToString(), Keywords = "android emulator avd avdmanager sdkmanager virtual device launch wipe cold boot sdk sdkmanager packages platform-tools build-tools ndk license channel system image install uninstall update 套件 平台 授權 模擬器 虛擬裝置 安裝 更新 移除" },
        new() { Tag = "module.vpn", En = "VPN & Mesh", Zh = "VPN 與網狀網", Glyph = ((char)0xE945).ToString(), Keywords = "vpn nordvpn tailscale mesh connect exit node ping 連線 網狀網" },
        new() { Tag = "module.homeassistant", En = "Home Assistant", Zh = "家居助理", Glyph = ((char)0xE80F).ToString(), Keywords = "home assistant ha smart home rest api template scene script light climate thermostat camera notify intent calendar 智能家居 家居助理" },
        new() { Tag = "module.qbittorrent", En = "qBittorrent", Zh = "種子下載", Glyph = ((char)0xE896).ToString(), Keywords = "qbittorrent torrent torrents magnet bittorrent download seed leech tracker peer category tag webui web api speed limit 種子 磁力 下載 做種 追蹤器 分類 標籤 速度限制" },
        new() { Tag = "module.comms", En = "Communications", Zh = "通訊", Glyph = ((char)0xE8BD).ToString(), Keywords = "communications mail email outlook mailto draft attach teams meeting call discord telegram slack phone link tel sms deep link 通訊 信件 電郵 草稿 會議 電話" },
        new() { Tag = "module.configbackup", En = "Config & Backup", Zh = "設定與備份", Glyph = ((char)0xE8F7).ToString(), Keywords = "config backup snapshot restore export import bundle zip git schedule mirror reg winget integrity secrets ssh api key encrypt aes password 設定 備份 快照 還原 匯出 匯入 排程 鏡像 加密 密鑰 機密" },
        new() { Tag = "module.configbackup", En = "Config & Backup", Zh = "設定與備份", Glyph = ((char)0xE8F7).ToString(), Keywords = "config backup snapshot restore export import bundle zip git schedule mirror reg winget integrity auto-sync interval push remote 自動同步 設定 備份 快照 還原 匯出 匯入 排程 鏡像" },
        new() { Tag = "module.worldmonitor", En = "World Monitor", Zh = "世界監察", Glyph = ((char)0xE909).ToString(), Keywords = "world monitor worldmonitor news geopolitics finance commodity energy happy instability index intelligence dashboard globe map webview variant 世界 監察 新聞 地緣政治 金融 商品 能源 情報 儀表板 地球 不穩定指數" },
        new() { Tag = "module.configbackup", En = "Config & Backup", Zh = "設定與備份", Glyph = ((char)0xE8F7).ToString(), Keywords = "config backup snapshot restore export import bundle zip git schedule mirror reg winget integrity 設定 備份 快照 還原 匯出 匯入 排程 鏡像" },
        new() { Tag = "module.native", En = "Native Utilities", Zh = "原生工具", Glyph = ((char)0xE950).ToString(), Keywords = "wifi password saved nearby scan smb shares sessions brightness ddc certificate users logoff disconnect gpu disk counters process modules bluetooth pinvoke wlan 原生 密碼 共享 亮度 憑證 藍牙 模組" },
        new() { Tag = "module.powertoys", En = "PowerToys Extras", Zh = "PowerToys 額外工具", Glyph = ((char)0xE945).ToString(), Keywords = "powertoys image resizer ocr text extractor always on top topmost paste plain text 圖片縮放 文字擷取 置頂 純文字" },
        new() { Tag = "module.wslvm", En = "WSL & VM Launcher", Zh = "WSL 與 VM 啟動器", Glyph = ((char)0xEC7A).ToString(), Keywords = "wsl linux distro ubuntu debian windows sandbox wsb virtual machine vm hyper-v export import 子系統 沙盒 虛擬機" },
        new() { Tag = "module.virtualbox", En = "VirtualBox Manager", Zh = "VirtualBox 管理", Glyph = ((char)0xEC7A).ToString(), Keywords = "virtualbox vbox vboxmanage vm virtual machine snapshot clone ova headless oracle 虛擬機 虛擬機器 快照 複製 匯入 匯出" },
        new() { Tag = "module.fonts", En = "Font Manager", Zh = "字型管理", Glyph = ((char)0xE8D2).ToString(), Keywords = "font fonts install preview uninstall ttf otf typeface typography 字型 字款 安裝 預覽 移除" },
        new() { Tag = "module.onedrive", En = "OneDrive", Zh = "OneDrive", Glyph = ((char)0xE753).ToString(), Keywords = "onedrive files on demand pin dehydrate online only cloud free space storage sense sync 雲端 釘選 脫水 釋放空間 同步 隨選" },
        new() { Tag = "module.timeunit", En = "Time & Unit Tools", Zh = "時間與單位工具", Glyph = ((char)0xE823).ToString(), Keywords = "time zone timezone world clock converter convert unit length mass temperature 時間 時區 世界時鐘 換算 單位" },
        new() { Tag = "module.settingshub", En = "Settings & Control Panel", Zh = "設定與控制台", Glyph = ((char)0xE713).ToString(), Keywords = "settings control panel ms-settings applet cpl launcher open page 設定 控制台 啟動器 面板" },
        new() { Tag = "module.imaging", En = "Imaging & Game Tools", Zh = "燒錄與遊戲工具", Glyph = ((char)0xE7F4).ToString(), Keywords = "raspberry pi imager sd card flash image write boot ssh wifi minecraft world downloader proxy jar 樹莓派 燒錄 映像 我的世界 下載" },
        new() { Tag = "module.amulet", En = "Minecraft World Editor (Amulet)", Zh = "Minecraft 世界編輯器（Amulet）", Glyph = ((char)0xE7FC).ToString(), Keywords = "amulet minecraft world editor map editor java bedrock level dat nbt python wxpython launch backup saves chunk dimension 世界 編輯器 我的世界 地圖 備份 存檔 維度" },
        new() { Tag = "module.minecraftserver", En = "Minecraft Server", Zh = "Minecraft 伺服器", Glyph = ((char)0xE7FC).ToString(), Keywords = "minecraft server paper spigot papermc buildtools eula server.properties plugin plugins luckperms essentialsx viaversion worldedit console rcon start.bat aikar gradle maven jar 伺服器 外掛 主控台 我的世界" },
        new() { Tag = "module.voice", En = "Voice & Read-Aloud", Zh = "語音朗讀", Glyph = ((char)0xE767).ToString(), Keywords = "voice tts text to speech read aloud speak narrator wav export sapi 語音 朗讀 文字轉語音 讀出" },
        new() { Tag = "module.viaproxy", En = "ViaProxy", Zh = "Minecraft 版本代理", Glyph = ((char)0xE990).ToString(), Keywords = "viaproxy minecraft version proxy viaversion viabackwards viarewind protocol translate bridge server jar java cli bind target auth offline online mc 我的世界 版本 代理 協定 轉換 伺服器" },
        new() { Tag = "module.aiagents", En = "AI Agents", Zh = "AI 代理", Glyph = ((char)0xE99A).ToString(), Keywords = "ai agent claude code codex opencode pi openclaw hermes coding agent terminal cli install launch api key 代理 編程 安裝 啟動" },
        new() { Tag = "module.resume", En = "Resume Writer", Zh = "履歷與求職信寫手", Glyph = ((char)0xE8A5).ToString(), Keywords = "resume cv cover letter job application tailor ai writer generate export base history docx pdf markdown 履歷 求職信 應徵 工作 職位 自我推薦 度身 生成 匯出" },
        new() { Tag = "module.cloudflare", En = "Cloudflare & Tunnel", Zh = "Cloudflare 與 Tunnel", Glyph = ((char)0xE753).ToString(), Keywords = "cloudflare cloudflared tunnel quick tunnel trycloudflare access warp dns over https doh zero trust route ingress 隧道 加密 連線" },
        new() { Tag = "module.weblogin", En = "In-App Login", Zh = "內置登入", Glyph = ((char)0xE77B).ToString(), Keywords = "login sign in signin oauth webview2 web view browser embedded auth authentication token cookie session redirect callback github cloudflare openai anthropic bitwarden account credentials 登入 登錄 內置 瀏覽器 認證 帳戶 憑證 權杖 重新導向" },
        new() { Tag = "module.ssh", En = "SSH Toolset", Zh = "SSH 工具", Glyph = ((char)0xE756).ToString(), Keywords = "ssh sftp scp terminal shell remote profile key keygen ed25519 rsa passwordless deploy authorized_keys known hosts openssh dpapi 終端機 遠端 金鑰 免密碼 部署 連線 上載 下載" },
        new() { Tag = "module.vault-volumes", En = "WinForge Vault", Zh = "WinForge 保險庫", Glyph = ((char)0xE72E).ToString(), Keywords = "vault volume container encrypt encrypted disk encryption mount dismount unmount drive letter password keyfile pim benchmark aes serpent twofish on the fly cryptography 保險庫 加密 容器 磁碟 掛載 卸載 密碼 鎖匙檔 磁碟區" },
        new() { Tag = "module.mail", En = "Mail", Zh = "電郵", Glyph = ((char)0xE715).ToString(), Keywords = "mail email imap smtp thunderbird inbox compose reply forward attachment oauth gmail outlook icloud yahoo account folder message read send 電郵 郵件 收件匣 撰寫 回覆 轉寄 附件 帳戶 資料夾 訊息" },
        new() { Tag = "module.packer", En = "Packer (Image Builder)", Zh = "Packer（映像建置器）", Glyph = ((char)0xE7B8).ToString(), Keywords = "packer hashicorp image builder template hcl pkr.hcl json init validate fmt format build inspect plugin plugins var var-file variables provisioner builder source qemu docker aws azure vsphere amazon ami vm machine devops 映像 範本 建置 變數 插件" },
        new() { Tag = "module.webcloner", En = "Website Cloner", Zh = "網站複製器", Glyph = ((char)0xE774).ToString(), Keywords = "website cloner clone copy site web page scrape download fetch assets html css js mirror rebuild reverse engineer ai agent webview2 design tokens 網站 複製 抓取 下載 鏡像 重建 設計符記" },
        new() { Tag = "module.windhawk", En = "Windhawk Mods", Zh = "Windhawk 模組", Glyph = ((char)0xE945).ToString(), Keywords = "windhawk mod mods customize taskbar height icon size clock start menu styler explorer rounded corners classic taskbar aero tray injection ramensoftware 模組 自訂 工作列 時鐘 開始功能表 圓角 注入" },
        new() { Tag = "module.rainmeter", En = "Rainmeter Widgets", Zh = "Rainmeter 桌面小工具", Glyph = ((char)0xE7F4).ToString(), Keywords = "rainmeter skin skins widget widgets desktop gadget bang activate deactivate toggle refresh hide show rmskin skininstaller layout illustro clock cpu monitor personalization 桌面 小工具 皮膚 桌面美化 個人化 時鐘 監察" },
        new() { Tag = "module.pgadmin", En = "Postgres Tool", Zh = "Postgres 工具 / pgAdmin", Glyph = ((char)0xE94D).ToString(), Keywords = "postgres postgresql pgadmin sql database query npgsql connection schema table view server psql 資料庫 數據庫 查詢 表 檢視 結構描述 連線" },
        new() { Tag = "module.filezilla", En = "FTP / SFTP", Zh = "FTP／SFTP 檔案傳輸", Glyph = ((char)0xE8B7).ToString(), Keywords = "ftp sftp ftps filezilla file transfer client site manager upload download dual pane transfer queue resume tls ssh private key dpapi 檔案傳輸 上載 下載 站台 佇列 續傳 私鑰" },
        new() { Tag = "module.ollama", En = "Ollama", Zh = "本地大模型", Glyph = ((char)0xE99A).ToString(), Keywords = "ollama llm local ai model chat gguf llama mistral qwen gemma phi deepseek pull serve tags running ps temperature top_p num_ctx streaming 本地 模型 聊天 人工智能 下載 大模型" },
    };

    public static IEnumerable<ModuleInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        var q = query.Trim().ToLowerInvariant();
        return All.Where(m => m.Haystack.Contains(q));
    }
}
