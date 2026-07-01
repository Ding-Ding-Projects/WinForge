using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 7+ Taskbar Tweaker · 工作列調校。WinForge 原生子集（真正登錄機碼／ms-settings 嘅工作列調校），
/// 加上偵測 + 啟動真正嘅 7+TT 同 Windhawk 做深層行為。
///
/// 7+ Taskbar Tweaker module. Renders every real registry / ms-settings taskbar tweak WinForge can
/// natively do (from <see cref="TaskbarTweaks"/>), and detects + launches the real 7+TT and Windhawk
/// for the deep runtime behaviours that genuinely cannot be reimplemented in managed C#.
/// No external redirect beyond launching an already-installed tool; 7+TT is never bundled or auto-installed.
///
/// 每個調校用手砌嘅控件列渲染（唔再用 Controls/TweakCard）：左邊雙語標題／說明，右邊對應控件，
/// 結果／錯誤集中喺一個常駐 InfoBar。 Each tweak is a hand-built control row (no Controls/TweakCard):
/// bilingual title/description on the left, the matching WinUI control on the right; results and
/// errors go to one persistent InfoBar.
/// </summary>
public sealed partial class TaskbarTweakerModule : Page
{
    private TaskbarTweakerService.Detection _sevenTt = new() { Installed = false };
    private TaskbarTweakerService.Detection _windhawk = new() { Installed = false };
    private bool _rowBusy;

    public TaskbarTweakerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Populate(string.Empty);
        RefreshDetection();
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
        Populate(FilterBox?.Text ?? string.Empty);
        RefreshDetection();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Taskbar Tweaker · 工作列調校";
        HeaderBlurb.Text = P(
            "Every taskbar and Start tweak WinForge can do natively, plus a launcher for the real 7+ Taskbar Tweaker and Windhawk for behaviours that need a runtime hook.",
            "WinForge 原生可以做嘅工作列同開始功能表調校，仲有可以啟動真正嘅 7+ Taskbar Tweaker 同 Windhawk，做需要執行時鈎子嘅行為。");

        FilterBox.PlaceholderText = P("Filter taskbar tweaks…", "篩選工作列調校…");

        DeepTitle.Text = P(
            "Deep behaviours need a runtime hook",
            "深層行為需要執行時鈎子");
        DeepBody.Text = P(
            "Middle-click to close, double-click to show desktop, scroll-to-switch and drag-to-reorder all work by injecting a DLL into explorer.exe and rewriting the taskbar's window procedures at runtime. They genuinely cannot be reimplemented in managed C# — use the real 7+ Taskbar Tweaker (closed-source freeware) or Windhawk mods (see handoff 29). WinForge only detects and launches them, it never bundles or auto-installs 7+TT.",
            "中鍵關閉、雙擊顯示桌面、捲動切換、拖放重排，全部都係靠注入 explorer.exe 嘅 DLL 喺執行時改寫工作列嘅視窗程序。呢啲喺 C# 託管程式碼真係做唔到 — 要用真正嘅 7+ Taskbar Tweaker（閉源免費軟件）或者 Windhawk 模組（見 handoff 29）。WinForge 只係偵測同啟動佢哋，永遠唔會綑綁或者自動安裝 7+TT。");

