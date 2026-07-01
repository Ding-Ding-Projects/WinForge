using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 數字轉文字（加強版）· Number to Words+ — spells a number (decimals + negatives) as English
/// cardinal / ordinal, currency words, Chinese lowercase (一百二十三) and Chinese financial 大寫
/// (壹佰貳拾參 / 元角分). Several outputs at once, each with a copy button. Pure managed, bilingual,
/// robust (never throws). No redirect, no external process. See <see cref="NumWordsXService"/>.
/// </summary>
public sealed partial class NumWordsXModule : Page
{
    // Mode indices in the ComboBox.
    private const int ModeAll = 0;
    private const int ModeCardinal = 1;
    private const int ModeOrdinal = 2;
    private const int ModeCurrency = 3;
    private const int ModeChineseLower = 4;
    private const int ModeChineseUpper = 5;

    private static readonly string[] CurrencyCodes = { "USD", "HKD", "GBP" };

    private bool _suppress;

    public NumWordsXModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildCombos();
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e)
    {
        BuildCombos();
        Render();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        _suppress = true;
        try
        {
            int mode = ModeCombo.SelectedIndex < 0 ? 0 : ModeCombo.SelectedIndex;
            int cur = CurrencyCombo.SelectedIndex < 0 ? 0 : CurrencyCombo.SelectedIndex;

            ModeCombo.Items.Clear();
            ModeCombo.Items.Add(P("All results", "全部結果"));
            ModeCombo.Items.Add(P("English cardinal", "英文基數"));
            ModeCombo.Items.Add(P("English ordinal", "英文序數"));
            ModeCombo.Items.Add(P("Currency (English)", "貨幣（英文）"));
            ModeCombo.Items.Add(P("Chinese 中文小寫", "中文小寫"));
            ModeCombo.Items.Add(P("Chinese 大寫 uppercase", "中文大寫"));
            ModeCombo.SelectedIndex = mode;

            CurrencyCombo.Items.Clear();
            CurrencyCombo.Items.Add(P("USD — US dollars/cents", "美元 USD 元/仙"));
            CurrencyCombo.Items.Add(P("HKD — HK dollars/cents", "港幣 HKD 元/仙"));
            CurrencyCombo.Items.Add(P("GBP — pounds/pence", "英鎊 GBP 鎊/便士"));
            CurrencyCombo.SelectedIndex = cur;
        }
        finally { _suppress = false; }
    }

    private void Render()
    {
        Header.Title = "Number to Words+ · 數字轉文字（加強版）";
        HeaderBlurb.Text = P(
            "Type any number — decimals and negatives welcome — and read it back as English words, ordinals, currency, or Chinese (小寫 & 大寫 financial). Copy any result with one click.",
            "打個數字入去（可以有小數同負號），即刻變做英文字、序數、貨幣，或者中文（小寫同大寫財務寫法）。撳一下就複製到任何結果。");
        InputLabel.Text = P("Number", "數字");
        ModeLabel.Text = P("Output mode", "輸出模式");
        CurrencyLabel.Text = P("Currency", "貨幣");
        Recompute();
    }

    private void OnInputChanged(object sender, TextChangedEventArgs e) { if (!_suppress) Recompute(); }
    private void OnModeChanged(object sender, SelectionChangedEventArgs e) { if (!_suppress) Recompute(); }
    private void OnCurrencyChanged(object sender, SelectionChangedEventArgs e) { if (!_suppress) Recompute(); }

    private void Recompute()
    {
        try
        {
            RecomputeCore();
        }
        catch (Exception ex)
        {
            // Never throw to the UI thread; surface a friendly status.
            SafeClearResults();
            ShowStatus(InfoBarSeverity.Error,
                P("Could not convert that input.", "無法轉換呢個輸入。"),
                ex.Message);
        }
    }

    private void RecomputeCore()
    {
        SafeClearResults();

        string raw = InputBox?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            ShowStatus(InfoBarSeverity.Informational,
                P("Enter a number to begin.", "輸入一個數字開始。"), null);
            return;
        }

        if (!NumWordsXService.TryParse(raw, out bool neg, out BigInteger integer, out string fraction))
        {
            ShowStatus(InfoBarSeverity.Warning,
                P("That is not a valid number. Use digits, an optional '-' and one '.'.",
                  "唔係有效數字。只可以用數字、可選嘅「-」同一個「.」。"), null);
            return;
        }

        // Guard oversized values that exceed our named English scales (~decillions).
        int intDigits = integer.IsZero ? 1 : integer.ToString().Length;
        bool englishInRange = intDigits <= 36; // up to decillion group table
        bool chineseInRange = intDigits <= 16; // up to 兆 (10^16 exclusive)

        Status.IsOpen = false;

        int mode = ModeCombo.SelectedIndex < 0 ? ModeAll : ModeCombo.SelectedIndex;
        var spec = NumWordsXService.Currencies[CurrencyCodes[Math.Max(0, CurrencyCombo.SelectedIndex)]];

        bool all = mode == ModeAll;
        bool wanted(int m) => all || mode == m;

        string echo = NumWordsXService.DisplayNumber(neg, integer, fraction);
        var notes = new List<string>();

        if (wanted(ModeCardinal))
        {
            if (englishInRange)
                AddResult(P("English cardinal", "英文基數"),
                    NumWordsXService.EnglishCardinalSigned(neg, integer));
            else notes.Add(P("English words: number too large.", "英文字：數字太大。"));
        }

        if (wanted(ModeOrdinal))
        {
            if (englishInRange)
            {
                string ord = (neg && !integer.IsZero ? "negative " : "") + NumWordsXService.EnglishOrdinal(integer);
                string num = NumWordsXService.OrdinalNumeric(neg, integer);
                AddResult(P("English ordinal", "英文序數"), ord + "  (" + num + ")");
            }
            else notes.Add(P("Ordinal: number too large.", "序數：數字太大。"));
        }

        if (wanted(ModeCurrency))
        {
            if (englishInRange)
                AddResult(P($"Currency — {spec.Code} (English)", $"貨幣 — {spec.Code}（英文）"),
                    NumWordsXService.EnglishCurrency(neg, integer, fraction, spec));
            else notes.Add(P("Currency: number too large.", "貨幣：數字太大。"));
        }

        if (wanted(ModeChineseLower))
        {
            if (chineseInRange)
                AddResult(P("Chinese 中文小寫", "中文小寫"),
                    NumWordsXService.ChineseSigned(neg, integer, upper: false));
            else notes.Add(P("Chinese lowercase: number too large (max 兆).", "中文小寫：數字太大（最多到兆）。"));
        }

        if (wanted(ModeChineseUpper))
        {
            if (chineseInRange)
            {
                AddResult(P("Chinese 大寫 uppercase", "中文大寫"),
                    NumWordsXService.ChineseSigned(neg, integer, upper: true));
                // Financial currency form (元角分) makes sense whenever uppercase is shown.
                AddResult(P("Chinese 大寫 currency 元角分", "中文大寫貨幣 圓角分"),
                    NumWordsXService.ChineseCurrencyUpper(neg, integer, fraction));
            }
            else notes.Add(P("Chinese uppercase: number too large (max 兆).", "中文大寫：數字太大（最多到兆）。"));
        }

        if (ResultsPanel.Children.Count == 0 && notes.Count == 0)
        {
            ShowStatus(InfoBarSeverity.Informational,
                P("No output for this mode.", "呢個模式冇輸出。"), null);
            return;
        }

        string head = P($"Read as {echo}", $"讀作 {echo}");
        string msg = notes.Count > 0 ? head + " — " + string.Join(" ", notes) : head;
        ShowStatus(notes.Count > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success, msg, null);
    }

    private void AddResult(string label, string value)
    {
        var outer = new StackPanel { Spacing = 6 };
        outer.Children.Add(new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new TextBox
        {
            Text = value ?? string.Empty,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = false,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(box, 0);

        var copy = new Button
        {
            Content = P("Copy", "複製"),
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        string toCopy = value ?? string.Empty;
        copy.Click += (_, _) => CopyToClipboard(toCopy);
        Grid.SetColumn(copy, 1);

        grid.Children.Add(box);
        grid.Children.Add(copy);
        outer.Children.Add(grid);

        var card = new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Child = outer,
        };
        if (Application.Current.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out var bg))
            card.Background = bg as Microsoft.UI.Xaml.Media.Brush;
        if (Application.Current.Resources.TryGetValue("CardStrokeColorDefaultBrush", out var st))
            card.BorderBrush = st as Microsoft.UI.Xaml.Media.Brush;

        ResultsPanel.Children.Add(card);
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text ?? string.Empty);
            Clipboard.SetContent(dp);
            ShowStatus(InfoBarSeverity.Success, P("Copied to clipboard.", "已複製到剪貼簿。"), null);
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P("Copy failed.", "複製失敗。"), ex.Message);
        }
    }

    private void SafeClearResults()
    {
        if (ResultsPanel != null) ResultsPanel.Children.Clear();
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string? message)
    {
        if (Status == null) return;
        Status.Severity = severity;
        Status.Title = title ?? string.Empty;
        Status.Message = message ?? string.Empty;
        Status.IsOpen = true;
    }
}
