#include "PasswordStrength.h"

#include <algorithm>
#include <array>
#include <cmath>
#include <cwctype>
#include <limits>
#include <string>

namespace winforge::core::passwordstrength
{
    namespace
    {
        constexpr std::array<std::wstring_view, 100> CommonPasswords{
            L"123456", L"password", L"123456789", L"12345678", L"12345", L"1234567", L"qwerty", L"abc123", L"111111", L"123123",
            L"1234567890", L"1234", L"000000", L"password1", L"qwerty123", L"1q2w3e4r", L"admin", L"letmein", L"welcome", L"monkey",
            L"dragon", L"football", L"iloveyou", L"sunshine", L"princess", L"aa123456", L"654321", L"superman", L"666666", L"987654321",
            L"qwertyuiop", L"121212", L"zaq12wsx", L"passw0rd", L"trustno1", L"master", L"hello", L"freedom", L"whatever", L"qazwsx",
            L"michael", L"batman", L"shadow", L"baseball", L"soccer", L"hockey", L"killer", L"charlie", L"jordan", L"harley",
            L"andrew", L"tigger", L"robert", L"daniel", L"hannah", L"jessica", L"thomas", L"summer", L"ashley", L"jennifer",
            L"starwars", L"computer", L"secret", L"internet", L"service", L"canada", L"hunter", L"buster", L"soccer1", L"liverpool",
            L"test", L"test123", L"guest", L"root", L"admin123", L"login", L"changeme", L"password123", L"p@ssw0rd", L"qwe123",
            L"1qaz2wsx", L"asdfgh", L"asdfghjkl", L"zxcvbnm", L"q1w2e3r4", L"abcd1234", L"a1b2c3d4", L"987654", L"112233", L"696969",
            L"555555", L"777777", L"888888", L"999999", L"google", L"facebook", L"chocolate", L"cheese", L"ninja", L"pokemon",
        };

        constexpr std::array<std::wstring_view, 10> Sequences{
            L"abcdefghijklmnopqrstuvwxyz", L"zyxwvutsrqponmlkjihgfedcba",
            L"0123456789", L"9876543210",
            L"qwertyuiop", L"asdfghjkl", L"zxcvbnm", L"qwerty", L"qazwsx", L"1q2w3e4r",
        };

        [[nodiscard]] wchar_t ToInvariantLower(wchar_t character) noexcept
        {
            if (character >= L'A' && character <= L'Z')
            {
                return static_cast<wchar_t>(character + (L'a' - L'A'));
            }
            return static_cast<wchar_t>(std::towlower(character));
        }

        [[nodiscard]] std::wstring_view Trim(std::wstring_view value) noexcept
        {
            while (!value.empty() && std::iswspace(value.front()))
            {
                value.remove_prefix(1);
            }
            while (!value.empty() && std::iswspace(value.back()))
            {
                value.remove_suffix(1);
            }
            return value;
        }

        [[nodiscard]] bool EqualsOrdinalIgnoreCase(std::wstring_view left, std::wstring_view right) noexcept
        {
            if (left.size() != right.size()) return false;
            for (std::size_t index = 0; index < left.size(); ++index)
            {
                if (ToInvariantLower(left[index]) != ToInvariantLower(right[index])) return false;
            }
            return true;
        }

        [[nodiscard]] bool IsCommon(std::wstring_view password) noexcept
        {
            auto const trimmed = Trim(password);
            return std::any_of(CommonPasswords.begin(), CommonPasswords.end(), [trimmed](std::wstring_view candidate)
            {
                return EqualsOrdinalIgnoreCase(trimmed, candidate);
            });
        }

        [[nodiscard]] bool HasRun(std::wstring_view password, std::size_t count) noexcept
        {
            if (password.empty()) return false;

            std::size_t run{ 1 };
            for (std::size_t index = 1; index < password.size(); ++index)
            {
                run = password[index] == password[index - 1] ? run + 1 : 1;
                if (run >= count) return true;
            }
            return false;
        }

