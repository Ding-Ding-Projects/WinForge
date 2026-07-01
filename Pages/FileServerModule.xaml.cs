using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 檔案伺服器 · File Server — host one of your folders out over SFTP or FTP/FTPS by running a server in
/// Docker (atmoz/sftp or alpine-ftp-server). Many shares run at once on auto-picked ports; passwords are
/// DPAPI-protected. The FTP/SFTP client module can then connect to what you host. Fully in-app & bilingual.
/// </summary>
public sealed partial class FileServerModule : Page
{
    private bool _busy;

    public FileServerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
        Loaded += async (_, _) => { Render(); await Refresh(); await CheckDocker(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "File Server · 檔案伺服器（FTP／SFTP 主機）";
        HeaderBlurb.Text = P(
            "Host one of your folders out over SFTP or FTP/FTPS by running a server in Docker. Each share runs on its own auto-picked port, so you can host several at once and connect from another machine.",
            "用 Docker 行一個伺服器，將你嘅資料夾經 SFTP 或 FTP／FTPS 對外分享。每個分享用自己自動揀嘅連接埠，可以同時開幾個，並由另一部機連入。");

        NewTitle.Text = P("Host a folder", "分享一個資料夾");
        NewDesc.Text = P("Pick a folder, a protocol and credentials, then start the container.",
            "揀資料夾、通訊協定同帳戶密碼，然後啟動容器。");
        NameLabel.Text = P("Share name", "分享名稱");
        FolderLabel.Text = P("Folder", "資料夾");
        BrowseBtn.Content = P("Browse…", "瀏覽…");
        ProtoLabel.Text = P("Protocol", "通訊協定");
        UserLabel.Text = P("Login", "登入");
        UserBox.PlaceholderText = P("username", "使用者名稱");
        PwdBox.PlaceholderText = P("password", "密碼");
        PortLabel.Text = P("Host port", "主機連接埠");
        AutoPortCheck.Content = P("Auto-pick a free port", "自動揀一個空連接埠");
        AddBtn.Content = P("Add share", "新增分享");
        SharesTitle.Text = P("Hosted shares", "已分享");
        SharesEmpty.Text = P("No shares yet.", "未有分享。");
        OutputTitle.Text = P("Output", "輸出");
        SecurityBar.Title = P("Before you host", "分享之前");
        SecurityBar.Message = P(
            "Bind-mounting grants the container read/write to the exact folder you choose — never a drive root. Plain FTP is unencrypted; prefer SFTP. Opening a port may need a Windows Firewall rule. Keep it on your LAN unless you mean to expose it.",
            "繫結掛載會畀容器讀寫你揀嘅資料夾 — 唔好揀磁碟根目錄。純 FTP 冇加密，建議用 SFTP。開連接埠可能要加 Windows 防火牆規則。除非有意對外，否則只限區域網。");
    }

    private void Show(TweakResult r, string verb)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = r.Message is null ? verb : $"{verb} — {r.Message.Primary}";
        ResultBar.IsOpen = true;
    }

    private async Task CheckDocker()
    {
        try
        {
            bool ok = await FileServerService.DockerReachableAsync();
            DockerBar.IsOpen = !ok;
            if (!ok)
            {
                DockerBar.Title = P("Docker engine not reachable", "連唔到 Docker 引擎");
                DockerBar.Message = P("Start Docker Desktop (or the engine) to host shares. You can still edit definitions.",
                    "請啟動 Docker Desktop（或引擎）先可以分享。你仍然可以編輯定義。");
            }
        }
        catch (Exception ex) { CrashLogger.Log("fileserver.docker", ex); }
    }

