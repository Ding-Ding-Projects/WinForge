#include "RomanNum.h"

#include <array>
#include <cwctype>
#include <limits>
#include <string>
#include <vector>

namespace
{
    using winforge::core::romannum::Overline;

    struct RomanPiece
    {
        std::int64_t value;
        std::wstring_view symbol;
    };

    constexpr std::array<RomanPiece, 13> StandardTable{{
        { 1000, L"M" }, { 900, L"CM" }, { 500, L"D" }, { 400, L"CD" },
        { 100, L"C" }, { 90, L"XC" }, { 50, L"L" }, { 40, L"XL" },
        { 10, L"X" }, { 9, L"IX" }, { 5, L"V" }, { 4, L"IV" }, { 1, L"I" },
    }};

    [[nodiscard]] std::wstring Trim(std::wstring_view value)
    {
        auto first = std::size_t{};
        auto last = value.size();
        while (first < last && std::iswspace(value[first])) ++first;
        while (last > first && std::iswspace(value[last - 1])) --last;
        return std::wstring(value.substr(first, last - first));
    }

    [[nodiscard]] wchar_t ToUpperRoman(wchar_t value)
    {
        return value >= L'a' && value <= L'z'
            ? static_cast<wchar_t>(value - L'a' + L'A')
            : value;
    }

    [[nodiscard]] std::wstring Bar(std::wstring_view symbol)
    {
        std::wstring result;
        result.reserve(symbol.size() * 2);
        for (auto const character : symbol)
        {
            result.push_back(character);
            result.push_back(Overline);
        }
        return result;
    }

    [[nodiscard]] std::wstring BuildStandard(
        std::int64_t value,
        std::vector<std::wstring>& breakdown,
        bool barred)
    {
        std::wstring result;
        for (auto const& piece : StandardTable)
        {
            while (value >= piece.value)
            {
                value -= piece.value;
                auto part = barred ? Bar(piece.symbol) : std::wstring(piece.symbol);
                result += part;
                breakdown.push_back(std::move(part));
            }
        }
        return result;
    }

    [[nodiscard]] std::wstring JoinBreakdown(std::vector<std::wstring> const& parts)
    {
        std::wstring result;
        for (std::size_t index = 0; index < parts.size(); ++index)
        {
            if (index != 0) result += L" + ";
            result += parts[index];
        }
        return result;
    }

    [[nodiscard]] std::wstring ExpandParentheses(std::wstring_view input, bool& error)
    {
        error = false;
        if (input.find(L'(') == std::wstring_view::npos && input.find(L')') == std::wstring_view::npos)
        {
            // Preserve the managed oracle's exact canonical-round-trip quirk:
            // direct lowercase input scans case-insensitively but is rejected
            // because its unmodified spelling differs from uppercase canonical
            // output. Parenthetical input takes the branch below and is folded.
            return std::wstring(input);
        }

        std::wstring result;
        std::size_t index{};
        while (index < input.size())
        {
            auto const character = input[index];
            if (character == L'(')
            {
                auto const close = input.find(L')', index + 1);
                if (close == std::wstring_view::npos || close == index + 1)
                {
                    error = true;
                    return std::wstring(input);
                }
                std::wstring inner;
                inner.reserve(close - index - 1);
                for (auto nested = index + 1; nested < close; ++nested)
                {
                    inner.push_back(ToUpperRoman(input[nested]));
                }
                result += Bar(inner);
                index = close + 1;
            }
            else if (character == L')')
            {
                error = true;
                return std::wstring(input);
            }
            else
            {
                result.push_back(ToUpperRoman(character));
                ++index;
            }
        }
        return result;
    }

    [[nodiscard]] bool TryRomanLetter(wchar_t value, std::int64_t& result)
    {
        switch (ToUpperRoman(value))
        {
        case L'I': result = 1; return true;
        case L'V': result = 5; return true;
        case L'X': result = 10; return true;
        case L'L': result = 50; return true;
        case L'C': result = 100; return true;
        case L'D': result = 500; return true;
        case L'M': result = 1000; return true;
        default: result = 0; return false;
        }
    }

    [[nodiscard]] bool TryScan(
        std::wstring_view input,
        std::int64_t& value,
        std::wstring& reasonEn,
        std::wstring& reasonZh)
    {
        std::vector<std::int64_t> tokens;
        tokens.reserve(input.size());
        for (std::size_t index{}; index < input.size();)
        {
            auto const character = input[index];
            auto barred = index + 1 < input.size() && input[index + 1] == Overline;
            std::int64_t base{};
            if (!TryRomanLetter(character, base))
            {
                reasonEn = L"Unexpected character '" + std::wstring(1, character) + L"'.";
                reasonZh = L"出現無效字元「" + std::wstring(1, character) + L"」。";
                return false;
            }
            tokens.push_back(barred ? base * 1000 : base);
            index += barred ? 2 : 1;
        }

        if (tokens.empty())
        {
            reasonEn = L"No Roman letters found.";
            reasonZh = L"搵唔到羅馬字母。";
            return false;
        }

        value = 0;
        for (std::size_t index{}; index < tokens.size(); ++index)
        {
            auto const token = tokens[index];
            auto const subtract = index + 1 < tokens.size() && token < tokens[index + 1];
            if (subtract)
            {
                if (value < std::numeric_limits<std::int64_t>::min() + token)
                {
                    reasonEn = L"Roman numeral is too large.";
                    reasonZh = L"羅馬數字太大。";
                    return false;
                }
                value -= token;
            }
            else
            {
                if (value > std::numeric_limits<std::int64_t>::max() - token)
                {
                    reasonEn = L"Roman numeral is too large.";
                    reasonZh = L"羅馬數字太大。";
                    return false;
                }
                value += token;
            }
        }
        return true;
    }

