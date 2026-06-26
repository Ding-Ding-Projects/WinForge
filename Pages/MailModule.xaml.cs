using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生電郵客戶端（Thunderbird 風格三欄）· Native email client (Thunderbird-style three pane):
/// 帳戶 + 資料夾樹 ｜ 訊息清單 ｜ 閱讀窗。用 MailKit／MimeKit 連 IMAP／SMTP，HTML 內文喺沙箱
/// WebView2 渲染（預設封鎖遠端內容），機密用 DPAPI 加密。撰寫／回覆／轉寄／附件全部喺 app 內。
/// Three-pane client over MailKit/MimeKit; HTML rendered in a sandboxed WebView2 (remote content blocked
/// by default); credentials DPAPI-encrypted; compose / reply / forward / attachments all in-app. Bilingual.
/// </summary>
public sealed partial class MailModule : Page
{
    private List<MailAccount> _accounts = new();
    private MailAccount? _account;
    private string _folder = "INBOX";
    private MailMessageBody? _current;
    private CancellationTokenSource? _cts;
    private bool _allowRemote;
    private int _loaded;
    private const int PageSize = 30;
    private bool _webReady;

    public ObservableCollection<FolderVm> Folders { get; } = new();
    public ObservableCollection<MessageVm> Messages { get; } = new();

    public MailModule()
    {
        InitializeComponent();
        FolderList.ItemsSource = Folders;
        MessageList.ItemsSource = Messages;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await ReloadAccountsAsync(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Mail · 電郵";
        HeaderBlurb.Text = P(
            "A native multi-account email client. Add IMAP/SMTP accounts (auto-detects Gmail/Outlook/iCloud), browse folders, read mail with attachments, and compose / reply / forward — all in-app. Credentials are DPAPI-encrypted.",
            "原生多帳戶電郵客戶端。加 IMAP／SMTP 帳戶（自動偵測 Gmail／Outlook／iCloud），瀏覽資料夾、睇郵件同附件，撰寫／回覆／轉寄全部喺 app 內。憑證以 DPAPI 加密。");
        AddAccountLabel.Text = P("Add account", "加帳戶");
        ComposeLabel.Text = P("Compose", "撰寫");
        SearchBox.PlaceholderText = P("Search this folder…", "搜尋呢個資料夾…");
        FoldersHeader.Text = P("Folders", "資料夾");
        ReplyLabel.Text = P("Reply", "回覆");
        ReplyAllLabel.Text = P("Reply all", "全部回覆");
        ForwardLabel.Text = P("Forward", "轉寄");
        RemoteLabel.Text = P("Show images", "顯示圖片");
        EmptyTitle.Text = P("No mail accounts yet", "未有電郵帳戶");
        EmptyBlurb.Text = P(
            "Add an IMAP + SMTP account to get started. Gmail and Outlook sign in with OAuth2; other providers use an app password. You can also install Thunderbird as a fallback.",
            "加一個 IMAP ＋ SMTP 帳戶就可以開始。Gmail 同 Outlook 用 OAuth2 登入；其他供應商用 App 專用密碼。亦可以安裝 Thunderbird 作後備。");
        EmptyAddBtn.Content = P("Add your first account", "加你第一個帳戶");
        ReaderEmpty.Text = P("Select a message to read it here.", "揀一封訊息喺度睇。");
        MessagesHeader.Text = P("Messages", "訊息");
        LoadMoreBtn.Content = P("Load more", "載入更多");
        if (_account is null) MessagesHeader.Text = P("Messages", "訊息");
    }

    private void Done(bool ok, string en, string zh, string? detail = null)
    {
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = P(en, zh);
        ResultBar.Message = detail ?? "";
        ResultBar.IsOpen = true;
    }

    // ===================== Accounts =====================

    private async Task ReloadAccountsAsync()
    {
        _accounts = MailAccountStore.Load();
        AccountBox.Items.Clear();
        foreach (var a in _accounts)
            AccountBox.Items.Add(new ComboBoxItem { Content = a.Label, Tag = a.Id });

        bool any = _accounts.Count > 0;
        EmptyState.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        ThreePane.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (!any) { _account = null; return; }

        var lastId = SettingsStore.Get("mail.lastAccount", _accounts[0].Id);
        var idx = Math.Max(0, _accounts.FindIndex(a => a.Id == lastId));
        AccountBox.SelectedIndex = idx;
        await Task.CompletedTask;
    }

    private async void Account_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (AccountBox.SelectedItem is not ComboBoxItem item || item.Tag is not string id) return;
        _account = _accounts.FirstOrDefault(a => a.Id == id);
        if (_account is null) return;
        SettingsStore.Set("mail.lastAccount", id);
        await LoadFoldersAsync();
    }

