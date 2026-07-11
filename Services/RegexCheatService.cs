using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 正則速查 · Regex cheatsheet — a comprehensive, embedded .NET-regex reference.
/// Pure data + filtering; never throws. Bilingual descriptions (English + 粵語).
/// </summary>
public static class RegexCheatService
{
    /// <summary>One reference entry: token/pattern, category, bilingual description, and an example.</summary>
    public sealed class Entry
    {
        public string Category { get; init; } = "";
        public string CategoryZh { get; init; } = "";
        public string Token { get; init; } = "";
        public string DescEn { get; init; } = "";
        public string DescZh { get; init; } = "";
        public string Example { get; init; } = "";
    }

    /// <summary>A ready-made, copyable full pattern.</summary>
    public sealed class Recipe
    {
        public string Name { get; init; } = "";
        public string NameZh { get; init; } = "";
        public string Pattern { get; init; } = "";
    }

    // ---- Categories (English key + 粵語) ---------------------------------
    private const string C_CHAR = "Character classes";
    private const string C_CHAR_ZH = "字元類";
    private const string C_ANCHOR = "Anchors";
    private const string C_ANCHOR_ZH = "錨點";
    private const string C_QUANT = "Quantifiers";
    private const string C_QUANT_ZH = "量詞";
    private const string C_GROUP = "Groups & backreferences";
    private const string C_GROUP_ZH = "群組同反向引用";
    private const string C_NAMED = "Named groups";
    private const string C_NAMED_ZH = "具名群組";
    private const string C_LOOK = "Lookaround";
    private const string C_LOOK_ZH = "環視";
    private const string C_ALT = "Alternation";
    private const string C_ALT_ZH = "選擇";
    private const string C_FLAGS = "Flags & options";
    private const string C_FLAGS_ZH = "旗標同選項";
    private const string C_RECIPE = "Common recipes";
    private const string C_RECIPE_ZH = "常用配方";

