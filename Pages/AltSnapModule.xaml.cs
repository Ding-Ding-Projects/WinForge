using System;
using System.Collections.Generic;
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
/// AltSnap · Alt 拖曳視窗 · AltSnap manager module — install/detect the official RamonUnch.AltSnap binary,
/// launch / quit / restart / reload its hook (optionally elevated to control admin windows), toggle
/// run-at-startup, and edit the high-value AltSnap.ini keys (modifier key, release actions, auto-snap,
/// drag-to-top maximize, snap threshold, transparency, snap layouts, multi-monitor, blacklists) from a
/// bilingual front-end. Import/export the config and a raw-edit fallback round things off. AltSnap is GPL —
/// WinForge only installs and drives the binary; it never relinks its code. Bilingual throughout.
/// </summary>
public sealed partial class AltSnapModule : Page
{
    private bool _installed;
    private bool _busy;
    private bool _suppress;

    /// <summary>每個選項對應嘅讀取器（畀儲存時用）· Per-option value readers, wired when each card is built.</summary>
    private readonly Dictionary<string, Func<string>> _readers = new();

    public AltSnapModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await DetectAndLoad(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "AltSnap · Alt 拖曳視窗";
        HeaderBlurb.Text = P(
            "Move and resize any window by holding a modifier key (Alt by default) and dragging anywhere inside it — classic Linux-style alt-drag. WinForge installs the official AltSnap, controls it, and edits its configuration.",
            "撳住一個修飾鍵（預設 Alt）就可以喺視窗任何位置拖動嚟移動同縮放 — 經典 Linux 式 alt 拖曳。WinForge 會安裝官方 AltSnap、控制佢、並編輯佢嘅設定。");

        InstallBtn.Content = P("Install via winget", "用 winget 安裝");
        RefreshBtn.Content = P("Refresh", "重新整理");

        LifecycleTitle.Text = P("AltSnap engine", "AltSnap 引擎");
        LaunchBtn.Content = P("Launch", "啟動");
        LaunchElevatedBtn.Content = P("Launch as admin", "以管理員啟動");
        QuitBtn.Content = P("Quit", "結束");
        RestartBtn.Content = P("Restart", "重啟");
        AdvancedBtn.Content = P("Open advanced settings", "開啟進階設定");

        StartupTitle.Text = P("Run at startup", "開機自啟動");
        StartupBlurb.Text = P("Launch AltSnap automatically when you sign in (hidden to the tray).",
            "登入時自動啟動 AltSnap（收入系統匣）。");
        ReloadOnSaveChk.Content = P("Reload AltSnap automatically after saving a setting",
            "儲存設定後自動重新載入 AltSnap");

        ConfigHeader.Text = P("Configuration", "設定");
        ConfigBlurb.Text = P("Edit the most-used AltSnap.ini keys. Changes need an AltSnap reload to take effect (use Restart / Reload, or tick the box above).",
            "編輯最常用嘅 AltSnap.ini 設定。變更要 AltSnap 重新載入先生效（用「重啟／重新載入」，或者剔上面個選項）。");

        ImportBtn.Content = P("Import config…", "匯入設定…");
        ExportBtn.Content = P("Export config…", "匯出設定…");

        RawHeader.Text = P("Advanced: raw AltSnap.ini editor", "進階：原始 AltSnap.ini 編輯器");
        RawBlurb.Text = P("Full fallback — edit the entire AltSnap.ini by hand. Use this for keys not surfaced above.",
            "完整後備 — 親手編輯整個 AltSnap.ini。上面冇出嘅鍵可喺呢度改。");
        RawReloadBtn.Content = P("Reload from disk", "由磁碟重新載入");
        RawSaveBtn.Content = P("Save raw file", "儲存原始檔");

        BuildOptionCards();
    }

