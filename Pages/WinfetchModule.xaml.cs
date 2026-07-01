using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 winfetch · A native winfetch clone — shows OS, host, kernel, uptime, packages, shell, resolution,
/// desktop/theme, CPU, GPU(s), memory, disks and battery beside a coloured Windows logo. 兩種顯示模式：
/// 圖形面板同經典 ASCII 主控台輸出，可複製或匯出 · two render modes (a styled UI panel and the classic
/// ASCII console output), copy-to-clipboard and export-to-text. Bilingual throughout.
/// </summary>
public sealed partial class WinfetchModule : Page
{
    private FetchSnapshot? _snap;
    private CancellationTokenSource? _cts;

    // 每實例富文字工具列（格式只影響呢個 ASCII 面）· per-instance rich-text toolbar for the ASCII surface.
    private RichTextToolbar? _asciiToolbar;

    // Windows-console accent used to colour the info-row titles.
    private static readonly Color Accent = Color.FromArgb(0xFF, 0x3A, 0x96, 0xDD);

    public WinfetchModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) => { RenderText(); await ReloadAsync(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; _cts?.Cancel(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        if (_snap is not null) RenderSnapshot(_snap);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void RenderText()
    {
        Header.Title = "System Info · 系統資訊 (Winfetch)";
        HeaderBlurb.Text = P(
            "A native winfetch — your machine at a glance beside the Windows logo. Toggle ASCII for the classic console look, then copy or export.",
            "原生版 winfetch — 喺 Windows 標誌旁邊一眼睇晒部機嘅資料。撳 ASCII 切換經典主控台外觀，再複製或匯出。");
        RefreshLabel.Text = P("Refresh", "重新整理");
        CopyLabel.Text = P("Copy", "複製");
        ExportLabel.Text = P("Export…", "匯出…");
    }

    private async Task ReloadAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        StatusBar.IsOpen = true;
        StatusBar.Severity = InfoBarSeverity.Informational;
        StatusBar.Message = P("Gathering system information…", "正在收集系統資訊…");
        RefreshBtn.IsEnabled = false;

        try
        {
            var snap = await WinfetchService.CollectAsync((en, zh) => Loc.I.Pick(en, zh), ct);
            if (ct.IsCancellationRequested) return;
            _snap = snap;
            RenderSnapshot(snap);
            StatusBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Message = P($"Failed to read system info: {ex.Message}", $"讀取系統資訊失敗：{ex.Message}");
            StatusBar.IsOpen = true;
        }
        finally
        {
            RefreshBtn.IsEnabled = true;
        }
    }

    private void RenderSnapshot(FetchSnapshot snap)
    {
        TitleLine.Text = $"{snap.User}@{snap.Host}";
        DashLine.Text = new string('-', snap.User.Length + snap.Host.Length + 1);

        RowsHost.Items.Clear();
        foreach (var r in snap.Rows)
            RowsHost.Items.Add(BuildRow(r));

        if (AsciiToggle.IsOn)
            AsciiText.Text = WinfetchService.ToAsciiText(snap, !Loc.I.IsCantonesePrimary);
    }

    private FrameworkElement BuildRow(FetchRow r)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var title = Loc.I.IsCantonesePrimary && !string.IsNullOrEmpty(r.TitleZh) ? r.TitleZh : r.TitleEn;
        var titleBlock = new TextBlock
        {
            Text = title,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Accent),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(titleBlock, 0);
        grid.Children.Add(titleBlock);

        if (r.Percent >= 0)
        {
            // value + a small usage bar
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
            panel.Children.Add(new TextBlock
            {
                Text = r.Value,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            });
            panel.Children.Add(BuildBar(r.Percent));
            Grid.SetColumn(panel, 1);
            grid.Children.Add(panel);
        }
        else
        {
            var valueBlock = new TextBlock
            {
                Text = r.Value,
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true,
            };
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);
        }

        return grid;
    }

    private FrameworkElement BuildBar(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        const double track = 110, height = 8;
        var host = new Grid { Width = track, Height = height, VerticalAlignment = VerticalAlignment.Center };

        var bg = new Border
        {
            Width = track,
            Height = height,
            CornerRadius = new CornerRadius(4),
            Opacity = 0.25,
            Background = (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"],
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        host.Children.Add(bg);

        // green ≤60, yellow ≤80, red otherwise — matching winfetch's bar colours.
        Color barColor = percent <= 60
            ? Color.FromArgb(0xFF, 0x13, 0xA1, 0x0E)
            : percent <= 80
                ? Color.FromArgb(0xFF, 0xC1, 0x9C, 0x00)
                : Color.FromArgb(0xFF, 0xC5, 0x0F, 0x1F);

        var fill = new Border
        {
            Width = track * percent / 100.0,
            Height = height,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(barColor),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        host.Children.Add(fill);
        return host;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private void Ascii_Toggled(object sender, RoutedEventArgs e)
    {
        bool ascii = AsciiToggle.IsOn;
        AsciiHost.Visibility = ascii ? Visibility.Visible : Visibility.Collapsed;
        UiScroller.Visibility = ascii ? Visibility.Collapsed : Visibility.Visible;
        if (ascii)
        {
            EnsureAsciiToolbar();
            if (_snap is not null)
                AsciiText.Text = WinfetchService.ToAsciiText(_snap, !Loc.I.IsCantonesePrimary);
        }
    }

    /// <summary>第一次切去 ASCII 模式時，建立自己嘅富文字工具列（主題只影響 AsciiHost）· lazily build a per-instance toolbar.</summary>
    private void EnsureAsciiToolbar()
    {
        if (_asciiToolbar is not null) return;
        try
        {
            _asciiToolbar = new RichTextToolbar(AsciiText, RichTextToolbar.Mode.Editable, themeScope: AsciiHost);
            AsciiToolbarHost.Content = _asciiToolbar;
        }
        catch (Exception ex) { CrashLogger.Log("winfetch:toolbar", ex); }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_snap is null) return;
        var text = AsciiToggle.IsOn
            ? WinfetchService.ToAsciiText(_snap, !Loc.I.IsCantonesePrimary)
            : WinfetchService.ToPlainText(_snap, !Loc.I.IsCantonesePrimary);
        try
        {
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Message = P("Copied to clipboard.", "已複製到剪貼簿。");
            StatusBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Message = P($"Copy failed: {ex.Message}", $"複製失敗：{ex.Message}");
            StatusBar.IsOpen = true;
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_snap is null) return;
        try
        {
            var path = await FileDialogs.SaveFileAsync("winfetch.txt", ".txt");
            if (string.IsNullOrEmpty(path)) return;
            var text = WinfetchService.ToAsciiText(_snap, !Loc.I.IsCantonesePrimary);
            await System.IO.File.WriteAllTextAsync(path, text);
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Message = P($"Exported to {path}", $"已匯出至 {path}");
            StatusBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusBar.Severity = InfoBarSeverity.Error;
            StatusBar.Message = P($"Export failed: {ex.Message}", $"匯出失敗：{ex.Message}");
            StatusBar.IsOpen = true;
        }
    }
}
