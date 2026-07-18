#include "UnixPerm.h"

#include <array>
#include <string>

namespace
{
    [[nodiscard]] constexpr bool IsManagedWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\x0009' && value <= L'\x000D') ||
            value == L'\x0020' || value == L'\x0085' || value == L'\x00A0' ||
            value == L'\x1680' || (value >= L'\x2000' && value <= L'\x200A') ||
            value == L'\x2028' || value == L'\x2029' || value == L'\x202F' ||
            value == L'\x205F' || value == L'\x3000';
    }

    [[nodiscard]] std::wstring_view Trim(std::wstring_view value) noexcept
    {
        while (!value.empty() && IsManagedWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsManagedWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    [[nodiscard]] wchar_t SpecialExecute(bool execute, bool special, wchar_t lower) noexcept
    {
        if (!special) return execute ? L'x' : L'-';
        return execute ? lower : static_cast<wchar_t>(lower - L'a' + L'A');
    }

    [[nodiscard]] bool ReadPermission(
        wchar_t value,
        wchar_t expected,
        winforge::core::unixperm::Mode bit,
        winforge::core::unixperm::Mode& mode) noexcept
    {
        if (value == expected)
        {
            mode = static_cast<winforge::core::unixperm::Mode>(mode | bit);
            return true;
        }
        return value == L'-';
    }

    [[nodiscard]] bool ReadExecute(
        wchar_t value,
        winforge::core::unixperm::Mode executeBit,
        winforge::core::unixperm::Mode specialBit,
        wchar_t lower,
        winforge::core::unixperm::Mode& mode) noexcept
    {
        auto const upper = static_cast<wchar_t>(lower - L'a' + L'A');
        if (value == L'x')
        {
            mode = static_cast<winforge::core::unixperm::Mode>(mode | executeBit);
            return true;
        }
        if (value == L'-') return true;
        if (value == lower)
        {
            mode = static_cast<winforge::core::unixperm::Mode>(mode | executeBit | specialBit);
            return true;
        }
        if (value == upper)
        {
            mode = static_cast<winforge::core::unixperm::Mode>(mode | specialBit);
            return true;
        }
        return false;
    }

    [[nodiscard]] std::wstring OctalDigits(winforge::core::unixperm::Mode mode, bool includeSpecial)
    {
        auto const normalized = winforge::core::unixperm::Normalize(mode);
        std::wstring result;
        if (includeSpecial)
        {
            result.push_back(static_cast<wchar_t>(L'0' + ((normalized >> 9) & 0x7)));
        }
        result.push_back(static_cast<wchar_t>(L'0' + ((normalized >> 6) & 0x7)));
        result.push_back(static_cast<wchar_t>(L'0' + ((normalized >> 3) & 0x7)));
        result.push_back(static_cast<wchar_t>(L'0' + (normalized & 0x7)));
        return result;
    }
}

namespace winforge::core::unixperm
{
    std::wstring ToOctal(Mode mode)
    {
        return OctalDigits(mode, true);
    }

    std::wstring ToChmodOctal(Mode mode)
    {
        auto const normalized = Normalize(mode);
        return OctalDigits(normalized, (normalized & (SetUid | SetGid | Sticky)) != 0);
    }

    std::wstring ToSymbolic(Mode mode)
    {
        auto const normalized = Normalize(mode);
        std::array<wchar_t, 9> text{
            (normalized & OwnerR) != 0 ? L'r' : L'-',
            (normalized & OwnerW) != 0 ? L'w' : L'-',
            SpecialExecute((normalized & OwnerX) != 0, (normalized & SetUid) != 0, L's'),
            (normalized & GroupR) != 0 ? L'r' : L'-',
            (normalized & GroupW) != 0 ? L'w' : L'-',
            SpecialExecute((normalized & GroupX) != 0, (normalized & SetGid) != 0, L's'),
            (normalized & OtherR) != 0 ? L'r' : L'-',
            (normalized & OtherW) != 0 ? L'w' : L'-',
            SpecialExecute((normalized & OtherX) != 0, (normalized & Sticky) != 0, L't'),
        };
        return std::wstring(text.begin(), text.end());
    }

    bool TryParseOctal(std::wstring_view text, Mode& mode)
    {
        mode = 0;
        text = Trim(text);
        if (text.size() >= 2 && text[0] == L'0' && (text[1] == L'o' || text[1] == L'O'))
        {
            text.remove_prefix(2);
        }
        else if (text.size() >= 2 && text[0] == L'0' && (text[1] == L'x' || text[1] == L'X'))
        {
            return false;
        }

        if (text.empty() || text.size() > 4) return false;
        std::uint32_t value{};
        for (auto const character : text)
        {
            if (character < L'0' || character > L'7') return false;
            value = (value << 3) | static_cast<std::uint32_t>(character - L'0');
        }
        mode = Normalize(value);
        return true;
    }

    bool TryParseSymbolic(std::wstring_view text, Mode& mode)
    {
        mode = 0;
        text = Trim(text);
        if (text.size() == 10) text.remove_prefix(1);
        if (text.size() != 9) return false;

        Mode parsed{};
        if (!ReadPermission(text[0], L'r', OwnerR, parsed) ||
            !ReadPermission(text[1], L'w', OwnerW, parsed) ||
            !ReadExecute(text[2], OwnerX, SetUid, L's', parsed) ||
            !ReadPermission(text[3], L'r', GroupR, parsed) ||
            !ReadPermission(text[4], L'w', GroupW, parsed) ||
            !ReadExecute(text[5], GroupX, SetGid, L's', parsed) ||
            !ReadPermission(text[6], L'r', OtherR, parsed) ||
            !ReadPermission(text[7], L'w', OtherW, parsed) ||
            !ReadExecute(text[8], OtherX, Sticky, L't', parsed))
        {
            return false;
        }
        mode = parsed;
        return true;
    }
}
