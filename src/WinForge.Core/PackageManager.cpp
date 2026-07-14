#include "PackageManager.h"

#include <algorithm>
#include <array>
#include <cwctype>
#include <stdexcept>
#include <utility>

namespace
{
    using namespace winforge::core::packages;

    constexpr std::array<PackageManagerDescriptor, 11> Managers{
        PackageManagerDescriptor{ L"winget", L"Windows Package Manager", L"Windows 套件管理員", L"winget" },
        PackageManagerDescriptor{ L"scoop", L"Scoop", L"Scoop", L"scoop" },
        PackageManagerDescriptor{ L"choco", L"Chocolatey", L"Chocolatey", L"choco" },
        PackageManagerDescriptor{ L"pip", L"pip (Python)", L"pip（Python）", L"pip" },
        PackageManagerDescriptor{ L"npm", L"npm (Node global)", L"npm（Node 全域）", L"npm" },
        PackageManagerDescriptor{ L"dotnet", L".NET Tools", L".NET 工具", L"dotnet" },
        PackageManagerDescriptor{ L"psgallery", L"PowerShell Gallery", L"PowerShell 資源庫", L"powershell" },
        PackageManagerDescriptor{ L"pwsh7", L"PowerShell 7 (PSResource)", L"PowerShell 7（PSResource）", L"pwsh" },
        PackageManagerDescriptor{ L"cargo", L"Cargo (Rust)", L"Cargo（Rust）", L"cargo" },
        PackageManagerDescriptor{ L"bun", L"Bun (global)", L"Bun（全域）", L"bun" },
        PackageManagerDescriptor{ L"vcpkg", L"vcpkg", L"vcpkg", L"vcpkg" },
    };

    constexpr std::array<PackageViewDescriptor, 9> Views{
        PackageViewDescriptor{ PackageView::Discover, L"Discover", L"搜尋安裝" },
        PackageViewDescriptor{ PackageView::Updates, L"Updates", L"可更新" },
        PackageViewDescriptor{ PackageView::Installed, L"Installed", L"已安裝" },
        PackageViewDescriptor{ PackageView::Bundles, L"Bundles", L"套件清單" },
        PackageViewDescriptor{ PackageView::Sources, L"Sources", L"來源" },
        PackageViewDescriptor{ PackageView::Ignored, L"Ignored", L"已忽略" },
        PackageViewDescriptor{ PackageView::Setup, L"Setup", L"設定引擎" },
        PackageViewDescriptor{ PackageView::Settings, L"Settings", L"設定" },
        PackageViewDescriptor{ PackageView::Operations, L"Operations", L"操作佇列" },
    };

    std::wstring Trim(std::wstring_view value)
    {
        auto first = value.begin();
        auto last = value.end();
        while (first != last && std::iswspace(*first))
        {
            ++first;
        }
        while (last != first && std::iswspace(*(last - 1)))
        {
            --last;
        }
        return std::wstring(first, last);
    }

    std::wstring Lower(std::wstring_view value)
    {
        auto result = Trim(value);
        std::transform(result.begin(), result.end(), result.begin(), [](wchar_t character)
        {
            return static_cast<wchar_t>(std::towlower(character));
        });
        return result;
    }

    bool IsAsciiAlphaNumeric(wchar_t value) noexcept
    {
        return (value >= L'a' && value <= L'z')
            || (value >= L'A' && value <= L'Z')
            || (value >= L'0' && value <= L'9');
    }

    bool IsSafeSegment(std::wstring_view value, bool allow_tilde) noexcept
    {
        if (value.empty() || !IsAsciiAlphaNumeric(value.front()))
        {
            return false;
        }
        for (auto const character : value)
        {
            if (IsAsciiAlphaNumeric(character)
                || character == L'.'
                || character == L'_'
                || character == L'-'
                || character == L'+'
                || (allow_tilde && character == L'~'))
            {
                continue;
            }
            return false;
        }
        return true;
    }

    bool IsSafeToken(std::wstring_view value) noexcept
    {
        if (value.empty() || value.size() > 64 || !IsAsciiAlphaNumeric(value.front()))
        {
            return false;
        }
        return std::all_of(value.begin(), value.end(), [](wchar_t character)
        {
            return IsAsciiAlphaNumeric(character)
                || character == L'.'
                || character == L'_'
                || character == L'-';
        });
    }

    bool IsOperation(PackageAction action) noexcept
    {
        return action == PackageAction::Install
            || action == PackageAction::Update
            || action == PackageAction::Uninstall;
    }

    bool IsExplicitlyIncompatibleSource(std::wstring_view source)
    {
        auto const value = Lower(source);
        return value == L"local"
            || value == L"local pc"
            || value == L"localpc"
            || value == L"unknown"
            || value == L"msstore-fallback";
    }

    bool EqualsAny(std::wstring_view value, std::initializer_list<std::wstring_view> accepted)
    {
        auto const normalized = Lower(value);
        return std::any_of(accepted.begin(), accepted.end(), [&](std::wstring_view candidate)
        {
            return normalized == Lower(candidate);
        });
    }

    ValidationResult Valid()
    {
        return { true, {} };
    }

    ValidationResult Invalid(std::wstring code)
    {
        return { false, std::move(code) };
    }

    CommandBuildResult Failed(std::wstring code)
    {
        return { std::nullopt, std::move(code) };
    }

    CommandBuildResult Built(CommandSpec command)
    {
        return { std::move(command), {} };
    }

    CommandSpec Process(
        std::wstring executable,
        std::vector<std::wstring> arguments,
        CommandPostProcess post_process = CommandPostProcess::None)
    {
        CommandSpec command;
        command.executable = std::move(executable);
        command.arguments = std::move(arguments);
        command.post_process = post_process;
        return command;
    }

    std::wstring QuotePreviewArgument(std::wstring_view value)
    {
        if (value.empty())
        {
            return L"\"\"";
        }

        bool needs_quotes = false;
        for (auto const character : value)
        {
            if (std::iswspace(character) || character == L'"')
            {
                needs_quotes = true;
                break;
            }
        }
        if (!needs_quotes)
        {
            return std::wstring(value);
        }

        std::wstring quoted;
        quoted.reserve(value.size() + 2);
        quoted.push_back(L'"');
        for (auto const character : value)
        {
            if (character == L'\\' || character == L'"')
            {
                quoted.push_back(L'\\');
            }
            quoted.push_back(character);
        }
        quoted.push_back(L'"');
        return quoted;
    }

