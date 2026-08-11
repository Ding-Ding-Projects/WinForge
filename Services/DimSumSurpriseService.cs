using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;

namespace WinForge.Services;

/// <summary>
/// Non-blocking startup dim-sum surprise. The catalog and image remain in the user's local app
/// data cache; no image is generated, copied into the repository, or attached to a release.
/// </summary>
public static class DimSumSurpriseService
{
    public const string CatalogUrl = "https://raw.githubusercontent.com/Ding-Ding-Projects/dim-sum-photos/main/catalog/index.json";
    public const string AssetBaseUrl = "https://github.com/Ding-Ding-Projects/dim-sum-photos/releases/download/catalog-v1-part-003/";
    public const string CatalogRevision = "catalog-v1-part-003";

    // These are the first published assets in catalog-v1-part-003. The catalog remains the
    // authority for bilingual names and alt text; this bounded list prevents a startup request
    // from guessing at an unpublished image URL.
    private static readonly string[] PublishedAssetFileNames =
    {
        "hk-dish-1986-new-territories-tea-house-ginger-scallion-preserved-olive-and-pea-shoot-steamed-bao.png",
        "hk-dish-1987-new-territories-tea-house-white-pepper-preserved-olive-and-pea-shoot-steamed-bao.png",
        "hk-dish-1988-new-territories-tea-house-fermented-chilli-preserved-olive-and-pea-shoot-steamed-bao.png",
        "hk-dish-1989-new-territories-tea-house-mandarin-peel-preserved-olive-and-pea-shoot-steamed-bao.png",
        "hk-dish-1990-new-territories-tea-house-black-bean-preserved-olive-and-pea-shoot-steamed-bao.png",
    };

    private static readonly HttpClient Http = CreateHttpClient();
    private static int _attempted;

    public static IReadOnlyList<string> PublishedAssets => PublishedAssetFileNames;

    public static void Start(DispatcherQueue ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        if (Interlocked.Exchange(ref _attempted, 1) != 0) return;
        if (!TermsService.HasAccepted || UniversalSettingsService.SchoolModeEnabled ||
            !string.IsNullOrWhiteSpace(App.StartPage))
            return;

        _ = RunAsync(ui);
    }

    private static async Task RunAsync(DispatcherQueue ui)
    {
        try
        {
            // A fresh draw is made for every eligible launch and is never retried in that launch.
            if (!DimSumSurpriseCore.DrawSurprise(Random.Shared.NextDouble())) return;
            var catalog = await ReadCatalogAsync().ConfigureAwait(false);
            if (catalog is null) return;

            var dishes = DimSumSurpriseCore.ParseEligibleCatalog(catalog, PublishedAssetFileNames);
            var dish = DimSumSurpriseCore.SelectRandom(dishes);
            if (dish is null) return;

            var imagePath = await EnsureImageAsync(dish).ConfigureAwait(false);
            if (imagePath is null) return;
            if (UniversalSettingsService.SchoolModeEnabled) return;

            ui.TryEnqueue(() =>
            {
                AppNotificationService.Publish(new AppNoticeDraft(
                    "A little dim sum surprise",
                    "有少少點心驚喜",
                    $"{dish.NameEn} · {dish.NameZhHant} is ready for a tiny yum-cha moment.",
                    $"{dish.NameEn} · {dish.NameZhHant} 出場，食住先開工啦。",
                    AppNoticeSeverity.Informational,
                    Key: "dim-sum.surprise",
                    AutoDismissMs: 12_000,
                    ImagePath: imagePath,
                    ImageAltEn: dish.AltEn,
                    ImageAltZh: dish.AltZhHant));
            });
        }
        catch (Exception ex)
        {
            CrashLogger.Log("startup:dim-sum-surprise", ex);
        }
    }

    private static async Task<string?> ReadCatalogAsync()
    {
        var root = CacheRoot();
        var path = Path.Combine(root, "catalog-v1.json");
        try
        {
            if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(7))
                return File.ReadAllText(path);

            var bytes = await DownloadBoundedAsync(new Uri(CatalogUrl), DimSumSurpriseCore.MaximumCatalogBytes).ConfigureAwait(false);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            if (DimSumSurpriseCore.ParseEligibleCatalog(json, PublishedAssetFileNames).Count == 0)
                return File.Exists(path) ? File.ReadAllText(path) : null;
            WriteAtomic(path, bytes);
            return json;
        }
        catch
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; } catch { return null; }
        }
    }

    private static async Task<string?> EnsureImageAsync(DimSumDishDefinition dish)
    {
        var root = CacheRoot();
        var imagePath = Path.Combine(root, dish.Id + ".png");
        try
        {
            if (File.Exists(imagePath) && DimSumSurpriseCore.LooksLikePng(File.ReadAllBytes(imagePath)))
                return imagePath;

            var uri = new Uri(AssetBaseUrl + Uri.EscapeDataString(dish.AssetFileName));
            var bytes = await DownloadBoundedAsync(uri, DimSumSurpriseCore.MaximumImageBytes).ConfigureAwait(false);
            if (!DimSumSurpriseCore.LooksLikePng(bytes)) return null;
            WriteAtomic(imagePath, bytes);
            return imagePath;
        }
        catch { return null; }
    }

    private static async Task<byte[]> DownloadBoundedAsync(Uri uri, int maximumBytes)
    {
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host is not ("raw.githubusercontent.com" or "github.com"))
            throw new InvalidOperationException("Dim-sum source is outside the public allowlist.");

        using var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length > maximumBytes)
            throw new InvalidDataException("Dim-sum source exceeded the bounded response size.");

        await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > maximumBytes) throw new InvalidDataException("Dim-sum source exceeded the bounded response size.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static string CacheRoot()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "dim-sum", CatalogRevision);
        Directory.CreateDirectory(root);
        return root;
    }

    private static void WriteAtomic(string path, byte[] bytes)
    {
        var partial = path + ".part-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(partial, bytes);
            File.Move(partial, path, true);
        }
        finally
        {
            try { if (File.Exists(partial)) File.Delete(partial); } catch { }
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }
}
