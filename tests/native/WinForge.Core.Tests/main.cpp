#include "BinaryTextTests.h"
#include "CaseConvertTests.h"
#include "CommandLine.h"
#include "CheckDigitTests.h"
#include "Localization.h"
#include "PackageManagerTests.h"
#include "PackageRuntime.h"
#include "PackageRuntimeTests.h"
#include "ProcessRunnerTests.h"
#include "RouteIndex.h"

#include <chrono>
#include <iostream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

int RunPackageParserTests();

namespace
{
    int passed = 0;
    int failed = 0;

    void Expect(bool condition, std::string_view name)
    {
        if (condition)
        {
            ++passed;
            std::cout << "PASS " << name << '\n';
        }
        else
        {
            ++failed;
            std::cerr << "FAIL " << name << '\n';
        }
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (auto const helperResult = winforge::tests::TryRunProcessRunnerHelper(argc, argv);
        helperResult >= 0)
    {
        return helperResult;
    }

    if (argc == 3 && std::wstring_view(argv[1]) == L"--package-probe")
    {
        winforge::core::packages::PackageRuntimeOptions options;
        options.timeout = std::chrono::seconds(20);
        auto const started = std::chrono::steady_clock::now();
        auto const result = winforge::core::packages::ProbePackageManager(argv[2], options);
        auto const elapsed = std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::steady_clock::now() - started);
        std::wcout << L"manager=" << argv[2]
            << L" success=" << result.success
            << L" started=" << result.command_started
            << L" timeout=" << result.timed_out
            << L" cancelled=" << result.cancelled
            << L" elapsed_ms=" << elapsed.count()
            << L" diagnostic=" << result.diagnostic << L'\n';
        return result.success ? 0 : 2;
    }

    using winforge::core::LanguageMode;
    using winforge::core::LocalizedText;
    using winforge::core::NormalizeRouteKey;
    using winforge::core::ParseLaunchRequest;

    Expect(NormalizeRouteKey(L"  Module.Packages  ") == L"module.packages", "normalizes route keys");

