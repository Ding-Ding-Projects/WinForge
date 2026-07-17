#include "AppUninstallerTests.h"

#include "AppUninstaller.h"

#include <iostream>
#include <string_view>
#include <vector>

namespace
{
    struct Suite
    {
        NativeTestCounts counts;

        void Expect(bool condition, std::string_view name)
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
        }
    };
}

NativeTestCounts RunAppUninstallerTests()
{
    using namespace winforge::core::uninstall;
    Suite suite;

    AppPackage resource_name{
        L"Contoso.Tools.Editor",
        L"Contoso.Tools.Editor_1.0.0.0_x64__abc",
        L"Contoso.Tools.Editor_abc",
        L"ms-resource:AppName",
        L"Contoso",
        L"1.0.0.0",
        L"C:\\Program Files\\WindowsApps\\Contoso.Tools.Editor",
    };
    AppPackage friendly_name{
        L"Fabrikam.Player",
        L"Fabrikam.Player_2.0.0.0_x64__def",
        L"Fabrikam.Player_def",
        L"  Fabrikam Player  ",
        L"Fabrikam Studios",
        L"2.0.0.0",
        L"C:\\Program Files\\WindowsApps\\Fabrikam.Player",
    };
    AppPackage publisher_name{
        L"Tailspin.Utility",
        L"Tailspin.Utility_3.0.0.0_x64__ghi",
        L"Tailspin.Utility_ghi",
        L"",
        L"Northwind Publishing",
        L"3.0.0.0",
        L"C:\\Program Files\\WindowsApps\\Tailspin.Utility",
    };
    std::vector<AppPackage> packages{ resource_name, friendly_name, publisher_name };

    suite.Expect(AppPackageShortName(L"Contoso.Tools.Editor") == L"Editor" &&
            AppPackageShortName(L"NoDot") == L"NoDot" &&
            AppPackageShortName(L"Trailing.") == L"Trailing." &&
            AppPackageDisplayName(resource_name) == L"Editor" &&
            AppPackageDisplayName(friendly_name) == L"Fabrikam Player",
        "App Uninstaller normalizes short and ms-resource display names without package I/O");

    auto literal = FilterAppPackages(packages, L"  faBRIKAM  ");
    auto publisher = FilterAppPackages(packages, L"northwind");
    auto family = FilterAppPackages(packages, L"tailspin.utility_ghi");
    suite.Expect(literal.size() == 1 && literal.front().name == L"Fabrikam.Player" &&
            publisher.size() == 1 && publisher.front().name == L"Tailspin.Utility" &&
            family.size() == 1 && family.front().name == L"Tailspin.Utility",
        "App Uninstaller trims a case-insensitive local literal filter across cached identity fields");

    winforge::core::regex::RegexOptions regex_options;
    regex_options.case_sensitive = false;
    auto compiled = winforge::core::regex::SafeRegex::Compile(L"contoso|fabrikam", regex_options);
    AppUninstallerFilterOptions regex_filter;
    auto const regex_compiled = compiled.Ok();
    if (regex_compiled)
    {
        regex_filter.expression = std::make_shared<winforge::core::regex::SafeRegex const>(
            std::move(*compiled.expression));
    }
    auto regex_matches = FilterAppPackages(packages, L"contoso|fabrikam", regex_filter);
    suite.Expect(regex_compiled && regex_matches.size() == 2 &&
            regex_matches.front().name == L"Contoso.Tools.Editor" &&
            regex_matches.back().name == L"Fabrikam.Player",
        "App Uninstaller applies bounded regex only to its already-enumerated local cache and sorts display rows");

    auto case_sensitive = FilterAppPackages(
        packages,
        L"Fabrikam",
        AppUninstallerFilterOptions{ true, {} });
    auto case_sensitive_miss = FilterAppPackages(
        packages,
        L"fabrikam",
        AppUninstallerFilterOptions{ true, {} });
    suite.Expect(case_sensitive.size() == 1 && case_sensitive_miss.empty(),
        "App Uninstaller supports explicit case-sensitive local literal matching");

    auto const valid_path = AppPackageLocalDataPath(
        std::filesystem::path(L"C:\\Users\\Example\\AppData\\Local"),
        L"Contoso.Tools.Editor_abc");
    auto const traversal = AppPackageLocalDataPath(
        std::filesystem::path(L"C:\\Users\\Example\\AppData\\Local"),
        L"..\\escape");
    auto const separator = AppPackageLocalDataPath(
        std::filesystem::path(L"C:\\Users\\Example\\AppData\\Local"),
        L"family/name");
    auto const relative_root = AppPackageLocalDataPath(
        std::filesystem::path(L"relative"),
        L"Contoso.Tools.Editor_abc");
    suite.Expect(valid_path.has_value() &&
            valid_path->parent_path().filename() == L"Packages" &&
            !traversal.has_value() && !separator.has_value() && !relative_root.has_value() &&
            IsSafeAppPackageFamilyName(L"Contoso.Tools.Editor_abc") &&
            !IsSafeAppPackageFamilyName(L".") && !IsSafeAppPackageFamilyName(L"..") &&
            !IsSafeAppPackageFamilyName(L"family\\name") &&
            !IsSafeAppPackageFamilyName(std::wstring(256, L'a')),
        "App Uninstaller deep-cleanup path derivation accepts exactly one safe Package Family Name component");

    suite.Expect(FormatAppPackageSize(0) == L"Unknown" &&
            FormatAppPackageSize(1023) == L"1023 B" &&
            FormatAppPackageSize(1536) == L"1.5 KB" &&
            FormatAppPackageSize(5ull * 1024ull * 1024ull * 1024ull) == L"5 GB",
        "App Uninstaller formats cached size evidence deterministically without filesystem enumeration");

    std::cout << "\nApp Uninstaller tests: " << suite.counts.passed << " passed, "
        << suite.counts.failed << " failed\n";
    return suite.counts;
}
