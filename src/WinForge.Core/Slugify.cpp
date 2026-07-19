#include "Slugify.h"

#include <Windows.h>
#include <icu.h>

#include <algorithm>
#include <cstdint>
#include <optional>
#include <string>
#include <string_view>

#pragma comment(lib, "icu.lib")

namespace
{
    using namespace winforge::core::slugify;

    static_assert(sizeof(wchar_t) == sizeof(UChar),
        "WinForge native text cores require UTF-16 wchar_t on Windows.");

    [[nodiscard]] bool IsHighSurrogate(wchar_t value) noexcept
    {
        return value >= static_cast<wchar_t>(0xD800u) && value <= static_cast<wchar_t>(0xDBFFu);
    }

    [[nodiscard]] bool IsLowSurrogate(wchar_t value) noexcept
    {
        return value >= static_cast<wchar_t>(0xDC00u) && value <= static_cast<wchar_t>(0xDFFFu);
    }

    [[nodiscard]] bool HasMalformedUtf16(std::wstring_view value) noexcept
    {
        for (std::size_t index{}; index < value.size(); ++index)
        {
            if (IsHighSurrogate(value[index]))
            {
                if (index + 1 >= value.size() || !IsLowSurrogate(value[index + 1])) return true;
                ++index;
            }
            else if (IsLowSurrogate(value[index]))
            {
                return true;
            }
        }
        return false;
    }