    auto request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"packages" });
    Expect(request.route == L"packages" && request.argument.empty(), "parses --page value");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page=module.packages#updates" });
    Expect(request.route == L"module.packages" && request.argument == L"#updates", "preserves module fragments");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"package-discover" });
    Expect(request.route == L"module.packages" && request.argument == L"#discover", "package-discover selects Discover");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"package-updates" });
    Expect(request.route == L"module.packages" && request.argument == L"#updates", "package-updates selects Updates");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"package-installed" });
    Expect(request.route == L"module.packages" && request.argument == L"#installed", "package-installed selects Installed");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page=packages-updates" });
    Expect(request.route == L"module.packages" && request.argument == L"#updates", "plural package view aliases preserve managed routing");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"weblogin?url=https://example.test/path" });
    Expect(request.route == L"weblogin" && request.argument == L"?url=https://example.test/path", "preserves web-login query");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"search:package manager" });
    Expect(request.route == L"search" && request.argument == L"package manager", "parses dynamic search route");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"manual:reactor-safety" });
    Expect(request.route == L"manual" && request.argument == L"reactor-safety", "parses manual fragment route");

    request = ParseLaunchRequest({ L"WinForge.exe", L"--reactor" });
    Expect(request.route == L"reactor", "preserves legacy reactor flag");

    request = ParseLaunchRequest({ L"WinForge.exe" });
    Expect(request.route == L"dashboard", "defaults to dashboard");

    LocalizedText text{ L"Settings", L"設定" };
    Expect(text.Pick(LanguageMode::Bilingual) == L"Settings · 設定", "renders bilingual text");
    Expect(text.Pick(LanguageMode::Cantonese) == L"設定", "renders Cantonese text");
    Expect(text.Pick(LanguageMode::English) == L"Settings", "renders English text");

    auto makeRoute = [](std::wstring id, std::wstring kind, std::vector<std::wstring> aliases)
    {
        winforge::core::ModuleRecord record;
        record.id = std::move(id);
        record.tag = record.id;
        record.kind = std::move(kind);
        record.aliases = std::move(aliases);
        return record;
    };
    std::vector<winforge::core::ModuleRecord> routes{
        makeRoute(L"apps", L"category", { L"apps" }),
        makeRoute(L"module.taskbar-tweaker", L"module", { L"taskbar" }),
        makeRoute(L"launcher", L"category", { L"launcher" }),
        makeRoute(L"module.vault-volumes", L"module", { L"vault" }),
        makeRoute(L"module.uninstall", L"module", { L"apps" }),
        makeRoute(L"taskbar", L"category", { L"taskbar" }),
        makeRoute(L"module.cmdpalette", L"module", { L"launcher" }),
        makeRoute(L"vault", L"category", { L"vault" })
    };
    winforge::core::RouteIndex routeIndex;
    routeIndex.Rebuild(routes);
    auto resolvesTo = [&](std::optional<std::size_t> index, std::wstring_view id)
    {
        return index && routes[*index].id == id;
    };
    Expect(resolvesTo(routeIndex.FindCanonicalOrAlias(L"apps"), L"apps"), "in-app apps resolves to category");
    Expect(resolvesTo(routeIndex.FindCanonicalOrAlias(L"launcher"), L"launcher"), "in-app launcher resolves to category");
    Expect(resolvesTo(routeIndex.FindCanonicalOrAlias(L"taskbar"), L"taskbar"), "in-app taskbar resolves to category");
    Expect(resolvesTo(routeIndex.FindCanonicalOrAlias(L"vault"), L"vault"), "in-app vault resolves to category");
    Expect(resolvesTo(routeIndex.FindLaunch(L"apps"), L"module.uninstall"), "deep-link apps preserves managed target");
    Expect(resolvesTo(routeIndex.FindLaunch(L"launcher"), L"module.cmdpalette"), "deep-link launcher preserves managed target");
    Expect(resolvesTo(routeIndex.FindLaunch(L"taskbar"), L"module.taskbar-tweaker"), "deep-link taskbar preserves managed target");
    Expect(resolvesTo(routeIndex.FindLaunch(L"vault"), L"module.vault-volumes"), "deep-link vault preserves managed target");
    Expect(!routeIndex.FindLaunch(L"missing-route"), "unknown route stays unresolved");

    bool rejectedDuplicate = false;
    try
    {
        routeIndex.Rebuild({
            makeRoute(L"duplicate", L"module", { L"first" }),
            makeRoute(L"duplicate", L"module", { L"second" })
        });
    }
    catch (std::invalid_argument const&)
    {
        rejectedDuplicate = true;
    }
    Expect(rejectedDuplicate, "rejects duplicate canonical route keys");

    winforge::tests::RunProcessRunnerTests(Expect);

    auto const binaryTextCounts = RunBinaryTextTests();
    passed += binaryTextCounts.passed;
    failed += binaryTextCounts.failed;

    auto const caseConvertCounts = RunCaseConvertTests();
    passed += caseConvertCounts.passed;
    failed += caseConvertCounts.failed;

    auto const checkDigitCounts = RunCheckDigitTests();
    passed += checkDigitCounts.passed;
    failed += checkDigitCounts.failed;

    auto const package_manager_counts = RunPackageManagerTests();
    passed += package_manager_counts.passed;
    failed += package_manager_counts.failed;

    auto const package_runtime_counts = RunPackageRuntimeTests();
    passed += package_runtime_counts.passed;
    failed += package_runtime_counts.failed;

    auto const packageParserFailures = RunPackageParserTests();
    std::cout << "\nCore route/package-manager tests: " << passed << " passed, " << failed << " failed\n";
    return failed == 0 && packageParserFailures == 0 ? 0 : 1;
}
