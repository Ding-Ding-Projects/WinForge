using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON → 型別 · Paste a JSON sample, name a root type, pick TypeScript interface or C# class,
/// and get generated type definitions (nested objects become their own named types, arrays infer
/// their element type). Pure managed (System.Text.Json). Robust: never throws — bilingual status.
/// </summary>
public sealed partial class JsonToTsModule : Page
{
    private bool _ready;

    public JsonToTsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (LangBox.SelectedIndex < 0) LangBox.SelectedIndex = 0;
        _ready = true;
        Render();
        Generate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON to Types · JSON 轉型別";
        HeaderBlurb.Text = P("Paste a JSON sample and get ready-to-use type definitions. Nested objects become their own named types; arrays infer their element type. Nothing leaves this machine.",
            "貼一段 JSON 樣本，即刻攞到可以直接用嘅型別定義。巢狀物件會各自變成一個具名型別，陣列會自動推斷元素型別。所有嘢都留喺你部機。");
        InputLabel.Text = P("JSON sample", "JSON 樣本");
        RootLabel.Text = P("Root type name", "根型別名稱");
        LangLabel.Text = P("Output language", "輸出語言");
        OutputLabel.Text = P("Generated types", "生成嘅型別");
        CopyButton.Content = P("Copy", "複製");
        if (string.IsNullOrEmpty(JsonBox.Text))
            StatusText.Text = P("Waiting for a JSON sample…", "等緊 JSON 樣本…");
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        Generate();
    }

    private void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        Generate();
    }

    private void Generate()
    {
        try
        {
            var lang = LangBox.SelectedIndex == 1 ? JsonToTsService.Lang.CSharp : JsonToTsService.Lang.TypeScript;
            var result = JsonToTsService.Generate(JsonBox.Text, RootBox.Text, lang);
            if (result.Ok)
            {
                OutputBox.Text = result.Code;
                StatusText.Text = P($"OK — {result.TypeCount} type(s) generated.", $"完成 — 生成咗 {result.TypeCount} 個型別。");
            }
            else
            {
                OutputBox.Text = "";
                StatusText.Text = P(result.ErrorEn ?? "Could not generate types.", result.ErrorZh ?? "生成唔到型別。");
            }
        }
        catch (Exception ex)
        {
            OutputBox.Text = "";
            StatusText.Text = P("Unexpected error: " + ex.Message, "非預期錯誤：" + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? "";
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "而家冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已經複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: " + ex.Message, "複製失敗：" + ex.Message);
        }
    }
}
