using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內磁碟機模組 · In-app Drives — overview with used/free bars, plus mount/dismount disk
/// images and create VHDs. Pure C# list + native cmdlets, no redirect. Bilingual.
/// </summary>
public sealed partial class DrivesModule : Page
{
    public sealed class Row
    {
        public string Glyph { get; init; } = "";
        public string Title { get; init; } = "";
        public string SubText { get; init; } = "";
        public string SizeText { get; init; } = "";
        public double UsedBarWidth { get; init; }
        public Brush BarBrush { get; init; } = null!;
    }

    private const double BarTrack = 300;
    private bool _busy;

    public DrivesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); Reload(); };
        Loaded += (_, _) => { Render(); Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Drives · 磁碟機";
        HeaderBlurb.Text = P("See used/free space on every drive at a glance, mount or eject ISO/VHD images, and create a new VHD.",
            "一眼睇晒每個磁碟機嘅已用／可用空間，掛載或退出 ISO/VHD 映像，仲可以整新 VHD。");
        RefreshBtn.Content = P("Refresh", "重新整理");
        MountBtn.Content = P("Mount image…", "掛載映像…");
        DismountBtn.Content = P("Dismount image…", "卸載映像…");
        CreateBtn.Content = P("Create VHD…", "建立 VHD…");
    }

    private void Reload()
    {
        var rows = new List<Row>();
        foreach (var d in DriveService.List())
        {
            string glyph = d.Type switch
            {
                "CDRom" => "",
                "Removable" => "",
                "Network" => "",
                _ => "",
            };
            if (!d.Ready)
            {
                rows.Add(new Row { Glyph = glyph, Title = d.Name, SubText = $"{d.Type} · {P("not ready", "未就緒")}", SizeText = "", UsedBarWidth = 0, BarBrush = Brush("TextFillColorTertiaryBrush") });
                continue;
            }
            var label = string.IsNullOrWhiteSpace(d.Label) ? P("Local Disk", "本機磁碟") : d.Label;
            rows.Add(new Row
            {
                Glyph = glyph,
                Title = $"{d.Name}  {label}",
                SubText = $"{d.Format} · {d.Type} · {Math.Round(d.UsedPercent)}% " + P("used", "已用"),
                SizeText = $"{DriveService.HumanSize(d.Free)} {P("free of", "可用 /")} {DriveService.HumanSize(d.Total)}",
                UsedBarWidth = BarTrack * d.UsedPercent / 100.0,
                BarBrush = d.UsedPercent >= 90 ? Brush("SystemFillColorCriticalBrush") : Brush("AccentFillColorDefaultBrush"),
            });
        }
        List.ItemsSource = rows;
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private async Task<string?> PickImage()
    {
        return await FileDialogs.OpenFileAsync(".iso", ".vhd", ".vhdx", ".img");
    }

    private async void Mount_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickImage();
        if (path is not null) await Run(() => DriveService.MountImage(path), P("Mount", "掛載"));
    }

    private async void Dismount_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickImage();
        if (path is not null) await Run(() => DriveService.DismountImage(path), P("Dismount", "卸載"));
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        var savePath = await FileDialogs.SaveFileAsync("disk", ".vhdx", ".vhd");
        if (savePath is null) return;

        var size = new NumberBox { Header = P("Size (GB)", "大細 (GB)"), Value = 10, Minimum = 1, Maximum = 65536, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var dyn = new CheckBox { Content = P("Dynamically expanding", "動態擴充"), IsChecked = true };
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(size);
        panel.Children.Add(dyn);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Create VHD", "建立 VHD"),
            Content = panel,
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        await Run(() => DriveService.CreateVhd(savePath, (int)size.Value, dyn.IsChecked == true), P("Create VHD", "建立 VHD"));
    }

    private async Task Run(Func<Task<Models.TweakResult>> op, string verb)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await op();
            bool needAdmin = !r.Success && !AdminHelper.IsElevated;
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = needAdmin
                ? P($"{verb} may need administrator rights.", $"{verb}可能需要管理員權限。")
                : (r.Output ?? (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        Reload();
    }
}
