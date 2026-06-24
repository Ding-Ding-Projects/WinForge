using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 RustDesk 管理器 · In-app RustDesk manager and launcher. Shows this PC's ID and lets you set
/// the permanent password, quick-connect to a peer by ID (with optional view-only), keep a saved-peers
/// address book, configure a self-hosted ID/relay/API server + public key (written to RustDesk2.toml),
/// install/remove the unattended service and launch RustDesk. Wraps the unmodified rustdesk.exe (AGPL —
/// nothing bundled or derived). Bilingual throughout. No external redirect.
/// </summary>
public sealed partial class RustDeskModule : Page
{
    private readonly ObservableCollection<RdPeer> _peers = new();

    public RustDeskModule()
    {
        InitializeComponent();
        PeerList.ItemsSource = _peers;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            BuildInstallButton();
            LoadPeers();
            LoadServer();
            Render();
            await RefreshStatus();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "RustDesk · 遠端桌面";
        HeaderBlurb.Text = P("Install, launch and manage RustDesk remote desktop — show this PC's ID, set a permanent password, quick-connect to a peer, keep an address book and point RustDesk at your own self-hosted server. Everything runs in-app by driving the RustDesk CLI and config.",
            "安裝、啟動同管理 RustDesk 遠端桌面 — 顯示本機 ID、設永久密碼、快速連去對端、保存常用清單，仲可以指向你自己嘅自架伺服器。全部喺 app 內透過 RustDesk CLI 同設定檔做。");

        ThisPcTitle.Text = P("This PC · 本機", "本機");
        IdLbl.Text = P("ID", "ID");
        PwLbl.Text = P("Permanent password · 永久密碼", "永久密碼");
        SetPwBtn.Content = P("Set password · 設定密碼", "設定密碼");
        ToolTipService.SetToolTip(CopyIdBtn, P("Copy ID · 複製 ID", "複製 ID"));
        ToolTipService.SetToolTip(RefreshIdBtn, P("Refresh · 重新整理", "重新整理"));
        ToolTipService.SetToolTip(GenPwBtn, P("Generate · 產生密碼", "產生密碼"));
        PwHint.Text = P("Setting the permanent password needs the RustDesk service installed and runs elevated (UAC).",
            "設定永久密碼需要已安裝 RustDesk 服務，並會要求管理員權限（UAC）。");

        ConnectTitle.Text = P("Quick connect · 快速連線", "快速連線");
        ViewOnlyChk.Content = P("View only · 只睇唔控制", "只睇");
        ConnectBtn.Content = P("Connect · 連線", "連線");
        SavePeerBtn.Content = P("Save to address book · 加入常用清單", "加入常用清單");

        PeersTitle.Text = P("Saved peers · 常用清單", "常用清單");
        PeersEmpty.Text = P("No saved peers yet — enter an ID above and tap “Save to address book”.",
            "未有常用對端 — 喺上面輸入 ID 再撳「加入常用清單」。");

        ServerTitle.Text = P("Self-hosted server · 自架伺服器", "自架伺服器");
        ServerBlurb.Text = P("Point RustDesk at your own ID / relay server. Saved to RustDesk2.toml — restart RustDesk for changes to take effect. Stop RustDesk before saving so it doesn't overwrite the file.",
            "將 RustDesk 指向你自己嘅 ID／中繼伺服器。寫入 RustDesk2.toml — 重啟 RustDesk 後生效。儲存前最好先關閉 RustDesk，免得覆蓋設定。");
        IdServerLbl.Text = P("ID server (custom-rendezvous-server) · ID 伺服器", "ID 伺服器");
        RelayServerLbl.Text = P("Relay server · 中繼伺服器", "中繼伺服器");
        ApiServerLbl.Text = P("API server · API 伺服器", "API 伺服器");
        KeyLbl.Text = P("Public key · 公開金鑰", "公開金鑰");
        SaveServerBtn.Content = P("Save settings · 儲存設定", "儲存設定");
        ReloadServerBtn.Content = P("Reload · 重新讀取", "重新讀取");
        ImportServerBtn.Content = P("Import config… · 匯入設定…", "匯入設定…");
        ClearServerBtn.Content = P("Clear · 清空", "清空");

        ServiceTitle.Text = P("Service & launch · 服務與啟動", "服務與啟動");
        ServiceBlurb.Text = P("Install the unattended service for background access (needs admin), or just launch the RustDesk window.",
            "安裝無人值守服務以便背景遠端存取（需要管理員），或者淨係開啟 RustDesk 視窗。");
        LaunchBtn.Content = P("Launch RustDesk · 啟動 RustDesk", "啟動 RustDesk");
        InstallSvcBtn.Content = P("Install service · 安裝服務", "安裝服務");
        UninstallSvcBtn.Content = P("Remove service · 移除服務", "移除服務");
        OpenConfigBtn.Content = P("Open config folder · 開啟設定資料夾", "開啟設定資料夾");
    }

