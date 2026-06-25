using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Services;
using static WinForge.Services.AudioTagService;

namespace WinForge.Pages;

/// <summary>
/// 音訊標籤編輯器（Mp3tag 風格）· Native, Mp3tag-style audio tag editor.
/// 揀資料夾／檔案 → 喺表格睇現有標籤（標題、演出者、專輯、專輯演出者、曲目、年份、類型、時長、位元率）→
/// 編輯單一或批次標籤（含封面圖：載入／顯示／移除），用 TagLib# 寫返落檔。仲有兩個工具：
/// 「由檔名取標籤」（樣式如 %artist% - %title%）同「由標籤改檔名」（先預覽再套用）。
/// Pure-managed via TagLib# — no external tool (Mp3tag, etc.) is ever launched or bundled.
/// </summary>
public sealed partial class AudioTaggerModule : Page
{
    private readonly ObservableCollection<TrackTags> _rows = new();
    private string? _folder;
    private bool _recursive;
    private bool _loading;

    // Pending cover edit for the current selection. CoverAction: 0 leave / 1 set / 2 remove.
    private int _coverAction;
    private byte[]? _pendingCover;
    private string? _pendingCoverMime;

    public AudioTaggerModule()
    {
        InitializeComponent();
        FileList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object? s, RoutedEventArgs e) { Render(); UpdateEditor(); UpdateListVisibility(); }
    private void OnUnloaded(object? s, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? s, EventArgs e) { Render(); UpdateEditor(); }

    private List<TrackTags> Selected => FileList.SelectedItems.OfType<TrackTags>().ToList();

    // ───────────────────────── render (bilingual labels) ─────────────────────────

    private void Render()
    {
        HeaderTitle.Text = "Audio Tagger · 音訊標籤編輯器";
        HeaderBlurb.Text = P("A native, Mp3tag-style tag editor. Pick a folder or files, see current tags in the grid, edit one or many at once (including cover art), and save back with TagLib# — fully in-app.",
            "原生、Mp3tag 風格嘅標籤編輯器。揀資料夾或檔案、喺表格睇現有標籤、單一或批次編輯（包括封面圖），再用 TagLib# 寫返落檔 — 全部喺 app 內。");

        OpenFolderLbl.Text = P("Open folder…", "開資料夾…");
        OpenFilesLbl.Text = P("Add files…", "加檔案…");
        RecurseChk.Content = P("Include subfolders · 含子資料夾", "含子資料夾");
        ColTitle.Text = P("Title · 標題", "標題");
        ColArtist.Text = P("Artist · 演出者", "演出者");
        ColAlbum.Text = P("Album · 專輯", "專輯");
        ColTrack.Text = P("Track · 曲目", "曲目");
        ColYear.Text = P("Year · 年份", "年份");
        ColDur.Text = P("Time · 時長", "時長");
        EmptyText.Text = P("Open a folder or add files to list audio (mp3, flac, m4a, ogg, wav…) and edit their tags.",
            "開資料夾或加檔案，就會列出音訊（mp3、flac、m4a、ogg、wav…）等你改標籤。");

        EditorHeader.Text = P("Tag editor · 標籤編輯器", "標籤編輯器");
        CoverLbl.Text = P("Cover art · 封面圖", "封面圖");
        LoadCoverBtn.Content = P("Load image…", "載入圖片…");
        RemoveCoverBtn.Content = P("Remove", "移除");

        TitleBox.Header = P("Title · 標題", "標題");
        ArtistBox.Header = P("Artist · 演出者 (use ; for multiple)", "演出者（多個用 ; 分隔）");
        AlbumBox.Header = P("Album · 專輯", "專輯");
        AlbumArtistBox.Header = P("Album artist · 專輯演出者", "專輯演出者");
        TrackBox.Header = P("Track · 曲目", "曲目");
        DiscBox.Header = P("Disc · 碟", "碟");
        YearBox.Header = P("Year · 年份", "年份");
        GenreBox.Header = P("Genre · 類型", "類型");
        ComposerBox.Header = P("Composer · 作曲", "作曲");
        CommentBox.Header = P("Comment · 備註", "備註");

        SaveLbl.Text = P("Save tags", "儲存標籤");
        RevertBtn.Content = P("Revert · 還原", "還原");

        ToolsHeader.Text = P("Tools — filename ⇄ tags", "工具 — 檔名 ⇄ 標籤");
        FromNameLbl.Text = P("Tag from filename", "由檔名取標籤");
        FromNameHint.Text = P("Tokens: %artist% %title% %album% %albumartist% %track% %year% %genre% %disc% %composer%. Parses each selected file's name and fills matching tags.",
            "符記：%artist% %title% %album% %albumartist% %track% %year% %genre% %disc% %composer%。會解析每個選定檔嘅檔名並填入對應標籤。");
        FromNamePreviewBtn.Content = P("Preview · 預覽", "預覽");
        FromNameApplyBtn.Content = P("Apply & save · 套用並儲存", "套用並儲存");

        ToNameLbl.Text = P("Rename from tags", "由標籤改檔名");
        ToNameHint.Text = P("Same tokens. Builds a new filename from each selected file's tags (extension kept).",
            "同樣符記。會由每個選定檔嘅標籤砌新檔名（保留副檔名）。");
        ToNamePreviewBtn.Content = P("Preview · 預覽", "預覽");
        ToNameApplyBtn.Content = P("Rename · 改名", "改名");
    }

