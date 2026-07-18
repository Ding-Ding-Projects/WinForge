#include "RouteIndex.h"

#include "CommandLine.h"

#include <array>
#include <stdexcept>

namespace winforge::core
{
    bool HasNativeRenderer(std::wstring_view canonicalRoute)
    {
        constexpr std::array<std::wstring_view, 18> implemented{
            L"dashboard",
            L"shell.allapps",
            L"about",
            L"module.packages",
            L"module.uninstall",
            L"module.checkdigit",
            L"module.binarytext",
            L"module.base32",
            L"module.caseconvert",
            L"module.guidgen",
            L"module.passgen",
            L"module.passwordstrength",
            L"module.uuidv7",
            L"module.romannum",
            L"module.unixperm",
            L"module.regextester",
            L"module.regexcheat",
            L"module.symbols",
        };
        auto const normalized = NormalizeRouteKey(canonicalRoute);
        for (auto const route : implemented)
        {
            if (normalized == route) return true;
        }
        return false;
    }

    void RouteIndex::Rebuild(std::vector<ModuleRecord> const& records)
    {
        m_canonical.clear();
        m_deepLinks.clear();

        auto addCanonical = [&](std::wstring_view value, std::size_t index)
        {
            auto const key = NormalizeRouteKey(value);
            if (key.empty())
            {
                return;
            }
            auto const [found, inserted] = m_canonical.emplace(key, index);
            if (!inserted && found->second != index)
            {
                throw std::invalid_argument("The native catalog contains a duplicate canonical route key.");
            }
        };

        for (std::size_t index = 0; index < records.size(); ++index)
        {
            addCanonical(records[index].id, index);
            addCanonical(records[index].tag, index);
        }

        // Managed ApplyStartPage gives these module aliases precedence over
        // identically named runtime categories. Populate categories first and
        // every non-category route second to preserve that launch contract.
        auto addDeepLinks = [&](bool categories)
        {
            for (std::size_t index = 0; index < records.size(); ++index)
            {
                auto const& record = records[index];
                if ((record.kind == L"category") != categories)
                {
                    continue;
                }
                for (auto const& alias : record.aliases)
                {
                    auto const key = NormalizeRouteKey(alias);
                    if (!key.empty())
                    {
                        m_deepLinks[key] = index;
                    }
                }
            }
        };
        addDeepLinks(true);
        addDeepLinks(false);
    }

    std::optional<std::size_t> RouteIndex::FindCanonicalOrAlias(std::wstring_view route) const
    {
        auto const key = NormalizeRouteKey(route);
        if (auto const canonical = m_canonical.find(key); canonical != m_canonical.end())
        {
            return canonical->second;
        }
        if (auto const alias = m_deepLinks.find(key); alias != m_deepLinks.end())
        {
            return alias->second;
        }
        return std::nullopt;
    }

    std::optional<std::size_t> RouteIndex::FindLaunch(std::wstring_view route) const
    {
        auto const key = NormalizeRouteKey(route);
        if (auto const alias = m_deepLinks.find(key); alias != m_deepLinks.end())
        {
            return alias->second;
        }
        if (auto const canonical = m_canonical.find(key); canonical != m_canonical.end())
        {
            return canonical->second;
        }
        return std::nullopt;
    }
}
