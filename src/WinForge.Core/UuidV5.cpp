#include "UuidV5.h"

#include <algorithm>
#include <array>
#include <string>
#include <utility>
#include <vector>

namespace winforge::core::uuidv5
{
    namespace
    {
        // Exact .NET char.IsWhiteSpace(char) BMP set. Do not use the UCRT
        // classification here: it includes U+180E, which string.Trim and
        // Guid.TryParse deliberately preserve/reject in the managed oracle.
        [[nodiscard]] bool IsManagedWhitespace(wchar_t value) noexcept
        {
            return (value >= L'\u0009' && value <= L'\u000D') ||
                value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
                value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
                value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
                value == L'\u205F' || value == L'\u3000';
        }

        [[nodiscard]] std::wstring Trim(std::wstring_view value)
        {
            auto first = value.begin();
            auto last = value.end();
            while (first != last && IsManagedWhitespace(*first))
            {
                ++first;
            }
            while (last != first && IsManagedWhitespace(*(last - 1)))
            {
                --last;
            }
            return std::wstring(first, last);
        }

        [[nodiscard]] int HexValue(wchar_t value)
        {
            if (value >= L'0' && value <= L'9') return value - L'0';
            if (value >= L'a' && value <= L'f') return value - L'a' + 10;
            if (value >= L'A' && value <= L'F') return value - L'A' + 10;
            return -1;
        }

        [[nodiscard]] wchar_t HexDigit(std::uint8_t value)
        {
            constexpr wchar_t Digits[] = L"0123456789abcdef";
            return Digits[value & 0x0F];
        }

        void AppendByte(std::wstring& output, std::uint8_t value)
        {
            output.push_back(HexDigit(static_cast<std::uint8_t>(value >> 4)));
            output.push_back(HexDigit(value));
        }

        [[nodiscard]] std::wstring FormatUuid(NamespaceBytes const& bytes)
        {
            std::wstring result;
            result.reserve(36);
            for (std::size_t index = 0; index < bytes.size(); ++index)
            {
                if (index == 4 || index == 6 || index == 8 || index == 10)
                {
                    result.push_back(L'-');
                }
                AppendByte(result, bytes[index]);
            }
            return result;
        }

