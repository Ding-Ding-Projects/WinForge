using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

public sealed record LicenseNotice
{
    public required string Name { get; init; }
    public required string License { get; init; }
    public required string SourceUrl { get; init; }
    public string LicenseUrl { get; init; } = "";
    public string ModuleTag { get; init; } = "";
    public string CategoryEn { get; init; } = "Third-party component";
    public string CategoryZh { get; init; } = "第三方元件";
    public string UseEn { get; init; } = "";
    public string UseZh { get; init; } = "";
    public string ObligationEn { get; init; } = "";
    public string ObligationZh { get; init; } = "";

    public bool IsCopyleftOrSourceAvailable =>
        License.Contains("GPL", StringComparison.OrdinalIgnoreCase)
        || License.Contains("AGPL", StringComparison.OrdinalIgnoreCase)
        || License.Contains("LGPL", StringComparison.OrdinalIgnoreCase)
        || License.Contains("MPL", StringComparison.OrdinalIgnoreCase)
        || License.Contains("BUSL", StringComparison.OrdinalIgnoreCase)
        || License.Contains("source", StringComparison.OrdinalIgnoreCase)
        || License.Contains("custom", StringComparison.OrdinalIgnoreCase);

    public string Haystack =>
        $"{Name} {License} {SourceUrl} {LicenseUrl} {ModuleTag} {CategoryEn} {CategoryZh} {UseEn} {UseZh} {ObligationEn} {ObligationZh}"
            .ToLowerInvariant();
}