    private async void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MailAccountDialog { XamlRoot = XamlRoot };
        var acc = await dlg.ShowWizardAsync();
        if (acc is null) return;
        MailAccountStore.Upsert(acc);
        await ReloadAccountsAsync();
        // select the new account
        var idx = _accounts.FindIndex(a => a.Id == acc.Id);
        if (idx >= 0) AccountBox.SelectedIndex = idx;
        Done(true, "Account added", "已加帳戶", acc.Label);
    }

    // ===================== Folders =====================

    private async Task LoadFoldersAsync()
    {
        if (_account is null) return;
        Folders.Clear();
        Messages.Clear();
        ClearReader();
        ListProgress.IsActive = true;
        try
        {
            var nodes = await MailService.GetFoldersAsync(_account, Cancel());
            foreach (var n in nodes) Folders.Add(new FolderVm(n));
            if (Folders.Count > 0) FolderList.SelectedIndex = 0;
        }
        catch (Exception ex) { Done(false, "Could not load folders", "載入資料夾失敗", ex.Message); }
        finally { ListProgress.IsActive = false; }
    }

    private async void Folder_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (FolderList.SelectedItem is not FolderVm vm) return;
        _folder = vm.FullName;
        MessagesHeader.Text = $"{vm.Name}";
        await LoadMessagesAsync(reset: true);
    }

    // ===================== Messages =====================

    private async Task LoadMessagesAsync(bool reset, string? search = null)
    {
        if (_account is null) return;
        if (reset) { Messages.Clear(); _loaded = 0; ClearReader(); }
        ListProgress.IsActive = true;
        LoadMoreBtn.Visibility = Visibility.Collapsed;
        try
        {
            var list = await MailService.ListAsync(_account, _folder, _loaded, PageSize, search, Cancel());
            foreach (var m in list) Messages.Add(new MessageVm(m));
            _loaded += list.Count;
            LoadMoreBtn.Visibility = list.Count >= PageSize ? Visibility.Visible : Visibility.Collapsed;
            if (Messages.Count == 0)
                MessagesHeader.Text = P("No messages", "無訊息");
        }
        catch (Exception ex) { Done(false, "Could not load messages", "載入訊息失敗", ex.Message); }
        finally { ListProgress.IsActive = false; }
    }

    private async void LoadMore_Click(object sender, RoutedEventArgs e)
        => await LoadMessagesAsync(reset: false, search: CurrentSearch());

    private string? CurrentSearch() => string.IsNullOrWhiteSpace(SearchBox.Text) ? null : SearchBox.Text.Trim();

    private async void Search_Submitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => await LoadMessagesAsync(reset: true, search: CurrentSearch());

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null) return;
        await LoadFoldersAsync();
    }

    private async void Message_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (MessageList.SelectedItem is not MessageVm vm || _account is null) return;
        _allowRemote = false;
        RemoteToggle.IsChecked = false;
        ReaderEmpty.Visibility = Visibility.Collapsed;
        ListProgress.IsActive = true;
        try
        {
            _current = await MailService.OpenAsync(_account, _folder, vm.Uid, markSeen: true, Cancel());
            if (_current is null) return;
            vm.MarkSeen();
            ShowReader(_current);
        }
        catch (Exception ex) { Done(false, "Could not open message", "開唔到訊息", ex.Message); }
        finally { ListProgress.IsActive = false; }
    }

    private void ShowReader(MailMessageBody b)
    {
        ReaderHeader.Visibility = Visibility.Visible;
        ReaderActions.Visibility = Visibility.Visible;
        ReaderEmpty.Visibility = Visibility.Collapsed;
        ReaderSubject.Text = b.Subject;
        ReaderFrom.Text = P("From: ", "寄件人：") + b.From;
        ReaderTo.Text = P("To: ", "收件人：") + b.To + (string.IsNullOrEmpty(b.Cc) ? "" : "  ·  Cc: " + b.Cc);
        ReaderDate.Text = b.Date == default ? "" : b.Date.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

        // Attachments
        AttachmentsPanel.Items.Clear();
        foreach (var att in b.Attachments)
        {
            var btn = new Button { Margin = new Thickness(0, 0, 6, 0) };
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            sp.Children.Add(new FontIcon { FontSize = 13, Glyph = "" });
            sp.Children.Add(new TextBlock { Text = $"{att.FileName} ({Human(att.Size)})", VerticalAlignment = VerticalAlignment.Center });
            btn.Content = sp;
            var captured = att;
            btn.Click += async (_, _) => await SaveAttachmentAsync(captured);
            AttachmentsPanel.Items.Add(btn);
        }

        RenderBody(b);
    }

    private async void RenderBody(MailMessageBody b)
    {
        if (!string.IsNullOrEmpty(b.HtmlBody))
        {
            TextScroller.Visibility = Visibility.Collapsed;
            BodyWeb.Visibility = Visibility.Visible;
            await EnsureWebAsync();
            var html = b.HtmlBody!;
            if (!_allowRemote) html = SanitizeRemote(html);
            try { BodyWeb.NavigateToString(WrapHtml(html)); } catch { }
        }
        else
        {
            BodyWeb.Visibility = Visibility.Collapsed;
            TextScroller.Visibility = Visibility.Visible;
            BodyText.Text = b.TextBody;
        }
    }

    private async Task EnsureWebAsync()
    {
        if (_webReady) return;
        try
        {
            await BodyWeb.EnsureCoreWebView2Async();
            var s = BodyWeb.CoreWebView2.Settings;
            // Sandbox the HTML view: no scripts, no autoplay, no extra windows, no devtools.
            s.IsScriptEnabled = false;
            s.AreDefaultScriptDialogsEnabled = false;
            s.IsWebMessageEnabled = false;
            s.AreDevToolsEnabled = false;
            s.AreDefaultContextMenusEnabled = false;
            s.IsZoomControlEnabled = true;
            BodyWeb.CoreWebView2.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri) &&
                    (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "mailto"))
                    try { CopyText(e.Uri); Done(true, "Link copied", "已複製連結", e.Uri); } catch { }
            };
            _webReady = true;
        }
        catch (Exception ex) { Done(false, "WebView2 unavailable", "WebView2 唔可用", ex.Message); }
    }

    /// <summary>封鎖遠端內容（圖片／追蹤像素）· Strip remote content (images / tracking pixels) when not allowed.</summary>
    private static string SanitizeRemote(string html)
    {
        // Neutralise remote http(s) src/background so no images/pixels load until the user opts in.
        html = System.Text.RegularExpressions.Regex.Replace(html,
            @"(?i)\s(src|background)\s*=\s*([""'])\s*https?://[^""']*\2", " data-blocked=$2$2");
        return html;
    }

    private static string WrapHtml(string body) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\">" +
        "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'; object-src 'none';\">" +
        "<style>body{font-family:Segoe UI,Arial,sans-serif;margin:12px;color:#1a1a1a;background:#fff;word-wrap:break-word;}img{max-width:100%;height:auto;}</style></head><body>" +
        body + "</body></html>";

    private static void CopyText(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }

    private void RemoteToggle_Click(object sender, RoutedEventArgs e)
    {
        _allowRemote = RemoteToggle.IsChecked == true;
        if (_current is not null) RenderBody(_current);
    }

    private void ClearReader()
    {
        _current = null;
        ReaderHeader.Visibility = Visibility.Collapsed;
        ReaderActions.Visibility = Visibility.Collapsed;
        AttachmentsPanel.Items.Clear();
        BodyWeb.Visibility = Visibility.Collapsed;
        TextScroller.Visibility = Visibility.Collapsed;
        ReaderEmpty.Visibility = Visibility.Visible;
    }

    // ===================== Attachments =====================

    private async Task SaveAttachmentAsync(MailAttachment att)
    {
        var path = await FileDialogs.SaveFileAsync(att.FileName);
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            await MailService.SaveAttachmentAsync(att, path, Cancel());
            Done(true, "Attachment saved", "已儲存附件", path);
        }
        catch (Exception ex) { Done(false, "Save failed", "儲存失敗", ex.Message); }
    }

    private static string Human(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }

    // ===================== Flags / delete =====================

    private async void ReadToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || _account is null) return;
        if (MessageList.SelectedItem is not MessageVm vm) return;
        try
        {
            await MailService.SetSeenAsync(_account, _folder, _current.Uid, !vm.Seen, Cancel());
            vm.ToggleSeen();
            Done(true, "Updated", "已更新");
        }
        catch (Exception ex) { Done(false, "Failed", "失敗", ex.Message); }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null || _account is null) return;
        try
        {
            await MailService.DeleteAsync(_account, _folder, _current.Uid, Cancel());
            if (MessageList.SelectedItem is MessageVm vm) Messages.Remove(vm);
            ClearReader();
            Done(true, "Moved to Trash", "已移去垃圾桶");
        }
        catch (Exception ex) { Done(false, "Delete failed", "刪除失敗", ex.Message); }
    }

    // ===================== Compose / reply / forward =====================

    private async void Compose_Click(object sender, RoutedEventArgs e) => await ComposeAsync(ComposeMode.New);
    private async void Reply_Click(object sender, RoutedEventArgs e) => await ComposeAsync(ComposeMode.Reply);
    private async void ReplyAll_Click(object sender, RoutedEventArgs e) => await ComposeAsync(ComposeMode.ReplyAll);
    private async void Forward_Click(object sender, RoutedEventArgs e) => await ComposeAsync(ComposeMode.Forward);

    private async Task ComposeAsync(ComposeMode mode)
    {
        if (_account is null) { Done(false, "Add an account first", "請先加帳戶"); return; }
        var src = mode == ComposeMode.New ? null : _current;
        var draft = MailComposer.Prefill(mode, _account, src);
        var dlg = new MailComposeDialog(_account, draft, mode == ComposeMode.New ? null : src) { XamlRoot = XamlRoot };
        var sent = await dlg.ShowComposeAsync();
        if (sent) Done(true, "Message sent", "已寄出");
    }

    // ===================== Thunderbird fallback =====================

    private async void Thunderbird_Click(object sender, RoutedEventArgs e)
    {
        var exe = ResolveThunderbird();
        if (exe is not null)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true }); Done(true, "Launched Thunderbird", "已啟動 Thunderbird"); }
            catch (Exception ex) { Done(false, "Launch failed", "啟動失敗", ex.Message); }
            return;
        }
        var ok = await PackageService.AutoInstall("Mozilla.Thunderbird");
        Done(ok, ok ? "Thunderbird installed" : "Install failed",
                 ok ? "已安裝 Thunderbird" : "安裝失敗");
    }

    private static string? ResolveThunderbird()
    {
        foreach (var p in new[]
        {
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles%\Mozilla Thunderbird\thunderbird.exe"),
            Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\Mozilla Thunderbird\thunderbird.exe"),
        })
            if (System.IO.File.Exists(p)) return p;
        return null;
    }

    // ===================== helpers =====================

    private CancellationToken Cancel()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }
}

