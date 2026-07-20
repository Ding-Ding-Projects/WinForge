#include "AsciiTable.h"

#include <array>
#include <cstddef>

namespace
{
    using winforge::core::LanguageMode;
    using winforge::core::LocalizedText;
    using winforge::core::asciitable::Row;

    struct ControlName
    {
        std::wstring_view mnemonic;
        std::wstring_view english;
        std::wstring_view cantonese;
    };

    constexpr std::array<ControlName, 32> C0{
        ControlName{ L"NUL", L"Null", L"空字元" },
        ControlName{ L"SOH", L"Start of Heading", L"標題開始" },
        ControlName{ L"STX", L"Start of Text", L"文字開始" },
        ControlName{ L"ETX", L"End of Text", L"文字結束" },
        ControlName{ L"EOT", L"End of Transmission", L"傳輸結束" },
        ControlName{ L"ENQ", L"Enquiry", L"查詢" },
        ControlName{ L"ACK", L"Acknowledge", L"確認" },
        ControlName{ L"BEL", L"Bell", L"響鈴" },
        ControlName{ L"BS", L"Backspace", L"退格" },
        ControlName{ L"HT", L"Horizontal Tab", L"水平定位（Tab）" },
        ControlName{ L"LF", L"Line Feed", L"換行" },
        ControlName{ L"VT", L"Vertical Tab", L"垂直定位" },
        ControlName{ L"FF", L"Form Feed", L"換頁" },
        ControlName{ L"CR", L"Carriage Return", L"歸位" },
        ControlName{ L"SO", L"Shift Out", L"移出" },
        ControlName{ L"SI", L"Shift In", L"移入" },
        ControlName{ L"DLE", L"Data Link Escape", L"資料連結跳脫" },
        ControlName{ L"DC1", L"Device Control 1 (XON)", L"裝置控制 1（XON）" },
        ControlName{ L"DC2", L"Device Control 2", L"裝置控制 2" },
        ControlName{ L"DC3", L"Device Control 3 (XOFF)", L"裝置控制 3（XOFF）" },
        ControlName{ L"DC4", L"Device Control 4", L"裝置控制 4" },
        ControlName{ L"NAK", L"Negative Acknowledge", L"否定確認" },
        ControlName{ L"SYN", L"Synchronous Idle", L"同步閒置" },
        ControlName{ L"ETB", L"End of Transmission Block", L"傳輸區塊結束" },
        ControlName{ L"CAN", L"Cancel", L"取消" },
        ControlName{ L"EM", L"End of Medium", L"媒體結束" },
        ControlName{ L"SUB", L"Substitute", L"替代" },
        ControlName{ L"ESC", L"Escape", L"跳脫" },
        ControlName{ L"FS", L"File Separator", L"檔案分隔" },
        ControlName{ L"GS", L"Group Separator", L"群組分隔" },
        ControlName{ L"RS", L"Record Separator", L"記錄分隔" },
        ControlName{ L"US", L"Unit Separator", L"單位分隔" },
    };

    [[nodiscard]] bool IsManagedWhitespace(wchar_t value) noexcept
    {
        return (value >= L'\u0009' && value <= L'\u000D') ||
            value == L'\u0020' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] std::wstring_view TrimManagedWhitespace(std::wstring_view value) noexcept
    {
        while (!value.empty() && IsManagedWhitespace(value.front())) value.remove_prefix(1);
        while (!value.empty() && IsManagedWhitespace(value.back())) value.remove_suffix(1);
        return value;
    }

    [[nodiscard]] wchar_t FoldSearchChar(wchar_t value) noexcept
    {
        // The managed table builds and queries its searchable values with
        // String.ToLowerInvariant(). The rows contain ASCII, Cantonese text,
        // and Latin-1 glyphs; preserve the complete invariant simple-case
        // mappings that can land in those row values without consulting the
        // machine/user locale. The four singleton mappings below are the
        // non-Latin-1 query values that lower into a table glyph.
        if ((value >= L'A' && value <= L'Z') ||
            (value >= static_cast<wchar_t>(0x00C0u) && value <= static_cast<wchar_t>(0x00D6u)) ||
            (value >= static_cast<wchar_t>(0x00D8u) && value <= static_cast<wchar_t>(0x00DEu)))
        {
            return static_cast<wchar_t>(value + 0x20);
        }

        switch (value)
        {
        case static_cast<wchar_t>(0x0178u): return static_cast<wchar_t>(0x00FFu); // Ÿ → ÿ
        case static_cast<wchar_t>(0x1E9Eu): return static_cast<wchar_t>(0x00DFu); // ẞ → ß
        case static_cast<wchar_t>(0x212Au): return L'k'; // Kelvin sign → k
        case static_cast<wchar_t>(0x212Bu): return static_cast<wchar_t>(0x00E5u); // Ångström sign → å
        default: return value;
        }
    }

