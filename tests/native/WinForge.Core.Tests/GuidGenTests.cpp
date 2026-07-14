#include "GuidGenTests.h"

#include "../../../src/WinForge.Core/GuidGen.h"

#include <algorithm>
#include <cwctype>
#include <iostream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::tests::guidgen
{
    namespace
    {
        bool IsHex(wchar_t ch)
        {
            return (ch >= L'0' && ch <= L'9') ||
                (ch >= L'a' && ch <= L'f') ||
                (ch >= L'A' && ch <= L'F');
        }

        bool IsUpperHex(wchar_t ch)
        {
            return (ch >= L'0' && ch <= L'9') ||
                (ch >= L'A' && ch <= L'F');
        }

        bool IsGuidD(std::wstring_view value, bool upper)
        {
            if (value.size() != 36) return false;
            for (std::size_t index = 0; index < value.size(); ++index)
            {
                if (index == 8 || index == 13 || index == 18 || index == 23)
                {
                    if (value[index] != L'-') return false;
                }
                else if (upper ? !IsUpperHex(value[index]) : !IsHex(value[index]))
                {
                    return false;
                }
            }
            return true;
        }

        bool IsNanoAlphabet(std::wstring_view value)
        {
            constexpr std::wstring_view alphabet = L"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";
            return std::all_of(value.begin(), value.end(), [](wchar_t ch)
            {
                return alphabet.find(ch) != std::wstring_view::npos;
            });
        }

        bool IsUlidAlphabet(std::wstring_view value)
        {
            constexpr std::wstring_view alphabet = L"0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            return std::all_of(value.begin(), value.end(), [](wchar_t ch)
            {
                return alphabet.find(ch) != std::wstring_view::npos;
            });
        }

        bool Expect(bool condition, char const* name, int& failures)
        {
            if (condition)
            {
                std::cout << "PASS " << name << "\n";
                return true;
            }
            std::cout << "FAIL " << name << "\n";
            ++failures;
            return false;
        }

        std::vector<std::wstring> Lines(std::wstring_view value)
        {
            std::vector<std::wstring> lines;
            std::wstring current;
            for (auto const ch : value)
            {
                if (ch == L'\n')
                {
                    lines.push_back(current);
                    current.clear();
                }
                else
                {
                    current.push_back(ch);
                }
            }
            lines.push_back(current);
            return lines;
        }
    }

    int RunGuidGenTests()
    {
        int failures = 0;

        auto const guidD = winforge::core::guidgen::NewGuid(L"D", false);
        Expect(IsGuidD(guidD, false), "GUID generator emits D format", failures);

        auto const guidUpper = winforge::core::guidgen::NewGuid(L"D", true);
        Expect(IsGuidD(guidUpper, true), "GUID generator uppercases D format", failures);

        auto const guidN = winforge::core::guidgen::NewGuid(L"N", false);
        Expect(guidN.size() == 32 && std::all_of(guidN.begin(), guidN.end(), IsHex),
            "GUID generator emits N format", failures);

        auto const guidB = winforge::core::guidgen::NewGuid(L"B", false);
        Expect(guidB.size() == 38 && guidB.front() == L'{' && guidB.back() == L'}',
            "GUID generator emits B format", failures);

        auto const guidP = winforge::core::guidgen::NewGuid(L"P", false);
        Expect(guidP.size() == 38 && guidP.front() == L'(' && guidP.back() == L')',
            "GUID generator emits P format", failures);

        auto const guidX = winforge::core::guidgen::NewGuid(L"X", true);
        Expect(guidX.rfind(L"{0X", 0) == 0 || guidX.rfind(L"{0x", 0) == 0,
            "GUID generator emits X format", failures);

        auto const bulkLow = Lines(winforge::core::guidgen::BulkGuids(0, L"D", false));
        Expect(bulkLow.size() == 1 && IsGuidD(bulkLow.front(), false),
            "GUID bulk clamps low count", failures);

        auto const bulkThree = Lines(winforge::core::guidgen::BulkGuids(3, L"N", true));
        Expect(bulkThree.size() == 3 &&
            std::all_of(bulkThree.begin(), bulkThree.end(), [](std::wstring const& line)
            {
                return line.size() == 32 && std::all_of(line.begin(), line.end(), IsUpperHex);
            }),
            "GUID bulk emits requested count and format", failures);

        auto const ulid = winforge::core::guidgen::NewUlid();
        Expect(ulid.size() == 26 && IsUlidAlphabet(ulid), "ULID uses Crockford base32 shape", failures);

        auto const nanoLow = winforge::core::guidgen::NewNanoId(1);
        auto const nanoHigh = winforge::core::guidgen::NewNanoId(100);
        Expect(nanoLow.size() == 4 && IsNanoAlphabet(nanoLow), "nano-ID clamps low length", failures);
        Expect(nanoHigh.size() == 64 && IsNanoAlphabet(nanoHigh), "nano-ID clamps high length", failures);

        auto const info = winforge::core::guidgen::Inspect(L"00112233-4455-6677-8899-aabbccddeeff");
        Expect(info.hex == L"00 11 22 33 44 55 66 77 88 99 AA BB CC DD EE FF",
            "GUID inspector reports RFC byte order", failures);
        Expect(info.version == 6, "GUID inspector reports version nibble", failures);
        Expect(info.variant == L"RFC 4122 (10xx)", "GUID inspector reports RFC variant", failures);

        auto const infoB = winforge::core::guidgen::Inspect(L"{00112233-4455-6677-8899-aabbccddeeff}");
        Expect(infoB.hex == info.hex, "GUID inspector accepts braces", failures);

        bool invalidRejected = false;
        try
        {
            (void)winforge::core::guidgen::Inspect(L"not-a-guid");
        }
        catch (std::invalid_argument const&)
        {
            invalidRejected = true;
        }
        Expect(invalidRejected, "GUID inspector rejects invalid input", failures);

        std::cout << "\nGUID generator tests: " << (16 - failures) << " passed, " << failures << " failed\n";
        return failures;
    }
}