    private static readonly List<Entry> _entries = new()
    {
        // --- Character classes ---
        E(C_CHAR, C_CHAR_ZH, ".", "Any character except newline (unless Singleline).", "除咗換行以外任何一個字元（除非開咗 Singleline）。", "a.c → \"abc\", \"a-c\""),
        E(C_CHAR, C_CHAR_ZH, "[abc]", "Any one of a, b, or c.", "a、b 或 c 其中一個。", "[aeiou] matches a vowel"),
        E(C_CHAR, C_CHAR_ZH, "[^abc]", "Any character except a, b, or c.", "除咗 a、b、c 之外任何一個。", "[^0-9] → non-digit"),
        E(C_CHAR, C_CHAR_ZH, "[a-z]", "A range: any lowercase letter.", "範圍：任何細楷字母。", "[A-Za-z0-9] alnum"),
        E(C_CHAR, C_CHAR_ZH, "\\d", "A digit [0-9].", "一個數字 [0-9]。", "\\d{3} → \"123\""),
        E(C_CHAR, C_CHAR_ZH, "\\D", "A non-digit.", "唔係數字嘅字元。", "\\D+ → \"abc\""),
        E(C_CHAR, C_CHAR_ZH, "\\w", "A word char [A-Za-z0-9_].", "字詞字元 [A-Za-z0-9_]。", "\\w+ → \"user_1\""),
        E(C_CHAR, C_CHAR_ZH, "\\W", "A non-word char.", "唔係字詞嘅字元。", "\\W → \" \", \"!\""),
        E(C_CHAR, C_CHAR_ZH, "\\s", "Whitespace (space, tab, newline).", "空白（空格、Tab、換行）。", "a\\sb → \"a b\""),
        E(C_CHAR, C_CHAR_ZH, "\\S", "Non-whitespace.", "非空白字元。", "\\S+ → \"word\""),
        E(C_CHAR, C_CHAR_ZH, "\\t", "A tab character.", "一個 Tab 字元。", "\\t"),
        E(C_CHAR, C_CHAR_ZH, "\\n", "A newline (line feed).", "換行字元。", "line1\\nline2"),
        E(C_CHAR, C_CHAR_ZH, "\\uXXXX", "Unicode char by 4-hex code point.", "以 4 位十六進制碼位嘅 Unicode 字元。", "\\u00e9 → \"é\""),
        E(C_CHAR, C_CHAR_ZH, "\\p{L}", "Any Unicode letter category.", "任何 Unicode 字母類別。", "\\p{Lu} → uppercase"),
        E(C_CHAR, C_CHAR_ZH, "\\\\", "A literal backslash.", "一個真正嘅反斜線。", "C:\\\\ → \"C:\\\""),

        // --- Anchors ---
        E(C_ANCHOR, C_ANCHOR_ZH, "^", "Start of string (or line in Multiline).", "字串開頭（Multiline 時係行首）。", "^Hello"),
        E(C_ANCHOR, C_ANCHOR_ZH, "$", "End of string (or line in Multiline).", "字串結尾（Multiline 時係行尾）。", "world$"),
        E(C_ANCHOR, C_ANCHOR_ZH, "\\b", "A word boundary.", "字詞邊界。", "\\bcat\\b whole word"),
        E(C_ANCHOR, C_ANCHOR_ZH, "\\B", "Not a word boundary.", "唔係字詞邊界。", "\\Bcat"),
        E(C_ANCHOR, C_ANCHOR_ZH, "\\A", "Start of the input, ignores Multiline.", "輸入開頭，唔理 Multiline。", "\\AHello"),
        E(C_ANCHOR, C_ANCHOR_ZH, "\\z", "Very end of the input.", "輸入最尾。", "end\\z"),
        E(C_ANCHOR, C_ANCHOR_ZH, "\\Z", "End of input, before a final newline.", "輸入結尾，喺最後換行之前。", "end\\Z"),
        E(C_ANCHOR, C_ANCHOR_ZH, "\\G", "Where the previous match ended.", "上一次配對結束嘅位置。", "\\G\\d+"),

        // --- Quantifiers ---
        E(C_QUANT, C_QUANT_ZH, "*", "0 or more (greedy).", "0 次或以上（貪婪）。", "a* → \"\", \"aaa\""),
        E(C_QUANT, C_QUANT_ZH, "+", "1 or more (greedy).", "1 次或以上（貪婪）。", "a+ → \"a\", \"aaa\""),
        E(C_QUANT, C_QUANT_ZH, "?", "0 or 1 (optional).", "0 或 1 次（可選）。", "colou?r → \"color\""),
        E(C_QUANT, C_QUANT_ZH, "{n}", "Exactly n times.", "剛好 n 次。", "\\d{4} → \"2026\""),
        E(C_QUANT, C_QUANT_ZH, "{n,}", "n or more times.", "n 次或以上。", "\\d{2,} 2+ digits"),
        E(C_QUANT, C_QUANT_ZH, "{n,m}", "Between n and m times.", "n 至 m 次。", "\\d{2,4}"),
        E(C_QUANT, C_QUANT_ZH, "*?", "0 or more (lazy / non-greedy).", "0 次或以上（懶惰）。", "<.*?> shortest tag"),
        E(C_QUANT, C_QUANT_ZH, "+?", "1 or more (lazy).", "1 次或以上（懶惰）。", "\".+?\" shortest string"),
        E(C_QUANT, C_QUANT_ZH, "??", "0 or 1 (lazy).", "0 或 1 次（懶惰）。", "a??"),
        E(C_QUANT, C_QUANT_ZH, "(?>a*)", "Atomic 0-or-more (the .NET equivalent of possessive a*; .NET does not use *+).", "原子 0 次或以上（.NET 入面等同佔有 a*；.NET 唔用 *+）。", "(?>a*)"),
        E(C_QUANT, C_QUANT_ZH, "{n,m}?", "Between n and m (lazy).", "n 至 m 次（懶惰）。", "\\d{2,4}?"),

        // --- Groups & backreferences ---
        E(C_GROUP, C_GROUP_ZH, "(...)", "Capturing group (numbered from 1).", "捕獲群組（由 1 開始編號）。", "(ab)+ → \"abab\""),
        E(C_GROUP, C_GROUP_ZH, "(?:...)", "Non-capturing group.", "非捕獲群組。", "(?:ab)+"),
        E(C_GROUP, C_GROUP_ZH, "\\1", "Backreference to group 1.", "引用第 1 個群組。", "(\\w)\\1 → \"aa\""),
        E(C_GROUP, C_GROUP_ZH, "(?>...)", "Atomic group (no backtracking inside).", "原子群組（內部唔回溯）。", "(?>\\d+)"),
        E(C_GROUP, C_GROUP_ZH, "(?i:...)", "Inline options for this group only.", "只喺呢個群組套用選項。", "(?i:abc) → \"ABC\""),

        // --- Named groups ---
        E(C_NAMED, C_NAMED_ZH, "(?<name>...)", "Named capturing group.", "具名捕獲群組。", "(?<yr>\\d{4})"),
        E(C_NAMED, C_NAMED_ZH, "(?'name'...)", "Named group, quote form.", "具名群組（單引號寫法）。", "(?'yr'\\d{4})"),
        E(C_NAMED, C_NAMED_ZH, "\\k<name>", "Backreference to a named group.", "引用具名群組。", "(?<c>\\w)\\k<c>"),
        E(C_NAMED, C_NAMED_ZH, "${name}", "Named group in a replacement string.", "喺替換字串引用具名群組。", "Regex.Replace(..., \"${yr}\")"),
        E(C_NAMED, C_NAMED_ZH, "$1", "Numbered group in a replacement.", "喺替換字串引用編號群組。", "\"$1-$2\""),

        // --- Lookaround ---
        E(C_LOOK, C_LOOK_ZH, "(?=...)", "Positive lookahead — followed by.", "正向前瞻 — 後面跟住。", "\\d+(?= USD)"),
        E(C_LOOK, C_LOOK_ZH, "(?!...)", "Negative lookahead — not followed by.", "負向前瞻 — 後面唔係。", "foo(?!bar)"),
        E(C_LOOK, C_LOOK_ZH, "(?<=...)", "Positive lookbehind — preceded by.", "正向後顧 — 前面係。", "(?<=\\$)\\d+"),
        E(C_LOOK, C_LOOK_ZH, "(?<!...)", "Negative lookbehind — not preceded by.", "負向後顧 — 前面唔係。", "(?<!\\$)\\d+"),

        // --- Alternation ---
        E(C_ALT, C_ALT_ZH, "a|b", "Match a or b (alternation).", "配對 a 或 b（選擇）。", "cat|dog"),
        E(C_ALT, C_ALT_ZH, "(cat|dog)", "Grouped alternation.", "群組化選擇。", "(cat|dog)s?"),
        E(C_ALT, C_ALT_ZH, "(?(1)yes|no)", "Conditional on whether group 1 matched.", "視乎第 1 組有冇配對嘅條件。", "(a)?(?(1)b|c)"),

        // --- Flags & options ---
        E(C_FLAGS, C_FLAGS_ZH, "(?i)", "Case-insensitive from here on.", "由此開始唔分大細楷。", "(?i)hello → \"HELLO\""),
        E(C_FLAGS, C_FLAGS_ZH, "(?m)", "Multiline — ^ and $ match each line.", "多行 — ^ 同 $ 對每行。", "(?m)^\\d+"),
        E(C_FLAGS, C_FLAGS_ZH, "(?s)", "Singleline — . matches newlines too.", "單行 — . 連換行都配。", "(?s)<.*>"),
        E(C_FLAGS, C_FLAGS_ZH, "(?x)", "Ignore whitespace & allow # comments.", "忽略空白、可以用 # 註解。", "(?x) \\d+  # digits"),
        E(C_FLAGS, C_FLAGS_ZH, "(?n)", "Only named groups capture (explicit).", "只有具名群組先捕獲。", "(?n)(a)(?<x>b)"),
        E(C_FLAGS, C_FLAGS_ZH, "(?i-s)", "Turn options on/off (i on, s off).", "開/關選項（開 i、關 s）。", "(?i-s)abc"),
        E(C_FLAGS, C_FLAGS_ZH, "RegexOptions.Compiled", ".NET flag — compile to IL for speed.", ".NET 旗標 — 編譯成 IL 提速。", "new Regex(p, RegexOptions.Compiled)"),
        E(C_FLAGS, C_FLAGS_ZH, "RegexOptions.IgnoreCase", ".NET equivalent of (?i).", "等同 (?i) 嘅 .NET 旗標。", "RegexOptions.IgnoreCase"),

        // --- Common recipes (as inline tokens too) ---
        E(C_RECIPE, C_RECIPE_ZH, "Email", "A pragmatic email matcher.", "務實嘅電郵配對。", "user@example.com"),
        E(C_RECIPE, C_RECIPE_ZH, "URL (http/https)", "An http/https URL.", "http/https 網址。", "https://a.com/x?y=1"),
        E(C_RECIPE, C_RECIPE_ZH, "IPv4", "A dotted-quad IPv4 address.", "點分四段 IPv4 位址。", "192.168.0.1"),
        E(C_RECIPE, C_RECIPE_ZH, "ISO date", "A YYYY-MM-DD date.", "YYYY-MM-DD 日期。", "2026-07-01"),
        E(C_RECIPE, C_RECIPE_ZH, "Time HH:MM", "A 24-hour time.", "24 小時制時間。", "23:59"),
        E(C_RECIPE, C_RECIPE_ZH, "Hex color", "A #RGB or #RRGGBB color.", "#RGB 或 #RRGGBB 色碼。", "#1e90ff"),
        E(C_RECIPE, C_RECIPE_ZH, "UUID", "A canonical UUID/GUID.", "標準 UUID/GUID。", "9b2f...-..."),
        E(C_RECIPE, C_RECIPE_ZH, "Slug", "A lowercase URL slug.", "細楷網址 slug。", "my-post-title"),
    };

