using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WinForge.Services;

namespace WinForge.Services;

/// <summary>
/// HTTP 安全標頭計分 · HTTP security-header scorecard. Pure managed, fully local, never throws.
/// Parses a raw "Name: Value" response-header block (case-insensitive) and grades the presence
/// and quality of the common browser-security headers, flags version-disclosure headers, and
/// produces a weighted letter grade (A+..F) plus a bilingual copyable report.
/// </summary>
public static class HeaderScoreService
{
    /// <summary>One graded row for the results ListView (classic {Binding}, no x:Bind).</summary>
    public sealed class Row
    {
        public string Header { get; set; } = "";
        public string Status { get; set; } = "";      // Present / Missing / Risky (localized)
        public string Value { get; set; } = "";       // the parsed value (trimmed) or "—"
        public string Note { get; set; } = "";        // quality note (localized)
        public string Advice { get; set; } = "";      // recommendation (localized)
        public string BadgeHex { get; set; } = "#8A8A8A"; // status colour for the badge
    }

    /// <summary>Full analysis result.</summary>
    public sealed class Result
    {
        public string Grade { get; set; } = "F";
        public string GradeHex { get; set; } = "#D13438";
        public int Score { get; set; }        // 0..100
        public int MaxScore { get; set; } = 100;
        public string Summary { get; set; } = "";
        public List<Row> Rows { get; } = new();
        public bool ParsedAny { get; set; }
    }

    private static string P(string en, string zh)
    {
        try { return Loc.I.Pick(en, zh); } catch { return en; }
    }

    // status badge colours
    private const string Green = "#2EA043";
    private const string Amber = "#D9A400";
    private const string Red = "#D13438";
    private const string Grey = "#8A8A8A";

    private sealed class Spec
    {
        public string Name = "";
        public double Weight;
        // returns (score 0..1, note, advice) given the raw value (null = missing)
        public Func<string?, (double, string, string)> Grade = _ => (0, "", "");
    }

