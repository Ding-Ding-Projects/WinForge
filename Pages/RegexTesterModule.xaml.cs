using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 正則表達式測試器 · Live .NET regex tester — type a pattern + test input and see matches, groups and a
/// replacement preview update as you type. Pure managed <see cref="System.Text.RegularExpressions"/> with a
/// 1-second match timeout, so a bad or runaway pattern shows a red status instead of freezing the UI.
/// </summary>
public sealed partial class RegexTesterModule : Page
{
    /// <summary>Row shown in the results list (already-formatted, bilingual).</summary>
    private sealed record ResultRow(string Heading, string Value, string Groups, Visibility GroupsVisible);

    public RegexTesterModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Evaluate(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Regex Tester · 正則表達式測試器";
        HeaderBlurb.Text = P("Test a .NET regular expression live — matches, capture groups and a replacement preview update as you type. Runs with a 1-second safety timeout.",
            "即時測試 .NET 正則表達式 — 一路打，配對、擷取群組同替換預覽即刻更新。有 1 秒安全超時保護。");

        PatternBox.Header = P("Pattern", "表達式");
        PatternBox.PlaceholderText = P("e.g. (?<word>\\w+)", "例如 (?<word>\\w+)");

        IgnoreCaseChk.Content = P("IgnoreCase", "忽略大小寫");
        MultilineChk.Content = P("Multiline", "多行");
        SinglelineChk.Content = P("Singleline", "單行");
        IgnoreWsChk.Content = P("IgnorePatternWhitespace", "忽略空白");
        ExplicitCaptureChk.Content = P("ExplicitCapture", "只顯式擷取");

        InputLabel.Text = P("Test input", "測試文字");
        ReplacementLabel.Text = P("Replacement ($1, ${name})", "替換（$1、${name}）");
        ResultLabel.Text = P("Result", "結果");
        CheatExpander.Header = P("Cheat sheet — common tokens", "速查表 — 常用符號");
        CheatText.Text = CheatSheet();

        Evaluate();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Evaluate();

    private void Option_Changed(object sender, RoutedEventArgs e) => Evaluate();

    private void Evaluate()
    {
        // Loaded event may not have fired yet during first Render(); guard the named controls.
        if (PatternBox is null || ResultsList is null) return;

        var options = RegexTesterService.BuildOptions(
            IgnoreCaseChk.IsChecked == true,
            MultilineChk.IsChecked == true,
            SinglelineChk.IsChecked == true,
            IgnoreWsChk.IsChecked == true,
            ExplicitCaptureChk.IsChecked == true);

        var result = RegexTesterService.Evaluate(PatternBox.Text, InputBox.Text, ReplacementBox.Text, options);

        if (!result.Ok)
        {
            StatusText.Text = result.Error ?? P("Invalid pattern.", "表達式錯誤。");
            StatusText.Foreground = new SolidColorBrush(Colors.OrangeRed);
            ResultsList.ItemsSource = null;
            MatchCountText.Text = string.Empty;
            ResultBox.Text = string.Empty;
            return;
        }

        StatusText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        StatusText.Text = string.IsNullOrEmpty(PatternBox.Text)
            ? P("Enter a pattern to begin.", "輸入表達式開始。")
            : P("Pattern OK.", "表達式正常。");

        MatchCountText.Text = P($"{result.Matches.Count} match(es)", $"{result.Matches.Count} 個配對");
        ResultsList.ItemsSource = BuildRows(result.Matches);
        ResultBox.Text = result.Replacement;
    }

    private List<ResultRow> BuildRows(IReadOnlyList<RegexTesterService.MatchHit> matches)
    {
        var rows = new List<ResultRow>();
        foreach (var m in matches)
        {
            string heading = P($"Match {m.Number} — index {m.Index}, length {m.Length}",
                $"配對 {m.Number} — 位置 {m.Index}，長度 {m.Length}");

            string groups = string.Empty;
            if (m.Groups.Count > 0)
            {
                var sb = new StringBuilder();
                foreach (var g in m.Groups)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(P($"group {g.Name}: \"{g.Value}\" (index {g.Index}, length {g.Length})",
                        $"群組 {g.Name}：「{g.Value}」（位置 {g.Index}，長度 {g.Length}）"));
                }
                groups = sb.ToString();
            }

            rows.Add(new ResultRow(heading, m.Value, groups,
                m.Groups.Count > 0 ? Visibility.Visible : Visibility.Collapsed));
        }
        return rows;
    }

    private string CheatSheet()
    {
        var lines = new[]
        {
            P(".   any character (except newline)", ".   任何字元（換行除外）"),
            P("\\d  digit   \\D  non-digit", "\\d  數字   \\D  非數字"),
            P("\\w  word char (a-z 0-9 _)   \\W  non-word", "\\w  字詞字元（a-z 0-9 _）   \\W  非字詞"),
            P("\\s  whitespace   \\S  non-whitespace", "\\s  空白   \\S  非空白"),
            P("^   start of line/string   $   end", "^   行/字串開頭   $   結尾"),
            P("\\b  word boundary", "\\b  字詞邊界"),
            P("*   0 or more   +   1 or more   ?   0 or 1", "*   零或多次   +   一或多次   ?   零或一次"),
            P("{n} exactly n   {n,} n+   {n,m} n to m", "{n} 剛好 n 次   {n,} n 次以上   {n,m} n 至 m 次"),
            P("[abc] any of a b c   [^abc] none of them", "[abc] a b c 其一   [^abc] 都唔係"),
            P("(...) group   (?<name>...) named group", "(...) 群組   (?<name>...) 具名群組"),
            P("(?:...) non-capturing group", "(?:...) 唔擷取嘅群組"),
            P("a|b   a or b", "a|b   a 或者 b"),
            P("\\.  \\*  escape a literal metacharacter", "\\.  \\*  轉義字面元字元"),
        };
        return string.Join("\n", lines);
    }
}
