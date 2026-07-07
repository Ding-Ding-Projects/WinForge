using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 時長計算器 · Duration calculator — parse flexible duration text into TimeSpan and add/subtract, sum a
/// list, convert to decimal units, or multiply/divide. Pure managed (System.TimeSpan). Bilingual, never throws.
/// </summary>
public sealed partial class DurationCalcModule : Page
{
    private TimeSpan _addResult, _sumResult, _convSource, _scaleResult;
    private string _convText = string.Empty;

    public DurationCalcModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Duration Calculator · 時長計算器";
        HeaderBlurb.Text = P("Add, subtract, sum, convert and scale time durations — all worked out locally.",
            "加、減、加總、換算同放大縮小時間長度 — 全部喺本機計。");
        FormatsHint.Text = P("Accepted formats: 1:30:00 · 1d 2h 30m · 90m · 2.5h · 45s · 1h30m · a bare number = minutes.",
            "接受格式：1:30:00 · 1d 2h 30m · 90m · 2.5h · 45s · 1h30m · 淨數字當分鐘。");

        AddTitle.Text = P("Add or subtract two durations", "兩個時長相加或相減");
        SumTitle.Text = P("Sum a list of durations", "加總一列時長");
        ConvTitle.Text = P("Convert a duration", "換算一個時長");
        ScaleTitle.Text = P("Multiply or divide a duration", "時長乘或除");

        AddA.PlaceholderText = ConvBox.PlaceholderText = ScaleDur.PlaceholderText = P("e.g. 1h30m", "例如 1h30m");
        AddB.PlaceholderText = P("e.g. 45m", "例如 45m");
        SumBox.PlaceholderText = P("One duration per line, e.g.\n1:30:00\n90m\n2.5h", "每行一個時長，例如\n1:30:00\n90m\n2.5h");

