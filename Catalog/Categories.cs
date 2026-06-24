using WinForge.Models;

namespace WinForge.Catalog;

/// <summary>
/// 全部調校分類 · The fixed set of tweak categories, with bilingual names and Segoe Fluent glyphs.
/// </summary>
public static class Categories
{
    public static readonly AppCategory Appearance = new()
    {
        Id = "appearance",
        Name = new("Appearance & Personalisation", "外觀與個人化"),
        Blurb = new("Dark mode, accent colour, transparency, animations and visual effects.",
            "深色模式、強調色、透明度、動畫同視覺特效。"),
        Glyph = "",
    };

    public static readonly AppCategory Explorer = new()
    {
        Id = "explorer",
        Name = new("File Explorer", "檔案總管"),
        Blurb = new("Show file extensions, hidden files, classic menus and Explorer behaviour.",
            "顯示副檔名、隱藏檔案、經典選單同檔案總管行為。"),
        Glyph = "",
    };

    public static readonly AppCategory Taskbar = new()
    {
        Id = "taskbar",
        Name = new("Taskbar & Start", "工作列與開始功能表"),
        Blurb = new("Taskbar alignment, Search, Widgets, Task View and Start menu layout.",
            "工作列對齊、搜尋、小工具、工作檢視同開始功能表版面。"),
        Glyph = "",
    };

    public static readonly AppCategory Privacy = new()
    {
        Id = "privacy",
        Name = new("Privacy & Telemetry", "私隱與遙測"),
        Blurb = new("Advertising ID, telemetry, activity history, location and tailored ads.",
            "廣告 ID、遙測、活動記錄、定位同個人化廣告。"),
        Glyph = "",
    };

    public static readonly AppCategory Performance = new()
    {
        Id = "performance",
        Name = new("Performance & Power", "效能與電源"),
        Blurb = new("Power plans, hibernation, fast startup, game mode and responsiveness.",
            "電源計劃、休眠、快速啟動、遊戲模式同反應速度。"),
        Glyph = "",
    };

    public static readonly AppCategory Network = new()
    {
        Id = "network",
        Name = new("Network & Internet", "網絡與互聯網"),
        Blurb = new("Flush DNS, reset Winsock, change DNS servers and inspect connections.",
            "清 DNS、重設 Winsock、轉 DNS 伺服器同檢視連線。"),
        Glyph = "",
    };

    public static readonly AppCategory Cleanup = new()
    {
        Id = "cleanup",
        Name = new("Cleanup & Storage", "清理與儲存"),
        Blurb = new("Temp files, caches, Recycle Bin, Windows Update cache and thumbnails.",
            "暫存檔、快取、回收筒、Windows Update 快取同縮圖。"),
        Glyph = "",
    };

    public static readonly AppCategory Security = new()
    {
        Id = "security",
        Name = new("Security", "安全"),
        Blurb = new("UAC, Defender, SmartScreen, firewall and account protections.",
            "UAC、Defender、SmartScreen、防火牆同帳戶保護。"),
        Glyph = "",
    };

    public static readonly AppCategory System = new()
    {
        Id = "system",
        Name = new("System & Boot", "系統與開機"),
        Blurb = new("Long paths, restore points, boot options, clipboard and developer mode.",
            "長路徑、還原點、開機選項、剪貼簿同開發人員模式。"),
        Glyph = "",
    };

    public static readonly AppCategory Apps = new()
    {
        Id = "apps",
        Name = new("Apps & Startup", "應用程式與啟動"),
        Blurb = new("Startup items, winget upgrades, running processes and Explorer restart.",
            "啟動項目、winget 更新、執行中程序同重啟檔案總管。"),
        Glyph = "",
    };

    public static readonly AppCategory PowerTools = new()
    {
        Id = "powertools",
        Name = new("Power Tools", "進階工具"),
        Blurb = new("God Mode, hosts file, restart to UEFI, system repair and quick power actions.",
            "上帝模式、hosts 檔、重啟入 UEFI、系統修復同快速電源動作。"),
        Glyph = "",
    };

    public static readonly AppCategory Launcher = new()
    {
        Id = "launcher",
        Name = new("Launcher & Elevation", "啟動器與提權"),
        Blurb = new("Create a no-UAC elevated launcher via Task Scheduler, and run the suite as admin.",
            "用工作排程器整一個免 UAC 提權啟動器，以管理員身分運行套件。"),
        Glyph = "",
    };

    public static readonly AppCategory Maintenance = new()
    {
        Id = "maintenance",
        Name = new("Maintenance & Diagnostics", "維護與診斷"),
        Blurb = new("Services, scheduled tasks, disk health, SFC/DISM, drivers, updates, event logs and power reports.",
            "服務、排程工作、磁碟健康、SFC/DISM、驅動程式、更新、事件記錄同電源報告。"),
        Glyph = "",
    };

