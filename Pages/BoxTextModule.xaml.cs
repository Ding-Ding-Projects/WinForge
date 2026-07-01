using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字方框 / 橫幅 · Box &amp; banner text — wrap input text in a drawn border box (ASCII, Single,
/// Double, Rounded, Heavy, Stars, comment blocks), with padding / alignment / optional title.
/// Monospaced output, one-click copy. Bilingual (粵語). Robust — never throws.
/// </summary>
public sealed partial class BoxTextModule : Page
{
    private bool _suppress;

    public BoxTextModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); RenderOutput(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        // Preserve selections across a relabel of the combo items.
        int style = StyleBox.SelectedIndex;
        int align = AlignBox.SelectedIndex;
        Render();
        _suppress = true;
        if (style >= 0 && style < StyleBox.Items.Count) StyleBox.SelectedIndex = style;
        if (align >= 0 && align < AlignBox.Items.Count) AlignBox.SelectedIndex = align;
        _suppress = false;
        RenderOutput();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Box & Banner Text · 文字方框";
            HeaderBlurb.Text = P("Wrap any text in a drawn box or banner — pick a border style, padding, alignment and an optional title. Great for README headers, ASCII banners and code comments.",
                "將任何文字包成一個方框或者橫幅 — 揀邊框樣式、內距、對齊同可選標題。整 README 標題、ASCII 橫幅同程式註解都啱。");

            InputLabel.Text = P("Text to box", "要入框嘅文字");
            StyleLabel.Text = P("Border style", "邊框樣式");
            AlignLabel.Text = P("Alignment", "對齊");
            PaddingLabel.Text = P("Horizontal padding", "水平內距");
            TitleLabel.Text = P("Title (optional)", "標題（可選）");
            OutputLabel.Text = P("Result", "結果");
            CopyBtn.Content = P("Copy", "複製");

            _suppress = true;

            int prevStyle = StyleBox.SelectedIndex;
            StyleBox.Items.Clear();
            StyleBox.Items.Add(P("ASCII ( + - | )", "ASCII（+ - |）"));
            StyleBox.Items.Add(P("Single ( ─ │ )", "單線（─ │）"));
            StyleBox.Items.Add(P("Double ( ═ ║ )", "雙線（═ ║）"));
            StyleBox.Items.Add(P("Rounded ( ╭ ╮ )", "圓角（╭ ╮）"));
            StyleBox.Items.Add(P("Heavy ( ━ ┃ )", "粗線（━ ┃）"));
            StyleBox.Items.Add(P("Stars ( * )", "星號（*）"));
            StyleBox.Items.Add(P("Comment /* … */", "註解 /* … */"));
            StyleBox.Items.Add(P("Comment ### … ###", "註解 ### … ###"));
            StyleBox.SelectedIndex = prevStyle >= 0 ? prevStyle : 0;

            int prevAlign = AlignBox.SelectedIndex;
            AlignBox.Items.Clear();
            AlignBox.Items.Add(P("Left", "靠左"));
            AlignBox.Items.Add(P("Center", "置中"));
            AlignBox.Items.Add(P("Right", "靠右"));
            AlignBox.SelectedIndex = prevAlign >= 0 ? prevAlign : 0;

            _suppress = false;
        }
        catch
        {
            _suppress = false;
        }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => RenderOutput();
    private void Options_Changed(object sender, SelectionChangedEventArgs e) { if (!_suppress) RenderOutput(); }
    private void Padding_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppress) RenderOutput(); }

    private void RenderOutput()
    {
        try
        {
            if (OutputBox is null) return;

            var style = (BoxTextService.BorderStyle)Math.Max(0, StyleBox.SelectedIndex);
            var align = (BoxTextService.Align)Math.Max(0, AlignBox.SelectedIndex);
            int padding = (int)(double.IsNaN(PaddingBox.Value) ? 0 : PaddingBox.Value);
            string title = TitleBox.Text ?? string.Empty;
            string input = InputBox.Text ?? string.Empty;

            string result = BoxTextService.Render(input, style, padding, align, title);
            OutputBox.Text = result;

            int lines = string.IsNullOrEmpty(result) ? 0 : result.Split('\n').Length;
            StatusText.Text = P($"{result.Length} chars · {lines} lines", $"{result.Length} 個字元 · {lines} 行");
        }
        catch
        {
            StatusText.Text = P("Could not render — check your input.", "無法產生 — 請檢查輸入。");
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Copy failed.", "複製失敗。");
        }
    }
}
