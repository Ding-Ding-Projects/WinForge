#include "ReferenceText.h"

#include <icu.h>

#include <algorithm>
#include <array>
#include <cstdint>
#include <limits>
#include <optional>
#include <string>
#include <string_view>
#include <utility>
#include <vector>

#pragma comment(lib, "icu.lib")

namespace
{
    using namespace winforge::core::referencetext;

    struct PhoneticEntry
    {
        wchar_t key;
        std::wstring_view word;
    };

    constexpr std::array<PhoneticEntry, 10> DigitWords{
        PhoneticEntry{ L'0', L"Zero" },
        PhoneticEntry{ L'1', L"One" },
        PhoneticEntry{ L'2', L"Two" },
        PhoneticEntry{ L'3', L"Three" },
        PhoneticEntry{ L'4', L"Four" },
        PhoneticEntry{ L'5', L"Five" },
        PhoneticEntry{ L'6', L"Six" },
        PhoneticEntry{ L'7', L"Seven" },
        PhoneticEntry{ L'8', L"Eight" },
        PhoneticEntry{ L'9', L"Niner" },
    };

    constexpr std::array<PhoneticEntry, 26> NatoWords{
        PhoneticEntry{ L'A', L"Alpha" }, PhoneticEntry{ L'B', L"Bravo" },
        PhoneticEntry{ L'C', L"Charlie" }, PhoneticEntry{ L'D', L"Delta" },
        PhoneticEntry{ L'E', L"Echo" }, PhoneticEntry{ L'F', L"Foxtrot" },
        PhoneticEntry{ L'G', L"Golf" }, PhoneticEntry{ L'H', L"Hotel" },
        PhoneticEntry{ L'I', L"India" }, PhoneticEntry{ L'J', L"Juliett" },
        PhoneticEntry{ L'K', L"Kilo" }, PhoneticEntry{ L'L', L"Lima" },
        PhoneticEntry{ L'M', L"Mike" }, PhoneticEntry{ L'N', L"November" },
        PhoneticEntry{ L'O', L"Oscar" }, PhoneticEntry{ L'P', L"Papa" },
        PhoneticEntry{ L'Q', L"Quebec" }, PhoneticEntry{ L'R', L"Romeo" },
        PhoneticEntry{ L'S', L"Sierra" }, PhoneticEntry{ L'T', L"Tango" },
        PhoneticEntry{ L'U', L"Uniform" }, PhoneticEntry{ L'V', L"Victor" },
        PhoneticEntry{ L'W', L"Whiskey" }, PhoneticEntry{ L'X', L"X-ray" },
        PhoneticEntry{ L'Y', L"Yankee" }, PhoneticEntry{ L'Z', L"Zulu" },
    };

    constexpr std::array<PhoneticEntry, 26> PoliceWords{
        PhoneticEntry{ L'A', L"Adam" }, PhoneticEntry{ L'B', L"Boy" },
        PhoneticEntry{ L'C', L"Charlie" }, PhoneticEntry{ L'D', L"David" },
        PhoneticEntry{ L'E', L"Edward" }, PhoneticEntry{ L'F', L"Frank" },
        PhoneticEntry{ L'G', L"George" }, PhoneticEntry{ L'H', L"Henry" },
        PhoneticEntry{ L'I', L"Ida" }, PhoneticEntry{ L'J', L"John" },
        PhoneticEntry{ L'K', L"King" }, PhoneticEntry{ L'L', L"Lincoln" },
        PhoneticEntry{ L'M', L"Mary" }, PhoneticEntry{ L'N', L"Nora" },
        PhoneticEntry{ L'O', L"Ocean" }, PhoneticEntry{ L'P', L"Paul" },
        PhoneticEntry{ L'Q', L"Queen" }, PhoneticEntry{ L'R', L"Robert" },
        PhoneticEntry{ L'S', L"Sam" }, PhoneticEntry{ L'T', L"Tom" },
        PhoneticEntry{ L'U', L"Union" }, PhoneticEntry{ L'V', L"Victor" },
        PhoneticEntry{ L'W', L"William" }, PhoneticEntry{ L'X', L"X-ray" },
        PhoneticEntry{ L'Y', L"Young" }, PhoneticEntry{ L'Z', L"Zebra" },
    };

