using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 可重用嘅內嵌終端機面板 · A reusable embedded-terminal panel: wraps a real ConPTY-backed
/// <see cref="ConPtySession"/> rendered by the shared <see cref="TerminalView"/>, plus a small
/// Connect / Disconnect / Clear toolbar and live status line. 任何模組想喺相關工作目錄／shell
/// 開一個真正嘅終端機（支援全螢幕 TUI：vim、htop、tmux）都可以擺呢個 control 落去再叫
/// <see cref="Configure"/>。Drop this control anywhere a module wants a genuine terminal at a
/// relevant working directory or command (full-screen TUIs work because it is a true PTY), then call
/// <see cref="Configure"/>. Tear-down is automatic on Unloaded; the host can also call
/// <see cref="Stop"/> explicitly. 全部介面文字雙語。 All UI strings are bilingual.
/// </summary>
public sealed partial class EmbeddedTerminalPanel : UserControl
{
    private ConPtySession? _pty;
    private string _commandLine = "powershell.exe";
    private string? _workingDir;
    private bool _autoStart;
    private bool _configured;

    /// <summary>子程序結束時觸發（帶結束代碼）· Raised when the hosted shell process exits.</summary>
    public event Action<int>? Exited;

    public EmbeddedTerminalPanel()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Term.SizeChangedCols += (cols, rows) => _pty?.Resize((short)cols, (short)rows);
        Term.SendInput += t => _pty?.Write(t);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RenderLabels();
        RefreshStatus();
    }

    /// <summary>終端機而家有冇連線 · Whether a shell is currently running.</summary>
    public bool IsRunning => _pty is { IsRunning: true };

    /// <summary>
    /// 設定要開咩 shell／命令同工作目錄 · Configure which command line + working directory the panel
    /// should spawn. <paramref name="autoStart"/> 為真就一 Loaded 即刻開（適合對話框）。Set
    /// <paramref name="autoStart"/> to launch as soon as the control is loaded (handy in a dialog).
    /// </summary>
    public void Configure(string commandLine, string? workingDir = null, bool autoStart = false)
    {
        if (!string.IsNullOrWhiteSpace(commandLine)) _commandLine = commandLine;
        _workingDir = workingDir;
        _autoStart = autoStart;
        _configured = true;
        if (autoStart && IsLoaded && !IsRunning) StartTerminal();
        RefreshStatus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_autoStart && _configured && !IsRunning) StartTerminal();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Stop();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) { RenderLabels(); RefreshStatus(); }

    private void RenderLabels()
    {
        StartBtn.Content = P("Connect", "連線");
        StopBtn.Content = P("Disconnect", "斷線");
        ClearBtn.Content = P("Clear", "清除");
    }

    private void Start_Click(object sender, RoutedEventArgs e) => StartTerminal();

    private void Stop_Click(object sender, RoutedEventArgs e) => Stop();

    private void Clear_Click(object sender, RoutedEventArgs e) => Term.ClearScreen();

    /// <summary>啟動終端機 · Spawn the configured shell and wire its stream to the view.</summary>
    public void StartTerminal()
    {
        if (IsRunning) Stop();
        Term.ClearScreen();
        try
        {
            _pty = new ConPtySession();
            _pty.OutputReceived += s => Term.Feed(s);
            _pty.Exited += code => DispatcherQueue.TryEnqueue(() =>
            {
                Term.Feed($"\r\n[{P("process exited", "程序結束")}: {code}]\r\n");
                CleanupAfterExit();
                Exited?.Invoke(code);
            });
            _pty.Start(_commandLine, (short)Term.Columns, (short)Term.Rows,
                string.IsNullOrWhiteSpace(_workingDir) ? null : _workingDir);
            StartBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            Term.Focus(FocusState.Programmatic);
        }
        catch (Exception ex)
        {
            _pty = null;
            Term.Feed($"\r\n[{P("could not start", "開唔到")}: {ex.Message}]\r\n");
        }
        RefreshStatus();
    }

    /// <summary>停止並釋放終端機 · Stop and dispose the session (deterministic tear-down).</summary>
    public void Stop()
    {
        try { _pty?.Dispose(); } catch { }
        _pty = null;
        CleanupAfterExit();
    }

    private void CleanupAfterExit()
    {
        if (StartBtn is not null) StartBtn.IsEnabled = true;
        if (StopBtn is not null) StopBtn.IsEnabled = false;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (StatusText is null) return;
        StatusText.Text = IsRunning
            ? P("Running — click the terminal and type. Ctrl+C interrupts.",
                "執行中 — 撳一下終端機就可以打字。Ctrl+C 中斷。")
            : P("Stopped.", "已停止。");
    }
}
