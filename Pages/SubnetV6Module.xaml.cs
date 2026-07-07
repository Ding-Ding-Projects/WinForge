using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// IPv6 位址與子網工具 · IPv6 address &amp; subnet tools — expand/compress an address, classify its
/// type, compute a prefix's network/mask/count/first-last, and derive an EUI-64 interface id from a
/// MAC. Pure managed (IPAddress / BigInteger). Bilingual, never-throws. No redirect.
/// </summary>
public sealed partial class SubnetV6Module : Page
{
    public SubnetV6Module()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Recompute();
        RecomputeEui();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        Recompute();
        RecomputeEui();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "IPv6 Tools · IPv6 工具";
        HeaderBlurb.Text = P(
            "Expand or compress any IPv6 address, see its type, work out a prefix's network, mask, size and first/last address — and turn a MAC into an EUI-64 interface id. All computed locally.",
            "展開或壓縮任何 IPv6 位址、睇佢類型、計出某個前綴嘅網路、遮罩、大細同首尾位址 —— 仲可以將 MAC 轉做 EUI-64 介面識別碼。全部喺本機計，唔會上網。");

        AddrLabel.Text = P("IPv6 address (an optional /prefix is accepted)", "IPv6 位址（可以加 /前綴）");
        PrefixLabel.Text = P("Prefix length (0–128)", "前綴長度（0–128）");
        ResultsTitle.Text = P("Address", "位址");
        ExpandedLabel.Text = P("Fully expanded", "完整展開");
        CompressedLabel.Text = P("Compressed (canonical)", "壓縮（標準）");
        TypeLabel.Text = P("Type", "類型");

        PrefixTitle.Text = P("Prefix / subnet", "前綴 / 子網");
        NetLabel.Text = P("Network prefix", "網路前綴");
        MaskLabel.Text = P("Prefix mask", "前綴遮罩");
        CountLabel.Text = P("Addresses in block", "區塊內位址數");
        FirstLabel.Text = P("First address", "第一個位址");
        LastLabel.Text = P("Last address", "最後一個位址");

        EuiTitle.Text = P("EUI-64 helper", "EUI-64 小工具");
        EuiBlurb.Text = P("Enter a 48-bit MAC to get the EUI-64 interface identifier (ff:fe inserted, U/L bit flipped).",
            "輸入 48-bit MAC，計出 EUI-64 介面識別碼（中間加 ff:fe、翻轉 U/L 位）。");
        EuiLabel.Text = P("EUI-64 interface id", "EUI-64 介面識別碼");
    }

    // -------- address + prefix --------

    private void Addr_Changed(object sender, TextChangedEventArgs e) => Recompute();

    private void Prefix_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => Recompute();

    private void Recompute()
    {
        // Loaded may not have run yet during initial InitializeComponent wiring.
        if (StatusText is null) return;

        try
        {
            var parsed = SubnetV6Service.Parse(AddrBox?.Text);
            if (!parsed.Ok || parsed.Address is null)
            {
                ResultsCard.Visibility = Visibility.Collapsed;
                PrefixCard.Visibility = Visibility.Collapsed;
                StatusText.Text = parsed.Error switch
                {
                    "empty" => P("Enter an IPv6 address above to begin.", "喺上面輸入一個 IPv6 位址開始。"),
                    "badprefix" => P("Prefix must be a whole number 0–128.", "前綴要係 0–128 嘅整數。"),
                    "notv6" => P("That's a valid IP, but not IPv6.", "呢個係有效 IP，但唔係 IPv6。"),
                    _ => P("Not a valid IPv6 address.", "唔係有效嘅 IPv6 位址。"),
                };
                return;
            }

            var addr = parsed.Address;
            ExpandedVal.Text = SubnetV6Service.Expand(addr);
            CompressedVal.Text = SubnetV6Service.Compress(addr);
            var (tEn, tZh) = SubnetV6Service.Classify(addr);
            TypeVal.Text = P(tEn, tZh);
            ResultsCard.Visibility = Visibility.Visible;

            // If the input carried an inline prefix, mirror it into the NumberBox.
            if (parsed.Prefix is int inlinePfx && (int)PrefixBox.Value != inlinePfx)
            {
                PrefixBox.Value = inlinePfx;
                return; // ValueChanged will re-enter Recompute with the synced value
            }

            int prefix = double.IsNaN(PrefixBox.Value) ? 64 : (int)PrefixBox.Value;
            var pr = SubnetV6Service.ComputePrefix(addr, prefix);
            if (!pr.Ok)
            {
                PrefixCard.Visibility = Visibility.Collapsed;
                StatusText.Text = P("Address parsed; prefix could not be computed.", "位址解析成功；前綴計唔到。");
                return;
            }

            NetVal.Text = pr.NetworkCompressed + "/" + prefix;
            MaskVal.Text = pr.MaskCompressed;
            CountVal.Text = pr.CountPow + "  =  " + pr.CountBig;
            FirstVal.Text = pr.FirstAddress;
            LastVal.Text = pr.LastAddress;
            PrefixCard.Visibility = Visibility.Visible;

            StatusText.Text = P("Parsed OK.", "解析成功。");
        }
        catch
        {
            ResultsCard.Visibility = Visibility.Collapsed;
            PrefixCard.Visibility = Visibility.Collapsed;
            StatusText.Text = P("Could not process that input.", "處理唔到呢個輸入。");
        }
    }

    // -------- EUI-64 --------

    private void Mac_Changed(object sender, TextChangedEventArgs e) => RecomputeEui();

    private void RecomputeEui()
    {
        if (EuiStatus is null) return;
        try
        {
            var text = MacBox?.Text ?? "";
            if (text.Trim().Length == 0)
            {
                EuiVal.Text = "";
                EuiStatus.Text = P("Enter a MAC like 00:1A:2B:3C:4D:5E.", "輸入一個 MAC，例如 00:1A:2B:3C:4D:5E。");
                return;
            }

            var eui = SubnetV6Service.MacToEui64(text);
            if (eui is null)
            {
                EuiVal.Text = "";
                EuiStatus.Text = P("Need 12 hex digits (a 48-bit MAC).", "要 12 個十六進位數字（48-bit MAC）。");
                return;
            }

            EuiVal.Text = eui;
            EuiStatus.Text = P("Interface id ready.", "介面識別碼準備好。");
        }
        catch
        {
            EuiVal.Text = "";
            EuiStatus.Text = P("Could not process that MAC.", "處理唔到呢個 MAC。");
        }
    }

    // -------- clipboard --------

    private void CopyText(string? text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return;
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
        }
        catch { /* never throw from a copy button */ }
    }

    private void Copy_Expanded(object sender, RoutedEventArgs e) => CopyText(ExpandedVal.Text);
    private void Copy_Compressed(object sender, RoutedEventArgs e) => CopyText(CompressedVal.Text);
    private void Copy_Net(object sender, RoutedEventArgs e) => CopyText(NetVal.Text);
    private void Copy_Count(object sender, RoutedEventArgs e) => CopyText(CountVal.Text);
    private void Copy_First(object sender, RoutedEventArgs e) => CopyText(FirstVal.Text);
    private void Copy_Last(object sender, RoutedEventArgs e) => CopyText(LastVal.Text);
    private void Copy_Eui(object sender, RoutedEventArgs e) => CopyText(EuiVal.Text);
}
