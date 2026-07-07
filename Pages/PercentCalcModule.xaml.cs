using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 百分比計算器 · Percentage & ratio calculator — a stack of live cards: X% of Y, X is what % of Y,
/// % change, increase/decrease by X%, tip splitter, ratio-simplify. Each card recalculates on input
/// and can copy its result. Pure managed, never-throw, bilingual. No redirect / side-effects.
/// </summary>
public sealed partial class PercentCalcModule : Page
{
    private string _c1, _c2, _c3, _c4, _c5, _c6 = "";

    public PercentCalcModule()
    {
        InitializeComponent();
        _c1 = _c2 = _c3 = _c4 = _c5 = _c6 = "";
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();
    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;
    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("Percentage Calculator · 百分比計算器", "百分比計算器 · Percentage Calculator");
        HeaderBlurb.Text = P("Everyday percentage and ratio maths — percentages, percent change, increase/decrease, a tip splitter and a ratio simplifier. Every card updates as you type.",
            "日常百分比同比例運算 — 百分比、變化率、加減百分比、貼士分帳同埋比例化簡。你一打字，每張卡即刻更新。");

        C1Title.Text = P("X% of Y", "Y 嘅 X%");
        C2Title.Text = P("X is what % of Y", "X 係 Y 嘅百分之幾");
        C3Title.Text = P("% change from A to B", "由 A 到 B 嘅變化率");
        C4Title.Text = P("Increase / decrease Y by X%", "將 Y 加 / 減 X%");
        C5Title.Text = P("Tip splitter (bill, tip %, split)", "貼士分帳（帳單、貼士 %、人數）");
        C6Title.Text = P("Ratio simplify (a : b)", "比例化簡（a : b）");

        SetPlaceholder(C1X, P("X (%)", "X（%）")); SetPlaceholder(C1Y, P("Y", "Y"));
        SetPlaceholder(C2X, P("X", "X")); SetPlaceholder(C2Y, P("Y", "Y"));
        SetPlaceholder(C3A, P("A (from)", "A（由）")); SetPlaceholder(C3B, P("B (to)", "B（到）"));
        SetPlaceholder(C4Y, P("Y", "Y")); SetPlaceholder(C4X, P("X (%)", "X（%）"));
        SetPlaceholder(C5Bill, P("Bill", "帳單")); SetPlaceholder(C5Tip, P("Tip %", "貼士 %")); SetPlaceholder(C5Split, P("People", "人數"));
        SetPlaceholder(C6A, P("a", "a")); SetPlaceholder(C6B, P("b", "b"));

        C4Inc.Content = P("Increase", "加");
        C4Dec.Content = P("Decrease", "減");

        string copy = P("Copy", "複製");
        C1Copy.Content = copy; C2Copy.Content = copy; C3Copy.Content = copy;
        C4Copy.Content = copy; C5Copy.Content = copy; C6Copy.Content = copy;

        Recalc(this, null!);
    }

    private static void SetPlaceholder(TextBox box, string text) { box.PlaceholderText = text; }

