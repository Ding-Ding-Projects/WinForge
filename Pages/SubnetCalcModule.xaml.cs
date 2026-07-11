using System;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// IPv4 子網計算器 · IPv4 subnet calculator. Enter an IP with a CIDR prefix OR a dotted subnet mask
/// (kept in sync) and see the network/broadcast/host details; split a network into equal subnets.
/// Pure managed uint bit math. Never throws — bad input becomes a bilingual status line.
/// </summary>
public sealed partial class SubnetCalcModule : Page
{
    private bool _suppress;

    public SubnetCalcModule()
    {
        InitializeComponent();
        // Preserve the CIDR/split defaults without triggering the CidrBox
        // synchronization while the page is still constructing.
        _suppress = true;
        CidrBox.Value = 24;
        NewPrefixBox.Value = 26;
        CountBox.Value = 0;
        _suppress = false;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += (_, _) => Render();
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = P("Subnet Calculator · 子網計算器", "子網計算器 · Subnet Calculator");
        HeaderBlurb.Text = P("Work out the network, broadcast, mask, host range and class for any IPv4 address — then split a network into equal subnets. All local math, nothing leaves your PC.",
            "計出任何 IPv4 位址嘅網絡、廣播、遮罩、主機範圍同類別，再將一個網絡拆成等份子網。全部喺本機計，冇嘢會離開你部電腦。");
        InputTitle.Text = P("Address & mask", "位址同遮罩");
        IpLabel.Text = P("IPv4 address", "IPv4 位址");
        CidrLabel.Text = P("Prefix (CIDR)", "前綴（CIDR）");
        MaskLabel.Text = P("Subnet mask", "子網遮罩");
        ResultTitle.Text = P("Results", "計算結果");
        SplitTitle.Text = P("Split into equal subnets", "拆成等份子網");
        NewPrefixLabel.Text = P("New prefix", "新前綴");
        CountLabel.Text = P("or subnet count (0 = use prefix)", "或子網數目（0 = 用前綴）");
        SplitBtn.Content = P("Split", "拆分");
        try { Compute(); } catch { /* never throw from UI */ }
    }

