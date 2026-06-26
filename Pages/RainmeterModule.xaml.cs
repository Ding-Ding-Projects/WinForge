using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Rainmeter 桌面小工具管理 · In-app Rainmeter front-end. Detects/installs Rainmeter (winget), scans the
/// Skins folder for every config, shows active vs inactive, loads/unloads/toggles/refreshes/shows/hides
/// and edits skins via !bangs against the live instance, installs <c>.rmskin</c> packs (FileDialogs →
/// SkinInstaller.exe), loads saved layouts, links a curated skin-pack list, and runs global one-shot ops.
/// State is re-read from Rainmeter.ini after every change rather than trusting bang exit codes. Bilingual.
/// </summary>
public sealed partial class RainmeterModule : Page
{
    private readonly RainmeterService _rm = new();
    private readonly ObservableCollection<RainmeterSkin> _skins = new();
    private List<RainmeterSkin> _allSkins = new();
    private List<TweakDefinition>? _ops;
    private string _filter = "";
    private bool _onlyActive;
    private bool _suppressToggle; // guard while we rebuild the list (avoids re-firing Toggled)

    public RainmeterModule()
    {
        InitializeComponent();
        SkinList.ItemsSource = _skins;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) =>
        {
            Render();
            BuildPacks();
            PopulateOps("");
            await CheckEngine();
            await ReloadSkins();
            ReloadLayouts();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildPacks();
        PopulateOps(OpsFilter.Text ?? "");
        ApplyFilter();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Rainmeter Widgets · Rainmeter 桌面小工具";
        HeaderBlurb.Text = P(
            "Install and manage Rainmeter — the desktop-widget engine. Scan installed skins, load/unload and toggle them, show/hide/refresh/edit individual widgets, install .rmskin packs, switch layouts and run global operations. WinForge drives the Rainmeter binary; nothing is reimplemented.",
            "安裝同管理 Rainmeter — 桌面小工具引擎。掃描已裝皮膚、載入／卸載／切換，逐個顯示／隱藏／重新整理／編輯小工具，安裝 .rmskin 皮膚包，切換版面配置，仲可以跑全域操作。WinForge 操控 Rainmeter 程式本體，唔會重寫。");

        TabSkins.Header = P("Skins · 皮膚", "皮膚");
        TabLayouts.Header = P("Layouts · 版面配置", "版面配置");
        TabPacks.Header = P("Skin packs · 皮膚包", "皮膚包");
        TabOps.Header = P("Operations · 操作", "操作");

        SkinsBlurb.Text = P(
            "Each row is one skin config (.ini) found under your Skins folder. The switch loads or unloads it; the buttons show, hide, refresh or edit the running widget.",
            "每一行係 Skins 資料夾入面搵到嘅一個皮膚設定（.ini）。開關用嚟載入或卸載；按鈕用嚟顯示、隱藏、重新整理或編輯運行中嘅小工具。");
        RescanBtn.Content = P("Rescan skins · 重新掃描", "重新掃描");
        RefreshAllBtn.Content = P("Refresh all · 全部重整", "全部重整");
        InstallRmskinBtn.Content = P("Install .rmskin… · 安裝 .rmskin…", "安裝 .rmskin…");
        OnlyActiveToggle.Header = P("Only loaded · 只顯示已載入", "只顯示已載入");
        SkinFilter.PlaceholderText = P("Filter skins…", "篩選皮膚…");

        LayoutsBlurb.Text = P(
            "Load a saved Rainmeter layout (a full set of widgets + positions saved from Rainmeter's Manage window).",
            "載入已儲存嘅 Rainmeter 版面配置（喺 Rainmeter 管理視窗儲存嘅一整套小工具同位置）。");
        LoadLayoutBtn.Content = P("Load layout · 載入配置", "載入配置");
        ReloadLayoutsBtn.Content = P("Reload list · 重載清單", "重載清單");

        PacksBlurb.Text = P(
            "A hand-picked starter list of popular skin suites. Open a page, download its .rmskin, then install it here. (v1 has no in-app online catalog — that's a later feature.)",
            "精選嘅熱門皮膚套裝清單。開啟頁面、下載佢嘅 .rmskin，再喺呢度安裝。（v1 未有內建線上目錄 — 屬後續功能。）");
        InstallRmskin2Btn.Content = P("Install a .rmskin file… · 安裝 .rmskin 檔…", "安裝 .rmskin 檔…");

        OpsBlurb.Text = P("Global one-shot operations against the Rainmeter engine.", "對 Rainmeter 引擎嘅全域一鍵操作。");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");

        UpdateCount();
    }

