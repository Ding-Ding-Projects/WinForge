#include "RegexCheat.h"

#include <algorithm>
#include <array>
#include <cwctype>

namespace winforge::core::regex
{
    namespace
    {
        constexpr std::array kCategories{
            RegexCheatCategory{ L"character-classes", L"Character classes", L"字元類" },
            RegexCheatCategory{ L"anchors", L"Anchors", L"錨點" },
            RegexCheatCategory{ L"quantifiers", L"Quantifiers", L"量詞" },
            RegexCheatCategory{ L"groups-backreferences", L"Groups & backreferences", L"群組同反向引用" },
            RegexCheatCategory{ L"named-groups", L"Named groups", L"具名群組" },
            RegexCheatCategory{ L"lookaround", L"Lookaround", L"環視" },
            RegexCheatCategory{ L"alternation", L"Alternation", L"選擇" },
            RegexCheatCategory{ L"flags-options", L"Flags & options", L"旗標同選項" },
            RegexCheatCategory{ L"common-recipes", L"Common recipes", L"常用配方" },
        };

        constexpr std::array kEntries{
            // Character classes (15)
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L".", L"Any character except newline (unless Singleline).", L"除咗換行以外任何一個字元（除非開咗 Singleline）。", L"a.c → \"abc\", \"a-c\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"[abc]", L"Any one of a, b, or c.", L"a、b 或 c 其中一個。", L"[aeiou] matches a vowel" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"[^abc]", L"Any character except a, b, or c.", L"除咗 a、b、c 之外任何一個。", L"[^0-9] → non-digit" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"[a-z]", L"A range: any lowercase letter.", L"範圍：任何細楷字母。", L"[A-Za-z0-9] alnum" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\d", L"A digit [0-9].", L"一個數字 [0-9]。", L"\\d{3} → \"123\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\D", L"A non-digit.", L"唔係數字嘅字元。", L"\\D+ → \"abc\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\w", L"A word char [A-Za-z0-9_].", L"字詞字元 [A-Za-z0-9_]。", L"\\w+ → \"user_1\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\W", L"A non-word char.", L"唔係字詞嘅字元。", L"\\W → \" \", \"!\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\s", L"Whitespace (space, tab, newline).", L"空白（空格、Tab、換行）。", L"a\\sb → \"a b\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\S", L"Non-whitespace.", L"非空白字元。", L"\\S+ → \"word\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\t", L"A tab character.", L"一個 Tab 字元。", L"\\t" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\n", L"A newline (line feed).", L"換行字元。", L"line1\\nline2" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\uXXXX", L"Unicode char by 4-hex code point.", L"以 4 位十六進制碼位嘅 Unicode 字元。", L"\\u00e9 → \"é\"" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\p{L}", L"Any Unicode letter category.", L"任何 Unicode 字母類別。", L"\\p{Lu} → uppercase" },
            RegexCheatEntry{ L"character-classes", L"Character classes", L"字元類", L"\\\\", L"A literal backslash.", L"一個真正嘅反斜線。", L"C:\\\\ → \"C:\\\"" },

            // Anchors (8)
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"^", L"Start of string (or line in Multiline).", L"字串開頭（Multiline 時係行首）。", L"^Hello" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"$", L"End of string (or line in Multiline).", L"字串結尾（Multiline 時係行尾）。", L"world$" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"\\b", L"A word boundary.", L"字詞邊界。", L"\\bcat\\b whole word" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"\\B", L"Not a word boundary.", L"唔係字詞邊界。", L"\\Bcat" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"\\A", L"Start of the input, ignores Multiline.", L"輸入開頭，唔理 Multiline。", L"\\AHello" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"\\z", L"Very end of the input.", L"輸入最尾。", L"end\\z" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"\\Z", L"End of input, before a final newline.", L"輸入結尾，喺最後換行之前。", L"end\\Z" },
            RegexCheatEntry{ L"anchors", L"Anchors", L"錨點", L"\\G", L"Where the previous match ended.", L"上一次配對結束嘅位置。", L"\\G\\d+" },

            // Quantifiers (11)
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"*", L"0 or more (greedy).", L"0 次或以上（貪婪）。", L"a* → \"\", \"aaa\"" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"+", L"1 or more (greedy).", L"1 次或以上（貪婪）。", L"a+ → \"a\", \"aaa\"" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"?", L"0 or 1 (optional).", L"0 或 1 次（可選）。", L"colou?r → \"color\"" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"{n}", L"Exactly n times.", L"剛好 n 次。", L"\\d{4} → \"2026\"" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"{n,}", L"n or more times.", L"n 次或以上。", L"\\d{2,} 2+ digits" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"{n,m}", L"Between n and m times.", L"n 至 m 次。", L"\\d{2,4}" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"*?", L"0 or more (lazy / non-greedy).", L"0 次或以上（懶惰）。", L"<.*?> shortest tag" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"+?", L"1 or more (lazy).", L"1 次或以上（懶惰）。", L"\".+?\" shortest string" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"??", L"0 or 1 (lazy).", L"0 或 1 次（懶惰）。", L"a??" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"(?>a*)", L"Atomic 0-or-more (the .NET equivalent of possessive a*; .NET does not use *+).", L"原子 0 次或以上（.NET 入面等同佔有 a*；.NET 唔用 *+）。", L"(?>a*)" },
            RegexCheatEntry{ L"quantifiers", L"Quantifiers", L"量詞", L"{n,m}?", L"Between n and m (lazy).", L"n 至 m 次（懶惰）。", L"\\d{2,4}?" },

