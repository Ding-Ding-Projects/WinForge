using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// 像素畫文件模型（原生 WinForge，唔抄 Aseprite 任何程式碼／資產／調色盤）·
/// The pixel-art document model — original WinForge code, NOT derived from Aseprite's source,
/// assets, default palette or .ase format. A document is a stack of <see cref="PixelLayer"/>s
/// across one or more <see cref="PixelFrame"/>s, each a flat BGRA buffer the size of the canvas.
/// All raster ops (set/get pixel, flood fill, composite, PNG/GIF encode) live here so the page
/// stays a thin host. Undo/redo stores compact per-stroke pixel deltas, not full snapshots.
/// </summary>
public sealed class PixelEditorService
{
    public const int MaxSize = 256;

    public int Width { get; private set; }
    public int Height { get; private set; }

    public List<PixelFrame> Frames { get; } = new();
    public int ActiveFrameIndex { get; private set; }
    public int ActiveLayerIndex { get; private set; }

    public PixelFrame ActiveFrame => Frames[ActiveFrameIndex];
    public PixelLayer ActiveLayer => ActiveFrame.Layers[ActiveLayerIndex];

    private readonly Stack<EditAction> _undo = new();
    private readonly Stack<EditAction> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public PixelEditorService(int w = 32, int h = 32)
    {
        NewDocument(w, h);
    }

    /// <summary>新建畫布（清空所有圖層與影格）· Start a fresh document of the given size.</summary>
    public void NewDocument(int w, int h)
    {
        Width = Math.Clamp(w, 1, MaxSize);
        Height = Math.Clamp(h, 1, MaxSize);
        Frames.Clear();
        var frame = new PixelFrame(Width, Height);
        frame.Layers.Add(new PixelLayer("Layer 1 · 圖層 1", Width, Height));
        Frames.Add(frame);
        ActiveFrameIndex = 0;
        ActiveLayerIndex = 0;
        _undo.Clear();
        _redo.Clear();
    }

    // ===================== Frame / layer navigation =====================

    public void SetActiveFrame(int i) => ActiveFrameIndex = Math.Clamp(i, 0, Frames.Count - 1);

    public void SetActiveLayer(int i) => ActiveLayerIndex = Math.Clamp(i, 0, ActiveFrame.Layers.Count - 1);

    /// <summary>新增空白影格 · Add a new blank frame after the active one.</summary>
    public void AddFrame()
    {
        var f = new PixelFrame(Width, Height);
        // Mirror the active frame's layer structure (names/visibility/opacity) but blank pixels.
        foreach (var src in ActiveFrame.Layers)
            f.Layers.Add(new PixelLayer(src.Name, Width, Height) { Visible = src.Visible, Opacity = src.Opacity });
        if (f.Layers.Count == 0) f.Layers.Add(new PixelLayer("Layer 1 · 圖層 1", Width, Height));
        f.DelayMs = ActiveFrame.DelayMs;
        Frames.Insert(ActiveFrameIndex + 1, f);
        ActiveFrameIndex++;
        ClampLayer();
        InvalidateHistoryForStructureChange();
    }

    /// <summary>複製目前影格（連像素）· Duplicate the active frame, pixels and all.</summary>
    public void DuplicateFrame()
    {
        var f = ActiveFrame.Clone();
        Frames.Insert(ActiveFrameIndex + 1, f);
        ActiveFrameIndex++;
        InvalidateHistoryForStructureChange();
    }

    public void DeleteFrame()
    {
        if (Frames.Count <= 1) return;
        Frames.RemoveAt(ActiveFrameIndex);
        ActiveFrameIndex = Math.Clamp(ActiveFrameIndex, 0, Frames.Count - 1);
        ClampLayer();
        InvalidateHistoryForStructureChange();
    }

