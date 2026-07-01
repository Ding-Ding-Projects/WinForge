using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 假文產生器 · Lorem Ipsum / placeholder-text generator. Classic Latin or hipster/tech
/// word pools; produces paragraphs, sentences, words or list items with an optional
/// HTML wrap and the familiar "Lorem ipsum dolor sit amet" opener. Copy to clipboard.
/// Pure managed C# (RandomNumberGenerator). Bilingual (English + 粵語). Never throws.
/// </summary>
public sealed partial class LoremTextModule : Page
{
    private bool _ready;

    public LoremTextModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { BuildCombos(); Render(); _ready = true; Generate(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void BuildCombos()
    {
        try
        {
            int unitIdx = UnitCombo.SelectedIndex;
            int poolIdx = PoolCombo.SelectedIndex;

            UnitCombo.Items.Clear();
            UnitCombo.Items.Add(P("Paragraphs", "段落"));
            UnitCombo.Items.Add(P("Sentences", "句子"));
            UnitCombo.Items.Add(P("Words", "字詞"));
            UnitCombo.Items.Add(P("List items", "列表項目"));
            UnitCombo.SelectedIndex = unitIdx >= 0 ? unitIdx : 0;

            PoolCombo.Items.Clear();
            PoolCombo.Items.Add(P("Classic Latin", "經典拉丁文"));
            PoolCombo.Items.Add(P("Hipster / tech", "潮語／科技"));
            PoolCombo.SelectedIndex = poolIdx >= 0 ? poolIdx : 0;
        }
        catch { /* never throw during UI build */ }
    }

    private void Render()
    {
        try
        {
            Header.Title = P("Lorem Ipsum", "假文產生器");
            HeaderBlurb.Text = P("Generate placeholder / dummy text — paragraphs, sentences, words or list items — for mock-ups and layouts. Choose classic Latin or a hipster/tech word pool.",
                "產生佔位／假文 — 段落、句子、字詞或列表項目 — 畀你做草圖同版面。可揀經典拉丁文或者潮語／科技詞庫。");
            UnitLabel.Text = P("Generate", "產生");
            CountLabel.Text = P("How many", "數量");
            PoolLabel.Text = P("Word pool", "詞庫");
            RangeLabel.Text = P("Sentences per paragraph", "每段句數");
            ClassicSwitch.Header = P("Start with “Lorem ipsum dolor sit amet”", "以「Lorem ipsum dolor sit amet」開頭");
            HtmlSwitch.Header = P("Wrap in HTML (<p> / <li>)", "包 HTML 標籤（<p> / <li>）");
            GenerateBtn.Content = P("Generate", "產生");
            CopyBtn.Content = P("Copy", "複製");
            OutputLabel.Text = P("Output", "輸出");
            BuildCombos();
            UpdateCounts();
        }
        catch { /* never throw */ }
    }

    private LoremTextService.Options ReadOptions()
    {
        var o = new LoremTextService.Options();
        o.Unit = UnitCombo.SelectedIndex switch
        {
            1 => LoremTextService.Unit.Sentences,
            2 => LoremTextService.Unit.Words,
            3 => LoremTextService.Unit.ListItems,
            _ => LoremTextService.Unit.Paragraphs,
        };
        o.Pool = PoolCombo.SelectedIndex == 1
            ? LoremTextService.Pool.HipsterTech
            : LoremTextService.Pool.ClassicLatin;
        o.Count = ToInt(CountBox.Value, 5);
        o.StartWithClassic = ClassicSwitch.IsOn;
        o.HtmlWrap = HtmlSwitch.IsOn;
        o.MinSentencesPerParagraph = ToInt(MinSentBox.Value, 3);
        o.MaxSentencesPerParagraph = ToInt(MaxSentBox.Value, 7);
        return o;
    }

    private static int ToInt(double v, int fallback) =>
        double.IsNaN(v) ? fallback : (int)Math.Round(v);

    private void Generate()
    {
        if (!_ready) return;
        try
        {
            var result = LoremTextService.Generate(ReadOptions());
            OutputBox.Text = result.Text ?? string.Empty;
            CountText.Text = P($"{result.Words:N0} words · {result.Characters:N0} characters",
                                $"{result.Words:N0} 字 · {result.Characters:N0} 字元");
        }
        catch
        {
            OutputBox.Text = string.Empty;
            UpdateCounts();
        }
    }

    private void UpdateCounts()
    {
        try
        {
            string t = OutputBox.Text ?? string.Empty;
            int words = 0;
            bool inWord = false;
            foreach (char c in t)
            {
                bool ws = char.IsWhiteSpace(c);
                if (!ws && !inWord) { words++; inWord = true; }
                else if (ws) inWord = false;
            }
            CountText.Text = P($"{words:N0} words · {t.Length:N0} characters",
                                $"{words:N0} 字 · {t.Length:N0} 字元");
        }
        catch { /* never throw */ }
    }

    private void Any_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_ready) Generate();
    }

    private void Generate_Click(object sender, RoutedEventArgs e) => Generate();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string t = OutputBox.Text ?? string.Empty;
            if (t.Length == 0) return;
            var pkg = new DataPackage();
            pkg.SetText(t);
            Clipboard.SetContent(pkg);
        }
        catch { /* never throw */ }
    }
}
