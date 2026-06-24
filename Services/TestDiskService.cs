using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個救援目標（實體碟或映像檔）· One recovery source: a physical disk or an image file.</summary>
public sealed class RecoverySource
{
    /// <summary>true = 實體碟 · physical disk; false = 映像檔 · image file.</summary>
    public bool IsDisk { get; init; }

    /// <summary>實體碟編號（IsDisk 時有效）· Physical-disk number (valid when IsDisk).</summary>
    public int DiskNumber { get; init; }

    /// <summary>傳俾 photorec/testdisk 嘅裝置路徑或檔案路徑 · Device path or file path passed to the CLI.</summary>
    public string DevicePath { get; init; } = "";

    public string Model { get; init; } = "";
    public long Size { get; init; }
    public string BusType { get; init; } = "";
    public bool IsRemovable { get; init; }
    public bool IsSystem { get; init; }
    public bool IsBoot { get; init; }

    /// <summary>呢個碟佔住嘅磁碟機代號（例如 C: D:）· Drive letters carved out of this disk.</summary>
    public List<string> Letters { get; init; } = new();

    public string HumanSize => TestDiskService.HumanSize(Size);

    public string Display
    {
        get
        {
            if (!IsDisk)
                return $"📄 {Path.GetFileName(DevicePath)}  ·  {HumanSize}";
            var letters = Letters.Count > 0 ? $" [{string.Join(" ", Letters)}]" : "";
            var flags = IsSystem || IsBoot ? "  ⚠ SYSTEM" : (IsRemovable ? "" : "  (fixed)");
            return $"💽 Disk {DiskNumber} · {Model} · {HumanSize} · {BusType}{letters}{flags}";
        }
    }
}

/// <summary>一個可救援嘅檔案類型（由 photorec fileopt 解析）· One recoverable file family from photorec fileopt.</summary>
public sealed class RecoveryFileType
{
    /// <summary>photorec 內部副檔名 token（例如 jpg、mov、pdf）· photorec extension token.</summary>
    public string Token { get; init; } = "";
    /// <summary>簡短描述 · short description from the listing.</summary>
    public string Description { get; init; } = "";
    /// <summary>預設係咪啟用 · whether photorec enables it by default.</summary>
    public bool DefaultEnabled { get; init; }
    /// <summary>使用者選擇 · the user's current selection in the UI.</summary>
    public bool Selected { get; set; }
}

/// <summary>掃描進度回報 · Live progress callback payload.</summary>
public sealed record RecoveryProgress(string Line, int RecoveredCount);

/// <summary>
/// TestDisk / PhotoRec 資料救援引擎 · TestDisk / PhotoRec data-recovery engine.
/// 定位／下載／解壓 cgsecurity 嘅 photorec_win.exe / testdisk_win.exe（執行時下載，唔入 repo），
/// 列舉實體碟（WMI Win32_DiskDrive / Get-Disk）同映像檔，由 <c>photorec /cmd … fileopt</c> 解析可救援檔案類型，
/// 用非互動 <c>/cmd</c> 腳本模式碳化（carve）檔案到指定資料夾，做 TestDisk 唯讀分割區掃描，
/// 即時 tail 記錄檔同數已救回嘅檔案。全程 in-app，永不啟動 ncurses TUI。
///
/// Locates / downloads / extracts cgsecurity's photorec_win.exe / testdisk_win.exe (fetched at runtime,
/// never committed); enumerates physical disks (WMI) plus an image-file picker; parses recoverable file
/// types from <c>photorec /cmd … fileopt</c>; carves files non-interactively via the <c>/cmd</c> script
/// grammar to a chosen folder; runs a TestDisk read-only partition scan; tails the log live and counts
/// recovered files. Everything in-app — the interactive TUI is never launched.
/// </summary>
public static class TestDiskService
{
    /// <summary>釘住嘅已知良好版本 · Pinned known-good cgsecurity release.</summary>
    public const string Version = "7.2";
    private const string DownloadUrl = "https://www.cgsecurity.org/Download_and_donate.php/testdisk-7.2.win64.zip";