    public void MoveFrame(int from, int to)
    {
        if (from < 0 || from >= Frames.Count || to < 0 || to >= Frames.Count) return;
        var f = Frames[from];
        Frames.RemoveAt(from);
        Frames.Insert(to, f);
        ActiveFrameIndex = to;
        InvalidateHistoryForStructureChange();
    }

    // ===================== Layers =====================

    public void AddLayer()
    {
        var name = $"Layer {ActiveFrame.Layers.Count + 1} · 圖層 {ActiveFrame.Layers.Count + 1}";
        // Add the same-named layer slot to every frame so the timeline stays rectangular.
        foreach (var f in Frames)
            f.Layers.Add(new PixelLayer(name, Width, Height));
        ActiveLayerIndex = ActiveFrame.Layers.Count - 1;
        InvalidateHistoryForStructureChange();
    }

    public void DeleteLayer()
    {
        if (ActiveFrame.Layers.Count <= 1) return;
        int idx = ActiveLayerIndex;
        foreach (var f in Frames)
            if (idx < f.Layers.Count) f.Layers.RemoveAt(idx);
        ActiveLayerIndex = Math.Clamp(ActiveLayerIndex, 0, ActiveFrame.Layers.Count - 1);
        InvalidateHistoryForStructureChange();
    }

    /// <summary>圖層上移／下移（喺所有影格同步）· Reorder the active layer across all frames.</summary>
    public void MoveLayer(int delta)
    {
        int to = ActiveLayerIndex + delta;
        if (to < 0 || to >= ActiveFrame.Layers.Count) return;
        foreach (var f in Frames)
        {
            if (ActiveLayerIndex < f.Layers.Count && to < f.Layers.Count)
            {
                var l = f.Layers[ActiveLayerIndex];
                f.Layers.RemoveAt(ActiveLayerIndex);
                f.Layers.Insert(to, l);
            }
        }
        ActiveLayerIndex = to;
        InvalidateHistoryForStructureChange();
    }

    private void ClampLayer() => ActiveLayerIndex = Math.Clamp(ActiveLayerIndex, 0, ActiveFrame.Layers.Count - 1);

    // EditAction stores frame/layer indices. Any timeline or layer-structure
    // mutation can remap those indices, so never replay a stroke onto a wrong
    // target (or into a deleted one).
    private void InvalidateHistoryForStructureChange()
    {
        _undo.Clear();
        _redo.Clear();
    }

    // ===================== Pixel ops (with undo deltas) =====================

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public uint GetPixel(PixelLayer layer, int x, int y) => layer.Pixels[y * Width + x];

    /// <summary>取得目前圖層某格的顏色（合成前）· Read the active layer's raw pixel.</summary>
    public uint GetActivePixel(int x, int y) => InBounds(x, y) ? ActiveLayer.Pixels[y * Width + x] : 0u;

    /// <summary>合成後某點顏色（眼藥水用）· Composited colour at a point (used by the eyedropper).</summary>
    public uint GetCompositePixel(int x, int y)
    {
        if (!InBounds(x, y)) return 0u;
        uint outc = 0u;
        foreach (var l in ActiveFrame.Layers)
        {
            if (!l.Visible) continue;
            uint c = l.Pixels[y * Width + x];
            c = ApplyOpacity(c, l.Opacity);
            outc = OverBgra(c, outc);
        }
        return outc;
    }

    /// <summary>一筆畫開始：開新可撤銷動作 · Begin a stroke = open a new undoable batch.</summary>
    public EditAction BeginStroke()
    {
        var a = new EditAction(ActiveFrameIndex, ActiveLayerIndex);
        return a;
    }

    /// <summary>set 一個像素並記錄 delta（畀 undo 用）· Set one pixel, recording the before-value.</summary>
    public void SetPixel(EditAction action, int x, int y, uint bgra)
    {
        if (!InBounds(x, y)) return;
        var layer = ActiveFrame.Layers[action.LayerIndex];
        int i = y * Width + x;
        uint before = layer.Pixels[i];
        if (before == bgra) return;
        action.Record(i, before, bgra);
        layer.Pixels[i] = bgra;
    }

