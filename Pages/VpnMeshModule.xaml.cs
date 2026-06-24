using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 VPN 與網狀網 · In-app VPN &amp; mesh — NordVPN (CLI: connect/group/Meshnet), Tailscale
/// (CLI: up/down/status/ping/devices + exit-node picker + Serve/Funnel), the built-in Windows VPN client
/// (Get/Add/Remove-VpnConnection + rasdial) and WireGuard config import. No redirect. Bilingual.
/// </summary>
public sealed partial class VpnMeshModule : Page
{
    public sealed class PeerRow
    {
        public string Name { get; init; } = "";
        public string Ip { get; init; } = "";
        public SolidColorBrush Dot { get; init; } = new(Colors.Gray);
    }

    public sealed class WinVpnRow
    {
        public string Name { get; init; } = "";
        public string Detail { get; init; } = "";
        public SolidColorBrush Dot { get; init; } = new(Colors.Gray);
        public string ActionsLabel { get; init; } = "";
        public string ConnectLabel { get; init; } = "";
        public string DisconnectLabel { get; init; } = "";
        public string RemoveLabel { get; init; } = "";
    }

    public sealed class WgRow
    {
        public string Name { get; init; } = "";
        public SolidColorBrush Dot { get; init; } = new(Colors.Gray);
        public string RemoveLabel { get; init; } = "";
    }

