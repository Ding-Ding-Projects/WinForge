using System;
using System.Linq;
using System.Threading;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.System;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 進階貼上面板（PowerToys 式置頂小視窗）· The topmost Advanced Paste palette window.
///
/// 由全域熱鍵叫起，喺滑鼠附近彈出，列出而家剪貼簿適用嘅轉換動作。
/// 揀一個（撳或者數字鍵）→ 轉換剪貼簿 → 貼落之前作用中嘅 app。
/// 失焦或者 Esc 自動關。單例：同一時間最多一個面板。
///
/// Summoned by the global hotkey, pops up near the cursor, lists the transform actions that apply to
/// the current clipboard, and on selection transforms + pastes into the previously-active app. Closes
/// on Esc or focus loss. Singleton — at most one palette at a time.
/// </summary>
public sealed class AdvancedPastePalette
{
    private static AdvancedPastePalette? _current;

    private readonly Window _window;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dq;
    private readonly StackPanel _list = new() { Spacing = 4 };
    private readonly TextBox _aiBox;
    private CancellationTokenSource? _cts;
    private bool _closing;

    /// <summary>開（或者重新聚焦）面板 · Show the palette (closing any existing one first).</summary>
    public static void Show(Microsoft.UI.Dispatching.DispatcherQueue dq)
    {
        try
        {
            _current?.CloseNow();
            _current = new AdvancedPastePalette(dq);
        }
        catch { /* never crash the hook callback path */ }
    }

    private AdvancedPastePalette(Microsoft.UI.Dispatching.DispatcherQueue dq)
    {
        _dq = dq;
        _window = new Window { Title = "WinForge Advanced Paste" };

        string P(string en, string zh) => Loc.I.Pick(en, zh);

        var content = AdvancedPasteService.CurrentContent();

        var header = new TextBlock
        {
            Text = P("Advanced Paste", "進階貼上"),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15,
        };
        var sub = new TextBlock
        {
            Text = content == PasteContent.None
                ? P("Clipboard is empty.", "剪貼簿係空嘅。")
                : (content.HasFlag(PasteContent.Image)
                    ? P("Image on clipboard.", "剪貼簿有圖片。")
                    : P("Text on clipboard.", "剪貼簿有文字。")),
            FontSize = 12,
            Opacity = 0.7,
            Margin = new Thickness(0, 0, 0, 6),
        };

        // Build the action buttons (filtered to the current clipboard content).
        int index = 1;
        foreach (var action in AdvancedPasteService.Enabled)
        {
            if (action.RequiresAi && !AdvancedPasteService.AiAvailable) continue;
            bool applies = content != PasteContent.None && (action.Accepts & content) != 0;
            if (!applies && !action.RequiresAi) continue; // AI works on text; show it when text present

            if (action.RequiresAi && !content.HasFlag(PasteContent.Text)) continue;

            var local = action;
            int num = index <= 9 ? index : 0;
            var btn = ActionButton(local, num, P);
            _list.Children.Add(btn);
            if (num > 0) index++;
        }

        if (_list.Children.Count == 0)
        {
            _list.Children.Add(new TextBlock
            {
                Text = P("No actions apply to the current clipboard.", "冇動作適用於目前嘅剪貼簿。"),
                Opacity = 0.7,
                FontSize = 12,
                Margin = new Thickness(2, 6, 2, 2),
            });
        }

        // AI free-text box (only when a provider exists and the clipboard has text).
        _aiBox = new TextBox
        {
            PlaceholderText = P("Paste with AI — type an instruction, press Enter…", "用 AI 貼上 — 輸入指示，撳 Enter…"),
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = (AdvancedPasteService.AiAvailable && content.HasFlag(PasteContent.Text))
                ? Visibility.Visible : Visibility.Collapsed,
        };
        _aiBox.KeyDown += async (_, e) =>
        {
            if (e.Key == VirtualKey.Enter && !string.IsNullOrWhiteSpace(_aiBox.Text))
            {
                e.Handled = true;
                await RunAndPaste(AdvancedPasteService.Find("ai")!, _aiBox.Text.Trim());
            }
        };

        var hint = new TextBlock
        {
            Text = P("Esc to cancel · 1–9 to pick · click an action", "Esc 取消 · 1–9 揀選 · 撳動作"),
            FontSize = 11,
            Opacity = 0.55,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var inner = new StackPanel { Margin = new Thickness(14), Spacing = 2 };
        inner.Children.Add(header);
        inner.Children.Add(sub);
        var scroll = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 360,
        };
        inner.Children.Add(scroll);
        inner.Children.Add(_aiBox);
        inner.Children.Add(hint);

        var root = new Border
        {
            Child = inner,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)Application.Current.Resources["AcrylicBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
        };

        var grid = new Grid();
        grid.Children.Add(root);
        grid.RequestedTheme = (Application.Current as App) is not null && App.Shell?.Content is FrameworkElement fe
            ? fe.RequestedTheme : ElementTheme.Default;
        grid.KeyDown += OnKeyDown;
        _window.Content = grid;

        ConfigureWindow();

        _window.Activated += OnActivated;
        _window.Closed += (_, _) => { if (_current == this) _current = null; };

        _window.Activate();
        grid.Loaded += (_, _) =>
        {
            // Focus the grid so number keys / Esc work immediately.
            grid.Focus(FocusState.Programmatic);
        };
    }

