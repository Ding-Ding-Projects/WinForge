using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.UI;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 指令面板啟動器視窗（PowerToys Run 式）· The Command Palette launcher window — a centered, topmost,
/// borderless search box + results list, built entirely in code so it can live outside the main shell.
///
/// 一個搜尋框 + 結果清單；輸入即時搜尋，↑↓ 揀，Enter 啟動，Esc 關閉，失焦自動收埋。
/// One search box and a results list: typing searches live, arrows navigate, Enter launches the
/// selected result, Esc (or losing focus) closes. A single shared instance is toggled by the hotkey.
/// </summary>
public sealed class CommandPaletteWindow
{
    private static CommandPaletteWindow? _instance;
    private static bool _open;

    private readonly Window _window;
    private readonly Grid _root;
    private readonly Border _surface;
    private readonly TextBox _search;
    private readonly ListView _list;
    private readonly TextBlock _hint;
    private readonly ProgressRing _busyIndicator;
    private List<CommandPaletteResult> _results = new();
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _invokeCts;
    private int _refreshVersion;
    private bool _invoking;

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    private const int VK_CONTROL = 0x11;

    /// <summary>切換顯示／隱藏（畀全域熱鍵呼叫）· Toggle the palette open/closed (called by the global hotkey).</summary>
    public static void Toggle()
    {
        if (_open) { _instance?.Hide(); return; }
        _instance ??= new CommandPaletteWindow();
        _instance.Show();
    }

    /// <summary>由設定頁「試打開」按鈕呼叫 · Open the palette directly (used by the settings preview button).</summary>
    public static void Open() { _instance ??= new CommandPaletteWindow(); _instance.ShowInternal(); }

    /// <summary>Open the palette with a prefilled query, used when a saved Dock pin no longer resolves.</summary>
    public static void OpenWithQuery(string query) { _instance ??= new CommandPaletteWindow(); _instance.ShowInternal(query); }

    /// <summary>Refresh the shared launcher after its native appearance settings change.</summary>
    public static void RefreshAppearance() => _instance?.ApplyAppearance();

    private void Show() => ShowInternal();

    private CommandPaletteWindow()
    {
        _window = new Window { Title = "WinForge Command Palette · WinForge 指令面板" };

        // Borderless, topmost, no taskbar entry, sized for a launcher.
        var ap = _window.AppWindow;
        var presenter = OverlappedPresenter.Create();
        presenter.IsResizable = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.SetBorderAndTitleBar(false, false);
        ap.SetPresenter(presenter);
        try { ap.IsShownInSwitchers = false; } catch { }

        // ---- UI ----
        _root = new Grid
        {
            CornerRadius = new CornerRadius(12),
        };
        _surface = new Border
        {
            Padding = new Thickness(14),
            CornerRadius = new CornerRadius(12),
            Background = ResBrush("AcrylicInAppFillColorDefaultBrush", Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B)),
            BorderBrush = ResBrush("CardStrokeColorDefaultBrush", Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
        };
        _root.Children.Add(_surface);
        // 跟隨 app 嘅佈景主題 · Follow the app's chosen theme.
        try { if (App.Shell?.Content is FrameworkElement fe) _root.RequestedTheme = fe.RequestedTheme; } catch { }
        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var searchRow = new Grid();
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var searchIcon = new FontIcon
        {
            Glyph = ((char)0xE721).ToString(),
            FontSize = 20,
            Margin = new Thickness(6, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = ResBrush("TextFillColorSecondaryBrush", Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)),
        };
        Grid.SetColumn(searchIcon, 0);
        _search = new TextBox
        {
            FontSize = 22,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Colors.Transparent),
            PlaceholderText = Loc.I.Pick("Search apps, modules, files, math…", "搜尋程式、模組、檔案、計算…"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_search, 1);
        _busyIndicator = new ProgressRing
        {
            Width = 22,
            Height = 22,
            Margin = new Thickness(10, 0, 6, 0),
            IsActive = false,
            Visibility = Visibility.Collapsed,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(_busyIndicator, Loc.I.Pick("Running extension action", "擴充套件操作進行中"));
        Grid.SetColumn(_busyIndicator, 2);
        searchRow.Children.Add(searchIcon);
        searchRow.Children.Add(_search);
        searchRow.Children.Add(_busyIndicator);
        Grid.SetRow(searchRow, 0);

        _list = new ListView
        {
            Margin = new Thickness(0, 10, 0, 0),
            SelectionMode = ListViewSelectionMode.Single,
            ItemTemplate = BuildItemTemplate(),
            IsItemClickEnabled = true,
            MaxHeight = 420,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_list, ScrollBarVisibility.Auto);
        Grid.SetRow(_list, 1);

        _hint = new TextBlock
        {
            Margin = new Thickness(6, 8, 6, 0),
            FontSize = 11,
            Foreground = ResBrush("TextFillColorSecondaryBrush", Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)),
            Text = Loc.I.Pick("Enter launch · ↑↓ navigate · Ctrl+P pin · Esc close", "Enter 啟動 · ↑↓ 選擇 · Ctrl+P 釘選 · Esc 關閉"),
        };
        AutomationProperties.SetLiveSetting(_hint, AutomationLiveSetting.Polite);
        Grid.SetRow(_hint, 2);

        layout.Children.Add(searchRow);
        layout.Children.Add(_list);
        layout.Children.Add(_hint);
        _surface.Child = layout;
        _window.Content = _root;
        ApplyAppearance();

        // ---- events ----
        _search.TextChanged += (_, _) => Refresh();
        _search.KeyDown += OnSearchKeyDown;
        _list.ItemClick += (_, e) => { if (e.ClickedItem is CommandPaletteResult r) InvokeResult(r); };
        _list.KeyDown += OnListKeyDown;
        _window.Activated += OnActivated;
    }

