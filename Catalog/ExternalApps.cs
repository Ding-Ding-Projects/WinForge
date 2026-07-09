using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Catalog;

/// <summary>
/// 安裝鏈中嘅一個相依項 · One installable step in an app's auto-install chain — a winget package id with a
/// bilingual label. <see cref="Optional"/> deps do not fail the chain if they can't be installed (e.g. a
/// heavy optional backend like WSL for Docker).
/// </summary>
public sealed record AppDependency(string WingetId, string En, string Zh, bool Optional = false);

/// <summary>
/// 一個「塞唔入」WinForge 嘅 app 嘅規格 · Spec for an app that cannot be reimplemented in-app ("stuffed"):
/// it is auto-installed (dependencies-and-all) and launched in its ORIGINAL native form as a popup. Carries
/// the ordered install chain (dependencies first, the app itself last) plus the exe-resolution strategy used
/// to detect and launch the real program. This is the single place to correct a winget id or install path.
/// </summary>
public sealed record ExternalAppSpec
{
    /// <summary>穩定短 id（deep-link / 設定鍵用）· Stable short id used for deep-links + settings keys.</summary>
    public required string Id { get; init; }
    public required string NameEn { get; init; }
    public required string NameZh { get; init; }
    /// <summary>Segoe Fluent / MDL2 圖示字元 · The header glyph character.</summary>
    public required string Glyph { get; init; }
    public required string CategoryEn { get; init; }
    public required string CategoryZh { get; init; }
    public required string DescriptionEn { get; init; }
    public required string DescriptionZh { get; init; }
    /// <summary>官網（顯示連結用）· Homepage (shown as a link, never auto-opened).</summary>
    public string Homepage { get; init; } = "";

    /// <summary>依序安裝鏈：相依項行先，app 本體最後 · Ordered install chain: dependencies first, the app last.</summary>
    public IReadOnlyList<AppDependency> Dependencies { get; init; } = Array.Empty<AppDependency>();

    /// <summary>PATH 上探測嘅檔名主幹（會試 .exe/.cmd）· PATH stems probed with .exe/.cmd (e.g. "blender").</summary>
    public IReadOnlyList<string> PathStems { get; init; } = Array.Empty<string>();
    /// <summary>App Paths 登錄檔嘅 exe 名 · App Paths registry exe names (e.g. "soffice.exe").</summary>
    public IReadOnlyList<string> AppPathsExe { get; init; } = Array.Empty<string>();
    /// <summary>絕對候選路徑（會展開環境變數，可含單個 * 萬用段）· Absolute candidate paths (env vars expanded; may
    /// contain a single <c>*</c> wildcard segment, e.g. <c>%ProgramFiles%\Blender Foundation\*\blender.exe</c>).</summary>
    public IReadOnlyList<string> Candidates { get; init; } = Array.Empty<string>();
    /// <summary>啟動時額外參數（罕有）· Extra launch arguments (rare).</summary>
    public string LaunchArgs { get; init; } = "";

    /// <summary>主套件 id（鏈嘅最後一個）· The primary winget id (last in the chain).</summary>
    public string PrimaryWingetId => Dependencies.Count > 0 ? Dependencies[^1].WingetId : "";

    public string SearchHaystack =>
        $"{Id} {NameEn} {NameZh} {CategoryEn} {CategoryZh} {DescriptionEn} {DescriptionZh} {PrimaryWingetId}"
            .ToLowerInvariant();
}

/// <summary>
/// 「原生 app 彈窗啟動器」目錄 · Catalog of native apps that WinForge installs-and-launches rather than
/// reimplements. Every winget id here was validated against the live winget catalog. Adding an app = add one
/// entry (data only) — the launcher popup, the auto-install chain and the hub page pick it up automatically.
/// </summary>
public static class ExternalApps
{
    // Category label pairs (kept as constants so the whole catalog stays consistent + easy to re-group).
    private const string CatCreative = "Creative & media";
    private const string CatCreativeZh = "創作與媒體";
    private const string CatDev = "Developer & runtime";
    private const string CatDevZh = "開發與執行環境";
    private const string CatVirt = "Virtualization & containers";
    private const string CatVirtZh = "虛擬化與容器";
    private const string CatNet = "Network & security";
    private const string CatNetZh = "網絡與安全";
    private const string CatDocs = "Office & documents";
    private const string CatDocsZh = "辦公與文件";

