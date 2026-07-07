using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 大小寫／命名轉換 · Case &amp; naming converter — type text, get every programming/writing case
/// (camelCase, PascalCase, snake_case, kebab-case, CONSTANT_CASE, Title/Sentence, dot./path/, Train-Case)
/// live, each copyable. Pure managed, robust, bilingual (粵語). No redirect.
/// </summary>
public sealed partial class CaseConvertModule : Page
{
    /// <summary>Row view-model bound in the ItemsControl (classic {Binding}).</summary>
    public sealed class Row
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string CopyLabel { get; set; } = "Copy";
    }

    public CaseConvertModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
        => Loc.I.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Case Converter · 大小寫轉換";
            HeaderBlurb.Text = P(
                "Type anything and get every naming convention — camelCase, PascalCase, snake_case, kebab-case, CONSTANT_CASE, Title, Sentence, dot, path and Train-Case. Copy any form with one click.",
                "打乜都得，即刻幫你轉晒所有命名格式 — camelCase、PascalCase、snake_case、kebab-case、CONSTANT_CASE、標題、句子、點、路徑同 Train-Case。撳一下就複製到。");
            InputLabel.Text = P("Input text", "輸入文字");
            OutputLabel.Text = P("Converted forms", "轉換結果");
            RebuildRows();
        }
        catch
        {
            // Never let a render throw take down the page.
        }
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => RebuildRows();

    private void RebuildRows()
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            string copy = P("Copy", "複製");
            var rows = new List<Row>();
            foreach (var (en, zh, value) in CaseConvertService.AllForms(input))
                rows.Add(new Row { Label = P(en, zh), Value = value, CopyLabel = copy });

            if (RowsHost != null) RowsHost.ItemsSource = rows;

            int words = CaseConvertService.Tokenize(input).Count;
            if (StatusText != null)
                StatusText.Text = string.IsNullOrWhiteSpace(input)
                    ? P("Type above to see conversions.", "喺上面打字就會出結果。")
                    : P($"{words} word(s) detected.", $"偵測到 {words} 個字。");
        }
        catch (Exception ex)
        {
            if (StatusText != null)
                StatusText.Text = P("Could not convert this input.", "呢段輸入轉換唔到。") + " " + ex.Message;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = (sender as Button)?.Tag as string ?? string.Empty;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            if (StatusText != null)
                StatusText.Text = string.IsNullOrEmpty(text)
                    ? P("Nothing to copy.", "冇嘢可以複製。")
                    : P("Copied to clipboard.", "已經複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            if (StatusText != null)
                StatusText.Text = P("Copy failed.", "複製失敗。") + " " + ex.Message;
        }
    }
}
