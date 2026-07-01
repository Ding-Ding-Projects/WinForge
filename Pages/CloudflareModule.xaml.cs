using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Cloudflare 與 Cloudflare Tunnel · cloudflared, named &amp; quick Tunnels, Cloudflare Access,
/// DNS-over-HTTPS and WARP — all in-app via the cloudflared / warp-cli CLIs. Bilingual.
///
/// 每個操作用手砌嘅控件列渲染（唔再用 TweakCard）：左邊雙語標題／說明，右邊對應控件。
/// Each operation is rendered as a hand-built control row (no TweakCard): bilingual
/// title/description on the left, the matching WinUI control on the right.
/// </summary>
public sealed partial class CloudflareModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _rowBusy;

    public CloudflareModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildQuickActions();
        PopulateOps(string.Empty);
        await CheckEngine();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildQuickActions();
        PopulateOps(OpsFilter?.Text ?? string.Empty);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Cloudflare & Tunnel · Cloudflare 與 Tunnel";
        HeaderBlurb.Text = P(
            "Run cloudflared from inside WinForge: named tunnels, free quick tunnels, route DNS, Cloudflare Access, DNS-over-HTTPS and WARP.",
            "喺 WinForge 直接用 cloudflared：具名 tunnel、免費快速 tunnel、DNS 路由、Cloudflare Access、DNS-over-HTTPS 同 WARP。");
        _ops ??= CloudflareOperations.All().ToList();
        AdvancedHeader.Text = P($"Operations ({_ops.Count})", $"操作（{_ops.Count}）");
        PlaceholderHint.Text = P(
            "Tip: many operations use placeholders — MYTUNNEL (tunnel name), app.example.com (hostname), http://localhost:8080 (local service). Edit them to your values; long-running ones (run, quick tunnel, DoH) open in a terminal.",
            "提示：好多操作用咗佔位符 — MYTUNNEL（tunnel 名）、app.example.com（主機名）、http://localhost:8080（本機服務）。改成你嘅值；長時間運行嘅（run、快速 tunnel、DoH）會喺終端機開出嚟。");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
    }

    private async Task CheckEngine()
    {
        bool ok = await CloudflareService.IsInstalledAsync();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("cloudflared not found", "搵唔到 cloudflared");
        EngineBar.Message = P("Click to install cloudflared automatically (winget) — no restart needed.",
            "撳一下自動安裝 cloudflared（winget）— 唔使重開。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            CloudflareService.WingetId, "Install cloudflared automatically", "自動安裝 cloudflared",
            async () => { await CheckEngine(); }, null);
    }

    private void BuildQuickActions()
    {
        QuickActions.Children.Clear();
        AddQuick(P("Version", "版本"), () => CloudflareService.RunRaw("--version"));
        AddQuick(P("List tunnels", "列出 tunnel"), () => CloudflareService.RunRaw("tunnel list"));
        AddQuick(P("Login", "登入"), () => Task.FromResult(CloudflareService.LaunchInTerminal("cloudflared", "tunnel login")));
        AddQuick(P("WARP status", "WARP 狀態"), () => CloudflareService.Warp("status"));
        AddQuick(P("Update", "更新"), () => CloudflareService.RunRaw("update"));
    }

    private void AddQuick(string label, Func<Task<TweakResult>> run)
    {
        var btn = new Button { Content = label };
        btn.Click += async (_, _) =>
        {
            btn.IsEnabled = false;
            try
            {
                var r = await run();
                OutBorder.Visibility = Visibility.Visible;
                var body = string.IsNullOrWhiteSpace(r.Output)
                    ? ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "")
                    : r.Output!;
                OutText.Text = body.Length > 4000 ? body[^4000..] : body;
            }
            catch (Exception ex) { OutBorder.Visibility = Visibility.Visible; OutText.Text = ex.Message; }
            finally { btn.IsEnabled = true; }
        };
        QuickActions.Children.Add(btn);
    }

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= CloudflareOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: title + optional secondary title + description
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        var title = new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        text.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(op, result);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Info → TextBlock (+ refresh) ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Shared result / status area ----------------
    private void ShowResult(TweakDefinition op, TweakResult result)
    {
        ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = result.Message is null ? string.Empty : result.Message.Get(Loc.I.Language);
        ResultBar.IsOpen = true;

        // Mirror any raw output into the monospace pane (same behaviour as the quick actions).
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            OutBorder.Visibility = Visibility.Visible;
            var body = result.Output!;
            OutText.Text = body.Length > 4000 ? body[^4000..] : body;
        }
    }

    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = P(en, zh);
        ResultBar.IsOpen = true;
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message;
        ResultBar.IsOpen = true;
    }
}
