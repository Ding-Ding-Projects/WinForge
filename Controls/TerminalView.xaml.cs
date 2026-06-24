using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// 共用終端機檢視 · A shared ANSI-rendering terminal control fed by a byte/char stream.
/// 由 SSH 模組（handoff 02）同 Windows Terminal 模組共用：handoff 02 餵 SSH ShellStream，
/// Windows Terminal 模組餵本地 <see cref="ConPtySession"/>。
/// Shared by the SSH module (feeds an SSH ShellStream) and the Windows Terminal module (feeds a local
/// ConPTY). It owns a fixed-size screen buffer, a pragmatic VT/ANSI parser (SGR colours, cursor moves,
/// erase, scroll) and forwards keystrokes back to whoever wired up <see cref="SendInput"/>.
/// </summary>
public sealed partial class TerminalView : UserControl
{
    /// <summary>使用者輸入（包含已轉成 VT 的特殊鍵）· Raised with text/keys to send to the host (PTY/SSH).</summary>
    public event Action<string>? SendInput;

    private int _cols = 80;
    private int _rows = 25;

    // Screen buffer: rows x cols of cells.
    private Cell[,] _buf = new Cell[25, 80];
    private int _cx, _cy;                  // cursor x (col), y (row)
    private Color _fg = DefaultFg;
    private Color _bg = DefaultBg;
    private bool _bold;
    private readonly StringBuilder _esc = new();
    private ParseState _state = ParseState.Normal;

    private static readonly Color DefaultFg = Color.FromArgb(0xFF, 0xCC, 0xCC, 0xCC);
    private static readonly Color DefaultBg = Color.FromArgb(0xFF, 0x0C, 0x0C, 0x0C);

    private enum ParseState { Normal, Escape, Csi, Osc }

    private struct Cell
    {
        public char Ch;
        public Color Fg;
        public Color Bg;
        public bool Bold;
    }

    public TerminalView()
    {
        InitializeComponent();
        ResetBuffer(_cols, _rows);

        IsTabStop = true;
        InputCatcher.PointerPressed += (_, _) => Focus(FocusState.Programmatic);
        // Capture keystrokes at the control level.
        PreviewKeyDown += OnPreviewKeyDown;
        CharacterReceived += OnCharacterReceived;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>目前欄數 · Current column count.</summary>
    public int Columns => _cols;

    /// <summary>目前列數 · Current row count.</summary>
    public int Rows => _rows;

    /// <summary>尺寸改變時通知宿主（PTY 要 resize）· Raised when the visible grid size changes.</summary>
    public event Action<int, int>? SizeChangedCols;

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Roughly derive cols/rows from pixel size and the monospace cell metrics.
        double charW = Surface.FontSize * 0.6;       // Cascadia Mono advance ≈ 0.6em
        double lineH = 17;
        int cols = Math.Max(20, (int)((e.NewSize.Width - 20) / charW));
        int rows = Math.Max(6, (int)((e.NewSize.Height - 20) / lineH));
        if (cols == _cols && rows == _rows) return;
        Resize(cols, rows);
        SizeChangedCols?.Invoke(_cols, _rows);
    }

    /// <summary>重設緩衝區大小 · Resize the screen buffer, preserving as much content as fits.</summary>
    public void Resize(int cols, int rows)
    {
        cols = Math.Clamp(cols, 20, 400);
        rows = Math.Clamp(rows, 6, 200);
        var old = _buf;
        var oldRows = _rows;
        var oldCols = _cols;
        var nb = new Cell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                nb[r, c] = Blank();
        int copyRows = Math.Min(rows, oldRows);
        int copyCols = Math.Min(cols, oldCols);
        for (int r = 0; r < copyRows; r++)
            for (int c = 0; c < copyCols; c++)
                nb[r, c] = old[r, c];
        _buf = nb;
        _cols = cols;
        _rows = rows;
        _cx = Math.Min(_cx, cols - 1);
        _cy = Math.Min(_cy, rows - 1);
        Render();
    }

    private void ResetBuffer(int cols, int rows)
    {
        _buf = new Cell[rows, cols];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
                _buf[r, c] = Blank();
        _cx = _cy = 0;
        _cols = cols;
        _rows = rows;
    }

