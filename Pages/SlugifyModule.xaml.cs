using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 網址別名產生器 · Slugify (URL slug generator) — turn any text into clean URL slugs.
/// Multiline input (one slug per line), separator/case/length options, diacritic stripping
/// and unicode-letter keeping, with live before→after preview and copy. Robust, never throws.
/// </summary>
public sealed partial class SlugifyModule : Page
{
    private bool _suppress;

    public SlugifyModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { BuildCombos(); Render(); Convert(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) { BuildCombos(); Render(); Convert(); }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        _suppress = true;
        try
        {
            int sep = SepBox.SelectedIndex < 0 ? 0 : SepBox.SelectedIndex;
            int cas = CaseBox.SelectedIndex < 0 ? 0 : CaseBox.SelectedIndex;

            SepBox.Items.Clear();
            SepBox.Items.Add(P("Hyphen ( - )", "連字號（ - ）"));
            SepBox.Items.Add(P("Underscore ( _ )", "底線（ _ ）"));
            SepBox.Items.Add(P("Dot ( . )", "點（ . ）"));
            SepBox.SelectedIndex = sep;

            CaseBox.Items.Clear();
            CaseBox.Items.Add(P("lowercase", "細楷"));
            CaseBox.Items.Add(P("UPPERCASE", "大楷"));
            CaseBox.Items.Add(P("Keep as-is", "維持原樣"));
            CaseBox.SelectedIndex = cas;
        }
        catch { }
        _suppress = false;
    }

    private void Render()
    {
        try
        {
            Header.Title = "Slugify · 網址別名";
            HeaderBlurb.Text = P("Turn any text into clean URL slugs — kebab-case permalinks for blogs, files or SEO. Paste multiple lines; each line becomes its own slug.",
                "將任何文字變成乾淨嘅網址別名 — 部落格、檔案或者 SEO 用嘅連字號永久連結。貼多行文字，每行變一個別名。");
            InputLabel.Text = P("Text (one slug per line)", "文字（每行一個別名）");
            SepLabel.Text = P("Separator", "分隔符");
            CaseLabel.Text = P("Case", "大小寫");
            MaxLabel.Text = P("Max length (0 = unlimited)", "最長長度（0 = 無限制）");
            DiacriticsChk.Content = P("Strip accents (café→cafe)", "去除重音（café→cafe）");
            CollapseChk.Content = P("Collapse repeats", "合併重複符號");
            UnicodeChk.Content = P("Keep unicode letters (中文)", "保留 Unicode 字母（中文）");
            OutputLabel.Text = P("Slugs", "別名");
            CopyBtn.Content = P("Copy", "複製");
            PreviewLabel.Text = P("Before → after (first line)", "轉換前 → 後（第一行）");
            UpdatePreview();
        }
        catch { }
    }

    private SlugifyService.Options ReadOptions()
    {
        var o = new SlugifyService.Options();
        try
        {
            o.Separator = SepBox.SelectedIndex switch
            {
                1 => SlugifyService.Separator.Underscore,
                2 => SlugifyService.Separator.Dot,
                _ => SlugifyService.Separator.Hyphen,
            };
            o.Case = CaseBox.SelectedIndex switch
            {
                1 => SlugifyService.LetterCase.Upper,
                2 => SlugifyService.LetterCase.Keep,
                _ => SlugifyService.LetterCase.Lower,
            };
            o.StripDiacritics = DiacriticsChk.IsChecked == true;
            o.CollapseRepeats = CollapseChk.IsChecked == true;
            o.KeepUnicodeLetters = UnicodeChk.IsChecked == true;
            o.MaxLength = (int)(double.IsNaN(MaxBox.Value) ? 0 : MaxBox.Value);
            if (o.MaxLength < 0) o.MaxLength = 0;
        }
        catch { }
        return o;
    }

    private void Convert()
    {
        try
        {
            string outText = SlugifyService.SlugifyBlock(InputBox.Text, ReadOptions());
            _suppress = true;
            OutputBox.Text = outText;
            _suppress = false;
            UpdatePreview();
        }
        catch { }
    }

    private void UpdatePreview()
    {
        try
        {
            string input = InputBox.Text ?? string.Empty;
            string firstIn = string.Empty;
            foreach (string line in input.Replace("\r", "\n").Split('\n'))
            {
                if (line.Trim().Length > 0) { firstIn = line.Trim(); break; }
            }
            if (firstIn.Length == 0)
            {
                PreviewText.Text = P("(type something above)", "（喺上面輸入啲文字）");
                return;
            }
            string slug = SlugifyService.Slugify(firstIn, ReadOptions());
            PreviewText.Text = $"{firstIn}  →  {(slug.Length == 0 ? "(empty)" : slug)}";
        }
        catch { }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) { if (!_suppress) Convert(); }
    private void Opt_Changed(object sender, RoutedEventArgs e) { if (!_suppress) Convert(); }
    private void Opt_Changed(object sender, SelectionChangedEventArgs e) { if (!_suppress) Convert(); }
    private void Num_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) { if (!_suppress) Convert(); }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            CopyBtn.Content = P("Copied ✓", "已複製 ✓");
        }
        catch { }
    }
}
