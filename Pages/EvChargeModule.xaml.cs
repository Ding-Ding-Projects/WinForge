using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 電動車快充站 · EV Fast-Charge Depot — a reactor-powered heavy load. A bank of DC fast-charge stalls
/// (up to 40) draws live electricity from the flagship reactor via <see cref="ReactorStatusApiService"/>;
/// each stall charges one vehicle at up to 350 kW, throttled together when the reactor can't supply every
/// stall. Vehicles fill, leave, and are replaced. Energy delivered (kWh) mints ⚡ into the shared
/// <see cref="ReactorEconomyService"/>. Shows a prominent "needs nuclear power" empty-state when the reactor
/// isn't generating. Pure managed, never throws, driven by an integer tick counter (not wall-clock).
/// </summary>
public sealed partial class EvChargeModule : Page
{
    private readonly EvChargeService _depot = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly ObservableCollection<StallRow> _rows = new();

    private const double SecondsPerTick = 0.5;   // sim seconds advanced per timer tick (deterministic)
    private int _ticks;                            // integer tick counter — sim clock (NOT DateTime.Now)
    private int _sinceEarn;                        // ticks since last ⚡ deposit
    private double _kwhBuffer;                     // kWh awaiting minting
    private double _deposited;                     // total ⚡ earned this session
    private bool _suppress;

