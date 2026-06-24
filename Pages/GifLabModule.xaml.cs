using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 一個畫面格（縮圖 + 來源 PNG 路徑）· One captured frame: its source PNG path, a thumbnail bitmap, and
/// the 1-based position shown in the strip. Bound by the GIF Studio frame strip.
/// </summary>
public sealed class FrameItem
{
    public string Path { get; init; } = "";
    public BitmapImage Thumb { get; init; } = new();
    public int Index { get; set; }
}

/// <summary>
/// GIF 工作室 · GIF Studio — a ScreenToGif-style tool: capture a screen region / window / fullscreen to
/// PNG frames, edit them (delete · reorder by drag or move-left/right · uniform crop), preview the
/// loop, then export to GIF / MP4 / APNG with per-export fps, scale and loop. All in-app (ffmpeg
/// gdigrab + palettegen). Bilingual. 全部喺 app 內做。
/// </summary>
public sealed partial class GifLabModule : Page
{
    private readonly ObservableCollection<FrameItem> _frames = new();
    private readonly DispatcherTimer _capTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _playTimer = new();
    private int _elapsed;
    private int _playIdx;
    private bool _playing;
    private CancellationTokenSource? _busyCts;

    // win32: foreground-window rect (for "active window" capture) + DPI-aware virtual metrics
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int i);
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }
    private const int SM_XVIRTUALSCREEN = 76, SM_YVIRTUALSCREEN = 77, SM_CXVIRTUALSCREEN = 78, SM_CYVIRTUALSCREEN = 79;

    public GifLabModule()
    {
        InitializeComponent();
        FrameStrip.ItemsSource = _frames;
        _capTimer.Tick += (_, _) => { _elapsed++; UpdateStatus(); };
        _playTimer.Tick += PlayTick;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); SyncButtons(); RefreshFrames(); };
        Unloaded += (_, _) => { _capTimer.Stop(); _playTimer.Stop(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string Msg(TweakResult r) => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Render()
    {
        HeaderTitle.Text = "GIF Studio · 螢幕轉 GIF";
        HeaderBlurb.Text = P("Record a screen region, window or the whole screen into frames, tidy them up — delete, reorder (drag or move buttons), uniform crop — preview the loop, then export to GIF, MP4 or APNG. Everything runs in-app via ffmpeg.",
            "錄一忽螢幕、一個視窗或者成個畫面做一格格畫面，再執靚佢 — 刪格、調次序（拖或者用按鈕）、統一裁切 — 預覽個循環，最後匯出做 GIF、MP4 或 APNG。全部喺 app 內用 ffmpeg 做。");

        CapLabel.Text = P("1 · Capture frames", "1 · 影低畫面格");
        CapBlurb.Text = P("Pick a source, set the frame rate, and an optional auto-stop duration (0 = stop manually). Region capture lets you drag a rectangle; Esc or right-click cancels.",
            "揀來源、設定幀率，同埋一個可選嘅自動停止秒數（0 = 自己手動停）。區域擷取要你拖一個框；Esc 或右鍵取消。");
        SourceCap.Text = P("Source", "來源");
        SrcRegion.Content = P("Screen region (drag)", "螢幕區域（拖框）");
        SrcWindow.Content = P("Active window", "使用中視窗");
        SrcFull.Content = P("Full screen", "全螢幕");
        FpsCap.Text = P("Capture fps", "擷取幀率");
        DurCap.Text = P("Duration (s)", "時長（秒）");
        CaptureBtn.Content = P("● Capture", "● 開始擷取");
        StopBtn.Content = P("■ Stop", "■ 停止");

        FramesLabel.Text = P("2 · Frames", "2 · 畫面格");
        FramesBlurb.Text = P("Select one or more frames (drag to reorder). Use the buttons to move, delete or crop. Crop applies to ALL frames uniformly.",
            "揀一格或者多格（拖嚟調次序）。用啲按鈕嚟移動、刪除或裁切。裁切會統一套用落所有格。");
        MoveLeftText.Text = P("Move left", "左移");
        MoveRightText.Text = P("Move right", "右移");
        DeleteText.Text = P("Delete", "刪除");
        CropText.Text = P("Crop all…", "裁切全部…");
        ReloadText.Text = P("Reload", "重新載入");
        ClearText.Text = P("Clear all", "清空");
        EmptyHint.Text = P("No frames yet — capture some above.", "未有畫面格 — 喺上面影低先。");

        PreviewLabel.Text = P("3 · Preview", "3 · 預覽");
        PlayText.Text = _playing ? P("Pause", "暫停") : P("Play", "播放");
        PlayIcon.Glyph = _playing ? "" : "";

        ExportLabel.Text = P("4 · Export", "4 · 匯出");
        FormatCap.Text = P("Format", "格式");
        OutFpsCap.Text = P("Output fps", "輸出幀率");
        ScaleCap.Text = P("Width", "寬度");
        ScaleOrig.Content = P("Original", "原本");
        LoopCap.Text = P("Loop", "循環");
        LoopForever.Content = P("Forever", "永遠");
        LoopOnce.Content = P("Play once", "播一次");
        LoopThree.Content = P("3 times", "3 次");
        ExportBtn.Content = P("Export…", "匯出…");

        UpdateFrameCount();
        UpdateStatus();

        if (!MediaService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            EngineBar.Message = P("Click to install ffmpeg automatically (winget) — needed to capture and export. No restart needed.",
                "撳一下自動安裝 ffmpeg（winget）— 擷取同匯出都要佢。唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "Gyan.FFmpeg", "Install ffmpeg automatically", "自動安裝 ffmpeg",
                () => { Render(); return Task.CompletedTask; }, MediaService.Rescan);
        }
        else { EngineBar.IsOpen = false; EngineBar.ActionButton = null; }
    }

    // ===================== Capture =====================

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (GifLabService.IsCapturing) return;
        int fps = (int)FpsBox.Value;
        int dur = (int)DurBox.Value;

        (int x, int y, int w, int h)? region = SourceBox.SelectedIndex switch
        {
            1 => ForegroundWindowRect(),
            2 => FullScreenRect(),
            _ => RegionSelector.PickRegion(),
        };

        if (region is null)
        {
            ShowBar(CapBar, false, P("Cancelled", "已取消"), P("No region was selected.", "未揀區域。"));
            return;
        }

        var (rx, ry, rw, rh) = region.Value;
        var r = GifLabService.StartCapture(rx, ry, rw, rh, fps, dur);
        if (!r.Success) { ShowBar(CapBar, false, P("Failed", "失敗"), Msg(r)); return; }

        _elapsed = 0;
        _capTimer.Start();
        CapBar.IsOpen = false;
        SyncButtons();

        if (dur > 0)
        {
            // self-terminating capture — wait for ffmpeg, then load frames
            await GifLabService.WaitForCaptureExit();
            _capTimer.Stop();
            SyncButtons();
            RefreshFrames();
            ShowBar(CapBar, true, P("Captured", "已擷取"),
                P($"Captured {_frames.Count} frames.", $"已擷取 {_frames.Count} 格。"));
        }
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        _capTimer.Stop();
        StopBtn.IsEnabled = false;
        var r = await GifLabService.StopCapture();
        SyncButtons();
        RefreshFrames();
        ShowBar(CapBar, r.Success, r.Success ? P("Captured", "已擷取") : P("Failed", "失敗"),
            r.Success ? P($"Captured {_frames.Count} frames.", $"已擷取 {_frames.Count} 格。") : Msg(r));
    }

    private (int, int, int, int)? ForegroundWindowRect()
    {
        var h = GetForegroundWindow();
        if (h == IntPtr.Zero || !GetWindowRect(h, out var r)) return null;
        int w = r.Right - r.Left, ht = r.Bottom - r.Top;
        if (w < 4 || ht < 4) return null;
        w -= w % 2; ht -= ht % 2;
        return (r.Left, r.Top, w, ht);
    }

    private (int, int, int, int)? FullScreenRect()
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN), y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int w = GetSystemMetrics(SM_CXVIRTUALSCREEN), h = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (w <= 0 || h <= 0) return null;
        w -= w % 2; h -= h % 2;
        return (x, y, w, h);
    }

    // ===================== Frame strip =====================

    private void RefreshFrames()
    {
        StopPlayback();
        _frames.Clear();
        foreach (var path in GifLabService.ListFrames())
        {
            var bmp = new BitmapImage { DecodePixelWidth = 160 };
            try
            {
                bmp.UriSource = new Uri(path);
            }
            catch { }
            _frames.Add(new FrameItem { Path = path, Thumb = bmp });
        }
        Renumber();
        UpdateFrameCount();
        bool any = _frames.Count > 0;
        EmptyHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        PreviewCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        ExportCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (any) ShowPreviewFrame(0);
    }

    private void Renumber()
    {
        for (int i = 0; i < _frames.Count; i++) _frames[i].Index = i + 1;
    }

    private void UpdateFrameCount()
    {
        FrameCountText.Text = P($"{_frames.Count} frames", $"{_frames.Count} 格");
    }

    private void FrameStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FrameStrip.SelectedItems.Count > 0 && FrameStrip.SelectedItem is FrameItem fi)
            ShowPreviewFrame(_frames.IndexOf(fi));
        SyncButtons();
    }

    private void MoveLeft_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveRight_Click(object sender, RoutedEventArgs e) => MoveSelected(+1);

    private void MoveSelected(int delta)
    {
        var sel = FrameStrip.SelectedItems.OfType<FrameItem>()
            .OrderBy(f => _frames.IndexOf(f)).ToList();
        if (sel.Count == 0) return;
        if (delta > 0) sel.Reverse(); // move rightmost first to avoid clobbering

        foreach (var f in sel)
        {
            int i = _frames.IndexOf(f);
            int j = i + delta;
            if (j < 0 || j >= _frames.Count) continue;
            _frames.Move(i, j);
        }
        Renumber();
        FrameStrip.SelectedItems.Clear();
        foreach (var f in sel) FrameStrip.SelectedItems.Add(f);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var sel = FrameStrip.SelectedItems.OfType<FrameItem>().ToList();
        if (sel.Count == 0)
        {
            ShowBar(EditBar, false, P("Nothing selected", "未揀任何格"),
                P("Select one or more frames first.", "請先揀一格或多格。"));
            return;
        }
        foreach (var f in sel)
        {
            _frames.Remove(f);
            try { if (File.Exists(f.Path)) File.Delete(f.Path); } catch { }
        }
        Renumber();
        UpdateFrameCount();
        bool any = _frames.Count > 0;
        EmptyHint.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        PreviewCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        ExportCard.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        if (any) ShowPreviewFrame(0); else { StopPlayback(); PreviewImage.Source = null; }
        ShowBar(EditBar, true, P("Deleted", "已刪除"),
            P($"Deleted {sel.Count} frame(s).", $"已刪除 {sel.Count} 格。"));
        await Task.CompletedTask;
    }

    private async void Crop_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Count == 0) return;
        if (!MediaService.IsInstalled) { ShowBar(EditBar, false, P("ffmpeg not found", "搵唔到 ffmpeg"), ""); return; }

        // measure the source frame so the crop box maxes out at the real pixel size
        var (fw, fh) = await MeasureFrame(_frames[0].Path);
        if (fw <= 0 || fh <= 0) { ShowBar(EditBar, false, P("Failed", "失敗"), P("Could not read the frame size.", "讀唔到畫面尺寸。")); return; }
        var dlg = BuildCropDialog(fw, fh, out var xBox, out var yBox, out var wBox, out var hBox);
        dlg.XamlRoot = this.XamlRoot;
        var res = await dlg.ShowAsync();
        if (res != ContentDialogResult.Primary) return;

        int cx = (int)xBox.Value, cy = (int)yBox.Value, cw = (int)wBox.Value, ch = (int)hBox.Value;
        if (cw < 2 || ch < 2 || cx + cw > fw || cy + ch > fh)
        {
            ShowBar(EditBar, false, P("Invalid crop", "裁切無效"),
                P("The crop rectangle is outside the frame.", "裁切框超出咗畫面範圍。"));
            return;
        }

        StartBusy(EditBar, P("Cropping all frames…", "裁切緊所有格…"));
        var paths = _frames.Select(f => f.Path).ToList();
        var r = await GifLabService.CropFrames(paths, cx, cy, cw, ch, _busyCts!.Token);
        EndBusy();
        RefreshFrames();
        ShowBar(EditBar, r.Success, r.Success ? P("Cropped", "已裁切") : P("Failed", "失敗"),
            r.Success ? P("Applied the crop to every frame.", "已將裁切套用落每一格。") : Msg(r));
    }

    private ContentDialog BuildCropDialog(int fw, int fh, out NumberBox xBox, out NumberBox yBox, out NumberBox wBox, out NumberBox hBox)
    {
        xBox = new NumberBox { Header = "X", Value = 0, Minimum = 0, Maximum = fw, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        yBox = new NumberBox { Header = "Y", Value = 0, Minimum = 0, Maximum = fh, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        wBox = new NumberBox { Header = P("Width", "寬"), Value = fw, Minimum = 2, Maximum = fw, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        hBox = new NumberBox { Header = P("Height", "高"), Value = fh, Minimum = 2, Maximum = fh, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = P($"Source frame is {fw} × {fh}. Set the crop rectangle (pixels).", $"原始畫面係 {fw} × {fh}。設定裁切框（像素）。"),
            TextWrapping = TextWrapping.Wrap,
        });
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        Grid.SetColumn(xBox, 0); Grid.SetRow(xBox, 0);
        Grid.SetColumn(yBox, 1); Grid.SetRow(yBox, 0);
        Grid.SetColumn(wBox, 0); Grid.SetRow(wBox, 1);
        Grid.SetColumn(hBox, 1); Grid.SetRow(hBox, 1);
        grid.Children.Add(xBox); grid.Children.Add(yBox); grid.Children.Add(wBox); grid.Children.Add(hBox);
        panel.Children.Add(grid);
        return new ContentDialog
        {
            Title = P("Crop all frames", "裁切所有格"),
            Content = panel,
            PrimaryButtonText = P("Crop", "裁切"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
    }

    private static async Task<(int w, int h)> MeasureFrame(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var d = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(fs.AsRandomAccessStream());
            return ((int)d.PixelWidth, (int)d.PixelHeight);
        }
        catch { return (0, 0); }
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => RefreshFrames();

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
        GifLabService.Reset();
        _frames.Clear();
        UpdateFrameCount();
        EmptyHint.Visibility = Visibility.Visible;
        PreviewCard.Visibility = Visibility.Collapsed;
        ExportCard.Visibility = Visibility.Collapsed;
        PreviewImage.Source = null;
        SyncButtons();
        ShowBar(EditBar, true, P("Cleared", "已清空"), P("Removed all frames.", "已移除所有格。"));
    }

    // ===================== Preview playback =====================

    private void ShowPreviewFrame(int idx)
    {
        if (idx < 0 || idx >= _frames.Count) return;
        _playIdx = idx;
        PreviewImage.Source = _frames[idx].Thumb;
        PreviewPos.Text = $"{idx + 1}/{_frames.Count}";
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Count == 0) return;
        if (_playing) StopPlayback();
        else
        {
            _playing = true;
            int fps = Math.Clamp((int)OutFpsBox.Value, 1, 60);
            _playTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            _playTimer.Start();
        }
        Render();
    }

    private void PlayTick(object? sender, object e)
    {
        if (_frames.Count == 0) { StopPlayback(); return; }
        _playIdx = (_playIdx + 1) % _frames.Count;
        ShowPreviewFrame(_playIdx);
    }

    private void StopPlayback()
    {
        if (!_playing) return;
        _playing = false;
        _playTimer.Stop();
        PlayText.Text = P("Play", "播放");
        PlayIcon.Glyph = "";
    }

    // ===================== Export =====================

    private void Format_Changed(object sender, SelectionChangedEventArgs e)
    {
        // MP4 has no loop concept
        if (LoopRow is not null)
            LoopRow.Visibility = FormatBox.SelectedIndex == 1 ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_frames.Count == 0) return;
        if (!MediaService.IsInstalled) { ShowBar(ExportBar, false, P("ffmpeg not found", "搵唔到 ffmpeg"), ""); return; }
        StopPlayback();

        var fmt = FormatBox.SelectedIndex switch
        {
            1 => GifLabService.ExportFormat.Mp4,
            2 => GifLabService.ExportFormat.Apng,
            _ => GifLabService.ExportFormat.Gif,
        };
        var ext = fmt switch
        {
            GifLabService.ExportFormat.Mp4 => ".mp4",
            GifLabService.ExportFormat.Apng => ".apng",
            _ => ".gif",
        };

        var path = await FileDialogs.SaveFileAsync($"WinForge-gif-{DateTime.Now:yyyyMMdd-HHmmss}", ext);
        if (path is null) return;

        int fps = (int)OutFpsBox.Value;
        int scale = int.TryParse((ScaleBox.SelectedItem as ComboBoxItem)?.Tag as string, out var sv) ? sv : 0;
        int loop = int.TryParse((LoopBox.SelectedItem as ComboBoxItem)?.Tag as string, out var lv) ? lv : 0;

        ExportRing.IsActive = true;
        ExportBtn.IsEnabled = false;
        ExportBar.IsOpen = false;
        var ordered = _frames.Select(f => f.Path).ToList();
        var r = await GifLabService.Export(ordered, path, fmt, fps, scale, loop);
        ExportRing.IsActive = false;
        ExportBtn.IsEnabled = true;
        ShowBar(ExportBar, r.Success, r.Success ? P("Exported", "已匯出") : P("Failed", "失敗"),
            r.Success ? path : Msg(r));
    }

    // ===================== shared helpers =====================

    private void SyncButtons()
    {
        bool cap = GifLabService.IsCapturing;
        bool hasFrames = _frames.Count > 0;
        bool hasSel = FrameStrip.SelectedItems.Count > 0;

        CaptureBtn.IsEnabled = !cap;
        StopBtn.IsEnabled = cap;
        SourceBox.IsEnabled = !cap;
        FpsBox.IsEnabled = !cap;
        DurBox.IsEnabled = !cap;
        Dot.Visibility = cap ? Visibility.Visible : Visibility.Collapsed;

        MoveLeftBtn.IsEnabled = hasSel;
        MoveRightBtn.IsEnabled = hasSel;
        DeleteBtn.IsEnabled = hasSel;
        CropBtn.IsEnabled = hasFrames;
        ClearBtn.IsEnabled = hasFrames;
        ExportBtn.IsEnabled = hasFrames;
    }

    private void UpdateStatus()
    {
        StatusText.Text = GifLabService.IsCapturing
            ? P($"REC  {_elapsed / 60:00}:{_elapsed % 60:00}", $"錄緊  {_elapsed / 60:00}:{_elapsed % 60:00}")
            : P("Idle", "閒置");
    }

    private void StartBusy(InfoBar bar, string msg)
    {
        _busyCts = new CancellationTokenSource();
        BusyRing.IsActive = true;
        bar.Severity = InfoBarSeverity.Informational;
        bar.Title = msg;
        bar.Message = "";
        bar.IsOpen = true;
    }

    private void EndBusy()
    {
        BusyRing.IsActive = false;
        _busyCts?.Dispose();
        _busyCts = null;
    }

    private static void ShowBar(InfoBar bar, bool ok, string title, string message)
    {
        bar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        bar.Title = title;
        bar.Message = message;
        bar.IsOpen = true;
    }
}
