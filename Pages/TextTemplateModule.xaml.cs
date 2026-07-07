using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 模板渲染器 · Template Renderer — write a template with {{placeholder}} / nested {{a.b.c}} tokens
/// and {{#if key}}…{{/if}} blocks, feed it data (JSON object OR key=value lines), and get a live
/// rendered result you can copy. Pure managed, never throws, bilingual. No redirect.
/// </summary>
public sealed partial class TextTemplateModule : Page
{
    public TextTemplateModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TemplateBox.Text) && string.IsNullOrEmpty(DataBox.Text))
        {
            TemplateBox.Text = "Hello {{name}}!\n{{#if vip}}Thanks for being a VIP.{{/if}}\nAccount: {{account.id}}";
            DataBox.Text = "{\n  \"name\": \"Sam\",\n  \"vip\": true,\n  \"account\": { \"id\": \"A-42\" }\n}";
        }
        Render();
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
        try
        {
            Header.Title = P("Template Renderer · 模板渲染器", "模板渲染器 · Template Renderer");
            HeaderBlurb.Text = P(
                "Fill a template with your data. Use {{name}} or nested {{a.b.c}} placeholders and optional {{#if key}}…{{/if}} blocks. Everything renders live as you type.",
                "用你嘅資料填入模板。用 {{name}} 或者巢狀 {{a.b.c}} 佔位符，仲可以用 {{#if key}}…{{/if}} 區塊。你一邊打字，佢就即時渲染。");
            TemplateLabel.Text = P("Template", "模板");
            DataLabel.Text = P("Data", "資料");
            DataHint.Text = P("Paste a JSON object, or write one key=value per line.", "貼上一個 JSON 物件，或者每行寫一個 key=value。");
            PassthroughChk.Content = P("Keep {{unknown}} placeholders instead of blanking them", "保留搵唔到嘅 {{佔位符}}，唔好清空");
            OutputLabel.Text = P("Rendered output", "渲染結果");
            CopyButton.Content = P("Copy", "複製");
            RenderOutput();
        }
        catch
        {
            // Never let a UI refresh throw.
        }
    }

    private void Input_Changed(object sender, RoutedEventArgs e) => RenderOutput();

    private void Input_Changed(object sender, TextChangedEventArgs e) => RenderOutput();

    private void RenderOutput()
    {
        try
        {
            var res = TextTemplateService.Render(TemplateBox.Text, DataBox.Text, PassthroughChk.IsChecked == true);
            OutputBox.Text = res.Output;

            if (!res.Ok)
            {
                StatusText.Text = P(res.ErrorEn ?? "Could not render.", res.ErrorZh ?? "渲染唔到。");
            }
            else if (res.Missing > 0)
            {
                StatusText.Text = P(
                    $"Rendered — {res.Substituted} filled, {res.Missing} placeholder(s) not found in data.",
                    $"已渲染 — 填咗 {res.Substituted} 個，有 {res.Missing} 個佔位符喺資料入面搵唔到。");
            }
            else
            {
                StatusText.Text = P($"Rendered — {res.Substituted} placeholder(s) filled.", $"已渲染 — 填咗 {res.Substituted} 個佔位符。");
            }
        }
        catch
        {
            StatusText.Text = P("Could not render.", "渲染唔到。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(OutputBox.Text ?? "");
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "複製唔到去剪貼簿。");
        }
    }
}