    private void ApplyAppearance()
    {
        try { if (App.Shell?.Content is FrameworkElement fe) _root.RequestedTheme = fe.RequestedTheme; } catch { }

        string imagePath = CommandPaletteService.BackgroundImagePath;
        if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
        {
            try
            {
                _root.Background = new ImageBrush
                {
                    ImageSource = new BitmapImage { UriSource = new Uri(imagePath) },
                    Stretch = Stretch.UniformToFill,
                };
            }
            catch { _root.Background = new SolidColorBrush(Colors.Transparent); }
        }
        else _root.Background = new SolidColorBrush(Colors.Transparent);

        // The surface stays on top of any image so palette text retains its established contrast in both themes.
        _surface.Background = ResBrush("AcrylicInAppFillColorDefaultBrush", Color.FromArgb(0xF2, 0x2B, 0x2B, 0x2B));
        try
        {
            _window.SystemBackdrop = CommandPaletteService.AppearanceBackdrop switch
            {
                CommandPaletteService.CommandPaletteBackdrop.Mica => new MicaBackdrop(),
                CommandPaletteService.CommandPaletteBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
                _ => null,
            };
        }
        catch { }
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        // 失焦即收（似 Run 啟動器）· Auto-hide when the launcher loses focus, like Run.
        if (args.WindowActivationState == WindowActivationState.Deactivated && _open)
            Hide();
    }

