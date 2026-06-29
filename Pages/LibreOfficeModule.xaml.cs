using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// LibreOffice 模組 · Document Converter: wraps headless soffice for batch document conversion
/// (--headless --convert-to) plus "Open in LibreOffice" and app launchers. 全程留喺 app 入面。
/// </summary>
public sealed partial class LibreOfficeModule : Page
{
    private readonly ObservableCollection<FileRow> _rows = new();
    private List<TweakDefinition>? _ops;
    private CancellationTokenSource? _cts;
    private string? _outDirOverride;
    private bool _busy;

    public LibreOfficeModule()
    {
        InitializeComponent();
        FileList.ItemsSource = _rows;
        _rows.CollectionChanged += (_, _) => UpdateEmptyHint();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += (_, _) => { BuildFormats(); Render(); PopulateOps(string.Empty); UpdateEmptyHint(); };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateOps(OpsFilter.Text ?? string.Empty); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildFormats()
    {
        if (FormatBox.Items.Count > 0) return;
        foreach (var f in LibreOfficeService.Formats)
            FormatBox.Items.Add(new ComboBoxItem { Content = $"{P(f.En, f.Zh)} · .{f.Ext}", Tag = f });
        FormatBox.SelectedIndex = 0;
    }

    private LibreOfficeService.TargetFormat CurrentFormat =>
        (FormatBox.SelectedItem as ComboBoxItem)?.Tag as LibreOfficeService.TargetFormat
        ?? LibreOfficeService.Formats[0];

    private void Render()
    {
        Header.Title = "Document Converter · 文件轉換器";
        HeaderBlurb.Text = P("Batch-convert documents with LibreOffice (headless soffice) — pick a target format, choose an output folder, and convert with per-file status. Also opens files for editing.",
            "用 LibreOffice（無介面 soffice）批次轉換文件 — 揀目標格式、選輸出資料夾，逐個檔案顯示狀態。亦可開檔編輯。");

        FilesLabel.Text = P("Files to convert", "要轉換嘅檔案");
        AddFilesBtn.Content = P("Add files…", "加入檔案…");
        AddFolderBtn.Content = P("Add folder…", "加入資料夾…");
        ClearBtn.Content = P("Clear", "清空");
        EmptyHint.Text = P("No files yet. Add documents to convert (DOCX, XLSX, PPTX, ODT, PDF…).",
            "未有檔案。加入要轉換嘅文件（DOCX、XLSX、PPTX、ODT、PDF…）。");

        ConvertLabel.Text = P("Conversion", "轉換");
        FmtCap.Text = P("Target format", "目標格式");
        FilterCap.Text = P("Filter override", "篩選器覆寫");
        FilterBox.PlaceholderText = P("Optional LibreOffice filter name…", "可選 LibreOffice 篩選器名稱…");
        OutCap.Text = P("Output folder", "輸出資料夾");
        OutDirBtn.Content = P("Browse…", "瀏覽…");
        OutDirResetBtn.Content = P("Use source", "用來源");
        RecurseCheck.Content = P("Recurse subfolders when adding a folder", "加入資料夾時包含子資料夾");

        ConvertBtn.Content = P("Convert", "開始轉換");
        CancelBtn.Content = P("Cancel", "取消");
        OpenSelectedBtn.Content = P("Open first file in LibreOffice", "喺 LibreOffice 開第一個檔案");
        OpenOutDirBtn.Content = P("Open output folder", "開啟輸出資料夾");

        LogLabel.Text = P("Log", "記錄");
        ClearLogBtn.Content = P("Clear log", "清除記錄");
        OpsHeader.Text = P("LibreOffice tools", "LibreOffice 工具");
        OpsFilter.PlaceholderText = P("Filter tools…", "篩選工具…");

        RefreshOutDirText();
        UpdateProgressText();

        if (!LibreOfficeService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("LibreOffice not found", "搵唔到 LibreOffice");
            EngineBar.Message = P("Click to install LibreOffice automatically (winget) — no restart needed.",
                "撳一下自動安裝 LibreOffice（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "TheDocumentFoundation.LibreOffice", "Install LibreOffice", "安裝 LibreOffice",
                () => { Render(); return Task.CompletedTask; }, LibreOfficeService.Rescan);
            ConvertBtn.IsEnabled = false;
        }
        else
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
            ConvertBtn.IsEnabled = !_busy;
        }
    }

