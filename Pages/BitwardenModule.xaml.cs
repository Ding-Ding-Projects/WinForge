using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Bitwarden 保險庫（重做）· Bitwarden Vault, revamped — a tabbed multi-instance shell.
///
/// • 一個固定「伺服器」分頁：透過 managed <see cref="DockerService"/> 起／管理自寄存
///   <b>Vaultwarden</b> 容器（每個實例 = 獨立 compose 專案 + 具名資料卷 + 自動揀嘅空閒主機埠，
///   所以可以同時行多個）。一鍵連去任何一個。
/// • 用「＋」開任意多個<b>連線分頁</b>，每個 = 一個 <see cref="BitwardenConnectionView"/>，自己擁有
///   一個 <see cref="BitwardenService"/> 同自己嘅全部狀態 —— 完全獨立、無 static 共用 session／selection
///   （handoff 54 §3）。
///
/// • A fixed "Servers" tab manages self-hosted <b>Vaultwarden</b> containers through the managed
///   <see cref="DockerService"/> (each instance is its own compose project + named volume + auto-picked free
///   host port, so many can run at once); one click opens a connection to any of them.
/// • The "+" button opens any number of <b>connection tabs</b>, each a <see cref="BitwardenConnectionView"/>
///   that owns its own <see cref="BitwardenService"/> and all of its state — fully independent, no shared
///   static session/selection (handoff 54 §3).
///
/// 安全 · Security: master passwords / session keys never hit disk or logs; ADMIN_TOKEN is DPAPI-wrapped.
/// 失敗開放：Docker 引擎掛咗只顯示 banner，唔會卡住或閃退。 Fails open: a dead Docker engine shows a banner.
/// </summary>
public sealed partial class BitwardenModule : Page
{
    private readonly DockerService _docker = new();
    private readonly BitwardenInstanceService _instances;
    private int _newTabSeq;
    private CancellationTokenSource? _opCts;
    private bool _dockerReady;

    public BitwardenModule()
    {
        InitializeComponent();
        _instances = new BitwardenInstanceService(_docker);
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Render();
            await EnsureDockerAsync();
            RenderInstances(await ListStatesAsync());
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenModule.OnLoaded", ex); }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        try { _opCts?.Cancel(); } catch { }
        // 關閉每個連線分頁，清走佢哋嘅記憶體金鑰 · Dispose each connection tab so its in-memory keys are wiped.
        foreach (var t in Tabs.TabItems.OfType<TabViewItem>().ToList())
            if (t.Content is BitwardenConnectionView v) { try { v.Dispose(); } catch { } }
        try { _docker.Dispose(); } catch { }
    }

    private void OnLang(object? sender, EventArgs e) => SafeUi(async () =>
    {
        Render();
        RenderInstances(await ListStatesAsync());
        foreach (var t in Tabs.TabItems.OfType<TabViewItem>())
            if (t.Content is BitwardenConnectionView v) t.Header = v.TabTitle();
    });

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private bool Zh => Loc.I.IsCantonesePrimary;

    // ===================== Static text =====================

