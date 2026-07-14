#include "BinaryText.h"

#include <algorithm>
#include <cstdint>
#include <limits>
#include <vector>

namespace
{
    using winforge::core::binarytext::Error;
    using winforge::core::binarytext::NumericBase;
    using winforge::core::binarytext::Result;

    constexpr std::uint32_t ReplacementCharacter = 0xFFFD;

    [[nodiscard]] bool IsHighSurrogate(wchar_t value)
    {
        return value >= 0xD800 && value <= 0xDBFF;
    }

    [[nodiscard]] bool IsLowSurrogate(wchar_t value)
    {
        return value >= 0xDC00 && value <= 0xDFFF;
    }

    [[nodiscard]] bool IsSeparator(wchar_t value)
    {
        return value == L' ' || value == L'\t' || value == L'\r' || value == L'\n' || value == L',';
    }

    [[nodiscard]] bool IsUnicodeWhitespace(wchar_t value)
    {
        // Mirrors Char.IsWhiteSpace, which String.Trim and IsNullOrWhiteSpace use in
        // the managed reference. The delimiter set itself deliberately stays narrower.
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] std::wstring_view TrimUnicodeWhitespace(std::wstring_view value)
    {
        while (!value.empty() && IsUnicodeWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsUnicodeWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    [[nodiscard]] bool IsContinuation(std::uint8_t value)
    {
        return (value & 0xC0) == 0x80;
    }

    void AppendUtf8(std::uint32_t codePoint, std::vector<std::uint8_t>& bytes)
    {
        if (codePoint <= 0x7F)
        {
            bytes.push_back(static_cast<std::uint8_t>(codePoint));
        }
        else if (codePoint <= 0x7FF)
        {
            bytes.push_back(static_cast<std::uint8_t>(0xC0 | (codePoint >> 6)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | (codePoint & 0x3F)));
        }
        else if (codePoint <= 0xFFFF)
        {
            bytes.push_back(static_cast<std::uint8_t>(0xE0 | (codePoint >> 12)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | ((codePoint >> 6) & 0x3F)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | (codePoint & 0x3F)));
        }
        else
        {
            bytes.push_back(static_cast<std::uint8_t>(0xF0 | (codePoint >> 18)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | ((codePoint >> 12) & 0x3F)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | ((codePoint >> 6) & 0x3F)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | (codePoint & 0x3F)));
        }
    }

    [[nodiscard]] std::vector<std::uint8_t> ToUtf8Bytes(std::wstring_view input)
    {
        std::vector<std::uint8_t> bytes;
        bytes.reserve(input.size() * 3);
        for (std::size_t index = 0; index < input.size(); ++index)
        {
            auto const unit = static_cast<std::uint32_t>(input[index]);
            if (IsHighSurrogate(input[index]))
            {
                if (index + 1 < input.size() && IsLowSurrogate(input[index + 1]))
                {
                    auto const low = static_cast<std::uint32_t>(input[++index]);
                    AppendUtf8(0x10000 + ((unit - 0xD800) << 10) + (low - 0xDC00), bytes);
                }
                else
                {
                    AppendUtf8(ReplacementCharacter, bytes);
                }
            }
            else if (IsLowSurrogate(input[index]))
            {
                AppendUtf8(ReplacementCharacter, bytes);
            }
            else
            {
                AppendUtf8(unit, bytes);
            }
        }
        return bytes;
    }

    void AppendUtf16(std::wstring& output, std::uint32_t codePoint)
    {
        if (codePoint <= 0xFFFF)
        {
            output.push_back(static_cast<wchar_t>(codePoint));
            return;
        }

        codePoint -= 0x10000;
        output.push_back(static_cast<wchar_t>(0xD800 + (codePoint >> 10)));
        output.push_back(static_cast<wchar_t>(0xDC00 + (codePoint & 0x3FF)));
    }

    [[nodiscard]] std::wstring DecodeUtf8WithReplacement(std::vector<std::uint8_t> const& bytes)
    {
        std::wstring output;
        output.reserve(bytes.size());
        for (std::size_t index = 0; index < bytes.size();)
        {
            auto const lead = bytes[index];
            if (lead <= 0x7F)
            {
                AppendUtf16(output, lead);
                ++index;
                continue;
            }

            std::size_t continuationCount{};
            std::uint32_t codePoint{};
            if (lead >= 0xC2 && lead <= 0xDF)
            {
                continuationCount = 1;
                codePoint = lead & 0x1F;
            }
            else if (lead >= 0xE0 && lead <= 0xEF)
            {
                continuationCount = 2;
                codePoint = lead & 0x0F;
            }
            else if (lead >= 0xF0 && lead <= 0xF4)
            {
                continuationCount = 3;
                codePoint = lead & 0x07;
            }
            else
            {
                AppendUtf16(output, ReplacementCharacter);
                ++index;
                continue;
            }

            // A syntactic continuation can still make the lead impossible. Encoding.UTF8
            // isolates that invalid lead before it groups a continuation prefix for fallback.
            if (index + 1 < bytes.size() && IsContinuation(bytes[index + 1]) &&
                ((lead == 0xE0 && bytes[index + 1] < 0xA0) ||
                 (lead == 0xED && bytes[index + 1] > 0x9F) ||
                 (lead == 0xF0 && bytes[index + 1] < 0x90) ||
                 (lead == 0xF4 && bytes[index + 1] > 0x8F)))
            {
                AppendUtf16(output, ReplacementCharacter);
                ++index;
                continue;
            }

            auto const availableContinuationCount = std::min(
                continuationCount,
                bytes.size() - index - 1);
            std::size_t validContinuationCount{};
            auto continuationsValid = true;
            for (std::size_t offset = 1; offset <= availableContinuationCount; ++offset)
            {
                if (!IsContinuation(bytes[index + offset]))
                {
                    continuationsValid = false;
                    break;
                }
                codePoint = (codePoint << 6) | (bytes[index + offset] & 0x3F);
                ++validContinuationCount;
            }
            if (!continuationsValid)
            {
                // Match Encoding.UTF8: the invalid lead and any valid continuation prefix are
                // one malformed unit, while the first non-continuation starts fresh decoding.
                AppendUtf16(output, ReplacementCharacter);
                index += validContinuationCount + 1;
                continue;
            }

            if (availableContinuationCount < continuationCount)
            {
                // A truncated unit with only valid continuations is one replacement fallback.
                AppendUtf16(output, ReplacementCharacter);
                index += validContinuationCount + 1;
                continue;
            }

            auto const minimum = continuationCount == 1 ? 0x80u : continuationCount == 2 ? 0x800u : 0x10000u;
            if (codePoint < minimum || codePoint > 0x10FFFF || (codePoint >= 0xD800 && codePoint <= 0xDFFF))
            {
                // The managed UTF-8 replacement decoder reports each unit of an overlong,
                // surrogate, or out-of-range sequence independently. Keep the lead isolated.
                AppendUtf16(output, ReplacementCharacter);
                ++index;
                continue;
            }

            AppendUtf16(output, codePoint);
            index += continuationCount + 1;
        }
        return output;
    }

    [[nodiscard]] std::wstring FormatByte(std::uint8_t value, NumericBase base)
    {
        auto const radix = static_cast<unsigned>(base);
        if (base == NumericBase::Binary)
        {
            std::wstring output(8, L'0');
            for (int bit = 7; bit >= 0; --bit)
            {
                output[7 - bit] = (value & (1u << bit)) == 0 ? L'0' : L'1';
            }
            return output;
        }

        wchar_t buffer[4]{};
        std::size_t count{};
        auto remaining = static_cast<unsigned>(value);
        do
        {
            auto const digit = remaining % radix;
            buffer[count++] = static_cast<wchar_t>(digit < 10 ? L'0' + digit : L'A' + digit - 10);
            remaining /= radix;
        }
        while (remaining != 0);

        std::wstring output;
        output.reserve(count + (base == NumericBase::Hex ? 1 : 0));
        while (count != 0)
        {
            output.push_back(buffer[--count]);
        }
        if (base == NumericBase::Hex && output.size() == 1)
        {
            output.insert(output.begin(), L'0');
        }
        return output;
    }

    [[nodiscard]] int DigitValue(wchar_t value)
    {
        if (value >= L'0' && value <= L'9') return value - L'0';
        if (value >= L'a' && value <= L'f') return 10 + value - L'a';
        if (value >= L'A' && value <= L'F') return 10 + value - L'A';
        return -1;
    }

    [[nodiscard]] std::wstring_view StripPrefix(std::wstring_view token, NumericBase base)
    {
        if (token.size() <= 2 || token.front() != L'0') return token;
        auto const prefix = token[1] >= L'A' && token[1] <= L'Z'
            ? static_cast<wchar_t>(token[1] - L'A' + L'a')
            : token[1];
        auto const matching = (base == NumericBase::Binary && prefix == L'b') ||
            (base == NumericBase::Octal && prefix == L'o') ||
            (base == NumericBase::Hex && prefix == L'x');
        return matching ? token.substr(2) : token;
    }

    [[nodiscard]] Result Failure(Error error, std::wstring_view token)
    {
        Result result;
        result.error = error;
        result.token = token;
        return result;
    }
}

namespace winforge::core::binarytext
{
    Result Encode(std::wstring_view input, NumericBase base)
    {
        try
        {
            auto const bytes = ToUtf8Bytes(input);
            Result result;
            result.ok = true;
            if (bytes.empty()) return result;

            for (std::size_t index = 0; index < bytes.size(); ++index)
            {
                if (index != 0) result.text.push_back(L' ');
                result.text += FormatByte(bytes[index], base);
            }
            return result;
        }
        catch (...)
        {
            return Failure(Error::Unexpected, L"");
        }
    }

    Result Decode(std::wstring_view input, NumericBase base)
    {
        try
        {
            std::vector<std::uint8_t> bytes;
            for (std::size_t index = 0; index < input.size();)
            {
                while (index < input.size() && IsSeparator(input[index])) ++index;
                auto const start = index;
                while (index < input.size() && !IsSeparator(input[index])) ++index;
                if (start == index) continue;

                auto const raw = input.substr(start, index - start);
                auto const token = StripPrefix(TrimUnicodeWhitespace(raw), base);
                if (token.empty()) continue;

                std::uint32_t value{};
                auto const radix = static_cast<unsigned>(base);
                for (auto const character : token)
                {
                    auto const digit = DigitValue(character);
                    if (digit < 0 || static_cast<unsigned>(digit) >= radix)
                    {
                        return Failure(Error::InvalidToken, raw);
                    }
                    value = value * radix + static_cast<unsigned>(digit);
                    if (value > 255)
                    {
                        return Failure(Error::OutOfRange, raw);
                    }
                }
                bytes.push_back(static_cast<std::uint8_t>(value));
            }

            Result result;
            result.ok = true;
            result.text = DecodeUtf8WithReplacement(bytes);
            return result;
        }
        catch (...)
        {
            return Failure(Error::Unexpected, L"");
        }
    }
}
