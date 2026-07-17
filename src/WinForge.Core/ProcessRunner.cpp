#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "ProcessRunner.h"

#include <windows.h>
#include <aclapi.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <cstddef>
#include <limits>
#include <map>
#include <memory>
#include <stdexcept>
#include <system_error>
#include <thread>
#include <utility>

namespace winforge::core
{
    namespace
    {
        class UniqueHandle final
        {
        public:
            UniqueHandle() noexcept = default;
            explicit UniqueHandle(HANDLE handle) noexcept : handle_(handle) {}
            ~UniqueHandle() { reset(); }

            UniqueHandle(UniqueHandle const&) = delete;
            UniqueHandle& operator=(UniqueHandle const&) = delete;

            UniqueHandle(UniqueHandle&& other) noexcept : handle_(other.release()) {}
            UniqueHandle& operator=(UniqueHandle&& other) noexcept
            {
                if (this != &other)
                {
                    reset(other.release());
                }
                return *this;
            }

            [[nodiscard]] HANDLE get() const noexcept { return handle_; }
            [[nodiscard]] explicit operator bool() const noexcept
            {
                return handle_ != nullptr && handle_ != INVALID_HANDLE_VALUE;
            }

            HANDLE release() noexcept
            {
                return std::exchange(handle_, nullptr);
            }

            void reset(HANDLE handle = nullptr) noexcept
            {
                if (*this)
                {
                    CloseHandle(handle_);
                }
                handle_ = handle;
            }

        private:
            HANDLE handle_{ nullptr };
        };

        class AttributeList final
        {
        public:
            explicit AttributeList(DWORD attributeCount)
            {
                SIZE_T bytes = 0;
                InitializeProcThreadAttributeList(nullptr, attributeCount, 0, &bytes);
                if (bytes == 0)
                {
                    ThrowLastError("InitializeProcThreadAttributeList size query failed");
                }

                storage_.resize(bytes);
                list_ = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(storage_.data());
                if (!InitializeProcThreadAttributeList(list_, attributeCount, 0, &bytes))
                {
                    list_ = nullptr;
                    ThrowLastError("InitializeProcThreadAttributeList failed");
                }
            }

            ~AttributeList()
            {
                if (list_ != nullptr)
                {
                    DeleteProcThreadAttributeList(list_);
                }
            }

            AttributeList(AttributeList const&) = delete;
            AttributeList& operator=(AttributeList const&) = delete;

            [[nodiscard]] LPPROC_THREAD_ATTRIBUTE_LIST get() const noexcept { return list_; }

        private:
            static void ThrowLastError(char const* operation)
            {
                auto const error = static_cast<int>(GetLastError());
                throw std::system_error(error, std::system_category(), operation);
            }

            std::vector<std::byte> storage_;
            LPPROC_THREAD_ATTRIBUTE_LIST list_{ nullptr };
        };

        struct EnvironmentStringsDeleter
        {
            void operator()(wchar_t* value) const noexcept
            {
                if (value != nullptr)
                {
                    FreeEnvironmentStringsW(value);
                }
            }
        };

        struct LocalFreeDeleter
        {
            void operator()(void* value) const noexcept
            {
                if (value != nullptr)
                {
                    LocalFree(value);
                }
            }
        };

        struct CaseInsensitiveLess
        {
            bool operator()(std::wstring const& left, std::wstring const& right) const noexcept
            {
                auto const comparison = CompareStringOrdinal(
                    left.data(), static_cast<int>(left.size()),
                    right.data(), static_cast<int>(right.size()), TRUE);
                return comparison == CSTR_LESS_THAN;
            }
        };

        [[noreturn]] void ThrowLastError(char const* operation)
        {
            auto const error = static_cast<int>(GetLastError());
            throw std::system_error(error, std::system_category(), operation);
        }

