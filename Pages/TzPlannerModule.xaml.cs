using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI;                     // Colors
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;                        // Color  (Microsoft.UI only exposes Colors, not Color)
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 時區會議規劃 · Timezone meeting planner — pick a reference zone + date/time, add participant zones,
/// and see each one's local time colour-coded against working hours. Pure managed TimeZoneInfo. Bilingual.
/// </summary>
public sealed partial class TzPlannerModule : Page
{
    /// <summary>One participant row bound via classic {Binding} (needs a Brush property).</summary>
    public sealed class ZoneRow
    {
        public string ZoneId { get; set; } = "";
        public string ZoneName { get; set; } = "";
        public string LocalTime { get; set; } = "";
        public string OffsetAndState { get; set; } = "";
        public Brush AccentBrush { get; set; } = new SolidColorBrush(Colors.Gray);
        public Brush RowBrush { get; set; } = new SolidColorBrush(Colors.Transparent);
    }

    private readonly ObservableCollection<ZoneRow> _rows = new();
    private readonly List<string> _zoneIds = new();     // participant zone ids, in order
    private IReadOnlyList<TimeZoneInfo> _all = Array.Empty<TimeZoneInfo>();
    private bool _suppress;

    public TzPlannerModule()
    {
        InitializeComponent();
        RowsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _all = TzPlannerService.AllZones();
        PopulateCombos();
        var now = DateTimeOffset.Now;
        _suppress = true;
        RefDate.Date = now;
        RefTime.Time = now.TimeOfDay;
        _suppress = false;
        Render();
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void PopulateCombos()
    {
        _suppress = true;
        try
        {
            RefZoneCombo.Items.Clear();
            AddZoneCombo.Items.Clear();
            foreach (var z in _all)
            {
                RefZoneCombo.Items.Add(new ComboBoxItem { Content = z.DisplayName, Tag = z.Id });
                AddZoneCombo.Items.Add(new ComboBoxItem { Content = z.DisplayName, Tag = z.Id });
            }
            // Default the reference zone to the local zone if present.
            string localId = TimeZoneInfo.Local?.Id ?? "";
            int idx = _all.ToList().FindIndex(z => z.Id == localId);
            RefZoneCombo.SelectedIndex = idx >= 0 ? idx : (_all.Count > 0 ? 0 : -1);
            if (AddZoneCombo.Items.Count > 0) AddZoneCombo.SelectedIndex = 0;
        }
        catch { /* never throw from UI setup */ }
        finally { _suppress = false; }
    }

    private void Render()
    {
        Header.Title = "Timezone Planner · 時區會議規劃";
        HeaderBlurb.Text = P("Plan a meeting across time zones. Pick a reference zone and moment, add each participant's zone, and see who's inside working hours.",
            "跨時區安排會議。揀一個參考時區同時間，加入每位參加者嘅時區，即刻睇到邊個喺辦公時間內。");
        RefTitle.Text = P("Reference moment", "參考時間");
        RefZoneLabel.Text = P("Reference time zone", "參考時區");
        RefDateLabel.Text = P("Date", "日期");
        RefTimeLabel.Text = P("Time", "時間");
        StartLabel.Text = P("Work start (hour)", "上班（小時）");
        EndLabel.Text = P("Work end (hour)", "下班（小時）");
        AddTitle.Text = P("Add a participant zone", "加入參加者時區");
        AddButton.Content = P("Add", "加入");
        EmptyText.Text = P("No participant zones yet — add one above.", "仲未有參加者時區 — 喺上面加入一個。");
    }

    private DateTime ReferenceLocal()
    {
        try
        {
            var d = RefDate.Date?.Date ?? DateTime.Today;
            var t = RefTime.Time;
            return d.Date + t;
        }
        catch { return DateTime.Now; }
    }

    private (int start, int end) WorkHours()
    {
        int s = (int)(double.IsNaN(StartBox.Value) ? 9 : StartBox.Value);
        int e = (int)(double.IsNaN(EndBox.Value) ? 17 : EndBox.Value);
        return (s, e);
    }

    private void Recompute()
    {
        try
        {
            _rows.Clear();
            var refZone = TzPlannerService.FindZone((RefZoneCombo.SelectedItem as ComboBoxItem)?.Tag as string)
                          ?? TimeZoneInfo.Local;
            var refLocal = ReferenceLocal();
            var (startH, endH) = WorkHours();

            foreach (var id in _zoneIds)
            {
                var zone = TzPlannerService.FindZone(id);
                if (zone is null) continue;
                var local = TzPlannerService.LocalTimeIn(refLocal, refZone, zone);
                var offset = TzPlannerService.OffsetAt(local, zone);
                var state = TzPlannerService.Classify(local, startH, endH);

                _rows.Add(new ZoneRow
                {
                    ZoneId = id,
                    ZoneName = zone.DisplayName,
                    LocalTime = local.ToString("ddd dd MMM  HH:mm"),
                    OffsetAndState = $"{TzPlannerService.FormatOffset(offset)}  ·  {StateLabel(state)}",
                    AccentBrush = new SolidColorBrush(AccentColor(state)),
                    RowBrush = new SolidColorBrush(RowColor(state)),
                });
            }

            EmptyText.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { /* status only; never throw */ }
    }

    private string StateLabel(TzPlannerService.HoursState s) => s switch
    {
        TzPlannerService.HoursState.InHours => P("Working hours", "辦公時間"),
        TzPlannerService.HoursState.EdgeHours => P("Early / late", "太早或太夜"),
        _ => P("Night", "深夜"),
    };

    private static Color AccentColor(TzPlannerService.HoursState s) => s switch
    {
        TzPlannerService.HoursState.InHours => Color.FromArgb(0xFF, 0x2E, 0xA0, 0x43),   // green
        TzPlannerService.HoursState.EdgeHours => Color.FromArgb(0xFF, 0xD9, 0x8A, 0x00), // amber
        _ => Color.FromArgb(0xFF, 0xC4, 0x2B, 0x1C),                                     // red
    };

    private static Color RowColor(TzPlannerService.HoursState s) => s switch
    {
        TzPlannerService.HoursState.InHours => Color.FromArgb(0x22, 0x2E, 0xA0, 0x43),
        TzPlannerService.HoursState.EdgeHours => Color.FromArgb(0x22, 0xD9, 0x8A, 0x00),
        _ => Color.FromArgb(0x22, 0xC4, 0x2B, 0x1C),
    };

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var id = (AddZoneCombo.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrWhiteSpace(id) || _zoneIds.Contains(id)) return;
            _zoneIds.Add(id);
            Recompute();
        }
        catch { }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as FrameworkElement)?.Tag is string id)
            {
                _zoneIds.Remove(id);
                Recompute();
            }
        }
        catch { }
    }

    private void Ref_Changed(object sender, SelectionChangedEventArgs e) { if (!_suppress) Recompute(); }
    private void Ref_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args) { if (!_suppress) Recompute(); }
    private void Ref_TimeChanged(object sender, TimePickerValueChangedEventArgs e) { if (!_suppress) Recompute(); }
    private void Hours_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppress) Recompute(); }
}