// ===================== View models =====================

/// <summary>資料夾列項目 · A folder row in the tree.</summary>
public sealed class FolderVm
{
    public string FullName { get; }
    public string Name { get; }
    public int Unread { get; }
    public string UnreadText => Unread > 0 ? Unread.ToString() : "";
    public FolderVm(MailFolderNode n) { FullName = n.FullName; Name = n.Name; Unread = n.Unread; }
}

/// <summary>訊息列項目（可改已讀狀態）· A message row; supports flipping the read state in place.</summary>
public sealed class MessageVm : System.ComponentModel.INotifyPropertyChanged
{
    public uint Uid { get; }
    public string From { get; }
    public string Subject { get; }
    public string DateText { get; }
    public bool HasAttachments { get; }
    private bool _seen;

    public MessageVm(MailMessageSummary s)
    {
        Uid = s.Uid; From = Pretty(s.From); Subject = s.Subject; DateText = s.DateText;
        HasAttachments = s.HasAttachments; _seen = s.Seen;
    }

    public bool Seen => _seen;
    public string Weight => _seen ? "Normal" : "SemiBold";
    public Visibility UnreadDot => _seen ? Visibility.Collapsed : Visibility.Visible;
    public Visibility AttachVis => HasAttachments ? Visibility.Visible : Visibility.Collapsed;

    public void MarkSeen() { if (!_seen) { _seen = true; Notify(); } }
    public void ToggleSeen() { _seen = !_seen; Notify(); }

    private void Notify()
    {
        foreach (var p in new[] { nameof(Seen), nameof(Weight), nameof(UnreadDot) })
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(p));
    }

    private static string Pretty(string addr)
    {
        // "Name <a@b>" -> "Name"; bare address stays.
        var lt = addr.IndexOf('<');
        if (lt > 0) return addr[..lt].Trim().Trim('"');
        return addr;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
