using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WinForge.Services;

/// <summary>
/// 啟動使用者層級程式 · Starts interactive applications at the signed-in user's normal integrity level.
/// WinForge is normally unelevated, but it can be started as administrator for maintenance work. In that
/// case, launching an executable resolved from HKCU, PATH or LocalAppData directly would accidentally grant
/// it administrator rights. Explorer's automation object is hosted by the normal desktop shell and acts as
/// the de-elevation broker. We deliberately fail closed if that broker is unavailable.
/// </summary>
public static class UserProcessLauncher
{
    public static bool TryStart(string fileName, string? arguments, string? workingDirectory, out string error)
    {
        error = "";
        try
        {
            if (!AdminHelper.IsElevated)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? "",
                    WorkingDirectory = workingDirectory ?? "",
                    UseShellExecute = true,
                });
                return true;
            }

            // Ask the desktop folder view for its Application object. That object lives in the Explorer
            // process hosting the user's desktop, so its ShellExecute uses the interactive user's normal
            // token (including the over-the-shoulder UAC case). Merely creating Shell.Application here is
            // not sufficient: we use it only to locate the desktop-hosted automation object.
            var shellType = Type.GetTypeFromProgID("Shell.Application", throwOnError: false);
            if (shellType is null)
            {
                error = "The normal desktop shell is unavailable. Restart WinForge without administrator rights.";
                return false;
            }

            object? shell = null;
            object? windows = null;
            object? desktop = null;
            object? document = null;
            object? desktopApplication = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                if (shell is null)
                {
                    error = "The normal desktop shell could not be started. Restart WinForge without administrator rights.";
                    return false;
                }

                windows = shell.GetType().InvokeMember(
                    "Windows", BindingFlags.InvokeMethod, binder: null, target: shell, args: null);
                if (windows is null)
                {
                    error = "The Explorer window service is unavailable. Restart WinForge without administrator rights.";
                    return false;
                }

                // SWC_DESKTOP (8) + SWFO_NEEDDISPATCH (1). Reflection updates the fourth (out HWND)
                // argument in-place; only the returned desktop automation object matters here.
                object?[] findArgs = { 0, 0, 8, 0, 1 };
                desktop = windows.GetType().InvokeMember(
                    "FindWindowSW", BindingFlags.InvokeMethod, binder: null, target: windows, args: findArgs);
                if (desktop is null)
                {
                    error = "The Explorer desktop automation object is unavailable. Restart WinForge without administrator rights.";
                    return false;
                }

                document = desktop.GetType().InvokeMember(
                    "Document", BindingFlags.GetProperty, binder: null, target: desktop, args: null);
                desktopApplication = document?.GetType().InvokeMember(
                    "Application", BindingFlags.GetProperty, binder: null, target: document, args: null);
                if (desktopApplication is null)
                {
                    error = "The Explorer launch broker is unavailable. Restart WinForge without administrator rights.";
                    return false;
                }

                desktopApplication.GetType().InvokeMember(
                    "ShellExecute",
                    BindingFlags.InvokeMethod,
                    binder: null,
                    target: desktopApplication,
                    args: new object?[] { fileName, arguments ?? "", workingDirectory ?? "", "open", 1 });
                return true;
            }
            finally
            {
                foreach (var com in new[] { desktopApplication, document, desktop, windows, shell })
                {
                    if (com is null || !Marshal.IsComObject(com)) continue;
                    try { Marshal.FinalReleaseComObject(com); } catch { }
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
