using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 像素畫編輯器（Aseprite 風格嘅輕量原生子集）· A lightweight, native Aseprite-style pixel-art editor.
/// 完全原生 WinForge 程式碼，唔抄 Aseprite 任何原始碼／資產／調色盤。This is original WinForge code —
/// NOT full Aseprite and NOT derived from its source, assets or default palette. Draw on a WriteableBitmap
/// with pencil / eraser / fill / eyedropper / select-move-delete, edit a palette, manage layers and frames,
/// undo/redo, and export PNG / animated GIF. If Aseprite is installed it can be launched too.
/// </summary>
public sealed partial class PixelEditorModule : Page
{
    private enum Tool { Pencil, Eraser, Fill, Eyedropper, Select }

    private readonly PixelEditorService _doc = new(32, 32);
    private Tool _tool = Tool.Pencil;
    private uint _primary = 0xFF000000; // opaque black, packed BGRA

    private WriteableBitmap? _bmp;
    private WriteableBitmap? _checker;
    private int _zoom = 12;
    private bool _showGrid = true;

    // Stroke state
    private EditAction? _stroke;
    private int _lastX = -1, _lastY = -1;

    // Selection (in pixel coords)
    private bool _hasSelection;
    private int _selX0, _selY0, _selX1, _selY1;
    private bool _movingSelection;
    private int _moveStartX, _moveStartY;

    // Animation playback
    private DispatcherTimer? _playTimer;
    private int _playFrame;

    private readonly List<Button> _toolButtons = new();
    private string? _lastSavedPath;

    // A small original palette (NOT Aseprite's default). Values are packed BGRA (0xAARRGGBB by hex
    // digits, but the low byte is Blue): greys, then a generic spread of hues + transparent at [0].
    private static readonly uint[] DefaultPalette =
    {
        0x00000000, // transparent
        0xFF000000, 0xFF3F3F3F, 0xFF7F7F7F, 0xFFBFBFBF, 0xFFFFFFFF,
        0xFF202020, 0xFF6B6B6B, 0xFFA8A8A8, 0xFFD8D8D8,
        0xFF2222C8, 0xFF2A6BE0, 0xFF36B0F0, 0xFF49D7F0, 0xFF8CE8F0,
        0xFF227FE0, 0xFF1FB04F, 0xFF44C86A, 0xFF8CE08C,
        0xFF1FC8C8, 0xFF38E0E0,
        0xFF1F1FC8, 0xFF3737E0, 0xFF6B6BF0,
        0xFFC82A8C, 0xFFE04FB0, 0xFFF08CD7,
        0xFF1F8CE0, 0xFF2AB0F0, 0xFF6BD7F0,
        0xFF227FBF, 0xFF49A8D7,
    };

