using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 使用手冊嘅一條目（一個功能嘅教學）· One manual entry — a user-facing how-to for a single feature.
/// 永遠雙語：每個欄位都有英文 + 廣東話。Always bilingual: every field carries English + Cantonese.
/// </summary>
public sealed class ManualEntry
{
    /// <summary>對應嘅模組導覽 tag（例如 "module.git"）· The nav tag this how-to opens, or "" if none.</summary>
    public string Tag { get; init; } = "";
    public string Glyph { get; init; } = "";
    public string TitleEn { get; init; } = "";
    public string TitleZh { get; init; } = "";
    /// <summary>一句話講佢做乜 · One-line "what it is".</summary>
    public string SummaryEn { get; init; } = "";
    public string SummaryZh { get; init; } = "";
    /// <summary>逐步點用 · Step-by-step how-to.</summary>
    public string[] StepsEn { get; init; } = Array.Empty<string>();
    public string[] StepsZh { get; init; } = Array.Empty<string>();
    /// <summary>貼士／注意（可選）· Optional tip / caveat.</summary>
    public string? TipEn { get; init; }
    public string? TipZh { get; init; }
    /// <summary>畀手冊搜尋用嘅額外關鍵字 · Extra keywords for the manual's own search.</summary>
    public string Keywords { get; init; } = "";

    public string Haystack =>
        $"{TitleEn} {TitleZh} {SummaryEn} {SummaryZh} {Keywords} {Tag}".ToLowerInvariant();
}

/// <summary>使用手冊嘅一個大章節 · A top-level manual section grouping related how-tos.</summary>
public sealed class ManualSection
{
    public string Id { get; init; } = "";
    public string Glyph { get; init; } = "";
    public string TitleEn { get; init; } = "";
    public string TitleZh { get; init; } = "";
    public string IntroEn { get; init; } = "";
    public string IntroZh { get; init; } = "";
    public List<ManualEntry> Entries { get; init; } = new();
}

/// <summary>
/// 應用程式內建使用手冊嘅全部內容（雙語）· All content for the in-app Instruction Manual / User Guide.
/// 內容由 docs/handoffs 規格同各模組改寫成「點樣用」嘅指引，唔係開發規格。
/// Content is rewritten from docs/handoffs specs and the live modules into user-facing how-to guidance.
/// </summary>
public static partial class ManualContent
{
    public static readonly List<ManualSection> Sections = new()
    {
        GettingStarted(),
        FilesAndDisks(),
        SystemAndDiagnostics(),
        MediaAndCapture(),
        TweaksAndInput(),
        AppsDevAndCloud(),
        SecurityAndPrivacy(),
        Windows11Tweaks(),
    };

    /// <summary>全部條目（用嚟搜尋）· Flattened entries for search.</summary>
    public static IEnumerable<ManualEntry> AllEntries => Sections.SelectMany(s => s.Entries);

    public static int FeatureCount => AllEntries.Count();

