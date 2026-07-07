using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內 OneDrive 檔案隨選控制 · In-app OneDrive Files-On-Demand control. Pick a OneDrive folder,
/// then pin (always-local) or dehydrate (online-only) files/folders, pause/resume sync, and set the
/// auto-free (auto-dehydration) age threshold. All in-app, no redirect. Bilingual.
/// </summary>
public sealed partial class OneDriveModule : Page
{
    public sealed class Row
    {
        public string Path { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsFolder { get; init; }
        public string Glyph { get; init; } = "";
        public string SubText { get; init; } = "";
        public string SizeText { get; init; } = "";
        public string StateText { get; init; } = "";
        public Brush StateBrush { get; init; } = null!;
    }

    private string _currentFolder = "";
    private bool _busy;

    public OneDriveModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            Render();
            _currentFolder = OneDriveService.DefaultRoot() ?? "";
            LoadThreshold();
            Reload();
        };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) { Render(); Reload(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "OneDrive · OneDrive";
        HeaderBlurb.Text = P(
            "Control OneDrive Files-On-Demand. Pin items to keep them always on this PC, or dehydrate them to free space (online-only). Pause sync, and set how many days until unused files are auto-freed.",
            "控制 OneDrive 檔案隨選。釘選項目令佢哋永遠留喺呢部電腦，或者脫水變返只在雲端嚟釋放空間。可以暫停同步，仲可以設定幾多日後自動釋放未用嘅檔案。");

        PickBtn.Content = P("Pick folder…", "揀資料夾…");
        RefreshBtn.Content = P("Refresh", "重新整理");

        PinBtnText.Text = P("Pin (always local)", "釘選（永遠本機）");
        DehydrateBtnText.Text = P("Free space (online-only)", "釋放空間（只在雲端）");
        SelectAllBtn.Content = P("Select all", "全選");
        ClearSelBtn.Content = P("Clear", "清除");

        PauseBtnText.Text = P("Pause sync", "暫停同步");
        ResumeBtnText.Text = P("Resume sync", "回復同步");
        ThresholdLabel.Text = P("Auto-free after (days, 0 = off)", "幾多日後自動釋放（0 = 關閉）");
        ApplyThresholdBtn.Content = P("Apply", "套用");

        EmptyPickBtn.Content = P("Pick a OneDrive folder…", "揀一個 OneDrive 資料夾…");

        UpdateSelCount();
    }

    private void LoadThreshold()
    {
        var days = OneDriveService.GetDehydrationThresholdDays();
        ThresholdBox.Value = days ?? 0;
    }

    private void Reload()
    {
        if (string.IsNullOrWhiteSpace(_currentFolder) || !Directory.Exists(_currentFolder))
        {
            PathBox.Text = _currentFolder;
            List.ItemsSource = null;
            EmptyState.Visibility = Visibility.Visible;
            EmptyText.Text = string.IsNullOrWhiteSpace(_currentFolder)
                ? P("No OneDrive folder detected. Pick a folder to manage its files.",
                    "偵測唔到 OneDrive 資料夾。揀一個資料夾嚟管理入面嘅檔案。")
                : P("This folder is empty or unreadable.", "呢個資料夾係空嘅或者讀唔到。");
            return;
        }

        PathBox.Text = _currentFolder;
        var rows = new List<Row>();
        foreach (var e in OneDriveService.List(_currentFolder))
        {
            string glyph = e.IsFolder ? "" : "";
            string state;
            Brush brush;
            if (e.IsOnlineOnly)
            {
                state = P("Online-only", "只在雲端");
                brush = Brush("SystemFillColorCautionBrush");
            }
            else if (e.IsPinned)
            {
                state = P("Always local", "永遠本機");
                brush = Brush("SystemFillColorSuccessBrush");
            }
            else
            {
                state = P("On-demand", "隨選");
                brush = Brush("SystemFillColorNeutralBrush");
            }

            rows.Add(new Row
            {
                Path = e.Path,
                Name = e.Name,
                IsFolder = e.IsFolder,
                Glyph = glyph,
                SubText = e.IsFolder ? P("Folder", "資料夾") : P("File", "檔案"),
                SizeText = e.IsFolder ? "" : OneDriveService.HumanSize(e.Size),
                StateText = state,
                StateBrush = brush,
            });
        }

        List.ItemsSource = rows;
        EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (rows.Count == 0)
            EmptyText.Text = P("This folder is empty.", "呢個資料夾係空嘅。");
        UpdateSelCount();
    }

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFolder)) return;
        var parent = Directory.GetParent(_currentFolder.TrimEnd('\\', '/'));
        if (parent is not null)
        {
            _currentFolder = parent.FullName;
            Reload();
        }
    }

    private async void Pick_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Pick a OneDrive folder", "揀一個 OneDrive 資料夾"));
        if (!string.IsNullOrWhiteSpace(folder))
        {
            _currentFolder = folder;
            Reload();
        }
    }

    private void List_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if ((e.OriginalSource as FrameworkElement)?.DataContext is Row row && row.IsFolder && Directory.Exists(row.Path))
        {
            _currentFolder = row.Path;
            Reload();
        }
    }

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateSelCount();

    private void UpdateSelCount()
    {
        int n = List?.SelectedItems?.Count ?? 0;
        SelCountText.Text = n == 0 ? "" : P($"{n} selected", $"已揀 {n} 項");
        bool any = n > 0;
        PinBtn.IsEnabled = any;
        DehydrateBtn.IsEnabled = any;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => List.SelectAll();
    private void ClearSel_Click(object sender, RoutedEventArgs e) => List.SelectedItems.Clear();

    private List<string> SelectedPaths()
        => List.SelectedItems.OfType<Row>().Select(r => r.Path).ToList();

    private async void Pin_Click(object sender, RoutedEventArgs e)
    {
        var paths = SelectedPaths();
        if (paths.Count == 0) return;
        await RunOnEach(paths, OneDriveService.Pin, P("Pin", "釘選"));
    }

    private async void Dehydrate_Click(object sender, RoutedEventArgs e)
    {
        var paths = SelectedPaths();
        if (paths.Count == 0) return;
        await RunOnEach(paths, OneDriveService.Dehydrate, P("Free space", "釋放空間"));
    }

    private async Task RunOnEach(List<string> paths, Func<string, System.Threading.CancellationToken, Task<TweakResult>> op, string verb)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            int ok = 0, fail = 0;
            string? lastErr = null;
            foreach (var p in paths)
            {
                var r = await op(p, default);
                if (r.Success) ok++;
                else { fail++; lastErr = r.Output ?? (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En); }
            }
            ResultBar.Severity = fail == 0 ? InfoBarSeverity.Success : (ok == 0 ? InfoBarSeverity.Error : InfoBarSeverity.Warning);
            ResultBar.Title = fail == 0 ? P("Done", "完成") : P("Partial", "部分完成");
            ResultBar.Message = P($"{verb}: {ok} succeeded, {fail} failed.", $"{verb}：成功 {ok} 項，失敗 {fail} 項。")
                + (lastErr is null ? "" : $"\n{lastErr}");
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
        Reload();
    }

    private async void Pause_Click(object sender, RoutedEventArgs e)
        => await RunSimple(() => OneDriveService.PauseSync(), P("Pause sync", "暫停同步"));

    private async void Resume_Click(object sender, RoutedEventArgs e)
        => await RunSimple(() => OneDriveService.ResumeSync(), P("Resume sync", "回復同步"));

    private async Task RunSimple(Func<Task<TweakResult>> op, string verb)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var r = await op();
            ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
            ResultBar.Message = r.Success
                ? P($"{verb} done.", $"{verb}完成。")
                : (r.Output ?? (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");
            ResultBar.IsOpen = true;
        }
        finally { _busy = false; }
    }

    private void ApplyThreshold_Click(object sender, RoutedEventArgs e)
    {
        int days = (int)Math.Round(double.IsNaN(ThresholdBox.Value) ? 0 : ThresholdBox.Value);
        var r = OneDriveService.SetDehydrationThresholdDays(days);
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        ResultBar.IsOpen = true;
    }
}
