using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;
using WinForge.Services;
using ISImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32>;

namespace WinForge.Pages;

/// <summary>
/// 點陣圖（相片）影像編輯器 · A native, general-purpose raster (photo) image editor — Paint.NET / GIMP-style,
/// distinct from the Aseprite-style Pixel Editor. 完全原生 WinForge 程式碼，唔啟動／唔捆綁 Paint.NET、GIMP
/// 或任何外部工具；所有處理用純託管 SixLabors.ImageSharp 喺程序內完成。Original WinForge code — launches NO
/// external tool; all processing runs in-process on managed SixLabors.ImageSharp. Open PNG/JPG/BMP/GIF/WebP;
/// zoom &amp; pan canvas; live brightness/contrast/saturation/hue/gamma; grayscale/invert/sepia/blur/sharpen/
/// edge filters; crop/resize/rotate/flip; brush/fill/eraser/eyedropper/text paint tools; simple multi-layer
/// model with opacity + show/hide; undo/redo; Save / Save As (PNG/JPG/BMP/GIF/WebP with quality).
/// </summary>
public sealed partial class ImageEditorModule : Page
{
    private enum Tool { Move, Brush, Eraser, Fill, Eyedropper, Text, Crop }

    private readonly ImageEditorService _doc = new(800, 600);
    private Tool _tool = Tool.Brush;
    private uint _primary = 0xFF1E1E1E;     // packed BGRA (opaque dark grey)
    private int _brushSize = 6;

    private WriteableBitmap? _bmp;
    private WriteableBitmap? _checker;
    private double _zoom = 1.0;             // display scale

    private readonly List<Button> _toolButtons = new();
    private bool _syncingLayers;
    private bool _suppressResize;

    // Brush stroke state
    private bool _drawing;
    private int _lastX = -1, _lastY = -1;

    // Crop rect (image pixel coords)
    private bool _hasCrop;
    private bool _draggingCrop;
    private int _cropX0, _cropY0, _cropX1, _cropY1;

    // Live adjustment preview (committed = baked into layer)
    private ImageEditorService.Adjustments _adjust = ImageEditorService.Adjustments.Neutral;
    private bool _adjusting;

    public ImageEditorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; _doc.Dispose(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => RenderText();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildTools();
        RenderText();
        RebuildBitmaps();
        RefreshLayers();
        UpdatePrimarySwatch();
        SyncResizeBoxes();
        UpdateUndoButtons();
    }

    // ===================== Localisation =====================