    private Cell Blank() => new() { Ch = ' ', Fg = DefaultFg, Bg = DefaultBg, Bold = false };

    /// <summary>清空畫面 · Clear the whole screen.</summary>
    public void ClearScreen()
    {
        ResetBuffer(_cols, _rows);
        Render();
    }

    // ===================== Feed (from PTY/SSH) =====================

    /// <summary>餵入一段輸出（可能含 ANSI 控制碼）· Feed a chunk of host output (may contain ANSI).</summary>
    public void Feed(string text)
    {
        // Marshal to UI thread; the read loop runs on a background thread.
        if (DispatcherQueue.HasThreadAccess) { Process(text); Render(); }
        else DispatcherQueue.TryEnqueue(() => { Process(text); Render(); });
    }

    private void Process(string text)
    {
        foreach (var ch in text)
        {
            switch (_state)
            {
                case ParseState.Normal:
                    if (ch == '\x1b') { _state = ParseState.Escape; _esc.Clear(); }
                    else HandlePrintable(ch);
                    break;

                case ParseState.Escape:
                    if (ch == '[') { _state = ParseState.Csi; _esc.Clear(); }
                    else if (ch == ']') { _state = ParseState.Osc; _esc.Clear(); }
                    else if (ch == '(' || ch == ')') { /* charset select; consume one more */ _state = ParseState.Normal; }
                    else { _state = ParseState.Normal; } // ignore other escapes (RI, etc.)
                    break;

                case ParseState.Csi:
                    if ((ch >= '0' && ch <= '9') || ch == ';' || ch == '?' || ch == ' ')
                        _esc.Append(ch);
                    else { HandleCsi(ch, _esc.ToString()); _state = ParseState.Normal; }
                    break;

                case ParseState.Osc:
                    // OSC ... terminated by BEL (\x07) or ST (\x1b\\). We just swallow it (titles etc.).
                    if (ch == '\x07') _state = ParseState.Normal;
                    else if (ch == '\x1b') { /* expect '\\' next; simplest: go normal */ _state = ParseState.Normal; }
                    break;
            }
        }
    }

    private void HandlePrintable(char ch)
    {
        switch (ch)
        {
            case '\r': _cx = 0; return;
            case '\n': NewLine(); return;
            case '\b': if (_cx > 0) _cx--; return;
            case '\t': _cx = Math.Min(_cols - 1, (_cx + 8) & ~7); return;
            case '\x07': return; // bell
        }
        if (ch < ' ') return;

        if (_cx >= _cols) { _cx = 0; NewLine(); }
        _buf[_cy, _cx] = new Cell { Ch = ch, Fg = _fg, Bg = _bg, Bold = _bold };
        _cx++;
    }

    private void NewLine()
    {
        _cy++;
        if (_cy >= _rows)
        {
            ScrollUp();
            _cy = _rows - 1;
        }
    }

    private void ScrollUp()
    {
        for (int r = 0; r < _rows - 1; r++)
            for (int c = 0; c < _cols; c++)
                _buf[r, c] = _buf[r + 1, c];
        for (int c = 0; c < _cols; c++)
            _buf[_rows - 1, c] = Blank();
    }

    private void HandleCsi(char final, string paramStr)
    {
        // Parse numeric params.
        var ps = paramStr.TrimStart('?').TrimEnd(' ');
        var parts = ps.Split(';');
        int Arg(int i, int def) =>
            i < parts.Length && int.TryParse(parts[i], out var v) ? v : def;

        switch (final)
        {
            case 'H': case 'f': // cursor position (1-based)
                _cy = Math.Clamp(Arg(0, 1) - 1, 0, _rows - 1);
                _cx = Math.Clamp(Arg(1, 1) - 1, 0, _cols - 1);
                break;
            case 'A': _cy = Math.Max(0, _cy - Arg(0, 1)); break;
            case 'B': _cy = Math.Min(_rows - 1, _cy + Arg(0, 1)); break;
            case 'C': _cx = Math.Min(_cols - 1, _cx + Arg(0, 1)); break;
            case 'D': _cx = Math.Max(0, _cx - Arg(0, 1)); break;
            case 'G': _cx = Math.Clamp(Arg(0, 1) - 1, 0, _cols - 1); break;
            case 'd': _cy = Math.Clamp(Arg(0, 1) - 1, 0, _rows - 1); break;
            case 'J': EraseDisplay(Arg(0, 0)); break;
            case 'K': EraseLine(Arg(0, 0)); break;
            case 'P': DeleteChars(Arg(0, 1)); break;
            case 'X': EraseChars(Arg(0, 1)); break;
            case 'm': ApplySgr(parts); break;
            case 's': _savedX = _cx; _savedY = _cy; break;
            case 'u': _cx = _savedX; _cy = _savedY; break;
            // 'h'/'l' (mode set/reset, e.g. ?25 cursor visibility, ?1049 alt buffer) — ignored safely.
            default: break;
        }
    }

