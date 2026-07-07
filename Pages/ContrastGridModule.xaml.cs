using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 對比度網格 · WCAG contrast grid — the user adds colours (hex / rgb) and every pair is scored
/// with its WCAG relative-luminance contrast ratio plus AA/AAA pass-fail badges for normal and
/// large text. Colour-coded, copyable report. Pure managed, never throws. Bilingual (粵語).
/// </summary>
public sealed partial class ContrastGridModule : Page
{
    private readonly ObservableCollection<ColorRow> _colors = new();
    private readonly ObservableCollection<PairRow> _pairs = new();

    private static readonly SolidColorBrush PassBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush FailBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xC6, 0x28, 0x28));

    public ContrastGridModule()
    {
        InitializeComponent();
        ColorsList.ItemsSource = _colors;
        PairsList.ItemsSource = _pairs;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        if (_colors.Count == 0)
        {
            // start with 3
            TryAdd("#FFFFFF");
            TryAdd("#767676");
            TryAdd("#1A73E8");
        }
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Contrast Grid · 對比度網格";
        HeaderBlurb.Text = P(
            "Add colours (hex like #3366CC, or rgb(51,102,204)). WinForge scores every pair with its WCAG contrast ratio and AA/AAA pass-fail badges for normal and large text.",
            "加入顏色（十六進制例如 #3366CC，或者 rgb(51,102,204)）。WinForge 會計出每一對嘅 WCAG 對比度，並顯示正常同大字嘅 AA/AAA 合格與否。");
        ColorInput.PlaceholderText = P("#3366CC or rgb(51,102,204)", "#3366CC 或 rgb(51,102,204)");
        AddBtn.Content = P("Add", "加入");
        CopyBtn.Content = P("Copy report", "複製報告");
        ColorsLabel.Text = P("Colours", "顏色");
        PairsLabel.Text = P("Pairs", "配對");
        UpdateEmptyStatus();
    }

    private void UpdateEmptyStatus()
    {
        if (_colors.Count < 2)
            StatusText.Text = P("Add at least two colours to compare.", "加入至少兩隻顏色先可以比較。");
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string text = ColorInput.Text;
        if (TryAdd(text))
        {
            ColorInput.Text = string.Empty;
            StatusText.Text = P("Added.", "已加入。");
            Recompute();
        }
        else
        {
            StatusText.Text = P($"Couldn't read \"{text}\" — try #RRGGBB or rgb(r,g,b).",
                $"讀唔到「{text}」— 試下 #RRGGBB 或 rgb(r,g,b)。");
        }
    }

    private bool TryAdd(string? text)
    {
        try
        {
            var c = ContrastGridService.Parse(text);
            if (!c.Ok) return false;
            foreach (var existing in _colors)
                if (string.Equals(existing.Hex, c.Hex, StringComparison.OrdinalIgnoreCase))
                    return true; // already present; treat as success, no dup
            _colors.Add(new ColorRow(c));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button b && b.Tag is string hex)
            {
                for (int i = _colors.Count - 1; i >= 0; i--)
                    if (string.Equals(_colors[i].Hex, hex, StringComparison.OrdinalIgnoreCase))
                        _colors.RemoveAt(i);
                Recompute();
            }
        }
        catch { /* never throw from UI */ }
    }

    private void Recompute()
    {
        try
        {
            _pairs.Clear();
            for (int i = 0; i < _colors.Count; i++)
                for (int j = i + 1; j < _colors.Count; j++)
                    _pairs.Add(new PairRow(_colors[i].Color, _colors[j].Color, P));

            if (_colors.Count >= 2)
                StatusText.Text = P($"{_colors.Count} colours · {_pairs.Count} pairs.",
                    $"{_colors.Count} 隻顏色 · {_pairs.Count} 對。");
            else
                UpdateEmptyStatus();
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Couldn't compute the grid: ", "計唔到對比度網格：") + ex.Message;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(P("WCAG Contrast Grid · WCAG 對比度網格", "WCAG 對比度網格 · WCAG Contrast Grid"));
            sb.AppendLine();
            foreach (var p in _pairs)
            {
                sb.AppendLine($"{p.PairText}  {p.RatioText}");
                sb.AppendLine($"    AA normal: {p.AaNormalText} | AAA normal: {p.AaaNormalText} | AA large: {p.AaLargeText} | AAA large: {p.AaaLargeText}");
            }
            if (_pairs.Count == 0)
                sb.AppendLine(P("(no pairs — add at least two colours)", "（未有配對 — 加入至少兩隻顏色）"));

            var dp = new DataPackage();
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);
            StatusText.Text = P("Report copied to clipboard.", "報告已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Couldn't copy: ", "複製唔到：") + ex.Message;
        }
    }

    // ---- item view-models (classic {Binding}) ----

    public sealed class ColorRow
    {
        public ContrastGridService.Rgb Color { get; }
        public string Hex { get; }
        public Brush Swatch { get; }
        public ColorRow(ContrastGridService.Rgb c)
        {
            Color = c;
            Hex = c.Hex;
            Swatch = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, c.R, c.G, c.B));
        }
    }

    public sealed class PairRow
    {
        public Brush ColorA { get; }
        public Brush ColorB { get; }
        public string PairText { get; }
        public string RatioText { get; }

        public string AaNormalText { get; }
        public Brush AaNormalBrush { get; }
        public string AaaNormalText { get; }
        public Brush AaaNormalBrush { get; }
        public string AaLargeText { get; }
        public Brush AaLargeBrush { get; }
        public string AaaLargeText { get; }
        public Brush AaaLargeBrush { get; }

        public PairRow(ContrastGridService.Rgb a, ContrastGridService.Rgb b, Func<string, string, string> pick)
        {
            ColorA = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, a.R, a.G, a.B));
            ColorB = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, b.R, b.G, b.B));
            PairText = $"{a.Hex} · {b.Hex}";

            double ratio = ContrastGridService.ContrastRatio(a, b);
            RatioText = ratio.ToString("0.00", CultureInfo.InvariantCulture) + ":1";

            AaNormalText = Badge(ratio >= ContrastGridService.AaNormal, "AA", pick);
            AaNormalBrush = Score(ratio >= ContrastGridService.AaNormal);
            AaaNormalText = Badge(ratio >= ContrastGridService.AaaNormal, "AAA", pick);
            AaaNormalBrush = Score(ratio >= ContrastGridService.AaaNormal);
            AaLargeText = Badge(ratio >= ContrastGridService.AaLarge, "AA " + pick("Large", "大字"), pick);
            AaLargeBrush = Score(ratio >= ContrastGridService.AaLarge);
            AaaLargeText = Badge(ratio >= ContrastGridService.AaaLarge, "AAA " + pick("Large", "大字"), pick);
            AaaLargeBrush = Score(ratio >= ContrastGridService.AaaLarge);
        }

        private static string Badge(bool pass, string label, Func<string, string, string> pick)
            => $"{label} {(pass ? pick("Pass", "合格") : pick("Fail", "唔合格"))}";

        private static Brush Score(bool pass) => pass ? PassBrush : FailBrush;
    }
}
