using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTTP 標頭參考 · HTTP headers reference — offline lookup of ~80 common HTTP request/response
/// headers with bilingual (English + 粵語) descriptions, direction, category and an example.
/// Search + category + direction filters. Click a row to copy the header (or "Name: example").
/// Reference tool only — never throws, no redirect, no network. Mirrors the Awake module shell.
/// </summary>
public sealed partial class HttpHeaderRefModule : Page
{
    private const string AllTag = "__all__";
    private bool _suppress;

    public HttpHeaderRefModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        BuildFilters();
        ApplyFilter();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        try
        {
            Render();
            BuildFilters();
            ApplyFilter();
        }
        catch { /* reference tool — never throw */ }
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "HTTP Headers Ref · HTTP 標頭參考";
            HeaderBlurb.Text = P("An offline reference of ~80 common HTTP headers — direction, category, a plain-language note and an example value. Search or filter, then click a row to copy it.",
                "約 80 個常用 HTTP 標頭嘅離線參考 — 方向、分類、白話解釋同埋例子值。搵嘢或者篩選，再撳一行就複製。");
            if (SearchBox != null)
                SearchBox.PlaceholderText = P("Search name or description…", "搵標頭名或者描述…");
            UpdateStatus(null);
        }
        catch { /* never throw */ }
    }

    private void BuildFilters()
    {
        try
        {
            _suppress = true;

            // Category box — first entry = All, then the catalogue's categories.
            object? prevCat = CategoryBox?.SelectedItem is ComboBoxItem ci ? ci.Tag : null;
            if (CategoryBox != null)
            {
                CategoryBox.Items.Clear();
                CategoryBox.Items.Add(new ComboBoxItem { Content = P("All categories", "全部分類"), Tag = AllTag });
                foreach (var cat in HttpHeaderRefService.Categories())
                    CategoryBox.Items.Add(new ComboBoxItem { Content = cat, Tag = cat });
                RestoreSelection(CategoryBox, prevCat);
            }

            // Direction box — All / Request / Response.
            object? prevDir = DirectionBox?.SelectedItem is ComboBoxItem di ? di.Tag : null;
            if (DirectionBox != null)
            {
                DirectionBox.Items.Clear();
                DirectionBox.Items.Add(new ComboBoxItem { Content = P("All directions", "全部方向"), Tag = AllTag });
                DirectionBox.Items.Add(new ComboBoxItem { Content = P("Request", "請求"), Tag = "Request" });
                DirectionBox.Items.Add(new ComboBoxItem { Content = P("Response", "回應"), Tag = "Response" });
                RestoreSelection(DirectionBox, prevDir);
            }

            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private static void RestoreSelection(ComboBox box, object? tag)
    {
        try
        {
            int idx = 0;
            if (tag != null)
            {
                for (int i = 0; i < box.Items.Count; i++)
                    if (box.Items[i] is ComboBoxItem it && Equals(it.Tag, tag)) { idx = i; break; }
            }
            box.SelectedIndex = box.Items.Count > 0 ? idx : -1;
        }
        catch { if (box.Items.Count > 0) box.SelectedIndex = 0; }
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        try
        {
            string? query = SearchBox?.Text;

            string? category = null;
            if (CategoryBox?.SelectedItem is ComboBoxItem cItem && cItem.Tag is string ct && ct != AllTag)
                category = ct;

            HttpHeaderRefService.Direction? dir = null;
            if (DirectionBox?.SelectedItem is ComboBoxItem dItem && dItem.Tag is string dt && dt != AllTag)
                dir = dt == "Request" ? HttpHeaderRefService.Direction.Request : HttpHeaderRefService.Direction.Response;

            IReadOnlyList<HttpHeaderRefService.HeaderInfo> results = HttpHeaderRefService.Filter(query, category, dir);

            if (HeaderList != null)
                HeaderList.ItemsSource = results;

            if (CountText != null)
                CountText.Text = P($"{results.Count} of {HttpHeaderRefService.All.Count} headers",
                                    $"顯示 {results.Count} / {HttpHeaderRefService.All.Count} 個標頭");

            UpdateStatus(null);
        }
        catch
        {
            if (HeaderList != null) HeaderList.ItemsSource = Array.Empty<HttpHeaderRefService.HeaderInfo>();
        }
    }

    private void Header_Click(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not HttpHeaderRefService.HeaderInfo info) return;
            string text = HttpHeaderRefService.CopyText(info);
            if (string.IsNullOrEmpty(text)) return;

            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);

            UpdateStatus(P($"Copied “{text}” to the clipboard.", $"已複製「{text}」到剪貼簿。"));
        }
        catch
        {
            UpdateStatus(P("Couldn't copy that to the clipboard.", "複製唔到去剪貼簿。"));
        }
    }

    private void UpdateStatus(string? message)
    {
        try
        {
            if (StatusText == null) return;
            StatusText.Text = message ?? P("Click any header to copy it (name, or “Name: example” when an example exists).",
                                           "撳任何一個標頭就複製佢（標頭名，或者有例子時嘅「名: 例子」）。");
        }
        catch { /* never throw */ }
    }
}