    private int _savedX, _savedY;

    private void EraseDisplay(int mode)
    {
        if (mode == 2 || mode == 3) { for (int r = 0; r < _rows; r++) for (int c = 0; c < _cols; c++) _buf[r, c] = Blank(); _cx = _cy = 0; return; }
        if (mode == 0) // cursor to end
        {
            for (int c = _cx; c < _cols; c++) _buf[_cy, c] = Blank();
            for (int r = _cy + 1; r < _rows; r++) for (int c = 0; c < _cols; c++) _buf[r, c] = Blank();
        }
        else if (mode == 1) // start to cursor
        {
            for (int r = 0; r < _cy; r++) for (int c = 0; c < _cols; c++) _buf[r, c] = Blank();
            for (int c = 0; c <= _cx && c < _cols; c++) _buf[_cy, c] = Blank();
        }
    }

    private void EraseLine(int mode)
    {
        if (mode == 0) for (int c = _cx; c < _cols; c++) _buf[_cy, c] = Blank();
        else if (mode == 1) for (int c = 0; c <= _cx && c < _cols; c++) _buf[_cy, c] = Blank();
        else for (int c = 0; c < _cols; c++) _buf[_cy, c] = Blank();
    }

    private void EraseChars(int n)
    {
        for (int i = 0; i < n && _cx + i < _cols; i++) _buf[_cy, _cx + i] = Blank();
    }

    private void DeleteChars(int n)
    {
        for (int c = _cx; c < _cols; c++)
            _buf[_cy, c] = (c + n < _cols) ? _buf[_cy, c + n] : Blank();
    }