    // ───────────────────────── open / load ─────────────────────────

    private async void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Pick a folder of audio files", "揀一個音訊檔資料夾"));
        if (folder is null) return;
        _folder = folder;
        await LoadFolder();
    }

    private async void OpenFiles_Click(object sender, RoutedEventArgs e)
    {
        var files = await FileDialogs.OpenFilesAsync(Extensions);
        if (files.Count == 0) return;
        SetLoading(true);
        var added = await ReadFilesAsync(files);
        SetLoading(false);
        _folder = null;
        FolderPathText.Text = P($"{added.Count} file(s) added", $"已加 {added.Count} 個檔案");
        MergeRows(added, replace: false);
        Notify(InfoBarSeverity.Success, P($"Added {added.Count} file(s).", $"已加入 {added.Count} 個檔案。"));
    }

    private async void Reload_Click(object sender, RoutedEventArgs e)
    {
        if (_folder is not null) await LoadFolder();
        else if (_rows.Count > 0)
        {
            var paths = _rows.Select(r => r.Path).ToList();
            SetLoading(true);
            var reread = await ReadFilesAsync(paths);
            SetLoading(false);
            MergeRows(reread, replace: true);
        }
    }

    private async void Recurse_Click(object sender, RoutedEventArgs e)
    {
        _recursive = RecurseChk.IsChecked == true;
        if (_folder is not null) await LoadFolder();
    }

    private async Task LoadFolder()
    {
        if (_folder is null) return;
        SetLoading(true);
        FolderPathText.Text = _folder;
        var list = await ReadFolderAsync(_folder, _recursive);
        SetLoading(false);
        MergeRows(list, replace: true);
        Notify(InfoBarSeverity.Success, P($"Loaded {list.Count} audio file(s).", $"已載入 {list.Count} 個音訊檔。"));
    }

    private void MergeRows(List<TrackTags> items, bool replace)
    {
        if (replace) _rows.Clear();
        foreach (var t in items)
        {
            if (!replace && _rows.Any(r => string.Equals(r.Path, t.Path, StringComparison.OrdinalIgnoreCase)))
                continue;
            _rows.Add(t);
        }
        UpdateListVisibility();
        UpdateEditor();
    }

    private void SetLoading(bool on)
    {
        _loading = on;
        OpenFolderBtn.IsEnabled = OpenFilesBtn.IsEnabled = ReloadBtn.IsEnabled = !on;
    }

    private void UpdateListVisibility()
    {
        bool empty = _rows.Count == 0;
        EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        FileList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    // ───────────────────────── selection / editor binding ─────────────────────────

    private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateEditor();

    private void UpdateEditor()
    {
        var sel = Selected;
        bool any = sel.Count > 0;
        foreach (var c in new Control[] { TitleBox, ArtistBox, AlbumBox, AlbumArtistBox, TrackBox, DiscBox,
            YearBox, GenreBox, ComposerBox, CommentBox, SaveBtn, RevertBtn, LoadCoverBtn, RemoveCoverBtn })
            c.IsEnabled = any;

        _coverAction = 0; _pendingCover = null; _pendingCoverMime = null;

        if (!any)
        {
            SelectionInfo.Text = P("Select one or more files on the left to edit. With multiple selected, any field you change applies to all of them.",
                "喺左邊揀一個或多個檔案編輯。揀多個時，你改嘅任何欄位都會套用到全部。");
            ClearFields();
            UpdateCoverPreview(null, null);
            TechInfo.Text = "";
            return;
        }

        if (sel.Count == 1)
        {
            var t = sel[0];
            SelectionInfo.Text = t.FileName;
            TitleBox.Text = t.Title ?? "";
            ArtistBox.Text = t.Artist ?? "";
            AlbumBox.Text = t.Album ?? "";
            AlbumArtistBox.Text = t.AlbumArtist ?? "";
            TrackBox.Text = t.Track > 0 ? t.Track.ToString() : "";
            DiscBox.Text = t.Disc > 0 ? t.Disc.ToString() : "";
            YearBox.Text = t.YearText;
            GenreBox.Text = t.Genre ?? "";
            ComposerBox.Text = t.Composer ?? "";
            CommentBox.Text = t.Comment ?? "";
            UpdateCoverPreview(t.CoverData, t.CoverMime);
            TechInfo.Text = P(
                $"{t.DurationText} · {t.BitrateText} · {t.SampleRate} Hz · {Channels(t.Channels)} · {System.IO.Path.GetExtension(t.Path).TrimStart('.').ToUpperInvariant()}",
                $"{t.DurationText} · {t.BitrateText} · {t.SampleRate} Hz · {Channels(t.Channels)} · {System.IO.Path.GetExtension(t.Path).TrimStart('.').ToUpperInvariant()}");
        }
        else
        {
            SelectionInfo.Text = P($"{sel.Count} files selected — fields shown are common values; edits apply to all.",
                $"已選 {sel.Count} 個檔案 — 顯示嘅係共同值；編輯會套用到全部。");
            TitleBox.Text = Common(sel, t => t.Title);
            ArtistBox.Text = Common(sel, t => t.Artist);
            AlbumBox.Text = Common(sel, t => t.Album);
            AlbumArtistBox.Text = Common(sel, t => t.AlbumArtist);
            TrackBox.Text = "";
            DiscBox.Text = Common(sel, t => t.Disc > 0 ? t.Disc.ToString() : null);
            YearBox.Text = Common(sel, t => t.YearText.Length > 0 ? t.YearText : null);
            GenreBox.Text = Common(sel, t => t.Genre);
            ComposerBox.Text = Common(sel, t => t.Composer);
            CommentBox.Text = Common(sel, t => t.Comment);
            UpdateCoverPreview(null, null);
            CoverInfo.Text = P("Load a new image to set it on all selected files.", "載入新圖片可套用到所有選定檔案。");
            TechInfo.Text = "";
        }
    }

    private string Channels(int c) => c switch { 1 => P("mono", "單聲道"), 2 => P("stereo", "立體聲"), _ => $"{c}ch" };

    private static string Common(List<TrackTags> sel, Func<TrackTags, string?> get)
    {
        var first = get(sel[0]);
        return sel.All(t => string.Equals(get(t), first, StringComparison.Ordinal)) ? (first ?? "") : "";
    }

    private void ClearFields()
    {
        foreach (var tb in new[] { TitleBox, ArtistBox, AlbumBox, AlbumArtistBox, TrackBox, DiscBox, YearBox, GenreBox, ComposerBox, CommentBox })
            tb.Text = "";
        FromNamePreview.Text = ""; ToNamePreview.Text = "";
    }

    // ───────────────────────── cover art ─────────────────────────

    private async void UpdateCoverPreview(byte[]? data, string? mime)
    {
        if (data is { Length: > 0 })
        {
            try
            {
                var bmp = new BitmapImage();
                using (var ms = new MemoryStream(data))
                using (var ras = ms.AsRandomAccessStream())
                    await bmp.SetSourceAsync(ras);
                CoverImage.Source = bmp;
                CoverImage.Visibility = Visibility.Visible;
                CoverPlaceholder.Visibility = Visibility.Collapsed;
                CoverInfo.Text = P($"{data.Length / 1024} KB · {mime ?? "image"}", $"{data.Length / 1024} KB · {mime ?? "image"}");
                return;
            }
            catch { }
        }
        CoverImage.Source = null;
        CoverImage.Visibility = Visibility.Collapsed;
        CoverPlaceholder.Visibility = Visibility.Visible;
        CoverInfo.Text = P("No cover.", "冇封面。");
    }

    private async void LoadCover_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp");
        if (path is null) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            _pendingCover = bytes;
            _pendingCoverMime = MimeFor(path);
            _coverAction = 1;
            UpdateCoverPreview(bytes, _pendingCoverMime);
            Notify(InfoBarSeverity.Informational, P("Cover staged — click Save to write it.", "封面已準備 — 撳「儲存」寫入。"));
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
    }

    private void RemoveCover_Click(object sender, RoutedEventArgs e)
    {
        _coverAction = 2; _pendingCover = null; _pendingCoverMime = null;
        UpdateCoverPreview(null, null);
        Notify(InfoBarSeverity.Informational, P("Cover will be removed on Save.", "儲存時會移除封面。"));
    }

    private static string MimeFor(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

    // ───────────────────────── save / revert ─────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel.Count == 0) return;

        var edit = BuildEditFromForm(sel.Count > 1);
        SaveBusy.IsActive = true;
        SaveBtn.IsEnabled = false;
        try
        {
            var (ok, fail, errors) = await WriteManyAsync(sel.Select(t => t.Path), edit);
            // Re-read the affected files so the grid + editor reflect what's on disk.
            await RereadAndRefresh(sel.Select(t => t.Path).ToList());
            if (fail == 0)
                Notify(InfoBarSeverity.Success, P($"Saved tags to {ok} file(s).", $"已儲存標籤到 {ok} 個檔案。"));
            else
                Notify(InfoBarSeverity.Warning, P($"Saved {ok}, failed {fail}. {string.Join(" | ", errors.Take(3))}",
                    $"成功 {ok}，失敗 {fail}。{string.Join(" | ", errors.Take(3))}"));
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, ex.Message); }
        finally { SaveBusy.IsActive = false; SaveBtn.IsEnabled = true; }
    }

    /// <summary>
    /// Build the edit from the form. In single-file mode every field is written (empty clears it).
    /// In multi-file (batch) mode, a blank text field is treated as "leave untouched" so you can
    /// set just one field across many files. The cover staging applies in both modes.
    /// </summary>
    private TagEdit BuildEditFromForm(bool batch)
    {
        string? Field(TextBox tb) => batch && string.IsNullOrEmpty(tb.Text) ? null : tb.Text;
        uint? Num(TextBox tb)
        {
            if (batch && string.IsNullOrEmpty(tb.Text)) return null;
            return uint.TryParse(tb.Text, out var v) ? v : 0u;
        }

        return new TagEdit
        {
            Title = Field(TitleBox),
            Artist = Field(ArtistBox),
            Album = Field(AlbumBox),
            AlbumArtist = Field(AlbumArtistBox),
            Track = Num(TrackBox),
            Disc = Num(DiscBox),
            Year = Num(YearBox),
            Genre = Field(GenreBox),
            Composer = Field(ComposerBox),
            Comment = Field(CommentBox),
            CoverAction = _coverAction,
            CoverData = _pendingCover,
            CoverMime = _pendingCoverMime,
        };
    }

    private void Revert_Click(object sender, RoutedEventArgs e) => UpdateEditor();

    private async Task RereadAndRefresh(List<string> paths)
    {
        foreach (var p in paths)
        {
            var fresh = Read(p);
            if (fresh is null) continue;
            for (int i = 0; i < _rows.Count; i++)
                if (string.Equals(_rows[i].Path, p, StringComparison.OrdinalIgnoreCase))
                {
                    _rows[i] = fresh;
                    break;
                }
        }
        await Task.CompletedTask;
        UpdateEditor();
    }

    // ───────────────────────── tools: filename ⇄ tags ─────────────────────────

    private void FromNamePreview_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel.Count == 0) { FromNamePreview.Text = P("Select files first.", "請先揀檔案。"); return; }
        var lines = new List<string>();
        int matched = 0;
        foreach (var t in sel.Take(8))
        {
            var edit = ParseFromFileName(t.Path, FromNamePattern.Text);
            if (edit is null) { lines.Add($"✗ {t.FileName} — " + P("no match", "唔符合")); continue; }
            matched++;
            lines.Add($"✓ {t.FileName} → {DescribeEdit(edit)}");
        }
        if (sel.Count > 8) lines.Add(P($"… and {sel.Count - 8} more", $"… 仲有 {sel.Count - 8} 個"));
        FromNamePreview.Text = P($"{matched}/{sel.Count} match:\n", $"{matched}/{sel.Count} 符合：\n") + string.Join("\n", lines);
    }

    private async void FromNameApply_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel.Count == 0) return;
        SaveBusy.IsActive = true;
        int ok = 0, skip = 0, fail = 0;
        var errors = new List<string>();
        foreach (var t in sel)
        {
            var edit = ParseFromFileName(t.Path, FromNamePattern.Text);
            if (edit is null) { skip++; continue; }
            try { Write(t.Path, edit); ok++; }
            catch (Exception ex) { fail++; errors.Add($"{t.FileName}: {ex.Message}"); }
        }
        await RereadAndRefresh(sel.Select(t => t.Path).ToList());
        SaveBusy.IsActive = false;
        Notify(fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            P($"Tagged {ok}, skipped {skip} (no match), failed {fail}.", $"已標記 {ok}，跳過 {skip}（唔符合），失敗 {fail}。"));
    }

    private void ToNamePreview_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel.Count == 0) { ToNamePreview.Text = P("Select files first.", "請先揀檔案。"); return; }
        var lines = new List<string>();
        foreach (var t in sel.Take(8))
        {
            var name = BuildFileName(t, ToNamePattern.Text) + System.IO.Path.GetExtension(t.Path);
            lines.Add($"{t.FileName} → {name}");
        }
        if (sel.Count > 8) lines.Add(P($"… and {sel.Count - 8} more", $"… 仲有 {sel.Count - 8} 個"));
        ToNamePreview.Text = string.Join("\n", lines);
    }

    private async void ToNameApply_Click(object sender, RoutedEventArgs e)
    {
        var sel = Selected;
        if (sel.Count == 0) return;
        SaveBusy.IsActive = true;
        int ok = 0, fail = 0;
        var errors = new List<string>();
        var changed = new List<(string oldPath, string newPath)>();
        foreach (var t in sel)
        {
            try
            {
                var baseName = BuildFileName(t, ToNamePattern.Text);
                var np = Rename(t.Path, baseName);
                if (!string.Equals(np, t.Path, StringComparison.OrdinalIgnoreCase)) changed.Add((t.Path, np));
                ok++;
            }
            catch (Exception ex) { fail++; errors.Add($"{t.FileName}: {ex.Message}"); }
        }
        // Update rows in place with the new paths (re-read for fresh FileName).
        foreach (var (oldPath, newPath) in changed)
        {
            var fresh = Read(newPath);
            if (fresh is null) continue;
            for (int i = 0; i < _rows.Count; i++)
                if (string.Equals(_rows[i].Path, oldPath, StringComparison.OrdinalIgnoreCase)) { _rows[i] = fresh; break; }
        }
        SaveBusy.IsActive = false;
        UpdateEditor();
        Notify(fail == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            P($"Renamed {changed.Count} file(s) ({ok} processed, {fail} failed). {string.Join(" | ", errors.Take(2))}",
              $"已改名 {changed.Count} 個（處理 {ok}，失敗 {fail}）。{string.Join(" | ", errors.Take(2))}"));
    }

    private string DescribeEdit(TagEdit e)
    {
        var bits = new List<string>();
        if (e.Title is not null) bits.Add($"title={e.Title}");
        if (e.Artist is not null) bits.Add($"artist={e.Artist}");
        if (e.Album is not null) bits.Add($"album={e.Album}");
        if (e.AlbumArtist is not null) bits.Add($"albumArtist={e.AlbumArtist}");
        if (e.Track is not null) bits.Add($"track={e.Track}");
        if (e.Year is not null) bits.Add($"year={e.Year}");
        if (e.Genre is not null) bits.Add($"genre={e.Genre}");
        if (e.Disc is not null) bits.Add($"disc={e.Disc}");
        if (e.Composer is not null) bits.Add($"composer={e.Composer}");
        return bits.Count == 0 ? P("(nothing)", "（無）") : string.Join(", ", bits);
    }

    // ───────────────────────── result bar ─────────────────────────

    private void Notify(InfoBarSeverity sev, string message)
    {
        ResultBar.Severity = sev;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
