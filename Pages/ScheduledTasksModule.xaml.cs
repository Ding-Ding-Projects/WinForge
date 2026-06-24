using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內排程工作管理員 · In-app Task Scheduler — list, search, run/stop/enable/disable, no
/// taskschd.msc redirect. Bilingual.
/// </summary>
public sealed partial class ScheduledTasksModule : Page
{
    private List<TaskInfo> _all = new();
    private bool _busy;

    public ScheduledTasksModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Scheduled Tasks · 排程工作";
        FilterBox.PlaceholderText = P("Filter tasks (e.g. Appraiser, Consolidator)…", "篩選工作（例如 Appraiser、Consolidator）…");
        RefreshBtn.Content = P("Refresh", "重新整理");
        if (!AdminHelper.IsElevated)
        {
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Tip", "提示");
            ResultBar.Message = P("Relaunch WinTune as administrator to change protected tasks.",
                "以管理員身分重開 WinTune 先可以改受保護嘅工作。");
            ResultBar.IsOpen = true;
        }
    }

    private async Task Reload()
    {
        if (_busy) return;
        _busy = true;
        CountText.Text = P("Loading…", "載入緊…");
        try
        {
            _all = await TaskSchedulerManager.ListAsync();
            ApplyFilter(FilterBox.Text ?? string.Empty);
        }
        finally { _busy = false; }
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<TaskInfo> shown = _all;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _all.Where(t => t.TaskName.ToLowerInvariant().Contains(f) || t.TaskPath.ToLowerInvariant().Contains(f));
        }
        var listed = shown.ToList();
        List.ItemsSource = listed;
        CountText.Text = P($"{listed.Count} / {_all.Count} tasks", $"{listed.Count} / {_all.Count} 個工作");
        EmptyText.Text = _all.Count == 0
            ? P("No scheduled tasks found.", "搵唔到排程工作。")
            : P("No tasks match your filter.", "冇工作符合你嘅篩選。");
        EmptyText.Visibility = listed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ApplyFilter(sender.Text ?? string.Empty);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private static TaskInfo? Item(object sender) => (sender as FrameworkElement)?.DataContext as TaskInfo;

    private void Actions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not TaskInfo task) return;
        var mf = new MenuFlyout();
        void Add(string en, string zh, string glyph, Func<TaskInfo, System.Threading.CancellationToken, Task<TweakResult>> op)
        {
            var it = new MenuFlyoutItem { Text = $"{en} · {zh}", Icon = new FontIcon { Glyph = glyph } };
            it.Click += async (_, _) => await Run(task, op, P(en, zh));
            mf.Items.Add(it);
        }
        Add("Run", "執行", ((char)0xE768).ToString(), TaskSchedulerManager.Run);
        Add("Stop", "停止", ((char)0xE71A).ToString(), TaskSchedulerManager.Stop);
        Add("Enable", "啟用", ((char)0xE73E).ToString(), TaskSchedulerManager.Enable);
        Add("Disable", "停用", ((char)0xE711).ToString(), TaskSchedulerManager.Disable);
        mf.ShowAt(b);
    }

    private async Task Run(TaskInfo? task, Func<TaskInfo, System.Threading.CancellationToken, Task<TweakResult>> op, string verb)
    {
        if (task is null || _busy) return;
        _busy = true;
        try
        {
            var r = await op(task, System.Threading.CancellationToken.None);
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = needAdmin
                ? P($"{verb} '{task.TaskName}' needs administrator rights.", $"{verb}「{task.TaskName}」需要管理員權限。")
                : $"{verb} '{task.TaskName}' — {(r.Success ? "OK" : (r.Output ?? ""))}";
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        await Reload();
    }
}
