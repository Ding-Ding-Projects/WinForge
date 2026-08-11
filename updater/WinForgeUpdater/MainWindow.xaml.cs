using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForgeUpdater;

/// <summary>
/// The visible updater window: download (with a real progress bar) → verify SHA-256 → wait for WinForge
/// to close → stage the single-file launcher outside the install folder → exit so that helper can install
/// and relaunch. Driven by command-line args passed by WinForge:
///   --pid &lt;id&gt; --tag &lt;v&gt; --install-dir &lt;dir&gt; --exe &lt;path&gt; --launcher &lt;path&gt; --sha256 &lt;digest&gt;
///   [--installer &lt;already-downloaded.exe&gt;] [--url &lt;installer-download-url&gt;]
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly DispatcherQueue _ui;
    private static readonly HttpClient Http = new();
    private string? _persistentLogPath;
    private bool _handedOff;
    private int _relaunchScheduled;

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
        Closed += (_, _) => { if (!_handedOff) ClearUpdatePendingFlag(); };
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
    private void Log(string s)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_persistentLogPath))
                File.AppendAllText(_persistentLogPath,
                    $"[{DateTimeOffset.Now:O}] {s}{Environment.NewLine}");
        }
        catch { }
        _ui.TryEnqueue(() =>
        {
            LogText.Text += (LogText.Text.Length == 0 ? "" : "\n") + s;
            LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
        });
    }

    private async Task RunAsync()
    {
        var args = ParseArgs();
        string tag = args.GetValueOrDefault("tag", "");
        string installer = args.GetValueOrDefault("installer", "");
        string url = args.GetValueOrDefault("url", "");
        string installDir = args.GetValueOrDefault("install-dir", AppContext.BaseDirectory);
        string exe = args.GetValueOrDefault("exe", "");
        string launcher = args.GetValueOrDefault("launcher", "");
        string expectedSha256 = NormalizeSha256(args.GetValueOrDefault("sha256", ""));
        string updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "updates");
        Directory.CreateDirectory(updateDir);
        _persistentLogPath = Path.Combine(updateDir,
            $"updater-{SafeTag(tag)}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        _ui.TryEnqueue(() => TagText.Text = string.IsNullOrEmpty(tag) ? "" : "v" + tag);

        try
        {
            if (IsElevated())
                throw new InvalidOperationException(
                    "WinForge Updater cannot run as administrator. Restart WinForge normally and retry. · " +
                    "WinForge 更新程式唔可以用系統管理員身分執行。請以一般權限重開 WinForge 再試。");
            if (expectedSha256.Length != 64)
                throw new InvalidOperationException(
                    "The release did not provide a valid SHA-256 installer digest. The update was stopped safely. · " +
                    "Release 冇提供有效嘅 SHA-256 安裝程式雜湊值，更新已安全停止。");
            if (!ManagedReleaseContract.TryParseReleaseVersion(tag, out Version? releaseVersion))
                throw new InvalidOperationException(
                    "The update tag is outside WinForge's managed release line. · " +
                    "更新 tag 唔屬於 WinForge 正式發佈版本線。");
            tag = ManagedReleaseContract.NormalizeTag(releaseVersion.ToString(3));
            ManagedInstallLayout layout = ManagedReleaseContract.ValidateInstallLayout(installDir, launcher, exe);
            installDir = layout.InstallDirectory;
            launcher = layout.LauncherPath;
            exe = layout.ExecutablePath;

            // 1) Obtain the installer (download with progress unless one was already provided).
            if (string.IsNullOrEmpty(installer) || !File.Exists(installer))
            {
                if (string.IsNullOrEmpty(url)) throw new InvalidOperationException("No installer or URL was provided.");
                if (!ManagedReleaseContract.IsCanonicalReleaseDownload(
                        url, tag, ManagedReleaseContract.InstallerAssetName))
                    throw new InvalidDataException(
                        "The installer URL is outside the canonical WinForge release path. · " +
                        "安裝程式網址唔屬於正式 WinForge release 路徑。");
                Status($"Downloading WinForge {(string.IsNullOrEmpty(tag) ? "" : "v" + tag)} … · 下載緊…");
                Log("Downloading: " + url);
                installer = await DownloadAsync(url, tag);
                Log("Downloaded: " + installer);
            }
            else
            {
                installer = ManagedReleaseContract.ValidateStagedInstallerPath(installer, updateDir);
                Log("Using downloaded installer: " + installer);
                Progress(100);
            }

            long installerBytes = new FileInfo(installer).Length;
            if (installerBytes <= 0 || installerBytes > ManagedReleaseContract.MaximumInstallerBytes)
                throw new InvalidDataException(
                    "Installer size is invalid or exceeds the 512 MB safety limit. · " +
                    "安裝程式大小無效，或者超過 512 MB 安全上限。");

            string actualSha256 = await ComputeSha256Async(installer);
            Log("Installer SHA-256: " + actualSha256);
            if (!ManagedReleaseContract.FixedTimeSha256Equals(expectedSha256, actualSha256))
            {
                try { File.Delete(installer); } catch { }
                throw new InvalidDataException(
                    "Installer SHA-256 verification failed. The downloaded file was not run. · " +
                    "安裝程式 SHA-256 驗證失敗，下載檔案冇執行。");
            }

            // 2) Wait for the running WinForge to exit.
            if (int.TryParse(args.GetValueOrDefault("pid", ""), out int pid) && pid > 0)
            {
                Status("Waiting for WinForge to close… · 等待 WinForge 關閉…");
                Progress(null);
                if (!await WaitForExitAsync(pid, TimeSpan.FromSeconds(120)))
                    throw new TimeoutException("WinForge did not close within two minutes. · WinForge 兩分鐘內未關閉。");
                Log($"WinForge (pid {pid}) closed.");
            }

            // 3) Copy the single-file launcher outside the installation directory and hand off. This
            // updater must exit before unsigned Squirrel Setup.exe replaces shared runtime files.
            Status($"Preparing WinForge {(string.IsNullOrEmpty(tag) ? "" : "v" + tag)} … · 準備緊…");
            Progress(null);
            string helperLog = Path.Combine(updateDir,
                $"install-{SafeTag(tag)}-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            int helperPid = LaunchApplyHelper(launcher, installer, installDir, exe, expectedSha256, helperLog);
            _handedOff = true;
            Log($"Staged update helper (pid {helperPid}). Squirrel log will be {Path.ChangeExtension(helperLog, ".squirrel.log")}");
            Status("Applying update in the background… · 背景套用更新緊…");
            await Task.Delay(350);
            _ui.TryEnqueue(() => Application.Current.Exit());
            return;
        }
        catch (Exception ex)
        {
            if (!_handedOff)
            {
                ClearUpdatePendingFlag();
                if (!IsElevated()) ScheduleRelaunchWhenNeeded(args);
            }
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
        string path = Path.Combine(dir, $"Setup-{safe}.exe");
        string tmp = path + ".tmp";
        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }

        try
        {
            using var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            res.EnsureSuccessStatusCode();
            long? total = res.Content.Headers.ContentLength;
            if (total is null or <= 0 or > ManagedReleaseContract.MaximumInstallerBytes)
                throw new InvalidDataException("Installer download size is missing, empty, or exceeds 512 MB.");
            long read = 0;
            await using (var input = await res.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buffer = new byte[81920];
                int n;
                var sw = Stopwatch.StartNew();
                while ((n = await input.ReadAsync(buffer)) > 0)
                {
                    if (read > ManagedReleaseContract.MaximumInstallerBytes - n)
                        throw new InvalidDataException("Installer download exceeded the 512 MB safety limit.");
                    await output.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    double pct = read * 100.0 / total.Value;
                    Progress(pct);
                    if (sw.ElapsedMilliseconds > 150)
                    {
                        Detail($"{Human(read)} / {Human(total.Value)}  ({pct:0}%)");
                        sw.Restart();
                    }
                }
                Detail(Human(read) + " downloaded");
            }
            if (read != total.Value)
                throw new InvalidDataException("Installer download length did not match GitHub's content length.");
            try { if (File.Exists(path)) File.Delete(path); } catch { }
            File.Move(tmp, path);
            Progress(100);
            return path;
        }
        catch
        {
            try { File.Delete(tmp); } catch { }
            throw;
        }
    }

    private static async Task<bool> WaitForExitAsync(int pid, TimeSpan timeout)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            await proc.WaitForExitAsync(cts.Token);
            return true;
        }
        catch (ArgumentException) { return true; }
        catch (OperationCanceledException) { return false; }
        catch { return false; }
    }

    private static int LaunchApplyHelper(string launcher, string installer, string installDir, string exe,
        string expectedSha256, string logPath)
    {
        ManagedInstallLayout layout = ManagedReleaseContract.ValidateInstallLayout(installDir, launcher, exe);
        launcher = layout.LauncherPath;
        installDir = layout.InstallDirectory;
        exe = layout.ExecutablePath;
        string updateDir = Path.GetDirectoryName(logPath)!;
        installer = ManagedReleaseContract.ValidateStagedInstallerPath(installer, updateDir);
        logPath = ManagedReleaseContract.ValidateUpdateLogPath(logPath, updateDir);
        if (!File.Exists(launcher))
            throw new FileNotFoundException(
                "The single-file WinForge launcher required for update handoff is missing.", launcher);

        Directory.CreateDirectory(updateDir);
        string helper = Path.Combine(updateDir, $"WinForgeApplyUpdate-{Guid.NewGuid():N}.exe");
        File.Copy(launcher, helper, overwrite: false);

        var psi = new ProcessStartInfo
        {
            FileName = helper,
            WorkingDirectory = updateDir,
            UseShellExecute = false,
        };
        psi.ArgumentList.Add("--apply-update");
        foreach (var (name, value) in new[]
                 {
                     ("--installer", installer), ("--install-dir", installDir), ("--launcher", launcher),
                     ("--exe", exe), ("--sha256", expectedSha256), ("--log", logPath),
                     ("--wait-pid", Environment.ProcessId.ToString())
                 })
        {
            psi.ArgumentList.Add(name);
            psi.ArgumentList.Add(value);
        }
        var process = Process.Start(psi) ??
            throw new InvalidOperationException("The staged update helper did not start.");
        return process.Id;
    }

    private async void Close_Click(object sender, RoutedEventArgs e)
    {
        if (!_handedOff)
        {
            ClearUpdatePendingFlag();
            if (!IsElevated()) await RelaunchWhenNeededAsync(ParseArgs());
        }
        Application.Current.Exit();
    }

    private static string SafeTag(string tag)
    {
        var chars = new char[tag.Length];
        for (int i = 0; i < tag.Length; i++) chars[i] = char.IsLetterOrDigit(tag[i]) || tag[i] is '.' or '-' or '_' ? tag[i] : '-';
        var s = new string(chars).Trim('-');
        return s.Length == 0 ? "latest" : s;
    }

    private static string NormalizeSha256(string value)
    {
        value = value.Trim();
        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) value = value[7..];
        return ManagedReleaseContract.NormalizeSha256(value);
    }

    private static async Task<string> ComputeSha256Async(string path)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(input));
    }

    private static bool IsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return true; }
    }

    private static async Task RelaunchWhenNeededAsync(Dictionary<string, string> args)
    {
        if (int.TryParse(args.GetValueOrDefault("pid", ""), out int pid) && pid > 0)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                try
                {
                    using var process = Process.GetProcessById(pid);
                    if (process.HasExited) break;
                }
                catch (ArgumentException) { break; }
                catch { return; }
                await Task.Delay(250);
            }
        }

        string launcher = args.GetValueOrDefault("launcher", "");
        string exe = args.GetValueOrDefault("exe", "");
        string installDir = args.GetValueOrDefault("install-dir", "");
        string? target = File.Exists(launcher) ? launcher : File.Exists(exe) ? exe : null;
        if (target is null) return;
        if (IsElevated()) return;
        try
        {
            string root = Path.GetFullPath(installDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(target).StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
        }
        catch { return; }
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                WorkingDirectory = Path.GetDirectoryName(target) ?? AppContext.BaseDirectory,
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private void ScheduleRelaunchWhenNeeded(Dictionary<string, string> args)
    {
        if (Interlocked.Exchange(ref _relaunchScheduled, 1) == 0)
            _ = RelaunchWhenNeededAsync(args);
    }

    private static void ClearUpdatePendingFlag()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "update.pending");
            File.Delete(path);
        }
        catch { }
    }

    private static string Human(long b)
    {
        string[] u = { "B", "KB", "MB", "GB" };
        double v = b; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return $"{v:0.#} {u[i]}";
    }
}