    [[nodiscard]] std::wstring FoldSearch(std::wstring_view value)
    {
        std::wstring result;
        result.reserve(value.size());
        for (auto const character : value)
        {
            result.push_back(FoldSearchChar(character));
        }
        return result;
    }

    [[nodiscard]] std::wstring Hexadecimal(int code)
    {
        constexpr std::wstring_view digits{ L"0123456789ABCDEF" };
        std::wstring value{ L"0x00" };
        value[2] = digits[static_cast<std::size_t>((code >> 4) & 0x0F)];
        value[3] = digits[static_cast<std::size_t>(code & 0x0F)];
        return value;
    }

    [[nodiscard]] std::wstring Octal(int code)
    {
        std::wstring reversed;
        do
        {
            reversed.push_back(static_cast<wchar_t>(L'0' + (code & 0x07)));
            code >>= 3;
        } while (code != 0);

        std::wstring value{ L"0o" };
        for (auto index = reversed.rbegin(); index != reversed.rend(); ++index)
        {
            value.push_back(*index);
        }
        return value;
    }

    [[nodiscard]] std::wstring Binary(int code)
    {
        std::wstring value(8, L'0');
        for (int bit{}; bit < 8; ++bit)
        {
            if ((code & (1 << bit)) != 0)
            {
                value[static_cast<std::size_t>(7 - bit)] = L'1';
            }
        }
        return value;
    }

    [[nodiscard]] Row MakeRow(int code, LanguageMode language)
    {
        Row row;
        row.code = code;
        row.decimal = std::to_wstring(code);
        row.hexadecimal = Hexadecimal(code);
        row.octal = Octal(code);
        row.binary = Binary(code);
        row.copy_value.assign(1, static_cast<wchar_t>(code));

        if (code >= 0 && code < static_cast<int>(C0.size()))
        {
            auto const& control = C0[static_cast<std::size_t>(code)];
            row.glyph = control.mnemonic;
            row.name = row.glyph + L" — " + LocalizedText{
                std::wstring(control.english), std::wstring(control.cantonese) }.Pick(language);
        }
        else if (code == 32)
        {
            row.glyph = L"SP";
            row.name = row.glyph + L" — " + LocalizedText{ L"Space", L"空格" }.Pick(language);
        }
        else if (code == 127)
        {
            row.glyph = L"DEL";
            row.name = row.glyph + L" — " + LocalizedText{ L"Delete", L"刪除" }.Pick(language);
        }
        else if (code >= 128 && code <= 160)
        {
            if (code == 160)
            {
                row.glyph = L"NBSP";
                row.name = row.glyph + L" — " + LocalizedText{ L"No-Break Space", L"不換行空格" }.Pick(language);
            }
            else
            {
                row.glyph = L"CTRL";
                row.name = LocalizedText{ L"C1 control", L"C1 控制碼" }.Pick(language);
            }
        }
        else
        {
            row.glyph.assign(1, static_cast<wchar_t>(code));
            row.name = LocalizedText{ L"Printable", L"可列印字元" }.Pick(language);
        }

        row.search_key = FoldSearch(
            row.decimal + L" " + row.hexadecimal + L" " + row.octal + L" " +
            row.binary + L" " + row.glyph + L" " + row.name);
        return row;
    }
}

namespace winforge::core::asciitable
{
    std::vector<Row> Build(bool include_latin1, LanguageMode language)
    {
        std::vector<Row> rows;
        try
        {
            auto const maximum = include_latin1 ? 255 : 127;
            rows.reserve(static_cast<std::size_t>(maximum + 1));
            for (int code{}; code <= maximum; ++code)
            {
                try
                {
                    rows.push_back(MakeRow(code, language));
                }
                catch (...)
                {
                    // Match the managed row builder: one bad presentation row
                    // must never discard the entire local reference table.
                }
            }
        }
        catch (...)
        {
            // Return any rows already built; this core is a local reference,
            // never a reason for the host page to throw.
        }
        return rows;
    }

    std::vector<Row> Filter(std::vector<Row> const& rows, std::wstring_view query)
    {
        try
        {
            auto const folded = FoldSearch(TrimManagedWhitespace(query));
            if (folded.empty()) return rows;

            std::vector<Row> result;
            result.reserve(rows.size());
            for (auto const& row : rows)
            {
                if (row.search_key.find(folded) != std::wstring::npos)
                {
                    result.push_back(row);
                }
            }
            return result;
        }
        catch (...)
        {
            // The managed page falls back to the unfiltered reference on a
            // search failure, which is safer than hiding available rows.
            return rows;
        }
    }

    bool IsInvisibleOrControl(int code) noexcept
    {
        return code >= 0 && (code <= 32 || code == 127 || (code >= 128 && code <= 160));
    }
}
