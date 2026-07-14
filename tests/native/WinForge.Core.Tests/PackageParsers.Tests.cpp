#include "PackageParsers.h"

#include <algorithm>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <iterator>
#include <string>
#include <string_view>
#include <vector>

namespace
{
    using winforge::core::packages::PackageOutputKind;
    using winforge::core::packages::PackageRecord;

    int passed = 0;
    int failed = 0;

    void Expect(bool condition, std::string_view name)
    {
        if (condition)
        {
            ++passed;
            std::cout << "PASS package parser: " << name << '\n';
        }
        else
        {
            ++failed;
            std::cerr << "FAIL package parser: " << name << '\n';
        }
    }

    std::filesystem::path FindFixtureRoot()
    {
        auto const sourceRelative = std::filesystem::path(__FILE__).parent_path()
            / "Fixtures" / "PackageManagers";
        if (std::filesystem::exists(sourceRelative))
        {
            return sourceRelative;
        }

        auto current = std::filesystem::current_path();
        for (int depth = 0; depth < 8; ++depth)
        {
            auto const candidate = current / "tests" / "native" / "WinForge.Core.Tests"
                / "Fixtures" / "PackageManagers";
            if (std::filesystem::exists(candidate))
            {
                return candidate;
            }
            if (!current.has_parent_path() || current.parent_path() == current)
            {
                break;
            }
            current = current.parent_path();
        }
        return sourceRelative;
    }

    std::string ReadFixture(std::string_view name)
    {
        auto const path = FindFixtureRoot() / std::filesystem::path(name);
        std::ifstream input(path, std::ios::binary);
        return { std::istreambuf_iterator<char>(input), std::istreambuf_iterator<char>() };
    }

    PackageRecord const* Find(std::vector<PackageRecord> const& packages, std::string_view id)
    {
        for (auto const& package : packages)
        {
            if (package.id == id)
            {
                return &package;
            }
        }
        return nullptr;
    }
}