        void ValidateNoNull(std::wstring_view value, char const* field)
        {
            if (value.find(L'\0') != std::wstring_view::npos)
            {
                throw std::invalid_argument(std::string(field) + " contains an embedded null character");
            }
        }

        std::pair<UniqueHandle, UniqueHandle> CreateRedirectPipe()
        {
            HANDLE rawToken = nullptr;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken))
            {
                ThrowLastError("OpenProcessToken for pipe ACL failed");
            }
            UniqueHandle token{ rawToken };
            DWORD tokenBytes = 0;
            GetTokenInformation(rawToken, TokenUser, nullptr, 0, &tokenBytes);
            if (tokenBytes < sizeof(TOKEN_USER))
            {
                ThrowLastError("TokenUser size query for pipe ACL failed");
            }
            std::vector<std::byte> tokenStorage(tokenBytes);
            if (!GetTokenInformation(
                rawToken, TokenUser, tokenStorage.data(), tokenBytes, &tokenBytes))
            {
                ThrowLastError("TokenUser query for pipe ACL failed");
            }
            auto const* tokenUser = reinterpret_cast<TOKEN_USER const*>(tokenStorage.data());
            if (tokenUser->User.Sid == nullptr || !IsValidSid(tokenUser->User.Sid))
            {
                throw std::runtime_error("TokenUser returned an invalid SID for pipe ACL");
            }

            EXPLICIT_ACCESSW access{};
            access.grfAccessPermissions = GENERIC_ALL;
            access.grfAccessMode = SET_ACCESS;
            access.grfInheritance = NO_INHERITANCE;
            access.Trustee.TrusteeForm = TRUSTEE_IS_SID;
            access.Trustee.TrusteeType = TRUSTEE_IS_USER;
            access.Trustee.ptstrName = static_cast<LPWSTR>(tokenUser->User.Sid);
            PACL rawAcl = nullptr;
            auto const aclError = SetEntriesInAclW(1, &access, nullptr, &rawAcl);
            if (aclError != ERROR_SUCCESS)
            {
                throw std::system_error(
                    static_cast<int>(aclError), std::system_category(),
                    "SetEntriesInAcl for pipe failed");
            }
            std::unique_ptr<void, LocalFreeDeleter> acl{ rawAcl };

            SECURITY_DESCRIPTOR descriptor{};
            if (!InitializeSecurityDescriptor(&descriptor, SECURITY_DESCRIPTOR_REVISION) ||
                !SetSecurityDescriptorDacl(
                    &descriptor, TRUE, static_cast<PACL>(acl.get()), FALSE))
            {
                ThrowLastError("security descriptor setup for pipe failed");
            }
            SECURITY_ATTRIBUTES security{};
            security.nLength = sizeof(security);
            security.bInheritHandle = TRUE;
            security.lpSecurityDescriptor = &descriptor;

            HANDLE readHandle = nullptr;
            HANDLE writeHandle = nullptr;
            if (!CreatePipe(&readHandle, &writeHandle, &security, 0))
            {
                ThrowLastError("CreatePipe failed");
            }