    [[nodiscard]] winforge::core::romannum::ToNumberResult Fail(
        std::wstring en,
        std::wstring zh)
    {
        return { false, 0, {}, std::move(en), std::move(zh) };
    }
}

namespace winforge::core::romannum
{
    std::wstring FormatGrouped(std::int64_t value)
    {
        auto text = std::to_wstring(value);
        auto const firstDigit = text.starts_with(L'-') ? 1u : 0u;
        for (auto position = text.size(); position > firstDigit + 3; position -= 3)
        {
            text.insert(position - 3, 1, L',');
        }
        return text;
    }

    bool TryParseInteger(std::wstring_view input, std::int64_t& value)
    {
        value = 0;
        auto const text = Trim(input);
        if (text.empty()) return false;

        auto position = std::size_t{};
        auto negative = false;
        if (text[position] == L'+' || text[position] == L'-')
        {
            negative = text[position] == L'-';
            ++position;
        }
        if (position == text.size()) return false;

        std::uint64_t magnitude{};
        auto const maximum = negative
            ? static_cast<std::uint64_t>(std::numeric_limits<std::int64_t>::max()) + 1u
            : static_cast<std::uint64_t>(std::numeric_limits<std::int64_t>::max());
        auto hasDigit = false;
        for (; position < text.size(); ++position)
        {
            auto const character = text[position];
            if (character == L',') continue;
            if (character < L'0' || character > L'9') return false;
            auto const digit = static_cast<std::uint64_t>(character - L'0');
            if (magnitude > (maximum - digit) / 10u) return false;
            magnitude = magnitude * 10u + digit;
            hasDigit = true;
        }
        if (!hasDigit) return false;

        if (!negative)
        {
            value = static_cast<std::int64_t>(magnitude);
        }
        else if (magnitude == static_cast<std::uint64_t>(std::numeric_limits<std::int64_t>::max()) + 1u)
        {
            value = std::numeric_limits<std::int64_t>::min();
        }
        else
        {
            value = -static_cast<std::int64_t>(magnitude);
        }
        return true;
    }

    ToRomanResult ToRoman(std::int64_t value, bool allowExtended)
    {
        auto const maximum = allowExtended ? ExtendedMax : StandardMax;
        if (value < 1 || value > maximum)
        {
            auto const maximumText = FormatGrouped(maximum);
            return { false, {}, {},
                L"Enter a whole number from 1 to " + maximumText + L".",
                L"請輸入 1 至 " + maximumText + L" 之間嘅整數。" };
        }

        std::vector<std::wstring> breakdown;
        std::wstring roman;
        if (allowExtended && value >= 4'000)
        {
            auto const high = value / 1'000;
            auto const low = value % 1'000;
            roman = BuildStandard(high, breakdown, true);
            if (low > 0) roman += BuildStandard(low, breakdown, false);
        }
        else
        {
            roman = BuildStandard(value, breakdown, false);
        }
        return { true, std::move(roman), JoinBreakdown(breakdown), {}, {} };
    }

    ToNumberResult ToNumber(std::wstring_view input, bool allowExtended)
    {
        auto const raw = Trim(input);
        if (raw.empty())
        {
            return Fail(L"Type a Roman numeral to convert.", L"輸入羅馬數字嚟轉換。");
        }

        bool parenthesesError{};
        auto const normalized = ExpandParentheses(raw, parenthesesError);
        if (parenthesesError)
        {
            return Fail(L"Unbalanced or empty parentheses.", L"括號唔對稱或者係空嘅。");
        }

        std::int64_t value{};
        std::wstring reasonEn;
        std::wstring reasonZh;
        if (!TryScan(normalized, value, reasonEn, reasonZh))
        {
            return Fail(std::move(reasonEn), std::move(reasonZh));
        }
        if (value < 1)
        {
            return Fail(L"Result is not a positive number.", L"結果唔係正整數。");
        }

        auto const maximum = allowExtended ? ExtendedMax : StandardMax;
        if (value > maximum)
        {
            if (allowExtended)
            {
                return Fail(
                    L"Above the extended maximum (" + FormatGrouped(ExtendedMax) + L").",
                    L"超過擴充上限（" + FormatGrouped(ExtendedMax) + L"）。");
            }
            return Fail(
                L"Above " + std::to_wstring(StandardMax) + L" — enable Extended for larger values.",
                L"超過 " + std::to_wstring(StandardMax) + L" — 開啟「擴充」先可以更大。");
        }

        auto const canonical = ToRoman(value, true);
        if (!canonical.ok || canonical.roman != normalized)
        {
            auto const canonicalText = canonical.ok ? canonical.roman : L"?";
            return Fail(
                L"Malformed Roman numeral — the canonical form of " + FormatGrouped(value) + L" is \"" + canonicalText + L"\".",
                L"羅馬數字寫法唔正確 — " + FormatGrouped(value) + L" 嘅標準寫法係「" + canonicalText + L"」。");
        }

        return { true, value, canonical.breakdown, {}, {} };
    }
}
