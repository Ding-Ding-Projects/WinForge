#pragma once

#include "RegexSearch.h"

#include <cstdint>
#include <filesystem>
#include <memory>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::uninstall
{
    // This is intentionally an inert cache record. Enumeration and package
    // removal remain in the C++/WinRT shell; the testable Core layer never
    // calls a package API, starts a process, or touches disk.
    struct AppPackage
    {
        std::wstring name;
        std::wstring package_full_name;
        std::wstring package_family_name;
        std::wstring display_name;
        std::wstring publisher;
        std::wstring version;
        std::wstring install_location;
    };

    struct AppUninstallerFilterOptions
    {
        bool case_sensitive{ false };
        std::shared_ptr<winforge::core::regex::SafeRegex const> expression{};
    };

    [[nodiscard]] std::wstring AppPackageShortName(std::wstring_view name);
    [[nodiscard]] std::wstring AppPackageDisplayName(AppPackage const& package);

    // A Package Family Name is used only as one path component below
    // %LocalAppData%\Packages. Keep this narrow so untrusted package metadata
    // can never turn a reviewed deep cleanup into a traversal.
    [[nodiscard]] bool IsSafeAppPackageFamilyName(std::wstring_view value) noexcept;
    [[nodiscard]] std::optional<std::filesystem::path> AppPackageLocalDataPath(
        std::filesystem::path const& local_app_data,
        std::wstring_view package_family_name);

    // Filters an already-enumerated local cache. When expression is present,
    // its bounded PCRE2 Search is applied only to cache fields; it is never
    // forwarded to PackageManager or a deletion API.
    [[nodiscard]] std::vector<AppPackage> FilterAppPackages(
        std::span<AppPackage const> packages,
        std::wstring_view query,
        AppUninstallerFilterOptions const& options = {});

    [[nodiscard]] std::wstring FormatAppPackageSize(std::uint64_t bytes);
}
