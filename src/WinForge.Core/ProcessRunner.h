#pragma once

#include <chrono>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <stop_token>
#include <string>
#include <string_view>
#include <vector>

namespace winforge::core
{
    struct EnvironmentOverride
    {
        std::wstring name;
        std::optional<std::wstring> value;
    };

    struct ProcessOptions
    {
        std::wstring executable;
        std::vector<std::wstring> arguments;
        std::optional<std::wstring> workingDirectory;
        bool inheritEnvironment{ true };
        std::vector<EnvironmentOverride> environmentOverrides;
        std::chrono::milliseconds timeout{ std::chrono::milliseconds::max() };
        // Aggregate retained bytes across stdout and stderr.
        std::size_t maximumOutputBytes{ 16u * 1024u * 1024u };
        std::stop_token cancellationToken{};
    };

    struct ProcessResult
    {
        std::optional<std::uint32_t> exitCode;
        std::wstring standardOutput;
        std::wstring standardError;
        bool timedOut{ false };
        bool cancelled{ false };
        bool outputLimitExceeded{ false };
    };

    // Implements the quoting convention consumed by CommandLineToArgvW and the
    // Microsoft C/C++ runtime. The returned value is one complete argv token.
    std::wstring QuoteWindowsArgument(std::wstring_view argument);
    std::wstring BuildWindowsCommandLine(
        std::wstring_view executable,
        std::vector<std::wstring> const& arguments);

    class ProcessRunner final
    {
    public:
        // Runs only the supplied executable. No shell is inferred or inserted.
        // Win32 setup failures are reported as std::system_error.
        static ProcessResult Run(ProcessOptions const& options);
    };
}