    private void Render()
    {
        HeaderTitle.Text = "Bitwarden Vault · Bitwarden 保險庫";
        HeaderBlurb.Text = P(
            "A native, end-to-end Bitwarden client built into WinForge. Open unlimited connection tabs (each fully independent), or spin up your own self-hosted Vaultwarden server with Docker and connect to it in one click. Keys live in memory only and are wiped on lock.",
            "WinForge 內建嘅原生、端對端 Bitwarden 用戶端。可以開無限個連線分頁（每個完全獨立），或者用 Docker 起你自己嘅自寄存 Vaultwarden 伺服器，一鍵連過去。金鑰只留喺記憶體，鎖定即清除。");

        ServersTitle.Text = P("Self-hosted servers (Vaultwarden via Docker)", "自寄存伺服器（用 Docker 起 Vaultwarden）");
        ServersBlurb.Text = P(
            "Run your own Bitwarden-compatible server locally. Each instance gets its own data volume and host port, so you can run several at once. Requires a running Docker engine.",
            "喺本機行你自己嘅 Bitwarden 相容伺服器。每個實例有自己嘅資料卷同主機埠，可以同時行幾個。需要 Docker 引擎行緊。");

        NewServerTitle.Text = P("New Vaultwarden instance", "新 Vaultwarden 實例");
        NewNameBox.Header = P("Display name (optional)", "顯示名（可選）");
        NewNameBox.PlaceholderText = P("My Vaultwarden", "我嘅 Vaultwarden");
        SignupsToggle.Content = P("Allow sign-ups (needed to create your first account)", "允許註冊（首次建立帳戶需要）");
        WebsocketToggle.Content = P("Enable WebSocket notifications", "啟用 WebSocket 通知");
        CreateServerBtn.Content = P("Create & start", "建立並啟動");
        RefreshServersBtn.Content = P("Refresh", "重新整理");
        InstancesHeader.Text = P("Instances", "實例");
        NoInstancesHint.Text = P("No instances yet. Create one above.", "未有實例。喺上面建立一個。");
        LogHeader.Text = P("Docker activity", "Docker 活動");

        ServersTab.Header = P("Servers", "伺服器");
        ToolTipService.SetToolTip(Tabs, P("New connection", "新連線"));
    }

    // ===================== Docker engine =====================

    private async Task EnsureDockerAsync()
    {
        try
        {
            await _docker.ConnectAsync();
            _dockerReady = true;
            DockerBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            _dockerReady = false;
            CrashLogger.Log("BitwardenModule.EnsureDocker", ex);
            DockerBar.Severity = InfoBarSeverity.Warning;
            DockerBar.Title = P("Docker engine not reachable", "連唔到 Docker 引擎");
            DockerBar.Message = P(
                "Start Docker Desktop (or the Docker engine) to create and manage self-hosted Vaultwarden servers. You can still open connections to existing servers above.",
                "啟動 Docker Desktop（或 Docker 引擎）先可以建立同管理自寄存 Vaultwarden 伺服器。你仍然可以喺上面開連線去現有伺服器。");
            DockerBar.IsOpen = true;
        }
    }

    private async Task<List<(BitwardenInstanceService.Instance inst, BitwardenInstanceService.RunState state)>> ListStatesAsync()
    {
        var list = _instances.LoadInstances();
        var result = new List<(BitwardenInstanceService.Instance, BitwardenInstanceService.RunState)>();
        foreach (var i in list)
        {
            var st = _dockerReady
                ? await _instances.GetStateAsync(i)
                : BitwardenInstanceService.RunState.Unknown;
            result.Add((i, st));
        }
        return result;
    }

    // ===================== Instances UI =====================

