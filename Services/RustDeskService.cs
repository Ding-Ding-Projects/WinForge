using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個已儲存嘅遠端對端 · One saved RustDesk peer (address-book entry).</summary>
public sealed class RdPeer
{
    public string Id { get; set; } = "";
    public string Alias { get; set; } = "";
    public bool ViewOnly { get; set; }
    /// <summary>顯示用名稱 · Display label.</summary>
    public string Display => string.IsNullOrWhiteSpace(Alias) ? Id : $"{Alias} ({Id})";
}

/// <summary>RustDesk 伺服器設定 · RustDesk server/relay configuration block.</summary>
public sealed class RdServerConfig
{
    /// <summary>ID 伺服器（custom-rendezvous-server）· ID / rendezvous server.</summary>
    public string IdServer { get; set; } = "";
    public string RelayServer { get; set; } = "";
    public string ApiServer { get; set; } = "";
    /// <summary>公開金鑰 · Server public key.</summary>
    public string Key { get; set; } = "";
}

/// <summary>
/// 應用程式內 RustDesk 控制（包 rustdesk.exe CLI + 讀寫 TOML 設定）·
/// In-app RustDesk control. Locates rustdesk.exe under %ProgramFiles%\RustDesk, reads the local ID and
/// permanent password, drives the CLI for connect / set-password / install-service, and reads/writes the
/// RustDesk2.toml options table (ID/relay/api server + key). Saved peers persist in WinForge settings.
/// AGPL: only the unmodified RustDesk binary is launched — nothing is bundled or derived. No redirect.
/// </summary>
public static class RustDeskService
{
    private static readonly string[] ExeCandidates =
    {
        @"C:\Program Files\RustDesk\rustdesk.exe",
        @"C:\Program Files (x86)\RustDesk\rustdesk.exe",
    };

