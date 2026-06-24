using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內視窗管理 · In-app window manager — list top-level windows and snap the selected one to
/// halves/quarters/thirds, maximise, centre, or pin always-on-top. Pure Win32, no redirect. Bilingual.
/// </summary>
public sealed partial class WindowManagerModule : Page
{
    public WindowManagerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); BuildPad(); };
        Loaded += (_, _) => { Render(); BuildPad(); Reload(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Window Manager · 視窗管理";
        HeaderBlurb.Text = P("Pick an open window on the left, then snap it to a zone — halves, quarters, thirds, maximise, centre or always-on-top.",
            "喺左邊揀一個開住嘅視窗，再貼去一個分區 — 一半、四分一、三分一、最大化、置中或者永遠置頂。");
        RefreshBtn.Content = P("Refresh", "重新整理");
    }

    private void Reload()
    {
        var wins = WindowManager.List();
        List.ItemsSource = wins;
        CountText.Text = P($"{wins.Count} windows", $"{wins.Count} 個視窗");
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private void List_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void BuildPad()
    {
        SnapPad.Children.Clear();

        AddRowLabel(P("Halves", "一半"));
        AddRow(
            ("◧ " + P("Left", "左"), Zone.LeftHalf),
            ("◨ " + P("Right", "右"), Zone.RightHalf),
            ("⬒ " + P("Top", "上"), Zone.TopHalf),
            ("⬓ " + P("Bottom", "下"), Zone.BottomHalf));

        AddRowLabel(P("Quarters", "四分一"));
        AddRow(
            ("◰ " + P("Top-L", "左上"), Zone.TopLeft),
            ("◳ " + P("Top-R", "右上"), Zone.TopRight),
            ("◱ " + P("Bot-L", "左下"), Zone.BottomLeft),
            ("◲ " + P("Bot-R", "右下"), Zone.BottomRight));

        AddRowLabel(P("Thirds", "三分一"));
        AddRow(
            (P("Left ⅓", "左 ⅓"), Zone.LeftThird),
            (P("Centre ⅓", "中 ⅓"), Zone.CenterThird),
            (P("Right ⅓", "右 ⅓"), Zone.RightThird));

        AddRowLabel(P("Whole", "整個"));
        AddRow(
            (P("Maximise", "最大化"), Zone.Maximize),
            (P("Centre", "置中"), Zone.Center),
            (P("Full area", "全工作區"), Zone.FullArea));

        // extras
        var extras = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var focus = new Button { Content = P("Focus", "聚焦") };
        focus.Click += (_, _) => { if (Selected(out var h)) WindowManager.Focus(h); };
        var top = new ToggleSwitch { OnContent = P("On top", "置頂"), OffContent = P("Normal", "正常") };
        top.Toggled += (_, _) => { if (Selected(out var h)) WindowManager.SetTopMost(h, top.IsOn); };
        extras.Children.Add(focus);
        extras.Children.Add(top);
        SnapPad.Children.Add(extras);
    }

    private void AddRowLabel(string text)
        => SnapPad.Children.Add(new TextBlock { Text = text, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });

    private void AddRow(params (string label, Zone zone)[] items)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (label, zone) in items)
        {
            var btn = new Button { Content = label, MinWidth = 72 };
            btn.Click += (_, _) => DoSnap(zone);
            row.Children.Add(btn);
        }
        SnapPad.Children.Add(row);
    }

    private bool Selected(out IntPtr handle)
    {
        if (List.SelectedItem is WinInfo w) { handle = w.Handle; return true; }
        handle = IntPtr.Zero;
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = P("Select a window on the left first.", "請先喺左邊揀一個視窗。");
        ResultBar.IsOpen = true;
        return false;
    }

    private void DoSnap(Zone zone)
    {
        if (!Selected(out var h)) return;
        try
        {
            WindowManager.Snap(h, zone);
            var w = List.SelectedItem as WinInfo;
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Snapped", "已貼齊");
            ResultBar.Message = P($"{w?.Title} → {zone}", $"{w?.Title} → {zone}");
            ResultBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            ResultBar.Severity = InfoBarSeverity.Error;
            ResultBar.Title = P("Failed", "失敗");
            ResultBar.Message = ex.Message;
            ResultBar.IsOpen = true;
        }
    }
}
