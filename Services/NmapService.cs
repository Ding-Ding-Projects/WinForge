using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>一個被掃描嘅主機 · One scanned host with its address, hostname, status and OS guess.</summary>
public sealed class NmapHost
{
    public string Address { get; set; } = "";
    public string AddrType { get; set; } = "";   // ipv4 / ipv6 / mac
    public string Hostname { get; set; } = "";
    public string Status { get; set; } = "";      // up / down
    public string Vendor { get; set; } = "";
    public string Os { get; set; } = "";          // best OS match
    public int OsAccuracy { get; set; }
    public string Latency { get; set; } = "";
    public List<NmapPort> Ports { get; } = new();

    /// <summary>顯示用標題 · A display label combining hostname + address.</summary>
    public string Display => string.IsNullOrEmpty(Hostname) ? Address : $"{Hostname} ({Address})";
}

/// <summary>一個連接埠／服務列 · One port/service row in the results grid.</summary>
public sealed class NmapPort
{
    public string HostAddress { get; set; } = "";
    public string HostName { get; set; } = "";
    public int Port { get; set; }
    public string Protocol { get; set; } = "";    // tcp / udp
    public string State { get; set; } = "";        // open / closed / filtered
    public string Service { get; set; } = "";
    public string Version { get; set; } = "";      // product + version + extrainfo

    public string HostDisplay => string.IsNullOrEmpty(HostName) ? HostAddress : $"{HostName} ({HostAddress})";
    public string PortProto => $"{Port}/{Protocol}";
}

/// <summary>一次掃描嘅完整結果 · The full result of one scan run.</summary>
public sealed class NmapScanResult
{
    public List<NmapHost> Hosts { get; } = new();
    public string RawXml { get; set; } = "";
    public string Command { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Error { get; set; } = "";
    public bool Ok { get; set; }
    public bool Cancelled { get; set; }

    /// <summary>攤平嘅連接埠列表（畀格網用）· Flattened port rows across all hosts for the grid.</summary>
    public List<NmapPort> AllPorts =>
        Hosts.SelectMany(h => h.Ports).OrderBy(p => p.HostAddress, StringComparer.Ordinal)
             .ThenBy(p => p.Port).ToList();
}

/// <summary>一個掃描設定檔 · A named scan profile mapping to nmap flag presets.</summary>
public sealed class NmapProfile
{
    public string Key { get; init; } = "";
    public string En { get; init; } = "";
    public string Zh { get; init; } = "";
    public string Flags { get; init; } = "";
    public bool NeedsAdmin { get; init; }
}

/// <summary>
/// 應用程式內 Nmap 掃描引擎（包真實 nmap.exe）· In-app Nmap scanner wrapping nmap.exe — locates the
/// binary, composes a command line from a target + scan profile + flag toggles, always appends
/// <c>-oX -</c> for machine-readable XML on stdout, runs off the UI thread with cancel (kills the child
/// process), and deserialises the XML into hosts / ports / services. No redirect; bilingual UI in the page.
/// nmap comes from winget id <c>Insecure.Nmap</c> (bundles Npcap). Bilingual.
/// </summary>
public static class NmapService
{
    public const string WingetId = "Insecure.Nmap";

    private static string? _cachedPath;

    /// <summary>清除快取嘅 nmap 路徑（安裝後重新偵測）· Clear the cached nmap path so we re-detect after an install.</summary>
    public static void Rescan() => _cachedPath = null;

    /// <summary>定位 nmap.exe（PATH → 登錄檔 → 預設安裝路徑）· Locate nmap.exe via PATH, the registry, or the default install dir.</summary>
    public static string? FindNmap()
    {
        if (_cachedPath is not null && File.Exists(_cachedPath)) return _cachedPath;

        // 1) On PATH
        try
        {
            var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in pathVar.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    var cand = Path.Combine(dir.Trim(), "nmap.exe");
                    if (File.Exists(cand)) { _cachedPath = cand; return cand; }
                }
                catch { }
            }
        }
        catch { }

