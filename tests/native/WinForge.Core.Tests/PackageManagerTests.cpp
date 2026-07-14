#include "PackageManagerTests.h"

#include "CommandLine.h"
#include "PackageManager.h"

#include <algorithm>
#include <array>
#include <iostream>
#include <string_view>
#include <vector>

namespace
{
    using namespace winforge::core::packages;
    using winforge::core::ParseLaunchRequest;

    bool Contains(std::vector<std::wstring> const& values, std::wstring_view expected)
    {
        return std::find(values.begin(), values.end(), expected) != values.end();
    }

    bool ContainsSequence(
        std::vector<std::wstring> const& values,
        std::initializer_list<std::wstring_view> expected)
    {
        if (expected.size() > values.size())
        {
            return false;
        }
        for (std::size_t start = 0; start + expected.size() <= values.size(); ++start)
        {
            bool matches = true;
            std::size_t offset = 0;
            for (auto const token : expected)
            {
                if (values[start + offset] != token)
                {
                    matches = false;
                    break;
                }
                ++offset;
            }
            if (matches)
            {
                return true;
            }
        }
        return false;
    }

    bool IsEncodedPowerShellValue(std::wstring_view value)
    {
        return value.size() >= 2
            && value.rfind(L"WF", 0) == 0
            && std::all_of(value.begin() + 2, value.end(), [](wchar_t character)
            {
                return (character >= L'a' && character <= L'z')
                    || (character >= L'A' && character <= L'Z')
                    || (character >= L'0' && character <= L'9')
                    || character == L'-'
                    || character == L'_';
            });
    }
}

