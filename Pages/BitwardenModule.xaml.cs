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
/// Bitwarden 保險庫 · A REAL native Bitwarden client — pure managed C# (no bw CLI, no desktop app, no browser).
/// 登入（API + 端對端解密）→ 解鎖 → 按資料夾瀏覽／搜尋 → 睇詳情（顯示／隱藏密碼、複製、自動清除剪貼簿、開網址）
/// → 本機 TOTP（倒數）→ 產生密碼 → 同步 → 鎖定（清除記憶體金鑰）。雙語介面，機密只留喺記憶體。
/// Login (API + E2E decryption) → unlock → browse/search by folder → details (show/hide password, copy with
/// auto-clear, open URL) → local TOTP countdown → generate → sync → lock (wipe keys). Bilingual throughout.
/// </summary>
public sealed partial class BitwardenModule : Page
{
    private static bool _restored;

    private readonly BitwardenService _svc = BitwardenService.Shared;
    private List<TweakDefinition>? _ops;
    private List<BitwardenService.VaultItem> _items = new();
    private BitwardenService.VaultItem? _selected;
    private string? _revealedPassword;
    private bool _passwordRevealed;

    private CancellationTokenSource? _searchCts;

    private readonly DispatcherTimer _clipTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private string? _lastCopied;

    private readonly DispatcherTimer _totpTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _totpItemId;