    // --- input sync ---------------------------------------------------------

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Compute();
    }

    private void Cidr_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        int prefix = ClampPrefix(CidrBox.Value);
        _suppress = true;
        MaskBox.Text = SubnetCalcService.ToDotted(SubnetCalcService.MaskFromPrefix(prefix));
        _suppress = false;
        Compute();
    }

    private void Mask_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        if (SubnetCalcService.TryParseIPv4(MaskBox.Text, out uint mask))
        {
            int prefix = SubnetCalcService.PrefixFromMask(mask);
            if (prefix >= 0)
            {
                _suppress = true;
                CidrBox.Value = prefix;
                _suppress = false;
            }
        }
        Compute();
    }

    private static int ClampPrefix(double v)
    {
        if (double.IsNaN(v)) return 0;
        int i = (int)Math.Round(v);
        return i < 0 ? 0 : i > 32 ? 32 : i;
    }

    // --- compute ------------------------------------------------------------

    private int CurrentPrefix()
    {
        if (SubnetCalcService.TryParseIPv4(MaskBox.Text, out uint mask))
        {
            int p = SubnetCalcService.PrefixFromMask(mask);
            if (p >= 0) return p;
        }
        return ClampPrefix(CidrBox.Value);
    }

    private void Compute()
    {
        if (!SubnetCalcService.TryParseIPv4(IpBox.Text, out uint ip))
        {
            StatusText.Text = P("Enter a valid IPv4 address, e.g. 192.168.1.10.", "請輸入有效嘅 IPv4 位址，例如 192.168.1.10。");
            ResultBox.Text = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(MaskBox.Text) &&
            SubnetCalcService.TryParseIPv4(MaskBox.Text, out uint m) &&
            SubnetCalcService.PrefixFromMask(m) < 0)
        {
            StatusText.Text = P("That subnet mask is not contiguous (must be 1s then 0s).", "呢個子網遮罩唔連續（要先 1 後 0）。");
            ResultBox.Text = string.Empty;
            return;
        }

        int prefix = CurrentPrefix();
        var r = SubnetCalcService.Compute(ip, prefix);
        if (r is null)
        {
            StatusText.Text = P("Prefix must be between 0 and 32.", "前綴要喺 0 到 32 之間。");
            ResultBox.Text = string.Empty;
            return;
        }

        StatusText.Text = P($"OK — {SubnetCalcService.ToDotted(ip)}/{prefix}", $"完成 — {SubnetCalcService.ToDotted(ip)}/{prefix}");

        string D(uint v) => SubnetCalcService.ToDotted(v);
        string priv = r.IsPrivate ? P("private (RFC1918)", "私有（RFC1918）") : P("public", "公用");

        var sb = new StringBuilder();
        void Row(string en, string zh, string val) => sb.AppendLine($"{P(en, zh),-28}{val}");

        Row("Network address", "網絡位址", $"{D(r.Network)}/{r.Prefix}");
        Row("Broadcast address", "廣播位址", D(r.Broadcast));
        Row("Subnet mask", "子網遮罩", D(r.Mask));
        Row("Wildcard mask", "萬用字元遮罩", D(r.Wildcard));
        if (r.Prefix <= 30)
        {
            Row("First usable host", "第一個可用主機", D(r.FirstHost));
            Row("Last usable host", "最後可用主機", D(r.LastHost));
        }
        else
        {
            Row("Host range", "主機範圍", $"{D(r.FirstHost)} – {D(r.LastHost)}");
        }
        Row("Total addresses", "位址總數", r.TotalAddresses.ToString());
        Row("Usable hosts", "可用主機數", r.UsableHosts.ToString());
        Row("IP class", "IP 類別", $"{r.Class}  ({priv})");

        ResultBox.Text = sb.ToString().TrimEnd();
    }

    // --- split --------------------------------------------------------------

    private void Split_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!SubnetCalcService.TryParseIPv4(IpBox.Text, out uint ip))
            {
                SplitBox.Text = P("Enter a valid IPv4 address first.", "請先輸入有效嘅 IPv4 位址。");
                return;
            }
            int current = CurrentPrefix();

            int count = (int)(double.IsNaN(CountBox.Value) ? 0 : CountBox.Value);
            int newPrefix = count > 0
                ? SubnetCalcService.PrefixForCount(current, count)
                : ClampPrefix(NewPrefixBox.Value);

            if (newPrefix < current)
            {
                SplitBox.Text = P($"New prefix (/{newPrefix}) must be larger than the base prefix (/{current}).",
                    $"新前綴（/{newPrefix}）要大過原本前綴（/{current}）。");
                return;
            }

            uint baseNet = ip & SubnetCalcService.MaskFromPrefix(current);
            const int cap = 256;
            var subs = SubnetCalcService.Split(baseNet, current, newPrefix, cap);
            if (subs.Count == 0)
            {
                SplitBox.Text = P("Nothing to split — check the prefixes.", "冇嘢可以拆 — 檢查下前綴。");
                return;
            }

            long totalWanted = 1L << (newPrefix - current);
            var sb = new StringBuilder();
            sb.AppendLine(P($"{SubnetCalcService.ToDotted(baseNet)}/{current} → {totalWanted} × /{newPrefix} subnets:",
                $"{SubnetCalcService.ToDotted(baseNet)}/{current} → {totalWanted} 個 /{newPrefix} 子網："));
            sb.AppendLine();
            foreach (var s in subs)
                sb.AppendLine($"{s.Index,4}.  {SubnetCalcService.ToDotted(s.Network)}/{s.Prefix}   {SubnetCalcService.ToDotted(s.Mask)}");
            if (totalWanted > subs.Count)
                sb.AppendLine(P($"… showing first {subs.Count} of {totalWanted}.", $"… 只顯示頭 {subs.Count} 個（共 {totalWanted} 個）。"));

            SplitBox.Text = sb.ToString().TrimEnd();
        }
        catch
        {
            SplitBox.Text = P("Could not split — check your inputs.", "拆唔到 — 檢查下輸入。");
        }
    }
}