        [[nodiscard]] bool ParseDOrNUuid(
            std::wstring_view input,
            NamespaceBytes& bytes,
            bool allowCompact)
        {
            std::array<wchar_t, 32> compact{};
            if (input.size() == compact.size())
            {
                if (!allowCompact)
                {
                    return false;
                }
                std::copy(input.begin(), input.end(), compact.begin());
            }
            else
            {
                constexpr std::array<std::size_t, 4> hyphenPositions{ 8, 13, 18, 23 };
                if (input.size() != 36 || std::any_of(
                    hyphenPositions.begin(), hyphenPositions.end(),
                    [&input](std::size_t position) { return input[position] != L'-'; }))
                {
                    return false;
                }
                std::size_t compactIndex{};
                for (auto const character : input)
                {
                    if (character != L'-')
                    {
                        compact[compactIndex++] = character;
                    }
                }
            }

            NamespaceBytes parsed{};
            for (std::size_t index = 0; index < parsed.size(); ++index)
            {
                auto const high = HexValue(compact[index * 2]);
                auto const low = HexValue(compact[(index * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    return false;
                }
                parsed[index] = static_cast<std::uint8_t>((high << 4) | low);
            }
            bytes = parsed;
            return true;
        }

        void SkipWhitespace(std::wstring_view input, std::size_t& offset)
        {
            while (offset < input.size() && IsManagedWhitespace(input[offset]))
            {
                ++offset;
            }
        }

        [[nodiscard]] bool ConsumeX(std::wstring_view input, std::size_t& offset, std::wstring_view expected)
        {
            SkipWhitespace(input, offset);
            if (input.substr(offset, expected.size()) != expected)
            {
                return false;
            }
            offset += expected.size();
            return true;
        }

        [[nodiscard]] bool ConsumeXHexField(
            std::wstring_view input,
            std::size_t& offset,
            std::uint32_t maximum,
            std::uint32_t& value)
        {
            SkipWhitespace(input, offset);
            if (offset + 2 > input.size() || input[offset] != L'0' ||
                (input[offset + 1] != L'x' && input[offset + 1] != L'X'))
            {
                return false;
            }
            offset += 2;
            SkipWhitespace(input, offset);
            if (offset < input.size() && input[offset] == L'+')
            {
                ++offset;
                SkipWhitespace(input, offset);
            }

            bool hasDigit = false;
            std::uint64_t parsed{};
            while (offset < input.size())
            {
                auto const digit = HexValue(input[offset]);
                if (digit < 0)
                {
                    break;
                }
                auto const unsignedDigit = static_cast<std::uint64_t>(digit);
                if (parsed > (static_cast<std::uint64_t>(maximum) - unsignedDigit) / 16)
                {
                    return false;
                }
                parsed = (parsed * 16) + unsignedDigit;
                hasDigit = true;
                ++offset;
            }
            if (!hasDigit)
            {
                return false;
            }
            value = static_cast<std::uint32_t>(parsed);
            return true;
        }

        [[nodiscard]] bool ParseXUuid(std::wstring_view input, NamespaceBytes& bytes)
        {
            std::size_t offset{};
            std::uint32_t data1{};
            std::uint32_t data2{};
            std::uint32_t data3{};
            std::array<std::uint32_t, 8> data4{};
            if (!ConsumeX(input, offset, L"{") ||
                !ConsumeXHexField(input, offset, 0xFFFFFFFFu, data1) ||
                !ConsumeX(input, offset, L",") ||
                !ConsumeXHexField(input, offset, 0xFFFFu, data2) ||
                !ConsumeX(input, offset, L",") ||
                !ConsumeXHexField(input, offset, 0xFFFFu, data3) ||
                !ConsumeX(input, offset, L",") ||
                !ConsumeX(input, offset, L"{"))
            {
                return false;
            }
            for (std::size_t index = 0; index < data4.size(); ++index)
            {
                if (!ConsumeXHexField(input, offset, 0xFFu, data4[index]))
                {
                    return false;
                }
                if (index + 1 < data4.size() && !ConsumeX(input, offset, L","))
                {
                    return false;
                }
            }
            if (!ConsumeX(input, offset, L"}") || !ConsumeX(input, offset, L"}"))
            {
                return false;
            }
            SkipWhitespace(input, offset);
            if (offset != input.size()) return false;

            bytes = {
                static_cast<std::uint8_t>(data1 >> 24),
                static_cast<std::uint8_t>(data1 >> 16),
                static_cast<std::uint8_t>(data1 >> 8),
                static_cast<std::uint8_t>(data1),
                static_cast<std::uint8_t>(data2 >> 8),
                static_cast<std::uint8_t>(data2),
                static_cast<std::uint8_t>(data3 >> 8),
                static_cast<std::uint8_t>(data3),
                static_cast<std::uint8_t>(data4[0]),
                static_cast<std::uint8_t>(data4[1]),
                static_cast<std::uint8_t>(data4[2]),
                static_cast<std::uint8_t>(data4[3]),
                static_cast<std::uint8_t>(data4[4]),
                static_cast<std::uint8_t>(data4[5]),
                static_cast<std::uint8_t>(data4[6]),
                static_cast<std::uint8_t>(data4[7]),
            };
            return true;
        }

        void AppendUtf8(std::vector<std::uint8_t>& output, std::uint32_t scalar)
        {
            if (scalar <= 0x7F)
            {
                output.push_back(static_cast<std::uint8_t>(scalar));
            }
            else if (scalar <= 0x7FF)
            {
                output.push_back(static_cast<std::uint8_t>(0xC0 | (scalar >> 6)));
                output.push_back(static_cast<std::uint8_t>(0x80 | (scalar & 0x3F)));
            }
            else if (scalar <= 0xFFFF)
            {
                output.push_back(static_cast<std::uint8_t>(0xE0 | (scalar >> 12)));
                output.push_back(static_cast<std::uint8_t>(0x80 | ((scalar >> 6) & 0x3F)));
                output.push_back(static_cast<std::uint8_t>(0x80 | (scalar & 0x3F)));
            }
            else
            {
                output.push_back(static_cast<std::uint8_t>(0xF0 | (scalar >> 18)));
                output.push_back(static_cast<std::uint8_t>(0x80 | ((scalar >> 12) & 0x3F)));
                output.push_back(static_cast<std::uint8_t>(0x80 | ((scalar >> 6) & 0x3F)));
                output.push_back(static_cast<std::uint8_t>(0x80 | (scalar & 0x3F)));
            }
        }

        [[nodiscard]] std::vector<std::uint8_t> EncodeUtf8(std::wstring_view text)
        {
            std::vector<std::uint8_t> bytes;
            bytes.reserve(text.size() * 3);
            for (std::size_t index = 0; index < text.size(); ++index)
            {
                auto scalar = static_cast<std::uint32_t>(text[index]);
                if (scalar >= 0xD800 && scalar <= 0xDBFF)
                {
                    if (index + 1 < text.size())
                    {
                        auto const low = static_cast<std::uint32_t>(text[index + 1]);
                        if (low >= 0xDC00 && low <= 0xDFFF)
                        {
                            scalar = 0x10000 + ((scalar - 0xD800) << 10) + (low - 0xDC00);
                            ++index;
                        }
                        else
                        {
                            scalar = 0xFFFD;
                        }
                    }
                    else
                    {
                        scalar = 0xFFFD;
                    }
                }
                else if (scalar >= 0xDC00 && scalar <= 0xDFFF)
                {
                    scalar = 0xFFFD;
                }
                AppendUtf8(bytes, scalar);
            }
            return bytes;
        }

        [[nodiscard]] std::uint32_t RotateLeft(std::uint32_t value, int amount)
        {
            return (value << amount) | (value >> (32 - amount));
        }

        [[nodiscard]] std::array<std::uint8_t, 20> Sha1(std::vector<std::uint8_t> input)
        {
            auto const originalByteCount = input.size();
            auto const bitLength = static_cast<std::uint64_t>(originalByteCount) * 8;
            input.push_back(0x80);
            while ((input.size() % 64) != 56)
            {
                input.push_back(0);
            }
            for (int shift = 56; shift >= 0; shift -= 8)
            {
                input.push_back(static_cast<std::uint8_t>(bitLength >> shift));
            }

            std::uint32_t h0 = 0x67452301;
            std::uint32_t h1 = 0xEFCDAB89;
            std::uint32_t h2 = 0x98BADCFE;
            std::uint32_t h3 = 0x10325476;
            std::uint32_t h4 = 0xC3D2E1F0;

            for (std::size_t offset = 0; offset < input.size(); offset += 64)
            {
                std::array<std::uint32_t, 80> words{};
                for (std::size_t index = 0; index < 16; ++index)
                {
                    auto const base = offset + (index * 4);
                    words[index] =
                        (static_cast<std::uint32_t>(input[base]) << 24) |
                        (static_cast<std::uint32_t>(input[base + 1]) << 16) |
                        (static_cast<std::uint32_t>(input[base + 2]) << 8) |
                        static_cast<std::uint32_t>(input[base + 3]);
                }
                for (std::size_t index = 16; index < words.size(); ++index)
                {
                    words[index] = RotateLeft(
                        words[index - 3] ^ words[index - 8] ^ words[index - 14] ^ words[index - 16],
                        1);
                }

                auto a = h0;
                auto b = h1;
                auto c = h2;
                auto d = h3;
                auto e = h4;
                for (std::size_t index = 0; index < words.size(); ++index)
                {
                    std::uint32_t f{};
                    std::uint32_t k{};
                    if (index < 20)
                    {
                        f = (b & c) | ((~b) & d);
                        k = 0x5A827999;
                    }
                    else if (index < 40)
                    {
                        f = b ^ c ^ d;
                        k = 0x6ED9EBA1;
                    }
                    else if (index < 60)
                    {
                        f = (b & c) | (b & d) | (c & d);
                        k = 0x8F1BBCDC;
                    }
                    else
                    {
                        f = b ^ c ^ d;
                        k = 0xCA62C1D6;
                    }
                    auto const temp = RotateLeft(a, 5) + f + e + k + words[index];
                    e = d;
                    d = c;
                    c = RotateLeft(b, 30);
                    b = a;
                    a = temp;
                }
                h0 += a;
                h1 += b;
                h2 += c;
                h3 += d;
                h4 += e;
            }

            std::array<std::uint8_t, 20> result{};
            std::array<std::uint32_t, 5> words{ h0, h1, h2, h3, h4 };
            for (std::size_t index = 0; index < words.size(); ++index)
            {
                auto const word = words[index];
                result[(index * 4)] = static_cast<std::uint8_t>(word >> 24);
                result[(index * 4) + 1] = static_cast<std::uint8_t>(word >> 16);
                result[(index * 4) + 2] = static_cast<std::uint8_t>(word >> 8);
                result[(index * 4) + 3] = static_cast<std::uint8_t>(word);
            }
            return result;
        }

        [[nodiscard]] std::array<std::uint8_t, 16> Md5(std::vector<std::uint8_t> input)
        {
            static constexpr std::array<std::uint32_t, 64> Constants{
                0xd76aa478, 0xe8c7b756, 0x242070db, 0xc1bdceee, 0xf57c0faf, 0x4787c62a, 0xa8304613, 0xfd469501,
                0x698098d8, 0x8b44f7af, 0xffff5bb1, 0x895cd7be, 0x6b901122, 0xfd987193, 0xa679438e, 0x49b40821,
                0xf61e2562, 0xc040b340, 0x265e5a51, 0xe9b6c7aa, 0xd62f105d, 0x02441453, 0xd8a1e681, 0xe7d3fbc8,
                0x21e1cde6, 0xc33707d6, 0xf4d50d87, 0x455a14ed, 0xa9e3e905, 0xfcefa3f8, 0x676f02d9, 0x8d2a4c8a,
                0xfffa3942, 0x8771f681, 0x6d9d6122, 0xfde5380c, 0xa4beea44, 0x4bdecfa9, 0xf6bb4b60, 0xbebfbc70,
                0x289b7ec6, 0xeaa127fa, 0xd4ef3085, 0x04881d05, 0xd9d4d039, 0xe6db99e5, 0x1fa27cf8, 0xc4ac5665,
                0xf4292244, 0x432aff97, 0xab9423a7, 0xfc93a039, 0x655b59c3, 0x8f0ccc92, 0xffeff47d, 0x85845dd1,
                0x6fa87e4f, 0xfe2ce6e0, 0xa3014314, 0x4e0811a1, 0xf7537e82, 0xbd3af235, 0x2ad7d2bb, 0xeb86d391,
            };
            static constexpr std::array<int, 64> Shifts{
                7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22, 7, 12, 17, 22,
                5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20, 5, 9, 14, 20,
                4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23, 4, 11, 16, 23,
                6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21, 6, 10, 15, 21,
            };

            auto const originalByteCount = input.size();
            auto const bitLength = static_cast<std::uint64_t>(originalByteCount) * 8;
            input.push_back(0x80);
            while ((input.size() % 64) != 56)
            {
                input.push_back(0);
            }
            for (int shift = 0; shift <= 56; shift += 8)
            {
                input.push_back(static_cast<std::uint8_t>(bitLength >> shift));
            }

            std::uint32_t a0 = 0x67452301;
            std::uint32_t b0 = 0xefcdab89;
            std::uint32_t c0 = 0x98badcfe;
            std::uint32_t d0 = 0x10325476;

            for (std::size_t offset = 0; offset < input.size(); offset += 64)
            {
                std::array<std::uint32_t, 16> words{};
                for (std::size_t index = 0; index < words.size(); ++index)
                {
                    auto const base = offset + (index * 4);
                    words[index] =
                        static_cast<std::uint32_t>(input[base]) |
                        (static_cast<std::uint32_t>(input[base + 1]) << 8) |
                        (static_cast<std::uint32_t>(input[base + 2]) << 16) |
                        (static_cast<std::uint32_t>(input[base + 3]) << 24);
                }

                auto a = a0;
                auto b = b0;
                auto c = c0;
                auto d = d0;
                for (std::size_t index = 0; index < 64; ++index)
                {
                    std::uint32_t f{};
                    std::size_t g{};
                    if (index < 16)
                    {
                        f = (b & c) | ((~b) & d);
                        g = index;
                    }
                    else if (index < 32)
                    {
                        f = (d & b) | ((~d) & c);
                        g = ((5 * index) + 1) % 16;
                    }
                    else if (index < 48)
                    {
                        f = b ^ c ^ d;
                        g = ((3 * index) + 5) % 16;
                    }
                    else
                    {
                        f = c ^ (b | (~d));
                        g = (7 * index) % 16;
                    }
                    auto const previousD = d;
                    d = c;
                    c = b;
                    b += RotateLeft(a + f + Constants[index] + words[g], Shifts[index]);
                    a = previousD;
                }
                a0 += a;
                b0 += b;
                c0 += c;
                d0 += d;
            }

            std::array<std::uint8_t, 16> result{};
            std::array<std::uint32_t, 4> words{ a0, b0, c0, d0 };
            for (std::size_t index = 0; index < words.size(); ++index)
            {
                auto const word = words[index];
                result[(index * 4)] = static_cast<std::uint8_t>(word);
                result[(index * 4) + 1] = static_cast<std::uint8_t>(word >> 8);
                result[(index * 4) + 2] = static_cast<std::uint8_t>(word >> 16);
                result[(index * 4) + 3] = static_cast<std::uint8_t>(word >> 24);
            }
            return result;
        }

        [[nodiscard]] std::vector<std::wstring> SplitNonBlankLines(std::wstring_view text)
        {
            std::vector<std::wstring> result;
            std::size_t start{};
            while (start <= text.size())
            {
                auto end = start;
                while (end < text.size() && text[end] != L'\r' && text[end] != L'\n')
                {
                    ++end;
                }
                auto line = Trim(text.substr(start, end - start));
                if (!line.empty())
                {
                    result.push_back(std::move(line));
                }
                if (end == text.size())
                {
                    break;
                }
                if (text[end] == L'\r' && end + 1 < text.size() && text[end + 1] == L'\n')
                {
                    ++end;
                }
                start = end + 1;
            }
            return result;
        }
    }

    bool IsSupportedVersion(Version version)
    {
        return version == Version::V3 || version == Version::V5;
    }

    bool TryParseNamespace(std::wstring_view input, NamespaceBytes& bytes)
    {
        bytes = {};
        auto text = Trim(input);
        if (text.empty())
        {
            return false;
        }
        if (text.front() == L'{' && text.back() == L'}')
        {
            if (ParseXUuid(text, bytes))
            {
                return true;
            }
            return ParseDOrNUuid(text.substr(1, text.size() - 2), bytes, false);
        }
        if (text.front() == L'(' && text.back() == L')')
        {
            return ParseDOrNUuid(text.substr(1, text.size() - 2), bytes, false);
        }
        return ParseDOrNUuid(text, bytes, true);
    }

    std::wstring FormatNamespace(NamespaceBytes const& bytes)
    {
        return FormatUuid(bytes);
    }

    ComputeResult Compute(NamespaceBytes const& nameSpace, std::wstring_view name, Version version)
    {
        if (!IsSupportedVersion(version))
        {
            return ComputeResult{ false, {}, L"unsupported-version" };
        }

        try
        {
            auto input = EncodeUtf8(name);
            input.insert(input.begin(), nameSpace.begin(), nameSpace.end());
            NamespaceBytes uuid{};
            if (version == Version::V3)
            {
                auto const digest = Md5(std::move(input));
                std::copy(digest.begin(), digest.end(), uuid.begin());
            }
            else
            {
                auto const digest = Sha1(std::move(input));
                std::copy_n(digest.begin(), uuid.size(), uuid.begin());
            }

            auto const versionBits = static_cast<std::uint8_t>(version);
            uuid[6] = static_cast<std::uint8_t>((uuid[6] & 0x0F) | (versionBits << 4));
            uuid[8] = static_cast<std::uint8_t>((uuid[8] & 0x3F) | 0x80);
            return ComputeResult{ true, FormatUuid(uuid), {} };
        }
        catch (...)
        {
            return ComputeResult{ false, {}, L"compute-failed" };
        }
    }

    std::vector<std::wstring> ComputeBulk(
        NamespaceBytes const& nameSpace,
        std::wstring_view multiline,
        Version version)
    {
        std::vector<std::wstring> rows;
        if (!IsSupportedVersion(version))
        {
            return rows;
        }

        try
        {
            for (auto const& line : SplitNonBlankLines(multiline))
            {
                auto const computed = Compute(nameSpace, line, version);
                if (computed.ok)
                {
                    rows.push_back(line + L"  \x2192  " + computed.uuid);
                }
            }
        }
        catch (...)
        {
            // Match the managed never-throw bulk contract. Any rows completed
            // before an allocation failure remain locally inspectable.
        }
        return rows;
    }
}
