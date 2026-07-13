#include "CheckDigit.h"

#include <algorithm>
#include <array>
#include <cwctype>

namespace
{
    using winforge::core::checkdigit::Result;

    Result Fail(std::wstring en, std::wstring zh)
    {
        return Result{ false, false, std::move(en), std::move(zh), {} };
    }

    constexpr bool IsAsciiDigit(wchar_t character)
    {
        return character >= L'0' && character <= L'9';
    }

    constexpr bool IsAsciiLetter(wchar_t character)
    {
        return (character >= L'A' && character <= L'Z')
            || (character >= L'a' && character <= L'z');
    }

    struct IbanFormat
    {
        std::wstring_view country;
        std::size_t length;
        std::wstring_view bban_pattern;
    };

    // SWIFT ISO 13616 IBAN Registry, Release 102 (June 2026). Pattern symbols:
    // n = ASCII digit, a = ASCII uppercase letter, c = ASCII alphanumeric.
    constexpr std::array<IbanFormat, 89> IbanFormats{
        IbanFormat{ L"AD", 24, L"4!n4!n12!c" },
        IbanFormat{ L"AE", 23, L"3!n16!n" },
        IbanFormat{ L"AL", 28, L"8!n16!c" },
        IbanFormat{ L"AT", 20, L"5!n11!n" },
        IbanFormat{ L"AZ", 28, L"4!a20!c" },
        IbanFormat{ L"BA", 20, L"3!n3!n8!n2!n" },
        IbanFormat{ L"BE", 16, L"3!n7!n2!n" },
        IbanFormat{ L"BG", 22, L"4!a4!n2!n8!c" },
        IbanFormat{ L"BH", 22, L"4!a14!c" },
        IbanFormat{ L"BI", 27, L"5!n5!n11!n2!n" },
        IbanFormat{ L"BR", 29, L"8!n5!n10!n1!a1!c" },
        IbanFormat{ L"BY", 28, L"4!c4!n16!c" },
        IbanFormat{ L"CH", 21, L"5!n12!c" },
        IbanFormat{ L"CR", 22, L"4!n14!n" },
        IbanFormat{ L"CY", 28, L"3!n5!n16!c" },
        IbanFormat{ L"CZ", 24, L"4!n16!n" },
        IbanFormat{ L"DE", 22, L"8!n10!n" },
        IbanFormat{ L"DJ", 27, L"5!n5!n11!n2!n" },
        IbanFormat{ L"DK", 18, L"4!n9!n1!n" },
        IbanFormat{ L"DO", 28, L"4!c20!n" },
        IbanFormat{ L"EE", 20, L"2!n14!n" },
        IbanFormat{ L"EG", 29, L"4!n4!n17!n" },
        IbanFormat{ L"ES", 24, L"4!n4!n1!n1!n10!n" },
        IbanFormat{ L"FI", 18, L"3!n11!n" },
        IbanFormat{ L"FK", 18, L"2!a12!n" },
        IbanFormat{ L"FO", 18, L"4!n9!n1!n" },
        IbanFormat{ L"FR", 27, L"5!n5!n11!c2!n" },
        IbanFormat{ L"GB", 22, L"4!a6!n8!n" },
        IbanFormat{ L"GE", 22, L"2!a16!n" },
        IbanFormat{ L"GI", 23, L"4!a15!c" },
        IbanFormat{ L"GL", 18, L"4!n9!n1!n" },
        IbanFormat{ L"GR", 27, L"3!n4!n16!c" },
        IbanFormat{ L"GT", 28, L"4!c20!c" },
        IbanFormat{ L"HN", 28, L"4!a20!n" },
        IbanFormat{ L"HR", 21, L"7!n10!n" },
        IbanFormat{ L"HU", 28, L"3!n4!n1!n15!n1!n" },
        IbanFormat{ L"IE", 22, L"4!a6!n8!n" },
        IbanFormat{ L"IL", 23, L"3!n3!n13!n" },
        IbanFormat{ L"IQ", 23, L"4!a3!n12!n" },
        IbanFormat{ L"IS", 26, L"4!n2!n6!n10!n" },
        IbanFormat{ L"IT", 27, L"1!a5!n5!n12!c" },
        IbanFormat{ L"JO", 30, L"4!a4!n18!c" },
        IbanFormat{ L"KW", 30, L"4!a22!c" },
        IbanFormat{ L"KZ", 20, L"3!n13!c" },
        IbanFormat{ L"LB", 28, L"4!n20!c" },
        IbanFormat{ L"LC", 32, L"4!a24!c" },
        IbanFormat{ L"LI", 21, L"5!n12!c" },
        IbanFormat{ L"LT", 20, L"5!n11!n" },
        IbanFormat{ L"LU", 20, L"3!n13!c" },
        IbanFormat{ L"LV", 21, L"4!a13!c" },
        IbanFormat{ L"LY", 25, L"3!n3!n15!n" },
        IbanFormat{ L"MC", 27, L"5!n5!n11!c2!n" },
        IbanFormat{ L"MD", 24, L"2!c18!c" },
        IbanFormat{ L"ME", 22, L"3!n13!n2!n" },
        IbanFormat{ L"MK", 19, L"3!n10!c2!n" },
        IbanFormat{ L"MN", 20, L"4!n12!n" },
        IbanFormat{ L"MR", 27, L"5!n5!n11!n2!n" },
        IbanFormat{ L"MT", 31, L"4!a5!n18!c" },
        IbanFormat{ L"MU", 30, L"4!a2!n2!n12!n3!n3!a" },
        IbanFormat{ L"NI", 28, L"4!a20!n" },
        IbanFormat{ L"NL", 18, L"4!a10!n" },
        IbanFormat{ L"NO", 15, L"4!n6!n1!n" },
        IbanFormat{ L"OM", 23, L"3!n16!c" },
        IbanFormat{ L"PK", 24, L"4!a16!c" },
        IbanFormat{ L"PL", 28, L"8!n16!n" },
        IbanFormat{ L"PS", 29, L"4!a21!c" },
        IbanFormat{ L"PT", 25, L"4!n4!n11!n2!n" },
        IbanFormat{ L"QA", 29, L"4!a21!c" },
        IbanFormat{ L"RO", 24, L"4!a16!c" },
        IbanFormat{ L"RS", 22, L"3!n13!n2!n" },
        IbanFormat{ L"RU", 33, L"9!n5!n15!c" },
        IbanFormat{ L"SA", 24, L"2!n18!c" },
        IbanFormat{ L"SC", 31, L"4!a2!n2!n16!n3!a" },
        IbanFormat{ L"SD", 18, L"2!n12!n" },
        IbanFormat{ L"SE", 24, L"3!n16!n1!n" },
        IbanFormat{ L"SI", 19, L"5!n8!n2!n" },
        IbanFormat{ L"SK", 24, L"4!n6!n10!n" },
        IbanFormat{ L"SM", 27, L"1!a5!n5!n12!c" },
        IbanFormat{ L"SO", 23, L"4!n3!n12!n" },
        IbanFormat{ L"ST", 25, L"4!n4!n11!n2!n" },
        IbanFormat{ L"SV", 28, L"4!a20!n" },
        IbanFormat{ L"TL", 23, L"3!n14!n2!n" },
        IbanFormat{ L"TN", 24, L"2!n3!n13!n2!n" },
        IbanFormat{ L"TR", 26, L"5!n1!n16!c" },
        IbanFormat{ L"UA", 29, L"6!n19!c" },
        IbanFormat{ L"VA", 22, L"3!n15!n" },
        IbanFormat{ L"VG", 24, L"4!a16!n" },
        IbanFormat{ L"XK", 20, L"4!n10!n2!n" },
        IbanFormat{ L"YE", 30, L"4!a4!n18!c" },
    };

