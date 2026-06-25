using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 原生十六進位／二進位編輯器 · Native HxD-style hex/binary editor (pure managed C#).
/// 開檔（記憶體對應，唔會把成個檔讀入記憶體）、虛擬化十六進位檢視（位移欄＋每行 16 byte＋ASCII）、
/// 就地覆寫位元組（標記已改）、儲存／另存、搵十六進位或文字、跳到位移、選取資訊、
/// 狀態列（位移 dec/hex、位元組 dec/hex/bin、檔案大細）、MD5／SHA-1／SHA-256（純 C# 計算），
/// 仲可以插入／刪除位元組。
/// Open (memory-mapped, never loads the whole file), virtualized hex view (offset column + 16
/// bytes/row + ASCII gutter), in-place overwrite with modified-byte highlight, Save / Save As,
/// find hex or text, go-to offset, selection info, a rich status bar, MD5/SHA-1/SHA-256 hashing,
/// plus optional insert/delete — everything in managed C#, bilingual.
/// </summary>
public sealed partial class HexEditorModule : Page
{
    private readonly HexEditorService _svc = new();
    private readonly ObservableCollection<HexRowVM> _rows = new();
    private long _caret = -1;        // current offset, -1 = none
    private long _selAnchor = -1;    // selection start
    private byte[]? _lastPattern;
    private long _lastFindPos;
    private CancellationTokenSource? _findCts;

    public HexEditorModule()
    {
        InitializeComponent();
        RowList.ItemsSource = _rows;
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => Render();
        Unloaded += (_, _) => _svc.Dispose();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Hex Editor · 十六進位編輯器";
        HeaderBlurb.Text = P(
            "A native HxD-style hex/binary editor — open files of any size (memory-mapped, never loaded whole), view offsets · hex · ASCII, overwrite bytes in place (highlighted), insert/delete, find hex or text, go to an offset, and compute MD5/SHA-1/SHA-256. Everything runs in managed C#.",
            "原生 HxD 風格十六進位／二進位編輯器 — 開任何大細嘅檔（記憶體對應，唔會成個讀入記憶體）、睇位移 · 十六進位 · ASCII、就地覆寫位元組（會標示）、插入／刪除、搵十六進位或文字、跳到位移、計 MD5／SHA-1／SHA-256。全部用純 C# 行。");

        OpenLbl.Text = P("Open file · 開檔", "開檔");
        SaveLbl.Text = P("Save · 儲存", "儲存");
        SaveAsLbl.Text = P("Save As… · 另存", "另存為…");
        InsertModeLbl.Text = P("Insert mode · 插入模式", "插入模式");
        HashLbl.Text = P("Hashes · 雜湊", "雜湊");
        FindNextLbl.Text = P("Find Next · 搵下一個", "搵下一個");
        GotoLbl.Text = P("Go To · 跳到", "跳到");
        FindBox.PlaceholderText = P("Search…", "搜尋…");

        int fm = FindMode.SelectedIndex < 0 ? 0 : FindMode.SelectedIndex;
        FindMode.Items.Clear();
        FindMode.Items.Add(new ComboBoxItem { Content = P("Text · 文字", "文字") });
        FindMode.Items.Add(new ComboBoxItem { Content = P("Hex · 十六進位", "十六進位") });
        FindMode.SelectedIndex = fm;

        EmptyText.Text = _svc.IsOpen
            ? ""
            : P("Open a file to begin. Large files are memory-mapped, so even multi-gigabyte files load instantly.",
                "開個檔開始啦。大檔會用記憶體對應，所以就算幾 GB 都即刻開到。");
        EmptyState.Visibility = _svc.IsOpen ? Visibility.Collapsed : Visibility.Visible;

        ColumnHeader.Text = BuildColumnHeader();
        UpdateStatus();
    }

    private static string BuildColumnHeader()
    {
        var sb = new StringBuilder();
        sb.Append("Offset(h)  ");
        for (int i = 0; i < HexEditorService.BytesPerRow; i++)
            sb.Append(i.ToString("X2")).Append(' ');
        sb.Append(" Decoded text");
        return sb.ToString();
    }

    // ── Open ─────────────────────────────────────────────────────────────────────

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync();
        if (path is null) return;
        try
        {
            _svc.Open(path);
            _caret = _svc.Length > 0 ? 0 : -1;
            _selAnchor = _caret;
            _lastPattern = null;
            _lastFindPos = 0;
            RebuildRows();
            EnableEditing(true);
            EmptyState.Visibility = Visibility.Collapsed;
            ShowStatus(InfoBarSeverity.Success, P($"Opened {System.IO.Path.GetFileName(path)} ({HexEditorService.HumanSize(_svc.Length)}).",
                $"已開啟 {System.IO.Path.GetFileName(path)}（{HexEditorService.HumanSize(_svc.Length)}）。"));
        }
        catch (Exception ex)
        {
            ShowStatus(InfoBarSeverity.Error, P($"Could not open file: {ex.Message}", $"開唔到檔案：{ex.Message}"));
        }
        Render();
    }

    private void EnableEditing(bool on)
    {
        SaveBtn.IsEnabled = on;
        SaveAsBtn.IsEnabled = on;
        InsertModeToggle.IsEnabled = on;
        HashBtn.IsEnabled = on;
        FindBox.IsEnabled = on;
        FindMode.IsEnabled = on;
        FindNextBtn.IsEnabled = on;
        GotoBox.IsEnabled = on;
        GotoBtn.IsEnabled = on;
    }

    /// <summary>(Re)materialise one row view-model per 16-byte line. The ListView virtualizes them,
    /// so even a 2 GB file produces 128M lightweight VMs that are only realised when scrolled into view.</summary>
    private void RebuildRows()
    {
        _rows.Clear();
        long rowCount = (_svc.Length + HexEditorService.BytesPerRow - 1) / HexEditorService.BytesPerRow;
        if (_svc.Length == 0) return;
        // Cap eager VM creation; for huge files we still create one VM per row but they are tiny.
        for (long r = 0; r < rowCount; r++)
            _rows.Add(new HexRowVM(_svc, r * HexEditorService.BytesPerRow));
    }

    // ── Save ─────────────────────────────────────────────────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!_svc.IsOpen) return;
        Busy.IsActive = true;
        try
        {
            await _svc.SaveAsync();
            RebuildRows();
            ShowStatus(InfoBarSeverity.Success, P("Saved.", "已儲存。"));
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P($"Save failed: {ex.Message}", $"儲存失敗：{ex.Message}")); }
        finally { Busy.IsActive = false; UpdateStatus(); }
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (!_svc.IsOpen) return;
        var suggested = _svc.FilePath is null ? "untitled.bin" : System.IO.Path.GetFileName(_svc.FilePath);
        var path = await FileDialogs.SaveFileAsync(suggested);
        if (path is null) return;
        Busy.IsActive = true;
        try
        {
            await _svc.SaveAsAsync(path);
            RebuildRows();
            ShowStatus(InfoBarSeverity.Success, P($"Saved to {System.IO.Path.GetFileName(path)}.", $"已儲存到 {System.IO.Path.GetFileName(path)}。"));
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, P($"Save failed: {ex.Message}", $"儲存失敗：{ex.Message}")); }
        finally { Busy.IsActive = false; UpdateStatus(); }
    }

    private void InsertMode_Click(object sender, RoutedEventArgs e) => UpdateStatus();

    // ── Edit (tap a row to pick an offset, then edit) ────────────────────────────

    private async void EditByteAt(long offset)
    {
        if (!_svc.IsOpen || offset < 0 || offset >= _svc.Length) return;
        bool insert = InsertModeToggle.IsChecked == true;

        var hexBox = new TextBox
        {
            Header = insert
                ? P("Bytes to insert (hex, e.g. 'DE AD BE EF')", "要插入嘅位元組（十六進位，例如「DE AD BE EF」）")
                : P("New byte value (hex 00–FF)", "新位元組值（十六進位 00–FF）"),
            Text = insert ? "" : _svc.ReadByteAt(offset).ToString("X2"),
            FontFamily = new FontFamily("Consolas"),
        };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Edit at offset 0x{offset:X}", $"編輯位移 0x{offset:X}"),
            Content = hexBox,
            PrimaryButtonText = insert ? P("Insert · 插入", "插入") : P("Overwrite · 覆寫", "覆寫"),
            SecondaryButtonText = insert ? null : P("Delete byte · 刪除", "刪除位元組"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.None) return;

        try
        {
            if (res == ContentDialogResult.Secondary && !insert)
            {
                _svc.Delete(offset, 1);
                RebuildRows();
            }
            else if (insert)
            {
                var bytes = HexEditorService.ParsePattern(hexBox.Text, asHex: true);
                if (bytes is null || bytes.Length == 0)
                {
                    ShowStatus(InfoBarSeverity.Warning, P("Enter an even number of hex digits.", "請輸入偶數個十六進位數字。"));
                    return;
                }
                _svc.Insert(offset, bytes);
                RebuildRows();
            }
            else
            {
                var v = ParseByte(hexBox.Text);
                if (v is null)
                {
                    ShowStatus(InfoBarSeverity.Warning, P("Enter a hex value 00–FF.", "請輸入 00–FF 嘅十六進位值。"));
                    return;
                }
                _svc.Overwrite(offset, v.Value);
                RefreshRowAt(offset);
            }
            UpdateStatus();
        }
        catch (Exception ex) { ShowStatus(InfoBarSeverity.Error, ex.Message); }
    }

    private void RefreshRowAt(long offset)
    {
        long rowStart = offset - (offset % HexEditorService.BytesPerRow);
        long idx = rowStart / HexEditorService.BytesPerRow;
        if (idx >= 0 && idx < _rows.Count) _rows[(int)idx].Refresh();
    }

    private static byte? ParseByte(string? s)
    {
        s = (s ?? "").Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return byte.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b) ? b : null;
    }

    // ── Find ─────────────────────────────────────────────────────────────────────

    private void FindBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) FindNext_Click(sender, e);
    }

    private async void FindNext_Click(object sender, RoutedEventArgs e)
    {
        if (!_svc.IsOpen) return;
        bool asHex = FindMode.SelectedIndex == 1;
        var pattern = HexEditorService.ParsePattern(FindBox.Text ?? "", asHex, Encoding.UTF8);
        if (pattern is null || pattern.Length == 0)
        {
            ShowStatus(InfoBarSeverity.Warning, asHex
                ? P("Enter an even number of hex digits.", "請輸入偶數個十六進位數字。")
                : P("Enter some text to search for.", "請輸入要搵嘅文字。"));
            return;
        }

        // Continue from just after the last hit, else from the caret.
        long start = (_lastPattern is not null && SamePattern(_lastPattern, pattern))
            ? _lastFindPos + 1
            : Math.Max(0, _caret);

        _findCts?.Cancel();
        _findCts = new CancellationTokenSource();
        Busy.IsActive = true;
        try
        {
            long hit = await Task.Run(() => _svc.Find(pattern, start, _findCts.Token), _findCts.Token);
            if (hit < 0 && start > 0)
                hit = await Task.Run(() => _svc.Find(pattern, 0, _findCts.Token), _findCts.Token); // wrap around
            if (hit < 0)
            {
                ShowStatus(InfoBarSeverity.Informational, P("Not found.", "搵唔到。"));
            }
            else
            {
                _lastPattern = pattern;
                _lastFindPos = hit;
                SelectRange(hit, hit + pattern.Length - 1);
                ScrollToOffset(hit);
                ShowStatus(InfoBarSeverity.Success, P($"Found at 0x{hit:X}.", $"喺 0x{hit:X} 搵到。"));
            }
        }
        catch (OperationCanceledException) { }
        finally { Busy.IsActive = false; }
    }

    private static bool SamePattern(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    // ── Go To ────────────────────────────────────────────────────────────────────

    private void GotoBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) Goto_Click(sender, e);
    }

    private void Goto_Click(object sender, RoutedEventArgs e)
    {
        if (!_svc.IsOpen) return;
        var off = ParseOffset(GotoBox.Text);
        if (off is null || off < 0 || off >= _svc.Length)
        {
            ShowStatus(InfoBarSeverity.Warning, P($"Enter an offset between 0 and 0x{Math.Max(0, _svc.Length - 1):X}.",
                $"請輸入 0 至 0x{Math.Max(0, _svc.Length - 1):X} 之間嘅位移。"));
            return;
        }
        SelectRange(off.Value, off.Value);
        ScrollToOffset(off.Value);
    }

    private static long? ParseOffset(string? s)
    {
        s = (s ?? "").Trim();
        if (s.Length == 0) return null;
        bool hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (hex) s = s[2..];
        return long.TryParse(s, hex ? NumberStyles.HexNumber : NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    // ── Selection / scroll ───────────────────────────────────────────────────────

    private void SelectRange(long from, long to)
    {
        _selAnchor = from;
        _caret = to;
        UpdateStatus();
    }

    private void ScrollToOffset(long offset)
    {
        long idx = offset / HexEditorService.BytesPerRow;
        if (idx >= 0 && idx < _rows.Count)
        {
            RowList.ScrollIntoView(_rows[(int)idx]);
            _rows[(int)idx].Refresh();
        }
    }

    // ── Hashes ───────────────────────────────────────────────────────────────────

    private async void Hash_Click(object sender, RoutedEventArgs e)
    {
        if (!_svc.IsOpen) return;
        var progress = new ProgressBar { Minimum = 0, Maximum = 1, Width = 360 };
        var label = new TextBlock { Text = P("Hashing…", "計算緊…"), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        var panel = new StackPanel { Spacing = 10, MinWidth = 380 };
        panel.Children.Add(label);
        panel.Children.Add(progress);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("File hashes · 檔案雜湊", "檔案雜湊"),
            Content = panel,
            CloseButtonText = P("Close · 關閉", "關閉"),
        };
        var cts = new CancellationTokenSource();
        dlg.Closing += (_, _) => cts.Cancel();
        var prog = new Progress<double>(p => progress.Value = p);

        _ = Task.Run(async () =>
        {
            try
            {
                var h = await _svc.ComputeHashesAsync(prog, cts.Token);
                DispatcherQueue.TryEnqueue(() =>
                {
                    panel.Children.Clear();
                    panel.Children.Add(HashRow("MD5", h.Md5));
                    panel.Children.Add(HashRow("SHA-1", h.Sha1));
                    panel.Children.Add(HashRow("SHA-256", h.Sha256));
                });
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => label.Text = ex.Message);
            }
        });
        await dlg.ShowAsync();
    }

    private static StackPanel HashRow(string name, string value)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock { Text = name, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 12 });
        panel.Children.Add(new TextBox { Text = value, IsReadOnly = true, FontFamily = new FontFamily("Consolas"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        return panel;
    }

    // ── Status bar ───────────────────────────────────────────────────────────────

    private void UpdateStatus()
    {
        if (!_svc.IsOpen)
        {
            StatusOffset.Text = ""; StatusValue.Text = ""; StatusSelection.Text = "";
            StatusSize.Text = "";
            return;
        }
        StatusSize.Text = P($"Size: {HexEditorService.HumanSize(_svc.Length)}", $"大細：{HexEditorService.HumanSize(_svc.Length)}")
            + (_svc.IsDirty ? P("  • modified", "  • 已修改") : "");

        if (_caret >= 0 && _caret < _svc.Length)
        {
            byte v = _svc.ReadByteAt(_caret);
            StatusOffset.Text = $"Offset: {_caret} (0x{_caret:X})";
            StatusValue.Text = $"Byte: {v} (0x{v:X2}, 0b{Convert.ToString(v, 2).PadLeft(8, '0')})";
        }
        else { StatusOffset.Text = ""; StatusValue.Text = ""; }

        if (_selAnchor >= 0 && _caret >= 0 && _selAnchor != _caret)
        {
            long lo = Math.Min(_selAnchor, _caret), hi = Math.Max(_selAnchor, _caret);
            long len = hi - lo + 1;
            StatusSelection.Text = P($"Selection: 0x{lo:X}–0x{hi:X} ({len} bytes)", $"選取：0x{lo:X}–0x{hi:X}（{len} 位元組）");
        }
        else StatusSelection.Text = "";
    }

    private void ShowStatus(InfoBarSeverity sev, string message)
    {
        StatusBar.Severity = sev;
        StatusBar.Message = message;
        StatusBar.IsOpen = true;
    }

    // ── Row interaction ──────────────────────────────────────────────────────────

    private async void RowList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not HexRowVM vm) return;
        // Ask which byte in the row to act on (0–15), defaulting to the first.
        var picker = new NumberBox
        {
            Header = P("Byte in this row (0–15) · 此行第幾個位元組（0–15）", "此行第幾個位元組（0–15）"),
            Minimum = 0, Maximum = HexEditorService.BytesPerRow - 1, Value = 0,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
        };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P($"Row at 0x{vm.Start:X}", $"位移 0x{vm.Start:X} 嘅一行"),
            Content = picker,
            PrimaryButtonText = P("Edit byte… · 編輯", "編輯位元組…"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        long target = vm.Start + (long)Math.Clamp(picker.Value, 0, HexEditorService.BytesPerRow - 1);
        SelectRange(target, target);
        EditByteAt(target);
    }
}

