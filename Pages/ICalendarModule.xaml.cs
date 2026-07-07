using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// iCalendar (.ics) 產生器 · Build a valid RFC 5545 VCALENDAR/VEVENT (with RRULE + VALARM) from
/// simple fields, copy or save it, and parse a pasted .ics to list each event's title/start. Pure
/// managed, never throws, fully bilingual (粵語). Mirrors AwakeModule's rich control layout.
/// </summary>
public sealed partial class ICalendarModule : Page
{
    private bool _ready;

    public ICalendarModule()
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
            if (StartDate.Date is null) StartDate.Date = DateTimeOffset.Now;
            Render();
            _ready = true;
            Regenerate();
        }
        catch { /* never throw during load */ }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "iCalendar Builder · 日曆檔產生器";
            HeaderBlurb.Text = P(
                "Build a shareable calendar event (.ics) from a few fields — with recurrence and a reminder — then copy or save it. You can also paste an .ics file to list the events inside.",
                "填幾格就整到一個可以分享嘅日曆活動（.ics），仲支援重複同提醒，然後複製或者儲存。你亦可以貼一個 .ics 入嚟，睇下入面有咩活動。");

            DetailsTitle.Text = P("Event details", "活動詳情");
            SummaryBox.Header = P("Title", "標題");
            SummaryBox.PlaceholderText = P("e.g. Project review", "例如：專案檢討");
            LocationBox.Header = P("Location", "地點");
            LocationBox.PlaceholderText = P("e.g. Room 4B / Online", "例如：4B 室 / 線上");
            DescBox.Header = P("Description", "描述");

            StartDate.Header = P("Start date", "開始日期");
            StartDate.PlaceholderText = P("Pick a date", "揀日期");
            StartTime.Header = P("Start time", "開始時間");

            AllDayTitle.Text = P("All-day event", "全日活動");
            AllDayHint.Text = P("Ignores the time and duration below.", "會忽略下面嘅時間同長度。");
            DurationLabel.Text = P("Lasts for", "持續");
            SetCombo(DurationUnit, new[] { P("minutes", "分鐘"), P("hours", "小時") }, keepIndex: true, fallback: 0);

            RepeatTitle.Text = P("Repeat & reminder", "重複同提醒");
            RecurLabel.Text = P("Repeat", "重複");
            SetCombo(RecurCombo, new[]
            {
                P("Does not repeat", "唔重複"),
                P("Daily", "每日"),
                P("Weekly", "每週"),
                P("Monthly", "每月"),
                P("Yearly", "每年"),
            }, keepIndex: true, fallback: 0);
            IntervalLabel.Text = P("Every", "每隔");
            CountLabel.Text = P("times (0 = forever)", "次（0 = 無限）");

            ReminderLabel.Text = P("Reminder", "提醒");
            SetCombo(ReminderCombo, new[]
            {
                P("No reminder", "冇提醒"),
                P("5 minutes before", "提前 5 分鐘"),
                P("15 minutes before", "提前 15 分鐘"),
                P("30 minutes before", "提前 30 分鐘"),
                P("1 hour before", "提前 1 小時"),
            }, keepIndex: true, fallback: 0);

            OutputTitle.Text = P("Generated .ics", "產生嘅 .ics");
            CopyBtn.Content = P("Copy", "複製");
            SaveBtn.Content = P("Save .ics…", "儲存 .ics…");

            ParseTitle.Text = P("Parse an .ics file", "解析 .ics 檔");
            ParseInput.PlaceholderText = P("Paste .ics text here…", "喺度貼 .ics 文字…");
            ParseBtn.Content = P("List events", "列出活動");
            PasteBtn.Content = P("Paste from clipboard", "由剪貼簿貼上");

            UpdateVisibility();
        }
        catch { /* never throw */ }
    }

    private static void SetCombo(ComboBox combo, IReadOnlyList<string> items, bool keepIndex, int fallback)
    {
        int idx = combo.SelectedIndex;
        combo.Items.Clear();
        foreach (var it in items) combo.Items.Add(it);
        if (keepIndex && idx >= 0 && idx < items.Count) combo.SelectedIndex = idx;
        else combo.SelectedIndex = fallback;
    }

    private void AllDay_Toggled(object sender, RoutedEventArgs e)
    {
        UpdateVisibility();
        Regenerate();
    }

    private void AnyChanged(object sender, object e)
    {
        UpdateVisibility();
        Regenerate();
    }

    private void Start_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        UpdateVisibility();
        Regenerate();
    }

    private void Start_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
    {
        UpdateVisibility();
        Regenerate();
    }

    private void UpdateVisibility()
    {
        try
        {
            bool allDay = AllDaySwitch.IsOn;
            if (DurationPanel is not null) DurationPanel.Visibility = allDay ? Visibility.Collapsed : Visibility.Visible;
            if (StartTime is not null) StartTime.IsEnabled = !allDay;
            bool repeats = RecurCombo.SelectedIndex > 0;
            if (RecurDetail is not null) RecurDetail.Visibility = repeats ? Visibility.Visible : Visibility.Collapsed;
        }
        catch { }
    }

    private ICalendarService.EventSpec BuildSpec()
    {
        var spec = new ICalendarService.EventSpec
        {
            Summary = SummaryBox.Text ?? "",
            Location = LocationBox.Text ?? "",
            Description = DescBox.Text ?? "",
            AllDay = AllDaySwitch.IsOn,
        };

        // Compose start date + time.
        DateTimeOffset date = StartDate.Date ?? DateTimeOffset.Now;
        TimeSpan tod = StartTime.SelectedTime ?? TimeSpan.Zero;
        spec.Start = new DateTimeOffset(date.Year, date.Month, date.Day,
            tod.Hours, tod.Minutes, 0, date.Offset);

        // Duration → minutes.
        double dv = double.IsNaN(DurationBox.Value) ? 60 : DurationBox.Value;
        bool hours = DurationUnit.SelectedIndex == 1;
        spec.DurationMinutes = Math.Max(1, (int)(hours ? dv * 60 : dv));

        spec.Recurrence = RecurCombo.SelectedIndex switch
        {
            1 => ICalendarService.Recur.Daily,
            2 => ICalendarService.Recur.Weekly,
            3 => ICalendarService.Recur.Monthly,
            4 => ICalendarService.Recur.Yearly,
            _ => ICalendarService.Recur.None,
        };
        spec.Interval = Math.Max(1, (int)(double.IsNaN(IntervalBox.Value) ? 1 : IntervalBox.Value));
        spec.Count = Math.Max(0, (int)(double.IsNaN(CountBox.Value) ? 0 : CountBox.Value));

        spec.ReminderMinutes = ReminderCombo.SelectedIndex switch
        {
            1 => 5,
            2 => 15,
            3 => 30,
            4 => 60,
            _ => -1,
        };
        return spec;
    }

    private void Regenerate()
    {
        if (!_ready) return;
        try
        {
            OutputBox.Text = ICalendarService.Build(BuildSpec());
            ShowStatus(P("Event ready — copy or save it.", "活動已備妥 — 複製或者儲存。"), InfoBarSeverity.Success);
        }
        catch
        {
            ShowStatus(P("Could not generate the event.", "產生唔到活動。"), InfoBarSeverity.Error);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? "";
            if (string.IsNullOrEmpty(text)) { ShowStatus(P("Nothing to copy yet.", "而家冇嘢可以複製。"), InfoBarSeverity.Warning); return; }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            ShowStatus(P("Copied to clipboard.", "已複製到剪貼簿。"), InfoBarSeverity.Success);
        }
        catch { ShowStatus(P("Copy failed.", "複製失敗。"), InfoBarSeverity.Error); }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? "";
            if (string.IsNullOrEmpty(text)) { ShowStatus(P("Nothing to save yet.", "而家冇嘢可以儲存。"), InfoBarSeverity.Warning); return; }

            string suggested = MakeFileName();
            var filters = new[] { new FileDialogs.Filter("iCalendar", "*.ics"), new FileDialogs.Filter("All files", "*.*") };
            string? path = await FileDialogs.SaveFileAsync(suggested, filters, "ics",
                P("Save calendar event", "儲存日曆活動"));
            if (string.IsNullOrEmpty(path)) return;

            await System.IO.File.WriteAllTextAsync(path, text, new System.Text.UTF8Encoding(false));
            ShowStatus(P("Saved: ", "已儲存：") + path, InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            ShowStatus(P("Save failed: ", "儲存失敗：") + ex.Message, InfoBarSeverity.Error);
        }
    }

    private string MakeFileName()
    {
        try
        {
            string title = (SummaryBox.Text ?? "").Trim();
            if (title.Length == 0) title = "event";
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                title = title.Replace(c, '_');
            if (title.Length > 40) title = title.Substring(0, 40);
            return title + ".ics";
        }
        catch { return "event.ics"; }
    }

    private void Parse_Click(object sender, RoutedEventArgs e) => RunParse(ParseInput.Text ?? "");

    private async void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view is not null && view.Contains(StandardDataFormats.Text))
            {
                string text = await view.GetTextAsync() ?? "";
                ParseInput.Text = text;
                RunParse(text);
            }
            else
            {
                ShowStatus(P("Clipboard has no text.", "剪貼簿冇文字。"), InfoBarSeverity.Warning);
            }
        }
        catch { ShowStatus(P("Paste failed.", "貼上失敗。"), InfoBarSeverity.Error); }
    }

    private void RunParse(string ics)
    {
        try
        {
            var events = ICalendarService.Parse(ics);
            ParseList.ItemsSource = events;
            if (events.Count == 0)
                ShowStatus(P("No VEVENT found in that text.", "喺文字入面搵唔到 VEVENT。"), InfoBarSeverity.Warning);
            else
                ShowStatus(P($"Found {events.Count} event(s).", $"搵到 {events.Count} 個活動。"), InfoBarSeverity.Success);
        }
        catch { ShowStatus(P("Could not parse that text.", "解析唔到嗰段文字。"), InfoBarSeverity.Error); }
    }

    private void ShowStatus(string msg, InfoBarSeverity severity)
    {
        try
        {
            Status.Message = msg;
            Status.Severity = severity;
            Status.IsOpen = true;
        }
        catch { }
    }
}