    public static readonly AppCategory Annoyances = new()
    {
        Id = "annoyances",
        Name = new("Debloat & Annoyances", "去煩擾"),
        Blurb = new("Switch off the most-complained-about Windows 11 nags: Copilot, Recall, Bing search, Search Highlights, lock-screen tips and ads.",
            "關掉最多人投訴嘅 Windows 11 煩擾：Copilot、Recall、Bing 搜尋、搜尋醒目提示、鎖機畫面提示同廣告。"),
        Glyph = "",
    };

    public static readonly AppCategory Winaero = new()
    {
        Id = "winaero",
        Name = new("Winaero Tweaks", "Winaero 進階調校"),
        Blurb = new("Advanced Winaero-Tweaker-style tweaks: coloured title bars, snappier menus, classic balloon tips, faster shutdown, lock-screen and boot options and more.",
            "Winaero Tweaker 風格嘅進階調校：彩色標題列、更快選單、傳統氣球提示、加快關機、鎖機畫面同開機選項等等。"),
        Glyph = "",
    };

    public static readonly AppCategory Win11Pro = new()
    {
        Id = "win11pro",
        Name = new("Windows 11 Advanced", "Windows 11 進階"),
        Blurb = new("Power-user tweaks: input precision, storage, performance, boot, Explorer extras and every Settings deep link.",
            "進階調校：輸入精準度、儲存、效能、開機、檔案總管進階同所有設定深層連結。"),
        Glyph = "",
    };

    public static readonly AppCategory RecipesCat = new()
    {
        Id = "recipes",
        Name = new("Recipes (one-click)", "一鍵流程"),
        Blurb = new("Bundled multi-step chores that run with a single button — cleanup, privacy, gaming, dev setup and more.",
            "將多步驟嘅例行工作夾埋一個掣搞掂 — 清理、私隱、遊戲、開發設定等等。"),
        Glyph = "",
        Group = "recipes",
    };

    public static readonly AppCategory DevTerminal = new()
    {
        Id = "devterminal",
        Name = new("Developer & Terminal", "開發與終端機"),
        Blurb = new("winget, Docker, Node/Python/.NET, env vars, ports, and the claude/codex/opencode/gh CLIs.",
            "winget、Docker、Node/Python/.NET、環境變數、連接埠，同 claude/codex/opencode/gh CLI。"),
        Glyph = "",
        Group = "tools",
    };

    public static readonly AppCategory Browser = new()
    {
        Id = "browser",
        Name = new("Browser Control", "瀏覽器控制"),
        Blurb = new("Launch Chrome/Edge in any mode, open flags/settings, set policies, manage profiles and caches.",
            "用任何模式啟動 Chrome/Edge、開 flags／設定、設定政策、管理設定檔同快取。"),
        Glyph = "",
        Group = "tools",
    };

    public static readonly AppCategory Vault = new()
    {
        Id = "vault",
        Name = new("Encryption & Vault", "加密與保險庫"),
        Blurb = new("BitLocker, VeraCrypt, EFS/cipher, certificates and advanced Defender/firewall controls.",
            "BitLocker、VeraCrypt、EFS/cipher、憑證，同進階 Defender／防火牆控制。"),
        Glyph = "",
        Group = "tools",
    };

    public static readonly AppCategory NetPro = new()
    {
        Id = "netpro",
        Name = new("Network Pro", "網絡進階"),
        Blurb = new("Adapters, IP/DNS, Wi-Fi profiles, firewall rules and deep network diagnostics.",
            "網絡卡、IP/DNS、Wi-Fi 設定檔、防火牆規則同深入網絡診斷。"),
        Glyph = "",
        Group = "tools",
    };

    public static readonly AppCategory Info = new()
    {
        Id = "info",
        Name = new("System Information", "系統資訊"),
        Blurb = new("Live read-out of OS build, CPU, RAM, GPU, disk, uptime and activation.",
            "即時顯示系統版本、CPU、RAM、GPU、磁碟、運行時間同啟用狀態。"),
        Glyph = "",
    };

    /// <summary>顯示次序 · Display order in the navigation pane.</summary>
    public static readonly AppCategory[] All =
    {
        Appearance, Explorer, Taskbar, Privacy, Performance, Network,
        Cleanup, Security, System, Apps, PowerTools, Launcher, Maintenance, Win11Pro, Annoyances, Winaero, Info,
        RecipesCat,
        DevTerminal, Browser, Vault, NetPro,
    };
}
