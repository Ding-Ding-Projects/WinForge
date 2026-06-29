using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內字型管理 · In-app Font Manager — bulk install, live per-face preview, and uninstall.
/// Per-user install needs no admin (copies to %LOCALAPPDATA%\Microsoft\Windows\Fonts + HKCU + WM_FONTCHANGE);
/// a machine-wide variant writes to %WINDIR%\Fonts + HKLM and needs administrator rights. Bilingual.
/// </summary>
public sealed partial class FontManagerModule : Page
{
    private string _sample = "";

    public FontManagerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); RefreshList(); };
        Loaded += (_, _) => { Render(); RefreshList(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private string DefaultSample => P(
        "The quick brown fox jumps over the lazy dog 0123456789",
        "永遠都係嗰句 The quick brown fox 0123456789 香港字型測試");

    private void Render()
    {
        Header.Title = "Font Manager · 字型管理";
        HeaderBlurb.Text = P(
            "Install, preview and uninstall fonts in-app. Per-user install needs no admin (it copies to your profile's Fonts folder). The machine-wide option installs for all users and needs administrator rights.",
            "喺 app 內裝、睇同移除字型。逐個使用者安裝唔使管理員（會複製去你個人檔案嘅 Fonts 資料夾）。全機安裝係畀所有使用者用，需要管理員權限。");
        BrowseBtn.Content = P("Install fonts… (.ttf/.otf)", "安裝字型…（.ttf/.otf）");
        MachineWideCheck.Content = P("Machine-wide (admin)", "全機安裝（管理員）");
        SampleBox.PlaceholderText = P("Preview text…", "預覽文字…");
        if (string.IsNullOrEmpty(SampleBox.Text)) SampleBox.Text = DefaultSample;
        RefreshBtn.Content = P("Refresh", "重新整理");
        FootNote.Text = P(
            "Shows fonts you installed for your user account (HKCU). After installing, running apps are notified via WM_FONTCHANGE — some apps need a restart to list the new font.",
            "顯示你以使用者身分安裝嘅字型（HKCU）。裝完之後會用 WM_FONTCHANGE 通知執行緊嘅程式 — 有啲程式要重開先見到新字型。");
    }

    private void Sample_Changed(object sender, TextChangedEventArgs e)
    {
        _sample = string.IsNullOrWhiteSpace(SampleBox.Text) ? DefaultSample : SampleBox.Text;
        RefreshList();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshList();

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        bool machineWide = MachineWideCheck.IsChecked == true;
        if (machineWide && !AdminHelper.IsElevated)
        {
            Info(InfoBarSeverity.Error, P("Admin required", "需要管理員"),
                P("Machine-wide install writes to the Windows Fonts folder and HKLM — relaunch WinForge as administrator, or uncheck 'Machine-wide' to install just for you.",
                  "全機安裝要寫入 Windows 字型資料夾同 HKLM — 請以管理員身分重開 WinForge，或者唔好剔「全機安裝」改成淨係裝畀自己。"));
            return;
        }

        var files = await FileDialogs.OpenFilesAsync(".ttf", ".otf", ".ttc", ".otc", ".fon");
        if (files is null || files.Count == 0) return;

        var (installed, errors) = FontService.InstallMany(files, machineWide);

        var where = machineWide ? P("all users", "所有使用者") : P("your account", "你嘅帳戶");
        if (installed.Count > 0 && errors.Count == 0)
            Info(InfoBarSeverity.Success, P("Installed", "已安裝"),
                P($"Installed {installed.Count} font(s) for {where}: {string.Join(", ", installed.Select(f => f.Face).Distinct())}.",
                  $"已為{where}安裝 {installed.Count} 個字型：{string.Join("、", installed.Select(f => f.Face).Distinct())}。"));
        else if (installed.Count > 0)
            Info(InfoBarSeverity.Warning, P("Partly installed", "部分已安裝"),
                P($"Installed {installed.Count}, {errors.Count} failed. First error: {errors[0].error}",
                  $"已安裝 {installed.Count} 個，{errors.Count} 個失敗。第一個錯誤：{errors[0].error}"));
        else
            Info(InfoBarSeverity.Error, P("Failed", "失敗"),
                errors.Count > 0 ? errors[0].error : P("No fonts installed.", "冇安裝到字型。"));

        RefreshList();
    }

    private void RefreshList()
    {
        _sample = string.IsNullOrWhiteSpace(SampleBox.Text) ? DefaultSample : SampleBox.Text;

        List<FontEntry> fonts;
        try { fonts = FontService.ListUserFonts(); }
        catch { fonts = new List<FontEntry>(); }

        FontList.Items.Clear();

        if (fonts.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            FontList.Visibility = Visibility.Collapsed;
            EmptyState.Text = P(
                "No user-installed fonts yet.\nClick \"Install fonts…\" to add .ttf/.otf files.",
                "仲未有你自己裝嘅字型。\n撳「安裝字型…」加 .ttf/.otf 檔。");
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        FontList.Visibility = Visibility.Visible;

        foreach (var f in fonts)
            FontList.Items.Add(BuildRow(f));
    }

    private UIElement BuildRow(FontEntry f)
    {
        var root = new Grid { Padding = new Thickness(12, 10, 12, 10), ColumnSpacing = 12 };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var texts = new StackPanel { Spacing = 2 };

        var title = new TextBlock
        {
            Text = $"{f.Face}  ·  {f.Kind}",
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        texts.Children.Add(title);

        // Live preview rendered in the installed face itself.
        var preview = new TextBlock
        {
            Text = _sample,
            FontSize = 22,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 2, 0, 2),
        };
        try { preview.FontFamily = new FontFamily(f.Face); } catch { /* fall back to default */ }
        texts.Children.Add(preview);

        var path = new TextBlock
        {
            Text = f.Path,
            FontSize = 11,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        texts.Children.Add(path);

        Grid.SetColumn(texts, 0);
        root.Children.Add(texts);

        var uninstall = new Button
        {
            Content = P("Uninstall", "移除"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        uninstall.Click += (_, _) => Uninstall(f);
        Grid.SetColumn(uninstall, 1);
        root.Children.Add(uninstall);

        return new Border
        {
            Margin = new Thickness(2, 2, 2, 2),
            CornerRadius = new CornerRadius(6),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"],
            Child = root,
        };
    }

    private void Uninstall(FontEntry f)
    {
        try
        {
            FontService.Uninstall(f);
            Info(InfoBarSeverity.Success, P("Uninstalled", "已移除"),
                P($"Removed {f.Face}.", $"已移除 {f.Face}。"));
        }
        catch (UnauthorizedAccessException)
        {
            Info(InfoBarSeverity.Error, P("Failed", "失敗"),
                P("That font is locked or needs admin to remove.", "嗰個字型被鎖住或者要管理員先移到。"));
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message);
        }
        RefreshList();
    }

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
