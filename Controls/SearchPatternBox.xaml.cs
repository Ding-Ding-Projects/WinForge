using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// Plain-text-first search field with a synchronized, progressively disclosed full .NET regex builder.
/// Patterns and samples stay session-only and evaluation is bounded by the shared regex services.
/// </summary>
public sealed partial class SearchPatternBox : UserControl
{
    private const int PreviewMatchLimit = 24;
    private readonly SearchPatternSession _session = new();
    private bool _syncing;
    private bool _languageSubscribed;
    private bool _toneSubscribed;
    private bool _builderChoicesReady;
    private string _automationName = string.Empty;
    private string _automationIdPrefix = "SearchPattern";
    private Func<string>? _automationNameProvider;

    public SearchPatternBox()
    {
        InitializeComponent();
        _session.Changed += Session_Changed;
        RegexModeButton.IsChecked = false;
        IgnoreCaseCheck.IsChecked = true;
        MultilineCheck.IsChecked = false;
        SinglelineCheck.IsChecked = false;
        IgnoreWhitespaceCheck.IsChecked = false;
        ExplicitCaptureCheck.IsChecked = false;
        PieceOptionCheck.IsChecked = false;
        RenderText();
    }

    public event EventHandler? PatternChanged;
    public event EventHandler? QuerySubmitted;
    public event EventHandler<KeyRoutedEventArgs>? QueryKeyDown;

    public string Text
    {
        get => QueryBox.Text ?? string.Empty;
        set
        {
            string normalized = value ?? string.Empty;
            if (QueryBox.Text == normalized && _session.Query == normalized) return;
            QueryBox.Text = normalized;
            _session.Query = normalized;
        }
    }

    public string PlaceholderText
    {
        get => QueryBox.PlaceholderText ?? string.Empty;
        set => QueryBox.PlaceholderText = value ?? string.Empty;
    }

    public string AutomationName
    {
        get => _automationName;
        set
        {
            _automationName = value ?? string.Empty;
            ApplyAutomationNames();
        }
    }

    /// <summary>Namespaces descendant automation IDs when several search controls share a surface.</summary>
    public string AutomationIdPrefix
    {
        get => _automationIdPrefix;
        set
        {
            _automationIdPrefix = string.IsNullOrWhiteSpace(value) ? "SearchPattern" : value.TrimEnd('_');
            ApplyAutomationIds();
        }
    }

    /// <summary>Optional host width cap for nested builder surfaces.</summary>
    public double MaxLayoutWidth { get; set; } = double.PositiveInfinity;

    /// <summary>Provides a language-refreshable base name for the composite search control.</summary>
    public Func<string>? AutomationNameProvider
    {
        get => _automationNameProvider;
        set
        {
            _automationNameProvider = value;
            ApplyAutomationNames();
        }
    }

    /// <summary>Refreshes descendant accessible names after a host-owned tone update.</summary>
    public void RefreshAutomationNames() => ApplyAutomationNames();

    public SearchPatternService.Spec Spec => _session.Spec;
    public bool IsRegexMode => _session.UseRegex;
    public string? ValidationError => _session.Compile().Error;

    public SearchPatternService.Matcher CompileMatcher() => _session.Compile();
    public SearchPatternService.MatchResult Match(string? candidate) => _session.Match(candidate);
    public SearchPatternService.MatchResult MatchAny(IEnumerable<string?> candidates) => _session.MatchAny(candidates);

    /// <summary>Focuses the real editable query child rather than the composite wrapper.</summary>
    public void FocusQuery() => QueryBox.Focus(FocusState.Programmatic);

    public void Clear()
    {
        _session.Apply(new SearchPatternService.Spec(string.Empty));
        SampleBox.Text = string.Empty;
    }

    private string P(string en, string zh)
        => Loc.I.Pick(FunnyLevelSettings.I.StyleEnglish(en), FunnyLevelSettings.I.StyleCantonese(zh));