    // ── Engine detection / install ─────────────────────────────────────────────

    private async Task CheckEngine()
    {
        await Task.Yield();
        if (_rm.IsInstalled) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Rainmeter not found", "搵唔到 Rainmeter");
        EngineBar.Message = P("Click to install Rainmeter automatically (winget) — no restart needed.",
            "撳一下自動安裝 Rainmeter（winget）— 唔使重開。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            RainmeterService.WingetId, "Install Rainmeter", "安裝 Rainmeter",
            async () => { await CheckEngine(); await ReloadSkins(); ReloadLayouts(); },
            () => _rm.Rescan());
    }

    // ── Skins ──────────────────────────────────────────────────────────────────

    private async Task ReloadSkins()
    {
        SkinsBusy.IsActive = true;
        try
        {
            _allSkins = await Task.Run(() => _rm.EnumerateSkins());
        }
        catch { _allSkins = new(); }
        finally { SkinsBusy.IsActive = false; }
        ApplyFilter();
        if (_allSkins.Count == 0 && _rm.IsInstalled)
            ShowStatus(P("No skins found. Install a .rmskin pack or open the Skins folder to add some.",
                "搵唔到皮膚。安裝一個 .rmskin 皮膚包，或開啟 Skins 資料夾加入皮膚。"), InfoBarSeverity.Informational);
    }

    private void ApplyFilter()
    {
        _suppressToggle = true;
        _skins.Clear();
        IEnumerable<RainmeterSkin> shown = _allSkins;
        if (_onlyActive) shown = shown.Where(s => s.Active);
        if (!string.IsNullOrWhiteSpace(_filter))
        {
            var f = _filter.Trim().ToLowerInvariant();
            shown = shown.Where(s => s.Config.ToLowerInvariant().Contains(f) || s.File.ToLowerInvariant().Contains(f));
        }
        foreach (var s in shown) _skins.Add(s);
        _suppressToggle = false;
        UpdateCount();
    }

    private void UpdateCount()
    {
        int total = _allSkins.Count, active = _allSkins.Count(s => s.Active);
        SkinsCount.Text = P($"{total} skins · {active} loaded", $"{total} 個皮膚 · {active} 個已載入");
    }

    private async void Rescan_Click(object sender, RoutedEventArgs e) { _rm.Rescan(); await CheckEngine(); await ReloadSkins(); ReloadLayouts(); }

    private async void RefreshAll_Click(object sender, RoutedEventArgs e)
    {
        var r = await _rm.RefreshApp();
        ShowResult(r);
        await Task.Delay(500);
        await ReloadSkins();
    }

