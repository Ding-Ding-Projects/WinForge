using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// HTTP 標頭參考 · HTTP headers reference — an embedded, offline catalogue of ~80 common HTTP
/// request/response headers with bilingual (English + 粵語) one-line descriptions, category,
/// direction and an example value. Pure data; never throws. No redirect / no network.
/// </summary>
public static class HttpHeaderRefService
{
    public enum Direction { Request, Response, Both }

    /// <summary>One catalogued HTTP header. Public props so classic {Binding} can read them.</summary>
    public sealed class HeaderInfo
    {
        public string Name { get; init; } = "";
        public Direction Dir { get; init; }
        public string Category { get; init; } = "";
        public string DescEn { get; init; } = "";
        public string DescZh { get; init; } = "";
        public string Example { get; init; } = "";

        // Localised display helpers used by the classic {Binding} DataTemplate.
        public string Description => Loc.I.Pick(DescEn, DescZh);
        public string DirectionText => Dir switch
        {
            Direction.Request => Loc.I.Pick("Request", "請求"),
            Direction.Response => Loc.I.Pick("Response", "回應"),
            _ => Loc.I.Pick("Both", "兩者")
        };
        public string CategoryText => Category;
        public string ExampleText => string.IsNullOrEmpty(Example) ? "" : Example;
    }

    private static readonly List<HeaderInfo> _all = Build();

    /// <summary>All catalogued headers (stable order). Never null.</summary>
    public static IReadOnlyList<HeaderInfo> All => _all;

    /// <summary>Distinct categories, alphabetically. Never null / never throws.</summary>
    public static IReadOnlyList<string> Categories()
    {
        try { return _all.Select(h => h.Category).Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList(); }
        catch { return Array.Empty<string>(); }
    }