    private static readonly List<Recipe> _recipes = new()
    {
        R("Email", "電郵", @"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}$"),
        R("URL", "網址", @"^https?://[^\s/$.?#].[^\s]*$"),
        R("IPv4", "IPv4", @"^((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$"),
        R("Hex color", "十六進制色碼", @"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$"),
        R("UUID", "UUID", @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"),
        R("Slug", "Slug", @"^[a-z0-9]+(?:-[a-z0-9]+)*$"),
        R("ISO date", "ISO 日期", @"^\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])$"),
        R("US phone", "美式電話", @"^\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}$"),
    };

    private static Entry E(string cat, string catZh, string token, string en, string zh, string ex) =>
        new() { Category = cat, CategoryZh = catZh, Token = token, DescEn = en, DescZh = zh, Example = ex };

    private static Recipe R(string name, string nameZh, string pattern) =>
        new() { Name = name, NameZh = nameZh, Pattern = pattern };

    /// <summary>All ready-made copyable full patterns.</summary>
    public static IReadOnlyList<Recipe> Recipes => _recipes;

    /// <summary>Distinct category labels; the first item is the "All" sentinel supplied by the caller.</summary>
    public static IReadOnlyList<(string En, string Zh)> Categories()
    {
        var seen = new List<(string, string)>();
        foreach (var e in _entries)
        {
            var key = (e.Category, e.CategoryZh);
            if (!seen.Contains(key)) seen.Add(key);
        }
        return seen;
    }

    /// <summary>
    /// Filter entries by a free-text query (token/description/example, both languages) and an optional
    /// category (English key; null/empty = all). Never throws.
    /// </summary>
    public static IReadOnlyList<Entry> Filter(string? query, string? categoryEn)
    {
        try
        {
            IEnumerable<Entry> q = _entries;
            if (!string.IsNullOrWhiteSpace(categoryEn))
                q = q.Where(e => string.Equals(e.Category, categoryEn, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(query))
            {
                var t = query.Trim();
                q = q.Where(e =>
                    Has(e.Token, t) || Has(e.DescEn, t) || Has(e.DescZh, t) ||
                    Has(e.Example, t) || Has(e.Category, t) || Has(e.CategoryZh, t));
            }
            return q.ToList();
        }
        catch
        {
            return _entries;
        }
    }

    private static bool Has(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
}
