using System;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 剪貼簿格式檢查器 · Clipboard format inspector — a "Read clipboard" button lists every format the
/// clipboard currently exposes, flags which StandardDataFormats are present, and previews text /
/// notes HTML length, bitmaps and storage items. Reads on demand only; never writes; never throws.
/// </summary>
public sealed partial class ClipInspectModule : Page
{
    private readonly ObservableCollection<ClipInspectService.FormatRow> _formats = new();
    private bool _busy;

    public ClipInspectModule()
    {
        InitializeComponent();
        FormatsList.ItemsSource = _formats;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Clipboard Inspector · 剪貼簿檢查器";
        HeaderBlurb.Text = P("Peek at what's on the clipboard right now — every data format it exposes, which standard types are present, and a preview of any text. Read-only: it only looks when you press the button and never changes the clipboard.",
            "睇下而家剪貼簿裏面有乜 — 佢提供嘅每一種資料格式、有邊啲標準類型、仲有文字嘅預覽。純讀取：淨係你㩒制先至讀，永遠唔會改動剪貼簿。");
        ReadBtn.Content = P("Read clipboard", "讀取剪貼簿");
        RefreshBtn.Content = P("Refresh", "重新讀取");
        StdTitle.Text = P("Standard formats", "標準格式");
        FormatsTitle.Text = P("All available formats", "所有可用格式");
        PreviewTitle.Text = P("Text preview", "文字預覽");
        if (string.IsNullOrEmpty(StatusText.Text))
            StatusText.Text = P("Press \"Read clipboard\" to inspect the current clipboard contents.", "㩒「讀取剪貼簿」嚟檢查而家剪貼簿嘅內容。");
    }

    private async void Read_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        ReadBtn.IsEnabled = false;
        RefreshBtn.IsEnabled = false;
        StatusText.Text = P("Reading clipboard…", "正在讀取剪貼簿…");
        try
        {
            var r = await ClipInspectService.ReadAsync();

            _formats.Clear();
            if (r.Ok)
                foreach (var f in r.Formats) _formats.Add(f);

            if (!r.Ok)
            {
                StdText.Text = "";
                PreviewText.Text = "";
                StatusText.Text = P($"Couldn't read the clipboard: {r.Error}", $"讀唔到剪貼簿：{r.Error}");
                return;
            }

            StdText.Text = BuildStandard(r);
            PreviewText.Text = string.IsNullOrEmpty(r.TextPreview)
                ? P("(no text on the clipboard)", "（剪貼簿冇文字）")
                : r.TextPreview!;

            StatusText.Text = r.Formats.Count == 0
                ? P("The clipboard is empty (no formats).", "剪貼簿係空嘅（冇任何格式）。")
                : P($"Read {r.Formats.Count} format(s) from the clipboard.", $"由剪貼簿讀到 {r.Formats.Count} 種格式。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Couldn't read the clipboard: {ex.Message}", $"讀唔到剪貼簿：{ex.Message}");
        }
        finally
        {
            ReadBtn.IsEnabled = true;
            RefreshBtn.IsEnabled = true;
            _busy = false;
        }
    }

    private string BuildStandard(ClipInspectService.InspectResult r)
    {
        var sb = new StringBuilder();
        void Line(bool present, string en, string zh)
        {
            string mark = present ? "✔" : "—";
            sb.AppendLine($"{mark} {P(en, zh)}");
        }

        Line(r.HasText, "Text", "文字");
        if (r.HasHtml)
            sb.AppendLine($"✔ {P($"HTML ({r.HtmlLength} chars)", $"HTML（{r.HtmlLength} 字元）")}");
        else
            Line(false, "HTML", "HTML");
        Line(r.HasRtf, "Rich text (RTF)", "格式化文字（RTF）");
        Line(r.HasBitmap, "Bitmap image", "點陣圖影像");
        if (r.HasStorageItems)
            sb.AppendLine($"✔ {P($"Files / folders ({r.StorageItemCount})", $"檔案／資料夾（{r.StorageItemCount}）")}");
        else
            Line(false, "Files / folders", "檔案／資料夾");
        Line(r.HasWebLink, "Web link", "網頁連結");
        Line(r.HasApplicationLink, "Application link", "應用程式連結");

        return sb.ToString().TrimEnd();
    }
}
