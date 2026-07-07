using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 壓縮模組 · Archives module: wraps 7-Zip for create/extract/list/test plus ~100 advanced operations.
/// </summary>
public sealed partial class ArchivesModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _busy;

    public ArchivesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildQuickOps();
        PopulateOps(string.Empty);
        RefreshSelection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Fix the leak: unsubscribe the named handler when the page is torn down.
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) { Render(); BuildQuickOps(); PopulateOps(OpsFilter.Text ?? string.Empty); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Archives · 壓縮檔";
        HeaderBlurb.Text = P("Create, extract, list and test archives with 7-Zip — zip, 7z, tar, gzip, bzip2, xz and more.",
            "用 7-Zip 建立、解壓、列出同測試壓縮檔 — zip、7z、tar、gzip、bzip2、xz 等等。");
        SelLabel.Text = P("Selection", "選擇");
        ArcCap.Text = P("Archive", "壓縮檔");
        SrcCap.Text = P("Source", "來源");
        OpenArcBtn.Content = P("Open…", "開啟…");
        NewArcBtn.Content = P("New…", "新建…");
        SrcFileBtn.Content = P("File…", "檔案…");
        SrcFolderBtn.Content = P("Folder…", "資料夾…");
        CreateLabel.Text = P("Create archive (format · level · password · options)", "建立壓縮檔（格式 · 等級 · 密碼 · 選項）");
        PasswordBox.PlaceholderText = P("Optional password…", "可選密碼…");
        VolumeBox.PlaceholderText = P("Split volumes, e.g. 100m", "分卷，例如 100m");
        SfxCheck.Content = P("Self-extracting .exe", "自解壓 .exe");
        HeaderEncCheck.Content = P("Encrypt file names", "加密檔名");
        SolidCheck.Content = P("Solid", "實體壓縮");
        MtCheck.Content = P("Multi-thread", "多執行緒");
        RarNote.Text = P("Tip: 7-Zip can create 7z, zip, tar, gzip, bzip2, xz and wim — but not .rar. Self-extracting, solid and encrypt-file-names apply to 7z only.",
            "提示：7-Zip 可以整 7z、zip、tar、gzip、bzip2、xz、wim — 但係整唔到 .rar。自解壓、實體同加密檔名淨係 7z 先用得。");
        CreateBtn.Content = P("Create", "建立");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
        AdvancedHeader.Text = P($"Advanced operations ({GitOpsCount()})", $"進階操作（{GitOpsCount()}）");

        if (!ArchiveService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("7-Zip not found", "搵唔到 7-Zip");
            EngineBar.Message = P("Install 7-Zip automatically (winget) — live progress below, no restart needed.", "自動安裝 7-Zip（winget）— 下面有即時進度，唔使重開。");
            EngineBar.ActionButton = null;
            // Rich install-progress control: real bar + live bilingual winget status + % + Cancel +
            // flashy success/error animation, surfacing the real exit code on failure.
            EngineInstallHost.Children.Clear();
            EngineInstallHost.Children.Add(EngineBars.AutoInstallProgress(
                "7zip.7zip", "Install 7-Zip automatically", "自動安裝 7-Zip",
                recheck: () => { Render(); return Task.CompletedTask; },
                rescan: ArchiveService.Rescan));
        }
        else { EngineBar.IsOpen = false; EngineBar.ActionButton = null; EngineInstallHost.Children.Clear(); }
    }

    private int GitOpsCount() => (_ops ??= ArchiveOperations.All().ToList()).Count;

    private void RefreshSelection()
    {
        ArchiveBox.Text = AppState.CurrentArchivePath;
        SourceBox.Text = AppState.CurrentSourcePath;
    }

    private void BuildQuickOps()
    {
        QuickOps.Children.Clear();
        AddQuick(P("List", "列出"), () => ArchiveService.List());
        AddQuick(P("Test", "測試"), () => ArchiveService.Test());
        AddQuick(P("Extract here", "解壓到旁邊"), () => ArchiveService.ExtractHere());
        AddQuick(P("Benchmark", "效能測試"), () => ArchiveService.Benchmark());
    }

    private void AddQuick(string label, Func<Task<TweakResult>> run)
    {
        var btn = new Button { Content = label };
        btn.Click += async (_, _) => await RunAndShow(btn, run);
        QuickOps.Children.Add(btn);
    }

    private async Task RunAndShow(Button btn, Func<Task<TweakResult>> run)
    {
        if (_busy) return;
        _busy = true;
        var label = btn.Content;
        btn.IsEnabled = false;
        btn.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = P("Running…", "執行緊…");
        try
        {
            var r = await run();
            var head = r.Success ? P("✓ Done", "✓ 完成") : P("✗ Failed", "✗ 失敗");
            OutText.Text = head + "\n" + (string.IsNullOrWhiteSpace(r.Output)
                ? ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "")
                : r.Output);
        }
        catch (Exception ex) { OutText.Text = ex.Message; }
        finally { btn.Content = label; btn.IsEnabled = true; _busy = false; RefreshSelection(); }
    }

    // ---- pickers ----
    private async void OpenArchive_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".7z", ".zip", ".rar", ".tar", ".gz", ".bz2", ".xz", ".cab", ".iso", ".wim");
        if (path is not null) { AppState.CurrentArchivePath = path; RefreshSelection(); }
    }

    private async void NewArchive_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("archive", ".7z", ".zip", ".tar");
        if (path is not null) { AppState.CurrentArchivePath = path; RefreshSelection(); }
    }

    private async void SourceFile_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (path is not null) { AppState.CurrentSourcePath = path; RefreshSelection(); }
    }

    private async void SourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFolderAsync();
        if (path is not null) { AppState.CurrentSourcePath = path; RefreshSelection(); }
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var fmt = (FormatBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "7z";
        var level = int.Parse((LevelBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "5");
        var pwd = PasswordBox.Password;
        var vol = VolumeBox.Text;
        bool sfx = SfxCheck.IsChecked == true;
        bool hdr = HeaderEncCheck.IsChecked == true;
        bool solid = SolidCheck.IsChecked == true;
        bool mt = MtCheck.IsChecked == true;
        await RunAndShow(CreateBtn, () => ArchiveService.Create(
            fmt, level, string.IsNullOrEmpty(pwd) ? null : pwd,
            encryptHeader: hdr, solid: solid, multithread: mt, sfx: sfx,
            volumeSize: string.IsNullOrWhiteSpace(vol) ? null : vol));
    }

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= ArchiveOperations.All().ToList();
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
            if (!first) OpsPanel.Children.Add(MakeDivider());
            OpsPanel.Children.Add(BuildOpRow(op));
            first = false;
        }
    }

    // ================================================================
    //  Hand-built control rows (replaces TweakCard) · 手砌控件列
    //  Each tweak → one Grid: bilingual title/description on the left,
    //  the matching WinUI control on the right. No card chrome.
    // ================================================================

    private Border MakeDivider() => new Border
    {
        Height = 1,
        Margin = new Thickness(0, 8, 0, 8),
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
    };

    private Grid BuildOpRow(TweakDefinition def)
    {
        var row = new Grid { Padding = new Thickness(2, 6, 2, 6), ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // ---- Left: bilingual title + description (Loc.I.Pick) ----
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var title = new TextBlock
        {
            Text = P(def.Title.En, def.Title.Zh),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        text.Children.Add(title);
        var descText = P(def.Description.En, def.Description.Zh);
        if (!string.IsNullOrWhiteSpace(descText))
        {
            text.Children.Add(new TextBlock
            {
                Text = descText,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
        }
        Grid.SetColumn(text, 0);
        row.Children.Add(text);

        // ---- Right: the matching control for this kind ----
        var control = BuildControl(def);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    /// <summary>把一個 TweakDefinition 揀啱嘅控件砌出嚟 · Build the right control for a tweak's Kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition def)
    {
        switch (def.Kind)
        {
            case TweakKind.Toggle: return BuildToggle(def);
            case TweakKind.Choice: return BuildChoice(def);
            case TweakKind.RadioGroup: return BuildChoice(def); // reuses Choices; ComboBox is fine here
            case TweakKind.Slider: return BuildSlider(def);
            case TweakKind.Number: return BuildNumber(def);
            case TweakKind.Info: return BuildInfo(def);
            case TweakKind.Action:
            case TweakKind.Wizard:
            default:
                return BuildAction(def);
        }
    }

    // ---------------- Action → Button ----------------
    private Button BuildAction(TweakDefinition def)
    {
        var label = def.ActionLabel is not null ? P(def.ActionLabel.En, def.ActionLabel.Zh) : P("Run", "執行");
        var btn = new Button { MinWidth = 110, Content = label };
        ToolTipService.SetToolTip(btn, def.ActionLabel is null ? label : $"{def.ActionLabel.En} · {def.ActionLabel.Zh}");
        btn.Click += async (_, _) => await RunOp(btn, def, label);
        return btn;
    }

    private async Task RunOp(Button btn, TweakDefinition def, object label)
    {
        if (_busy || def.RunAsync is null) return;
        if (def.Destructive && !await ConfirmOp(def)) return;

        _busy = true;
        btn.IsEnabled = false;
        btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
        OpsResultBar.IsOpen = false;
        OpsOutBorder.Visibility = Visibility.Collapsed;
        try
        {
            var r = await def.RunAsync(CancellationToken.None);
            OpsResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            OpsResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            OpsResultBar.Message = r.Message is null ? string.Empty : (Loc.I.IsCantonesePrimary ? r.Message.Zh : r.Message.En);
            OpsResultBar.IsOpen = true;

            if (!string.IsNullOrWhiteSpace(r.Output))
            {
                OpsOutText.Text = r.Output;
                OpsOutBorder.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            OpsResultBar.Severity = InfoBarSeverity.Error;
            OpsResultBar.Title = P("Failed", "失敗");
            OpsResultBar.Message = ex.Message;
            OpsResultBar.IsOpen = true;
        }
        finally
        {
            btn.Content = label;
            btn.IsEnabled = true;
            _busy = false;
            RefreshSelection();
        }
    }

    private async Task<bool> ConfirmOp(TweakDefinition def)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{def.Title.En}\n{def.Title.Zh}\n\n" + P("This action may be hard to undo.", "呢個動作可能難以復原。"),
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private ToggleSwitch BuildToggle(TweakDefinition def)
    {
        var ts = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { ts.IsOn = def.GetIsOn?.Invoke() ?? false; } catch { /* show off */ }
        suppress = false;
        ts.Toggled += (_, _) =>
        {
            if (suppress || def.SetIsOn is null) return;
            try { def.SetIsOn(ts.IsOn); ShowApplied(def); }
            catch (Exception ex)
            {
                suppress = true;
                try { ts.IsOn = def.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowOpError(ex);
            }
        };
        return ts;
    }

    // ---------------- Choice / RadioGroup → ComboBox ----------------
    private ComboBox BuildChoice(TweakDefinition def)
    {
        var cb = new ComboBox { MinWidth = 170 };
        if (def.Choices is not null)
            foreach (var c in def.Choices)
                cb.Items.Add(new ComboBoxItem { Content = P(c.Label.En, c.Label.Zh), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = def.GetCurrentChoice?.Invoke();
            if (cur is not null && def.Choices is not null)
                for (int i = 0; i < def.Choices.Count; i++)
                    if (string.Equals(def.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { cb.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        cb.SelectionChanged += (_, _) =>
        {
            if (suppress || def.SetChoice is null) return;
            if (cb.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { def.SetChoice(val); ShowApplied(def); }
                catch (Exception ex) { ShowOpError(ex); }
            }
        };
        return cb;
    }

    // ---------------- Slider → Slider + live value ----------------
    private FrameworkElement BuildSlider(TweakDefinition def)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = def.Min, Maximum = def.Max, StepFrequency = def.Step, Width = 160 };
        var valueLabel = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        string Fmt(double v)
        {
            bool whole = def.Step >= 1 && Math.Abs(def.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.###", CultureInfo.InvariantCulture);
            return def.Unit is null ? num : $"{num} {def.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(def.Min, Math.Min(def.Max, v));

        bool suppress = true;
        try { slider.Value = Clamp(def.GetNumber?.Invoke() ?? def.Min); } catch { slider.Value = def.Min; }
        suppress = false;
        valueLabel.Text = Fmt(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueLabel.Text = Fmt(e.NewValue);
            if (suppress || def.SetNumber is null) return;
            try { def.SetNumber(e.NewValue); ShowApplied(def); }
            catch (Exception ex)
            {
                ShowOpError(ex);
                suppress = true;
                try { slider.Value = Clamp(def.GetNumber?.Invoke() ?? def.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueLabel);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private NumberBox BuildNumber(TweakDefinition def)
    {
        var nb = new NumberBox
        {
            Minimum = def.Min,
            Maximum = def.Max,
            SmallChange = def.Step,
            LargeChange = def.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        double Clamp(double v) => Math.Max(def.Min, Math.Min(def.Max, v));
        bool suppress = true;
        try { nb.Value = Clamp(def.GetNumber?.Invoke() ?? def.Min); } catch { nb.Value = def.Min; }
        suppress = false;
        nb.ValueChanged += (_, e) =>
        {
            if (suppress || def.SetNumber is null || double.IsNaN(e.NewValue)) return;
            try { def.SetNumber(e.NewValue); ShowApplied(def); }
            catch (Exception ex)
            {
                ShowOpError(ex);
                suppress = true;
                try { nb.Value = Clamp(def.GetNumber?.Invoke() ?? def.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return nb;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition def)
    {
        string Safe() { try { return def.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }
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

    // ---------------- Shared status surfacing (one persistent bar) ----------------
    private void ShowApplied(TweakDefinition def)
    {
        OpsResultBar.Severity = InfoBarSeverity.Success;
        OpsResultBar.Title = P("Done", "完成");
        string en = "Applied.", zh = "已套用。";
        switch (def.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        OpsResultBar.Message = P(en, zh);
        OpsResultBar.IsOpen = true;
    }

    private void ShowOpError(Exception ex)
    {
        OpsResultBar.Severity = InfoBarSeverity.Error;
        OpsResultBar.Title = P("Failed", "失敗");
        OpsResultBar.Message = ex.Message;
        OpsResultBar.IsOpen = true;
    }
}
