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
/// 一個獨立嘅 Bitwarden 連線（一個分頁）· One independent Bitwarden connection (one tab).
///
/// 每個實例【自己擁有】一個 <see cref="BitwardenService"/> 同自己嘅全部 UI 狀態（狀態、搜尋字、選取項、
/// 解鎖中嘅密碼）。【冇】static／共用 session 或 selection —— 鎖定 A 分頁唔會影響 B 分頁，即使佢哋指向同一部
/// 伺服器（同 handoff 54 §3 嘅每實例隔離一致）。所有 UI 都喺 C# 建立，唔靠任何共用 XAML 名字。
///
/// Each instance OWNS its own <see cref="BitwardenService"/> and all of its UI state (status, search text,
/// selected item, the in-memory revealed password). NO static/shared session or selection — locking tab A
/// never affects tab B even if both point at the same server (per-instance isolation, handoff 54 §3). The UI
/// is built entirely in code so no shared XAML names can leak state between tabs.
/// 機密只留喺記憶體；剪貼簿自動清除。 Secrets stay in memory only; clipboard auto-clears.
/// </summary>
public sealed class BitwardenConnectionView : UserControl
{
    // ── per-instance state (NEVER static) ──────────────────────────────────────────
    private readonly BitwardenService _svc = new();
    private List<BitwardenService.VaultItem> _items = new();
    private BitwardenService.VaultItem? _selected;
    private string? _revealedPassword;
    private bool _passwordRevealed;

    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _busyCts;
    private readonly DispatcherTimer _clipTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private string? _lastCopied;
    private readonly DispatcherTimer _totpTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _totpItemId;

    private bool _disposed;

    /// <summary>畀分頁標題用嘅顯示名 · Display name for the tab header.</summary>
    public string DisplayName { get; }

    /// <summary>分頁標題變更時通知（例如登入後顯示電郵）· Raised when the tab title should refresh.</summary>
    public event Action<BitwardenConnectionView>? TitleChanged;

    // ── controls (instance fields) ──────────────────────────────────────────────────
    private InfoBar _statusBar = null!, _toastBar = null!;
    private Border _authPanel = null!;
    private TextBlock _authTitle = null!, _serverHint = null!;
    private TextBox _serverBox = null!, _emailBox = null!, _twoFaCode = null!;
    private PasswordBox _masterBox = null!;
    private CheckBox _twoFaToggle = null!;
    private ComboBox _twoFaMethod = null!;
    private StackPanel _twoFaPanel = null!;
    private Button _authButton = null!;
    private ProgressRing _authBusy = null!;

    private StackPanel _vaultPanel = null!;
    private AutoSuggestBox _searchBox = null!;
    private ListView _itemsList = null!;
    private TextBlock _emptyHint = null!;
    private ProgressRing _listBusy = null!;

    private TextBlock _detailPlaceholder = null!, _detailName = null!, _detailType = null!;
    private StackPanel _detailContent = null!;
    private StackPanel _userRow = null!, _passRow = null!, _totpRow = null!, _uriRow = null!, _cardRow = null!, _notesRow = null!;
    private TextBlock _userValue = null!, _passValue = null!, _totpValue = null!, _totpSeconds = null!, _uriValue = null!, _cardValue = null!, _notesValue = null!;
    private FontIcon _revealPassIcon = null!;
    private ProgressRing _totpCountdown = null!;

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private bool Zh => Loc.I.IsCantonesePrimary;

    /// <param name="seedServerUrl">若由「連去呢部伺服器」帶入嘅基底網址 · Optional base URL seeded from a self-hosted instance.</param>
    public BitwardenConnectionView(string displayName, string? seedServerUrl = null)
    {
        DisplayName = displayName;
        BuildUi();
        _clipTimer.Tick += (_, _) => ClearClipboardIfOurs();
        _totpTimer.Tick += (_, _) => SafeTotpTick();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Dispose();

        if (!string.IsNullOrWhiteSpace(seedServerUrl))
            _serverBox.Text = seedServerUrl!.Trim();

        Render();
        Refresh();
    }

    // ===================== UI construction =====================

