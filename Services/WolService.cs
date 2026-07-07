using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 網絡喚醒 · Wake-on-LAN (WOL) — build & send the "magic packet" (6×0xFF + MAC×16 = 102 bytes)
/// over UDP broadcast so a sleeping/off machine's NIC powers it on. Pure managed (UdpClient).
/// Nothing here throws to the UI: parse/send return a bilingual-safe result the page can display.
/// </summary>
public static class WolService
{
    /// <summary>Outcome of a send attempt. <see cref="Ok"/> plus an English/中文 message pair.</summary>
    public readonly record struct Result(bool Ok, string En, string Zh);

    /// <summary>
    /// Parse a MAC in any common notation — AA:BB:CC:DD:EE:FF, AA-BB-…, AABB.CCDD.EEFF, or bare
    /// 12 hex digits — into 6 bytes. Returns false (no throw) on anything malformed.
    /// </summary>
    public static bool TryParseMac(string? input, out byte[] mac)
    {
        mac = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(input)) return false;

        // Strip the usual separators; whatever's left must be exactly 12 hex chars.
        var hex = new System.Text.StringBuilder(12);
        foreach (char c in input)
        {
            if (c is ':' or '-' or '.' or ' ' or '\t') continue;
            if (Uri.IsHexDigit(c)) hex.Append(c);
            else return false;
        }
        if (hex.Length != 12) return false;

        var bytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            if (!byte.TryParse(hex.ToString(i * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out bytes[i]))
                return false;
        }
        mac = bytes;
        return true;
    }

    /// <summary>Build the 102-byte magic packet for a 6-byte MAC.</summary>
    public static byte[] BuildMagicPacket(byte[] mac)
    {
        var packet = new byte[102];
        for (int i = 0; i < 6; i++) packet[i] = 0xFF;
        for (int rep = 0; rep < 16; rep++)
            Array.Copy(mac, 0, packet, 6 + rep * 6, 6);
        return packet;
    }

    /// <summary>
    /// Send a magic packet for <paramref name="macText"/> to <paramref name="broadcast"/>:<paramref name="port"/>.
    /// Sends 3 times for reliability. Never throws — socket/parse errors come back as a failed <see cref="Result"/>.
    /// </summary>
    public static async Task<Result> SendAsync(string? macText, string? broadcast, int port)
    {
        if (!TryParseMac(macText, out var mac))
            return new Result(false,
                "Invalid MAC address — use AA:BB:CC:DD:EE:FF, AA-BB-…, AABB.CCDD.EEFF or 12 hex digits.",
                "MAC 位址無效 — 請用 AA:BB:CC:DD:EE:FF、AA-BB-…、AABB.CCDD.EEFF 或者 12 個十六進位數字。");

        string host = string.IsNullOrWhiteSpace(broadcast) ? "255.255.255.255" : broadcast!.Trim();
        if (!IPAddress.TryParse(host, out var addr))
            return new Result(false,
                $"Invalid broadcast address “{host}”.",
                $"廣播位址「{host}」無效。");

        if (port is < 1 or > 65535) port = 9;

        try
        {
            var packet = BuildMagicPacket(mac);
            using var udp = new UdpClient { EnableBroadcast = true };
            var endpoint = new IPEndPoint(addr, port);
            for (int i = 0; i < 3; i++)
                await udp.SendAsync(packet, packet.Length, endpoint).ConfigureAwait(false);

            string macStr = Convert.ToHexString(mac);
            macStr = $"{macStr[0]}{macStr[1]}:{macStr[2]}{macStr[3]}:{macStr[4]}{macStr[5]}:{macStr[6]}{macStr[7]}:{macStr[8]}{macStr[9]}:{macStr[10]}{macStr[11]}";
            return new Result(true,
                $"Magic packet sent to {macStr} via {host}:{port} (×3).",
                $"魔術封包已送去 {macStr}，經 {host}:{port}（×3）。");
        }
        catch (SocketException ex)
        {
            return new Result(false,
                $"Network error: {ex.Message}",
                $"網絡錯誤：{ex.Message}");
        }
        catch (Exception ex)
        {
            return new Result(false,
                $"Could not send: {ex.Message}",
                $"無法傳送：{ex.Message}");
        }
    }
}