            // Groups & backreferences (5)
            RegexCheatEntry{ L"groups-backreferences", L"Groups & backreferences", L"群組同反向引用", L"(...)", L"Capturing group (numbered from 1).", L"捕獲群組（由 1 開始編號）。", L"(ab)+ → \"abab\"" },
            RegexCheatEntry{ L"groups-backreferences", L"Groups & backreferences", L"群組同反向引用", L"(?:...)", L"Non-capturing group.", L"非捕獲群組。", L"(?:ab)+" },
            RegexCheatEntry{ L"groups-backreferences", L"Groups & backreferences", L"群組同反向引用", L"\\1", L"Backreference to group 1.", L"引用第 1 個群組。", L"(\\w)\\1 → \"aa\"" },
            RegexCheatEntry{ L"groups-backreferences", L"Groups & backreferences", L"群組同反向引用", L"(?>...)", L"Atomic group (no backtracking inside).", L"原子群組（內部唔回溯）。", L"(?>\\d+)" },
            RegexCheatEntry{ L"groups-backreferences", L"Groups & backreferences", L"群組同反向引用", L"(?i:...)", L"Inline options for this group only.", L"只喺呢個群組套用選項。", L"(?i:abc) → \"ABC\"" },

            // Named groups (5)
            RegexCheatEntry{ L"named-groups", L"Named groups", L"具名群組", L"(?<name>...)", L"Named capturing group.", L"具名捕獲群組。", L"(?<yr>\\d{4})" },
            RegexCheatEntry{ L"named-groups", L"Named groups", L"具名群組", L"(?'name'...)", L"Named group, quote form.", L"具名群組（單引號寫法）。", L"(?'yr'\\d{4})" },
            RegexCheatEntry{ L"named-groups", L"Named groups", L"具名群組", L"\\k<name>", L"Backreference to a named group.", L"引用具名群組。", L"(?<c>\\w)\\k<c>" },
            RegexCheatEntry{ L"named-groups", L"Named groups", L"具名群組", L"${name}", L"Named group in a replacement string.", L"喺替換字串引用具名群組。", L"Regex.Replace(..., \"${yr}\")" },
            RegexCheatEntry{ L"named-groups", L"Named groups", L"具名群組", L"$1", L"Numbered group in a replacement.", L"喺替換字串引用編號群組。", L"\"$1-$2\"" },

            // Lookaround (4)
            RegexCheatEntry{ L"lookaround", L"Lookaround", L"環視", L"(?=...)", L"Positive lookahead — followed by.", L"正向前瞻 — 後面跟住。", L"\\d+(?= USD)" },
            RegexCheatEntry{ L"lookaround", L"Lookaround", L"環視", L"(?!...)", L"Negative lookahead — not followed by.", L"負向前瞻 — 後面唔係。", L"foo(?!bar)" },
            RegexCheatEntry{ L"lookaround", L"Lookaround", L"環視", L"(?<=...)", L"Positive lookbehind — preceded by.", L"正向後顧 — 前面係。", L"(?<=\\$)\\d+" },
            RegexCheatEntry{ L"lookaround", L"Lookaround", L"環視", L"(?<!...)", L"Negative lookbehind — not preceded by.", L"負向後顧 — 前面唔係。", L"(?<!\\$)\\d+" },

            // Alternation (3)
            RegexCheatEntry{ L"alternation", L"Alternation", L"選擇", L"a|b", L"Match a or b (alternation).", L"配對 a 或 b（選擇）。", L"cat|dog" },
            RegexCheatEntry{ L"alternation", L"Alternation", L"選擇", L"(cat|dog)", L"Grouped alternation.", L"群組化選擇。", L"(cat|dog)s?" },
            RegexCheatEntry{ L"alternation", L"Alternation", L"選擇", L"(?(1)yes|no)", L"Conditional on whether group 1 matched.", L"視乎第 1 組有冇配對嘅條件。", L"(a)?(?(1)b|c)" },