    std::wstring EncodePowerShellValue(std::wstring_view value)
    {
        constexpr std::string_view alphabet{ "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/" };
        std::vector<unsigned char> bytes;
        bytes.reserve(value.size() * 2);
        for (auto const character : value)
        {
            auto const code = static_cast<std::uint16_t>(character);
            bytes.push_back(static_cast<unsigned char>(code & 0xff));
            bytes.push_back(static_cast<unsigned char>((code >> 8) & 0xff));
        }

        std::wstring encoded{ L"WF" };
        encoded.reserve(2 + ((bytes.size() + 2) / 3) * 4);
        for (std::size_t index = 0; index < bytes.size(); index += 3)
        {
            auto const remaining = bytes.size() - index;
            auto const first = static_cast<unsigned int>(bytes[index]);
            auto const second = remaining > 1 ? static_cast<unsigned int>(bytes[index + 1]) : 0u;
            auto const third = remaining > 2 ? static_cast<unsigned int>(bytes[index + 2]) : 0u;
            auto const block = (first << 16) | (second << 8) | third;
            encoded.push_back(static_cast<wchar_t>(alphabet[(block >> 18) & 0x3f]));
            encoded.push_back(static_cast<wchar_t>(alphabet[(block >> 12) & 0x3f]));
            if (remaining > 1) encoded.push_back(static_cast<wchar_t>(alphabet[(block >> 6) & 0x3f]));
            if (remaining > 2) encoded.push_back(static_cast<wchar_t>(alphabet[block & 0x3f]));
        }
        std::replace(encoded.begin(), encoded.end(), L'+', L'-');
        std::replace(encoded.begin(), encoded.end(), L'/', L'_');
        return encoded;
    }

    CommandSpec PowerShell(
        std::wstring executable,
        std::wstring script,
        std::vector<std::wstring> values = {},
        CommandPostProcess post_process = CommandPostProcess::None)
    {
        if (!values.empty())
        {
            // powershell.exe treats everything after -Command as language input. Only safe base64url
            // tokens cross that boundary; the constant wrapper decodes them before binding the
            // caller values to the constant script block.
            if (script.rfind(L"& {", 0) != 0)
            {
                throw std::invalid_argument("Parameterized PowerShell commands require a script block.");
            }
            auto const script_block = script.substr(2);
            script = LR"PS(& { $decoded=@($args | ForEach-Object { $s=$_.Substring(2).Replace('-','+').Replace('_','/'); switch($s.Length % 4){2{$s+='=='}3{$s+='='}}; [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($s)) }); & )PS"
                + script_block
                + L" @decoded }";
            std::transform(values.begin(), values.end(), values.begin(), EncodePowerShellValue);
        }

        std::vector<std::wstring> arguments{
            L"-NoProfile",
            L"-NonInteractive",
            L"-Command",
            std::move(script),
        };
        arguments.insert(
            arguments.end(),
            std::make_move_iterator(values.begin()),
            std::make_move_iterator(values.end()));
        return Process(std::move(executable), std::move(arguments), post_process);
    }

    ValidationResult ValidateQuery(std::wstring_view query)
    {
        auto const value = Trim(query);
        if (value.empty() || value.size() > 512)
        {
            return Invalid(L"invalid-query");
        }
        if (std::any_of(value.begin(), value.end(), [](wchar_t character)
            {
                return character == L'\0' || (std::iswcntrl(character) && !std::iswspace(character));
            }))
        {
            return Invalid(L"invalid-query");
        }
        return Valid();
    }

    bool IsSafeVersion(std::wstring_view value)
    {
        auto const version = Trim(value);
        if (version.empty())
        {
            return true;
        }
        if (version.size() > 128)
        {
            return false;
        }
        return std::all_of(version.begin(), version.end(), [](wchar_t character)
        {
            return IsAsciiAlphaNumeric(character)
                || character == L'.'
                || character == L'_'
                || character == L'-'
                || character == L'+'
                || character == L':'
                || character == L'!'
                || character == L'~'
                || character == L'*';
        });
    }

    bool IsSafeInstallPath(std::wstring_view value)
    {
        auto const path = Trim(value);
        if (path.empty())
        {
            return true;
        }
        if (path.size() > 1024)
        {
            return false;
        }
        constexpr std::wstring_view forbidden{ L"\"`%!&|<>" };
        return std::none_of(path.begin(), path.end(), [&](wchar_t character)
        {
            return std::iswcntrl(character) || forbidden.find(character) != std::wstring_view::npos;
        });
    }

    bool HasShellHook(InstallOptions const& options)
    {
        std::array<std::wstring_view, 6> const hooks{
            options.pre_install_command,
            options.post_install_command,
            options.pre_update_command,
            options.post_update_command,
            options.pre_uninstall_command,
            options.post_uninstall_command,
        };
        return std::any_of(hooks.begin(), hooks.end(), [](std::wstring_view hook)
        {
            return !Trim(hook).empty();
        });
    }

    bool IsDeniedCustomOption(std::wstring_view value)
    {
        auto const option = Lower(value);
        constexpr std::array<std::wstring_view, 14> exact{
            L"--source", L"-s", L"--registry", L"--index-url", L"-i",
            L"--extra-index-url", L"--trusted-host", L"--add-source",
            L"--repository", L"-repository", L"--config-file", L"--configfile",
            L"--triplet", L"--overlay-ports",
        };
        if (std::find(exact.begin(), exact.end(), option) != exact.end())
        {
            return true;
        }
        constexpr std::array<std::wstring_view, 10> prefixes{
            L"--source=", L"--registry=", L"--index-url=", L"--extra-index-url=",
            L"--trusted-host=", L"--add-source=", L"--repository=", L"-repository:",
            L"--triplet=", L"--overlay-ports=",
        };
        return std::any_of(prefixes.begin(), prefixes.end(), [&](std::wstring_view prefix)
        {
            return option.rfind(prefix, 0) == 0;
        });
    }

    std::wstring const& CustomArgumentsFor(InstallOptions const& options, PackageAction action)
    {
        switch (action)
        {
        case PackageAction::Install:
            return options.custom_args_install;
        case PackageAction::Update:
            return options.custom_args_update;
        default:
            return options.custom_args_uninstall;
        }
    }

    std::wstring BooleanArgument(bool value)
    {
        return value ? L"1" : L"0";
    }

    std::wstring SourceSelectorValue(SourceResolution const& source)
    {
        return source.arguments.size() >= 2 ? source.arguments.back() : L"";
    }

