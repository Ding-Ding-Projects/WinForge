using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WinForge.Models;
using ISImage = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Bgra32>;
using ISColor = SixLabors.ImageSharp.Color;
using ISPointF = SixLabors.ImageSharp.PointF;

namespace WinForge.Services;

/// <summary>
/// 點陣圖影像編輯器嘅文件模型同處理引擎（原生 WinForge，唔 shell／唔捆綁 Paint.NET、GIMP 或任何外部工具）·
/// The raster Image Editor's document model and processing engine — original WinForge code that launches
/// NO external tool (NOT Paint.NET / GIMP). All decode / encode / adjustment / filter / transform / paint
/// ops run in-process on managed SixLabors.ImageSharp <see cref="Bgra32"/> buffers.
///
/// 文件 = 一疊圖層（每層一張同尺寸 ISImage + 不透明度 + 顯示），合成後畀 UI 用 WriteableBitmap 顯示。
/// A document is a stack of layers (each a same-size ISImage with opacity + visibility), composited for the
/// UI to show via a WriteableBitmap. Undo/redo stores compact full-document snapshots (cap'd depth).
/// </summary>
public sealed class ImageEditorService : IDisposable
{
    /// <summary>一個圖層 · One layer: a same-size BGRA image plus opacity / visibility / name.</summary>
    public sealed class Layer
    {
        public string Name { get; set; }
        public bool Visible { get; set; } = true;
        public double Opacity { get; set; } = 1.0; // 0..1
        public ISImage Image { get; set; }
        public Layer(string name, ISImage image) { Name = name; Image = image; }
        public Layer Clone() => new(Name, Image.Clone()) { Visible = Visible, Opacity = Opacity };
    }

    public int Width { get; private set; }
    public int Height { get; private set; }
    public List<Layer> Layers { get; } = new();
    public int ActiveLayerIndex { get; private set; }
    public Layer ActiveLayer => Layers[Math.Clamp(ActiveLayerIndex, 0, Layers.Count - 1)];

    public string? FilePath { get; set; }
    public bool Dirty { get; private set; }

    // Undo/redo of whole-document snapshots (filters/transforms/paint all funnel through here).
    private readonly List<Snapshot> _undo = new();
    private readonly List<Snapshot> _redo = new();
    private const int MaxUndo = 24;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public ImageEditorService(int w = 800, int h = 600)
    {
        NewDocument(w, h, fillWhite: true);
    }

    // ===================== Document lifecycle =====================

    public void NewDocument(int w, int h, bool fillWhite)
    {
        DisposeLayers();
        Width = Math.Clamp(w, 1, 16384);
        Height = Math.Clamp(h, 1, 16384);
        var img = new ISImage(Width, Height);
        if (fillWhite) img.Mutate(c => c.BackgroundColor(ISColor.White));
        Layers.Clear();
        Layers.Add(new Layer(Loc.I.Pick("Background · 背景", "背景"), img));
        ActiveLayerIndex = 0;
        FilePath = null;
        Dirty = false;
        _undo.Clear();
        _redo.Clear();
    }

    /// <summary>由檔案開啟（單一圖層）· Open an image file as a single-layer document.</summary>
    public async Task<TweakResult> OpenAsync(string path)
    {
        try
        {
            var img = await SixLabors.ImageSharp.Image.LoadAsync<Bgra32>(path);
            DisposeLayers();
            Width = img.Width;
            Height = img.Height;
            Layers.Clear();
            Layers.Add(new Layer(Path.GetFileNameWithoutExtension(path), img));
            ActiveLayerIndex = 0;
            FilePath = path;
            Dirty = false;
            _undo.Clear();
            _redo.Clear();
            return TweakResult.Ok($"Opened {Width}×{Height}: {Path.GetFileName(path)}", $"已開啟 {Width}×{Height}：{Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Open failed: {ex.Message}", $"開啟失敗：{ex.Message}");
        }
    }

    // ===================== Snapshots / undo =====================

    private sealed class Snapshot
    {
        public int Width, Height, ActiveLayerIndex;
        public required List<(string Name, bool Visible, double Opacity, ISImage Image)> Layers;
    }

