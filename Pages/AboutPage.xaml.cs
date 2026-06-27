using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Catalog;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 關於頁：雙語介紹、功能數目、免責聲明、原始碼連結。
/// About: bilingual intro, feature count, disclaimer and source link.
/// </summary>
public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Build();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Build();

    private void Build()
    {
        Root.Children.Clear();

        Root.Children.Add(new TextBlock
        {
            Text = "WinForge · 視窗鑄造",
            Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
        });
        Root.Children.Add(new TextBlock
        {
            Text = $"Windows 11 · {TweakCatalog.Count} {Loc.I.Pick("features", "項功能")} · {Categories.All.Length} {Loc.I.Pick("categories", "個分類")} · WinUI 3",
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        Root.Children.Add(Para(
            "WinForge is an all-in-one control center for Windows 11. Every feature is shown in both " +
            "English and Cantonese (粵語), and every toggle and action truly changes the system — " +
            "registry keys, power plans, network stack, privacy settings, cleanup and more.",
            "WinForge 係一個 Windows 11 全方位控制中心。每一項功能都同時用英文同粵語顯示，" +
            "而且每個開關同動作都會真正改到系統 — 登錄檔、電源計劃、網絡堆疊、私隱設定、清理等等。"));

        Root.Children.Add(Heading(Loc.I.Pick("Safety", "安全"), null));
        Root.Children.Add(Para(
            "These tweaks modify real Windows settings. Changes are reversible where possible, but please " +
            "read each description first. Some require administrator rights or a restart to take effect.",
            "呢啲調校會改到真實嘅 Windows 設定。可逆嘅都做咗可逆，但請先睇清楚每段說明。" +
            "部分需要管理員權限，或者要重啟先生效。"));

        Root.Children.Add(Heading(Loc.I.Pick("Source code", "原始碼"), null));
        var link = new Button
        {
            Content = "github.com/codingmachineedge/WinForge",
            Padding = new Thickness(0),
        };
        link.Click += (_, _) => CopyText("https://github.com/codingmachineedge/WinForge");
        Root.Children.Add(link);

        var licenses = new Button
        {
            Content = Loc.I.Pick("Licenses & source notices · 授權與原始碼聲明", "授權與原始碼聲明 · Licenses & source notices"),
        };
        licenses.Click += (_, _) => Navigator.GoToPage?.Invoke("licenses");
        Root.Children.Add(licenses);

        Root.Children.Add(new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Text = Loc.I.Pick("Version 1.0.0  ·  MIT License  ·  Built with .NET + WinUI 3", "版本 1.0.0  ·  MIT 授權  ·  用 .NET + WinUI 3 整"),
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
    }

    private static StackPanel Heading(string title, string? subtitle)
    {
        var p = new StackPanel { Spacing = 1, Margin = new Thickness(0, 6, 0, 0) };
        p.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 16 });
        if (!string.IsNullOrEmpty(subtitle))
            p.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Opacity = 0.7 });
        return p;
    }

    private static StackPanel Para(string en, string zh)
    {
        var p = new StackPanel { Spacing = 4 };
        p.Children.Add(new TextBlock { Text = en, TextWrapping = TextWrapping.Wrap });
        p.Children.Add(new TextBlock
        {
            Text = zh,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        return p;
    }

    private static void CopyText(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
    }
}
