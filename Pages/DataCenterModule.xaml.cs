using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 核能資料中心 · Nuclear Data Center — a HEAVY reactor-powered load. A nuclear-powered hyperscale data
/// centre: the operator sets an IT (server) load; the facility draws IT × PUE (cooling overhead), and
/// requests-served scale with online rack capacity. Reads live reactor output via
/// <see cref="ReactorStatusApiService"/>; while the reactor supplies the full draw, uptime holds
/// ~99.99 % — but when reactor output can't meet demand, racks are shed and the SLA bleeds. When the
/// reactor is not generating it runs on generator reserve and shows a prominent "needs nuclear power"
/// empty-state. Pure managed, never throws.
/// </summary>
public sealed partial class DataCenterModule : Page
{
    private readonly DataCenterService _dc = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DateTime _lastTick = DateTime.UtcNow;
    private bool _suppress;

    public DataCenterModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.Start(); } catch { }
        Render();
        _lastTick = DateTime.UtcNow;
        UpdateStep();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private void Render()
    {
        try
        {
            Header.Title = "Nuclear Data Center · 核能資料中心";
            HeaderBlurb.Text = P(
                "A nuclear-powered hyperscale data centre — a heavy load that only stays fully online while the reactor is generating. Set an IT (server) load; the facility draws IT × PUE for cooling, serves requests, and holds a 99.99% SLA. When reactor output can't meet the draw, racks are shed and uptime bleeds.",
                "核能供電嘅超大規模資料中心 — 一個重負載，淨係喺反應堆發電嗰陣先可以全面運作。設定 IT（伺服器）負載；設施會抽 IT × PUE 嚟散熱、處理請求，維持 99.99% SLA。當反應堆輸出唔夠應付時，機櫃會被削減，正常運行時間亦會下跌。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            LoadTitle.Text = P("IT load (server power)", "IT 負載（伺服器功率）");
            LoadLabel.Text = P("How many MW of servers to run (0–500 MW)", "要運行幾多 MW 嘅伺服器（0–500 MW）");
            FacilityTitle.Text = P("Facility telemetry", "設施遙測");

            DrawCaption.Text = P("Total facility draw (IT + cooling)", "設施總用電（IT + 散熱）");
            ItCaption.Text = P("IT load", "IT 負載");
            PueCaption.Text = P("PUE (cooling overhead)", "PUE（散熱開銷）");
            ReqCaption.Text = P("Requests served", "已處理請求");
            UptimeCaption.Text = P("Uptime / SLA", "正常運行時間 / SLA");
            RackCaption.Text = P("Racks online vs shed", "上線 vs 削減機櫃");

            UpdateScaleButtons();
            UpdateStep();
        }
        catch { }
    }

    private void UpdateScaleButtons()
    {
        try
        {
            ScaleUpButton.Content = P("Scale up (+50 MW)", "擴容（+50 MW）");
            ScaleDownButton.Content = P("Scale down (−50 MW)", "縮容（−50 MW）");
            ResetSlaButton.Content = P("Reset SLA", "重設 SLA");
        }
        catch { }
    }

    private void LoadSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _dc.SetItLoad(double.IsNaN(LoadSlider.Value) ? 0 : LoadSlider.Value);
            UpdateStep();
        }
        catch { }
    }

    private void ScaleUp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _dc.ScaleUp(50);
            SyncSliderFromModel();
            UpdateStep();
        }
        catch { }
    }

    private void ScaleDown_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _dc.ScaleDown(50);
            SyncSliderFromModel();
            UpdateStep();
        }
        catch { }
    }

    private void ResetSla_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _dc.ResetSla();
            UpdateStep();
        }
        catch { }
    }

    private void SyncSliderFromModel()
    {
        _suppress = true;
        try { LoadSlider.Value = _dc.ItLoadMW; } catch { }
        _suppress = false;
    }

    private void OnTick(object? sender, object e) => UpdateStep();

    private void UpdateStep()
    {
        try
        {
            var now = DateTime.UtcNow;
            double dt = Math.Clamp((now - _lastTick).TotalSeconds, 0.0, 2.0);
            _lastTick = now;

            ReactorStatusSnapshot snap;
            try { snap = ReactorStatusApiService.I.LastSnapshot; } catch { snap = default; }

            // ReactorStatusSnapshot is a value struct — LastSnapshot is always present (defaults to Offline).
            double available = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool scrammed = snap.IsScrammed;
            bool meltdown = snap.IsMeltdown;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && available > 1.0 && !scrammed && !meltdown && !coldMode;

            _dc.Tick(dt, available, generating);

            // Live reactor output meter + colour.
            OutputBar.Value = Math.Clamp(available, 0, OutputBar.Maximum);
            OutputValue.Text = $"{available:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");

            Color meterColor = !generating
                ? Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A)   // grey — idle
                : available > 800
                    ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)   // green — strong
                    : available > 300
                        ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)   // amber — moderate
                        : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A);  // orange — low
            OutputBar.Foreground = new SolidColorBrush(meterColor);
            OutputValue.Foreground = new SolidColorBrush(meterColor);

            // Empty-state gating.
            if (!generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("On generator reserve — needs nuclear power", "靠後備發電機 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the data centre is running on emergency reserve only. Uptime is bleeding.", "反應堆處於熔毀狀態 — 資料中心淨係靠緊急後備供電，正常運行時間正在下跌。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up to power the data centre. Racks are shedding.", "反應堆已急停。復原並啟動先可以為資料中心供電，機櫃正在削減。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown) to power the data centre. Only a thin generator reserve is keeping racks alive.",
                            "啟動反應堆（脫離 MODE 5 冷停機）先可以為資料中心供電。而家淨係得薄弱嘅後備電力吊住機櫃。");
                NeedPowerBar.IsOpen = true;
            }
            else if (_dc.ShedItMW > 0.5)
            {
                // Generating but can't meet the full draw — racks shedding.
                NeedPowerBar.Severity = InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Reactor output can't meet the draw", "反應堆輸出唔夠應付用電");
                NeedPowerBar.Message = P(
                    $"Shedding {_dc.ShedItMW:0.0} MW of IT load — scale down, or raise reactor output.",
                    $"正在削減 {_dc.ShedItMW:0.0} MW IT 負載 — 縮容，或者提高反應堆輸出。");
                NeedPowerBar.IsOpen = true;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
            }

            // Total facility draw meter + value.
            DrawBar.Value = Math.Clamp(_dc.TotalDrawMW, 0, DrawBar.Maximum);
            DrawValue.Text = $"{_dc.TotalDrawMW:0.0} MW";
            Color drawColor = _dc.TotalDrawMW > available && (generating)
                ? Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A)   // orange — draw exceeds supply
                : Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A);  // green — within supply
            DrawBar.Foreground = new SolidColorBrush(drawColor);

            // Facility readouts.
            ItValue.Text = $"{_dc.ItLoadMW:0} MW";
            PueValue.Text = $"{_dc.Pue:0.00}";
            ReqValue.Text = $"{_dc.RequestsPerSec:N0}/s";
            UptimeValue.Text = $"{_dc.UptimePercent:0.000}%";

            Color slaColor = _dc.UptimePercent >= 99.9
                ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)
                : _dc.UptimePercent >= 99.0
                    ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)
                    : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A);
            UptimeValue.Foreground = new SolidColorBrush(slaColor);

            RackValue.Text = _dc.ShedRacks > 0
                ? P($"{_dc.OnlineRacks} online · {_dc.ShedRacks} shed", $"{_dc.OnlineRacks} 上線 · {_dc.ShedRacks} 削減")
                : P($"{_dc.OnlineRacks} online · all powered", $"{_dc.OnlineRacks} 上線 · 全部有電");
            RackValue.Foreground = new SolidColorBrush(_dc.ShedRacks > 0
                ? Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A)
                : Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A));
        }
        catch { }
    }
}
