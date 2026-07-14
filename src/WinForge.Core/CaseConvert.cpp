#include "CaseConvert.h"

#include <Windows.h>

#include <array>
#include <cstdint>
#include <mutex>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    using u_charType_fn = std::int32_t(__cdecl*)(std::int32_t);
    using u_tolower_fn = std::int32_t(__cdecl*)(std::int32_t);
    using u_toupper_fn = std::int32_t(__cdecl*)(std::int32_t);
    using u_isUWhiteSpace_fn = std::uint8_t(__cdecl*)(std::int32_t);

    constexpr std::uint32_t kLetter = 1;
    constexpr std::uint32_t kLower = 2;
    constexpr std::uint32_t kTitle = 3;
    constexpr std::uint32_t kModifier = 4;
    constexpr std::uint32_t kOther = 5;
    constexpr std::uint32_t kDigit = 9;

    struct UnicodeApi
    {
        HMODULE module{};
        u_charType_fn charType{};
        u_tolower_fn toLower{};
        u_toupper_fn toUpper{};
        u_isUWhiteSpace_fn isWhiteSpace{};

        [[nodiscard]] bool Loaded() const noexcept
        {
            return charType && toLower && toUpper && isWhiteSpace;
        }
    };

    [[nodiscard]] UnicodeApi LoadUnicodeApi()
    {
        UnicodeApi api;
        auto const module = LoadLibraryExW(L"icu.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        if (!module)
        {
            return api;
        }

        api.module = module;
        api.charType = reinterpret_cast<u_charType_fn>(GetProcAddress(module, "u_charType"));
        api.toLower = reinterpret_cast<u_tolower_fn>(GetProcAddress(module, "u_tolower"));
        api.toUpper = reinterpret_cast<u_toupper_fn>(GetProcAddress(module, "u_toupper"));
        api.isWhiteSpace = reinterpret_cast<u_isUWhiteSpace_fn>(GetProcAddress(module, "u_isUWhiteSpace"));
        if (!api.Loaded())
        {
            FreeLibrary(module);
            api = {};
        }
        return api;
    }

    [[nodiscard]] UnicodeApi const& Unicode()
    {
        static const UnicodeApi api = LoadUnicodeApi();
        return api;
    }

    [[nodiscard]] bool IsExplicitSeparator(wchar_t value)
    {
        return value == L' ' || value == L'\t' || value == L'\r' || value == L'\n' ||
            value == L'_' || value == L'-' || value == L'.' || value == L'/' ||
            value == L'\\' || value == L':';
    }

    [[nodiscard]] bool IsLetterOrDigit(wchar_t value)
    {
        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        auto const type = Unicode().Loaded()
            ? static_cast<std::uint32_t>(Unicode().charType(code))
            : 0u;

        switch (type)
        {
        case kLetter:
        case kLower:
        case kTitle:
        case kModifier:
        case kOther:
        case kDigit:
            return true;
        default:
            break;
        }

        switch (value)
        {
        case static_cast<wchar_t>(0x1C89):
        case static_cast<wchar_t>(0x1C8A):
        case static_cast<wchar_t>(0xA7CB):
        case static_cast<wchar_t>(0xA7CC):
        case static_cast<wchar_t>(0xA7CD):
        case static_cast<wchar_t>(0xA7DA):
        case static_cast<wchar_t>(0xA7DB):
        case static_cast<wchar_t>(0xA7DC):
            return true;
        default:
            return false;
        }
    }

    [[nodiscard]] bool IsDigit(wchar_t value)
    {
        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        return Unicode().Loaded()
            ? static_cast<std::uint32_t>(Unicode().charType(code)) == kDigit
            : value >= L'0' && value <= L'9';
    }

    [[nodiscard]] bool IsLower(wchar_t value)
    {
        switch (value)
        {
        case static_cast<wchar_t>(0x1C8A):
        case static_cast<wchar_t>(0xA7CD):
        case static_cast<wchar_t>(0xA7DB):
            return true;
        default:
            break;
        }

        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        return Unicode().Loaded()
            ? static_cast<std::uint32_t>(Unicode().charType(code)) == kLower
            : value >= L'a' && value <= L'z';
    }

    [[nodiscard]] bool IsUpper(wchar_t value)
    {
        switch (value)
        {
        case static_cast<wchar_t>(0x1C89):
        case static_cast<wchar_t>(0xA7CB):
        case static_cast<wchar_t>(0xA7CC):
        case static_cast<wchar_t>(0xA7DA):
        case static_cast<wchar_t>(0xA7DC):
            return true;
        default:
            break;
        }

        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        return Unicode().Loaded()
            ? static_cast<std::uint32_t>(Unicode().charType(code)) == kLetter
            : value >= L'A' && value <= L'Z';
    }

    [[nodiscard]] bool IsWhiteSpace(wchar_t value)
    {
        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        if (Unicode().Loaded())
        {
            return Unicode().isWhiteSpace(code) != 0;
        }

        return (value >= L'\t' && value <= L'\r') ||
            value == L' ' || value == L'\u0085' || value == L'\u00A0' ||
            value == L'\u1680' || (value >= L'\u2000' && value <= L'\u200A') ||
            value == L'\u2028' || value == L'\u2029' || value == L'\u202F' ||
            value == L'\u205F' || value == L'\u3000';
    }

    [[nodiscard]] wchar_t ToLowerInvariantChar(wchar_t value)
    {
        if (value == static_cast<wchar_t>(0x0130))
        {
            return value;
        }

        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        if (Unicode().Loaded())
        {
            return static_cast<wchar_t>(Unicode().toLower(code));
        }
        if (value >= L'A' && value <= L'Z')
        {
            return static_cast<wchar_t>(value - L'A' + L'a');
        }
        return value;
    }

    [[nodiscard]] wchar_t ToUpperInvariantChar(wchar_t value)
    {
        if (value == static_cast<wchar_t>(0x0131))
        {
            return value;
        }

        auto const code = static_cast<std::int32_t>(static_cast<std::uint16_t>(value));
        if (Unicode().Loaded())
        {
            return static_cast<wchar_t>(Unicode().toUpper(code));
        }
        if (value >= L'a' && value <= L'z')
        {
            return static_cast<wchar_t>(value - L'a' + L'A');
        }
        return value;
    }

    [[nodiscard]] std::wstring ToLowerInvariant(std::wstring_view value)
    {
        std::wstring output;
        output.reserve(value.size());
        for (auto const character : value)
        {
            output.push_back(ToLowerInvariantChar(character));
        }
        return output;
    }

    [[nodiscard]] std::wstring Cap(std::wstring_view word)
    {
        if (word.empty())
        {
            return {};
        }

        std::wstring output;
        output.reserve(word.size());
        output.push_back(ToUpperInvariantChar(word.front()));
        for (std::size_t index = 1; index < word.size(); ++index)
        {
            output.push_back(ToLowerInvariantChar(word[index]));
        }
        return output;
    }

    [[nodiscard]] std::wstring JoinWords(std::vector<std::wstring> const& words, std::wstring_view delimiter)
    {
        std::wstring output;
        for (std::size_t index = 0; index < words.size(); ++index)
        {
            if (index != 0)
            {
                output.append(delimiter);
            }
            output.append(words[index]);
        }
        return output;
    }

    [[nodiscard]] std::wstring CamelCase(std::vector<std::wstring> const& words)
    {
        if (words.empty())
        {
            return {};
        }

        std::wstring output = words[0];
        for (std::size_t index = 1; index < words.size(); ++index)
        {
            output.append(Cap(words[index]));
        }
        return output;
    }

    [[nodiscard]] std::wstring PascalCase(std::vector<std::wstring> const& words)
    {
        std::wstring output;
        for (auto const& word : words)
        {
            output.append(Cap(word));
        }
        return output;
    }

    [[nodiscard]] std::wstring TitleCase(std::vector<std::wstring> const& words)
    {
        std::wstring output;
        for (std::size_t index = 0; index < words.size(); ++index)
        {
            if (index != 0)
            {
                output.push_back(L' ');
            }
            output.append(Cap(words[index]));
        }
        return output;
    }

    [[nodiscard]] std::wstring SentenceCase(std::vector<std::wstring> const& words)
    {
        if (words.empty())
        {
            return {};
        }

        std::wstring output = Cap(words[0]);
        for (std::size_t index = 1; index < words.size(); ++index)
        {
            output.push_back(L' ');
            output.append(words[index]);
        }
        return output;
    }

    [[nodiscard]] std::wstring TrainCase(std::vector<std::wstring> const& words)
    {
        std::wstring output;
        for (std::size_t index = 0; index < words.size(); ++index)
        {
            if (index != 0)
            {
                output.push_back(L'-');
            }
            output.append(Cap(words[index]));
        }
        return output;
    }

    [[nodiscard]] std::wstring ConstantCase(std::vector<std::wstring> const& words)
    {
        auto output = JoinWords(words, L"_");
        for (auto& character : output)
        {
            character = ToUpperInvariantChar(character);
        }
        return output;
    }
}

