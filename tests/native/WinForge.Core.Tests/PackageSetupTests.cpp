#include "PackageSetupTests.h"

#include "PackageMutationCoordinator.h"
#include "PackageSetup.h"

#include <algorithm>
#include <array>
#include <iostream>
#include <string_view>
#include <vector>

namespace
{
    bool Contains(std::vector<std::wstring> const& values, std::wstring_view expected)
    {
        return std::find(values.begin(), values.end(), expected) != values.end();
    }
}

NativeTestCounts RunPackageSetupTests()
{
    using namespace winforge::core::packages;

    NativeTestCounts counts;
    auto expect = [&](bool condition, std::string_view name)
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
    };

    auto const entries = PackageSetupEntries();
    auto const managerCount = static_cast<std::size_t>(std::count_if(entries.begin(), entries.end(),
        [](PackageSetupDescriptor const& entry) { return entry.kind == PackageSetupKind::ManagerBootstrap; }));
    auto const dependencyCount = entries.size() - managerCount;
    expect(entries.size() == 25 && managerCount == 11 && dependencyCount == 14,
        "Package Setup preserves eleven manager rows and fourteen managed curated dependencies");

    auto const choco = FindPackageSetupEntry(L" manager-choco ");
    auto const scoop = FindPackageSetupEntry(L"MANAGER-SCOOP");
    auto const ffmpeg = FindPackageSetupEntry(L"dependency-ffmpeg");
    expect(choco && choco->winget_id == L"Chocolatey.Chocolatey" && choco->target_manager_key == L"choco",
        "Package Setup maps Chocolatey bootstrap to the fixed official Winget ID");
    expect(scoop && scoop->winget_id.empty() && ffmpeg && ffmpeg->winget_id == L"Gyan.FFmpeg",
        "Package Setup preserves manual-only Scoop and the managed FFmpeg dependency ID");
    expect(FindPackageSetupEntry(L"not-a-setup-entry") == nullptr,
        "Package Setup rejects unknown descriptor keys");

    expect(EvaluatePackageSetupEntry(*choco, false, false) == PackageSetupReadiness::RequiresWinget &&
        EvaluatePackageSetupEntry(*choco, true, false) == PackageSetupReadiness::ReadyToReview &&
        EvaluatePackageSetupEntry(*choco, true, true) == PackageSetupReadiness::AlreadyAvailable,
        "Package Setup distinguishes missing Winget, reviewable bootstrap, and an available engine");
    expect(EvaluatePackageSetupEntry(*scoop, true, false) == PackageSetupReadiness::ManualOnly &&
        EvaluatePackageSetupEntry(*ffmpeg, true, false) == PackageSetupReadiness::ReadyToReview,
        "Package Setup refuses script bootstrap while keeping immutable curated IDs reviewable");

    auto const chocolateyPackage = BuildPackageSetupPackage(L"manager-choco");
    auto const manualPackage = BuildPackageSetupPackage(L"manager-scoop");
    expect(chocolateyPackage && chocolateyPackage->manager_key == L"winget" &&
        chocolateyPackage->source.empty() && chocolateyPackage->id == L"Chocolatey.Chocolatey" &&
        !manualPackage,
        "Package Setup only builds source-default Winget rows from fixed reviewable descriptors");

    auto const command = chocolateyPackage
        ? BuildPackageActionCommand(
            chocolateyPackage->manager_key,
            chocolateyPackage->id,
            chocolateyPackage->source,
            PackageAction::Install)
        : CommandBuildResult{};
    expect(command && command.command->executable == L"winget" &&
        Contains(command.command->arguments, L"--id") &&
        Contains(command.command->arguments, L"Chocolatey.Chocolatey") &&
        Contains(command.command->arguments, L"--accept-source-agreements") &&
        Contains(command.command->arguments, L"--accept-package-agreements") &&
        Contains(command.command->arguments, L"--disable-interactivity"),
        "Package Setup reuses the native validated Winget argv planner");

    constexpr std::array<std::wstring_view, 5> selectedKeys{
        L"dependency-ffmpeg",
        L"manager-scoop",
        L"dependency-ffmpeg",
        L"manager-choco",
        L"not-a-setup-entry",
    };
    auto const selectedPackages = BuildPackageSetupPackages(selectedKeys);
    expect(selectedPackages.size() == 2 &&
        selectedPackages[0].id == L"Gyan.FFmpeg" &&
        selectedPackages[1].id == L"Chocolatey.Chocolatey",
        "Package Setup retains first occurrence order and drops manual duplicate unknown entries");

    std::vector<std::wstring_view> allReviewableKeys;
    for (auto const& entry : entries)
    {
        if (!entry.winget_id.empty())
        {
            allReviewableKeys.push_back(entry.key);
        }
    }
    auto const allReviewable = BuildPackageSetupPackages(allReviewableKeys);
    expect(allReviewableKeys.size() == 21 &&
        allReviewable.size() == 18 &&
        allReviewable.size() <= MaximumPackageMutationBatchRecords,
        "Package Setup de-duplicates overlapping fixed IDs and remains within the atomic mutation batch bound");

    return counts;
}