    constexpr std::array<PhoneticEntry, 26> SimpleWords{
        PhoneticEntry{ L'A', L"Apple" }, PhoneticEntry{ L'B', L"Banana" },
        PhoneticEntry{ L'C', L"Cat" }, PhoneticEntry{ L'D', L"Dog" },
        PhoneticEntry{ L'E', L"Egg" }, PhoneticEntry{ L'F', L"Fish" },
        PhoneticEntry{ L'G', L"Goat" }, PhoneticEntry{ L'H', L"House" },
        PhoneticEntry{ L'I', L"Ice" }, PhoneticEntry{ L'J', L"Juice" },
        PhoneticEntry{ L'K', L"Kite" }, PhoneticEntry{ L'L', L"Lion" },
        PhoneticEntry{ L'M', L"Moon" }, PhoneticEntry{ L'N', L"Nose" },
        PhoneticEntry{ L'O', L"Orange" }, PhoneticEntry{ L'P', L"Pig" },
        PhoneticEntry{ L'Q', L"Queen" }, PhoneticEntry{ L'R', L"Rabbit" },
        PhoneticEntry{ L'S', L"Sun" }, PhoneticEntry{ L'T', L"Tree" },
        PhoneticEntry{ L'U', L"Umbrella" }, PhoneticEntry{ L'V', L"Violin" },
        PhoneticEntry{ L'W', L"Water" }, PhoneticEntry{ L'X', L"Xylophone" },
        PhoneticEntry{ L'Y', L"Yellow" }, PhoneticEntry{ L'Z', L"Zebra" },
    };

    [[nodiscard]] std::array<PhoneticEntry, 26> const& LettersFor(
        PhoneticAlphabet alphabet) noexcept
    {
        switch (alphabet)
        {
        case PhoneticAlphabet::Police: return PoliceWords;
        case PhoneticAlphabet::Simple: return SimpleWords;
        default: return NatoWords;
        }
    }

    template <std::size_t Size>
    [[nodiscard]] std::wstring_view FindWord(
        std::array<PhoneticEntry, Size> const& entries,
        wchar_t key) noexcept
    {
        for (auto const& entry : entries)
        {
            if (entry.key == key) return entry.word;
        }
        return {};
    }

    [[nodiscard]] wchar_t UpperInvariantCodeUnit(wchar_t value) noexcept
    {
        auto const codeUnit = static_cast<std::uint32_t>(
            static_cast<std::uint16_t>(value));

        // ICU's simple uppercase data has one intentional difference from
        // Char.ToUpperInvariant(char): invariant .NET leaves dotless i alone.
        if (codeUnit == 0x0131u) return value;

        auto const upper = static_cast<std::uint32_t>(
            u_toupper(static_cast<UChar32>(codeUnit)));
        return upper <= 0xFFFFu ? static_cast<wchar_t>(upper) : value;
    }

    struct BoxGlyphs
    {
        wchar_t topLeft;
        wchar_t topRight;
        wchar_t bottomLeft;
        wchar_t bottomRight;
        wchar_t horizontal;
        wchar_t vertical;
    };

    [[nodiscard]] BoxGlyphs GlyphsFor(BoxBorderStyle style) noexcept
    {
        switch (style)
        {
        case BoxBorderStyle::Single:
            return { L'\u250C', L'\u2510', L'\u2514', L'\u2518', L'\u2500', L'\u2502' };
        case BoxBorderStyle::Double:
            return { L'\u2554', L'\u2557', L'\u255A', L'\u255D', L'\u2550', L'\u2551' };
        case BoxBorderStyle::Rounded:
            return { L'\u256D', L'\u256E', L'\u2570', L'\u256F', L'\u2500', L'\u2502' };
        case BoxBorderStyle::Heavy:
            return { L'\u250F', L'\u2513', L'\u2517', L'\u251B', L'\u2501', L'\u2503' };
        case BoxBorderStyle::Stars:
            return { L'*', L'*', L'*', L'*', L'*', L'*' };
        default:
            return { L'+', L'+', L'+', L'+', L'-', L'|' };
        }
    }

    [[nodiscard]] bool IsDotNetWhitespace(wchar_t value) noexcept
    {
        auto const codeUnit = static_cast<std::uint32_t>(
            static_cast<std::uint16_t>(value));
        return (codeUnit >= 0x0009u && codeUnit <= 0x000Du) ||
            codeUnit == 0x0020u || codeUnit == 0x0085u || codeUnit == 0x00A0u ||
            codeUnit == 0x1680u || (codeUnit >= 0x2000u && codeUnit <= 0x200Au) ||
            codeUnit == 0x2028u || codeUnit == 0x2029u || codeUnit == 0x202Fu ||
            codeUnit == 0x205Fu || codeUnit == 0x3000u;
    }

    [[nodiscard]] std::wstring TrimDotNetWhitespace(std::wstring value)
    {
        std::size_t first{};
        auto last = value.size();
        while (first < last && IsDotNetWhitespace(value[first])) ++first;
        while (last > first && IsDotNetWhitespace(value[last - 1])) --last;
        if (first == 0 && last == value.size()) return value;
        return value.substr(first, last - first);
    }

