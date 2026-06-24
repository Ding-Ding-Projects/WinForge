using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
        switch (_tweak.Kind)
        {
            case TweakKind.Toggle: BuildToggle(); break;
            case TweakKind.Action: BuildAction(); break;
            case TweakKind.Choice: BuildChoice(); break;
            case TweakKind.Info: BuildInfo(); break;
        }
        RenderText();
        UpdateBadges();
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
            _actionButton.Content = label;
            _actionButton.IsEnabled = true;
            _busy = false;
            RenderText();
        }
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