/// <summary>
/// 一行（16 位元組）嘅檢視模型 · View-model for one 16-byte line, materialised lazily by the
/// virtualizing ListView. Reads its bytes from the service on demand so memory stays tiny.
/// </summary>
public sealed class HexRowVM : INotifyPropertyChanged
{
    private readonly HexEditorService _svc;
    public long Start { get; }

    public HexRowVM(HexEditorService svc, long start) { _svc = svc; Start = start; }

    public string OffsetText => Start.ToString("X8");

    public string HexText
    {
        get
        {
            var buf = new byte[HexEditorService.BytesPerRow];
            int n = _svc.Read(Start, buf, buf.Length);
            var sb = new StringBuilder(HexEditorService.BytesPerRow * 3);
            for (int i = 0; i < HexEditorService.BytesPerRow; i++)
            {
                if (i < n) sb.Append(buf[i].ToString("X2"));
                else sb.Append("  ");
                sb.Append(' ');
            }
            return sb.ToString();
        }
    }

    public string AsciiText
    {
        get
        {
            var buf = new byte[HexEditorService.BytesPerRow];
            int n = _svc.Read(Start, buf, buf.Length);
            var sb = new StringBuilder(HexEditorService.BytesPerRow);
            for (int i = 0; i < n; i++)
            {
                byte b = buf[i];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            return sb.ToString();
        }
    }

    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HexText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AsciiText)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