    // ===================================================================
    // 1 · Getting started · 入門
    // ===================================================================
    private static ManualSection GettingStarted() => new()
    {
        Id = "getting-started",
        Glyph = "",
        TitleEn = "Getting started",
        TitleZh = "入門",
        IntroEn = "WinForge is an all-in-one control center for Windows 11. It bundles 100+ tools — file utilities, system fixers, media studios, window managers, developer kits and one-click tweaks — each shown in both English and Cantonese. This guide explains how to find a tool, how the window works, and how to use every feature.",
        IntroZh = "WinForge 係一個 Windows 11 全方位控制中心，集合咗 100 幾個工具 — 檔案工具、系統修復、媒體工作室、視窗管理、開發套件同一鍵調校 — 全部都同時用英文同廣東話顯示。呢份手冊會教你點搵工具、視窗點運作，同每個功能點用。",
        Entries =
        {
            new ManualEntry
            {
                Glyph = "", Tag = "",
                TitleEn = "Find anything", TitleZh = "搵嘢",
                SummaryEn = "Search every page and setting from the top of the navigation pane.",
                SummaryZh = "喺導覽窗頂部嘅搜尋框搵任何頁面同設定。",
                StepsEn = new[]
                {
                    "Click the search box at the top of the left navigation pane (or press the Search field).",
                    "Type a tool name, a task, or a keyword — in English or Cantonese.",
                    "Matching pages appear as cards; matching Windows settings appear as live toggles you can flip right in the results.",
                    "Click a card to open that tool in the current tab.",
                },
                StepsZh = new[]
                {
                    "撳左邊導覽窗頂部嘅搜尋框。",
                    "打工具名、想做嘅嘢或者關鍵字 — 英文廣東話都得。",
                    "符合嘅頁面會以卡片顯示；符合嘅 Windows 設定會變成可以即場切換嘅開關。",
                    "撳卡片就會喺目前分頁開個工具。",
                },
                Keywords = "search find filter omnibox",
            },
            new ManualEntry
            {
                Glyph = "", Tag = "",
                TitleEn = "Tabs & sessions", TitleZh = "分頁同工作階段",
                SummaryEn = "WinForge works like a browser: every tab keeps its own page and history.",
                SummaryZh = "WinForge 好似瀏覽器咁：每個分頁有自己嘅頁面同瀏覽記錄。",
                StepsEn = new[]
                {
                    "Press Ctrl+T (or the + button) to open a new tab; Ctrl+W closes the current one.",
                    "Use the back button in the title bar to return to the previous page in that tab.",
                    "Open the session menu (right of the tab strip) to export, import or restore your whole set of tabs.",
                    "Your tabs are saved automatically and reopen next time you launch WinForge.",
                },
                StepsZh = new[]
                {
                    "撳 Ctrl+T（或者 + 掣）開新分頁；Ctrl+W 關閉目前分頁。",
                    "撳標題列嘅返回掣，可以返去該分頁上一個頁面。",
                    "撳分頁列右邊嘅工作階段選單，可以匯出、匯入或者還原成套分頁。",
                    "分頁會自動儲存，下次開 WinForge 會幫你開返。",
                },
                Keywords = "tab session ctrl+t restore export import",
            },
            new ManualEntry
            {
                Glyph = "", Tag = "settings",
                TitleEn = "Language, theme & admin", TitleZh = "語言、主題同管理員",
                SummaryEn = "Both languages always show; Settings picks which one leads, the theme, and admin rights.",
                SummaryZh = "兩種語言永遠一齊顯示；喺設定揀邊個排前面、主題，同管理員權限。",
                StepsEn = new[]
                {
                    "Open Settings (gear, bottom of the navigation pane).",
                    "Under Primary language, pick English or 粵語 — the whole UI re-orders instantly.",
                    "Under App theme, choose Light, Dark or Use system setting.",
                    "If a tool needs system-wide changes, click Relaunch as administrator.",
                },
                StepsZh = new[]
                {
                    "開設定（導覽窗底部嘅齒輪）。",
                    "喺「主要語言」揀英文或者粵語 — 成個介面會即刻重新排序。",
                    "喺「應用程式主題」揀淺色、深色或者跟系統設定。",
                    "如果某個工具要改全系統嘅嘢，撳「以管理員身分重新啟動」。",
                },
                TipEn = "Many tweaks (registry, services, power) need administrator rights to take effect.",
                TipZh = "好多調校（登錄檔、服務、電源）要管理員權限先生效。",
                Keywords = "language cantonese english theme dark light admin elevate settings",
            },
            new ManualEntry
            {
                Glyph = "", Tag = "manual",
                TitleEn = "Using this manual", TitleZh = "點用呢份手冊",
                SummaryEn = "Browse by section, search within the manual, and jump straight to any tool.",
                SummaryZh = "分章節瀏覽、喺手冊內搜尋，同直接跳去任何工具。",
                StepsEn = new[]
                {
                    "Use the table of contents on the left to pick a section, then an entry.",
                    "Type in the manual's search box to filter every how-to by name or keyword.",
                    "Each how-to shows a summary, numbered steps and tips — in both languages.",
                    "Where a how-to has an Open button, click it to jump straight to that tool.",
                },
                StepsZh = new[]
                {
                    "用左邊嘅目錄揀章節，再揀條目。",
                    "喺手冊嘅搜尋框打字，可以按名或者關鍵字篩選所有教學。",
                    "每篇教學都有簡介、編號步驟同貼士 — 兩種語言都有。",
                    "如果教學有「開啟」掣，撳一下就直接跳去個工具。",
                },
                Keywords = "manual guide help toc table of contents how-to",
            },
        },
    };

