using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace WinForge.Services;

// ============================================================================
//  Native utilities (System32 DLL P/Invoke) · 原生工具（System32 DLL P/Invoke）
//  One service, several independent feature groups, each a faithful wrapper over
//  a documented Win32 API. Struct layouts / marshaling adversarially verified
//  vs MS Learn, mirroring how ConnectionsService wraps iphlpapi. No redirect.
//  每一組都係一個對有文件記載 Win32 API 嘅忠實包裝；結構排版／封送已對照 MS Learn。
// ============================================================================

#region Wi-Fi (wlanapi) — saved passwords + nearby scan · Wi-Fi（已儲存密碼 + 附近掃描）

/// <summary>一個已儲存 Wi-Fi 設定檔 · One saved Wi-Fi profile with its plaintext key (if any).</summary>
public sealed class WifiProfile
{
    public string Name { get; init; } = "";
    public string Authentication { get; init; } = "";
    public string Encryption { get; init; } = "";
    public string Key { get; init; } = "";          // plaintext key material, "" if open / none
    public bool HasKey => Key.Length > 0;
}

/// <summary>一個附近嘅 Wi-Fi 網絡 · One nearby (visible) Wi-Fi network.</summary>
public sealed class WifiNetwork
{
    public string Ssid { get; init; } = "";
    public int SignalQuality { get; init; }          // 0..100
    public string Auth { get; init; } = "";
    public string Cipher { get; init; } = "";
    public bool HasProfile { get; init; }
    public bool Connectable { get; init; }
}