    private async Task DetectAndLoad()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            _installed = await AltSnapService.IsInstalled();
            if (!_installed)
            {
                InstallBar.Title = P("AltSnap is not installed", "未安裝 AltSnap");
                InstallBar.Message = P(
                    "AltSnap (RamonUnch.AltSnap) is required. Install it below, then it appears here. The install may prompt for UAC.",
                    "需要 AltSnap（RamonUnch.AltSnap）。喺下面安裝，之後就會喺度顯示。安裝可能會彈 UAC。");
                InstallBar.IsOpen = true;
                SetEnabled(false);
            }
            else
            {
                InstallBar.IsOpen = false;
                SetEnabled(true);
                await LoadConfigValues();
            }
            UpdateStatus();
            await UpdateVersion();
            CheckConflict();
            UpdateElevationHint();
            await UpdateRawPath();
        }
        finally { _busy = false; }
        SyncStartupSwitch();
    }

    private void SetEnabled(bool on)
    {
        LaunchBtn.IsEnabled = on;
        LaunchElevatedBtn.IsEnabled = on;
        QuitBtn.IsEnabled = on;
        RestartBtn.IsEnabled = on;
        AdvancedBtn.IsEnabled = on;
        StartupSwitch.IsEnabled = on;
        ReloadOnSaveChk.IsEnabled = on;
        ImportBtn.IsEnabled = on;
        ExportBtn.IsEnabled = on;
        RawExpander.IsEnabled = on;
        foreach (var opt in AltSnapOptions.All)
        {
            foreach (var child in OptionsHost.Children)
            {
                if (FindEditor(child, opt.Id) is Control c) { c.IsEnabled = on; break; }
            }
        }
    }

    private void UpdateStatus()
    {
        if (!_installed)
            StatusText.Text = P("Not installed.", "未安裝。");
        else if (AltSnapService.IsRunning())
            StatusText.Text = P("Installed · running ✓", "已安裝 · 執行中 ✓");
        else
            StatusText.Text = P("Installed · not running", "已安裝 · 未執行");
    }

    private async Task UpdateVersion()
    {
        var v = await AltSnapService.VersionAsync();
        VersionText.Text = string.IsNullOrWhiteSpace(v) ? "" : P($"Version {v}", $"版本 {v}");
    }

    private void CheckConflict()
    {
        var conflict = AltSnapService.DetectConflict();
        if (conflict is null) { ConflictBar.IsOpen = false; return; }
        ConflictBar.Title = P("Conflicting tool detected", "偵測到衝突工具");
        ConflictBar.Message = P(
            $"{conflict} is running. Only one global mouse-hook owner works at a time — quit it before using AltSnap.",
            $"{conflict} 正在執行。同一時間只可以有一個全域滑鼠 hook 擁有者 — 用 AltSnap 前請先關閉佢。");
        ConflictBar.IsOpen = true;
    }

    private void UpdateElevationHint()
    {
        if (!_installed) { ElevationBar.IsOpen = false; return; }
        ElevationBar.Title = P("Tip: elevation", "提示：權限");
        ElevationBar.Message = P(
            "To move or resize windows that run as administrator, launch AltSnap as admin (UAC will prompt).",
            "若要移動或縮放以管理員身分執行嘅視窗，請以管理員身分啟動 AltSnap（會彈 UAC）。");
        ElevationBar.IsOpen = true;
    }

    private void SyncStartupSwitch()
    {
        _suppress = true;
        StartupSwitch.IsOn = AltSnapService.IsRunAtStartupEnabled();
        _suppress = false;
    }

    // ===================== option cards =====================

    private void BuildOptionCards()
    {
        OptionsHost.Children.Clear();
        _readers.Clear();
        foreach (var opt in AltSnapOptions.All)
            OptionsHost.Children.Add(BuildCard(opt));
    }

    private Border BuildCard(AltSnapOption opt)
    {
        var card = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
        };
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var secondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var labels = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        labels.Children.Add(new TextBlock { Text = $"{opt.En} · {opt.Zh}", FontWeight = FontWeights.SemiBold, FontSize = 14, TextWrapping = TextWrapping.Wrap });
        labels.Children.Add(new TextBlock { Text = P(opt.EnDesc, opt.ZhDesc), FontSize = 12, Foreground = secondary, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(labels, 0);
        grid.Children.Add(labels);

        FrameworkElement editor = opt.Kind switch
        {
            AltSnapOptionKind.Toggle => BuildToggleEditor(opt),
            AltSnapOptionKind.Choice => BuildChoiceEditor(opt),
            AltSnapOptionKind.Number => BuildNumberEditor(opt),
            _ => BuildTextEditor(opt),
        };
        editor.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(editor, 1);
        grid.Children.Add(editor);

        // Text editor is wide — put it under the labels instead of beside them.
        if (opt.Kind == AltSnapOptionKind.Text)
        {
            grid.ColumnDefinitions[1].Width = new GridLength(0);
            editor.Visibility = Visibility.Collapsed;
            var outer = new StackPanel { Spacing = 8 };
            outer.Children.Add(grid);
            var tb = (TextBox)editor;
            tb.Visibility = Visibility.Visible;
            tb.HorizontalAlignment = HorizontalAlignment.Stretch;
            outer.Children.Add(tb);
            card.Child = outer;
            return card;
        }

        card.Child = grid;
        return card;
    }

    private FrameworkElement BuildToggleEditor(AltSnapOption opt)
    {
        var sw = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        sw.Tag = opt;
        _readers[opt.Id] = () => sw.IsOn ? "1" : "0";
        sw.Toggled += (_, _) => { if (!_suppress) _ = SaveOption(opt, _readers[opt.Id]()); };
        return sw;
    }

    private FrameworkElement BuildChoiceEditor(AltSnapOption opt)
    {
        var combo = new ComboBox { MinWidth = 200 };
        foreach (var c in opt.Choices)
            combo.Items.Add(new ComboBoxItem { Content = $"{c.En} · {c.Zh}", Tag = c.Value });
        combo.Tag = opt;
        _readers[opt.Id] = () => (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? opt.Default;
        combo.SelectionChanged += (_, _) => { if (!_suppress) _ = SaveOption(opt, _readers[opt.Id]()); };
        return combo;
    }

    private FrameworkElement BuildNumberEditor(AltSnapOption opt)
    {
        var box = new NumberBox
        {
            Minimum = opt.Min,
            Maximum = opt.Max,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 160,
        };
        box.Tag = opt;
        _readers[opt.Id] = () => double.IsNaN(box.Value) ? opt.Default : ((int)box.Value).ToString();
        box.ValueChanged += (_, _) => { if (!_suppress) _ = SaveOption(opt, _readers[opt.Id]()); };
        return box;
    }

    private FrameworkElement BuildTextEditor(AltSnapOption opt)
    {
        var box = new TextBox { AcceptsReturn = false, PlaceholderText = opt.Default, MinWidth = 200 };
        box.Tag = opt;
        _readers[opt.Id] = () => box.Text ?? "";
        // Save on focus-loss (avoid a write on every keystroke).
        box.LostFocus += (_, _) => { if (!_suppress) _ = SaveOption(opt, _readers[opt.Id]()); };
        return box;
    }

    private async Task LoadConfigValues()
    {
        _suppress = true;
        try
        {
            foreach (var opt in AltSnapOptions.All)
            {
                var val = await AltSnapService.ReadIni(opt.Section, opt.Key, opt.Default);
                ApplyValueToCard(opt, val);
            }
        }
        finally { _suppress = false; }
    }

    private void ApplyValueToCard(AltSnapOption opt, string val)
    {
        // Find the card's editor by walking the same construction order.
        // We rebuild against a tagged search through OptionsHost.
        foreach (var child in OptionsHost.Children)
        {
            var editor = FindEditor(child, opt.Id);
            if (editor is null) continue;
            switch (opt.Kind)
            {
                case AltSnapOptionKind.Toggle when editor is ToggleSwitch sw:
                    sw.IsOn = val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case AltSnapOptionKind.Choice when editor is ComboBox cb:
                    SelectComboValue(cb, opt, val);
                    break;
                case AltSnapOptionKind.Number when editor is NumberBox nb:
                    nb.Value = int.TryParse(val, out var n) ? n : (int.TryParse(opt.Default, out var d) ? d : 0);
                    break;
                case AltSnapOptionKind.Text when editor is TextBox tb:
                    tb.Text = val;
                    break;
            }
            return;
        }
    }

    private static void SelectComboValue(ComboBox cb, AltSnapOption opt, string val)
    {
        for (int i = 0; i < cb.Items.Count; i++)
            if (cb.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, val, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedIndex = i;
                return;
            }
        // Fall back to the default if the stored value isn't one of our curated choices.
        for (int i = 0; i < cb.Items.Count; i++)
            if (cb.Items[i] is ComboBoxItem item && string.Equals(item.Tag as string, opt.Default, StringComparison.OrdinalIgnoreCase))
            {
                cb.SelectedIndex = i;
                return;
            }
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }

    private static FrameworkElement? FindEditor(object node, string optId)
    {
        if (node is FrameworkElement fe && fe.Tag is AltSnapOption o && o.Id == optId) return fe;
        if (node is Border b && b.Child is not null) return FindEditor(b.Child, optId);
        if (node is Panel p)
        {
            foreach (var c in p.Children)
            {
                var hit = FindEditor(c, optId);
                if (hit is not null) return hit;
            }
        }
        if (node is Grid g)
        {
            foreach (var c in g.Children)
            {
                var hit = FindEditor(c, optId);
                if (hit is not null) return hit;
            }
        }
        return null;
    }

    private async Task SaveOption(AltSnapOption opt, string value)
    {
        var r = await AltSnapService.WriteIni(opt.Section, opt.Key, value);
        if (!r.Success)
        {
            ShowResult(r);
            return;
        }
        if (ReloadOnSaveChk.IsChecked == true)
            await AltSnapService.ReloadSettings(elevated: false);
        ShowOk(P($"Saved: {opt.En}", $"已儲存：{opt.Zh}"),
            ReloadOnSaveChk.IsChecked == true
                ? P("AltSnap reloaded.", "AltSnap 已重新載入。")
                : P("Restart or reload AltSnap for it to take effect.", "重啟或重新載入 AltSnap 後生效。"));
        await UpdateRawPath();
    }

    // ===================== lifecycle handlers =====================

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await DetectAndLoad();

    private async void Launch_Click(object sender, RoutedEventArgs e) => await RunLifecycle(() => AltSnapService.Launch(elevated: false));

    private async void LaunchElevated_Click(object sender, RoutedEventArgs e) => await RunLifecycle(() => AltSnapService.Launch(elevated: true));

    private async void Quit_Click(object sender, RoutedEventArgs e) => await RunLifecycle(() => AltSnapService.Quit());

    private async void Restart_Click(object sender, RoutedEventArgs e) => await RunLifecycle(() => AltSnapService.Restart(elevated: false));

    private async void Advanced_Click(object sender, RoutedEventArgs e) => await RunLifecycle(() => AltSnapService.OpenAdvancedSettings());

    private async Task RunLifecycle(Func<Task<TweakResult>> op)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await op();
            ShowResult(r);
        }
        finally { _busy = false; }
        UpdateStatus();
        CheckConflict();
    }

    private async void Startup_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        var r = await AltSnapService.SetRunAtStartup(StartupSwitch.IsOn);
        ShowResult(r);
        if (!r.Success) SyncStartupSwitch();
    }

    // ===================== import / export =====================

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".ini", ".txt");
        if (path is null) return;
        var r = await AltSnapService.ImportIni(path);
        ShowResult(r);
        if (r.Success) { await LoadConfigValues(); await UpdateRawPath(); }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("AltSnap", ".ini", ".txt");
        if (path is null) return;
        var r = await AltSnapService.ExportIni(path);
        ShowResult(r);
    }

    // ===================== raw editor =====================

    private async Task UpdateRawPath()
    {
        var path = await AltSnapService.IniPathAsync(forWrite: true);
        RawPathText.Text = path is null ? "" : P($"File: {path}", $"檔案：{path}");
        // Only fill the raw box if the user hasn't started editing it.
        if (string.IsNullOrEmpty(RawBox.Text))
            RawBox.Text = await AltSnapService.ReadRaw();
    }

    private async void RawReload_Click(object sender, RoutedEventArgs e)
    {
        RawBox.Text = await AltSnapService.ReadRaw();
        await UpdateRawPath();
        ShowOk(P("Reloaded", "已重新載入"), P("AltSnap.ini reloaded from disk.", "已由磁碟重新載入 AltSnap.ini。"));
    }

    private async void RawSave_Click(object sender, RoutedEventArgs e)
    {
        var r = await AltSnapService.WriteRaw(RawBox.Text ?? "");
        ShowResult(r);
        if (r.Success)
        {
            await LoadConfigValues();
            if (ReloadOnSaveChk.IsChecked == true)
                await AltSnapService.ReloadSettings(elevated: false);
        }
    }

    // ===================== install =====================

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        InstallBtn.IsEnabled = false;
        InstallBtn.Content = P("Installing…", "安裝緊…");
        bool ok;
        try { ok = await AltSnapService.InstallViaWinget(); }
        catch { ok = false; }
        InstallBtn.Content = ok ? P("Installed ✓", "已安裝 ✓") : P("Install failed — retry", "安裝失敗 — 再試");
        InstallBtn.IsEnabled = !ok;
        _busy = false;
        if (ok) await DetectAndLoad();
    }

    // ===================== feedback =====================

    private void ShowResult(TweakResult r)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = r.Message is null ? "" : $"{r.Message.En}\n{r.Message.Zh}";
        ResultBar.IsOpen = true;
    }

    private void ShowOk(string title, string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