    /// <summary>提交一筆畫到 undo stack（無改動就丟棄）· Commit a stroke, dropping no-ops.</summary>
    public void CommitStroke(EditAction action)
    {
        if (action.Count == 0) return;
        _undo.Push(action);
        _redo.Clear();
    }

    /// <summary>泛洪填色（4 連通）· Flood fill (4-connected) starting at x,y on the active layer.</summary>
    public EditAction? FloodFill(int x, int y, uint bgra)
    {
        if (!InBounds(x, y)) return null;
        var layer = ActiveLayer;
        uint target = layer.Pixels[y * Width + x];
        if (target == bgra) return null;

        var action = BeginStroke();
        var stack = new Stack<(int x, int y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            if (cx < 0 || cy < 0 || cx >= Width || cy >= Height) continue;
            int i = cy * Width + cx;
            if (layer.Pixels[i] != target) continue;
            action.Record(i, layer.Pixels[i], bgra);
            layer.Pixels[i] = bgra;
            stack.Push((cx + 1, cy));
            stack.Push((cx - 1, cy));
            stack.Push((cx, cy + 1));
            stack.Push((cx, cy - 1));
        }
        if (action.Count == 0) return null;
        _undo.Push(action);
        _redo.Clear();
        return action;
    }

    /// <summary>清除一個矩形選取區（刪除選取）· Clear a rectangle on the active layer (delete selection).</summary>
    public void ClearRect(int x0, int y0, int x1, int y1)
    {
        var action = BeginStroke();
        int xa = Math.Min(x0, x1), xb = Math.Max(x0, x1);
        int ya = Math.Min(y0, y1), yb = Math.Max(y0, y1);
        for (int yy = ya; yy <= yb; yy++)
            for (int xx = xa; xx <= xb; xx++)
                SetPixel(action, xx, yy, 0u);
        CommitStroke(action);
    }

    /// <summary>移動一個矩形選取區（剪走 + 貼上偏移）· Move a rectangle selection by (dx,dy).</summary>
    public void MoveRect(int x0, int y0, int x1, int y1, int dx, int dy)
    {
        if (dx == 0 && dy == 0) return;
        int rawXa = Math.Min(x0, x1), rawXb = Math.Max(x0, x1);
        int rawYa = Math.Min(y0, y1), rawYb = Math.Max(y0, y1);
        if (rawXb < 0 || rawXa >= Width || rawYb < 0 || rawYa >= Height) return;
        int xa = Math.Max(0, rawXa), xb = Math.Min(Width - 1, rawXb);
        int ya = Math.Max(0, rawYa), yb = Math.Min(Height - 1, rawYb);
        var layer = ActiveLayer;
        // Snapshot the source block.
        var grab = new List<(int x, int y, uint c)>();
        for (int yy = ya; yy <= yb; yy++)
            for (int xx = xa; xx <= xb; xx++)
                grab.Add((xx, yy, layer.Pixels[yy * Width + xx]));

        var action = BeginStroke();
        foreach (var (xx, yy, _) in grab) SetPixel(action, xx, yy, 0u);
        foreach (var (xx, yy, c) in grab) SetPixel(action, xx + dx, yy + dy, c);
        CommitStroke(action);
    }

    public void Undo()
    {
        if (_undo.Count == 0) return;
        var a = _undo.Pop();
        var layer = Frames[a.FrameIndex].Layers[a.LayerIndex];
        a.ApplyBefore(layer.Pixels);
        _redo.Push(a);
    }

    public void Redo()
    {
        if (_redo.Count == 0) return;
        var a = _redo.Pop();
        var layer = Frames[a.FrameIndex].Layers[a.LayerIndex];
        a.ApplyAfter(layer.Pixels);
        _undo.Push(a);
    }

    // ===================== Compositing =====================

