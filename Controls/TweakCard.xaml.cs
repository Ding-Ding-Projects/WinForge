using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 一張調校卡片 · A single tweak rendered as a card.
/// 標題同說明永遠同時顯示英文同粵語 · title and description always show both English and Cantonese.
/// </summary>
public sealed partial class TweakCard : UserControl
{
    private TweakDefinition? _tweak;
    private bool _suppress;
    private bool _busy;

    private ToggleSwitch? _toggle;
    private ComboBox? _combo;
    private Button? _actionButton;
    private TextBlock? _infoText;

    // ---- Rich interactive controls (foundation upgrade) · 進階互動控件 ----
    private Slider? _slider;
    private TextBlock? _sliderValue;
    private NumberBox? _numberBox;
    private RadioButtons? _radio;
    private readonly List<CheckBox> _checks = new();
    private Button? _colorButton;
    private Border? _colorSwatch;
    private TextBox? _hexBox;
    private ColorPicker? _colorPicker;
    private DatePicker? _datePicker;
    private TimePicker? _timePicker;
    private Button? _wizardButton;
    private DispatcherTimer? _progressTimer;

    public TweakCard()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public void SetTweak(TweakDefinition tweak)
    {
        _tweak = tweak;
        if (IsLoaded) Build();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged += OnLanguageChanged;
        Build();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RenderText();

    private void Build()
    {
        if (_tweak is null) return;
        ControlHost.Children.Clear();
        ResetControlRefs();
        switch (_tweak.Kind)
        {
            case TweakKind.Toggle: BuildToggle(); break;
            case TweakKind.Action: BuildAction(); break;
            case TweakKind.Choice: BuildChoice(); break;
            case TweakKind.Info: BuildInfo(); break;
            case TweakKind.Slider: BuildSlider(); break;
            case TweakKind.Number: BuildNumber(); break;
            case TweakKind.RadioGroup: BuildRadioGroup(); break;
            case TweakKind.MultiCheck: BuildMultiCheck(); break;
            case TweakKind.Color: BuildColor(); break;
            case TweakKind.DateKind: BuildDate(); break;
            case TweakKind.Wizard: BuildWizard(); break;
        }
        RenderText();
        UpdateBadges();
        UpdateStatusPill();
        BuildVisual();
    }

    /// <summary>
    /// 渲染（或清除）程式碼生成嘅視覺預覽 · Render (or clear) the code-generated visual preview.
    /// 工廠擲錯就靜靜收起個區，唔影響卡片其他部分 · a throwing factory just hides the pane.
    /// </summary>
    private void BuildVisual()
    {
        if (_tweak?.VisualBuilder is null)
        {
            VisualHost.Content = null;
            VisualPane.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var element = _tweak.VisualBuilder(_tweak);
            VisualHost.Content = element;
            VisualPane.Visibility = element is null ? Visibility.Collapsed : Visibility.Visible;
        }
        catch
        {
            VisualHost.Content = null;
            VisualPane.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>套用後若標記咗活動更新，重建預覽 · Rebuild the preview after an apply when live-update is on.</summary>
    private void RefreshVisualIfLive()
    {
        if (_tweak?.VisualBuilder is not null && _tweak.VisualLiveUpdate) BuildVisual();
    }

    /// <summary>清走上次 Build 嘅控件參照，避免語言切換時動到已棄用控件 · Null stale control refs before a rebuild.</summary>
    private void ResetControlRefs()
    {
        _toggle = null; _combo = null; _actionButton = null; _infoText = null;
        _slider = null; _sliderValue = null; _numberBox = null; _radio = null;
        _checks.Clear(); _colorButton = null; _colorSwatch = null; _hexBox = null;
        _colorPicker = null; _datePicker = null; _timePicker = null; _wizardButton = null;
    }

    private void RenderText()
    {
        if (_tweak is null) return;
        TitlePrimary.Text = _tweak.Title.Primary;
        TitleSecondary.Text = _tweak.Title.Secondary;
        DescPrimary.Text = _tweak.Description.Primary;
        DescSecondary.Text = _tweak.Description.Secondary;

        if (_actionButton is not null && _tweak.ActionLabel is not null)
        {
            _actionButton.Content = _tweak.ActionLabel.Primary;
            ToolTipService.SetToolTip(_actionButton, $"{_tweak.ActionLabel.En} · {_tweak.ActionLabel.Zh}");
        }
        if (_toggle is not null)
        {
            _toggle.OnContent = "On · 開";
            _toggle.OffContent = "Off · 熄";
        }
        if (_infoText is not null)
            _infoText.Text = SafeInfo();

        // ---- Rich controls: relabel on language change ----
        if (_sliderValue is not null && _slider is not null)
            _sliderValue.Text = FormatNumber(_slider.Value);
        if (_radio is not null && _tweak.Choices is not null)
            RelabelRadio();
        for (int i = 0; i < _checks.Count && _tweak.CheckItems is not null && i < _tweak.CheckItems.Count; i++)
        {
            var it = _tweak.CheckItems[i];
            _checks[i].Content = $"{it.Label.En} · {it.Label.Zh}";
        }
        if (_wizardButton is not null && _tweak.ActionLabel is not null)
        {
            _wizardButton.Content = _tweak.ActionLabel.Primary;
            ToolTipService.SetToolTip(_wizardButton, $"{_tweak.ActionLabel.En} · {_tweak.ActionLabel.Zh}");
        }
        UpdateStatusPill();
    }

    private void UpdateBadges()
    {
        AdminBadge.Visibility = _tweak!.RequiresAdmin ? Visibility.Visible : Visibility.Collapsed;
        RestartBadge.Visibility = _tweak.Restart != RestartScope.None ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---------------- Toggle ----------------
    private void BuildToggle()
    {
        _toggle = new ToggleSwitch();
        _suppress = true;
        try { _toggle.IsOn = _tweak!.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        _suppress = false;
        _toggle.Toggled += Toggle_Toggled;
        ControlHost.Children.Add(_toggle);
    }

    private void Toggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress || _tweak?.SetIsOn is null) return;
        try
        {
            _tweak.SetIsOn(_toggle!.IsOn);
            ShowApplied();
        }
        catch (Exception ex)
        {
            _suppress = true;
            try { _toggle!.IsOn = _tweak.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
            _suppress = false;
            ShowError(ex);
        }
    }

    // ---------------- Choice ----------------
    private void BuildChoice()
    {
        _combo = new ComboBox { MinWidth = 170 };
        foreach (var c in _tweak!.Choices!)
            _combo.Items.Add(new ComboBoxItem { Content = $"{c.Label.En} · {c.Label.Zh}", Tag = c.Value });

        _suppress = true;
        try
        {
            var cur = _tweak.GetCurrentChoice?.Invoke();
            if (cur is not null)
            {
                for (int i = 0; i < _tweak.Choices!.Count; i++)
                {
                    if (string.Equals(_tweak.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    {
                        _combo.SelectedIndex = i;
                        break;
                    }
                }
            }
        }
        catch { /* leave unselected */ }
        _suppress = false;

        _combo.SelectionChanged += Choice_Changed;
        ControlHost.Children.Add(_combo);
    }

    private void Choice_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _tweak?.SetChoice is null) return;
        if (_combo!.SelectedItem is ComboBoxItem item && item.Tag is string val)
        {
            try
            {
                _tweak.SetChoice(val);
                ShowApplied();
            }
            catch (Exception ex)
            {
                ShowError(ex);
                _suppress = true;
                try
                {
                    var cur = _tweak.GetCurrentChoice?.Invoke();
                    if (cur is not null)
                        for (int i = 0; i < _tweak.Choices!.Count; i++)
                            if (string.Equals(_tweak.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                            { _combo.SelectedIndex = i; break; }
                }
                catch { /* ignore */ }
                _suppress = false;
            }
        }
    }

    // ---------------- Action ----------------
    private void BuildAction()
    {
        _actionButton = new Button { MinWidth = 110 };
        _actionButton.Click += Action_Click;
        ControlHost.Children.Add(_actionButton);
    }

    private async void Action_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _tweak?.RunAsync is null) return;
        if (_tweak.Destructive && !await ConfirmAsync()) return;

        _busy = true;
        _actionButton!.IsEnabled = false;
        var label = _actionButton.Content;
        _actionButton.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
        ResultBar.IsOpen = false;
        OutputPane.Visibility = Visibility.Collapsed;
        StartProgress();

        try
        {
            var result = await _tweak.RunAsync(CancellationToken.None);
            ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = result.Success ? Loc.I.Pick("Done", "完成") : Loc.I.Pick("Failed", "失敗");
            ResultBar.Message = result.Message is null ? string.Empty : $"{result.Message.En}\n{result.Message.Zh}";
            ResultBar.ActionButton = (!result.Success && _tweak.RequiresAdmin && !AdminHelper.IsElevated)
                ? MakeRelaunchButton() : null;
            ResultBar.IsOpen = true;

            // Output: a native grid for tabular (CSV) results, else a monospace scrollable pane.
            // Both keep the raw text in _lastOutput for Copy / Save; no truncation either way.
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                _lastOutput = result.Output!;
                CopyOutBtn.Content = Loc.I.Pick("Copy", "複製");
                SaveOutBtn.Content = Loc.I.Pick("Save…", "儲存…");

                if (_tweak.TabularOutput && result.Success && TryParseCsv(_lastOutput, out var rows))
                {
                    RenderTable(rows);
                    TableScroller.Visibility = Visibility.Visible;
                    OutputBox.Visibility = Visibility.Collapsed;
                    int dataRows = rows.Count - 1;
                    OutputHeader.Text = Loc.I.Pick($"Table · {dataRows} rows × {rows[0].Length} cols",
                        $"表格 · {dataRows} 列 × {rows[0].Length} 欄");
                }
                else
                {
                    OutputBox.Text = _lastOutput;
                    OutputBox.Visibility = Visibility.Visible;
                    TableScroller.Visibility = Visibility.Collapsed;
                    OutputHeader.Text = Loc.I.Pick($"Output · {_lastOutput.Length} chars", $"輸出 · {_lastOutput.Length} 字");
                }
                OutputPane.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            StopProgress();
            _actionButton.Content = label;
            _actionButton.IsEnabled = true;
            _busy = false;
            RenderText();
            RefreshVisualIfLive();
        }
    }

    /// <summary>
    /// 開始顯示動作進度條 · Begin showing the action progress bar.
    /// 有 <see cref="TweakDefinition.ActionProgress"/> 就用確定進度（每 200ms poll），否則不確定。
    /// Determinate (polled every 200 ms) when ActionProgress is set, otherwise indeterminate.
    /// </summary>
    private void StartProgress()
    {
        if (_tweak?.ShowProgressBar != true) return;
        ActionProgressBar.Visibility = Visibility.Visible;
        if (_tweak.ActionProgress is null)
        {
            ActionProgressBar.IsIndeterminate = true;
        }
        else
        {
            ActionProgressBar.IsIndeterminate = false;
            ActionProgressBar.Minimum = 0;
            ActionProgressBar.Maximum = 1;
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _progressTimer.Tick += (_, _) =>
            {
                try { ActionProgressBar.Value = Math.Clamp(_tweak.ActionProgress!(), 0, 1); }
                catch { /* ignore */ }
            };
            _progressTimer.Start();
        }
    }

    private void StopProgress()
    {
        _progressTimer?.Stop();
        _progressTimer = null;
        ActionProgressBar.IsIndeterminate = false;
        ActionProgressBar.Visibility = Visibility.Collapsed;
    }

    private async Task<bool> ConfirmAsync()
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Loc.I.Pick("Are you sure?", "確定嗎？"),
            Content = $"{_tweak!.Title.En}\n{_tweak.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = Loc.I.Pick("Proceed", "繼續"),
            CloseButtonText = Loc.I.Pick("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private string _lastOutput = "";

    /// <summary>RFC-4180 CSV parser (handles quoted fields, embedded commas/quotes/newlines). Needs ≥1 header + ≥1 data row.</summary>
    private static bool TryParseCsv(string text, out List<string[]> rows)
    {
        rows = new List<string[]>();
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.TrimStart('﻿', '​');

        var result = new List<string[]>();
        var field = new StringBuilder();
        var cur = new List<string>();
        bool inQuotes = false, sawAny = false;
        void EndField() { cur.Add(field.ToString()); field.Clear(); sawAny = true; }
        void EndRow() { EndField(); result.Add(cur.ToArray()); cur = new List<string>(); }

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') EndField();
            else if (ch == '\r') { /* swallow */ }
            else if (ch == '\n') { if (sawAny || field.Length > 0 || cur.Count > 0) EndRow(); }
            else field.Append(ch);
        }
        if (field.Length > 0 || cur.Count > 0) EndRow();

        if (result.Count < 2 || result[0].Length < 1) return false;
        // Keep only rows whose column count matches the header — drops any trailing CLIXML/noise lines.
        int cols = result[0].Length;
        var kept = new List<string[]> { result[0] };
        for (int r = 1; r < result.Count; r++)
            if (result[r].Length == cols) kept.Add(result[r]);
        if (kept.Count < 2) return false;
        rows = kept;
        return true;
    }

    /// <summary>Draw parsed CSV rows into <see cref="TableHost"/> as a header + separator + data grid.</summary>
    private void RenderTable(List<string[]> rows)
    {
        TableHost.Children.Clear();
        TableHost.ColumnDefinitions.Clear();
        TableHost.RowDefinitions.Clear();
        TableHost.ColumnSpacing = 18;
        TableHost.RowSpacing = 3;

        int cols = rows[0].Length;
        for (int c = 0; c < cols; c++)
            TableHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int r = 0; r < rows.Count + 1; r++) // +1 row for the header separator
            TableHost.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var secondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        var stroke = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];

        for (int c = 0; c < cols; c++)
        {
            var head = new TextBlock { Text = rows[0][c], FontWeight = FontWeights.SemiBold, FontSize = 12 };
            Grid.SetRow(head, 0); Grid.SetColumn(head, c);
            TableHost.Children.Add(head);
        }
        var sep = new Border { Height = 1, Background = stroke, Margin = new Thickness(0, 1, 0, 1) };
        Grid.SetRow(sep, 1); Grid.SetColumn(sep, 0); Grid.SetColumnSpan(sep, cols);
        TableHost.Children.Add(sep);

        for (int r = 1; r < rows.Count; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                var val = c < rows[r].Length ? rows[r][c] : "";
                var cell = new TextBlock { Text = val, FontSize = 12, Foreground = secondary, IsTextSelectionEnabled = true };
                Grid.SetRow(cell, r + 1); Grid.SetColumn(cell, c);
                TableHost.Children.Add(cell);
            }
        }
    }

    private void CopyOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(_lastOutput);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        catch { }
    }

