#pragma once

#include <cstddef>
#include <cstdint>
#include <memory>
#include <optional>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core::regex
{
    // PCRE2-16 is used privately by the native implementation.  The public
    // boundary remains ordinary C++ types and applies hard compile, heap,
    // depth, backtracking, input, and callout-time limits before an untrusted
    // pattern can be used from an interactive search surface.
    enum class RegexErrorCode : std::uint8_t
    {
        None = 0,
        Syntax,
        UnsupportedFeature,
        PatternTooLong,
        PatternNestingTooDeep,
        CompiledCodeTooLarge,
        TooComplex,
    };

    struct RegexDiagnostic
    {
        RegexErrorCode code{ RegexErrorCode::None };
        std::size_t offset{};
        std::wstring message{};

        [[nodiscard]] bool Ok() const noexcept { return code == RegexErrorCode::None; }
    };

    struct RegexOptions
    {
        bool case_sensitive{ false };
        bool multiline{ false };
        bool dot_matches_newline{ false };
        // PCRE2 equivalents of .NET's IgnorePatternWhitespace and
        // ExplicitCapture. Named captures remain available with the latter.
        bool ignore_pattern_whitespace{ false };
        bool explicit_capture{ false };
        std::size_t max_pattern_length{ 512 };
        std::size_t max_input_length{ 8 * 1024 };
        std::size_t max_parenthesis_nesting{ 64 };
        std::size_t max_compiled_code_bytes{ 256 * 1024 };
        std::uint32_t match_limit{ 100'000 };
        std::uint32_t depth_limit{ 1'000 };
        std::uint32_t heap_limit_kib{ 1'024 };
        std::uint32_t match_timeout_ms{ 10 };
        std::size_t max_result_count{ 100 };
        std::size_t max_replacement_output_length{ 32 * 1024 };
    };

    struct RegexCapture
    {
        bool matched{ false };
        std::size_t start{};
        std::size_t length{};
        std::wstring name{};
    };

    struct RegexMatchResult
    {
        bool matched{ false };
        bool input_limit_exceeded{ false };
        bool resource_limit_exceeded{ false };
        bool invalid_utf16{ false };
        std::vector<RegexCapture> captures{};
        std::wstring diagnostic{};
    };

    // A bounded all-match result for the native tester. Captures include
    // group zero so the renderer can report a stable match span, and each
    // named capture carries its PCRE2 group name without exposing test text.
    struct RegexOccurrence
    {
        std::size_t start{};
        std::size_t length{};
        std::vector<RegexCapture> captures{};
    };

    struct RegexFindAllResult
    {
        bool input_limit_exceeded{ false };
        bool resource_limit_exceeded{ false };
        bool invalid_utf16{ false };
        bool result_limit_exceeded{ false };
        std::vector<RegexOccurrence> matches{};
        std::wstring diagnostic{};
    };

    // Replacement previews intentionally support only a small explicit
    // dialect: $$, $0..$99 (when that numbered group exists), and ${name}
    // for a known named capture. They never alter a target search surface.
    struct RegexReplaceResult
    {
        bool input_limit_exceeded{ false };
        bool resource_limit_exceeded{ false };
        bool invalid_utf16{ false };
        bool result_limit_exceeded{ false };
        bool output_limit_exceeded{ false };
        bool invalid_replacement{ false };
        std::size_t substitutions{};
        std::wstring output{};
        std::wstring diagnostic{};
    };

    struct RegexProgram;
    struct RegexCompileResult;

    class SafeRegex
    {
    public:
        SafeRegex() = default;

        [[nodiscard]] static RegexCompileResult Compile(
            std::wstring_view pattern,
            RegexOptions options = {});

        [[nodiscard]] bool Valid() const noexcept;
        [[nodiscard]] RegexMatchResult Search(
            std::wstring_view text,
            bool include_captures = false) const;
        [[nodiscard]] RegexMatchResult FullMatch(
            std::wstring_view text,
            bool include_captures = false) const;
        [[nodiscard]] RegexFindAllResult FindAll(
            std::wstring_view text,
            bool include_captures = true) const;
        [[nodiscard]] RegexReplaceResult ReplaceAll(
            std::wstring_view text,
            std::wstring_view replacement) const;

    private:
        explicit SafeRegex(std::shared_ptr<RegexProgram const> program);

        std::shared_ptr<RegexProgram const> m_program;
    };

    struct RegexCompileResult
    {
        std::optional<SafeRegex> expression;
        RegexDiagnostic diagnostic;

        [[nodiscard]] bool Ok() const noexcept { return expression.has_value(); }
    };

    // Builder helpers keep generated patterns literal-safe. The wizard uses
    // these instead of asking callers to hand-escape text fragments.
    [[nodiscard]] std::wstring EscapeRegexLiteral(std::wstring_view value);
    [[nodiscard]] std::wstring BuildRegexCharacterClass(
        std::wstring_view characters,
        bool negate = false);
}