    private void ApplySgr(string[] parts)
    {
        if (parts.Length == 0 || (parts.Length == 1 && parts[0] == ""))
        { _fg = DefaultFg; _bg = DefaultBg; _bold = false; return; }

        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out var n)) { if (parts[i] == "") n = 0; else continue; }
            switch (n)
            {
                case 0: _fg = DefaultFg; _bg = DefaultBg; _bold = false; break;
                case 1: _bold = true; break;
                case 22: _bold = false; break;
                case 39: _fg = DefaultFg; break;
                case 49: _bg = DefaultBg; break;
                case >= 30 and <= 37: _fg = Ansi16(n - 30, _bold); break;
                case >= 90 and <= 97: _fg = Ansi16(n - 90, true); break;
                case >= 40 and <= 47: _bg = Ansi16(n - 40, false); break;
                case >= 100 and <= 107: _bg = Ansi16(n - 100, true); break;
                case 38: i = ExtendedColor(parts, i, isFg: true); break;
                case 48: i = ExtendedColor(parts, i, isFg: false); break;
                default: break;
            }
        }
    }

    private int ExtendedColor(string[] parts, int i, bool isFg)
    {
        // 38;5;n  (256) or 38;2;r;g;b (truecolor)
        if (i + 1 >= parts.Length) return i;
        if (!int.TryParse(parts[i + 1], out var mode)) return i + 1;
        if (mode == 5 && i + 2 < parts.Length && int.TryParse(parts[i + 2], out var idx))
        {
            var col = Color256(idx);
            if (isFg) _fg = col; else _bg = col;
            return i + 2;
        }
        if (mode == 2 && i + 4 < parts.Length &&
            int.TryParse(parts[i + 2], out var r) && int.TryParse(parts[i + 3], out var g) && int.TryParse(parts[i + 4], out var b))
        {
            var col = Color.FromArgb(0xFF, (byte)r, (byte)g, (byte)b);
            if (isFg) _fg = col; else _bg = col;
            return i + 4;
        }
        return i + 1;
    }

    private static Color Ansi16(int idx, bool bright)
    {
        // Campbell palette (Windows Terminal default).
        (byte r, byte g, byte b)[] normal =
        {
            (12,12,12),(197,15,31),(19,161,14),(193,156,0),(0,55,218),(136,23,152),(58,150,221),(204,204,204)
        };
        (byte r, byte g, byte b)[] brightP =
        {
            (118,118,118),(231,72,86),(22,198,12),(249,241,165),(59,120,255),(180,0,158),(97,214,214),(242,242,242)
        };
        var p = bright ? brightP[idx] : normal[idx];
        return Color.FromArgb(0xFF, p.r, p.g, p.b);
    }

    private static Color Color256(int idx)
    {
        if (idx < 16) return Ansi16(idx % 8, idx >= 8);
        if (idx >= 232) { byte v = (byte)(8 + (idx - 232) * 10); return Color.FromArgb(0xFF, v, v, v); }
        idx -= 16;
        int r = idx / 36, g = (idx % 36) / 6, b = idx % 6;
        byte Map(int c) => (byte)(c == 0 ? 0 : 55 + c * 40);
        return Color.FromArgb(0xFF, Map(r), Map(g), Map(b));
    }

    // ===================== Render =====================

    private void Render()
    {
        Surface.Blocks.Clear();
        for (int r = 0; r < _rows; r++)
        {
            var para = new Paragraph { LineHeight = 17, Margin = new Thickness(0) };
            // Coalesce adjacent cells with the same attributes into runs.
            int c = 0;
            while (c < _cols)
            {
                var first = _buf[r, c];
                var sb = new StringBuilder();
                Color fg = first.Fg, bg = first.Bg; bool bold = first.Bold;
                while (c < _cols && _buf[r, c].Fg.Equals(fg) && _buf[r, c].Bg.Equals(bg) && _buf[r, c].Bold == bold)
                {
                    sb.Append(_buf[r, c].Ch);
                    c++;
                }
                var run = new Run
                {
                    Text = sb.ToString(),
                    Foreground = new SolidColorBrush(fg),
                    FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                };
                para.Inlines.Add(run);
            }
            Surface.Blocks.Add(para);
        }
        // Keep newest output in view.
        Scroller.ChangeView(null, Scroller.ScrollableHeight, null, disableAnimation: true);
    }

    // ===================== Keyboard input =====================

    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        var ch = args.Character;
        if (ch == '\r' || ch == '\n') { SendInput?.Invoke("\r"); args.Handled = true; return; }
        if (ch == '\b') { SendInput?.Invoke("\x7f"); args.Handled = true; return; }
        if (ch >= ' ' || ch == '\t')
        {
            SendInput?.Invoke(ch.ToString());
            args.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down)
            == Windows.UI.Core.CoreVirtualKeyStates.Down;

        string? seq = e.Key switch
        {
            VirtualKey.Up => "\x1b[A",
            VirtualKey.Down => "\x1b[B",
            VirtualKey.Right => "\x1b[C",
            VirtualKey.Left => "\x1b[D",
            VirtualKey.Home => "\x1b[H",
            VirtualKey.End => "\x1b[F",
            VirtualKey.Delete => "\x1b[3~",
            VirtualKey.Insert => "\x1b[2~",
            VirtualKey.PageUp => "\x1b[5~",
            VirtualKey.PageDown => "\x1b[6~",
            VirtualKey.Escape => "\x1b",
            VirtualKey.Tab => "\t",
            VirtualKey.Enter => "\r",
            VirtualKey.Back => "\x7f",
            _ => null,
        };

        // Ctrl+C / Ctrl+letter -> control codes.
        if (ctrl && e.Key >= VirtualKey.A && e.Key <= VirtualKey.Z)
        {
            char c = (char)(e.Key - VirtualKey.A + 1); // ^A=1 ... ^Z=26
            SendInput?.Invoke(c.ToString());
            e.Handled = true;
            return;
        }

        if (seq is not null)
        {
            SendInput?.Invoke(seq);
            e.Handled = true;
        }
    }
}