    constexpr std::size_t IbanPatternLength(std::wstring_view pattern)
    {
        std::size_t result = 0;
        std::size_t index = 0;
        while (index < pattern.size())
        {
            std::size_t count = 0;
            while (index < pattern.size() && IsAsciiDigit(pattern[index]))
            {
                count = (count * 10) + static_cast<std::size_t>(pattern[index] - L'0');
                ++index;
            }
            if (count == 0 || index + 1 >= pattern.size() || pattern[index] != L'!')
            {
                return 0;
            }
            ++index;
            if (pattern[index] != L'n' && pattern[index] != L'a' && pattern[index] != L'c')
            {
                return 0;
            }
            ++index;
            result += count;
        }
        return result;
    }

    constexpr bool IbanFormatsAreConsistent()
    {
        for (std::size_t index = 0; index < IbanFormats.size(); ++index)
        {
            auto const& format = IbanFormats[index];
            if (format.country.size() != 2 || format.length > 34 ||
                IbanPatternLength(format.bban_pattern) + 4 != format.length)
            {
                return false;
            }
            if (index > 0 && IbanFormats[index - 1].country >= format.country)
            {
                return false;
            }
        }
        return true;
    }

    static_assert(IbanFormatsAreConsistent(), "IBAN registry table must be sorted, unique, and structurally consistent.");

