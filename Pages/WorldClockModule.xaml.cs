using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 世界時鐘同時區轉換 · World-clock &amp; time-zone converter. A live (1 s) list of chosen zones plus a
/// converter that projects one instant into every listed zone. Pure <see cref="TimeZoneInfo"/>; every
/// lookup is guarded so it never throws. Bilingual (English + 粵語). No redirect.
/// </summary>
public sealed partial class WorldClockModule : Page
{
    /// <summary>One live clock row. Properties raise change notifications so the ListView updates in place.</summary>
    public sealed class ClockRow : INotifyPropertyChanged
    {
        public string ZoneId { get; }
        public TimeZoneInfo? Zone { get; }

        private string _name = "", _time = "", _offset = "", _day = "";
        public string Name { get => _name; set { _name = value; Raise(nameof(Name)); } }
        public string Time { get => _time; set { _time = value; Raise(nameof(Time)); } }
        public string Offset { get => _offset; set { _offset = value; Raise(nameof(Offset)); } }
        public string Day { get => _day; set { _day = value; Raise(nameof(Day)); } }

        public ClockRow(string zoneId, TimeZoneInfo? zone) { ZoneId = zoneId; Zone = zone; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>A single converter result row (static snapshot, no notifications needed).</summary>
    public sealed class ConvertRow
    {
        public string Name { get; set; } = "";
        public string Time { get; set; } = "";
        public string Day { get; set; } = "";
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<ClockRow> _rows = new();
    private readonly ObservableCollection<ConvertRow> _conv = new();

    public WorldClockModule()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => Tick();
        ClockList.ItemsSource = _rows;
        ConvList.ItemsSource = _conv;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; _timer.Stop(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? sender, EventArgs e) => Render();

    private void Render()
    {
        try
        {
            Header.Title = "World Clock · 世界時鐘";
            HeaderBlurb.Text = P("Track the current time across cities and convert any instant between time zones — all with pure Windows time-zone data.",
                "睇實各地城市而家幾點，仲可以將任何一個時間喺唔同時區之間轉換 — 全部用返 Windows 內置嘅時區資料。");
            AddTitle.Text = P("Add a time zone", "加一個時區");
            AddBtn.Content = P("Add", "加入");
            RemoveBtn.Content = P("Remove selected", "移除已選");
            ConvTitle.Text = P("Convert a time", "轉換時間");
            ConvBtn.Content = P("Convert", "轉換");
            ConvHint.Text = P("Enter a date/time (e.g. 2026-07-01 14:30), pick its zone, then convert to see it everywhere below. Blank uses now.",
                "輸入日期時間（例如 2026-07-01 14:30），揀返佢屬邊個時區，然後轉換就會喺下面顯示各地時間。留空即係用而家。");

            if (_rows.Count == 0) SeedZones();
            PopulateZonePickers();
            RefreshRows();
            SetStatus();
        }
        catch { /* never throw from UI */ }
    }

    private void SeedZones()
    {
        try
        {
            AddRow("Local", WorldClockService.Local()?.Id ?? "UTC", WorldClockService.Local());
            foreach (var id in WorldClockService.SeedZoneIds)
            {
                var z = WorldClockService.Resolve(id);
                if (z != null) AddRowUnique(id, z);
            }
        }
        catch { }
    }

    private void AddRow(string _, string id, TimeZoneInfo? zone) => AddRowUnique(id, zone);

    private void AddRowUnique(string id, TimeZoneInfo? zone)
    {
        try
        {
            foreach (var r in _rows) if (string.Equals(r.ZoneId, id, StringComparison.OrdinalIgnoreCase)) return;
            _rows.Add(new ClockRow(id, zone));
        }
        catch { }
    }

    private void PopulateZonePickers()
    {
        try
        {
            if (AddZoneBox.Items.Count == 0)
            {
                var all = WorldClockService.AllZones();
                foreach (var z in all) { AddZoneBox.Items.Add(z); ConvZoneBox.Items.Add(z); }
                if (ConvZoneBox.SelectedIndex < 0 && ConvZoneBox.Items.Count > 0)
                {
                    var local = WorldClockService.Local();
                    int idx = 0;
                    for (int i = 0; i < ConvZoneBox.Items.Count; i++)
                        if (ConvZoneBox.Items[i] is TimeZoneInfo tz && local != null && tz.Id == local.Id) { idx = i; break; }
                    ConvZoneBox.SelectedIndex = idx;
                }
            }
        }
        catch { }
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (AddZoneBox.SelectedItem is TimeZoneInfo z) { AddRowUnique(z.Id, z); RefreshRows(); SetStatus(); }
        }
        catch { }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ClockList.SelectedItem is ClockRow r) { _rows.Remove(r); SetStatus(); }
        }
        catch { }
    }

    private void Convert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var src = ConvZoneBox.SelectedItem as TimeZoneInfo ?? WorldClockService.Local();
            string text = ConvTimeBox.Text?.Trim() ?? "";
            DateTime local;
            if (string.IsNullOrEmpty(text))
                local = WorldClockService.InZone(DateTime.UtcNow, src);
            else if (!DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out local)
                  && !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
            {
                ConvHint.Text = P("Could not read that time. Try a format like 2026-07-01 14:30.",
                    "睇唔明呢個時間。試下 2026-07-01 14:30 呢種格式。");
                return;
            }

            // Interpret the parsed wall-clock time as being in the source zone, get the UTC instant.
            DateTime utc;
            try
            {
                var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
                utc = src != null ? TimeZoneInfo.ConvertTimeToUtc(unspecified, src) : DateTime.SpecifyKind(local, DateTimeKind.Utc);
            }
            catch { utc = DateTime.SpecifyKind(local, DateTimeKind.Utc); }

            _conv.Clear();
            foreach (var r in _rows)
            {
                var t = WorldClockService.InZone(utc, r.Zone);
                var off = WorldClockService.OffsetAt(utc, r.Zone);
                _conv.Add(new ConvertRow
                {
                    Name = ZoneLabel(r.Zone, r.ZoneId) + "  ·  " + WorldClockService.FormatOffset(off),
                    Time = t.ToString("HH:mm"),
                    Day = t.ToString("ddd, dd MMM"),
                });
            }
            ConvHint.Text = P($"Showing {ConvTimeBox.Text} interpreted in the source zone across {_conv.Count} listed zone(s).",
                $"以來源時區解讀 {ConvTimeBox.Text}，喺 {_conv.Count} 個列出嘅時區顯示。");
        }
        catch { }
    }

    private void Tick()
    {
        try { RefreshRows(); } catch { }
    }

    private void RefreshRows()
    {
        DateTime utc = DateTime.UtcNow;
        foreach (var r in _rows)
        {
            var t = WorldClockService.InZone(utc, r.Zone);
            var off = WorldClockService.OffsetAt(utc, r.Zone);
            r.Name = ZoneLabel(r.Zone, r.ZoneId);
            r.Time = t.ToString("HH:mm:ss");
            r.Offset = WorldClockService.FormatOffset(off);
            r.Day = t.ToString("dddd, dd MMM");
        }
        if (!_timer.IsEnabled) _timer.Start();
    }

    private static string ZoneLabel(TimeZoneInfo? zone, string fallbackId)
    {
        if (zone == null) return fallbackId;
        try { return string.IsNullOrWhiteSpace(zone.DisplayName) ? zone.Id : zone.DisplayName; }
        catch { return fallbackId; }
    }

    private void SetStatus()
    {
        StatusText.Text = P($"{_rows.Count} zone(s) tracked · updating live.", $"追蹤緊 {_rows.Count} 個時區 · 即時更新。");
    }
}
