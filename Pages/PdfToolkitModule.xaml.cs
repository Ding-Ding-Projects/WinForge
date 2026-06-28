using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生 PDF 工具箱（Stirling-PDF 風格，純 C#）· Native, Stirling-PDF-style PDF toolkit.
/// 合併、分割、旋轉、刪頁／重排／抽頁、文字浮水印、加密／解密、抽取文字、圖片轉 PDF —
/// 全部用受管理 NuGet 程式庫（PDFsharp + PdfPig），絕不啟動或捆綁任何外部工具
/// （Stirling-PDF / Ghostscript 等）。Everything runs in-process via PDFsharp + PdfPig.
/// </summary>
public sealed partial class PdfToolkitModule : Page
{
    public PdfToolkitModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static readonly FileDialogs.Filter[] PdfFilter =
    {
        new("PDF files", "*.pdf"), new("All files", "*.*"),
    };
    private static readonly FileDialogs.Filter[] ImageFilter =
    {
        new("Images", "*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"), new("All files", "*.*"),
    };

    private void Render()
    {
        HeaderTitle.Text = "PDF Toolkit · PDF 工具箱";
        HeaderBlurb.Text = P(
            "A native, Stirling-PDF-style toolkit with an in-page PDF viewer — browse, inspect, search text, jump pages, extract ranges, rotate copies, merge, split, watermark, encrypt/decrypt, extract text, and build a PDF from images. Fully managed (PDFsharp + PdfPig) with WebView2 for viewing; nothing is shelled out or bundled.",
            "原生、Stirling-PDF 風格嘅工具箱，內置 PDF 檢視器 — 瀏覽、檢查、搜尋文字、跳頁、抽取範圍、旋轉副本、合併、分割、浮水印、加密／解密、抽取文字、由圖片整 PDF。完全受管理（PDFsharp + PdfPig），檢視用 WebView2；唔會呼叫或捆綁任何外部工具。");

        CardHost.Children.Clear();
        CardHost.Children.Add(BuildViewerCard());
        CardHost.Children.Add(BuildMergeCard());
        CardHost.Children.Add(BuildSplitCard());
        CardHost.Children.Add(BuildRotateCard());
        CardHost.Children.Add(BuildPagesCard());
        CardHost.Children.Add(BuildWatermarkCard());
        CardHost.Children.Add(BuildEncryptCard());
        CardHost.Children.Add(BuildDecryptCard());
        CardHost.Children.Add(BuildExtractTextCard());
        CardHost.Children.Add(BuildImagesToPdfCard());
        CardHost.Children.Add(BuildNote());
    }

    // ───────────────────────── shared card scaffolding ─────────────────────────

    /// <summary>建立一張卡（標題、說明、內容、結果列）· Build one tool card; returns the body panel to fill.</summary>
    private static (Border card, StackPanel body, InfoBar result) NewCard(string glyph, string titleEn, string titleZh, string descEn, string descZh)
    {
        var body = new StackPanel { Spacing = 10 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        header.Children.Add(new FontIcon { Glyph = glyph, FontSize = 18, VerticalAlignment = VerticalAlignment.Center });
        header.Children.Add(new TextBlock { Text = $"{titleEn} · {titleZh}", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16, VerticalAlignment = VerticalAlignment.Center });
        body.Children.Add(header);

        body.Children.Add(new TextBlock
        {
            Text = P(descEn, descZh),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
        });

        var result = new InfoBar { IsOpen = false, IsClosable = true, Margin = new Thickness(0, 2, 0, 0) };
        body.Children.Add(result);

        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
            Child = body,
        };
        return (card, body, result);
    }

    private static Button PrimaryButton(string en, string zh)
        => new() { Content = P(en, zh), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };

