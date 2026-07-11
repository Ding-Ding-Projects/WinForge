using System;
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 計時器・碼錶・番茄鐘 · Timer, Stopwatch &amp; Pomodoro — three independent modes, each driven by its
/// own DispatcherTimer. Pure managed, no side-effects. All timers are stopped on Unloaded and the
/// language handler is detached to avoid leaks. Robust: handlers never throw. Bilingual (粵語).
/// </summary>
public sealed partial class TimerModule : Page
{
    // Stopwatch — high-resolution base timestamp + accumulated elapsed while paused.
    private readonly DispatcherTimer _swTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Stopwatch _sw = new();
    private TimeSpan _swAccum = TimeSpan.Zero;
    private bool _swRunning;
    private int _swLapNo;
    private TimeSpan _swLastLap = TimeSpan.Zero;

    // Countdown.
    private readonly DispatcherTimer _cdTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _cdRemaining;
    private bool _cdRunning;
    private bool _cdDone;

    // Pomodoro.
    private readonly DispatcherTimer _pomoTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _pomoRemaining;
    private bool _pomoRunning;
    private bool _pomoWorkPhase = true;
    private int _pomoCycles;
    private bool _languageSubscribed;

    public TimerModule()
    {
        InitializeComponent();

        _swTimer.Tick += (_, _) => SwTick();
        _cdTimer.Tick += (_, _) => CdTick();
        _pomoTimer.Tick += (_, _) => PomoTick();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SubscribeLanguage();
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PauseForUnload();
        UnsubscribeLanguage();
    }

    private void SubscribeLanguage()
    {
        if (_languageSubscribed) return;
        Loc.I.LanguageChanged += OnLang;
        _languageSubscribed = true;
    }

    private void UnsubscribeLanguage()
    {
        if (!_languageSubscribed) return;
        Loc.I.LanguageChanged -= OnLang;
        _languageSubscribed = false;
    }

