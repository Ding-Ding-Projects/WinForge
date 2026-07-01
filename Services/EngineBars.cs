using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 引擎安裝列共用工具 · Shared helper that turns an engine "not found" InfoBar into a one-click
/// touchless auto-install (winget) — now with a live progress bar, streaming status text and a flashy
/// pulse animation, plus real error surfacing (the actual winget output, on failure). Keeps every engine
/// purely in-app (no "go download X" redirects). The winget call runs ELEVATED so installs actually succeed.
/// </summary>
public static class EngineBars
{
    /// <summary>
    /// Build an action button that silently installs <paramref name="wingetId"/> via winget (elevated),
    /// refreshes this process's PATH, optionally clears a service's cached path (<paramref name="rescan"/>),
    /// then runs <paramref name="recheck"/> so the UI updates without an app restart. The button embeds a
    /// progress bar + status line + pulse animation and shows the real error on failure. Returns a Button so
    /// every existing call site keeps working unchanged.
    /// </summary>
    public static Button AutoInstallButton(string wingetId, string en, string zh, Func<Task> recheck, Action? rescan = null)
    {
        var label = new TextBlock
        {
            Text = Loc.I.Pick(en, zh),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var bar = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 4,
            Margin = new Thickness(0, 6, 0, 0),
            Visibility = Visibility.Collapsed,
        };
        var status = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed,
        };
        try { status.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]; } catch { }

        var stack = new StackPanel { Spacing = 2, MinWidth = 210 };
        stack.Children.Add(label);
        stack.Children.Add(bar);
        stack.Children.Add(status);

        var b = new Button { Content = stack, HorizontalContentAlignment = HorizontalAlignment.Stretch };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            bar.Visibility = Visibility.Visible;
            status.Visibility = Visibility.Visible;
            label.Text = Loc.I.Pick("Installing…", "安裝緊…");
            status.Text = Loc.I.Pick("Requesting administrator… approve the UAC prompt.", "要求管理員權限… 請批准 UAC 提示。");
            SetStatusBrush(status, "TextFillColorSecondaryBrush");
            var pulse = StartPulse(stack);

            TweakResult r;
            try { r = await PackageService.AutoInstallResult(wingetId); }
            catch (Exception ex) { r = TweakResult.Fail(ex.Message, ex.Message, ex.Message); }

            pulse.Stop();
            stack.Opacity = 1.0;
            rescan?.Invoke();
            bar.Visibility = Visibility.Collapsed;

            if (r.Success)
            {
                label.Text = Loc.I.Pick("Installed ✓", "已安裝 ✓");
                status.Text = r.Message?.Primary ?? Loc.I.Pick("Ready.", "已就緒。");
                SetStatusBrush(status, "SystemFillColorSuccessBrush");
                ToolTipService.SetToolTip(b, null);
                try { await recheck(); } catch { }
            }
            else
            {
                label.Text = Loc.I.Pick("Install failed — retry", "安裝失敗 — 再試");
                var full = !string.IsNullOrWhiteSpace(r.Output) ? r.Output! : (r.Message?.Primary ?? "");
                var oneLine = FirstMeaningfulLine(full);
                status.Text = string.IsNullOrWhiteSpace(oneLine) ? Loc.I.Pick("Unknown error — see tooltip.", "未知錯誤 — 睇提示。") : oneLine;
                SetStatusBrush(status, "SystemFillColorCriticalBrush");
                ToolTipService.SetToolTip(b, string.IsNullOrWhiteSpace(full) ? oneLine : full); // full error on hover
                b.IsEnabled = true;
            }
        };
        return b;
    }

    /// <summary>閃動脈衝動畫（安裝進行中）· A gentle opacity pulse while the install runs (flashy but tasteful).</summary>
    private static Storyboard StartPulse(FrameworkElement target)
    {
        var sb = new Storyboard();
        var anim = new DoubleAnimation
        {
            From = 1.0,
            To = 0.55,
            Duration = new Duration(TimeSpan.FromMilliseconds(650)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(anim, target);
        Storyboard.SetTargetProperty(anim, "Opacity");
        sb.Children.Add(anim);
        try { sb.Begin(); } catch { }
        return sb;
    }

    private static void SetStatusBrush(TextBlock tb, string resourceKey)
    {
        try { tb.Foreground = (Brush)Application.Current.Resources[resourceKey]; } catch { }
    }

    private static string FirstMeaningfulLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            // Skip winget's progress spinner / bar noise.
            if (line.All(c => c is '-' or '\\' or '/' or '|' or '█' or '▒' or '░' or ' ' or '%')) continue;
            return line.Length > 160 ? line[..160] + "…" : line;
        }
        var t = text.Trim();
        return t.Length > 160 ? t[..160] + "…" : t;
    }
}
