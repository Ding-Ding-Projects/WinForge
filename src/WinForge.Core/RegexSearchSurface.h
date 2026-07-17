#pragma once

#include <array>
#include <cstdint>
#include <span>
#include <string_view>

// The native shell deliberately keeps its search surfaces in one small
// contract.  A new C++/WinRT Search, Filter, or Find control is not complete
// until it has a descriptor here and routes through the bounded PCRE2 layer.
namespace winforge::core::regex
{
    enum class RegexSearchSurfaceId : std::uint8_t
    {
        ShellCatalog = 0,
        AllApps,
        PackageDiscoverCachedResults,
        RegexCheatsheetEntries,
    };

    enum class RegexSearchQueryPolicy : std::uint8_t
    {
        // The source is local generated catalog metadata. No process or
        // network operation exists behind this filter.
        LocalCatalog,

        // The source is an already-returned package result cache. The raw
        // pattern must never enter argv, HTTP(S), or a package runtime query.
        LocalCachedResultsOnly,
    };

    enum class RegexInvalidPatternPolicy : std::uint8_t
    {
        // A live filter retains the last valid visible list while the user
        // corrects the pattern.
        KeepPriorVisibleResults,

        // The builder must not navigate or apply target state until its
        // pattern compiles under the interactive safety budget.
        BlockTargetApplication,
    };

    struct RegexSearchSurface
    {
        RegexSearchSurfaceId id;
        std::wstring_view target_name_en;
        std::wstring_view target_name_zh;
        std::wstring_view route;
        std::wstring_view search_automation_id;
        std::wstring_view regex_mode_automation_id;
        std::wstring_view builder_automation_id;
        std::wstring_view searched_fields;
        RegexSearchQueryPolicy query_policy;
        RegexInvalidPatternPolicy invalid_pattern_policy;
    };

    inline constexpr std::array<RegexSearchSurface, 4> kRegexSearchSurfaces{
        RegexSearchSurface{
            RegexSearchSurfaceId::ShellCatalog,
            L"Native catalog search",
            L"原生目錄搜尋",
            L"search",
            L"NativeShellSearchBox",
            L"NativeShellRegexMode",
            L"NativeShellRegexBuilder",
            L"route id, tag, English and Cantonese labels, keywords, aliases",
            RegexSearchQueryPolicy::LocalCatalog,
            RegexInvalidPatternPolicy::BlockTargetApplication },
        RegexSearchSurface{
            RegexSearchSurfaceId::AllApps,
            L"All Apps local filter",
            L"所有 app 本機篩選",
            L"shell.allapps",
            L"NativeAllAppsSearchBox",
            L"NativeAllAppsRegexMode",
            L"NativeAllAppsRegexBuilder",
            L"route id, tag, English and Cantonese labels, keywords, aliases",
            RegexSearchQueryPolicy::LocalCatalog,
            RegexInvalidPatternPolicy::KeepPriorVisibleResults },
        RegexSearchSurface{
            RegexSearchSurfaceId::PackageDiscoverCachedResults,
            L"Package Discover cached results",
            L"套件 Discover 已快取結果",
            L"module.packages",
            L"NativePackageSearchBox",
            L"NativePackageRegexMode",
            L"NativePackageRegexBuilder",
            L"cached package name and id from the Discover result list",
            RegexSearchQueryPolicy::LocalCachedResultsOnly,
            RegexInvalidPatternPolicy::KeepPriorVisibleResults },
        RegexSearchSurface{
            RegexSearchSurfaceId::RegexCheatsheetEntries,
            L"Regex Cheatsheet reference filter",
            L"Regex Cheatsheet 本機參考篩選",
            L"module.regexcheat",
            L"NativeRegexCheatSearchBox",
            L"NativeRegexCheatRegexMode",
            L"NativeRegexCheatRegexBuilder",
            L"token, English and Cantonese descriptions, example, and category from the static local reference catalog",
            RegexSearchQueryPolicy::LocalCatalog,
            RegexInvalidPatternPolicy::KeepPriorVisibleResults },
    };

    [[nodiscard]] constexpr std::span<RegexSearchSurface const> RegexSearchSurfaces() noexcept
    {
        return kRegexSearchSurfaces;
    }

    [[nodiscard]] constexpr RegexSearchSurface const& RegexSearchSurfaceFor(
        RegexSearchSurfaceId id) noexcept
    {
        for (auto const& surface : kRegexSearchSurfaces)
        {
            if (surface.id == id)
            {
                return surface;
            }
        }
        // The enum is private to this fixed catalog. Returning the first
        // descriptor keeps this noexcept helper total for defensive callers;
        // core regression tests pin every supported enum value.
        return kRegexSearchSurfaces.front();
    }
}
