using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 保持喚醒（PowerToys Awake 式）· Keep-awake module — stop the PC sleeping/dimming, optionally for a set
/// time. Persists across pages while WinForge runs (SetThreadExecutionState). No redirect. Bilingual.
/// </summary>
public sealed partial class AwakeModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _remaining; // seconds; 0 = indefinite
    private bool _suppress;

    public AwakeModule()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Tick();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); SyncFromState(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Awake · 保持喚醒";
        HeaderBlurb.Text = P("Keep this PC awake — no sleep, no screen timeout — while WinForge is running. Great for downloads, installs or presentations.",
            "WinForge 開住嘅時候令電腦保持清醒 — 唔瞓、螢幕唔熄。下載、安裝或者做簡報啱用。");
        ToggleTitle.Text = P("Keep the PC awake", "保持電腦清醒");
        DisplayChk.Content = P("Keep the screen on too", "連螢幕都唔好熄");
        MinutesLabel.Text = P("Auto-off after (minutes, 0 = never)", "幾耐後自動關（分鐘，0 = 唔關）");
        UpdateStatus();
    }

    private void SyncFromState()
    {
        _suppress = true;
        AwakeSwitch.IsOn = AwakeService.Active;
        DisplayChk.IsChecked = AwakeService.KeepDisplay;
        _suppress = false;
        UpdateStatus();
    }

    private void Awake_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (AwakeSwitch.IsOn)
        {
            AwakeService.KeepAwake(DisplayChk.IsChecked == true);
            int mins = (int)(double.IsNaN(MinutesBox.Value) ? 0 : MinutesBox.Value);
            _remaining = mins * 60;
            if (_remaining > 0) _timer.Start(); else _timer.Stop();
        }
        else
        {
            AwakeService.AllowSleep();
            _timer.Stop();
            _remaining = 0;
        }
        UpdateStatus();
    }

    private void Display_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (AwakeSwitch.IsOn) AwakeService.KeepAwake(DisplayChk.IsChecked == true);
    }

    private void Tick()
    {
        if (_remaining > 0)
        {
            _remaining--;
            if (_remaining <= 0)
            {
                _timer.Stop();
                AwakeService.AllowSleep();
                _suppress = true; AwakeSwitch.IsOn = false; _suppress = false;
            }
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (!AwakeService.Active)
            StatusText.Text = P("Sleep & screen timeout follow your power plan.", "瞓覺同熄螢幕跟返你嘅電源計劃。");
        else if (_remaining > 0)
            StatusText.Text = P($"Awake — {_remaining / 60:00}:{_remaining % 60:00} left", $"清醒中 — 仲有 {_remaining / 60:00}:{_remaining % 60:00}");
        else
            StatusText.Text = P("Awake — until turned off", "清醒中 — 直到關閉為止");
    }
}