    private static TextBox MakeBox(string headerEn, string headerZh, string placeholderEn = "", string placeholderZh = "")
        => new()
        {
            Header = P(headerEn, headerZh),
            PlaceholderText = P(placeholderEn, placeholderZh),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

    /// <summary>標準「輸出列」：唯讀路徑框 + 瀏覽按鈕（另存 / 選資料夾）· A read-only path box + Browse button.</summary>
    private static (Grid row, TextBox box) OutputRow(string headerEn, string headerZh)
    {
        var box = new TextBox { Header = P(headerEn, headerZh), IsReadOnly = true, HorizontalAlignment = HorizontalAlignment.Stretch };
        var browse = new Button { Content = P("Browse…", "瀏覽…"), VerticalAlignment = VerticalAlignment.Bottom };
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(box, 0); Grid.SetColumn(browse, 1);
        row.Children.Add(box); row.Children.Add(browse);
        row.Tag = browse; // expose the button so callers can wire Click
        return (row, box);
    }

    private static Button BrowseOf(Grid outputRow) => (Button)outputRow.Tag;

    private static void ShowOk(InfoBar bar, TweakResult r, bool offerOpen, string? path)
    {
        bar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        bar.Message = r.Message?.Display ?? "";
        bar.IsOpen = true;
        bar.ActionButton = null;
        if (r.Success && offerOpen && !string.IsNullOrEmpty(path))
        {
            var open = new Button { Content = P("Open folder · 開資料夾", "開資料夾") };
            open.Click += (_, _) => OpenInExplorer(path);
            bar.ActionButton = open;
        }
    }

    private static void OpenInExplorer(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            else if (File.Exists(path))
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        }
        catch { }
    }

    /// <summary>包住一個動作：顯示忙碌、跑、出結果 · Run with busy indicator and report into the bar.</summary>
    private static async Task RunOp(Button btn, InfoBar bar, ProgressRing busy, Func<Task<TweakResult>> op, bool offerOpen, Func<string?> pathForOpen)
    {
        btn.IsEnabled = false; busy.IsActive = true; busy.Visibility = Visibility.Visible;
        try
        {
            var r = await op();
            ShowOk(bar, r, offerOpen, r.Success ? pathForOpen() : null);
        }
        catch (Exception ex)
        {
            bar.Severity = InfoBarSeverity.Error;
            bar.Message = P($"Failed: {ex.Message}", $"失敗：{ex.Message}");
            bar.IsOpen = true;
        }
        finally { btn.IsEnabled = true; busy.IsActive = false; busy.Visibility = Visibility.Collapsed; }
    }

    private static ProgressRing NewBusy() => new() { Width = 18, Height = 18, IsActive = false, Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };

