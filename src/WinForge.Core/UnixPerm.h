#pragma once

#include <cstdint>
#include <string>
#include <string_view>

namespace winforge::core::unixperm
{
    using Mode = std::uint16_t;

    inline constexpr Mode SetUid = 0x800;
    inline constexpr Mode SetGid = 0x400;
    inline constexpr Mode Sticky = 0x200;

    inline constexpr Mode OwnerR = 0x100;
    inline constexpr Mode OwnerW = 0x080;
    inline constexpr Mode OwnerX = 0x040;
    inline constexpr Mode GroupR = 0x020;
    inline constexpr Mode GroupW = 0x010;
    inline constexpr Mode GroupX = 0x008;
    inline constexpr Mode OtherR = 0x004;
    inline constexpr Mode OtherW = 0x002;
    inline constexpr Mode OtherX = 0x001;

    inline constexpr Mode PermMask = 0x0FFF;

    [[nodiscard]] constexpr Mode Normalize(std::uint32_t mode) noexcept
    {
        return static_cast<Mode>(mode & PermMask);
    }

    // Always emits four octal digits, including the leading special-bit digit.
    [[nodiscard]] std::wstring ToOctal(Mode mode);

    // Emits chmod-friendly octal: three digits unless a special bit is set.
    [[nodiscard]] std::wstring ToChmodOctal(Mode mode);

    // Emits rwxrwxrwx, substituting s/S/t/T for special execute positions.
    [[nodiscard]] std::wstring ToSymbolic(Mode mode);

    // Accepts 755, 0755, 4755, or an optional case-insensitive 0o prefix.
    [[nodiscard]] bool TryParseOctal(std::wstring_view text, Mode& mode);

    // Accepts a nine-character symbolic mode and the common ten-character
    // ls spelling with an ignored leading file-type character.
    [[nodiscard]] bool TryParseSymbolic(std::wstring_view text, Mode& mode);
}