    public BitwardenModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        _clipTimer.Tick += (_, _) => ClearClipboardIfOurs();
        _totpTimer.Tick += (_, _) => TotpTick();
        Unloaded += OnUnloaded;
        Loaded += (_, _) =>
        {
            if (!_restored) { _restored = true; _svc.TryRestoreSession(); }
            Render();
            BuildOps();
            Refresh();
        };
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        _clipTimer.Stop();
        _totpTimer.Stop();
        // 離開頁面唔自動鎖；金鑰留喺記憶體直到鎖定／登出／退出。
        // We don't auto-lock on navigation; keys stay in memory until lock / logout / exit.
    }

    private void OnLang(object? sender, EventArgs e) { Render(); BuildOps(); Refresh(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private bool Zh => Loc.I.IsCantonesePrimary;

    // ===================== Render static text =====================

    private void Render()
    {
        HeaderTitle.Text = "Bitwarden Vault · Bitwarden 保險庫";
        HeaderBlurb.Text = P(
            "A native Bitwarden client built into WinForge — it talks to the Bitwarden API and decrypts your vault end-to-end in pure managed code (no Bitwarden CLI, app, or browser). Log in, browse by folder, copy username / password / TOTP (clipboard auto-clears), generate passwords, and sync. Keys live in memory only and are wiped on lock.",
            "WinForge 內建嘅原生 Bitwarden 用戶端 —— 直接同 Bitwarden API 對話，喺純 managed 程式碼度端對端解密你嘅保險庫（唔使 Bitwarden CLI、App 或瀏覽器）。登入、按資料夾瀏覽、複製用戶名／密碼／TOTP（剪貼簿自動清除）、產生密碼、同步。金鑰只留喺記憶體，鎖定即清除。");

        ServerBox.Header = P("Server (base URL)", "伺服器（基底網址）");
        ServerBox.PlaceholderText = "https://vault.bitwarden.com";
        ServerHint.Text = P(
            "Leave the default for the official cloud, or enter your self-hosted Bitwarden / Vaultwarden URL.",
            "用官方雲端就保留預設，或者輸入你自寄存嘅 Bitwarden／Vaultwarden 網址。");

        EmailBox.PlaceholderText = P("Email", "電郵");
        EmailBox.Header = P("Email", "電郵");
        MasterBox.PlaceholderText = P("Master password", "主密碼");
        MasterBox.Header = P("Master password", "主密碼");
        TwoFaToggle.Content = P("I need to enter a two-step (2FA) code", "我需要輸入兩步（2FA）驗證碼");
        TwoFaCode.PlaceholderText = P("2FA code", "2FA 驗證碼");
        if (TwoFaMethod.Items.Count == 0)
        {
            TwoFaMethod.Items.Add(P("Authenticator app (TOTP)", "驗證器 App（TOTP）"));
            TwoFaMethod.Items.Add(P("Email code", "電郵驗證碼"));
            TwoFaMethod.SelectedIndex = 0;
        }

        SyncLabel.Text = P("Sync", "同步");
        GenLabel.Text = P("Generate", "產生密碼");
        LockLabel.Text = P("Lock", "鎖定");
        LogoutLabel.Text = P("Log out", "登出");

        SearchBox.PlaceholderText = P("Search vault…", "搜尋保險庫…");
        UserLabel.Text = P("Username", "用戶名");
        PassLabel.Text = P("Password", "密碼");
        TotpLabel.Text = P("Verification code (TOTP)", "驗證碼（TOTP）");
        UriLabel.Text = P("Website", "網址");
        CardLabel.Text = P("Card", "信用卡");
        NotesLabel.Text = P("Notes", "備註");
        DetailPlaceholder.Text = P("Select an item to view its details.", "揀一個項目睇詳情。");

        OpsHeader.Text = P("Maintenance", "維護");
        ToolTipService.SetToolTip(CopyUserBtn, P("Copy username", "複製用戶名"));
        ToolTipService.SetToolTip(CopyPassBtn, P("Copy password", "複製密碼"));
        ToolTipService.SetToolTip(RevealPassBtn, P("Reveal / hide password", "顯示／隱藏密碼"));
        ToolTipService.SetToolTip(CopyTotpBtn, P("Copy TOTP", "複製 TOTP"));
        ToolTipService.SetToolTip(OpenUriBtn, P("Open website", "開網址"));
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

    // ===================== Status / surface =====================

    private void Refresh()
    {
        var status = _svc.GetStatus();
        UpdateStatusBanner(status);

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
                LoadItems(SearchBox.Text);
                break;
        }
    }

    private void UpdateStatusBanner(BitwardenService.StatusInfo status)
    {
        switch (status.Status)
        {
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
                var sync = status.LastSync is { } ls
                    ? P($" · last sync {ls.LocalDateTime:g}", $" · 上次同步 {ls.LocalDateTime:g}")
                    : "";
                StatusBar.Message = P($"Vault unlocked for {who}.{sync}", $"{who} 嘅保險庫已解鎖。{sync}");
                break;
        }
    }

    // ===================== Auth panel =====================

    private void ShowAuth(bool login)
    {
        AuthPanel.Visibility = Visibility.Visible;
        ServerBox.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        ServerHint.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        EmailBox.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        TwoFaToggle.Visibility = login ? Visibility.Visible : Visibility.Collapsed;
        if (!login) { TwoFaPanel.Visibility = Visibility.Collapsed; TwoFaToggle.IsChecked = false; }
        AuthTitle.Text = login ? P("Log in", "登入") : P("Unlock vault", "解鎖保險庫");
        AuthButton.Content = login ? P("Log in", "登入") : P("Unlock", "解鎖");
        MasterBox.Password = "";

        if (login)
        {
            if (string.IsNullOrWhiteSpace(ServerBox.Text))
            {
                var saved = _svc.SavedBaseUrl;
                ServerBox.Text = string.IsNullOrWhiteSpace(saved) ? BitwardenService.DefaultBase : saved;
            }
            if (string.IsNullOrWhiteSpace(EmailBox.Text) && _svc.SavedEmail.Length > 0)
                EmailBox.Text = _svc.SavedEmail;
        }
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
            if (login)
            {
                int? method = null; string? code = null;
                if (TwoFaToggle.IsChecked == true && !string.IsNullOrWhiteSpace(TwoFaCode.Text))
                {
                    method = TwoFaMethod.SelectedIndex switch { 0 => 0, 1 => 1, _ => 0 }; // 0=Authenticator,1=Email
                    code = TwoFaCode.Text.Trim();
                }
                var r = await _svc.LoginAsync(EmailBox.Text.Trim(), master, ServerBox.Text.Trim(), code, method);
                MasterBox.Password = "";
                if (r.TwoFactorRequired)
                {
                    TwoFaToggle.IsChecked = true;
                    TwoFaPanel.Visibility = Visibility.Visible;
                    Toast(InfoBarSeverity.Warning, Zh ? r.Message?.Zh : r.Message?.En);
                    return;
                }
                if (r.Success) { Toast(InfoBarSeverity.Success, Zh ? r.Message?.Zh : r.Message?.En); Refresh(); }
                else Toast(InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
            }
            else
            {
                var r = await _svc.UnlockAsync(master);
                MasterBox.Password = "";
                if (r.Success) { Toast(InfoBarSeverity.Success, Zh ? r.Message?.Zh : r.Message?.En); Refresh(); }
                else Toast(InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
            }
        }
        finally { AuthBusy.IsActive = false; AuthButton.IsEnabled = true; }
    }

    // ===================== Item list (grouped by folder) =====================

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
        try { await Task.Delay(220, token); } catch { return; }
        if (token.IsCancellationRequested) return;
        LoadItems(text);
    }

    private void LoadItems(string? search)
    {
        ListBusy.IsActive = true;
        try
        {
            _items = _svc.ListItems(search);
            ItemsList.Items.Clear();

            // Group by folder name, sorted; "No Folder" last.
            var groups = _items
                .GroupBy(i => _svc.FolderName(i.FolderId, Zh))
                .OrderBy(g => g.Key == P("No Folder", "無資料夾") ? "￿" : g.Key, StringComparer.CurrentCultureIgnoreCase);

            foreach (var g in groups)
            {
                ItemsList.Items.Add(BuildGroupHeader(g.Key));
                foreach (var it in g.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
                    ItemsList.Items.Add(BuildItemRow(it));
            }

            EmptyHint.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyHint.Text = string.IsNullOrWhiteSpace(search)
                ? P("No items in this vault. Try Sync.", "呢個保險庫無項目。試吓同步。")
                : P("No items match your search.", "無符合搜尋嘅項目。");
        }
        finally { ListBusy.IsActive = false; }
    }

    private ListViewItem BuildGroupHeader(string name)
    {
        var tb = new TextBlock
        {
            Text = name,
            FontSize = 11,
            Margin = new Thickness(6, 8, 6, 2),
        };
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
        Grid.SetColumn(icon, 0);
        grid.Children.Add(icon);

        var info = new StackPanel();
        info.Children.Add(new TextBlock { Text = it.Name, TextTrimming = TextTrimming.CharacterEllipsis });
        var sub = it.Type == 1 && !string.IsNullOrWhiteSpace(it.Username) ? it.Username! : it.TypeLabel(Zh);
        var subBlock = new TextBlock { Text = sub, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis };
        if (TryBrush("TextFillColorSecondaryBrush") is { } sec) subBlock.Foreground = sec;
        info.Children.Add(subBlock);
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        if (it.HasTotp)
        {
            var totp = new FontIcon { FontSize = 13, Glyph = "" };
            if (TryBrush("AccentTextFillColorPrimaryBrush") is { } accent) totp.Foreground = accent;
            Grid.SetColumn(totp, 2);
            grid.Children.Add(totp);
        }

        return new ListViewItem { Content = grid, Tag = it.Id };
    }

    private static string GlyphFor(BitwardenService.VaultItem it) => it.Type switch
    {
        1 => "",
        2 => "",
        3 => "",
        4 => "",
        _ => "",
    };

    private void Items_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ItemsList.SelectedItem is not ListViewItem lvi || lvi.Tag is not string id)
        {
            ShowDetailPlaceholder();
            return;
        }
        SelectItem(id);
    }

    // ===================== Detail pane =====================

    private void ShowDetailPlaceholder()
    {
        _selected = null;
        StopTotp();
        DetailContent.Visibility = Visibility.Collapsed;
        DetailPlaceholder.Visibility = Visibility.Visible;
    }

    private void SelectItem(string id)
    {
        StopTotp();
        var item = _svc.GetItem(id);
        if (item is null) { ShowDetailPlaceholder(); return; }
        _selected = item;
        _revealedPassword = item.Login?.Password;
        _passwordRevealed = false;

        DetailPlaceholder.Visibility = Visibility.Collapsed;
        DetailContent.Visibility = Visibility.Visible;

        DetailName.Text = item.Name;
        DetailType.Text = item.TypeLabel(Zh);

        bool hasUser = !string.IsNullOrWhiteSpace(item.Username);
        UserRow.Visibility = hasUser ? Visibility.Visible : Visibility.Collapsed;
        UserValue.Text = item.Username ?? "";

        bool hasPass = !string.IsNullOrEmpty(item.Login?.Password);
        PassRow.Visibility = hasPass ? Visibility.Visible : Visibility.Collapsed;
        PassValue.Text = hasPass ? "••••••••••" : "";
        RevealPassIcon.Glyph = "";

        if (item.HasTotp)
        {
            TotpRow.Visibility = Visibility.Visible;
            _totpItemId = id;
            RefreshTotp();
            StartTotp();
        }
        else
        {
            TotpRow.Visibility = Visibility.Collapsed;
            _totpItemId = null;
        }

        bool hasUri = !string.IsNullOrWhiteSpace(item.PrimaryUri);
        UriRow.Visibility = hasUri ? Visibility.Visible : Visibility.Collapsed;
        UriValue.Text = item.PrimaryUri ?? "";

        bool hasCard = !string.IsNullOrWhiteSpace(item.CardSummary);
        CardRow.Visibility = hasCard ? Visibility.Visible : Visibility.Collapsed;
        CardValue.Text = item.CardSummary ?? "";

        bool hasNotes = !string.IsNullOrWhiteSpace(item.Notes);
        NotesRow.Visibility = hasNotes ? Visibility.Visible : Visibility.Collapsed;
        NotesValue.Text = item.Notes ?? "";
    }

    private void RevealPass_Click(object sender, RoutedEventArgs e)
    {
        if (_selected is null) return;
        _passwordRevealed = !_passwordRevealed;
        PassValue.Text = _passwordRevealed ? (_revealedPassword ?? "") : "••••••••••";
        RevealPassIcon.Glyph = _passwordRevealed ? "" : "";
    }

    private async void OpenUri_Click(object sender, RoutedEventArgs e)
    {
        var uri = _selected?.PrimaryUri;
        if (string.IsNullOrWhiteSpace(uri)) return;
        if (!uri.Contains("://")) uri = "https://" + uri;
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var u) &&
                (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps))
                await Windows.System.Launcher.LaunchUriAsync(u);
            else
                Toast(InfoBarSeverity.Error, P("That URL is not a valid web address.", "嗰個唔係有效嘅網址。"));
        }
        catch (Exception ex) { Toast(InfoBarSeverity.Error, ex.Message); }
    }

    // ===================== Copy (with clipboard auto-clear) =====================

    private void CopyUser_Click(object sender, RoutedEventArgs e)
    {
        if (_selected?.Username is not { Length: > 0 } u) return;
        CopySecret(u, persistent: true, P("Username copied.", "已複製用戶名。"));
    }

    private void CopyPass_Click(object sender, RoutedEventArgs e)
    {
        var pw = _revealedPassword;
        if (string.IsNullOrEmpty(pw)) { Toast(InfoBarSeverity.Error, P("No password to copy.", "無密碼可複製。")); return; }
        CopySecret(pw, persistent: false,
            P("Password copied — clipboard clears in 20s.", "已複製密碼 — 20 秒後自動清除剪貼簿。"));
    }

    private void CopyTotp_Click(object sender, RoutedEventArgs e)
    {
        var code = TotpValue.Text;
        if (string.IsNullOrWhiteSpace(code) || code.Contains('—')) return;
        CopySecret(code, persistent: false,
            P("TOTP copied — clipboard clears in 20s.", "已複製 TOTP — 20 秒後自動清除剪貼簿。"));
    }

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

    // ===================== TOTP countdown (local RFC 6238) =====================

    private void StartTotp() { _totpTimer.Stop(); _totpTimer.Start(); }
    private void StopTotp() { _totpTimer.Stop(); _totpItemId = null; }

    private void TotpTick() => RefreshTotp();

    private void RefreshTotp()
    {
        if (_totpItemId is null) return;
        var t = _svc.GetTotp(_totpItemId);
        if (_totpItemId is null) return;
        if (t is null)
        {
            TotpValue.Text = "——————";
            TotpSeconds.Text = "";
            return;
        }
        // Format as "123 456" for readability when 6 digits.
        TotpValue.Text = t.Code.Length == 6 ? t.Code.Substring(0, 3) + " " + t.Code.Substring(3) : t.Code;
        TotpCountdown.Maximum = t.Period;
        TotpCountdown.Value = t.RemainingSeconds;
        TotpSeconds.Text = t.RemainingSeconds + "s";
    }

    // ===================== Toolbar actions =====================

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        SyncBtn.IsEnabled = false;
        ListBusy.IsActive = true;
        try
        {
            var r = await _svc.SyncAsync();
            Toast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, Zh ? r.Message?.Zh : r.Message?.En);
            if (r.Success) { UpdateStatusBanner(_svc.GetStatus()); LoadItems(SearchBox.Text); }
        }
        finally { SyncBtn.IsEnabled = true; ListBusy.IsActive = false; }
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        _svc.Lock();
        Toast(InfoBarSeverity.Success, P("Vault locked. Keys wiped from memory.", "保險庫已鎖定，記憶體金鑰已清除。"));
        ShowDetailPlaceholder();
        Refresh();
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
        _svc.Logout();
        Toast(InfoBarSeverity.Success, P("Logged out.", "已登出。"));
        ShowDetailPlaceholder();
        Refresh();
    }

    // ===================== Password generator =====================

    private async void Gen_Click(object sender, RoutedEventArgs e)
    {
        var (dlg, getResult, regen) = BuildGeneratorDialog();
        regen();
        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            var pw = getResult();
            if (!string.IsNullOrEmpty(pw))
                CopySecret(pw, persistent: false,
                    P("Generated password copied — clipboard clears in 20s.", "已複製產生嘅密碼 — 20 秒後自動清除剪貼簿。"));
        }
    }

    private (ContentDialog dlg, Func<string> result, Action regenerate) BuildGeneratorDialog()
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

        void Regen() => outBox.Text = BitwardenService.Generate(Opts());

        regenBtn.Click += (_, _) => Regen();
        typeToggle.Toggled += (_, _) =>
        {
            passwordOpts.Visibility = typeToggle.IsOn ? Visibility.Collapsed : Visibility.Visible;
            passphraseOpts.Visibility = typeToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            Regen();
        };
        void OnAny(object s, RoutedEventArgs e) => Regen();
        foreach (var cb in new[] { upper, lower, nums, special, capitalize })
            cb.Click += OnAny;
        lengthSlider.ValueChanged += (_, _) => Regen();
        wordsSlider.ValueChanged += (_, _) => Regen();

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

    // ===================== Toast helper =====================

    private void Toast(InfoBarSeverity severity, string? message)
    {
        ToastBar.Severity = severity;
        ToastBar.Title = "";
        ToastBar.Message = message ?? "";
        ToastBar.IsOpen = true;
    }

    private static Brush? TryBrush(string key)
    {
        try { return Application.Current.Resources[key] as Brush; }
        catch { return null; }
    }
}
