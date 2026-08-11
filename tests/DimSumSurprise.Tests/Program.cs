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

Console.WriteLine($"SUMMARY: {passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
