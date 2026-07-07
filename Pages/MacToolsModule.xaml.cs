using System;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// MAC 位址工具 · MAC address tools — format/normalize a MAC to colon / hyphen / Cisco-dot / bare
/// (upper &amp; lower, copyable rows), validate + analyze the I/G and U/L bits, look up the OUI vendor,
/// and generate a random locally-administered unicast address. Pure-managed; robust; bilingual.
/// </summary>
public sealed partial class MacToolsModule : Page
{
    public MacToolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "MAC Address Tools · MAC 位址工具";
            HeaderBlurb.Text = P(
                "Type a MAC address in any form to normalize it, check the unicast/multicast and universal/local bits, and look up the vendor. Or generate a fresh locally-administered one.",
                "任何格式打個 MAC 位址入嚟，即刻幫你轉換格式、睇單播/多播同全域/本地位元，再查廠商。又或者幫你隨機生成一個本地管理位址。");
            InputLabel.Text = P("MAC address", "MAC 位址");
            RandomBtn.Content = P("Generate random", "隨機生成");
            AnalysisTitle.Text = P("Analysis", "分析");
            FormatsTitle.Text = P("Formats (tap a row to copy)", "格式（撳一行即複製）");
            Analyze();
        }
        catch { }
    }

    private void Mac_Changed(object sender, TextChangedEventArgs e) => Analyze();

    private void Random_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var mac = MacToolsService.GenerateLocalUnicast();
            MacBox.Text = MacToolsService.ToColon(mac, false); // TextChanged → Analyze()
        }
        catch { Status(P("Could not generate an address.", "生成唔到位址。")); }
    }

    private void Analyze()
    {
        try
        {
            var raw = MacBox?.Text;
            if (string.IsNullOrWhiteSpace(raw))
            {
                ResultCard.Visibility = Visibility.Collapsed;
                Status(P("Enter a MAC address to begin.", "輸入一個 MAC 位址開始。"));
                return;
            }

            var mac = MacToolsService.Parse(raw);
            if (mac is null)
            {
                ResultCard.Visibility = Visibility.Collapsed;
                Status(P("Not a valid MAC address — need 12 hex digits (e.g. 00:1A:2B:3C:4D:5E).",
                    "唔係有效嘅 MAC 位址 — 要 12 個十六進位數字（例如 00:1A:2B:3C:4D:5E）。"));
                return;
            }

            // Analysis: I/G and U/L bits + vendor.
            string cast = MacToolsService.IsBroadcast(mac)
                ? P("Broadcast (all-ones)", "廣播（全一）")
                : MacToolsService.IsMulticast(mac)
                    ? P("Multicast (I/G bit set)", "多播（I/G 位元 = 1）")
                    : P("Unicast (I/G bit clear)", "單播（I/G 位元 = 0）");
            string admin = MacToolsService.IsLocallyAdministered(mac)
                ? P("Locally administered (U/L bit set)", "本地管理（U/L 位元 = 1）")
                : P("Universally administered (U/L bit clear)", "全域管理（U/L 位元 = 0）");
            string vendor = MacToolsService.LookupVendor(mac)
                ?? P("unknown", "未知");
            AnalysisText.Text =
                P("Delivery: ", "傳送：") + cast + "\n" +
                P("Administration: ", "管理：") + admin + "\n" +
                P("OUI vendor: ", "OUI 廠商：") + vendor;

            // Formats.
            FormatsPanel.Children.Clear();
            AddRow(P("Colon (lower)", "冒號（細楷）"), MacToolsService.ToColon(mac, false));
            AddRow(P("Colon (upper)", "冒號（大楷）"), MacToolsService.ToColon(mac, true));
            AddRow(P("Hyphen (lower)", "連字號（細楷）"), MacToolsService.ToHyphen(mac, false));
            AddRow(P("Hyphen (upper)", "連字號（大楷）"), MacToolsService.ToHyphen(mac, true));
            AddRow(P("Cisco dot (lower)", "Cisco 點（細楷）"), MacToolsService.ToDot(mac, false));
            AddRow(P("Cisco dot (upper)", "Cisco 點（大楷）"), MacToolsService.ToDot(mac, true));
            AddRow(P("Bare (lower)", "純值（細楷）"), MacToolsService.ToBare(mac, false));
            AddRow(P("Bare (upper)", "純值（大楷）"), MacToolsService.ToBare(mac, true));

            ResultCard.Visibility = Visibility.Visible;
            Status(P("Valid MAC address.", "有效嘅 MAC 位址。"));
        }
        catch
        {
            ResultCard.Visibility = Visibility.Collapsed;
            Status(P("Could not analyze that input.", "分析唔到呢個輸入。"));
        }
    }

    private void AddRow(string label, string value)
    {
        var btn = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Tag = value,
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var lbl = new TextBlock { Text = label, Opacity = 0.7 };
        var val = new TextBlock
        {
            Text = value,
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(lbl, 0);
        Grid.SetColumn(val, 1);
        grid.Children.Add(lbl);
        grid.Children.Add(val);
        btn.Content = grid;
        btn.Click += Row_Click;
        FormatsPanel.Children.Add(btn);
    }

    private void Row_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button { Tag: string text } && text.Length > 0)
            {
                var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
                dp.SetText(text);
                Clipboard.SetContent(dp);
                Status(P($"Copied {text}", $"已複製 {text}"));
            }
        }
        catch { Status(P("Could not access the clipboard.", "用唔到剪貼簿。")); }
    }

    private void Status(string text)
    {
        try { StatusText.Text = text; } catch { }
    }
}
