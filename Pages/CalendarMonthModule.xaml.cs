using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI; // Windows.UI.Color for day-cell colouring under Microsoft.UI namespaces
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 月曆 · Month-calendar viewer — a weekday-header + up-to-6-week grid with ISO week-number column,
/// leading/trailing adjacent-month days dimmed, today highlighted, prev/next/today navigation and a
/// "week starts on" selector. Clicking a day shows full-date facts. Pure managed; never throws.
/// </summary>
public sealed partial class CalendarMonthModule : Page
{
    private DateTime _display = DateTime.Today;      // any day within the shown month
    private DayOfWeek _firstDay = DayOfWeek.Monday;
    private DateTime? _selected;
    private bool _suppress;

    public CalendarMonthModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _suppress = true;
            if (WeekStartCombo.Items.Count == 0)
            {
                WeekStartCombo.Items.Add(P("Monday", "星期一"));
                WeekStartCombo.Items.Add(P("Sunday", "星期日"));
            }
            WeekStartCombo.SelectedIndex = _firstDay == DayOfWeek.Sunday ? 1 : 0;
            _suppress = false;
            Render();
        }
        catch { SafeStatus(); }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e)
    {
        try
        {
            _suppress = true;
            object? sel = WeekStartCombo.SelectedIndex;
            WeekStartCombo.Items.Clear();
            WeekStartCombo.Items.Add(P("Monday", "星期一"));
            WeekStartCombo.Items.Add(P("Sunday", "星期日"));
            WeekStartCombo.SelectedIndex = _firstDay == DayOfWeek.Sunday ? 1 : 0;
            _suppress = false;
            Render();
        }
        catch { SafeStatus(); }
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Prev_Click(object sender, RoutedEventArgs e)
    {
        try { _display = SafeAddMonths(_display, -1); Render(); } catch { SafeStatus(); }
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        try { _display = SafeAddMonths(_display, 1); Render(); } catch { SafeStatus(); }
    }

    private void Today_Click(object sender, RoutedEventArgs e)
    {
        try { _display = DateTime.Today; _selected = DateTime.Today; Render(); } catch { SafeStatus(); }
    }

    private void WeekStart_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            _firstDay = WeekStartCombo.SelectedIndex == 1 ? DayOfWeek.Sunday : DayOfWeek.Monday;
            Render();
        }
        catch { SafeStatus(); }
    }

    private static DateTime SafeAddMonths(DateTime d, int months)
    {
        try { return d.AddMonths(months); } catch { return d; }
    }

    private void Render()
    {
        try
        {
            Header.Title = "Calendar · 月曆";
            HeaderBlurb.Text = P(
                "Browse any month at a glance — ISO week numbers down the left, today highlighted, and adjacent-month days dimmed. Click a day for its full details.",
                "一眼睇晒任何一個月 — 左邊有 ISO 週數，今日會高亮，隔籬月嘅日子會淡色顯示。撳一個日子就睇到完整資料。");
            WeekStartLabel.Text = P("Week starts on", "一星期由邊日開始");
            TodayBtn.Content = P("Today", "今日");
            PrevBtn.Content = "‹"; // ‹
            NextBtn.Content = "›"; // ›

            MonthTitle.Text = P(
                $"{CalendarMonthService.MonthName(_display.Month, false)} {_display.Year}",
                $"{_display.Year} 年 {CalendarMonthService.MonthName(_display.Month, true)}");

            BuildGridUi();
            RenderDetail();
            SafeStatus();
        }
        catch { SafeStatus(); }
    }

    private void BuildGridUi()
    {
        CalendarGrid.Children.Clear();
        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.ColumnDefinitions.Clear();

        bool zh = Loc.I.Pick("en", "zh") == "zh";

        // Column 0 = ISO week number, columns 1..7 = weekdays.
        CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
        for (int c = 0; c < 7; c++)
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // 1 header row + 6 week rows.
        for (int r = 0; r < 7; r++)
            CalendarGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var secondary = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];

        // Header: "Wk" label + weekday names.
        var wkHeader = new TextBlock
        {
            Text = P("Wk", "週"),
            FontSize = 11,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = secondary,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        Grid.SetRow(wkHeader, 0);
        Grid.SetColumn(wkHeader, 0);
        CalendarGrid.Children.Add(wkHeader);

        DayOfWeek[] order = CalendarMonthService.WeekdayOrder(_firstDay);
        for (int c = 0; c < 7; c++)
        {
            var hdr = new TextBlock
            {
                Text = CalendarMonthService.WeekdayShort(order[c], zh),
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(hdr, 0);
            Grid.SetColumn(hdr, c + 1);
            CalendarGrid.Children.Add(hdr);
        }

        var cells = CalendarMonthService.BuildGrid(_display.Year, _display.Month, _firstDay);
        Color accent = AccentColor();
        Color dim = Color.FromArgb(0x66, 0x88, 0x88, 0x88);       // dimmed adjacent-month text
        Color normal = NormalTextColor();

        for (int week = 0; week < 6; week++)
        {
            int rowStart = week * 7;

            // ISO week number for the first day in this row.
            var cell0 = cells[rowStart];
            string wkText = cell0.Date == DateTime.MinValue ? "" : CalendarMonthService.IsoWeek(cell0.Date).ToString();
            var wkBlock = new TextBlock
            {
                Text = wkText,
                FontSize = 11,
                Foreground = secondary,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
            Grid.SetRow(wkBlock, week + 1);
            Grid.SetColumn(wkBlock, 0);
            CalendarGrid.Children.Add(wkBlock);

            for (int col = 0; col < 7; col++)
            {
                var cell = cells[rowStart + col];
                var btn = new Button
                {
                    Content = cell.Date == DateTime.MinValue ? "" : cell.Day.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(1),
                    MinWidth = 0,
                    MinHeight = 40,
                    Padding = new Thickness(0),
                    Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)),
                    BorderThickness = new Thickness(0)
                };

                bool isToday = cell.Date != DateTime.MinValue && cell.Date.Date == DateTime.Today;
                bool isSelected = _selected.HasValue && cell.Date != DateTime.MinValue && cell.Date.Date == _selected.Value.Date;

                if (isToday)
                {
                    btn.Background = new SolidColorBrush(accent);
                    btn.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
                    btn.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                }
                else
                {
                    btn.Foreground = new SolidColorBrush(cell.InCurrentMonth ? normal : dim);
                    if (isSelected)
                        btn.BorderThickness = new Thickness(1);
                    if (isSelected)
                        btn.BorderBrush = new SolidColorBrush(accent);
                }

                if (cell.Date != DateTime.MinValue)
                {
                    var captured = cell.Date;
                    btn.Click += (_, _) => OnDayClick(captured);
                }
                else
                {
                    btn.IsEnabled = false;
                }

                Grid.SetRow(btn, week + 1);
                Grid.SetColumn(btn, col + 1);
                CalendarGrid.Children.Add(btn);
            }
        }
    }

    private void OnDayClick(DateTime date)
    {
        try { _selected = date; BuildGridUi(); RenderDetail(); } catch { SafeStatus(); }
    }

    private void RenderDetail()
    {
        try
        {
            if (!_selected.HasValue)
            {
                DetailText.Text = P("Click a day to see its details.", "撳一個日子睇詳細資料。");
                return;
            }

            DateTime d = _selected.Value;
            bool zh = Loc.I.Pick("en", "zh") == "zh";
            string weekday = CalendarMonthService.WeekdayLong(d.DayOfWeek, zh);
            int doy = CalendarMonthService.DayOfYear(d);
            int wk = CalendarMonthService.IsoWeek(d);
            int delta = CalendarMonthService.DaysFromToday(d);

            string relEn = delta == 0 ? "today" : delta > 0 ? $"in {delta} day(s)" : $"{-delta} day(s) ago";
            string relZh = delta == 0 ? "即係今日" : delta > 0 ? $"{delta} 日之後" : $"{-delta} 日之前";

            DetailText.Text = P(
                $"{d:yyyy-MM-dd} · {weekday} · day {doy} of {d.Year} · ISO week {wk} · {relEn}",
                $"{d:yyyy 年 M 月 d 日} · {weekday} · 全年第 {doy} 日 · ISO 第 {wk} 週 · {relZh}");
        }
        catch { DetailText.Text = P("Details unavailable.", "冇法顯示詳細資料。"); }
    }

    private void SafeStatus()
    {
        try
        {
            StatusText.Text = P(
                $"Showing {CalendarMonthService.MonthName(_display.Month, false)} {_display.Year}. Week starts {(_firstDay == DayOfWeek.Sunday ? "Sunday" : "Monday")}.",
                $"顯示緊 {_display.Year} 年 {CalendarMonthService.MonthName(_display.Month, true)}。一星期由{(_firstDay == DayOfWeek.Sunday ? "星期日" : "星期一")}開始。");
        }
        catch { /* never throw from status */ }
    }

    private static Color AccentColor()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("SystemAccentColor", out var v) && v is Color c)
                return c;
        }
        catch { }
        return Color.FromArgb(0xFF, 0x2D, 0x7D, 0x46); // reactor-green fallback
    }

    private static Color NormalTextColor()
    {
        try
        {
            if (Application.Current.Resources.TryGetValue("TextFillColorPrimary", out var v) && v is Color c)
                return c;
        }
        catch { }
        return Color.FromArgb(0xFF, 0xE0, 0xE0, 0xE0);
    }
}
