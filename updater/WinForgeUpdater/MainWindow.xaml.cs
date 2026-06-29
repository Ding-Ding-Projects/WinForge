using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinForgeUpdater;

/// <summary>
/// The visible updater window: download (with a real progress bar) → wait for WinForge to close →
/// run the installer silently → relaunch WinForge. Driven by command-line args passed by WinForge:
///   --pid &lt;id&gt; --tag &lt;v&gt; --install-dir &lt;dir&gt; --exe &lt;path&gt; --launcher &lt;path&gt;
///   [--installer &lt;already-downloaded.exe&gt;] [--url &lt;installer-download-url&gt;]
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueue _ui;
    private static readonly HttpClient Http = new();

    public MainWindow()
    {
        InitializeComponent();
        _ui = DispatcherQueue.GetForCurrentThread();
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge-Updater/1.0");

        try
        {
            AppWindow.Resize(new Windows.Graphics.SizeInt32(560, 380));
            if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter p)
            { p.IsResizable = false; p.IsMaximizable = false; }
        }
        catch { }

        Root.Loaded += (_, _) => _ = RunAsync();
    }

    private Dictionary<string, string> ParseArgs()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var a = Environment.GetCommandLineArgs();
        for (int i = 1; i < a.Length - 1; i++)
            if (a[i].StartsWith("--", StringComparison.Ordinal))
                d[a[i][2..]] = a[i + 1];
        return d;
    }

    private void Status(string s) => _ui.TryEnqueue(() => StatusText.Text = s);
    private void Detail(string s) => _ui.TryEnqueue(() => DetailText.Text = s);
    private void Progress(double? pct) => _ui.TryEnqueue(() =>
    {
        if (pct is null) { Bar.IsIndeterminate = true; }
        else { Bar.IsIndeterminate = false; Bar.Value = Math.Clamp(pct.Value, 0, 100); }
    });
    private void Log(string s) => _ui.TryEnqueue(() =>
    {
        LogText.Text += (LogText.Text.Length == 0 ? "" : "\n") + s;
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
    });

    private async Task RunAsync()
    {
        var args = ParseArgs();
        string tag = args.GetValueOrDefault("tag", "");
        string installer = args.GetValueOrDefault("installer", "");
        string url = args.GetValueOrDefault("url", "");
        string installDir = args.GetValueOrDefault("install-dir", AppContext.BaseDirectory);
        string exe = args.GetValueOrDefault("exe", "");
        string launcher = args.GetValueOrDefault("launcher", "");
        _ui.TryEnqueue(() => TagText.Text = string.IsNullOrEmpty(tag) ? "" : "v" + tag);

        try
        {
            // 1) Obtain the installer (download with progress unless one was already provided).
            if (string.IsNullOrEmpty(installer) || !File.Exists(installer))
            {
                if (string.IsNullOrEmpty(url)) throw new InvalidOperationException("No installer or URL was provided.");
                Status($"Downloading WinForge {(string.IsNullOrEmpty(tag) ? "" : "v" + tag)} … · 下載緊…");
                Log("Downloading: " + url);
                installer = await DownloadAsync(url, tag);
                Log("Downloaded: " + installer);
            }
            else
            {
                Log("Using downloaded installer: " + installer);
                Progress(100);
            }

            // 2) Wait for the running WinForge to exit.
            if (int.TryParse(args.GetValueOrDefault("pid", ""), out int pid) && pid > 0)
            {
                Status("Waiting for WinForge to close… · 等待 WinForge 關閉…");
                Progress(null);
                await WaitForExitAsync(pid, TimeSpan.FromSeconds(120));
                Log($"WinForge (pid {pid}) closed.");
            }

            // 3) Run the installer silently (indeterminate — the installer itself is quiet).
            Status($"Installing WinForge {(string.IsNullOrEmpty(tag) ? "" : "v" + tag)} … · 安裝緊…");
            Progress(null);
            Log("Running installer (silent)…");
            int code = await RunInstallerAsync(installer, installDir);
            Log($"Installer exit code: {code}");
            if (code != 0) throw new Exception($"Installer exited with code {code}.");

            // 4) Relaunch WinForge.
            Status("Reopening WinForge… · 重新開啟 WinForge…");
            Progress(100);
            string? target = File.Exists(launcher) ? launcher : (File.Exists(exe) ? exe : null);
            if (target is not null)
            {
                try { Process.Start(new ProcessStartInfo { FileName = target, WorkingDirectory = Path.GetDirectoryName(target)!, UseShellExecute = true }); Log("Relaunched: " + target); }
                catch (Exception ex) { Log("Relaunch failed: " + ex.Message); }
            }

            Status("Update complete. · 更新完成。");
            Log("Done.");
            await Task.Delay(1200);
            _ui.TryEnqueue(() => Application.Current.Exit());
        }
        catch (Exception ex)
        {
            Status("Update failed. · 更新失敗。");
            Progress(0);
            Log("ERROR: " + ex.Message);
            _ui.TryEnqueue(() => { CloseBtn.Visibility = Visibility.Visible; });
        }
    }

    private async Task<string> DownloadAsync(string url, string tag)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "updates");
        Directory.CreateDirectory(dir);
        string safe = SafeTag(tag);
        string path = Path.Combine(dir, $"WinForge-Setup-{safe}.exe");
        string tmp = path + ".tmp";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        res.EnsureSuccessStatusCode();
        long? total = res.Content.Headers.ContentLength;
        await using (var input = await res.Content.ReadAsStreamAsync())
        await using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            var buffer = new byte[81920];
            long read = 0;
            int n;
            var sw = Stopwatch.StartNew();
            while ((n = await input.ReadAsync(buffer)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                if (total is > 0)
                {
                    double pct = read * 100.0 / total.Value;
                    Progress(pct);
                    if (sw.ElapsedMilliseconds > 150) { Detail($"{Human(read)} / {Human(total.Value)}  ({pct:0}%)"); sw.Restart(); }
                }
                else { Progress(null); Detail(Human(read) + " downloaded"); }
            }
            Detail(Human(read) + " downloaded");
        }
        try { if (File.Exists(path)) File.Delete(path); } catch { }
        File.Move(tmp, path);
        Progress(100);
        return path;
    }

    private static async Task WaitForExitAsync(int pid, TimeSpan timeout)
    {
        try
        {
            var proc = Process.GetProcessById(pid);
            await proc.WaitForExitAsync(new System.Threading.CancellationTokenSource(timeout).Token);
        }
        catch { /* already gone, or timed out — proceed; the installer uses /CLOSEAPPLICATIONS */ }
    }

    private static Task<int> RunInstallerAsync(string installer, string installDir)
    {
        var psi = new ProcessStartInfo
        {
            FileName = installer,
            UseShellExecute = true,   // Inno Setup elevates via its own manifest
            Verb = "runas",
        };
        foreach (var a in new[] { "/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS", $"/DIR=\"{installDir}\"" })
            psi.ArgumentList.Add(a);
        var tcs = new TaskCompletionSource<int>();
        try
        {
            var p = Process.Start(psi);
            if (p is null) { tcs.SetResult(-1); return tcs.Task; }
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) => tcs.TrySetResult(p.ExitCode);
        }
        catch (Exception ex) { tcs.SetException(ex); }
        return tcs.Task;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();

    private static string SafeTag(string tag)
    {
        var chars = new char[tag.Length];
        for (int i = 0; i < tag.Length; i++) chars[i] = char.IsLetterOrDigit(tag[i]) || tag[i] is '.' or '-' or '_' ? tag[i] : '-';
        var s = new string(chars).Trim('-');
        return s.Length == 0 ? "latest" : s;
    }

    private static string Human(long b)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }
}
