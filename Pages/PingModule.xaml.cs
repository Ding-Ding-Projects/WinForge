using System;
using System.Collections.ObjectModel;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Ping &amp; Traceroute · 網路測試 — managed ICMP echo (System.Net.NetworkInformation.Ping).
/// Ping tab loops SendPingAsync showing address / RTT / TTL / status with running min·avg·max and
/// packet-loss; traceroute walks TTL 1..N to map the path. Everything is async &amp; cancellable so the
/// UI never blocks; Stop and Unloaded cancel cleanly. Bilingual. No redirect.
/// </summary>
public sealed partial class PingModule : Page
{
    /// <summary>Row bound into the result ListViews.</summary>
    public sealed record Row(int Sequence, string Address, string Rtt, string Ttl, string Status);

    private readonly ObservableCollection<Row> _pingRows = new();
    private readonly ObservableCollection<Row> _traceRows = new();

    private CancellationTokenSource? _pingCts;
    private CancellationTokenSource? _traceCts;

    // Running ping stats.
    private long _min, _max, _sum;
    private int _sent, _received;

    public PingModule()
    {
        InitializeComponent();
        PingList.ItemsSource = _pingRows;
        TraceList.ItemsSource = _traceRows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => Render();
        Unloaded += OnUnloaded;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Unloaded -= OnUnloaded;
        CancelPing();
        CancelTrace();
    }

    private void Render()
    {
        Header.Title = "Ping & Traceroute · 網路測試";
        HeaderBlurb.Text = P("Check whether a host is reachable and how fast it replies, then trace the network path packets take to get there — all in pure managed code, no external tools.",
            "睇吓某個主機通唔通、覆得幾快，再追蹤封包去到目的地行過嘅網路路徑 — 全部純託管碼，唔使外部工具。");

        PingTab.Header = P("Ping", "Ping 測試");
        TraceTab.Header = P("Traceroute", "路由追蹤");

        PingHostLabel.Text = P("Host", "主機");
        PingCountLabel.Text = P("Count", "次數");
        TraceHostLabel.Text = P("Host", "主機");
        TraceHopsLabel.Text = P("Max hops", "最多躍點");

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        bool pinging = _pingCts is not null;
        PingStartBtn.Content = P("Start", "開始");
        PingStopBtn.Content = P("Stop", "停止");
        PingStartBtn.IsEnabled = !pinging;
        PingStopBtn.IsEnabled = pinging;

        bool tracing = _traceCts is not null;
        TraceStartBtn.Content = P("Start", "開始");
        TraceStopBtn.Content = P("Stop", "停止");
        TraceStartBtn.IsEnabled = !tracing;
        TraceStopBtn.IsEnabled = tracing;
    }

    // ---- Ping ----