    IbanFormat const* FindIbanFormat(std::wstring_view country)
    {
        auto const found = std::find_if(IbanFormats.begin(), IbanFormats.end(), [country](IbanFormat const& format)
        {
            return format.country == country;
        });
        return found == IbanFormats.end() ? nullptr : &*found;
    }

    bool MatchesIbanPattern(std::wstring_view value, std::wstring_view pattern)
    {
        std::size_t valueIndex = 0;
        std::size_t patternIndex = 0;
        while (patternIndex < pattern.size())
        {
            std::size_t count = 0;
            while (patternIndex < pattern.size() && IsAsciiDigit(pattern[patternIndex]))
            {
                count = (count * 10) + static_cast<std::size_t>(pattern[patternIndex] - L'0');
                ++patternIndex;
            }
            if (count == 0 || patternIndex + 1 >= pattern.size() || pattern[patternIndex] != L'!')
            {
                return false;
            }
            ++patternIndex;
            auto const kind = pattern[patternIndex++];
            if (valueIndex + count > value.size())
            {
                return false;
            }
            for (std::size_t offset = 0; offset < count; ++offset)
            {
                auto const character = value[valueIndex++];
                auto const matches = kind == L'n'
                    ? IsAsciiDigit(character)
                    : kind == L'a'
                        ? character >= L'A' && character <= L'Z'
                        : kind == L'c'
                            ? IsAsciiDigit(character) || (character >= L'A' && character <= L'Z')
                            : false;
                if (!matches)
                {
                    return false;
                }
            }
        }
        return valueIndex == value.size();
    }

    std::wstring Trim(std::wstring value)
    {
        auto const first = std::find_if_not(value.begin(), value.end(), [](wchar_t character)
        {
            return std::iswspace(character) != 0;
        });
        auto const last = std::find_if_not(value.rbegin(), value.rend(), [](wchar_t character)
        {
            return std::iswspace(character) != 0;
        }).base();
        if (first >= last)
        {
            return {};
        }
        return std::wstring(first, last);
    }

    std::wstring CleanDigits(std::wstring_view input)
    {
        std::wstring result;
        result.reserve(input.size());
        for (auto const character : input)
        {
            if (character != L' ' && character != L'-')
            {
                result.push_back(character);
            }
        }
        return Trim(std::move(result));
    }

    bool HasOnlyAsciiDigits(std::wstring_view value)
    {
        return std::all_of(value.begin(), value.end(), IsAsciiDigit);
    }

    int LuhnCheckDigit(std::wstring_view body)
    {
        int sum = 0;
        bool doubleDigit = true;
        for (auto iterator = body.rbegin(); iterator != body.rend(); ++iterator)
        {
            int digit = *iterator - L'0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum = (sum + digit) % 10;
            doubleDigit = !doubleDigit;
        }
        return (10 - (sum % 10)) % 10;
    }