    private static string ToolsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "testdisk");

    private static string? _photorec;
    private static string? _testdisk;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };

    // ───────────────────────── binary location / download ─────────────────────────

    /// <summary>搵 photorec_win.exe · Locate photorec_win.exe (cached). Returns null if missing.</summary>
    public static string? PhotoRecPath()
    {
        if (_photorec is not null && File.Exists(_photorec)) return _photorec;
        _photorec = FindExe("photorec_win.exe");
        return _photorec;
    }

    /// <summary>搵 testdisk_win.exe · Locate testdisk_win.exe (cached). Returns null if missing.</summary>
    public static string? TestDiskPath()
    {
        if (_testdisk is not null && File.Exists(_testdisk)) return _testdisk;
        _testdisk = FindExe("testdisk_win.exe");
        return _testdisk;
    }

    /// <summary>兩個 exe 都搵到先當已安裝 · Both binaries present.</summary>
    public static bool IsAvailable() => PhotoRecPath() is not null && TestDiskPath() is not null;

    /// <summary>強制重新掃描快取 · Drop the cached paths so the next lookup re-scans disk.</summary>
    public static void Rescan() { _photorec = null; _testdisk = null; }

    private static string? FindExe(string name)
    {
        try
        {
            if (!Directory.Exists(ToolsDir)) return null;
            // The cgsecurity zip extracts to testdisk-7.2\ — search a couple of levels deep.
            foreach (var hit in SafeEnumerate(ToolsDir, name, 4))
                if (File.Exists(hit)) return hit;
        }
        catch { /* ignore */ }
        return null;
    }

    private static IEnumerable<string> SafeEnumerate(string root, string pattern, int maxDepth)
    {
        var stack = new Stack<(string dir, int depth)>();
        stack.Push((root, 0));
        while (stack.Count > 0)
        {
            var (dir, depth) = stack.Pop();
            string[] files = Array.Empty<string>();
            try { files = Directory.GetFiles(dir, pattern); } catch { }
            foreach (var f in files) yield return f;
            if (depth >= maxDepth) continue;
            string[] subs = Array.Empty<string>();
            try { subs = Directory.GetDirectories(dir); } catch { }
            foreach (var s in subs) stack.Push((s, depth + 1));
        }
    }

    /// <summary>
    /// 由 cgsecurity 下載並解壓 win64 zip 到 %LOCALAPPDATA%\WinForge\testdisk · Download + extract the
    /// pinned cgsecurity win64 zip. Reports coarse progress through <paramref name="onProgress"/>.
    /// </summary>
    public static async Task<TweakResult> DownloadBinaries(IProgress<string>? onProgress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(ToolsDir);
            var zipPath = Path.Combine(ToolsDir, $"testdisk-{Version}.win64.zip");

            onProgress?.Report(Loc.I.Pick($"Downloading TestDisk {Version} (win64)…", $"下載緊 TestDisk {Version}（win64）…"));
            using (var resp = await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = File.Create(zipPath);
                var buffer = new byte[81920];
                long total = resp.Content.Headers.ContentLength ?? -1;
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    read += n;
                    if (total > 0 && read % (1024 * 1024 * 4) < 81920)
                        onProgress?.Report(Loc.I.Pick(
                            $"Downloading… {read / 1048576} / {total / 1048576} MB",
                            $"下載緊… {read / 1048576} / {total / 1048576} MB"));
                }
            }

            // Integrity: a real cgsecurity zip is several MB. Reject obvious error pages.
            var info = new FileInfo(zipPath);
            if (!info.Exists || info.Length < 1024 * 1024)
            {
                try { File.Delete(zipPath); } catch { }
                return TweakResult.Fail(
                    "Downloaded file is too small — the download may have failed or the URL changed.",
                    "下載到嘅檔案太細 — 可能下載失敗或者網址有變。");
            }

            onProgress?.Report(Loc.I.Pick("Extracting…", "解壓緊…"));
            // Extract into a clean subfolder to avoid mixing versions.
            var extractDir = Path.Combine(ToolsDir, $"testdisk-{Version}");
            try { if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true); } catch { }
            ZipFile.ExtractToDirectory(zipPath, ToolsDir, overwriteFiles: true);
            try { File.Delete(zipPath); } catch { }

            Rescan();
            if (!IsAvailable())
                return TweakResult.Fail(
                    "Extracted, but photorec_win.exe / testdisk_win.exe were not found in the archive.",
                    "已解壓，但喺壓縮檔內搵唔到 photorec_win.exe / testdisk_win.exe。");

            return TweakResult.Ok(
                $"Recovery tools v{Version} are ready.", $"救援工具 v{Version} 已就緒。");
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Download cancelled.", "已取消下載。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Download failed: {ex.Message}", $"下載失敗：{ex.Message}");
        }
    }

    // ───────────────────────── disk / image enumeration ─────────────────────────

    /// <summary>
    /// 列舉實體碟 · Enumerate physical disks (read-only) with size / bus / removable / system flags and the
    /// drive letters they host, so we can warn and block same-disk recovery.
    /// </summary>
    public static async Task<List<RecoverySource>> ListDisks(CancellationToken ct = default)
    {
        const string script = @"
$sys = (Get-CimInstance Win32_OperatingSystem).SystemDrive
Get-Disk | ForEach-Object {
  $d = $_
  $letters = @()
  try { $letters = Get-Partition -DiskNumber $d.Number -ErrorAction SilentlyContinue |
        Where-Object { $_.DriveLetter } | ForEach-Object { ""$($_.DriveLetter):"" } } catch {}
  [pscustomobject]@{
    Number     = $d.Number
    Model      = ($d.FriendlyName -as [string])
    Size       = [int64]$d.Size
    BusType    = ($d.BusType -as [string])
    Removable  = [bool]($d.BusType -eq 'USB' -or $d.BusType -eq 'SD' -or $d.BusType -eq 'MMC')
    IsBoot     = [bool]$d.IsBoot
    IsSystem   = [bool]$d.IsSystem
    Letters    = $letters
    HasSysDrive= [bool]($letters -contains $sys)
  }
} | ConvertTo-Json -Depth 4";
        var json = await ShellRunner.CapturePowershellJson(script, ct);
        var list = new List<RecoverySource>();
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var items = root.ValueKind == System.Text.Json.JsonValueKind.Array
                ? root.EnumerateArray().ToList()
                : new List<System.Text.Json.JsonElement> { root };
            foreach (var el in items)
            {
                var letters = new List<string>();
                if (el.TryGetProperty("Letters", out var le))
                {
                    if (le.ValueKind == System.Text.Json.JsonValueKind.Array)
                        foreach (var l in le.EnumerateArray()) { var s = l.GetString(); if (!string.IsNullOrEmpty(s)) letters.Add(s); }
                    else if (le.ValueKind == System.Text.Json.JsonValueKind.String)
                    { var s = le.GetString(); if (!string.IsNullOrEmpty(s)) letters.Add(s); }
                }
                int num = GetInt(el, "Number");
                bool isSystem = GetBool(el, "IsSystem") || GetBool(el, "HasSysDrive");
                list.Add(new RecoverySource
                {
                    IsDisk = true,
                    DiskNumber = num,
                    DevicePath = $@"\\.\PhysicalDrive{num}",
                    Model = GetStr(el, "Model"),
                    Size = GetLong(el, "Size"),
                    BusType = GetStr(el, "BusType"),
                    IsRemovable = GetBool(el, "Removable"),
                    IsBoot = GetBool(el, "IsBoot"),
                    IsSystem = isSystem,
                    Letters = letters,
                });
            }
        }
        catch { /* return what we have */ }
        return list.OrderBy(d => d.DiskNumber).ToList();
    }

    /// <summary>由映像檔路徑建一個救援來源 · Wrap an image file path as a recovery source.</summary>
    public static RecoverySource ImageSource(string path)
    {
        long size = 0;
        try { size = new FileInfo(path).Length; } catch { }
        return new RecoverySource { IsDisk = false, DevicePath = path, Size = size };
    }

    /// <summary>
    /// 檢查救援輸出資料夾係咪同來源同一個實體碟（高危）· Returns true when the output folder lives on the
    /// SAME physical disk we are recovering from — PhotoRec must write to a DIFFERENT disk.
    /// </summary>
    public static async Task<bool> IsSameDisk(RecoverySource source, string outputFolder, CancellationToken ct = default)
    {
        if (!source.IsDisk) return false; // recovering from an image file — any local folder is fine.
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(outputFolder));
            if (string.IsNullOrEmpty(root)) return false;
            var letter = root.TrimEnd('\\', '/');                 // e.g. "C:"
            if (string.IsNullOrEmpty(letter)) return false;
            var driveLetter = letter.TrimEnd(':');                 // "C"
            if (driveLetter.Length != 1) return false;

            var script = $@"
