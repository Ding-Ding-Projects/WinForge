using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI; // Color.FromArgb (bare Color would be ambiguous under Microsoft.UI)
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 條碼產生器 · 1D barcode generator — Code 128 (auto B/C + checksum), Code 39 (start/stop *) and EAN-13
/// (guards, parity, mod-10 check). Encoders are hand-rolled in <see cref="BarcodeService"/> (no NuGet). Draws
/// the barcode live as Rectangles into a Grid, produces a self-contained SVG you can copy to the clipboard,
/// and saves a .svg via <see cref="FileDialogs"/>. Fully bilingual; robust; never throws.
/// </summary>
public sealed partial class BarcodeModule : Page
{
    private const double ModuleWidth = 2.0;   // narrow-module width in px (screen render)
    private const double BarHeight = 90.0;

    private static readonly SolidColorBrush BarBrush = new(Color.FromArgb(255, 0, 0, 0));

    private BarcodeService.BarcodeResult? _last;
    private string _svg = "";
    private bool _ready;

    public BarcodeModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { _ready = true; Render(); RebuildAndDraw(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        RebuildAndDraw(); // labels + error text are language-dependent
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Barcode Generator · 條碼產生器";
        HeaderBlurb.Text = P(
            "Generate 1D barcodes as crisp vector bars — Code 128, Code 39 or EAN-13. Every encoder is written by hand in pure C#. Copy the SVG or save it to a file.",
            "產生一維條碼（清晰向量線條）— Code 128、Code 39 或者 EAN-13。每個編碼器都係純 C# 手寫。可以複製 SVG 或者存做檔案。");
        SymLabel.Text = P("Symbology", "條碼類型");
        InputLabel.Text = P("Data", "資料");
        TextChk.Content = P("Show human-readable text under the bars", "喺條碼下面顯示可讀文字");
        CopySvgBtn.Content = P("Copy SVG", "複製 SVG");
        SaveSvgBtn.Content = P("Save .svg…", "儲存 .svg…");

        // Rebuild the symbology list keeping the current selection.
        int keep = SymBox.SelectedIndex < 0 ? 0 : SymBox.SelectedIndex;
        SymBox.Items.Clear();
        SymBox.Items.Add("Code 128");
        SymBox.Items.Add("Code 39");
        SymBox.Items.Add("EAN-13");
        SymBox.SelectedIndex = keep;

        UpdateHint();
    }

    private BarcodeService.Symbology CurrentSymbology() => SymBox.SelectedIndex switch
    {
        1 => BarcodeService.Symbology.Code39,
        2 => BarcodeService.Symbology.Ean13,
        _ => BarcodeService.Symbology.Code128,
    };

    private void UpdateHint()
    {
        HintText.Text = CurrentSymbology() switch
        {
            BarcodeService.Symbology.Code39 => P(
                "A–Z, 0–9 and - . $ / + % and space. Lower-case is upper-cased.",
                "A–Z、0–9 同 - . $ / + % 同空格。細楷會轉做大楷。"),
            BarcodeService.Symbology.Ean13 => P(
                "12 or 13 digits. The check digit is computed (or verified) for you.",
                "12 或 13 個數字。檢查碼會自動計算（或者驗證）。"),
            _ => P(
                "Any printable ASCII. Long digit runs auto-switch to the compact code set C.",
                "任何可列印 ASCII。連續數字會自動轉用緊湊嘅 code set C。"),
        };
    }

    private void OnAnyChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        UpdateHint();
        RebuildAndDraw();
    }

    private void RebuildAndDraw()
    {
        try
        {
            var sym = CurrentSymbology();
            var r = BarcodeService.Encode(sym, InputBox?.Text ?? "");
            _last = r;

            if (!r.Ok)
            {
                _svg = "";
                ErrorBar.Severity = InfoBarSeverity.Error;
                ErrorBar.Message = P(r.ErrorEn, r.ErrorZh);
                ErrorBar.IsOpen = true;
                BarsHost.Children.Clear();
                CopySvgBtn.IsEnabled = false;
                SaveSvgBtn.IsEnabled = false;
                return;
            }

            ErrorBar.IsOpen = false;
            bool showText = TextChk.IsChecked == true;
            _svg = BarcodeService.ToSvg(r, ModuleWidth, BarHeight, showText);
            DrawBars(r, showText);
            CopySvgBtn.IsEnabled = true;
            SaveSvgBtn.IsEnabled = true;
        }
        catch
        {
            // Never let a render fault escape.
            try { ErrorBar.Message = P("Could not render the barcode.", "畫唔到呢個條碼。"); ErrorBar.IsOpen = true; } catch { }
        }
    }

    private void DrawBars(BarcodeService.BarcodeResult r, bool showText)
    {
        BarsHost.Children.Clear();
        BarsHost.ColumnDefinitions.Clear();
        BarsHost.RowDefinitions.Clear();

        var mods = r.Modules;
        int n = mods.Count;
        double totalW = n * ModuleWidth;
        double textH = showText && !string.IsNullOrEmpty(r.HumanText) ? 24 : 0;

        // A fixed-size canvas via a single-cell Grid; bars are absolutely positioned Rectangles.
        var canvas = new Canvas
        {
            Width = totalW,
            Height = BarHeight + textH,
        };

        int i = 0;
        while (i < n)
        {
            if (mods[i])
            {
                int start = i;
                while (i < n && mods[i]) i++;
                var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Width = (i - start) * ModuleWidth,
                    Height = BarHeight,
                    Fill = BarBrush,
                };
                Canvas.SetLeft(rect, start * ModuleWidth);
                Canvas.SetTop(rect, 0);
                canvas.Children.Add(rect);
            }
            else i++;
        }

        if (textH > 0)
        {
            var tb = new TextBlock
            {
                Text = r.HumanText,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 16,
                Foreground = BarBrush,
                Width = totalW,
                TextAlignment = TextAlignment.Center,
            };
            Canvas.SetLeft(tb, 0);
            Canvas.SetTop(tb, BarHeight + 2);
            canvas.Children.Add(tb);
        }

        BarsHost.Children.Add(canvas);
    }

    private void CopySvg_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_svg)) return;
            var pkg = new DataPackage();
            pkg.SetText(_svg);
            Clipboard.SetContent(pkg);
            ShowInfo(P("SVG copied to the clipboard.", "SVG 已經複製到剪貼簿。"), InfoBarSeverity.Success);
        }
        catch
        {
            ShowInfo(P("Could not copy to the clipboard.", "複製唔到去剪貼簿。"), InfoBarSeverity.Error);
        }
    }

    private async void SaveSvg_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_svg)) return;
            string suggested = CurrentSymbology() switch
            {
                BarcodeService.Symbology.Code39 => "code39.svg",
                BarcodeService.Symbology.Ean13 => "ean13.svg",
                _ => "code128.svg",
            };
            var path = await FileDialogs.SaveFileAsync(suggested, ".svg");
            if (string.IsNullOrEmpty(path)) return;
            await System.IO.File.WriteAllTextAsync(path, _svg);
            ShowInfo(P("Saved.", "已儲存。"), InfoBarSeverity.Success);
        }
        catch
        {
            ShowInfo(P("Could not save the file.", "存唔到呢個檔案。"), InfoBarSeverity.Error);
        }
    }

    private void ShowInfo(string msg, InfoBarSeverity sev)
    {
        try
        {
            ErrorBar.Severity = sev;
            ErrorBar.Message = msg;
            ErrorBar.IsOpen = true;
        }
        catch { }
    }
}