public static class LicenseCatalogService
{
    public static readonly LicenseNotice[] Notices =
    {
        Notice("WinForge", "MIT", "https://github.com/codingmachineedge/WinForge", "https://github.com/codingmachineedge/WinForge/blob/main/LICENSE",
            "Application", "應用程式", "", "WinForge app source.", "WinForge app 原始碼。",
            "Keep the MIT license and copyright notice with redistributed copies.", "重新散佈時保留 MIT 授權同版權聲明。"),
        Notice("Devolutions UniGetUI (pinned 21116375)", "MIT", "https://github.com/Devolutions/UniGetUI/tree/21116375c8299d1db38a3c3b4c2eb7e18bc97c4e", "https://github.com/Devolutions/UniGetUI/blob/21116375c8299d1db38a3c3b4c2eb7e18bc97c4e/LICENSE",
            "Vendored reference source", "隨附參考原始碼", "module.packages",
            "Complete upstream source snapshot used as the auditable specification for WinForge's native Package Manager port; the upstream executable is not launched or published with WinForge.",
            "完整上游原始碼快照，作為 WinForge 原生套件管理移植嘅可審核規格；WinForge 唔會啟動或發佈上游執行檔。",
            "Preserve the upstream MIT copyright and permission notice; separately honor notices embedded in upstream third-party files.",
            "保留上游 MIT 版權同許可聲明，並另外遵守上游第三方檔案內附嘅聲明。"),
        Notice("Dew Encryption (pinned a207c742)", "MIT", "https://github.com/codingmachineedge/dew-encryption/tree/a207c7424f203ef0ea88bba825d51b15aba30939", "https://github.com/codingmachineedge/dew-encryption/blob/a207c7424f203ef0ea88bba825d51b15aba30939/LICENSE",
            "Vendored reference source", "隨附參考原始碼", "module.dew-encryption",
            "Complete upstream source snapshot used as the auditable specification for WinForge's native Dew-compatible history and encrypted-archive port; upstream Python and Avalonia executables are not launched or published.",
            "完整上游原始碼快照，作為 WinForge 原生 Dew 相容歷史同加密封存移植嘅可審核規格；唔會啟動或發佈上游 Python／Avalonia 執行檔。",
            "Preserve the upstream MIT copyright and permission notice with redistributed source snapshots.",
            "重新散佈原始碼快照時保留上游 MIT 版權同許可聲明。"),
        Notice("SharpSevenZip 2.0.107", "LGPL-3.0-or-later", "https://github.com/JeremyAnsel/SharpSevenZip/tree/ee5afbecd451cee2ab1211c9859eb6501abfa699", "https://github.com/JeremyAnsel/SharpSevenZip/blob/ee5afbecd451cee2ab1211c9859eb6501abfa699/LICENSE",
            "Library", "程式庫", "module.dew-encryption",
            "In-process wrapper around the user's installed 7z.dll so encrypted Dew archive passwords never enter a child-process command line, environment, or response file.",
            "程序內包裝用戶已安裝嘅 7z.dll，令 Dew 加密封存密碼唔會進入子程序命令列、環境或者回應檔。",
            "Keep the LGPL notice and permit replacement or relinking of the dynamically loaded library.",
            "保留 LGPL 聲明，並容許替換或重新連結動態載入程式庫。"),
        Notice("Microsoft Windows App SDK", "Microsoft / WinAppSDK redistributable terms", "https://github.com/microsoft/WindowsAppSDK", "https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE",
            "Runtime", "執行階段", "", "WinUI 3 desktop UI framework.", "WinUI 3 桌面 UI 框架。"),
        Notice("Microsoft WebView2", "Microsoft WebView2 SDK license", "https://github.com/MicrosoftEdge/WebView2Samples", "https://www.nuget.org/packages/Microsoft.Web.WebView2",
            "Runtime", "執行階段", "module.weblogin", "Embedded browser control for in-app login and previews.", "內嵌瀏覽器控制項，畀 app 內登入同預覽用。"),
        Notice("SSH.NET", "MIT", "https://github.com/sshnet/SSH.NET", "https://github.com/sshnet/SSH.NET/blob/develop/LICENSE",
            "Library", "程式庫", "module.ssh", "Managed SSH/SFTP engine.", "受控 SSH/SFTP 引擎。"),
        Notice("FluentFTP", "MIT", "https://github.com/robinrodricks/FluentFTP", "https://github.com/robinrodricks/FluentFTP/blob/master/LICENSE",
            "Library", "程式庫", "module.filezilla", "Managed FTP/FTPS client.", "受控 FTP/FTPS 用戶端。"),
        Notice("MailKit / MimeKit", "MIT", "https://github.com/jstedfast/MailKit", "https://github.com/jstedfast/MailKit/blob/master/LICENSE",
            "Library", "程式庫", "module.mail", "IMAP, SMTP and MIME support.", "IMAP、SMTP 同 MIME 支援。"),
        Notice("LibVLCSharp / libVLC", "LGPL-2.1-or-later", "https://github.com/videolan/libvlcsharp", "https://github.com/videolan/libvlcsharp/blob/3.x/LICENSE",
            "Library", "程式庫", "module.mediaplayer", "Embedded media playback engine.", "內嵌媒體播放引擎。",
            "Keep LGPL notices and allow users to replace or inspect LGPL components where bundled.", "保留 LGPL 聲明；如有捆綁，需容許用戶檢視或替換 LGPL 元件。"),
        Notice("MonoTorrent", "MIT", "https://github.com/alanmcgovern/monotorrent", "https://github.com/alanmcgovern/monotorrent/blob/master/LICENSE",
            "Library", "程式庫", "module.torrent", "Managed BitTorrent engine.", "受控 BitTorrent 引擎。"),
        Notice("Docker.DotNet", "MIT", "https://github.com/dotnet/Docker.DotNet", "https://github.com/dotnet/Docker.DotNet/blob/main/LICENSE",
            "Library", "程式庫", "module.docker", "Docker Engine API client.", "Docker Engine API 用戶端。"),
        Notice("YamlDotNet", "MIT", "https://github.com/aaubry/YamlDotNet", "https://github.com/aaubry/YamlDotNet/blob/master/LICENSE.txt",
            "Library", "程式庫", "module.docker", "YAML parsing for compose-like files.", "解析 compose 類 YAML 檔。"),
        Notice("Microsoft.Data.Sqlite", "MIT", "https://github.com/dotnet/efcore", "https://github.com/dotnet/efcore/blob/main/LICENSE.txt",
            "Library", "程式庫", "module.sqlitebrowser", "SQLite database access.", "SQLite 資料庫存取。"),
        Notice("SQLite", "Public domain", "https://sqlite.org", "https://sqlite.org/copyright.html",
            "Library", "程式庫", "module.sqlitebrowser", "Native SQLite engine included through SQLitePCLRaw dependency chain.", "透過 SQLitePCLRaw 相依鏈包含原生 SQLite 引擎。"),
        Notice("PDFsharp", "MIT", "https://github.com/empira/PDFsharp", "https://github.com/empira/PDFsharp/blob/master/LICENSE",
            "Library", "程式庫", "module.pdftoolkit", "PDF merge, split, rotate and watermark operations.", "PDF 合併、分割、旋轉同浮水印操作。"),
        Notice("PdfPig", "Apache-2.0", "https://github.com/UglyToad/PdfPig", "https://github.com/UglyToad/PdfPig/blob/master/LICENSE",
            "Library", "程式庫", "module.pdftoolkit", "PDF text extraction.", "PDF 文字抽取。"),
        Notice("TagLibSharp", "LGPL-2.1", "https://github.com/mono/taglib-sharp", "https://github.com/mono/taglib-sharp/blob/main/COPYING",
            "Library", "程式庫", "module.audiotagger", "Audio metadata read/write engine.", "音訊中繼資料讀寫引擎。",
            "Keep LGPL notices for redistributed binaries.", "重新散佈二進位時保留 LGPL 聲明。"),
        Notice("SixLabors ImageSharp", "Apache-2.0", "https://github.com/SixLabors/ImageSharp", "https://github.com/SixLabors/ImageSharp/blob/main/LICENSE",
            "Library", "程式庫", "module.imageeditor", "Managed raster image processing.", "受控點陣圖影像處理。"),
        Notice("ICSharpCode.Decompiler", "MIT", "https://github.com/icsharpcode/ILSpy", "https://github.com/icsharpcode/ILSpy/blob/master/doc/ILSpyAboutPage.txt",
            "Library", "程式庫", "module.decompiler", "ILSpy decompiler engine used in-process.", "程序內使用 ILSpy 反編譯引擎。"),
        Notice("LibreHardwareMonitorLib", "MPL-2.0", "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor", "https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE",
            "Library", "程式庫", "module.battery", "Hardware sensor data.", "硬件感測器資料。",
            "MPL-covered files keep MPL notices when redistributed or modified.", "重新散佈或修改 MPL 檔案時保留 MPL 聲明。"),
        Notice("QRCoder", "MIT", "https://github.com/codebude/QRCoder", "https://github.com/codebude/QRCoder/blob/master/LICENSE.txt",
            "Library", "程式庫", "module.quicktype", "Offline QR-code generation where used.", "需要時離線產生 QR code。"),
        Notice("Konscious Argon2", "MIT", "https://github.com/kmaragon/Konscious.Security.Cryptography", "https://github.com/kmaragon/Konscious.Security.Cryptography/blob/master/LICENSE",
            "Library", "程式庫", "module.keepass", "Argon2 password KDF support.", "Argon2 密碼 KDF 支援。"),
        Notice("Audacity", "GPL-2.0-or-later", "https://github.com/audacity/audacity", "https://github.com/audacity/audacity/blob/master/LICENSE.txt",
            "Reference", "參考", "module.audioeditor", "Behavioral reference for the native audio editor; do not paste upstream C++ source unless the whole distribution plan satisfies GPL.", "原生音訊編輯器嘅行為參考；除非整個散佈方案滿足 GPL，否則唔直接貼上游 C++ 原始碼。",
            "If GPL code is ported or bundled, provide corresponding source and GPL notices.", "如移植或捆綁 GPL 程式碼，需提供對應原始碼同 GPL 聲明。"),
        Notice("qBittorrent", "GPL-2.0-or-later", "https://github.com/qbittorrent/qBittorrent", "https://github.com/qbittorrent/qBittorrent/blob/master/COPYING",
            "Reference / optional API peer", "參考／可選 API 對象", "module.qbittorrent", "Reference for torrent client behavior; native torrent module uses MonoTorrent.", "種子客戶端行為參考；原生種子模組使用 MonoTorrent。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("Amulet Map Editor", "GPL-3.0", "https://github.com/Amulet-Team/Amulet-Map-Editor", "https://github.com/Amulet-Team/Amulet-Map-Editor/blob/master/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.amulet", "Minecraft world editor reference.", "Minecraft 世界編輯器參考。",
            "Bundled GPL code must keep GPL notices and source availability.", "捆綁 GPL 程式碼時需保留 GPL 聲明同原始碼可得性。"),
        Notice("ViaProxy", "GPL-3.0", "https://github.com/ViaVersion/ViaProxy", "https://github.com/ViaVersion/ViaProxy/blob/master/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.viaproxy", "Minecraft protocol proxy reference.", "Minecraft 協定代理參考。",
            "Bundled GPL code must keep GPL notices and source availability.", "捆綁 GPL 程式碼時需保留 GPL 聲明同原始碼可得性。"),
        Notice("RustDesk", "AGPL-3.0", "https://github.com/rustdesk/rustdesk", "https://github.com/rustdesk/rustdesk/blob/master/LICENCE",
            "Reference / integration target", "參考／整合對象", "module.rustdesk", "Remote desktop reference.", "遠端桌面參考。",
            "AGPL modifications or network services require source availability to users.", "AGPL 修改或網絡服務需向用戶提供原始碼。"),
        Notice("WorldMonitor", "AGPL-3.0-only", "https://github.com/koala73/worldmonitor", "",
            "Reference", "參考", "module.worldmonitor", "World monitor reference.", "世界監察參考。",
            "AGPL modifications or network services require source availability to users.", "AGPL 修改或網絡服務需向用戶提供原始碼。"),
        Notice("Windhawk", "GPL-3.0", "https://github.com/ramensoftware/windhawk", "https://github.com/ramensoftware/windhawk/blob/master/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.windhawk", "Windows customization mod manager reference.", "Windows 自訂模組管理參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("Rainmeter", "GPL-2.0", "https://github.com/rainmeter/rainmeter", "https://github.com/rainmeter/rainmeter/blob/master/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.rainmeter", "Desktop widget manager reference.", "桌面小工具管理參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("TestDisk / PhotoRec", "GPL-2.0-or-later", "https://git.cgsecurity.org/cgit/testdisk", "https://git.cgsecurity.org/cgit/testdisk/tree/COPYING",
            "Reference / integration target", "參考／整合對象", "module.testdisk", "Data recovery reference.", "資料救援參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("Wireshark", "GPL-2.0-or-later", "https://gitlab.com/wireshark/wireshark", "https://gitlab.com/wireshark/wireshark/-/blob/master/COPYING",
            "Reference / integration target", "參考／整合對象", "module.wireshark", "Packet capture reference.", "封包擷取參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("Nmap", "NPSL / GPL-derived", "https://github.com/nmap/nmap", "https://github.com/nmap/nmap/blob/master/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.nmap", "Network scanner reference.", "網絡掃描參考。",
            "Follow Nmap license terms for redistributed source or binaries.", "重新散佈原始碼或二進位時遵守 Nmap 授權條款。"),
        Notice("Rufus", "GPL-3.0", "https://github.com/pbatard/rufus", "https://github.com/pbatard/rufus/blob/master/LICENSE.txt",
            "Reference", "參考", "module.imaging", "USB imaging reference.", "USB 燒錄參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("AltSnap", "GPL-2.0-or-later", "https://github.com/RamonUnch/AltSnap", "https://github.com/RamonUnch/AltSnap/blob/master/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.altsnap", "Alt-drag window management reference.", "Alt 拖曳視窗管理參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("GlazeWM", "GPL-3.0", "https://github.com/glzr-io/glazewm", "https://github.com/glzr-io/glazewm/blob/main/LICENSE.md",
            "Reference / integration target", "參考／整合對象", "module.glazewm", "Tiling window manager reference.", "平鋪視窗管理參考。",
            "GPL source/binary use requires GPL notices and corresponding source.", "使用 GPL 原始碼或二進位需要 GPL 聲明同對應原始碼。"),
        Notice("Packer", "BUSL-1.1 source-available", "https://github.com/hashicorp/packer", "https://github.com/hashicorp/packer/blob/main/LICENSE",
            "Reference / integration target", "參考／整合對象", "module.packer", "Image builder workflow reference.", "映像建置流程參考。",
            "BUSL is source-available, not MIT-style permissive; keep license/source disclosure and avoid copying restricted code unless permitted.", "BUSL 係 source-available，唔係 MIT 式寬鬆授權；保留授權／來源披露，未獲允許唔複製受限程式碼。"),
        Notice("Komorebi", "Custom source-available license", "https://github.com/LGUG2Z/komorebi", "https://github.com/LGUG2Z/komorebi/blob/master/LICENCE.md",
            "Reference / integration target", "參考／整合對象", "module.komorebi", "Tiling window manager reference.", "平鋪視窗管理參考。",
            "Honor custom source-available/commercial-use terms.", "遵守自訂 source-available／商用條款。"),
        Notice("Aseprite", "Source-available Aseprite EULA", "https://github.com/aseprite/aseprite", "https://github.com/aseprite/aseprite/blob/main/EULA.txt",
            "Reference", "參考", "module.pixeleditor", "Pixel editor reference.", "像素畫編輯器參考。",
            "Do not redistribute copied Aseprite code/assets unless the EULA permits that use.", "除非 EULA 容許，否則唔重新散佈複製嘅 Aseprite 程式碼／資產。"),
    };

