using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// SSH 工具 · SSH Toolset — saved profiles (DPAPI-encrypted secrets), an in-app interactive
/// terminal (SSH.NET ShellStream), key management (generate + list ~/.ssh), one-click passwordless
/// deploy, and an SFTP browser. Fully bilingual (English + 廣東話). All file picking goes through
/// Services/FileDialogs (never WinRT pickers).
/// </summary>
public sealed partial class SshModule : Page
{
    private SshProfile? _editing;       // the profile currently in the editor (new or existing)
    private SshProfile? _connected;     // the profile the terminal/SFTP last used
    private string _sftpPath = ".";

    public SshModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        SshProfileStore.Changed += OnStoreChanged;
        Terminal.Disconnected += (_, _) => DispatcherQueue.TryEnqueue(UpdateTerminalButtons);
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            SshProfileStore.Changed -= OnStoreChanged;
            Terminal.Stop();
        };
        Loaded += async (_, _) =>
        {
            Render();
            RefreshProfiles();
            NewProfile_Click(this, null!);
            PopulateOps(string.Empty);
            RefreshKeyList();
            await CheckEngine();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? sender, EventArgs e) { Render(); PopulateOps(OpsFilter.Text ?? ""); RefreshKeyList(); }
    private void OnStoreChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(RefreshProfiles);

    // ================= rendering =================

    private void Render()
    {
        HeaderTitle.Text = "SSH Toolset · SSH 工具";
        HeaderBlurb.Text = P(
            "Saved connection profiles (secrets encrypted at rest with DPAPI), an in-app terminal, key generation, one-click passwordless deploy, and an SFTP browser — all in-process via SSH.NET.",
            "已儲存嘅連線設定檔（秘密用 DPAPI 加密存放）、應用程式內終端機、產生金鑰、一鍵免密碼部署，同 SFTP 瀏覽器 — 全部經 SSH.NET 喺進程內完成。");

        TabProfiles.Header = P("Profiles", "設定檔");
        TabTerminal.Header = P("Terminal", "終端機");
        TabKeys.Header = P("Keys", "金鑰");
        TabSftp.Header = P("SFTP", "SFTP");

        ProfilesHeader.Text = P("Saved profiles", "已儲存設定檔");
        NewProfileBtn.Content = P("New", "新增");
        DeleteProfileBtn.Content = P("Delete", "刪除");
        EditorHeader.Text = _editing is { } e && SshProfileStore.Get(e.Id) is not null
            ? P("Edit profile", "編輯設定檔") : P("New profile", "新設定檔");
        SaveProfileBtn.Content = P("Save", "儲存");
        ConnectBtn.Content = P("Connect", "連線");
        DeployBtn.Content = P("Passwordless deploy", "免密碼部署");
        SecretHint.Text = P(
            "Secrets are encrypted with Windows DPAPI (per-user) before being written to disk. Leave the password/passphrase blank to keep the previously saved one.",
            "秘密喺寫入磁碟前會用 Windows DPAPI（按使用者）加密。留空密碼／密碼短語就會保留上次存嘅。");

        TermConnectBtn.Content = P("Connect", "連線");
        TermDisconnectBtn.Content = P("Disconnect", "斷線");
        OpsHeader.Text = P("Quick remote commands", "快速遠端指令");
        OpsFilter.PlaceholderText = P("Filter commands…", "篩選指令…");

        KeysHeader.Text = P("SSH keys", "SSH 金鑰");
        KeysBlurb.Text = P(
            "Generate a new key pair (ed25519 or RSA) into ~/.ssh, or browse the keys already there. Generation uses the built-in ssh-keygen.exe.",
            "喺 ~/.ssh 產生新金鑰對（ed25519 或 RSA），或瀏覽已有嘅金鑰。產生用內建嘅 ssh-keygen.exe。");
        GenHeader.Text = P("Generate a new key", "產生新金鑰");
        GenerateBtn.Content = P("Generate", "產生");
        OpenSshFolderBtn.Content = P("Open .ssh folder", "開 .ssh 資料夾");
        RefreshKeysBtn.Content = P("Refresh", "重新整理");

        SftpConnectBtn.Content = P("Connect & list", "連線並列出");
        UploadBtn.Content = P("Upload…", "上載…");
        DownloadBtn.Content = P("Download…", "下載…");
        MkdirBtn.Content = P("New folder", "新資料夾");
        DeleteRemoteBtn.Content = P("Delete", "刪除");

        UpdateProfileLabels();
    }

    private void UpdateProfileLabels()
    {
        var disp = _connected?.Display;
        TermProfileLabel.Text = disp is null
            ? P("Pick a profile on the Profiles tab, then Connect.", "喺「設定檔」分頁揀一個，再連線。")
            : P($"Active: {disp}", $"使用中：{disp}");
        SftpProfileLabel.Text = disp is null
            ? P("Pick a profile on the Profiles tab.", "喺「設定檔」分頁揀一個。")
            : P($"Active: {disp}", $"使用中：{disp}");
    }

    private async Task CheckEngine()
    {
        if (SshService.OpenSshClientPresent()) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("OpenSSH client not found", "搵唔到 OpenSSH 客戶端");
        EngineBar.Message = P(
            "ssh-keygen.exe (key generation) is missing. Connect/SFTP still work via SSH.NET, but click to enable the OpenSSH Client optional feature.",
            "搵唔到 ssh-keygen.exe（產生金鑰用）。連線／SFTP 仍可經 SSH.NET 運作，撳一下啟用 OpenSSH 客戶端可選功能。");
        var btn = new Button { Content = P("Enable OpenSSH Client", "啟用 OpenSSH 客戶端") };
        btn.Click += async (_, _) =>
        {
            btn.IsEnabled = false;
            btn.Content = P("Enabling…", "啟用緊…");
            var r = await SshService.EnableOpenSshClientAsync();
            await CheckEngine();
            if (!SshService.OpenSshClientPresent()) { btn.IsEnabled = true; btn.Content = P("Retry", "再試"); }
        };
        EngineBar.ActionButton = btn;
    }

    // ================= profiles =================

    private void RefreshProfiles()
    {
        var sel = (ProfileList.SelectedItem as SshProfile)?.Id;
        ProfileList.ItemsSource = SshProfileStore.All.ToList();
        if (sel is not null)
            ProfileList.SelectedItem = (ProfileList.ItemsSource as IEnumerable<SshProfile>)?.FirstOrDefault(p => p.Id == sel);
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        DeleteProfileBtn.IsEnabled = ProfileList.SelectedItem is SshProfile;
        if (ProfileList.SelectedItem is SshProfile p) LoadIntoEditor(p);
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        ProfileList.SelectedItem = null;
        _editing = new SshProfile { Name = P("New profile", "新設定檔"), Port = 22 };
        NameBox.Text = _editing.Name;
        HostBox.Text = "";
        PortBox.Value = 22;
        UserBox.Text = "";
        AuthCombo.SelectedIndex = 0;
        PasswordBox.Password = "";
        KeyPathBox.Text = "";
        PassphraseBox.Password = "";
        EditorHeader.Text = P("New profile", "新設定檔");
        ProfileBar.IsOpen = false;
    }

    private void LoadIntoEditor(SshProfile p)
    {
        _editing = p;
        NameBox.Text = p.Name;
        HostBox.Text = p.Host;
        PortBox.Value = p.Port;
        UserBox.Text = p.User;
        AuthCombo.SelectedIndex = p.Auth == SshAuthKind.PrivateKey ? 1 : 0;
        PasswordBox.Password = "";
        PassphraseBox.Password = "";
        KeyPathBox.Text = p.KeyPath;
        EditorHeader.Text = P("Edit profile", "編輯設定檔");
        ProfileBar.IsOpen = false;
    }

    private void AuthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool key = AuthCombo.SelectedIndex == 1;
        if (KeyRow is not null) KeyRow.Visibility = key ? Visibility.Visible : Visibility.Collapsed;
        if (PasswordBox is not null) PasswordBox.Visibility = key ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(new[]
        {
            new FileDialogs.Filter("Private keys", "*.pem;*.key;id_*;*"),
            new FileDialogs.Filter("All files", "*.*"),
        }, P("Pick a private key file", "揀一個私鑰檔"));
        if (path is not null) KeyPathBox.Text = path;
    }

    private SshProfile? CollectEditor(out string? secret)
    {
        secret = null;
        if (_editing is null) return null;
        var p = _editing;
        p.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? P("Unnamed", "未命名") : NameBox.Text.Trim();
        p.Host = HostBox.Text.Trim();
        p.Port = (int)(double.IsNaN(PortBox.Value) ? 22 : PortBox.Value);
        p.User = UserBox.Text.Trim();
        p.Auth = AuthCombo.SelectedIndex == 1 ? SshAuthKind.PrivateKey : SshAuthKind.Password;
        p.KeyPath = KeyPathBox.Text.Trim();
        var typed = p.Auth == SshAuthKind.PrivateKey ? PassphraseBox.Password : PasswordBox.Password;
        secret = string.IsNullOrEmpty(typed) ? null : typed; // null = keep existing
        return p;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = CollectEditor(out var secret);
        if (p is null) return;
        if (string.IsNullOrWhiteSpace(p.Host) || string.IsNullOrWhiteSpace(p.User))
        {
            ShowProfileBar(false, P("Host and user are required.", "主機同使用者係必填。"));
            return;
        }
        SshProfileStore.Save(p, secret);
        RefreshProfiles();
        ProfileList.SelectedItem = (ProfileList.ItemsSource as IEnumerable<SshProfile>)?.FirstOrDefault(x => x.Id == p.Id);
        ShowProfileBar(true, P("Profile saved (secret DPAPI-encrypted).", "設定檔已儲存（秘密已用 DPAPI 加密）。"));
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfileList.SelectedItem is SshProfile p)
        {
            SshProfileStore.Remove(p.Id);
            RefreshProfiles();
            NewProfile_Click(this, null!);
        }
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var p = CollectEditor(out var secret);
        if (p is null || string.IsNullOrWhiteSpace(p.Host)) { ShowProfileBar(false, P("Fill in host and user first.", "先填主機同使用者。")); return; }
        SshProfileStore.Save(p, secret);
        _connected = SshProfileStore.Get(p.Id);
        UpdateProfileLabels();
        Tabs.SelectedItem = TabTerminal;
        await StartTerminal();
    }

    private async void Deploy_Click(object sender, RoutedEventArgs e)
    {
        var p = CollectEditor(out var secret);
        if (p is null || string.IsNullOrWhiteSpace(p.Host) || string.IsNullOrWhiteSpace(p.User))
        { ShowProfileBar(false, P("Fill in host and user first.", "先填主機同使用者。")); return; }
        SshProfileStore.Save(p, secret);
        var stored = SshProfileStore.Get(p.Id)!;

        // pick a public key to push
        var keys = SshService.ListKeys();
        if (keys.Count == 0)
        {
            ShowProfileBar(false, P("No SSH keys found in ~/.ssh — generate one on the Keys tab first.", "~/.ssh 冇 SSH 金鑰 — 先喺「金鑰」分頁產生一個。"));
            Tabs.SelectedItem = TabKeys;
            return;
        }

        string pubText;
        if (keys.Count == 1) pubText = keys[0].PublicKeyText;
        else
        {
            var combo = new ComboBox { Width = 360 };
            foreach (var k in keys) combo.Items.Add(new ComboBoxItem { Content = $"{Path.GetFileName(k.PublicKeyPath)} ({k.Type})", Tag = k.PublicKeyText });
            combo.SelectedIndex = 0;
            var dlg = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = P("Choose a public key to deploy", "揀一個要部署嘅公鑰"),
                Content = combo,
                PrimaryButtonText = P("Deploy", "部署"),
                CloseButtonText = P("Cancel", "取消"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            pubText = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? keys[0].PublicKeyText;
        }

        var pw = SshProfileStore.Reveal(stored);
        if (stored.Auth == SshAuthKind.Password && string.IsNullOrEmpty(pw))
        {
            ShowProfileBar(false, P("This profile has no stored password — passwordless deploy needs an initial password session.", "呢個設定檔冇存密碼 — 免密碼部署需要先用密碼登入一次。"));
            return;
        }

        ShowProfileBar(true, P("Deploying public key…", "部署緊公鑰…"));
        DeployBtn.IsEnabled = false;
        try
        {
            var r = await SshService.DeployPublicKeyAsync(stored, pw, pubText);
            ShowProfileBar(r.Success, Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? ""));
        }
        finally { DeployBtn.IsEnabled = true; }
    }

    private void ShowProfileBar(bool ok, string msg)
    {
        ProfileBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ProfileBar.Title = ok ? P("OK", "完成") : P("Problem", "出錯");
        ProfileBar.Message = msg;
        ProfileBar.IsOpen = true;
    }

    // ================= terminal =================

    private async void TermConnect_Click(object sender, RoutedEventArgs e) => await StartTerminal();

    private async Task StartTerminal()
    {
        if (_connected is null)
        {
            if (ProfileList.SelectedItem is SshProfile sel) _connected = sel;
            else { Tabs.SelectedItem = TabProfiles; return; }
        }
        var secret = SshProfileStore.Reveal(_connected);
        TermConnectBtn.IsEnabled = false;
        await Terminal.StartAsync(_connected, secret);
        UpdateTerminalButtons();
    }

    private void TermDisconnect_Click(object sender, RoutedEventArgs e)
    {
        Terminal.Stop();
        UpdateTerminalButtons();
    }

    private void UpdateTerminalButtons()
    {
        bool conn = Terminal.IsConnected;
        TermConnectBtn.IsEnabled = !conn;
        TermDisconnectBtn.IsEnabled = conn;
    }

    // ================= quick ops =================

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput) PopulateOps(sender.Text ?? "");
    }

    private void PopulateOps(string filter)
    {
        OpsPanel.Items.Clear();
        IEnumerable<SshOperations.RemoteOp> ops = SshOperations.All;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            ops = ops.Where(o => o.Haystack.Contains(f));
        }
        foreach (var op in ops)
        {
            var card = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
            };
            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var texts = new StackPanel { Spacing = 1 };
            texts.Children.Add(new TextBlock { Text = $"{op.Title.Primary} · {op.Title.Secondary}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            texts.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            Grid.SetColumn(texts, 0);
            grid.Children.Add(texts);

            var run = new Button { Content = P("Run", "執行"), VerticalAlignment = VerticalAlignment.Center };
            run.Click += async (_, _) => await RunOp(op, run);
            Grid.SetColumn(run, 1);
            grid.Children.Add(run);

            card.Child = grid;
            OpsPanel.Items.Add(card);
        }
    }

    private async Task RunOp(SshOperations.RemoteOp op, Button btn)
    {
        if (_connected is null)
        {
            if (ProfileList.SelectedItem is SshProfile sel) _connected = sel;
            else { Tabs.SelectedItem = TabProfiles; return; }
            UpdateProfileLabels();
        }
        btn.IsEnabled = false;
        var label = btn.Content;
        btn.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };
        try
        {
            var secret = SshProfileStore.Reveal(_connected);
            var r = await SshService.RunCommandAsync(_connected, secret, op.Command);
            await ShowOutputDialog(op.Title.Primary, r);
        }
        finally { btn.Content = label; btn.IsEnabled = true; }
    }

    private async Task ShowOutputDialog(string title, TweakResult r)
    {
        var body = string.IsNullOrWhiteSpace(r.Output)
            ? Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? "")
            : r.Output!;
        var tb = new TextBox
        {
            Text = body,
            IsReadOnly = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"),
            FontSize = 12,
            AcceptsReturn = true,
        };
        var scroller = new ScrollViewer
        {
            Content = tb,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 380,
            MinWidth = 540,
        };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = scroller,
            CloseButtonText = P("Close", "關閉"),
        };
        await dlg.ShowAsync();
    }

    // ================= keys =================

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        string type = KeyTypeCombo.SelectedIndex == 1 ? "rsa" : "ed25519";
        string file = string.IsNullOrWhiteSpace(KeyFileBox.Text) ? (type == "rsa" ? "id_rsa" : "id_ed25519") : KeyFileBox.Text.Trim();
        GenerateBtn.IsEnabled = false;
        try
        {
            var r = await SshService.GenerateKeyAsync(type, file, KeyCommentBox.Text.Trim(), KeyPassphraseBox.Password);
            KeysBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            KeysBar.Title = r.Success ? P("Key generated", "已產生金鑰") : P("Failed", "失敗");
            KeysBar.Message = r.Success
                ? P($"Created ~/.ssh/{file} and {file}.pub.", $"已建立 ~/.ssh/{file} 同 {file}.pub。")
                : Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? "") + (string.IsNullOrWhiteSpace(r.Output) ? "" : $"\n{r.Output}");
            KeysBar.IsOpen = true;
            RefreshKeyList();
        }
        finally { GenerateBtn.IsEnabled = true; }
    }

    private async void OpenSshFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Directory.CreateDirectory(SshService.SshDir); } catch { }
        await ShellRunner.Run("explorer.exe", $"\"{SshService.SshDir}\"");
    }

    private void RefreshKeys_Click(object sender, RoutedEventArgs e) => RefreshKeyList();

    private void RefreshKeyList()
    {
        if (KeyListPanel is null) return;
        KeyListPanel.Children.Clear();
        var keys = SshService.ListKeys();
        if (keys.Count == 0)
        {
            KeyListPanel.Children.Add(new TextBlock
            {
                Text = P("No keys found in ~/.ssh yet.", "~/.ssh 暫時冇金鑰。"),
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            return;
        }
        foreach (var k in keys)
        {
            var border = new Border
            {
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
            };
            var sp = new StackPanel { Spacing = 4 };
            sp.Children.Add(new TextBlock
            {
                Text = $"{Path.GetFileName(k.PublicKeyPath)}  ·  {k.Type}{(string.IsNullOrWhiteSpace(k.Comment) ? "" : "  ·  " + k.Comment)}",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            sp.Children.Add(new TextBox
            {
                Text = k.PublicKeyText,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas"),
                FontSize = 11,
            });
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var copy = new Button { Content = P("Copy public key", "複製公鑰") };
            copy.Click += (_, _) =>
            {
                try
                {
                    var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dp.SetText(k.PublicKeyText);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                }
                catch { }
            };
            row.Children.Add(copy);
            sp.Children.Add(row);
            border.Child = sp;
            KeyListPanel.Children.Add(border);
        }
    }

    // ================= SFTP =================

    /// <summary>UI-friendly row wrapper adding a glyph and formatted date.</summary>
    private sealed class SftpRow
    {
        public SftpEntry Entry { get; init; } = null!;
        public string Name => Entry.Name;
        public string DisplaySize => Entry.DisplaySize;
        public bool IsDirectory => Entry.IsDirectory;
        public string FullPath => Entry.FullPath;
        public string Glyph => Entry.IsDirectory ? "" : "";
        public string ModifiedText => Entry.Modified == default ? "" : Entry.Modified.ToString("yyyy-MM-dd HH:mm");
    }

    private async void SftpConnect_Click(object sender, RoutedEventArgs e)
    {
        if (_connected is null)
        {
            if (ProfileList.SelectedItem is SshProfile sel) { _connected = sel; UpdateProfileLabels(); }
            else { Tabs.SelectedItem = TabProfiles; return; }
        }
        _sftpPath = ".";
        await LoadSftp(_sftpPath);
    }

    private async Task LoadSftp(string path)
    {
        if (_connected is null) return;
        SftpConnectBtn.IsEnabled = false;
        ShowSftpBar(true, P("Listing…", "列出緊…"));
        try
        {
            var secret = SshProfileStore.Reveal(_connected);
            var (ok, err, entries) = await SshService.ListRemoteAsync(_connected, secret, path);
            if (!ok)
            {
                ShowSftpBar(false, err);
                return;
            }
            _sftpPath = path;
            // Resolve "." display path to the working dir's full path if we got entries.
            SftpPathBox.Text = path == "." ? (entries.FirstOrDefault()?.FullPath is { } fp ? SshService.ParentPath(fp) : ".") : path;
            SftpList.ItemsSource = entries.Select(en => new SftpRow { Entry = en }).ToList();
            SftpUpBtn.IsEnabled = true;
            UploadBtn.IsEnabled = true;
            MkdirBtn.IsEnabled = true;
            DownloadBtn.IsEnabled = false;
            DeleteRemoteBtn.IsEnabled = false;
            SftpBar.IsOpen = false;
        }
        finally { SftpConnectBtn.IsEnabled = true; }
    }

    private void SftpList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (SftpList.SelectedItem is SftpRow row && row.IsDirectory)
            _ = LoadSftp(row.FullPath);
    }

    private async void SftpUp_Click(object sender, RoutedEventArgs e)
    {
        var cur = string.IsNullOrWhiteSpace(SftpPathBox.Text) ? _sftpPath : SftpPathBox.Text;
        await LoadSftp(SshService.ParentPath(cur));
    }

    private async void SftpGo_Click(object sender, RoutedEventArgs e)
        => await LoadSftp(string.IsNullOrWhiteSpace(SftpPathBox.Text) ? "." : SftpPathBox.Text.Trim());

    private async void SftpPath_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) await LoadSftp(string.IsNullOrWhiteSpace(SftpPathBox.Text) ? "." : SftpPathBox.Text.Trim());
    }

    private void SftpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var row = SftpList.SelectedItem as SftpRow;
        DownloadBtn.IsEnabled = row is { IsDirectory: false };
        DeleteRemoteBtn.IsEnabled = row is not null;
    }

    private async void Upload_Click(object sender, RoutedEventArgs e)
    {
        if (_connected is null) return;
        var local = await FileDialogs.OpenFileAsync();
        if (local is null) return;
        var dir = SftpPathBox.Text;
        ShowSftpBar(true, P("Uploading…", "上載緊…"));
        var secret = SshProfileStore.Reveal(_connected);
        var r = await SshService.UploadAsync(_connected, secret, local, string.IsNullOrWhiteSpace(dir) ? "." : dir);
        ShowSftpBar(r.Success, Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? ""));
        if (r.Success) await LoadSftp(_sftpPath);
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (_connected is null || SftpList.SelectedItem is not SftpRow row || row.IsDirectory) return;
        var local = await FileDialogs.SaveFileAsync(row.Name);
        if (local is null) return;
        ShowSftpBar(true, P("Downloading…", "下載緊…"));
        var secret = SshProfileStore.Reveal(_connected);
        var r = await SshService.DownloadAsync(_connected, secret, row.FullPath, local);
        ShowSftpBar(r.Success, Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? ""));
    }

    private async void Mkdir_Click(object sender, RoutedEventArgs e)
    {
        if (_connected is null) return;
        var input = new TextBox { PlaceholderText = P("Folder name", "資料夾名") };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("New remote folder", "新遠端資料夾"),
            Content = input,
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrWhiteSpace(input.Text)) return;
        var secret = SshProfileStore.Reveal(_connected);
        var dir = string.IsNullOrWhiteSpace(SftpPathBox.Text) ? "." : SftpPathBox.Text;
        var r = await SshService.MakeDirAsync(_connected, secret, dir, input.Text.Trim());
        ShowSftpBar(r.Success, Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? ""));
        if (r.Success) await LoadSftp(_sftpPath);
    }

    private async void DeleteRemote_Click(object sender, RoutedEventArgs e)
    {
        if (_connected is null || SftpList.SelectedItem is not SftpRow row) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete?", "刪除？"),
            Content = $"{row.Name}\n{P("This cannot be undone.", "唔可以復原。")}",
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var secret = SshProfileStore.Reveal(_connected);
        var r = await SshService.DeleteRemoteAsync(_connected, secret, row.Entry);
        ShowSftpBar(r.Success, Loc.I.Pick(r.Message?.En ?? "", r.Message?.Zh ?? ""));
        if (r.Success) await LoadSftp(_sftpPath);
    }

    private void ShowSftpBar(bool ok, string msg)
    {
        SftpBar.Severity = ok ? InfoBarSeverity.Informational : InfoBarSeverity.Error;
        SftpBar.Title = ok ? "" : P("Error", "錯誤");
        SftpBar.Message = msg;
        SftpBar.IsOpen = true;
    }
}
