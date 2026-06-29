using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 透過 SSH 遠端控制 Docker · Remote Docker container power control over SSH.
/// 連去遠端主機（重用已儲存嘅 SSH 設定檔，或手動輸入主機／帳號／密碼或私鑰），喺嗰部機跑
/// docker CLI 指令：列出容器、啟動／停止／重啟／暫停／移除、睇日誌、喺容器內執行命令。
/// 全部經 SSH.NET 喺背景執行緒完成；唔需要本機安裝 docker。
/// Connects to a remote host (reusing saved SSH profiles, or manual host/user/password/key) and
/// runs the docker CLI there: list containers, start/stop/restart/pause/remove, view logs, exec a
/// command. All over SSH.NET on background threads; no local docker needed. Fully bilingual.
/// </summary>
public sealed partial class DockerSshModule : Page
{
    private readonly DockerSshService _docker = new();
    private readonly ObservableCollection<SshContainerVM> _rows = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(5) };

    private SshProfile? _profile;       // the connection currently in use (saved or ad-hoc)
    private string _secret = string.Empty;
    private bool _connected;
    private bool _refreshing;
    private string _search = string.Empty;
    private CancellationTokenSource? _cts;
    private bool _suppressEvents;

    public DockerSshModule()
    {
        InitializeComponent();
        ContainerList.ItemsSource = _rows;
        _timer.Tick += async (_, _) => await RefreshTick();
        Loc.I.LanguageChanged += OnLang;
        SshProfileStore.Changed += OnStoreChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? s, EventArgs e) { Render(); RefreshProfileCombo(); }
    private void OnStoreChanged(object? s, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshProfileCombo);

    private void OnLoaded(object? s, RoutedEventArgs e)
    {
        BuildAuthCombo();
        RefreshProfileCombo();
        Render();
        UpdateConnUi();
        UpdateActionButtons();
        UpdateListVisibility();
    }

    private void OnUnloaded(object? s, RoutedEventArgs e)
    {
        _timer.Stop();
        _cts?.Cancel();
        Loc.I.LanguageChanged -= OnLang;
        SshProfileStore.Changed -= OnStoreChanged;
        _secret = string.Empty;
    }

    // ── rendering ───────────────────────────────────────────────────────────

    private void Render()
    {
        Header.Title = "Docker over SSH · 透過 SSH 控制 Docker";
        HeaderBlurb.Text = P(
            "Manage Docker containers on a remote host over SSH — pick a saved SSH profile (or enter host/user/password or key), then list containers and power them: start, stop, restart, pause, remove, view logs and exec commands. Everything runs the docker CLI on the remote machine via SSH.NET; nothing is installed locally.",
            "經 SSH 管理遠端主機上嘅 Docker 容器 — 揀一個已儲存嘅 SSH 設定檔（或者輸入主機／帳號／密碼或私鑰），就可以列出容器同控制電源：啟動、停止、重啟、暫停、移除、睇日誌、執行命令。全部經 SSH.NET 喺遠端機跑 docker CLI；本機唔使安裝任何嘢。");

        ConnHeader.Text = P("Connection · 連線", "連線");
        ProfileBox.Header = P("Saved SSH profile · 已儲存 SSH 設定檔", "已儲存 SSH 設定檔");
        ConnectBtn.Content = P("Connect · 連線", "連線");
        DisconnectBtn.Content = P("Disconnect · 中斷", "中斷");
        RememberChk.Content = P("Save as SSH profile · 儲存做 SSH 設定檔", "儲存做 SSH 設定檔");
        BrowseKeyBtn.Content = P("Browse… · 瀏覽…", "瀏覽…");
        AutoRefreshLbl.Text = P("Auto-refresh · 自動重新整理", "自動重新整理");
        LogsBtnLbl.Text = P("Logs · 日誌", "日誌");
        ExecBtn.Content = P("Exec · 執行", "執行");
        SearchBox.PlaceholderText = P("Filter by name/image · 按名稱／鏡像篩選", "按名稱／鏡像篩選");

        BuildAuthCombo();

        EmptyText.Text = _connected
            ? P("No containers on this host. (docker ps -a returned nothing.)", "呢部主機冇容器。（docker ps -a 冇結果。）")
            : P("Connect to a remote Docker host above to see its containers.", "喺上面連去遠端 Docker 主機就會見到佢嘅容器。");

        if (string.IsNullOrEmpty(DetailTitle.Text))
            DetailTitle.Text = P("Select a container · 揀一個容器", "揀一個容器");

        UpdateConnUi();
    }

    private void BuildAuthCombo()
    {
        _suppressEvents = true;
        int sel = AuthBox.SelectedIndex < 0 ? 0 : AuthBox.SelectedIndex;
        AuthBox.Items.Clear();
        AuthBox.Items.Add(new ComboBoxItem { Content = P("Password · 密碼", "密碼"), Tag = "password" });
        AuthBox.Items.Add(new ComboBoxItem { Content = P("Private key · 私鑰", "私鑰"), Tag = "key" });
        AuthBox.SelectedIndex = sel;
        _suppressEvents = false;
    }

    private void RefreshProfileCombo()
    {
        _suppressEvents = true;
        string? prevId = (ProfileBox.SelectedItem as ComboBoxItem)?.Tag as string;
        ProfileBox.Items.Clear();
        ProfileBox.Items.Add(new ComboBoxItem { Content = P("(manual entry) · （手動輸入）", "（手動輸入）"), Tag = "" });
        foreach (var p in SshProfileStore.All)
        {
            var label = string.IsNullOrWhiteSpace(p.Name) ? p.Display : $"{p.Name}  ({p.Display})";
            ProfileBox.Items.Add(new ComboBoxItem { Content = label, Tag = p.Id });
        }
        int idx = 0;
        for (int i = 0; i < ProfileBox.Items.Count; i++)
            if (((ComboBoxItem)ProfileBox.Items[i]).Tag as string == prevId) { idx = i; break; }
        ProfileBox.SelectedIndex = idx;
        _suppressEvents = false;
    }

    private void Profile_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        var id = (ProfileBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (string.IsNullOrEmpty(id)) return;
        var p = SshProfileStore.Get(id);
        if (p is null) return;
        HostBox.Text = p.Host;
        PortBox.Value = p.Port;
        UserBox.Text = p.User;
        SelectAuth(p.Auth == SshAuthKind.PrivateKey ? "key" : "password");
        KeyPathBox.Text = p.KeyPath;
        PassBox.Password = string.Empty; // stored secret is used directly on connect
    }

    private void SelectAuth(string tag)
    {
        for (int i = 0; i < AuthBox.Items.Count; i++)
            if (((ComboBoxItem)AuthBox.Items[i]).Tag as string == tag) { AuthBox.SelectedIndex = i; return; }
    }

    private void Auth_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        bool key = (AuthBox.SelectedItem as ComboBoxItem)?.Tag as string == "key";
        KeyRow.Visibility = key ? Visibility.Visible : Visibility.Collapsed;
        PassBox.Header = key ? P("Key passphrase (optional) · 私鑰密碼短語（可選）", "私鑰密碼短語（可選）")
                             : P("Password · 密碼", "密碼");
    }

    private async void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (!string.IsNullOrEmpty(path)) KeyPathBox.Text = path;
    }

    private void ReloadProfiles_Click(object sender, RoutedEventArgs e) => RefreshProfileCombo();

    // ── connect / disconnect ────────────────────────────────────────────────

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        bool key = (AuthBox.SelectedItem as ComboBoxItem)?.Tag as string == "key";
        var selId = (ProfileBox.SelectedItem as ComboBoxItem)?.Tag as string;

        SshProfile profile;
        string secret;

        if (!string.IsNullOrEmpty(selId) && SshProfileStore.Get(selId) is { } saved
            && saved.Host == HostBox.Text.Trim() && saved.User == UserBox.Text.Trim())
        {
            // Reuse the saved profile + its DPAPI secret unless the user typed a fresh one.
            profile = saved;
            secret = PassBox.Password.Length > 0 ? PassBox.Password : SshProfileStore.Reveal(saved);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(HostBox.Text) || string.IsNullOrWhiteSpace(UserBox.Text))
            {
                ShowConn(InfoBarSeverity.Warning, P("Enter at least a host and a username.", "至少要輸入主機同帳號。"));
                return;
            }
            profile = new SshProfile
            {
                Name = string.Empty,
                Host = HostBox.Text.Trim(),
                Port = (int)PortBox.Value,
                User = UserBox.Text.Trim(),
                Auth = key ? SshAuthKind.PrivateKey : SshAuthKind.Password,
                KeyPath = key ? KeyPathBox.Text.Trim() : string.Empty,
            };
            secret = PassBox.Password;

            if (RememberChk.IsChecked == true)
            {
                profile.Name = $"{profile.User}@{profile.Host}";
                SshProfileStore.Save(profile, secret); // DPAPI-encrypts the secret at rest
                RefreshProfileCombo();
            }
        }

        await DoConnect(profile, secret);
    }

    private async Task DoConnect(SshProfile profile, string secret)
    {
        ConnBusy.IsActive = true;
        ConnectBtn.IsEnabled = false;
        try
        {
            var info = await _docker.ProbeAsync(profile, secret);
            if (!info.DockerPresent)
            {
                _connected = false;
                ShowDockerBar(info.RawError);
                ShowConn(InfoBarSeverity.Error, string.IsNullOrWhiteSpace(info.RawError)
                    ? P("Connected, but docker was not found on the remote host.", "已連線，但喺遠端主機搵唔到 docker。")
                    : P($"Connected, but docker is unavailable: {info.RawError}", $"已連線，但 docker 用唔到：{info.RawError}"));
                UpdateConnUi();
                return;
            }

            _profile = profile;
            _secret = secret;
            _connected = true;
            DockerBar.IsOpen = false;
            FooterHost.Text = P($"Host: {profile.Display}", $"主機：{profile.Display}");
            FooterVersion.Text = P($"Docker {info.ServerVersion}  ·  {info.OsArch}", $"Docker {info.ServerVersion}  ·  {info.OsArch}");
            ShowConn(InfoBarSeverity.Success, P("Connected.", "已連線。"));
            ConnExpander.IsExpanded = false;
            await RefreshTick();
            if (AutoRefreshToggle.IsOn) _timer.Start();
        }
        catch (Exception ex)
        {
            _connected = false;
            ShowConn(InfoBarSeverity.Error, FriendlyError(ex));
        }
        finally
        {
            ConnBusy.IsActive = false;
            ConnectBtn.IsEnabled = true;
            UpdateConnUi();
            UpdateActionButtons();
            UpdateListVisibility();
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _cts?.Cancel();
        _connected = false;
        _profile = null;
        _secret = string.Empty;
        _rows.Clear();
        FooterHost.Text = ""; FooterVersion.Text = ""; FooterCount.Text = "";
        LogText.Text = ""; DetailTitle.Text = P("Select a container · 揀一個容器", "揀一個容器"); DetailSub.Text = "";
        UpdateConnUi();
        UpdateActionButtons();
        UpdateListVisibility();
        ShowConn(InfoBarSeverity.Informational, P("Disconnected.", "已中斷。"));
    }

    private void UpdateConnUi()
    {
        ConnState.Text = _connected ? P("Connected · 已連線", "已連線") : P("Not connected · 未連線", "未連線");
        ConnPill.Background = _connected
            ? new SolidColorBrush(Color.FromArgb(40, 0x6C, 0xCB, 0x5A))
            : (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"];
        DisconnectBtn.IsEnabled = _connected;
        RefreshBtn.IsEnabled = _connected;
        SearchBox.IsEnabled = _connected;
    }

    private void ShowDockerBar(string raw)
    {
        DockerBar.IsOpen = true;
        DockerBar.Severity = InfoBarSeverity.Warning;
        DockerBar.Title = P("Docker not available", "Docker 用唔到");
        DockerBar.Message = string.IsNullOrWhiteSpace(raw)
            ? P("docker was not found on the remote host. Install Docker there, or make sure the SSH user can run docker (e.g. in the 'docker' group).",
                "喺遠端主機搵唔到 docker。請喺嗰部機安裝 Docker，或者確保該 SSH 使用者可以執行 docker（例如加入 'docker' 群組）。")
            : P($"docker could not be used on the remote host: {raw}", $"喺遠端主機用唔到 docker：{raw}");
    }

    // ── search / refresh ────────────────────────────────────────────────────

    private async void Search_Changed(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _search = sender.Text ?? "";
        if (_connected) await RefreshTick();
    }

    private void AutoRefresh_Toggled(object sender, RoutedEventArgs e)
    {
        if (AutoRefreshToggle.IsOn && _connected) _timer.Start();
        else _timer.Stop();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshTick();

    private async Task RefreshTick()
    {
        if (_refreshing || !_connected || _profile is null) return;
        _refreshing = true;
        _cts ??= new CancellationTokenSource();
        var ct = _cts.Token;
        try
        {
            var (ok, error, rows) = await _docker.ListAsync(_profile, _secret, ct);
            if (!ok)
            {
                ShowConn(InfoBarSeverity.Error, P($"Could not list containers: {error}", $"列唔到容器：{error}"));
                return;
            }

            var filtered = string.IsNullOrWhiteSpace(_search)
                ? rows
                : rows.Where(r => (r.Name + " " + r.Image).Contains(_search, StringComparison.OrdinalIgnoreCase)).ToList();
            MergeRows(filtered);

            int running = rows.Count(r => r.IsRunning);
            FooterCount.Text = P($"{_rows.Count} shown · {running} running", $"顯示 {_rows.Count} 個 · {running} 個執行中");
            UpdateActionButtons();
            UpdateListVisibility();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ShowConn(InfoBarSeverity.Error, FriendlyError(ex));
        }
        finally { _refreshing = false; }
    }

    private void MergeRows(List<DockerContainer> snapshot)
    {
        var byId = _rows.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // preserve order from snapshot; update in place where possible
        for (int i = 0; i < snapshot.Count; i++)
        {
            var c = snapshot[i];
            seen.Add(c.Id);
            if (byId.TryGetValue(c.Id, out var vm))
            {
                vm.Update(c);
                int cur = _rows.IndexOf(vm);
                if (cur != i && i < _rows.Count) _rows.Move(cur, i);
            }
            else
            {
                var nv = new SshContainerVM(c);
                if (i < _rows.Count) _rows.Insert(i, nv); else _rows.Add(nv);
            }
        }
        for (int i = _rows.Count - 1; i >= 0; i--)
            if (!seen.Contains(_rows[i].Id)) _rows.RemoveAt(i);
    }

    private void UpdateListVisibility()
    {
        bool empty = _rows.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        ContainerList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    // ── selection / actions ─────────────────────────────────────────────────

    private SshContainerVM? Selected => ContainerList.SelectedItem as SshContainerVM;

    private async void ContainerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
        var vm = Selected;
        if (vm is null) return;
        DetailTitle.Text = vm.Name;
        DetailSub.Text = $"{vm.Image}\n{vm.Id}  ·  {vm.StatusText}{(string.IsNullOrEmpty(vm.Ports) ? "" : "\n" + vm.Ports)}";
        await LoadLogs(vm);
    }

    private void UpdateActionButtons()
    {
        var vm = Selected;
        bool any = vm is not null && _connected;
        StartBtn.IsEnabled = any && !vm!.Running;
        StopBtn.IsEnabled = any && vm!.Running;
        RestartBtn.IsEnabled = any && vm!.Running;
        PauseBtn.IsEnabled = any && (vm!.Running || vm.Paused);
        RemoveBtn.IsEnabled = any;
        LogsBtn.IsEnabled = any;
        ExecBtn.IsEnabled = any && vm!.Running;
        // Pause button label reflects current state
        if (vm is not null && vm.Paused)
            ToolTipService.SetToolTip(PauseBtn, P("Unpause · 繼續", "繼續"));
        else
            ToolTipService.SetToolTip(PauseBtn, P("Pause · 暫停", "暫停"));
    }

    private async Task Act(Func<string, Task<(int code, string stdout, string stderr)>> action, string verbEn, string verbZh)
    {
        var vm = Selected;
        if (vm is null || _profile is null) return;
        try
        {
            var (code, _, stderr) = await action(vm.Id);
            if (code != 0)
                ShowConn(InfoBarSeverity.Error, P($"{verbEn} failed: {stderr.Trim()}", $"{verbZh}失敗：{stderr.Trim()}"));
            else
                ShowConn(InfoBarSeverity.Success, P($"{verbEn} done.", $"已{verbZh}。"));
        }
        catch (Exception ex) { ShowConn(InfoBarSeverity.Error, FriendlyError(ex)); }
        await RefreshTick();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
        => await Act(id => _docker.StartAsync(_profile!, _secret, id), "Start", "啟動");

    private async void Stop_Click(object sender, RoutedEventArgs e)
        => await Act(id => _docker.StopAsync(_profile!, _secret, id), "Stop", "停止");

    private async void Restart_Click(object sender, RoutedEventArgs e)
        => await Act(id => _docker.RestartAsync(_profile!, _secret, id), "Restart", "重啟");

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm is null) return;
        if (vm.Paused) await Act(id => _docker.UnpauseAsync(_profile!, _secret, id), "Unpause", "繼續");
        else await Act(id => _docker.PauseAsync(_profile!, _secret, id), "Pause", "暫停");
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Remove container '{vm.Name}'?", $"移除容器「{vm.Name}」？"),
            Content = new TextBlock
            {
                Text = P("This force-removes the container (docker rm -f). This cannot be undone.",
                         "呢個會強制移除容器（docker rm -f），無法復原。"),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = P("Remove · 移除", "移除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        await Act(id => _docker.RemoveAsync(_profile!, _secret, id), "Remove", "移除");
    }

    // ── logs / exec ─────────────────────────────────────────────────────────

    private async void Logs_Click(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm is not null) await LoadLogs(vm);
    }

    private async Task LoadLogs(SshContainerVM vm)
    {
        if (_profile is null) return;
        LogText.Text = P("Loading logs… · 載入日誌中…", "載入日誌中…");
        try
        {
            int tail = (int)Math.Max(10, TailBox.Value);
            var logs = await _docker.LogsAsync(_profile, _secret, vm.Id, tail);
            LogText.Text = string.IsNullOrWhiteSpace(logs) ? P("(no log output)", "（冇日誌輸出）") : logs;
            LogScroller.ChangeView(null, double.MaxValue, null);
        }
        catch (Exception ex) { LogText.Text = FriendlyError(ex); }
    }

    private async void Exec_Click(object sender, RoutedEventArgs e)
    {
        var vm = Selected;
        if (vm is null || _profile is null) return;
        var command = ExecBox.Text.Trim();
        if (string.IsNullOrEmpty(command)) return;
        LogText.Text = P($"$ {command}\nRunning… · 執行中…", $"$ {command}\n執行中…");
        try
        {
            var output = await _docker.ExecAsync(_profile, _secret, vm.Id, command);
            LogText.Text = $"$ {command}\n" + (string.IsNullOrWhiteSpace(output) ? P("(no output)", "（冇輸出）") : output);
            LogScroller.ChangeView(null, double.MaxValue, null);
        }
        catch (Exception ex) { LogText.Text = FriendlyError(ex); }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private string FriendlyError(Exception ex)
    {
        var m = ex.Message;
        if (ex is Renci.SshNet.Common.SshAuthenticationException)
            return P($"Authentication failed: {m}", $"認證失敗：{m}");
        if (ex is Renci.SshNet.Common.SshConnectionException)
            return P($"Connection error: {m}", $"連線錯誤：{m}");
        if (ex is System.Net.Sockets.SocketException || ex is TimeoutException)
            return P($"Could not reach the host (timeout/network): {m}", $"連唔到主機（逾時／網絡）：{m}");
        return P($"Error: {m}", $"出錯：{m}");
    }

    private void ShowConn(InfoBarSeverity sev, string message)
    {
        ConnResult.Severity = sev;
        ConnResult.Message = message;
        ConnResult.IsOpen = true;
    }
}

/// <summary>
/// 一行容器嘅可觀察檢視模型 · Observable view-model for one container row, updated in place so the
/// ListView keeps its selection and scroll position across refreshes.
/// </summary>
public sealed class SshContainerVM : INotifyPropertyChanged
{
    public string Id { get; }
    private DockerContainer _c;

    public SshContainerVM(DockerContainer c) { Id = c.Id; _c = c; }

    public void Update(DockerContainer c)
    {
        _c = c;
        foreach (var p in new[] { nameof(Name), nameof(Image), nameof(SubLine), nameof(StateText),
            nameof(StateBrush), nameof(StatusText), nameof(Ports), nameof(Running), nameof(Paused) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public string Name => _c.Name;
    public string Image => _c.Image;
    public string Ports => _c.Ports;
    public bool Running => _c.IsRunning;
    public bool Paused => _c.IsPaused;
    public string StatusText => _c.Status;

    public string SubLine
    {
        get
        {
            var bits = new List<string> { _c.Image, _c.Id };
            if (!string.IsNullOrWhiteSpace(_c.Ports)) bits.Add(_c.Ports);
            return string.Join("   ·   ", bits);
        }
    }

    public string StateText
    {
        get
        {
            return _c.State.ToLowerInvariant() switch
            {
                "running" => Loc.I.Pick("running", "執行中"),
                "exited" => Loc.I.Pick("exited", "已停止"),
                "paused" => Loc.I.Pick("paused", "已暫停"),
                "created" => Loc.I.Pick("created", "已建立"),
                "restarting" => Loc.I.Pick("restarting", "重啟中"),
                "dead" => Loc.I.Pick("dead", "已死"),
                _ => _c.State,
            };
        }
    }

    public Brush StateBrush
    {
        get
        {
            var key = _c.State.ToLowerInvariant() switch
            {
                "running" => "SystemFillColorSuccessBrush",
                "paused" or "restarting" => "SystemFillColorAttentionBrush",
                "dead" => "SystemFillColorCriticalBrush",
                _ => "TextFillColorSecondaryBrush",
            };
            return (Brush)Application.Current.Resources[key];
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