        // 2) Uninstall registry key written by the Nmap installer
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var view in new[] { "SOFTWARE\\WOW6432Node\\Nmap", "SOFTWARE\\Nmap" })
            {
                try
                {
                    using var k = root.OpenSubKey(view);
                    var dir = k?.GetValue(null) as string ?? k?.GetValue("Install_Dir") as string;
                    if (!string.IsNullOrEmpty(dir))
                    {
                        var cand = Path.Combine(dir, "nmap.exe");
                        if (File.Exists(cand)) { _cachedPath = cand; return cand; }
                    }
                }
                catch { }
            }
        }

        // 3) Default install locations
        foreach (var pf in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                 })
        {
            try
            {
                if (string.IsNullOrEmpty(pf)) continue;
                var cand = Path.Combine(pf, "Nmap", "nmap.exe");
                if (File.Exists(cand)) { _cachedPath = cand; return cand; }
            }
            catch { }
        }

        return null;
    }

    public static bool IsAvailable() => FindNmap() is not null;

    /// <summary>所有掃描設定檔 · The built-in scan profiles (label resolved bilingually in the page).</summary>
    public static IReadOnlyList<NmapProfile> Profiles { get; } = new List<NmapProfile>
    {
        new() { Key = "ping",    En = "Ping sweep (host discovery)", Zh = "Ping 掃描（探測主機）", Flags = "-sn" },
        new() { Key = "quick",   En = "Quick scan",                  Zh = "快速掃描",              Flags = "-T4 -F" },
        new() { Key = "quickv",  En = "Quick + version",             Zh = "快速 + 版本偵測",       Flags = "-T4 -F -sV" },
        new() { Key = "intense", En = "Intense scan",                Zh = "深度掃描",              Flags = "-T4 -A -v", NeedsAdmin = true },
        new() { Key = "full",    En = "Full TCP (all 65535 ports)",  Zh = "完整 TCP（全部 65535 埠）", Flags = "-p- -T4" },
        new() { Key = "service", En = "Service / version detection", Zh = "服務／版本偵測",        Flags = "-sV" },
        new() { Key = "os",      En = "OS detection",                Zh = "作業系統偵測",          Flags = "-O", NeedsAdmin = true },
        new() { Key = "scripts", En = "Default scripts (-sC)",       Zh = "預設指令稿（-sC）",     Flags = "-sC" },
        new() { Key = "udp",     En = "UDP scan (top ports)",        Zh = "UDP 掃描（常用埠）",    Flags = "-sU -T4", NeedsAdmin = true },
        new() { Key = "custom",  En = "Custom (flags only)",         Zh = "自訂（只用旗標）",      Flags = "" },
    };

    public static NmapProfile ProfileByKey(string key)
        => Profiles.FirstOrDefault(p => p.Key == key) ?? Profiles[1];

    /// <summary>勾選旗標 · One togglable common flag and what it costs.</summary>
    public sealed record FlagOption(string Flag, string En, string Zh, bool NeedsAdmin);

    public static IReadOnlyList<FlagOption> CommonFlags { get; } = new List<FlagOption>
    {
        new("-sV", "Service/version (-sV)", "服務／版本（-sV）", false),
        new("-O",  "OS detection (-O)",     "作業系統偵測（-O）", true),
        new("-sC", "Default scripts (-sC)", "預設指令稿（-sC）", false),
        new("-Pn", "Skip ping (-Pn)",       "跳過 ping（-Pn）",  false),
        new("-A",  "Aggressive (-A)",       "進取（-A）",        true),
        new("-T4", "Fast timing (-T4)",     "快速時序（-T4）",   false),
        new("-sU", "UDP scan (-sU)",        "UDP 掃描（-sU）",   true),
    };

    /// <summary>
    /// 驗證掃描目標 · Validate a target string (single IP/host, CIDR, range or space-separated list).
    /// 唔做 shell 串接，淨係用 ArgumentList，但仍然拒絕明顯危險嘅字元。
    /// We never shell-concatenate; args go through ArgumentList. Still reject obviously bad input.
    /// </summary>
    public static bool IsValidTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        var t = target.Trim();
        if (t.Length > 4000) return false;
        // Reject shell metacharacters that should never appear in a host/CIDR/range spec.
        if (Regex.IsMatch(t, "[\"'`|&;<>^%\r\n]")) return false;
        // Each token must look like a host / ip / cidr / range / wildcard.
        foreach (var tok in t.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            if (!Regex.IsMatch(tok, "^[A-Za-z0-9_.:/*\\-]+$")) return false;
        return true;
    }

    /// <summary>
    /// 由設定檔 + 勾選旗標 + 額外旗標建構 nmap 引數 · Build the ordered nmap argument list.
    /// 永遠加 <c>-oX -</c>。重複旗標會去重。<c>-oX -</c> is always appended; duplicate flags are de-duped.
    /// </summary>
    public static List<string> BuildArgs(string profileKey, IEnumerable<string> extraFlags,
        string extraText, string target)
    {
        var args = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddFlag(string f)
        {
            f = f.Trim();
            if (f.Length == 0) return;
            // -p- and -p<x> can coexist with others; only de-dup exact repeats.
            if (seen.Add(f)) args.Add(f);
        }

        var profile = ProfileByKey(profileKey);
        foreach (var f in profile.Flags.Split(' ', StringSplitOptions.RemoveEmptyEntries)) AddFlag(f);
        foreach (var f in extraFlags) AddFlag(f);

        // Free-form extra flags (e.g. "-p 80,443" or "--script vuln"). Tokenise on whitespace.
        if (!string.IsNullOrWhiteSpace(extraText))
            foreach (var tok in extraText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                AddFlag(tok);

        // Machine-readable XML on stdout — always.
        args.Add("-oX");
        args.Add("-");

        // Targets last.
        foreach (var tok in target.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            args.Add(tok);

        return args;
    }

    /// <summary>係咪需要管理員權限（旗標或設定檔要 raw socket / Npcap）· Whether these args need elevation.</summary>
    public static bool NeedsAdmin(string profileKey, IEnumerable<string> extraFlags)
    {
        if (ProfileByKey(profileKey).NeedsAdmin) return true;
        var adminFlags = CommonFlags.Where(f => f.NeedsAdmin).Select(f => f.Flag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return extraFlags.Any(f => adminFlags.Contains(f));
    }

    /// <summary>引數預覽字串（畀唯讀命令列預覽）· A human-readable preview of the full command line.</summary>
    public static string PreviewCommand(IReadOnlyList<string> args)
    {
        var sb = new StringBuilder("nmap");
        foreach (var a in args)
            sb.Append(' ').Append(a.Contains(' ') ? $"\"{a}\"" : a);
        return sb.ToString();
    }

    /// <summary>
    /// 執行一次掃描 · Run a scan with the given argument list, streaming progress lines via
    /// <paramref name="onProgress"/> and honouring cancellation by killing the child process tree.
    /// </summary>
    public static async Task<NmapScanResult> RunScanAsync(IReadOnlyList<string> args,
        Action<string>? onProgress, CancellationToken ct)
    {
        var result = new NmapScanResult { Command = PreviewCommand(args) };
        var exe = FindNmap();
        if (exe is null)
        {
            result.Error = "nmap.exe not found.";
            return result;
        }

        var psi = new ProcessStartInfo
        {
            FileName = exe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        try
        {
            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                stderr.AppendLine(e.Data);
                onProgress?.Invoke(e.Data);
            };

            if (!p.Start())
            {
                result.Error = "Failed to start nmap.";
                return result;
            }
            p.BeginErrorReadLine();

            // Read stdout (the XML) to the end on a background task.
            var outTask = p.StandardOutput.ReadToEndAsync(ct);

            try
            {
                await p.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                result.Cancelled = true;
            }

            try { stdout.Append(await outTask); } catch { /* cancelled mid-read */ }

            if (result.Cancelled) { result.Error = "Cancelled."; return result; }

            result.RawXml = stdout.ToString();
            Parse(result);
            result.Ok = result.Hosts.Count > 0 || result.RawXml.Contains("<nmaprun");
            if (!result.Ok && stderr.Length > 0) result.Error = stderr.ToString().Trim();
        }
        catch (OperationCanceledException)
        {
            result.Cancelled = true;
            result.Error = "Cancelled.";
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>解析 nmap 嘅 -oX XML 輸出 · Parse the -oX XML into hosts/ports/services.</summary>
    public static void Parse(NmapScanResult result)
    {
        if (string.IsNullOrWhiteSpace(result.RawXml)) return;
        XDocument doc;
        try { doc = XDocument.Parse(result.RawXml); }
        catch { return; }

        var run = doc.Root;
        if (run is null) return;

        foreach (var hostEl in run.Elements("host"))
        {
            var host = new NmapHost();

            var status = hostEl.Element("status");
            host.Status = status?.Attribute("state")?.Value ?? "";

            // Addresses: ipv4/ipv6 first, mac for vendor.
            foreach (var addr in hostEl.Elements("address"))
            {
                var type = addr.Attribute("addrtype")?.Value ?? "";
                var val = addr.Attribute("addr")?.Value ?? "";
                if (type is "ipv4" or "ipv6")
                {
                    if (string.IsNullOrEmpty(host.Address)) { host.Address = val; host.AddrType = type; }
                }
                else if (type == "mac")
                {
                    if (string.IsNullOrEmpty(host.Address)) { host.Address = val; host.AddrType = type; }
                    host.Vendor = addr.Attribute("vendor")?.Value ?? "";
                }
            }

            // Hostname (first one).
            host.Hostname = hostEl.Element("hostnames")?.Elements("hostname")
                .FirstOrDefault()?.Attribute("name")?.Value ?? "";

            // Latency.
            var times = hostEl.Element("times");
            if (times?.Attribute("srtt")?.Value is string srtt && double.TryParse(srtt, out var us))
                host.Latency = $"{us / 1000.0:0.#} ms";

            // OS best match.
            var osMatch = hostEl.Element("os")?.Elements("osmatch").FirstOrDefault();
            if (osMatch is not null)
            {
                host.Os = osMatch.Attribute("name")?.Value ?? "";
                if (int.TryParse(osMatch.Attribute("accuracy")?.Value, out var acc)) host.OsAccuracy = acc;
            }

            // Ports.
            var portsEl = hostEl.Element("ports");
            if (portsEl is not null)
            {
                foreach (var portEl in portsEl.Elements("port"))
                {
                    var stateEl = portEl.Element("state");
                    var svcEl = portEl.Element("service");
                    var port = new NmapPort
                    {
                        HostAddress = host.Address,
                        HostName = host.Hostname,
                        Protocol = portEl.Attribute("protocol")?.Value ?? "",
                        State = stateEl?.Attribute("state")?.Value ?? "",
                        Service = svcEl?.Attribute("name")?.Value ?? "",
                    };
                    if (int.TryParse(portEl.Attribute("portid")?.Value, out var pid)) port.Port = pid;

                    // Version string from product / version / extrainfo.
                    var parts = new List<string>();
                    if (svcEl?.Attribute("product")?.Value is { Length: > 0 } prod) parts.Add(prod);
                    if (svcEl?.Attribute("version")?.Value is { Length: > 0 } ver) parts.Add(ver);
                    if (svcEl?.Attribute("extrainfo")?.Value is { Length: > 0 } extra) parts.Add($"({extra})");
                    port.Version = string.Join(" ", parts);

                    host.Ports.Add(port);
                }
            }

            result.Hosts.Add(host);
        }

        // Run summary from <runstats><finished>.
        var fin = run.Element("runstats")?.Element("finished");
        var hostsEl = run.Element("runstats")?.Element("hosts");
        var up = hostsEl?.Attribute("up")?.Value ?? "?";
        var total = hostsEl?.Attribute("total")?.Value ?? "?";
        var elapsed = fin?.Attribute("elapsed")?.Value;
        result.Summary = elapsed is not null
            ? $"{up}/{total} hosts up · {elapsed}s"
            : $"{up}/{total} hosts up";
    }

    /// <summary>將結果攤平成 CSV · Flatten the result into a CSV string (host,port,proto,state,service,version).</summary>
    public static string ToCsv(NmapScanResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Host,Address,Port,Protocol,State,Service,Version,OS");
        foreach (var h in result.Hosts)
        {
            if (h.Ports.Count == 0)
            {
                sb.AppendLine(string.Join(",", Csv(h.Hostname), Csv(h.Address), "", "", Csv(h.Status), "", "", Csv(h.Os)));
                continue;
            }
            foreach (var p in h.Ports)
                sb.AppendLine(string.Join(",", Csv(h.Hostname), Csv(h.Address), p.Port.ToString(),
                    Csv(p.Protocol), Csv(p.State), Csv(p.Service), Csv(p.Version), Csv(h.Os)));
        }
        return sb.ToString();
    }

    private static string Csv(string s)
    {
        s ??= "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
