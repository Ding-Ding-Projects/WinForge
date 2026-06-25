using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 圖表文件嘅資料模型同存取（純 C#）· Data model and persistence for the native diagram editor (pure C#).
/// 自家 JSON 結構：節點（形狀／文字）＋連線（箭咀）＋樣式。冇外部工具、冇瀏覽器。
/// Own JSON schema: nodes (shapes/text) + edges (arrows) + styling. No external tool, no browser.
/// </summary>
public static class DiagramService
{
    public const string FileExtension = ".wfdiagram";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>節點形狀種類 · Kind of a diagram node shape.</summary>
    public enum ShapeKind
    {
        Rectangle,
        RoundedRectangle,
        Ellipse,
        Diamond,
        Text,
    }

    /// <summary>一個圖表節點（形狀或文字標籤）· One diagram node (a shape or a text label).</summary>
    public sealed class DiagramNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ShapeKind Kind { get; set; } = ShapeKind.Rectangle;
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 140;
        public double Height { get; set; } = 80;
        public string Label { get; set; } = "";

        /// <summary>填色（#AARRGGBB）· Fill colour as #AARRGGBB.</summary>
        public string Fill { get; set; } = "#FF2B5797";
        /// <summary>邊框色（#AARRGGBB）· Stroke colour as #AARRGGBB.</summary>
        public string Stroke { get; set; } = "#FFFFFFFF";
        public double StrokeWidth { get; set; } = 2;
        public double FontSize { get; set; } = 14;
        /// <summary>文字色（#AARRGGBB）· Text colour as #AARRGGBB.</summary>
        public string TextColor { get; set; } = "#FFFFFFFF";
    }

    /// <summary>一條連線（兩節點之間嘅箭咀）· One edge (an arrow between two nodes).</summary>
    public sealed class DiagramEdge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string FromId { get; set; } = "";
        public string ToId { get; set; } = "";
        public string Label { get; set; } = "";
        public string Stroke { get; set; } = "#FFFFFFFF";
        public double StrokeWidth { get; set; } = 2;
        public double FontSize { get; set; } = 12;
    }

    /// <summary>整份圖表文件 · The whole diagram document.</summary>
    public sealed class DiagramDocument
    {
        public int Version { get; set; } = 1;
        public string App { get; set; } = "WinForge.DiagramEditor";
        public double CanvasWidth { get; set; } = 2400;
        public double CanvasHeight { get; set; } = 1600;
        public List<DiagramNode> Nodes { get; set; } = new();
        public List<DiagramEdge> Edges { get; set; } = new();
    }

    public static string Serialize(DiagramDocument doc) => JsonSerializer.Serialize(doc, JsonOpts);

    public static DiagramDocument? Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<DiagramDocument>(json, JsonOpts); }
        catch { return null; }
    }

    public static async Task SaveAsync(string path, DiagramDocument doc)
        => await File.WriteAllTextAsync(path, Serialize(doc));

    public static async Task<DiagramDocument?> LoadAsync(string path)
    {
        try { return Deserialize(await File.ReadAllTextAsync(path)); }
        catch { return null; }
    }
}
