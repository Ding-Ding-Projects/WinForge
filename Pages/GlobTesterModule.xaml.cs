using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Glob 樣式測試器 · Glob pattern tester — type a glob (*, **, ?, [abc], {a,b,c}),
/// see the generated regex, and test paths line-by-line with coloured match badges.
/// Pure managed; the compiled regex runs the matching. Never throws. Bilingual (粵語).
/// </summary>
public sealed partial class GlobTesterModule : Page
{
    // Badge colours (qualified Windows.UI.Color to avoid the Microsoft.UI `Colors` ambiguity).
    private static readonly SolidColorBrush MatchBrush = new(Color.FromArgb(0xFF, 0x2E, 0x7D, 0x32));   // green
    private static readonly SolidColorBrush NoMatchBrush = new(Color.FromArgb(0xFF, 0x9E, 0x9E, 0x9E)); // grey

    public sealed class Row
    {
        public string Path { get; set; } = "";
        public string BadgeText { get; set; } = "";
        public Brush BadgeBrush { get; set; } = NoMatchBrush;
    }

    private readonly ObservableCollection<Row> _rows = new();

    public GlobTesterModule()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Glob Tester · Glob 樣式測試器";
        HeaderBlurb.Text = P(
            "Write a glob pattern, watch it compile to a regex, and test paths line by line. Supports * (within a segment), ** (across /), ?, character classes [a-z], negation [!x], and brace alternation {a,b,c}.",
            "打一個 glob 樣式，睇住佢即時編譯成 regex，再逐行測試路徑。支援 *（同一段內）、**（跨越 /）、?、字元類別 [a-z]、否定 [!x]，同埋大括號選擇 {a,b,c}。");
        PatternLabel.Text = P("Glob pattern", "Glob 樣式");
        CaseLabel.Text = P("Case-insensitive", "唔分大細楷");
        DotLabel.Text = P("Dot-files match wildcards", "點檔案都俾萬用字元配對");
        RegexLabel.Text = P("Generated regex (read-only)", "產生嘅 regex（唯讀）");
        CopyBtn.Content = P("Copy", "複製");
        PathsLabel.Text = P("Paths (one per line)", "路徑（一行一個）");
        ResultsLabel.Text = P("Results", "結果");
        Evaluate();
    }

    private void Input_Changed(object sender, RoutedEventArgs e) => Evaluate();

    private void Evaluate()
    {
        // Guard against calls before InitializeComponent finishes wiring named parts.
        if (PatternBox is null || RegexBox is null || StatusBar is null) return;

        try
        {
            string glob = PatternBox.Text ?? "";
            bool ci = CaseSwitch?.IsOn == true;
            bool dot = DotSwitch?.IsOn == true;

            var result = GlobTesterService.Compile(glob, ci, dot);
            RegexBox.Text = result.Ok ? result.Regex : "";

            if (string.IsNullOrEmpty(glob))
            {
                _rows.Clear();
                SetStatus(InfoBarSeverity.Informational,
                    P("Enter a glob pattern to begin.", "輸入一個 glob 樣式先開始。"));
                return;
            }

            if (!result.Ok)
            {
                _rows.Clear();
                SetStatus(InfoBarSeverity.Error, P(result.ErrorEn ?? "Invalid pattern.", result.ErrorZh ?? "樣式無效。"));
                return;
            }

            var paths = SplitLines(PathsBox?.Text ?? "");
            _rows.Clear();
            int matched = 0;
            foreach (var path in paths)
            {
                bool ok = GlobTesterService.IsMatch(result.Matcher, path);
                if (ok) matched++;
                _rows.Add(new Row
                {
                    Path = path,
                    BadgeText = ok ? P("match", "配對") : P("no match", "唔配對"),
                    BadgeBrush = ok ? MatchBrush : NoMatchBrush,
                });
            }

            if (paths.Count == 0)
                SetStatus(InfoBarSeverity.Success,
                    P("Pattern compiled. Add some paths to test.", "樣式已編譯。加啲路徑嚟測試。"));
            else
                SetStatus(InfoBarSeverity.Success,
                    P($"Compiled OK — {matched}/{paths.Count} path(s) matched.",
                      $"編譯成功 — {matched}/{paths.Count} 個路徑配對到。"));
        }
        catch (Exception ex)
        {
            _rows.Clear();
            SetStatus(InfoBarSeverity.Error, P("Unexpected error: " + ex.Message, "意外錯誤：" + ex.Message));
        }
    }

    private static List<string> SplitLines(string text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) return list;
        foreach (var raw in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length > 0) list.Add(line);
        }
        return list;
    }

    private void SetStatus(InfoBarSeverity severity, string message)
    {
        StatusBar.Severity = severity;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = RegexBox?.Text ?? "";
            if (string.IsNullOrEmpty(text))
            {
                SetStatus(InfoBarSeverity.Informational, P("Nothing to copy yet.", "而家冇嘢可以複製。"));
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            SetStatus(InfoBarSeverity.Success, P("Regex copied to clipboard.", "regex 已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SetStatus(InfoBarSeverity.Error, P("Copy failed: " + ex.Message, "複製失敗：" + ex.Message));
        }
    }
}