    /// <summary>
    /// Filter the catalogue. <paramref name="query"/> matches name or description (case-insensitive),
    /// empty <paramref name="category"/> = any, null <paramref name="dir"/> = any direction (Both always shown).
    /// Never throws — returns an empty list on any error.
    /// </summary>
    public static IReadOnlyList<HeaderInfo> Filter(string? query, string? category, Direction? dir)
    {
        try
        {
            IEnumerable<HeaderInfo> q = _all;

            if (!string.IsNullOrWhiteSpace(query))
            {
                string needle = query.Trim();
                q = q.Where(h =>
                    (h.Name?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (h.DescEn?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (h.DescZh?.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            if (!string.IsNullOrWhiteSpace(category))
                q = q.Where(h => string.Equals(h.Category, category, StringComparison.OrdinalIgnoreCase));

            if (dir.HasValue && dir.Value != Direction.Both)
                q = q.Where(h => h.Dir == dir.Value || h.Dir == Direction.Both);

            return q.OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch { return Array.Empty<HeaderInfo>(); }
    }

    /// <summary>Text to place on the clipboard when a row is clicked. Never throws.</summary>
    public static string CopyText(HeaderInfo? h)
    {
        try
        {
            if (h == null) return "";
            return string.IsNullOrEmpty(h.Example) ? h.Name : $"{h.Name}: {h.Example}";
        }
        catch { return h?.Name ?? ""; }
    }

    private static HeaderInfo H(string name, Direction dir, string cat, string en, string zh, string ex = "")
        => new() { Name = name, Dir = dir, Category = cat, DescEn = en, DescZh = zh, Example = ex };

    private static List<HeaderInfo> Build()
    {
        var l = new List<HeaderInfo>
        {
            // ── Caching ──
            H("Cache-Control", Direction.Both, "Caching", "Directives for caching in requests and responses.", "喺請求同回應入面控制快取行為嘅指令。", "no-cache, max-age=3600"),
            H("Expires", Direction.Response, "Caching", "Date/time after which the response is stale.", "回應過期嘅日期時間，過咗就當過時。", "Wed, 21 Oct 2026 07:28:00 GMT"),
            H("Age", Direction.Response, "Caching", "Time in seconds the object has been in a proxy cache.", "物件喺代理快取入面存咗幾多秒。", "3600"),
            H("Pragma", Direction.Both, "Caching", "Legacy HTTP/1.0 caching control; mostly 'no-cache'.", "舊 HTTP/1.0 快取控制，多數係 no-cache。", "no-cache"),
            H("Vary", Direction.Response, "Caching", "Which request headers vary the cached response.", "邊啲請求標頭會令快取回應唔同。", "Accept-Encoding, User-Agent"),
            H("Warning", Direction.Both, "Caching", "Extra info about possible staleness of a message.", "關於訊息可能過時嘅額外資訊。", "110 - \"Response is stale\""),

            // ── Conditional ──
            H("ETag", Direction.Response, "Conditional", "Opaque validator identifying a specific resource version.", "識別某個資源版本嘅驗證標籤。", "\"33a64df551\""),
            H("If-Match", Direction.Request, "Conditional", "Apply the request only if the ETag matches.", "只有 ETag 相符先執行呢個請求。", "\"33a64df551\""),
            H("If-None-Match", Direction.Request, "Conditional", "Apply only if the ETag does NOT match (cache revalidate).", "只有 ETag 唔相符先執行（用嚟重新驗證快取）。", "\"33a64df551\""),
            H("If-Modified-Since", Direction.Request, "Conditional", "Return the resource only if changed since this date.", "資源喺呢個日期之後有改先傳返。", "Wed, 21 Oct 2026 07:28:00 GMT"),
            H("If-Unmodified-Since", Direction.Request, "Conditional", "Proceed only if the resource is unchanged since this date.", "資源自呢個日期未改過先執行。", "Wed, 21 Oct 2026 07:28:00 GMT"),
            H("Last-Modified", Direction.Response, "Conditional", "Date/time the resource was last modified.", "資源最後一次修改嘅日期時間。", "Wed, 21 Oct 2026 07:28:00 GMT"),

            // ── Content ──
            H("Content-Type", Direction.Both, "Content", "The media type of the body (MIME type).", "訊息主體嘅媒體類型（MIME）。", "application/json; charset=utf-8"),
            H("Content-Length", Direction.Both, "Content", "Size of the body in bytes.", "訊息主體嘅位元組大小。", "348"),
            H("Content-Encoding", Direction.Both, "Content", "Compression applied to the body.", "主體用咗嘅壓縮方式。", "gzip"),
            H("Content-Language", Direction.Both, "Content", "Natural language(s) of the content.", "內容嘅自然語言。", "zh-HK, en"),
            H("Content-Disposition", Direction.Response, "Content", "Display inline or as a downloadable attachment.", "內容係內嵌定當附件下載。", "attachment; filename=\"report.pdf\""),
            H("Content-Location", Direction.Response, "Content", "Alternate location for the returned data.", "回傳資料嘅另一個位置。", "/documents/report.pdf"),
            H("Content-Range", Direction.Response, "Range", "Where a partial body fits in the full resource.", "部分主體喺整個資源入面嘅位置。", "bytes 200-1000/67589"),
            H("MIME-Version", Direction.Both, "Content", "MIME protocol version used in the message.", "訊息用嘅 MIME 協定版本。", "1.0"),

            // ── Content negotiation ──
            H("Accept", Direction.Request, "Content", "Media types the client can process.", "客戶端可以處理嘅媒體類型。", "text/html, application/json"),
            H("Accept-Charset", Direction.Request, "Content", "Character sets the client accepts.", "客戶端接受嘅字元集。", "utf-8"),
            H("Accept-Encoding", Direction.Request, "Content", "Content encodings (compression) the client accepts.", "客戶端接受嘅內容編碼（壓縮）。", "gzip, deflate, br"),
            H("Accept-Language", Direction.Request, "Content", "Preferred natural languages for the response.", "回應偏好嘅自然語言。", "zh-HK, en;q=0.8"),

            // ── Range ──
            H("Accept-Ranges", Direction.Response, "Range", "Indicates the server supports range requests.", "話畀你知伺服器支援分段請求。", "bytes"),
            H("Range", Direction.Request, "Range", "Request only part of a resource (byte range).", "只請求資源嘅一部分（位元組範圍）。", "bytes=0-1023"),
            H("If-Range", Direction.Request, "Range", "Range request valid only if the validator matches.", "驗證器相符先當呢個範圍請求有效。", "\"33a64df551\""),

            // ── Authentication ──
            H("Authorization", Direction.Request, "Auth", "Credentials to authenticate the client.", "用嚟驗證客戶端身份嘅憑證。", "Bearer eyJhbGci..."),
            H("WWW-Authenticate", Direction.Response, "Auth", "Authentication scheme the server requires.", "伺服器要求嘅驗證方式。", "Bearer realm=\"api\""),
            H("Proxy-Authenticate", Direction.Response, "Proxy", "Authentication scheme required by a proxy.", "代理要求嘅驗證方式。", "Basic realm=\"proxy\""),
            H("Proxy-Authorization", Direction.Request, "Proxy", "Credentials to authenticate with a proxy.", "同代理驗證用嘅憑證。", "Basic aGVsbG8="),

            // ── Cookies ──
            H("Cookie", Direction.Request, "Cookies", "Cookies previously set by the server.", "之前伺服器設定畀你嘅 cookie。", "sessionId=abc123; theme=dark"),
            H("Set-Cookie", Direction.Response, "Cookies", "Instructs the client to store a cookie.", "叫客戶端儲存一個 cookie。", "id=a3f; HttpOnly; Secure; SameSite=Lax"),

            // ── CORS ──
            H("Origin", Direction.Request, "CORS", "Origin (scheme+host+port) initiating the request.", "發起請求嘅來源（協定＋主機＋埠）。", "https://example.com"),
            H("Access-Control-Allow-Origin", Direction.Response, "CORS", "Which origins may access the resource.", "邊啲來源可以存取呢個資源。", "*"),
            H("Access-Control-Allow-Methods", Direction.Response, "CORS", "HTTP methods allowed for cross-origin requests.", "跨來源請求准用嘅 HTTP 方法。", "GET, POST, PUT, DELETE"),
            H("Access-Control-Allow-Headers", Direction.Response, "CORS", "Request headers allowed in cross-origin requests.", "跨來源請求准用嘅請求標頭。", "Content-Type, Authorization"),
            H("Access-Control-Allow-Credentials", Direction.Response, "CORS", "Whether cookies/credentials may be sent cross-origin.", "跨來源時可唔可以帶 cookie／憑證。", "true"),
            H("Access-Control-Expose-Headers", Direction.Response, "CORS", "Response headers scripts may read cross-origin.", "腳本跨來源可以讀嘅回應標頭。", "X-Request-Id"),
            H("Access-Control-Max-Age", Direction.Response, "CORS", "How long a preflight result may be cached (seconds).", "預檢結果可以快取幾耐（秒）。", "600"),
            H("Access-Control-Request-Method", Direction.Request, "CORS", "Method the actual request will use (preflight).", "實際請求會用嘅方法（預檢）。", "POST"),
            H("Access-Control-Request-Headers", Direction.Request, "CORS", "Headers the actual request will send (preflight).", "實際請求會送嘅標頭（預檢）。", "Content-Type"),

            // ── Security ──
            H("Strict-Transport-Security", Direction.Response, "Security", "Force HTTPS for future requests (HSTS).", "強制之後嘅請求用 HTTPS（HSTS）。", "max-age=31536000; includeSubDomains"),
            H("Content-Security-Policy", Direction.Response, "Security", "Controls which resources the page may load.", "控制頁面可以載入邊啲資源。", "default-src 'self'"),
            H("Content-Security-Policy-Report-Only", Direction.Response, "Security", "Report CSP violations without enforcing them.", "只回報 CSP 違規但唔強制執行。", "default-src 'self'; report-uri /csp"),
            H("X-Content-Type-Options", Direction.Response, "Security", "Disables MIME-type sniffing by the browser.", "禁止瀏覽器亂猜 MIME 類型。", "nosniff"),
            H("X-Frame-Options", Direction.Response, "Security", "Controls whether the page can be framed (clickjacking).", "控制頁面可唔可以被 iframe 內嵌（防點擊劫持）。", "DENY"),
            H("X-XSS-Protection", Direction.Response, "Security", "Legacy browser XSS filter control.", "舊瀏覽器 XSS 過濾器控制。", "1; mode=block"),
            H("Referrer-Policy", Direction.Response, "Security", "How much referrer info to send with requests.", "請求時送幾多來源網址資訊。", "no-referrer-when-downgrade"),
            H("Permissions-Policy", Direction.Response, "Security", "Enable/disable browser features per origin.", "按來源開關瀏覽器功能。", "geolocation=(), camera=()"),
            H("Cross-Origin-Opener-Policy", Direction.Response, "Security", "Isolates the browsing context from other origins.", "將瀏覽情境同其他來源隔離。", "same-origin"),
            H("Cross-Origin-Embedder-Policy", Direction.Response, "Security", "Requires resources to opt into being embedded.", "要求資源明確允許被嵌入。", "require-corp"),
            H("Cross-Origin-Resource-Policy", Direction.Response, "Security", "Limits which origins may embed this resource.", "限制邊啲來源可以嵌入呢個資源。", "same-site"),
            H("Expect-CT", Direction.Response, "Security", "Enforce Certificate Transparency requirements.", "強制執行憑證透明度要求。", "max-age=86400, enforce"),
            H("Clear-Site-Data", Direction.Response, "Security", "Instructs the browser to clear stored data.", "叫瀏覽器清除已儲存嘅資料。", "\"cache\", \"cookies\", \"storage\""),

            // ── Connection ──
            H("Connection", Direction.Both, "Connection", "Control options for the current connection.", "控制當前連線嘅選項。", "keep-alive"),
            H("Keep-Alive", Direction.Both, "Connection", "Parameters for a persistent connection.", "持久連線嘅參數。", "timeout=5, max=1000"),
            H("Upgrade", Direction.Both, "Connection", "Ask to switch protocols (e.g. to WebSocket).", "要求轉換協定（例如轉去 WebSocket）。", "websocket"),
            H("Transfer-Encoding", Direction.Both, "Connection", "Encoding used to transfer the body (e.g. chunked).", "傳送主體用嘅編碼（例如 chunked）。", "chunked"),
            H("TE", Direction.Request, "Connection", "Transfer encodings the client will accept.", "客戶端接受嘅傳送編碼。", "trailers, deflate"),
            H("Trailer", Direction.Both, "Connection", "Names header fields sent after a chunked body.", "分塊主體之後會送嘅標頭欄位名。", "Expires"),
            H("Expect", Direction.Request, "Connection", "Client expects certain server behaviour first.", "客戶端預期伺服器先做某啲行為。", "100-continue"),

            // ── Proxy / routing ──
            H("Host", Direction.Request, "Proxy", "Target host and port of the request.", "請求嘅目標主機同埠。", "example.com:443"),
            H("Via", Direction.Both, "Proxy", "Intermediate proxies the message passed through.", "訊息經過嘅中間代理。", "1.1 vegur"),
            H("Forwarded", Direction.Request, "Proxy", "Client/proxy info disclosed by proxies.", "代理披露嘅客戶端／代理資訊。", "for=192.0.2.60; proto=https"),
            H("X-Forwarded-For", Direction.Request, "Proxy", "Originating client IP through proxies.", "經過代理嘅原始客戶端 IP。", "203.0.113.195"),
            H("X-Forwarded-Host", Direction.Request, "Proxy", "Original Host requested by the client.", "客戶端原本請求嘅 Host。", "example.com"),
            H("X-Forwarded-Proto", Direction.Request, "Proxy", "Original protocol (http/https) used by the client.", "客戶端原本用嘅協定（http／https）。", "https"),
            H("Max-Forwards", Direction.Request, "Proxy", "Limits proxy hops for TRACE/OPTIONS.", "限制 TRACE／OPTIONS 嘅代理跳數。", "10"),

            // ── Request context ──
            H("User-Agent", Direction.Request, "Request", "Identifies the client software making the request.", "識別發出請求嘅客戶端軟件。", "Mozilla/5.0 (Windows NT 11.0)"),
            H("Referer", Direction.Request, "Request", "URL of the page that linked to this request.", "連過嚟呢個請求嘅頁面網址。", "https://example.com/page"),
            H("From", Direction.Request, "Request", "Email address of the human controlling the agent.", "操作呢個用戶端嘅人嘅電郵地址。", "webmaster@example.com"),
            H("Date", Direction.Both, "Connection", "Date and time the message was originated.", "訊息產生嘅日期同時間。", "Wed, 21 Oct 2026 07:28:00 GMT"),
            H("DNT", Direction.Request, "Request", "Do Not Track preference of the user.", "使用者嘅唔追蹤（Do Not Track）偏好。", "1"),
            H("Save-Data", Direction.Request, "Request", "Client signals a preference for reduced data use.", "客戶端表示想慳流量。", "on"),

            // ── Response / status ──
            H("Location", Direction.Response, "Response", "Redirect target or URL of a newly created resource.", "重新導向目標，或新建資源嘅網址。", "https://example.com/new"),
            H("Server", Direction.Response, "Response", "Software handling the request on the server.", "伺服器處理請求嘅軟件。", "nginx/1.25.0"),
            H("Retry-After", Direction.Response, "Response", "How long to wait before retrying (503/429).", "重試前要等幾耐（503／429）。", "120"),
            H("Allow", Direction.Response, "Response", "HTTP methods supported by the resource.", "資源支援嘅 HTTP 方法。", "GET, POST, HEAD"),
            H("Accept-Patch", Direction.Response, "Response", "Media types accepted by a PATCH request.", "PATCH 請求接受嘅媒體類型。", "application/json-patch+json"),
            H("Alt-Svc", Direction.Response, "Response", "Advertises alternative services (e.g. HTTP/3).", "宣告替代服務（例如 HTTP/3）。", "h3=\":443\"; ma=2592000"),
            H("Link", Direction.Response, "Response", "Typed relationships to other resources.", "同其他資源嘅類型化關係。", "</style.css>; rel=preload"),
            H("Server-Timing", Direction.Response, "Response", "Server-side performance metrics for the response.", "回應嘅伺服器端效能量度。", "db;dur=53, app;dur=47.2"),

            // ── Misc / conditional behaviour ──
            H("Upgrade-Insecure-Requests", Direction.Request, "Security", "Client prefers upgraded (HTTPS) resources.", "客戶端偏好升級（HTTPS）嘅資源。", "1"),
            H("X-Requested-With", Direction.Request, "Request", "Marks AJAX/XHR requests (de-facto convention).", "標示 AJAX／XHR 請求（慣例）。", "XMLHttpRequest"),
            H("X-Powered-By", Direction.Response, "Response", "Technology powering the server (often removed).", "驅動伺服器嘅技術（通常會移除）。", "Express"),
            H("X-Request-Id", Direction.Both, "Response", "Correlation id for tracing a single request.", "追蹤單一請求嘅關聯 id。", "f47ac10b-58cc-4372"),
        };
        return l;
    }
}
