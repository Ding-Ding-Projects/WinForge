using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;

namespace WinForge.Services;

/// <summary>
/// 引擎安裝列共用工具 · Shared helper that turns an engine "not found" InfoBar into a one-click
/// touchless auto-install (winget) — with progress text, an optional cache rescan and a re-check
/// callback. Keeps every engine purely in-app (no "go download X" redirects).
/// </summary>
public static class EngineBars
{
    /// <summary>
    /// Build an action button that silently installs <paramref name="wingetId"/> via winget, refreshes
    /// this process's PATH, optionally clears a service's cached path (<paramref name="rescan"/>), then
    /// runs <paramref name="recheck"/> so the UI updates without an app restart.
    /// </summary>
    public static Button AutoInstallButton(string wingetId, string en, string zh, Func<Task> recheck, Action? rescan = null)
    {
        var b = new Button { Content = Loc.I.Pick(en, zh) };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            b.Content = Loc.I.Pick("Installing…", "安裝緊…");
            bool ok;
            try { ok = await PackageService.AutoInstall(wingetId); }
            catch { ok = false; }
            rescan?.Invoke();
            if (ok)
            {
                b.Content = Loc.I.Pick("Installed ✓", "已安裝 ✓");
                await recheck();
            }
            else
            {
                b.Content = Loc.I.Pick("Install failed — retry", "安裝失敗 — 再試");
                b.IsEnabled = true;
            }
        };
        return b;
    }
}
