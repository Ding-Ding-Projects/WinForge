using System;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 通用單位換算 · General unit converter — pick a category, a from/to unit and a value; the result
/// updates live. Pure offline managed conversion via <see cref="UnitConvertService"/> (base-unit
/// factors, temperature affine). Non-numeric input shows a friendly status, never throws. Bilingual.
/// </summary>
public sealed partial class UnitConvertModule : Page
{
    private bool _suppress;

    public UnitConvertModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { RenderStatics(); PopulateCategories(); };
        Loaded += (_, _) => { RenderStatics(); PopulateCategories(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void RenderStatics()
    {
        Header.Title = P("Unit Converter · 單位換算", "單位換算 · Unit Converter");
        HeaderBlurb.Text = P("Convert between units of length, mass, temperature, data, speed, area, time and pressure — fully offline.",
            "喺長度、質量、溫度、資料、速度、面積、時間同壓力單位之間換算 — 全程離線。");
        CategoryLabel.Text = P("Category", "類別");
        ValueLabel.Text = P("Value", "數值");
        FromLabel.Text = P("From", "由");
        ToLabel.Text = P("To", "轉做");
        CopyLabel.Text = P("Copy result", "複製結果");
    }

    private UnitCategory? CurrentCategory =>
        (CategoryBox.SelectedItem as ComboBoxItem)?.Tag as UnitCategory;

    private void PopulateCategories()
    {
        _suppress = true;
        int keep = CategoryBox.SelectedIndex;
        CategoryBox.Items.Clear();
        foreach (var cat in UnitConvertService.Categories)
            CategoryBox.Items.Add(new ComboBoxItem { Content = cat.Label(Loc.I), Tag = cat });
        CategoryBox.SelectedIndex = keep >= 0 && keep < CategoryBox.Items.Count ? keep : 0;
        _suppress = false;
        PopulateUnits();
    }

    private void PopulateUnits()
    {
        var cat = CurrentCategory;
        if (cat is null) return;

        _suppress = true;
        int keepFrom = FromBox.SelectedIndex;
        int keepTo = ToBox.SelectedIndex;
        FromBox.Items.Clear();
        ToBox.Items.Clear();
        foreach (var u in cat.Units)
        {
            FromBox.Items.Add(new ComboBoxItem { Content = u.Label(Loc.I), Tag = u });
            ToBox.Items.Add(new ComboBoxItem { Content = u.Label(Loc.I), Tag = u });
        }
        // Sensible defaults: first → second (or first → first when only one unit).
        FromBox.SelectedIndex = keepFrom >= 0 && keepFrom < FromBox.Items.Count ? keepFrom : 0;
        ToBox.SelectedIndex = keepTo >= 0 && keepTo < ToBox.Items.Count ? keepTo
            : (ToBox.Items.Count > 1 ? 1 : 0);
        _suppress = false;

        UpdateReference(cat);
        Compute();
    }

    private void Category_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        // Category changed → repopulate units from scratch (don't preserve stale indices).
        _suppress = true;
        FromBox.SelectedIndex = -1;
        ToBox.SelectedIndex = -1;
        _suppress = false;
        // Reset kept indices by clearing then repopulating fresh.
        var cat = CurrentCategory;
        if (cat is null) return;
        _suppress = true;
        FromBox.Items.Clear();
        ToBox.Items.Clear();
        foreach (var u in cat.Units)
        {
            FromBox.Items.Add(new ComboBoxItem { Content = u.Label(Loc.I), Tag = u });
            ToBox.Items.Add(new ComboBoxItem { Content = u.Label(Loc.I), Tag = u });
        }
        FromBox.SelectedIndex = 0;
        ToBox.SelectedIndex = ToBox.Items.Count > 1 ? 1 : 0;
        _suppress = false;
        UpdateReference(cat);
        Compute();
    }

    private void Any_Changed(object sender, object e)
    {
        if (_suppress) return;
        Compute();
    }

    private void Compute()
    {
        StatusText.Visibility = Microsoft.UI.Xaml.Visibility.Collapsed;

        var cat = CurrentCategory;
        var from = (FromBox.SelectedItem as ComboBoxItem)?.Tag as UnitDef;
        var to = (ToBox.SelectedItem as ComboBoxItem)?.Tag as UnitDef;
        if (cat is null || from is null || to is null)
            return;

        double value = ValueBox.Value;
        if (double.IsNaN(value))
        {
            ResultText.Text = "—";
            SummaryText.Text = "";
            StatusText.Text = P("Enter a number to convert.", "輸入一個數字嚟換算。");
            StatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            return;
        }

        double result = UnitConvertService.Convert(value, from, to);
        if (double.IsNaN(result) || double.IsInfinity(result))
        {
            ResultText.Text = "—";
            SummaryText.Text = "";
            StatusText.Text = P("That conversion isn't valid.", "呢個換算唔成立。");
            StatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
            return;
        }

        ResultText.Text = $"{Trim(value)} {from.Label(Loc.I)}  =  {Trim(result)} {to.Label(Loc.I)}";

        double one = UnitConvertService.Convert(1.0, from, to);
        SummaryText.Text = $"1 {from.Label(Loc.I)} = {Trim(one)} {to.Label(Loc.I)}";
    }

    private void UpdateReference(UnitCategory cat)
    {
        ReferenceTitle.Text = P($"Units in {cat.Label(Loc.I)}", $"「{cat.Label(Loc.I)}」嘅單位");
        ReferenceList.Text = string.Join("  ·  ", cat.Units.Select(u => u.Label(Loc.I)));
    }

    private void Copy_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        try
        {
            string text = ResultText.Text;
            if (string.IsNullOrWhiteSpace(text) || text == "—") return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            CopyLabel.Text = P("Copied!", "已複製！");
            var timer = DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1.5);
            timer.Tick += (s, _) => { CopyLabel.Text = P("Copy result", "複製結果"); timer.Stop(); };
            timer.Start();
        }
        catch
        {
            // Clipboard can transiently fail; never crash the page.
            StatusText.Text = P("Couldn't copy to the clipboard.", "無法複製到剪貼簿。");
            StatusText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
    }

    /// <summary>Trim a value to a readable precision without trailing zeros.</summary>
    private static string Trim(double v)
    {
        if (v == 0) return "0";
        double abs = Math.Abs(v);
        string s = (abs >= 1e12 || abs < 1e-6)
            ? v.ToString("G6")
            : v.ToString("0.##########");
        return s;
    }
}