/// <summary>
/// 包 wlanapi.dll · Wraps wlanapi.dll: WlanOpenHandle / WlanEnumInterfaces /
/// WlanGetProfileList / WlanGetProfile (WLAN_PROFILE_GET_PLAINTEXT_KEY) for saved passwords,
/// and WlanScan + WlanGetAvailableNetworkList for the nearby scanner. WlanDeleteProfile removes one.
/// </summary>
public static class WifiService
{
    private const uint WLAN_PROFILE_GET_PLAINTEXT_KEY = 0x00000004;
    private const uint ERROR_SUCCESS = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strInterfaceDescription;
        public uint isState;
    }

    // dot11Ssid is a length-prefixed byte array (max 32). uSSIDLength then the bytes.
    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    // WLAN_AVAILABLE_NETWORK — only the fields up to wlanSignalQuality + security flags are read.
    // Layout faithfully reproduced from MS Learn (wlanapi.h).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strProfileName;
        public DOT11_SSID dot11Ssid;
        public uint dot11BssType;
        public uint uNumberOfBssids;
        [MarshalAs(UnmanagedType.Bool)] public bool bNetworkConnectable;
        public uint wlanNotConnectableReason;
        public uint uNumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public uint[] dot11PhyTypes;
        [MarshalAs(UnmanagedType.Bool)] public bool bMorePhyTypes;
        public uint wlanSignalQuality;               // 0..100
        [MarshalAs(UnmanagedType.Bool)] public bool bSecurityEnabled;
        public uint dot11DefaultAuthAlgorithm;
        public uint dot11DefaultCipherAlgorithm;
        public uint dwFlags;
        public uint dwReserved;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint dwClientVersion, IntPtr pReserved, out uint pdwNegotiatedVersion, out IntPtr phClientHandle);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr hClientHandle, IntPtr pReserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr hClientHandle, IntPtr pReserved, out IntPtr ppInterfaceList);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetProfileList(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pReserved, out IntPtr ppProfileList);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanGetProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved,
        out IntPtr pstrProfileXml, ref uint pdwFlags, out uint pdwGrantedAccess);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanDeleteProfile(IntPtr hClientHandle, ref Guid pInterfaceGuid, string strProfileName, IntPtr pReserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr hClientHandle, ref Guid pInterfaceGuid, IntPtr pDot11Ssid, IntPtr pIeData, IntPtr pReserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(IntPtr hClientHandle, ref Guid pInterfaceGuid, uint dwFlags, IntPtr pReserved, out IntPtr ppAvailableNetworkList);
    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr pMemory);

    private static bool TryOpen(out IntPtr handle, out Guid iface)
    {
        handle = IntPtr.Zero; iface = Guid.Empty;
        // Client version 2 = Vista+; negotiated version returned but unused.
        if (WlanOpenHandle(2, IntPtr.Zero, out _, out handle) != ERROR_SUCCESS) return false;
        if (WlanEnumInterfaces(handle, IntPtr.Zero, out IntPtr list) != ERROR_SUCCESS || list == IntPtr.Zero)
        {
            WlanCloseHandle(handle, IntPtr.Zero); handle = IntPtr.Zero; return false;
        }
        try
        {
            // WLAN_INTERFACE_INFO_LIST { DWORD dwNumberOfItems; DWORD dwIndex; WLAN_INTERFACE_INFO[]; }
            int count = Marshal.ReadInt32(list);
            if (count <= 0) return false;
            IntPtr first = IntPtr.Add(list, 8);
            var info = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(first);
            iface = info.InterfaceGuid;
            return true;
        }
        finally { WlanFreeMemory(list); }
    }

    /// <summary>Every saved Wi-Fi profile with its plaintext key (where one exists).</summary>
    public static List<WifiProfile> SavedProfiles()
    {
        var result = new List<WifiProfile>();
        if (!TryOpen(out IntPtr h, out Guid iface)) return result;
        try
        {
            if (WlanGetProfileList(h, ref iface, IntPtr.Zero, out IntPtr listPtr) != ERROR_SUCCESS || listPtr == IntPtr.Zero)
                return result;
            try
            {
                // WLAN_PROFILE_INFO_LIST { DWORD dwNumberOfItems; DWORD dwIndex; WLAN_PROFILE_INFO[]; }
                // WLAN_PROFILE_INFO { WCHAR strProfileName[256]; DWORD dwFlags; }
                int count = Marshal.ReadInt32(listPtr);
                IntPtr ptr = IntPtr.Add(listPtr, 8);
                int infoSize = (256 * 2) + 4; // 256 WCHARs + DWORD
                for (int i = 0; i < count; i++)
                {
                    string name = Marshal.PtrToStringUni(ptr) ?? "";
                    ptr = IntPtr.Add(ptr, infoSize);
                    if (name.Length == 0) continue;
                    result.Add(ReadProfile(h, ref iface, name));
                }
            }
            finally { WlanFreeMemory(listPtr); }
        }
        finally { WlanCloseHandle(h, IntPtr.Zero); }
        return result;
    }

    private static WifiProfile ReadProfile(IntPtr h, ref Guid iface, string name)
    {
        uint flags = WLAN_PROFILE_GET_PLAINTEXT_KEY;
        string xml = "";
        if (WlanGetProfile(h, ref iface, name, IntPtr.Zero, out IntPtr xmlPtr, ref flags, out _) == ERROR_SUCCESS && xmlPtr != IntPtr.Zero)
        {
            xml = Marshal.PtrToStringUni(xmlPtr) ?? "";
            WlanFreeMemory(xmlPtr);
        }
        return new WifiProfile
        {
            Name = name,
            Authentication = Between(xml, "<authentication>", "</authentication>"),
            Encryption = Between(xml, "<encryption>", "</encryption>"),
            Key = Between(xml, "<keyMaterial>", "</keyMaterial>"),
        };
    }

    /// <summary>Delete a saved profile by name. Returns true on success.</summary>
    public static bool DeleteProfile(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!TryOpen(out IntPtr h, out Guid iface)) return false;
        try { return WlanDeleteProfile(h, ref iface, name, IntPtr.Zero) == ERROR_SUCCESS; }
        finally { WlanCloseHandle(h, IntPtr.Zero); }
    }

    /// <summary>Trigger a scan, then enumerate every visible network on the primary adapter.</summary>
    public static List<WifiNetwork> ScanNearby()
    {
        var result = new List<WifiNetwork>();
        if (!TryOpen(out IntPtr h, out Guid iface)) return result;
        try
        {
            WlanScan(h, ref iface, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero); // async; results may be slightly stale
            if (WlanGetAvailableNetworkList(h, ref iface, 0, IntPtr.Zero, out IntPtr listPtr) != ERROR_SUCCESS || listPtr == IntPtr.Zero)
                return result;
            try
            {
                // WLAN_AVAILABLE_NETWORK_LIST { DWORD dwNumberOfItems; DWORD dwIndex; WLAN_AVAILABLE_NETWORK[]; }
                int count = Marshal.ReadInt32(listPtr);
                IntPtr ptr = IntPtr.Add(listPtr, 8);
                int rowSize = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
                var seen = new HashSet<string>();
                for (int i = 0; i < count; i++)
                {
                    var n = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(ptr);
                    ptr = IntPtr.Add(ptr, rowSize);
                    string ssid = SsidText(n.dot11Ssid);
                    string dedupe = $"{ssid}|{n.dot11DefaultAuthAlgorithm}";
                    if (ssid.Length == 0 || !seen.Add(dedupe)) continue;
                    result.Add(new WifiNetwork
                    {
                        Ssid = ssid,
                        SignalQuality = (int)n.wlanSignalQuality,
                        Auth = AuthName(n.dot11DefaultAuthAlgorithm, n.bSecurityEnabled),
                        Cipher = CipherName(n.dot11DefaultCipherAlgorithm),
                        HasProfile = n.strProfileName.Length > 0,
                        Connectable = n.bNetworkConnectable,
                    });
                }
            }
            finally { WlanFreeMemory(listPtr); }
        }
        finally { WlanCloseHandle(h, IntPtr.Zero); }
        return result;
    }

    private static string SsidText(DOT11_SSID s)
    {
        int len = (int)Math.Min(s.uSSIDLength, 32u);
        if (s.ucSSID is null || len <= 0) return "";
        return Encoding.UTF8.GetString(s.ucSSID, 0, len);
    }

    private static string AuthName(uint a, bool secured) => a switch
    {
        1 => "Open", 2 => "WEP (shared)", 3 => "WPA", 4 => "WPA-PSK", 5 => "WPA-None",
        6 => "WPA2", 7 => "WPA2-PSK", 10 => "WPA3", 11 => "WPA3-SAE", 12 => "OWE",
        _ => secured ? "Secured" : "Open",
    };

    private static string CipherName(uint c) => c switch
    {
        0x00 => "None", 0x01 => "WEP-40", 0x02 => "TKIP", 0x04 => "CCMP (AES)",
        0x05 => "WEP-104", 0x08 => "GCMP", 0x100 => "WEP", _ => $"0x{c:X}",
    };

    private static string Between(string s, string a, string b)
    {
        if (string.IsNullOrEmpty(s)) return "";
        int i = s.IndexOf(a, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "";
        i += a.Length;
        int j = s.IndexOf(b, i, StringComparison.OrdinalIgnoreCase);
        return j < 0 ? "" : s.Substring(i, j - i).Trim();
    }
}

