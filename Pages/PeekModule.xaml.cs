using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.Storage.Streams;

namespace WinForge.Pages;

/// <summary>
/// 快速預覽模組 · Peek module — a native clone of PowerToys "Peek": fast, read-only file preview.
/// 揀檔案（用 Win32 對話框，唔用 WinRT picker）或者拖放，按類型即時預覽：圖片、文字／程式碼、Markdown
/// （渲染）、PDF、音訊／影片（內嵌播放）、壓縮檔（列出內容）、未知類型（顯示中繼資料）。仲有同一資料夾
/// 上一個／下一個導覽、開啟／用其他程式開啟／開資料夾，同一個可選嘅全域熱鍵（預覽檔案總管目前選取嘅檔案）。
/// Pick a file (via the Win32 dialog — never a WinRT picker) or drop one in, and it previews by type:
/// images, text/code (monospace), Markdown (rendered), PDF, audio/video (embedded MediaPlayer), archives
/// (entry list) and unknown files (metadata: size, dates, type, icon). Prev/Next steps through siblings in
/// the same folder; Open / Open-with / Open-folder shell out; an optional global hotkey previews whatever
/// is selected in the foreground Explorer window.
/// </summary>
public sealed partial class PeekModule : Page
{
    private const string HotkeyEnabledKey = "peek.hotkey.enabled";
    private const string HotkeyModKey = "peek.hotkey.mods";
    private const string HotkeyVkKey = "peek.hotkey.vk";

    private List<string> _siblings = new();
    private int _index = -1;
    private string? _current;
    private MediaPlayer? _player;
    private CancellationTokenSource? _loadCts;

