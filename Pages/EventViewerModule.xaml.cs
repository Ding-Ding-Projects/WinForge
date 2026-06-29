using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內事件檢視器 · In-app Event Viewer (wraps Get-WinEvent) — browse System/Application/Security/
/// Setup logs with a level filter and a detail pane. Replaces the eventvwr.msc redirect. Bilingual.
/// </summary>
public sealed partial class EventViewerModule : Page
{
    public sealed class EvtView
    {
        public string Time { get; init; } = "";
        public int Id { get; init; }
        public string Level { get; init; } = "";
        public string Provider { get; init; } = "";
        public string Message { get; init; } = "";
        public string FirstLine => (Message ?? "").Split('\n').FirstOrDefault()?.Trim() ?? "";
        public SolidColorBrush LevelBrush => Level switch
        {
            "Critical" or "Error" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE8, 0x11, 0x23)),
            "Warning" => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xB8, 0x8A, 0x00)),
            _ => (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
    }

    private readonly ObservableCollection<EvtView> _rows = new();
    private List<EventRow> _all = new();
    private string _filter = "";
    private bool _ready;

    public EventViewerModule()
    {
        InitializeComponent();
        List.ItemsSource = _rows;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); _ready = true; await Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Event Viewer · 事件檢視器";
        HeaderBlurb.Text = P("Browse the Windows event logs in-app — pick a log and severity, filter, and read the full message. No eventvwr.msc.",
            "喺 app 內睇 Windows 事件記錄 — 揀記錄同嚴重程度、篩選、睇完整訊息。唔使開 eventvwr.msc。");
        FilterBox.PlaceholderText = P("Filter by provider, message or ID…", "用來源、訊息或 ID 篩選…");
        ColTime.Text = P("Time", "時間");
        ColLevel.Text = P("Level", "層級");
        ColId.Text = P("ID", "ID");
        ColProvider.Text = P("Source", "來源");
        ColMessage.Text = P("Message", "訊息");

        FillCombo(LogBox, new[] { "System", "Application", "Security", "Setup" });
        FillCombo(LevelBox, new[] { P("All levels", "所有層級"), P("Errors", "錯誤"), P("Warnings+", "警告以上"), P("Information", "資訊") });
    }

    private static void FillCombo(ComboBox box, string[] items)
    {
        int sel = box.SelectedIndex < 0 ? 0 : box.SelectedIndex;
        box.Items.Clear();
        foreach (var i in items) box.Items.Add(i);
        box.SelectedIndex = Math.Min(sel, items.Length - 1);
    }

    private string CurrentLog() => LogBox.SelectedIndex switch { 1 => "Application", 2 => "Security", 3 => "Setup", _ => "System" };
    private string CurrentLevels() => LevelBox.SelectedIndex switch { 1 => "error", 2 => "warn", 3 => "info", _ => "all" };

    private async void Query_Changed(object sender, object e) { if (_ready) await Reload(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) { _ = Reload(); }

    private void Filter_Changed(object sender, object e)
    {
        _filter = (FilterBox.Text ?? "").Trim();
        ApplyFilter();
    }

    private async System.Threading.Tasks.Task Reload()
    {
        Busy.IsActive = true;
        HintBar.IsOpen = false;
        int max = (int)(double.IsNaN(CountBox.Value) ? 100 : CountBox.Value);
        string log = CurrentLog();
        try { _all = await EventLogService.QueryAsync(log, CurrentLevels(), max); }
        catch { _all = new List<EventRow>(); }
        Busy.IsActive = false;

        ApplyFilter();

        if (_all.Count == 0)
        {
            HintBar.Severity = InfoBarSeverity.Informational;
            HintBar.Title = P("No matching events", "冇符合嘅事件");
            HintBar.Message = log == "Security" && !AdminHelper.IsElevated
                ? P("The Security log needs administrator rights — relaunch WinForge as admin.", "安全記錄要管理員權限 — 請以管理員身分重開 WinForge。")
                : P("Nothing at this level in this log.", "呢個記錄喺呢個層級冇嘢。");
            HintBar.IsOpen = true;
        }
    }

    private void ApplyFilter()
    {
        IEnumerable<EventRow> src = _all;
        if (_filter.Length > 0)
        {
            var f = _filter;
            src = src.Where(r =>
                (r.Provider ?? "").Contains(f, StringComparison.OrdinalIgnoreCase) ||
                (r.Message ?? "").Contains(f, StringComparison.OrdinalIgnoreCase) ||
                r.Id.ToString().Contains(f));
        }
        _rows.Clear();
        foreach (var r in src)
            _rows.Add(new EvtView { Time = r.Time, Id = r.Id, Level = r.Level, Provider = r.Provider, Message = r.Message });

        ColMessage.Text = P($"Message — {_rows.Count} shown", $"訊息 — 顯示 {_rows.Count} 條");
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (List.SelectedItem is EvtView v)
        {
            DetailHead.Text = $"{v.Level}  ·  ID {v.Id}  ·  {v.Provider}  ·  {v.Time}";
            DetailBody.Text = v.Message;
        }
        else { DetailHead.Text = ""; DetailBody.Text = ""; }
    }
}
