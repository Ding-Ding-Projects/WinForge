using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內裝置管理員 · In-app Devices Manager (Get-PnpDevice) — list, search, enable/disable,
/// no devmgmt.msc redirect. Bilingual; disabling asks for confirmation.
/// </summary>
public sealed partial class DevicesModule : Page
{
    private List<DeviceInfo> _all = new();
    private bool _busy;

    public DevicesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Devices · 裝置";
        FilterBox.PlaceholderText = P("Filter devices (e.g. Bluetooth, Audio)…", "篩選裝置（例如 Bluetooth、Audio）…");
        RefreshBtn.Content = P("Refresh", "重新整理");
        if (!AdminHelper.IsElevated)
        {
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Tip", "提示");
            ResultBar.Message = P("Relaunch WinForge as administrator to enable/disable devices.",
                "以管理員身分重開 WinForge 先可以啟用／停用裝置。");
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
            _all = await DeviceManager.ListAsync();
            ApplyFilter(FilterBox.Text ?? string.Empty);
        }
        finally { _busy = false; }
    }

    private void ApplyFilter(string filter)
    {
        IEnumerable<DeviceInfo> shown = _all;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _all.Where(d => d.Display.ToLowerInvariant().Contains(f) || d.Class.ToLowerInvariant().Contains(f));
        }
        var listed = shown.ToList();
        List.ItemsSource = listed;
        CountText.Text = P($"{listed.Count} / {_all.Count} devices", $"{listed.Count} / {_all.Count} 個裝置");
        EmptyText.Text = _all.Count == 0
            ? P("Loading devices… or none found.", "載入緊裝置…或者搵唔到。")
            : P("No devices match your filter.", "冇裝置符合你嘅篩選。");
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
        if (sender is not Button b || b.DataContext is not DeviceInfo dev) return;
        var mf = new MenuFlyout();

        var enable = new MenuFlyoutItem { Text = "Enable · 啟用", Icon = new FontIcon { Glyph = ((char)0xE73E).ToString() } };
        enable.Click += async (_, _) => await Run(dev, DeviceManager.Enable, P("Enable", "啟用"));
        mf.Items.Add(enable);

        var disable = new MenuFlyoutItem { Text = "Disable · 停用", Icon = new FontIcon { Glyph = ((char)0xE711).ToString() } };
        disable.Click += async (_, _) => await ConfirmDisable(dev);
        mf.Items.Add(disable);

        mf.ShowAt(b);
    }

    /// <summary>Disabling a device is destructive (could disable display/disk/keyboard) — always confirm first.</summary>
    private async Task ConfirmDisable(DeviceInfo d)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Disable device?", "停用裝置？"),
            Content = $"{d.Display}\n\n" + P("Disabling the wrong device (display, disk, keyboard) can make the PC unusable until re-enabled.",
                "停用錯嘅裝置（顯示、磁碟、鍵盤）可能令部機用唔到，要重新啟用先得返。"),
            PrimaryButtonText = P("Disable", "停用"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            await Run(d, DeviceManager.Disable, P("Disable", "停用"));
    }

    private async Task Run(DeviceInfo? dev, Func<DeviceInfo, CancellationToken, Task<TweakResult>> op, string verb)
    {
        if (dev is null || _busy) return;
        _busy = true;
        try
        {
            var r = await op(dev, CancellationToken.None);
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = needAdmin
                ? P($"{verb} '{dev.Display}' needs administrator rights.", $"{verb}「{dev.Display}」需要管理員權限。")
                : $"{verb} '{dev.Display}' — {(r.Success ? "OK" : (r.Output ?? ""))}";
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        await Reload();
    }
}
