#ifndef NOMINMAX
#define NOMINMAX
#endif
#ifndef WIN32_LEAN_AND_MEAN
#define WIN32_LEAN_AND_MEAN
#endif

#include "PackageRuntime.h"

#include "PackageParsers.h"

#include <windows.h>
#include <winhttp.h>

#include <algorithm>
#include <array>
#include <cctype>
#include <cstddef>
#include <filesystem>
#include <limits>
#include <memory>
#include <stdexcept>
#include <system_error>
#include <utility>

namespace winforge::core::packages
{
    namespace
    {
        class InternetHandle final
        {
        public:
            InternetHandle() noexcept = default;
            explicit InternetHandle(HINTERNET value) noexcept : value_(value) {}
            ~InternetHandle() { close(); }

            InternetHandle(InternetHandle const&) = delete;
            InternetHandle& operator=(InternetHandle const&) = delete;
            InternetHandle(InternetHandle&& other) noexcept : value_(std::exchange(other.value_, nullptr)) {}
            InternetHandle& operator=(InternetHandle&& other) noexcept
            {
                if (this != &other)
                {
                    close();
                    value_ = std::exchange(other.value_, nullptr);
                }
                return *this;
            }

            void close() noexcept
            {
                if (auto value = std::exchange(value_, nullptr)) WinHttpCloseHandle(value);
            }

            [[nodiscard]] HINTERNET get() const noexcept { return value_; }
            [[nodiscard]] explicit operator bool() const noexcept { return get() != nullptr; }

        private:
            HINTERNET value_{ nullptr };
        };

        struct HttpResult
        {
            bool success{ false };
            bool cancelled{ false };
            bool timed_out{ false };
            std::uint32_t status_code{ 0 };
            std::string body;
            std::wstring diagnostic;
        };

        std::wstring Trim(std::wstring_view value)
        {
            auto first = value.begin();
            auto last = value.end();
            while (first != last && iswspace(*first)) ++first;
            while (last != first && iswspace(*(last - 1))) --last;
            return std::wstring(first, last);
        }

        std::wstring Lower(std::wstring_view value)
        {
            std::wstring result(value);
            std::transform(result.begin(), result.end(), result.begin(), [](wchar_t character)
            {
                return static_cast<wchar_t>(towlower(character));
            });
            return result;
        }

        std::wstring Win32Message(DWORD error)
        {
            wchar_t* buffer = nullptr;
            auto const size = FormatMessageW(
                FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                nullptr,
                error,
                0,
                reinterpret_cast<wchar_t*>(&buffer),
                0,
                nullptr);
            std::wstring message = size && buffer ? std::wstring(buffer, size) : L"Win32 error " + std::to_wstring(error);
            if (buffer) LocalFree(buffer);
            while (!message.empty() && iswspace(message.back())) message.pop_back();
            return message;
        }

        std::wstring Utf8ToWide(std::string_view value)
        {
            if (value.empty()) return {};
            if (value.size() > static_cast<std::size_t>(std::numeric_limits<int>::max()))
                throw std::length_error("UTF-8 text is too large");
            auto const length = MultiByteToWideChar(
                CP_UTF8,
                MB_ERR_INVALID_CHARS,
                value.data(),
                static_cast<int>(value.size()),
                nullptr,
                0);
            if (length == 0)
            {
                std::wstring fallback;
                fallback.reserve(value.size());
                for (auto const byte : value) fallback.push_back(static_cast<unsigned char>(byte));
                return fallback;
            }
            std::wstring result(length, L'\0');
            MultiByteToWideChar(
                CP_UTF8,
                MB_ERR_INVALID_CHARS,
                value.data(),
                static_cast<int>(value.size()),
                result.data(),
                length);
            return result;
        }

        std::string WideToUtf8(std::wstring_view value)
        {
            if (value.empty()) return {};
            if (value.size() > static_cast<std::size_t>(std::numeric_limits<int>::max()))
                throw std::length_error("wide text is too large");
            auto const length = WideCharToMultiByte(
                CP_UTF8,
                WC_ERR_INVALID_CHARS,
                value.data(),
                static_cast<int>(value.size()),
                nullptr,
                0,
                nullptr,
                nullptr);
            if (length == 0) throw std::system_error(GetLastError(), std::system_category(), "WideCharToMultiByte");
            std::string result(length, '\0');
            WideCharToMultiByte(
                CP_UTF8,
                WC_ERR_INVALID_CHARS,
                value.data(),
                static_cast<int>(value.size()),
                result.data(),
                length,
                nullptr,
                nullptr);
            return result;
        }