    private void Control_Loaded(object sender, RoutedEventArgs e)
    {
        if (!_languageSubscribed)
        {
            Loc.I.LanguageChanged += OnLanguageChanged;
            _languageSubscribed = true;
        }
        if (!_toneSubscribed)
        {
            FunnyLevelSettings.I.Changed += OnToneChanged;
            _toneSubscribed = true;
        }
        EnsureBuilderChoices();
        RenderText();
        SyncControlsFromSession();
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_languageSubscribed)
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _languageSubscribed = false;
        }
        if (_toneSubscribed)
        {
            FunnyLevelSettings.I.Changed -= OnToneChanged;
            _toneSubscribed = false;
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        RebuildBuilderChoices();
        UpdatePreview();
    }

    private void OnToneChanged(object? sender, EventArgs e)
    {
        RenderText();
        ApplyAutomationNames();
        UpdatePreview();
    }

    private void QueryBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_syncing) return;
        _session.Query = sender.Text ?? string.Empty;
    }

    private void QueryBox_KeyDown(object sender, KeyRoutedEventArgs e)
        => QueryKeyDown?.Invoke(this, e);

    private void QueryBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        => QuerySubmitted?.Invoke(this, EventArgs.Empty);

    private void RawPatternBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing) return;
        _session.Query = RawPatternBox.Text ?? string.Empty;
    }

    private void RegexModeButton_Click(object sender, RoutedEventArgs e)
        => _session.UseRegex = RegexModeButton.IsChecked == true;

    private void Flag_Click(object sender, RoutedEventArgs e)
    {
        if (_syncing) return;
        _session.IgnoreCase = IgnoreCaseCheck.IsChecked == true;
        _session.Multiline = MultilineCheck.IsChecked == true;
        _session.Singleline = SinglelineCheck.IsChecked == true;
        _session.IgnorePatternWhitespace = IgnoreWhitespaceCheck.IsChecked == true;
        _session.ExplicitCapture = ExplicitCaptureCheck.IsChecked == true;
    }

    private void Session_Changed(object? sender, EventArgs e)
    {
        SyncControlsFromSession();
        UpdateValidation();
        UpdatePreview();
        PatternChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SyncControlsFromSession()
    {
        _syncing = true;
        try
        {
            if (QueryBox.Text != _session.Query) QueryBox.Text = _session.Query;
            if (RawPatternBox.Text != _session.Query) RawPatternBox.Text = _session.Query;
            RegexModeButton.IsChecked = _session.UseRegex;
            IgnoreCaseCheck.IsChecked = _session.IgnoreCase;
            MultilineCheck.IsChecked = _session.Multiline;
            SinglelineCheck.IsChecked = _session.Singleline;
            IgnoreWhitespaceCheck.IsChecked = _session.IgnorePatternWhitespace;
            ExplicitCaptureCheck.IsChecked = _session.ExplicitCapture;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void BuilderFlyout_Opening(object sender, object e)
    {
        EnsureBuilderChoices();
        _session.UseRegex = true;
        double available = XamlRoot?.Size.Width ?? 600;
        double capped = Math.Min(available - 72, MaxLayoutWidth);
        double minimum = double.IsFinite(MaxLayoutWidth) ? Math.Max(1, Math.Min(216, MaxLayoutWidth)) : 216;
        BuilderPanel.Width = Math.Clamp(capped, minimum, 600);
        RawPatternBox.Focus(FocusState.Programmatic);
        UpdatePreview();
    }

    private void SampleBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdateValidation()
    {
        SearchPatternService.Matcher matcher = _session.Compile();
        bool invalid = _session.UseRegex && !matcher.Ok;
        ValidationText.Visibility = invalid ? Visibility.Visible : Visibility.Collapsed;
        ValidationText.Text = invalid
            ? P($"Invalid .NET regex: {matcher.Error}", $".NET 正則表達式錯誤：{matcher.Error}")
            : string.Empty;
        AutomationProperties.SetHelpText(QueryBox, ValidationText.Text);

        SyntaxBar.IsOpen = _session.UseRegex;
        SyntaxBar.Severity = invalid ? InfoBarSeverity.Error : InfoBarSeverity.Success;
        SyntaxBar.Title = invalid ? P("Pattern error", "表達式錯誤") : P("Pattern ready", "表達式可用");
        SyntaxBar.Message = invalid
            ? matcher.Error ?? P("Invalid pattern.", "表達式錯誤。")
            : P("This exact pattern and flags are active in the search results.", "搜尋結果正使用呢個表達式同旗標。 ");
    }

    private void UpdatePreview()
    {
        if (!_session.UseRegex || PreviewText is null) return;
        RegexTesterService.EvalResult result = _session.Preview(SampleBox.Text);
        if (!result.Ok)
        {
            PreviewText.Text = result.Error ?? P("Invalid pattern.", "表達式錯誤。");
            PreviewText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
            return;
        }

        PreviewText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var output = new StringBuilder();
        output.Append(P($"{result.Matches.Count} match(es)", $"{result.Matches.Count} 個配對"));
        if (result.MatchesTruncated) output.Append(P(" (capped)", "（已封頂）"));
        foreach (RegexTesterService.MatchHit match in result.Matches)
        {
            if (match.Number > PreviewMatchLimit)
            {
                output.AppendLine().Append(P("… preview shortened", "… 預覽已縮短"));
                break;
            }

            output.AppendLine().Append('#').Append(match.Number).Append(" @ ")
                .Append(match.Index).Append(" + ").Append(match.Length).Append(": ").Append(match.Value);
            foreach (RegexTesterService.GroupHit group in match.Groups)
                output.AppendLine().Append("  ").Append(group.Name).Append(" @ ")
                    .Append(group.Index).Append(" + ").Append(group.Length).Append(": ").Append(group.Value);
        }
        PreviewText.Text = output.ToString();
    }

    private void EnsureBuilderChoices()
    {
        if (_builderChoicesReady) return;
        _builderChoicesReady = true;
        RebuildBuilderChoices();
    }

    private void RebuildBuilderChoices()
    {
        RegexBuilderService.PieceKind kind = SelectedPieceKind();
        RegexBuilderService.AnchorKind anchor = SelectedAnchorKind();
        PieceKindBox.Items.Clear();
        AddChoice(PieceKindBox, P("Literal text", "字面文字"), RegexBuilderService.PieceKind.Literal);
        AddChoice(PieceKindBox, P("Character class", "字元類"), RegexBuilderService.PieceKind.CharacterClass);
        AddChoice(PieceKindBox, P("Anchor", "錨點"), RegexBuilderService.PieceKind.Anchor);
        AddChoice(PieceKindBox, P("Group", "群組"), RegexBuilderService.PieceKind.Group);
        AddChoice(PieceKindBox, P("Alternation", "二選一"), RegexBuilderService.PieceKind.Alternation);
        AddChoice(PieceKindBox, P("Quantifier", "量詞"), RegexBuilderService.PieceKind.Quantifier);
        SelectTag(PieceKindBox, kind);

        AnchorBox.Items.Clear();
        AddChoice(AnchorBox, P("Start of string (\\A)", "字串開頭（\\A）"), RegexBuilderService.AnchorKind.StartOfString);
        AddChoice(AnchorBox, P("End of string (\\z)", "字串結尾（\\z）"), RegexBuilderService.AnchorKind.EndOfString);
        AddChoice(AnchorBox, P("Start of line (^)", "行首（^）"), RegexBuilderService.AnchorKind.StartOfLine);
        AddChoice(AnchorBox, P("End of line ($)", "行尾（$）"), RegexBuilderService.AnchorKind.EndOfLine);
        AddChoice(AnchorBox, P("Word boundary (\\b)", "字詞邊界（\\b）"), RegexBuilderService.AnchorKind.WordBoundary);
        AddChoice(AnchorBox, P("Non-word boundary (\\B)", "非字詞邊界（\\B）"), RegexBuilderService.AnchorKind.NonWordBoundary);
        SelectTag(AnchorBox, anchor);
        RenderPieceInputs();
    }

    private void PieceKindBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RenderPieceInputs();

    private void RenderPieceInputs()
    {
        if (PieceKindBox is null) return;
        RegexBuilderService.PieceKind kind = SelectedPieceKind();
        AnchorBox.Visibility = kind == RegexBuilderService.PieceKind.Anchor ? Visibility.Visible : Visibility.Collapsed;
        PiecePrimaryBox.Visibility = kind == RegexBuilderService.PieceKind.Anchor ? Visibility.Collapsed : Visibility.Visible;
        PieceSecondaryBox.Visibility = kind is RegexBuilderService.PieceKind.Group
            or RegexBuilderService.PieceKind.Alternation or RegexBuilderService.PieceKind.Quantifier
            ? Visibility.Visible : Visibility.Collapsed;
        PieceOptionCheck.Visibility = kind is RegexBuilderService.PieceKind.CharacterClass
            or RegexBuilderService.PieceKind.Group ? Visibility.Visible : Visibility.Collapsed;

        PiecePrimaryBox.Header = kind switch
        {
            RegexBuilderService.PieceKind.Literal => P("Literal text to escape", "要跳脫嘅字面文字"),
            RegexBuilderService.PieceKind.CharacterClass => P("Characters", "字元"),
            RegexBuilderService.PieceKind.Group => P("Sub-pattern (raw)", "子表達式（原樣）"),
            RegexBuilderService.PieceKind.Alternation => P("Left branch (raw)", "左邊分支（原樣）"),
            RegexBuilderService.PieceKind.Quantifier => P("Atom or sub-pattern (raw)", "原子或子表達式（原樣）"),
            _ => string.Empty,
        };
        PieceSecondaryBox.Header = kind switch
        {
            RegexBuilderService.PieceKind.Group => P("Optional group name", "可選群組名"),
            RegexBuilderService.PieceKind.Alternation => P("Right branch (raw)", "右邊分支（原樣）"),
            RegexBuilderService.PieceKind.Quantifier => P("Quantity: *, +, ?, n, n,, or n,m", "次數：*、+、?、n、n, 或 n,m"),
            _ => string.Empty,
        };
        PieceOptionCheck.Content = kind == RegexBuilderService.PieceKind.CharacterClass
            ? P("Negate the character class", "反轉呢個字元類")
            : P("Use a non-capturing group", "使用唔擷取群組");
    }

    private void AddPiece_Click(object sender, RoutedEventArgs e)
    {
        RegexBuilderService.PieceResult result = RegexBuilderService.Build(
            SelectedPieceKind(),
            PiecePrimaryBox.Text,
            PieceSecondaryBox.Text,
            PieceOptionCheck.IsChecked == true,
            SelectedAnchorKind());
        if (!result.Ok)
        {
            SetPieceStatus(P("Complete the selected builder fields before inserting.", "插入前請先填妥所選砌法欄位。"), true);
            return;
        }

        try
        {
            int start = RawPatternBox.SelectionStart;
            int length = RawPatternBox.SelectionLength;
            string pattern = RegexBuilderService.InsertAtSelection(_session.Query, result.Token, start, length);
            _session.Query = pattern;
            RawPatternBox.SelectionStart = Math.Min(pattern.Length, start + result.Token.Length);
            RawPatternBox.SelectionLength = 0;
            RawPatternBox.Focus(FocusState.Programmatic);
            SetPieceStatus(P($"Inserted {result.Token}", $"已插入 {result.Token}"), false);
        }
        catch (ArgumentOutOfRangeException)
        {
            SetPieceStatus(P("The result exceeds the 4,096-character safety limit.", "結果超過 4,096 個字元安全上限。"), true);
        }
    }

    private void ClearPattern_Click(object sender, RoutedEventArgs e)
    {
        _session.Query = string.Empty;
        RawPatternBox.Focus(FocusState.Programmatic);
        SetPieceStatus(P("Pattern cleared.", "表達式已清除。"), false);
    }

    private void CopyPattern_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_session.Query))
        {
            SetPieceStatus(P("There is no pattern to copy.", "冇表達式可以複製。"), true);
            return;
        }

        try
        {
            var package = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            package.SetText(_session.Query);
            Clipboard.SetContent(package);
            Clipboard.Flush();
            SetPieceStatus(P("Pattern copied.", "表達式已複製。"), false);
        }
        catch (Exception ex)
        {
            SetPieceStatus(P($"Copy failed: {ex.Message}", $"複製失敗：{ex.Message}"), true);
        }
    }

    private void SetPieceStatus(string text, bool error)
    {
        PieceStatusText.Text = text;
        PieceStatusText.Foreground = error
            ? new SolidColorBrush(Microsoft.UI.Colors.OrangeRed)
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void RenderText()
    {
        BuilderTitle.Text = P("Regex builder for this search", "呢個搜尋嘅正則砌法");
        EngineText.Text = P(
            ".NET System.Text.RegularExpressions · local bounded evaluation · 250 ms search timeout · literals are escaped by the guided builder.",
            ".NET System.Text.RegularExpressions · 本機有限評估 · 搜尋超時 250 ms · 引導砌法會跳脫字面文字。");
        RawPatternBox.Header = P("Raw .NET regex pattern", "原樣 .NET 正則表達式");
        IgnoreCaseCheck.Content = P("Ignore case", "忽略大小寫");
        MultilineCheck.Content = P("Multiline (^ and $ per line)", "多行（^ 同 $ 逐行）");
        SinglelineCheck.Content = P("Singleline (. includes newline)", "單行（. 包括換行）");
        IgnoreWhitespaceCheck.Content = P("Ignore pattern whitespace", "忽略表達式空白");
        ExplicitCaptureCheck.Content = P("Explicit captures only", "只用明確擷取");
        GuidedExpander.Header = P("Guided construction", "引導砌法");
        AddPieceButton.Content = P("Insert piece", "插入組件");
        ClearPatternButton.Content = P("Clear pattern", "清除表達式");
        SampleBox.Header = P("Session-only sample text", "只限今次工作階段嘅範例文字");
        SampleBox.PlaceholderText = P("Sample for live matches/captures", "即時配對／擷取範例");
        CopyPatternLabel.Text = P("Copy pattern", "複製表達式");
        ToolTipService.SetToolTip(RegexModeButton, P("Toggle .NET regex mode", "切換 .NET 正則模式"));
        ToolTipService.SetToolTip(BuilderButton, P("Open the full regex builder", "開啟完整正則砌法"));
        ApplyAutomationNames();
        ApplyAutomationIds();
        UpdateValidation();
    }

    private void ApplyAutomationIds()
    {
        string Id(string suffix) => _automationIdPrefix == "SearchPattern"
            ? $"SearchPattern{suffix}"
            : $"{_automationIdPrefix}_{suffix.TrimStart('_')}";

        AutomationProperties.SetAutomationId(QueryBox, Id("Query"));
        AutomationProperties.SetAutomationId(RegexModeButton, Id("RegexMode"));
        AutomationProperties.SetAutomationId(BuilderButton, Id("BuilderButton"));
        AutomationProperties.SetAutomationId(RawPatternBox, Id("RawPattern"));
        AutomationProperties.SetAutomationId(SampleBox, Id("Sample"));
        AutomationProperties.SetAutomationId(ValidationText, Id("Validation"));
    }

    private void ApplyAutomationNames()
    {
        string baseName = _automationNameProvider?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = string.IsNullOrWhiteSpace(_automationName) ? P("Search", "搜尋") : _automationName;
        AutomationProperties.SetName(QueryBox, baseName);
        AutomationProperties.SetName(RegexModeButton, P($"{baseName}: .NET regex mode", $"{baseName}：.NET 正則模式"));
        AutomationProperties.SetName(BuilderButton, P($"{baseName}: open full regex builder", $"{baseName}：開啟完整正則砌法"));
    }

    private RegexBuilderService.PieceKind SelectedPieceKind()
        => (PieceKindBox.SelectedItem as ComboBoxItem)?.Tag is RegexBuilderService.PieceKind kind
            ? kind : RegexBuilderService.PieceKind.Literal;

    private RegexBuilderService.AnchorKind SelectedAnchorKind()
        => (AnchorBox.SelectedItem as ComboBoxItem)?.Tag is RegexBuilderService.AnchorKind anchor
            ? anchor : RegexBuilderService.AnchorKind.StartOfString;

    private static void AddChoice(ComboBox box, string content, object tag)
        => box.Items.Add(new ComboBoxItem { Content = content, Tag = tag });

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
}