    [[nodiscard]] std::wstring NormalizeTitle(std::wstring_view title)
    {
        std::wstring normalized;
        normalized.reserve(title.size());
        for (auto const value : title)
        {
            if (value == L'\r') continue;
            normalized.push_back(value == L'\n' ? L' ' : value);
        }
        return TrimDotNetWhitespace(std::move(normalized));
    }

    [[nodiscard]] std::vector<std::wstring> SplitBoxLines(std::wstring_view text)
    {
        std::vector<std::wstring> lines;
        std::wstring current;
        current.reserve(text.size());

        for (std::size_t index{}; index < text.size(); ++index)
        {
            auto const value = text[index];
            if (value == L'\r' || value == L'\n')
            {
                if (value == L'\r' && index + 1 < text.size() && text[index + 1] == L'\n')
                {
                    ++index;
                }
                lines.push_back(std::move(current));
                current.clear();
                continue;
            }
            if (value == L'\t')
            {
                current.append(4, L' ');
            }
            else
            {
                current.push_back(value);
            }
        }
        lines.push_back(std::move(current));
        return lines;
    }

    [[nodiscard]] bool IsZeroWidthBoxMark(wchar_t value) noexcept
    {
        auto const codeUnit = static_cast<std::uint32_t>(
            static_cast<std::uint16_t>(value));

        // U+0897 entered Unicode after the Windows ICU data used by the native
        // runtime, but .NET 11 categorizes it as NonSpacingMark.
        if (codeUnit == 0x0897u) return true;

        auto const category = u_charType(static_cast<UChar32>(codeUnit));
        return category == U_NON_SPACING_MARK || category == U_ENCLOSING_MARK;
    }

    [[nodiscard]] std::size_t BoxDisplayWidth(std::wstring_view value) noexcept
    {
        std::size_t width{};
        for (auto const codeUnit : value)
        {
            if (!IsZeroWidthBoxMark(codeUnit)) ++width;
        }
        return width;
    }

    [[nodiscard]] std::wstring BuildTitleBar(
        wchar_t horizontal,
        std::size_t inner,
        std::wstring_view title)
    {
        std::wstring label{ L" " };
        label.append(title);
        label.push_back(L' ');
        auto const labelWidth = BoxDisplayWidth(label);
        if (labelWidth + 1 >= inner)
        {
            return std::wstring(std::max<std::size_t>(inner, 1), horizontal);
        }

        constexpr std::size_t lead = 1;
        auto const rest = inner - lead - labelWidth;
        std::wstring result(lead, horizontal);
        result.append(label);
        result.append(rest, horizontal);
        return result;
    }

    void AppendAligned(
        std::wstring& output,
        std::wstring_view line,
        std::size_t inner,
        std::size_t padding,
        BoxAlignment alignment)
    {
        auto const contentWidth = inner >= padding * 2 ? inner - padding * 2 : 0;
        auto const lineWidth = BoxDisplayWidth(line);
        auto const slack = contentWidth >= lineWidth ? contentWidth - lineWidth : 0;

        std::size_t left{};
        std::size_t right{};
        switch (alignment)
        {
        case BoxAlignment::Right:
            left = slack;
            break;
        case BoxAlignment::Center:
            left = slack / 2;
            right = slack - left;
            break;
        default:
            right = slack;
            break;
        }

        output.append(padding, L' ');
        output.append(left, L' ');
        output.append(line);
        output.append(right, L' ');
        output.append(padding, L' ');
    }

    [[nodiscard]] std::wstring RenderCommentSlash(
        std::vector<std::wstring> const& lines,
        std::wstring_view title,
        std::size_t padding)
    {
        std::wstring output{ L"/*\n" };
        if (!title.empty())
        {
            output.append(L" * ").append(title).append(L"\n *\n");
        }
        auto const pad = std::wstring(padding, L' ');
        for (auto const& line : lines)
        {
            output.append(L" * ").append(pad).append(line).push_back(L'\n');
        }
        output.append(L" */");
        return output;
    }

    [[nodiscard]] std::wstring RenderCommentHash(
        std::vector<std::wstring> const& lines,
        std::wstring_view title,
        std::size_t padding)
    {
        auto longest = BoxDisplayWidth(title);
        for (auto const& line : lines)
        {
            longest = std::max(longest, BoxDisplayWidth(line));
        }
        auto const bar = std::max<std::size_t>(3, longest + padding * 2 + 6);

        std::wstring output(bar, L'#');
        output.push_back(L'\n');
        if (!title.empty())
        {
            output.append(L"### ").append(title).push_back(L'\n');
            output.append(bar, L'#').push_back(L'\n');
        }
        auto const pad = std::wstring(padding, L' ');
        for (auto const& line : lines)
        {
            output.append(L"### ").append(pad).append(line).push_back(L'\n');
        }
        output.append(bar, L'#');
        return output;
    }

