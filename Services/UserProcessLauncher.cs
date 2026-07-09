using System;
using System.Diagnostics;

namespace WinForge.Services;

/// <summary>
/// 啟動使用者層級程式 · Starts interactive applications only when WinForge is at normal integrity.
/// WinForge is normally unelevated, but it can be started as administrator for maintenance work. In that
/// case, paths resolved from HKCU, PATH or LocalAppData belong to the elevated account and must never be
/// executed implicitly. We fail closed and ask for a normal restart; app manifests can request their own
/// elevation after an explicit normal-integrity launch when genuinely required.
/// </summary>
public static class UserProcessLauncher
{
    public static bool TryStart(string fileName, string? arguments, string? workingDirectory, out string error)
    {
        error = "";
        try
        {
            if (AdminHelper.IsElevated)
            {
                error = "WinForge is running as administrator. Restart it normally before launching interactive apps.";
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments ?? "",
                WorkingDirectory = workingDirectory ?? "",
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
