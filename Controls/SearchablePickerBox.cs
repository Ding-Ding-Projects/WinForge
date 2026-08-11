using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// A keyboard-first picker whose anchored flyout owns a plain-text-first SearchPatternBox.
/// The selected value remains the caller's original object; filtering never changes its identity.
/// </summary>
public sealed class SearchablePickerBox : UserControl
{
    private sealed class Option
    {
        public required object Value { get; init; }
        public required string Text { get; init; }
        public required string[] SearchValues { get; init; }

        public override string ToString() => Text;
    }

    private readonly TextBlock _header = new();
    private readonly Button _pickerButton = new();
    private readonly TextBlock _selectedText = new();
    private readonly Flyout _flyout = new();
    private readonly SearchPatternBox _search = new();
    private readonly ListView _optionsList = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _searchLabel = new();
    private readonly StackPanel _flyoutPanel;
    private readonly List<Option> _allOptions = new();
    private readonly List<Option> _visibleOptions = new();
    private IEnumerable? _itemsSource;
    private bool _languageSubscribed;
    private bool _toneSubscribed;
    private object? _selectedItem;
    private int _pendingSelectedIndex = -1;
    private string _automationId = string.Empty;
    private string _searchAutomationId = "SearchablePickerSearchBox";
    private string _automationName = string.Empty;
    private Func<string>? _automationNameProvider;
    private Func<object, string>? _displayTextProvider;
    private Func<object, IEnumerable<string>>? _searchTextProvider;

    public SearchablePickerBox()
    {
        _header.FontSize = 12;
        _header.Foreground = SecondaryBrush();
        _header.Visibility = Visibility.Collapsed;

        _selectedText.TextWrapping = TextWrapping.Wrap;
        _selectedText.VerticalAlignment = VerticalAlignment.Center;
        _pickerButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        _pickerButton.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _pickerButton.MinHeight = 40;
        _pickerButton.Padding = new Thickness(12, 7, 10, 7);
        _pickerButton.Content = BuildPickerButtonContent();

        _search.PlaceholderText = P("Search options", "搜尋選項");
        _search.AutomationNameProvider = () =>
        {
            string name = _automationNameProvider?.Invoke() ?? _automationName;
            return string.IsNullOrWhiteSpace(name)
                ? P("Search picker options", "搜尋選擇器選項")
                : P($"{name}: search options", $"{name}：搜尋選項");
        };
        _search.PatternChanged += (_, _) => RenderOptions();
        _search.QueryKeyDown += Search_QueryKeyDown;
        _search.QuerySubmitted += (_, _) => CommitHighlightedOption();

        _optionsList.DisplayMemberPath = nameof(Option.Text);
        _optionsList.SelectionMode = ListViewSelectionMode.Single;
        _optionsList.MaxHeight = 320;
        _optionsList.IsTabStop = true;
        _optionsList.KeyDown += OptionsList_KeyDown;
        _optionsList.Tapped += OptionsList_Tapped;
        _optionsList.DoubleTapped += OptionsList_DoubleTapped;

        _statusText.TextWrapping = TextWrapping.Wrap;
        _statusText.Visibility = Visibility.Collapsed;
        _statusText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.OrangeRed);
        AutomationProperties.SetName(_statusText, P("Picker search status", "選擇器搜尋狀態"));
        AutomationProperties.SetLiveSetting(_statusText, AutomationLiveSetting.Polite);

        _flyoutPanel = new StackPanel { Spacing = 8, MaxWidth = 560 };
        _searchLabel.Text = P("Search options", "搜尋選項");
        _searchLabel.FontWeight = FontWeights.SemiBold;
        _flyoutPanel.Children.Add(_searchLabel);
        _flyoutPanel.Children.Add(_search);
        _flyoutPanel.Children.Add(_optionsList);
        _flyoutPanel.Children.Add(_statusText);
        _flyout.Content = new Border
        {
            Padding = new Thickness(12),
            Background = SurfaceBrush(),
            BorderBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            BorderThickness = new Thickness(1),
            Child = _flyoutPanel,
        };
        _flyout.Opening += Flyout_Opening;
        _flyout.Closed += Flyout_Closed;
        _pickerButton.Flyout = _flyout;