        [[nodiscard]] bool HasSequence(std::wstring_view password) noexcept
        {
            if (password.size() < 3) return false;

            for (auto const sequence : Sequences)
            {
                for (std::size_t start = 0; start + 3 <= sequence.size(); ++start)
                {
                    for (std::size_t position = 0; position + 3 <= password.size(); ++position)
                    {
                        if (ToInvariantLower(password[position]) == sequence[start] &&
                            ToInvariantLower(password[position + 1]) == sequence[start + 1] &&
                            ToInvariantLower(password[position + 2]) == sequence[start + 2])
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        [[nodiscard]] long long RoundToEven(double value) noexcept
        {
            if (!std::isfinite(value) || value <= 0) return 0;
            auto const maximum = static_cast<double>((std::numeric_limits<long long>::max)());
            if (value >= maximum) return (std::numeric_limits<long long>::max)();

            auto const integral = std::floor(value);
            auto const fractional = value - integral;
            if (fractional < 0.5) return static_cast<long long>(integral);
            if (fractional > 0.5) return static_cast<long long>(integral + 1.0);

            auto const rounded = static_cast<long long>(integral);
            return (rounded % 2 == 0) ? rounded : rounded + 1;
        }

        [[nodiscard]] std::wstring FormatGrouped(long long value)
        {
            auto const digits = std::to_wstring(value);
            std::wstring result;
            result.reserve(digits.size() + digits.size() / 3);
            for (std::size_t index = 0; index < digits.size(); ++index)
            {
                if (index != 0 && (digits.size() - index) % 3 == 0)
                {
                    result.push_back(L',');
                }
                result.push_back(digits[index]);
            }
            return result;
        }

        [[nodiscard]] std::wstring FormatTime(
            double value,
            std::wstring_view singular,
            std::wstring_view plural,
            std::wstring_view cantonese,
            HumanTimeLanguage language)
        {
            auto count = RoundToEven(value);
            if (count < 1) count = 1;
            std::wstring result = FormatGrouped(count);
            result.push_back(L' ');
            if (language == HumanTimeLanguage::English)
            {
                result.append(count == 1 ? singular : plural);
            }
            else
            {
                result.append(cantonese);
            }
            return result;
        }

        [[nodiscard]] std::wstring EnglishOrCantonese(
            HumanTimeLanguage language,
            std::wstring_view english,
            std::wstring_view cantonese)
        {
            return std::wstring(language == HumanTimeLanguage::English ? english : cantonese);
        }
    }

    Result Analyze(std::wstring_view password) noexcept
    {
        Result result;
        try
        {
            result.length = static_cast<std::int32_t>(std::min<std::size_t>(
                password.size(), static_cast<std::size_t>((std::numeric_limits<std::int32_t>::max)())));
            if (password.empty()) return result;

            for (auto const character : password)
            {
                if (character >= L'a' && character <= L'z') result.has_lower = true;
                else if (character >= L'A' && character <= L'Z') result.has_upper = true;
                else if (character >= L'0' && character <= L'9') result.has_digit = true;
                else if (character == L' ') result.has_space = true;
                else result.has_symbol = true;
            }

            if (result.has_lower) result.pool_size += 26;
            if (result.has_upper) result.pool_size += 26;
            if (result.has_digit) result.pool_size += 10;
            if (result.has_symbol) result.pool_size += 33;
            if (result.has_space) result.pool_size += 1;
            if (result.pool_size <= 0) result.pool_size = 1;

            result.entropy_bits = static_cast<double>(result.length) * std::log2(static_cast<double>(result.pool_size));
            result.len8 = result.length >= 8;
            result.len12 = result.length >= 12;
            result.len16 = result.length >= 16;
            result.no_repeats = !HasRun(password, 3);
            result.no_sequences = !HasSequence(password);
            result.is_common = IsCommon(password);

            auto effective = result.entropy_bits;
            if (result.is_common)
            {
                effective = (std::min)(effective, 8.0);
            }
            else
            {
                if (!result.no_sequences) effective -= 8.0;
                if (!result.no_repeats) effective -= 6.0;
                if (effective < 0.0) effective = 0.0;
            }

            auto const guesses = std::pow(2.0, (std::max)(0.0, effective - 1.0));
            result.online_seconds = guesses / 1e4;
            result.offline_gpu_seconds = guesses / 1e10;
            result.fast_seconds = guesses / 1e12;

            auto const score = result.is_common ? 0.0 : effective;
            result.band = score < 28.0 ? 0 : score < 40.0 ? 1 : score < 60.0 ? 2 : score < 80.0 ? 3 : 4;
            result.fraction = (std::clamp)(score / 100.0, 0.02, 1.0);
        }
        catch (...)
        {
            // The managed oracle never throws. Preserve any safely computed fields.
        }
        return result;
    }

    std::wstring HumanTime(double seconds, HumanTimeLanguage language) noexcept
    {
        try
        {
            if (!std::isfinite(seconds) || seconds < 0.0)
            {
                seconds = 0.0;
            }

            if (seconds < 1.0)
            {
                return EnglishOrCantonese(language, L"instantly", L"即刻破解");
            }

            if (seconds < 60.0)
            {
                return FormatTime(seconds, L"second", L"seconds", L"秒", language);
            }
            if (seconds < 3600.0)
            {
                return FormatTime(seconds / 60.0, L"minute", L"minutes", L"分鐘", language);
            }
            if (seconds < 86400.0)
            {
                return FormatTime(seconds / 3600.0, L"hour", L"hours", L"小時", language);
            }
            if (seconds < 2592000.0)
            {
                return FormatTime(seconds / 86400.0, L"day", L"days", L"日", language);
            }
            if (seconds < 31536000.0)
            {
                return FormatTime(seconds / 2592000.0, L"month", L"months", L"個月", language);
            }
            if (seconds < 3153600000.0)
            {
                return FormatTime(seconds / 31536000.0, L"year", L"years", L"年", language);
            }
            if (seconds < 3.1536e11)
            {
                return FormatTime(seconds / 3153600000.0, L"century", L"centuries", L"個世紀", language);
            }

            auto const eons = seconds / 3.15576e16;
            if (eons < 1000.0)
            {
                return FormatTime(seconds / 31536000.0, L"year", L"years", L"年", language);
            }
            return EnglishOrCantonese(language, L"effectively forever", L"近乎永遠");
        }
        catch (...)
        {
            return EnglishOrCantonese(language, L"—", L"—");
        }
    }
}
