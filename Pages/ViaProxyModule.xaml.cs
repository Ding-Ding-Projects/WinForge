using System;
using System.Diagnostics;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// ViaProxy（Minecraft 版本代理）· Wraps the ViaProxy Java jar so a single Minecraft client can join servers
/// of almost any version. Detect/install Java, download the jar from GitHub releases, fill a config form
/// (target server, version, auth, bind port, upstream proxy…), start/stop the headless proxy and watch its
/// live logs. No external redirect; the Swing GUI is never launched — WinForge drives it via the cli mode.
/// </summary>
public sealed partial class ViaProxyModule : Page
{
    private readonly StringBuilder _log = new();

    public ViaProxyModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { BuildVersionItems(); BuildAuthItems(); Render(); LoadForm(); RefreshEngine(); RefreshRunState(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "ViaProxy · Minecraft 版本代理";
        HeaderBlurb.Text = P("Run the ViaProxy Java proxy so one Minecraft client can join servers of almost any version (and vice-versa). Detect Java, download the jar, set your target server + version, start the proxy and connect Minecraft to the local address.",
            "行 ViaProxy Java 代理，等一個 Minecraft 客戶端可以連去差唔多任何版本嘅伺服器（反之亦然）。偵測 Java、下載 jar、設定目標伺服器同版本、啟動代理，再用 Minecraft 連去本機位址。");

        DownloadText.Text = P("Download latest jar", "下載最新 jar");
        PickJarBtn.Content = P("Use custom jar…", "用自訂 jar…");
        SourceBtn.Content = P("Source · GPL-3.0", "原始碼 · GPL-3.0");

        ConfigHeader.Text = P("Target server", "目標伺服器");
        TargetHostBox.Header = P("Server host", "伺服器主機");
        TargetHostBox.PlaceholderText = "play.example.net";
        TargetPortBox.Header = P("Server port", "伺服器埠");
        VersionBox.Header = P("Target MC version", "目標 MC 版本");
        AuthBox.Header = P("Auth method", "認證方法");
        OnlineModeToggle.Header = P("Proxy online mode (skins / signed chat)", "代理線上模式（顯示面板／簽署聊天）");
        OnlineModeToggle.OnContent = P("On", "開");
        OnlineModeToggle.OffContent = P("Off", "關");

        AdvancedHeader.Text = P("Advanced", "進階");
        BindHostBox.Header = P("Bind host", "綁定主機");
        BindPortBox.Header = P("Bind port (Minecraft connects here)", "綁定埠（Minecraft 連呢度）");
        BackendProxyBox.Header = P("Upstream proxy URL (optional)", "上游代理 URL（可選）");
        BackendProxyBox.PlaceholderText = "socks5://user:pass@host:1080";
        AllowBetaPingToggle.Header = P("Allow pinging very old servers (≤ b1.7.3)", "允許 ping 好舊嘅伺服器（≤ b1.7.3）");
        AllowBetaPingToggle.OnContent = P("On", "開"); AllowBetaPingToggle.OffContent = P("Off", "關");
        BetacraftToggle.Header = P("BetaCraft auth (classic online servers)", "BetaCraft 認證（classic 線上伺服器）");
        BetacraftToggle.OnContent = P("On", "開"); BetacraftToggle.OffContent = P("Off", "關");

        StartBtn.Content = P("Start proxy", "啟動代理");
        StopBtn.Content = P("Stop", "停止");

        LogHeader.Text = P("Live log", "即時記錄");
        ClearLogBtn.Content = P("Clear", "清除");
        CopyLogBtn.Content = P("Copy", "複製");
        CopyAddrBtn.Content = P("Copy local address", "複製本機位址");

        RefreshRunState();
    }

    private void BuildVersionItems()
    {
        VersionBox.Items.Clear();
        foreach (var v in ViaProxyService.CommonVersions) VersionBox.Items.Add(v);
        VersionBox.SelectedIndex = 0;
    }

    private void BuildAuthItems()
    {
        AuthBox.Items.Clear();
        AuthBox.Items.Add(new ComboBoxItem { Content = P("None (offline)", "無（離線）"), Tag = "none" });
        AuthBox.Items.Add(new ComboBoxItem { Content = P("Account (configured in GUI)", "帳戶（喺 GUI 設定）"), Tag = "account" });
        AuthBox.SelectedIndex = 0;
    }

    // ── Engine bars ──────────────────────────────────────────────────────────

    private void RefreshEngine()
    {
        var hasJava = ViaProxyService.HasJava();
        JavaBar.IsOpen = true;
        JavaBar.Severity = hasJava ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        JavaBar.Title = hasJava ? P("Java found", "搵到 Java") : P("Java not found", "搵唔到 Java");
        JavaBar.Message = hasJava
            ? P("A JDK is available to run ViaProxy.", "已有 JDK 可以行 ViaProxy。")
            : P("ViaProxy needs a JDK (21+). Install one with one click.", "ViaProxy 需要 JDK（21+）。一鍵安裝。");
        JavaBar.ActionButton = hasJava ? null : EngineBars.AutoInstallButton(
            "Microsoft.OpenJDK.21",
            "Install JDK 21", "安裝 JDK 21",
            async () => { RefreshEngine(); await System.Threading.Tasks.Task.CompletedTask; });

        var jar = ViaProxyService.FindJar();
        JarBar.IsOpen = true;
        JarBar.Severity = jar is not null ? InfoBarSeverity.Success : InfoBarSeverity.Informational;
        JarBar.Title = jar is not null ? P("ViaProxy.jar ready", "ViaProxy.jar 已備妥") : P("ViaProxy.jar not downloaded", "未下載 ViaProxy.jar");
        JarBar.Message = jar is not null
            ? System.IO.Path.GetFileName(jar)
            : P("Click \"Download latest jar\" to fetch it from GitHub (GPL-3.0, on-demand).", "撳「下載最新 jar」由 GitHub 攞返嚟（GPL-3.0，即時下載）。");

        StartBtn.IsEnabled = hasJava && jar is not null && !ViaProxyService.IsRunning;
    }

    // ── Toolbar actions ──────────────────────────────────────────────────────

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        DownloadBtn.IsEnabled = false;
        Busy.IsActive = true;
        AppendLog(P("Downloading ViaProxy.jar…", "下載緊 ViaProxy.jar…"));
        var (ok, jar, log) = await ViaProxyService.DownloadLatestJar(
            s => DispatcherQueue.TryEnqueue(() => AppendLog(s)));
        Busy.IsActive = false;
        DownloadBtn.IsEnabled = true;
        AppendLog(log);
        Notify(StatusBar, ok ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            ok ? P("Download complete", "下載完成") : P("Download failed", "下載失敗"), log);
        RefreshEngine();
    }

