using System;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 表達式計數機 · Expression calculator — a real recursive-descent evaluator (see
/// <see cref="CalculatorService"/>) over + - * / % ^, unary minus, parentheses, math functions and
/// pi/e, with a deg/rad angle toggle, live evaluation, a "expr = result" history, a programmer view
/// (dec/hex/bin/oct for integral results) and an optional button pad. Bilingual, leak-free. No redirect.
/// </summary>
public sealed partial class CalculatorModule : Page
{
    private readonly ObservableCollection<string> _history = new();
    private bool _suppress;

    public CalculatorModule()
    {
        InitializeComponent();
        HistoryList.ItemsSource = _history;
        BuildPad();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private CalculatorService.AngleMode Mode =>
        AngleSwitch.IsOn ? CalculatorService.AngleMode.Degrees : CalculatorService.AngleMode.Radians;

    private void Render()
    {
        Header.Title = P("Calculator", "計數機");
        HeaderBlurb.Text = P("Type a math expression and see it evaluate live. Supports + - * / % ^, parentheses, functions (sin cos tan asin acos atan sqrt cbrt ln log log2 abs round floor ceil exp) and the constants pi and e.",
            "打數學表達式，即刻計出結果。支援 + - * / % ^、括號、函數（sin cos tan asin acos atan sqrt cbrt ln log log2 abs round floor ceil exp）同埋常數 pi、e。");
        AngleLabel.Text = AngleSwitch.IsOn ? P("Degrees", "角度") : P("Radians", "弧度");
        PadTitle.Text = P("Button pad", "按鍵盤");
        ProgTitle.Text = P("Programmer view", "程式員檢視");
        HistoryTitle.Text = P("History", "紀錄");
        ClearBtn.Content = P("Clear", "清除");
        ExprBox.PlaceholderText = P("e.g.  (2 + 3) * sin(pi/4)", "例如  (2 + 3) * sin(pi/4)");
        Evaluate();
    }

    // ---- Evaluation ------------------------------------------------------

    private void Expr_TextChanged(object sender, TextChangedEventArgs e) => Evaluate();

    private void Expr_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            CommitToHistory();
        }
    }

    private void Angle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        AngleLabel.Text = AngleSwitch.IsOn ? P("Degrees", "角度") : P("Radians", "弧度");
        Evaluate();
    }

    private void Evaluate()
    {
        string expr = ExprBox?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expr))
        {
            ResultText.Text = string.Empty;
            StatusText.Text = string.Empty;
            ProgCard.Visibility = Visibility.Collapsed;
            return;
        }

        var r = CalculatorService.Evaluate(expr, Mode);
        if (r.Ok)
        {
            ResultText.Text = "= " + FormatNumber(r.Value);
            StatusText.Text = string.Empty;
            UpdateProgrammer(r.Value);
        }
        else
        {
            ResultText.Text = string.Empty;
            StatusText.Text = ErrorText(r.Error);
            ProgCard.Visibility = Visibility.Collapsed;
        }
    }

    private void CommitToHistory()
    {
        string expr = ExprBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expr)) return;
        var r = CalculatorService.Evaluate(expr, Mode);
        if (!r.Ok)
        {
            StatusText.Text = ErrorText(r.Error);
            return;
        }
        string entry = $"{expr} = {FormatNumber(r.Value)}";
        _history.Insert(0, entry);
        if (_history.Count > 100) _history.RemoveAt(_history.Count - 1);
    }

    private void History_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string s)
        {
            int idx = s.LastIndexOf(" = ", StringComparison.Ordinal);
            string expr = idx > 0 ? s.Substring(0, idx) : s;
            ExprBox.Text = expr;
            ExprBox.SelectionStart = expr.Length;
            ExprBox.Focus(FocusState.Programmatic);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _history.Clear();

    // ---- Programmer view -------------------------------------------------

    private void UpdateProgrammer(double v)
    {
        // Only for finite, integral values that fit a signed 64-bit integer.
        if (double.IsNaN(v) || double.IsInfinity(v) ||
            v != Math.Floor(v) || v < long.MinValue || v > long.MaxValue)
        {
            ProgCard.Visibility = Visibility.Collapsed;
            return;
        }

        long n = (long)v;
        ulong bits = unchecked((ulong)n);
        DecText.Text = n.ToString(CultureInfo.InvariantCulture);
        HexText.Text = "0x" + bits.ToString("X");
        BinText.Text = "0b" + Convert.ToString((long)bits, 2);
        OctText.Text = "0o" + Convert.ToString((long)bits, 8);
        ProgCard.Visibility = Visibility.Visible;
    }

    // ---- Formatting & errors --------------------------------------------

    private static string FormatNumber(double v)
    {
        if (v == Math.Floor(v) && Math.Abs(v) < 1e15 && !double.IsInfinity(v))
            return ((long)v).ToString(CultureInfo.InvariantCulture);
        return v.ToString("G15", CultureInfo.InvariantCulture);
    }

    private string ErrorText(string tag)
    {
        if (tag.StartsWith("unknown:", StringComparison.Ordinal))
        {
            string name = tag.Substring("unknown:".Length);
            return P($"Unknown name \"{name}\".", $"唔識嘅名 \"{name}\"。");
        }
        if (tag.StartsWith("unknownfn:", StringComparison.Ordinal))
        {
            string name = tag.Substring("unknownfn:".Length);
            return P($"Unknown function \"{name}\".", $"唔識嘅函數 \"{name}\"。");
        }
        return tag switch
        {
            "empty" => string.Empty,
            "divzero" => P("Can't divide by zero.", "唔可以除以零。"),
            "domain" => P("Value out of range for that function.", "數值超出咗嗰個函數嘅範圍。"),
            "unbalanced" => P("Unbalanced parentheses.", "括號唔對稱。"),
            "trailing" => P("Unexpected trailing input.", "後面有多餘嘅嘢。"),
            "incomplete" => P("Expression is incomplete.", "表達式未打完。"),
            "badchar" => P("Unrecognized character.", "唔認得嘅字元。"),
            "nan" => P("Result is not a number.", "結果唔係一個數。"),
            "infinity" => P("Result is too large (infinity).", "結果太大（無限大）。"),
            "malformed" => P("Malformed expression.", "表達式格式錯誤。"),
            _ => P("Malformed expression.", "表達式格式錯誤。"),
        };
    }

    // ---- Button pad ------------------------------------------------------

    private void BuildPad()
    {
        string[][] rows =
        {
            new[] { "7", "8", "9", "/", "(", ")" },
            new[] { "4", "5", "6", "*", "^", "%" },
            new[] { "1", "2", "3", "-", "pi", "e" },
            new[] { "0", ".", "=", "+", "sqrt(", "ln(" },
            new[] { "sin(", "cos(", "tan(", "log(", "abs(", "⌫" },
        };

        foreach (var row in rows)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var key in row)
            {
                var btn = new Button
                {
                    Content = key,
                    MinWidth = 64,
                    Tag = key,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                };
                btn.Click += Pad_Click;
                panel.Children.Add(btn);
            }
            PadPanel.Children.Add(panel);
        }
    }

    private void Pad_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string key) return;

        if (key == "⌫") // backspace
        {
            string t = ExprBox.Text ?? string.Empty;
            if (t.Length > 0) ExprBox.Text = t.Substring(0, t.Length - 1);
            ExprBox.SelectionStart = ExprBox.Text.Length;
            ExprBox.Focus(FocusState.Programmatic);
            return;
        }

        if (key == "=")
        {
            CommitToHistory();
            return;
        }

        ExprBox.Text = (ExprBox.Text ?? string.Empty) + key;
        ExprBox.SelectionStart = ExprBox.Text.Length;
        ExprBox.Focus(FocusState.Programmatic);
    }
}
