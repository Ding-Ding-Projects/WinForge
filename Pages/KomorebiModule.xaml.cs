using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Komorebi 平鋪視窗管理 · Komorebi tiling window manager — a rich WinUI front-end over the `komorebic`
/// CLI. Detects/installs the binary (winget), controls the daemon (start/stop/restart), shows live state
/// as a monitors → workspaces → windows tree, switches layouts, navigates workspaces, edits padding,
/// manages window rules, opens/reloads the config, and exposes every toggle op as a card. Bilingual.
/// </summary>
public sealed partial class KomorebiModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _busy;
    private bool _opBusy;

    public KomorebiModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) =>
        {
            Render();
            BuildLayoutCombo();
            BuildRuleCombos();
            PopulateOps(string.Empty);
            await CheckEngine();
            await RefreshStatus();
        };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildLayoutCombo();
        BuildRuleCombos();
        PopulateOps(OpsFilter.Text ?? string.Empty);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Komorebi · 平鋪視窗管理";
        HeaderBlurb.Text = P(
            "Drive the Komorebi tiling window manager from inside WinForge: install it, start/stop the daemon, switch layouts, navigate workspaces, set gaps, add window rules and edit the config — all over the komorebic CLI.",
            "喺 WinForge 直接操控 Komorebi 平鋪視窗管理：安裝、開／停守護程序、切換排版、切換工作區、設定間距、加視窗規則同編輯設定 — 全部經 komorebic CLI。");

        WarnBar.Title = P("Before you start", "開始前留意");
        WarnBar.Message = P(
            "Komorebi takes over tiling for your windows and may rearrange them. Make sure your work is saved. You can stop it any time to restore your windows.",
            "Komorebi 會接管你嘅視窗平鋪，可能會重新排列。請先儲存工作。你隨時可以停止佢嚟還原視窗。");

        StartBtn.Content = P("Start daemon", "啟動守護程序");
        StopBtn.Content = P("Stop", "停止");
        RestartBtn.Content = P("Restart", "重新啟動");
        RefreshBtn.Content = P("Refresh", "重新整理");
        WithBarCheck.Content = P("Also start komorebi-bar", "同時啟動 komorebi-bar");

        LayoutHeader.Text = P("Layout (focused workspace)", "排版（聚焦工作區）");
        LayoutHint.Text = P("Pick a layout to apply to the currently focused workspace, or cycle through them.",
            "揀一個排版套用到目前聚焦嘅工作區，或者循環切換。");
        ApplyLayoutBtn.Content = P("Apply layout", "套用排版");
        CycleNextBtn.Content = P("Next ▷", "下一個 ▷");
        CyclePrevBtn.Content = P("◁ Previous", "◁ 上一個");

        TreeHeader.Text = P("Live state — monitors → workspaces → windows", "即時狀態 — 顯示器 → 工作區 → 視窗");
        TreeHint.Text = P("Reflects `komorebic state`. ★ marks the focused monitor/workspace. Refresh to update.",
            "對應 `komorebic state`。★ 代表聚焦嘅顯示器／工作區。撳重新整理更新。");

        WsHeader.Text = P("Workspace navigation & padding", "工作區導覽與間距");
        WsLabel.Text = P("Workspace index (0-based):", "工作區索引（由 0 起）：");
        FocusWsBtn.Content = P("Focus", "聚焦");
        MoveWsBtn.Content = P("Move window here", "移視窗過去");
        SendWsBtn.Content = P("Send window here", "送視窗過去");
        WsPadLabel.Text = P("Workspace padding (px)", "工作區間距（像素）");
        ContPadLabel.Text = P("Container padding (px)", "容器間距（像素）");
        ApplyPadBtn.Content = P("Apply to focused", "套用到聚焦工作區");

        RulesHeader.Text = P("Window rules", "視窗規則");
        RulesHint.Text = P("Pin an app to a workspace, ignore it, or force-manage it. The identifier is matched against the chosen field (exe / class / title / path).",
            "將應用釘到工作區、忽略佢、或者強制管理。識別碼會比對所選欄位（exe／class／title／path）。");
        RuleMonLabel.Text = P("Monitor:", "顯示器：");
        RuleWsLabel.Text = P("Workspace:", "工作區：");
        AddRuleBtn.Content = P("Add rule", "加規則");

        ConfigHeader.Text = P("Configuration (komorebi.json)", "設定（komorebi.json）");
        OpenConfigBtn.Content = P("Open in editor", "用編輯器開啟");
        PickConfigBtn.Content = P("Pick config file…", "揀設定檔…");
        ReloadConfigBtn.Content = P("Reload configuration", "重新載入設定");
        QuickstartBtn.Content = P("Create defaults (quickstart)", "建立預設（quickstart）");
        UpdateConfigPathText();

        RawStateBtn.Content = P("View raw state JSON", "查睇原始狀態 JSON");
        GlobalStateBtn.Content = P("View global state", "查睇全域狀態");
        CheckBtn.Content = P("Check configuration", "檢查設定");

        _ops ??= KomorebiOperations.All().ToList();
        OpsHeader.Text = P($"Operations ({_ops.Count})", $"操作（{_ops.Count}）");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");

        HotkeyBar.Title = P("Hotkeys", "熱鍵");
        HotkeyBar.Message = P(
            "WinForge manages the daemon, not keybindings. For keyboard control bind komorebic commands with whkd or AutoHotkey (see the komorebi docs).",
            "WinForge 只管守護程序，唔管鍵盤綁定。要鍵盤操控，用 whkd 或 AutoHotkey 綁定 komorebic 指令（詳見 komorebi 文件）。");
    }

    private void UpdateConfigPathText()
    {
        var path = KomorebiService.GuessConfigPath();
        bool exists = KomorebiService.ConfigExists();
        ConfigPathText.Text = exists
            ? P($"Config found: {path}", $"搵到設定：{path}")
            : P($"No config yet — expected at: {path}. Use “Create defaults” to generate one.",
                $"未有設定 — 預期喺：{path}。用「建立預設」嚟產生一個。");
    }

    private void BuildLayoutCombo()
    {
        var prev = (LayoutCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        LayoutCombo.Items.Clear();
        foreach (var (value, en, zh) in KomorebiService.Layouts)
            LayoutCombo.Items.Add(new ComboBoxItem { Content = $"{P(en, zh)}", Tag = value });
        var match = LayoutCombo.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string) == (prev ?? "bsp"));
        LayoutCombo.SelectedItem = match ?? LayoutCombo.Items[0];
    }

    private void BuildRuleCombos()
    {
        var prevKind = (RuleKind.SelectedItem as ComboBoxItem)?.Tag as string;
        RuleKind.Items.Clear();
        RuleKind.Items.Add(new ComboBoxItem { Content = P("Workspace rule (pin)", "工作區規則（釘選）"), Tag = "workspace" });
        RuleKind.Items.Add(new ComboBoxItem { Content = P("Ignore rule", "忽略規則"), Tag = "ignore" });
        RuleKind.Items.Add(new ComboBoxItem { Content = P("Manage rule (force)", "管理規則（強制）"), Tag = "manage" });
        RuleKind.SelectedItem = RuleKind.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string) == prevKind)
                                 ?? RuleKind.Items[0];
        RuleKind.SelectionChanged -= RuleKind_Changed;
        RuleKind.SelectionChanged += RuleKind_Changed;

        var prevId = (RuleId.SelectedItem as ComboBoxItem)?.Tag as string;
        RuleId.Items.Clear();
        foreach (var id in new[] { "exe", "class", "title", "path" })
            RuleId.Items.Add(new ComboBoxItem { Content = id, Tag = id });
        RuleId.SelectedItem = RuleId.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string) == (prevId ?? "exe"))
                              ?? RuleId.Items[0];

        UpdateRuleRowVisibility();
    }

    private void RuleKind_Changed(object sender, SelectionChangedEventArgs e) => UpdateRuleRowVisibility();

    private void UpdateRuleRowVisibility()
    {
        var kind = (RuleKind.SelectedItem as ComboBoxItem)?.Tag as string;
        RuleWsRow.Visibility = kind == "workspace" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ===== engine / status =====

    private async Task CheckEngine()
    {
        bool ok = await KomorebiService.IsInstalledAsync();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; EngineBar.Content = null; SetControlsEnabled(true); return; }
        SetControlsEnabled(false);
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Komorebi not found", "搵唔到 Komorebi");
        EngineBar.Message = P("Click to install Komorebi automatically (winget) — no restart needed.",
            "撳一下自動安裝 Komorebi（winget）— 唔使重開。");
        // Rich install control: real progress bar + live streamed status + % + Cancel + success/error animation.
        EngineBar.ActionButton = null;
        var install = EngineBars.AutoInstallProgress(
            KomorebiService.WingetId, "Install Komorebi", "安裝 Komorebi",
            async () => { await CheckEngine(); await RefreshStatus(); }, null);
        install.Margin = new Thickness(0, 4, 0, 8);
        EngineBar.Content = install;
    }

    private void SetControlsEnabled(bool on)
    {
        foreach (var b in new[] { StartBtn, StopBtn, RestartBtn, ApplyLayoutBtn, CycleNextBtn, CyclePrevBtn,
                                  FocusWsBtn, MoveWsBtn, SendWsBtn, ApplyPadBtn, AddRuleBtn, ReloadConfigBtn,
                                  QuickstartBtn, RawStateBtn, GlobalStateBtn, CheckBtn })
            b.IsEnabled = on;
    }

    private async Task RefreshStatus()
    {
        if (!await KomorebiService.IsInstalledAsync()) { WarnBar.IsOpen = false; return; }

        bool running = await KomorebiService.IsRunningAsync();
        StatusDot.Fill = new SolidColorBrush(running ? Colors.LimeGreen : Colors.Gray);
        StatusText.Text = running
            ? P("Daemon running", "守護程序執行中")
            : P("Daemon not running", "守護程序未執行");
        StartBtn.IsEnabled = !running && !_busy;
        StopBtn.IsEnabled = running && !_busy;
        RestartBtn.IsEnabled = running && !_busy;
        WarnBar.IsOpen = !running;

        if (running) await RefreshTree();
        else { StateTree.RootNodes.Clear(); TreeEmpty.Visibility = Visibility.Visible; TreeEmpty.Text = P("Start the daemon to see live state.", "啟動守護程序就會見到即時狀態。"); }
    }

    private async Task RefreshTree()
    {
        var state = await KomorebiService.GetStateAsync();
        StateTree.RootNodes.Clear();
        if (state.Monitors.Count == 0)
        {
            TreeEmpty.Visibility = Visibility.Visible;
            TreeEmpty.Text = state.RawError is { Length: > 0 }
                ? P($"Could not read state: {Trim(state.RawError, 200)}", $"讀唔到狀態：{Trim(state.RawError, 200)}")
                : P("No monitors reported.", "未有顯示器資料。");
            return;
        }
        TreeEmpty.Visibility = Visibility.Collapsed;

        foreach (var m in state.Monitors)
        {
            var mNode = new TreeViewNode
            {
                Content = (m.Focused ? "★ " : "") + P($"Monitor {m.Index}: {m.Name}", $"顯示器 {m.Index}：{m.Name}"),
                IsExpanded = true,
            };
            foreach (var w in m.Workspaces)
            {
                var layout = string.IsNullOrEmpty(w.Layout) ? "" : $" · {w.Layout}";
                var wNode = new TreeViewNode
                {
                    Content = (w.Focused ? "★ " : "") + P($"Workspace {w.Index} ({w.Name}){layout} — {w.Windows.Count} win",
                                                          $"工作區 {w.Index}（{w.Name}）{layout} — {w.Windows.Count} 個視窗"),
                    IsExpanded = w.Focused,
                };
                foreach (var win in w.Windows)
                {
                    var title = string.IsNullOrWhiteSpace(win.Title) ? win.Exe : win.Title;
                    wNode.Children.Add(new TreeViewNode { Content = $"{title}  ·  {win.Exe}" });
                }
                mNode.Children.Add(wNode);
            }
            StateTree.RootNodes.Add(mNode);
        }
    }

    private static string Trim(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "…";

    // ===== lifecycle handlers =====

    private async Task RunGuarded(Func<Task<TweakResult>> op, string okEn, string okZh)
    {
        if (_busy) return;
        _busy = true;
        BusyRing.IsActive = true;
        SetControlsEnabled(false);
        try
        {
            var r = await op();
            ShowResult(r, okEn, okZh);
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally
        {
            _busy = false;
            BusyRing.IsActive = false;
            SetControlsEnabled(true);
            await RefreshStatus();
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var flags = WithBarCheck.IsChecked == true ? "--bar" : "";
        await RunGuarded(() => KomorebiService.StartAsync(flags), "Daemon started.", "守護程序已啟動。");
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.StopAsync(), "Daemon stopped, windows restored.", "守護程序已停止，視窗已還原。");

    private async void Restart_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.RestartAsync(), "Daemon restarted.", "守護程序已重新啟動。");

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshStatus();

    // ===== layout =====

    private async void ApplyLayout_Click(object sender, RoutedEventArgs e)
    {
        if ((LayoutCombo.SelectedItem as ComboBoxItem)?.Tag is not string layout) return;
        await RunGuarded(() => KomorebiService.ChangeLayoutAsync(layout), $"Layout set to {layout}.", $"排版已設為 {layout}。");
    }

    private async void CycleNext_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.CycleLayoutAsync(true), "Cycled to next layout.", "已切換到下一個排版。");

    private async void CyclePrev_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.CycleLayoutAsync(false), "Cycled to previous layout.", "已切換到上一個排版。");

    // ===== workspace nav / padding =====

    private int WsIndexValue => (int)(double.IsNaN(WsIndex.Value) ? 0 : WsIndex.Value);

    private async void FocusWs_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.FocusWorkspaceAsync(WsIndexValue), $"Focused workspace {WsIndexValue}.", $"已聚焦工作區 {WsIndexValue}。");

    private async void MoveWs_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.MoveToWorkspaceAsync(WsIndexValue), $"Window moved to workspace {WsIndexValue}.", $"視窗已移去工作區 {WsIndexValue}。");

    private async void SendWs_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.SendToWorkspaceAsync(WsIndexValue), $"Window sent to workspace {WsIndexValue}.", $"視窗已送去工作區 {WsIndexValue}。");

    private async void ApplyPad_Click(object sender, RoutedEventArgs e)
    {
        int wsPad = (int)(double.IsNaN(WsPad.Value) ? 0 : WsPad.Value);
        int contPad = (int)(double.IsNaN(ContPad.Value) ? 0 : ContPad.Value);
        await RunGuarded(async () =>
        {
            var a = await KomorebiService.FocusedWorkspacePaddingAsync(wsPad);
            var b = await KomorebiService.FocusedContainerPaddingAsync(contPad);
            return a.Success && b.Success
                ? TweakResult.Ok("Padding applied.", "間距已套用。")
                : TweakResult.Fail((a.Output ?? "") + "\n" + (b.Output ?? ""), (a.Output ?? "") + "\n" + (b.Output ?? ""));
        }, "Padding applied to focused workspace.", "間距已套用到聚焦工作區。");
    }

    // ===== rules =====

    private async void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var kind = (RuleKind.SelectedItem as ComboBoxItem)?.Tag as string ?? "workspace";
        var id = (RuleId.SelectedItem as ComboBoxItem)?.Tag as string ?? "exe";
        var val = (RuleValue.Text ?? "").Trim();
        if (val.Length == 0)
        {
            ShowWarn(P("Enter an identifier value first (e.g. firefox.exe).", "請先輸入識別碼值（例如 firefox.exe）。"));
            return;
        }
        int mon = (int)(double.IsNaN(RuleMon.Value) ? 0 : RuleMon.Value);
        int ws = (int)(double.IsNaN(RuleWs.Value) ? 0 : RuleWs.Value);

        Func<Task<TweakResult>> op = kind switch
        {
            "ignore" => () => KomorebiService.IgnoreRuleAsync(id, val),
            "manage" => () => KomorebiService.ManageRuleAsync(id, val),
            _ => () => KomorebiService.WorkspaceRuleAsync(id, val, mon, ws),
        };
        await RunGuarded(op, $"Rule added: {kind} {id} {val}.", $"已加規則：{kind} {id} {val}。");
    }

    // ===== config =====

    private async void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        var path = KomorebiService.GuessConfigPath();
        if (!File.Exists(path)) { ShowWarn(P("No config file exists yet. Use “Create defaults” first.", "暫時未有設定檔。請先用「建立預設」。")); return; }
        try { Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex.Message); }
        await Task.CompletedTask;
    }

    private async void PickConfig_Click(object sender, RoutedEventArgs e)
    {
        var picked = await FileDialogs.OpenFileAsync(".json");
        if (picked is null) return;
        try { Process.Start(new ProcessStartInfo { FileName = picked, UseShellExecute = true }); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async void ReloadConfig_Click(object sender, RoutedEventArgs e)
        => await RunGuarded(() => KomorebiService.ReloadConfigurationAsync(), "Configuration reloaded.", "設定已重新載入。");

    private async void Quickstart_Click(object sender, RoutedEventArgs e)
    {
        await RunGuarded(() => KomorebiService.QuickstartAsync(), "Example configuration gathered.", "已收集範例設定。");
        UpdateConfigPathText();
    }

    // ===== raw inspectors =====

    private async void RawState_Click(object sender, RoutedEventArgs e) => await ShowRaw(KomorebiService.RawStateAsync());
    private async void GlobalState_Click(object sender, RoutedEventArgs e) => await ShowRaw(KomorebiService.GlobalStateAsync());
    private async void Check_Click(object sender, RoutedEventArgs e) => await ShowRaw(KomorebiService.CheckAsync());

    private async Task ShowRaw(Task<TweakResult> task)
    {
        BusyRing.IsActive = true;
        try
        {
            var r = await task;
            var body = string.IsNullOrWhiteSpace(r.Output)
                ? (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? ""
                : r.Output!;
            OutBorder.Visibility = Visibility.Visible;
            OutText.Text = body.Length > 12000 ? body.Substring(0, 12000) + "\n…" : body;
        }
        catch (Exception ex) { OutBorder.Visibility = Visibility.Visible; OutText.Text = ex.Message; }
        finally { BusyRing.IsActive = false; }
    }

    // ===== ops list =====

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= KomorebiOperations.All().ToList();
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
        ResultBar.IsOpen = false;
        OpsOutBorder.Visibility = Visibility.Collapsed;
        try
        {
            var r = await def.RunAsync(CancellationToken.None);
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = r.Message is null ? string.Empty : (Loc.I.IsCantonesePrimary ? r.Message.Zh : r.Message.En);
            ResultBar.IsOpen = true;

            if (!string.IsNullOrWhiteSpace(r.Output))
            {
                var body = r.Output!;
                OpsOutText.Text = body.Length > 4000 ? body[^4000..] : body;
                OpsOutBorder.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
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
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Shared op status surfacing (persistent ResultBar) ----------------
    private void ShowApplied(TweakDefinition def)
    {
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成");
        string en = "Applied.", zh = "已套用。";
        switch (def.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ResultBar.Message = P(en, zh);
        ResultBar.IsOpen = true;
    }

    private void ShowOpError(Exception ex)
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = ex.Message;
        ResultBar.IsOpen = true;
    }

    // ===== result helpers =====

    private void ShowResult(TweakResult r, string okEn, string okZh)
    {
        if (r.Success)
        {
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Done", "完成");
            ResultBar.Message = P(okEn, okZh);
        }
        else
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            var detail = string.IsNullOrWhiteSpace(r.Output)
                ? (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? P("Command failed.", "指令失敗。")
                : r.Output!;
            ResultBar.Message = Trim(detail, 600);
        }
        ResultBar.IsOpen = true;
    }

    private void ShowWarn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private void ShowError(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Error", "錯誤");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