    struct NamedEntity
    {
        std::wstring_view name;
        std::uint32_t codePoint;
    };

    constexpr NamedEntity NamedEntities[]{
        { L"amp", 38 }, { L"lt", 60 }, { L"gt", 62 }, { L"quot", 34 },
        { L"apos", 39 }, { L"nbsp", 160 },
        { L"iexcl", 161 }, { L"cent", 162 }, { L"pound", 163 }, { L"curren", 164 },
        { L"yen", 165 }, { L"brvbar", 166 }, { L"sect", 167 }, { L"uml", 168 },
        { L"copy", 169 }, { L"ordf", 170 }, { L"laquo", 171 }, { L"not", 172 },
        { L"shy", 173 }, { L"reg", 174 }, { L"macr", 175 }, { L"deg", 176 },
        { L"plusmn", 177 }, { L"sup2", 178 }, { L"sup3", 179 }, { L"acute", 180 },
        { L"micro", 181 }, { L"para", 182 }, { L"middot", 183 }, { L"cedil", 184 },
        { L"sup1", 185 }, { L"ordm", 186 }, { L"raquo", 187 }, { L"frac14", 188 },
        { L"frac12", 189 }, { L"frac34", 190 }, { L"iquest", 191 }, { L"times", 215 },
        { L"divide", 247 },
        { L"Agrave", 192 }, { L"Aacute", 193 }, { L"Acirc", 194 }, { L"Atilde", 195 },
        { L"Auml", 196 }, { L"Aring", 197 }, { L"AElig", 198 }, { L"Ccedil", 199 },
        { L"Egrave", 200 }, { L"Eacute", 201 }, { L"Ecirc", 202 }, { L"Euml", 203 },
        { L"Igrave", 204 }, { L"Iacute", 205 }, { L"Icirc", 206 }, { L"Iuml", 207 },
        { L"ETH", 208 }, { L"Ntilde", 209 }, { L"Ograve", 210 }, { L"Oacute", 211 },
        { L"Ocirc", 212 }, { L"Otilde", 213 }, { L"Ouml", 214 }, { L"Oslash", 216 },
        { L"Ugrave", 217 }, { L"Uacute", 218 }, { L"Ucirc", 219 }, { L"Uuml", 220 },
        { L"Yacute", 221 }, { L"THORN", 222 },
        { L"szlig", 223 }, { L"agrave", 224 }, { L"aacute", 225 }, { L"acirc", 226 },
        { L"atilde", 227 }, { L"auml", 228 }, { L"aring", 229 }, { L"aelig", 230 },
        { L"ccedil", 231 }, { L"egrave", 232 }, { L"eacute", 233 }, { L"ecirc", 234 },
        { L"euml", 235 }, { L"igrave", 236 }, { L"iacute", 237 }, { L"icirc", 238 },
        { L"iuml", 239 }, { L"eth", 240 }, { L"ntilde", 241 }, { L"ograve", 242 },
        { L"oacute", 243 }, { L"ocirc", 244 }, { L"otilde", 245 }, { L"ouml", 246 },
        { L"oslash", 248 }, { L"ugrave", 249 }, { L"uacute", 250 }, { L"ucirc", 251 },
        { L"uuml", 252 }, { L"yacute", 253 }, { L"thorn", 254 }, { L"yuml", 255 },
        { L"OElig", 338 }, { L"oelig", 339 }, { L"Scaron", 352 }, { L"scaron", 353 },
        { L"Yuml", 376 }, { L"fnof", 402 },
        { L"Alpha", 913 }, { L"Beta", 914 }, { L"Gamma", 915 }, { L"Delta", 916 },
        { L"Epsilon", 917 }, { L"Theta", 920 }, { L"Lambda", 923 }, { L"Mu", 924 },
        { L"Pi", 928 }, { L"Sigma", 931 }, { L"Phi", 934 }, { L"Omega", 937 },
        { L"alpha", 945 }, { L"beta", 946 }, { L"gamma", 947 }, { L"delta", 948 },
        { L"epsilon", 949 }, { L"zeta", 950 }, { L"eta", 951 }, { L"theta", 952 },
        { L"lambda", 955 }, { L"mu", 956 }, { L"pi", 960 }, { L"rho", 961 },
        { L"sigma", 963 }, { L"tau", 964 }, { L"phi", 966 }, { L"chi", 967 },
        { L"psi", 968 }, { L"omega", 969 },
        { L"ensp", 8194 }, { L"emsp", 8195 }, { L"thinsp", 8201 }, { L"zwnj", 8204 },
        { L"zwj", 8205 }, { L"lrm", 8206 }, { L"rlm", 8207 }, { L"ndash", 8211 },
        { L"mdash", 8212 }, { L"lsquo", 8216 }, { L"rsquo", 8217 }, { L"sbquo", 8218 },
        { L"ldquo", 8220 }, { L"rdquo", 8221 }, { L"bdquo", 8222 }, { L"dagger", 8224 },
        { L"Dagger", 8225 }, { L"bull", 8226 }, { L"hellip", 8230 }, { L"permil", 8240 },
        { L"prime", 8242 }, { L"Prime", 8243 }, { L"lsaquo", 8249 }, { L"rsaquo", 8250 },
        { L"oline", 8254 }, { L"frasl", 8260 }, { L"euro", 8364 },
        { L"trade", 8482 }, { L"alefsym", 8501 }, { L"larr", 8592 }, { L"uarr", 8593 },
        { L"rarr", 8594 }, { L"darr", 8595 }, { L"harr", 8596 }, { L"crarr", 8629 },
        { L"lArr", 8656 }, { L"uArr", 8657 }, { L"rArr", 8658 }, { L"dArr", 8659 },
        { L"hArr", 8660 },
        { L"forall", 8704 }, { L"part", 8706 }, { L"exist", 8707 }, { L"empty", 8709 },
        { L"nabla", 8711 }, { L"isin", 8712 }, { L"notin", 8713 }, { L"ni", 8715 },
        { L"prod", 8719 }, { L"sum", 8721 }, { L"minus", 8722 }, { L"lowast", 8727 },
        { L"radic", 8730 }, { L"prop", 8733 }, { L"infin", 8734 }, { L"ang", 8736 },
        { L"and", 8743 }, { L"or", 8744 }, { L"cap", 8745 }, { L"cup", 8746 },
        { L"int", 8747 }, { L"there4", 8756 }, { L"sim", 8764 }, { L"cong", 8773 },
        { L"asymp", 8776 }, { L"ne", 8800 }, { L"equiv", 8801 }, { L"le", 8804 },
        { L"ge", 8805 }, { L"sub", 8834 }, { L"sup", 8835 }, { L"sube", 8838 },
        { L"supe", 8839 }, { L"oplus", 8853 }, { L"otimes", 8855 }, { L"perp", 8869 },
        { L"sdot", 8901 }, { L"lceil", 8968 }, { L"rceil", 8969 }, { L"lfloor", 8970 },
        { L"rfloor", 8971 }, { L"loz", 9674 },
        { L"spades", 9824 }, { L"clubs", 9827 }, { L"hearts", 9829 }, { L"diams", 9830 },
        { L"star", 9733 }, { L"starf", 9733 }, { L"check", 10003 }, { L"cross", 10007 },
    };