    void Append(std::vector<std::wstring>& destination, std::vector<std::wstring> const& values)
    {
        destination.insert(destination.end(), values.begin(), values.end());
    }

    CommandBuildResult BuildPowerShellOperation(
        std::wstring const& key,
        SourceResolution const& source,
        PackageAction action,
        InstallOptions const& options,
        std::vector<std::wstring> custom_arguments)
    {
        std::size_t custom_length = 0;
        for (auto const& argument : custom_arguments)
        {
            custom_length += argument.size();
        }
        // UTF-16 values expand when converted to safe base64url tokens. Stay below the Windows
        // CreateProcess command-line ceiling after accounting for the constant script wrapper.
        if (custom_length > 8'192)
        {
            return Failed(L"options-too-large");
        }

        std::wstring executable = key == L"psgallery" ? L"powershell" : L"pwsh";
        std::wstring script;
        std::vector<std::wstring> values;

        if (key == L"psgallery")
        {
            if (action == PackageAction::Install)
            {
                script = LR"PS(& { param($id,$repository,$version,$prerelease,[Parameter(ValueFromRemainingArguments=$true)][string[]]$extra) $p=@{Name=$id;Force=$true;Scope='CurrentUser'}; if($repository){$p.Repository=$repository}; if($version){$p.RequiredVersion=$version}; if($prerelease -eq '1'){$p.AllowPrerelease=$true}; Install-Module @p @extra })PS";
                values = { source.package_id, SourceSelectorValue(source), Trim(options.version), BooleanArgument(options.pre_release) };
            }
            else if (action == PackageAction::Update)
            {
                script = LR"PS(& { param($id,$version,$prerelease,[Parameter(ValueFromRemainingArguments=$true)][string[]]$extra) $p=@{Name=$id}; if($version){$p.RequiredVersion=$version}; if($prerelease -eq '1'){$p.AllowPrerelease=$true}; Update-Module @p @extra })PS";
                values = { source.package_id, Trim(options.version), BooleanArgument(options.pre_release) };
            }
            else
            {
                script = LR"PS(& { param($id,[Parameter(ValueFromRemainingArguments=$true)][string[]]$extra) Uninstall-Module -Name $id @extra })PS";
                values = { source.package_id };
            }
        }
        else
        {
            if (action == PackageAction::Install)
            {
                script = LR"PS(& { param($id,$repository,$version,$prerelease,[Parameter(ValueFromRemainingArguments=$true)][string[]]$extra) $p=@{Name=$id;TrustRepository=$true;Scope='CurrentUser'}; if($repository){$p.Repository=$repository}; if($version){$p.Version=$version}; if($prerelease -eq '1'){$p.Prerelease=$true}; Install-PSResource @p @extra })PS";
                values = { source.package_id, SourceSelectorValue(source), Trim(options.version), BooleanArgument(options.pre_release) };
            }
            else if (action == PackageAction::Update)
            {
                script = LR"PS(& { param($id,$repository,$version,$prerelease,$removeOld,[Parameter(ValueFromRemainingArguments=$true)][string[]]$extra) $ErrorActionPreference='Stop'; $p=@{Name=$id;TrustRepository=$true;Force=$true;ErrorAction='Stop'}; if($repository){$p.Repository=$repository}; if($version){$p.Version=$version}; if($prerelease -eq '1'){$p.Prerelease=$true}; Update-PSResource @p @extra; if($removeOld -eq '1'){ $installed=@(Get-InstalledPSResource -Name $id -ErrorAction SilentlyContinue | Sort-Object Version -Descending); if($installed.Count -gt 1){ $keep=$installed[0].Version.ToString(); $installed | Select-Object -Skip 1 | ForEach-Object { $old=$_.Version.ToString(); if($old -ne $keep){ Uninstall-PSResource -Name $id -Version $old -Confirm:$false -ErrorAction Stop } } } } })PS";
                values = {
                    source.package_id,
                    SourceSelectorValue(source),
                    Trim(options.version),
                    BooleanArgument(options.pre_release),
                    BooleanArgument(options.uninstall_previous_on_update),
                };
            }
            else
            {
                script = LR"PS(& { param($id,[Parameter(ValueFromRemainingArguments=$true)][string[]]$extra) Uninstall-PSResource -Name $id @extra })PS";
                values = { source.package_id };
            }
        }

        Append(values, custom_arguments);
        auto command = PowerShell(std::move(executable), std::move(script), std::move(values));
        command.requires_elevation = options.run_as_administrator;
        return Built(std::move(command));
    }
}

namespace winforge::core::packages
{
    std::span<PackageManagerDescriptor const> PackageManagers() noexcept
    {
        return Managers;
    }

    PackageManagerDescriptor const* FindPackageManager(std::wstring_view key) noexcept
    {
        try
        {
            auto const normalized = Lower(key);
            auto const found = std::find_if(Managers.begin(), Managers.end(), [&](PackageManagerDescriptor const& manager)
            {
                return manager.key == normalized;
            });
            return found == Managers.end() ? nullptr : &*found;
        }
        catch (...)
        {
            return nullptr;
        }
    }

    std::span<PackageViewDescriptor const> PackageViews() noexcept
    {
        return Views;
    }

