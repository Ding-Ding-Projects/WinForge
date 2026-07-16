#include "RegexSearch.h"

#ifndef PCRE2_STATIC
#define PCRE2_STATIC
#endif
#define PCRE2_CODE_UNIT_WIDTH 16
#include <pcre2.h>

#include <algorithm>
#include <chrono>
#include <limits>
#include <memory>
#include <new>
#include <utility>

namespace winforge::core::regex
{
    struct RegexProgram
    {
        RegexOptions options{};
        pcre2_code* code{ nullptr };

        ~RegexProgram()
        {
            if (code)
            {
                pcre2_code_free(code);
            }
        }
    };
}

namespace
{
    using namespace winforge::core::regex;

    struct MatchDeadline
    {
        std::chrono::steady_clock::time_point deadline;
    };

    [[nodiscard]] int PcreTimeoutCallout(pcre2_callout_block*, void* user_data) noexcept
    {
        auto const* deadline = static_cast<MatchDeadline const*>(user_data);
        return deadline && std::chrono::steady_clock::now() >= deadline->deadline
            ? PCRE2_ERROR_CALLOUT
            : 0;
    }

    [[nodiscard]] std::wstring PcreMessage(int code)
    {
        PCRE2_UCHAR buffer[256]{};
        auto const length = pcre2_get_error_message(code, buffer,
            static_cast<PCRE2_SIZE>(sizeof(buffer) / sizeof(buffer[0])));
        if (length < 0)
        {
            return L"PCRE2 returned an undocumented error.";
        }
        return std::wstring(reinterpret_cast<wchar_t const*>(buffer),
            static_cast<std::size_t>(length));
    }

    [[nodiscard]] std::size_t MaximumParenthesisNesting(std::wstring_view pattern)
    {
        std::size_t depth{};
        std::size_t maximum{};
        bool escaped{};
        bool in_class{};
        for (auto const character : pattern)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }
            if (character == L'\\')
            {
                escaped = true;
                continue;
            }
            if (in_class)
            {
                if (character == L']') in_class = false;
                continue;
            }
            if (character == L'[')
            {
                in_class = true;
                continue;
            }
            if (character == L'(')
            {
                ++depth;
                maximum = std::max(maximum, depth);
            }
            else if (character == L')' && depth > 0)
            {
                --depth;
            }
        }
        return maximum;
    }

    [[nodiscard]] bool IsUtf16Error(int code) noexcept
    {
        return code <= PCRE2_ERROR_UTF16_ERR1 && code >= PCRE2_ERROR_UTF16_ERR3;
    }

    [[nodiscard]] bool IsResourceLimitError(int code) noexcept
    {
        return code == PCRE2_ERROR_MATCHLIMIT
            || code == PCRE2_ERROR_DEPTHLIMIT
            || code == PCRE2_ERROR_HEAPLIMIT
            || code == PCRE2_ERROR_CALLOUT;
    }

    [[nodiscard]] RegexMatchResult RunMatch(
        RegexProgram const& program,
        std::wstring_view text,
        bool full_match,
        bool include_captures)
    {
        RegexMatchResult result;
        if (text.size() > program.options.max_input_length)
        {
            result.input_limit_exceeded = true;
            result.diagnostic = L"Regex input exceeded the safety limit of "
                + std::to_wstring(program.options.max_input_length) + L" UTF-16 code units.";
            return result;
        }

        auto* match_data = pcre2_match_data_create_from_pattern(program.code, nullptr);
        auto* match_context = pcre2_match_context_create(nullptr);
        if (!match_data || !match_context)
        {
            if (match_data) pcre2_match_data_free(match_data);
            if (match_context) pcre2_match_context_free(match_context);
            result.resource_limit_exceeded = true;
            result.diagnostic = L"Native regex resources could not be allocated.";
            return result;
        }

        pcre2_set_match_limit(match_context, program.options.match_limit);
        pcre2_set_depth_limit(match_context, program.options.depth_limit);
        pcre2_set_heap_limit(match_context, program.options.heap_limit_kib);
        MatchDeadline deadline{
            std::chrono::steady_clock::now()
                + std::chrono::milliseconds(program.options.match_timeout_ms) };
        pcre2_set_callout(match_context, PcreTimeoutCallout, &deadline);

        auto const subject = reinterpret_cast<PCRE2_SPTR>(text.data());
        auto const flags = full_match ? PCRE2_ANCHORED : 0u;
        auto const match_count = pcre2_match(
            program.code,
            subject,
            static_cast<PCRE2_SIZE>(text.size()),
            0,
            flags,
            match_data,
            match_context);

        if (match_count >= 0)
        {
            auto const* vector = pcre2_get_ovector_pointer(match_data);
            result.matched = !full_match
                || (vector[0] == 0 && vector[1] == static_cast<PCRE2_SIZE>(text.size()));
            if (result.matched && include_captures)
            {
                auto const groups = pcre2_get_ovector_count(match_data);
                result.captures.reserve(groups);
                for (std::uint32_t index = 0; index < groups; ++index)
                {
                    auto const start = vector[index * 2];
                    auto const end = vector[index * 2 + 1];
                    if (start == PCRE2_UNSET || end == PCRE2_UNSET)
                    {
                        result.captures.push_back({});
                    }
                    else
                    {
                        result.captures.push_back({ true,
                            static_cast<std::size_t>(start),
                            static_cast<std::size_t>(end - start) });
                    }
                }
            }
        }
        else if (match_count != PCRE2_ERROR_NOMATCH)
        {
            result.resource_limit_exceeded = IsResourceLimitError(match_count);
            result.invalid_utf16 = IsUtf16Error(match_count);
            if (match_count == PCRE2_ERROR_CALLOUT)
            {
                result.diagnostic = L"Regex match exceeded the interactive safety time budget.";
            }
            else if (result.resource_limit_exceeded)
            {
                result.diagnostic = L"Regex match exceeded a configured safety limit.";
            }
            else if (result.invalid_utf16)
            {
                result.diagnostic = L"Regex input contains invalid UTF-16.";
            }
            else
            {
                result.diagnostic = PcreMessage(match_count);
            }
        }

        pcre2_match_context_free(match_context);
        pcre2_match_data_free(match_data);
        return result;
    }
}

