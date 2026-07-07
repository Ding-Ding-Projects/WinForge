using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;
using Windows.ApplicationModel.DataTransfer;
using WinColor = Windows.UI.Color;

namespace WinForge.Pages;

/// <summary>
/// JSON Schema 驗證器 · JSON Schema validator (draft-07 practical subset). Paste a schema and a
/// document, validate, and see each violation with its JSON-Pointer path in a coloured list.
/// Pure managed C# via <see cref="JsonSchemaService"/> — no NuGet, no process launch. Bilingual.
/// </summary>
public sealed partial class JsonSchemaModule : Page
{
    /// <summary>Row shown in the findings ListView. Classic {Binding} — no x:Bind in the DataTemplate.</summary>
    public sealed class Finding
    {
        public string Badge { get; set; } = "";
        public string Path { get; set; } = "";
        public string Message { get; set; } = "";
        public Brush BadgeBrush { get; set; } = new SolidColorBrush();
    }

    private static readonly WinColor PassColor = WinColor.FromArgb(0xFF, 0x2E, 0xA0, 0x43); // green
    private static readonly WinColor FailColor = WinColor.FromArgb(0xFF, 0xC4, 0x2B, 0x1C); // red
    private static readonly WinColor WarnColor = WinColor.FromArgb(0xFF, 0xB5, 0x8A, 0x00); // amber

    private readonly ObservableCollection<Finding> _findings = new();

    public JsonSchemaModule()
    {
        InitializeComponent();
        FindingsList.ItemsSource = _findings;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("JSON Schema Validator", "JSON 結構描述驗證器");
        HeaderBlurb.Text = P("Validate a JSON document against a draft-07 JSON Schema. Each violation shows its JSON-Pointer path and a plain message. Everything runs locally — nothing leaves your PC.",
            "用 draft-07 JSON 結構描述去驗證一份 JSON 文件。每個違規都會列出佢嘅 JSON-Pointer 路徑同埋淺白訊息。全部喺本機運行 — 冇任何嘢會離開你部電腦。");
        ValidateBtn.Content = P("Validate", "驗證");
        SampleBtn.Content = P("Load sample", "載入範例");
        ClearBtn.Content = P("Clear", "清除");
        CopyBtn.Content = P("Copy results", "複製結果");
        SchemaLabel.Text = P("JSON Schema (draft-07)", "JSON 結構描述（draft-07）");
        DocLabel.Text = P("JSON Document", "JSON 文件");
        FindingsLabel.Text = P("Findings", "結果");
        SchemaBox.PlaceholderText = P("Paste a JSON Schema here…", "喺呢度貼上 JSON 結構描述…");
        DocBox.PlaceholderText = P("Paste the JSON document to check…", "喺呢度貼上要檢查嘅 JSON 文件…");
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _findings.Clear();
            var result = JsonSchemaService.Validate(SchemaBox.Text ?? "", DocBox.Text ?? "", P);

            // Surface JSON-parse problems up top.
            if (!result.SchemaOk && result.SchemaError is not null)
                AddRow(P("SCHEMA", "描述"), "/", result.SchemaError, WarnColor);
            if (!result.DocumentOk && result.DocumentError is not null)
                AddRow(P("DOC", "文件"), "/", result.DocumentError, WarnColor);

            foreach (var v in result.Violations)
                AddRow(P("FAIL", "違規"), v.Path, v.Message, FailColor);

            VerdictBorder.Visibility = Visibility.Visible;
            if (!result.SchemaOk || !result.DocumentOk)
            {
                VerdictText.Text = P("⚠ COULD NOT VALIDATE", "⚠ 未能驗證");
                VerdictText.Foreground = new SolidColorBrush(WarnColor);
                VerdictSub.Text = P("Fix the JSON syntax highlighted below, then validate again.",
                    "先修正下面標示嘅 JSON 語法錯誤，再重新驗證。");
            }
            else if (result.Valid)
            {
                VerdictText.Text = P("✔ PASS — document is valid", "✔ 通過 — 文件有效");
                VerdictText.Foreground = new SolidColorBrush(PassColor);
                VerdictSub.Text = P("The document satisfies every constraint in the schema.",
                    "文件符合結構描述入面嘅每一項規則。");
                AddRow(P("PASS", "通過"), "/", P("No violations found.", "冇發現任何違規。"), PassColor);
            }
            else
            {
                VerdictText.Text = P($"✘ FAIL — {result.Violations.Count} violation(s)", $"✘ 失敗 — {result.Violations.Count} 項違規");
                VerdictText.Foreground = new SolidColorBrush(FailColor);
                VerdictSub.Text = P("The document breaks one or more schema constraints. See the list below.",
                    "文件違反咗一項或以上嘅結構描述規則。詳見下面列表。");
            }
        }
        catch (Exception ex)
        {
            // Never throw to the UI.
            VerdictBorder.Visibility = Visibility.Visible;
            VerdictText.Text = P("⚠ Unexpected error", "⚠ 發生意外錯誤");
            VerdictText.Foreground = new SolidColorBrush(WarnColor);
            VerdictSub.Text = ex.Message;
        }
    }

    private void AddRow(string badge, string path, string message, WinColor color)
        => _findings.Add(new Finding
        {
            Badge = badge,
            Path = path,
            Message = message,
            BadgeBrush = new SolidColorBrush(color),
        });

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (schema, doc) = JsonSchemaService.Sample();
            SchemaBox.Text = schema;
            DocBox.Text = doc;
        }
        catch { /* never throw */ }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        SchemaBox.Text = "";
        DocBox.Text = "";
        _findings.Clear();
        VerdictBorder.Visibility = Visibility.Collapsed;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(VerdictText.Text);
            foreach (var f in _findings)
                sb.AppendLine($"[{f.Badge}] {f.Path} — {f.Message}");

            var pkg = new DataPackage();
            pkg.SetText(sb.ToString());
            Clipboard.SetContent(pkg);
        }
        catch { /* never throw */ }
    }
}
