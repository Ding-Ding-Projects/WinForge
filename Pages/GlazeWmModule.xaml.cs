using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
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
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
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
