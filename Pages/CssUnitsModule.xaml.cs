using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// CSS 單位換算 · CSS unit converter — enter a value + source unit, resolve against a
/// context (root/element font-size, viewport, container) and see every other CSS unit live.
/// Click a result row to copy it (e.g. "12.5rem"). Pure managed maths, never throws. Bilingual (粵語).
/// </summary>
public sealed partial class CssUnitsModule : Page
{
    private bool _ready;

    public CssUnitsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("CSS Unit Converter · CSS 單位換算", "CSS 單位換算 · CSS Unit Converter");
            HeaderBlurb.Text = P(
                "Convert any CSS length to every other unit at once. Absolute units use the CSS 96-DPI reference; relative units (em/rem/%/vw/vh) resolve against the context below. Click a result to copy it.",
                "一次過將任何 CSS 長度換算成其他所有單位。絕對單位用 CSS 96-DPI 標準；相對單位（em/rem/%/vw/vh）跟返下面嘅內容脈絡計。㩒一下結果就可以複製。");
            InputTitle.Text = P("Value to convert", "要換算嘅數值");
            ContextTitle.Text = P("Context", "內容脈絡");
            ContextBlurb.Text = P(
                "Used to resolve relative units. em uses the element font-size; rem the root; % the container; vw/vh the viewport.",
                "用嚟計相對單位。em 用元素字級；rem 用根字級；% 用容器；vw/vh 用視窗大細。");
            RootLabel.Text = P("Root font-size (px) — rem", "根字級（px）— rem");
            ElemLabel.Text = P("Element font-size (px) — em", "元素字級（px）— em");
            VwLabel.Text = P("Viewport width (px) — vw", "視窗闊度（px）— vw");
            VhLabel.Text = P("Viewport height (px) — vh", "視窗高度（px）— vh");
            ContainerLabel.Text = P("Container size (px) — %", "容器大細（px）— %");
            ResultsTitle.Text = P("Converted", "換算結果");
            CopyHint.Text = P("click a row to copy", "㩒一行複製");

            // Populate the unit ComboBox once (guard against re-render duplicating items).
            if (UnitBox.Items.Count == 0)
            {
                foreach (var u in CssUnitsService.Units) UnitBox.Items.Add(u);
                UnitBox.SelectedItem = "px";
                _ready = true;
            }
        }
        catch { /* never throws */ }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Recompute();
    private void Unit_Changed(object sender, SelectionChangedEventArgs e) => Recompute();
    private void Ctx_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => Recompute();

    private void Recompute()
    {
        if (!_ready) return;
        try
        {
            double value = CssUnitsService.Parse(ValueBox?.Text);
            string unit = UnitBox?.SelectedItem as string ?? "px";
            var ctx = new CssUnitsService.Context
            {
                RootFontPx = Val(RootBox, 16),
                ElementFontPx = Val(ElemBox, 16),
                ViewportWidthPx = Val(VwBox, 1920),
                ViewportHeightPx = Val(VhBox, 1080),
                ContainerPx = Val(ContainerBox, 1000),
            };
            ResultsList.ItemsSource = CssUnitsService.ConvertAll(value, unit, ctx);
        }
        catch { /* never throws */ }
    }

    private static double Val(NumberBox? box, double fallback)
    {
        if (box == null) return fallback;
        double v = box.Value;
        return (double.IsNaN(v) || double.IsInfinity(v)) ? fallback : v;
    }

    private void Result_Click(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (e.ClickedItem is CssUnitsService.Result r && !string.IsNullOrEmpty(r.Combined))
            {
                var pkg = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                pkg.SetText(r.Combined);
                Clipboard.SetContent(pkg);
                CopyHint.Text = P($"copied {r.Combined}", $"已複製 {r.Combined}");
            }
        }
        catch { /* never throws */ }
    }
}