#endregion

#region SMB shares + sessions (netapi32) · SMB 共享同工作階段

/// <summary>一個已發佈嘅 SMB 共享 · One published SMB share.</summary>
public sealed class SmbShare
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Type { get; init; } = "";
    public string Remark { get; init; } = "";
}

/// <summary>一個連入嘅 SMB 工作階段 · One inbound SMB session.</summary>
public sealed class SmbSession
{
    public string Computer { get; init; } = "";
    public string User { get; init; } = "";
    public uint OpenFiles { get; init; }
    public uint SecondsActive { get; init; }
    public uint SecondsIdle { get; init; }
}

/// <summary>
/// 包 netapi32.dll · Wraps netapi32.dll: NetShareEnum (level 2) for published shares and
/// NetSessionEnum (level 10) for inbound sessions — what you publish + who's connected.
/// </summary>
public static class SmbService
{
    private const uint NERR_Success = 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHARE_INFO_2
    {
        public string shi2_netname;
        public uint shi2_type;
        public string shi2_remark;
        public uint shi2_permissions;
        public uint shi2_max_uses;
        public uint shi2_current_uses;
        public string shi2_path;
        public string shi2_passwd;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SESSION_INFO_10
    {
        public string sesi10_cname;     // client name
        public string sesi10_username;
        public uint sesi10_time;        // seconds connected
        public uint sesi10_idle_time;   // seconds idle
    }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint NetShareEnum(string? servername, int level, out IntPtr bufptr, int prefmaxlen,
        out int entriesread, out int totalentries, ref int resume_handle);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern uint NetSessionEnum(string? servername, string? UncClientName, string? username, int level,
        out IntPtr bufptr, int prefmaxlen, out int entriesread, out int totalentries, ref int resume_handle);
    [DllImport("netapi32.dll")]
    private static extern uint NetApiBufferFree(IntPtr Buffer);

    private const int MAX_PREFERRED_LENGTH = -1;

    public static List<SmbShare> Shares()
    {
        var result = new List<SmbShare>();
        int resume = 0;
        uint r = NetShareEnum(null, 2, out IntPtr buf, MAX_PREFERRED_LENGTH, out int read, out _, ref resume);
        if (r != NERR_Success || buf == IntPtr.Zero) return result;
        try
        {
            int size = Marshal.SizeOf<SHARE_INFO_2>();
            IntPtr ptr = buf;
            for (int i = 0; i < read; i++)
            {
                var s = Marshal.PtrToStructure<SHARE_INFO_2>(ptr);
                ptr = IntPtr.Add(ptr, size);
                result.Add(new SmbShare
                {
                    Name = s.shi2_netname ?? "",
                    Path = s.shi2_path ?? "",
                    Remark = s.shi2_remark ?? "",
                    Type = ShareType(s.shi2_type),
                });
            }
        }
        finally { NetApiBufferFree(buf); }
        return result;
    }

    public static List<SmbSession> Sessions()
    {
        var result = new List<SmbSession>();
        int resume = 0;
        uint r = NetSessionEnum(null, null, null, 10, out IntPtr buf, MAX_PREFERRED_LENGTH, out int read, out _, ref resume);
        if (r != NERR_Success || buf == IntPtr.Zero) return result;
        try
        {
            int size = Marshal.SizeOf<SESSION_INFO_10>();
            IntPtr ptr = buf;
            for (int i = 0; i < read; i++)
            {
                var s = Marshal.PtrToStructure<SESSION_INFO_10>(ptr);
                ptr = IntPtr.Add(ptr, size);
                result.Add(new SmbSession
                {
                    Computer = s.sesi10_cname ?? "",
                    User = s.sesi10_username ?? "",
                    SecondsActive = s.sesi10_time,
                    SecondsIdle = s.sesi10_idle_time,
                });
            }
        }
        finally { NetApiBufferFree(buf); }
        return result;
    }

    private static string ShareType(uint t)
    {
        uint baseType = t & 0x0FFFFFFF;
        bool special = (t & 0x80000000) != 0;
        string b = baseType switch { 0 => "Disk", 1 => "Print queue", 2 => "Device", 3 => "IPC", _ => "?" };
        return special ? $"{b} (special)" : b;
    }
}

#endregion

#region Monitor brightness DDC/CI (dxva2) · 螢幕亮度

/// <summary>一個實體顯示器 · One physical monitor handle + its brightness range.</summary>
public sealed class MonitorBrightness
{
    public string Description { get; init; } = "";
    public uint Min { get; init; }
    public uint Current { get; set; }
    public uint Max { get; init; }
    internal IntPtr Handle { get; init; }
}

/// <summary>
/// 包 dxva2.dll + user32 · Wraps dxva2.dll (GetMonitorBrightness / SetMonitorBrightness /
/// GetPhysicalMonitorsFromHMONITOR) via user32 EnumDisplayMonitors — works on external DDC/CI monitors.
/// </summary>
public static class MonitorService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left, top, right, bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT lprc, IntPtr data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint min, out uint current, out uint max);
    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint brightness);
    [DllImport("dxva2.dll")]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    /// <summary>Enumerate every DDC/CI-capable monitor and read its brightness range.</summary>
    public static List<MonitorBrightness> Enumerate()
    {
        var result = new List<MonitorBrightness>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT rc, IntPtr data) =>
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0) return true;
            var arr = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMon, count, arr)) return true;
            foreach (var pm in arr)
            {
                if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint min, out uint cur, out uint max))
                {
                    result.Add(new MonitorBrightness
                    {
                        Description = string.IsNullOrWhiteSpace(pm.szPhysicalMonitorDescription) ? "Display" : pm.szPhysicalMonitorDescription,
                        Min = min, Current = cur, Max = max, Handle = pm.hPhysicalMonitor,
                    });
                }
                else
                {
                    // Not DDC/CI-capable (or internal laptop panel) — release the handle.
                    DestroyPhysicalMonitor(pm.hPhysicalMonitor);
                }
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    /// <summary>Set brightness (clamped to [Min,Max]). Returns true on success.</summary>
    public static bool SetBrightness(MonitorBrightness m, uint value)
    {
        if (m is null || m.Handle == IntPtr.Zero) return false;
        if (value < m.Min) value = m.Min;
        if (value > m.Max) value = m.Max;
        bool ok = SetMonitorBrightness(m.Handle, value);
        if (ok) m.Current = value;
        return ok;
    }

    /// <summary>Release every handle returned by a prior Enumerate() call.</summary>
    public static void Release(IEnumerable<MonitorBrightness> monitors)
    {
        foreach (var m in monitors)
            if (m.Handle != IntPtr.Zero) DestroyPhysicalMonitor(m.Handle);
    }
}

