using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個 Windows 內置 VPN 設定檔 · One built-in Windows VPN profile.</summary>
public sealed class WinVpnProfile
{
    public string Name { get; set; } = "";
    public string ServerAddress { get; set; } = "";
    public string TunnelType { get; set; } = "";
    public string ConnectionStatus { get; set; } = "";
    public bool Connected => string.Equals(ConnectionStatus, "Connected", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 應用程式內 Windows 內置 VPN 管理 · In-app manager for the built-in Windows VPN client.
/// Wraps the PowerShell VPN cmdlets (Get/Add/Remove-VpnConnection) for IKEv2 / L2TP / SSTP / PPTP /
/// "Automatic" profiles, plus rasdial.exe for connect/disconnect. No redirect to the Settings app.
/// </summary>
public static class WindowsVpnService
{
    /// <summary>支援嘅通道類型 · Supported tunnel types for Add-VpnConnection.</summary>
    public static readonly (string en, string zh, string value)[] TunnelTypes =
    {
        ("Automatic", "自動", "Automatic"),
        ("IKEv2", "IKEv2", "Ikev2"),
        ("L2TP/IPsec", "L2TP/IPsec", "L2tp"),
        ("SSTP", "SSTP", "Sstp"),
        ("PPTP", "PPTP", "Pptp"),
    };

    /// <summary>列出所有使用者 VPN 設定檔（連即時連線狀態）· List all user VPN profiles with live status.</summary>
    public static async Task<List<WinVpnProfile>> List(CancellationToken ct = default)
    {
        var list = new List<WinVpnProfile>();
        const string script =
            "Get-VpnConnection -AllUserConnection -ErrorAction SilentlyContinue | " +
            "Select-Object Name,ServerAddress,TunnelType,ConnectionStatus | ConvertTo-Json -Compress; " +
            "Get-VpnConnection -ErrorAction SilentlyContinue | " +
            "Select-Object Name,ServerAddress,TunnelType,ConnectionStatus | ConvertTo-Json -Compress";
        string json;
        try { json = await ShellRunner.CapturePowershell(script, ct); }
        catch { return list; }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in SplitJsonChunks(json))
        {
            try
            {
                using var doc = JsonDocument.Parse(chunk);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    foreach (var e in doc.RootElement.EnumerateArray()) Add(list, seen, e);
                else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    Add(list, seen, doc.RootElement);
            }
            catch { }
        }
        return list;
    }

    private static void Add(List<WinVpnProfile> list, HashSet<string> seen, JsonElement e)
    {
        string name = Str(e, "Name");
        if (name.Length == 0 || !seen.Add(name)) return;
        list.Add(new WinVpnProfile
        {
            Name = name,
            ServerAddress = Str(e, "ServerAddress"),
            TunnelType = Str(e, "TunnelType"),
            ConnectionStatus = Str(e, "ConnectionStatus"),
        });
    }

    private static string Str(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>ConvertTo-Json -Compress 會接連輸出兩個 JSON，喺度逐個拆開 · Split back-to-back JSON values.</summary>
    private static IEnumerable<string> SplitJsonChunks(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) yield break;
        raw = raw.Trim().TrimStart('﻿');
        int depth = 0, start = -1;
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '{' || c == '[')
            {
                if (depth == 0) start = i;
                depth++;
            }
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0 && start >= 0) { yield return raw.Substring(start, i - start + 1); start = -1; }
            }
        }
    }

    /// <summary>新增一個 VPN 設定檔 · Add a VPN profile via Add-VpnConnection.</summary>
    public static Task<TweakResult> Add(string name, string serverAddress, string tunnelType,
        bool rememberCredential = true, CancellationToken ct = default)
    {
        string n = Esc(name), s = Esc(serverAddress), t = Esc(tunnelType);
        // L2tp uses PSK-less default (machine cert) unless user configures; Add with EAP/MSCHAPv2 default auth.
        string remember = rememberCredential ? "$true" : "$false";
        string script =
            $"Add-VpnConnection -Name '{n}' -ServerAddress '{s}' -TunnelType '{t}' " +
            $"-AuthenticationMethod MSChapv2 -EncryptionLevel Optional -RememberCredential:{remember} " +
            "-Force -PassThru -ErrorAction Stop | Out-Null";
        return ShellRunner.RunPowershell(script, false, ct);
    }

    /// <summary>刪除一個 VPN 設定檔 · Remove a VPN profile.</summary>
    public static Task<TweakResult> Remove(string name, CancellationToken ct = default)
        => ShellRunner.RunPowershell($"Remove-VpnConnection -Name '{Esc(name)}' -Force -ErrorAction Stop", false, ct);

    /// <summary>連接（rasdial）· Connect to a profile using rasdial. Optional user/password.</summary>
    public static Task<TweakResult> Connect(string name, string? user = null, string? password = null,
        CancellationToken ct = default)
    {
        string args = $"\"{name}\"";
        if (!string.IsNullOrEmpty(user))
            args += $" \"{user}\" \"{password ?? ""}\"";
        return ShellRunner.Run("rasdial.exe", args, false, ct);
    }

    /// <summary>斷開（rasdial /disconnect）· Disconnect a profile using rasdial.</summary>
    public static Task<TweakResult> Disconnect(string name, CancellationToken ct = default)
        => ShellRunner.Run("rasdial.exe", $"\"{name}\" /disconnect", false, ct);

    /// <summary>rasdial 嘅即時連線清單（原始文字）· Raw rasdial connection list.</summary>
    public static Task<string> RasdialStatus(CancellationToken ct = default)
        => ShellRunner.Capture("rasdial.exe", "", ct);

    private static string Esc(string s) => (s ?? "").Replace("'", "''");
}