    // ===================== Status / install =====================

    private void BuildInstallButton()
    {
        InstallButtonHost.Children.Clear();
        InstallButtonHost.Children.Add(EngineBars.AutoInstallButton(
            "RustDesk.RustDesk",
            "Install RustDesk", "安裝 RustDesk",
            async () => await RefreshStatus()));
    }

    private async Task RefreshStatus()
    {
        bool installed = RustDeskService.IsInstalled;
        InstallBar.IsOpen = !installed;
        if (!installed)
        {
            InstallBar.Title = P("RustDesk is not installed", "未安裝 RustDesk");
            InstallBar.Message = P("Install it to show your ID, connect to peers and manage the server.",
                "安裝後可顯示 ID、連線對端同管理伺服器。");
        }

        if (!installed)
        {
            StatusBar.Severity = InfoBarSeverity.Warning;
            StatusBar.Title = P("Not installed · 未安裝", "未安裝");
            StatusBar.Message = "";
            IdValue.Text = "—";
            return;
        }

        bool running = RustDeskService.IsRunning;
        StatusBar.Severity = running ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        StatusBar.Title = running ? P("RustDesk is running · 執行中", "執行中") : P("Installed · 已安裝", "已安裝");
        StatusBar.Message = running
            ? P("Background access is available.", "可以背景遠端存取。")
            : P("Launch RustDesk to start receiving connections.", "啟動 RustDesk 先可以接收連線。");

        try
        {
            var id = await RustDeskService.GetLocalId();
            IdValue.Text = string.IsNullOrEmpty(id) ? "—" : FormatId(id);
        }
        catch { IdValue.Text = "—"; }
    }

    private static string FormatId(string id)
    {
        // Group 9-digit IDs as "123 456 789" for readability.
        if (id.Length == 9 && id.All(char.IsDigit))
            return $"{id.Substring(0, 3)} {id.Substring(3, 3)} {id.Substring(6, 3)}";
        return id;
    }

    private void CopyId_Click(object sender, RoutedEventArgs e)
    {
        var id = RustDeskService.CleanId(IdValue.Text);
        if (id.Length == 0) return;
        var dp = new DataPackage();
        dp.SetText(id);
        Clipboard.SetContent(dp);
        Show(PwResult, true, P("ID copied", "已複製 ID"), id);
    }

    private async void RefreshId_Click(object sender, RoutedEventArgs e) => await RefreshStatus();

    // ===================== Permanent password =====================

    private void GenPw_Click(object sender, RoutedEventArgs e)
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var rnd = new Random();
        PwBox.Password = new string(Enumerable.Range(0, 12).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
    }

    private async void SetPw_Click(object sender, RoutedEventArgs e)
    {
        var pw = PwBox.Password;
        if (string.IsNullOrWhiteSpace(pw)) { Show(PwResult, false, P("Enter a password", "請輸入密碼"), ""); return; }
        SetPwBtn.IsEnabled = false;
        try
        {
            var r = await RustDeskService.SetPassword(pw);
            Show(PwResult, r.Success, r.Message?.Primary ?? "", r.Output ?? "");
            if (r.Success) PwBox.Password = "";
        }
        finally { SetPwBtn.IsEnabled = true; }
    }

    // ===================== Quick connect =====================

    private async void Connect_Click(object sender, RoutedEventArgs e)
        => await DoConnect(PeerIdBox.Text, ViewOnlyChk.IsChecked == true);

    private async Task DoConnect(string id, bool viewOnly)
    {
        var r = await RustDeskService.Connect(id, viewOnly);
        Show(ConnectResult, r.Success, r.Message?.Primary ?? "", "");
    }

    private void SavePeer_Click(object sender, RoutedEventArgs e)
    {
        var id = RustDeskService.CleanId(PeerIdBox.Text);
        if (id.Length == 0) { Show(ConnectResult, false, P("Enter an ID first", "請先輸入 ID"), ""); return; }
        if (_peers.Any(p => p.Id == id)) { Show(ConnectResult, false, P("Already saved", "已經喺清單"), ""); return; }
        _peers.Add(new RdPeer { Id = id, ViewOnly = ViewOnlyChk.IsChecked == true });
        RustDeskService.SavePeers(_peers);
        UpdatePeersEmpty();
        Show(ConnectResult, true, P("Saved to address book", "已加入清單"), id);
    }

    private void LoadPeers()
    {
        _peers.Clear();
        foreach (var p in RustDeskService.LoadPeers()) _peers.Add(p);
        UpdatePeersEmpty();
    }