        var layout = new StackPanel { Spacing = 4 };
        layout.Children.Add(_header);
        layout.Children.Add(_pickerButton);
        Content = layout;

        Loaded += SearchablePickerBox_Loaded;
        Unloaded += SearchablePickerBox_Unloaded;
        ApplyAutomationNames();
    }

    public event EventHandler? SelectionChanged;

    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set
        {
            _itemsSource = value;
            _allOptions.Clear();
            if (value is not null)
            {
                foreach (object? item in value)
                {
                    if (item is null) continue;
                    _allOptions.Add(CreateOption(item));
                }
            }

            if (_selectedItem is null && _pendingSelectedIndex >= 0 && _pendingSelectedIndex < _allOptions.Count)
                _selectedItem = _allOptions[_pendingSelectedIndex].Value;
            else if (_selectedItem is not null && !_allOptions.Any(option => Equals(option.Value, _selectedItem)))
                _selectedItem = null;

            RenderOptions();
            UpdatePickerButton();
        }
    }

    public Func<object, string>? DisplayTextProvider
    {
        get => _displayTextProvider;
        set
        {
            _displayTextProvider = value;
            RebuildOptionText();
        }
    }

    public Func<object, IEnumerable<string>>? SearchTextProvider
    {
        get => _searchTextProvider;
        set
        {
            _searchTextProvider = value;
            RebuildOptionText();
        }
    }

    public string Header
    {
        get => _header.Text;
        set
        {
            _header.Text = value ?? string.Empty;
            _header.Visibility = string.IsNullOrWhiteSpace(_header.Text) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string PlaceholderText
    {
        get => _search.PlaceholderText ?? string.Empty;
        set => _search.PlaceholderText = value ?? string.Empty;
    }

    public string AutomationId
    {
        get => _automationId;
        set
        {
            _automationId = value ?? string.Empty;
            ApplyAutomationIds();
        }
    }

    public string SearchAutomationId
    {
        get => _searchAutomationId;
        set
        {
            _searchAutomationId = string.IsNullOrWhiteSpace(value) ? "SearchablePickerSearchBox" : value;
            _search.AutomationIdPrefix = _searchAutomationId;
            ApplyAutomationIds();
        }
    }

    public Func<string>? AutomationNameProvider
    {
        get => _automationNameProvider;
        set
        {
            _automationNameProvider = value;
            ApplyAutomationNames();
        }
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set => SelectItem(value, notify: true);
    }

    public int SelectedIndex
    {
        get => _selectedItem is null ? -1 : _allOptions.FindIndex(option => Equals(option.Value, _selectedItem));
        set
        {
            _pendingSelectedIndex = value;
            if (value >= 0 && value < _allOptions.Count)
                SelectItem(_allOptions[value].Value, notify: true);
            else if (value < 0)
                SelectItem(null, notify: true);
        }
    }

    public void RefreshItems()
    {
        RebuildOptionText();
        RenderOptions();
        UpdatePickerButton();
    }

    public void FocusPicker() => _pickerButton.Focus(FocusState.Programmatic);

    private Option CreateOption(object item)
    {
        string text = _displayTextProvider?.Invoke(item) ?? item.ToString() ?? string.Empty;
        var values = new List<string> { text };
        if (_searchTextProvider is not null)
            values.AddRange(_searchTextProvider(item).Where(value => !string.IsNullOrEmpty(value)));
        return new Option { Value = item, Text = text, SearchValues = values.Distinct(StringComparer.Ordinal).ToArray() };
    }

    private void RebuildOptionText()
    {
        if (_allOptions.Count == 0) return;
        var values = _allOptions.Select(option => option.Value).ToArray();
        _allOptions.Clear();
        foreach (object value in values) _allOptions.Add(CreateOption(value));
        RenderOptions();
        UpdatePickerButton();
    }

    private void RenderOptions()
    {
        if (_optionsList is null) return;
        _visibleOptions.Clear();
        bool hasQuery = _search.Spec.UseRegex
            ? _search.Spec.Query.Length > 0
            : _search.Spec.Query.Trim().Length > 0;

        if (!hasQuery)
        {
            _visibleOptions.AddRange(_allOptions);
            HideStatus();
        }
        else
        {
            SearchPatternService.Matcher matcher = _search.CompileMatcher();
            if (!matcher.Ok)
            {
                ShowStatus(P($"Invalid .NET regex: {matcher.Error}", $".NET regex 無效：{matcher.Error}"));
                ApplyVisibleOptions(Array.Empty<Option>());
                return;
            }

            string? error = null;
            foreach (Option option in _allOptions)
            {
                SearchPatternService.MatchResult result = matcher.MatchAny(option.SearchValues);
                if (!result.Ok)
                {
                    error = result.Error;
                    break;
                }
                if (result.IsMatch) _visibleOptions.Add(option);
            }

            if (error is not null)
            {
                ShowStatus(P($"Search stopped: {error}", $"搜尋已停止：{error}"));
                ApplyVisibleOptions(Array.Empty<Option>());
                return;
            }

            if (_visibleOptions.Count == 0)
            {
                ShowStatus(P("No matching categories.", "搵唔到符合嘅分類。"));
                ApplyVisibleOptions(Array.Empty<Option>());
                return;
            }

            HideStatus();
        }

        ApplyVisibleOptions(_visibleOptions);
    }

    private void ApplyVisibleOptions(IEnumerable<Option> options)
    {
        _optionsList.ItemsSource = options.ToList();
        Option? selected = _optionsList.Items.OfType<Option>().FirstOrDefault(option => Equals(option.Value, _selectedItem));
        _optionsList.SelectedItem = selected ?? (_optionsList.Items.Count > 0 ? _optionsList.Items[0] : null);
    }

    private void OptionsList_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            CommitSelectedOption();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            _flyout.Hide();
            e.Handled = true;
        }
    }

    private void OptionsList_Tapped(object sender, TappedRoutedEventArgs e) => CommitSelectedOption();

    private void OptionsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e) => CommitSelectedOption();

    private void CommitHighlightedOption()
    {
        Option? option = _optionsList.SelectedItem as Option ?? _visibleOptions.FirstOrDefault();
        if (option is not null) SelectItem(option.Value, notify: true);
    }

    private void CommitSelectedOption()
    {
        if (_optionsList.SelectedItem is Option option) SelectItem(option.Value, notify: true);
    }

    private void SelectItem(object? value, bool notify)
    {
        if (value is not null && !_allOptions.Any(option => Equals(option.Value, value))) return;
        bool changed = !Equals(_selectedItem, value);
        _selectedItem = value;
        _pendingSelectedIndex = SelectedIndex;
        UpdatePickerButton();
        if (!changed || !notify) return;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        _flyout.Hide();
    }

    private void Flyout_Opening(object? sender, object e)
    {
        ApplyFlyoutLayout();
        _search.Clear();
        RenderOptions();
        _search.FocusQuery();
    }

    private void Flyout_Closed(object? sender, object e) => FocusPicker();

    private void Search_QueryKeyDown(object? sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Down:
                FocusOption(1);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Up:
                FocusOption(-1);
                e.Handled = true;
                break;
            case Windows.System.VirtualKey.Escape:
                if (HasSearchInput())
                {
                    _search.Clear();
                    RenderOptions();
                    _search.FocusQuery();
                }
                else
                {
                    _flyout.Hide();
                }
                e.Handled = true;
                break;
        }
    }

    private bool HasSearchInput()
        => _search.Spec.Query.Length > 0;

    private void FocusOption(int delta)
    {
        if (_optionsList.Items.Count == 0) return;
        int current = _optionsList.SelectedIndex;
        int next = current < 0
            ? delta > 0 ? 0 : _optionsList.Items.Count - 1
            : Math.Clamp(current + delta, 0, _optionsList.Items.Count - 1);
        _optionsList.SelectedIndex = next;
        _optionsList.Focus(FocusState.Programmatic);
    }

    private void UpdatePickerButton()
    {
        Option? option = _allOptions.FirstOrDefault(candidate => Equals(candidate.Value, _selectedItem));
        _selectedText.Text = option?.Text ?? P("Choose a category", "揀分類");
        _pickerButton.Content = BuildPickerButtonContent();
    }

    private void ApplyFlyoutLayout()
    {
        double viewportWidth = _pickerButton.XamlRoot?.Size.Width ?? 640;
        double viewportHeight = _pickerButton.XamlRoot?.Size.Height ?? 640;
        double width = Math.Max(1, Math.Min(560, viewportWidth - 32));
        _flyoutPanel.Width = width;
        _search.MaxLayoutWidth = Math.Max(1, width - 24);
        _optionsList.MaxHeight = Math.Clamp(viewportHeight - 220, 160, 320);
    }

    private object BuildPickerButtonContent()
    {
        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(_selectedText);
        var icon = new FontIcon { Glyph = "\uE70D", FontSize = 12 };
        Grid.SetColumn(icon, 1);
        grid.Children.Add(icon);
        return grid;
    }

    private void ShowStatus(string text)
    {
        _statusText.Text = text;
        _statusText.Visibility = Visibility.Visible;
    }

    private void HideStatus()
    {
        _statusText.Text = string.Empty;
        _statusText.Visibility = Visibility.Collapsed;
    }

    private void SearchablePickerBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (_languageSubscribed) return;
        Loc.I.LanguageChanged += OnLanguageChanged;
        FunnyLevelSettings.I.Changed += OnToneChanged;
        _languageSubscribed = true;
        _toneSubscribed = true;
        ApplyAutomationNames();
        RefreshItems();
    }

    private void SearchablePickerBox_Unloaded(object sender, RoutedEventArgs e)
    {
        if (!_languageSubscribed) return;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        if (_toneSubscribed)
        {
            FunnyLevelSettings.I.Changed -= OnToneChanged;
            _toneSubscribed = false;
        }
        _languageSubscribed = false;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        ApplyAutomationNames();
        RefreshItems();
    }

    private void OnToneChanged(object? sender, EventArgs e)
    {
        _search.RefreshAutomationNames();
        ApplyAutomationNames();
        RefreshItems();
    }

    private void ApplyAutomationNames()
    {
        string name = _automationNameProvider?.Invoke() ?? _automationName;
        if (string.IsNullOrWhiteSpace(name)) name = P("Category picker", "分類選擇器");
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetName(_pickerButton, P(name, name));
        AutomationProperties.SetHelpText(_pickerButton, P("Opens a searchable category list. Use arrow keys and Enter to select.", "開啟可搜尋分類清單；用方向鍵同 Enter 揀選。"));
        AutomationProperties.SetName(_statusText, P("Category picker search status", "分類選擇器搜尋狀態"));
        AutomationProperties.SetName(_search, P($"{name}: search categories", $"{name}：搜尋分類"));
        _searchLabel.Text = P("Search options", "搜尋選項");
        _search.PlaceholderText = P("Search categories", "搜尋分類");
        _search.AutomationIdPrefix = _searchAutomationId;
        ApplyAutomationIds();
        _header.Text = string.IsNullOrWhiteSpace(Header) ? string.Empty : Header;
        UpdatePickerButton();
    }

    private string P(string en, string zh)
        => Loc.I.Pick(FunnyLevelSettings.I.StyleEnglish(en), FunnyLevelSettings.I.StyleCantonese(zh));

    private void ApplyAutomationIds()
    {
        string prefix = string.IsNullOrWhiteSpace(_automationId) ? "SearchablePicker" : _automationId;
        AutomationProperties.SetAutomationId(this, prefix);
        AutomationProperties.SetAutomationId(_pickerButton, $"{prefix}_Button");
        AutomationProperties.SetAutomationId(_statusText, $"{prefix}_Status");
        AutomationProperties.SetAutomationId(_search, _searchAutomationId);
    }

    private static Brush? SecondaryBrush()
        => Application.Current?.Resources.TryGetValue("TextFillColorSecondaryBrush", out object value) == true ? value as Brush : null;

    private static Brush? SurfaceBrush()
        => Application.Current?.Resources.TryGetValue("CardBackgroundFillColorDefaultBrush", out object value) == true ? value as Brush : null;
}
