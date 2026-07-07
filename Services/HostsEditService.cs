using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 主機檔編輯器（hosts）· Safe, non-elevated HOSTS file editor engine. Reads / parses / rebuilds the
/// Windows hosts file (%WINDIR%\System32\drivers\etc\hosts). All file IO is async and fully guarded —
/// this class NEVER throws; every operation returns a bilingual status string instead. Writing back to
/// the real system hosts path needs admin; on UnauthorizedAccess it reports (does not crash). Pure managed.
/// </summary>
public sealed class HostsEditService
{
    /// <summary>一行主機記錄 · One parsed hosts entry. Uses classic {Binding} in XAML (INotifyPropertyChanged).</summary>
    public sealed class HostEntry : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _enabled = true;
        private string _ip = "";
        private string _hosts = "";
        private string _comment = "";

        public bool Enabled { get => _enabled; set { _enabled = value; Raise(nameof(Enabled)); } }
        public string Ip { get => _ip; set { _ip = value ?? ""; Raise(nameof(Ip)); } }
        public string Hosts { get => _hosts; set { _hosts = value ?? ""; Raise(nameof(Hosts)); } }
        public string Comment { get => _comment; set { _comment = value ?? ""; Raise(nameof(Comment)); } }

        /// <summary>顯示字串 · Display summary used by the ListView row.</summary>
        public string Display => string.IsNullOrWhiteSpace(Comment)
            ? $"{Ip}  →  {Hosts}"
            : $"{Ip}  →  {Hosts}   # {Comment}";

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string n)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(n));
            if (n != nameof(Display))
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Display)));
        }
    }

    /// <summary>已封鎖清單預設 · A named block-list preset (curated ad/tracker domains).</summary>
    public readonly record struct BlockPreset(string En, string Zh, IReadOnlyList<string> Domains);

    /// <summary>綁定用嘅記錄集合 · Live collection bound to the ListView.</summary>
    public ObservableCollection<HostEntry> Entries { get; } = new();

    /// <summary>系統 hosts 檔嘅真實路徑 · The real system hosts path.</summary>
    public static string SystemHostsPath
    {
        get
        {
            var win = Environment.GetEnvironmentVariable("WINDIR");
            if (string.IsNullOrWhiteSpace(win)) win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrWhiteSpace(win)) win = @"C:\Windows";
            return Path.Combine(win, "System32", "drivers", "etc", "hosts");
        }
    }

    // ===== curated block-list presets (small, well-known) =====

    /// <summary>幾個常見預設 · A few curated presets mapping known ad/tracker hosts to 0.0.0.0.</summary>
    public static IReadOnlyList<BlockPreset> Presets { get; } = new List<BlockPreset>
    {
        new("Common ad servers", "常見廣告伺服器", new[]
        {
            "doubleclick.net", "ad.doubleclick.net", "pagead2.googlesyndication.com",
            "googleads.g.doubleclick.net", "adservice.google.com", "ads.yahoo.com",
        }),
        new("Trackers & analytics", "追蹤器同分析", new[]
        {
            "google-analytics.com", "www.google-analytics.com", "ssl.google-analytics.com",
            "connect.facebook.net", "graph.facebook.com", "analytics.tiktok.com",
        }),
        new("Telemetry (misc)", "遙測（雜項）", new[]
        {
            "app-measurement.com", "in.appcenter.ms", "data.flurry.com",
            "settings-win.data.microsoft.com", "vortex.data.microsoft.com",
        }),
    };

    // ===== load / parse =====

    /// <summary>載入系統 hosts 檔並解析 · Read and parse the system hosts file. Never throws.</summary>
    public async Task<string> LoadSystemAsync()
    {
        try
        {
            var path = SystemHostsPath;
            if (!File.Exists(path))
                return Pick($"Hosts file not found at {path}.", $"喺 {path} 搵唔到 hosts 檔。");
            string text;
            using (var sr = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                text = await sr.ReadToEndAsync().ConfigureAwait(true);
            int n = Parse(text);
            return Pick($"Loaded {n} entr{(n == 1 ? "y" : "ies")} from the system hosts file.",
                        $"由系統 hosts 檔載入咗 {n} 條記錄。");
        }
        catch (UnauthorizedAccessException)
        {
            return Pick("Access denied reading the hosts file. Try running WinForge as administrator.",
                        "冇權限讀 hosts 檔。試吓以管理員身分執行 WinForge。");
        }
        catch (Exception ex)
        {
            return Pick($"Could not read the hosts file: {ex.Message}", $"讀唔到 hosts 檔：{ex.Message}");
        }
    }

    /// <summary>解析文字 · Parse hosts text into <see cref="Entries"/>. Returns count of host entries.</summary>
    public int Parse(string? text)
    {
        Entries.Clear();
        if (string.IsNullOrEmpty(text)) return 0;
        int count = 0;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0) continue;

            bool enabled = true;
            string body = trimmed;
            if (trimmed.StartsWith("#"))
            {
                // A commented line MAY be a disabled entry ("# 0.0.0.0 host") or a pure comment.
                var afterHash = trimmed.TrimStart('#').TrimStart();
                if (LooksLikeEntry(afterHash)) { enabled = false; body = afterHash; }
                else continue; // pure comment — dropped from the entry list (preserved conceptually as blanks)
            }

            // split inline trailing comment
            string comment = "";
            int hashIdx = body.IndexOf('#');
            if (hashIdx >= 0)
            {
                comment = body[(hashIdx + 1)..].Trim();
                body = body[..hashIdx].Trim();
            }
            var parts = body.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var ip = parts[0];
            var hosts = string.Join(' ', parts.Skip(1));
            Entries.Add(new HostEntry { Enabled = enabled, Ip = ip, Hosts = hosts, Comment = comment });
            count++;
        }
        return count;
    }

    private static bool LooksLikeEntry(string s)
    {
        var parts = s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && IPAddress.TryParse(parts[0], out _);
    }

    // ===== edits =====

    /// <summary>加一條記錄 · Add an entry (validates the IP).</summary>
    public string Add(string ip, string hosts, string comment)
    {
        ip = (ip ?? "").Trim();
        hosts = (hosts ?? "").Trim();
        if (!IPAddress.TryParse(ip, out _))
            return Pick($"“{ip}” is not a valid IP address.", $"「{ip}」唔係有效嘅 IP 位址。");
        if (hosts.Length == 0)
            return Pick("Enter at least one host name.", "至少要輸入一個主機名稱。");
        Entries.Add(new HostEntry { Enabled = true, Ip = ip, Hosts = hosts, Comment = (comment ?? "").Trim() });
        return Pick($"Added {ip} → {hosts}.", $"已加入 {ip} → {hosts}。");
    }

    /// <summary>套用預設封鎖清單 · Append a block-list preset (0.0.0.0), skipping duplicates.</summary>
    public string ApplyPreset(BlockPreset preset)
    {
        int added = 0;
        var existing = new HashSet<string>(
            Entries.SelectMany(e => e.Hosts.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)),
            StringComparer.OrdinalIgnoreCase);
        foreach (var d in preset.Domains)
        {
            if (existing.Contains(d)) continue;
            Entries.Add(new HostEntry { Enabled = true, Ip = "0.0.0.0", Hosts = d, Comment = Pick("blocked", "已封鎖") });
            existing.Add(d);
            added++;
        }
        return Pick($"Added {added} blocked domain{(added == 1 ? "" : "s")} from “{preset.En}”.",
                    $"由「{preset.Zh}」加入咗 {added} 個封鎖網域。");
    }

    // ===== rebuild / save =====

    /// <summary>重建標準 hosts 文字 · Rebuild canonical hosts text from the entries.</summary>
    public string BuildText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hosts file — generated by WinForge · 由 WinForge 產生");
        sb.AppendLine("# 127.0.0.1  localhost");
        sb.AppendLine();
        foreach (var e in Entries)
        {
            if (string.IsNullOrWhiteSpace(e.Ip) || string.IsNullOrWhiteSpace(e.Hosts)) continue;
            var line = $"{e.Ip}\t{e.Hosts}";
            if (!string.IsNullOrWhiteSpace(e.Comment)) line += $"    # {e.Comment}";
            if (!e.Enabled) line = "# " + line;
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    /// <summary>另存到指定路徑 · Save the rebuilt text to an arbitrary path. Never throws.</summary>
    public async Task<string> SaveAsAsync(string path)
    {
        try
        {
            await File.WriteAllTextAsync(path, BuildText(), new UTF8Encoding(false)).ConfigureAwait(true);
            return Pick($"Saved to {path}.", $"已儲存到 {path}。");
        }
        catch (UnauthorizedAccessException)
        {
            return Pick("Access denied writing that file.", "冇權限寫入嗰個檔案。");
        }
        catch (Exception ex)
        {
            return Pick($"Could not save: {ex.Message}", $"儲存失敗：{ex.Message}");
        }
    }

    /// <summary>寫返系統 hosts 檔（需要管理員）· Write back to the real system hosts. Needs admin. Never throws.</summary>
    public async Task<string> WriteBackAsync()
    {
        var path = SystemHostsPath;
        try
        {
            await File.WriteAllTextAsync(path, BuildText(), new UTF8Encoding(false)).ConfigureAwait(true);
            return Pick("Wrote the system hosts file. Changes take effect immediately.",
                        "已寫入系統 hosts 檔。改動即時生效。");
        }
        catch (UnauthorizedAccessException)
        {
            return Pick("Access denied — run WinForge as administrator to write the system hosts file.",
                        "冇權限 — 要以管理員身分執行 WinForge 先可以寫入系統 hosts 檔。");
        }
        catch (IOException ex)
        {
            return Pick($"Could not write the hosts file (it may be locked): {ex.Message}",
                        $"寫唔到 hosts 檔（可能被鎖住）：{ex.Message}");
        }
        catch (Exception ex)
        {
            return Pick($"Could not write the hosts file: {ex.Message}", $"寫唔到 hosts 檔：{ex.Message}");
        }
    }

    private static string Pick(string en, string zh) => Loc.I.Pick(en, zh);
}
