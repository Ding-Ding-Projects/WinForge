using System.Collections.Generic;

namespace WinForge.Services;

// 系統與診斷章節嘅教學條目 · How-to entries for the System & diagnostics section.
public static partial class ManualContent
{
    private static List<ManualEntry> SystemEntries() => new()
    {
        new ManualEntry
        {
            Tag = "module.doctors", Glyph = "",
            TitleEn = "System Doctors", TitleZh = "系統醫生",
            SummaryEn = "One-click repair cards for common Windows breakages: print queue, network/DNS, sleep/wake, taskbar & Start, search index, Explorer and file ownership.",
            SummaryZh = "一撳就修復常見嘅 Windows 毛病：列印佇列、網絡／DNS、睡眠喚醒、工作列同開始、搜尋索引、Explorer 同檔案擁有權。",
            StepsEn = new[]
            {
                "Scroll to the doctor card matching your problem (e.g. Print Spooler, Network / DNS, Sleep / Wake).",
                "Read the card's description so you know what it touches.",
                "Click a fix button — destructive ones like Reset Winsock or Rescue spooler are marked.",
                "Check the Output box and the result bar at the bottom to confirm it worked.",
            },
            StepsZh = new[]
            {
                "捲到同你個問題啱嘅醫生卡（例如 Print Spooler、Network / DNS、Sleep / Wake）。",
                "睇下張卡嘅說明，知道佢會郁啲乜。",
                "撳個修復掣 —— 似 Reset Winsock、救援 spooler 咁有破壞性嘅會標明。",
                "睇下 Output 框同最底嘅結果列，確認搞掂咗。",
            },
            TipEn = "Spooler, wake/sleep, fast startup and take-ownership need admin — use Relaunch as admin in the top banner for full effect.",
            TipZh = "Spooler、喚醒睡眠、快速啟動同取得擁有權要管理員權限 —— 撳最頂橫額嘅「以管理員身分重新啟動」先至完全生效。",
            Keywords = "doctor repair fix rescue printer spooler dns winsock tcp sleep wake taskbar start search index explorer ownership 修復 醫生 救援 列印 網絡 睡眠 工作列",
        },
        new ManualEntry
        {
            Tag = "module.services", Glyph = "",
            TitleEn = "Services", TitleZh = "服務",
            SummaryEn = "Browse every Windows service with its state and start mode, then start, stop or change startup type.",
            SummaryZh = "睇晒每個 Windows 服務嘅狀態同開機模式，再去啟動、停止或者改開機類型。",
            StepsEn = new[]
            {
                "Type in the search box to filter the service list by display name or short name.",
                "Read the State and Start Mode columns for each service.",
                "Click Actions on a row to start, stop, restart or change its startup type.",
                "Hit Refresh after a change to confirm the new state.",
            },
            StepsZh = new[]
            {
                "喺搜尋框打字，用顯示名或者短名 filter 個服務清單。",
                "睇下每個服務嘅 State 同 Start Mode 兩欄。",
                "撳嗰行嘅 Actions（操作）去啟動、停止、重啟或者改開機類型。",
                "改完撳 Refresh，確認新狀態。",
            },
            TipEn = "Starting, stopping or changing a service needs admin rights.",
            TipZh = "啟動、停止或者改服務都要管理員權限。",
            Keywords = "services start stop startup type manual automatic disabled 服務 啟動 停止 開機類型",
        },
        new ManualEntry
        {
            Tag = "module.tasks", Glyph = "",
            TitleEn = "Scheduled Tasks", TitleZh = "排程工作",
            SummaryEn = "List Task Scheduler entries with their state, then run, enable, disable or delete a task.",
            SummaryZh = "列出工作排程器嘅項目同狀態，再去執行、啟用、停用或者刪除一個工作。",
            StepsEn = new[]
            {
                "Use the search box to filter by task name or path.",
                "Pick a task and check its State column (Ready / Running / Disabled).",
                "Click Actions to run now, enable, disable or delete it.",
                "Press Refresh to reload the list after an action.",
            },
            StepsZh = new[]
            {
                "用搜尋框按工作名或者路徑 filter。",
                "揀個工作，睇下佢 State 欄（Ready／Running／Disabled）。",
                "撳 Actions（操作）即刻執行、啟用、停用或者刪除佢。",
                "做完撳 Refresh 重新載入清單。",
            },
            TipEn = "Editing system tasks under \\Microsoft\\Windows usually needs admin rights.",
            TipZh = "改 \\Microsoft\\Windows 下面嘅系統工作通常要管理員權限。",
            Keywords = "scheduled task scheduler run enable disable delete trigger 排程 工作 執行 觸發",
        },
        new ManualEntry
        {
            Tag = "module.devices", Glyph = "",
            TitleEn = "Devices", TitleZh = "裝置",
            SummaryEn = "A flat Device Manager: list hardware with class and status, then enable, disable or update drivers.",
            SummaryZh = "一個平面版嘅裝置管理員：列出硬件嘅類別同狀態，再去啟用、停用或者更新驅動程式。",
            StepsEn = new[]
            {
                "Search by device name or instance ID to narrow the list.",
                "Read the Class and Status columns to spot problem devices.",
                "Click Actions on a device to enable, disable, uninstall or update its driver.",
                "Refresh to re-scan after changes.",
            },
            StepsZh = new[]
            {
                "按裝置名或者 instance ID 搜尋，收窄個清單。",
                "睇 Class 同 Status 兩欄，搵出有問題嘅裝置。",
                "撳嗰個裝置嘅 Actions（操作）去啟用、停用、解除安裝或者更新驅動。",
                "改完撳 Refresh 重新掃描。",
            },
            TipEn = "Disabling, uninstalling or updating drivers needs admin rights.",
            TipZh = "停用、解除安裝或者更新驅動都要管理員權限。",
            Keywords = "device manager hardware driver enable disable uninstall update instance id 裝置 驅動 硬件",
        },
        new ManualEntry
        {
            Tag = "module.vivetool", Glyph = "",
            TitleEn = "ViVeTool", TitleZh = "功能旗標",
            SummaryEn = "Turn hidden Windows feature flags on or off with ViVeTool — named toggles for popular ones plus the full feature store.",
            SummaryZh = "用 ViVeTool 開關隱藏嘅 Windows 功能旗標 —— 熱門嘅有現成開關掣，仲有成個 feature store。",
            StepsEn = new[]
            {
                "If the warning banner shows, click Install to fetch ViVeTool first.",
                "Use the named toggles at the top to flip well-known features on or off.",
                "Or search the feature store list and click Actions to enable/disable a specific feature ID.",
                "Open the More menu for Scan, Export/Import or Last-known-good.",
                "Use Restart Explorer or Reboot from the More menu so the change takes effect.",
            },
            StepsZh = new[]
            {
                "如果見到警告橫額，先撳 Install 攞 ViVeTool 落嚟。",
                "用上面嗰排現成開關掣去開關啲熱門功能。",
                "或者喺 feature store 清單搜尋，撳 Actions 啟用／停用某個 feature ID。",
                "撳 More 選單入面有 Scan、Export／Import 同 Last-known-good。",
                "喺 More 選單撳 Restart Explorer 或者 Reboot，個改動先生效。",
            },
            TipEn = "Feature flags are experimental and need admin rights — note the ID so you can revert if something breaks.",
            TipZh = "功能旗標係實驗性嘅，要管理員權限 —— 記低個 ID，搞壞咗可以還原。",
            Keywords = "vivetool vive feature flag experiment hidden id enable disable feature store 功能 旗標 實驗 隱藏",
        },
        new ManualEntry
        {
            Tag = "module.regedit", Glyph = "",
            TitleEn = "Registry Editor", TitleZh = "登錄編輯器",
            SummaryEn = "Browse the registry hive tree on the left, see a key's values on the right, and add, edit or delete them.",
            SummaryZh = "左邊行登錄檔嘅 hive 樹，右邊睇個 key 嘅值，再去新增、編輯或者刪除。",
            StepsEn = new[]
            {
                "Expand the tree on the left to drill into a hive and key.",
                "Read the full path in the bar under the header.",
                "Look at the Name / Type / Data columns for that key's values on the right.",
                "Use New, Edit or Delete to change a value, then Refresh to confirm.",
            },
            StepsZh = new[]
            {
                "喺左邊展開個樹，鑽入去某個 hive 同 key。",
                "標題下面條 path bar 會顯示完整路徑。",
                "右邊睇嗰個 key 啲值嘅 Name／Type／Data 三欄。",
                "用 New、Edit 或者 Delete 改個值，再撳 Refresh 確認。",
            },
            TipEn = "Editing HKLM keys needs admin rights, and a wrong change can break Windows — export or note the old value first.",
            TipZh = "改 HKLM 嘅 key 要管理員權限，改錯可以搞壞 Windows —— 改之前最好匯出或者記低舊值。",
            Keywords = "registry regedit hive key value reg dword string new edit delete 登錄檔 機碼 數值",
        },
        new ManualEntry
        {
            Tag = "module.startup", Glyph = "",
            TitleEn = "Startup Apps", TitleZh = "開機程式",
            SummaryEn = "See everything that auto-runs at logon with its command and location, then enable or disable each entry.",
            SummaryZh = "睇晒登入時自動執行嘅嘢、佢哋嘅指令同位置，再去逐個啟用或者停用。",
            StepsEn = new[]
            {
                "Search to filter the startup entries by name.",
                "Check the Command, Location and state columns for each item.",
                "Click the enable (check) or disable (x) button on a row to toggle it.",
                "Refresh to confirm the new state.",
            },
            StepsZh = new[]
            {
                "用搜尋框按名 filter 啲開機項目。",
                "睇每項嘅 Command、Location 同狀態欄。",
                "撳嗰行嘅啟用（剔）或者停用（x）掣去切換。",
                "撳 Refresh 確認新狀態。",
            },
            TipEn = "Disabling unneeded startup apps speeds up logon; entries under HKLM may need admin rights.",
            TipZh = "停用唔需要嘅開機程式可以加快登入；HKLM 下面嘅項目可能要管理員權限。",
            Keywords = "startup autostart logon run boot enable disable 開機 自啟動 登入",
        },
        new ManualEntry
        {
            Tag = "module.events", Glyph = "",
            TitleEn = "Event Viewer", TitleZh = "事件檢視器",
            SummaryEn = "Read Windows event logs in-app: pick a log and level, filter the text, and click a row to see full details.",
            SummaryZh = "喺應用內睇 Windows 事件記錄：揀個 log 同 level、filter 文字，撳一行就睇到完整內容。",
            StepsEn = new[]
            {
                "Choose a log (System, Application…) and a level (Error, Warning…) from the drop-downs.",
                "Set how many entries to pull, then type in the filter box to narrow them.",
                "Scan the Time / Level / Id / Provider / Message columns.",
                "Click a row to read the full event detail in the pane below; press Refresh to re-query.",
            },
            StepsZh = new[]
            {
                "喺下拉揀個 log（System、Application…）同 level（Error、Warning…）。",
                "設定要攞幾多筆，再喺 filter 框打字收窄。",
                "睇 Time／Level／Id／Provider／Message 幾欄。",
                "撳一行喺下面睇完整事件內容；撳 Refresh 重新查詢。",
            },
            TipEn = "Some logs (like Security) need admin rights to read.",
            TipZh = "有啲 log（例如 Security）要管理員權限先睇到。",
            Keywords = "event log viewer system application security error warning provider id 事件 記錄 錯誤 警告",
        },
        new ManualEntry
        {
            Tag = "module.monitor", Glyph = "",
            TitleEn = "System Monitor", TitleZh = "系統監察",
            SummaryEn = "A btop-style live dashboard: CPU per-core bars, RAM/swap meters, network sparklines, plus a sortable process list you can act on.",
            SummaryZh = "btop 風格嘅即時儀表板：CPU 每核心條、RAM／swap 計、網絡 sparkline，仲有可排序、可操作嘅程序清單。",
            StepsEn = new[]
            {
                "Pick a refresh interval from the drop-down at the top right.",
                "Watch the CPU sparkline and per-core bars, plus the memory, swap and network meters.",
                "Click a column header (PID / Name / CPU / MEM) to sort the process list.",
                "Search to find a process, then use Priority to set priority/efficiency/affinity or the x button to end task.",
            },
            StepsZh = new[]
            {
                "喺右上角下拉揀個更新間隔。",
                "睇 CPU sparkline 同每核心條，仲有記憶體、swap 同網絡計。",
                "撳欄標題（PID／Name／CPU／MEM）排序個程序清單。",
                "搜尋搵個程序，用 Priority 設定優先權／效率／親和性，或者撳 x 結束工作。",
            },
            TipEn = "Changing priority/affinity or ending system processes needs admin rights.",
            TipZh = "改優先權／親和性或者結束系統程序要管理員權限。",
            Keywords = "cpu ram memory swap network task manager priority affinity efficiency btop per-core sparkline 監察 工作管理員 每核心",
        },
        new ManualEntry
        {
            Tag = "module.winfetch", Glyph = "",
            TitleEn = "System Info (Winfetch)", TitleZh = "系統資訊",
            SummaryEn = "A neofetch-style summary of your machine — OS, host, kernel, uptime, CPU, GPU, memory and more — in a pretty UI or ASCII view.",
            SummaryZh = "neofetch 風格嘅機器摘要 —— OS、host、kernel、開機時間、CPU、GPU、記憶體等等 —— 有靚 UI 或者 ASCII 兩種睇法。",
            StepsEn = new[]
            {
                "Read the info rows beside the Windows logo for your full specs.",
                "Flip the ASCII / UI toggle to switch to a console-style text view.",
                "Click Copy to grab the text, or Export to save it to a file.",
                "Press Refresh to re-read the system info.",
            },
            StepsZh = new[]
            {
                "睇 Windows logo 隔籬嗰啲資料行，就係你嘅完整規格。",
                "撳 ASCII／UI 開關，切換成主控台風格嘅文字版。",
                "撳 Copy 抄走啲文字，或者 Export 存做檔案。",
                "撳 Refresh 重新讀取系統資訊。",
            },
            Keywords = "winfetch neofetch fetch system info os host kernel uptime cpu gpu memory ascii specs 系統資訊 規格 開機時間",
        },
        new ManualEntry
        {
            Tag = "module.battery", Glyph = "",
            TitleEn = "Battery & Thermal", TitleZh = "電池與散熱",
            SummaryEn = "Live battery charge, health/wear and temperature cards, a sensor table, plus one-click battery and energy reports.",
            SummaryZh = "即時電池電量、健康／耗損同溫度卡、感應器清單，仲有一撳出嘅電池同能源報告。",
            StepsEn = new[]
            {
                "Read the top cards for charge %, battery wear/health and the hottest temperature.",
                "Scroll the sensor table for per-hardware readings (temps, fans, etc.).",
                "Click Health report for a battery wear report, or Energy report for a power analysis.",
                "Wait for the report to generate, then open the saved HTML file.",
            },
            StepsZh = new[]
            {
                "睇最頂幾張卡：電量 %、電池耗損／健康同最高溫度。",
                "捲動感應器清單睇逐個硬件嘅讀數（溫度、風扇等等）。",
                "撳 Health report 出電池耗損報告，或者 Energy report 出耗電分析。",
                "等報告整好，再打開個 HTML 檔。",
            },
            TipEn = "Temperature and fan sensors only show on hardware that exposes them; reports need admin rights.",
            TipZh = "溫度同風扇感應器淨係喺有提供嘅硬件先見到；出報告要管理員權限。",
            Keywords = "battery thermal temperature wear health cpu gpu fan powercfg batteryreport energy report 電池 溫度 散熱 風扇 耗損",
        },
        new ManualEntry
        {
            Tag = "module.connections", Glyph = "",
            TitleEn = "Connections", TitleZh = "連線",
            SummaryEn = "A live netstat/TCPView: every TCP/UDP connection with its local/remote address, state and owning process — droppable and killable.",
            SummaryZh = "即時 netstat／TCPView：每條 TCP／UDP 連線嘅本地／遠端位址、狀態同擁有程序 —— 可以切斷或者結束。",
            StepsEn = new[]
            {
                "Filter by text and pick a protocol (TCP/UDP) in the drop-down.",
                "Read the Local, Remote, State and Process/PID columns.",
                "Leave the auto-refresh switch on for a live view, or hit Refresh manually.",
                "Use the drop button to cut a single connection, or the x to end the owning process.",
            },
            StepsZh = new[]
            {
                "用文字 filter，再喺下拉揀協定（TCP／UDP）。",
                "睇 Local、Remote、State 同 Process／PID 幾欄。",
                "開住 auto-refresh 開關就會即時更新，或者自己撳 Refresh。",
                "撳切斷掣 cut 單一條連線，或者撳 x 結束擁有嗰個程序。",
            },
            TipEn = "Dropping a connection or ending a process needs admin rights.",
            TipZh = "切斷連線或者結束程序要管理員權限。",
            Keywords = "tcp udp connections netstat tcpview port state process pid drop kill 連線 連接埠 程序",
        },
        new ManualEntry
        {
            Tag = "module.wireshark", Glyph = "",
            TitleEn = "Packet Capture", TitleZh = "封包擷取",
            SummaryEn = "Capture and inspect network packets via tshark/dumpcap: pick an interface, apply filters, watch a live packet grid, follow streams and read stats.",
            SummaryZh = "用 tshark／dumpcap 抓同睇網絡封包：揀介面、套 filter、睇即時封包格、follow stream 同睇統計。",
            StepsEn = new[]
            {
                "On the Live capture tab, refresh and pick a network interface.",
                "Set a capture filter (BPF, e.g. tcp port 443) and/or a display filter, then click Start.",
                "Watch the live packet grid; click a row to see its detail, or Stop when done.",
                "Use the Open file, Statistics and Detail tabs to inspect a saved pcap, follow a stream or run protocol/conversation stats.",
            },
            StepsZh = new[]
            {
                "喺 Live capture 分頁撳 refresh，揀個網絡介面。",
                "設定 capture filter（BPF，例如 tcp port 443）同／或 display filter，再撳 Start。",
                "睇即時封包格；撳一行睇詳情，搞掂就撳 Stop。",
                "用 Open file、Statistics 同 Detail 分頁去睇舊 pcap、follow stream 或者跑協定／對話統計。",
            },
            TipEn = "Needs Npcap installed and admin rights to capture; if the engine bar warns, install the missing piece first.",
            TipZh = "抓包要裝咗 Npcap 同管理員權限；如果 engine 橫額有警告，先裝返缺咗嗰嚿。",
            Keywords = "wireshark packet capture tshark dumpcap pcap npcap interface bpf display filter follow stream statistics 封包 擷取 抓包 過濾",
        },
        new ManualEntry
        {
            Tag = "module.nmap", Glyph = "",
            TitleEn = "Nmap Scanner", TitleZh = "網絡掃描",
            SummaryEn = "Run Nmap port/host scans from a friendly UI: enter a target, pick a profile and flags, preview the command, then read open ports and services in a grid.",
            SummaryZh = "用友善介面跑 Nmap 端口／主機掃描：打 target、揀 profile 同 flag、預覽指令，再喺表格睇開住嘅端口同服務。",
            StepsEn = new[]
            {
                "Enter a target (host, IP, CIDR or range) and pick a scan profile.",
                "Toggle the common flag checkboxes or type extra flags; check the command preview.",
                "Click Run and watch the Live log tab; Cancel to stop.",
                "Read results in the Host / Port / Proto / State / Service / Version grid, then Save if you want.",
            },
            StepsZh = new[]
            {
                "打個 target（主機、IP、CIDR 或者範圍），揀個 scan profile。",
                "撳啲常用 flag checkbox 或者打額外 flag；睇下指令預覽。",
                "撳 Run，喺 Live log 分頁睇進度；撳 Cancel 停。",
                "喺 Host／Port／Proto／State／Service／Version 表格睇結果，需要就撳 Save。",
            },
            TipEn = "Only scan hosts you're allowed to; OS/version detection and some scans need Npcap and admin rights.",
            TipZh = "淨係掃你有權掃嘅主機；OS／版本偵測同某啲掃描要 Npcap 同管理員權限。",
            Keywords = "nmap port scan network security host service os version cidr subnet npcap profile flags 掃描 端口 連接埠 主機 服務",
        },
        new ManualEntry
        {
            Tag = "module.native", Glyph = "",
            TitleEn = "Native Utilities", TitleZh = "原生工具",
            SummaryEn = "A grab-bag of native Windows tools in tabs: saved & nearby Wi-Fi, SMB shares/sessions, monitor brightness, user sessions, certificates, live counters, process modules and Bluetooth.",
            SummaryZh = "一堆原生 Windows 工具，分做幾個分頁：已存同附近 Wi-Fi、SMB 共享／工作階段、螢幕亮度、使用者工作階段、憑證、即時計數、程序模組同藍牙。",
            StepsEn = new[]
            {
                "Pick a tab for the tool you need (Wi-Fi, SMB, Brightness, Sessions, Certificates, Counters, Modules, Bluetooth).",
                "Click Refresh in that tab to load its data.",
                "For saved Wi-Fi, copy a password or forget a network; drag brightness sliders for each monitor.",
                "Disconnect/log off a user session, or unpair a Bluetooth device from its row button.",
            },
            StepsZh = new[]
            {
                "揀你要嘅工具分頁（Wi-Fi、SMB、亮度、工作階段、憑證、計數、模組、藍牙）。",
                "喺嗰個分頁撳 Refresh 載入資料。",
                "已存 Wi-Fi 可以複製密碼或者移除網絡；逐個螢幕拖亮度條。",
                "中斷／登出某個使用者工作階段，或者喺嗰行撳掣解除藍牙配對。",
            },
            TipEn = "Showing Wi-Fi passwords, SMB sessions and disconnecting users generally needs admin rights.",
            TipZh = "睇 Wi-Fi 密碼、SMB 工作階段同中斷使用者通常要管理員權限。",
            Keywords = "wifi password saved nearby smb shares sessions brightness ddc certificate users logoff bluetooth modules counters 原生 密碼 共享 亮度 憑證 藍牙",
        },
        new ManualEntry
        {
            Tag = "module.envvars", Glyph = "",
            TitleEn = "Environment Variables", TitleZh = "環境變數",
            SummaryEn = "View and edit User or System environment variables in one place, including adding new ones and tidying PATH.",
            SummaryZh = "喺一個地方睇同改 User 或者 System 環境變數，包括新增同埋執靚個 PATH。",
            StepsEn = new[]
            {
                "Choose User or System variables from the drop-down at the top.",
                "Read the Name / Value list for the chosen target.",
                "Type a Name and Value and click Add to create one.",
                "Use the edit or delete button on a row to change or remove a variable.",
            },
            StepsZh = new[]
            {
                "喺頂部下拉揀 User 定 System 變數。",
                "睇所揀目標嘅 Name／Value 清單。",
                "打個 Name 同 Value，撳 Add 新增一個。",
                "撳嗰行嘅 edit 或者 delete 掣去改或者刪除變數。",
            },
            TipEn = "Editing System variables needs admin rights; open a new terminal afterwards to pick up changes.",
            TipZh = "改 System 變數要管理員權限；改完開返個新 terminal 先讀到新值。",
            Keywords = "environment variables path user system env add edit delete 環境變數 路徑",
        },
        new ManualEntry
        {
            Tag = "module.clipboard", Glyph = "",
            TitleEn = "Clipboard", TitleZh = "剪貼簿",
            SummaryEn = "Browse your clipboard history of text, images and files, and convert items — like making a QR code or stripping to plain text.",
            SummaryZh = "睇你嘅剪貼簿歷史（文字、圖片、檔案），仲可以轉換 —— 例如整 QR code 或者轉做純文字。",
            StepsEn = new[]
            {
                "Scroll the list to browse recent clipboard items (text, images, files).",
                "Click an item to copy it back, or use its convert options (e.g. QR code, plain text).",
                "Use Clear to wipe the history.",
            },
            StepsZh = new[]
            {
                "捲動清單睇最近嘅剪貼簿項目（文字、圖片、檔案）。",
                "撳一項複製返佢，或者用佢嘅轉換選項（例如 QR code、純文字）。",
                "撳 Clear 清走歷史。",
            },
            TipEn = "Clipboard history must be on in Windows (Win+V) for items to show here.",
            TipZh = "Windows 嘅剪貼簿歷史要開咗（Win+V），啲項目先會喺度顯示。",
            Keywords = "clipboard history text image file qr qrcode plain text paste win+v 剪貼簿 歷史 二維碼 純文字",
        },
        new ManualEntry
        {
            Tag = "module.settingshub", Glyph = "",
            TitleEn = "Settings & Control Panel", TitleZh = "設定與控制台",
            SummaryEn = "A searchable launcher for every Windows Settings page and Control Panel applet — change things in-app or jump straight to the Windows page.",
            SummaryZh = "一個可搜尋嘅啟動器，涵蓋每個 Windows 設定頁同控制台面板 —— 喺應用內改，或者直接跳去 Windows 嗰版。",
            StepsEn = new[]
            {
                "Pick a mode: Change here (in-app) or Open in Windows.",
                "Type in the search box to find a setting or applet by name.",
                "Click an entry to adjust it in-app or open the matching ms-settings page / Control Panel applet.",
            },
            StepsZh = new[]
            {
                "揀模式：「喺度改（應用內）」或者「喺 Windows 打開」。",
                "喺搜尋框打字，按名搵設定或者面板。",
                "撳一項喺應用內改，或者打開對應嘅 ms-settings 頁／控制台面板。",
            },
            Keywords = "settings control panel ms-settings applet cpl launcher open page 設定 控制台 啟動器 面板",
        },
    };
}