    private static string G(int code) => ((char)code).ToString();

    public static readonly IReadOnlyList<ExternalAppSpec> All = new List<ExternalAppSpec>
    {
        new()
        {
            Id = "vscode", NameEn = "Visual Studio Code", NameZh = "VS Code 編輯器", Glyph = G(0xE943),
            CategoryEn = CatDev, CategoryZh = CatDevZh,
            DescriptionEn = "Microsoft's cross-platform code editor — too large to reimplement, launched in its native window.",
            DescriptionZh = "Microsoft 跨平台程式碼編輯器 — 太龐大無法重製，以原生視窗啟動。",
            Homepage = "https://code.visualstudio.com/",
            Dependencies = new AppDependency[]
            {
                new("Microsoft.VisualStudioCode", "Visual Studio Code", "VS Code 編輯器"),
            },
            AppPathsExe = new[] { "Code.exe" },
            Candidates = new[]
            {
                @"%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe",
                @"%ProgramFiles%\Microsoft VS Code\Code.exe",
            },
        },
        new()
        {
            Id = "githubdesktop", NameEn = "GitHub Desktop", NameZh = "GitHub Desktop 桌面用戶端", Glyph = G(0xE716),
            CategoryEn = CatDev, CategoryZh = CatDevZh,
            DescriptionEn = "GitHub's native desktop client for repositories, branches, commits, and pull requests.",
            DescriptionZh = "GitHub 原生桌面用戶端，用嚟管理儲存庫、分支、提交同 pull request。",
            Homepage = "https://desktop.github.com/",
            Dependencies = new AppDependency[]
            {
                new("GitHub.GitHubDesktop", "GitHub Desktop", "GitHub Desktop 桌面用戶端"),
            },
            AppPathsExe = new[] { "GitHubDesktop.exe" },
            Candidates = new[]
            {
                @"%LOCALAPPDATA%\GitHubDesktop\app-*\GitHubDesktop.exe",
                @"%LOCALAPPDATA%\GitHubDesktop\GitHubDesktop.exe",
            },
        },
        new()
        {
            Id = "libreoffice", NameEn = "LibreOffice", NameZh = "LibreOffice 辦公套件", Glyph = G(0xE8A5),
            CategoryEn = CatDocs, CategoryZh = CatDocsZh,
            DescriptionEn = "The full LibreOffice suite (Writer / Calc / Impress) in its own window; WinForge also drives soffice headless for conversion.",
            DescriptionZh = "完整 LibreOffice 套件（Writer／Calc／Impress）以自己視窗開啟；WinForge 亦用 soffice 無介面轉檔。",
            Homepage = "https://www.libreoffice.org/",
            Dependencies = new AppDependency[]
            {
                new("TheDocumentFoundation.LibreOffice", "LibreOffice", "LibreOffice 辦公套件"),
            },
            AppPathsExe = new[] { "soffice.exe" },
            Candidates = new[]
            {
                @"%ProgramFiles%\LibreOffice\program\soffice.exe",
                @"%ProgramFiles(x86)%\LibreOffice\program\soffice.exe",
            },
        },
        new()
        {
            Id = "blender", NameEn = "Blender", NameZh = "Blender（3D）", Glyph = G(0xE7F4),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "The Blender 3D creation suite in its native GPU window; WinForge additionally drives it headless for renders.",
            DescriptionZh = "Blender 3D 創作套件以原生 GPU 視窗開啟；WinForge 亦以無介面模式驅動算圖。",
            Homepage = "https://www.blender.org/",
            Dependencies = new AppDependency[]
            {
                new("BlenderFoundation.Blender", "Blender", "Blender"),
            },
            PathStems = new[] { "blender" },
            Candidates = new[] { @"%ProgramFiles%\Blender Foundation\*\blender.exe" },
        },
        new()
        {
            Id = "gimp", NameEn = "GIMP", NameZh = "GIMP 影像編輯", Glyph = G(0xE7C5),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "The GNU Image Manipulation Program — a full raster editor launched in its own window.",
            DescriptionZh = "GNU 影像處理程式 — 完整點陣圖編輯器，以自己視窗啟動。",
            Homepage = "https://www.gimp.org/",
            Dependencies = new AppDependency[]
            {
                new("GIMP.GIMP", "GIMP", "GIMP 影像編輯"),
            },
            AppPathsExe = new[] { "gimp.exe" },
            Candidates = new[]
            {
                @"%ProgramFiles%\GIMP 2\bin\gimp-*.exe",
                @"%ProgramFiles%\GIMP *\bin\gimp-*.exe",
            },
        },
        new()
        {
            Id = "inkscape", NameEn = "Inkscape", NameZh = "Inkscape 向量繪圖", Glyph = G(0xEB9F),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "Professional vector (SVG) illustration — launched natively.",
            DescriptionZh = "專業向量（SVG）繪圖 — 原生啟動。",
            Homepage = "https://inkscape.org/",
            Dependencies = new AppDependency[]
            {
                new("Inkscape.Inkscape", "Inkscape", "Inkscape 向量繪圖"),
            },
            AppPathsExe = new[] { "inkscape.exe" },
            Candidates = new[] { @"%ProgramFiles%\Inkscape\bin\inkscape.exe" },
        },
        new()
        {
            Id = "krita", NameEn = "Krita", NameZh = "Krita 數位繪畫", Glyph = G(0xE790),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "A digital painting studio for artists and illustrators.",
            DescriptionZh = "畀藝術家同插畫師嘅數位繪畫工作室。",
            Homepage = "https://krita.org/",
            Dependencies = new AppDependency[]
            {
                new("KDE.Krita", "Krita", "Krita 數位繪畫"),
            },
            AppPathsExe = new[] { "krita.exe" },
            Candidates = new[]
            {
                @"%ProgramFiles%\Krita (x64)\bin\krita.exe",
                @"%ProgramFiles%\Krita\bin\krita.exe",
            },
        },
        new()
        {
            Id = "darktable", NameEn = "darktable", NameZh = "darktable 相片沖印", Glyph = G(0xE114),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "A RAW photo workflow and non-destructive developer.",
            DescriptionZh = "RAW 相片工作流程同非破壞式沖印。",
            Homepage = "https://www.darktable.org/",
            Dependencies = new AppDependency[]
            {
                new("darktable.darktable", "darktable", "darktable 相片沖印"),
            },
            AppPathsExe = new[] { "darktable.exe" },
            Candidates = new[] { @"%ProgramFiles%\darktable\bin\darktable.exe" },
        },
        new()
        {
            Id = "obs", NameEn = "OBS Studio", NameZh = "OBS 錄影直播", Glyph = G(0xE714),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "Open Broadcaster Software for screen recording and live streaming.",
            DescriptionZh = "Open Broadcaster Software，用嚟螢幕錄影同直播串流。",
            Homepage = "https://obsproject.com/",
            Dependencies = new AppDependency[]
            {
                new("OBSProject.OBSStudio", "OBS Studio", "OBS 錄影直播"),
            },
            // OBS requires its working directory be the exe folder; the launcher sets that.
            Candidates = new[] { @"%ProgramFiles%\obs-studio\bin\64bit\obs64.exe" },
        },
        new()
        {
            Id = "audacity", NameEn = "Audacity", NameZh = "Audacity 音訊編輯", Glyph = G(0xE8D6),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "A multi-track audio recorder and editor.",
            DescriptionZh = "多軌音訊錄音同編輯器。",
            Homepage = "https://www.audacityteam.org/",
            Dependencies = new AppDependency[]
            {
                new("Audacity.Audacity", "Audacity", "Audacity 音訊編輯"),
            },
            AppPathsExe = new[] { "audacity.exe" },
            Candidates = new[] { @"%ProgramFiles%\Audacity\Audacity.exe" },
        },
        new()
        {
            Id = "handbrake", NameEn = "HandBrake", NameZh = "HandBrake 影片轉檔", Glyph = G(0xE714),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "A video transcoder; the required .NET Desktop Runtime is installed first, automatically.",
            DescriptionZh = "影片轉碼器；所需嘅 .NET 桌面執行環境會自動先行安裝。",
            Homepage = "https://handbrake.fr/",
            Dependencies = new AppDependency[]
            {
                new("Microsoft.DotNet.DesktopRuntime.8", ".NET Desktop Runtime 8", ".NET 桌面執行環境 8"),
                new("HandBrake.HandBrake", "HandBrake", "HandBrake 影片轉檔"),
            },
            AppPathsExe = new[] { "HandBrake.exe" },
            Candidates = new[] { @"%ProgramFiles%\HandBrake\HandBrake.exe" },
        },
        new()
        {
            Id = "shotcut", NameEn = "Shotcut", NameZh = "Shotcut 影片剪輯", Glyph = G(0xE8B2),
            CategoryEn = CatCreative, CategoryZh = CatCreativeZh,
            DescriptionEn = "A free, open-source non-linear video editor.",
            DescriptionZh = "免費開源非線性影片剪輯器。",
            Homepage = "https://shotcut.org/",
            Dependencies = new AppDependency[]
            {
                new("Meltytech.Shotcut", "Shotcut", "Shotcut 影片剪輯"),
            },
            Candidates = new[] { @"%ProgramFiles%\Shotcut\shotcut.exe" },
        },
        new()
        {
            Id = "virtualbox", NameEn = "VirtualBox", NameZh = "VirtualBox 虛擬機", Glyph = G(0xE7F8),
            CategoryEn = CatVirt, CategoryZh = CatVirtZh,
            DescriptionEn = "Oracle VirtualBox — a full type-2 hypervisor; its kernel drivers ship in the installer.",
            DescriptionZh = "Oracle VirtualBox — 完整第二型 hypervisor；核心驅動隨安裝程式一齊。",
            Homepage = "https://www.virtualbox.org/",
            Dependencies = new AppDependency[]
            {
                new("Oracle.VirtualBox", "Oracle VirtualBox", "Oracle VirtualBox"),
            },
            AppPathsExe = new[] { "VirtualBox.exe" },
            Candidates = new[] { @"%ProgramFiles%\Oracle\VirtualBox\VirtualBox.exe" },
        },
        new()
        {
            Id = "dockerdesktop", NameEn = "Docker Desktop", NameZh = "Docker Desktop", Glyph = G(0xEC7A),
            CategoryEn = CatVirt, CategoryZh = CatVirtZh,
            DescriptionEn = "Docker Desktop with its GUI; the WSL2 backend is installed first (optional). WinForge also talks to the Engine API directly in the Docker tab.",
            DescriptionZh = "帶 GUI 嘅 Docker Desktop；會先安裝 WSL2 後端（可選）。WinForge 亦喺 Docker 分頁直接連 Engine API。",
            Homepage = "https://www.docker.com/products/docker-desktop/",
            Dependencies = new AppDependency[]
            {
                new("Microsoft.WSL", "Windows Subsystem for Linux (WSL2 backend)", "Windows Linux 子系統（WSL2 後端）", Optional: true),
                new("Docker.DockerDesktop", "Docker Desktop", "Docker Desktop"),
            },
            Candidates = new[] { @"%ProgramFiles%\Docker\Docker\Docker Desktop.exe" },
        },
        new()
        {
            Id = "wireshark", NameEn = "Wireshark", NameZh = "Wireshark 封包分析", Glyph = G(0xEDA3),
            CategoryEn = CatNet, CategoryZh = CatNetZh,
            DescriptionEn = "The Wireshark protocol analyzer; the bundled Npcap capture driver installs with it.",
            DescriptionZh = "Wireshark 協定分析器；隨附嘅 Npcap 擷取驅動會一齊安裝。",
            Homepage = "https://www.wireshark.org/",
            Dependencies = new AppDependency[]
            {
                new("WiresharkFoundation.Wireshark", "Wireshark", "Wireshark 封包分析"),
            },
            AppPathsExe = new[] { "Wireshark.exe" },
            Candidates = new[] { @"%ProgramFiles%\Wireshark\Wireshark.exe" },
        },
    };

    /// <summary>依 id 攞規格（大小寫不敏感）· Look a spec up by id (case-insensitive).</summary>
    public static ExternalAppSpec? ById(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null
        : All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>去重嘅英文分類鍵（穩定排序）· Distinct English category keys (stable order).</summary>
    public static IReadOnlyList<string> CategoryKeys =>
        All.Select(a => a.CategoryEn).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
}