    private static string LocalProgramsExe => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "RustDesk", "rustdesk.exe");

    /// <summary>解析到嘅 rustdesk.exe（搵唔到就用 PATH 名）· Resolved exe path, falling back to the PATH name.</summary>
    public static string Exe
    {
        get
        {
            var hit = ExeCandidates.FirstOrDefault(File.Exists);
            if (hit is not null) return hit;
            return File.Exists(LocalProgramsExe) ? LocalProgramsExe : "rustdesk.exe";
        }
    }

    /// <summary>%AppData%\RustDesk\config 資料夾 · The RustDesk config directory.</summary>
    public static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RustDesk", "config");

    public static string Config2Path => Path.Combine(ConfigDir, "RustDesk2.toml");
    public static string ConfigPath => Path.Combine(ConfigDir, "RustDesk.toml");

    /// <summary>裝咗冇？· Whether RustDesk appears installed.</summary>
    public static bool IsInstalled => ExeCandidates.Any(File.Exists)
        || File.Exists(LocalProgramsExe)
        || Directory.Exists(ConfigDir);

    /// <summary>RustDesk 後台／服務行緊冇？· Whether a RustDesk process is currently running.</summary>
    public static bool IsRunning
    {
        get
        {
            try { return Process.GetProcessesByName("rustdesk").Length > 0; }
            catch { return false; }
        }
    }

    // ===================== CLI =====================

    private static async Task<string> Cli(string args, CancellationToken ct)
    {
        try
        {
            var r = await ShellRunner.Run(Exe, args, elevated: false, ct);
            return (r.Output ?? "").Trim();
        }
        catch { return ""; }
    }

    /// <summary>攞本機 ID（先試 CLI，唔得就讀 TOML）· Read this PC's ID — CLI first, TOML fallback.</summary>
    public static async Task<string> GetLocalId(CancellationToken ct = default)
    {
        var fromCli = CleanId(await Cli("--get-id", ct));
        if (LooksLikeId(fromCli)) return fromCli;

        var fromToml = ReadTomlValue(ConfigPath, "id");
        return CleanId(fromToml ?? "");
    }

    /// <summary>由 ID 連去一部遠端機（view-only 可選）· Connect to a peer by ID.</summary>
    public static Task<TweakResult> Connect(string peerId, bool viewOnly = false, CancellationToken ct = default)
    {
        peerId = CleanId(peerId);
        if (peerId.Length == 0) return Task.FromResult(TweakResult.Fail("Enter a peer ID first.", "請先輸入對端 ID。"));
        if (Exe == "rustdesk.exe" && !IsInstalled)
            return Task.FromResult(TweakResult.Fail("RustDesk is not installed.", "未安裝 RustDesk。"));
        var args = viewOnly ? $"--view-only --connect {peerId}" : $"--connect {peerId}";
        return Launch(args, ct);
    }

    /// <summary>設定永久密碼（需要已安裝 + 系統服務 + 管理員）· Set the permanent password (needs the service + admin).</summary>
    public static async Task<TweakResult> SetPassword(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password))
            return TweakResult.Fail("Enter a password first.", "請先輸入密碼。");
        // --password requires elevation and the installed service; run elevated.
        var r = await ShellRunner.Run(Exe, $"--password \"{password}\"", elevated: true, ct);
        if (!r.Success) return r;
        return TweakResult.Ok("Permanent password updated.", "已更新永久密碼。", r.Output);
    }

    /// <summary>安裝 RustDesk 系統服務（無人值守）· Install the RustDesk system service (unattended).</summary>
    public static Task<TweakResult> InstallService(CancellationToken ct = default)
        => ShellRunner.Run(Exe, "--install-service", elevated: true, ct);

    /// <summary>移除 RustDesk 系統服務 · Uninstall the RustDesk system service.</summary>
    public static Task<TweakResult> UninstallService(CancellationToken ct = default)
        => ShellRunner.Run(Exe, "--uninstall-service", elevated: true, ct);

    /// <summary>淨開 RustDesk 主視窗 · Just launch the RustDesk main window.</summary>
    public static Task<TweakResult> Launch(CancellationToken ct = default) => Launch("", ct);

    private static Task<TweakResult> Launch(string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = Exe,
                Arguments = args,
                UseShellExecute = true,
            };
            var p = Process.Start(psi);
            return Task.FromResult(p is null
                ? TweakResult.Fail("Failed to launch RustDesk.", "無法啟動 RustDesk。")
                : TweakResult.Ok("RustDesk launched.", "已啟動 RustDesk。"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }
    }

    /// <summary>匯入 RustDesk 設定字串／檔（--import-config）· Import a server config string or file.</summary>
    public static Task<TweakResult> ImportConfig(string filePath, CancellationToken ct = default)
        => ShellRunner.Run(Exe, $"--import-config \"{filePath}\"", elevated: true, ct);

    // ===================== Server settings (TOML) =====================

    /// <summary>由 RustDesk2.toml 讀伺服器設定 · Read the server settings from RustDesk2.toml.</summary>
    public static RdServerConfig ReadServerConfig()
    {
        var cfg = new RdServerConfig();
        try
        {
            if (!File.Exists(Config2Path)) return cfg;
            var opts = ReadOptionsTable(Config2Path);
            cfg.IdServer = opts.GetValueOrDefault("custom-rendezvous-server", "");
            cfg.RelayServer = opts.GetValueOrDefault("relay-server", "");
            cfg.ApiServer = opts.GetValueOrDefault("api-server", "");
            cfg.Key = opts.GetValueOrDefault("key", "");
        }
        catch { }
        return cfg;
    }

    /// <summary>
    /// 寫伺服器設定入 RustDesk2.toml · Persist server settings to the [options] table. RustDesk should be
    /// stopped first or it may overwrite the file; restart it afterwards for the change to take effect.
    /// </summary>
    public static Task<TweakResult> WriteServerConfig(RdServerConfig cfg, CancellationToken ct = default)
    {
        var map = new Dictionary<string, string>
        {
            ["custom-rendezvous-server"] = cfg.IdServer?.Trim() ?? "",
            ["relay-server"] = cfg.RelayServer?.Trim() ?? "",
            ["api-server"] = cfg.ApiServer?.Trim() ?? "",
            ["key"] = cfg.Key?.Trim() ?? "",
        };

        try
        {
            WriteOptionsTable(Config2Path, map);
            return Task.FromResult(TweakResult.Ok(
                "Server settings saved. Restart RustDesk for them to take effect.",
                "已儲存伺服器設定。重啟 RustDesk 後生效。"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }
    }

    // ===================== Saved peers (WinForge settings) =====================

    private const string PeersKey = "rustdesk.peers";
    private const char Sep = ''; // unit separator between fields

    public static List<RdPeer> LoadPeers()
    {
        var list = new List<RdPeer>();
        try
        {
            var raw = SettingsStore.Get(PeersKey, "");
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(Sep);
                if (parts.Length == 0 || parts[0].Trim().Length == 0) continue;
                list.Add(new RdPeer
                {
                    Id = parts[0].Trim(),
                    Alias = parts.Length > 1 ? parts[1] : "",
                    ViewOnly = parts.Length > 2 && parts[2] == "1",
                });
            }
        }
        catch { }
        return list;
    }

    public static void SavePeers(IEnumerable<RdPeer> peers)
    {
        var sb = new StringBuilder();
        foreach (var p in peers)
        {
            if (string.IsNullOrWhiteSpace(p.Id)) continue;
            sb.Append(p.Id.Trim()).Append(Sep)
              .Append(p.Alias ?? "").Append(Sep)
              .Append(p.ViewOnly ? "1" : "0").Append('\n');
        }
        SettingsStore.Set(PeersKey, sb.ToString());
    }

    // ===================== TOML helpers (minimal, dependency-free) =====================

    /// <summary>清理 ID（淨低字母數字）· Normalise an entered ID (keep only alphanumerics).</summary>
    public static string CleanId(string raw)
        => new string((raw ?? "").Where(char.IsLetterOrDigit).ToArray());

    private static bool LooksLikeId(string s) => s.Length >= 6 && s.All(char.IsLetterOrDigit);

    /// <summary>讀一個頂層 key=value（用喺 RustDesk.toml 嘅 id 等）· Read a top-level scalar key from a TOML file.</summary>
    private static string? ReadTomlValue(string path, string key)
    {
        try
        {
            if (!File.Exists(path)) return null;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.StartsWith('[') || line.StartsWith('#')) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                if (!line.Substring(0, eq).Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                return Unquote(line.Substring(eq + 1).Trim());
            }
        }
        catch { }
        return null;
    }

    /// <summary>讀 [options] 表入面所有 key=value · Read every key=value under the [options] table.</summary>
    private static Dictionary<string, string> ReadOptionsTable(string path)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool inOptions = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('['))
            {
                inOptions = line.Equals("[options]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            if (!inOptions || line.Length == 0 || line.StartsWith('#')) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            map[line.Substring(0, eq).Trim()] = Unquote(line.Substring(eq + 1).Trim());
        }
        return map;
    }

    /// <summary>
    /// 將指定 key=value 寫入／更新 [options] 表，保留其他內容。空值會移除該 key。
    /// Merge the given keys into the [options] table, preserving everything else. Empty values remove the key.
    /// </summary>
    private static void WriteOptionsTable(string path, Dictionary<string, string> updates)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = File.Exists(path)
            ? new List<string>(File.ReadAllLines(path))
            : new List<string>();

        // Locate [options] section boundaries.
        int sectionStart = -1, sectionEnd = lines.Count;
        for (int i = 0; i < lines.Count; i++)
        {
            var t = lines[i].Trim();
            if (sectionStart < 0)
            {
                if (t.Equals("[options]", StringComparison.OrdinalIgnoreCase)) sectionStart = i;
            }
            else if (t.StartsWith('['))
            {
                sectionEnd = i;
                break;
            }
        }

        if (sectionStart < 0)
        {
            // No [options] yet — append a fresh one.
            if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
            lines.Add("[options]");
            sectionStart = lines.Count - 1;
            sectionEnd = lines.Count;
        }

        var remaining = new Dictionary<string, string>(updates, StringComparer.OrdinalIgnoreCase);

        // Update existing keys inside the section.
        for (int i = sectionStart + 1; i < sectionEnd; i++)
        {
            var t = lines[i].Trim();
            int eq = t.IndexOf('=');
            if (eq <= 0) continue;
            var k = t.Substring(0, eq).Trim();
            if (!remaining.TryGetValue(k, out var v)) continue;
            lines[i] = v.Length == 0 ? " " : $"{k} = {Quote(v)}";
            remaining.Remove(k);
        }

        // Append any new non-empty keys at the end of the section.
        var toAppend = remaining.Where(kv => kv.Value.Length > 0)
                                .Select(kv => $"{kv.Key} = {Quote(kv.Value)}").ToList();
        if (toAppend.Count > 0) lines.InsertRange(sectionEnd, toAppend);

        // Drop the placeholder lines left by cleared keys.
        var cleaned = lines.Where(l => l != " ").ToList();
        File.WriteAllLines(path, cleaned, new UTF8Encoding(false));
    }

    private static string Unquote(string s)
    {
        s = s.Trim();
        if (s.Length >= 2 && ((s[0] == '"' && s[^1] == '"') || (s[0] == '\'' && s[^1] == '\'')))
            return s.Substring(1, s.Length - 2);
        return s;
    }

    private static string Quote(string s) => "'" + s.Replace("'", "") + "'";
}
