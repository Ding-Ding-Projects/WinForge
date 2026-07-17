#include "GuidGen.h"

#include <Windows.h>
#include <bcrypt.h>

#include <algorithm>
#include <array>
#include <chrono>
#include <cwctype>
#include <iomanip>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

#pragma comment(lib, "Bcrypt.lib")

namespace winforge::core::guidgen
{
    namespace
    {
        constexpr std::wstring_view CrockfordAlphabet = L"0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        constexpr std::wstring_view NanoAlphabet = L"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

        [[nodiscard]] std::wstring Trim(std::wstring_view value)
        {
            auto first = value.begin();
            auto last = value.end();
            while (first != last && std::iswspace(*first))
            {
                ++first;
            }
            while (last != first && std::iswspace(*(last - 1)))
            {
                --last;
            }
            return std::wstring(first, last);
        }

        void FillRandom(std::uint8_t* data, std::size_t size)
        {
            if (size == 0)
            {
                return;
            }
            if (size > static_cast<std::size_t>((std::numeric_limits<ULONG>::max)()))
            {
                throw std::runtime_error("random request too large");
            }
            auto const status = BCryptGenRandom(
                nullptr,
                data,
                static_cast<ULONG>(size),
                BCRYPT_USE_SYSTEM_PREFERRED_RNG);
            if (status < 0)
            {
                throw std::runtime_error("cryptographic random generator failed");
            }
        }

