#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <string_view>

namespace winforge::core::guidgen
{
    struct GuidInfo
    {
        std::wstring hex;
        int version{ 0 };
        std::wstring variant;
    };

    [[nodiscard]] std::wstring NewGuid(std::wstring_view format = L"D", bool upper = false);
    [[nodiscard]] std::wstring BulkGuids(int count, std::wstring_view format = L"D", bool upper = false);
    [[nodiscard]] std::wstring NewUlid();
    [[nodiscard]] std::wstring NewNanoId(int length);
    [[nodiscard]] GuidInfo Inspect(std::wstring_view text);
}