    /// <summary>ListView 一行（會通知 UI）· One stall row for the ListView (classic {Binding}, INotifyPropertyChanged).</summary>
    public sealed class StallRow : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void On(string n) { try { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n)); } catch { } }

        private string _title = "";
        public string Title { get => _title; set { if (_title != value) { _title = value; On(nameof(Title)); } } }

        private double _soc;
        public double Soc { get => _soc; set { if (Math.Abs(_soc - value) > 0.001) { _soc = value; On(nameof(Soc)); } } }

        private string _socText = "";
        public string SocText { get => _socText; set { if (_socText != value) { _socText = value; On(nameof(SocText)); } } }

        private string _kwText = "";
        public string KwText { get => _kwText; set { if (_kwText != value) { _kwText = value; On(nameof(KwText)); } } }
    }

    public EvChargeModule()
    {
        InitializeComponent();
        _timer.Tick += OnTick;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        StallList.ItemsSource = _rows;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { ReactorStatusApiService.I.Start(); } catch { }
        Render();
        _ticks = 0;
        _sinceEarn = 0;
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
            Header.Title = "EV Fast-Charge Depot · 電動車快充站";
            HeaderBlurb.Text = P(
                "A reactor-powered fast-charge depot. Open the depot and each stall pulls up to 350 kW from the reactor to charge a vehicle; when it fills, it drives off and the next arrives. If the reactor can't supply every stall, power is throttled and shared. Earn ⚡ for every kWh delivered.",
                "由核電驅動嘅快充站。開站之後，每個充電位最高可以由反應堆抽 350 kW 幫車充電；充滿之後架車就會離開，下一架駛入。如果反應堆供唔起晒所有充電位，功率會自動節流平分。每交付一度電（kWh）就賺 ⚡。");

            ReactorTitle.Text = P("Available reactor output", "反應堆可用輸出");
            DepotTitle.Text = P("Depot", "充電站");
            StallsLabel.Text = P("Number of stalls (max 40)", "充電位數量（最多 40）");
            StatsTitle.Text = P("Depot status", "充電站狀態");

            ActiveCaption.Text = P("Stalls charging", "充電中充電位");
            DrawCaption.Text = P("Total draw", "總負載");
            AvgCaption.Text = P("Fleet avg SoC", "車隊平均電量");
            CompletedCaption.Text = P("Vehicles completed", "完成充電車輛");
            QueueCaption.Text = P("Queue (power-starved)", "輪候（缺電）");
            EarnedCaption.Text = P("Earned", "已賺取");
            StallListTitle.Text = P("Stalls", "充電位");

            UpdateOpenButton();
            AddButton.Content = P("Add stall", "加充電位");
            RemoveButton.Content = P("Remove stall", "減充電位");
            ResetButton.Content = P("Reset", "重設");

            UpdateStep();
        }
        catch { }
    }

    private void UpdateOpenButton()
    {
        try { OpenButton.Content = _depot.IsOpen ? P("Close depot", "關閉充電站") : P("Open depot", "開放充電站"); }
        catch { }
    }

    private void StallsBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        try
        {
            int n = (int)(double.IsNaN(StallsBox.Value) ? 0 : StallsBox.Value);
            _depot.SetStallCount(n);
            UpdateStep();
        }
        catch { }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_depot.IsOpen) _depot.Close(); else _depot.Open();
            UpdateOpenButton();
            UpdateStep();
        }
        catch { }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try { _depot.AddStalls(1); SyncStallsBox(); UpdateStep(); } catch { }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try { _depot.RemoveStalls(1); SyncStallsBox(); UpdateStep(); } catch { }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _depot.Reset();
            _deposited = 0;
            _kwhBuffer = 0;
            _sinceEarn = 0;
            UpdateOpenButton();
            UpdateStep();
        }
        catch { }
    }

    private void SyncStallsBox()
    {
        try { _suppress = true; StallsBox.Value = _depot.StallCount; _suppress = false; }
        catch { _suppress = false; }
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
            ReactorStatusSnapshot snap;
            try { snap = ReactorStatusApiService.I.LastSnapshot; } catch { snap = default; }

            // ReactorStatusSnapshot is a value struct — read fields directly.
            double available = double.IsNaN(snap.ElectricMW) || snap.ElectricMW < 0 ? 0 : snap.ElectricMW;
            string mode = string.IsNullOrWhiteSpace(snap.Mode) ? "?" : snap.Mode;
            bool scrammed = snap.IsScrammed;
            bool meltdown = snap.IsMeltdown;
            bool coldMode = mode.IndexOf("5", StringComparison.OrdinalIgnoreCase) >= 0
                            || mode.IndexOf("cold", StringComparison.OrdinalIgnoreCase) >= 0;
            bool generating = snap.IsGenerating && available > 1.0 && !scrammed && !meltdown && !coldMode;

            // Advance the simulation.
            double kwh = _depot.Tick(SecondsPerTick, available, generating);
            _kwhBuffer += kwh;

            // Earn ⚡ periodically (every ~3s) or once we've buffered enough energy. ~1 ⚡ per 4 kWh.
            _sinceEarn++;
            if ((_sinceEarn >= 6 || _kwhBuffer >= 40) && _kwhBuffer > 0)
            {
                double earn = _kwhBuffer * 0.25;
                if (earn >= 1.0)
                {
                    ReactorEconomyService.I.Earn(earn, P("EV charging revenue", "電動車充電收入"));
                    _deposited += earn;
                    _kwhBuffer = 0;
                    _depot.DrainDeliveredKwh(_depot.UndeliveredKwh);
                }
                _sinceEarn = 0;
            }

            // Reactor meter + colour.
            OutputBar.Value = Math.Clamp(available, 0, OutputBar.Maximum);
            OutputValue.Text = $"{available:0.0} MWe";
            ReactorModeText.Text = P($"Reactor mode: {mode}", $"反應堆模式：{mode}");

            Color meterColor = !generating
                ? Color.FromArgb(0xFF, 0x9A, 0x9A, 0x9A)
                : available > 800
                    ? Color.FromArgb(0xFF, 0x3D, 0xD5, 0x6A)
                    : available > 300
                        ? Color.FromArgb(0xFF, 0xE6, 0xB4, 0x2A)
                        : Color.FromArgb(0xFF, 0xE0, 0x6C, 0x3A);
            OutputBar.Foreground = new SolidColorBrush(meterColor);
            OutputValue.Foreground = new SolidColorBrush(meterColor);

            // Empty-state gating.
            if (!generating)
            {
                NeedPowerBar.Severity = meltdown ? InfoBarSeverity.Error : InfoBarSeverity.Warning;
                NeedPowerBar.Title = P("Chargers idle — needs nuclear power", "充電機閒置 — 需要核電");
                NeedPowerBar.Message = meltdown
                    ? P("Reactor is in a meltdown state — charging is halted.", "反應堆處於熔毀狀態 — 充電已停止。")
                    : scrammed
                        ? P("Reactor is scrammed. Recover and start it up to charge vehicles.", "反應堆已急停。復原並啟動先可以充電。")
                        : P("Start the reactor (bring it out of MODE 5 cold shutdown) to power the fast-charge stalls.",
                            "啟動反應堆（脫離 MODE 5 冷停機）先可以驅動快充充電位。");
                NeedPowerBar.IsOpen = true;
            }
            else
            {
                NeedPowerBar.IsOpen = false;
            }

            // Depot readouts.
            ActiveValue.Text = $"{_depot.ActiveStalls} / {_depot.StallCount}";
            DrawValue.Text = $"{_depot.TotalDrawMW:0.00} MW";
            AvgValue.Text = $"{_depot.FleetAvgSoc:0}%";
            CompletedValue.Text = $"{_depot.Completed}";
            QueueValue.Text = $"{_depot.QueueLength}";
            EarnedValue.Text = $"{_deposited:0} ⚡";

            bool throttled = generating && _depot.ActiveStalls > 0 && _depot.PerStallKw < EvChargeService.PerStallMaxKw - 0.5;
            if (!_depot.IsOpen)
                PerStallText.Text = P("Depot closed — open it to start charging vehicles.", "充電站已關閉 — 開放先可以開始充電。");
            else if (!generating)
                PerStallText.Text = P("Waiting on reactor power.", "等緊反應堆供電。");
            else if (throttled)
                PerStallText.Text = P($"Reactor can't supply every stall — throttled to {_depot.PerStallKw:0} kW per stall (max 350 kW).",
                                      $"反應堆供唔起晒所有充電位 — 每位節流至 {_depot.PerStallKw:0} kW（上限 350 kW）。");
            else
                PerStallText.Text = P($"Charging at up to {_depot.PerStallKw:0} kW per stall.", $"每位最高以 {_depot.PerStallKw:0} kW 充電。");

            SyncRows();
        }
        catch { }
    }

    private void SyncRows()
    {
        try
        {
            IReadOnlyList<EvChargeService.Stall> stalls = _depot.Stalls;

            // Trim extras.
            while (_rows.Count > stalls.Count) _rows.RemoveAt(_rows.Count - 1);
            // Add missing.
            while (_rows.Count < stalls.Count) _rows.Add(new StallRow());

            for (int i = 0; i < stalls.Count; i++)
            {
                var s = stalls[i];
                var row = _rows[i];
                row.Title = P($"Stall {s.Id}", $"充電位 {s.Id}");
                if (s.VehicleId == 0)
                {
                    row.Soc = 0;
                    row.SocText = P("empty", "空置");
                    row.KwText = "—";
                }
                else
                {
                    row.Soc = Math.Clamp(s.Soc, 0, 100);
                    row.SocText = $"{s.Soc:0}%";
                    row.KwText = s.DeliveredKw > 0.01 ? $"{s.DeliveredKw:0} kW" : P("idle", "閒置");
                }
            }
        }
        catch { }
    }
}
