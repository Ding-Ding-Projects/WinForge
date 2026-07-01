using System;
using System.Collections.Generic;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HTTP 狀態碼參考 · HTTP status code reference — an offline, searchable catalogue of every standard
/// HTTP status code (1xx–5xx) with reason phrase and a one-line English + Cantonese description.
/// Filter by text or class, jump straight to a code, and click a row to copy "code name". Bilingual,
/// never-throw. Backed by <see cref="HttpStatusService"/>.
/// </summary>
public sealed partial class HttpStatusModule : Page
{
    /// <summary>Row view-model used by the ListView via classic {Binding}.</summary>
    public sealed class Row
    {
        public int Code { get; init; }
        public string CodeText { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Desc { get; init; } = string.Empty;
        public Brush Accent { get; init; } = new SolidColorBrush(Colors.Gray);
    }

    private readonly List<Row> _rows = new();
    private bool _suppress;

    public HttpStatusModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildCategories();
        Render();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        BuildCategories();
        Render();
        Refresh();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("HTTP Status Codes · HTTP 狀態碼", "HTTP 狀態碼 · HTTP Status Codes");
            HeaderBlurb.Text = P(
                "An offline reference for every standard HTTP status code (1xx–5xx). Search by number or text, filter by class, or jump to a single code. Click any row to copy its code and name.",
                "所有標準 HTTP 狀態碼（1xx–5xx）嘅離線參考。可以用號碼或文字搜尋、按類別篩選，或者直接跳去某個碼。撳一行就會複製個碼同名稱。");
            SearchBox.PlaceholderText = P("Search code or text…", "搜尋號碼或文字…");
            LookupLabel.Text = P("Look up a code", "查一個碼");
            ClearLookupBtn.Content = P("Show all", "顯示全部");
        }
        catch { /* never throw from UI text */ }
    }

    private void BuildCategories()
    {
        try
        {
            _suppress = true;
            int prev = CategoryBox.SelectedIndex;
            CategoryBox.Items.Clear();
            CategoryBox.Items.Add(P("All classes", "全部類別"));
            CategoryBox.Items.Add(P("1xx Informational", "1xx 資訊"));
            CategoryBox.Items.Add(P("2xx Success", "2xx 成功"));
            CategoryBox.Items.Add(P("3xx Redirect", "3xx 重新導向"));
            CategoryBox.Items.Add(P("4xx Client error", "4xx 客戶端錯誤"));
            CategoryBox.Items.Add(P("5xx Server error", "5xx 伺服器錯誤"));
            CategoryBox.SelectedIndex = (prev >= 0 && prev <= 5) ? prev : 0;
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    // 0 = all, else 1..5
    private int SelectedCategory => CategoryBox.SelectedIndex >= 1 && CategoryBox.SelectedIndex <= 5 ? CategoryBox.SelectedIndex : 0;

    private static Color CategoryColor(int category) => category switch
    {
        1 => Color.FromArgb(255, 0x6B, 0x7B, 0x8C),   // 1xx grey-blue
        2 => Color.FromArgb(255, 0x2E, 0x8B, 0x57),   // 2xx green
        3 => Color.FromArgb(255, 0x2B, 0x6C, 0xB0),   // 3xx blue
        4 => Color.FromArgb(255, 0xC7, 0x7D, 0x0A),   // 4xx amber
        5 => Color.FromArgb(255, 0xC0, 0x39, 0x2B),   // 5xx red
        _ => Colors.Gray,
    };

    private void Refresh()
    {
        try
        {
            string q = SearchBox?.Text ?? string.Empty;
            var results = HttpStatusService.Filter(q, SelectedCategory);

            _rows.Clear();
            foreach (var s in results)
            {
                _rows.Add(new Row
                {
                    Code = s.Code,
                    CodeText = s.Code.ToString(),
                    Name = s.Name,
                    Desc = P(s.DescEn, s.DescZh),
                    Accent = new SolidColorBrush(CategoryColor(s.Category)),
                });
            }

            CodesList.ItemsSource = null;
            CodesList.ItemsSource = _rows;

            StatusText.Text = _rows.Count == 0
                ? P("No codes match your filter.", "冇符合條件嘅狀態碼。")
                : P($"Showing {_rows.Count} code(s). Click a row to copy \"code name\".",
                    $"顯示緊 {_rows.Count} 個碼。撳一行複製「碼 名稱」。");
        }
        catch
        {
            try { StatusText.Text = P("Could not build the list.", "建立清單時出咗問題。"); } catch { }
        }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Refresh();
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Refresh();
    }

    private void Lookup_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        try
        {
            double v = args.NewValue;
            if (double.IsNaN(v) || v <= 0) return;
            int code = (int)v;
            var hit = HttpStatusService.Lookup(code);
            if (hit == null)
            {
                StatusText.Text = P($"No status code {code} in the catalogue.", $"目錄裏面冇 {code} 呢個狀態碼。");
                return;
            }

            // Show just that code: clear filters and search the exact number.
            _suppress = true;
            SearchBox.Text = code.ToString();
            CategoryBox.SelectedIndex = 0;
            _suppress = false;
            Refresh();
        }
        catch
        {
            try { StatusText.Text = P("Look-up failed.", "查碼失敗。"); } catch { }
        }
    }

    private void ClearLookup_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _suppress = true;
            LookupBox.Value = 0;
            SearchBox.Text = string.Empty;
            CategoryBox.SelectedIndex = 0;
            _suppress = false;
            Refresh();
        }
        catch { _suppress = false; }
    }

    private void Codes_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is not Row r) return;
            string text = $"{r.CodeText} {r.Name}".Trim();
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P($"Copied \"{text}\".", $"已複製「{text}」。");
        }
        catch
        {
            try { StatusText.Text = P("Copy failed.", "複製失敗。"); } catch { }
        }
    }
}