    bool StartsWith(std::wstring_view value, std::wstring_view prefix)
    {
        return value.starts_with(prefix);
    }

    int ParsePrefix(std::wstring_view value, std::size_t count)
    {
        int result = 0;
        for (std::size_t index = 0; index < count; ++index)
        {
            result = (result * 10) + (value[index] - L'0');
        }
        return result;
    }

    std::wstring DetectBrand(std::wstring_view digits)
    {
        auto const length = digits.size();
        if (StartsWith(digits, L"4") && (length == 13 || length == 16 || length == 19))
        {
            return L"Visa";
        }
        if (length == 15 && (StartsWith(digits, L"34") || StartsWith(digits, L"37")))
        {
            return L"Amex";
        }
        if (length == 16)
        {
            auto const prefix2 = ParsePrefix(digits, 2);
            auto const prefix4 = ParsePrefix(digits, 4);
            if ((prefix2 >= 51 && prefix2 <= 55) || (prefix4 >= 2221 && prefix4 <= 2720))
            {
                return L"Mastercard";
            }
        }
        if (length >= 16 && length <= 19)
        {
            auto const prefix8 = ParsePrefix(digits, 8);
            if ((prefix8 >= 60110000 && prefix8 <= 60119999)
                || (prefix8 >= 64400000 && prefix8 <= 65899999))
            {
                return L"Discover";
            }
        }
        return {};
    }

    Result ValidateLuhn(std::wstring_view input)
    {
        auto const digits = CleanDigits(input);
        if (digits.empty()) return Fail(L"Enter a card / number.", L"請輸入卡號或數字。");
        if (!HasOnlyAsciiDigits(digits)) return Fail(L"Digits only for Luhn.", L"Luhn 只接受數字。");
        if (digits.size() < 2) return Fail(L"Too short.", L"太短。");

        int sum = 0;
        bool doubleDigit = false;
        for (auto iterator = digits.rbegin(); iterator != digits.rend(); ++iterator)
        {
            int digit = *iterator - L'0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum = (sum + digit) % 10;
            doubleDigit = !doubleDigit;
        }

        auto const check = LuhnCheckDigit(std::wstring_view(digits).substr(0, digits.size() - 1));
        auto const computed = std::to_wstring(check);
        auto const brand = DetectBrand(digits);
        auto detailEn = L"Expected last digit: " + computed;
        auto detailZh = L"應有嘅尾數：" + computed;
        if (!brand.empty())
        {
            detailEn += L" · brand: " + brand;
            detailZh += L" · 卡種：" + brand;
        }
        return Result{ true, sum == 0, std::move(detailEn), std::move(detailZh), computed };
    }

    int WeightedIsbn10(std::wstring_view firstNine)
    {
        int sum = 0;
        for (std::size_t index = 0; index < firstNine.size(); ++index)
        {
            sum += (firstNine[index] - L'0') * (10 - static_cast<int>(index));
        }
        return sum;
    }

    Result ValidateIsbn10(std::wstring_view input)
    {
        auto digits = CleanDigits(input);
        std::transform(digits.begin(), digits.end(), digits.begin(), [](wchar_t character)
        {
            return static_cast<wchar_t>(std::towupper(character));
        });
        if (digits.empty()) return Fail(L"Enter an ISBN-10.", L"請輸入 ISBN-10。");
        if (digits.size() != 10) return Fail(L"ISBN-10 needs 10 characters.", L"ISBN-10 要 10 個字元。");

        int sum = 0;
        for (std::size_t index = 0; index < digits.size(); ++index)
        {
            int value = 0;
            if (digits[index] == L'X' && index == 9)
            {
                value = 10;
            }
            else if (IsAsciiDigit(digits[index]))
            {
                value = digits[index] - L'0';
            }
            else
            {
                return Fail(L"Only digits, plus X as the last check.", L"只可用數字，尾位可用 X。");
            }
            sum += value * (10 - static_cast<int>(index));
        }

        auto const check = (11 - (WeightedIsbn10(std::wstring_view(digits).substr(0, 9)) % 11)) % 11;
        auto const computed = check == 10 ? std::wstring(L"X") : std::to_wstring(check);
        return Result{
            true,
            sum % 11 == 0,
            L"Expected check: " + computed,
            L"應有檢查碼：" + computed,
            computed };
    }