    private void BuildUi()
    {
        var root = new StackPanel { Spacing = 14, Padding = new Thickness(4, 8, 4, 8) };

        _statusBar = new InfoBar { IsClosable = false, IsOpen = true, Severity = InfoBarSeverity.Informational };
        _toastBar = new InfoBar { IsClosable = true, IsOpen = false, Severity = InfoBarSeverity.Success };
        root.Children.Add(_statusBar);
        root.Children.Add(_toastBar);

        // ── Auth panel ──
        _authTitle = new TextBlock { Style = TryStyle("SubtitleTextBlockStyle") };
        _serverBox = new TextBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        _serverHint = new TextBlock { FontSize = 12, MaxWidth = 460, HorizontalAlignment = HorizontalAlignment.Left, TextWrapping = TextWrapping.Wrap };
        if (TryBrush("TextFillColorSecondaryBrush") is { } sh) _serverHint.Foreground = sh;
        _emailBox = new TextBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        _masterBox = new PasswordBox { MaxWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        _twoFaToggle = new CheckBox { Visibility = Visibility.Collapsed };
        _twoFaToggle.Click += (_, _) => Safe(() => _twoFaPanel.Visibility = _twoFaToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed);
        _twoFaMethod = new ComboBox { Width = 220 };
        _twoFaCode = new TextBox { Width = 180 };
        _twoFaPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Visibility = Visibility.Collapsed };
        _twoFaPanel.Children.Add(_twoFaMethod);
        _twoFaPanel.Children.Add(_twoFaCode);
        _authButton = new Button { Style = TryStyle("AccentButtonStyle") };
        _authButton.Click += (_, _) => _ = AuthClick();
        _authBusy = new ProgressRing { Width = 22, Height = 22, IsActive = false, VerticalAlignment = VerticalAlignment.Center };
        var authBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        authBtnRow.Children.Add(_authButton);
        authBtnRow.Children.Add(_authBusy);

        var authInner = new StackPanel { Spacing = 10 };
        authInner.Children.Add(_authTitle);
        authInner.Children.Add(_serverBox);
        authInner.Children.Add(_serverHint);
        authInner.Children.Add(_emailBox);
        authInner.Children.Add(_masterBox);
        authInner.Children.Add(_twoFaToggle);
        authInner.Children.Add(_twoFaPanel);
        authInner.Children.Add(authBtnRow);

        _authPanel = new Border
        {
            Padding = new Thickness(16),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Visibility = Visibility.Collapsed,
            Child = authInner,
        };
        if (TryBrush("CardBackgroundFillColorDefaultBrush") is { } cb) _authPanel.Background = cb;
        if (TryBrush("CardStrokeColorDefaultBrush") is { } cs) _authPanel.BorderBrush = cs;
        root.Children.Add(_authPanel);

        // ── Vault panel ──
        _vaultPanel = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };

        _searchBox = new AutoSuggestBox { Width = 360, QueryIcon = new SymbolIcon(Symbol.Find) };
        _searchBox.TextChanged += SearchTextChanged;
        var syncBtn = MakeIconButton("", P("Sync", "同步"), () => _ = SyncClick());
        var genBtn = MakeIconButton("", P("Generate", "產生密碼"), () => _ = GenClick());
        var lockBtn = MakeIconButton("", P("Lock", "鎖定"), LockClick);
        var logoutBtn = new Button { Content = P("Log out", "登出") };
        logoutBtn.Click += (_, _) => _ = LogoutClick();
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        toolbar.Children.Add(_searchBox);
        toolbar.Children.Add(syncBtn);
        toolbar.Children.Add(genBtn);
        toolbar.Children.Add(lockBtn);
        toolbar.Children.Add(logoutBtn);
        _vaultPanel.Children.Add(toolbar);

