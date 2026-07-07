using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// XPath 測試器 · XPath tester — paste XML, type an XPath, see each matched node (name + value +
/// outer XML) and a live match count. Also handles string / number / boolean results. Pure managed
/// (System.Xml.Linq / System.Xml.XPath); never throws — errors surface as bilingual status text.
/// </summary>
public sealed partial class XPathTesterModule : Page
{
    private readonly ObservableCollection<XPathTesterService.XPathMatch> _rows = new();

    public XPathTesterModule()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(XmlBox.Text))
        {
            XmlBox.Text = "<catalog>\n  <book id=\"b1\"><title>WinForge</title><price>0</price></book>\n  <book id=\"b2\"><title>Reactor</title><price>42</price></book>\n</catalog>";
            XPathBox.Text = "//book/title";
        }
        Render();
        Reevaluate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        Reevaluate();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "XPath Tester · XPath 測試器";
        HeaderBlurb.Text = P("Paste XML, type an XPath expression, and see the matching nodes update live. Node names, text values and outer XML are listed, and string / number / boolean results are shown too.",
            "貼上 XML，打條 XPath 表達式，即時睇到匹配嘅節點。會列出節點名、文字值同外層 XML，字串／數字／布林結果都顯示到。");
        XmlLabel.Text = P("XML", "XML 內容");
        XPathLabel.Text = P("XPath expression", "XPath 表達式");
        ResultsLabel.Text = P("Matches", "匹配結果");
        Reevaluate(); // refresh status/empty text in the current language
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Reevaluate();

    private void Reevaluate()
    {
        _rows.Clear();

        XPathTesterService.XPathResult res;
        try
        {
            res = XPathTesterService.Evaluate(XmlBox?.Text, XPathBox?.Text);
        }
        catch (Exception ex)
        {
            StatusText.Text = P($"Unexpected error: {ex.Message}", $"意外錯誤：{ex.Message}");
            EmptyText.Text = "";
            return;
        }

        if (!res.Ok)
        {
            StatusText.Text = P(res.ErrorEn ?? "Error.", res.ErrorZh ?? "錯誤。");
            EmptyText.Text = "";
            return;
        }

        if (res.Scalar != null)
        {
            StatusText.Text = P($"Result (scalar): {res.Scalar}", $"結果（純量）：{res.Scalar}");
            EmptyText.Text = "";
            return;
        }

        foreach (var m in res.Matches) _rows.Add(m);

        StatusText.Text = P($"{res.Count} match(es).", $"{res.Count} 個匹配。");
        EmptyText.Text = res.Count == 0
            ? P("No nodes matched this expression.", "呢條表達式冇匹配到任何節點。")
            : "";
    }
}
