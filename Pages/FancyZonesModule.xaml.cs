using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// FancyZones（PowerToys 視窗分區）· A manager front-end for PowerToys FancyZones — detect / install
/// PowerToys, launch it, open the zone editor, toggle the FancyZones module, browse built-in layouts &amp;
/// snap hotkeys, tweak FancyZones behaviour (settings.json), and import/export saved layouts. The zone
/// engine itself stays native PowerToys. Fully bilingual (English + 廣東話).
/// </summary>
public sealed partial class FancyZonesModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _suppressModuleToggle;
    private bool _installed;

    public FancyZonesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) =>
        {
            Render();
            BuildLayouts();
            BuildHotkeys();
            BuildActions();
            PopulateOps(string.Empty);
            await Refresh();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildLayouts();
        BuildHotkeys();
        BuildActions();
        PopulateOps(OpsFilter.Text ?? string.Empty);
        UpdateStatus();
        UpdateCustomLayouts();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "FancyZones · 視窗分區";
        HeaderBlurb.Text = P(
            "FancyZones is the PowerToys window-tiling feature: drag windows into a custom grid of zones and snap them instantly. WinForge installs and launches PowerToys, opens the zone editor, toggles the module and lets you tune its behaviour — the snap engine stays native PowerToys.",
            "FancyZones 係 PowerToys 嘅視窗排版功能：將窗拖入自訂嘅分區格網，即刻貼齊。WinForge 幫你安裝同啟動 PowerToys、開分區編輯器、開關模組同調校行為 — 貼齊引擎本身仍然係原生 PowerToys。");

        StatusTitle.Text = P("PowerToys status", "PowerToys 狀態");
        RefreshBtn.Content = P("Re-check", "重新檢查");
        ModuleToggleTitle.Text = P("Enable FancyZones module", "啟用 FancyZones 模組");
        ModuleToggleSub.Text = P("Writes the module flag into PowerToys settings.json and reloads PowerToys.",
            "將模組旗標寫入 PowerToys settings.json 並重新載入 PowerToys。");

        LayoutsHeader.Text = P("Built-in layouts", "內建版面");
        LayoutsHint.Text = P(
            "Open the zone editor to apply or customise these. Press the editor hotkey (default Win+Shift+`) or use \"Open Zone Editor\" above.",
            "開分區編輯器去套用或自訂呢啲。撳編輯器熱鍵（預設 Win+Shift+`）或者用上面嘅「開分區編輯器」。");

        HotkeysHeader.Text = P("Snap hotkeys", "貼齊熱鍵");
        CustomHeader.Text = P("Your saved custom layouts", "你儲存咗嘅自訂版面");
        CustomText.Text = P("Read from PowerToys' custom-layouts.json (best-effort). Create layouts in the zone editor.",
            "由 PowerToys 嘅 custom-layouts.json 讀返（盡力而為）。喺分區編輯器入面整版面。");
        ImportBtn.Content = P("Import layout JSON…", "匯入版面 JSON…");
        ExportBtn.Content = P("Export layout files…", "匯出版面檔案…");

        OpsHeader.Text = P("FancyZones behaviour", "FancyZones 行為");
        OpsHint.Text = P(
            "These toggles write PowerToys' FancyZones settings.json directly and reload PowerToys. Defaults match PowerToys when no file exists yet.",
            "呢啲開關直接寫 PowerToys 嘅 FancyZones settings.json 並重新載入。未有檔案時跟 PowerToys 預設。");
        OpsFilter.PlaceholderText = P("Filter settings…", "篩選設定…");
    }

    // ===================== 偵測／狀態 · Detection / status =====================

    private void Refresh_Click(object sender, RoutedEventArgs e) => _ = Refresh();

    private async Task Refresh()
    {
        FancyZonesService.Rescan();
        _installed = await FancyZonesService.IsInstalledAsync();

        if (_installed)
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
        }
        else
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("PowerToys not found", "搵唔到 PowerToys");
            EngineBar.Message = P(
                "Click to install PowerToys automatically (winget). The installer adds an elevated runner and may prompt for UAC.",
                "撳一下自動安裝 PowerToys（winget）。安裝程式會加入需要提權嘅 runner，可能會彈 UAC。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                FancyZonesService.WingetId,
                "Install PowerToys automatically", "自動安裝 PowerToys",
                async () => { await Refresh(); },
                FancyZonesService.Rescan);
        }

        UpdateStatus();
        UpdateModuleToggle();
        UpdateActions();
        UpdateCustomLayouts();
    }

    private void UpdateStatus()
    {
        if (!_installed)
        {
            StatusText.Text = P("PowerToys is not installed. Install it above to use FancyZones.",
                "未安裝 PowerToys。喺上面安裝先可以用 FancyZones。");
            return;
        }
        var ver = FancyZonesService.Version;
        var running = FancyZonesService.IsRunning;
        var enabled = FancyZonesService.IsModuleEnabled();
        var enText = enabled switch
        {
            true => P("FancyZones on", "FancyZones 已開"),
            false => P("FancyZones off", "FancyZones 已關"),
            _ => P("FancyZones state unknown", "FancyZones 狀態未知"),
        };
        var runText = running ? P("running", "行緊") : P("not running", "未行");
        StatusText.Text = P(
            $"Installed{(ver is null ? "" : $" (v{ver})")} · {runText} · {enText}.",
            $"已安裝{(ver is null ? "" : $"（v{ver}）")} · {runText} · {enText}。");
    }

    // ===================== 模組開關 · Module toggle =====================

    private void UpdateModuleToggle()
    {
        _suppressModuleToggle = true;
        ModuleSwitch.IsEnabled = _installed;
        ModuleSwitch.IsOn = FancyZonesService.IsModuleEnabled() == true;
        _suppressModuleToggle = false;
    }

    private void ModuleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppressModuleToggle) return;
        var r = FancyZonesService.SetModuleEnabled(ModuleSwitch.IsOn);
        ShowResult(r);
        UpdateStatus();
    }

    // ===================== 主要動作 · Primary actions =====================

    private void BuildActions()
    {
        ActionsPanel.Children.Clear();
        AddAction(P("Launch PowerToys", "啟動 PowerToys"), () => FancyZonesService.LaunchPowerToys());
        AddAction(P("Open Zone Editor", "開分區編輯器"), () => FancyZonesService.OpenZoneEditor());
        AddAction(P("Open FancyZones Settings", "開 FancyZones 設定"), () => FancyZonesService.OpenSettingsPage());
        UpdateActions();
    }

    private void AddAction(string label, Func<TweakResult> run)
    {
        var btn = new Button { Content = label, Tag = run };
        btn.Click += (_, _) =>
        {
            try { ShowResult(run()); UpdateStatus(); }
            catch (Exception ex) { ShowResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
        };
        ActionsPanel.Children.Add(btn);
    }

    private void UpdateActions()
    {
        foreach (var child in ActionsPanel.Children)
            if (child is Button b) b.IsEnabled = _installed;
    }

    private void ShowResult(TweakResult r)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        var msg = r.Message is null ? "" : (Loc.I.IsCantonesePrimary ? r.Message.Zh : r.Message.En);
        ResultBar.Message = string.IsNullOrWhiteSpace(r.Output) ? msg : $"{msg}\n{r.Output}";
    }

    // ===================== 內建版面預覽 · Built-in layout previews =====================

    private void BuildLayouts()
    {
        LayoutsRepeater.Items.Clear();
        foreach (var (en, zh, cells) in BuiltInLayouts())
            LayoutsRepeater.Items.Add(LayoutCard(en, zh, cells));
    }

    // 每個版面用相對矩形描述（x,y,w,h，0..1）· Each layout described as relative rects (x,y,w,h in 0..1).
    private static IEnumerable<(string en, string zh, (double x, double y, double w, double h)[] cells)> BuiltInLayouts()
    {
        yield return ("Focus", "焦點", new[]
        {
            (0.10, 0.16, 0.45, 0.55), (0.28, 0.30, 0.45, 0.55),
        });
        yield return ("Columns", "直欄", new[]
        {
            (0.02, 0.05, 0.30, 0.90), (0.35, 0.05, 0.30, 0.90), (0.68, 0.05, 0.30, 0.90),
        });
        yield return ("Rows", "橫行", new[]
        {
            (0.05, 0.04, 0.90, 0.28), (0.05, 0.36, 0.90, 0.28), (0.05, 0.68, 0.90, 0.28),
        });
        yield return ("Grid", "格網", new[]
        {
            (0.04, 0.06, 0.44, 0.40), (0.52, 0.06, 0.44, 0.40),
            (0.04, 0.54, 0.44, 0.40), (0.52, 0.54, 0.44, 0.40),
        });
        yield return ("Priority Grid", "優先格網", new[]
        {
            (0.04, 0.06, 0.30, 0.88), (0.37, 0.06, 0.30, 0.88),
            (0.70, 0.06, 0.26, 0.42), (0.70, 0.52, 0.26, 0.42),
        });
    }

    private static Color AccentColor()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var v) && v is Color c)
                return c;
        }
        catch { }
        return Color.FromArgb(255, 0, 120, 215); // Windows default accent
    }

    private Border LayoutCard(string en, string zh, (double x, double y, double w, double h)[] cells)
    {
        const double W = 150, H = 95;
        var canvas = new Canvas { Width = W, Height = H };
        var accent = AccentColor();
        var fill = new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B));
        var stroke = new SolidColorBrush(accent);
        foreach (var c in cells)
        {
            var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
            {
                Width = Math.Max(2, c.w * W),
                Height = Math.Max(2, c.h * H),
                RadiusX = 3,
                RadiusY = 3,
                Fill = fill,
                Stroke = stroke,
                StrokeThickness = 1.2,
            };
            Canvas.SetLeft(rect, c.x * W);
            Canvas.SetTop(rect, c.y * H);
            canvas.Children.Add(rect);
        }

        var label = new TextBlock
        {
            Text = $"{en} · {zh}",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };

        return new Border
        {
            Margin = new Thickness(0, 0, 10, 10),
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { canvas, label },
            },
        };
    }

    // ===================== 貼齊熱鍵 · Snap hotkeys =====================

    private void BuildHotkeys()
    {
        HotkeysPanel.Children.Clear();
        AddHotkey("Shift (hold while dragging)", "Shift（拖曳時按住）",
            "Show the zones and snap the window into one.", "顯示分區並將窗貼入其中一個。");
        AddHotkey("Win + Ctrl + Arrow", "Win + Ctrl + 方向鍵",
            "Move the active window to an adjacent zone (needs \"override snap hotkeys\").",
            "將作用中嘅窗移去相鄰分區（要開「覆寫貼齊熱鍵」）。");
        AddHotkey("Win + Arrow", "Win + 方向鍵",
            "With override on, snaps between zones instead of Windows Snap.",
            "開咗覆寫之後，會喺分區之間貼齊而唔係 Windows 貼齊。");
        AddHotkey("Win + Shift + ` ", "Win + Shift + `",
            "Open the FancyZones zone editor (default editor hotkey).", "開 FancyZones 分區編輯器（預設編輯器熱鍵）。");
        AddHotkey("Ctrl + Win + Alt + Number", "Ctrl + Win + Alt + 數字",
            "Quick-switch to a layout (when quick layout switch is on).", "快速切換到某個版面（開咗快速版面切換時）。");
    }

    private void AddHotkey(string keysEn, string keysZh, string descEn, string descZh)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var key = new Border
        {
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            Child = new TextBlock
            {
                Text = P(keysEn, keysZh),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12.5,
            },
        };
        Grid.SetColumn(key, 0);

        var desc = new TextBlock
        {
            Text = P(descEn, descZh),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        Grid.SetColumn(desc, 1);

        grid.Children.Add(key);
        grid.Children.Add(desc);
        HotkeysPanel.Children.Add(grid);
    }

    // ===================== 自訂版面 · Custom layouts =====================

    private void UpdateCustomLayouts()
    {
        CustomList.Children.Clear();
        var names = _installed ? FancyZonesService.ReadCustomLayoutNames() : Array.Empty<string>();
        if (names.Count == 0)
        {
            CustomList.Children.Add(new TextBlock
            {
                Text = P("No saved custom layouts found.", "搵唔到已儲存嘅自訂版面。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                FontStyle = Windows.UI.Text.FontStyle.Italic,
            });
            return;
        }
        foreach (var n in names)
        {
            CustomList.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "", FontSize = 14, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = n, VerticalAlignment = VerticalAlignment.Center },
                },
            });
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".json");
        if (string.IsNullOrEmpty(path)) return;
        ShowResult(FancyZonesService.ImportLayoutFile(path));
        UpdateCustomLayouts();
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose a folder to export FancyZones JSON into",
            "揀一個資料夾匯出 FancyZones JSON"));
        if (string.IsNullOrEmpty(folder)) return;
        ShowResult(FancyZonesService.ExportLayouts(folder));
    }

    // ===================== 行為開關 · Behaviour toggles =====================

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= FancyZonesOperations.All().ToList();
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
}
