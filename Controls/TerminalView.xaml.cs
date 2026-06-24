using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Renci.SshNet;
using Windows.System;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 一個自包含嘅互動式 SSH 終端機 · A self-contained interactive SSH terminal.
/// 喺背景執行緒讀 SSH.NET <see cref="ShellStream"/>，解析基本 ANSI（SGR 顏色 + 游標清除 + CR/BS），
/// 再渲染入 RichTextBlock；輸入框將行送返去個 stream。呢個係 v1（行導向 shell）— 全螢幕 TUI
/// （vim/top）唔會完美渲染，ConPTY 真 PTY 留俾下一輪。Reads the ShellStream on a background
/// thread, parses basic ANSI (SGR colours, erase, CR/BS), renders into a RichTextBlock, and sends
/// typed lines back. Line-oriented v1; full-screen TUIs are out of scope (ConPTY is the later pass).
/// </summary>
public sealed partial class TerminalView : UserControl
{
    private SshClient? _client;
    private ShellStream? _stream;
    private CancellationTokenSource? _cts;
    private Paragraph _para = new();

    private const char ESC = '';
    private const char BEL = '';
    private const char ETX = ''; // Ctrl+C

    // ANSI render state
    private Color _fg = Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6);
    private bool _bold;
    private Run? _line; // current line's trailing run (so backspace/CR can edit it)
    private readonly StringBuilder _lineText = new();

    public event EventHandler? Disconnected;

    public bool IsConnected => _client?.IsConnected == true;

    public TerminalView()
    {
        InitializeComponent();
        OutBlock.Blocks.Add(_para);
        Unloaded += (_, _) => Stop();
    }

    /// <summary>用一個設定檔同已解密嘅秘密開啟 session · Start a session for a profile + revealed secret.</summary>
    public async Task StartAsync(SshProfile profile, string secret)
    {
        Stop();
        AppendSystem($"Connecting to {profile.Display} …\n", $"連線去 {profile.Display} …\n");
        try
        {
            var (client, stream) = await Task.Run(() => SshService.OpenShell(profile, secret));
            _client = client;
            _stream = stream;
            _cts = new CancellationTokenSource();
            InputBox.IsEnabled = true;
            CtrlCBtn.IsEnabled = true;
            InputBox.Focus(FocusState.Programmatic);
            _ = ReadLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            AppendSystem($"Connection failed: {ex.Message}\n", $"連線失敗：{ex.Message}\n");
            RaiseDisconnected();
        }
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _stream?.Dispose(); } catch { }
        try { if (_client?.IsConnected == true) _client.Disconnect(); _client?.Dispose(); } catch { }
        _stream = null; _client = null; _cts = null;
        if (InputBox is not null) { InputBox.IsEnabled = false; CtrlCBtn.IsEnabled = false; }
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && _stream is { } s && s.CanRead)
            {
                int n = await s.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
                if (n <= 0) { await Task.Delay(40, ct); if (_client?.IsConnected != true) break; continue; }
                var text = Encoding.UTF8.GetString(buffer, 0, n);
                DispatcherQueue.TryEnqueue(() => Feed(text));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DispatcherQueue.TryEnqueue(() => AppendSystem($"\n[disconnected: {ex.Message}]\n", $"\n[已斷線：{ex.Message}]\n"));
        }
        DispatcherQueue.TryEnqueue(RaiseDisconnected);
    }

    private void RaiseDisconnected()
    {
        InputBox.IsEnabled = false;
        CtrlCBtn.IsEnabled = false;
        Disconnected?.Invoke(this, EventArgs.Empty);
    }

    // ---- ANSI feed ---------------------------------------------------------

    private void Feed(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == ESC)
            {
                // CSI sequence: ESC [ ... letter
                if (i + 1 < text.Length && text[i + 1] == '[')
                {
                    int j = i + 2;
                    while (j < text.Length && !char.IsLetter(text[j])) j++;
                    if (j < text.Length)
                    {
                        var seq = text.Substring(i + 2, j - (i + 2));
                        HandleCsi(seq, text[j]);
                        i = j;
                        continue;
                    }
                }
                // OSC sequence: ESC ] ... BEL — skip (titles etc.)
                if (i + 1 < text.Length && text[i + 1] == ']')
                {
                    int j = i + 2;
                    while (j < text.Length && text[j] != BEL) j++;
                    i = j;
                    continue;
                }
                continue; // unknown escape — drop
            }
            switch (c)
            {
                case '\r': _lineText.Clear(); FlushLine(); break;
                case '\n': CommitLine(); break;
                case '\b':
                    if (_lineText.Length > 0) { _lineText.Length--; FlushLine(); }
                    break;
                case BEL: break; // bell
                case '\t': _lineText.Append("    "); FlushLine(); break;
                default:
                    if (!char.IsControl(c)) { _lineText.Append(c); FlushLine(); }
                    break;
            }
        }
        AutoScroll();
    }

    private void HandleCsi(string args, char final)
    {
        switch (final)
        {
            case 'm': ApplySgr(args); break;
            case 'K': // erase in line — clear current line buffer
                if (args is "" or "0" or "2") { _lineText.Clear(); FlushLine(); }
                break;
            case 'J': // erase in display — just drop trailing content of this line
                _lineText.Clear(); FlushLine();
                break;
            // cursor moves (H, A, B, C, D, etc.) are ignored in this line-oriented v1
        }
    }

    private void ApplySgr(string args)
    {
        if (string.IsNullOrEmpty(args)) { ResetSgr(); return; }
        foreach (var part in args.Split(';'))
        {
            if (!int.TryParse(part, out int code)) continue;
            switch (code)
            {
                case 0: ResetSgr(); break;
                case 1: _bold = true; break;
                case 22: _bold = false; break;
                case 39: _fg = Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6); break;
                case >= 30 and <= 37: _fg = Ansi16(code - 30, false); break;
                case >= 90 and <= 97: _fg = Ansi16(code - 90, true); break;
            }
        }
        // colour change starts a fresh run
        _line = null;
    }

    private void ResetSgr()
    {
        _bold = false;
        _fg = Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6);
        _line = null;
    }

    private static Color Ansi16(int idx, bool bright) => idx switch
    {
        0 => bright ? Color.FromArgb(0xFF, 0x66, 0x66, 0x66) : Color.FromArgb(0xFF, 0x2E, 0x2E, 0x2E),
        1 => bright ? Color.FromArgb(0xFF, 0xFF, 0x6E, 0x67) : Color.FromArgb(0xFF, 0xE0, 0x4A, 0x42),
        2 => bright ? Color.FromArgb(0xFF, 0x5A, 0xF7, 0x8E) : Color.FromArgb(0xFF, 0x3E, 0xC4, 0x6A),
        3 => bright ? Color.FromArgb(0xFF, 0xF3, 0xF9, 0x9D) : Color.FromArgb(0xFF, 0xD3, 0xBE, 0x3A),
        4 => bright ? Color.FromArgb(0xFF, 0x6E, 0xA8, 0xFF) : Color.FromArgb(0xFF, 0x49, 0x7A, 0xE0),
        5 => bright ? Color.FromArgb(0xFF, 0xD3, 0x7E, 0xFF) : Color.FromArgb(0xFF, 0xB0, 0x55, 0xD0),
        6 => bright ? Color.FromArgb(0xFF, 0x5A, 0xF7, 0xF7) : Color.FromArgb(0xFF, 0x3E, 0xC4, 0xC4),
        7 => bright ? Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF) : Color.FromArgb(0xFF, 0xC8, 0xC8, 0xC8),
        _ => Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6),
    };

    /// <summary>Re-render the working line's trailing run from the line buffer.</summary>
    private void FlushLine()
    {
        if (_line is null)
        {
            _line = new Run { Foreground = new SolidColorBrush(_fg), FontWeight = _bold ? FontWeights.Bold : FontWeights.Normal };
            _para.Inlines.Add(_line);
        }
        _line.Text = _lineText.ToString();
    }

    /// <summary>Commit the current line and start a new paragraph line.</summary>
    private void CommitLine()
    {
        FlushLine();
        _para.Inlines.Add(new LineBreak());
        _lineText.Clear();
        _line = null;

        // keep the buffer bounded
        if (_para.Inlines.Count > 4000)
            for (int k = 0; k < 800 && _para.Inlines.Count > 0; k++)
                _para.Inlines.RemoveAt(0);
    }

    private void AutoScroll() => OutScroller.ChangeView(null, OutScroller.ScrollableHeight, null, true);

    private void AppendSystem(string en, string zh)
    {
        var run = new Run
        {
            Text = Loc.I.Pick(en, zh),
            Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x8A, 0xB4, 0xF8)),
            FontStyle = Windows.UI.Text.FontStyle.Italic,
        };
        _para.Inlines.Add(run);
        _line = null;
        AutoScroll();
    }

    // ---- input -------------------------------------------------------------

    private void InputBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter) return;
        e.Handled = true;
        var line = InputBox.Text;
        InputBox.Text = string.Empty;
        try { _stream?.WriteLine(line); _stream?.Flush(); } catch { }
    }

    private void CtrlC_Click(object sender, RoutedEventArgs e)
    {
        try { _stream?.Write(ETX.ToString()); _stream?.Flush(); } catch { }
        InputBox.Focus(FocusState.Programmatic);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _para.Inlines.Clear();
        _lineText.Clear();
        _line = null;
    }
}