    private void RenderInstances(List<(BitwardenInstanceService.Instance inst, BitwardenInstanceService.RunState state)> states)
    {
        InstancesPanel.Children.Clear();
        NoInstancesHint.Visibility = states.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (inst, state) in states)
            InstancesPanel.Children.Add(BuildInstanceCard(inst, state));
    }

    private Border BuildInstanceCard(BitwardenInstanceService.Instance inst, BitwardenInstanceService.RunState state)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new StackPanel { Spacing = 2 };
        info.Children.Add(new TextBlock { Text = inst.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var sub = new TextBlock { Text = $"{inst.LocalUrl}  ·  {StateLabel(state)}", FontSize = 12 };
        if (TryBrush("TextFillColorSecondaryBrush") is { } sec) sub.Foreground = sec;
        info.Children.Add(sub);
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

        var connectBtn = new Button { Content = P("Connect", "連線") };
        connectBtn.Click += (_, _) => SafeUi(() => { OpenConnectionTab(P("Self-hosted", "自寄存") + " · " + inst.DisplayName, inst.LocalUrl); return Task.CompletedTask; });
        actions.Children.Add(connectBtn);

        if (state == BitwardenInstanceService.RunState.Running)
        {
            var stopBtn = new Button { Content = P("Stop", "停止") };
            stopBtn.Click += (_, _) => SafeUi(() => StopInstance(inst));
            actions.Children.Add(stopBtn);
        }
        else
        {
            var startBtn = new Button { Content = P("Start", "啟動"), IsEnabled = _dockerReady };
            startBtn.Click += (_, _) => SafeUi(() => StartInstance(inst));
            actions.Children.Add(startBtn);
        }

        var copyTokenBtn = new Button { Content = P("Copy admin token", "複製管理權杖") };
        copyTokenBtn.Click += (_, _) => SafeUi(() => { CopyAdminToken(inst); return Task.CompletedTask; });
        actions.Children.Add(copyTokenBtn);

        var removeBtn = new Button { Content = P("Remove", "移除") };
        removeBtn.Click += (_, _) => SafeUi(() => RemoveInstance(inst));
        actions.Children.Add(removeBtn);

        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        var border = new Border
        {
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
        if (TryBrush("CardBackgroundFillColorDefaultBrush") is { } cb) border.Background = cb;
        if (TryBrush("CardStrokeColorDefaultBrush") is { } cs) border.BorderBrush = cs;
        return border;
    }

    private string StateLabel(BitwardenInstanceService.RunState s) => s switch
    {
        BitwardenInstanceService.RunState.Running => P("running", "行緊"),
        BitwardenInstanceService.RunState.Stopped => P("stopped", "已停止"),
        BitwardenInstanceService.RunState.NotCreated => P("not created", "未建立"),
        _ => P("unknown", "未知"),
    };

    // ===================== Instance actions =====================

    private async void CreateServer_Click(object sender, RoutedEventArgs e) => await SafeUiAsync(async () =>
    {
        if (!_dockerReady)
        {
            await EnsureDockerAsync();
            if (!_dockerReady) { Log(P("Docker engine is not reachable.", "連唔到 Docker 引擎。")); return; }
        }
        ServerBusy.IsActive = true;
        CreateServerBtn.IsEnabled = false;
        ClearLog();
        _opCts = new CancellationTokenSource();
        try
        {
            var req = new BitwardenInstanceService.CreateRequest(
                NewNameBox.Text, SignupsToggle.IsChecked == true, WebsocketToggle.IsChecked == true);
            var outcome = await _instances.CreateAsync(req, LogProgress(), _opCts.Token);
            Log((Zh ? outcome.Message?.Zh : outcome.Message?.En) ?? "");
            if (outcome.Success && outcome.Instance is { } inst)
            {
                NewNameBox.Text = "";
                if (outcome.AdminToken is { Length: > 0 } tok) await ShowAdminTokenAsync(inst, tok);
                RenderInstances(await ListStatesAsync());
            }
        }
        finally { ServerBusy.IsActive = false; CreateServerBtn.IsEnabled = true; }
    });

    private async void RefreshServers_Click(object sender, RoutedEventArgs e) => await SafeUiAsync(async () =>
    {
        await EnsureDockerAsync();
        RenderInstances(await ListStatesAsync());
    });

    private async Task StartInstance(BitwardenInstanceService.Instance inst)
    {
        ClearLog();
        _opCts = new CancellationTokenSource();
        var r = await _instances.StartAsync(inst, LogProgress(), _opCts.Token);
        Log((Zh ? r.Message?.Zh : r.Message?.En) ?? "");
        RenderInstances(await ListStatesAsync());
    }

    private async Task StopInstance(BitwardenInstanceService.Instance inst)
    {
        var r = await _instances.StopAsync(inst, (_opCts = new CancellationTokenSource()).Token);
        Log((Zh ? r.Message?.Zh : r.Message?.En) ?? "");
        RenderInstances(await ListStatesAsync());
    }

    private async Task RemoveInstance(BitwardenInstanceService.Instance inst)
    {
        var deleteData = await ConfirmRemoveAsync(inst);
        if (deleteData is null) return; // cancelled
        ClearLog();
        _opCts = new CancellationTokenSource();
        var r = await _instances.RemoveAsync(inst, deleteData.Value, LogProgress(), _opCts.Token);
        Log((Zh ? r.Message?.Zh : r.Message?.En) ?? "");
        RenderInstances(await ListStatesAsync());
    }

    private async Task<bool?> ConfirmRemoveAsync(BitwardenInstanceService.Instance inst)
    {
        var dataCheck = new CheckBox { Content = P("Also delete the data volume (irreversible)", "同時刪除資料卷（不可復原）"), IsChecked = false };
        var content = new StackPanel { Spacing = 10 };
        content.Children.Add(new TextBlock
        {
            Text = P($"Remove the instance '{inst.DisplayName}'? Its container will be stopped and removed.",
                     $"移除實例「{inst.DisplayName}」？佢嘅容器會被停止同移除。"),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(dataCheck);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Remove instance?", "移除實例？"),
            Content = content,
            PrimaryButtonText = P("Remove", "移除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        var res = await ShowDialogAsync(dlg);
        if (res != ContentDialogResult.Primary) return null;
        return dataCheck.IsChecked == true;
    }

    private async Task ShowAdminTokenAsync(BitwardenInstanceService.Instance inst, string token)
    {
        var tokenBox = new TextBox { Text = token, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas") };
        var content = new StackPanel { Spacing = 10, MinWidth = 380 };
        content.Children.Add(new TextBlock
        {
            Text = P($"Your Vaultwarden server is at {inst.LocalUrl}. Save this admin token now — it opens the /admin panel. WinForge stores it encrypted (DPAPI), but copy it somewhere safe.",
                     $"你嘅 Vaultwarden 伺服器喺 {inst.LocalUrl}。而家儲存呢個管理權杖 —— 佢用嚟開 /admin 面板。WinForge 會加密（DPAPI）儲存，但都複製去安全嘅地方。"),
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(tokenBox);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Admin token", "管理權杖"),
            Content = content,
            PrimaryButtonText = P("Copy", "複製"),
            CloseButtonText = P("Done", "完成"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await ShowDialogAsync(dlg) == ContentDialogResult.Primary) CopyToClipboard(token);
    }

    private void CopyAdminToken(BitwardenInstanceService.Instance inst)
    {
        var tok = _instances.GetAdminToken(inst.ProjectName);
        if (string.IsNullOrEmpty(tok)) { Log(P("No admin token stored for this instance.", "呢個實例無儲存管理權杖。")); return; }
        CopyToClipboard(tok);
        Log(P("Admin token copied.", "已複製管理權杖。"));
    }

    private static void CopyToClipboard(string value)
    {
        try { var dp = new DataPackage(); dp.SetText(value); Clipboard.SetContent(dp); }
        catch (Exception ex) { CrashLogger.Log("BitwardenModule.Copy", ex); }
    }

    // ===================== Connection tabs =====================

    private void Tabs_AddTabButtonClick(TabView sender, object args)
        => SafeUi(() => { OpenConnectionTab(P("Connection", "連線") + " " + (++_newTabSeq)); return Task.CompletedTask; });

    private void OpenConnectionTab(string title, string? seedServerUrl = null)
    {
        var view = new BitwardenConnectionView(title, seedServerUrl);
        var tab = new TabViewItem
        {
            Header = view.DisplayName,
            Content = view,
            IconSource = new FontIconSource { Glyph = "" },
        };
        view.TitleChanged += v => { try { tab.Header = v.TabTitle(); } catch { } };
        Tabs.TabItems.Add(tab);
        Tabs.SelectedItem = tab;
    }

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        try
        {
            if (args.Tab.Content is BitwardenConnectionView v) v.Dispose();
            sender.TabItems.Remove(args.Tab);
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenModule.TabClose", ex); }
    }

    // ===================== Log =====================

    private IProgress<string> LogProgress() => new Progress<string>(Log);

    private void ClearLog() => LogText.Text = "";

    private void Log(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        LogText.Text = string.IsNullOrEmpty(LogText.Text) ? line : LogText.Text + "\n" + line;
        try { LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null); } catch { }
    }

    // ===================== Helpers =====================

    private async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("BitwardenModule.ShowDialog", ex); return ContentDialogResult.None; }
    }

    private void SafeUi(Func<Task> work)
    {
        _ = SafeUiAsync(work);
    }

    private async Task SafeUiAsync(Func<Task> work)
    {
        try { await work(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CrashLogger.Log("BitwardenModule", ex); }
    }

    private static Brush? TryBrush(string key)
    {
        try { return Application.Current.Resources[key] as Brush; }
        catch { return null; }
    }
}
