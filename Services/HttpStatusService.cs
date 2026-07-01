using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// HTTP 狀態碼參考 · HTTP status code reference — an embedded, offline catalogue of the standard
/// HTTP status codes (1xx–5xx), each with its reason phrase and a one-line English + Cantonese
/// description. Pure managed data; never throws. No network, no process launch.
/// </summary>
public static class HttpStatusService
{
    /// <summary>One HTTP status code entry (immutable).</summary>
    public sealed class HttpStatus
    {
        public int Code { get; }
        public string Name { get; }      // reason phrase, e.g. "Not Found"
        public string DescEn { get; }
        public string DescZh { get; }

        public HttpStatus(int code, string name, string en, string zh)
        {
            Code = code;
            Name = name ?? string.Empty;
            DescEn = en ?? string.Empty;
            DescZh = zh ?? string.Empty;
        }

        /// <summary>Leading digit → class (1..5); 0 when out of range.</summary>
        public int Category => (Code >= 100 && Code <= 599) ? Code / 100 : 0;
    }

    private static readonly List<HttpStatus> _all = Build();

    /// <summary>All embedded status codes, ascending by code. Never null.</summary>
    public static IReadOnlyList<HttpStatus> All => _all;

    /// <summary>
    /// Filter by free text (matches code number or any text, case-insensitive) and category
    /// (0 = all classes, otherwise 1..5). Always returns a fresh list — never throws.
    /// </summary>
    public static List<HttpStatus> Filter(string? query, int category)
    {
        try
        {
            IEnumerable<HttpStatus> q = _all;
            if (category >= 1 && category <= 5)
                q = q.Where(s => s.Category == category);

            string term = (query ?? string.Empty).Trim();
            if (term.Length > 0)
            {
                q = q.Where(s =>
                    s.Code.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.DescEn.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    s.DescZh.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            return q.ToList();
        }
        catch
        {
            return new List<HttpStatus>();
        }
    }

    /// <summary>Exact code lookup; null when not found. Never throws.</summary>
    public static HttpStatus? Lookup(int code)
    {
        try { return _all.FirstOrDefault(s => s.Code == code); }
        catch { return null; }
    }

    private static List<HttpStatus> Build()
    {
        var l = new List<HttpStatus>
        {
            // ── 1xx Informational · 資訊 ──
            new(100, "Continue", "Request headers received; the client should continue sending the body.", "已收到請求標頭，客戶端可以繼續傳送主體。"),
            new(101, "Switching Protocols", "Server agrees to switch protocols as the client requested (e.g. to WebSocket).", "伺服器同意按客戶端要求轉換協定（例如轉去 WebSocket）。"),
            new(102, "Processing", "Server has accepted the request but has not finished processing it (WebDAV).", "伺服器已收到請求但仲未處理完（WebDAV）。"),
            new(103, "Early Hints", "Preliminary headers sent so the client can start preloading resources.", "先傳部分標頭，等客戶端可以預先載入資源。"),

            // ── 2xx Success · 成功 ──
            new(200, "OK", "The request succeeded; the response carries the requested content.", "請求成功，回應帶住你要嘅內容。"),
            new(201, "Created", "The request succeeded and a new resource was created.", "請求成功，並且新開咗一個資源。"),
            new(202, "Accepted", "The request was accepted for processing, but is not yet complete.", "請求已收到會處理，但仲未做完。"),
            new(203, "Non-Authoritative Information", "Returned metadata came from a copy, not the origin server.", "回傳嘅資料嚟自副本，唔係源伺服器。"),
            new(204, "No Content", "The request succeeded but there is no content to return.", "請求成功，但冇內容返俾你。"),
            new(205, "Reset Content", "Success; the client should reset the document view that sent the request.", "成功，客戶端應該重設個表單／畫面。"),
            new(206, "Partial Content", "Only part of the resource is returned, as asked by a Range request.", "只回傳資源嘅一部分（回應 Range 範圍請求）。"),
            new(207, "Multi-Status", "Conveys multiple independent status codes for a WebDAV request.", "一次過回傳多個狀態（WebDAV）。"),
            new(208, "Already Reported", "Members of a WebDAV binding were already enumerated earlier.", "WebDAV 綁定成員之前已經列過。"),
            new(226, "IM Used", "The response is the result of instance-manipulations applied to the resource.", "回應係對資源做咗實例操作之後嘅結果。"),

            // ── 3xx Redirection · 重新導向 ──
            new(300, "Multiple Choices", "Several responses are available; the client may choose one.", "有幾個選擇可以揀，由客戶端決定。"),
            new(301, "Moved Permanently", "The resource has permanently moved to a new URL.", "資源已經永久搬去新網址。"),
            new(302, "Found", "The resource is temporarily at a different URL.", "資源暫時喺另一個網址。"),
            new(303, "See Other", "Follow up with a GET request to a different URL.", "去另一個網址用 GET 攞返結果。"),
            new(304, "Not Modified", "The cached copy is still fresh; no need to resend the body.", "快取嘅版本仲新，唔使再傳主體。"),
            new(305, "Use Proxy", "The resource must be accessed through a proxy (deprecated).", "要經指定代理伺服器先攞到（已棄用）。"),
            new(307, "Temporary Redirect", "Temporary redirect that keeps the original HTTP method.", "暫時導向，而且保持原本嘅 HTTP 方法。"),
            new(308, "Permanent Redirect", "Permanent redirect that keeps the original HTTP method.", "永久導向，而且保持原本嘅 HTTP 方法。"),

            // ── 4xx Client Error · 客戶端錯誤 ──
            new(400, "Bad Request", "The server cannot process the request due to a client error.", "請求有問題，伺服器處理唔到。"),
            new(401, "Unauthorized", "Authentication is required and has failed or not been provided.", "要先登入驗證，而家未通過。"),
            new(402, "Payment Required", "Reserved for future use; sometimes used for paid APIs.", "預留俾將來用，有時用喺付費 API。"),
            new(403, "Forbidden", "The server understood the request but refuses to authorize it.", "伺服器明白請求，但唔俾你做。"),
            new(404, "Not Found", "The requested resource could not be found on the server.", "搵唔到你要嘅資源。"),
            new(405, "Method Not Allowed", "The HTTP method is not supported for this resource.", "呢個資源唔支援你用嘅 HTTP 方法。"),
            new(406, "Not Acceptable", "No response matches the client's Accept headers.", "冇符合你 Accept 條件嘅回應。"),
            new(407, "Proxy Authentication Required", "The client must authenticate with a proxy first.", "要先向代理伺服器驗證。"),
            new(408, "Request Timeout", "The server timed out waiting for the request.", "等你個請求等到逾時。"),
            new(409, "Conflict", "The request conflicts with the current state of the resource.", "同資源目前嘅狀態有衝突。"),
            new(410, "Gone", "The resource is permanently gone and will not return.", "資源已經永久消失，唔會返嚟。"),
            new(411, "Length Required", "The server requires a Content-Length header.", "伺服器要求要有 Content-Length 標頭。"),
            new(412, "Precondition Failed", "A precondition in the request headers was not met.", "請求標頭裏面嘅前置條件唔符合。"),
            new(413, "Payload Too Large", "The request body is larger than the server will accept.", "請求主體太大，伺服器收唔落。"),
            new(414, "URI Too Long", "The request URL is longer than the server will process.", "個網址太長，伺服器處理唔到。"),
            new(415, "Unsupported Media Type", "The request's media type is not supported.", "唔支援呢種媒體類型。"),
            new(416, "Range Not Satisfiable", "The requested Range cannot be served.", "你要嘅 Range 範圍畀唔到。"),
            new(417, "Expectation Failed", "The server cannot meet the Expect request header.", "滿足唔到 Expect 標頭嘅要求。"),
            new(418, "I'm a teapot", "An April Fools' joke code — the server refuses to brew coffee.", "愚人節玩笑碼 — 一個茶壺沖唔到咖啡。"),
            new(421, "Misdirected Request", "The request was routed to a server that cannot respond to it.", "請求去錯咗一部應付唔到嘅伺服器。"),
            new(422, "Unprocessable Entity", "The request is well-formed but semantically invalid.", "格式啱但語意上處理唔到。"),
            new(423, "Locked", "The resource being accessed is locked (WebDAV).", "要攞嘅資源被鎖住咗（WebDAV）。"),
            new(424, "Failed Dependency", "The request failed because a dependent request failed.", "因為所依賴嘅請求失敗，所以連埋失敗。"),
            new(425, "Too Early", "The server is unwilling to process a possibly-replayed request.", "伺服器唔想處理可能被重放嘅請求。"),
            new(426, "Upgrade Required", "The client should switch to a different protocol.", "客戶端要升級／轉用另一個協定。"),
            new(428, "Precondition Required", "The server requires the request to be conditional.", "伺服器要求請求要帶前置條件。"),
            new(429, "Too Many Requests", "The client has sent too many requests in a given time.", "短時間內請求太多次（限速）。"),
            new(431, "Request Header Fields Too Large", "The header fields are too large to process.", "標頭欄位太大，處理唔到。"),
            new(451, "Unavailable For Legal Reasons", "The resource is blocked for legal reasons.", "因法律原因唔提供呢個資源。"),

            // ── 5xx Server Error · 伺服器錯誤 ──
            new(500, "Internal Server Error", "A generic error — the server hit an unexpected condition.", "伺服器出咗個未預料到嘅錯誤。"),
            new(501, "Not Implemented", "The server does not support the functionality required.", "伺服器未實作呢個功能。"),
            new(502, "Bad Gateway", "An upstream server returned an invalid response.", "上游伺服器回咗個無效回應。"),
            new(503, "Service Unavailable", "The server is overloaded or down for maintenance.", "伺服器過載或者維護緊，暫時用唔到。"),
            new(504, "Gateway Timeout", "An upstream server did not respond in time.", "上游伺服器遲遲唔回應（逾時）。"),
            new(505, "HTTP Version Not Supported", "The HTTP version used is not supported.", "唔支援你用嘅 HTTP 版本。"),
            new(506, "Variant Also Negotiates", "A content-negotiation configuration error occurred.", "內容協商設定出錯（循環協商）。"),
            new(507, "Insufficient Storage", "The server cannot store what is needed to finish (WebDAV).", "伺服器儲存空間不足，做唔完（WebDAV）。"),
            new(508, "Loop Detected", "The server detected an infinite loop while processing (WebDAV).", "處理途中偵測到無限迴圈（WebDAV）。"),
            new(510, "Not Extended", "Further extensions to the request are required.", "請求要加額外擴充先處理到。"),
            new(511, "Network Authentication Required", "The client must authenticate to gain network access.", "要先做網絡驗證先上到網（如 Wi-Fi 登入頁）。"),
        };
        return l.OrderBy(s => s.Code).ToList();
    }
}
