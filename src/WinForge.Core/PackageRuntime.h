#pragma once

#include "PackageManager.h"
#include "ProcessRunner.h"

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <stop_token>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::packages
{
    struct PackageRuntimeOptions
    {
        std::chrono::milliseconds timeout{ std::chrono::seconds(60) };
        std::stop_token cancellation_token{};
    };

    struct PackageRuntimeResult
    {
        bool success{ false };
        bool command_started{ false };
        bool parser_supported{ true };
        bool requires_runtime_resolution{ false };
        bool timed_out{ false };
        bool cancelled{ false };
        bool output_limit_exceeded{ false };
        std::optional<std::uint32_t> exit_code;
        std::vector<PackageItem> packages;
        std::wstring standard_output;
        std::wstring standard_error;
        std::wstring diagnostic;
    };

    struct ResolvedExecutable
    {
        std::wstring executable;
        std::vector<std::wstring> argument_prefix;
        std::wstring resolved_path;
    };

    namespace detail
    {
        // npm outdated uses exit code 1 when it successfully finds updates. Keep this
        // exception narrow and post-parse so arbitrary nonzero output never becomes success.
        [[nodiscard]] constexpr bool AcceptsParsedNonZeroExit(
            std::wstring_view manager_key,
            PackageAction action,
            CommandPostProcess post_process,
            std::uint32_t exit_code,
            std::size_t parsed_package_count) noexcept
        {
            return manager_key == L"npm" &&
                action == PackageAction::Updates &&
                post_process == CommandPostProcess::NpmJson &&
                exit_code == 1 &&
                parsed_package_count != 0;
        }
    }

    // Resolves a native executable from explicit absolute PATH entries. A PowerShell
    // .ps1 shim is allowed only through the fixed System32 powershell.exe -File path;
    // current-directory lookup and .cmd/.bat launch are rejected.
    [[nodiscard]] std::optional<ResolvedExecutable> ResolvePackageExecutable(
        std::wstring_view executable) noexcept;

    [[nodiscard]] std::wstring PercentEncodeUtf8(std::wstring_view value);

    [[nodiscard]] PackageRuntimeResult ProbePackageManager(
        std::wstring_view manager_key,
        PackageRuntimeOptions const& options = {});

    [[nodiscard]] PackageRuntimeResult QueryPackageManager(
        std::wstring_view manager_key,
        PackageAction action,
        std::wstring_view query = {},
        PackageRuntimeOptions const& options = {});

    [[nodiscard]] PackageRuntimeResult RunPackageMutation(
        PackageItem const& package,
        PackageAction action,
        InstallOptions const& install_options = {},
        PackageRuntimeOptions const& runtime_options = {});
}
