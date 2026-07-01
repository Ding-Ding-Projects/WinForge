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
/// 粒子對撞機 · Particle Collider — a ⚛️ HEAVY reactor-powered load. The superconducting magnets need
/// enormous power (~ energy², up to ~800 MW at 14 TeV) to ramp the beam energy. Reads the live reactor
/// via <see cref="ReactorStatusApiService"/>; the beam energy only ramps toward the operator's target
/// while the reactor can supply the required MW. If available power falls short the beam is capped there;
/// if the reactor stops generating, a BEAM DUMP occurs and the module shows a prominent "needs nuclear
/// power" empty-state. While at/above the collision threshold and stable, integrated luminosity, recorded
/// events and occasional "discovery!" milestones accumulate. Pure managed, bilingual, never throws.
/// A ~500 ms <see cref="DispatcherTimer"/> drives an internal tick counter (no wall-clock sim logic).
/// </summary>
public sealed partial class ColliderModule : Page
{
    private readonly ColliderService _sim = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _suppress;

    public ColliderModule()
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
            Header.Title = "Particle Collider · 粒子對撞機";
            HeaderBlurb.Text = P(
                "A heavy reactor-powered load. The collider's superconducting magnets need enormous power to ramp the beam energy — the higher the target, the more megawatts required. The beam only climbs while the reactor can supply the demand; if power falls short the beam is capped, and if the reactor stops the beam is dumped.",
                "一個超食電嘅核電負載。對撞機嘅超導磁鐵要好大功率先可以推高束能 — 目標愈高，需要嘅兆瓦愈多。只有反應堆供得起電，束能先會爬升；電力唔夠就會被封頂，反應堆一停束流即刻傾瀉（beam dump）。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            BeamTitle.Text = P("Beam control", "束流控制");
            TargetLabel.Text = P("Target beam energy (TeV)", "目標束能（TeV）");
            EnergyTitle.Text = P("Beam energy · magnet power", "束能 · 磁鐵功率");
            PhysicsTitle.Text = P("Physics output", "物理成果");

            LumiCaption.Text = P("Integrated luminosity", "積分亮度");
            EventsCaption.Text = P("Events recorded", "已記錄事件");
            DiscoveriesCaption.Text = P("Discoveries", "發現次數");
            LastDiscoveryCaption.Text = P("Latest discovery", "最新發現");
            ResetButton.Content = P("Reset", "重設");

            UpdateRampButton();
            UpdateStep();
        }
        catch { }
    }

    private void UpdateRampButton()
    {
        RampButton.Content = _sim.Ramping ? P("Standby", "待機") : P("Ramp beam", "推升束流");
    }

    private void Target_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _sim.SetTarget(double.IsNaN(TargetSlider.Value) ? 0 : TargetSlider.Value);
            UpdateStep();
        }
        catch { }
    }

    private void Ramp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_sim.Ramping) _sim.Standby();
            else _sim.StartRamp();
            UpdateRampButton();
            UpdateStep();
        }
        catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _sim.Reset();
            _suppress = true;
            TargetSlider.Value = 0;
            _suppress = false;
            UpdateRampButton();
            UpdateStep();
        }
        catch { }
    }

    private void OnTick(object? sender, object e) => UpdateStep();

    private void UpdateStep()
    {
        try
        {
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

            // Advance the simulation one tick.
            string? discovery = _sim.Tick(available, generating);

            // ── Available reactor output meter ────────────────────────────────────────────────────
            OutputBar.Value = Math.Clamp(available, 0, OutputBar.Maximum);
            OutputValue.Text = $"{available:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");

            Color outColor = !generating
                ? Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A)
                : available > 800
                    ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)
                    : available > 300
                        ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)
                        : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A);
            OutputBar.Foreground = new SolidColorBrush(outColor);
            OutputValue.Foreground = new SolidColorBrush(outColor);

            // ── Empty-state gating ────────────────────────────────────────────────────────────────
            if (!generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Beam dumped — needs nuclear power", "束流已傾瀉 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — the beam is dumped and the collider is offline.",
                        "反應堆處於熔毀狀態 — 束流已傾瀉，對撞機停機。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up before ramping the beam.",
                            "反應堆已急停。復原並啟動先可以推升束流。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown) to power the collider magnets.",
                            "啟動反應堆（脫離 MODE 5 冷停機）先可以為對撞機磁鐵供電。");
                NeedPowerBar.IsOpen = true;
                RampButton.IsEnabled = false;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
                RampButton.IsEnabled = true;
            }

            // ── Target / beam-energy readouts ─────────────────────────────────────────────────────
            TargetValue.Text = $"{_sim.TargetTeV:0.0} TeV";

            EnergyBar.Value = Math.Clamp(_sim.BeamEnergyTeV, 0, EnergyBar.Maximum);
            EnergyValue.Text = $"{_sim.BeamEnergyTeV:0.00} TeV";

            Color energyColor = _sim.Colliding
                ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)     // green — colliding
                : _sim.BeamEnergyTeV > 0.01
                    ? Color.FromArgb(0xFF, 0x4E, 0x9A, 0xE6)  // blue — beam up, below threshold
                    : Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A); // grey — no beam
            EnergyBar.Foreground = new SolidColorBrush(energyColor);
            EnergyValue.Foreground = new SolidColorBrush(energyColor);

            // Required vs available magnet power.
            MagnetBar.Value = Math.Clamp(_sim.RequiredMW, 0, MagnetBar.Maximum);
            MagnetValue.Text = $"{_sim.RequiredMW:0} / {available:0} MW";
            bool overBudget = _sim.RequiredMW > available + 0.5;
            Color magnetColor = overBudget || _sim.PowerStarved
                ? Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A)      // orange — starved
                : Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A);     // green — within budget
            MagnetBar.Foreground = new SolidColorBrush(magnetColor);
            MagnetValue.Foreground = new SolidColorBrush(magnetColor);

            PowerCaption.Text = P(
                $"Magnets need {_sim.RequiredMW:0} MW to hold {_sim.BeamEnergyTeV:0.00} TeV; reactor is supplying {available:0} MW.",
                $"磁鐵要 {_sim.RequiredMW:0} MW 先撐得住 {_sim.BeamEnergyTeV:0.00} TeV；反應堆現供 {available:0} MW。");

            // ── Beam status line ──────────────────────────────────────────────────────────────────
            BeamStatus.Text = !generating
                ? P("Beam dumped — no reactor power.", "束流傾瀉 — 無反應堆供電。")
                : _sim.PowerStarved
                    ? P("Power-limited — beam capped by available reactor output.", "電力受限 — 束能被可用輸出封頂。")
                    : _sim.Colliding
                        ? P("Stable collisions — recording events.", "穩定對撞中 — 正在記錄事件。")
                        : _sim.Ramping
                            ? (_sim.BeamEnergyTeV < ColliderService.CollisionThresholdTeV
                                ? P("Ramping — below collision threshold.", "推升中 — 未到對撞閾值。")
                                : P("Ramping the beam.", "推升束流中。"))
                            : P("Standby — beam idle.", "待機 — 束流閒置。");

            // ── Physics readouts ──────────────────────────────────────────────────────────────────
            LumiValue.Text = P($"{_sim.IntegratedLuminosity:0.0} fb⁻¹", $"{_sim.IntegratedLuminosity:0.0} fb⁻¹");
            EventsValue.Text = $"{_sim.EventsRecorded:N0}";
            DiscoveriesValue.Text = $"{_sim.Discoveries}";
            LastDiscoveryValue.Text = string.IsNullOrEmpty(_sim.LastDiscovery)
                ? P("none yet", "尚未有")
                : DiscoveryText(_sim.LastDiscovery);

            if (discovery != null)
            {
                NeedPowerBar.Severity = InfoBarSeverity.Success;
                NeedPowerBar.Title = P("Discovery!", "有發現！");
                NeedPowerBar.Message = P(
                    $"A candidate signal has been recorded: {discovery}.",
                    $"記錄到一個候選訊號：{DiscoveryText(discovery)}。");
                NeedPowerBar.IsOpen = true;
            }
        }
        catch { }
    }

    private string DiscoveryText(string english)
    {
        switch (english)
        {
            case "unknown resonance": return P("unknown resonance", "未知共振態");
            case "exotic hadron": return P("exotic hadron", "奇異強子");
            case "long-lived boson": return P("long-lived boson", "長壽玻色子");
            case "heavy lepton": return P("heavy lepton", "重輕子");
            case "dark-sector candidate": return P("dark-sector candidate", "暗物質候選");
            case "sterile neutrino hint": return P("sterile neutrino hint", "惰性微中子跡象");
            case "beyond-standard-model signal": return P("beyond-standard-model signal", "超標準模型訊號");
            default: return P("new particle", "新粒子");
        }
    }
}
