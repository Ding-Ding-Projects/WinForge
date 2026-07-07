using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// HTTP 標頭檢測 · HTTP header inspector — issues a real GET/HEAD request with pure managed
/// <see cref="HttpClient"/>, optionally following redirects, and reports the final status, elapsed
/// time and every response/content header. No redirect to any external program. Never throws.
/// </summary>
public static class HttpHeadersService
{
    /// <summary>One header row for the ListView (key/value).</summary>
    public sealed class HeaderRow
    {
        public string Key { get; init; } = "";
        public string Value { get; init; } = "";
    }

    /// <summary>Outcome of an inspection. <see cref="Ok"/> false means the message is an error.</summary>
    public sealed class Result
    {
        public bool Ok { get; init; }
        public string Message { get; init; } = "";
        public int StatusCode { get; init; }
        public string Reason { get; init; } = "";
        public long ElapsedMs { get; init; }
        public string ContentType { get; init; } = "";
        public string ContentLength { get; init; } = "";
        public string Location { get; init; } = "";
        public string FinalUrl { get; init; } = "";
        public List<HeaderRow> Headers { get; init; } = new();
    }

    /// <summary>
    /// Send a request and collect headers. All failures (bad URL, DNS, TLS, timeout, socket) are
    /// caught and returned as an <see cref="Result"/> with <see cref="Result.Ok"/> = false.
    /// </summary>
    public static async Task<Result> InspectAsync(
        string url,
        bool head,
        bool followRedirects,
        IReadOnlyList<(string Key, string Value)>? customHeaders = null,
        int timeoutSeconds = 30)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Fail("Enter a URL.", "請輸入網址。");

        url = url.Trim();
        if (!url.Contains("://", StringComparison.Ordinal))
            url = "https://" + url;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return Fail($"“{url}” is not a valid http(s) URL.", $"「{url}」唔係有效嘅 http(s) 網址。");

        var handler = new HttpClientHandler { AllowAutoRedirect = followRedirects };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };

        var method = head ? HttpMethod.Head : HttpMethod.Get;
        using var request = new HttpRequestMessage(method, uri);

        if (customHeaders != null)
        {
            foreach (var (k, v) in customHeaders)
            {
                if (string.IsNullOrWhiteSpace(k)) continue;
                try { request.Headers.TryAddWithoutValidation(k.Trim(), v ?? ""); }
                catch { /* ignore a single malformed custom header */ }
            }
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds + 5));
            using var response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                .ConfigureAwait(false);
            sw.Stop();

            var rows = new List<HeaderRow>();
            foreach (var h in response.Headers)
                rows.Add(new HeaderRow { Key = h.Key, Value = string.Join(", ", h.Value) });
            foreach (var h in response.Content.Headers)
                rows.Add(new HeaderRow { Key = h.Key, Value = string.Join(", ", h.Value) });
            rows.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));

            string contentType = response.Content.Headers.ContentType?.ToString() ?? "—";
            string contentLength = response.Content.Headers.ContentLength is long len
                ? len.ToString("N0")
                : "—";
            string location = response.Headers.Location?.ToString() ?? "";
            int code = (int)response.StatusCode;

            string msg = Loc.I.Pick(
                $"{code} {response.ReasonPhrase} · {sw.ElapsedMilliseconds} ms · {rows.Count} headers",
                $"{code} {response.ReasonPhrase} · {sw.ElapsedMilliseconds} 毫秒 · {rows.Count} 個標頭");

            return new Result
            {
                Ok = true,
                Message = msg,
                StatusCode = code,
                Reason = response.ReasonPhrase ?? "",
                ElapsedMs = sw.ElapsedMilliseconds,
                ContentType = contentType,
                ContentLength = contentLength,
                Location = location,
                FinalUrl = response.RequestMessage?.RequestUri?.ToString() ?? uri.ToString(),
                Headers = rows,
            };
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return Fail($"Timed out after {timeoutSeconds}s.", $"超過 {timeoutSeconds} 秒未有回應，逾時。");
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            string detail = ex.InnerException?.Message ?? ex.Message;
            return Fail($"Request failed: {detail}", $"請求失敗：{detail}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Fail($"Error: {ex.Message}", $"發生錯誤：{ex.Message}");
        }
    }

    private static Result Fail(string en, string zh) =>
        new() { Ok = false, Message = Loc.I.Pick(en, zh), Headers = new List<HeaderRow>() };
}
