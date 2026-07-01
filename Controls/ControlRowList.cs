using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 可重用嘅控件列表 · A reusable, pure-C# renderer that turns a set of
/// <see cref="TweakDefinition"/>s into control <b>rows</b> (not cards): the bilingual title, an
/// optional secondary line and a description on the left, and the matching WinUI control on the
/// right (ToggleSwitch / ComboBox / Slider / NumberBox / refreshable Info / Action button).
///
/// This is the lightweight replacement for <c>Controls/TweakCard</c> when a page shows a large
/// catalog of tweaks — one persistent <see cref="InfoBar"/> at the top reports every result, subtle
/// 1px dividers separate rows, and a staggered entrance transition animates them in.
///
/// 全部介面文字雙語（英文＋粵語），永不擲錯 · Bilingual, never-throw. Everything is guarded with
/// try/catch and reverts a control to its last-known state on error.
/// </summary>
public sealed class ControlRowList : UserControl
{
    private readonly StackPanel _root;      // outer column: InfoBar + rows
    private readonly InfoBar _resultBar;    // one shared, persistent results bar
    private readonly StackPanel _rows;      // the animated rows host
    private bool _busy;                     // guard so one Action runs at a time

    public ControlRowList()
    {
        _resultBar = new InfoBar
        {
            IsClosable = true,
            IsOpen = false,
            Margin = new Thickness(0, 0, 0, 8),
        };

        _rows = new StackPanel { Spacing = 0 };
        _rows.ChildrenTransitions = new TransitionCollection
        {
            new EntranceThemeTransition { IsStaggeringEnabled = true },
        };

        _root = new StackPanel { Spacing = 0 };
        _root.Children.Add(_resultBar);
        _root.Children.Add(_rows);

        Content = _root;

        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
    }