    std::optional<PackageView> PackageViewFromFragment(std::wstring_view fragment) noexcept
    {
        try
        {
            auto const value = Lower(fragment);
            if (value == L"discover") return PackageView::Discover;
            if (value == L"updates") return PackageView::Updates;
            if (value == L"installed") return PackageView::Installed;
            if (value == L"bundle" || value == L"bundles") return PackageView::Bundles;
            if (value == L"source" || value == L"sources") return PackageView::Sources;
            if (value == L"ignored") return PackageView::Ignored;
            if (value == L"setup") return PackageView::Setup;
            if (value == L"settings") return PackageView::Settings;
            if (value == L"operations") return PackageView::Operations;
            return std::nullopt;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    std::optional<std::wstring_view> PackageViewFragment(PackageView view) noexcept
    {
        switch (view)
        {
        case PackageView::Discover: return L"discover";
        case PackageView::Updates: return L"updates";
        case PackageView::Installed: return L"installed";
        case PackageView::Bundles: return L"bundles";
        case PackageView::Sources: return L"sources";
        case PackageView::Ignored: return L"ignored";
        case PackageView::Setup: return L"setup";
        case PackageView::Settings: return L"settings";
        case PackageView::Operations: return L"operations";
        default: return std::nullopt;
        }
    }

    std::optional<std::wstring> PackageNavigationKey(PackageView view)
    {
        auto const fragment = PackageViewFragment(view);
        if (!fragment)
        {
            return std::nullopt;
        }
        return std::wstring(L"module.packages#") + std::wstring(*fragment);
    }

    void SortPackageItems(std::vector<PackageItem>& items, PackageSortMode sort_mode) noexcept
    {
        auto const by_manager = [](PackageItem const& left, PackageItem const& right)
        {
            if (left.manager_key != right.manager_key) return left.manager_key < right.manager_key;
            if (left.name != right.name) return left.name < right.name;
            if (left.id != right.id) return left.id < right.id;
            if (left.source != right.source) return left.source < right.source;
            return left.version < right.version;
        };
        auto const by_name = [](PackageItem const& left, PackageItem const& right)
        {
            if (left.name != right.name) return left.name < right.name;
            if (left.manager_key != right.manager_key) return left.manager_key < right.manager_key;
            if (left.id != right.id) return left.id < right.id;
            if (left.source != right.source) return left.source < right.source;
            return left.version < right.version;
        };
        auto const by_source = [](PackageItem const& left, PackageItem const& right)
        {
            if (left.source != right.source) return left.source < right.source;
            if (left.manager_key != right.manager_key) return left.manager_key < right.manager_key;
            if (left.name != right.name) return left.name < right.name;
            if (left.id != right.id) return left.id < right.id;
            return left.version < right.version;
        };
        auto const by_id = [](PackageItem const& left, PackageItem const& right)
        {
            if (left.id != right.id) return left.id < right.id;
            if (left.manager_key != right.manager_key) return left.manager_key < right.manager_key;
            if (left.name != right.name) return left.name < right.name;
            if (left.source != right.source) return left.source < right.source;
            return left.version < right.version;
        };

        switch (sort_mode)
        {
        case PackageSortMode::Name:
            std::stable_sort(items.begin(), items.end(), by_name);
            break;
        case PackageSortMode::Source:
            std::stable_sort(items.begin(), items.end(), by_source);
            break;
        case PackageSortMode::Id:
            std::stable_sort(items.begin(), items.end(), by_id);
            break;
        default:
            std::stable_sort(items.begin(), items.end(), by_manager);
            break;
        }
    }

    ValidationResult ValidatePackageReference(
        std::wstring_view manager_key,
        std::wstring_view package_id) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            auto const id = Trim(package_id);
            if (!FindPackageManager(key))
            {
                return Invalid(L"invalid-manager");
            }
            if (id.empty() || id.size() > 256)
            {
                return Invalid(L"invalid-package-id");
            }

            bool valid = false;
            if (key == L"npm" || key == L"bun")
            {
                if (id.front() != L'@')
                {
                    valid = id.find(L'/') == std::wstring::npos && IsSafeSegment(id, true);
                }
                else
                {
                    auto const slash = id.find(L'/');
                    valid = slash > 1
                        && slash == id.rfind(L'/')
                        && slash + 1 < id.size()
                        && IsSafeSegment(std::wstring_view(id).substr(1, slash - 1), true)
                        && IsSafeSegment(std::wstring_view(id).substr(slash + 1), true);
                }
            }
            else if (key == L"scoop")
            {
                auto const slash = id.find(L'/');
                valid = slash == std::wstring::npos
                    ? IsSafeSegment(id, false)
                    : slash > 0
                        && slash == id.rfind(L'/')
                        && slash + 1 < id.size()
                        && IsSafeSegment(std::wstring_view(id).substr(0, slash), false)
                        && IsSafeSegment(std::wstring_view(id).substr(slash + 1), false);
            }
            else if (key == L"vcpkg")
            {
                auto const colon = id.find(L':');
                if (colon != std::wstring::npos && colon != id.rfind(L':'))
                {
                    valid = false;
                }
                else
                {
                    auto const spec = colon == std::wstring::npos
                        ? std::wstring_view(id)
                        : std::wstring_view(id).substr(0, colon);
                    auto const triplet = colon == std::wstring::npos
                        ? std::wstring_view{}
                        : std::wstring_view(id).substr(colon + 1);
                    auto const open = spec.find(L'[');
                    auto const close = spec.find(L']');
                    if (!triplet.empty() && !IsSafeSegment(triplet, false))
                    {
                        valid = false;
                    }
                    else if (open == std::wstring_view::npos && close == std::wstring_view::npos)
                    {
                        valid = IsSafeSegment(spec, false);
                    }
                    else if (open > 0
                        && close == spec.size() - 1
                        && close > open + 1
                        && open == spec.rfind(L'[')
                        && close == spec.rfind(L']')
                        && IsSafeSegment(spec.substr(0, open), false))
                    {
                        valid = true;
                        auto features = spec.substr(open + 1, close - open - 1);
                        while (!features.empty())
                        {
                            auto const comma = features.find(L',');
                            auto const feature = features.substr(0, comma);
                            if (!IsSafeSegment(feature, false))
                            {
                                valid = false;
                                break;
                            }
                            if (comma == std::wstring_view::npos)
                            {
                                break;
                            }
                            features.remove_prefix(comma + 1);
                        }
                    }
                }
            }
            else
            {
                valid = IsSafeSegment(id, false);
            }

            return valid ? Valid() : Invalid(L"invalid-package-id");
        }
        catch (...)
        {
            return Invalid(L"invalid-package-id");
        }
    }

