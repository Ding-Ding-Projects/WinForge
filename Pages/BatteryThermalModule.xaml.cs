using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;
using LHM = LibreHardwareMonitor.Hardware;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內電池與散熱儀表板 · In-app Battery &amp; Thermal dashboard — live charge (Win32_Battery),
/// battery health/wear (powercfg /batteryreport), energy warnings (powercfg /energy), plus live
/// CPU/GPU temperature, fan and load sensors (LibreHardwareMonitorLib, WMI thermal-zone fallback).
/// Updates every second like System Monitor. Fully bilingual; everything runs in-app — no redirect.
/// </summary>
public sealed partial class BatteryThermalModule : Page
{
    public sealed class SensorRow : INotifyPropertyChanged
    {
        public string Key { get; }
        public string Hardware { get; }
        public string Name { get; }
        private string _typeText = "", _valueText = "";
        public string TypeText { get => _typeText; private set { if (_typeText != value) { _typeText = value; OnPC(); } } }
        public string ValueText { get => _valueText; private set { if (_valueText != value) { _valueText = value; OnPC(); } } }

        public SensorRow(SensorReading s, Func<LHM.SensorType, string> typeName)
        {
            Key = $"{s.Hardware}|{s.Name}|{s.Type}";
            Hardware = s.Hardware;
            Name = s.Name;
            Apply(s, typeName);
        }