    private void ShowInternal(string? initialQuery = null)
    {
        _open = true;
        _search.Text = initialQuery ?? "";
        Refresh();
        Resize();
        CenterOnCursorMonitor();
        try { _window.AppWindow.Show(); } catch { }
        _window.Activate();
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            SetForegroundWindow(hwnd);
        }
        catch { }
        _search.Focus(FocusState.Programmatic);
    }

    private void Hide()
    {
        _open = false;
        _invokeCts?.Cancel();
        try { _window.AppWindow.Hide(); } catch { }
    }

    private void Resize()
    {
        try
        {
            int w = 720;
            int rows = Math.Min(_results.Count, 8);
            int h = 86 + rows * 56 + 28; // search row + N result rows + hint
            _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(w, Math.Max(140, h)));
        }
        catch { }
    }

    private void CenterOnCursorMonitor()
    {
        try
        {
            var ap = _window.AppWindow;
            var area = DisplayArea.GetFromWindowId(ap.Id, DisplayAreaFallback.Primary);
            var size = ap.Size;
            int x = area.WorkArea.X + (area.WorkArea.Width - size.Width) / 2;
            int y = area.WorkArea.Y + (int)(area.WorkArea.Height * 0.22); // upper-third, like PowerToys Run
            ap.Move(new Windows.Graphics.PointInt32(x, y));
        }
        catch { }
    }

    private void Refresh()
    {
        string query = _search.Text ?? "";
        _refreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        int version = Interlocked.Increment(ref _refreshVersion);

        if (query.Trim().Length > 0)
            _hint.Text = Loc.I.Pick("Searching…", "搜尋中…");

        _ = RefreshAsync(query, version, cts.Token);
    }

    private async Task RefreshAsync(string query, int version, CancellationToken ct)
    {
        try
        {
            if (query.Trim().Length > 0)
                await Task.Delay(80, ct);

            var results = await Task.Run(() => CommandPaletteService.Query(query), ct);
            if (ct.IsCancellationRequested || version != _refreshVersion) return;

            _results = results;
            _list.ItemsSource = _results;
            if (_results.Count > 0) _list.SelectedIndex = 0;
            Resize();
            UpdateHint();
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (ct.IsCancellationRequested || version != _refreshVersion) return;
            _results = new List<CommandPaletteResult>();
            _list.ItemsSource = _results;
            Resize();
            UpdateHint();
        }
    }

    private void UpdateHint()
    {
        _hint.Text = _results.Count == 0
            ? Loc.I.Pick("No results · type to search", "無結果 · 輸入以搜尋")
            : Loc.I.Pick("Enter launch · ↑↓ navigate · Ctrl+P pin · Esc close", "Enter 啟動 · ↑↓ 選擇 · Ctrl+P 釘選 · Esc 關閉");
    }

    private void OnSearchKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.P && CtrlDown)
        {
            TogglePinSelected();
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case VirtualKey.Escape:
                Hide(); e.Handled = true; break;
            case VirtualKey.Enter:
                InvokeSelected(); e.Handled = true; break;
            case VirtualKey.Down:
                Move(1); e.Handled = true; break;
            case VirtualKey.Up:
                Move(-1); e.Handled = true; break;
        }
    }

    private void OnListKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.P && CtrlDown) { TogglePinSelected(); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter) { InvokeSelected(); e.Handled = true; }
        else if (e.Key == VirtualKey.Escape) { Hide(); e.Handled = true; }
    }

    private static bool CtrlDown => (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

    private void TogglePinSelected()
    {
        var result = _list.SelectedItem as CommandPaletteResult;
        if (result is null && _results.Count > 0) result = _results[0];
        if (result is null) return;
        bool pinned = CommandPaletteService.ToggleDockPin(result, _search.Text);
        _hint.Text = pinned
            ? Loc.I.Pick("Pinned to Dock · Ctrl+P again to unpin", "已釘選到 Dock · 再按 Ctrl+P 取消釘選")
            : Loc.I.Pick("Removed from Dock", "已由 Dock 移除");
    }

    private void Move(int delta)
    {
        if (_results.Count == 0) return;
        int idx = _list.SelectedIndex + delta;
        if (idx < 0) idx = _results.Count - 1;
        if (idx >= _results.Count) idx = 0;
        _list.SelectedIndex = idx;
        _list.ScrollIntoView(_list.SelectedItem);
    }

    private void InvokeSelected()
    {
        if (_list.SelectedItem is CommandPaletteResult r) InvokeResult(r);
        else if (_results.Count > 0) InvokeResult(_results[0]);
    }

    private async void InvokeResult(CommandPaletteResult r)
    {
        if (_invoking) return;

        bool close = true;
        var asyncAction = r.InvokeAsync;
        try
        {
            if (asyncAction is null)
            {
                close = r.Invoke();
            }
            else
            {
                _invoking = true;
                _invokeCts = new CancellationTokenSource();
                _list.IsEnabled = false;
                _busyIndicator.Visibility = Visibility.Visible;
                _busyIndicator.IsActive = true;
                AutomationProperties.SetName(_busyIndicator, Loc.I.Pick("Running extension action", "擴充套件操作進行中"));
                _hint.Text = Loc.I.Pick("Running extension action…", "擴充套件操作進行中…");
                close = await asyncAction(_invokeCts.Token);
            }
        }
        catch
        {
            close = false;
        }
        finally
        {
            if (asyncAction is not null)
            {
                _invokeCts?.Dispose();
                _invokeCts = null;
                _busyIndicator.IsActive = false;
                _busyIndicator.Visibility = Visibility.Collapsed;
                _list.IsEnabled = true;
                _invoking = false;
            }
        }

        if (close) Hide();
        else if (_open)
        {
            _hint.Text = Loc.I.Pick("The action could not be completed.", "未能完成操作。");
            _search.Focus(FocusState.Programmatic);
        }
    }

    /// <summary>安全攞主題畫刷，攞唔到就用後備顏色 · Resolve a theme brush, falling back to a fixed color.</summary>
    private static Brush ResBrush(string key, Color fallback)
    {
        try
        {
            if (Application.Current.Resources.TryGetValue(key, out var v) && v is Brush b) return b;
        }
        catch { }
        return new SolidColorBrush(fallback);
    }

    // ----- item template (built in code; no XAML file) · 結果項範本 -----
    private static DataTemplate BuildItemTemplate()
    {
        const string xaml =
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
            " xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
            "  <Grid Padding='6,8,6,8' ColumnSpacing='12'>" +
            "    <Grid.ColumnDefinitions>" +
            "      <ColumnDefinition Width='Auto'/>" +
            "      <ColumnDefinition Width='*'/>" +
            "      <ColumnDefinition Width='Auto'/>" +
            "    </Grid.ColumnDefinitions>" +
            "    <FontIcon Grid.Column='0' FontSize='20' Glyph='{Binding Glyph}' VerticalAlignment='Center'/>" +
            "    <StackPanel Grid.Column='1' VerticalAlignment='Center'>" +
            "      <TextBlock Text='{Binding Title}' FontSize='15' FontWeight='SemiBold' TextTrimming='CharacterEllipsis'/>" +
            "      <TextBlock Text='{Binding Subtitle}' FontSize='11' Opacity='0.7' TextTrimming='CharacterEllipsis'/>" +
            "    </StackPanel>" +
            "    <Border Grid.Column='2' VerticalAlignment='Center' CornerRadius='4' Padding='6,2,6,2'" +
            "            Background='{ThemeResource ControlFillColorSecondaryBrush}'>" +
            "      <TextBlock Text='{Binding ProviderTag}' FontSize='10' Opacity='0.8'/>" +
            "    </Border>" +
            "  </Grid>" +
            "</DataTemplate>";
        return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml);
    }
}
