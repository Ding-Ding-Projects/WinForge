using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// ASCII 表參考 · ASCII / Latin-1 reference table. Rows for codes 0–127 (toggle to extend to
/// 255): decimal, hex, octal, 8-bit binary, the printable glyph or a control-code mnemonic
/// (NUL/LF/CR/ESC/DEL…) with a bilingual description. Search filters by dec/hex/char/name; a
/// row click copies the character (or its code for non-printable rows). Fully bilingual, never
/// throws. No redirect.
/// </summary>
public sealed partial class AsciiTableModule : Page
{
    private List<AsciiTableService.AsciiRow> _all = new();
    private bool _suppress;

    public AsciiTableModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Rebuild();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        Rebuild();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("ASCII Table", "ASCII 表");
            HeaderBlurb.Text = P(
                "A quick reference for character codes 0–127 — decimal, hex, octal, 8-bit binary, the glyph (or control-code mnemonic) and a short description. Search by number, hex or name; click a row to copy the character. Toggle Latin-1 to see 128–255.",
                "字元碼 0–127 嘅快速參考 — 十進、十六進、八進、8 位二進、字元（或控制碼縮寫）同簡短說明。可以用數字、十六進或名稱搜尋；撳一行就複製個字元。㩒 Latin-1 睇埋 128–255。");
            if (SearchBox != null)
                SearchBox.PlaceholderText = P("Search dec / hex / char / name…", "搜尋 十進 / 十六進 / 字元 / 名稱…");
            if (Latin1Chk != null)
                Latin1Chk.Content = P("Include 128–255 (Latin-1)", "包含 128–255（Latin-1）");
            ColDec.Text = P("Dec", "十進");
            ColHex.Text = P("Hex", "十六進");
            ColOct.Text = P("Oct", "八進");
            ColBin.Text = P("Binary", "二進");
            ColChar.Text = P("Char", "字元");
            ColName.Text = P("Name / Description", "名稱 / 說明");
        }
        catch { /* never throw from UI text */ }
    }

    private void Rebuild()
    {
        try
        {
            bool latin1 = Latin1Chk?.IsChecked == true;
            _all = AsciiTableService.Build(latin1, P);
            ApplyFilter();
        }
        catch
        {
            SetStatus(P("Could not build the table.", "無法建立表格。"));
        }
    }

    private void ApplyFilter()
    {
        try
        {
            var rows = AsciiTableService.Filter(_all, SearchBox?.Text);
            _suppress = true;
            RowsList.ItemsSource = rows;
            _suppress = false;
            SetStatus(P($"{rows.Count} of {_all.Count} rows", $"{rows.Count} / {_all.Count} 行"));
        }
        catch
        {
            _suppress = false;
            SetStatus(P("Filter failed.", "篩選失敗。"));
        }
    }

    private void SetStatus(string text)
    {
        try { if (StatusText != null) StatusText.Text = text; } catch { }
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void Latin1_Changed(object sender, RoutedEventArgs e) => Rebuild();

    private void Rows_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            if (RowsList.SelectedItem is not AsciiTableService.AsciiRow row) return;
            string toCopy = string.IsNullOrEmpty(row.CopyChar) ? row.Dec : row.CopyChar;
            string label;
            if (string.IsNullOrEmpty(row.CopyChar))
            {
                label = P($"Copied code {row.Dec} ({row.Hex})", $"已複製代碼 {row.Dec}（{row.Hex}）");
            }
            else if (row.Code <= 32 || row.Code == 127 || (row.Code >= 128 && row.Code <= 160))
            {
                // Control / space / C1 — copying the raw char is invisible, so tell the user what it was.
                label = P($"Copied {row.Char} (code {row.Dec})", $"已複製 {row.Char}（代碼 {row.Dec}）");
            }
            else
            {
                label = P($"Copied \"{row.Char}\" (code {row.Dec})", $"已複製「{row.Char}」（代碼 {row.Dec}）");
            }

            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(toCopy);
            Clipboard.SetContent(pkg);
            SetStatus(label);
        }
        catch
        {
            SetStatus(P("Copy failed.", "複製失敗。"));
        }
    }
}
