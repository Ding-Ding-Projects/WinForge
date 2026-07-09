using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using WinForge.Models;
using WinForge.Services;
using Windows.UI;

namespace WinForge.Controls;

/// <summary>
/// 安裝進度回報 · A single progress report a caller pushes into an <see cref="InstallProgress"/> run.
/// 兩種用法：畀一段狀態文字（雙語），或畀百分比（0–100 或 0–1 都得）。
/// Two ways to use it: give a bilingual status line, and/or a percentage (accepts 0–100 or 0–1).
/// </summary>
public readonly struct InstallProgressReport
{
    /// <summary>百分比（0–100）· Percent 0–100 (null ⇒ leave the bar indeterminate).</summary>
    public double? Percent { get; init; }
    /// <summary>英文狀態 · English status line (optional).</summary>
    public string? StatusEn { get; init; }
    /// <summary>粵語狀態 · Cantonese status line (optional).</summary>
    public string? StatusZh { get; init; }
    /// <summary>
    /// 呢個回報係原始程序輸出，而唔係高階狀態 · This report is a raw process-output line rather than a
    /// high-level status update. Rich hosts can append it to a terminal without replacing their phase label.
    /// </summary>
    public bool IsOutputLine { get; init; }

    public InstallProgressReport(double? percent = null, string? statusEn = null, string? statusZh = null,
        bool isOutputLine = false)
    {
        // Accept either a 0–1 fraction or a 0–100 percent; normalise to 0–100.
        if (percent is double p)
        {
            if (p <= 1.0 && p > 0.0) p *= 100.0;
            Percent = Math.Clamp(p, 0, 100);
        }
        else Percent = null;
        StatusEn = statusEn;
        StatusZh = statusZh;
        IsOutputLine = isOutputLine;
    }

    /// <summary>由一段純文字狀態砌 · Build from a plain status line shown in both languages.</summary>
    public static InstallProgressReport Status(string en, string zh) => new(null, en, zh);

    /// <summary>由百分比砌 · Build from a percentage (0–100 or 0–1).</summary>
    public static InstallProgressReport Progress(double percent, string? en = null, string? zh = null)
        => new(percent, en, zh);

    /// <summary>由一行原始 stdout 砌（兩語都顯示同一行）· Build from a raw stdout line (shown in both languages).</summary>
    public static InstallProgressReport FromLine(string line) => new(null, line, line, isOutputLine: true);
}

/// <summary>
/// 可重用嘅安裝進度控件 · A reusable, pure-C# install-progress control (mirrors the style of
/// <see cref="ControlRowList"/>). Drop it into any panel, call <see cref="SetAction"/> with a bilingual
/// label and a streaming <c>RunAsync</c> delegate, and it renders a primary button that — while running —
/// shows a real <see cref="ProgressBar"/> (determinate when a percent is reported, indeterminate otherwise),
/// a live bilingual status line, a percentage label and a Cancel button, then a flashy success (green,
/// popping checkmark) or error (red, shake) state.
///
/// 全部雙語、永不擲錯、Unloaded 時清走 storyboard／事件。
/// Bilingual, never-throw (everything is guarded), and stops storyboards + unsubscribes handlers on Unloaded.
/// </summary>
public sealed class InstallProgress : UserControl
{
    // caller-supplied action
    private Func<IProgress<InstallProgressReport>, CancellationToken, Task<TweakResult>>? _runAsync;
    private string _labelEn = "Install";
    private string _labelZh = "安裝";

    // visual tree
    private readonly StackPanel _root;
    private readonly Button _actionButton;
    private readonly StackPanel _runningPanel;
    private readonly ProgressBar _bar;
    private readonly TextBlock _statusText;
    private readonly TextBlock _percentText;
    private readonly Button _cancelButton;
    private readonly FontIcon _resultIcon;        // check / cross shown on success/error
    private readonly TextBlock _resultText;
    private readonly StackPanel _resultPanel;
    private readonly TranslateTransform _shake;   // error shake transform (on the whole card)

    // run state
    private CancellationTokenSource? _cts;
    private bool _busy;
    private Storyboard? _pulse;                    // running glow/opacity pulse

    public InstallProgress()
    {
        // ── Primary action button ──
        _actionButton = new Button { MinWidth = 140, HorizontalAlignment = HorizontalAlignment.Left };
        _actionButton.Click += OnActionClick;

        // ── Running panel: bar + status + percent + cancel ──
        _bar = new ProgressBar { Minimum = 0, Maximum = 100, IsIndeterminate = true, Width = 240 };
        _percentText = new TextBlock
        {
            MinWidth = 44,
            VerticalAlignment = VerticalAlignment.Center,
            FontWeight = FontWeights.SemiBold,
        };
        _cancelButton = new Button { Content = P("Cancel", "取消"), MinWidth = 84 };
        _cancelButton.Click += OnCancelClick;

        var barRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        barRow.Children.Add(_bar);
        barRow.Children.Add(_percentText);
        barRow.Children.Add(_cancelButton);

        _statusText = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 380,
            Foreground = SafeBrush("TextFillColorSecondaryBrush", Colors.Gray),
            Margin = new Thickness(0, 4, 0, 0),
        };

