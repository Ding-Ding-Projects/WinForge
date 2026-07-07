using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 檔案切割／合併 · File split &amp; join — stream any file into sequential numbered parts
/// (<c>&lt;name&gt;.001, .002, …</c>) and losslessly rejoin them back into one file, with progress
/// and an optional SHA256 of the result. All I/O runs off the UI thread and is guarded — the UI never
/// blocks and never sees an unhandled throw. Bilingual (English + 粵語).
/// </summary>
public sealed partial class FileSplitModule : Page
{
    private string? _source;
    private string? _outFolder;
    private string? _firstPart;
    private bool _busy;

    public FileSplitModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "File Split & Join · 檔案切割／合併";
        HeaderBlurb.Text = P(
            "Break a large file into sequential numbered parts (.001, .002, …), then rejoin them anywhere back into the exact original — no compression, pure copy.",
            "將大檔案切成一份份順序編號嘅部件（.001、.002…），之後喺任何地方都可以原封不動合返轉頭 — 唔壓縮，淨係逐個位元組複製。");

        SplitTitle.Text = P("Split a file", "切割檔案");
        PickSourceBtn.Content = P("Pick file…", "揀檔案…");
        PartSizeLabel.Text = P("Part size (MB)", "每份大細（MB）");
        PickOutFolderBtn.Content = P("Output folder…", "輸出資料夾…");
        SplitBtn.Content = P("Split", "開始切割");

        JoinTitle.Text = P("Join parts", "合併部件");
        PickFirstPartBtn.Content = P("Pick first part (.001)…", "揀第一份（.001）…");
        HashChk.Content = P("Show SHA256 of the rejoined file", "顯示合併後檔案嘅 SHA256");
        JoinBtn.Content = P("Join", "開始合併");

        SourceText.Text = _source is null ? P("No file chosen", "未揀檔案") : _source;
        OutFolderText.Text = _outFolder is null ? P("No folder chosen", "未揀資料夾") : _outFolder;
        FirstPartText.Text = _firstPart is null ? P("No part chosen", "未揀部件") : _firstPart;
    }

    private async void PickSource_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFileAsync();
            if (!string.IsNullOrEmpty(path)) { _source = path; SourceText.Text = path; }
        }
        catch (Exception ex) { SplitStatus.Text = P("Could not pick file: ", "揀唔到檔案：") + ex.Message; }
    }

    private async void PickOutFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFolderAsync();
            if (!string.IsNullOrEmpty(path)) { _outFolder = path; OutFolderText.Text = path; }
        }
        catch (Exception ex) { SplitStatus.Text = P("Could not pick folder: ", "揀唔到資料夾：") + ex.Message; }
    }

    private async void PickFirstPart_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFileAsync();
            if (!string.IsNullOrEmpty(path)) { _firstPart = path; FirstPartText.Text = path; }
        }
        catch (Exception ex) { JoinStatus.Text = P("Could not pick part: ", "揀唔到部件：") + ex.Message; }
    }

    private async void Split_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (string.IsNullOrEmpty(_source)) { SplitStatus.Text = P("Pick a file first.", "請先揀一個檔案。"); return; }
        if (string.IsNullOrEmpty(_outFolder)) { SplitStatus.Text = P("Pick an output folder first.", "請先揀輸出資料夾。"); return; }

        double mb = double.IsNaN(PartSizeBox.Value) ? 100 : PartSizeBox.Value;
        long partBytes = (long)Math.Max(1, mb * 1024 * 1024);

        _busy = true;
        SplitBtn.IsEnabled = false;
        SplitProgress.Value = 0;
        SplitStatus.Text = P("Splitting…", "切割緊…");
        var progress = new Progress<double>(v => SplitProgress.Value = v);
        try
        {
            var r = await FileSplitService.SplitAsync(_source, partBytes, _outFolder, progress);
            SplitStatus.Text = P(
                $"Done — {r.Parts} part(s), {FileSplitService.FormatBytes(r.TotalBytes)} total. First: {Path.GetFileName(r.FirstPart)}",
                $"完成 — 共 {r.Parts} 份、合共 {FileSplitService.FormatBytes(r.TotalBytes)}。第一份：{Path.GetFileName(r.FirstPart)}");
        }
        catch (Exception ex)
        {
            SplitStatus.Text = P("Split failed: ", "切割失敗：") + ex.Message;
        }
        finally
        {
            _busy = false;
            SplitBtn.IsEnabled = true;
        }
    }

    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (string.IsNullOrEmpty(_firstPart)) { JoinStatus.Text = P("Pick the first part (.001) first.", "請先揀第一份（.001）。"); return; }

        // Suggest the original name by stripping the trailing ".NNN".
        var partName = Path.GetFileName(_firstPart);
        int dot = partName.LastIndexOf('.');
        var suggested = dot > 0 ? partName[..dot] : partName + ".joined";

        string? outPath;
        try
        {
            outPath = await FileDialogs.SaveFileAsync(suggested);
        }
        catch (Exception ex) { JoinStatus.Text = P("Could not choose output: ", "揀唔到輸出檔案：") + ex.Message; return; }
        if (string.IsNullOrEmpty(outPath)) return;

        _busy = true;
        JoinBtn.IsEnabled = false;
        JoinProgress.Value = 0;
        JoinStatus.Text = P("Joining…", "合併緊…");
        bool wantHash = HashChk.IsChecked == true;
        var progress = new Progress<double>(v => JoinProgress.Value = v);
        try
        {
            var r = await FileSplitService.JoinAsync(_firstPart, outPath, wantHash, progress);
            var baseMsg = P(
                $"Done — {r.Parts} part(s) → {FileSplitService.FormatBytes(r.TotalBytes)} at {Path.GetFileName(r.OutputPath)}",
                $"完成 — {r.Parts} 份 → {FileSplitService.FormatBytes(r.TotalBytes)}，儲存為 {Path.GetFileName(r.OutputPath)}");
            JoinStatus.Text = r.Sha256 is { } h ? baseMsg + $"\nSHA256: {h}" : baseMsg;
        }
        catch (Exception ex)
        {
            JoinStatus.Text = P("Join failed: ", "合併失敗：") + ex.Message;
        }
        finally
        {
            _busy = false;
            JoinBtn.IsEnabled = true;
        }
    }
}