    /// <summary>
    /// Stop every timer at the page boundary and keep each state flag truthful. The stopwatch is
    /// explicitly accumulated before reset so a later reload shows the paused elapsed time rather
    /// than continuing invisibly while its UI timer is stopped.
    /// </summary>
    private void PauseForUnload()
    {
        if (_swRunning)
        {
            _swAccum += _sw.Elapsed;
            _sw.Reset();
            _swRunning = false;
        }
        _swTimer.Stop();

        _cdTimer.Stop();
        _cdRunning = false;

        _pomoTimer.Stop();
        _pomoRunning = false;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Timer · 計時器";
            HeaderBlurb.Text = P("A stopwatch, a countdown timer and a Pomodoro focus clock in one place — all fully offline.",
                "碼錶、倒數計時器同番茄鐘一次過齊晒 — 全部離線運作。");

            SwTab.Header = P("Stopwatch", "碼錶");
            CdTab.Header = P("Countdown", "倒數");
            PomoTab.Header = P("Pomodoro", "番茄鐘");

            // Stopwatch buttons.
            SwStartBtn.Content = _swRunning ? P("Stop", "停止") : P("Start", "開始");
            SwLapBtn.Content = P("Lap", "分段");
            SwResetBtn.Content = P("Reset", "重設");

            // Countdown.
            CdMinLabel.Text = P("Minutes", "分鐘");
            CdSecLabel.Text = P("Seconds", "秒");
            CdStartBtn.Content = _cdRunning ? P("Pause", "暫停") : P("Start", "開始");
            CdResetBtn.Content = P("Reset", "重設");

            // Pomodoro.
            PomoWorkLabel.Text = P("Work (min)", "工作（分）");
            PomoBreakLabel.Text = P("Break (min)", "休息（分）");
            PomoStartBtn.Content = _pomoRunning ? P("Pause", "暫停") : P("Start", "開始");
            PomoResetBtn.Content = P("Reset", "重設");
            RenderPomoPhase();

            // A page can be unloaded while a timer is active. Render the paused snapshot after a
            // subsequent load so button labels, elapsed values and running flags stay coherent.
            SwUpdateDisplay();
            CdUpdateDisplay();
            PomoUpdateDisplay();

            UpdateStatus();
        }
        catch { /* never throw from UI render */ }
    }

    private void SetStatus(string text, bool alert = false)
    {
        StatusText.Text = text;
        StatusText.Foreground = alert
            ? new SolidColorBrush(Colors.OrangeRed)
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    }

    private void UpdateStatus()
    {
        try
        {
            int idx = ModePivot?.SelectedIndex ?? 0;
            if (idx == 1 && _cdDone)
            {
                SetStatus(P("DONE — countdown finished! · 完成 — 倒數結束！",
                            "完成 — 倒數結束！ · DONE"), alert: true);
                return;
            }
            switch (idx)
            {
                case 0:
                    SetStatus(_swRunning ? P("Running…", "計時中…") : P("Stopped.", "已停止。"));
                    break;
                case 1:
                    SetStatus(_cdRunning ? P("Counting down…", "倒數緊…") : P("Ready.", "準備就緒。"));
                    break;
                default:
                    SetStatus(_pomoRunning ? P("Focus session running…", "專注時段進行中…") : P("Ready.", "準備就緒。"));
                    break;
            }
        }
        catch { }
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e) => UpdateStatus();

    // ============================ STOPWATCH ============================

    private void Sw_StartStop(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_swRunning)
            {
                _swAccum += _sw.Elapsed;
                _sw.Reset();
                _swTimer.Stop();
                _swRunning = false;
            }
            else
            {
                _sw.Restart();
                _swTimer.Start();
                _swRunning = true;
            }
            SwStartBtn.Content = _swRunning ? P("Stop", "停止") : P("Start", "開始");
            SwUpdateDisplay();
            UpdateStatus();
        }
        catch { }
    }

    private void Sw_Lap(object sender, RoutedEventArgs e)
    {
        try
        {
            TimeSpan total = SwElapsed();
            TimeSpan split = total - _swLastLap;
            _swLastLap = total;
            _swLapNo++;
            string line = P($"Lap {_swLapNo}   +{TimerService.FormatStopwatch(split)}   ({TimerService.FormatStopwatch(total)})",
                            $"第 {_swLapNo} 段   +{TimerService.FormatStopwatch(split)}   （{TimerService.FormatStopwatch(total)}）");
            SwLaps.Items.Insert(0, line);
        }
        catch { }
    }

    private void Sw_Reset(object sender, RoutedEventArgs e)
    {
        try
        {
            _swTimer.Stop();
            _sw.Reset();
            _swAccum = TimeSpan.Zero;
            _swRunning = false;
            _swLapNo = 0;
            _swLastLap = TimeSpan.Zero;
            SwLaps.Items.Clear();
            SwStartBtn.Content = P("Start", "開始");
            SwUpdateDisplay();
            UpdateStatus();
        }
        catch { }
    }

    private TimeSpan SwElapsed() => _swAccum + (_swRunning ? _sw.Elapsed : TimeSpan.Zero);

    private void SwTick() => SwUpdateDisplay();

    private void SwUpdateDisplay()
    {
        SwDisplay.Text = TimerService.FormatStopwatch(SwElapsed());
    }

    // ============================ COUNTDOWN ============================

    private void Cd_StartPause(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_cdRunning)
            {
                _cdTimer.Stop();
                _cdRunning = false;
            }
            else
            {
                if (_cdRemaining <= 0 || _cdDone)
                {
                    int mins = TimerService.ClampBox(CdMinBox.Value, 0, 999);
                    int secs = TimerService.ClampBox(CdSecBox.Value, 0, 59);
                    _cdRemaining = mins * 60 + secs;
                    _cdDone = false;
                }
                if (_cdRemaining <= 0) { UpdateStatus(); return; }
                _cdTimer.Start();
                _cdRunning = true;
            }
            CdStartBtn.Content = _cdRunning ? P("Pause", "暫停") : P("Start", "開始");
            CdUpdateDisplay();
            UpdateStatus();
        }
        catch { }
    }

    private void Cd_Reset(object sender, RoutedEventArgs e)
    {
        try
        {
            _cdTimer.Stop();
            _cdRunning = false;
            _cdRemaining = 0;
            _cdDone = false;
            CdStartBtn.Content = P("Start", "開始");
            CdDisplay.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            CdUpdateDisplay();
            UpdateStatus();
        }
        catch { }
    }

    private void CdTick()
    {
        try
        {
            if (_cdRemaining > 0) _cdRemaining--;
            if (_cdRemaining <= 0)
            {
                _cdTimer.Stop();
                _cdRunning = false;
                _cdDone = true;
                CdStartBtn.Content = P("Start", "開始");
                CdDisplay.Text = P("DONE", "完成");
                CdDisplay.Foreground = new SolidColorBrush(Colors.OrangeRed);
                UpdateStatus();
                return;
            }
            CdUpdateDisplay();
        }
        catch { }
    }

    private void CdUpdateDisplay()
    {
        if (!_cdDone)
        {
            CdDisplay.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
            CdDisplay.Text = TimerService.FormatCountdown(_cdRemaining);
        }
    }

    // ============================ POMODORO ============================

    private void Pomo_StartPause(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_pomoRunning)
            {
                _pomoTimer.Stop();
                _pomoRunning = false;
            }
            else
            {
                if (_pomoRemaining <= 0)
                {
                    _pomoWorkPhase = true;
                    _pomoRemaining = TimerService.ClampBox(PomoWorkBox.Value, 1, 180) * 60;
                }
                _pomoTimer.Start();
                _pomoRunning = true;
            }
            PomoStartBtn.Content = _pomoRunning ? P("Pause", "暫停") : P("Start", "開始");
            RenderPomoPhase();
            PomoUpdateDisplay();
            UpdateStatus();
        }
        catch { }
    }

    private void Pomo_Reset(object sender, RoutedEventArgs e)
    {
        try
        {
            _pomoTimer.Stop();
            _pomoRunning = false;
            _pomoWorkPhase = true;
            _pomoCycles = 0;
            _pomoRemaining = 0;
            PomoStartBtn.Content = P("Start", "開始");
            RenderPomoPhase();
            PomoUpdateDisplay();
            UpdateStatus();
        }
        catch { }
    }

    private void PomoTick()
    {
        try
        {
            if (_pomoRemaining > 0) _pomoRemaining--;
            if (_pomoRemaining <= 0)
            {
                // Switch phase.
                if (_pomoWorkPhase)
                {
                    _pomoCycles++;
                    _pomoWorkPhase = false;
                    _pomoRemaining = TimerService.ClampBox(PomoBreakBox.Value, 1, 180) * 60;
                }
                else
                {
                    _pomoWorkPhase = true;
                    _pomoRemaining = TimerService.ClampBox(PomoWorkBox.Value, 1, 180) * 60;
                }
                RenderPomoPhase();
            }
            PomoUpdateDisplay();
        }
        catch { }
    }

    private void RenderPomoPhase()
    {
        try
        {
            PomoPhase.Text = _pomoWorkPhase ? P("Work · 工作", "工作 · Work") : P("Break · 休息", "休息 · Break");
            PomoPhase.Foreground = _pomoWorkPhase
                ? new SolidColorBrush(Colors.OrangeRed)
                : new SolidColorBrush(Colors.MediumSeaGreen);
            PomoCycles.Text = P($"Completed work sessions: {_pomoCycles}", $"已完成工作時段：{_pomoCycles}");
        }
        catch { }
    }

    private void PomoUpdateDisplay()
    {
        int show = _pomoRemaining > 0 ? _pomoRemaining : TimerService.ClampBox(PomoWorkBox.Value, 1, 180) * 60;
        PomoDisplay.Text = TimerService.FormatCountdown(show);
    }
}
