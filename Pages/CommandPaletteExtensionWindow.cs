using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Generic, theme-native renderer for the validated structured pages returned by
/// an out-of-process Command Palette extension host. It never renders extension
/// HTML or script; the protocol carries only text, simple fields, and actions.
/// </summary>
public sealed class CommandPaletteExtensionWindow : Window
{
    private static readonly object WindowGate = new();
    private static readonly List<CommandPaletteExtensionWindow> OpenWindows = new();

    private readonly CommandPaletteExtensionPack _pack;
    private readonly CommandPaletteExtensionCommand _command;
    private readonly Grid _root = new();
    private readonly TextBlock _title = new() { FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _body = new() { TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };
    private readonly InfoBar _notice = new() { IsOpen = false, IsClosable = true };
    private readonly StackPanel _fieldsPanel = new() { Spacing = 10 };
    private readonly StackPanel _actionsPanel = new() { Spacing = 8 };
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Button> _actionButtons = new();
    private readonly CancellationTokenSource _lifetime = new();
    private CommandPaletteExtensionHostPage _page;
    private string _noticeEn = string.Empty;
    private string _noticeZh = string.Empty;
    private bool _busy;
    private bool _closed;

    private CommandPaletteExtensionWindow(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        CommandPaletteExtensionHostPage page)
    {
        _pack = pack;
        _command = command;
        _page = page;
        Title = "WinForge Command Palette Extension";
        Loc.I.LanguageChanged += OnLanguageChanged;
        Closed += OnClosed;
        AutomationProperties.SetLiveSetting(_notice, AutomationLiveSetting.Assertive);

        var content = new StackPanel { Spacing = 14 };
        content.Children.Add(_title);
        content.Children.Add(_body);
        content.Children.Add(_notice);
        content.Children.Add(_fieldsPanel);
        content.Children.Add(_actionsPanel);

        _root.Children.Add(new Border
        {
            Padding = new Thickness(24),
            Child = new ScrollViewer
            {
                Content = content,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            }
        });
        Content = _root;
        ApplyTheme();
        RenderPage(page);
    }

    public static void Open(
        CommandPaletteExtensionPack pack,
        CommandPaletteExtensionCommand command,
        CommandPaletteExtensionHostPage page)
    {
        var window = new CommandPaletteExtensionWindow(pack, command, page);
        lock (WindowGate) OpenWindows.Add(window);
        window.Activate();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void RenderPage(CommandPaletteExtensionHostPage page, bool preserveValues = false)
    {
        _page = page;
        if (!preserveValues)
        {
            _notice.IsOpen = false;
            _noticeEn = string.Empty;
            _noticeZh = string.Empty;
        }
        Title = P(page.Title, page.Zh);
        _title.Text = P(page.Title, page.Zh);
        AutomationProperties.SetName(_root, Title);
        _body.Text = P(page.Body, page.ZhBody);
        _body.Visibility = string.IsNullOrWhiteSpace(_body.Text) ? Visibility.Collapsed : Visibility.Visible;

        var previousValues = preserveValues
            ? new Dictionary<string, string>(_values, StringComparer.OrdinalIgnoreCase)
            : null;
        _values.Clear();
        _fieldsPanel.Children.Clear();
        foreach (var field in page.Fields)
        {
            var label = P(field.Label, field.Zh);
            _values[field.Id] = previousValues is not null && previousValues.TryGetValue(field.Id, out var previousValue)
                ? previousValue
                : field.Value;

            switch (field.Type)
            {
                case CommandPaletteExtensionHostFieldType.Text:
                {
                    var fieldId = field.Id;
                    var box = new TextBox
                    {
                        Header = label,
                        Text = _values[fieldId],
                        MaxLength = 4096,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    AutomationProperties.SetName(box, label);
                    box.TextChanged += (_, _) => _values[fieldId] = box.Text;
                    _fieldsPanel.Children.Add(box);
                    break;
                }
                case CommandPaletteExtensionHostFieldType.Toggle:
                {
                    var fieldId = field.Id;
                    var toggle = new ToggleSwitch
                    {
                        Header = label,
                        IsOn = string.Equals(_values[fieldId], "true", StringComparison.OrdinalIgnoreCase),
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    AutomationProperties.SetName(toggle, label);
                    toggle.Toggled += (_, _) => _values[fieldId] = toggle.IsOn ? "true" : "false";
                    _fieldsPanel.Children.Add(toggle);
                    break;
                }
                case CommandPaletteExtensionHostFieldType.Choice:
                {
                    var fieldId = field.Id;
                    var combo = new ComboBox
                    {
                        Header = label,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    AutomationProperties.SetName(combo, label);
                    foreach (var option in field.Options)
                    {
                        combo.Items.Add(new ComboBoxItem { Content = P(option.Title, option.Zh), Tag = option.Value });
                    }
                    for (var index = 0; index < combo.Items.Count; index++)
                    {
                        if (combo.Items[index] is ComboBoxItem { Tag: string value }
                            && string.Equals(value, _values[fieldId], StringComparison.OrdinalIgnoreCase))
                        {
                            combo.SelectedIndex = index;
                            break;
                        }
                    }
                    combo.SelectionChanged += (_, _) =>
                    {
                        if (combo.SelectedItem is ComboBoxItem { Tag: string value }) _values[fieldId] = value;
                    };
                    _fieldsPanel.Children.Add(combo);
                    break;
                }
            }
        }

        _actionsPanel.Children.Clear();
        _actionButtons.Clear();
        foreach (var action in page.Actions)
        {
            var label = P(action.Title, action.Zh);
            var button = new Button
            {
                Content = label,
                Tag = action.Id,
                MinHeight = 44,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = !_busy
            };
            AutomationProperties.SetName(button, label);
            if (action.Primary)
            {
                try
                {
                    if (Application.Current.Resources.TryGetValue("AccentButtonStyle", out var resource)
                        && resource is Style accentButtonStyle)
                    {
                        button.Style = accentButtonStyle;
                    }
                }
                catch
                {
                    // The normal button style remains accessible if app resources are unavailable.
                }
            }
            button.Click += PageAction_Click;
            _actionButtons.Add(button);
            _actionsPanel.Children.Add(button);
        }
        _actionsPanel.Visibility = page.Actions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void PageAction_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: string actionId }) return;
        _busy = true;
        foreach (var button in _actionButtons) button.IsEnabled = false;
        try
        {
            var response = await CommandPaletteExtensionHostService.ExecutePageActionAsync(
                _pack,
                _command,
                _page.Id,
                actionId,
                _values,
                _lifetime.Token);
            if (_closed) return;
            if (!response.Success)
            {
                ShowNotice(
                    "The extension host could not complete that action.",
                    "擴充套件主機未能完成呢個操作。",
                    InfoBarSeverity.Error);
                return;
            }

            if (response.Kind == CommandPaletteExtensionHostResponseKind.Page && response.Page is not null)
            {
                RenderPage(response.Page);
                return;
            }

            if (!CommandPaletteService.ApplyExtensionHostResponse(_pack, _command, response))
            {
                ShowNotice(
                    "The extension host returned an unsupported result.",
                    "擴充套件主機傳回咗未支援嘅結果。",
                    InfoBarSeverity.Error);
                return;
            }

            ShowNotice("Extension action completed.", "擴充套件操作已完成。", InfoBarSeverity.Success);
        }
        catch
        {
            if (!_closed)
            {
                ShowNotice(
                    "The extension action could not be completed.",
                    "未能完成擴充套件操作。",
                    InfoBarSeverity.Error);
            }
        }
        finally
        {
            _busy = false;
            if (!_closed)
            {
                foreach (var button in _actionButtons) button.IsEnabled = true;
            }
        }
    }

    private void ShowNotice(string en, string zh, InfoBarSeverity severity)
    {
        _noticeEn = en;
        _noticeZh = zh;
        _notice.Title = P("Extension host", "擴充套件主機");
        _notice.Message = P(en, zh);
        _notice.Severity = severity;
        AutomationProperties.SetLiveSetting(
            _notice,
            severity == InfoBarSeverity.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        _notice.IsOpen = true;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderPage(_page, preserveValues: true);
        if (_notice.IsOpen)
        {
            _notice.Title = P("Extension host", "擴充套件主機");
            _notice.Message = P(_noticeEn, _noticeZh);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _closed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        Loc.I.LanguageChanged -= OnLanguageChanged;
        lock (WindowGate) OpenWindows.Remove(this);
    }

    private void ApplyTheme()
    {
        try
        {
            if (App.Shell?.Content is FrameworkElement shell && shell.ActualTheme != ElementTheme.Default)
            {
                _root.RequestedTheme = shell.ActualTheme;
            }
        }
        catch
        {
            // Default WinUI theme resources remain readable if the shell is not available.
        }
    }
}