    /// <summary>將一個影格合成成一張平 BGRA 圖（含透明棋盤背景由 UI 畫）· Flatten a frame to BGRA.</summary>
    public byte[] CompositeFrame(int frameIndex)
    {
        var frame = Frames[Math.Clamp(frameIndex, 0, Frames.Count - 1)];
        int n = Width * Height;
        var outp = new uint[n];
        foreach (var l in frame.Layers)
        {
            if (!l.Visible) continue;
            for (int i = 0; i < n; i++)
            {
                uint c = ApplyOpacity(l.Pixels[i], l.Opacity);
                if ((c >> 24) == 0) continue;
                outp[i] = OverBgra(c, outp[i]);
            }
        }
        var bytes = new byte[n * 4];
        System.Buffer.BlockCopy(outp, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>「source-over」合成（BGRA，非預乘）· Porter–Duff source-over for non-premultiplied BGRA.</summary>
    private static uint OverBgra(uint src, uint dst)
    {
        byte sa = (byte)(src >> 24);
        if (sa == 255 || dst == 0) return sa == 0 ? dst : src;
        if (sa == 0) return dst;
        byte da = (byte)(dst >> 24);
        float fa = sa / 255f, fda = da / 255f;
        float outA = fa + fda * (1 - fa);
        if (outA <= 0) return 0;
        byte Blend(int shift)
        {
            float s = ((src >> shift) & 0xFF) / 255f;
            float d = ((dst >> shift) & 0xFF) / 255f;
            float o = (s * fa + d * fda * (1 - fa)) / outA;
            return (byte)Math.Clamp((int)Math.Round(o * 255), 0, 255);
        }
        byte b = Blend(0), g = Blend(8), r = Blend(16);
        return ((uint)Math.Clamp((int)Math.Round(outA * 255), 0, 255) << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
    }

    private static uint ApplyOpacity(uint c, double opacity)
    {
        if (opacity >= 1.0) return c;
        byte a = (byte)(c >> 24);
        a = (byte)Math.Clamp((int)Math.Round(a * opacity), 0, 255);
        return (c & 0x00FFFFFF) | ((uint)a << 24);
    }

    // ===================== PNG / GIF export =====================

    /// <summary>匯出單一影格做 PNG · Export one frame to a PNG file.</summary>
    public async Task<TweakResult> ExportPngAsync(string path, int frameIndex)
    {
        try
        {
            var bgra = CompositeFrame(frameIndex);
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
            var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                (uint)Width, (uint)Height, 96, 96, bgra);
            await enc.FlushAsync();
            return TweakResult.Ok($"Saved PNG: {path}", $"已儲存 PNG：{path}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"PNG export failed: {ex.Message}", $"PNG 匯出失敗：{ex.Message}");
        }
    }

    /// <summary>匯出全部影格做動畫 GIF（原生 GifEncoder，逐格延遲＋無限循環）·
    /// Export every frame to an animated GIF with per-frame delay and looping, via the native
    /// Windows GIF encoder (GoToNextFrame + LZW). No external tool needed.</summary>
    public async Task<TweakResult> ExportGifAsync(string path)
    {
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
            var file = await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.ReplaceExisting);
            using var stream = await file.OpenAsync(FileAccessMode.ReadWrite);
            var enc = await BitmapEncoder.CreateAsync(BitmapEncoder.GifEncoderId, stream);

            // Loop forever: GIF Application Extension "NETSCAPE2.0" with loop count 0.
            try
            {
                var appProps = new BitmapPropertySet
                {
                    { "/appext/application", new BitmapTypedValue(
                        new byte[] { (byte)'N',(byte)'E',(byte)'T',(byte)'S',(byte)'C',(byte)'A',(byte)'P',(byte)'E',
                                     (byte)'2',(byte)'.',(byte)'0' }, Windows.Foundation.PropertyType.UInt8Array) },
                    { "/appext/data", new BitmapTypedValue(
                        new byte[] { 3, 1, 0, 0 }, Windows.Foundation.PropertyType.UInt8Array) },
                };
                await enc.BitmapContainerProperties.SetPropertiesAsync(appProps);
            }
            catch { /* loop metadata best-effort */ }

            for (int f = 0; f < Frames.Count; f++)
            {
                var bgra = CompositeFrame(f);
                enc.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                    (uint)Width, (uint)Height, 96, 96, bgra);

                // Per-frame delay is in 1/100 s for GIF.
                ushort delayCs = (ushort)Math.Clamp(Frames[f].DelayMs / 10, 1, 65535);
                try
                {
                    var frameProps = new BitmapPropertySet
                    {
                        { "/grctlext/Delay", new BitmapTypedValue(delayCs, Windows.Foundation.PropertyType.UInt16) },
                        { "/grctlext/Disposal", new BitmapTypedValue((byte)2, Windows.Foundation.PropertyType.UInt8) },
                    };
                    await enc.BitmapProperties.SetPropertiesAsync(frameProps);
                }
                catch { }

                if (f < Frames.Count - 1) await enc.GoToNextFrameAsync();
            }
            await enc.FlushAsync();
            return TweakResult.Ok($"Saved GIF ({Frames.Count} frames): {path}", $"已儲存 GIF（{Frames.Count} 格）：{path}");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"GIF export failed: {ex.Message}", $"GIF 匯出失敗：{ex.Message}");
        }
    }

    // ===================== PNG import =====================

    /// <summary>匯入 PNG／圖片做新文件（縮放到 ≤256）· Import an image as a fresh document.</summary>
    public async Task<TweakResult> ImportImageAsync(string path)
    {
        try
        {
            var srcFile = await StorageFile.GetFileFromPathAsync(path);
            using var inStream = await srcFile.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(inStream);
            uint ow = decoder.PixelWidth, oh = decoder.PixelHeight;
            double scale = Math.Min(1.0, Math.Min(MaxSize / (double)ow, MaxSize / (double)oh));
            uint nw = (uint)Math.Max(1, Math.Round(ow * scale));
            uint nh = (uint)Math.Max(1, Math.Round(oh * scale));
            var data = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight,
                new BitmapTransform { ScaledWidth = nw, ScaledHeight = nh, InterpolationMode = BitmapInterpolationMode.NearestNeighbor },
                ExifOrientationMode.IgnoreExifOrientation, ColorManagementMode.DoNotColorManage);
            var bytes = data.DetachPixelData();

            NewDocument((int)nw, (int)nh);
            var layer = ActiveLayer;
            for (int i = 0; i < Width * Height; i++)
            {
                int b = i * 4;
                layer.Pixels[i] = (uint)(bytes[b] | (bytes[b + 1] << 8) | (bytes[b + 2] << 16) | (bytes[b + 3] << 24));
            }
            return TweakResult.Ok($"Imported {nw}×{nh} from {Path.GetFileName(path)}", $"已匯入 {nw}×{nh}（{Path.GetFileName(path)}）");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Import failed: {ex.Message}", $"匯入失敗：{ex.Message}");
        }
    }

    // ===================== Optional: detect / launch installed Aseprite =====================

    private static string? _asepritePath;
    private static bool _checked;

    /// <summary>偵測有冇裝咗 Aseprite（Steam／登錄檔／PATH／LocalAppData）· Locate an installed Aseprite, if any.</summary>
    public static string? FindAseprite()
    {
        if (_checked) return _asepritePath;
        _checked = true;
        try
        {
            var candidates = new List<string>();
            string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            candidates.Add(Path.Combine(pf86, "Steam", "steamapps", "common", "Aseprite", "Aseprite.exe"));
            candidates.Add(Path.Combine(pf, "Steam", "steamapps", "common", "Aseprite", "Aseprite.exe"));
            candidates.Add(Path.Combine(pf, "Aseprite", "Aseprite.exe"));
            candidates.Add(Path.Combine(pf86, "Aseprite", "Aseprite.exe"));
            candidates.Add(Path.Combine(local, "Programs", "Aseprite", "Aseprite.exe"));

            foreach (var c in candidates)
                if (File.Exists(c)) { _asepritePath = c; return _asepritePath; }

            // PATH lookup.
            var env = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var dir in env.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try { var p = Path.Combine(dir, "Aseprite.exe"); if (File.Exists(p)) { _asepritePath = p; return p; } }
                catch { }
            }

            // Registry App Paths.
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\Aseprite.exe");
                if (key?.GetValue("") is string rp && File.Exists(rp)) { _asepritePath = rp; return rp; }
            }
            catch { }
        }
        catch { }
        return _asepritePath;
    }

    /// <summary>重新偵測（清快取）· Force a re-scan for Aseprite.</summary>
    public static string? RescanAseprite()
    {
        _checked = false;
        _asepritePath = null;
        return FindAseprite();
    }

    public static bool LaunchAseprite(string? openFile = null)
    {
        var exe = FindAseprite();
        if (exe is null) return false;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
            };
            if (!string.IsNullOrEmpty(openFile) && File.Exists(openFile)) psi.Arguments = $"\"{openFile}\"";
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }
}

