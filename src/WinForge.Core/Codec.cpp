#include "Codec.h"

#include <algorithm>
#include <cstdint>
#include <limits>
#include <string>
#include <vector>

namespace
{
    using winforge::core::codec::Encoding;
    using winforge::core::codec::Result;

    constexpr std::uint32_t Replacement = 0xFFFD;
    constexpr std::string_view Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    constexpr std::string_view Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    [[nodiscard]] bool IsHigh(wchar_t value) { return value >= 0xD800 && value <= 0xDBFF; }
    [[nodiscard]] bool IsLow(wchar_t value) { return value >= 0xDC00 && value <= 0xDFFF; }
    [[nodiscard]] bool IsContinuation(std::uint8_t value) { return (value & 0xC0) == 0x80; }

    void AppendUtf8(std::uint32_t cp, std::vector<std::uint8_t>& bytes)
    {
        if (cp <= 0x7F) bytes.push_back(static_cast<std::uint8_t>(cp));
        else if (cp <= 0x7FF)
        {
            bytes.push_back(static_cast<std::uint8_t>(0xC0 | (cp >> 6)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | (cp & 0x3F)));
        }
        else if (cp <= 0xFFFF)
        {
            bytes.push_back(static_cast<std::uint8_t>(0xE0 | (cp >> 12)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | ((cp >> 6) & 0x3F)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | (cp & 0x3F)));
        }
        else
        {
            bytes.push_back(static_cast<std::uint8_t>(0xF0 | (cp >> 18)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | ((cp >> 12) & 0x3F)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | ((cp >> 6) & 0x3F)));
            bytes.push_back(static_cast<std::uint8_t>(0x80 | (cp & 0x3F)));
        }
    }

    [[nodiscard]] std::vector<std::uint8_t> Utf8Encode(std::wstring_view text)
    {
        std::vector<std::uint8_t> bytes;
        bytes.reserve(text.size() * 2);
        for (std::size_t i = 0; i < text.size(); ++i)
        {
            auto const unit = static_cast<std::uint32_t>(text[i]);
            if (IsHigh(text[i]))
            {
                if (i + 1 < text.size() && IsLow(text[i + 1]))
                {
                    auto const low = static_cast<std::uint32_t>(text[++i]);
                    AppendUtf8(0x10000 + ((unit - 0xD800) << 10) + (low - 0xDC00), bytes);
                }
                else AppendUtf8(Replacement, bytes);
            }
            else if (IsLow(text[i])) AppendUtf8(Replacement, bytes);
            else AppendUtf8(unit, bytes);
        }
        return bytes;
    }

    void AppendUtf16(std::wstring& text, std::uint32_t cp)
    {
        if (cp <= 0xFFFF)
        {
            text.push_back(static_cast<wchar_t>(cp));
            return;
        }
        cp -= 0x10000;
        text.push_back(static_cast<wchar_t>(0xD800 + (cp >> 10)));
        text.push_back(static_cast<wchar_t>(0xDC00 + (cp & 0x3FF)));
    }

    [[nodiscard]] std::wstring Utf8Decode(std::vector<std::uint8_t> const& bytes)
    {
        std::wstring text;
        text.reserve(bytes.size());
        for (std::size_t i = 0; i < bytes.size();)
        {
            auto const lead = bytes[i];
            if (lead <= 0x7F) { AppendUtf16(text, lead); ++i; continue; }
            std::size_t count{};
            std::uint32_t cp{};
            if (lead >= 0xC2 && lead <= 0xDF) { count = 1; cp = lead & 0x1F; }
            else if (lead >= 0xE0 && lead <= 0xEF) { count = 2; cp = lead & 0x0F; }
            else if (lead >= 0xF0 && lead <= 0xF4) { count = 3; cp = lead & 0x07; }
            else { AppendUtf16(text, Replacement); ++i; continue; }

            auto const available = std::min(count, bytes.size() - i - 1);
            bool valid = true;
            for (std::size_t j = 1; j <= available; ++j)
            {
                if (!IsContinuation(bytes[i + j])) { valid = false; break; }
                cp = (cp << 6) | (bytes[i + j] & 0x3F);
            }
            if (!valid || available < count)
            {
                AppendUtf16(text, Replacement);
                i += available + 1;
                continue;
            }
            auto const minimum = count == 1 ? 0x80u : count == 2 ? 0x800u : 0x10000u;
            if (cp < minimum || cp > 0x10FFFF || (cp >= 0xD800 && cp <= 0xDFFF))
            {
                AppendUtf16(text, Replacement);
                ++i;
                continue;
            }
            AppendUtf16(text, cp);
            i += count + 1;
        }
        return text;
    }

