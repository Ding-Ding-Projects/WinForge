using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Bitwarden 密碼庫 · A native WinUI front-end over the official <c>bw</c> CLI.
/// 解鎖 → 瀏覽／搜尋 → 複製用戶名／密碼（剪貼簿自動清除）→ 睇 TOTP（倒數）→ 產生密碼 → 同步 → 新增／編輯登入。
/// Unlock → browse/search → copy username/password (clipboard auto-clears) → view TOTP (countdown) →
/// generate passwords → sync → add/edit logins. Bilingual throughout; secrets never persisted or logged.
/// </summary>
public sealed partial class BitwardenModule : Page
{
    private List<TweakDefinition>? _ops;
    private List<BitwardenService.VaultItem> _items = new();
    private BitwardenService.VaultItem? _selected;
    private string? _revealedPassword;     // 只喺記憶體短暫保留所選項目密碼 · selected item password, memory only
    private bool _passwordRevealed;

    private CancellationTokenSource? _searchCts;

    // 剪貼簿自動清除 · clipboard auto-clear
    private readonly DispatcherTimer _clipTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private string? _lastCopied;

    // TOTP 倒數 · TOTP countdown
    private readonly DispatcherTimer _totpTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _totpItemId;

    public BitwardenModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        _clipTimer.Tick += (_, _) => ClearClipboardIfOurs();
        _totpTimer.Tick += (_, _) => TotpTick();
        Unloaded += OnUnloaded;
        Loaded += async (_, _) => { Render(); BuildOps(); await Refresh(); };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        _clipTimer.Stop();
        _totpTimer.Stop();
        // 離開頁面唔自動鎖；金鑰留喺記憶體（DPAPI 包住）直到鎖定／登出／退出。
        // We don't auto-lock on navigation; the session key stays DPAPI-wrapped in memory until lock/logout/exit.
    }

    private void OnLang(object? sender, EventArgs e) { Render(); BuildOps(); _ = Refresh(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private bool Zh => Loc.I.IsCantonesePrimary;

    // ===================== Render static text =====================

    private void Render()
    {
        HeaderTitle.Text = "Bitwarden Vault · Bitwarden 密碼庫";
        HeaderBlurb.Text = P(
            "A native vault front-end over the Bitwarden CLI: unlock, search, copy username / password / TOTP, generate passwords, add & edit logins, and sync — all inside WinForge. Secrets stay in memory only and the clipboard auto-clears.",
            "喺 WinForge 內建嘅 Bitwarden 密碼庫介面（包住 bw CLI）：解鎖、搜尋、複製用戶名／密碼／TOTP、產生密碼、新增同編輯登入、同步。機密只留喺記憶體，剪貼簿會自動清除。");

        ServerHeader.Text = P("Self-hosted server (optional)", "自寄存伺服器（選用）");
        ServerHint.Text = P(
            "If you use a self-hosted Bitwarden / Vaultwarden, set its URL before logging in. Leave blank for the official cloud.",
            "如果你用自寄存嘅 Bitwarden／Vaultwarden，登入前設定佢嘅網址。用官方雲端就留空。");
        ServerSetBtn.Content = P("Set server", "設定伺服器");
        ServerClearBtn.Content = P("Use cloud", "用官方雲端");

        EmailBox.PlaceholderText = P("Email", "電郵");
        EmailBox.Header = P("Email", "電郵");
        MasterBox.PlaceholderText = P("Master password", "主密碼");
        MasterBox.Header = P("Master password", "主密碼");
        TwoFaToggle.Content = P("I need to enter a two-step (2FA) code", "我需要輸入兩步（2FA）驗證碼");
        TwoFaCode.PlaceholderText = P("2FA code", "2FA 驗證碼");
        if (TwoFaMethod.Items.Count == 0)
        {
            TwoFaMethod.Items.Add(P("Authenticator app", "驗證器 App"));
            TwoFaMethod.Items.Add(P("Email", "電郵"));
            TwoFaMethod.Items.Add(P("YubiKey", "YubiKey"));
            TwoFaMethod.SelectedIndex = 0;
        }

        RefreshLabel.Text = P("Refresh", "重新整理");
        SyncLabel.Text = P("Sync", "同步");
        AddLabel.Text = P("Add login", "新增登入");
        GenLabel.Text = P("Generate", "產生密碼");
        LockLabel.Text = P("Lock", "鎖定");
        LogoutLabel.Text = P("Log out", "登出");
        EditLabel.Text = P("Edit", "編輯");

        SearchBox.PlaceholderText = P("Search vault…", "搜尋密碼庫…");
        UserLabel.Text = P("Username", "用戶名");
        PassLabel.Text = P("Password", "密碼");
        TotpLabel.Text = P("Verification code (TOTP)", "驗證碼（TOTP）");
        UriLabel.Text = P("Website", "網址");
        NotesLabel.Text = P("Notes", "備註");
        DetailPlaceholder.Text = P("Select an item to view its details.", "揀一個項目睇詳情。");

        OpsHeader.Text = P("Maintenance", "維護");
        ToolTipService.SetToolTip(CopyUserBtn, P("Copy username", "複製用戶名"));
        ToolTipService.SetToolTip(CopyPassBtn, P("Copy password", "複製密碼"));
        ToolTipService.SetToolTip(RevealPassBtn, P("Reveal / hide password", "顯示／隱藏密碼"));
        ToolTipService.SetToolTip(CopyTotpBtn, P("Copy TOTP", "複製 TOTP"));
    }

    private void BuildOps()
    {
        _ops ??= BitwardenOperations.All().ToList();
        OpsPanel.Children.Clear();
        foreach (var op in _ops)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }

    // ===================== Status / engine =====================

    /// <summary>重新讀取狀態，刷新成個介面 · Re-read bw status and refresh the whole surface.</summary>
    private async Task Refresh()
    {
        var status = await BitwardenService.GetStatusAsync();

        // Engine bar
        if (status.Status == BitwardenService.VaultStatus.NotInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Bitwarden CLI not found", "搵唔到 Bitwarden CLI");
            EngineBar.Message = P("Click to install the bw CLI automatically (winget) — no restart needed.",
                "撳一下自動安裝 bw CLI（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                BitwardenService.WingetId, "Install Bitwarden CLI", "安裝 Bitwarden CLI",
                async () => await Refresh(), null);

            DesktopBar.IsOpen = true;
            DesktopBar.Title = P("Optional: Bitwarden desktop app", "選用：Bitwarden 桌面程式");
            DesktopBar.Message = P("Install the full desktop app as well (optional).",
                "亦可一併安裝完整桌面程式（選用）。");
            DesktopBar.ActionButton = EngineBars.AutoInstallButton(
                BitwardenService.DesktopWingetId, "Install desktop app", "安裝桌面程式",
                async () => await Refresh(), null);
        }
        else
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
            DesktopBar.IsOpen = false;
        }

        // Status banner
        UpdateStatusBanner(status);

        // Panels by status
        bool installed = status.Status != BitwardenService.VaultStatus.NotInstalled
                         && status.Status != BitwardenService.VaultStatus.Unknown;
        ServerExpander.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;

        switch (status.Status)
        {
            case BitwardenService.VaultStatus.Unauthenticated:
                ShowAuth(login: true);
                VaultPanel.Visibility = Visibility.Collapsed;
                break;
            case BitwardenService.VaultStatus.Locked:
                ShowAuth(login: false);
                VaultPanel.Visibility = Visibility.Collapsed;
                break;
            case BitwardenService.VaultStatus.Unlocked:
                AuthPanel.Visibility = Visibility.Collapsed;
                VaultPanel.Visibility = Visibility.Visible;
                await LoadItems(SearchBox.Text);
                break;
            default:
                AuthPanel.Visibility = Visibility.Collapsed;
                VaultPanel.Visibility = Visibility.Collapsed;
                break;
        }

        if (installed && ServerBox.Text.Length == 0)
        {
            var server = await BitwardenService.GetServerAsync();
            if (!string.IsNullOrWhiteSpace(server)) ServerBox.Text = server.Trim();
        }
    }

    private void UpdateStatusBanner(BitwardenService.StatusInfo status)
    {
        switch (status.Status)
        {
            case BitwardenService.VaultStatus.NotInstalled:
                StatusBar.Severity = InfoBarSeverity.Warning;
                StatusBar.Title = P("Not installed", "未安裝");
                StatusBar.Message = P("Install the Bitwarden CLI to begin.", "先安裝 Bitwarden CLI。");
                break;
            case BitwardenService.VaultStatus.Unauthenticated:
                StatusBar.Severity = InfoBarSeverity.Informational;
                StatusBar.Title = P("Not logged in", "未登入");
                StatusBar.Message = P("Log in with your Bitwarden email and master password.",
                    "用你嘅 Bitwarden 電郵同主密碼登入。");
                break;
            case BitwardenService.VaultStatus.Locked:
                StatusBar.Severity = InfoBarSeverity.Warning;
                StatusBar.Title = P("Locked", "已鎖定");
                StatusBar.Message = (status.UserEmail is { Length: > 0 } e
                    ? P($"Signed in as {e}. ", $"已登入：{e}。")
                    : "") + P("Enter your master password to unlock.", "輸入主密碼解鎖。");
                break;
            case BitwardenService.VaultStatus.Unlocked:
                StatusBar.Severity = InfoBarSeverity.Success;
                StatusBar.Title = P("Unlocked", "已解鎖");
                var who = status.UserEmail is { Length: > 0 } u ? u : P("your account", "你嘅帳戶");
                var sync = string.IsNullOrWhiteSpace(status.LastSync) ? "" :
                    P($" · last sync {FormatSync(status.LastSync)}", $"· 上次同步 {FormatSync(status.LastSync)}");
                StatusBar.Message = P($"Vault unlocked for {who}.{sync}", $"{who} 嘅密碼庫已解鎖。{sync}");
                break;
            default:
                StatusBar.Severity = InfoBarSeverity.Informational;
                StatusBar.Title = P("Status unknown", "狀態未知");
                StatusBar.Message = P("Could not read vault status.", "讀唔到密碼庫狀態。");
                break;
        }
    }

    private static string FormatSync(string iso)
    {
        return DateTimeOffset.TryParse(iso, out var dt) ? dt.LocalDateTime.ToString("g") : iso;
    }

    // ===================== Auth panel =====================

    private void ShowAuth(bool login)
    {
        AuthPanel.Visibility = Visibility.Visible;
        EmailBox.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        TwoFaToggle.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        if (!login) { TwoFaPanel.Visibility = Visibility.Collapsed; TwoFaToggle.IsChecked = false; }
        AuthTitle.Text = login ? P("Log in", "登入") : P("Unlock vault", "解鎖密碼庫");
        AuthButton.Content = login ? P("Log in", "登入") : P("Unlock", "解鎖");
        MasterBox.Password = "";
    }

    private void TwoFaToggle_Click(object sender, RoutedEventArgs e)
    {
        TwoFaPanel.Visibility = TwoFaToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Auth_Click(object sender, RoutedEventArgs e)
    {
        bool login = EmailBox.Visibility == Visibility.Visible;
        var master = MasterBox.Password;
        if (string.IsNullOrEmpty(master)) { Toast(InfoBarSeverity.Error, P("Enter your master password.", "請輸入主密碼。")); return; }

        AuthBusy.IsActive = true;
        AuthButton.IsEnabled = false;
        try
        {
            TweakResult r;
            if (login)
            {
                int? method = null; string? code = null;
                if (TwoFaToggle.IsChecked == true && !string.IsNullOrWhiteSpace(TwoFaCode.Text))
                {
                    method = TwoFaMethod.SelectedIndex switch { 0 => 0, 1 => 1, 2 => 3, _ => 0 };
                    code = TwoFaCode.Text.Trim();
                }
                r = await BitwardenService.LoginAsync(EmailBox.Text.Trim(), master, method, code);
            }
            else
            {
                r = await BitwardenService.UnlockAsync(master);
            }

            MasterBox.Password = ""; // 即時清除主密碼 · clear master password immediately
            if (r.Success)
            {
                Toast(InfoBarSeverity.Success, Zh ? r.Message?.Zh : r.Message?.En);
                await Refresh();
            }
            else
            {
                Toast(InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
            }
        }
        finally { AuthBusy.IsActive = false; AuthButton.IsEnabled = true; }
    }

    // ===================== Server config =====================

    private async void ServerSet_Click(object sender, RoutedEventArgs e)
    {
        var r = await BitwardenService.SetServerAsync(ServerBox.Text.Trim());
        Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Server set.", "已設定伺服器。") : (Zh ? r.Message?.Zh : r.Message?.En));
    }

    private async void ServerClear_Click(object sender, RoutedEventArgs e)
    {
        ServerBox.Text = "";
        var r = await BitwardenService.SetServerAsync("");
        Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Using official cloud.", "已改用官方雲端。") : (Zh ? r.Message?.Zh : r.Message?.En));
    }

    // ===================== Item list =====================

    private void Search_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedSearch(sender.Text, token);
    }

    private async Task DebouncedSearch(string text, CancellationToken token)
    {
        try { await Task.Delay(280, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        await LoadItems(text);
    }

    private async Task LoadItems(string? search)
    {
        ListBusy.IsActive = true;
        try
        {
            _items = await BitwardenService.ListItemsAsync(search);
            ItemsList.Items.Clear();
            foreach (var it in _items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                ItemsList.Items.Add(BuildItemRow(it));

            EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyHint.Text = string.IsNullOrWhiteSpace(search)
                ? P("No items in this vault.", "呢個密碼庫無項目。")
                : P("No items match your search.", "無符合搜尋嘅項目。");
        }
        finally { ListBusy.IsActive = false; }
    }

    private ListViewItem BuildItemRow(BitwardenService.VaultItem it)
    {
        var grid = new Grid { Padding = new Thickness(6, 8, 6, 8), ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = new FontIcon { FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Glyph = GlyphFor(it) };
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = it.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        var sub = it.Type == 1 && !string.IsNullOrWhiteSpace(it.Username)
            ? it.Username
            : it.TypeLabel(Zh);
        var subBlock = new TextBlock { Text = sub, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        if (TryBrush("TextFillColorSecondaryBrush") is { } sec) subBlock.Foreground = sec;
        info.Children.Add(subBlock);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        if (it.HasTotp)
        {
            var totp = new FontIcon { FontSize = 13, Glyph = "\uE916" }; // stopwatch
            if (TryBrush("AccentTextFillColorPrimaryBrush") is { } accent) totp.Foreground = accent;
            Grid.SetColumn(totp, 2);
            grid.Children.Add(totp);
        }

        return new ListViewItem { Content = grid, Tag = it.Id };
    }

    private static string GlyphFor(BitwardenService.VaultItem it) => it.Type switch
    {
        1 => "\uE8D7",  // login - key
        2 => "\uE70B",  // secure note
        3 => "\uE8C7",  // card
        4 => "\uE77B",  // identity / contact
        _ => "\uE8A5",  // generic item
    };

    private async void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is not ListViewItem lvi || lvi.Tag is not string id)
        {
            ShowDetailPlaceholder();
            return;
        }
        await SelectItem(id);
    }

    // ===================== Detail pane =====================

    private void ShowDetailPlaceholder()
    {
        _selected = null;
        StopTotp();
        DetailContent.Visibility = Visibility.Collapsed;
        DetailPlaceholder.Visibility = Visibility.Visible;
    }

    private async Task SelectItem(string id)
    {
        StopTotp();
        var item = await BitwardenService.GetItemAsync(id);
        if (item is null) { ShowDetailPlaceholder(); return; }
        _selected = item;
        _revealedPassword = item.Login?.Password;
        _passwordRevealed = false;

        DetailPlaceholder.Visibility = Visibility.Collapsed;
        DetailContent.Visibility = Visibility.Visible;

        DetailName.Text = item.Name;
        DetailType.Text = item.TypeLabel(Zh);

        // Username
        bool hasUser = !string.IsNullOrWhiteSpace(item.Username);
        UserRow.Visibility = hasUser ? Visibility.Visible : Visibility.Collapsed;
        UserValue.Text = item.Username;

        // Password
        bool hasPass = !string.IsNullOrEmpty(item.Login?.Password);
        PassRow.Visibility = hasPass ? Visibility.Visible : Visibility.Collapsed;
        PassValue.Text = hasPass ? "••••••••••" : "";
        RevealPassIcon.Glyph = "\uE7B3";
        // TOTP
        if (item.HasTotp)
        {
            TotpRow.Visibility = Visibility.Visible;
            _totpItemId = id;
            await RefreshTotp();
            StartTotp();
        }
        else
        {
            TotpRow.Visibility = Visibility.Collapsed;
            _totpItemId = null;
        }

        // URI
        bool hasUri = !string.IsNullOrWhiteSpace(item.PrimaryUri);
        UriRow.Visibility = hasUri ? Visibility.Visible : Visibility.Collapsed;
        UriValue.Text = item.PrimaryUri;

        // Notes
        bool hasNotes = !string.IsNullOrWhiteSpace(item.Notes);
        NotesRow.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        NotesValue.Text = item.Notes ?? "";

        EditBtn.Visibility = item.Type == 1 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RevealPass_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _passwordRevealed = !_passwordRevealed;
        PassValue.Text = _passwordRevealed ? (_revealedPassword ?? "") : "••••••••••";
        RevealPassIcon.Glyph = _passwordRevealed ? "\uE890" : "\uE7B3"; // eye (reveal/hide)
    }

    // ===================== Copy (with clipboard auto-clear) =====================

    private void CopyUser_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        CopySecret(_selected.Username, persistent: true,
            P("Username copied.", "已複製用戶名。"));
    }

    private async void CopyPass_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        var pw = _revealedPassword ?? await BitwardenService.GetPasswordAsync(_selected.Id);
        if (string.IsNullOrEmpty(pw)) { Toast(InfoBarSeverity.Error, P("No password to copy.", "無密碼可複製。")); return; }
        CopySecret(pw, persistent: false,
            P("Password copied — clipboard clears in 20s.", "已複製密碼 — 20 秒後自動清除剪貼簿。"));
    }

    private void CopyTotp_Click(object sender, RoutedEventArgs e)
    {
        var code = TotpValue.Text;
        if (string.IsNullOrWhiteSpace(code)) return;
        CopySecret(code, persistent: false,
            P("TOTP copied — clipboard clears in 20s.", "已複製 TOTP — 20 秒後自動清除剪貼簿。"));
    }

    /// <summary>複製到剪貼簿；機密項目 20 秒後自動清除 · Copy to clipboard; secrets auto-clear after 20s.</summary>
    private void CopySecret(string value, bool persistent, string toast)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(value);
            Clipboard.SetContent(dp);
            if (!persistent)
            {
                _lastCopied = value;
                _clipTimer.Stop();
                _clipTimer.Start();
            }
            Toast(InfoBarSeverity.Success, toast);
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    private async void ClearClipboardIfOurs()
    {
        _clipTimer.Stop();
        try
        {
            // 只喺剪貼簿仲係我哋複製嗰個值先清，避免清走使用者後來複製嘅嘢。
            // Only clear if the clipboard still holds what we copied — don't clobber the user's later copy.
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

    // ===================== TOTP countdown =====================

    private void StartTotp() { _totpTimer.Stop(); _totpTimer.Start(); }
    private void StopTotp() { _totpTimer.Stop(); _totpItemId = null; }

    private void TotpTick()
    {
        int secs = 30 - (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 30);
        TotpCountdown.Value = secs;
        if (secs >= 30 || secs <= 1) _ = RefreshTotp();
    }

    private async Task RefreshTotp()
    {
        if (_totpItemId is null) return;
        var code = await BitwardenService.GetTotpAsync(_totpItemId);
        if (_totpItemId is null) return; // could have changed during await
        TotpValue.Text = code ?? "——————";
    }

    // ===================== Toolbar actions =====================

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Refresh();

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        SyncBtn.IsEnabled = false;
        try
        {
            var r = await BitwardenService.SyncAsync();
            Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
            if (r.Success) await LoadItems(SearchBox.Text);
        }
        finally { SyncBtn.IsEnabled = true; }
    }

    private async void Lock_Click(object sender, RoutedEventArgs e)
    {
        var r = await BitwardenService.LockAsync();
        Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
        ShowDetailPlaceholder();
        await Refresh();
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Log out?", "登出？"),
            Content = P("You will need your email and master password to log in again.",
                "下次要再用電郵同主密碼登入。"),
            PrimaryButtonText = P("Log out", "登出"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var r = await BitwardenService.LogoutAsync();
        Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
        ShowDetailPlaceholder();
        await Refresh();
    }

    // ===================== Password generator =====================

    private async void Gen_Click(object sender, RoutedEventArgs e)
    {
        var (dlg, getResult, regen) = BuildGeneratorDialog();
        await regen();
        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            var pw = getResult();
            if (!string.IsNullOrEmpty(pw))
                CopySecret(pw, persistent: false,
                    P("Generated password copied — clipboard clears in 20s.", "已複製產生嘅密碼 — 20 秒後自動清除剪貼簿。"));
        }
    }

    private (ContentDialog dlg, Func<string> result, Func<Task> regenerate) BuildGeneratorDialog()
    {
        var outBox = new TextBox
        {
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
            MinWidth = 360,
        };

        var typeToggle = new ToggleSwitch
        {
            Header = P("Passphrase (words) instead of password", "用通行短語（字詞）而唔係密碼"),
            IsOn = false,
        };

        var lengthSlider = new Slider { Minimum = 5, Maximum = 64, Value = 16, Header = P("Length", "長度"), Width = 360 };
        var upper = new CheckBox { Content = P("Uppercase (A-Z)", "大寫（A-Z）"), IsChecked = true };
        var lower = new CheckBox { Content = P("Lowercase (a-z)", "細寫（a-z）"), IsChecked = true };
        var nums = new CheckBox { Content = P("Numbers (0-9)", "數字（0-9）"), IsChecked = true };
        var special = new CheckBox { Content = P("Special (!@#$…)", "特殊符號（!@#$…）"), IsChecked = true };

        var wordsSlider = new Slider { Minimum = 3, Maximum = 12, Value = 4, Header = P("Words", "字詞數"), Width = 360 };
        var sepBox = new TextBox { Header = P("Separator", "分隔符"), Text = "-", Width = 120 };
        var capitalize = new CheckBox { Content = P("Capitalize", "首字母大寫"), IsChecked = true };

        var passwordOpts = new StackPanel { Spacing = 4 };
        passwordOpts.Children.Add(lengthSlider);
        passwordOpts.Children.Add(upper);
        passwordOpts.Children.Add(lower);
        passwordOpts.Children.Add(nums);
        passwordOpts.Children.Add(special);

        var passphraseOpts = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed };
        passphraseOpts.Children.Add(wordsSlider);
        passphraseOpts.Children.Add(sepBox);
        passphraseOpts.Children.Add(capitalize);

        var regenBtn = new Button { Content = P("Regenerate", "重新產生") };

        BitwardenService.GenOptions Opts() => new(
            Passphrase: typeToggle.IsOn,
            Length: (int)lengthSlider.Value,
            Uppercase: upper.IsChecked == true,
            Lowercase: lower.IsChecked == true,
            Numbers: nums.IsChecked == true,
            Special: special.IsChecked == true,
            Words: (int)wordsSlider.Value,
            Separator: sepBox.Text,
            Capitalize: capitalize.IsChecked == true);

        async Task Regen()
        {
            var pw = await BitwardenService.GenerateAsync(Opts());
            outBox.Text = pw ?? "";
        }

        regenBtn.Click += async (_, _) => await Regen();
        typeToggle.Toggled += async (_, _) =>
        {
            passwordOpts.Visibility = typeToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
            passphraseOpts.Visibility = typeToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            await Regen();
        };
        void OnAny(object s, RoutedEventArgs e) => _ = Regen();
        foreach (var cb in new[] { upper, lower, nums, special, capitalize })
            cb.Click += OnAny;
        lengthSlider.ValueChanged += (_, _) => _ = Regen();
        wordsSlider.ValueChanged += (_, _) => _ = Regen();

        var content = new StackPanel { Spacing = 10, MinWidth = 380 };
        content.Children.Add(outBox);
        content.Children.Add(regenBtn);
        content.Children.Add(typeToggle);
        content.Children.Add(passwordOpts);
        content.Children.Add(passphraseOpts);

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

    // ===================== Add / edit login =====================

    private async void Add_Click(object sender, RoutedEventArgs e) => await ShowItemEditor(null);

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null || _selected.Type != 1) return;
        await ShowItemEditor(_selected);
    }

    private async Task ShowItemEditor(BitwardenService.VaultItem? existing)
    {
        var nameBox = new TextBox { Header = P("Name", "名稱"), Text = existing?.Name ?? "", Width = 360 };
        var userBox = new TextBox { Header = P("Username", "用戶名"), Text = existing?.Username ?? "", Width = 360 };
        var passBox = new PasswordBox { Header = P("Password", "密碼"), Password = existing?.Login?.Password ?? "", Width = 360 };
        var totpBox = new TextBox { Header = P("TOTP secret / otpauth URI", "TOTP 密鑰／otpauth URI"), Text = existing?.Login?.Totp ?? "", Width = 360 };
        var uriBox = new TextBox { Header = P("Website (URI)", "網址（URI）"), Text = existing?.PrimaryUri ?? "", Width = 360 };
        var notesBox = new TextBox
        {
            Header = P("Notes", "備註"),
            Text = existing?.Notes ?? "",
            Width = 360,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 80,
        };

        var genInline = new Button { Content = P("Generate password", "產生密碼") };
        genInline.Click += async (_, _) =>
        {
            var pw = await BitwardenService.GenerateAsync(new BitwardenService.GenOptions(
                false, 16, true, true, true, true, 4, "-", true));
            if (!string.IsNullOrEmpty(pw)) passBox.Password = pw;
        };

        var content = new StackPanel { Spacing = 10, MinWidth = 380 };
        content.Children.Add(nameBox);
        content.Children.Add(userBox);
        content.Children.Add(passBox);
        content.Children.Add(genInline);
        content.Children.Add(totpBox);
        content.Children.Add(uriBox);
        content.Children.Add(notesBox);

        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = existing is null ? P("Add login", "新增登入") : P("Edit login", "編輯登入"),
            Content = new ScrollViewer { Content = content, MaxHeight = 460 },
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            Toast(InfoBarSeverity.Error, P("Name is required.", "需要名稱。"));
            return;
        }

        TweakResult r;
        if (existing is null)
            r = await BitwardenService.CreateLoginAsync(
                nameBox.Text.Trim(), userBox.Text, passBox.Password, totpBox.Text, uriBox.Text, notesBox.Text);
        else
            r = await BitwardenService.EditLoginAsync(
                existing.Id, nameBox.Text.Trim(), userBox.Text, passBox.Password, totpBox.Text, uriBox.Text, notesBox.Text);

        Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
        if (r.Success)
        {
            await LoadItems(SearchBox.Text);
            if (existing is not null) await SelectItem(existing.Id);
        }
    }

    // ===================== Toast helper =====================

    private void Toast(InfoBarSeverity severity, string? message)
    {
        ToastBar.Severity = severity;
        ToastBar.Title = "";
        ToastBar.Message = message ?? "";
        ToastBar.IsOpen = true;
    }

    /// <summary>安全攞主題筆刷（搵唔到就回 null）· Safely fetch a theme brush, or null if absent.</summary>
    private static Brush? TryBrush(string key)
    {
        try { return Application.Current.Resources[key] as Brush; }
        catch { return null; }
    }
}
