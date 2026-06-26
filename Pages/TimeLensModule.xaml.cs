using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 活動時間軸（TimeLens）· Activity Timeline. Passively records which app/window is in the foreground
/// over the day and shows a 24-hour stacked timeline plus sorted per-app totals — all local, on-device.
/// Tracking is OFF by default; idle periods are excluded; data can be exported to CSV (FileDialogs, never
/// WinRT pickers) or cleared. Fully bilingual (English + 粵語).
/// </summary>
public sealed partial class TimeLensModule : Page
{
    private DateOnly _day = DateOnly.FromDateTime(DateTime.Now);
    private bool _loadingPrefs;

    public TimeLensModule()
    {
        InitializeComponent();
        IdleSlider.ValueChanged += Idle_Changed;
        PollSlider.ValueChanged += Poll_Changed;
        Loc.I.LanguageChanged += OnLang;
        ActivityTrackerService.I.StateChanged += OnTrackerState;
        ActivityTrackerService.I.SegmentRecorded += OnSegment;
        Loaded += (_, _) =>
        {
            LoadPrefsIntoControls();
            Render();
            SyncControls();
            DatePicker.Date = new DateTimeOffset(_day.ToDateTime(TimeOnly.MinValue));
            LoadDay();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            ActivityTrackerService.I.StateChanged -= OnTrackerState;
            ActivityTrackerService.I.SegmentRecorded -= OnSegment;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? s, EventArgs e) { Render(); SyncControls(); LoadDay(); }

    private void OnTrackerState(object? s, EventArgs e)
        => DispatcherQueue.TryEnqueue(SyncControls);

    private void OnSegment(object? s, EventArgs e)
        => DispatcherQueue.TryEnqueue(() => { if (_day == DateOnly.FromDateTime(DateTime.Now)) LoadDay(); });

    // ===== static text =====

    private void Render()
    {
        HeaderTitle.Text = "Activity Timeline · 活動時間軸";
        HeaderBlurb.Text = P(
            "See where your time goes. WinForge can quietly record which app is in the foreground while it's running, then show a readable timeline of your day and a sorted list of per-app totals.",
            "睇下你嘅時間用咗喺邊。WinForge 開住嘅時候，可以靜靜哋記錄前景係邊個 app，然後整理成一條易睇嘅當日時間軸同埋每個 app 嘅總時間排行。");

        PrivacyBar.Title = P("Local-only & private", "純本機・私隱優先");
        PrivacyBar.Message = P(
            "Tracking is off until you turn it on. Window titles can contain sensitive text, so everything stays on this device (under %LOCALAPPDATA%\\WinForge\\activity) and never leaves it. There is no cloud sync.",
            "未開之前都唔會追蹤。視窗標題有機會含敏感字眼，所以所有資料都只係留喺呢部機（喺 %LOCALAPPDATA%\\WinForge\\activity），唔會傳出去，亦冇雲端同步。");

        TrackingLabel.Text = P("Activity tracking", "活動追蹤");
        TimelineTitle.Text = P("Timeline (24 hours)", "時間軸（24 小時）");
        TotalsTitle.Text = P("Per-app totals", "每個 app 總時間");
        RefreshText.Text = P("Refresh", "重新整理");
        ExportText.Text = P("Export CSV…", "匯出 CSV…");
        ClearText.Text = P("Clear…", "清除…");
        TodayBtn.Content = P("Today", "今日");
        IdleLabel.Text = P("Pause logging after idle (minutes)", "閒置幾分鐘後停止記錄");
        PollLabel.Text = P("Sample every (seconds)", "每隔幾秒取樣");
        EmptyText.Text = P("No activity recorded for this day yet. Turn tracking on and use your PC — segments will appear here.",
            "呢日仲未有任何活動記錄。開咗追蹤再用部電腦，時段就會喺度顯示。");
    }

    private void LoadPrefsIntoControls()
    {
        _loadingPrefs = true;
        IdleSlider.Value = ActivityTrackerService.I.IdleMinutes;
        PollSlider.Value = ActivityTrackerService.I.PollSeconds;
        _loadingPrefs = false;
        IdleValue.Text = ((int)IdleSlider.Value).ToString();
        PollValue.Text = ((int)PollSlider.Value).ToString();
    }

    private void SyncControls()
    {
        var t = ActivityTrackerService.I;
        TrackToggle.Toggled -= Track_Toggled;
        TrackToggle.IsOn = t.IsTracking;
        TrackToggle.OnContent = P("On", "開");
        TrackToggle.OffContent = P("Off", "關");
        TrackToggle.Toggled += Track_Toggled;

        PauseBtn.IsEnabled = t.IsTracking;
        if (t.IsPaused)
        {
            PauseText.Text = P("Resume", "繼續");
            PauseIcon.Glyph = ""; // play
        }
        else
        {
            PauseText.Text = P("Pause", "暫停");
            PauseIcon.Glyph = ""; // pause
        }

        StatusText.Text = !t.IsTracking
            ? P("Not tracking. Toggle on to start recording foreground apps.", "未追蹤。撳開就開始記錄前景 app。")
            : t.IsPaused
                ? P("Tracking paused — nothing is being recorded.", "已暫停追蹤 — 而家冇任何記錄。")
                : t.IsIdle
                    ? P("Idle — logging paused until you return.", "閒置中 — 暫停記錄直至你返嚟。")
                    : P("Tracking… recording the foreground app.", "追蹤緊… 正在記錄前景 app。");
    }

    // ===== control handlers =====

    private void Track_Toggled(object sender, RoutedEventArgs e)
    {
        if (TrackToggle.IsOn) ActivityTrackerService.I.Start();
        else ActivityTrackerService.I.Stop();
        SyncControls();
        LoadDay();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        var t = ActivityTrackerService.I;
        t.SetPaused(!t.IsPaused);
        SyncControls();
    }

    private void Idle_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loadingPrefs) return;
        ActivityTrackerService.I.IdleMinutes = (int)e.NewValue;
        IdleValue.Text = ((int)e.NewValue).ToString();
    }

    private void Poll_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_loadingPrefs) return;
        ActivityTrackerService.I.PollSeconds = (int)e.NewValue;
        PollValue.Text = ((int)e.NewValue).ToString();
    }

    private void Date_Changed(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (args.NewDate is DateTimeOffset dto)
        {
            _day = DateOnly.FromDateTime(dto.LocalDateTime);
            LoadDay();
        }
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        _day = DateOnly.FromDateTime(DateTime.Now);
        DatePicker.Date = new DateTimeOffset(_day.ToDateTime(TimeOnly.MinValue));
        LoadDay();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadDay();

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var segs = ActivityStore.Load(_day);
        if (segs.Count == 0)
        {
            Result(InfoBarSeverity.Warning, P("Nothing to export", "冇資料可匯出"),
                P("There is no activity recorded for this day.", "呢日冇任何活動記錄。"));
            return;
        }
        var path = await FileDialogs.SaveFileAsync($"WinForge-activity-{_day:yyyy-MM-dd}", ".csv");
        if (path is null) return;
        try
        {
            File.WriteAllText(path, ActivityStore.ToCsv(segs), new System.Text.UTF8Encoding(true));
            Result(InfoBarSeverity.Success, P("Exported", "已匯出"), path);
        }
        catch (Exception ex)
        {
            Result(InfoBarSeverity.Error, P("Export failed", "匯出失敗"), ex.Message);
        }
    }

    private async void Clear_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Clear activity history?", "清除活動記錄？"),
            Content = P("This permanently deletes recorded activity. Tip: Export to CSV first if you want a copy. Clear just this day, or all recorded days?",
                "呢個動作會永久刪除已記錄嘅活動。提示：想留底就先匯出 CSV。淨係清呢日，定係清晒全部日子？"),
            PrimaryButtonText = P("Clear this day", "清呢日"),
            SecondaryButtonText = P("Clear ALL days", "清晒全部"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        var r = await dlg.ShowAsync();
        if (r == ContentDialogResult.Primary)
        {
            ActivityStore.ClearDay(_day);
            Result(InfoBarSeverity.Success, P("Cleared", "已清除"), P("This day's activity was removed.", "呢日嘅活動已刪除。"));
            LoadDay();
        }
        else if (r == ContentDialogResult.Secondary)
        {
            int n = ActivityStore.ClearAll();
            Result(InfoBarSeverity.Success, P("Cleared", "已清除"),
                P($"Removed {n} day(s) of activity.", $"已刪除 {n} 日嘅活動。"));
            LoadDay();
        }
    }

    // ===== rendering =====

    private void LoadDay()
    {
        var segs = ActivityStore.Load(_day);
        double total = segs.Sum(s => s.Seconds);

        bool isToday = _day == DateOnly.FromDateTime(DateTime.Now);
        string dayLabel = isToday ? P("Today", "今日") : _day.ToString("yyyy-MM-dd");
        SummaryText.Text = segs.Count == 0
            ? P($"{dayLabel}: no activity recorded.", $"{dayLabel}：冇活動記錄。")
            : P($"{dayLabel}: {FmtDuration(total)} tracked across {segs.Count} segment(s).",
                $"{dayLabel}：共追蹤 {FmtDuration(total)}，分 {segs.Count} 段。");

        bool empty = segs.Count == 0;
        EmptyCard.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        TimelineCard.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        TotalsCard.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
        if (empty) { TimelineHost.Children.Clear(); TotalsHost.Children.Clear(); return; }

        // Stable colour per process across both views.
        var totals = ActivityStore.TotalsByProcess(segs);
        var colorMap = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        foreach (var (proc, _) in totals) colorMap[proc] = ColorFor(proc);

        BuildTimeline(segs, colorMap);
        BuildTotals(totals, total, colorMap);
    }

    private void BuildTimeline(List<ActivitySegment> segs, Dictionary<string, Color> colorMap)
    {
        TimelineHost.Children.Clear();
        DateTime dayStart = _day.ToDateTime(TimeOnly.MinValue);

        for (int hour = 0; hour < 24; hour++)
        {
            DateTime hStart = dayStart.AddHours(hour);
            DateTime hEnd = hStart.AddHours(1);

            var row = new Grid { Height = 22, Margin = new Thickness(0, 0, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(54) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var label = new TextBlock
            {
                Text = $"{hour:00}:00",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var track = new Grid
            {
                Height = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(3),
            };
            var canvas = new Canvas { Height = 18 };
            track.Children.Add(canvas);
            Grid.SetColumn(track, 1);
            row.Children.Add(track);

            // For each segment overlapping this hour, draw a proportional rectangle.
            double hourSecs = 3600.0;
            foreach (var s in segs)
            {
                var segStart = s.Start;
                var segEnd = s.End;
                if (segEnd <= hStart || segStart >= hEnd) continue;
                var clipStart = segStart < hStart ? hStart : segStart;
                var clipEnd = segEnd > hEnd ? hEnd : segEnd;
                double offFrac = (clipStart - hStart).TotalSeconds / hourSecs;
                double widFrac = (clipEnd - clipStart).TotalSeconds / hourSecs;
                if (widFrac <= 0) continue;

                var rect = new Microsoft.UI.Xaml.Shapes.Rectangle
                {
                    Height = 18,
                    RadiusX = 2,
                    RadiusY = 2,
                    Fill = new SolidColorBrush(colorMap.TryGetValue(s.Process, out var c) ? c : ColorFor(s.Process)),
                };
                ToolTipService.SetToolTip(rect,
                    $"{Display(s.Process)} · {s.Start:HH:mm}–{s.End:HH:mm} ({FmtDuration(s.Seconds)})"
                    + (string.IsNullOrWhiteSpace(s.Title) ? "" : $"\n{s.Title}"));

                // Width resolves once the track has a measured width; bind via SizeChanged.
                void Place()
                {
                    double w = track.ActualWidth;
                    if (w <= 0) return;
                    rect.Width = Math.Max(1, widFrac * w);
                    Canvas.SetLeft(rect, offFrac * w);
                }
                track.SizeChanged += (_, _) => Place();
                canvas.Children.Add(rect);
                Place();
            }

            TimelineHost.Children.Add(row);
        }
    }

    private void BuildTotals(List<(string Process, double Seconds)> totals, double grand, Dictionary<string, Color> colorMap)
    {
        TotalsHost.Children.Clear();
        double max = totals.Count > 0 ? totals.Max(t => t.Seconds) : 1;

        foreach (var (proc, secs) in totals)
        {
            var item = new StackPanel { Spacing = 4 };

            var head = new Grid();
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            name.Children.Add(new Border
            {
                Width = 12, Height = 12, CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(colorMap.TryGetValue(proc, out var c) ? c : ColorFor(proc)),
                VerticalAlignment = VerticalAlignment.Center,
            });
            name.Children.Add(new TextBlock { Text = Display(proc), VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(name, 0);
            head.Children.Add(name);

            double pct = grand > 0 ? secs / grand * 100 : 0;
            var amount = new TextBlock
            {
                Text = $"{FmtDuration(secs)}  ·  {pct:0}%",
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(amount, 1);
            head.Children.Add(amount);
            item.Children.Add(head);

            // Proportional bar (relative to the busiest app).
            var barBg = new Grid
            {
                Height = 8,
                Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(4),
            };
            var bar = new Border
            {
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(colorMap.TryGetValue(proc, out var c2) ? c2 : ColorFor(proc)),
            };
            double frac = max > 0 ? secs / max : 0;
            void PlaceBar()
            {
                double w = barBg.ActualWidth;
                if (w > 0) bar.Width = Math.Max(3, frac * w);
            }
            barBg.SizeChanged += (_, _) => PlaceBar();
            barBg.Children.Add(bar);
            PlaceBar();
            item.Children.Add(barBg);

            TotalsHost.Children.Add(item);
        }
    }

    // ===== helpers =====

    private void Result(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private static string Display(string proc)
        => string.IsNullOrWhiteSpace(proc) ? "Unknown · 未知" : proc;

    private string FmtDuration(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Round(seconds));
        if (ts.TotalHours >= 1) return P($"{(int)ts.TotalHours}h {ts.Minutes}m", $"{(int)ts.TotalHours}小時 {ts.Minutes}分");
        if (ts.TotalMinutes >= 1) return P($"{ts.Minutes}m {ts.Seconds}s", $"{ts.Minutes}分 {ts.Seconds}秒");
        return P($"{ts.Seconds}s", $"{ts.Seconds}秒");
    }

    /// <summary>由 process 名穩定推導一個顏色 · Deterministic pleasant colour from a process name.</summary>
    private static Color ColorFor(string key)
    {
        key ??= "";
        int hash = 17;
        foreach (var ch in key.ToLowerInvariant()) hash = hash * 31 + ch;
        double hue = (uint)hash % 360;
        return HslToRgb(hue, 0.55, 0.55);
    }

    private static Color HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs((h / 60.0) % 2 - 1));
        double m = l - c / 2;
        double r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; }
        else if (h < 120) { r = x; g = c; }
        else if (h < 180) { g = c; b = x; }
        else if (h < 240) { g = x; b = c; }
        else if (h < 300) { r = x; b = c; }
        else { r = c; b = x; }
        return Color.FromArgb(255,
            (byte)Math.Round((r + m) * 255),
            (byte)Math.Round((g + m) * 255),
            (byte)Math.Round((b + m) * 255));
    }
}