    struct HtmlReferenceRow
    {
        std::wstring_view name;
        std::uint32_t codePoint;
        std::wstring_view description_en;
        std::wstring_view description_zh;
    };

    constexpr HtmlReferenceRow HtmlReferenceRows[]{
        { L"amp", 38, L"Ampersand", L"and 符號" },
        { L"lt", 60, L"Less-than", L"細於號" },
        { L"gt", 62, L"Greater-than", L"大於號" },
        { L"quot", 34, L"Double quote", L"雙引號" },
        { L"apos", 39, L"Apostrophe", L"撇號" },
        { L"nbsp", 160, L"Non-breaking space", L"不換行空格" },
        { L"copy", 169, L"Copyright", L"版權符號" },
        { L"reg", 174, L"Registered", L"註冊商標符號" },
        { L"trade", 8482, L"Trademark", L"商標符號" },
        { L"cent", 162, L"Cent", L"分幣符號" },
        { L"pound", 163, L"Pound sterling", L"英鎊符號" },
        { L"yen", 165, L"Yen", L"日圓符號" },
        { L"euro", 8364, L"Euro", L"歐元符號" },
        { L"sect", 167, L"Section", L"章節符號" },
        { L"para", 182, L"Pilcrow", L"段落符號" },
        { L"deg", 176, L"Degree", L"度數符號" },
        { L"plusmn", 177, L"Plus-minus", L"正負號" },
        { L"times", 215, L"Multiplication", L"乘號" },
        { L"divide", 247, L"Division", L"除號" },
        { L"frac12", 189, L"One half", L"二分之一" },
        { L"frac14", 188, L"One quarter", L"四分之一" },
        { L"micro", 181, L"Micro", L"微符號" },
        { L"middot", 183, L"Middle dot", L"中點" },
        { L"bull", 8226, L"Bullet", L"項目符號" },
        { L"hellip", 8230, L"Ellipsis", L"省略號" },
        { L"ndash", 8211, L"En dash", L"En 短破折號" },
        { L"mdash", 8212, L"Em dash", L"Em 長破折號" },
        { L"lsquo", 8216, L"Left single quote", L"左單引號" },
        { L"rsquo", 8217, L"Right single quote", L"右單引號" },
        { L"ldquo", 8220, L"Left double quote", L"左雙引號" },
        { L"rdquo", 8221, L"Right double quote", L"右雙引號" },
        { L"dagger", 8224, L"Dagger", L"劍號" },
        { L"laquo", 171, L"Left guillemet", L"左法文引號" },
        { L"raquo", 187, L"Right guillemet", L"右法文引號" },
        { L"larr", 8592, L"Left arrow", L"向左箭咀" },
        { L"rarr", 8594, L"Right arrow", L"向右箭咀" },
        { L"uarr", 8593, L"Up arrow", L"向上箭咀" },
        { L"darr", 8595, L"Down arrow", L"向下箭咀" },
        { L"hearts", 9829, L"Heart", L"紅心" },
        { L"spades", 9824, L"Spade", L"葵扇" },
        { L"clubs", 9827, L"Club", L"梅花" },
        { L"diams", 9830, L"Diamond", L"階磚" },
        { L"star", 9733, L"Star", L"星形" },
        { L"check", 10003, L"Check mark", L"剔號" },
        { L"infin", 8734, L"Infinity", L"無限符號" },
        { L"ne", 8800, L"Not equal", L"不等號" },
        { L"le", 8804, L"Less or equal", L"細於或等於" },
        { L"ge", 8805, L"Greater or equal", L"大於或等於" },
        { L"sum", 8721, L"Summation", L"總和符號" },
        { L"radic", 8730, L"Square root", L"平方根" },
    };

