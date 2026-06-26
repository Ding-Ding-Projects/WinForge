using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內媒體下載器（包 yt-dlp）· In-app media downloader wrapping the yt-dlp CLI — paste URLs, list
/// formats, pick a quality preset or an explicit format, choose an output folder + template, download with
/// live progress (%/speed/ETA), audio-only, subtitles, embed thumbnail/metadata, playlists, sponsorblock,
/// cookies-from-browser, a download archive, plus self-update and clear-cache. No redirect. Bilingual.
/// </summary>
public sealed partial class YtDlpModule : Page
{
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _ui;
    private CancellationTokenSource? _cts;

    // preset format selectors keyed by combo index
    private static readonly (string En, string Zh, string Sel, bool Audio)[] Presets =
    {
        ("Best video + audio", "最佳影片＋音訊", "bv*+ba/b", false),
        ("1080p (or best ≤1080p)", "1080p（或 ≤1080p 最佳）", "bv*[height<=1080]+ba/b[height<=1080]", false),
        ("720p (or best ≤720p)", "720p（或 ≤720p 最佳）", "bv*[height<=720]+ba/b[height<=720]", false),
        ("480p (or best ≤480p)", "480p（或 ≤480p 最佳）", "bv*[height<=480]+ba/b[height<=480]", false),
        ("Audio only", "只要音訊", "", true),
        ("Custom (use box / list below)", "自訂（用下面欄位／清單）", null!, false),
    };

    public YtDlpModule()
    {
        InitializeComponent();
        _ui = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await CheckEngines(); };
        Unloaded += (_, _) => { try { _cts?.Cancel(); } catch { } YtDlpService.Cancel(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Media Downloader · 媒體下載器";
        HeaderBlurb.Text = P(
            "Download video and audio from 1000+ sites with yt-dlp. Paste one or more URLs (one per line), list the available formats, pick a quality, choose a folder, and download with live progress. Audio-only, subtitles, thumbnails, playlists and more are supported.",
            "用 yt-dlp 由 1000+ 個網站下載影片同音訊。貼一條或多條網址（每行一條），列出可用格式，揀畫質，揀資料夾，即時睇住進度下載。支援只要音訊、字幕、縮圖、播放清單等等。");

        UrlLabel.Text = P("Video URL(s) — one per line", "影片網址 — 每行一條");
        PasteBtn.Content = P("Paste", "貼上");
        ListFormatsBtn.Content = P("List formats", "列出格式");

        FormatsLabel.Text = P("Available formats (from yt-dlp -F)", "可用格式（yt-dlp -F）");
        FormatsHint.Text = P("Select a row to use that exact format id. Pairs like \"137+140\" merge video+audio automatically (needs ffmpeg).",
            "撳一行就用嗰個格式 id。例如「137+140」會自動合併影片＋音訊（需要 ffmpeg）。");

        OptionsLabel.Text = P("Quality & options", "畫質與選項");
        PresetCap.Text = P("Quality preset", "畫質預設");
        if (PresetBox.Items.Count == 0)
        {
            foreach (var pr in Presets) PresetBox.Items.Add(P(pr.En, pr.Zh));
            PresetBox.SelectedIndex = 0;
        }
        AudioFmtCap.Text = P("Audio format", "音訊格式");
        if (AudioFmtBox.Items.Count == 0)
        {
            foreach (var a in new[] { "mp3", "m4a", "opus", "flac", "wav", "best" }) AudioFmtBox.Items.Add(a);
            AudioFmtBox.SelectedIndex = 0;
        }
        SelectedFmtCap.Text = P("Format id / selector", "格式 id／選擇式");
        CookiesCap.Text = P("Cookies from browser", "瀏覽器 Cookie");
        if (CookiesBox.Items.Count == 0)
        {
            CookiesBox.Items.Add(P("None", "無"));
            foreach (var b in new[] { "chrome", "edge", "firefox", "brave", "opera", "vivaldi" }) CookiesBox.Items.Add(b);
            CookiesBox.SelectedIndex = 0;
        }

        SubsCheck.Content = P("Download subtitles", "下載字幕");
        SubLangsBox.Header = null;
        ThumbCheck.Content = P("Embed thumbnail", "嵌入縮圖");
        MetaCheck.Content = P("Embed metadata", "嵌入中繼資料");
        SponsorCheck.Content = P("Remove sponsor segments (SponsorBlock)", "移除贊助片段（SponsorBlock）");
        ArchiveCheck.Content = P("Use download archive (skip already-downloaded)", "用下載封存（跳過已下載）");
        PlaylistCap.Text = P("Playlist items", "播放清單項目");

        OutputLabel.Text = P("Output", "輸出");
        FolderCap.Text = P("Folder", "資料夾");
        FolderBtn.Content = P("Choose…", "選擇…");
        TemplateCap.Text = P("Template", "範本");
        if (string.IsNullOrEmpty(FolderBox.Text))
            FolderBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);

        DownloadBtn.Content = P("Download", "下載");
        CancelBtn.Content = P("Cancel", "取消");
        UpdateBtn.Content = P("Update yt-dlp", "更新 yt-dlp");
        ClearCacheBtn.Content = P("Clear cache", "清快取");

        LogLabel.Text = P("Log", "記錄");
        LogClearBtn.Content = P("Clear", "清除");

        UpdatePresetState();
    }

