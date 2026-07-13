#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "ProcessRunnerTests.h"

#include "ProcessRunner.h"

#include <windows.h>

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <filesystem>
#include <stdexcept>
#include <stop_token>
#include <string>
#include <thread>
#include <vector>

namespace winforge::tests
{
    namespace
    {
        constexpr wchar_t HelperSwitch[] = L"--process-runner-helper";

        std::wstring CurrentExecutable()
        {
            std::wstring path(260, L'\0');
            for (;;)
            {
                auto const copied = GetModuleFileNameW(
                    nullptr, path.data(), static_cast<DWORD>(path.size()));
                if (copied == 0)
                {
                    throw std::runtime_error("GetModuleFileNameW failed");
                }
                if (copied < path.size() - 1)
                {
                    path.resize(copied);
                    return path;
                }
                path.resize(path.size() * 2);
            }
        }

        std::string ToUtf8(std::wstring_view value)
        {
            if (value.empty())
            {
                return {};
            }
            auto const required = WideCharToMultiByte(
                CP_UTF8, WC_ERR_INVALID_CHARS,
                value.data(), static_cast<int>(value.size()),
                nullptr, 0, nullptr, nullptr);
            if (required == 0)
            {
                throw std::runtime_error("WideCharToMultiByte failed");
            }
            std::string bytes(required, '\0');
            if (WideCharToMultiByte(
                CP_UTF8, WC_ERR_INVALID_CHARS,
                value.data(), static_cast<int>(value.size()),
                bytes.data(), required, nullptr, nullptr) == 0)
            {
                throw std::runtime_error("WideCharToMultiByte failed");
            }
            return bytes;
        }

        void WriteBytes(HANDLE stream, std::string const& bytes)
        {
            std::size_t offset = 0;
            while (offset < bytes.size())
            {
                DWORD written = 0;
                auto const remaining = static_cast<DWORD>(
                    (std::min)(bytes.size() - offset, static_cast<std::size_t>(MAXDWORD)));
                if (!WriteFile(stream, bytes.data() + offset, remaining, &written, nullptr))
                {
                    return;
                }
                offset += written;
            }
        }

        void WriteUtf8(HANDLE stream, std::wstring_view value)
        {
            WriteBytes(stream, ToUtf8(value));
        }

        winforge::core::ProcessResult RunHelper(
            std::wstring_view mode,
            std::vector<std::wstring> arguments = {},
            std::chrono::milliseconds timeout = std::chrono::seconds(10),
            std::stop_token cancellationToken = {})
        {
            winforge::core::ProcessOptions options;
            options.executable = CurrentExecutable();
            options.arguments = { HelperSwitch, std::wstring{ mode } };
            options.arguments.insert(options.arguments.end(), arguments.begin(), arguments.end());
            options.timeout = timeout;
            options.cancellationToken = cancellationToken;
            return winforge::core::ProcessRunner::Run(options);
        }

