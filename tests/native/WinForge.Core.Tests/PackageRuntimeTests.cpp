#include "PackageRuntime.h"
#include "PackageRuntimeTests.h"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <string_view>

namespace
{
    PackageRuntimeTestCounts counts;

    void Expect(bool condition, std::string_view name)
    {
        if (condition)
        {
            ++counts.passed;
            std::cout << "PASS package runtime: " << name << '\n';
        }
        else
        {
            ++counts.failed;
            std::cerr << "FAIL package runtime: " << name << '\n';
        }
    }
}

PackageRuntimeTestCounts RunPackageRuntimeTests()
{
    using namespace winforge::core::packages;
    counts = {};

    auto cmd = ResolvePackageExecutable(L"cmd");
    Expect(cmd && !cmd->executable.empty() && cmd->argument_prefix.empty(),
        "resolves a native PATH executable without a shell wrapper");

    auto powershell = ResolvePackageExecutable(L"powershell");
    Expect(powershell && powershell->resolved_path.find(L"WindowsPowerShell") != std::wstring::npos,
        "resolves Windows PowerShell through its fixed System32 location");
    Expect(!ResolvePackageExecutable(L"winforge-definitely-missing-command-4971"),
        "missing executables fail closed");

    auto temporary = std::filesystem::temp_directory_path() / L"WinForge-PackageRuntime-Resolution";
    std::filesystem::create_directories(temporary);
    auto batchPath = temporary / L"unsafe.cmd";
    auto batPath = temporary / L"unsafe.bat";
    auto shimPath = temporary / L"safe-shim.ps1";
    auto plantedPath = temporary / L"winforge-current-directory-plant.exe";
    {
        std::ofstream batch(batchPath);
        batch << "@echo off\n";
        std::ofstream bat(batPath);
        bat << "@echo off\n";
        std::ofstream shim(shimPath);
        shim << "Write-Output safe\n";
        std::ofstream planted(plantedPath);
        planted << "not an executable\n";
    }
    Expect(!ResolvePackageExecutable(batchPath.wstring()) &&
        !ResolvePackageExecutable(batPath.wstring()),
        "explicit cmd and bat scripts are rejected");
    auto shim = ResolvePackageExecutable(shimPath.wstring());
    Expect(shim && shim->resolved_path == std::filesystem::weakly_canonical(shimPath).wstring() &&
        std::find(shim->argument_prefix.begin(), shim->argument_prefix.end(), L"-File") != shim->argument_prefix.end(),
        "explicit PowerShell shims use the fixed interpreter and a discrete File argument");
    auto const originalDirectory = std::filesystem::current_path();
    std::filesystem::current_path(temporary);
    auto relative = ResolvePackageExecutable(L".\\winforge-current-directory-plant.exe");
    auto planted = ResolvePackageExecutable(L"winforge-current-directory-plant");
    std::filesystem::current_path(originalDirectory);
    Expect(!relative, "relative executable paths never consult the current directory");
    Expect(!planted, "bare executable lookup never searches the current directory");
    std::error_code ignored;
    std::filesystem::remove_all(temporary, ignored);

    Expect(PercentEncodeUtf8(L"hello world+好") == L"hello%20world%2B%E5%A5%BD",
        "HTTPS query encoding operates on UTF-8 bytes");

    Expect(detail::AcceptsParsedNonZeroExit(
        L"npm", PackageAction::Updates, CommandPostProcess::NpmJson, 1, 1),
        "npm updates accept exit code one after valid rows are parsed");
    Expect(!detail::AcceptsParsedNonZeroExit(
        L"npm", PackageAction::Updates, CommandPostProcess::NpmJson, 1, 0),
        "npm updates reject exit code one when no valid rows were parsed");
    Expect(!detail::AcceptsParsedNonZeroExit(
        L"npm", PackageAction::Installed, CommandPostProcess::NpmJson, 1, 1),
        "npm non-update actions keep exit code one as failure");
    Expect(!detail::AcceptsParsedNonZeroExit(
        L"npm", PackageAction::Updates, CommandPostProcess::JsonPackages, 1, 1),
        "non-npm update parsers keep exit code one as failure");
    Expect(!detail::AcceptsParsedNonZeroExit(
        L"pip", PackageAction::Updates, CommandPostProcess::NpmJson, 1, 1),
        "other package managers keep exit code one as failure");
    Expect(!detail::AcceptsParsedNonZeroExit(
        L"npm", PackageAction::Updates, CommandPostProcess::NpmJson, 2, 1),
        "npm updates keep all other nonzero exit codes as failure");

    auto staticResult = QueryPackageManager(L"cargo", PackageAction::Sources);
    Expect(staticResult.success && !staticResult.command_started &&
        staticResult.standard_output.empty() && staticResult.standard_error.empty() &&
        staticResult.diagnostic.find(L"withheld") != std::wstring::npos,
        "source metadata returns without process creation and crosses the runtime redacted");

    auto probe = ProbePackageManager(L"psgallery", PackageRuntimeOptions{ std::chrono::seconds(20) });
    Expect((probe.success && probe.command_started && probe.exit_code == std::uint32_t{ 0 }) ||
        (!probe.command_started && probe.diagnostic.find(L"elevated") != std::wstring::npos),
        "runtime executes a safe probe or fails closed at high integrity");

    auto invalidProbe = ProbePackageManager(L"unknown-manager");
    Expect(!invalidProbe.success && !invalidProbe.command_started && !invalidProbe.diagnostic.empty(),
        "invalid manager probes fail before process creation");

    PackageItem unsafePackage;
    unsafePackage.manager_key = L"winget";
    unsafePackage.id = L"safe & calc";
    auto unsafeMutation = RunPackageMutation(unsafePackage, PackageAction::Install);
    Expect(!unsafeMutation.success && !unsafeMutation.command_started &&
        unsafeMutation.diagnostic == L"invalid-package-id",
        "unsafe package mutations fail before process creation");

    std::stop_source cancelled;
    cancelled.request_stop();
    PackageRuntimeOptions cancelledOptions;
    cancelledOptions.cancellation_token = cancelled.get_token();
    auto cancelledResult = QueryPackageManager(
        L"cargo", PackageAction::Sources, {}, cancelledOptions);
    Expect(!cancelledResult.success && cancelledResult.cancelled && !cancelledResult.command_started,
        "pre-cancelled requests fail before any transport starts");

    PackageRuntimeOptions invalidTimeout;
    invalidTimeout.timeout = std::chrono::milliseconds::zero();
    auto timeoutResult = QueryPackageManager(
        L"cargo", PackageAction::Sources, {}, invalidTimeout);
    Expect(!timeoutResult.success && !timeoutResult.command_started &&
        timeoutResult.diagnostic.find(L"withheld") != std::wstring::npos,
        "non-positive source-query timeouts fail before transport and retain redaction");

    std::cout << counts.passed << " package-runtime tests passed, "
        << counts.failed << " failed\n";
    return counts;
}