    int Mod10Weighted(std::wstring_view body, bool oddWeight3FromLeft)
    {
        int sum = 0;
        for (std::size_t index = 0; index < body.size(); ++index)
        {
            auto const digit = body[index] - L'0';
            auto const weightThree = oddWeight3FromLeft ? index % 2 == 0 : index % 2 == 1;
            sum += digit * (weightThree ? 3 : 1);
        }
        return (10 - (sum % 10)) % 10;
    }

    Result ValidateEan13(std::wstring_view input, bool isbn)
    {
        auto const digits = CleanDigits(input);
        auto const label = isbn ? std::wstring(L"ISBN-13") : std::wstring(L"EAN-13");
        if (digits.empty()) return Fail(L"Enter an " + label + L".", L"請輸入 " + label + L"。");
        if (!HasOnlyAsciiDigits(digits)) return Fail(L"Digits only.", L"只可用數字。");
        if (digits.size() != 13) return Fail(label + L" needs 13 digits.", label + L" 要 13 個數字。");
        if (isbn && !StartsWith(digits, L"978") && !StartsWith(digits, L"979"))
        {
            return Fail(L"ISBN-13 must start with 978 or 979.", L"ISBN-13 開頭一定要係 978 或 979。");
        }
        auto const check = Mod10Weighted(std::wstring_view(digits).substr(0, 12), false);
        auto const computed = std::to_wstring(check);
        return Result{
            true,
            digits[12] - L'0' == check,
            L"Expected check digit: " + computed,
            L"應有檢查碼：" + computed,
            computed };
    }

    Result ValidateUpcA(std::wstring_view input)
    {
        auto const digits = CleanDigits(input);
        if (digits.empty()) return Fail(L"Enter a UPC-A.", L"請輸入 UPC-A。");
        if (!HasOnlyAsciiDigits(digits)) return Fail(L"Digits only.", L"只可用數字。");
        if (digits.size() != 12) return Fail(L"UPC-A needs 12 digits.", L"UPC-A 要 12 個數字。");
        auto const check = Mod10Weighted(std::wstring_view(digits).substr(0, 11), true);
        auto const computed = std::to_wstring(check);
        return Result{
            true,
            digits[11] - L'0' == check,
            L"Expected check digit: " + computed,
            L"應有檢查碼：" + computed,
            computed };
    }

    int AppendMod97(int remainder, wchar_t character)
    {
        if (IsAsciiDigit(character))
        {
            return ((remainder * 10) + (character - L'0')) % 97;
        }
        auto const value = character - L'A' + 10;
        return ((remainder * 100) + value) % 97;
    }

    int IbanRemainder(std::wstring_view value)
    {
        int remainder = 0;
        for (auto const character : value)
        {
            remainder = AppendMod97(remainder, character);
        }
        return remainder;
    }

    std::wstring ComputeIbanCheck(std::wstring_view raw)
    {
        std::wstring rearranged(raw.substr(4));
        rearranged.append(raw.substr(0, 2));
        rearranged.append(L"00");
        auto const check = 98 - IbanRemainder(rearranged);
        auto result = std::to_wstring(check);
        if (result.size() == 1) result.insert(result.begin(), L'0');
        return result;
    }

