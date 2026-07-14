#pragma once

#include "MainWindow.g.h"
#include "../WinForge.Core/BinaryText.h"
#include "../WinForge.Core/CaseConvert.h"
#include "../WinForge.Core/CheckDigit.h"
#include "../WinForge.Core/CommandLine.h"
#include "../WinForge.Core/ModuleRecord.h"
#include "../WinForge.Core/PackageRuntime.h"
#include "../WinForge.Core/RouteIndex.h"

#include <cstdint>
#include <filesystem>
#include <stop_token>

namespace winrt::WinForge::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();

    private:
        struct PackageManagerRunState
        {
            std::wstring manager_key;
            bool success{ false };
            bool parser_supported{ true };
            bool requires_runtime_resolution{ false };
            std::size_t package_count{ 0 };
            std::wstring output;
            std::wstring diagnostic;
        };

        struct PackageIgnoredRule
        {
            std::wstring manager_key;
            std::wstring package_id;
            std::wstring package_name;
            std::wstring version;
        };

        struct PackagePinnedRule
        {
            std::wstring manager_key;
            std::wstring package_id;
            std::wstring package_name;
            std::wstring version;
        };

        struct PackageSnoozedRule
        {
            std::wstring manager_key;
            std::wstring package_id;
            std::wstring package_name;
            std::wstring version;
            std::int64_t until_epoch_seconds{ 0 };
        };

        struct PackageOperationEntry
        {
            std::wstring id;
            std::wstring title;
            std::wstring details;
            std::wstring status;
            std::int64_t created_epoch_seconds{ 0 };
            int32_t retry_count{ 0 };
        };

        winforge::core::LanguageMode m_language{ winforge::core::LanguageMode::Bilingual };
        std::vector<winforge::core::ModuleRecord> m_modules;
        winforge::core::RouteIndex m_routeIndex;
        Microsoft::UI::Xaml::Controls::NavigationView m_navigation{ nullptr };
        Microsoft::UI::Xaml::Controls::Grid m_content{ nullptr };
        Microsoft::UI::Xaml::Controls::AutoSuggestBox m_search{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_languagePicker{ nullptr };
        Microsoft::UI::Xaml::Controls::ListView m_allAppsList{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_allAppsCount{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_packageViewPicker{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_packageSortPicker{ nullptr };
        Microsoft::UI::Xaml::Controls::AutoSuggestBox m_packageSearchBox{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packagePrimaryAction{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageSecondaryAction{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageOperationsAction{ nullptr };
        Microsoft::UI::Xaml::Controls::ProgressRing m_packageBusy{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageResultsHeader{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageLiveStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_packageResults{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_packageManagerFilters{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_checkDigitSchemePicker{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_checkDigitInput{ nullptr };
        Microsoft::UI::Xaml::Controls::Border m_checkDigitBadge{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_checkDigitBadgeText{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_checkDigitDetail{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_checkDigitStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_binaryTextBasePicker{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_binaryTextInput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_binaryTextOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_binaryTextStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_caseConvertInput{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_caseConvertRows{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_caseConvertStatus{ nullptr };
        std::unordered_map<std::wstring, bool> m_packageManagersSelected;
        std::unordered_map<std::wstring, bool> m_packageManagersAvailable;
        std::unordered_map<std::wstring, std::wstring> m_packageProbeDiagnostics;
        std::vector<winforge::core::packages::PackageItem> m_packageItems;
        std::vector<winforge::core::packages::PackageItem> m_packageBundleItems;
        std::vector<PackageManagerRunState> m_packageRunStates;
        std::vector<PackageOperationEntry> m_packageOperations;
        std::vector<PackageIgnoredRule> m_packageIgnoredRules;
        std::vector<PackagePinnedRule> m_packagePinnedRules;
        std::vector<PackageSnoozedRule> m_packageSnoozedRules;
        std::stop_source m_packageStopSource;
        std::uint64_t m_packageGeneration{ 0 };
        winforge::core::packages::PackageAction m_packageLastAction{
            winforge::core::packages::PackageAction::Probe };
        bool m_packageProbeComplete{ false };
        bool m_packageWorking{ false };
        int32_t m_packageView{ 0 };
        bool m_packageRememberView{ true };
        bool m_packageRememberSearch{ true };
        bool m_packageRememberFilters{ true };
        int32_t m_packageSnoozeDays{ 7 };
        std::filesystem::path m_packageStatePath;
        bool m_packageStateApplying{ false };
        std::uint64_t m_packageOperationSequence{ 0 };
        std::wstring m_packageSearchText{};
        std::wstring m_packageBundleSourcePath{};
        std::wstring m_packageDetailsTarget{};
        int32_t m_packageSortMode{ 0 };
        int32_t m_checkDigitScheme{ 0 };
        std::wstring m_checkDigitValue{};
        bool m_checkDigitRendering{ false };
        int32_t m_binaryTextBase{ 0 };
        std::wstring m_binaryTextInputValue{};
        std::wstring m_binaryTextOutputValue{};
        bool m_binaryTextRendering{ false };
        std::wstring m_caseConvertInputValue{};
        bool m_caseConvertRendering{ false };
        std::wstring m_currentRoute{ L"dashboard" };
        std::wstring m_currentArgument{};

        void ConfigureWindow();
        void BuildAliasIndex();
        void BuildShell();
        void BuildPrimaryNavigation();
        void Navigate(std::wstring_view route, std::wstring_view argument = {}, bool deepLink = false);
        void SelectNavigationItem(std::wstring_view route);
        void RenderCurrent();
        void RenderDashboard();
        void RenderAllApps(std::wstring_view query = {});
        void PopulateAllApps(std::wstring_view query);
        void RenderPackageManager();
        void RenderPackageManagerView();
        void LoadPackageManagerState();
        void SavePackageManagerState() const;
        void ResetPackageManagerState();
        void ApplyPackageSort();
        void RecordPackageOperation(std::wstring message);
        void RecordPackageOperation(
            std::wstring title,
            std::wstring details,
            std::wstring status);
        void MovePackageOperation(std::size_t index, bool runNext);
        void RetryPackageOperation(std::size_t index);
        void ClearPackageOperationLog();
        void PreviewPackageOperation(
            winforge::core::packages::PackageItem const& package,
            winforge::core::packages::PackageAction action);
        void PreviewPackageDetails(
            winforge::core::packages::PackageItem const& package);
        void StartPackageDetailsQuery(
            winforge::core::packages::PackageItem const& package);
        void IgnorePackageUpdate(
            winforge::core::packages::PackageItem const& package);
        void PinPackageUpdate(
            winforge::core::packages::PackageItem const& package);
        void SnoozePackageUpdate(
            winforge::core::packages::PackageItem const& package);
        void RemoveIgnoredPackage(std::wstring managerKey, std::wstring packageId);
        void RemovePinnedPackage(std::wstring managerKey, std::wstring packageId, std::wstring version);
        void RemoveSnoozedPackage(std::wstring managerKey, std::wstring packageId);
        void ClearPackageUpdateRules();
        [[nodiscard]] bool IsPackageIgnored(
            winforge::core::packages::PackageItem const& package) const;
        [[nodiscard]] bool IsPackagePinned(
            winforge::core::packages::PackageItem const& package) const;
        [[nodiscard]] bool IsPackageSnoozed(
            winforge::core::packages::PackageItem const& package) const;
        [[nodiscard]] bool IsPackageUpdateSuppressed(
            winforge::core::packages::PackageItem const& package) const;
        void PreviewPackageBulkUpdate();
        [[nodiscard]] std::wstring BundleSnapshotToJson(
            std::vector<winforge::core::packages::PackageItem> const& items) const;
        [[nodiscard]] bool LoadBundleSnapshot(std::wstring_view path);
        [[nodiscard]] bool SaveBundleSnapshot(std::wstring_view path) const;
        [[nodiscard]] std::wstring PromptBundleOpenPath() const;
        [[nodiscard]] std::wstring PromptBundleSavePath() const;
        void PopulatePackageManagerFilters(Microsoft::UI::Xaml::Controls::StackPanel const& panel);
        void CancelPackageWork();
        [[nodiscard]] bool InvalidatePackageQueryResults();
        void AnnouncePackageStatus(
            std::wstring_view en,
            std::wstring_view zh,
            bool assertive = false);
        void StartPackageManagerProbes();
        void StartPackageQuery();
        [[nodiscard]] bool HasSelectedAvailablePackageManager() const;
        void ProbePackageManagersAsync(
            std::uint64_t generation,
            std::stop_token cancellationToken);
        void QueryPackageManagersAsync(
            std::uint64_t generation,
            winforge::core::packages::PackageAction action,
            std::wstring query,
            std::vector<std::wstring> managerKeys,
            std::stop_token cancellationToken);
        void CompletePackageManagerProbes(
            std::uint64_t generation,
            std::vector<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>> results);
        void CompletePackageQuery(
            std::uint64_t generation,
            winforge::core::packages::PackageAction action,
            std::vector<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>> results);
        [[nodiscard]] int32_t PackageViewFromArgument(std::wstring_view argument) const;
        void RenderCheckDigit();
        void RefreshCheckDigit();
        void AnnounceCheckDigitStatus(std::wstring_view message);
        void RenderBinaryText();
        void AnnounceBinaryTextStatus(std::wstring_view message, bool warning = false);
        void RenderCaseConvert();
        void RefreshCaseConvert();
        void AnnounceCaseConvertStatus(std::wstring_view message, bool warning = false);
        void RenderSearch(std::wstring_view query);
        void RenderAbout();
        void RenderPending(winforge::core::ModuleRecord const& module);
        void RenderUnknown(std::wstring_view route);
        void RenderCatalogError(std::string_view message);

        [[nodiscard]] winforge::core::ModuleRecord const* FindModule(std::wstring_view route) const;
        [[nodiscard]] winforge::core::ModuleRecord const* FindLaunchModule(std::wstring_view route) const;
        [[nodiscard]] bool Matches(winforge::core::ModuleRecord const& module, std::wstring_view query) const;
        [[nodiscard]] Microsoft::UI::Xaml::Controls::StackPanel CreatePage(
            std::wstring_view title,
            std::wstring_view subtitle);
        void ShowPage(Microsoft::UI::Xaml::UIElement const& element);
        [[nodiscard]] Microsoft::UI::Xaml::Controls::TextBlock CreateText(
            std::wstring_view text,
            double size = 14.0,
            bool semibold = false) const;
        [[nodiscard]] Microsoft::UI::Xaml::Controls::Button CreateRouteButton(
            std::wstring_view label,
            std::wstring_view route);
        [[nodiscard]] std::wstring Label(winforge::core::ModuleRecord const& module) const;

        void OnNavigationInvoked(
            Microsoft::UI::Xaml::Controls::NavigationView const&,
            Microsoft::UI::Xaml::Controls::NavigationViewItemInvokedEventArgs const& args);
        void OnSearchSubmitted(
            Microsoft::UI::Xaml::Controls::AutoSuggestBox const&,
            Microsoft::UI::Xaml::Controls::AutoSuggestBoxQuerySubmittedEventArgs const& args);
        void OnLanguageChanged(
            winrt::Windows::Foundation::IInspectable const&,
            Microsoft::UI::Xaml::Controls::SelectionChangedEventArgs const&);
    };
}

namespace winrt::WinForge::factory_implementation
{
    struct MainWindow : MainWindowT<MainWindow, implementation::MainWindow>
    {
    };
}
