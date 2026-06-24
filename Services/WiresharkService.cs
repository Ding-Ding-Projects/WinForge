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

/// <summary>一個擷取介面 · One capture interface (NIC) reported by dumpcap/tshark -D.</summary>
public sealed class CaptureInterface
{
    /// <summary>tshark 介面號（-i 用）· The numeric/string id tshark uses with -i.</summary>
    public string Id { get; set; } = "";
    /// <summary>裝置名（\Device\NPF_…）· The raw device name.</summary>
    public string Device { get; set; } = "";
    /// <summary>友善名 · Friendly name shown to the user.</summary>
    public string FriendlyName { get; set; } = "";

    public string Display => string.IsNullOrEmpty(FriendlyName)
        ? $"{Id}. {Device}"
        : $"{Id}. {FriendlyName}";
}

/// <summary>一行封包摘要 · One packet summary row in the live/offline grid.</summary>
public sealed class PacketRow
{
    public string No { get; set; } = "";
    public string Time { get; set; } = "";
    public string Source { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string Length { get; set; } = "";
    public string Info { get; set; } = "";
}

/// <summary>一次擷取嘅選項 · Options for one capture session.</summary>
public sealed class CaptureOptions
{
    public string InterfaceId { get; set; } = "";
    public string OutputFile { get; set; } = "";
    public string CaptureFilter { get; set; } = "";   // BPF, dumpcap -f
    public string DisplayFilter { get; set; } = "";   // tshark -Y for the live summary
    public bool Promiscuous { get; set; } = true;
    public int RingFiles { get; set; }                // -b files: (ring buffer count); 0 = off
    public int RingFileSizeKb { get; set; }           // -b filesize: (KB)
    public int StopAfterSeconds { get; set; }         // -a duration: ; 0 = off
    public int StopAfterPackets { get; set; }         // -c ; 0 = off
}

/// <summary>
/// 應用程式內封包擷取（包 Wireshark 嘅 dumpcap / tshark）· In-app packet capture wrapping Wireshark's CLI
/// tools. dumpcap.exe does the actual capture to a .pcapng file (lighter, loss-free); tshark.exe reads /
/// filters / dissects. Locates the install via the registry or %ProgramFiles%\Wireshark, detects the
/// Npcap driver and elevation, lists interfaces, streams a live packet summary, reads saved files with a
/// display filter, shows packet detail, gathers protocol statistics, and opens the GUI. No redirect —
/// WinForge drives the real binaries. Bilingual. Install via winget WiresharkFoundation.Wireshark.
/// </summary>
public static class WiresharkService
{
    public const string WingetId = "WiresharkFoundation.Wireshark";

    private static string? _installDir;

    /// <summary>需要管理員先可以擷取 · Capture requires elevation (Npcap kernel driver access).</summary>
    public static bool IsElevated => AdminHelper.IsElevated;

    // ── install location ────────────────────────────────────────────────────────────

