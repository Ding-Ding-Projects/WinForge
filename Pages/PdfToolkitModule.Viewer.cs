using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using WinForge.Services;

namespace WinForge.Pages;

public sealed partial class PdfToolkitModule
{
    private WebView2? _viewerWeb;
    private Border? _viewerEmpty;
    private Border? _viewerDropOverlay;
    private StackPanel? _viewerLoading;
    private StackPanel? _viewerMetaRows;
    private StackPanel? _viewerPageRows;
    private StackPanel? _viewerSearchRows;
    private TextBox? _viewerPathBox;
    private PasswordBox? _viewerPasswordBox;
    private NumberBox? _viewerPageBox;
    private ComboBox? _viewerZoomBox;
    private TextBlock? _viewerPositionText;
    private TextBlock? _viewerStatusText;
    private TextBox? _viewerRangeBox;
    private TextBox? _viewerSearchBox;
    private CheckBox? _viewerCaseBox;
    private Border? _viewerHostFrame;
    private Border? _viewerSidePanel;
    private Button? _viewerPrevPdfBtn;
    private Button? _viewerNextPdfBtn;
    private Button? _viewerPrevPageBtn;
    private Button? _viewerNextPageBtn;
    private Button? _viewerOpenFolderBtn;
    private Button? _viewerCopyPathBtn;
    private bool _viewerWebReady;
    private bool _viewerWebConfigured;
    private bool _viewerTall;
    private bool _viewerSideVisible = true;
    private string? _viewerPath;
    private string _viewerZoom = "page-width";
    private int _viewerPage = 1;
    private int _viewerPageCount;
    private List<string> _viewerSiblings = new();
    private int _viewerSiblingIndex = -1;