        public void Apply(SensorReading s, Func<LHM.SensorType, string> typeName)
        {
            TypeText = typeName(s.Type);
            ValueText = s.Type switch
            {
                LHM.SensorType.Temperature => $"{Math.Round(s.Value, 1)} °C",
                LHM.SensorType.Fan => $"{Math.Round(s.Value)} RPM",
                LHM.SensorType.Load => $"{Math.Round(s.Value)} %",
                _ => $"{Math.Round(s.Value, 1)}",
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPC([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<SensorRow> _rows = new();
    private readonly BatteryThermal _engine = new();
    private const double BattTrack = 228;
    private bool _sensorsReady;
    private bool _busy;      // guards the powercfg report actions (buttons)
    private bool _ticking;   // guards the sampling tick against re-entrancy

    public BatteryThermalModule()
    {
        InitializeComponent();
        SensorList.ItemsSource = _rows;
        _timer.Tick += (_, _) => _ = Tick();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) =>
        {
            Render();
            _sensorsReady = _engine.OpenSensors();
            await Tick();
            _timer.Start();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _engine.Dispose();
        };
    }

    private void OnLanguageChanged(object? s, EventArgs e) { Render(); _ = Tick(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private string TypeName(LHM.SensorType t) => t switch
    {
        LHM.SensorType.Temperature => P("Temperature", "溫度"),
        LHM.SensorType.Fan => P("Fan", "風扇"),
        LHM.SensorType.Load => P("Load", "負載"),
        _ => t.ToString(),
    };

    private void Render()
    {
        Header.Title = "Battery & Thermal · 電池與散熱";
        HeaderBlurb.Text = P(
            "Live battery charge, health & wear, energy warnings, plus CPU/GPU temperatures, fans and load — all in-app, updating every second.",
            "即時電量、電池健康同耗損、能源警告，再加 CPU／GPU 溫度、風扇同負載 — 全部喺 app 內，每秒更新。");

        BattLabel.Text = P("Battery", "電池");
        WearLabel.Text = P("Battery health", "電池健康");
        TempLabel.Text = P("Hottest sensor", "最高溫感測器");

        HealthBtnText.Text = P("Battery health report", "電池健康報告");
        EnergyBtnText.Text = P("Energy warnings", "能源警告");

        ColHw.Text = P("Hardware", "硬件");
        ColSensor.Text = P("Sensor", "感測器");
        ColType.Text = P("Type", "類型");
        ColReading.Text = P("Reading", "讀數");

        EmptyTitle.Text = P("No hardware sensors available", "冇硬件感測器可用");
        EmptyBody.Text = P(
            "No driver-backed CPU/GPU/fan sensors were found. Run as administrator for full sensor access. A coarse thermal-zone reading (if any) is shown in the card above.",
            "搵唔到由驅動支援嘅 CPU／GPU／風扇感測器。以管理員身分執行可取得完整感測器。上面卡片會顯示粗略嘅熱區溫度（如有）。");
    }

    // ReadBattery (WMI Win32_Battery) and the sensor reads (LibreHardwareMonitor hw.Update() /
    // WMI thermal zones) are heavy; gather them off the UI thread and update the UI after the await.
    private async System.Threading.Tasks.Task Tick()
    {
        if (_ticking) return;
        _ticking = true;
        try
        {
            bool sensorsReady = _sensorsReady;
            BatterySnapshot? battery = null;
            List<SensorReading>? sensors = null;
            List<double>? zones = null;

            await System.Threading.Tasks.Task.Run(() =>
            {
                battery = BatteryThermal.ReadBattery();
                if (sensorsReady) sensors = _engine.ReadSensors();
                else zones = BatteryThermal.ReadThermalZonesCelsius();
            });

            // Back on the UI thread after the await.
            if (battery is not null) UpdateBattery(battery);
            UpdateThermal(sensors, zones);
        }
        catch
        {
            // Never let a sampling failure crash the tick / take down the page.
        }
        finally
        {
            _ticking = false;
        }
    }

    private void UpdateBattery(BatterySnapshot b)
    {
        if (!b.Present)
        {
            BattValue.Text = P("No battery", "冇電池");
            BattBar.Width = 0;
            BattStatus.Text = P("Desktop or no battery detected.", "桌面機或者偵測唔到電池。");
            BattRuntime.Text = "";
            return;
        }

        BattValue.Text = $"{b.ChargePercent}%";
        BattBar.Width = BattTrack * b.ChargePercent / 100.0;
        var (en, zh) = b.StatusText();
        BattStatus.Text = P(en, zh);
        if (b.RuntimeMinutes > 0 && !b.OnAc)
        {
            int h = b.RuntimeMinutes / 60, m = b.RuntimeMinutes % 60;
            BattRuntime.Text = P($"~{h}h {m}m remaining", $"剩餘約 {h} 小時 {m} 分鐘");
        }
        else
        {
            BattRuntime.Text = b.OnAc ? P("On AC power", "接駁電源中") : "";
        }
    }

    private void UpdateThermal(List<SensorReading>? sensors, List<double>? zones)
    {
        if (_sensorsReady)
        {
            EmptyState.Visibility = Visibility.Collapsed;
            Reconcile(sensors ?? new List<SensorReading>());

            var temps = _rows.Where(r => r.TypeText == TypeName(LHM.SensorType.Temperature)).ToList();
            // pick max temperature by parsing the leading number
            double max = double.MinValue; string? maxRow = null;
            foreach (var r in temps)
            {
                if (double.TryParse(r.ValueText.Split(' ')[0], out var v) && v > max) { max = v; maxRow = r.Name; }
            }
            if (max > double.MinValue)
            {
                TempValue.Text = $"{Math.Round(max, 1)} °C";
                TempSub.Text = maxRow ?? "";
            }
            else { TempValue.Text = "—"; TempSub.Text = ""; }
        }
        else
        {
            // WMI thermal-zone fallback
            _rows.Clear();
            EmptyState.Visibility = Visibility.Visible;
            zones ??= new List<double>();
            if (zones.Count > 0)
            {
                double max = zones.Max();
                TempValue.Text = $"{Math.Round(max, 1)} °C";
                TempSub.Text = P($"Thermal zone (WMI), {zones.Count} zone(s)", $"熱區（WMI），{zones.Count} 個區");
            }
            else
            {
                TempValue.Text = "—";
                TempSub.Text = P("No thermal data", "冇溫度資料");
            }
        }
    }

    /// <summary>Update the bound collection in place so item containers survive each refresh.</summary>
    private void Reconcile(List<SensorReading> sample)
    {
        // stable order: temperature, fan, load; then by hardware/name
        sample = sample
            .OrderBy(s => s.Type switch { LHM.SensorType.Temperature => 0, LHM.SensorType.Fan => 1, _ => 2 })
            .ThenBy(s => s.Hardware)
            .ThenBy(s => s.Name)
            .ToList();

        var present = new HashSet<string>();
        foreach (var s in sample) present.Add($"{s.Hardware}|{s.Name}|{s.Type}");
        for (int i = _rows.Count - 1; i >= 0; i--)
            if (!present.Contains(_rows[i].Key)) _rows.RemoveAt(i);

        for (int i = 0; i < sample.Count; i++)
        {
            var s = sample[i];
            string key = $"{s.Hardware}|{s.Name}|{s.Type}";
            int idx = -1;
            for (int j = i; j < _rows.Count; j++)
                if (_rows[j].Key == key) { idx = j; break; }

            if (idx == -1) { _rows.Insert(i, new SensorRow(s, TypeName)); }
            else { _rows[idx].Apply(s, TypeName); if (idx != i) _rows.Move(idx, i); }
        }
        while (_rows.Count > sample.Count) _rows.RemoveAt(_rows.Count - 1);
    }

    // ------------------------------------------------------------- actions ----
    private async void HealthReport_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true, P("Generating battery report…", "正在產生電池報告…"));
        BatteryReport report;
        try { report = await BatteryThermal.GenerateHealthReportAsync(); }
        finally { SetBusy(false, ""); }

        if (report.HasData)
        {
            WearValue.Text = $"{report.WearPercent}%";
            WearCaps.Text = P(
                $"Full charge {Fmt(report.FullChargeCapacityMwh)} / design {Fmt(report.DesignCapacityMwh)} mWh",
                $"滿充 {Fmt(report.FullChargeCapacityMwh)} / 設計 {Fmt(report.DesignCapacityMwh)} mWh");
            WearCycles.Text = report.CycleCount > 0
                ? P($"Cycle count: {report.CycleCount}", $"循環次數：{report.CycleCount}")
                : "";
            await ShowReportDialog(report);
        }
        else
        {
            await ShowMessage(P("Battery health report", "電池健康報告"),
                report.Error ?? P("No battery data available.", "冇電池資料。"));
        }
    }

    private async void EnergyReport_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true, P("Running energy diagnostics (~10s)…", "正在執行能源診斷（約 10 秒）…"));
        (int errors, int warnings, string? path, string raw) r;
        try { r = await BatteryThermal.RunEnergyReportAsync(); }
        finally { SetBusy(false, ""); }

        string summary = (r.errors < 0)
            ? P("Could not run powercfg /energy. Administrator rights may be required.",
                "無法執行 powercfg /energy，可能需要管理員權限。")
            : P($"powercfg /energy finished — {r.errors} error(s), {r.warnings} warning(s).",
                $"powercfg /energy 完成 — {r.errors} 個錯誤、{r.warnings} 個警告。");

        ActionStatus.Text = summary;

        var body = string.IsNullOrWhiteSpace(r.raw) ? summary : r.raw.Trim();
        if (r.path is not null)
            body += "\n\n" + P($"Full report saved to: {r.path}", $"完整報告儲存於：{r.path}");
        await ShowMessage(P("Energy warnings", "能源警告"), body);
    }

    private static string Fmt(long mwh) => mwh.ToString("N0");

    private void SetBusy(bool on, string status)
    {
        _busy = on;
        Busy.IsActive = on;
        HealthBtn.IsEnabled = !on;
        EnergyBtn.IsEnabled = !on;
        ActionStatus.Text = status;
    }

    private async Task ShowReportDialog(BatteryReport r)
    {
        var panel = new StackPanel { Spacing = 6 };
        void Row(string en, string zh, string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return;
            panel.Children.Add(new TextBlock { Text = $"{P(en, zh)}: {val}", TextWrapping = TextWrapping.Wrap });
        }
        Row("Device", "裝置", r.DeviceName);
        Row("Manufacturer", "製造商", r.Manufacturer);
        Row("Chemistry", "化學成分", r.Chemistry);
        Row("Design capacity", "設計容量", $"{Fmt(r.DesignCapacityMwh)} mWh");
        Row("Full charge capacity", "滿充容量", $"{Fmt(r.FullChargeCapacityMwh)} mWh");
        if (r.CycleCount > 0) Row("Cycle count", "循環次數", r.CycleCount.ToString());

        panel.Children.Add(new TextBlock
        {
            Text = P($"Battery wear: {r.WearPercent}%", $"電池耗損：{r.WearPercent}%"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 18,
            Margin = new Thickness(0, 8, 0, 0),
        });
        if (r.ReportPath is not null)
            panel.Children.Add(new TextBlock
            {
                Text = P($"Saved HTML report: {r.ReportPath}", $"已儲存 HTML 報告：{r.ReportPath}"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            });

        var dlg = new ContentDialog
        {
            Title = P("Battery health", "電池健康"),
            Content = new ScrollViewer { Content = panel, MaxHeight = 400 },
            CloseButtonText = P("Close", "關閉"),
            XamlRoot = XamlRoot,
        };
        await dlg.ShowAsync();
    }

    private async Task ShowMessage(string title, string body)
    {
        var dlg = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                    FontSize = 12,
                },
            },
            CloseButtonText = P("Close", "關閉"),
            XamlRoot = XamlRoot,
        };
        await dlg.ShowAsync();
    }
}