        std::vector<std::filesystem::path> PathDirectories()
        {
            DWORD required = GetEnvironmentVariableW(L"PATH", nullptr, 0);
            if (required == 0) return {};
            std::wstring value(required, L'\0');
            auto const written = GetEnvironmentVariableW(L"PATH", value.data(), required);
            if (written == 0 || written >= required) return {};
            value.resize(written);

            std::vector<std::filesystem::path> result;
            std::size_t offset = 0;
            while (offset <= value.size())
            {
                auto const separator = value.find(L';', offset);
                auto token = Trim(value.substr(offset, separator == std::wstring::npos ? std::wstring::npos : separator - offset));
                if (token.size() >= 2 && token.front() == L'"' && token.back() == L'"')
                    token = token.substr(1, token.size() - 2);
                if (!token.empty())
                {
                    std::filesystem::path directory(token);
                    if (directory.is_absolute())
                    {
                        std::error_code ignored;
                        auto canonical = std::filesystem::weakly_canonical(directory, ignored);
                        if (!ignored && std::filesystem::is_directory(canonical, ignored))
                        {
                            result.emplace_back(std::move(canonical));
                        }
                    }
                }
                if (separator == std::wstring::npos) break;
                offset = separator + 1;
            }
            return result;
        }

        std::optional<std::filesystem::path> FindOnPath(
            std::wstring_view executable,
            std::wstring_view extension)
        {
            auto name = std::filesystem::path(executable);
            if (!extension.empty()) name += extension;
            std::error_code ignored;
            for (auto const& directory : PathDirectories())
            {
                auto candidate = directory / name;
                if (std::filesystem::is_regular_file(candidate, ignored))
                {
                    auto canonical = std::filesystem::weakly_canonical(candidate, ignored);
                    if (!ignored && std::filesystem::is_regular_file(canonical, ignored))
                    {
                        return canonical;
                    }
                }
                ignored.clear();
            }
            return std::nullopt;
        }

        std::optional<std::filesystem::path> SystemPowerShell()
        {
            std::array<wchar_t, MAX_PATH + 1> systemDirectory{};
            auto const length = GetSystemDirectoryW(
                systemDirectory.data(), static_cast<UINT>(systemDirectory.size()));
            if (length == 0 || length >= systemDirectory.size()) return std::nullopt;
            auto path = std::filesystem::path(systemDirectory.data()) /
                L"WindowsPowerShell" / L"v1.0" / L"powershell.exe";
            std::error_code ignored;
            if (!std::filesystem::is_regular_file(path, ignored)) return std::nullopt;
            auto canonical = std::filesystem::weakly_canonical(path, ignored);
            return ignored ? std::nullopt : std::optional<std::filesystem::path>(std::move(canonical));
        }

        bool IsUnsafePackageExecutionToken() noexcept
        {
            HANDLE rawToken = nullptr;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken)) return true;
            struct TokenCloser
            {
                void operator()(void* value) const noexcept
                {
                    if (value) CloseHandle(value);
                }
            };
            std::unique_ptr<void, TokenCloser> token(rawToken);
            DWORD required = 0;
            GetTokenInformation(rawToken, TokenIntegrityLevel, nullptr, 0, &required);
            if (required < sizeof(TOKEN_MANDATORY_LABEL)) return true;
            std::vector<std::byte> storage(required);
            if (!GetTokenInformation(
                rawToken, TokenIntegrityLevel, storage.data(), required, &required)) return true;
            auto const* label = reinterpret_cast<TOKEN_MANDATORY_LABEL const*>(storage.data());
            if (!label->Label.Sid || !IsValidSid(label->Label.Sid)) return true;
            auto const count = *GetSidSubAuthorityCount(label->Label.Sid);
            if (count == 0) return true;
            auto const integrity = *GetSidSubAuthority(label->Label.Sid, count - 1);
            if (integrity >= SECURITY_MANDATORY_HIGH_RID) return true;

            TOKEN_ELEVATION elevation{};
            required = sizeof(elevation);
            if (!GetTokenInformation(
                rawToken, TokenElevation, &elevation, sizeof(elevation), &required)) return true;
            if (elevation.TokenIsElevated != 0) return true;