    private List<TweakDefinition> _current = new();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Unloaded -= OnUnloaded;
    }

    /// <summary>語言切換時重建所有列，令標籤重新在地化 · Rebuild every row so labels re-localise.</summary>
    private void OnLanguageChanged(object? sender, EventArgs e) => Rebuild();

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>設定要顯示嘅調校項目（會替換現有清單）· Set the tweaks to render, replacing the current set.</summary>
    public void SetTweaks(IEnumerable<TweakDefinition> tweaks)
    {
        _current = tweaks?.ToList() ?? new List<TweakDefinition>();
        Rebuild();
    }

    /// <summary>清空所有列 · Remove every row.</summary>
    public void Clear()
    {
        _current = new List<TweakDefinition>();
        _rows.Children.Clear();
    }

    // ── Rendering ──────────────────────────────────────────────────────────────

    private void Rebuild()
    {
        _rows.Children.Clear();
        bool first = true;
        foreach (var op in _current)
        {
            if (op is null) continue;
            if (!first) _rows.Children.Add(BuildDivider());
            first = false;
            try { _rows.Children.Add(BuildRow(op)); }
            catch { /* never let one bad row take the list down */ }
        }
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    // ---- One clean row: bilingual title + optional secondary + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        // Small admin / restart hints so nothing from the card chrome is lost.
        var hint = HintText(op);
        if (hint is not null)
            text.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private string? HintText(TweakDefinition op)
    {
        var parts = new List<string>();
        if (op.RequiresAdmin) parts.Add(P("Admin", "管理員"));
        switch (op.Restart)
        {
            case RestartScope.Explorer: parts.Add(P("Restart Explorer", "重啟檔案總管")); break;
            case RestartScope.SignOut: parts.Add(P("Sign out", "登出")); break;
            case RestartScope.Reboot: parts.Add(P("Reboot", "重新開機")); break;
        }
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.RadioGroup => BuildChoice(op), // rendered as a compact ComboBox in row layout
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync (with live progress + flashy result) ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");

        // Root swaps between the button and a running affordance (bar + status + spinner).
        var host = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Right };

        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        // Running affordance: a real ProgressBar + a live status line (hidden until running).
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, IsIndeterminate = true, Width = 160 };
        var running = new StackPanel { Spacing = 2, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Right };
        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var statusText = new TextBlock
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 220,
            HorizontalTextAlignment = TextAlignment.Right,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        statusRow.Children.Add(statusText);
        running.Children.Add(bar);
        running.Children.Add(statusRow);

        host.Children.Add(btn);
        host.Children.Add(running);

        btn.Click += async (_, _) =>
        {
            if (_busy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _busy = true;
            btn.Visibility = Visibility.Collapsed;
            running.Visibility = Visibility.Visible;
            bar.IsIndeterminate = true;
            bar.Value = 0;
            bar.ShowError = false;
            statusText.Text = P("Working…", "處理緊…");
            StartRowPulse(bar);

            TweakResult? result = null;
            try
            {
                result = await op.RunAsync(CancellationToken.None);
                ShowResult(result);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                StopRowPulse(bar);
                running.Visibility = Visibility.Collapsed;
                btn.Visibility = Visibility.Visible;
                _busy = false;
                // Flashy result cue on the button itself.
                if (result is not null)
                {
                    if (result.Success) AnimatePop(btn);
                    else AnimateShake(btn);
                }
                else AnimateShake(btn);
            }
        };
        return host;
    }

    // ── Row-level flashy animation helpers (created in C#) ───────────────────────

    private static void StartRowPulse(ProgressBar bar)
    {
        try
        {
            var a = new DoubleAnimation
            {
                From = 1.0,
                To = 0.55,
                Duration = new Duration(TimeSpan.FromMilliseconds(900)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(a, bar);
            Storyboard.SetTargetProperty(a, "Opacity");
            var sb = new Storyboard();
            sb.Children.Add(a);
            bar.Tag = sb;                 // stash so we can stop it
            sb.Begin();
        }
        catch { /* decorative */ }
    }

    private static void StopRowPulse(ProgressBar bar)
    {
        try { if (bar.Tag is Storyboard sb) sb.Stop(); } catch { }
        try { bar.Tag = null; bar.Opacity = 1.0; } catch { }
    }

    private static void AnimatePop(FrameworkElement el)
    {
        try
        {
            var scale = new ScaleTransform { CenterX = el.ActualWidth / 2, CenterY = el.ActualHeight / 2 };
            el.RenderTransform = scale;
            var sb = new Storyboard();
            foreach (var path in new[]
            {
                "(UIElement.RenderTransform).(ScaleTransform.ScaleX)",
                "(UIElement.RenderTransform).(ScaleTransform.ScaleY)",
            })
            {
                var a = new DoubleAnimationUsingKeyFrames();
                Storyboard.SetTarget(a, el);
                Storyboard.SetTargetProperty(a, path);
                a.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 1.0 });
                a.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)),
                    Value = 1.12,
                    EasingFunction = new BackEase { Amplitude = 0.5, EasingMode = EasingMode.EaseOut },
                });
                a.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)),
                    Value = 1.0,
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
                });
                sb.Children.Add(a);
            }
            sb.Begin();
        }
        catch { /* decorative */ }
    }

    private static void AnimateShake(FrameworkElement el)
    {
        try
        {
            var t = new TranslateTransform();
            el.RenderTransform = t;
            var a = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(a, el);
            Storyboard.SetTargetProperty(a, "(UIElement.RenderTransform).(TranslateTransform.X)");
            double[] xs = { 0, -7, 7, -5, 5, -2, 0 };
            for (int i = 0; i < xs.Length; i++)
                a.KeyFrames.Add(new EasingDoubleKeyFrame
                {
                    KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * 55)),
                    Value = xs[i],
                });
            var sb = new Storyboard();
            sb.Children.Add(a);
            sb.Begin();
        }
        catch { /* decorative */ }
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice / RadioGroup → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try { SelectComboToCurrent(combo, op); }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try { SelectComboToCurrent(combo, op); } catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    private static void SelectComboToCurrent(ComboBox combo, TweakDefinition op)
    {
        var cur = op.GetCurrentChoice?.Invoke();
        if (cur is null || op.Choices is null) return;
        for (int i = 0; i < op.Choices.Count; i++)
            if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
            { combo.SelectedIndex = i; return; }
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = op.Min,
            Maximum = op.Max,
            StepFrequency = op.Step,
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var valueLabel = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        string Fmt(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture)
                               : v.ToString("0.###", CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueLabel.Text = Fmt(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueLabel.Text = Fmt(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };

        panel.Children.Add(slider);
        panel.Children.Add(valueLabel);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step,
            LargeChange = op.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null) return;
            if (double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ── Result / status helpers (route through the one persistent InfoBar) ──────

    private void ShowResult(TweakResult result)
    {
        _resultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        _resultBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        _resultBar.Message = result.Message is not null
            ? result.Message.Get(Loc.I.Language)
            : (result.Output ?? string.Empty);
        _resultBar.IsOpen = true;
    }

    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        _resultBar.Severity = InfoBarSeverity.Success;
        _resultBar.Title = P("Done", "完成");
        _resultBar.Message = P(en, zh);
        _resultBar.IsOpen = true;
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        _resultBar.Severity = InfoBarSeverity.Error;
        _resultBar.Title = P("Failed", "失敗");
        _resultBar.Message = needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message;
        _resultBar.IsOpen = true;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
}
