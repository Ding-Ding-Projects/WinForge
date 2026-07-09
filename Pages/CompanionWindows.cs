using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;
using Windows.System;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 「開啟完整功能」路由 · Router for "Open full features · 開啟完整功能": WinForge companion apps written
/// in each upstream app's LITERAL language. Web companions (TypeScript/JS — Monaco is VS Code's real editor
/// engine) open in a WinForge WebView2 popup (<see cref="WebAppWindow"/>); native C++ companions compile on
/// demand behind a streaming prep popup (<see cref="CompanionPrepWindow"/>) then launch their own native
/// window. Everything is WinForge-authored and WinForge-branded — no upstream product is launched.
/// </summary>
public static class CompanionLauncher
{
    /// <summary>依 id 開隨附 app（唔識嘅 id 靜靜略過）· Open a companion by id (no-op on unknown ids).</summary>
    public static void Open(string id)
    {
        var spec = CompanionAppService.ById(id);
        if (spec is null) return;
        Open(spec);
    }

    public static void Open(CompanionSpec spec)
    {
        if (spec.Kind == CompanionKind.Web)
        {
            WebAppWindow.Show(spec);
            return;
        }
        // Native: instant launch when a compiled exe already exists; otherwise show the prep popup
        // (toolchain auto-install + compile with live streaming output), which launches on success.
        if (CompanionAppService.NativeReady(spec))
        {
            if (CompanionAppService.LaunchNative(spec).Success) return;
        }
        CompanionPrepWindow.Show(spec);
    }
}

