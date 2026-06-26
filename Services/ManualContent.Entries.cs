using System.Collections.Generic;

namespace WinForge.Services;

// 各功能嘅雙語教學條目 · Per-feature bilingual how-to entries.
// 內容由 docs/handoffs 規格同各模組改寫成使用者導向嘅指引。
// Module icons are resolved from ModuleRegistry at render time, so Glyph here is left blank for module entries.
public static partial class ManualContent
{
    private static List<ManualEntry> AppsEntries()
    {
        var list = AppsEntriesPart1();
        list.AddRange(AppsEntriesPart2());
        return list;
    }

    // ===================================================================
    // 7 · Security & privacy · 安全與私隱
    // ===================================================================
    private static List<ManualEntry> SecurityEntries() => new()
    {
        new ManualEntry
        {
            Tag = "module.vault-volumes", Glyph = "",
            TitleEn = "WinForge Vault", TitleZh = "WinForge 保險庫",
            SummaryEn = "Create on-the-fly encrypted volumes (VeraCrypt-style) and mount them as a normal drive letter to keep sensitive files private.",
            SummaryZh = "整即時加密嘅磁碟區（VeraCrypt 風格），掛載成普通磁碟機字母，將敏感檔案收埋。",
            StepsEn = new[]
            {
                "If the engine bar warns the encryption engine is missing, install it first.",
                "Open Create a container, pick a file path and size, choose an algorithm (AES, Serpent…), hash and filesystem, then set a strong password and click Create.",
                "Open Mount, pick the container file and a free drive letter, type the password (or pick a keyfile), and click Mount.",
                "Use the mounted drive like any other disk; when finished, click Dismount (or Dismount all) to lock it away.",
                "Use Change password, Benchmark or Wipe cache from the toolbar as needed.",
            },
            StepsZh = new[]
            {
                "如果引擎列提示加密引擎唔見咗，先裝返佢。",
                "開 Create a container，揀檔案路徑同大細、揀演算法（AES、Serpent…）、雜湊同檔案系統，設個強密碼再撳 Create。",
                "開 Mount，揀個容器檔同一個未用嘅磁碟機字母，打密碼（或者揀 keyfile），撳 Mount。",
                "之後個掛載磁碟用得同普通磁碟一樣；用完撳 Dismount（或者 Dismount all）鎖返佢。",
                "需要嘅話用工具列嘅 Change password、Benchmark 或者 Wipe cache。",
            },
            TipEn = "There is no password recovery — if you forget the container password, the data is gone for good. Keep a safe backup of the password.",
            TipZh = "冇得救回密碼 —— 如果你唔記得容器密碼，啲資料就永遠攞唔返。記得將密碼安全咁備份好。",
            Keywords = "vault veracrypt volume container encrypt mount dismount password keyfile aes serpent 保險庫 加密 容器 掛載 卸載 密碼",
        },
        new ManualEntry
        {
            Tag = "", Glyph = "",
            TitleEn = "How WinForge protects your secrets", TitleZh = "WinForge 點保護你嘅機密",
            SummaryEn = "Passwords, API keys and tokens you enter are stored encrypted on your own machine — not in plain text.",
            SummaryZh = "你輸入嘅密碼、API key 同權杖都會喺你自己部機度加密儲存 —— 唔係明文。",
            StepsEn = new[]
            {
                "Module credentials (SSH, FTP, Postgres, Bitwarden session, captured logins) are encrypted with Windows DPAPI, tied to your user account.",
                "Use In-App Login to sign in to web services; the captured session lives in an isolated WebView2 profile.",
                "When exporting settings with Config & Backup, tick \"include secrets\" only if you set an export password to encrypt them.",
                "Never share an exported bundle that contains secrets without its password.",
            },
            StepsZh = new[]
            {
                "模組嘅憑證（SSH、FTP、Postgres、Bitwarden session、擷取到嘅登入）都用 Windows DPAPI 加密，綁住你個用戶帳戶。",
                "用內置登入去登入網頁服務；擷取到嘅 session 會放喺一個獨立嘅 WebView2 profile。",
                "用設定與備份匯出設定嗰陣，淨係喺你設咗匯出密碼加密之後先剔「包含機密」。",
                "唔好喺冇密碼嘅情況下分享含有機密嘅匯出捆綁。",
            },
            TipEn = "DPAPI keys are bound to your Windows account, so an exported secret blob won't decrypt on another user or PC unless re-encrypted with an export password.",
            TipZh = "DPAPI 金鑰綁住你個 Windows 帳戶，所以匯出嘅機密喺第二個用戶或者第二部機解唔到，除非用匯出密碼重新加密。",
            Keywords = "secrets dpapi encryption credentials api key token export password security 機密 加密 憑證 密碼 安全",
        },
    };