    public PixelEditorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => RenderText();
        Loaded += OnLoaded;
        Unloaded += (_, _) => { _playTimer?.Stop(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildTools();
        BuildPalette();
        RenderText();
        RebuildBitmaps();
        RefreshLayers();
        RefreshFrames();
        UpdatePrimarySwatch();
        DetectAseprite();
        DelayBox.Value = _doc.ActiveFrame.DelayMs;
    }

    // ===================== Text / localisation =====================

    private void RenderText()
    {
        HeaderTitle.Text = "Pixel Editor · 像素畫編輯器";
        HeaderBlurb.Text = P("A native, lightweight pixel-art editor: draw, manage a palette, layers and animation frames, then export PNG or animated GIF.",
            "原生輕量像素畫編輯器：畫畫、管理調色盤、圖層同動畫影格，再匯出 PNG 或動畫 GIF。");
        SubsetNote.Title = P("Lightweight subset — not full Aseprite", "輕量子集 — 唔係完整 Aseprite");
        SubsetNote.Message = P("This is an original WinForge editor covering the core: pencil/eraser/fill/eyedropper/select, palette, layers, frames, undo and PNG/GIF export. It does not include Aseprite's tilemaps, Lua scripting, slices or advanced brushes. If Aseprite is installed you can launch it from here.",
            "呢個係 WinForge 原創編輯器，覆蓋核心功能：鉛筆／橡皮／填色／吸色／選取、調色盤、圖層、影格、復原同 PNG／GIF 匯出。唔包含 Aseprite 嘅圖塊地圖、Lua 腳本、切片或進階筆刷。如果你裝咗 Aseprite，可以喺呢度啟動佢。");

        NewBtnText.Text = P("New…", "新建…");
        ImportBtnText.Text = P("Import…", "匯入…");
        UndoBtnText.Text = P("Undo", "復原");
        RedoBtnText.Text = P("Redo", "重做");
        ZoomLabel.Text = P("Zoom", "縮放");
        GridCheck.Content = P("Grid", "格線");
        ExportPngText.Text = P("Export PNG", "匯出 PNG");
        ExportGifText.Text = P("Export GIF", "匯出 GIF");
        AsepriteText.Text = P("Launch Aseprite", "啟動 Aseprite");

        ColorHeader.Text = P("Colour", "顏色");
        AddSwatchBtn.Content = P("Add to palette", "加入調色盤");
        PaletteLabel.Text = P("Palette", "調色盤");
        RecentLabel.Text = P("Recent", "最近");
        LayersHeader.Text = P("Layers", "圖層");
        OpacityLabel.Text = P("Opacity", "不透明度");
        FramesHeader.Text = P("Frames", "影格");
        DelayLabel.Text = P("Delay (ms)", "延遲（毫秒）");
        PlayText.Text = P("Play", "播放");

        ToolTipService.SetToolTip(NewBtn, P("New canvas (choose size)", "新畫布（揀尺寸）"));
        ToolTipService.SetToolTip(ImportBtn, P("Import a PNG/GIF as a new canvas", "匯入 PNG／GIF 做新畫布"));
        ToolTipService.SetToolTip(ExportPngBtn, P("Export the current frame as PNG", "將目前影格匯出做 PNG"));
        ToolTipService.SetToolTip(ExportGifBtn, P("Export all frames as an animated GIF", "將所有影格匯出做動畫 GIF"));
        RefreshToolTips();
    }

    // ===================== Tools =====================

    private void BuildTools()
    {
        _toolButtons.Clear();
        ToolStack.Children.Clear();
        AddToolButton(Tool.Pencil, "", "Pencil", "鉛筆");
        AddToolButton(Tool.Eraser, "", "Eraser", "橡皮");
        AddToolButton(Tool.Fill, "", "Bucket fill", "油桶填色");
        AddToolButton(Tool.Eyedropper, "", "Eyedropper", "吸色");
        AddToolButton(Tool.Select, "", "Select / move / delete", "選取／移動／刪除");
        HighlightTool();
    }

    private void AddToolButton(Tool t, string glyph, string en, string zh)
    {
        var b = new Button
        {
            Width = 40,
            Height = 40,
            Padding = new Thickness(0),
            Content = new FontIcon { FontSize = 16, Glyph = glyph },
            Tag = t,
        };
        ToolTipService.SetToolTip(b, P(en, zh));
        b.Click += (_, _) => { _tool = t; HighlightTool(); };
        _toolButtons.Add(b);
        ToolStack.Children.Add(b);
    }

    private void RefreshToolTips()
    {
        string[][] hints =
        {
            new[] { "Pencil", "鉛筆" }, new[] { "Eraser", "橡皮" }, new[] { "Bucket fill", "油桶填色" },
            new[] { "Eyedropper", "吸色" }, new[] { "Select / move / delete", "選取／移動／刪除" },
        };
        for (int i = 0; i < _toolButtons.Count && i < hints.Length; i++)
            ToolTipService.SetToolTip(_toolButtons[i], P(hints[i][0], hints[i][1]));
    }

    private void HighlightTool()
    {
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        foreach (var b in _toolButtons)
            b.Background = (Tool)b.Tag! == _tool ? accent : null;
    }

    // ===================== Palette =====================

    private void BuildPalette()
    {
        PaletteGrid.Items.Clear();
        foreach (var c in DefaultPalette) PaletteGrid.Items.Add(MakeSwatch(c, 22));
    }

    private Border MakeSwatch(uint bgra, double size)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(3),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            Background = BgraBrush(bgra),
            Tag = bgra,
        };
        if ((bgra >> 24) == 0)
        {
            // transparent swatch — show a tiny checker hint
            border.Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128));
            ToolTipService.SetToolTip(border, P("Transparent (eraser colour)", "透明（橡皮色）"));
        }
        else
        {
            ToolTipService.SetToolTip(border, HexOf(bgra));
        }
        return border;
    }

    private void Palette_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Border b && b.Tag is uint c) SetPrimary(c);
    }

    private void SetPrimary(uint bgra)
    {
        _primary = bgra;
        UpdatePrimarySwatch();
    }

    private void UpdatePrimarySwatch()
    {
        PrimarySwatch.Background = (_primary >> 24) == 0
            ? new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
            : BgraBrush(_primary);
        HexInput.Text = HexOf(_primary);
    }

    private void AddSwatch_Click(object sender, RoutedEventArgs e)
    {
        PaletteGrid.Items.Add(MakeSwatch(_primary, 22));
    }

    private void Hex_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) return;
        if (TryParseHex(HexInput.Text, out var c)) { SetPrimary(c); }
    }

    private void AddRecent(uint bgra)
    {
        if ((bgra >> 24) == 0) return;
        var sw = MakeSwatch(bgra, 18);
        uint cc = bgra;
        sw.Tapped += (_, _) => SetPrimary(cc);
        RecentColors.Items.Insert(0, sw);
        while (RecentColors.Items.Count > 12) RecentColors.Items.RemoveAt(RecentColors.Items.Count - 1);
    }

    // ===================== Bitmap rendering =====================

    private void RebuildBitmaps()
    {
        int w = _doc.Width, h = _doc.Height;
        _bmp = new WriteableBitmap(w, h);
        PixelImage.Source = _bmp;

        // Checkerboard (8px cells at pixel scale) behind transparency.
        _checker = new WriteableBitmap(w, h);
        var cbuf = _checker.PixelBuffer.ToArray();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool dark = ((x >> 1) + (y >> 1)) % 2 == 0;
                byte v = (byte)(dark ? 200 : 230);
                int i = (y * w + x) * 4;
                cbuf[i] = v; cbuf[i + 1] = v; cbuf[i + 2] = v; cbuf[i + 3] = 255;
            }
        using (var s = _checker.PixelBuffer.AsStream()) { s.Write(cbuf, 0, cbuf.Length); }
        _checker.Invalidate();
        CheckerImage.Source = _checker;

        ApplyZoom();
        RedrawCanvas();
    }

    private void ApplyZoom()
    {
        int w = _doc.Width, h = _doc.Height;
        double pw = w * _zoom, ph = h * _zoom;
        PixelImage.Width = pw; PixelImage.Height = ph;
        CheckerImage.Width = pw; CheckerImage.Height = ph;
        OverlayCanvas.Width = pw; OverlayCanvas.Height = ph;
        CanvasWrap.Width = pw; CanvasWrap.Height = ph;
        ZoomLabel.Text = P("Zoom", "縮放") + $" {_zoom}×";
        DrawOverlay();
    }

    /// <summary>將整個目前影格合成後寫入 WriteableBitmap · Composite the active frame into the bitmap.</summary>
    private void RedrawCanvas()
    {
        if (_bmp is null) return;
        var bgra = _doc.CompositeFrame(_doc.ActiveFrameIndex);
        using (var s = _bmp.PixelBuffer.AsStream()) { s.Write(bgra, 0, bgra.Length); }
        _bmp.Invalidate();
        UpdateUndoButtons();
    }

    private void DrawOverlay()
    {
        OverlayCanvas.Children.Clear();
        int w = _doc.Width, h = _doc.Height;

        if (_showGrid && _zoom >= 6)
        {
            var stroke = new SolidColorBrush(Color.FromArgb(60, 128, 128, 128));
            for (int x = 1; x < w; x++)
                OverlayCanvas.Children.Add(new Line { X1 = x * _zoom, Y1 = 0, X2 = x * _zoom, Y2 = h * _zoom, Stroke = stroke, StrokeThickness = 1 });
            for (int y = 1; y < h; y++)
                OverlayCanvas.Children.Add(new Line { X1 = 0, Y1 = y * _zoom, X2 = w * _zoom, Y2 = y * _zoom, Stroke = stroke, StrokeThickness = 1 });
        }

        if (_hasSelection)
        {
            int xa = Math.Min(_selX0, _selX1), xb = Math.Max(_selX0, _selX1);
            int ya = Math.Min(_selY0, _selY1), yb = Math.Max(_selY0, _selY1);
            var rect = new Rectangle
            {
                Width = (xb - xa + 1) * _zoom,
                Height = (yb - ya + 1) * _zoom,
                Stroke = new SolidColorBrush(Colors.DodgerBlue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)),
            };
            Canvas.SetLeft(rect, xa * _zoom);
            Canvas.SetTop(rect, ya * _zoom);
            OverlayCanvas.Children.Add(rect);
        }
    }

    // ===================== Pointer / drawing =====================

    private bool ToPixel(PointerRoutedEventArgs e, out int px, out int py)
    {
        var p = e.GetCurrentPoint(PixelImage).Position;
        px = (int)Math.Floor(p.X / _zoom);
        py = (int)Math.Floor(p.Y / _zoom);
        return _doc.InBounds(px, py);
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ToPixel(e, out int px, out int py)) return;
        PixelImage.CapturePointer(e.Pointer);

        switch (_tool)
        {
            case Tool.Eyedropper:
                var picked = _doc.GetCompositePixel(px, py);
                if ((picked >> 24) != 0) { SetPrimary(picked); }
                break;

            case Tool.Fill:
                var act = _doc.FloodFill(px, py, _primary);
                if (act != null) { AddRecent(_primary); RedrawCanvas(); }
                break;

            case Tool.Select:
                BeginSelect(px, py);
                break;

            default: // Pencil / Eraser
                _stroke = _doc.BeginStroke();
                _lastX = px; _lastY = py;
                PaintPixel(px, py);
                RedrawCanvas();
                break;
        }
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!ToPixel(e, out int px, out int py))
        {
            // allow drag past edge for select; clamp
            var p = e.GetCurrentPoint(PixelImage).Position;
            px = Math.Clamp((int)Math.Floor(p.X / _zoom), 0, _doc.Width - 1);
            py = Math.Clamp((int)Math.Floor(p.Y / _zoom), 0, _doc.Height - 1);
        }

        if (_tool == Tool.Select)
        {
            if (_movingSelection) { UpdateSelectMove(px, py); }
            else if (_draggingSelectRect) { _selX1 = px; _selY1 = py; DrawOverlay(); }
            return;
        }

        if (_stroke is null) return; // not drawing
        if (px == _lastX && py == _lastY) return;
        // Bresenham line to fill gaps between pointer samples.
        DrawLinePixels(_lastX, _lastY, px, py);
        _lastX = px; _lastY = py;
        RedrawCanvas();
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        PixelImage.ReleasePointerCapture(e.Pointer);
        if (_stroke != null)
        {
            _doc.CommitStroke(_stroke);
            if (_tool == Tool.Pencil) AddRecent(_primary);
            _stroke = null;
            UpdateUndoButtons();
        }
        if (_tool == Tool.Select)
        {
            _draggingSelectRect = false;
            if (_movingSelection) EndSelectMove();
        }
    }

    private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e) { }

    private void PaintPixel(int x, int y)
    {
        if (_stroke is null) return;
        uint c = _tool == Tool.Eraser ? 0u : _primary;
        _doc.SetPixel(_stroke, x, y, c);
    }

    private void DrawLinePixels(int x0, int y0, int x1, int y1)
    {
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            PaintPixel(x0, y0);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    // ===================== Selection =====================

    private bool _draggingSelectRect;

    private void BeginSelect(int px, int py)
    {
        if (_hasSelection && InSelection(px, py))
        {
            _movingSelection = true;
            _moveStartX = px; _moveStartY = py;
            _moveDx = 0; _moveDy = 0;
            return;
        }
        _hasSelection = true;
        _draggingSelectRect = true;
        _selX0 = px; _selY0 = py; _selX1 = px; _selY1 = py;
        DrawOverlay();
    }

    private bool InSelection(int px, int py)
    {
        int xa = Math.Min(_selX0, _selX1), xb = Math.Max(_selX0, _selX1);
        int ya = Math.Min(_selY0, _selY1), yb = Math.Max(_selY0, _selY1);
        return px >= xa && px <= xb && py >= ya && py <= yb;
    }

    private int _moveDx, _moveDy;

    private void UpdateSelectMove(int px, int py)
    {
        _moveDx = px - _moveStartX;
        _moveDy = py - _moveStartY;
        // Live preview: shift the overlay rect only; commit on release.
        DrawOverlay();
        var rect = new Rectangle
        {
            Width = (Math.Abs(_selX1 - _selX0) + 1) * _zoom,
            Height = (Math.Abs(_selY1 - _selY0) + 1) * _zoom,
            Stroke = new SolidColorBrush(Colors.Orange),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 2, 2 },
        };
        Canvas.SetLeft(rect, (Math.Min(_selX0, _selX1) + _moveDx) * _zoom);
        Canvas.SetTop(rect, (Math.Min(_selY0, _selY1) + _moveDy) * _zoom);
        OverlayCanvas.Children.Add(rect);
    }

    private void EndSelectMove()
    {
        _movingSelection = false;
        if (_moveDx != 0 || _moveDy != 0)
        {
            _doc.MoveRect(_selX0, _selY0, _selX1, _selY1, _moveDx, _moveDy);
            _selX0 += _moveDx; _selX1 += _moveDx;
            _selY0 += _moveDy; _selY1 += _moveDy;
            RedrawCanvas();
        }
        _moveDx = _moveDy = 0;
        DrawOverlay();
    }

    private void DeleteSelection()
    {
        if (!_hasSelection) return;
        _doc.ClearRect(_selX0, _selY0, _selX1, _selY1);
        RedrawCanvas();
    }

    // ===================== Command bar =====================

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var wBox = new NumberBox { Value = 32, Minimum = 1, Maximum = PixelEditorService.MaxSize, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Header = P("Width", "闊度") };
        var hBox = new NumberBox { Value = 32, Minimum = 1, Maximum = PixelEditorService.MaxSize, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Header = P("Height", "高度"), Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = P($"Size up to {PixelEditorService.MaxSize}×{PixelEditorService.MaxSize}.", $"尺寸最大 {PixelEditorService.MaxSize}×{PixelEditorService.MaxSize}。"), Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] });
        panel.Children.Add(wBox);
        panel.Children.Add(hBox);
        var dlg = new ContentDialog
        {
            Title = P("New canvas", "新畫布"),
            Content = panel,
            PrimaryButtonText = P("Create", "建立"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        int w = (int)(double.IsNaN(wBox.Value) ? 32 : wBox.Value);
        int h = (int)(double.IsNaN(hBox.Value) ? 32 : hBox.Value);
        _doc.NewDocument(w, h);
        _hasSelection = false;
        _lastSavedPath = null;
        RebuildBitmaps();
        RefreshLayers();
        RefreshFrames();
        DelayBox.Value = _doc.ActiveFrame.DelayMs;
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".png", ".gif", ".bmp", ".jpg", ".jpeg");
        if (string.IsNullOrEmpty(path)) return;
        var r = await _doc.ImportImageAsync(path);
        _hasSelection = false;
        RebuildBitmaps();
        RefreshLayers();
        RefreshFrames();
        await ToastAsync(r.Message?.Primary ?? "");
    }

    private void Undo_Click(object sender, RoutedEventArgs e) { _doc.Undo(); RedrawCanvas(); }
    private void Redo_Click(object sender, RoutedEventArgs e) { _doc.Redo(); RedrawCanvas(); }

    private void UpdateUndoButtons()
    {
        UndoBtn.IsEnabled = _doc.CanUndo;
        RedoBtn.IsEnabled = _doc.CanRedo;
    }

    private void Zoom_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _zoom = Math.Max(1, (int)e.NewValue);
        if (IsLoaded) ApplyZoom();
    }

    private void Grid_Click(object sender, RoutedEventArgs e)
    {
        _showGrid = GridCheck.IsChecked == true;
        DrawOverlay();
    }

    private async void ExportPng_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync($"sprite-frame{_doc.ActiveFrameIndex + 1}", ".png");
        if (string.IsNullOrEmpty(path)) return;
        var r = await _doc.ExportPngAsync(path, _doc.ActiveFrameIndex);
        if (r.Success) _lastSavedPath = path;
        await ToastAsync(r.Message?.Primary ?? "");
    }

    private async void ExportGif_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("sprite", ".gif");
        if (string.IsNullOrEmpty(path)) return;
        var r = await _doc.ExportGifAsync(path);
        if (r.Success) _lastSavedPath = path;
        await ToastAsync(r.Message?.Primary ?? "");
    }

    // ===================== Aseprite detect / launch =====================

    private void DetectAseprite()
    {
        var exe = PixelEditorService.FindAseprite();
        AsepriteBtn.Visibility = exe is null ? Visibility.Collapsed : Visibility.Visible;
        if (exe is not null)
            ToolTipService.SetToolTip(AsepriteBtn, P($"Found: {exe}", $"已偵測：{exe}"));
    }

    private async void Aseprite_Click(object sender, RoutedEventArgs e)
    {
        // Offer to open the last exported file too.
        if (!string.IsNullOrEmpty(_lastSavedPath))
        {
            var dlg = new ContentDialog
            {
                Title = P("Launch Aseprite", "啟動 Aseprite"),
                Content = P("Open your last exported file in Aseprite, or just open Aseprite?",
                            "喺 Aseprite 開返你最後匯出嘅檔案，定係淨係開 Aseprite？"),
                PrimaryButtonText = P("Open last file", "開最後檔案"),
                SecondaryButtonText = P("Just open Aseprite", "淨係開 Aseprite"),
                CloseButtonText = P("Cancel", "取消"),
                XamlRoot = XamlRoot,
            };
            var res = await dlg.ShowAsync();
            if (res == ContentDialogResult.None) return;
            PixelEditorService.LaunchAseprite(res == ContentDialogResult.Primary ? _lastSavedPath : null);
            return;
        }
        if (!PixelEditorService.LaunchAseprite())
            await ToastAsync(P("Could not launch Aseprite.", "無法啟動 Aseprite。"));
    }

    // ===================== Layers =====================

    private bool _syncingLayers;

    private void RefreshLayers()
    {
        _syncingLayers = true;
        LayersList.Items.Clear();
        var layers = _doc.ActiveFrame.Layers;
        // top layer shown first
        for (int i = layers.Count - 1; i >= 0; i--)
            LayersList.Items.Add(MakeLayerRow(layers[i], i));
        // selected index in the reversed list
        LayersList.SelectedIndex = layers.Count - 1 - _doc.ActiveLayerIndex;
        OpacitySlider.Value = _doc.ActiveLayer.Opacity * 100;
        _syncingLayers = false;
    }

    private Grid MakeLayerRow(PixelLayer layer, int index)
    {
        var g = new Grid { Tag = index };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var vis = new CheckBox { IsChecked = layer.Visible, MinWidth = 0, Margin = new Thickness(0, 0, 6, 0) };
        ToolTipService.SetToolTip(vis, P("Toggle visibility", "切換顯示"));
        vis.Click += (_, _) => { layer.Visible = vis.IsChecked == true; RedrawCanvas(); };
        Grid.SetColumn(vis, 0);
        var tb = new TextBlock { Text = layer.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(tb, 1);
        g.Children.Add(vis);
        g.Children.Add(tb);
        return g;
    }

    private void Layers_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingLayers || LayersList.SelectedItem is not Grid g || g.Tag is not int idx) return;
        _doc.SetActiveLayer(idx);
        OpacitySlider.Value = _doc.ActiveLayer.Opacity * 100;
    }

    private void LayerAdd_Click(object sender, RoutedEventArgs e) { _doc.AddLayer(); RefreshLayers(); RedrawCanvas(); }
    private void LayerDel_Click(object sender, RoutedEventArgs e) { _doc.DeleteLayer(); RefreshLayers(); RedrawCanvas(); }
    private void LayerUp_Click(object sender, RoutedEventArgs e) { _doc.MoveLayer(+1); RefreshLayers(); RedrawCanvas(); }
    private void LayerDown_Click(object sender, RoutedEventArgs e) { _doc.MoveLayer(-1); RefreshLayers(); RedrawCanvas(); }

    private void Opacity_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_syncingLayers || !IsLoaded) return;
        _doc.ActiveLayer.Opacity = e.NewValue / 100.0;
        RedrawCanvas();
    }

    // ===================== Frames =====================

    private bool _syncingFrames;

    private void RefreshFrames()
    {
        _syncingFrames = true;
        FramesList.Items.Clear();
        for (int i = 0; i < _doc.Frames.Count; i++)
        {
            var row = new TextBlock { Text = P($"Frame {i + 1}", $"影格 {i + 1}") + $"  ({_doc.Frames[i].DelayMs} ms)", Tag = i };
            FramesList.Items.Add(row);
        }
        FramesList.SelectedIndex = _doc.ActiveFrameIndex;
        _syncingFrames = false;
    }

    private void Frames_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingFrames || FramesList.SelectedItem is not TextBlock tb || tb.Tag is not int idx) return;
        _doc.SetActiveFrame(idx);
        RefreshLayers();
        RedrawCanvas();
        DelayBox.Value = _doc.ActiveFrame.DelayMs;
    }

    private void FrameAdd_Click(object sender, RoutedEventArgs e) { _doc.AddFrame(); RefreshFrames(); RefreshLayers(); RedrawCanvas(); }
    private void FrameDup_Click(object sender, RoutedEventArgs e) { _doc.DuplicateFrame(); RefreshFrames(); RefreshLayers(); RedrawCanvas(); }
    private void FrameDel_Click(object sender, RoutedEventArgs e) { _doc.DeleteFrame(); RefreshFrames(); RefreshLayers(); RedrawCanvas(); }

    private void FrameLeft_Click(object sender, RoutedEventArgs e)
    {
        int i = _doc.ActiveFrameIndex;
        if (i > 0) { _doc.MoveFrame(i, i - 1); RefreshFrames(); }
    }

    private void FrameRight_Click(object sender, RoutedEventArgs e)
    {
        int i = _doc.ActiveFrameIndex;
        if (i < _doc.Frames.Count - 1) { _doc.MoveFrame(i, i + 1); RefreshFrames(); }
    }

    private void Delay_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_syncingFrames || double.IsNaN(args.NewValue)) return;
        _doc.ActiveFrame.DelayMs = (int)Math.Clamp(args.NewValue, 10, 10000);
        // update the label in the list without losing selection
        if (FramesList.SelectedItem is TextBlock tb)
            tb.Text = P($"Frame {_doc.ActiveFrameIndex + 1}", $"影格 {_doc.ActiveFrameIndex + 1}") + $"  ({_doc.ActiveFrame.DelayMs} ms)";
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_playTimer is { IsEnabled: true })
        {
            _playTimer.Stop();
            PlayText.Text = P("Play", "播放");
            _doc.SetActiveFrame(_playFrame);
            RefreshFrames();
            RedrawCanvas();
            return;
        }
        if (_doc.Frames.Count < 2) return;
        _playFrame = _doc.ActiveFrameIndex;
        _playTimer = new DispatcherTimer();
        AdvancePlay();
        PlayText.Text = P("Stop", "停止");
    }

    private void AdvancePlay()
    {
        if (_playTimer is null) return;
        _playTimer.Stop();
        int show = _playFrame % _doc.Frames.Count;
        _doc.SetActiveFrame(show);
        RedrawCanvas();
        _playTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(10, _doc.Frames[show].DelayMs));
        _playTimer.Tick -= PlayTick;
        _playTimer.Tick += PlayTick;
        _playTimer.Start();
    }

    private void PlayTick(object? sender, object e)
    {
        _playFrame = (_playFrame + 1) % _doc.Frames.Count;
        AdvancePlay();
    }

    // ===================== Keyboard shortcuts =====================

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                    & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        if (ctrl && e.Key == Windows.System.VirtualKey.Z) { _doc.Undo(); RedrawCanvas(); e.Handled = true; return; }
        if (ctrl && e.Key == Windows.System.VirtualKey.Y) { _doc.Redo(); RedrawCanvas(); e.Handled = true; return; }
        // Don't hijack single-letter tool keys while a text field has focus.
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or NumberBox) { base.OnKeyDown(e); return; }
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Delete:
            case Windows.System.VirtualKey.Back:
                DeleteSelection(); e.Handled = true; break;
            case Windows.System.VirtualKey.B: _tool = Tool.Pencil; HighlightTool(); break;
            case Windows.System.VirtualKey.E: _tool = Tool.Eraser; HighlightTool(); break;
            case Windows.System.VirtualKey.G: _tool = Tool.Fill; HighlightTool(); break;
            case Windows.System.VirtualKey.I: _tool = Tool.Eyedropper; HighlightTool(); break;
            case Windows.System.VirtualKey.M: _tool = Tool.Select; HighlightTool(); break;
        }
        base.OnKeyDown(e);
    }

    // ===================== Helpers =====================

    private async System.Threading.Tasks.Task ToastAsync(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        var dlg = new ContentDialog
        {
            Title = "Pixel Editor · 像素畫編輯器",
            Content = message,
            CloseButtonText = P("OK", "好"),
            XamlRoot = XamlRoot,
        };
        try { await dlg.ShowAsync(); } catch { }
    }

    private static SolidColorBrush BgraBrush(uint bgra)
    {
        byte b = (byte)(bgra & 0xFF);
        byte g = (byte)((bgra >> 8) & 0xFF);
        byte r = (byte)((bgra >> 16) & 0xFF);
        byte a = (byte)((bgra >> 24) & 0xFF);
        return new SolidColorBrush(Color.FromArgb(a, r, g, b));
    }

    private static string HexOf(uint bgra)
    {
        byte b = (byte)(bgra & 0xFF);
        byte g = (byte)((bgra >> 8) & 0xFF);
        byte r = (byte)((bgra >> 16) & 0xFF);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static bool TryParseHex(string? text, out uint bgra)
    {
        bgra = 0xFF000000;
        var s = (text ?? "").Trim().TrimStart('#');
        if (s.Length == 8) s = s.Substring(2); // ignore alpha prefix if given
        if (s.Length != 6) return false;
        if (byte.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) &&
            byte.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) &&
            byte.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            bgra = 0xFF000000u | ((uint)r << 16) | ((uint)g << 8) | b;
            return true;
        }
        return false;
    }
}