    private Border BuildViewerCard()
    {
        _viewerWebReady = false;
        _viewerWebConfigured = false;

        var (card, body, result) = NewCard("", "PDF viewer workbench", "PDF 檢視工作台",
            "Open, drop, inspect and search a PDF, then save focused page/range operations without leaving the toolkit.",
            "開啟、拖放、檢查同搜尋 PDF，再直接喺工具箱另存頁面／範圍操作。");

        var shell = new Grid { ColumnSpacing = 12 };
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        shell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(360) });

        var left = BuildViewerLeftPane(result);
        var right = BuildViewerRightPane(result);
        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        shell.Children.Add(left);
        shell.Children.Add(right);

        InsertBeforeResult(body, result, shell);

        if (_viewerPath is not null && File.Exists(_viewerPath))
            _ = LoadViewerPdfAsync(_viewerPath, keepSiblings: true);
        else
            UpdateViewerState();

        return card;
    }

    private StackPanel BuildViewerLeftPane(InfoBar result)
    {
        var pane = new StackPanel { Spacing = 10 };

        var pathRow = new Grid { ColumnSpacing = 8 };
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        pathRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _viewerPathBox = new TextBox
        {
            Header = P("Current PDF · 目前 PDF", "目前 PDF"),
            IsReadOnly = true,
            PlaceholderText = P("Open a PDF or drop one into the viewer.", "開啟 PDF，或者拖入檢視器。"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(_viewerPathBox, 0);
        pathRow.Children.Add(_viewerPathBox);

        var open = ButtonWithIcon("", P("Open PDF… · 開 PDF…", "開 PDF…"), accent: true);
        open.VerticalAlignment = VerticalAlignment.Bottom;
        open.Click += async (_, _) => await PickViewerPdfAsync();
        Grid.SetColumn(open, 1);
        pathRow.Children.Add(open);

        _viewerPrevPdfBtn = IconOnlyButton("", P("Previous PDF in folder · 資料夾上一個 PDF", "資料夾上一個 PDF"));
        _viewerPrevPdfBtn.VerticalAlignment = VerticalAlignment.Bottom;
        _viewerPrevPdfBtn.Click += async (_, _) => await MoveViewerSiblingAsync(-1);
        Grid.SetColumn(_viewerPrevPdfBtn, 2);
        pathRow.Children.Add(_viewerPrevPdfBtn);

        _viewerNextPdfBtn = IconOnlyButton("", P("Next PDF in folder · 資料夾下一個 PDF", "資料夾下一個 PDF"));
        _viewerNextPdfBtn.VerticalAlignment = VerticalAlignment.Bottom;
        _viewerNextPdfBtn.Click += async (_, _) => await MoveViewerSiblingAsync(1);
        Grid.SetColumn(_viewerNextPdfBtn, 3);
        pathRow.Children.Add(_viewerNextPdfBtn);

        var reload = IconOnlyButton("", P("Reload viewer · 重新載入檢視器", "重新載入檢視器"));
        reload.VerticalAlignment = VerticalAlignment.Bottom;
        reload.Click += async (_, _) => await NavigateViewerAsync();
        Grid.SetColumn(reload, 4);
        pathRow.Children.Add(reload);

        var toolbar = new Grid { ColumnSpacing = 8 };
        for (int i = 0; i < 10; i++)
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 9 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

        _viewerPrevPageBtn = IconOnlyButton("", P("Previous page · 上一頁", "上一頁"));
        _viewerPrevPageBtn.Click += async (_, _) => await MoveViewerPageAsync(-1);
        Grid.SetColumn(_viewerPrevPageBtn, 0);
        toolbar.Children.Add(_viewerPrevPageBtn);

        _viewerPageBox = new NumberBox
        {
            Width = 94,
            Minimum = 1,
            Maximum = 99999,
            Value = 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
        };
        Grid.SetColumn(_viewerPageBox, 1);
        toolbar.Children.Add(_viewerPageBox);

        var goPage = ButtonWithIcon("", P("Go · 前往", "前往"));
        goPage.Click += async (_, _) => await GoViewerPageAsync();
        Grid.SetColumn(goPage, 2);
        toolbar.Children.Add(goPage);

        _viewerNextPageBtn = IconOnlyButton("", P("Next page · 下一頁", "下一頁"));
        _viewerNextPageBtn.Click += async (_, _) => await MoveViewerPageAsync(1);
        Grid.SetColumn(_viewerNextPageBtn, 3);
        toolbar.Children.Add(_viewerNextPageBtn);

        _viewerZoomBox = new ComboBox { Width = 150 };
        AddZoomItem(P("Fit width · 適合闊度", "適合闊度"), "page-width");
        AddZoomItem(P("Fit page · 適合頁面", "適合頁面"), "page-fit");
        AddZoomItem("75%", "75");
        AddZoomItem("100%", "100");
        AddZoomItem("125%", "125");
        AddZoomItem("150%", "150");
        AddZoomItem("200%", "200");
        _viewerZoomBox.SelectedIndex = 0;
        _viewerZoomBox.SelectionChanged += async (_, _) =>
        {
            if ((_viewerZoomBox.SelectedItem as ComboBoxItem)?.Tag is string zoom)
            {
                _viewerZoom = zoom;
                await NavigateViewerAsync();
            }
        };
        Grid.SetColumn(_viewerZoomBox, 4);
        toolbar.Children.Add(_viewerZoomBox);

        var heightBtn = IconOnlyButton("", P("Toggle tall viewer · 切換高身檢視器", "切換高身檢視器"));
        heightBtn.Click += (_, _) =>
        {
            _viewerTall = !_viewerTall;
            if (_viewerHostFrame is not null) _viewerHostFrame.Height = ViewerHeight();
        };
        Grid.SetColumn(heightBtn, 5);
        toolbar.Children.Add(heightBtn);

        var sideBtn = IconOnlyButton("", P("Show/hide details · 顯示／隱藏詳細資料", "顯示／隱藏詳細資料"));
        sideBtn.Click += (_, _) =>
        {
            _viewerSideVisible = !_viewerSideVisible;
            if (_viewerSidePanel is not null)
                _viewerSidePanel.Visibility = _viewerSideVisible ? Visibility.Visible : Visibility.Collapsed;
        };
        Grid.SetColumn(sideBtn, 6);
        toolbar.Children.Add(sideBtn);

        _viewerCopyPathBtn = IconOnlyButton("", P("Copy file path · 複製檔案路徑", "複製檔案路徑"));
        _viewerCopyPathBtn.Click += (_, _) =>
        {
            if (_viewerPath is null) return;
            CopyText(_viewerPath);
            Note(result, P("PDF path copied.", "已複製 PDF 路徑。"));
        };
        Grid.SetColumn(_viewerCopyPathBtn, 7);
        toolbar.Children.Add(_viewerCopyPathBtn);

        _viewerOpenFolderBtn = IconOnlyButton("", P("Show in folder · 喺資料夾顯示", "喺資料夾顯示"));
        _viewerOpenFolderBtn.Click += (_, _) => { if (_viewerPath is not null) OpenInExplorer(_viewerPath); };
        Grid.SetColumn(_viewerOpenFolderBtn, 8);
        toolbar.Children.Add(_viewerOpenFolderBtn);

        _viewerPositionText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12,
        };
        Grid.SetColumn(_viewerPositionText, 9);
        toolbar.Children.Add(_viewerPositionText);

        var hostGrid = new Grid();
        _viewerWeb = new WebView2 { Visibility = Visibility.Collapsed };
        hostGrid.Children.Add(_viewerWeb);

        _viewerEmpty = new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 440,
                Children =
                {
                    new FontIcon { Glyph = "", FontSize = 42, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] },
                    new TextBlock { Text = P("No PDF loaded", "未載入 PDF"), HorizontalAlignment = HorizontalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                    new TextBlock
                    {
                        Text = P("Open a file, use folder navigation, or drop a PDF here.", "開啟檔案、用資料夾導覽，或者將 PDF 拖入嚟。"),
                        TextWrapping = TextWrapping.Wrap,
                        TextAlignment = TextAlignment.Center,
                        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                    },
                },
            },
        };
        hostGrid.Children.Add(_viewerEmpty);

        _viewerLoading = new StackPanel
        {
            Visibility = Visibility.Collapsed,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new ProgressRing { IsActive = true, Width = 34, Height = 34 },
                new TextBlock
                {
                    Text = P("Loading PDF…", "正在載入 PDF…"),
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                },
            },
        };
        hostGrid.Children.Add(_viewerLoading);

        _viewerDropOverlay = new Border
        {
            Visibility = Visibility.Collapsed,
            Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"],
            Opacity = 0.16,
        };
        hostGrid.Children.Add(_viewerDropOverlay);

        _viewerHostFrame = new Border
        {
            Height = ViewerHeight(),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = hostGrid,
            AllowDrop = true,
        };
        _viewerHostFrame.DragOver += Viewer_DragOver;
        _viewerHostFrame.DragLeave += (_, _) => { if (_viewerDropOverlay is not null) _viewerDropOverlay.Visibility = Visibility.Collapsed; };
        _viewerHostFrame.Drop += Viewer_Drop;

        _viewerStatusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 12,
        };

        pane.Children.Add(pathRow);
        pane.Children.Add(toolbar);
        pane.Children.Add(_viewerHostFrame);
        pane.Children.Add(_viewerStatusText);
        return pane;

        void AddZoomItem(string label, string tag) => _viewerZoomBox.Items.Add(new ComboBoxItem { Content = label, Tag = tag });
    }

    private Border BuildViewerRightPane(InfoBar result)
    {
        var side = new Border
        {
            Width = 360,
            Visibility = _viewerSideVisible ? Visibility.Visible : Visibility.Collapsed,
            Background = (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14),
        };
        _viewerSidePanel = side;

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = ViewerHeight() + 122 };
        var stack = new StackPanel { Spacing = 12 };
        scroll.Content = stack;
        side.Child = scroll;

        _viewerPasswordBox = new PasswordBox
        {
            Header = P("Password for protected PDFs · 受保護 PDF 密碼", "受保護 PDF 密碼"),
            PlaceholderText = P("Optional", "可選"),
        };
        var reinspect = ButtonWithIcon("", P("Reload metadata · 重新讀取資料", "重新讀取資料"));
        reinspect.Click += async (_, _) =>
        {
            if (_viewerPath is null) return;
            await RefreshViewerMetadataAsync(result);
            await NavigateViewerAsync();
        };

        _viewerMetaRows = Section(P("Document", "文件"));
        _viewerPageRows = Section(P("Page inventory", "頁面清單"));

        _viewerSearchBox = new TextBox
        {
            Header = P("Search text · 搜尋文字", "搜尋文字"),
            PlaceholderText = P("Find words in extractable PDF text", "搜尋可抽取嘅 PDF 文字"),
        };
        _viewerCaseBox = new CheckBox { Content = P("Case-sensitive · 分大小寫", "分大小寫") };
        var searchBtn = ButtonWithIcon("", P("Search · 搜尋", "搜尋"));
        var clearSearchBtn = ButtonWithIcon("", P("Clear · 清除", "清除"));
        _viewerSearchRows = new StackPanel { Spacing = 8 };
        var searchBusy = NewBusy();
        searchBtn.Click += async (_, _) => await SearchViewerAsync(result, searchBtn, searchBusy);
        clearSearchBtn.Click += (_, _) => _viewerSearchRows.Children.Clear();

        _viewerRangeBox = new TextBox
        {
            Header = P("Page range for quick actions · 快速操作頁碼範圍", "快速操作頁碼範圍"),
            PlaceholderText = P("blank = current page, e.g. 1-3, 7", "留空 = 目前頁，例如 1-3, 7"),
        };
        var watermarkText = new TextBox
        {
            Header = P("Watermark text · 浮水印文字", "浮水印文字"),
            Text = "CONFIDENTIAL",
        };
        var quickBusy = NewBusy();
        var saveRange = ButtonWithIcon("", P("Save range · 儲存範圍", "儲存範圍"));
        var savePage = ButtonWithIcon("", P("Save page · 儲存頁面", "儲存頁面"));
        var rotatePage = ButtonWithIcon("", P("Rotate page copy · 旋轉頁面副本", "旋轉頁面副本"));
        var splitAll = ButtonWithIcon("", P("Split all · 全部分割", "全部分割"));
        var extractText = ButtonWithIcon("", P("Extract text · 抽取文字", "抽取文字"));
        var mergeMore = ButtonWithIcon("", P("Merge with… · 合併其他…", "合併其他…"));
        var watermark = ButtonWithIcon("", P("Watermark copy · 浮水印副本", "浮水印副本"));

        saveRange.Click += async (_, _) => await SaveViewerRangeAsync(result, saveRange, quickBusy);
        savePage.Click += async (_, _) => await SaveViewerCurrentPageAsync(result, savePage, quickBusy);
        rotatePage.Click += async (_, _) => await RotateViewerPageAsync(result, rotatePage, quickBusy);
        splitAll.Click += async (_, _) => await SplitViewerAsync(result, splitAll, quickBusy);
        extractText.Click += async (_, _) => await ExtractViewerTextAsync(result, extractText, quickBusy);
        mergeMore.Click += async (_, _) => await MergeViewerWithMoreAsync(result, mergeMore, quickBusy);
        watermark.Click += async (_, _) => await WatermarkViewerAsync(result, watermark, quickBusy, watermarkText.Text);

        stack.Children.Add(_viewerPasswordBox);
        stack.Children.Add(reinspect);
        stack.Children.Add(_viewerMetaRows);
        stack.Children.Add(_viewerPageRows);
        stack.Children.Add(ThinRule());
        stack.Children.Add(_viewerSearchBox);
        stack.Children.Add(_viewerCaseBox);
        stack.Children.Add(ActionRow(searchBtn, clearSearchBtn, searchBusy));
        stack.Children.Add(new ScrollViewer { MaxHeight = 220, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _viewerSearchRows });
        stack.Children.Add(ThinRule());
        stack.Children.Add(_viewerRangeBox);
        stack.Children.Add(watermarkText);
        stack.Children.Add(ActionRow(savePage, saveRange));
        stack.Children.Add(ActionRow(rotatePage, splitAll));
        stack.Children.Add(ActionRow(extractText, mergeMore));
        stack.Children.Add(ActionRow(watermark, quickBusy));

        return side;

        StackPanel Section(string title)
        {
            return new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock
                    {
                        Text = title,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    },
                },
            };
        }
    }

    private async Task PickViewerPdfAsync()
    {
        var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Open PDF", "開 PDF"));
        if (p is not null) await LoadViewerPdfAsync(p);
    }

    private async Task LoadViewerPdfAsync(string path, bool keepSiblings = false)
    {
        if (!File.Exists(path))
        {
            if (_viewerStatusText is not null) _viewerStatusText.Text = P("File not found.", "搵唔到檔案。");
            return;
        }

        _viewerPath = path;
        _viewerPage = 1;
        if (_viewerPathBox is not null) _viewerPathBox.Text = path;
        if (!keepSiblings) LoadViewerSiblings(path);
        SetViewerLoading(true);
        UpdateViewerState();

        await NavigateViewerAsync();
        await RefreshViewerMetadataAsync(null);
        SetViewerLoading(false);
    }

    private void LoadViewerSiblings(string path)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            _viewerSiblings = string.IsNullOrEmpty(dir)
                ? new List<string> { path }
                : Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly)
                    .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            _viewerSiblingIndex = _viewerSiblings.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (_viewerSiblingIndex < 0)
            {
                _viewerSiblings.Insert(0, path);
                _viewerSiblingIndex = 0;
            }
        }
        catch
        {
            _viewerSiblings = new List<string> { path };
            _viewerSiblingIndex = 0;
        }
    }

    private async Task MoveViewerSiblingAsync(int delta)
    {
        if (_viewerSiblings.Count < 2) return;
        _viewerSiblingIndex = (_viewerSiblingIndex + delta + _viewerSiblings.Count) % _viewerSiblings.Count;
        await LoadViewerPdfAsync(_viewerSiblings[_viewerSiblingIndex], keepSiblings: true);
    }

    private async Task MoveViewerPageAsync(int delta)
    {
        if (_viewerPath is null) return;
        int max = _viewerPageCount > 0 ? _viewerPageCount : 99999;
        _viewerPage = Math.Clamp(_viewerPage + delta, 1, max);
        if (_viewerPageBox is not null) _viewerPageBox.Value = _viewerPage;
        UpdateViewerState();
        await NavigateViewerAsync();
    }

    private async Task GoViewerPageAsync()
    {
        if (_viewerPath is null || _viewerPageBox is null || double.IsNaN(_viewerPageBox.Value)) return;
        int max = _viewerPageCount > 0 ? _viewerPageCount : 99999;
        _viewerPage = Math.Clamp((int)Math.Round(_viewerPageBox.Value), 1, max);
        _viewerPageBox.Value = _viewerPage;
        UpdateViewerState();
        await NavigateViewerAsync();
    }

    private async Task NavigateViewerAsync()
    {
        if (_viewerPath is null || _viewerWeb is null) return;
        try
        {
            await EnsureViewerWebAsync();
            if (_viewerEmpty is not null) _viewerEmpty.Visibility = Visibility.Collapsed;
            _viewerWeb.Visibility = Visibility.Visible;
            var uri = new Uri(_viewerPath).AbsoluteUri;
            var fragment = $"page={Math.Max(1, _viewerPage)}&zoom={Uri.EscapeDataString(_viewerZoom)}";
            _viewerWeb.CoreWebView2!.Navigate($"{uri}#{fragment}");
            if (_viewerStatusText is not null)
                _viewerStatusText.Text = P("Viewer ready. The built-in PDF toolbar remains available inside the preview surface.",
                    "檢視器已就緒。預覽區入面仍可用內建 PDF 工具列。");
        }
        catch (Exception ex)
        {
            SetViewerLoading(false);
            if (_viewerWeb is not null) _viewerWeb.Visibility = Visibility.Collapsed;
            if (_viewerEmpty is not null) _viewerEmpty.Visibility = Visibility.Visible;
            if (_viewerStatusText is not null)
                _viewerStatusText.Text = P($"WebView2 PDF viewer unavailable: {ex.Message}", $"WebView2 PDF 檢視器唔可用：{ex.Message}");
        }
    }

    private async Task EnsureViewerWebAsync()
    {
        if (_viewerWeb is null) return;
        if (_viewerWebReady && _viewerWeb.CoreWebView2 is not null) return;
        _ = CoreWebView2Environment.GetAvailableBrowserVersionString();
        await _viewerWeb.EnsureCoreWebView2Async();
        _viewerWebReady = true;

        if (!_viewerWebConfigured && _viewerWeb.CoreWebView2 is { } core)
        {
            var s = core.Settings;
            s.AreDefaultContextMenusEnabled = true;
            s.AreDevToolsEnabled = false;
            s.IsZoomControlEnabled = true;
            core.NewWindowRequested += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    CopyText(e.Uri);
                    if (_viewerStatusText is not null) _viewerStatusText.Text = P("Link copied.", "已複製連結。");
                }
                catch { }
            };
            core.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess && _viewerStatusText is not null)
                    _viewerStatusText.Text = P($"PDF viewer navigation failed: {e.WebErrorStatus}", $"PDF 檢視器導覽失敗：{e.WebErrorStatus}");
            };
            _viewerWebConfigured = true;
        }
    }

    private async Task RefreshViewerMetadataAsync(InfoBar? result)
    {
        if (_viewerPath is null) return;
        try
        {
            var meta = await PdfToolkitService.InspectAsync(_viewerPath, ViewerPassword());
            _viewerPageCount = meta.PageCount;
            FillViewerMetadata(meta);
            UpdateViewerState();
            if (result is not null) Note(result, P("PDF metadata refreshed.", "已重新讀取 PDF 資料。"));
        }
        catch (Exception ex)
        {
            _viewerPageCount = 0;
            FillViewerMetadataError(ex.Message);
            UpdateViewerState();
            if (result is not null) Warn(result, "Could not inspect this PDF. Enter its password if it is protected.", "無法檢查呢個 PDF。如果受保護，請輸入密碼。");
        }
    }

    private void FillViewerMetadata(PdfToolkitService.PdfMetadata meta)
    {
        if (_viewerMetaRows is null || _viewerPageRows is null) return;
        ClearRows(_viewerMetaRows, keepHeader: true);
        ClearRows(_viewerPageRows, keepHeader: true);

        AddMetaRow(_viewerMetaRows, P("Name", "名稱"), Path.GetFileName(meta.Path));
        AddMetaRow(_viewerMetaRows, P("Pages", "頁數"), meta.PageCount.ToString("N0", CultureInfo.CurrentCulture));
        AddMetaRow(_viewerMetaRows, P("Size", "大小"), FormatBytes(meta.FileBytes));
        AddMetaRow(_viewerMetaRows, P("Modified", "修改時間"), meta.Modified == DateTime.MinValue ? "" : meta.Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture));
        AddMetaRow(_viewerMetaRows, P("PDF version", "PDF 版本"), meta.Version);
        AddMetaRow(_viewerMetaRows, P("Title", "標題"), meta.Title);
        AddMetaRow(_viewerMetaRows, P("Author", "作者"), meta.Author);
        AddMetaRow(_viewerMetaRows, P("Subject", "主題"), meta.Subject);
        AddMetaRow(_viewerMetaRows, P("Keywords", "關鍵字"), meta.Keywords);
        AddMetaRow(_viewerMetaRows, P("Creator", "建立程式"), meta.Creator);
        AddMetaRow(_viewerMetaRows, P("Producer", "製作程式"), meta.Producer);
        AddMetaRow(_viewerMetaRows, P("Created", "建立時間"), meta.Created);
        AddMetaRow(_viewerMetaRows, P("PDF modified", "PDF 修改時間"), meta.PdfModified);

        foreach (var p in meta.PageSummaries)
        {
            AddMetaRow(_viewerPageRows, P($"Page {p.PageNumber}", $"第 {p.PageNumber} 頁"),
                $"{FormatPageSize(p.WidthPoints, p.HeightPoints)}" + (p.Rotation == 0 ? "" : $" · {p.Rotation}°"));
        }
        if (meta.PageCount > meta.PageSummaries.Count)
            AddPlainRow(_viewerPageRows, P($"Showing first {meta.PageSummaries.Count} of {meta.PageCount} pages.",
                $"顯示頭 {meta.PageSummaries.Count} / {meta.PageCount} 頁。"));
    }

    private void FillViewerMetadataError(string message)
    {
        if (_viewerMetaRows is null || _viewerPageRows is null) return;
        ClearRows(_viewerMetaRows, keepHeader: true);
        ClearRows(_viewerPageRows, keepHeader: true);
        AddMetaRow(_viewerMetaRows, P("Status", "狀態"), P("Metadata unavailable", "讀唔到資料"));
        AddMetaRow(_viewerMetaRows, P("Reason", "原因"), message);
        AddPlainRow(_viewerPageRows, P("Enter the PDF password, then reload metadata.", "輸入 PDF 密碼，然後重新讀取資料。"));
    }

    private async Task SearchViewerAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (_viewerPath is null)
        {
            Warn(result, "Open a PDF first.", "請先開啟 PDF。");
            return;
        }
        var query = _viewerSearchBox?.Text ?? "";
        if (string.IsNullOrWhiteSpace(query))
        {
            Warn(result, "Enter search text.", "請輸入搜尋文字。");
            return;
        }

        btn.IsEnabled = false;
        busy.IsActive = true;
        busy.Visibility = Visibility.Visible;
        try
        {
            var hits = await PdfToolkitService.SearchTextAsync(_viewerPath, query, ViewerPassword(), _viewerCaseBox?.IsChecked == true);
            if (_viewerSearchRows is null) return;
            _viewerSearchRows.Children.Clear();
            if (hits.Count == 0)
            {
                AddPlainRow(_viewerSearchRows, P("No matches found.", "搵唔到符合項目。"));
                Note(result, P("Search complete: no matches.", "搜尋完成：冇符合項目。"));
                return;
            }

            foreach (var hit in hits)
            {
                var jump = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Content = new StackPanel
                    {
                        Spacing = 3,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = P($"Page {hit.PageNumber} · {hit.MatchCount} match(es)", $"第 {hit.PageNumber} 頁 · {hit.MatchCount} 個符合項目"),
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                            },
                            new TextBlock
                            {
                                Text = hit.Snippet,
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 12,
                                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                            },
                        },
                    },
                };
                jump.Click += async (_, _) =>
                {
                    _viewerPage = hit.PageNumber;
                    if (_viewerPageBox is not null) _viewerPageBox.Value = _viewerPage;
                    UpdateViewerState();
                    await NavigateViewerAsync();
                };
                _viewerSearchRows.Children.Add(jump);
            }
            Note(result, P($"Search complete: {hits.Count} page(s) with matches.", $"搜尋完成：{hits.Count} 頁有符合項目。"));
        }
        catch (Exception ex)
        {
            result.Severity = InfoBarSeverity.Error;
            result.Message = P($"Search failed: {ex.Message}", $"搜尋失敗：{ex.Message}");
            result.IsOpen = true;
        }
        finally
        {
            btn.IsEnabled = true;
            busy.IsActive = false;
            busy.Visibility = Visibility.Collapsed;
        }
    }

    private async Task SaveViewerCurrentPageAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (!RequireViewerPdf(result)) return;
        var suggested = SuggestedPdfName($"_p{_viewerPage}");
        var output = await FileDialogs.SaveFileAsync(suggested, PdfFilter, "pdf", P("Save current page", "儲存目前頁"));
        if (output is null) return;
        string page = _viewerPage.ToString(CultureInfo.InvariantCulture);
        await RunOp(btn, result, busy, () => PdfToolkitService.ExtractPagesAsync(_viewerPath!, output, page, ViewerPassword()), true, () => output);
    }

    private async Task SaveViewerRangeAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (!RequireViewerPdf(result)) return;
        var spec = ViewerRangeOrCurrent();
        var output = await FileDialogs.SaveFileAsync(SuggestedPdfName("_range"), PdfFilter, "pdf", P("Save page range", "儲存頁碼範圍"));
        if (output is null) return;
        await RunOp(btn, result, busy, () => PdfToolkitService.ExtractPagesAsync(_viewerPath!, output, spec, ViewerPassword()), true, () => output);
    }

    private async Task RotateViewerPageAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (!RequireViewerPdf(result)) return;
        var output = await FileDialogs.SaveFileAsync(SuggestedPdfName($"_p{_viewerPage}_rotated"), PdfFilter, "pdf", P("Save rotated-page copy", "儲存旋轉頁面副本"));
        if (output is null) return;
        string page = _viewerPage.ToString(CultureInfo.InvariantCulture);
        await RunOp(btn, result, busy, () => PdfToolkitService.RotateAsync(_viewerPath!, output, 90, page, ViewerPassword()), true, () => output);
    }

    private async Task SplitViewerAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (!RequireViewerPdf(result)) return;
        var folder = await FileDialogs.OpenFolderAsync(P("Choose output folder", "揀輸出資料夾"));
        if (folder is null) return;
        await RunOp(btn, result, busy, () => PdfToolkitService.SplitPerPageAsync(_viewerPath!, folder, ViewerPassword()), true, () => folder);
    }

    private async Task ExtractViewerTextAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (!RequireViewerPdf(result)) return;
        var output = await FileDialogs.SaveFileAsync(SuggestedTextName("_text"), new[] { new FileDialogs.Filter("Text", "*.txt"), new FileDialogs.Filter("All files", "*.*") }, "txt", P("Save extracted text", "儲存抽取文字"));
        if (output is null) return;
        await RunOp(btn, result, busy, () => PdfToolkitService.ExtractTextAsync(_viewerPath!, output, ViewerPassword()), true, () => output);
    }

    private async Task MergeViewerWithMoreAsync(InfoBar result, Button btn, ProgressRing busy)
    {
        if (!RequireViewerPdf(result)) return;
        var more = await FileDialogs.OpenFilesAsync(PdfFilter, P("Choose PDFs to append", "揀要加喺後面嘅 PDF"));
        if (more.Count == 0) return;
        var output = await FileDialogs.SaveFileAsync(SuggestedPdfName("_merged"), PdfFilter, "pdf", P("Save merged PDF", "儲存合併 PDF"));
        if (output is null) return;
        var inputs = new List<string> { _viewerPath! };
        inputs.AddRange(more);
        await RunOp(btn, result, busy, () => PdfToolkitService.MergeAsync(inputs, output), true, () => output);
    }

    private async Task WatermarkViewerAsync(InfoBar result, Button btn, ProgressRing busy, string text)
    {
        if (!RequireViewerPdf(result)) return;
        if (string.IsNullOrWhiteSpace(text))
        {
            Warn(result, "Enter watermark text.", "請輸入浮水印文字。");
            return;
        }
        var output = await FileDialogs.SaveFileAsync(SuggestedPdfName("_watermarked"), PdfFilter, "pdf", P("Save watermarked PDF", "儲存浮水印 PDF"));
        if (output is null) return;
        await RunOp(btn, result, busy, () => PdfToolkitService.WatermarkAsync(_viewerPath!, output, text, 0.25, 45, 48, ViewerPassword()), true, () => output);
    }

    private void Viewer_DragOver(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = P("Drop PDF to open", "放低 PDF 來開啟");
        e.DragUIOverride.IsContentVisible = true;
        if (_viewerDropOverlay is not null) _viewerDropOverlay.Visibility = Visibility.Visible;
    }

    private async void Viewer_Drop(object sender, DragEventArgs e)
    {
        if (_viewerDropOverlay is not null) _viewerDropOverlay.Visibility = Visibility.Collapsed;
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var def = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault(f =>
                string.Equals(Path.GetExtension(f.Path), ".pdf", StringComparison.OrdinalIgnoreCase));
            if (file is not null) await LoadViewerPdfAsync(file.Path);
        }
        finally
        {
            def.Complete();
        }
    }

    private void UpdateViewerState()
    {
        bool hasPdf = _viewerPath is not null;
        bool hasPages = _viewerPageCount > 0;
        int max = hasPages ? _viewerPageCount : 99999;
        _viewerPage = Math.Clamp(_viewerPage, 1, max);

        if (_viewerPageBox is not null)
        {
            _viewerPageBox.Maximum = max;
            _viewerPageBox.Value = _viewerPage;
        }
        if (_viewerPositionText is not null)
        {
            var pageText = hasPages ? $"{_viewerPage} / {_viewerPageCount}" : (hasPdf ? $"{_viewerPage}" : "");
            var fileText = _viewerSiblings.Count > 1 ? $" · PDF {_viewerSiblingIndex + 1} / {_viewerSiblings.Count}" : "";
            _viewerPositionText.Text = pageText + fileText;
        }

        if (_viewerPrevPdfBtn is not null) _viewerPrevPdfBtn.IsEnabled = _viewerSiblings.Count > 1;
        if (_viewerNextPdfBtn is not null) _viewerNextPdfBtn.IsEnabled = _viewerSiblings.Count > 1;
        if (_viewerPrevPageBtn is not null) _viewerPrevPageBtn.IsEnabled = hasPdf && _viewerPage > 1;
        if (_viewerNextPageBtn is not null) _viewerNextPageBtn.IsEnabled = hasPdf && (!hasPages || _viewerPage < _viewerPageCount);
        if (_viewerOpenFolderBtn is not null) _viewerOpenFolderBtn.IsEnabled = hasPdf;
        if (_viewerCopyPathBtn is not null) _viewerCopyPathBtn.IsEnabled = hasPdf;
    }

    private string? ViewerPassword()
        => string.IsNullOrEmpty(_viewerPasswordBox?.Password) ? null : _viewerPasswordBox.Password;

    private string ViewerRangeOrCurrent()
        => string.IsNullOrWhiteSpace(_viewerRangeBox?.Text)
            ? _viewerPage.ToString(CultureInfo.InvariantCulture)
            : _viewerRangeBox!.Text.Trim();

    private bool RequireViewerPdf(InfoBar result)
    {
        if (_viewerPath is not null) return true;
        Warn(result, "Open a PDF first.", "請先開啟 PDF。");
        return false;
    }

    private void SetViewerLoading(bool on)
    {
        if (_viewerLoading is not null) _viewerLoading.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    private double ViewerHeight() => _viewerTall ? 760 : 540;

    private string SuggestedPdfName(string suffix)
    {
        var stem = _viewerPath is null ? "document" : Path.GetFileNameWithoutExtension(_viewerPath);
        return $"{stem}{suffix}.pdf";
    }

    private string SuggestedTextName(string suffix)
    {
        var stem = _viewerPath is null ? "document" : Path.GetFileNameWithoutExtension(_viewerPath);
        return $"{stem}{suffix}.txt";
    }

    private static Button ButtonWithIcon(string glyph, string text, bool accent = false)
    {
        var b = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 7,
                Children =
                {
                    new FontIcon { Glyph = glyph, FontSize = 14 },
                    new TextBlock { Text = text },
                },
            },
        };
        if (accent) b.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        return b;
    }

    private static Button IconOnlyButton(string glyph, string tooltip)
    {
        var b = new Button { Content = new FontIcon { Glyph = glyph, FontSize = 14 }, MinWidth = 38 };
        ToolTipService.SetToolTip(b, tooltip);
        return b;
    }

    private static Border ThinRule()
        => new()
        {
            Height = 1,
            Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Opacity = 0.8,
            Margin = new Thickness(0, 2, 0, 2),
        };

    private static void AddMetaRow(StackPanel host, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var k = new TextBlock
        {
            Text = key,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
            TextWrapping = TextWrapping.Wrap,
        };
        var v = new TextBlock
        {
            Text = value,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        Grid.SetColumn(k, 0);
        Grid.SetColumn(v, 1);
        row.Children.Add(k);
        row.Children.Add(v);
        host.Children.Add(row);
    }

    private static void AddPlainRow(StackPanel host, string text)
        => host.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            IsTextSelectionEnabled = true,
        });

    private static void ClearRows(StackPanel host, bool keepHeader)
    {
        int keep = keepHeader && host.Children.Count > 0 ? 1 : 0;
        while (host.Children.Count > keep)
            host.Children.RemoveAt(keep);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0
            ? $"{bytes:N0} {units[unit]}"
            : $"{value:N1} {units[unit]}";
    }

    private static string FormatPageSize(double widthPt, double heightPt)
    {
        double widthIn = widthPt / 72.0;
        double heightIn = heightPt / 72.0;
        double widthMm = widthIn * 25.4;
        double heightMm = heightIn * 25.4;
        return string.Format(CultureInfo.CurrentCulture, "{0:0.#}×{1:0.#} in · {2:0}×{3:0} mm", widthIn, heightIn, widthMm, heightMm);
    }

    private static void CopyText(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }

    private static void Note(InfoBar bar, string message)
    {
        bar.Severity = InfoBarSeverity.Informational;
        bar.Message = message;
        bar.ActionButton = null;
        bar.IsOpen = true;
    }
}
