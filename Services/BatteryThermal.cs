using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LHM = LibreHardwareMonitor.Hardware;

namespace WinForge.Services;

/// <summary>即時電池讀數 · A live battery snapshot from the Win32_Battery WMI class.</summary>
public sealed class BatterySnapshot
{
    public bool Present { get; init; }
    public int ChargePercent { get; init; }
    public int StatusCode { get; init; }
    public bool OnAc { get; init; }
    public bool Charging { get; init; }
    public int RuntimeMinutes { get; init; } // EstimatedRunTime; 71582788 == unknown / on AC
    public string DeviceName { get; init; } = "";

    /// <summary>Human label for the BatteryStatus code (EN/ZH pair).</summary>
    public (string En, string Zh) StatusText() => StatusCode switch
    {
        1 => ("Discharging", "放電中"),
        2 => ("On AC (plugged in)", "接駁電源（已插電）"),
        3 => ("Fully charged", "已充滿"),
        4 => ("Low", "電量低"),
        5 => ("Critical", "電量嚴重不足"),
        6 => ("Charging", "充電中"),
        7 => ("Charging (high)", "充電中（高）"),
        8 => ("Charging (low)", "充電中（低）"),
        9 => ("Charging (critical)", "充電中（嚴重不足）"),
        10 => ("Undefined", "未定義"),
        11 => ("Partially charged", "部分充電"),
        _ => ("Unknown", "未知"),
    };
}

/// <summary>由 powercfg /batteryreport 解析出嘅健康／耗損 · Parsed battery health &amp; wear report.</summary>
public sealed record BatteryReport
{
    public bool HasData { get; init; }
    public string? DeviceName { get; init; }
    public string? Manufacturer { get; init; }
    public string? Chemistry { get; init; }
    public long DesignCapacityMwh { get; init; }
    public long FullChargeCapacityMwh { get; init; }
    public int CycleCount { get; init; }
    public double WearPercent { get; init; }
    public string? ReportPath { get; init; }
    public string? Error { get; init; }
}

/// <summary>一個感測器讀數（溫度／風扇／負載）· A single hardware sensor reading.</summary>
public sealed class SensorReading
{
    public string Hardware { get; init; } = "";
    public string Name { get; init; } = "";
    public LHM.SensorType Type { get; init; }
    public float Value { get; init; }
}