#endregion

#region User sessions (wtsapi32) · 登出／中斷其他使用者

/// <summary>一個 Windows 工作階段 · One Windows terminal-services session.</summary>
public sealed class UserSession
{
    public uint SessionId { get; init; }
    public string User { get; init; } = "";
    public string Station { get; init; } = "";
    public string State { get; init; } = "";
    public bool IsCurrent { get; init; }
}

/// <summary>
/// 包 wtsapi32.dll · Wraps wtsapi32.dll: WTSEnumerateSessions + WTSQuerySessionInformation to list
/// logged-on users; WTSLogoffSession / WTSDisconnectSession to log off or disconnect one (admin).
/// </summary>
public static class SessionService
{
    private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

    private enum WTS_INFO_CLASS { WTSUserName = 5, WTSWinStationName = 6 }

    [StructLayout(LayoutKind.Sequential)]
    private struct WTS_SESSION_INFO
    {
        public uint SessionId;
        public IntPtr pWinStationName; // LPTSTR
        public uint State;             // WTS_CONNECTSTATE_CLASS
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(IntPtr hServer, uint Reserved, uint Version, out IntPtr ppSessionInfo, out uint pCount);
    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool WTSQuerySessionInformation(IntPtr hServer, uint sessionId, WTS_INFO_CLASS infoClass, out IntPtr ppBuffer, out uint pBytesReturned);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSLogoffSession(IntPtr hServer, uint SessionId, bool bWait);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSDisconnectSession(IntPtr hServer, uint SessionId, bool bWait);
    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr pMemory);
    [DllImport("kernel32.dll")]
    private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);

    public static List<UserSession> Enumerate()
    {
        var result = new List<UserSession>();
        uint currentSession = 0;
        try { ProcessIdToSessionId((uint)Environment.ProcessId, out currentSession); } catch { }

        if (!WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, out IntPtr buf, out uint count) || buf == IntPtr.Zero)
            return result;
        try
        {
            int size = Marshal.SizeOf<WTS_SESSION_INFO>();
            IntPtr ptr = buf;
            for (int i = 0; i < count; i++)
            {
                var si = Marshal.PtrToStructure<WTS_SESSION_INFO>(ptr);
                ptr = IntPtr.Add(ptr, size);
                string user = QueryString(si.SessionId, WTS_INFO_CLASS.WTSUserName);
                string station = QueryString(si.SessionId, WTS_INFO_CLASS.WTSWinStationName);
                // Skip the listening / services / unnamed sessions with no user attached.
                if (string.IsNullOrWhiteSpace(user) && (station == "Services" || station.StartsWith("RDP-Tcp") || station.Length == 0))
                    continue;
                result.Add(new UserSession
                {
                    SessionId = si.SessionId,
                    User = string.IsNullOrWhiteSpace(user) ? "(none)" : user,
                    Station = station,
                    State = StateName(si.State),
                    IsCurrent = si.SessionId == currentSession,
                });
            }
        }
        finally { WTSFreeMemory(buf); }
        return result;
    }

    private static string QueryString(uint session, WTS_INFO_CLASS cls)
    {
        if (!WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, session, cls, out IntPtr p, out _) || p == IntPtr.Zero)
            return "";
        try { return Marshal.PtrToStringUni(p) ?? ""; }
        finally { WTSFreeMemory(p); }
    }

    private static string StateName(uint s) => s switch
    {
        0 => "Active", 1 => "Connected", 2 => "ConnectQuery", 3 => "Shadow", 4 => "Disconnected",
        5 => "Idle", 6 => "Listen", 7 => "Reset", 8 => "Down", 9 => "Init", _ => $"({s})",
    };

    /// <summary>Log off a session (admin). Returns true on success.</summary>
    public static bool Logoff(uint sessionId) => WTSLogoffSession(WTS_CURRENT_SERVER_HANDLE, sessionId, false);

    /// <summary>Disconnect a session (admin). Returns true on success.</summary>
    public static bool Disconnect(uint sessionId) => WTSDisconnectSession(WTS_CURRENT_SERVER_HANDLE, sessionId, false);
}