    /// <summary>搵 Wireshark 安裝資料夾 · Locate the Wireshark install directory (registry → ProgramFiles).</summary>
    public static string? InstallDir()
    {
        if (_installDir is not null && Directory.Exists(_installDir)) return _installDir;
        _installDir = null;

        // 1) Registry: HKLM\SOFTWARE\Wireshark "InstallDir" (also under WOW6432Node).
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var k = b.OpenSubKey(@"SOFTWARE\Wireshark");
                var dir = k?.GetValue("InstallDir") as string;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) { _installDir = dir; return dir; }
            }
            catch { /* ignore */ }
        }

        // 2) Uninstall key DisplayIcon / InstallLocation.
        foreach (var hive in new[] { RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var b = RegistryKey.OpenBaseKey(hive, view);
                using var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Wireshark");
                var loc = k?.GetValue("InstallLocation") as string;
                if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc)) { _installDir = loc; return loc; }
            }
            catch { /* ignore */ }
        }

        // 3) Default %ProgramFiles%\Wireshark.
        foreach (var pf in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 })
        {
            if (string.IsNullOrEmpty(pf)) continue;
            var cand = Path.Combine(pf, "Wireshark");
            if (Directory.Exists(cand) && File.Exists(Path.Combine(cand, "tshark.exe")))
            { _installDir = cand; return cand; }
        }
        return null;
    }

    /// <summary>清除已快取嘅安裝路徑（裝完之後 rescan 用）· Clear the cached install path after an install.</summary>
    public static void Rescan() => _installDir = null;

    public static string? TsharkPath() => Exe("tshark.exe");
    public static string? DumpcapPath() => Exe("dumpcap.exe");

    /// <summary>Wireshark GUI 可執行檔 · The Wireshark GUI executable (Wireshark.exe, falls back to wireshark.exe).</summary>
    public static string? GuiPath()
    {
        var dir = InstallDir();
        if (dir is null) return null;
        foreach (var name in new[] { "Wireshark.exe", "wireshark.exe" })
        {
            var p = Path.Combine(dir, name);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static string? Exe(string name)
    {
        var dir = InstallDir();
        if (dir is null) return null;
        var p = Path.Combine(dir, name);
        return File.Exists(p) ? p : null;
    }

    public static bool IsInstalled => TsharkPath() is not null && DumpcapPath() is not null;

    /// <summary>tshark 版本字串 · Return the tshark version banner, or empty if unavailable.</summary>
    public static async Task<string> Version(CancellationToken ct = default)
    {
        var ts = TsharkPath();
        if (ts is null) return "";
        try { return (await ShellRunner.Capture(ts, "-v", ct)).Trim(); }
        catch { return ""; }
    }

    // ── Npcap detection ─────────────────────────────────────────────────────────────

    /// <summary>Npcap 驅動／服務有冇裝住 · Whether the Npcap capture driver/service is present.</summary>
    public static bool IsNpcapInstalled()
    {
        // The Npcap service is registered as "npcap" (or legacy "npf"); the driver lives in System32\Npcap.
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npcap");
            if (k is not null) return true;
        }
        catch { }
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npf");
            if (k is not null) return true;
        }
        catch { }
        try
        {
            var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (Directory.Exists(Path.Combine(sys, "Npcap"))) return true;
            if (File.Exists(Path.Combine(sys, "Npcap", "wpcap.dll"))) return true;
        }
        catch { }
        return false;
    }

    // ── interfaces ──────────────────────────────────────────────────────────────────

    /// <summary>列出擷取介面 · List capture interfaces via `dumpcap -D` (falls back to `tshark -D`).</summary>
    public static async Task<List<CaptureInterface>> Interfaces(CancellationToken ct = default)
    {
        var list = new List<CaptureInterface>();
        var dump = DumpcapPath();
        var ts = TsharkPath();
        string outp = "";
        try
        {
            if (dump is not null) outp = await ShellRunner.Capture(dump, "-D", ct);
            if (string.IsNullOrWhiteSpace(outp) && ts is not null) outp = await ShellRunner.Capture(ts, "-D", ct);
        }
        catch { return list; }

        // Lines look like:  "1. \Device\NPF_{GUID} (Ethernet)"  — number, device, optional (friendly).
        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var dot = line.IndexOf('.');
            if (dot <= 0) continue;
            var id = line.Substring(0, dot).Trim();
            if (!int.TryParse(id, out _)) continue;
            var rest = line.Substring(dot + 1).Trim();

            string device = rest, friendly = "";
            int op = rest.LastIndexOf('(');
            int cl = rest.LastIndexOf(')');
            if (op >= 0 && cl > op)
            {
                friendly = rest.Substring(op + 1, cl - op - 1).Trim();
                device = rest.Substring(0, op).Trim();
            }
            list.Add(new CaptureInterface { Id = id, Device = device, FriendlyName = friendly });
        }
        return list;
    }

    // ── live capture (dumpcap) + live summary (tshark) ──────────────────────────────

    private static Process? _captureProc;   // dumpcap writing the .pcapng
    private static Process? _summaryProc;   // tshark streaming the live grid
    private static long _packetCount;

    public static bool IsCapturing => _captureProc is { HasExited: false };
    public static long PacketCount => Interlocked.Read(ref _packetCount);

    /// <summary>The seven fields the live/offline summary requests, in grid order.</summary>
    private const string FieldArgs =
        "-T fields -e frame.number -e frame.time_relative -e ip.src -e ip.dst " +
        "-e _ws.col.Protocol -e frame.len -e _ws.col.Info " +
        "-E separator=\\t -E occurrence=f";

    /// <summary>
    /// 開始擷取 · Start a capture: dumpcap writes the file loss-free, and a second tshark process reads the
    /// same live interface to stream a summary grid (each parsed row → <paramref name="onRow"/>; errors →
    /// <paramref name="onLog"/>). Returns a failure result if it can't start.
    /// </summary>
    public static TweakResult StartCapture(CaptureOptions opts, Action<PacketRow> onRow, Action<string> onLog)
    {
        if (IsCapturing) return TweakResult.Fail("A capture is already running.", "已經喺度擷取緊。");
        var dump = DumpcapPath();
        var ts = TsharkPath();
        if (dump is null || ts is null)
            return TweakResult.Fail("Wireshark CLI tools not found.", "搵唔到 Wireshark 命令列工具。");
        if (string.IsNullOrEmpty(opts.InterfaceId))
            return TweakResult.Fail("Pick a capture interface first.", "請先揀一個擷取介面。");
        if (string.IsNullOrEmpty(opts.OutputFile))
            return TweakResult.Fail("Choose an output file first.", "請先揀一個輸出檔案。");

        Interlocked.Exchange(ref _packetCount, 0);

        try
        {
            // 1) dumpcap — the real capture to disk (nothing is lost even if the UI lags).
            var dumpArgs = BuildDumpcapArgs(opts);
            _captureProc = StartProc(dump, dumpArgs, onCaptureLine: line =>
            {
                if (line.Length > 0) onLog(line);
            });
            if (_captureProc is null)
                return TweakResult.Fail("Failed to start dumpcap.", "無法啟動 dumpcap。");

            // 2) tshark — a live read of the same interface, parsed into grid rows.
            //    We capture to stdout only the summary; -l line-buffers, -n skips name resolution (fast).
            var sb = new StringBuilder();
            sb.Append($"-i {opts.InterfaceId} -l -n ");
            if (opts.Promiscuous == false) sb.Append("-p ");
            if (!string.IsNullOrWhiteSpace(opts.CaptureFilter)) sb.Append($"-f \"{Escape(opts.CaptureFilter)}\" ");
            if (!string.IsNullOrWhiteSpace(opts.DisplayFilter)) sb.Append($"-Y \"{Escape(opts.DisplayFilter)}\" ");
            sb.Append(FieldArgs);
            _summaryProc = StartProc(ts, sb.ToString(), onCaptureLine: line =>
            {
                var row = ParseRow(line);
                if (row is null) { if (line.Length > 0) onLog(line); return; }
                Interlocked.Increment(ref _packetCount);
                onRow(row);
            });

            return TweakResult.Ok("Capture started.", "開始擷取。");
        }
        catch (Exception ex)
        {
            StopCapture();
            return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
    }

    /// <summary>停止擷取（畀 dumpcap 自己 flush 個檔案）· Stop the capture; dumpcap flushes the file on kill.</summary>
    public static void StopCapture()
    {
        Kill(ref _summaryProc);
        Kill(ref _captureProc);
    }

    private static string BuildDumpcapArgs(CaptureOptions opts)
    {
        var sb = new StringBuilder();
        sb.Append($"-i {opts.InterfaceId} ");
        if (!opts.Promiscuous) sb.Append("-p ");
        if (!string.IsNullOrWhiteSpace(opts.CaptureFilter)) sb.Append($"-f \"{Escape(opts.CaptureFilter)}\" ");
        if (opts.RingFiles > 1 && opts.RingFileSizeKb > 0)
            sb.Append($"-b files:{opts.RingFiles} -b filesize:{opts.RingFileSizeKb} ");
        if (opts.StopAfterSeconds > 0) sb.Append($"-a duration:{opts.StopAfterSeconds} ");
        if (opts.StopAfterPackets > 0) sb.Append($"-c {opts.StopAfterPackets} ");
        sb.Append($"-w \"{opts.OutputFile}\"");
        return sb.ToString();
    }

    // ── offline read / filter (tshark -r) ───────────────────────────────────────────

    /// <summary>讀一個已存檔案，套用顯示篩選器，回傳摘要列 · Read a saved file, apply a display filter, return rows.</summary>
    public static async Task<List<PacketRow>> ReadFile(string file, string displayFilter, int limit = 50000,
        CancellationToken ct = default)
    {
        var rows = new List<PacketRow>();
        var ts = TsharkPath();
        if (ts is null || !File.Exists(file)) return rows;

        var sb = new StringBuilder();
        sb.Append($"-r \"{file}\" -n ");
        if (!string.IsNullOrWhiteSpace(displayFilter)) sb.Append($"-Y \"{Escape(displayFilter)}\" ");
        sb.Append(FieldArgs);

        string outp;
        try { outp = await ShellRunner.Capture(ts, sb.ToString(), ct); }
        catch { return rows; }

        foreach (var raw in outp.Replace("\r", "").Split('\n'))
        {
            var row = ParseRow(raw);
            if (row is not null) rows.Add(row);
            if (rows.Count >= limit) break;
        }
        return rows;
    }

    /// <summary>逐行串流讀一個檔案 · Stream a file's rows (for large files) via a tracked tshark process.</summary>
    private static Process? _readProc;
    public static bool IsReading => _readProc is { HasExited: false };

    public static TweakResult StreamFile(string file, string displayFilter, Action<PacketRow> onRow, Action onDone)
    {
        if (IsReading) return TweakResult.Fail("Already reading a file.", "已經喺度讀緊檔案。");
        var ts = TsharkPath();
        if (ts is null) return TweakResult.Fail("tshark not found.", "搵唔到 tshark。");
        if (!File.Exists(file)) return TweakResult.Fail("File not found.", "搵唔到檔案。");

        var sb = new StringBuilder();
        sb.Append($"-r \"{file}\" -n -l ");
        if (!string.IsNullOrWhiteSpace(displayFilter)) sb.Append($"-Y \"{Escape(displayFilter)}\" ");
        sb.Append(FieldArgs);
        try
        {
            _readProc = StartProc(ts, sb.ToString(), onCaptureLine: line =>
            {
                var row = ParseRow(line);
                if (row is not null) onRow(row);
            });
            if (_readProc is null) return TweakResult.Fail("Failed to start tshark.", "無法啟動 tshark。");
            _readProc.EnableRaisingEvents = true;
            _readProc.Exited += (_, _) => onDone();
            return TweakResult.Ok("Reading…", "讀緊…");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    public static void StopReading() => Kill(ref _readProc);

    /// <summary>一個封包嘅完整逐層解析 · Full layer-by-layer dissection of one packet (tshark -V).</summary>
    public static async Task<string> PacketDetail(string file, string frameNumber, CancellationToken ct = default)
    {
        var ts = TsharkPath();
        if (ts is null || !File.Exists(file) || string.IsNullOrEmpty(frameNumber)) return "";
        try
        {
            return await ShellRunner.Capture(ts,
                $"-r \"{file}\" -n -V -x -Y \"frame.number=={frameNumber}\"", ct);
        }
        catch { return ""; }
    }

    /// <summary>協定階層統計 · Protocol hierarchy statistics (tshark -z io,phs).</summary>
    public static Task<string> ProtocolStats(string file, CancellationToken ct = default)
        => Stats(file, "-q -z io,phs", ct);

    /// <summary>TCP 對話統計 · TCP conversation statistics (tshark -z conv,tcp).</summary>
    public static Task<string> ConversationStats(string file, CancellationToken ct = default)
        => Stats(file, "-q -z conv,tcp", ct);

    /// <summary>端點統計 · Endpoint statistics (tshark -z endpoints,ip).</summary>
    public static Task<string> EndpointStats(string file, CancellationToken ct = default)
        => Stats(file, "-q -z endpoints,ip", ct);

    private static async Task<string> Stats(string file, string zArgs, CancellationToken ct)
    {
        var ts = TsharkPath();
        if (ts is null || !File.Exists(file)) return "";
        try { return await ShellRunner.Capture(ts, $"-r \"{file}\" -n {zArgs}", ct); }
        catch { return ""; }
    }

    /// <summary>跟蹤 TCP stream 內容 · Follow a TCP stream's reassembled payload (tshark -z follow,tcp,ascii,N).</summary>
    public static async Task<string> FollowTcpStream(string file, int streamIndex, CancellationToken ct = default)
    {
        var ts = TsharkPath();
        if (ts is null || !File.Exists(file)) return "";
        try { return await ShellRunner.Capture(ts, $"-r \"{file}\" -n -q -z follow,tcp,ascii,{streamIndex}", ct); }
        catch { return ""; }
    }

    /// <summary>匯出已篩選嘅子集到新 .pcapng · Export a display-filtered subset to a new .pcapng (tshark -w).</summary>
    public static async Task<TweakResult> ExportFiltered(string sourceFile, string displayFilter, string destFile,
        CancellationToken ct = default)
    {
        var ts = TsharkPath();
        if (ts is null) return TweakResult.Fail("tshark not found.", "搵唔到 tshark。");
        if (!File.Exists(sourceFile)) return TweakResult.Fail("Source file not found.", "搵唔到來源檔案。");
        var df = string.IsNullOrWhiteSpace(displayFilter) ? "" : $"-Y \"{Escape(displayFilter)}\" ";
        return await ShellRunner.Run(ts, $"-r \"{sourceFile}\" {df}-w \"{destFile}\"", false, ct);
    }

    /// <summary>喺 Wireshark GUI 打開個檔案 · Open the saved file in the full Wireshark GUI.</summary>
    public static TweakResult OpenInWireshark(string file)
    {
        var gui = GuiPath();
        if (gui is null) return TweakResult.Fail("Wireshark.exe not found.", "搵唔到 Wireshark.exe。");
        if (!File.Exists(file)) return TweakResult.Fail("File not found.", "搵唔到檔案。");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = gui, Arguments = $"-r \"{file}\"", UseShellExecute = true });
            return TweakResult.Ok("Opened in Wireshark.", "已喺 Wireshark 打開。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    /// <summary>預設輸出檔案路徑 · The default output path (%TEMP%\WinForge-<timestamp>.pcapng).</summary>
    public static string DefaultOutputFile()
        => Path.Combine(Path.GetTempPath(), $"WinForge-{DateTime.Now:yyyyMMdd-HHmmss}.pcapng");

    /// <summary>打開預設擷取資料夾 · Open the default capture folder (%TEMP%) in Explorer.</summary>
    public static TweakResult OpenCaptureFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = Path.GetTempPath(), UseShellExecute = true });
            return TweakResult.Ok("Opened folder.", "已打開資料夾。");
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────

    /// <summary>Parse one tab-separated tshark fields line into a PacketRow (null if it isn't a packet row).</summary>
    private static PacketRow? ParseRow(string line)
    {
        if (string.IsNullOrEmpty(line)) return null;
        line = line.Replace("\r", "");
        var p = line.Split('\t');
        // The first field must be a frame number for this to be a real packet row.
        if (p.Length < 1 || !int.TryParse(p[0].Trim(), out _)) return null;
        string At(int i) => i < p.Length ? p[i].Trim() : "";
        return new PacketRow
        {
            No = At(0),
            Time = FormatTime(At(1)),
            Source = At(2),
            Destination = At(3),
            Protocol = At(4),
            Length = At(5),
            Info = At(6),
        };
    }

    private static string FormatTime(string t)
        => double.TryParse(t, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture)
            : t;

    /// <summary>Start a hidden process whose stdout/stderr lines are forwarded to <paramref name="onCaptureLine"/>.</summary>
    private static Process? StartProc(string fileName, string arguments, Action<string> onCaptureLine)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        var p = Process.Start(psi);
        if (p is null) return null;
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onCaptureLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onCaptureLine(e.Data); };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static void Kill(ref Process? proc)
    {
        var p = proc;
        proc = null;
        try { if (p is { HasExited: false }) p.Kill(true); } catch { }
        try { p?.Dispose(); } catch { }
    }

    private static string Escape(string s) => s.Replace("\"", "\\\"");
}
