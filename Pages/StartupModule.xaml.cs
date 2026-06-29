using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內開機程式管理員 · In-app startup-apps manager — list Run keys + Startup folders and
/// enable/disable each (via the StartupApproved blob). No Task Manager / Settings redirect. Bilingual.
/// </summary>
public sealed partial class StartupModule : Page
{
    private List<StartupItem> _all = new();

    public StartupModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Startup Apps · 開機程式";
        FilterBox.PlaceholderText = P("Filter startup apps…", "篩選開機程式…");
        RefreshBtn.Content = P("Refresh", "重新整理");
    }

    private void Reload()
    {
        _all = StartupManager.List();
        ApplyFilter(FilterBox.Text ?? string.Empty);
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<StartupItem> shown = _all;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _all.Where(s => s.Name.ToLowerInvariant().Contains(f) || s.Command.ToLowerInvariant().Contains(f));
        }
        var listed = shown.ToList();
        List.ItemsSource = listed;
        CountText.Text = P($"{listed.Count} / {_all.Count} apps", $"{listed.Count} / {_all.Count} 個程式");
    }

    private void Filter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            ApplyFilter(sender.Text ?? string.Empty);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private static StartupItem? Item(object sender) => (sender as FrameworkElement)?.DataContext as StartupItem;

    private void Enable_Click(object sender, RoutedEventArgs e) => Toggle(Item(sender), true);
    private void Disable_Click(object sender, RoutedEventArgs e) => Toggle(Item(sender), false);

    private void Toggle(StartupItem? item, bool enabled)
    {
        if (item is null) return;
        try
        {
            StartupManager.SetEnabled(item, enabled);
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Done", "完成");
            ResultBar.Message = P($"{(enabled ? "Enabled" : "Disabled")} '{item.Name}'.", $"已{(enabled ? "啟用" : "停用")}「{item.Name}」。");
            ResultBar.IsOpen = true;
            Reload();
        }
        catch (UnauthorizedAccessException)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = P($"'{item.Name}' is a system (HKLM) entry — relaunch as administrator.",
                $"「{item.Name}」係系統 (HKLM) 項目 — 請以管理員身分重開。");
            ResultBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
        }
    }
}