    private async void PingStart_Click(object sender, RoutedEventArgs e)
    {
        string host = (PingHostBox.Text ?? "").Trim();
        if (host.Length == 0)
        {
            PingStatusText.Text = P("Enter a host name or IP address.", "請輸入主機名或者 IP 位址。");
            return;
        }

        CancelPing();
        _pingRows.Clear();
        _min = long.MaxValue; _max = 0; _sum = 0; _sent = 0; _received = 0;
        PingStatsText.Text = "";
        _pingCts = new CancellationTokenSource();
        var ct = _pingCts.Token;
        UpdateButtons();

        int count = (int)(double.IsNaN(PingCountBox.Value) ? 4 : PingCountBox.Value);
        if (count < 1) count = 1;

        string? resolved = await PingService.TryResolveAsync(host, ct);
        if (ct.IsCancellationRequested) { FinishPing(); return; }
        if (resolved is null)
        {
            PingStatusText.Text = P($"Could not resolve “{host}” — check the name or your connection.",
                $"解析唔到「{host}」 — 請檢查名稱或者你嘅網路連線。");
            FinishPing();
            return;
        }

        PingStatusText.Text = P($"Pinging {host} [{resolved}]…", $"正在 Ping {host} [{resolved}]…");

        try
        {
            for (int i = 1; i <= count && !ct.IsCancellationRequested; i++)
            {
                var probe = await PingService.PingOnceAsync(host, i, timeoutMs: 4000, ttl: null, ct: ct);
                if (ct.IsCancellationRequested) break;

                _sent++;
                string rtt;
                if (probe.Success)
                {
                    _received++;
                    _sum += probe.RoundtripMs;
                    if (probe.RoundtripMs < _min) _min = probe.RoundtripMs;
                    if (probe.RoundtripMs > _max) _max = probe.RoundtripMs;
                    rtt = $"{probe.RoundtripMs} ms";
                }
                else
                {
                    rtt = "—";
                }

                _pingRows.Add(new Row(probe.Sequence, probe.Address, rtt,
                    probe.Ttl > 0 ? probe.Ttl.ToString() : "—", DescribeStatus(probe.Status)));
                UpdatePingStats();

                if (i < count && !ct.IsCancellationRequested)
                {
                    try { await Task.Delay(1000, ct); } catch (OperationCanceledException) { break; }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PingStatusText.Text = P($"Ping failed: {ex.Message}", $"Ping 失敗：{ex.Message}");
        }

        FinishPing();
    }

    private void UpdatePingStats()
    {
        int lost = _sent - _received;
        double lossPct = _sent > 0 ? lost * 100.0 / _sent : 0;
        if (_received > 0)
        {
            long avg = _sum / _received;
            PingStatsText.Text = P(
                $"Sent {_sent}, received {_received}, lost {lost} ({lossPct:0.#}% loss) — min {_min} ms / avg {avg} ms / max {_max} ms",
                $"送出 {_sent}、收到 {_received}、遺失 {lost}（丟包 {lossPct:0.#}%）— 最低 {_min} ms／平均 {avg} ms／最高 {_max} ms");
        }
        else
        {
            PingStatsText.Text = P(
                $"Sent {_sent}, received 0, lost {lost} ({lossPct:0.#}% loss)",
                $"送出 {_sent}、收到 0、遺失 {lost}（丟包 {lossPct:0.#}%）");
        }
    }

    private void PingStop_Click(object sender, RoutedEventArgs e) => CancelPing();

    private void CancelPing()
    {
        if (_pingCts is null) return;
        try { _pingCts.Cancel(); } catch { }
        _pingCts.Dispose();
        _pingCts = null;
        UpdateButtons();
    }

    private void FinishPing()
    {
        if (_pingCts is not null)
        {
            _pingCts.Dispose();
            _pingCts = null;
        }
        UpdateButtons();
    }

    // ---- Traceroute ----

    private async void TraceStart_Click(object sender, RoutedEventArgs e)
    {
        string host = (TraceHostBox.Text ?? "").Trim();
        if (host.Length == 0)
        {
            TraceStatusText.Text = P("Enter a host name or IP address.", "請輸入主機名或者 IP 位址。");
            return;
        }

        CancelTrace();
        _traceRows.Clear();
        _traceCts = new CancellationTokenSource();
        var ct = _traceCts.Token;
        UpdateButtons();

        int maxHops = (int)(double.IsNaN(TraceHopsBox.Value) ? 30 : TraceHopsBox.Value);
        if (maxHops < 1) maxHops = 1;

        string? resolved = await PingService.TryResolveAsync(host, ct);
        if (ct.IsCancellationRequested) { FinishTrace(); return; }
        if (resolved is null)
        {
            TraceStatusText.Text = P($"Could not resolve “{host}” — check the name or your connection.",
                $"解析唔到「{host}」 — 請檢查名稱或者你嘅網路連線。");
            FinishTrace();
            return;
        }

        TraceStatusText.Text = P($"Tracing route to {host} [{resolved}], up to {maxHops} hops…",
            $"正在追蹤到 {host} [{resolved}] 嘅路由，最多 {maxHops} 個躍點…");

        try
        {
            for (int ttl = 1; ttl <= maxHops && !ct.IsCancellationRequested; ttl++)
            {
                var probe = await PingService.PingOnceAsync(host, ttl, timeoutMs: 2500, ttl: ttl, ct: ct);
                if (ct.IsCancellationRequested) break;

                // TtlExpired = an intermediate router responded; Success = we reached the destination.
                string addr = probe.Address;
                string rtt = probe.RoundtripMs >= 0 && probe.Status is IPStatus.Success or IPStatus.TtlExpired
                    ? $"{probe.RoundtripMs} ms" : "*";
                if (probe.Status == IPStatus.TimedOut) { addr = "*"; rtt = "*"; }

                _traceRows.Add(new Row(ttl, addr, rtt, "", DescribeStatus(probe.Status)));

                if (probe.Status == IPStatus.Success)
                {
                    TraceStatusText.Text = P($"Reached {host} [{resolved}] in {ttl} hop(s).",
                        $"經過 {ttl} 個躍點到達 {host} [{resolved}]。");
                    break;
                }
            }

            if (!ct.IsCancellationRequested && (_traceRows.Count == 0 || _traceRows.Count >= maxHops))
            {
                TraceStatusText.Text = P($"Stopped after {maxHops} hops without reaching {host}.",
                    $"行咗 {maxHops} 個躍點都未到 {host}，已停止。");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            TraceStatusText.Text = P($"Traceroute failed: {ex.Message}", $"路由追蹤失敗：{ex.Message}");
        }

        FinishTrace();
    }

    private void TraceStop_Click(object sender, RoutedEventArgs e) => CancelTrace();

    private void CancelTrace()
    {
        if (_traceCts is null) return;
        try { _traceCts.Cancel(); } catch { }
        _traceCts.Dispose();
        _traceCts = null;
        UpdateButtons();
    }

    private void FinishTrace()
    {
        if (_traceCts is not null)
        {
            _traceCts.Dispose();
            _traceCts = null;
        }
        UpdateButtons();
    }

    private string DescribeStatus(IPStatus status) => status switch
    {
        IPStatus.Success => P("Reply", "回覆"),
        IPStatus.TtlExpired => P("Hop (TTL expired)", "躍點（TTL 到期）"),
        IPStatus.TimedOut => P("Request timed out", "要求逾時"),
        IPStatus.DestinationHostUnreachable => P("Host unreachable", "主機無法到達"),
        IPStatus.DestinationNetworkUnreachable => P("Network unreachable", "網路無法到達"),
        IPStatus.DestinationUnreachable => P("Unreachable", "無法到達"),
        IPStatus.BadDestination => P("Bad destination", "無效目的地"),
        _ => status.ToString(),
    };
}