    private async Task Refresh()
    {
        try
        {
            var shares = FileServerService.Load();
            SharesPanel.ItemsSource = shares;
            SharesEmpty.Visibility = shares.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex) { CrashLogger.Log("fileserver.refresh", ex); }
        await Task.CompletedTask;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await FileDialogs.OpenFolderAsync(P("Pick a folder to host", "揀一個要分享嘅資料夾"));
            if (folder is not null) FolderBox.Text = folder;
        }
        catch (Exception ex) { CrashLogger.Log("fileserver.browse", ex); }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        try
        {
            if (string.IsNullOrWhiteSpace(FolderBox.Text))
            { Warn(P("Pick a folder first.", "請先揀資料夾。")); return; }
            if (string.IsNullOrWhiteSpace(PwdBox.Password))
            { Warn(P("Set a password.", "請設定密碼。")); return; }

            var proto = (ProtoBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "sftp";
            int port = AutoPortCheck.IsChecked == true
                ? FileServerService.FindFreePort(proto == "ftp" ? 2121 : 2222)
                : (int)PortBox.Value;
            if (port <= 0) port = FileServerService.FindFreePort(proto == "ftp" ? 2121 : 2222);

            var share = new HostedShare
            {
                Name = string.IsNullOrWhiteSpace(NameBox.Text) ? P("Share", "分享") : NameBox.Text.Trim(),
                FolderPath = FolderBox.Text.Trim(),
                Protocol = proto,
                Username = string.IsNullOrWhiteSpace(UserBox.Text) ? "user" : UserBox.Text.Trim(),
                Port = port,
                PasvBase = FileServerService.FindFreePort(21000),
            };
            FileServerService.SaveShare(share, PwdBox.Password);
            NameBox.Text = ""; FolderBox.Text = ""; PwdBox.Password = "";
            Show(TweakResult.Ok($"Share \"{share.Name}\" added. Press Start to host it.",
                $"已新增分享「{share.Name}」。撳啟動嚟分享。"), P("Add share", "新增分享"));
            await Refresh();
        }
        catch (Exception ex) { CrashLogger.Log("fileserver.add", ex); Warn(ex.Message); }
    }

    private HostedShare? ById(object? tag)
    {
        var id = tag as string;
        if (id is null) return null;
        return FileServerService.Load().Find(s => s.Id == id);
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        var s = ById((sender as Button)?.Tag);
        if (s is null) return;
        await Run(() => FileServerService.StartShareAsync(s, MakeLog()), P("Start share", "啟動分享"));
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        var s = ById((sender as Button)?.Tag);
        if (s is null) return;
        await Run(() => FileServerService.StopShareAsync(s, MakeLog()), P("Stop share", "停止分享"));
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        var s = ById((sender as Button)?.Tag);
        if (s is null) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Remove share?", "移除分享？"),
            Content = P($"Stop and remove \"{s.Name}\"? Your folder's contents are not touched.",
                       $"停止並移除「{s.Name}」？你資料夾入面嘅內容唔會受影響。"),
            PrimaryButtonText = P("Remove", "移除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await SafeShowAsync(dlg) != ContentDialogResult.Primary) return;
        await Run(() => FileServerService.RemoveShareAsync(s, MakeLog()), P("Remove share", "移除分享"));
        await Refresh();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var s = ById((sender as Button)?.Tag);
        if (s is null) return;
        try
        {
            var conn = FileServerService.ConnectionString(s);
            var dp = new DataPackage();
            dp.SetText(conn);
            Clipboard.SetContent(dp);
            Show(TweakResult.Ok($"Copied: {conn}", $"已複製：{conn}"), P("Copy connection", "複製連線"));
        }
        catch (Exception ex) { CrashLogger.Log("fileserver.copy", ex); Warn(ex.Message); }
    }

    private IProgress<string> MakeLog() => new Progress<string>(line =>
        DispatcherQueue.TryEnqueue(() => OutputText.Text += line + "\n"));

    private async Task Run(Func<Task<TweakResult>> op, string verb)
    {
        if (_busy) return;
        _busy = true;
        try { Show(await op(), verb); }
        catch (Exception ex) { CrashLogger.Log("fileserver", ex); Warn(ex.Message); }
        finally { _busy = false; await Refresh(); }
    }

    private void Warn(string message)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "提提你");
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private static async Task<ContentDialogResult> SafeShowAsync(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("fileserver.dialog", ex); return ContentDialogResult.None; }
    }
}