    private async void PickJar_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".jar");
        if (string.IsNullOrWhiteSpace(path)) return;
        ViaProxyService.SetJar(path);
        AppendLog(P("Using custom jar: ", "用自訂 jar：") + path);
        RefreshEngine();
    }

    private void Source_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ViaProxyService.SourceUrl, UseShellExecute = true });
        }
        catch { }
    }

    // ── Start / Stop ─────────────────────────────────────────────────────────

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        var jar = ViaProxyService.FindJar();
        if (jar is null) { Notify(StatusBar, InfoBarSeverity.Warning, P("No jar", "冇 jar"), P("Download ViaProxy.jar first.", "請先下載 ViaProxy.jar。")); return; }

        var opt = ReadForm();
        if (string.IsNullOrWhiteSpace(opt.TargetHost))
        {
            Notify(StatusBar, InfoBarSeverity.Warning, P("Target required", "要填目標"), P("Enter the target server host.", "請輸入目標伺服器主機。"));
            return;
        }
        ViaProxyService.SaveConfig(opt);

        var r = ViaProxyService.Start(jar, opt,
            line => DispatcherQueue.TryEnqueue(() => AppendLog(line)),
            () => DispatcherQueue.TryEnqueue(() => { AppendLog(P("[ViaProxy stopped]", "[ViaProxy 已停止]")); RefreshRunState(); RefreshEngine(); }));
        Report(r);
        if (r.Success)
        {
            ShowHint(opt);
            RefreshRunState();
            RefreshEngine();
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        var r = ViaProxyService.Stop();
        Report(r);
        RefreshRunState();
        RefreshEngine();
    }

    private void RefreshRunState()
    {
        var running = ViaProxyService.IsRunning;
        StopBtn.IsEnabled = running;
        StartBtn.IsEnabled = !running && ViaProxyService.HasJava() && ViaProxyService.HasJar();
        RunDot.Fill = new SolidColorBrush(running ? Color.FromArgb(255, 0x4C, 0xAF, 0x50) : Microsoft.UI.Colors.Gray);
        RunState.Text = running ? P("Running", "運行中") : P("Stopped", "已停止");
    }

    private void ShowHint(ViaProxyService.RunOptions opt)
    {
        HintCard.Visibility = Visibility.Visible;
        var addr = ViaProxyService.LocalAddress(opt);
        HintText.Text = P($"Connect Minecraft to {addr} to reach {opt.TargetHost}:{opt.TargetPort} (v{opt.TargetVersion}).",
            $"用 Minecraft 連去 {addr} 就可以去 {opt.TargetHost}:{opt.TargetPort}（版本 {opt.TargetVersion}）。");
    }

    private void CopyAddr_Click(object sender, RoutedEventArgs e)
    {
        var opt = ReadForm();
        var dp = new DataPackage();
        dp.SetText(ViaProxyService.LocalAddress(opt));
        Clipboard.SetContent(dp);
        Notify(StatusBar, InfoBarSeverity.Success, P("Copied", "已複製"), ViaProxyService.LocalAddress(opt));
    }

    // ── Log pane ─────────────────────────────────────────────────────────────

    private void AppendLog(string line)
    {
        if (_log.Length > 0) _log.Append('\n');
        _log.Append(line);
        if (_log.Length > 200_000) _log.Remove(0, _log.Length - 180_000);
        LogText.Text = _log.ToString();
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null, true);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) { _log.Clear(); LogText.Text = ""; }

    private void CopyLog_Click(object sender, RoutedEventArgs e)
    {
        var dp = new DataPackage();
        dp.SetText(_log.ToString());
        Clipboard.SetContent(dp);
        Notify(StatusBar, InfoBarSeverity.Success, P("Log copied", "已複製記錄"), "");
    }

    // ── Form binding ─────────────────────────────────────────────────────────

    private ViaProxyService.RunOptions ReadForm() => new()
    {
        BindHost = (BindHostBox.Text ?? "").Trim(),
        BindPort = (int)BindPortBox.Value,
        TargetHost = (TargetHostBox.Text ?? "").Trim(),
        TargetPort = (int)TargetPortBox.Value,
        TargetVersion = VersionBox.SelectedItem as string ?? "AUTO",
        AuthMethod = (AuthBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "none",
        ProxyOnlineMode = OnlineModeToggle.IsOn,
        BackendProxyUrl = (BackendProxyBox.Text ?? "").Trim(),
        AllowBetaPinging = AllowBetaPingToggle.IsOn,
        BetacraftAuth = BetacraftToggle.IsOn,
    };

    private void LoadForm()
    {
        var o = ViaProxyService.LoadConfig();
        BindHostBox.Text = o.BindHost;
        BindPortBox.Value = o.BindPort;
        TargetHostBox.Text = o.TargetHost;
        TargetPortBox.Value = o.TargetPort;
        var vi = -1;
        for (var i = 0; i < ViaProxyService.CommonVersions.Count; i++)
            if (ViaProxyService.CommonVersions[i] == o.TargetVersion) { vi = i; break; }
        VersionBox.SelectedIndex = vi >= 0 ? vi : 0;
        AuthBox.SelectedIndex = o.AuthMethod == "account" ? 1 : 0;
        OnlineModeToggle.IsOn = o.ProxyOnlineMode;
        BackendProxyBox.Text = o.BackendProxyUrl;
        AllowBetaPingToggle.IsOn = o.AllowBetaPinging;
        BetacraftToggle.IsOn = o.BetacraftAuth;
    }

    // ── Notifications ────────────────────────────────────────────────────────

    private void Report(TweakResult r)
        => Notify(StatusBar, r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Done", "完成") : P("Error", "出錯"),
            (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");

    private static void Notify(InfoBar bar, InfoBarSeverity sev, string title, string msg)
    {
        bar.IsOpen = true; bar.Severity = sev; bar.Title = title; bar.Message = msg;
    }
}