        var grid = new Grid { ColumnSpacing = 16 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _itemsList = new ListView { Padding = new Thickness(4) };
        _itemsList.SelectionChanged += ItemsSelectionChanged;
        _emptyHint = new TextBlock { Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        if (TryBrush("TextFillColorSecondaryBrush") is { } eh) _emptyHint.Foreground = eh;
        _listBusy = new ProgressRing { IsActive = false, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 12, 0, 0) };
        var listGrid = new Grid();
        listGrid.Children.Add(_itemsList);
        listGrid.Children.Add(_emptyHint);
        listGrid.Children.Add(_listBusy);
        var listBorder = new Border { MinHeight = 360, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = listGrid };
        if (TryBrush("LayerFillColorDefaultBrush") is { } lb) listBorder.Background = lb;
        if (TryBrush("CardStrokeColorDefaultBrush") is { } ls) listBorder.BorderBrush = ls;
        Grid.SetColumn(listBorder, 0);
        grid.Children.Add(listBorder);

        var detailInner = new StackPanel { Spacing = 10 };
        _detailPlaceholder = new TextBlock { TextWrapping = TextWrapping.Wrap };
        if (TryBrush("TextFillColorSecondaryBrush") is { } dp) _detailPlaceholder.Foreground = dp;
        detailInner.Children.Add(_detailPlaceholder);

        _detailContent = new StackPanel { Spacing = 12, Visibility = Visibility.Collapsed };
        _detailName = new TextBlock { Style = TryStyle("SubtitleTextBlockStyle"), TextWrapping = TextWrapping.Wrap };
        _detailType = new TextBlock { FontSize = 12 };
        if (TryBrush("TextFillColorSecondaryBrush") is { } dt) _detailType.Foreground = dt;
        _detailContent.Children.Add(_detailName);
        _detailContent.Children.Add(_detailType);

        (_userRow, _userValue) = BuildCopyRow(out var copyUserBtn, P("Username", "用戶名"), "");
        copyUserBtn.Click += (_, _) => CopyUser();
        _detailContent.Children.Add(_userRow);

        // Password row (reveal + copy)
        _passRow = new StackPanel { Spacing = 4 };
        var passLabel = MakeFieldLabel(P("Password", "密碼"));
        _passValue = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Consolas"), IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        var revealBtn = new Button(); _revealPassIcon = new FontIcon { FontSize = 14, Glyph = "" }; revealBtn.Content = _revealPassIcon;
        revealBtn.Click += (_, _) => RevealPass();
        var copyPassBtn = new Button { Content = new FontIcon { FontSize = 14, Glyph = "" } };
        copyPassBtn.Click += (_, _) => CopyPass();
        var passInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        passInner.Children.Add(_passValue); passInner.Children.Add(revealBtn); passInner.Children.Add(copyPassBtn);
        _passRow.Children.Add(passLabel); _passRow.Children.Add(passInner);
        _detailContent.Children.Add(_passRow);

        // TOTP row
        _totpRow = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        var totpLabel = MakeFieldLabel(P("Verification code (TOTP)", "驗證碼（TOTP）"));
        _totpValue = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontFamily = new FontFamily("Consolas"), FontSize = 18 };
        _totpCountdown = new ProgressRing { Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Center, IsIndeterminate = false, Maximum = 30, Value = 30 };
        _totpSeconds = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
        if (TryBrush("TextFillColorSecondaryBrush") is { } ts) _totpSeconds.Foreground = ts;
        var copyTotpBtn = new Button { Content = new FontIcon { FontSize = 14, Glyph = "" } };
        copyTotpBtn.Click += (_, _) => CopyTotp();
        var totpInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        totpInner.Children.Add(_totpValue); totpInner.Children.Add(_totpCountdown); totpInner.Children.Add(_totpSeconds); totpInner.Children.Add(copyTotpBtn);
        _totpRow.Children.Add(totpLabel); _totpRow.Children.Add(totpInner);
        _detailContent.Children.Add(_totpRow);