    private void UpdateEmptyHint()
    {
        EmptyHint.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FileList.Visibility = _rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    // ===== file list =====

    private void AddRow(string path)
    {
        if (_rows.Any(r => string.Equals(r.SourcePath, path, StringComparison.OrdinalIgnoreCase))) return;
        long size = 0;
        try { size = new FileInfo(path).Length; } catch { }
        _rows.Add(new FileRow(new LibreOfficeService.ConvertItem { SourcePath = path, SizeBytes = size }));
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var filters = new List<FileDialogs.Filter>
        {
            new("Documents", string.Join(";", LibreOfficeService.SourceExtensions.Select(x => "*" + x))),
            new("All files", "*.*"),
        };
        var files = await FileDialogs.OpenFilesAsync(filters, P("Add files to convert", "加入要轉換嘅檔案"));
        foreach (var f in files) AddRow(f);
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = await FileDialogs.OpenFolderAsync(P("Add a folder of documents", "加入文件資料夾"));
        if (dir is null) return;
        try
        {
            var opt = RecurseCheck.IsChecked == true ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var exts = new HashSet<string>(LibreOfficeService.SourceExtensions, StringComparer.OrdinalIgnoreCase);
            foreach (var f in Directory.EnumerateFiles(dir, "*", opt))
                if (exts.Contains(Path.GetExtension(f))) AddRow(f);
        }
        catch (Exception ex) { AppendLog(ex.Message); }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string path)
        {
            var row = _rows.FirstOrDefault(r => r.SourcePath == path);
            if (row is not null) _rows.Remove(row);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _rows.Clear();

    // ===== output folder =====

    private void RefreshOutDirText()
    {
        OutDirBox.Text = _outDirOverride ?? P("(same folder as each source file)", "（與每個來源檔案同一資料夾）");
    }

    private async void PickOutDir_Click(object sender, RoutedEventArgs e)
    {
        var dir = await FileDialogs.OpenFolderAsync(P("Choose output folder", "選擇輸出資料夾"));
        if (dir is not null) { _outDirOverride = dir; RefreshOutDirText(); }
    }

    private void ResetOutDir_Click(object sender, RoutedEventArgs e) { _outDirOverride = null; RefreshOutDirText(); }

    private string OutDirFor(LibreOfficeService.ConvertItem item)
        => _outDirOverride ?? (Path.GetDirectoryName(item.SourcePath) ?? Directory.GetCurrentDirectory());

    private void FormatBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FilterBox is null) return;
        var f = CurrentFormat;
        FilterBox.Text = f.Filter ?? string.Empty;
    }