    // ===================================================================
    // 8 · Windows 11 tweaks & recipes · Windows 11 調校與一鍵流程
    // ===================================================================
    private static List<ManualEntry> TweaksCatalogEntries() => new()
    {
        new ManualEntry
        {
            Tag = "", Glyph = "",
            TitleEn = "How tweaks work", TitleZh = "調校點運作",
            SummaryEn = "Beyond the tool modules, WinForge has hundreds of real Windows settings ('tweaks') grouped into categories under All Tweaks.",
            SummaryZh = "除咗工具模組，WinForge 仲有幾百個真實 Windows 設定（「調校」），分門別類放喺「全部調校」下面。",
            StepsEn = new[]
            {
                "Open All Tweaks in the navigation pane and pick a category, or search from the top search box.",
                "Each tweak is a card with a bilingual title and description — read it so you know what it changes.",
                "Flip the switch or click the action button to apply it; many changes are reversible by flipping back.",
                "Some tweaks need administrator rights, or an Explorer restart / sign-out to fully take effect.",
            },
            StepsZh = new[]
            {
                "喺導覽窗開「全部調校」揀個分類，或者用頂部搜尋框搵。",
                "每個調校都係一張卡，有雙語標題同說明 —— 睇清楚佢會改啲乜。",
                "撳開關或者動作掣套用；好多改動撳返轉頭就還原到。",
                "有啲調校要管理員權限，或者要重啟 Explorer／登出先完全生效。",
            },
            TipEn = "Always read a tweak's description before applying — these change real Windows settings.",
            TipZh = "套用之前一定要睇說明 —— 呢啲會改到真實嘅 Windows 設定。",
            Keywords = "tweaks settings categories registry toggle 調校 設定 分類 登錄檔",
        },
        new ManualEntry
        {
            Tag = "appearance", Glyph = "",
            TitleEn = "Appearance & Personalisation", TitleZh = "外觀與個人化",
            SummaryEn = "Dark mode, accent colour, transparency, animations and visual effects.",
            SummaryZh = "深色模式、強調色、透明度、動畫同視覺特效。",
            StepsEn = new[]
            {
                "Open the Appearance & Personalisation category.",
                "Toggle dark/light mode, transparency and animation tweaks to taste.",
                "Apply accent-colour and visual-effect changes; some refresh instantly, others after an Explorer restart.",
            },
            StepsZh = new[]
            {
                "開「外觀與個人化」分類。",
                "按喜好開關深色／淺色模式、透明度同動畫調校。",
                "套用強調色同視覺特效；有啲即刻更新，有啲要重啟 Explorer。",
            },
            Keywords = "appearance dark mode accent transparency animation 外觀 深色 強調色 透明 動畫",
        },
        new ManualEntry
        {
            Tag = "explorer", Glyph = "",
            TitleEn = "File Explorer", TitleZh = "檔案總管",
            SummaryEn = "Show file extensions and hidden files, restore classic menus, and change Explorer behaviour.",
            SummaryZh = "顯示副檔名同隱藏檔案、還原經典選單，同改檔案總管行為。",
            StepsEn = new[]
            {
                "Open the File Explorer category.",
                "Turn on show file extensions and hidden files if you want them visible.",
                "Toggle classic context menu or other Explorer behaviours; restart Explorer if a change hasn't appeared.",
            },
            StepsZh = new[]
            {
                "開「檔案總管」分類。",
                "想睇到就開啟顯示副檔名同隱藏檔案。",
                "開關經典右鍵選單或者其他 Explorer 行為；改動未出就重啟 Explorer。",
            },
            Keywords = "explorer file extensions hidden files classic menu 檔案總管 副檔名 隱藏檔案 經典選單",
        },
        new ManualEntry
        {
            Tag = "taskbar", Glyph = "",
            TitleEn = "Taskbar & Start", TitleZh = "工作列與開始功能表",
            SummaryEn = "Taskbar alignment, Search, Widgets, Task View and Start menu layout.",
            SummaryZh = "工作列對齊、搜尋、小工具、工作檢視同開始功能表版面。",
            StepsEn = new[]
            {
                "Open the Taskbar & Start category.",
                "Align the taskbar left, hide Search / Widgets / Task View, and adjust the Start layout.",
                "Restart Explorer if a taskbar change doesn't show right away.",
            },
            StepsZh = new[]
            {
                "開「工作列與開始功能表」分類。",
                "將工作列靠左、收起搜尋／小工具／工作檢視，同調開始功能表版面。",
                "如果工作列改動冇即刻出，重啟 Explorer。",
            },
            TipEn = "For deep taskbar behaviours, see the Taskbar Tweaker and Windhawk Mods tools.",
            TipZh = "想要深層工作列行為，睇工作列調校同 Windhawk 模組工具。",
            Keywords = "taskbar start search widgets task view align 工作列 開始功能表 搜尋 小工具 對齊",
        },
        new ManualEntry
        {
            Tag = "privacy", Glyph = "",
            TitleEn = "Privacy & Telemetry", TitleZh = "私隱與遙測",
            SummaryEn = "Advertising ID, telemetry, activity history, location and tailored ads.",
            SummaryZh = "廣告 ID、遙測、活動記錄、定位同個人化廣告。",
            StepsEn = new[]
            {
                "Open the Privacy & Telemetry category.",
                "Switch off the advertising ID, activity history, tailored ads and location access you don't want.",
                "Reduce telemetry; some changes need admin rights.",
            },
            StepsZh = new[]
            {
                "開「私隱與遙測」分類。",
                "關掉你唔想要嘅廣告 ID、活動記錄、個人化廣告同定位存取。",
                "減少遙測；有啲改動要管理員權限。",
            },
            Keywords = "privacy telemetry advertising id activity history location ads 私隱 遙測 廣告 定位",
        },
        new ManualEntry
        {
            Tag = "performance", Glyph = "",
            TitleEn = "Performance & Power", TitleZh = "效能與電源",
            SummaryEn = "Power plans, hibernation, fast startup, game mode and responsiveness.",
            SummaryZh = "電源計劃、休眠、快速啟動、遊戲模式同反應速度。",
            StepsEn = new[]
            {
                "Open the Performance & Power category.",
                "Pick a power plan, toggle fast startup or hibernation, and enable game mode.",
                "Apply responsiveness tweaks; some need admin rights or a reboot.",
            },
            StepsZh = new[]
            {
                "開「效能與電源」分類。",
                "揀電源計劃、開關快速啟動或者休眠，同開遊戲模式。",
                "套用反應速度調校；有啲要管理員權限或者重開機。",
            },
            Keywords = "performance power plan hibernation fast startup game mode 效能 電源 休眠 快速啟動 遊戲模式",
        },
        new ManualEntry
        {
            Tag = "cleanup", Glyph = "",
            TitleEn = "Cleanup & Storage", TitleZh = "清理與儲存",
            SummaryEn = "Clear temp files, caches, the Recycle Bin, the Windows Update cache and thumbnails.",
            SummaryZh = "清暫存檔、快取、回收筒、Windows Update 快取同縮圖。",
            StepsEn = new[]
            {
                "Open the Cleanup & Storage category.",
                "Pick what to clear: temp files, caches, Recycle Bin, Update cache, thumbnails.",
                "Run the cleanup action; clearing the Update cache may need admin rights.",
            },
            StepsZh = new[]
            {
                "開「清理與儲存」分類。",
                "揀要清乜：暫存檔、快取、回收筒、更新快取、縮圖。",
                "執行清理；清更新快取可能要管理員權限。",
            },
            Keywords = "cleanup storage temp cache recycle bin update thumbnails 清理 儲存 暫存 快取 回收筒 縮圖",
        },
        new ManualEntry
        {
            Tag = "security", Glyph = "",
            TitleEn = "Security (tweaks)", TitleZh = "安全（調校）",
            SummaryEn = "UAC, Defender, SmartScreen, firewall and account protections.",
            SummaryZh = "UAC、Defender、SmartScreen、防火牆同帳戶保護。",
            StepsEn = new[]
            {
                "Open the Security category under All Tweaks.",
                "Adjust UAC level, Defender, SmartScreen and firewall toggles.",
                "Most security changes need admin rights — accept the prompt.",
            },
            StepsZh = new[]
            {
                "喺「全部調校」開「安全」分類。",
                "調 UAC 等級、Defender、SmartScreen 同防火牆開關。",
                "多數安全改動要管理員權限 —— 應允個提示。",
            },
            TipEn = "Don't lower UAC or disable Defender unless you understand the risk.",
            TipZh = "除非你明白風險，否則唔好降低 UAC 或者關 Defender。",
            Keywords = "security uac defender smartscreen firewall account 安全 防火牆 帳戶",
        },
        new ManualEntry
        {
            Tag = "annoyances", Glyph = "",
            TitleEn = "Debloat & Annoyances", TitleZh = "去煩擾",
            SummaryEn = "Switch off the most-complained-about Windows 11 nags: Copilot, Recall, Bing search, Search Highlights, lock-screen tips and ads.",
            SummaryZh = "關掉最多人投訴嘅 Windows 11 煩擾：Copilot、Recall、Bing 搜尋、搜尋醒目提示、鎖機畫面提示同廣告。",
            StepsEn = new[]
            {
                "Open the Debloat & Annoyances category.",
                "Toggle off Copilot, Recall, Bing/web search in Start, Search Highlights and lock-screen tips.",
                "Restart Explorer or sign out so the nags disappear.",
            },
            StepsZh = new[]
            {
                "開「去煩擾」分類。",
                "關掉 Copilot、Recall、開始功能表嘅 Bing／網絡搜尋、搜尋醒目提示同鎖機畫面提示。",
                "重啟 Explorer 或者登出，啲煩擾就會消失。",
            },
            Keywords = "debloat annoyances copilot recall bing search highlights ads 去煩擾 廣告",
        },
        new ManualEntry
        {
            Tag = "win11pro", Glyph = "",
            TitleEn = "Windows 11 Advanced & Winaero", TitleZh = "Windows 11 進階與 Winaero",
            SummaryEn = "Power-user tweaks: input precision, storage, boot options, Explorer extras, Winaero-style polish and every Settings deep link.",
            SummaryZh = "進階調校：輸入精準度、儲存、開機選項、檔案總管進階、Winaero 風格修飾，同所有設定深層連結。",
            StepsEn = new[]
            {
                "Open the Windows 11 Advanced (or Winaero Tweaks) category.",
                "Browse advanced tweaks for input, storage, boot, coloured title bars, faster menus and shutdown.",
                "Use the Settings deep links to jump straight to any Windows Settings page.",
            },
            StepsZh = new[]
            {
                "開「Windows 11 進階」（或者「Winaero 進階調校」）分類。",
                "瀏覽輸入、儲存、開機、彩色標題列、更快選單同關機嘅進階調校。",
                "用設定深層連結直接跳去任何 Windows 設定頁。",
            },
            Keywords = "winaero advanced win11 pro input storage boot title bar deep link 進階 開機 標題列 深層連結",
        },
        new ManualEntry
        {
            Tag = "recipes", Glyph = "",
            TitleEn = "Recipes (one-click)", TitleZh = "一鍵流程",
            SummaryEn = "Bundled multi-step chores that run with a single button — cleanup, privacy, gaming, dev setup and more.",
            SummaryZh = "將多步驟嘅例行工作夾埋一個掣搞掂 —— 清理、私隱、遊戲、開發設定等等。",
            StepsEn = new[]
            {
                "Open Recipes in the navigation pane.",
                "Pick a recipe (e.g. Privacy hardening, Gaming setup, Cleanup); read the list of steps it will run.",
                "Click Run to execute the whole bundle in one go; watch the per-step results.",
            },
            StepsZh = new[]
            {
                "喺導覽窗開「一鍵流程」。",
                "揀一個流程（例如私隱強化、遊戲設定、清理）；睇清楚佢會跑邊啲步驟。",
                "撳 Run 一次過執行成個流程；睇住每步嘅結果。",
            },
            TipEn = "A recipe can change many settings at once — review its steps before running.",
            TipZh = "一個流程可以一次過改好多設定 —— 跑之前睇清楚啲步驟。",
            Keywords = "recipes one-click bundle cleanup privacy gaming dev setup 一鍵 流程 清理 私隱 遊戲",
        },
        new ManualEntry
        {
            Tag = "devterminal", Glyph = "",
            TitleEn = "Developer & Terminal", TitleZh = "開發與終端機",
            SummaryEn = "winget, Docker, Node/Python/.NET, env vars, ports, and the claude/codex/opencode/gh CLIs.",
            SummaryZh = "winget、Docker、Node/Python/.NET、環境變數、連接埠，同 claude/codex/opencode/gh CLI。",
            StepsEn = new[]
            {
                "Open the Developer & Terminal tool group.",
                "Install or check toolchains (Node, Python, .NET, Docker) and the coding-agent CLIs.",
                "Inspect listening ports and edit environment variables from the same place.",
            },
            StepsZh = new[]
            {
                "開「開發與終端機」工具組。",
                "安裝或者檢查工具鏈（Node、Python、.NET、Docker）同編程代理 CLI。",
                "喺同一個地方檢視監聽中嘅連接埠同改環境變數。",
            },
            Keywords = "developer terminal winget docker node python dotnet ports env cli 開發 終端機 連接埠 環境變數",
        },
        new ManualEntry
        {
            Tag = "browser", Glyph = "",
            TitleEn = "Browser Control", TitleZh = "瀏覽器控制",
            SummaryEn = "Launch Chrome/Edge in any mode, open flags/settings, set policies, and manage profiles and caches.",
            SummaryZh = "用任何模式啟動 Chrome/Edge、開 flags／設定、設定政策、管理設定檔同快取。",
            StepsEn = new[]
            {
                "Open the Browser Control tool group.",
                "Launch Chrome or Edge in normal, incognito, app or kiosk mode.",
                "Open chrome://flags or settings pages, manage profiles, and clear caches.",
            },
            StepsZh = new[]
            {
                "開「瀏覽器控制」工具組。",
                "用普通、無痕、app 或者 kiosk 模式啟動 Chrome 或者 Edge。",
                "開 chrome://flags 或者設定頁、管理設定檔，同清快取。",
            },
            Keywords = "browser chrome edge flags policy profile cache incognito kiosk 瀏覽器 設定檔 快取",
        },
        new ManualEntry
        {
            Tag = "vault", Glyph = "",
            TitleEn = "Encryption & Vault tools", TitleZh = "加密與保險庫工具",
            SummaryEn = "BitLocker, VeraCrypt, EFS/cipher, certificates and advanced Defender/firewall controls.",
            SummaryZh = "BitLocker、VeraCrypt、EFS/cipher、憑證，同進階 Defender／防火牆控制。",
            StepsEn = new[]
            {
                "Open the Encryption & Vault tool group.",
                "Turn BitLocker on/off for a drive, manage EFS/cipher and certificates.",
                "Most encryption actions need admin rights.",
            },
            StepsZh = new[]
            {
                "開「加密與保險庫」工具組。",
                "開關磁碟嘅 BitLocker、管理 EFS/cipher 同憑證。",
                "多數加密操作要管理員權限。",
            },
            TipEn = "For portable encrypted containers, use the WinForge Vault tool instead.",
            TipZh = "想要可攜帶嘅加密容器，改用 WinForge 保險庫工具。",
            Keywords = "bitlocker veracrypt efs cipher certificates encryption 加密 保險庫 憑證",
        },
        new ManualEntry
        {
            Tag = "netpro", Glyph = "",
            TitleEn = "Network Pro", TitleZh = "網絡進階",
            SummaryEn = "Adapters, IP/DNS, Wi-Fi profiles, firewall rules and deep network diagnostics.",
            SummaryZh = "網絡卡、IP/DNS、Wi-Fi 設定檔、防火牆規則同深入網絡診斷。",
            StepsEn = new[]
            {
                "Open the Network Pro tool group.",
                "Inspect adapters, set static IP/DNS, and manage saved Wi-Fi profiles.",
                "Add or review firewall rules and run deeper diagnostics.",
            },
            StepsZh = new[]
            {
                "開「網絡進階」工具組。",
                "檢視網絡卡、設定靜態 IP/DNS，同管理已存 Wi-Fi 設定檔。",
                "加或者檢視防火牆規則，同跑更深入嘅診斷。",
            },
            Keywords = "network adapter ip dns wifi firewall diagnostics 網絡 網絡卡 防火牆 診斷",
        },
        new ManualEntry
        {
            Tag = "info", Glyph = "",
            TitleEn = "System Information", TitleZh = "系統資訊",
            SummaryEn = "A live read-out of your OS build, CPU, RAM, GPU, disk, uptime and activation status.",
            SummaryZh = "即時顯示系統版本、CPU、RAM、GPU、磁碟、運行時間同啟用狀態。",
            StepsEn = new[]
            {
                "Open the System Information category.",
                "Read your OS build, hardware specs, uptime and activation at a glance.",
                "For a styled summary you can copy or export, use the System Info (Winfetch) tool.",
            },
            StepsZh = new[]
            {
                "開「系統資訊」分類。",
                "一眼睇晒系統版本、硬件規格、運行時間同啟用狀態。",
                "想要可以複製或者匯出嘅靚摘要，用系統資訊（Winfetch）工具。",
            },
            Keywords = "system information os build cpu ram gpu disk uptime activation 系統資訊 規格 啟用",
        },
    };
}