namespace winforge::core::regex
{
    SafeRegex::SafeRegex(std::shared_ptr<RegexProgram const> program)
        : m_program(std::move(program))
    {
    }

    RegexCompileResult SafeRegex::Compile(std::wstring_view pattern, RegexOptions options)
    {
        if (pattern.size() > options.max_pattern_length)
        {
            return { std::nullopt, { RegexErrorCode::PatternTooLong, pattern.size(),
                L"Regex patterns are limited to " + std::to_wstring(options.max_pattern_length)
                    + L" UTF-16 code units." } };
        }
        if (MaximumParenthesisNesting(pattern) > options.max_parenthesis_nesting)
        {
            return { std::nullopt, { RegexErrorCode::PatternNestingTooDeep, pattern.size(),
                L"Regex parenthesis nesting is limited to "
                    + std::to_wstring(options.max_parenthesis_nesting) + L" levels." } };
        }

        std::uint32_t compile_options = PCRE2_UTF | PCRE2_UCP | PCRE2_AUTO_CALLOUT
            | PCRE2_NEVER_BACKSLASH_C;
        if (!options.case_sensitive) compile_options |= PCRE2_CASELESS;
        if (options.multiline) compile_options |= PCRE2_MULTILINE;
        if (options.dot_matches_newline) compile_options |= PCRE2_DOTALL;

        int error_code{};
        PCRE2_SIZE error_offset{};
        auto* code = pcre2_compile(
            reinterpret_cast<PCRE2_SPTR>(pattern.data()),
            static_cast<PCRE2_SIZE>(pattern.size()),
            compile_options,
            &error_code,
            &error_offset,
            nullptr);
        if (!code)
        {
            auto diagnostic = RegexDiagnostic{
                RegexErrorCode::Syntax,
                static_cast<std::size_t>(error_offset),
                PcreMessage(error_code) };
            if (error_code == PCRE2_ERROR_BACKSLASH_C_CALLER_DISABLED)
            {
                diagnostic.code = RegexErrorCode::UnsupportedFeature;
                diagnostic.message = L"\\C is disabled because it is unsafe for UTF-16 search.";
            }
            return { std::nullopt, std::move(diagnostic) };
        }

        PCRE2_SIZE code_size{};
        if (pcre2_pattern_info(code, PCRE2_INFO_SIZE, &code_size) != 0
            || code_size > options.max_compiled_code_bytes)
        {
            pcre2_code_free(code);
            return { std::nullopt, { RegexErrorCode::CompiledCodeTooLarge, 0,
                L"Compiled regex exceeds the native safety budget." } };
        }

        try
        {
            auto program = std::shared_ptr<RegexProgram>(new RegexProgram{ options, code });
            return { SafeRegex(std::move(program)), {} };
        }
        catch (std::bad_alloc const&)
        {
            pcre2_code_free(code);
            return { std::nullopt, { RegexErrorCode::TooComplex, 0,
                L"Native regex memory allocation failed." } };
        }
    }

    bool SafeRegex::Valid() const noexcept
    {
        return m_program && m_program->code;
    }

    RegexMatchResult SafeRegex::Search(std::wstring_view text, bool include_captures) const
    {
        if (!Valid())
        {
            return { false, false, false, false, {}, L"Regex was not compiled." };
        }
        return RunMatch(*m_program, text, false, include_captures);
    }

    RegexMatchResult SafeRegex::FullMatch(std::wstring_view text, bool include_captures) const
    {
        if (!Valid())
        {
            return { false, false, false, false, {}, L"Regex was not compiled." };
        }
        return RunMatch(*m_program, text, true, include_captures);
    }

    std::wstring EscapeRegexLiteral(std::wstring_view value)
    {
        std::wstring escaped;
        escaped.reserve(value.size() * 2);
        for (auto const character : value)
        {
            if (std::wstring_view(L"\\.^$|?*+()[]{}").find(character) != std::wstring_view::npos)
            {
                escaped.push_back(L'\\');
            }
            escaped.push_back(character);
        }
        return escaped;
    }

    std::wstring BuildRegexCharacterClass(std::wstring_view characters, bool negate)
    {
        std::wstring result = negate ? L"[^" : L"[";
        for (auto const character : characters)
        {
            if (character == L'\\' || character == L']' || character == L'^' || character == L'-')
            {
                result.push_back(L'\\');
            }
            result.push_back(character);
        }
        result.push_back(L']');
        return result;
    }
}