    Result ValidateIban(std::wstring_view input)
    {
        auto raw = CleanDigits(input);
        std::transform(raw.begin(), raw.end(), raw.begin(), [](wchar_t character)
        {
            return character >= L'a' && character <= L'z'
                ? static_cast<wchar_t>(character - L'a' + L'A')
                : character;
        });
        if (raw.empty()) return Fail(L"Enter an IBAN.", L"請輸入 IBAN。");
        if (raw.size() < 4 || raw.size() > 34)
        {
            return Fail(L"IBAN must include a country and two check digits.", L"IBAN 要包含國家代碼同兩個檢查碼。");
        }
        if (!std::all_of(raw.begin(), raw.end(), [](wchar_t character)
        {
            return IsAsciiDigit(character) || (character >= L'A' && character <= L'Z');
        }))
        {
            return Fail(L"IBAN uses letters & digits only.", L"IBAN 只可用字母同數字。");
        }
        if (!IsAsciiLetter(raw[0]) || !IsAsciiLetter(raw[1]))
        {
            return Fail(L"IBAN must start with a 2-letter country.", L"IBAN 開頭要兩個國家字母。");
        }
        if (!IsAsciiDigit(raw[2]) || !IsAsciiDigit(raw[3]))
        {
            return Fail(L"IBAN check digits must be numeric.", L"IBAN 檢查碼一定要係數字。");
        }

        auto const country = std::wstring_view(raw).substr(0, 2);
        auto const format = FindIbanFormat(country);
        if (!format)
        {
            return Fail(
                L"Unsupported IBAN country: " + std::wstring(country) + L".",
                L"未支援嘅 IBAN 國家代碼：" + std::wstring(country) + L"。");
        }
        if (raw.size() != format->length)
        {
            auto const expected = std::to_wstring(format->length);
            return Fail(
                std::wstring(country) + L" IBAN needs " + expected + L" characters.",
                std::wstring(country) + L" IBAN 要 " + expected + L" 個字元。");
        }
        if (!MatchesIbanPattern(std::wstring_view(raw).substr(4), format->bban_pattern))
        {
            return Fail(
                L"BBAN does not match the registered " + std::wstring(country) + L" format.",
                L"BBAN 唔符合已登記嘅 " + std::wstring(country) + L" 格式。");
        }

        std::wstring rearranged(raw.substr(4));
        rearranged.append(raw.substr(0, 4));
        auto const remainder = IbanRemainder(rearranged);
        auto const computed = ComputeIbanCheck(raw);
        return Result{
            true,
            remainder == 1,
            L"mod-97 = " + std::to_wstring(remainder) + L" (valid = 1) · correct check digits: " + computed,
            L"mod-97 = " + std::to_wstring(remainder) + L"（正確係 1）· 正確檢查碼：" + computed,
            computed };
    }
}

namespace winforge::core::checkdigit
{
    std::wstring IbanRegistryCanonicalForTests()
    {
        std::wstring result;
        result.reserve(IbanFormats.size() * 32);
        for (auto const& format : IbanFormats)
        {
            result.append(format.country);
            result.push_back(L'\t');
            result.append(std::to_wstring(format.length));
            result.push_back(L'\t');
            result.append(format.bban_pattern);
            result.push_back(L'\n');
        }
        return result;
    }

    Result Validate(Scheme scheme, std::wstring_view input) noexcept
    {
        try
        {
            switch (scheme)
            {
            case Scheme::Luhn:
                return ValidateLuhn(input);
            case Scheme::Isbn10:
                return ValidateIsbn10(input);
            case Scheme::Isbn13:
                return ValidateEan13(input, true);
            case Scheme::Ean13:
                return ValidateEan13(input, false);
            case Scheme::UpcA:
                return ValidateUpcA(input);
            case Scheme::Iban:
                return ValidateIban(input);
            default:
                return Fail(L"Unknown scheme.", L"未知格式。");
            }
        }
        catch (std::exception const& error)
        {
            auto const message = std::wstring(error.what(), error.what() + std::char_traits<char>::length(error.what()));
            return Fail(L"Could not validate: " + message, L"無法驗證：" + message);
        }
        catch (...)
        {
            return Fail(L"Could not validate.", L"無法驗證。");
        }
    }
}
