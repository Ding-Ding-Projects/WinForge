using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內磁碟分析 · In-app disk-usage analyser (folder sizes with %-bars + largest files),
/// drill-in, recycle. Pure C#, off-UI-thread, no redirect. Bilingual.
/// </summary>
public sealed partial class DiskAnalyzerModule : Page
{
    public sealed class Row
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string SizeText { get; init; } = "";
        public string Percent { get; init; } = "";
        public bool IsDir { get; init; }
        public double BarWidth { get; init; }
        public string Glyph => IsDir ? "" : "";
    }

    private string _folder = "";
    private bool _busy;
    private const double MaxBar = 240;

    public DiskAnalyzerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            if (string.IsNullOrEmpty(_folder)) { _folder = AppContext.BaseDirectory; FolderBox.Text = _folder; }
            await DoScan();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Disk Analyser · 磁碟分析";
        HeaderBlurb.Text = P("See what's using space: size of each folder/file with bars, click a folder to drill in.",
            "睇下啲空間用咗喺邊：每個資料夾／檔案嘅大細連長條，撳資料夾入去再睇。");
        BrowseBtn.Content = P("Browse…", "瀏覽…");
        ScanBtn.Content = P("Analyse", "分析");
        RecycleBtn.Content = P("Recycle selected", "回收選取");

        var sel = ModeBox.SelectedIndex < 0 ? 0 : ModeBox.SelectedIndex;
        ModeBox.Items.Clear();
        ModeBox.Items.Add(new ComboBoxItem { Content = P("By folder", "按資料夾") });
        ModeBox.Items.Add(new ComboBoxItem { Content = P("Largest files", "最大檔案") });
        ModeBox.SelectedIndex = sel;
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) { _folder = folder; FolderBox.Text = _folder; await DoScan(); }
    }

    /// <summary>喺目前資料夾開內嵌終端機 · Open the embedded ConPTY terminal at the current folder.</summary>
    private async void Terminal_Click(object sender, RoutedEventArgs e)
    {
        var dir = string.IsNullOrWhiteSpace(_folder) || !Directory.Exists(_folder)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : _folder;
        await TerminalLauncher.OpenEmbeddedAsync(this.XamlRoot,
            P($"Terminal · {System.IO.Path.GetFileName(dir.TrimEnd('\\', '/'))}",
              $"終端機 · {System.IO.Path.GetFileName(dir.TrimEnd('\\', '/'))}"),
            commandLine: null, workingDir: dir);
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await DoScan();
    private async void Mode_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) await DoScan(); }

    private async void Up_Click(object sender, RoutedEventArgs e)
    {
        var parent = Directory.GetParent(_folder.TrimEnd('\\'));
        if (parent is not null) { _folder = parent.FullName; FolderBox.Text = _folder; await DoScan(); }
    }

    private async void List_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Row r && r.IsDir && Directory.Exists(r.Path))
        {
            _folder = r.Path; FolderBox.Text = _folder; await DoScan();
        }
    }

    private async Task DoScan()
    {
        if (_busy || string.IsNullOrEmpty(_folder)) return;
        _busy = true;
        ScanBtn.IsEnabled = false;
        StatusText.Text = P("Analysing…", "分析緊…");
        List.ItemsSource = null;

        bool largest = ModeBox.SelectedIndex == 1;
        var progress = new Progress<string>(s => StatusText.Text = largest ? P($"Scanning… {s}", $"掃描緊… {s}") : P($"Sizing… {s}", $"計算緊… {s}"));

        List<DiskEntry> entries;
        try
        {
            entries = await Task.Run(() => largest
                ? DiskAnalyzer.LargestFiles(_folder, 200, progress, CancellationToken.None)
                : DiskAnalyzer.ByChild(_folder, progress, CancellationToken.None));
        }
        catch (Exception ex) { StatusText.Text = ex.Message; _busy = false; ScanBtn.IsEnabled = true; return; }

        long max = entries.Count > 0 ? Math.Max(1, entries[0].Size) : 1;
        long total = entries.Sum(x => x.Size);
        var rows = entries.Select(en => new Row
        {
            Name = en.Name,
            Path = en.Path,
            SizeText = DiskAnalyzer.HumanSize(en.Size),
            Percent = total > 0 ? $"{Math.Round(en.Size * 100.0 / total)}%" : "",
            IsDir = en.IsDir,
            BarWidth = MaxBar * en.Size / max,
        }).ToList();

        List.ItemsSource = rows;
        StatusText.Text = P($"{rows.Count} items · {DiskAnalyzer.HumanSize(total)} total", $"{rows.Count} 項 · 合共 {DiskAnalyzer.HumanSize(total)}");
        _busy = false;
        ScanBtn.IsEnabled = true;
    }

    private async void Recycle_Click(object sender, RoutedEventArgs e)
    {
        if (List.SelectedItem is not Row r) { Warn(P("Select an item first.", "請先揀一項。")); return; }
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Recycle?", "放入回收筒？"),
            Content = $"{r.Name} ({r.SizeText})\n\n" + P("Send to the Recycle Bin?", "放入回收筒？"),
            PrimaryButtonText = P("Recycle", "回收"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var (ok, fail) = BulkFileOps.Recycle(new[] { r.Path });
        ResultBar.Severity = fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = fail == 0 ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = fail == 0 ? P($"Recycled {r.Name}.", $"已回收 {r.Name}。") : P("Could not recycle (in use?).", "回收唔到（用緊？）。");
        ResultBar.IsOpen = true;
        await DoScan();
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