    private void Recalc(object sender, object e)
    {
        // Cards may not be built yet during early InitializeComponent parsing.
        if (C1Out == null) return;

        string bad = P("Check the numbers.", "睇下啲數字啱唔啱。");

        // Card 1 — X% of Y
        try
        {
            var r = PercentCalcService.PercentOf(C1X.Text, C1Y.Text);
            _c1 = r.Ok ? r.Value : "";
            C1Out.Text = r.Ok
                ? P($"= {r.Value}", $"= {r.Value}")
                : (AnyEmpty(C1X, C1Y) ? "" : bad);
        }
        catch { _c1 = ""; C1Out.Text = bad; }

        // Card 2 — X is what % of Y
        try
        {
            var r = PercentCalcService.WhatPercent(C2X.Text, C2Y.Text);
            _c2 = r.Ok ? r.Value : "";
            C2Out.Text = r.Ok
                ? P($"= {r.Value}", $"= {r.Value}")
                : (AnyEmpty(C2X, C2Y) ? "" : P("Y cannot be zero.", "Y 唔可以係零。"));
        }
        catch { _c2 = ""; C2Out.Text = bad; }

        // Card 3 — % change
        try
        {
            var r = PercentCalcService.PercentChange(C3A.Text, C3B.Text);
            _c3 = r.Ok ? r.Value : "";
            C3Out.Text = r.Ok
                ? P($"= {r.Value}", $"= {r.Value}")
                : (AnyEmpty(C3A, C3B) ? "" : P("Starting value A cannot be zero.", "起始值 A 唔可以係零。"));
        }
        catch { _c3 = ""; C3Out.Text = bad; }

        // Card 4 — increase / decrease
        try
        {
            bool inc = C4Inc.IsChecked == true;
            var r = PercentCalcService.AdjustBy(C4Y.Text, C4X.Text, inc);
            _c4 = r.Ok ? r.Value : "";
            C4Out.Text = r.Ok
                ? P($"= {r.Value}", $"= {r.Value}")
                : (AnyEmpty(C4Y, C4X) ? "" : bad);
        }
        catch { _c4 = ""; C4Out.Text = bad; }

        // Card 5 — tip
        try
        {
            var r = PercentCalcService.Tip(C5Bill.Text, C5Tip.Text, C5Split.Text);
            if (r.Ok)
            {
                _c5 = P($"Tip {PercentCalcService.Fmt(r.TipAmount)} · Total {PercentCalcService.Fmt(r.Total)} · Each {PercentCalcService.Fmt(r.PerPerson)}",
                        $"貼士 {PercentCalcService.Fmt(r.TipAmount)} · 合計 {PercentCalcService.Fmt(r.Total)} · 每人 {PercentCalcService.Fmt(r.PerPerson)}");
                C5Out.Text = _c5;
            }
            else
            {
                _c5 = "";
                C5Out.Text = AnyEmpty(C5Bill, C5Tip, C5Split) ? "" : P("Check bill, tip % and people (≥ 1).", "睇下帳單、貼士 % 同人數（≥ 1）。");
            }
        }
        catch { _c5 = ""; C5Out.Text = bad; }

        // Card 6 — ratio simplify
        try
        {
            var r = PercentCalcService.SimplifyRatio(C6A.Text, C6B.Text);
            if (r.Ok)
            {
                _c6 = $"{r.A} : {r.B}";
                C6Out.Text = P($"= {r.A} : {r.B}", $"= {r.A} : {r.B}");
            }
            else
            {
                _c6 = "";
                C6Out.Text = AnyEmpty(C6A, C6B) ? "" : P("Enter two numbers (not both zero).", "輸入兩個數字（唔可以兩個都係零）。");
            }
        }
        catch { _c6 = ""; C6Out.Text = bad; }
    }

    private static bool AnyEmpty(params TextBox[] boxes)
    {
        foreach (var b in boxes) if (string.IsNullOrWhiteSpace(b.Text)) return true;
        return false;
    }

    private void CopyValue(string value)
    {
        try
        {
            if (string.IsNullOrEmpty(value)) return;
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(value);
            Clipboard.SetContent(dp);
        }
        catch { /* clipboard busy — ignore */ }
    }

    private void Copy1(object sender, RoutedEventArgs e) => CopyValue(_c1);
    private void Copy2(object sender, RoutedEventArgs e) => CopyValue(_c2);
    private void Copy3(object sender, RoutedEventArgs e) => CopyValue(_c3);
    private void Copy4(object sender, RoutedEventArgs e) => CopyValue(_c4);
    private void Copy5(object sender, RoutedEventArgs e) => CopyValue(_c5);
    private void Copy6(object sender, RoutedEventArgs e) => CopyValue(_c6);
}
