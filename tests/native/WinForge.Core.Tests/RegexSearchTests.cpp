#include "RegexSearchTests.h"

#include "RegexBuilder.h"
#include "RegexSearch.h"
#include "RegexSearchSurface.h"

#include <iostream>
#include <string_view>
#include <vector>

namespace
{
    struct Suite
    {
        NativeTestCounts counts;

        void Expect(bool condition, std::string_view name)
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
        }
    };
}

NativeTestCounts RunRegexSearchTests()
{
    using namespace winforge::core::regex;
    Suite suite;

    auto expression = SafeRegex::Compile(L"^module\\.(packages|regextester)$");
    suite.Expect(expression.Ok() && expression.expression->Search(L"module.packages").matched
            && !expression.expression->Search(L"module.packages.extra").matched,
        "Regex Search anchors and alternation match catalog ids");

    expression = SafeRegex::Compile(L"winforge", RegexOptions{});
    suite.Expect(expression.Ok() && expression.expression->Search(L"WinForge Native").matched,
        "Regex Search defaults to Unicode case-insensitive matching");

    RegexOptions case_sensitive;
    case_sensitive.case_sensitive = true;
    expression = SafeRegex::Compile(L"WinForge", case_sensitive);
    suite.Expect(expression.Ok() && expression.expression->Search(L"WinForge").matched
            && !expression.expression->Search(L"winforge").matched,
        "Regex Search preserves explicit case-sensitive matching");

    expression = SafeRegex::Compile(L"(?<word>Win)Forge");
    auto captures = expression.expression->Search(L"WinForge", true);
    suite.Expect(expression.Ok() && captures.matched && captures.captures.size() >= 2
            && captures.captures[1].matched && captures.captures[1].start == 0
            && captures.captures[1].length == 3,
        "Regex Search returns capture spans for the native tester");

    expression = SafeRegex::Compile(L"(?<=foo)bar");
    suite.Expect(expression.Ok() && expression.expression->Search(L"foobar").matched,
        "Regex Search supports lookbehind");

    expression = SafeRegex::Compile(L"(?<token>foo)-\\k<token>");
    suite.Expect(expression.Ok() && expression.expression->FullMatch(L"foo-foo").matched
            && !expression.expression->FullMatch(L"foo-bar").matched,
        "Regex Search supports named backreferences");

    RegexOptions extendedWhitespace;
    extendedWhitespace.case_sensitive = true;
    extendedWhitespace.ignore_pattern_whitespace = true;
    expression = SafeRegex::Compile(L"Win Forge # trailing comment", extendedWhitespace);
    suite.Expect(expression.Ok() && expression.expression->FullMatch(L"WinForge").matched,
        "Regex Search supports bounded extended-whitespace matching");

    RegexOptions noAutoCapture;
    noAutoCapture.case_sensitive = true;
    noAutoCapture.explicit_capture = true;
    expression = SafeRegex::Compile(L"(Win)(?<name>Forge)", noAutoCapture);
    captures = expression.Ok()
        ? expression.expression->Search(L"WinForge", true)
        : RegexMatchResult{};
    suite.Expect(expression.Ok() && captures.matched && captures.captures.size() == 2
            && captures.captures[1].name == L"name" && captures.captures[1].matched
            && captures.captures[1].start == 3 && captures.captures[1].length == 5,
        "Regex Search preserves named captures with explicit-capture mode");

    RegexOptions matchSetOptions;
    matchSetOptions.case_sensitive = true;
    matchSetOptions.max_result_count = 4;
    expression = SafeRegex::Compile(L"(?<word>[A-Za-z]+)", matchSetOptions);
    auto matchSet = expression.Ok()
        ? expression.expression->FindAll(L"one 2 two THREE", true)
        : RegexFindAllResult{};
    suite.Expect(expression.Ok() && matchSet.matches.size() == 3
            && matchSet.matches[0].start == 0 && matchSet.matches[0].length == 3
            && matchSet.matches[1].start == 6 && matchSet.matches[1].length == 3
            && matchSet.matches[2].start == 10 && matchSet.matches[2].length == 5
            && matchSet.matches[0].captures.size() == 2
            && matchSet.matches[0].captures[1].name == L"word"
            && !matchSet.result_limit_exceeded,
        "Regex Search returns bounded all-match sets with named capture metadata");

    expression = SafeRegex::Compile(L"(?=a)", matchSetOptions);
    auto zeroLengthSet = expression.Ok()
        ? expression.expression->FindAll(L"aa", true)
        : RegexFindAllResult{};
    suite.Expect(expression.Ok() && zeroLengthSet.matches.size() == 2
            && zeroLengthSet.matches[0].start == 0 && zeroLengthSet.matches[0].length == 0
            && zeroLengthSet.matches[1].start == 1 && zeroLengthSet.matches[1].length == 0,
        "Regex Search advances safely after zero-length all-match results");

    matchSetOptions.max_result_count = 2;
    expression = SafeRegex::Compile(L"\\w", matchSetOptions);
    auto cappedSet = expression.Ok()
        ? expression.expression->FindAll(L"abc", false)
        : RegexFindAllResult{};
    suite.Expect(expression.Ok() && cappedSet.matches.size() == 2
            && cappedSet.result_limit_exceeded,
        "Regex Search stops all-match enumeration at its configured result cap");

    expression = SafeRegex::Compile(L"(?<word>[a-z]+)", matchSetOptions);
    auto replacement = expression.Ok()
        ? expression.expression->ReplaceAll(L"ab cd", L"${word}-$0-$$-$1")
        : RegexReplaceResult{};
    suite.Expect(expression.Ok() && replacement.substitutions == 2
            && replacement.output == L"ab-ab-$-ab cd-cd-$-cd"
            && !replacement.invalid_replacement && !replacement.output_limit_exceeded,
        "Regex Search previews bounded numbered named and literal-dollar replacements");

    auto invalidReplacement = expression.Ok()
        ? expression.expression->ReplaceAll(L"ab", L"$&")
        : RegexReplaceResult{};
    suite.Expect(expression.Ok() && invalidReplacement.invalid_replacement
            && invalidReplacement.output.empty(),
        "Regex Search rejects replacement syntax outside its explicit local subset");

    RegexOptions replacementLimit;
    replacementLimit.case_sensitive = true;
    replacementLimit.max_replacement_output_length = 5;
    expression = SafeRegex::Compile(L"a", replacementLimit);
    auto outputLimitedReplacement = expression.Ok()
        ? expression.expression->ReplaceAll(L"aaaa", L"xx")
        : RegexReplaceResult{};
    suite.Expect(expression.Ok() && outputLimitedReplacement.output_limit_exceeded
            && outputLimitedReplacement.output.empty(),
        "Regex Search rejects replacement previews beyond the output safety cap");

    expression = SafeRegex::Compile(L"(?>a|ab)c");
    suite.Expect(expression.Ok() && !expression.expression->FullMatch(L"abc").matched,
        "Regex Search keeps PCRE2 atomic-group semantics");

    expression = SafeRegex::Compile(L"[");
    suite.Expect(!expression.Ok() && expression.diagnostic.code == RegexErrorCode::Syntax
            && expression.diagnostic.offset == 1,
        "Regex Search reports syntax diagnostics with offsets");

    expression = SafeRegex::Compile(L"\\C");
    suite.Expect(!expression.Ok() && expression.diagnostic.code == RegexErrorCode::UnsupportedFeature,
        "Regex Search rejects unsafe backslash-C byte matching");

    RegexOptions limited;
    limited.max_pattern_length = 4;
    expression = SafeRegex::Compile(L"abcde", limited);
    suite.Expect(!expression.Ok() && expression.diagnostic.code == RegexErrorCode::PatternTooLong,
        "Regex Search enforces a pattern-length safety limit");

    limited = RegexOptions{};
    limited.max_input_length = 3;
    expression = SafeRegex::Compile(L"a", limited);
    auto input_limit = expression.expression->Search(L"aaaa");
    suite.Expect(expression.Ok() && input_limit.input_limit_exceeded && !input_limit.matched,
        "Regex Search rejects oversized input before matching");

    limited = RegexOptions{};
    limited.match_limit = 100;
    limited.match_timeout_ms = 100;
    expression = SafeRegex::Compile(L"(a+)+$", limited);
    auto const pathological = expression.expression->Search(std::wstring(2'048, L'a') + L"b");
    suite.Expect(expression.Ok() && pathological.resource_limit_exceeded,
        "Regex Search contains pathological backtracking with a resource limit");

    suite.Expect(EscapeRegexLiteral(L"a.b[1]") == L"a\\.b\\[1\\]"
            && BuildRegexCharacterClass(L"a-z]") == L"[a\\-z\\]]",
        "Regex builder escapes literal and character-class fragments");

    auto const grouped = BuildRegexGroup(L"foo|bar", L"choice");
    std::vector<std::wstring> alternatives{ L"foo", L"bar" };
    auto const alternation = BuildRegexAlternation(alternatives);
    auto const quantified = ApplyRegexQuantifier(L"\\d", { RegexQuantifier::Range, 2, 4, false });
    suite.Expect(grouped == L"(?<choice>foo|bar)" && alternation == L"(?:foo|bar)"
            && quantified == L"(?:\\d){2,4}",
        "Regex builder composes groups alternatives and quantifiers");

    auto const recipes = RegexRecipes();
    auto const literalExact = BuildRegexRecipe(RegexRecipe::LiteralExact, L"a.b");
    auto const routeId = BuildRegexRecipe(RegexRecipe::NativeRouteId);
    auto const packageId = BuildRegexRecipe(RegexRecipe::PackageId);
    auto const semanticVersion = BuildRegexRecipe(RegexRecipe::SemanticVersion);
    auto const wordBoundary = BuildRegexAssertion(RegexAssertion::WordBoundary);
    auto const positiveLookahead = BuildRegexAssertion(RegexAssertion::PositiveLookahead, L"bar");
    auto const negativeLookbehind = BuildRegexAssertion(RegexAssertion::NegativeLookbehind, L"foo");
    auto const recipeExactExpression = SafeRegex::Compile(literalExact);
    auto const routeExpression = SafeRegex::Compile(routeId);
    auto const packageExpression = SafeRegex::Compile(packageId);
    auto const versionExpression = SafeRegex::Compile(semanticVersion);
    suite.Expect(recipes.size() == 8 && literalExact == L"^(?:a\\.b)$"
            && routeExpression.Ok() && routeExpression.expression->FullMatch(L"module.reactor").matched
            && packageExpression.Ok() && packageExpression.expression->FullMatch(L"WinGet.Client-2").matched
            && versionExpression.Ok() && versionExpression.expression->Search(L"version v1.2.3-beta+5").matched
            && wordBoundary == L"\\b" && positiveLookahead == L"(?=bar)"
            && negativeLookbehind == L"(?<!foo)",
        "Regex builder recipes and bounded assertions compose valid PCRE2 patterns");

    auto const surfaces = RegexSearchSurfaces();
    auto const& shellSurface = RegexSearchSurfaceFor(RegexSearchSurfaceId::ShellCatalog);
    auto const& allAppsSurface = RegexSearchSurfaceFor(RegexSearchSurfaceId::AllApps);
    auto const& packageSurface = RegexSearchSurfaceFor(RegexSearchSurfaceId::PackageDiscoverCachedResults);
    auto const& appUninstallerSurface =
        RegexSearchSurfaceFor(RegexSearchSurfaceId::AppUninstallerCachedResults);
    auto const& cheatSurface = RegexSearchSurfaceFor(RegexSearchSurfaceId::RegexCheatsheetEntries);
    auto const& symbolsSurface = RegexSearchSurfaceFor(RegexSearchSurfaceId::SymbolsPalette);
    suite.Expect(surfaces.size() == 6
            && shellSurface.search_automation_id == L"NativeShellSearchBox"
            && allAppsSurface.invalid_pattern_policy == RegexInvalidPatternPolicy::KeepPriorVisibleResults
            && packageSurface.query_policy == RegexSearchQueryPolicy::LocalCachedResultsOnly
            && packageSurface.regex_mode_automation_id == L"NativePackageRegexMode"
            && appUninstallerSurface.route == L"module.uninstall"
            && appUninstallerSurface.search_automation_id == L"NativeAppUninstallerSearch"
            && appUninstallerSurface.query_policy == RegexSearchQueryPolicy::LocalCachedResultsOnly
            && appUninstallerSurface.invalid_pattern_policy ==
                RegexInvalidPatternPolicy::KeepPriorVisibleResults
            && cheatSurface.route == L"module.regexcheat"
            && cheatSurface.query_policy == RegexSearchQueryPolicy::LocalCatalog
            && cheatSurface.invalid_pattern_policy == RegexInvalidPatternPolicy::KeepPriorVisibleResults
            && symbolsSurface.route == L"module.symbols"
            && symbolsSurface.search_automation_id == L"NativeSymbolsSearch"
            && symbolsSurface.query_policy == RegexSearchQueryPolicy::LocalCatalog
            && symbolsSurface.invalid_pattern_policy == RegexInvalidPatternPolicy::KeepPriorVisibleResults,
        "Regex search-surface contract covers each implemented native filter and local-only policy");

    std::cout << "\nRegex Search tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}
