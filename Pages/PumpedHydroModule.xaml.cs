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
/// 抽水蓄能水力發電 · Pumped-Storage Hydro — a reactor-tied grid BUFFER. PUMP consumes the reactor's spare
/// electricity to lift water into an upper reservoir (storing surplus nuclear power); GENERATE releases it
/// back through turbines to produce MWe and earn ⚡ — even when the reactor is down, so it backs up the grid.
/// Round-trip efficiency ~80%. An Auto mode charges on spare and discharges when the reactor can't generate.
/// Pure managed, never throws.
/// </summary>
public sealed partial class PumpedHydroModule : Page
{
    private readonly PumpedHydroService _hydro = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private int _ticks;          // integer tick counter (no DateTime.Now)
    private bool _suppress;

    public PumpedHydroModule()
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
        try
        {
            _suppress = true;
            if (ModeCombo.Items.Count == 0)
            {
                ModeCombo.Items.Add(new ComboBoxItem());
                ModeCombo.Items.Add(new ComboBoxItem());
                ModeCombo.Items.Add(new ComboBoxItem());
            }
            SyncModeCombo();
            _suppress = false;
        }
        catch { _suppress = false; }
        Render();
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
            Header.Title = "Pumped-Storage Hydro · 抽水蓄能";
            HeaderBlurb.Text = P(
                "A reactor-tied grid buffer. Pump water uphill with the reactor's surplus power to store energy, then release it downhill through turbines to back up the grid — even when the reactor can't generate.",
                "一個掛住反應堆嘅電網緩衝。用反應堆多餘電力抽水上山儲能，之後放水落山經水輪機發電撐住電網 — 就算反應堆冇得發電都得。");

            ReactorTitle.Text = P("Reactor output", "反應堆輸出");
            ReservoirTitle.Text = P("Upper reservoir level (stored energy)", "上水塘水位（儲存能量）");
            StoredCaption.Text = P("Stored energy", "儲存能量");
            EfficiencyText.Text = P(
                $"Round-trip efficiency ~{PumpedHydroService.RoundTripEfficiency * 100:0}% — pumping stores less than you get back. This stores surplus nuclear power for when the reactor can't generate.",
                $"往返效率約 {PumpedHydroService.RoundTripEfficiency * 100:0}% — 抽水儲起嘅比放返出嚟少。呢個係將多餘核電儲起，等反應堆冇得發電時用。");

            ControlsTitle.Text = P("Operating mode", "運作模式");
            ModeLabel.Text = P("Mode", "模式");
            PumpLabel.Text = P("Pump draw", "抽水功率");
            AutoTitle.Text = P("Auto mode", "自動模式");
            AutoHint.Text = P("Pump when the reactor has spare power; generate when the reactor is down.",
                              "反應堆有多餘電就抽水；反應堆停咗就發電。");
            ResetButton.Content = P("Reset", "重設");

            FlowTitle.Text = P("Power flow", "功率流向");
            InCaption.Text = P("Pumping (draw)", "抽水（耗電）");
            OutCaption.Text = P("Generating (output)", "發電（輸出）");
            EarnedCaption.Text = P("Earned this session", "今次賺到");