#endregion

#region Certificate viewer (crypt32) · 憑證檢視

/// <summary>一張憑證 · One certificate from a system store.</summary>
public sealed class CertInfo
{
    public string Subject { get; init; } = "";
    public string Issuer { get; init; } = "";
    public string Thumbprint { get; init; } = "";
    public DateTime NotBefore { get; init; }
    public DateTime NotAfter { get; init; }
    public string Store { get; init; } = "";
    public bool Expired => DateTime.Now > NotAfter;
}

/// <summary>
/// 包 crypt32.dll · Wraps crypt32.dll: CertOpenSystemStore + CertEnumCertificatesInStore over the
/// My / Root / CA stores. Each CERT_CONTEXT is wrapped in a managed X509Certificate2 for parsing.
/// </summary>
public static class CertificateService
{
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CertOpenSystemStore(IntPtr hProv, string szSubsystemProtocol);
    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern IntPtr CertEnumCertificatesInStore(IntPtr hCertStore, IntPtr pPrevCertContext);
    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern bool CertCloseStore(IntPtr hCertStore, uint dwFlags);

    public static List<CertInfo> Enumerate(string storeName)
    {
        var result = new List<CertInfo>();
        IntPtr store = CertOpenSystemStore(IntPtr.Zero, storeName);
        if (store == IntPtr.Zero) return result;
        try
        {
            IntPtr ctx = IntPtr.Zero;
            while ((ctx = CertEnumCertificatesInStore(store, ctx)) != IntPtr.Zero)
            {
                try
                {
                    using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(ctx);
                    result.Add(new CertInfo
                    {
                        Subject = ShortName(cert.Subject),
                        Issuer = ShortName(cert.Issuer),
                        Thumbprint = cert.Thumbprint ?? "",
                        NotBefore = cert.NotBefore,
                        NotAfter = cert.NotAfter,
                        Store = storeName,
                    });
                }
                catch { /* skip a cert we can't parse */ }
            }
        }
        finally { CertCloseStore(store, 0); }
        return result;
    }