    ArgumentParseResult ParseCustomArguments(std::wstring_view value) noexcept
    {
        try
        {
            if (value.size() > 16'384)
            {
                return { {}, L"options-too-large" };
            }

            ArgumentParseResult result;
            std::wstring current;
            bool quoted = false;
            bool started = false;
            auto flush = [&]()
            {
                if (!started)
                {
                    return;
                }
                result.arguments.push_back(current);
                current.clear();
                started = false;
            };

            for (std::size_t index = 0; index < value.size(); ++index)
            {
                auto const character = value[index];
                if (character == L'\\' && index + 1 < value.size() && value[index + 1] == L'"')
                {
                    current.push_back(L'"');
                    started = true;
                    ++index;
                    continue;
                }
                if (character == L'"')
                {
                    quoted = !quoted;
                    started = true;
                    continue;
                }
                if (!quoted && std::iswspace(character))
                {
                    flush();
                    continue;
                }
                constexpr std::wstring_view forbidden{ L"`&|<>;^%!" };
                if (character == L'\0'
                    || std::iswcntrl(character)
                    || forbidden.find(character) != std::wstring_view::npos)
                {
                    return { {}, L"invalid-custom-arguments" };
                }
                current.push_back(character);
                started = true;
                if (current.size() > 2'048)
                {
                    return { {}, L"options-too-large" };
                }
            }
            if (quoted)
            {
                return { {}, L"invalid-custom-arguments" };
            }
            flush();
            if (result.arguments.size() > 128)
            {
                return { {}, L"options-too-large" };
            }
            if (std::any_of(result.arguments.begin(), result.arguments.end(), IsDeniedCustomOption))
            {
                return { {}, L"custom-arguments-source-override" };
            }
            return result;
        }
        catch (...)
        {
            return { {}, L"invalid-custom-arguments" };
        }
    }

    ValidationResult ValidateInstallOptions(InstallOptions const& options) noexcept
    {
        try
        {
            if (!IsSafeVersion(options.version))
            {
                return Invalid(L"invalid-version");
            }
            auto const scope = Lower(options.scope);
            if (!scope.empty()
                && scope != L"user"
                && scope != L"machine"
                && scope != L"currentuser"
                && scope != L"allusers")
            {
                return Invalid(L"invalid-scope");
            }
            auto const architecture = Lower(options.architecture);
            if (!architecture.empty()
                && architecture != L"x64"
                && architecture != L"x86"
                && architecture != L"arm64"
                && architecture != L"arm"
                && architecture != L"neutral")
            {
                return Invalid(L"invalid-architecture");
            }
            if (!IsSafeInstallPath(options.custom_install_location))
            {
                return Invalid(L"invalid-install-location");
            }
            if (HasShellHook(options))
            {
                return Invalid(L"shell-hooks-unsupported");
            }
            for (auto const custom : {
                std::wstring_view(options.custom_args_install),
                std::wstring_view(options.custom_args_update),
                std::wstring_view(options.custom_args_uninstall) })
            {
                auto const parsed = ParseCustomArguments(custom);
                if (!parsed)
                {
                    return Invalid(parsed.error_code);
                }
            }
            if (!options.kill_before_operation.empty() || options.force_kill)
            {
                return Invalid(L"process-kill-orchestration-unsupported");
            }
            return Valid();
        }
        catch (...)
        {
            return Invalid(L"invalid-install-options");
        }
    }

    SourceResolutionResult ResolvePackageSource(
        std::wstring_view manager_key,
        std::wstring_view package_id,
        std::wstring_view source,
        PackageAction action) noexcept
    {
        try
        {
            auto const reference = ValidatePackageReference(manager_key, package_id);
            if (!reference)
            {
                return { std::nullopt, reference.code };
            }
            if (!IsOperation(action))
            {
                return { std::nullopt, L"invalid-action" };
            }

            auto const key = Lower(manager_key);
            auto const id = Trim(package_id);
            auto const requested = Trim(source);
            SourceResolution resolution{ {}, id, {} };
            if (requested.empty())
            {
                return { std::move(resolution), {} };
            }
            if (IsExplicitlyIncompatibleSource(requested))
            {
                return { std::nullopt, L"source-local-or-unknown" };
            }

            if (key == L"winget" || key == L"choco")
            {
                if (!IsSafeToken(requested))
                {
                    return { std::nullopt, L"invalid-package-source" };
                }
                resolution.normalized_source = Lower(requested);
                resolution.arguments = { L"--source", requested };
            }
            else if (key == L"scoop")
            {
                if (!IsSafeToken(requested))
                {
                    return { std::nullopt, L"invalid-package-source" };
                }
                resolution.normalized_source = Lower(requested);
                if (action == PackageAction::Install)
                {
                    auto const slash = id.find(L'/');
                    if (slash == std::wstring::npos)
                    {
                        resolution.package_id = requested + L"/" + id;
                    }
                    else if (slash == 0
                        || slash != id.rfind(L'/')
                        || Lower(std::wstring_view(id).substr(0, slash)) != Lower(requested))
                    {
                        return { std::nullopt, L"source-id-mismatch" };
                    }
                }
            }
            else if (key == L"pip")
            {
                if (!EqualsAny(requested, { L"pypi", L"pypi.org", L"https://pypi.org/simple", L"https://pypi.org/simple/" }))
                {
                    return { std::nullopt, L"source-unsupported" };
                }
                resolution.normalized_source = L"pypi.org";
                if (action != PackageAction::Uninstall)
                {
                    resolution.arguments = { L"--index-url", L"https://pypi.org/simple" };
                }
            }
            else if (key == L"npm" || key == L"bun")
            {
                if (!EqualsAny(requested, { L"npm", L"npmjs.org", L"registry.npmjs.org", L"https://registry.npmjs.org", L"https://registry.npmjs.org/" }))
                {
                    return { std::nullopt, L"source-unsupported" };
                }
                resolution.normalized_source = L"npmjs.org";
                if (action != PackageAction::Uninstall)
                {
                    resolution.arguments = { L"--registry", L"https://registry.npmjs.org/" };
                }
            }
            else if (key == L"dotnet")
            {
                if (!EqualsAny(requested, { L"nuget", L"nuget.org", L"https://api.nuget.org/v3/index.json" }))
                {
                    return { std::nullopt, L"source-unsupported" };
                }
                resolution.normalized_source = L"nuget.org";
                if (action != PackageAction::Uninstall)
                {
                    resolution.arguments = { L"--add-source", L"https://api.nuget.org/v3/index.json" };
                }
            }
            else if (key == L"cargo")
            {
                if (!EqualsAny(requested, { L"crates.io", L"crates-io" }))
                {
                    return { std::nullopt, L"source-unsupported" };
                }
                resolution.normalized_source = L"crates.io";
                if (action != PackageAction::Uninstall)
                {
                    resolution.arguments = { L"--registry", L"crates-io" };
                }
            }
            else if (key == L"psgallery" || key == L"pwsh7")
            {
                if (!IsSafeToken(requested))
                {
                    return { std::nullopt, L"invalid-package-source" };
                }
                resolution.normalized_source = Lower(requested);
                bool const supports_selector = key == L"psgallery"
                    ? action == PackageAction::Install
                    : action != PackageAction::Uninstall;
                if (supports_selector)
                {
                    resolution.arguments = { L"-Repository", requested };
                }
            }
            else if (key == L"vcpkg")
            {
                if (!IsSafeToken(requested))
                {
                    return { std::nullopt, L"invalid-package-source" };
                }
                auto const colon = id.rfind(L':');
                if (colon == std::wstring::npos)
                {
                    if (Lower(requested) != L"vcpkg")
                    {
                        return { std::nullopt, L"source-id-mismatch" };
                    }
                }
                else if (colon + 1 >= id.size()
                    || Lower(std::wstring_view(id).substr(colon + 1)) != Lower(requested))
                {
                    return { std::nullopt, L"source-id-mismatch" };
                }
                resolution.normalized_source = Lower(requested);
            }
            else
            {
                return { std::nullopt, L"source-unsupported" };
            }
            return { std::move(resolution), {} };
        }
        catch (...)
        {
            return { std::nullopt, L"invalid-package-source" };
        }
    }

    CommandBuildResult BuildProbeCommand(std::wstring_view manager_key) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            auto const manager = FindPackageManager(key);
            if (!manager)
            {
                return Failed(L"invalid-manager");
            }
            if (key == L"vcpkg")
            {
                return Built(Process(std::wstring(manager->executable), { L"version" }));
            }
            if (key == L"psgallery")
            {
                return Built(PowerShell(L"powershell", L"$PSVersionTable.PSVersion.ToString()"));
            }
            if (key == L"pwsh7")
            {
                return Built(PowerShell(L"pwsh", L"$PSVersionTable.PSVersion.Major"));
            }
            return Built(Process(std::wstring(manager->executable), { L"--version" }));
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }

    CommandBuildResult BuildSearchCommand(
        std::wstring_view manager_key,
        std::wstring_view query) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            if (!FindPackageManager(key))
            {
                return Failed(L"invalid-manager");
            }
            auto const query_validation = ValidateQuery(query);
            if (!query_validation)
            {
                return Failed(query_validation.code);
            }
            auto const value = Trim(query);

            if (key == L"winget") return Built(Process(L"winget", { L"search", L"--query", value, L"--accept-source-agreements", L"--disable-interactivity" }, CommandPostProcess::WingetTable));
            if (key == L"scoop") return Built(Process(L"scoop", { L"search", value }, CommandPostProcess::ScoopSearch));
            if (key == L"choco") return Built(Process(L"choco", { L"search", value, L"--limit-output" }, CommandPostProcess::ChocolateyDelimited));
            if (key == L"npm") return Built(Process(L"npm", { L"search", value, L"--json" }, CommandPostProcess::NpmJson));
            if (key == L"dotnet") return Built(Process(L"dotnet", { L"tool", L"search", value }, CommandPostProcess::DotnetTable));
            if (key == L"cargo") return Built(Process(L"cargo", { L"search", value }, CommandPostProcess::CargoSearch));
            if (key == L"vcpkg") return Built(Process(L"vcpkg", { L"search", value }, CommandPostProcess::VcpkgSearch));
            if (key == L"psgallery")
            {
                auto const script = LR"PS(& { param($query) Find-Module -Name ('*' + $query + '*') -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json })PS";
                return Built(PowerShell(L"powershell", script, { value }, CommandPostProcess::PowerShellPackagesJson));
            }
            if (key == L"pwsh7")
            {
                auto const script = LR"PS(& { param($query) Find-PSResource -Name ('*' + $query + '*') -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json })PS";
                return Built(PowerShell(L"pwsh", script, { value }, CommandPostProcess::PowerShellPackagesJson));
            }
            if (key == L"pip" || key == L"bun")
            {
                CommandSpec command;
                command.transport = CommandTransport::HttpGet;
                command.request_uri = key == L"pip"
                    ? L"https://pypi.org/simple/"
                    : L"https://registry.npmjs.org/-/v1/search?size=100";
                command.query = value;
                command.post_process = key == L"pip"
                    ? CommandPostProcess::PyPiSearch
                    : CommandPostProcess::NpmRegistrySearch;
                return Built(std::move(command));
            }
            return Failed(L"command-unsupported");
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }

    CommandBuildResult BuildInstalledCommand(std::wstring_view manager_key) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            if (!FindPackageManager(key)) return Failed(L"invalid-manager");
            if (key == L"winget") return Built(Process(L"winget", { L"list", L"--accept-source-agreements", L"--disable-interactivity" }, CommandPostProcess::WingetTable));
            if (key == L"scoop") return Built(Process(L"scoop", { L"export" }, CommandPostProcess::ScoopExport));
            if (key == L"choco") return Built(Process(L"choco", { L"list", L"--local-only", L"--limit-output" }, CommandPostProcess::ChocolateyDelimited));
            if (key == L"pip") return Built(Process(L"pip", { L"list", L"--format=json" }, CommandPostProcess::JsonPackages));
            if (key == L"npm") return Built(Process(L"npm", { L"ls", L"-g", L"--depth=0", L"--json" }, CommandPostProcess::NpmJson));
            if (key == L"dotnet") return Built(Process(L"dotnet", { L"tool", L"list", L"-g" }, CommandPostProcess::DotnetTable));
            if (key == L"cargo") return Built(Process(L"cargo", { L"install", L"--list" }, CommandPostProcess::CargoInstalled));
            if (key == L"vcpkg") return Built(Process(L"vcpkg", { L"list" }, CommandPostProcess::VcpkgInstalled));
            if (key == L"bun")
            {
                auto command = Process(L"bun", { L"pm", L"ls" }, CommandPostProcess::BunInstalled);
                command.working_directory = CommandWorkingDirectory::BunGlobalPackages;
                return Built(std::move(command));
            }
            if (key == L"psgallery")
            {
                auto const script = L"Get-InstalledModule -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json";
                return Built(PowerShell(L"powershell", script, {}, CommandPostProcess::PowerShellPackagesJson));
            }
            if (key == L"pwsh7")
            {
                auto const script = L"Get-InstalledPSResource -ErrorAction SilentlyContinue | Select-Object Name,Version | ConvertTo-Json";
                return Built(PowerShell(L"pwsh", script, {}, CommandPostProcess::PowerShellPackagesJson));
            }
            return Failed(L"command-unsupported");
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }

