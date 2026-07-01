using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 Docker 容器管理模組 · Native Docker container management module (Portainer-style).
///
/// 同 qBittorrent 模組一樣，係「原生客戶端對本機 API」嘅做法：用 managed .NET 程式庫 Docker.DotNet
/// 直接打本機 Docker Engine 嘅 REST API（npipe://./pipe/docker_engine，或 tcp://localhost:2375），
/// 絕對【唔會】shell out 去 docker / docker compose CLI、唔啟動 Docker Desktop、亦唔夾帶安裝程式。
/// 一定要本機有 Docker daemon 行緊先用得；連唔到就顯示雙語 InfoBar 提示用戶開啟 Docker 再重新整理。
///
/// Like the qBittorrent module, this is a native-client-over-a-local-API design: it uses the managed
/// .NET library Docker.DotNet to call the local Docker Engine REST API directly. It NEVER shells out to
/// the docker/docker-compose CLI, never launches Docker Desktop, and bundles no installer. A running
/// local daemon is required; if unreachable, a bilingual InfoBar asks the user to start Docker + Refresh.
///
/// Containers (list / start / stop / restart / pause / unpause / remove / logs / exec / live stats /
/// inspect), Images (list / pull / remove / prune), Volumes, Networks, and Compose (managed YAML →
/// Docker.DotNet API) — everything in-app, bilingual.
/// </summary>
public sealed partial class DockerModule : Page
{
    private readonly DockerService _docker = new();
    private readonly ObservableCollection<ContainerVM> _containers = new();
    private readonly ObservableCollection<ImageVM> _images = new();
    private readonly ObservableCollection<VolumeVM> _volumes = new();
    private readonly ObservableCollection<NetworkVM> _networks = new();
    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromMilliseconds(2000) };
    private CancellationTokenSource? _statsCts;
    private string? _selectedContainerId;
    private string? _composePath;
    private bool _busy;

    public DockerModule()
    {
        InitializeComponent();
        ContainerList.ItemsSource = _containers;
        ImageList.ItemsSource = _images;
        VolumeList.ItemsSource = _volumes;
        NetworkList.ItemsSource = _networks;
        _statsTimer.Tick += async (_, _) => await StatsTick();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private async void OnLoaded(object? s, RoutedEventArgs e)
    {
        EndpointBox.Text = _docker.Endpoint;
        ComposeNameBox.Text = "app";
        Render();
        await Connect();
    }

    private void OnUnloaded(object? s, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _statsTimer.Stop();
        _statsCts?.Cancel();
        _docker.Dispose();
    }

    private void Render()
    {
        Header.Title = "Docker · Docker 容器管理";
        HeaderBlurb.Text = P("Full container management over the local Docker Engine REST API — a managed .NET client (Docker.DotNet) talking to npipe://./pipe/docker_engine, the same native-client pattern as the qBittorrent module. List and control containers, view logs, exec commands, watch live CPU/memory, manage images, volumes and networks, and bring docker-compose stacks up/down — all in-app, never shelling out to the docker CLI. Requires a running Docker daemon.",
            "喺本機 Docker Engine REST API 上面做完整容器管理 — 用 managed .NET 客戶端（Docker.DotNet）打 npipe://./pipe/docker_engine，同 qBittorrent 模組一樣係原生客戶端模式。列出同控制容器、睇 log、exec 指令、睇實時 CPU／記憶體、管理映像／磁碟區／網路，仲可以將 docker-compose 堆疊 up／down — 全部喺 app 內做，唔會行 docker CLI。需要本機有 Docker daemon 行緊。");

        RefreshLbl.Text = P("Refresh", "重新整理");
        DisconnectBtn.Content = P("Disconnect · 中斷", "中斷");
        EndpointBox.Header = P("Engine endpoint · 引擎端點", "引擎端點");

        ContainersTab.Header = P("Containers · 容器", "容器");
        ImagesTab.Header = P("Images · 映像", "映像");
        VolumesTab.Header = P("Volumes · 磁碟區", "磁碟區");
        NetworksTab.Header = P("Networks · 網路", "網路");
        ComposeTab.Header = P("Compose · 堆疊", "Compose");

        CStartLbl.Text = P("Start", "啟動");
        CStopLbl.Text = P("Stop", "停止");
        CRestartLbl.Text = P("Restart", "重啟");
        CPauseLbl.Text = P("Pause", "暫停");
        CUnpauseLbl.Text = P("Unpause", "繼續");
        CLogsLbl.Text = P("Logs", "日誌");
        CExecLbl.Text = P("Exec", "執行");
        CRemoveLbl.Text = P("Remove", "移除");

        PullLbl.Text = P("Pull", "拉取");
        ImgPruneLbl.Text = P("Prune dangling", "清理懸空");
        VolCreateLbl.Text = P("Create", "建立");
        VolPruneLbl.Text = P("Prune", "清理");
        NetCreateLbl.Text = P("Create", "建立");
        NetPruneLbl.Text = P("Prune", "清理");

        ComposeBlurb.Text = P("Pick a docker-compose.yml. WinForge parses it in managed C# (YamlDotNet) and creates/starts each service via the Docker.DotNet API — it does NOT run `docker compose`. Supported: image, ports, environment, volumes, depends_on ordering. Unsupported fields are listed as warnings.",
            "揀一個 docker-compose.yml。WinForge 用 managed C#（YamlDotNet）解析，再經 Docker.DotNet API 建立／啟動每個服務 — 唔會行 `docker compose`。支援：image、ports、environment、volumes、depends_on 排序。未支援嘅欄位會列做警告。");
        ComposePickLbl.Text = P("Pick file…", "揀檔案…");
        ComposeNameBox.Header = P("Project name · 專案名稱", "專案名稱");
        ComposeUpLbl.Text = P("Up", "啟動");
        ComposeDownLbl.Text = P("Down", "停止");
        ComposeLogLbl.Text = P("Output · 輸出", "輸出");

        ContainerEmptyTxt.Text = _docker.Connected
            ? P("No containers. Pull an image and run one, or bring up a compose stack.", "冇容器。拉個映像嚟跑，或者啟動一個 compose 堆疊。")
            : P("Not connected to a Docker daemon.", "未連到 Docker daemon。");
    }

    // ── connection / engine detection ───────────────────────────────────────────

    private async Task Connect()
    {
        Busy.IsActive = true;
        try
        {
            var ver = await _docker.ConnectAsync(EndpointBox.Text);
            _docker.SaveEndpoint(EndpointBox.Text);
            EngineBar.IsOpen = false;
            SummaryCard.Visibility = Visibility.Visible;
            DisconnectBtn.IsEnabled = true;
            await RefreshAll();
            _statsTimer.Start();
        }
        catch (Exception ex)
        {
            ShowDaemonUnreachable(ex);
        }
        finally { Busy.IsActive = false; }
    }

    private void ShowDaemonUnreachable(Exception ex)
    {
        _statsTimer.Stop();
        SummaryCard.Visibility = Visibility.Collapsed;
        DisconnectBtn.IsEnabled = false;
        _containers.Clear(); _images.Clear(); _volumes.Clear(); _networks.Clear();
        UpdateEmptyState();
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Docker daemon not reachable", "連唔到 Docker daemon");
        EngineBar.Message = P($"Could not connect to the Docker Engine at {_docker.Endpoint}. Start Docker (Docker Desktop or the Docker Engine service) and click Refresh. WinForge talks to the engine's local API directly and cannot start the daemon for you. ({ex.Message})",
            $"連唔到 {_docker.Endpoint} 嘅 Docker Engine。請啟動 Docker（Docker Desktop 或 Docker Engine 服務）再撳「重新整理」。WinForge 直接打引擎嘅本機 API，唔可以幫你開 daemon。（{ex.Message}）");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (!_docker.Connected) { await Connect(); return; }
        await RefreshAll();
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        _statsTimer.Stop();
        _docker.Disconnect();
        _containers.Clear(); _images.Clear(); _volumes.Clear(); _networks.Clear();
        SummaryCard.Visibility = Visibility.Collapsed;
        ContainerDetail.Visibility = Visibility.Collapsed;
        DisconnectBtn.IsEnabled = false;
        UpdateEmptyState();
        Notify(InfoBarSeverity.Informational, P("Disconnected.", "已中斷。"));
    }

    private async Task RefreshAll()
    {
        if (!_docker.Connected) return;
        await RefreshContainers();
        await RefreshImages();
        await RefreshVolumes();
        await RefreshNetworks();
        await UpdateSummary();
    }

    private async Task UpdateSummary()
    {
        try
        {
            int running = _containers.Count(c => c.IsRunning);
            SumVersion.Text = P($"Engine {_docker.Version}", $"引擎 {_docker.Version}");
            SumContainers.Text = P($"{running}/{_containers.Count} running", $"{running}/{_containers.Count} 運行中");
            SumImages.Text = P($"{_images.Count} images", $"{_images.Count} 個映像");
        }
        catch { }
        await Task.CompletedTask;
    }

    // ── containers ────────────────────────────────────────────────────────────────

    private async Task RefreshContainers()
    {
        try
        {
            var list = await _docker.ListContainersAsync(true);
            var keepId = _selectedContainerId;
            _containers.Clear();
            foreach (var c in list.OrderBy(c => c.Names.FirstOrDefault()))
                _containers.Add(new ContainerVM(c));
            UpdateEmptyState();
            if (keepId is not null)
            {
                var match = _containers.FirstOrDefault(c => c.Id == keepId);
                if (match is not null) ContainerList.SelectedItem = match;
                else { _selectedContainerId = null; ContainerDetail.Visibility = Visibility.Collapsed; }
            }
            UpdateContainerButtons();
        }
        catch (Exception ex) { ShowDaemonUnreachable(ex); }
    }

    private void UpdateEmptyState()
    {
        bool empty = _containers.Count == 0;
        ContainerEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ContainerList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ContainerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedContainerId = (ContainerList.SelectedItem as ContainerVM)?.Id;
        UpdateContainerButtons();
        if (_selectedContainerId is not null) _ = ShowInspect(_selectedContainerId);
        else ContainerDetail.Visibility = Visibility.Collapsed;
    }

    private void UpdateContainerButtons()
    {
        var vm = ContainerList.SelectedItem as ContainerVM;
        bool any = vm is not null && _docker.Connected;
        CStartBtn.IsEnabled = any && !vm!.IsRunning;
        CStopBtn.IsEnabled = any && vm!.IsRunning;
        CRestartBtn.IsEnabled = any;
        CPauseBtn.IsEnabled = any && vm!.IsRunning && !vm.IsPaused;
        CUnpauseBtn.IsEnabled = any && vm!.IsPaused;
        CLogsBtn.IsEnabled = any;
        CExecBtn.IsEnabled = any && vm!.IsRunning;
        CRemoveBtn.IsEnabled = any;
    }

    private async Task ContainerAct(Func<string, Task> action, string okEn, string okZh)
    {
        var vm = ContainerList.SelectedItem as ContainerVM;
        if (vm is null || _busy) return;
        _busy = true; Busy.IsActive = true;
        try
        {
            await action(vm.Id);
            Notify(InfoBarSeverity.Success, P(okEn, okZh));
            await RefreshContainers();
            await UpdateSummary();
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { _busy = false; Busy.IsActive = false; }
    }

    private async void CStart_Click(object s, RoutedEventArgs e) => await ContainerAct(id => _docker.StartContainerAsync(id), "Started.", "已啟動。");
    private async void CStop_Click(object s, RoutedEventArgs e) => await ContainerAct(id => _docker.StopContainerAsync(id), "Stopped.", "已停止。");
    private async void CRestart_Click(object s, RoutedEventArgs e) => await ContainerAct(id => _docker.RestartContainerAsync(id), "Restarted.", "已重啟。");
    private async void CPause_Click(object s, RoutedEventArgs e) => await ContainerAct(id => _docker.PauseContainerAsync(id), "Paused.", "已暫停。");
    private async void CUnpause_Click(object s, RoutedEventArgs e) => await ContainerAct(id => _docker.UnpauseContainerAsync(id), "Unpaused.", "已繼續。");

    private async void CRemove_Click(object sender, RoutedEventArgs e)
    {
        var vm = ContainerList.SelectedItem as ContainerVM;
        if (vm is null) return;
        var forceChk = new CheckBox { Content = P("Force remove (kill if running)", "強制移除（運行中亦會 kill）"), IsChecked = vm.IsRunning };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Remove container {vm.Name}?", $"移除容器 {vm.Name}？"),
            Content = new StackPanel { Spacing = 10, Children =
            {
                new TextBlock { Text = P("This removes the container. Volumes are kept.", "呢個會移除容器，磁碟區會保留。"), TextWrapping = TextWrapping.Wrap },
                forceChk,
            } },
            PrimaryButtonText = P("Remove · 移除", "移除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        await ContainerAct(id => _docker.RemoveContainerAsync(id, forceChk.IsChecked == true), "Removed.", "已移除。");
    }

    private async void CLogs_Click(object sender, RoutedEventArgs e)
    {
        var vm = ContainerList.SelectedItem as ContainerVM;
        if (vm is null) return;
        var box = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, MinWidth = 640, Height = 360,
            Text = P("Loading…", "載入中…"),
        };
        var refreshBtn = new Button { Content = P("Refresh · 重新整理", "重新整理") };
        async Task Load()
        {
            try { box.Text = await _docker.GetLogsAsync(vm.Id, 500); if (string.IsNullOrWhiteSpace(box.Text)) box.Text = P("(no log output)", "（冇日誌輸出）"); }
            catch (Exception ex) { box.Text = ex.Message; }
        }
        refreshBtn.Click += async (_, _) => await Load();
        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(refreshBtn);
        panel.Children.Add(box);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P($"Logs · {vm.Name}", $"日誌 · {vm.Name}"),
            Content = panel, CloseButtonText = P("Close · 關閉", "關閉"),
        };
        _ = Load();
        await dlg.ShowAsync();
    }

    private async void CExec_Click(object sender, RoutedEventArgs e)
    {
        var vm = ContainerList.SelectedItem as ContainerVM;
        if (vm is null) return;
        var cmdBox = new TextBox { PlaceholderText = "ls -la /  ·  env  ·  cat /etc/hostname", Text = "ls -la" };
        var outBox = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"), FontSize = 12, MinWidth = 640, Height = 280,
        };
        var runBtn = new Button { Content = P("Run · 執行", "執行"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        runBtn.Click += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(cmdBox.Text)) return;
            outBox.Text = P("Running…", "執行中…");
            try { outBox.Text = await _docker.ExecAsync(vm.Id, cmdBox.Text.Trim()); if (string.IsNullOrWhiteSpace(outBox.Text)) outBox.Text = P("(no output)", "（冇輸出）"); }
            catch (Exception ex) { outBox.Text = ex.Message; }
        };
        var panel = new StackPanel { Spacing = 8, MinWidth = 640 };
        panel.Children.Add(new TextBlock { Text = P("Runs via: /bin/sh -c \"<command>\" inside the container.", "用容器內嘅 /bin/sh -c \"<指令>\" 執行。"), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(cmdBox);
        panel.Children.Add(runBtn);
        panel.Children.Add(outBox);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P($"Exec · {vm.Name}", $"執行 · {vm.Name}"),
            Content = panel, CloseButtonText = P("Close · 關閉", "關閉"),
        };
        await dlg.ShowAsync();
    }

    private async Task ShowInspect(string id)
    {
        try
        {
            var info = await _docker.InspectContainerAsync(id);
            if (_selectedContainerId != id) return;
            ContainerDetail.Visibility = Visibility.Visible;
            DetailName.Text = (info.Name ?? id).TrimStart('/');
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(P("Image: ", "映像：") + (info.Config?.Image ?? ""));
            sb.AppendLine(P("State: ", "狀態：") + (info.State?.Status ?? "") + (info.State?.Running == true ? "" : ""));
            if (info.Config?.Env is { Count: > 0 } env)
            {
                sb.AppendLine(P("Env:", "環境變數："));
                foreach (var ev in env) sb.AppendLine("  " + ev);
            }
            if (info.Mounts is { Count: > 0 } mounts)
            {
                sb.AppendLine(P("Mounts:", "掛載："));
                foreach (var m in mounts) sb.AppendLine($"  {m.Source} → {m.Destination} ({m.Mode})");
            }
            if (info.NetworkSettings?.Ports is { Count: > 0 } ports)
            {
                sb.AppendLine(P("Ports:", "連接埠："));
                foreach (var kv in ports)
                {
                    var binds = kv.Value is null ? "" : string.Join(", ", kv.Value.Select(b => $"{b.HostIP}:{b.HostPort}"));
                    sb.AppendLine($"  {kv.Key} {(binds.Length > 0 ? "→ " + binds : "")}");
                }
            }
            DetailBox.Text = sb.ToString();
        }
        catch { }
    }

    // ── live stats for the selected container ──────────────────────────────────────

    private async Task StatsTick()
    {
        if (!_docker.Connected || _selectedContainerId is null) return;
        var vm = ContainerList.SelectedItem as ContainerVM;
        if (vm is null || !vm.IsRunning) { DetailStats.Text = ""; return; }
        _statsCts?.Cancel();
        _statsCts = new CancellationTokenSource();
        try
        {
            var sample = await _docker.GetStatsOnceAsync(_selectedContainerId, _statsCts.Token);
            if (sample is not null && _selectedContainerId == vm.Id)
                DetailStats.Text = P($"CPU {sample.CpuPercent:0.0}%  ·  MEM {DockerService.HumanSize(sample.MemUsage)} / {DockerService.HumanSize(sample.MemLimit)} ({sample.MemPercent:0.0}%)",
                    $"CPU {sample.CpuPercent:0.0}%  ·  記憶體 {DockerService.HumanSize(sample.MemUsage)} / {DockerService.HumanSize(sample.MemLimit)} ({sample.MemPercent:0.0}%)");
        }
        catch { }
    }

    // ── images ──────────────────────────────────────────────────────────────────────

    private async Task RefreshImages()
    {
        try
        {
            var list = await _docker.ListImagesAsync();
            _images.Clear();
            foreach (var img in list.OrderByDescending(i => i.Created))
                _images.Add(new ImageVM(img));
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
    }

    private async void Pull_Click(object sender, RoutedEventArgs e)
    {
        var name = (PullBox.Text ?? "").Trim();
        if (name.Length == 0) return;
        var progress = new TextBlock { Text = P("Starting…", "開始…"), FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P($"Pulling {name}", $"拉取 {name}"),
            Content = new ScrollViewer { Content = progress, MinWidth = 460, MaxHeight = 200 },
            CloseButtonText = P("Close · 關閉", "關閉"),
        };
        var rep = new Progress<string>(line => DispatcherQueue.TryEnqueue(() => progress.Text = line));
        var pullTask = Task.Run(async () =>
        {
            try { await _docker.PullImageAsync(name, rep); return (true, ""); }
            catch (Exception ex) { return (false, ex.Message); }
        });
        _ = pullTask.ContinueWith(async t =>
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                var (ok, err) = t.Result;
                progress.Text = ok ? P("Done.", "完成。") : err;
                if (ok) await RefreshImages();
                await UpdateSummary();
            });
            await Task.CompletedTask;
        });
        await dlg.ShowAsync();
    }

    private async void ImgRemove_Click(object sender, RoutedEventArgs e)
    {
        if (ImageList.SelectedItem is not ImageVM vm) return;
        if (!await Confirm(P($"Remove image {vm.Repo}?", $"移除映像 {vm.Repo}？"),
            P("Force removal will delete even if containers reference it.", "強制移除即使有容器引用都會刪除。"), P("Remove", "移除"))) return;
        Busy.IsActive = true;
        try { await _docker.RemoveImageAsync(vm.Id, true); Notify(InfoBarSeverity.Success, P("Removed.", "已移除。")); await RefreshImages(); await UpdateSummary(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    private async void ImgPrune_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        try { var freed = await _docker.PruneImagesAsync(); Notify(InfoBarSeverity.Success, P($"Pruned. Reclaimed {DockerService.HumanSize(freed)}.", $"已清理，釋放 {DockerService.HumanSize(freed)}。")); await RefreshImages(); await UpdateSummary(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    // ── volumes ──────────────────────────────────────────────────────────────────────

    private async Task RefreshVolumes()
    {
        try
        {
            var list = await _docker.ListVolumesAsync();
            _volumes.Clear();
            foreach (var v in list.OrderBy(v => v.Name)) _volumes.Add(new VolumeVM(v));
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
    }

    private async void VolCreate_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptText(P("Create volume", "建立磁碟區"), P("Volume name", "磁碟區名稱"), "");
        if (string.IsNullOrWhiteSpace(name)) return;
        Busy.IsActive = true;
        try { await _docker.CreateVolumeAsync(name.Trim()); Notify(InfoBarSeverity.Success, P("Created.", "已建立。")); await RefreshVolumes(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    private async void VolRemove_Click(object sender, RoutedEventArgs e)
    {
        if (VolumeList.SelectedItem is not VolumeVM vm) return;
        if (!await Confirm(P($"Remove volume {vm.Name}?", $"移除磁碟區 {vm.Name}？"), P("Data in the volume is permanently deleted.", "磁碟區內嘅資料會永久刪除。"), P("Remove", "移除"))) return;
        Busy.IsActive = true;
        try { await _docker.RemoveVolumeAsync(vm.Name, true); Notify(InfoBarSeverity.Success, P("Removed.", "已移除。")); await RefreshVolumes(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    private async void VolPrune_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        try { var freed = await _docker.PruneVolumesAsync(); Notify(InfoBarSeverity.Success, P($"Pruned. Reclaimed {DockerService.HumanSize(freed)}.", $"已清理，釋放 {DockerService.HumanSize(freed)}。")); await RefreshVolumes(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    // ── networks ──────────────────────────────────────────────────────────────────────

    private async Task RefreshNetworks()
    {
        try
        {
            var list = await _docker.ListNetworksAsync();
            _networks.Clear();
            foreach (var n in list.OrderBy(n => n.Name)) _networks.Add(new NetworkVM(n));
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
    }

    private async void NetCreate_Click(object sender, RoutedEventArgs e)
    {
        var nameBox = new TextBox { Header = P("Network name · 網路名稱", "網路名稱") };
        var driverBox = new ComboBox { Header = P("Driver · 驅動", "驅動"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var d in new[] { "bridge", "host", "overlay", "macvlan", "none" }) driverBox.Items.Add(new ComboBoxItem { Content = d, Tag = d });
        driverBox.SelectedIndex = 0;
        var panel = new StackPanel { Spacing = 10, MinWidth = 360 };
        panel.Children.Add(nameBox); panel.Children.Add(driverBox);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("Create network", "建立網路"), Content = panel,
            PrimaryButtonText = P("Create · 建立", "建立"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(nameBox.Text)) return;
        Busy.IsActive = true;
        try { await _docker.CreateNetworkAsync(nameBox.Text.Trim(), (driverBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "bridge"); Notify(InfoBarSeverity.Success, P("Created.", "已建立。")); await RefreshNetworks(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    private async void NetRemove_Click(object sender, RoutedEventArgs e)
    {
        if (NetworkList.SelectedItem is not NetworkVM vm) return;
        if (!await Confirm(P($"Remove network {vm.Name}?", $"移除網路 {vm.Name}？"), P("Built-in networks cannot be removed.", "內建網路無法移除。"), P("Remove", "移除"))) return;
        Busy.IsActive = true;
        try { await _docker.RemoveNetworkAsync(vm.Id); Notify(InfoBarSeverity.Success, P("Removed.", "已移除。")); await RefreshNetworks(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    private async void NetPrune_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        try { var n = await _docker.PruneNetworksAsync(); Notify(InfoBarSeverity.Success, P($"Pruned {n} network(s).", $"清理咗 {n} 個網路。")); await RefreshNetworks(); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; }
    }

    // ── compose ──────────────────────────────────────────────────────────────────────

    private async void ComposePick_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".yml", ".yaml");
        if (string.IsNullOrEmpty(path)) return;
        _composePath = path;
        ComposePathBox.Text = path;
        // Default project name from the parent folder.
        var folder = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
        if (!string.IsNullOrWhiteSpace(folder)) ComposeNameBox.Text = folder;
        // Parse immediately to show warnings.
        try
        {
            var proj = ComposeParser.Parse(await File.ReadAllTextAsync(path), ComposeNameBox.Text);
            ShowComposeWarnings(proj);
            ComposeLog($"Parsed {proj.Services.Count} service(s): {string.Join(", ", proj.Services.Select(s => s.Name))} · 已解析 {proj.Services.Count} 個服務", reset: true);
        }
        catch (Exception ex) { ComposeLog("Parse error: " + ex.Message, reset: true); }
    }

    private void ShowComposeWarnings(ComposeProject proj)
    {
        if (proj.Warnings.Count == 0) { ComposeWarnBar.IsOpen = false; return; }
        ComposeWarnBar.IsOpen = true;
        ComposeWarnBar.Severity = InfoBarSeverity.Warning;
        ComposeWarnBar.Title = P($"{proj.Warnings.Count} compose field(s) not fully applied", $"{proj.Warnings.Count} 個 compose 欄位未完全套用");
        ComposeWarnBar.Message = string.Join("\n", proj.Warnings);
    }

    private async void ComposeUp_Click(object sender, RoutedEventArgs e)
    {
        if (!_docker.Connected) { Notify(InfoBarSeverity.Warning, P("Connect to Docker first.", "請先連到 Docker。")); return; }
        if (_composePath is null || !File.Exists(_composePath)) { Notify(InfoBarSeverity.Warning, P("Pick a docker-compose.yml first.", "請先揀一個 docker-compose.yml。")); return; }
        ComposeProject proj;
        try { proj = ComposeParser.Parse(await File.ReadAllTextAsync(_composePath), ComposeNameBox.Text); }
        catch (Exception ex) { ComposeLog("Parse error: " + ex.Message, reset: true); return; }
        ShowComposeWarnings(proj);
        if (proj.Services.Count == 0) { ComposeLog(P("No usable services to start.", "冇可用嘅服務可以啟動。"), reset: true); return; }

        ComposeLog($"Bringing up '{proj.Name}'… · 啟動「{proj.Name}」…", reset: true);
        Busy.IsActive = true; ComposeUpBtn.IsEnabled = false; ComposeDownBtn.IsEnabled = false;
        var rep = new Progress<string>(line => DispatcherQueue.TryEnqueue(() => ComposeLog(line)));
        try
        {
            await _docker.ComposeUpAsync(proj, rep);
            Notify(InfoBarSeverity.Success, P($"Compose '{proj.Name}' is up.", $"Compose「{proj.Name}」已啟動。"));
            await RefreshAll();
        }
        catch (Exception ex) { ComposeLog("ERROR: " + ex.Message); Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; ComposeUpBtn.IsEnabled = true; ComposeDownBtn.IsEnabled = true; }
    }

    private async void ComposeDown_Click(object sender, RoutedEventArgs e)
    {
        if (!_docker.Connected) { Notify(InfoBarSeverity.Warning, P("Connect to Docker first.", "請先連到 Docker。")); return; }
        var name = (ComposeNameBox.Text ?? "").Trim();
        if (name.Length == 0) { Notify(InfoBarSeverity.Warning, P("Enter the project name.", "請輸入專案名稱。")); return; }
        ComposeLog($"Bringing down '{name}'… · 停止「{name}」…", reset: true);
        Busy.IsActive = true; ComposeUpBtn.IsEnabled = false; ComposeDownBtn.IsEnabled = false;
        var rep = new Progress<string>(line => DispatcherQueue.TryEnqueue(() => ComposeLog(line)));
        try
        {
            await _docker.ComposeDownAsync(name, rep);
            Notify(InfoBarSeverity.Success, P($"Compose '{name}' is down.", $"Compose「{name}」已停止。"));
            await RefreshAll();
        }
        catch (Exception ex) { ComposeLog("ERROR: " + ex.Message); Notify(InfoBarSeverity.Error, ex.Message); }
        finally { Busy.IsActive = false; ComposeUpBtn.IsEnabled = true; ComposeDownBtn.IsEnabled = true; }
    }

    private void ComposeLog(string line, bool reset = false)
    {
        if (reset) ComposeLogBox.Text = "";
        ComposeLogBox.Text += (ComposeLogBox.Text.Length > 0 ? "\n" : "") + line;
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────

    private async Task<bool> Confirm(string title, string body, string primary)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = title,
            Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap },
            PrimaryButtonText = primary, CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<string?> PromptText(string title, string header, string initial)
    {
        var box = new TextBox { Header = header, Text = initial, MinWidth = 360 };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = title, Content = box,
            PrimaryButtonText = P("OK · 確定", "確定"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }

    private void Notify(InfoBarSeverity sev, string message)
    {
        ResultBar.Severity = sev;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}

// ─────────────────────────────────── view-models ───────────────────────────────────

/// <summary>一行容器 · One container row.</summary>
public sealed class ContainerVM
{
    public string Id { get; }
    public string Name { get; }
    public string Image { get; }
    public string Status { get; }
    public string Ports { get; }
    public string Created { get; }
    public bool IsRunning { get; }
    public bool IsPaused { get; }
    public string ShortId => DockerService.ShortId(Id);

    public ContainerVM(ContainerListResponse c)
    {
        Id = c.ID;
        Name = (c.Names.FirstOrDefault() ?? c.ID).TrimStart('/');
        Image = c.Image ?? "";
        Status = c.Status ?? c.State ?? "";
        Ports = DockerService.FormatPorts(c.Ports);
        Created = c.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        var state = (c.State ?? "").ToLowerInvariant();
        IsRunning = state == "running" || state == "restarting";
        IsPaused = state == "paused";
    }

    public Brush StateBrush
    {
        get
        {
            string key = IsPaused ? "SystemFillColorCautionBrush"
                : IsRunning ? "SystemFillColorSuccessBrush"
                : "TextFillColorSecondaryBrush";
            return (Brush)Application.Current.Resources[key];
        }
    }
}

/// <summary>一行映像 · One image row.</summary>
public sealed class ImageVM
{
    public string Id { get; }
    public string Repo { get; }
    public string Size { get; }
    public string ShortId => DockerService.ShortId(Id);

    public ImageVM(ImagesListResponse i)
    {
        Id = i.ID;
        Repo = i.RepoTags is { Count: > 0 } rt && rt[0] != "<none>:<none>"
            ? string.Join(", ", rt)
            : "<none>";
        Size = DockerService.HumanSize(i.Size);
    }
}

/// <summary>一行磁碟區 · One volume row.</summary>
public sealed class VolumeVM
{
    public string Name { get; }
    public string Driver { get; }
    public string Mountpoint { get; }

    public VolumeVM(VolumeResponse v)
    {
        Name = v.Name ?? "";
        Driver = v.Driver ?? "";
        Mountpoint = v.Mountpoint ?? "";
    }
}

/// <summary>一行網路 · One network row.</summary>
public sealed class NetworkVM
{
    public string Id { get; }
    public string Name { get; }
    public string Driver { get; }
    public string Scope { get; }
    public string ShortId => DockerService.ShortId(Id);

    public NetworkVM(NetworkResponse n)
    {
        Id = n.ID ?? "";
        Name = n.Name ?? "";
        Driver = n.Driver ?? "";
        Scope = n.Scope ?? "";
    }
}
