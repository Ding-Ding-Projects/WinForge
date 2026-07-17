#include "PackageSetup.h"

#include <algorithm>
#include <array>
#include <cwctype>
#include <unordered_set>

namespace
{
    using namespace winforge::core::packages;

    constexpr std::array<PackageSetupDescriptor, 25> Entries{
        // Package engines. Empty Winget IDs are an intentional manual-only
        // safety boundary, never a missing fallback command.
        PackageSetupDescriptor{ L"manager-winget", L"Windows Package Manager", L"Windows 套件管理員", L"", L"winget", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-scoop", L"Scoop", L"Scoop（手動安裝）", L"", L"scoop", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-choco", L"Chocolatey", L"Chocolatey", L"Chocolatey.Chocolatey", L"choco", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-pip", L"Python / pip", L"Python / pip", L"Python.Python.3.12", L"pip", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-npm", L"Node.js / npm", L"Node.js / npm", L"OpenJS.NodeJS.LTS", L"npm", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-dotnet", L".NET SDK", L".NET SDK", L"Microsoft.DotNet.SDK.9", L"dotnet", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-psgallery", L"PowerShell Gallery", L"PowerShell Gallery（內建／手動）", L"", L"psgallery", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-pwsh7", L"PowerShell 7", L"PowerShell 7", L"Microsoft.PowerShell", L"pwsh7", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-cargo", L"Rustup / Cargo", L"Rustup / Cargo", L"Rustlang.Rustup", L"cargo", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-bun", L"Bun", L"Bun", L"Oven-sh.Bun", L"bun", PackageSetupKind::ManagerBootstrap },
        PackageSetupDescriptor{ L"manager-vcpkg", L"vcpkg", L"vcpkg（手動安裝）", L"", L"vcpkg", PackageSetupKind::ManagerBootstrap },

        // Managed PackageService.Deps parity. These remain fixed IDs and use
        // Winget's default source policy; Setup never accepts source text.
        PackageSetupDescriptor{ L"dependency-ffmpeg", L"FFmpeg (media engine)", L"FFmpeg（媒體引擎）", L"Gyan.FFmpeg", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-7zip", L"7-Zip", L"7-Zip", L"7zip.7zip", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-git", L"Git", L"Git", L"Git.Git", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-platform-tools", L"Android Platform Tools (adb / fastboot)", L"Android 平台工具（adb / fastboot）", L"Google.PlatformTools", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-scrcpy", L"scrcpy (screen mirror)", L"scrcpy（螢幕鏡像）", L"Genymobile.scrcpy", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-python", L"Python 3", L"Python 3", L"Python.Python.3.12", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-node", L"Node.js LTS", L"Node.js LTS", L"OpenJS.NodeJS.LTS", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-powershell", L"PowerShell 7", L"PowerShell 7", L"Microsoft.PowerShell", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-terminal", L"Windows Terminal", L"Windows Terminal", L"Microsoft.WindowsTerminal", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-vlc", L"VLC media player", L"VLC 媒體播放器", L"VideoLAN.VLC", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-notepadpp", L"Notepad++", L"Notepad++", L"Notepad++.Notepad++", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-docker", L"Docker Desktop", L"Docker Desktop", L"Docker.DockerDesktop", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-veracrypt", L"VeraCrypt (encryption)", L"VeraCrypt（加密）", L"IDRIX.VeraCrypt", L"", PackageSetupKind::CuratedDependency },
        PackageSetupDescriptor{ L"dependency-ssms", L"SQL Server Management Studio (SSMS)", L"SQL Server Management Studio（SSMS）", L"Microsoft.SQLServerManagementStudio", L"", PackageSetupKind::CuratedDependency },
    };

    std::wstring Normalize(std::wstring_view value)
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

        std::wstring normalized(first, last);
        std::transform(normalized.begin(), normalized.end(), normalized.begin(), [](wchar_t character)
        {
            return static_cast<wchar_t>(std::towlower(character));
        });
        return normalized;
    }
}

namespace winforge::core::packages
{
    std::span<PackageSetupDescriptor const> PackageSetupEntries() noexcept
    {
        return Entries;
    }

    PackageSetupDescriptor const* FindPackageSetupEntry(std::wstring_view key) noexcept
    {
        try
        {
            auto const normalized = Normalize(key);
            auto const found = std::find_if(Entries.begin(), Entries.end(), [&](PackageSetupDescriptor const& entry)
            {
                return entry.key == normalized;
            });
            return found == Entries.end() ? nullptr : &*found;
        }
        catch (...)
        {
            return nullptr;
        }
    }

    PackageSetupReadiness EvaluatePackageSetupEntry(
        PackageSetupDescriptor const& descriptor,
        bool winget_available,
        bool target_manager_available) noexcept
    {
        if (descriptor.kind == PackageSetupKind::ManagerBootstrap &&
            !descriptor.target_manager_key.empty() &&
            target_manager_available)
        {
            return PackageSetupReadiness::AlreadyAvailable;
        }
        if (descriptor.winget_id.empty())
        {
            return PackageSetupReadiness::ManualOnly;
        }
        return winget_available
            ? PackageSetupReadiness::ReadyToReview
            : PackageSetupReadiness::RequiresWinget;
    }

    std::optional<PackageItem> BuildPackageSetupPackage(std::wstring_view descriptor_key) noexcept
    {
        try
        {
            auto const descriptor = FindPackageSetupEntry(descriptor_key);
            if (!descriptor || descriptor->winget_id.empty())
            {
                return std::nullopt;
            }

            if (!ValidatePackageReference(L"winget", descriptor->winget_id))
            {
                return std::nullopt;
            }

            PackageItem package;
            package.name = std::wstring(descriptor->name_en);
            package.id = std::wstring(descriptor->winget_id);
            package.manager_key = L"winget";
            // Empty means the safe default Winget source, never a text value
            // copied from a descriptor or from an imported bundle.
            package.source.clear();
            return package;
        }
        catch (...)
        {
            return std::nullopt;
        }
    }

    std::vector<PackageItem> BuildPackageSetupPackages(
        std::span<std::wstring_view const> descriptor_keys)
    {
        std::vector<PackageItem> packages;
        packages.reserve(descriptor_keys.size());
        std::unordered_set<std::wstring> seen_ids;

        for (auto const key : descriptor_keys)
        {
            auto package = BuildPackageSetupPackage(key);
            if (!package)
            {
                continue;
            }

            auto identity = Normalize(package->id);
            if (!seen_ids.emplace(std::move(identity)).second)
            {
                continue;
            }
            packages.push_back(std::move(*package));
        }
        return packages;
    }
}
