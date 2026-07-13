#pragma once

#include "Localization.h"

#include <string>
#include <vector>

namespace winforge::core
{
    struct ModuleRecord
    {
        std::wstring id;
        std::wstring tag;
        std::wstring kind;
        LocalizedText name;
        std::wstring glyph;
        std::wstring keywords;
        std::vector<std::wstring> aliases;
    };
}