        RestartExplorerBtn.Content = P("Restart Explorer", "重啟檔案總管");
    }

    // ================= Tweak rows =================

    private void Populate(string filter)
    {
        RowsPanel.Children.Clear();

        IEnumerable<TweakDefinition> tweaks = TaskbarTweaks.All();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            tweaks = tweaks.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        bool any = false;
        foreach (var t in tweaks)
        {
            if (!first) RowsPanel.Children.Add(BuildDivider());
            first = false;
            RowsPanel.Children.Add(BuildRow(t));
            any = true;
        }

        if (!any)
        {
            RowsPanel.Children.Add(new TextBlock
            {
                Text = P("No matches.", "搵唔到。"),
                Opacity = 0.6,
                Margin = new Thickness(4, 12, 0, 0),
            });
        }
    }

    private void FilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            Populate(sender.Text ?? string.Empty);
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        // Small admin / restart hints so nothing from the card chrome is lost.
        var hint = HintText(op);
        if (hint is not null)
            text.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private string? HintText(TweakDefinition op)
    {
        var parts = new List<string>();
        if (op.RequiresAdmin) parts.Add(P("Admin", "管理員"));
        switch (op.Restart)
        {
            case RestartScope.Explorer: parts.Add(P("Restart Explorer", "重啟檔案總管")); break;
            case RestartScope.SignOut: parts.Add(P("Sign out", "登出")); break;
            case RestartScope.Reboot: parts.Add(P("Reboot", "重新開機")); break;
        }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
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
        TweakKind.RadioGroup => BuildRadio(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
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
        try { SelectComboToCurrent(combo, op); }
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
                    try { SelectComboToCurrent(combo, op); } catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    private static void SelectComboToCurrent(ComboBox combo, TweakDefinition op)
    {
        var cur = op.GetCurrentChoice?.Invoke();
        if (cur is null || op.Choices is null) return;
        for (int i = 0; i < op.Choices.Count; i++)
            if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
            { combo.SelectedIndex = i; return; }
    }

    // ---------------- RadioGroup → RadioButtons (reuses Choices) ----------------
    private FrameworkElement BuildRadio(TweakDefinition op)
    {
        var radio = new RadioButtons { MaxColumns = 1 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                radio.Items.Add(new RadioButton { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try { SelectRadioToCurrent(radio, op); }
        catch { /* leave unselected */ }
        suppress = false;

        radio.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (radio.SelectedItem is RadioButton rb && rb.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try { SelectRadioToCurrent(radio, op); } catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return radio;
    }

    private static void SelectRadioToCurrent(RadioButtons radio, TweakDefinition op)
    {
        var cur = op.GetCurrentChoice?.Invoke();
        if (cur is null || op.Choices is null) return;
        for (int i = 0; i < op.Choices.Count; i++)
            if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
            { radio.SelectedIndex = i; return; }
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = op.Min,
            Maximum = op.Max,
            StepFrequency = op.Step,
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var valueLabel = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        string Fmt(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                               : v.ToString("0.###", CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueLabel.Text = Fmt(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueLabel.Text = Fmt(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };

        panel.Children.Add(slider);
        panel.Children.Add(valueLabel);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step,
            LargeChange = op.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null) return;
            if (double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
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

    // ---------------- Shared result / status area (one persistent InfoBar) ----------------
    private void ShowResult(TweakDefinition op, TweakResult result)
    {
        ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = result.Message is null
            ? (result.Output ?? string.Empty)
            : result.Message.Get(Loc.I.Language);
        ResultBar.IsOpen = true;
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

    // ---------------- Detection / launch ----------------

    private void RefreshDetection()
    {
        _sevenTt = TaskbarTweakerService.Detect7Tt();
        _windhawk = TaskbarTweakerService.DetectWindhawk();
        RenderSevenTtBar();
        RenderWindhawkBar();
        RenderWindhawkInstall();
    }

    private void RenderSevenTtBar()
    {
        if (_sevenTt.Installed && _sevenTt.ExecutablePath is not null)
        {
            SevenTtBar.Severity = InfoBarSeverity.Success;
            SevenTtBar.Title = P("7+ Taskbar Tweaker is installed", "已安裝 7+ Taskbar Tweaker");
            var ver = string.IsNullOrEmpty(_sevenTt.Version) ? "" : $" (v{_sevenTt.Version})";
            SevenTtBar.Message = P(
                $"Launch it for the deep runtime behaviours{ver}.",
                $"可以啟動佢嚟做深層執行時行為{ver}。");
            var btn = new Button { Content = P("Launch 7+TT", "啟動 7+TT") };
            btn.Click += (_, _) => TaskbarTweakerService.Launch(_sevenTt.ExecutablePath);
            SevenTtBar.ActionButton = btn;
            SevenTtBar.IsOpen = true;
        }
        else
        {
            SevenTtBar.Severity = InfoBarSeverity.Informational;
            SevenTtBar.Title = P("7+ Taskbar Tweaker not detected", "未偵測到 7+ Taskbar Tweaker");
            SevenTtBar.Message = P(
                "7+TT is closed-source freeware with no winget package — WinForge will not download or install it. Install it yourself if you want the deep behaviours; this page will then offer to launch it.",
                "7+TT 係閉源免費軟件，無 winget 套件 — WinForge 唔會下載或者安裝佢。想要深層行為就自己裝，裝咗之後呢頁就會畀你啟動。");
            SevenTtBar.ActionButton = null;
            SevenTtBar.IsOpen = true;
        }
    }

    private void RenderWindhawkBar()
    {
        if (_windhawk.Installed && _windhawk.ExecutablePath is not null)
        {
            WindhawkBar.Severity = InfoBarSeverity.Success;
            WindhawkBar.Title = P("Windhawk is installed", "已安裝 Windhawk");
            var ver = string.IsNullOrEmpty(_windhawk.Version) ? "" : $" (v{_windhawk.Version})";
            WindhawkBar.Message = P(
                $"Open Windhawk to install taskbar mods for the deep behaviours{ver}.",
                $"開啟 Windhawk 嚟裝工作列模組做深層行為{ver}。");
            var btn = new Button { Content = P("Launch Windhawk", "啟動 Windhawk") };
            btn.Click += (_, _) => TaskbarTweakerService.Launch(_windhawk.ExecutablePath);
            WindhawkBar.ActionButton = btn;
            WindhawkBar.IsOpen = true;
        }
        else
        {
            WindhawkBar.Severity = InfoBarSeverity.Informational;
            WindhawkBar.Title = P("Windhawk not detected", "未偵測到 Windhawk");
            WindhawkBar.Message = P(
                "Windhawk is a maintained, open mod platform that can replicate 7+TT's deep behaviours. Install it below to get started.",
                "Windhawk 係仍有維護嘅開放模組平台，可以重現 7+TT 嘅深層行為。喺下面安裝就可以開始。");
            WindhawkBar.ActionButton = null;
            WindhawkBar.IsOpen = true;
        }
    }

    private void RenderWindhawkInstall()
    {
        // Offer a one-click Windhawk install (winget) only when it is NOT already present.
        // 7+TT is deliberately never offered for auto-install (closed-source freeware, no winget id).
        if (_windhawk.Installed)
        {
            WindhawkInstallHost.Visibility = Visibility.Collapsed;
            return;
        }

        WindhawkInstallHost.Content = P("Install Windhawk", "安裝 Windhawk");
        WindhawkInstallHost.IsEnabled = true;
        WindhawkInstallHost.Visibility = Visibility.Visible;
        WindhawkInstallHost.Click -= WindhawkInstall_Click;
        WindhawkInstallHost.Click += WindhawkInstall_Click;
    }

    private async void WindhawkInstall_Click(object sender, RoutedEventArgs e)
    {
        var b = (Button)sender;
        b.IsEnabled = false;
        b.Content = P("Installing…", "安裝緊…");
        bool ok;
        try { ok = await PackageService.AutoInstall("RamenSoftware.Windhawk"); }
        catch { ok = false; }
        if (ok)
        {
            b.Content = P("Installed ✓", "已安裝 ✓");
            RefreshDetection();
        }
        else
        {
            b.Content = P("Install failed — retry", "安裝失敗 — 再試");
            b.IsEnabled = true;
        }
    }

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var b = (Button)sender;
        b.IsEnabled = false;
        try { await ShellRunner.RunCmd("taskkill /f /im explorer.exe & start explorer.exe"); }
        catch { /* never throw from UI */ }
        b.IsEnabled = true;
    }
}