    [[nodiscard]] bool AppendScalar(std::wstring& output, std::uint32_t codePoint)
    {
        if (codePoint > 0x10FFFFu ||
            (codePoint >= 0xD800u && codePoint <= 0xDFFFu))
        {
            return false;
        }

        if constexpr (sizeof(wchar_t) == 2)
        {
            if (codePoint <= 0xFFFFu)
            {
                output.push_back(static_cast<wchar_t>(codePoint));
            }
            else
            {
                codePoint -= 0x10000u;
                output.push_back(static_cast<wchar_t>(0xD800u + (codePoint >> 10)));
                output.push_back(static_cast<wchar_t>(0xDC00u + (codePoint & 0x3FFu)));
            }
        }
        else
        {
            output.push_back(static_cast<wchar_t>(codePoint));
        }
        return true;
    }

    [[nodiscard]] std::wstring ScalarString(std::uint32_t codePoint)
    {
        std::wstring result;
        static_cast<void>(AppendScalar(result, codePoint));
        return result;
    }

    void AppendUpperHex(std::wstring& output, std::uint32_t value)
    {
        constexpr wchar_t Digits[]{ L"0123456789ABCDEF" };
        wchar_t buffer[8]{};
        auto position = std::size(buffer);
        do
        {
            buffer[--position] = Digits[value & 0xFu];
            value >>= 4;
        } while (value != 0);
        output.append(buffer + position, std::size(buffer) - position);
    }

    [[nodiscard]] bool IsNumberWhite(wchar_t value) noexcept
    {
        return value == L' ' || (value >= L'\t' && value <= L'\r');
    }

    [[nodiscard]] std::wstring_view TrimNumberWhite(std::wstring_view value) noexcept
    {
        std::size_t first{};
        auto last = value.size();
        while (first < last && IsNumberWhite(value[first])) ++first;
        while (last > first && IsNumberWhite(value[last - 1])) --last;
        return value.substr(first, last - first);
    }

    [[nodiscard]] bool TryParseDecimalInt32(std::wstring_view value, std::int32_t& result) noexcept
    {
        while (!value.empty() && value.back() == L'\0') value.remove_suffix(1);
        value = TrimNumberWhite(value);
        if (value.empty()) return false;

        bool negative{};
        std::size_t position{};
        if (value[position] == L'+' || value[position] == L'-')
        {
            negative = value[position] == L'-';
            if (++position == value.size()) return false;
        }

        constexpr std::uint64_t PositiveLimit = 2'147'483'647ull;
        constexpr std::uint64_t NegativeLimit = 2'147'483'648ull;
        auto const limit = negative ? NegativeLimit : PositiveLimit;
        std::uint64_t magnitude{};
        for (; position < value.size(); ++position)
        {
            auto const digit = value[position];
            if (digit < L'0' || digit > L'9') return false;
            auto const numeric = static_cast<std::uint64_t>(digit - L'0');
            if (magnitude > (limit - numeric) / 10) return false;
            magnitude = magnitude * 10 + numeric;
        }

        if (negative)
        {
            if (magnitude == NegativeLimit)
            {
                result = std::numeric_limits<std::int32_t>::min();
            }
            else
            {
                result = -static_cast<std::int32_t>(magnitude);
            }
        }
        else
        {
            result = static_cast<std::int32_t>(magnitude);
        }
        return true;
    }

