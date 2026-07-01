using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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
/// 一個可編輯嘅工作區名 · One editable workspace name row (two-way bound in the workspaces editor).
/// </summary>
public sealed class WorkspaceRow : INotifyPropertyChanged
{
    private string _name = "";
    public string Name { get => _name; set { _name = value; PropertyChanged?.Invoke(this, new(nameof(Name))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// GlazeWM 平鋪視窗管理 · GlazeWM tiling window manager control panel.
/// 偵測／安裝（winget）、起停重載 daemon、結構化編輯 config（gaps／focus／startup／workspaces）、
/// 鍵盤綁定一覽、原始 YAML 編輯，以及 CLI 操作卡。全部雙語。
/// Detect/install (winget), start/stop/reload the daemon, structured config editing
/// (gaps / focus / startup / workspaces), a keybinding list, a raw-YAML editor, and CLI operation cards.
/// </summary>
public sealed partial class GlazeWmModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _opBusy;
    private readonly ObservableCollection<WorkspaceRow> _workspaces = new();

    public GlazeWmModule()
    {
        InitializeComponent();
        WorkspaceList.ItemsSource = _workspaces;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) =>
        {
            Render();
            PopulateOps(string.Empty);
            await CheckEngine();
            LoadConfigIntoUi();
            RefreshStatus();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        PopulateOps(OpsFilter.Text ?? string.Empty);
        RefreshStatus();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "GlazeWM Tiling · GlazeWM 平鋪視窗";
        HeaderBlurb.Text = P(
            "Wrap GlazeWM, the open-source tiling window manager, inside WinForge: install it, start/stop/reload the daemon, and edit its config (gaps, keybindings, workspaces) in-app.",
            "喺 WinForge 包住開源平鋪視窗管理員 GlazeWM：安裝、起停／重載 daemon，並喺 app 內編輯設定（邊距、鍵盤綁定、工作區）。");

        WarnBar.Title = P("Heads up before you start", "啟動前注意");
        WarnBar.Message = P(
            "Once started, GlazeWM takes over and tiles ALL your windows. Press alt+shift+e (or use Exit) to stop it and restore your windows.",
            "一開咗，GlazeWM 會接管並平鋪你所有視窗。撳 alt+shift+e（或用「退出」）即可停止並還原視窗。");

        StartBtn.Content = P("Start", "啟動");
        StopBtn.Content = P("Stop", "停止");
        ReloadBtn.Content = P("Reload config", "重載設定");
        RefreshBtn.Content = P("Refresh status", "重新整理狀態");
        StartupCheck.Content = P("Start with Windows", "開機自動啟動");

        ConfigHeader.Text = P("Config editor", "設定編輯器");
        GapsLabel.Text = P("Gaps", "邊距");
        InnerGapLabel.Text = P("Inner gap (between windows)", "內邊距（視窗之間）");
        OuterGapLabel.Text = P("Outer gap (screen edge)", "外邊距（螢幕邊緣）");
        FocusHoverToggle.Header = P("Focus follows cursor", "焦點跟隨游標");
        FocusHoverToggle.OnContent = P("On", "開");
        FocusHoverToggle.OffContent = P("Off", "關");
        StartupCmdLabel.Text = P("Startup commands (raw YAML list, e.g. ['shell-exec zebar'])",
            "啟動指令（原始 YAML 陣列，例如 ['shell-exec zebar']）");
        SaveStructuredBtn.Content = P("Save & reload", "儲存並重載");
        ReloadStructuredBtn.Content = P("Revert", "還原");

        WorkspacesHeader.Text = P("Workspaces", "工作區");
        WorkspacesHint.Text = P("Edit workspace names. Saving rewrites only the workspaces block — comments and other keys are preserved.",
            "編輯工作區名稱。儲存只會重寫 workspaces 區塊 — 註解同其他鍵會保留。");
        AddWorkspaceBtn.Content = P("Add workspace", "新增工作區");
        SaveWorkspacesBtn.Content = P("Save workspaces", "儲存工作區");

        KeybindHeader.Text = P("Keybindings", "鍵盤綁定");
        KeyColHeader.Text = P("Keys", "按鍵");
        CmdColHeader.Text = P("Commands", "指令");
        KeybindEmpty.Text = P("No keybindings parsed (config missing or empty).", "未解析到鍵盤綁定（設定缺失或空白）。");

        RawHeader.Text = P("Raw YAML editor", "原始 YAML 編輯器");
        RawHint.Text = P("Full access to every config key. Save writes config.yaml verbatim; reload the daemon to apply.",
            "完整存取每個設定鍵。儲存會原文寫入 config.yaml；重載 daemon 即生效。");
        SaveRawBtn.Content = P("Save raw", "儲存原文");
        ReloadRawBtn.Content = P("Revert", "還原");
        OpenFolderBtn.Content = P("Open config folder", "開啟設定資料夾");
        PickConfigBtn.Content = P("Use another config…", "改用其他設定檔…");
        CreateConfigBtn.Content = P("Create default config", "建立預設設定檔");

        _ops ??= GlazeWmOperations.All().ToList();
        OpsHeader.Text = P($"CLI operations ({_ops.Count})", $"CLI 操作（{_ops.Count}）");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");

        ConfigPathText.Text = P($"Config: {GlazeWmService.ConfigPath}", $"設定檔：{GlazeWmService.ConfigPath}");
    }

    // ===== engine / install =====

    private async Task CheckEngine()
    {
        bool ok = await GlazeWmService.IsInstalledAsync();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; WarnBar.IsOpen = true; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("GlazeWM not found", "搵唔到 GlazeWM");
        EngineBar.Message = P("Click to install GlazeWM automatically (winget) — no restart needed.",
            "撳一下自動安裝 GlazeWM（winget）— 唔使重開。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            GlazeWmService.WingetId, "Install GlazeWM automatically", "自動安裝 GlazeWM",
            async () => { await CheckEngine(); RefreshStatus(); }, null);
    }

    // ===== status =====

    private async void RefreshStatus()
    {
        bool running = GlazeWmService.IsRunning();
        StatusBar.Severity = running ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        StatusBar.Title = running ? P("Running", "運行中") : P("Stopped", "已停止");
        StatusBar.Message = running
            ? P("GlazeWM is managing your windows.", "GlazeWM 正在管理你嘅視窗。")
            : P("GlazeWM is not running.", "GlazeWM 而家冇行緊。");
        StartBtn.IsEnabled = !running;
        StopBtn.IsEnabled = running;
        ReloadBtn.IsEnabled = running;
        StartupCheck.IsChecked = GlazeWmService.IsStartWithWindows();

        try
        {
            var v = await GlazeWmService.VersionAsync();
            VersionText.Text = string.IsNullOrWhiteSpace(v) ? "" : P($"Version: {v}", $"版本：{v}");
        }
        catch { VersionText.Text = ""; }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var r = GlazeWmService.Start();
        Report(r);
        RefreshStatus();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopBtn.IsEnabled = false;
        var r = await GlazeWmService.StopAsync();
        Report(r);
        RefreshStatus();
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        var r = await GlazeWmService.ReloadAsync();
        Report(r);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshStatus();

    private void Startup_Click(object sender, RoutedEventArgs e)
    {
        try { GlazeWmService.SetStartWithWindows(StartupCheck.IsChecked == true); }
        catch { }
        RefreshStatus();
    }

    // ===== config load / structured editor =====

    private void LoadConfigIntoUi()
    {
        ConfigPathText.Text = P($"Config: {GlazeWmService.ConfigPath}", $"設定檔：{GlazeWmService.ConfigPath}");
        bool exists = GlazeWmService.ConfigExists();
        NoConfigBar.IsOpen = !exists;
        if (!exists)
        {
            NoConfigBar.Severity = InfoBarSeverity.Warning;
            NoConfigBar.Title = P("No config yet", "未有設定檔");
            NoConfigBar.Message = P("config.yaml was not found. Click \"Create default config\" below, or start GlazeWM once (it writes one).",
                "搵唔到 config.yaml。撳下面「建立預設設定檔」，或者啟動一次 GlazeWM（佢會自動建立）。");
        }

        // structured
        InnerGapBox.Text = GlazeWmService.GetInnerGap();
        OuterGapBox.Text = GlazeWmService.GetOuterGapTop();
        FocusHoverToggle.IsOn = GlazeWmService.GetFocusFollowsCursor();
        StartupCmdBox.Text = GlazeWmService.GetStartupCommands();

        // workspaces
        _workspaces.Clear();
        foreach (var w in GlazeWmService.GetWorkspaces())
            _workspaces.Add(new WorkspaceRow { Name = w });

        // keybindings
        var binds = GlazeWmService.GetKeybindings();
        KeybindList.ItemsSource = binds;
        KeybindEmpty.Visibility = binds.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // raw
        RawBox.Text = GlazeWmService.ReadConfig();
    }

    private async void SaveStructured_Click(object sender, RoutedEventArgs e)
    {
        if (!GlazeWmService.ConfigExists())
        {
            Report(TweakResult.Fail("No config to edit — create one first.", "未有設定檔可改 — 請先建立。"));
            return;
        }
        TweakResult last = TweakResult.Ok("Saved.", "已儲存。");
        if (!string.IsNullOrWhiteSpace(InnerGapBox.Text)) last = GlazeWmService.SetInnerGap(InnerGapBox.Text.Trim());
        if (!string.IsNullOrWhiteSpace(OuterGapBox.Text)) GlazeWmService.SetOuterGapTop(OuterGapBox.Text.Trim());
        GlazeWmService.SetFocusFollowsCursor(FocusHoverToggle.IsOn);
        if (!string.IsNullOrWhiteSpace(StartupCmdBox.Text)) GlazeWmService.SetStartupCommands(StartupCmdBox.Text.Trim());

        // reflect into raw box and auto-reload if running.
        RawBox.Text = GlazeWmService.ReadConfig();
        await AutoReload();
        Report(TweakResult.Ok("Config saved.", "設定已儲存。"));
    }

    private void ReloadStructured_Click(object sender, RoutedEventArgs e) => LoadConfigIntoUi();

    // ===== workspaces editor =====

    private void AddWorkspace_Click(object sender, RoutedEventArgs e)
        => _workspaces.Add(new WorkspaceRow { Name = (_workspaces.Count + 1).ToString() });

    private void RemoveWorkspace_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is WorkspaceRow row)
            _workspaces.Remove(row);
    }

    private async void SaveWorkspaces_Click(object sender, RoutedEventArgs e)
    {
        var names = _workspaces.Select(w => w.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        var r = GlazeWmService.SetWorkspaces(names);
        RawBox.Text = GlazeWmService.ReadConfig();
        if (r.Success) await AutoReload();
        Report(r);
    }

    // ===== raw editor =====

    private async void SaveRaw_Click(object sender, RoutedEventArgs e)
    {
        var r = GlazeWmService.WriteConfig(RawBox.Text ?? "");
        if (r.Success) { LoadConfigIntoUi(); await AutoReload(); }
        Report(r);
    }

    private void ReloadRaw_Click(object sender, RoutedEventArgs e)
    {
        RawBox.Text = GlazeWmService.ReadConfig();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(GlazeWmService.ConfigPath);
            if (string.IsNullOrEmpty(dir)) return;
            System.IO.Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private async void PickConfig_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".yaml", ".yml");
        if (string.IsNullOrEmpty(path)) return;
        GlazeWmService.ConfigPath = path;
        LoadConfigIntoUi();
        Report(TweakResult.Ok($"Now editing: {path}", $"現正編輯：{path}"));
    }

    private void CreateConfig_Click(object sender, RoutedEventArgs e)
    {
        var r = GlazeWmService.CreateDefaultConfig();
        if (r.Success) LoadConfigIntoUi();
        Report(r);
    }

    private async Task AutoReload()
    {
        if (GlazeWmService.IsRunning())
        {
            try { await GlazeWmService.ReloadAsync(); } catch { }
        }
    }

    // ===== ops =====

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= GlazeWmOperations.All().ToList();
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

    private Border MakeDivider() => new()
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
        text.Children.Add(new TextBlock
        {
            Text = P(def.Title.En, def.Title.Zh),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
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

    // ---------------- Action → Button awaiting RunAsync ----------------
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
        if (_opBusy || def.RunAsync is null) return;
        if (def.Destructive && !await ConfirmOp(def)) return;

        _opBusy = true;
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
                var body = r.Output!;
                OpsOutText.Text = body.Length > 4000 ? body[^4000..] : body;
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
            _opBusy = false;
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
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
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

    // ---------------- Shared op status surfacing (persistent OpsResultBar) ----------------
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

    // ===== shared status reporting =====

    private void Report(TweakResult r)
    {
        StatusBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        StatusBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        if (!string.IsNullOrWhiteSpace(r.Output)) msg = string.IsNullOrWhiteSpace(msg) ? r.Output! : $"{msg}\n{r.Output}";
        StatusBar.Message = msg.Length > 1200 ? msg[..1200] : msg;
        StatusBar.IsOpen = true;
    }
}
