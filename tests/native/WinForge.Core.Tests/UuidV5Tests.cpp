#include "UuidV5Tests.h"

#include "UuidV5.h"

#include <iostream>
#include <string>
#include <string_view>

namespace
{
    struct Suite
    {
        NativeTestCounts counts;

        void Expect(bool condition, std::string_view name)
        {
            if (condition)
            {
                ++counts.passed;
                std::cout << "PASS " << name << '\n';
            }
            else
            {
                ++counts.failed;
                std::cerr << "FAIL " << name << '\n';
            }
        }
    };

    [[nodiscard]] std::wstring Scalar(std::uint32_t codePoint)
    {
        std::wstring result;
        if constexpr (sizeof(wchar_t) == 2)
        {
            if (codePoint > 0xFFFFu)
            {
                codePoint -= 0x10000u;
                result.push_back(static_cast<wchar_t>(0xD800u + (codePoint >> 10)));
                result.push_back(static_cast<wchar_t>(0xDC00u + (codePoint & 0x3FFu)));
            }
            else
            {
                result.push_back(static_cast<wchar_t>(codePoint));
            }
        }
        else
        {
            result.push_back(static_cast<wchar_t>(codePoint));
        }
        return result;
    }
}

NativeTestCounts RunUuidV5Tests()
{
    using namespace winforge::core::uuidv5;
    Suite suite;

    suite.Expect(
        FormatNamespace(NamespaceDns) == L"6ba7b810-9dad-11d1-80b4-00c04fd430c8" &&
        FormatNamespace(NamespaceUrl) == L"6ba7b811-9dad-11d1-80b4-00c04fd430c8" &&
        FormatNamespace(NamespaceOid) == L"6ba7b812-9dad-11d1-80b4-00c04fd430c8" &&
        FormatNamespace(NamespaceX500) == L"6ba7b814-9dad-11d1-80b4-00c04fd430c8",
        "UUID v3/v5 exposes all RFC 4122 appendix C namespaces in network byte order");

    auto const mongolianVowelSeparator = std::wstring(1, static_cast<wchar_t>(0x180Eu));
    NamespaceBytes parsed{};
    suite.Expect(
        TryParseNamespace(L"  6BA7B810-9DAD-11D1-80B4-00C04FD430C8 ", parsed) &&
        parsed == NamespaceDns &&
        TryParseNamespace(L"6ba7b8119dad11d180b400c04fd430c8", parsed) &&
        parsed == NamespaceUrl &&
        TryParseNamespace(L"{6ba7b812-9dad-11d1-80b4-00c04fd430c8}", parsed) &&
        parsed == NamespaceOid &&
        TryParseNamespace(L"(6ba7b814-9dad-11d1-80b4-00c04fd430c8)", parsed) &&
        parsed == NamespaceX500 &&
        TryParseNamespace(
            L"{ 0x000000006ba7b812, 0x+00009dad, 0x11d1, { 0x80, 0xb4, 0x00, 0xc0, 0x4f, 0xd4, 0x30, 0xc8 } }",
            parsed) &&
        parsed == NamespaceOid,
        "UUID namespace parsing accepts .NET Guid.TryParse D N B P and X forms");

    parsed = NamespaceDns;
    suite.Expect(
        !TryParseNamespace(L"", parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(L"urn:uuid:{6ba7b810-9dad-11d1-80b4-00c04fd430c8}", parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(L"6ba7-b8109dad11d180b400c04fd430c8", parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(L"{6ba7b8109dad11d180b400c04fd430c8}", parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(
            L"{0x100000000,0x9dad,0x11d1,{0x80,0xb4,0x00,0xc0,0x4f,0xd4,0x30,0xc8}}",
            parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(L"6ba7b810-9dad-11d1-80b4-00c04fd430c", parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(L"{6ba7b810-9dad-11d1-80b4-00c04fd430c8)", parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(
            mongolianVowelSeparator + L"6ba7b810-9dad-11d1-80b4-00c04fd430c8" + mongolianVowelSeparator,
            parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(
            L"{" + mongolianVowelSeparator +
                L"0x6ba7b810,0x9dad,0x11d1,{0x80,0xb4,0x00,0xc0,0x4f,0xd4,0x30,0xc8}}",
            parsed) &&
        parsed == NamespaceBytes{} &&
        !TryParseNamespace(L"not-a-guid", parsed) &&
        parsed == NamespaceBytes{},
        "UUID namespace parsing rejects non-.NET forms, U+180E edges, malformed input, and clears failed output");

    auto const v3Rfc = Compute(NamespaceDns, L"www.widgets.com", Version::V3);
    auto const v5Rfc = Compute(NamespaceDns, L"www.widgets.com", Version::V5);
    suite.Expect(
        v3Rfc.ok && v3Rfc.uuid == L"3d813cbb-47fb-32ba-91df-831e1593ac29" &&
        v5Rfc.ok && v5Rfc.uuid == L"21f7f8de-8051-5b89-8680-0195ef798b6a",
        "UUID v3 MD5 and v5 SHA-1 match RFC 4122 www.widgets.com test vectors");

    auto const emptyName = Compute(NamespaceDns, L"", Version::V5);
    suite.Expect(
        emptyName.ok && emptyName.uuid == L"4ebd0208-8328-5d69-8c44-ec50939c0967",
        "UUID v5 hashes an empty UTF-8 name deterministically");

    auto const length39 = std::wstring(39, L'a');
    auto const length40 = std::wstring(40, L'a');
    auto const length47 = std::wstring(47, L'a');
    auto const length48 = std::wstring(48, L'a');
    suite.Expect(
        Compute(NamespaceDns, length39, Version::V3).uuid == L"96cb729a-b665-38ba-b98f-a35a1d044728" &&
        Compute(NamespaceDns, length39, Version::V5).uuid == L"5824f981-4282-59d4-9716-acb6d741350e" &&
        Compute(NamespaceDns, length40, Version::V3).uuid == L"13c085b8-0e53-35ed-bd46-f814ae2cd6cf" &&
        Compute(NamespaceDns, length40, Version::V5).uuid == L"39f39c20-db47-5131-8879-62f8f67f9014" &&
        Compute(NamespaceDns, length47, Version::V3).uuid == L"f41abfa0-01e6-34a5-ad0c-0c9835688c00" &&
        Compute(NamespaceDns, length47, Version::V5).uuid == L"660c273c-8a00-5941-b6f4-8d0afed88966" &&
        Compute(NamespaceDns, length48, Version::V3).uuid == L"12adee6c-b187-318d-82d2-f934bf55422b" &&
        Compute(NamespaceDns, length48, Version::V5).uuid == L"7280cc42-274a-5c4a-91fc-ae23f853eeb7",
        "UUID v3/v5 hashes are correct at MD5 and SHA-1 padding boundaries");

    auto const cafeV3 = Compute(NamespaceDns, L"Caf\u00E9", Version::V3);
    auto const cafeV5 = Compute(NamespaceDns, L"Caf\u00E9", Version::V5);
    auto const catV5 = Compute(NamespaceDns, Scalar(0x1F63Au), Version::V5);
    suite.Expect(
        cafeV3.ok && cafeV3.uuid == L"66098b2f-a135-357a-8251-36d440ecce00" &&
        cafeV5.ok && cafeV5.uuid == L"8f877528-bab7-5825-b423-479fee2bfa10" &&
        catV5.ok && catV5.uuid == L"ab22c429-e507-5d47-a984-88ce225c8422",
        "UUID name hashing uses UTF-8 for accented and supplementary Unicode names");

    std::wstring invalidUtf16 = L"bad";
    invalidUtf16.push_back(static_cast<wchar_t>(0xD800u));
    invalidUtf16 += L"name";
    auto const invalidUtf16Result = Compute(NamespaceDns, invalidUtf16, Version::V5);
    suite.Expect(
        invalidUtf16Result.ok && invalidUtf16Result.uuid == L"a2bf9502-33ab-5ac2-9771-123d965d0cda",
        "UUID name hashing replaces invalid UTF-16 with the managed UTF-8 replacement fallback");

    suite.Expect(
        IsSupportedVersion(Version::V3) && IsSupportedVersion(Version::V5) &&
        !IsSupportedVersion(static_cast<Version>(4)),
        "UUID core recognizes only the v3 and v5 versions exposed by the page");

    auto const unsupported = Compute(NamespaceDns, L"name", static_cast<Version>(4));
    suite.Expect(
        !unsupported.ok && unsupported.uuid.empty() && unsupported.error == L"unsupported-version",
        "UUID core rejects an unsupported version instead of silently hashing as v5");

    auto const versionBitsV3 = Compute(NamespaceUrl, L"https://example.test", Version::V3);
    auto const versionBitsV5 = Compute(NamespaceUrl, L"https://example.test", Version::V5);
    suite.Expect(
        versionBitsV3.ok && versionBitsV3.uuid[14] == L'3' &&
        (versionBitsV3.uuid[19] == L'8' || versionBitsV3.uuid[19] == L'9' ||
            versionBitsV3.uuid[19] == L'a' || versionBitsV3.uuid[19] == L'b') &&
        versionBitsV5.ok && versionBitsV5.uuid[14] == L'5' &&
        (versionBitsV5.uuid[19] == L'8' || versionBitsV5.uuid[19] == L'9' ||
            versionBitsV5.uuid[19] == L'a' || versionBitsV5.uuid[19] == L'b'),
        "UUID results set RFC version and variant nibbles after hash truncation");

    auto const bulk = ComputeBulk(
        NamespaceDns,
        L"  alpha  \r\n\r beta\n\t\n gamma \r",
        Version::V5);
    auto const mongolianBulk = ComputeBulk(
        NamespaceDns,
        mongolianVowelSeparator + L"alpha" + mongolianVowelSeparator,
        Version::V5);
    suite.Expect(
        bulk.size() == 3 &&
        bulk[0] == L"alpha  \x2192  1ecf6c63-8cd3-53fe-b653-1c1b56b3275b" &&
        bulk[1] == L"beta  \x2192  5aab62ea-1ad8-5083-a410-08c2524ea6ae" &&
        bulk[2] == L"gamma  \x2192  c6dea981-302e-5565-a370-0add83440673" &&
        mongolianBulk.size() == 1 &&
        mongolianBulk.front() == mongolianVowelSeparator + L"alpha" + mongolianVowelSeparator +
            L"  \x2192  1332d9f6-a457-5c23-bb37-87feef5d836f",
        "UUID bulk generation trims managed CRLF CR and LF whitespace but preserves U+180E names");

    suite.Expect(
        ComputeBulk(NamespaceDns, L" \r\n\t\n", Version::V5).empty() &&
        ComputeBulk(NamespaceDns, L"alpha", static_cast<Version>(4)).empty(),
        "UUID bulk generation skips blank rows and fails closed for unsupported versions");

    std::wstring manyLines;
    for (int index = 0; index < 501; ++index)
    {
        if (index > 0) manyLines += L'\n';
        manyLines += L"line-" + std::to_wstring(index);
    }
    auto const manyRows = ComputeBulk(NamespaceOid, manyLines, Version::V5);
    suite.Expect(
        manyRows.size() == 501 && manyRows.front().starts_with(L"line-0  \x2192  ") &&
        manyRows.back().starts_with(L"line-500  \x2192  "),
        "UUID bulk generation retains every nonblank row for the UI's bounded scroll surface");

    std::cout << "\nUUID v3/v5 tests: " << suite.counts.passed << " passed, " << suite.counts.failed << " failed\n";
    return suite.counts;
}