    private static readonly SolidColorBrush GreenDot = new(Color.FromArgb(255, 0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush GrayDot = new(Colors.Gray);

    private bool _tsAvailable;
    private readonly ObservableCollection<WinVpnRow> _winVpn = new();
    private readonly ObservableCollection<WgRow> _wg = new();

    public VpnMeshModule()
    {
        InitializeComponent();
        WinVpnList.ItemsSource = _winVpn;
        WgList.ItemsSource = _wg;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            await CheckEngines();
            if (_tsAvailable) await RefreshTailscale();
            await RefreshWinVpn();
            await RefreshWireGuard();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "VPN & Mesh · VPN 與網狀網";
        HeaderBlurb.Text = P("Control NordVPN, Tailscale, the built-in Windows VPN client and WireGuard in-app by wrapping their command-line tools.",
            "喺 app 內透過包住佢哋嘅命令列工具控制 NordVPN、Tailscale、Windows 內置 VPN 同 WireGuard。");

        // NordVPN
        NordTitle.Text = "NordVPN";
        NordConnectBtn.Content = P("Connect", "連接");
        NordDisconnectBtn.Content = P("Disconnect", "斷開");
        NordGroupBtn.Content = P("Connect group", "連接群組");
        NordMeshTitle.Text = P("Meshnet (link your own devices)", "Meshnet（連接自己嘅裝置）");
        NordMeshPeersBtn.Content = P("List Meshnet peers", "列出 Meshnet 裝置");
        FillCountries();
        FillGroups();

        // Tailscale
        TsTitle.Text = "Tailscale";
        TsUpBtn.Content = P("Up (connect)", "Up（連接）");
        TsDownBtn.Content = P("Down", "Down");
        TsStatusBtn.Content = P("Status", "狀態");
        TsIpBtn.Content = P("My IP", "我嘅 IP");
        TsPingBtn.Content = P("Ping", "Ping");
        TsPeersHeader.Text = P("Mesh devices", "網狀網裝置");
        TsExitNodeBtn.Content = P("Use exit node", "用出口節點");
        TsClearExitNodeBtn.Content = P("Clear", "清除");
        TsAdvertiseExit.Content = P("Offer this PC as an exit node (approve in admin console)",
            "將呢部機提供做出口節點（喺管理主控台批准）");
        TsShareTitle.Text = P("Serve / Funnel — share a local port", "Serve / Funnel — 分享本機 port");
        TsShareBlurb.Text = P("Serve shares an HTTP port inside your tailnet (HTTPS); Funnel exposes it to the public internet.",
            "Serve 喺你嘅 tailnet 內分享一個 HTTP port（HTTPS）；Funnel 會公開畀全互聯網。");
        TsServeBtn.Content = P("Serve", "Serve");
        TsFunnelBtn.Content = P("Funnel", "Funnel");
        TsServeStatusBtn.Content = P("Share status", "分享狀態");
        TsServeResetBtn.Content = P("Stop Serve", "停止 Serve");
        TsFunnelResetBtn.Content = P("Stop Funnel", "停止 Funnel");
        FillExitNodeBoxPlaceholder();

        // Windows VPN
        WinVpnTitle.Text = P("Windows VPN (built-in client)", "Windows VPN（內置用戶端）");
        WinVpnBlurb.Text = P("Manage the built-in Windows VPN client — add IKEv2 / L2TP / SSTP / PPTP profiles and connect with rasdial. Adding/connecting some profiles may need administrator.",
            "管理 Windows 內置 VPN 用戶端 — 新增 IKEv2 / L2TP / SSTP / PPTP 設定檔，再用 rasdial 連接。部分設定檔可能要管理員權限。");
        WinVpnName.PlaceholderText = P("Profile name", "設定檔名稱");
        WinVpnServer.PlaceholderText = P("Server address (host / IP)", "伺服器地址（主機 / IP）");
        WinVpnAddBtn.Content = P("Add", "新增");
        WinVpnListHeader.Text = P("Profiles", "設定檔");
        WinVpnEmpty.Title = P("No VPN profiles", "未有 VPN 設定檔");
        WinVpnEmpty.Message = P("Add one above with a name and server address.", "喺上面輸入名稱同伺服器地址新增一個。");
        FillTunnelTypes();

        // WireGuard
        WgTitle.Text = "WireGuard";
        WgBlurb.Text = P("Import a WireGuard .conf as a tunnel. Importing/removing a tunnel installs a Windows service and needs administrator.",
            "匯入 WireGuard .conf 做隧道。匯入／移除隧道會安裝 Windows 服務，需要管理員權限。");
        WgImportBtn.Content = P("Import .conf…", "匯入 .conf…");
        WgRefreshBtn.Content = P("Refresh", "重新整理");
        WgListHeader.Text = P("Tunnels", "隧道");
        WgEmpty.Title = P("No tunnels imported", "未匯入隧道");
        WgEmpty.Message = P("Use Import .conf to add one.", "用「匯入 .conf」新增一個。");

        RelabelWinVpnRows();
        RelabelWgRows();
    }

    private void FillCountries()
    {
        int sel = NordCountryBox.SelectedIndex < 0 ? 0 : NordCountryBox.SelectedIndex;
        NordCountryBox.Items.Clear();
        foreach (var (en, zh) in NordVpnService.Countries) NordCountryBox.Items.Add($"{en} · {zh}");
        NordCountryBox.SelectedIndex = sel;
    }

    private void FillGroups()
    {
        int sel = NordGroupBox.SelectedIndex < 0 ? 0 : NordGroupBox.SelectedIndex;
        NordGroupBox.Items.Clear();
        foreach (var g in NordVpnService.Groups) NordGroupBox.Items.Add(g.Replace('_', ' '));
        NordGroupBox.SelectedIndex = sel;
    }

    private void FillTunnelTypes()
    {
        int sel = WinVpnTunnel.SelectedIndex < 0 ? 0 : WinVpnTunnel.SelectedIndex;
        WinVpnTunnel.Items.Clear();
        foreach (var (en, zh, _) in WindowsVpnService.TunnelTypes) WinVpnTunnel.Items.Add($"{en} · {zh}");
        WinVpnTunnel.SelectedIndex = sel;
    }

    private void FillExitNodeBoxPlaceholder()
    {
        if (TsExitNodeBox.Items.Count == 0)
            TsExitNodeBox.PlaceholderText = P("(no exit nodes found)", "（搵唔到出口節點）");
    }

    private async Task CheckEngines()
    {
        if (!NordVpnService.Installed)
        {
            NordEngineBar.IsOpen = true;
            NordEngineBar.Severity = InfoBarSeverity.Warning;
            NordEngineBar.Title = P("NordVPN not found", "搵唔到 NordVPN");
            NordEngineBar.Message = P("Click to install NordVPN automatically (winget), then sign in once.",
                "撳一下自動安裝 NordVPN（winget），再登入一次。");
            NordEngineBar.ActionButton = AutoInstallButton("NordVPN.NordVPN", "Install NordVPN automatically", "自動安裝 NordVPN");
        }
        else { NordEngineBar.IsOpen = false; NordEngineBar.ActionButton = null; }

        _tsAvailable = await TailscaleService.IsAvailable();
        if (!_tsAvailable)
        {
            TsEngineBar.IsOpen = true;
            TsEngineBar.Severity = InfoBarSeverity.Warning;
            TsEngineBar.Title = P("Tailscale not found", "搵唔到 Tailscale");
            TsEngineBar.Message = P("Click to install Tailscale automatically (winget), then sign in once.",
                "撳一下自動安裝 Tailscale（winget），再登入一次。");
            TsEngineBar.ActionButton = AutoInstallButton("tailscale.tailscale", "Install Tailscale automatically", "自動安裝 Tailscale");
        }
        else { TsEngineBar.IsOpen = false; TsEngineBar.ActionButton = null; }

        if (!WireGuardService.Installed)
        {
            WgEngineBar.IsOpen = true;
            WgEngineBar.Severity = InfoBarSeverity.Warning;
            WgEngineBar.Title = P("WireGuard not found", "搵唔到 WireGuard");
            WgEngineBar.Message = P("Click to install WireGuard automatically (winget).", "撳一下自動安裝 WireGuard（winget）。");
            WgEngineBar.ActionButton = AutoInstallButton("WireGuard.WireGuard", "Install WireGuard automatically", "自動安裝 WireGuard");
        }
        else { WgEngineBar.IsOpen = false; WgEngineBar.ActionButton = null; }
    }

    private Button AutoInstallButton(string wingetId, string en, string zh)
    {
        var btn = new Button { Content = P(en, zh) };
        btn.Click += async (_, _) =>
        {
            btn.IsEnabled = false;
            btn.Content = P("Installing…", "安裝緊…");
            await PackageService.AutoInstall(wingetId);
            await CheckEngines();
            await RefreshWireGuard();
        };
        return btn;
    }

    // ---- NordVPN ----
    private async void NordConnect_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string? name = NordCountryBox.SelectedIndex <= 0 ? null : NordVpnService.Countries[NordCountryBox.SelectedIndex].en;
        NordNotify(InfoBarSeverity.Informational, P("Connecting…", "連接緊…"), name ?? P("Quick connect", "快速連接"));
        var r = await NordVpnService.Connect(name);
        NordNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("NordVPN", "NordVPN"), Msg(r));
    }

