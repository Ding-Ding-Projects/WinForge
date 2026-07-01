using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;
using Windows.ApplicationModel.DataTransfer;

namespace WinForge.Pages;

/// <summary>
/// MIME 類型查詢 · MIME type lookup — search a curated table of ~150 file-extension↔MIME-type mappings,
/// and detect the MIME type from any filename. Copy either to the clipboard. Pure-managed, bilingual.
/// </summary>
public sealed partial class MimeTypesModule : Page
{
    private string? _lastDetect;

    public MimeTypesModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        ApplyFilter();
        RunDetect();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "MIME Type Lookup · MIME 類型查詢";
            HeaderBlurb.Text = P(
                $"Look up the MIME (content) type for any of {MimeTypesService.Count} common file extensions, or detect it from a filename. Handy for web servers, uploads and Content-Type headers.",
                $"喺 {MimeTypesService.Count} 個常見副檔名裏面查 MIME（內容）類型，或者由檔名偵測出嚟。整網站伺服器、上載、Content-Type 標頭都啱用。");
            SearchTitle.Text = P("Search extensions & MIME types", "搜尋副檔名同 MIME 類型");
            SearchBox.PlaceholderText = P("e.g. .png, png, or image/", "例如 .png、png 或者 image/");
            CopyHint.Text = P("Click a row to copy its MIME type to the clipboard.", "撳一行就會複製個 MIME 類型去剪貼簿。");
            DetectTitle.Text = P("Detect from a filename", "由檔名偵測");
            DetectBox.PlaceholderText = P("e.g. report.pdf or C:\\photos\\cat.jpg", "例如 report.pdf 或者 C:\\photos\\cat.jpg");
            DetectCopyBtn.Content = P("Copy", "複製");
            UpdateResultCount();
            RunDetect();
        }
        catch { /* never throw from UI */ }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        try
        {
            ResultsList.ItemsSource = MimeTypesService.Search(SearchBox.Text);
            UpdateResultCount();
        }
        catch { /* never throw */ }
    }

    private void UpdateResultCount()
    {
        try
        {
            int n = (ResultsList.ItemsSource as System.Collections.ICollection)?.Count ?? 0;
            ResultCount.Text = P($"{n} match(es)", $"{n} 個結果");
        }
        catch { /* never throw */ }
    }

    private void Results_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is MimeTypesService.MimeRow row && !string.IsNullOrEmpty(row.Mime))
            {
                Copy(row.Mime);
                ResultCount.Text = P($"Copied: {row.Mime}", $"已複製：{row.Mime}");
            }
        }
        catch { /* never throw */ }
    }

    private void Detect_Changed(object sender, TextChangedEventArgs e) => RunDetect();

    private void RunDetect()
    {
        try
        {
            string name = DetectBox?.Text ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                _lastDetect = null;
                DetectResult.Text = P("Type a filename above.", "喺上面打個檔名。");
                DetectCopyBtn.IsEnabled = false;
                return;
            }

            string? mime = MimeTypesService.DetectFromFilename(name);
            if (mime is null)
            {
                _lastDetect = null;
                DetectResult.Text = P("No extension found in that name.", "個名冇搵到副檔名。");
                DetectCopyBtn.IsEnabled = false;
            }
            else
            {
                _lastDetect = mime;
                DetectResult.Text = mime;
                DetectCopyBtn.IsEnabled = true;
            }
        }
        catch { /* never throw */ }
    }

    private void DetectCopy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!string.IsNullOrEmpty(_lastDetect))
            {
                Copy(_lastDetect!);
                DetectResult.Text = P($"Copied: {_lastDetect}", $"已複製：{_lastDetect}");
            }
        }
        catch { /* never throw */ }
    }

    private static void Copy(string text)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
        }
        catch { /* clipboard may be busy — ignore */ }
    }
}