    CommandBuildResult BuildUpdatesCommand(std::wstring_view manager_key) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            if (!FindPackageManager(key)) return Failed(L"invalid-manager");
            if (key == L"winget") return Built(Process(L"winget", { L"upgrade", L"--accept-source-agreements", L"--disable-interactivity" }, CommandPostProcess::WingetTable));
            if (key == L"scoop") return Built(Process(L"scoop", { L"status" }, CommandPostProcess::ScoopSearch));
            if (key == L"choco") return Built(Process(L"choco", { L"outdated", L"--limit-output" }, CommandPostProcess::ChocolateyDelimited));
            if (key == L"pip") return Built(Process(L"pip", { L"list", L"--outdated", L"--format=json" }, CommandPostProcess::JsonPackages));
            if (key == L"npm") return Built(Process(L"npm", { L"outdated", L"-g", L"--json" }, CommandPostProcess::NpmJson));
            if (key == L"dotnet") return Built(Process(L"dotnet", { L"tool", L"list", L"-g" }, CommandPostProcess::DotnetUpdatesFromNuGet));
            if (key == L"cargo") return Built(Process(L"cargo", { L"install-update", L"--list" }, CommandPostProcess::CargoUpdates));
            if (key == L"vcpkg") return Built(Process(L"vcpkg", { L"update" }, CommandPostProcess::VcpkgUpdates));
            if (key == L"bun")
            {
                auto command = Process(L"bun", { L"outdated" }, CommandPostProcess::BunUpdates);
                command.working_directory = CommandWorkingDirectory::BunGlobalPackages;
                return Built(std::move(command));
            }
            if (key == L"psgallery" || key == L"pwsh7")
            {
                auto const installed = key == L"psgallery" ? L"Get-InstalledModule" : L"Get-InstalledPSResource";
                auto const available = key == L"psgallery" ? L"Find-Module" : L"Find-PSResource";
                std::wstring script = L"$ErrorActionPreference='SilentlyContinue'; $updates=@(& " + std::wstring(installed)
                    + L" | Group-Object Name | ForEach-Object { $i=$_.Group | Sort-Object Version -Descending | Select-Object -First 1; $a="
                    + std::wstring(available)
                    + L" -Name $i.Name -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1; if($null -ne $a -and $a.Version -gt $i.Version){ [pscustomobject]@{Name=$i.Name;Version=$i.Version.ToString();AvailableVersion=$a.Version.ToString();Repository=$a.Repository} } }); ConvertTo-Json -InputObject @($updates) -Depth 3 -Compress";
                return Built(PowerShell(
                    key == L"psgallery" ? L"powershell" : L"pwsh",
                    std::move(script),
                    {},
                    CommandPostProcess::PowerShellPackagesJson));
            }
            return Failed(L"command-unsupported");
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }

    CommandBuildResult BuildPackageActionCommand(
        std::wstring_view manager_key,
        std::wstring_view package_id,
        std::wstring_view source,
        PackageAction action,
        InstallOptions const& options) noexcept
    {
        try
        {
            if (!IsOperation(action))
            {
                return Failed(L"invalid-action");
            }
            auto const key = Lower(manager_key);
            auto const reference = ValidatePackageReference(key, package_id);
            if (!reference)
            {
                return Failed(reference.code);
            }
            auto const option_validation = ValidateInstallOptions(options);
            if (!option_validation)
            {
                return Failed(option_validation.code);
            }
            auto const resolved = ResolvePackageSource(key, package_id, source, action);
            if (!resolved)
            {
                return Failed(resolved.error_code);
            }
            auto custom = ParseCustomArguments(CustomArgumentsFor(options, action));
            if (!custom)
            {
                return Failed(custom.error_code);
            }
            auto const& package = resolved.resolution->package_id;

            if (key == L"psgallery" || key == L"pwsh7")
            {
                return BuildPowerShellOperation(key, *resolved.resolution, action, options, std::move(custom.arguments));
            }

            std::vector<std::wstring> arguments;
            if (key == L"winget")
            {
                arguments = { action == PackageAction::Install ? L"install" : action == PackageAction::Update ? L"upgrade" : L"uninstall", L"--id", package, L"-e" };
                if (action != PackageAction::Uninstall)
                {
                    Append(arguments, { L"--accept-source-agreements", L"--accept-package-agreements" });
                }
                if (!Trim(options.version).empty() && action != PackageAction::Uninstall) Append(arguments, { L"--version", Trim(options.version) });
                if (!Trim(options.scope).empty()) Append(arguments, { L"--scope", Trim(options.scope) });
                if (!Trim(options.architecture).empty() && action != PackageAction::Uninstall) Append(arguments, { L"--architecture", Trim(options.architecture) });
                if (!Trim(options.custom_install_location).empty() && action != PackageAction::Uninstall) Append(arguments, { L"--location", Trim(options.custom_install_location) });
                if (options.skip_hash_check && action != PackageAction::Uninstall) arguments.push_back(L"--ignore-security-hash");
                if (options.interactive)
                {
                    arguments.push_back(L"--interactive");
                }
                else
                {
                    Append(arguments, { L"--silent", L"--disable-interactivity" });
                }
            }
            else if (key == L"scoop")
            {
                arguments.push_back(action == PackageAction::Install ? L"install" : action == PackageAction::Update ? L"update" : L"uninstall");
                auto package_argument = package;
                if (action == PackageAction::Install && !Trim(options.version).empty()) package_argument += L"@" + Trim(options.version);
                arguments.push_back(std::move(package_argument));
                if (options.skip_hash_check && action != PackageAction::Uninstall) arguments.push_back(L"--skip");
                if (options.remove_data_on_uninstall && action == PackageAction::Uninstall) arguments.push_back(L"--purge");
            }
            else if (key == L"choco")
            {
                arguments = { action == PackageAction::Install ? L"install" : action == PackageAction::Update ? L"upgrade" : L"uninstall", package, L"-y" };
                if (!Trim(options.version).empty() && action != PackageAction::Uninstall) Append(arguments, { L"--version", Trim(options.version) });
                if (options.pre_release && action != PackageAction::Uninstall) arguments.push_back(L"--pre");
                if (options.skip_hash_check && action != PackageAction::Uninstall) arguments.push_back(L"--ignore-checksums");
                if (options.interactive && action != PackageAction::Uninstall) arguments.push_back(L"--notsilent");
                if (options.remove_data_on_uninstall && action == PackageAction::Uninstall) arguments.push_back(L"--remove-dependencies");
            }
            else if (key == L"pip")
            {
                if (action == PackageAction::Uninstall)
                {
                    arguments = { L"uninstall", L"-y", package };
                }
                else
                {
                    arguments = { L"install" };
                    if (action == PackageAction::Update) arguments.push_back(L"--upgrade");
                    if (options.pre_release) arguments.push_back(L"--pre");
                    auto package_argument = package;
                    if (!Trim(options.version).empty()) package_argument += L"==" + Trim(options.version);
                    arguments.push_back(std::move(package_argument));
                }
            }
            else if (key == L"npm" || key == L"bun")
            {
                if (action == PackageAction::Uninstall)
                {
                    arguments = { key == L"npm" ? L"uninstall" : L"remove", L"-g", package };
                }
                else
                {
                    arguments = { key == L"npm" ? L"install" : L"add", L"-g" };
                    auto package_argument = package;
                    if (!Trim(options.version).empty()) package_argument += L"@" + Trim(options.version);
                    else if (options.pre_release) package_argument += L"@next";
                    else if (action == PackageAction::Update) package_argument += L"@latest";
                    arguments.push_back(std::move(package_argument));
                }
            }
            else if (key == L"dotnet")
            {
                arguments = { L"tool", action == PackageAction::Install ? L"install" : action == PackageAction::Update ? L"update" : L"uninstall", package };
                if (!Trim(options.custom_install_location).empty()) Append(arguments, { L"--tool-path", Trim(options.custom_install_location) });
                else arguments.push_back(L"--global");
                if (!Trim(options.version).empty() && action != PackageAction::Uninstall) Append(arguments, { L"--version", Trim(options.version) });
                if (options.pre_release && action != PackageAction::Uninstall) arguments.push_back(L"--prerelease");
            }
            else if (key == L"cargo")
            {
                arguments = { action == PackageAction::Uninstall ? L"uninstall" : L"install", package };
                if (action == PackageAction::Update) arguments.push_back(L"--force");
                if (!Trim(options.version).empty() && action != PackageAction::Uninstall) Append(arguments, { L"--version", Trim(options.version) });
                if (!Trim(options.custom_install_location).empty()) Append(arguments, { L"--root", Trim(options.custom_install_location) });
            }
            else if (key == L"vcpkg")
            {
                arguments = { action == PackageAction::Install ? L"install" : action == PackageAction::Update ? L"upgrade" : L"remove", package };
                if (action == PackageAction::Update) arguments.push_back(L"--no-dry-run");
            }
            else
            {
                return Failed(L"command-unsupported");
            }

            Append(arguments, resolved.resolution->arguments);
            Append(arguments, custom.arguments);
            auto command = Process(key == L"psgallery" ? L"powershell" : key, std::move(arguments));
            command.requires_elevation = options.run_as_administrator;
            return Built(std::move(command));
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }

    std::wstring FormatCommandPreview(CommandSpec const& command)
    {
        switch (command.transport)
        {
        case CommandTransport::HttpGet:
            return L"GET " + QuotePreviewArgument(command.request_uri);
        case CommandTransport::StaticText:
            return L"[static] " + command.static_text;
        default:
            break;
        }

        std::wstring preview = QuotePreviewArgument(command.executable);
        for (auto const& argument : command.arguments)
        {
            preview += L' ';
            preview += QuotePreviewArgument(argument);
        }
        if (command.working_directory == CommandWorkingDirectory::BunGlobalPackages)
        {
            preview += L"  [cwd: Bun global packages]";
        }
        if (command.requires_elevation)
        {
            preview += L"  [requires elevation]";
        }
        return preview;
    }

    CommandBuildResult BuildDetailsCommand(
        std::wstring_view manager_key,
        std::wstring_view package_id) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            auto const validation = ValidatePackageReference(key, package_id);
            if (!validation) return Failed(validation.code);
            auto const id = Trim(package_id);
            if (key == L"winget") return Built(Process(L"winget", { L"show", L"--id", id, L"-e", L"--accept-source-agreements", L"--disable-interactivity" }));
            if (key == L"scoop") return Built(Process(L"scoop", { L"info", id }));
            if (key == L"choco") return Built(Process(L"choco", { L"info", id }));
            if (key == L"pip") return Built(Process(L"pip", { L"show", id }));
            if (key == L"npm" || key == L"bun") return Built(Process(L"npm", { L"view", id }));
            if (key == L"dotnet") return Built(Process(L"dotnet", { L"tool", L"search", id, L"--detail" }));
            if (key == L"cargo") return Built(Process(L"cargo", { L"search", id }));
            if (key == L"vcpkg") return Built(Process(L"vcpkg", { L"search", id }));
            if (key == L"psgallery")
            {
                auto const script = LR"PS(& { param($id) Find-Module -Name $id | Format-List Name,Version,Author,ProjectUri,Description | Out-String -Width 200 })PS";
                return Built(PowerShell(L"powershell", script, { id }));
            }
            if (key == L"pwsh7")
            {
                auto const script = LR"PS(& { param($id) Find-PSResource -Name $id | Format-List Name,Version,Author,Repository,Description | Out-String -Width 200 })PS";
                return Built(PowerShell(L"pwsh", script, { id }));
            }
            return Failed(L"command-unsupported");
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }

    CommandBuildResult BuildSourcesCommand(std::wstring_view manager_key) noexcept
    {
        try
        {
            auto const key = Lower(manager_key);
            if (!FindPackageManager(key)) return Failed(L"invalid-manager");
            if (key == L"winget") return Built(Process(L"winget", { L"source", L"list" }));
            if (key == L"scoop") return Built(Process(L"scoop", { L"bucket", L"list" }));
            if (key == L"choco") return Built(Process(L"choco", { L"source", L"list" }));
            if (key == L"pip") return Built(Process(L"pip", { L"config", L"list" }));
            if (key == L"npm") return Built(Process(L"npm", { L"config", L"get", L"registry" }));
            if (key == L"dotnet") return Built(Process(L"dotnet", { L"nuget", L"list", L"source" }));
            if (key == L"vcpkg") return Built(Process(L"vcpkg", { L"x-update-baseline", L"--dry-run" }));
            if (key == L"psgallery")
            {
                auto const script = L"Get-PSRepository | Format-Table Name,InstallationPolicy,SourceLocation | Out-String -Width 200";
                return Built(PowerShell(L"powershell", script));
            }
            if (key == L"pwsh7")
            {
                auto const script = L"Get-PSResourceRepository | Format-Table Name,Trusted,Uri | Out-String -Width 200";
                return Built(PowerShell(L"pwsh", script));
            }
            if (key == L"cargo" || key == L"bun")
            {
                CommandSpec command;
                command.transport = CommandTransport::StaticText;
                command.static_text = key == L"cargo"
                    ? L"crates.io (default registry)"
                    : L"registry.npmjs.org (default registry)";
                return Built(std::move(command));
            }
            return Failed(L"command-unsupported");
        }
        catch (...)
        {
            return Failed(L"command-build-failed");
        }
    }
}
