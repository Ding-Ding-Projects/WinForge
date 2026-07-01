using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 假資料產生器 · Lorem ipsum &amp; fake-data generator. Produces placeholder text and columns of
/// realistic-looking fake records. Pure managed C#; all randomness via RandomNumberGenerator. Bilingual.
/// </summary>
public sealed partial class FakerModule : Page
{
    public FakerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Data Faker · 假資料產生器";
            HeaderBlurb.Text = P("Generate lorem ipsum placeholder text and columns of realistic fake data — names, emails, addresses, UUIDs and more — for mock-ups, tests and seed data.",
                "產生 lorem ipsum 佔位文字，同埋一欄欄逼真嘅假資料 — 姓名、電郵、地址、UUID 等等 — 畀你做樣板、測試同種子資料用。");
            StatusText.Text = P("All values are randomly generated with a cryptographic RNG. Nothing here is real.",
                "所有數值都係用加密隨機產生器隨機生成，冇一樣係真嘅。");

            LoremTitle.Text = P("Lorem ipsum", "Lorem ipsum 佔位文字");
            LoremModeLabel.Text = P("Mode", "模式");
            LoremCountLabel.Text = P("Count", "數量");
            LoremGenBtn.Content = P("Generate", "產生");
            LoremCopyBtn.Content = P("Copy", "複製");

            int loremSel = LoremModeBox.SelectedIndex < 0 ? 0 : LoremModeBox.SelectedIndex;
            LoremModeBox.Items.Clear();
            LoremModeBox.Items.Add(P("Paragraphs", "段落"));
            LoremModeBox.Items.Add(P("Sentences", "句子"));
            LoremModeBox.Items.Add(P("Words", "字詞"));
            LoremModeBox.SelectedIndex = loremSel;

            DataTitle.Text = P("Fake data", "假資料");
            DataFieldLabel.Text = P("Field", "欄位");
            DataCountLabel.Text = P("Count", "數量");
            DataGenBtn.Content = P("Generate", "產生");
            DataCopyBtn.Content = P("Copy", "複製");

            int dataSel = DataFieldBox.SelectedIndex < 0 ? 0 : DataFieldBox.SelectedIndex;
            DataFieldBox.Items.Clear();
            DataFieldBox.Items.Add(P("Full name", "全名"));
            DataFieldBox.Items.Add(P("Email", "電郵"));
            DataFieldBox.Items.Add(P("Username", "用戶名"));
            DataFieldBox.Items.Add(P("Phone", "電話"));
            DataFieldBox.Items.Add(P("Street address", "街道地址"));
            DataFieldBox.Items.Add(P("City", "城市"));
            DataFieldBox.Items.Add(P("Company", "公司"));
            DataFieldBox.Items.Add(P("UUID", "UUID"));
            DataFieldBox.Items.Add(P("Date", "日期"));
            DataFieldBox.Items.Add(P("Integer", "整數"));
            DataFieldBox.Items.Add(P("Boolean", "布林值"));
            DataFieldBox.Items.Add(P("IPv4", "IPv4"));
            DataFieldBox.Items.Add(P("Hex color", "十六進位色碼"));
            DataFieldBox.SelectedIndex = dataSel;
        }
        catch { /* never throw from UI */ }
    }

    private static int CountOf(NumberBox box, int fallback)
    {
        double v = box.Value;
        if (double.IsNaN(v)) return fallback;
        return Math.Clamp((int)v, 1, 500);
    }

    private void LoremGen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mode = LoremModeBox.SelectedIndex switch
            {
                2 => FakerService.LoremMode.Words,
                1 => FakerService.LoremMode.Sentences,
                _ => FakerService.LoremMode.Paragraphs
            };
            LoremOutput.Text = FakerService.Lorem_(mode, CountOf(LoremCountBox, 3));
        }
        catch (Exception ex) { StatusText.Text = P("Could not generate: ", "無法產生：") + ex.Message; }
    }

    private void DataGen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int sel = DataFieldBox.SelectedIndex < 0 ? 0 : DataFieldBox.SelectedIndex;
            var field = (FakerService.Field)sel;
            DataOutput.Text = FakerService.Generate(field, CountOf(DataCountBox, 10));
        }
        catch (Exception ex) { StatusText.Text = P("Could not generate: ", "無法產生：") + ex.Message; }
    }

    private void LoremCopy_Click(object sender, RoutedEventArgs e) => Copy(LoremOutput.Text);
    private void DataCopy_Click(object sender, RoutedEventArgs e) => Copy(DataOutput.Text);

    private void Copy(string? text)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy — generate something first.", "冇嘢可以複製 — 先產生啲資料。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex) { StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message; }
    }
}
