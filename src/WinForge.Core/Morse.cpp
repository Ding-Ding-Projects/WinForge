#include "Morse.h"

#include "TextDiff.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace
{
    using Code = std::pair<wchar_t, std::wstring_view>;

    constexpr std::array<Code, 54> MorseCodes{{
        { L'A', L".-" }, { L'B', L"-..." }, { L'C', L"-.-." }, { L'D', L"-.." },
        { L'E', L"." }, { L'F', L"..-." }, { L'G', L"--." }, { L'H', L"...." },
        { L'I', L".." }, { L'J', L".---" }, { L'K', L"-.-" }, { L'L', L".-.." },
        { L'M', L"--" }, { L'N', L"-." }, { L'O', L"---" }, { L'P', L".--." },
        { L'Q', L"--.-" }, { L'R', L".-." }, { L'S', L"..." }, { L'T', L"-" },
        { L'U', L"..-" }, { L'V', L"...-" }, { L'W', L".--" }, { L'X', L"-..-" },
        { L'Y', L"-.--" }, { L'Z', L"--.." },
        { L'0', L"-----" }, { L'1', L".----" }, { L'2', L"..---" }, { L'3', L"...--" },
        { L'4', L"....-" }, { L'5', L"....." }, { L'6', L"-...." }, { L'7', L"--..." },
        { L'8', L"---.." }, { L'9', L"----." },
        { L'.', L".-.-.-" }, { L',', L"--..--" }, { L'?', L"..--.." }, { L'\'', L".----." },
        { L'!', L"-.-.--" }, { L'/', L"-..-." }, { L'(', L"-.--." }, { L')', L"-.--.-" },
        { L'&', L".-..." }, { L':', L"---..." }, { L';', L"-.-.-." }, { L'=', L"-...-" },
        { L'+', L".-.-." }, { L'-', L"-....-" }, { L'_', L"..--.-" }, { L'\"', L".-..-." },
        { L'$', L"...-..-" }, { L'@', L".--.-." },
    }};

    [[nodiscard]] std::wstring_view CodeFor(wchar_t raw) noexcept
    {
        // The managed service calls char.ToUpperInvariant for each UTF-16
        // code unit. Text Diff owns the generated equivalent mapping so all
        // native local-text tools share the same non-expanding contract.
        auto const upper = winforge::core::textdiff::ToUpperInvariantCodeUnit(raw);
        for (auto const& [letter, code] : MorseCodes)
        {
            if (upper == letter) return code;
        }
        return {};
    }

    [[nodiscard]] bool IsTextWordSeparator(wchar_t value) noexcept
    {
        return value == L' ' || value == L'\t' || value == L'\r' || value == L'\n';
    }

    [[nodiscard]] bool IsDotNetWhitespace(wchar_t value) noexcept
    {
        // String.IsNullOrWhiteSpace and String.Trim use Char.IsWhiteSpace on
        // each UTF-16 code unit. Keep the current Unicode whitespace set
        // explicit so the core remains independent of the user's C locale.
        auto const unit = static_cast<std::uint16_t>(value);
        return (unit >= 0x0009u && unit <= 0x000Du) ||
            unit == 0x0020u || unit == 0x0085u || unit == 0x00A0u ||
            unit == 0x1680u || (unit >= 0x2000u && unit <= 0x200Au) ||
            unit == 0x2028u || unit == 0x2029u || unit == 0x202Fu ||
            unit == 0x205Fu || unit == 0x3000u;
    }

    [[nodiscard]] bool IsNullOrWhitespace(std::wstring_view value) noexcept
    {
        for (auto const character : value)
        {
            if (!IsDotNetWhitespace(character)) return false;
        }
        return true;
    }

    [[nodiscard]] std::wstring_view TrimDotNetWhitespace(std::wstring_view value) noexcept
    {
        auto first = std::size_t{};
        auto last = value.size();
        while (first < last && IsDotNetWhitespace(value[first])) ++first;
        while (last > first && IsDotNetWhitespace(value[last - 1])) --last;
        return value.substr(first, last - first);
    }

    [[nodiscard]] std::vector<std::wstring_view> SplitTextWords(std::wstring_view value)
    {
        std::vector<std::wstring_view> words;
        auto index = std::size_t{};
        while (index < value.size())
        {
            while (index < value.size() && IsTextWordSeparator(value[index])) ++index;
            auto const first = index;
            while (index < value.size() && !IsTextWordSeparator(value[index])) ++index;
            if (first < index) words.push_back(value.substr(first, index - first));
        }
        return words;
    }

    [[nodiscard]] wchar_t Decode(std::wstring_view token) noexcept
    {
        for (auto const& [letter, code] : MorseCodes)
        {
            if (token == code) return letter;
        }
        return L'\0';
    }

    void AddGap(std::vector<winforge::core::morse::Flash>& timeline, int units)
    {
        timeline.push_back({ false, units });
    }
}

