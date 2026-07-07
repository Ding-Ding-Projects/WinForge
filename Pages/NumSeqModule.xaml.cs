using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 數字序列產生器 · Number-sequence generator — arithmetic / geometric / Fibonacci / primes / range /
/// squares / cubes / triangular / powers, with a chosen separator, read-only output and clipboard copy.
/// Pure managed (NumSeqService + BigInteger). Never throws; guards invalid input with bilingual status.
/// </summary>
public sealed partial class NumSeqModule : Page
{
    // Sequence-type indexes (order must match TypeCombo population in Render()).
    private const int Arithmetic = 0, Geometric = 1, Fibonacci = 2, PrimesFirst = 3,
        PrimesUpTo = 4, RangeSeq = 5, Squares = 6, Cubes = 7, Triangular = 8, Powers = 9;

    private bool _suppress;

    public NumSeqModule()
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
        try
        {
            Header.Title = P("Number Sequence", "數字序列");
            HeaderBlurb.Text = P("Generate common number sequences — arithmetic, geometric, Fibonacci, primes, ranges, squares, cubes, triangular numbers and powers — then copy them out.",
                "產生常見嘅數字序列 — 等差、等比、斐波那契、質數、範圍、平方、立方、三角數同次方 — 然後複製出嚟。");
            TypeLabel.Text = P("Sequence type", "序列類型");
            SepLabel.Text = P("Separator", "分隔符");
            GenBtn.Content = P("Generate", "產生");
            CopyBtn.Content = P("Copy", "複製");

            _suppress = true;

            int prevType = TypeCombo.SelectedIndex;
            TypeCombo.Items.Clear();
            TypeCombo.Items.Add(P("Arithmetic (start, step, count)", "等差（起始、間距、個數）"));
            TypeCombo.Items.Add(P("Geometric (start, ratio, count)", "等比（起始、比率、個數）"));
            TypeCombo.Items.Add(P("Fibonacci (n terms)", "斐波那契（n 項）"));
            TypeCombo.Items.Add(P("Prime numbers — first n", "質數 — 頭 n 個"));
            TypeCombo.Items.Add(P("Prime numbers — up to N", "質數 — 直到 N"));
            TypeCombo.Items.Add(P("Range (start..end, step)", "範圍（起始..結束、間距）"));
            TypeCombo.Items.Add(P("Squares (n terms)", "平方（n 項）"));
            TypeCombo.Items.Add(P("Cubes (n terms)", "立方（n 項）"));
            TypeCombo.Items.Add(P("Triangular (n terms)", "三角數（n 項）"));
            TypeCombo.Items.Add(P("Powers of base (base, count)", "次方（底數、個數）"));
            TypeCombo.SelectedIndex = prevType < 0 ? Arithmetic : prevType;

            int prevSep = SepCombo.SelectedIndex;
            SepCombo.Items.Clear();
            SepCombo.Items.Add(P("Comma", "逗號"));
            SepCombo.Items.Add(P("Space", "空格"));
            SepCombo.Items.Add(P("New line", "換行"));
            SepCombo.SelectedIndex = prevSep < 0 ? 0 : prevSep;

            _suppress = false;

            ApplyTypeLabels();
        }
        catch (Exception ex)
        {
            SafeStatus(P("Could not build the form: ", "無法建立表單：") + ex.Message);
        }
    }

    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        ApplyTypeLabels();
    }

    private void Sep_Changed(object sender, SelectionChangedEventArgs e)
    {
        // No-op; separator is read on Generate.
    }

    /// <summary>Show only the inputs relevant to the selected type, with the right labels.</summary>
    private void ApplyTypeLabels()
    {
        try
        {
            int t = TypeCombo.SelectedIndex;

            // Defaults: all three rows visible.
            StartRow.Visibility = Visibility.Visible;
            SecondRow.Visibility = Visibility.Visible;
            CountRow.Visibility = Visibility.Visible;

            switch (t)
            {
                case Arithmetic:
                    StartLabel.Text = P("Start", "起始");
                    SecondLabel.Text = P("Step", "間距");
                    CountLabel.Text = P("Count", "個數");
                    break;
                case Geometric:
                    StartLabel.Text = P("Start", "起始");
                    SecondLabel.Text = P("Ratio", "比率");
                    CountLabel.Text = P("Count", "個數");
                    break;
                case Fibonacci:
                case Squares:
                case Cubes:
                case Triangular:
                    SecondRow.Visibility = Visibility.Collapsed;
                    StartRow.Visibility = Visibility.Collapsed;
                    CountLabel.Text = P("Terms (n)", "項數（n）");
                    break;
                case PrimesFirst:
                    StartRow.Visibility = Visibility.Collapsed;
                    SecondRow.Visibility = Visibility.Collapsed;
                    CountLabel.Text = P("How many (n)", "頭幾個（n）");
                    break;
                case PrimesUpTo:
                    StartRow.Visibility = Visibility.Collapsed;
                    SecondRow.Visibility = Visibility.Collapsed;
                    CountLabel.Text = P("Up to (N)", "直到（N）");
                    break;
                case RangeSeq:
                    StartLabel.Text = P("Start", "起始");
                    SecondLabel.Text = P("End", "結束");
                    CountLabel.Text = P("Step", "間距");
                    break;
                case Powers:
                    StartRow.Visibility = Visibility.Collapsed;
                    StartLabel.Text = P("Base", "底數");
                    SecondLabel.Text = P("Base", "底數");
                    CountLabel.Text = P("Count", "個數");
                    // For powers we reuse SecondBox as base, CountBox as count.
                    break;
            }
        }
        catch { /* never throw from UI wiring */ }
    }

    private static BigInteger Big(NumberBox box)
    {
        double v = double.IsNaN(box.Value) ? 0 : box.Value;
        return new BigInteger(Math.Truncate(v));
    }

    private static long LongOf(NumberBox box)
    {
        double v = double.IsNaN(box.Value) ? 0 : box.Value;
        v = Math.Truncate(v);
        if (v < 0) return 0;
        if (v > NumSeqService.MaxCount) return NumSeqService.MaxCount;
        return (long)v;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int t = TypeCombo.SelectedIndex;
            List<BigInteger> seq;

            switch (t)
            {
                case Arithmetic:
                    seq = NumSeqService.Arithmetic(Big(StartBox), Big(SecondBox), LongOf(CountBox));
                    break;
                case Geometric:
                    seq = NumSeqService.Geometric(Big(StartBox), Big(SecondBox), LongOf(CountBox));
                    break;
                case Fibonacci:
                    seq = NumSeqService.Fibonacci(LongOf(CountBox));
                    break;
                case PrimesFirst:
                    seq = NumSeqService.PrimesFirst(LongOf(CountBox));
                    break;
                case PrimesUpTo:
                {
                    long limit = LongOf(CountBox);
                    if (limit < 2)
                    {
                        SafeStatus(P("Enter a limit of at least 2 for primes.", "質數上限最少要 2。"));
                        OutputBox.Text = string.Empty;
                        return;
                    }
                    seq = NumSeqService.PrimesUpTo(limit);
                    break;
                }
                case RangeSeq:
                {
                    BigInteger step = Big(CountBox);
                    if (step == 0)
                    {
                        SafeStatus(P("Step cannot be zero.", "間距唔可以係 0。"));
                        OutputBox.Text = string.Empty;
                        return;
                    }
                    seq = NumSeqService.Range(Big(StartBox), Big(SecondBox), step);
                    if (seq.Count == 0)
                        SafeStatus(P("Range direction and step sign don't match — nothing to show.", "範圍方向同間距正負唔夾 — 冇嘢顯示。"));
                    break;
                }
                case Squares:
                    seq = NumSeqService.Squares(LongOf(CountBox));
                    break;
                case Cubes:
                    seq = NumSeqService.Cubes(LongOf(CountBox));
                    break;
                case Triangular:
                    seq = NumSeqService.Triangular(LongOf(CountBox));
                    break;
                case Powers:
                    seq = NumSeqService.Powers(Big(SecondBox), LongOf(CountBox));
                    break;
                default:
                    seq = new List<BigInteger>();
                    break;
            }

            string separator = SepCombo.SelectedIndex switch
            {
                1 => " ",
                2 => Environment.NewLine,
                _ => ", "
            };

            OutputBox.Text = NumSeqService.Format(seq, separator);

            if (seq.Count == 0 && string.IsNullOrEmpty(StatusText.Text))
                SafeStatus(P("No numbers generated — check the inputs.", "冇產生數字 — 請檢查輸入。"));
            else if (seq.Count > 0)
            {
                string capped = seq.Count >= NumSeqService.MaxCount
                    ? P($" (capped at {NumSeqService.MaxCount:N0})", $"（已封頂 {NumSeqService.MaxCount:N0}）")
                    : string.Empty;
                SafeStatus(P($"Generated {seq.Count:N0} number(s).", $"已產生 {seq.Count:N0} 個數字。") + capped);
            }
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            SafeStatus(P("Generation failed: ", "產生失敗：") + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(OutputBox.Text))
            {
                SafeStatus(P("Nothing to copy — generate a sequence first.", "冇嘢可以複製 — 請先產生序列。"));
                return;
            }
            var dp = new DataPackage();
            dp.SetText(OutputBox.Text);
            Clipboard.SetContent(dp);
            SafeStatus(P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SafeStatus(P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private void SafeStatus(string text)
    {
        try { StatusText.Text = text; } catch { /* ignore */ }
    }
}
