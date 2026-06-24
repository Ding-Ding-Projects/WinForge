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
/// 應用程式內服務管理員 · In-app Services Manager — list, search, start/stop/restart and set startup type,
/// all without leaving WinForge (no services.msc redirect). Bilingual.
/// </summary>
public sealed partial class ServicesModule : Page
{
    private List<ServiceInfo> _all = new();
    private bool _busy;

    public ServicesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Services · 服務";
        FilterBox.PlaceholderText = P("Filter services…", "篩選服務…");
        RefreshBtn.Content = P("Refresh", "重新整理");
        if (!AdminHelper.IsElevated)
        {
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Tip", "提示");
            ResultBar.Message = P("Relaunch WinForge as administrator to start/stop services.",
                "以管理員身分重開 WinForge 先可以啟動／停止服務。");
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
            _all = await ServiceManager.ListAsync();
            ApplyFilter(FilterBox.Text ?? string.Empty);
        }
        finally { _busy = false; }
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<ServiceInfo> shown = _all;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _all.Where(s => s.DisplayName.ToLowerInvariant().Contains(f) || s.Name.ToLowerInvariant().Contains(f));
        }
        var listed = shown.ToList();
        List.ItemsSource = listed;
        CountText.Text = P($"{listed.Count} / {_all.Count} services", $"{listed.Count} / {_all.Count} 個服務");
        EmptyText.Text = _all.Count == 0 ? P("No services found.", "搵唔到服務。") : P("No services match your filter.", "冇服務符合你嘅篩選。");
        EmptyText.Visibility = listed.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ApplyFilter(sender.Text ?? string.Empty);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await Reload();

    private void Actions_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.DataContext is not ServiceInfo svc) return;
        var mf = new MenuFlyout();
        void Add(string en, string zh, string glyph, Func<string, Task<TweakResult>> op)
        {
            var it = new MenuFlyoutItem { Text = $"{en} · {zh}", Icon = new FontIcon { Glyph = glyph } };
            it.Click += async (_, _) => await Run(svc, op, P(en, zh));
            mf.Items.Add(it);
        }
        Add("Start", "啟動", ((char)0xE768).ToString(), n => ServiceManager.Start(n));
        Add("Stop", "停止", ((char)0xE71A).ToString(), n => ServiceManager.Stop(n));
        Add("Restart", "重啟", ((char)0xE72C).ToString(), n => ServiceManager.Restart(n));

        mf.Items.Add(new MenuFlyoutSeparator());
        var sub = new MenuFlyoutSubItem { Text = P("Startup type", "啟動類型"), Icon = new FontIcon { Glyph = ((char)0xE713).ToString() } };
        void AddStartup(string en, string zh, string mode)
        {
            var it = new MenuFlyoutItem { Text = $"{en} · {zh}" };
            it.Click += async (_, _) => await Run(svc, n => ServiceManager.SetStartup(n, mode), P($"Set {en}", $"設{zh}"));
            sub.Items.Add(it);
        }
        AddStartup("Automatic", "自動", "Automatic");
        AddStartup("Manual", "手動", "Manual");
        AddStartup("Disabled", "停用", "Disabled");
        mf.Items.Add(sub);

        mf.ShowAt(b);
    }

    private async Task Run(ServiceInfo? svc, Func<string, Task<TweakResult>> op, string verb)
    {
        if (svc is null || _busy) return;
        _busy = true;
        try
        {
            var r = await op(svc.Name);
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = needAdmin
                ? P($"{verb} '{svc.DisplayName}' needs administrator rights.", $"{verb}「{svc.DisplayName}」需要管理員權限。")
                : $"{verb} '{svc.DisplayName}' — {(r.Success ? "OK" : (r.Output ?? ""))}";
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        await Reload();
    }
}
