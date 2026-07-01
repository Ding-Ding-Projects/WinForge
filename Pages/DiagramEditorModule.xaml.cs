using System;
using System.Collections.Generic;
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
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using WinForge.Services;
using static WinForge.Services.DiagramService;
using IoPath = System.IO.Path;
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;

namespace WinForge.Pages;

/// <summary>
/// 原生圖表／流程圖編輯器 · Native draw.io / diagrams.net-style editor — pure managed C# on a WinUI Canvas.
/// 形狀調色盤、拖曳移動、拉柄縮放、雙擊改字、連線箭咀、樣式、多選、複製、層次、貼齊格線、JSON 存／載、匯出 PNG。
/// Shape palette, drag-to-move, resize handles, double-click rename, connector arrows, styling, multi-select,
/// duplicate, z-order, snap-to-grid, JSON save/load, PNG export — no external tool, no browser, no WebView.
/// </summary>
public sealed partial class DiagramEditorModule : Page
{
    private const double GridSize = 20;
    private const double HandleSize = 10;

    private DiagramDocument _doc = new();
    private readonly HashSet<string> _selected = new();
    private string? _currentPath;

    // Live XAML elements keyed by model id, so the model stays the single source of truth.
    private readonly Dictionary<string, FrameworkElement> _nodeVisuals = new();
    private readonly Dictionary<string, Border> _labelVisuals = new();
    private readonly Dictionary<string, ShapePath> _edgeVisuals = new();
    private readonly Dictionary<string, Border> _edgeLabelVisuals = new();
    private readonly List<Rectangle> _handles = new();

    // Interaction state.
    private bool _connectMode;
    private string? _connectFrom;
    private bool _suppressProps;
    private double _zoom = 1.0;

    private enum DragKind { None, Move, Resize, Marquee }
    private DragKind _drag = DragKind.None;
    private Point _dragStart;
    private string? _resizeNodeId;
    private int _resizeHandle;
    private Dictionary<string, Rect> _dragOrigin = new();
    private Rectangle? _marquee;

    public DiagramEditorModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
        Loaded += (_, _) => { Render(); RebuildAll(); UpdatePropsPanel(); };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ── Localised chrome ───────────────────────────────────────────────────────

    private void Render()
    {
        Header.Title = "Diagram Editor · 圖表編輯器";
        HeaderBlurb.Text = P("A native flowchart / diagram editor on a WinUI canvas — add shapes, drag to move, resize with handles, double-click to rename, connect shapes with arrows, restyle, then save to JSON or export a PNG. Everything runs in-app; nothing leaves your machine.",
            "喺 WinUI 畫布上嘅原生流程圖／圖表編輯器 — 加形狀、拖曳移動、用拉柄縮放、雙擊改字、用箭咀連接形狀、改樣式，再存做 JSON 或者匯出 PNG。全部喺 app 內運行，唔會離開你部機。");

        NewLbl.Text = P("New", "新建");
        OpenLbl.Text = P("Open", "開啟");
        SaveLbl.Text = P("Save", "儲存");
        ExportLbl.Text = P("Export PNG", "匯出 PNG");
        ConnectLbl.Text = P("Connect", "連線");
        SnapLbl.Text = P("Snap to grid", "貼齊格線");

        PaletteHeader.Text = P("Shapes · 形狀", "形狀");
        RectLbl.Text = P("Rectangle", "矩形");
        RoundLbl.Text = P("Rounded", "圓角矩形");
        EllipseLbl.Text = P("Ellipse", "橢圓");
        DiamondLbl.Text = P("Diamond", "菱形");
        TextLbl.Text = P("Text label", "文字標籤");
        PaletteHint.Text = P("Drag a shape to move it. Drag a corner to resize. Double-click to edit its label. Use Connect to draw arrows.",
            "拖曳形狀移動。拖角縮放。雙擊改文字。用「連線」畫箭咀。");

        PropsHeader.Text = P("Properties · 屬性", "屬性");
        PropsEmpty.Text = P("Select a shape or arrow to edit its style.", "揀一個形狀或箭咀去編輯佢嘅樣式。");
        LabelBox.Header = P("Label · 文字", "文字");
        FillLbl.Text = P("Fill colour · 填色", "填色");
        StrokeLbl.Text = P("Stroke colour · 邊框色", "邊框色");
        StrokeWidthBox.Header = P("Stroke width · 邊框粗幼", "邊框粗幼");
        FontSizeBox.Header = P("Font size · 字體大小", "字體大小");
        TextColorLbl.Text = P("Text colour · 文字色", "文字色");

        UpdateZoomText();
    }

    // ── Colour helpers ─────────────────────────────────────────────────────────

