using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一條已安裝嘅 WireGuard 隧道 · One installed WireGuard tunnel.</summary>
public sealed class WgTunnel
{
    public string Name { get; set; } = "";
    public bool Active { get; set; }
}

/// <summary>
/// 應用程式內 WireGuard 控制（包官方 wireguard.exe）· In-app WireGuard control wrapping the official
/// wireguard.exe. Import a .conf as a tunnel service (/installtunnelservice), start/stop, and list/remove
/// tunnels. No redirect. Install the WireGuard engine via the Package Manager.
/// </summary>
public static class WireGuardService
{
    private static readonly string[] Paths =
    {
        @"C:\Program Files\WireGuard\wireguard.exe",
    };

    public static string? Exe => Paths.FirstOrDefault(File.Exists);
    public static bool Installed => Exe is not null;

    /// <summary>已安裝隧道嘅 data 目錄 · Where wireguard.exe keeps installed tunnel configs.</summary>
    private static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "WireGuard", "Configurations");

    /// <summary>匯入一個 .conf 做隧道服務（需要管理員）· Import a .conf file as a tunnel service (needs admin).</summary>
    public static Task<TweakResult> ImportConfig(string confPath, CancellationToken ct = default)
    {
        var exe = Exe;
        if (exe is null) return Task.FromResult(TweakResult.Fail("WireGuard not found.", "搵唔到 WireGuard。"));
        if (!File.Exists(confPath)) return Task.FromResult(TweakResult.Fail("Config file not found.", "搵唔到設定檔。"));
        return ShellRunner.Run(exe, $"/installtunnelservice \"{confPath}\"", elevated: true, ct);
    }

    /// <summary>移除（停用）一條隧道服務 · Uninstall (stop) a tunnel service by name. Needs admin.</summary>
    public static Task<TweakResult> RemoveTunnel(string name, CancellationToken ct = default)
    {
        var exe = Exe;
        return exe is null
            ? Task.FromResult(TweakResult.Fail("WireGuard not found.", "搵唔到 WireGuard。"))
            : ShellRunner.Run(exe, $"/uninstalltunnelservice \"{name}\"", elevated: true, ct);
    }

    /// <summary>已匯入嘅隧道（連即時 up 狀態）· Imported tunnels with live up/down status.</summary>
    public static async Task<List<WgTunnel>> Tunnels(CancellationToken ct = default)
    {
        var list = new List<WgTunnel>();
        var names = new List<string>();
        try
        {
            if (Directory.Exists(DataDir))
                foreach (var f in Directory.GetFiles(DataDir, "*.conf"))
                    names.Add(Path.GetFileNameWithoutExtension(f));
        }
        catch { }
        if (names.Count == 0) return list;

        // A WireGuard tunnel runs as a Windows service named "WireGuardTunnel$<name>".
        string statusJson;
        try
        {
            statusJson = await ShellRunner.CapturePowershellJson(
                "Get-Service -Name 'WireGuardTunnel$*' -ErrorAction SilentlyContinue | " +
                "Select-Object Name,Status | ConvertTo-Json -Compress", ct);
        }
        catch { statusJson = "[]"; }

        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(statusJson);
            void Read(System.Text.Json.JsonElement e)
            {
                string svc = e.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                // Status: 4 = Running (enum int) or string "Running".
                bool up = e.TryGetProperty("Status", out var s) &&
                          ((s.ValueKind == System.Text.Json.JsonValueKind.Number && s.GetInt32() == 4) ||
                           (s.ValueKind == System.Text.Json.JsonValueKind.String &&
                            string.Equals(s.GetString(), "Running", StringComparison.OrdinalIgnoreCase)));
                int dollar = svc.IndexOf('$');
                if (up && dollar >= 0 && dollar + 1 < svc.Length) running.Add(svc[(dollar + 1)..]);
            }
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                foreach (var e in doc.RootElement.EnumerateArray()) Read(e);
            else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                Read(doc.RootElement);
        }
        catch { }

        foreach (var name in names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            list.Add(new WgTunnel { Name = name, Active = running.Contains(name) });
        return list;
    }
}
