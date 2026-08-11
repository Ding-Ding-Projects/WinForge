using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace WinForge.Services;

/// <summary>
/// Non-blocking startup dim-sum surprise. The catalog and image remain in the user's local app
/// data cache; no image is generated, copied into the repository, or attached to a release.
/// </summary>
public static class DimSumSurpriseService
{
    public const string CatalogUrl = "https://raw.githubusercontent.com/Ding-Ding-Projects/dim-sum-photos/main/catalog/index.json";
    public const string CatalogRevision = "catalog-v1";

    /// <summary>Public release partitions verified against the dim-sum photo repository.</summary>
    public static IReadOnlyList<DimSumPublishedAssetPartition> PublishedAssets
        => DimSumSurpriseCore.PublishedAssetPartitions;

    private static readonly HttpClient Http = CreateHttpClient();
    private static int _attempted;

    public static void Start(DispatcherQueue ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        if (Interlocked.Exchange(ref _attempted, 1) != 0) return;
        if (!TermsService.HasAccepted || App.StartMinimized || UniversalSettingsService.SchoolModeEnabled ||
            !string.IsNullOrWhiteSpace(App.StartPage))
            return;

        // Run cache, catalog, and image work away from the UI thread. The dispatcher is used only
        // for the final notification publication after the first usable layout exists.
        _ = Task.Run(() => RunAsync(ui));
    }

    private static async Task RunAsync(DispatcherQueue ui)
    {
        try
        {
            // A fresh draw is made for every eligible launch and is never retried in that launch.
            if (!DimSumSurpriseCore.DrawSurprise(Random.Shared.NextDouble())) return;
            var catalog = await ReadCatalogAsync().ConfigureAwait(false);
            if (catalog is null) return;

            var dishes = DimSumSurpriseCore.ParsePublishedCatalog(catalog);
            var dish = DimSumSurpriseCore.SelectRandom(dishes);
            if (dish is null) return;

            var imagePath = await EnsureImageAsync(dish).ConfigureAwait(false);
            if (imagePath is null) return;
            if (UniversalSettingsService.SchoolModeEnabled || HasBlockingAttentionNotice()) return;

            ui.TryEnqueue(() =>
            {
                if (UniversalSettingsService.SchoolModeEnabled || HasBlockingAttentionNotice()) return;
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
        var digestPath = path + ".sha256";
        try
        {
            if (File.Exists(path) && DateTime.UtcNow - File.GetLastWriteTimeUtc(path) < TimeSpan.FromDays(7) &&
                TryReadCachedCatalog(path, digestPath) is string current)
                return current;

            var bytes = await DownloadBoundedAsync(new Uri(CatalogUrl), DimSumSurpriseCore.MaximumCatalogBytes).ConfigureAwait(false);
            var json = Encoding.UTF8.GetString(bytes);
            if (DimSumSurpriseCore.ParsePublishedCatalog(json).Count == 0)
                return TryReadCachedCatalog(path, digestPath);
            WriteAtomic(path, bytes);
            WriteAtomic(digestPath, Encoding.UTF8.GetBytes(Sha256Hex(bytes)));
            return json;
        }
        catch
        {
            return TryReadCachedCatalog(path, digestPath);
        }
    }

    private static async Task<string?> EnsureImageAsync(DimSumDishDefinition dish)
    {
        var root = CacheRoot();
        var imageRoot = Path.Combine(root, "images");
        Directory.CreateDirectory(imageRoot);
        var imagePath = Path.Combine(imageRoot, Sha256Hex(
            Encoding.UTF8.GetBytes(dish.AssetReleaseTag + "\n" + dish.AssetFileName)) + ".png");
        try
        {
            if (File.Exists(imagePath))
            {
                var cached = new FileInfo(imagePath);
                if (cached.Length is > 0 and <= DimSumSurpriseCore.MaximumImageBytes &&
                    await IsDecodedPngAsync(await File.ReadAllBytesAsync(imagePath).ConfigureAwait(false)).ConfigureAwait(false))
                    return imagePath;
            }

            var uri = new Uri(
                $"https://github.com/Ding-Ding-Projects/dim-sum-photos/releases/download/" +
                $"{dish.AssetReleaseTag}/{Uri.EscapeDataString(dish.AssetFileName)}");
            var bytes = await DownloadBoundedAsync(uri, DimSumSurpriseCore.MaximumImageBytes).ConfigureAwait(false);
            if (!await IsDecodedPngAsync(bytes).ConfigureAwait(false)) return null;
            WriteAtomic(imagePath, bytes);
            return imagePath;
        }
        catch { return null; }
    }

    private static async Task<byte[]> DownloadBoundedAsync(Uri uri, int maximumBytes)
    {
        ValidateSourceUri(uri);
        Uri current = uri;
        for (int redirect = 0; ; redirect++)
        {
            using var response = await Http.GetAsync(current, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                if (redirect != 0 || response.Headers.Location is not Uri location ||
                    !IsExpectedReleaseRedirect(current, location))
                    throw new InvalidDataException("Dim-sum source returned an unapproved redirect.");
                current = location;
                continue;
            }

            response.EnsureSuccessStatusCode();
            ValidateSourceUri(current);
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
    }

    private static void ValidateSourceUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme != Uri.UriSchemeHttps || uri.UserInfo.Length != 0 || uri.Port != -1 ||
            uri.Host is not ("raw.githubusercontent.com" or "github.com" or "release-assets.githubusercontent.com"))
            throw new InvalidOperationException("Dim-sum source is outside the public allowlist.");
    }

    private static bool IsExpectedReleaseRedirect(Uri from, Uri to)
        => string.Equals(from.Host, "github.com", StringComparison.OrdinalIgnoreCase) &&
           string.Equals(to.Host, "release-assets.githubusercontent.com", StringComparison.OrdinalIgnoreCase) &&
           to.Scheme == Uri.UriSchemeHttps && to.UserInfo.Length == 0 && to.Port == -1;

    private static async Task<bool> IsDecodedPngAsync(byte[] bytes)
    {
        if (!DimSumSurpriseCore.LooksLikePng(bytes) || bytes.Length > DimSumSurpriseCore.MaximumImageBytes)
            return false;
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(stream))
            {
                writer.WriteBytes(bytes);
                await writer.StoreAsync();
                await writer.FlushAsync();
                writer.DetachStream();
            }
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            using var bitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied);
            return decoder.PixelWidth > 0 && decoder.PixelHeight > 0 && bitmap is not null;
        }
        catch { return false; }
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

    private static string? TryReadCachedCatalog(string path, string digestPath)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length > DimSumSurpriseCore.MaximumCatalogBytes || !File.Exists(digestPath)) return null;
            var expected = File.ReadAllText(digestPath).Trim();
            if (!string.Equals(expected, Sha256Hex(bytes), StringComparison.OrdinalIgnoreCase)) return null;
            var json = Encoding.UTF8.GetString(bytes);
            return DimSumSurpriseCore.ParsePublishedCatalog(json).Count == 0 ? null : json;
        }
        catch { return null; }
    }

    private static string Sha256Hex(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
    }

    private static bool HasBlockingAttentionNotice()
        => AppNotificationService.Active.Any(x => x.Key == "app.update" || x.Severity is
            AppNoticeSeverity.Progress or AppNoticeSeverity.Warning or AppNoticeSeverity.Error);
}