    /// <summary>影改之前影低快照 · Capture a snapshot BEFORE a mutating op so it can be undone.</summary>
    public void PushUndo()
    {
        var snap = new Snapshot
        {
            Width = Width,
            Height = Height,
            ActiveLayerIndex = ActiveLayerIndex,
            Layers = Layers.Select(l => (l.Name, l.Visible, l.Opacity, l.Image.Clone())).ToList(),
        };
        _undo.Add(snap);
        while (_undo.Count > MaxUndo)
        {
            foreach (var l in _undo[0].Layers) l.Image.Dispose();
            _undo.RemoveAt(0);
        }
        // Clear redo branch.
        foreach (var s in _redo) foreach (var l in s.Layers) l.Image.Dispose();
        _redo.Clear();
        Dirty = true;
    }

    private Snapshot CaptureCurrent() => new()
    {
        Width = Width,
        Height = Height,
        ActiveLayerIndex = ActiveLayerIndex,
        Layers = Layers.Select(l => (l.Name, l.Visible, l.Opacity, l.Image.Clone())).ToList(),
    };

    private void Restore(Snapshot s)
    {
        DisposeLayers();
        Width = s.Width;
        Height = s.Height;
        Layers.Clear();
        foreach (var l in s.Layers)
            Layers.Add(new Layer(l.Name, l.Image) { Visible = l.Visible, Opacity = l.Opacity });
        ActiveLayerIndex = Math.Clamp(s.ActiveLayerIndex, 0, Layers.Count - 1);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        _redo.Add(CaptureCurrent());
        var s = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Restore(s); // takes ownership of snapshot's cloned images
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        _undo.Add(CaptureCurrent());
        var s = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        Restore(s);
    }

    // ===================== Layers =====================

    public void SetActiveLayer(int i) => ActiveLayerIndex = Math.Clamp(i, 0, Layers.Count - 1);

    public void AddLayer()
    {
        PushUndo();
        var img = new ISImage(Width, Height); // transparent
        Layers.Add(new Layer($"{Loc.I.Pick("Layer", "圖層")} {Layers.Count + 1}", img));
        ActiveLayerIndex = Layers.Count - 1;
    }

    public void DeleteLayer()
    {
        if (Layers.Count <= 1) return;
        PushUndo();
        Layers[ActiveLayerIndex].Image.Dispose();
        Layers.RemoveAt(ActiveLayerIndex);
        ActiveLayerIndex = Math.Clamp(ActiveLayerIndex, 0, Layers.Count - 1);
    }

    public void MoveLayer(int delta)
    {
        int to = ActiveLayerIndex + delta;
        if (to < 0 || to >= Layers.Count) return;
        PushUndo();
        var l = Layers[ActiveLayerIndex];
        Layers.RemoveAt(ActiveLayerIndex);
        Layers.Insert(to, l);
        ActiveLayerIndex = to;
    }

    public void FlattenAll()
    {
        if (Layers.Count <= 1) return;
        PushUndo();
        var flat = CompositeImage();
        DisposeLayersKeepList();
        Layers.Clear();
        Layers.Add(new Layer(Loc.I.Pick("Flattened · 已合併", "已合併"), flat));
        ActiveLayerIndex = 0;
    }

    // ===================== Compositing =====================

    /// <summary>合成所有可見圖層成一張新 ISImage · Composite all visible layers into a new image.</summary>
    public ISImage CompositeImage()
    {
        var outImg = new ISImage(Width, Height); // transparent base
        foreach (var l in Layers)
        {
            if (!l.Visible || l.Opacity <= 0) continue;
            float op = (float)Math.Clamp(l.Opacity, 0, 1);
            outImg.Mutate(c => c.DrawImage(l.Image, op));
        }
        return outImg;
    }

    /// <summary>合成後輸出 BGRA bytes（畀 WriteableBitmap 用）· Composite to a flat BGRA byte buffer.</summary>
    public byte[] CompositeToBgra()
    {
        using var flat = CompositeImage();
        var bytes = new byte[Width * Height * 4];
        flat.CopyPixelDataTo(bytes);
        return bytes;
    }

    /// <summary>單一圖層輸出 BGRA（圖層縮圖用）· One layer's BGRA bytes (for layer thumbnails).</summary>
    public byte[] LayerToBgra(Layer l, out int w, out int h)
    {
        w = l.Image.Width; h = l.Image.Height;
        var bytes = new byte[w * h * 4];
        l.Image.CopyPixelDataTo(bytes);
        return bytes;
    }

