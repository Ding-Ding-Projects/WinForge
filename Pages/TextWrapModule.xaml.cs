using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字換行／重排 · Text wrap / reflow module — hard-wrap, unwrap, reflow, prefix and hanging-indent
/// a block of text at a chosen column. Pure-managed transforms via <see cref="TextWrapService"/>.
/// Bilingual, never-throwing, clipboard via <see cref="DataPackage"/>. No redirect.
/// </summary>
public sealed partial class TextWrapModule : Page
{
    public TextWrapModule()
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

    private int Width()
    {
        double v = WidthBox?.Value ?? 72;
        if (double.IsNaN(v) || v < 1) v = 72;
        return (int)v;
    }

    private int Indent()
    {
        double v = IndentBox?.Value ?? 4;
        if (double.IsNaN(v) || v < 0) v = 0;
        return (int)v;
    }

    private void Render()
    {
        Header.Title = "Text Wrap · 文字換行";
        HeaderBlurb.Text = P("Rewrap, unwrap or reflow plain text to a fixed column width — handy for code comments, commit messages, emails and README files. Blank lines keep paragraphs apart.",
            "將純文字重新換行、拉直或者重排到固定闊度 — 寫程式註解、commit 訊息、電郵同 README 好用。空白行會分開段落。");
        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");
        WidthLabel.Text = P("Width (columns)", "闊度（字元）");
        BreakLongChk.Content = P("Break words longer than the width", "斬開超過闊度嘅長字");
        PrefixLabel.Text = P("Prefix", "前綴");
        IndentLabel.Text = P("Indent spaces", "縮排空格");
        WrapBtn.Content = P("Hard-wrap", "硬換行");
        UnwrapBtn.Content = P("Unwrap", "拉直");
        ReflowBtn.Content = P("Reflow", "重排");
        PrefixBtn.Content = P("Add prefix", "加前綴");
        IndentBtn.Content = P("Hanging indent", "懸掛縮排");
        CopyBtn.Content = P("Copy output", "複製輸出");
        UpdateReadout();
        if (StatusText.Text.Length == 0)
            StatusText.Text = P("Ready.", "準備就緒。");
    }

    private void UpdateReadout()
    {
        try
        {
            string src = OutputBox.Text.Length > 0 ? OutputBox.Text : InputBox.Text;
            var lines = (src ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int longest = lines.Length == 0 ? 0 : lines.Max(l => l.Length);
            int chars = (src ?? string.Empty).Length;
            ReadoutText.Text = P($"Target {Width()} cols · longest line {longest} · {lines.Length} lines · {chars} chars",
                $"目標 {Width()} 字元 · 最長一行 {longest} · {lines.Length} 行 · {chars} 個字元");
        }
        catch { /* readout is best-effort */ }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateReadout();

    private void Width_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateReadout();

    private void SetOutput(string text, string okEn, string okZh)
    {
        OutputBox.Text = text ?? string.Empty;
        StatusText.Text = P(okEn, okZh);
        UpdateReadout();
    }

    private void Wrap_Click(object sender, RoutedEventArgs e)
    {
        try { SetOutput(TextWrapService.HardWrap(InputBox.Text, Width(), BreakLongChk.IsChecked == true), "Hard-wrapped.", "已硬換行。"); }
        catch { StatusText.Text = P("Could not wrap the text.", "換行失敗。"); }
    }

    private void Unwrap_Click(object sender, RoutedEventArgs e)
    {
        try { SetOutput(TextWrapService.Unwrap(InputBox.Text), "Unwrapped.", "已拉直。"); }
        catch { StatusText.Text = P("Could not unwrap the text.", "拉直失敗。"); }
    }

    private void Reflow_Click(object sender, RoutedEventArgs e)
    {
        try { SetOutput(TextWrapService.Reflow(InputBox.Text, Width(), BreakLongChk.IsChecked == true), "Reflowed.", "已重排。"); }
        catch { StatusText.Text = P("Could not reflow the text.", "重排失敗。"); }
    }

    private void Prefix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string basis = OutputBox.Text.Length > 0 ? OutputBox.Text : InputBox.Text;
            SetOutput(TextWrapService.AddPrefix(basis, PrefixBox.Text), "Prefix added to each line.", "已為每行加前綴。");
        }
        catch { StatusText.Text = P("Could not add the prefix.", "加前綴失敗。"); }
    }

    private void Indent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string basis = OutputBox.Text.Length > 0 ? OutputBox.Text : InputBox.Text;
            SetOutput(TextWrapService.HangingIndent(basis, Indent()), "Hanging indent applied.", "已套用懸掛縮排。");
        }
        catch { StatusText.Text = P("Could not indent the text.", "縮排失敗。"); }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0) { StatusText.Text = P("Nothing to copy.", "冇嘢可以複製。"); return; }
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch { StatusText.Text = P("Copy failed.", "複製失敗。"); }
    }
}
