#include "BinaryTextTests.h"
#include "CaseConvertTests.h"
#include "CommandLine.h"
#include "CheckDigitTests.h"
#include "CodecTests.h"
#include "DesignTools.h"
#include "DesignToolsTests.h"
#include "LineProcessingTests.h"
#include "MorseTests.h"
#include "TextAnalysisTests.h"
#include "ReferenceTextTests.h"
#include "SlugifyTests.h"
#include "GuidGenTests.h"
#include "PassGenTests.h"
#include "PasswordStrengthTests.h"
#include "RomanNumTests.h"
#include "UnixPermTests.h"
#include "TextDiffTests.h"
#include "UuidV7Tests.h"
#include "RegexCheatTests.h"
#include "SymbolsPaletteTests.h"
#include "AppUninstallerTests.h"
#include "RegexSearchTests.h"
#include "Localization.h"
#include "PackageManagerTests.h"
#include "PackageSetupTests.h"
#include "PackageMutationCoordinatorTests.h"
#include "PackageRuntime.h"
#include "PackageRuntimeTests.h"
#include "ProcessRunnerTests.h"
#include "RouteIndex.h"

#include <chrono>
#include <bit>
#include <cerrno>
#include <cmath>
#include <cstdint>
#include <cstdlib>
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

    if (argc == 3 && std::wstring_view(argv[1]) == L"--aspect-format-sample")
    {
        errno = 0;
        wchar_t* end{};
        auto const requested = std::wcstoull(argv[2], &end, 10);
        if (errno != 0 || end == argv[2] || *end != L'\0' || requested == 0 || requested > 2'000'000)
        {
            std::cerr << "aspect format sample count must be between 1 and 2000000\n";
            return 2;
        }

        auto narrowAscii = [](std::wstring const& value)
        {
            std::string result;
            result.reserve(value.size());
            for (auto const ch : value)
            {
                if (ch < 0 || ch > 0x7F) throw std::runtime_error("aspect format probe emitted non-ASCII text");
                result.push_back(static_cast<char>(ch));
            }
            return result;
        };
        std::uint64_t state{ 0x9E3779B97F4A7C15ull };
        std::uint64_t emitted{};
        while (emitted < requested)
        {
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            auto const bits = state * 0x2545F4914F6CDD1Dull;
            auto const value = std::bit_cast<double>(bits);
            if (!std::isfinite(value)) continue;

            std::cout << bits << '\t'
                << narrowAscii(winforge::core::aspectratio::FormatDisplayNumber(value, 0)) << '\t'
                << narrowAscii(winforge::core::aspectratio::FormatDisplayNumber(value, 2)) << '\t'
                << narrowAscii(winforge::core::aspectratio::FormatDisplayNumber(value, 4)) << '\n';
            ++emitted;
        }
        return 0;
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

    request = ParseLaunchRequest({ L"WinForge.exe", L"--page", L"search:^(WinForge)?#Native$" });
    Expect(request.route == L"search" && request.argument == L"^(WinForge)?#Native$",
        "preserves raw regex search payload casing question and hash");

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
    Expect(winforge::core::HasNativeRenderer(L" module.unixperm ") &&
        winforge::core::HasNativeRenderer(L"MODULE.TEXTDIFF") &&
        winforge::core::HasNativeRenderer(L"module.aspectratio") &&
        winforge::core::HasNativeRenderer(L"module.cssunits") &&
        winforge::core::HasNativeRenderer(L" module.linetools ") &&
        winforge::core::HasNativeRenderer(L"MODULE.TEXTSORT") &&
        winforge::core::HasNativeRenderer(L"module.textwrap") &&
        winforge::core::HasNativeRenderer(L"module.phonetic") &&
        winforge::core::HasNativeRenderer(L"MODULE.BOXTEXT") &&
        winforge::core::HasNativeRenderer(L" module.htmlentities ") &&
        winforge::core::HasNativeRenderer(L"module.morse") &&
        winforge::core::HasNativeRenderer(L"MODULE.SLUGIFY") &&
        winforge::core::HasNativeRenderer(L"MODULE.REGEXCHEAT") &&
        winforge::core::HasNativeRenderer(L"shell.allapps"),
        "native renderer contract identifies implemented fixed routes");
    Expect(!winforge::core::HasNativeRenderer(L"module.smelter") &&
        !winforge::core::HasNativeRenderer(L"settings"),
        "native renderer contract keeps pending routes explicit");

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

    auto const codecCounts = winforge::tests::codec::RunCodecTests();
    passed += codecCounts.passed;
    failed += codecCounts.failed;

    auto const guidGenFailures = winforge::tests::guidgen::RunGuidGenTests();
    passed += 16 - guidGenFailures;
    failed += guidGenFailures;

    auto const passGenCounts = RunPassGenTests();
    passed += passGenCounts.passed;
    failed += passGenCounts.failed;

    auto const passwordStrengthCounts = RunPasswordStrengthTests();
    passed += passwordStrengthCounts.passed;
    failed += passwordStrengthCounts.failed;

    auto const romanNumCounts = RunRomanNumTests();
    passed += romanNumCounts.passed;
    failed += romanNumCounts.failed;

    auto const unixPermCounts = RunUnixPermTests();
    passed += unixPermCounts.passed;
    failed += unixPermCounts.failed;

    auto const textDiffCounts = RunTextDiffTests();
    passed += textDiffCounts.passed;
    failed += textDiffCounts.failed;

    auto const designToolsCounts = RunDesignToolsTests();
    passed += designToolsCounts.passed;
    failed += designToolsCounts.failed;

    auto const lineProcessingCounts = RunLineProcessingTests();
    passed += lineProcessingCounts.passed;
    failed += lineProcessingCounts.failed;

    auto const morseCounts = RunMorseTests();
    passed += morseCounts.passed;
    failed += morseCounts.failed;

    auto const textAnalysisCounts = RunTextAnalysisTests();
    passed += textAnalysisCounts.passed;
    failed += textAnalysisCounts.failed;

    auto const referenceTextCounts = RunReferenceTextTests();
    passed += referenceTextCounts.passed;
    failed += referenceTextCounts.failed;

    auto const slugifyCounts = RunSlugifyTests();
    passed += slugifyCounts.passed;
    failed += slugifyCounts.failed;

    auto const uuidV7Counts = RunUuidV7Tests();
    passed += uuidV7Counts.passed;
    failed += uuidV7Counts.failed;

    auto const regexSearchCounts = RunRegexSearchTests();
    passed += regexSearchCounts.passed;
    failed += regexSearchCounts.failed;

    auto const regexCheatCounts = RunRegexCheatTests();
    passed += regexCheatCounts.passed;
    failed += regexCheatCounts.failed;

    auto const symbolsPaletteCounts = RunSymbolsPaletteTests();
    passed += symbolsPaletteCounts.passed;
    failed += symbolsPaletteCounts.failed;

    auto const appUninstallerCounts = RunAppUninstallerTests();
    passed += appUninstallerCounts.passed;
    failed += appUninstallerCounts.failed;

    auto const package_manager_counts = RunPackageManagerTests();
    passed += package_manager_counts.passed;
    failed += package_manager_counts.failed;

    auto const package_setup_counts = RunPackageSetupTests();
    passed += package_setup_counts.passed;
    failed += package_setup_counts.failed;

    auto const package_mutation_counts = RunPackageMutationCoordinatorTests();
    passed += package_mutation_counts.passed;
    failed += package_mutation_counts.failed;

    auto const package_runtime_counts = RunPackageRuntimeTests();
    passed += package_runtime_counts.passed;
    failed += package_runtime_counts.failed;

    auto const packageParserFailures = RunPackageParserTests();
    std::cout << "\nCore route/package-manager tests: " << passed << " passed, " << failed << " failed\n";
    return failed == 0 && packageParserFailures == 0 ? 0 : 1;
}