    // The following section builders are populated below (one method per nav category).
    // Each returns a ManualSection whose Entries are user-facing how-tos.

    private static ManualSection FilesAndDisks() => new()
    {
        Id = "files-disks",
        Glyph = "",
        TitleEn = "Files & disks",
        TitleZh = "檔案與磁碟",
        IntroEn = "Preview, organise, rename, recover and reclaim space — everything that touches your files and drives.",
        IntroZh = "預覽、整理、改名、救援同清出空間 — 所有同你嘅檔案同磁碟有關嘅工具。",
        Entries = FilesEntries(),
    };

    private static ManualSection SystemAndDiagnostics() => new()
    {
        Id = "system",
        Glyph = "",
        TitleEn = "System & diagnostics",
        TitleZh = "系統與診斷",
        IntroEn = "Repair Windows, manage services and tasks, watch performance, and inspect the network down to the packet.",
        IntroZh = "修復 Windows、管理服務同工作、監察效能，仲可以深入到封包層面檢視網絡。",
        Entries = SystemEntries(),
    };

    private static ManualSection MediaAndCapture() => new()
    {
        Id = "media",
        Glyph = "",
        TitleEn = "Media & capture",
        TitleZh = "媒體與擷取",
        IntroEn = "Edit audio and video, download and convert, record the screen, grab GIFs, pick colours and measure pixels.",
        IntroZh = "剪輯音訊同影片、下載同轉檔、錄影、擷取 GIF、取色同量度像素。",
        Entries = MediaEntries(),
    };

    private static ManualSection TweaksAndInput() => new()
    {
        Id = "tweaks-input",
        Glyph = "",
        TitleEn = "Tweaks & input",
        TitleZh = "調校與輸入",
        IntroEn = "Reshape the mouse, keyboard, windows and taskbar — from window tiling to hotkeys, macros and shell mods.",
        IntroZh = "重塑滑鼠、鍵盤、視窗同工作列 — 由視窗平鋪到熱鍵、巨集同 shell 模組。",
        Entries = TweaksEntries(),
    };

    private static ManualSection AppsDevAndCloud() => new()
    {
        Id = "apps-dev",
        Glyph = "",
        TitleEn = "Apps, dev & cloud",
        TitleZh = "程式、開發與雲端",
        IntroEn = "Install software, manage Git and GitHub, run AI agents, drive VMs and Android, and connect to remote services.",
        IntroZh = "安裝軟件、管理 Git 同 GitHub、運行 AI 代理、操控虛擬機同 Android，同連接遠端服務。",
        Entries = AppsEntries(),
    };

    private static ManualSection SecurityAndPrivacy() => new()
    {
        Id = "security",
        Glyph = "",
        TitleEn = "Security & privacy",
        TitleZh = "安全與私隱",
        IntroEn = "Encrypt your data in on-the-fly vaults, and keep secrets safe at rest.",
        IntroZh = "用即時加密嘅保險庫保護你嘅資料，並安全咁儲存機密。",
        Entries = SecurityEntries(),
    };

    private static ManualSection Windows11Tweaks() => new()
    {
        Id = "win11-tweaks",
        Glyph = "",
        TitleEn = "Windows 11 tweaks & recipes",
        TitleZh = "Windows 11 調校與一鍵流程",
        IntroEn = "Hundreds of real Windows settings, organised into categories, plus one-click Recipes that run multi-step chores for you.",
        IntroZh = "幾百個真實 Windows 設定，分門別類，仲有一鍵「流程」幫你做埋多步驟嘅例行工作。",
        Entries = TweaksCatalogEntries(),
    };
}
