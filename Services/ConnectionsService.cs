using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>一條連線 · One TCP/UDP socket row with its owning process.</summary>
public sealed class ConnRow
{
    public string Proto { get; init; } = "";
    public string Local { get; init; } = "";
    public string Remote { get; init; } = "";
    public string State { get; init; } = "";
    public int Pid { get; init; }
    public string Process { get; init; } = "";
    public bool CanKill { get; init; }

    // Raw network-order fields kept verbatim for SetTcpEntry.
    internal uint RawLocalAddr { get; init; }
    internal uint RawLocalPort { get; init; }
    internal uint RawRemoteAddr { get; init; }
    internal uint RawRemotePort { get; init; }

    public string Key => $"{Proto}|{Local}|{Remote}|{Pid}";
}

/// <summary>
/// 應用程式內連線檢視（TCPView 風格，包 iphlpapi）· In-app active-connections viewer built on
/// iphlpapi.dll: GetExtendedTcp/UdpTable enumeration + SetTcpEntry to drop one TCP socket.
/// Struct layouts &amp; byte-order adversarially verified vs MS Learn. No redirect.
/// </summary>
public static class ConnectionsService
{
    private const int AF_INET = 2;
    private const int TCP_TABLE_OWNER_PID_ALL = 5;
    private const int UDP_TABLE_OWNER_PID = 1;
    private const uint NO_ERROR = 0, ERROR_INSUFFICIENT_BUFFER = 122, MIB_TCP_STATE_DELETE_TCB = 12;

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID { public uint dwState, dwLocalAddr, dwLocalPort, dwRemoteAddr, dwRemotePort, dwOwningPid; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_UDPROW_OWNER_PID { public uint dwLocalAddr, dwLocalPort, dwOwningPid; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW { public uint dwState, dwLocalAddr, dwLocalPort, dwRemoteAddr, dwRemotePort; }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr p, ref int size, bool order, int af, int cls, int reserved);
    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(IntPtr p, ref int size, bool order, int af, int cls, int reserved);
    [DllImport("iphlpapi.dll")]
    private static extern uint SetTcpEntry(ref MIB_TCPROW row);

    private static IPAddress Addr(uint netOrder) => new(netOrder & 0xFFFFFFFFu);
    private static int Port(uint dword)
    {
        uint p = dword & 0xFFFFu;
        return (int)(((p & 0x00FFu) << 8) | ((p & 0xFF00u) >> 8));
    }

    private static string TcpStateName(uint s) => s switch
    {
        1 => "CLOSED", 2 => "LISTENING", 3 => "SYN_SENT", 4 => "SYN_RECEIVED", 5 => "ESTABLISHED",
        6 => "FIN_WAIT1", 7 => "FIN_WAIT2", 8 => "CLOSE_WAIT", 9 => "CLOSING", 10 => "LAST_ACK",
        11 => "TIME_WAIT", 12 => "DELETE_TCB", _ => $"UNKNOWN({s})",
    };

    /// <summary>One unified snapshot of all IPv4 TCP + UDP sockets with owning process names.</summary>
    public static List<ConnRow> Snapshot(bool tcp, bool udp)
    {
        var names = PidNames();
        var rows = new List<ConnRow>();
        if (tcp) AddTcp(rows, names);
        if (udp) AddUdp(rows, names);
        return rows;
    }

    private static Dictionary<int, string> PidNames()
    {
        var d = new Dictionary<int, string> { [0] = "System Idle", [4] = "System" };
        foreach (var p in Process.GetProcesses())
        {
            try { d[p.Id] = p.ProcessName; } catch { } finally { p.Dispose(); }
        }
        return d;
    }

    private static void AddTcp(List<ConnRow> rows, Dictionary<int, string> names)
    {
        int size = 0;
        uint ret = GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (ret != ERROR_INSUFFICIENT_BUFFER && ret != NO_ERROR) return;
        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buf, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) != NO_ERROR) return;
            int n = Marshal.ReadInt32(buf);
            IntPtr ptr = IntPtr.Add(buf, sizeof(uint));
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < n; i++)
            {
                var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(ptr);
                ptr = IntPtr.Add(ptr, rowSize);
                bool listen = r.dwState == 2;
                int pid = (int)r.dwOwningPid;
                rows.Add(new ConnRow
                {
                    Proto = "TCP",
                    Local = $"{Addr(r.dwLocalAddr)}:{Port(r.dwLocalPort)}",
                    Remote = listen ? "*:*" : $"{Addr(r.dwRemoteAddr)}:{Port(r.dwRemotePort)}",
                    State = TcpStateName(r.dwState),
                    Pid = pid,
                    Process = names.TryGetValue(pid, out var nm) ? nm : "?",
                    CanKill = r.dwState == 5 || r.dwState == 8, // ESTABLISHED / CLOSE_WAIT
                    RawLocalAddr = r.dwLocalAddr,
                    RawLocalPort = r.dwLocalPort,
                    RawRemoteAddr = r.dwRemoteAddr,
                    RawRemotePort = r.dwRemotePort,
                });
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static void AddUdp(List<ConnRow> rows, Dictionary<int, string> names)
    {
        int size = 0;
        uint ret = GetExtendedUdpTable(IntPtr.Zero, ref size, true, AF_INET, UDP_TABLE_OWNER_PID, 0);
        if (ret != ERROR_INSUFFICIENT_BUFFER && ret != NO_ERROR) return;
        IntPtr buf = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedUdpTable(buf, ref size, true, AF_INET, UDP_TABLE_OWNER_PID, 0) != NO_ERROR) return;
            int n = Marshal.ReadInt32(buf);
            IntPtr ptr = IntPtr.Add(buf, sizeof(uint));
            int rowSize = Marshal.SizeOf<MIB_UDPROW_OWNER_PID>();
            for (int i = 0; i < n; i++)
            {
                var r = Marshal.PtrToStructure<MIB_UDPROW_OWNER_PID>(ptr);
                ptr = IntPtr.Add(ptr, rowSize);
                int pid = (int)r.dwOwningPid;
                rows.Add(new ConnRow
                {
                    Proto = "UDP",
                    Local = $"{Addr(r.dwLocalAddr)}:{Port(r.dwLocalPort)}",
                    Remote = "*:*",
                    State = "",
                    Pid = pid,
                    Process = names.TryGetValue(pid, out var nm) ? nm : "?",
                    CanKill = false,
                });
            }
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    /// <summary>Drop one TCP connection (DELETE_TCB). Returns 0 on success; non-zero needs admin
    /// (317/5 = access denied). Raw fields copied verbatim — do NOT byte-swap.</summary>
    public static uint KillTcp(ConnRow c)
    {
        if (c is null || c.Proto != "TCP") return 87;
        var row = new MIB_TCPROW
        {
            dwState = MIB_TCP_STATE_DELETE_TCB,
            dwLocalAddr = c.RawLocalAddr,
            dwLocalPort = c.RawLocalPort,
            dwRemoteAddr = c.RawRemoteAddr,
            dwRemotePort = c.RawRemotePort,
        };
        try { return SetTcpEntry(ref row); } catch { return 1; }
    }
}
