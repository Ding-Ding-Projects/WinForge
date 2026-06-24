namespace WinForge.Models;

/// <summary>
/// 一個調校分類（例如「私隱」、「效能」）。
/// A category of tweaks (e.g. Privacy, Performance), with bilingual name and a Segoe Fluent icon.
/// </summary>
public sealed class AppCategory
{
    public required string Id { get; init; }
    public required LocalizedText Name { get; init; }
    public required LocalizedText Blurb { get; init; }

    /// <summary>導覽分組 · Nav group ("win11" or "tools").</summary>
    public string Group { get; init; } = "win11";

    /// <summary>Segoe Fluent Icons glyph code, e.g. "".</summary>
    public required string Glyph { get; init; }

    public override string ToString() => Id;
}
