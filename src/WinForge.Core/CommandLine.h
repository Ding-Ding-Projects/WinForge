#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace winforge::core
{
    struct LaunchRequest
    {
        std::wstring route{ L"dashboard" };
        std::wstring argument{};
    };

    [[nodiscard]] std::wstring NormalizeRouteKey(std::wstring_view value);
    [[nodiscard]] LaunchRequest ParseLaunchRequest(std::vector<std::wstring> const& arguments);
    [[nodiscard]] LaunchRequest CurrentProcessLaunchRequest();
}
