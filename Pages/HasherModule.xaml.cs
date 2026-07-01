using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 雜湊與校驗和 · Text &amp; file hasher. Computes MD5/SHA1/SHA256/SHA384/SHA512 + a hand-rolled CRC32
/// over typed text or a streamed file, with an optional HMAC (keyed) mode and an expected-hash
/// verifier. Every string is bilingual (Loc.I.Pick). All IO is wrapped in try/catch — errors go to
/// a status TextBlock, never to the UI. Mirrors AwakeModule's structure. No redirect, pure managed.
/// </summary>
public sealed partial class HasherModule : Page
{
    // The most recent computed set (text or file), used by the verifier.
    private HasherService.HashSet? _current;
    private bool _suppress;

    private static readonly string[] Algs = { "MD5", "SHA-1", "SHA-256", "SHA-384", "SHA-512", "CRC32" };

    public HasherModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await RecomputeTextAsync(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("Hash & Checksum · 雜湊與校驗和", "雜湊與校驗和 · Hash & Checksum");
        HeaderBlurb.Text = P("Compute MD5, SHA-1, SHA-256/384/512 and CRC32 for any text or file, verify a download against an expected hash, or switch on keyed HMAC — all offline.",
            "為任何文字或者檔案計 MD5、SHA-1、SHA-256/384/512 同 CRC32，用預期雜湊核對下載嘅嘢，或者開 HMAC 金鑰模式 — 全程離線。");

        TextCardTitle.Text = P("Hash text", "雜湊文字");
        InputBox.PlaceholderText = P("Type or paste text to hash…", "打字或者貼上要雜湊嘅文字…");
        EncodingLabel.Text = P("Encoding", "編碼");
        HmacTitle.Text = P("HMAC (keyed) — swaps SHA-256/512 for HMAC", "HMAC 金鑰模式 — SHA-256/512 換成 HMAC");
        HmacKeyBox.PlaceholderText = P("HMAC secret key", "HMAC 密鑰");

        FileCardTitle.Text = P("Hash a file", "雜湊檔案");
        PickFileBtn.Content = P("Pick file…", "揀檔案…");

        VerifyTitle.Text = P("Verify against an expected hash", "核對預期雜湊");
        ExpectedBox.PlaceholderText = P("Paste an expected hash (any algorithm)…", "貼上預期雜湊（任何演算法）…");

        ResultsTitle.Text = P("Results", "結果");
        UpdateVerify();
        RenderResults();
    }

    // ===== Text path =====

    private async void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        await RecomputeTextAsync();
    }

    private async void Options_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        HmacKeyBox.Visibility = HmacSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
        await RecomputeTextAsync();
    }

    private HasherService.TextEncodingKind SelectedEncoding() => EncodingBox.SelectedIndex switch
    {
        1 => HasherService.TextEncodingKind.Utf16,
        2 => HasherService.TextEncodingKind.Ascii,
        _ => HasherService.TextEncodingKind.Utf8,
    };

    private async Task RecomputeTextAsync()
    {
        try
        {
            string text = InputBox.Text ?? "";
            string? key = HmacSwitch.IsOn ? HmacKeyBox.Text : null;
            _current = await HasherService.HashTextAsync(text, SelectedEncoding(), key);
            StatusText.Text = P("Hashed the text above.", "已雜湊上面嘅文字。");
        }
        catch (Exception ex)
        {
            _current = null;
            StatusText.Text = P("Could not hash the text: ", "雜湊唔到文字：") + ex.Message;
        }
        RenderResults();
        UpdateVerify();
    }

    // ===== File path =====

    private async void PickFile_Click(object sender, RoutedEventArgs e)
    {
        string? path;
        try
        {
            path = await FileDialogs.OpenFileAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not open the file dialog: ", "開唔到檔案對話框：") + ex.Message;
            return;
        }
        if (string.IsNullOrEmpty(path)) return;

        _suppress = true;
        PickFileBtn.IsEnabled = false;
        Working.IsActive = true;
        StatusText.Text = P("Hashing file… this runs in the background.", "正在雜湊檔案…喺背景執行。");
        FileInfoText.Visibility = Visibility.Collapsed;
        _suppress = false;

        try
        {
            var res = await HasherService.HashFileAsync(path);
            _current = res.Hashes;
            FileInfoText.Text = $"{res.Name}  ·  {HasherService.FormatSize(res.Size)}";
            FileInfoText.Visibility = Visibility.Visible;
            StatusText.Text = P("Hashed the selected file.", "已雜湊揀咗嘅檔案。");
        }
        catch (Exception ex)
        {
            _current = null;
            StatusText.Text = P("Could not hash the file: ", "雜湊唔到檔案：") + ex.Message;
        }
        finally
        {
            Working.IsActive = false;
            PickFileBtn.IsEnabled = true;
            RenderResults();
            UpdateVerify();
        }
    }

    // ===== Results rendering =====

    private void RenderResults()
    {
        ResultsPanel.Children.Clear();
        if (_current is null)
        {
            var empty = new TextBlock
            {
                Text = P("Nothing to show yet.", "暫時未有結果。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                FontSize = 12,
            };
            ResultsPanel.Children.Add(empty);
            return;
        }

        string[] values =
        {
            _current.Md5, _current.Sha1, _current.Sha256,
            _current.Sha384, _current.Sha512, _current.Crc32,
        };

        for (int i = 0; i < Algs.Length; i++)
            ResultsPanel.Children.Add(BuildRow(Algs[i], values[i]));
    }

    private UIElement BuildRow(string label, string value)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var name = new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 0);

        var hex = new TextBox
        {
            Text = value,
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(hex, 1);

        var copy = new Button
        {
            Content = P("Copy", "複製"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        copy.Click += (_, _) =>
        {
            try
            {
                var pkg = new DataPackage();
                pkg.SetText(value);
                Clipboard.SetContent(pkg);
                StatusText.Text = P($"Copied {label}.", $"已複製 {label}。");
            }
            catch (Exception ex)
            {
                StatusText.Text = P("Could not copy: ", "複製唔到：") + ex.Message;
            }
        };
        Grid.SetColumn(copy, 2);

        grid.Children.Add(name);
        grid.Children.Add(hex);
        grid.Children.Add(copy);
        grid.Margin = new Thickness(0, 0, 0, 2);
        return grid;
    }

    // ===== Verify =====

    private void UpdateVerify()
    {
        string expected = ExpectedBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(expected))
        {
            VerifyStatus.Text = P("Enter an expected hash to check a match.", "輸入預期雜湊嚟核對。");
            VerifyStatus.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            return;
        }
        if (_current is null)
        {
            VerifyStatus.Text = P("No computed hashes to compare against yet.", "暫時未有計出嘅雜湊可以比對。");
            VerifyStatus.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            return;
        }

        bool match = HasherService.Matches(_current, expected);
        if (match)
        {
            VerifyStatus.Text = P("Match ✓ — the expected hash matches a computed hash.", "對到 ✓ — 預期雜湊同計出嘅其中一個一樣。");
            VerifyStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
        }
        else
        {
            VerifyStatus.Text = P("No match ✗ — the expected hash does not match any computed hash.", "唔對 ✗ — 預期雜湊同任何計出嘅都唔一樣。");
            VerifyStatus.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
        }
    }
}