NativeTestCounts RunPackageManagerTests()
{
    using namespace winforge::core::packages;

    NativeTestCounts counts;
    auto expect = [&](bool condition, std::string_view name)
    {
        if (condition)
        {
            ++counts.passed;
            std::cout << "PASS " << name << '\n';
        }
        else
        {
            ++counts.failed;
            std::cerr << "FAIL " << name << '\n';
        }
    };

    auto const managers = PackageManagers();
    constexpr std::array<std::wstring_view, 11> manager_order{
        L"winget", L"scoop", L"choco", L"pip", L"npm", L"dotnet",
        L"psgallery", L"pwsh7", L"cargo", L"bun", L"vcpkg",
    };
    expect(managers.size() == manager_order.size(), "package registry has eleven managers");
    expect(std::equal(managers.begin(), managers.end(), manager_order.begin(), manager_order.end(),
        [](PackageManagerDescriptor const& manager, std::wstring_view key)
        {
            return manager.key == key;
        }), "package registry preserves managed display order");
    expect(managers.front().name_en == L"Windows Package Manager"
        && managers.front().name_zh == L"Windows 套件管理員", "registry carries bilingual manager names");
    expect(FindPackageManager(L" PWSH7 ")->executable == L"pwsh", "manager lookup is normalized");
    expect(FindPackageManager(L"missing") == nullptr, "unknown manager lookup fails closed");

    auto const views = PackageViews();
    expect(views.size() == 9 && views[8].view == PackageView::Operations, "all nine package views are registered");
    expect(PackageViewFromFragment(L" UPDATES ") == PackageView::Updates, "supported fragment parsing is case insensitive");
    expect(PackageNavigationKey(PackageView::Installed) == L"module.packages#installed", "deep link builds managed navigation key");
    expect(PackageViewFromFragment(L"bundles") == PackageView::Bundles
        && PackageViewFromFragment(L"operations") == PackageView::Operations
        && PackageViewFragment(PackageView::Operations) == L"operations", "extended package view fragments stay round-trippable");
    auto launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"package-bundles" });
    expect(launch.route == L"module.packages" && launch.argument == L"#bundles", "bundle launch alias deep-links the bundle view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"packages-bundles" });
    expect(launch.route == L"module.packages" && launch.argument == L"#bundles", "plural bundle launch alias deep-links the bundle view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page=package-settings" });
    expect(launch.route == L"module.packages" && launch.argument == L"#settings", "settings launch alias deep-links the settings view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page=packages-settings" });
    expect(launch.route == L"module.packages" && launch.argument == L"#settings", "plural settings launch alias deep-links the settings view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"packages-operations" });
    expect(launch.route == L"module.packages" && launch.argument == L"#operations", "plural operations launch alias deep-links the operations view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"package-sources" });
    expect(launch.route == L"module.packages" && launch.argument == L"#sources", "sources launch alias deep-links the sources view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"packages-sources" });
    expect(launch.route == L"module.packages" && launch.argument == L"#sources", "plural sources launch alias deep-links the sources view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"package-ignored" });
    expect(launch.route == L"module.packages" && launch.argument == L"#ignored", "ignored launch alias deep-links the ignored view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"packages-ignored" });
    expect(launch.route == L"module.packages" && launch.argument == L"#ignored", "plural ignored launch alias deep-links the ignored view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"package-setup" });
    expect(launch.route == L"module.packages" && launch.argument == L"#setup", "setup launch alias deep-links the setup view");
    launch = ParseLaunchRequest(std::vector<std::wstring>{ L"WinForge.exe", L"--page", L"packages-setup" });
    expect(launch.route == L"module.packages" && launch.argument == L"#setup", "plural setup launch alias deep-links the setup view");

    std::vector<PackageItem> sort_items{
        { L"Zulu", L"b.id", L"2.0", L"", L"beta", L"winget" },
        { L"Beta", L"a.id", L"1.0", L"", L"gamma", L"npm" },
        { L"Alpha", L"c.id", L"3.0", L"", L"alpha", L"scoop" },
    };
    SortPackageItems(sort_items, PackageSortMode::Name);
    expect(sort_items.front().name == L"Alpha" && sort_items.back().name == L"Zulu", "package sorting by name is applied");
    SortPackageItems(sort_items, PackageSortMode::Source);
    expect(sort_items.front().source == L"alpha" && sort_items.back().source == L"gamma", "package sorting by source is applied");
    SortPackageItems(sort_items, PackageSortMode::Id);
    expect(sort_items.front().id == L"a.id" && sort_items.back().id == L"c.id", "package sorting by id is applied");
    SortPackageItems(sort_items, PackageSortMode::Manager);
    expect(sort_items.front().manager_key == L"npm" && sort_items.back().manager_key == L"winget", "package sorting by manager is applied");

    std::array<std::pair<std::wstring_view, std::wstring_view>, 11> const valid_references{
        std::pair{ L"winget", L"Microsoft.PowerToys" },
        std::pair{ L"scoop", L"extras/7zip" },
        std::pair{ L"choco", L"7zip" },
        std::pair{ L"pip", L"typing_extensions" },
        std::pair{ L"npm", L"@scope/tool" },
        std::pair{ L"dotnet", L"dotnet-ef" },
        std::pair{ L"psgallery", L"Az.Accounts" },
        std::pair{ L"pwsh7", L"Pester" },
        std::pair{ L"cargo", L"cargo-edit" },
        std::pair{ L"bun", L"@scope/tool" },
        std::pair{ L"vcpkg", L"zlib[core,tools]:x64-windows" },
    };
    expect(std::all_of(valid_references.begin(), valid_references.end(), [](auto const& reference)
        {
            return static_cast<bool>(ValidatePackageReference(reference.first, reference.second));
        }), "strict package ids accept every manager's valid grammar");
    expect(!ValidatePackageReference(L"winget", L"safe & calc"), "package id rejects command separators");
    expect(!ValidatePackageReference(L"npm", L"@scope/one/two"), "npm id rejects malformed scopes");
    expect(!ValidatePackageReference(L"vcpkg", L"zlib[core,,tools]"), "vcpkg id rejects malformed feature lists");
    expect(ValidatePackageReference(L"missing", L"safe").code == L"invalid-manager", "package validation reports unknown managers");

    auto arguments = ParseCustomArguments(L"--channel \"preview build\" --force");
    expect(arguments && arguments.arguments.size() == 3
        && arguments.arguments[1] == L"preview build", "custom arguments tokenize quoted values into argv");
    expect(!ParseCustomArguments(L"--flag & calc"), "custom arguments reject shell operators");
    expect(ParseCustomArguments(L"--source evil").error_code == L"custom-arguments-source-override", "custom arguments cannot bypass source policy");
    expect(!ParseCustomArguments(L"--flag \"unterminated"), "custom arguments reject unterminated quotes");

    InstallOptions valid_options;
    valid_options.version = L"1.2.3-preview.1";
    valid_options.scope = L"user";
    valid_options.architecture = L"x64";
    valid_options.custom_install_location = LR"(C:\Program Files\Example)";
    valid_options.custom_args_install = L"--accept-license \"named value\"";
    expect(static_cast<bool>(ValidateInstallOptions(valid_options)), "structured install options validate");
    auto bad_options = valid_options;
    bad_options.version = L"1.2.3 & calc";
    expect(ValidateInstallOptions(bad_options).code == L"invalid-version", "version validation rejects syntax");
    bad_options = valid_options;
    bad_options.scope = L"system";
    expect(ValidateInstallOptions(bad_options).code == L"invalid-scope", "scope validation uses managed allow list");
    bad_options = valid_options;
    bad_options.custom_install_location = L"C:\\Apps%TEMP%";
    expect(ValidateInstallOptions(bad_options).code == L"invalid-install-location", "install path rejects command expansion");
    bad_options = valid_options;
    bad_options.pre_install_command = L"Write-Host unsafe";
    expect(ValidateInstallOptions(bad_options).code == L"shell-hooks-unsupported", "argv layer rejects shell hooks");
    bad_options = valid_options;
    bad_options.kill_before_operation = { L"example.exe" };
    expect(ValidateInstallOptions(bad_options).code == L"process-kill-orchestration-unsupported",
        "argv layer rejects unimplemented process-kill orchestration");

    auto source = ResolvePackageSource(L"winget", L"Example.Tool", L"MSStore", PackageAction::Install);
    expect(source && source.resolution->normalized_source == L"msstore"
        && ContainsSequence(source.resolution->arguments, { L"--source", L"MSStore" }), "winget source becomes validated argv");
    source = ResolvePackageSource(L"scoop", L"7zip", L"extras", PackageAction::Install);
    expect(source && source.resolution->package_id == L"extras/7zip", "scoop source qualifies install id");
    source = ResolvePackageSource(L"scoop", L"main/7zip", L"extras", PackageAction::Install);
    expect(!source && source.error_code == L"source-id-mismatch", "scoop rejects conflicting bucket and id");
    source = ResolvePackageSource(L"pip", L"requests", L"pypi.org", PackageAction::Uninstall);
    expect(source && source.resolution->arguments.empty(), "uninstall retains source identity without selector");
    source = ResolvePackageSource(L"vcpkg", L"zlib:x64-windows", L"x64-windows", PackageAction::Update);
    expect(source && source.resolution->normalized_source == L"x64-windows", "vcpkg triplet metadata matches id");
    expect(ResolvePackageSource(L"winget", L"Example.Tool", L"Local PC", PackageAction::Install).error_code
        == L"source-local-or-unknown", "local source is explicitly incompatible");
    expect(!ResolvePackageSource(L"winget", L"Example.Tool", L"safe & calc", PackageAction::Install), "source rejects shell syntax");

    auto probe = BuildProbeCommand(L"vcpkg");
    expect(probe && probe.command->executable == L"vcpkg"
        && probe.command->arguments == std::vector<std::wstring>{ L"version" }, "probe command uses manager-specific argv");
    auto search = BuildSearchCommand(L"winget", L"terminal; calc");
    expect(search && search.command->arguments[2] == L"terminal; calc"
        && search.command->arguments.size() == 5, "search query remains one inert argv value");
    search = BuildSearchCommand(L"psgallery", L"Az.Accounts");
    expect(search && search.command->arguments[3].find(L"Az.Accounts") == std::wstring::npos
        && search.command->arguments.back() != L"Az.Accounts"
        && IsEncodedPowerShellValue(search.command->arguments.back()), "PowerShell query crosses the command parser as safe encoded argv");
    expect(BuildSearchCommand(L"pip", L"request").command->transport == CommandTransport::HttpGet, "pip search preserves managed HTTP backend");
    expect(BuildSearchCommand(L"bun", L"typescript").command->post_process == CommandPostProcess::NpmRegistrySearch, "Bun search preserves npm registry backend");

    auto installed = BuildInstalledCommand(L"bun");
    expect(installed && installed.command->working_directory == CommandWorkingDirectory::BunGlobalPackages
        && installed.command->arguments == std::vector<std::wstring>{ L"pm", L"ls" }, "Bun installed command uses global manifest directory");
    auto updates = BuildUpdatesCommand(L"dotnet");
    expect(updates && updates.command->post_process == CommandPostProcess::DotnetUpdatesFromNuGet, ".NET updates retain NuGet comparison stage");

    InstallOptions operation_options;
    operation_options.version = L"1.2.3";
    operation_options.scope = L"user";
    operation_options.architecture = L"x64";
    operation_options.custom_install_location = LR"(C:\Apps\Example)";
    operation_options.skip_hash_check = true;
    operation_options.custom_args_install = L"--accept-license \"named value\"";
    auto operation = BuildPackageActionCommand(
        L"winget", L"Example.Tool", L"msstore", PackageAction::Install, operation_options);
    expect(operation && operation.command->executable == L"winget"
        && ContainsSequence(operation.command->arguments, { L"--id", L"Example.Tool" })
        && ContainsSequence(operation.command->arguments, { L"--source", L"msstore" })
        && operation.command->arguments.back() == L"named value", "winget install maps options and source to discrete argv");
    expect(operation
        && FormatCommandPreview(*operation.command).find(L"winget install --id Example.Tool") == 0
        && FormatCommandPreview(*operation.command).find(L"\"named value\"") != std::wstring::npos,
        "operation previews expose the exact executable and quoted argv without executing");
    expect(!BuildPackageActionCommand(L"winget", L"safe & calc", L"", PackageAction::Install), "operation builder validates package id before building");
    operation_options.custom_args_install = L"--registry https://evil.test";
    expect(BuildPackageActionCommand(L"npm", L"safe-tool", L"npmjs.org", PackageAction::Install, operation_options).error_code
        == L"custom-arguments-source-override", "operation custom args cannot override trusted registry");

    InstallOptions dotnet_options;
    dotnet_options.custom_install_location = LR"(C:\Tools)";
    operation = BuildPackageActionCommand(L"dotnet", L"dotnet-ef", L"nuget.org", PackageAction::Install, dotnet_options);
    expect(operation && ContainsSequence(operation.command->arguments, { L"--tool-path", LR"(C:\Tools)" })
        && !Contains(operation.command->arguments, L"--global"), ".NET custom tool path excludes global flag");

    InstallOptions ps_options;
    ps_options.version = L"2.0.0";
    operation = BuildPackageActionCommand(L"psgallery", L"Az.Accounts", L"PSGallery", PackageAction::Install, ps_options);
    expect(operation && operation.command->executable == L"powershell"
        && operation.command->arguments[3].find(L"Az.Accounts") == std::wstring::npos
        && !Contains(operation.command->arguments, L"Az.Accounts")
        && std::all_of(operation.command->arguments.begin() + 4, operation.command->arguments.end(), IsEncodedPowerShellValue), "PowerShell package values never enter script syntax");

    constexpr std::array<PackageAction, 3> operations{
        PackageAction::Install, PackageAction::Update, PackageAction::Uninstall,
    };
    expect(std::all_of(valid_references.begin(), valid_references.end(), [&](auto const& reference)
        {
            return std::all_of(operations.begin(), operations.end(), [&](PackageAction action)
            {
                return static_cast<bool>(BuildPackageActionCommand(
                    reference.first, reference.second, L"", action));
            });
        }), "all eleven managers build install update and uninstall commands");

    auto details = BuildDetailsCommand(L"pwsh7", L"Pester");
    expect(details && details.command->arguments[3].find(L"Pester") == std::wstring::npos
        && IsEncodedPowerShellValue(details.command->arguments.back()), "details encode package id outside PowerShell script");
    expect(details
        && FormatCommandPreview(*details.command).find(L"pwsh -NoProfile -NonInteractive -Command") == 0
        && FormatCommandPreview(*details.command).find(L"Pester") == std::wstring::npos,
        "details previews expose the exact executable and argv while preserving encoded package values");
    auto sources = BuildSourcesCommand(L"cargo");
    expect(sources && sources.command->transport == CommandTransport::StaticText
        && sources.command->static_text == L"crates.io (default registry)", "Cargo sources preserve managed static result");

    expect(std::all_of(manager_order.begin(), manager_order.end(), [](std::wstring_view key)
        {
            return BuildProbeCommand(key)
                && BuildSearchCommand(key, L"sample")
                && BuildInstalledCommand(key)
                && BuildUpdatesCommand(key)
                && BuildDetailsCommand(key, key == L"npm" || key == L"bun" ? L"@scope/sample" : L"sample")
                && BuildSourcesCommand(key);
        }), "all eleven managers build every read-only command family");

    std::cout << counts.passed << " package-manager tests passed, "
        << counts.failed << " failed\n";
    return counts;
}
