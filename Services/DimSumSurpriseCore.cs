using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>
/// Pure catalog parsing and selection rules for the startup dim-sum surprise. The application
/// consumes only metadata from the public catalog and only filenames that have already been
/// verified against a published catalog-v1 release asset.
/// </summary>
public sealed record DimSumDishDefinition(
    string Id,
    string NameEn,
    string NameZhHant,
    string AltEn,
    string AltZhHant,
    string AssetFileName)
{
    /// <summary>Published catalog-v1 release tag containing <see cref="AssetFileName"/>.</summary>
    public string AssetReleaseTag { get; init; } = "catalog-v1";
}

/// <summary>
/// A verified public catalog-v1 release partition. The partition ranges are derived from the
/// published release inventories; the catalog supplies the authoritative dish metadata and path.
/// </summary>
public sealed record DimSumPublishedAssetPartition(
    int FirstDishNumber,
    int LastDishNumber,
    string ReleaseTag)
{
    public bool Contains(int dishNumber)
        => dishNumber >= FirstDishNumber && dishNumber <= LastDishNumber;
}

public static class DimSumSurpriseCore
{
    public const double Probability = 0.10;
    public const int MaximumCatalogBytes = 16 * 1024 * 1024;
    public const int MaximumImageBytes = 12 * 1024 * 1024;

    // Public release inventory verified on 2026-08-11:
    // catalog-v1: 995 assets, catalog-v1-part-002: 990 assets,
    // catalog-v1-part-003: 943 assets. The exact names live in the generated name-only manifest.
    public static IReadOnlyList<DimSumPublishedAssetPartition> PublishedAssetPartitions { get; } =
        new[]
        {
            new DimSumPublishedAssetPartition(1, 995, "catalog-v1"),
            new DimSumPublishedAssetPartition(996, 1985, "catalog-v1-part-002"),
            new DimSumPublishedAssetPartition(1986, 3070, "catalog-v1-part-003"),
        };

    public static bool TryGetPublishedAssetRelease(string assetFileName, out string releaseTag)
    {
        releaseTag = string.Empty;
        var fileName = Path.GetFileName(assetFileName);
        if (!string.Equals(fileName, assetFileName, StringComparison.Ordinal) ||
            !DimSumPublishedAssetManifest.TryGetReleaseTag(fileName, out releaseTag))
            return false;
        return true;
    }

    public static IReadOnlyList<DimSumDishDefinition> ParsePublishedCatalog(string json)
        => ParseCatalog(json, asset =>
            TryGetPublishedAssetRelease(asset, out var releaseTag) ? releaseTag : null);

    public static IReadOnlyList<DimSumDishDefinition> ParseEligibleCatalog(
        string json,
        IEnumerable<string> publishedAssetFileNames)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumCatalogBytes)
            return Array.Empty<DimSumDishDefinition>();

        var assets = (publishedAssetFileNames ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Path.GetFileName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ParseCatalog(json, asset => assets.Contains(asset) ? "catalog-v1" : null);
    }

    private static IReadOnlyList<DimSumDishDefinition> ParseCatalog(
        string json,
        Func<string, string?> resolveReleaseTag)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumCatalogBytes)
            return Array.Empty<DimSumDishDefinition>();

        var result = new List<DimSumDishDefinition>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("dishes", out var dishes) ||
                dishes.ValueKind != JsonValueKind.Array)
                return Array.Empty<DimSumDishDefinition>();

            foreach (var dish in dishes.EnumerateArray())
            {
                if (dish.ValueKind != JsonValueKind.Object) continue;
                var id = ReadBounded(dish, "id", 128);
                var name = ObjectProperty(dish, "name");
                var image = ObjectProperty(dish, "image");
                var imageAlt = ObjectProperty(image, "alt");
                var en = ReadBounded(name, "en", 200);
                var zh = ReadBounded(name, "zhHant", 200);
                var path = ReadBounded(image, "path", 300);
                var asset = Path.GetFileName(path);
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(en) ||
                    string.IsNullOrWhiteSpace(zh) || string.IsNullOrWhiteSpace(asset))
                    continue;

                var releaseTag = resolveReleaseTag(asset);
                if (string.IsNullOrWhiteSpace(releaseTag)) continue;

                var altEn = ReadBounded(imageAlt, "en", 300);
                var altZh = ReadBounded(imageAlt, "yue", 300);
                if (string.IsNullOrWhiteSpace(altEn)) altEn = en;
                if (string.IsNullOrWhiteSpace(altZh)) altZh = zh;
                result.Add(new DimSumDishDefinition(id, en, zh, altEn, altZh, asset)
                {
                    AssetReleaseTag = releaseTag,
                });
            }
        }
        catch (JsonException)
        {
            return Array.Empty<DimSumDishDefinition>();
        }

        return result;
    }

    public static DimSumDishDefinition? SelectRandom(
        IReadOnlyList<DimSumDishDefinition> dishes,
        Random? random = null)
    {
        if (dishes is null || dishes.Count == 0) return null;
        return dishes[(random ?? Random.Shared).Next(dishes.Count)];
    }

    public static bool DrawSurprise(double sample)
        => sample >= 0 && sample < 1 && sample < Probability;

    public static bool LooksLikePng(ReadOnlySpan<byte> bytes)
        => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E &&
           bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static JsonElement ObjectProperty(JsonElement parent, string name)
        => parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.Object ? value : default;

    private static string ReadBounded(JsonElement parent, string name, int maximum)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
            return string.Empty;
        var text = value.GetString()?.Trim() ?? string.Empty;
        return text.Length <= maximum ? text : text[..maximum];
    }
}
