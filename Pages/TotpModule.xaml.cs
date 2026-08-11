using System;
using QRCoder;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// TOTP/HOTP 驗證器 · RFC 6238 authenticator — enter a Base32 secret (or paste an
/// <c>otpauth://totp/...</c> URI), pick digits / period / algorithm and see the live code
/// with a countdown ring. Pure managed C# (System.Security.Cryptography). Bilingual, never throws.
/// </summary>
public sealed partial class TotpModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string _lastCode = "";
    private TotpAuthenticatorStore.PendingEntry? _pending;
    private string? _selectedEntryId;
    private bool _subscribed;
    private bool _clearingPairing;

    public TotpModule()
    {
        InitializeComponent();
        DigitsBox.Value = 6;
        PeriodBox.Value = 30;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            Loc.I.LanguageChanged += OnLang;
            EntrySearchBox.PatternChanged += EntrySearchBox_PatternChanged;
            TotpAuthenticatorStore.Changed += OnStoreChanged;
            _timer.Tick += OnTick;
            _subscribed = true;
        }
        if (AlgoBox.SelectedIndex < 0) AlgoBox.SelectedIndex = 0;
        Render();
        Refresh();
        RefreshEntries();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ClearPairingSurface(clearSecret: true);
        _lastCode = string.Empty;
        CodeText.Text = "— — —";
        _timer.Stop();
        if (_subscribed)
        {
            Loc.I.LanguageChanged -= OnLang;
            EntrySearchBox.PatternChanged -= EntrySearchBox_PatternChanged;
            TotpAuthenticatorStore.Changed -= OnStoreChanged;
            _timer.Tick -= OnTick;
            _subscribed = false;
        }
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        RefreshEntries();
    }

    private void OnStoreChanged(object? sender, EventArgs e) => DispatcherQueue.TryEnqueue(() =>
    {
        RefreshEntries();
        if (TotpAuthenticatorStore.LastHistoryWarning is string warning) StatusText.Text = warning;
    });

    private void EntrySearchBox_PatternChanged(object? sender, EventArgs e) => RefreshEntries();

    private void OnTick(object? sender, object e)
    {
        Refresh();
        RefreshEntries();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "TOTP Authenticator · TOTP 驗證器";
        HeaderBlurb.Text = P("Generate RFC 6238 time-based one-time codes from a Base32 secret — the same six-digit codes as Google Authenticator, Authy or Microsoft Authenticator. All local; nothing leaves this PC.",
            "由 Base32 密鑰計出 RFC 6238 時間型一次性驗證碼 — 同 Google Authenticator、Authy 或 Microsoft Authenticator 一樣嘅六位數。全部喺本機計，冇任何嘢送出電腦。");
        EntriesTitle.Text = P("Saved authenticator entries", "已儲存驗證器項目");
        EntriesBlurb.Text = P("Entries are named, searchable and reorderable. Metadata stays in local settings; each secret is held only by the Windows credential vault. The ordinary exports are deliberately redacted.",
            "項目可以命名、搜尋同排序。資料留喺本機設定；每個密鑰只會放喺 Windows credential vault。普通匯出會刻意刪走密鑰。");
        EntrySearchBox.PlaceholderText = P("Search issuer, account, group or status", "搜尋發行者、帳戶、群組或狀態");
        EntryCountText.Text = "";
        ExportJsonButton.Content = P("Export redacted JSON", "匯出已刪密鑰 JSON");
        ExportCsvButton.Content = P("Export redacted CSV", "匯出已刪密鑰 CSV");
        ExportDisclosure.Text = P("Exports include every visible metadata field and explicitly say that usable secrets were omitted. A separate clear-secret export is not provided by default.",
            "匯出會包括所有可見資料欄位，並清楚講明可用密鑰被省略。預設唔會提供明文密鑰匯出。");
        CodeLabel.Text = P("Current code", "目前驗證碼");
        CopyBtn.Content = P("Copy", "複製");
        SecretLabel.Text = P("Base32 secret", "Base32 密鑰");
        UriLabel.Text = P("otpauth:// URI (optional)", "otpauth:// 連結（可選）");
        ImportBtn.Content = P("Import", "匯入");
        PasteUriButton.Content = P("Paste URI from clipboard", "由剪貼簿貼上 URI");
        IssuerBox.Header = P("Issuer", "發行者");
        AccountBox.Header = P("Account", "帳戶");
        GroupBox.Header = P("Group (optional)", "群組（可選）");
        QrTitle.Text = P("Authenticator pairing QR", "驗證器配對 QR");
        QrBlurb.Text = P("Draw a standard otpauth://totp/ QR locally for your authenticator. Nothing is uploaded.",
            "喺本機畫標準 otpauth://totp/ QR 畀驗證器配對；唔會上載任何嘢。");
        QrButton.Content = P("Generate QR locally", "喺本機產生 QR");
        SaveMetadataButton.Content = P("Save selected entry metadata", "儲存所選項目資料");
        ClearPairingButton.Content = P("Clear pairing draft", "清除配對草稿");
        ConfirmCodeBox.Header = P("Current code confirmation", "目前驗證碼確認");
        ConfirmPairingButton.Content = P("Confirm pairing and save", "確認配對並儲存");
        QrStatusText.Text = P("Enter a valid secret, then choose Generate QR locally.", "輸入有效密鑰，再撳「喺本機產生 QR」。");
        DigitsLabel.Text = P("Digits", "位數");
        PeriodLabel.Text = P("Period (s)", "週期（秒）");
        AlgoLabel.Text = P("Algorithm", "演算法");
        Refresh();
    }

    private static TotpService.HashAlgo AlgoFromIndex(int index) => index switch
    {
        1 => TotpService.HashAlgo.Sha256,
        2 => TotpService.HashAlgo.Sha512,
        _ => TotpService.HashAlgo.Sha1,
    };

    private void Refresh()
    {
        try
        {
            int digits = (int)(double.IsNaN(DigitsBox.Value) ? 6 : DigitsBox.Value);
            int period = (int)(double.IsNaN(PeriodBox.Value) ? 30 : PeriodBox.Value);
            var algo = AlgoFromIndex(AlgoBox.SelectedIndex);
            string secret = SecretBox.Password ?? "";

            if (string.IsNullOrWhiteSpace(secret))
            {
                _lastCode = "";
                CodeText.Text = "— — —";
                CountText.Text = "—";
                CountRing.Value = 0;
                StatusText.Text = P("Enter a Base32 secret to start.", "輸入 Base32 密鑰就可以開始。");
                return;
            }

            if (TotpService.DecodeBase32(secret) is null)
            {
                _lastCode = "";
                CodeText.Text = "— — —";
                CountText.Text = "—";
                CountRing.Value = 0;
                StatusText.Text = P("Invalid Base32 secret — allowed characters are A–Z and 2–7.", "Base32 密鑰無效 — 只可以用 A–Z 同 2–7。");
                return;
            }

            long now = TotpService.UnixNow();
            string? code = TotpService.Compute(secret, digits, period, algo, now);
            if (code is null)
            {
                _lastCode = "";
                CodeText.Text = "— — —";
                StatusText.Text = P("Could not generate a code — check the parameters.", "無法產生驗證碼 — 請檢查參數。");
                return;
            }

            _lastCode = code;
            CodeText.Text = Spaced(code);

            int remaining = TotpService.SecondsRemaining(period, now);
            CountText.Text = remaining.ToString();
            CountRing.Value = period > 0 ? Math.Clamp(remaining * 100.0 / period, 0, 100) : 0;
            StatusText.Text = P($"Refreshes in {remaining}s · {digits} digits · {period}s step · {algo.ToString().ToUpperInvariant()}",
                $"{remaining} 秒後更新 · {digits} 位 · {period} 秒週期 · {algo.ToString().ToUpperInvariant()}");
        }
        catch
        {
            StatusText.Text = P("Something went wrong generating the code.", "產生驗證碼時發生錯誤。");
        }
    }

    private static string Spaced(string code)
    {
        if (code.Length == 6) return code.Substring(0, 3) + " " + code.Substring(3);
        if (code.Length == 8) return code.Substring(0, 4) + " " + code.Substring(4);
        return code;
    }

    private void Config_Changed(object sender, RoutedEventArgs e)
    {
        InvalidatePairingDraft();
        Refresh();
    }

    private void Number_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        InvalidatePairingDraft();
        Refresh();
    }

    private void Algo_Changed(object sender, SelectionChangedEventArgs e)
    {
        InvalidatePairingDraft();
        Refresh();
    }

    private void InvalidatePairingDraft()
    {
        if (_clearingPairing) return;
        _pending = null;
        ConfirmPairingButton.IsEnabled = false;
        QrImage.Source = null;
        UriBox.Text = string.Empty;
        QrStatusText.Text = P(
            "The pairing draft was cleared because its configuration changed. Generate a new QR before confirming.",
            "設定有變，配對草稿已清除；確認前請重新產生 QR。");
    }

    private void ClearPairingSurface(bool clearSecret)
    {
        _clearingPairing = true;
        try
        {
            _pending = null;
            ConfirmPairingButton.IsEnabled = false;
            QrImage.Source = null;
            UriBox.Text = string.Empty;
            ConfirmCodeBox.Text = string.Empty;
            if (clearSecret) SecretBox.Password = string.Empty;
        }
        finally
        {
            _clearingPairing = false;
        }
    }

    private void ClearPairing_Click(object sender, RoutedEventArgs e)
    {
        ClearPairingSurface(clearSecret: true);
        QrStatusText.Text = P("The pairing draft and secret input were cleared.", "配對草稿同密鑰輸入已清除。");
    }

    private void SaveMetadata_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedEntryId)) return;
        if (!TotpAuthenticatorStore.UpdateMetadata(
                _selectedEntryId,
                IssuerBox.Text,
                AccountBox.Text,
                string.Empty,
                GroupBox.Text,
                out string error))
        {
            StatusText.Text = error;
            return;
        }
        SaveMetadataButton.IsEnabled = true;
        StatusText.Text = P("Selected authenticator metadata saved without revealing its secret.",
            "所選驗證器資料已儲存，冇顯示密鑰。");
        RefreshEntries();
    }

    private static async void ClearClipboardLater(string code)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30));
            DataPackageView view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.Text)) return;
            string current = await view.GetTextAsync();
            if (string.Equals(current, code, StringComparison.Ordinal)) Clipboard.Clear();
        }
        catch
        {
            // Clipboard ownership/history is controlled by the operating system; failure to clear
            // it must not interrupt the authenticator surface.
        }
    }

    private async void PasteUri_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DataPackageView view = Clipboard.GetContent();
            if (!view.Contains(StandardDataFormats.Text))
            {
                StatusText.Text = P("The clipboard does not contain text.", "剪貼簿入面冇文字。");
                return;
            }
            UriBox.Text = await view.GetTextAsync();
            Import_Click(sender, e);
        }
        catch (Exception exception)
        {
            StatusText.Text = P("Could not read the clipboard.", "無法讀取剪貼簿。") + " " + exception.Message;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_lastCode))
            {
                StatusText.Text = P("No code to copy yet.", "暫時未有驗證碼可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(_lastCode);
            Clipboard.SetContent(pkg);
            ClearClipboardLater(_lastCode);
            StatusText.Text = P("Code copied to clipboard.", "驗證碼已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "無法複製到剪貼簿。");
        }
    }

    private async void GenerateQr_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int digits = (int)(double.IsNaN(DigitsBox.Value) ? 6 : DigitsBox.Value);
            int period = (int)(double.IsNaN(PeriodBox.Value) ? 30 : PeriodBox.Value);
            var algo = AlgoFromIndex(AlgoBox.SelectedIndex);
            _selectedEntryId = null;
            SaveMetadataButton.IsEnabled = false;
            _pending = TotpAuthenticatorStore.PrepareManual(
                SecretBox.Password,
                IssuerBox.Text,
                AccountBox.Text,
                digits,
                period,
                algo,
                AccountBox.Text,
                GroupBox.Text,
                out string prepareError);
            if (_pending is null)
            {
                QrStatusText.Text = prepareError;
                QrImage.Source = null;
                ConfirmPairingButton.IsEnabled = false;
                return;
            }

            string uri = BuildPendingUri(_pending);
            UriBox.Text = uri;

            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(data).GetGraphic(8);
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(png);
                await writer.StoreAsync();
                await writer.FlushAsync();
            }
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            QrImage.Source = bitmap;
            AutomationProperties.SetName(QrImage, P($"Local TOTP pairing QR for {_pending.Metadata.DisplayLabel}, {digits} digits every {period} seconds", $"{_pending.Metadata.DisplayLabel} 本機 TOTP 配對 QR，{digits} 位，每 {period} 秒"));
            QrStatusText.Text = P("QR generated locally. Scan it with your authenticator, then verify the current code.",
                "QR 已喺本機產生。用 authenticator 掃描，之後驗證目前驗證碼。");
            ConfirmPairingButton.IsEnabled = true;
        }
        catch (Exception exception)
        {
            _pending = null;
            ConfirmPairingButton.IsEnabled = false;
            QrImage.Source = null;
            SecretBox.Password = string.Empty;
            UriBox.Text = string.Empty;
            QrStatusText.Text = P("Could not draw the QR locally.", "無法喺本機畫出 QR。") + " " + exception.Message;
        }
    }

    private static string BuildPendingUri(TotpAuthenticatorStore.PendingEntry pending)
    {
        string label = string.IsNullOrWhiteSpace(pending.Metadata.Issuer)
            ? pending.Metadata.Account
            : pending.Metadata.Issuer + ":" + pending.Metadata.Account;
        return "otpauth://totp/" + Uri.EscapeDataString(label) +
            "?secret=" + Uri.EscapeDataString(pending.Secret) +
            "&issuer=" + Uri.EscapeDataString(pending.Metadata.Issuer) +
            "&algorithm=" + pending.Metadata.Algorithm.ToString().ToUpperInvariant() +
            "&digits=" + pending.Metadata.Digits +
            "&period=" + pending.Metadata.Period;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            TotpAuthenticatorStore.PendingEntry? pending = TotpAuthenticatorStore.PrepareFromUri(UriBox.Text, out string parseError);
            if (pending is null)
            {
                StatusText.Text = parseError;
                return;
            }

            SecretBox.Password = pending.Secret;
            IssuerBox.Text = pending.Metadata.Issuer;
            AccountBox.Text = pending.Metadata.Account;
            GroupBox.Text = string.Empty;
            DigitsBox.Value = pending.Metadata.Digits;
            PeriodBox.Value = pending.Metadata.Period;
            AlgoBox.SelectedIndex = pending.Metadata.Algorithm switch
            {
                TotpService.HashAlgo.Sha256 => 1,
                TotpService.HashAlgo.Sha512 => 2,
                _ => 0,
            };

            string who = pending.Metadata.DisplayLabel;
            StatusText.Text = string.IsNullOrEmpty(who)
                ? P("Imported from URI.", "已由連結匯入。")
                : P($"Imported \"{who}\" from URI.", $"已由連結匯入「{who}」。");
            Refresh();
        }
        catch
        {
            StatusText.Text = P("Could not import that URI.", "無法匯入呢個連結。");
        }
    }

    private void ConfirmPairing_Click(object sender, RoutedEventArgs e)
    {
        if (_pending is null)
        {
            QrStatusText.Text = P("Generate the QR again before confirming this pairing.", "確認配對前請重新產生 QR。");
            return;
        }

        if (!TotpAuthenticatorStore.ConfirmAndSave(
                _pending,
                ConfirmCodeBox.Text,
                TotpService.UnixNow(),
                out TotpAuthenticatorStore.Entry? saved,
                out string error))
        {
            QrStatusText.Text = error;
            return;
        }

        _pending = null;
        ConfirmCodeBox.Text = string.Empty;
        ConfirmPairingButton.IsEnabled = false;
        QrImage.Source = null;
        SecretBox.Password = string.Empty;
        UriBox.Text = string.Empty;
        SaveMetadataButton.IsEnabled = false;
        QrStatusText.Text = P($"Saved {saved!.DisplayLabel} after current-code confirmation.",
            $"已用目前驗證碼確認並儲存 {saved.DisplayLabel}。");
        RefreshEntries();
    }

    private void RefreshEntries()
    {
        if (EntriesPanel is null) return;
        EntriesPanel.Children.Clear();
        IReadOnlyList<TotpAuthenticatorStore.Entry> entries = TotpAuthenticatorStore.Entries;
        SearchPatternService.Matcher matcher = EntrySearchBox.CompileMatcher();
        int visible = 0;
        foreach (TotpAuthenticatorStore.Entry entry in entries)
        {
            SearchPatternService.MatchResult match = matcher.MatchAny(new[]
            {
                entry.DisplayLabel, entry.Issuer, entry.Account, entry.Group,
                entry.Algorithm.ToString(), entry.Digits.ToString(), entry.Period.ToString(),
            });
            if (!match.Ok || !match.IsMatch) continue;
            EntriesPanel.Children.Add(BuildEntryCard(entry));
            visible++;
        }

        EntryCountText.Text = P($"Showing {visible} of {entries.Count} saved entry(ies).",
            $"顯示緊 {entries.Count} 個已儲存項目入面嘅 {visible} 個。");
        EmptyEntriesText.Text = matcher.Error is not null
            ? matcher.Error
            : entries.Count == 0
                ? P("No authenticator entries yet. Start with a URI or Base32 secret below.",
                    "暫時冇驗證器項目；由下面嘅 URI 或 Base32 密鑰開始。")
                : P("No saved entries match this search.", "冇已儲存項目符合呢個搜尋。");
        EmptyEntriesText.Visibility = visible == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Border BuildEntryCard(TotpAuthenticatorStore.Entry entry)
    {
        long now = TotpService.UnixNow();
        string code = TotpAuthenticatorStore.TryGetCode(entry, now, out string value, out int remaining)
            ? Spaced(value)
            : "— — —";
        string group = string.IsNullOrWhiteSpace(entry.Group) ? P("Ungrouped", "未分組") : entry.Group;
        var title = new TextBlock
        {
            Text = entry.DisplayLabel,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var detail = new TextBlock
        {
            Text = P($"{group} · {entry.Digits} digits · {entry.Period}s · {entry.Algorithm.ToString().ToUpperInvariant()}",
                $"{group} · {entry.Digits} 位 · {entry.Period} 秒 · {entry.Algorithm.ToString().ToUpperInvariant()}"),
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        var codeText = new TextBlock
        {
            Text = code + (remaining > 0 ? $" · {remaining}s" : ""),
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            FontSize = 22,
            TextWrapping = TextWrapping.Wrap,
        };
        var select = new Button { Content = P("Use entry", "使用項目"), MinHeight = 40 };
        select.Click += (_, _) => SelectEntry(entry);
        var copy = new Button { Content = P("Copy code", "複製驗證碼"), MinHeight = 40 };
        copy.Click += (_, _) => CopySavedCode(entry);
        var remove = new Button { Content = P("Remove", "移除"), MinHeight = 40 };
        remove.Click += async (_, _) => await DeleteEntryAsync(entry);
        var actions = new StackPanel { Spacing = 8, Children = { select, copy, remove } };
        var content = new StackPanel { Spacing = 5, Children = { title, detail, codeText, actions } };
        var card = new Border
        {
            Padding = new Thickness(12),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = content,
        };
        AutomationProperties.SetName(codeText, P(
            $"Current code {code}, {remaining} seconds remaining",
            $"目前驗證碼 {code}，剩餘 {remaining} 秒"));
        AutomationProperties.SetName(card, P(
            $"Authenticator entry {entry.DisplayLabel}, {group}, {entry.Digits} digits every {entry.Period} seconds",
            $"驗證器項目 {entry.DisplayLabel}，{group}，{entry.Digits} 位，每 {entry.Period} 秒"));
        return card;
    }

    private void SelectEntry(TotpAuthenticatorStore.Entry entry)
    {
        _selectedEntryId = entry.Id;
        SecretBox.Password = string.Empty;
        IssuerBox.Text = entry.Issuer;
        AccountBox.Text = entry.Account;
        GroupBox.Text = entry.Group;
        DigitsBox.Value = entry.Digits;
        PeriodBox.Value = entry.Period;
        AlgoBox.SelectedIndex = entry.Algorithm switch
        {
            TotpService.HashAlgo.Sha256 => 1,
            TotpService.HashAlgo.Sha512 => 2,
            _ => 0,
        };
        _pending = null;
        ConfirmPairingButton.IsEnabled = false;
        SaveMetadataButton.IsEnabled = true;
        StatusText.Text = P($"Selected {entry.DisplayLabel}. The secret remains in the Windows credential vault and was not copied into the editor.",
            $"已揀選 {entry.DisplayLabel}。密鑰仍然留喺 Windows credential vault，冇複製入編輯器。");
    }

    private void CopySavedCode(TotpAuthenticatorStore.Entry entry)
    {
        try
        {
            if (!TotpAuthenticatorStore.TryGetCode(entry, TotpService.UnixNow(), out string code, out _))
            {
                StatusText.Text = P("The credential vault did not return a code.", "credential vault 冇提供驗證碼。");
                return;
            }
            var package = new DataPackage();
            package.SetText(code);
            Clipboard.SetContent(package);
            ClearClipboardLater(code);
            StatusText.Text = P("Saved-entry code copied.", "已複製已儲存項目嘅驗證碼。");
        }
        catch (Exception exception)
        {
            StatusText.Text = P("Could not copy the saved-entry code.", "無法複製已儲存項目嘅驗證碼。") + " " + exception.Message;
        }
    }

    private async Task DeleteEntryAsync(TotpAuthenticatorStore.Entry entry)
    {
        string keyOne = "DELETE";
        string keyTwo = entry.Id[..Math.Min(8, entry.Id.Length)].ToUpperInvariant();
        var first = new TextBox { Header = P("Key 1", "第一條匙"), PlaceholderText = keyOne, MaxLength = keyOne.Length };
        var second = new TextBox { Header = P("Key 2", "第二條匙"), PlaceholderText = keyTwo, MaxLength = keyTwo.Length };
        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, IsEnabled = false };
        var panel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = P($"This removes {entry.DisplayLabel} and its vault credential. It cannot be undone from this screen.",
                        $"呢個動作會移除 {entry.DisplayLabel} 同佢嘅 vault 憑證；呢個畫面冇得復原。"),
                },
                first, second, slider,
            },
        };
        void UpdateSlider(object? sender, TextChangedEventArgs args)
            => slider.IsEnabled = string.Equals(first.Text, keyOne, StringComparison.Ordinal) &&
                                  string.Equals(second.Text, keyTwo, StringComparison.Ordinal);
        first.TextChanged += UpdateSlider;
        second.TextChanged += UpdateSlider;
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Confirm authenticator removal", "確認移除驗證器項目"),
            Content = panel,
            PrimaryButtonText = P("Remove", "移除"),
            CloseButtonText = P("Emergency exit", "緊急離開"),
            DefaultButton = ContentDialogButton.Close,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!slider.IsEnabled || slider.Value < 100) args.Cancel = true;
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (!TotpAuthenticatorStore.Delete(entry.Id, out string error))
        {
            StatusText.Text = error;
            return;
        }
        if (_selectedEntryId == entry.Id) _selectedEntryId = null;
        RefreshEntries();
    }

    private async void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        string? path = await FileDialogs.SaveFileAsync("winforge-authenticator-redacted", ".json");
        if (string.IsNullOrWhiteSpace(path)) return;
        StatusText.Text = TotpAuthenticatorStore.ExportRedactedJson(path, out string error)
            ? P("Redacted JSON exported; usable secrets were omitted.", "已匯出刪密鑰 JSON；可用密鑰已省略。")
            : error;
    }

    private async void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        string? path = await FileDialogs.SaveFileAsync("winforge-authenticator-redacted", ".csv");
        if (string.IsNullOrWhiteSpace(path)) return;
        StatusText.Text = TotpAuthenticatorStore.ExportRedactedCsv(path, out string error)
            ? P("Redacted CSV exported; usable secrets were omitted.", "已匯出刪密鑰 CSV；可用密鑰已省略。")
            : error;
    }
}