        // URI row
        _uriRow = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        var uriLabel = MakeFieldLabel(P("Website", "網址"));
        _uriValue = new TextBlock { VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        var openUriBtn = new Button { Content = new FontIcon { FontSize = 14, Glyph = "" } };
        openUriBtn.Click += (_, _) => OpenUri();
        var uriInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        uriInner.Children.Add(_uriValue); uriInner.Children.Add(openUriBtn);
        _uriRow.Children.Add(uriLabel); _uriRow.Children.Add(uriInner);
        _detailContent.Children.Add(_uriRow);

        // Card row
        _cardRow = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        _cardValue = new TextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        _cardRow.Children.Add(MakeFieldLabel(P("Card", "信用卡"))); _cardRow.Children.Add(_cardValue);
        _detailContent.Children.Add(_cardRow);

        // Notes row
        _notesRow = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        _notesValue = new TextBlock { IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        _notesRow.Children.Add(MakeFieldLabel(P("Notes", "備註"))); _notesRow.Children.Add(_notesValue);
        _detailContent.Children.Add(_notesRow);

        detailInner.Children.Add(_detailContent);
        var detailBorder = new Border { Padding = new Thickness(16), VerticalAlignment = VerticalAlignment.Top, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = detailInner };
        if (TryBrush("CardBackgroundFillColorDefaultBrush") is { } db) detailBorder.Background = db;
        if (TryBrush("CardStrokeColorDefaultBrush") is { } ds) detailBorder.BorderBrush = ds;
        Grid.SetColumn(detailBorder, 1);
        grid.Children.Add(detailBorder);

        _vaultPanel.Children.Add(grid);
        root.Children.Add(_vaultPanel);

        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8, 4, 8, 8) };
    }

    private static TextBlock MakeFieldLabel(string text)
    {
        var tb = new TextBlock { Text = text, FontSize = 12 };
        if (TryBrush("TextFillColorSecondaryBrush") is { } b) tb.Foreground = b;
        return tb;
    }

    private (StackPanel row, TextBlock value) BuildCopyRow(out Button copyBtn, string label, string copyGlyph)
    {
        var row = new StackPanel { Spacing = 4 };
        var value = new TextBlock { VerticalAlignment = VerticalAlignment.Center, IsTextSelectionEnabled = true, TextWrapping = TextWrapping.Wrap };
        copyBtn = new Button { Content = new FontIcon { FontSize = 14, Glyph = copyGlyph } };
        var inner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        inner.Children.Add(value); inner.Children.Add(copyBtn);
        row.Children.Add(MakeFieldLabel(label));
        row.Children.Add(inner);
        return (row, value);
    }

    private Button MakeIconButton(string glyph, string label, Action onClick)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        sp.Children.Add(new FontIcon { FontSize = 14, Glyph = glyph });
        sp.Children.Add(new TextBlock { Text = label });
        var b = new Button { Content = sp };
        b.Click += (_, _) => onClick();
        return b;
    }

    // ===================== Render / status =====================

    private void OnLang(object? sender, EventArgs e) => Safe(() => { Render(); Refresh(); });

    private void Render()
    {
        _serverBox.Header = P("Server (base URL)", "伺服器（基底網址）");
        _serverBox.PlaceholderText = "https://vault.bitwarden.com";
        _serverHint.Text = P(
            "Leave the default for the official cloud, or enter your self-hosted Bitwarden / Vaultwarden URL.",
            "用官方雲端就保留預設，或者輸入你自寄存嘅 Bitwarden／Vaultwarden 網址。");
        _emailBox.Header = P("Email", "電郵");
        _emailBox.PlaceholderText = P("Email", "電郵");
        _masterBox.Header = P("Master password", "主密碼");
        _masterBox.PlaceholderText = P("Master password", "主密碼");
        _twoFaToggle.Content = P("I need to enter a two-step (2FA) code", "我需要輸入兩步（2FA）驗證碼");
        _twoFaCode.PlaceholderText = P("2FA code", "2FA 驗證碼");
        if (_twoFaMethod.Items.Count == 0)
        {
            _twoFaMethod.Items.Add(P("Authenticator app (TOTP)", "驗證器 App（TOTP）"));
            _twoFaMethod.Items.Add(P("Email code", "電郵驗證碼"));
            _twoFaMethod.SelectedIndex = 0;
        }
        _searchBox.PlaceholderText = P("Search vault…", "搜尋保險庫…");
        _detailPlaceholder.Text = P("Select an item to view its details.", "揀一個項目睇詳情。");
    }

    private void Refresh()
    {
        Safe(() =>
        {
            var status = _svc.GetStatus();
            UpdateStatusBanner(status);
            switch (status.Status)
            {
                case BitwardenService.VaultStatus.Unauthenticated:
                    ShowAuth(login: true);
                    _vaultPanel.Visibility = Visibility.Collapsed;
                    break;
                case BitwardenService.VaultStatus.Locked:
                    ShowAuth(login: false);
                    _vaultPanel.Visibility = Visibility.Collapsed;
                    break;
                case BitwardenService.VaultStatus.Unlocked:
                    _authPanel.Visibility = Visibility.Collapsed;
                    _vaultPanel.Visibility = Visibility.Visible;
                    LoadItems(_searchBox.Text);
                    break;
            }
        });
        TitleChanged?.Invoke(this);
    }

    private void UpdateStatusBanner(BitwardenService.StatusInfo status)
    {
        switch (status.Status)
        {
            case BitwardenService.VaultStatus.Unauthenticated:
                _statusBar.Severity = InfoBarSeverity.Informational;
                _statusBar.Title = P("Not logged in", "未登入");
                _statusBar.Message = P("Log in with your Bitwarden email and master password.", "用你嘅 Bitwarden 電郵同主密碼登入。");
                break;
            case BitwardenService.VaultStatus.Locked:
                _statusBar.Severity = InfoBarSeverity.Warning;
                _statusBar.Title = P("Locked", "已鎖定");
                _statusBar.Message = (status.UserEmail is { Length: > 0 } e ? P($"Signed in as {e}. ", $"已登入：{e}。") : "")
                    + P("Enter your master password to unlock.", "輸入主密碼解鎖。");
                break;
            case BitwardenService.VaultStatus.Unlocked:
                _statusBar.Severity = InfoBarSeverity.Success;
                _statusBar.Title = P("Unlocked", "已解鎖");
                var who = status.UserEmail is { Length: > 0 } u ? u : P("your account", "你嘅帳戶");
                var sync = status.LastSync is { } ls ? P($" · last sync {ls.LocalDateTime:g}", $" · 上次同步 {ls.LocalDateTime:g}") : "";
                _statusBar.Message = P($"Vault unlocked for {who}.{sync}", $"{who} 嘅保險庫已解鎖。{sync}");
                break;
        }
    }

    /// <summary>分頁標題（已登入顯示電郵）· The current tab header text.</summary>
    public string TabTitle()
    {
        var st = _svc.GetStatus();
        return st.UserEmail is { Length: > 0 } e ? e : DisplayName;
    }

    private void ShowAuth(bool login)
    {
        _authPanel.Visibility = Visibility.Visible;
        _serverBox.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        _serverHint.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        _emailBox.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        _twoFaToggle.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        if (!login) { _twoFaPanel.Visibility = Visibility.Collapsed; _twoFaToggle.IsChecked = false; }
        _authTitle.Text = login ? P("Log in", "登入") : P("Unlock vault", "解鎖保險庫");
        _authButton.Content = login ? P("Log in", "登入") : P("Unlock", "解鎖");
        _masterBox.Password = "";
        if (login && string.IsNullOrWhiteSpace(_serverBox.Text))
            _serverBox.Text = BitwardenService.DefaultBase;
    }

    // ===================== Auth =====================

    private async Task AuthClick()
    {
        try
        {
            bool login = _emailBox.Visibility == Visibility.Visible;
            var master = _masterBox.Password;
            if (string.IsNullOrEmpty(master)) { Toast(InfoBarSeverity.Error, P("Enter your master password.", "請輸入主密碼。")); return; }

            // Warn on remote self-hosted over plain HTTP.
            if (login)
            {
                var srv = _serverBox.Text.Trim();
                if (srv.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !IsLocalHttp(srv))
                {
                    if (!await ConfirmAsync(
                        P("Insecure connection", "唔安全嘅連線"),
                        P("This server uses plain HTTP and is not on localhost. Your data could be intercepted. Continue anyway?",
                          "呢部伺服器用明文 HTTP 而且唔係 localhost，你嘅資料可能被攔截。仍然繼續？")))
                        return;
                }
            }

            _authBusy.IsActive = true;
            _authButton.IsEnabled = false;
            _busyCts = new CancellationTokenSource();
            var ct = _busyCts.Token;
            try
            {
                if (login)
                {
                    int? method = null; string? code = null;
                    if (_twoFaToggle.IsChecked == true && !string.IsNullOrWhiteSpace(_twoFaCode.Text))
                    {
                        method = _twoFaMethod.SelectedIndex switch { 1 => 1, _ => 0 };
                        code = _twoFaCode.Text.Trim();
                    }
                    var r = await _svc.LoginAsync(_emailBox.Text.Trim(), master, _serverBox.Text.Trim(), code, method, ct);
                    _masterBox.Password = "";
                    if (r.TwoFactorRequired)
                    {
                        _twoFaToggle.IsChecked = true;
                        _twoFaPanel.Visibility = Visibility.Visible;
                        Toast(InfoBarSeverity.Warning, Zh ? r.Message?.Zh : r.Message?.En);
                        return;
                    }
                    if (r.Success) { Toast(InfoBarSeverity.Success, Zh ? r.Message?.Zh : r.Message?.En); await SyncSilent(ct); Refresh(); }
                    else Toast(InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
                }
                else
                {
                    var r = await _svc.UnlockAsync(master, ct);
                    _masterBox.Password = "";
                    if (r.Success) { Toast(InfoBarSeverity.Success, Zh ? r.Message?.Zh : r.Message?.En); Refresh(); }
                    else Toast(InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
                }
            }
            finally { _authBusy.IsActive = false; _authButton.IsEnabled = true; }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView.AuthClick", ex); Toast(InfoBarSeverity.Error, ex.Message); }
    }

    private async Task SyncSilent(CancellationToken ct)
    {
        try { await _svc.SyncAsync(ct); } catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView.SyncSilent", ex); }
    }

    private static bool IsLocalHttp(string url) =>
        url.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("127.0.0.1") || url.Contains("[::1]");

    // ===================== Items =====================

    private void SearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedSearch(sender.Text, token);
    }

    private async Task DebouncedSearch(string text, CancellationToken token)
    {
        try { await Task.Delay(220, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        Safe(() => LoadItems(text));
    }

    private void LoadItems(string? search)
    {
        _listBusy.IsActive = true;
        try
        {
            _items = _svc.ListItems(search);
            _itemsList.Items.Clear();
            var noFolder = P("No Folder", "無資料夾");
            var groups = _items
                .GroupBy(i => _svc.FolderName(i.FolderId, Zh))
                .OrderBy(g => g.Key == noFolder ? "￿" : g.Key, StringComparer.CurrentCultureIgnoreCase);
            foreach (var g in groups)
            {
                _itemsList.Items.Add(BuildGroupHeader(g.Key));
                foreach (var it in g.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                    _itemsList.Items.Add(BuildItemRow(it));
            }
            _emptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _emptyHint.Text = string.IsNullOrWhiteSpace(search)
                ? P("No items in this vault. Try Sync.", "呢個保險庫無項目。試吓同步。")
                : P("No items match your search.", "無符合搜尋嘅項目。");
        }
        finally { _listBusy.IsActive = false; }
    }

    private ListViewItem BuildGroupHeader(string name)
    {
        var tb = new TextBlock { Text = name, FontSize = 11, Margin = new Thickness(6, 8, 6, 2) };
        if (TryBrush("TextFillColorSecondaryBrush") is { } sec) tb.Foreground = sec;
        return new ListViewItem { Content = tb, IsHitTestVisible = false, Tag = null };
    }

    private ListViewItem BuildItemRow(BitwardenService.VaultItem it)
    {
        var grid = new Grid { Padding = new Thickness(6, 8, 6, 8), ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var icon = new FontIcon { FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Glyph = GlyphFor(it) };
        Grid.SetColumn(icon, 0); grid.Children.Add(icon);
        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = it.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        var sub = it.Type == 1 && !string.IsNullOrWhiteSpace(it.Username) ? it.Username! : it.TypeLabel(Zh);
        var subBlock = new TextBlock { Text = sub, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        if (TryBrush("TextFillColorSecondaryBrush") is { } sec) subBlock.Foreground = sec;
        info.Children.Add(subBlock);
        Grid.SetColumn(info, 1); grid.Children.Add(info);
        if (it.HasTotp)
        {
            var totp = new FontIcon { FontSize = 13, Glyph = "" };
            if (TryBrush("AccentTextFillColorPrimaryBrush") is { } accent) totp.Foreground = accent;
            Grid.SetColumn(totp, 2); grid.Children.Add(totp);
        }
        return new ListViewItem { Content = grid, Tag = it.Id };
    }

    private static string GlyphFor(BitwardenService.VaultItem it) => it.Type switch
    {
        1 => "", 2 => "", 3 => "", 4 => "", _ => "",
    };

    private void ItemsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_itemsList.SelectedItem is not ListViewItem lvi || lvi.Tag is not string id) { ShowDetailPlaceholder(); return; }
        Safe(() => SelectItem(id));
    }

    // ===================== Detail =====================

    private void ShowDetailPlaceholder()
    {
        _selected = null;
        StopTotp();
        _detailContent.Visibility = Visibility.Collapsed;
        _detailPlaceholder.Visibility = Visibility.Visible;
    }

    private void SelectItem(string id)
    {
        StopTotp();
        var item = _svc.GetItem(id);
        if (item is null) { ShowDetailPlaceholder(); return; }
        _selected = item;
        _revealedPassword = item.Login?.Password;
        _passwordRevealed = false;
        _detailPlaceholder.Visibility = Visibility.Collapsed;
        _detailContent.Visibility = Visibility.Visible;
        _detailName.Text = item.Name;
        _detailType.Text = item.TypeLabel(Zh);

        bool hasUser = !string.IsNullOrWhiteSpace(item.Username);
        _userRow.Visibility = hasUser ? Visibility.Visible : Visibility.Collapsed;
        _userValue.Text = item.Username ?? "";

        bool hasPass = !string.IsNullOrEmpty(item.Login?.Password);
        _passRow.Visibility = hasPass ? Visibility.Visible : Visibility.Collapsed;
        _passValue.Text = hasPass ? "••••••••••" : "";
        _revealPassIcon.Glyph = "";

        if (item.HasTotp) { _totpRow.Visibility = Visibility.Visible; _totpItemId = id; RefreshTotp(); StartTotp(); }
        else { _totpRow.Visibility = Visibility.Collapsed; _totpItemId = null; }

        bool hasUri = !string.IsNullOrWhiteSpace(item.PrimaryUri);
        _uriRow.Visibility = hasUri ? Visibility.Visible : Visibility.Collapsed;
        _uriValue.Text = item.PrimaryUri ?? "";

        bool hasCard = !string.IsNullOrWhiteSpace(item.CardSummary);
        _cardRow.Visibility = hasCard ? Visibility.Visible : Visibility.Collapsed;
        _cardValue.Text = item.CardSummary ?? "";

        bool hasNotes = !string.IsNullOrWhiteSpace(item.Notes);
        _notesRow.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        _notesValue.Text = item.Notes ?? "";
    }

    private void RevealPass()
    {
        if (_selected is null) return;
        _passwordRevealed = !_passwordRevealed;
        _passValue.Text = _passwordRevealed ? (_revealedPassword ?? "") : "••••••••••";
        _revealPassIcon.Glyph = _passwordRevealed ? "" : "";
    }

    private void OpenUri()
    {
        var uri = _selected?.PrimaryUri;
        if (string.IsNullOrWhiteSpace(uri)) return;
        if (!uri.Contains("://")) uri = "https://" + uri;
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                CopySecret(u.ToString(), persistent: true, P("URL copied.", "已複製網址。"));
            else
                Toast(InfoBarSeverity.Error, P("That URL is not a valid web address.", "嗰個唔係有效嘅網址。"));
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    // ===================== Copy =====================

    private void CopyUser()
    {
        if (_selected?.Username is not { Length: > 0 } u) return;
        CopySecret(u, persistent: true, P("Username copied.", "已複製用戶名。"));
    }

    private void CopyPass()
    {
        var pw = _revealedPassword;
        if (string.IsNullOrEmpty(pw)) { Toast(InfoBarSeverity.Error, P("No password to copy.", "無密碼可複製。")); return; }
        CopySecret(pw, persistent: false, P("Password copied — clipboard clears in 20s.", "已複製密碼 — 20 秒後自動清除剪貼簿。"));
    }

    private void CopyTotp()
    {
        var code = _totpValue.Text;
        if (string.IsNullOrWhiteSpace(code) || code.Contains('—')) return;
        CopySecret(code, persistent: false, P("TOTP copied — clipboard clears in 20s.", "已複製 TOTP — 20 秒後自動清除剪貼簿。"));
    }

    private void CopySecret(string value, bool persistent, string toast)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(value);
            Clipboard.SetContent(dp);
            if (!persistent) { _lastCopied = value; _clipTimer.Stop(); _clipTimer.Start(); }
            Toast(InfoBarSeverity.Success, toast);
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    private async void ClearClipboardIfOurs()
    {
        _clipTimer.Stop();
        try
        {
            var view = Clipboard.GetContent();
            if (view != null && view.Contains(StandardDataFormats.Text))
            {
                var current = await view.GetTextAsync();
                if (current == _lastCopied) Clipboard.Clear();
            }
        }
        catch { try { Clipboard.Clear(); } catch { } }
        _lastCopied = null;
    }

    // ===================== TOTP =====================

    private void StartTotp() { _totpTimer.Stop(); _totpTimer.Start(); }
    private void StopTotp() { _totpTimer.Stop(); _totpItemId = null; }
    private void SafeTotpTick() => Safe(RefreshTotp);

    private void RefreshTotp()
    {
        if (_totpItemId is null) return;
        var t = _svc.GetTotp(_totpItemId);
        if (_totpItemId is null) return;
        if (t is null) { _totpValue.Text = "——————"; _totpSeconds.Text = ""; return; }
        _totpValue.Text = t.Code.Length == 6 ? t.Code.Substring(0, 3) + " " + t.Code.Substring(3) : t.Code;
        _totpCountdown.Maximum = t.Period;
        _totpCountdown.Value = t.RemainingSeconds;
        _totpSeconds.Text = t.RemainingSeconds + "s";
    }

    // ===================== Toolbar actions =====================

    private async Task SyncClick()
    {
        try
        {
            _listBusy.IsActive = true;
            _busyCts = new CancellationTokenSource();
            var r = await _svc.SyncAsync(_busyCts.Token);
            Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
            if (r.Success) { UpdateStatusBanner(_svc.GetStatus()); LoadItems(_searchBox.Text); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView.SyncClick", ex); Toast(InfoBarSeverity.Error, ex.Message); }
        finally { _listBusy.IsActive = false; }
    }

    private void LockClick()
    {
        Safe(() =>
        {
            _svc.Lock();
            Toast(InfoBarSeverity.Success, P("Vault locked. Keys wiped from memory.", "保險庫已鎖定，記憶體金鑰已清除。"));
            ShowDetailPlaceholder();
            Refresh();
        });
    }

    private async Task LogoutClick()
    {
        try
        {
            if (!await ConfirmAsync(P("Log out?", "登出？"),
                P("You will need your email and master password to log in again.", "下次要再用電郵同主密碼登入。"),
                P("Log out", "登出")))
                return;
            _svc.Logout();
            Toast(InfoBarSeverity.Success, P("Logged out.", "已登出。"));
            ShowDetailPlaceholder();
            Refresh();
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView.LogoutClick", ex); }
    }

    // ===================== Password generator =====================

    private async Task GenClick()
    {
        try
        {
            var (dlg, getResult, regen) = BuildGeneratorDialog();
            regen();
            var res = await ShowDialogAsync(dlg);
            if (res == ContentDialogResult.Primary)
            {
                var pw = getResult();
                if (!string.IsNullOrEmpty(pw))
                    CopySecret(pw, persistent: false, P("Generated password copied — clipboard clears in 20s.", "已複製產生嘅密碼 — 20 秒後自動清除剪貼簿。"));
            }
        }
        catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView.GenClick", ex); }
    }

    private (ContentDialog dlg, Func<string> result, Action regenerate) BuildGeneratorDialog()
    {
        var outBox = new TextBox { IsReadOnly = true, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap, MinWidth = 360 };
        var typeToggle = new ToggleSwitch { Header = P("Passphrase (words) instead of password", "用通行短語（字詞）而唔係密碼"), IsOn = false };
        var lengthSlider = new Slider { Minimum = 5, Maximum = 64, Value = 16, Header = P("Length", "長度"), Width = 360 };
        var upper = new CheckBox { Content = P("Uppercase (A-Z)", "大寫（A-Z）"), IsChecked = true };
        var lower = new CheckBox { Content = P("Lowercase (a-z)", "細寫（a-z）"), IsChecked = true };
        var nums = new CheckBox { Content = P("Numbers (0-9)", "數字（0-9）"), IsChecked = true };
        var special = new CheckBox { Content = P("Special (!@#$…)", "特殊符號（!@#$…）"), IsChecked = true };
        var wordsSlider = new Slider { Minimum = 3, Maximum = 12, Value = 4, Header = P("Words", "字詞數"), Width = 360 };
        var sepBox = new TextBox { Header = P("Separator", "分隔符"), Text = "-", Width = 120 };
        var capitalize = new CheckBox { Content = P("Capitalize", "首字母大寫"), IsChecked = true };

        var passwordOpts = new StackPanel { Spacing = 4 };
        foreach (var c in new UIElement[] { lengthSlider, upper, lower, nums, special }) passwordOpts.Children.Add(c);
        var passphraseOpts = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        foreach (var c in new UIElement[] { wordsSlider, sepBox, capitalize }) passphraseOpts.Children.Add(c);
        var regenBtn = new Button { Content = P("Regenerate", "重新產生") };

        BitwardenService.GenOptions Opts() => new(
            Passphrase: typeToggle.IsOn, Length: (int)lengthSlider.Value,
            Uppercase: upper.IsChecked == true, Lowercase: lower.IsChecked == true,
            Numbers: nums.IsChecked == true, Special: special.IsChecked == true,
            Words: (int)wordsSlider.Value, Separator: sepBox.Text, Capitalize: capitalize.IsChecked == true);
        void Regen() => outBox.Text = BitwardenService.Generate(Opts());

        regenBtn.Click += (_, _) => Regen();
        typeToggle.Toggled += (_, _) =>
        {
            passwordOpts.Visibility = typeToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
            passphraseOpts.Visibility = typeToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            Regen();
        };
        void OnAny(object s, RoutedEventArgs e) => Regen();
        foreach (var cb in new[] { upper, lower, nums, special, capitalize }) cb.Click += OnAny;
        lengthSlider.ValueChanged += (_, _) => Regen();
        wordsSlider.ValueChanged += (_, _) => Regen();

        var content = new StackPanel { Spacing = 10, MinWidth = 380 };
        foreach (var c in new UIElement[] { outBox, regenBtn, typeToggle, passwordOpts, passphraseOpts }) content.Children.Add(c);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Password generator", "密碼產生器"),
            Content = new ScrollViewer { Content = content, MaxHeight = 460 },
            PrimaryButtonText = P("Copy", "複製"),
            CloseButtonText = P("Close", "關閉"),
            DefaultButton = ContentDialogButton.Primary,
        };
        return (dlg, () => outBox.Text, Regen);
    }

    // ===================== Dialog / helpers =====================

    private async Task<bool> ConfirmAsync(string title, string content, string? primary = null)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = primary ?? P("Continue", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await ShowDialogAsync(dlg) == ContentDialogResult.Primary;
    }

    private static async Task<ContentDialogResult> ShowDialogAsync(ContentDialog dlg)
    {
        try { return await dlg.ShowAsync(); }
        catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView.ShowDialog", ex); return ContentDialogResult.None; }
    }

    private void Toast(InfoBarSeverity severity, string? message)
    {
        _toastBar.Severity = severity;
        _toastBar.Title = "";
        _toastBar.Message = message ?? "";
        _toastBar.IsOpen = true;
    }

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { CrashLogger.Log("BitwardenConnectionView", ex); }
    }

    /// <summary>關閉分頁時叫：取消在途工作、停計時器、清金鑰 · Called on tab close: cancel work, stop timers, wipe keys.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Loc.I.LanguageChanged -= OnLang; } catch { }
        try { _searchCts?.Cancel(); } catch { }
        try { _busyCts?.Cancel(); } catch { }
        try { _clipTimer.Stop(); } catch { }
        try { _totpTimer.Stop(); } catch { }
        try { _svc.Logout(); } catch { } // wipe in-memory keys + tokens for this tab's service
    }

    private static Style? TryStyle(string key)
    {
        try { return Application.Current.Resources[key] as Style; }
        catch { return null; }
    }

    private static Brush? TryBrush(string key)
    {
        try { return Application.Current.Resources[key] as Brush; }
        catch { return null; }
    }
}
