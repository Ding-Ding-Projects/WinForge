using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Cron 下次執行時間計算器 · Cron next-run calculator. Parses a 5-field cron expression (with macros),
/// computes the next N fire times in a chosen time zone, shows a bilingual plain-English description,
/// and offers presets + copy. Pure managed — see <see cref="CronNextService"/>. Never throws.
/// </summary>
public sealed partial class CronNextModule : Page
{
    /// <summary>Row shown in the ListView (classic {Binding}, not x:Bind).</summary>
    public sealed class RunRow
    {
        public string Index { get; set; } = "";
        public string Iso { get; set; } = "";
        public string Relative { get; set; } = "";
    }

    private readonly ObservableCollection<RunRow> _rows = new();
    private readonly List<TimeZoneInfo> _zones = new();
    private bool _ready;

    public CronNextModule()
    {
        InitializeComponent();
        RunsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { InitZones(); Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Recompute(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void InitZones()
    {
        if (_zones.Count > 0) return;
        try
        {
            foreach (var z in TimeZoneInfo.GetSystemTimeZones())
                _zones.Add(z);
        }
        catch { /* never throw */ }

        // Ensure local is present & selectable.
        TimeZoneInfo local;
        try { local = TimeZoneInfo.Local; } catch { local = TimeZoneInfo.Utc; }
        if (!_zones.Any(z => z.Id == local.Id))
            _zones.Insert(0, local);

        TzBox.Items.Clear();
        foreach (var z in _zones)
            TzBox.Items.Add(z.DisplayName);

        int idx = _zones.FindIndex(z => z.Id == local.Id);
        TzBox.SelectedIndex = idx >= 0 ? idx : 0;

        if (string.IsNullOrWhiteSpace(ExprBox.Text))
            ExprBox.Text = "*/15 * * * *";

        _ready = true;
    }

    private TimeZoneInfo SelectedZone()
    {
        int i = TzBox.SelectedIndex;
        if (i >= 0 && i < _zones.Count) return _zones[i];
        try { return TimeZoneInfo.Local; } catch { return TimeZoneInfo.Utc; }
    }

    private void Render()
    {
        Header.Title = "Cron Next Runs · Cron 下次執行時間";
        HeaderBlurb.Text = P("Type a 5-field cron expression and see exactly when it will next fire — in any time zone — with a plain-English read-out. No jobs are scheduled; this only calculates.",
            "打一條 5 欄位嘅 cron 運算式，睇下佢下次幾時會觸發（可揀任何時區），仲會用人話講返個排程。唔會真係排任何工作，淨係計時間。");

        ExprLabel.Text = P("Cron expression", "Cron 運算式");
        ExprHint.Text = P("Fields: minute hour day-of-month month day-of-week. Supports * , - / and @hourly @daily @weekly @monthly @yearly.",
            "欄位：分 時 日 月 星期。支援 * , - / 同 @hourly @daily @weekly @monthly @yearly。");

        Preset15.Content = P("Every 15 min", "每 15 分鐘");
        PresetWeekdays.Content = P("Weekdays 9am", "平日朝早 9 點");
        PresetFirst.Content = P("1st of month", "每月 1 號");

        TzLabel.Text = P("Time zone", "時區");
        CountLabel.Text = P("Show", "顯示");
        RunsTitle.Text = P("Upcoming runs", "接下來嘅執行時間");
        DescTitle.Text = P("What this means", "呢個排程係咩意思");
        CopyBtn.Content = P("Copy list", "複製清單");
    }

    private void Input_Changed(object sender, object e)
    {
        if (!_ready) return;
        Recompute();
    }

    private void Count_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (!_ready) return;
        Recompute();
    }

    private void Recompute()
    {
        try
        {
            _rows.Clear();
            var parse = CronNextService.Parse(ExprBox.Text);
            if (!parse.Ok || parse.Schedule == null)
            {
                DescEn.Text = "";
                DescZh.Text = "";
                Status.Severity = InfoBarSeverity.Error;
                Status.Title = P("Invalid expression", "運算式無效");
                Status.Message = P(parse.ErrorEn ?? "Parse error.", parse.ErrorZh ?? "解析錯誤。");
                Status.IsOpen = true;
                return;
            }

            var sched = parse.Schedule;
            var (dEn, dZh) = CronNextService.Describe(sched);
            DescEn.Text = dEn;
            DescZh.Text = dZh;

            var tz = SelectedZone();
            int count = 10;
            if (!double.IsNaN(CountBox.Value)) count = Math.Clamp((int)CountBox.Value, 1, 200);

            DateTimeOffset now;
            try { now = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, tz); }
            catch { now = DateTimeOffset.Now; }

            var runs = CronNextService.NextRuns(sched, now, tz, count);
            if (runs.Count == 0)
            {
                Status.Severity = InfoBarSeverity.Warning;
                Status.Title = P("No runs found", "搵唔到執行時間");
                Status.Message = P("This schedule doesn't fire within the ~4-year search window (e.g. Feb 30).",
                    "呢個排程喺約 4 年嘅搜尋範圍內都唔會觸發（例如 2 月 30 號）。");
                Status.IsOpen = true;
                return;
            }

            int idx = 1;
            foreach (var r in runs)
            {
                var (relEn, relZh) = CronNextService.Relative(r, now);
                _rows.Add(new RunRow
                {
                    Index = idx.ToString(CultureInfo.InvariantCulture) + ".",
                    Iso = r.ToString("yyyy-MM-dd HH:mm (ddd)", CultureInfo.InvariantCulture),
                    Relative = P(relEn, relZh),
                });
                idx++;
            }

            Status.Severity = InfoBarSeverity.Success;
            Status.Title = P("Valid", "有效");
            Status.Message = P($"Next {runs.Count} run(s) in {tz.DisplayName}.",
                $"喺 {tz.DisplayName} 嘅下 {runs.Count} 次執行。");
            Status.IsOpen = true;
        }
        catch (Exception ex)
        {
            Status.Severity = InfoBarSeverity.Error;
            Status.Title = P("Error", "出錯");
            Status.Message = ex.Message;
            Status.IsOpen = true;
        }
    }

    private void Preset15_Click(object sender, RoutedEventArgs e) => SetExpr("*/15 * * * *");
    private void PresetWeekdays_Click(object sender, RoutedEventArgs e) => SetExpr("0 9 * * 1-5");
    private void PresetFirst_Click(object sender, RoutedEventArgs e) => SetExpr("0 0 1 * *");

    private void SetExpr(string expr)
    {
        ExprBox.Text = expr; // TextChanged triggers Recompute
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_rows.Count == 0)
            {
                Status.Severity = InfoBarSeverity.Informational;
                Status.Title = P("Nothing to copy", "冇嘢可以複製");
                Status.Message = P("Enter a valid cron expression first.", "先輸入一條有效嘅 cron 運算式。");
                Status.IsOpen = true;
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine(ExprBox.Text.Trim());
            foreach (var r in _rows)
                sb.AppendLine($"{r.Iso}\t{r.Relative}");

            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(sb.ToString());
            Clipboard.SetContent(pkg);

            Status.Severity = InfoBarSeverity.Success;
            Status.Title = P("Copied", "已複製");
            Status.Message = P($"{_rows.Count} run(s) copied to the clipboard.", $"已複製 {_rows.Count} 次執行時間到剪貼簿。");
            Status.IsOpen = true;
        }
        catch (Exception ex)
        {
            Status.Severity = InfoBarSeverity.Error;
            Status.Title = P("Copy failed", "複製失敗");
            Status.Message = ex.Message;
            Status.IsOpen = true;
        }
    }
}