    [[nodiscard]] int HexDigitValue(wchar_t value) noexcept
    {
        if (value >= L'0' && value <= L'9') return value - L'0';
        if (value >= L'a' && value <= L'f') return value - L'a' + 10;
        if (value >= L'A' && value <= L'F') return value - L'A' + 10;
        return -1;
    }

    [[nodiscard]] bool TryParseHexInt32(std::wstring_view value, std::int32_t& result) noexcept
    {
        while (!value.empty() && value.back() == L'\0') value.remove_suffix(1);
        value = TrimNumberWhite(value);
        if (value.empty()) return false;

        std::uint32_t bits{};
        for (auto const character : value)
        {
            auto const digit = HexDigitValue(character);
            if (digit < 0) return false;
            if (bits > (std::numeric_limits<std::uint32_t>::max() -
                static_cast<std::uint32_t>(digit)) / 16u)
            {
                return false;
            }
            bits = bits * 16u + static_cast<std::uint32_t>(digit);
        }
        result = static_cast<std::int32_t>(bits);
        return true;
    }

    [[nodiscard]] std::optional<std::wstring> ResolveEntity(std::wstring_view body)
    {
        if (body.empty()) return std::nullopt;
        if (body.front() == L'#')
        {
            if (body.size() < 2) return std::nullopt;
            std::int32_t code{};
            if (body[1] == L'x' || body[1] == L'X')
            {
                if (body.size() < 3 || !TryParseHexInt32(body.substr(2), code))
                {
                    return std::nullopt;
                }
            }
            else if (!TryParseDecimalInt32(body.substr(1), code))
            {
                return std::nullopt;
            }

            if (code < 0 || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF))
            {
                return std::nullopt;
            }
            return ScalarString(static_cast<std::uint32_t>(code));
        }

        for (auto const& entity : NamedEntities)
        {
            if (entity.name == body) return ScalarString(entity.codePoint);
        }
        return std::nullopt;
    }
}

namespace winforge::core::referencetext
{
    std::wstring_view PhoneticAlphabetDisplayName(PhoneticAlphabet alphabet) noexcept
    {
        switch (alphabet)
        {
        case PhoneticAlphabet::Police: return L"LAPD / Police";
        case PhoneticAlphabet::Simple: return L"Simple words";
        default: return L"NATO / ICAO";
        }
    }

    PhoneticSpellResult SpellPhonetic(
        std::wstring_view input,
        PhoneticAlphabet alphabet,
        bool upper,
        bool keepPunctuation) noexcept
    {
        PhoneticSpellResult result;
        try
        {
            if (input.empty()) return result;
            auto const& table = LettersFor(alphabet);

            for (auto const raw : input)
            {
                auto const shown = upper ? UpperInvariantCodeUnit(raw) : raw;
                auto const key = UpperInvariantCodeUnit(raw);
                std::wstring code;

                if (key >= L'A' && key <= L'Z')
                {
                    auto const word = FindWord(table, key);
                    code.assign(word);
                }
                else if (key >= L'0' && key <= L'9')
                {
                    auto const word = FindWord(DigitWords, key);
                    code.assign(word);
                }
                else if (raw == L' ')
                {
                    code = L"(space)";
                }
                else if (keepPunctuation)
                {
                    code.assign(1, raw);
                }
                else
                {
                    continue;
                }

                result.characters.push_back({ std::wstring(1, shown), code });
                if (!result.spoken.empty()) result.spoken.push_back(L' ');
                result.spoken.append(code);
            }
        }
        catch (...)
        {
            // Match the managed never-throw contract and retain partial output.
        }
        return result;
    }

