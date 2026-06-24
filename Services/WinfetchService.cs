using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 一行系統資訊（標題 + 內容 + 可選百分比）· One winfetch info row (title + value, optional percent for a bar).
/// </summary>
public sealed class FetchRow
{
    public string TitleEn { get; init; } = "";
    public string TitleZh { get; init; } = "";
    public string Value { get; init; } = "—";
    /// <summary>0–100 用嚟畫進度條；-1 = 唔畫 · 0–100 to draw a usage bar; -1 = no bar.</summary>
    public int Percent { get; init; } = -1;
    /// <summary>用嚟匯出純文字嘅穩定英文標題 · Stable English title used for plain-text/ASCII export.</summary>
    public string PlainTitle => string.IsNullOrEmpty(TitleEn) ? "" : TitleEn;
}

/// <summary>
/// 一個 winfetch 風格嘅完整快照 · A full winfetch-style snapshot: title line, the colour-bar legend
/// and every info row. 所有欄位各自 try/catch，一個失敗唔會搞砸成版 · every field is isolated so one
/// WMI failure never blanks the panel.
/// </summary>
public sealed class FetchSnapshot
{
    public string User { get; init; } = "";
    public string Host { get; init; } = "";
    public List<FetchRow> Rows { get; } = new();
}

/// <summary>
/// 原生 winfetch 克隆嘅資料層 · The data layer for the native winfetch clone. Reuses <see cref="SystemInfo"/>
/// where possible and adds the slower WMI/CIM-backed fields (host, motherboard, GPUs, monitors, disks,
/// package-manager counts, theme, terminal). Everything runs off the UI thread.
/// </summary>
public static class WinfetchService
{
    // ---- monitor enumeration via EnumDisplayMonitors ----
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc proc, IntPtr data);

    private delegate bool MonitorEnumProc(IntPtr hMon, IntPtr hdc, ref Rect r, IntPtr data);

    private static List<string> Monitors()
    {
        var list = new List<string>();
        try
        {
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr _, IntPtr _, ref Rect r, IntPtr _) =>
            {
                list.Add($"{r.Right - r.Left}x{r.Bottom - r.Top}");
                return true;
            }, IntPtr.Zero);
        }
        catch { /* ignore */ }
        return list;
    }

    private static string Wmi(string cls, string prop)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
            foreach (var o in searcher.Get())
                return o[prop]?.ToString()?.Trim() ?? "";
        }
        catch { /* ignore */ }
        return "";
    }

    private static List<string> WmiAll(string cls, string prop)
    {
        var list = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {prop} FROM {cls}");
            foreach (var o in searcher.Get())
            {
                var v = o[prop]?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(v)) list.Add(v);
            }
        }
        catch { /* ignore */ }
        return list;
    }

    // ---- theme ----
    private static string ThemeDescription(Func<string, string, string> pick)
    {
        try
        {
            var sys = RegistryHelper.GetValue(RegRoot.HKCU,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme");
            var app = RegistryHelper.GetValue(RegRoot.HKCU,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme");
            string Light(object? o, string l, string d) => o is int i && i != 0 ? l : d;
            var sysT = Light(sys, pick("Light", "淺色"), pick("Dark", "深色"));
            var appT = Light(app, pick("Light", "淺色"), pick("Dark", "深色"));
            var name = "—";
            var cur = RegistryHelper.GetValue(RegRoot.HKCU,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes", "CurrentTheme")?.ToString();
            if (!string.IsNullOrEmpty(cur))
                name = Path.GetFileNameWithoutExtension(cur);
            return pick(
                $"{name} (System: {sysT}, Apps: {appT})",
                $"{name}（系統：{sysT}、應用程式：{appT}）");
        }
        catch { return "—"; }
    }

    // ---- package-manager counts ----
    private static async Task<string> PackageCounts(CancellationToken ct)
    {
        var parts = new List<string>();

        // winget — count lines minus header
        try
        {
            var outp = await ShellRunner.Capture("winget",
                "list --accept-source-agreements --disable-interactivity", ct);
            if (!string.IsNullOrWhiteSpace(outp))
            {
                int n = outp.Split('\n')
                    .Count(l => l.Trim().Trim('-', '\\', '|', '/', ' ', '\r', '\t', '\b').Length != 0) - 1;
                if (n > 0) parts.Add($"{n} (winget)");
            }
        }
        catch { /* absent → skip */ }

        // scoop — count app dirs minus "current"
        try
        {
            var scoop = Environment.GetEnvironmentVariable("SCOOP");
            var dir = string.IsNullOrEmpty(scoop)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "scoop", "apps")
                : Path.Combine(scoop, "apps");
            if (Directory.Exists(dir))
            {
                int n = Directory.GetDirectories(dir).Length - 1;
                if (n > 0) parts.Add($"{n} (scoop)");
            }
        }
        catch { /* skip */ }

        // choco — last line's first token minus 1
        try
        {
            var outp = await ShellRunner.Capture("choco", "list --local-only --limit-output", ct);
            if (!string.IsNullOrWhiteSpace(outp))
            {
                int n = outp.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));
                if (n > 0) parts.Add($"{n} (choco)");
            }
        }
        catch { /* skip */ }

        return parts.Count == 0 ? "(none) · 無" : string.Join(", ", parts);
    }

    private static string ShellVersion()
    {
        try
        {
            // PowerShell (Windows) version from the registry; pwsh handled separately.
            var v = RegistryHelper.GetValue(RegRoot.HKLM,
                @"SOFTWARE\Microsoft\PowerShell\3\PowerShellEngine", "PowerShellVersion")?.ToString();
            if (!string.IsNullOrEmpty(v)) return $"PowerShell v{v}";
        }
        catch { /* ignore */ }
        return "PowerShell";
    }

    private static string Terminal()
    {
        try
        {
            // Best-effort: the WT_SESSION env var means Windows Terminal hosts us.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION")))
                return "Windows Terminal";
            var term = Environment.GetEnvironmentVariable("TERM_PROGRAM");
            if (!string.IsNullOrEmpty(term)) return term;
        }
        catch { /* ignore */ }
        return "Windows Console";
    }

    // ---- battery via GetSystemPowerStatus ----
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;       // 0 offline, 1 online, 255 unknown
        public byte BatteryFlag;        // 128 = no system battery
        public byte BatteryLifePercent; // 0–100, 255 unknown
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    private static string Battery(Func<string, string, string> pick)
    {
        try
        {
            if (!GetSystemPowerStatus(out var s) || (s.BatteryFlag & 128) != 0 || s.BatteryLifePercent == 255)
                return pick("(none)", "（無）");
            int pct = s.BatteryLifePercent;
            var state = (s.BatteryFlag & 8) != 0
                ? pick("Charging", "充電中")
                : s.ACLineStatus == 1
                    ? pick("Plugged in", "已插電")
                    : pick("Discharging", "放電中");
            return $"{pct}% ({state})";
        }
        catch { return pick("(none)", "（無）"); }
    }

    private static int BatteryPercent()
    {
        try
        {
            if (!GetSystemPowerStatus(out var s) || (s.BatteryFlag & 128) != 0 || s.BatteryLifePercent == 255)
                return -1;
            return s.BatteryLifePercent;
        }
        catch { return -1; }
    }

    /// <summary>
    /// 砌出完整快照 · Build the full snapshot off-thread.
    /// </summary>
    public static Task<FetchSnapshot> CollectAsync(Func<string, string, string> pick, CancellationToken ct = default)
        => Task.Run(async () =>
        {
            string Pk(string en, string zh) => pick(en, zh);

            var snap = new FetchSnapshot
            {
                User = SafeUser(),
                Host = SafeMachine(),
            };

            // OS
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "OS", TitleZh = "作業系統",
                Value = Try(() => $"{SystemInfo.OsProductName} ({SystemInfo.OsDisplayVersion}) [{SystemInfo.Architecture}]"),
            });

            // Host (manufacturer + model)
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Host", TitleZh = "主機",
                Value = Try(() =>
                {
                    var m = $"{Wmi("Win32_ComputerSystem", "Manufacturer")} {Wmi("Win32_ComputerSystem", "Model")}".Trim();
                    return string.IsNullOrWhiteSpace(m) ? "—" : m;
                }),
            });

            // Motherboard
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Motherboard", TitleZh = "主機板",
                Value = Try(() =>
                {
                    var m = $"{Wmi("Win32_BaseBoard", "Manufacturer")} {Wmi("Win32_BaseBoard", "Product")}".Trim();
                    return string.IsNullOrWhiteSpace(m) ? "—" : m;
                }),
            });

            // Kernel (NT version)
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Kernel", TitleZh = "核心",
                Value = Try(() => Environment.OSVersion.Version.ToString()),
            });

            // Uptime
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Uptime", TitleZh = "開機時間",
                Value = Try(() => SystemInfo.Uptime),
            });

            // Packages
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Packages", TitleZh = "套件",
                Value = await SafeAsync(() => PackageCounts(ct), "(none) · 無"),
            });

            // Shell
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Shell", TitleZh = "Shell",
                Value = Try(ShellVersion),
            });

            // Terminal
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Terminal", TitleZh = "終端機",
                Value = Try(Terminal),
            });

            // Resolution (per monitor)
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Resolution", TitleZh = "解像度",
                Value = Try(() =>
                {
                    var mons = Monitors();
                    return mons.Count == 0 ? "—" : string.Join(", ", mons);
                }),
            });

            // DE / WM + theme
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "DE/WM", TitleZh = "桌面環境",
                Value = "Fluent (DWM) · Windows Shell",
            });
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Theme", TitleZh = "佈景主題",
                Value = Try(() => ThemeDescription(pick)),
            });

            // Locale
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Locale", TitleZh = "地區語言",
                Value = Try(() => $"{CultureInfo.CurrentCulture.Name} ({CultureInfo.CurrentCulture.DisplayName})"),
            });

            // CPU (name + cores + clock)
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "CPU", TitleZh = "處理器",
                Value = Try(() =>
                {
                    var name = SystemInfo.CpuName;
                    if (name.Contains('@')) name = name.Split('@')[0].Trim();
                    var mhz = RegistryHelper.GetValue(RegRoot.HKLM,
                        @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz");
                    var ghz = mhz is int m ? $" @ {Math.Round(m / 1000.0, 2)}GHz" : "";
                    return $"{name} ({SystemInfo.LogicalProcessors} {Pk("threads", "執行緒")}){ghz}";
                }),
            });

            // GPU(s)
            foreach (var gpu in Try(() => WmiAll("Win32_VideoController", "Name"), new List<string>()))
            {
                snap.Rows.Add(new FetchRow { TitleEn = "GPU", TitleZh = "顯示卡", Value = gpu });
            }
            if (snap.Rows.All(r => r.TitleEn != "GPU"))
            {
                snap.Rows.Add(new FetchRow
                {
                    TitleEn = "GPU", TitleZh = "顯示卡",
                    Value = Try(() => SystemInfo.GpuName),
                });
            }

            // Memory (used / total + percent)
            try
            {
                var pct = (int)Math.Round(SystemInfo.RamLoadPercent);
                snap.Rows.Add(new FetchRow
                {
                    TitleEn = "Memory", TitleZh = "記憶體",
                    Value = SystemInfo.RamUsage,
                    Percent = pct,
                });
            }
            catch
            {
                snap.Rows.Add(new FetchRow { TitleEn = "Memory", TitleZh = "記憶體", Value = "—" });
            }

            // Disks (per volume + percent)
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady || d.DriveType != DriveType.Fixed || d.TotalSize <= 0) continue;
                        double total = d.TotalSize / 1073741824.0;
                        double free = d.AvailableFreeSpace / 1073741824.0;
                        double used = total - free;
                        int pct = total > 0 ? (int)Math.Floor(used / total * 100) : 0;
                        snap.Rows.Add(new FetchRow
                        {
                            TitleEn = $"Disk ({d.Name.TrimEnd('\\')})",
                            TitleZh = $"磁碟 ({d.Name.TrimEnd('\\')})",
                            Value = $"{used:0.0} GiB / {total:0.0} GiB",
                            Percent = pct,
                        });
                    }
                    catch { /* skip one drive */ }
                }
            }
            catch { /* skip disks */ }

            // Battery
            var batPct = BatteryPercent();
            snap.Rows.Add(new FetchRow
            {
                TitleEn = "Battery", TitleZh = "電池",
                Value = Try(() => Battery(pick)),
                Percent = batPct,
            });

            return snap;
        }, ct);

    private static string Try(Func<string> f)
    {
        try { var v = f(); return string.IsNullOrWhiteSpace(v) ? "—" : v; }
        catch { return "—"; }
    }

    private static T Try<T>(Func<T> f, T fallback)
    {
        try { return f(); }
        catch { return fallback; }
    }

    private static async Task<string> SafeAsync(Func<Task<string>> f, string fallback)
    {
        try { var v = await f(); return string.IsNullOrWhiteSpace(v) ? fallback : v; }
        catch { return fallback; }
    }

    private static string SafeUser() { try { return Environment.UserName; } catch { return "user"; } }
    private static string SafeMachine() { try { return Environment.MachineName; } catch { return "host"; } }

    /// <summary>
    /// 將快照渲染成 winfetch 風格嘅純文字（連 ASCII Windows 標誌）· Render the snapshot as winfetch-style
    /// monospace text with the ASCII Windows 11 logo, suitable for copy/paste or export.
    /// </summary>
    public static string ToAsciiText(FetchSnapshot snap, bool english)
    {
        // ASCII Windows 11 logo (four blocks), matching upstream's shape.
        string[] logo =
        {
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "                             ",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
            "lllllllllllll   lllllllllllll",
        };

        var info = new List<string> { $"{snap.User}@{snap.Host}", new string('-', snap.User.Length + snap.Host.Length + 1) };
        foreach (var r in snap.Rows)
        {
            var title = english ? r.TitleEn : (string.IsNullOrEmpty(r.TitleZh) ? r.TitleEn : r.TitleZh);
            var line = string.IsNullOrEmpty(title) ? r.Value : $"{title}: {r.Value}";
            if (r.Percent >= 0) line += $"  {Bar(r.Percent)}";
            info.Add(line);
        }

        var sb = new StringBuilder();
        int rows = Math.Max(logo.Length, info.Count);
        const int logoWidth = 31;
        for (int i = 0; i < rows; i++)
        {
            var l = i < logo.Length ? logo[i] : "";
            var n = i < info.Count ? info[i] : "";
            sb.Append(l.PadRight(logoWidth));
            sb.Append("  ");
            sb.AppendLine(n);
        }
        return sb.ToString();
    }

    /// <summary>純文字（無標誌）· Plain key/value text, no logo — for the simple "Copy" button.</summary>
    public static string ToPlainText(FetchSnapshot snap, bool english)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{snap.User}@{snap.Host}");
        foreach (var r in snap.Rows)
        {
            var title = english ? r.TitleEn : (string.IsNullOrEmpty(r.TitleZh) ? r.TitleEn : r.TitleZh);
            var line = string.IsNullOrEmpty(title) ? r.Value : $"{title}: {r.Value}";
            if (r.Percent >= 0) line += $"  {Bar(r.Percent)}";
            sb.AppendLine(line);
        }
        return sb.ToString();
    }

    private static string Bar(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        int filled = (int)Math.Round(percent / 10.0);
        var sb = new StringBuilder("[ ");
        for (int i = 0; i < filled; i++) sb.Append('■');
        for (int i = 0; i < 10 - filled; i++) sb.Append('-');
        sb.Append($" ] {percent}%");
        return sb.ToString();
    }
}
