#include "BaseConvert.h"

#include <algorithm>
#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <limits>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

namespace
{
    using winforge::core::baseconvert::Integer;

    [[nodiscard]] bool IsManagedWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] int DigitValue(wchar_t value) noexcept
    {
        if (value >= L'0' && value <= L'9') return value - L'0';
        if (value >= L'a' && value <= L'z') return value - L'a' + 10;
        if (value >= L'A' && value <= L'Z') return value - L'A' + 10;
        return -1;
    }

    void NormalizeMagnitude(Integer& value) noexcept
    {
        while (!value.m_words.empty() && value.m_words.back() == 0)
        {
            value.m_words.pop_back();
        }
        if (value.m_words.empty()) value.m_negative = false;
    }

    void MultiplySmall(Integer& value, std::uint32_t factor)
    {
        if (value.m_words.empty() || factor == 1) return;
        if (factor == 0)
        {
            value.m_words.clear();
            value.m_negative = false;
            return;
        }
        std::uint64_t carry{};
        for (auto& word : value.m_words)
        {
            auto const product = static_cast<std::uint64_t>(word) * factor + carry;
            word = static_cast<std::uint32_t>(product);
            carry = product >> 32;
        }
        if (carry != 0) value.m_words.push_back(static_cast<std::uint32_t>(carry));
    }

    void AddSmall(Integer& value, std::uint32_t addend)
    {
        if (addend == 0) return;
        if (value.m_words.empty())
        {
            value.m_words.push_back(addend);
            return;
        }
        std::uint64_t carry = addend;
        for (auto& word : value.m_words)
        {
            auto const sum = static_cast<std::uint64_t>(word) + carry;
            word = static_cast<std::uint32_t>(sum);
            carry = sum >> 32;
            if (carry == 0) return;
        }
        value.m_words.push_back(static_cast<std::uint32_t>(carry));
    }

    [[nodiscard]] std::uint32_t DivideSmall(Integer& value, std::uint32_t divisor) noexcept
    {
        std::uint64_t remainder{};
        for (auto index = value.m_words.size(); index > 0; --index)
        {
            auto const combined = (remainder << 32) | value.m_words[index - 1];
            value.m_words[index - 1] = static_cast<std::uint32_t>(combined / divisor);
            remainder = combined % divisor;
        }
        NormalizeMagnitude(value);
        return static_cast<std::uint32_t>(remainder);
    }

    [[nodiscard]] std::int64_t MagnitudeBitLength(Integer const& value) noexcept
    {
        if (value.m_words.empty()) return 0;
        auto const high = value.m_words.back();
        return static_cast<std::int64_t>((value.m_words.size() - 1) * 32 +
            (32 - std::countl_zero(high)));
    }

    [[nodiscard]] bool MagnitudeFits(Integer const& value, std::uint64_t maximum) noexcept
    {
        if (value.m_words.size() > 2) return false;
        std::uint64_t magnitude{};
        if (!value.m_words.empty()) magnitude = value.m_words[0];
        if (value.m_words.size() == 2) magnitude |= static_cast<std::uint64_t>(value.m_words[1]) << 32;
        return magnitude <= maximum;
    }

    [[nodiscard]] std::uint64_t MagnitudeAsUint64(Integer const& value) noexcept
    {
        std::uint64_t magnitude{};
        if (!value.m_words.empty()) magnitude = value.m_words[0];
        if (value.m_words.size() > 1) magnitude |= static_cast<std::uint64_t>(value.m_words[1]) << 32;
        return magnitude;
    }

    [[nodiscard]] Integer ShiftMagnitudeRight(Integer value, std::size_t shift, bool* discarded = nullptr)
    {
        if (discarded) *discarded = false;
        if (value.m_words.empty() || shift == 0) return value;
        auto const wordShift = shift / 32;
        auto const bitShift = static_cast<unsigned int>(shift % 32);
        if (wordShift >= value.m_words.size())
        {
            if (discarded) *discarded = !value.m_words.empty();
            return {};
        }
        if (discarded)
        {
            for (std::size_t index{}; index < wordShift; ++index)
            {
                if (value.m_words[index] != 0)
                {
                    *discarded = true;
                    break;
                }
            }
            if (!*discarded && bitShift != 0 && (value.m_words[wordShift] & ((std::uint32_t{ 1 } << bitShift) - 1)) != 0)
            {
                *discarded = true;
            }
        }
        std::vector<std::uint32_t> result(value.m_words.size() - wordShift);
        for (std::size_t source = wordShift; source < value.m_words.size(); ++source)
        {
            auto const target = source - wordShift;
            std::uint64_t current = value.m_words[source];
            if (bitShift != 0 && source + 1 < value.m_words.size())
            {
                current |= static_cast<std::uint64_t>(value.m_words[source + 1]) << 32;
            }
            result[target] = static_cast<std::uint32_t>(current >> bitShift);
        }
        value.m_words = std::move(result);
        NormalizeMagnitude(value);
        return value;
    }

    [[nodiscard]] Integer ShiftMagnitudeLeft(Integer value, std::size_t shift)
    {
        if (value.m_words.empty() || shift == 0) return value;
        auto const wordShift = shift / 32;
        auto const bitShift = static_cast<unsigned int>(shift % 32);
        std::vector<std::uint32_t> result(wordShift, 0);
        result.reserve(value.m_words.size() + wordShift + 1);
        std::uint64_t carry{};
        for (auto const word : value.m_words)
        {
            auto const shifted = (static_cast<std::uint64_t>(word) << bitShift) | carry;
            result.push_back(static_cast<std::uint32_t>(shifted));
            carry = shifted >> 32;
        }
        if (carry != 0) result.push_back(static_cast<std::uint32_t>(carry));
        value.m_words = std::move(result);
        return value;
    }

    [[nodiscard]] std::vector<std::uint32_t> ToTwosComplement(Integer const& value, std::size_t wordCount)
    {
        std::vector<std::uint32_t> words(wordCount, 0);
        auto const copied = std::min(wordCount, value.m_words.size());
        std::copy_n(value.m_words.begin(), copied, words.begin());
        if (!value.m_negative) return words;

        for (auto& word : words) word = ~word;
        std::uint64_t carry = 1;
        for (auto& word : words)
        {
            auto const sum = static_cast<std::uint64_t>(word) + carry;
            word = static_cast<std::uint32_t>(sum);
            carry = sum >> 32;
            if (carry == 0) break;
        }
        return words;
    }

    [[nodiscard]] Integer FromTwosComplement(std::vector<std::uint32_t> words)
    {
        Integer result;
        if (words.empty()) return result;
        auto const negative = (words.back() & 0x80000000u) != 0;
        if (negative)
        {
            for (auto& word : words) word = ~word;
            std::uint64_t carry = 1;
            for (auto& word : words)
            {
                auto const sum = static_cast<std::uint64_t>(word) + carry;
                word = static_cast<std::uint32_t>(sum);
                carry = sum >> 32;
                if (carry == 0) break;
            }
            result.m_negative = true;
        }
        result.m_words = std::move(words);
        NormalizeMagnitude(result);
        return result;
    }

    [[nodiscard]] Integer Bitwise(Integer const& a, Integer const& b, winforge::core::baseconvert::BitOp op)
    {
        auto const requiredBits = static_cast<std::size_t>(std::max(MagnitudeBitLength(a), MagnitudeBitLength(b)) + 1);
        auto const wordCount = std::max<std::size_t>(1, (requiredBits + 31) / 32);
        auto left = ToTwosComplement(a, wordCount);
        auto right = ToTwosComplement(b, wordCount);
        for (std::size_t index{}; index < wordCount; ++index)
        {
            switch (op)
            {
            case winforge::core::baseconvert::BitOp::And: left[index] &= right[index]; break;
            case winforge::core::baseconvert::BitOp::Or: left[index] |= right[index]; break;
            case winforge::core::baseconvert::BitOp::Xor: left[index] ^= right[index]; break;
            case winforge::core::baseconvert::BitOp::Nand: left[index] = ~(left[index] & right[index]); break;
            case winforge::core::baseconvert::BitOp::Nor: left[index] = ~(left[index] | right[index]); break;
            default: break;
            }
        }
        return FromTwosComplement(std::move(left));
    }
}

