using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 字串跳脫（多語言）· String Escaper — escape / unescape text for many target syntaxes
/// (JSON, C#, JS, Java, Python, XML/HTML, URL, shell single-quote, regex, CSV, SQL). Pure managed,
/// bilingual, and never throws — malformed unescape input surfaces as a status message. No redirect.
/// </summary>
public sealed partial class TextEscapeModule : Page
{
    // Kept in sync with the ComboBox order below.
    private static readonly TextEscapeService.Lang[] Langs =
    {
        TextEscapeService.Lang.Json,
        TextEscapeService.Lang.CSharp,
        TextEscapeService.Lang.JavaScript,
        TextEscapeService.Lang.Java,
        TextEscapeService.Lang.Python,
        TextEscapeService.Lang.Html,
        TextEscapeService.Lang.Url,
        TextEscapeService.Lang.Shell,
        TextEscapeService.Lang.Regex,
        TextEscapeService.Lang.Csv,
        TextEscapeService.Lang.Sql,
    };

    public TextEscapeModule()
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
        try
        {
            Header.Title = "String Escaper · 字串跳脫";
            HeaderBlurb.Text = P("Escape or unescape text for a chosen syntax — JSON, C#, JavaScript, Java, Python, XML/HTML, URL, shell, regex, CSV or SQL. Everything runs locally.",
                "揀一種語法嚟做跳脫或者還原 — JSON、C#、JavaScript、Java、Python、XML/HTML、網址、Shell、正則、CSV 或者 SQL。全部喺本機執行。");
            LangLabel.Text = P("Target syntax", "目標語法");
            InputLabel.Text = P("Input", "輸入");
            OutputLabel.Text = P("Output", "輸出");
            EscapeBtn.Content = P("Escape", "跳脫");
            UnescapeBtn.Content = P("Unescape", "還原");
            CopyBtn.Content = P("Copy output", "複製輸出");
            ClearBtn.Content = P("Clear", "清除");

            RebuildLangBox();
            if (string.IsNullOrEmpty(StatusText.Text))
                StatusText.Text = P("Ready.", "準備好。");
        }
        catch
        {
            // Rendering must never crash the page.
        }
    }

    private void RebuildLangBox()
    {
        int keep = LangBox.SelectedIndex;
        var items = new List<string>
        {
            P("JSON string", "JSON 字串"),
            P("C# string", "C# 字串"),
            P("JavaScript", "JavaScript"),
            P("Java", "Java"),
            P("Python", "Python"),
            P("XML / HTML", "XML / HTML"),
            P("URL", "網址 URL"),
            P("Shell (single-quote)", "Shell（單引號）"),
            P("Regex", "正則表達式"),
            P("CSV field", "CSV 欄位"),
            P("SQL string", "SQL 字串"),
        };
        LangBox.ItemsSource = items;
        LangBox.SelectedIndex = keep >= 0 && keep < items.Count ? keep : 0;
    }

    private TextEscapeService.Lang CurrentLang()
    {
        int i = LangBox.SelectedIndex;
        return i >= 0 && i < Langs.Length ? Langs[i] : TextEscapeService.Lang.Json;
    }

    private void Lang_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Selecting a new syntax just resets the status; leave the boxes as-is.
        StatusText.Text = P("Ready.", "準備好。");
    }

    private void Escape_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = TextEscapeService.Escape(CurrentLang(), InputBox.Text ?? string.Empty);
            OutputBox.Text = r.Text;
            StatusText.Text = r.Ok
                ? P("Escaped.", "已跳脫。")
                : P("Could not escape that input.", "無法跳脫呢段輸入。");
        }
        catch
        {
            StatusText.Text = P("Something went wrong.", "出咗啲問題。");
        }
    }

    private void Unescape_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var r = TextEscapeService.Unescape(CurrentLang(), InputBox.Text ?? string.Empty);
            if (r.Ok)
            {
                OutputBox.Text = r.Text;
                StatusText.Text = P("Unescaped.", "已還原。");
            }
            else
            {
                StatusText.Text = P("Malformed input for this syntax — nothing to unescape.",
                    "呢種語法嘅輸入格式唔啱 — 冇嘢可以還原。");
            }
        }
        catch
        {
            StatusText.Text = P("Something went wrong.", "出咗啲問題。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Output copied to clipboard.", "輸出已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Could not access the clipboard.", "無法存取剪貼簿。");
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text = string.Empty;
        OutputBox.Text = string.Empty;
        StatusText.Text = P("Cleared.", "已清除。");
    }
}
