using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// TCP 連接埠掃描器 · Async TCP port scanner. Probe a host across a port range with a bounded
/// pool of connections and a per-port timeout; open ports (with well-known service names) stream
/// into a ListView with live progress. Pure managed sockets, no external tools. Bilingual.
/// Only scan hosts you own or are authorised to test.
/// </summary>
public sealed partial class PortScanModule : Page
{
    private const int MaxConcurrency = 100;

    private readonly ObservableCollection<PortScanService.OpenPort> _open = new();
    private CancellationTokenSource? _cts;
    private int _total;
    private int _scanned;
    private int _openCount;

    public PortScanModule()
    {
        InitializeComponent();
        OpenList.ItemsSource = _open;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += OnUnloaded;
        Loaded += (_, _) => Render();
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
        Header.Title = "Port Scanner · 連接埠掃描";
        HeaderBlurb.Text = P("Check which TCP ports are open on a host — a quick reachability and service check for machines you administer.",
            "睇下一部主機邊啲 TCP 連接埠開住 — 為你自己管理嘅機器做快速連通同服務檢查。");
        EthicsBar.Title = P("Scan responsibly", "負責任咁掃描");
        EthicsBar.Message = P("Only scan hosts you own or are explicitly authorised to test. Unsolicited scanning of others' systems may be illegal.",
            "只可以掃描你擁有或者獲明確授權嘅主機。未經同意去掃描人哋嘅系統可能違法。");
        HostLabel.Text = P("Host or IP address", "主機或 IP 位址");
        StartLabel.Text = P("Start port", "起始連接埠");
        EndLabel.Text = P("End port", "結束連接埠");
        TimeoutLabel.Text = P("Per-port timeout (ms)", "每個連接埠逾時（毫秒）");
        StartBtn.Content = P("Start scan", "開始掃描");
        StopBtn.Content = P("Stop", "停止");
        OpenTitle.Text = P("Open ports", "開啟嘅連接埠");
        UpdateStatus();
        UpdateEmpty();
    }

    private void UpdateEmpty()
    {
        bool has = _open.Count > 0;
        OpenList.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        EmptyText.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        EmptyText.Text = _cts != null
            ? P("Scanning…", "掃描中…")
            : P("No open ports found yet. Run a scan to begin.", "暫時未搵到開啟嘅連接埠。開始掃描啦。");
    }

    private void UpdateStatus(TimeSpan? elapsed = null)
    {
        if (_total <= 0)
        {
            StatusText.Text = P("Idle.", "閒置中。");
            Progress.Value = 0;
            return;
        }
        Progress.Value = (double)_scanned / _total;
        string el = elapsed is { } t ? $" · {t.TotalSeconds:0.0}s" : "";
        StatusText.Text = P($"Scanned {_scanned}/{_total} · {_openCount} open{el}",
            $"已掃描 {_scanned}/{_total} · {_openCount} 個開啟{el}");
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_cts != null) return;

        string host = (HostBox.Text ?? "").Trim();
        if (host.Length == 0)
        {
            StatusText.Text = P("Enter a host or IP address first.", "請先輸入主機或 IP 位址。");
            return;
        }

        int start = ToInt(StartBox.Value, 1);
        int end = ToInt(EndBox.Value, 1024);
        int timeout = ToInt(TimeoutBox.Value, 300);
        if (end < start) (start, end) = (end, start);

        _open.Clear();
        _scanned = 0;
        _openCount = 0;
        _total = end - start + 1;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        HostBox.IsEnabled = StartBox.IsEnabled = EndBox.IsEnabled = TimeoutBox.IsEnabled = false;
        UpdateStatus();
        UpdateEmpty();

        var sw = Stopwatch.StartNew();
        var dq = DispatcherQueue;

        void OnProgress(int _)
        {
            int done = Interlocked.Increment(ref _scanned);
            dq.TryEnqueue(() => UpdateStatus(sw.Elapsed));
        }

        void OnOpen(PortScanService.OpenPort port)
        {
            Interlocked.Increment(ref _openCount);
            dq.TryEnqueue(() =>
            {
                _open.Add(port);
                UpdateEmpty();
            });
        }

        try
        {
            await PortScanService.ScanAsync(host, start, end, timeout, MaxConcurrency, OnProgress, OnOpen, ct);
            sw.Stop();
            StatusText.Text = P($"Done — {_openCount} open of {_total} scanned · {sw.Elapsed.TotalSeconds:0.0}s",
                $"完成 — 掃描 {_total} 個當中 {_openCount} 個開啟 · {sw.Elapsed.TotalSeconds:0.0}秒");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            StatusText.Text = P($"Stopped — {_scanned}/{_total} scanned, {_openCount} open.",
                $"已停止 — 掃描咗 {_scanned}/{_total}，{_openCount} 個開啟。");
        }
        catch (SocketException)
        {
            sw.Stop();
            StatusText.Text = P($"Could not resolve or reach \"{host}\". Check the host name.",
                $"無法解析或連接「{host}」。請檢查主機名稱。");
        }
        catch (Exception ex)
        {
            sw.Stop();
            StatusText.Text = P($"Scan failed: {ex.Message}", $"掃描失敗：{ex.Message}");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            StartBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
            HostBox.IsEnabled = StartBox.IsEnabled = EndBox.IsEnabled = TimeoutBox.IsEnabled = true;
            UpdateEmpty();
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        StopBtn.IsEnabled = false;
    }

    private static int ToInt(double v, int fallback) => double.IsNaN(v) ? fallback : (int)Math.Round(v);
}