    private static StackPanel ActionRow(params UIElement[] kids)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 2, 0, 0) };
        foreach (var k in kids) sp.Children.Add(k);
        return sp;
    }

    // ─────────────────────────────── Merge ───────────────────────────────

    private Border BuildMergeCard()
    {
        var (card, body, result) = NewCard("", "Merge PDFs", "合併 PDF",
            "Combine several PDFs into one. Add files, drag to reorder, then choose where to save.",
            "將多個 PDF 合併成一個。加入檔案、拖曳調次序，再揀儲存位置。");

        var items = new ObservableCollection<string>();
        var list = new ListView
        {
            ItemsSource = items,
            SelectionMode = ListViewSelectionMode.Single,
            CanReorderItems = true,
            CanDragItems = true,
            AllowDrop = true,
            ReorderMode = ListViewReorderMode.Enabled,
            MaxHeight = 180,
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
        };
        list.ItemTemplate = null;

        var add = new Button { Content = P("Add files… · 加入檔案", "加入檔案…") };
        var up = new Button { Content = P("Move up · 上移", "上移") };
        var down = new Button { Content = P("Move down · 下移", "下移") };
        var remove = new Button { Content = P("Remove · 移除", "移除") };
        var clear = new Button { Content = P("Clear · 清空", "清空") };

        add.Click += async (_, _) =>
        {
            var picked = await FileDialogs.OpenFilesAsync(PdfFilter, P("Select PDFs to merge", "揀要合併嘅 PDF"));
            foreach (var p in picked) items.Add(p);
        };
        up.Click += (_, _) =>
        {
            int i = list.SelectedIndex;
            if (i > 0) { var v = items[i]; items.RemoveAt(i); items.Insert(i - 1, v); list.SelectedIndex = i - 1; }
        };
        down.Click += (_, _) =>
        {
            int i = list.SelectedIndex;
            if (i >= 0 && i < items.Count - 1) { var v = items[i]; items.RemoveAt(i); items.Insert(i + 1, v); list.SelectedIndex = i + 1; }
        };
        remove.Click += (_, _) => { if (list.SelectedIndex >= 0) items.RemoveAt(list.SelectedIndex); };
        clear.Click += (_, _) => items.Clear();

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) =>
        {
            var p = await FileDialogs.SaveFileAsync("merged.pdf", PdfFilter, "pdf", P("Save merged PDF", "儲存合併 PDF"));
            if (p is not null) outBox.Text = p;
        };

        var busy = NewBusy();
        var run = PrimaryButton("Merge · 合併", "合併");
        run.Click += async (_, _) =>
        {
            if (items.Count < 2) { Warn(result, "Add at least two PDFs.", "請加入至少兩個 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            await RunOp(run, result, busy, () => PdfToolkitService.MergeAsync(items.ToList(), outBox.Text), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, list, ActionRow(add, up, down, remove, clear), outRow, ActionRow(run, busy));
        return card;
    }

    // ─────────────────────────────── Split ───────────────────────────────

    private Border BuildSplitCard()
    {
        var (card, body, result) = NewCard("", "Split PDF", "分割 PDF",
            "Split one PDF into many. Either one file per page, or one file per page-range (e.g. 1-3, 4-6, 7).",
            "將一個 PDF 分割成多個。可以每頁一個檔，或者每個頁碼範圍一個檔（例如 1-3、4-6、7）。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select PDF", "揀 PDF")); if (p is not null) inBox.Text = p; };

        var pw = new PasswordBox { Header = P("Password (if protected) · 密碼（如有保護）", "密碼（如有保護）") };

        var mode = new ComboBox { Header = P("Mode · 模式", "模式"), HorizontalAlignment = HorizontalAlignment.Stretch };
        mode.Items.Add(new ComboBoxItem { Content = P("One file per page · 每頁一個檔", "每頁一個檔"), Tag = "page" });
        mode.Items.Add(new ComboBoxItem { Content = P("By page ranges · 按頁碼範圍", "按頁碼範圍"), Tag = "ranges" });
        mode.SelectedIndex = 0;

        var ranges = MakeBox("Ranges (one group per output file) · 範圍（每組一個輸出檔）", "範圍（每組一個輸出檔）", "1-3, 4-6, 7", "1-3, 4-6, 7");
        ranges.Visibility = Visibility.Collapsed;
        mode.SelectionChanged += (_, _) =>
            ranges.Visibility = (mode.SelectedItem as ComboBoxItem)?.Tag as string == "ranges" ? Visibility.Visible : Visibility.Collapsed;

        var (outRow, outBox) = OutputRow("Output folder · 輸出資料夾", "輸出資料夾");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.OpenFolderAsync(P("Choose output folder", "揀輸出資料夾")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Split · 分割", "分割");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output folder.", "請揀輸出資料夾。"); return; }
            var byRanges = (mode.SelectedItem as ComboBoxItem)?.Tag as string == "ranges";
            string? pwd = string.IsNullOrEmpty(pw.Password) ? null : pw.Password;
            await RunOp(run, result, busy,
                () => byRanges
                    ? PdfToolkitService.SplitByRangesAsync(inBox.Text, outBox.Text, ranges.Text, pwd)
                    : PdfToolkitService.SplitPerPageAsync(inBox.Text, outBox.Text, pwd),
                true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, pw, mode, ranges, outRow, ActionRow(run, busy));
        return card;
    }

    // ─────────────────────────────── Rotate ───────────────────────────────

    private Border BuildRotateCard()
    {
        var (card, body, result) = NewCard("", "Rotate pages", "旋轉頁面",
            "Rotate all pages, or a range, by 90/180/270 degrees clockwise.",
            "將全部頁或者指定範圍順時針旋轉 90／180／270 度。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select PDF", "揀 PDF")); if (p is not null) inBox.Text = p; };

        var pw = new PasswordBox { Header = P("Password (if protected) · 密碼（如有保護）", "密碼（如有保護）") };

        var deg = new ComboBox { Header = P("Angle · 角度", "角度"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var d in new[] { 90, 180, 270 }) deg.Items.Add(new ComboBoxItem { Content = $"{d}°", Tag = d });
        deg.SelectedIndex = 0;

        var range = MakeBox("Pages (blank = all) · 頁碼（留空 = 全部）", "頁碼（留空 = 全部）", "e.g. 2-5, 8", "例如 2-5, 8");

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("rotated.pdf", PdfFilter, "pdf", P("Save rotated PDF", "儲存旋轉 PDF")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Rotate · 旋轉", "旋轉");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            int d = (int)((ComboBoxItem)deg.SelectedItem).Tag;
            string? pwd = string.IsNullOrEmpty(pw.Password) ? null : pw.Password;
            await RunOp(run, result, busy, () => PdfToolkitService.RotateAsync(inBox.Text, outBox.Text, d, range.Text, pwd), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, pw, deg, range, outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────── Delete / Extract / Reorder pages ─────────────────────

    private Border BuildPagesCard()
    {
        var (card, body, result) = NewCard("", "Delete · Extract · Reorder pages", "刪頁 · 抽頁 · 重排",
            "Delete the named pages, extract them into a new PDF, or reorder by listing the new order (a permutation of all pages, e.g. 3,1,2,4).",
            "刪除指定頁、將佢哋抽成新 PDF，或者輸入新次序重排（要係全部頁嘅一個排列，例如 3,1,2,4）。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select PDF", "揀 PDF")); if (p is not null) inBox.Text = p; };

        var pw = new PasswordBox { Header = P("Password (if protected) · 密碼（如有保護）", "密碼（如有保護）") };

        var op = new ComboBox { Header = P("Operation · 操作", "操作"), HorizontalAlignment = HorizontalAlignment.Stretch };
        op.Items.Add(new ComboBoxItem { Content = P("Delete pages · 刪除頁", "刪除頁"), Tag = "delete" });
        op.Items.Add(new ComboBoxItem { Content = P("Extract pages · 抽取頁", "抽取頁"), Tag = "extract" });
        op.Items.Add(new ComboBoxItem { Content = P("Reorder pages · 重新排序", "重新排序"), Tag = "reorder" });
        op.SelectedIndex = 0;

        var spec = MakeBox("Pages / order · 頁碼／次序", "頁碼／次序", "e.g. 2-4, 7   or   3,1,2,4", "例如 2-4, 7  或  3,1,2,4");

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("pages.pdf", PdfFilter, "pdf", P("Save PDF", "儲存 PDF")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Apply · 套用", "套用");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            if (string.IsNullOrWhiteSpace(spec.Text)) { Warn(result, "Enter the pages / order.", "請輸入頁碼／次序。"); return; }
            var which = (op.SelectedItem as ComboBoxItem)?.Tag as string;
            string? pwd = string.IsNullOrEmpty(pw.Password) ? null : pw.Password;
            await RunOp(run, result, busy, () => which switch
            {
                "extract" => PdfToolkitService.ExtractPagesAsync(inBox.Text, outBox.Text, spec.Text, pwd),
                "reorder" => PdfToolkitService.ReorderAsync(inBox.Text, outBox.Text, spec.Text, pwd),
                _ => PdfToolkitService.DeletePagesAsync(inBox.Text, outBox.Text, spec.Text, pwd),
            }, true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, pw, op, spec, outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────────────── Watermark ─────────────────────────────

    private Border BuildWatermarkCard()
    {
        var (card, body, result) = NewCard("", "Text watermark", "文字浮水印",
            "Stamp a diagonal text watermark across every page (set the text, opacity, angle and size).",
            "喺每頁打斜印上文字浮水印（可設定文字、透明度、角度同大細）。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select PDF", "揀 PDF")); if (p is not null) inBox.Text = p; };

        var pw = new PasswordBox { Header = P("Password (if protected) · 密碼（如有保護）", "密碼（如有保護）") };
        var text = MakeBox("Watermark text · 浮水印文字", "浮水印文字", "CONFIDENTIAL", "機密");
        text.Text = "CONFIDENTIAL";

        var opacity = new NumberBox { Header = P("Opacity (0–100%) · 透明度", "透明度（0–100%）"), Value = 25, Minimum = 2, Maximum = 100, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var angle = new NumberBox { Header = P("Angle (°) · 角度", "角度（°）"), Value = 45, Minimum = -90, Maximum = 90, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var size = new NumberBox { Header = P("Font size (pt) · 字體大細", "字體大細（pt）"), Value = 48, Minimum = 6, Maximum = 200, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var nums = new Grid { ColumnSpacing = 8 };
        for (int i = 0; i < 3; i++) nums.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(opacity, 0); Grid.SetColumn(angle, 1); Grid.SetColumn(size, 2);
        nums.Children.Add(opacity); nums.Children.Add(angle); nums.Children.Add(size);

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("watermarked.pdf", PdfFilter, "pdf", P("Save watermarked PDF", "儲存浮水印 PDF")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Apply watermark · 加浮水印", "加浮水印");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            if (string.IsNullOrWhiteSpace(text.Text)) { Warn(result, "Enter the watermark text.", "請輸入浮水印文字。"); return; }
            string? pwd = string.IsNullOrEmpty(pw.Password) ? null : pw.Password;
            await RunOp(run, result, busy, () => PdfToolkitService.WatermarkAsync(
                inBox.Text, outBox.Text, text.Text, opacity.Value / 100.0, angle.Value, size.Value, pwd), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, pw, text, nums, outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────────────── Encrypt ─────────────────────────────

    private Border BuildEncryptCard()
    {
        var (card, body, result) = NewCard("", "Encrypt (set password)", "加密（設定密碼）",
            "Protect a PDF with a user password (to open) and an optional owner password (for permissions).",
            "用使用者密碼（開啟用）同可選嘅擁有者密碼（權限用）保護 PDF。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select PDF", "揀 PDF")); if (p is not null) inBox.Text = p; };

        var userPw = new PasswordBox { Header = P("User password (to open) · 使用者密碼（開啟）", "使用者密碼（開啟）") };
        var ownerPw = new PasswordBox { Header = P("Owner password (optional) · 擁有者密碼（可選）", "擁有者密碼（可選）") };

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("encrypted.pdf", PdfFilter, "pdf", P("Save encrypted PDF", "儲存加密 PDF")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Encrypt · 加密", "加密");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            if (string.IsNullOrEmpty(userPw.Password) && string.IsNullOrEmpty(ownerPw.Password)) { Warn(result, "Enter at least one password.", "請輸入至少一個密碼。"); return; }
            await RunOp(run, result, busy, () => PdfToolkitService.EncryptAsync(
                inBox.Text, outBox.Text, userPw.Password, string.IsNullOrEmpty(ownerPw.Password) ? null : ownerPw.Password), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, userPw, ownerPw, outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────────────── Decrypt ─────────────────────────────

    private Border BuildDecryptCard()
    {
        var (card, body, result) = NewCard("", "Decrypt (remove password)", "解密（移除密碼）",
            "Remove the password from a protected PDF — you must supply the current password.",
            "由受保護嘅 PDF 移除密碼 — 必須提供現有密碼。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select protected PDF", "揀受保護 PDF")); if (p is not null) inBox.Text = p; };

        var pw = new PasswordBox { Header = P("Current password · 現有密碼", "現有密碼") };

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("decrypted.pdf", PdfFilter, "pdf", P("Save decrypted PDF", "儲存解密 PDF")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Decrypt · 解密", "解密");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            if (string.IsNullOrEmpty(pw.Password)) { Warn(result, "Enter the current password.", "請輸入現有密碼。"); return; }
            await RunOp(run, result, busy, () => PdfToolkitService.DecryptAsync(inBox.Text, outBox.Text, pw.Password), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, pw, outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────────────── Extract text ─────────────────────────────

    private Border BuildExtractTextCard()
    {
        var (card, body, result) = NewCard("", "Extract text → .txt", "抽取文字 → .txt",
            "Pull all text out of a PDF into a plain-text file (uses PdfPig). Scanned/image-only PDFs yield no text.",
            "將 PDF 入面所有文字抽取成純文字檔（用 PdfPig）。掃描／純圖片 PDF 抽唔到文字。");

        var (inRow, inBox) = OutputRow("Input PDF · 輸入 PDF", "輸入 PDF");
        BrowseOf(inRow).Click += async (_, _) => { var p = await FileDialogs.OpenFileAsync(PdfFilter, P("Select PDF", "揀 PDF")); if (p is not null) inBox.Text = p; };

        var pw = new PasswordBox { Header = P("Password (if protected) · 密碼（如有保護）", "密碼（如有保護）") };

        var (outRow, outBox) = OutputRow("Output text file · 輸出文字檔", "輸出文字檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("extracted.txt", new[] { new FileDialogs.Filter("Text", "*.txt"), new FileDialogs.Filter("All files", "*.*") }, "txt", P("Save text", "儲存文字")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Extract text · 抽取文字", "抽取文字");
        run.Click += async (_, _) =>
        {
            if (string.IsNullOrEmpty(inBox.Text)) { Warn(result, "Choose an input PDF.", "請揀輸入 PDF。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output text file.", "請揀輸出文字檔。"); return; }
            string? pwd = string.IsNullOrEmpty(pw.Password) ? null : pw.Password;
            await RunOp(run, result, busy, () => PdfToolkitService.ExtractTextAsync(inBox.Text, outBox.Text, pwd), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, inRow, pw, outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────────────── Images → PDF ─────────────────────────────

    private Border BuildImagesToPdfCard()
    {
        var (card, body, result) = NewCard("", "Images → PDF", "圖片轉 PDF",
            "Combine selected images (PNG/JPG/BMP/GIF/TIFF) into a single PDF — one image per page, in order.",
            "將揀咗嘅圖片（PNG／JPG／BMP／GIF／TIFF）合併成一個 PDF — 每頁一張，按次序。");

        var items = new ObservableCollection<string>();
        var list = new ListView
        {
            ItemsSource = items,
            SelectionMode = ListViewSelectionMode.Single,
            CanReorderItems = true,
            CanDragItems = true,
            AllowDrop = true,
            ReorderMode = ListViewReorderMode.Enabled,
            MaxHeight = 180,
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
        };

        var add = new Button { Content = P("Add images… · 加入圖片", "加入圖片…") };
        var up = new Button { Content = P("Move up · 上移", "上移") };
        var down = new Button { Content = P("Move down · 下移", "下移") };
        var remove = new Button { Content = P("Remove · 移除", "移除") };
        var clear = new Button { Content = P("Clear · 清空", "清空") };

        add.Click += async (_, _) => { foreach (var p in await FileDialogs.OpenFilesAsync(ImageFilter, P("Select images", "揀圖片"))) items.Add(p); };
        up.Click += (_, _) => { int i = list.SelectedIndex; if (i > 0) { var v = items[i]; items.RemoveAt(i); items.Insert(i - 1, v); list.SelectedIndex = i - 1; } };
        down.Click += (_, _) => { int i = list.SelectedIndex; if (i >= 0 && i < items.Count - 1) { var v = items[i]; items.RemoveAt(i); items.Insert(i + 1, v); list.SelectedIndex = i + 1; } };
        remove.Click += (_, _) => { if (list.SelectedIndex >= 0) items.RemoveAt(list.SelectedIndex); };
        clear.Click += (_, _) => items.Clear();

        var (outRow, outBox) = OutputRow("Output file · 輸出檔", "輸出檔");
        BrowseOf(outRow).Click += async (_, _) => { var p = await FileDialogs.SaveFileAsync("images.pdf", PdfFilter, "pdf", P("Save PDF", "儲存 PDF")); if (p is not null) outBox.Text = p; };

        var busy = NewBusy();
        var run = PrimaryButton("Build PDF · 製作 PDF", "製作 PDF");
        run.Click += async (_, _) =>
        {
            if (items.Count == 0) { Warn(result, "Add at least one image.", "請加入至少一張圖片。"); return; }
            if (string.IsNullOrEmpty(outBox.Text)) { Warn(result, "Choose an output file.", "請揀輸出檔。"); return; }
            await RunOp(run, result, busy, () => PdfToolkitService.ImagesToPdfAsync(items.ToList(), outBox.Text), true, () => outBox.Text);
        };

        InsertBeforeResult(body, result, list, ActionRow(add, up, down, remove, clear), outRow, ActionRow(run, busy));
        return card;
    }

    // ───────────────────────────── footer note ─────────────────────────────

    private Border BuildNote()
    {
        var bar = new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Informational,
            Title = P("Fully managed · 完全受管理", "完全受管理"),
            Message = P(
                "All write operations run in-process via the PDFsharp and PdfPig NuGet libraries. No Stirling-PDF, Ghostscript or external tool is launched or bundled. The viewer uses the built-in Edge/WebView2 PDF renderer and keeps editing actions as save-to-new-file operations.",
                "全部寫入操作都喺程序內用 PDFsharp 同 PdfPig NuGet 程式庫執行。唔會啟動或捆綁 Stirling-PDF、Ghostscript 或外部工具。檢視器使用 Edge/WebView2 內建 PDF 渲染，編輯動作都係另存新檔。"),
        };
        return new Border { Child = bar };
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static void Warn(InfoBar bar, string en, string zh)
    {
        bar.Severity = InfoBarSeverity.Warning;
        bar.Message = P(en, zh);
        bar.ActionButton = null;
        bar.IsOpen = true;
    }

    /// <summary>將控件插入卡體，喺結果列之前（保持結果列喺最底）· Insert controls before the result bar.</summary>
    private static void InsertBeforeResult(StackPanel body, InfoBar result, params UIElement[] controls)
    {
        int at = body.Children.IndexOf(result);
        foreach (var c in controls) body.Children.Insert(at++, c);
    }
}