    private void UpdatePeersEmpty()
        => PeersEmpty.Visibility = _peers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private async void PeerConnect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            var peer = _peers.FirstOrDefault(p => p.Id == id);
            await DoConnect(id, peer?.ViewOnly ?? false);
        }
    }

    private void PeerDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string id })
        {
            var peer = _peers.FirstOrDefault(p => p.Id == id);
            if (peer is not null) _peers.Remove(peer);
            RustDeskService.SavePeers(_peers);
            UpdatePeersEmpty();
        }
    }

    // ===================== Server settings =====================

    private void LoadServer()
    {
        var cfg = RustDeskService.ReadServerConfig();
        IdServerBox.Text = cfg.IdServer;
        RelayServerBox.Text = cfg.RelayServer;
        ApiServerBox.Text = cfg.ApiServer;
        KeyBox.Text = cfg.Key;
    }

    private async void SaveServer_Click(object sender, RoutedEventArgs e)
    {
        var cfg = new RdServerConfig
        {
            IdServer = IdServerBox.Text,
            RelayServer = RelayServerBox.Text,
            ApiServer = ApiServerBox.Text,
            Key = KeyBox.Text,
        };
        var r = await RustDeskService.WriteServerConfig(cfg);
        Show(ServerResult, r.Success, r.Message?.Primary ?? "", "");
    }

    private void ReloadServer_Click(object sender, RoutedEventArgs e)
    {
        LoadServer();
        Show(ServerResult, true, P("Reloaded from RustDesk2.toml", "已由 RustDesk2.toml 重新讀取"), "");
    }

    private void ClearServer_Click(object sender, RoutedEventArgs e)
    {
        IdServerBox.Text = RelayServerBox.Text = ApiServerBox.Text = KeyBox.Text = "";
    }

    private async void ImportServer_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".txt", ".toml");
        if (string.IsNullOrEmpty(path)) return;
        // Try a direct CLI import first; if that fails, parse the file for known keys.
        var r = await RustDeskService.ImportConfig(path);
        if (r.Success)
        {
            LoadServer();
            Show(ServerResult, true, P("Config imported", "已匯入設定"), "");
            return;
        }
        // Fallback: scan the file for host/relay/key/api lines and fill the form.
        try
        {
            var cfg = new RdServerConfig();
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) eq = line.IndexOf(':');
                if (eq <= 0) continue;
                var k = line.Substring(0, eq).Trim().Trim('"', '\'').ToLowerInvariant();
                var v = line.Substring(eq + 1).Trim().Trim('"', '\'', ',');
                switch (k)
                {
                    case "host":
                    case "custom-rendezvous-server":
                    case "id-server":
                    case "idserver": cfg.IdServer = v; break;
                    case "relay":
                    case "relay-server": cfg.RelayServer = v; break;
                    case "api":
                    case "api-server": cfg.ApiServer = v; break;
                    case "key": cfg.Key = v; break;
                }
            }
            IdServerBox.Text = cfg.IdServer;
            RelayServerBox.Text = cfg.RelayServer;
            ApiServerBox.Text = cfg.ApiServer;
            KeyBox.Text = cfg.Key;
            Show(ServerResult, true, P("Parsed from file — review then Save", "由檔案解析 — 檢查後再儲存"), "");
        }
        catch (Exception ex)
        {
            Show(ServerResult, false, P("Could not import", "匯入失敗"), ex.Message);
        }
    }

    // ===================== Service / launch =====================

    private async void Launch_Click(object sender, RoutedEventArgs e)
    {
        var r = await RustDeskService.Launch();
        Show(ServiceResult, r.Success, r.Message?.Primary ?? "", "");
        await Task.Delay(800);
        await RefreshStatus();
    }

    private async void InstallSvc_Click(object sender, RoutedEventArgs e) => await RunSvc(RustDeskService.InstallService);
    private async void UninstallSvc_Click(object sender, RoutedEventArgs e) => await RunSvc(RustDeskService.UninstallService);

    private async Task RunSvc(Func<System.Threading.CancellationToken, Task<TweakResult>> op)
    {
        SvcBusy.IsActive = true;
        try
        {
            var r = await op(default);
            Show(ServiceResult, r.Success, r.Message?.Primary ?? "", r.Output ?? "");
            await RefreshStatus();
        }
        finally { SvcBusy.IsActive = false; }
    }

    private void OpenConfig_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = RustDeskService.ConfigDir;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Show(ServiceResult, false, P("Could not open folder", "開唔到資料夾"), ex.Message);
        }
    }

    // ===================== InfoBar helper =====================

    private void Show(InfoBar bar, bool ok, string title, string msg)
    {
        bar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        bar.Title = title;
        bar.Message = msg ?? "";
        bar.IsOpen = true;
    }
}