    // ===================== Adjustments (live preview friendly) =====================

    /// <summary>調整參數（中性 = 全 0 / gamma 1）· Live adjustment parameters; neutral = zeros, gamma 1.</summary>
    public struct Adjustments
    {
        public float Brightness; // -1..+1
        public float Contrast;   // -1..+1
        public float Saturation; // -1..+1
        public float Hue;        // -180..+180 degrees
        public float Gamma;      // 0.1..3 (1 = neutral)
        public static Adjustments Neutral => new() { Gamma = 1f };
        public readonly bool IsNeutral =>
            Brightness == 0 && Contrast == 0 && Saturation == 0 && Hue == 0 && Math.Abs(Gamma - 1f) < 0.001f;
    }

    /// <summary>將調整套用喺一張圖嘅副本上（畀預覽用，唔改原圖）· Apply adjustments to a CLONE (for preview).</summary>
    public static ISImage ApplyAdjustmentsPreview(ISImage src, Adjustments a)
    {
        var img = src.Clone();
        ApplyAdjustmentsInPlace(img, a);
        return img;
    }

    private static void ApplyAdjustmentsInPlace(ISImage img, Adjustments a)
    {
        img.Mutate(ctx =>
        {
            if (a.Brightness != 0) ctx.Brightness(1f + a.Brightness);
            if (a.Contrast != 0) ctx.Contrast(1f + a.Contrast);
            if (a.Saturation != 0) ctx.Saturate(1f + a.Saturation);
            if (a.Hue != 0) ctx.Hue(a.Hue);
        });
        // Gamma has no built-in IImageProcessingContext extension in ImageSharp 4 — apply it
        // directly per channel with a 256-entry lookup table (out = in^(1/gamma)).
        if (Math.Abs(a.Gamma - 1f) > 0.001f)
            ApplyGamma(img, Math.Clamp(a.Gamma, 0.1f, 3f));
    }