/// <summary>一個影格 = 一疊圖層 + 動畫延遲 · One animation frame: a stack of layers plus a delay.</summary>
public sealed class PixelFrame
{
    public List<PixelLayer> Layers { get; } = new();
    public int DelayMs { get; set; } = 100;
    public int Width { get; }
    public int Height { get; }

    public PixelFrame(int w, int h) { Width = w; Height = h; }

    public PixelFrame Clone()
    {
        var f = new PixelFrame(Width, Height) { DelayMs = DelayMs };
        foreach (var l in Layers) f.Layers.Add(l.Clone());
        return f;
    }
}

/// <summary>一個圖層 = 一塊扁平 BGRA 緩衝 · One layer: a flat BGRA pixel buffer (0xAARRGGBB packed BGRA).</summary>
public sealed class PixelLayer
{
    public string Name { get; set; }
    public bool Visible { get; set; } = true;
    public double Opacity { get; set; } = 1.0;
    /// <summary>像素：每格一個 uint，記憶體佈局 = B,G,R,A（低→高位）· One uint per pixel, byte order B,G,R,A.</summary>
    public uint[] Pixels { get; }
    public int Width { get; }
    public int Height { get; }

    public PixelLayer(string name, int w, int h)
    {
        Name = name; Width = w; Height = h;
        Pixels = new uint[w * h];
    }

    public PixelLayer Clone()
    {
        var l = new PixelLayer(Name, Width, Height) { Visible = Visible, Opacity = Opacity };
        Array.Copy(Pixels, l.Pixels, Pixels.Length);
        return l;
    }
}

/// <summary>一筆可撤銷編輯（壓縮 delta：索引 + 前後值）· One undoable edit as compact (index, before, after) deltas.</summary>
public sealed class EditAction
{
    public int FrameIndex { get; }
    public int LayerIndex { get; }
    private readonly List<int> _idx = new();
    private readonly List<uint> _before = new();
    private readonly List<uint> _after = new();

    public int Count => _idx.Count;

    public EditAction(int frameIndex, int layerIndex)
    {
        FrameIndex = frameIndex;
        LayerIndex = layerIndex;
    }

    public void Record(int index, uint before, uint after)
    {
        _idx.Add(index); _before.Add(before); _after.Add(after);
    }

    public void ApplyBefore(uint[] pixels)
    {
        for (int i = 0; i < _idx.Count; i++) pixels[_idx[i]] = _before[i];
    }

    public void ApplyAfter(uint[] pixels)
    {
        for (int i = 0; i < _idx.Count; i++) pixels[_idx[i]] = _after[i];
    }
}