    private async void SaveOut_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Win32 COM save dialog (FileDialogs) — works whether or not the app is elevated;
            // the old WinRT FileSavePicker failed silently under admin.
            // Win32 COM 儲存對話框，無論係咪管理員身分都用得；舊嘅 WinRT picker 喺管理員模式會默默失敗。
            var path = await FileDialogs.SaveFileAsync("winforge-output", ".txt");
            if (path is not null) await File.WriteAllTextAsync(path, _lastOutput);
        }
        catch { }
    }

    // ---------------- Info ----------------
    private void BuildInfo()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _infoText = new TextBlock
        {
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 14 },
            Padding = new Thickness(8),
        };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => { _infoText.Text = SafeInfo(); };
        panel.Children.Add(_infoText);
        panel.Children.Add(refresh);
        ControlHost.Children.Add(panel);
    }

    private string SafeInfo()
    {
        try { return _tweak?.GetInfo?.Invoke() ?? "—"; }
        catch { return "—"; }
    }

    // ================================================================
    //  Rich interactive renderers (foundation upgrade) · 進階互動渲染
    // ================================================================

    /// <summary>把數值連單位格式化 · Format a number with optional unit, e.g. "400 ms".</summary>
    private string FormatNumber(double v)
    {
        bool whole = _tweak!.Step >= 1 && Math.Abs(_tweak.Step % 1) < 1e-9;
        string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                           : v.ToString("0.###", CultureInfo.InvariantCulture);
        var unit = _tweak.Unit;
        return unit is null ? num : $"{num} {unit.Primary}";
    }

    // ---------------- Slider ----------------
    private void BuildSlider()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        _slider = new Slider
        {
            Minimum = _tweak!.Min,
            Maximum = _tweak.Max,
            StepFrequency = _tweak.Step,
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _sliderValue = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        _suppress = true;
        try { _slider.Value = Clamp(_tweak.GetNumber?.Invoke() ?? _tweak.Min); } catch { _slider.Value = _tweak.Min; }
        _suppress = false;
        _sliderValue.Text = FormatNumber(_slider.Value);

        _slider.ValueChanged += Slider_ValueChanged;
        panel.Children.Add(_slider);
        panel.Children.Add(_sliderValue);
        ControlHost.Children.Add(panel);
    }

    private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_sliderValue is not null) _sliderValue.Text = FormatNumber(e.NewValue);
        if (_suppress || _tweak?.SetNumber is null) return;
        try { _tweak.SetNumber(e.NewValue); ShowApplied(); UpdateStatusPill(); }
        catch (Exception ex)
        {
            ShowError(ex);
            _suppress = true;
            try { _slider!.Value = Clamp(_tweak.GetNumber?.Invoke() ?? _tweak.Min); } catch { /* ignore */ }
            _suppress = false;
        }
    }

    // ---------------- Number ----------------
    private void BuildNumber()
    {
        _numberBox = new NumberBox
        {
            Minimum = _tweak!.Min,
            Maximum = _tweak.Max,
            SmallChange = _tweak.Step,
            LargeChange = _tweak.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        _suppress = true;
        try { _numberBox.Value = Clamp(_tweak.GetNumber?.Invoke() ?? _tweak.Min); } catch { _numberBox.Value = _tweak.Min; }
        _suppress = false;
        _numberBox.ValueChanged += Number_ValueChanged;
        ControlHost.Children.Add(_numberBox);
    }

    private void Number_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs e)
    {
        if (_suppress || _tweak?.SetNumber is null) return;
        if (double.IsNaN(e.NewValue)) return;
        try { _tweak.SetNumber(e.NewValue); ShowApplied(); UpdateStatusPill(); }
        catch (Exception ex)
        {
            ShowError(ex);
            _suppress = true;
            try { _numberBox!.Value = Clamp(_tweak.GetNumber?.Invoke() ?? _tweak.Min); } catch { /* ignore */ }
            _suppress = false;
        }
    }

    private double Clamp(double v) => Math.Max(_tweak!.Min, Math.Min(_tweak.Max, v));

    // ---------------- RadioGroup ----------------
    private void BuildRadioGroup()
    {
        _radio = new RadioButtons { MaxColumns = 1 };
        RelabelRadio();

        _suppress = true;
        try
        {
            var cur = _tweak!.GetCurrentChoice?.Invoke();
            if (cur is not null)
                for (int i = 0; i < _tweak.Choices!.Count; i++)
                    if (string.Equals(_tweak.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { _radio.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        _suppress = false;

        _radio.SelectionChanged += Radio_Changed;
        ControlHost.Children.Add(_radio);
    }

    /// <summary>(重)填 RadioButtons 嘅雙語標籤，保留目前選擇 · (Re)label the radio items bilingually, keeping selection.</summary>
    private void RelabelRadio()
    {
        if (_radio is null || _tweak?.Choices is null) return;
        int sel = _radio.SelectedIndex;
        _suppress = true;
        _radio.Items.Clear();
        foreach (var c in _tweak.Choices)
            _radio.Items.Add(new RadioButton { Content = $"{c.Label.En} · {c.Label.Zh}", Tag = c.Value });
        _radio.SelectedIndex = sel;
        _suppress = false;
    }

    private void Radio_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress || _tweak?.SetChoice is null) return;
        if (_radio!.SelectedItem is RadioButton rb && rb.Tag is string val)
        {
            try { _tweak.SetChoice(val); ShowApplied(); UpdateStatusPill(); }
            catch (Exception ex)
            {
                ShowError(ex);
                _suppress = true;
                try
                {
                    var cur = _tweak.GetCurrentChoice?.Invoke();
                    if (cur is not null)
                        for (int i = 0; i < _tweak.Choices!.Count; i++)
                            if (string.Equals(_tweak.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                            { _radio.SelectedIndex = i; break; }
                }
                catch { /* ignore */ }
                _suppress = false;
            }
        }
    }

    // ---------------- MultiCheck ----------------
    private void BuildMultiCheck()
    {
        var panel = new StackPanel { Spacing = 4, MinWidth = 180 };
        _checks.Clear();
        if (_tweak!.CheckItems is not null)
        {
            foreach (var item in _tweak.CheckItems)
            {
                var cb = new CheckBox { Content = $"{item.Label.En} · {item.Label.Zh}", Tag = item };
                _suppress = true;
                try { cb.IsChecked = item.Get(); } catch { cb.IsChecked = false; }
                _suppress = false;
                cb.Checked += Check_Toggled;
                cb.Unchecked += Check_Toggled;
                _checks.Add(cb);
                panel.Children.Add(cb);
            }
        }
        ControlHost.Children.Add(panel);
    }

    private void Check_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (sender is CheckBox cb && cb.Tag is TweakToggleItem item)
        {
            try { item.Set(cb.IsChecked == true); ShowApplied(); UpdateStatusPill(); }
            catch (Exception ex)
            {
                ShowError(ex);
                _suppress = true;
                try { cb.IsChecked = item.Get(); } catch { /* ignore */ }
                _suppress = false;
            }
        }
    }

    // ---------------- Color ----------------
    private void BuildColor()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        _colorSwatch = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        };

        _colorPicker = new ColorPicker
        {
            ColorSpectrumShape = ColorSpectrumShape.Box,
            IsMoreButtonVisible = false,
            IsColorSliderVisible = true,
            IsColorChannelTextInputVisible = true,
            IsHexInputVisible = true,
            IsAlphaEnabled = false,
        };
        var apply = new Button { Content = "Apply · 套用", Margin = new Thickness(0, 8, 0, 0) };
        var flyoutPanel = new StackPanel { Spacing = 6 };
        flyoutPanel.Children.Add(_colorPicker);
        flyoutPanel.Children.Add(apply);
        var flyout = new Flyout { Content = flyoutPanel };

        _colorButton = new Button { Content = "Pick · 揀色", Flyout = flyout };
        apply.Click += (_, _) => { ApplyHex(ColorToHex(_colorPicker.Color)); flyout.Hide(); };

        _hexBox = new TextBox { Width = 88, PlaceholderText = "#RRGGBB" };
        _hexBox.KeyDown += (s, k) =>
        {
            if (k.Key == Windows.System.VirtualKey.Enter) ApplyHex(_hexBox.Text);
        };
        _hexBox.LostFocus += (_, _) => ApplyHex(_hexBox.Text);

        string cur = SafeHex();
        SetSwatch(cur);
        _hexBox.Text = cur;
        try { _colorPicker.Color = HexToColor(cur); } catch { /* keep default */ }

        panel.Children.Add(_colorSwatch);
        panel.Children.Add(_hexBox);
        panel.Children.Add(_colorButton);
        ControlHost.Children.Add(panel);
    }

    private string SafeHex()
    {
        try { return NormalizeHex(_tweak?.GetHex?.Invoke() ?? "#000000"); }
        catch { return "#000000"; }
    }

    private void ApplyHex(string raw)
    {
        if (_tweak?.SetHex is null) return;
        string hex;
        try { hex = NormalizeHex(raw); }
        catch { return; }
        try
        {
            _tweak.SetHex(hex);
            SetSwatch(hex);
            if (_hexBox is not null) _hexBox.Text = hex;
            ShowApplied();
            UpdateStatusPill();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SetSwatch(string hex)
    {
        try { if (_colorSwatch is not null) _colorSwatch.Background = new SolidColorBrush(HexToColor(hex)); }
        catch { /* ignore */ }
    }

    private static string NormalizeHex(string raw)
    {
        var s = (raw ?? "").Trim().TrimStart('#');
        if (s.Length == 3) s = $"{s[0]}{s[0]}{s[1]}{s[1]}{s[2]}{s[2]}";
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
            throw new FormatException("Bad hex");
        return "#" + s.ToUpperInvariant();
    }

    private static Color HexToColor(string hex)
    {
        var s = NormalizeHex(hex).TrimStart('#');
        byte r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        byte b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb(255, r, g, b);
    }

    private static string ColorToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    // ---------------- Date / Time ----------------
    private void BuildDate()
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        _datePicker = new DatePicker();
        _suppress = true;
        DateTimeOffset? cur = null;
        try { cur = _tweak!.GetDate?.Invoke(); } catch { /* ignore */ }
        if (cur is not null) _datePicker.Date = cur.Value;
        _suppress = false;
        _datePicker.DateChanged += (_, _) => CommitDate();
        panel.Children.Add(_datePicker);

        if (_tweak!.IncludeTime)
        {
            _timePicker = new TimePicker { ClockIdentifier = "24HourClock" };
            if (cur is not null) _timePicker.Time = cur.Value.TimeOfDay;
            _timePicker.TimeChanged += (_, _) => CommitDate();
            panel.Children.Add(_timePicker);
        }
        ControlHost.Children.Add(panel);
    }

    private void CommitDate()
    {
        if (_suppress || _tweak?.SetDate is null || _datePicker is null) return;
        try
        {
            var date = _datePicker.Date;
            var time = _timePicker?.Time ?? TimeSpan.Zero;
            var combined = new DateTimeOffset(date.Year, date.Month, date.Day,
                time.Hours, time.Minutes, time.Seconds, date.Offset);
            _tweak.SetDate(combined);
            ShowApplied();
            UpdateStatusPill();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    // ---------------- Wizard ----------------
    private void BuildWizard()
    {
        _wizardButton = new Button { MinWidth = 110 };
        _wizardButton.Click += Wizard_Click;
        ControlHost.Children.Add(_wizardButton);
    }

    private async void Wizard_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _tweak?.WizardSteps is null || _tweak.WizardSteps.Count == 0) return;
        var steps = _tweak.WizardSteps;
        var collected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inputs = new Dictionary<string, Func<string>>();

        int index = 0;
        var host = new StackPanel { Spacing = 10, MinWidth = 360 };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = _tweak.Title.Primary,
            Content = host,
            PrimaryButtonText = Loc.I.Pick("Next", "下一步"),
            CloseButtonText = Loc.I.Pick("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };

        void RenderStep()
        {
            host.Children.Clear();
            inputs.Clear();
            var step = steps[index];
            host.Children.Add(new TextBlock
            {
                Text = $"{Loc.I.Pick("Step", "步驟")} {index + 1} / {steps.Count}",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });
            host.Children.Add(new TextBlock { Text = step.Title.Primary, FontWeight = FontWeights.SemiBold });
            host.Children.Add(new TextBlock { Text = step.Title.Secondary, FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
            host.Children.Add(new TextBlock { Text = step.Description.Primary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) });
            host.Children.Add(new TextBlock { Text = step.Description.Secondary, FontSize = 12, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

            string key = string.IsNullOrEmpty(step.Key) ? $"step{index}" : step.Key;
            switch (step.Input)
            {
                case WizardInputKind.Text:
                {
                    var tb = new TextBox { Text = step.Default ?? "", Margin = new Thickness(0, 6, 0, 0) };
                    host.Children.Add(tb);
                    inputs[key] = () => tb.Text;
                    break;
                }
                case WizardInputKind.Number:
                {
                    var nb = new NumberBox
                    {
                        Value = double.TryParse(step.Default, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0,
                        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                        Margin = new Thickness(0, 6, 0, 0),
                    };
                    host.Children.Add(nb);
                    inputs[key] = () => double.IsNaN(nb.Value) ? "" : nb.Value.ToString(CultureInfo.InvariantCulture);
                    break;
                }
                case WizardInputKind.Choice:
                {
                    var cb = new ComboBox { MinWidth = 200, Margin = new Thickness(0, 6, 0, 0) };
                    if (step.Choices is not null)
                        foreach (var c in step.Choices)
                            cb.Items.Add(new ComboBoxItem { Content = $"{c.Label.En} · {c.Label.Zh}", Tag = c.Value });
                    cb.SelectedIndex = cb.Items.Count > 0 ? 0 : -1;
                    host.Children.Add(cb);
                    inputs[key] = () => (cb.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                    break;
                }
                case WizardInputKind.Toggle:
                {
                    var ts = new ToggleSwitch
                    {
                        IsOn = string.Equals(step.Default, "true", StringComparison.OrdinalIgnoreCase),
                        OnContent = "On · 開",
                        OffContent = "Off · 熄",
                        Margin = new Thickness(0, 6, 0, 0),
                    };
                    host.Children.Add(ts);
                    inputs[key] = () => ts.IsOn ? "true" : "false";
                    break;
                }
            }

            dlg.PrimaryButtonText = index == steps.Count - 1 ? Loc.I.Pick("Finish", "完成") : Loc.I.Pick("Next", "下一步");
        }

        RenderStep();

        while (true)
        {
            var r = await dlg.ShowAsync();
            if (r != ContentDialogResult.Primary) return; // cancelled

            foreach (var kv in inputs) collected[kv.Key] = kv.Value();

            if (index < steps.Count - 1) { index++; RenderStep(); continue; }
            break; // finished
        }

        if (_tweak.WizardFinish is null) return;
        _busy = true;
        _wizardButton!.IsEnabled = false;
        var label = _wizardButton.Content;
        _wizardButton.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
        ResultBar.IsOpen = false;
        try
        {
            var result = await _tweak.WizardFinish(collected, CancellationToken.None);
            ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = result.Success ? Loc.I.Pick("Done", "完成") : Loc.I.Pick("Failed", "失敗");
            ResultBar.Message = result.Message is null ? string.Empty : $"{result.Message.En}\n{result.Message.Zh}";
            ResultBar.IsOpen = true;
            UpdateStatusPill();
        }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            _wizardButton.Content = label;
            _wizardButton.IsEnabled = true;
            _busy = false;
            RenderText();
            RefreshVisualIfLive();
        }
    }

    // ---------------- Coloured status pill ----------------
    private void UpdateStatusPill()
    {
        if (_tweak?.ColoredStatus is null)
        {
            StatusPill.Visibility = Visibility.Collapsed;
            return;
        }
        try
        {
            var (en, zh, color) = _tweak.ColoredStatus();
            StatusPillText.Text = $"{Loc.I.Pick(en, zh)} · {Loc.I.Pick(zh, en)}";
            var (bg, fg) = StatusBrushes(color);
            StatusPill.Background = bg;
            StatusPillText.Foreground = fg;
            StatusPill.Visibility = Visibility.Visible;
        }
        catch
        {
            StatusPill.Visibility = Visibility.Collapsed;
        }
    }

    private static (Brush bg, Brush fg) StatusBrushes(StatusColor color)
    {
        string key = color switch
        {
            StatusColor.Good => "SystemFillColorSuccessBackgroundBrush",
            StatusColor.Warn => "SystemFillColorCautionBackgroundBrush",
            StatusColor.Bad => "SystemFillColorCriticalBackgroundBrush",
            _ => "SystemFillColorNeutralBackgroundBrush",
        };
        string fgKey = color switch
        {
            StatusColor.Good => "SystemFillColorSuccessBrush",
            StatusColor.Warn => "SystemFillColorCautionBrush",
            StatusColor.Bad => "SystemFillColorCriticalBrush",
            _ => "TextFillColorPrimaryBrush",
        };
        var res = Application.Current.Resources;
        var bg = res.TryGetValue(key, out var b) && b is Brush bb ? bb : new SolidColorBrush(Colors.Gray);
        var fg = res.TryGetValue(fgKey, out var f) && f is Brush ff ? ff : new SolidColorBrush(Colors.Black);
        return (bg, fg);
    }

    // ---------------- Result helpers ----------------
    private void ShowApplied()
    {
        var t = _tweak!;
        string en = "Applied.", zh = "已套用。";
        switch (t.Restart)
        {
            case RestartScope.Explorer:
                en = "Applied. Restart Explorer to see the change.";
                zh = "已套用。重啟檔案總管就睇到變化。";
                break;
            case RestartScope.SignOut:
                en = "Applied. Sign out and back in to take effect.";
                zh = "已套用。登出再登入後生效。";
                break;
            case RestartScope.Reboot:
                en = "Applied. Reboot to take effect.";
                zh = "已套用。重新開機後生效。";
                break;
        }
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = Loc.I.Pick("Done", "完成");
        ResultBar.Message = $"{en}\n{zh}";
        ResultBar.ActionButton = t.Restart == RestartScope.Explorer ? MakeRestartExplorerButton() : null;
        ResultBar.IsOpen = true;
        RefreshVisualIfLive();
    }

    private void ShowError(Exception ex)
    {
        bool needAdmin = _tweak!.RequiresAdmin && !AdminHelper.IsElevated;
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = Loc.I.Pick("Failed", "失敗");
        ResultBar.Message = needAdmin
            ? "This change needs administrator rights.\n呢項更改需要管理員權限。"
            : $"{ex.Message}";
        ResultBar.ActionButton = needAdmin ? MakeRelaunchButton() : null;
        ResultBar.IsOpen = true;
    }

    private Button MakeRestartExplorerButton()
    {
        var b = new Button { Content = "Restart Explorer · 重啟檔案總管" };
        b.Click += async (_, _) =>
        {
            b.IsEnabled = false;
            await ShellRunner.RunCmd("taskkill /f /im explorer.exe & start explorer.exe");
        };
        return b;
    }

    private Button MakeRelaunchButton()
    {
        var b = new Button { Content = "Relaunch as admin · 以管理員身分重新啟動" };
        b.Click += (_, _) =>
        {
            if (AdminHelper.RelaunchElevated())
                Application.Current.Exit();
        };
        return b;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s[..max] + " …";
}