    private static string ShortName(string dn)
    {
        if (string.IsNullOrEmpty(dn)) return "";
        foreach (var part in dn.Split(','))
        {
            var p = part.Trim();
            if (p.StartsWith("CN=", StringComparison.OrdinalIgnoreCase)) return p.Substring(3).Trim();
        }
        return dn;
    }
}

#endregion

#region Per-disk / GPU live counters (pdh) · 每磁碟／GPU 即時計數

/// <summary>一個即時效能計數器讀數 · One live PDH counter reading.</summary>
public sealed class CounterSample
{
    public string Label { get; init; } = "";
    public double Value { get; set; }
    public string Unit { get; init; } = "";
}

/// <summary>
/// 包 pdh.dll · Wraps pdh.dll: PdhOpenQuery / PdhAddEnglishCounter / PdhCollectQueryData /
/// PdhGetFormattedCounterValue — live disk %-busy and GPU engine % (locale-independent counter paths).
/// </summary>
public sealed class PdhCounters : IDisposable
{
    private const uint PDH_FMT_DOUBLE = 0x00000200;
    private const uint PDH_CSTATUS_VALID_DATA = 0;
    private const uint PDH_CSTATUS_NEW_DATA = 1;

    [StructLayout(LayoutKind.Explicit)]
    private struct PDH_FMT_COUNTERVALUE
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double doubleValue;
    }

    [DllImport("pdh.dll")]
    private static extern uint PdhOpenQuery(string? szDataSource, IntPtr dwUserData, out IntPtr phQuery);
    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);
    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr hQuery);
    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, out uint lpdwType, out PDH_FMT_COUNTERVALUE pValue);
    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr hQuery);

    private IntPtr _query;
    private readonly List<(IntPtr handle, string label, string unit)> _counters = new();

    /// <summary>Open a query for total disk %-busy and total GPU engine utilisation.</summary>
    public bool Open()
    {
        if (PdhOpenQuery(null, IntPtr.Zero, out _query) != 0) return false;
        // English (locale-independent) counter paths. _Total instance aggregates all disks/engines.
        Add(@"\PhysicalDisk(_Total)\% Disk Time", "Disk busy", "%");
        Add(@"\PhysicalDisk(_Total)\Disk Read Bytes/sec", "Disk read", "B/s");
        Add(@"\PhysicalDisk(_Total)\Disk Write Bytes/sec", "Disk write", "B/s");
        Add(@"\GPU Engine(*)\Utilization Percentage", "GPU", "%");
        return _counters.Count > 0;
    }

    private void Add(string path, string label, string unit)
    {
        if (PdhAddEnglishCounter(_query, path, IntPtr.Zero, out IntPtr h) == 0)
            _counters.Add((h, label, unit));
    }

    /// <summary>Collect one sample. PDH needs two collections spaced in time for rate counters,
    /// so call this once, wait, then call Read().</summary>
    public void Collect() { if (_query != IntPtr.Zero) PdhCollectQueryData(_query); }

    public List<CounterSample> Read()
    {
        var result = new List<CounterSample>();
        foreach (var (handle, label, unit) in _counters)
        {
            if (PdhGetFormattedCounterValue(handle, PDH_FMT_DOUBLE, out _, out var val) == 0 &&
                (val.CStatus == PDH_CSTATUS_VALID_DATA || val.CStatus == PDH_CSTATUS_NEW_DATA))
            {
                result.Add(new CounterSample { Label = label, Value = val.doubleValue, Unit = unit });
            }
        }
        return result;
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero) { PdhCloseQuery(_query); _query = IntPtr.Zero; }
    }
}

