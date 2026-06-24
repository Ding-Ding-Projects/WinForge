using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 包住 LibreOffice (soffice) · A wrapper over the LibreOffice command line (soffice.com /
/// soffice.exe). 用嚟做無介面批次轉檔（--headless --convert-to）同埋開檔編輯。
/// Drives headless batch conversion and "open for editing"; resolves soffice from the registry
/// and known paths (it is normally NOT on PATH), and always uses an isolated user profile so
/// conversions work even while the desktop app is running.
/// </summary>
public static class LibreOfficeService
{
    private static string? _com;   // soffice.com — gives real stdout/exit codes on Windows
    private static string? _exe;   // soffice.exe — used for "open for editing"

    private static readonly string[] ProgramDirs =
    {
        @"C:\Program Files\LibreOffice\program",
        @"C:\Program Files (x86)\LibreOffice\program",
    };

    /// <summary>由登錄檔搵 LibreOffice 安裝資料夾 · Resolve the install dir from the registry.</summary>
    private static string? InstallDirFromRegistry()
    {
        // App Paths is the most reliable place LibreOffice registers soffice.exe.
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var k = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe");
                var path = k?.GetValue(null)?.ToString();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return Path.GetDirectoryName(path);
            }
            catch { }
        }
        // LibreOffice's own key (value "" / "Path" points at the install root or program dir).
        foreach (var sub in new[] { @"SOFTWARE\LibreOffice\UNO\InstallPath", @"SOFTWARE\The Document Foundation\LibreOffice" })
        {
            try
            {
                var v = RegistryHelper.GetValue(RegRoot.HKLM, sub, "")?.ToString()
                        ?? RegistryHelper.GetValue(RegRoot.HKLM, sub, "Path")?.ToString();
                if (!string.IsNullOrEmpty(v))
                {
                    if (File.Exists(Path.Combine(v, "soffice.com"))) return v;
                    var prog = Path.Combine(v, "program");
                    if (File.Exists(Path.Combine(prog, "soffice.com"))) return prog;
                }
            }
            catch { }
        }
        return null;
    }

    private static string? Resolve(string fileName)
    {
        var fromReg = InstallDirFromRegistry();
        var candidates = new List<string?>();
        if (fromReg is not null) candidates.Add(Path.Combine(fromReg, fileName));
        foreach (var d in ProgramDirs) candidates.Add(Path.Combine(d, fileName));
        foreach (var c in candidates)
            if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
        return null;
    }

    /// <summary>soffice.com 嘅完整路徑（轉檔用，stdout 可靠）· Full path to soffice.com (for conversion).</summary>
    public static string? SofficeCom => _com ??= Resolve("soffice.com");

    /// <summary>soffice.exe 嘅完整路徑（開檔用）· Full path to soffice.exe (for opening files).</summary>
    public static string? SofficeExe => _exe ??= Resolve("soffice.exe");

    public static bool IsInstalled => SofficeCom is not null || SofficeExe is not null;

    /// <summary>清快取，等啱啱裝完即刻搵到 · Clear cached paths so a just-installed LibreOffice is re-resolved.</summary>
    public static void Rescan() { _com = null; _exe = null; }

    /// <summary>每次轉檔用一個獨立的暫時 profile，等桌面 app 開住都轉到 · A unique throwaway user profile.</summary>
    private static string UserInstallationArg()
    {
        var dir = Path.Combine(Path.GetTempPath(), "winforge_lo_" + Guid.NewGuid().ToString("N"));
        var uri = new Uri(dir).AbsoluteUri; // file:///C:/...
        return $"-env:UserInstallation={uri}";
    }

    /// <summary>查 LibreOffice 版本 · Probe the installed LibreOffice version string.</summary>
    public static async Task<string> Version(CancellationToken ct = default)
    {
        var exe = SofficeCom ?? SofficeExe;
        if (exe is null) return "";
        var outp = await ShellRunner.Capture(exe, $"--version {UserInstallationArg()} --norestore --nolockcheck", ct);
        return outp.Trim();
    }

    // ===== conversion =====

    /// <summary>每個轉檔項目嘅狀態 · Status of one file in the batch.</summary>
    public enum ConvertState { Queued, Converting, Done, Failed }

    /// <summary>一個轉檔項目 · One convertible file with live status.</summary>
    public sealed class ConvertItem
    {
        public string SourcePath { get; init; } = "";
        public string FileName => Path.GetFileName(SourcePath);
        public long SizeBytes { get; init; }
        public ConvertState State { get; set; } = ConvertState.Queued;
        public string? OutputPath { get; set; }
        public string? Detail { get; set; }
    }

    /// <summary>常見目標格式（顯示名 → 篩選器）· Target formats with their preferred filter overrides.</summary>
    public sealed record TargetFormat(string Ext, string En, string Zh, string? Filter = null)
    {
        /// <summary>--convert-to 嘅參數（ext[:filter]）· The --convert-to value (ext or ext:filter).</summary>
        public string ConvertArg => Filter is null ? Ext : $"{Ext}:{Filter}";
    }

    /// <summary>內建目標格式清單 · The built-in list of target formats and default filters.</summary>
    public static IReadOnlyList<TargetFormat> Formats { get; } = new List<TargetFormat>
    {
        new("pdf",  "PDF document",       "PDF 文件",        "writer_pdf_Export"),
        new("docx", "Word (.docx)",       "Word（.docx）",   "MS Word 2007 XML"),
        new("odt",  "OpenDocument Text",  "OpenDocument 文字"),
        new("xlsx", "Excel (.xlsx)",      "Excel（.xlsx）",  "Calc MS Excel 2007 XML"),
        new("ods",  "OpenDocument Sheet", "OpenDocument 試算表"),
        new("pptx", "PowerPoint (.pptx)", "PowerPoint（.pptx）", "Impress MS PowerPoint 2007 XML"),
        new("odp",  "OpenDocument Pres.", "OpenDocument 簡報"),
        new("csv",  "CSV (text)",         "CSV（文字）",     "Text - txt - csv (StarCalc)"),
        new("txt",  "Plain text",         "純文字",          "Text"),
        new("html", "HTML",               "HTML"),
        new("rtf",  "Rich Text (.rtf)",   "RTF 格式"),
        new("png",  "PNG image",          "PNG 圖片"),
        new("jpg",  "JPEG image",         "JPEG 圖片"),
    };

    /// <summary>常見可轉檔副檔名（畀檔案揀選用）· Extensions accepted into the convertible list.</summary>
    public static string[] SourceExtensions { get; } =
    {
        ".doc", ".docx", ".odt", ".rtf", ".txt", ".html", ".htm",
        ".xls", ".xlsx", ".ods", ".csv",
        ".ppt", ".pptx", ".odp",
        ".pdf", ".fodt", ".fods", ".fodp", ".wps", ".pub",
    };

    /// <summary>
    /// 轉一個檔案 · Convert one file to the given format. Verifies the output exists (soffice
    /// can exit 0 even on failure), so success means the file is really on disk.
    /// </summary>
    public static async Task<(bool ok, string? outPath, string log)> ConvertOne(
        string source, TargetFormat fmt, string outDir, string? filterOverride = null, CancellationToken ct = default)
    {
        var exe = SofficeCom ?? SofficeExe;
        if (exe is null) return (false, null, "soffice not found");

        Directory.CreateDirectory(outDir);
        var convertArg = string.IsNullOrWhiteSpace(filterOverride)
            ? fmt.ConvertArg
            : $"{fmt.Ext}:{filterOverride}";

        var args = new StringBuilder();
        args.Append("--headless --norestore --nolockcheck --nodefault --nologo --nofirststartwizard ");
        args.Append(UserInstallationArg()).Append(' ');
        args.Append("--convert-to ").Append('"').Append(convertArg).Append('"').Append(' ');
        args.Append("--outdir ").Append('"').Append(outDir.TrimEnd('\\')).Append('"').Append(' ');
        args.Append('"').Append(source).Append('"');

        var expected = Path.Combine(outDir, Path.GetFileNameWithoutExtension(source) + "." + fmt.Ext);
        var log = new StringBuilder();
        log.AppendLine($"> {Path.GetFileName(exe)} {args}");

        var r = await ShellRunner.RunIn(Path.GetDirectoryName(source), exe, args.ToString(), elevated: false, ct);
        if (!string.IsNullOrWhiteSpace(r.Output)) log.AppendLine(r.Output.Trim());

        // Verify the output really exists — soffice's exit code alone is not trustworthy.
        if (File.Exists(expected))
            return (true, expected, log.ToString());

        // Some filters produce a slightly different name; pick the newest matching-extension file.
        try
        {
            var alt = Directory.EnumerateFiles(outDir, "*." + fmt.Ext)
                .Where(p => Path.GetFileNameWithoutExtension(p)
                    .StartsWith(Path.GetFileNameWithoutExtension(source), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (alt is not null) return (true, alt, log.ToString());
        }
        catch { }

        log.AppendLine("(output file was not produced)");
        return (false, null, log.ToString());
    }

    /// <summary>
    /// 順序轉一批檔案 · Convert a batch sequentially (LibreOffice will not run two conversions
    /// against one profile in parallel reliably). Reports progress per item via callbacks.
    /// </summary>
    public static async Task ConvertBatch(
        IReadOnlyList<ConvertItem> items, TargetFormat fmt, Func<ConvertItem, string> outDirFor,
        string? filterOverride, Action<ConvertItem> onItemChanged, Action<string> onLog, CancellationToken ct)
    {
        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) { item.State = ConvertState.Failed; item.Detail = "Cancelled · 已取消"; onItemChanged(item); continue; }
            if (item.State == ConvertState.Done) continue; // skip already-converted on re-run

            item.State = ConvertState.Converting; item.Detail = null; onItemChanged(item);
            try
            {
                var (ok, outPath, log) = await ConvertOne(item.SourcePath, fmt, outDirFor(item), filterOverride, ct);
                onLog(log);
                item.OutputPath = outPath;
                item.State = ok ? ConvertState.Done : ConvertState.Failed;
                item.Detail = ok ? outPath : "Conversion failed · 轉檔失敗";
            }
            catch (OperationCanceledException) { item.State = ConvertState.Failed; item.Detail = "Cancelled · 已取消"; }
            catch (Exception ex) { item.State = ConvertState.Failed; item.Detail = ex.Message; onLog(ex.Message); }
            onItemChanged(item);
        }
    }

    // ===== open / launch =====

    /// <summary>用 LibreOffice 開檔編輯 · Launch LibreOffice to edit the file (UI app).</summary>
    public static TweakResult OpenForEditing(string file)
    {
        var exe = SofficeExe ?? SofficeCom;
        if (exe is null) return TweakResult.Fail("LibreOffice not found.", "搵唔到 LibreOffice。");
        if (!File.Exists(file)) return TweakResult.Fail("File not found.", "搵唔到檔案。");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--norestore \"{file}\"",
                UseShellExecute = false,
            });
            return TweakResult.Ok("Opened in LibreOffice.", "已喺 LibreOffice 開啟。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>開一個冇檔案嘅 LibreOffice 模組 · Launch a blank LibreOffice app (writer/calc/impress…).</summary>
    public static TweakResult LaunchApp(string switchArg)
    {
        var exe = SofficeExe ?? SofficeCom;
        if (exe is null) return TweakResult.Fail("LibreOffice not found.", "搵唔到 LibreOffice。");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--norestore {switchArg}",
                UseShellExecute = false,
            });
            return TweakResult.Ok("Launched LibreOffice.", "已啟動 LibreOffice。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>殺死殘留嘅 soffice 程序（轉檔卡住時用）· Kill stray soffice processes.</summary>
    public static TweakResult KillStray()
    {
        int killed = 0;
        foreach (var name in new[] { "soffice", "soffice.bin" })
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try { p.Kill(true); killed++; } catch { }
                finally { p.Dispose(); }
            }
        }
        return TweakResult.Ok($"Killed {killed} soffice process(es).", $"已結束 {killed} 個 soffice 程序。");
    }
}
