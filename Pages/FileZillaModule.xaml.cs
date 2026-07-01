using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// FTP／SFTP 檔案傳輸（FileZilla 風格）· FileZilla-style FTP/FTPS/SFTP client: a Site Manager of saved
/// sites with DPAPI-encrypted secrets, a quickconnect bar, a dual local/remote browser, and a serial
/// transfer queue with per-item + overall progress and resume. All strings bilingual (English + 粵語).
/// </summary>
public sealed partial class FileZillaModule : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _ui;

    private FtpService? _client;
    private FtpSite? _connectedSite;

    private string _localDir = "";
    private string _remoteDir = "/";

    private readonly ObservableCollection<SiteRow> _sites = new();
    private readonly ObservableCollection<LocalRow> _localRows = new();
    private readonly ObservableCollection<RemoteRow> _remoteRows = new();
    private readonly ObservableCollection<QueueRow> _queue = new();

    private CancellationTokenSource? _transferCts;
    private bool _pumping;

    public FileZillaModule()
    {
        InitializeComponent();
        _ui = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        SitesList.ItemsSource = _sites;
        LocalList.ItemsSource = _localRows;
        RemoteList.ItemsSource = _remoteRows;
        QueueList.ItemsSource = _queue;

        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildProtocolCombo();
        BuildOpsCards();
        LoadSites();
        _localDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        RefreshLocal();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        try { _transferCts?.Cancel(); } catch { }
        _client?.Dispose();
    }

    private void OnLang(object? sender, EventArgs e) { Render(); RefreshLocal(); UpdateQueueHeader(); }

    private void Render()
    {
        Header.Title = "FTP / SFTP · FTP／SFTP 檔案傳輸";
        Header.Subtitle = P(
            "Native FileZilla-style transfers: saved sites with encrypted credentials, FTP / FTPS / SFTP, a dual local + remote browser, and a resumable transfer queue.",
            "原生 FileZilla 式傳輸：加密憑證嘅已儲存站台、FTP／FTPS／SFTP、雙窗本機＋遠端瀏覽，仲有可續傳嘅傳輸佇列。");
        SiteMgrHeader.Text = P("Site Manager", "站台管理");
        LocalHeader.Text = P("Local · 本機", "本機 · Local");
        RemoteHeader.Text = P("Remote · 遠端", "遠端 · Remote");
        QcConnectBtn.Content = P("Quickconnect", "快速連線");
        DisconnectBtn.Content = P("Disconnect", "斷線");
        ResumeCheck.Content = P("Resume", "續傳");
        CancelTransferBtn.Content = P("Cancel transfer", "取消傳輸");
        ClearQueueBtn.Content = P("Clear finished", "清除已完成");
        HelpHeader.Text = P("About & help", "關於與說明");
        UpdateQueueHeader();
    }

    private void BuildOpsCards()
    {
        if (OpsPanel.Children.Count > 0) return;
        var rows = new Controls.ControlRowList();
        rows.SetTweaks(Catalog.FileZillaOperations.All());
        OpsPanel.Children.Add(rows);
    }

    private void BuildProtocolCombo()
    {
        if (QcProtocol.Items.Count > 0) return;
        QcProtocol.Items.Add(new ComboBoxItem { Content = "SFTP", Tag = FtpProtocol.Sftp });
        QcProtocol.Items.Add(new ComboBoxItem { Content = "FTP", Tag = FtpProtocol.Ftp });
        QcProtocol.Items.Add(new ComboBoxItem { Content = "FTPS", Tag = FtpProtocol.Ftps });
        QcProtocol.SelectedIndex = 0;
    }

    // ===================== Site Manager =====================

    private void LoadSites()
    {
        var selectedId = (SitesList.SelectedItem as SiteRow)?.Site.Id;
        _sites.Clear();
        foreach (var s in FtpSiteStore.All())
            _sites.Add(new SiteRow(s));
        if (selectedId is not null)
            SitesList.SelectedItem = _sites.FirstOrDefault(r => r.Site.Id == selectedId);
    }

    private void SitesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool has = SitesList.SelectedItem is SiteRow;
        EditSiteBtn.IsEnabled = has;
        DeleteSiteBtn.IsEnabled = has;
        ConnectSiteBtn.IsEnabled = has;
    }

    private async void NewSite_Click(object sender, RoutedEventArgs e)
    {
        var site = new FtpSite { Name = P("New site", "新站台"), Protocol = FtpProtocol.Sftp, Port = 22 };
        if (await ShowSiteEditorAsync(site, isNew: true))
        {
            FtpSiteStore.Save(site);
            LoadSites();
            SitesList.SelectedItem = _sites.FirstOrDefault(r => r.Site.Id == site.Id);
        }
    }

    private async void EditSite_Click(object sender, RoutedEventArgs e)
    {
        if (SitesList.SelectedItem is not SiteRow row) return;
        var clone = row.Site.Clone();
        if (await ShowSiteEditorAsync(clone, isNew: false))
        {
            FtpSiteStore.Save(clone);
            LoadSites();
            SitesList.SelectedItem = _sites.FirstOrDefault(r => r.Site.Id == clone.Id);
        }
    }

    private async void DeleteSite_Click(object sender, RoutedEventArgs e)
    {
        if (SitesList.SelectedItem is not SiteRow row) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete site?", "刪除站台？"),
            Content = $"{row.Site.Name}\n{row.Site.Host}",
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            FtpSiteStore.Delete(row.Site.Id);
            LoadSites();
        }
    }

    private async void ConnectSite_Click(object sender, RoutedEventArgs e)
    {
        if (SitesList.SelectedItem is not SiteRow row) return;
        await ConnectAsync(row.Site, FtpSiteStore.Unprotect(row.Site.EncryptedSecret));
    }

    /// <summary>站台編輯器對話框 · A bilingual site-editor dialog. Returns true if saved.</summary>
    private async Task<bool> ShowSiteEditorAsync(FtpSite site, bool isNew)
    {
        var name = new TextBox { Header = P("Name · 名稱", "名稱 · Name"), Text = site.Name };
        var proto = new ComboBox { Header = P("Protocol · 協定", "協定 · Protocol"), MinWidth = 160 };
        proto.Items.Add(new ComboBoxItem { Content = "SFTP", Tag = FtpProtocol.Sftp });
        proto.Items.Add(new ComboBoxItem { Content = "FTP", Tag = FtpProtocol.Ftp });
        proto.Items.Add(new ComboBoxItem { Content = "FTPS", Tag = FtpProtocol.Ftps });
        proto.SelectedIndex = site.Protocol switch { FtpProtocol.Sftp => 0, FtpProtocol.Ftp => 1, _ => 2 };

        var host = new TextBox { Header = P("Host · 主機", "主機 · Host"), Text = site.Host };
        var port = new TextBox { Header = P("Port · 連接埠", "連接埠 · Port"), Text = (site.Port > 0 ? site.Port : site.DefaultPort).ToString(), Width = 110 };
        var user = new TextBox { Header = P("User · 使用者", "使用者 · User"), Text = site.User };

        var auth = new ComboBox { Header = P("Auth · 驗證", "驗證 · Auth"), MinWidth = 160 };
        auth.Items.Add(new ComboBoxItem { Content = P("Password · 密碼", "密碼 · Password"), Tag = SftpAuth.Password });
        auth.Items.Add(new ComboBoxItem { Content = P("Key file (SFTP) · 私鑰檔（SFTP）", "私鑰檔（SFTP）· Key file"), Tag = SftpAuth.KeyFile });
        auth.SelectedIndex = site.Auth == SftpAuth.KeyFile ? 1 : 0;

        var pass = new PasswordBox { Header = P("Password / passphrase · 密碼／密語", "密碼／密語 · Password / passphrase"), Password = FtpSiteStore.Unprotect(site.EncryptedSecret) };

        var keyPath = new TextBox { Header = P("Private key file · 私鑰檔", "私鑰檔 · Private key file"), Text = site.KeyFilePath, IsEnabled = site.Auth == SftpAuth.KeyFile };
        var keyBrowse = new Button { Content = P("Browse… · 瀏覽…", "瀏覽… · Browse…"), Margin = new Thickness(0, 28, 0, 0) };
        keyBrowse.Click += async (_, _) =>
        {
            var p = await FileDialogs.OpenFileAsync();
            if (p is not null) keyPath.Text = p;
        };
        var keyRow = new Grid { ColumnSpacing = 8 };
        keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(keyPath, 0); Grid.SetColumn(keyBrowse, 1);
        keyRow.Children.Add(keyPath); keyRow.Children.Add(keyBrowse);

        var remoteDir = new TextBox { Header = P("Default remote dir (optional) · 預設遠端目錄（可空）", "預設遠端目錄（可空）· Default remote dir"), Text = site.RemoteDir };

        auth.SelectionChanged += (_, _) =>
        {
            var a = (auth.SelectedItem as ComboBoxItem)?.Tag as SftpAuth? ?? SftpAuth.Password;
            keyPath.IsEnabled = a == SftpAuth.KeyFile;
            keyBrowse.IsEnabled = a == SftpAuth.KeyFile;
        };
        proto.SelectionChanged += (_, _) =>
        {
            var pr = (proto.SelectedItem as ComboBoxItem)?.Tag as FtpProtocol? ?? FtpProtocol.Sftp;
            // 切換協定時，如果連接埠仲係預設值就更新 · refresh port to the protocol default if untouched.
            if (int.TryParse(port.Text, out var cur) && (cur == 21 || cur == 22))
                port.Text = (pr == FtpProtocol.Sftp ? 22 : 21).ToString();
            bool sftp = pr == FtpProtocol.Sftp;
            auth.IsEnabled = sftp;
            if (!sftp) { auth.SelectedIndex = 0; keyPath.IsEnabled = false; keyBrowse.IsEnabled = false; }
        };

        var panel = new StackPanel { Spacing = 10, Width = 420 };
        panel.Children.Add(name);
        panel.Children.Add(proto);
        var hp = new Grid { ColumnSpacing = 8 };
        hp.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        hp.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(host, 0); Grid.SetColumn(port, 1);
        hp.Children.Add(host); hp.Children.Add(port);
        panel.Children.Add(hp);
        panel.Children.Add(user);
        panel.Children.Add(auth);
        panel.Children.Add(pass);
        panel.Children.Add(keyRow);
        panel.Children.Add(remoteDir);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = isNew ? P("New site", "新增站台") : P("Edit site", "編輯站台"),
            Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 520 },
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return false;

        site.Name = string.IsNullOrWhiteSpace(name.Text) ? (string.IsNullOrWhiteSpace(host.Text) ? "site" : host.Text) : name.Text.Trim();
        site.Protocol = (proto.SelectedItem as ComboBoxItem)?.Tag as FtpProtocol? ?? FtpProtocol.Sftp;
        site.Host = host.Text.Trim();
        site.Port = int.TryParse(port.Text, out var pv) && pv > 0 ? pv : site.DefaultPort;
        site.User = user.Text.Trim();
        site.Auth = (auth.SelectedItem as ComboBoxItem)?.Tag as SftpAuth? ?? SftpAuth.Password;
        site.KeyFilePath = keyPath.Text.Trim();
        site.RemoteDir = remoteDir.Text.Trim();
        site.EncryptedSecret = FtpSiteStore.Protect(pass.Password);
        return true;
    }

    // ===================== Connect / Disconnect =====================

    private void BuildQuickConnectSite(out FtpSite site, out string password)
    {
        var proto = (QcProtocol.SelectedItem as ComboBoxItem)?.Tag as FtpProtocol? ?? FtpProtocol.Sftp;
        int port = int.TryParse(QcPort.Text, out var p) && p > 0 ? p : (proto == FtpProtocol.Sftp ? 22 : 21);
        site = new FtpSite
        {
            Name = QcHost.Text,
            Protocol = proto,
            Host = QcHost.Text.Trim(),
            Port = port,
            User = QcUser.Text.Trim(),
            Auth = SftpAuth.Password,
        };
        password = QcPass.Password;
    }

    private async void QuickConnect_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(QcHost.Text))
        {
            ShowStatus(InfoBarSeverity.Warning, P("Enter a host", "請輸入主機"), "");
            return;
        }
        BuildQuickConnectSite(out var site, out var pass);
        await ConnectAsync(site, pass);
    }

    private async Task ConnectAsync(FtpSite site, string password)
    {
        await DisconnectAsync();

        QcConnectBtn.IsEnabled = false;
        ConnectSiteBtn.IsEnabled = false;
        ShowStatus(InfoBarSeverity.Informational, P($"Connecting to {site.Host}…", $"正在連線到 {site.Host}…"), "");

        var client = new FtpService(site, password);
        client.TrustCallback = PromptTrustAsync;
        client.FingerprintTrusted += fp =>
        {
            if (!string.IsNullOrEmpty(site.Id)) FtpSiteStore.TrustFingerprint(site.Id, fp);
            site.TrustedFingerprint = fp;
        };

        try
        {
            await Task.Run(() => client.ConnectAsync(CancellationToken.None));
            _client = client;
            _connectedSite = site;
            _remoteDir = await client.GetHomeDirectoryAsync(CancellationToken.None);
            if (string.IsNullOrWhiteSpace(_remoteDir)) _remoteDir = "/";

            DisconnectBtn.IsEnabled = true;
            RemotePath.IsEnabled = true;
            UpdateTransferButtons();
            ShowStatus(InfoBarSeverity.Success, P($"Connected · {site.Protocol} {site.Host}", $"已連線 · {site.Protocol} {site.Host}"), "");
            await RefreshRemoteAsync();
        }
        catch (Exception ex)
        {
            client.Dispose();
            ShowStatus(InfoBarSeverity.Error, P("Connection failed", "連線失敗"), ex.Message);
        }
        finally
        {
            QcConnectBtn.IsEnabled = true;
            ConnectSiteBtn.IsEnabled = SitesList.SelectedItem is SiteRow;
        }
    }

    private void Disconnect_Click(object sender, RoutedEventArgs e) => _ = DisconnectAsync();

    private async Task DisconnectAsync()
    {
        try { _transferCts?.Cancel(); } catch { }
        _client?.Dispose();
        _client = null;
        _connectedSite = null;
        _remoteRows.Clear();
        RemotePath.Text = "";
        RemotePath.IsEnabled = false;
        DisconnectBtn.IsEnabled = false;
        UpdateTransferButtons();
        await Task.CompletedTask;
    }

    /// <summary>TOFU 信任提示 · Show a trust-on-first-use dialog for an unknown host key / cert.</summary>
    private Task<TrustDecision> PromptTrustAsync(TrustPrompt prompt)
    {
        var tcs = new TaskCompletionSource<TrustDecision>();
        _ui.TryEnqueue(async () =>
        {
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Text = P(
                    $"The {prompt.Kind} for {prompt.Host} is not yet trusted. Verify the fingerprint before continuing.",
                    $"{prompt.Host} 嘅 {prompt.Kind} 仲未受信任。繼續之前請核對指紋。"),
            });
            body.Children.Add(new TextBlock { Text = prompt.Detail, Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"], FontSize = 12 });
            body.Children.Add(new TextBox { Text = prompt.Fingerprint, IsReadOnly = true, Header = "Fingerprint (SHA-256) · 指紋", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas") });

            var dlg = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = P("Unknown host — trust?", "未知主機 — 是否信任？"),
                Content = body,
                PrimaryButtonText = P("Trust always", "永遠信任"),
                SecondaryButtonText = P("Trust once", "信任一次"),
                CloseButtonText = P("Reject", "拒絕"),
                DefaultButton = ContentDialogButton.Secondary,
            };
            var r = await dlg.ShowAsync();
            tcs.SetResult(r switch
            {
                ContentDialogResult.Primary => TrustDecision.Always,
                ContentDialogResult.Secondary => TrustDecision.Once,
                _ => TrustDecision.Reject,
            });
        });
        return tcs.Task;
    }

    // ===================== Local pane =====================

    private void RefreshLocal()
    {
        _localRows.Clear();
        LocalPath.Text = _localDir;
        try
        {
            var di = new DirectoryInfo(_localDir);
            if (!di.Exists) return;
            foreach (var d in di.GetDirectories().OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                _localRows.Add(LocalRow.ForDir(d));
            foreach (var f in di.GetFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                _localRows.Add(LocalRow.ForFile(f));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Cannot read folder", "讀唔到資料夾"), ex.Message);
        }
    }

    private void LocalRefresh_Click(object sender, RoutedEventArgs e) => RefreshLocal();

    private void LocalUp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var parent = Directory.GetParent(_localDir.TrimEnd(Path.DirectorySeparatorChar));
            if (parent is not null) { _localDir = parent.FullName; RefreshLocal(); }
        }
        catch { }
    }

    private async void LocalBrowse_Click(object sender, RoutedEventArgs e)
    {
        var p = await FileDialogs.OpenFolderAsync(P("Pick a local folder", "揀一個本機資料夾"));
        if (p is not null) { _localDir = p; RefreshLocal(); }
    }

    private void LocalPath_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        if (Directory.Exists(LocalPath.Text)) { _localDir = LocalPath.Text; RefreshLocal(); }
        else ShowStatus(InfoBarSeverity.Warning, P("No such folder", "搵唔到資料夾"), LocalPath.Text);
    }

    private void LocalList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (LocalList.SelectedItem is LocalRow row && row.IsDirectory)
        {
            _localDir = row.FullPath;
            RefreshLocal();
        }
    }

    private async void LocalMkdir_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptTextAsync(P("New local folder", "新本機資料夾"), P("Folder name", "資料夾名稱"));
        if (string.IsNullOrWhiteSpace(name)) return;
        try { Directory.CreateDirectory(Path.Combine(_localDir, name.Trim())); RefreshLocal(); }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P("Create failed", "建立失敗"), ex.Message); }
    }

    // ===================== Remote pane =====================

    private async Task RefreshRemoteAsync()
    {
        if (_client is null) return;
        RemotePath.Text = _remoteDir;
        _remoteRows.Clear();
        try
        {
            var entries = await Task.Run(() => _client.ListAsync(_remoteDir, CancellationToken.None));
            foreach (var en in entries)
                _remoteRows.Add(new RemoteRow(en));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Cannot list remote folder", "列唔到遠端資料夾"), ex.Message);
        }
    }

    private void RemoteRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshRemoteAsync();

    private async void RemoteUp_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null) return;
        _remoteDir = FtpService.ParentRemote(_remoteDir);
        await RefreshRemoteAsync();
    }

    private async void RemotePath_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || _client is null) return;
        _remoteDir = string.IsNullOrWhiteSpace(RemotePath.Text) ? "/" : RemotePath.Text.Trim();
        await RefreshRemoteAsync();
    }

    private async void RemoteList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (RemoteList.SelectedItem is RemoteRow row && row.IsDirectory && _client is not null)
        {
            _remoteDir = row.Entry.FullPath;
            await RefreshRemoteAsync();
        }
    }

    private async void RemoteMkdir_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null) return;
        var name = await PromptTextAsync(P("New remote folder", "新遠端資料夾"), P("Folder name", "資料夾名稱"));
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            await Task.Run(() => _client.CreateDirectoryAsync(FtpService.CombineRemote(_remoteDir, name.Trim()), CancellationToken.None));
            await RefreshRemoteAsync();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P("Create failed", "建立失敗"), ex.Message); }
    }

    // ===================== Context actions: rename / delete =====================

    private async void LocalRename_Click(object sender, RoutedEventArgs e)
    {
        if (LocalList.SelectedItem is not LocalRow row) return;
        var name = await PromptTextAsync(P("Rename", "重新命名"), P("New name", "新名稱"), row.Name);
        if (string.IsNullOrWhiteSpace(name) || name == row.Name) return;
        try
        {
            var dest = Path.Combine(_localDir, name.Trim());
            if (row.IsDirectory) Directory.Move(row.FullPath, dest); else File.Move(row.FullPath, dest);
            RefreshLocal();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P("Rename failed", "重新命名失敗"), ex.Message); }
    }

    private async void LocalDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LocalList.SelectedItem is not LocalRow row) return;
        if (!await ConfirmAsync(P("Delete local item?", "刪除本機項目？"), row.Name)) return;
        try
        {
            if (row.IsDirectory) Directory.Delete(row.FullPath, true); else File.Delete(row.FullPath);
            RefreshLocal();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P("Delete failed", "刪除失敗"), ex.Message); }
    }

    private async void RemoteRename_Click(object sender, RoutedEventArgs e)
    {
        if (RemoteList.SelectedItem is not RemoteRow row || _client is null) return;
        var name = await PromptTextAsync(P("Rename", "重新命名"), P("New name", "新名稱"), row.Name);
        if (string.IsNullOrWhiteSpace(name) || name == row.Name) return;
        try
        {
            var dest = FtpService.CombineRemote(_remoteDir, name.Trim());
            await Task.Run(() => _client.RenameAsync(row.Entry.FullPath, dest, CancellationToken.None));
            await RefreshRemoteAsync();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P("Rename failed", "重新命名失敗"), ex.Message); }
    }

    private async void RemoteDelete_Click(object sender, RoutedEventArgs e)
    {
        if (RemoteList.SelectedItem is not RemoteRow row || _client is null) return;
        if (!await ConfirmAsync(P("Delete remote item?", "刪除遠端項目？"), row.Name)) return;
        try
        {
            if (row.IsDirectory) await Task.Run(() => _client.DeleteDirectoryAsync(row.Entry.FullPath, CancellationToken.None));
            else await Task.Run(() => _client.DeleteFileAsync(row.Entry.FullPath, CancellationToken.None));
            await RefreshRemoteAsync();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P("Delete failed", "刪除失敗"), ex.Message); }
    }

    // ===================== Transfer queue =====================

    private void UpdateTransferButtons()
    {
        bool connected = _client is not null;
        UploadBtn.IsEnabled = connected;
        DownloadBtn.IsEnabled = connected;
    }

    private void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null) return;
        var sel = LocalList.SelectedItems.OfType<LocalRow>().Where(r => !r.IsDirectory).ToList();
        if (sel.Count == 0) { ShowStatus(InfoBarSeverity.Warning, P("Select local file(s)", "請揀本機檔案"), ""); return; }
        foreach (var f in sel)
            _queue.Add(QueueRow.Upload(f.FullPath, FtpService.CombineRemote(_remoteDir, f.Name), f.Name));
        UpdateQueueHeader();
        PumpQueue();
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_client is null) return;
        var sel = RemoteList.SelectedItems.OfType<RemoteRow>().Where(r => !r.IsDirectory).ToList();
        if (sel.Count == 0) { ShowStatus(InfoBarSeverity.Warning, P("Select remote file(s)", "請揀遠端檔案"), ""); return; }
        foreach (var f in sel)
            _queue.Add(QueueRow.Download(f.Entry.FullPath, Path.Combine(_localDir, f.Name), f.Name));
        UpdateQueueHeader();
        PumpQueue();
    }

    private async void PumpQueue()
    {
        if (_pumping || _client is null) return;
        _pumping = true;
        _transferCts = new CancellationTokenSource();
        CancelTransferBtn.IsEnabled = true;
        var ct = _transferCts.Token;
        bool resume = ResumeCheck.IsChecked == true;

        try
        {
            while (true)
            {
                var item = _queue.FirstOrDefault(q => q.Status == QueueStatus.Pending);
                if (item is null) break;
                if (_client is null) break;

                item.SetStatus(QueueStatus.Active, P("transferring…", "傳輸中…"));
                var progress = new Progress<double>(v => _ui.TryEnqueue(() => { item.Progress = v; UpdateOverall(); }));
                try
                {
                    if (item.IsUpload)
                        await Task.Run(() => _client.UploadAsync(item.LocalPath, item.RemotePath, resume, progress, ct), ct);
                    else
                        await Task.Run(() => _client.DownloadAsync(item.RemotePath, item.LocalPath, resume, progress, ct), ct);

                    item.Progress = 1;
                    item.SetStatus(QueueStatus.Done, P("done", "完成"));
                }
                catch (OperationCanceledException)
                {
                    item.SetStatus(QueueStatus.Cancelled, P("cancelled", "已取消"));
                    foreach (var q in _queue.Where(q => q.Status == QueueStatus.Pending))
                        q.SetStatus(QueueStatus.Cancelled, P("cancelled", "已取消"));
                    break;
                }
                catch (Exception ex)
                {
                    item.SetStatus(QueueStatus.Failed, ex.Message);
                }
                UpdateOverall();
            }
            // 傳輸完後重新整理兩窗，睇到新檔案 · refresh both panes after transfers.
            RefreshLocal();
            if (_client is not null) await RefreshRemoteAsync();
        }
        finally
        {
            _pumping = false;
            CancelTransferBtn.IsEnabled = false;
            _transferCts?.Dispose();
            _transferCts = null;
            UpdateOverall();
            UpdateQueueHeader();
        }
    }

    private void UpdateOverall()
    {
        if (_queue.Count == 0) { OverallProgress.Value = 0; return; }
        var relevant = _queue.Where(q => q.Status != QueueStatus.Cancelled).ToList();
        if (relevant.Count == 0) { OverallProgress.Value = 0; return; }
        OverallProgress.Value = relevant.Average(q => q.Status == QueueStatus.Done ? 1.0 : q.Progress);
    }

    private void CancelTransfer_Click(object sender, RoutedEventArgs e)
    {
        try { _transferCts?.Cancel(); } catch { }
    }

    private void ClearQueue_Click(object sender, RoutedEventArgs e)
    {
        for (int i = _queue.Count - 1; i >= 0; i--)
            if (_queue[i].Status is QueueStatus.Done or QueueStatus.Failed or QueueStatus.Cancelled)
                _queue.RemoveAt(i);
        UpdateOverall();
        UpdateQueueHeader();
    }

    private void UpdateQueueHeader()
    {
        int active = _queue.Count(q => q.Status is QueueStatus.Pending or QueueStatus.Active);
        int done = _queue.Count(q => q.Status == QueueStatus.Done);
        QueueHeader.Text = P($"Transfer queue · {active} queued, {done} done", $"傳輸佇列 · 排隊 {active}、完成 {done}");
    }

    // ===================== shared helpers =====================

    private void ShowStatus(InfoBarSeverity sev, string title, string message)
    {
        StatusBar.Severity = sev;
        StatusBar.Title = title;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    private async Task<bool> ConfirmAsync(string title, string detail)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = detail,
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<string?> PromptTextAsync(string title, string header, string initial = "")
    {
        var box = new TextBox { Header = header, Text = initial };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = P("OK", "確定"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary ? box.Text : null;
    }
}
