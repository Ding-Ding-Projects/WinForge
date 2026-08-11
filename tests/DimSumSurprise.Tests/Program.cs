using System.Linq;
using WinForge.Services;

var passed = 0;
var failed = 0;

void Check(string name, bool condition)
{
    if (condition)
    {
        Console.WriteLine($"PASS: {name}");
        passed++;
    }
    else
    {
        Console.WriteLine($"FAIL: {name}");
        failed++;
    }
}

var asset = "dish-0001.png";
var json = """
{
  "dishes": [
    {
      "id": "hk-dish-0001",
      "name": { "en": "Classic Har Gow", "zhHant": "蝦餃" },
      "image": {
        "path": "images/dish-0001.png",
        "alt": { "en": "Har Gow on a tea-house table", "yue": "茶樓枱上嘅蝦餃" }
      }
    },
    {
      "id": "hk-dish-unpublished",
      "name": { "en": "Not Published", "zhHant": "未發佈" },
      "image": { "path": "images/not-published.png", "alt": { "en": "No image", "yue": "冇圖" } }
    }
  ]
}
""";

var dishes = DimSumSurpriseCore.ParseEligibleCatalog(json, new[] { asset });
Check("published asset filters catalog", dishes.Count == 1 && dishes[0].Id == "hk-dish-0001");
Check("authoritative bilingual names and alt text", dishes[0].NameEn == "Classic Har Gow" &&
    dishes[0].NameZhHant == "蝦餃" && dishes[0].AltZhHant == "茶樓枱上嘅蝦餃");
Check("random selection stays within eligible set", DimSumSurpriseCore.SelectRandom(dishes, new Random(7))?.Id == "hk-dish-0001");
Check("probability is exactly ten percent", DimSumSurpriseCore.DrawSurprise(0.099999) &&
    !DimSumSurpriseCore.DrawSurprise(0.1) && !DimSumSurpriseCore.DrawSurprise(1));
Check("malformed catalog fails safe", DimSumSurpriseCore.ParseEligibleCatalog("{", new[] { asset }).Count == 0);
Check("PNG signature is validated", DimSumSurpriseCore.LooksLikePng(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
    !DimSumSurpriseCore.LooksLikePng(new byte[] { 0, 1, 2, 3 }));

var publishedJson = """
{
  "dishes": [
    {
      "id": "hk-dish-0023",
      "name": { "en": "Watercress Beef Balls", "zhHant": "西洋菜牛肉球" },
      "image": {
        "path": "images/hk-dish-0023-watercress-beef-balls.png",
        "alt": { "en": "Watercress beef balls", "yue": "西洋菜牛肉球" }
      }
    },
    {
      "id": "hk-dish-3071",
      "name": { "en": "Not Yet Published", "zhHant": "未發佈" },
      "image": { "path": "images/hk-dish-3071-not-yet-published.png" }
    }
  ]
}
""";
var published = DimSumSurpriseCore.ParsePublishedCatalog(publishedJson);
Check("published partition resolves catalog-v1 asset", published.Count == 1 &&
    published[0].Id == "hk-dish-0023" && published[0].AssetReleaseTag == "catalog-v1");
Check("published partition rejects an unavailable dish", !DimSumSurpriseCore.TryGetPublishedAssetRelease(
    "hk-dish-3071-not-yet-published.png", out _));
Check("published partition routes later volume", DimSumSurpriseCore.TryGetPublishedAssetRelease(
    "hk-dish-1986-new-territories-tea-house-ginger-scallion-preserved-olive-and-pea-shoot-steamed-bao.png",
    out var laterTag) && laterTag == "catalog-v1-part-003");
Check("published manifest rejects a real-number fake suffix", !DimSumSurpriseCore.TryGetPublishedAssetRelease(
    "hk-dish-2000-some-dish.png", out _));

var manyEntries = string.Join(",", Enumerable.Range(1, 513).Select(i =>
    $"{{\"id\":\"fixture-{i}\",\"name\":{{\"en\":\"Dish {i}\",\"zhHant\":\"菜式 {i}\"}},\"image\":{{\"path\":\"images/hk-dish-0001-classic-har-gow.png\"}}}}"));
var manyDishes = "{\"dishes\":[" + manyEntries + "]}";
Check("published catalog is not biased by a 512-item cap", DimSumSurpriseCore.ParsePublishedCatalog(manyDishes).Count == 513);

Console.WriteLine($"SUMMARY: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