namespace winforge::core::morse
{
    EncodeResult ToMorse(
        std::wstring_view text,
        std::wstring_view letterSeparator,
        std::wstring_view wordSeparator) noexcept
    {
        EncodeResult result;
        try
        {
            if (text.empty()) return result;
            if (letterSeparator.empty()) letterSeparator = L" ";
            if (wordSeparator.empty()) wordSeparator = L" / ";

            auto const words = SplitTextWords(text);
            for (std::size_t wordIndex{}; wordIndex < words.size(); ++wordIndex)
            {
                if (wordIndex != 0) result.text.append(wordSeparator);
                auto firstLetter = true;
                for (auto const raw : words[wordIndex])
                {
                    if (!firstLetter) result.text.append(letterSeparator);
                    firstLetter = false;

                    if (auto const code = CodeFor(raw); !code.empty())
                    {
                        result.text.append(code);
                    }
                    else
                    {
                        if (std::find(result.unknown.begin(), result.unknown.end(), raw) == result.unknown.end())
                        {
                            result.unknown.push_back(raw);
                        }
                        result.text.push_back(L'#');
                    }
                }
            }
        }
        catch (...)
        {
            // The managed service is intentionally never-throwing. Preserve
            // the output accumulated before an allocation or conversion error.
        }
        return result;
    }

    std::wstring FromMorse(std::wstring_view morse) noexcept
    {
        std::wstring result;
        try
        {
            if (IsNullOrWhitespace(morse)) return result;

            std::wstring normalized;
            normalized.reserve(morse.size());
            for (auto const character : morse)
            {
                switch (character)
                {
                case L'\x00B7': // middle dot
                case L'\x2022': // bullet
                case L'.':
                    normalized.push_back(L'.');
                    break;
                case L'\x2013': // en dash
                case L'\x2014': // em dash
                case L'_':
                case L'-':
                    normalized.push_back(L'-');
                    break;
                case L'|':
                    normalized.push_back(L'/');
                    break;
                default:
                    normalized.push_back(character);
                    break;
                }
            }

            auto firstWord = true;
            auto wordStart = std::size_t{};
            for (std::size_t index{}; index <= normalized.size(); ++index)
            {
                if (index != normalized.size() && normalized[index] != L'/') continue;

                auto const word = TrimDotNetWhitespace(
                    std::wstring_view(normalized).substr(wordStart, index - wordStart));
                if (!word.empty())
                {
                    if (!firstWord) result.push_back(L' ');
                    firstWord = false;

                    // Preserve the managed page's implementation detail:
                    // only space and tab split Morse letters. Newlines trim at
                    // the outer edge but remain part of an internal token.
                    auto tokenStart = std::size_t{};
                    for (std::size_t tokenEnd{}; tokenEnd <= word.size(); ++tokenEnd)
                    {
                        if (tokenEnd != word.size() && word[tokenEnd] != L' ' && word[tokenEnd] != L'\t') continue;
                        if (tokenStart < tokenEnd)
                        {
                            auto const token = word.substr(tokenStart, tokenEnd - tokenStart);
                            if (token == L"#")
                            {
                                result.push_back(L'#');
                            }
                            else if (auto const decoded = Decode(token); decoded != L'\0')
                            {
                                result.push_back(decoded);
                            }
                            else
                            {
                                result.push_back(L'\xFFFD');
                            }
                        }
                        tokenStart = tokenEnd + 1;
                    }
                }
                wordStart = index + 1;
            }
        }
        catch (...)
        {
            // Match the managed never-throwing helper and retain accumulated
            // output when possible.
        }
        return result;
    }

    std::vector<Flash> BuildTimeline(std::wstring_view text) noexcept
    {
        std::vector<Flash> result;
        try
        {
            if (text.empty()) return result;
            auto const words = SplitTextWords(text);
            for (std::size_t wordIndex{}; wordIndex < words.size(); ++wordIndex)
            {
                if (wordIndex != 0) AddGap(result, 7);
                auto firstLetter = true;
                for (auto const raw : words[wordIndex])
                {
                    auto const code = CodeFor(raw);
                    if (code.empty()) continue;
                    if (!firstLetter) AddGap(result, 3);
                    firstLetter = false;
                    for (std::size_t signalIndex{}; signalIndex < code.size(); ++signalIndex)
                    {
                        if (signalIndex != 0) AddGap(result, 1);
                        result.push_back({ true, code[signalIndex] == L'-' ? 3 : 1 });
                    }
                }
            }
        }
        catch (...)
        {
            // Keep any timeline segments accumulated before a recoverable
            // allocation error, just as the managed helper keeps its list.
        }
        return result;
    }

    double UnitMsForWpm(double wpm) noexcept
    {
        try
        {
            if (std::isnan(wpm) || wpm < 1.0) wpm = 1.0;
            if (wpm > 60.0) wpm = 60.0;
            return 1200.0 / wpm;
        }
        catch (...)
        {
            return 200.0;
        }
    }
}