$p = Get-Partition -DriveLetter '{driveLetter}' -ErrorAction SilentlyContinue
if ($p) {{ $p.DiskNumber }} else {{ -1 }}";
            var outNum = (await ShellRunner.CapturePowershell(script, ct)).Trim();
            if (int.TryParse(outNum, out var diskNum))
                return diskNum == source.DiskNumber;
        }
        catch { /* fail open is unsafe — fail closed (treat as same disk) */ return true; }
        return false;
    }

    // ───────────────────────── file-type listing (photorec fileopt) ─────────────────────────

    /// <summary>
    /// 由 photorec 取得可救援檔案類型清單 · Ask photorec to print its supported file families and parse
    /// them. We start it in <c>/cmd … fileopt</c> mode (no device action) and read the listing it writes
    /// to the log; if that yields nothing we fall back to a bundled common list so the UI is never empty.
    /// </summary>
    public static async Task<List<RecoveryFileType>> ListFileTypes(CancellationToken ct = default)
    {
        var exe = PhotoRecPath();
        if (exe is not null)
        {
            // photorec writes the file-format table to photorec.log when fileopt is invoked.
            // Run in a scratch dir so we can read a fresh log, then bail out (",quit") without searching.
            var scratch = Path.Combine(Path.GetTempPath(), "winforge_phopt_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(scratch);
                var logPath = Path.Combine(scratch, "photorec.log");
                // No real device: list types via a dummy file. We create a tiny image so photorec opens it.
                var dummy = Path.Combine(scratch, "dummy.dd");
                try { await File.WriteAllBytesAsync(dummy, new byte[512], ct); } catch { }
                // "fileopt" then "quit"-equivalent: we cannot easily 'quit', so just fileopt with no search.
                var args = $"/log /d \"{Path.Combine(scratch, "recup")}\" /cmd \"{dummy}\" fileopt";
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(20));
                try { await ShellRunner.RunIn(scratch, exe, args, elevated: false, cts.Token); } catch { }

                if (File.Exists(logPath))
                {
                    var parsed = ParseFileOptLog(await SafeReadAllText(logPath));
                    if (parsed.Count > 0) return parsed;
                }
            }
            catch { /* fall through to bundled list */ }
            finally { try { Directory.Delete(scratch, recursive: true); } catch { } }
        }
        return DefaultFileTypes();
    }

    /// <summary>解析 fileopt 記錄行（[X] ext description）· Parse fileopt listing lines.</summary>
    public static List<RecoveryFileType> ParseFileOptLog(string log)
    {
        var list = new List<RecoveryFileType>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(log)) return list;
        foreach (var raw in log.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            // Format: "[X] jpg JPG picture" or "[ ] mov mov/mp4/3gp/3g2/jp2 video".
            if (line.Length < 5 || line[0] != '[') continue;
            int close = line.IndexOf(']');
            if (close < 1 || close + 1 >= line.Length) continue;
            bool enabled = line.Substring(1, close - 1).Trim().Equals("X", StringComparison.OrdinalIgnoreCase);
            var rest = line.Substring(close + 1).Trim();
            if (rest.Length == 0) continue;
            int sp = rest.IndexOf(' ');
            var token = sp < 0 ? rest : rest.Substring(0, sp);
            var desc = sp < 0 ? "" : rest.Substring(sp + 1).Trim();
            // token must look like an extension (letters/digits), reject prose lines.
            if (token.Length == 0 || token.Length > 12 || !token.All(c => char.IsLetterOrDigit(c) || c == '_')) continue;
            if (!seen.Add(token)) continue;
            list.Add(new RecoveryFileType { Token = token, Description = desc, DefaultEnabled = enabled, Selected = enabled });
        }
        return list;
    }

    /// <summary>後備常用類型 · Bundled fallback list of common PhotoRec families.</summary>
    public static List<RecoveryFileType> DefaultFileTypes()
    {
        (string t, string d, bool on)[] common =
        {
            ("jpg", "JPG picture", true), ("png", "PNG picture", true), ("gif", "GIF image", true),
            ("tiff", "TIFF image", true), ("bmp", "BMP bitmap", true), ("heic", "HEIC/HEIF photo", true),
            ("raw", "Camera RAW (CR2/NEF/ARW)", true), ("pdf", "PDF document", true),
            ("doc", "MS Office (legacy doc/xls/ppt)", true), ("docx", "Office Open XML / ZIP", true),
            ("txt", "Plain text", false), ("rtf", "Rich Text", true), ("zip", "ZIP archive", true),
            ("rar", "RAR archive", true), ("7z", "7-Zip archive", true), ("gz", "gzip archive", true),
            ("tar", "TAR archive", true), ("mp4", "MP4 / MOV video", true), ("mov", "QuickTime video", true),
            ("avi", "AVI video", true), ("mkv", "Matroska video", true), ("mp3", "MP3 audio", true),
            ("wav", "WAV audio", true), ("flac", "FLAC audio", true), ("ogg", "OGG audio", true),
            ("sqlite", "SQLite database", false), ("html", "HTML page", false), ("exe", "Windows executable", false),
        };
        return common.Select(c => new RecoveryFileType
        { Token = c.t, Description = c.d, DefaultEnabled = c.on, Selected = c.on }).ToList();
    }

    // ───────────────────────── PhotoRec carve ─────────────────────────

    /// <summary>
    /// 組裝 PhotoRec 嘅 /cmd 字串 · Build the PhotoRec /cmd command string.
    /// Grammar: <c>partition_none, [freespace|wholespace], fileopt,everything,disable,&lt;type&gt;,enable,…, search</c>.
    /// When <paramref name="selected"/> is null/empty we let photorec keep its defaults (no fileopt clause).
    /// </summary>
    public static string BuildPhotoRecCmd(IEnumerable<RecoveryFileType>? selected, bool freeSpaceOnly)
    {
        var sb = new StringBuilder();
        sb.Append("partition_none,");
        sb.Append(freeSpaceOnly ? "freespace," : "wholespace,");
        var picks = selected?.Where(t => t.Selected).ToList();
        if (picks is { Count: > 0 })
        {
            sb.Append("fileopt,everything,disable");
            foreach (var t in picks)
                sb.Append(',').Append(t.Token).Append(",enable");
            sb.Append(',');
        }
        sb.Append("search");
        return sb.ToString();
    }

    /// <summary>
    /// 跑 PhotoRec 碳化 · Run a PhotoRec carve. Streams each stdout/log line through
    /// <paramref name="progress"/> with a running recovered-file count, and returns a bilingual result.
    /// </summary>
    public static async Task<TweakResult> RunPhotoRec(
        RecoverySource source, string outputFolder, IEnumerable<RecoveryFileType>? selected,
        bool freeSpaceOnly, IProgress<RecoveryProgress>? progress, CancellationToken ct = default)
    {
        var exe = PhotoRecPath();
        if (exe is null) return TweakResult.Fail("photorec_win.exe not found. Download the tools first.", "搵唔到 photorec_win.exe，請先下載工具。");
        if (string.IsNullOrWhiteSpace(outputFolder)) return TweakResult.Fail("Pick an output folder first.", "請先揀輸出資料夾。");

        try { Directory.CreateDirectory(outputFolder); }
        catch (Exception ex) { return TweakResult.Fail($"Cannot create output folder: {ex.Message}", $"無法建立輸出資料夾：{ex.Message}"); }

        var cmd = BuildPhotoRecCmd(selected, freeSpaceOnly);
        var recupDir = Path.Combine(outputFolder, "recup_dir");
        var args = $"/log /d \"{recupDir}\" /cmd \"{source.DevicePath}\" {cmd}";

        var result = await RunWithLiveLog(
            exe, outputFolder, args,
            logFileName: "photorec.log",
            countDir: outputFolder,
            progress, ct);

        int recovered = CountRecovered(outputFolder);
        if (result.ok)
            return TweakResult.Ok(
                $"PhotoRec finished — {recovered} file(s) recovered to {outputFolder}.",
                $"PhotoRec 完成 — 已救回 {recovered} 個檔案到 {outputFolder}。",
                result.tail);
        return TweakResult.Fail(
            $"PhotoRec exited with code {result.exit}. {recovered} file(s) recovered so far.",
            $"PhotoRec 以代碼 {result.exit} 結束。暫時救回 {recovered} 個檔案。",
            result.tail);
    }

    /// <summary>數已救回嘅檔案（recup_dir.* 內，排除 report.xml）· Count carved files under recup_dir.*.</summary>
    public static int CountRecovered(string outputFolder)
    {
        try
        {
            if (!Directory.Exists(outputFolder)) return 0;
            int count = 0;
            foreach (var dir in Directory.GetDirectories(outputFolder, "recup_dir*", SearchOption.AllDirectories))
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var name = Path.GetFileName(f);
                    if (name.Equals("report.xml", StringComparison.OrdinalIgnoreCase)) continue;
                    count++;
                }
            return count;
        }
        catch { return 0; }
    }

    // ───────────────────────── TestDisk read-only scan ─────────────────────────

    /// <summary>
    /// TestDisk 唯讀全機分割區清單 · TestDisk read-only listing of every disk + partition (<c>/list</c>).
    /// Never writes; pure enumeration of the partition tables found.
    /// </summary>
    public static async Task<TweakResult> RunTestDiskList(CancellationToken ct = default)
    {
        var exe = TestDiskPath();
        if (exe is null) return TweakResult.Fail("testdisk_win.exe not found. Download the tools first.", "搵唔到 testdisk_win.exe，請先下載工具。");

        var work = Path.Combine(ToolsDir, "scan");
        try { Directory.CreateDirectory(work); } catch { }
        var r = await ShellRunner.RunIn(work, exe, "/log /list", elevated: false, ct);
        var output = r.Output ?? "";
        if (string.IsNullOrWhiteSpace(output))
        {
            var log = Path.Combine(work, "testdisk.log");
            if (File.Exists(log)) output = await SafeReadAllText(log);
        }
        if (string.IsNullOrWhiteSpace(output))
            return TweakResult.Fail("TestDisk produced no output (admin rights may be required to read raw disks).",
                "TestDisk 冇任何輸出（讀取原始磁碟可能需要管理員權限）。", output);
        return TweakResult.Ok("TestDisk listed all disks and partitions.", "TestDisk 已列出所有磁碟同分割區。", output);
    }

    /// <summary>
    /// TestDisk 唯讀單碟分割區/檔案結構 · TestDisk read-only structure view of one source (<c>/cmd dev "list"</c>).
    /// This lists the partition's directory contents (read-only) — no analyze/search/write tokens are sent,
    /// so the partition table is never rewritten.
    /// </summary>
    public static async Task<TweakResult> RunTestDiskScan(RecoverySource source, CancellationToken ct = default)
    {
        var exe = TestDiskPath();
        if (exe is null) return TweakResult.Fail("testdisk_win.exe not found. Download the tools first.", "搵唔到 testdisk_win.exe，請先下載工具。");

        var work = Path.Combine(ToolsDir, "scan");
        try { Directory.CreateDirectory(work); } catch { }
        // Read-only: "list" prints the partition/dir structure for the device, then TestDisk exits.
        var args = $"/log /cmd \"{source.DevicePath}\" list";
        var r = await ShellRunner.RunIn(work, exe, args, elevated: false, ct);
        var output = r.Output ?? "";
        var log = Path.Combine(work, "testdisk.log");
        if (File.Exists(log))
        {
            var logText = await SafeReadAllText(log);
            if (!string.IsNullOrWhiteSpace(logText))
                output = string.IsNullOrWhiteSpace(output) ? logText : output + "\n" + logText;
        }
        if (string.IsNullOrWhiteSpace(output))
            return TweakResult.Fail("TestDisk produced no output (admin rights may be required).",
                "TestDisk 冇任何輸出（可能需要管理員權限）。", output);
        return TweakResult.Ok("TestDisk read-only scan complete.", "TestDisk 唯讀掃描完成。", output);
    }

    // ───────────────────────── shared process + live-log runner ─────────────────────────

    private static async Task<(bool ok, int exit, string tail)> RunWithLiveLog(
        string exe, string workDir, string args, string logFileName, string countDir,
        IProgress<RecoveryProgress>? progress, CancellationToken ct)
    {
        try { Directory.CreateDirectory(workDir); } catch { }

        var info = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        var tail = new StringBuilder();
        void Emit(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (tail)
            {
                tail.AppendLine(line);
                if (tail.Length > 64 * 1024) tail.Remove(0, tail.Length - 48 * 1024);
            }
            progress?.Report(new RecoveryProgress(line, CountRecovered(countDir)));
        }

        try
        {
            using var p = new Process { StartInfo = info, EnableRaisingEvents = true };
            p.OutputDataReceived += (_, e) => { if (e.Data is not null) Emit(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (e.Data is not null) Emit(e.Data); };
            if (!p.Start())
                return (false, -1, Loc.I.Pick("Failed to start process.", "無法啟動程序。"));
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // Tail the log file in parallel for the lines photorec writes only to disk.
            var logPath = Path.Combine(workDir, logFileName);
            using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var tailTask = TailLogAsync(logPath, Emit, tailCts.Token);

            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                tailCts.Cancel();
                try { await tailTask; } catch { }
                return (false, -1, tail.ToString());
            }

            tailCts.Cancel();
            try { await tailTask; } catch { }
            return (p.ExitCode == 0, p.ExitCode, tail.ToString());
        }
        catch (Exception ex)
        {
            Emit(ex.Message);
            return (false, -1, tail.ToString());
        }
    }

    /// <summary>跟住記錄檔尾部讀新行 · Follow a log file and emit appended lines until cancelled.</summary>
    private static async Task TailLogAsync(string path, Action<string> emit, CancellationToken ct)
    {
        long pos = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (fs.Length < pos) pos = 0; // log rotated/truncated
                        fs.Seek(pos, SeekOrigin.Begin);
                        using var sr = new StreamReader(fs, Encoding.UTF8);
                        string? line;
                        while ((line = await sr.ReadLineAsync(ct)) is not null)
                            emit(line);
                        pos = fs.Position;
                    }
                    catch { /* transient lock — retry next tick */ }
                }
                await Task.Delay(400, ct);
            }
        }
        catch (OperationCanceledException) { /* normal */ }
    }

    private static async Task<string> SafeReadAllText(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            return await sr.ReadToEndAsync();
        }
        catch { return ""; }
    }

    /// <summary>喺檔案總管開啟資料夾 · Open a folder in Explorer.</summary>
    public static void OpenFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    // ───────────────────────── small helpers ─────────────────────────

    public static string HumanSize(long bytes)
    {
        if (bytes <= 0) return "—";
        string[] u = { "B", "KB", "MB", "GB", "TB", "PB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }

    private static string GetStr(System.Text.Json.JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? (v.GetString() ?? "") : "";

    private static int GetInt(System.Text.Json.JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : 0;

    private static long GetLong(System.Text.Json.JsonElement el, string name)
        => el.TryGetProperty(name, out var v) && v.TryGetInt64(out var n) ? n : 0;

    private static bool GetBool(System.Text.Json.JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return false;
        return v.ValueKind switch
        {
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Number => v.TryGetInt32(out var n) && n != 0,
            System.Text.Json.JsonValueKind.String => bool.TryParse(v.GetString(), out var b) && b,
            _ => false,
        };
    }
}