            UniqueHandle read{ readHandle };
            UniqueHandle write{ writeHandle };
            if (!SetHandleInformation(read.get(), HANDLE_FLAG_INHERIT, 0))
            {
                ThrowLastError("SetHandleInformation failed");
            }
            return { std::move(read), std::move(write) };
        }

        UniqueHandle OpenNullInput()
        {
            SECURITY_ATTRIBUTES security{};
            security.nLength = sizeof(security);
            security.bInheritHandle = TRUE;
            UniqueHandle input{ CreateFileW(
                L"NUL",
                GENERIC_READ,
                FILE_SHARE_READ | FILE_SHARE_WRITE,
                &security,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr) };
            if (!input)
            {
                ThrowLastError("opening NUL for child stdin failed");
            }
            return input;
        }

        std::vector<wchar_t> BuildEnvironmentBlock(ProcessOptions const& options)
        {
            std::map<std::wstring, std::wstring, CaseInsensitiveLess> environment;
            if (options.inheritEnvironment)
            {
                std::unique_ptr<wchar_t, EnvironmentStringsDeleter> inherited{ GetEnvironmentStringsW() };
                if (!inherited)
                {
                    ThrowLastError("GetEnvironmentStringsW failed");
                }

                for (auto current = inherited.get(); *current != L'\0';)
                {
                    std::wstring entry{ current };
                    auto const searchFrom = entry.starts_with(L'=') ? 1u : 0u;
                    auto const separator = entry.find(L'=', searchFrom);
                    if (separator != std::wstring::npos)
                    {
                        environment.insert_or_assign(
                            entry.substr(0, separator),
                            entry.substr(separator + 1));
                    }
                    current += entry.size() + 1;
                }
            }

            for (auto const& change : options.environmentOverrides)
            {
                ValidateNoNull(change.name, "environment variable name");
                if (change.name.empty() || change.name.find(L'=') != std::wstring::npos)
                {
                    throw std::invalid_argument("environment variable names cannot be empty or contain '='");
                }

                if (change.value)
                {
                    ValidateNoNull(*change.value, "environment variable value");
                    environment.insert_or_assign(change.name, *change.value);
                }
                else
                {
                    environment.erase(change.name);
                }
            }

            std::vector<wchar_t> block;
            for (auto const& [name, value] : environment)
            {
                block.insert(block.end(), name.begin(), name.end());
                block.push_back(L'=');
                block.insert(block.end(), value.begin(), value.end());
                block.push_back(L'\0');
            }
            block.push_back(L'\0');
            if (environment.empty())
            {
                block.push_back(L'\0');
            }
            return block;
        }

        std::string ReadPipe(
            HANDLE pipe,
            std::size_t maximumBytes,
            std::atomic_size_t& capturedBytes,
            HANDLE outputLimitEvent)
        {
            std::string bytes;
            bytes.reserve((std::min)(maximumBytes, std::size_t{ 64 * 1024 }));
            std::array<char, 16 * 1024> buffer{};
            for (;;)
            {
                DWORD bytesRead = 0;
                if (!ReadFile(pipe, buffer.data(), static_cast<DWORD>(buffer.size()), &bytesRead, nullptr))
                {
                    auto const error = GetLastError();
                    if (error == ERROR_BROKEN_PIPE || error == ERROR_HANDLE_EOF)
                    {
                        break;
                    }
                    break;
                }
                if (bytesRead == 0)
                {
                    break;
                }
                auto accepted = std::size_t{};
                auto captured = capturedBytes.load(std::memory_order_relaxed);
                for (;;)
                {
                    auto const remaining = maximumBytes - (std::min)(captured, maximumBytes);
                    accepted = (std::min)(remaining, static_cast<std::size_t>(bytesRead));
                    if (capturedBytes.compare_exchange_weak(
                        captured,
                        captured + accepted,
                        std::memory_order_relaxed,
                        std::memory_order_relaxed))
                    {
                        break;
                    }
                }
                bytes.append(buffer.data(), accepted);
                if (accepted != bytesRead)
                {
                    SetEvent(outputLimitEvent);
                }
            }
            return bytes;
        }

        bool TryDecode(std::string const& bytes, UINT codePage, DWORD flags, std::wstring& decoded)
        {
            if (bytes.empty())
            {
                decoded.clear();
                return true;
            }
            if (bytes.size() > static_cast<std::size_t>(std::numeric_limits<int>::max()))
            {
                throw std::length_error("process output is too large to decode");
            }

            auto const inputLength = static_cast<int>(bytes.size());
            auto const outputLength = MultiByteToWideChar(
                codePage, flags, bytes.data(), inputLength, nullptr, 0);
            if (outputLength == 0)
            {
                return false;
            }

            decoded.resize(outputLength);
            return MultiByteToWideChar(
                codePage, flags, bytes.data(), inputLength,
                decoded.data(), outputLength) != 0;
        }

        std::wstring DecodeOutput(std::string const& bytes)
        {
            std::wstring decoded;
            if (TryDecode(bytes, CP_UTF8, MB_ERR_INVALID_CHARS, decoded))
            {
                return decoded;
            }

            std::array<UINT, 3> const fallbackCodePages{
                GetConsoleOutputCP(), GetOEMCP(), GetACP()
            };
            for (auto const codePage : fallbackCodePages)
            {
                if (codePage != 0 && codePage != CP_UTF8 && TryDecode(bytes, codePage, 0, decoded))
                {
                    return decoded;
                }
            }

            decoded.assign(bytes.begin(), bytes.end());
            return decoded;
        }

        DWORD WaitDuration(
            std::chrono::steady_clock::time_point deadline,
            bool hasDeadline)
        {
            if (!hasDeadline)
            {
                return INFINITE;
            }

            auto const now = std::chrono::steady_clock::now();
            if (now >= deadline)
            {
                return 0;
            }
            auto const remaining = std::chrono::duration_cast<std::chrono::milliseconds>(deadline - now);
            constexpr auto maximumFiniteWait = static_cast<long long>(INFINITE) - 1;
            if (remaining.count() >= maximumFiniteWait)
            {
                return static_cast<DWORD>(maximumFiniteWait);
            }
            return static_cast<DWORD>(remaining.count() + 1);
        }

        bool TryGetActiveProcessCount(HANDLE job, DWORD& activeProcesses, DWORD& error) noexcept
        {
            JOBOBJECT_BASIC_ACCOUNTING_INFORMATION accounting{};
            if (!QueryInformationJobObject(
                job, JobObjectBasicAccountingInformation,
                &accounting, sizeof(accounting), nullptr))
            {
                error = GetLastError();
                return false;
            }
            error = ERROR_SUCCESS;
            activeProcesses = accounting.ActiveProcesses;
            return true;
        }

        bool WaitForJobToEmpty(HANDLE job, DWORD& error) noexcept
        {
            for (;;)
            {
                DWORD activeProcesses = 0;
                if (!TryGetActiveProcessCount(job, activeProcesses, error))
                {
                    return false;
                }
                if (activeProcesses == 0)
                {
                    return true;
                }
                Sleep(1);
            }
        }
    }

    std::wstring QuoteWindowsArgument(std::wstring_view argument)
    {
        ValidateNoNull(argument, "process argument");
        if (!argument.empty() && argument.find_first_of(L" \t\"") == std::wstring_view::npos)
        {
            return std::wstring{ argument };
        }

        std::wstring quoted;
        quoted.push_back(L'"');
        std::size_t backslashes = 0;
        for (auto const character : argument)
        {
            if (character == L'\\')
            {
                ++backslashes;
                continue;
            }

            if (character == L'"')
            {
                quoted.append(backslashes * 2 + 1, L'\\');
                quoted.push_back(L'"');
            }
            else
            {
                quoted.append(backslashes, L'\\');
                quoted.push_back(character);
            }
            backslashes = 0;
        }
        quoted.append(backslashes * 2, L'\\');
        quoted.push_back(L'"');
        return quoted;
    }

    std::wstring BuildWindowsCommandLine(
        std::wstring_view executable,
        std::vector<std::wstring> const& arguments)
    {
        ValidateNoNull(executable, "process executable");
        if (executable.empty())
        {
            throw std::invalid_argument("process executable cannot be empty");
        }

        auto commandLine = QuoteWindowsArgument(executable);
        for (auto const& argument : arguments)
        {
            commandLine.push_back(L' ');
            commandLine.append(QuoteWindowsArgument(argument));
        }
        if (commandLine.size() >= 32'767)
        {
            throw std::length_error("process command line exceeds the Windows limit");
        }
        return commandLine;
    }

    ProcessResult ProcessRunner::Run(ProcessOptions const& options)
    {
        if (options.cancellationToken.stop_requested())
        {
            ProcessResult result;
            result.cancelled = true;
            return result;
        }
        if (options.timeout < std::chrono::milliseconds::zero())
        {
            throw std::invalid_argument("process timeout cannot be negative");
        }
        if (options.maximumOutputBytes == 0)
        {
            throw std::invalid_argument("process output limit must be positive");
        }
        if (options.workingDirectory)
        {
            ValidateNoNull(*options.workingDirectory, "working directory");
        }

        auto commandLineText = BuildWindowsCommandLine(options.executable, options.arguments);
        std::vector<wchar_t> commandLine(commandLineText.begin(), commandLineText.end());
        commandLine.push_back(L'\0');

        auto [stdoutRead, stdoutWrite] = CreateRedirectPipe();
        auto [stderrRead, stderrWrite] = CreateRedirectPipe();
        auto stdinRead = OpenNullInput();

        UniqueHandle cancellationEvent{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
        if (!cancellationEvent)
        {
            ThrowLastError("CreateEventW failed");
        }
        UniqueHandle outputLimitEvent{ CreateEventW(nullptr, TRUE, FALSE, nullptr) };
        if (!outputLimitEvent)
        {
            ThrowLastError("CreateEventW for process output limit failed");
        }
        std::stop_callback cancellationCallback(options.cancellationToken, [&cancellationEvent]
        {
            SetEvent(cancellationEvent.get());
        });

        UniqueHandle job{ CreateJobObjectW(nullptr, nullptr) };
        if (!job)
        {
            ThrowLastError("CreateJobObjectW failed");
        }
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION jobLimits{};
        jobLimits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        if (!SetInformationJobObject(
            job.get(), JobObjectExtendedLimitInformation,
            &jobLimits, sizeof(jobLimits)))
        {
            ThrowLastError("SetInformationJobObject failed");
        }

        AttributeList attributes{ 1 };
        std::array<HANDLE, 3> inheritedHandles{
            stdinRead.get(), stdoutWrite.get(), stderrWrite.get()
        };
        if (!UpdateProcThreadAttribute(
            attributes.get(), 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
            inheritedHandles.data(), sizeof(inheritedHandles), nullptr, nullptr))
        {
            ThrowLastError("UpdateProcThreadAttribute handle list failed");
        }

        STARTUPINFOEXW startup{};
        startup.StartupInfo.cb = sizeof(startup);
        startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES | STARTF_USESHOWWINDOW;
        startup.StartupInfo.wShowWindow = SW_HIDE;
        startup.StartupInfo.hStdInput = stdinRead.get();
        startup.StartupInfo.hStdOutput = stdoutWrite.get();
        startup.StartupInfo.hStdError = stderrWrite.get();
        startup.lpAttributeList = attributes.get();

        bool const hasCustomEnvironment =
            !options.inheritEnvironment || !options.environmentOverrides.empty();
        std::vector<wchar_t> environmentBlock;
        if (hasCustomEnvironment)
        {
            environmentBlock = BuildEnvironmentBlock(options);
        }

        PROCESS_INFORMATION processInfo{};
        DWORD creationFlags = EXTENDED_STARTUPINFO_PRESENT | CREATE_SUSPENDED | CREATE_NO_WINDOW;
        if (hasCustomEnvironment)
        {
            creationFlags |= CREATE_UNICODE_ENVIRONMENT;
        }
        auto const workingDirectory = options.workingDirectory
            ? options.workingDirectory->c_str()
            : nullptr;

        if (!CreateProcessW(
            options.executable.c_str(),
            commandLine.data(),
            nullptr,
            nullptr,
            TRUE,
            creationFlags,
            hasCustomEnvironment ? environmentBlock.data() : nullptr,
            workingDirectory,
            &startup.StartupInfo,
            &processInfo))
        {
            ThrowLastError("CreateProcessW failed");
        }

        UniqueHandle process{ processInfo.hProcess };
        UniqueHandle thread{ processInfo.hThread };
        if (!AssignProcessToJobObject(job.get(), process.get()))
        {
            auto const error = GetLastError();
            TerminateProcess(process.get(), error);
            WaitForSingleObject(process.get(), INFINITE);
            throw std::system_error(
                static_cast<int>(error), std::system_category(),
                "AssignProcessToJobObject failed");
        }
        if (ResumeThread(thread.get()) == static_cast<DWORD>(-1))
        {
            auto const error = GetLastError();
            if (!TerminateJobObject(job.get(), error))
            {
                job.reset();
                TerminateProcess(process.get(), error);
            }
            WaitForSingleObject(process.get(), INFINITE);
            throw std::system_error(
                static_cast<int>(error), std::system_category(),
                "ResumeThread failed");
        }
        thread.reset();

        // The child owns the inherited duplicates now. Keeping parent write ends
        // open would prevent the reader threads from observing EOF.
        stdinRead.reset();
        stdoutWrite.reset();
        stderrWrite.reset();

        std::string stdoutBytes;
        std::string stderrBytes;
        std::atomic_size_t capturedBytes{ 0 };
        std::thread stdoutReader;
        std::thread stderrReader;
        try
        {
            stdoutReader = std::thread([&]
            {
                stdoutBytes = ReadPipe(
                    stdoutRead.get(), options.maximumOutputBytes, capturedBytes, outputLimitEvent.get());
            });
            stderrReader = std::thread([&]
            {
                stderrBytes = ReadPipe(
                    stderrRead.get(), options.maximumOutputBytes, capturedBytes, outputLimitEvent.get());
            });
        }
        catch (...)
        {
            TerminateJobObject(job.get(), ERROR_NOT_ENOUGH_MEMORY);
            job.reset();
            WaitForSingleObject(process.get(), INFINITE);
            if (stdoutReader.joinable())
            {
                stdoutReader.join();
            }
            if (stderrReader.joinable())
            {
                stderrReader.join();
            }
            throw;
        }

        bool const hasDeadline = options.timeout != std::chrono::milliseconds::max();
        auto const waitStarted = std::chrono::steady_clock::now();
        auto const maximumRemaining = std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::steady_clock::time_point::max() - waitStarted);
        auto const deadline = !hasDeadline || options.timeout >= maximumRemaining
            ? std::chrono::steady_clock::time_point::max()
            : waitStarted + options.timeout;
        std::array<HANDLE, 3> waitHandles{
            process.get(), cancellationEvent.get(), outputLimitEvent.get()
        };

        std::array<HANDLE, 2> controlHandles{
            cancellationEvent.get(), outputLimitEvent.get()
        };
        ProcessResult result;
        DWORD waitError = ERROR_SUCCESS;
        bool rootExited = false;

        for (;;)
        {
            DWORD waitResult = WAIT_FAILED;
            if (!rootExited)
            {
                waitResult = WaitForMultipleObjects(
                    static_cast<DWORD>(waitHandles.size()), waitHandles.data(), FALSE,
                    WaitDuration(deadline, hasDeadline));
                if (waitResult == WAIT_OBJECT_0)
                {
                    rootExited = true;
                }
            }
            else
            {
                auto const duration = (std::min)(WaitDuration(deadline, hasDeadline), DWORD{ 25 });
                waitResult = WaitForMultipleObjects(
                    static_cast<DWORD>(controlHandles.size()), controlHandles.data(), FALSE, duration);
            }

            if (rootExited)
            {
                if (WaitForSingleObject(outputLimitEvent.get(), 0) == WAIT_OBJECT_0)
                {
                    result.outputLimitExceeded = true;
                    break;
                }
                DWORD activeProcesses = 0;
                if (!TryGetActiveProcessCount(job.get(), activeProcesses, waitError))
                {
                    break;
                }
                if (activeProcesses == 0)
                {
                    break;
                }
                if (waitResult == WAIT_OBJECT_0 &&
                    WaitForSingleObject(cancellationEvent.get(), 0) != WAIT_OBJECT_0)
                {
                    continue;
                }
            }

            auto const cancellationResult = rootExited ? WAIT_OBJECT_0 : WAIT_OBJECT_0 + 1;
            auto const outputLimitResult = rootExited ? WAIT_OBJECT_0 + 1 : WAIT_OBJECT_0 + 2;
            if (waitResult == cancellationResult)
            {
                result.cancelled = true;
                break;
            }
            if (waitResult == outputLimitResult)
            {
                result.outputLimitExceeded = true;
                break;
            }
            if (rootExited && waitResult == WAIT_TIMEOUT &&
                (!hasDeadline || std::chrono::steady_clock::now() < deadline))
            {
                continue;
            }
            if (waitResult == WAIT_TIMEOUT)
            {
                if (WaitForSingleObject(process.get(), 0) == WAIT_OBJECT_0)
                {
                    DWORD activeProcesses = 0;
                    if (TryGetActiveProcessCount(job.get(), activeProcesses, waitError) &&
                        activeProcesses == 0)
                    {
                        break;
                    }
                }
                result.timedOut = true;
                break;
            }
            waitError = GetLastError();
            break;
        }

        if (result.cancelled || result.timedOut || result.outputLimitExceeded ||
            waitError != ERROR_SUCCESS)
        {
            auto const terminationCode = result.cancelled
                ? ERROR_CANCELLED
                : (result.timedOut
                    ? ERROR_TIMEOUT
                    : (result.outputLimitExceeded ? ERROR_BUFFER_OVERFLOW : waitError));
            if (!TerminateJobObject(job.get(), terminationCode))
            {
                auto const terminationError = GetLastError();
                job.reset();
                if (WaitForSingleObject(process.get(), 5'000) == WAIT_TIMEOUT)
                {
                    TerminateProcess(process.get(), terminationCode);
                }
                if (waitError == ERROR_SUCCESS)
                {
                    waitError = terminationError;
                }
            }
            else
            {
                DWORD jobWaitError = ERROR_SUCCESS;
                if (!WaitForJobToEmpty(job.get(), jobWaitError) && waitError == ERROR_SUCCESS)
                {
                    waitError = jobWaitError;
                }
            }
            if (WaitForSingleObject(process.get(), INFINITE) == WAIT_FAILED &&
                waitError == ERROR_SUCCESS)
            {
                waitError = GetLastError();
            }
        }

        // Kill-on-close remains a final containment boundary for a process-tree
        // race while never affecting processes outside this runner's job.
        job.reset();
        stdoutReader.join();
        stderrReader.join();
        if (WaitForSingleObject(outputLimitEvent.get(), 0) == WAIT_OBJECT_0)
        {
            // The child can exit before reader threads finish draining buffered pipe
            // data. A late limit signal must still make the result fail closed.
            result.outputLimitExceeded = true;
        }

        DWORD exitCode = 0;
        if (!GetExitCodeProcess(process.get(), &exitCode))
        {
            auto const error = GetLastError();
            if (waitError == ERROR_SUCCESS)
            {
                waitError = error;
            }
        }
        else
        {
            result.exitCode = exitCode;
        }
        if (result.outputLimitExceeded)
        {
            result.exitCode = static_cast<std::uint32_t>(ERROR_BUFFER_OVERFLOW);
        }

        result.standardOutput = DecodeOutput(stdoutBytes);
        result.standardError = DecodeOutput(stderrBytes);
        if (waitError != ERROR_SUCCESS)
        {
            throw std::system_error(
                static_cast<int>(waitError), std::system_category(),
                "waiting for child process failed");
        }
        return result;
    }
}
