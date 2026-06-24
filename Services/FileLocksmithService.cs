using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 一個鎖住目標檔案／資料夾嘅程序 · One process that is holding a handle (lock) on the target
/// file or folder. 由 Windows 重新啟動管理員 API（rstrtmgr.dll）枚舉出嚟。
/// Enumerated via the Windows Restart Manager API — the same mechanism PowerToys FileLocksmith
/// surfaces "what process is locking this?".
/// </summary>
public sealed class LockingProcess
{
    public string Name { get; init; } = "";
    public int Pid { get; init; }
    public string User { get; init; } = "";
    /// <summary>呢個程序鎖住嘅、屬於目標嘅檔案 · The locked files (within the target) this process holds.</summary>
    public List<string> Files { get; init; } = new();
    public int FileCount => Files.Count;
    public string Path { get; init; } = "";
    public DateTime? Started { get; init; }
    /// <summary>Restart Manager 應用程式類別（服務／一般程式／關鍵系統等）· The Restart Manager app type.</summary>
    public string AppType { get; init; } = "";
    /// <summary>係咪需要管理員權限先讀到（例如另一個使用者或受保護嘅程序）· Whether elevation is likely needed to act on it.</summary>
    public bool Restricted { get; init; }
}

/// <summary>
/// 枚舉結果 · Result of a scan: the locking processes plus whether some handles were hidden
/// because WinForge is not elevated.
/// </summary>
public sealed class LockScanResult
{
    public List<LockingProcess> Processes { get; init; } = new();
    /// <summary>掃描咗幾多個檔案路徑（資料夾會展開）· How many file paths were registered with the session.</summary>
    public int FilesScanned { get; init; }
    /// <summary>有冇因為唔夠權限而見唔到某啲鎖（例如系統／其他使用者嘅程序）·
    /// Whether at least one process appears to need elevation to inspect/end.</summary>
    public bool NeedsElevationHint { get; init; }
    public string? Error { get; init; }
    public string? ErrorZh { get; init; }
}

/// <summary>
/// 檔案鎖偵測服務 · File-lock detection service. Native clone of PowerToys FileLocksmith using the
/// Windows Restart Manager API (RmStartSession / RmRegisterResources / RmGetList / RmEndSession)
/// via P/Invoke to rstrtmgr.dll. 完全離線、無外部程式 · Fully offline, no external process.
/// </summary>
public static class FileLocksmithService
{
    // 每次掃描資料夾時最多展開幾多個檔案（避免巨型資料夾掛住）·
    // Cap on files expanded for a folder scan, so a huge tree doesn't hang the session.
    private const int MaxFilesPerScan = 4000;

    /// <summary>
    /// 枚舉所有鎖住指定路徑（檔案或資料夾）嘅程序 · Enumerate every process holding a handle on the
    /// given path. 如果係資料夾，會（有上限地）枚舉入面嘅檔案再合併結果。
    /// For a folder, files within it are enumerated (bounded) and the per-process results aggregated.
    /// </summary>
    public static Task<LockScanResult> ScanAsync(string path, CancellationToken ct = default)
        => Task.Run(() => Scan(path, ct), ct);