        var calc = P("Calculate", "計算");
        AddBtn.Content = SumBtn.Content = ConvBtn.Content = ScaleBtn.Content = calc;
        var copy = P("Copy", "複製");
        AddCopyBtn.Content = SumCopyBtn.Content = ConvCopyBtn.Content = ScaleCopyBtn.Content = copy;
    }

    // ---- (a) add / subtract ----
    private void Add_Click(object sender, RoutedEventArgs e)
    {
        AddResult.Text = string.Empty;
        AddCopyBtn.IsEnabled = false;
        if (!DurationCalcService.TryParse(AddA.Text, out var a, out var en, out var zh))
        { AddStatus.Text = P("First: " + en, "第一個：" + zh); return; }
        if (!DurationCalcService.TryParse(AddB.Text, out var b, out en, out zh))
        { AddStatus.Text = P("Second: " + en, "第二個：" + zh); return; }

        bool subtract = AddOp.SelectedIndex == 1;
        try { _addResult = subtract ? a - b : a + b; }
        catch { AddStatus.Text = P("Result out of range.", "結果超出範圍。"); return; }

        AddResult.Text = $"{DurationCalcService.FormatClock(_addResult)}   ({DurationCalcService.FormatUnits(_addResult)})";
        AddStatus.Text = P($"= {DurationCalcService.DecimalHours(_addResult)} h · {DurationCalcService.DecimalMinutes(_addResult)} min",
            $"= {DurationCalcService.DecimalHours(_addResult)} 小時 · {DurationCalcService.DecimalMinutes(_addResult)} 分鐘");
        AddCopyBtn.IsEnabled = true;
    }

    private void AddCopy_Click(object sender, RoutedEventArgs e) => Copy(DurationCalcService.FormatClock(_addResult), AddStatus);

    // ---- (b) sum a list ----
    private void Sum_Click(object sender, RoutedEventArgs e)
    {
        SumResult.Text = string.Empty;
        SumCopyBtn.IsEnabled = false;
        var lines = (SumBox.Text ?? string.Empty).Split('\n');
        var total = TimeSpan.Zero;
        int count = 0, lineNo = 0;
        try
        {
            foreach (var raw in lines)
            {
                lineNo++;
                var line = raw.Trim();
                if (line.Length == 0) continue;
                if (!DurationCalcService.TryParse(line, out var ts, out var en, out var zh))
                {
                    SumStatus.Text = P($"Line {lineNo}: {en}", $"第 {lineNo} 行：{zh}");
                    return;
                }
                total += ts;
                count++;
            }
        }
        catch { SumStatus.Text = P("Total out of range.", "總數超出範圍。"); return; }

        if (count == 0) { SumStatus.Text = P("No durations entered.", "未輸入任何時長。"); return; }

        _sumResult = total;
        SumResult.Text = $"{DurationCalcService.FormatClock(total)}   ({DurationCalcService.FormatUnits(total)})";
        SumStatus.Text = P($"{count} item(s) · {DurationCalcService.DecimalHours(total)} h total",
            $"{count} 項 · 合共 {DurationCalcService.DecimalHours(total)} 小時");
        SumCopyBtn.IsEnabled = true;
    }

    private void SumCopy_Click(object sender, RoutedEventArgs e) => Copy(DurationCalcService.FormatClock(_sumResult), SumStatus);

    // ---- (c) convert ----
    private void Convert_Click(object sender, RoutedEventArgs e)
    {
        ConvResult.Text = string.Empty;
        ConvCopyBtn.IsEnabled = false;
        if (!DurationCalcService.TryParse(ConvBox.Text, out var ts, out var en, out var zh))
        { ConvStatus.Text = P(en, zh); return; }

        _convSource = ts;
        _convText =
            $"{DurationCalcService.DecimalSeconds(ts)} s\n" +
            $"{DurationCalcService.DecimalMinutes(ts)} min\n" +
            $"{DurationCalcService.DecimalHours(ts)} h\n" +
            $"{DurationCalcService.DecimalDays(ts)} d";
        ConvResult.Text = _convText;
        ConvStatus.Text = P($"{DurationCalcService.FormatClock(ts)}  ({DurationCalcService.FormatUnits(ts)})",
            $"{DurationCalcService.FormatClock(ts)}  ({DurationCalcService.FormatUnits(ts)})");
        ConvCopyBtn.IsEnabled = true;
    }

    private void ConvCopy_Click(object sender, RoutedEventArgs e) => Copy(_convText, ConvStatus);

    // ---- (d) multiply / divide ----
    private void Scale_Click(object sender, RoutedEventArgs e)
    {
        ScaleResult.Text = string.Empty;
        ScaleCopyBtn.IsEnabled = false;
        if (!DurationCalcService.TryParse(ScaleDur.Text, out var ts, out var en, out var zh))
        { ScaleStatus.Text = P(en, zh); return; }

        double factor = double.IsNaN(ScaleNum.Value) ? 0 : ScaleNum.Value;
        bool divide = ScaleOp.SelectedIndex == 1;
        if (divide && factor == 0)
        { ScaleStatus.Text = P("Cannot divide by zero.", "唔可以除以零。"); return; }

        try { _scaleResult = divide ? ts / factor : ts * factor; }
        catch { ScaleStatus.Text = P("Result out of range.", "結果超出範圍。"); return; }

        ScaleResult.Text = $"{DurationCalcService.FormatClock(_scaleResult)}   ({DurationCalcService.FormatUnits(_scaleResult)})";
        var op = divide ? "÷" : "×";
        ScaleStatus.Text = P($"{op} {factor.ToString("0.######", CultureInfo.InvariantCulture)}  =  {DurationCalcService.DecimalHours(_scaleResult)} h",
            $"{op} {factor.ToString("0.######", CultureInfo.InvariantCulture)}  =  {DurationCalcService.DecimalHours(_scaleResult)} 小時");
        ScaleCopyBtn.IsEnabled = true;
    }

    private void ScaleCopy_Click(object sender, RoutedEventArgs e) => Copy(DurationCalcService.FormatClock(_scaleResult), ScaleStatus);

    // ---- clipboard ----
    private void Copy(string text, TextBlock status)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return;
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            status.Text = P("Copied.", "已複製。");
        }
        catch
        {
            status.Text = P("Couldn't copy to clipboard.", "複製唔到去剪貼簿。");
        }
    }
}
