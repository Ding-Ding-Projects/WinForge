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

    [[nodiscard]] std::vector<std::wstring> CaptureNames(
        RegexProgram const& program,
        std::uint32_t groupCount)
    {
        std::vector<std::wstring> names(groupCount);
        std::uint32_t nameCount{};
        std::uint32_t entrySize{};
        PCRE2_SPTR nameTable{};
        if (pcre2_pattern_info(program.code, PCRE2_INFO_NAMECOUNT, &nameCount) != 0
            || nameCount == 0
            || pcre2_pattern_info(program.code, PCRE2_INFO_NAMEENTRYSIZE, &entrySize) != 0
            || entrySize < 2
            || pcre2_pattern_info(program.code, PCRE2_INFO_NAMETABLE, &nameTable) != 0
            || !nameTable)
        {
            return names;
        }

        for (std::uint32_t entryIndex = 0; entryIndex < nameCount; ++entryIndex)
        {
            auto const* entry = nameTable + (static_cast<std::size_t>(entryIndex) * entrySize);
            // PCRE2's 16-bit name table stores the group number in its first
            // code unit (unlike the two-byte prefix in the 8-bit library).
            auto const groupIndex = static_cast<std::uint32_t>(entry[0]);
            if (groupIndex >= groupCount || !names[groupIndex].empty())
            {
                continue;
            }

            std::wstring name;
            for (std::uint32_t characterIndex = 1;
                 characterIndex < entrySize && entry[characterIndex] != 0;
                 ++characterIndex)
            {
                name.push_back(static_cast<wchar_t>(entry[characterIndex]));
            }
            names[groupIndex] = std::move(name);
        }
        return names;
    }

    void PopulateCaptures(
        std::vector<RegexCapture>& captures,
        PCRE2_SIZE const* vector,
        std::uint32_t groupCount,
        std::vector<std::wstring> const& names)
    {
        captures.reserve(groupCount);
        for (std::uint32_t index = 0; index < groupCount; ++index)
        {
            RegexCapture capture;
            if (index < names.size())
            {
                capture.name = names[index];
            }

            auto const start = vector[index * 2];
            auto const end = vector[index * 2 + 1];
            if (start != PCRE2_UNSET && end != PCRE2_UNSET)
            {
                capture.matched = true;
                capture.start = static_cast<std::size_t>(start);
                capture.length = static_cast<std::size_t>(end - start);
            }
            captures.push_back(std::move(capture));
        }
    }

    [[nodiscard]] std::size_t AdvanceUtf16Offset(
        std::wstring_view text,
        std::size_t offset) noexcept
    {
        if (offset >= text.size())
        {
            return text.size();
        }
        if (text[offset] == L'\r' && offset + 1 < text.size() && text[offset + 1] == L'\n')
        {
            return offset + 2;
        }
        auto const isHighSurrogate = text[offset] >= 0xd800 && text[offset] <= 0xdbff;
        auto const hasLowSurrogate = offset + 1 < text.size()
            && text[offset + 1] >= 0xdc00 && text[offset + 1] <= 0xdfff;
        return isHighSurrogate && hasLowSurrogate ? offset + 2 : offset + 1;
    }

    void SetFindAllFailure(RegexFindAllResult& result, int code)
    {
        result.resource_limit_exceeded = IsResourceLimitError(code);
        result.invalid_utf16 = IsUtf16Error(code);
        if (code == PCRE2_ERROR_CALLOUT)
        {
            result.diagnostic = L"Regex match set exceeded the interactive safety time budget.";
        }
        else if (result.resource_limit_exceeded)
        {
            result.diagnostic = L"Regex match set exceeded a configured safety limit.";
        }
        else if (result.invalid_utf16)
        {
            result.diagnostic = L"Regex input contains invalid UTF-16.";
        }
        else
        {
            result.diagnostic = PcreMessage(code);
        }
    }

    [[nodiscard]] bool AppendWithinLimit(
        std::wstring& destination,
        std::wstring_view value,
        std::size_t maximum)
    {
        if (value.size() > maximum || destination.size() > maximum - value.size())
        {
            return false;
        }
        destination.append(value);
        return true;
    }

    [[nodiscard]] bool ExpandReplacement(
        RegexOccurrence const& occurrence,
        std::wstring_view subject,
        std::wstring_view replacement,
        std::size_t maximumOutput,
        std::wstring& expanded,
        bool& outputLimitExceeded,
        std::wstring& diagnostic)
    {
        auto appendCapture = [&](RegexCapture const& capture)
        {
            if (!capture.matched)
            {
                return true;
            }
            if (!AppendWithinLimit(expanded,
                    subject.substr(capture.start, capture.length),
                    maximumOutput))
            {
                outputLimitExceeded = true;
                diagnostic = L"Replacement preview exceeded the configured output safety limit.";
                return false;
            }
            return true;
        };

        for (std::size_t index = 0; index < replacement.size(); ++index)
        {
            auto const character = replacement[index];
            if (character != L'$')
            {
                if (!AppendWithinLimit(expanded, replacement.substr(index, 1), maximumOutput))
                {
                    outputLimitExceeded = true;
                    diagnostic = L"Replacement preview exceeded the configured output safety limit.";
                    return false;
                }
                continue;
            }

            if (++index >= replacement.size())
            {
                diagnostic = L"Replacement ends with an incomplete $ token.";
                return false;
            }
            auto const marker = replacement[index];
            if (marker == L'$')
            {
                if (!AppendWithinLimit(expanded, L"$", maximumOutput))
                {
                    outputLimitExceeded = true;
                    diagnostic = L"Replacement preview exceeded the configured output safety limit.";
                    return false;
                }
                continue;
            }
            if (marker >= L'0' && marker <= L'9')
            {
                std::size_t groupNumber = static_cast<std::size_t>(marker - L'0');
                while (index + 1 < replacement.size()
                    && replacement[index + 1] >= L'0'
                    && replacement[index + 1] <= L'9')
                {
                    auto const digit = static_cast<std::size_t>(replacement[++index] - L'0');
                    if (groupNumber > 9 || (groupNumber == 9 && digit > 9))
                    {
                        diagnostic = L"Replacement numbered captures are limited to $0 through $99.";
                        return false;
                    }
                    groupNumber = groupNumber * 10 + digit;
                }
                if (groupNumber >= occurrence.captures.size())
                {
                    diagnostic = L"Replacement refers to a numbered capture that does not exist.";
                    return false;
                }
                if (!appendCapture(occurrence.captures[groupNumber]))
                {
                    return false;
                }
                continue;
            }
            if (marker == L'{')
            {
                auto const nameStart = index + 1;
                auto const close = replacement.find(L'}', nameStart);
                if (close == std::wstring_view::npos || close == nameStart)
                {
                    diagnostic = L"Replacement named captures must use ${name}.";
                    return false;
                }
                auto const name = replacement.substr(nameStart, close - nameStart);
                auto const found = std::find_if(
                    occurrence.captures.begin(),
                    occurrence.captures.end(),
                    [name](RegexCapture const& capture) { return capture.name == name; });
                if (found == occurrence.captures.end())
                {
                    diagnostic = L"Replacement refers to a named capture that does not exist.";
                    return false;
                }
                if (!appendCapture(*found))
                {
                    return false;
                }
                index = close;
                continue;
            }

            diagnostic = L"Replacement supports only $$, $0 through $99, and ${name}.";
            return false;
        }
        return true;
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
                PopulateCaptures(result.captures, vector, groups, CaptureNames(program, groups));
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
        if (options.ignore_pattern_whitespace) compile_options |= PCRE2_EXTENDED;
        if (options.explicit_capture) compile_options |= PCRE2_NO_AUTO_CAPTURE;

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

    RegexFindAllResult SafeRegex::FindAll(std::wstring_view text, bool include_captures) const
    {
        RegexFindAllResult result;
        if (!Valid())
        {
            result.diagnostic = L"Regex was not compiled.";
            return result;
        }
        if (text.size() > m_program->options.max_input_length)
        {
            result.input_limit_exceeded = true;
            result.diagnostic = L"Regex input exceeded the safety limit of "
                + std::to_wstring(m_program->options.max_input_length) + L" UTF-16 code units.";
            return result;
        }

        auto* matchData = pcre2_match_data_create_from_pattern(m_program->code, nullptr);
        auto* matchContext = pcre2_match_context_create(nullptr);
        if (!matchData || !matchContext)
        {
            if (matchData) pcre2_match_data_free(matchData);
            if (matchContext) pcre2_match_context_free(matchContext);
            result.resource_limit_exceeded = true;
            result.diagnostic = L"Native regex resources could not be allocated.";
            return result;
        }

        try
        {
            pcre2_set_match_limit(matchContext, m_program->options.match_limit);
            pcre2_set_depth_limit(matchContext, m_program->options.depth_limit);
            pcre2_set_heap_limit(matchContext, m_program->options.heap_limit_kib);
            MatchDeadline deadline{
                std::chrono::steady_clock::now()
                    + std::chrono::milliseconds(m_program->options.match_timeout_ms) };
            pcre2_set_callout(matchContext, PcreTimeoutCallout, &deadline);

            auto const subject = reinterpret_cast<PCRE2_SPTR>(text.data());
            auto const groupCount = pcre2_get_ovector_count(matchData);
            auto const captureNames = include_captures
                ? CaptureNames(*m_program, groupCount)
                : std::vector<std::wstring>{};
            PCRE2_SIZE offset{};
            bool retryNonEmptyAtOffset{};

            while (offset <= static_cast<PCRE2_SIZE>(text.size()))
            {
                if (std::chrono::steady_clock::now() >= deadline.deadline)
                {
                    result.resource_limit_exceeded = true;
                    result.diagnostic = L"Regex match set exceeded the interactive safety time budget.";
                    break;
                }

                auto const flags = retryNonEmptyAtOffset
                    ? PCRE2_NOTEMPTY_ATSTART | PCRE2_ANCHORED
                    : 0u;
                auto const matchCount = pcre2_match(
                    m_program->code,
                    subject,
                    static_cast<PCRE2_SIZE>(text.size()),
                    offset,
                    flags,
                    matchData,
                    matchContext);
                if (matchCount == PCRE2_ERROR_NOMATCH)
                {
                    if (!retryNonEmptyAtOffset)
                    {
                        break;
                    }
                    retryNonEmptyAtOffset = false;
                    if (offset >= static_cast<PCRE2_SIZE>(text.size()))
                    {
                        break;
                    }
                    offset = static_cast<PCRE2_SIZE>(AdvanceUtf16Offset(
                        text,
                        static_cast<std::size_t>(offset)));
                    continue;
                }
                if (matchCount < 0)
                {
                    SetFindAllFailure(result, matchCount);
                    break;
                }
                if (result.matches.size() >= m_program->options.max_result_count)
                {
                    result.result_limit_exceeded = true;
                    result.diagnostic = L"Regex match set exceeded the configured result safety limit of "
                        + std::to_wstring(m_program->options.max_result_count) + L" matches.";
                    break;
                }

                auto const* vector = pcre2_get_ovector_pointer(matchData);
                RegexOccurrence occurrence;
                occurrence.start = static_cast<std::size_t>(vector[0]);
                occurrence.length = static_cast<std::size_t>(vector[1] - vector[0]);
                if (include_captures)
                {
                    PopulateCaptures(occurrence.captures, vector, groupCount, captureNames);
                }
                result.matches.push_back(std::move(occurrence));

                auto const matchStart = vector[0];
                auto const matchEnd = vector[1];
                if (matchEnd > matchStart)
                {
                    offset = matchEnd;
                    retryNonEmptyAtOffset = false;
                }
                else
                {
                    // This mirrors PCRE2's documented global-search progression:
                    // after an empty match, retry once for a non-empty anchored
                    // alternative, then advance one UTF-16 character/CRLF pair.
                    offset = matchEnd;
                    retryNonEmptyAtOffset = true;
                }
            }
        }
        catch (std::bad_alloc const&)
        {
            result.matches.clear();
            result.resource_limit_exceeded = true;
            result.diagnostic = L"Native regex match-set allocation failed.";
        }

        pcre2_match_context_free(matchContext);
        pcre2_match_data_free(matchData);
        return result;
    }

    RegexReplaceResult SafeRegex::ReplaceAll(
        std::wstring_view text,
        std::wstring_view replacement) const
    {
        RegexReplaceResult result;
        if (!Valid())
        {
            result.diagnostic = L"Regex was not compiled.";
            return result;
        }
        auto const matches = FindAll(text, true);
        result.input_limit_exceeded = matches.input_limit_exceeded;
        result.resource_limit_exceeded = matches.resource_limit_exceeded;
        result.invalid_utf16 = matches.invalid_utf16;
        result.result_limit_exceeded = matches.result_limit_exceeded;
        if (matches.input_limit_exceeded || matches.resource_limit_exceeded
            || matches.invalid_utf16 || matches.result_limit_exceeded)
        {
            result.diagnostic = matches.diagnostic;
            return result;
        }

        try
        {
            std::wstring output;
            auto const maximumOutput = m_program->options.max_replacement_output_length;
            output.reserve(std::min(text.size(), maximumOutput));
            std::size_t cursor{};
            for (auto const& occurrence : matches.matches)
            {
                if (!AppendWithinLimit(output,
                        text.substr(cursor, occurrence.start - cursor),
                        maximumOutput))
                {
                    result.output_limit_exceeded = true;
                    result.diagnostic = L"Replacement preview exceeded the configured output safety limit.";
                    return result;
                }

                std::wstring expanded;
                bool expansionOutputLimit{};
                std::wstring expansionDiagnostic;
                if (!ExpandReplacement(
                        occurrence,
                        text,
                        replacement,
                        maximumOutput - output.size(),
                        expanded,
                        expansionOutputLimit,
                        expansionDiagnostic))
                {
                    result.output_limit_exceeded = expansionOutputLimit;
                    result.invalid_replacement = !expansionOutputLimit;
                    result.diagnostic = std::move(expansionDiagnostic);
                    return result;
                }
                if (!AppendWithinLimit(output, expanded, maximumOutput))
                {
                    result.output_limit_exceeded = true;
                    result.diagnostic = L"Replacement preview exceeded the configured output safety limit.";
                    return result;
                }
                cursor = occurrence.start + occurrence.length;
                ++result.substitutions;
            }
            if (!AppendWithinLimit(output, text.substr(cursor), maximumOutput))
            {
                result.output_limit_exceeded = true;
                result.diagnostic = L"Replacement preview exceeded the configured output safety limit.";
                return result;
            }
            result.output = std::move(output);
            return result;
        }
        catch (std::bad_alloc const&)
        {
            result.resource_limit_exceeded = true;
            result.diagnostic = L"Native regex replacement preview allocation failed.";
            return result;
        }
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