namespace winforge::core::baseconvert
{
    std::wstring_view TrimManagedWhitespace(std::wstring_view value) noexcept
    {
        while (!value.empty() && IsManagedWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsManagedWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    bool Integer::IsZero() const noexcept
    {
        return m_words.empty();
    }

    bool Integer::IsNegative() const noexcept
    {
        return m_negative;
    }

    void Integer::Normalize() noexcept
    {
        NormalizeMagnitude(*this);
    }

    bool IsBlank(std::wstring_view text) noexcept
    {
        return TrimManagedWhitespace(text).empty();
    }

    bool TryParse(std::wstring_view text, int radix, Integer& value) noexcept
    {
        try
        {
            value = {};
            if (radix < MinBase || radix > MaxBase) return false;
            text = TrimManagedWhitespace(text);
            if (text.empty()) return false;

            bool negative{};
            std::size_t index{};
            if (text.front() == L'+' || text.front() == L'-')
            {
                negative = text.front() == L'-';
                index = 1;
            }

            bool sawDigit{};
            Integer parsed;
            for (; index < text.size(); ++index)
            {
                auto const character = text[index];
                if (character == L'_' || character == L' ') continue;
                auto const digit = DigitValue(character);
                if (digit < 0 || digit >= radix) return false;
                MultiplySmall(parsed, static_cast<std::uint32_t>(radix));
                AddSmall(parsed, static_cast<std::uint32_t>(digit));
                sawDigit = true;
            }
            if (!sawDigit) return false;
            parsed.m_negative = negative && !parsed.m_words.empty();
            value = std::move(parsed);
            return true;
        }
        catch (...)
        {
            value = {};
            return false;
        }
    }

    bool TryParseOperand(std::wstring_view text, Integer& value) noexcept
    {
        try
        {
            value = {};
            auto trimmed = TrimManagedWhitespace(text);
            if (trimmed.empty()) return false;

            bool negative{};
            if (trimmed.front() == L'+')
            {
                trimmed.remove_prefix(1);
            }
            else if (trimmed.front() == L'-')
            {
                negative = true;
                trimmed.remove_prefix(1);
            }

            Integer parsed;
            auto const hexadecimal = trimmed.size() >= 2 && trimmed[0] == L'0' &&
                (trimmed[1] == L'x' || trimmed[1] == L'X');
            if (hexadecimal)
            {
                trimmed.remove_prefix(2);
                if (!TryParse(trimmed, 16, parsed)) return false;
            }
            else if (!TryParse(trimmed, 10, parsed))
            {
                return false;
            }
            // The managed helper first parses the remaining literal (which
            // itself accepts a sign) and only then applies an outer '-'.
            // Preserve that intentionally permissive nested-sign contract.
            if (negative && !parsed.m_words.empty()) parsed.m_negative = !parsed.m_negative;
            value = std::move(parsed);
            return true;
        }
        catch (...)
        {
            value = {};
            return false;
        }
    }

    std::wstring ToBase(Integer const& value, int radix) noexcept
    {
        try
        {
            if (radix < MinBase || radix > MaxBase) return {};
            if (value.m_words.empty()) return L"0";

            Integer work = value;
            work.m_negative = false;
            std::wstring result;
            while (!work.m_words.empty())
            {
                auto const digit = DivideSmall(work, static_cast<std::uint32_t>(radix));
                result.push_back(static_cast<wchar_t>(digit < 10 ? L'0' + digit : L'a' + (digit - 10)));
            }
            std::reverse(result.begin(), result.end());
            if (value.m_negative) result.insert(result.begin(), L'-');
            return result;
        }
        catch (...)
        {
            return {};
        }
    }

    std::wstring ToGroupedBinary(Integer const& value) noexcept
    {
        try
        {
            Integer magnitude = value;
            magnitude.m_negative = false;
            auto bits = ToBase(magnitude, 2);
            auto const padding = (4 - (bits.size() % 4)) % 4;
            bits.insert(0, padding, L'0');
            std::wstring result;
            result.reserve(bits.size() + bits.size() / 4 + 1);
            for (std::size_t index{}; index < bits.size(); ++index)
            {
                if (index != 0 && index % 4 == 0) result.push_back(L' ');
                result.push_back(bits[index]);
            }
            if (value.m_negative) result.insert(result.begin(), L'-');
            return result;
        }
        catch (...)
        {
            return {};
        }
    }

    std::wstring ToHexPrefixed(Integer const& value) noexcept
    {
        try
        {
            Integer magnitude = value;
            magnitude.m_negative = false;
            auto hex = ToBase(magnitude, 16);
            std::transform(hex.begin(), hex.end(), hex.begin(), [](wchar_t value)
            {
                return value >= L'a' && value <= L'z' ? static_cast<wchar_t>(value - L'a' + L'A') : value;
            });
            return value.m_negative ? L"-0x" + hex : L"0x" + hex;
        }
        catch (...)
        {
            return {};
        }
    }

    std::int64_t BitLength(Integer const& value) noexcept
    {
        return MagnitudeBitLength(value);
    }

    bool FitsIn64Bits(Integer const& value) noexcept
    {
        return MagnitudeFits(value, value.m_negative ? (std::uint64_t{ 1 } << 63) :
            static_cast<std::uint64_t>(std::numeric_limits<std::int64_t>::max()));
    }

    std::wstring To64BitBinary(Integer const& value) noexcept
    {
        if (!FitsIn64Bits(value)) return {};
        auto const magnitude = MagnitudeAsUint64(value);
        std::uint64_t bits{};
        if (value.m_negative)
        {
            bits = ~magnitude + 1;
        }
        else
        {
            bits = magnitude;
        }
        std::wstring result;
        result.reserve(71);
        for (int bit = 63; bit >= 0; --bit)
        {
            result.push_back(((bits >> bit) & 1u) != 0 ? L'1' : L'0');
            if (bit % 8 == 0 && bit != 0) result.push_back(L' ');
        }
        return result;
    }

    Integer Evaluate(BitOp op, Integer const& a, Integer const& b, int shift) noexcept
    {
        try
        {
            if (shift < 0) shift = 0;
            switch (op)
            {
            case BitOp::And:
            case BitOp::Or:
            case BitOp::Xor:
            case BitOp::Nand:
            case BitOp::Nor:
                return Bitwise(a, b, op);
            case BitOp::LeftShift:
            {
                auto result = ShiftMagnitudeLeft(a, static_cast<std::size_t>(shift));
                result.m_negative = a.m_negative && !result.m_words.empty();
                return result;
            }
            case BitOp::RightShift:
            {
                bool discarded{};
                auto result = ShiftMagnitudeRight(a, static_cast<std::size_t>(shift), &discarded);
                if (a.m_negative && (!result.m_words.empty() || discarded))
                {
                    if (discarded) AddSmall(result, 1);
                    result.m_negative = !result.m_words.empty();
                }
                return result;
            }
            default:
                return {};
            }
        }
        catch (...)
        {
            return {};
        }
    }
}