        int HoldNamedEvent(wchar_t const* name)
        {
            auto const event = CreateEventW(nullptr, TRUE, FALSE, name);
            if (event == nullptr)
            {
                return 81;
            }
            Sleep(10'000);
            CloseHandle(event);
            return 0;
        }

        int SpawnEventHoldingChild(wchar_t const* name)
        {
            auto const executable = CurrentExecutable();
            auto commandLineText = winforge::core::BuildWindowsCommandLine(
                executable, { HelperSwitch, L"hold-event", name });
            std::vector<wchar_t> commandLine(commandLineText.begin(), commandLineText.end());
            commandLine.push_back(L'\0');

            STARTUPINFOW startup{};
            startup.cb = sizeof(startup);
            PROCESS_INFORMATION process{};
            if (!CreateProcessW(
                executable.c_str(), commandLine.data(), nullptr, nullptr, FALSE,
                CREATE_NO_WINDOW, nullptr, nullptr, &startup, &process))
            {
                return 82;
            }
            CloseHandle(process.hThread);
            CloseHandle(process.hProcess);

            for (int attempt = 0; attempt < 100; ++attempt)
            {
                auto const event = OpenEventW(SYNCHRONIZE, FALSE, name);
                if (event != nullptr)
                {
                    CloseHandle(event);
                    WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), L"child-ready\n");
                    return 0;
                }
                Sleep(20);
            }
            WriteUtf8(GetStdHandle(STD_ERROR_HANDLE), L"child-not-ready\n");
            return 83;
        }
    }

    int TryRunProcessRunnerHelper(int argc, wchar_t** argv)
    {
        if (argc < 3 || std::wstring_view{ argv[1] } != HelperSwitch)
        {
            return -1;
        }

        auto const mode = std::wstring_view{ argv[2] };
        if (mode == L"verify-arguments")
        {
            std::vector<std::wstring> const expected{
                L"", L"plain", L"two words", L"quote\"inside",
                L"trailing slash\\", L"quoted trailing\\\\",
                L"slashes\\\\\"quote", L"tab\tvalue", L"粵語"
            };
            if (argc != static_cast<int>(expected.size()) + 3)
            {
                return 71;
            }
            for (std::size_t index = 0; index < expected.size(); ++index)
            {
                if (argv[index + 3] != expected[index])
                {
                    return 72;
                }
            }
            WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), L"arguments-ok\n");
            return 0;
        }
        if (mode == L"streams")
        {
            WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), L"standard output\n");
            WriteUtf8(GetStdHandle(STD_ERROR_HANDLE), L"standard error\n");
            return 0;
        }
        if (mode == L"nonzero")
        {
            return 37;
        }
        if (mode == L"unicode")
        {
            WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), L"粵語輸出 — café 🚀\n");
            return 0;
        }
        if (mode == L"sleep")
        {
            Sleep(10'000);
            return 0;
        }
        if (mode == L"environment-and-directory" && argc == 4)
        {
            std::wstring value(32'768, L'\0');
            auto const valueLength = GetEnvironmentVariableW(
                argv[3], value.data(), static_cast<DWORD>(value.size()));
            if (valueLength == 0 || valueLength >= value.size())
            {
                return 73;
            }
            value.resize(valueLength);

            std::wstring directory(32'768, L'\0');
            auto const directoryLength = GetCurrentDirectoryW(
                static_cast<DWORD>(directory.size()), directory.data());
            if (directoryLength == 0 || directoryLength >= directory.size())
            {
                return 74;
            }
            directory.resize(directoryLength);
            WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), value + L"\n" + directory + L"\n");
            return 0;
        }
        if (mode == L"probe-handle" && argc == 4)
        {
            auto const raw = _wcstoui64(argv[3], nullptr, 10);
            auto const candidate = reinterpret_cast<HANDLE>(static_cast<std::uintptr_t>(raw));
            if (SetEvent(candidate))
            {
                WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), L"inherited\n");
                return 75;
            }
            WriteUtf8(GetStdHandle(STD_OUTPUT_HANDLE), L"not-inherited\n");
            return 0;
        }
        if (mode == L"hold-event" && argc == 4)
        {
            return HoldNamedEvent(argv[3]);
        }
        if (mode == L"spawn-child" && argc == 4)
        {
            return SpawnEventHoldingChild(argv[3]);
        }
        return 79;
    }

    void RunProcessRunnerTests(Expectation expect)
    {
        using namespace std::chrono_literals;
        using winforge::core::ProcessOptions;
        using winforge::core::ProcessRunner;
        using winforge::core::QuoteWindowsArgument;

        expect(QuoteWindowsArgument(L"") == L"\"\"", "quotes an empty argument");
        expect(QuoteWindowsArgument(L"plain") == L"plain", "leaves a plain argument unquoted");
        expect(QuoteWindowsArgument(L"two words") == L"\"two words\"", "quotes argument whitespace");
        expect(QuoteWindowsArgument(L"a\"b") == L"\"a\\\"b\"", "escapes an embedded quote");
        expect(
            QuoteWindowsArgument(L"space slash\\") == L"\"space slash\\\\\"",
            "doubles trailing slashes inside quotes");

        std::vector<std::wstring> const trickyArguments{
            L"", L"plain", L"two words", L"quote\"inside",
            L"trailing slash\\", L"quoted trailing\\\\",
            L"slashes\\\\\"quote", L"tab\tvalue", L"粵語"
        };
        auto result = RunHelper(L"verify-arguments", trickyArguments);
        expect(result.exitCode == std::uint32_t{ 0 } && result.standardOutput == L"arguments-ok\n",
            "round-trips argv-safe quoted arguments");

        result = RunHelper(L"streams");
        expect(result.exitCode == std::uint32_t{ 0 }, "reports a successful exit code");
        expect(result.standardOutput == L"standard output\n", "captures stdout");
        expect(result.standardError == L"standard error\n", "captures stderr");

        result = RunHelper(L"nonzero");
        expect(result.exitCode == std::uint32_t{ 37 }, "reports a nonzero exit code");

        result = RunHelper(L"unicode");
        expect(result.standardOutput == L"粵語輸出 — café 🚀\n", "decodes UTF-8 Unicode output");

        auto const temporaryDirectory = std::filesystem::temp_directory_path().wstring();
        ProcessOptions environmentOptions;
        environmentOptions.executable = CurrentExecutable();
        environmentOptions.arguments = {
            HelperSwitch, L"environment-and-directory", L"WINFORGE_PROCESS_RUNNER_TEST"
        };
        environmentOptions.workingDirectory = temporaryDirectory;
        environmentOptions.inheritEnvironment = false;
        environmentOptions.environmentOverrides.push_back({
            L"WINFORGE_PROCESS_RUNNER_TEST", L"環境值"
        });
        environmentOptions.timeout = 10s;
        result = ProcessRunner::Run(environmentOptions);
        expect(result.exitCode == std::uint32_t{ 0 },
            "runs with a custom environment and working directory");
        constexpr std::wstring_view environmentPrefix = L"環境值\n";
        auto const hasExpectedEnvironment = result.standardOutput.starts_with(environmentPrefix) &&
            result.standardOutput.ends_with(L"\n");
        std::error_code pathError;
        auto const reportedDirectory = hasExpectedEnvironment
            ? result.standardOutput.substr(
                environmentPrefix.size(),
                result.standardOutput.size() - environmentPrefix.size() - 1)
            : std::wstring{};
        auto const hasExpectedDirectory = hasExpectedEnvironment && std::filesystem::equivalent(
            std::filesystem::path{ reportedDirectory },
            std::filesystem::path{ temporaryDirectory }, pathError);
        expect(hasExpectedEnvironment && hasExpectedDirectory && !pathError,
            "passes environment overrides and working directory");

        result = RunHelper(L"sleep", {}, 250ms);
        expect(result.timedOut && !result.cancelled, "marks a process timeout");
        expect(result.exitCode == static_cast<std::uint32_t>(ERROR_TIMEOUT),
            "terminates a timed-out child with a timeout code");

        std::stop_source cancellation;
        std::jthread cancelSoon([&]
        {
            Sleep(150);
            cancellation.request_stop();
        });
        result = RunHelper(L"sleep", {}, 10s, cancellation.get_token());
        expect(result.cancelled && !result.timedOut, "marks a cancelled process");
        expect(result.exitCode == static_cast<std::uint32_t>(ERROR_CANCELLED),
            "terminates a cancelled child with a cancellation code");

        SECURITY_ATTRIBUTES inheritable{};
        inheritable.nLength = sizeof(inheritable);
        inheritable.bInheritHandle = TRUE;
        auto const unrelatedEvent = CreateEventW(&inheritable, TRUE, FALSE, nullptr);
        expect(unrelatedEvent != nullptr, "creates an unrelated inheritable test handle");
        if (unrelatedEvent != nullptr)
        {
            auto const numericHandle = std::to_wstring(
                reinterpret_cast<std::uintptr_t>(unrelatedEvent));
            result = RunHelper(L"probe-handle", { numericHandle });
            expect(result.exitCode == std::uint32_t{ 0 } &&
                result.standardOutput == L"not-inherited\n",
                "does not leak unrelated inheritable handles");
            expect(WaitForSingleObject(unrelatedEvent, 0) == WAIT_TIMEOUT,
                "child cannot signal the unrelated parent handle");
            expect(SetEvent(unrelatedEvent) && WaitForSingleObject(unrelatedEvent, 0) == WAIT_OBJECT_0,
                "unrelated parent handle remains valid");
            CloseHandle(unrelatedEvent);
        }

        auto const treeEventName = L"Local\\WinForge.ProcessRunner.Tree." +
            std::to_wstring(GetCurrentProcessId()) + L"." + std::to_wstring(GetTickCount64());
        result = RunHelper(L"spawn-child", { treeEventName }, 2s);
        expect(result.timedOut && result.exitCode == std::uint32_t{ 0 } &&
            result.standardOutput == L"child-ready\n",
            "keeps tracking descendants after the root process exits");
        bool treeEventReleased = false;
        for (int attempt = 0; attempt < 100; ++attempt)
        {
            auto const treeEvent = OpenEventW(SYNCHRONIZE, FALSE, treeEventName.c_str());
            if (treeEvent == nullptr)
            {
                treeEventReleased = true;
                break;
            }
            CloseHandle(treeEvent);
            Sleep(10);
        }
        expect(treeEventReleased, "job cleanup terminates the created process tree");
    }
}
