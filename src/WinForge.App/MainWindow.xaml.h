#pragma once

#include "MainWindow.g.h"
#include "../WinForge.Core/BinaryText.h"
#include "../WinForge.Core/CaseConvert.h"
#include "../WinForge.Core/CheckDigit.h"
#include "../WinForge.Core/Codec.h"
#include "../WinForge.Core/CommandLine.h"
#include "../WinForge.Core/GuidGen.h"
#include "../WinForge.Core/PassGen.h"
#include "../WinForge.Core/RomanNum.h"
#include "../WinForge.Core/UuidV7.h"
#include "../WinForge.Core/ModuleRecord.h"
#include "../WinForge.Core/PackageMutationCoordinator.h"
#include "../WinForge.Core/PackageRuntime.h"
#include "../WinForge.Core/RegexSearch.h"
#include "../WinForge.Core/RegexSearchSurface.h"
#include "../WinForge.Core/RouteIndex.h"

#include <atomic>
#include <cstdint>
#include <filesystem>
#include <optional>
#include <stop_token>
#include <unordered_set>

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
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_shellRegexMode{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_shellRegexBuilder{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_shellSearchExecute{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_shellRegexStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_languagePicker{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_allAppsSearchBox{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_allAppsRegexMode{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_allAppsRegexBuilder{ nullptr };
        Microsoft::UI::Xaml::Controls::ListView m_allAppsList{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_allAppsCount{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_allAppsRegexStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_packageViewPicker{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_packageSortPicker{ nullptr };
        Microsoft::UI::Xaml::Controls::AutoSuggestBox m_packageSearchBox{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_packageDiscoverFilterPanel{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_packageSearchModePicker{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_packageSearchCaseSensitive{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_packageSearchIgnoreSpecial{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_packageDiscoverRegexMode{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageDiscoverRegexBuilder{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageDiscoverRegexApply{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageDiscoverRegexStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packagePrimaryAction{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageSecondaryAction{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageOperationsAction{ nullptr };
        Microsoft::UI::Xaml::Controls::ProgressRing m_packageBusy{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageResultsHeader{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageLiveStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageQueryAudit{ nullptr };
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
        Microsoft::UI::Xaml::Controls::ComboBox m_codecPicker{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_codecInput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_codecOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_codecStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_caseConvertInput{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_caseConvertRows{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_caseConvertStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_guidGenFormatPicker{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_guidGenUpperSwitch{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_guidGenGuidBox{ nullptr };
        Microsoft::UI::Xaml::Controls::NumberBox m_guidGenCountBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_guidGenBulkBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_guidGenUlidBox{ nullptr };
        Microsoft::UI::Xaml::Controls::NumberBox m_guidGenNanoLengthBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_guidGenNanoBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_guidGenInspectInput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_guidGenInspectHexBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_guidGenInspectMeta{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_guidGenStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_passGenMode{ nullptr };
        Microsoft::UI::Xaml::Controls::NumberBox m_passGenLengthBox{ nullptr };
        Microsoft::UI::Xaml::Controls::CheckBox m_passGenLower{ nullptr };
        Microsoft::UI::Xaml::Controls::CheckBox m_passGenUpper{ nullptr };
        Microsoft::UI::Xaml::Controls::CheckBox m_passGenDigits{ nullptr };
        Microsoft::UI::Xaml::Controls::CheckBox m_passGenSymbols{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_passGenAvoidAmbiguous{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_passGenNoRepeats{ nullptr };
        Microsoft::UI::Xaml::Controls::NumberBox m_passGenWordCountBox{ nullptr };
        Microsoft::UI::Xaml::Controls::ComboBox m_passGenSeparator{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_passGenCapitalize{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_passGenAppendDigit{ nullptr };
        Microsoft::UI::Xaml::Controls::NumberBox m_passGenCountBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_passGenEntropy{ nullptr };
        Microsoft::UI::Xaml::Controls::ProgressBar m_passGenEntropyBar{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_passGenOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_passGenStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::NumberBox m_uuidV7CountBox{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_uuidV7MonotonicSwitch{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7GeneratedOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7DecodeInput{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_uuidV7DecodeResults{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7VersionOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7VariantOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7UtcOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7LocalOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_uuidV7CanonicalOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_uuidV7Status{ nullptr };
        Microsoft::UI::Xaml::Controls::ToggleSwitch m_romanNumExtendedSwitch{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_romanNumNumberInput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_romanNumRomanOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_romanNumRomanBreakdown{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_romanNumCopyRoman{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_romanNumRomanInput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_romanNumNumberOutput{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_romanNumNumberBreakdown{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_romanNumCopyNumber{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_romanNumStatus{ nullptr };
        std::unordered_map<std::wstring, bool> m_packageManagersSelected;
        std::unordered_map<std::wstring, bool> m_packageManagersAvailable;
        std::unordered_map<std::wstring, std::wstring> m_packageProbeDiagnostics;
        std::vector<winforge::core::packages::PackageItem> m_packageItems;
        std::unordered_set<std::wstring> m_packageSelectedKeys;
        std::vector<winforge::core::packages::PackageItem> m_packageBundleItems;
        std::vector<winforge::core::packages::PackageItem> m_packageBundleIncompatibleItems;
        std::vector<PackageManagerRunState> m_packageRunStates;
        std::vector<PackageOperationEntry> m_packageOperations;
        winforge::core::packages::PackageMutationCoordinator m_packageMutationCoordinator;
        std::vector<PackageIgnoredRule> m_packageIgnoredRules;
        std::vector<PackagePinnedRule> m_packagePinnedRules;
        std::vector<PackageSnoozedRule> m_packageSnoozedRules;
        std::stop_source m_packageStopSource;
        std::uint64_t m_packageGeneration{ 0 };
        winforge::core::packages::PackageAction m_packageLastAction{
            winforge::core::packages::PackageAction::Probe };
        bool m_packageProbeComplete{ false };
        bool m_packageWorking{ false };
        std::atomic_bool m_packageMutationWorkerRunning{ false };
        int32_t m_packageView{ 0 };
        bool m_packageRememberView{ true };
        bool m_packageRememberSearch{ true };
        bool m_packageRememberFilters{ true };
        int32_t m_packageSnoozeDays{ 7 };
        std::filesystem::path m_packageStatePath;
        bool m_packageStateApplying{ false };
        std::uint64_t m_packageOperationSequence{ 0 };
        std::uint64_t m_packageQueryEpoch{ 0 };
        std::wstring m_packageSearchText{};
        winforge::core::packages::PackageSearchMode m_packageSearchMode{
            winforge::core::packages::PackageSearchMode::Both };
        bool m_packageSearchCaseSensitiveValue{ false };
        bool m_packageSearchIgnoreSpecialValue{ false };
        bool m_packageDiscoverRegexEnabled{ false };
        bool m_packageDiscoverRegexMultiline{ false };
        bool m_packageDiscoverRegexDotMatchesNewline{ false };
        std::wstring m_packageDiscoverRegexPattern{};
        std::wstring m_packageDiscoverRegexDiagnostic{};
        bool m_packageRetainCachedResultsOnNextRender{ false };
        std::wstring m_packageBundleSourcePath{};
        std::wstring m_packageBundleImportNote{};
        bool m_packageBundleDirty{ false };
        std::wstring m_packageDetailsTarget{};
        int32_t m_packageSortMode{ 0 };
        int32_t m_checkDigitScheme{ 0 };
        std::wstring m_checkDigitValue{};
        bool m_checkDigitRendering{ false };
        int32_t m_binaryTextBase{ 0 };
        std::wstring m_binaryTextInputValue{};
        std::wstring m_binaryTextOutputValue{};
        bool m_binaryTextRendering{ false };
        int32_t m_codecEncoding{ 0 };
        std::wstring m_codecInputValue{};
        std::wstring m_codecOutputValue{};
        bool m_codecRendering{ false };
        std::wstring m_caseConvertInputValue{};
        bool m_caseConvertRendering{ false };
        int32_t m_guidGenFormatIndex{ 0 };
        bool m_guidGenUpper{ false };
        int32_t m_guidGenBulkCount{ 10 };
        int32_t m_guidGenNanoLength{ 21 };
        std::wstring m_guidGenGuidValue{};
        std::wstring m_guidGenBulkValue{};
        std::wstring m_guidGenUlidValue{};
        std::wstring m_guidGenNanoValue{};
        std::wstring m_guidGenInspectValue{};
        bool m_guidGenRendering{ false };
        bool m_passGenPassphrase{ false };
        int32_t m_passGenLength{ 16 };
        bool m_passGenLowerEnabled{ true };
        bool m_passGenUpperEnabled{ true };
        bool m_passGenDigitsEnabled{ true };
        bool m_passGenSymbolsEnabled{ true };
        bool m_passGenAvoidAmbiguousEnabled{ false };
        bool m_passGenNoRepeatsEnabled{ false };
        int32_t m_passGenWordCount{ 4 };
        int32_t m_passGenSeparatorIndex{ 0 };
        bool m_passGenCapitalizeEnabled{ false };
        bool m_passGenAppendDigitEnabled{ false };
        int32_t m_passGenCount{ 1 };
        std::wstring m_passGenOutputValue{};
        std::wstring m_passGenStatusValue{};
        bool m_passGenRendering{ false };
        int32_t m_uuidV7Count{ 1 };
        bool m_uuidV7Monotonic{ true };
        std::wstring m_uuidV7GeneratedValue{};
        std::wstring m_uuidV7DecodeInputValue{};
        std::wstring m_uuidV7TimestampValue{};
        bool m_uuidV7Rendering{ false };
        bool m_romanNumExtended{ false };
        std::wstring m_romanNumNumberInputValue{};
        std::wstring m_romanNumRomanInputValue{};
        std::wstring m_romanNumRomanOutputValue{};
        std::wstring m_romanNumNumberOutputValue{};
        bool m_romanNumRendering{ false };
        bool m_shellRegexEnabled{ false };
        bool m_shellRegexCaseSensitive{ false };
        bool m_shellRegexMultiline{ false };
        bool m_shellRegexDotMatchesNewline{ false };
        std::wstring m_shellRegexDiagnostic{};
        std::wstring m_allAppsSearchText{};
        bool m_allAppsRegexEnabled{ false };
        bool m_allAppsRegexCaseSensitive{ false };
        bool m_allAppsRegexMultiline{ false };
        bool m_allAppsRegexDotMatchesNewline{ false };
        std::wstring m_allAppsRegexDiagnostic{};

        enum class RegexBuilderTarget : std::uint8_t
        {
            ShellCatalog = static_cast<std::uint8_t>(
                winforge::core::regex::RegexSearchSurfaceId::ShellCatalog),
            AllApps = static_cast<std::uint8_t>(
                winforge::core::regex::RegexSearchSurfaceId::AllApps),
            PackageDiscover = static_cast<std::uint8_t>(
                winforge::core::regex::RegexSearchSurfaceId::PackageDiscoverCachedResults),
            TesterOnly,
        };

        RegexBuilderTarget m_regexBuilderTarget{ RegexBuilderTarget::TesterOnly };
        int32_t m_regexBuilderStep{ 0 };
        bool m_regexBuilderCaseSensitive{ false };
        bool m_regexBuilderMultiline{ false };
        bool m_regexBuilderDotMatchesNewline{ false };
        std::wstring m_regexBuilderPattern{};
        std::wstring m_regexBuilderTestText{ L"WinForge Native\r\nPackage Manager" };
        std::wstring m_regexBuilderLiteral{};
        std::wstring m_regexBuilderCharacterClass{};
        std::wstring m_regexBuilderCaptureName{};
        std::wstring m_regexBuilderAlternatives{};
        std::wstring m_regexBuilderAssertion{};
        int32_t m_regexBuilderRecipeIndex{ 0 };
        int32_t m_regexBuilderAssertionIndex{ 0 };
        int32_t m_regexBuilderQuantifierIndex{ 0 };
        int32_t m_regexBuilderRangeMinimum{ 0 };
        int32_t m_regexBuilderRangeMaximum{ 1 };
        bool m_regexBuilderRangeUnbounded{ false };
        Microsoft::UI::Xaml::Controls::TextBox m_regexBuilderPatternBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBox m_regexBuilderTestTextBox{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_regexBuilderStatus{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_regexBuilderPreview{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_regexBuilderApply{ nullptr };
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
        void RenderRegexTester();
        void OpenRegexBuilder(RegexBuilderTarget target, std::wstring_view initialPattern = {});
        void ApplyRegexBuilderTarget();
        void AppendRegexBuilderToken(std::wstring_view token);
        void RefreshRegexTesterPreview();
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
        void RequestPackageMutation(
            winforge::core::packages::PackageItem const& package,
            winforge::core::packages::PackageAction action);
        void ReviewPackageMutationBatch(
            std::vector<winforge::core::packages::PackageItem> packages,
            winforge::core::packages::PackageAction action,
            std::wstring sourceLabelEn,
            std::wstring sourceLabelZh);
        void ConfirmPackageMutation(std::wstring id);
        void ConfirmPackageMutationBatch(std::wstring id);
        void CancelPackageMutation(std::wstring id);
        void CancelPackageMutationBatch(std::wstring id);
        void RetryPackageMutation(std::wstring id);
        void RetryPackageMutationBatch(std::wstring id);
        void StartNextPackageMutation();
        [[nodiscard]] std::optional<winforge::core::packages::PackageAction> CurrentPackageSelectionAction() const;
        void SetPackageSelected(
            winforge::core::packages::PackageItem const& package,
            winforge::core::packages::PackageAction action,
            bool selected);
        void ClearPackageSelection();
        [[nodiscard]] std::vector<winforge::core::packages::PackageItem> SelectedPackageItems(
            winforge::core::packages::PackageAction action) const;
        void PreviewSelectedPackageOperations(winforge::core::packages::PackageAction action);
        void AddSelectedPackagesToBundle();
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
        enum class BundleSnapshotLoadStatus : std::uint8_t
        {
            Loaded,
            Cancelled,
            Failed,
        };
        [[nodiscard]] std::wstring BundleSnapshotToJson(
            winforge::core::packages::PackageBundleSnapshot const& snapshot) const;
        [[nodiscard]] BundleSnapshotLoadStatus LoadBundleSnapshot(std::wstring_view path);
        [[nodiscard]] bool SaveBundleSnapshot(std::wstring_view path) const;
        [[nodiscard]] bool ConfirmBundleWorkspaceReplacement() const;
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
        void RenderCodec();
        void AnnounceCodecStatus(std::wstring_view message, bool warning = false);
        void RenderCaseConvert();
        void RefreshCaseConvert();
        void AnnounceCaseConvertStatus(std::wstring_view message, bool warning = false);
        void RenderGuidGen();
        void GenerateGuidValue();
        void GenerateBulkGuidValues();
        void GenerateUlidValue();
        void GenerateNanoIdValue();
        void RefreshGuidInspector();
        void AnnounceGuidGenStatus(std::wstring_view message, bool warning = false);
        void RenderPassGen();
        void RegeneratePassGen();
        void UpdatePassGenEntropy();
        void CopyPassGenOutput();
        void AnnouncePassGenStatus(std::wstring_view message, bool warning = false);
        void RenderUuidV7();
        void GenerateUuidV7Values();
        void DecodeUuidV7Value();
        void ClearUuidV7DecodeResults();
        void CopyUuidV7Value(std::wstring_view value, std::wstring_view successMessage);
        void AnnounceUuidV7Status(std::wstring_view message, bool warning = false);
        void RenderRomanNum();
        void RefreshRomanNum(bool refreshNumber = true, bool refreshRoman = true);
        void AnnounceRomanNumStatus(std::wstring_view message, bool warning = false);
        void RenderSearch(std::wstring_view query);
        void RenderAbout();
        void RenderPending(winforge::core::ModuleRecord const& module);
        void RenderUnknown(std::wstring_view route);
        void RenderCatalogError(std::string_view message);

        [[nodiscard]] winforge::core::ModuleRecord const* FindModule(std::wstring_view route) const;
        [[nodiscard]] winforge::core::ModuleRecord const* FindLaunchModule(std::wstring_view route) const;
        [[nodiscard]] std::shared_ptr<winforge::core::regex::SafeRegex const> CompileSearchRegex(
            std::wstring_view pattern,
            bool caseSensitive,
            bool multiline,
            bool dotMatchesNewline,
            std::wstring& diagnostic) const;
        [[nodiscard]] bool Matches(
            winforge::core::ModuleRecord const& module,
            std::wstring_view query,
            winforge::core::regex::SafeRegex const* expression = nullptr) const;
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
        void SubmitShellSearch(std::wstring_view query);
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
