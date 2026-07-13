#pragma once

#include <string>
#include <string_view>

namespace winforge::core
{
    enum class LanguageMode
    {
        Bilingual,
        Cantonese,
        English,
    };

    struct LocalizedText
    {
        std::wstring en;
        std::wstring zh;

        [[nodiscard]] std::wstring Pick(LanguageMode mode) const
        {
            switch (mode)
            {
            case LanguageMode::Cantonese:
                return zh.empty() ? en : zh;
            case LanguageMode::English:
                return en.empty() ? zh : en;
            case LanguageMode::Bilingual:
            default:
                if (en.empty()) return zh;
                if (zh.empty() || en == zh) return en;
                return en + L" · " + zh;
            }
        }
    };

    [[nodiscard]] inline std::wstring Bilingual(std::wstring_view en, std::wstring_view zh)
    {
        return LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(LanguageMode::Bilingual);
    }
}