    std::wstring RenderBoxText(
        std::wstring_view text,
        BoxBorderStyle style,
        int padding,
        BoxAlignment alignment,
        std::wstring_view title) noexcept
    {
        try
        {
            auto const safePadding = static_cast<std::size_t>(std::clamp(padding, 0, 40));
            auto const normalizedTitle = NormalizeTitle(title);
            auto const lines = SplitBoxLines(text);

            if (style == BoxBorderStyle::CommentSlash)
            {
                return RenderCommentSlash(lines, normalizedTitle, safePadding);
            }
            if (style == BoxBorderStyle::CommentHash)
            {
                return RenderCommentHash(lines, normalizedTitle, safePadding);
            }

            auto const glyphs = GlyphsFor(style);
            std::size_t longest{};
            for (auto const& line : lines)
            {
                longest = std::max(longest, BoxDisplayWidth(line));
            }
            auto inner = std::max(longest, BoxDisplayWidth(normalizedTitle)) + safePadding * 2;
            inner = std::max<std::size_t>(inner, 1);

            std::wstring output;
            output.push_back(glyphs.topLeft);
            if (normalizedTitle.empty())
            {
                output.append(inner, glyphs.horizontal);
            }
            else
            {
                output.append(BuildTitleBar(glyphs.horizontal, inner, normalizedTitle));
            }
            output.push_back(glyphs.topRight);
            output.push_back(L'\n');

            for (auto const& line : lines)
            {
                output.push_back(glyphs.vertical);
                AppendAligned(output, line, inner, safePadding, alignment);
                output.push_back(glyphs.vertical);
                output.push_back(L'\n');
            }

            output.push_back(glyphs.bottomLeft);
            output.append(inner, glyphs.horizontal);
            output.push_back(glyphs.bottomRight);
            return output;
        }
        catch (...)
        {
            try
            {
                return std::wstring(text);
            }
            catch (...)
            {
                return {};
            }
        }
    }

    std::wstring EncodeHtmlEntities(std::wstring_view input, bool escapeNonAscii) noexcept
    {
        if (input.empty()) return {};
        try
        {
            std::wstring output;
            output.reserve(input.size() + 16);
            for (std::size_t index{}; index < input.size();)
            {
                auto const first = static_cast<std::uint32_t>(
                    static_cast<std::uint16_t>(input[index]));
                std::uint32_t codePoint{};
                if (first >= 0xD800u && first <= 0xDBFFu && index + 1 < input.size())
                {
                    auto const second = static_cast<std::uint32_t>(
                        static_cast<std::uint16_t>(input[index + 1]));
                    if (second >= 0xDC00u && second <= 0xDFFFu)
                    {
                        codePoint = 0x10000u + ((first - 0xD800u) << 10) + (second - 0xDC00u);
                        index += 2;
                    }
                    else
                    {
                        codePoint = first;
                        ++index;
                    }
                }
                else
                {
                    codePoint = first;
                    ++index;
                }

                switch (codePoint)
                {
                case L'&': output.append(L"&amp;"); continue;
                case L'<': output.append(L"&lt;"); continue;
                case L'>': output.append(L"&gt;"); continue;
                case L'"': output.append(L"&quot;"); continue;
                case L'\'': output.append(L"&#39;"); continue;
                default: break;
                }

                if (escapeNonAscii && (codePoint > 0x7Eu ||
                    (codePoint < 0x20u && codePoint != L'\t' &&
                        codePoint != L'\n' && codePoint != L'\r')))
                {
                    output.append(L"&#x");
                    AppendUpperHex(output, codePoint);
                    output.push_back(L';');
                }
                else if (!AppendScalar(output, codePoint))
                {
                    // ConvertFromUtf32 throws on an isolated surrogate; the
                    // managed outer catch discards all partial encoding.
                    return std::wstring(input);
                }
            }
            return output;
        }
        catch (...)
        {
            try
            {
                return std::wstring(input);
            }
            catch (...)
            {
                return {};
            }
        }
    }

    std::wstring DecodeHtmlEntities(std::wstring_view input) noexcept
    {
        if (input.empty()) return {};
        try
        {
            std::wstring output;
            output.reserve(input.size());
            std::size_t index{};
            while (index < input.size())
            {
                if (input[index] != L'&')
                {
                    output.push_back(input[index++]);
                    continue;
                }

                auto const semicolon = input.find(L';', index + 1);
                if (semicolon == std::wstring_view::npos || semicolon - index > 32)
                {
                    output.push_back(input[index++]);
                    continue;
                }

                auto const body = input.substr(index + 1, semicolon - index - 1);
                auto resolved = ResolveEntity(body);
                if (resolved)
                {
                    output.append(*resolved);
                    index = semicolon + 1;
                }
                else
                {
                    output.push_back(input[index++]);
                }
            }
            return output;
        }
        catch (...)
        {
            try
            {
                return std::wstring(input);
            }
            catch (...)
            {
                return {};
            }
        }
    }

    std::vector<HtmlEntityReference> const& HtmlEntityReferenceList() noexcept
    {
        static std::vector<HtmlEntityReference> const references = []
        {
            std::vector<HtmlEntityReference> result;
            result.reserve(std::size(HtmlReferenceRows));
            for (auto const& row : HtmlReferenceRows)
            {
                std::wstring name{ L"&" };
                name.append(row.name).push_back(L';');
                result.push_back({
                    std::move(name), ScalarString(row.codePoint),
                    std::wstring(row.description_en), std::wstring(row.description_zh)
                });
            }
            return result;
        }();
        return references;
    }

    std::size_t HtmlEntityUtf16Length(std::wstring_view input) noexcept
    {
        return input.size();
    }
}
