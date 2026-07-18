#pragma once

#include "ModuleRecord.h"

#include <cstddef>
#include <optional>
#include <string>
#include <string_view>
#include <unordered_map>
#include <vector>

namespace winforge::core
{
    // Central evidence-gated list of fixed routes with a dedicated native
    // renderer. A route resolving through the catalog is not enough.
    [[nodiscard]] bool HasNativeRenderer(std::wstring_view canonicalRoute);

    class RouteIndex
    {
    public:
        void Rebuild(std::vector<ModuleRecord> const& records);

        [[nodiscard]] std::optional<std::size_t> FindCanonicalOrAlias(std::wstring_view route) const;
        [[nodiscard]] std::optional<std::size_t> FindLaunch(std::wstring_view route) const;

    private:
        std::unordered_map<std::wstring, std::size_t> m_canonical;
        std::unordered_map<std::wstring, std::size_t> m_deepLinks;
    };
}
