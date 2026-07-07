using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字編碼／換行轉換 · Text encoding &amp; line-ending converter. Load a text file (or paste), sniff the
/// source encoding by BOM and detect its line-endings, then re-encode to a chosen target encoding and
/// line-ending (with add/remove-BOM baked into the encoding choice) and Save/Copy the result. Bilingual,
/// fully guarded — nothing throws to the UI. No redirect.
/// </summary>
public sealed partial class EncodingConvModule : Page
{
    private bool _suppress;
    private EncodingConvService.EncKind _detectedEnc = EncodingConvService.EncKind.Unknown;
    private EncodingConvService.Eol _detectedEol = EncodingConvService.Eol.None;

    private static readonly EncodingConvService.EncKind[] EncOrder =
    {
        EncodingConvService.EncKind.Utf8,
        EncodingConvService.EncKind.Utf8Bom,
        EncodingConvService.EncKind.Utf16Le,
        EncodingConvService.EncKind.Utf16Be,
        EncodingConvService.EncKind.Ascii,
        EncodingConvService.EncKind.Latin1,
    };

    private static readonly EncodingConvService.Eol[] EolOrder =
    {
        EncodingConvService.Eol.Lf,
        EncodingConvService.Eol.CrLf,
        EncodingConvService.Eol.Cr,
    };

    public EncodingConvModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Encoding Converter · 編碼轉換";
        HeaderBlurb.Text = P("Load a text file or paste text, detect its encoding (by BOM) and line-endings, then convert to a target encoding and line-ending. Add or remove the BOM by picking the matching encoding.",
            "載入文字檔或者貼上文字，偵測佢嘅編碼（睇 BOM）同換行方式，然後轉去目標編碼同換行。想加／去 BOM 就揀對應嘅編碼。");
        LoadBtn.Content = P("Load file…", "載入檔案…");
        SourceLabel.Text = P("Source text", "來源文字");
        EncLabel.Text = P("Target encoding", "目標編碼");
        EolLabel.Text = P("Target line-ending", "目標換行");
        ConvertBtn.Content = P("Convert", "轉換");
        ResultLabel.Text = P("Result", "結果");
        SaveBtn.Content = P("Save…", "儲存…");
        CopyBtn.Content = P("Copy", "複製");

        _suppress = true;
        if (EncCombo.Items.Count == 0)
        {
            foreach (var k in EncOrder) EncCombo.Items.Add(EncodingConvService.Label(k));
            EncCombo.SelectedIndex = 0;
        }
        if (EolCombo.Items.Count == 0)
        {
            foreach (var eol in EolOrder) EolCombo.Items.Add(EncodingConvService.Label(eol));
            EolCombo.SelectedIndex = 1; // CRLF default on Windows
        }
        _suppress = false;

        UpdateDetectText();
        if (StatusText.Text.Length == 0)
            StatusText.Text = P("Ready.", "準備好。");
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var filters = new List<FileDialogs.Filter>
            {
                new("Text files", "*.txt;*.csv;*.log;*.json;*.xml;*.md;*.ini;*.cfg"),
                new("All files", "*.*"),
            };
            string? path = await FileDialogs.OpenFileAsync(filters, P("Choose a text file", "揀一個文字檔"));
            if (string.IsNullOrEmpty(path)) return;

            var r = await EncodingConvService.ReadFileAsync(path);
            if (!r.Ok)
            {
                StatusText.Text = P("Could not read the file: ", "讀唔到檔案：") + r.Message;
                return;
            }
            _suppress = true;
            SourceBox.Text = r.Text;
            _suppress = false;
            _detectedEnc = r.Encoding;
            _detectedEol = r.LineEnding;
            UpdateDetectText();
            StatusText.Text = r.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Load failed: ", "載入失敗：") + ex.Message;
        }
    }

    private void Source_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        // Typed/pasted text — encoding is unknown; detect line-endings live.
        _detectedEnc = EncodingConvService.EncKind.Unknown;
        _detectedEol = EncodingConvService.DetectEol(SourceBox.Text);
        UpdateDetectText();
    }

    private void Options_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        UpdateDetectText();
    }

    private void Convert_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var eol = SelectedEol();
            string converted = EncodingConvService.ConvertLineEndings(SourceBox.Text ?? string.Empty, eol);
            ResultBox.Text = converted;

            var enc = SelectedEnc();
            byte[] bytes = EncodingConvService.Encode(converted, enc);
            StatusText.Text = P($"Converted → {EncodingConvService.Label(enc)}, {EncodingConvService.Label(eol)} ({bytes.Length} bytes).",
                $"已轉換 → {EncodingConvService.Label(enc)}、{EncodingConvService.Label(eol)}（{bytes.Length} 位元組）。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Convert failed: ", "轉換失敗：") + ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(ResultBox.Text))
            {
                StatusText.Text = P("Nothing to save — convert first.", "冇嘢可以儲存 — 請先轉換。");
                return;
            }
            string? path = await FileDialogs.SaveFileAsync(P("converted.txt", "converted.txt"),
                FileDialogs.BuildFilters(new[] { ".txt" }), "txt", P("Save converted text", "儲存已轉換文字"));
            if (string.IsNullOrEmpty(path)) return;

            var r = await EncodingConvService.SaveFileAsync(path, ResultBox.Text, SelectedEnc());
            StatusText.Text = r.Ok ? r.Message : P("Save failed: ", "儲存失敗：") + r.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Save failed: ", "儲存失敗：") + ex.Message;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(ResultBox.Text))
            {
                StatusText.Text = P("Nothing to copy — convert first.", "冇嘢可以複製 — 請先轉換。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(ResultBox.Text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }

    private EncodingConvService.EncKind SelectedEnc()
    {
        int i = EncCombo.SelectedIndex;
        return (i >= 0 && i < EncOrder.Length) ? EncOrder[i] : EncodingConvService.EncKind.Utf8;
    }

    private EncodingConvService.Eol SelectedEol()
    {
        int i = EolCombo.SelectedIndex;
        return (i >= 0 && i < EolOrder.Length) ? EolOrder[i] : EncodingConvService.Eol.Lf;
    }

    private void UpdateDetectText()
    {
        string encName = _detectedEnc == EncodingConvService.EncKind.Unknown
            ? P("unknown", "未知")
            : EncodingConvService.Label(_detectedEnc);
        string eolName = EncodingConvService.Label(_detectedEol);
        string tEnc = EncodingConvService.Label(SelectedEnc());
        string tEol = EncodingConvService.Label(SelectedEol());
        DetectText.Text = P($"Detected: {encName}, {eolName}  →  target: {tEnc}, {tEol}",
            $"偵測到：{encName}、{eolName}  →  目標：{tEnc}、{tEol}");
    }
}