#endregion

#region Process module list (psapi) · 程序模組清單

/// <summary>一個程序載入嘅模組 · One module (DLL/EXE) loaded by a process.</summary>
public sealed class ProcModule
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public long SizeBytes { get; init; }
}

/// <summary>
/// 包 psapi.dll · Wraps psapi.dll: EnumProcessModulesEx + GetModuleFileNameEx + GetModuleInformation —
/// the loaded-DLL view for one PID (Process Explorer-style). Falls back to managed Modules for self.
/// </summary>
public static class ProcessModuleService
{
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint LIST_MODULES_ALL = 0x03;

    [StructLayout(LayoutKind.Sequential)]
    private struct MODULEINFO { public IntPtr lpBaseOfDll; public uint SizeOfImage; public IntPtr EntryPoint; }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);
    [DllImport("psapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileNameExW(IntPtr hProcess, IntPtr hModule, StringBuilder lpFilename, uint nSize);
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

    public static List<ProcModule> Modules(int pid)
    {
        var result = new List<ProcModule>();
        IntPtr h = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)pid);
        if (h == IntPtr.Zero) return result; // access denied / protected process
        try
        {
            // Probe required size first, then enumerate.
            if (!EnumProcessModulesEx(h, Array.Empty<IntPtr>(), 0, out uint needed, LIST_MODULES_ALL) && needed == 0)
                return result;
            int n = (int)(needed / IntPtr.Size);
            if (n <= 0) return result;
            var mods = new IntPtr[n];
            if (!EnumProcessModulesEx(h, mods, needed, out needed, LIST_MODULES_ALL))
                return result;

            var sb = new StringBuilder(1024);
            foreach (var m in mods)
            {
                if (m == IntPtr.Zero) continue;
                sb.Clear();
                uint len = GetModuleFileNameExW(h, m, sb, (uint)sb.Capacity);
                string path = len > 0 ? sb.ToString() : "";
                long size = 0;
                if (GetModuleInformation(h, m, out var mi, (uint)Marshal.SizeOf<MODULEINFO>())) size = mi.SizeOfImage;
                result.Add(new ProcModule
                {
                    Name = path.Length > 0 ? System.IO.Path.GetFileName(path) : "?",
                    Path = path,
                    SizeBytes = size,
                });
            }
        }
        finally { CloseHandle(h); }
        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return result;
    }
}

