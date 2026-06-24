using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 應用程式內 NordVPN 控制（包 NordVPN.exe CLI）· In-app NordVPN control wrapping the Windows NordVPN CLI
/// (C:\Program Files\NordVPN\NordVPN.exe -c / -d / -n / -g). No redirect. Install via the Package Manager.
/// </summary>
public static class NordVpnService
{
    private static readonly string[] Paths =
    {
        @"C:\Program Files\NordVPN\NordVPN.exe",
        @"C:\Program Files (x86)\NordVPN\NordVPN.exe",
    };

    public static string? Exe => Paths.FirstOrDefault(File.Exists);
    public static bool Installed => Exe is not null;

    /// <summary>Quick-connect, or connect to a country/server by name when given.</summary>
    public static Task<TweakResult> Connect(string? name, CancellationToken ct = default)
        => Run(string.IsNullOrWhiteSpace(name) ? "-c" : $"-c -n \"{name}\"", ct);

    /// <summary>Connect to the best server in a server group (e.g. P2P, Double_VPN).</summary>
    public static Task<TweakResult> ConnectGroup(string group, CancellationToken ct = default)
        => Run($"-c -g {group}", ct);

    public static Task<TweakResult> Disconnect(CancellationToken ct = default) => Run("-d", ct);

    // ---- Meshnet · 網狀網 ----
    // The Windows NordVPN.exe exposes Meshnet via the "set meshnet" / "meshnet" verbs.

    /// <summary>開／熄 Meshnet · Enable or disable Meshnet.</summary>
    public static Task<TweakResult> SetMeshnet(bool on, CancellationToken ct = default)
        => Run($"set meshnet {(on ? "on" : "off")}", ct);

    /// <summary>列出 Meshnet 同伴裝置（原始文字）· List Meshnet peer devices (raw text).</summary>
    public static Task<TweakResult> MeshnetPeerList(CancellationToken ct = default)
        => Run("meshnet peer list", ct);

    /// <summary>容許／拒絕一部 Meshnet 機嘅內送流量 · Allow or deny incoming traffic from a Meshnet peer.</summary>
    public static Task<TweakResult> MeshnetSetIncoming(string peer, bool allow, CancellationToken ct = default)
        => Run($"meshnet peer incoming {(allow ? "allow" : "deny")} \"{peer}\"", ct);

    private static Task<TweakResult> Run(string args, CancellationToken ct)
    {
        var exe = Exe;
        return exe is null
            ? Task.FromResult(TweakResult.Fail("NordVPN not found.", "搵唔到 NordVPN。"))
            : ShellRunner.Run(exe, args, false, ct);
    }

    public static readonly (string en, string zh)[] Countries =
    {
        ("(Quick connect)", "（快速連接）"),
        ("United States", "美國"), ("United Kingdom", "英國"), ("Canada", "加拿大"),
        ("Germany", "德國"), ("Japan", "日本"), ("Australia", "澳洲"),
        ("Netherlands", "荷蘭"), ("France", "法國"), ("Singapore", "新加坡"),
        ("Switzerland", "瑞士"), ("Hong Kong", "香港"), ("Taiwan", "台灣"),
    };

    public static readonly string[] Groups =
    {
        "P2P", "Double_VPN", "Onion_Over_VPN", "Dedicated_IP", "Obfuscated_Servers",
    };
}