        _runningPanel = new StackPanel { Spacing = 2, Visibility = Visibility.Collapsed };
        _runningPanel.Children.Add(barRow);
        _runningPanel.Children.Add(_statusText);

        // ── Result panel: icon + text (success/error) ──
        _resultIcon = new FontIcon { FontSize = 18, Glyph = "" };  // checkmark
        _resultText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
        _resultPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _resultPanel.Children.Add(_resultIcon);
        _resultPanel.Children.Add(_resultText);

        _root = new StackPanel { Spacing = 6 };
        _root.Children.Add(_actionButton);
        _root.Children.Add(_runningPanel);
        _root.Children.Add(_resultPanel);

        // Shake transform lives on the whole control for the error animation.
        _shake = new TranslateTransform();
        RenderTransform = _shake;

        Content = _root;
        UpdateLabel();

        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += OnUnloaded;
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// 設定按鈕標籤同執行委派 · Set the button's bilingual label and the streaming action to run.
    /// <paramref name="runAsync"/> gets an <see cref="IProgress{T}"/> to push status/percent and a
    /// <see cref="CancellationToken"/> that fires when the user clicks Cancel.
    /// </summary>
    public void SetAction(string labelEn, string labelZh,
        Func<IProgress<InstallProgressReport>, CancellationToken, Task<TweakResult>> runAsync)
    {
        _labelEn = labelEn ?? "Install";
        _labelZh = labelZh ?? "安裝";
        _runAsync = runAsync;
        UpdateLabel();
    }

    /// <summary>方便：由一句 stdout 造一個狀態回報 · Convenience: turn a raw stdout line into a status report.</summary>
    public static InstallProgressReport LineToReport(string line) => InstallProgressReport.FromLine(line);

    /// <summary>方便建構：new 咗即刻 SetAction · Convenience factory: new + SetAction in one call.</summary>
    public static InstallProgress Create(string labelEn, string labelZh,
        Func<IProgress<InstallProgressReport>, CancellationToken, Task<TweakResult>> runAsync)
    {
        var ctl = new InstallProgress();
        ctl.SetAction(labelEn, labelZh, runAsync);
        return ctl;
    }

    /// <summary>係咪執行緊 · Whether a run is currently in progress.</summary>
    public bool IsRunning => _busy;

    // ── Run flow ─────────────────────────────────────────────────────────────────