    // ===== convert =====

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!LibreOfficeService.IsInstalled) { Render(); return; }
        if (_rows.Count == 0) { AppendLog(P("Add at least one file first.", "請先加入至少一個檔案。")); return; }

        _busy = true;
        _cts = new CancellationTokenSource();
        ConvertBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        AddFilesBtn.IsEnabled = AddFolderBtn.IsEnabled = ClearBtn.IsEnabled = false;

        var fmt = CurrentFormat;
        var filterOverride = string.IsNullOrWhiteSpace(FilterBox.Text) ? null : FilterBox.Text.Trim();
        var items = _rows.Select(r => r.Item).ToList();

        // reset states
        foreach (var r in _rows) { r.Item.State = LibreOfficeService.ConvertState.Queued; r.Item.Detail = null; r.Refresh(); }
        int total = items.Count, done = 0;
        Progress.Maximum = total; Progress.Value = 0;
        UpdateProgressText();
        AppendLog(P($"Converting {total} file(s) → .{fmt.Ext}…", $"轉換緊 {total} 個檔案 → .{fmt.Ext}…"));

        try
        {
            await LibreOfficeService.ConvertBatch(items, fmt, OutDirFor, filterOverride,
                onItemChanged: item =>
                {
                    var row = _rows.FirstOrDefault(r => r.Item == item);
                    row?.Refresh();
                    if (item.State is LibreOfficeService.ConvertState.Done or LibreOfficeService.ConvertState.Failed)
                    {
                        done++;
                        Progress.Value = done;
                        UpdateProgressText();
                    }
                },
                onLog: AppendLog,
                ct: _cts.Token);

            int ok = _rows.Count(r => r.Item.State == LibreOfficeService.ConvertState.Done);
            int fail = _rows.Count(r => r.Item.State == LibreOfficeService.ConvertState.Failed);
            AppendLog(P($"Finished: {ok} succeeded, {fail} failed.", $"完成：{ok} 個成功，{fail} 個失敗。"));
        }
        catch (Exception ex) { AppendLog(ex.Message); }
        finally
        {
            _busy = false;
            _cts?.Dispose();
            _cts = null;
            CancelBtn.IsEnabled = false;
            AddFilesBtn.IsEnabled = AddFolderBtn.IsEnabled = ClearBtn.IsEnabled = true;
            ConvertBtn.IsEnabled = LibreOfficeService.IsInstalled;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        CancelBtn.IsEnabled = false;
        AppendLog(P("Cancelling… (kill stray soffice from the tools below if it hangs)",
            "取消緊…（如果卡住，用下面嘅工具結束殘留 soffice）"));
    }

    private void UpdateProgressText()
    {
        int done = _rows.Count(r => r.Item.State is LibreOfficeService.ConvertState.Done or LibreOfficeService.ConvertState.Failed);
        ProgressText.Text = $"{done} / {_rows.Count}";
    }

    private TweakResult Notify(TweakResult r)
    {
        AppendLog((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? (r.Success ? "OK" : "Failed"));
        return r;
    }

    private void OpenSelected_Click(object sender, RoutedEventArgs e)
    {
        var first = _rows.FirstOrDefault();
        // Prefer a produced output file if one exists; otherwise open the source for editing.
        var target = first?.Item.OutputPath is { } op && File.Exists(op) ? op : first?.SourcePath;
        if (target is null) { AppendLog(P("No file to open.", "冇檔案可以開。")); return; }
        Notify(LibreOfficeService.OpenForEditing(target));
    }

    private void OpenOutFolder_Click(object sender, RoutedEventArgs e)
    {
        string? dir = _outDirOverride;
        if (dir is null)
        {
            var first = _rows.FirstOrDefault();
            if (first?.Item.OutputPath is { } op) dir = Path.GetDirectoryName(op);
            else if (first is not null) dir = Path.GetDirectoryName(first.SourcePath);
        }
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { AppendLog(P("No output folder yet.", "未有輸出資料夾。")); return; }
        try { Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true }); }
        catch (Exception ex) { AppendLog(ex.Message); }
    }

    // ===== log =====

    private void AppendLog(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        void Do()
        {
            LogText.Text += (LogText.Text.Length > 0 ? "\n" : "") + text;
            LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
        }
        if (DispatcherQueue.HasThreadAccess) Do();
        else DispatcherQueue.TryEnqueue(Do);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogText.Text = "";

    // ===== ops =====

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= LibreOfficeOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }

    /// <summary>畀 ListView 用嘅一行（INotifyPropertyChanged，狀態變即時更新）· Bindable file row.</summary>
    public sealed class FileRow : INotifyPropertyChanged
    {
        public LibreOfficeService.ConvertItem Item { get; }
        public FileRow(LibreOfficeService.ConvertItem item) { Item = item; }

        public string SourcePath => Item.SourcePath;
        public string FileName => Item.FileName;

        public string SubText
        {
            get
            {
                var size = HumanSize(Item.SizeBytes);
                if (Item.State == LibreOfficeService.ConvertState.Done && Item.OutputPath is not null)
                    return $"{size}  →  {Item.OutputPath}";
                if (Item.State == LibreOfficeService.ConvertState.Failed && Item.Detail is not null)
                    return $"{size}  ·  {Item.Detail}";
                return size;
            }
        }

        public string StateText => Item.State switch
        {
            LibreOfficeService.ConvertState.Queued => Loc.I.Pick("Queued", "排隊中"),
            LibreOfficeService.ConvertState.Converting => Loc.I.Pick("Converting…", "轉換中…"),
            LibreOfficeService.ConvertState.Done => Loc.I.Pick("Done", "完成"),
            LibreOfficeService.ConvertState.Failed => Loc.I.Pick("Failed", "失敗"),
            _ => "",
        };

        public string StateGlyph => ((char)(Item.State switch
        {
            LibreOfficeService.ConvertState.Queued => 0xE823,
            LibreOfficeService.ConvertState.Converting => 0xE895,
            LibreOfficeService.ConvertState.Done => 0xE73E,
            LibreOfficeService.ConvertState.Failed => 0xEA39,
            _ => 0xE8A5,
        })).ToString();

        public Brush StateBrush => new SolidColorBrush(Item.State switch
        {
            LibreOfficeService.ConvertState.Done => Colors.SeaGreen,
            LibreOfficeService.ConvertState.Failed => Colors.IndianRed,
            LibreOfficeService.ConvertState.Converting => Colors.DodgerBlue,
            _ => Colors.Gray,
        });

        public void Refresh()
        {
            OnPropertyChanged(nameof(SubText));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(StateGlyph));
            OnPropertyChanged(nameof(StateBrush));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        private static string HumanSize(long bytes)
        {
            if (bytes <= 0) return "0 B";
            string[] u = { "B", "KB", "MB", "GB" };
            double v = bytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return $"{v:0.#} {u[i]}";
        }
    }
}
