using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// LightSwitch（PowerToys LightSwitch 原生克隆）· Automatically switch the Windows light/dark theme on a
/// schedule — fixed times or computed sunrise/sunset. Applies the theme live (registry + WM_SETTINGCHANGE),
/// keeps a per-minute DispatcherTimer while the app runs, and offers a schtasks background job for when the
/// app is closed. Manual "switch now" buttons, scope (apps/system/both), and a current-theme status.
/// Bilingual throughout. Backed by LightSwitchService.
/// </summary>
public sealed partial class LightSwitchModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private bool _suppress;

    public LightSwitchModule()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => OnTick();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); LoadFromSettings(); RefreshBackgroundState(); _timer.Start(); };
        Unloaded += (_, _) => _timer.Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ───────────────────────── rendering (bilingual labels) ─────────────────────────

    private void Render()
    {
        HeaderTitle.Text = "LightSwitch · 自動深淺色";
        HeaderBlurb.Text = P(
            "Automatically switch Windows between light and dark theme on a schedule — at fixed times, or following sunrise and sunset for your location.",
            "按排程自動喺淺色同深色主題之間切換 — 可以揀固定時間，或者跟你所在位置嘅日出日落。");

        StatusTitle.Text = P("Current theme", "目前主題");
        LightNowLabel.Text = P("Light now", "即刻淺色");
        DarkNowLabel.Text = P("Dark now", "即刻深色");

        EnableTitle.Text = P("Enable scheduled switching", "啟用排程切換");
        EnableSub.Text = P("Flips the theme automatically while WinForge is running. Turn on the background job below to keep switching when WinForge is closed.",
            "WinForge 開住嘅時候自動切換主題。想 WinForge 關咗都繼續切換，請開下面嘅背景工作。");

        ScopeLabel.Text = P("Apply to", "套用範圍");
        ScopeBoth.Content = P("Apps and system", "應用程式同系統");
        ScopeApps.Content = P("Apps only", "淨係應用程式");
        ScopeSystem.Content = P("System only (taskbar, Start)", "淨係系統（工作列、開始功能表）");

        ModeLabel.Text = P("Schedule mode", "排程模式");
        ModeOff.Content = P("Off — don't switch automatically", "關閉 — 唔自動切換");
        ModeFixed.Content = P("Fixed times", "固定時間");
        ModeSun.Content = P("Sunrise / sunset", "日出／日落");

        LightTimeLabel.Text = P("Light starts at", "淺色開始時間");
        DarkTimeLabel.Text = P("Dark starts at", "深色開始時間");

        SunHelp.Text = P("Enter your latitude and longitude (decimal degrees). Sunrise/sunset are computed locally — no internet needed. Use Auto-detect for a best-effort guess from your IP address.",
            "輸入你嘅緯度同經度（十進制度數）。日出日落喺本機計算，唔使上網。亦可以用「自動偵測」由 IP 位址盡力估算。");
        LatLabel.Text = P("Latitude", "緯度");
        LonLabel.Text = P("Longitude", "經度");
        SunriseOffLabel.Text = P("Sunrise offset (min)", "日出偏移（分鐘）");
        SunsetOffLabel.Text = P("Sunset offset (min)", "日落偏移（分鐘）");
        DetectLabel.Text = P("Auto-detect location", "自動偵測位置");

        BgTitle.Text = P("Background job (works when WinForge is closed)", "背景工作（WinForge 關咗都運作）");
        BgSub.Text = P("Registers a Windows Scheduled Task that checks every minute and applies the theme even when WinForge isn't open.",
            "登記一個 Windows 排程工作，每分鐘檢查一次，就算 WinForge 冇開都會套用主題。");

        UpdateStatusText();
        UpdateModePanels();
        UpdateSunPreview();
    }

    // ───────────────────────── load / save ─────────────────────────

    private void LoadFromSettings()
    {
        _suppress = true;

        EnableSwitch.IsOn = LightSwitchService.Enabled;

        ScopeRadios.SelectedIndex = LightSwitchService.Scope switch
        {
            LightSwitchScope.AppsOnly => 1,
            LightSwitchScope.SystemOnly => 2,
            _ => 0,
        };

        ModeRadios.SelectedIndex = LightSwitchService.Mode switch
        {
            LightSwitchMode.FixedHours => 1,
            LightSwitchMode.SunsetToSunrise => 2,
            _ => 0,
        };

        LightTimePicker.Time = TimeSpan.FromMinutes(LightSwitchService.LightTimeMinutes);
        DarkTimePicker.Time = TimeSpan.FromMinutes(LightSwitchService.DarkTimeMinutes);

        LatBox.Text = LightSwitchService.Latitude;
        LonBox.Text = LightSwitchService.Longitude;
        SunriseOffBox.Value = LightSwitchService.SunriseOffset;
        SunsetOffBox.Value = LightSwitchService.SunsetOffset;

        _suppress = false;

        UpdateModePanels();
        UpdateSunPreview();
        UpdateStatusText();
    }

    // ───────────────────────── event handlers ─────────────────────────

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        LightSwitchService.Enabled = EnableSwitch.IsOn;
        if (EnableSwitch.IsOn) ApplyNow();
        UpdateStatusText();
    }

    private void Scope_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        LightSwitchService.Scope = ScopeRadios.SelectedIndex switch
        {
            1 => LightSwitchScope.AppsOnly,
            2 => LightSwitchScope.SystemOnly,
            _ => LightSwitchScope.Both,
        };
        if (LightSwitchService.Enabled) ApplyNow();
        UpdateStatusText();
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        LightSwitchService.Mode = ModeRadios.SelectedIndex switch
        {
            1 => LightSwitchMode.FixedHours,
            2 => LightSwitchMode.SunsetToSunrise,
            _ => LightSwitchMode.Off,
        };
        UpdateModePanels();
        UpdateSunPreview();
        if (LightSwitchService.Enabled) ApplyNow();
        UpdateStatusText();
    }

    private void LightTime_Changed(object sender, TimePickerValueChangedEventArgs e)
    {
        if (_suppress) return;
        LightSwitchService.LightTimeMinutes = (int)LightTimePicker.Time.TotalMinutes;
        if (LightSwitchService.Enabled) ApplyNow();
        UpdateStatusText();
    }

    private void DarkTime_Changed(object sender, TimePickerValueChangedEventArgs e)
    {
        if (_suppress) return;
        LightSwitchService.DarkTimeMinutes = (int)DarkTimePicker.Time.TotalMinutes;
        if (LightSwitchService.Enabled) ApplyNow();
        UpdateStatusText();
    }

    private void Coords_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        LightSwitchService.Latitude = LatBox.Text;
        LightSwitchService.Longitude = LonBox.Text;
        UpdateSunPreview();
        if (LightSwitchService.Enabled) ApplyNow();
        UpdateStatusText();
    }

    private void Offsets_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        LightSwitchService.SunriseOffset = (int)(double.IsNaN(SunriseOffBox.Value) ? 0 : SunriseOffBox.Value);
        LightSwitchService.SunsetOffset = (int)(double.IsNaN(SunsetOffBox.Value) ? 0 : SunsetOffBox.Value);
        UpdateSunPreview();
        if (LightSwitchService.Enabled) ApplyNow();
        UpdateStatusText();
    }

    private void LightNow_Click(object sender, RoutedEventArgs e)
    {
        var r = LightSwitchService.SwitchToLightNow();
        ShowResult(r);
        UpdateStatusText();
    }

    private void DarkNow_Click(object sender, RoutedEventArgs e)
    {
        var r = LightSwitchService.SwitchToDarkNow();
        ShowResult(r);
        UpdateStatusText();
    }

    private async void Detect_Click(object sender, RoutedEventArgs e)
    {
        DetectBtn.IsEnabled = false;
        DetectRing.IsActive = true;
        try
        {
            var loc = await LightSwitchService.DetectLocationByIpAsync();
            if (loc is { } l)
            {
                _suppress = true;
                LatBox.Text = l.lat;
                LonBox.Text = l.lon;
                _suppress = false;
                LightSwitchService.Latitude = l.lat;
                LightSwitchService.Longitude = l.lon;
                UpdateSunPreview();
                UpdateStatusText();
                ShowResult(TweakResult.Ok(
                    $"Location detected: {(string.IsNullOrWhiteSpace(l.place) ? $"{l.lat}, {l.lon}" : l.place)}.",
                    $"已偵測位置：{(string.IsNullOrWhiteSpace(l.place) ? $"{l.lat}, {l.lon}" : l.place)}。"));
            }
            else
            {
                ShowResult(TweakResult.Fail("Could not detect location from your IP. Enter coordinates manually.",
                    "無法由 IP 偵測位置。請手動輸入座標。"));
            }
        }
        finally
        {
            DetectRing.IsActive = false;
            DetectBtn.IsEnabled = true;
        }
    }

    private async void Bg_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        var r = BgSwitch.IsOn
            ? await LightSwitchService.ScheduleBackgroundJob()
            : await LightSwitchService.UnscheduleBackgroundJob();
        ShowResult(r);
        if (!r.Success) await RefreshBackgroundStateAsync();
    }

    // ───────────────────────── timer ─────────────────────────

    private void OnTick()
    {
        if (LightSwitchService.Enabled)
            LightSwitchService.EvaluateAndApply(DateTime.Now);
        UpdateStatusText();
    }

    private void ApplyNow()
    {
        LightSwitchService.EvaluateAndApply(DateTime.Now);
    }

    // ───────────────────────── status / preview ─────────────────────────

    private void UpdateStatusText()
    {
        bool appsLight = LightSwitchService.AppsUseLight();
        bool sysLight = LightSwitchService.SystemUsesLight();
        string apps = appsLight ? P("light", "淺色") : P("dark", "深色");
        string sys = sysLight ? P("light", "淺色") : P("dark", "深色");

        string mode = LightSwitchService.Mode switch
        {
            LightSwitchMode.FixedHours => P("fixed times", "固定時間"),
            LightSwitchMode.SunsetToSunrise => P("sunrise/sunset", "日出／日落"),
            _ => P("off", "關閉"),
        };

        string sched;
        if (!LightSwitchService.Enabled)
            sched = P("Scheduling is off.", "排程未啟用。");
        else
        {
            var should = LightSwitchService.ShouldBeLightNow(DateTime.Now);
            sched = should switch
            {
                true => P($"Schedule ({mode}) wants light now.", $"排程（{mode}）依家想要淺色。"),
                false => P($"Schedule ({mode}) wants dark now.", $"排程（{mode}）依家想要深色。"),
                _ => P($"Schedule ({mode}) can't decide (check coordinates).", $"排程（{mode}）未能決定（請檢查座標）。"),
            };
        }

        StatusText.Text = P($"Apps: {apps}  ·  System: {sys}. {sched}",
            $"應用程式：{apps}  ·  系統：{sys}。{sched}");
    }

    private void UpdateModePanels()
    {
        bool fixedMode = ModeRadios.SelectedIndex == 1;
        bool sunMode = ModeRadios.SelectedIndex == 2;
        FixedPanel.Visibility = fixedMode ? Visibility.Visible : Visibility.Collapsed;
        SunPanel.Visibility = sunMode ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateSunPreview()
    {
        if (ModeRadios.SelectedIndex != 2) return;

        if (!LightSwitchService.CoordinatesValid(LatBox.Text, LonBox.Text))
        {
            SunPreview.Text = P("Enter valid coordinates to preview today's sunrise and sunset.",
                "輸入有效座標即可預覽今日嘅日出同日落。");
            return;
        }

        var sun = LightSwitchService.TodaySunTimes();
        if (sun is not { Valid: true } s)
        {
            SunPreview.Text = P("The sun does not rise/set at this location today (polar day or night).",
                "今日呢個位置太陽唔升／唔落（極晝或極夜）。");
            return;
        }

        int riseOff = s.SunriseMinutes + LightSwitchService.SunriseOffset;
        int setOff = s.SunsetMinutes + LightSwitchService.SunsetOffset;
        SunPreview.Text = P(
            $"Today: sunrise {Fmt(s.SunriseMinutes)} → light {Fmt(riseOff)};  sunset {Fmt(s.SunsetMinutes)} → dark {Fmt(setOff)}.",
            $"今日：日出 {Fmt(s.SunriseMinutes)} → 淺色 {Fmt(riseOff)}；日落 {Fmt(s.SunsetMinutes)} → 深色 {Fmt(setOff)}。");
    }

    private static string Fmt(int minutesOfDay)
    {
        int m = ((minutesOfDay % 1440) + 1440) % 1440;
        return $"{m / 60:00}:{m % 60:00}";
    }

    // ───────────────────────── background job state ─────────────────────────

    private async void RefreshBackgroundState() => await RefreshBackgroundStateAsync();

    private async System.Threading.Tasks.Task RefreshBackgroundStateAsync()
    {
        bool scheduled = await LightSwitchService.IsBackgroundJobScheduled();
        _suppress = true;
        BgSwitch.IsOn = scheduled;
        _suppress = false;
    }

    // ───────────────────────── result bar ─────────────────────────

    private void ShowResult(TweakResult r)
    {
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Problem", "出咗問題");
        ResultBar.Message = r.Message is null ? "" : $"{r.Message.Primary} · {r.Message.Secondary}";
        ResultBar.IsOpen = true;
    }
}