    // ───────────────────────── engine checks ─────────────────────────

    private async Task CheckEngines()
    {
        bool ok = await YtDlpService.IsAvailable();
        EngineBar.IsOpen = !ok;
        if (!ok)
        {
            EngineBar.Title = P("yt-dlp not found", "搵唔到 yt-dlp");
            EngineBar.Message = P("Click to install it automatically (yt-dlp.yt-dlp via winget) — no restart needed.",
                "撳一下自動安裝（用 winget 裝 yt-dlp.yt-dlp）— 唔使重啟。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                YtDlpService.WingetId, "Install yt-dlp", "安裝 yt-dlp",
                CheckEngines, YtDlpService.Rescan);
        }
        else EngineBar.ActionButton = null;

        bool hasFfmpeg = YtDlpService.HasFfmpeg;
        FfmpegBar.IsOpen = !hasFfmpeg;
        if (!hasFfmpeg)
        {
            FfmpegBar.Title = P("ffmpeg not found", "搵唔到 ffmpeg");
            FfmpegBar.Message = P("ffmpeg is needed to merge separate video+audio streams and to convert audio. Click to install it (Gyan.FFmpeg via winget).",
                "合併分開嘅影片＋音訊串流同轉檔音訊都需要 ffmpeg。撳一下自動安裝（用 winget 裝 Gyan.FFmpeg）。");
            FfmpegBar.ActionButton = EngineBars.AutoInstallButton(
                YtDlpService.FfmpegWingetId, "Install ffmpeg", "安裝 ffmpeg",
                CheckEngines, MediaService.Rescan);
        }
        else FfmpegBar.ActionButton = null;

        SyncRunning();
    }

