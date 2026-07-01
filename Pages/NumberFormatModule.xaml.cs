using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 數字格式化 · Number Formatter — type a number and see culture-aware variants
/// (thousands-grouped, fixed decimals, currency, percent, scientific, accounting,
/// zero-padded), each individually copyable. Live on change; never throws; bilingual.
/// </summary>
public sealed partial class NumberFormatModule : Page
{
    private bool _suppress;

    public NumberFormatModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PopulateCurrencies();
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void PopulateCurrencies()
    {
        if (CurrencyCombo.Items.Count > 0) return;
        _suppress = true;
        foreach (var c in NumberFormatService.Currencies)
            CurrencyCombo.Items.Add(new ComboBoxItem { Content = c.Display, Tag = c.Culture });
        CurrencyCombo.SelectedIndex = 0;
        _suppress = false;
    }

    private void Render()
    {
        Header.Title = P("Number Formatter · 數字格式化", "數字格式化 · Number Formatter");
        HeaderBlurb.Text = P(
            "Type a number and get culture-aware formatted variants — grouped, fixed decimals, currency, percent, scientific, accounting and zero-padded. Tap the copy button beside any line.",
            "打個數字入去，即刻出到唔同格式 — 千分位、固定小數、貨幣、百分比、科學記數、會計格式同埋補零。撳每行隔籬粒複製掣就得。");
        InputLabel.Text = P("Number", "數字");
        DecimalsLabel.Text = P("Decimal places (0–10)", "小數位（0–10）");
        PadLabel.Text = P("Zero-pad width (0–40)", "補零寬度（0–40）");
        CurrencyLabel.Text = P("Currency culture", "貨幣地區");
        ResultsLabel.Text = P("Formatted variants", "格式化結果");
        RenderResults();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        RenderResults();
    }

    private void Options_Changed(object sender, object e)
    {
        if (_suppress) return;
        RenderResults();
    }

    private void RenderResults()
    {
        try
        {
            var raw = InputBox?.Text ?? string.Empty;
            if (!NumberFormatService.TryParse(raw, out var value))
            {
                ResultsList.ItemsSource = null;
                StatusText.Text = string.IsNullOrWhiteSpace(raw)
                    ? P("Enter a number to see formatted variants.", "輸入一個數字就會顯示格式化結果。")
                    : P("That doesn't look like a number — check for stray letters or symbols.", "呢個唔似係數字 — 睇下係咪有多餘嘅字母或者符號。");
                return;
            }

            int decimals = ReadInt(DecimalsBox, 2, 0, 10);
            int pad = ReadInt(PadBox, 8, 0, 40);
            string culture = (CurrencyCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "en-US";

            var labels = new NumberFormatService.Labels(
                Grouped: P("Grouped (invariant)", "千分位（不變文化）"),
                Fixed: P("Fixed decimals", "固定小數"),
                Currency: P("Currency", "貨幣"),
                Percent: P("Percent", "百分比"),
                Scientific: P("Scientific (E)", "科學記數（E）"),
                Accounting: P("Accounting", "會計格式"),
                Padded: P("Zero-padded", "補零"));

            var items = NumberFormatService.BuildAll(value, decimals, culture, pad, labels);
            ResultsList.ItemsSource = new List<NumberFormatService.FormatItem>(items);
            StatusText.Text = P("Parsed OK — results update as you type.", "解析成功 — 你打字嗰陣結果會即時更新。");
        }
        catch
        {
            ResultsList.ItemsSource = null;
            StatusText.Text = P("Could not format that value.", "冇辦法格式化呢個數值。");
        }
    }

    private static int ReadInt(NumberBox box, int fallback, int lo, int hi)
    {
        if (box == null) return fallback;
        var v = box.Value;
        if (double.IsNaN(v) || double.IsInfinity(v)) return fallback;
        int i = (int)Math.Round(v);
        return i < lo ? lo : (i > hi ? hi : i);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = (sender as FrameworkElement)?.Tag as string;
            if (string.IsNullOrEmpty(text)) return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P($"Copied: {text}", $"已複製：{text}");
        }
        catch
        {
            StatusText.Text = P("Copy failed.", "複製失敗。");
        }
    }
}
