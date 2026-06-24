using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 媒體模組 · In-app Media module: wraps ffmpeg/ffprobe — convert, trim, GIF, grab frames, inspect, plus ~60 ops.
/// Browse uses the Win32 file dialogs so it works whether or not WinForge runs elevated.
/// </summary>
public sealed partial class MediaModule : Page
{
    private List<TweakDefinition>? _ops;
    private bool _busy;

    private static readonly string[] MediaExts =
        { ".mp4", ".mkv", ".mov", ".avi", ".webm", ".m4v", ".wmv", ".flv", ".mp3", ".wav", ".flac", ".aac", ".m4a", ".ogg", ".opus" };

    public MediaModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += (_, _) => { Render(); BuildQuickOps(); PopulateOps(string.Empty); RefreshSelection(); };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); BuildQuickOps(); PopulateOps(OpsFilter.Text ?? string.Empty); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Media · 媒體";
        HeaderBlurb.Text = P("Convert, trim, make GIFs, grab frames and inspect video/audio with ffmpeg — all in-app.",
            "用 ffmpeg 轉檔、剪裁、整 GIF、擷取畫格、檢視影片／音訊 — 全部喺 app 內。");
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
            EngineBar.Message = P("Click to install ffmpeg automatically (winget) — no restart needed.",
                "撳一下自動安裝 ffmpeg（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "Gyan.FFmpeg", "Install ffmpeg automatically", "自動安裝 ffmpeg",
                () => { Render(); return Task.CompletedTask; }, MediaService.Rescan);
        }
        else { EngineBar.IsOpen = false; EngineBar.ActionButton = null; }
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
        if (_busy) return;
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

    private async void PickInput_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(MediaExts);
        if (path is null) return;
        AppState.CurrentMediaInput = path;
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
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }
}