    private static Color ParseColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6) hex = "FF" + hex;
            byte a = Convert.ToByte(hex.Substring(0, 2), 16);
            byte r = Convert.ToByte(hex.Substring(2, 2), 16);
            byte g = Convert.ToByte(hex.Substring(4, 2), 16);
            byte b = Convert.ToByte(hex.Substring(6, 2), 16);
            return Color.FromArgb(a, r, g, b);
        }
        catch { return Color.FromArgb(255, 43, 87, 151); }
    }

    private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    private static SolidColorBrush Brush(string hex) => new(ParseColor(hex));

    // ── Building the visual tree from the model ────────────────────────────────

    private void RebuildAll()
    {
        DiagramCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _labelVisuals.Clear();
        _edgeVisuals.Clear();
        _edgeLabelVisuals.Clear();
        _handles.Clear();

        DiagramCanvas.Width = _doc.CanvasWidth;
        DiagramCanvas.Height = _doc.CanvasHeight;

        // Edges first (drawn under nodes).
        foreach (var edge in _doc.Edges) CreateEdgeVisual(edge);
        foreach (var node in _doc.Nodes) CreateNodeVisual(node);
        RefreshAllEdges();
        UpdateSelectionVisuals();
    }

    private void CreateNodeVisual(DiagramNode n)
    {
        FrameworkElement shape = n.Kind switch
        {
            ShapeKind.Ellipse => new Ellipse(),
            ShapeKind.Diamond => MakeDiamond(n),
            ShapeKind.Text => MakeTextShape(n),
            _ => new Rectangle { RadiusX = n.Kind == ShapeKind.RoundedRectangle ? 14 : 0, RadiusY = n.Kind == ShapeKind.RoundedRectangle ? 14 : 0 },
        };

        if (shape is Shape sh && n.Kind != ShapeKind.Text)
        {
            sh.Fill = Brush(n.Fill);
            sh.Stroke = Brush(n.Stroke);
            sh.StrokeThickness = n.StrokeWidth;
        }
        shape.Width = n.Width;
        shape.Height = n.Height;
        Canvas.SetLeft(shape, n.X);
        Canvas.SetTop(shape, n.Y);
        Canvas.SetZIndex(shape, 10);
        shape.Tag = n.Id;
        DiagramCanvas.Children.Add(shape);
        _nodeVisuals[n.Id] = shape;

        if (n.Kind != ShapeKind.Text)
        {
            var label = new Border
            {
                Width = n.Width,
                Height = n.Height,
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = n.Label,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = n.FontSize,
                    Foreground = Brush(n.TextColor),
                },
            };
            Canvas.SetLeft(label, n.X);
            Canvas.SetTop(label, n.Y);
            Canvas.SetZIndex(label, 11);
            DiagramCanvas.Children.Add(label);
            _labelVisuals[n.Id] = label;
        }
    }

    private static Polygon MakeDiamond(DiagramNode n)
    {
        var poly = new Polygon();
        SetDiamondPoints(poly, n.Width, n.Height);
        return poly;
    }

    private static void SetDiamondPoints(Polygon poly, double w, double h)
    {
        poly.Points = new PointCollection
        {
            new Point(w / 2, 0), new Point(w, h / 2), new Point(w / 2, h), new Point(0, h / 2),
        };
    }

    private static Border MakeTextShape(DiagramNode n)
    {
        // For a text node the "shape" is a transparent hit-testable border carrying the text itself.
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
            Child = new TextBlock
            {
                Text = n.Label,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = n.FontSize,
                Foreground = Brush(n.TextColor),
            },
        };
    }

    private void CreateEdgeVisual(DiagramEdge e)
    {
        var path = new ShapePath { Stroke = Brush(e.Stroke), StrokeThickness = e.StrokeWidth, Tag = e.Id };
        Canvas.SetZIndex(path, 5);
        DiagramCanvas.Children.Add(path);
        _edgeVisuals[e.Id] = path;

        var label = new Border
        {
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 1, 4, 1),
            Visibility = string.IsNullOrEmpty(e.Label) ? Visibility.Collapsed : Visibility.Visible,
            Child = new TextBlock { Text = e.Label, FontSize = e.FontSize, Foreground = Brush(e.Stroke) },
        };
        Canvas.SetZIndex(label, 6);
        DiagramCanvas.Children.Add(label);
        _edgeLabelVisuals[e.Id] = label;
    }

    private void RefreshAllEdges()
    {
        foreach (var e in _doc.Edges) RefreshEdge(e);
    }

    private void RefreshEdge(DiagramEdge e)
    {
        if (!_edgeVisuals.TryGetValue(e.Id, out var path)) return;
        var from = _doc.Nodes.FirstOrDefault(n => n.Id == e.FromId);
        var to = _doc.Nodes.FirstOrDefault(n => n.Id == e.ToId);
        if (from is null || to is null) { path.Data = null; return; }

        var c1 = new Point(from.X + from.Width / 2, from.Y + from.Height / 2);
        var c2 = new Point(to.X + to.Width / 2, to.Y + to.Height / 2);
        var start = EdgePoint(from, c2);
        var end = EdgePoint(to, c1);

        var geo = new GeometryGroup();
        geo.Children.Add(new LineGeometry { StartPoint = start, EndPoint = end });

        // Arrow head.
        double angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        double len = 12, spread = Math.PI / 7;
        var p1 = new Point(end.X - len * Math.Cos(angle - spread), end.Y - len * Math.Sin(angle - spread));
        var p2 = new Point(end.X - len * Math.Cos(angle + spread), end.Y - len * Math.Sin(angle + spread));
        var head = new PathGeometry();
        var fig = new PathFigure { StartPoint = p1, IsClosed = true };
        fig.Segments.Add(new LineSegment { Point = end });
        fig.Segments.Add(new LineSegment { Point = p2 });
        head.Figures.Add(fig);
        geo.Children.Add(head);
        path.Data = geo;
        path.Fill = Brush(e.Stroke);

        if (_edgeLabelVisuals.TryGetValue(e.Id, out var label))
        {
            var mid = new Point((start.X + end.X) / 2, (start.Y + end.Y) / 2);
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, mid.X - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, mid.Y - label.DesiredSize.Height / 2);
        }
    }

    /// <summary>Intersection of the box edge with the line toward <paramref name="toward"/>.</summary>
    private static Point EdgePoint(DiagramNode n, Point toward)
    {
        double cx = n.X + n.Width / 2, cy = n.Y + n.Height / 2;
        double dx = toward.X - cx, dy = toward.Y - cy;
        if (dx == 0 && dy == 0) return new Point(cx, cy);
        double hw = n.Width / 2, hh = n.Height / 2;
        double scale = 1.0 / Math.Max(Math.Abs(dx) / hw, Math.Abs(dy) / hh);
        return new Point(cx + dx * scale, cy + dy * scale);
    }

    // ── Adding shapes ──────────────────────────────────────────────────────────

    private Point ViewportCenterOnCanvas()
    {
        double x = (CanvasScroller.HorizontalOffset + CanvasScroller.ViewportWidth / 2) / _zoom;
        double y = (CanvasScroller.VerticalOffset + CanvasScroller.ViewportHeight / 2) / _zoom;
        return new Point(Math.Max(20, x - 70), Math.Max(20, y - 40));
    }

    private void AddNode(ShapeKind kind)
    {
        var p = ViewportCenterOnCanvas();
        var n = new DiagramNode { Kind = kind, X = Snap(p.X), Y = Snap(p.Y) };
        if (kind == ShapeKind.Text)
        {
            n.Label = P("Text", "文字");
            n.Fill = "#00000000";
            n.Stroke = "#00000000";
            n.StrokeWidth = 0;
            n.TextColor = "#FFFFFFFF";
            n.Width = 120; n.Height = 40;
        }
        else
        {
            n.Label = P("Node", "節點");
        }
        _doc.Nodes.Add(n);
        CreateNodeVisual(n);
        SelectOnly(n.Id);
        SetStatus(InfoBarSeverity.Success, P("Shape added.", "已加入形狀。"));
    }

    private void AddRect_Click(object s, RoutedEventArgs e) => AddNode(ShapeKind.Rectangle);
    private void AddRound_Click(object s, RoutedEventArgs e) => AddNode(ShapeKind.RoundedRectangle);
    private void AddEllipse_Click(object s, RoutedEventArgs e) => AddNode(ShapeKind.Ellipse);
    private void AddDiamond_Click(object s, RoutedEventArgs e) => AddNode(ShapeKind.Diamond);
    private void AddText_Click(object s, RoutedEventArgs e) => AddNode(ShapeKind.Text);

    // ── Snap ───────────────────────────────────────────────────────────────────

    private bool SnapOn => SnapToggle.IsOn;
    private double Snap(double v) => SnapOn ? Math.Round(v / GridSize) * GridSize : v;
    private void Snap_Toggled(object s, RoutedEventArgs e) { }

    // ── Selection ──────────────────────────────────────────────────────────────

    private void SelectOnly(string id)
    {
        _selected.Clear();
        _selected.Add(id);
        UpdateSelectionVisuals();
        UpdatePropsPanel();
    }

    private void ToggleSelect(string id)
    {
        if (!_selected.Add(id)) _selected.Remove(id);
        UpdateSelectionVisuals();
        UpdatePropsPanel();
    }

    private void ClearSelection()
    {
        _selected.Clear();
        UpdateSelectionVisuals();
        UpdatePropsPanel();
    }

    private void UpdateSelectionVisuals()
    {
        foreach (var h in _handles) DiagramCanvas.Children.Remove(h);
        _handles.Clear();

        foreach (var kv in _nodeVisuals)
        {
            bool sel = _selected.Contains(kv.Key);
            if (kv.Value is Shape sh && _doc.Nodes.First(n => n.Id == kv.Key).Kind != ShapeKind.Text)
            {
                // keep model stroke; selection drawn via handles + glow
            }
            kv.Value.Opacity = 1.0;
        }

        // Draw selection rectangles + resize handles for selected nodes.
        foreach (var id in _selected)
        {
            var n = _doc.Nodes.FirstOrDefault(x => x.Id == id);
            if (n is null) continue;

            var outline = new Rectangle
            {
                Width = n.Width + 6, Height = n.Height + 6,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)),
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 3, 2 },
                Fill = null, IsHitTestVisible = false,
            };
            Canvas.SetLeft(outline, n.X - 3);
            Canvas.SetTop(outline, n.Y - 3);
            Canvas.SetZIndex(outline, 50);
            DiagramCanvas.Children.Add(outline);
            _handles.Add(outline);

            // 4 corner handles (only when exactly one node selected, to keep resize unambiguous).
            if (_selected.Count == 1)
            {
                var corners = new (double x, double y, int idx)[]
                {
                    (n.X, n.Y, 0), (n.X + n.Width, n.Y, 1),
                    (n.X, n.Y + n.Height, 2), (n.X + n.Width, n.Y + n.Height, 3),
                };
                foreach (var c in corners)
                {
                    var handle = new Rectangle
                    {
                        Width = HandleSize, Height = HandleSize,
                        Fill = new SolidColorBrush(Colors.White),
                        Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)),
                        StrokeThickness = 1.5,
                        Tag = $"H:{id}:{c.idx}",
                    };
                    Canvas.SetLeft(handle, c.x - HandleSize / 2);
                    Canvas.SetTop(handle, c.y - HandleSize / 2);
                    Canvas.SetZIndex(handle, 51);
                    DiagramCanvas.Children.Add(handle);
                    _handles.Add(handle);
                }
            }
        }
    }

    // ── Hit testing ────────────────────────────────────────────────────────────

    private (string id, int handle)? HitHandle(Point p)
    {
        foreach (var h in _handles)
        {
            if (h.Tag is string tag && tag.StartsWith("H:"))
            {
                double left = Canvas.GetLeft(h), top = Canvas.GetTop(h);
                if (p.X >= left && p.X <= left + HandleSize && p.Y >= top && p.Y <= top + HandleSize)
                {
                    var parts = tag.Split(':');
                    return (parts[1], int.Parse(parts[2]));
                }
            }
        }
        return null;
    }

    private string? HitNode(Point p)
    {
        // Topmost first: iterate nodes in reverse document order.
        for (int i = _doc.Nodes.Count - 1; i >= 0; i--)
        {
            var n = _doc.Nodes[i];
            if (p.X >= n.X && p.X <= n.X + n.Width && p.Y >= n.Y && p.Y <= n.Y + n.Height)
                return n.Id;
        }
        return null;
    }

    // ── Pointer interaction ────────────────────────────────────────────────────

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var p = e.GetCurrentPoint(DiagramCanvas).Position;
        DiagramCanvas.CapturePointer(e.Pointer);

        if (_connectMode)
        {
            var hit = HitNode(p);
            if (hit is null) return;
            if (_connectFrom is null)
            {
                _connectFrom = hit;
                SelectOnly(hit);
                SetStatus(InfoBarSeverity.Informational, P("Now click the target shape.", "而家撳目標形狀。"));
            }
            else if (hit != _connectFrom)
            {
                AddEdge(_connectFrom, hit);
                _connectFrom = null;
                SetStatus(InfoBarSeverity.Success, P("Connected.", "已連線。"));
            }
            return;
        }

        // Resize handle?
        var hh = HitHandle(p);
        if (hh is { } rh)
        {
            _drag = DragKind.Resize;
            _resizeNodeId = rh.id;
            _resizeHandle = rh.handle;
            _dragStart = p;
            return;
        }

        var node = HitNode(p);
        if (node is not null)
        {
            bool ctrl = (e.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0;
            if (ctrl) ToggleSelect(node);
            else if (!_selected.Contains(node)) SelectOnly(node);

            _drag = DragKind.Move;
            _dragStart = p;
            _dragOrigin = _selected.ToDictionary(id => id, id =>
            {
                var nn = _doc.Nodes.First(x => x.Id == id);
                return new Rect(nn.X, nn.Y, nn.Width, nn.Height);
            });
        }
        else
        {
            // Marquee select on empty canvas.
            ClearSelection();
            _drag = DragKind.Marquee;
            _dragStart = p;
            _marquee = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromArgb(255, 0, 120, 215)),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)),
                IsHitTestVisible = false,
            };
            Canvas.SetZIndex(_marquee, 60);
            Canvas.SetLeft(_marquee, p.X);
            Canvas.SetTop(_marquee, p.Y);
            DiagramCanvas.Children.Add(_marquee);
        }
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_drag == DragKind.None) return;
        var p = e.GetCurrentPoint(DiagramCanvas).Position;
        double dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;

        if (_drag == DragKind.Move)
        {
            foreach (var id in _selected)
            {
                if (!_dragOrigin.TryGetValue(id, out var orig)) continue;
                var n = _doc.Nodes.First(x => x.Id == id);
                n.X = Snap(orig.X + dx);
                n.Y = Snap(orig.Y + dy);
                ApplyNodeBounds(n);
            }
            RefreshEdgesFor(_selected);
            UpdateSelectionVisuals();
        }
        else if (_drag == DragKind.Resize && _resizeNodeId is not null)
        {
            var n = _doc.Nodes.First(x => x.Id == _resizeNodeId);
            ResizeNode(n, p);
            ApplyNodeBounds(n);
            RefreshEdgesFor(new[] { n.Id });
            UpdateSelectionVisuals();
        }
        else if (_drag == DragKind.Marquee && _marquee is not null)
        {
            double x = Math.Min(p.X, _dragStart.X), y = Math.Min(p.Y, _dragStart.Y);
            Canvas.SetLeft(_marquee, x);
            Canvas.SetTop(_marquee, y);
            _marquee.Width = Math.Abs(dx);
            _marquee.Height = Math.Abs(dy);
        }
    }

    private void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        DiagramCanvas.ReleasePointerCapture(e.Pointer);
        if (_drag == DragKind.Marquee && _marquee is not null)
        {
            var rect = new Rect(Canvas.GetLeft(_marquee), Canvas.GetTop(_marquee), _marquee.Width, _marquee.Height);
            DiagramCanvas.Children.Remove(_marquee);
            _marquee = null;
            _selected.Clear();
            foreach (var n in _doc.Nodes)
            {
                var nr = new Rect(n.X, n.Y, n.Width, n.Height);
                if (RectsIntersect(rect, nr)) _selected.Add(n.Id);
            }
            UpdateSelectionVisuals();
            UpdatePropsPanel();
        }
        else if (_drag == DragKind.Move || _drag == DragKind.Resize)
        {
            UpdatePropsPanel();
        }
        _drag = DragKind.None;
        _resizeNodeId = null;
    }

    private static bool RectsIntersect(Rect a, Rect b)
        => a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;

    private void ResizeNode(DiagramNode n, Point p)
    {
        double right = n.X + n.Width, bottom = n.Y + n.Height;
        double nx = n.X, ny = n.Y, nr = right, nb = bottom;
        switch (_resizeHandle)
        {
            case 0: nx = Snap(p.X); ny = Snap(p.Y); break;            // top-left
            case 1: nr = Snap(p.X); ny = Snap(p.Y); break;            // top-right
            case 2: nx = Snap(p.X); nb = Snap(p.Y); break;            // bottom-left
            case 3: nr = Snap(p.X); nb = Snap(p.Y); break;            // bottom-right
        }
        n.X = Math.Min(nx, nr);
        n.Y = Math.Min(ny, nb);
        n.Width = Math.Max(24, Math.Abs(nr - nx));
        n.Height = Math.Max(24, Math.Abs(nb - ny));
    }

    /// <summary>Push the model's bounds/colours back into the live visuals for one node.</summary>
    private void ApplyNodeBounds(DiagramNode n)
    {
        if (!_nodeVisuals.TryGetValue(n.Id, out var v)) return;
        v.Width = n.Width;
        v.Height = n.Height;
        Canvas.SetLeft(v, n.X);
        Canvas.SetTop(v, n.Y);
        if (v is Polygon poly) SetDiamondPoints(poly, n.Width, n.Height);

        if (_labelVisuals.TryGetValue(n.Id, out var lbl))
        {
            lbl.Width = n.Width;
            lbl.Height = n.Height;
            Canvas.SetLeft(lbl, n.X);
            Canvas.SetTop(lbl, n.Y);
        }
    }

    private void ApplyNodeStyle(DiagramNode n)
    {
        if (!_nodeVisuals.TryGetValue(n.Id, out var v)) return;
        if (v is Shape sh && n.Kind != ShapeKind.Text)
        {
            sh.Fill = Brush(n.Fill);
            sh.Stroke = Brush(n.Stroke);
            sh.StrokeThickness = n.StrokeWidth;
        }
        TextBlock? tb = v is Border tbb ? tbb.Child as TextBlock
            : _labelVisuals.TryGetValue(n.Id, out var lbl) ? lbl.Child as TextBlock : null;
        if (tb is not null)
        {
            tb.Text = n.Label;
            tb.FontSize = n.FontSize;
            tb.Foreground = Brush(n.TextColor);
        }
    }

    private void RefreshEdgesFor(IEnumerable<string> nodeIds)
    {
        var set = nodeIds.ToHashSet();
        foreach (var e in _doc.Edges)
            if (set.Contains(e.FromId) || set.Contains(e.ToId)) RefreshEdge(e);
    }

    // ── Double-click to edit label ─────────────────────────────────────────────

    protected override void OnDoubleTapped(DoubleTappedRoutedEventArgs e)
    {
        base.OnDoubleTapped(e);
        var p = e.GetPosition(DiagramCanvas);
        var id = HitNode(p);
        if (id is not null) { _ = EditLabelAsync(id, isEdge: false); return; }
        var edgeId = HitEdge(p);
        if (edgeId is not null) _ = EditLabelAsync(edgeId, isEdge: true);
    }

    private string? HitEdge(Point p)
    {
        const double tol = 8;
        foreach (var e in _doc.Edges)
        {
            var from = _doc.Nodes.FirstOrDefault(n => n.Id == e.FromId);
            var to = _doc.Nodes.FirstOrDefault(n => n.Id == e.ToId);
            if (from is null || to is null) continue;
            var a = EdgePoint(from, new Point(to.X + to.Width / 2, to.Y + to.Height / 2));
            var b = EdgePoint(to, new Point(from.X + from.Width / 2, from.Y + from.Height / 2));
            if (DistToSegment(p, a, b) <= tol) return e.Id;
        }
        return null;
    }

    private static double DistToSegment(Point p, Point a, Point b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = dx * dx + dy * dy;
        if (len2 == 0) return Math.Sqrt((p.X - a.X) * (p.X - a.X) + (p.Y - a.Y) * (p.Y - a.Y));
        double t = Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
        double px = a.X + t * dx, py = a.Y + t * dy;
        return Math.Sqrt((p.X - px) * (p.X - px) + (p.Y - py) * (p.Y - py));
    }

    private async Task EditLabelAsync(string id, bool isEdge)
    {
        string current = isEdge
            ? _doc.Edges.First(x => x.Id == id).Label
            : _doc.Nodes.First(x => x.Id == id).Label;
        var box = new TextBox { Text = current, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinWidth = 320, MinHeight = 80 };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Edit label · 編輯文字", "編輯文字"),
            Content = box,
            PrimaryButtonText = P("OK · 確定", "確定"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Primary,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (isEdge)
        {
            var ed = _doc.Edges.First(x => x.Id == id);
            ed.Label = box.Text;
            if (_edgeLabelVisuals.TryGetValue(id, out var lv))
            {
                lv.Visibility = string.IsNullOrEmpty(ed.Label) ? Visibility.Collapsed : Visibility.Visible;
                if (lv.Child is TextBlock tb) tb.Text = ed.Label;
                RefreshEdge(ed);
            }
        }
        else
        {
            var n = _doc.Nodes.First(x => x.Id == id);
            n.Label = box.Text;
            ApplyNodeStyle(n);
            SelectOnly(id);
        }
    }

    // ── Edges ──────────────────────────────────────────────────────────────────

    private void AddEdge(string fromId, string toId)
    {
        if (_doc.Edges.Any(x => x.FromId == fromId && x.ToId == toId)) return;
        var e = new DiagramEdge { FromId = fromId, ToId = toId };
        _doc.Edges.Add(e);
        CreateEdgeVisual(e);
        RefreshEdge(e);
    }

    private void ConnectMode_Click(object s, RoutedEventArgs e)
    {
        _connectMode = ConnectModeBtn.IsChecked == true;
        _connectFrom = null;
        SetStatus(InfoBarSeverity.Informational, _connectMode
            ? P("Connect mode: click a source shape, then a target shape.", "連線模式：撳起點形狀，再撳目標形狀。")
            : P("Connect mode off.", "已關閉連線模式。"));
    }

    // ── Properties panel ───────────────────────────────────────────────────────

    private DiagramNode? OnlyNode =>
        _selected.Count == 1 && _doc.Nodes.FirstOrDefault(n => n.Id == _selected.First()) is { } n ? n : null;

    private void UpdatePropsPanel()
    {
        var n = OnlyNode;
        bool show = n is not null;
        PropsPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        PropsEmpty.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        if (n is null)
        {
            SelectionInfo.Text = _selected.Count > 1 ? P($"{_selected.Count} items selected", $"已選 {_selected.Count} 個") : "";
            PropsEmpty.Text = _selected.Count > 1
                ? P($"{_selected.Count} items selected. Use the toolbar to duplicate, delete or reorder.", $"已選 {_selected.Count} 個。用工具列複製、刪除或調層次。")
                : P("Select a shape or arrow to edit its style.", "揀一個形狀或箭咀去編輯佢嘅樣式。");
            return;
        }

        _suppressProps = true;
        LabelBox.Text = n.Label;
        bool isText = n.Kind == ShapeKind.Text;
        FillRow.Visibility = isText ? Visibility.Collapsed : Visibility.Visible;
        StrokeWidthBox.Visibility = isText ? Visibility.Collapsed : Visibility.Visible;
        FillSwatch.Background = Brush(n.Fill);
        FillHex.Text = n.Fill;
        FillPicker.Color = ParseColor(n.Fill);
        StrokeSwatch.Background = Brush(n.Stroke);
        StrokeHex.Text = n.Stroke;
        StrokePicker.Color = ParseColor(n.Stroke);
        StrokeWidthBox.Value = n.StrokeWidth;
        FontSizeBox.Value = n.FontSize;
        TextColorSwatch.Background = Brush(n.TextColor);
        TextColorHex.Text = n.TextColor;
        TextColorPicker.Color = ParseColor(n.TextColor);
        SelectionInfo.Text = P($"{n.Kind}", $"{n.Kind}");
        _suppressProps = false;
    }

    private void Label_Changed(object s, TextChangedEventArgs e)
    {
        if (_suppressProps || OnlyNode is not { } n) return;
        n.Label = LabelBox.Text;
        ApplyNodeStyle(n);
    }

    private void FillColor_Changed(ColorPicker s, ColorChangedEventArgs e)
    {
        if (_suppressProps || OnlyNode is not { } n) return;
        n.Fill = ToHex(e.NewColor);
        FillSwatch.Background = Brush(n.Fill);
        FillHex.Text = n.Fill;
        ApplyNodeStyle(n);
    }

    private void StrokeColor_Changed(ColorPicker s, ColorChangedEventArgs e)
    {
        if (_suppressProps || OnlyNode is not { } n) return;
        n.Stroke = ToHex(e.NewColor);
        StrokeSwatch.Background = Brush(n.Stroke);
        StrokeHex.Text = n.Stroke;
        ApplyNodeStyle(n);
    }

    private void TextColor_Changed(ColorPicker s, ColorChangedEventArgs e)
    {
        if (_suppressProps || OnlyNode is not { } n) return;
        n.TextColor = ToHex(e.NewColor);
        TextColorSwatch.Background = Brush(n.TextColor);
        TextColorHex.Text = n.TextColor;
        ApplyNodeStyle(n);
    }

    private void StrokeWidth_Changed(NumberBox s, NumberBoxValueChangedEventArgs e)
    {
        if (_suppressProps || OnlyNode is not { } n || double.IsNaN(e.NewValue)) return;
        n.StrokeWidth = e.NewValue;
        ApplyNodeStyle(n);
    }

    private void FontSize_Changed(NumberBox s, NumberBoxValueChangedEventArgs e)
    {
        if (_suppressProps || OnlyNode is not { } n || double.IsNaN(e.NewValue)) return;
        n.FontSize = Math.Max(6, e.NewValue);
        ApplyNodeStyle(n);
    }

    // ── Edit toolbar: duplicate / delete / z-order ─────────────────────────────

    private void Duplicate_Click(object s, RoutedEventArgs e)
    {
        if (_selected.Count == 0) return;
        var clones = new List<string>();
        foreach (var id in _selected.ToList())
        {
            var n = _doc.Nodes.FirstOrDefault(x => x.Id == id);
            if (n is null) continue;
            var copy = new DiagramNode
            {
                Kind = n.Kind, X = n.X + 24, Y = n.Y + 24, Width = n.Width, Height = n.Height,
                Label = n.Label, Fill = n.Fill, Stroke = n.Stroke, StrokeWidth = n.StrokeWidth,
                FontSize = n.FontSize, TextColor = n.TextColor,
            };
            _doc.Nodes.Add(copy);
            CreateNodeVisual(copy);
            clones.Add(copy.Id);
        }
        _selected.Clear();
        foreach (var c in clones) _selected.Add(c);
        UpdateSelectionVisuals();
        UpdatePropsPanel();
        SetStatus(InfoBarSeverity.Success, P("Duplicated.", "已複製。"));
    }

    private void Delete_Click(object s, RoutedEventArgs e)
    {
        if (_selected.Count == 0) return;
        foreach (var id in _selected.ToList())
        {
            _doc.Nodes.RemoveAll(n => n.Id == id);
            _doc.Edges.RemoveAll(x => x.FromId == id || x.ToId == id);
        }
        _selected.Clear();
        RebuildAll();
        UpdatePropsPanel();
        SetStatus(InfoBarSeverity.Success, P("Deleted.", "已刪除。"));
    }

    private void BringFront_Click(object s, RoutedEventArgs e)
    {
        foreach (var id in _selected.ToList())
        {
            var n = _doc.Nodes.FirstOrDefault(x => x.Id == id);
            if (n is null) continue;
            _doc.Nodes.Remove(n);
            _doc.Nodes.Add(n);
        }
        RebuildAll();
    }

    private void SendBack_Click(object s, RoutedEventArgs e)
    {
        foreach (var id in _selected.ToList().AsEnumerable().Reverse())
        {
            var n = _doc.Nodes.FirstOrDefault(x => x.Id == id);
            if (n is null) continue;
            _doc.Nodes.Remove(n);
            _doc.Nodes.Insert(0, n);
        }
        RebuildAll();
    }

    // ── Zoom ───────────────────────────────────────────────────────────────────

    private void SetZoom(double z)
    {
        _zoom = Math.Clamp(z, 0.25, 4.0);
        CanvasScale.ScaleX = _zoom;
        CanvasScale.ScaleY = _zoom;
        UpdateZoomText();
    }

    private void UpdateZoomText() { if (ZoomText is not null) ZoomText.Text = $"{_zoom * 100:0}%"; }
    private void ZoomIn_Click(object s, RoutedEventArgs e) => SetZoom(_zoom + 0.1);
    private void ZoomOut_Click(object s, RoutedEventArgs e) => SetZoom(_zoom - 0.1);
    private void ZoomReset_Click(object s, RoutedEventArgs e) => SetZoom(1.0);

    // ── File: new / open / save / export ───────────────────────────────────────

    private void New_Click(object s, RoutedEventArgs e)
    {
        _doc = new DiagramDocument();
        _currentPath = null;
        _selected.Clear();
        RebuildAll();
        UpdatePropsPanel();
        SetStatus(InfoBarSeverity.Informational, P("New diagram.", "新圖表。"));
    }

    private async void Open_Click(object s, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(
            new[] { new FileDialogs.Filter("WinForge diagram", "*" + FileExtension), new FileDialogs.Filter("JSON", "*.json"), new FileDialogs.Filter("All files", "*.*") },
            P("Open diagram", "開啟圖表"));
        if (path is null) return;
        var doc = await LoadAsync(path);
        if (doc is null) { SetStatus(InfoBarSeverity.Error, P("Could not read that file.", "讀唔到嗰個檔案。")); return; }
        _doc = doc;
        _currentPath = path;
        _selected.Clear();
        RebuildAll();
        UpdatePropsPanel();
        SetStatus(InfoBarSeverity.Success, P($"Opened {IoPath.GetFileName(path)}.", $"已開啟 {IoPath.GetFileName(path)}。"));
    }

    private async void Save_Click(object s, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync(
            _currentPath is not null ? IoPath.GetFileName(_currentPath) : "diagram" + FileExtension,
            new[] { new FileDialogs.Filter("WinForge diagram", "*" + FileExtension), new FileDialogs.Filter("JSON", "*.json") },
            FileExtension.TrimStart('.'),
            P("Save diagram", "儲存圖表"));
        if (path is null) return;
        try
        {
            await SaveAsync(path, _doc);
            _currentPath = path;
            SetStatus(InfoBarSeverity.Success, P($"Saved {IoPath.GetFileName(path)}.", $"已儲存 {IoPath.GetFileName(path)}。"));
        }
        catch (Exception ex) { SetStatus(InfoBarSeverity.Error, ex.Message); }
    }

    private async void Export_Click(object s, RoutedEventArgs e)
    {
        var path = await FileDialogs.SaveFileAsync("diagram.png",
            new[] { new FileDialogs.Filter("PNG image", "*.png") }, "png", P("Export PNG", "匯出 PNG"));
        if (path is null) return;
        try
        {
            // Render the canvas (de-selected, at 1:1) into a bitmap.
            var savedSel = _selected.ToList();
            ClearSelection();
            var oldScaleX = CanvasScale.ScaleX; var oldScaleY = CanvasScale.ScaleY;
            CanvasScale.ScaleX = 1; CanvasScale.ScaleY = 1;
            DiagramCanvas.UpdateLayout();

            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(DiagramCanvas, (int)_doc.CanvasWidth, (int)_doc.CanvasHeight);
            var pixels = await rtb.GetPixelsAsync();

            CanvasScale.ScaleX = oldScaleX; CanvasScale.ScaleY = oldScaleY;
            foreach (var id in savedSel) _selected.Add(id);
            UpdateSelectionVisuals();

            var folder = await StorageFolder.GetFolderFromPathAsync(IoPath.GetDirectoryName(path)!);
            var file = await folder.CreateFileAsync(IoPath.GetFileName(path), CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)rtb.PixelWidth, (uint)rtb.PixelHeight, 96, 96, pixels.ToArray());
            await enc.FlushAsync();
            SetStatus(InfoBarSeverity.Success, P($"Exported {IoPath.GetFileName(path)}.", $"已匯出 {IoPath.GetFileName(path)}。"));
        }
        catch (Exception ex) { SetStatus(InfoBarSeverity.Error, ex.Message); }
    }

    private void SetStatus(InfoBarSeverity sev, string msg)
    {
        StatusBar.Severity = sev;
        StatusBar.Message = msg;
        StatusBar.IsOpen = true;
    }
}
