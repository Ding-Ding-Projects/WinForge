using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Windhawk mod 管理器前端 · Windhawk mod-manager front-end — detect/install the official Windhawk
/// (winget RamenSoftware.Windhawk), launch its UI, open the mods folder, and browse a curated bilingual
/// gallery of popular mods that deep-link into windhawk.net. Bilingual. No WinRT pickers. No redirect for install.
///
/// The mod gallery is rendered as hand-built control rows (one <see cref="Grid"/> per tweak, bilingual title
/// + description on the left, the matching WinUI control on the right) — no <c>Controls.TweakCard</c> is used.
/// </summary>
public sealed partial class WindhawkModule : Page
{
    private List<TweakDefinition>? _mods;
    private bool _rowBusy; // guard so only one control-row action runs at a time

    public WindhawkModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += async (_, _) => { Render(); PopulateMods(string.Empty); await CheckEngine(); };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateMods(ModFilter.Text ?? string.Empty); _ = CheckEngine(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Windhawk Mods · Windhawk 模組";
        HeaderBlurb.Text = P(
            "Windhawk is an open-source platform that customizes Windows by injecting community 'mods' — taskbar height, clock tweaks, Start-menu styling and much more. Install it, launch it, and browse a curated gallery below.",
            "Windhawk 係一個開源平台，透過注入社群「mod」嚟自訂 Windows — 工作列高度、時鐘調校、開始功能表美化等等。喺下面安裝、啟動，再瀏覽精選 mod。");

        _mods ??= WindhawkMods.All();
        GalleryHeader.Text = P($"Popular mods ({_mods.Count})", $"熱門 mod（{_mods.Count}）");
        GalleryHint.Text = P(
            "Each row opens the mod's page on windhawk.net. Click \"Open in Windhawk\" then install/configure it inside the Windhawk app — programmatic toggling is intentionally left to Windhawk itself.",
            "每一行會開啟 windhawk.net 上嗰個 mod 嘅頁面。撳「喺 Windhawk 開」之後喺 Windhawk app 內安裝／設定 — 啟用／停用刻意交返畀 Windhawk 本身。");
        ModFilter.PlaceholderText = P("Filter mods…", "篩選 mod…");

        AboutHeader.Text = P("How it works & where files live", "運作原理同檔案位置");
        AboutBody.Text = P(
            "Windhawk runs an elevated service that compiles mods and injects them into target processes (explorer.exe, the taskbar, etc.). Installing it via winget may prompt UAC. Mod settings live under %ProgramData%\\Windhawk\\Engine. WinForge installs and launches the official binary and deep-links to its catalog — it does not fork or bundle Windhawk (GPL-3.0).",
            "Windhawk 會行一個提權服務，編譯 mod 並注入目標程序（explorer.exe、工作列等）。經 winget 安裝時可能彈 UAC。Mod 設定喺 %ProgramData%\\Windhawk\\Engine。WinForge 只係安裝同啟動官方版本並深層連結到佢嘅目錄 — 並無 fork 或內嵌 Windhawk（GPL-3.0）。");

        LaunchBtn.Content = P("Launch Windhawk", "啟動 Windhawk");
        ModsFolderBtn.Content = P("Open mods folder", "開啟 mod 資料夾");
        SiteBtn.Content = P("Browse all mods (windhawk.net)", "瀏覽全部 mod（windhawk.net）");
        RenderStatus();
    }

    private void RenderStatus()
    {
        bool installed = WindhawkService.IsInstalled();
        if (installed)
        {
            var ver = WindhawkService.Version();
            StatusText.Text = ver is null
                ? P("Windhawk is installed.", "Windhawk 已安裝。")
                : P($"Windhawk is installed (version {ver}).", $"Windhawk 已安裝（版本 {ver}）。");
        }
        else
        {
            StatusText.Text = P("Windhawk is not installed yet — install it from the bar above.",
                "尚未安裝 Windhawk — 喺上方安裝列安裝。");
        }
        LaunchBtn.IsEnabled = installed;
        ModsFolderBtn.IsEnabled = WindhawkService.EngineFolder() is not null;
    }

    private async Task CheckEngine()
    {
        bool ok = await WindhawkService.IsInstalledAsync();
        RenderStatus();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Windhawk not found", "搵唔到 Windhawk");
        EngineBar.Message = P(
            "Click to install Windhawk automatically (winget). It installs an elevated service, so Windows may prompt for UAC.",
            "撳一下自動安裝 Windhawk（winget）。佢會安裝一個提權服務，所以 Windows 可能會彈 UAC。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            WindhawkService.WingetId, "Install Windhawk automatically", "自動安裝 Windhawk",
            async () => { await CheckEngine(); }, WindhawkService.Rescan);
    }

    // ── Primary action buttons ───────────────────────────────────────────────────

    private void Launch_Click(object sender, RoutedEventArgs e)
        => ShowResult(WindhawkService.Launch());

    private void ModsFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = WindhawkService.EngineFolder();
        if (folder is null)
        {
            ShowResult(TweakResult.Fail(
                "Windhawk's engine folder was not found — launch Windhawk once so it creates it.",
                "搵唔到 Windhawk 引擎資料夾 — 先啟動一次 Windhawk 等佢建立。"));
            return;
        }
        ShowResult(WindhawkService.OpenFolder(folder));
    }

    private void Site_Click(object sender, RoutedEventArgs e)
        => ShowResult(WindhawkService.OpenUrl(WindhawkService.Homepage));

    // ── Mod gallery ──────────────────────────────────────────────────────────────

    private void ModFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateMods(sender.Text ?? string.Empty);
    }

    private void PopulateMods(string filter)
    {
        _mods ??= WindhawkMods.All();
        ModsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _mods;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _mods.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
        {
            if (!first) ModsPanel.Children.Add(BuildDivider());
            first = false;
            ModsPanel.Children.Add(BuildRow(op));
        }
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
        TweakKind.RadioGroup => BuildChoice(op),
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
                ShowResult(result);
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

    // ---------------- Choice / RadioGroup → ComboBox ----------------
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

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        string Format(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                               : v.ToString("0.###", CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = op.Min, Maximum = op.Max, StepFrequency = op.Step, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueText.Text = Format(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = Format(e.NewValue);
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
        panel.Children.Add(valueText);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));
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
        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(e.NewValue)) return;
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

    // ---------------- Applied / error status (routes through the persistent StatusBar) ----------------
    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ShowStatus(P(en, zh), InfoBarSeverity.Success);
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ShowStatus(needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message, InfoBarSeverity.Error);
    }

    // ── Status helpers ──────────────────────────────────────────────────────────────

    private void ShowResult(TweakResult r)
        => ShowStatus(Loc.I.IsCantonesePrimary ? (r.Message?.Zh ?? "") : (r.Message?.En ?? ""),
            r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        StatusBar.IsOpen = true;
        StatusBar.Severity = severity;
        StatusBar.Message = message;
    }
}