/// <summary>
/// Web 隨附 app 彈窗 · WinForge WebView2 popup hosting one web companion (served from WebApps\&lt;id&gt; at
/// https://app.winforge, engine libraries at https://libs.winforge). Handles the JS bridge (ready / save /
/// setTitle), pushes language + theme, and auto-downloads the Monaco engine with an in-page progress view
/// when the app needs it. Windows are tracked so they aren't garbage-collected while open.
/// </summary>
public sealed class WebAppWindow : Window
{
    private static readonly Dictionary<string, WebAppWindow> Open = new(StringComparer.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const int MaxBridgeJsonChars = 96 * 1024 * 1024;
    private const int MaxBridgeBase64Chars = 88 * 1024 * 1024;
    private const int MaxBridgeDecodedBytes = 64 * 1024 * 1024;

    private readonly CompanionSpec _spec;
    private readonly WebView2 _web = new();
    private readonly OverlappedPresenter _presenter = OverlappedPresenter.Create();
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly CancellationTokenSource _windowCts = new();
    private bool _initializing;
    private bool _ready;
    private bool _full;
    private bool _coreEventsAttached;
    private bool _enginePageActive;
    private bool _closed;
    private CancellationTokenSource? _engineCts;

    private WebAppWindow(CompanionSpec spec)
    {
        _spec = spec;
        Title = $"{spec.TitleEn} · {spec.TitleZh} — WinForge";
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }
        ExtendsContentIntoTitleBar = false;
        AppWindow.SetPresenter(_presenter);

        _web.HorizontalAlignment = HorizontalAlignment.Stretch;
        _web.VerticalAlignment = VerticalAlignment.Stretch;
        Content = _web;

        var f11 = new KeyboardAccelerator { Key = VirtualKey.F11 };
        f11.Invoked += (_, e) => { e.Handled = true; ToggleFull(); };
        _web.KeyboardAccelerators.Add(f11);

        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            const int w = 1200, h = 800;
            AppWindow.Resize(new SizeInt32(w, h));
            AppWindow.Move(new PointInt32(
                area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - w) / 2),
                area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - h) / 2)));
        }
        catch { }

        Loc.I.LanguageChanged += OnLanguageChanged;
        _web.Loaded += async (_, _) => await InitWebAsync();
        Closed += (_, _) =>
        {
            _closed = true;
            try { _windowCts.Cancel(); } catch { }
            Loc.I.LanguageChanged -= OnLanguageChanged;
            try { _engineCts?.Cancel(); } catch { }
            try
            {
                if (_web.CoreWebView2 is not null && _coreEventsAttached)
                {
                    _web.CoreWebView2.WebMessageReceived -= OnWebMessage;
                    _web.CoreWebView2.NavigationStarting -= OnNavigationStarting;
                    _web.CoreWebView2.NewWindowRequested -= OnNewWindowRequested;
                }
            }
            catch { }
            if (Open.TryGetValue(_spec.Id, out var current) && ReferenceEquals(current, this))
                Open.Remove(_spec.Id);
        };
    }

    public static void Show(CompanionSpec spec)
    {
        if (Open.TryGetValue(spec.Id, out var existing))
        {
            existing.Activate();
            return;
        }
        var window = new WebAppWindow(spec);
        Open[spec.Id] = window;
        window.Activate();
    }

    private async Task InitWebAsync()
    {
        if (_initializing || _ready) return;
        _initializing = true;
        try
        {
            if (AdminHelper.IsElevated)
                throw new InvalidOperationException(Loc.I.Pick(
                    "Companion apps are disabled while WinForge is running as administrator. Restart WinForge normally and retry.",
                    "WinForge 以系統管理員身分運行時會停用隨附 app。請以一般權限重開 WinForge 再試。"));
            var userData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "WebView2", "companions-userdata");
            Directory.CreateDirectory(userData);
            var env = await CoreWebView2Environment.CreateWithOptionsAsync(
                browserExecutableFolder: string.Empty,
                userDataFolder: userData,
                options: new CoreWebView2EnvironmentOptions());
            await _web.EnsureCoreWebView2Async(env);
            if (_closed) return;

            var core = _web.CoreWebView2;
            core.Settings.IsWebMessageEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;  // editors want cut/copy/paste
            core.Settings.IsZoomControlEnabled = false;
            if (!_coreEventsAttached)
            {
                core.WebMessageReceived += OnWebMessage;         // subscribe BEFORE navigate
                core.NavigationStarting += OnNavigationStarting;
                core.NewWindowRequested += OnNewWindowRequested;
                _coreEventsAttached = true;
            }

            var appDir = Path.Combine(AppContext.BaseDirectory, "WebApps", _spec.WebFolder);
            core.SetVirtualHostNameToFolderMapping("app.winforge", appDir,
                CoreWebView2HostResourceAccessKind.DenyCors);
            core.SetVirtualHostNameToFolderMapping("libs.winforge", CompanionAppService.WebLibsDir,
                CoreWebView2HostResourceAccessKind.Allow);

            _ready = true;

            if (_spec.NeedsMonaco && !CompanionAppService.MonacoInstalled)
                await PrepareEngineThenNavigate();
            else
            {
                _enginePageActive = false;
                core.Navigate("https://app.winforge/index.html");
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log($"companion:{_spec.Id}:init", ex);
            _ready = false;
            if (!_closed) Content = BuildInitError(ex.Message);
        }
        finally { _initializing = false; }
    }

    /// <summary>引擎未裝：顯示內置進度頁，下載完先導航去 app · Show the built-in progress page while the
    /// engine downloads, then navigate to the app. Retry stays inside the page.</summary>
    private async Task PrepareEngineThenNavigate()
    {
        _enginePageActive = true;
        _web.CoreWebView2.NavigateToString(EngineProgressHtml());
        _engineCts?.Dispose();
        _engineCts = new CancellationTokenSource();

        var progress = new Progress<InstallProgressReport>(r =>
        {
            var text = (Loc.I.IsCantonesePrimary ? r.StatusZh : r.StatusEn) ?? r.StatusEn ?? "";
            PostToPage(new { type = "prep", pct = r.Percent, text });
        });

        TweakResult result;
        try { result = await CompanionAppService.EnsureMonacoAsync(progress, _engineCts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { result = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
        if (_closed) return;

        if (result.Success)
        {
            _enginePageActive = false;
            _web.CoreWebView2?.Navigate("https://app.winforge/index.html");
        }
        else
            PostToPage(new { type = "prepError", text = result.Message?.Get(Loc.I.Language) ?? "" });
    }

    private FrameworkElement BuildInitError(string detail)
    {
        var retry = new Button { Content = Loc.I.Pick("Retry", "再試"), MinWidth = 110 };
        retry.Click += async (_, _) =>
        {
            Content = _web;
            await InitWebAsync();
        };
        return new Border
        {
            Padding = new Thickness(32),
            Child = new StackPanel
            {
                MaxWidth = 620,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = Loc.I.Pick("The companion window could not start.", "隨附 app 視窗無法啟動。"),
                        FontSize = 20,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    new TextBlock { Text = detail, TextWrapping = TextWrapping.Wrap },
                    retry,
                },
            },
        };
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (IsAppOrigin(e.Uri))
            return;
        if (_enginePageActive && string.Equals(e.Uri, "about:blank", StringComparison.OrdinalIgnoreCase))
            return;
        e.Cancel = true;
    }

    private static void OnNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs e)
        => e.Handled = true;

    private static bool IsAppOrigin(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
        && uri.Host.Equals("app.winforge", StringComparison.OrdinalIgnoreCase)
        && uri.IsDefaultPort
        && string.IsNullOrEmpty(uri.UserInfo);

    private void OnWebMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        bool appOrigin = IsAppOrigin(e.Source);
        bool engineOrigin = _enginePageActive
            && string.Equals(e.Source, "about:blank", StringComparison.OrdinalIgnoreCase);
        if (!appOrigin && !engineOrigin) return;

        BridgeMsg? msg;
        try
        {
            var json = e.WebMessageAsJson;
            if (json.Length > MaxBridgeJsonChars) return;
            msg = JsonSerializer.Deserialize<BridgeMsg>(json, JsonOpts);
        }
        catch { return; }
        if (msg is null) return;
        if ((msg.Type?.Length ?? 0) > 32) return;

        if (engineOrigin)
        {
            if (msg.Type == "retryEngine") _ = PrepareEngineThenNavigate();
            return;
        }

        switch (msg.Type)
        {
            case "ready":
                PostLanguage();
                PostTheme();
                break;
            case "save":
                if (string.IsNullOrWhiteSpace(msg.RequestId) || msg.RequestId.Length > 128
                    || (msg.Name?.Length ?? 0) > 512 || (msg.Mime?.Length ?? 0) > 128)
                    return;
                _ = HandleSaveAsync(msg, _windowCts.Token);
                break;
            case "setTitle":
                if (!string.IsNullOrWhiteSpace(msg.Text))
                {
                    var raw = msg.Text!.Length > 256 ? msg.Text[..256] : msg.Text;
                    var t = raw.Trim();
                    if (t.Length > 60) t = t[..60] + "…";
                    Title = $"{_spec.TitleEn} · {_spec.TitleZh} — {t}";
                }
                break;
        }
    }

    /// <summary>bridge save：JS 畀 base64，經 FileDialogs 儲存 · Bridge save: decode base64, save via FileDialogs.</summary>
    private async Task HandleSaveAsync(BridgeMsg msg, CancellationToken ct)
    {
        bool entered = false;
        try
        {
            await _saveGate.WaitAsync(ct);
            entered = true;
            ct.ThrowIfCancellationRequested();
            if (_closed) return;

            if ((msg.DataBase64?.Length ?? 0) > MaxBridgeBase64Chars)
            {
                PostToPage(new { type = "saveDone", requestId = msg.RequestId, ok = false,
                    error = "The file exceeds the 64 MB companion-save limit." });
                return;
            }

            var name = string.IsNullOrWhiteSpace(msg.Name) ? "untitled.txt" : Path.GetFileName(msg.Name!.Trim());
            if (string.IsNullOrWhiteSpace(name)) name = "untitled.txt";
            if (name.Length > 128) name = name[..128];
            var ext = Path.GetExtension(name);
            if (string.IsNullOrEmpty(ext)) ext = ".txt";
            var dest = await FileDialogs.SaveFileAsync(name, ext);
            ct.ThrowIfCancellationRequested();
            if (_closed) return;
            if (dest is null)
            {
                PostToPage(new { type = "saveDone", requestId = msg.RequestId, ok = false, cancelled = true });
                return;
            }
            var bytes = Convert.FromBase64String(msg.DataBase64 ?? "");
            if (bytes.Length > MaxBridgeDecodedBytes)
            {
                PostToPage(new { type = "saveDone", requestId = msg.RequestId, ok = false,
                    error = "The file exceeds the 64 MB companion-save limit." });
                return;
            }
            ct.ThrowIfCancellationRequested();
            await File.WriteAllBytesAsync(dest, bytes, ct);
            ct.ThrowIfCancellationRequested();
            PostToPage(new { type = "saveDone", requestId = msg.RequestId, ok = true, path = dest });
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PostToPage(new { type = "saveDone", requestId = msg.RequestId, ok = false, error = ex.Message });
        }
        finally
        {
            if (entered) _saveGate.Release();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => PostLanguage();

    private void PostLanguage()
    {
        var lang = Loc.I.Language switch
        {
            AppLanguage.English => "En",
            AppLanguage.Cantonese => "Zh",
            _ => "Bilingual",
        };
        PostToPage(new { type = "language", lang });
    }

    private void PostTheme()
    {
        bool dark = true;
        try { dark = Application.Current.RequestedTheme == ApplicationTheme.Dark; } catch { }
        PostToPage(new { type = "theme", dark });
    }

    private static readonly JsonSerializerOptions PostOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private void PostToPage(object o)
    {
        if (_closed) return;
        try { _web.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(o, PostOpts)); } catch { }
    }

    private void ToggleFull()
    {
        try
        {
            _full = !_full;
            AppWindow.SetPresenter(_full ? FullScreenPresenter.Create() : _presenter);
        }
        catch { _full = false; }
    }

    /// <summary>引擎下載中顯示嘅內置頁（雙語）· The built-in bilingual engine-download page.</summary>
    private string EngineProgressHtml() => $$$"""
<!doctype html><html><head><meta charset="utf-8"><style>
  html,body{height:100%;margin:0;background:#141619;color:#e8ecf1;
    font-family:'Segoe UI',system-ui,sans-serif;display:flex;align-items:center;justify-content:center}
  .card{max-width:520px;text-align:center;padding:32px}
  h1{font-size:20px;margin:0 0 6px}
  .sub{color:#9aa4b0;font-size:13px;margin-bottom:22px}
  .bar{height:6px;border-radius:3px;background:#2a2e34;overflow:hidden}
  .fill{height:100%;width:0%;border-radius:3px;background:#54e07e;transition:width .25s}
  .indet .fill{width:30%;animation:sl 1.2s ease-in-out infinite alternate}
  @keyframes sl{from{margin-left:0}to{margin-left:70%}}
  #status{margin-top:12px;font-size:12px;color:#9aa4b0;min-height:2.4em;word-break:break-all}
  #retry{display:none;margin-top:16px;padding:8px 22px;border-radius:6px;border:1px solid #3a3f46;
    background:#22262b;color:#e8ecf1;font-size:13px;cursor:pointer}
  #retry:hover{background:#2b3036}
  .err #status{color:#ff8080}
</style></head><body>
<div class="card">
  <h1>{{{_spec.TitleEn}}} · {{{_spec.TitleZh}}}</h1>
  <div class="sub">Preparing the editor engine (one-time download) · 準備緊編輯器引擎（一次性下載）</div>
  <div class="bar indet" id="bar"><div class="fill" id="fill"></div></div>
  <div id="status">Starting… · 開始緊…</div>
  <button id="retry" onclick="window.chrome.webview.postMessage({type:'retryEngine'});this.style.display='none';document.body.classList.remove('err');document.getElementById('bar').classList.add('indet');">Retry · 再試</button>
</div>
<script>
  window.chrome.webview.addEventListener('message', e => {
    const m = e.data || {};
    if (m.type === 'prep') {
      if (typeof m.pct === 'number') {
        document.getElementById('bar').classList.remove('indet');
        document.getElementById('fill').style.width = m.pct + '%';
      }
      if (m.text) document.getElementById('status').textContent = m.text;
    } else if (m.type === 'prepError') {
      document.body.classList.add('err');
      document.getElementById('status').textContent = m.text || 'Failed. · 失敗。';
      document.getElementById('retry').style.display = 'inline-block';
    }
  });
</script>
</body></html>
""";

    private sealed record BridgeMsg(
        string? Type, string? Name, string? Mime, string? DataBase64, string? Text, string? RequestId);
}

/// <summary>
/// 原生隨附 app 準備彈窗 · Prep popup for a native (C++) companion: auto-installs the toolchain when
/// missing (winget, streamed), compiles the shipped WinForge source (streamed compiler output), then
/// launches the WinForge-branded native window and closes itself. Retry on failure. Fully bilingual.
/// </summary>
public sealed class CompanionPrepWindow : Window
{
    private static readonly Dictionary<string, CompanionPrepWindow> Open = new(StringComparer.OrdinalIgnoreCase);

    private readonly CompanionSpec _spec;
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, IsIndeterminate = true };
    private readonly TextBlock _status = new()
    {
        FontSize = 12,
        TextWrapping = TextWrapping.Wrap,
        MaxLines = 4,
    };
    private readonly Button _retry = new() { Visibility = Visibility.Collapsed, MinWidth = 110 };
    private readonly Button _cancel = new() { MinWidth = 110 };
    private CancellationTokenSource? _cts;
    private bool _running;
    private bool _closed;

    private CompanionPrepWindow(CompanionSpec spec)
    {
        _spec = spec;
        Title = $"{spec.TitleEn} · {spec.TitleZh} — WinForge";
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }
        ExtendsContentIntoTitleBar = false;
        var presenter = OverlappedPresenter.Create();
        presenter.IsMaximizable = false;
        presenter.IsResizable = false;
        AppWindow.SetPresenter(presenter);

        var root = new StackPanel { Spacing = 12, Padding = new Thickness(24, 20, 24, 20) };
        root.Children.Add(new TextBlock
        {
            Text = $"{spec.TitleEn} · {spec.TitleZh}",
            FontSize = 17,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        root.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick(
                "First run: WinForge compiles this app from its shipped C++ source — the toolchain installs automatically if needed.",
                "首次執行：WinForge 會由隨附嘅 C++ 原始碼編譯呢個 app — 需要時會自動安裝工具鏈。"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = SafeBrush("TextFillColorSecondaryBrush"),
        });
        root.Children.Add(_bar);
        _status.Foreground = SafeBrush("TextFillColorSecondaryBrush");
        root.Children.Add(_status);

        _retry.Content = Loc.I.Pick("Retry", "再試");
        _retry.Click += (_, _) => { _ = RunAsync(); };
        _cancel.Content = Loc.I.Pick("Cancel", "取消");
        _cancel.Click += (_, _) => { try { _cts?.Cancel(); } catch { } Close(); };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_retry);
        buttons.Children.Add(_cancel);
        root.Children.Add(buttons);

        Content = root;

        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            const int w = 460, h = 300;
            AppWindow.Resize(new SizeInt32(w, h));
            AppWindow.Move(new PointInt32(
                area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - w) / 2),
                area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - h) / 2)));
        }
        catch { }

        Closed += (_, _) =>
        {
            _closed = true;
            try { _cts?.Cancel(); } catch { }
            if (Open.TryGetValue(_spec.Id, out var current) && ReferenceEquals(current, this))
                Open.Remove(_spec.Id);
        };

    }

    public static void Show(CompanionSpec spec)
    {
        if (Open.TryGetValue(spec.Id, out var existing))
        {
            existing.Activate();
            return;
        }
        var window = new CompanionPrepWindow(spec);
        Open[spec.Id] = window;
        window.Activate();
        _ = window.RunAsync();
    }

    private async Task RunAsync()
    {
        if (_running || _closed) return;
        _running = true;
        _retry.Visibility = Visibility.Collapsed;
        _bar.IsIndeterminate = true;
        _bar.ShowError = false;
        _status.Text = Loc.I.Pick("Starting…", "開始緊…");

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        var progress = new Progress<InstallProgressReport>(r =>
        {
            if (_closed) return;
            if (r.Percent is double p) { _bar.IsIndeterminate = false; _bar.Value = p; }
            var text = (Loc.I.IsCantonesePrimary ? r.StatusZh : r.StatusEn) ?? r.StatusEn;
            if (!string.IsNullOrWhiteSpace(text)) _status.Text = text;
        });

        TweakResult result;
        try { result = await CompanionAppService.EnsureNativeAsync(_spec, progress, _cts.Token); }
        catch (OperationCanceledException) { _running = false; return; }
        catch (Exception ex) { result = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
        _running = false;
        if (_closed || _cts?.IsCancellationRequested == true) return;

        if (result.Success)
        {
            var launched = CompanionAppService.LaunchNative(_spec);
            if (launched.Success)
            {
                Close();
                return;
            }
            result = launched;
        }

        _bar.IsIndeterminate = false;
        _bar.ShowError = true;
        var msg = result.Message?.Get(Loc.I.Language) ?? Loc.I.Pick("Failed.", "失敗。");
        var detail = (result.Output ?? "").Trim();
        if (detail.Length > 400) detail = detail[^400..];   // compiler errors: the tail matters most
        _status.Text = detail.Length > 0 ? $"{msg}\n{detail}" : msg;
        _retry.Visibility = Visibility.Visible;
    }

    private static Brush? SafeBrush(string key)
    {
        try
        {
            if (Application.Current?.Resources?.TryGetValue(key, out var v) == true && v is Brush b) return b;
        }
        catch { }
        return null;
    }
}
