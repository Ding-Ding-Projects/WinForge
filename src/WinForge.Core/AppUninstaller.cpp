#include "AppUninstaller.h"

#include <algorithm>
#include <array>
#include <cwctype>

namespace winforge::core::uninstall
{
    namespace
    {
        std::wstring_view Trim(std::wstring_view value) noexcept
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

        bool StartsWithInsensitive(std::wstring_view value, std::wstring_view prefix) noexcept
        {
            if (value.size() < prefix.size())
            {
                return false;
            }
            for (std::size_t index = 0; index < prefix.size(); ++index)
            {
                if (std::towlower(value[index]) != std::towlower(prefix[index]))
                {
                    return false;
                }
            }
            return true;
        }

        bool Contains(
            std::wstring_view value,
            std::wstring_view needle,
            bool case_sensitive) noexcept
        {
            if (needle.empty())
            {
                return true;
            }
            if (needle.size() > value.size())
            {
                return false;
            }
            for (std::size_t offset = 0; offset <= value.size() - needle.size(); ++offset)
            {
                bool matches = true;
                for (std::size_t index = 0; index < needle.size(); ++index)
                {
                    auto const left = case_sensitive
                        ? value[offset + index]
                        : static_cast<wchar_t>(std::towlower(value[offset + index]));
                    auto const right = case_sensitive
                        ? needle[index]
                        : static_cast<wchar_t>(std::towlower(needle[index]));
                    if (left != right)
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                {
                    return true;
                }
            }
            return false;
        }

        bool MatchesCacheFields(
            AppPackage const& package,
            std::wstring_view query,
            AppUninstallerFilterOptions const& options)
        {
            auto const display_name = AppPackageDisplayName(package);
            std::array<std::wstring_view, 5> const fields{
                display_name,
                package.name,
                package.publisher,
                package.package_family_name,
                package.package_full_name,
            };
            for (auto const field : fields)
            {
                if (options.expression)
                {
                    if (options.expression->Search(field).matched)
                    {
                        return true;
                    }
                }
                else if (Contains(field, query, options.case_sensitive))
                {
                    return true;
                }
            }
            return false;
        }

        bool DisplayLess(AppPackage const& left, AppPackage const& right) noexcept
        {
            auto const left_display = AppPackageDisplayName(left);
            auto const right_display = AppPackageDisplayName(right);
            auto const length = std::min(left_display.size(), right_display.size());
            for (std::size_t index = 0; index < length; ++index)
            {
                auto const left_character = std::towlower(left_display[index]);
                auto const right_character = std::towlower(right_display[index]);
                if (left_character != right_character)
                {
                    return left_character < right_character;
                }
            }
            return left_display.size() < right_display.size();
        }
    }

    std::wstring AppPackageShortName(std::wstring_view name)
    {
        auto const dot = name.find_last_of(L'.');
        if (dot == std::wstring_view::npos || dot + 1 >= name.size())
        {
            return std::wstring(name);
        }
        return std::wstring(name.substr(dot + 1));
    }

    std::wstring AppPackageDisplayName(AppPackage const& package)
    {
        auto const display = Trim(package.display_name);
        if (display.empty() || StartsWithInsensitive(display, L"ms-resource"))
        {
            return AppPackageShortName(package.name);
        }
        return std::wstring(display);
    }

    bool IsSafeAppPackageFamilyName(std::wstring_view value) noexcept
    {
        if (value.empty() || value.size() > 255 || value == L"." || value == L"..")
        {
            return false;
        }
        for (auto const character : value)
        {
            if (!(std::iswalnum(character) || character == L'.' ||
                character == L'_' || character == L'-'))
            {
                return false;
            }
        }
        return true;
    }

    std::optional<std::filesystem::path> AppPackageLocalDataPath(
        std::filesystem::path const& local_app_data,
        std::wstring_view package_family_name)
    {
        if (!local_app_data.is_absolute() || !IsSafeAppPackageFamilyName(package_family_name))
        {
            return std::nullopt;
        }

        auto const root = (local_app_data / L"Packages").lexically_normal();
        auto const candidate = (root / std::wstring(package_family_name)).lexically_normal();
        if (candidate.parent_path() != root)
        {
            return std::nullopt;
        }
        return candidate;
    }

    std::vector<AppPackage> FilterAppPackages(
        std::span<AppPackage const> packages,
        std::wstring_view query,
        AppUninstallerFilterOptions const& options)
    {
        query = Trim(query);
        std::vector<AppPackage> visible;
        visible.reserve(packages.size());
        for (auto const& package : packages)
        {
            if (query.empty() || MatchesCacheFields(package, query, options))
            {
                visible.push_back(package);
            }
        }
        std::stable_sort(visible.begin(), visible.end(), DisplayLess);
        return visible;
    }

    std::wstring FormatAppPackageSize(std::uint64_t bytes)
    {
        if (bytes == 0)
        {
            return L"Unknown";
        }

        constexpr std::array<std::wstring_view, 5> units{
            L"B", L"KB", L"MB", L"GB", L"TB",
        };
        long double size = static_cast<long double>(bytes);
        std::size_t unit = 0;
        while (size >= 1024.0L && unit + 1 < units.size())
        {
            size /= 1024.0L;
            ++unit;
        }

        auto const scaled = static_cast<std::uint64_t>(size * 10.0L + 0.5L);
        auto const whole = scaled / 10;
        auto const tenth = scaled % 10;
        auto text = std::to_wstring(whole);
        if (tenth != 0)
        {
            text.push_back(L'.');
            text += std::to_wstring(tenth);
        }
        text.push_back(L' ');
        text += units[unit];
        return text;
    }
}