    private void RenderText()
    {
        Header.Title = "Image Editor · 點陣圖影像編輯器";
        HeaderBlurb.Text = P("A native raster photo editor — open PNG/JPG/BMP/GIF/WebP, then adjust, filter, transform, paint and layer your image. All processing runs in-app on managed code; no Paint.NET, GIMP or external tool is launched.",
            "原生點陣圖相片編輯器 — 開 PNG/JPG/BMP/GIF/WebP，再調色、套濾鏡、變形、繪畫同分層。所有處理喺 app 內用託管程式碼完成；唔會啟動 Paint.NET、GIMP 或任何外部工具。");

        NewBtnText.Text = P("New", "新建");
        OpenBtnText.Text = P("Open…", "開啟…");
        SaveBtnText.Text = P("Save", "儲存");
        SaveAsBtnText.Text = P("Save As…", "另存…");
        UndoBtnText.Text = P("Undo", "復原");
        RedoBtnText.Text = P("Redo", "重做");
        ZoomLabel.Text = P("Zoom", "縮放");
        FitBtnText.Text = P("Fit", "符合");
        EmptyHintText.Text = P("Open an image to start", "開一張圖開始");

        BrushSizeLabel.Text = P("Size", "大小");

        AdjustHeader.Text = P("Adjustments · 調整", "調整");
        BrightLabel.Text = P("Brightness", "亮度");
        ContrastLabel.Text = P("Contrast", "對比");
        SatLabel.Text = P("Saturation", "飽和度");
        HueLabel.Text = P("Hue", "色相");
        GammaLabel.Text = P("Gamma", "伽瑪");
        AdjustApplyBtn.Content = P("Apply", "套用");
        AdjustResetBtn.Content = P("Reset", "重設");

        FilterHeader.Text = P("Filters · 濾鏡", "濾鏡");
        FltGrayBtn.Content = P("Grayscale", "灰階");
        FltInvertBtn.Content = P("Invert", "反相");
        FltSepiaBtn.Content = P("Sepia", "棕褐");
        FltBlurBtn.Content = P("Gaussian Blur", "高斯模糊");
        FltSharpBtn.Content = P("Sharpen", "銳化");
        FltEdgeBtn.Content = P("Edge Detect", "邊緣偵測");
        BlurAmtLabel.Text = P("Blur/Sharpen amount", "模糊／銳化強度");

        TransformHeader.Text = P("Transform · 變形", "變形");
        FlipHText.Text = P("Flip H", "水平翻轉");
        FlipVText.Text = P("Flip V", "垂直翻轉");
        RotArbText.Text = P("Rotate°", "旋轉°");
        ResizeLabel.Text = P("Resize", "縮放尺寸");
        AspectLockChk.Content = P("Lock aspect ratio", "鎖定長寬比");
        ResizeApplyBtn.Content = P("Apply resize", "套用縮放");
        CropHint.Text = P("Pick the Crop tool, then drag a rectangle on the canvas.", "揀裁切工具，喺畫布拖一個矩形。");
        CropApplyBtn.Content = P("Apply crop", "套用裁切");

        LayersHeader.Text = P("Layers · 圖層", "圖層");
        OpacityLabel.Text = P("Opacity", "不透明度");

        ToolTipService.SetToolTip(NewBtn, P("New blank document", "新建空白文件"));
        ToolTipService.SetToolTip(OpenBtn, P("Open an image file", "開啟影像檔"));
        ToolTipService.SetToolTip(SaveBtn, P("Save (overwrites current file)", "儲存（覆寫目前檔案）"));
        ToolTipService.SetToolTip(SaveAsBtn, P("Save as a new file / format", "另存成新檔／格式"));
        ToolTipService.SetToolTip(ColorBtn, P("Pick brush / text colour", "揀筆刷／文字顏色"));
        ToolTipService.SetToolTip(RotCwBtn, P("Rotate 90° clockwise", "順時針旋轉 90°"));
        ToolTipService.SetToolTip(RotCcwBtn, P("Rotate 90° counter-clockwise", "逆時針旋轉 90°"));
        ToolTipService.SetToolTip(LayerFlatBtn, P("Flatten all layers", "合併所有圖層"));
        RefreshToolTips();
        UpdateBrushLabel();
    }

    // ===================== Tools =====================

    private static readonly (Tool t, string glyph, string en, string zh)[] ToolDefs =
    {
        (Tool.Move, "", "Move / pan", "移動／平移"),
        (Tool.Brush, "", "Brush", "筆刷"),
        (Tool.Eraser, "", "Eraser", "橡皮"),
        (Tool.Fill, "", "Bucket fill", "油桶填色"),
        (Tool.Eyedropper, "", "Eyedropper", "吸色"),
        (Tool.Text, "", "Text", "文字"),
        (Tool.Crop, "", "Crop (drag a rectangle)", "裁切（拖矩形）"),
    };

    private void BuildTools()
    {
        _toolButtons.Clear();
        ToolStack.Children.Clear();
        foreach (var d in ToolDefs)
        {
            var b = new Button
            {
                Width = 40,
                Height = 40,
                Padding = new Thickness(0),
                Content = new FontIcon { FontSize = 16, Glyph = d.glyph },
                Tag = d.t,
            };
            ToolTipService.SetToolTip(b, P(d.en, d.zh));
            b.Click += (_, _) => { _tool = d.t; HighlightTool(); UpdateCropButton(); };
            _toolButtons.Add(b);
            ToolStack.Children.Add(b);
        }
        HighlightTool();
    }

    private void RefreshToolTips()
    {
        for (int i = 0; i < _toolButtons.Count && i < ToolDefs.Length; i++)
            ToolTipService.SetToolTip(_toolButtons[i], P(ToolDefs[i].en, ToolDefs[i].zh));
    }

    private void HighlightTool()
    {
        var accent = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        foreach (var b in _toolButtons)
            b.Background = (Tool)b.Tag! == _tool ? accent : null;
    }

