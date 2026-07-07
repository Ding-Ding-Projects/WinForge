using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI; // Windows.UI.Color — qualify to avoid the Microsoft.UI.Colors ambiguity
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 佔位圖產生器 · Placeholder-image generator. Turns width/height/colours/label into a self-contained
/// SVG, a base64 data URI and a picsum-style reference URL, with a live in-app preview drawn from real
/// controls (Border + centred TextBlock). Pure managed C#, no remote fetch. Bilingual (粵語). Never throws.
/// </summary>
public sealed partial class LoremImgModule : Page
{
    private LoremImgService.Result _last;

    public LoremImgModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { RenderText(); Render(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => RenderText();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void RenderText()
    {
        try
        {
            Header.Title = P("Placeholder Image · 佔位圖", "佔位圖 · Placeholder Image");
            HeaderBlurb.Text = P(
                "Generate a self-contained SVG placeholder — pick a size, colours and a label, then copy the SVG, a base64 data URI, or save a .svg file. All local, no network.",
                "整一張自成一體嘅 SVG 佔位圖 — 揀尺寸、顏色同標籤，然後複製 SVG、base64 資料 URI，或者存做 .svg 檔。全部喺本機做，唔使上網。");
            InputsTitle.Text = P("Settings", "設定");
            WidthLabel.Text = P("Width (px)", "闊度（像素）");
            HeightLabel.Text = P("Height (px)", "高度（像素）");
            FontLabel.Text = P("Font size (0 = auto)", "字型大小（0 = 自動）");
            BgLabel.Text = P("Background colour (hex)", "背景顏色（十六進位）");
            FgLabel.Text = P("Text colour (hex)", "文字顏色（十六進位）");
            LabelLabel.Text = P("Label (blank = W×H)", "標籤（留空 = 闊×高）");
            PreviewTitle.Text = P("Live preview", "即時預覽");
            PreviewNote.Text = P("Preview is scaled to fit; the SVG uses your exact pixel size.",
                "預覽會縮放至適合大細；SVG 用返你設定嘅實際像素尺寸。");
            CopySvgBtn.Content = P("Copy SVG", "複製 SVG");
            CopyUriBtn.Content = P("Copy data URI", "複製資料 URI");
            SaveBtn.Content = P("Save .svg…", "存做 .svg…");
            SvgTitle.Text = P("SVG source", "SVG 原始碼");
            UriTitle.Text = P("Data URI (base64)", "資料 URI（base64）");
            PicsumTitle.Text = P("Picsum-style reference URL", "Picsum 式參考網址");
        }
        catch { }
    }

    private void Input_Changed(object sender, object e) => Render();

    private LoremImgService.Spec CurrentSpec()
    {
        int w = ToInt(WidthBox.Value, 640);
        int h = ToInt(HeightBox.Value, 480);
        double fs = double.IsNaN(FontBox.Value) ? 0 : FontBox.Value;
        return new LoremImgService.Spec(w, h, BgBox.Text, FgBox.Text, LabelBox.Text, fs);
    }

    private void Render()
    {
        try
        {
            var spec = CurrentSpec();
            var r = LoremImgService.Build(spec);
            _last = r;

            SvgBox.Text = r.Svg;
            UriBox.Text = r.DataUri;
            PicsumBox.Text = r.PicsumUrl;

            // Live preview — draw with real controls (approximate the SVG).
            PreviewBox.Background = new SolidColorBrush(FromArgb(r.BgArgb));
            PreviewText.Foreground = new SolidColorBrush(FromArgb(r.FgArgb));

            int w = Math.Max(1, spec.Width < 1 ? 640 : spec.Width);
            int h = Math.Max(1, spec.Height < 1 ? 480 : spec.Height);
            PreviewText.Text = string.IsNullOrWhiteSpace(spec.Label) ? (w + "×" + h) : spec.Label.Trim();

            // Fit the preview box within a 360×260 area, preserving aspect ratio.
            const double maxW = 360, maxH = 260;
            double scale = Math.Min(maxW / w, maxH / h);
            if (scale > 1) scale = 1; // don't upscale tiny images past native px
            double pw = Math.Max(24, Math.Round(w * scale));
            double ph = Math.Max(24, Math.Round(h * scale));
            PreviewBox.Width = pw;
            PreviewBox.Height = ph;

            double fs = spec.FontSize;
            if (double.IsNaN(fs) || fs <= 0) fs = Math.Max(10, Math.Min(w, h) / 6.0);
            PreviewText.FontSize = Math.Clamp(fs * scale, 6, 200);
        }
        catch { /* never throw from the UI */ }
    }

    private async void CopySvg_Click(object sender, RoutedEventArgs e)
    {
        if (CopyToClipboard(_last.Svg))
            Notify(P("SVG copied to clipboard.", "SVG 已複製到剪貼簿。"), InfoBarSeverity.Success);
        await System.Threading.Tasks.Task.CompletedTask;
    }

    private void CopyUri_Click(object sender, RoutedEventArgs e)
    {
        if (CopyToClipboard(_last.DataUri))
            Notify(P("Data URI copied to clipboard.", "資料 URI 已複製到剪貼簿。"), InfoBarSeverity.Success);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var spec = CurrentSpec();
            int w = spec.Width < 1 ? 640 : spec.Width;
            int h = spec.Height < 1 ? 480 : spec.Height;
            string suggested = $"placeholder-{w}x{h}.svg";
            var path = await FileDialogs.SaveFileAsync(suggested, ".svg");
            if (string.IsNullOrEmpty(path)) return;
            await System.IO.File.WriteAllTextAsync(path, _last.Svg ?? string.Empty, System.Text.Encoding.UTF8);
            Notify(P("Saved: ", "已儲存：") + path, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Notify(P("Could not save the file. ", "儲存失敗。") + ex.Message, InfoBarSeverity.Error);
        }
    }

    // ===== helpers =====

    private bool CopyToClipboard(string? text)
    {
        try
        {
            var dp = new DataPackage();
            dp.SetText(text ?? string.Empty);
            Clipboard.SetContent(dp);
            return true;
        }
        catch (Exception ex)
        {
            Notify(P("Clipboard is unavailable. ", "剪貼簿唔用得。") + ex.Message, InfoBarSeverity.Error);
            return false;
        }
    }

    private void Notify(string message, InfoBarSeverity severity)
    {
        try
        {
            Info.Message = message;
            Info.Severity = severity;
            Info.IsOpen = true;
        }
        catch { }
    }

    private static Color FromArgb(uint argb)
        => Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb);

    private static int ToInt(double v, int fallback)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
        return (int)Math.Round(v);
    }
}