    [[nodiscard]] std::optional<std::wstring> Normalize(
        std::wstring_view input,
        UNormalizer2 const* normalizer) noexcept
    {
        try
        {
            UErrorCode error = U_ZERO_ERROR;
            auto const required = unorm2_normalize(
                normalizer,
                reinterpret_cast<UChar const*>(input.data()),
                static_cast<int32_t>(input.size()),
                nullptr,
                0,
                &error);
            if (error != U_BUFFER_OVERFLOW_ERROR && U_FAILURE(error)) return std::nullopt;

            std::wstring output(static_cast<std::size_t>(required), L'\0');
            error = U_ZERO_ERROR;
            auto const actual = unorm2_normalize(
                normalizer,
                reinterpret_cast<UChar const*>(input.data()),
                static_cast<int32_t>(input.size()),
                reinterpret_cast<UChar*>(output.data()),
                required,
                &error);
            if (U_FAILURE(error) || actual < 0) return std::nullopt;
            output.resize(static_cast<std::size_t>(actual));
            return output;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    [[nodiscard]] std::optional<std::wstring> StripDiacritics(std::wstring_view input) noexcept
    {
        if (HasMalformedUtf16(input)) return std::nullopt;

        UErrorCode error = U_ZERO_ERROR;
        auto const nfd = unorm2_getNFDInstance(&error);
        if (U_FAILURE(error) || !nfd) return std::nullopt;
        auto decomposed = Normalize(input, nfd);
        if (!decomposed) return std::nullopt;

        std::wstring withoutMarks;
        withoutMarks.reserve(decomposed->size());
        for (auto const value : *decomposed)
        {
            if (u_charType(static_cast<UChar>(value)) != U_NON_SPACING_MARK)
            {
                withoutMarks.push_back(value);
            }
        }

        error = U_ZERO_ERROR;
        auto const nfc = unorm2_getNFCInstance(&error);
        if (U_FAILURE(error) || !nfc) return std::nullopt;
        return Normalize(withoutMarks, nfc);
    }

    [[nodiscard]] bool IsManagedLetterOrDigit(wchar_t value) noexcept
    {
        auto const category = u_charType(static_cast<UChar>(value));
        switch (category)
        {
        case U_UPPERCASE_LETTER:
        case U_LOWERCASE_LETTER:
        case U_TITLECASE_LETTER:
        case U_MODIFIER_LETTER:
        case U_OTHER_LETTER:
        case U_DECIMAL_DIGIT_NUMBER:
            return true;
        default:
            return false;
        }
    }

    [[nodiscard]] wchar_t MapInvariantChar(wchar_t value, DWORD mapFlags) noexcept
    {
        wchar_t mapped = value;
        auto const mappedLength = LCMapStringEx(
            LOCALE_NAME_INVARIANT,
            mapFlags,
            &value,
            1,
            &mapped,
            1,
            nullptr,
            nullptr,
            0);
        return mappedLength == 1 ? mapped : value;
    }

    [[nodiscard]] wchar_t ToLowerInvariantChar(wchar_t value) noexcept
    {
        // .NET's invariant simple casing keeps dotted capital I as a single
        // UTF-16 code unit rather than expanding it to i + combining dot.
        if (value == static_cast<wchar_t>(0x0130u)) return value;
        return MapInvariantChar(value, LCMAP_LOWERCASE);
    }

    [[nodiscard]] wchar_t ToUpperInvariantChar(wchar_t value) noexcept
    {
        // The managed simple-char conversion preserves these values rather
        // than using a multi-unit uppercase expansion.
        if (value == static_cast<wchar_t>(0x0131u) || value == static_cast<wchar_t>(0x00DFu)) return value;
        return MapInvariantChar(value, LCMAP_UPPERCASE);
    }

    [[nodiscard]] std::wstring ApplyCase(std::wstring value, LetterCase letterCase) noexcept
    {
        try
        {
            // The managed switch expression leaves unknown enum values alone.
            // Keep that behavior for callers that construct Options outside the UI.
            if (letterCase != LetterCase::Lower && letterCase != LetterCase::Upper) return value;
            for (auto& character : value)
            {
                character = letterCase == LetterCase::Upper
                    ? ToUpperInvariantChar(character)
                    : ToLowerInvariantChar(character);
            }
            return value;
        }
        catch (...)
        {
            return {};
        }
    }

    [[nodiscard]] bool IsManagedWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] std::wstring_view TrimManagedWhitespace(std::wstring_view value) noexcept
    {
        while (!value.empty() && IsManagedWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsManagedWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    void TrimSeparator(std::wstring& value, wchar_t separator) noexcept
    {
        auto const first = value.find_first_not_of(separator);
        if (first == std::wstring::npos)
        {
            value.clear();
            return;
        }
        auto const last = value.find_last_not_of(separator);
        value = value.substr(first, last - first + 1);
    }
}

namespace winforge::core::slugify
{
    wchar_t SeparatorCharacter(Separator separator) noexcept
    {
        switch (separator)
        {
        case Separator::Underscore: return L'_';
        case Separator::Dot: return L'.';
        case Separator::Hyphen:
        default: return L'-';
        }
    }

    std::wstring Slugify(std::wstring_view input, Options const& options) noexcept
    {
        try
        {
            auto const separator = SeparatorCharacter(options.separator);
            std::wstring source;
            if (options.stripDiacritics)
            {
                auto stripped = StripDiacritics(input);
                if (!stripped) return {};
                source = std::move(*stripped);
            }
            else
            {
                source.assign(input);
            }

            std::wstring output;
            output.reserve(source.size());
            bool pendingSeparator{};
            for (auto const character : source)
            {
                bool keep = (character >= L'a' && character <= L'z') ||
                    (character >= L'A' && character <= L'Z') ||
                    (character >= L'0' && character <= L'9');
                if (!keep && options.keepUnicodeLetters && character > 127)
                {
                    keep = IsManagedLetterOrDigit(character);
                }

                if (keep)
                {
                    if (pendingSeparator && !output.empty()) output.push_back(separator);
                    pendingSeparator = false;
                    output.push_back(character);
                }
                else
                {
                    pendingSeparator = true;
                }
            }

            if (options.collapseRepeats && !output.empty())
            {
                std::wstring collapsed;
                collapsed.reserve(output.size());
                wchar_t previous{};
                for (auto const character : output)
                {
                    if (character == separator && previous == separator) continue;
                    collapsed.push_back(character);
                    previous = character;
                }
                output = std::move(collapsed);
            }

            TrimSeparator(output, separator);
            output = ApplyCase(std::move(output), options.letterCase);
            if (options.maxLength > 0 && output.size() > static_cast<std::size_t>(options.maxLength))
            {
                output.resize(static_cast<std::size_t>(options.maxLength));
                TrimSeparator(output, separator);
            }
            return output;
        }
        catch (...)
        {
            return {};
        }
    }

    std::wstring SlugifyBlock(std::wstring_view input, Options const& options) noexcept
    {
        try
        {
            if (input.empty()) return {};

            std::wstring output;
            bool firstOutput{ true };
            std::size_t lineStart{};
            for (std::size_t index{}; index <= input.size(); ++index)
            {
                auto const atEnd = index == input.size();
                auto const atBreak = !atEnd && (input[index] == L'\r' || input[index] == L'\n');
                if (!atEnd && !atBreak) continue;

                auto const line = input.substr(lineStart, index - lineStart);
                if (!TrimManagedWhitespace(line).empty())
                {
                    if (!firstOutput) output.push_back(L'\n');
                    output.append(Slugify(line, options));
                    firstOutput = false;
                }

                if (!atEnd && input[index] == L'\r' && index + 1 < input.size() && input[index + 1] == L'\n')
                {
                    ++index;
                }
                lineStart = index + 1;
            }
            return output;
        }
        catch (...)
        {
            return {};
        }
    }

    Preview PreviewFirstLine(std::wstring_view input, Options const& options) noexcept
    {
        try
        {
            std::size_t lineStart{};
            for (std::size_t index{}; index <= input.size(); ++index)
            {
                if (index != input.size() && input[index] != L'\r' && input[index] != L'\n') continue;
                auto const line = TrimManagedWhitespace(input.substr(lineStart, index - lineStart));
                if (!line.empty())
                {
                    Preview preview;
                    preview.input.assign(line);
                    preview.slug = Slugify(line, options);
                    preview.hasInput = true;
                    return preview;
                }
                lineStart = index + 1;
            }
        }
        catch (...)
        {
        }
        return {};
    }
}