    [[nodiscard]] std::string Base32Encode(std::vector<std::uint8_t> const& bytes, bool pad)
    {
        std::string output;
        if (bytes.empty()) return output;
        output.reserve(((bytes.size() + 4) / 5) * 8);
        std::uint32_t buffer{};
        int bits{};
        for (auto byte : bytes)
        {
            buffer = (buffer << 8) | byte;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output.push_back(Base32Alphabet[(buffer >> bits) & 0x1F]);
            }
        }
        if (bits > 0) output.push_back(Base32Alphabet[(buffer << (5 - bits)) & 0x1F]);
        if (pad) while (output.size() % 8 != 0) output.push_back('=');
        return output;
    }

    [[nodiscard]] int Base32Value(char value)
    {
        if (value >= 'a' && value <= 'z') value = static_cast<char>(value - 'a' + 'A');
        auto const index = Base32Alphabet.find(value);
        return index == std::string_view::npos ? -1 : static_cast<int>(index);
    }

    [[nodiscard]] bool Base32Decode(std::string_view input, std::vector<std::uint8_t>& bytes)
    {
        std::uint32_t buffer{};
        int bits{};
        for (char value : input)
        {
            if (value == '=' || value == '-' || value == ' ' || value == '\t' || value == '\r' || value == '\n') continue;
            auto const digit = Base32Value(value);
            if (digit < 0) return false;
            buffer = (buffer << 5) | static_cast<std::uint32_t>(digit);
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.push_back(static_cast<std::uint8_t>((buffer >> bits) & 0xFF));
            }
        }
        return true;
    }

    [[nodiscard]] std::string Base58Encode(std::vector<std::uint8_t> const& bytes)
    {
        if (bytes.empty()) return {};
        std::size_t zeros{};
        while (zeros < bytes.size() && bytes[zeros] == 0) ++zeros;
        std::vector<std::uint8_t> digits(1, 0);
        for (auto byte : bytes)
        {
            unsigned carry = byte;
            for (auto it = digits.rbegin(); it != digits.rend(); ++it)
            {
                carry += static_cast<unsigned>(*it) << 8;
                *it = static_cast<std::uint8_t>(carry % 58);
                carry /= 58;
            }
            while (carry != 0) { digits.insert(digits.begin(), static_cast<std::uint8_t>(carry % 58)); carry /= 58; }
        }
        std::string output(zeros, '1');
        auto first = digits.begin();
        while (first != digits.end() && *first == 0) ++first;
        for (; first != digits.end(); ++first) output.push_back(Base58Alphabet[*first]);
        return output;
    }

    [[nodiscard]] int Base58Value(char value)
    {
        auto const index = Base58Alphabet.find(value);
        return index == std::string_view::npos ? -1 : static_cast<int>(index);
    }

    [[nodiscard]] bool Base58Decode(std::string_view input, std::vector<std::uint8_t>& bytes)
    {
        if (input.empty()) return true;
        std::vector<std::uint8_t> digits(1, 0);
        std::size_t leading{};
        bool sawNonLeading{};
        for (char value : input)
        {
            if (value == ' ' || value == '\t' || value == '\r' || value == '\n') continue;
            auto const digit = Base58Value(value);
            if (digit < 0) return false;
            if (!sawNonLeading && digit == 0) ++leading; else sawNonLeading = true;
            unsigned carry = static_cast<unsigned>(digit);
            for (auto it = digits.rbegin(); it != digits.rend(); ++it)
            {
                carry += static_cast<unsigned>(*it) * 58;
                *it = static_cast<std::uint8_t>(carry & 0xFF);
                carry >>= 8;
            }
            while (carry != 0) { digits.insert(digits.begin(), static_cast<std::uint8_t>(carry & 0xFF)); carry >>= 8; }
        }
        auto first = digits.begin();
        while (first != digits.end() && *first == 0) ++first;
        bytes.assign(first, digits.end());
        bytes.insert(bytes.begin(), leading, 0);
        return true;
    }

    [[nodiscard]] std::string Ascii85Encode(std::vector<std::uint8_t> const& bytes)
    {
        if (bytes.empty()) return "<~~>";
        std::string output = "<~";
        for (std::size_t offset = 0; offset < bytes.size(); offset += 4)
        {
            auto const count = std::min<std::size_t>(4, bytes.size() - offset);
            std::uint32_t tuple{};
            for (std::size_t j = 0; j < 4; ++j) tuple = (tuple << 8) | (j < count ? bytes[offset + j] : 0);
            if (count == 4 && tuple == 0) { output.push_back('z'); continue; }
            char group[5]{};
            for (int j = 4; j >= 0; --j) { group[j] = static_cast<char>('!' + tuple % 85); tuple /= 85; }
            output.append(group, count + 1);
        }
        output += "~>";
        return output;
    }

    [[nodiscard]] bool Ascii85Decode(std::string_view input, std::vector<std::uint8_t>& bytes)
    {
        std::string body(input);
        while (!body.empty() && (body.front() == ' ' || body.front() == '\t' || body.front() == '\r' || body.front() == '\n')) body.erase(body.begin());
        while (!body.empty() && (body.back() == ' ' || body.back() == '\t' || body.back() == '\r' || body.back() == '\n')) body.pop_back();
        if (body.rfind("<~", 0) == 0) body.erase(0, 2);
        if (auto const end = body.find("~>"); end != std::string::npos) body.erase(end);
        std::uint64_t tuple{};
        int count{};
        for (char value : body)
        {
            if (value == ' ' || value == '\t' || value == '\r' || value == '\n') continue;
            if (value == 'z')
            {
                if (count != 0) return false;
                bytes.insert(bytes.end(), { 0, 0, 0, 0 });
                continue;
            }
            if (value < '!' || value > 'u') return false;
            tuple = tuple * 85 + static_cast<unsigned>(value - '!');
            if (++count == 5)
            {
                if (tuple > 0xFFFFFFFFull) return false;
                bytes.push_back(static_cast<std::uint8_t>(tuple >> 24));
                bytes.push_back(static_cast<std::uint8_t>(tuple >> 16));
                bytes.push_back(static_cast<std::uint8_t>(tuple >> 8));
                bytes.push_back(static_cast<std::uint8_t>(tuple));
                tuple = 0;
                count = 0;
            }
        }
        if (count == 1) return false;
        if (count > 0)
        {
            for (int j = count; j < 5; ++j) tuple = tuple * 85 + 84;
            if (tuple > 0xFFFFFFFFull) return false;
            for (int j = 0; j < count - 1; ++j) bytes.push_back(static_cast<std::uint8_t>(tuple >> (24 - j * 8)));
        }
        return true;
    }

    [[nodiscard]] bool IsUnicodeWhitespace(wchar_t value)
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] std::string Narrow(std::wstring_view text)
    {
        std::string output;
        output.reserve(text.size());
        for (auto value : text)
        {
            if (IsUnicodeWhitespace(value)) output.push_back(' ');
            else output.push_back(value <= 0x7F ? static_cast<char>(value) : '?');
        }
        return output;
    }

    [[nodiscard]] Result Failure(std::wstring_view error)
    {
        return Result{ false, {}, std::wstring(error) };
    }
}

namespace winforge::core::codec
{
    Result Encode(std::wstring_view text, Encoding encoding)
    {
        auto const bytes = Utf8Encode(text);
        std::string output;
        switch (encoding)
        {
        case Encoding::Base32: output = Base32Encode(bytes, true); break;
        case Encoding::Base32NoPad: output = Base32Encode(bytes, false); break;
        case Encoding::Base58: output = Base58Encode(bytes); break;
        case Encoding::Ascii85: output = Ascii85Encode(bytes); break;
        default: return Failure(L"unknown codec");
        }
        return Result{ true, std::wstring(output.begin(), output.end()), {} };
    }

    Result Decode(std::wstring_view text, Encoding encoding)
    {
        auto const input = Narrow(text);
        std::vector<std::uint8_t> bytes;
        bool ok = false;
        switch (encoding)
        {
        case Encoding::Base32:
        case Encoding::Base32NoPad: ok = Base32Decode(input, bytes); break;
        case Encoding::Base58: ok = Base58Decode(input, bytes); break;
        case Encoding::Ascii85: ok = Ascii85Decode(input, bytes); break;
        default: return Failure(L"unknown codec");
        }
        return ok ? Result{ true, Utf8Decode(bytes), {} } : Failure(L"invalid codec input");
    }
}
