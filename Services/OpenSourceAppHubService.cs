using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

public sealed record NativeOssCloneInfo
{
    public required string NameEn { get; init; }
    public required string NameZh { get; init; }
    public required string InspiredBy { get; init; }
    public required string CategoryEn { get; init; }
    public required string CategoryZh { get; init; }
    public required string DescriptionEn { get; init; }
    public required string DescriptionZh { get; init; }
    public required string ModuleTag { get; init; }
    public required string PageAlias { get; init; }
    public required string StatusEn { get; init; }
    public required string StatusZh { get; init; }
    public required string ImplementationEn { get; init; }
    public required string ImplementationZh { get; init; }
    public string[] Tags { get; init; } = Array.Empty<string>();

    public string SearchHaystack =>
        $"{NameEn} {NameZh} {InspiredBy} {CategoryEn} {CategoryZh} {DescriptionEn} {DescriptionZh} {PageAlias} {StatusEn} {StatusZh} {ImplementationEn} {ImplementationZh} {string.Join(' ', Tags)}"
            .ToLowerInvariant();
}

/// <summary>
/// Native open-source-inspired modules already baked into WinForge.
/// This catalog intentionally contains in-app C# tabs only: no installer-only entries and no external launchers.
/// </summary>
public static class OpenSourceAppHubService
{
    public static readonly NativeOssCloneInfo[] Catalog =
    {
        Clone("API Client", "REST API 用戶端", "Postman / Insomnia",
            "Developer & data", "開發與數據", "Build, send and save REST requests in-app with collections and environments.",
            "喺 app 內建立、發送同儲存 REST 請求，支援集合與環境變數。", "module.apiclient", "api",
            "Native tab", "原生分頁", "C# HttpClient engine with JSON workspace persistence.",
            "C# HttpClient 引擎，加 JSON 工作區持久化。", "rest", "http", "api", "postman"),
        Clone("Diff & Merge", "比對與合併", "WinMerge",
            "Developer & data", "開發與數據", "Side-by-side file and folder diff/merge with patch export.",
            "並排檔案／資料夾比對與合併，可匯出 patch。", "module.diffmerge", "diff",
            "Native tab", "原生分頁", "C# text/folder comparison surface inside WinForge.",
            "WinForge 內建 C# 文字／資料夾比對介面。", "diff", "merge", "winmerge"),
        Clone("Diagram Editor", "圖表編輯器", "draw.io / diagrams.net",
            "Developer & data", "開發與數據", "Flowchart and diagram canvas with JSON/PNG export.",
            "流程圖同圖表畫布，可匯出 JSON／PNG。", "module.diagram", "diagram",
            "Native tab", "原生分頁", "WinUI canvas model with local serialization.",
            "WinUI 畫布模型，加本機序列化。", "diagram", "flowchart", "drawio"),
        Clone(".NET Decompiler", ".NET 反編譯器", "ILSpy",
            "Developer & data", "開發與數據", "Browse assemblies and decompile IL to readable C#.",
            "瀏覽組件並將 IL 反編譯成可讀 C#。", "module.decompiler", "decompiler",
            "Native tab", "原生分頁", "ICSharpCode.Decompiler runs in-process; no ILSpy executable.",
            "ICSharpCode.Decompiler 喺程序內運行；唔啟動 ILSpy exe。", "ilspy", "decompile", "dotnet"),
        Clone("SQLite Browser", "SQLite 資料庫瀏覽器", "DB Browser for SQLite",
            "Developer & data", "開發與數據", "Open SQLite files, inspect schema, edit rows and run SQL.",
            "開 SQLite 檔、檢視結構、編輯資料列同執行 SQL。", "module.sqlitebrowser", "sqlite",
            "Native tab", "原生分頁", "Microsoft.Data.Sqlite-backed browser/editor.",
            "以 Microsoft.Data.Sqlite 驅動嘅瀏覽／編輯器。", "sqlite", "database", "sql"),
        Clone("Feed Reader", "RSS 閱讀器", "QuiteRSS / Fluent Reader",
            "Documents & knowledge", "文件與知識", "Subscribe to RSS/Atom feeds, refresh articles and read summaries in-app.",
            "訂閱 RSS／Atom feed、重新整理文章，並喺 app 內閱讀摘要。", "module.feedreader", "rss",
            "New native tab", "新增原生分頁", "C# HttpClient + XML parser with local JSON feed storage.",
            "C# HttpClient + XML 解析器，加本機 JSON feed 儲存。", "rss", "atom", "news", "reader"),
        Clone("Flashcards", "間隔重複記憶卡", "Anki",
            "Documents & knowledge", "文件與知識", "Decks, cards, CSV import/export and SM-2 review scheduling.",
            "牌組、卡片、CSV 匯入／匯出同 SM-2 複習排程。", "module.flashcards", "flashcards",
            "Native tab", "原生分頁", "Managed C# scheduler and JSON deck store.",
            "受控 C# 排程器同 JSON 牌組儲存。", "anki", "srs", "study"),
        Clone("PDF Toolkit", "PDF 工具箱", "Stirling-PDF / PDFsam",
            "Media & documents", "媒體與文件", "Merge, split, rotate, watermark, encrypt and extract from PDFs.",
            "合併、分割、旋轉、加浮水印、加密同抽取 PDF。", "module.pdftoolkit", "pdf",
            "Native tab", "原生分頁", "PDFsharp and PdfPig run inside WinForge.",
            "PDFsharp 同 PdfPig 喺 WinForge 內運行。", "pdf", "pdfsam", "stirling"),
        Clone("Audio Tagger", "音訊標籤編輯器", "Mp3tag / Kid3",
            "Media & documents", "媒體與文件", "Batch-edit audio metadata and cover art.",
            "批次編輯音訊中繼資料同封面圖。", "module.audiotagger", "tags",
            "Native tab", "原生分頁", "TagLibSharp metadata engine in-process.",
            "TagLibSharp 中繼資料引擎喺程序內運行。", "mp3tag", "id3", "flac"),
        Clone("Image Editor", "點陣圖影像編輯器", "GIMP / Paint.NET",
            "Media & documents", "媒體與文件", "Open images, adjust color, apply filters, crop, resize and layer edits.",
            "開圖、調色、套濾鏡、裁切、縮放同圖層編輯。", "module.imageeditor", "imageeditor",
            "Native tab", "原生分頁", "SixLabors.ImageSharp processing in managed C#.",
            "SixLabors.ImageSharp 以受控 C# 處理影像。", "gimp", "paint", "image"),
        Clone("Text Extractor", "原生文字辨識", "NormCap / PowerToys Text Extractor",
            "Media & documents", "媒體與文件", "OCR a screen region or image file using Windows OCR.",
            "用 Windows OCR 辨識螢幕區域或圖片檔。", "module.textocr", "ocr",
            "Native tab", "原生分頁", "Windows.Media.Ocr WinRT engine, no Tesseract executable.",
            "Windows.Media.Ocr WinRT 引擎，無 Tesseract exe。", "ocr", "text", "normcap"),
        Clone("KeePass Vault", "密碼保險庫", "KeePass / KeePassXC",
            "Security & privacy", "安全與私隱", "Open and manage local KDBX password databases.",
            "開啟同管理本機 KDBX 密碼資料庫。", "module.keepass", "keepass",
            "Native tab", "原生分頁", "KDBX parser/crypto in managed C# with Argon2 support.",
            "受控 C# KDBX 解析／加密，支援 Argon2。", "keepass", "kdbx", "password"),
        Clone("Native Torrent", "原生種子下載", "qBittorrent / Transmission",
            "Network & transfer", "網絡與傳輸", "Download magnets/torrents with an in-process BitTorrent engine.",
            "用程序內 BitTorrent 引擎下載磁力／種子。", "module.torrent", "torrent",
            "Native tab", "原生分頁", "MonoTorrent engine, no qBittorrent process required.",
            "MonoTorrent 引擎，唔需要 qBittorrent 程序。", "torrent", "magnet", "bittorrent"),
        Clone("Docker", "Docker 容器管理", "Docker Desktop / Portainer",
            "Virtualization & containers", "虛擬化與容器", "Manage containers, images, volumes, networks and compose stacks.",
            "管理容器、映像、磁碟區、網路同 compose stack。", "module.docker", "docker",
            "Native tab", "原生分頁", "Docker.DotNet talks to Docker Engine API directly.",
            "Docker.DotNet 直接連 Docker Engine API。", "docker", "container", "portainer"),
        Clone("Process Explorer", "程序總管", "Sysinternals Process Explorer",
            "System utilities", "系統工具", "Inspect process trees, paths, command lines, CPU, memory and modules.",
            "檢視程序樹、路徑、命令列、CPU、記憶體同模組。", "module.procexp", "procexp",
            "Native tab", "原生分頁", "C# process/WMI/module inspection inside WinForge.",
            "WinForge 內建 C# 程序／WMI／模組檢視。", "process", "taskmanager", "sysinternals"),
        Clone("Disk Health", "硬碟健康", "CrystalDiskInfo",
            "System utilities", "系統工具", "Show SMART health, temperatures and disk warning signals.",
            "顯示 SMART 健康、溫度同硬碟警號。", "module.diskhealth", "diskhealth",
            "Native tab", "原生分頁", "Native storage counters and SMART collection.",
            "原生儲存計數器與 SMART 收集。", "smart", "disk", "crystaldiskinfo"),
        Clone("Disk Benchmark", "硬碟速度測試", "CrystalDiskMark",
            "System utilities", "系統工具", "Run sequential/random disk speed tests from WinForge.",
            "喺 WinForge 內執行循序／隨機磁碟速度測試。", "module.diskbench", "diskbench",
            "Native tab", "原生分頁", "C# benchmark runner with managed result UI.",
            "C# 測速執行器，加受控結果介面。", "benchmark", "disk", "crystaldiskmark"),
        Clone("Everything Search", "即時檔案搜尋", "Everything",
            "Files & disks", "檔案與磁碟", "Instant NTFS filename index/search inside WinForge.",
            "喺 WinForge 入面做即時 NTFS 檔名索引／搜尋。", "module.everything", "everything",
            "Native tab", "原生分頁", "In-app index/search surface, no Everything UI redirect.",
            "App 內索引／搜尋介面，唔跳去 Everything UI。", "search", "files", "ntfs"),
    };

    public static IReadOnlyList<string> CategoryKeys =>
        Catalog.Select(a => a.CategoryEn).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

    private static NativeOssCloneInfo Clone(string nameEn, string nameZh, string inspiredBy,
        string categoryEn, string categoryZh, string descEn, string descZh,
        string moduleTag, string alias, string statusEn, string statusZh,
        string implEn, string implZh, params string[] tags) => new()
    {
        NameEn = nameEn,
        NameZh = nameZh,
        InspiredBy = inspiredBy,
        CategoryEn = categoryEn,
        CategoryZh = categoryZh,
        DescriptionEn = descEn,
        DescriptionZh = descZh,
        ModuleTag = moduleTag,
        PageAlias = alias,
        StatusEn = statusEn,
        StatusZh = statusZh,
        ImplementationEn = implEn,
        ImplementationZh = implZh,
        Tags = tags,
    };
}