    private static void ApplyGamma(ISImage img, float gamma)
    {
        float inv = 1f / gamma;
        var lut = new byte[256];
        for (int i = 0; i < 256; i++)
            lut[i] = (byte)Math.Clamp((int)Math.Round(Math.Pow(i / 255.0, inv) * 255.0), 0, 255);
        img.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    row[x] = new Bgra32(lut[p.R], lut[p.G], lut[p.B], p.A);
                }
            }
        });
    }

    /// <summary>把調整燒進目前合成（合併圖層後）· Bake adjustments into the active layer.</summary>
    public void CommitAdjustments(Adjustments a)
    {
        if (a.IsNeutral) return;
        PushUndo();
        ApplyAdjustmentsInPlace(ActiveLayer.Image, a);
    }

    // ===================== Filters (apply to active layer) =====================

    public enum FilterKind { Grayscale, Invert, Sepia, GaussianBlur, Sharpen, EdgeDetect }

    public void ApplyFilter(FilterKind kind, float amount = 1f)
    {
        PushUndo();
        var img = ActiveLayer.Image;
        img.Mutate(ctx =>
        {
            switch (kind)
            {
                case FilterKind.Grayscale: ctx.Grayscale(); break;
                case FilterKind.Invert: ctx.Invert(); break;
                case FilterKind.Sepia: ctx.Sepia(); break;
                case FilterKind.GaussianBlur: ctx.GaussianBlur(Math.Max(0.3f, amount)); break;
                case FilterKind.Sharpen: ctx.GaussianSharpen(Math.Max(0.3f, amount)); break;
                case FilterKind.EdgeDetect: ctx.DetectEdges(); break;
            }
        });
    }

    // ===================== Transforms =====================

    /// <summary>縮放整個文件（所有圖層）· Resize the whole document (every layer).</summary>
    public void Resize(int newW, int newH)
    {
        newW = Math.Clamp(newW, 1, 16384);
        newH = Math.Clamp(newH, 1, 16384);
        if (newW == Width && newH == Height) return;
        PushUndo();
        foreach (var l in Layers)
            l.Image.Mutate(c => c.Resize(newW, newH));
        Width = newW;
        Height = newH;
    }

    /// <summary>裁切（像素矩形，所有圖層）· Crop every layer to a pixel rectangle.</summary>
    public void Crop(int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Width - 1);
        y = Math.Clamp(y, 0, Height - 1);
        w = Math.Clamp(w, 1, Width - x);
        h = Math.Clamp(h, 1, Height - y);
        if (x == 0 && y == 0 && w == Width && h == Height) return;
        PushUndo();
        var rect = new SixLabors.ImageSharp.Rectangle(x, y, w, h);
        foreach (var l in Layers)
            l.Image.Mutate(c => c.Crop(rect));
        Width = w;
        Height = h;
    }

    public void Rotate90(bool clockwise)
    {
        PushUndo();
        var mode = clockwise ? RotateMode.Rotate90 : RotateMode.Rotate270;
        foreach (var l in Layers)
            l.Image.Mutate(c => c.Rotate(mode));
        (Width, Height) = (Height, Width);
    }

    public void Flip(bool horizontal)
    {
        PushUndo();
        var mode = horizontal ? FlipMode.Horizontal : FlipMode.Vertical;
        foreach (var l in Layers)
            l.Image.Mutate(c => c.Flip(mode));
    }

    /// <summary>任意角度旋轉（會放大畫布以容納）· Arbitrary-angle rotate (canvas grows to fit).</summary>
    public void RotateArbitrary(float degrees)
    {
        if (Math.Abs(degrees % 360) < 0.01f) return;
        PushUndo();
        foreach (var l in Layers)
            l.Image.Mutate(c => c.Rotate(degrees));
        // All layers grow identically; sync canvas size to layer 0.
        Width = Layers[0].Image.Width;
        Height = Layers[0].Image.Height;
        // Ensure every layer is the same size (Rotate is deterministic, but guard).
        foreach (var l in Layers)
            if (l.Image.Width != Width || l.Image.Height != Height)
                l.Image.Mutate(c => c.Resize(Width, Height));
    }

    // ===================== Paint ops (active layer, direct pixel access) =====================

    /// <summary>喺目前圖層畫一筆圓形筆刷線（含中間補間）· Stamp a round brush line on the active layer.</summary>
    public void BrushLine(int x0, int y0, int x1, int y1, int radius, uint bgra, bool erase)
    {
        var img = ActiveLayer.Image;
        int dx = Math.Abs(x1 - x0), dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;
        while (true)
        {
            Stamp(img, x0, y0, radius, bgra, erase);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy) { err -= dy; x0 += sx; }
            if (e2 < dx) { err += dx; y0 += sy; }
        }
    }

    private void Stamp(ISImage img, int cx, int cy, int radius, uint bgra, bool erase)
    {
        var px = new Bgra32((byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF), (byte)((bgra >> 24) & 0xFF));
        int r2 = radius * radius;
        img.ProcessPixelRows(accessor =>
        {
            for (int oy = -radius; oy <= radius; oy++)
            {
                int y = cy + oy;
                if (y < 0 || y >= img.Height) continue;
                var row = accessor.GetRowSpan(y);
                for (int ox = -radius; ox <= radius; ox++)
                {
                    if (ox * ox + oy * oy > r2) continue;
                    int x = cx + ox;
                    if (x < 0 || x >= img.Width) continue;
                    if (erase) row[x] = new Bgra32(0, 0, 0, 0);
                    else row[x] = px;
                }
            }
        });
    }

    /// <summary>油桶填色（4 連通，目前圖層）· Flood fill (4-connected) on the active layer.</summary>
    public void FloodFill(int x, int y, uint bgra, int tolerance = 24)
    {
        var img = ActiveLayer.Image;
        if (x < 0 || y < 0 || x >= img.Width || y >= img.Height) return;
        var fill = new Bgra32((byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF), (byte)((bgra >> 24) & 0xFF));
        img.ProcessPixelRows(accessor =>
        {
            Bgra32 target = accessor.GetRowSpan(y)[x];
            if (PixelsEqual(target, fill, 0)) return;
            int w = img.Width, h = img.Height;
            var visited = new bool[w * h];
            var stack = new Stack<(int x, int y)>();
            stack.Push((x, y));
            while (stack.Count > 0)
            {
                var (cx, cy) = stack.Pop();
                if (cx < 0 || cy < 0 || cx >= w || cy >= h) continue;
                int idx = cy * w + cx;
                if (visited[idx]) continue;
                var row = accessor.GetRowSpan(cy);
                if (!PixelsEqual(row[cx], target, tolerance)) continue;
                visited[idx] = true;
                row[cx] = fill;
                stack.Push((cx + 1, cy));
                stack.Push((cx - 1, cy));
                stack.Push((cx, cy + 1));
                stack.Push((cx, cy - 1));
            }
        });
    }

    private static bool PixelsEqual(Bgra32 a, Bgra32 b, int tol)
    {
        return Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol &&
               Math.Abs(a.B - b.B) <= tol && Math.Abs(a.A - b.A) <= tol;
    }

    /// <summary>讀取合成後某點顏色（吸色用）· Composited colour at a point, packed BGRA (eyedropper).</summary>
    public uint GetCompositePixel(int x, int y)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return 0;
        using var flat = CompositeImage();
        var p = flat[x, y];
        return ((uint)p.A << 24) | ((uint)p.R << 16) | ((uint)p.G << 8) | p.B;
    }

    /// <summary>喺目前圖層畫文字（用內建系統字型）· Draw text on the active layer using a system font.</summary>
    public TweakResult DrawText(string text, int x, int y, float sizePx, uint bgra, string? fontFamily)
    {
        try
        {
            PushUndo();
            FontFamily family;
            if (!string.IsNullOrWhiteSpace(fontFamily) && SystemFonts.TryGet(fontFamily, out var f))
                family = f;
            else if (SystemFonts.TryGet("Segoe UI", out var seg))
                family = seg;
            else
                family = SystemFonts.Families.First();
            var font = family.CreateFont(Math.Max(4f, sizePx), FontStyle.Regular);
            var color = ISColor.FromRgba((byte)((bgra >> 16) & 0xFF), (byte)((bgra >> 8) & 0xFF), (byte)(bgra & 0xFF), (byte)((bgra >> 24) & 0xFF));
            ActiveLayer.Image.Mutate(c => c.DrawText(text, font, color, new ISPointF(x, y)));
            return TweakResult.Ok("Text added", "已加入文字");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Text failed: {ex.Message}", $"加文字失敗：{ex.Message}");
        }
    }

    public static IReadOnlyList<string> AvailableFonts()
    {
        try { return SystemFonts.Families.Select(f => f.Name).OrderBy(n => n).ToList(); }
        catch { return new List<string> { "Segoe UI" }; }
    }

    // ===================== Save =====================

    public async Task<TweakResult> SaveAsync(string path, int jpegQuality = 90)
    {
        try
        {
            using var flat = CompositeImage();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            await using var fs = File.Create(path);
            switch (ext)
            {
                case ".jpg":
                case ".jpeg":
                    await flat.SaveAsJpegAsync(fs, new JpegEncoder { Quality = Math.Clamp(jpegQuality, 1, 100) });
                    break;
                case ".bmp":
                    await flat.SaveAsBmpAsync(fs, new BmpEncoder());
                    break;
                case ".gif":
                    await flat.SaveAsGifAsync(fs, new GifEncoder());
                    break;
                case ".webp":
                    await flat.SaveAsWebpAsync(fs, new WebpEncoder { Quality = Math.Clamp(jpegQuality, 1, 100) });
                    break;
                default: // .png and anything else
                    await flat.SaveAsPngAsync(fs, new PngEncoder());
                    break;
            }
            FilePath = path;
            Dirty = false;
            return TweakResult.Ok($"Saved: {path}", $"已儲存：{path}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Save failed: {ex.Message}", $"儲存失敗：{ex.Message}");
        }
    }

    // ===================== Cleanup =====================

    private void DisposeLayers()
    {
        foreach (var l in Layers) l.Image.Dispose();
    }

    private void DisposeLayersKeepList()
    {
        foreach (var l in Layers) l.Image.Dispose();
    }

    public void Dispose()
    {
        DisposeLayers();
        foreach (var s in _undo) foreach (var l in s.Layers) l.Image.Dispose();
        foreach (var s in _redo) foreach (var l in s.Layers) l.Image.Dispose();
        _undo.Clear();
        _redo.Clear();
    }
}