    private void BrushSize_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _brushSize = Math.Max(1, (int)e.NewValue);
        UpdateBrushLabel();
    }

    private void UpdateBrushLabel() => BrushSizeLabel.Text = P("Size", "大小") + $" {_brushSize}";

    // ===================== Bitmap rendering =====================

    private void RebuildBitmaps()
    {
        int w = _doc.Width, h = _doc.Height;
        _bmp = new WriteableBitmap(w, h);
        CanvasImage.Source = _bmp;

        _checker = BuildChecker(w, h);
        CheckerImage.Source = _checker;

        ApplyZoom();
        RedrawCanvas();
        DimText.Text = $"{w} × {h} px";
        SyncResizeBoxes();
    }

    private static WriteableBitmap BuildChecker(int w, int h)
    {
        var cb = new WriteableBitmap(w, h);
        var buf = cb.PixelBuffer.ToArray();
        const int cell = 16;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool dark = ((x / cell) + (y / cell)) % 2 == 0;
                byte v = (byte)(dark ? 200 : 230);
                int i = (y * w + x) * 4;
                buf[i] = v; buf[i + 1] = v; buf[i + 2] = v; buf[i + 3] = 255;
            }
        using (var s = cb.PixelBuffer.AsStream()) s.Write(buf, 0, buf.Length);
        cb.Invalidate();
        return cb;
    }

    private void ApplyZoom()
    {
        int w = _doc.Width, h = _doc.Height;
        double pw = w * _zoom, ph = h * _zoom;
        CanvasImage.Width = pw; CanvasImage.Height = ph;
        CheckerImage.Width = pw; CheckerImage.Height = ph;
        OverlayCanvas.Width = pw; OverlayCanvas.Height = ph;
        CanvasWrap.Width = pw; CanvasWrap.Height = ph;
        ZoomLabel.Text = P("Zoom", "縮放") + $" {_zoom * 100:0}%";
        DrawOverlay();
    }

    /// <summary>合成（含活躍調整預覽）寫入 WriteableBitmap · Composite (with live adjustment preview) to the bitmap.</summary>
    private void RedrawCanvas()
    {
        if (_bmp is null) return;
        byte[] bgra;
        if (_adjusting && !_adjust.IsNeutral)
        {
            // Preview: composite, then apply non-destructive adjustments to the flattened copy.
            using var flat = _doc.CompositeImage();
            using var prev = ImageEditorService.ApplyAdjustmentsPreview(flat, _adjust);
            bgra = new byte[_doc.Width * _doc.Height * 4];
            prev.CopyPixelDataTo(bgra);
        }
        else
        {
            bgra = _doc.CompositeToBgra();
        }
        using (var s = _bmp.PixelBuffer.AsStream()) s.Write(bgra, 0, bgra.Length);
        _bmp.Invalidate();
        UpdateUndoButtons();
    }

    private void DrawOverlay()
    {
        OverlayCanvas.Children.Clear();
        if (!_hasCrop) return;
        int xa = Math.Min(_cropX0, _cropX1), xb = Math.Max(_cropX0, _cropX1);
        int ya = Math.Min(_cropY0, _cropY1), yb = Math.Max(_cropY0, _cropY1);
        var rect = new Rectangle
        {
            Width = Math.Max(1, (xb - xa)) * _zoom,
            Height = Math.Max(1, (yb - ya)) * _zoom,
            Stroke = new SolidColorBrush(Colors.DodgerBlue),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)),
        };
        Canvas.SetLeft(rect, xa * _zoom);
        Canvas.SetTop(rect, ya * _zoom);
        OverlayCanvas.Children.Add(rect);
    }

    // ===================== Pointer / paint =====================

    private bool ToPixel(PointerRoutedEventArgs e, out int px, out int py)
    {
        var p = e.GetCurrentPoint(CanvasImage).Position;
        px = (int)Math.Floor(p.X / _zoom);
        py = (int)Math.Floor(p.Y / _zoom);
        return px >= 0 && py >= 0 && px < _doc.Width && py < _doc.Height;
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!ToPixel(e, out int px, out int py)) return;
        CanvasImage.CapturePointer(e.Pointer);

        switch (_tool)
        {
            case Tool.Eyedropper:
                var picked = _doc.GetCompositePixel(px, py);
                if ((picked >> 24) != 0) { _primary = picked | 0xFF000000; UpdatePrimarySwatch(); }
                break;

            case Tool.Fill:
                _doc.PushUndo();
                _doc.FloodFill(px, py, _primary);
                RedrawCanvas();
                break;

            case Tool.Text:
                _ = ShowTextDialog(px, py);
                break;

            case Tool.Crop:
                _hasCrop = true;
                _draggingCrop = true;
                _cropX0 = px; _cropY0 = py; _cropX1 = px; _cropY1 = py;
                DrawOverlay();
                break;

            case Tool.Brush:
            case Tool.Eraser:
                _doc.PushUndo();
                _drawing = true;
                _lastX = px; _lastY = py;
                _doc.BrushLine(px, py, px, py, _brushSize, _primary, _tool == Tool.Eraser);
                RedrawCanvas();
                break;

            case Tool.Move:
                // panning handled by ScrollViewer; nothing to do
                break;
        }
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Clamp so dragging past the edge still works for crop / brush.
        var p = e.GetCurrentPoint(CanvasImage).Position;
        int px = Math.Clamp((int)Math.Floor(p.X / _zoom), 0, _doc.Width - 1);
        int py = Math.Clamp((int)Math.Floor(p.Y / _zoom), 0, _doc.Height - 1);

        if (_tool == Tool.Crop && _draggingCrop)
        {
            _cropX1 = px; _cropY1 = py;
            DrawOverlay();
            return;
        }

        if (_drawing && (_tool == Tool.Brush || _tool == Tool.Eraser))
        {
            if (px == _lastX && py == _lastY) return;
            _doc.BrushLine(_lastX, _lastY, px, py, _brushSize, _primary, _tool == Tool.Eraser);
            _lastX = px; _lastY = py;
            RedrawCanvas();
        }
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        CanvasImage.ReleasePointerCapture(e.Pointer);
        _drawing = false;
        if (_tool == Tool.Crop && _draggingCrop)
        {
            _draggingCrop = false;
            UpdateCropButton();
        }
    }

    private void UpdateCropButton()
    {
        bool ready = _tool == Tool.Crop && _hasCrop &&
                     Math.Abs(_cropX1 - _cropX0) > 1 && Math.Abs(_cropY1 - _cropY0) > 1;
        CropApplyBtn.IsEnabled = ready;
    }

    private async Task ShowTextDialog(int px, int py)
    {
        var input = new TextBox { PlaceholderText = P("Type your text…", "輸入文字…"), AcceptsReturn = true, MinWidth = 320 };
        var sizeBox = new NumberBox { Header = P("Font size (px)", "字型大小（px）"), Value = 36, Minimum = 4, Maximum = 1000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        var fontBox = new ComboBox { Header = P("Font", "字型"), HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var f in ImageEditorService.AvailableFonts().Take(400)) fontBox.Items.Add(f);
        fontBox.SelectedItem = fontBox.Items.Contains("Segoe UI") ? "Segoe UI" : (fontBox.Items.Count > 0 ? fontBox.Items[0] : null);
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(input);
        panel.Children.Add(sizeBox);
        panel.Children.Add(fontBox);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Add text", "加入文字"),
            Content = panel,
            PrimaryButtonText = P("Add · 加入", "加入"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary || string.IsNullOrEmpty(input.Text)) return;
        float size = (float)(double.IsNaN(sizeBox.Value) ? 36 : sizeBox.Value);
        var r = _doc.DrawText(input.Text, px, py, size, _primary, fontBox.SelectedItem as string);
        RedrawCanvas();
        if (!r.Success) ShowToast(InfoBarSeverity.Error, r.Message?.Primary ?? "");
    }

    // ===================== Command bar =====================

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var wBox = new NumberBox { Header = P("Width", "闊度"), Value = 800, Minimum = 1, Maximum = 16384, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var hBox = new NumberBox { Header = P("Height", "高度"), Value = 600, Minimum = 1, Maximum = 16384, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Margin = new Thickness(0, 8, 0, 0) };
        var white = new CheckBox { Content = P("White background (else transparent)", "白色背景（否則透明）"), IsChecked = true, Margin = new Thickness(0, 8, 0, 0) };
        var panel = new StackPanel();
        panel.Children.Add(wBox); panel.Children.Add(hBox); panel.Children.Add(white);
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = P("New document", "新建文件"), Content = panel,
            PrimaryButtonText = P("Create · 建立", "建立"), CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        int w = (int)(double.IsNaN(wBox.Value) ? 800 : wBox.Value);
        int h = (int)(double.IsNaN(hBox.Value) ? 600 : hBox.Value);
        _doc.NewDocument(w, h, white.IsChecked == true);
        ResetAdjustSliders();
        _hasCrop = false;
        RebuildBitmaps();
        RefreshLayers();
    }

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp");
        if (string.IsNullOrEmpty(path)) return;
        var r = await _doc.OpenAsync(path);
        ResetAdjustSliders();
        _hasCrop = false;
        // Fit large images into view on open.
        _zoom = 1.0;
        RebuildBitmaps();
        RefreshLayers();
        FitToView();
        ShowToast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, r.Message?.Primary ?? "");
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_doc.FilePath)) { SaveAs_Click(sender, e); return; }
        var r = await _doc.SaveAsync(_doc.FilePath);
        ShowToast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, r.Message?.Primary ?? "");
    }

    private async void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var filters = new List<FileDialogs.Filter>
        {
            new("PNG image", "*.png"),
            new("JPEG image", "*.jpg;*.jpeg"),
            new("Bitmap", "*.bmp"),
            new("GIF", "*.gif"),
            new("WebP", "*.webp"),
        };
        var suggested = string.IsNullOrEmpty(_doc.FilePath)
            ? "image"
            : System.IO.Path.GetFileNameWithoutExtension(_doc.FilePath);
        var path = await FileDialogs.SaveFileAsync(suggested, filters, "png", P("Save image as", "另存影像"));
        if (string.IsNullOrEmpty(path)) return;

        int quality = 90;
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".webp")
        {
            var q = new Slider { Minimum = 1, Maximum = 100, Value = 90, Width = 280 };
            var lbl = new TextBlock { Text = P("Quality: 90", "品質：90") };
            q.ValueChanged += (_, ev) => lbl.Text = P($"Quality: {ev.NewValue:0}", $"品質：{ev.NewValue:0}");
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(lbl); panel.Children.Add(q);
            var dlg = new ContentDialog
            {
                XamlRoot = XamlRoot, Title = P("Export quality", "匯出品質"), Content = panel,
                PrimaryButtonText = P("Save · 儲存", "儲存"), CloseButtonText = P("Cancel · 取消", "取消"),
                DefaultButton = ContentDialogButton.Primary,
            };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
            quality = (int)q.Value;
        }
        var r = await _doc.SaveAsync(path, quality);
        ShowToast(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, r.Message?.Primary ?? "");
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        _doc.Undo();
        AfterStructuralChange();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        _doc.Redo();
        AfterStructuralChange();
    }

    /// <summary>復原／變形之後可能改咗尺寸或圖層數，要全部重建 · Rebuild after ops that may change size/layers.</summary>
    private void AfterStructuralChange()
    {
        RebuildBitmaps();
        RefreshLayers();
        _hasCrop = false;
        UpdateCropButton();
    }

    private void UpdateUndoButtons()
    {
        UndoBtn.IsEnabled = _doc.CanUndo;
        RedoBtn.IsEnabled = _doc.CanRedo;
    }

    private void Zoom_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        _zoom = Math.Max(0.05, e.NewValue / 100.0);
        if (IsLoaded) ApplyZoom();
    }

    private void Fit_Click(object sender, RoutedEventArgs e) => FitToView();
    private void Zoom100_Click(object sender, RoutedEventArgs e) { ZoomSlider.Value = 100; }

    private void FitToView()
    {
        double availW = CanvasScroll.ViewportWidth - 48;
        double availH = CanvasScroll.ViewportHeight - 48;
        if (availW <= 0 || availH <= 0) { availW = 700; availH = 500; }
        double z = Math.Min(availW / _doc.Width, availH / _doc.Height);
        z = Math.Clamp(z, 0.05, 8.0);
        ZoomSlider.Value = z * 100; // triggers Zoom_Changed -> ApplyZoom
    }

    // ===================== Adjustments =====================

    private void Adjust_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (!IsLoaded) return;
        _adjust = new ImageEditorService.Adjustments
        {
            Brightness = (float)(BrightSlider.Value / 100.0),
            Contrast = (float)(ContrastSlider.Value / 100.0),
            Saturation = (float)(SatSlider.Value / 100.0),
            Hue = (float)HueSlider.Value,
            Gamma = (float)(GammaSlider.Value / 100.0),
        };
        BrightLabel.Text = P("Brightness", "亮度") + $" {BrightSlider.Value:+0;-0;0}";
        ContrastLabel.Text = P("Contrast", "對比") + $" {ContrastSlider.Value:+0;-0;0}";
        SatLabel.Text = P("Saturation", "飽和度") + $" {SatSlider.Value:+0;-0;0}";
        HueLabel.Text = P("Hue", "色相") + $" {HueSlider.Value:+0;-0;0}°";
        GammaLabel.Text = P("Gamma", "伽瑪") + $" {GammaSlider.Value / 100.0:0.00}";
        _adjusting = true;
        RedrawCanvas();
    }

    private void AdjustApply_Click(object sender, RoutedEventArgs e)
    {
        if (_adjust.IsNeutral) return;
        _doc.CommitAdjustments(_adjust);
        ResetAdjustSliders();
        _adjusting = false;
        RedrawCanvas();
        ShowToast(InfoBarSeverity.Success, P("Adjustments applied", "已套用調整"));
    }

    private void AdjustReset_Click(object sender, RoutedEventArgs e)
    {
        ResetAdjustSliders();
        _adjusting = false;
        RedrawCanvas();
    }

    private void ResetAdjustSliders()
    {
        BrightSlider.Value = 0; ContrastSlider.Value = 0; SatSlider.Value = 0;
        HueSlider.Value = 0; GammaSlider.Value = 100;
        _adjust = ImageEditorService.Adjustments.Neutral;
        _adjusting = false;
    }

    // ===================== Filters =====================

    private void ApplyFilter(ImageEditorService.FilterKind kind)
    {
        _doc.ApplyFilter(kind, (float)BlurAmtSlider.Value);
        RedrawCanvas();
    }

    private void FilterGray_Click(object sender, RoutedEventArgs e) => ApplyFilter(ImageEditorService.FilterKind.Grayscale);
    private void FilterInvert_Click(object sender, RoutedEventArgs e) => ApplyFilter(ImageEditorService.FilterKind.Invert);
    private void FilterSepia_Click(object sender, RoutedEventArgs e) => ApplyFilter(ImageEditorService.FilterKind.Sepia);
    private void FilterBlur_Click(object sender, RoutedEventArgs e) => ApplyFilter(ImageEditorService.FilterKind.GaussianBlur);
    private void FilterSharpen_Click(object sender, RoutedEventArgs e) => ApplyFilter(ImageEditorService.FilterKind.Sharpen);
    private void FilterEdge_Click(object sender, RoutedEventArgs e) => ApplyFilter(ImageEditorService.FilterKind.EdgeDetect);

    // ===================== Transform =====================

    private void RotateCw_Click(object sender, RoutedEventArgs e) { _doc.Rotate90(true); AfterStructuralChange(); }
    private void RotateCcw_Click(object sender, RoutedEventArgs e) { _doc.Rotate90(false); AfterStructuralChange(); }
    private void FlipH_Click(object sender, RoutedEventArgs e) { _doc.Flip(true); RedrawCanvas(); }
    private void FlipV_Click(object sender, RoutedEventArgs e) { _doc.Flip(false); RedrawCanvas(); }

    private void RotateArb_Click(object sender, RoutedEventArgs e)
    {
        float deg = (float)(double.IsNaN(RotDegBox.Value) ? 0 : RotDegBox.Value);
        if (Math.Abs(deg % 360) < 0.01f) return;
        _doc.RotateArbitrary(deg);
        AfterStructuralChange();
    }

    private void SyncResizeBoxes()
    {
        _suppressResize = true;
        ResizeWBox.Value = _doc.Width;
        ResizeHBox.Value = _doc.Height;
        _suppressResize = false;
    }

    private void ResizeW_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressResize || AspectLockChk.IsChecked != true || double.IsNaN(args.NewValue)) return;
        _suppressResize = true;
        double ratio = _doc.Height / (double)_doc.Width;
        ResizeHBox.Value = Math.Max(1, Math.Round(args.NewValue * ratio));
        _suppressResize = false;
    }

    private void ResizeH_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppressResize || AspectLockChk.IsChecked != true || double.IsNaN(args.NewValue)) return;
        _suppressResize = true;
        double ratio = _doc.Width / (double)_doc.Height;
        ResizeWBox.Value = Math.Max(1, Math.Round(args.NewValue * ratio));
        _suppressResize = false;
    }

    private void ResizeApply_Click(object sender, RoutedEventArgs e)
    {
        int w = (int)(double.IsNaN(ResizeWBox.Value) ? _doc.Width : ResizeWBox.Value);
        int h = (int)(double.IsNaN(ResizeHBox.Value) ? _doc.Height : ResizeHBox.Value);
        _doc.Resize(w, h);
        AfterStructuralChange();
        ShowToast(InfoBarSeverity.Success, P($"Resized to {w}×{h}", $"已縮放至 {w}×{h}"));
    }

    private void CropApply_Click(object sender, RoutedEventArgs e)
    {
        if (!_hasCrop) return;
        int xa = Math.Min(_cropX0, _cropX1), xb = Math.Max(_cropX0, _cropX1);
        int ya = Math.Min(_cropY0, _cropY1), yb = Math.Max(_cropY0, _cropY1);
        _doc.Crop(xa, ya, xb - xa, yb - ya);
        AfterStructuralChange();
        ShowToast(InfoBarSeverity.Success, P("Cropped", "已裁切"));
    }

    // ===================== Layers =====================

    private void RefreshLayers()
    {
        _syncingLayers = true;
        LayersList.Items.Clear();
        // Top layer shown first.
        for (int i = _doc.Layers.Count - 1; i >= 0; i--)
            LayersList.Items.Add(MakeLayerRow(_doc.Layers[i], i));
        LayersList.SelectedIndex = _doc.Layers.Count - 1 - _doc.ActiveLayerIndex;
        OpacitySlider.Value = _doc.ActiveLayer.Opacity * 100;
        LayerDelBtn.IsEnabled = _doc.Layers.Count > 1;
        LayerFlatBtn.IsEnabled = _doc.Layers.Count > 1;
        _syncingLayers = false;
    }

    private Grid MakeLayerRow(ImageEditorService.Layer layer, int index)
    {
        var g = new Grid { Tag = index, ColumnSpacing = 6 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var vis = new CheckBox { IsChecked = layer.Visible, MinWidth = 0, Margin = new Thickness(0) };
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
    private void LayerFlatten_Click(object sender, RoutedEventArgs e) { _doc.FlattenAll(); RefreshLayers(); RedrawCanvas(); }

    private void Opacity_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_syncingLayers || !IsLoaded) return;
        _doc.ActiveLayer.Opacity = e.NewValue / 100.0;
        RedrawCanvas();
    }

    // ===================== Colour =====================

    private async void Color_Click(object sender, RoutedEventArgs e)
    {
        var picker = new ColorPicker
        {
            IsAlphaEnabled = true,
            Color = BgraToColor(_primary),
            ColorSpectrumShape = ColorSpectrumShape.Box,
        };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Pick colour", "揀顏色"),
            Content = picker,
            PrimaryButtonText = P("OK · 確定", "確定"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        var c = picker.Color;
        _primary = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
        UpdatePrimarySwatch();
    }

    private void UpdatePrimarySwatch() => PrimarySwatch.Background = new SolidColorBrush(BgraToColor(_primary));

    private static Color BgraToColor(uint bgra) =>
        Color.FromArgb((byte)(bgra >> 24), (byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF));

    // ===================== Keyboard shortcuts =====================

    protected override void OnKeyDown(KeyRoutedEventArgs e)
    {
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                    & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        if (ctrl && e.Key == Windows.System.VirtualKey.Z) { _doc.Undo(); AfterStructuralChange(); e.Handled = true; return; }
        if (ctrl && e.Key == Windows.System.VirtualKey.Y) { _doc.Redo(); AfterStructuralChange(); e.Handled = true; return; }
        if (ctrl && e.Key == Windows.System.VirtualKey.S) { Save_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
        if (FocusManager.GetFocusedElement(XamlRoot) is TextBox or NumberBox) { base.OnKeyDown(e); return; }
        switch (e.Key)
        {
            case Windows.System.VirtualKey.B: _tool = Tool.Brush; HighlightTool(); break;
            case Windows.System.VirtualKey.E: _tool = Tool.Eraser; HighlightTool(); break;
            case Windows.System.VirtualKey.G: _tool = Tool.Fill; HighlightTool(); break;
            case Windows.System.VirtualKey.I: _tool = Tool.Eyedropper; HighlightTool(); break;
            case Windows.System.VirtualKey.T: _tool = Tool.Text; HighlightTool(); break;
            case Windows.System.VirtualKey.C: _tool = Tool.Crop; HighlightTool(); UpdateCropButton(); break;
            case Windows.System.VirtualKey.V: _tool = Tool.Move; HighlightTool(); break;
        }
        base.OnKeyDown(e);
    }

    // ===================== Helpers =====================

    private void ShowToast(InfoBarSeverity sev, string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        Toast.Severity = sev;
        Toast.Message = message;
        Toast.IsOpen = true;
    }
}
