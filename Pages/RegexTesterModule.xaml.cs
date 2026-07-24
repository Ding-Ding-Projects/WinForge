using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
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

    private IReadOnlyList<RegexTesterService.MatchHit> _lastMatches = Array.Empty<RegexTesterService.MatchHit>();
    private RegexBuilderService.PieceKind? _lastBuilderKind;
    private bool _renderingBuilder;

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

        EngineText.Text = P(
            ".NET 11 System.Text.RegularExpressions dialect · backslash escaping · local-only evaluation · 1-second timeout · bounded input and results.",
            ".NET 11 System.Text.RegularExpressions 方言 · 反斜線跳脫 · 只喺本機運算 · 1 秒超時 · 輸入同結果都有上限。");

        PatternBox.Header = P("Pattern", "表達式");
        PatternBox.PlaceholderText = P("e.g. (?<word>\\w+)", "例如 (?<word>\\w+)");

        BuilderExpander.Header = P("Guided pattern builder", "引導式表達式砌法");
        BuilderIntroText.Text = P(
            "Choose a piece, fill its fields, then insert it at the pattern cursor. Literal and character-class input is escaped for the .NET dialect; group, alternation, and quantifier sub-patterns stay raw.",
            "揀一件、填欄位，再插入表達式游標位置。字面文字同字元類會按 .NET 方言安全跳脫；群組、二選一同量詞入面嘅子表達式會保留原樣。");
        AddPieceButton.Content = P("Insert piece", "插入呢件");
        ClearPatternButton.Content = P("Clear pattern", "清除表達式");
        CopyPatternButton.Content = P("Copy pattern", "複製表達式");
        CopyMatchesButton.Content = P("Copy matches", "複製配對");
        RenderBuilderChoices();

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

    private void BuilderKind_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_renderingBuilder || BuilderKindBox is null) return;
        ConfigureBuilderFields(resetOption: _lastBuilderKind != SelectedBuilderKind());
    }

    private void RenderBuilderChoices()
    {
        if (BuilderKindBox is null || BuilderVariantBox is null) return;

        var selectedKind = SelectedBuilderKind();
        var selectedAnchor = SelectedAnchorKind();
        _renderingBuilder = true;
        try
        {
            BuilderKindBox.Header = P("Piece", "組件");
            BuilderKindBox.Items.Clear();
            AddChoice(BuilderKindBox, P("Literal text", "字面文字"), RegexBuilderService.PieceKind.Literal);
            AddChoice(BuilderKindBox, P("Character class", "字元類"), RegexBuilderService.PieceKind.CharacterClass);
            AddChoice(BuilderKindBox, P("Anchor", "錨點"), RegexBuilderService.PieceKind.Anchor);
            AddChoice(BuilderKindBox, P("Group", "群組"), RegexBuilderService.PieceKind.Group);
            AddChoice(BuilderKindBox, P("Alternation", "二選一"), RegexBuilderService.PieceKind.Alternation);
            AddChoice(BuilderKindBox, P("Quantifier", "量詞"), RegexBuilderService.PieceKind.Quantifier);
            SelectTag(BuilderKindBox, selectedKind);

            BuilderVariantBox.Header = P("Anchor", "錨點");
            BuilderVariantBox.Items.Clear();
            AddChoice(BuilderVariantBox, P("Start of string (\\A)", "字串開頭（\\A）"), RegexBuilderService.AnchorKind.StartOfString);
            AddChoice(BuilderVariantBox, P("End of string (\\z)", "字串結尾（\\z）"), RegexBuilderService.AnchorKind.EndOfString);
            AddChoice(BuilderVariantBox, P("Start of line (^)", "行首（^）"), RegexBuilderService.AnchorKind.StartOfLine);
            AddChoice(BuilderVariantBox, P("End of line ($)", "行尾（$）"), RegexBuilderService.AnchorKind.EndOfLine);
            AddChoice(BuilderVariantBox, P("Word boundary (\\b)", "字詞邊界（\\b）"), RegexBuilderService.AnchorKind.WordBoundary);
            AddChoice(BuilderVariantBox, P("Non-word boundary (\\B)", "非字詞邊界（\\B）"), RegexBuilderService.AnchorKind.NonWordBoundary);
            SelectTag(BuilderVariantBox, selectedAnchor);
        }
        finally
        {
            _renderingBuilder = false;
        }

        ConfigureBuilderFields(resetOption: false);
    }

    private void ConfigureBuilderFields(bool resetOption)
    {
        if (BuilderPrimaryBox is null || BuilderSecondaryBox is null || BuilderOptionCheck is null) return;

        var kind = SelectedBuilderKind();
        _lastBuilderKind = kind;
        if (resetOption) BuilderOptionCheck.IsChecked = false;

        BuilderVariantBox.Visibility = kind == RegexBuilderService.PieceKind.Anchor
            ? Visibility.Visible : Visibility.Collapsed;
        BuilderPrimaryBox.Visibility = kind == RegexBuilderService.PieceKind.Anchor
            ? Visibility.Collapsed : Visibility.Visible;
        BuilderSecondaryBox.Visibility = kind is RegexBuilderService.PieceKind.Group
            or RegexBuilderService.PieceKind.Alternation or RegexBuilderService.PieceKind.Quantifier
            ? Visibility.Visible : Visibility.Collapsed;
        BuilderOptionCheck.Visibility = kind is RegexBuilderService.PieceKind.CharacterClass
            or RegexBuilderService.PieceKind.Group ? Visibility.Visible : Visibility.Collapsed;

        switch (kind)
        {
            case RegexBuilderService.PieceKind.Literal:
                BuilderPrimaryBox.Header = P("Literal text", "字面文字");
                BuilderPrimaryBox.PlaceholderText = P("e.g. price: $5", "例如 price: $5");
                break;
            case RegexBuilderService.PieceKind.CharacterClass:
                BuilderPrimaryBox.Header = P("Characters", "字元");
                BuilderPrimaryBox.PlaceholderText = P("e.g. abc-]", "例如 abc-]");
                BuilderOptionCheck.Content = P("Negate the class", "反轉呢個字元類");
                break;
            case RegexBuilderService.PieceKind.Anchor:
                break;
            case RegexBuilderService.PieceKind.Group:
                BuilderPrimaryBox.Header = P("Sub-pattern (raw)", "子表達式（原樣）");
                BuilderPrimaryBox.PlaceholderText = @"\w+";
                BuilderSecondaryBox.Header = P("Optional group name", "可選群組名");
                BuilderSecondaryBox.PlaceholderText = P("e.g. word", "例如 word");
                BuilderOptionCheck.Content = P("Non-capturing group", "唔擷取群組");
                break;
            case RegexBuilderService.PieceKind.Alternation:
                BuilderPrimaryBox.Header = P("Left branch (raw)", "左邊分支（原樣）");
                BuilderPrimaryBox.PlaceholderText = P("e.g. cat", "例如 cat");
                BuilderSecondaryBox.Header = P("Right branch (raw)", "右邊分支（原樣）");
                BuilderSecondaryBox.PlaceholderText = P("e.g. dog", "例如 dog");
                break;
            case RegexBuilderService.PieceKind.Quantifier:
                BuilderPrimaryBox.Header = P("Atom or sub-pattern (raw)", "原子或子表達式（原樣）");
                BuilderPrimaryBox.PlaceholderText = @"\d";
                BuilderSecondaryBox.Header = P("Quantity", "次數");
                BuilderSecondaryBox.PlaceholderText = P("*, +, ?, 3, 2, or 2,5", "*、+、?、3、2, 或 2,5");
                break;
        }
    }

    private void AddPiece_Click(object sender, RoutedEventArgs e)
    {
        int selectionStart = PatternBox.SelectionStart;
        int selectionLength = PatternBox.SelectionLength;
        string primary = BuilderPrimaryBox.Text ?? string.Empty;
        var kind = SelectedBuilderKind();

        if (primary.Length == 0 && selectionLength > 0 && kind is RegexBuilderService.PieceKind.Group
            or RegexBuilderService.PieceKind.Quantifier)
            primary = PatternBox.Text.Substring(selectionStart, selectionLength);

        var result = RegexBuilderService.Build(kind, primary, BuilderSecondaryBox.Text,
            BuilderOptionCheck.IsChecked == true, SelectedAnchorKind());
        if (!result.Ok)
        {
            SetBuilderStatus(BuilderError(result.Error), error: true);
            return;
        }

        try
        {
            PatternBox.Text = RegexBuilderService.InsertAtSelection(
                PatternBox.Text, result.Token, selectionStart, selectionLength);
            PatternBox.SelectionStart = selectionStart + result.Token.Length;
            PatternBox.SelectionLength = 0;
            PatternBox.Focus(FocusState.Programmatic);
            SetBuilderStatus(P($"Inserted {result.Token}", $"已插入 {result.Token}"), error: false);
        }
        catch (ArgumentOutOfRangeException)
        {
            SetBuilderStatus(P("The resulting pattern exceeds the 4,096-character safety limit.",
                "完成後嘅表達式超過 4,096 個字元安全上限。"), error: true);
        }
    }

    private void ClearPattern_Click(object sender, RoutedEventArgs e)
    {
        PatternBox.Text = string.Empty;
        PatternBox.Focus(FocusState.Programmatic);
        SetBuilderStatus(P("Pattern cleared.", "表達式已清除。"), error: false);
    }

    private void CopyPattern_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PatternBox.Text))
        {
            SetBuilderStatus(P("There is no pattern to copy.", "冇表達式可以複製。"), error: true);
            return;
        }

        CopyText(PatternBox.Text, P("Pattern copied.", "表達式已複製。"));
    }

    private void CopyMatches_Click(object sender, RoutedEventArgs e)
    {
        if (_lastMatches.Count == 0)
        {
            SetBuilderStatus(P("There are no matches to copy.", "冇配對可以複製。"), error: true);
            return;
        }

        var text = new StringBuilder();
        foreach (var match in _lastMatches)
        {
            text.Append("#").Append(match.Number).Append(" @ ").Append(match.Index)
                .Append(" + ").Append(match.Length).Append(": ").AppendLine(match.Value);
            foreach (var group in match.Groups)
                text.Append("  ").Append(group.Name).Append(" @ ").Append(group.Index)
                    .Append(" + ").Append(group.Length).Append(": ").AppendLine(group.Value);
        }
        CopyText(text.ToString(), P("Matches and capture groups copied.", "配對同擷取群組已複製。"));
    }

    private void CopyText(string text, string success)
    {
        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(text);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            SetBuilderStatus(success, error: false);
        }
        catch (Exception ex)
        {
            SetBuilderStatus(P($"Clipboard copy failed: {ex.Message}", $"複製去剪貼簿失敗：{ex.Message}"), error: true);
        }
    }

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
            _lastMatches = Array.Empty<RegexTesterService.MatchHit>();
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
            : result.MatchesTruncated
                ? P($"Pattern OK. Showing the first {RegexTesterService.MaxMatches:N0} matches.",
                    $"表達式正常。顯示頭 {RegexTesterService.MaxMatches:N0} 個配對。")
                : P("Pattern OK.", "表達式正常。");

        _lastMatches = result.Matches;
        MatchCountText.Text = result.MatchesTruncated
            ? P($"{result.Matches.Count}+ match(es), results capped", $"{result.Matches.Count}+ 個配對，結果已封頂")
            : P($"{result.Matches.Count} match(es)", $"{result.Matches.Count} 個配對");
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

    private RegexBuilderService.PieceKind SelectedBuilderKind() =>
        (BuilderKindBox?.SelectedItem as ComboBoxItem)?.Tag is RegexBuilderService.PieceKind kind
            ? kind : RegexBuilderService.PieceKind.Literal;

    private RegexBuilderService.AnchorKind SelectedAnchorKind() =>
        (BuilderVariantBox?.SelectedItem as ComboBoxItem)?.Tag is RegexBuilderService.AnchorKind anchor
            ? anchor : RegexBuilderService.AnchorKind.StartOfString;

    private static void AddChoice(ComboBox box, string content, object tag) =>
        box.Items.Add(new ComboBoxItem { Content = content, Tag = tag });

    private static void SelectTag(ComboBox box, object tag)
    {
        for (int index = 0; index < box.Items.Count; index++)
        {
            if (box.Items[index] is ComboBoxItem item && Equals(item.Tag, tag))
            {
                box.SelectedIndex = index;
                return;
            }
        }
        box.SelectedIndex = box.Items.Count > 0 ? 0 : -1;
    }

    private string BuilderError(RegexBuilderService.PieceError error) => error switch
    {
        RegexBuilderService.PieceError.EmptyLiteral => P("Enter literal text to escape.", "請輸入要跳脫嘅字面文字。"),
        RegexBuilderService.PieceError.EmptyCharacterClass => P("Enter one or more characters for the class.", "請為字元類輸入至少一個字元。"),
        RegexBuilderService.PieceError.EmptyGroup => P("Enter the group sub-pattern.", "請輸入群組子表達式。"),
        RegexBuilderService.PieceError.InvalidGroupName => P(
            "Group names must start with an ASCII letter and contain only letters, digits, or underscore.",
            "群組名要由英文字母開頭，而且只可以有英文字母、數字或者底線。"),
        RegexBuilderService.PieceError.MissingAlternationBranch => P("Enter both alternation branches.", "請輸入二選一嘅左右兩個分支。"),
        RegexBuilderService.PieceError.EmptyQuantifierAtom => P("Enter the atom or sub-pattern to repeat.", "請輸入要重複嘅原子或者子表達式。"),
        RegexBuilderService.PieceError.InvalidQuantity => P("Use *, +, ?, n, n,, or n,m for the quantity.", "次數請用 *、+、?、n、n, 或者 n,m。"),
        RegexBuilderService.PieceError.InvalidMinimum => P("The minimum must be between 0 and 100,000.", "最少次數要喺 0 至 100,000 之間。"),
        RegexBuilderService.PieceError.InvalidMaximum => P("The maximum must be between the minimum and 100,000.", "最多次數要介乎最少次數同 100,000 之間。"),
        _ => P("That builder piece is not supported.", "未支援呢個組件。"),
    };

    private void SetBuilderStatus(string message, bool error)
    {
        BuilderStatusText.Text = message;
        BuilderStatusText.Foreground = error
            ? new SolidColorBrush(Colors.OrangeRed)
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }
}
