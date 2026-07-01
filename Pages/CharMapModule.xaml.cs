using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 字元地圖 · Unicode character explorer. Pick a Unicode block to fill a grid,
/// search by codepoint ("U+2764", "2764", "#10084") or by name substring, and
/// select a glyph to copy it and see its Code / decimal / UTF-8 / UTF-16 / HTML
/// entity encodings. Pure-managed, bilingual, never throws. No redirect.
/// </summary>
public sealed partial class CharMapModule : Page
{
    private List<CharMapService.CharInfo> _all = new();      // current block, unfiltered
    private bool _suppress;

    public CharMapModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { BuildBlockCombo(); Render(); LoadSelectedBlock(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Character Map · 字元地圖";
        HeaderBlurb.Text = P("Browse Unicode blocks, search by codepoint or category, and click a character to copy it — with its code, decimal, UTF-8, UTF-16 and HTML entity.",
            "瀏覽 Unicode 區塊，用編碼點或者類別搜尋，撳一下就複製個字元 — 連埋佢嘅編碼、十進制、UTF-8、UTF-16 同 HTML 實體。");
        BlockLabel.Text = P("Unicode block", "Unicode 區塊");
        SearchLabel.Text = P("Search", "搜尋");
        SearchHint.Text = P("Type U+2764, 2764, or #10084 to jump to a codepoint, or type text to filter by category.",
            "輸入 U+2764、2764 或者 #10084 跳去某個編碼點，或者打字按類別篩選。");
        RefreshBlockComboLabels();
    }

    private void BuildBlockCombo()
    {
        _suppress = true;
        BlockCombo.Items.Clear();
        foreach (var b in CharMapService.Blocks)
            BlockCombo.Items.Add(new ComboBoxItem { Content = BlockLabelFor(b), Tag = b });
        if (BlockCombo.Items.Count > 0) BlockCombo.SelectedIndex = 0;
        _suppress = false;
    }

    private void RefreshBlockComboLabels()
    {
        foreach (var obj in BlockCombo.Items)
            if (obj is ComboBoxItem item && item.Tag is CharMapService.Block b)
                item.Content = BlockLabelFor(b);
    }

    private string BlockLabelFor(CharMapService.Block b) =>
        $"{P(b.En, b.Zh)}  ({"U+" + b.Start.ToString(b.Start <= 0xFFFF ? "X4" : "X6") + "–U+" + b.End.ToString(b.End <= 0xFFFF ? "X4" : "X6")})";

    private void Block_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        LoadSelectedBlock();
    }

    private void LoadSelectedBlock()
    {
        try
        {
            if (BlockCombo.SelectedItem is ComboBoxItem item && item.Tag is CharMapService.Block b)
            {
                _all = CharMapService.BuildRange(b.Start, b.End);
                ApplyFilter(SearchBox.Text);
            }
        }
        catch { /* never throw */ }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        ApplyFilter(SearchBox.Text);
    }

    private void ApplyFilter(string? query)
    {
        try
        {
            // Codepoint jump? (U+2764 / 2764 / 0x2764 / #10084)
            int cp = CharMapService.ParseCodePoint(query);
            if (cp >= 0)
            {
                var info = CharMapService.Describe(cp);
                var single = new List<CharMapService.CharInfo>();
                if (info != null) single.Add(info);
                SetSource(single);
                if (single.Count > 0)
                {
                    _suppress = true;
                    CharList.SelectedIndex = 0;
                    _suppress = false;
                    ShowDetails(single[0], copy: false);
                }
                return;
            }

            // Plain-text filter by name/category substring (case-insensitive).
            if (string.IsNullOrWhiteSpace(query))
            {
                SetSource(_all);
                return;
            }

            string q = query.Trim();
            var filtered = new List<CharMapService.CharInfo>();
            foreach (var ci in _all)
            {
                if (ci.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 && filtered.Count < CharMapService.MaxItems)
                    filtered.Add(ci);
            }
            SetSource(filtered);
        }
        catch { /* never throw */ }
    }

    private void SetSource(List<CharMapService.CharInfo> items)
    {
        _suppress = true;
        CharList.ItemsSource = items;
        _suppress = false;
    }

    private void Char_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (CharList.SelectedItem is CharMapService.CharInfo ci)
            ShowDetails(ci, copy: true);
    }

    private void ShowDetails(CharMapService.CharInfo ci, bool copy)
    {
        try
        {
            BigGlyph.Text = ci.Glyph;
            DetailName.Text = ci.Name;
            DetailCode.Text = "Code: " + ci.Code;
            DetailDec.Text = "Dec:  " + ci.Dec;
            DetailUtf8.Text = "UTF-8:  " + ci.Utf8;
            DetailUtf16.Text = "UTF-16: " + ci.Utf16;
            DetailHtml.Text = "HTML: " + ci.Html;
            DetailsCard.Visibility = Visibility.Visible;

            if (copy && Copy(ci.Glyph))
                CopiedText.Text = P($"Copied “{ci.Glyph}” to the clipboard.", $"已複製「{ci.Glyph}」到剪貼簿。");
            else
                CopiedText.Text = string.Empty;
        }
        catch { /* never throw */ }
    }

    private static bool Copy(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return false;
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            return true;
        }
        catch { return false; }
    }
}
