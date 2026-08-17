using WinForge.Services;

const string Sample = """
{
  "tag_name": "1.4.9",
  "draft": false,
  "prerelease": false,
  "assets": [
    {
      "name": "rustdesk-1.4.9-x86_64.exe",
      "size": 24472432,
      "digest": "sha256:eaedeb0088e687bf46f7c46a9c6ea5493ce51f3134dfd6acbedb47b5b9136274",
      "browser_download_url": "https://github.com/rustdesk/rustdesk/releases/download/1.4.9/rustdesk-1.4.9-x86_64.exe"
    }
  ]
}
""";

var failures = new List<string>();
var passed = 0;

Run("parses the official Windows x64 asset", ParseOfficialAsset);
Run("rejects a download URL outside the official repository", RejectsUntrustedUrl);
Run("rejects a release without a SHA-256 digest", RejectsMissingDigest);
Run("rejects a draft release", RejectsDraft);
Run("recognizes only the catalog-unavailable fallback signal", CatalogUnavailableSignal);

if (failures.Count == 0)
{
    Console.WriteLine($"PASS {passed}/{passed} RustDesk installer tests");
    return 0;
}

foreach (var failure in failures) Console.Error.WriteLine(failure);
Console.Error.WriteLine($"FAIL {failures.Count}/{passed + failures.Count} RustDesk installer tests");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"FAIL {name}: {ex.Message}");
    }
}

void ParseOfficialAsset()
{
    Assert(RustDeskRelease.TryParseLatest(Sample, out var asset, out var error), error);
    Assert(asset is not null, "asset was null");
    Equal("1.4.9", asset!.Version, "version");
    Equal("rustdesk-1.4.9-x86_64.exe", asset.FileName, "asset filename");
    Equal("eaedeb0088e687bf46f7c46a9c6ea5493ce51f3134dfd6acbedb47b5b9136274",
        asset.Sha256, "digest");
    Equal(24_472_432L, asset.Size, "size");
    Equal("https", asset.DownloadUri.Scheme, "download scheme");
    Equal("github.com", asset.DownloadUri.Host, "download host");
}

void RejectsUntrustedUrl()
{
    var json = Sample.Replace("https://github.com/rustdesk/rustdesk/",
        "https://example.invalid/rustdesk/rustdesk/", StringComparison.Ordinal);
    Assert(!RustDeskRelease.TryParseLatest(json, out _, out var error), "untrusted URL was accepted");
    AssertContains(error, "not trusted");
}

void RejectsMissingDigest()
{
    var json = Sample.Replace(
        "\"digest\": \"sha256:eaedeb0088e687bf46f7c46a9c6ea5493ce51f3134dfd6acbedb47b5b9136274\"",
        "\"digest\": \"\"",
        StringComparison.Ordinal);
    Assert(!RustDeskRelease.TryParseLatest(json, out _, out var error), "missing digest was accepted");
    AssertContains(error, "SHA-256");
}

void RejectsDraft()
{
    var json = Sample.Replace("\"draft\": false", "\"draft\": true", StringComparison.Ordinal);
    Assert(!RustDeskRelease.TryParseLatest(json, out _, out var error), "draft release was accepted");
    AssertContains(error, "draft");
}

void CatalogUnavailableSignal()
{
    Assert(RustDeskRelease.IsPackageUnavailable("No package found matching input criteria."),
        "catalog failure was not recognized");
    Assert(!RustDeskRelease.IsPackageUnavailable("Installer hash mismatch."),
        "an installer failure incorrectly triggered the fallback");
    Assert(!RustDeskRelease.IsPackageUnavailable(""),
        "empty output incorrectly triggered the fallback");
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual, string field)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"{field}: expected '{expected}', got '{actual}'");
}

static void AssertContains(string actual, string expected)
{
    Assert(actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
        $"expected '{actual}' to contain '{expected}'");
}
