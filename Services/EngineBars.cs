using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Controls;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 引擎安裝列共用工具 · Shared helper that turns an engine "not found" InfoBar into a one-click
/// touchless auto-install (winget) — with LIVE progress, captured output on failure, an optional cache
/// rescan and a re-check callback. Keeps every engine purely in-app (no "go download X" redirects).
/// </summary>
public static class EngineBars
{
    /// <summary>
    /// Build an action button that silently installs <paramref name="wingetId"/> via winget, refreshes
    /// this process's PATH, optionally clears a service's cached path (<paramref name="rescan"/>), then
    /// runs <paramref name="recheck"/> so the UI updates without an app restart.
    ///
    /// 失敗時唔再靜靜吞錯誤：擷取到嘅 winget 輸出／結束代碼會用一個可展開嘅錯誤區顯示。
    /// On failure the captured winget output/exit code is SURFACED (no longer swallowed) in an expander
    /// under the button, so the user sees the real reason.
    /// </summary>
    public static Button AutoInstallButton(string wingetId, string en, string zh, Func<Task> recheck, Action? rescan = null)
    {
        var b = new Button { Content = Loc.I.Pick(en, zh) };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            b.Content = Loc.I.Pick("Installing…", "安裝緊…");
            TweakResult result;
            try { result = await PackageService.AutoInstallDetailed(wingetId); }
            catch (Exception ex) { result = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
            rescan?.Invoke();
            if (result.Success)
            {
                b.Content = Loc.I.Pick("Installed ✓", "已安裝 ✓");
                try { await recheck(); } catch { /* best effort */ }
            }
            else
            {
                b.Content = Loc.I.Pick("Install failed — retry", "安裝失敗 — 再試");
                b.IsEnabled = true;
                ShowFailureFlyout(b, result);
            }
        };
        return b;
    }

    /// <summary>
    /// 一個「豐富」自動安裝控件：進度條 + 即時狀態 + 成功／失敗動畫 · A "rich" auto-install control returning an
    /// <see cref="InstallProgress"/> pre-wired to install <paramref name="wingetId"/> with live streaming
    /// status. Prefer this in bespoke modules; the bare <see cref="AutoInstallButton"/> stays for InfoBars.
    /// </summary>
    public static InstallProgress AutoInstallProgress(string wingetId, string en, string zh,
        Func<Task>? recheck = null, Action? rescan = null)
    {
        var ctl = new InstallProgress();
        ctl.SetAction(en, zh, async (progress, ct) =>
        {
            var onLine = new Progress<string>(line => progress.Report(InstallProgressReport.FromLine(line)));
            TweakResult r;
            try { r = await PackageService.AutoInstallDetailed(wingetId, onLine, ct); }
            catch (Exception ex) { r = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
            rescan?.Invoke();
            if (r.Success && recheck is not null)
            {
                try { await recheck(); } catch { /* best effort */ }
            }
            return r;
        });
        return ctl;
    }

    /// <summary>失敗時彈出擷取到嘅輸出 · Surface the captured output on failure via a flyout under the button.</summary>
    private static void ShowFailureFlyout(Button b, TweakResult result)
    {
        try
        {
            var msg = result.Message?.Get(Loc.I.Language);
            var detail = result.Output;
            var text = string.IsNullOrWhiteSpace(detail)
                ? (msg ?? Loc.I.Pick("Install failed.", "安裝失敗。"))
                : $"{msg}\n\n{detail!.Trim()}";

            var box = new TextBox
            {
                Text = text,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                MaxHeight = 240,
                MaxWidth = 460,
            };
            var flyout = new Flyout { Content = box };
            flyout.ShowAt(b);
        }
        catch { /* never throw from error display */ }
    }
}