            // Flags & options (8)
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"(?i)", L"Case-insensitive from here on.", L"由此開始唔分大細楷。", L"(?i)hello → \"HELLO\"" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"(?m)", L"Multiline — ^ and $ match each line.", L"多行 — ^ 同 $ 對每行。", L"(?m)^\\d+" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"(?s)", L"Singleline — . matches newlines too.", L"單行 — . 連換行都配。", L"(?s)<.*>" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"(?x)", L"Ignore whitespace & allow # comments.", L"忽略空白、可以用 # 註解。", L"(?x) \\d+  # digits" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"(?n)", L"Only named groups capture (explicit).", L"只有具名群組先捕獲。", L"(?n)(a)(?<x>b)" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"(?i-s)", L"Turn options on/off (i on, s off).", L"開/關選項（開 i、關 s）。", L"(?i-s)abc" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"RegexOptions.Compiled", L".NET flag — compile to IL for speed.", L".NET 旗標 — 編譯成 IL 提速。", L"new Regex(p, RegexOptions.Compiled)" },
            RegexCheatEntry{ L"flags-options", L"Flags & options", L"旗標同選項", L"RegexOptions.IgnoreCase", L".NET equivalent of (?i).", L"等同 (?i) 嘅 .NET 旗標。", L"RegexOptions.IgnoreCase" },

            // Common recipes (8)
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"Email", L"A pragmatic email matcher.", L"務實嘅電郵配對。", L"user@example.com" },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"URL (http/https)", L"An http/https URL.", L"http/https 網址。", L"https://a.com/x?y=1" },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"IPv4", L"A dotted-quad IPv4 address.", L"點分四段 IPv4 位址。", L"192.168.0.1" },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"ISO date", L"A YYYY-MM-DD date.", L"YYYY-MM-DD 日期。", L"2026-07-01" },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"Time HH:MM", L"A 24-hour time.", L"24 小時制時間。", L"23:59" },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"Hex color", L"A #RGB or #RRGGBB color.", L"#RGB 或 #RRGGBB 色碼。", L"#1e90ff" },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"UUID", L"A canonical UUID/GUID.", L"標準 UUID/GUID。", L"9b2f...-..." },
            RegexCheatEntry{ L"common-recipes", L"Common recipes", L"常用配方", L"Slug", L"A lowercase URL slug.", L"細楷網址 slug。", L"my-post-title" },
        };

        constexpr std::array kRecipes{
            RegexCheatRecipe{ L"email", L"Email", L"電郵", L"^[A-Za-z0-9._%+\\-]+@[A-Za-z0-9.\\-]+\\.[A-Za-z]{2,}$" },
            RegexCheatRecipe{ L"url", L"URL", L"網址", L"^https?://[^\\s/$.?#].[^\\s]*$" },
            RegexCheatRecipe{ L"ipv4", L"IPv4", L"IPv4", L"^((25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]?\\d)\\.){3}(25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]?\\d)$" },
            RegexCheatRecipe{ L"hex-color", L"Hex color", L"十六進制色碼", L"^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$" },
            RegexCheatRecipe{ L"uuid", L"UUID", L"UUID", L"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$" },
            RegexCheatRecipe{ L"slug", L"Slug", L"Slug", L"^[a-z0-9]+(?:-[a-z0-9]+)*$" },
            RegexCheatRecipe{ L"iso-date", L"ISO date", L"ISO 日期", L"^\\d{4}-(0[1-9]|1[0-2])-(0[1-9]|[12]\\d|3[01])$" },
            RegexCheatRecipe{ L"us-phone", L"US phone", L"美式電話", L"^\\(?\\d{3}\\)?[\\s.\\-]?\\d{3}[\\s.\\-]?\\d{4}$" },
        };

        [[nodiscard]] bool EqualsInsensitive(std::wstring_view left, std::wstring_view right) noexcept
        {
            return left.size() == right.size() && std::equal(
                left.begin(), left.end(), right.begin(),
                [](wchar_t a, wchar_t b)
                {
                    return std::towlower(a) == std::towlower(b);
                });
        }

        [[nodiscard]] bool ContainsInsensitive(std::wstring_view haystack, std::wstring_view needle) noexcept
        {
            if (needle.empty())
            {
                return true;
            }
            if (needle.size() > haystack.size())
            {
                return false;
            }
            return std::search(
                haystack.begin(), haystack.end(), needle.begin(), needle.end(),
                [](wchar_t a, wchar_t b)
                {
                    return std::towlower(a) == std::towlower(b);
                }) != haystack.end();
        }
    }

    std::span<RegexCheatCategory const> RegexCheatCategories() noexcept
    {
        return kCategories;
    }

    std::span<RegexCheatEntry const> RegexCheatEntries() noexcept
    {
        return kEntries;
    }

    std::span<RegexCheatRecipe const> RegexCheatRecipes() noexcept
    {
        return kRecipes;
    }

    bool RegexCheatMatchesLiteral(
        RegexCheatEntry const& entry,
        std::wstring_view category_key,
        std::wstring_view query) noexcept
    {
        if (!category_key.empty() && !EqualsInsensitive(entry.category_key, category_key))
        {
            return false;
        }
        return ContainsInsensitive(entry.token, query) ||
            ContainsInsensitive(entry.description_en, query) ||
            ContainsInsensitive(entry.description_zh, query) ||
            ContainsInsensitive(entry.example, query) ||
            ContainsInsensitive(entry.category_en, query) ||
            ContainsInsensitive(entry.category_zh, query);
    }
}
