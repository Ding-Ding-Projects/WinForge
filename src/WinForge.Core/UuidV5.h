#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::uuidv5
{
    // RFC 4122 name-based UUID versions.  The native surface deliberately
    // exposes both versions offered by the managed oracle: v3/MD5 and
    // v5/SHA-1.  Values outside this enum are rejected instead of silently
    // becoming a different UUID version.
    enum class Version : std::uint8_t
    {
        V3 = 3,
        V5 = 5,
    };

    using NamespaceBytes = std::array<std::uint8_t, 16>;

    struct ComputeResult
    {
        bool ok{ false };
        std::wstring uuid{};
        std::wstring error{};
    };

    // RFC 4122 appendix C namespace values, stored in network (RFC text)
    // byte order rather than the first-three-field little-endian layout used
    // by Windows GUID memory representations.
    inline constexpr NamespaceBytes NamespaceDns{
        0x6B, 0xA7, 0xB8, 0x10, 0x9D, 0xAD, 0x11, 0xD1,
        0x80, 0xB4, 0x00, 0xC0, 0x4F, 0xD4, 0x30, 0xC8,
    };
    inline constexpr NamespaceBytes NamespaceUrl{
        0x6B, 0xA7, 0xB8, 0x11, 0x9D, 0xAD, 0x11, 0xD1,
        0x80, 0xB4, 0x00, 0xC0, 0x4F, 0xD4, 0x30, 0xC8,
    };
    inline constexpr NamespaceBytes NamespaceOid{
        0x6B, 0xA7, 0xB8, 0x12, 0x9D, 0xAD, 0x11, 0xD1,
        0x80, 0xB4, 0x00, 0xC0, 0x4F, 0xD4, 0x30, 0xC8,
    };
    inline constexpr NamespaceBytes NamespaceX500{
        0x6B, 0xA7, 0xB8, 0x14, 0x9D, 0xAD, 0x11, 0xD1,
        0x80, 0xB4, 0x00, 0xC0, 0x4F, 0xD4, 0x30, 0xC8,
    };

    [[nodiscard]] bool IsSupportedVersion(Version version);

    // Parses the .NET Guid.TryParse-compatible D/N/B/P/X namespace forms.
    // The returned bytes are always in RFC network order, ready for hashing.
    [[nodiscard]] bool TryParseNamespace(std::wstring_view text, NamespaceBytes& bytes);

    [[nodiscard]] std::wstring FormatNamespace(NamespaceBytes const& bytes);

    // Computes hash(namespace RFC bytes || UTF-8(name)), takes the first 16
    // bytes, and sets RFC 4122 version/variant fields. Invalid UTF-16 names
    // use U+FFFD replacement, matching .NET's default UTF-8 fallback.
    [[nodiscard]] ComputeResult Compute(
        NamespaceBytes const& nameSpace,
        std::wstring_view name,
        Version version);

    // One display row per non-blank line, exactly as the managed bulk surface:
    // "trimmed name  →  canonical UUID". CRLF, CR, and LF are normalized.
    [[nodiscard]] std::vector<std::wstring> ComputeBulk(
        NamespaceBytes const& nameSpace,
        std::wstring_view multiline,
        Version version);
}
