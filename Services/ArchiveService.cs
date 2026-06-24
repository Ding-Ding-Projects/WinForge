using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 包住 7-Zip (7z.exe) · A thin wrapper over the 7-Zip command line, scoped to the
/// archive/source the user selected in the Archives module.
/// </summary>
public static class ArchiveService
{
    private static string? _exe;

    /// <summary>7z.exe 嘅完整路徑 · Resolved full path to 7z.exe (or "7z.exe" on PATH).</summary>
    public static string SevenZip
    {
        get
        {
            if (_exe is not null) return _exe;
            var fromReg = RegistryHelper.GetValue(RegRoot.HKLM, @"SOFTWARE\7-Zip", "Path")?.ToString();
            foreach (var c in new[]
                     {
                         fromReg is null ? null : Path.Combine(fromReg, "7z.exe"),
                         @"C:\Program Files\7-Zip\7z.exe",
                         @"C:\Program Files (x86)\7-Zip\7z.exe",
                     })
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) { _exe = c; return _exe; }
            }
            _exe = "7z.exe"; // fall back to PATH
            return _exe;
        }
    }

    public static bool IsInstalled =>
        File.Exists(@"C:\Program Files\7-Zip\7z.exe") ||
        File.Exists(@"C:\Program Files (x86)\7-Zip\7z.exe") ||
        RegistryHelper.KeyExists(RegRoot.HKLM, @"SOFTWARE\7-Zip");

    /// <summary>清快取，等啱啱裝完嘅 7-Zip 即刻搵到 · Clear the cached exe path so a just-installed 7-Zip is re-resolved.</summary>
    public static void Rescan() { _exe = null; }
    private static string? _unrar;

    /// <summary>
    /// unrar.exe 嘅完整路徑（7-Zip 修唔到 RAR，要用 RARLAB unrar）。
    /// Resolved full path to unrar.exe — needed because 7-Zip cannot repair RAR archives,
    /// only the RARLAB unrar / WinRAR CLI can. Falls back to "unrar.exe" on PATH.
    /// </summary>
    public static string UnRar
    {
        get
        {
            if (_unrar is not null) return _unrar;
            foreach (var c in new[]
                     {
                         Path.Combine(AppContext.BaseDirectory, "unrar.exe"),
                         Path.Combine(AppContext.BaseDirectory, "Tools", "unrar.exe"),
                         @"C:\Program Files\WinRAR\unrar.exe",
                         @"C:\Program Files\WinRAR\UnRAR.exe",
                         @"C:\Program Files (x86)\WinRAR\unrar.exe",
                         @"C:\Program Files (x86)\WinRAR\UnRAR.exe",
                     })
            {
                if (File.Exists(c)) { _unrar = c; return _unrar; }
            }
            _unrar = "unrar.exe"; // fall back to PATH
            return _unrar;
        }
    }

    public static bool HasUnRar =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "unrar.exe")) ||
        File.Exists(Path.Combine(AppContext.BaseDirectory, "Tools", "unrar.exe")) ||
        File.Exists(@"C:\Program Files\WinRAR\unrar.exe") ||
        File.Exists(@"C:\Program Files\WinRAR\UnRAR.exe") ||
        File.Exists(@"C:\Program Files (x86)\WinRAR\unrar.exe") ||
        File.Exists(@"C:\Program Files (x86)\WinRAR\UnRAR.exe");

    public static string Archive => AppState.CurrentArchivePath;
    public static string Source => AppState.CurrentSourcePath;
    public static bool HasArchive => !string.IsNullOrWhiteSpace(Archive);
    public static bool HasSource => !string.IsNullOrWhiteSpace(Source);

    private static string OutDir()
    {
        try
        {
            var dir = Path.GetDirectoryName(Archive) ?? Directory.GetCurrentDirectory();
            var name = Path.GetFileNameWithoutExtension(Archive);
            return Path.Combine(dir, name + "_extracted");
        }
        catch { return Path.Combine(Path.GetTempPath(), "winforge_extracted"); }
    }

    /// <summary>
    /// 執行 7z，將 {archive}/{src}/{outdir} 換成實際路徑。
    /// Run 7z with {archive}/{src}/{outdir} substituted (and quoted) at runtime.
    /// </summary>
    public static Task<TweakResult> RunArgs(string args, bool needsArchive = true, bool needsSource = false,
        CancellationToken ct = default)
    {
        if (needsArchive && !HasArchive)
            return Task.FromResult(TweakResult.Fail("No archive selected.", "未揀壓縮檔。"));
        if (needsSource && !HasSource)
            return Task.FromResult(TweakResult.Fail("No source file/folder selected.", "未揀來源檔案／資料夾。"));

        var resolved = args
            .Replace("{archive}", Quote(Archive))
            .Replace("{src}", Quote(Source))
            .Replace("{outdir}", Quote(OutDir()));

        var workDir = HasArchive
            ? Path.GetDirectoryName(Archive)
            : (HasSource ? Path.GetDirectoryName(Source) : null);

        return ShellRunner.RunIn(workDir, SevenZip, resolved, elevated: false, ct);
    }

    /// <summary>
    /// 用 unrar.exe 跑 RAR 專用指令（7-Zip 修唔到 RAR）。{archive}/{outdir} 會換成實際路徑。
    /// Run a RAR-only command via unrar.exe ({archive}/{outdir} substituted). 7-Zip cannot
    /// repair RAR, so RAR repair/keep-broken must go through the RARLAB unrar CLI.
    /// </summary>
    public static Task<TweakResult> RunRar(string args, bool needsArchive = true, CancellationToken ct = default)
    {
        if (needsArchive && !HasArchive)
            return Task.FromResult(TweakResult.Fail("No archive selected.", "未揀壓縮檔。"));
        if (!HasUnRar)
            return Task.FromResult(TweakResult.Fail(
                "unrar.exe not found. Install WinRAR or place unrar.exe next to WinForge (7-Zip cannot repair RAR).",
                "搵唔到 unrar.exe。請安裝 WinRAR 或者將 unrar.exe 放喺 WinForge 旁邊（7-Zip 修唔到 RAR）。"));

        var resolved = args
            .Replace("{archive}", Quote(Archive))
            .Replace("{outdir}", Quote(OutDir()));

        var workDir = HasArchive ? Path.GetDirectoryName(Archive) : null;
        return ShellRunner.RunIn(workDir, UnRar, resolved, elevated: false, ct);
    }

    private static string Quote(string p) => string.IsNullOrEmpty(p) ? "" : $"\"{p.TrimEnd('\\')}\"";

    // ---- convenience for the bespoke page ----
    public static Task<TweakResult> List(CancellationToken ct = default) => RunArgs("l {archive}", true, false, ct);

    public static Task<TweakResult> Test(CancellationToken ct = default) => RunArgs("t {archive}", true, false, ct);

    public static Task<TweakResult> Benchmark(CancellationToken ct = default) => RunArgs("b", false, false, ct);

    public static Task<TweakResult> ExtractHere(CancellationToken ct = default)
        => RunArgs("x {archive} -o{outdir} -y", true, false, ct);

    public static Task<TweakResult> Create(string format, int level, string? password, CancellationToken ct = default)
        => Create(format, level, password, encryptHeader: true, solid: false, multithread: false, sfx: false, volumeSize: null, ct);

    /// <summary>
    /// 建立壓縮檔，連進階選項 · Create an archive with the full set of real 7-Zip switches:
    /// header (file-name) encryption, solid blocks, multithreading, a self-extracting .exe, and split volumes.
    /// 7-Zip 只可以「建立」佢支援嘅寫入格式（7z/zip/tar/gzip/bzip2/xz/wim）— 整唔到 .rar。
    /// </summary>
    public static Task<TweakResult> Create(string format, int level, string? password,
        bool encryptHeader, bool solid, bool multithread, bool sfx, string? volumeSize, CancellationToken ct = default)
    {
        bool is7z = string.Equals(format, "7z", StringComparison.OrdinalIgnoreCase);
        var sb = new System.Text.StringBuilder($"a -t{format} -mx={level}");

        if (!string.IsNullOrEmpty(password))
        {
            sb.Append($" -p\"{password}\"");
            if (encryptHeader && is7z) sb.Append(" -mhe=on"); // hide file names (7z only)
        }
        if (solid && is7z) sb.Append(" -ms=on");
        if (multithread) sb.Append(" -mmt=on");
        if (sfx && is7z) sb.Append(" -sfx"); // self-extracting .exe (7z module)
        if (!string.IsNullOrWhiteSpace(volumeSize)) sb.Append($" -v{volumeSize.Trim()}");

        sb.Append(" {archive} {src}");
        return RunArgs(sb.ToString(), true, true, ct);
    }
}
