using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// SQL 格式化 · SQL Formatter / beautifier. Paste messy SQL, get a tidy, one-clause-per-line
/// layout with each SELECT column on its own line; optional keyword upper-casing and indent
/// size; plus a Minify button and clipboard copy. Own tokenizer (SqlFormatService); no external
/// process or dependency. Bilingual (English + 粵語). Robust — never throws.
/// </summary>
public sealed partial class SqlFormatModule : Page
{
    private const string SampleSql =
        "select u.id, u.name, count(o.id) as orders /* per user */ from users u " +
        "left join orders o on o.user_id=u.id where u.active=1 and u.name like 'a%' " +
        "group by u.id, u.name having count(o.id)>0 order by orders desc limit 10;";

    public SqlFormatModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "SQL Formatter · SQL 格式化";
            HeaderBlurb.Text = P(
                "Paste messy SQL and beautify it — one clause per line, one column per line, optional uppercase keywords. Or minify it back to a single line. Everything runs locally.",
                "貼低亂糟糟嘅 SQL，一撳就變靚 — 每個子句一行、每個欄位一行，仲可以將關鍵字轉大楷。又或者壓縮返做一行。全部喺本機處理。");
            UpperTitle.Text = P("Uppercase keywords", "關鍵字轉大楷");
            IndentLabel.Text = P("Indent spaces", "縮排空格");
            InputLabel.Text = P("Input SQL", "輸入 SQL");
            OutputLabel.Text = P("Output", "輸出");
            FormatBtn.Content = P("Format", "格式化");
            MinifyBtn.Content = P("Minify", "壓縮");
            SampleBtn.Content = P("Sample", "範例");
            ClearBtn.Content = P("Clear", "清除");
            CopyBtn.Content = P("Copy", "複製");
            FeaturesLabel.Text = P("What it handles", "支援乜嘢");

            FeaturesList.ItemsSource = new List<string>
            {
                P("'strings', \"identifiers\", `backticks`, [brackets]", "'字串'、\"識別碼\"、`引號`、[方括號]"),
                P("-- line comments and /* block comments */", "-- 單行註解 同埋 /* 區塊註解 */"),
                P("Clauses on their own line: SELECT/FROM/WHERE/JOIN/GROUP BY/ORDER BY…", "子句各自一行：SELECT/FROM/WHERE/JOIN/GROUP BY/ORDER BY…"),
                P("One column per line inside SELECT", "SELECT 入面每個欄位一行"),
            };

            UpdateStatus(P("Ready.", "準備就緒。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Render error: ", "介面錯誤：") + ex.Message);
        }
    }

    private (bool upper, int indent) ReadOptions()
    {
        bool upper = UpperSwitch.IsOn;
        double v = IndentBox.Value;
        int indent = double.IsNaN(v) ? 2 : (int)v;
        if (indent < 0) indent = 0;
        if (indent > 16) indent = 16;
        return (upper, indent);
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (upper, indent) = ReadOptions();
            string result = SqlFormatService.Format(InputBox.Text, upper, indent);
            OutputBox.Text = result;
            UpdateStatus(string.IsNullOrWhiteSpace(result)
                ? P("Nothing to format.", "冇嘢可以格式化。")
                : P("Formatted.", "已格式化。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Format failed: ", "格式化失敗：") + ex.Message);
        }
    }

    private void Minify_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string result = SqlFormatService.Minify(InputBox.Text);
            OutputBox.Text = result;
            UpdateStatus(string.IsNullOrWhiteSpace(result)
                ? P("Nothing to minify.", "冇嘢可以壓縮。")
                : P("Minified (comments dropped).", "已壓縮（去除註解）。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Minify failed: ", "壓縮失敗：") + ex.Message);
        }
    }

    private void Options_Changed(object sender, RoutedEventArgs e) => ReformatIfAny();

    private void Indent_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => ReformatIfAny();

    private void ReformatIfAny()
    {
        try
        {
            if (!IsLoaded) return;
            if (string.IsNullOrWhiteSpace(OutputBox?.Text)) return;
            if (string.IsNullOrWhiteSpace(InputBox?.Text)) return;
            var (upper, indent) = ReadOptions();
            OutputBox.Text = SqlFormatService.Format(InputBox.Text, upper, indent);
            UpdateStatus(P("Re-formatted with new options.", "已用新設定重新格式化。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Format failed: ", "格式化失敗：") + ex.Message);
        }
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InputBox.Text = SampleSql;
            var (upper, indent) = ReadOptions();
            OutputBox.Text = SqlFormatService.Format(SampleSql, upper, indent);
            UpdateStatus(P("Loaded a sample query.", "已載入範例查詢。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Sample failed: ", "範例失敗：") + ex.Message);
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            InputBox.Text = string.Empty;
            OutputBox.Text = string.Empty;
            UpdateStatus(P("Cleared.", "已清除。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Clear failed: ", "清除失敗：") + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                UpdateStatus(P("Nothing to copy.", "冇嘢可以複製。"));
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            UpdateStatus(P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            UpdateStatus(P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private void UpdateStatus(string text)
    {
        if (StatusText != null) StatusText.Text = text;
    }
}
