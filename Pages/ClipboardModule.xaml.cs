using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 剪貼簿歷史（背景監察）· Clipboard history — text, images and copied files captured by the background
/// monitor; copy back, delete, and auto-convert (images via imaging, media via ffmpeg). Bilingual.
/// </summary>
public sealed partial class ClipboardModule : Page
{
    private static readonly string GlyphText = ((char)0xE8C1).ToString();
    private static readonly string GlyphImage = ((char)0xEB9F).ToString();
    private static readonly string GlyphFiles = ((char)0xE8B7).ToString();
    private static readonly string GlyphCopy = ((char)0xE8C8).ToString();
    private static readonly string GlyphPin = ((char)0xE718).ToString();
    private static readonly string GlyphDelete = ((char)0xE74D).ToString();
    private static readonly string GlyphQr = ((char)0xED14).ToString();      // QR code glyph
    private static readonly string GlyphPlain = ((char)0xE8E9).ToString();   // "paste as plain" (Font)

    private static readonly string[] MediaExt =
        { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma",
          ".mp4", ".mkv", ".mov", ".avi", ".webm", ".wmv", ".flv" };

    public ClipboardModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Build(); ClipboardService.Changed += OnChanged; };
        Unloaded += (_, _) => { ClipboardService.Changed -= OnChanged; Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Build(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnChanged() => DispatcherQueue.TryEnqueue(Build);

    private void Render()
    {
        Header.Title = "Clipboard Â· å‰ªè²¼ç°¿";
        HeaderBlurb.Text = P("Everything you copy â€” text, images and files â€” kept here automatically. Click to copy back, paste as plain text, make a QR code, or convert images and media to another format.",
            "ä½ è¤‡è£½éŽå˜…å˜¢ â€” æ–‡å­—ã€åœ–ç‰‡åŒæª”æ¡ˆ â€” è‡ªå‹•ç•™å–ºåº¦ã€‚æ’³ä¸€ä¸‹è¤‡è£½è¿”ã€è²¼ç‚ºç´”æ–‡å­—ã€æ•´ QR ç¢¼ï¼Œæˆ–è€…å°‡åœ–ç‰‡åŒåª’é«”è½‰åšå¦ä¸€ç¨®æ ¼å¼ã€‚");
        QrText.Text = P("QR from clipboard", "剪貼簿整 QR");
        ClearText.Text = P("Clear all", "æ¸…é™¤å…¨éƒ¨");
        BgBar.Title = P("Running in the background", "å–ºèƒŒæ™¯é‹è¡Œç·Š");
        BgBar.Message = P("The monitor keeps capturing even when the window is closed to the tray. Right-click the tray icon to Quit.",
            "å°±ç®—é—œçª—æ”¶å…¥ç³»çµ±åŒ£ï¼Œç›£å¯Ÿéƒ½æœƒç¹¼çºŒæ•æ‰ã€‚å³éµç³»çµ±åŒ£åœ–ç¤ºå°±å¯ä»¥çµæŸã€‚");
        BgBar.Title = P("Running in the background", "喺背景運行緊");
        BgBar.Message = P("The monitor keeps capturing even when the window is closed to the tray. Right-click the tray icon to Quit.",
            "就算關窗收入系統匣，監察都會繼續捕捉。右鍵系統匣圖示就可以結束。");

        // Run-on-startup toggle (launches WinForge minimized to the tray at login).
        var startup = new ToggleSwitch
        {
            OnContent = P("Run on startup", "開機自動執行"),
            OffContent = P("Run on startup", "開機自動執行"),
            Margin = new Thickness(0, 6, 0, 0),
        };
        startup.IsOn = StartupManager.IsSelfStartupEnabled();   // set BEFORE wiring so this doesn't fire the handler
        startup.Toggled += (_, _) =>
        {
            try { StartupManager.SetSelfStartup(startup.IsOn); } catch { /* best effort */ }
        };
        BgBar.Content = startup;
    }

    private void Build()
    {
        Root.Children.Clear();
        if (ClipboardService.History.Count == 0)
        {
            Root.Children.Add(new TextBlock
            {
                Text = P("Nothing captured yet — copy some text, an image or a file.", "暫時冇捕捉到 — 複製啲文字、圖片或者檔案吖。"),
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                Margin = new Thickness(4, 8, 0, 0),
            });
            return;
        }
        foreach (var item in ClipboardService.History.ToList())
            Root.Children.Add(Card(item));
    }

    private Border Card(ClipItem item)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var kindGlyph = item.Kind switch { ClipKind.Image => GlyphImage, ClipKind.Files => GlyphFiles, _ => GlyphText };
        grid.Children.Add(Col(new FontIcon { Glyph = kindGlyph, FontSize = 16, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) }, 0));

        var content = new StackPanel { Spacing = 4 };
        if (item.Kind == ClipKind.Image && File.Exists(item.ImagePath))
        {
            // 用 UriSource（而唔係 new BitmapImage(uri)）避免 WinUI 靜靜吞咗載入錯誤，PNG 先顯示得到。
            // Assign UriSource instead of the (uri) constructor so WinUI doesn't swallow load errors silently.
            var bmp = new BitmapImage { DecodePixelWidth = 180, UriSource = new Uri(item.ImagePath) };
            content.Children.Add(new Image { Source = bmp, MaxHeight = 90, HorizontalAlignment = HorizontalAlignment.Left, Stretch = Stretch.Uniform });
        }
        else
        {
            content.Children.Add(new TextBlock
            {
                Text = item.Preview,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 4,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        content.Children.Add(new TextBlock { Text = item.Time, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });
        grid.Children.Add(Col(content, 1));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Top };

        var copyBtn = new Button { Padding = new Thickness(8, 3, 8, 3), Content = new FontIcon { Glyph = GlyphCopy, FontSize = 12 } };
        ToolTipService.SetToolTip(copyBtn, P("Copy back", "複製返"));
        copyBtn.Click += (_, _) => { ClipboardService.CopyBack(item); Notify(InfoBarSeverity.Success, P("Copied to clipboard", "已複製到剪貼簿"), ""); };
        actions.Children.Add(copyBtn);

        var pinBtn = new Button
        {
            Padding = new Thickness(8, 3, 8, 3),
            Content = new FontIcon { Glyph = GlyphPin, FontSize = 12 },
        };
        ToolTipService.SetToolTip(pinBtn, item.Pinned ? P("Unpin", "取消釘選") : P("Pin", "釘選"));
        pinBtn.Click += (_, _) =>
        {
            ClipboardService.TogglePin(item);
            Notify(InfoBarSeverity.Success,
                item.Pinned ? P("Pinned", "已釘選") : P("Unpinned", "已取消釘選"),
                "");
        };
        actions.Children.Add(pinBtn);

        // Text & file items get "paste as plain text" + "make QR" (encode the text/paths locally).
        if (item.Kind == ClipKind.Text || item.Kind == ClipKind.Files)
        {
            var plain = new Button { Padding = new Thickness(8, 3, 8, 3), Content = new FontIcon { Glyph = GlyphPlain, FontSize = 12 } };
            ToolTipService.SetToolTip(plain, P("Paste as plain text", "貼為純文字"));
            plain.Click += (_, _) =>
            {
                ClipboardService.CopyPlainText(QrPayload(item));
                Notify(InfoBarSeverity.Success, P("Copied as plain text", "已複製做純文字"), P("Formatting stripped — paste anywhere.", "已剝走格式 — 隨處貼上。"));
            };
            actions.Children.Add(plain);

            var qr = new Button { Padding = new Thickness(8, 3, 8, 3), Content = new FontIcon { Glyph = GlyphQr, FontSize = 12 } };
            ToolTipService.SetToolTip(qr, P("Make QR code", "整 QR 碼"));
            qr.Click += async (_, _) => await ShowQrDialog(QrPayload(item));
            actions.Children.Add(qr);
        }

        if (item.Kind == ClipKind.Image)
        {
            var fmt = new ComboBox { MinWidth = 92 };
            foreach (var f in new[] { "PNG", "JPG", "BMP", "GIF" }) fmt.Items.Add(f);
            fmt.SelectedIndex = 1;
            actions.Children.Add(fmt);
            var save = new Button { Padding = new Thickness(8, 3, 8, 3), Content = P("Save", "儲存") };
            save.Click += async (_, _) =>
            {
                try
                {
                    var ext = "." + (fmt.SelectedItem as string ?? "PNG").ToLowerInvariant();
                    var outp = await ClipboardService.SaveImageAs(item, ext);
                    Notify(InfoBarSeverity.Success, P("Saved", "已儲存"), outp);
                }
                catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
            };
            actions.Children.Add(save);
        }
        else if (item.Kind == ClipKind.Files && item.Files.Any(IsMedia))
        {
            var target = item.Files.First(IsMedia);
            var fmt = new ComboBox { MinWidth = 92 };
            foreach (var f in new[] { "mp3", "wav", "flac", "m4a", "mp4", "mkv", "gif" }) fmt.Items.Add(f);
            fmt.SelectedIndex = 0;
            actions.Children.Add(fmt);
            var conv = new Button { Padding = new Thickness(8, 3, 8, 3), Content = P("Convert", "轉檔") };
            conv.Click += async (_, _) =>
            {
                // Media auto-convert needs ffmpeg. If it's missing, offer a first-class in-place install
                // (real progress bar + live status + % + Cancel + animation) instead of a dead-end warning.
                if (!MediaService.IsInstalled)
                {
                    await PromptInstallFfmpegAsync();
                    if (!MediaService.IsInstalled) return; // user cancelled or install failed
                }
                var ext = "." + (fmt.SelectedItem as string ?? "mp3");
                var outp = Path.Combine(Path.GetDirectoryName(target) ?? ".", Path.GetFileNameWithoutExtension(target) + "-wt" + ext);
                Notify(InfoBarSeverity.Informational, P("Converting…", "轉緊…"), Path.GetFileName(outp));
                var r = await ShellRunner.Run(MediaService.FFmpeg, $"-y -i \"{target}\" \"{outp}\"", false, CancellationToken.None);
                Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
                    r.Success ? P("Converted", "已轉檔") : P("Convert failed", "轉檔失敗"), outp);
            };
            actions.Children.Add(conv);
        }

        var del = new Button { Padding = new Thickness(8, 3, 8, 3), Content = new FontIcon { Glyph = GlyphDelete, FontSize = 12 } };
        ToolTipService.SetToolTip(del, P("Delete", "刪除"));
        del.Click += (_, _) => ClipboardService.Remove(item);
        actions.Children.Add(del);

        grid.Children.Add(Col(actions, 2));

        return new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClipboardService.Clear();

    private async void MakeQrFromClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = await ReadClipboardTextOrUrlAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                Notify(InfoBarSeverity.Warning, P("No text or URL on the clipboard", "剪貼簿冇文字或者網址"), "");
                return;
            }
            await ShowQrDialog(text);
        }
        catch (Exception ex)
        {
            Notify(InfoBarSeverity.Error, P("Could not make QR code", "整唔到 QR 碼"), ex.Message);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClipboardService.Clear();

    private async void MakeQrFromClipboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = await ReadClipboardTextOrUrlAsync();
            if (string.IsNullOrWhiteSpace(text))
            {
                Notify(InfoBarSeverity.Warning, P("No text or URL on the clipboard", "剪貼簿冇文字或者網址"), "");
                return;
            }
            await ShowQrDialog(text);
        }
        catch (Exception ex)
        {
            Notify(InfoBarSeverity.Error, P("Could not make QR code", "整唔到 QR 碼"), ex.Message);
        }
    }

    /// <summary>The text payload to encode/copy for a given item (text body, or newline-joined file paths).</summary>
    private static string QrPayload(ClipItem item) => item.Kind switch
    {
        ClipKind.Text => item.Text,
        ClipKind.Files => string.Join(Environment.NewLine, item.Files),
        _ => "",
    };

    private static async System.Threading.Tasks.Task<string> ReadClipboardTextOrUrlAsync()
    {
        try
        {
            var view = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.WebLink))
            {
                var link = await view.GetWebLinkAsync();
                if (link is not null) return link.ToString();
            }
            if (view.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
                return await view.GetTextAsync();
        }
        catch { }
        return "";
    }

    /// <summary>Generate a QR code locally and show it in a dialog with Save-PNG / Copy-to-clipboard. No network.</summary>
    private async System.Threading.Tasks.Task ShowQrDialog(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Notify(InfoBarSeverity.Warning, P("Nothing to encode", "å†‡å˜¢å¯ä»¥ç·¨ç¢¼"), "");
            return;
        }
        // QR (alphanumeric/byte) tops out around ~2.9 KB; guard so we give a friendly message.
        if (text.Length > 2900)
        {
            Notify(InfoBarSeverity.Warning, P("Too much text for a QR code", "æ–‡å­—å¤ªå¤šï¼Œæ•´å””åˆ° QR ç¢¼"),
                P("QR codes hold roughly 2,900 characters. Shorten the text and try again.", "QR ç¢¼å¤§ç´„åªèƒ½æ”¾ 2,900 å€‹å­—å…ƒã€‚è«‹ç¸®çŸ­æ–‡å­—å†è©¦ã€‚"));
            return;
        }

        byte[] png;
        try { png = ClipboardService.GenerateQrPng(text); }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Could not make QR code", "æ•´å””åˆ° QR ç¢¼"), ex.Message); return; }

        var image = new Image { Stretch = Stretch.Uniform, MaxHeight = 320, MaxWidth = 320 };
        var bmp = new BitmapImage();
        using (var ms = new MemoryStream(png))
        using (var ras = ms.AsRandomAccessStream())
            await bmp.SetSourceAsync(ras);
        image.Source = bmp;

        var caption = new TextBlock
        {
            Text = text.Length > 120 ? text.Substring(0, 120) + "â€¦" : text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxLines = 3,
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            Margin = new Thickness(0, 10, 0, 0),
        };

        var panel = new StackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        panel.Children.Add(image);
        panel.Children.Add(caption);

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("QR code Â· QR ç¢¼", "QR ç¢¼ Â· QR code"),
            Content = panel,
            PrimaryButtonText = P("Save PNGâ€¦", "å„²å­˜ PNGâ€¦"),
            SecondaryButtonText = P("Copy image", "è¤‡è£½åœ–ç‰‡"),
            CloseButtonText = P("Close", "é—œé–‰"),
            DefaultButton = ContentDialogButton.Primary,
        };

        // Keep the dialog open after the action so the user can do both Save and Copy.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            try { var p = ClipboardService.SaveQrPng(text); Notify(InfoBarSeverity.Success, P("Saved", "å·²å„²å­˜"), p); }
            catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Failed", "å¤±æ•—"), ex.Message); }
        };
        dialog.SecondaryButtonClick += (_, args) =>
        {
            args.Cancel = true;
            try { ClipboardService.CopyQrToClipboard(text); Notify(InfoBarSeverity.Success, P("QR copied to clipboard", "QR ç¢¼å·²è¤‡è£½åˆ°å‰ªè²¼ç°¿"), ""); }
            catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Failed", "å¤±æ•—"), ex.Message); }
        };

        await dialog.ShowAsync();
    }

    /// <summary>
    /// 缺 ffmpeg 時彈出即場安裝流程 · When ffmpeg is missing, pop a dialog hosting the rich install control
    /// (real progress bar + live bilingual status + % + Cancel + success/error animation) so the user can
    /// install it (winget Gyan.FFmpeg) without leaving the module, then retry the conversion. Never throws.
    /// </summary>
    private async System.Threading.Tasks.Task PromptInstallFfmpegAsync()
    {
        try
        {
            var install = WinForge.Services.EngineBars.AutoInstallProgress(
                "Gyan.FFmpeg", P("Install ffmpeg automatically", "自動安裝 ffmpeg"),
                P("Install ffmpeg automatically", "自動安裝 ffmpeg"),
                rescan: MediaService.Rescan);

            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(new TextBlock
            {
                Text = P("ffmpeg is required to convert this media file. Install it with live progress — no restart needed.",
                         "轉呢個媒體檔需要 ffmpeg。即時睇住進度自動安裝 — 唔使重開。"),
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(install);

            var dialog = new ContentDialog
            {
                XamlRoot = this.XamlRoot,
                Title = P("ffmpeg not found", "搵唔到 ffmpeg"),
                Content = panel,
                CloseButtonText = P("Close", "關閉"),
                DefaultButton = ContentDialogButton.Close,
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