        [[nodiscard]] std::array<std::uint8_t, 16> NewGuidBytes()
        {
            std::array<std::uint8_t, 16> bytes{};
            FillRandom(bytes.data(), bytes.size());
            bytes[6] = static_cast<std::uint8_t>((bytes[6] & 0x0F) | 0x40); // Version 4.
            bytes[8] = static_cast<std::uint8_t>((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant.
            return bytes;
        }

        [[nodiscard]] wchar_t HexDigit(std::uint8_t value, bool upper)
        {
            constexpr wchar_t lower[] = L"0123456789abcdef";
            constexpr wchar_t upperChars[] = L"0123456789ABCDEF";
            return upper ? upperChars[value & 0x0F] : lower[value & 0x0F];
        }

        void AppendByte(std::wstring& output, std::uint8_t byte, bool upper)
        {
            output.push_back(HexDigit(static_cast<std::uint8_t>(byte >> 4), upper));
            output.push_back(HexDigit(byte, upper));
        }

        void AppendHex(std::wstring& output, std::array<std::uint8_t, 16> const& bytes, bool upper)
        {
            for (auto const byte : bytes)
            {
                AppendByte(output, byte, upper);
            }
        }

        [[nodiscard]] std::wstring FormatGuid(std::array<std::uint8_t, 16> const& bytes, wchar_t format, bool upper)
        {
            if (format == L'N')
            {
                std::wstring result;
                result.reserve(32);
                AppendHex(result, bytes, upper);
                return result;
            }

            if (format == L'X')
            {
                auto const data1 =
                    (static_cast<std::uint32_t>(bytes[0]) << 24) |
                    (static_cast<std::uint32_t>(bytes[1]) << 16) |
                    (static_cast<std::uint32_t>(bytes[2]) << 8) |
                    static_cast<std::uint32_t>(bytes[3]);
                auto const data2 =
                    static_cast<std::uint16_t>((static_cast<std::uint16_t>(bytes[4]) << 8) | bytes[5]);
                auto const data3 =
                    static_cast<std::uint16_t>((static_cast<std::uint16_t>(bytes[6]) << 8) | bytes[7]);

                std::wostringstream stream;
                stream << (upper ? std::uppercase : std::nouppercase)
                    << std::hex << std::setfill(L'0')
                    << L"{0x" << std::setw(8) << data1
                    << L",0x" << std::setw(4) << data2
                    << L",0x" << std::setw(4) << data3
                    << L",{";
                for (std::size_t index = 8; index < bytes.size(); ++index)
                {
                    if (index > 8)
                    {
                        stream << L",";
                    }
                    stream << L"0x" << std::setw(2) << static_cast<unsigned int>(bytes[index]);
                }
                stream << L"}}";
                return stream.str();
            }

            std::wstring result;
            result.reserve(38);
            if (format == L'B') result.push_back(L'{');
            if (format == L'P') result.push_back(L'(');
            for (std::size_t index = 0; index < bytes.size(); ++index)
            {
                if (index == 4 || index == 6 || index == 8 || index == 10)
                {
                    result.push_back(L'-');
                }
                AppendByte(result, bytes[index], upper);
            }
            if (format == L'B') result.push_back(L'}');
            if (format == L'P') result.push_back(L')');
            return result;
        }

        [[nodiscard]] int HexValue(wchar_t character)
        {
            if (character >= L'0' && character <= L'9') return character - L'0';
            if (character >= L'a' && character <= L'f') return character - L'a' + 10;
            if (character >= L'A' && character <= L'F') return character - L'A' + 10;
            return -1;
        }

        [[nodiscard]] std::array<std::uint8_t, 16> ParseCompactGuid(std::wstring_view text)
        {
            std::wstring compact;
            compact.reserve(32);
            for (auto const character : text)
            {
                if (character == L'-')
                {
                    continue;
                }
                auto const value = HexValue(character);
                if (value < 0)
                {
                    throw std::invalid_argument("invalid GUID");
                }
                compact.push_back(character);
            }
            if (compact.size() != 32)
            {
                throw std::invalid_argument("invalid GUID");
            }

            std::array<std::uint8_t, 16> bytes{};
            for (std::size_t index = 0; index < bytes.size(); ++index)
            {
                auto const high = HexValue(compact[index * 2]);
                auto const low = HexValue(compact[(index * 2) + 1]);
                if (high < 0 || low < 0)
                {
                    throw std::invalid_argument("invalid GUID");
                }
                bytes[index] = static_cast<std::uint8_t>((high << 4) | low);
            }
            return bytes;
        }

        [[nodiscard]] std::array<std::uint8_t, 16> ParseXGuid(std::wstring const& text)
        {
            unsigned int data1{};
            unsigned int data2{};
            unsigned int data3{};
            unsigned int b[8]{};
            auto const matched = swscanf_s(
                text.c_str(),
                L"{0x%x,0x%x,0x%x,{0x%x,0x%x,0x%x,0x%x,0x%x,0x%x,0x%x,0x%x}}",
                &data1,
                &data2,
                &data3,
                &b[0],
                &b[1],
                &b[2],
                &b[3],
                &b[4],
                &b[5],
                &b[6],
                &b[7]);
            if (matched != 11 ||
                data1 > 0xFFFFFFFFu ||
                data2 > 0xFFFFu ||
                data3 > 0xFFFFu ||
                std::any_of(std::begin(b), std::end(b), [](unsigned int value) { return value > 0xFFu; }))
            {
                throw std::invalid_argument("invalid GUID");
            }

            return {
                static_cast<std::uint8_t>((data1 >> 24) & 0xFF),
                static_cast<std::uint8_t>((data1 >> 16) & 0xFF),
                static_cast<std::uint8_t>((data1 >> 8) & 0xFF),
                static_cast<std::uint8_t>(data1 & 0xFF),
                static_cast<std::uint8_t>((data2 >> 8) & 0xFF),
                static_cast<std::uint8_t>(data2 & 0xFF),
                static_cast<std::uint8_t>((data3 >> 8) & 0xFF),
                static_cast<std::uint8_t>(data3 & 0xFF),
                static_cast<std::uint8_t>(b[0]),
                static_cast<std::uint8_t>(b[1]),
                static_cast<std::uint8_t>(b[2]),
                static_cast<std::uint8_t>(b[3]),
                static_cast<std::uint8_t>(b[4]),
                static_cast<std::uint8_t>(b[5]),
                static_cast<std::uint8_t>(b[6]),
                static_cast<std::uint8_t>(b[7]),
            };
        }

        [[nodiscard]] std::array<std::uint8_t, 16> ParseGuidBytes(std::wstring_view input)
        {
            auto text = Trim(input);
            if (text.empty())
            {
                throw std::invalid_argument("invalid GUID");
            }

            if (text.rfind(L"{0x", 0) == 0 || text.rfind(L"{0X", 0) == 0)
            {
                return ParseXGuid(text);
            }

            if ((text.front() == L'{' && text.back() == L'}') ||
                (text.front() == L'(' && text.back() == L')'))
            {
                text = text.substr(1, text.size() - 2);
            }
            return ParseCompactGuid(text);
        }

        [[nodiscard]] wchar_t GuidFormat(std::wstring_view format)
        {
            auto trimmed = Trim(format);
            wchar_t value = trimmed.empty() ? L'D' : static_cast<wchar_t>(std::towupper(trimmed.front()));
            switch (value)
            {
            case L'N':
            case L'D':
            case L'B':
            case L'P':
            case L'X':
                return value;
            default:
                throw std::invalid_argument("unsupported GUID format");
            }
        }
    }

    std::wstring NewGuid(std::wstring_view format, bool upper)
    {
        return FormatGuid(NewGuidBytes(), GuidFormat(format), upper);
    }

    std::wstring BulkGuids(int count, std::wstring_view format, bool upper)
    {
        count = std::clamp(count, 1, 1000);
        auto const parsedFormat = GuidFormat(format);
        std::wstring result;
        for (int index = 0; index < count; ++index)
        {
            if (index > 0)
            {
                result.push_back(L'\n');
            }
            result.append(FormatGuid(NewGuidBytes(), parsedFormat, upper));
        }
        return result;
    }

    std::wstring NewUlid()
    {
        auto const now = std::chrono::time_point_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now());
        auto milliseconds = now.time_since_epoch().count();
        if (milliseconds < 0)
        {
            milliseconds = 0;
        }

        std::array<std::uint8_t, 10> random{};
        FillRandom(random.data(), random.size());

        std::wstring result(26, L'0');
        result[0] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 45) & 0x1F)];
        result[1] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 40) & 0x1F)];
        result[2] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 35) & 0x1F)];
        result[3] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 30) & 0x1F)];
        result[4] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 25) & 0x1F)];
        result[5] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 20) & 0x1F)];
        result[6] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 15) & 0x1F)];
        result[7] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 10) & 0x1F)];
        result[8] = CrockfordAlphabet[static_cast<std::size_t>((milliseconds >> 5) & 0x1F)];
        result[9] = CrockfordAlphabet[static_cast<std::size_t>(milliseconds & 0x1F)];

        result[10] = CrockfordAlphabet[(random[0] & 0xFF) >> 3];
        result[11] = CrockfordAlphabet[((random[0] << 2) | (random[1] >> 6)) & 0x1F];
        result[12] = CrockfordAlphabet[(random[1] >> 1) & 0x1F];
        result[13] = CrockfordAlphabet[((random[1] << 4) | (random[2] >> 4)) & 0x1F];
        result[14] = CrockfordAlphabet[((random[2] << 1) | (random[3] >> 7)) & 0x1F];
        result[15] = CrockfordAlphabet[(random[3] >> 2) & 0x1F];
        result[16] = CrockfordAlphabet[((random[3] << 3) | (random[4] >> 5)) & 0x1F];
        result[17] = CrockfordAlphabet[random[4] & 0x1F];
        result[18] = CrockfordAlphabet[(random[5] & 0xFF) >> 3];
        result[19] = CrockfordAlphabet[((random[5] << 2) | (random[6] >> 6)) & 0x1F];
        result[20] = CrockfordAlphabet[(random[6] >> 1) & 0x1F];
        result[21] = CrockfordAlphabet[((random[6] << 4) | (random[7] >> 4)) & 0x1F];
        result[22] = CrockfordAlphabet[((random[7] << 1) | (random[8] >> 7)) & 0x1F];
        result[23] = CrockfordAlphabet[(random[8] >> 2) & 0x1F];
        result[24] = CrockfordAlphabet[((random[8] << 3) | (random[9] >> 5)) & 0x1F];
        result[25] = CrockfordAlphabet[random[9] & 0x1F];
        return result;
    }

    std::wstring NewNanoId(int length)
    {
        length = std::clamp(length, 4, 64);
        std::vector<std::uint8_t> random(static_cast<std::size_t>(length));
        FillRandom(random.data(), random.size());
        std::wstring result;
        result.reserve(static_cast<std::size_t>(length));
        for (auto const byte : random)
        {
            result.push_back(NanoAlphabet[byte & 0x3F]);
        }
        return result;
    }

    GuidInfo Inspect(std::wstring_view text)
    {
        auto const bytes = ParseGuidBytes(text);

        std::wstring hex;
        hex.reserve(47);
        for (std::size_t index = 0; index < bytes.size(); ++index)
        {
            if (index > 0)
            {
                hex.push_back(L' ');
            }
            AppendByte(hex, bytes[index], true);
        }

        auto const version = (bytes[6] >> 4) & 0x0F;
        auto const variantBits = (bytes[8] >> 5) & 0x07;
        std::wstring variant =
            (variantBits & 0b100) == 0 ? L"NCS (0xxx)" :
            (variantBits & 0b110) == 0b100 ? L"RFC 4122 (10xx)" :
            (variantBits & 0b111) == 0b110 ? L"Microsoft (110x)" :
            L"Reserved (111x)";

        return GuidInfo{ hex, static_cast<int>(version), variant };
    }
}
