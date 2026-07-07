using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// IP 同網絡資訊 · IP &amp; network info — lists operational local adapters (name, MAC, IPv4/IPv6,
/// gateways, DNS, link speed) and looks up the public IP over HTTPS (api.ipify.org). The public-IP
/// lookup makes an outbound network request; every path is guarded and never throws. Bilingual.
/// </summary>
public sealed partial class IpInfoModule : Page
{
    private readonly ObservableCollection<IpInfoService.AdapterInfo> _adapters = new();
    private string? _publicIp;
    private CancellationTokenSource? _cts;

    public IpInfoModule()
    {
        InitializeComponent();
        AdapterList.ItemsSource = _adapters;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => { Render(); _ = RefreshAsync(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Unloaded -= OnUnloaded;
        try { _cts?.Cancel(); } catch { }
    }

    private void Render()
    {
        Header.Title = "IP & Network Info · IP 同網絡資訊";
        HeaderBlurb.Text = P("See your public IP and every active network adapter — MAC address, IPv4/IPv6, gateway, DNS servers and link speed. All read locally; the public-IP lookup makes one web request.",
            "睇下你嘅公開 IP 同每個運作緊嘅網絡卡 — MAC 位址、IPv4/IPv6、閘道、DNS 伺服器同連線速度。全部喺本機讀取；公開 IP 查詢會做一次網絡請求。");
        RefreshBtn.Content = P("Refresh", "重新整理");
        PublicTitle.Text = P("Public IP address", "公開 IP 位址");
        CopyBtn.Content = P("Copy", "複製");
        AdaptersTitle.Text = P("Active network adapters", "運作緊嘅網絡卡");
        UpdatePublicText();
        // Re-read adapters so their embedded labels track the current language.
        LoadAdapters();
    }

    private void UpdatePublicText()
    {
        PublicIpText.Text = _publicIp is { Length: > 0 }
            ? _publicIp
            : P("Not available (offline, or the lookup failed).", "無法取得（離線，或者查詢失敗）。");
    }

    private void LoadAdapters()
    {
        try
        {
            var list = IpInfoService.GetAdapters();
            _adapters.Clear();
            foreach (var a in list) _adapters.Add(a);
            AdaptersStatus.Text = list.Count == 0
                ? P("No active adapters found.", "搵唔到運作緊嘅網絡卡。")
                : P($"{list.Count} adapter(s) up.", $"{list.Count} 個網絡卡運作緊。");
        }
        catch
        {
            AdaptersStatus.Text = P("Could not read network adapters.", "讀唔到網絡卡資料。");
        }
    }

    private async Task RefreshAsync()
    {
        LoadAdapters();

        try { _cts?.Cancel(); } catch { }
        var cts = new CancellationTokenSource();
        _cts = cts;

        PublicIpText.Text = P("Looking up…", "查詢緊…");
        try
        {
            string? ip = await IpInfoService.GetPublicIpAsync(cts.Token);
            if (cts.IsCancellationRequested) return;
            _publicIp = ip;
        }
        catch
        {
            _publicIp = null;
        }
        UpdatePublicText();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        try { await RefreshAsync(); } catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrEmpty(_publicIp))
            {
                AdaptersStatus.Text = P("No public IP to copy yet.", "仲未有公開 IP 可以複製。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(_publicIp);
            Clipboard.SetContent(dp);
            AdaptersStatus.Text = P("Public IP copied to clipboard.", "公開 IP 已複製到剪貼簿。");
        }
        catch
        {
            AdaptersStatus.Text = P("Could not access the clipboard.", "用唔到剪貼簿。");
        }
    }
}
