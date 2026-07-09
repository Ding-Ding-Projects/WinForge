using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
    private const int MaxVisibleLogChars = 80_000;
    private static readonly Dictionary<string, CompanionPrepWindow> Open = new(StringComparer.OrdinalIgnoreCase);

    private readonly CompanionSpec _spec;
    private readonly TextBlock _description = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _stateText = new() { FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly Border _stateBadge = new() { Padding = new Thickness(10, 4, 10, 4), CornerRadius = new CornerRadius(12) };
    private readonly InfoBar _notice = new() { IsOpen = false, IsClosable = false };
    private readonly ProgressBar _bar = new() { Minimum = 0, Maximum = 100, IsIndeterminate = true };
    private readonly TextBlock _status = new()
    {
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
    };
    private readonly TextBlock _percent = new() { MinWidth = 44, HorizontalTextAlignment = TextAlignment.Right };
    private readonly TextBlock _terminalCaption = new() { FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly TextBlock _terminalMeta = new() { FontSize = 11, Opacity = 0.7 };
    private readonly TextBlock _terminalText = new()
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = 12,
        TextWrapping = TextWrapping.NoWrap,
        IsTextSelectionEnabled = true,
    };
    private readonly ScrollViewer _terminalScroll = new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        Padding = new Thickness(12, 10, 12, 10),
    };
    private readonly TextBlock _logCaption = new() { FontSize = 11, Opacity = 0.7 };
    private readonly TextBlock _logPath = new()
    {
        FontFamily = new FontFamily("Consolas"),
        FontSize = 11,
        TextTrimming = TextTrimming.CharacterEllipsis,
        IsTextSelectionEnabled = true,
        MaxWidth = 430,
    };
    private readonly Button _openLog = new() { MinWidth = 104 };
    private readonly Button _retry = new() { Visibility = Visibility.Collapsed, MinWidth = 110 };
    private readonly Button _cancel = new() { MinWidth = 110 };
    private readonly Button _close = new() { Visibility = Visibility.Collapsed, MinWidth = 110 };
    private readonly StringBuilder _visibleLog = new();
    private CancellationTokenSource? _cts;
    private CompanionBuildLog? _log;
    private bool _running;
    private bool _closed;
    private bool _allowClose;
    private bool _cancelRequested;
    private bool _closeBlockedNotified;
    private bool _visibleLogTrimmed;
    private int _attemptId;
    private int _visibleLineCount;
    private PrepState _state = PrepState.Starting;
    private NoticeKind _noticeKind;
    private string _noticeDetail = "";
    private string _statusEn = "Starting…";
    private string _statusZh = "開始緊…";

    private enum PrepState { Starting, Running, Cancelling, Failed, Complete }
    private enum NoticeKind { None, CloseBlocked, Failed, LogUnavailable, CleanupUnconfirmed }

    private CompanionPrepWindow(CompanionSpec spec)
    {
        _spec = spec;
        Title = $"{spec.TitleEn} · {spec.TitleZh} — WinForge";
        try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }
        ExtendsContentIntoTitleBar = false;
        var presenter = OverlappedPresenter.Create();
        presenter.IsMaximizable = true;
        presenter.IsResizable = true;
        AppWindow.SetPresenter(presenter);

        var root = new Grid
        {
            Padding = new Thickness(24, 20, 24, 20),
            RowSpacing = 12,
        };
        ApplyWindowTheme(root);
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new Grid { ColumnSpacing = 16 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headingStack = new StackPanel { Spacing = 5 };
        headingStack.Children.Add(new TextBlock
        {
            Text = $"{spec.TitleEn} · {spec.TitleZh}",
            FontSize = 19,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        headingStack.Children.Add(_description);
        header.Children.Add(headingStack);
        _stateBadge.Child = _stateText;
        _stateBadge.VerticalAlignment = VerticalAlignment.Top;
        Grid.SetColumn(_stateBadge, 1);
        header.Children.Add(_stateBadge);
        root.Children.Add(header);

        Grid.SetRow(_notice, 1);
        root.Children.Add(_notice);

        var progressPanel = new StackPanel { Spacing = 7 };
        var statusRow = new Grid { ColumnSpacing = 12 };
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusRow.Children.Add(_status);
        Grid.SetColumn(_percent, 1);
        statusRow.Children.Add(_percent);
        progressPanel.Children.Add(statusRow);
        progressPanel.Children.Add(_bar);
        Grid.SetRow(progressPanel, 2);
        root.Children.Add(progressPanel);

        var terminalGrid = new Grid();
        terminalGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        terminalGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        var terminalHeader = new Grid
        {
            Padding = new Thickness(12, 8, 12, 8),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 22, 27, 34)),
        };
        terminalHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        terminalHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _terminalCaption.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 230, 237, 243));
        _terminalMeta.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 148, 158));
        terminalHeader.Children.Add(_terminalCaption);
        Grid.SetColumn(_terminalMeta, 1);
        terminalHeader.Children.Add(_terminalMeta);
        terminalGrid.Children.Add(terminalHeader);

        _terminalText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 201, 209, 217));
        _terminalScroll.Content = _terminalText;
        _terminalScroll.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 13, 17, 23));
        Grid.SetRow(_terminalScroll, 1);
        terminalGrid.Children.Add(_terminalScroll);
        var terminalCard = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 48, 54, 61)),
            Child = terminalGrid,
        };
        Grid.SetRow(terminalCard, 3);
        root.Children.Add(terminalCard);

        var footer = new Grid { ColumnSpacing = 14 };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var logStack = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        logStack.Children.Add(_logCaption);
        logStack.Children.Add(_logPath);
        footer.Children.Add(logStack);

        _retry.Click += (_, _) => { _ = RunAsync(); };
        _cancel.Click += OnCancelClick;
        _close.Click += (_, _) => AllowCloseAndClose();
        _openLog.Click += OnOpenLogClick;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(_openLog);
        buttons.Children.Add(_retry);
        buttons.Children.Add(_cancel);
        buttons.Children.Add(_close);
        Grid.SetColumn(buttons, 1);
        footer.Children.Add(buttons);
        Grid.SetRow(footer, 4);
        root.Children.Add(footer);

        Content = root;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(root, "CompanionPrepRoot");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_notice, "CompanionPrepNotice");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_bar, "CompanionPrepProgress");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_status, "CompanionPrepStatus");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_terminalScroll, "CompanionPrepOutput");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_logPath, "CompanionPrepLogPath");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_openLog, "CompanionPrepOpenLog");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_cancel, "CompanionPrepCancel");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_retry, "CompanionPrepRetry");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(_close, "CompanionPrepClose");

        try
        {
            var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
            const int w = 760, h = 560;
            AppWindow.Resize(new SizeInt32(w, h));
            AppWindow.Move(new PointInt32(
                area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - w) / 2),
                area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - h) / 2)));
        }
        catch { }

        Loc.I.LanguageChanged += OnLanguageChanged;
        AppWindow.Closing += OnAppWindowClosing;
        Closed += (_, _) =>
        {
            _closed = true;
            try { _cts?.Cancel(); } catch { }
            try { _log?.Dispose(); } catch { }
            Loc.I.LanguageChanged -= OnLanguageChanged;
            AppWindow.Closing -= OnAppWindowClosing;
            if (Open.TryGetValue(_spec.Id, out var current) && ReferenceEquals(current, this))
                Open.Remove(_spec.Id);
        };
        UpdateLanguage();
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
        int attemptId = ++_attemptId;
        _running = true;
        _allowClose = false;
        _cancelRequested = false;
        _closeBlockedNotified = false;
        _noticeKind = NoticeKind.None;
        _notice.IsOpen = false;
        _state = PrepState.Running;
        _retry.Visibility = Visibility.Collapsed;
        _close.Visibility = Visibility.Collapsed;
        _cancel.Visibility = Visibility.Visible;
        _cancel.IsEnabled = true;
        _bar.IsIndeterminate = true;
        _bar.Value = 0;
        _bar.ShowError = false;
        _bar.ShowPaused = false;
        _percent.Text = "";
        SetStatus("Starting companion preparation…", "開始準備隨附 app…");
        ApplyStateVisual();

        _visibleLog.Clear();
        _visibleLogTrimmed = false;
        _visibleLineCount = 0;
        RenderVisibleLog();

        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        try { _log?.Dispose(); } catch { }
        var attemptLog = CompanionBuildLog.Start(_spec.Id, _spec.TitleEn, _spec.TitleZh,
            CompanionAppService.SourcePath(_spec), _spec.ExeName);
        _log = attemptLog;
        UpdateLogPath();
        AppendVisible($"$ winforge prepare {_spec.Id}");
        AppendVisible(Loc.I.Pick($"source  {CompanionAppService.SourcePath(_spec)}",
            $"原始碼  {CompanionAppService.SourcePath(_spec)}"));
        AppendVisible(Loc.I.Pick($"target  {_spec.ExeName}", $"目標    {_spec.ExeName}"));
        if (attemptLog.IsAvailable && attemptLog.FilePath is not null)
            AppendVisible(Loc.I.Pick($"log     {attemptLog.FilePath}", $"記錄    {attemptLog.FilePath}"));
        else
        {
            AppendVisible(Loc.I.Pick($"warning: durable log unavailable: {attemptLog.Error}",
                $"警告：持久記錄無法使用：{attemptLog.Error}"));
            SetNotice(NoticeKind.LogUnavailable, attemptLog.Error ?? "");
        }

        var streamedLines = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        void Audit(InstallProgressReport report)
        {
            if (report.IsOutputLine)
            {
                var line = report.StatusEn ?? report.StatusZh;
                if (!string.IsNullOrWhiteSpace(line)) streamedLines.TryAdd(NormalizeOutputLine(line), 0);
                attemptLog.AppendOutput(line);
            }
            else
                attemptLog.AppendStatus(report.StatusEn, report.StatusZh);
        }

        var progress = new BatchedUiProgress(DispatcherQueue,
            report =>
            {
                if (_closed || attemptId != _attemptId) return;
                OnProgress(report);
            },
            lines =>
            {
                if (_closed || attemptId != _attemptId) return;
                AppendVisible(lines);
            });

        TweakResult result;
        try { result = await CompanionAppService.EnsureNativeAsync(_spec, progress, _cts.Token, Audit); }
        catch (OperationCanceledException) { result = TweakResult.Fail("Cancelled.", "已取消。"); }
        catch (Exception ex) { result = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
        // No more service reports can arrive after the awaited call. Flush any visually-batched tail before
        // changing state or closing, and suppress already-queued phase callbacks from overwriting the result.
        progress.CompleteAndFlush();
        if (_closed || attemptId != _attemptId) return;

        if (result.Code == ShellRunner.ProcessCleanupTimeoutCode)
        {
            _running = false;
            _state = PrepState.Failed;
            _bar.IsIndeterminate = false;
            _bar.ShowError = true;
            var timeoutEn = result.Message?.En ?? "The compiler may still be running.";
            var timeoutZh = result.Message?.Zh ?? "編譯器可能仍然運行緊。";
            SetStatus(timeoutEn, timeoutZh);
            var uniqueTimeoutOutput = UniqueResultOutput(result.Output, streamedLines);
            if (!string.IsNullOrWhiteSpace(uniqueTimeoutOutput)) AppendVisible(uniqueTimeoutOutput);
            AppendVisible(Loc.I.Pick(
                "[warning] cleanup could not be confirmed; retry is disabled",
                "[警告] 無法確認清理完成；已停用重試"));
            attemptLog.Finish(_cancelRequested ? "CANCEL_CLEANUP_TIMEOUT" : "BUILD_QUARANTINED",
                $"{timeoutEn} · {timeoutZh}", uniqueTimeoutOutput);
            UpdateLogPath();
            SetNotice(NoticeKind.CleanupUnconfirmed, "");
            _retry.Visibility = Visibility.Collapsed;
            _cancel.Visibility = Visibility.Collapsed;
            _close.Visibility = Visibility.Visible;
            ApplyStateVisual();
            return;
        }

        if (_cancelRequested || _cts.IsCancellationRequested)
        {
            _running = false;
            _state = PrepState.Cancelling;
            _bar.IsIndeterminate = false;
            _bar.ShowPaused = true;
            SetStatus("Cancelled safely. Cleanup is complete.", "已安全取消，清理完成。");
            AppendVisible(Loc.I.Pick("[cancelled] cleanup complete", "[已取消] 清理完成"));
            var uniqueOutput = UniqueResultOutput(result.Output, streamedLines);
            attemptLog.Finish("CANCELLED", "The user cancelled companion preparation.",
                uniqueOutput);
            UpdateLogPath();
            if (!attemptLog.IsAvailable) SetNotice(NoticeKind.LogUnavailable, attemptLog.Error ?? "");
            ApplyStateVisual();
            await Task.Delay(350);
            if (!_closed) AllowCloseAndClose();
            return;
        }

        if (result.Success)
        {
            _state = PrepState.Complete;
            _cancel.Visibility = Visibility.Collapsed;
            _bar.IsIndeterminate = false;
            _bar.Value = 100;
            _percent.Text = "100%";
            SetStatus($"{_spec.TitleEn} is ready. Launching…", $"{_spec.TitleZh} 已就緒，啟動緊…");
            AppendVisible(Loc.I.Pick("[ready] compilation published successfully", "[已就緒] 編譯已成功發佈"));
            ApplyStateVisual();
            await Task.Delay(450);
            if (_closed) return;
            var launched = CompanionAppService.LaunchNative(_spec);
            if (launched.Success)
            {
                attemptLog.AppendStatus($"Launched {_spec.TitleEn}.", $"已啟動 {_spec.TitleZh}。");
                attemptLog.Finish("SUCCESS", launched.Message?.En ?? "Companion launched.");
                UpdateLogPath();
                if (!attemptLog.IsAvailable)
                {
                    SetNotice(NoticeKind.LogUnavailable, attemptLog.Error ?? "");
                    await Task.Delay(800);
                }
                _running = false;
                AllowCloseAndClose();
                return;
            }
            result = launched;
        }

        _running = false;
        _state = PrepState.Failed;
        _bar.IsIndeterminate = false;
        _bar.ShowError = true;
        var messageEn = result.Message?.En ?? "Preparation failed.";
        var messageZh = result.Message?.Zh ?? "準備失敗。";
        SetStatus(messageEn, messageZh);
        var uniqueFailureOutput = UniqueResultOutput(result.Output, streamedLines);
        if (!string.IsNullOrWhiteSpace(uniqueFailureOutput)) AppendVisible(uniqueFailureOutput);
        AppendVisible(Loc.I.Pick($"[failed] {messageEn}", $"[失敗] {messageZh}"));
        attemptLog.Finish("FAILED", $"{messageEn} · {messageZh}",
            uniqueFailureOutput);
        UpdateLogPath();
        SetNotice(attemptLog.IsAvailable ? NoticeKind.Failed : NoticeKind.LogUnavailable,
            attemptLog.Error ?? "");
        _retry.Visibility = Visibility.Visible;
        _cancel.Visibility = Visibility.Collapsed;
        _close.Visibility = Visibility.Visible;
        ApplyStateVisual();
    }

    private void OnProgress(InstallProgressReport report)
    {
        if (report.IsOutputLine)
        {
            var line = report.StatusEn ?? report.StatusZh;
            if (!string.IsNullOrWhiteSpace(line)) AppendVisible(line);
            return;
        }

        if (report.Percent is double percent)
        {
            // The service reports phase milestones, not byte-accurate compiler progress. Keep the bar
            // indeterminate until the only exact value (completion) rather than implying false precision.
            if (percent >= 100)
            {
                _bar.IsIndeterminate = false;
                _bar.Value = 100;
                _percent.Text = "100%";
            }
            else
            {
                _bar.IsIndeterminate = true;
                _percent.Text = "";
            }
        }
        if (!string.IsNullOrWhiteSpace(report.StatusEn) || !string.IsNullOrWhiteSpace(report.StatusZh))
        {
            SetStatus(report.StatusEn ?? report.StatusZh ?? "", report.StatusZh ?? report.StatusEn ?? "");
            AppendVisible($"> {Loc.I.Pick(report.StatusEn ?? report.StatusZh ?? "",
                report.StatusZh ?? report.StatusEn ?? "")}");
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (!_running || _cancelRequested) return;
        _cancelRequested = true;
        _cancel.IsEnabled = false;
        _state = PrepState.Cancelling;
        SetStatus("Stopping safely after the current operation…", "目前操作完成後安全停止緊…");
        AppendVisible(Loc.I.Pick("[cancel requested] waiting for process cleanup",
            "[已要求取消] 等緊程序清理"));
        _log?.AppendStatus("Cancellation requested; waiting for cleanup.", "已要求取消，等緊清理完成。");
        ApplyStateVisual();
        try { _cts?.Cancel(); } catch { }
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || !_running) return;
        args.Cancel = true;
        SetNotice(NoticeKind.CloseBlocked, "");
        if (_closeBlockedNotified) return;
        _closeBlockedNotified = true;
        AppendVisible(Loc.I.Pick(
            "[close blocked] preparation is still running; use Cancel to stop safely",
            "[已阻止關閉] 準備程序仲運行緊；請用取消安全停止"));
        _log?.AppendStatus(
            "Window close blocked while preparation is running; use Cancel to stop safely.",
            "準備程序運行期間已阻止關閉視窗；請用取消安全停止。");
    }

    private void AllowCloseAndClose()
    {
        if (_closed) return;
        _allowClose = true;
        Close();
    }

    private void OnOpenLogClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var directory = _log?.DirectoryPath ?? CompanionBuildLog.DefaultDirectory;
            Directory.CreateDirectory(directory);
            var file = _log?.FilePath;
            var arguments = file is not null && File.Exists(file) ? $"/select,\"{file}\"" : $"\"{directory}\"";
            if (!UserProcessLauncher.TryStart("explorer.exe", arguments, directory, out var error))
            {
                AppendVisible(Loc.I.Pick($"[log folder] {error}", $"[記錄資料夾] {error}"));
                SetNotice(NoticeKind.LogUnavailable, error);
            }
        }
        catch (Exception ex)
        {
            AppendVisible(Loc.I.Pick($"[log folder] {ex.Message}", $"[記錄資料夾] {ex.Message}"));
            SetNotice(NoticeKind.LogUnavailable, ex.Message);
        }
    }

    private void AppendVisible(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;
        foreach (var raw in value.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            _visibleLog.AppendLine(raw.Replace("\0", "", StringComparison.Ordinal));
            _visibleLineCount++;
        }
        if (_visibleLog.Length > MaxVisibleLogChars)
        {
            int removeThrough = _visibleLog.ToString().IndexOf('\n', _visibleLog.Length - MaxVisibleLogChars);
            if (removeThrough >= 0) _visibleLog.Remove(0, removeThrough + 1);
            else _visibleLog.Remove(0, _visibleLog.Length - MaxVisibleLogChars);
            _visibleLogTrimmed = true;
        }
        RenderVisibleLog();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (_closed) return;
            _terminalScroll.UpdateLayout();
            _terminalScroll.ChangeView(null, _terminalScroll.ScrollableHeight, null, true);
        });
    }

    private void RenderVisibleLog()
    {
        var prefix = _visibleLogTrimmed
            ? _log?.IsAvailable == true
                ? Loc.I.Pick("[Earlier on-screen output omitted; the log file is complete.]\n",
                    "[較早嘅畫面輸出已省略；記錄檔保留完整內容。]\n")
                : Loc.I.Pick("[Earlier on-screen output omitted; the persistent log is incomplete.]\n",
                    "[較早嘅畫面輸出已省略；持久記錄並唔完整。]\n")
            : "";
        _terminalText.Text = prefix + _visibleLog;
        _terminalMeta.Text = Loc.I.Pick($"{_visibleLineCount} lines", $"{_visibleLineCount} 行");
    }

    private void SetStatus(string en, string zh)
    {
        _statusEn = en;
        _statusZh = zh;
        _status.Text = Loc.I.Pick(en, zh);
    }

    private void SetNotice(NoticeKind kind, string detail)
    {
        _noticeKind = kind;
        _noticeDetail = detail;
        ApplyNotice();
    }

    private void ApplyNotice()
    {
        switch (_noticeKind)
        {
            case NoticeKind.None:
                _notice.IsOpen = false;
                return;
            case NoticeKind.CloseBlocked:
                _notice.Severity = InfoBarSeverity.Warning;
                _notice.Title = Loc.I.Pick("Preparation is still running", "準備程序仲運行緊");
                _notice.Message = Loc.I.Pick(
                    "Use Cancel to stop safely. This window will close after process cleanup finishes.",
                    "請撳取消安全停止。程序清理完成後，呢個視窗先會關閉。");
                break;
            case NoticeKind.Failed:
                _notice.Severity = InfoBarSeverity.Error;
                _notice.Title = Loc.I.Pick("Preparation failed", "準備失敗");
                _notice.Message = _log?.IsAvailable == true && _log.FilePath is string path
                    ? Loc.I.Pick($"Full output was saved to {path}", $"完整輸出已儲存到 {path}")
                    : Loc.I.Pick("See the compiler output above, then retry.", "請睇上面嘅編譯器輸出，再試一次。");
                break;
            case NoticeKind.CleanupUnconfirmed:
                _notice.Severity = InfoBarSeverity.Error;
                _notice.Title = Loc.I.Pick("Process cleanup could not be confirmed",
                    "無法確認程序清理完成");
                _notice.Message = Loc.I.Pick(
                    "The compiler or installer may still be running. Retry is disabled; check Task Manager, then restart WinForge before another native build.",
                    "編譯器或安裝程式可能仍然運行緊。已停用重試；請檢查工作管理員，再重開 WinForge 先做另一個原生編譯。");
                break;
            default:
                _notice.Severity = InfoBarSeverity.Warning;
                _notice.Title = Loc.I.Pick("The build log is unavailable", "編譯記錄無法使用");
                _notice.Message = _state == PrepState.Failed
                    ? Loc.I.Pick(
                        $"The persistent log could not be completed. Use the compiler output above. {_noticeDetail}",
                        $"無法完成持久記錄。請使用上面嘅編譯器輸出。{_noticeDetail}")
                    : _state == PrepState.Complete
                        ? Loc.I.Pick(
                            $"The companion launched, but its persistent build log is incomplete. {_noticeDetail}",
                            $"隨附 app 已啟動，但持久編譯記錄並唔完整。{_noticeDetail}")
                    : _state == PrepState.Cancelling
                        ? Loc.I.Pick(
                            $"Preparation was cancelled, but its persistent log is incomplete. {_noticeDetail}",
                            $"準備程序已取消，但持久記錄並唔完整。{_noticeDetail}")
                    : Loc.I.Pick(
                        $"Preparation will continue, but the persistent log could not be opened. {_noticeDetail}",
                        $"準備程序會繼續，但無法開啟持久記錄。{_noticeDetail}");
                break;
        }
        _notice.IsOpen = true;
    }

    private void ApplyStateVisual()
    {
        (string en, string zh, Windows.UI.Color background, Windows.UI.Color foreground) = _state switch
        {
            PrepState.Running => ("Running", "運行緊",
                Windows.UI.Color.FromArgb(50, 47, 129, 247), Windows.UI.Color.FromArgb(255, 88, 166, 255)),
            PrepState.Cancelling => ("Stopping", "停止緊",
                Windows.UI.Color.FromArgb(50, 210, 153, 34), Windows.UI.Color.FromArgb(255, 240, 188, 76)),
            PrepState.Failed => ("Failed", "失敗",
                Windows.UI.Color.FromArgb(50, 248, 81, 73), Windows.UI.Color.FromArgb(255, 255, 123, 114)),
            PrepState.Complete => ("Ready", "已就緒",
                Windows.UI.Color.FromArgb(50, 46, 160, 67), Windows.UI.Color.FromArgb(255, 86, 211, 100)),
            _ => ("Starting", "開始緊",
                Windows.UI.Color.FromArgb(50, 139, 148, 158), Windows.UI.Color.FromArgb(255, 173, 181, 189)),
        };
        _stateText.Text = Loc.I.Pick(en, zh);
        _stateBadge.Background = new SolidColorBrush(background);
        _stateText.Foreground = new SolidColorBrush(foreground);
    }

    private void UpdateLogPath()
    {
        var path = _log?.FilePath;
        _logPath.Text = _log?.IsAvailable == true && path is not null
            ? path
            : path is not null
                ? Loc.I.Pick($"Incomplete log: {path}", $"不完整記錄：{path}")
                : Loc.I.Pick("Persistent log unavailable", "持久記錄無法使用");
        ToolTipService.SetToolTip(_logPath, path ?? _log?.Error ?? "");
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => UpdateLanguage();

    private void UpdateLanguage()
    {
        _description.Text = Loc.I.Pick(
            "First run: WinForge compiles this app from its shipped C++ source. A toolchain installs automatically if needed.",
            "首次執行：WinForge 會由隨附嘅 C++ 原始碼編譯呢個 app；需要時會自動安裝工具鏈。");
        _terminalCaption.Text = Loc.I.Pick("Live compiler output", "即時編譯器輸出");
        _logCaption.Text = Loc.I.Pick("Complete build log", "完整編譯記錄");
        _openLog.Content = Loc.I.Pick("Open log", "開啟記錄");
        _retry.Content = Loc.I.Pick("Retry", "再試");
        _cancel.Content = Loc.I.Pick("Cancel", "取消");
        _close.Content = Loc.I.Pick("Close", "關閉");
        _status.Text = Loc.I.Pick(_statusEn, _statusZh);
        ApplyStateVisual();
        ApplyNotice();
        UpdateLogPath();
        RenderVisibleLog();
    }

    private static string NormalizeOutputLine(string value) =>
        value.Replace("\r", "", StringComparison.Ordinal).Trim();

    /// <summary>
    /// Return only diagnostics that were not already persisted by the live stdout/stderr sink. This keeps
    /// ordinary compiler failures free of duplicate blocks while retaining later publish exceptions.
    /// </summary>
    private static string? UniqueResultOutput(string? captured,
        ConcurrentDictionary<string, byte> streamedLines)
    {
        if (string.IsNullOrWhiteSpace(captured)) return null;
        var unique = new StringBuilder();
        foreach (var raw in captured.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var normalized = NormalizeOutputLine(raw);
            if (normalized.Length == 0 || streamedLines.ContainsKey(normalized)) continue;
            unique.AppendLine(raw);
        }
        return unique.Length == 0 ? null : unique.ToString().TrimEnd();
    }

    /// <summary>
    /// Batch chatty stdout/stderr before touching XAML. Durable audit logging still happens synchronously in
    /// the service; this adapter only coalesces visual updates so Cancel and window-close remain responsive.
    /// </summary>
    private sealed class BatchedUiProgress : IProgress<InstallProgressReport>
    {
        private const int MaxLinesPerBatch = 256;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
        private readonly Action<InstallProgressReport> _onStatus;
        private readonly Action<string> _onLines;
        private readonly ConcurrentQueue<string> _lines = new();
        private int _scheduled;
        private int _completed;

        public BatchedUiProgress(Microsoft.UI.Dispatching.DispatcherQueue dispatcher,
            Action<InstallProgressReport> onStatus, Action<string> onLines)
        {
            _dispatcher = dispatcher;
            _onStatus = onStatus;
            _onLines = onLines;
        }

        public void Report(InstallProgressReport value)
        {
            if (Volatile.Read(ref _completed) != 0) return;
            if (!value.IsOutputLine)
            {
                _dispatcher.TryEnqueue(() =>
                {
                    if (Volatile.Read(ref _completed) == 0) _onStatus(value);
                });
                return;
            }

            var line = value.StatusEn ?? value.StatusZh;
            if (string.IsNullOrWhiteSpace(line)) return;
            _lines.Enqueue(line);
            ScheduleDrain();
        }

        private void ScheduleDrain()
        {
            if (Interlocked.CompareExchange(ref _scheduled, 1, 0) != 0) return;
            if (!_dispatcher.TryEnqueue(Drain)) Interlocked.Exchange(ref _scheduled, 0);
        }

        private void Drain()
        {
            DrainBatch();
            Interlocked.Exchange(ref _scheduled, 0);
            if (!_lines.IsEmpty && Volatile.Read(ref _completed) == 0) ScheduleDrain();
        }

        private void DrainBatch()
        {
            var batch = new StringBuilder();
            int count = 0;
            while (count < MaxLinesPerBatch && _lines.TryDequeue(out var line))
            {
                if (batch.Length > 0) batch.Append('\n');
                batch.Append(line);
                count++;
            }
            if (batch.Length > 0) _onLines(batch.ToString());
        }

        /// <summary>Called on the window dispatcher after the service task completes.</summary>
        public void CompleteAndFlush()
        {
            Interlocked.Exchange(ref _completed, 1);
            while (!_lines.IsEmpty) DrainBatch();
            Interlocked.Exchange(ref _scheduled, 0);
        }
    }

    private void ApplyWindowTheme(Grid root)
    {
        bool dark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
        try
        {
            if (App.Shell?.Content is FrameworkElement shellRoot)
            {
                var theme = shellRoot.RequestedTheme != ElementTheme.Default
                    ? shellRoot.RequestedTheme
                    : shellRoot.ActualTheme;
                if (theme != ElementTheme.Default) dark = theme == ElementTheme.Dark;
            }
        }
        catch { }
        root.RequestedTheme = dark ? ElementTheme.Dark : ElementTheme.Light;
        root.Background = new SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 32, 32, 32)
            : Windows.UI.Color.FromArgb(255, 249, 249, 249));
        _description.Foreground = new SolidColorBrush(dark
            ? Windows.UI.Color.FromArgb(255, 190, 190, 190)
            : Windows.UI.Color.FromArgb(255, 95, 95, 95));
    }
}