    private async void SkinFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _filter = sender.Text ?? "";
        await Task.Yield();
        ApplyFilter();
    }

    private void OnlyActive_Toggled(object sender, RoutedEventArgs e)
    {
        _onlyActive = OnlyActiveToggle.IsOn;
        ApplyFilter();
    }

    private async void SkinToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggle) return;
        if (sender is not ToggleSwitch { Tag: RainmeterSkin skin } sw) return;
        // Ignore the spurious Toggled that fires when a virtualized container's OneWay binding
        // materialises IsOn to match the current state — only act on a real user-initiated change.
        if (sw.IsOn == skin.Active) return;
        sw.IsEnabled = false;
        TweakResult r = sw.IsOn ? await _rm.ActivateConfig(skin) : await _rm.DeactivateConfig(skin);
        ShowResult(r);
        await Task.Delay(400);
        await ReloadSkins();        // re-read Rainmeter.ini rather than trusting the bang
        sw.IsEnabled = true;
    }

    private static RainmeterSkin? SkinOf(object sender) => (sender as FrameworkElement)?.Tag as RainmeterSkin;

    private async void SkinShow_Click(object sender, RoutedEventArgs e)
    { if (SkinOf(sender) is { } s) ShowResult(await _rm.ShowSkin(s)); }

    private async void SkinHide_Click(object sender, RoutedEventArgs e)
    { if (SkinOf(sender) is { } s) ShowResult(await _rm.HideSkin(s)); }

    private async void SkinRefresh_Click(object sender, RoutedEventArgs e)
    { if (SkinOf(sender) is { } s) ShowResult(await _rm.RefreshSkin(s)); }

    private async void SkinEdit_Click(object sender, RoutedEventArgs e)
    { if (SkinOf(sender) is { } s) ShowResult(await _rm.EditSkin(s)); }

    private async void InstallRmskin_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(
            new[] { new FileDialogs.Filter("Rainmeter skin pack (*.rmskin)", "*.rmskin"), new FileDialogs.Filter("All files", "*.*") },
            P("Choose a .rmskin pack", "揀一個 .rmskin 皮膚包"));
        if (string.IsNullOrEmpty(path)) return;
        var r = _rm.InstallSkinPack(path!);
        ShowResult(r);
    }

    // ── Layouts ─────────────────────────────────────────────────────────────────

    private void ReloadLayouts()
    {
        var layouts = _rm.EnumerateLayouts();
        LayoutBox.Items.Clear();
        foreach (var l in layouts) LayoutBox.Items.Add(l);
        if (layouts.Count > 0) LayoutBox.SelectedIndex = 0;
        LoadLayoutBtn.IsEnabled = layouts.Count > 0;
        if (layouts.Count == 0)
            LayoutBox.PlaceholderText = P("No saved layouts found", "搵唔到已儲存嘅版面配置");
    }

    private void ReloadLayouts_Click(object sender, RoutedEventArgs e) => ReloadLayouts();

    private async void LoadLayout_Click(object sender, RoutedEventArgs e)
    {
        if (LayoutBox.SelectedItem is not string name) return;
        var r = await _rm.LoadLayout(name);
        LayoutResult.IsOpen = true;
        LayoutResult.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        LayoutResult.Message = Loc.I.IsCantonesePrimary ? (r.Message?.Zh ?? "") : (r.Message?.En ?? "");
        await Task.Delay(700);
        await ReloadSkins();
    }

    // ── Curated skin packs ───────────────────────────────────────────────────────

    private void BuildPacks()
    {
        PacksPanel.Children.Clear();
        foreach (var pack in RainmeterOperations.CuratedPacks)
        {
            var border = new Border
            {
                Padding = new Thickness(14, 12, 14, 12),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
            };
            border.SetValue(Border.BackgroundProperty, Application.Current.Resources["CardBackgroundFillColorDefaultBrush"]);
            border.SetValue(Border.BorderBrushProperty, Application.Current.Resources["CardStrokeColorDefaultBrush"]);

            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = pack.Name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            info.Children.Add(new TextBlock
            {
                Text = P(pack.En, pack.Zh),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var open = new Button { Content = P("Open page", "開啟頁面"), VerticalAlignment = VerticalAlignment.Center };
            var url = pack.Url;
            open.Click += (_, _) => OpenUrl(url);
            Grid.SetColumn(open, 1);
            grid.Children.Add(open);

            border.Child = grid;
            PacksPanel.Children.Add(border);
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(url);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            ShowStatus(P("URL copied.", "已複製網址。"), InfoBarSeverity.Success);
        }
        catch (Exception ex) { ShowStatus(ex.Message, InfoBarSeverity.Error); }
    }

    // ── Operations ────────────────────────────────────────────────────────────────

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? "");
    }

    private void PopulateOps(string filter)
    {
        _ops ??= RainmeterOperations.All(_rm).ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
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
