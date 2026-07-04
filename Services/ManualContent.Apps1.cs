using System.Collections.Generic;

namespace WinForge.Services;

// 程式、開發與雲端章節（第一部分）· Apps, dev & cloud section (part 1).
public static partial class ManualContent
{
    private static List<ManualEntry> AppsEntriesPart1() => new()
    {
        new ManualEntry
        {
            Tag = "module.packages", Glyph = "",
            TitleEn = "Package Manager", TitleZh = "套件管理",
            SummaryEn = "Search, install, update and uninstall software across winget, Scoop, Chocolatey, pip, npm and more from one list.",
            SummaryZh = "喺一個清單度跨 winget、Scoop、Chocolatey、pip、npm 等等搵、安裝、更新同解除安裝軟件。",
            StepsEn = new[]
            {
                "Pick a package manager from the filter bar at the top (winget, Scoop, Choco, pip, npm…).",
                "Type a name in the search box and run the search.",
                "Tick one or more results, then use the batch bar to Install, Update or Uninstall.",
                "Use Export to save your selection as a bundle, or open the Terminal button to watch the raw command run.",
            },
            StepsZh = new[]
            {
                "喺頂部嘅篩選列揀一個套件管理員（winget、Scoop、Choco、pip、npm⋯）。",
                "喺搜尋框打個名，然後撳搜尋。",
                "剔選一個或者幾個結果，再用批次列嘅安裝、更新或者解除安裝。",
                "撳匯出可以將你揀咗嘅嘢儲成一個 bundle，撳 Terminal 掣就睇到真正嘅指令點行。",
            },
            TipEn = "If a manager like Scoop or Chocolatey is missing, WinForge offers to bootstrap it for you on first use.",
            TipZh = "如果 Scoop 或者 Chocolatey 未裝，第一次用嗰陣 WinForge 會問你幫你裝返。",
            Keywords = "winget scoop choco chocolatey pip npm dotnet cargo upgrade bundle export 套件 安裝 更新 批次 多選",
        },
        new ManualEntry
        {
            Tag = "module.adb", Glyph = "",
            TitleEn = "Android (ADB)", TitleZh = "Android（ADB）",
            SummaryEn = "Control an Android phone over USB or Wi-Fi — run shell commands, push/pull files, back up APKs, stream logcat and mirror the screen.",
            SummaryZh = "用 USB 或者 Wi-Fi 控制 Android 手機 — 行 shell 指令、推送／拉取檔案、備份 APK、睇 logcat 同鏡像螢幕。",
            StepsEn = new[]
            {
                "Plug in your phone (with USB debugging on) or type its IP to connect wirelessly, then Refresh and pick the device.",
                "On the Console tab, take a screenshot, list packages, reboot, or type a shell command and Run it.",
                "Use the Files tab to push and pull files, and the APK tab to back up installed apps.",
                "Open the Live Logcat tab to stream logs, or the Mirror tab to start scrcpy screen mirroring.",
            },
            StepsZh = new[]
            {
                "插手機（開咗 USB 偵錯）或者打個 IP 用無線連線，撳 Refresh 再揀個裝置。",
                "喺 Console 分頁可以截圖、列出 packages、重啟，或者打個 shell 指令撳 Run。",
                "用 Files 分頁推送同拉取檔案，用 APK 分頁備份已裝嘅 app。",
                "撳 Live Logcat 分頁睇即時 log，或者撳 Mirror 分頁用 scrcpy 鏡像螢幕。",
            },
            TipEn = "First use installs the Android SDK platform-tools (via winget); the Mirror tab needs scrcpy.",
            TipZh = "第一次用會裝 Android SDK platform-tools（用 winget）；Mirror 分頁要 scrcpy。",
            Keywords = "adb scrcpy logcat apk shell push pull screenshot reboot 手機 安卓 鏡像 備份",
        },
        new ManualEntry
        {
            Tag = "module.fastboot", Glyph = "",
            TitleEn = "Fastboot / Flasher", TitleZh = "Fastboot／刷機",
            SummaryEn = "Flash boot images, unlock the bootloader, sideload OTAs and factory-reset an Android device in fastboot mode.",
            SummaryZh = "喺 fastboot 模式下刷 boot image、解鎖 bootloader、sideload OTA 同回復原廠。",
            StepsEn = new[]
            {
                "Boot the phone into fastboot/bootloader mode, connect USB, then Refresh and select it.",
                "Keep the Dry-run checkbox on first to preview commands without touching the device.",
                "Use Status to confirm the connection, then pick an action: Unlock, Flash Boot, Boot Image, Sideload or Factory Reset.",
                "Watch the console output, then Reboot when you are done.",
            },
            StepsZh = new[]
            {
                "將手機開去 fastboot／bootloader 模式，插 USB，撳 Refresh 再揀佢。",
                "頭先剔住 Dry-run，咁就可以預覽指令而唔會郁到部機。",
                "撳 Status 確認連線，再揀動作：Unlock、Flash Boot、Boot Image、Sideload 或者 Factory Reset。",
                "睇住 console 輸出，搞掂就撳 Reboot。",
            },
            TipEn = "Unlocking the bootloader wipes the phone. Flashing the wrong image can brick it — read each warning before you confirm.",
            TipZh = "解鎖 bootloader 會清空部機，刷錯 image 可能會變磚 — 撳確認前睇清楚每個警告。",
            Keywords = "fastboot flash bootloader unlock boot.img sideload ota factory reset 刷機 解鎖 變磚",
        },
        new ManualEntry
        {
            Tag = "module.emulator", Glyph = "",
            TitleEn = "Android Emulator & SDK", TitleZh = "Android 模擬器與 SDK",
            SummaryEn = "Create and launch Android virtual devices and manage SDK packages, platforms and system images.",
            SummaryZh = "建立同啟動 Android 虛擬裝置，同管理 SDK 套件、平台同 system image。",
            StepsEn = new[]
            {
                "On the AVD tab, Refresh the list, then Create a new virtual device.",
                "Select an AVD and Launch it (tick Cold Boot to start fresh); use Stop, Wipe or Delete as needed.",
                "On the SDK Packages tab, click Accept Licenses, then Quick Install or pick packages and Install Selected.",
                "Use Update All to bring platform-tools, build-tools and system images up to date.",
            },
            StepsZh = new[]
            {
                "喺 AVD 分頁撳 Refresh，再撳 Create 整個新虛擬裝置。",
                "揀個 AVD 撳 Launch 開佢（剔 Cold Boot 就由頭開始）；需要嘅話用 Stop、Wipe 或者 Delete。",
                "喺 SDK Packages 分頁撳 Accept Licenses，再撳 Quick Install，或者揀套件撳 Install Selected。",
                "撳 Update All 將 platform-tools、build-tools 同 system image 全部更新。",
            },
            TipEn = "First use installs the Android SDK (via winget); accepting the SDK licenses is required before packages will download.",
            TipZh = "第一次用會裝 Android SDK（用 winget）；要先 Accept Licenses 套件先會下載。",
            Keywords = "android emulator avd sdkmanager avdmanager system image platform build-tools cold boot 模擬器 虛擬裝置 授權 平台",
        },
        new ManualEntry
        {
            Tag = "module.vpn", Glyph = "",
            TitleEn = "VPN & Mesh", TitleZh = "VPN 與網狀網",
            SummaryEn = "Drive NordVPN, Tailscale, WireGuard and built-in Windows VPN connections, including meshnet and exit nodes.",
            SummaryZh = "操控 NordVPN、Tailscale、WireGuard 同內建 Windows VPN 連線，包埋 meshnet 同 exit node。",
            StepsEn = new[]
            {
                "In the NordVPN card, pick a country and Connect, or toggle Meshnet and view Peers.",
                "In the Tailscale card, click Up to join the tailnet, check Status/IP, and set an Exit node.",
                "In the Windows VPN card, fill in Name, Server and Tunnel type, click Add, then Connect from the list.",
                "In the WireGuard card, Import a .conf tunnel, then activate it from the list.",
            },
            StepsZh = new[]
            {
                "喺 NordVPN 卡揀個國家撳 Connect，或者開 Meshnet 同睇 Peers。",
                "喺 Tailscale 卡撳 Up 加入 tailnet，睇 Status／IP，再設定 Exit node。",
                "喺 Windows VPN 卡填 Name、Server 同 Tunnel type，撳 Add，再喺清單度 Connect。",
                "喺 WireGuard 卡撳 Import 入一個 .conf tunnel，再喺清單度啟用佢。",
            },
            TipEn = "Each provider must already be installed (NordVPN, Tailscale, WireGuard); WinForge just drives their command line.",
            TipZh = "各個工具要事先裝好（NordVPN、Tailscale、WireGuard）；WinForge 淨係幫你行佢哋嘅指令。",
            Keywords = "vpn nordvpn tailscale wireguard meshnet exit node serve funnel 連線 網狀網",
        },
        new ManualEntry
        {
            Tag = "module.qbittorrent", Glyph = "",
            TitleEn = "qBittorrent", TitleZh = "種子下載",
            SummaryEn = "Manage torrents through qBittorrent's Web API — add magnets, control downloads and set speed limits.",
            SummaryZh = "透過 qBittorrent 嘅 Web API 管理種子 — 加磁力連結、控制下載同設定速度限制。",
            StepsEn = new[]
            {
                "Open the Connection section, enter the Web UI host, port, username and password, then Connect.",
                "Click Add Magnet or Add File to start a download.",
                "Select torrents and use Resume, Pause, Delete, Recheck or assign a Category/Tag.",
                "Use the footer's Speed Limits or the Alt-speed toggle to throttle DL/UL.",
            },
            StepsZh = new[]
            {
                "打開 Connection 區，填 Web UI 嘅 host、port、用戶名同密碼，再撳 Connect。",
                "撳 Add Magnet 或者 Add File 開始下載。",
                "揀啲種子，用 Resume、Pause、Delete、Recheck，或者畀佢一個 Category／Tag。",
                "用底部嘅 Speed Limits 或者 Alt-speed 開關去限制 DL／UL 速度。",
            },
            TipEn = "First use can install qBittorrent (via winget); enable its Web UI in qBittorrent's options so WinForge can connect.",
            TipZh = "第一次用可以裝 qBittorrent（用 winget）；記住喺 qBittorrent 設定度開 Web UI，WinForge 先連到。",
            Keywords = "qbittorrent torrent magnet webui seed leech category tag speed limit 種子 磁力 下載 做種 速度限制",
        },
        new ManualEntry
        {
            Tag = "module.rustdesk", Glyph = "",
            TitleEn = "RustDesk", TitleZh = "遠端桌面",
            SummaryEn = "Remote-control another PC with RustDesk — share your ID, set a permanent password, save peers and point at your own server.",
            SummaryZh = "用 RustDesk 遙距控制另一部電腦 — 分享你嘅 ID、設定永久密碼、儲存 peer 同指向自己嘅 server。",
            StepsEn = new[]
            {
                "Read your local ID in the This PC card, and optionally set a permanent password.",
                "In Quick Connect, type the remote peer's ID and click Connect (tick view-only to just watch).",
                "Click Save peer to keep an ID in the Saved Peers list for one-click reconnects.",
                "Under Server Settings, point at a self-hosted ID/relay server if you run your own.",
            },
            StepsZh = new[]
            {
                "喺 This PC 卡睇你本機嘅 ID，需要嘅話設定個永久密碼。",
                "喺 Quick Connect 打對方嘅 peer ID 撳 Connect（剔 view-only 就淨係睇）。",
                "撳 Save peer 將個 ID 加入 Saved Peers 清單，下次一撳就連返。",
                "喺 Server Settings 度指向你自己 host 嘅 ID／relay server（如果你有自架）。",
            },
            TipEn = "First use installs RustDesk (via winget); set a permanent password for unattended access.",
            TipZh = "第一次用會裝 RustDesk（用 winget）；想無人睇住都連到就設定個永久密碼。",
            Keywords = "rustdesk remote desktop id password peer relay self-hosted unattended 遠端桌面 遙距桌面 遠端控制",
        },
        new ManualEntry
        {
            Tag = "module.homeassistant", Glyph = "",
            TitleEn = "Home Assistant", TitleZh = "家居助理",
            SummaryEn = "Control a Home Assistant smart home over its REST API — toggle lights, run scenes and scripts, send notifications and more.",
            SummaryZh = "透過 REST API 控制 Home Assistant 智能家居 — 開關燈、跑 scene 同 script、發通知等等。",
            StepsEn = new[]
            {
                "In the configuration row, enter your HA URL and a long-lived access token, then Test and Save.",
                "Use the States tab to inspect entities, or the Lights/Climate tab to adjust brightness and temperature.",
                "Trigger automations from the Scenes/Scripts/Events tab, and push messages from the Notify tab.",
                "Check the Template tab to test templating, or the Error Log tab when something misbehaves.",
            },
            StepsZh = new[]
            {
                "喺設定列填你嘅 HA URL 同一個 long-lived access token，撳 Test 再 Save。",
                "用 States 分頁睇 entity，或者用 Lights／Climate 分頁調光暗同溫度。",
                "喺 Scenes／Scripts／Events 分頁觸發自動化，喺 Notify 分頁推送訊息。",
                "撳 Template 分頁試 templating，出問題就睇 Error Log 分頁。",
            },
            TipEn = "You need Home Assistant already running on your network plus a long-lived access token from your HA profile.",
            TipZh = "你要喺網絡上已經有 Home Assistant 運行緊，再喺 HA 個人檔案攞個 long-lived access token。",
            Keywords = "home assistant ha rest api token scene script light climate notify template 智能家居 家居助理",
        },
        new ManualEntry
        {
            Tag = "module.comms", Glyph = "",
            TitleEn = "Communications", TitleZh = "通訊",
            SummaryEn = "Compose mail and fire off deep links into Discord, Teams, Telegram, Slack and Phone Link from one panel.",
            SummaryZh = "喺一個面板度寫信，同埋發 deep link 去 Discord、Teams、Telegram、Slack 同 Phone Link。",
            StepsEn = new[]
            {
                "In the Mail section, fill To/Subject/Body and click mailto or Outlook to open a draft.",
                "In the Discord, Teams, Telegram or Slack section, fill the IDs/fields and click the open button to jump there.",
                "Use the Teams section to start a chat, a call or schedule a meeting.",
                "Use Phone Link to place a call or send an SMS from your linked phone.",
            },
            StepsZh = new[]
            {
                "喺 Mail 區填 To／Subject／Body，撳 mailto 或者 Outlook 開個草稿。",
                "喺 Discord、Teams、Telegram 或者 Slack 區填 ID／欄位，撳開啟掣就跳過去。",
                "用 Teams 區開始傾偈、打電話或者預約會議。",
                "用 Phone Link 透過你連咗線嘅手機打電話或者發 SMS。",
            },
            TipEn = "These open your existing apps via deep links — the target app (Teams, Discord, Phone Link…) must already be installed.",
            TipZh = "呢啲係用 deep link 開你已有嘅 app — 對應嘅 app（Teams、Discord、Phone Link⋯）要事先裝好。",
            Keywords = "communications mail mailto outlook discord teams telegram slack phone link sms meeting 通訊 電郵 草稿 會議 電話",
        },
        new ManualEntry
        {
            Tag = "module.mail", Glyph = "",
            TitleEn = "Mail", TitleZh = "電郵",
            SummaryEn = "A built-in three-pane IMAP/SMTP email client to read, compose, reply and manage multiple accounts.",
            SummaryZh = "內建嘅三欄式 IMAP／SMTP 電郵客戶端，畀你睇信、寫信、回覆同管理多個帳戶。",
            StepsEn = new[]
            {
                "Click Add account and enter your IMAP/SMTP details (or sign in for Gmail/Outlook).",
                "Pick a folder on the left, then a message in the middle to read it on the right.",
                "Use Compose to write a new mail, or Reply / Reply-all / Forward on an open message.",
                "Switch accounts from the dropdown, and use the search box to find messages.",
            },
            StepsZh = new[]
            {
                "撳 Add account 填你嘅 IMAP／SMTP 資料（或者登入 Gmail／Outlook）。",
                "喺左邊揀個資料夾，喺中間揀封信，右邊就睇到內容。",
                "撳 Compose 寫新信，或者喺開咗嘅信度撳 Reply／Reply-all／Forward。",
                "喺下拉選單切換帳戶，用搜尋框搵信。",
            },
            TipEn = "Prefer a full desktop client? The toolbar's Thunderbird button launches (and on first use installs, via winget) Thunderbird.",
            TipZh = "想用足料嘅桌面客戶端？工具列嘅 Thunderbird 掣會開（第一次用會用 winget 裝）Thunderbird。",
            Keywords = "mail email imap smtp thunderbird inbox compose reply forward attachment account 電郵 郵件 收件匣 撰寫 回覆 附件",
        },
        new ManualEntry
        {
            Tag = "module.wslvm", Glyph = "",
            TitleEn = "WSL & VM Launcher", TitleZh = "WSL 與 VM 啟動器",
            SummaryEn = "Manage WSL Linux distros and spin up a configured Windows Sandbox in a couple of clicks.",
            SummaryZh = "管理 WSL Linux distro，同埋幾撳就開一個設定好嘅 Windows Sandbox。",
            StepsEn = new[]
            {
                "In WSL Distro Manager, pick a distro from the dropdown and Install, or Refresh the installed list.",
                "Use a distro's menu to open an embedded terminal, set it as default, export, terminate or unregister it.",
                "In Windows Sandbox, map a shared folder and toggle networking, vGPU and clipboard options.",
                "Preview the generated .wsb, then Launch the sandbox (or Save the .wsb for later).",
            },
            StepsZh = new[]
            {
                "喺 WSL Distro Manager 喺下拉選單揀個 distro 撳 Install，或者 Refresh 已裝清單。",
                "用 distro 嘅選單開內嵌終端機、設做預設、匯出、終止或者 unregister。",
                "喺 Windows Sandbox 對應一個共享資料夾，開關 networking、vGPU 同 clipboard。",
                "預覽生成嘅 .wsb，再 Launch 個 sandbox（或者 Save 個 .wsb 留返後用）。",
            },
            TipEn = "WSL and Windows Sandbox are optional Windows features — enable them in Windows Features if they're not available.",
            TipZh = "WSL 同 Windows Sandbox 係可選嘅 Windows 功能 — 如果用唔到，喺「Windows 功能」度啟用佢哋。",
            Keywords = "wsl linux distro ubuntu debian windows sandbox wsb shared folder export import 子系統 沙盒 虛擬機",
        },
        new ManualEntry
        {
            Tag = "module.virtualbox", Glyph = "",
            TitleEn = "VirtualBox Manager", TitleZh = "VirtualBox 管理",
            SummaryEn = "List, start and manage Oracle VirtualBox VMs — snapshots, CPU/RAM tweaks, cloning and OVA export.",
            SummaryZh = "列出、啟動同管理 Oracle VirtualBox 虛擬機 — 快照、調 CPU／RAM、複製同匯出 OVA。",
            StepsEn = new[]
            {
                "Refresh the VM list, or click Create VM / Import to add one.",
                "Use a VM's menu to Start (GUI or headless), Pause, Save state, ACPI shutdown, Power off or Reset.",
                "Select a VM to expand its detail card — adjust CPU/RAM (while powered off) and apply.",
                "Take, restore or delete Snapshots, and Clone or Export OVA from the same menu.",
            },
            StepsZh = new[]
            {
                "撳 Refresh 更新 VM 清單，或者撳 Create VM／Import 加一部。",
                "用 VM 嘅選單去 Start（GUI 或者 headless）、Pause、Save state、ACPI shutdown、Power off 或者 Reset。",
                "揀一部 VM 展開佢嘅詳情卡 — 喺關機狀態下調 CPU／RAM 再 apply。",
                "喺同一個選單 take／restore／delete 快照，同 Clone 或者 Export OVA。",
            },
            TipEn = "First use installs VirtualBox (via winget); CPU/RAM can only be changed while the VM is powered off.",
            TipZh = "第一次用會裝 VirtualBox（用 winget）；CPU／RAM 淨係喺 VM 關咗機嗰陣先改到。",
            Keywords = "virtualbox vbox vboxmanage vm snapshot clone ova headless oracle 虛擬機 快照 複製 匯出",
        },
        new ManualEntry
        {
            Tag = "module.terminal", Glyph = "",
            TitleEn = "Windows Terminal", TitleZh = "Windows 終端機",
            SummaryEn = "Edit Windows Terminal profiles and run an embedded shell right inside WinForge.",
            SummaryZh = "編輯 Windows Terminal 設定檔，同埋喺 WinForge 入面直接行一個內嵌 shell。",
            StepsEn = new[]
            {
                "On the Profiles tab, select a profile, edit its command, directory and icon, then Save.",
                "Use Set default, Duplicate or Delete to manage profiles, or Launch the profile in Terminal.",
                "On the Embedded Terminal tab, pick a shell and click Start Terminal to run it inside the app.",
                "Use Clear or Stop Terminal to reset the embedded session.",
            },
            StepsZh = new[]
            {
                "喺 Profiles 分頁揀個設定檔，改佢嘅指令、目錄同圖示，再撳 Save。",
                "用 Set default、Duplicate 或者 Delete 管理設定檔，或者 Launch 個設定檔去 Terminal。",
                "喺 Embedded Terminal 分頁揀個 shell，撳 Start Terminal 喺 app 入面行。",
                "用 Clear 或者 Stop Terminal 重設個內嵌工作階段。",
            },
            TipEn = "First use installs Windows Terminal (via winget) and reads its settings.json so your real profiles show up here.",
            TipZh = "第一次用會裝 Windows Terminal（用 winget），仲會讀佢嘅 settings.json，所以你真正嘅設定檔會喺度顯示。",
            Keywords = "windows terminal wt settings.json profile conpty embedded shell pwsh cmd default duplicate 終端機 設定檔 內嵌 殼層 預設",
        },
        new ManualEntry
        {
            Tag = "module.uninstall", Glyph = "",
            TitleEn = "App Uninstaller", TitleZh = "應用程式解除安裝",
            SummaryEn = "Find and cleanly remove installed programs from one searchable list.",
            SummaryZh = "喺一個可搜尋嘅清單度搵到同乾淨咁移除已安裝嘅程式。",
            StepsEn = new[]
            {
                "Type in the search box to filter the list of installed apps.",
                "Find the app you want to remove.",
                "Click its Uninstall button and confirm.",
                "Click Refresh to update the list after removing something.",
            },
            StepsZh = new[]
            {
                "喺搜尋框打字，篩選已裝嘅 app 清單。",
                "搵到你想移除嘅 app。",
                "撳佢嘅 Uninstall 掣再確認。",
                "移除完之後撳 Refresh 更新清單。",
            },
            Keywords = "uninstall remove app program winget bloatware 解除安裝 移除 程式",
        },
        new ManualEntry
        {
            Tag = "module.imaging", Glyph = "",
            TitleEn = "Imaging & Game Tools", TitleZh = "燒錄與遊戲工具",
            SummaryEn = "Flash SD cards and bootable USBs (Raspberry Pi Imager / Rufus) and download Minecraft worlds.",
            SummaryZh = "燒錄 SD 卡同開機 USB（Raspberry Pi Imager／Rufus），同下載 Minecraft 世界。",
            StepsEn = new[]
            {
                "On the Raspberry Pi tab, pick an image, choose the disk, optionally enable SSH/Wi-Fi, then Write.",
                "On the USB Imager tab, pick an ISO, choose the USB disk, tick Verify, then Write (or launch Rufus).",
                "On the Minecraft tab, locate the repo and Build the jar to download worlds.",
                "Double-check the target disk before writing — the wrong one will be erased.",
            },
            StepsZh = new[]
            {
                "喺 Raspberry Pi 分頁揀個 image、揀個 disk，需要就開 SSH／Wi-Fi，再撳 Write。",
                "喺 USB Imager 分頁揀個 ISO、揀個 USB disk、剔 Verify，再撳 Write（或者開 Rufus）。",
                "喺 Minecraft 分頁定位個 repo，Build 個 jar 去下載世界。",
                "燒之前再三確認個目標 disk — 揀錯會被清空。",
            },
            TipEn = "The USB Imager can install Rufus (via winget) on first use; writing an image erases the whole target disk.",
            TipZh = "USB Imager 第一次用可以用 winget 裝 Rufus；燒 image 會清空成個目標 disk。",
            Keywords = "raspberry pi imager rufus usb bootable iso sd card flash write verify minecraft world 開機 USB 啟動碟 樹莓派 燒錄 映像",
        },
        new ManualEntry
        {
            Tag = "module.amulet", Glyph = "",
            TitleEn = "Minecraft World Editor (Amulet)", TitleZh = "Minecraft 世界編輯器（Amulet）",
            SummaryEn = "Launch the Amulet map editor to open, edit and back up Minecraft Java and Bedrock worlds.",
            SummaryZh = "啟動 Amulet 地圖編輯器，開、編輯同備份 Minecraft Java 同 Bedrock 世界。",
            StepsEn = new[]
            {
                "Click Pick world to choose a save folder (or Open saves to browse your worlds).",
                "Click Backup to make a safe copy before editing.",
                "Click Launch to open the world in the Amulet editor; use Stop to close it.",
                "Watch the log card for progress and any messages.",
            },
            StepsZh = new[]
            {
                "撳 Pick world 揀個存檔資料夾（或者 Open saves 瀏覽你啲世界）。",
                "編輯之前撳 Backup 整個安全副本。",
                "撳 Launch 喺 Amulet 編輯器度開個世界；撳 Stop 關閉佢。",
                "睇住 log 卡嘅進度同訊息。",
            },
            TipEn = "First use installs Python (via winget) and unpacks Amulet — always back up a world before editing it.",
            TipZh = "第一次用會用 winget 裝 Python 同解開 Amulet — 編輯前記住要備份個世界。",
            Keywords = "amulet minecraft world editor map java bedrock nbt python backup saves chunk 世界 編輯器 我的世界 地圖 備份 存檔",
        },
        new ManualEntry
        {
            Tag = "module.viaproxy", Glyph = "",
            TitleEn = "ViaProxy", TitleZh = "Minecraft 版本代理",
            SummaryEn = "Run ViaProxy so any Minecraft client version can join a server on a different protocol version.",
            SummaryZh = "行 ViaProxy，等任何版本嘅 Minecraft 客戶端都連到唔同協定版本嘅伺服器。",
            StepsEn = new[]
            {
                "Click Download to fetch the latest ViaProxy jar (or Pick jar to use your own).",
                "Fill in the target host, port and the Minecraft version you want to bridge to.",
                "Set the bind host/port and online-mode toggle, then click Start.",
                "Copy the local address into your Minecraft client and watch the log pane.",
            },
            StepsZh = new[]
            {
                "撳 Download 攞最新嘅 ViaProxy jar（或者 Pick jar 用你自己嗰個）。",
                "填目標 host、port，同你想橋接去嘅 Minecraft 版本。",
                "設定 bind host／port 同 online-mode 開關，再撳 Start。",
                "將個本機位址 copy 落你嘅 Minecraft 客戶端，睇住 log 面板。",
            },
            TipEn = "First use installs Java (via winget) — needed to run the ViaProxy jar.",
            TipZh = "第一次用會用 winget 裝 Java — 行 ViaProxy jar 要用到。",
            Keywords = "viaproxy minecraft version proxy viaversion protocol bridge bind target online mode jar java 我的世界 版本 代理 協定 伺服器",
        },
        new ManualEntry
        {
            Tag = "module.minecraftserver", Glyph = "",
            TitleEn = "Minecraft Server", TitleZh = "Minecraft 伺服器",
            SummaryEn = "Set up and run a Paper or Spigot Minecraft server, edit its properties, manage plugins and use the live console.",
            SummaryZh = "建立同運行 Paper 或者 Spigot Minecraft 伺服器，編輯設定、管理外掛同用即時主控台。",
            StepsEn = new[]
            {
                "On the Server tab, pick a folder, accept the EULA, then Download Paper (or build Spigot with BuildTools).",
                "On the Properties tab, edit server.properties and Save.",
                "On the Console tab, click Start, then type commands and Send them to the running server.",
                "On the Plugins tab, install presets or build a custom plugin from a git repo.",
            },
            StepsZh = new[]
            {
                "喺 Server 分頁揀個資料夾、接受 EULA，再 Download Paper（或者用 BuildTools build Spigot）。",
                "喺 Properties 分頁編輯 server.properties 再 Save。",
                "喺 Console 分頁撳 Start，然後打指令 Send 去運行緊嘅伺服器。",
                "喺 Plugins 分頁裝預設外掛，或者由 git repo build 一個自訂外掛。",
            },
            TipEn = "First use installs Java (via winget), which the server requires; you must accept the EULA before the server will start.",
            TipZh = "第一次用會用 winget 裝 Java，伺服器要用到；伺服器要 Start 之前一定要先接受 EULA。",
            Keywords = "minecraft server paper spigot papermc buildtools eula server.properties plugin luckperms console rcon 伺服器 外掛 主控台 我的世界",
        },
    };
}