    // ───────────────────────── URL / formats ─────────────────────────

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view.Contains(StandardDataFormats.Text))
            {
                _ = PasteAsync(view);
            }
        }
        catch { }
    }

    private async Task PasteAsync(DataPackageView view)
    {
        try
        {
            var text = await view.GetTextAsync();
            if (string.IsNullOrWhiteSpace(text)) return;
            var existing = (UrlBox.Text ?? "").TrimEnd();
            UrlBox.Text = existing.Length == 0 ? text.Trim() : existing + "\n" + text.Trim();
        }
        catch { }
    }

    private string FirstUrl()
        => (UrlBox.Text ?? "").Replace("\r", "").Split('\n')
            .Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0) ?? "";

    private async void ListFormats_Click(object sender, RoutedEventArgs e)
    {
        var url = FirstUrl();
        if (url.Length == 0) { Notify(InfoBarSeverity.Warning, P("No URL", "未輸入網址"), P("Paste a video URL first.", "請先貼一條影片網址。")); return; }
        if (!await EnsureEngine()) return;

        ProbeBusy.IsActive = true;
        ListFormatsBtn.IsEnabled = false;
        FormatList.ItemsSource = null;
        AppendLog($"$ yt-dlp -F {url}");
        try
        {
            var (formats, raw) = await YtDlpService.ListFormats(url);
            FormatList.ItemsSource = formats;
            AppendLog(raw);
            if (formats.Count == 0)
                Notify(InfoBarSeverity.Warning, P("No formats", "冇格式"),
                    P("yt-dlp returned no formats — see the log. The site may need cookies/auth or be unsupported.",
                      "yt-dlp 冇傳回任何格式 — 睇記錄。該網站可能需要 cookie／登入，或者唔支援。"));
            else
                Notify(InfoBarSeverity.Success, P("Formats listed", "已列出格式"),
                    P($"{formats.Count} formats found.", $"搵到 {formats.Count} 個格式。"));
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Error", "出錯"), ex.Message); }
        finally { ProbeBusy.IsActive = false; ListFormatsBtn.IsEnabled = true; }
    }

    private void FormatList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FormatList.SelectedItem is YtDlpFormat f)
        {
            SelectedFmtBox.Text = f.Id;
            // switch preset to Custom so the explicit id is honoured
            PresetBox.SelectedIndex = Presets.Length - 1;
        }
    }

    // ───────────────────────── presets ─────────────────────────

    private void PresetBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePresetState();

    private bool IsCustomPreset => PresetBox.SelectedIndex == Presets.Length - 1;
    private bool IsAudioPreset => PresetBox.SelectedIndex >= 0 && Presets[PresetBox.SelectedIndex].Audio;

    private void UpdatePresetState()
    {
        bool custom = IsCustomPreset;
        SelectedFmtBox.IsEnabled = custom;
        AudioFmtBox.IsEnabled = IsAudioPreset;
        if (!custom && PresetBox.SelectedIndex >= 0 && !IsAudioPreset)
            SelectedFmtBox.Text = Presets[PresetBox.SelectedIndex].Sel ?? "";
        else if (IsAudioPreset)
            SelectedFmtBox.Text = "";
    }

    // ───────────────────────── output folder ─────────────────────────

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = await FileDialogs.OpenFolderAsync(P("Choose download folder", "揀下載資料夾"));
        if (dir is not null) FolderBox.Text = dir;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = (FolderBox.Text ?? "").Trim();
        if (dir.Length == 0 || !Directory.Exists(dir)) return;
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(dir);
            Clipboard.SetContent(dp);
            Clipboard.Flush();
            Notify(InfoBarSeverity.Success, P("Folder path copied", "已複製資料夾路徑"), dir);
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Copy failed", "複製失敗"), ex.Message); }
    }

    // ───────────────────────── download ─────────────────────────

    private async Task<bool> EnsureEngine()
    {
        if (await YtDlpService.IsAvailable()) return true;
        Notify(InfoBarSeverity.Warning, P("yt-dlp not installed", "未安裝 yt-dlp"),
            P("Install yt-dlp from the bar above first.", "請先用上面嘅列安裝 yt-dlp。"));
        await CheckEngines();
        return false;
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        var urls = (UrlBox.Text ?? "").Replace("\r", "").Split('\n')
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        if (urls.Count == 0) { Notify(InfoBarSeverity.Warning, P("No URL", "未輸入網址"), P("Paste at least one URL.", "請至少貼一條網址。")); return; }

        var folder = (FolderBox.Text ?? "").Trim();
        if (folder.Length == 0) { Notify(InfoBarSeverity.Warning, P("No folder", "未揀資料夾"), P("Choose an output folder.", "請揀一個輸出資料夾。")); return; }
        try { Directory.CreateDirectory(folder); } catch { }

        if (!await EnsureEngine()) return;

        _cts = new CancellationTokenSource();
        SetBusy(true);
        Progress.Value = 0;

        int idx = PresetBox.SelectedIndex;
        bool audioOnly = IsAudioPreset;
        string selector = audioOnly ? "" : (SelectedFmtBox.Text ?? "").Trim();
        string cookies = CookiesBox.SelectedIndex > 0 ? (CookiesBox.SelectedItem as string ?? "") : "";

        int done = 0, total = urls.Count;
        bool anyFail = false;
        foreach (var url in urls)
        {
            if (_cts.IsCancellationRequested) break;
            done++;
            StatusText.Text = total > 1 ? P($"Item {done}/{total}…", $"第 {done}/{total} 項…") : P("Starting…", "開始…");
            AppendLog($"\n$ yt-dlp … {url}");

            var opt = new YtDlpService.DownloadOptions
            {
                Url = url,
                FormatSelector = selector,
                OutputDir = folder,
                Template = (TemplateBox.Text ?? "").Trim(),
                AudioOnly = audioOnly,
                AudioFormat = AudioFmtBox.SelectedItem as string ?? "mp3",
                Subtitles = SubsCheck.IsChecked == true,
                SubLangs = (SubLangsBox.Text ?? "").Trim(),
                EmbedThumbnail = ThumbCheck.IsChecked == true,
                EmbedMetadata = MetaCheck.IsChecked == true,
                PlaylistItems = (PlaylistBox.Text ?? "").Trim(),
                UseArchive = ArchiveCheck.IsChecked == true,
                ArchivePath = Path.Combine(folder, "yt-dlp-archive.txt"),
                SponsorBlock = SponsorCheck.IsChecked == true,
                CookiesBrowser = cookies,
            };

            var r = await YtDlpService.Download(opt, OnProgress, OnLine, _cts.Token);
            if (!r.Success) anyFail = true;
            if (_cts.IsCancellationRequested) break;
        }

        SetBusy(false);
        bool cancelled = _cts.IsCancellationRequested;
        Progress.Value = cancelled ? Progress.Value : 100;
        StatusText.Text = cancelled ? P("Cancelled.", "已取消。")
            : anyFail ? P("Finished with errors — see log.", "完成但有錯誤 — 睇記錄。")
            : P("All downloads complete.", "全部下載完成。");
        Notify(cancelled ? InfoBarSeverity.Informational : anyFail ? InfoBarSeverity.Warning : InfoBarSeverity.Success,
            P("Media Downloader", "媒體下載器"), StatusText.Text);

        _cts.Dispose();
        _cts = null;
    }

    private void OnProgress(YtDlpProgress p)
    {
        _ui.TryEnqueue(() =>
        {
            if (p.Stage == "download")
            {
                Progress.IsIndeterminate = false;
                Progress.Value = Math.Clamp(p.Percent, 0, 100);
                var bits = new System.Collections.Generic.List<string> { $"{p.Percent:0.0}%" };
                if (!string.IsNullOrEmpty(p.TotalSize)) bits.Add(P("of", "／") + " " + p.TotalSize);
                if (!string.IsNullOrEmpty(p.Speed)) bits.Add(p.Speed);
                if (!string.IsNullOrEmpty(p.Eta)) bits.Add("ETA " + p.Eta);
                StatusText.Text = string.Join("  ·  ", bits);
            }
            else if (p.Stage == "merging")
            {
                Progress.IsIndeterminate = true;
                StatusText.Text = P("Merging video + audio…", "合併影片＋音訊…");
            }
            else
            {
                Progress.IsIndeterminate = true;
                StatusText.Text = P("Post-processing…", "後製處理…");
            }
        });
    }

    private void OnLine(string line) => _ui.TryEnqueue(() => AppendLog(line));

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        YtDlpService.Cancel();
        StatusText.Text = P("Cancelling…", "取消中…");
    }

    private void SetBusy(bool busy)
    {
        DownloadBtn.IsEnabled = !busy;
        CancelBtn.IsEnabled = busy;
        ListFormatsBtn.IsEnabled = !busy;
        UpdateBtn.IsEnabled = !busy;
        ClearCacheBtn.IsEnabled = !busy;
        Progress.IsIndeterminate = false;
    }

    private void SyncRunning() => SetBusy(YtDlpService.IsRunning);

    // ───────────────────────── maintenance ─────────────────────────

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureEngine()) return;
        UpdateBtn.IsEnabled = false;
        AppendLog("$ yt-dlp -U");
        var r = await YtDlpService.Update();
        if (!string.IsNullOrEmpty(r.Output)) AppendLog(r.Output);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, P("Update yt-dlp", "更新 yt-dlp"), Msg(r));
        UpdateBtn.IsEnabled = true;
    }

    private async void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        if (!await EnsureEngine()) return;
        ClearCacheBtn.IsEnabled = false;
        AppendLog("$ yt-dlp --rm-cache-dir");
        var r = await YtDlpService.ClearCache();
        if (!string.IsNullOrEmpty(r.Output)) AppendLog(r.Output);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, P("Clear cache", "清快取"), Msg(r));
        ClearCacheBtn.IsEnabled = true;
    }

    // ───────────────────────── log helpers ─────────────────────────

    private void AppendLog(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        if (LogBox.Text.Length > 400_000)
            LogBox.Text = LogBox.Text.Substring(LogBox.Text.Length - 200_000);
        LogBox.Text += text + "\n";
        LogBox.Select(LogBox.Text.Length, 0);
    }

    private void LogClear_Click(object sender, RoutedEventArgs e) => LogBox.Text = "";

    private static string Msg(TweakResult r)
        => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev; ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
