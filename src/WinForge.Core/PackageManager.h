#pragma once

#include <cstdint>
#include <optional>
#include <span>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::packages
{
    struct PackageManagerDescriptor
    {
        std::wstring_view key;
        std::wstring_view name_en;
        std::wstring_view name_zh;
        std::wstring_view executable;
    };

    enum class PackageView : std::uint8_t
    {
        Discover = 0,
        Updates = 1,
        Installed = 2,
        Bundles = 3,
        Sources = 4,
        Ignored = 5,
        Setup = 6,
        Settings = 7,
        Operations = 8,
    };

    struct PackageViewDescriptor
    {
        PackageView view;
        std::wstring_view name_en;
        std::wstring_view name_zh;
    };

    enum class PackageAction : std::uint8_t
    {
        Probe,
        Search,
        Installed,
        Updates,
        Install,
        Update,
        Uninstall,
        Details,
        Sources,
    };

    struct PackageItem
    {
        std::wstring name;
        std::wstring id;
        std::wstring version;
        std::wstring available_version;
        std::wstring source;
        std::wstring manager_key;
    };

    // Mirrors the managed InstallOptions schema. Pre/post hooks remain represented for bundle
    // compatibility, but this argv-only layer deliberately rejects non-empty shell hooks.
    struct InstallOptions
    {
        std::wstring custom_args_install;
        std::wstring custom_args_update;
        std::wstring custom_args_uninstall;

        bool run_as_administrator{ false };
        bool interactive{ false };
        bool skip_hash_check{ false };
        bool pre_release{ false };
        bool remove_data_on_uninstall{ false };
        bool uninstall_previous_on_update{ false };
        bool skip_minor_updates{ false };
        bool auto_update{ false };

        std::wstring scope;
        std::wstring architecture;
        std::wstring version;
        std::wstring custom_install_location;

        std::wstring pre_install_command;
        std::wstring post_install_command;
        std::wstring pre_update_command;
        std::wstring post_update_command;
        std::wstring pre_uninstall_command;
        std::wstring post_uninstall_command;

        bool abort_on_pre_install_fail{ false };
        bool abort_on_pre_update_fail{ false };
        bool abort_on_pre_uninstall_fail{ false };

        std::vector<std::wstring> kill_before_operation;
        bool force_kill{ false };
    };

    enum class CommandTransport : std::uint8_t
    {
        Process,
        HttpGet,
        StaticText,
    };

    enum class CommandWorkingDirectory : std::uint8_t
    {
        Default,
        BunGlobalPackages,
    };

    enum class CommandPostProcess : std::uint8_t
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

    // Process commands always carry a discrete executable and argv vector. request_uri and
    // static_text are used only by the corresponding non-process transports.
    struct CommandSpec
    {
        CommandTransport transport{ CommandTransport::Process };
        std::wstring executable;
        std::vector<std::wstring> arguments;
        std::wstring request_uri;
        std::wstring static_text;
        std::wstring query;
        CommandWorkingDirectory working_directory{ CommandWorkingDirectory::Default };
        CommandPostProcess post_process{ CommandPostProcess::None };
        bool requires_elevation{ false };
    };

    struct ValidationResult
    {
        bool valid{ false };
        std::wstring code;

        [[nodiscard]] explicit operator bool() const noexcept { return valid; }
    };

    struct ArgumentParseResult
    {
        std::vector<std::wstring> arguments;
        std::wstring error_code;

        [[nodiscard]] explicit operator bool() const noexcept { return error_code.empty(); }
    };

    struct SourceResolution
    {
        std::wstring normalized_source;
        std::wstring package_id;
        std::vector<std::wstring> arguments;
    };

    struct SourceResolutionResult
    {
        std::optional<SourceResolution> resolution;
        std::wstring error_code;

        [[nodiscard]] explicit operator bool() const noexcept { return resolution.has_value(); }
    };

    struct CommandBuildResult
    {
        std::optional<CommandSpec> command;
        std::wstring error_code;

        [[nodiscard]] explicit operator bool() const noexcept { return command.has_value(); }
    };

    [[nodiscard]] std::span<PackageManagerDescriptor const> PackageManagers() noexcept;
    [[nodiscard]] PackageManagerDescriptor const* FindPackageManager(std::wstring_view key) noexcept;

    [[nodiscard]] std::span<PackageViewDescriptor const> PackageViews() noexcept;
    [[nodiscard]] std::optional<PackageView> PackageViewFromFragment(std::wstring_view fragment) noexcept;
    [[nodiscard]] std::optional<std::wstring_view> PackageViewFragment(PackageView view) noexcept;
    [[nodiscard]] std::optional<std::wstring> PackageNavigationKey(PackageView view);

    [[nodiscard]] ValidationResult ValidatePackageReference(
        std::wstring_view manager_key,
        std::wstring_view package_id) noexcept;
    [[nodiscard]] ValidationResult ValidateInstallOptions(InstallOptions const& options) noexcept;
    [[nodiscard]] ArgumentParseResult ParseCustomArguments(std::wstring_view value) noexcept;
    [[nodiscard]] SourceResolutionResult ResolvePackageSource(
        std::wstring_view manager_key,
        std::wstring_view package_id,
        std::wstring_view source,
        PackageAction action) noexcept;

    [[nodiscard]] CommandBuildResult BuildProbeCommand(std::wstring_view manager_key) noexcept;
    [[nodiscard]] CommandBuildResult BuildSearchCommand(
        std::wstring_view manager_key,
        std::wstring_view query) noexcept;
    [[nodiscard]] CommandBuildResult BuildInstalledCommand(std::wstring_view manager_key) noexcept;
    [[nodiscard]] CommandBuildResult BuildUpdatesCommand(std::wstring_view manager_key) noexcept;
    [[nodiscard]] CommandBuildResult BuildPackageActionCommand(
        std::wstring_view manager_key,
        std::wstring_view package_id,
        std::wstring_view source,
        PackageAction action,
        InstallOptions const& options = {}) noexcept;
    [[nodiscard]] CommandBuildResult BuildDetailsCommand(
        std::wstring_view manager_key,
        std::wstring_view package_id) noexcept;
    [[nodiscard]] CommandBuildResult BuildSourcesCommand(std::wstring_view manager_key) noexcept;
}
