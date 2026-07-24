using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 媒體模組 · In-app Media module: wraps ffmpeg/ffprobe — convert, trim, GIF, grab frames, inspect, plus ~60 ops.
/// Browse uses the Win32 file dialogs so it works whether or not WinForge runs elevated.
///
/// 每個進階操作用手砌嘅控件列渲染（唔再用 TweakCard）：左邊雙語標題／說明，右邊對應控件。
/// Each advanced operation is rendered as a hand-built control row (no TweakCard): bilingual
/// title/description on the left, the matching WinUI control on the right.
/// </summary>
public sealed partial class MediaModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _busy;
    private bool _rowBusy;
    private bool _workflowBusy;
    private CancellationTokenSource? _workflowCts;
    private IReadOnlyList<string> _concatClips = Array.Empty<string>();
    private string _subtitlePath = string.Empty;
    private string _chapterOutputFolder = string.Empty;
    private string _photoInputFolder = string.Empty;
    private string _photoOutputFolder = string.Empty;

    private static readonly string[] MediaExts =
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv", ".flv", ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus", ".jpg", ".jpeg", ".png", ".webp", ".heic", ".heif", ".jxl", ".tif", ".tiff" };

    private static readonly string[] VideoExts =
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv", ".flv" };

    private static readonly HashSet<string> AudioExts = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus" };

    public MediaModule()
    {
        InitializeComponent();
        // Typed NumberBox defaults are assigned after InitializeComponent because this runtime has
        // reproduced XamlParseException for some typed XAML literals.
        NvencQualityBox.Value = 26;
        TargetSizeBox.Value = 25;
        TargetAudioBox.Value = 128;
        SubtitleModeBox.SelectedIndex = 0;
        PhotoFormatBox.SelectedIndex = 0;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildQuickOps();
        PopulateOps(string.Empty);
        RefreshSelection();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _workflowCts?.Cancel();
        _workflowCts?.Dispose();
        _workflowCts = null;
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        BuildQuickOps();
        PopulateOps(OpsFilter?.Text ?? string.Empty);
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Media · 媒體";
        HeaderBlurb.Text = P("Convert, repair, deliver, and inspect video, audio, and photos with bounded ffmpeg/ffprobe workflows — all in-app.",
            "用有界限嘅 ffmpeg／ffprobe 工作流程轉檔、修復、交付同檢視影片、音訊、相片 — 全部喺 app 內。");

        StudioHeader.Text = P("Guided studio workflows", "引導式媒體工作流程");
        StudioDescription.Text = P(
            "Choose an input here, then run measured two-pass, repair, delivery, chapter, or privacy workflows. Outputs are staged safely and promoted only after ffmpeg succeeds; Cancel terminates the active child process.",
            "先喺呢度揀輸入，再用量度式兩步、修復、交付、章節或者私隱工作流程。輸出會先安全暫存，ffmpeg 成功先正式取代；取消會終止進行中子程序。");
        StudioPickInputBtn.Content = P("Choose workflow input…", "揀工作流程輸入…");
        CancelWorkflowBtn.Content = P("Cancel active workflow", "取消進行中工作流程");

        AudioWorkflowTitle.Text = P("Broadcast loudness & silence", "廣播響度同靜音整理");
        AudioWorkflowDescription.Text = P("Measured EBU R128 normalization plus audio-only start/end/internal-gap silence removal.",
            "量度式 EBU R128 正規化，加埋只限音訊嘅頭尾／中間靜音剪走功能。");
        NormalizeR128Btn.Content = P("Normalize to EBU R128 (measure + apply)", "正規化做 EBU R128（量度＋套用）");
        TrimSilenceBtn.Content = P("Auto-trim audio silence (start, end & gaps)", "自動剪走音訊頭尾同中間靜音");

        VideoWorkflowTitle.Text = P("Video repair & lossless joining", "影片修復同無損合併");
        VideoWorkflowDescription.Text = P("Two-pass vidstab, cropdetect-to-crop, and concat-demuxer stream copy.",
            "兩步 vidstab、防黑邊偵測後裁剪，同 concat demuxer 無重編碼合併。");
        StabilizeBtn.Content = P("Stabilize video (vidstab two-pass)", "影片防震（vidstab 兩步）");
        AutoCropBtn.Content = P("Detect and crop black bars", "偵測同裁走黑邊");
        ConcatLabel.Text = P("Clips are joined in the order shown; codecs and stream parameters must match.",
            "片段會按顯示次序合併；codec 同 stream 參數要一致。");
        ChooseConcatBtn.Content = P("Choose clips…", "揀片段…");
        JoinConcatBtn.Content = P("Join without re-encoding…", "無重編碼合併…");

        DeliveryWorkflowTitle.Text = P("Hardware & delivery encoding", "硬件同交付編碼");
        DeliveryWorkflowDescription.Text = P("Probe real NVENC hardware, hit a target file-size cap, or burn/mux SRT and ASS subtitles.",
            "實測 NVENC 硬件、命中目標檔案容量，或者燒入／軟掛 SRT 同 ASS 字幕。");
        NvencLabel.Text = P("NVIDIA NVENC (encoder + live hardware probe; CQ 0–51)", "NVIDIA NVENC（編碼器＋即時硬件測試；CQ 0–51）");
        DetectNvencBtn.Content = P("Detect working NVENC encoders", "偵測可用 NVENC 編碼器");
        EncodeNvencBtn.Content = P("Encode with selected NVENC codec", "用已揀 NVENC codec 編碼");
        TargetSizeLabel.Text = P("Two-pass target size (MiB · audio kbps)", "兩步目標容量（MiB · 音訊 kbps）");
        TargetSizeBtn.Content = P("Encode to target size", "按目標容量編碼");
        SubtitleLabel.Text = P("SRT / ASS subtitles", "SRT／ASS 字幕");
        PickSubtitleBtn.Content = P("Choose subtitle file…", "揀字幕檔…");
        BurnInSubtitleItem.Content = P("Burn into picture (libass)", "燒入畫面（libass）");
        SoftMuxSubtitleItem.Content = P("Add toggleable track (soft mux)", "加入可開關字幕軌（軟掛）");
        SubtitleRunBtn.Content = P("Apply subtitle workflow", "執行字幕工作流程");

        PhotoChapterWorkflowTitle.Text = P("Chapters, photo conversion & privacy", "章節、相片轉換同私隱");
        PhotoChapterWorkflowDescription.Text = P("Read/split ffprobe chapters, batch-convert HEIC/JXL, and remove EXIF/GPS without re-encoding.",
            "讀取／分割 ffprobe 章節、批次轉 HEIC／JXL，同無重編碼移除 EXIF／GPS。");
        ChapterLabel.Text = P("Extract chapter metadata and split each chapter by timestamp", "抽取章節 metadata，再按時間逐章分割");
        PickChapterFolderBtn.Content = P("Choose chapter output folder…", "揀章節輸出資料夾…");
        ReadChaptersBtn.Content = P("Read chapters", "讀取章節");
        SplitChaptersBtn.Content = P("Split all chapters", "分割全部章節");
        PhotoBatchLabel.Text = P($"Batch HEIC / HEIF / JPEG-XL conversion (maximum {MediaWorkflowExecutor.MaxBatchFiles})",
            $"批次轉 HEIC／HEIF／JPEG-XL（最多 {MediaWorkflowExecutor.MaxBatchFiles} 張）");
        PickPhotoInputBtn.Content = P("Choose source folder…", "揀來源資料夾…");
        PickPhotoOutputBtn.Content = P("Choose separate output folder…", "揀另一個輸出資料夾…");
        PhotoJpegItem.Content = "JPG";
        PhotoPngItem.Content = "PNG";
        ConvertPhotosBtn.Content = P("Convert photo batch", "批次轉相");
        StripMetadataBtn.Content = P("Strip EXIF, GPS & image metadata", "移除 EXIF、GPS 同相片 metadata");

        SelLabel.Text = P("Files", "檔案");
        InCap.Text = P("Input", "輸入");
        OutCap.Text = P("Output", "輸出");
        InputBtn.Content = P("Open…", "開啟…");
        OutputBtn.Content = P("Save as…", "另存…");
        QuickLabel.Text = P("Quick conversions", "快速轉檔");
        TrimLabel.Text = P("Trim (start + length, HH:MM:SS)", "剪裁（開始 + 長度，HH:MM:SS）");
        TrimCopyBtn.Content = P("Trim (no re-encode)", "剪裁（唔重編碼）");
        TrimEncodeBtn.Content = P("Trim (re-encode)", "剪裁（重編碼）");
        GifLabel.Text = P("GIF / frame (fps · width)", "GIF／畫格（fps · 闊度）");
        GifBtn.Content = P("Make GIF", "整 GIF");
        FrameBtn.Content = P("Grab frame", "擷取畫格");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
        AdvancedHeader.Text = P($"Advanced operations ({(_ops ??= MediaOperations.All().ToList()).Count})",
            $"進階操作（{(_ops ??= MediaOperations.All().ToList()).Count}）");

        if (!MediaService.IsInstalled)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            EngineBar.Message = P("Install ffmpeg automatically (winget) with live progress — no restart needed.",
                "自動安裝 ffmpeg（winget），即時睇住進度 — 唔使重開。");
            // Rich install control: real progress bar + live bilingual status + % + Cancel + success/error animation.
            EngineBar.Content = EngineBars.AutoInstallProgress(
                "Gyan.FFmpeg", "Install ffmpeg automatically", "自動安裝 ffmpeg",
                recheck: () => { Render(); BuildQuickOps(); return Task.CompletedTask; },
                rescan: MediaService.Rescan);
        }
        else { EngineBar.IsOpen = false; EngineBar.Content = null; }
    }

    private void RefreshSelection()
    {
        InputBox.Text = AppState.CurrentMediaInput;
        OutputBox.Text = AppState.CurrentMediaOutput;
    }

    private void BuildQuickOps()
    {
        QuickOps.Children.Clear();
        AddQuick(P("To MP4", "轉 MP4"), () => MediaService.Quick(".converted.mp4", "-i {in} -c:v libx264 -c:a aac -movflags +faststart {out}"));
        AddQuick(P("To WebM", "轉 WebM"), () => MediaService.Quick(".webm", "-i {in} -c:v libvpx-vp9 -b:v 0 -crf 32 -c:a libopus {out}"));
        AddQuick(P("To MKV", "轉 MKV"), () => MediaService.Quick(".mkv", "-i {in} -c copy {out}"));
        AddQuick(P("Extract MP3", "抽 MP3"), () => MediaService.Quick(".mp3", "-i {in} -vn -c:a libmp3lame -q:a 2 {out}"));
        AddQuick(P("Extract WAV", "抽 WAV"), () => MediaService.Quick(".wav", "-i {in} -vn -c:a pcm_s16le {out}"));
        AddQuick(P("GIF", "GIF"), () => MediaService.Quick(".gif", "-i {in} -vf \"fps=12,scale=480:-1:flags=lanczos\" {out}"));
        AddQuick(P("Compress", "壓細"), () => MediaService.Quick(".compressed.mp4", "-i {in} -c:v libx264 -crf 28 -c:a aac {out}"));
        AddQuick(P("Mute", "靜音"), () => MediaService.Quick(".muted.mp4", "-i {in} -c:v copy -an {out}"));
        AddQuick(P("Normalize audio", "正規化音量"), () => MediaService.Quick(".norm.mp4", "-i {in} -af loudnorm -c:v copy {out}"));
        AddQuick(P("Info", "資訊"), () => MediaService.Info());
    }

    private void AddQuick(string label, Func<Task<TweakResult>> run)
    {
        var btn = new Button { Content = label };
        btn.Click += async (_, _) => await RunAndShow(btn, run);
        QuickOps.Children.Add(btn);
    }

    private async Task RunAndShow(Button btn, Func<Task<TweakResult>> run)
    {
        if (_busy || _workflowBusy) return;
        _busy = true;
        var label = btn.Content;
        btn.IsEnabled = false;
        btn.Content = new ProgressRing { IsActive = true, Width = 16, Height = 16 };
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = P("Running ffmpeg…", "執行緊 ffmpeg…");
        try
        {
            var r = await run();
            var head = r.Success ? P("✓ Done", "✓ 完成") : P("✗ Failed", "✗ 失敗");
            var body = string.IsNullOrWhiteSpace(r.Output)
                ? ((Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "")
                : r.Output!;
            OutText.Text = head + "\n" + (body.Length > 4000 ? body[^4000..] : body);
        }
        catch (Exception ex) { OutText.Text = ex.Message; }
        finally { btn.Content = label; btn.IsEnabled = true; _busy = false; RefreshSelection(); }
    }

    private async Task RunWorkflowAsync(Button button, Func<CancellationToken, Task<TweakResult>> run)
    {
        if (_busy || _workflowBusy) return;
        _workflowBusy = true;
        var originalContent = button.Content;
        button.IsEnabled = false;
        button.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
        ResultBar.IsOpen = false;
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = P("Running a bounded ffmpeg workflow…", "執行緊有界限嘅 ffmpeg 工作流程…");

        var cts = new CancellationTokenSource();
        _workflowCts = cts;
        CancelWorkflowBtn.Visibility = Visibility.Visible;
        CancelWorkflowBtn.IsEnabled = true;
        try
        {
            var result = await run(cts.Token);
            ShowWorkflowResult(result);
        }
        catch (OperationCanceledException)
        {
            ShowWorkflowResult(TweakResult.Fail("Cancelled.", "已取消。"));
        }
        catch (Exception ex)
        {
            ShowWorkflowResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"));
        }
        finally
        {
            if (ReferenceEquals(_workflowCts, cts)) _workflowCts = null;
            cts.Dispose();
            CancelWorkflowBtn.Visibility = Visibility.Collapsed;
            CancelWorkflowBtn.IsEnabled = false;
            button.Content = originalContent;
            button.IsEnabled = true;
            _workflowBusy = false;
            RefreshSelection();
        }
    }

    private void ShowWorkflowResult(TweakResult result)
    {
        ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = result.Success ? P("Workflow complete", "工作流程完成") : P("Workflow stopped", "工作流程未完成");
        ResultBar.Message = result.Message?.Get(Loc.I.Language) ?? string.Empty;
        ResultBar.IsOpen = true;

        var detail = result.Output?.Trim();
        var summary = result.Message?.Get(Loc.I.Language) ?? string.Empty;
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = string.IsNullOrWhiteSpace(detail)
            ? summary
            : summary + Environment.NewLine + Environment.NewLine + (detail.Length > 8000 ? detail[^8000..] : detail);
    }

    private void CancelWorkflow_Click(object sender, RoutedEventArgs e)
    {
        CancelWorkflowBtn.IsEnabled = false;
        OutText.Text = P("Cancelling and cleaning owned temporary files…", "取消緊，同時清理自家暫存檔…");
        _workflowCts?.Cancel();
    }

    private void StudioPickInput_Click(object sender, RoutedEventArgs e) => PickInput_Click(sender, e);

    private async void NormalizeR128_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var output = DeriveTagged(".r128-normalized");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.NormalizeLoudnessAsync(MediaService.Input, output, ct));
    }

    private async void TrimSilence_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        if (!AudioExts.Contains(Path.GetExtension(MediaService.Input)))
        {
            ShowWorkflowResult(TweakResult.Fail(
                "Choose an audio file for silence-gap removal; collapsing audio gaps in a video would desynchronize it.",
                "請揀音訊檔先做靜音清理；直接剪走影片聲軌中間空位會令畫面同聲音甩 sync。"));
            return;
        }
        var output = DeriveTagged(".silence-trimmed");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.TrimSilenceAsync(MediaService.Input, output, ct));
    }

    private async void Stabilize_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var output = DeriveTagged(".stabilized", ".mp4");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.StabilizeVideoAsync(MediaService.Input, output, ct));
    }

    private async void AutoCrop_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var output = DeriveTagged(".cropped", ".mp4");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.AutoCropAsync(MediaService.Input, output, ct));
    }

    private async void ChooseConcat_Click(object sender, RoutedEventArgs e)
    {
        var clips = await FileDialogs.OpenFilesAsync(FileDialogs.BuildFilters(VideoExts),
            P("Choose clips in join order", "按合併次序揀片段"));
        if (clips.Count == 0) return;
        _concatClips = clips;
        ConcatClipsList.ItemsSource = clips.Select((path, index) => $"{index + 1}. {Path.GetFileName(path)}").ToList();
    }

    private async void JoinConcat_Click(object sender, RoutedEventArgs e)
    {
        if (_concatClips.Count < 2)
        {
            ShowWorkflowResult(TweakResult.Fail("Choose at least two clips first.", "請先揀最少兩段片。"));
            return;
        }
        var extension = Path.GetExtension(_concatClips[0]);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".mkv";
        var output = await FileDialogs.SaveFileAsync("joined" + extension, extension);
        if (output is null) return;
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.ConcatCopyAsync(_concatClips, output, ct));
    }

    private async void DetectNvenc_Click(object sender, RoutedEventArgs e)
    {
        await RunWorkflowAsync((Button)sender, async ct =>
        {
            var scan = await MediaWorkflowService.DetectNvencAsync(ct);
            NvencCodecBox.Items.Clear();
            foreach (var codec in scan.Codecs)
                NvencCodecBox.Items.Add(new ComboBoxItem { Content = codec, Tag = codec });
            if (NvencCodecBox.Items.Count > 0) NvencCodecBox.SelectedIndex = 0;
            return scan.Result;
        });
    }

    private async void EncodeNvenc_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        if (NvencCodecBox.SelectedItem is not ComboBoxItem { Tag: string codec })
        {
            ShowWorkflowResult(TweakResult.Fail("Detect and choose a working NVENC encoder first.", "請先偵測同揀一個可用 NVENC 編碼器。"));
            return;
        }
        int quality = (int)(double.IsNaN(NvencQualityBox.Value) ? 26 : NvencQualityBox.Value);
        var output = DeriveTagged($".{codec}", ".mp4");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.EncodeNvencAsync(MediaService.Input, output, codec, quality, ct));
    }

    private async void TargetSize_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        double target = double.IsNaN(TargetSizeBox.Value) ? 25 : TargetSizeBox.Value;
        int audio = (int)(double.IsNaN(TargetAudioBox.Value) ? 128 : TargetAudioBox.Value);
        var output = DeriveTagged($".{Math.Max(1, (int)Math.Round(target))}MiB", ".mp4");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.EncodeTargetSizeAsync(MediaService.Input, output, target, audio, ct));
    }

    private async void PickSubtitle_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(FileDialogs.BuildFilters(new[] { ".srt", ".ass" }),
            P("Choose an SRT or ASS subtitle", "揀 SRT 或 ASS 字幕"));
        if (path is null) return;
        _subtitlePath = path;
        SubtitlePathBox.Text = path;
    }

    private async void SubtitleRun_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        if (string.IsNullOrWhiteSpace(_subtitlePath) || !File.Exists(_subtitlePath))
        {
            ShowWorkflowResult(TweakResult.Fail("Choose an SRT or ASS subtitle first.", "請先揀 SRT 或 ASS 字幕。"));
            return;
        }
        var mode = SubtitleModeBox.SelectedIndex == 1 ? MediaSubtitleMode.SoftMux : MediaSubtitleMode.BurnIn;
        var output = DeriveTagged(mode == MediaSubtitleMode.BurnIn ? ".sub-burned" : ".sub-muxed", ".mp4");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.AddSubtitlesAsync(MediaService.Input, _subtitlePath, output, mode, ct));
    }

    private async void PickChapterFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose chapter output folder", "揀章節輸出資料夾"));
        if (folder is null) return;
        _chapterOutputFolder = folder;
        ChapterFolderBox.Text = folder;
    }

    private async void ReadChapters_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        await RunWorkflowAsync((Button)sender, async ct =>
        {
            var scan = await MediaWorkflowService.ReadChaptersAsync(MediaService.Input, ct);
            ChapterSummary.Text = scan.Chapters.Count == 0
                ? P("No chapters loaded.", "未有讀到章節。")
                : string.Join(Environment.NewLine, scan.Chapters.Select(ch =>
                    $"{ch.Index:00}  {TimeSpan.FromSeconds(ch.StartSeconds):hh\\:mm\\:ss}–{TimeSpan.FromSeconds(ch.EndSeconds):hh\\:mm\\:ss}  {ch.Title}"));
            return scan.Result;
        });
    }

    private async void SplitChapters_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        if (string.IsNullOrWhiteSpace(_chapterOutputFolder) || !Directory.Exists(_chapterOutputFolder))
        {
            ShowWorkflowResult(TweakResult.Fail("Choose a chapter output folder first.", "請先揀章節輸出資料夾。"));
            return;
        }
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.SplitChaptersAsync(MediaService.Input, _chapterOutputFolder, ct));
    }

    private async void PickPhotoInput_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose HEIC / HEIF / JPEG-XL source folder", "揀 HEIC／HEIF／JPEG-XL 來源資料夾"));
        if (folder is null) return;
        _photoInputFolder = folder;
        PhotoInputFolderBox.Text = folder;
    }

    private async void PickPhotoOutput_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose a separate photo output folder", "揀另一個相片輸出資料夾"));
        if (folder is null) return;
        _photoOutputFolder = folder;
        PhotoOutputFolderBox.Text = folder;
    }

    private async void ConvertPhotos_Click(object sender, RoutedEventArgs e)
    {
        var format = PhotoFormatBox.SelectedIndex == 1 ? MediaPhotoFormat.Png : MediaPhotoFormat.Jpeg;
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.ConvertPhotoBatchAsync(_photoInputFolder, _photoOutputFolder, format, ct));
    }

    private async void StripMetadata_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var output = DeriveTagged(".metadata-clean");
        await RunWorkflowAsync((Button)sender, ct => MediaWorkflowService.StripImageMetadataAsync(MediaService.Input, output, ct));
    }

    private async void PickInput_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(MediaExts);
        if (path is null) return;
        AppState.CurrentMediaInput = path;
        if (string.IsNullOrWhiteSpace(_chapterOutputFolder))
        {
            _chapterOutputFolder = Path.GetDirectoryName(path) ?? string.Empty;
            ChapterFolderBox.Text = _chapterOutputFolder;
        }
        RefreshSelection();
        await ShowProbe();
    }

    private async void PickOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("output", ".mp4", ".mp3", ".gif", ".wav", ".webm", ".mkv", ".png");
        if (path is null) return;
        AppState.CurrentMediaOutput = path;
        RefreshSelection();
    }

    private async Task ShowProbe()
    {
        if (!MediaService.HasInput) { InfoBorder.Visibility = Visibility.Collapsed; return; }
        InfoBorder.Visibility = Visibility.Visible;
        InfoText.Text = P("Reading media info…", "讀取媒體資訊緊…");
        try
        {
            var r = await MediaService.Info();
            var body = (r.Output ?? "").Trim();
            InfoText.Text = body.Length == 0 ? P("No info available.", "冇資訊。")
                : (body.Length > 1600 ? body[..1600] + " …" : body);
        }
        catch (Exception ex) { InfoText.Text = ex.Message; }
    }

    private string DeriveBeside(string suffixWithExt)
    {
        var input = MediaService.Input;
        var dir = Path.GetDirectoryName(input) ?? "";
        var name = Path.GetFileNameWithoutExtension(input);
        return Path.Combine(dir, name + suffixWithExt);
    }

    private string DeriveTagged(string tag, string? outputExtension = null)
    {
        var input = MediaService.Input;
        var directory = Path.GetDirectoryName(input) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(input);
        var extension = outputExtension ?? Path.GetExtension(input);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".mp4";
        return Path.Combine(directory, name + tag + extension);
    }

    private async void TrimCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var ext = Path.GetExtension(MediaService.Input);
        var outp = DeriveBeside($".trimmed{ext}");
        var args = $"-ss {Start()} -i {{in}} -t {Dur()} -c copy {{out}}";
        await RunAndShow((Button)sender, () => MediaService.RunWith(MediaService.Input, outp, args, useProbe: false));
    }

    private async void TrimEncode_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var outp = DeriveBeside(".trimmed.mp4");
        var args = $"-ss {Start()} -i {{in}} -t {Dur()} -c:v libx264 -c:a aac -movflags +faststart {{out}}";
        await RunAndShow((Button)sender, () => MediaService.RunWith(MediaService.Input, outp, args, useProbe: false));
    }

    private async void MakeGif_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        int fps = (int)(double.IsNaN(GifFps.Value) ? 12 : GifFps.Value);
        int w = (int)(double.IsNaN(GifWidth.Value) ? 480 : GifWidth.Value);
        var args = $"-i {{in}} -vf \"fps={fps},scale={w}:-1:flags=lanczos\" {{out}}";
        await RunAndShow((Button)sender, () => MediaService.Quick(".gif", args));
    }

    private async void GrabFrame_Click(object sender, RoutedEventArgs e)
    {
        if (!Guard()) return;
        var args = $"-ss {Start()} -i {{in}} -frames:v 1 {{out}}";
        await RunAndShow((Button)sender, () => MediaService.Quick(".frame.png", args));
    }

    private bool Guard()
    {
        if (MediaService.HasInput) return true;
        OutBorder.Visibility = Visibility.Visible;
        OutText.Text = P("Pick an input file first.", "請先揀輸入檔。");
        return false;
    }

    private string Start() => string.IsNullOrWhiteSpace(TrimStart.Text) ? "00:00:00" : TrimStart.Text.Trim();
    private string Dur() => string.IsNullOrWhiteSpace(TrimDuration.Text) ? "00:00:10" : TrimDuration.Text.Trim();

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= MediaOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }

        bool first = true;
        foreach (var op in shown)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: title + optional secondary title + description
        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        var title = new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        text.Children.Add(title);

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        }
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
        {
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });
        }

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(op, result);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + live value ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider
        {
            Minimum = op.Min,
            Maximum = op.Max,
            StepFrequency = op.Step > 0 ? op.Step : 1,
            Width = 180,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var unit = op.Unit is null ? "" : " " + op.Unit.Get(Loc.I.Language);
        var valueLabel = new TextBlock { MinWidth = 48, VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = TextAlignment.Right };

        bool suppress = true;
        try { slider.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { slider.Value = op.Min; }
        valueLabel.Text = ((int)slider.Value) + unit;
        suppress = false;

        slider.ValueChanged += (_, _) =>
        {
            valueLabel.Text = ((int)slider.Value) + unit;
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(slider.Value); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { slider.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { /* ignore */ }
                valueLabel.Text = ((int)slider.Value) + unit;
                suppress = false;
                ShowError(op, ex);
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueLabel);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step > 0 ? op.Step : 1,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 130,
        };
        bool suppress = true;
        try { box.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, _) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(box.Value)) return;
            try { op.SetNumber(box.Value); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { box.Value = op.GetNumber?.Invoke() ?? op.Min; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return box;
    }

    // ---------------- Info → TextBlock (+ refresh) ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Shared result / status area ----------------
    private void ShowResult(TweakDefinition op, TweakResult result)
    {
        ResultBar.Severity = result.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = result.Success ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = result.Message is null ? string.Empty : result.Message.Get(Loc.I.Language);
        ResultBar.IsOpen = true;

        // Mirror any raw output into the monospace pane (same behaviour as the quick actions).
        if (!string.IsNullOrWhiteSpace(result.Output))
        {
            OutBorder.Visibility = Visibility.Visible;
            var body = result.Output!;
            OutText.Text = body.Length > 4000 ? body[^4000..] : body;
        }
    }

    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成");
        ResultBar.Message = P(en, zh);
        ResultBar.IsOpen = true;
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message;
        ResultBar.IsOpen = true;
    }
}