    private Button ActionButton(PasteAction action, int num, Func<string, string, string> P)
    {
        var title = new TextBlock { Text = action.Name.Primary, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var second = new TextBlock { Text = action.Name.Secondary, FontSize = 11, Opacity = 0.6 };
        second.Visibility = string.IsNullOrWhiteSpace(second.Text) ? Visibility.Collapsed : Visibility.Visible;
        var col = new StackPanel { Spacing = 0 };
        col.Children.Add(title);
        col.Children.Add(second);

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var badge = new TextBlock
        {
            Text = num > 0 ? num.ToString() : "·",
            Opacity = 0.55,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
        };
        Grid.SetColumn(badge, 0);
        Grid.SetColumn(col, 1);
        row.Children.Add(badge);
        row.Children.Add(col);

        var btn = new Button
        {
            Content = row,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            MinWidth = 320,
            Padding = new Thickness(10, 7, 10, 7),
        };
        ToolTipService.SetToolTip(btn, action.Blurb.Display);
        btn.Click += async (_, _) =>
        {
            if (action.RequiresAi)
            {
                // Focus the AI box rather than running with no instruction.
                _aiBox.Visibility = Visibility.Visible;
                _aiBox.Focus(FocusState.Programmatic);
                return;
            }
            await RunAndPaste(action, null);
        };
        return btn;
    }

    private async System.Threading.Tasks.Task RunAndPaste(PasteAction action, string? aiInstruction)
    {
        if (_closing) return;
        _closing = true;
        _cts = new CancellationTokenSource();
        try
        {
            // Close the palette first so focus returns to the target app, then transform + paste.
            HideWindow();
            await AdvancedPasteService.TransformAndPasteAsync(action, aiInstruction, _cts.Token);
        }
        catch { /* swallow — best effort */ }
        finally { CloseNow(); }
    }

    private void OnKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { e.Handled = true; CloseNow(); return; }
        int n = e.Key switch
        {
            VirtualKey.Number1 => 1, VirtualKey.Number2 => 2, VirtualKey.Number3 => 3,
            VirtualKey.Number4 => 4, VirtualKey.Number5 => 5, VirtualKey.Number6 => 6,
            VirtualKey.Number7 => 7, VirtualKey.Number8 => 8, VirtualKey.Number9 => 9,
            _ => 0,
        };
        if (n > 0)
        {
            var buttons = _list.Children.OfType<Button>().ToList();
            if (n <= buttons.Count) { e.Handled = true; var peer = buttons[n - 1]; AutomationInvoke(peer); }
        }
    }

    private static void AutomationInvoke(Button b)
    {
        var peer = Microsoft.UI.Xaml.Automation.Peers.FrameworkElementAutomationPeer.CreatePeerForElement(b)
            as Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer;
        peer?.Invoke();
    }

    private void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        // Close when the palette loses focus (mirrors PowerToys behaviour).
        if (e.WindowActivationState == WindowActivationState.Deactivated && !_closing)
            _dq.TryEnqueue(CloseNow);
    }

    private void ConfigureWindow()
    {
        try
        {
            var aw = _window.AppWindow;
            if (aw is null) return;

            if (aw.Presenter is OverlappedPresenter op)
            {
                op.IsAlwaysOnTop = true;
                op.IsResizable = false;
                op.IsMaximizable = false;
                op.IsMinimizable = false;
                op.SetBorderAndTitleBar(true, false); // border, no title bar
            }

            // Size & position near the cursor.
            var (cx, cy) = AdvancedPasteService.CursorPos();
            int w = 380, h = 460;
            aw.Resize(new SizeInt32(w, h));
            int x = cx + 8;
            int y = cy + 8;
            aw.Move(new PointInt32(x, y));

            aw.IsShownInSwitchers = false;
        }
        catch { }
    }

    private void HideWindow()
    {
        try { _window.AppWindow?.Hide(); } catch { }
    }

    public void CloseNow()
    {
        if (_window is null) return;
        try { _window.Close(); } catch { }
        if (_current == this) _current = null;
    }
}