/// <summary>
/// 電池 + 散熱資料來源 · Battery + thermal data source.
/// 電池：Win32_Battery 即時讀數 + powercfg /batteryreport 健康報告 + powercfg /energy 警告。
/// 散熱：LibreHardwareMonitorLib（CPU/GPU 溫度、風扇、負載），冇驅動時回退到 MSAcpi_ThermalZoneTemperature WMI。
/// Battery: live Win32_Battery + powercfg /batteryreport health + powercfg /energy warnings.
/// Thermal: LibreHardwareMonitorLib (CPU/GPU temps, fans, load), falling back to the
/// MSAcpi_ThermalZoneTemperature WMI class when no admin-level driver is available.
/// </summary>
public sealed class BatteryThermal : IDisposable
{
    // ---------------------------------------------------------------- battery (live) ----
    public static BatterySnapshot ReadBattery()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining, BatteryStatus, EstimatedRunTime, Name FROM Win32_Battery");
            foreach (ManagementObject mo in searcher.Get())
            {
                int charge = ToInt(mo["EstimatedChargeRemaining"]);
                int status = ToInt(mo["BatteryStatus"]);
                int runtime = ToInt(mo["EstimatedRunTime"]);
                string name = mo["Name"]?.ToString() ?? "";

                // BatteryStatus 2 == AC (plugged); 6/7/8/9 == charging variants; 11 == partially charged on AC.
                bool onAc = status is 2 or 6 or 7 or 8 or 9 or 11;
                bool charging = status is 6 or 7 or 8 or 9;

                return new BatterySnapshot
                {
                    Present = true,
                    ChargePercent = Math.Clamp(charge, 0, 100),
                    StatusCode = status,
                    OnAc = onAc,
                    Charging = charging,
                    RuntimeMinutes = runtime is > 0 and < 71582788 ? runtime : -1,
                    DeviceName = name,
                };
            }
        }
        catch { }
        return new BatterySnapshot { Present = false };
    }

    private static int ToInt(object? o)
    {
        try { return o is null ? 0 : Convert.ToInt32(o, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    // ---------------------------------------------------- battery health report ----
    /// <summary>Generate &amp; parse a powercfg /batteryreport into design-vs-full wear %.</summary>
    public static async Task<BatteryReport> GenerateHealthReportAsync(CancellationToken ct = default)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), $"winforge-batteryreport-{Guid.NewGuid():N}.html");
            var res = await ShellRunner.Run("powercfg.exe", $"/batteryreport /output \"{path}\"", elevated: false, ct);

            if (!File.Exists(path))
                return new BatteryReport
                {
                    HasData = false,
                    Error = res.Output is { Length: > 0 } ? res.Output
                        : "powercfg could not produce a battery report (no battery?).",
                };

            string html = await File.ReadAllTextAsync(path, ct);
            var report = ParseBatteryReport(html);
            return report with { ReportPath = path };
        }
        catch (Exception ex)
        {
            return new BatteryReport { HasData = false, Error = ex.Message };
        }
    }

    /// <summary>Parse the HTML battery report. Capacities appear as "41,440 mWh" rows.</summary>
    public static BatteryReport ParseBatteryReport(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new BatteryReport { HasData = false, Error = "Empty report." };

        long design = ExtractCapacity(html, "DESIGN CAPACITY");
        long full = ExtractCapacity(html, "FULL CHARGE CAPACITY");
        int cycles = ExtractCycleCount(html);
        string? name = ExtractField(html, "NAME");
        string? maker = ExtractField(html, "MANUFACTURER");
        string? chem = ExtractField(html, "CHEMISTRY");

        if (design <= 0 && full <= 0)
            return new BatteryReport
            {
                HasData = false,
                Error = "No battery capacity data found in report (desktop or no battery).",
            };

        double wear = (design > 0 && full > 0 && full <= design)
            ? Math.Round((1.0 - (double)full / design) * 100.0, 1)
            : 0;

        return new BatteryReport
        {
            HasData = true,
            DeviceName = name,
            Manufacturer = maker,
            Chemistry = chem,
            DesignCapacityMwh = design,
            FullChargeCapacityMwh = full,
            CycleCount = cycles,
            WearPercent = wear,
        };
    }

    // Capacity rows look like: <td class="label">DESIGN CAPACITY</td><td>41,440 mWh</td>
    private static long ExtractCapacity(string html, string label)
    {
        var m = Regex.Match(html,
            Regex.Escape(label) + @"\s*</td>\s*<td[^>]*>\s*([\d.,\s]+?)\s*mWh",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return 0;
        var digits = new string(m.Groups[1].Value.Where(char.IsDigit).ToArray());
        return long.TryParse(digits, out var v) ? v : 0;
    }

    private static int ExtractCycleCount(string html)
    {
        var m = Regex.Match(html,
            @"CYCLE COUNT\s*</td>\s*<td[^>]*>\s*([\d,\s]+?)\s*</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return 0;
        var digits = new string(m.Groups[1].Value.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var v) ? v : 0;
    }

    private static string? ExtractField(string html, string label)
    {
        var m = Regex.Match(html,
            @">\s*" + Regex.Escape(label) + @"\s*</td>\s*<td[^>]*>\s*(.*?)\s*</td>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!m.Success) return null;
        var v = Regex.Replace(m.Groups[1].Value, "<.*?>", "").Trim();
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    // -------------------------------------------------- powercfg /energy warnings ----
    /// <summary>Run powercfg /energy and pull out the Errors/Warnings count + report path.</summary>
    public static async Task<(int errors, int warnings, string? path, string raw)> RunEnergyReportAsync(CancellationToken ct = default)
    {
        try
        {
            string path = Path.Combine(Path.GetTempPath(), $"winforge-energy-{Guid.NewGuid():N}.html");
            // /energy traces system behaviour; a short 10s window keeps the UI responsive.
            var res = await ShellRunner.Run("powercfg.exe", $"/energy /output \"{path}\" /duration 10", elevated: false, ct);
            string raw = res.Output ?? "";

            int errors = ExtractCount(raw, "Errors");
            int warnings = ExtractCount(raw, "Warnings");
            return (errors, warnings, File.Exists(path) ? path : null, raw);
        }
        catch (Exception ex)
        {
            return (-1, -1, null, ex.Message);
        }
    }

    private static int ExtractCount(string text, string label)
    {
        var m = Regex.Match(text, label + @"\s*:?\s*(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : -1;
    }

    // ----------------------------------------------------------- thermal sensors ----
    private LibreHardwareMonitorLease? _sensorLease;
    private readonly object _gate = new();
    private bool _useFallback;

    /// <summary>
    /// Acquire the shared LibreHardwareMonitor computer for CPU/GPU/motherboard sensors. A page gets
    /// one lease; the exact Computer opened by WinForge closes only after every monitoring page has
    /// released its lease.
    /// </summary>
    public bool OpenSensors()
    {
        lock (_gate)
        {
            if (_sensorLease is not null) return !_useFallback;
            _useFallback = true;

            var lease = LibreHardwareMonitorLifecycle.Acquire();
            if (lease is null) return false;

            try
            {
                _sensorLease = lease;
                // Probe: if no temperature sensors are visible (no admin driver), use WMI fallback.
                _useFallback = !HasAnyTemperature(lease);
                if (_useFallback)
                {
                    // Keeping a driver-backed computer open cannot help the WMI fallback. Release it
                    // immediately, unless System Monitor still has its own shared lease.
                    _sensorLease = null;
                    lease.Dispose();
                }
                return !_useFallback;
            }
            catch
            {
                _sensorLease = null;
                try { lease.Dispose(); } catch { }
                _useFallback = true;
                return false;
            }
        }
    }

    private static bool HasAnyTemperature(LibreHardwareMonitorLease lease)
    {
        bool found = false;
        bool sampled = lease.TryUse(computer =>
        {
            foreach (var hw in computer.Hardware)
            {
                hw.Update();
                foreach (var s in hw.Sensors)
                {
                    if (s.SensorType != LHM.SensorType.Temperature || s.Value is null or <= 0) continue;
                    found = true;
                    return;
                }
            }
        });
        return sampled && found;
    }

    /// <summary>True when no driver-backed sensors were found and we are on the WMI thermal-zone fallback.</summary>
    public bool UsingFallback => _useFallback;

    /// <summary>Sample all temperature/fan/load sensors. Empty when only the WMI fallback applies.</summary>
    public List<SensorReading> ReadSensors()
    {
        var list = new List<SensorReading>();
        lock (_gate)
        {
            var lease = _sensorLease;
            if (lease is null) return list;
            try
            {
                lease.TryUse(computer =>
                {
                    foreach (var hw in computer.Hardware)
                    {
                        hw.Update();
                        foreach (var sub in hw.SubHardware) sub.Update();
                        Collect(hw, list);
                        foreach (var sub in hw.SubHardware) Collect(sub, list);
                    }
                });
            }
            catch { }
        }
        return list;
    }

    private static void Collect(LHM.IHardware hw, List<SensorReading> list)
    {
        foreach (var s in hw.Sensors)
        {
            if (s.SensorType is not (LHM.SensorType.Temperature
                or LHM.SensorType.Fan
                or LHM.SensorType.Load))
                continue;
            if (s.Value is null) continue;
            list.Add(new SensorReading
            {
                Hardware = hw.Name,
                Name = s.Name,
                Type = s.SensorType,
                Value = s.Value.Value,
            });
        }
    }

    /// <summary>
    /// Coarse thermal-zone temperatures (°C) via MSAcpi_ThermalZoneTemperature — works without an
    /// admin driver but is reported in tenths of a Kelvin. Used as a fallback when no real sensors exist.
    /// </summary>
    public static List<double> ReadThermalZonesCelsius()
    {
        var temps = new List<double>();
        try
        {
            var scope = new ManagementScope(@"\\.\root\WMI");
            var query = new ObjectQuery("SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
            using var searcher = new ManagementObjectSearcher(scope, query);
            foreach (ManagementObject mo in searcher.Get())
            {
                int tenthsK = ToInt(mo["CurrentTemperature"]);
                if (tenthsK <= 0) continue;
                double c = tenthsK / 10.0 - 273.15;
                if (c is > -20 and < 150) temps.Add(Math.Round(c, 1));
            }
        }
        catch { }
        return temps;
    }

    public void Dispose()
    {
        LibreHardwareMonitorLease? lease;
        lock (_gate)
        {
            lease = _sensorLease;
            _sensorLease = null;
            _useFallback = true;
        }
        try { lease?.Dispose(); } catch { }
    }
}