    private async void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || _runAsync is null) return;
        _busy = true;

        try
        {
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            // Enter running state.
            _actionButton.Visibility = Visibility.Collapsed;
            _resultPanel.Visibility = Visibility.Collapsed;
            _runningPanel.Visibility = Visibility.Visible;
            _bar.IsIndeterminate = true;
            _bar.Value = 0;
            _bar.ShowError = false;
            _bar.ShowPaused = false;
            _percentText.Text = "";
            _statusText.Text = P("Starting…", "開始緊…");
            StartPulse();

            var progress = new Progress<InstallProgressReport>(OnReport);

            TweakResult result;
            try
            {
                result = await _runAsync(progress, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                result = TweakResult.Fail("Cancelled.", "已取消。");
            }
            catch (Exception ex)
            {
                result = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
            }

            ShowResult(result);
        }
        catch
        {
            // absolute last-resort guard — never throw out of an event handler
            try { ShowResult(TweakResult.Fail("Unexpected error.", "發生未預期錯誤。")); } catch { }
        }
        finally
        {
            StopPulse();
            _runningPanel.Visibility = Visibility.Collapsed;
            _actionButton.Visibility = Visibility.Visible;
            _busy = false;
        }
    }

    private void OnReport(InstallProgressReport r)
    {
        try
        {
            if (r.Percent is double pct)
            {
                _bar.IsIndeterminate = false;
                _bar.Value = pct;
                _percentText.Text = $"{Math.Round(pct)}%";
            }
            var status = PickStatus(r.StatusEn, r.StatusZh);
            if (!string.IsNullOrWhiteSpace(status)) _statusText.Text = status;
        }
        catch { /* never let a bad report throw */ }
    }

    private string PickStatus(string? en, string? zh)
    {
        if (string.IsNullOrWhiteSpace(en) && string.IsNullOrWhiteSpace(zh)) return "";
        en ??= zh; zh ??= en;
        return P(en!, zh!);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            _statusText.Text = P("Cancelling…", "取消緊…");
            _cancelButton.IsEnabled = false;
            _cts?.Cancel();
        }
        catch { /* ignore */ }
    }

    // ── Success / error states with flashy animation ─────────────────────────────

    private void ShowResult(TweakResult result)
    {
        try
        {
            bool ok = result?.Success ?? false;
            _cancelButton.IsEnabled = true;

            _resultPanel.Visibility = Visibility.Visible;
            _resultIcon.Glyph = ok ? "" : "";     // check : error-badge
            _resultIcon.Foreground = ok
                ? SafeBrush("WinForgeBrandBrush", Color.FromArgb(0xFF, 0x54, 0xE0, 0x7E))
                : SafeBrush("WinForgeDangerBrush", Color.FromArgb(0xFF, 0xFF, 0x5F, 0x5F));

            var msg = result?.Message?.Get(Loc.I.Language);
            if (string.IsNullOrWhiteSpace(msg)) msg = ok ? P("Done", "完成") : P("Failed", "失敗");
            // Append captured output detail (trim to something readable).
            var detail = result?.Output;
            if (!string.IsNullOrWhiteSpace(detail))
            {
                var trimmed = detail!.Trim();
                if (trimmed.Length > 500) trimmed = trimmed[^500..];   // keep the tail (real error usually last)
                msg = $"{msg}\n{trimmed}";
            }
            _resultText.Text = msg;
            _resultText.Foreground = ok
                ? SafeBrush("WinForgeBrandBrush", Color.FromArgb(0xFF, 0x54, 0xE0, 0x7E))
                : SafeBrush("TextFillColorPrimaryBrush", Colors.White);

            if (ok) AnimateSuccessPop();
            else AnimateErrorShake();
        }
        catch { /* never throw from result rendering */ }
    }

    private void AnimateSuccessPop()
    {
        try
        {
            var scale = new ScaleTransform { CenterX = 9, CenterY = 9 };
            _resultIcon.RenderTransform = scale;

            var sb = new Storyboard();
            var ax = MakePop("(UIElement.RenderTransform).(ScaleTransform.ScaleX)");
            var ay = MakePop("(UIElement.RenderTransform).(ScaleTransform.ScaleY)");
            Storyboard.SetTarget(ax, _resultIcon);
            Storyboard.SetTarget(ay, _resultIcon);
            sb.Children.Add(ax);
            sb.Children.Add(ay);
            sb.Begin();
        }
        catch { /* animation is decorative — ignore faults */ }
    }

    private static DoubleAnimationUsingKeyFrames MakePop(string path)
    {
        var a = new DoubleAnimationUsingKeyFrames();
        Storyboard.SetTargetProperty(a, path);
        a.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime = KeyTime.FromTimeSpan(TimeSpan.Zero), Value = 0.4 });
        a.KeyFrames.Add(new EasingDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)),
            Value = 1.25,
            EasingFunction = new BackEase { Amplitude = 0.6, EasingMode = EasingMode.EaseOut },
        });
        a.KeyFrames.Add(new EasingDoubleKeyFrame
        {
            KeyTime = KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320)),
            Value = 1.0,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        });
        return a;
    }

    private void AnimateErrorShake()
    {
        try
        {
            var a = new DoubleAnimationUsingKeyFrames();
            Storyboard.SetTarget(a, this);
            Storyboard.SetTargetProperty(a, "(UIElement.RenderTransform).(TranslateTransform.X)");
            double[] xs = { 0, -8, 8, -6, 6, -3, 0 };
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
        catch { /* decorative — ignore */ }
    }

    // ── Running pulse (subtle glow via opacity) ──────────────────────────────────

    private void StartPulse()
    {
        try
        {
            StopPulse();
            var a = new DoubleAnimation
            {
                From = 1.0,
                To = 0.55,
                Duration = new Duration(TimeSpan.FromMilliseconds(900)),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            Storyboard.SetTarget(a, _bar);
            Storyboard.SetTargetProperty(a, "Opacity");
            _pulse = new Storyboard();
            _pulse.Children.Add(a);
            _pulse.Begin();
        }
        catch { _pulse = null; }
    }

    private void StopPulse()
    {
        try { _pulse?.Stop(); } catch { }
        _pulse = null;
        try { _bar.Opacity = 1.0; } catch { }
    }

    // ── Localisation / lifecycle ─────────────────────────────────────────────────

    private void UpdateLabel()
    {
        try { _actionButton.Content = P(_labelEn, _labelZh); } catch { }
        try { _cancelButton.Content = P("Cancel", "取消"); } catch { }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateLabel();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopPulse();
        try { _cts?.Cancel(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _cts = null;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        _actionButton.Click -= OnActionClick;
        _cancelButton.Click -= OnCancelClick;
        Unloaded -= OnUnloaded;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Brush SafeBrush(string key, Color fallback)
    {
        try
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var v) == true && v is Brush b) return b;
        }
        catch { }
        return new SolidColorBrush(fallback);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
}
