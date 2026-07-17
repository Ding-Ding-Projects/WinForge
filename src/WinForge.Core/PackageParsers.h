#pragma once

#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::packages
{
    enum class PackageOutputKind
    {
        Search,
        Installed,
        Updates,
    };

    // Parser-owned transport record. CLI text is retained as UTF-8 so this standard-C++
    // layer stays independent from WinRT, the CLR, and any UI model.
    struct PackageRecord
    {
        std::string name;
        std::string id;
        std::string version;
        std::string availableVersion;
        std::string source;
        std::string managerKey;

        [[nodiscard]] bool IsValid() const noexcept
        {
            return !id.empty();
        }

        bool operator==(PackageRecord const&) const = default;
    };

    struct PackageDetailField
    {
        std::string label;
        std::string value;

        [[nodiscard]] bool IsValid() const noexcept
        {
            return !label.empty() && !value.empty();
        }

        bool operator==(PackageDetailField const&) const = default;
    };

    // Mirrors the native command planner's post-processing contract without depending on
    // the process-launch layer. Names intentionally match CommandPostProcess.
    enum class PackageParserKind
    {
        None,
        WingetTable,
        ScoopSearch,
        ScoopExport,
        ChocolateyDelimited,
        JsonPackages,
        NpmJson,
        DotnetTable,
        DotnetUpdatesFromNuGet,
        PowerShellPackagesJson,
        CargoSearch,
        CargoInstalled,
        CargoUpdates,
        BunInstalled,
        BunUpdates,
        VcpkgSearch,
        VcpkgInstalled,
        VcpkgUpdates,
        PyPiSearch,
        NpmRegistrySearch,
    };

    struct PackageParseResult
    {
        std::vector<PackageRecord> packages;
        bool supported{ true };
        bool requiresRuntimeResolution{ false };
        std::string diagnostic;
    };

    [[nodiscard]] std::vector<PackageRecord> ParseWingetTable(std::string_view output) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseScoopSearch(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseScoopInstalledJson(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseScoopList(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseScoopStatus(std::string_view output) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseChocolatey(
        std::string_view output,
        PackageOutputKind kind) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParsePipJson(
        std::string_view output,
        PackageOutputKind kind) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParsePyPiSearchJson(
        std::string_view output,
        std::string_view query) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseNpmJson(
        std::string_view output,
        PackageOutputKind kind) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseBunSearchJson(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseNpmRegistrySearchJson(
        std::string_view output,
        std::string_view managerKey) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseBunInstalled(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseBunOutdated(
        std::string_view output,
        bool preferLatest = true) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseDotnetToolTable(
        std::string_view output,
        PackageOutputKind kind) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParsePowerShellGalleryJson(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParsePowerShell7Json(std::string_view output) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseCargoSearch(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseCargoInstalled(std::string_view output) noexcept;
    [[nodiscard]] std::vector<PackageRecord> ParseCargoUpdates(std::string_view output) noexcept;

    [[nodiscard]] std::vector<PackageRecord> ParseVcpkg(
        std::string_view output,
        PackageOutputKind kind) noexcept;

    [[nodiscard]] PackageParseResult ParsePackageOutput(
        PackageParserKind parser,
        std::string_view output,
        PackageOutputKind outputKind = PackageOutputKind::Search,
        std::string_view managerKey = {},
        std::string_view query = {},
        bool preferLatest = true) noexcept;

    [[nodiscard]] std::vector<PackageDetailField> ParsePackageDetailsFields(
        std::string_view output) noexcept;
}