            TOKEN_ELEVATION_TYPE elevationType = TokenElevationTypeDefault;
            required = sizeof(elevationType);
            if (!GetTokenInformation(
                rawToken, TokenElevationType, &elevationType, sizeof(elevationType), &required)) return true;
            return elevationType == TokenElevationTypeFull;
        }

        std::wstring BunGlobalDirectory()
        {
            DWORD required = GetEnvironmentVariableW(L"USERPROFILE", nullptr, 0);
            if (required == 0) return {};
            std::wstring home(required, L'\0');
            auto const written = GetEnvironmentVariableW(L"USERPROFILE", home.data(), required);
            if (written == 0 || written >= required) return {};
            home.resize(written);
            return (std::filesystem::path(home) / L".bun" / L"install" / L"global").wstring();
        }

        PackageParserKind ParserKind(CommandPostProcess value)
        {
            switch (value)
            {
            case CommandPostProcess::None: return PackageParserKind::None;
            case CommandPostProcess::WingetTable: return PackageParserKind::WingetTable;
            case CommandPostProcess::ScoopSearch: return PackageParserKind::ScoopSearch;
            case CommandPostProcess::ScoopExport: return PackageParserKind::ScoopExport;
            case CommandPostProcess::ChocolateyDelimited: return PackageParserKind::ChocolateyDelimited;
            case CommandPostProcess::JsonPackages: return PackageParserKind::JsonPackages;
            case CommandPostProcess::NpmJson: return PackageParserKind::NpmJson;
            case CommandPostProcess::DotnetTable: return PackageParserKind::DotnetTable;
            case CommandPostProcess::DotnetUpdatesFromNuGet: return PackageParserKind::DotnetUpdatesFromNuGet;
            case CommandPostProcess::PowerShellPackagesJson: return PackageParserKind::PowerShellPackagesJson;
            case CommandPostProcess::CargoSearch: return PackageParserKind::CargoSearch;
            case CommandPostProcess::CargoInstalled: return PackageParserKind::CargoInstalled;
            case CommandPostProcess::CargoUpdates: return PackageParserKind::CargoUpdates;
            case CommandPostProcess::BunInstalled: return PackageParserKind::BunInstalled;
            case CommandPostProcess::BunUpdates: return PackageParserKind::BunUpdates;
            case CommandPostProcess::VcpkgSearch: return PackageParserKind::VcpkgSearch;
            case CommandPostProcess::VcpkgInstalled: return PackageParserKind::VcpkgInstalled;
            case CommandPostProcess::VcpkgUpdates: return PackageParserKind::VcpkgUpdates;
            case CommandPostProcess::PyPiSearch: return PackageParserKind::PyPiSearch;
            case CommandPostProcess::NpmRegistrySearch: return PackageParserKind::NpmRegistrySearch;
            }
            return PackageParserKind::None;
        }

        PackageOutputKind OutputKind(PackageAction action)
        {
            switch (action)
            {
            case PackageAction::Installed: return PackageOutputKind::Installed;
            case PackageAction::Updates: return PackageOutputKind::Updates;
            default: return PackageOutputKind::Search;
            }
        }

        PackageItem ToPackageItem(PackageRecord const& record, std::wstring_view fallbackManager)
        {
            PackageItem item;
            item.name = Utf8ToWide(record.name);
            item.id = Utf8ToWide(record.id);
            item.version = Utf8ToWide(record.version);
            item.available_version = Utf8ToWide(record.availableVersion);
            item.source = Utf8ToWide(record.source);
            item.manager_key = record.managerKey.empty() ? std::wstring(fallbackManager) : Utf8ToWide(record.managerKey);
            if (item.name.empty()) item.name = item.id;
            return item;
        }

        HttpResult HttpGet(
            std::wstring url,
            CommandPostProcess postProcess,
            PackageRuntimeOptions const& options)
        {
            HttpResult result;
            if (options.timeout <= std::chrono::milliseconds::zero())
            {
                result.diagnostic = L"HTTPS timeout must be positive";
                return result;
            }
            if (options.cancellation_token.stop_requested())
            {
                result.cancelled = true;
                result.diagnostic = L"cancelled";
                return result;
            }

            URL_COMPONENTS components{};
            components.dwStructSize = sizeof(components);
            components.dwSchemeLength = static_cast<DWORD>(-1);
            components.dwHostNameLength = static_cast<DWORD>(-1);
            components.dwUrlPathLength = static_cast<DWORD>(-1);
            components.dwExtraInfoLength = static_cast<DWORD>(-1);
            components.dwUserNameLength = static_cast<DWORD>(-1);
            components.dwPasswordLength = static_cast<DWORD>(-1);
            if (!WinHttpCrackUrl(url.c_str(), static_cast<DWORD>(url.size()), 0, &components))
            {
                result.diagnostic = L"invalid HTTPS request URI: " + Win32Message(GetLastError());
                return result;
            }
            if (components.nScheme != INTERNET_SCHEME_HTTPS || components.nPort != INTERNET_DEFAULT_HTTPS_PORT ||
                components.dwHostNameLength == 0 ||
                components.dwUserNameLength != 0 || components.dwPasswordLength != 0)
            {
                result.diagnostic = L"only credential-free HTTPS package endpoints are allowed";
                return result;
            }

            std::wstring host(components.lpszHostName, components.dwHostNameLength);
            std::wstring path;
            if (components.dwUrlPathLength) path.assign(components.lpszUrlPath, components.dwUrlPathLength);
            if (components.dwExtraInfoLength) path.append(components.lpszExtraInfo, components.dwExtraInfoLength);
            if (path.empty()) path = L"/";

            auto const normalizedHost = Lower(host);
            auto const allowed = (postProcess == CommandPostProcess::PyPiSearch &&
                    normalizedHost == L"pypi.org" && path == L"/simple/") ||
                (postProcess == CommandPostProcess::NpmRegistrySearch &&
                    normalizedHost == L"registry.npmjs.org" && path.starts_with(L"/-/v1/search?"));
            if (!allowed)
            {
                result.diagnostic = L"HTTPS package endpoint is not on the native allowlist";
                return result;
            }

            InternetHandle session{ WinHttpOpen(
                L"WinForge-Native/1.0",
                WINHTTP_ACCESS_TYPE_AUTOMATIC_PROXY,
                WINHTTP_NO_PROXY_NAME,
                WINHTTP_NO_PROXY_BYPASS,
                0) };
            if (!session)
            {
                result.diagnostic = L"WinHttpOpen failed: " + Win32Message(GetLastError());
                return result;
            }

            // This request is synchronous. Keep each blocking WinHTTP phase bounded
            // and observe cancellation between phases; never close a live synchronous
            // request from another thread because WinHTTP does not support that race.
            auto const phaseTimeout = options.timeout == std::chrono::milliseconds::max()
                ? 30'000
                : static_cast<int>(std::clamp<long long>(options.timeout.count() / 4, 1'000, 30'000));
            if (!WinHttpSetTimeouts(
                session.get(), phaseTimeout, phaseTimeout, phaseTimeout, phaseTimeout))
            {
                result.diagnostic = L"could not configure HTTPS timeouts: " + Win32Message(GetLastError());
                return result;
            }
            DWORD redirectPolicy = WINHTTP_OPTION_REDIRECT_POLICY_NEVER;
            if (!WinHttpSetOption(
                session.get(), WINHTTP_OPTION_REDIRECT_POLICY, &redirectPolicy, sizeof(redirectPolicy)))
            {
                result.diagnostic = L"could not disable HTTPS redirects: " + Win32Message(GetLastError());
                return result;
            }
            BOOL rejectUserInfo = TRUE;
            if (!WinHttpSetOption(
                session.get(), WINHTTP_OPTION_REJECT_USERPWD_IN_URL, &rejectUserInfo, sizeof(rejectUserInfo)))
            {
                result.diagnostic = L"could not enforce credential-free HTTPS: " + Win32Message(GetLastError());
                return result;
            }

            InternetHandle connection{ WinHttpConnect(
                session.get(),
                host.c_str(),
                components.nPort,
                0) };
            if (!connection)
            {
                result.diagnostic = L"WinHttpConnect failed: " + Win32Message(GetLastError());
                return result;
            }

            InternetHandle request{ WinHttpOpenRequest(
                connection.get(),
                L"GET",
                path.c_str(),
                nullptr,
                WINHTTP_NO_REFERER,
                WINHTTP_DEFAULT_ACCEPT_TYPES,
                WINHTTP_FLAG_SECURE) };
            if (!request)
            {
                result.diagnostic = L"WinHttpOpenRequest failed: " + Win32Message(GetLastError());
                return result;
            }

            if (postProcess == CommandPostProcess::PyPiSearch)
            {
                constexpr auto accept = L"Accept: application/vnd.pypi.simple.v1+json\r\n";
                if (!WinHttpAddRequestHeaders(
                    request.get(), accept, static_cast<DWORD>(-1),
                    WINHTTP_ADDREQ_FLAG_ADD | WINHTTP_ADDREQ_FLAG_REPLACE))
                {
                    result.diagnostic = L"could not configure PyPI request: " + Win32Message(GetLastError());
                    return result;
                }
            }

            auto failIo = [&](std::wstring_view operation)
            {
                auto const error = GetLastError();
                if (error == ERROR_WINHTTP_TIMEOUT)
                {
                    result.timed_out = true;
                    result.diagnostic = L"HTTPS package request timed out";
                }
                else if (options.cancellation_token.stop_requested())
                {
                    result.cancelled = true;
                    result.diagnostic = L"cancelled";
                }
                else
                {
                    result.diagnostic = std::wstring(operation) + L": " + Win32Message(error);
                }
                result.body.clear();
            };
            if (options.cancellation_token.stop_requested())
            {
                result.cancelled = true;
                result.diagnostic = L"cancelled";
                return result;
            }
            if (!WinHttpSendRequest(
                request.get(),
                WINHTTP_NO_ADDITIONAL_HEADERS,
                0,
                WINHTTP_NO_REQUEST_DATA,
                0,
                0,
                0) || !WinHttpReceiveResponse(request.get(), nullptr))
            {
                failIo(L"HTTPS package request failed");
                return result;
            }
            if (options.cancellation_token.stop_requested())
            {
                result.cancelled = true;
                result.diagnostic = L"cancelled";
                return result;
            }

            DWORD status = 0;
            DWORD statusSize = sizeof(status);
            if (!WinHttpQueryHeaders(
                request.get(),
                WINHTTP_QUERY_STATUS_CODE | WINHTTP_QUERY_FLAG_NUMBER,
                WINHTTP_HEADER_NAME_BY_INDEX,
                &status,
                &statusSize,
                WINHTTP_NO_HEADER_INDEX))
            {
                failIo(L"could not read HTTPS status");
                return result;
            }
            result.status_code = status;
            if (status < 200 || status >= 300)
            {
                result.diagnostic = L"HTTPS package endpoint returned status " + std::to_wstring(status);
                return result;
            }

            constexpr std::size_t MaximumResponse = 128ull * 1024ull * 1024ull;
            for (;;)
            {
                if (options.cancellation_token.stop_requested())
                {
                    result.cancelled = true;
                    result.diagnostic = L"cancelled";
                    return result;
                }
                DWORD available = 0;
                if (!WinHttpQueryDataAvailable(request.get(), &available))
                {
                    failIo(L"could not read HTTPS package response");
                    return result;
                }
                if (available == 0) break;
                if (result.body.size() + available > MaximumResponse)
                {
                    result.diagnostic = L"HTTPS package response exceeded 128 MiB";
                    result.body.clear();
                    return result;
                }
                auto const offset = result.body.size();
                result.body.resize(offset + available);
                DWORD read = 0;
                if (!WinHttpReadData(request.get(), result.body.data() + offset, available, &read))
                {
                    failIo(L"could not read HTTPS package response");
                    return result;
                }
                result.body.resize(offset + read);
                if (read == 0) break;
            }
            if (options.cancellation_token.stop_requested())
            {
                result.cancelled = true;
                result.body.clear();
                result.diagnostic = L"cancelled";
                return result;
            }
            result.success = true;
            return result;
        }

        void ParseInto(
            PackageRuntimeResult& runtime,
            CommandSpec const& command,
            PackageAction action,
            std::wstring_view managerKey,
            std::string_view output)
        {
            auto const managerUtf8 = WideToUtf8(managerKey);
            auto const queryUtf8 = WideToUtf8(command.query);
            auto parsed = ParsePackageOutput(
                ParserKind(command.post_process),
                output,
                OutputKind(action),
                managerUtf8,
                queryUtf8);
            runtime.parser_supported = parsed.supported;
            runtime.requires_runtime_resolution = parsed.requiresRuntimeResolution;
            if (!parsed.diagnostic.empty()) runtime.diagnostic = Utf8ToWide(parsed.diagnostic);
            runtime.packages.reserve(parsed.packages.size());
            for (auto const& record : parsed.packages)
            {
                if (record.IsValid()) runtime.packages.push_back(ToPackageItem(record, managerKey));
            }
        }
    }

    std::optional<ResolvedExecutable> ResolvePackageExecutable(std::wstring_view executable) noexcept
    {
        try
        {
            auto requested = Trim(executable);
            if (requested.empty() || requested.find(L'\0') != std::wstring::npos) return std::nullopt;

            std::error_code ignored;
            std::filesystem::path explicitPath(requested);
            if (explicitPath.has_parent_path())
            {
                if (!explicitPath.is_absolute()) return std::nullopt;
                if (!std::filesystem::is_regular_file(explicitPath, ignored)) return std::nullopt;
                auto const extension = Lower(explicitPath.extension().wstring());
                if (extension == L".ps1")
                {
                    auto powershell = SystemPowerShell();
                    if (!powershell) return std::nullopt;
                    auto canonical = std::filesystem::weakly_canonical(explicitPath, ignored);
                    if (ignored) return std::nullopt;
                    return ResolvedExecutable{
                        powershell->wstring(),
                        { L"-NoProfile", L"-NonInteractive", L"-ExecutionPolicy", L"Bypass", L"-File", canonical.wstring() },
                        canonical.wstring(),
                    };
                }
                if (extension != L".exe" && extension != L".com") return std::nullopt;
                auto canonical = std::filesystem::weakly_canonical(explicitPath, ignored);
                if (ignored) return std::nullopt;
                return ResolvedExecutable{ canonical.wstring(), {}, canonical.wstring() };
            }

            auto const requestedLower = Lower(requested);
            if (requestedLower == L"powershell" || requestedLower == L"powershell.exe")
            {
                auto powershell = SystemPowerShell();
                if (!powershell) return std::nullopt;
                return ResolvedExecutable{ powershell->wstring(), {}, powershell->wstring() };
            }

            auto const extension = Lower(explicitPath.extension().wstring());
            if (!extension.empty())
            {
                if (extension == L".exe" || extension == L".com")
                {
                    if (auto native = FindOnPath(requested, L""))
                        return ResolvedExecutable{ native->wstring(), {}, native->wstring() };
                    return std::nullopt;
                }
                if (extension == L".ps1")
                {
                    auto shim = FindOnPath(requested, L"");
                    auto powershell = SystemPowerShell();
                    if (!shim || !powershell) return std::nullopt;
                    return ResolvedExecutable{
                        powershell->wstring(),
                        { L"-NoProfile", L"-NonInteractive", L"-ExecutionPolicy", L"Bypass", L"-File", shim->wstring() },
                        shim->wstring(),
                    };
                }
                return std::nullopt;
            }

            if (auto native = FindOnPath(requested, L".exe"))
                return ResolvedExecutable{ native->wstring(), {}, native->wstring() };
            if (auto native = FindOnPath(requested, L".com"))
                return ResolvedExecutable{ native->wstring(), {}, native->wstring() };
            auto shim = FindOnPath(requested, L".ps1");
            auto powershell = SystemPowerShell();
            if (shim && powershell)
            {
                return ResolvedExecutable{
                    powershell->wstring(),
                    { L"-NoProfile", L"-NonInteractive", L"-ExecutionPolicy", L"Bypass", L"-File", shim->wstring() },
                    shim->wstring(),
                };
            }
            return std::nullopt;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    std::wstring PercentEncodeUtf8(std::wstring_view value)
    {
        auto const bytes = WideToUtf8(value);
        constexpr std::array<wchar_t, 16> Hex{
            L'0', L'1', L'2', L'3', L'4', L'5', L'6', L'7',
            L'8', L'9', L'A', L'B', L'C', L'D', L'E', L'F'
        };
        std::wstring encoded;
        encoded.reserve(bytes.size() * 3);
        for (auto const byte : bytes)
        {
            auto const valueByte = static_cast<unsigned char>(byte);
            if (std::isalnum(valueByte) || valueByte == '-' || valueByte == '_' || valueByte == '.' || valueByte == '~')
            {
                encoded.push_back(static_cast<wchar_t>(valueByte));
            }
            else
            {
                encoded.push_back(L'%');
                encoded.push_back(Hex[valueByte >> 4]);
                encoded.push_back(Hex[valueByte & 0x0F]);
            }
        }
        return encoded;
    }

    namespace
    {
        PackageRuntimeResult ExecuteValidatedCommand(
            CommandSpec const& command,
            PackageAction action,
            std::wstring_view manager_key,
            PackageRuntimeOptions const& options)
        {
            PackageRuntimeResult runtime;
            try
            {
                if (options.timeout <= std::chrono::milliseconds::zero())
                {
                    runtime.diagnostic = L"package timeout must be positive";
                    return runtime;
                }
                if (options.cancellation_token.stop_requested())
                {
                    runtime.cancelled = true;
                    runtime.diagnostic = L"package command was cancelled";
                    return runtime;
                }

                std::string parserInput;
                bool transportSuccess = false;
                if (command.transport == CommandTransport::StaticText)
                {
                    runtime.standard_output = command.static_text;
                    parserInput = WideToUtf8(command.static_text);
                    transportSuccess = true;
                }
                else if (command.transport == CommandTransport::HttpGet)
                {
                    auto url = command.request_uri;
                    if (command.post_process == CommandPostProcess::NpmRegistrySearch && !command.query.empty())
                    {
                        url += url.find(L'?') == std::wstring::npos ? L"?text=" : L"&text=";
                        url += PercentEncodeUtf8(command.query);
                    }
                    auto http = HttpGet(url, command.post_process, options);
                    runtime.command_started = true;
                    runtime.cancelled = http.cancelled;
                    runtime.timed_out = http.timed_out;
                    runtime.diagnostic = std::move(http.diagnostic);
                    parserInput = std::move(http.body);
                    runtime.standard_output = Utf8ToWide(parserInput);
                    transportSuccess = http.success;
                }
                else
                {
                    if (command.requires_elevation)
                    {
                        runtime.diagnostic = L"elevated package operations require the native consent broker";
                        return runtime;
                    }
                    if (IsUnsafePackageExecutionToken())
                    {
                        runtime.diagnostic = L"external package commands are disabled while WinForge is elevated";
                        return runtime;
                    }
                    auto resolved = ResolvePackageExecutable(command.executable);
                    if (!resolved)
                    {
                        runtime.diagnostic = L"package executable was not found or is not a supported native/PowerShell command: " + command.executable;
                        return runtime;
                    }
                    ProcessOptions process;
                    process.executable = resolved->executable;
                    process.arguments = resolved->argument_prefix;
                    process.arguments.insert(process.arguments.end(), command.arguments.begin(), command.arguments.end());
                    process.timeout = options.timeout;
                    process.cancellationToken = options.cancellation_token;
                    if (command.working_directory == CommandWorkingDirectory::BunGlobalPackages)
                    {
                        auto directory = BunGlobalDirectory();
                        if (!directory.empty()) process.workingDirectory = std::move(directory);
                    }
                    auto result = ProcessRunner::Run(process);
                    runtime.command_started = true;
                    runtime.exit_code = result.exitCode;
                    runtime.standard_output = std::move(result.standardOutput);
                    runtime.standard_error = std::move(result.standardError);
                    runtime.timed_out = result.timedOut;
                    runtime.cancelled = result.cancelled;
                    runtime.output_limit_exceeded = result.outputLimitExceeded;
                    parserInput = WideToUtf8(runtime.standard_output);
                    transportSuccess = result.exitCode && *result.exitCode == 0 &&
                        !result.timedOut && !result.cancelled && !result.outputLimitExceeded;
                }

                if (command.post_process != CommandPostProcess::None)
                {
                    ParseInto(runtime, command, action, manager_key, parserInput);
                    auto const acceptedParsedNonZeroExit = runtime.exit_code &&
                        detail::AcceptsParsedNonZeroExit(
                            manager_key,
                            action,
                            command.post_process,
                            *runtime.exit_code,
                            runtime.packages.size());
                    runtime.success = runtime.parser_supported && !runtime.requires_runtime_resolution &&
                        !runtime.timed_out && !runtime.cancelled && !runtime.output_limit_exceeded &&
                        (transportSuccess || acceptedParsedNonZeroExit);
                }
                else
                {
                    runtime.success = transportSuccess;
                }
                if (!runtime.success && runtime.diagnostic.empty())
                {
                    if (runtime.timed_out) runtime.diagnostic = L"package command timed out";
                    else if (runtime.cancelled) runtime.diagnostic = L"package command was cancelled";
                    else if (runtime.output_limit_exceeded) runtime.diagnostic = L"package command exceeded the 16 MiB output limit";
                    else if (runtime.requires_runtime_resolution) runtime.diagnostic = L"package output requires additional native resolution";
                    else if (!runtime.parser_supported) runtime.diagnostic = L"package output requires additional native orchestration";
                    else if (!runtime.standard_error.empty()) runtime.diagnostic = runtime.standard_error;
                    else if (runtime.exit_code) runtime.diagnostic = L"package command exited with code " + std::to_wstring(*runtime.exit_code);
                    else runtime.diagnostic = L"package command failed";
                }
                return runtime;
            }
            catch (std::exception const& error)
            {
                runtime.diagnostic = Utf8ToWide(error.what());
                return runtime;
            }
        }
    }

    PackageRuntimeResult ProbePackageManager(
        std::wstring_view manager_key,
        PackageRuntimeOptions const& options)
    {
        auto built = BuildProbeCommand(manager_key);
        if (!built)
        {
            PackageRuntimeResult result;
            result.diagnostic = built.error_code;
            return result;
        }
        return ExecuteValidatedCommand(*built.command, PackageAction::Probe, manager_key, options);
    }

    PackageRuntimeResult QueryPackageManager(
        std::wstring_view manager_key,
        PackageAction action,
        std::wstring_view query,
        PackageRuntimeOptions const& options)
    {
        auto const* manager = FindPackageManager(manager_key);
        if (!manager)
        {
            PackageRuntimeResult result;
            result.diagnostic = L"invalid-manager";
            return result;
        }
        auto const normalizedKey = std::wstring(manager->key);
        if (normalizedKey == L"bun" &&
            (action == PackageAction::Installed || action == PackageAction::Updates))
        {
            auto const directory = BunGlobalDirectory();
            std::error_code ignored;
            if (directory.empty() || !std::filesystem::is_regular_file(
                std::filesystem::path(directory) / L"package.json", ignored))
            {
                PackageRuntimeResult result;
                result.success = true;
                result.diagnostic = L"Bun has no global package manifest";
                return result;
            }
        }

        CommandBuildResult built;
        switch (action)
        {
        case PackageAction::Search: built = BuildSearchCommand(normalizedKey, query); break;
        case PackageAction::Installed: built = BuildInstalledCommand(normalizedKey); break;
        case PackageAction::Updates: built = BuildUpdatesCommand(normalizedKey); break;
        case PackageAction::Details: built = BuildDetailsCommand(normalizedKey, query); break;
        case PackageAction::Sources: built = BuildSourcesCommand(normalizedKey); break;
        default:
            built.error_code = L"invalid-query-action";
            break;
        }
        if (!built)
        {
            PackageRuntimeResult result;
            result.diagnostic = built.error_code;
            return result;
        }
        auto result = ExecuteValidatedCommand(*built.command, action, normalizedKey, options);
        if (action == PackageAction::Sources)
        {
            // Package source/configuration commands may echo credentials in either
            // stream. Do not let raw transport text or diagnostics cross this API
            // boundary until a manager-specific redactor is proven.
            result.standard_output.clear();
            result.standard_error.clear();
            if (result.success)
                result.diagnostic = L"source query completed; raw configuration output is withheld";
            else if (result.cancelled)
                result.diagnostic = L"source query was cancelled; raw diagnostics are withheld";
            else if (result.timed_out)
                result.diagnostic = L"source query timed out; raw diagnostics are withheld";
            else
                result.diagnostic = L"source query failed; raw diagnostics are withheld";
        }
        if (normalizedKey == L"scoop" && action == PackageAction::Installed &&
            result.command_started && (!result.success || result.packages.empty()) &&
            !result.cancelled && !result.timed_out)
        {
            CommandSpec fallback;
            fallback.executable = L"scoop";
            fallback.arguments = { L"list" };
            fallback.post_process = CommandPostProcess::ScoopSearch;
            auto fallbackResult = ExecuteValidatedCommand(
                fallback, PackageAction::Installed, normalizedKey, options);
            if (fallbackResult.success || !fallbackResult.packages.empty()) return fallbackResult;
            if (!result.diagnostic.empty() && !fallbackResult.diagnostic.empty())
                fallbackResult.diagnostic = result.diagnostic + L"; fallback: " + fallbackResult.diagnostic;
            return fallbackResult;
        }
        return result;
    }

    PackageRuntimeResult RunPackageMutation(
        PackageItem const& package,
        PackageAction action,
        InstallOptions const& install_options,
        PackageRuntimeOptions const& runtime_options)
    {
        auto built = BuildPackageActionCommand(
            package.manager_key,
            package.id,
            package.source,
            action,
            install_options);
        if (!built)
        {
            PackageRuntimeResult result;
            result.diagnostic = built.error_code;
            return result;
        }
        if (built.command->requires_elevation)
        {
            PackageRuntimeResult result;
            result.diagnostic = L"elevated package operations require the native consent broker, which is not enabled in this migration batch";
            return result;
        }
        return ExecuteValidatedCommand(*built.command, action, package.manager_key, runtime_options);
    }
}