    public static IReadOnlyList<string> CategoryKeys =>
        Notices.Select(n => n.CategoryEn).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

    public static IEnumerable<LicenseNotice> Search(string query, string category, bool copyleftOnly)
    {
        var q = (query ?? "").Trim().ToLowerInvariant();
        return Notices
            .Where(n => string.IsNullOrEmpty(category) || n.CategoryEn.Equals(category, StringComparison.OrdinalIgnoreCase))
            .Where(n => !copyleftOnly || n.IsCopyleftOrSourceAvailable)
            .Where(n => q.Length == 0 || n.Haystack.Contains(q))
            .OrderBy(n => n.CategoryEn)
            .ThenBy(n => n.Name);
    }

    private static LicenseNotice Notice(string name, string license, string sourceUrl, string licenseUrl,
        string categoryEn, string categoryZh, string moduleTag, string useEn, string useZh,
        string obligationEn = "", string obligationZh = "") => new()
    {
        Name = name,
        License = license,
        SourceUrl = sourceUrl,
        LicenseUrl = licenseUrl,
        CategoryEn = categoryEn,
        CategoryZh = categoryZh,
        ModuleTag = moduleTag,
        UseEn = useEn,
        UseZh = useZh,
        ObligationEn = obligationEn,
        ObligationZh = obligationZh,
    };
}