#endregion

#region Paired Bluetooth devices (bluetoothapis) · 已配對藍牙裝置

/// <summary>一個已配對藍牙裝置 · One paired Bluetooth device.</summary>
public sealed class BtDevice
{
    public string Name { get; init; } = "";
    public bool Connected { get; init; }
    public bool Authenticated { get; init; }
    public bool Remembered { get; init; }
    public DateTime LastSeen { get; init; }
    internal ulong Address { get; init; }
}

/// <summary>
/// 包 bluetoothapis.dll · Wraps bluetoothapis.dll: BluetoothFindFirstDevice / BluetoothFindNextDevice
/// to list paired devices; BluetoothRemoveDevice to unpair one.
/// </summary>
public static class BluetoothService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEMTIME
    {
        public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
    }

    // BLUETOOTH_ADDRESS is a union of a ULONGLONG and a 6-byte array; we read it as ULONGLONG.
    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_DEVICE_INFO
    {
        public uint dwSize;
        public ulong Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
        public SYSTEMTIME stLastSeen;
        public SYSTEMTIME stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
        public string szName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
    {
        public uint dwSize;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnAuthenticated;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnUnknown;
        [MarshalAs(UnmanagedType.Bool)] public bool fReturnConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fIssueInquiry;
        public byte cTimeoutMultiplier;
        public IntPtr hRadio;
    }

    [DllImport("bluetoothapis.dll", SetLastError = true)]
    private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);
    [DllImport("bluetoothapis.dll", SetLastError = true)]
    private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);
    [DllImport("bluetoothapis.dll", SetLastError = true)]
    private static extern bool BluetoothFindDeviceClose(IntPtr hFind);
    [DllImport("bluetoothapis.dll")]
    private static extern uint BluetoothRemoveDevice(ref ulong pAddress);

    public static List<BtDevice> Paired()
    {
        var result = new List<BtDevice>();
        var search = new BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
            fReturnAuthenticated = true,
            fReturnRemembered = true,
            fReturnUnknown = false,
            fReturnConnected = true,
            fIssueInquiry = false,
            cTimeoutMultiplier = 1,
            hRadio = IntPtr.Zero, // all radios
        };
        var info = new BLUETOOTH_DEVICE_INFO { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>() };

        IntPtr find = BluetoothFindFirstDevice(ref search, ref info);
        if (find == IntPtr.Zero) return result; // no devices / no radio
        try
        {
            do
            {
                result.Add(new BtDevice
                {
                    Name = string.IsNullOrWhiteSpace(info.szName) ? "(unnamed)" : info.szName,
                    Connected = info.fConnected,
                    Authenticated = info.fAuthenticated,
                    Remembered = info.fRemembered,
                    LastSeen = ToDate(info.stLastSeen),
                    Address = info.Address,
                });
                info = new BLUETOOTH_DEVICE_INFO { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>() };
            } while (BluetoothFindNextDevice(find, ref info));
        }
        finally { BluetoothFindDeviceClose(find); }
        return result;
    }

    /// <summary>Unpair a device. Returns true on success (ERROR_SUCCESS).</summary>
    public static bool Remove(BtDevice d)
    {
        if (d is null) return false;
        ulong addr = d.Address;
        return BluetoothRemoveDevice(ref addr) == 0;
    }

    private static DateTime ToDate(SYSTEMTIME s)
    {
        try { return s.wYear == 0 ? DateTime.MinValue : new DateTime(s.wYear, s.wMonth, s.wDay, s.wHour, s.wMinute, s.wSecond, DateTimeKind.Utc).ToLocalTime(); }
        catch { return DateTime.MinValue; }
    }
}

#endregion