    private async void NordDisconnect_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var r = await NordVpnService.Disconnect();
        NordNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Disconnected", "已斷開"), Msg(r));
    }

    private async void NordGroup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (NordGroupBox.SelectedIndex < 0) return;
        var group = NordVpnService.Groups[NordGroupBox.SelectedIndex];
        NordNotify(InfoBarSeverity.Informational, P("Connecting…", "連接緊…"), group);
        var r = await NordVpnService.ConnectGroup(group);
        NordNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("NordVPN", "NordVPN"), Msg(r));
    }

    private async void NordMeshToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        bool on = NordMeshToggle.IsOn;
        var r = await NordVpnService.SetMeshnet(on);
        NordNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            on ? P("Meshnet on", "Meshnet 已開") : P("Meshnet off", "Meshnet 已熄"), Msg(r));
    }

    private async void NordMeshPeers_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var r = await NordVpnService.MeshnetPeerList();
        NordNotify(r.Success ? InfoBarSeverity.Informational : InfoBarSeverity.Error,
            P("Meshnet peers", "Meshnet 裝置"),
            string.IsNullOrWhiteSpace(r.Output) ? Msg(r) : r.Output);
    }

    // ---- Tailscale ----
    private async void TsUp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; var r = await TailscaleService.Up(); TsBusy.IsActive = false;
        TsConsole.Text = Msg(r); await RefreshTailscale();
    }

    private async void TsDown_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; var r = await TailscaleService.Down(); TsBusy.IsActive = false;
        TsConsole.Text = Msg(r); await RefreshTailscale();
    }

    private async void TsStatus_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await RefreshTailscale();

    private async void TsIp_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; TsConsole.Text = await TailscaleService.Ip(); TsBusy.IsActive = false;
    }

    private async void TsPing_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var host = (TsPingBox.Text ?? "").Trim();
        if (host.Length == 0) return;
        TsBusy.IsActive = true; TsConsole.Text = await TailscaleService.Ping(host); TsBusy.IsActive = false;
    }

    private async void TsSetExitNode_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (TsExitNodeBox.SelectedItem is not string node || node.Length == 0) return;
        // ComboBox items are "name · ip" — pass the IP (most reliable).
        string target = node.Contains('·') ? node[(node.LastIndexOf('·') + 1)..].Trim() : node.Trim();
        TsBusy.IsActive = true;
        var r = await TailscaleService.SetExitNode(target);
        TsBusy.IsActive = false;
        TsConsole.Text = r.Success ? P($"Routing through exit node {target}.", $"已經行緊出口節點 {target}。") : Msg(r);
    }

    private async void TsClearExitNode_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true;
        var r = await TailscaleService.SetExitNode(null);
        TsBusy.IsActive = false;
        TsConsole.Text = r.Success ? P("Exit node cleared.", "已清除出口節點。") : Msg(r);
    }

    private async void TsAdvertiseExit_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        bool on = TsAdvertiseExit.IsChecked == true;
        TsBusy.IsActive = true;
        var r = await TailscaleService.AdvertiseExitNode(on);
        TsBusy.IsActive = false;
        TsConsole.Text = r.Success
            ? (on ? P("Advertising this PC as an exit node. Approve it in the Tailscale admin console.",
                       "已自薦呢部機做出口節點。請喺 Tailscale 管理主控台批准。")
                  : P("Stopped advertising as an exit node.", "已停止自薦做出口節點。"))
            : Msg(r);
    }

    private int Port() => (int)Math.Clamp(double.IsNaN(TsPortBox.Value) ? 0 : TsPortBox.Value, 1, 65535);

    private async void TsServe_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; var r = await TailscaleService.Serve(Port()); TsBusy.IsActive = false;
        TsConsole.Text = r.Success ? P($"Serving local port {Port()} on your tailnet.", $"已喺 tailnet 分享本機 port {Port()}。") : Msg(r);
        if (r.Success) TsConsole.Text += "\n" + await TailscaleService.ServeStatus();
    }

    private async void TsFunnel_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; var r = await TailscaleService.Funnel(Port()); TsBusy.IsActive = false;
        TsConsole.Text = r.Success ? P($"Funnelling local port {Port()} to the public internet.", $"已將本機 port {Port()} 公開到互聯網。") : Msg(r);
        if (r.Success) TsConsole.Text += "\n" + await TailscaleService.ServeStatus();
    }

    private async void TsServeStatus_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; TsConsole.Text = await TailscaleService.ServeStatus(); TsBusy.IsActive = false;
    }

    private async void TsServeReset_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; var r = await TailscaleService.ServeReset(); TsBusy.IsActive = false;
        TsConsole.Text = r.Success ? P("Serve stopped.", "Serve 已停止。") : Msg(r);
    }

    private async void TsFunnelReset_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        TsBusy.IsActive = true; var r = await TailscaleService.FunnelReset(); TsBusy.IsActive = false;
        TsConsole.Text = r.Success ? P("Funnel stopped.", "Funnel 已停止。") : Msg(r);
    }

    private async Task RefreshTailscale()
    {
        TsBusy.IsActive = true;
        try
        {
            TsConsole.Text = await TailscaleService.Status();
            var peers = await TailscaleService.Peers();
            TsPeers.Items.Clear();
            foreach (var p in peers)
                TsPeers.Items.Add(new PeerRow
                {
                    Name = p.Self ? $"{p.Name} ({P("this PC", "呢部機")})" : p.Name,
                    Ip = p.Ip,
                    Dot = p.Online ? GreenDot : GrayDot,
                });

            // Exit-node picker
            string? sel = TsExitNodeBox.SelectedItem as string;
            TsExitNodeBox.Items.Clear();
            var exits = await TailscaleService.ExitNodes();
            foreach (var n in exits)
                TsExitNodeBox.Items.Add(string.IsNullOrEmpty(n.Ip) ? n.Name : $"{n.Name} · {n.Ip}");
            if (TsExitNodeBox.Items.Count == 0)
                TsExitNodeBox.PlaceholderText = P("(no exit nodes found)", "（搵唔到出口節點）");
            else if (sel is not null && TsExitNodeBox.Items.Contains(sel))
                TsExitNodeBox.SelectedItem = sel;
        }
        catch { }
        TsBusy.IsActive = false;
    }

    // ---- Windows VPN ----
    private async void WinVpnAdd_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        string name = (WinVpnName.Text ?? "").Trim();
        string server = (WinVpnServer.Text ?? "").Trim();
        if (name.Length == 0 || server.Length == 0)
        {
            WinVpnNotify(InfoBarSeverity.Warning, P("Missing details", "資料未齊"),
                P("Enter a profile name and a server address.", "請輸入設定檔名稱同伺服器地址。"));
            return;
        }
        int idx = WinVpnTunnel.SelectedIndex < 0 ? 0 : WinVpnTunnel.SelectedIndex;
        string tunnel = WindowsVpnService.TunnelTypes[idx].value;
        var r = await WindowsVpnService.Add(name, server, tunnel);
        WinVpnNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Profile added", "已新增設定檔") : P("Could not add profile", "未能新增設定檔"), Msg(r));
        if (r.Success) { WinVpnName.Text = ""; WinVpnServer.Text = ""; await RefreshWinVpn(); }
    }

    private async void WinVpnConnect_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not string name) return;
        WinVpnNotify(InfoBarSeverity.Informational, P("Connecting…", "連接緊…"), name);
        var r = await WindowsVpnService.Connect(name);
        WinVpnNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Connected", "已連接") : P("Connect failed", "連接失敗"),
            string.IsNullOrWhiteSpace(r.Output) ? Msg(r) : r.Output);
        await RefreshWinVpn();
    }

    private async void WinVpnDisconnect_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not string name) return;
        var r = await WindowsVpnService.Disconnect(name);
        WinVpnNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("Disconnected", "已斷開"), Msg(r));
        await RefreshWinVpn();
    }

    private async void WinVpnRemove_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if ((sender as MenuFlyoutItem)?.Tag is not string name) return;
        var dlg = new ContentDialog
        {
            Title = P("Remove VPN profile?", "刪除 VPN 設定檔？"),
            Content = P($"This deletes the Windows VPN profile \"{name}\".", $"呢個會刪除 Windows VPN 設定檔「{name}」。"),
            PrimaryButtonText = P("Remove", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var r = await WindowsVpnService.Remove(name);
        WinVpnNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Profile removed", "已刪除設定檔") : P("Could not remove", "未能刪除"), Msg(r));
        await RefreshWinVpn();
    }

    private async Task RefreshWinVpn()
    {
        try
        {
            var profiles = await WindowsVpnService.List();
            _winVpn.Clear();
            foreach (var p in profiles)
                _winVpn.Add(new WinVpnRow
                {
                    Name = p.Name,
                    Detail = $"{p.TunnelType}  ·  {p.ServerAddress}  ·  {p.ConnectionStatus}",
                    Dot = p.Connected ? GreenDot : GrayDot,
                    ActionsLabel = P("Actions ▾", "操作 ▾"),
                    ConnectLabel = P("Connect", "連接"),
                    DisconnectLabel = P("Disconnect", "斷開"),
                    RemoveLabel = P("Remove", "刪除"),
                });
            WinVpnEmpty.IsOpen = _winVpn.Count == 0;
        }
        catch { }
    }

    private void RelabelWinVpnRows()
    {
        // Labels live on the row objects; re-apply on language change by rebuilding from current names.
        if (_winVpn.Count == 0) return;
        var snapshot = new System.Collections.Generic.List<WinVpnRow>(_winVpn);
        _winVpn.Clear();
        foreach (var r in snapshot)
            _winVpn.Add(new WinVpnRow
            {
                Name = r.Name,
                Detail = r.Detail,
                Dot = r.Dot,
                ActionsLabel = P("Actions ▾", "操作 ▾"),
                ConnectLabel = P("Connect", "連接"),
                DisconnectLabel = P("Disconnect", "斷開"),
                RemoveLabel = P("Remove", "刪除"),
            });
    }

    // ---- WireGuard ----
    private async void WgImport_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (!WireGuardService.Installed)
        {
            WgNotify(InfoBarSeverity.Warning, P("WireGuard not found", "搵唔到 WireGuard"),
                P("Install WireGuard first (see the notice above).", "請先安裝 WireGuard（睇上面提示）。"));
            return;
        }
        var path = await FileDialogs.OpenFileAsync(".conf");
        if (path is null) return;

        WgBusy.IsActive = true;
        var r = await WireGuardService.ImportConfig(path);
        WgBusy.IsActive = false;
        WgNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Tunnel imported", "已匯入隧道") : P("Import failed", "匯入失敗"), Msg(r));
        await RefreshWireGuard();
    }

    private async void WgRefresh_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e) => await RefreshWireGuard();

    private async void WgRemove_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not string name) return;
        var dlg = new ContentDialog
        {
            Title = P("Remove WireGuard tunnel?", "移除 WireGuard 隧道？"),
            Content = P($"This uninstalls the tunnel service \"{name}\".", $"呢個會解除安裝隧道服務「{name}」。"),
            PrimaryButtonText = P("Remove", "移除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        WgBusy.IsActive = true;
        var r = await WireGuardService.RemoveTunnel(name);
        WgBusy.IsActive = false;
        WgNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Tunnel removed", "已移除隧道") : P("Could not remove", "未能移除"), Msg(r));
        await RefreshWireGuard();
    }

    private async Task RefreshWireGuard()
    {
        try
        {
            var tunnels = WireGuardService.Installed
                ? await WireGuardService.Tunnels()
                : new System.Collections.Generic.List<WgTunnel>();
            _wg.Clear();
            foreach (var t in tunnels)
                _wg.Add(new WgRow
                {
                    Name = t.Active ? $"{t.Name} ({P("active", "使用中")})" : t.Name,
                    Dot = t.Active ? GreenDot : GrayDot,
                    RemoveLabel = P("Remove", "移除"),
                });
            WgEmpty.IsOpen = WireGuardService.Installed && _wg.Count == 0;
        }
        catch { }
    }

    private void RelabelWgRows()
    {
        if (_wg.Count == 0) return;
        var snapshot = new System.Collections.Generic.List<WgRow>(_wg);
        _wg.Clear();
        foreach (var r in snapshot)
            _wg.Add(new WgRow { Name = r.Name, Dot = r.Dot, RemoveLabel = P("Remove", "移除") });
    }

    // ---- helpers ----
    private static string Msg(WinForge.Models.TweakResult r)
        => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void NordNotify(InfoBarSeverity sev, string title, string msg)
    {
        NordResult.Severity = sev; NordResult.Title = title; NordResult.Message = msg; NordResult.IsOpen = true;
    }

    private void WinVpnNotify(InfoBarSeverity sev, string title, string msg)
    {
        WinVpnResult.Severity = sev; WinVpnResult.Title = title; WinVpnResult.Message = msg; WinVpnResult.IsOpen = true;
    }

    private void WgNotify(InfoBarSeverity sev, string title, string msg)
    {
        WgResult.Severity = sev; WgResult.Title = title; WgResult.Message = msg; WgResult.IsOpen = true;
    }
}
