#include "RegexSearchTests.h"

#include "RegexBuilder.h"
#include "RegexSearch.h"

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

    std::cout << "\nRegex Search tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}