    public PeekModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        StartHotkeyIfEnabled();
        // a path may have been queued by the start-page / search before navigation
        if (!string.IsNullOrEmpty(AppState.CurrentPeekPath) && File.Exists(AppState.CurrentPeekPath))
            _ = LoadAsync(AppState.CurrentPeekPath);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        StopHotkey();
        TeardownMedia();
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        if (_current is not null) UpdateInfoHeader(PeekService.Describe(_current));
    }

    // ===================== text / labels =====================

    private void Render()
    {
        HeaderTitle.Text = "Peek · 快速預覽";
        HeaderBlurb.Text = P(
            "Quick, read-only file preview — pick or drop a file and it shows by type: images, text & code, Markdown, PDF, audio, video, archives, or metadata. Step Prev/Next through the folder.",
            "快速、唯讀嘅檔案預覽 — 揀或者拖放一個檔案，按類型即時顯示：圖片、文字同程式碼、Markdown、PDF、音訊、影片、壓縮檔，或者中繼資料。可以喺資料夾入面上一個／下一個咁睇。");
        PickText.Text = P("Open file…", "開啟檔案…");
        PickFolderText.Text = P("From folder…", "由資料夾…");
        ExplorerText.Text = P("Peek Explorer selection", "預覽總管選取");
        HotkeyCfgText.Text = P("Hotkey", "熱鍵");
        DropHint.Text = P("Drag a file here to preview", "拖放檔案到呢度預覽");
        EmptyTitle.Text = P("Nothing to preview yet", "未有嘢預覽");
        EmptyBlurb.Text = P(
            "Open a file, drop one here, or press your Peek hotkey while a file is selected in Explorer.",
            "開一個檔案、拖放一個過嚟，或者喺檔案總管揀咗檔案時撳 Peek 熱鍵。");
        OpenText.Text = P("Open", "開啟");
        OpenWithText.Text = P("Open with…", "用…開啟");
        OpenFolderText.Text = P("Show in folder", "喺資料夾顯示");
        CopyPathText.Text = P("Copy path", "複製路徑");
        ToolTipService.SetToolTip(PrevBtn, P("Previous file", "上一個檔案"));
        ToolTipService.SetToolTip(NextBtn, P("Next file", "下一個檔案"));
    }

    // ===================== pickers / drop =====================

    private async void Pick_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync((IEnumerable<FileDialogs.Filter>?)null, P("Pick a file to preview", "揀檔案嚟預覽"));
        if (!string.IsNullOrEmpty(path)) await LoadAsync(path!);
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Pick a folder", "揀資料夾"));
        if (string.IsNullOrEmpty(folder)) return;
        try
        {
            var first = Directory.EnumerateFiles(folder!)
                .OrderBy(f => Path.GetFileName(f), NaturalComparer.Instance)
                .FirstOrDefault();
            if (first is null) { ShowHotkeyNote(P("Folder is empty.", "資料夾係空嘅。"), InfoBarSeverity.Warning); return; }
            await LoadAsync(first);
        }
        catch (Exception ex) { ShowHotkeyNote(ex.Message, InfoBarSeverity.Error); }
    }

    private async void PeekExplorer_Click(object sender, RoutedEventArgs e)
    {
        var sel = PeekService.TryGetExplorerSelection();
        if (string.IsNullOrEmpty(sel))
        {
            ShowHotkeyNote(P(
                "No file is selected in a foreground Explorer window (or it can't be read when running elevated). Use Open file… instead.",
                "檔案總管前景視窗未有揀檔案（或者以管理員身分執行時讀唔到）。請改用「開啟檔案…」。"),
                InfoBarSeverity.Warning);
            return;
        }
        await LoadAsync(sel!);
    }

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = P("Drop to preview", "放低嚟預覽");
            e.DragUIOverride.IsContentVisible = true;
        }
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems)) return;
        var def = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var file = items.OfType<StorageFile>().FirstOrDefault();
            if (file is not null) await LoadAsync(file.Path);
        }
        catch { }
        finally { def.Complete(); }
    }

    // ===================== Prev / Next =====================

    private async void Prev_Click(object sender, RoutedEventArgs e)
    {
        if (_siblings.Count == 0) return;
        _index = (_index - 1 + _siblings.Count) % _siblings.Count;
        await LoadAsync(_siblings[_index], keepSiblings: true);
    }

    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_siblings.Count == 0) return;
        _index = (_index + 1) % _siblings.Count;
        await LoadAsync(_siblings[_index], keepSiblings: true);
    }

    // ===================== action bar =====================

    private void Open_Click(object sender, RoutedEventArgs e) { if (_current is not null) PeekService.Open(_current); }
    private void OpenWith_Click(object sender, RoutedEventArgs e) { if (_current is not null) PeekService.OpenWith(_current); }
    private void OpenFolder_Click(object sender, RoutedEventArgs e) { if (_current is not null) PeekService.OpenFolder(_current); }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;
        try
        {
            var dp = new DataPackage();
            dp.SetText(_current);
            Clipboard.SetContent(dp);
            ShowHotkeyNote(P("Path copied.", "已複製路徑。"), InfoBarSeverity.Success);
        }
        catch { }
    }

    // ===================== core load =====================

    private async Task LoadAsync(string path, bool keepSiblings = false)
    {
        _loadCts?.Cancel();
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        var ct = cts.Token;

        _current = path;
        AppState.CurrentPeekPath = path;

        if (!keepSiblings)
        {
            _siblings = PeekService.SiblingFiles(path);
            _index = _siblings.FindIndex(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
            if (_index < 0) { _siblings.Insert(0, path); _index = 0; }
        }

        var item = PeekService.Describe(path);
        EmptyState.Visibility = Visibility.Collapsed;
        InfoCard.Visibility = Visibility.Visible;
        ActionBar.Visibility = Visibility.Visible;
        UpdateInfoHeader(item);
        HideAllPreviews();
        TeardownMedia();

        if (!item.Exists)
        {
            RenderMetadata(item, missing: true);
            return;
        }

        Busy.IsActive = true;
        try
        {
            switch (item.Kind)
            {
                case PeekKind.Image: await ShowImageAsync(item, ct); break;
                case PeekKind.Markdown: await ShowMarkdownAsync(item, ct); break;
                case PeekKind.Pdf:
                case PeekKind.Web: ShowWebFile(item); break;
                case PeekKind.Audio:
                case PeekKind.Video: ShowMedia(item); break;
                case PeekKind.Archive: await ShowArchiveAsync(item, ct); break;
                case PeekKind.Text: await ShowTextAsync(item, ct); break;
                default: RenderMetadata(item, missing: false); break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            RenderMetadata(item, missing: false, extraNote: ex.Message);
        }
        finally
        {
            if (_loadCts == cts) Busy.IsActive = false;
        }
    }

    private void UpdateInfoHeader(PeekItem item)
    {
        FileNameText.Text = item.Name;
        var parts = new List<string>
        {
            item.Exists ? item.SizeText : P("missing", "唔見咗"),
            item.Kind switch
            {
                PeekKind.Image => P("Image", "圖片"),
                PeekKind.Text => P("Text / code", "文字／程式碼"),
                PeekKind.Markdown => "Markdown",
                PeekKind.Pdf => "PDF",
                PeekKind.Audio => P("Audio", "音訊"),
                PeekKind.Video => P("Video", "影片"),
                PeekKind.Archive => P("Archive", "壓縮檔"),
                PeekKind.Web => "HTML",
                _ => string.IsNullOrEmpty(item.Extension) ? P("File", "檔案") : "." + item.Extension,
            },
        };
        if (item.Exists)
            parts.Add(P("modified ", "修改於 ") + item.Modified.ToString("yyyy-MM-dd HH:mm"));
        FileMetaText.Text = string.Join("   ·   ", parts);

        CounterText.Text = _siblings.Count > 1 ? $"{_index + 1} / {_siblings.Count}" : "";
        PrevBtn.IsEnabled = NextBtn.IsEnabled = _siblings.Count > 1;

        _ = LoadShellThumbAsync(item.Path, FileIcon, 48);
    }

    private void HideAllPreviews()
    {
        ImageHost.Visibility = Visibility.Collapsed;
        TextHost.Visibility = Visibility.Collapsed;
        WebHost.Visibility = Visibility.Collapsed;
        MediaHost.Visibility = Visibility.Collapsed;
        ArchiveHost.Visibility = Visibility.Collapsed;
        MetaHost.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        TextView.Text = "";
    }

    // ===================== per-type previewers =====================

    private async Task ShowImageAsync(PeekItem item, CancellationToken ct)
    {
        if (PeekService.IsVector(item.Path))
        {
            // SVG via WebView2 so it stays crisp at any zoom.
            await EnsureWebAsync();
            if (ct.IsCancellationRequested) return;
            var dark = ActualTheme == ElementTheme.Dark;
            var svg = await File.ReadAllTextAsync(item.Path, ct);
            var bg = dark ? "#1f1f1f" : "#ffffff";
            var html = $"<!DOCTYPE html><html><head><meta charset='utf-8'><style>html,body{{margin:0;height:100%;background:{bg};display:flex;align-items:center;justify-content:center;}}svg{{max-width:96vw;max-height:96vh;}}</style></head><body>{svg}</body></html>";
            WebHost.NavigateToString(html);
            WebHost.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            var bmp = new BitmapImage();
            var file = await StorageFile.GetFileFromPathAsync(item.Path);
            using var stream = await file.OpenReadAsync();
            if (ct.IsCancellationRequested) return;
            await bmp.SetSourceAsync(stream);
            if (ct.IsCancellationRequested) return;
            PreviewImage.Source = bmp;
            ImageHost.Visibility = Visibility.Visible;

            // append pixel dimensions to the header once known
            if (bmp.PixelWidth > 0)
                FileMetaText.Text += $"   ·   {bmp.PixelWidth} × {bmp.PixelHeight} px";
        }
        catch (Exception ex)
        {
            RenderMetadata(item, missing: false, extraNote: ex.Message);
        }
    }

    private async Task ShowTextAsync(PeekItem item, CancellationToken ct)
    {
        var (text, truncated, isBinary) = await PeekService.ReadTextAsync(item.Path);
        if (ct.IsCancellationRequested) return;

        if (isBinary)
        {
            // not really text — fall back to metadata + a hex-ish note
            RenderMetadata(item, missing: false,
                extraNote: P("This file looks binary; showing metadata instead.", "呢個檔案似係二進位，改為顯示中繼資料。"));
            return;
        }

        var note = truncated ? P("\n\n… (truncated — file is larger than 1 MB)", "\n\n…（已截斷 — 檔案大過 1 MB）") : "";
        TextView.Text = text + note;
        FileMetaText.Text += $"   ·   {PeekService.CountLines(text):N0} " + P("lines", "行");
        TextHost.Visibility = Visibility.Visible;
    }

    private async Task ShowMarkdownAsync(PeekItem item, CancellationToken ct)
    {
        var (md, _, _) = await PeekService.ReadTextAsync(item.Path, 4 * 1024 * 1024);
        if (ct.IsCancellationRequested) return;
        await EnsureWebAsync();
        if (ct.IsCancellationRequested) return;
        var html = PeekService.MarkdownToHtml(md, ActualTheme == ElementTheme.Dark);
        WebHost.NavigateToString(html);
        WebHost.Visibility = Visibility.Visible;
    }

    private void ShowWebFile(PeekItem item)
    {
        // WebView2 renders PDFs (built-in Edge viewer) and HTML natively via file:// URIs.
        _ = NavigateWebFileAsync(item.Path);
    }

    private async Task NavigateWebFileAsync(string path)
    {
        try
        {
            await EnsureWebAsync();
            WebHost.Source = new Uri(path);
            WebHost.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            RenderMetadata(PeekService.Describe(path), missing: false, extraNote: ex.Message);
        }
    }

    private void ShowMedia(PeekItem item)
    {
        try
        {
            _player = new MediaPlayer { AutoPlay = false };
            MediaView.SetMediaPlayer(_player);
            _player.Source = MediaSource.CreateFromUri(new Uri(item.Path));
            MediaHost.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            RenderMetadata(item, missing: false, extraNote: ex.Message);
        }
    }

    private async Task ShowArchiveAsync(PeekItem item, CancellationToken ct)
    {
        var (entries, note) = await PeekService.ListArchiveAsync(item.Path);
        if (ct.IsCancellationRequested) return;

        if (note == "7zip-missing")
        {
            RenderMetadata(item, missing: false,
                extraNote: P("Install 7-Zip (from the Archives module) to list archive contents.",
                             "由「壓縮檔」模組安裝 7-Zip 就可以列出壓縮檔內容。"));
            return;
        }

        ArchiveList.ItemsSource = entries;
        long total = entries.Sum(en => en.Size);
        ArchiveSummary.Text = entries.Count == 0
            ? P("No listable entries.", "無可列出嘅項目。")
            : $"{entries.Count:N0} " + P("entries", "項") + $"   ·   {PeekService.HumanSize(total)} " + P("uncompressed", "未壓縮");
        FileMetaText.Text += $"   ·   {entries.Count:N0} " + P("entries", "項");
        ArchiveHost.Visibility = Visibility.Visible;
    }

    private void RenderMetadata(PeekItem item, bool missing, string? extraNote = null)
    {
        MetaPanel.Children.Clear();

        var bigIcon = new Image { Width = 64, Height = 64, HorizontalAlignment = HorizontalAlignment.Center };
        _ = LoadShellThumbAsync(item.Path, bigIcon, 96);
        MetaPanel.Children.Add(bigIcon);
        MetaPanel.Children.Add(new TextBlock
        {
            Text = item.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        });

        if (missing)
        {
            MetaPanel.Children.Add(MetaRow(P("Status", "狀態"), P("File not found", "搵唔到檔案")));
            MetaHost.Visibility = Visibility.Visible;
            return;
        }

        MetaPanel.Children.Add(MetaRow(P("Type", "類型"),
            string.IsNullOrEmpty(item.Extension) ? P("File (no extension)", "檔案（無副檔名）") : "." + item.Extension));
        MetaPanel.Children.Add(MetaRow(P("Size", "大小"),
            $"{item.SizeText}   ({item.SizeBytes:N0} " + P("bytes", "位元組") + ")"));
        MetaPanel.Children.Add(MetaRow(P("Modified", "修改時間"), item.Modified.ToString("yyyy-MM-dd HH:mm:ss")));
        MetaPanel.Children.Add(MetaRow(P("Created", "建立時間"), item.Created.ToString("yyyy-MM-dd HH:mm:ss")));
        MetaPanel.Children.Add(MetaRow(P("Location", "位置"), Path.GetDirectoryName(item.Path) ?? ""));

        if (!string.IsNullOrEmpty(extraNote))
            MetaPanel.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Informational,
                Message = extraNote,
                Margin = new Thickness(0, 8, 0, 0),
            });

        MetaHost.Visibility = Visibility.Visible;
    }

    private Grid MetaRow(string label, string value)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock
        {
            Text = label,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            FontSize = 13,
        };
        var v = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontSize = 13 };
        Grid.SetColumn(v, 1);
        g.Children.Add(l);
        g.Children.Add(v);
        return g;
    }

    private async Task EnsureWebAsync()
    {
        await WebHost.EnsureCoreWebView2Async();
    }

    private void TeardownMedia()
    {
        try
        {
            if (_player is not null)
            {
                _player.Pause();
                MediaView.SetMediaPlayer(null);
                _player.Dispose();
                _player = null;
            }
        }
        catch { }
    }

    // ===================== file icon / thumbnail (shell) =====================

    /// <summary>
    /// 載入 Shell 縮圖／圖示 · Load the shell thumbnail (or associated icon) for a file via the
    /// Storage thumbnail API — no System.Drawing dependency. Assigns onto <paramref name="target"/>
    /// on the UI thread; silently no-ops on failure (the Image just stays blank).
    /// </summary>
    private async Task LoadShellThumbAsync(string path, Image target, uint size)
    {
        try
        {
            if (!File.Exists(path)) return;
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(
                Windows.Storage.FileProperties.ThumbnailMode.SingleItem, size,
                Windows.Storage.FileProperties.ThumbnailOptions.UseCurrentScale);
            if (thumb is null || thumb.Size == 0) return;
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(thumb);
            target.Source = bmp;
        }
        catch { }
    }

    // ===================== global hotkey =====================

    private Thread? _hotkeyThread;
    private uint _hotkeyThreadId;
    private volatile bool _hotkeyRunning;

    private const int WM_HOTKEY = 0x0312;
    private const uint MOD_NOREPEAT = 0x4000;
    private const int HOTKEY_ID = 0xB33F;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public int ptX; public int ptY; }

    private void StartHotkeyIfEnabled()
    {
        bool enabled = SettingsStore.Get(HotkeyEnabledKey, "0") == "1";
        if (!enabled) { ShowHotkeyHint(); return; }
        uint mods = ParseUint(SettingsStore.Get(HotkeyModKey, "3")); // Ctrl+Alt = 0x2 | ... default below
        uint vk = ParseUint(SettingsStore.Get(HotkeyVkKey, "0x50"));  // 'P'
        StartHotkey(mods, vk);
        ShowHotkeyHint();
    }

    private static uint ParseUint(string s)
    {
        s = s.Trim();
        try
        {
            if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Convert.ToUInt32(s.Substring(2), 16);
            return uint.Parse(s);
        }
        catch { return 0; }
    }

    private void StartHotkey(uint mods, uint vk)
    {
        StopHotkey();
        if (mods == 0 || vk == 0) return;
        _hotkeyRunning = true;
        _hotkeyThread = new Thread(() =>
        {
            _hotkeyThreadId = GetCurrentThreadId();
            if (!RegisterHotKey(IntPtr.Zero, HOTKEY_ID, mods | MOD_NOREPEAT, vk))
            {
                _hotkeyRunning = false;
                return;
            }
            try
            {
                while (_hotkeyRunning && GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
                {
                    if (msg.message == WM_HOTKEY && (int)msg.wParam == HOTKEY_ID)
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            var sel = PeekService.TryGetExplorerSelection();
                            if (!string.IsNullOrEmpty(sel)) _ = LoadAsync(sel!);
                            else ShowHotkeyNote(P(
                                "Hotkey pressed, but no file is selected in a foreground Explorer window.",
                                "撳咗熱鍵，但檔案總管前景視窗未有揀檔案。"), InfoBarSeverity.Warning);
                        });
                    }
                }
            }
            finally { UnregisterHotKey(IntPtr.Zero, HOTKEY_ID); }
        })
        { IsBackground = true, Name = "PeekHotkey" };
        _hotkeyThread.SetApartmentState(ApartmentState.STA);
        _hotkeyThread.Start();
    }

    private void StopHotkey()
    {
        if (!_hotkeyRunning) return;
        _hotkeyRunning = false;
        try { if (_hotkeyThreadId != 0) PostThreadMessage(_hotkeyThreadId, 0x0012 /*WM_QUIT*/, IntPtr.Zero, IntPtr.Zero); } catch { }
        _hotkeyThread = null;
        _hotkeyThreadId = 0;
    }

    private void ShowHotkeyHint()
    {
        bool enabled = SettingsStore.Get(HotkeyEnabledKey, "0") == "1";
        if (!enabled) { HotkeyBar.IsOpen = false; return; }
        var chord = DescribeChord(ParseUint(SettingsStore.Get(HotkeyModKey, "3")), ParseUint(SettingsStore.Get(HotkeyVkKey, "0x50")));
        HotkeyBar.Severity = InfoBarSeverity.Informational;
        HotkeyBar.Title = P("Global hotkey active", "全域熱鍵已啟用");
        HotkeyBar.Message = P($"Press {chord} while a file is selected in Explorer to preview it here.",
                              $"喺檔案總管揀咗檔案時撳 {chord} 就會喺呢度預覽。");
        HotkeyBar.IsOpen = true;
    }

    private void ShowHotkeyNote(string msg, InfoBarSeverity sev)
    {
        HotkeyBar.Severity = sev;
        HotkeyBar.Title = "";
        HotkeyBar.Message = msg;
        HotkeyBar.IsOpen = true;
    }

    private static string DescribeChord(uint mods, uint vk)
    {
        var sb = new StringBuilder();
        if ((mods & 0x0002) != 0) sb.Append("Ctrl+");
        if ((mods & 0x0001) != 0) sb.Append("Alt+");
        if ((mods & 0x0004) != 0) sb.Append("Shift+");
        if ((mods & 0x0008) != 0) sb.Append("Win+");
        sb.Append((char)vk);
        return sb.ToString();
    }

    private async void HotkeyConfig_Click(object sender, RoutedEventArgs e)
    {
        bool enabled = SettingsStore.Get(HotkeyEnabledKey, "0") == "1";
        uint mods = ParseUint(SettingsStore.Get(HotkeyModKey, "3"));
        uint vk = ParseUint(SettingsStore.Get(HotkeyVkKey, "0x50"));

        var enableToggle = new ToggleSwitch
        {
            IsOn = enabled,
            OnContent = P("Enabled", "已啟用"),
            OffContent = P("Disabled", "已停用"),
        };

        var ctrl = new CheckBox { Content = "Ctrl", IsChecked = (mods & 0x0002) != 0 };
        var alt = new CheckBox { Content = "Alt", IsChecked = (mods & 0x0001) != 0 };
        var shift = new CheckBox { Content = "Shift", IsChecked = (mods & 0x0004) != 0 };
        var win = new CheckBox { Content = "Win", IsChecked = (mods & 0x0008) != 0 };
        var modRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modRow.Children.Add(ctrl); modRow.Children.Add(alt); modRow.Children.Add(shift); modRow.Children.Add(win);

        var keyBox = new TextBox
        {
            MaxLength = 1,
            Width = 60,
            Text = ((char)vk).ToString(),
            PlaceholderText = "P",
        };

        var panel = new StackPanel { Spacing = 12, MinWidth = 320 };
        panel.Children.Add(enableToggle);
        panel.Children.Add(new TextBlock { Text = P("Modifiers", "修飾鍵") });
        panel.Children.Add(modRow);
        panel.Children.Add(new TextBlock { Text = P("Key (a single letter or digit)", "按鍵（單一字母或數字）") });
        panel.Children.Add(keyBox);
        panel.Children.Add(new TextBlock
        {
            Text = P("Note: previewing the Explorer selection needs WinForge running un-elevated.",
                     "注意：預覽檔案總管選取需要 WinForge 以非管理員身分執行。"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var dlg = new ContentDialog
        {
            Title = P("Peek hotkey", "Peek 熱鍵"),
            Content = panel,
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        uint newMods = 0;
        if (ctrl.IsChecked == true) newMods |= 0x0002;
        if (alt.IsChecked == true) newMods |= 0x0001;
        if (shift.IsChecked == true) newMods |= 0x0004;
        if (win.IsChecked == true) newMods |= 0x0008;
        var keyChar = (keyBox.Text ?? "").Trim().ToUpperInvariant();
        uint newVk = keyChar.Length == 1 ? keyChar[0] : 0x50u;

        bool newEnabled = enableToggle.IsOn;
        if (newEnabled && (newMods == 0 || newVk == 0))
        {
            ShowHotkeyNote(P("Pick at least one modifier and a key.", "請最少揀一個修飾鍵同一個按鍵。"), InfoBarSeverity.Warning);
            return;
        }

        SettingsStore.Set(HotkeyEnabledKey, newEnabled ? "1" : "0");
        SettingsStore.Set(HotkeyModKey, newMods.ToString());
        SettingsStore.Set(HotkeyVkKey, "0x" + newVk.ToString("X"));

        StopHotkey();
        if (newEnabled) StartHotkey(newMods, newVk);
        ShowHotkeyHint();
        if (!newEnabled) ShowHotkeyNote(P("Hotkey disabled.", "熱鍵已停用。"), InfoBarSeverity.Success);
    }
}