            SyncModeCombo();
            UpdateStep();
        }
        catch { }
    }

    private void SyncModeCombo()
    {
        try
        {
            if (ModeCombo.Items.Count < 3) return;
            _suppress = true;
            ((ComboBoxItem)ModeCombo.Items[0]).Content = P("Pump (store)", "抽水（儲能）");
            ((ComboBoxItem)ModeCombo.Items[1]).Content = P("Hold (idle)", "保持（閒置）");
            ((ComboBoxItem)ModeCombo.Items[2]).Content = P("Generate (release)", "發電（放水）");
            ModeCombo.SelectedIndex = _hydro.Mode switch
            {
                HydroMode.Pump => 0,
                HydroMode.Generate => 2,
                _ => 1,
            };
            ModeCombo.IsEnabled = !_hydro.Auto;
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _hydro.SetMode(ModeCombo.SelectedIndex switch
            {
                0 => HydroMode.Pump,
                2 => HydroMode.Generate,
                _ => HydroMode.Hold,
            });
            UpdateStep();
        }
        catch { }
    }

    private void Pump_Changed(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        UpdateStep();
    }

    private void Auto_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _hydro.SetAuto(AutoSwitch.IsOn);
            SyncModeCombo();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _hydro.Reset();
            _suppress = true;
            AutoSwitch.IsOn = false;
            PumpSlider.Value = 200;
            _suppress = false;
            SyncModeCombo();
            UpdateStep();
        }
        catch { }
    }

    private void OnTick(object? sender, object e)
    {
        _ticks++;
        UpdateStep();
    }

    private void UpdateStep()
    {
        try
        {
            // ReactorStatusSnapshot is a value struct — LastSnapshot is always present (defaults to Offline).
            ReactorStatusSnapshot snap;
            try { snap = ReactorStatusApiService.I.LastSnapshot; } catch { snap = default; }

            double reactorMW = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool reactorGenerating = snap.IsGenerating && reactorMW > 1.0 && !snap.IsScrammed && !snap.IsMeltdown && !coldMode;

            // Fixed dt per tick from the timer interval (integer counter, no wall clock).
            const double dt = 0.5;
            double requestPump = double.IsNaN(PumpSlider.Value) ? 0 : PumpSlider.Value;

            double earned = _hydro.Tick(dt, reactorMW, reactorGenerating, requestPump);
            if (earned >= 1)
            {
                try { ReactorEconomyService.I.Earn(earned, P("Hydro peaking revenue", "抽水蓄能收入")); } catch { }
            }

            // Reactor meter.
            ReactorBar.Value = Math.Clamp(reactorMW, 0, ReactorBar.Maximum);
            ReactorValue.Text = $"{reactorMW:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");
            Color rColor = !reactorGenerating
                ? Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A)
                : reactorMW > 800 ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)
                : reactorMW > 300 ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)
                : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A);
            ReactorBar.Foreground = new SolidColorBrush(rColor);
            ReactorValue.Foreground = new SolidColorBrush(rColor);

            // Reservoir meter.
            ReservoirBar.Value = Math.Clamp(_hydro.LevelPercent, 0, 100);
            StoredValue.Text = $"{_hydro.StoredMWh:0} / {PumpedHydroService.CapacityMWh:0} MWh ({_hydro.LevelPercent:0}%)";

            // Pump slider readout.
            PumpValue.Text = $"{requestPump:0} MW";

            // Sync combo selection if Auto changed the mode under us.
            SyncModeCombo();

            // Flows.
            InValue.Text = $"{_hydro.PumpDrawMW:0.0} MW";
            OutValue.Text = $"{_hydro.GenOutMW:0.0} MW";
            EarnedValue.Text = $"{_hydro.EarnedTotal:0} {ReactorEconomyService.Symbol}";
            StatusNote.Text = _hydro.StatusNote;

            // Guidance InfoBar.
            if (_hydro.Mode == HydroMode.Generate && _hydro.IsEmpty)
            {
                NeedPowerBar.Severity = InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Reservoir empty", "水塘空咗");
                NeedPowerBar.Message = P("Reservoir empty — pump first (needs reactor power).",
                                         "水塘空咗 — 要先抽水（需要反應堆電力）。");
                NeedPowerBar.IsOpen = true;
            }
            else if (_hydro.Mode == HydroMode.Pump && !reactorGenerating)
            {
                NeedPowerBar.Severity = InfoBarSeverity.Informational;
                NeedPowerBar.Title = P("Needs reactor power to pump", "抽水需要反應堆電力");
                NeedPowerBar.Message = P("Start the reactor (out of MODE 5 cold shutdown) to pump water uphill. You can still GENERATE to back up the grid.",
                                         "啟動反應堆（脫離 MODE 5 冷停機）先可以抽水。你仍然可以用發電模式撐住電網。");
                NeedPowerBar.IsOpen = true;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
            }
        }
        catch { }
    }
}