namespace winforge::core::caseconvert
{
    std::vector<std::wstring> Tokenize(std::wstring_view input)
    {
        std::vector<std::wstring> words;
        if (input.empty())
        {
            return words;
        }

        try
        {
            std::wstring current;
            current.reserve(input.size());
            wchar_t prev{};

            auto flush = [&]()
            {
                if (!current.empty())
                {
                    words.push_back(ToLowerInvariant(current));
                    current.clear();
                }
            };

            for (auto const c : input)
            {
                if (IsExplicitSeparator(c))
                {
                    flush();
                    prev = c;
                    continue;
                }

                if (!IsLetterOrDigit(c))
                {
                    flush();
                    prev = c;
                    continue;
                }

                if (!current.empty())
                {
                    auto const prevLower = IsLower(prev);
                    auto const prevDigit = IsDigit(prev);
                    auto const curUpper = IsUpper(c);
                    auto const curDigit = IsDigit(c);

                    if (curUpper && (prevLower || prevDigit))
                    {
                        flush();
                    }
                    else if (curDigit != prevDigit)
                    {
                        flush();
                    }
                }

                current.push_back(c);
                prev = c;
            }

            flush();
        }
        catch (...)
        {
            words.clear();
            std::wstring current;
            for (auto const c : input)
            {
                if (c == L' ' || c == L'\t' || c == L'\r' || c == L'\n')
                {
                    if (!current.empty())
                    {
                        words.push_back(ToLowerInvariant(current));
                        current.clear();
                    }
                }
                else
                {
                    current.push_back(c);
                }
            }
            if (!current.empty())
            {
                words.push_back(ToLowerInvariant(current));
            }
        }

        return words;
    }

    [[nodiscard]] std::vector<Form> AllForms(std::wstring_view input)
    {
        auto words = Tokenize(input);
        std::vector<Form> forms;
        forms.reserve(10);
        forms.push_back({ L"camelCase", L"駝峰式 camelCase", CamelCase(words) });
        forms.push_back({ L"PascalCase", L"帕斯卡式 PascalCase", PascalCase(words) });
        forms.push_back({ L"snake_case", L"蛇形 snake_case", JoinWords(words, L"_") });
        forms.push_back({ L"kebab-case", L"烤串 kebab-case", JoinWords(words, L"-") });
        forms.push_back({ L"CONSTANT_CASE", L"常數式 CONSTANT_CASE", ConstantCase(words) });
        forms.push_back({ L"Title Case", L"標題式 Title Case", TitleCase(words) });
        forms.push_back({ L"Sentence case", L"句子式 Sentence case", SentenceCase(words) });
        forms.push_back({ L"dot.case", L"點式 dot.case", JoinWords(words, L".") });
        forms.push_back({ L"path/case", L"路徑式 path/case", JoinWords(words, L"/") });
        forms.push_back({ L"Train-Case", L"火車式 Train-Case", TrainCase(words) });

        return forms;
    }
}
