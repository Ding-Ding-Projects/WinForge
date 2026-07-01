using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 日期計算器 · Date Calculator — difference between two dates, add/subtract offsets, age &amp;
/// next-birthday countdown, and calendar facts (weekday, ISO week, day-of-year, leap year).
/// Pure managed (<see cref="DateCalcService"/>). Bilingual, never throws. No redirect.
/// </summary>
public sealed partial class DateCalcModule : Page
{
    public DateCalcModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
        Loaded += (_, _) => Render();
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void AnyChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs e) => Render();
    private void AnyToggled(object sender, RoutedEventArgs e) => Render();
    private void AnyNum(NumberBox sender, NumberBoxValueChangedEventArgs e) => Render();

    private void Render()
    {
        try
        {
            Header.Title = "Date Calculator · 日期計算器";
            HeaderBlurb.Text = P("Work out the span between two dates, shift a date by any offset, figure out an exact age, or look up calendar facts — all offline.",
                "計兩個日期之間隔幾耐、將日期加減、計準確年齡、或者查日曆資料 — 全部離線運算。");
            DiffTitle.Text = P("Difference between two dates", "兩個日期之間嘅差距");
            AddTitle.Text = P("Add / subtract from a date", "由某日加或減");
            AgeTitle.Text = P("Age & next birthday", "年齡同下一個生日");
            InfoTitle.Text = P("Date info", "日期資料");

            AddMode.OnContent = P("Subtract", "減");
            AddMode.OffContent = P("Add", "加");
            AddYears.Header = P("Years", "年");
            AddMonths.Header = P("Months", "月");
            AddWeeks.Header = P("Weeks", "週");
            AddDays.Header = P("Days", "日");

            string copy = P("Copy result", "複製結果");
            DiffCopy.Content = copy;
            AddCopy.Content = copy;
            AgeCopy.Content = copy;
            InfoCopy.Content = copy;

            RenderDiff();
            RenderAdd();
            RenderAge();
            RenderInfo();
            StatusText.Text = "";
        }
        catch (Exception ex)
        {
            SetStatus(ex);
        }
    }

    // Cached results for the copy buttons.
    private string _diffText = "", _addText = "", _ageText = "", _infoText = "";

    private void RenderDiff()
    {
        if (DiffFrom.Date is not DateTimeOffset from || DiffTo.Date is not DateTimeOffset to)
        {
            _diffText = "";
            DiffResult.Text = P("Pick both dates.", "請揀兩個日期。");
            return;
        }
        var d = DateCalcService.Diff(from.DateTime, to.DateTime);
        string sign = d.Negative ? P(" (earlier)", "（較早）") : "";
        _diffText = P(
            $"Total: {d.TotalDays:N0} days{sign}\n" +
            $"= {d.Weeks:N0} weeks and {d.RemDays} days\n" +
            $"= {d.Years}y {d.Months}m {d.Days}d\n" +
            $"Business days (Mon–Fri, start inclusive, end exclusive): {d.BusinessDays:N0}",
            $"合共：{d.TotalDays:N0} 日{sign}\n" +
            $"= {d.Weeks:N0} 週 {d.RemDays} 日\n" +
            $"= {d.Years}年 {d.Months}月 {d.Days}日\n" +
            $"工作日（週一至週五，包含起始、不含結束）：{d.BusinessDays:N0}");
        DiffResult.Text = _diffText;
    }

    private void RenderAdd()
    {
        if (AddBase.Date is not DateTimeOffset b)
        {
            _addText = "";
            AddResult.Text = P("Pick a base date.", "請揀一個基準日期。");
            return;
        }
        int y = NB(AddYears), m = NB(AddMonths), w = NB(AddWeeks), da = NB(AddDays);
        var result = DateCalcService.Offset(b.DateTime, y, m, w, da, AddMode.IsOn);
        string verb = AddMode.IsOn ? P("−", "−") : P("+", "+");
        _addText = P(
            $"{verb} {y}y {m}m {w}w {da}d → {result:dddd, dd MMM yyyy}",
            $"{verb} {y}年 {m}月 {w}週 {da}日 → {result:yyyy年MM月dd日} {result:dddd}");
        AddResult.Text = _addText;
    }

    private void RenderAge()
    {
        if (AgeBirth.Date is not DateTimeOffset birth)
        {
            _ageText = "";
            AgeResult.Text = P("Pick a birth date.", "請揀出生日期。");
            return;
        }
        var a = DateCalcService.Age(birth.DateTime, DateTime.Now);
        if (a.NotYetBorn)
        {
            _ageText = P($"That date is {a.DaysToNextBirthday:N0} days in the future.",
                         $"嗰個日期喺 {a.DaysToNextBirthday:N0} 日之後。");
            AgeResult.Text = _ageText;
            return;
        }
        _ageText = P(
            $"Age: {a.Years} years, {a.Months} months, {a.Days} days\n" +
            $"Days lived: {a.TotalDays:N0}\n" +
            $"Next birthday: {a.NextBirthday:dddd, dd MMM yyyy} — in {a.DaysToNextBirthday:N0} days",
            $"年齡：{a.Years} 年 {a.Months} 個月 {a.Days} 日\n" +
            $"已活日數：{a.TotalDays:N0}\n" +
            $"下一個生日：{a.NextBirthday:yyyy年MM月dd日} {a.NextBirthday:dddd} — 仲有 {a.DaysToNextBirthday:N0} 日");
        AgeResult.Text = _ageText;
    }

    private void RenderInfo()
    {
        if (InfoDate.Date is not DateTimeOffset dt)
        {
            _infoText = "";
            InfoResult.Text = P("Pick a date.", "請揀一個日期。");
            return;
        }
        var f = DateCalcService.Facts(dt.DateTime);
        string leap = f.LeapYear ? P("yes", "係") : P("no", "唔係");
        _infoText = P(
            $"Weekday: {dt.DateTime:dddd}\n" +
            $"ISO 8601 week: {f.IsoWeek} of {f.IsoYear}\n" +
            $"Day of year: {f.DayOfYear}\n" +
            $"Leap year: {leap}",
            $"星期：{dt.DateTime:dddd}\n" +
            $"ISO 8601 週數：{f.IsoYear} 年第 {f.IsoWeek} 週\n" +
            $"一年中第幾日：{f.DayOfYear}\n" +
            $"閏年：{leap}");
        InfoResult.Text = _infoText;
    }

    private static int NB(NumberBox box)
    {
        double v = box.Value;
        return double.IsNaN(v) ? 0 : (int)v;
    }

    private void DiffCopy_Click(object sender, RoutedEventArgs e) => Copy(_diffText);
    private void AddCopy_Click(object sender, RoutedEventArgs e) => Copy(_addText);
    private void AgeCopy_Click(object sender, RoutedEventArgs e) => Copy(_ageText);
    private void InfoCopy_Click(object sender, RoutedEventArgs e) => Copy(_infoText);

    private void Copy(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            SetStatus(ex);
        }
    }

    private void SetStatus(Exception ex)
    {
        try { StatusText.Text = P("Something went wrong: ", "出錯：") + ex.Message; }
        catch { /* never throw from status */ }
    }
}