    public static LockScanResult Scan(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new LockScanResult { Error = "No path given.", ErrorZh = "未提供路徑。" };

        path = path.Trim().Trim('"');

        bool isDir;
        try { isDir = Directory.Exists(path); }
        catch { isDir = false; }

        if (!isDir && !File.Exists(path))
            return new LockScanResult
            {
                Error = $"Path not found: {path}",
                ErrorZh = $"搵唔到路徑：{path}",
            };

        // 收集要登記嘅檔案 · Collect the files to register with the session.
        var files = new List<string>();
        var dirs = new List<string>();
        if (isDir)
        {
            dirs.Add(path);
            try
            {
                foreach (var f in EnumerateFilesBounded(path, MaxFilesPerScan, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    files.Add(f);
                    if (files.Count >= MaxFilesPerScan) break;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { /* 部分子資料夾可能讀唔到 · some subtrees may be unreadable */ }
        }
        else
        {
            files.Add(path);
        }

        return RunRestartManager(files, dirs, ct);
    }

    /// <summary>
    /// 結束（殺死）一個程序 · End a process. Returns (ok, accessDenied).
    /// accessDenied 為 true 時通常代表需要管理員權限 · accessDenied true usually means elevation is needed.
    /// </summary>
    public static (bool ok, bool accessDenied) EndProcess(int pid)
    {
        if (pid <= 4) return (false, false); // 永遠唔殺 System / Idle · never the System/Idle pseudo-processes
        try
        {
            using var p = Process.GetProcessById(pid);
            p.Kill();
            p.WaitForExit(3000);
            return (true, false);
        }
        catch (Win32Exception) { return (false, true); }
        catch (InvalidOperationException) { return (true, false); } // already gone
        catch { return (false, false); }
    }

    /// <summary>喺檔案總管打開並選中某條路徑 · Open Explorer and select the given path.</summary>
    public static bool OpenLocation(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true,
                });
                return true;
            }
            var dir = Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(dir)) return false;
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{dir}\"",
                UseShellExecute = true,
            });
            return true;
        }
        catch { return false; }
    }

    // ===== internals =====

    private static IEnumerable<string> EnumerateFilesBounded(string root, int cap, CancellationToken ct)
    {
        // 自家做廣度優先遍歷，避免單一無權限子資料夾令整個枚舉拋出 ·
        // Manual BFS so one unreadable subfolder doesn't abort the whole walk.
        var queue = new Queue<string>();
        queue.Enqueue(root);
        int yielded = 0;
        while (queue.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = queue.Dequeue();
            string[] subFiles;
            try { subFiles = Directory.GetFiles(dir); }
            catch { subFiles = Array.Empty<string>(); }
            foreach (var f in subFiles)
            {
                yield return f;
                if (++yielded >= cap) yield break;
            }
            string[] subDirs;
            try { subDirs = Directory.GetDirectories(dir); }
            catch { subDirs = Array.Empty<string>(); }
            foreach (var d in subDirs) queue.Enqueue(d);
        }
    }

    private static LockScanResult RunRestartManager(List<string> files, List<string> dirs, CancellationToken ct)
    {
        uint sessionHandle = 0;
        var key = new StringBuilder(CCH_RM_SESSION_KEY + 1);
        int rc = RmStartSession(out sessionHandle, 0, key);
        if (rc != 0)
            return new LockScanResult
            {
                Error = $"Restart Manager could not start a session (error {rc}).",
                ErrorZh = $"重新啟動管理員無法建立工作階段（錯誤 {rc}）。",
            };

        try
        {
            // 登記資源（檔案 + 資料夾路徑）· Register the resources (files + directory paths).
            // Restart Manager 接受目錄路徑做資源，會回報鎖住該目錄本身嘅程序。
            // It accepts directory paths and reports processes locking the directory itself.
            var resources = new List<string>(files.Count + dirs.Count);
            resources.AddRange(files);
            resources.AddRange(dirs);

            if (resources.Count > 0)
            {
                rc = RmRegisterResources(sessionHandle,
                    (uint)resources.Count, resources.ToArray(),
                    0, null, 0, null);
                if (rc != 0)
                    return new LockScanResult
                    {
                        FilesScanned = files.Count,
                        Error = $"Restart Manager could not register the file(s) (error {rc}).",
                        ErrorZh = $"重新啟動管理員無法登記檔案（錯誤 {rc}）。",
                    };
            }

            ct.ThrowIfCancellationRequested();

            // 第一次呼叫攞所需數量，再用足夠大嘅 buffer 重試 ·
            // First call to learn how many entries, then retry with a large-enough buffer.
            uint pnProcInfoNeeded = 0;
            uint pnProcInfo = 0;
            uint lpdwRebootReasons = RmRebootReasonNone;
            RM_PROCESS_INFO[] info = Array.Empty<RM_PROCESS_INFO>();

            rc = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);
            if (rc == ERROR_MORE_DATA && pnProcInfoNeeded > 0)
            {
                // 留少少餘裕，因為兩次呼叫之間清單可能變大 ·
                // Add slack because the list can grow between the two calls.
                pnProcInfo = pnProcInfoNeeded + 8;
                info = new RM_PROCESS_INFO[pnProcInfo];
                lpdwRebootReasons = RmRebootReasonNone;
                rc = RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, info, ref lpdwRebootReasons);
            }
            else if (rc == 0)
            {
                // 冇任何程序鎖住 · nothing is locking it.
                return new LockScanResult { Processes = new(), FilesScanned = files.Count };
            }

            if (rc != 0)
                return new LockScanResult
                {
                    FilesScanned = files.Count,
                    Error = $"Restart Manager could not list processes (error {rc}).",
                    ErrorZh = $"重新啟動管理員無法列出程序（錯誤 {rc}）。",
                };

            var processes = new List<LockingProcess>();
            bool needsElevation = false;

            for (uint i = 0; i < pnProcInfo; i++)
            {
                ct.ThrowIfCancellationRequested();
                var pi = info[i];
                int pid = (int)pi.Process.dwProcessId;
                if (pid == 0) continue;

                string name = pi.strAppName ?? "";
                string exePath = "";
                string user = "";
                DateTime? started = null;
                bool restricted = false;

                // 用 PID + 啟動時間確認程序未被回收 · Confirm the PID by matching the process creation time.
                try
                {
                    using var p = Process.GetProcessById(pid);
                    if (string.IsNullOrEmpty(name)) name = p.ProcessName;
                    try { started = p.StartTime; } catch { restricted = true; }
                    try { exePath = QueryImagePath(pid); }
                    catch { restricted = true; }
                }
                catch { /* 程序可能已經結束 · process may have exited */ }

                user = QueryProcessUser(pid, ref restricted);
                if (restricted) needsElevation = true;

                // 收集呢個程序鎖住、而又屬於目標嘅檔案 ·
                // Collect which of the target files this process is locking.
                var locked = WhichFilesLocked(files, dirs, pid);

                processes.Add(new LockingProcess
                {
                    Name = string.IsNullOrEmpty(name) ? $"PID {pid}" : name,
                    Pid = pid,
                    User = user,
                    Files = locked,
                    Path = exePath,
                    Started = started,
                    AppType = DescribeAppType(pi.ApplicationType),
                    Restricted = restricted,
                });
            }

            // 排序：鎖住檔案越多越前，然後依名稱 ·
            // Sort: most locked files first, then by name.
            processes = processes
                .OrderByDescending(p => p.FileCount)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new LockScanResult
            {
                Processes = processes,
                FilesScanned = files.Count,
                NeedsElevationHint = needsElevation && !IsElevated(),
            };
        }
        finally
        {
            try { RmEndSession(sessionHandle); } catch { }
        }
    }

    /// <summary>
    /// Restart Manager 唔會直接話你知邊個程序鎖咗邊個檔案，所以對每個 PID 用一次只登記單一檔案
    /// 嘅快速會話去確認 · The list call doesn't map files→pids, so for accuracy we do a cheap per-file
    /// check only when there are few files; otherwise we attribute all scanned files to the process.
    /// </summary>
    private static List<string> WhichFilesLocked(List<string> files, List<string> dirs, int pid)
    {
        // 為咗效能，只喺檔案數量唔多時逐個檔案精確比對；否則回報全部目標檔案。
        // For performance, do exact per-file attribution only when the file set is small.
        var all = new List<string>(files);
        all.AddRange(dirs);
        if (files.Count == 0) return all;
        if (files.Count > 64) return all; // 大資料夾：唔逐個查，回報整批 · large folder: attribute the whole batch

        var locked = new List<string>();
        foreach (var f in all)
        {
            if (ProcessLocksFile(f, pid)) locked.Add(f);
        }
        // 如果精確比對都搵唔到（例如目錄鎖、或時序差異），就退回回報全部 ·
        // If exact matching found nothing (dir locks / timing), fall back to the whole set.
        return locked.Count > 0 ? locked : all;
    }

    private static bool ProcessLocksFile(string file, int pid)
    {
        uint session = 0;
        var key = new StringBuilder(CCH_RM_SESSION_KEY + 1);
        if (RmStartSession(out session, 0, key) != 0) return false;
        try
        {
            if (RmRegisterResources(session, 1, new[] { file }, 0, null, 0, null) != 0) return false;
            uint needed = 0, count = 0, reasons = RmRebootReasonNone;
            int rc = RmGetList(session, out needed, ref count, null, ref reasons);
            if (rc == ERROR_MORE_DATA && needed > 0)
            {
                count = needed + 4;
                var info = new RM_PROCESS_INFO[count];
                reasons = RmRebootReasonNone;
                if (RmGetList(session, out needed, ref count, info, ref reasons) != 0) return false;
                for (uint i = 0; i < count; i++)
                    if ((int)info[i].Process.dwProcessId == pid) return true;
            }
            return false;
        }
        catch { return false; }
        finally { try { RmEndSession(session); } catch { } }
    }

    private static string DescribeAppType(RM_APP_TYPE t) => t switch
    {
        RM_APP_TYPE.RmMainWindow => "App (window) · 視窗程式",
        RM_APP_TYPE.RmOtherWindow => "App · 程式",
        RM_APP_TYPE.RmService => "Service · 服務",
        RM_APP_TYPE.RmExplorer => "Explorer · 檔案總管",
        RM_APP_TYPE.RmConsole => "Console · 主控台",
        RM_APP_TYPE.RmCritical => "Critical system · 關鍵系統",
        _ => "Unknown · 未知",
    };

    private static string QueryImagePath(int pid)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            int cap = 1024;
            var sb = new StringBuilder(cap);
            if (QueryFullProcessImageName(h, 0, sb, ref cap))
                return sb.ToString();
            return "";
        }
        finally { CloseHandle(h); }
    }

    private static string QueryProcessUser(int pid, ref bool restricted)
    {
        IntPtr h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (h == IntPtr.Zero) { restricted = true; return ""; }
        try
        {
            if (!OpenProcessToken(h, TOKEN_QUERY, out var token))
            {
                restricted = true;
                return "";
            }
            try
            {
                GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, IntPtr.Zero, 0, out int len);
                if (len <= 0) { restricted = true; return ""; }
                IntPtr buf = Marshal.AllocHGlobal(len);
                try
                {
                    if (!GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenUser, buf, len, out _))
                    {
                        restricted = true;
                        return "";
                    }
                    var tu = Marshal.PtrToStructure<TOKEN_USER>(buf);
                    var sid = new SecurityIdentifier(tu.User.Sid);
                    try
                    {
                        var acct = (NTAccount)sid.Translate(typeof(NTAccount));
                        return acct.Value;
                    }
                    catch { return sid.Value; }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            finally { CloseHandle(token); }
        }
        catch { restricted = true; return ""; }
        finally { CloseHandle(h); }
    }

    private static bool IsElevated()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    // ===== P/Invoke: Restart Manager (rstrtmgr.dll) =====

    private const int CCH_RM_SESSION_KEY = 32;
    private const int CCH_RM_MAX_APP_NAME = 255;
    private const int CCH_RM_MAX_SVC_NAME = 63;
    private const int ERROR_MORE_DATA = 234;
    private const uint RmRebootReasonNone = 0;

    private enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public uint dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(uint pSessionHandle,
        uint nFiles, string[]? rgsFilenames,
        uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(uint dwSessionHandle,
        out uint pnProcInfoNeeded, ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        ref uint lpdwRebootReasons);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    // ===== P/Invoke: process token / image path =====

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;

    private enum TOKEN_INFORMATION_CLASS { TokenUser = 1 }

    [StructLayout(LayoutKind.Sequential)]
    private struct SID_AND_ATTRIBUTES
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_USER
    {
        public SID_AND_ATTRIBUTES User;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass,
        IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);
}
