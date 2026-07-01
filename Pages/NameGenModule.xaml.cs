using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 名稱產生器 · Name generator — pick a type (username, project, company, fantasy, band, slug),
/// choose how many, and generate cryptographically-random names into a read-only list.
/// Copy-all to clipboard. Pure managed, never throws, bilingual (粵語).
/// </summary>
public sealed partial class NameGenModule : Page
{
    private bool _suppress;
    private readonly NameGenService.Kind[] _kinds =
    {
        NameGenService.Kind.Username,
        NameGenService.Kind.Project,
        NameGenService.Kind.Company,
        NameGenService.Kind.Fantasy,
        NameGenService.Kind.Band,
        NameGenService.Kind.Slug
    };

    public NameGenModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        if (TypeBox.SelectedIndex < 0) { _suppress = true; TypeBox.SelectedIndex = 0; _suppress = false; }
        Generate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Name Generator · 名稱產生器";
            HeaderBlurb.Text = P("Spin up random names — usernames, project codenames, startup blends, fantasy names, band names or URL slugs. Nothing leaves your PC.",
                "隨機整名 — 用戶名、專案代號、初創混合名、奇幻名、樂隊名或者網址 slug。全部喺你部機度整，唔會外洩。");
            TypeLabel.Text = P("Name type", "名稱類型");
            CountLabel.Text = P("How many (1–100)", "整幾多個（1–100）");
            RegenBtn.Content = P("Regenerate", "重新產生");
            CopyBtn.Content = P("Copy all", "全部複製");

            int keep = TypeBox.SelectedIndex;
            _suppress = true;
            TypeBox.Items.Clear();
            foreach (var name in KindNames()) TypeBox.Items.Add(name);
            TypeBox.SelectedIndex = keep >= 0 && keep < _kinds.Length ? keep : 0;
            _suppress = false;

            UpdateStatus();
        }
        catch { /* never throw from UI */ }
    }

    private IEnumerable<string> KindNames()
    {
        yield return P("Username (adjective + noun + number)", "用戶名（形容詞 + 名詞 + 數字）");
        yield return P("Project name (codename style)", "專案名（代號風格）");
        yield return P("Company / startup name (blend)", "公司 / 初創名（混合詞）");
        yield return P("Fantasy name (syllable generator)", "奇幻名（音節產生器）");
        yield return P("Band name (The Adjective Nouns)", "樂隊名（The 形容詞 名詞）");
        yield return P("Slug (kebab-case)", "Slug（kebab 連字號）");
    }

    private NameGenService.Kind SelectedKind()
    {
        int i = TypeBox.SelectedIndex;
        return i >= 0 && i < _kinds.Length ? _kinds[i] : NameGenService.Kind.Username;
    }

    private int SelectedCount()
    {
        double v = CountBox.Value;
        if (double.IsNaN(v)) return 10;
        int n = (int)v;
        if (n < 1) n = 1;
        if (n > 100) n = 100;
        return n;
    }

    private void Generate()
    {
        try
        {
            var names = NameGenService.Generate(SelectedKind(), SelectedCount());
            OutputBox.Text = string.Join(Environment.NewLine, names);
            UpdateStatus(names.Count);
        }
        catch
        {
            OutputBox.Text = "";
            StatusText.Text = P("Could not generate names.", "無法產生名稱。");
        }
    }

    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Generate();
    }

    private void Count_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        Generate();
    }

    private void Regen_Click(object sender, RoutedEventArgs e) => Generate();

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? "";
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "未有嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied all to clipboard.", "已全部複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Clipboard is unavailable right now.", "剪貼簿而家用唔到。");
        }
    }

    private void UpdateStatus(int count = -1)
    {
        try
        {
            if (count < 0)
            {
                var t = OutputBox.Text ?? "";
                count = string.IsNullOrEmpty(t) ? 0 : t.Split('\n').Length;
            }
            StatusText.Text = P($"Generated {count} name(s).", $"已產生 {count} 個名稱。");
        }
        catch { /* never throw */ }
    }
}