int RunPackageParserTests()
{
    using namespace winforge::core::packages;

    static_assert(noexcept(ParseWingetTable({})));
    static_assert(noexcept(ParseScoopSearch({})));
    static_assert(noexcept(ParseChocolatey({}, PackageOutputKind::Installed)));
    static_assert(noexcept(ParsePipJson({}, PackageOutputKind::Installed)));
    static_assert(noexcept(ParseNpmJson({}, PackageOutputKind::Search)));
    static_assert(noexcept(ParsePackageDetailsFields({})));

    auto packages = ParseWingetTable(ReadFixture("winget-table.txt"));
    auto const winget = Find(packages, "7zip.7zip");
    Expect(packages.size() == 2, "winget parses header-offset table");
    Expect(winget && winget->version == "24.09" && winget->availableVersion == "25.00"
        && winget->source == "winget", "winget maps installed, available, and source columns");
    auto const unicodeWinget = Find(packages, "Contoso.Cafe");
    Expect(unicodeWinget && unicodeWinget->name == "Café Tool"
        && unicodeWinget->availableVersion.empty() && unicodeWinget->source == "msstore",
        "winget preserves UTF-8 names and blank optional columns");
    Expect(ParseWingetTable("diagnostic only\n").empty(), "winget rejects output without a table header");

    packages = ParseScoopSearch(ReadFixture("scoop-search.txt"));
    auto const scoopPython = Find(packages, "python310");
    Expect(packages.size() == 2, "Scoop search skips diagnostics");
    Expect(scoopPython && scoopPython->version == "3.10.11" && scoopPython->source == "versions",
        "Scoop search tracks bucket sections and parenthesized versions");

    packages = ParseScoopInstalledJson(ReadFixture("scoop-export.json"));
    auto const scoopPwsh = Find(packages, "pwsh");
    Expect(packages.size() == 2, "Scoop export accepts object apps shape and drops nameless rows");
    Expect(scoopPwsh && scoopPwsh->source == "versions" && scoopPwsh->managerKey == "scoop",
        "Scoop export accepts lower-case fields and Bucket fallback");

    packages = ParseScoopList(ReadFixture("scoop-list.txt"));
    Expect(packages.size() == 2 && Find(packages, "git") && Find(packages, "pwsh"),
        "Scoop list parses fallback table");
    packages = ParseScoopStatus(ReadFixture("scoop-status.txt"));
    Expect(packages.size() == 1 && packages[0].id == "git"
        && packages[0].availableVersion == "2.48.1", "Scoop status returns changed packages only");

    packages = ParseChocolatey(ReadFixture("chocolatey-pipe.txt"), PackageOutputKind::Updates);
    Expect(packages.size() == 1 && packages[0].id == "git"
        && packages[0].version == "2.47.0" && packages[0].availableVersion == "2.48.1",
        "Chocolatey parses limit-output update pipes and filters equal/noise rows");
    packages = ParseChocolatey(ReadFixture("chocolatey-table.txt"), PackageOutputKind::Installed);
    Expect(packages.size() == 2 && Find(packages, "7zip"),
        "Chocolatey parses legacy whitespace list table");

    auto const pipFixture = ReadFixture("pip.json");
    packages = ParsePipJson(pipFixture, PackageOutputKind::Installed);
    auto const numericPip = Find(packages, "café-lib");
    Expect(packages.size() == 2 && numericPip && numericPip->version == "7",
        "pip parses warning-prefixed JSON and scalar versions");
    packages = ParsePipJson(pipFixture, PackageOutputKind::Updates);
    auto const requests = Find(packages, "requests");
    Expect(requests && requests->availableVersion == "2.32.3" && requests->managerKey == "pip",
        "pip maps latest_version for outdated output");
    packages = ParsePipJson(R"({"projects":[{"name":"httpx"},{"name":"café"}]})",
        PackageOutputKind::Search);
    Expect(packages.size() == 2 && packages[0].source == "pypi.org",
        "pip parses PyPI simple-index search JSON");
    packages = ParsePyPiSearchJson(ReadFixture("pypi-index.json"), "requests");
    std::string largePyPiIndex = "{\"projects\":[";
    for (int index = 0; index < 25; ++index)
    {
        if (index > 0) largePyPiIndex += ',';
        largePyPiIndex += "{\"name\":\"tool-" + std::to_string(index) + "\"}";
    }
    largePyPiIndex += "]}";
    auto const cappedPyPi = ParsePyPiSearchJson(largePyPiIndex, "tool");
    Expect(packages.size() == 4 && packages[0].id == "Requests"
        && packages[1].id == "requests-cache" && packages[3].id == "django-requests"
        && cappedPyPi.size() == 20,
        "PyPI index search de-duplicates, filters, ranks, and caps candidates");
    Expect(ParsePipJson("[{broken]", PackageOutputKind::Installed).empty(),
        "pip malformed JSON fails closed");

    auto details = ParsePackageDetailsFields(
        "Found Git [Git.Git]\n"
        "Version: 2.50.1\n"
        "Publisher: The Git Development Community\n"
        "Homepage: https://git-scm.com\n"
        "License: GPL-2.0-only\n"
        "Password: should-never-render\n");
    Expect(details.size() == 6
        && details[0].label == "Name" && details[0].value == "Git"
        && details[1].label == "Package ID" && details[1].value == "Git.Git",
        "details parser extracts winget found header and safe key-value fields");
    Expect(std::none_of(details.begin(), details.end(), [](PackageDetailField const& field)
        {
            return field.value == "should-never-render";
        }), "details parser drops secret-like fields");

    details = ParsePackageDetailsFields(
        R"({"name":"ripgrep","version":"14.1.1","description":"Search recursively","repository":"https://github.com/BurntSushi/ripgrep","keywords":["grep","search"],"access_token":"nope"})");
    Expect(details.size() == 5
        && details[0].label == "Name" && details[0].value == "ripgrep"
        && details[4].label == "Tags" && details[4].value == "grep, search",
        "details parser extracts safe JSON scalar and list fields");
    details = ParsePackageDetailsFields("Name: First\nName: Second\nUnknown: hidden\n");
    Expect(details.size() == 1 && details[0].value == "First",
        "details parser de-duplicates canonical labels and ignores unknown fields");

    packages = ParseNpmJson(ReadFixture("npm-search-array.txt"), PackageOutputKind::Search);
    Expect(packages.size() == 2 && Find(packages, "@types/node"),
        "npm search parses warning-prefixed JSON arrays");
    packages = ParseNpmJson(ReadFixture("npm-search.ndjson"), PackageOutputKind::Search);
    Expect(packages.size() == 2 && Find(packages, "npm-check-updates"),
        "npm search falls back to NDJSON and skips malformed/incomplete entries");
    packages = ParseNpmJson(ReadFixture("npm-installed.json"), PackageOutputKind::Installed);
    auto const scopedNpm = Find(packages, "@scope/tool");
    Expect(packages.size() == 2 && scopedNpm && scopedNpm->version == "2.0.0",
        "npm installed parses dependency object and scoped ids");
    packages = ParseNpmJson(ReadFixture("npm-outdated.json"), PackageOutputKind::Updates);
    Expect(packages.size() == 1 && packages[0].id == "npm"
        && packages[0].availableVersion == "11.0.0", "npm outdated maps current and latest");

    packages = ParseBunSearchJson(ReadFixture("bun-search.json"));
    Expect(packages.size() == 2 && packages[0].managerKey == "bun"
        && packages[0].source == "npmjs.org", "Bun parses npm registry search response");
    packages = ParseNpmRegistrySearchJson(ReadFixture("bun-search.json"), "npm");
    Expect(packages.size() == 2 && packages[0].managerKey == "npm"
        && packages[0].source == "npmjs.org", "npm registry parser supports an explicit manager key");
    packages = ParseBunInstalled(ReadFixture("bun-installed.txt"));
    auto const scopedBun = Find(packages, "@devcontainers/cli");
    Expect(packages.size() == 3 && scopedBun && scopedBun->version == "0.81.1",
        "Bun installed tree splits scoped names at the last at-sign");
    auto const bunOutdated = ReadFixture("bun-outdated.txt");
    packages = ParseBunOutdated(bunOutdated);
    Expect(packages.size() == 1 && packages[0].availableVersion == "5.4.0",
        "Bun outdated accepts Unicode tables and prefers Latest");
    packages = ParseBunOutdated(bunOutdated, false);
    Expect(packages.size() == 1 && packages[0].availableVersion == "5.3.0",
        "Bun outdated can select compatible Update column");

    packages = ParseDotnetToolTable(ReadFixture("dotnet-tools.txt"), PackageOutputKind::Search);
    Expect(packages.size() == 2 && packages[0].id == "dotnetsay" && packages[0].version == "2.1.7",
        "dotnet tool parses separator-delimited table");
    Expect(ParseDotnetToolTable(ReadFixture("dotnet-tools.txt"), PackageOutputKind::Updates).empty(),
        "dotnet tool does not invent a CLI updates table");

    packages = ParsePowerShellGalleryJson(ReadFixture("powershell.json"));
    auto const pester = Find(packages, "Pester");
    Expect(packages.size() == 2 && pester && pester->availableVersion == "5.8.0"
        && pester->source == "PSGallery", "PowerShell Gallery parses array JSON and update metadata");
    packages = ParsePowerShell7Json(
        R"({"Name":"caf\u00e9-module","Version":"1.0","Repository":"PSGallery"})");
    Expect(packages.size() == 1 && packages[0].id == "café-module"
        && packages[0].managerKey == "pwsh7", "PowerShell 7 parses single-object JSON and escapes");

    packages = ParseCargoSearch(ReadFixture("cargo-search.txt"));
    Expect(packages.size() == 2 && packages[0].id == "ripgrep" && packages[0].version == "14.1.1",
        "Cargo search parses quoted versions");
    packages = ParseCargoInstalled(ReadFixture("cargo-installed.txt"));
    Expect(packages.size() == 2 && Find(packages, "cargo-edit")
        && packages[0].version == "14.1.1", "Cargo install list ignores indented binaries");
    packages = ParseCargoUpdates(ReadFixture("cargo-updates.txt"));
    Expect(packages.size() == 1 && packages[0].availableVersion == "14.1.1"
        && packages[0].source == "crates.io", "Cargo update helper table maps two versions");

    packages = ParseVcpkg(ReadFixture("vcpkg-search.txt"), PackageOutputKind::Search);
    Expect(packages.size() == 2 && packages[0].id == "zlib" && packages[0].version == "1.3.1",
        "vcpkg search parses port rows and skips footer");
    packages = ParseVcpkg(ReadFixture("vcpkg-list.txt"), PackageOutputKind::Installed);
    Expect(packages.size() == 2 && Find(packages, "fmt:x64-windows"),
        "vcpkg list preserves triplet-qualified ids");
    packages = ParseVcpkg(ReadFixture("vcpkg-update.txt"), PackageOutputKind::Updates);
    Expect(packages.size() == 1 && packages[0].name == "zlib"
        && packages[0].source == "x64-windows" && packages[0].availableVersion == "1.3.1",
        "vcpkg update parses arrows and derives triplet source");

    auto dispatched = ParsePackageOutput(
        PackageParserKind::ScoopSearch,
        ReadFixture("scoop-status.txt"),
        PackageOutputKind::Updates,
        "scoop");
    Expect(dispatched.supported && dispatched.packages.size() == 1
        && dispatched.packages[0].id == "git", "dispatcher maps ScoopSearch by operation kind");
    dispatched = ParsePackageOutput(
        PackageParserKind::PyPiSearch,
        ReadFixture("pypi-index.json"),
        PackageOutputKind::Search,
        "pip",
        "requests");
    Expect(dispatched.supported && dispatched.requiresRuntimeResolution
        && dispatched.packages.size() == 4 && !dispatched.diagnostic.empty(),
        "dispatcher exposes PyPI version-hydration requirement");
    dispatched = ParsePackageOutput(
        PackageParserKind::DotnetUpdatesFromNuGet,
        ReadFixture("dotnet-tools.txt"),
        PackageOutputKind::Updates,
        "dotnet");
    Expect(!dispatched.supported && dispatched.requiresRuntimeResolution
        && !dispatched.diagnostic.empty(), "dispatcher fails closed for multi-request dotnet updates");

    std::vector<PackageParserKind> const parserKinds{
        PackageParserKind::None,
        PackageParserKind::WingetTable,
        PackageParserKind::ScoopSearch,
        PackageParserKind::ScoopExport,
        PackageParserKind::ChocolateyDelimited,
        PackageParserKind::JsonPackages,
        PackageParserKind::NpmJson,
        PackageParserKind::DotnetTable,
        PackageParserKind::DotnetUpdatesFromNuGet,
        PackageParserKind::PowerShellPackagesJson,
        PackageParserKind::CargoSearch,
        PackageParserKind::CargoInstalled,
        PackageParserKind::CargoUpdates,
        PackageParserKind::BunInstalled,
        PackageParserKind::BunUpdates,
        PackageParserKind::VcpkgSearch,
        PackageParserKind::VcpkgInstalled,
        PackageParserKind::VcpkgUpdates,
        PackageParserKind::PyPiSearch,
        PackageParserKind::NpmRegistrySearch,
    };
    int unsupportedKinds = 0;
    for (auto const parserKind : parserKinds)
    {
        auto const coverage = ParsePackageOutput(
            parserKind, {}, PackageOutputKind::Search, "bun", "query");
        unsupportedKinds += coverage.supported ? 0 : 1;
    }
    Expect(parserKinds.size() == 20 && unsupportedKinds == 1,
        "dispatcher explicitly covers every command post-process kind");

    std::vector<std::string> malformedCorpus{
        {},
        "{",
        "[",
        R"({"unterminated":")",
        "\x1b[31mgarbage\x1b[0m",
        std::string(80, '[') + std::string(80, ']'),
    };
    bool malformedFailedClosed = true;
    for (auto const& sample : malformedCorpus)
    {
        for (auto const parserKind : parserKinds)
        {
            auto const parsed = ParsePackageOutput(
                parserKind, sample, PackageOutputKind::Search, "bun", "query");
            malformedFailedClosed = malformedFailedClosed && parsed.packages.empty();
        }
    }
    Expect(malformedFailedClosed, "all dispatcher paths fail closed across malformed corpus");

    Expect(ParsePowerShellGalleryJson("{\"Name\": [}").empty()
        && ParseNpmJson("\0garbage", PackageOutputKind::Search).empty()
        && ParseBunOutdated("totally unrelated output").empty(),
        "malformed and unrelated output fails closed without partial records");

    std::cout << "\nPackage parser tests: " << passed << " passed, " << failed << " failed\n";
    return failed;
}
