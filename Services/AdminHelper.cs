using System;
using System.Diagnostics;
using System.Security.Principal;

namespace WinForge.Services;

/// <summary>
/// 管理員權限偵測同重新以管理員身分啟動。
/// Elevation detection and "relaunch as administrator" support.
/// </summary>
public static class AdminHelper
{
    private static bool? _isElevated;

    /// <summary>而家係咪以管理員身分運行 · Whether the process is currently elevated.</summary>
    public static bool IsElevated
    {
        get
        {
            if (_isElevated.HasValue) return _isElevated.Value;
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                _isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                _isElevated = false;
            }
            return _isElevated.Value;
        }
    }

    /// <summary>
    /// 以管理員身分重新啟動 app · Relaunch this app elevated, then exit the current instance.
    /// 回傳 true 代表已啟動新實例 · returns true if a new elevated instance was started.
    /// </summary>
    public static bool RelaunchElevated()
    {
        if (IsElevated) return false;
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return false;

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
            };
            Process.Start(psi);
            return true;
        }
        catch
        {
            // 使用者拒絕 UAC · user declined the UAC prompt
            return false;
        }
    }
}