    /// <summary>Parse a raw header block into a case-insensitive last-wins map. Never throws.</summary>
    public static Dictionary<string, string> Parse(string? raw)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return map;
        try
        {
            foreach (var lineRaw in raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = lineRaw.Trim();
                if (line.Length == 0) continue;
                // Skip an HTTP status line like "HTTP/2 200" or "HTTP/1.1 200 OK"
                if (line.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) && !line.Contains(": ")) continue;
                int idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var name = line.Substring(0, idx).Trim();
                var value = line.Substring(idx + 1).Trim();
                if (name.Length == 0) continue;
                map[name] = value; // last wins
            }
        }
        catch { /* never throw */ }
        return map;
    }

    /// <summary>Analyse a raw header block and produce a graded scorecard. Never throws.</summary>
    public static Result Analyze(string? raw)
    {
        var result = new Result();
        try
        {
            var map = Parse(raw);
            result.ParsedAny = map.Count > 0;

            var specs = BuildSpecs();
            double got = 0, max = 0;

            foreach (var s in specs)
            {
                max += s.Weight;
                map.TryGetValue(s.Name, out var val);
                bool present = val != null;
                var (frac, note, advice) = s.Grade(val);
                if (frac < 0) frac = 0; if (frac > 1) frac = 1;
                got += s.Weight * frac;

                string status;
                string hex;
                if (!present) { status = P("Missing", "缺少"); hex = Red; }
                else if (frac >= 0.999) { status = P("Present", "已設定"); hex = Green; }
                else { status = P("Weak", "偏弱"); hex = Amber; }

                result.Rows.Add(new Row
                {
                    Header = s.Name,
                    Status = status,
                    Value = present ? Trunc(val!) : "—",
                    Note = note,
                    Advice = advice,
                    BadgeHex = hex
                });
            }

            // Risky / disclosure headers (present = bad)
            AddRisky(result, map, "Server",
                P("Reveals server software; a version aids targeted attacks.",
                  "洩露伺服器軟件；版本號會方便針對式攻擊。"),
                P("Remove or blank the Server header (e.g. server_tokens off).",
                  "移除或清空 Server 標頭（例如 server_tokens off）。"),
                ref got, ref max);
            AddRisky(result, map, "X-Powered-By",
                P("Discloses the backend framework/version — unnecessary exposure.",
                  "透露後端框架／版本 — 無謂咁暴露。"),
                P("Strip X-Powered-By at the app or proxy layer.",
                  "喺應用或反向代理層移除 X-Powered-By。"),
                ref got, ref max);
            AddRisky(result, map, "X-AspNet-Version",
                P("Leaks the exact ASP.NET runtime version.",
                  "洩露 ASP.NET 執行階段的確切版本。"),
                P("Set enableVersionHeader=false.",
                  "設定 enableVersionHeader=false。"),
                ref got, ref max);
            AddRisky(result, map, "X-AspNetMvc-Version",
                P("Leaks the ASP.NET MVC version.",
                  "洩露 ASP.NET MVC 版本。"),
                P("Disable the MvcHandler version header.",
                  "停用 MvcHandler 版本標頭。"),
                ref got, ref max);

            if (max <= 0) max = 1;
            int pct = (int)Math.Round(got / max * 100.0);
            if (pct < 0) pct = 0; if (pct > 100) pct = 100;
            result.Score = pct;
            result.MaxScore = 100;

            var (grade, gradeHex) = Letter(pct);
            result.Grade = grade;
            result.GradeHex = gradeHex;

            if (!result.ParsedAny)
                result.Summary = P("No headers parsed. Paste a raw response header block (Name: Value lines).",
                                    "未解析到任何標頭。請貼上原始回應標頭（Name: Value 每行一條）。");
            else
                result.Summary = P($"Score {pct}/100 · grade {grade}. Higher is safer.",
                                    $"分數 {pct}/100 · 等級 {grade}。分數越高越安全。");
        }
        catch (Exception ex)
        {
            // never throw — surface as a benign result
            result.Grade = "?";
            result.GradeHex = Grey;
            result.Summary = P("Could not analyze the input.", "無法分析輸入內容。") + " (" + ex.GetType().Name + ")";
        }
        return result;
    }

    private static void AddRisky(Result r, Dictionary<string, string> map, string name, string note, string advice, ref double got, ref double max)
    {
        // Risky headers carry a small weight; absence is good (full marks), presence loses it.
        const double w = 4.0;
        max += w;
        bool present = map.TryGetValue(name, out var val);
        if (!present) { got += w; return; } // good: header absent

        r.Rows.Add(new Row
        {
            Header = name,
            Status = P("Risky", "有風險"),
            Value = Trunc(val ?? ""),
            Note = note,
            Advice = advice,
            BadgeHex = Amber
        });
        // present => no marks added
    }

    private static List<Spec> BuildSpecs()
    {
        var list = new List<Spec>();

        list.Add(new Spec
        {
            Name = "Strict-Transport-Security",
            Weight = 20,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — browsers may connect over plain HTTP.", "未設定 — 瀏覽器可能用純 HTTP 連線。"),
                    P("Add: Strict-Transport-Security: max-age=63072000; includeSubDomains; preload",
                      "加入：Strict-Transport-Security: max-age=63072000; includeSubDomains; preload"));
                long age = MaxAge(v);
                bool sub = Has(v, "includesubdomains");
                bool pre = Has(v, "preload");
                if (age >= 31536000 && sub && pre)
                    return (1.0, P("Strong (1y+, subdomains, preload).", "強（1 年以上、含子網域、preload）。"),
                        P("Good. Keep it and consider HSTS preload list submission.",
                          "良好。可考慮提交至 HSTS preload 清單。"));
                if (age >= 31536000 && sub)
                    return (0.85, P("Good, but no preload.", "良好，但未 preload。"),
                        P("Add ; preload and submit to the HSTS preload list.",
                          "加上 ; preload 並提交至 HSTS preload 清單。"));
                if (age >= 15768000)
                    return (0.6, P("Present but short-ish / missing directives.", "已設定，但期限偏短／欠缺指令。"),
                        P("Use max-age≥31536000; includeSubDomains; preload.",
                          "使用 max-age≥31536000; includeSubDomains; preload。"));
                return (0.3, P("Very short max-age weakens HSTS.", "max-age 太短，削弱 HSTS 效果。"),
                    P("Raise max-age to at least 1 year (31536000).",
                      "將 max-age 提高至至少 1 年（31536000）。"));
            }
        });

        list.Add(new Spec
        {
            Name = "Content-Security-Policy",
            Weight = 22,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — no defence-in-depth against XSS/injection.", "未設定 — 缺乏對 XSS／注入嘅縱深防禦。"),
                    P("Define a CSP starting from default-src 'self'; then tighten per resource type.",
                      "由 default-src 'self' 開始定義 CSP，再按資源類型收緊。"));
                var low = v.ToLowerInvariant();
                bool wildcard = low.Contains("default-src *") || low.Contains("script-src *");
                bool unsafeInline = low.Contains("'unsafe-inline'");
                bool unsafeEval = low.Contains("'unsafe-eval'");
                bool hasDefault = low.Contains("default-src");
                if (wildcard)
                    return (0.35, P("Present but uses wildcard sources.", "已設定，但用咗萬用字元來源。"),
                        P("Avoid '*'; enumerate trusted origins or use 'self'.",
                          "避免用 '*'；列明可信來源或用 'self'。"));
                if (unsafeInline || unsafeEval)
                    return (0.6, P("Present but allows 'unsafe-inline'/'unsafe-eval'.", "已設定，但容許 'unsafe-inline'／'unsafe-eval'。"),
                        P("Remove unsafe-inline/eval; use nonces or hashes for scripts.",
                          "移除 unsafe-inline／eval；用 nonce 或 hash 管理腳本。"));
                if (hasDefault)
                    return (1.0, P("Solid policy with a default-src baseline.", "有 default-src 基線嘅穩健政策。"),
                        P("Good. Keep testing with report-only before tightening further.",
                          "良好。收緊前先用 report-only 測試。"));
                return (0.75, P("Present; consider adding a default-src fallback.", "已設定；可考慮加 default-src 後備。"),
                    P("Add default-src 'self' as a baseline fallback.",
                      "加入 default-src 'self' 作為基線後備。"));
            }
        });

        list.Add(new Spec
        {
            Name = "X-Content-Type-Options",
            Weight = 10,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — MIME sniffing risk.", "未設定 — 有 MIME 嗅探風險。"),
                    P("Add: X-Content-Type-Options: nosniff", "加入：X-Content-Type-Options: nosniff"));
                if (v.Trim().Equals("nosniff", StringComparison.OrdinalIgnoreCase))
                    return (1.0, P("nosniff set correctly.", "已正確設定 nosniff。"),
                        P("Good — no action needed.", "良好 — 無需動作。"));
                return (0.4, P("Unexpected value; only 'nosniff' is valid.", "值不正確；只有 'nosniff' 有效。"),
                    P("Set the value to exactly nosniff.", "將值設為 nosniff。"));
            }
        });

        list.Add(new Spec
        {
            Name = "X-Frame-Options",
            Weight = 10,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — clickjacking risk (unless CSP frame-ancestors covers it).",
                      "未設定 — 有點擊劫持風險（除非 CSP frame-ancestors 已涵蓋）。"),
                    P("Add X-Frame-Options: DENY (or SAMEORIGIN), or use CSP frame-ancestors.",
                      "加入 X-Frame-Options: DENY（或 SAMEORIGIN），或用 CSP frame-ancestors。"));
                var t = v.Trim();
                if (t.Equals("DENY", StringComparison.OrdinalIgnoreCase))
                    return (1.0, P("DENY — strongest anti-framing.", "DENY — 最強防嵌套。"),
                        P("Good.", "良好。"));
                if (t.Equals("SAMEORIGIN", StringComparison.OrdinalIgnoreCase))
                    return (0.9, P("SAMEORIGIN — allows same-origin framing.", "SAMEORIGIN — 容許同源嵌套。"),
                        P("Fine; prefer DENY if you never frame yourself.",
                          "可以；若不需自我嵌套，宜用 DENY。"));
                return (0.4, P("Deprecated ALLOW-FROM or unknown value.", "已棄用嘅 ALLOW-FROM 或未知值。"),
                    P("Use DENY/SAMEORIGIN, and CSP frame-ancestors for allow-lists.",
                      "用 DENY／SAMEORIGIN，允許清單改用 CSP frame-ancestors。"));
            }
        });

        list.Add(new Spec
        {
            Name = "Referrer-Policy",
            Weight = 8,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — full URLs may leak to other origins.", "未設定 — 完整網址可能洩露到其他來源。"),
                    P("Add Referrer-Policy: strict-origin-when-cross-origin (or no-referrer).",
                      "加入 Referrer-Policy: strict-origin-when-cross-origin（或 no-referrer）。"));
                var low = v.ToLowerInvariant();
                if (low.Contains("no-referrer") || low.Contains("strict-origin"))
                    return (1.0, P("Privacy-preserving policy.", "保護私隱嘅政策。"),
                        P("Good.", "良好。"));
                if (low.Contains("origin") || low.Contains("same-origin"))
                    return (0.75, P("Reasonable, but could be stricter.", "尚可，但可再收緊。"),
                        P("Prefer strict-origin-when-cross-origin.",
                          "宜用 strict-origin-when-cross-origin。"));
                if (low.Contains("unsafe-url"))
                    return (0.2, P("unsafe-url leaks full referrer — avoid.", "unsafe-url 會洩露完整 referrer — 應避免。"),
                        P("Switch to strict-origin-when-cross-origin.",
                          "改用 strict-origin-when-cross-origin。"));
                return (0.6, P("Present; verify it fits your privacy needs.", "已設定；確認符合私隱需要。"),
                    P("Consider strict-origin-when-cross-origin.",
                      "可考慮 strict-origin-when-cross-origin。"));
            }
        });

        list.Add(new Spec
        {
            Name = "Permissions-Policy",
            Weight = 8,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — powerful features aren't restricted.", "未設定 — 未限制強力功能。"),
                    P("Add Permissions-Policy to disable unused APIs, e.g. geolocation=(), camera=(), microphone=().",
                      "加入 Permissions-Policy 停用未用嘅 API，例如 geolocation=(), camera=(), microphone=()。"));
                return (1.0, P("Present — feature access is being controlled.", "已設定 — 有控制功能存取。"),
                    P("Good. Audit that every sensitive feature is explicitly restricted.",
                      "良好。檢查每個敏感功能都已明確限制。"));
            }
        });

        list.Add(new Spec
        {
            Name = "Cross-Origin-Opener-Policy",
            Weight = 6,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — no process isolation from cross-origin openers.", "未設定 — 無法與跨來源開啟者做程序隔離。"),
                    P("Add Cross-Origin-Opener-Policy: same-origin.",
                      "加入 Cross-Origin-Opener-Policy: same-origin。"));
                var low = v.ToLowerInvariant();
                if (low.Contains("same-origin") && !low.Contains("allow-popups"))
                    return (1.0, P("same-origin — strong isolation.", "same-origin — 強隔離。"),
                        P("Good.", "良好。"));
                if (low.Contains("same-origin-allow-popups"))
                    return (0.75, P("Isolated but allows popups.", "已隔離，但容許彈出視窗。"),
                        P("Use same-origin if popups aren't required.",
                          "如不需彈出視窗，宜用 same-origin。"));
                return (0.5, P("Weaker COOP value.", "COOP 值較弱。"),
                    P("Prefer same-origin.", "宜用 same-origin。"));
            }
        });

        list.Add(new Spec
        {
            Name = "Cross-Origin-Embedder-Policy",
            Weight = 6,
            Grade = v =>
            {
                if (v == null) return (0,
                    P("Not set — needed with COOP for crossOriginIsolated (e.g. SharedArrayBuffer).",
                      "未設定 — 配合 COOP 才能達成 crossOriginIsolated（例如 SharedArrayBuffer）。"),
                    P("Add Cross-Origin-Embedder-Policy: require-corp (or credentialless).",
                      "加入 Cross-Origin-Embedder-Policy: require-corp（或 credentialless）。"));
                var low = v.ToLowerInvariant();
                if (low.Contains("require-corp") || low.Contains("credentialless"))
                    return (1.0, P("Embedder isolation in place.", "已建立嵌入者隔離。"),
                        P("Good.", "良好。"));
                return (0.5, P("Unrecognized COEP value.", "未知嘅 COEP 值。"),
                    P("Use require-corp or credentialless.", "用 require-corp 或 credentialless。"));
            }
        });

        return list;
    }

    private static (string, string) Letter(int pct)
    {
        if (pct >= 97) return ("A+", Green);
        if (pct >= 90) return ("A", Green);
        if (pct >= 80) return ("B", "#57A639");
        if (pct >= 70) return ("C", Amber);
        if (pct >= 60) return ("D", "#E07B00");
        return ("F", Red);
    }

    private static long MaxAge(string v)
    {
        try
        {
            foreach (var part in v.Split(';'))
            {
                var p = part.Trim();
                int eq = p.IndexOf('=');
                if (eq > 0 && p.Substring(0, eq).Trim().Equals("max-age", StringComparison.OrdinalIgnoreCase))
                {
                    var num = p.Substring(eq + 1).Trim().Trim('"');
                    if (long.TryParse(num, NumberStyles.Integer, CultureInfo.InvariantCulture, out var age))
                        return age;
                }
            }
        }
        catch { }
        return 0;
    }

    private static bool Has(string v, string token)
        => v.ToLowerInvariant().Contains(token);

    private static string Trunc(string s)
    {
        s = (s ?? "").Trim();
        if (s.Length <= 120) return s;
        return s.Substring(0, 117) + "…";
    }

    /// <summary>Build a plain-text bilingual report for the clipboard. Never throws.</summary>
    public static string BuildReport(Result r)
    {
        var sb = new StringBuilder();
        try
        {
            sb.AppendLine(P("WinForge — Security Header Score", "WinForge — 安全標頭計分"));
            sb.AppendLine("========================================");
            sb.AppendLine(P($"Grade: {r.Grade}   Score: {r.Score}/100", $"等級：{r.Grade}   分數：{r.Score}/100"));
            sb.AppendLine(r.Summary);
            sb.AppendLine();
            foreach (var row in r.Rows)
            {
                sb.AppendLine($"[{row.Status}] {row.Header}");
                sb.AppendLine($"    {P("Value", "值")}: {row.Value}");
                if (!string.IsNullOrEmpty(row.Note)) sb.AppendLine($"    {P("Note", "註")}: {row.Note}");
                if (!string.IsNullOrEmpty(row.Advice)) sb.AppendLine($"    {P("Fix", "建議")}: {row.Advice}");
                sb.AppendLine();
            }
            sb.AppendLine(P("Analyzed locally by WinForge. Nothing left this PC.",
                            "由 WinForge 於本機分析，沒有任何資料離開此電腦。"));
        }
        catch { }
        return sb.ToString();
    }
}
