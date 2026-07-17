#include "pch.h"
#include "MainWindow.xaml.h"

#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "CatalogLoader.h"
#include "../WinForge.Core/PackageParsers.h"
#include "../WinForge.Core/RegexBuilder.h"
#include "microsoft.ui.xaml.window.h"
#include <winrt/Windows.ApplicationModel.DataTransfer.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Microsoft.UI.Xaml.Automation.Peers.h>

#include <chrono>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <cwctype>
#include <array>
#include <cstdlib>
#include <filesystem>
#include <functional>
#include <fstream>
#include <future>
#include <commdlg.h>
#include <iterator>
#include <limits>
#include <sstream>
#include <stdexcept>
#include <thread>
#include <unordered_set>
#include <utility>

#pragma comment(lib, "Comdlg32.lib")

namespace
{
    using namespace winrt;
    using namespace Microsoft::UI::Xaml;
    using namespace Microsoft::UI::Xaml::Automation;
    using namespace Microsoft::UI::Xaml::Controls;

    hstring ToHString(std::wstring_view value)
    {
        return hstring(value);
    }

    std::wstring ToWide(hstring const& value)
    {
        return std::wstring(value.c_str(), value.size());
    }

    std::string ToUtf8(std::wstring_view value)
    {
        if (value.empty())
        {
            return {};
        }
        if (value.size() > static_cast<std::size_t>(std::numeric_limits<int>::max()))
        {
            return {};
        }
        auto const length = WideCharToMultiByte(
            CP_UTF8,
            WC_ERR_INVALID_CHARS,
            value.data(),
            static_cast<int>(value.size()),
            nullptr,
            0,
            nullptr,
            nullptr);
        if (length == 0)
        {
            return {};
        }
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

    std::wstring ToWideUtf8(std::string_view value)
    {
        if (value.empty())
        {
            return {};
        }
        if (value.size() > static_cast<std::size_t>(std::numeric_limits<int>::max()))
        {
            return {};
        }
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
            for (auto const byte : value)
            {
                fallback.push_back(static_cast<unsigned char>(byte));
            }
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

    std::wstring AutomationKey(std::wstring_view value)
    {
        std::wstring result;
        result.reserve(value.size());
        for (auto const character : value)
        {
            result.push_back(std::iswalnum(character) ? character : L'_');
        }
        return result;
    }

    std::wstring StableAutomationSuffix(std::wstring_view value)
    {
        // FNV-1a keeps source-aware package identities opaque and stable across
        // sorting/filtering without exposing an untrusted package ID in UIA.
        std::uint64_t hash = 1469598103934665603ull;
        for (auto const character : value)
        {
            hash ^= static_cast<std::uint16_t>(character);
            hash *= 1099511628211ull;
        }
        return std::to_wstring(hash);
    }

    std::wstring BundleAutomationIdentity(
        winforge::core::packages::PackageItem const& package)
    {
        // Keep UI Automation IDs stable as a bundle row moves through sorting,
        // while including the version and raw metadata that distinguish audit
        // records. The value is immediately hashed before it reaches UIA.
        std::wstring identity;
        auto append = [&identity](std::wstring_view name, std::wstring_view value)
        {
            if (!identity.empty())
            {
                identity += L'|';
            }
            identity += name;
            identity += L'=';
            identity += std::to_wstring(value.size());
            identity += L':';
            identity += value;
        };
        append(L"manager", package.manager_key);
        append(L"id", package.id);
        append(L"source", package.source);
        append(L"version", package.version);
        return identity;
    }

    constexpr std::size_t MaximumNativeBundleBytes = 2 * 1024 * 1024;
    constexpr std::size_t MaximumNativeBundleRecords = 2048;
    constexpr std::size_t MaximumNativeBundleFieldLength = 512;

    std::size_t BundleRecordCount(
        winforge::core::packages::PackageBundleSnapshot const& snapshot) noexcept
    {
        return snapshot.packages.size() + snapshot.incompatible_packages.size();
    }

    bool IsBundleItemWithinLimits(
        winforge::core::packages::PackageItem const& item) noexcept
    {
        return item.name.size() <= MaximumNativeBundleFieldLength &&
            item.id.size() <= MaximumNativeBundleFieldLength &&
            item.version.size() <= MaximumNativeBundleFieldLength &&
            item.source.size() <= MaximumNativeBundleFieldLength &&
            item.manager_key.size() <= MaximumNativeBundleFieldLength;
    }

    std::wstring GuidFormatFromIndex(int32_t index)
    {
        switch (index)
        {
        case 1: return L"N";
        case 2: return L"B";
        case 3: return L"P";
        case 4: return L"X";
        default: return L"D";
        }
    }

    std::wstring PackageRuleKey(std::wstring_view managerKey, std::wstring_view packageId)
    {
        std::wstring result;
        result.reserve(managerKey.size() + packageId.size() + 1);
        auto appendNormalized = [&result](std::wstring_view value)
        {
            for (auto const character : value)
            {
                result.push_back(static_cast<wchar_t>(std::towlower(character)));
            }
        };
        appendNormalized(managerKey);
        result.push_back(L'\x1f');
        appendNormalized(packageId);
        return result;
    }

    std::wstring PackageVersionRuleKey(
        std::wstring_view managerKey,
        std::wstring_view packageId,
        std::wstring_view version)
    {
        std::wstring result = PackageRuleKey(managerKey, packageId);
        result.push_back(L'\x1f');
        for (auto const character : version)
        {
            result.push_back(static_cast<wchar_t>(std::towlower(character)));
        }
        return result;
    }

    std::wstring PackageUpdateRuleVersion(
        winforge::core::packages::PackageItem const& package)
    {
        return package.available_version.empty() ? package.version : package.available_version;
    }

    std::int64_t NowUnixSeconds()
    {
        return std::chrono::duration_cast<std::chrono::seconds>(
            std::chrono::system_clock::now().time_since_epoch()).count();
    }

    std::wstring FormatRuleTime(std::int64_t epochSeconds)
    {
        return std::to_wstring(epochSeconds) + L" UTC epoch seconds";
    }

    int32_t NormalizeSnoozeDays(int32_t days)
    {
        constexpr std::array<int32_t, 4> Allowed{ 1, 7, 14, 30 };
        auto const found = std::find(Allowed.begin(), Allowed.end(), days);
        return found == Allowed.end() ? 7 : days;
    }

    int32_t SnoozeDaysIndex(int32_t days)
    {
        constexpr std::array<int32_t, 4> Allowed{ 1, 7, 14, 30 };
        auto const found = std::find(Allowed.begin(), Allowed.end(), NormalizeSnoozeDays(days));
        return found == Allowed.end()
            ? 1
            : static_cast<int32_t>(std::distance(Allowed.begin(), found));
    }

    int32_t SnoozeDaysFromIndex(int32_t index)
    {
        constexpr std::array<int32_t, 4> Allowed{ 1, 7, 14, 30 };
        if (index < 0 || index >= static_cast<int32_t>(Allowed.size()))
        {
            return 7;
        }
        return Allowed[static_cast<std::size_t>(index)];
    }

    std::wstring FormatSnoozeLabel(int32_t days, winforge::core::LanguageMode language)
    {
        days = NormalizeSnoozeDays(days);
        auto const count = std::to_wstring(days);
        return winforge::core::LocalizedText{
            L"Snooze " + count + (days == 1 ? L" day" : L" days"),
            L"暫停 " + count + L" 日" }.Pick(language);
    }

    std::wstring EscapeJson(std::wstring_view value)
    {
        std::wstring escaped;
        escaped.reserve(value.size() + 8);
        for (auto const character : value)
        {
            switch (character)
            {
            case L'\\': escaped += L"\\\\"; break;
            case L'\"': escaped += L"\\\""; break;
            case L'\b': escaped += L"\\b"; break;
            case L'\f': escaped += L"\\f"; break;
            case L'\n': escaped += L"\\n"; break;
            case L'\r': escaped += L"\\r"; break;
            case L'\t': escaped += L"\\t"; break;
            default:
                if (character < 0x20)
                {
                    wchar_t buffer[7];
                    swprintf_s(buffer, L"\\u%04x", static_cast<unsigned int>(character));
                    escaped += buffer;
                }
                else
                {
                    escaped.push_back(character);
                }
                break;
            }
        }
        return escaped;
    }

    std::filesystem::path PackageManagerStatePath()
    {
        std::wstring buffer(32768, L'\0');
        auto const length = GetEnvironmentVariableW(L"LOCALAPPDATA", buffer.data(), static_cast<DWORD>(buffer.size()));
        std::filesystem::path root = (length != 0 && length < buffer.size())
            ? std::filesystem::path(buffer.substr(0, length))
            : std::filesystem::temp_directory_path();
        return root / L"WinForge" / L"native-package-manager-state.json";
    }

    std::wstring PromptOpenBundlePath(HWND owner)
    {
        wchar_t path[32768]{};
        static const wchar_t filters[] =
            L"JSON / UniGetUI bundle (*.json;*.ubundle)\0*.json;*.ubundle\0"
            L"JSON (*.json)\0*.json\0"
            L"All files (*.*)\0*.*\0\0";
        OPENFILENAMEW ofn{};
        ofn.lStructSize = sizeof(ofn);
        ofn.hwndOwner = owner;
        ofn.lpstrFilter = filters;
        ofn.lpstrFile = path;
        ofn.nMaxFile = static_cast<DWORD>(std::size(path));
        ofn.Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
        return GetOpenFileNameW(&ofn) ? std::wstring(path) : std::wstring{};
    }

    std::wstring PromptSaveBundlePath(HWND owner, std::wstring_view suggestedName)
    {
        wchar_t path[32768]{};
        auto const copyCount = std::min<std::size_t>(suggestedName.size(), std::size(path) - 1);
        std::copy_n(suggestedName.begin(), copyCount, path);
        path[copyCount] = L'\0';
        static const wchar_t filters[] =
            L"JSON / UniGetUI bundle (*.json;*.ubundle)\0*.json;*.ubundle\0"
            L"JSON (*.json)\0*.json\0"
            L"All files (*.*)\0*.*\0\0";
        OPENFILENAMEW ofn{};
        ofn.lStructSize = sizeof(ofn);
        ofn.hwndOwner = owner;
        ofn.lpstrFilter = filters;
        ofn.nFilterIndex = 1;
        ofn.lpstrFile = path;
        ofn.nMaxFile = static_cast<DWORD>(std::size(path));
        ofn.lpstrDefExt = L"json";
        ofn.Flags = OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;
        if (!GetSaveFileNameW(&ofn))
        {
            return {};
        }
        return std::wstring(path);
    }

    bool IsPackageSearchWhitespace(wchar_t character)
    {
        WORD type{};
        return GetStringTypeW(CT_CTYPE1, &character, 1, &type) != 0 && (type & C1_SPACE) != 0;
    }

    std::wstring TrimPackageSearchText(std::wstring_view value)
    {
        auto first = value.begin();
        auto last = value.end();
        while (first != last && IsPackageSearchWhitespace(*first))
        {
            ++first;
        }
        while (last != first && IsPackageSearchWhitespace(*(last - 1)))
        {
            --last;
        }
        return std::wstring(first, last);
    }

    bool HasNonWhitespace(std::wstring_view value)
    {
        return std::any_of(value.begin(), value.end(), [](wchar_t character)
        {
            return !std::iswspace(character);
        });
    }

    std::wstring TruncateForUi(std::wstring_view value, std::size_t maximum = 1'600)
    {
        if (value.size() <= maximum)
        {
            return std::wstring(value);
        }
        auto result = std::wstring(value.substr(0, maximum));
        result += L"\n…";
        return result;
    }

    bool ShouldRetainPackageDiscoverResults(
        int32_t view,
        winforge::core::packages::PackageAction lastAction,
        bool probeComplete,
        bool hasItems) noexcept
    {
        return view == static_cast<int32_t>(winforge::core::packages::PackageView::Discover)
            && lastAction == winforge::core::packages::PackageAction::Search
            && probeComplete
            && hasItems;
    }

    NavigationViewItem MakeNavigationItem(
        std::wstring_view label,
        std::wstring_view route,
        std::wstring_view glyph = {})
    {
        NavigationViewItem item;
        item.Content(box_value(ToHString(label)));
        item.Tag(box_value(ToHString(route)));
        AutomationProperties::SetAutomationId(item, ToHString(L"NativeNav_" + AutomationKey(route)));
        AutomationProperties::SetName(item, ToHString(label));
        if (!glyph.empty())
        {
            FontIcon icon;
            icon.Glyph(ToHString(glyph));
            item.Icon(icon);
        }
        return item;
    }

}

namespace winrt::WinForge::implementation
{
    using namespace Microsoft::UI;
    using namespace Microsoft::UI::Xaml;
    using namespace Microsoft::UI::Xaml::Automation;
    using namespace Microsoft::UI::Xaml::Controls;

    MainWindow::MainWindow()
    {
        InitializeComponent();
        Title(L"WinForge Native · 視窗調校原生版");
        Closed([this](Windows::Foundation::IInspectable const&, WindowEventArgs const&)
        {
            CancelPackageWork();
            static_cast<void>(m_packageMutationCoordinator.CancelAll());
        });

        try
        {
            ConfigureWindow();
            m_modules = winforge::app::LoadModuleCatalog();
            BuildAliasIndex();
            BuildShell();
            m_packageStatePath = PackageManagerStatePath();
            LoadPackageManagerState();

            m_initialLaunchRequest = winforge::core::CurrentProcessLaunchRequest();
            Activated([this](Windows::Foundation::IInspectable const&, WindowActivatedEventArgs const&)
            {
                QueueInitialNavigation();
            });
        }
        catch (winrt::hresult_error const& error)
        {
            RenderCatalogError(winrt::to_string(error.message()));
        }
        catch (std::exception const& error)
        {
            RenderCatalogError(error.what());
        }
    }

    void MainWindow::ConfigureWindow()
    {
        HWND windowHandle{};
        Window window = *this;
        check_hresult(window.as<IWindowNative>()->get_WindowHandle(&windowHandle));

        auto const dpi = GetDpiForWindow(windowHandle);
        auto const scale = static_cast<double>(dpi) / 96.0;
        SetWindowPos(
            windowHandle,
            nullptr,
            0,
            0,
            static_cast<int>(1320 * scale),
            static_cast<int>(880 * scale),
            SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);

        auto const iconPath = winforge::app::ExecutableDirectory() / L"Assets" / L"AppIcon.ico";
        auto const icon = LoadImageW(
            nullptr,
            iconPath.c_str(),
            IMAGE_ICON,
            GetSystemMetrics(SM_CXICON),
            GetSystemMetrics(SM_CYICON),
            LR_LOADFROMFILE | LR_SHARED);
        if (icon)
        {
            SendMessageW(windowHandle, WM_SETICON, ICON_BIG, reinterpret_cast<LPARAM>(icon));
            SendMessageW(windowHandle, WM_SETICON, ICON_SMALL, reinterpret_cast<LPARAM>(icon));
        }
    }

    void MainWindow::BuildAliasIndex()
    {
        m_routeIndex.Rebuild(m_modules);
    }

    void MainWindow::BuildShell()
    {
        Root().Children().Clear();
        auto const& shellRegexSurface = winforge::core::regex::RegexSearchSurfaceFor(
            winforge::core::regex::RegexSearchSurfaceId::ShellCatalog);

        m_navigation = NavigationView();
        m_navigation.PaneTitle(L"WinForge · 視窗調校");
        m_navigation.PaneDisplayMode(NavigationViewPaneDisplayMode::Auto);
        m_navigation.IsBackButtonVisible(NavigationViewBackButtonVisible::Collapsed);
        m_navigation.IsSettingsVisible(true);
        m_navigation.AlwaysShowHeader(false);
        AutomationProperties::SetAutomationId(m_navigation, L"NativeShellNavigation");

        m_search = AutoSuggestBox();
        m_search.PlaceholderText(L"Search every native route · 搜尋全部原生路線");
        AutomationProperties::SetAutomationId(
            m_search,
            ToHString(shellRegexSurface.search_automation_id));
        m_search.QuerySubmitted({ this, &MainWindow::OnSearchSubmitted });
        m_search.TextChanged([this](AutoSuggestBox const&, AutoSuggestBoxTextChangedEventArgs const&)
        {
            if (!m_shellRegexMode || !m_shellRegexStatus)
            {
                return;
            }
            if (!m_shellRegexEnabled)
            {
                m_shellRegexDiagnostic.clear();
                m_shellRegexStatus.Text(L"Literal catalog search · 文字目錄搜尋");
                return;
            }

            std::wstring diagnostic;
            auto const expression = CompileSearchRegex(
                ToWide(m_search.Text()),
                m_shellRegexCaseSensitive,
                m_shellRegexMultiline,
                m_shellRegexDotMatchesNewline,
                diagnostic);
            static_cast<void>(expression);
            m_shellRegexDiagnostic = diagnostic;
            m_shellRegexStatus.Text(ToHString(diagnostic.empty()
                ? L"PCRE2 regex is ready; search remains local and bounded. · PCRE2 正規表示式已就緒；搜尋只會喺本機並受限制。"
                : diagnostic));
        });
        m_navigation.AutoSuggestBox(m_search);

        m_shellRegexMode = ToggleSwitch();
        m_shellRegexMode.Header(box_value(L"Use PCRE2 regex · 使用 PCRE2 正規表示式"));
        m_shellRegexMode.IsOn(m_shellRegexEnabled);
        AutomationProperties::SetAutomationId(
            m_shellRegexMode,
            ToHString(shellRegexSurface.regex_mode_automation_id));
        AutomationProperties::SetName(m_shellRegexMode, L"Use PCRE2 regular expressions for native catalog search · 用 PCRE2 正規表示式搜尋原生目錄");
        m_shellRegexMode.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            m_shellRegexEnabled = sender.as<ToggleSwitch>().IsOn();
            m_search.PlaceholderText(m_shellRegexEnabled
                ? L"Search native routes with PCRE2 regex · 用 PCRE2 正規表示式搜尋原生路線"
                : L"Search every native route · 搜尋全部原生路線");
            std::wstring diagnostic;
            if (m_shellRegexEnabled)
            {
                auto const expression = CompileSearchRegex(
                    ToWide(m_search.Text()),
                    m_shellRegexCaseSensitive,
                    m_shellRegexMultiline,
                    m_shellRegexDotMatchesNewline,
                    diagnostic);
                static_cast<void>(expression);
            }
            m_shellRegexDiagnostic = diagnostic;
            if (m_shellRegexStatus)
            {
                m_shellRegexStatus.Text(ToHString(m_shellRegexEnabled
                    ? (diagnostic.empty()
                        ? L"PCRE2 regex is ready; search remains local and bounded. · PCRE2 正規表示式已就緒；搜尋只會喺本機並受限制。"
                        : diagnostic)
                    : L"Literal catalog search · 文字目錄搜尋"));
            }
            if (m_currentRoute == L"search")
            {
                RenderSearch(m_currentArgument);
            }
        });

        m_shellRegexBuilder = Button();
        m_shellRegexBuilder.Content(box_value(L"Build regex · 建立正規表示式"));
        m_shellRegexBuilder.HorizontalAlignment(HorizontalAlignment::Stretch);
        AutomationProperties::SetAutomationId(
            m_shellRegexBuilder,
            ToHString(shellRegexSurface.builder_automation_id));
        AutomationProperties::SetName(m_shellRegexBuilder, L"Open the regex builder for native catalog search · 開啟原生目錄搜尋嘅正規表示式建立器");
        m_shellRegexBuilder.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            OpenRegexBuilder(RegexBuilderTarget::ShellCatalog, ToWide(m_search.Text()));
        });

        m_shellSearchExecute = Button();
        m_shellSearchExecute.Content(box_value(L"Search catalog · 搜尋目錄"));
        m_shellSearchExecute.HorizontalAlignment(HorizontalAlignment::Stretch);
        AutomationProperties::SetAutomationId(m_shellSearchExecute, L"NativeShellSearchExecute");
        AutomationProperties::SetName(m_shellSearchExecute, L"Run the native catalog search · 執行原生目錄搜尋");
        m_shellSearchExecute.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            SubmitShellSearch(ToWide(m_search.Text()));
        });

        m_shellRegexStatus = CreateText(L"Literal catalog search · 文字目錄搜尋", 11);
        m_shellRegexStatus.TextWrapping(TextWrapping::Wrap);
        m_shellRegexStatus.Opacity(0.72);
        AutomationProperties::SetAutomationId(m_shellRegexStatus, L"NativeShellRegexStatus");
        AutomationProperties::SetLiveSetting(
            m_shellRegexStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        m_languagePicker = ComboBox();
        m_languagePicker.Header(box_value(L"Language · 語言"));
        m_languagePicker.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_languagePicker.Margin(Thickness{ 12, 8, 12, 12 });
        for (auto const label : { L"Bilingual · 雙語", L"粵語", L"English" })
        {
            ComboBoxItem option;
            option.Content(box_value(label));
            m_languagePicker.Items().Append(option);
        }
        m_languagePicker.SelectedIndex(0);
        m_languagePicker.SelectionChanged({ this, &MainWindow::OnLanguageChanged });
        AutomationProperties::SetAutomationId(m_languagePicker, L"NativeLanguagePicker");

        StackPanel paneFooter;
        paneFooter.Spacing(4);
        paneFooter.Padding(Thickness{ 8, 4, 8, 4 });
        paneFooter.Children().Append(m_shellRegexMode);
        paneFooter.Children().Append(m_shellRegexBuilder);
        paneFooter.Children().Append(m_shellSearchExecute);
        paneFooter.Children().Append(m_shellRegexStatus);
        paneFooter.Children().Append(m_languagePicker);
        m_navigation.PaneFooter(paneFooter);

        m_content = Grid();
        AutomationProperties::SetAutomationId(m_content, L"NativePageHost");
        m_navigation.Content(m_content);
        m_navigation.ItemInvoked({ this, &MainWindow::OnNavigationInvoked });

        BuildPrimaryNavigation();
        Root().Children().Append(m_navigation);
    }

    void MainWindow::BuildPrimaryNavigation()
    {
        m_navigation.MenuItems().Clear();
        m_navigation.FooterMenuItems().Clear();

        auto appendRoute = [&](std::wstring_view route)
        {
            auto const* module = FindModule(route);
            if (!module) return;
            m_navigation.MenuItems().Append(MakeNavigationItem(Label(*module), module->id, module->glyph));
        };

        appendRoute(L"dashboard");
        appendRoute(L"module.reactor");
        appendRoute(L"module.packages");
        appendRoute(L"shell.allapps");

        NavigationViewItemHeader categoryHeader;
        categoryHeader.Content(box_value(L"TWEAK CATEGORIES · 調校分類"));
        m_navigation.MenuItems().Append(categoryHeader);
        for (auto const& module : m_modules)
        {
            if (module.kind == L"category")
            {
                m_navigation.MenuItems().Append(MakeNavigationItem(Label(module), module.id, module.glyph));
            }
        }

        for (auto const route : { L"manual", L"licenses", L"about" })
        {
            auto const* module = FindModule(route);
            if (module)
            {
                m_navigation.FooterMenuItems().Append(MakeNavigationItem(Label(*module), module->id, module->glyph));
            }
        }
    }

    void MainWindow::QueueInitialNavigation()
    {
        if (m_initialNavigationQueued || !m_initialLaunchRequest)
        {
            return;
        }

        m_initialNavigationQueued = true;
        auto const request = std::move(*m_initialLaunchRequest);
        m_initialLaunchRequest.reset();
        auto const lifetime = get_strong();
        auto const navigate = [lifetime, request]()
        {
            try
            {
                lifetime->Navigate(request.route, request.argument, true);
            }
            catch (winrt::hresult_error const& error)
            {
                lifetime->RenderCatalogError(winrt::to_string(error.message()));
            }
            catch (std::exception const& error)
            {
                lifetime->RenderCatalogError(error.what());
            }
        };

        auto const dispatcher = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
        if (dispatcher && dispatcher.TryEnqueue(navigate))
        {
            return;
        }

        // The activated handler normally has a dispatcher. Fall back only if
        // shutdown has begun, so a valid deep link is never silently dropped.
        navigate();
    }

    void MainWindow::Navigate(std::wstring_view route, std::wstring_view argument, bool deepLink)
    {
        if (m_currentRoute == L"module.packages")
        {
            CancelPackageWork();
        }
        auto normalized = winforge::core::NormalizeRouteKey(route);
        auto cancelMutationIfLeavingPackages = [this](std::wstring_view nextRoute)
        {
            if (m_currentRoute == L"module.packages" && nextRoute != L"module.packages")
            {
                static_cast<void>(m_packageMutationCoordinator.CancelAll());
            }
        };
        if (normalized == L"search")
        {
            cancelMutationIfLeavingPackages(L"search");
            m_currentRoute = L"search";
            m_currentArgument = std::wstring(argument);
            RenderCurrent();
            return;
        }
        if (normalized == L"manual" && !argument.empty())
        {
            cancelMutationIfLeavingPackages(L"manual");
            m_currentRoute = L"manual";
            m_currentArgument = std::wstring(argument);
            RenderCurrent();
            return;
        }

        auto const* module = deepLink ? FindLaunchModule(normalized) : FindModule(normalized);
        if (!module)
        {
            cancelMutationIfLeavingPackages(normalized);
            m_currentRoute = normalized;
            m_currentArgument = std::wstring(argument);
            RenderUnknown(normalized);
            return;
        }

        cancelMutationIfLeavingPackages(module->id);
        m_currentRoute = module->id;
        m_currentArgument = std::wstring(argument);
        SelectNavigationItem(module->id);
        RenderCurrent();
    }

    void MainWindow::SelectNavigationItem(std::wstring_view route)
    {
        if (!m_navigation) return;
        if (route == L"settings")
        {
            auto const settings = m_navigation.SettingsItem();
            if (settings)
            {
                m_navigation.SelectedItem(settings);
            }
            return;
        }

        auto selectFrom = [&](auto const& items) -> bool
        {
            for (auto const& candidate : items)
            {
                auto const item = candidate.try_as<NavigationViewItem>();
                if (!item) continue;
                auto const tag = unbox_value_or<hstring>(item.Tag(), L"");
                if (winforge::core::NormalizeRouteKey(ToWide(tag)) == winforge::core::NormalizeRouteKey(route))
                {
                    m_navigation.SelectedItem(item);
                    return true;
                }
            }
            return false;
        };

        if (!selectFrom(m_navigation.MenuItems()))
        {
            selectFrom(m_navigation.FooterMenuItems());
        }
    }

    void MainWindow::RenderCurrent()
    {
        if (m_currentRoute != L"module.passwordstrength")
        {
            ClearPasswordStrengthSecret();
        }

        if (m_currentRoute == L"search")
        {
            RenderSearch(m_currentArgument);
            return;
        }

        auto const* module = FindModule(m_currentRoute);
        if (!module)
        {
            RenderUnknown(m_currentRoute);
            return;
        }

        if (module->id == L"dashboard")
        {
            RenderDashboard();
        }
        else if (module->id == L"shell.allapps")
        {
            RenderAllApps();
        }
        else if (module->id == L"module.packages")
        {
            RenderPackageManager();
        }
        else if (module->id == L"module.checkdigit")
        {
            RenderCheckDigit();
        }
        else if (module->id == L"module.binarytext")
        {
            RenderBinaryText();
        }
        else if (module->id == L"module.base32")
        {
            RenderCodec();
        }
        else if (module->id == L"module.caseconvert")
        {
            RenderCaseConvert();
        }
        else if (module->id == L"module.guidgen")
        {
            RenderGuidGen();
        }
        else if (module->id == L"module.passgen")
        {
            RenderPassGen();
        }
        else if (module->id == L"module.passwordstrength")
        {
            RenderPasswordStrength();
        }
        else if (module->id == L"module.uuidv7")
        {
            RenderUuidV7();
        }
        else if (module->id == L"module.romannum")
        {
            RenderRomanNum();
        }
        else if (module->id == L"module.regextester")
        {
            RenderRegexTester();
        }
        else if (module->id == L"about")
        {
            RenderAbout();
        }
        else
        {
            RenderPending(*module);
        }
    }

    void MainWindow::RenderDashboard()
    {
        auto page = CreatePage(
            L"WinForge Native · 視窗調校原生版",
            L"The C++/WinRT replacement shell is running side-by-side while feature parity is migrated and proven. · C++/WinRT 替代介面已經並行運行，功能會逐項移植同驗證。");

        auto status = InfoBar();
        status.IsOpen(true);
        status.IsClosable(false);
        status.Severity(InfoBarSeverity::Informational);
        status.Title(L"Migration is evidence-gated · 遷移以證據把關");
        status.Message(L"346 fixed routes and 5 dynamic route families are catalogued. A route is not marked complete until native behavior, tests, launch evidence, screenshots, and documentation all pass. · 已登記 346 條固定路線同 5 組動態路線；原生行為、測試、啟動、截圖同文件全部通過先會標記完成。");
        AutomationProperties::SetAutomationId(status, L"NativeMigrationStatus");
        page.Children().Append(status);

        std::wstringstream counts;
        counts << L"Native catalog: " << m_modules.size()
               << L" routes · 原生目錄：" << m_modules.size() << L" 條路線";
        page.Children().Append(CreateText(counts.str(), 22, true));
        page.Children().Append(CreateText(
            L"Current milestone: native shell, bilingual localization, canonical routing data, deep-link parsing, All Apps discovery, and parity automation. Existing feature pages remain the behavioral oracle until their C++ implementations pass the ledger. · 目前里程碑：原生介面、雙語、本體路線資料、深層連結解析、所有 app 搜尋同對等自動化；各功能未通過清單前，現有頁面仍然係行為基準。",
            15));

        page.Children().Append(CreateRouteButton(L"Open All Apps · 開啟所有 app", L"shell.allapps"));
        page.Children().Append(CreateRouteButton(L"Inspect Package Manager route · 檢視套件管理路線", L"module.packages"));
        page.Children().Append(CreateRouteButton(L"About the native rewrite · 關於原生重寫", L"about"));
        ShowPage(page);
    }

    void MainWindow::RenderCheckDigit()
    {
        m_checkDigitRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"Check Digit Validator", L"檢查碼驗證器" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Validate a number's check digit or checksum and see the expected value. Handles credit cards (Luhn, with brand detection), book and product barcodes, and bank IBANs.",
                L"驗證號碼嘅檢查碼／校驗碼，同時顯示應有數值；支援信用卡（Luhn，連卡種識別）、書籍同商品條碼，以及銀行 IBAN。" }.Pick(m_language));
        page.MaxWidth(820);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeCheckDigitPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native validation", L"全原生驗證" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"All six validators execute locally in standard C++ with bounded incremental mod-97 arithmetic; no CLR, network, process, file, registry, or elevation path is used.",
            L"六個驗證器全部用標準 C++ 喺本機執行，IBAN 亦用有界增量 mod-97 運算；唔會用 CLR、網絡、process、檔案、registry 或提升權限。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeCheckDigitImplementationStatus");
        page.Children().Append(nativeStatus);

        Border card;
        card.Padding(Thickness{ 18, 16, 18, 18 });
        card.CornerRadius(CornerRadius{ 8 });
        card.BorderThickness(Thickness{ 1 });
        card.BorderBrush(Application::Current().Resources().Lookup(
            box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
        card.Background(Application::Current().Resources().Lookup(
            box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

        StackPanel content;
        content.Spacing(14);

        auto schemeLabel = CreateText(
            winforge::core::LocalizedText{ L"Scheme", L"格式" }.Pick(m_language), 14, true);
        content.Children().Append(schemeLabel);

        m_checkDigitSchemePicker = ComboBox();
        m_checkDigitSchemePicker.MinWidth(260);
        m_checkDigitSchemePicker.HorizontalAlignment(HorizontalAlignment::Left);
        for (auto const& label : std::array<std::wstring, 6>{
            winforge::core::LocalizedText{ L"Luhn (credit card)", L"Luhn（信用卡）" }.Pick(m_language),
            L"ISBN-10",
            L"ISBN-13",
            L"EAN-13",
            L"UPC-A",
            winforge::core::LocalizedText{ L"IBAN (mod-97)", L"IBAN（mod-97）" }.Pick(m_language) })
        {
            ComboBoxItem item;
            item.Content(box_value(ToHString(label)));
            m_checkDigitSchemePicker.Items().Append(item);
        }
        m_checkDigitSchemePicker.SelectedIndex(m_checkDigitScheme);
        AutomationProperties::SetAutomationId(m_checkDigitSchemePicker, L"NativeCheckDigitScheme");
        AutomationProperties::SetLabeledBy(m_checkDigitSchemePicker, schemeLabel);
        AutomationProperties::SetName(
            m_checkDigitSchemePicker,
            ToHString(winforge::core::LocalizedText{
                L"Check digit scheme", L"檢查碼格式" }.Pick(m_language)));
        m_checkDigitSchemePicker.SelectionChanged(
            [this](Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&)
            {
                if (m_checkDigitRendering) return;
                auto const selected = m_checkDigitSchemePicker.SelectedIndex();
                m_checkDigitScheme = selected < 0 ? 0 : selected;
                RefreshCheckDigit();
            });
        content.Children().Append(m_checkDigitSchemePicker);

        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Value to check", L"要檢查嘅數值" }.Pick(m_language), 14, true);
        content.Children().Append(inputLabel);

        m_checkDigitInput = TextBox();
        m_checkDigitInput.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"e.g. 4111 1111 1111 1111", L"例如 4111 1111 1111 1111" }.Pick(m_language)));
        m_checkDigitInput.Text(ToHString(m_checkDigitValue));
        m_checkDigitInput.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_checkDigitInput, L"NativeCheckDigitInput");
        AutomationProperties::SetLabeledBy(m_checkDigitInput, inputLabel);
        AutomationProperties::SetName(
            m_checkDigitInput,
            ToHString(winforge::core::LocalizedText{
                L"Value to check", L"要檢查嘅數值" }.Pick(m_language)));
        m_checkDigitInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_checkDigitRendering) return;
            m_checkDigitValue = ToWide(sender.as<TextBox>().Text());
            RefreshCheckDigit();
        });
        content.Children().Append(m_checkDigitInput);

        m_checkDigitBadge = Border();
        m_checkDigitBadge.CornerRadius(CornerRadius{ 4 });
        m_checkDigitBadge.Padding(Thickness{ 12, 5, 12, 5 });
        m_checkDigitBadge.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_checkDigitBadge, L"NativeCheckDigitBadgeContainer");

        m_checkDigitBadgeText = CreateText(L"—", 14, true);
        m_checkDigitBadgeText.IsTextSelectionEnabled(false);
        AutomationProperties::SetAutomationId(m_checkDigitBadgeText, L"NativeCheckDigitBadge");
        m_checkDigitBadge.Child(m_checkDigitBadgeText);
        content.Children().Append(m_checkDigitBadge);

        m_checkDigitDetail = CreateText(L"", 14);
        m_checkDigitDetail.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_checkDigitDetail, L"NativeCheckDigitDetail");
        content.Children().Append(m_checkDigitDetail);

        m_checkDigitStatus = CreateText(L"", 12.5);
        m_checkDigitStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_checkDigitStatus, L"NativeCheckDigitStatus");
        AutomationProperties::SetLiveSetting(
            m_checkDigitStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        content.Children().Append(m_checkDigitStatus);

        card.Child(content);
        page.Children().Append(card);
        ShowPage(page);

        m_checkDigitRendering = false;
        RefreshCheckDigit();
    }

    void MainWindow::RefreshCheckDigit()
    {
        if (!m_checkDigitBadge || !m_checkDigitBadgeText || !m_checkDigitDetail || !m_checkDigitStatus)
        {
            return;
        }

        auto setBadge = [this](std::wstring_view label, Windows::UI::Color color, double opacity)
        {
            Media::SolidColorBrush brush(color);
            brush.Opacity(opacity);
            m_checkDigitBadge.Background(brush);
            m_checkDigitBadgeText.Text(ToHString(label));
            AutomationProperties::SetName(m_checkDigitBadgeText, ToHString(label));
        };

        if (!HasNonWhitespace(m_checkDigitValue))
        {
            setBadge(L"—", Windows::UI::Color{ 0xFF, 0x80, 0x80, 0x80 }, 0.35);
            m_checkDigitDetail.Text(L"");
            AutomationProperties::SetName(m_checkDigitDetail, L"");
            auto const prompt = winforge::core::LocalizedText{
                L"Type a value above to check it.", L"喺上面輸入數值嚟檢查。" }.Pick(m_language);
            AnnounceCheckDigitStatus(prompt);
            return;
        }

        using Scheme = winforge::core::checkdigit::Scheme;
        auto const scheme = [&]()
        {
            switch (m_checkDigitScheme)
            {
            case 1: return Scheme::Isbn10;
            case 2: return Scheme::Isbn13;
            case 3: return Scheme::Ean13;
            case 4: return Scheme::UpcA;
            case 5: return Scheme::Iban;
            default: return Scheme::Luhn;
            }
        }();
        auto const result = winforge::core::checkdigit::Validate(scheme, m_checkDigitValue);

        if (!result.ok)
        {
            setBadge(L"—", Windows::UI::Color{ 0xFF, 0x80, 0x80, 0x80 }, 0.35);
            m_checkDigitDetail.Text(L"");
            AutomationProperties::SetName(m_checkDigitDetail, L"");
            auto const diagnostic = winforge::core::LocalizedText{
                result.detail_en, result.detail_zh }.Pick(m_language);
            AnnounceCheckDigitStatus(diagnostic);
            return;
        }

        auto const badge = result.valid
            ? winforge::core::LocalizedText{ L"VALID", L"有效" }.Pick(m_language)
            : winforge::core::LocalizedText{ L"INVALID", L"無效" }.Pick(m_language);
        setBadge(
            badge,
            result.valid
                ? Windows::UI::Color{ 0xFF, 0x1E, 0x7A, 0x34 }
                : Windows::UI::Color{ 0xFF, 0x9B, 0x22, 0x26 },
            1.0);

        auto const detail = winforge::core::LocalizedText{
            result.detail_en, result.detail_zh }.Pick(m_language);
        m_checkDigitDetail.Text(ToHString(detail));
        AutomationProperties::SetName(m_checkDigitDetail, ToHString(detail));

        auto const status = result.valid
            ? winforge::core::LocalizedText{
                L"Check digit / checksum matches.", L"檢查碼／校驗碼吻合。" }.Pick(m_language)
            : winforge::core::LocalizedText{
                L"Check digit / checksum does NOT match.", L"檢查碼／校驗碼唔吻合。" }.Pick(m_language);
        AnnounceCheckDigitStatus(status);
    }

    void MainWindow::AnnounceCheckDigitStatus(std::wstring_view message)
    {
        if (!m_checkDigitStatus) return;

        m_checkDigitStatus.Text(ToHString(message));
        AutomationProperties::SetName(m_checkDigitStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_checkDigitStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_checkDigitStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_checkDigitStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility notification failure must not break local validation.
        }
    }

    void MainWindow::RenderBinaryText()
    {
        m_binaryTextRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"Text to Binary", L"文字轉二進位" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Turn text into space-separated UTF-8 byte codes and back in binary, decimal, octal, or hexadecimal.",
                L"將文字轉成用空格分隔嘅 UTF-8 位元組數字碼，又可以喺二進位、十進位、八進位或十六進位轉返文字。" }.Pick(m_language));
        page.MaxWidth(820);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeBinaryTextPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native UTF-8 conversion", L"全原生 UTF-8 轉換" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"Encoding and decoding run locally in standard C++. Invalid UTF-8 uses replacement characters just like the managed reference; no CLR, network, process, file, registry, or elevation path is used.",
            L"編碼同解碼全部喺本機標準 C++ 執行；無效 UTF-8 會跟受控版一樣用替代字元，唔會用 CLR、網絡、process、檔案、registry 或提升權限。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeBinaryTextImplementationStatus");
        page.Children().Append(nativeStatus);

        Border card;
        card.Padding(Thickness{ 18, 16, 18, 18 });
        card.CornerRadius(CornerRadius{ 8 });
        card.BorderThickness(Thickness{ 1 });
        card.BorderBrush(Application::Current().Resources().Lookup(
            box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
        card.Background(Application::Current().Resources().Lookup(
            box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

        StackPanel content;
        content.Spacing(14);

        auto baseLabel = CreateText(
            winforge::core::LocalizedText{ L"Numeric base", L"數字進位" }.Pick(m_language), 14, true);
        content.Children().Append(baseLabel);

        m_binaryTextBasePicker = ComboBox();
        m_binaryTextBasePicker.MinWidth(260);
        m_binaryTextBasePicker.HorizontalAlignment(HorizontalAlignment::Left);
        for (auto const& label : std::array<std::wstring, 4>{
            winforge::core::LocalizedText{ L"Binary (base 2)", L"二進位（2 進）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Decimal (base 10)", L"十進位（10 進）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Octal (base 8)", L"八進位（8 進）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Hex (base 16)", L"十六進位（16 進）" }.Pick(m_language) })
        {
            ComboBoxItem item;
            item.Content(box_value(ToHString(label)));
            m_binaryTextBasePicker.Items().Append(item);
        }
        m_binaryTextBasePicker.SelectedIndex(m_binaryTextBase);
        AutomationProperties::SetAutomationId(m_binaryTextBasePicker, L"NativeBinaryTextBase");
        AutomationProperties::SetLabeledBy(m_binaryTextBasePicker, baseLabel);
        AutomationProperties::SetName(
            m_binaryTextBasePicker,
            ToHString(winforge::core::LocalizedText{ L"Numeric base", L"數字進位" }.Pick(m_language)));
        m_binaryTextBasePicker.SelectionChanged(
            [this](Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&)
            {
                if (m_binaryTextRendering) return;
                auto const selected = m_binaryTextBasePicker.SelectedIndex();
                m_binaryTextBase = selected < 0 ? 0 : selected;
                AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                    L"Base changed — choose an action to convert.", L"已轉進位 — 請揀一個動作轉換。" }.Pick(m_language));
            });
        content.Children().Append(m_binaryTextBasePicker);

        auto note = CreateText(
            winforge::core::LocalizedText{
                L"Codes represent raw UTF-8 bytes (0–255). Binary pads every byte to 8 bits; codes may be separated by spaces, tabs, line breaks, or commas.",
                L"數字碼代表原始 UTF-8 位元組（0–255）。二進位每個位元組補足 8 個位；各個碼可以用空格、tab、換行或逗號分隔。" }.Pick(m_language),
            12);
        note.Opacity(0.78);
        content.Children().Append(note);

        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input", L"輸入" }.Pick(m_language), 14, true);
        content.Children().Append(inputLabel);

        m_binaryTextInput = TextBox();
        m_binaryTextInput.Text(ToHString(m_binaryTextInputValue));
        m_binaryTextInput.AcceptsReturn(true);
        m_binaryTextInput.TextWrapping(TextWrapping::Wrap);
        m_binaryTextInput.MinHeight(96);
        m_binaryTextInput.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_binaryTextInput, L"NativeBinaryTextInput");
        AutomationProperties::SetLabeledBy(m_binaryTextInput, inputLabel);
        AutomationProperties::SetName(
            m_binaryTextInput,
            ToHString(winforge::core::LocalizedText{ L"Binary Text input", L"文字轉二進位輸入" }.Pick(m_language)));
        m_binaryTextInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_binaryTextRendering) return;
            m_binaryTextInputValue = ToWide(sender.as<TextBox>().Text());
        });
        content.Children().Append(m_binaryTextInput);

        StackPanel actions;
        actions.Orientation(Orientation::Horizontal);
        actions.Spacing(10);

        Button encode;
        encode.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Text → codes", L"文字 → 數字碼" }.Pick(m_language))));
        encode.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(encode, L"NativeBinaryTextEncode");
        AutomationProperties::SetName(encode, ToHString(winforge::core::LocalizedText{
            L"Encode text to numeric codes", L"將文字編碼成數字碼" }.Pick(m_language)));
        encode.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            using NumericBase = winforge::core::binarytext::NumericBase;
            auto const base = m_binaryTextBase == 1 ? NumericBase::Decimal :
                m_binaryTextBase == 2 ? NumericBase::Octal :
                m_binaryTextBase == 3 ? NumericBase::Hex : NumericBase::Binary;
            auto const result = winforge::core::binarytext::Encode(m_binaryTextInputValue, base);
            if (!result.ok)
            {
                m_binaryTextOutputValue.clear();
                if (m_binaryTextOutput) m_binaryTextOutput.Text(L"");
                if (m_binaryTextOutput) AutomationProperties::SetHelpText(m_binaryTextOutput, L"");
                AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                    L"Could not encode the text.", L"無法編碼呢段文字。" }.Pick(m_language), true);
                return;
            }
            m_binaryTextOutputValue = result.text;
            if (m_binaryTextOutput) m_binaryTextOutput.Text(ToHString(result.text));
            if (m_binaryTextOutput) AutomationProperties::SetHelpText(m_binaryTextOutput, ToHString(result.text));
            AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                L"Encoded to numeric codes.", L"已編碼成數字碼。" }.Pick(m_language));
        });
        actions.Children().Append(encode);

        Button decode;
        decode.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Codes → text", L"數字碼 → 文字" }.Pick(m_language))));
        decode.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(decode, L"NativeBinaryTextDecode");
        AutomationProperties::SetName(decode, ToHString(winforge::core::LocalizedText{
            L"Decode numeric codes to text", L"將數字碼解碼成文字" }.Pick(m_language)));
        decode.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            using NumericBase = winforge::core::binarytext::NumericBase;
            auto const base = m_binaryTextBase == 1 ? NumericBase::Decimal :
                m_binaryTextBase == 2 ? NumericBase::Octal :
                m_binaryTextBase == 3 ? NumericBase::Hex : NumericBase::Binary;
            auto const result = winforge::core::binarytext::Decode(m_binaryTextInputValue, base);
            if (!result.ok)
            {
                m_binaryTextOutputValue.clear();
                if (m_binaryTextOutput) m_binaryTextOutput.Text(L"");
                if (m_binaryTextOutput) AutomationProperties::SetHelpText(m_binaryTextOutput, L"");
                AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                    L"Some codes are not valid for this base — nothing was decoded.",
                    L"有啲數字碼喺呢個進位無效 — 冇解碼到。" }.Pick(m_language), true);
                return;
            }
            m_binaryTextOutputValue = result.text;
            if (m_binaryTextOutput) m_binaryTextOutput.Text(ToHString(result.text));
            if (m_binaryTextOutput) AutomationProperties::SetHelpText(m_binaryTextOutput, ToHString(result.text));
            AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                L"Decoded back to text.", L"已解碼返做文字。" }.Pick(m_language));
        });
        actions.Children().Append(decode);

        Button moveOutput;
        moveOutput.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Move output to input", L"將輸出搬去輸入" }.Pick(m_language))));
        moveOutput.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(moveOutput, L"NativeBinaryTextSwap");
        AutomationProperties::SetName(moveOutput, ToHString(winforge::core::LocalizedText{
            L"Move output to input", L"將輸出搬去輸入" }.Pick(m_language)));
        moveOutput.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            m_binaryTextRendering = true;
            m_binaryTextInputValue = m_binaryTextOutputValue;
            m_binaryTextOutputValue.clear();
            if (m_binaryTextInput) m_binaryTextInput.Text(ToHString(m_binaryTextInputValue));
            if (m_binaryTextOutput) m_binaryTextOutput.Text(L"");
            if (m_binaryTextOutput) AutomationProperties::SetHelpText(m_binaryTextOutput, L"");
            m_binaryTextRendering = false;
            AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                L"Output moved to input.", L"已將輸出搬去輸入。" }.Pick(m_language));
        });
        actions.Children().Append(moveOutput);

        Button copy;
        copy.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Copy output", L"複製輸出" }.Pick(m_language))));
        copy.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(copy, L"NativeBinaryTextCopy");
        AutomationProperties::SetName(copy, ToHString(winforge::core::LocalizedText{
            L"Copy output", L"複製輸出" }.Pick(m_language)));
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_binaryTextOutputValue.empty())
            {
                AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                    L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_binaryTextOutputValue));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                    L"Output copied to clipboard.", L"已將輸出複製去剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceBinaryTextStatus(winforge::core::LocalizedText{
                    L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
            }
        });
        actions.Children().Append(copy);
        content.Children().Append(actions);

        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Output", L"輸出" }.Pick(m_language), 14, true);
        content.Children().Append(outputLabel);

        m_binaryTextOutput = TextBox();
        m_binaryTextOutput.Text(ToHString(m_binaryTextOutputValue));
        m_binaryTextOutput.AcceptsReturn(true);
        m_binaryTextOutput.TextWrapping(TextWrapping::Wrap);
        m_binaryTextOutput.MinHeight(96);
        m_binaryTextOutput.IsReadOnly(true);
        m_binaryTextOutput.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_binaryTextOutput, L"NativeBinaryTextOutput");
        AutomationProperties::SetLabeledBy(m_binaryTextOutput, outputLabel);
        AutomationProperties::SetName(
            m_binaryTextOutput,
            ToHString(winforge::core::LocalizedText{ L"Binary Text output", L"文字轉二進位輸出" }.Pick(m_language)));
        AutomationProperties::SetHelpText(m_binaryTextOutput, ToHString(m_binaryTextOutputValue));
        content.Children().Append(m_binaryTextOutput);

        m_binaryTextStatus = CreateText(L"", 12.5);
        m_binaryTextStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_binaryTextStatus, L"NativeBinaryTextStatus");
        AutomationProperties::SetLiveSetting(
            m_binaryTextStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        content.Children().Append(m_binaryTextStatus);

        card.Child(content);
        page.Children().Append(card);
        ShowPage(page);

        m_binaryTextRendering = false;
        AnnounceBinaryTextStatus(winforge::core::LocalizedText{
            L"Ready.", L"準備好。" }.Pick(m_language));
    }

    void MainWindow::AnnounceBinaryTextStatus(std::wstring_view message, bool warning)
    {
        if (!m_binaryTextStatus) return;

        m_binaryTextStatus.Text(ToHString(message));
        m_binaryTextStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_binaryTextStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_binaryTextStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_binaryTextStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_binaryTextStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility notification failure must not break local conversion.
        }
    }

    void MainWindow::RenderCodec()
    {
        m_codecRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Base32 / 58 / 85", L"Base32 / 58 / 85 編解碼" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Encode or decode UTF-8 text with RFC 4648 Base32, Bitcoin Base58, or Adobe Ascii85.",
                L"用 RFC 4648 Base32、Bitcoin Base58 或 Adobe Ascii85 編碼／解碼 UTF-8 文字。" }.Pick(m_language));
        page.MaxWidth(860);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeCodecPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native text codecs", L"全原生文字編解碼" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"Base32, Base58, Ascii85, UTF-8 conversion, and copy actions run locally in standard C++. Invalid input fails atomically; clipboard writes only happen after explicit Copy.",
            L"Base32、Base58、Ascii85、UTF-8 轉換同複製動作都喺本機標準 C++ 執行；無效輸入會原子式失敗，只有明確 Copy 先會寫剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeCodecImplementationStatus");
        page.Children().Append(nativeStatus);

        Border card;
        card.Padding(Thickness{ 18, 16, 18, 18 });
        card.CornerRadius(CornerRadius{ 8 });
        card.BorderThickness(Thickness{ 1 });
        card.BorderBrush(Application::Current().Resources().Lookup(
            box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
        card.Background(Application::Current().Resources().Lookup(
            box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

        StackPanel content;
        content.Spacing(14);

        auto codecLabel = CreateText(
            winforge::core::LocalizedText{ L"Codec", L"編碼方式" }.Pick(m_language), 14, true);
        content.Children().Append(codecLabel);

        m_codecPicker = ComboBox();
        m_codecPicker.MinWidth(300);
        AutomationProperties::SetAutomationId(m_codecPicker, L"NativeCodecPicker");
        AutomationProperties::SetLabeledBy(m_codecPicker, codecLabel);
        AutomationProperties::SetName(m_codecPicker, ToHString(winforge::core::LocalizedText{
            L"Text codec", L"文字編碼方式" }.Pick(m_language)));
        for (auto const& label : {
            L"Base32 (RFC 4648, padded)",
            L"Base32 (no padding)",
            L"Base58 (Bitcoin)",
            L"Ascii85 (Adobe)" })
        {
            ComboBoxItem item;
            item.Content(box_value(label));
            m_codecPicker.Items().Append(item);
        }
        m_codecPicker.SelectedIndex(m_codecEncoding);
        m_codecPicker.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_codecRendering) return;
            auto const selected = sender.as<ComboBox>().SelectedIndex();
            m_codecEncoding = selected < 0 ? 0 : selected;
            AnnounceCodecStatus(winforge::core::LocalizedText{
                L"Codec changed — choose Encode or Decode.", L"已切換編碼方式 — 請揀編碼或者解碼。" }.Pick(m_language));
        });
        content.Children().Append(m_codecPicker);

        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input", L"輸入" }.Pick(m_language), 14, true);
        content.Children().Append(inputLabel);
        m_codecInput = TextBox();
        m_codecInput.Text(ToHString(m_codecInputValue));
        m_codecInput.AcceptsReturn(true);
        m_codecInput.TextWrapping(TextWrapping::Wrap);
        m_codecInput.MinHeight(110);
        m_codecInput.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_codecInput, L"NativeCodecInput");
        AutomationProperties::SetLabeledBy(m_codecInput, inputLabel);
        AutomationProperties::SetName(m_codecInput, ToHString(winforge::core::LocalizedText{
            L"Codec input", L"編解碼輸入" }.Pick(m_language)));
        m_codecInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_codecRendering) return;
            m_codecInputValue = ToWide(sender.as<TextBox>().Text());
        });
        content.Children().Append(m_codecInput);

        StackPanel actions;
        actions.Orientation(Orientation::Horizontal);
        actions.Spacing(9);

        auto selectedEncoding = [this]()
        {
            using Encoding = winforge::core::codec::Encoding;
            switch (m_codecEncoding)
            {
            case 1: return Encoding::Base32NoPad;
            case 2: return Encoding::Base58;
            case 3: return Encoding::Ascii85;
            default: return Encoding::Base32;
            }
        };

        Button encode;
        encode.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Encode", L"編碼" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(encode, L"NativeCodecEncode");
        AutomationProperties::SetName(encode, ToHString(winforge::core::LocalizedText{
            L"Encode UTF-8 text", L"編碼 UTF-8 文字" }.Pick(m_language)));
        encode.Click([this, selectedEncoding](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            // UI Automation's ValuePattern.SetValue and WinUI's TextChanged
            // notification can cross the dispatcher boundary in either order.
            // Read the live editor at command time so an explicit Encode never
            // sees stale state or silently converts an earlier value.
            if (m_codecInput)
            {
                m_codecInputValue = ToWide(m_codecInput.Text());
            }
            auto const result = winforge::core::codec::Encode(m_codecInputValue, selectedEncoding());
            if (!result.ok)
            {
                m_codecOutputValue.clear();
                if (m_codecOutput) m_codecOutput.Text(L"");
                AnnounceCodecStatus(winforge::core::LocalizedText{
                    L"Could not encode the input.", L"無法編碼呢段輸入。" }.Pick(m_language), true);
                return;
            }
            m_codecOutputValue = result.text;
            if (m_codecOutput) m_codecOutput.Text(ToHString(m_codecOutputValue));
            if (m_codecOutput) AutomationProperties::SetHelpText(m_codecOutput, ToHString(m_codecOutputValue));
            AnnounceCodecStatus(winforge::core::LocalizedText{
                L"Encoded successfully.", L"編碼成功。" }.Pick(m_language));
        });
        actions.Children().Append(encode);

        Button decode;
        decode.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Decode", L"解碼" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(decode, L"NativeCodecDecode");
        AutomationProperties::SetName(decode, ToHString(winforge::core::LocalizedText{
            L"Decode codec text", L"解碼編碼文字" }.Pick(m_language)));
        decode.Click([this, selectedEncoding](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            // Match Encode: command execution is based on the visible editor,
            // not a deferred TextChanged callback.
            if (m_codecInput)
            {
                m_codecInputValue = ToWide(m_codecInput.Text());
            }
            auto const result = winforge::core::codec::Decode(m_codecInputValue, selectedEncoding());
            if (!result.ok)
            {
                m_codecOutputValue.clear();
                if (m_codecOutput) m_codecOutput.Text(L"");
                AnnounceCodecStatus(winforge::core::LocalizedText{
                    L"Input is not valid for this codec.", L"輸入唔係呢種編碼嘅有效格式。" }.Pick(m_language), true);
                return;
            }
            m_codecOutputValue = result.text;
            if (m_codecOutput) m_codecOutput.Text(ToHString(m_codecOutputValue));
            if (m_codecOutput) AutomationProperties::SetHelpText(m_codecOutput, ToHString(m_codecOutputValue));
            AnnounceCodecStatus(winforge::core::LocalizedText{
                L"Decoded successfully.", L"解碼成功。" }.Pick(m_language));
        });
        actions.Children().Append(decode);

        Button swap;
        swap.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Swap", L"對調" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(swap, L"NativeCodecSwap");
        swap.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            m_codecRendering = true;
            m_codecInputValue = m_codecOutputValue;
            m_codecOutputValue.clear();
            if (m_codecInput) m_codecInput.Text(ToHString(m_codecInputValue));
            if (m_codecOutput) m_codecOutput.Text(L"");
            m_codecRendering = false;
            AnnounceCodecStatus(winforge::core::LocalizedText{
                L"Output moved to input.", L"已將輸出搬去輸入。" }.Pick(m_language));
        });
        actions.Children().Append(swap);

        Button copy;
        copy.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy output", L"複製輸出" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(copy, L"NativeCodecCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_codecOutputValue.empty())
            {
                AnnounceCodecStatus(winforge::core::LocalizedText{
                    L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_codecOutputValue));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceCodecStatus(winforge::core::LocalizedText{
                    L"Copied to clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceCodecStatus(winforge::core::LocalizedText{
                    L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
            }
        });
        actions.Children().Append(copy);
        content.Children().Append(actions);

        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Output", L"輸出" }.Pick(m_language), 14, true);
        content.Children().Append(outputLabel);
        m_codecOutput = TextBox();
        m_codecOutput.IsReadOnly(true);
        m_codecOutput.AcceptsReturn(true);
        m_codecOutput.TextWrapping(TextWrapping::Wrap);
        m_codecOutput.MinHeight(110);
        m_codecOutput.IsSpellCheckEnabled(false);
        m_codecOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_codecOutput.Text(ToHString(m_codecOutputValue));
        AutomationProperties::SetAutomationId(m_codecOutput, L"NativeCodecOutput");
        AutomationProperties::SetLabeledBy(m_codecOutput, outputLabel);
        AutomationProperties::SetName(m_codecOutput, ToHString(winforge::core::LocalizedText{
            L"Codec output", L"編解碼輸出" }.Pick(m_language)));
        AutomationProperties::SetHelpText(m_codecOutput, ToHString(m_codecOutputValue));
        content.Children().Append(m_codecOutput);

        auto note = CreateText(winforge::core::LocalizedText{
            L"Base32 is uppercase RFC 4648; Base58 preserves leading zero bytes; Ascii85 uses Adobe <~ ~> markers and the z shortcut.",
            L"Base32 係大楷 RFC 4648；Base58 會保留開頭零位元組；Ascii85 用 Adobe <~ ~> 標記同 z 快捷字元。" }.Pick(m_language), 12);
        note.Opacity(0.78);
        content.Children().Append(note);

        card.Child(content);
        page.Children().Append(card);

        m_codecStatus = CreateText(L"", 12.5);
        m_codecStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_codecStatus, L"NativeCodecStatus");
        AutomationProperties::SetLiveSetting(
            m_codecStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_codecStatus);

        ShowPage(page);
        m_codecRendering = false;
        AnnounceCodecStatus(winforge::core::LocalizedText{
            L"Native codec tools ready.", L"原生編解碼工具已就緒。" }.Pick(m_language));
    }

    void MainWindow::AnnounceCodecStatus(std::wstring_view message, bool warning)
    {
        if (!m_codecStatus) return;
        m_codecStatus.Text(ToHString(message));
        m_codecStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_codecStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_codecStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(m_codecStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(m_codecStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
        }
    }

    void MainWindow::RenderCaseConvert()
    {
        m_caseConvertRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"Case Converter", L"大小寫轉換" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Type any text and see the common naming forms update live: camelCase, PascalCase, snake_case, kebab-case, CONSTANT_CASE, Title Case, Sentence case, dot.case, path/case, and Train-Case.",
                L"輸入任何文字都會即時轉晒常見命名格式：camelCase、PascalCase、snake_case、kebab-case、CONSTANT_CASE、Title Case、Sentence case、dot.case、path/case 同 Train-Case。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeCaseConvertPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native naming conversion", L"全原生命名轉換" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"Tokenization, casing, and copy actions run locally in standard C++. The native page keeps the managed split rules, including punctuation boundaries, digit transitions, and invariant casing.",
            L"分詞、轉寫同複製動作都喺本機用標準 C++ 執行。呢個原生頁會跟返受控版嘅分詞規則，包括標點邊界、數字轉折同 invariant 大小寫。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeCaseConvertImplementationStatus");
        page.Children().Append(nativeStatus);

        Border card;
        card.Padding(Thickness{ 18, 16, 18, 18 });
        card.CornerRadius(CornerRadius{ 8 });
        card.BorderThickness(Thickness{ 1 });
        card.BorderBrush(Application::Current().Resources().Lookup(
            box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
        card.Background(Application::Current().Resources().Lookup(
            box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

        StackPanel content;
        content.Spacing(14);

        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input text", L"輸入文字" }.Pick(m_language), 14, true);
        content.Children().Append(inputLabel);

        m_caseConvertInput = TextBox();
        m_caseConvertInput.AcceptsReturn(true);
        m_caseConvertInput.TextWrapping(TextWrapping::Wrap);
        m_caseConvertInput.MinHeight(96);
        m_caseConvertInput.IsSpellCheckEnabled(false);
        m_caseConvertInput.Text(ToHString(m_caseConvertInputValue));
        AutomationProperties::SetAutomationId(m_caseConvertInput, L"NativeCaseConvertInput");
        AutomationProperties::SetLabeledBy(m_caseConvertInput, inputLabel);
        AutomationProperties::SetName(m_caseConvertInput, ToHString(winforge::core::LocalizedText{
            L"Input text", L"輸入文字" }.Pick(m_language)));
        m_caseConvertInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_caseConvertRendering) return;
            m_caseConvertInputValue = ToWide(sender.as<TextBox>().Text());
            RefreshCaseConvert();
        });
        content.Children().Append(m_caseConvertInput);

        m_caseConvertStatus = CreateText(L"", 12.5);
        m_caseConvertStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_caseConvertStatus, L"NativeCaseConvertStatus");
        AutomationProperties::SetLiveSetting(
            m_caseConvertStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        content.Children().Append(m_caseConvertStatus);

        m_caseConvertRows = StackPanel();
        m_caseConvertRows.Spacing(10);
        AutomationProperties::SetAutomationId(m_caseConvertRows, L"NativeCaseConvertRows");
        content.Children().Append(m_caseConvertRows);

        card.Child(content);
        page.Children().Append(card);
        ShowPage(page);

        m_caseConvertRendering = false;
        RefreshCaseConvert();
    }

    void MainWindow::RefreshCaseConvert()
    {
        if (!m_caseConvertRows || !m_caseConvertStatus)
        {
            return;
        }

        auto const forms = winforge::core::caseconvert::AllForms(m_caseConvertInputValue);
        m_caseConvertRows.Children().Clear();

        struct RowSpec
        {
            std::wstring automation;
        };

        static const std::array<RowSpec, 10> rows{{
            { L"Camel" },
            { L"Pascal" },
            { L"Snake" },
            { L"Kebab" },
            { L"Constant" },
            { L"Title" },
            { L"Sentence" },
            { L"Dot" },
            { L"Path" },
            { L"Train" },
        }};

        for (std::size_t index = 0; index < rows.size() && index < forms.size(); ++index)
        {
            Border rowCard;
            rowCard.Padding(Thickness{ 12, 10, 12, 10 });
            rowCard.CornerRadius(CornerRadius{ 6 });
            rowCard.BorderThickness(Thickness{ 1 });
            rowCard.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            rowCard.Background(Application::Current().Resources().Lookup(
                box_value(L"SubtleFillColorSecondaryBrush")).as<Media::Brush>());

            StackPanel row;
            row.Orientation(Orientation::Horizontal);
            row.Spacing(10);
            row.VerticalAlignment(VerticalAlignment::Center);

            auto label = CreateText(
                winforge::core::LocalizedText{ forms[index].en, forms[index].zh }.Pick(m_language),
                13.5,
                true);
            label.Width(176);
            label.TextWrapping(TextWrapping::Wrap);
            label.VerticalAlignment(VerticalAlignment::Center);
            AutomationProperties::SetAutomationId(
                label,
                ToHString(std::wstring(L"NativeCaseConvertLabel") + rows[index].automation));
            row.Children().Append(label);

            auto value = TextBox();
            value.Text(ToHString(forms[index].value));
            value.IsReadOnly(true);
            value.IsSpellCheckEnabled(false);
            value.FontFamily(Media::FontFamily(L"Consolas"));
            value.MinWidth(380);
            value.VerticalAlignment(VerticalAlignment::Center);
            AutomationProperties::SetAutomationId(
                value,
                ToHString(std::wstring(L"NativeCaseConvertOutput") + rows[index].automation));
            AutomationProperties::SetName(value, ToHString(forms[index].en));
            AutomationProperties::SetHelpText(value, ToHString(forms[index].value));
            row.Children().Append(value);

            auto copy = Button();
            copy.Content(box_value(L"Copy"));
            copy.Padding(Thickness{ 14, 8, 14, 8 });
            AutomationProperties::SetAutomationId(
                copy,
                ToHString(std::wstring(L"NativeCaseConvertCopy") + rows[index].automation));
            AutomationProperties::SetName(copy, ToHString(winforge::core::LocalizedText{
                L"Copy", L"複製" }.Pick(m_language)));
            copy.Click([this, valueText = forms[index].value](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                try
                {
                    Windows::ApplicationModel::DataTransfer::DataPackage package;
                    package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                    package.SetText(ToHString(valueText));
                    Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                    AnnounceCaseConvertStatus(valueText.empty()
                        ? winforge::core::LocalizedText{ L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language)
                        : winforge::core::LocalizedText{ L"Output copied to clipboard.", L"已將結果複製去剪貼簿。" }.Pick(m_language));
                }
                catch (...)
                {
                    AnnounceCaseConvertStatus(
                        winforge::core::LocalizedText{ L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language),
                        true);
                }
            });
            row.Children().Append(copy);
            Grid::SetColumn(copy, 2);

            rowCard.Child(row);
            m_caseConvertRows.Children().Append(rowCard);
        }

        auto const words = winforge::core::caseconvert::Tokenize(m_caseConvertInputValue).size();
        std::wstring const englishStatus = std::to_wstring(words) + L" word(s) detected.";
        std::wstring const cantoneseStatus = std::wstring(L"偵測到 ") + std::to_wstring(words) + L" 個字。";
        auto const status = m_caseConvertInputValue.empty()
            ? winforge::core::LocalizedText{ L"Type above to see the conversions.", L"喺上面輸入就會見到轉換結果。" }.Pick(m_language)
            : winforge::core::LocalizedText{ englishStatus, cantoneseStatus }.Pick(m_language);
        AnnounceCaseConvertStatus(status);
    }

    void MainWindow::AnnounceCaseConvertStatus(std::wstring_view message, bool warning)
    {
        if (!m_caseConvertStatus) return;

        m_caseConvertStatus.Text(ToHString(message));
        m_caseConvertStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_caseConvertStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_caseConvertStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_caseConvertStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_caseConvertStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility notification failure must not break local conversion.
        }
    }

    void MainWindow::RenderRomanNum()
    {
        m_romanNumRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Roman Numerals", L"羅馬數字" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Convert whole numbers and canonical Roman numerals in both directions. Standard mode covers 1–3,999; Extended mode reaches 3,999,999 with vinculum overlines.",
                L"整數同標準羅馬數字可以雙向轉換。標準模式支援 1–3,999；擴充模式用橫線記法可以去到 3,999,999。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeRomanNumPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native Roman numeral conversion", L"全原生羅馬數字轉換" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Standard and vinculum conversion, strict canonical validation, parenthetical ×1000 input, and copy actions run locally in standard C++. Clipboard writes require an explicit Copy button.",
            L"標準同橫線記法轉換、嚴格標準寫法驗證、括號 ×1000 輸入同複製動作都喺本機標準 C++ 執行；一定要明確撳 Copy 先會寫入剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeRomanNumImplementationStatus");
        page.Children().Append(implementation);

        StackPanel range;
        range.Orientation(Orientation::Vertical);
        range.Spacing(6);
        range.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_romanNumExtendedSwitch = ToggleSwitch();
        m_romanNumExtendedSwitch.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Extended range (vinculum)", L"擴充範圍（橫線記法）" }.Pick(m_language))));
        m_romanNumExtendedSwitch.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
        m_romanNumExtendedSwitch.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
        m_romanNumExtendedSwitch.IsOn(m_romanNumExtended);
        AutomationProperties::SetAutomationId(m_romanNumExtendedSwitch, L"NativeRomanNumExtendedSwitch");
        AutomationProperties::SetName(m_romanNumExtendedSwitch, ToHString(winforge::core::LocalizedText{
            L"Extended vinculum range", L"擴充橫線範圍" }.Pick(m_language)));
        m_romanNumExtendedSwitch.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_romanNumRendering) return;
            m_romanNumExtended = sender.as<ToggleSwitch>().IsOn();
            RefreshRomanNum();
        });
        range.Children().Append(m_romanNumExtendedSwitch);
        auto rangeNote = CreateText(winforge::core::LocalizedText{
            L"A bar over each letter multiplies it by 1,000 (for example, I̅V̅ = 4,000). Input also accepts the canonical (IV) parenthetical form.",
            L"每個字母上面加橫線即係乘 1,000（例如 I̅V̅ = 4,000）。輸入亦接受標準嘅 (IV) 括號寫法。" }.Pick(m_language), 12);
        rangeNote.Opacity(0.80);
        rangeNote.TextWrapping(TextWrapping::Wrap);
        rangeNote.HorizontalAlignment(HorizontalAlignment::Stretch);
        rangeNote.MaxWidth(760);
        AutomationProperties::SetAutomationId(rangeNote, L"NativeRomanNumExtendedNote");
        range.Children().Append(rangeNote);
        page.Children().Append(range);

        auto makeCard = []()
        {
            Border card;
            card.Padding(Thickness{ 18, 16, 18, 18 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            return card;
        };

        Border numberCard = makeCard();
        StackPanel numberContent;
        numberContent.Spacing(10);
        numberContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Number → Roman", L"數字 → 羅馬" }.Pick(m_language), 15, true));
        auto numberLabel = CreateText(
            winforge::core::LocalizedText{ L"Whole number", L"整數" }.Pick(m_language), 13.5, true);
        numberContent.Children().Append(numberLabel);
        m_romanNumNumberInput = TextBox();
        m_romanNumNumberInput.Text(ToHString(m_romanNumNumberInputValue));
        m_romanNumNumberInput.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"1994", L"1994" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(m_romanNumNumberInput, L"NativeRomanNumNumberInput");
        AutomationProperties::SetLabeledBy(m_romanNumNumberInput, numberLabel);
        AutomationProperties::SetName(m_romanNumNumberInput, ToHString(winforge::core::LocalizedText{
            L"Whole number input", L"整數輸入" }.Pick(m_language)));
        m_romanNumNumberInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_romanNumRendering) return;
            m_romanNumNumberInputValue = ToWide(sender.as<TextBox>().Text());
            RefreshRomanNum(true, false);
        });
        numberContent.Children().Append(m_romanNumNumberInput);
        auto romanOutputLabel = CreateText(
            winforge::core::LocalizedText{ L"Roman numeral", L"羅馬數字" }.Pick(m_language), 13.5, true);
        numberContent.Children().Append(romanOutputLabel);
        m_romanNumRomanOutput = TextBox();
        m_romanNumRomanOutput.IsReadOnly(true);
        m_romanNumRomanOutput.IsSpellCheckEnabled(false);
        m_romanNumRomanOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_romanNumRomanOutput.MinHeight(42);
        m_romanNumRomanOutput.Text(ToHString(m_romanNumRomanOutputValue));
        AutomationProperties::SetAutomationId(m_romanNumRomanOutput, L"NativeRomanNumRomanOutput");
        AutomationProperties::SetLabeledBy(m_romanNumRomanOutput, romanOutputLabel);
        AutomationProperties::SetName(m_romanNumRomanOutput, ToHString(winforge::core::LocalizedText{
            L"Roman numeral output", L"羅馬數字輸出" }.Pick(m_language)));
        numberContent.Children().Append(m_romanNumRomanOutput);
        m_romanNumCopyRoman = Button();
        m_romanNumCopyRoman.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy Roman numeral", L"複製羅馬數字" }.Pick(m_language))));
        m_romanNumCopyRoman.HorizontalAlignment(HorizontalAlignment::Left);
        m_romanNumCopyRoman.IsEnabled(!m_romanNumRomanOutputValue.empty());
        AutomationProperties::SetAutomationId(m_romanNumCopyRoman, L"NativeRomanNumCopyRoman");
        AutomationProperties::SetName(m_romanNumCopyRoman, ToHString(winforge::core::LocalizedText{
            L"Copy Roman numeral", L"複製羅馬數字" }.Pick(m_language)));
        m_romanNumCopyRoman.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_romanNumRomanOutputValue.empty())
            {
                AnnounceRomanNumStatus(winforge::core::LocalizedText{
                    L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_romanNumRomanOutputValue));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceRomanNumStatus(winforge::core::LocalizedText{
                    L"Copied: " + m_romanNumRomanOutputValue,
                    L"已複製：" + m_romanNumRomanOutputValue }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceRomanNumStatus(winforge::core::LocalizedText{
                    L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
            }
        });
        numberContent.Children().Append(m_romanNumCopyRoman);
        m_romanNumRomanBreakdown = CreateText(L"", 12);
        m_romanNumRomanBreakdown.Opacity(0.80);
        m_romanNumRomanBreakdown.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_romanNumRomanBreakdown, L"NativeRomanNumRomanBreakdown");
        numberContent.Children().Append(m_romanNumRomanBreakdown);
        numberCard.Child(numberContent);
        page.Children().Append(numberCard);

        Border romanCard = makeCard();
        StackPanel romanContent;
        romanContent.Spacing(10);
        romanContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Roman → Number", L"羅馬 → 數字" }.Pick(m_language), 15, true));
        auto romanInputLabel = CreateText(
            winforge::core::LocalizedText{ L"Canonical Roman numeral", L"標準羅馬數字" }.Pick(m_language), 13.5, true);
        romanContent.Children().Append(romanInputLabel);
        m_romanNumRomanInput = TextBox();
        m_romanNumRomanInput.Text(ToHString(m_romanNumRomanInputValue));
        m_romanNumRomanInput.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"MCMXCIV", L"MCMXCIV" }.Pick(m_language)));
        m_romanNumRomanInput.IsSpellCheckEnabled(false);
        m_romanNumRomanInput.FontFamily(Media::FontFamily(L"Consolas"));
        AutomationProperties::SetAutomationId(m_romanNumRomanInput, L"NativeRomanNumRomanInput");
        AutomationProperties::SetLabeledBy(m_romanNumRomanInput, romanInputLabel);
        AutomationProperties::SetName(m_romanNumRomanInput, ToHString(winforge::core::LocalizedText{
            L"Roman numeral input", L"羅馬數字輸入" }.Pick(m_language)));
        m_romanNumRomanInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_romanNumRendering) return;
            m_romanNumRomanInputValue = ToWide(sender.as<TextBox>().Text());
            RefreshRomanNum(false, true);
        });
        romanContent.Children().Append(m_romanNumRomanInput);
        auto numberOutputLabel = CreateText(
            winforge::core::LocalizedText{ L"Whole number", L"整數" }.Pick(m_language), 13.5, true);
        romanContent.Children().Append(numberOutputLabel);
        m_romanNumNumberOutput = TextBox();
        m_romanNumNumberOutput.IsReadOnly(true);
        m_romanNumNumberOutput.IsSpellCheckEnabled(false);
        m_romanNumNumberOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_romanNumNumberOutput.MinHeight(42);
        m_romanNumNumberOutput.Text(ToHString(m_romanNumNumberOutputValue));
        AutomationProperties::SetAutomationId(m_romanNumNumberOutput, L"NativeRomanNumNumberOutput");
        AutomationProperties::SetLabeledBy(m_romanNumNumberOutput, numberOutputLabel);
        AutomationProperties::SetName(m_romanNumNumberOutput, ToHString(winforge::core::LocalizedText{
            L"Whole number output", L"整數輸出" }.Pick(m_language)));
        romanContent.Children().Append(m_romanNumNumberOutput);
        m_romanNumCopyNumber = Button();
        m_romanNumCopyNumber.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy number", L"複製數字" }.Pick(m_language))));
        m_romanNumCopyNumber.HorizontalAlignment(HorizontalAlignment::Left);
        m_romanNumCopyNumber.IsEnabled(!m_romanNumNumberOutputValue.empty());
        AutomationProperties::SetAutomationId(m_romanNumCopyNumber, L"NativeRomanNumCopyNumber");
        AutomationProperties::SetName(m_romanNumCopyNumber, ToHString(winforge::core::LocalizedText{
            L"Copy whole number", L"複製整數" }.Pick(m_language)));
        m_romanNumCopyNumber.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_romanNumNumberOutputValue.empty())
            {
                AnnounceRomanNumStatus(winforge::core::LocalizedText{
                    L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_romanNumNumberOutputValue));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceRomanNumStatus(winforge::core::LocalizedText{
                    L"Copied: " + m_romanNumNumberOutputValue,
                    L"已複製：" + m_romanNumNumberOutputValue }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceRomanNumStatus(winforge::core::LocalizedText{
                    L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
            }
        });
        romanContent.Children().Append(m_romanNumCopyNumber);
        m_romanNumNumberBreakdown = CreateText(L"", 12);
        m_romanNumNumberBreakdown.Opacity(0.80);
        m_romanNumNumberBreakdown.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_romanNumNumberBreakdown, L"NativeRomanNumNumberBreakdown");
        romanContent.Children().Append(m_romanNumNumberBreakdown);
        romanCard.Child(romanContent);
        page.Children().Append(romanCard);

        m_romanNumStatus = CreateText(L"", 12.5);
        m_romanNumStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_romanNumStatus, L"NativeRomanNumStatus");
        AutomationProperties::SetLiveSetting(
            m_romanNumStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_romanNumStatus);

        ShowPage(page);
        m_romanNumRendering = false;
        if (HasNonWhitespace(m_romanNumNumberInputValue) || HasNonWhitespace(m_romanNumRomanInputValue))
        {
            RefreshRomanNum();
        }
        else
        {
            AnnounceRomanNumStatus(winforge::core::LocalizedText{
                L"Native Roman numeral tools ready.", L"原生羅馬數字工具已就緒。" }.Pick(m_language));
        }
    }

    void MainWindow::RefreshRomanNum(bool refreshNumber, bool refreshRoman)
    {
        if (!m_romanNumStatus) return;

        auto setRomanOutput = [this](std::wstring_view displayed, std::wstring_view breakdown)
        {
            if (m_romanNumRomanOutput)
            {
                m_romanNumRomanOutput.Text(ToHString(displayed));
                AutomationProperties::SetHelpText(m_romanNumRomanOutput, ToHString(m_romanNumRomanOutputValue));
            }
            if (m_romanNumRomanBreakdown)
            {
                m_romanNumRomanBreakdown.Text(ToHString(breakdown));
                AutomationProperties::SetName(m_romanNumRomanBreakdown, ToHString(breakdown));
            }
            if (m_romanNumCopyRoman) m_romanNumCopyRoman.IsEnabled(!m_romanNumRomanOutputValue.empty());
        };
        auto setNumberOutput = [this](std::wstring_view displayed, std::wstring_view breakdown)
        {
            if (m_romanNumNumberOutput)
            {
                m_romanNumNumberOutput.Text(ToHString(displayed));
                AutomationProperties::SetHelpText(m_romanNumNumberOutput, ToHString(m_romanNumNumberOutputValue));
            }
            if (m_romanNumNumberBreakdown)
            {
                m_romanNumNumberBreakdown.Text(ToHString(breakdown));
                AutomationProperties::SetName(m_romanNumNumberBreakdown, ToHString(breakdown));
            }
            if (m_romanNumCopyNumber) m_romanNumCopyNumber.IsEnabled(!m_romanNumNumberOutputValue.empty());
        };

        if (refreshNumber)
        {
            if (!HasNonWhitespace(m_romanNumNumberInputValue))
            {
                m_romanNumRomanOutputValue.clear();
                setRomanOutput(L"", L"");
            }
            else
            {
                std::int64_t value{};
                if (!winforge::core::romannum::TryParseInteger(m_romanNumNumberInputValue, value))
                {
                    m_romanNumRomanOutputValue.clear();
                    setRomanOutput(L"—", L"");
                    AnnounceRomanNumStatus(winforge::core::LocalizedText{
                        L"Enter a whole number.", L"請輸入整數。" }.Pick(m_language), true);
                }
                else
                {
                    auto const result = winforge::core::romannum::ToRoman(value, m_romanNumExtended);
                    if (!result.ok)
                    {
                        m_romanNumRomanOutputValue.clear();
                        setRomanOutput(L"—", L"");
                        AnnounceRomanNumStatus(winforge::core::LocalizedText{
                            result.reason_en, result.reason_zh }.Pick(m_language), true);
                    }
                    else
                    {
                        m_romanNumRomanOutputValue = result.roman;
                        auto const breakdown = winforge::core::romannum::FormatGrouped(value) + L" = " + result.breakdown;
                        setRomanOutput(m_romanNumRomanOutputValue, breakdown);
                        AnnounceRomanNumStatus(winforge::core::LocalizedText{
                            L"Converted number to Roman numeral.", L"已將數字轉做羅馬數字。" }.Pick(m_language));
                    }
                }
            }
        }

        if (refreshRoman)
        {
            if (!HasNonWhitespace(m_romanNumRomanInputValue))
            {
                m_romanNumNumberOutputValue.clear();
                setNumberOutput(L"", L"");
            }
            else
            {
                auto const result = winforge::core::romannum::ToNumber(m_romanNumRomanInputValue, m_romanNumExtended);
                if (!result.ok)
                {
                    m_romanNumNumberOutputValue.clear();
                    setNumberOutput(L"—", L"");
                    AnnounceRomanNumStatus(winforge::core::LocalizedText{
                        result.reason_en, result.reason_zh }.Pick(m_language), true);
                }
                else
                {
                    m_romanNumNumberOutputValue = winforge::core::romannum::FormatGrouped(result.value);
                    auto const breakdown = result.breakdown.empty() ? L"" : L"= " + result.breakdown;
                    setNumberOutput(m_romanNumNumberOutputValue, breakdown);
                    AnnounceRomanNumStatus(winforge::core::LocalizedText{
                        L"Converted Roman numeral to number.", L"已將羅馬數字轉做整數。" }.Pick(m_language));
                }
            }
        }
    }

    void MainWindow::AnnounceRomanNumStatus(std::wstring_view message, bool warning)
    {
        if (!m_romanNumStatus) return;
        m_romanNumStatus.Text(ToHString(message));
        m_romanNumStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_romanNumStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_romanNumStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_romanNumStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_romanNumStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility reporting must never interrupt local conversion.
        }
    }

    void MainWindow::RenderGuidGen()
    {
        m_guidGenRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"GUID & ID Generator", L"GUID 同 ID 產生器" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Generate GUIDs, time-sortable ULIDs, nano-style URL-safe random IDs, and inspect GUID bytes/version/variant using a native cryptographic random source.",
                L"用原生加密級隨機源產生 GUID、可按時間排序嘅 ULID、nano 式 URL-safe 隨機 ID，亦可以拆解 GUID 位元組／版本／變體。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeGuidGenPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native identifier generation", L"全原生識別碼產生" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"GUID, bulk GUID, ULID, nano-ID, inspector, and copy actions run locally in C++ with cryptographic randomness. Clipboard writes only happen after explicit Copy buttons.",
            L"GUID、批量 GUID、ULID、nano-ID、拆解器同複製動作都喺本機 C++ 執行，並使用加密級隨機；只有明確撳 Copy 先會寫入剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeGuidGenImplementationStatus");
        page.Children().Append(nativeStatus);

        auto makeCard = []()
        {
            Border card;
            card.Padding(Thickness{ 18, 16, 18, 18 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            return card;
        };

        auto copyText = [this](std::wstring value)
        {
            try
            {
                if (value.empty())
                {
                    AnnounceGuidGenStatus(
                        winforge::core::LocalizedText{ L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language),
                        true);
                    return;
                }
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(value));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceGuidGenStatus(
                    winforge::core::LocalizedText{ L"Copied to clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceGuidGenStatus(
                    winforge::core::LocalizedText{ L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language),
                    true);
            }
        };

        Border guidCard = makeCard();
        StackPanel guidContent;
        guidContent.Spacing(12);
        guidContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"GUID", L"GUID" }.Pick(m_language),
            15,
            true));

        StackPanel guidOptions;
        guidOptions.Orientation(Orientation::Horizontal);
        guidOptions.Spacing(10);
        guidOptions.VerticalAlignment(VerticalAlignment::Center);

        auto formatLabel = CreateText(
            winforge::core::LocalizedText{ L"Format", L"格式" }.Pick(m_language),
            14,
            true);
        formatLabel.VerticalAlignment(VerticalAlignment::Center);
        guidOptions.Children().Append(formatLabel);

        m_guidGenFormatPicker = ComboBox();
        m_guidGenFormatPicker.MinWidth(230);
        AutomationProperties::SetAutomationId(m_guidGenFormatPicker, L"NativeGuidGenFormatPicker");
        AutomationProperties::SetName(m_guidGenFormatPicker, ToHString(winforge::core::LocalizedText{
            L"GUID format", L"GUID 格式" }.Pick(m_language)));
        for (auto const& label : {
            L"D — 32 digits + hyphens",
            L"N — 32 digits, no hyphens",
            L"B — {braces}",
            L"P — (parentheses)",
            L"X — hex object" })
        {
            ComboBoxItem item;
            item.Content(box_value(label));
            m_guidGenFormatPicker.Items().Append(item);
        }
        m_guidGenFormatPicker.SelectedIndex(m_guidGenFormatIndex);
        m_guidGenFormatPicker.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_guidGenRendering) return;
            m_guidGenFormatIndex = sender.as<ComboBox>().SelectedIndex();
            GenerateGuidValue();
        });
        guidOptions.Children().Append(m_guidGenFormatPicker);

        m_guidGenUpperSwitch = ToggleSwitch();
        m_guidGenUpperSwitch.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"UPPERCASE", L"大階" }.Pick(m_language))));
        m_guidGenUpperSwitch.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
        m_guidGenUpperSwitch.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
        m_guidGenUpperSwitch.IsOn(m_guidGenUpper);
        AutomationProperties::SetAutomationId(m_guidGenUpperSwitch, L"NativeGuidGenUpperSwitch");
        AutomationProperties::SetName(m_guidGenUpperSwitch, ToHString(winforge::core::LocalizedText{
            L"Uppercase GUID output", L"大階 GUID 輸出" }.Pick(m_language)));
        m_guidGenUpperSwitch.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_guidGenRendering) return;
            m_guidGenUpper = sender.as<ToggleSwitch>().IsOn();
            GenerateGuidValue();
        });
        guidOptions.Children().Append(m_guidGenUpperSwitch);
        guidContent.Children().Append(guidOptions);

        m_guidGenGuidBox = TextBox();
        m_guidGenGuidBox.IsReadOnly(true);
        m_guidGenGuidBox.IsSpellCheckEnabled(false);
        m_guidGenGuidBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_guidGenGuidBox.Text(ToHString(m_guidGenGuidValue));
        AutomationProperties::SetAutomationId(m_guidGenGuidBox, L"NativeGuidGenGuidOutput");
        AutomationProperties::SetName(m_guidGenGuidBox, ToHString(winforge::core::LocalizedText{
            L"Generated GUID", L"已產生 GUID" }.Pick(m_language)));
        guidContent.Children().Append(m_guidGenGuidBox);

        StackPanel guidActions;
        guidActions.Orientation(Orientation::Horizontal);
        guidActions.Spacing(8);

        Button guidGenerate;
        guidGenerate.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Generate", L"產生" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(guidGenerate, L"NativeGuidGenGenerateGuid");
        guidGenerate.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { GenerateGuidValue(); });
        guidActions.Children().Append(guidGenerate);

        Button guidCopy;
        guidCopy.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(guidCopy, L"NativeGuidGenCopyGuid");
        guidCopy.Click([this, copyText](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            copyText(m_guidGenGuidValue);
        });
        guidActions.Children().Append(guidCopy);
        guidContent.Children().Append(guidActions);

        guidCard.Child(guidContent);
        page.Children().Append(guidCard);

        Border bulkCard = makeCard();
        StackPanel bulkContent;
        bulkContent.Spacing(12);
        bulkContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Bulk generate", L"批量產生" }.Pick(m_language),
            15,
            true));

        StackPanel bulkActions;
        bulkActions.Orientation(Orientation::Horizontal);
        bulkActions.Spacing(10);
        bulkActions.VerticalAlignment(VerticalAlignment::Center);
        auto countLabel = CreateText(winforge::core::LocalizedText{ L"Count (1–1000)", L"數量（1–1000）" }.Pick(m_language));
        countLabel.VerticalAlignment(VerticalAlignment::Center);
        bulkActions.Children().Append(countLabel);

        m_guidGenCountBox = NumberBox();
        m_guidGenCountBox.Value(m_guidGenBulkCount);
        m_guidGenCountBox.Minimum(1);
        m_guidGenCountBox.Maximum(1000);
        m_guidGenCountBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_guidGenCountBox.MinWidth(150);
        AutomationProperties::SetAutomationId(m_guidGenCountBox, L"NativeGuidGenBulkCount");
        AutomationProperties::SetName(m_guidGenCountBox, ToHString(winforge::core::LocalizedText{
            L"Bulk GUID count", L"批量 GUID 數量" }.Pick(m_language)));
        bulkActions.Children().Append(m_guidGenCountBox);

        Button bulkGenerate;
        bulkGenerate.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Generate", L"產生" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(bulkGenerate, L"NativeGuidGenGenerateBulk");
        bulkGenerate.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            GenerateBulkGuidValues();
        });
        bulkActions.Children().Append(bulkGenerate);

        Button bulkCopy;
        bulkCopy.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy all", L"全部複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(bulkCopy, L"NativeGuidGenCopyBulk");
        bulkCopy.Click([this, copyText](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            copyText(m_guidGenBulkValue);
        });
        bulkActions.Children().Append(bulkCopy);
        bulkContent.Children().Append(bulkActions);

        m_guidGenBulkBox = TextBox();
        m_guidGenBulkBox.IsReadOnly(true);
        m_guidGenBulkBox.AcceptsReturn(true);
        m_guidGenBulkBox.TextWrapping(TextWrapping::NoWrap);
        m_guidGenBulkBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_guidGenBulkBox.Height(180);
        m_guidGenBulkBox.Text(ToHString(m_guidGenBulkValue));
        AutomationProperties::SetAutomationId(m_guidGenBulkBox, L"NativeGuidGenBulkOutput");
        AutomationProperties::SetName(m_guidGenBulkBox, ToHString(winforge::core::LocalizedText{
            L"Bulk GUID output", L"批量 GUID 輸出" }.Pick(m_language)));
        bulkContent.Children().Append(m_guidGenBulkBox);
        bulkCard.Child(bulkContent);
        page.Children().Append(bulkCard);

        Border otherCard = makeCard();
        StackPanel otherContent;
        otherContent.Spacing(12);
        otherContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"ULID & nano-ID", L"ULID 同 nano-ID" }.Pick(m_language),
            15,
            true));

        m_guidGenUlidBox = TextBox();
        m_guidGenUlidBox.IsReadOnly(true);
        m_guidGenUlidBox.IsSpellCheckEnabled(false);
        m_guidGenUlidBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_guidGenUlidBox.Text(ToHString(m_guidGenUlidValue));
        AutomationProperties::SetAutomationId(m_guidGenUlidBox, L"NativeGuidGenUlidOutput");
        AutomationProperties::SetName(m_guidGenUlidBox, L"Generated ULID");
        otherContent.Children().Append(m_guidGenUlidBox);

        StackPanel ulidActions;
        ulidActions.Orientation(Orientation::Horizontal);
        ulidActions.Spacing(8);
        Button ulidGenerate;
        ulidGenerate.Content(box_value(ToHString(winforge::core::LocalizedText{ L"New ULID", L"新 ULID" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(ulidGenerate, L"NativeGuidGenGenerateUlid");
        ulidGenerate.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { GenerateUlidValue(); });
        ulidActions.Children().Append(ulidGenerate);
        Button ulidCopy;
        ulidCopy.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(ulidCopy, L"NativeGuidGenCopyUlid");
        ulidCopy.Click([this, copyText](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            copyText(m_guidGenUlidValue);
        });
        ulidActions.Children().Append(ulidCopy);
        otherContent.Children().Append(ulidActions);

        StackPanel nanoLengthRow;
        nanoLengthRow.Orientation(Orientation::Horizontal);
        nanoLengthRow.Spacing(10);
        nanoLengthRow.VerticalAlignment(VerticalAlignment::Center);
        auto nanoLengthLabel = CreateText(winforge::core::LocalizedText{
            L"nano-ID length (4–64)", L"nano-ID 長度（4–64）" }.Pick(m_language));
        nanoLengthLabel.VerticalAlignment(VerticalAlignment::Center);
        nanoLengthRow.Children().Append(nanoLengthLabel);

        m_guidGenNanoLengthBox = NumberBox();
        m_guidGenNanoLengthBox.Value(m_guidGenNanoLength);
        m_guidGenNanoLengthBox.Minimum(4);
        m_guidGenNanoLengthBox.Maximum(64);
        m_guidGenNanoLengthBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_guidGenNanoLengthBox.MinWidth(150);
        AutomationProperties::SetAutomationId(m_guidGenNanoLengthBox, L"NativeGuidGenNanoLength");
        AutomationProperties::SetName(m_guidGenNanoLengthBox, ToHString(winforge::core::LocalizedText{
            L"nano-ID length", L"nano-ID 長度" }.Pick(m_language)));
        nanoLengthRow.Children().Append(m_guidGenNanoLengthBox);
        otherContent.Children().Append(nanoLengthRow);

        m_guidGenNanoBox = TextBox();
        m_guidGenNanoBox.IsReadOnly(true);
        m_guidGenNanoBox.IsSpellCheckEnabled(false);
        m_guidGenNanoBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_guidGenNanoBox.Text(ToHString(m_guidGenNanoValue));
        AutomationProperties::SetAutomationId(m_guidGenNanoBox, L"NativeGuidGenNanoOutput");
        AutomationProperties::SetName(m_guidGenNanoBox, L"Generated nano-ID");
        otherContent.Children().Append(m_guidGenNanoBox);

        StackPanel nanoActions;
        nanoActions.Orientation(Orientation::Horizontal);
        nanoActions.Spacing(8);
        Button nanoGenerate;
        nanoGenerate.Content(box_value(ToHString(winforge::core::LocalizedText{ L"New nano-ID", L"新 nano-ID" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(nanoGenerate, L"NativeGuidGenGenerateNano");
        nanoGenerate.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { GenerateNanoIdValue(); });
        nanoActions.Children().Append(nanoGenerate);
        Button nanoCopy;
        nanoCopy.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(nanoCopy, L"NativeGuidGenCopyNano");
        nanoCopy.Click([this, copyText](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            copyText(m_guidGenNanoValue);
        });
        nanoActions.Children().Append(nanoCopy);
        otherContent.Children().Append(nanoActions);

        otherCard.Child(otherContent);
        page.Children().Append(otherCard);

        Border inspectCard = makeCard();
        StackPanel inspectContent;
        inspectContent.Spacing(12);
        inspectContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"GUID inspector", L"GUID 拆解器" }.Pick(m_language),
            15,
            true));

        m_guidGenInspectInput = TextBox();
        m_guidGenInspectInput.IsSpellCheckEnabled(false);
        m_guidGenInspectInput.FontFamily(Media::FontFamily(L"Consolas"));
        m_guidGenInspectInput.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"Paste a GUID to inspect…", L"貼上一個 GUID 嚟拆解…" }.Pick(m_language)));
        m_guidGenInspectInput.Text(ToHString(m_guidGenInspectValue));
        AutomationProperties::SetAutomationId(m_guidGenInspectInput, L"NativeGuidGenInspectInput");
        AutomationProperties::SetName(m_guidGenInspectInput, ToHString(winforge::core::LocalizedText{
            L"GUID inspector input", L"GUID 拆解器輸入" }.Pick(m_language)));
        m_guidGenInspectInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_guidGenRendering) return;
            m_guidGenInspectValue = ToWide(sender.as<TextBox>().Text());
            RefreshGuidInspector();
        });
        inspectContent.Children().Append(m_guidGenInspectInput);
        inspectContent.Children().Append(CreateText(
            winforge::core::LocalizedText{
                L"16 bytes (RFC 4122 order)", L"16 位元組（RFC 4122 排序）" }.Pick(m_language),
            12));

        m_guidGenInspectHexBox = TextBox();
        m_guidGenInspectHexBox.IsReadOnly(true);
        m_guidGenInspectHexBox.IsSpellCheckEnabled(false);
        m_guidGenInspectHexBox.FontFamily(Media::FontFamily(L"Consolas"));
        AutomationProperties::SetAutomationId(m_guidGenInspectHexBox, L"NativeGuidGenInspectHex");
        AutomationProperties::SetName(m_guidGenInspectHexBox, ToHString(winforge::core::LocalizedText{
            L"GUID bytes in RFC 4122 order", L"RFC 4122 排序 GUID 位元組" }.Pick(m_language)));
        inspectContent.Children().Append(m_guidGenInspectHexBox);

        m_guidGenInspectMeta = CreateText(L"", 13);
        AutomationProperties::SetAutomationId(m_guidGenInspectMeta, L"NativeGuidGenInspectMeta");
        inspectContent.Children().Append(m_guidGenInspectMeta);
        inspectCard.Child(inspectContent);
        page.Children().Append(inspectCard);

        m_guidGenStatus = CreateText(L"", 12.5);
        m_guidGenStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_guidGenStatus, L"NativeGuidGenStatus");
        AutomationProperties::SetLiveSetting(
            m_guidGenStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_guidGenStatus);

        ShowPage(page);
        m_guidGenRendering = false;

        if (m_guidGenGuidValue.empty()) GenerateGuidValue();
        if (m_guidGenUlidValue.empty()) GenerateUlidValue();
        if (m_guidGenNanoValue.empty()) GenerateNanoIdValue();
        RefreshGuidInspector();
        AnnounceGuidGenStatus(
            winforge::core::LocalizedText{ L"Native GUID tools ready.", L"原生 GUID 工具已就緒。" }.Pick(m_language));
    }

    void MainWindow::GenerateGuidValue()
    {
        try
        {
            m_guidGenGuidValue = winforge::core::guidgen::NewGuid(GuidFormatFromIndex(m_guidGenFormatIndex), m_guidGenUpper);
            if (m_guidGenGuidBox)
            {
                m_guidGenGuidBox.Text(ToHString(m_guidGenGuidValue));
                AutomationProperties::SetHelpText(m_guidGenGuidBox, ToHString(m_guidGenGuidValue));
            }
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Generated a GUID.", L"已產生一個 GUID。" }.Pick(m_language));
        }
        catch (...)
        {
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Could not generate a GUID.", L"無法產生 GUID。" }.Pick(m_language),
                true);
        }
    }

    void MainWindow::GenerateBulkGuidValues()
    {
        try
        {
            auto value = m_guidGenCountBox ? m_guidGenCountBox.Value() : static_cast<double>(m_guidGenBulkCount);
            if (std::isnan(value)) value = static_cast<double>(m_guidGenBulkCount);
            m_guidGenBulkCount = std::clamp(static_cast<int32_t>(value), 1, 1000);
            m_guidGenBulkValue = winforge::core::guidgen::BulkGuids(
                m_guidGenBulkCount,
                GuidFormatFromIndex(m_guidGenFormatIndex),
                m_guidGenUpper);
            if (m_guidGenBulkBox)
            {
                m_guidGenBulkBox.Text(ToHString(m_guidGenBulkValue));
                AutomationProperties::SetHelpText(m_guidGenBulkBox, ToHString(m_guidGenBulkValue));
            }
            auto const status = std::to_wstring(m_guidGenBulkCount);
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{
                    L"Generated " + status + L" GUIDs.",
                    L"已產生 " + status + L" 個 GUID。" }.Pick(m_language));
        }
        catch (...)
        {
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Could not bulk-generate GUIDs.", L"無法批量產生 GUID。" }.Pick(m_language),
                true);
        }
    }

    void MainWindow::GenerateUlidValue()
    {
        try
        {
            m_guidGenUlidValue = winforge::core::guidgen::NewUlid();
            if (m_guidGenUlidBox)
            {
                m_guidGenUlidBox.Text(ToHString(m_guidGenUlidValue));
                AutomationProperties::SetHelpText(m_guidGenUlidBox, ToHString(m_guidGenUlidValue));
            }
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Generated a ULID.", L"已產生一個 ULID。" }.Pick(m_language));
        }
        catch (...)
        {
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Could not generate a ULID.", L"無法產生 ULID。" }.Pick(m_language),
                true);
        }
    }

    void MainWindow::GenerateNanoIdValue()
    {
        try
        {
            auto value = m_guidGenNanoLengthBox ? m_guidGenNanoLengthBox.Value() : static_cast<double>(m_guidGenNanoLength);
            if (std::isnan(value)) value = 21;
            m_guidGenNanoLength = std::clamp(static_cast<int32_t>(value), 4, 64);
            m_guidGenNanoValue = winforge::core::guidgen::NewNanoId(m_guidGenNanoLength);
            if (m_guidGenNanoBox)
            {
                m_guidGenNanoBox.Text(ToHString(m_guidGenNanoValue));
                AutomationProperties::SetHelpText(m_guidGenNanoBox, ToHString(m_guidGenNanoValue));
            }
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Generated a nano-ID.", L"已產生一個 nano-ID。" }.Pick(m_language));
        }
        catch (...)
        {
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"Could not generate a nano-ID.", L"無法產生 nano-ID。" }.Pick(m_language),
                true);
        }
    }

    void MainWindow::RefreshGuidInspector()
    {
        if (!m_guidGenInspectHexBox || !m_guidGenInspectMeta)
        {
            return;
        }

        if (m_guidGenInspectValue.empty())
        {
            m_guidGenInspectHexBox.Text(L"");
            m_guidGenInspectMeta.Text(L"");
            return;
        }

        try
        {
            auto const info = winforge::core::guidgen::Inspect(m_guidGenInspectValue);
            m_guidGenInspectHexBox.Text(ToHString(info.hex));
            AutomationProperties::SetHelpText(m_guidGenInspectHexBox, ToHString(info.hex));
            auto const version = std::to_wstring(info.version);
            auto const meta = winforge::core::LocalizedText{
                L"Version: " + version + L"    Variant: " + info.variant,
                L"版本：" + version + L"    變體：" + info.variant }.Pick(m_language);
            m_guidGenInspectMeta.Text(ToHString(meta));
            AutomationProperties::SetName(m_guidGenInspectMeta, ToHString(meta));
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"GUID parsed.", L"GUID 已解析。" }.Pick(m_language));
        }
        catch (...)
        {
            m_guidGenInspectHexBox.Text(L"");
            m_guidGenInspectMeta.Text(L"");
            AnnounceGuidGenStatus(
                winforge::core::LocalizedText{ L"That is not a valid GUID.", L"呢個唔係有效嘅 GUID。" }.Pick(m_language),
                true);
        }
    }

    void MainWindow::AnnounceGuidGenStatus(std::wstring_view message, bool warning)
    {
        if (!m_guidGenStatus) return;

        m_guidGenStatus.Text(ToHString(message));
        m_guidGenStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_guidGenStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_guidGenStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_guidGenStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_guidGenStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility notification failure must not break identifier generation.
        }
    }

    void MainWindow::RenderPassGen()
    {
        m_passGenRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"Password Generator", L"密碼產生器" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Create strong random passwords or memorable passphrases. All randomness comes from the OS cryptographic RNG and nothing leaves this PC.",
                L"整強勁嘅隨機密碼或者易記嘅通行短語。所有隨機數都係用系統嘅加密級隨機產生器，完全唔會離開部電腦。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativePassGenPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native password and passphrase generation", L"全原生密碼同通行短語產生" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"C++ uses BCrypt cryptographic randomness and rejection-sampled ranges. Passwords stay local; the clipboard changes only after an explicit Copy action.",
            L"C++ 使用 BCrypt 加密級隨機同 rejection-sampled 範圍。密碼會留喺本機；只有明確撳 Copy 先會改剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativePassGenImplementationStatus");
        page.Children().Append(nativeStatus);

        auto makeCard = []()
        {
            Border card;
            card.Padding(Thickness{ 18, 16, 18, 18 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            return card;
        };

        Border modeCard = makeCard();
        StackPanel modeContent;
        modeContent.Spacing(8);
        auto const modeLabel = CreateText(
            winforge::core::LocalizedText{ L"Mode", L"模式" }.Pick(m_language), 14, true);
        modeContent.Children().Append(modeLabel);
        m_passGenMode = ComboBox();
        m_passGenMode.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Password", L"密碼" }.Pick(m_language))));
        m_passGenMode.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Passphrase", L"通行短語" }.Pick(m_language))));
        m_passGenMode.SelectedIndex(m_passGenPassphrase ? 1 : 0);
        m_passGenMode.MinWidth(220);
        m_passGenMode.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_passGenMode, L"NativePassGenMode");
        AutomationProperties::SetName(m_passGenMode, ToHString(winforge::core::LocalizedText{
            L"Generator mode", L"產生器模式" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_passGenMode, modeLabel);
        m_passGenMode.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_passGenRendering) return;
            m_passGenPassphrase = sender.as<ComboBox>().SelectedIndex() == 1;
            m_passGenOutputValue.clear();
            m_passGenStatusValue.clear();
            RenderPassGen();
        });
        modeContent.Children().Append(m_passGenMode);
        modeCard.Child(modeContent);
        page.Children().Append(modeCard);

        if (!m_passGenPassphrase)
        {
            Border passwordCard = makeCard();
            StackPanel passwordContent;
            passwordContent.Spacing(10);
            passwordContent.Children().Append(CreateText(
                winforge::core::LocalizedText{ L"Password options", L"密碼選項" }.Pick(m_language), 15, true));

            auto const lengthLabel = CreateText(
                winforge::core::LocalizedText{ L"Length (4–128)", L"長度（4–128）" }.Pick(m_language));
            passwordContent.Children().Append(lengthLabel);
            m_passGenLengthBox = NumberBox();
            m_passGenLengthBox.Minimum(4);
            m_passGenLengthBox.Maximum(128);
            m_passGenLengthBox.Value(m_passGenLength);
            m_passGenLengthBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
            m_passGenLengthBox.MinWidth(180);
            m_passGenLengthBox.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(m_passGenLengthBox, L"NativePassGenLength");
            AutomationProperties::SetName(m_passGenLengthBox, ToHString(winforge::core::LocalizedText{
                L"Password length", L"密碼長度" }.Pick(m_language)));
            AutomationProperties::SetLabeledBy(m_passGenLengthBox, lengthLabel);
            m_passGenLengthBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
            {
                if (m_passGenRendering) return;
                auto value = sender.Value();
                if (std::isnan(value)) value = 16;
                m_passGenLength = std::clamp(static_cast<int32_t>(value), 4, 128);
                RegeneratePassGen();
            });
            passwordContent.Children().Append(m_passGenLengthBox);

            auto makeCheck = [this, &passwordContent](
                CheckBox& box,
                bool value,
                std::wstring_view label,
                std::wstring_view accessibleName,
                std::wstring_view automationId)
            {
                box = CheckBox();
                box.Content(box_value(ToHString(label)));
                box.IsChecked(value);
                AutomationProperties::SetAutomationId(box, ToHString(automationId));
                AutomationProperties::SetName(box, ToHString(accessibleName));
                passwordContent.Children().Append(box);
            };
            makeCheck(
                m_passGenLower,
                m_passGenLowerEnabled,
                winforge::core::LocalizedText{ L"Lowercase (a–z)", L"細楷字母（a–z）" }.Pick(m_language),
                winforge::core::LocalizedText{ L"Include lowercase letters", L"包括細楷字母" }.Pick(m_language),
                L"NativePassGenLower");
            makeCheck(
                m_passGenUpper,
                m_passGenUpperEnabled,
                winforge::core::LocalizedText{ L"UPPERCASE (A–Z)", L"大楷字母（A–Z）" }.Pick(m_language),
                winforge::core::LocalizedText{ L"Include uppercase letters", L"包括大楷字母" }.Pick(m_language),
                L"NativePassGenUpper");
            makeCheck(
                m_passGenDigits,
                m_passGenDigitsEnabled,
                winforge::core::LocalizedText{ L"Digits (0–9)", L"數字（0–9）" }.Pick(m_language),
                winforge::core::LocalizedText{ L"Include digits", L"包括數字" }.Pick(m_language),
                L"NativePassGenDigits");
            makeCheck(
                m_passGenSymbols,
                m_passGenSymbolsEnabled,
                winforge::core::LocalizedText{ L"Symbols (!@#…)", L"符號（!@#…）" }.Pick(m_language),
                winforge::core::LocalizedText{ L"Include symbols", L"包括符號" }.Pick(m_language),
                L"NativePassGenSymbols");

            auto updatePasswordOptions = [this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                if (m_passGenRendering) return;
                auto const valueOf = [](CheckBox const& box)
                {
                    auto const value = box.IsChecked();
                    return value && value.Value();
                };
                m_passGenLowerEnabled = valueOf(m_passGenLower);
                m_passGenUpperEnabled = valueOf(m_passGenUpper);
                m_passGenDigitsEnabled = valueOf(m_passGenDigits);
                m_passGenSymbolsEnabled = valueOf(m_passGenSymbols);
                RegeneratePassGen();
            };
            for (auto box : { m_passGenLower, m_passGenUpper, m_passGenDigits, m_passGenSymbols })
            {
                box.Checked(updatePasswordOptions);
                box.Unchecked(updatePasswordOptions);
            }

            m_passGenAvoidAmbiguous = ToggleSwitch();
            m_passGenAvoidAmbiguous.Header(box_value(ToHString(winforge::core::LocalizedText{
                L"Avoid ambiguous characters (O 0 I l 1 |)", L"避開易撈亂嘅字元（O 0 I l 1 |）" }.Pick(m_language))));
            m_passGenAvoidAmbiguous.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
            m_passGenAvoidAmbiguous.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
            m_passGenAvoidAmbiguous.IsOn(m_passGenAvoidAmbiguousEnabled);
            AutomationProperties::SetAutomationId(m_passGenAvoidAmbiguous, L"NativePassGenAvoidAmbiguous");
            AutomationProperties::SetName(m_passGenAvoidAmbiguous, ToHString(winforge::core::LocalizedText{
                L"Avoid ambiguous password characters", L"避開易撈亂密碼字元" }.Pick(m_language)));
            m_passGenAvoidAmbiguous.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_passGenRendering) return;
                m_passGenAvoidAmbiguousEnabled = sender.as<ToggleSwitch>().IsOn();
                RegeneratePassGen();
            });
            passwordContent.Children().Append(m_passGenAvoidAmbiguous);

            m_passGenNoRepeats = ToggleSwitch();
            m_passGenNoRepeats.Header(box_value(ToHString(winforge::core::LocalizedText{
                L"No repeated characters", L"唔好有重複字元" }.Pick(m_language))));
            m_passGenNoRepeats.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
            m_passGenNoRepeats.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
            m_passGenNoRepeats.IsOn(m_passGenNoRepeatsEnabled);
            AutomationProperties::SetAutomationId(m_passGenNoRepeats, L"NativePassGenNoRepeats");
            AutomationProperties::SetName(m_passGenNoRepeats, ToHString(winforge::core::LocalizedText{
                L"Do not repeat password characters", L"唔好重複密碼字元" }.Pick(m_language)));
            m_passGenNoRepeats.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_passGenRendering) return;
                m_passGenNoRepeatsEnabled = sender.as<ToggleSwitch>().IsOn();
                RegeneratePassGen();
            });
            passwordContent.Children().Append(m_passGenNoRepeats);

            passwordCard.Child(passwordContent);
            page.Children().Append(passwordCard);
        }
        else
        {
            Border passphraseCard = makeCard();
            StackPanel passphraseContent;
            passphraseContent.Spacing(10);
            passphraseContent.Children().Append(CreateText(
                winforge::core::LocalizedText{ L"Passphrase options", L"通行短語選項" }.Pick(m_language), 15, true));
            passphraseContent.Children().Append(CreateText(
                winforge::core::LocalizedText{
                    L"Dictionary: " + std::to_wstring(winforge::core::passgen::DictionarySize()) + L" common English words.",
                    L"字典：" + std::to_wstring(winforge::core::passgen::DictionarySize()) + L" 個常用英文字。" }.Pick(m_language),
                12));

            auto const wordCountLabel = CreateText(
                winforge::core::LocalizedText{ L"Word count (3–10)", L"字數（3–10）" }.Pick(m_language));
            passphraseContent.Children().Append(wordCountLabel);
            m_passGenWordCountBox = NumberBox();
            m_passGenWordCountBox.Minimum(3);
            m_passGenWordCountBox.Maximum(10);
            m_passGenWordCountBox.Value(m_passGenWordCount);
            m_passGenWordCountBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
            m_passGenWordCountBox.MinWidth(180);
            m_passGenWordCountBox.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(m_passGenWordCountBox, L"NativePassGenWordCount");
            AutomationProperties::SetName(m_passGenWordCountBox, ToHString(winforge::core::LocalizedText{
                L"Passphrase word count", L"通行短語字數" }.Pick(m_language)));
            AutomationProperties::SetLabeledBy(m_passGenWordCountBox, wordCountLabel);
            m_passGenWordCountBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
            {
                if (m_passGenRendering) return;
                auto value = sender.Value();
                if (std::isnan(value)) value = 4;
                m_passGenWordCount = std::clamp(static_cast<int32_t>(value), 3, 10);
                RegeneratePassGen();
            });
            passphraseContent.Children().Append(m_passGenWordCountBox);

            auto const separatorLabel = CreateText(
                winforge::core::LocalizedText{ L"Separator", L"分隔符" }.Pick(m_language));
            passphraseContent.Children().Append(separatorLabel);
            m_passGenSeparator = ComboBox();
            for (auto const& item : std::array<winforge::core::LocalizedText, 4>{
                winforge::core::LocalizedText{ L"Hyphen  -", L"連字號  -" },
                winforge::core::LocalizedText{ L"Dot  .", L"句號  ." },
                winforge::core::LocalizedText{ L"Space", L"空格" },
                winforge::core::LocalizedText{ L"Underscore  _", L"底線  _" } })
            {
                m_passGenSeparator.Items().Append(box_value(ToHString(item.Pick(m_language))));
            }
            m_passGenSeparator.SelectedIndex(std::clamp(m_passGenSeparatorIndex, 0, 3));
            m_passGenSeparator.MinWidth(220);
            m_passGenSeparator.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(m_passGenSeparator, L"NativePassGenSeparator");
            AutomationProperties::SetName(m_passGenSeparator, ToHString(winforge::core::LocalizedText{
                L"Passphrase separator", L"通行短語分隔符" }.Pick(m_language)));
            AutomationProperties::SetLabeledBy(m_passGenSeparator, separatorLabel);
            m_passGenSeparator.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
            {
                if (m_passGenRendering) return;
                m_passGenSeparatorIndex = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 3);
                RegeneratePassGen();
            });
            passphraseContent.Children().Append(m_passGenSeparator);

            m_passGenCapitalize = ToggleSwitch();
            m_passGenCapitalize.Header(box_value(ToHString(winforge::core::LocalizedText{
                L"Capitalize each word", L"每個字首字母大楷" }.Pick(m_language))));
            m_passGenCapitalize.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
            m_passGenCapitalize.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
            m_passGenCapitalize.IsOn(m_passGenCapitalizeEnabled);
            AutomationProperties::SetAutomationId(m_passGenCapitalize, L"NativePassGenCapitalize");
            AutomationProperties::SetName(m_passGenCapitalize, ToHString(winforge::core::LocalizedText{
                L"Capitalize every passphrase word", L"每個通行短語字首字母大楷" }.Pick(m_language)));
            m_passGenCapitalize.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_passGenRendering) return;
                m_passGenCapitalizeEnabled = sender.as<ToggleSwitch>().IsOn();
                RegeneratePassGen();
            });
            passphraseContent.Children().Append(m_passGenCapitalize);

            m_passGenAppendDigit = ToggleSwitch();
            m_passGenAppendDigit.Header(box_value(ToHString(winforge::core::LocalizedText{
                L"Append a random digit", L"尾加一個隨機數字" }.Pick(m_language))));
            m_passGenAppendDigit.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
            m_passGenAppendDigit.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
            m_passGenAppendDigit.IsOn(m_passGenAppendDigitEnabled);
            AutomationProperties::SetAutomationId(m_passGenAppendDigit, L"NativePassGenAppendDigit");
            AutomationProperties::SetName(m_passGenAppendDigit, ToHString(winforge::core::LocalizedText{
                L"Append a random passphrase digit", L"尾加隨機通行短語數字" }.Pick(m_language)));
            m_passGenAppendDigit.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_passGenRendering) return;
                m_passGenAppendDigitEnabled = sender.as<ToggleSwitch>().IsOn();
                RegeneratePassGen();
            });
            passphraseContent.Children().Append(m_passGenAppendDigit);

            passphraseCard.Child(passphraseContent);
            page.Children().Append(passphraseCard);
        }

        Border resultCard = makeCard();
        StackPanel resultContent;
        resultContent.Spacing(10);
        auto const countLabel = CreateText(
            winforge::core::LocalizedText{ L"How many (1–100)", L"產生幾多個（1–100）" }.Pick(m_language));
        resultContent.Children().Append(countLabel);
        m_passGenCountBox = NumberBox();
        m_passGenCountBox.Minimum(1);
        m_passGenCountBox.Maximum(100);
        m_passGenCountBox.Value(m_passGenCount);
        m_passGenCountBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_passGenCountBox.MinWidth(180);
        m_passGenCountBox.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_passGenCountBox, L"NativePassGenCount");
        AutomationProperties::SetName(m_passGenCountBox, ToHString(winforge::core::LocalizedText{
            L"Number of generated values", L"產生數量" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_passGenCountBox, countLabel);
        resultContent.Children().Append(m_passGenCountBox);

        Button generate;
        generate.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Generate", L"產生" }.Pick(m_language))));
        generate.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(generate, L"NativePassGenGenerate");
        AutomationProperties::SetName(generate, ToHString(winforge::core::LocalizedText{
            L"Generate secure password or passphrase", L"產生安全密碼或者通行短語" }.Pick(m_language)));
        generate.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { RegeneratePassGen(); });
        resultContent.Children().Append(generate);

        Button copy;
        copy.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        copy.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(copy, L"NativePassGenCopy");
        AutomationProperties::SetName(copy, ToHString(winforge::core::LocalizedText{
            L"Copy generated password or passphrase", L"複製產生咗嘅密碼或者通行短語" }.Pick(m_language)));
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { CopyPassGenOutput(); });
        resultContent.Children().Append(copy);

        m_passGenEntropy = CreateText(L"", 12);
        m_passGenEntropy.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_passGenEntropy, L"NativePassGenEntropy");
        resultContent.Children().Append(m_passGenEntropy);
        m_passGenEntropyBar = ProgressBar();
        m_passGenEntropyBar.Minimum(0);
        m_passGenEntropyBar.Maximum(128);
        AutomationProperties::SetAutomationId(m_passGenEntropyBar, L"NativePassGenEntropyBar");
        AutomationProperties::SetName(m_passGenEntropyBar, ToHString(winforge::core::LocalizedText{
            L"Estimated password entropy", L"估計密碼熵值" }.Pick(m_language)));
        resultContent.Children().Append(m_passGenEntropyBar);

        m_passGenOutput = TextBox();
        m_passGenOutput.IsReadOnly(true);
        m_passGenOutput.IsSpellCheckEnabled(false);
        m_passGenOutput.AcceptsReturn(true);
        m_passGenOutput.TextWrapping(TextWrapping::Wrap);
        m_passGenOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_passGenOutput.MinHeight(120);
        m_passGenOutput.Text(ToHString(m_passGenOutputValue));
        AutomationProperties::SetAutomationId(m_passGenOutput, L"NativePassGenOutput");
        AutomationProperties::SetName(m_passGenOutput, ToHString(winforge::core::LocalizedText{
            L"Generated password or passphrase", L"產生咗嘅密碼或者通行短語" }.Pick(m_language)));
        resultContent.Children().Append(m_passGenOutput);

        m_passGenStatus = CreateText(m_passGenStatusValue, 12);
        m_passGenStatus.TextWrapping(TextWrapping::Wrap);
        m_passGenStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_passGenStatus, L"NativePassGenStatus");
        AutomationProperties::SetLiveSetting(
            m_passGenStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        resultContent.Children().Append(m_passGenStatus);
        resultCard.Child(resultContent);
        page.Children().Append(resultCard);

        ShowPage(page);
        m_passGenRendering = false;
        UpdatePassGenEntropy();
        if (m_passGenOutputValue.empty())
        {
            RegeneratePassGen();
        }
    }

    void MainWindow::RegeneratePassGen()
    {
        if (m_passGenCountBox)
        {
            auto value = m_passGenCountBox.Value();
            if (std::isnan(value)) value = 1;
            m_passGenCount = std::clamp(static_cast<int32_t>(value), 1, 100);
        }

        try
        {
            winforge::core::passgen::SystemRandomSource source;
            if (m_passGenPassphrase)
            {
                static constexpr std::array<std::wstring_view, 4> Separators{ L"-", L".", L" ", L"_" };
                auto const separatorIndex = std::clamp(m_passGenSeparatorIndex, 0, 3);
                winforge::core::passgen::PassphraseOptions options;
                options.word_count = std::clamp(m_passGenWordCount, 3, 10);
                options.separator = std::wstring(Separators[separatorIndex]);
                options.capitalize = m_passGenCapitalizeEnabled;
                options.append_digit = m_passGenAppendDigitEnabled;
                m_passGenOutputValue = winforge::core::passgen::JoinLines(
                    winforge::core::passgen::GeneratePassphraseBatch(options, m_passGenCount, source));
            }
            else
            {
                winforge::core::passgen::PasswordOptions options;
                options.length = std::clamp(m_passGenLength, 4, 128);
                options.lower = m_passGenLowerEnabled;
                options.upper = m_passGenUpperEnabled;
                options.digits = m_passGenDigitsEnabled;
                options.symbols = m_passGenSymbolsEnabled;
                options.avoid_ambiguous = m_passGenAvoidAmbiguousEnabled;
                options.no_repeats = m_passGenNoRepeatsEnabled;
                m_passGenOutputValue = winforge::core::passgen::JoinLines(
                    winforge::core::passgen::GeneratePasswordBatch(options, m_passGenCount, source));
            }

            if (m_passGenOutput)
            {
                m_passGenOutput.Text(ToHString(m_passGenOutputValue));
            }
            AnnouncePassGenStatus(winforge::core::LocalizedText{
                L"Generated " + std::to_wstring(m_passGenCount) + L" · secure RNG.",
                L"已產生 " + std::to_wstring(m_passGenCount) + L" 個 · 加密級隨機。" }.Pick(m_language));
        }
        catch (winforge::core::passgen::GenerationError const& error)
        {
            m_passGenOutputValue.clear();
            if (m_passGenOutput)
            {
                m_passGenOutput.Text(L"");
            }
            auto const explanation = [&]() -> std::wstring
            {
                switch (error.Code())
                {
                case winforge::core::passgen::ErrorCode::NoCharacterSets:
                    return winforge::core::LocalizedText{ L"select at least one character set.", L"至少要揀一種字元。" }.Pick(m_language);
                case winforge::core::passgen::ErrorCode::LengthTooShort:
                    return winforge::core::LocalizedText{ L"length is too short to fit every selected set.", L"長度太短，容納唔到所有揀咗嘅字元類別。" }.Pick(m_language);
                case winforge::core::passgen::ErrorCode::NoRepeatsPoolTooSmall:
                    return winforge::core::LocalizedText{ L"no-repeats needs a shorter length or a bigger pool.", L"唔重複模式需要短啲嘅長度或者更大嘅字元池。" }.Pick(m_language);
                default:
                    return winforge::core::LocalizedText{ L"the secure generator is unavailable.", L"安全產生器而家用唔到。" }.Pick(m_language);
                }
            }();
            AnnouncePassGenStatus(winforge::core::LocalizedText{
                L"Can't generate: " + explanation,
                L"無法產生：" + explanation }.Pick(m_language), true);
        }
        catch (...)
        {
            m_passGenOutputValue.clear();
            if (m_passGenOutput)
            {
                m_passGenOutput.Text(L"");
            }
            AnnouncePassGenStatus(winforge::core::LocalizedText{
                L"Can't generate: the secure generator is unavailable.",
                L"無法產生：安全產生器而家用唔到。" }.Pick(m_language), true);
        }

        UpdatePassGenEntropy();
    }

    void MainWindow::UpdatePassGenEntropy()
    {
        double bits{};
        if (m_passGenPassphrase)
        {
            bits = winforge::core::passgen::PassphraseEntropyBits(
                std::clamp(m_passGenWordCount, 3, 10),
                static_cast<int>(winforge::core::passgen::DictionarySize()),
                m_passGenAppendDigitEnabled);
        }
        else
        {
            winforge::core::passgen::PasswordOptions options;
            options.lower = m_passGenLowerEnabled;
            options.upper = m_passGenUpperEnabled;
            options.digits = m_passGenDigitsEnabled;
            options.symbols = m_passGenSymbolsEnabled;
            options.avoid_ambiguous = m_passGenAvoidAmbiguousEnabled;
            bits = winforge::core::passgen::PasswordEntropyBits(
                std::clamp(m_passGenLength, 4, 128),
                static_cast<int>(winforge::core::passgen::BuildPool(options).size()));
        }

        auto const rounded = static_cast<int>(std::lround(bits));
        auto const label = bits < 40.0
            ? winforge::core::LocalizedText{ L"Weak", L"弱" }.Pick(m_language)
            : bits < 60.0
                ? winforge::core::LocalizedText{ L"Fair", L"一般" }.Pick(m_language)
                : bits < 90.0
                    ? winforge::core::LocalizedText{ L"Strong", L"強" }.Pick(m_language)
                    : winforge::core::LocalizedText{ L"Excellent", L"極強" }.Pick(m_language);
        auto const text = winforge::core::LocalizedText{
            L"Entropy: ~" + std::to_wstring(rounded) + L" bits · " + label,
            L"熵值：約 " + std::to_wstring(rounded) + L" 位元 · " + label }.Pick(m_language);
        if (m_passGenEntropy)
        {
            m_passGenEntropy.Text(ToHString(text));
            AutomationProperties::SetName(m_passGenEntropy, ToHString(text));
        }
        if (m_passGenEntropyBar)
        {
            m_passGenEntropyBar.Value((std::min)(128.0, bits));
        }
    }

    void MainWindow::CopyPassGenOutput()
    {
        if (m_passGenOutputValue.empty())
        {
            AnnouncePassGenStatus(winforge::core::LocalizedText{
                L"Nothing to copy yet.", L"未有嘢可以複製。" }.Pick(m_language), true);
            return;
        }

        try
        {
            Windows::ApplicationModel::DataTransfer::DataPackage package;
            package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
            package.SetText(ToHString(m_passGenOutputValue));
            Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
            AnnouncePassGenStatus(winforge::core::LocalizedText{
                L"Copied to clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
        }
        catch (...)
        {
            AnnouncePassGenStatus(winforge::core::LocalizedText{
                L"Copy failed: clipboard is unavailable.", L"複製失敗：剪貼簿用唔到。" }.Pick(m_language), true);
        }
    }

    void MainWindow::AnnouncePassGenStatus(std::wstring_view message, bool warning)
    {
        m_passGenStatusValue.assign(message);
        if (!m_passGenStatus) return;

        m_passGenStatus.Text(ToHString(message));
        m_passGenStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_passGenStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_passGenStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_passGenStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_passGenStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // A live-region failure must never interrupt local password generation.
        }
    }

    void MainWindow::RenderPasswordStrength()
    {
        m_passwordStrengthRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"Password Strength", L"密碼強度" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Type a password to see how strong it is — character variety, entropy, estimated crack time and a checklist of good habits.",
                L"打個密碼入嚟，睇下佢有幾穩陣 — 字元種類、熵值、估計破解時間，仲有一張良好習慣清單。" }.Pick(m_language));
        page.MaxWidth(760);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativePasswordStrengthPage");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native, local password analysis", L"全原生、本機密碼分析" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"The C++ analyzer works only in memory. Your password is never saved, logged, sent, or copied to the clipboard.",
            L"C++ 分析器只會喺記憶體度運行。你嘅密碼唔會儲存、記錄、傳送，亦唔會複製去剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativePasswordStrengthImplementationStatus");
        page.Children().Append(nativeStatus);

        auto makeCard = []()
        {
            Border card;
            card.Padding(Thickness{ 18, 16, 18, 18 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            return card;
        };

        Border entryCard = makeCard();
        StackPanel entryContent;
        entryContent.Spacing(10);
        auto const entryLabel = CreateText(
            winforge::core::LocalizedText{ L"Password to test", L"要測試嘅密碼" }.Pick(m_language), 15, true);
        entryContent.Children().Append(entryLabel);

        auto storeSecret = [this](std::wstring value)
        {
            if (!m_passwordStrengthValue.empty())
            {
                SecureZeroMemory(
                    m_passwordStrengthValue.data(),
                    m_passwordStrengthValue.size() * sizeof(wchar_t));
            }
            m_passwordStrengthValue = std::move(value);
        };

        m_passwordStrengthHidden = PasswordBox();
        m_passwordStrengthHidden.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"Type a password locally", L"喺本機輸入密碼" }.Pick(m_language)));
        m_passwordStrengthHidden.Password(m_passwordStrengthRevealed ? L"" : ToHString(m_passwordStrengthValue));
        m_passwordStrengthHidden.Visibility(m_passwordStrengthRevealed ? Visibility::Collapsed : Visibility::Visible);
        AutomationProperties::SetAutomationId(m_passwordStrengthHidden, L"NativePasswordStrengthHiddenInput");
        AutomationProperties::SetName(m_passwordStrengthHidden, ToHString(winforge::core::LocalizedText{
            L"Masked password to test", L"隱藏嘅要測試密碼" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_passwordStrengthHidden, entryLabel);
        m_passwordStrengthHidden.PasswordChanged([this, storeSecret](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_passwordStrengthRendering) return;
            auto const value = ToWide(sender.as<PasswordBox>().Password());
            // Collapsing this inactive box clears its visual value. WinUI may
            // deliver that programmatic event after the render guard has been
            // restored; never let it erase the locally held secret while the
            // revealed TextBox is the active editor.
            if (m_passwordStrengthRevealed && value.empty() && !m_passwordStrengthValue.empty()) return;
            storeSecret(value);
            RefreshPasswordStrength();
        });
        entryContent.Children().Append(m_passwordStrengthHidden);

        m_passwordStrengthShown = TextBox();
        m_passwordStrengthShown.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"Password shown locally", L"喺本機顯示密碼" }.Pick(m_language)));
        m_passwordStrengthShown.IsSpellCheckEnabled(false);
        m_passwordStrengthShown.Text(m_passwordStrengthRevealed ? ToHString(m_passwordStrengthValue) : L"");
        m_passwordStrengthShown.Visibility(m_passwordStrengthRevealed ? Visibility::Visible : Visibility::Collapsed);
        AutomationProperties::SetAutomationId(m_passwordStrengthShown, L"NativePasswordStrengthShownInput");
        AutomationProperties::SetName(m_passwordStrengthShown, ToHString(winforge::core::LocalizedText{
            L"Shown password to test", L"顯示咗嘅要測試密碼" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_passwordStrengthShown, entryLabel);
        m_passwordStrengthShown.TextChanged([this, storeSecret](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_passwordStrengthRendering) return;
            auto const value = ToWide(sender.as<TextBox>().Text());
            // Symmetric to the PasswordBox guard above: the hidden plain-text
            // editor is deliberately cleared, but a delayed programmatic
            // TextChanged notification must not replace the secret in memory.
            if (!m_passwordStrengthRevealed && value.empty() && !m_passwordStrengthValue.empty()) return;
            storeSecret(value);
            RefreshPasswordStrength();
        });
        entryContent.Children().Append(m_passwordStrengthShown);

        m_passwordStrengthReveal = ToggleSwitch();
        m_passwordStrengthReveal.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Show the password", L"顯示密碼" }.Pick(m_language))));
        m_passwordStrengthReveal.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
        m_passwordStrengthReveal.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
        m_passwordStrengthReveal.IsOn(m_passwordStrengthRevealed);
        AutomationProperties::SetAutomationId(m_passwordStrengthReveal, L"NativePasswordStrengthReveal");
        AutomationProperties::SetName(m_passwordStrengthReveal, ToHString(winforge::core::LocalizedText{
            L"Show the password", L"顯示密碼" }.Pick(m_language)));
        m_passwordStrengthReveal.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_passwordStrengthRendering) return;
            SetPasswordStrengthReveal(sender.as<ToggleSwitch>().IsOn());
        });
        entryContent.Children().Append(m_passwordStrengthReveal);
        entryCard.Child(entryContent);
        page.Children().Append(entryCard);

        Border strengthCard = makeCard();
        StackPanel strengthContent;
        strengthContent.Spacing(8);
        Grid strengthHeading;
        ColumnDefinition titleColumn;
        titleColumn.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
        ColumnDefinition bandColumn;
        bandColumn.Width(GridLengthHelper::FromValueAndType(0.0, GridUnitType::Auto));
        strengthHeading.ColumnDefinitions().Append(titleColumn);
        strengthHeading.ColumnDefinitions().Append(bandColumn);
        auto const strengthTitle = CreateText(
            winforge::core::LocalizedText{ L"Strength", L"強度" }.Pick(m_language), 15, true);
        Grid::SetColumn(strengthTitle, 0);
        strengthHeading.Children().Append(strengthTitle);
        m_passwordStrengthBand = CreateText(L"—", 15, true);
        Grid::SetColumn(m_passwordStrengthBand, 1);
        AutomationProperties::SetAutomationId(m_passwordStrengthBand, L"NativePasswordStrengthBand");
        strengthHeading.Children().Append(m_passwordStrengthBand);
        strengthContent.Children().Append(strengthHeading);

        m_passwordStrengthBar = ProgressBar();
        m_passwordStrengthBar.Minimum(0);
        m_passwordStrengthBar.Maximum(1);
        m_passwordStrengthBar.Height(8);
        AutomationProperties::SetAutomationId(m_passwordStrengthBar, L"NativePasswordStrengthBar");
        AutomationProperties::SetName(m_passwordStrengthBar, ToHString(winforge::core::LocalizedText{
            L"Password strength progress", L"密碼強度進度" }.Pick(m_language)));
        strengthContent.Children().Append(m_passwordStrengthBar);

        m_passwordStrengthStatus = CreateText(L"", 12);
        m_passwordStrengthStatus.TextWrapping(TextWrapping::Wrap);
        m_passwordStrengthStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_passwordStrengthStatus, L"NativePasswordStrengthStatus");
        AutomationProperties::SetLiveSetting(
            m_passwordStrengthStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        strengthContent.Children().Append(m_passwordStrengthStatus);

        m_passwordStrengthCommonWarning = InfoBar();
        m_passwordStrengthCommonWarning.IsOpen(false);
        m_passwordStrengthCommonWarning.IsClosable(false);
        m_passwordStrengthCommonWarning.Severity(InfoBarSeverity::Error);
        AutomationProperties::SetAutomationId(m_passwordStrengthCommonWarning, L"NativePasswordStrengthCommonWarning");
        strengthContent.Children().Append(m_passwordStrengthCommonWarning);

        strengthContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Details", L"詳細資料" }.Pick(m_language), 14, true));
        m_passwordStrengthLength = CreateText(L"", 13);
        m_passwordStrengthPool = CreateText(L"", 13);
        m_passwordStrengthEntropy = CreateText(L"", 13);
        AutomationProperties::SetAutomationId(m_passwordStrengthLength, L"NativePasswordStrengthLength");
        AutomationProperties::SetAutomationId(m_passwordStrengthPool, L"NativePasswordStrengthPool");
        AutomationProperties::SetAutomationId(m_passwordStrengthEntropy, L"NativePasswordStrengthEntropy");
        strengthContent.Children().Append(m_passwordStrengthLength);
        strengthContent.Children().Append(m_passwordStrengthPool);
        strengthContent.Children().Append(m_passwordStrengthEntropy);
        strengthCard.Child(strengthContent);
        page.Children().Append(strengthCard);

        Border crackCard = makeCard();
        StackPanel crackContent;
        crackContent.Spacing(8);
        crackContent.Children().Append(CreateText(
            winforge::core::LocalizedText{
                L"Estimated time to crack (average, brute force)", L"估計破解時間（平均，暴力破解）" }.Pick(m_language), 14, true));
        m_passwordStrengthOnline = CreateText(L"", 13);
        m_passwordStrengthOnline.TextWrapping(TextWrapping::Wrap);
        m_passwordStrengthGpu = CreateText(L"", 13);
        m_passwordStrengthGpu.TextWrapping(TextWrapping::Wrap);
        m_passwordStrengthFast = CreateText(L"", 13);
        m_passwordStrengthFast.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_passwordStrengthOnline, L"NativePasswordStrengthOnline");
        AutomationProperties::SetAutomationId(m_passwordStrengthGpu, L"NativePasswordStrengthGpu");
        AutomationProperties::SetAutomationId(m_passwordStrengthFast, L"NativePasswordStrengthFast");
        crackContent.Children().Append(m_passwordStrengthOnline);
        crackContent.Children().Append(m_passwordStrengthGpu);
        crackContent.Children().Append(m_passwordStrengthFast);
        crackCard.Child(crackContent);
        page.Children().Append(crackCard);

        Border checklistCard = makeCard();
        m_passwordStrengthChecklist = StackPanel();
        m_passwordStrengthChecklist.Spacing(6);
        m_passwordStrengthChecklist.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Checklist", L"檢查清單" }.Pick(m_language), 14, true));
        AutomationProperties::SetAutomationId(m_passwordStrengthChecklist, L"NativePasswordStrengthChecklist");
        checklistCard.Child(m_passwordStrengthChecklist);
        page.Children().Append(checklistCard);

        ShowPage(page);
        m_passwordStrengthRendering = false;
        RefreshPasswordStrength();
    }

    void MainWindow::RefreshPasswordStrength()
    {
        auto const result = winforge::core::passwordstrength::Analyze(m_passwordStrengthValue);
        auto const bandLabel = [this, &result]()
        {
            static const std::array<winforge::core::LocalizedText, 5> Labels{
                winforge::core::LocalizedText{ L"Very weak", L"非常弱" },
                winforge::core::LocalizedText{ L"Weak", L"弱" },
                winforge::core::LocalizedText{ L"Fair", L"一般" },
                winforge::core::LocalizedText{ L"Strong", L"強" },
                winforge::core::LocalizedText{ L"Very strong", L"非常強" },
            };
            return Labels[std::clamp(result.band, 0, 4)].Pick(m_language);
        };
        auto const bandBlurb = [this, &result]()
        {
            static const std::array<winforge::core::LocalizedText, 5> Blurbs{
                winforge::core::LocalizedText{ L"Cracked almost instantly. Make it much longer and more varied.", L"幾乎即刻俾人破解。要長好多同埋更多變化。" },
                winforge::core::LocalizedText{ L"Weak — easily guessed. Add length and character types.", L"弱 — 好易估中。加長度同字元種類。" },
                winforge::core::LocalizedText{ L"Fair — okay for low-value logins, not for anything important.", L"一般 — 低價值帳戶勉強得，重要嘢就唔好。" },
                winforge::core::LocalizedText{ L"Strong — good for most accounts.", L"強 — 大部分帳戶都夠用。" },
                winforge::core::LocalizedText{ L"Very strong — excellent for high-value accounts.", L"非常強 — 高價值帳戶都好穩陣。" },
            };
            return Blurbs[std::clamp(result.band, 0, 4)].Pick(m_language);
        };

        auto formatEntropy = [&result]()
        {
            std::wostringstream stream;
            stream.setf(std::ios::fixed);
            stream.precision(1);
            stream << result.entropy_bits;
            return stream.str();
        };
        auto const entropy = formatEntropy();
        auto const empty = result.length == 0;
        auto const status = empty
            ? winforge::core::LocalizedText{ L"Start typing to analyze a password.", L"開始打字嚟分析密碼。" }.Pick(m_language)
            : bandBlurb();

        if (m_passwordStrengthBand)
        {
            auto const text = empty ? std::wstring{ L"—" } : bandLabel();
            m_passwordStrengthBand.Text(ToHString(text));
            AutomationProperties::SetName(m_passwordStrengthBand, ToHString(text));
        }
        if (m_passwordStrengthBar)
        {
            m_passwordStrengthBar.Value(empty ? 0.0 : result.fraction);
            auto const color = empty
                ? Windows::UI::Color{ 0xFF, 0x80, 0x80, 0x80 }
                : result.band == 0
                    ? Windows::UI::Color{ 0xFF, 0xE8, 0x1A, 0x1A }
                    : result.band == 1
                        ? Windows::UI::Color{ 0xFF, 0xE8, 0x7A, 0x1A }
                        : result.band == 2
                            ? Windows::UI::Color{ 0xFF, 0xE8, 0xC8, 0x1A }
                            : result.band == 3
                                ? Windows::UI::Color{ 0xFF, 0x5A, 0xC8, 0x3A }
                                : Windows::UI::Color{ 0xFF, 0x2E, 0xA8, 0x44 };
            m_passwordStrengthBar.Foreground(Media::SolidColorBrush(color));
        }
        if (m_passwordStrengthStatus)
        {
            m_passwordStrengthStatus.Text(ToHString(status));
            AutomationProperties::SetName(m_passwordStrengthStatus, ToHString(status));
        }
        if (m_passwordStrengthCommonWarning)
        {
            m_passwordStrengthCommonWarning.IsOpen(!empty && result.is_common);
            auto const warningTitle = winforge::core::LocalizedText{
                L"Known common password", L"常見密碼" }.Pick(m_language);
            auto const warningMessage = winforge::core::LocalizedText{
                L"This appears in public breach lists — attackers try it first. Do not use it.",
                L"呢個出現喺公開洩漏名單度 — 攻擊者會第一時間試。唔好用。" }.Pick(m_language);
            m_passwordStrengthCommonWarning.Title(ToHString(warningTitle));
            m_passwordStrengthCommonWarning.Message(ToHString(warningMessage));
            AutomationProperties::SetName(
                m_passwordStrengthCommonWarning,
                ToHString(!empty && result.is_common ? warningTitle + L": " + warningMessage : std::wstring{}));
        }

        auto const lengthText = winforge::core::LocalizedText{
            L"Length:  " + std::to_wstring(result.length) + L" characters",
            L"長度：  " + std::to_wstring(result.length) + L" 個字元" }.Pick(m_language);
        auto const poolText = winforge::core::LocalizedText{
            L"Character pool:  " + std::to_wstring(result.pool_size) + L" symbols",
            L"字元集合：  " + std::to_wstring(result.pool_size) + L" 個符號" }.Pick(m_language);
        auto const entropyText = winforge::core::LocalizedText{
            L"Entropy:  " + entropy + L" bits",
            L"熵值：  " + entropy + L" bits" }.Pick(m_language);
        if (m_passwordStrengthLength)
        {
            m_passwordStrengthLength.Text(ToHString(lengthText));
            AutomationProperties::SetName(m_passwordStrengthLength, ToHString(lengthText));
        }
        if (m_passwordStrengthPool)
        {
            m_passwordStrengthPool.Text(ToHString(poolText));
            AutomationProperties::SetName(m_passwordStrengthPool, ToHString(poolText));
        }
        if (m_passwordStrengthEntropy)
        {
            m_passwordStrengthEntropy.Text(ToHString(entropyText));
            AutomationProperties::SetName(m_passwordStrengthEntropy, ToHString(entropyText));
        }

        auto const onlineText = winforge::core::LocalizedText{
            L"Online (throttled, ~10K/s):  " + winforge::core::passwordstrength::HumanTime(
                result.online_seconds, winforge::core::passwordstrength::HumanTimeLanguage::English),
            L"線上（限速，約 1 萬次/秒）：  " + winforge::core::passwordstrength::HumanTime(
                result.online_seconds, winforge::core::passwordstrength::HumanTimeLanguage::Cantonese) }.Pick(m_language);
        auto const gpuText = winforge::core::LocalizedText{
            L"Offline GPU (~10B/s):  " + winforge::core::passwordstrength::HumanTime(
                result.offline_gpu_seconds, winforge::core::passwordstrength::HumanTimeLanguage::English),
            L"離線 GPU（約 100 億次/秒）：  " + winforge::core::passwordstrength::HumanTime(
                result.offline_gpu_seconds, winforge::core::passwordstrength::HumanTimeLanguage::Cantonese) }.Pick(m_language);
        auto const fastText = winforge::core::LocalizedText{
            L"Fast rig (~1T/s):  " + winforge::core::passwordstrength::HumanTime(
                result.fast_seconds, winforge::core::passwordstrength::HumanTimeLanguage::English),
            L"高速機器（約 1 萬億次/秒）：  " + winforge::core::passwordstrength::HumanTime(
                result.fast_seconds, winforge::core::passwordstrength::HumanTimeLanguage::Cantonese) }.Pick(m_language);
        if (m_passwordStrengthOnline)
        {
            m_passwordStrengthOnline.Text(ToHString(onlineText));
            AutomationProperties::SetName(m_passwordStrengthOnline, ToHString(onlineText));
        }
        if (m_passwordStrengthGpu)
        {
            m_passwordStrengthGpu.Text(ToHString(gpuText));
            AutomationProperties::SetName(m_passwordStrengthGpu, ToHString(gpuText));
        }
        if (m_passwordStrengthFast)
        {
            m_passwordStrengthFast.Text(ToHString(fastText));
            AutomationProperties::SetName(m_passwordStrengthFast, ToHString(fastText));
        }

        if (!m_passwordStrengthChecklist) return;
        while (m_passwordStrengthChecklist.Children().Size() > 1)
        {
            m_passwordStrengthChecklist.Children().RemoveAt(m_passwordStrengthChecklist.Children().Size() - 1);
        }

        auto appendCheck = [this](int32_t index, bool passed, std::wstring_view en, std::wstring_view zh)
        {
            auto const label = winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
            auto row = CreateText((passed ? L"✓  " : L"✕  ") + label, 13);
            row.TextWrapping(TextWrapping::Wrap);
            AutomationProperties::SetAutomationId(row, ToHString(L"NativePasswordStrengthCheck" + std::to_wstring(index)));
            AutomationProperties::SetName(row, ToHString((passed ? L"Pass: " : L"Needs work: ") + label));
            m_passwordStrengthChecklist.Children().Append(row);
        };
        appendCheck(0, result.len8, L"At least 8 characters", L"至少 8 個字元");
        appendCheck(1, result.len12, L"At least 12 characters", L"至少 12 個字元");
        appendCheck(2, result.len16, L"At least 16 characters", L"至少 16 個字元");
        appendCheck(3, result.has_lower, L"Has lowercase letters", L"有細楷字母");
        appendCheck(4, result.has_upper, L"Has uppercase letters", L"有大楷字母");
        appendCheck(5, result.has_digit, L"Has digits", L"有數字");
        appendCheck(6, result.has_symbol, L"Has symbols", L"有符號");
        appendCheck(7, result.no_repeats, L"No 3+ repeated characters", L"冇連續 3 個或以上重複字元");
        appendCheck(8, result.no_sequences, L"No simple sequences (abc / 123 / qwerty)", L"冇簡單序列（abc / 123 / qwerty）");
        appendCheck(9, !result.is_common, L"Not a known common password", L"唔係常見密碼");
    }

    void MainWindow::SetPasswordStrengthReveal(bool revealed)
    {
        auto const previousRendering = m_passwordStrengthRendering;
        m_passwordStrengthRendering = true;
        m_passwordStrengthRevealed = revealed;
        if (m_passwordStrengthReveal)
        {
            m_passwordStrengthReveal.IsOn(revealed);
        }
        if (revealed)
        {
            if (m_passwordStrengthHidden)
            {
                m_passwordStrengthHidden.Password(L"");
                m_passwordStrengthHidden.Visibility(Visibility::Collapsed);
            }
            if (m_passwordStrengthShown)
            {
                m_passwordStrengthShown.Text(ToHString(m_passwordStrengthValue));
                m_passwordStrengthShown.Visibility(Visibility::Visible);
            }
        }
        else
        {
            if (m_passwordStrengthShown)
            {
                m_passwordStrengthShown.Text(L"");
                m_passwordStrengthShown.Visibility(Visibility::Collapsed);
            }
            if (m_passwordStrengthHidden)
            {
                m_passwordStrengthHidden.Password(ToHString(m_passwordStrengthValue));
                m_passwordStrengthHidden.Visibility(Visibility::Visible);
            }
        }
        m_passwordStrengthRendering = previousRendering;
        RefreshPasswordStrength();
    }

    void MainWindow::ClearPasswordStrengthSecret()
    {
        auto const previousRendering = m_passwordStrengthRendering;
        m_passwordStrengthRendering = true;
        try
        {
            if (!m_passwordStrengthValue.empty())
            {
                SecureZeroMemory(
                    m_passwordStrengthValue.data(),
                    m_passwordStrengthValue.size() * sizeof(wchar_t));
            }
            m_passwordStrengthValue.clear();
            m_passwordStrengthValue.shrink_to_fit();
            if (m_passwordStrengthHidden) m_passwordStrengthHidden.Password(L"");
            if (m_passwordStrengthShown) m_passwordStrengthShown.Text(L"");
            if (m_passwordStrengthReveal) m_passwordStrengthReveal.IsOn(false);
            m_passwordStrengthRevealed = false;
        }
        catch (...)
        {
            // Secret cleanup is best-effort and must never block navigation.
        }
        m_passwordStrengthRendering = previousRendering;
    }

    void MainWindow::RenderUuidV7()
    {
        m_uuidV7Rendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"UUID v7", L"UUID v7 識別碼" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Generate RFC 9562 time-ordered UUIDv7 identifiers, or decode a UUID to inspect its version, variant, and embedded timestamp.",
                L"產生 RFC 9562 按時間排序嘅 UUIDv7 識別碼，或者解碼 UUID 睇版本、變體同內嵌時間戳。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeUuidV7Page");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native UUID v7 generation and decoding", L"全原生 UUID v7 產生同解碼" }.Pick(m_language)));
        nativeStatus.Message(ToHString(winforge::core::LocalizedText{
            L"C++ uses BCrypt randomness, RFC 9562 timestamp/version/variant bits, a monotonic guard, local decoding, and explicit-copy clipboard actions only.",
            L"C++ 使用 BCrypt 隨機源、RFC 9562 時間戳／版本／變體位元、單調排序保護、本機解碼；只有明確複製先會寫入剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeUuidV7ImplementationStatus");
        page.Children().Append(nativeStatus);

        auto makeCard = []()
        {
            Border card;
            card.Padding(Thickness{ 18, 16, 18, 18 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            return card;
        };

        Border generateCard = makeCard();
        StackPanel generateContent;
        generateContent.Spacing(12);
        generateContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Generate", L"產生" }.Pick(m_language), 15, true));

        auto countLabel = CreateText(
            winforge::core::LocalizedText{ L"How many (1–1000)", L"數量（1–1000）" }.Pick(m_language),
            14,
            true);
        generateContent.Children().Append(countLabel);

        m_uuidV7CountBox = NumberBox();
        m_uuidV7CountBox.Value(m_uuidV7Count);
        m_uuidV7CountBox.Minimum(winforge::core::uuidv7::MinimumBulkCount);
        m_uuidV7CountBox.Maximum(winforge::core::uuidv7::MaximumBulkCount);
        m_uuidV7CountBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_uuidV7CountBox.MinWidth(160);
        m_uuidV7CountBox.MaxWidth(300);
        AutomationProperties::SetAutomationId(m_uuidV7CountBox, L"NativeUuidV7Count");
        AutomationProperties::SetName(m_uuidV7CountBox, ToHString(winforge::core::LocalizedText{
            L"Number of UUID v7 values to generate", L"要產生嘅 UUID v7 數量" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV7CountBox, countLabel);
        m_uuidV7CountBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (m_uuidV7Rendering) return;
            auto value = sender.Value();
            if (std::isnan(value)) value = 1;
            m_uuidV7Count = std::clamp(
                static_cast<int32_t>(value),
                winforge::core::uuidv7::MinimumBulkCount,
                winforge::core::uuidv7::MaximumBulkCount);
        });
        generateContent.Children().Append(m_uuidV7CountBox);

        m_uuidV7MonotonicSwitch = ToggleSwitch();
        m_uuidV7MonotonicSwitch.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Sortable / monotonic", L"可排序／單調遞增" }.Pick(m_language))));
        m_uuidV7MonotonicSwitch.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
        m_uuidV7MonotonicSwitch.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
        m_uuidV7MonotonicSwitch.IsOn(m_uuidV7Monotonic);
        AutomationProperties::SetAutomationId(m_uuidV7MonotonicSwitch, L"NativeUuidV7Monotonic");
        AutomationProperties::SetName(m_uuidV7MonotonicSwitch, ToHString(winforge::core::LocalizedText{
            L"Keep UUID v7 values strictly ordered within the same millisecond", L"同一毫秒內保持 UUID v7 嚴格排序" }.Pick(m_language)));
        m_uuidV7MonotonicSwitch.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_uuidV7Rendering) return;
            m_uuidV7Monotonic = sender.as<ToggleSwitch>().IsOn();
        });
        generateContent.Children().Append(m_uuidV7MonotonicSwitch);

        auto generateButton = Button();
        generateButton.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Generate", L"產生" }.Pick(m_language))));
        generateButton.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(generateButton, L"NativeUuidV7Generate");
        AutomationProperties::SetName(generateButton, ToHString(winforge::core::LocalizedText{
            L"Generate UUID v7 values", L"產生 UUID v7" }.Pick(m_language)));
        generateButton.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            GenerateUuidV7Values();
        });
        generateContent.Children().Append(generateButton);

        m_uuidV7GeneratedOutput = TextBox();
        m_uuidV7GeneratedOutput.IsReadOnly(true);
        m_uuidV7GeneratedOutput.IsSpellCheckEnabled(false);
        m_uuidV7GeneratedOutput.AcceptsReturn(true);
        m_uuidV7GeneratedOutput.TextWrapping(TextWrapping::NoWrap);
        m_uuidV7GeneratedOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_uuidV7GeneratedOutput.Height(180);
        m_uuidV7GeneratedOutput.Text(ToHString(m_uuidV7GeneratedValue));
        AutomationProperties::SetAutomationId(m_uuidV7GeneratedOutput, L"NativeUuidV7GeneratedOutput");
        AutomationProperties::SetName(m_uuidV7GeneratedOutput, ToHString(winforge::core::LocalizedText{
            L"Generated UUID v7 values", L"已產生嘅 UUID v7" }.Pick(m_language)));
        AutomationProperties::SetHelpText(m_uuidV7GeneratedOutput, ToHString(m_uuidV7GeneratedValue));
        generateContent.Children().Append(m_uuidV7GeneratedOutput);

        auto copyGenerated = Button();
        copyGenerated.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy all", L"全部複製" }.Pick(m_language))));
        copyGenerated.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(copyGenerated, L"NativeUuidV7CopyGenerated");
        AutomationProperties::SetName(copyGenerated, ToHString(winforge::core::LocalizedText{
            L"Copy generated UUID v7 values", L"複製已產生嘅 UUID v7" }.Pick(m_language)));
        copyGenerated.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUuidV7Value(
                m_uuidV7GeneratedValue,
                winforge::core::LocalizedText{ L"Generated UUIDv7 values copied to clipboard.", L"已複製產生嘅 UUIDv7 去剪貼簿。" }.Pick(m_language));
        });
        generateContent.Children().Append(copyGenerated);
        generateCard.Child(generateContent);
        page.Children().Append(generateCard);

        Border decodeCard = makeCard();
        StackPanel decodeContent;
        decodeContent.Spacing(12);
        decodeContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Decode", L"解碼" }.Pick(m_language), 15, true));
        auto decodeLabel = CreateText(
            winforge::core::LocalizedText{ L"UUID input", L"UUID 輸入" }.Pick(m_language), 14, true);
        decodeContent.Children().Append(decodeLabel);

        m_uuidV7DecodeInput = TextBox();
        m_uuidV7DecodeInput.IsSpellCheckEnabled(false);
        m_uuidV7DecodeInput.FontFamily(Media::FontFamily(L"Consolas"));
        m_uuidV7DecodeInput.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"Paste a UUID, urn:uuid value, or braced UUID…", L"貼 UUID、urn:uuid 或大括號 UUID…" }.Pick(m_language)));
        m_uuidV7DecodeInput.Text(ToHString(m_uuidV7DecodeInputValue));
        AutomationProperties::SetAutomationId(m_uuidV7DecodeInput, L"NativeUuidV7DecodeInput");
        AutomationProperties::SetName(m_uuidV7DecodeInput, ToHString(winforge::core::LocalizedText{
            L"UUID value to decode", L"要解碼嘅 UUID" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV7DecodeInput, decodeLabel);
        m_uuidV7DecodeInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_uuidV7Rendering) return;
            m_uuidV7DecodeInputValue = ToWide(sender.as<TextBox>().Text());
            if (m_uuidV7DecodeInputValue.empty())
            {
                ClearUuidV7DecodeResults();
            }
        });
        decodeContent.Children().Append(m_uuidV7DecodeInput);

        auto decodeButton = Button();
        decodeButton.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Decode", L"解碼" }.Pick(m_language))));
        decodeButton.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(decodeButton, L"NativeUuidV7Decode");
        AutomationProperties::SetName(decodeButton, ToHString(winforge::core::LocalizedText{
            L"Decode UUID version variant and timestamp", L"解碼 UUID 版本、變體同時間戳" }.Pick(m_language)));
        decodeButton.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            DecodeUuidV7Value();
        });
        decodeContent.Children().Append(decodeButton);

        m_uuidV7DecodeResults = StackPanel();
        m_uuidV7DecodeResults.Spacing(8);
        m_uuidV7DecodeResults.Visibility(Visibility::Collapsed);
        AutomationProperties::SetAutomationId(m_uuidV7DecodeResults, L"NativeUuidV7DecodeResults");

        auto addResult = [this, &decodeContent](
            std::wstring_view label,
            std::wstring_view automationId,
            std::wstring_view accessibleName)
        {
            auto caption = CreateText(label, 12, true);
            caption.Opacity(0.75);
            m_uuidV7DecodeResults.Children().Append(caption);
            TextBox value;
            value.IsReadOnly(true);
            value.IsSpellCheckEnabled(false);
            value.FontFamily(Media::FontFamily(L"Consolas"));
            value.TextWrapping(TextWrapping::Wrap);
            AutomationProperties::SetAutomationId(value, ToHString(automationId));
            AutomationProperties::SetName(value, ToHString(accessibleName));
            m_uuidV7DecodeResults.Children().Append(value);
            return value;
        };
        m_uuidV7VersionOutput = addResult(
            winforge::core::LocalizedText{ L"Version", L"版本" }.Pick(m_language),
            L"NativeUuidV7VersionOutput",
            winforge::core::LocalizedText{ L"UUID version", L"UUID 版本" }.Pick(m_language));
        m_uuidV7VariantOutput = addResult(
            winforge::core::LocalizedText{ L"Variant", L"變體" }.Pick(m_language),
            L"NativeUuidV7VariantOutput",
            winforge::core::LocalizedText{ L"UUID variant", L"UUID 變體" }.Pick(m_language));
        m_uuidV7UtcOutput = addResult(
            winforge::core::LocalizedText{ L"Timestamp (UTC)", L"時間戳（UTC）" }.Pick(m_language),
            L"NativeUuidV7UtcOutput",
            winforge::core::LocalizedText{ L"UUID timestamp in UTC", L"UUID UTC 時間戳" }.Pick(m_language));
        m_uuidV7LocalOutput = addResult(
            winforge::core::LocalizedText{ L"Timestamp (local)", L"時間戳（本地）" }.Pick(m_language),
            L"NativeUuidV7LocalOutput",
            winforge::core::LocalizedText{ L"UUID timestamp in local time", L"UUID 本地時間戳" }.Pick(m_language));
        m_uuidV7CanonicalOutput = addResult(
            winforge::core::LocalizedText{ L"Canonical", L"標準格式" }.Pick(m_language),
            L"NativeUuidV7CanonicalOutput",
            winforge::core::LocalizedText{ L"Canonical UUID", L"標準 UUID" }.Pick(m_language));
        decodeContent.Children().Append(m_uuidV7DecodeResults);

        auto copyTimestamp = Button();
        copyTimestamp.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy timestamp", L"複製時間戳" }.Pick(m_language))));
        copyTimestamp.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(copyTimestamp, L"NativeUuidV7CopyTimestamp");
        AutomationProperties::SetName(copyTimestamp, ToHString(winforge::core::LocalizedText{
            L"Copy decoded UTC timestamp", L"複製已解碼 UTC 時間戳" }.Pick(m_language)));
        copyTimestamp.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUuidV7Value(
                m_uuidV7TimestampValue,
                winforge::core::LocalizedText{ L"Timestamp copied to clipboard.", L"已複製時間戳去剪貼簿。" }.Pick(m_language));
        });
        decodeContent.Children().Append(copyTimestamp);
        decodeCard.Child(decodeContent);
        page.Children().Append(decodeCard);

        m_uuidV7Status = CreateText(L"", 12.5);
        m_uuidV7Status.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_uuidV7Status, L"NativeUuidV7Status");
        AutomationProperties::SetLiveSetting(
            m_uuidV7Status,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_uuidV7Status);

        ShowPage(page);
        m_uuidV7Rendering = false;

        if (!m_uuidV7DecodeInputValue.empty())
        {
            DecodeUuidV7Value();
        }
    }

    void MainWindow::GenerateUuidV7Values()
    {
        try
        {
            auto value = m_uuidV7CountBox ? m_uuidV7CountBox.Value() : static_cast<double>(m_uuidV7Count);
            if (std::isnan(value)) value = 1;
            m_uuidV7Count = std::clamp(
                static_cast<int32_t>(value),
                winforge::core::uuidv7::MinimumBulkCount,
                winforge::core::uuidv7::MaximumBulkCount);
            if (m_uuidV7CountBox)
            {
                m_uuidV7CountBox.Value(m_uuidV7Count);
            }
            if (m_uuidV7MonotonicSwitch)
            {
                m_uuidV7Monotonic = m_uuidV7MonotonicSwitch.IsOn();
            }

            m_uuidV7GeneratedValue = winforge::core::uuidv7::BulkUuidV7(
                m_uuidV7Count,
                m_uuidV7Monotonic);
            if (m_uuidV7GeneratedOutput)
            {
                m_uuidV7GeneratedOutput.Text(ToHString(m_uuidV7GeneratedValue));
                AutomationProperties::SetHelpText(m_uuidV7GeneratedOutput, ToHString(m_uuidV7GeneratedValue));
            }

            auto const count = std::to_wstring(m_uuidV7Count);
            AnnounceUuidV7Status(winforge::core::LocalizedText{
                L"Generated " + count + L" UUIDv7 value" + (m_uuidV7Count == 1 ? L"." : L"s."),
                L"已產生 " + count + L" 個 UUIDv7。" }.Pick(m_language));
        }
        catch (...)
        {
            AnnounceUuidV7Status(winforge::core::LocalizedText{
                L"Could not generate UUIDv7 values.", L"無法產生 UUIDv7。" }.Pick(m_language), true);
        }
    }

    void MainWindow::ClearUuidV7DecodeResults()
    {
        m_uuidV7TimestampValue.clear();
        if (m_uuidV7VersionOutput) m_uuidV7VersionOutput.Text(L"");
        if (m_uuidV7VariantOutput) m_uuidV7VariantOutput.Text(L"");
        if (m_uuidV7UtcOutput) m_uuidV7UtcOutput.Text(L"");
        if (m_uuidV7LocalOutput) m_uuidV7LocalOutput.Text(L"");
        if (m_uuidV7CanonicalOutput) m_uuidV7CanonicalOutput.Text(L"");
        if (m_uuidV7DecodeResults) m_uuidV7DecodeResults.Visibility(Visibility::Collapsed);
    }

    void MainWindow::DecodeUuidV7Value()
    {
        if (!m_uuidV7DecodeResults)
        {
            return;
        }

        try
        {
            auto const decoded = winforge::core::uuidv7::DecodeUuid(m_uuidV7DecodeInputValue);
            if (!decoded.ok)
            {
                ClearUuidV7DecodeResults();
                AnnounceUuidV7Status(decoded.error == L"empty"
                    ? winforge::core::LocalizedText{ L"Paste a UUID to decode.", L"貼 UUID 入嚟解碼。" }.Pick(m_language)
                    : winforge::core::LocalizedText{ L"That is not a valid UUID.", L"呢個唔係有效 UUID。" }.Pick(m_language), true);
                return;
            }

            auto const version = std::to_wstring(decoded.version);
            m_uuidV7VersionOutput.Text(ToHString(version));
            m_uuidV7VariantOutput.Text(ToHString(decoded.variant_name));
            m_uuidV7CanonicalOutput.Text(ToHString(decoded.canonical));
            AutomationProperties::SetHelpText(m_uuidV7VersionOutput, ToHString(version));
            AutomationProperties::SetHelpText(m_uuidV7VariantOutput, ToHString(decoded.variant_name));
            AutomationProperties::SetHelpText(m_uuidV7CanonicalOutput, ToHString(decoded.canonical));

            if (decoded.has_timestamp)
            {
                auto const utc = winforge::core::uuidv7::FormatTimestampUtc(decoded.unix_milliseconds);
                auto const local = winforge::core::uuidv7::FormatTimestampLocal(decoded.unix_milliseconds);
                auto const unavailable = winforge::core::LocalizedText{
                    L"(timestamp is outside the displayable calendar range)",
                    L"（時間戳超出可顯示日曆範圍）" }.Pick(m_language);
                auto const utcText = utc.empty() ? unavailable : utc;
                auto const localText = local.empty() ? unavailable : local;
                m_uuidV7UtcOutput.Text(ToHString(utcText));
                m_uuidV7LocalOutput.Text(ToHString(localText));
                AutomationProperties::SetHelpText(m_uuidV7UtcOutput, ToHString(utcText));
                AutomationProperties::SetHelpText(m_uuidV7LocalOutput, ToHString(localText));
                m_uuidV7TimestampValue = utc;
            }
            else
            {
                auto const none = winforge::core::LocalizedText{
                    L"(no embedded timestamp for this version)",
                    L"（呢個版本冇內嵌時間戳）" }.Pick(m_language);
                m_uuidV7UtcOutput.Text(ToHString(none));
                m_uuidV7LocalOutput.Text(ToHString(none));
                AutomationProperties::SetHelpText(m_uuidV7UtcOutput, ToHString(none));
                AutomationProperties::SetHelpText(m_uuidV7LocalOutput, ToHString(none));
                m_uuidV7TimestampValue.clear();
            }
            m_uuidV7DecodeResults.Visibility(Visibility::Visible);

            if (decoded.version == 7)
            {
                AnnounceUuidV7Status(winforge::core::LocalizedText{
                    L"Valid UUIDv7 — timestamp extracted.", L"有效 UUIDv7 — 已抽出時間戳。" }.Pick(m_language));
            }
            else if (decoded.version == 1)
            {
                AnnounceUuidV7Status(winforge::core::LocalizedText{
                    L"This is UUIDv1, not v7 — timestamp decoded best-effort.",
                    L"呢個係 UUIDv1，唔係 v7 — 已盡力解碼時間戳。" }.Pick(m_language));
            }
            else
            {
                AnnounceUuidV7Status(winforge::core::LocalizedText{
                    L"This UUID version does not carry a time-ordered timestamp.",
                    L"呢個 UUID 版本冇按時間排序嘅時間戳。" }.Pick(m_language), true);
            }
        }
        catch (...)
        {
            ClearUuidV7DecodeResults();
            AnnounceUuidV7Status(winforge::core::LocalizedText{
                L"Could not decode that UUID.", L"無法解碼呢個 UUID。" }.Pick(m_language), true);
        }
    }

    void MainWindow::CopyUuidV7Value(std::wstring_view value, std::wstring_view successMessage)
    {
        if (value.empty())
        {
            AnnounceUuidV7Status(winforge::core::LocalizedText{
                L"Nothing to copy.", L"冇嘢可以複製。" }.Pick(m_language), true);
            return;
        }

        try
        {
            Windows::ApplicationModel::DataTransfer::DataPackage package;
            package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
            package.SetText(ToHString(value));
            Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
            AnnounceUuidV7Status(successMessage);
        }
        catch (...)
        {
            AnnounceUuidV7Status(winforge::core::LocalizedText{
                L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
        }
    }

    void MainWindow::AnnounceUuidV7Status(std::wstring_view message, bool warning)
    {
        if (!m_uuidV7Status) return;

        m_uuidV7Status.Text(ToHString(message));
        m_uuidV7Status.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_uuidV7Status, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_uuidV7Status,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_uuidV7Status);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_uuidV7Status);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility notification failure must not break UUID handling.
        }
    }

    int32_t MainWindow::PackageViewFromArgument(std::wstring_view argument) const
    {
        auto key = winforge::core::NormalizeRouteKey(argument);
        while (!key.empty() && (key.front() == L'#' || key.front() == L'/'))
        {
            key.erase(key.begin());
        }
        auto const view = winforge::core::packages::PackageViewFromFragment(key);
        if (view)
        {
            return static_cast<int32_t>(*view);
        }
        return 0;
    }

    void MainWindow::PopulatePackageManagerFilters(StackPanel const& panel)
    {
        panel.Children().Clear();
        for (auto const& manager : winforge::core::packages::PackageManagers())
        {
            auto key = std::wstring(manager.key);
            if (!m_packageManagersSelected.contains(key))
            {
                m_packageManagersSelected.emplace(key, true);
            }

            CheckBox filter;
            auto label = CreateText(
                winforge::core::LocalizedText{
                    std::wstring(manager.name_en), std::wstring(manager.name_zh) }.Pick(m_language),
                14);
            label.TextWrapping(TextWrapping::WrapWholeWords);
            filter.Content(label);
            filter.Tag(box_value(ToHString(key)));
            filter.IsChecked(m_packageManagersSelected[key]);
            if (m_packageProbeComplete)
            {
                auto const available = m_packageManagersAvailable.contains(key) && m_packageManagersAvailable[key];
                filter.IsEnabled(available);
                if (!available)
                {
                    auto const diagnostic = m_packageProbeDiagnostics.contains(key)
                        ? m_packageProbeDiagnostics[key]
                        : std::wstring(L"not available");
                    ToolTipService::SetToolTip(filter, box_value(ToHString(diagnostic)));
                    AutomationProperties::SetHelpText(filter, ToHString(diagnostic));
                }
            }
            filter.Margin(Thickness{ 0, 0, 0, 2 });
            AutomationProperties::SetAutomationId(
                filter,
                ToHString(L"NativePackageManagerFilter_" + AutomationKey(key)));
        filter.Checked([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            auto const check = sender.as<CheckBox>();
            auto const keyValue = unbox_value_or<hstring>(check.Tag(), L"");
            m_packageManagersSelected[ToWide(keyValue)] = true;
            SavePackageManagerState();
                if (InvalidatePackageQueryResults())
                {
                    AnnouncePackageStatus(
                        L"Package-manager selection changed. Previous query results were cleared; run the query again.",
                        L"套件管理器選擇已更改。之前嘅查詢結果已清除；請重新執行查詢。");
                }
                RenderPackageManagerView();
            });
        filter.Unchecked([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            auto const check = sender.as<CheckBox>();
            auto const keyValue = unbox_value_or<hstring>(check.Tag(), L"");
            m_packageManagersSelected[ToWide(keyValue)] = false;
            SavePackageManagerState();
                if (InvalidatePackageQueryResults())
                {
                    AnnouncePackageStatus(
                        L"Package-manager selection changed. Previous query results were cleared; run the query again.",
                        L"套件管理器選擇已更改。之前嘅查詢結果已清除；請重新執行查詢。");
                }
                RenderPackageManagerView();
            });
            panel.Children().Append(filter);
        }
    }

    void MainWindow::LoadPackageManagerState()
    {
        m_packageRememberView = true;
        m_packageRememberSearch = true;
        m_packageRememberFilters = true;
        m_packageSortMode = 0;
        m_packageSearchText.clear();
        m_packageSearchMode = winforge::core::packages::PackageSearchMode::Both;
        m_packageSearchCaseSensitiveValue = false;
        m_packageSearchIgnoreSpecialValue = false;
        m_packageDiscoverRegexEnabled = false;
        m_packageDiscoverRegexMultiline = false;
        m_packageDiscoverRegexDotMatchesNewline = false;
        m_packageDiscoverRegexPattern.clear();
        m_packageDiscoverRegexDiagnostic.clear();
        ClearPackageSelection();
        m_packageManagersSelected.clear();
        for (auto const& manager : winforge::core::packages::PackageManagers())
        {
            m_packageManagersSelected.emplace(std::wstring(manager.key), true);
        }

        std::error_code ignored;
        if (!std::filesystem::is_regular_file(m_packageStatePath, ignored))
        {
            return;
        }

        std::ifstream input(m_packageStatePath, std::ios::binary);
        if (!input)
        {
            return;
        }

        std::ostringstream stream;
        stream << input.rdbuf();
        auto json = stream.str();
        if (json.empty())
        {
            return;
        }

        try
        {
            auto const root = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(json));
            m_packageRememberView = root.GetNamedBoolean(L"rememberView", true);
            m_packageRememberSearch = root.GetNamedBoolean(L"rememberSearch", true);
            m_packageRememberFilters = root.GetNamedBoolean(L"rememberFilters", true);
            m_packageSortMode = std::clamp(
                static_cast<int32_t>(root.GetNamedNumber(L"sortMode", static_cast<double>(m_packageSortMode))),
                0,
                3);
            m_packageSnoozeDays = NormalizeSnoozeDays(
                static_cast<int32_t>(root.GetNamedNumber(L"snoozeDays", static_cast<double>(m_packageSnoozeDays))));

            if (m_packageRememberFilters)
            {
                auto const searchMode = std::clamp(
                    static_cast<int32_t>(root.GetNamedNumber(
                        L"discoverSearchMode",
                        static_cast<double>(static_cast<int32_t>(m_packageSearchMode)))),
                    0,
                    4);
                m_packageSearchMode = static_cast<winforge::core::packages::PackageSearchMode>(searchMode);
                m_packageSearchCaseSensitiveValue = root.GetNamedBoolean(
                    L"discoverSearchCaseSensitive",
                    false);
                m_packageSearchIgnoreSpecialValue = root.GetNamedBoolean(
                    L"discoverSearchIgnoreSpecial",
                    false);
                m_packageDiscoverRegexEnabled = root.GetNamedBoolean(
                    L"discoverRegexEnabled",
                    false);
                m_packageDiscoverRegexMultiline = root.GetNamedBoolean(
                    L"discoverRegexMultiline",
                    false);
                m_packageDiscoverRegexDotMatchesNewline = root.GetNamedBoolean(
                    L"discoverRegexDotMatchesNewline",
                    false);
                if (root.HasKey(L"discoverRegexPattern"))
                {
                    auto const persistedPattern = ToWide(root.GetNamedString(L"discoverRegexPattern"));
                    // Never load an unbounded convenience value into a live
                    // native regex input. The compiler applies the same cap.
                    if (persistedPattern.size() <= 512)
                    {
                        m_packageDiscoverRegexPattern = persistedPattern;
                    }
                    else
                    {
                        m_packageDiscoverRegexEnabled = false;
                    }
                }
            }

            if (m_packageRememberView)
            {
                m_packageView = std::clamp(
                    static_cast<int32_t>(root.GetNamedNumber(L"view", static_cast<double>(m_packageView))),
                    0,
                    8);
            }

            if (m_packageRememberSearch && root.HasKey(L"search"))
            {
                m_packageSearchText = ToWide(root.GetNamedString(L"search"));
            }

            if (m_packageRememberFilters && root.HasKey(L"selectedManagers"))
            {
                for (auto& [key, selected] : m_packageManagersSelected)
                {
                    selected = false;
                }

                auto const selectedManagers = root.GetNamedArray(L"selectedManagers");
                for (auto const& value : selectedManagers)
                {
                    auto const key = ToWide(value.GetString());
                    auto const found = m_packageManagersSelected.find(key);
                    if (found != m_packageManagersSelected.end())
                    {
                        found->second = true;
                    }
                }
            }

            if (root.HasKey(L"operationEntries"))
            {
                auto const entries = root.GetNamedArray(L"operationEntries");
                m_packageOperations.clear();
                for (auto const& value : entries)
                {
                    auto const object = value.GetObject();
                    PackageOperationEntry entry;
                    entry.id = object.HasKey(L"id") ? ToWide(object.GetNamedString(L"id")) : std::wstring{};
                    entry.title = object.HasKey(L"title")
                        ? winforge::core::packages::RedactPackageMutationText(ToWide(object.GetNamedString(L"title")))
                        : std::wstring{};
                    entry.details = object.HasKey(L"details")
                        ? winforge::core::packages::RedactPackageMutationText(ToWide(object.GetNamedString(L"details")))
                        : std::wstring{};
                    entry.status = object.HasKey(L"status")
                        ? winforge::core::packages::RedactPackageMutationText(ToWide(object.GetNamedString(L"status")))
                        : std::wstring{};
                    entry.created_epoch_seconds = object.HasKey(L"created")
                        ? static_cast<std::int64_t>(object.GetNamedNumber(L"created"))
                        : 0;
                    entry.retry_count = object.HasKey(L"retryCount")
                        ? std::max(0, static_cast<int32_t>(object.GetNamedNumber(L"retryCount")))
                        : 0;
                    if (entry.id.empty())
                    {
                        entry.id = L"loaded-" + std::to_wstring(m_packageOperationSequence++);
                    }
                    if (entry.title.empty())
                    {
                        entry.title = L"Operation event";
                    }
                    if (entry.status.empty())
                    {
                        entry.status = L"Queued preview";
                    }
                    if (!entry.details.empty())
                    {
                        m_packageOperations.push_back(std::move(entry));
                    }
                    if (m_packageOperations.size() >= 50)
                    {
                        break;
                    }
                }
            }
            else if (root.HasKey(L"operationLog"))
            {
                auto const history = root.GetNamedArray(L"operationLog");
                m_packageOperations.clear();
                for (auto const& value : history)
                {
                    PackageOperationEntry entry;
                    entry.id = L"legacy-" + std::to_wstring(m_packageOperationSequence++);
                    entry.title = L"Legacy operation event";
                    entry.details = winforge::core::packages::RedactPackageMutationText(ToWide(value.GetString()));
                    entry.status = L"Imported legacy history";
                    entry.created_epoch_seconds = 0;
                    if (!entry.details.empty())
                    {
                        m_packageOperations.push_back(std::move(entry));
                    }
                    if (m_packageOperations.size() >= 50)
                    {
                        break;
                    }
                }
            }

            if (root.HasKey(L"ignoredRules"))
            {
                auto const rules = root.GetNamedArray(L"ignoredRules");
                m_packageIgnoredRules.clear();
                std::unordered_set<std::wstring> seen;
                for (auto const& value : rules)
                {
                    auto const object = value.GetObject();
                    auto const manager = object.HasKey(L"manager") ? ToWide(object.GetNamedString(L"manager")) : std::wstring{};
                    auto const packageId = object.HasKey(L"id") ? ToWide(object.GetNamedString(L"id")) : std::wstring{};
                    if (!winforge::core::packages::FindPackageManager(manager))
                    {
                        continue;
                    }
                    auto const validation = winforge::core::packages::ValidatePackageReference(manager, packageId);
                    if (!validation)
                    {
                        continue;
                    }
                    auto const key = PackageRuleKey(manager, packageId);
                    if (!seen.insert(key).second)
                    {
                        continue;
                    }
                    PackageIgnoredRule rule;
                    rule.manager_key = manager;
                    rule.package_id = packageId;
                    if (object.HasKey(L"name")) rule.package_name = ToWide(object.GetNamedString(L"name"));
                    if (object.HasKey(L"version")) rule.version = ToWide(object.GetNamedString(L"version"));
                    m_packageIgnoredRules.push_back(std::move(rule));
                }
            }

            if (root.HasKey(L"pinnedRules"))
            {
                auto const rules = root.GetNamedArray(L"pinnedRules");
                m_packagePinnedRules.clear();
                std::unordered_set<std::wstring> seen;
                for (auto const& value : rules)
                {
                    auto const object = value.GetObject();
                    auto const manager = object.HasKey(L"manager") ? ToWide(object.GetNamedString(L"manager")) : std::wstring{};
                    auto const packageId = object.HasKey(L"id") ? ToWide(object.GetNamedString(L"id")) : std::wstring{};
                    auto const version = object.HasKey(L"version") ? ToWide(object.GetNamedString(L"version")) : std::wstring{};
                    if (version.empty() || !winforge::core::packages::FindPackageManager(manager))
                    {
                        continue;
                    }
                    auto const validation = winforge::core::packages::ValidatePackageReference(manager, packageId);
                    if (!validation)
                    {
                        continue;
                    }
                    auto const key = PackageVersionRuleKey(manager, packageId, version);
                    if (!seen.insert(key).second)
                    {
                        continue;
                    }
                    PackagePinnedRule rule;
                    rule.manager_key = manager;
                    rule.package_id = packageId;
                    rule.version = version;
                    if (object.HasKey(L"name")) rule.package_name = ToWide(object.GetNamedString(L"name"));
                    m_packagePinnedRules.push_back(std::move(rule));
                }
            }

            if (root.HasKey(L"snoozedRules"))
            {
                auto const rules = root.GetNamedArray(L"snoozedRules");
                m_packageSnoozedRules.clear();
                std::unordered_set<std::wstring> seen;
                auto const now = NowUnixSeconds();
                for (auto const& value : rules)
                {
                    auto const object = value.GetObject();
                    auto const manager = object.HasKey(L"manager") ? ToWide(object.GetNamedString(L"manager")) : std::wstring{};
                    auto const packageId = object.HasKey(L"id") ? ToWide(object.GetNamedString(L"id")) : std::wstring{};
                    auto const until = object.HasKey(L"until") ? static_cast<std::int64_t>(object.GetNamedNumber(L"until")) : 0;
                    if (until <= now || !winforge::core::packages::FindPackageManager(manager))
                    {
                        continue;
                    }
                    auto const validation = winforge::core::packages::ValidatePackageReference(manager, packageId);
                    if (!validation)
                    {
                        continue;
                    }
                    auto const key = PackageRuleKey(manager, packageId);
                    if (!seen.insert(key).second)
                    {
                        continue;
                    }
                    PackageSnoozedRule rule;
                    rule.manager_key = manager;
                    rule.package_id = packageId;
                    rule.until_epoch_seconds = until;
                    if (object.HasKey(L"name")) rule.package_name = ToWide(object.GetNamedString(L"name"));
                    if (object.HasKey(L"version")) rule.version = ToWide(object.GetNamedString(L"version"));
                    m_packageSnoozedRules.push_back(std::move(rule));
                }
            }
        }
        catch (...)
        {
            // Package-manager state is a convenience cache; malformed data is ignored.
        }
    }

    void MainWindow::SavePackageManagerState() const
    {
        try
        {
            if (m_packageStatePath.empty())
            {
                return;
            }

            std::error_code ignored;
            std::filesystem::create_directories(m_packageStatePath.parent_path(), ignored);

            winrt::Windows::Data::Json::JsonObject root;
            root.SetNamedValue(L"rememberView", winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageRememberView));
            root.SetNamedValue(L"rememberSearch", winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageRememberSearch));
            root.SetNamedValue(L"rememberFilters", winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageRememberFilters));
            root.SetNamedValue(L"sortMode", winrt::Windows::Data::Json::JsonValue::CreateNumberValue(static_cast<double>(m_packageSortMode)));
            root.SetNamedValue(L"snoozeDays", winrt::Windows::Data::Json::JsonValue::CreateNumberValue(static_cast<double>(NormalizeSnoozeDays(m_packageSnoozeDays))));
            root.SetNamedValue(L"view", winrt::Windows::Data::Json::JsonValue::CreateNumberValue(static_cast<double>(m_packageView)));
            root.SetNamedValue(L"search", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(m_packageSearchText)));
            root.SetNamedValue(
                L"discoverSearchMode",
                winrt::Windows::Data::Json::JsonValue::CreateNumberValue(
                    static_cast<double>(static_cast<int32_t>(m_packageSearchMode))));
            root.SetNamedValue(
                L"discoverSearchCaseSensitive",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageSearchCaseSensitiveValue));
            root.SetNamedValue(
                L"discoverSearchIgnoreSpecial",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageSearchIgnoreSpecialValue));
            root.SetNamedValue(
                L"discoverRegexEnabled",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageDiscoverRegexEnabled));
            root.SetNamedValue(
                L"discoverRegexMultiline",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageDiscoverRegexMultiline));
            root.SetNamedValue(
                L"discoverRegexDotMatchesNewline",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(m_packageDiscoverRegexDotMatchesNewline));
            root.SetNamedValue(
                L"discoverRegexPattern",
                winrt::Windows::Data::Json::JsonValue::CreateStringValue(
                    ToHString(m_packageDiscoverRegexPattern.substr(0, 512))));

            winrt::Windows::Data::Json::JsonArray selectedManagers;
            for (auto const& [key, selected] : m_packageManagersSelected)
            {
                if (selected)
                {
                    selectedManagers.Append(
                        winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(key)));
                }
            }
            root.SetNamedValue(L"selectedManagers", selectedManagers);

            winrt::Windows::Data::Json::JsonArray operationEntries;
            winrt::Windows::Data::Json::JsonArray operationLog;
            for (auto const& entry : m_packageOperations)
            {
                winrt::Windows::Data::Json::JsonObject object;
                object.SetNamedValue(L"id", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(entry.id)));
                object.SetNamedValue(L"title", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(entry.title)));
                object.SetNamedValue(L"details", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(entry.details)));
                object.SetNamedValue(L"status", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(entry.status)));
                object.SetNamedValue(L"created", winrt::Windows::Data::Json::JsonValue::CreateNumberValue(static_cast<double>(entry.created_epoch_seconds)));
                object.SetNamedValue(L"retryCount", winrt::Windows::Data::Json::JsonValue::CreateNumberValue(static_cast<double>(entry.retry_count)));
                operationEntries.Append(object);
                operationLog.Append(winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(entry.details)));
            }
            root.SetNamedValue(L"operationEntries", operationEntries);
            root.SetNamedValue(L"operationLog", operationLog);

            winrt::Windows::Data::Json::JsonArray ignoredRules;
            for (auto const& rule : m_packageIgnoredRules)
            {
                winrt::Windows::Data::Json::JsonObject object;
                object.SetNamedValue(L"manager", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.manager_key)));
                object.SetNamedValue(L"id", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.package_id)));
                object.SetNamedValue(L"name", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.package_name)));
                object.SetNamedValue(L"version", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.version)));
                ignoredRules.Append(object);
            }
            root.SetNamedValue(L"ignoredRules", ignoredRules);

            winrt::Windows::Data::Json::JsonArray pinnedRules;
            for (auto const& rule : m_packagePinnedRules)
            {
                winrt::Windows::Data::Json::JsonObject object;
                object.SetNamedValue(L"manager", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.manager_key)));
                object.SetNamedValue(L"id", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.package_id)));
                object.SetNamedValue(L"name", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.package_name)));
                object.SetNamedValue(L"version", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.version)));
                pinnedRules.Append(object);
            }
            root.SetNamedValue(L"pinnedRules", pinnedRules);

            winrt::Windows::Data::Json::JsonArray snoozedRules;
            for (auto const& rule : m_packageSnoozedRules)
            {
                winrt::Windows::Data::Json::JsonObject object;
                object.SetNamedValue(L"manager", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.manager_key)));
                object.SetNamedValue(L"id", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.package_id)));
                object.SetNamedValue(L"name", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.package_name)));
                object.SetNamedValue(L"version", winrt::Windows::Data::Json::JsonValue::CreateStringValue(ToHString(rule.version)));
                object.SetNamedValue(L"until", winrt::Windows::Data::Json::JsonValue::CreateNumberValue(static_cast<double>(rule.until_epoch_seconds)));
                snoozedRules.Append(object);
            }
            root.SetNamedValue(L"snoozedRules", snoozedRules);

            std::ofstream output(m_packageStatePath, std::ios::binary | std::ios::trunc);
            if (!output)
            {
                return;
            }

            output << winrt::to_string(root.Stringify());
        }
        catch (...)
        {
            // Convenience persistence only; failures must not affect the native shell.
        }
    }

    void MainWindow::ResetPackageManagerState()
    {
        m_packageStateApplying = true;
        try
        {
            m_packageRememberView = true;
            m_packageRememberSearch = true;
            m_packageRememberFilters = true;
            m_packageView = 0;
            m_packageSortMode = 0;
            m_packageSnoozeDays = 7;
            m_packageSearchText.clear();
            m_packageSearchMode = winforge::core::packages::PackageSearchMode::Both;
            m_packageSearchCaseSensitiveValue = false;
            m_packageSearchIgnoreSpecialValue = false;
            m_packageDiscoverRegexEnabled = false;
            m_packageDiscoverRegexMultiline = false;
            m_packageDiscoverRegexDotMatchesNewline = false;
            m_packageDiscoverRegexPattern.clear();
            m_packageDiscoverRegexDiagnostic.clear();
            ClearPackageSelection();
            m_packageIgnoredRules.clear();
            m_packagePinnedRules.clear();
            m_packageSnoozedRules.clear();
            m_packageManagersSelected.clear();
            for (auto const& manager : winforge::core::packages::PackageManagers())
            {
                m_packageManagersSelected.emplace(std::wstring(manager.key), true);
            }

            if (m_packageViewPicker)
            {
                m_packageViewPicker.SelectedIndex(m_packageView);
            }
            if (m_packageSearchBox)
            {
                m_packageSearchBox.PlaceholderText(ToHString(winforge::core::LocalizedText{
                    L"Search packages (for example: vscode, vlc, obs)",
                    L"搜尋套件（例如 vscode、vlc、obs）" }.Pick(m_language)));
                m_packageSearchBox.Text(ToHString(m_packageSearchText));
            }
            if (m_packageSortPicker)
            {
                m_packageSortPicker.SelectedIndex(m_packageSortMode);
            }
            if (m_packageSearchModePicker)
            {
                m_packageSearchModePicker.SelectedIndex(static_cast<int32_t>(m_packageSearchMode));
            }
            if (m_packageSearchCaseSensitive)
            {
                m_packageSearchCaseSensitive.IsOn(m_packageSearchCaseSensitiveValue);
            }
            if (m_packageSearchIgnoreSpecial)
            {
                m_packageSearchIgnoreSpecial.IsOn(m_packageSearchIgnoreSpecialValue);
            }
            if (m_packageDiscoverRegexMode)
            {
                m_packageDiscoverRegexMode.IsOn(m_packageDiscoverRegexEnabled);
            }
            if (m_packageManagerFilters)
            {
                PopulatePackageManagerFilters(m_packageManagerFilters);
            }

            SavePackageManagerState();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"Package-manager state reset to defaults.",
                L"套件管理狀態已重設做預設值。");
        }
        catch (...)
        {
        }
        m_packageStateApplying = false;
    }

    void MainWindow::ApplyPackageSort()
    {
        winforge::core::packages::SortPackageItems(
            m_packageItems,
            static_cast<winforge::core::packages::PackageSortMode>(m_packageSortMode));
    }

    void MainWindow::RecordPackageOperation(std::wstring message)
    {
        RecordPackageOperation(
            winforge::core::LocalizedText{ L"Operation event", L"操作事件" }.Pick(m_language),
            std::move(message),
            winforge::core::LocalizedText{ L"Queued preview", L"已排隊預覽" }.Pick(m_language));
    }

    void MainWindow::RecordPackageOperation(
        std::wstring title,
        std::wstring details,
        std::wstring status)
    {
        PackageOperationEntry entry;
        entry.id = L"op-" + std::to_wstring(NowUnixSeconds()) + L"-" +
            std::to_wstring(m_packageOperationSequence++);
        // The operation pane is persisted. Keep it a bounded, redacted
        // lifecycle ledger even when a legacy preview or a malformed cached
        // package row supplied text outside the mutation coordinator.
        entry.title = winforge::core::packages::RedactPackageMutationText(title);
        entry.details = winforge::core::packages::RedactPackageMutationText(details);
        entry.status = winforge::core::packages::RedactPackageMutationText(status);
        entry.created_epoch_seconds = NowUnixSeconds();
        m_packageOperations.insert(
            m_packageOperations.begin(),
            std::move(entry));
        if (m_packageOperations.size() > 50)
        {
            m_packageOperations.resize(50);
        }
        SavePackageManagerState();
    }

    void MainWindow::MovePackageOperation(std::size_t index, bool runNext)
    {
        if (index >= m_packageOperations.size())
        {
            return;
        }
        auto entry = std::move(m_packageOperations[index]);
        m_packageOperations.erase(m_packageOperations.begin() + static_cast<std::ptrdiff_t>(index));
        entry.status = runNext
            ? winforge::core::LocalizedText{ L"Queued next preview", L"排下一個預覽" }.Pick(m_language)
            : winforge::core::LocalizedText{ L"Queued last preview", L"排最後預覽" }.Pick(m_language);
        if (runNext)
        {
            m_packageOperations.insert(m_packageOperations.begin(), std::move(entry));
        }
        else
        {
            m_packageOperations.push_back(std::move(entry));
        }
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            runNext
                ? L"Preview operation moved to the front of the native queue."
                : L"Preview operation moved to the end of the native queue.",
            runNext
                ? L"預覽操作已移到原生佇列最前。"
                : L"預覽操作已移到原生佇列最後。");
    }

    void MainWindow::RetryPackageOperation(std::size_t index)
    {
        if (index >= m_packageOperations.size())
        {
            return;
        }
        auto entry = std::move(m_packageOperations[index]);
        m_packageOperations.erase(m_packageOperations.begin() + static_cast<std::ptrdiff_t>(index));
        ++entry.retry_count;
        entry.status = winforge::core::LocalizedText{
            L"Retry requested; still preview-only",
            L"已要求重試；仍然只係預覽" }.Pick(m_language);
        m_packageOperations.insert(m_packageOperations.begin(), std::move(entry));
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Preview operation retry was queued without executing a package command.",
            L"預覽操作重試已排隊；冇執行套件指令。");
    }

    void MainWindow::ClearPackageOperationLog()
    {
        m_packageOperations.clear();
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Package operation history cleared.",
            L"套件操作歷史已清除。");
    }

    void MainWindow::PreviewPackageOperation(
        winforge::core::packages::PackageItem const& package,
        winforge::core::packages::PackageAction action)
    {
        auto const actionLabel = action == winforge::core::packages::PackageAction::Install ? std::wstring(L"install")
            : action == winforge::core::packages::PackageAction::Update ? std::wstring(L"update")
            : std::wstring(L"uninstall");
        auto const localizedAction = action == winforge::core::packages::PackageAction::Install
            ? winforge::core::LocalizedText{ L"install", L"安裝" }.Pick(m_language)
            : action == winforge::core::packages::PackageAction::Update
                ? winforge::core::LocalizedText{ L"update", L"更新" }.Pick(m_language)
                : winforge::core::LocalizedText{ L"uninstall", L"解除安裝" }.Pick(m_language);

        auto const command = winforge::core::packages::BuildPackageActionCommand(
            package.manager_key,
            package.id,
            package.source,
            action);
        if (!command)
        {
            RecordPackageOperation(
                L"Preview failed for " + actionLabel + L" " + package.id +
                    L" via " + package.manager_key + L": " + command.error_code +
                    L". · 操作預覽失敗。");
            AnnouncePackageStatus(
                L"Package operation preview failed. Review the Operations view for the validation reason.",
                L"套件操作預覽失敗。請喺操作檢視睇驗證原因。",
                true);
        }
        else
        {
            RecordPackageOperation(
                L"Preview-only " + actionLabel + L" plan for " + package.id +
                    L" via " + package.manager_key + L": " +
                    winforge::core::packages::FormatCommandPreview(*command.command) +
                    L". No package command was executed. · 只建立操作預覽，冇執行套件指令。");
            auto const statusEn = std::wstring(L"Package ") + actionLabel +
                L" preview added to Operations. No package command was executed.";
            auto const statusZh = std::wstring(L"套件") + localizedAction +
                L"預覽已加入操作檢視；冇執行套件指令。";
            AnnouncePackageStatus(
                statusEn,
                statusZh);
        }

        m_packageView = 8;
        if (m_packageViewPicker)
        {
            m_packageViewPicker.SelectedIndex(m_packageView);
        }
        SavePackageManagerState();
        RenderPackageManagerView();
    }

    void MainWindow::RequestPackageMutation(
        winforge::core::packages::PackageItem const& package,
        winforge::core::packages::PackageAction action)
    {
        auto const actionEn = action == winforge::core::packages::PackageAction::Install
            ? std::wstring(L"install")
            : action == winforge::core::packages::PackageAction::Update
                ? std::wstring(L"update")
                : std::wstring(L"uninstall");
        auto const actionZh = action == winforge::core::packages::PackageAction::Install
            ? std::wstring(L"安裝")
            : action == winforge::core::packages::PackageAction::Update
                ? std::wstring(L"更新")
                : std::wstring(L"解除安裝");

        winforge::core::packages::PackageMutationRequest request;
        request.id = L"mutation-" + std::to_wstring(NowUnixSeconds()) + L"-" +
            std::to_wstring(m_packageOperationSequence++);
        request.package = package;
        request.action = action;
        auto submission = m_packageMutationCoordinator.Submit(std::move(request));

        m_packageView = 8;
        if (m_packageViewPicker)
        {
            m_packageViewPicker.SelectedIndex(m_packageView);
        }

        if (submission.accepted)
        {
            AnnouncePackageStatus(
                L"Reviewed " + actionEn + L" plan added. Open its Operations card and explicitly confirm before a package command can be queued.",
                L"已加入已檢視嘅" + actionZh + L"計劃。請喺操作卡明確確認，套件指令先可以排隊。" );
        }
        else if (submission.duplicate)
        {
            AnnouncePackageStatus(
                L"A matching package mutation is already awaiting consent, queued, or running.",
                L"相同嘅套件修改已經等緊確認、排隊中或者執行中。",
                true);
        }
        else
        {
            RecordPackageOperation(
                L"Native " + actionEn + L" request rejected for " + package.id + L" via " +
                package.manager_key + L": " + submission.record.diagnostic +
                L". No package command was queued. · 原生操作要求已被拒絕，冇排隊指令。");
            AnnouncePackageStatus(
                L"The package operation was rejected before consent or process execution. Review Operations for the validation reason.",
                L"套件操作喺確認或者 process 執行之前已被拒絕。請喺操作檢視睇驗證原因。",
                true);
        }
        RenderPackageManagerView();
    }

    void MainWindow::ReviewPackageMutationBatch(
        std::vector<winforge::core::packages::PackageItem> packages,
        winforge::core::packages::PackageAction action,
        std::wstring sourceLabelEn,
        std::wstring sourceLabelZh)
    {
        auto const actionEn = action == winforge::core::packages::PackageAction::Install
            ? std::wstring(L"install")
            : action == winforge::core::packages::PackageAction::Update
                ? std::wstring(L"update")
                : std::wstring(L"uninstall");
        auto const actionZh = action == winforge::core::packages::PackageAction::Install
            ? std::wstring(L"安裝")
            : action == winforge::core::packages::PackageAction::Update
                ? std::wstring(L"更新")
                : std::wstring(L"解除安裝");

        if (packages.empty())
        {
            AnnouncePackageStatus(
                L"No eligible cached package rows are available for batch review.",
                L"冇合資格嘅快取套件資料列可以做批次檢視。",
                true);
            return;
        }

        // A batch id is intentionally opaque and local-only. Every child keeps
        // its own unique worker id, while this shared id is the only handle
        // exposed to the batch confirmation, cancellation, and retry controls.
        auto const batchId = L"batch-" + std::to_wstring(NowUnixSeconds()) + L"-" +
            std::to_wstring(m_packageOperationSequence++);
        winforge::core::packages::PackageMutationBatchRequest request;
        request.id = batchId;
        request.requests.reserve(packages.size());
        for (std::size_t index = 0; index < packages.size(); ++index)
        {
            winforge::core::packages::PackageMutationRequest child;
            child.id = batchId + L"-item-" + std::to_wstring(index + 1);
            child.package = std::move(packages[index]);
            child.action = action;
            request.requests.push_back(std::move(child));
        }

        auto submission = m_packageMutationCoordinator.SubmitBatch(std::move(request));

        if (submission.accepted)
        {
            m_packageView = 8;
            if (m_packageViewPicker)
            {
                m_packageViewPicker.SelectedIndex(m_packageView);
            }
            SavePackageManagerState();
            ClearPackageSelection();
            AnnouncePackageStatus(
                L"Reviewed " + std::to_wstring(submission.batch.records.size()) + L" cached " +
                    sourceLabelEn + L" " + actionEn +
                    L" command(s). Inspect every redacted argv on the batch card, then use its one explicit Confirm batch execution control to queue them serially.",
                L"已檢視 " + std::to_wstring(submission.batch.records.size()) + L" 個快取" +
                    sourceLabelZh + actionZh +
                    L" 指令。請先喺批次卡檢查每個已遮蔽 argv，再用唯一嘅「確認批次執行」控制串行排隊。");
        }
        else if (submission.duplicate)
        {
            AnnouncePackageStatus(
                L"The requested batch overlaps a reviewed, queued, or running package mutation, so no partial batch was retained.",
                L"要求嘅批次同已檢視、已排隊或者執行中嘅套件修改重疊，所以冇保留任何部分批次。",
                true);
        }
        else
        {
            // Batch validation is all-or-nothing. The coordinator intentionally
            // returns a bounded policy key rather than reflecting arbitrary
            // cached metadata or external process text into durable history.
            RecordPackageOperation(
                L"Native " + actionEn + L" batch review rejected: " + submission.batch.diagnostic +
                L". No package command was queued. · 原生批次" + actionZh +
                L"檢視已拒絕；冇排隊套件指令。");
            if (submission.batch.diagnostic == L"batch-review-capacity-exceeded")
            {
                AnnouncePackageStatus(
                    L"A reviewed batch may contain at most 25 commands. The selection remains unchanged; narrow it and review again so no prefix is silently staged.",
                    L"已檢視批次最多可以有 25 個指令。選擇保持不變；請縮窄之後再檢視，絕對唔會靜靜地暫存頭一部分。",
                    true);
            }
            else
            {
                AnnouncePackageStatus(
                    L"The package batch was rejected before consent or process execution. Review Operations for the bounded validation reason.",
                    L"套件批次喺確認或者 process 執行之前已被拒絕。請喺操作檢視睇有界驗證原因。",
                    true);
            }
        }
        RenderPackageManagerView();
    }

    void MainWindow::ConfirmPackageMutation(std::wstring id)
    {
        if (!m_packageMutationCoordinator.Confirm(id))
        {
            AnnouncePackageStatus(
                L"This package operation is no longer awaiting consent.",
                L"呢個套件操作已經唔係等緊確認。",
                true);
            RenderPackageManagerView();
            return;
        }
        AnnouncePackageStatus(
            L"Explicit consent recorded. The package command is queued for the serial native worker.",
            L"已記錄明確確認。套件指令已排入原生串行 worker。" );
        RenderPackageManagerView();
        StartNextPackageMutation();
    }

    void MainWindow::ConfirmPackageMutationBatch(std::wstring id)
    {
        if (!m_packageMutationCoordinator.ConfirmBatch(id))
        {
            AnnouncePackageStatus(
                L"This reviewed package batch is no longer awaiting one explicit batch confirmation.",
                L"呢個已檢視套件批次已經唔係等緊一次明確批次確認。",
                true);
            RenderPackageManagerView();
            return;
        }
        AnnouncePackageStatus(
            L"Explicit batch consent recorded. Every reviewed command is queued in its displayed order for the serial native worker.",
            L"已記錄明確批次確認。每個已檢視指令會按顯示次序排入原生串行 worker。");
        RenderPackageManagerView();
        StartNextPackageMutation();
    }

    void MainWindow::CancelPackageMutation(std::wstring id)
    {
        if (!m_packageMutationCoordinator.Cancel(id))
        {
            AnnouncePackageStatus(
                L"This package operation can no longer be cancelled.",
                L"呢個套件操作已經唔可以取消。",
                true);
            RenderPackageManagerView();
            return;
        }
        AnnouncePackageStatus(
            L"Package operation cancellation was requested. A running process receives a contained stop request.",
            L"已要求取消套件操作。執行中嘅 process 會收到受控停止要求。" );
        RenderPackageManagerView();
        StartNextPackageMutation();
    }

    void MainWindow::CancelPackageMutationBatch(std::wstring id)
    {
        if (!m_packageMutationCoordinator.CancelBatch(id))
        {
            AnnouncePackageStatus(
                L"This package batch can no longer be cancelled.",
                L"呢個套件批次已經唔可以取消。",
                true);
            RenderPackageManagerView();
            return;
        }
        AnnouncePackageStatus(
            L"Batch cancellation was requested. The active command receives a contained stop request and all remaining commands are cancelled before execution.",
            L"已要求取消批次。執行中指令會收到受控停止要求，所有其餘指令會喺執行之前取消。");
        RenderPackageManagerView();
        StartNextPackageMutation();
    }

    void MainWindow::RetryPackageMutation(std::wstring id)
    {
        if (!m_packageMutationCoordinator.Retry(id))
        {
            AnnouncePackageStatus(
                L"Only completed, failed, timed-out, or cancelled package operations can request a retry.",
                L"只有完成、失敗、超時或者已取消嘅套件操作先可以要求重試。",
                true);
            RenderPackageManagerView();
            return;
        }
        AnnouncePackageStatus(
            L"Retry prepared. Fresh explicit consent is required before it can run.",
            L"已準備重試；執行之前需要重新明確確認。" );
        RenderPackageManagerView();
    }

    void MainWindow::RetryPackageMutationBatch(std::wstring id)
    {
        if (!m_packageMutationCoordinator.RetryBatch(id))
        {
            AnnouncePackageStatus(
                L"This package batch has no failed, timed-out, or cancelled command eligible for retry.",
                L"呢個套件批次冇失敗、超時或者已取消而可重試嘅指令。",
                true);
            RenderPackageManagerView();
            return;
        }
        AnnouncePackageStatus(
            L"Only unsuccessful package commands returned to batch review. Successful commands will not replay; fresh explicit batch consent is required.",
            L"只有未成功嘅套件指令會返回批次檢視。成功指令唔會重播，而且需要重新明確批次確認。 ");
        RenderPackageManagerView();
    }

    void MainWindow::StartNextPackageMutation()
    {
        if (m_packageMutationWorkerRunning.load() || !m_packageMutationCoordinator.HasRunnableWork())
        {
            return;
        }

        m_packageMutationWorkerRunning.store(true);
        try
        {
            auto lifetime = get_strong();
            auto dispatcher = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
            std::thread worker([lifetime, dispatcher]() mutable
            {
                std::optional<winforge::core::packages::PackageMutationRecord> completed;
                bool workerFailed = false;
                try
                {
                    completed = lifetime->m_packageMutationCoordinator.RunNext(
                        [](winforge::core::packages::PackageMutationRequest const& request,
                            std::stop_token cancellationToken)
                        {
                            winforge::core::packages::PackageRuntimeOptions options;
                            options.timeout = std::chrono::minutes(5);
                            options.cancellation_token = cancellationToken;
                            return winforge::core::packages::RunPackageMutation(
                                request.package,
                                request.action,
                                request.install_options,
                                options);
                        },
                        [lifetime, dispatcher](winforge::core::packages::PackageMutationRecord const&)
                        {
                            try
                            {
                                if (dispatcher)
                                {
                                    static_cast<void>(dispatcher.TryEnqueue(
                                        [lifetime]()
                                        {
                                            if (lifetime->m_currentRoute == L"module.packages")
                                            {
                                                lifetime->RenderPackageManagerView();
                                            }
                                        }));
                                }
                            }
                            catch (...)
                            {
                                // A render notification is best-effort only;
                                // mutation containment remains independent.
                            }
                        });
                }
                catch (...)
                {
                    workerFailed = true;
                    static_cast<void>(lifetime->m_packageMutationCoordinator.CancelAll());
                }

                bool completionQueued = false;
                try
                {
                    completionQueued = dispatcher && dispatcher.TryEnqueue(
                        [lifetime, completed = std::move(completed), workerFailed]() mutable
                        {
                            lifetime->m_packageMutationWorkerRunning.store(false);
                            if (workerFailed)
                            {
                                lifetime->RecordPackageOperation(
                                    L"Native package mutation worker failed closed; pending work was cancelled. · 原生套件修改 worker 已 fail closed；等候工作已取消。");
                                if (lifetime->m_currentRoute == L"module.packages")
                                {
                                    lifetime->AnnouncePackageStatus(
                                        L"The native package mutation worker failed closed and cancelled pending work.",
                                        L"原生套件修改 worker 已 fail closed，並取消等候工作。",
                                        true);
                                    lifetime->RenderPackageManagerView();
                                }
                                return;
                            }

                            if (completed)
                            {
                                auto const actionEn = completed->request.action == winforge::core::packages::PackageAction::Install
                                    ? std::wstring(L"install")
                                    : completed->request.action == winforge::core::packages::PackageAction::Update
                                        ? std::wstring(L"update")
                                        : std::wstring(L"uninstall");
                                auto const actionZh = completed->request.action == winforge::core::packages::PackageAction::Install
                                    ? std::wstring(L"安裝")
                                    : completed->request.action == winforge::core::packages::PackageAction::Update
                                        ? std::wstring(L"更新")
                                        : std::wstring(L"解除安裝");
                                auto const state = std::wstring(winforge::core::packages::PackageMutationStateKey(completed->state));
                                lifetime->RecordPackageOperation(
                                    L"Native " + actionEn + L" " + state + L" for " +
                                    completed->request.package.id + L" via " +
                                    completed->request.package.manager_key + L": " +
                                    completed->diagnostic +
                                    L". · 原生" + actionZh + L"操作狀態：" + state + L"。");
                                if (lifetime->m_currentRoute == L"module.packages")
                                {
                                    lifetime->AnnouncePackageStatus(
                                        L"Package " + actionEn + L" finished with state " + state + L". Third-party output was withheld.",
                                        L"套件" + actionZh + L"已完成，狀態係 " + state + L"。第三方輸出唔會保留。",
                                        completed->state != winforge::core::packages::PackageMutationState::Succeeded);
                                    lifetime->RenderPackageManagerView();
                                }
                            }
                            lifetime->StartNextPackageMutation();
                        });
                }
                catch (...)
                {
                    completionQueued = false;
                }

                if (!completionQueued)
                {
                    // Dispatcher shutdown means the page can no longer surface
                    // completion. Stop anything still queued rather than letting
                    // a confirmed mutation continue after its host disappears.
                    lifetime->m_packageMutationWorkerRunning.store(false);
                    static_cast<void>(lifetime->m_packageMutationCoordinator.CancelAll());
                }
            });
            worker.detach();
        }
        catch (std::exception const&)
        {
            m_packageMutationWorkerRunning.store(false);
            static_cast<void>(m_packageMutationCoordinator.CancelAll());
            RecordPackageOperation(
                L"Could not start native package mutation worker; pending work was cancelled. · 無法啟動原生套件修改 worker；等候工作已取消。");
            AnnouncePackageStatus(
                L"Could not start the native package mutation worker; pending work was cancelled.",
                L"無法啟動原生套件修改 worker；等候工作已取消。",
                true);
            RenderPackageManagerView();
        }
        catch (...)
        {
            m_packageMutationWorkerRunning.store(false);
            static_cast<void>(m_packageMutationCoordinator.CancelAll());
            RecordPackageOperation(
                L"Could not start native package mutation worker; pending work was cancelled. · 無法啟動原生套件修改 worker；等候工作已取消。");
            AnnouncePackageStatus(
                L"Could not start the native package mutation worker; pending work was cancelled.",
                L"無法啟動原生套件修改 worker；等候工作已取消。",
                true);
            RenderPackageManagerView();
        }
    }

    std::optional<winforge::core::packages::PackageAction> MainWindow::CurrentPackageSelectionAction() const
    {
        using winforge::core::packages::PackageAction;
        switch (m_packageView)
        {
        case 0:
            if (m_packageLastAction == PackageAction::Search) return PackageAction::Install;
            break;
        case 1:
            if (m_packageLastAction == PackageAction::Updates) return PackageAction::Update;
            break;
        case 2:
            if (m_packageLastAction == PackageAction::Installed) return PackageAction::Uninstall;
            break;
        default:
            break;
        }
        return std::nullopt;
    }

    void MainWindow::SetPackageSelected(
        winforge::core::packages::PackageItem const& package,
        winforge::core::packages::PackageAction action,
        bool selected)
    {
        // Cached selection is deliberately transient. Query and view changes
        // clear it, and the action must still match the current cached result
        // set before a late checkbox event can change it.
        auto const currentAction = CurrentPackageSelectionAction();
        if (!currentAction || *currentAction != action)
        {
            return;
        }

        auto const key = winforge::core::packages::PackageSelectionKey(package, action);
        if (selected)
        {
            m_packageSelectedKeys.insert(key);
        }
        else
        {
            m_packageSelectedKeys.erase(key);
        }
        RenderPackageManagerView();
    }

    void MainWindow::ClearPackageSelection()
    {
        m_packageSelectedKeys.clear();
    }

    std::vector<winforge::core::packages::PackageItem> MainWindow::SelectedPackageItems(
        winforge::core::packages::PackageAction action) const
    {
        std::vector<winforge::core::packages::PackageItem> selected;
        auto const currentAction = CurrentPackageSelectionAction();
        if (!currentAction || *currentAction != action)
        {
            return selected;
        }

        selected.reserve(m_packageSelectedKeys.size());
        std::unordered_set<std::wstring> seen;
        for (auto const& package : m_packageItems)
        {
            auto const key = winforge::core::packages::PackageSelectionKey(package, action);
            if (m_packageSelectedKeys.contains(key) && seen.insert(key).second)
            {
                selected.push_back(package);
            }
        }
        return selected;
    }

    void MainWindow::PreviewSelectedPackageOperations(
        winforge::core::packages::PackageAction action)
    {
        auto const currentAction = CurrentPackageSelectionAction();
        if (!currentAction || *currentAction != action)
        {
            ClearPackageSelection();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"The selected package batch review is no longer available because its cached result view changed.",
                L"已揀套件批次檢視已經唔可用，因為快取結果檢視已經改變。",
                true);
            return;
        }

        auto const sourceViewEn = action == winforge::core::packages::PackageAction::Install
            ? std::wstring(L"Discover")
            : action == winforge::core::packages::PackageAction::Update
                ? std::wstring(L"Updates")
                : std::wstring(L"Installed");
        auto const sourceViewZh = action == winforge::core::packages::PackageAction::Install
            ? std::wstring(L"Discover")
            : action == winforge::core::packages::PackageAction::Update
                ? std::wstring(L"更新")
                : std::wstring(L"已安裝");

        auto const selected = SelectedPackageItems(action);
        if (selected.empty())
        {
            ClearPackageSelection();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"No current " + sourceViewEn + L" rows are selected. Choose one or more cached results first.",
                L"而家冇已揀嘅 " + sourceViewZh + L" 資料列；請先揀一個或者多個已快取結果。",
                true);
            return;
        }

        // SubmitBatch validates the entire cached selection before retaining a
        // single command. In particular it refuses a selection over the
        // visible 25-command capacity instead of silently staging a prefix.
        ReviewPackageMutationBatch(
            std::move(selected),
            action,
            sourceViewEn,
            sourceViewZh);
    }

    void MainWindow::AddSelectedPackagesToBundle()
    {
        auto const action = CurrentPackageSelectionAction();
        if (!action)
        {
            ClearPackageSelection();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"The selected package rows are no longer available because their cached result view changed.",
                L"已揀套件資料列已經唔可用，因為快取結果檢視已經改變。",
                true);
            return;
        }

        // UniGetUI exposes selected-row bundle append from Discover and
        // Installed. Updates remains preview-only until a safe native update
        // bundle contract exists.
        if (*action != winforge::core::packages::PackageAction::Install &&
            *action != winforge::core::packages::PackageAction::Uninstall)
        {
            AnnouncePackageStatus(
                L"Only Discover and Installed selections can be added to a native bundle workspace.",
                L"只可以將 Discover 同已安裝嘅選擇加入原生 bundle 工作區。",
                true);
            return;
        }

        auto const selected = SelectedPackageItems(*action);
        if (selected.empty())
        {
            ClearPackageSelection();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"No current cached package rows are selected. Choose one or more results first.",
                L"而家冇已揀嘅快取套件資料列；請先揀一個或者多個結果。",
                true);
            return;
        }

        std::vector<winforge::core::packages::PackageItem> boundedSelected;
        boundedSelected.reserve(selected.size());
        std::size_t rejected = 0;
        for (auto const& item : selected)
        {
            if (IsBundleItemWithinLimits(item))
            {
                boundedSelected.push_back(item);
            }
            else
            {
                ++rejected;
            }
        }
        if (boundedSelected.empty())
        {
            ClearPackageSelection();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"The selected package metadata exceeds native bundle field limits and was not added.",
                L"已揀套件 metadata 超過原生 bundle 欄位上限，所以冇加入。",
                true);
            return;
        }

        auto current = winforge::core::packages::NormalizePackageBundleSnapshot({
            m_packageBundleItems,
            m_packageBundleIncompatibleItems });
        auto const existing = BundleRecordCount(current);
        if (existing >= MaximumNativeBundleRecords)
        {
            ClearPackageSelection();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"The native bundle workspace has reached its 2,048-record limit.",
                L"原生 bundle 工作區已達到 2,048 筆記錄上限。",
                true);
            return;
        }
        auto const remaining = MaximumNativeBundleRecords - existing;
        if (boundedSelected.size() > remaining)
        {
            rejected += boundedSelected.size() - remaining;
            boundedSelected.resize(remaining);
        }
        auto compatibleItems = winforge::core::packages::MergePackageBundleItems(
            current.packages,
            boundedSelected);
        auto snapshot = winforge::core::packages::NormalizePackageBundleSnapshot({
            std::move(compatibleItems),
            current.incompatible_packages });
        auto const before = current.packages.size() + current.incompatible_packages.size();
        auto const after = snapshot.packages.size() + snapshot.incompatible_packages.size();
        auto const added = after - before;
        m_packageBundleItems = snapshot.packages;
        m_packageBundleIncompatibleItems = snapshot.incompatible_packages;
        if (added != 0)
        {
            // Adding cached rows changes any previously imported/saved
            // workspace; make the unsaved state explicit rather than implying
            // the old file was modified on disk.
            m_packageBundleSourcePath.clear();
            m_packageBundleImportNote.clear();
            m_packageBundleDirty = true;
        }
        ClearPackageSelection();
        m_packageView = static_cast<int32_t>(winforge::core::packages::PackageView::Bundles);
        if (m_packageViewPicker)
        {
            m_packageStateApplying = true;
            m_packageViewPicker.SelectedIndex(m_packageView);
            m_packageStateApplying = false;
        }
        SavePackageManagerState();
        RenderPackageManagerView();

        if (added != 0)
        {
            RecordPackageOperation(
                L"Added " + std::to_wstring(added) + L" selected cached package(s) to the native bundle workspace. No package command was executed. · 已加入所選快取套件到原生 bundle 工作區，冇執行套件指令。");
            AnnouncePackageStatus(
                rejected == 0
                    ? L"Selected packages were added to the native bundle workspace. No package command was executed."
                    : L"Selected packages were added, but some rows exceeded native bundle limits and were not added. No package command was executed.",
                rejected == 0
                    ? L"已揀套件已加入原生 bundle 工作區；冇執行套件指令。"
                    : L"已揀套件已加入，但有部分資料列超過原生 bundle 上限，所以冇加入；冇執行套件指令。",
                rejected != 0);
        }
        else
        {
            AnnouncePackageStatus(
                L"Every selected package is already in the native bundle workspace. No package command was executed.",
                L"每個已揀套件都已經喺原生 bundle 工作區；冇執行套件指令。");
        }
    }

    void MainWindow::PreviewPackageDetails(
        winforge::core::packages::PackageItem const& package)
    {
        auto const command = winforge::core::packages::BuildDetailsCommand(
            package.manager_key,
            package.id);
        if (!command)
        {
            RecordPackageOperation(
                L"Preview failed for details " + package.id +
                    L" via " + package.manager_key + L": " + command.error_code +
                    L". · 詳細資料預覽失敗。");
            AnnouncePackageStatus(
                L"Package details preview failed. Review the Operations view for the validation reason.",
                L"套件詳細資料預覽失敗。請喺操作檢視睇驗證原因。",
                true);
        }
        else
        {
            RecordPackageOperation(
                L"Preview-only details plan for " + package.id +
                    L" via " + package.manager_key + L": " +
                    winforge::core::packages::FormatCommandPreview(*command.command) +
                    L". No package command was executed. · 只建立詳細資料預覽，冇執行套件指令。");
            AnnouncePackageStatus(
                L"Package details preview added to Operations. No package command was executed.",
                L"套件詳細資料預覽已加入操作檢視；冇執行套件指令。");
        }

        m_packageView = 8;
        if (m_packageViewPicker)
        {
            m_packageViewPicker.SelectedIndex(m_packageView);
        }
        SavePackageManagerState();
        RenderPackageManagerView();
    }

    void MainWindow::StartPackageDetailsQuery(
        winforge::core::packages::PackageItem const& package)
    {
        using winforge::core::packages::PackageAction;

        auto const available = m_packageManagersAvailable.find(package.manager_key);
        if (available == m_packageManagersAvailable.end() || !available->second)
        {
            RecordPackageOperation(
                L"Details query was not started for " + package.id +
                    L" via " + package.manager_key +
                    L": manager is not available in the current native probe. · 詳細資料查詢未開始。");
            AnnouncePackageStatus(
                L"Package details query was not started because that manager is not available.",
                L"套件詳細資料查詢未開始，因為該管理器目前未可用。",
                true);
            m_packageView = 8;
            if (m_packageViewPicker)
            {
                m_packageViewPicker.SelectedIndex(m_packageView);
            }
            SavePackageManagerState();
            RenderPackageManagerView();
            return;
        }

        auto const command = winforge::core::packages::BuildDetailsCommand(
            package.manager_key,
            package.id);
        if (!command)
        {
            RecordPackageOperation(
                L"Details query validation failed for " + package.id +
                    L" via " + package.manager_key + L": " + command.error_code +
                    L". · 詳細資料查詢驗證失敗。");
            AnnouncePackageStatus(
                L"Package details query failed validation before any command started.",
                L"套件詳細資料查詢喺任何指令開始之前驗證失敗。",
                true);
            m_packageView = 8;
            if (m_packageViewPicker)
            {
                m_packageViewPicker.SelectedIndex(m_packageView);
            }
            SavePackageManagerState();
            RenderPackageManagerView();
            return;
        }

        CancelPackageWork();
        m_packageItems.clear();
        m_packageRunStates.clear();
        m_packageLastAction = PackageAction::Details;
        m_packageDetailsTarget = package.id;
        m_packageWorking = true;
        if (m_packageBusy) m_packageBusy.IsActive(true);

        RecordPackageOperation(
            L"Started read-only details query for " + package.id +
                L" via " + package.manager_key + L": " +
                winforge::core::packages::FormatCommandPreview(*command.command) +
                L". · 已開始只讀詳細資料查詢。");
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Package details query started. The bounded command output will render when it finishes.",
            L"套件詳細資料查詢已開始；有界指令輸出完成後會顯示。");

        auto const generation = m_packageGeneration;
        QueryPackageManagersAsync(
            generation,
            PackageAction::Details,
            package.id,
            { package.manager_key },
            m_packageStopSource.get_token());
    }

    bool MainWindow::IsPackageIgnored(
        winforge::core::packages::PackageItem const& package) const
    {
        auto const key = PackageRuleKey(package.manager_key, package.id);
        return std::any_of(
            m_packageIgnoredRules.begin(),
            m_packageIgnoredRules.end(),
            [&key](PackageIgnoredRule const& rule)
            {
                return PackageRuleKey(rule.manager_key, rule.package_id) == key;
            });
    }

    bool MainWindow::IsPackagePinned(
        winforge::core::packages::PackageItem const& package) const
    {
        auto const version = PackageUpdateRuleVersion(package);
        if (version.empty())
        {
            return false;
        }
        auto const key = PackageVersionRuleKey(package.manager_key, package.id, version);
        return std::any_of(
            m_packagePinnedRules.begin(),
            m_packagePinnedRules.end(),
            [&key](PackagePinnedRule const& rule)
            {
                return PackageVersionRuleKey(rule.manager_key, rule.package_id, rule.version) == key;
            });
    }

    bool MainWindow::IsPackageSnoozed(
        winforge::core::packages::PackageItem const& package) const
    {
        auto const key = PackageRuleKey(package.manager_key, package.id);
        auto const now = NowUnixSeconds();
        return std::any_of(
            m_packageSnoozedRules.begin(),
            m_packageSnoozedRules.end(),
            [&key, now](PackageSnoozedRule const& rule)
            {
                return rule.until_epoch_seconds > now &&
                    PackageRuleKey(rule.manager_key, rule.package_id) == key;
            });
    }

    bool MainWindow::IsPackageUpdateSuppressed(
        winforge::core::packages::PackageItem const& package) const
    {
        return IsPackageIgnored(package) || IsPackagePinned(package) || IsPackageSnoozed(package);
    }

    void MainWindow::IgnorePackageUpdate(
        winforge::core::packages::PackageItem const& package)
    {
        auto const validation = winforge::core::packages::ValidatePackageReference(
            package.manager_key,
            package.id);
        if (!validation)
        {
            RecordPackageOperation(
                L"Ignore rule was not saved for " + package.id +
                    L" via " + package.manager_key + L": " + validation.code +
                    L". · 忽略規則未保存。");
            AnnouncePackageStatus(
                L"Ignore rule was not saved because the package reference failed validation.",
                L"忽略規則未保存，因為套件參照未通過驗證。",
                true);
            return;
        }

        if (!IsPackageIgnored(package))
        {
            PackageIgnoredRule rule;
            rule.manager_key = package.manager_key;
            rule.package_id = package.id;
            rule.package_name = package.name.empty() ? package.id : package.name;
            rule.version = package.available_version.empty() ? package.version : package.available_version;
            m_packageIgnoredRules.push_back(std::move(rule));
        }

        auto const before = m_packageItems.size();
        m_packageItems.erase(
            std::remove_if(
                m_packageItems.begin(),
                m_packageItems.end(),
                [this](winforge::core::packages::PackageItem const& item)
                {
                    return IsPackageUpdateSuppressed(item);
                }),
            m_packageItems.end());

        RecordPackageOperation(
            L"Saved ignored-update rule for " + package.id +
                L" via " + package.manager_key + L"; hidden " +
                std::to_wstring(before - m_packageItems.size()) +
                L" currently loaded update row(s). · 已保存忽略更新規則。");
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Ignored-update rule saved. Matching update rows are hidden from the current and future Updates results.",
            L"已保存忽略更新規則；相符更新會喺目前同之後嘅更新結果隱藏。");
    }

    void MainWindow::PinPackageUpdate(
        winforge::core::packages::PackageItem const& package)
    {
        auto const validation = winforge::core::packages::ValidatePackageReference(
            package.manager_key,
            package.id);
        auto const version = PackageUpdateRuleVersion(package);
        if (!validation || version.empty())
        {
            RecordPackageOperation(
                L"Pin rule was not saved for " + package.id +
                    L" via " + package.manager_key + L". · 釘選規則未保存。");
            AnnouncePackageStatus(
                L"Version pin was not saved because the package reference or update version was incomplete.",
                L"版本釘選未保存，因為套件參照或者更新版本唔完整。",
                true);
            return;
        }

        if (!IsPackagePinned(package))
        {
            PackagePinnedRule rule;
            rule.manager_key = package.manager_key;
            rule.package_id = package.id;
            rule.package_name = package.name.empty() ? package.id : package.name;
            rule.version = version;
            m_packagePinnedRules.push_back(std::move(rule));
        }

        auto const before = m_packageItems.size();
        m_packageItems.erase(
            std::remove_if(
                m_packageItems.begin(),
                m_packageItems.end(),
                [this](winforge::core::packages::PackageItem const& item)
                {
                    return IsPackageUpdateSuppressed(item);
                }),
            m_packageItems.end());

        RecordPackageOperation(
            L"Pinned update version " + version + L" for " + package.id +
                L" via " + package.manager_key + L"; hidden " +
                std::to_wstring(before - m_packageItems.size()) +
                L" currently loaded update row(s). · 已保存版本釘選。");
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Version pin saved. Matching update-version rows are hidden until the pin is removed.",
            L"已保存版本釘選；移除之前，相符更新版本會被隱藏。");
    }

    void MainWindow::SnoozePackageUpdate(
        winforge::core::packages::PackageItem const& package)
    {
        auto const validation = winforge::core::packages::ValidatePackageReference(
            package.manager_key,
            package.id);
        if (!validation)
        {
            RecordPackageOperation(
                L"Snooze rule was not saved for " + package.id +
                    L" via " + package.manager_key + L": " + validation.code +
                    L". · 暫停規則未保存。");
            AnnouncePackageStatus(
                L"Snooze rule was not saved because the package reference failed validation.",
                L"暫停規則未保存，因為套件參照未通過驗證。",
                true);
            return;
        }

        auto const days = NormalizeSnoozeDays(m_packageSnoozeDays);
        auto const until = NowUnixSeconds() + static_cast<std::int64_t>(days) * 24 * 60 * 60;
        auto const key = PackageRuleKey(package.manager_key, package.id);
        auto found = std::find_if(
            m_packageSnoozedRules.begin(),
            m_packageSnoozedRules.end(),
            [&key](PackageSnoozedRule const& rule)
            {
                return PackageRuleKey(rule.manager_key, rule.package_id) == key;
            });
        if (found == m_packageSnoozedRules.end())
        {
            PackageSnoozedRule rule;
            rule.manager_key = package.manager_key;
            rule.package_id = package.id;
            rule.package_name = package.name.empty() ? package.id : package.name;
            rule.version = PackageUpdateRuleVersion(package);
            rule.until_epoch_seconds = until;
            m_packageSnoozedRules.push_back(std::move(rule));
        }
        else
        {
            found->package_name = package.name.empty() ? package.id : package.name;
            found->version = PackageUpdateRuleVersion(package);
            found->until_epoch_seconds = until;
        }

        auto const before = m_packageItems.size();
        m_packageItems.erase(
            std::remove_if(
                m_packageItems.begin(),
                m_packageItems.end(),
                [this](winforge::core::packages::PackageItem const& item)
                {
                    return IsPackageUpdateSuppressed(item);
                }),
            m_packageItems.end());

        RecordPackageOperation(
            L"Snoozed updates for " + package.id + L" for " +
                std::to_wstring(days) + L" day(s)" +
                L" via " + package.manager_key + L" until " + FormatRuleTime(until) +
                L"; hidden " + std::to_wstring(before - m_packageItems.size()) +
                L" currently loaded update row(s). · 已保存自訂暫停。");
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Update snooze saved. Matching update rows are hidden until the snooze expires or is removed.",
            L"已保存更新暫停；相符更新會隱藏到暫停到期或者被移除。");
    }

    void MainWindow::RemoveIgnoredPackage(std::wstring managerKey, std::wstring packageId)
    {
        auto const key = PackageRuleKey(managerKey, packageId);
        auto const before = m_packageIgnoredRules.size();
        m_packageIgnoredRules.erase(
            std::remove_if(
                m_packageIgnoredRules.begin(),
                m_packageIgnoredRules.end(),
                [&key](PackageIgnoredRule const& rule)
                {
                    return PackageRuleKey(rule.manager_key, rule.package_id) == key;
                }),
            m_packageIgnoredRules.end());
        if (m_packageIgnoredRules.size() != before)
        {
            RecordPackageOperation(
                L"Removed ignored-update rule for " + packageId +
                    L" via " + managerKey + L". · 已移除忽略更新規則。");
            SavePackageManagerState();
        }
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Ignored-update rule removed. Refresh Updates to show matching rows again.",
            L"忽略更新規則已移除；重新整理更新即可再次顯示相符項目。");
    }

    void MainWindow::RemovePinnedPackage(std::wstring managerKey, std::wstring packageId, std::wstring version)
    {
        auto const key = PackageVersionRuleKey(managerKey, packageId, version);
        auto const before = m_packagePinnedRules.size();
        m_packagePinnedRules.erase(
            std::remove_if(
                m_packagePinnedRules.begin(),
                m_packagePinnedRules.end(),
                [&key](PackagePinnedRule const& rule)
                {
                    return PackageVersionRuleKey(rule.manager_key, rule.package_id, rule.version) == key;
                }),
            m_packagePinnedRules.end());
        if (m_packagePinnedRules.size() != before)
        {
            RecordPackageOperation(
                L"Removed pinned update version " + version + L" for " + packageId +
                    L" via " + managerKey + L". · 已移除版本釘選。");
            SavePackageManagerState();
        }
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Version pin removed. Refresh Updates to show matching update-version rows again.",
            L"版本釘選已移除；重新整理更新即可再次顯示相符版本。");
    }

    void MainWindow::RemoveSnoozedPackage(std::wstring managerKey, std::wstring packageId)
    {
        auto const key = PackageRuleKey(managerKey, packageId);
        auto const before = m_packageSnoozedRules.size();
        m_packageSnoozedRules.erase(
            std::remove_if(
                m_packageSnoozedRules.begin(),
                m_packageSnoozedRules.end(),
                [&key](PackageSnoozedRule const& rule)
                {
                    return PackageRuleKey(rule.manager_key, rule.package_id) == key;
                }),
            m_packageSnoozedRules.end());
        if (m_packageSnoozedRules.size() != before)
        {
            RecordPackageOperation(
                L"Removed snoozed-update rule for " + packageId +
                    L" via " + managerKey + L". · 已移除暫停更新規則。");
            SavePackageManagerState();
        }
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Snooze removed. Refresh Updates to show matching rows again.",
            L"暫停已移除；重新整理更新即可再次顯示相符項目。");
    }

    void MainWindow::ClearPackageUpdateRules()
    {
        auto const removed = m_packageIgnoredRules.size() + m_packagePinnedRules.size() + m_packageSnoozedRules.size();
        m_packageIgnoredRules.clear();
        m_packagePinnedRules.clear();
        m_packageSnoozedRules.clear();
        RecordPackageOperation(
            L"Cleared " + std::to_wstring(removed) +
                L" update suppression rule(s). · 已清除更新隱藏規則。");
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Ignored, pinned, and snoozed update rules cleared. Refresh Updates to show matching rows again.",
            L"忽略、釘選同暫停更新規則已清除；重新整理更新即可再次顯示相符項目。");
    }

    void MainWindow::PreviewPackageBulkUpdate()
    {
        if (m_packageItems.empty() ||
            m_packageLastAction != winforge::core::packages::PackageAction::Updates)
        {
            AnnouncePackageStatus(
                L"No current update rows are loaded; refresh Updates before reviewing Update all.",
                L"未載入目前可更新資料列；檢視全部更新之前請先重新整理更新。",
                true);
            return;
        }

        std::vector<winforge::core::packages::PackageItem> eligible;
        eligible.reserve(m_packageItems.size());
        for (auto const& package : m_packageItems)
        {
            if (!IsPackageUpdateSuppressed(package))
            {
                eligible.push_back(package);
            }
        }
        if (eligible.empty())
        {
            AnnouncePackageStatus(
                L"Every cached update row is currently ignored, pinned, or snoozed; no Update all batch was created.",
                L"每個快取更新資料列目前都已忽略、釘住或者暫停；冇建立全部更新批次。",
                true);
            return;
        }

        ReviewPackageMutationBatch(
            std::move(eligible),
            winforge::core::packages::PackageAction::Update,
            L"Updates",
            L"更新");
    }

    std::wstring MainWindow::BundleSnapshotToJson(
        winforge::core::packages::PackageBundleSnapshot const& snapshot) const
    {
        std::wstring json = LR"({"export_version":3,"packages":[)";
        auto appendPackage = [&json](
            winforge::core::packages::PackageItem const& item,
            bool includeManager)
        {
            json += LR"({"Id":")";
            json += EscapeJson(item.id);
            json += LR"(","Name":")";
            json += EscapeJson(item.name);
            json += LR"(","Version":")";
            json += EscapeJson(item.version);
            json += LR"(","Source":")";
            json += EscapeJson(item.source);
            if (includeManager)
            {
                json += LR"(","ManagerName":")";
                json += EscapeJson(item.manager_key);
            }
            json += L"}";
        };

        bool first = true;
        for (auto const& item : snapshot.packages)
        {
            if (!first) json += L',';
            first = false;
            appendPackage(item, true);
        }
        json += LR"(],"incompatible_packages":[)";
        first = true;
        for (auto const& item : snapshot.incompatible_packages)
        {
            if (!first) json += L',';
            first = false;
            // UniGetUI v3 incompatible records are audit-only and omit the
            // installable ManagerName field. Keep the native record inert on
            // export rather than emitting an executable-looking entry.
            appendPackage(item, false);
        }
        json += L"]}";
        return json;
    }

    bool MainWindow::SaveBundleSnapshot(std::wstring_view path) const
    {
        try
        {
            auto const snapshot = winforge::core::packages::NormalizePackageBundleSnapshot({
                m_packageBundleItems,
                m_packageBundleIncompatibleItems });
            if (BundleRecordCount(snapshot) > MaximumNativeBundleRecords)
            {
                return false;
            }
            auto const withinLimits = [](std::vector<winforge::core::packages::PackageItem> const& items)
            {
                return std::all_of(items.begin(), items.end(), IsBundleItemWithinLimits);
            };
            if (!withinLimits(snapshot.packages) || !withinLimits(snapshot.incompatible_packages))
            {
                return false;
            }
            auto const destination = std::filesystem::path(path);
            auto const json = winrt::to_string(BundleSnapshotToJson(snapshot));
            if (json.empty() ||
                json.size() > MaximumNativeBundleBytes ||
                json.size() > static_cast<std::size_t>((std::numeric_limits<DWORD>::max)()))
            {
                return false;
            }

            // Keep the temporary file in the destination directory so the
            // final replacement remains atomic, but create it exclusively
            // from cryptographically random GUID material. This rejects an
            // attacker-created file/reparse point instead of truncating it.
            std::filesystem::path temporary;
            HANDLE temporaryHandle = INVALID_HANDLE_VALUE;
            for (int attempt = 0; attempt != 8; ++attempt)
            {
                temporary = destination;
                temporary += L".tmp-" + winforge::core::guidgen::NewGuid(L"N");
                temporaryHandle = CreateFileW(
                    temporary.c_str(),
                    GENERIC_WRITE,
                    0,
                    nullptr,
                    CREATE_NEW,
                    FILE_ATTRIBUTE_TEMPORARY | FILE_FLAG_WRITE_THROUGH | FILE_FLAG_OPEN_REPARSE_POINT,
                    nullptr);
                if (temporaryHandle != INVALID_HANDLE_VALUE)
                {
                    break;
                }
                auto const error = GetLastError();
                if (error != ERROR_FILE_EXISTS && error != ERROR_ALREADY_EXISTS)
                {
                    return false;
                }
            }
            if (temporaryHandle == INVALID_HANDLE_VALUE)
            {
                return false;
            }

            DWORD written = 0;
            auto const writeSucceeded = WriteFile(
                temporaryHandle,
                json.data(),
                static_cast<DWORD>(json.size()),
                &written,
                nullptr) && written == json.size() && FlushFileBuffers(temporaryHandle);
            auto const closeSucceeded = CloseHandle(temporaryHandle);
            if (!writeSucceeded || !closeSucceeded)
            {
                DeleteFileW(temporary.c_str());
                return false;
            }
            if (!MoveFileExW(
                    temporary.c_str(),
                    destination.c_str(),
                    MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
            {
                DeleteFileW(temporary.c_str());
                return false;
            }
            return true;
        }
        catch (...)
        {
            return false;
        }
    }

    bool MainWindow::ConfirmBundleWorkspaceReplacement() const
    {
        if (!m_packageBundleDirty)
        {
            return true;
        }
        auto const title = winforge::core::LocalizedText{
            L"Replace unsaved bundle workspace?",
            L"取代未儲存嘅 bundle 工作區？" }.Pick(m_language);
        auto const body = winforge::core::LocalizedText{
            L"Importing this bundle will discard the unsaved selected rows in the current native bundle workspace. Continue?",
            L"匯入呢個 bundle 會捨棄目前原生 bundle 工作區未儲存嘅所選資料列。要繼續嗎？" }.Pick(m_language);
        return MessageBoxW(
            nullptr,
            body.c_str(),
            title.c_str(),
            MB_YESNO | MB_ICONWARNING | MB_DEFBUTTON2) == IDYES;
    }

    MainWindow::BundleSnapshotLoadStatus MainWindow::LoadBundleSnapshot(std::wstring_view path)
    {
        try
        {
            auto const sourcePath = std::filesystem::path(path);
            if (!ConfirmBundleWorkspaceReplacement())
            {
                return BundleSnapshotLoadStatus::Cancelled;
            }

            std::ifstream input(sourcePath, std::ios::binary);
            if (!input)
            {
                return BundleSnapshotLoadStatus::Failed;
            }
            // Read from the opened handle with a hard bound instead of
            // trusting pre-open file metadata, which can change before or
            // during the read. One extra byte detects oversized input.
            std::string json(MaximumNativeBundleBytes + 1, '\0');
            input.read(json.data(), static_cast<std::streamsize>(json.size()));
            auto const read = input.gcount();
            if (input.bad() ||
                (input.fail() && !input.eof()) ||
                read <= 0 ||
                static_cast<std::size_t>(read) > MaximumNativeBundleBytes)
            {
                return BundleSnapshotLoadStatus::Failed;
            }
            json.resize(static_cast<std::size_t>(read));

            auto const root = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(json));
            auto const hasPackages = root.HasKey(L"packages");
            auto const hasIncompatible = root.HasKey(L"incompatible_packages");
            if (!hasPackages && !hasIncompatible)
            {
                return BundleSnapshotLoadStatus::Failed;
            }
            auto const packages = hasPackages
                ? root.GetNamedArray(L"packages")
                : winrt::Windows::Data::Json::JsonArray{};
            auto const incompatible = hasIncompatible
                ? root.GetNamedArray(L"incompatible_packages")
                : winrt::Windows::Data::Json::JsonArray{};
            if (packages.Size() > MaximumNativeBundleRecords ||
                incompatible.Size() > MaximumNativeBundleRecords ||
                packages.Size() + incompatible.Size() > MaximumNativeBundleRecords)
            {
                return BundleSnapshotLoadStatus::Failed;
            }

            int version = 3;
            if (root.HasKey(L"export_version"))
            {
                try
                {
                    auto const value = root.GetNamedNumber(L"export_version");
                    if (!std::isfinite(value) || std::trunc(value) != value ||
                        value < static_cast<double>(std::numeric_limits<int>::min()) ||
                        value > static_cast<double>(std::numeric_limits<int>::max()))
                    {
                        version = 0;
                    }
                    else
                    {
                        version = static_cast<int>(value);
                    }
                }
                catch (...)
                {
                    version = 0;
                }
            }

            std::vector<winforge::core::packages::PackageItem> imported;
            std::vector<winforge::core::packages::PackageItem> explicitlyIncompatible;
            imported.reserve(packages.Size());
            explicitlyIncompatible.reserve(incompatible.Size());
            std::size_t dropped = 0;
            std::size_t strippedOptions = 0;
            auto appendEntries = [&dropped, &strippedOptions](
                winrt::Windows::Data::Json::JsonArray const& entries,
                std::vector<winforge::core::packages::PackageItem>& destination,
                bool requiresManager)
            {
                for (auto const& entry : entries)
                {
                    try
                    {
                        auto const object = entry.GetObject();
                        bool bounded = true;
                        auto read = [&object, &bounded](winrt::hstring const& name)
                        {
                            auto value = ToWide(object.GetNamedString(name, L""));
                            if (value.size() > MaximumNativeBundleFieldLength)
                            {
                                bounded = false;
                                return std::wstring{};
                            }
                            return value;
                        };
                        auto const id = read(L"Id");
                        auto const name = read(L"Name");
                        auto const version = read(L"Version");
                        auto const source = read(L"Source");
                        auto const manager = read(L"ManagerName");
                        if (object.HasKey(L"InstallationOptions") || object.HasKey(L"Updates"))
                        {
                            ++strippedOptions;
                        }
                        if (!bounded)
                        {
                            ++dropped;
                            continue;
                        }
                        if (id.empty() || (requiresManager && manager.empty()))
                        {
                            ++dropped;
                            continue;
                        }
                        destination.push_back({ name, id, version, {}, source, manager });
                    }
                    catch (...)
                    {
                        ++dropped;
                    }
                }
            };
            appendEntries(packages, imported, true);
            appendEntries(incompatible, explicitlyIncompatible, false);
            if (imported.empty() && explicitlyIncompatible.empty() && dropped != 0)
            {
                return BundleSnapshotLoadStatus::Failed;
            }

            auto snapshot = winforge::core::packages::NormalizePackageBundleSnapshot({
                std::move(imported),
                std::move(explicitlyIncompatible) });
            m_packageBundleItems = snapshot.packages;
            m_packageBundleIncompatibleItems = snapshot.incompatible_packages;
            m_packageBundleSourcePath = std::wstring(path);
            m_packageBundleDirty = false;
            m_packageBundleImportNote.clear();
            if (version != 3 || strippedOptions != 0 || dropped != 0)
            {
                m_packageBundleImportNote =
                    L"Import safety: metadata only; no package command was executed.";
                if (version != 3)
                {
                    m_packageBundleImportNote += L" Schema version " + std::to_wstring(version) + L" differs from v3.";
                }
                if (strippedOptions != 0)
                {
                    m_packageBundleImportNote += L" Stripped option metadata from " +
                        std::to_wstring(strippedOptions) + L" record(s).";
                }
                if (dropped != 0)
                {
                    m_packageBundleImportNote += L" Rejected " + std::to_wstring(dropped) +
                        L" oversized or malformed record(s).";
                }
                m_packageBundleImportNote +=
                    L" · 匯入安全：只保留 metadata，冇執行套件指令。";
            }
            m_packageView = static_cast<int32_t>(winforge::core::packages::PackageView::Bundles);
            if (m_packageViewPicker)
            {
                m_packageStateApplying = true;
                m_packageViewPicker.SelectedIndex(m_packageView);
                m_packageStateApplying = false;
            }
            RenderPackageManagerView();
            SavePackageManagerState();
            return BundleSnapshotLoadStatus::Loaded;
        }
        catch (...)
        {
            return BundleSnapshotLoadStatus::Failed;
        }
    }

    std::wstring MainWindow::PromptBundleOpenPath() const
    {
        return PromptOpenBundlePath(nullptr);
    }

    std::wstring MainWindow::PromptBundleSavePath() const
    {
        auto const suggested = m_packageBundleItems.empty() && m_packageBundleIncompatibleItems.empty()
            ? L"package-bundle.json"
            : L"package-bundle.ubundle";
        return PromptSaveBundlePath(nullptr, suggested);
    }

    void MainWindow::RenderPackageManager()
    {
        auto const& packageRegexSurface = winforge::core::regex::RegexSearchSurfaceFor(
            winforge::core::regex::RegexSearchSurfaceId::PackageDiscoverCachedResults);
        auto const retainCachedResults = m_packageRetainCachedResultsOnNextRender;
        m_packageRetainCachedResultsOnNextRender = false;
        CancelPackageWork();
        if (!retainCachedResults)
        {
            m_packageItems.clear();
            m_packageRunStates.clear();
            m_packageDetailsTarget.clear();
            m_packageLastAction = winforge::core::packages::PackageAction::Probe;
        }
        if (!m_currentArgument.empty())
        {
            m_packageView = PackageViewFromArgument(m_currentArgument);
        }
        m_packageView = std::clamp(m_packageView, 0, 8);

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Package Manager", L"套件管理" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"A genuine native workspace over 11 package engines: discover, update, uninstall, bundle, source, setup, configure, and inspect operations without launching an upstream UI.",
                L"真正原生嘅 11 引擎套件工作區：搜尋、更新、解除安裝、清單、來源、設定引擎、配置同檢視操作，全程唔會啟動上游 UI。" }.Pick(m_language));
        AutomationProperties::SetAutomationId(page, L"NativePackageManagerPage");

        InfoBar migration;
        migration.IsOpen(true);
        migration.IsClosable(false);
        migration.Severity(InfoBarSeverity::Informational);
        migration.Title(ToHString(winforge::core::LocalizedText{
            L"Native Package Manager runtime", L"原生套件管理 runtime" }.Pick(m_language)));
        migration.Message(ToHString(winforge::core::LocalizedText{
            L"Availability, discovery, installed-package, update, details, and source queries run through audited C++ argv builders, parsers, HTTPS transport, and a contained Win32 process runner. Individual reviewed mutations require separate explicit confirmation, run serially only at normal integrity, and retain no third-party output.",
            L"可用性、搜尋、已安裝套件、更新、詳細資料同來源查詢，會經審核嘅 C++ argv 建立器、解析器、HTTPS transport 同受控 Win32 process runner 執行；個別已檢視修改需要另外明確確認，只會喺正常 integrity 串行執行，而且唔會保留第三方輸出。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(migration, L"NativePackageManagerMigrationStatus");
        page.Children().Append(migration);

        Border managerCard;
        managerCard.Padding(Thickness{ 14, 10, 14, 10 });
        managerCard.CornerRadius(CornerRadius{ 8 });
        managerCard.BorderThickness(Thickness{ 1 });
        managerCard.BorderBrush(Application::Current().Resources().Lookup(box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
        managerCard.Background(Application::Current().Resources().Lookup(box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

        StackPanel managerCardContent;
        managerCardContent.Spacing(7);
        managerCardContent.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Package managers", L"套件管理器" }.Pick(m_language), 13, true));
        m_packageManagerFilters = StackPanel();
        // Keep one manager per row. A horizontal strip hid right-most
        // bilingual labels in compact/headless viewports, while the page
        // already offers a reachable vertical scroll path for every engine.
        m_packageManagerFilters.Orientation(Orientation::Vertical);
        m_packageManagerFilters.Spacing(4);
        AutomationProperties::SetAutomationId(m_packageManagerFilters, L"NativePackageManagerFilters");
        PopulatePackageManagerFilters(m_packageManagerFilters);
        managerCardContent.Children().Append(m_packageManagerFilters);
        managerCard.Child(managerCardContent);
        page.Children().Append(managerCard);

        StackPanel toolbar;
        // This toolbar previously packed seven controls into one row.  At
        // 100% scaling that exceeded the content viewport and clipped the
        // search box and action buttons.  A vertical, full-width layout keeps
        // every control reachable without relying on a hidden horizontal
        // scroll position.
        toolbar.Orientation(Orientation::Vertical);
        toolbar.Spacing(8);
        AutomationProperties::SetAutomationId(toolbar, L"NativePackageToolbar");

        m_packageViewPicker = ComboBox();
        m_packageViewPicker.MinWidth(170);
        for (auto const& view : winforge::core::packages::PackageViews())
        {
            ComboBoxItem item;
            item.Content(box_value(ToHString(
                winforge::core::LocalizedText{
                    std::wstring(view.name_en), std::wstring(view.name_zh) }.Pick(m_language))));
            auto const fragment = winforge::core::packages::PackageViewFragment(view.view);
            item.Tag(box_value(ToHString(fragment.value_or(L""))));
            m_packageViewPicker.Items().Append(item);
        }
        m_packageViewPicker.SelectedIndex(m_packageView);
        AutomationProperties::SetAutomationId(m_packageViewPicker, L"NativePackageViewPicker");
        m_packageViewPicker.SelectionChanged([this](Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            auto const selected = m_packageViewPicker.SelectedIndex();
            m_packageView = selected < 0 ? 0 : selected;
            SavePackageManagerState();
            if (m_packageProbeComplete)
            {
                CancelPackageWork();
            }
            ClearPackageSelection();
            m_packageItems.clear();
            m_packageRunStates.clear();
            m_packageDetailsTarget.clear();
            m_packageLastAction = winforge::core::packages::PackageAction::Probe;
            RenderPackageManagerView();
            if (m_packageProbeComplete)
            {
                AnnouncePackageStatus(
                    L"Package view changed. Previous query results were cleared; run a fresh read-only query for this view.",
                    L"套件檢視已更改。之前嘅查詢結果已清除；請為呢個檢視重新執行只讀查詢。");
            }
        });
        toolbar.Children().Append(m_packageViewPicker);

        m_packageSearchBox = AutoSuggestBox();
        m_packageSearchBox.Width(350);
        m_packageSearchBox.PlaceholderText(ToHString(m_packageDiscoverRegexEnabled
            ? winforge::core::LocalizedText{
                L"Filter cached Discover results with PCRE2 regex",
                L"用 PCRE2 正規表示式篩選已快取 Discover 結果" }.Pick(m_language)
            : winforge::core::LocalizedText{
                L"Search packages (for example: vscode, vlc, obs)",
                L"搜尋套件（例如 vscode、vlc、obs）" }.Pick(m_language)));
        m_packageSearchBox.Text(ToHString(
            m_packageDiscoverRegexEnabled ? m_packageDiscoverRegexPattern : m_packageSearchText));
        AutomationProperties::SetAutomationId(
            m_packageSearchBox,
            ToHString(packageRegexSurface.search_automation_id));
        m_packageSearchBox.QuerySubmitted([this](AutoSuggestBox const&, AutoSuggestBoxQuerySubmittedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            if (m_packageDiscoverRegexEnabled)
            {
                m_packageDiscoverRegexPattern = ToWide(m_packageSearchBox.Text());
                std::wstring diagnostic;
                auto const expression = CompileSearchRegex(
                    m_packageDiscoverRegexPattern,
                    m_packageSearchCaseSensitiveValue,
                    m_packageDiscoverRegexMultiline,
                    m_packageDiscoverRegexDotMatchesNewline,
                    diagnostic);
                m_packageDiscoverRegexDiagnostic = diagnostic;
                SavePackageManagerState();
                if (!expression)
                {
                    if (m_packageDiscoverRegexStatus)
                    {
                        m_packageDiscoverRegexStatus.Text(ToHString(diagnostic));
                        AutomationProperties::SetName(m_packageDiscoverRegexStatus, ToHString(diagnostic));
                    }
                    AnnouncePackageStatus(
                        L"Regex needs correction. Cached results were not changed and no package query was run.",
                        L"正規表示式需要修正。已快取結果冇改變，而且冇執行套件查詢。",
                        true);
                    return;
                }
                RenderPackageManagerView();
                AnnouncePackageStatus(
                    L"PCRE2 regex was applied to cached Discover results only. No package query was run.",
                    L"PCRE2 正規表示式只套用喺已快取 Discover 結果。冇執行套件查詢。");
                return;
            }
            m_packageSearchText = ToWide(m_packageSearchBox.Text());
            SavePackageManagerState();
            if (m_packageView == 0)
            {
                StartPackageQuery();
            }
        });
        m_packageSearchBox.TextChanged([this](AutoSuggestBox const&, AutoSuggestBoxTextChangedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            if (m_packageDiscoverRegexEnabled)
            {
                m_packageDiscoverRegexPattern = ToWide(m_packageSearchBox.Text());
                std::wstring diagnostic;
                auto const expression = CompileSearchRegex(
                    m_packageDiscoverRegexPattern,
                    m_packageSearchCaseSensitiveValue,
                    m_packageDiscoverRegexMultiline,
                    m_packageDiscoverRegexDotMatchesNewline,
                    diagnostic);
                m_packageDiscoverRegexDiagnostic = diagnostic;
                SavePackageManagerState();
                if (!expression)
                {
                    if (m_packageDiscoverRegexStatus)
                    {
                        m_packageDiscoverRegexStatus.Text(ToHString(diagnostic));
                        AutomationProperties::SetName(m_packageDiscoverRegexStatus, ToHString(diagnostic));
                    }
                    return;
                }
                RenderPackageManagerView();
                return;
            }
            m_packageSearchText = ToWide(m_packageSearchBox.Text());
            SavePackageManagerState();
            if (m_packageView == 0)
            {
                if (InvalidatePackageQueryResults())
                {
                    AnnouncePackageStatus(
                        L"Search text changed. Previous query results were cleared; choose Search to run the new query.",
                        L"搜尋文字已更改。之前嘅查詢結果已清除；請揀搜尋執行新查詢。");
                }
                RenderPackageManagerView();
            }
        });
        toolbar.Children().Append(m_packageSearchBox);

        // Discover filters operate exclusively over m_packageItems after a
        // read-only query has completed. They deliberately do not invalidate
        // results or start another package-manager process/network request.
        m_packageDiscoverFilterPanel = StackPanel();
        m_packageDiscoverFilterPanel.Spacing(6);
        AutomationProperties::SetAutomationId(
            m_packageDiscoverFilterPanel,
            L"NativePackageDiscoverSearchOptions");
        auto const discoverFilterTitle = winforge::core::LocalizedText{
            L"Discover result filters", L"搜尋結果篩選" }.Pick(m_language);
        m_packageDiscoverFilterPanel.Children().Append(CreateText(discoverFilterTitle, 13, true));
        auto discoverFilterNote = CreateText(
            winforge::core::LocalizedText{
                L"Applied locally to cached Discover results; changing an option never runs another package query.",
                L"只會喺已快取嘅搜尋結果本機套用；改選項唔會再執行套件查詢。" }.Pick(m_language),
            12);
        discoverFilterNote.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(discoverFilterNote, L"NativePackageDiscoverSearchOptionsNote");
        m_packageDiscoverFilterPanel.Children().Append(discoverFilterNote);

        m_packageDiscoverRegexMode = ToggleSwitch();
        m_packageDiscoverRegexMode.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Use PCRE2 regex on cached results only",
            L"只喺已快取結果使用 PCRE2 正規表示式" }.Pick(m_language))));
        m_packageDiscoverRegexMode.IsOn(m_packageDiscoverRegexEnabled);
        AutomationProperties::SetAutomationId(
            m_packageDiscoverRegexMode,
            ToHString(packageRegexSurface.regex_mode_automation_id));
        AutomationProperties::SetName(
            m_packageDiscoverRegexMode,
            L"Use PCRE2 regex only for cached Package Discover results · 只喺已快取套件 Discover 結果使用 PCRE2 正規表示式");
        m_packageDiscoverRegexMode.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            m_packageDiscoverRegexEnabled = sender.as<ToggleSwitch>().IsOn();
            if (m_packageDiscoverRegexEnabled &&
                m_packageSearchMode == winforge::core::packages::PackageSearchMode::Similar)
            {
                m_packageSearchMode = winforge::core::packages::PackageSearchMode::Both;
                if (m_packageSearchModePicker)
                {
                    m_packageStateApplying = true;
                    m_packageSearchModePicker.SelectedIndex(
                        static_cast<int32_t>(winforge::core::packages::PackageSearchMode::Both));
                    m_packageStateApplying = false;
                }
            }
            if (m_packageSearchBox)
            {
                m_packageStateApplying = true;
                m_packageSearchBox.PlaceholderText(ToHString(m_packageDiscoverRegexEnabled
                    ? winforge::core::LocalizedText{
                        L"Filter cached Discover results with PCRE2 regex",
                        L"用 PCRE2 正規表示式篩選已快取 Discover 結果" }.Pick(m_language)
                    : winforge::core::LocalizedText{
                        L"Search packages (for example: vscode, vlc, obs)",
                        L"搜尋套件（例如 vscode、vlc、obs）" }.Pick(m_language)));
                m_packageSearchBox.Text(ToHString(
                    m_packageDiscoverRegexEnabled ? m_packageDiscoverRegexPattern : m_packageSearchText));
                m_packageStateApplying = false;
            }
            SavePackageManagerState();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                m_packageDiscoverRegexEnabled
                    ? L"PCRE2 regex mode is local-only. It cannot start or receive a package query."
                    : L"Literal package search mode is restored. Choose Search to start a new read-only query.",
                m_packageDiscoverRegexEnabled
                    ? L"PCRE2 正規表示式模式只限本機；唔可以開始或者接收套件查詢。"
                    : L"已還原文字套件搜尋模式。揀搜尋先會開始新只讀查詢。");
        });
        m_packageDiscoverFilterPanel.Children().Append(m_packageDiscoverRegexMode);

        m_packageDiscoverRegexBuilder = Button();
        m_packageDiscoverRegexBuilder.Content(box_value(L"Build cached-result regex · 建立已快取結果正規表示式"));
        m_packageDiscoverRegexBuilder.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(
            m_packageDiscoverRegexBuilder,
            ToHString(packageRegexSurface.builder_automation_id));
        AutomationProperties::SetName(m_packageDiscoverRegexBuilder, L"Open the regex builder for cached Package Discover results · 開啟已快取套件 Discover 結果嘅正規表示式建立器");
        m_packageDiscoverRegexBuilder.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            OpenRegexBuilder(RegexBuilderTarget::PackageDiscover, m_packageDiscoverRegexPattern);
        });
        m_packageDiscoverFilterPanel.Children().Append(m_packageDiscoverRegexBuilder);

        m_packageDiscoverRegexApply = Button();
        m_packageDiscoverRegexApply.Content(box_value(L"Apply local regex filter · 套用本機正規篩選"));
        m_packageDiscoverRegexApply.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_packageDiscoverRegexApply, L"NativePackageRegexApply");
        AutomationProperties::SetName(m_packageDiscoverRegexApply, L"Apply regex only to cached Package Discover results · 只向已快取套件 Discover 結果套用正規表示式");
        m_packageDiscoverRegexApply.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (!m_packageDiscoverRegexEnabled)
            {
                return;
            }
            std::wstring diagnostic;
            auto const expression = CompileSearchRegex(
                m_packageDiscoverRegexPattern,
                m_packageSearchCaseSensitiveValue,
                m_packageDiscoverRegexMultiline,
                m_packageDiscoverRegexDotMatchesNewline,
                diagnostic);
            m_packageDiscoverRegexDiagnostic = diagnostic;
            SavePackageManagerState();
            if (!expression)
            {
                if (m_packageDiscoverRegexStatus)
                {
                    m_packageDiscoverRegexStatus.Text(ToHString(diagnostic));
                    AutomationProperties::SetName(m_packageDiscoverRegexStatus, ToHString(diagnostic));
                }
                AnnouncePackageStatus(
                    L"Regex needs correction. Cached results were not changed and no package query was run.",
                    L"正規表示式需要修正。已快取結果冇改變，而且冇執行套件查詢。",
                    true);
                return;
            }
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"PCRE2 regex was applied to cached Discover results only. No package query was run.",
                L"PCRE2 正規表示式只套用喺已快取 Discover 結果。冇執行套件查詢。");
        });
        m_packageDiscoverFilterPanel.Children().Append(m_packageDiscoverRegexApply);

        m_packageDiscoverRegexStatus = CreateText(
            m_packageDiscoverRegexEnabled
                ? L"Validating local PCRE2 regex. · 正在驗證本機 PCRE2 正規表示式。"
                : L"Literal Discover filtering · 文字 Discover 篩選",
            12);
        m_packageDiscoverRegexStatus.TextWrapping(TextWrapping::Wrap);
        m_packageDiscoverRegexStatus.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_packageDiscoverRegexStatus, L"NativePackageRegexStatus");
        AutomationProperties::SetLiveSetting(
            m_packageDiscoverRegexStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        m_packageDiscoverFilterPanel.Children().Append(m_packageDiscoverRegexStatus);

        m_packageSearchModePicker = ComboBox();
        m_packageSearchModePicker.MinWidth(250);
        AutomationProperties::SetAutomationId(m_packageSearchModePicker, L"NativePackageSearchModePicker");
        AutomationProperties::SetName(
            m_packageSearchModePicker,
            ToHString(winforge::core::LocalizedText{ L"Discover search mode", L"搜尋模式" }.Pick(m_language)));
        for (auto const& option : std::array{
            std::pair{ winforge::core::packages::PackageSearchMode::Both,
                winforge::core::LocalizedText{ L"Both name and ID", L"名稱同 ID" } },
            std::pair{ winforge::core::packages::PackageSearchMode::Name,
                winforge::core::LocalizedText{ L"Package name", L"套件名稱" } },
            std::pair{ winforge::core::packages::PackageSearchMode::Id,
                winforge::core::LocalizedText{ L"Package ID", L"套件 ID" } },
            std::pair{ winforge::core::packages::PackageSearchMode::Exact,
                winforge::core::LocalizedText{ L"Exact match", L"完全符合" } },
            std::pair{ winforge::core::packages::PackageSearchMode::Similar,
                winforge::core::LocalizedText{ L"Show similar packages", L"顯示相似套件" } },
        })
        {
            ComboBoxItem item;
            item.Content(box_value(ToHString(option.second.Pick(m_language))));
            item.Tag(box_value(static_cast<int32_t>(option.first)));
            m_packageSearchModePicker.Items().Append(item);
        }
        m_packageSearchModePicker.SelectedIndex(static_cast<int32_t>(m_packageSearchMode));

        auto reapplyDiscoverFilters = [this]()
        {
            std::shared_ptr<winforge::core::regex::SafeRegex const> expression;
            std::wstring diagnostic;
            auto const filterText = m_packageDiscoverRegexEnabled
                ? std::wstring_view(m_packageDiscoverRegexPattern)
                : std::wstring_view(m_packageSearchText);
            if (m_packageDiscoverRegexEnabled)
            {
                expression = CompileSearchRegex(
                    m_packageDiscoverRegexPattern,
                    m_packageSearchCaseSensitiveValue,
                    m_packageDiscoverRegexMultiline,
                    m_packageDiscoverRegexDotMatchesNewline,
                    diagnostic);
                m_packageDiscoverRegexDiagnostic = diagnostic;
                if (!expression)
                {
                    if (m_packageDiscoverRegexStatus)
                    {
                        m_packageDiscoverRegexStatus.Text(ToHString(diagnostic));
                        AutomationProperties::SetName(m_packageDiscoverRegexStatus, ToHString(diagnostic));
                    }
                    SavePackageManagerState();
                    AnnouncePackageStatus(
                        L"Regex needs correction. Cached results were not changed and no package query was run.",
                        L"正規表示式需要修正。已快取結果冇改變，而且冇執行套件查詢。",
                        true);
                    return;
                }
            }
            auto const visibleItems = winforge::core::packages::FilterDiscoverPackageItems(
                filterText,
                m_packageItems,
                winforge::core::packages::PackageSearchOptions{
                    m_packageSearchMode,
                    m_packageSearchCaseSensitiveValue,
                    m_packageSearchIgnoreSpecialValue,
                    std::move(expression) });
            auto const visible = std::to_wstring(visibleItems.size());
            auto const raw = std::to_wstring(m_packageItems.size());
            SavePackageManagerState();
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"Discover filters applied locally: " + visible + L" of " + raw +
                    L" cached results visible. No package query was run.",
                L"已在本機套用搜尋篩選：" + visible + L"／" + raw +
                    L" 個已快取結果可見。冇執行套件查詢。");
        };
        m_packageSearchModePicker.SelectionChanged([this, reapplyDiscoverFilters](
            Windows::Foundation::IInspectable const&,
            SelectionChangedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            m_packageSearchMode = static_cast<winforge::core::packages::PackageSearchMode>(
                std::clamp(m_packageSearchModePicker.SelectedIndex(), 0, 4));
            if (m_packageDiscoverRegexEnabled &&
                m_packageSearchMode == winforge::core::packages::PackageSearchMode::Similar)
            {
                m_packageStateApplying = true;
                m_packageSearchMode = winforge::core::packages::PackageSearchMode::Both;
                m_packageSearchModePicker.SelectedIndex(
                    static_cast<int32_t>(winforge::core::packages::PackageSearchMode::Both));
                m_packageStateApplying = false;
            }
            reapplyDiscoverFilters();
        });
        m_packageDiscoverFilterPanel.Children().Append(m_packageSearchModePicker);

        m_packageSearchCaseSensitive = ToggleSwitch();
        m_packageSearchCaseSensitive.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Distinguish uppercase and lowercase", L"區分大小寫" }.Pick(m_language))));
        m_packageSearchCaseSensitive.IsOn(m_packageSearchCaseSensitiveValue);
        AutomationProperties::SetAutomationId(
            m_packageSearchCaseSensitive,
            L"NativePackageSearchCaseSensitive");
        AutomationProperties::SetName(
            m_packageSearchCaseSensitive,
            ToHString(winforge::core::LocalizedText{
                L"Discover search: distinguish uppercase and lowercase", L"搜尋：區分大小寫" }.Pick(m_language)));
        m_packageSearchCaseSensitive.Toggled([this, reapplyDiscoverFilters](
            Windows::Foundation::IInspectable const& sender,
            RoutedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            m_packageSearchCaseSensitiveValue = sender.as<ToggleSwitch>().IsOn();
            reapplyDiscoverFilters();
        });
        m_packageDiscoverFilterPanel.Children().Append(m_packageSearchCaseSensitive);

        m_packageSearchIgnoreSpecial = ToggleSwitch();
        m_packageSearchIgnoreSpecial.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Ignore special characters", L"忽略特殊字元" }.Pick(m_language))));
        m_packageSearchIgnoreSpecial.IsOn(m_packageSearchIgnoreSpecialValue);
        AutomationProperties::SetAutomationId(
            m_packageSearchIgnoreSpecial,
            L"NativePackageSearchIgnoreSpecial");
        AutomationProperties::SetName(
            m_packageSearchIgnoreSpecial,
            ToHString(winforge::core::LocalizedText{
                L"Discover search: ignore special characters", L"搜尋：忽略特殊字元" }.Pick(m_language)));
        m_packageSearchIgnoreSpecial.Toggled([this, reapplyDiscoverFilters](
            Windows::Foundation::IInspectable const& sender,
            RoutedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            m_packageSearchIgnoreSpecialValue = sender.as<ToggleSwitch>().IsOn();
            reapplyDiscoverFilters();
        });
        m_packageDiscoverFilterPanel.Children().Append(m_packageSearchIgnoreSpecial);
        toolbar.Children().Append(m_packageDiscoverFilterPanel);

        m_packageSortPicker = ComboBox();
        m_packageSortPicker.MinWidth(210);
        for (auto const& option : std::array{
            std::pair{ 0, winforge::core::LocalizedText{ L"Sort by manager", L"按管理器排序" } },
            std::pair{ 1, winforge::core::LocalizedText{ L"Sort by name", L"按名稱排序" } },
            std::pair{ 2, winforge::core::LocalizedText{ L"Sort by source", L"按來源排序" } },
            std::pair{ 3, winforge::core::LocalizedText{ L"Sort by ID", L"按 ID 排序" } },
        })
        {
            ComboBoxItem item;
            item.Content(box_value(ToHString(option.second.Pick(m_language))));
            item.Tag(box_value(option.first));
            m_packageSortPicker.Items().Append(item);
        }
        m_packageSortPicker.SelectedIndex(m_packageSortMode);
        AutomationProperties::SetAutomationId(m_packageSortPicker, L"NativePackageSortPicker");
        m_packageSortPicker.SelectionChanged([this](Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
                return;
            }
            m_packageSortMode = std::clamp(m_packageSortPicker.SelectedIndex(), 0, 3);
            ApplyPackageSort();
            SavePackageManagerState();
            RenderPackageManagerView();
        });
        toolbar.Children().Append(m_packageSortPicker);

        m_packagePrimaryAction = Button();
        m_packagePrimaryAction.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(m_packagePrimaryAction, L"NativePackagePrimaryAction");
        m_packagePrimaryAction.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_packageView == 6)
            {
                StartPackageManagerProbes();
            }
            else if (m_packageView == 3)
            {
                auto const savePath = PromptBundleSavePath();
                if (!savePath.empty())
                {
                    if (SaveBundleSnapshot(savePath))
                    {
                        m_packageBundleSourcePath = savePath;
                        m_packageBundleDirty = false;
                        RenderPackageManagerView();
                        RecordPackageOperation(
                            L"Exported native bundle snapshot to " + savePath + L". · 已匯出 bundle。");
                        AnnouncePackageStatus(
                            L"Bundle snapshot exported.",
                            L"Bundle 快照已匯出。");
                    }
                    else
                    {
                        AnnouncePackageStatus(
                            L"Bundle export failed.",
                            L"Bundle 匯出失敗。",
                            true);
                    }
                }
            }
            else if (m_packageView == 5)
            {
                ClearPackageUpdateRules();
            }
            else if (m_packageView == 7)
            {
                ResetPackageManagerState();
            }
            else
            {
                StartPackageQuery();
            }
        });
        toolbar.Children().Append(m_packagePrimaryAction);

        m_packageSecondaryAction = Button();
        m_packageSecondaryAction.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(m_packageSecondaryAction, L"NativePackageSecondaryAction");
        m_packageSecondaryAction.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_packageView == 8)
            {
                ClearPackageOperationLog();
            }
            else if (m_packageView == 1)
            {
                PreviewPackageBulkUpdate();
            }
            else if (m_packageView == 3)
            {
                auto const openPath = PromptBundleOpenPath();
                if (!openPath.empty())
                {
                    auto const loadStatus = LoadBundleSnapshot(openPath);
                    if (loadStatus == BundleSnapshotLoadStatus::Loaded)
                    {
                        RecordPackageOperation(
                            L"Imported native bundle snapshot from " + openPath + L". · 已匯入 bundle。");
                        AnnouncePackageStatus(
                            m_packageBundleImportNote.empty()
                                ? L"Bundle snapshot imported."
                                : L"Bundle snapshot imported with metadata-only safety notes.",
                            m_packageBundleImportNote.empty()
                                ? L"Bundle 快照已匯入。"
                                : L"Bundle 快照已匯入，並有只限 metadata 嘅安全提示。");
                    }
                    else if (loadStatus == BundleSnapshotLoadStatus::Cancelled)
                    {
                        AnnouncePackageStatus(
                            L"Bundle import cancelled; the unsaved workspace was kept.",
                            L"Bundle 匯入已取消；已保留未儲存工作區。");
                    }
                    else
                    {
                        AnnouncePackageStatus(
                            L"Bundle import failed.",
                            L"Bundle 匯入失敗。",
                            true);
                    }
                }
            }
            else
            {
                RenderPackageManagerView();
            }
        });
        toolbar.Children().Append(m_packageSecondaryAction);

        m_packageOperationsAction = Button();
        m_packageOperationsAction.Content(box_value(ToHString(
            winforge::core::LocalizedText{ L"Operations", L"操作" }.Pick(m_language))));
        m_packageOperationsAction.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(m_packageOperationsAction, L"NativePackageOperationsAction");
        m_packageOperationsAction.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            m_packageView = 8;
            if (m_packageViewPicker)
            {
                m_packageViewPicker.SelectedIndex(m_packageView);
            }
            RenderPackageManagerView();
        });
        toolbar.Children().Append(m_packageOperationsAction);

        m_packageBusy = ProgressRing();
        m_packageBusy.Width(24);
        m_packageBusy.Height(24);
        m_packageBusy.IsActive(false);
        AutomationProperties::SetAutomationId(m_packageBusy, L"NativePackageBusy");
        toolbar.Children().Append(m_packageBusy);
        page.Children().Append(toolbar);

        m_packageResultsHeader = CreateText(L"", 14, true);
        AutomationProperties::SetAutomationId(m_packageResultsHeader, L"NativePackageResultsHeader");
        page.Children().Append(m_packageResultsHeader);

        auto const initialStatus = winforge::core::LocalizedText{
            L"Preparing package-engine checks.",
            L"準備檢查套件引擎。" }.Pick(m_language);
        m_packageLiveStatus = CreateText(initialStatus, 12.5);
        m_packageLiveStatus.TextWrapping(TextWrapping::Wrap);
        m_packageLiveStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_packageLiveStatus, L"NativePackageLiveStatus");
        AutomationProperties::SetName(
            m_packageLiveStatus,
            ToHString(winforge::core::LocalizedText{
                L"Package Manager status: Preparing package-engine checks.",
                L"套件管理狀態：準備檢查套件引擎。" }.Pick(m_language)));
        AutomationProperties::SetLiveSetting(
            m_packageLiveStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_packageLiveStatus);

        m_packageQueryAudit = CreateText(
            L"Remote query epoch: " + std::to_wstring(m_packageQueryEpoch) +
                L" · 遠端查詢 epoch：" + std::to_wstring(m_packageQueryEpoch),
            11);
        m_packageQueryAudit.TextWrapping(TextWrapping::Wrap);
        m_packageQueryAudit.Opacity(0.70);
        AutomationProperties::SetAutomationId(m_packageQueryAudit, L"NativePackageQueryAudit");
        AutomationProperties::SetName(
            m_packageQueryAudit,
            ToHString(L"Remote package query epoch " + std::to_wstring(m_packageQueryEpoch) +
                L" · 遠端套件查詢 epoch " + std::to_wstring(m_packageQueryEpoch)));
        AutomationProperties::SetHelpText(
            m_packageQueryAudit,
            L"This counter changes only when the native Package Manager begins a CLI or HTTPS query. Local regex filtering never changes it. · 呢個計數只會喺原生套件管理開始 CLI 或 HTTPS 查詢時更改；本機正規篩選永遠唔會更改。");
        page.Children().Append(m_packageQueryAudit);

        m_packageResults = StackPanel();
        m_packageResults.Spacing(8);
        AutomationProperties::SetAutomationId(m_packageResults, L"NativePackageResults");
        page.Children().Append(m_packageResults);

        ShowPage(page);
        RenderPackageManagerView();
        if (!retainCachedResults)
        {
            StartPackageManagerProbes();
        }
        else
        {
            AnnouncePackageStatus(
                L"Returned to cached Discover results. PCRE2 filtering remains local and no package query was started.",
                L"已返回已快取 Discover 結果。PCRE2 篩選保持本機，而且冇開始套件查詢。");
        }
    }

    void MainWindow::CancelPackageWork()
    {
        m_packageStopSource.request_stop();
        m_packageStopSource = std::stop_source{};
        ++m_packageGeneration;
        m_packageWorking = false;
        if (m_packageBusy)
        {
            m_packageBusy.IsActive(false);
        }
    }

    bool MainWindow::InvalidatePackageQueryResults()
    {
        auto const queryInFlight = m_packageWorking && m_packageProbeComplete;
        auto const invalidated = queryInFlight || !m_packageItems.empty() || !m_packageRunStates.empty();
        if (queryInFlight)
        {
            CancelPackageWork();
        }
        ClearPackageSelection();
        m_packageItems.clear();
        m_packageRunStates.clear();
        m_packageDetailsTarget.clear();
        m_packageLastAction = winforge::core::packages::PackageAction::Probe;
        return invalidated;
    }

    void MainWindow::AnnouncePackageStatus(
        std::wstring_view en,
        std::wstring_view zh,
        bool assertive)
    {
        if (!m_packageLiveStatus) return;

        auto const message = winforge::core::LocalizedText{
            std::wstring(en), std::wstring(zh) }.Pick(m_language);
        auto const accessibleName = winforge::core::LocalizedText{
            L"Package Manager status: " + std::wstring(en),
            L"套件管理狀態：" + std::wstring(zh) }.Pick(m_language);
        m_packageLiveStatus.Text(ToHString(message));
        AutomationProperties::SetName(m_packageLiveStatus, ToHString(accessibleName));
        AutomationProperties::SetLiveSetting(
            m_packageLiveStatus,
            assertive
                ? Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Assertive
                : Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);

        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_packageLiveStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_packageLiveStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility notification failure must not break the package operation itself.
        }
    }

    bool MainWindow::HasSelectedAvailablePackageManager() const
    {
        for (auto const& manager : winforge::core::packages::PackageManagers())
        {
            auto const key = std::wstring(manager.key);
            auto const selected = m_packageManagersSelected.find(key);
            auto const available = m_packageManagersAvailable.find(key);
            if (selected != m_packageManagersSelected.end() && selected->second &&
                available != m_packageManagersAvailable.end() && available->second)
            {
                return true;
            }
        }
        return false;
    }

    void MainWindow::StartPackageManagerProbes()
    {
        CancelPackageWork();
        m_packageProbeComplete = false;
        m_packageManagersAvailable.clear();
        m_packageProbeDiagnostics.clear();
        ClearPackageSelection();
        m_packageItems.clear();
        m_packageRunStates.clear();
        m_packageWorking = true;
        if (m_packageBusy) m_packageBusy.IsActive(true);
        if (m_packageManagerFilters) PopulatePackageManagerFilters(m_packageManagerFilters);
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Checking package engines. Read-only controls will unlock after the checks finish.",
            L"正在檢查套件引擎。檢查完成之後，只讀控制項先會解鎖。");

        auto const generation = m_packageGeneration;
        ProbePackageManagersAsync(generation, m_packageStopSource.get_token());
    }

    void MainWindow::StartPackageQuery()
    {
        using winforge::core::packages::PackageAction;

        PackageAction action;
        switch (m_packageView)
        {
        case 0: action = PackageAction::Search; break;
        case 1: action = PackageAction::Updates; break;
        case 2: action = PackageAction::Installed; break;
        case 4: action = PackageAction::Sources; break;
        default: return;
        }

        // Regex is a cached Discover-result filter only. Keep a direct guard
        // here as well as disabled UI so no future caller can accidentally
        // route its pattern into a package CLI or HTTPS request.
        if (action == PackageAction::Search && m_packageDiscoverRegexEnabled)
        {
            RenderPackageManagerView();
            AnnouncePackageStatus(
                L"PCRE2 regex mode only filters cached Discover results. Switch back to literal search before starting a package query.",
                L"PCRE2 正規表示式模式只會篩選已快取 Discover 結果；開始套件查詢之前請切返文字搜尋。",
                true);
            return;
        }

        auto query = m_packageSearchBox ? ToWide(m_packageSearchBox.Text()) : std::wstring{};
        if (action == PackageAction::Search)
        {
            query = TrimPackageSearchText(query);
            m_packageSearchText = query;
            if (m_packageSearchBox && ToWide(m_packageSearchBox.Text()) != query)
            {
                m_packageStateApplying = true;
                m_packageSearchBox.Text(ToHString(query));
                m_packageStateApplying = false;
            }
            SavePackageManagerState();
            if (query.size() < 2)
            {
                RenderPackageManagerView();
                AnnouncePackageStatus(
                    L"Enter at least 2 characters before searching.",
                    L"請輸入最少 2 個字元先搜尋。",
                    true);
                return;
            }
        }

        std::vector<std::wstring> managerKeys;
        for (auto const& manager : winforge::core::packages::PackageManagers())
        {
            auto const key = std::wstring(manager.key);
            auto const selected = m_packageManagersSelected.find(key);
            auto const available = m_packageManagersAvailable.find(key);
            if (selected != m_packageManagersSelected.end() && selected->second &&
                available != m_packageManagersAvailable.end() && available->second)
            {
                managerKeys.push_back(key);
            }
        }
        if (managerKeys.empty())
        {
            RenderPackageManagerView();
            return;
        }

        ++m_packageQueryEpoch;
        if (m_packageQueryAudit)
        {
            auto const audit = L"Remote query epoch: " + std::to_wstring(m_packageQueryEpoch) +
                L" · 遠端查詢 epoch：" + std::to_wstring(m_packageQueryEpoch);
            m_packageQueryAudit.Text(ToHString(audit));
            AutomationProperties::SetName(
                m_packageQueryAudit,
                ToHString(L"Remote package query epoch " + std::to_wstring(m_packageQueryEpoch) +
                    L" · 遠端套件查詢 epoch " + std::to_wstring(m_packageQueryEpoch)));
        }

        CancelPackageWork();
        ClearPackageSelection();
        m_packageItems.clear();
        m_packageRunStates.clear();
        m_packageDetailsTarget.clear();
        m_packageLastAction = action;
        m_packageWorking = true;
        if (m_packageBusy) m_packageBusy.IsActive(true);

        auto const actionLabel = action == PackageAction::Search ? L"search"
            : action == PackageAction::Updates ? L"updates"
            : action == PackageAction::Installed ? L"installed"
            : L"sources";
        RecordPackageOperation(
            std::wstring(L"Started native ") + actionLabel + L" query across " +
                std::to_wstring(managerKeys.size()) + L" manager(s). · 已開始原生查詢。");
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Package query started. Completion and any engine failures will be announced.",
            L"套件查詢已開始。完成同任何引擎失敗都會通知。");

        auto const generation = m_packageGeneration;
        QueryPackageManagersAsync(
            generation,
            action,
            std::move(query),
            std::move(managerKeys),
            m_packageStopSource.get_token());
    }

    void MainWindow::ProbePackageManagersAsync(
        std::uint64_t generation,
        std::stop_token cancellationToken)
    {
        try
        {
            auto lifetime = get_strong();
            auto dispatcher = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
            std::vector<std::wstring> managerKeys;
            for (auto const& manager : winforge::core::packages::PackageManagers())
            {
                managerKeys.emplace_back(manager.key);
            }

            std::thread worker([lifetime, dispatcher, generation, cancellationToken,
                managerKeys = std::move(managerKeys)]() mutable
            {
                std::vector<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>> results;
                try
                {
                    std::vector<std::future<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>>> probes;
                    probes.reserve(managerKeys.size());
                    for (auto const& key : managerKeys)
                    {
                        probes.push_back(std::async(std::launch::async, [key, cancellationToken]()
                        {
                            winforge::core::packages::PackageRuntimeOptions options;
                            options.timeout = std::chrono::seconds(20);
                            options.cancellation_token = cancellationToken;
                            return std::make_pair(
                                key,
                                winforge::core::packages::ProbePackageManager(key, options));
                        }));
                    }
                    results.reserve(probes.size());
                    for (auto& probe : probes)
                    {
                        results.push_back(probe.get());
                    }
                }
                catch (std::exception const& error)
                {
                    winforge::core::packages::PackageRuntimeResult failed;
                    failed.diagnostic = winrt::to_hstring(error.what()).c_str();
                    results.emplace_back(L"runtime", std::move(failed));
                }
                catch (...)
                {
                    winforge::core::packages::PackageRuntimeResult failed;
                    failed.diagnostic = L"unknown native probe failure";
                    results.emplace_back(L"runtime", std::move(failed));
                }

                if (cancellationToken.stop_requested()) return;
                try
                {
                    dispatcher.TryEnqueue([lifetime, generation, cancellationToken,
                        results = std::move(results)]() mutable
                    {
                        if (!cancellationToken.stop_requested())
                            lifetime->CompletePackageManagerProbes(generation, std::move(results));
                    });
                }
                catch (...)
                {
                }
            });
            worker.detach();
        }
        catch (std::exception const& error)
        {
            winforge::core::packages::PackageRuntimeResult failed;
            failed.diagnostic = L"could not start the native package probe worker: ";
            failed.diagnostic += winrt::to_hstring(error.what()).c_str();
            CompletePackageManagerProbes(generation, { { L"runtime", std::move(failed) } });
        }
        catch (...)
        {
            winforge::core::packages::PackageRuntimeResult failed;
            failed.diagnostic = L"could not start the native package probe worker";
            CompletePackageManagerProbes(generation, { { L"runtime", std::move(failed) } });
        }
    }

    void MainWindow::QueryPackageManagersAsync(
        std::uint64_t generation,
        winforge::core::packages::PackageAction action,
        std::wstring query,
        std::vector<std::wstring> managerKeys,
        std::stop_token cancellationToken)
    {
        try
        {
            auto lifetime = get_strong();
            auto dispatcher = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
            std::thread worker([lifetime, dispatcher, generation, action, query = std::move(query),
                managerKeys = std::move(managerKeys), cancellationToken]() mutable
            {
                std::vector<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>> results;
                try
                {
                    std::vector<std::future<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>>> queries;
                    queries.reserve(managerKeys.size());
                    for (auto const& key : managerKeys)
                    {
                        queries.push_back(std::async(std::launch::async, [key, action, query, cancellationToken]()
                        {
                            winforge::core::packages::PackageRuntimeOptions options;
                            options.timeout = std::chrono::seconds(45);
                            options.cancellation_token = cancellationToken;
                            return std::make_pair(
                                key,
                                winforge::core::packages::QueryPackageManager(key, action, query, options));
                        }));
                    }
                    results.reserve(queries.size());
                    for (auto& managerQuery : queries)
                    {
                        results.push_back(managerQuery.get());
                    }
                }
                catch (std::exception const& error)
                {
                    winforge::core::packages::PackageRuntimeResult failed;
                    failed.diagnostic = winrt::to_hstring(error.what()).c_str();
                    results.emplace_back(L"runtime", std::move(failed));
                }
                catch (...)
                {
                    winforge::core::packages::PackageRuntimeResult failed;
                    failed.diagnostic = L"unknown native package query failure";
                    results.emplace_back(L"runtime", std::move(failed));
                }

                if (cancellationToken.stop_requested()) return;
                try
                {
                    dispatcher.TryEnqueue([lifetime, generation, action, cancellationToken,
                        results = std::move(results)]() mutable
                    {
                        if (!cancellationToken.stop_requested())
                            lifetime->CompletePackageQuery(generation, action, std::move(results));
                    });
                }
                catch (...)
                {
                }
            });
            worker.detach();
        }
        catch (std::exception const& error)
        {
            winforge::core::packages::PackageRuntimeResult failed;
            failed.diagnostic = L"could not start the native package query worker: ";
            failed.diagnostic += winrt::to_hstring(error.what()).c_str();
            CompletePackageQuery(generation, action, { { L"runtime", std::move(failed) } });
        }
        catch (...)
        {
            winforge::core::packages::PackageRuntimeResult failed;
            failed.diagnostic = L"could not start the native package query worker";
            CompletePackageQuery(generation, action, { { L"runtime", std::move(failed) } });
        }
    }

    void MainWindow::CompletePackageManagerProbes(
        std::uint64_t generation,
        std::vector<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>> results)
    {
        if (generation != m_packageGeneration || m_currentRoute != L"module.packages") return;

        m_packageWorking = false;
        m_packageProbeComplete = true;
        if (m_packageBusy) m_packageBusy.IsActive(false);
        m_packageManagersAvailable.clear();
        m_packageProbeDiagnostics.clear();

        std::size_t availableCount = 0;
        std::wstring runtimeFailure;
        for (auto& [key, result] : results)
        {
            if (key == L"runtime")
            {
                runtimeFailure = std::move(result.diagnostic);
                continue;
            }
            m_packageManagersAvailable[key] = result.success;
            m_packageProbeDiagnostics[key] = result.success
                ? std::wstring(L"available · 可用")
                : (result.diagnostic.empty() ? std::wstring(L"not available · 未可用") : result.diagnostic);
            if (result.success) ++availableCount;
        }
        for (auto const& manager : winforge::core::packages::PackageManagers())
        {
            auto const key = std::wstring(manager.key);
            if (!m_packageManagersAvailable.contains(key))
            {
                m_packageManagersAvailable[key] = false;
                m_packageProbeDiagnostics[key] = runtimeFailure.empty()
                    ? std::wstring(L"probe did not complete · 探測未完成")
                    : std::wstring(L"probe worker did not complete · 探測 worker 未完成");
            }
        }

        RecordPackageOperation(
            L"Availability probe: " + std::to_wstring(availableCount) + L"/" +
                std::to_wstring(winforge::core::packages::PackageManagers().size()) +
                L" managers ready. · 可用性探測完成。");
        if (m_packageManagerFilters) PopulatePackageManagerFilters(m_packageManagerFilters);
        RenderPackageManagerView();
        if (!runtimeFailure.empty())
        {
            AnnouncePackageStatus(
                L"Package-engine checks failed before every probe could complete. Review the setup status and try again.",
                L"套件引擎檢查喺全部探測完成之前失敗。請檢查設定狀態，再試一次。",
                true);
        }
        else
        {
            auto const total = winforge::core::packages::PackageManagers().size();
            AnnouncePackageStatus(
                L"Package-engine checks finished. " + std::to_wstring(availableCount) + L" of " +
                    std::to_wstring(total) + L" engines are available.",
                L"套件引擎檢查完成。" + std::to_wstring(total) + L" 個引擎之中有 " +
                    std::to_wstring(availableCount) + L" 個可用。",
                availableCount == 0);
        }
    }

    void MainWindow::CompletePackageQuery(
        std::uint64_t generation,
        winforge::core::packages::PackageAction action,
        std::vector<std::pair<std::wstring, winforge::core::packages::PackageRuntimeResult>> results)
    {
        if (generation != m_packageGeneration || m_currentRoute != L"module.packages") return;

        m_packageWorking = false;
        m_packageLastAction = action;
        if (m_packageBusy) m_packageBusy.IsActive(false);
        ClearPackageSelection();
        m_packageItems.clear();
        m_packageRunStates.clear();

        std::size_t successfulManagers = 0;
        std::size_t failedManagers = 0;
        std::size_t ignoredRows = 0;
        for (auto& [key, result] : results)
        {
            PackageManagerRunState state;
            state.manager_key = key;
            state.success = result.success;
            state.parser_supported = result.parser_supported;
            state.requires_runtime_resolution = result.requires_runtime_resolution;
            state.package_count = result.success ? result.packages.size() : 0;
            if (action == winforge::core::packages::PackageAction::Sources)
            {
                if (result.success)
                {
                    state.output = winforge::core::LocalizedText{
                        L"Source command completed; raw configuration output is withheld until native secret redaction is proven.",
                        L"來源指令已完成；原生機密遮罩驗證完成之前，唔會顯示原始設定輸出。" }.Pick(m_language);
                }
                else
                {
                    state.diagnostic = result.timed_out
                        ? winforge::core::LocalizedText{
                            L"Source query timed out. Raw diagnostic output was withheld because secret redaction is not yet proven.",
                            L"來源查詢逾時。機密遮罩未驗證完成，所以原始診斷輸出已隱藏。" }.Pick(m_language)
                        : winforge::core::LocalizedText{
                            L"Source query failed. Raw diagnostic output was withheld because secret redaction is not yet proven.",
                            L"來源查詢失敗。機密遮罩未驗證完成，所以原始診斷輸出已隱藏。" }.Pick(m_language);
                }
            }
            else if (action == winforge::core::packages::PackageAction::Details)
            {
                if (result.success)
                {
                    std::wstring output = std::move(result.standard_output);
                    if (!result.standard_error.empty())
                    {
                        if (!output.empty()) output += L"\n\n";
                        output += L"stderr:\n";
                        output += result.standard_error;
                    }
                    if (output.empty())
                    {
                        output = winforge::core::LocalizedText{
                            L"Details command completed without output.",
                            L"詳細資料指令已完成，但冇輸出。" }.Pick(m_language);
                    }
                    else
                    {
                        auto const detailFields =
                            winforge::core::packages::ParsePackageDetailsFields(ToUtf8(output));
                        if (!detailFields.empty())
                        {
                            std::wstring parsed = winforge::core::LocalizedText{
                                L"Parsed details fields:\n",
                                L"已解析詳細資料欄位：\n" }.Pick(m_language);
                            for (auto const& field : detailFields)
                            {
                                parsed += L"• ";
                                parsed += ToWideUtf8(field.label);
                                parsed += L": ";
                                parsed += ToWideUtf8(field.value);
                                parsed += L"\n";
                            }
                            parsed += winforge::core::LocalizedText{
                                L"\nRaw bounded command output:\n\n",
                                L"\n原始有界指令輸出：\n\n" }.Pick(m_language);
                            output = parsed + output;
                        }
                    }
                    if (!m_packageDetailsTarget.empty())
                    {
                        output = winforge::core::LocalizedText{
                            L"Details for " + m_packageDetailsTarget + L":\n\n",
                            L"「" + m_packageDetailsTarget + L"」詳細資料：\n\n" }.Pick(m_language) + output;
                    }
                    state.output = std::move(output);
                }
                else
                {
                    state.diagnostic = result.timed_out
                        ? winforge::core::LocalizedText{
                            L"Details query timed out.",
                            L"詳細資料查詢逾時。" }.Pick(m_language)
                        : result.cancelled
                            ? winforge::core::LocalizedText{
                                L"Details query was cancelled.",
                                L"詳細資料查詢已取消。" }.Pick(m_language)
                            : result.diagnostic.empty()
                                ? winforge::core::LocalizedText{
                                    L"Details query failed.",
                                    L"詳細資料查詢失敗。" }.Pick(m_language)
                                : std::move(result.diagnostic);
                }
            }
            else
            {
                state.diagnostic = std::move(result.diagnostic);
            }
            if (state.success)
            {
                ++successfulManagers;
                std::size_t visibleRows = 0;
                for (auto& package : result.packages)
                {
                    if (action == winforge::core::packages::PackageAction::Updates &&
                        IsPackageUpdateSuppressed(package))
                    {
                        ++ignoredRows;
                        continue;
                    }
                    ++visibleRows;
                    m_packageItems.push_back(std::move(package));
                }
                state.package_count = visibleRows;
            }
            else
            {
                ++failedManagers;
            }
            m_packageRunStates.push_back(std::move(state));
        }
        ApplyPackageSort();

        RecordPackageOperation(
            action == winforge::core::packages::PackageAction::Details
                ? L"Native details query completed: " + std::to_wstring(successfulManagers) +
                    L" manager(s) succeeded. · 原生詳細資料查詢完成。"
                : L"Native query completed: " + std::to_wstring(m_packageItems.size()) +
                    L" package row(s), " + std::to_wstring(successfulManagers) + L" manager(s) succeeded. · 原生查詢完成。");
        if (ignoredRows != 0)
        {
            RecordPackageOperation(
                L"Ignored-update filtering hid " + std::to_wstring(ignoredRows) +
                    L" update row(s). · 忽略更新篩選已隱藏相符資料列。");
        }
        RenderPackageManagerView();
        if (failedManagers != 0)
        {
            if (action == winforge::core::packages::PackageAction::Details)
            {
                AnnouncePackageStatus(
                    L"Package details query failed or was blocked. Review the engine status card.",
                    L"套件詳細資料查詢失敗或者受阻；請檢查引擎狀態卡。",
                    true);
            }
            else
            {
                AnnouncePackageStatus(
                    L"Package query finished with " + std::to_wstring(m_packageItems.size()) +
                        L" verified results. " + std::to_wstring(failedManagers) +
                        L" engines failed or still require resolution; review each engine status.",
                    L"套件查詢完成，有 " + std::to_wstring(m_packageItems.size()) +
                        L" 個已驗證結果。" + std::to_wstring(failedManagers) +
                        L" 個引擎失敗或者仍然需要解析；請檢查每個引擎狀態。",
                    true);
            }
        }
        else
        {
            if (action == winforge::core::packages::PackageAction::Details)
            {
                AnnouncePackageStatus(
                    L"Package details query finished successfully.",
                    L"套件詳細資料查詢成功完成。");
            }
            else
            {
                AnnouncePackageStatus(
                    L"Package query finished successfully with " + std::to_wstring(m_packageItems.size()) +
                        L" results.",
                    L"套件查詢成功完成，有 " + std::to_wstring(m_packageItems.size()) + L" 個結果。");
            }
        }
    }

    void MainWindow::RenderPackageManagerView()
    {
        if (!m_packageResults || !m_packageResultsHeader || !m_packagePrimaryAction ||
            !m_packageSecondaryAction || !m_packageSearchBox)
        {
            return;
        }

        m_packageResults.Children().Clear();
        auto pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };
        auto appendCard = [this](
            std::wstring_view title,
            std::wstring_view body,
            std::wstring_view automationId,
            double opacity = 1.0)
        {
            Border card;
            card.Padding(Thickness{ 16 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            card.Opacity(opacity);

            StackPanel content;
            content.Spacing(5);
            bool automationAssigned = false;
            if (!title.empty())
            {
                auto heading = CreateText(title, 14, true);
                if (!automationId.empty())
                {
                    AutomationProperties::SetAutomationId(heading, ToHString(automationId));
                    automationAssigned = true;
                }
                if (!body.empty()) AutomationProperties::SetHelpText(heading, ToHString(body));
                content.Children().Append(heading);
            }
            if (!body.empty())
            {
                auto note = CreateText(body, 12.5);
                note.TextWrapping(TextWrapping::Wrap);
                note.IsTextSelectionEnabled(true);
                if (!automationAssigned && !automationId.empty())
                    AutomationProperties::SetAutomationId(note, ToHString(automationId));
                content.Children().Append(note);
            }
            card.Child(content);
            m_packageResults.Children().Append(card);
        };

        auto const isDiscoverView = m_packageView == 0;
        m_packageSearchBox.IsEnabled(isDiscoverView && !m_packageWorking);
        if (m_packageDiscoverFilterPanel)
        {
            m_packageDiscoverFilterPanel.Visibility(
                isDiscoverView ? Visibility::Visible : Visibility::Collapsed);
        }
        if (m_packageSearchModePicker)
        {
            m_packageSearchModePicker.IsEnabled(isDiscoverView && !m_packageWorking);
            if (m_packageSearchModePicker.Items().Size() > 4)
            {
                auto const similar = m_packageSearchModePicker.Items().GetAt(4).try_as<ComboBoxItem>();
                if (similar)
                {
                    similar.IsEnabled(!m_packageDiscoverRegexEnabled);
                }
            }
        }
        if (m_packageSearchCaseSensitive)
        {
            m_packageSearchCaseSensitive.IsEnabled(isDiscoverView && !m_packageWorking);
        }
        if (m_packageSearchIgnoreSpecial)
        {
            m_packageSearchIgnoreSpecial.IsEnabled(
                isDiscoverView && !m_packageWorking && !m_packageDiscoverRegexEnabled);
        }
        if (m_packageDiscoverRegexMode)
        {
            m_packageDiscoverRegexMode.IsEnabled(isDiscoverView && !m_packageWorking);
        }
        if (m_packageDiscoverRegexBuilder)
        {
            m_packageDiscoverRegexBuilder.IsEnabled(isDiscoverView && !m_packageWorking);
        }
        if (m_packageDiscoverRegexApply)
        {
            m_packageDiscoverRegexApply.IsEnabled(
                isDiscoverView && !m_packageWorking && m_packageDiscoverRegexEnabled);
        }
        m_packagePrimaryAction.Visibility(Visibility::Visible);
        m_packageSecondaryAction.Visibility(Visibility::Collapsed);
        m_packagePrimaryAction.IsEnabled(false);
        m_packageSecondaryAction.IsEnabled(false);
        if (m_packageBusy) m_packageBusy.IsActive(m_packageWorking || m_packageMutationWorkerRunning.load());

        std::wstring header;
        std::wstring explanation;
        bool readOnlyQuery = false;
        switch (m_packageView)
        {
        case 0:
            m_packagePrimaryAction.Content(box_value(ToHString(m_packageDiscoverRegexEnabled
                ? pick(L"Regex filters cached results", L"正規篩選已快取結果")
                : pick(L"Search", L"搜尋"))));
            header = pick(L"Discover packages", L"搜尋套件");
            explanation = pick(
                m_packageDiscoverRegexEnabled
                    ? L"PCRE2 regex mode filters only the existing cached Discover rows. It is deliberately unable to start, receive, or forward a package-engine CLI or HTTPS query; switch back to literal search to run a new read-only query."
                    : L"Searches every selected, available engine concurrently using validated argv or an allowlisted HTTPS endpoint. UniGetUI-style filters refine cached rows; each result row can create a reviewed native install plan that still needs its own Confirm execution action.",
                m_packageDiscoverRegexEnabled
                    ? L"PCRE2 正規表示式模式只會篩選現有已快取 Discover 資料列。佢刻意唔可以開始、接收或者轉送套件引擎 CLI 或 HTTPS 查詢；要執行新只讀查詢，請切返文字搜尋。"
                    : L"會同時搜尋所有已選而且可用嘅引擎，只會用已驗證 argv 或准許清單內 HTTPS endpoint；UniGetUI 式篩選會整理已快取資料列；每個結果資料列都可以建立已檢視原生安裝計劃，但仲要由自己嘅「確認執行」動作先可以排隊。");
            readOnlyQuery = true;
            break;
        case 1:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            m_packageSecondaryAction.Content(box_value(ToHString(pick(L"Review Update all", L"檢視全部更新"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Available updates", L"可用更新");
            explanation = pick(
                L"Live update enumeration runs per selected engine. Each cached row can create a reviewed native update plan, or Review Update all can atomically stage up to 25 current unsuppressed rows. Every full redacted argv stays visible in Operations and one separate Confirm batch execution control is required before serial queueing.",
                L"會按已選引擎即時列出更新；每個已快取資料列都可以建立已檢視原生更新計劃，亦可以用「檢視全部更新」原子地暫存最多 25 個目前未隱藏資料列。每個完整已遮蔽 argv 都會喺操作檢視顯示，而且要由另一個「確認批次執行」控制先會串行排隊。");
            readOnlyQuery = true;
            break;
        case 2:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            header = pick(L"Installed packages", L"已安裝套件");
            explanation = pick(
                L"Installed packages are enumerated live. Each cached row can create a reviewed native uninstall plan requiring separate explicit confirmation; elevation-required requests fail closed and cancellation is available from the operation card.",
                L"會即時列出已安裝套件；每個已快取資料列都可以建立已檢視原生解除安裝計劃，而且要另外明確確認；需要提升權限嘅要求會 fail closed，亦可以由操作卡取消。");
            readOnlyQuery = true;
            break;
        case 3:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Export bundle", L"匯出 bundle"))));
            m_packageSecondaryAction.Content(box_value(ToHString(pick(L"Import bundle", L"匯入 bundle"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Portable package bundles", L"可攜套件清單");
            explanation = pick(
                L"Native bundle snapshots use an explicit metadata workspace, never the current query list. Import and export remain preview-only and cannot run package commands.",
                L"原生 bundle 快照會用明確 metadata 工作區，絕對唔會用目前查詢清單。匯入同匯出保持只供預覽，唔可以執行套件指令。");
            break;
        case 4:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            header = pick(L"Package sources", L"套件來源");
            explanation = pick(
                L"Source commands run read-only. Raw configuration is withheld until credential redaction is proven; add/remove operations remain disabled.",
                L"來源指令只讀執行；帳密遮罩驗證完成之前唔會顯示原始設定，新增／移除操作亦保持停用。");
            readOnlyQuery = true;
            break;
        case 5:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Clear rules", L"清除規則"))));
            header = pick(L"Ignored, pinned, and snoozed updates", L"已忽略、釘住同暫停嘅更新");
            explanation = pick(
                L"Native ignore, version-pin, and custom-duration snooze rules are persisted locally and hide matching rows from Updates results.",
                L"原生忽略、版本釘選同自訂時長暫停規則會本機保存，並喺更新結果隱藏相符資料列。");
            break;
        case 6:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Probe again", L"再次探測"))));
            header = pick(L"Engine setup", L"引擎設定");
            explanation = pick(
                L"Non-destructive probes show engine availability. Every native bootstrap uses a fixed Winget ID, opens a reviewed argv plan first, and needs a separate normal-integrity confirmation before execution; remote scripts and arbitrary commands are never accepted.",
                L"非破壞性探測會顯示邊啲引擎可用。每個原生 bootstrap 只會用固定 Winget ID，會先開已審閱 argv 計劃，執行前仍要喺正常 integrity 獨立確認；絕不接受遠端 script 或任意指令。");
            break;
        case 7:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Reset state", L"重設狀態"))));
            header = pick(L"Package Manager settings", L"套件管理設定");
            explanation = pick(
                L"Package-view, search, filter, and snooze-duration preferences now persist in native JSON state. Broader per-manager settings, backup, and restore remain gated.",
                L"套件檢視、搜尋、篩選同暫停時長偏好而家會保存喺原生 JSON 狀態。更完整嘅逐管理器設定、備份同還原仍然鎖住。");
            break;
        default:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            m_packageSecondaryAction.Content(box_value(ToHString(
                m_packageView == 8 ? pick(L"Clear preview history", L"清除預覽歷史") : pick(L"Clear completed", L"清除已完成"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Operation queue and history", L"操作佇列同歷史");
            explanation = pick(L"The native coordinator shows reviewed, queued, running, completed, failed, timed-out, cancelled, retry, and cancellation state here. Its detailed queue state and redacted reviewed argv stay in memory; a bounded redacted lifecycle event enters the existing history, while third-party stdout, stderr, and runtime diagnostics are withheld.",
                L"原生協調器會喺呢度顯示已檢視、排隊、執行中、完成、失敗、超時、已取消、重試同取消狀態。詳細佇列狀態同已遮罩已檢視 argv 只會留喺記憶體；有界、已遮罩嘅生命週期事件會寫入現有歷史，而第三方 stdout、stderr 同 runtime 診斷會略去。");
            break;
        }

        auto const selectedAvailable = HasSelectedAvailablePackageManager();
        auto const searchReady = m_packageView != 0 ||
            (!m_packageDiscoverRegexEnabled &&
                TrimPackageSearchText(ToWide(m_packageSearchBox.Text())).size() >= 2);
        if (readOnlyQuery)
        {
            m_packagePrimaryAction.IsEnabled(
                m_packageProbeComplete && selectedAvailable && searchReady && !m_packageWorking);
            if (m_packageView == 1)
            {
                m_packageSecondaryAction.IsEnabled(!m_packageWorking && !m_packageItems.empty());
            }
        }
        else if (m_packageView == 3 || m_packageView == 5 || m_packageView == 6 || m_packageView == 7)
        {
            if (m_packageView == 3)
            {
                auto const hasBundleRecords =
                    !m_packageBundleItems.empty() || !m_packageBundleIncompatibleItems.empty();
                m_packagePrimaryAction.IsEnabled(!m_packageWorking && hasBundleRecords);
                m_packageSecondaryAction.IsEnabled(!m_packageWorking);
            }
            else
            {
                m_packagePrimaryAction.IsEnabled(
                    !m_packageWorking && (m_packageView != 5 ||
                        !m_packageIgnoredRules.empty() ||
                        !m_packagePinnedRules.empty() ||
                        !m_packageSnoozedRules.empty()));
            }
        }
        else if (m_packageView == 8)
        {
            m_packageSecondaryAction.IsEnabled(!m_packageWorking && !m_packageOperations.empty());
        }

        std::vector<winforge::core::packages::PackageItem> filteredDiscoverItems;
        std::vector<winforge::core::packages::PackageItem> const* visibleItems = &m_packageItems;
        auto const isCachedDiscoverQuery = isDiscoverView &&
            m_packageLastAction == winforge::core::packages::PackageAction::Search;
        std::shared_ptr<winforge::core::regex::SafeRegex const> discoverExpression;
        bool discoverRegexValid = true;
        if (isDiscoverView && m_packageDiscoverRegexEnabled)
        {
            std::wstring diagnostic;
            discoverExpression = CompileSearchRegex(
                m_packageDiscoverRegexPattern,
                m_packageSearchCaseSensitiveValue,
                m_packageDiscoverRegexMultiline,
                m_packageDiscoverRegexDotMatchesNewline,
                diagnostic);
            m_packageDiscoverRegexDiagnostic = diagnostic;
            discoverRegexValid = static_cast<bool>(discoverExpression);
            if (m_packageDiscoverRegexStatus)
            {
                auto const status = discoverRegexValid
                    ? L"PCRE2 regex filters cached Discover results only; remote query epoch is unchanged. · PCRE2 正規表示式只篩選已快取 Discover 結果；遠端查詢 epoch 冇變。"
                    : diagnostic;
                m_packageDiscoverRegexStatus.Text(ToHString(status));
                AutomationProperties::SetName(m_packageDiscoverRegexStatus, ToHString(status));
            }
        }
        else if (m_packageDiscoverRegexStatus)
        {
            m_packageDiscoverRegexDiagnostic.clear();
            m_packageDiscoverRegexStatus.Text(L"Literal Discover filtering · 文字 Discover 篩選");
            AutomationProperties::SetName(m_packageDiscoverRegexStatus, L"Literal Discover filtering · 文字 Discover 篩選");
        }

        if (isCachedDiscoverQuery && discoverRegexValid)
        {
            filteredDiscoverItems = winforge::core::packages::FilterDiscoverPackageItems(
                m_packageDiscoverRegexEnabled
                    ? std::wstring_view(m_packageDiscoverRegexPattern)
                    : std::wstring_view(m_packageSearchText),
                m_packageItems,
                winforge::core::packages::PackageSearchOptions{
                    m_packageSearchMode,
                    m_packageSearchCaseSensitiveValue,
                    m_packageSearchIgnoreSpecialValue,
                    std::move(discoverExpression) });
            visibleItems = &filteredDiscoverItems;
        }
        auto const selectionAction = CurrentPackageSelectionAction();
        auto const selectedPackageItems = selectionAction
            ? SelectedPackageItems(*selectionAction)
            : std::vector<winforge::core::packages::PackageItem>{};

        auto const discoverSearchModeLabel = [this, &pick]()
        {
            switch (m_packageSearchMode)
            {
            case winforge::core::packages::PackageSearchMode::Name:
                return pick(L"Package name", L"套件名稱");
            case winforge::core::packages::PackageSearchMode::Id:
                return pick(L"Package ID", L"套件 ID");
            case winforge::core::packages::PackageSearchMode::Exact:
                return pick(L"Exact match", L"完全符合");
            case winforge::core::packages::PackageSearchMode::Similar:
                return pick(L"Show similar packages", L"顯示相似套件");
            case winforge::core::packages::PackageSearchMode::Both:
            default:
                return pick(L"Both name and ID", L"名稱同 ID");
            }
        };

        auto resultHeader = header;
        if (isCachedDiscoverQuery && !m_packageItems.empty())
        {
            resultHeader += L" · " + std::to_wstring(visibleItems->size());
            if (visibleItems->size() != m_packageItems.size())
            {
                resultHeader += L"/" + std::to_wstring(m_packageItems.size());
            }
            resultHeader += pick(L" results · ", L" 個結果 · ") + discoverSearchModeLabel();
            if (m_packageDiscoverRegexEnabled)
            {
                resultHeader += discoverRegexValid
                    ? pick(L" · PCRE2 local regex", L" · PCRE2 本機正規表示式")
                    : pick(L" · invalid regex (raw cache shown)", L" · 無效正規表示式（顯示原始快取）");
            }
            else if (m_packageSearchIgnoreSpecialValue)
            {
                resultHeader += pick(L" · ignoring special characters", L" · 忽略特殊字元");
            }
            if (m_packageSearchCaseSensitiveValue)
            {
                resultHeader += pick(L" · case-sensitive", L" · 區分大小寫");
            }
        }
        else if (!m_packageItems.empty())
        {
            resultHeader += L" · " + std::to_wstring(m_packageItems.size()) + pick(L" results", L" 個結果");
        }
        m_packageResultsHeader.Text(ToHString(resultHeader));
        appendCard(explanation, {}, L"NativePackageViewSummary", 0.88);
        if (isCachedDiscoverQuery && m_packageDiscoverRegexEnabled && !discoverRegexValid)
        {
            appendCard(
                pick(L"Regex needs correction", L"正規表示式需要修正"),
                m_packageDiscoverRegexDiagnostic +
                    pick(L" The raw cached list is retained and no package query was run.",
                        L" 已保留原始快取清單，而且冇執行套件查詢。"),
                L"NativePackageRegexInvalid");
        }

        if (selectionAction && !selectedPackageItems.empty())
        {
            auto const action = *selectionAction;
            auto const actionNameEn = action == winforge::core::packages::PackageAction::Install
                ? std::wstring(L"install")
                : action == winforge::core::packages::PackageAction::Update
                    ? std::wstring(L"update")
                    : std::wstring(L"uninstall");
            auto const actionNameZh = action == winforge::core::packages::PackageAction::Install
                ? std::wstring(L"安裝")
                : action == winforge::core::packages::PackageAction::Update
                    ? std::wstring(L"更新")
                    : std::wstring(L"解除安裝");
            auto const viewNameEn = action == winforge::core::packages::PackageAction::Install
                ? std::wstring(L"Discover")
                : action == winforge::core::packages::PackageAction::Update
                    ? std::wstring(L"Updates")
                    : std::wstring(L"Installed");
            auto const viewNameZh = action == winforge::core::packages::PackageAction::Install
                ? std::wstring(L"Discover")
                : action == winforge::core::packages::PackageAction::Update
                    ? std::wstring(L"更新")
                    : std::wstring(L"已安裝");
            auto const reviewAutomationId = action == winforge::core::packages::PackageAction::Install
                ? std::wstring(L"NativePackageBatchReviewInstall")
                : action == winforge::core::packages::PackageAction::Update
                    ? std::wstring(L"NativePackageBatchReviewUpdate")
                    : std::wstring(L"NativePackageBatchReviewUninstall");

            Border selectionCard;
            selectionCard.Padding(Thickness{ 16 });
            selectionCard.CornerRadius(CornerRadius{ 8 });
            selectionCard.BorderThickness(Thickness{ 1 });
            selectionCard.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            selectionCard.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

            StackPanel selectionContent;
            selectionContent.Spacing(7);
            auto selectionTitle = CreateText(
                std::to_wstring(selectedPackageItems.size()) +
                    pick(L" " + viewNameEn + L" result(s) selected", L" 個" + viewNameZh + L"結果已揀"),
                14,
                true);
            AutomationProperties::SetAutomationId(selectionTitle, L"NativePackageBatchSelectionSummary");
            selectionContent.Children().Append(selectionTitle);

            auto selectionNote = CreateText(
                pick(
                    L"Review selected atomically validates up to 25 cached rows and shows every full redacted native " + actionNameEn + L" argv in one Operations batch card. It never runs a package command; a later separate Confirm batch execution control is required, and the selection clears only after review succeeds.",
                    L"檢視所選會原子地驗證最多 25 個快取資料列，並喺一張操作批次卡顯示每個完整已遮蔽原生" + actionNameZh + L" argv。絕對唔會執行套件指令；之後仲要另一個「確認批次執行」控制，而且只會喺檢視成功之後清除選擇。"),
                12.5);
            selectionNote.TextWrapping(TextWrapping::Wrap);
            selectionContent.Children().Append(selectionNote);

            StackPanel selectionActions;
            selectionActions.Spacing(8);
            Button reviewSelected;
            reviewSelected.Content(box_value(ToHString(pick(
                L"Review selected " + actionNameEn + L" batch",
                L"檢視所選" + actionNameZh + L"批次"))));
            reviewSelected.Padding(Thickness{ 12, 6, 12, 6 });
            reviewSelected.IsEnabled(!m_packageWorking);
            AutomationProperties::SetAutomationId(reviewSelected, ToHString(reviewAutomationId));
            AutomationProperties::SetName(
                reviewSelected,
                ToHString(pick(
                    L"Review selected package " + actionNameEn + L" commands without executing them",
                    L"檢視所選套件" + actionNameZh + L"指令，唔會執行")));
            AutomationProperties::SetHelpText(
                reviewSelected,
                ToHString(pick(
                    L"Atomically validates up to 25 cached rows and opens a full reviewed argv batch. A separate explicit batch confirmation is required before any command queues.",
                    L"會原子地驗證最多 25 個快取資料列，並開啟完整已檢視 argv 批次。任何指令排隊之前，都需要另一個明確批次確認。")));
            reviewSelected.Click([this, action](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                PreviewSelectedPackageOperations(action);
            });
            selectionActions.Children().Append(reviewSelected);

            if (action == winforge::core::packages::PackageAction::Install ||
                action == winforge::core::packages::PackageAction::Uninstall)
            {
                Button addToBundle;
                addToBundle.Content(box_value(ToHString(pick(
                    L"Add selected to bundle",
                    L"加入所選到 bundle"))));
                addToBundle.Padding(Thickness{ 12, 6, 12, 6 });
                addToBundle.IsEnabled(!m_packageWorking);
                AutomationProperties::SetAutomationId(addToBundle, L"NativePackageBatchAddToBundle");
                AutomationProperties::SetName(
                    addToBundle,
                    ToHString(pick(
                        L"Add selected cached packages to the native bundle workspace",
                        L"將所選快取套件加入原生 bundle 工作區")));
                AutomationProperties::SetHelpText(
                    addToBundle,
                    ToHString(pick(
                        L"Copies metadata only. It never runs a package command.",
                        L"只會複製 metadata，絕對唔會執行套件指令。")));
                addToBundle.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                {
                    AddSelectedPackagesToBundle();
                });
                selectionActions.Children().Append(addToBundle);
            }

            Button clearSelection;
            clearSelection.Content(box_value(ToHString(pick(L"Clear selection", L"清除選擇"))));
            clearSelection.Padding(Thickness{ 12, 6, 12, 6 });
            clearSelection.IsEnabled(!m_packageWorking);
            AutomationProperties::SetAutomationId(clearSelection, L"NativePackageBatchClear");
            clearSelection.Click([this, viewNameEn, viewNameZh](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                ClearPackageSelection();
                RenderPackageManagerView();
                AnnouncePackageStatus(
                    viewNameEn + L" package selection cleared. No package command was executed.",
                    viewNameZh + L"套件選擇已清除；冇執行套件指令。");
            });
            selectionActions.Children().Append(clearSelection);
            selectionContent.Children().Append(selectionActions);
            selectionCard.Child(selectionContent);
            m_packageResults.Children().Append(selectionCard);
        }

        if (m_packageWorking || m_packageMutationWorkerRunning.load())
        {
            appendCard(
                pick(L"Native operation in progress", L"原生操作進行中"),
                pick(L"The page stays responsive while contained package processes and HTTPS requests run in the background. The mutation worker is serial and accepts explicit cancellation.",
                    L"受控套件 process 同 HTTPS request 喺背景執行期間，頁面會保持流暢；修改 worker 係串行，而且接受明確取消。"),
                L"NativePackageWorkingState");
        }
        else if (!m_packageProbeComplete)
        {
            appendCard(
                pick(L"Checking package engines", L"檢查套件引擎"),
                pick(L"Controls unlock only for engines whose non-destructive native probe succeeds.",
                    L"只有非破壞性原生探測成功嘅引擎先會解鎖控制項。"),
                L"NativePackageProbePending");
        }

        if (m_packageView == 6)
        {
            for (auto const& manager : winforge::core::packages::PackageManagers())
            {
                auto const key = std::wstring(manager.key);
                auto const label = winforge::core::LocalizedText{
                    std::wstring(manager.name_en), std::wstring(manager.name_zh) }.Pick(m_language);
                std::wstring status;
                if (!m_packageProbeComplete)
                {
                    status = pick(L"Probe pending…", L"等候探測…");
                }
                else if (m_packageManagersAvailable.contains(key) && m_packageManagersAvailable[key])
                {
                    status = pick(L"Available — read-only commands enabled.", L"可用 — 已啟用只讀指令。");
                }
                else
                {
                    status = m_packageProbeDiagnostics.contains(key)
                        ? TruncateForUi(m_packageProbeDiagnostics[key], 420)
                        : pick(L"Not available.", L"未可用。");
                }
                appendCard(label, status, L"NativePackageProbe_" + AutomationKey(key));
            }

            auto const wingetKey = std::wstring(L"winget");
            auto const wingetAvailable = m_packageProbeComplete &&
                m_packageManagersAvailable.contains(wingetKey) &&
                m_packageManagersAvailable[wingetKey];
            using SetupKind = winforge::core::packages::PackageSetupKind;
            using SetupReadiness = winforge::core::packages::PackageSetupReadiness;

            auto appendSetupCard = [this, &pick, wingetAvailable](
                winforge::core::packages::PackageSetupDescriptor const& descriptor,
                SetupReadiness readiness)
            {
                Border card;
                card.Padding(Thickness{ 16 });
                card.CornerRadius(CornerRadius{ 8 });
                card.BorderThickness(Thickness{ 1 });
                card.BorderBrush(Application::Current().Resources().Lookup(
                    box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
                card.Background(Application::Current().Resources().Lookup(
                    box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

                StackPanel content;
                content.Spacing(7);
                auto const title = pick(descriptor.name_en, descriptor.name_zh);
                auto const cardId = std::wstring(L"NativePackageSetup_") + AutomationKey(descriptor.key);
                auto heading = CreateText(title, 14, true);
                AutomationProperties::SetAutomationId(heading, ToHString(cardId));
                content.Children().Append(heading);

                std::wstring body;
                if (!m_packageProbeComplete)
                {
                    body = pick(
                        L"Engine probe is pending. Review controls remain disabled until availability is known.",
                        L"引擎探測仲未完成；未知道可用性之前，審閱控制會保持停用。");
                }
                else
                {
                    switch (readiness)
                    {
                    case SetupReadiness::AlreadyAvailable:
                        body = pick(
                            L"This engine is available. No bootstrap plan is offered.",
                            L"呢個引擎已經可用，唔會提供 bootstrap 計劃。");
                        break;
                    case SetupReadiness::ReadyToReview:
                        body = pick(
                            L"Fixed Winget package ID " + std::wstring(descriptor.winget_id) +
                                L" can be reviewed. It is not executed until a separate explicit confirmation in Operations.",
                            L"可審閱固定 Winget 套件 ID " + std::wstring(descriptor.winget_id) +
                                L"；Operations 入面仲要獨立明確確認先會執行。");
                        break;
                    case SetupReadiness::RequiresWinget:
                        body = pick(
                            L"Windows Package Manager is unavailable, so this safe native review plan cannot be constructed.",
                            L"Windows Package Manager 未可用，所以唔可以建立呢個安全原生審閱計劃。");
                        break;
                    case SetupReadiness::ManualOnly:
                        body = pick(
                            L"No native bootstrap command is offered for this engine. Remote scripts and arbitrary commands stay manual by design.",
                            L"呢個引擎唔會提供原生 bootstrap 指令。遠端 script 同任意指令按設計保持手動。");
                        break;
                    case SetupReadiness::UnknownEntry:
                    default:
                        body = pick(
                            L"This setup entry is unavailable.",
                            L"呢個 setup 項目未可用。");
                        break;
                    }
                }

                auto note = CreateText(body, 12.5);
                note.TextWrapping(TextWrapping::Wrap);
                note.IsTextSelectionEnabled(true);
                AutomationProperties::SetHelpText(heading, ToHString(body));
                content.Children().Append(note);

                auto const setupPackage = winforge::core::packages::BuildPackageSetupPackage(descriptor.key);
                if (setupPackage)
                {
                    Button review;
                    review.Content(box_value(ToHString(pick(L"Review safe install", L"審閱安全安裝"))));
                    review.Padding(Thickness{ 12, 6, 12, 6 });
                    review.IsEnabled(
                        m_packageProbeComplete &&
                        wingetAvailable &&
                        readiness == SetupReadiness::ReadyToReview &&
                        !m_packageWorking);
                    auto const reviewId = std::wstring(L"NativePackageSetupReview_") + AutomationKey(descriptor.key);
                    AutomationProperties::SetAutomationId(review, ToHString(reviewId));
                    AutomationProperties::SetName(
                        review,
                        ToHString(pick(
                            L"Review safe Winget install for " + title,
                            L"審閱 " + title + L" 嘅安全 Winget 安裝")));
                    AutomationProperties::SetHelpText(
                        review,
                        ToHString(pick(
                            L"Creates a redacted reviewed argv plan only. A separate explicit confirmation is required before normal-integrity execution.",
                            L"只會建立已遮蔽嘅 argv 審閱計劃；正常 integrity 執行之前仍要獨立明確確認。")));
                    review.Click([this, package = *setupPackage](
                        Windows::Foundation::IInspectable const&,
                        RoutedEventArgs const&)
                    {
                        RequestPackageMutation(package, winforge::core::packages::PackageAction::Install);
                    });
                    content.Children().Append(review);
                }

                card.Child(content);
                m_packageResults.Children().Append(card);
            };

            appendCard(
                pick(L"Safe engine bootstrap", L"安全引擎 bootstrap"),
                pick(
                    L"Only an immutable Winget allowlist is reviewable. Scoop, App Installer, PowerShell Gallery, and vcpkg retain manual setup because WinForge never downloads or executes bootstrap scripts.",
                    L"只可以審閱固定 Winget allowlist。Scoop、App Installer、PowerShell Gallery 同 vcpkg 保持手動 setup，因為 WinForge 絕不下載或執行 bootstrap script。"),
                L"NativePackageSetupPolicy",
                0.9);

            for (auto const& descriptor : winforge::core::packages::PackageSetupEntries())
            {
                if (descriptor.kind != SetupKind::ManagerBootstrap)
                {
                    continue;
                }

                auto targetAvailable = false;
                if (!descriptor.target_manager_key.empty())
                {
                    auto const targetKey = std::wstring(descriptor.target_manager_key);
                    targetAvailable = m_packageProbeComplete &&
                        m_packageManagersAvailable.contains(targetKey) &&
                        m_packageManagersAvailable[targetKey];
                }
                appendSetupCard(
                    descriptor,
                    winforge::core::packages::EvaluatePackageSetupEntry(
                        descriptor,
                        wingetAvailable,
                        targetAvailable));
            }

            appendCard(
                pick(L"Curated common dependencies", L"精選常用依賴"),
                pick(
                    L"These 14 managed-parity package IDs are fixed. This view intentionally does not claim they are installed; review an individual plan or the bounded batch, then confirm separately in Operations.",
                    L"呢 14 個同 managed parity 對齊嘅套件 ID 都係固定。呢個檢視唔會聲稱已安裝；可以審閱單一計劃或有限 batch，然後去 Operations 獨立確認。"),
                L"NativePackageSetupDependencies",
                0.9);

            std::vector<std::wstring_view> curatedKeys;
            curatedKeys.reserve(winforge::core::packages::PackageSetupEntries().size());
            for (auto const& descriptor : winforge::core::packages::PackageSetupEntries())
            {
                if (descriptor.kind != SetupKind::CuratedDependency)
                {
                    continue;
                }
                curatedKeys.push_back(descriptor.key);
                appendSetupCard(
                    descriptor,
                    winforge::core::packages::EvaluatePackageSetupEntry(descriptor, wingetAvailable, false));
            }

            auto const curatedPackages = winforge::core::packages::BuildPackageSetupPackages(curatedKeys);
            Border curatedBatchCard;
            curatedBatchCard.Padding(Thickness{ 16 });
            curatedBatchCard.CornerRadius(CornerRadius{ 8 });
            curatedBatchCard.BorderThickness(Thickness{ 1 });
            curatedBatchCard.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            curatedBatchCard.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());
            StackPanel curatedBatchContent;
            curatedBatchContent.Spacing(7);
            auto curatedBatchTitle = CreateText(
                pick(L"Review curated dependency batch", L"審閱精選依賴 batch"),
                14,
                true);
            AutomationProperties::SetAutomationId(curatedBatchTitle, L"NativePackageSetupBatch");
            curatedBatchContent.Children().Append(curatedBatchTitle);
            auto curatedBatchNote = CreateText(
                pick(
                    L"All fixed curated dependencies fit below the 25-command batch bound. Reviewing remains inert; every redacted argv stays visible before one separate Confirm batch action can queue serial normal-integrity work.",
                    L"所有固定精選依賴都喺 25 條指令 batch 上限之內。審閱仍然係惰性操作；每條已遮蔽 argv 都會先顯示，之後仲要一個獨立 Confirm batch 先可以排隊做正常 integrity 工作。"),
                12.5);
            curatedBatchNote.TextWrapping(TextWrapping::Wrap);
            curatedBatchContent.Children().Append(curatedBatchNote);
            Button reviewCuratedBatch;
            reviewCuratedBatch.Content(box_value(ToHString(pick(
                L"Review all curated dependencies",
                L"審閱全部精選依賴"))));
            reviewCuratedBatch.Padding(Thickness{ 12, 6, 12, 6 });
            reviewCuratedBatch.IsEnabled(
                m_packageProbeComplete &&
                wingetAvailable &&
                !m_packageWorking &&
                !curatedPackages.empty());
            AutomationProperties::SetAutomationId(reviewCuratedBatch, L"NativePackageSetupReviewCuratedBatch");
            AutomationProperties::SetName(
                reviewCuratedBatch,
                ToHString(pick(
                    L"Review all curated Winget dependency installs without executing them",
                    L"審閱全部精選 Winget 依賴安裝，但唔會執行")));
            AutomationProperties::SetHelpText(
                reviewCuratedBatch,
                ToHString(pick(
                    L"Builds one bounded reviewed batch only. It cannot start a package command until a later explicit confirmation.",
                    L"只會建立一個有限嘅審閱 batch。之後未明確確認之前，唔會開始任何套件指令。")));
            reviewCuratedBatch.Click([this, curatedPackages](
                Windows::Foundation::IInspectable const&,
                RoutedEventArgs const&)
            {
                ReviewPackageMutationBatch(
                    curatedPackages,
                    winforge::core::packages::PackageAction::Install,
                    L"curated Winget dependency",
                    L"精選 Winget 依賴");
            });
            curatedBatchContent.Children().Append(reviewCuratedBatch);
            curatedBatchCard.Child(curatedBatchContent);
            m_packageResults.Children().Append(curatedBatchCard);

            appendCard(
                pick(L"UniGetUI source provenance", L"UniGetUI 原始碼來源"),
                pick(
                    L"The complete pinned MIT UniGetUI source snapshot is vendored under ThirdParty/UniGetUI for audit and native-porting reference. WinForge never launches or embeds the upstream executable.",
                    L"完整固定嘅 MIT UniGetUI 原始碼快照已放喺 ThirdParty/UniGetUI，畀審核同原生移植參考。WinForge 絕不啟動或嵌入上游執行檔。"),
                L"NativePackageSetupUniGetUIProvenance",
                0.9);
            return;
        }

        if (m_packageView == 8)
        {
            auto const mutationRecords = m_packageMutationCoordinator.Snapshot();
            auto const mutationBatches = m_packageMutationCoordinator.SnapshotBatches();
            auto const mutationStateLabel = [&pick](winforge::core::packages::PackageMutationState state)
            {
                using State = winforge::core::packages::PackageMutationState;
                switch (state)
                {
                case State::AwaitingConsent: return pick(L"Awaiting explicit consent", L"等緊明確確認");
                case State::Queued: return pick(L"Queued", L"已排隊");
                case State::Running: return pick(L"Running", L"執行中");
                case State::Succeeded: return pick(L"Succeeded", L"已完成");
                case State::Failed: return pick(L"Failed", L"失敗");
                case State::Cancelled: return pick(L"Cancelled", L"已取消");
                case State::TimedOut: return pick(L"Timed out", L"已超時");
                case State::Rejected: return pick(L"Rejected", L"已拒絕");
                }
                return pick(L"Rejected", L"已拒絕");
            };
            appendCard(
                pick(L"Native mutation consent policy", L"原生修改確認政策"),
                pick(L"A package row first creates a redacted reviewed argv plan. A selected-row or Update all batch validates every command atomically, caps the visible review at 25 commands, and needs one separate Confirm batch execution control before serial normal-integrity queueing. The in-memory coordinator keeps those previews with request and lifecycle metadata; a bounded redacted lifecycle event is added to the existing history. Elevation, hooks, unsafe IDs, custom mutation arguments, and overlong previews fail closed. Third-party stdout, stderr, and runtime diagnostics are withheld.",
                    L"套件資料列會先建立已遮蔽嘅已檢視 argv 計劃。所選資料列或者全部更新批次會原子地驗證每個指令、將可見檢視限制喺 25 個指令，而且串行正常 integrity 排隊之前需要一個獨立嘅「確認批次執行」控制。記憶體協調器會保留呢啲預覽連同要求同生命週期 metadata；現有歷史會加入有界、已遮蔽嘅生命週期事件。提升權限、hooks、唔安全 ID、自訂修改參數同過長預覽都會 fail closed；第三方 stdout、stderr 同執行時診斷絕對唔會保留。"),
                L"NativePackageQueueSummary",
                0.9);

            appendCard(
                pick(L"Native batch consent policy", L"原生批次確認政策"),
                pick(L"A batch card exposes every redacted command preview before it can queue. Confirming it queues the displayed commands together and in order; cancelling stops the active command and cancels the rest. A retry returns only failed, timed-out, or cancelled commands to fresh batch review, never successful commands.",
                    L"批次卡會喺指令可以排隊之前展示每個已遮蔽指令預覽。確認會一齊而且按次序排入顯示嘅指令；取消會停止執行中指令並取消其餘指令。重試只會將失敗、超時或者已取消指令返回全新批次檢視，成功指令絕對唔會重播。"),
                L"NativePackageBatchConsentPolicy",
                0.9);

            if (m_packageOperations.empty() && mutationRecords.empty() && mutationBatches.empty())
            {
                appendCard(
                    pick(L"No native operations yet", L"暫時未有原生操作"),
                    pick(L"Availability probes, read-only queries, and reviewed package plans will appear here. A reviewed plan never runs until its own Confirm execution control is invoked.",
                        L"可用性探測、只讀查詢同已檢視嘅套件計劃會喺度顯示。已檢視計劃絕對唔會執行，除非佢自己嘅「確認執行」控制被呼叫。"),
                    L"NativePackageOperationsEmpty");
            }
            else
            {
                for (auto const& batch : mutationBatches)
                {
                    auto const batchId = batch.id;
                    auto const batchKey = AutomationKey(batchId);
                    auto const hasRetryableChild = std::any_of(
                        batch.records.begin(),
                        batch.records.end(),
                        [](winforge::core::packages::PackageMutationRecord const& record)
                        {
                            using State = winforge::core::packages::PackageMutationState;
                            return record.state == State::Failed ||
                                record.state == State::TimedOut ||
                                record.state == State::Cancelled;
                        });

                    Border card;
                    card.Padding(Thickness{ 16, 12, 16, 12 });
                    card.CornerRadius(CornerRadius{ 8 });
                    card.BorderThickness(Thickness{ 1 });
                    card.BorderBrush(Application::Current().Resources().Lookup(
                        box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
                    card.Background(Application::Current().Resources().Lookup(
                        box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

                    StackPanel content;
                    content.Spacing(7);
                    auto title = CreateText(
                        pick(L"Reviewed batch · ", L"已檢視批次 · ") +
                            std::to_wstring(batch.records.size()) +
                            pick(L" package command(s)", L" 個套件指令"),
                        14,
                        true);
                    AutomationProperties::SetAutomationId(
                        title,
                        ToHString(L"NativePackageMutationBatch_" + batchKey));
                    AutomationProperties::SetName(
                        title,
                        ToHString(pick(
                            L"Reviewed package batch with " + std::to_wstring(batch.records.size()) + L" commands",
                            L"已檢視套件批次，有 " + std::to_wstring(batch.records.size()) + L" 個指令")));
                    AutomationProperties::SetHelpText(title, ToHString(batch.diagnostic));
                    content.Children().Append(title);

                    std::wstring summary = mutationStateLabel(batch.state) +
                        pick(L" · commands: ", L" · 指令：") +
                        std::to_wstring(batch.records.size());
                    if (batch.retry_count > 0)
                    {
                        summary += pick(L" · retries: ", L" · 重試：") +
                            std::to_wstring(batch.retry_count);
                    }
                    if (batch.cancellation_requested)
                    {
                        summary += pick(L" · cancellation requested", L" · 已要求取消");
                    }
                    if (!batch.diagnostic.empty())
                    {
                        summary += L"\n" + batch.diagnostic;
                    }
                    auto details = CreateText(summary, 12.5);
                    details.TextWrapping(TextWrapping::Wrap);
                    details.IsTextSelectionEnabled(true);
                    content.Children().Append(details);

                    auto reviewedHeader = CreateText(
                        pick(L"Full redacted reviewed commands", L"完整已遮蔽已檢視指令"),
                        12.5,
                        true);
                    content.Children().Append(reviewedHeader);

                    for (std::size_t childIndex = 0; childIndex < batch.records.size(); ++childIndex)
                    {
                        auto const& child = batch.records[childIndex];
                        auto const childActionEn = child.request.action == winforge::core::packages::PackageAction::Install
                            ? std::wstring(L"Install")
                            : child.request.action == winforge::core::packages::PackageAction::Update
                                ? std::wstring(L"Update")
                                : std::wstring(L"Uninstall");
                        auto const childActionZh = child.request.action == winforge::core::packages::PackageAction::Install
                            ? std::wstring(L"安裝")
                            : child.request.action == winforge::core::packages::PackageAction::Update
                                ? std::wstring(L"更新")
                                : std::wstring(L"解除安裝");
                        auto childLabel = CreateText(
                            L"#" + std::to_wstring(childIndex + 1) + L" · " +
                                pick(childActionEn, childActionZh) + L" · " +
                                child.request.package.manager_key,
                            12.0,
                            true);
                        childLabel.TextWrapping(TextWrapping::Wrap);
                        content.Children().Append(childLabel);

                        // SubmitBatch rejects overlong previews, so this is the
                        // complete reviewed/redacted argv rather than a
                        // truncated UI summary. It remains selectable for
                        // careful inspection before batch consent.
                        auto childPreview = CreateText(child.command_preview, 12.5);
                        childPreview.TextWrapping(TextWrapping::Wrap);
                        childPreview.IsTextSelectionEnabled(true);
                        AutomationProperties::SetAutomationId(
                            childPreview,
                            ToHString(L"NativePackageMutationBatchPreview_" + batchKey + L"_" +
                                std::to_wstring(childIndex + 1)));
                        AutomationProperties::SetName(childPreview, ToHString(child.command_preview));
                        AutomationProperties::SetHelpText(
                            childPreview,
                            ToHString(mutationStateLabel(child.state) + L"\n" + child.diagnostic));
                        content.Children().Append(childPreview);

                        auto childState = CreateText(
                            mutationStateLabel(child.state) +
                                (child.diagnostic.empty() ? std::wstring{} : L" · " + child.diagnostic),
                            11.5);
                        childState.TextWrapping(TextWrapping::Wrap);
                        content.Children().Append(childState);
                    }

                    StackPanel actions;
                    // Vertical controls intentionally avoid narrow-window
                    // clipping, particularly where localized captions grow.
                    actions.Spacing(8);
                    using State = winforge::core::packages::PackageMutationState;
                    if (batch.state == State::AwaitingConsent)
                    {
                        Button confirm;
                        confirm.Content(box_value(ToHString(pick(L"Confirm batch execution", L"確認批次執行"))));
                        confirm.Padding(Thickness{ 12, 6, 12, 6 });
                        AutomationProperties::SetAutomationId(
                            confirm,
                            ToHString(L"NativePackageMutationBatchConfirm_" + batchKey));
                        AutomationProperties::SetName(
                            confirm,
                            ToHString(pick(
                                L"Confirm all " + std::to_wstring(batch.records.size()) + L" reviewed package commands in this batch",
                                L"確認呢個批次全部 " + std::to_wstring(batch.records.size()) + L" 個已檢視套件指令")));
                        AutomationProperties::SetHelpText(
                            confirm,
                            ToHString(pick(
                                L"Queues every displayed reviewed command together in serial order. It can run only at normal integrity.",
                                L"會一齊按串行次序排入每個顯示嘅已檢視指令；只可以喺正常 integrity 執行。")));
                        confirm.Click([this, batchId](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                        {
                            ConfirmPackageMutationBatch(batchId);
                        });
                        actions.Children().Append(confirm);
                    }
                    if (batch.state == State::AwaitingConsent || batch.state == State::Queued || batch.state == State::Running)
                    {
                        Button cancel;
                        cancel.Content(box_value(ToHString(pick(L"Cancel batch", L"取消批次"))));
                        cancel.Padding(Thickness{ 12, 6, 12, 6 });
                        AutomationProperties::SetAutomationId(
                            cancel,
                            ToHString(L"NativePackageMutationBatchCancel_" + batchKey));
                        AutomationProperties::SetName(
                            cancel,
                            ToHString(pick(L"Cancel this package command batch", L"取消呢個套件指令批次")));
                        AutomationProperties::SetHelpText(
                            cancel,
                            ToHString(pick(
                                L"Stops an active command through its contained cancellation token and cancels every remaining command before execution.",
                                L"會用受控取消 token 停止執行中指令，並喺執行之前取消每個其餘指令。")));
                        cancel.Click([this, batchId](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                        {
                            CancelPackageMutationBatch(batchId);
                        });
                        actions.Children().Append(cancel);
                    }
                    if (hasRetryableChild)
                    {
                        Button retry;
                        retry.Content(box_value(ToHString(pick(L"Review unsuccessful commands again", L"再次檢視未成功指令"))));
                        retry.Padding(Thickness{ 12, 6, 12, 6 });
                        AutomationProperties::SetAutomationId(
                            retry,
                            ToHString(L"NativePackageMutationBatchRetry_" + batchKey));
                        AutomationProperties::SetName(
                            retry,
                            ToHString(pick(
                                L"Return only unsuccessful package commands to fresh batch review",
                                L"只將未成功套件指令返回全新批次檢視")));
                        retry.Click([this, batchId](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                        {
                            RetryPackageMutationBatch(batchId);
                        });
                        actions.Children().Append(retry);
                    }
                    if (actions.Children().Size() > 0)
                    {
                        content.Children().Append(actions);
                    }
                    card.Child(content);
                    m_packageResults.Children().Append(card);
                }

                for (auto const& mutation : mutationRecords)
                {
                    // Batch children are surfaced together on their parent card.
                    // Giving them individual controls would let a caller bypass
                    // the one explicit, atomic batch-consent contract.
                    if (!mutation.batch_id.empty())
                    {
                        continue;
                    }
                    auto const actionEn = mutation.request.action == winforge::core::packages::PackageAction::Install
                        ? std::wstring(L"Install")
                        : mutation.request.action == winforge::core::packages::PackageAction::Update
                            ? std::wstring(L"Update")
                            : std::wstring(L"Uninstall");
                    auto const actionZh = mutation.request.action == winforge::core::packages::PackageAction::Install
                        ? std::wstring(L"安裝")
                        : mutation.request.action == winforge::core::packages::PackageAction::Update
                            ? std::wstring(L"更新")
                            : std::wstring(L"解除安裝");
                    auto const stateLabel = mutationStateLabel(mutation.state);
                    auto const mutationId = mutation.request.id;

                    Border card;
                    card.Padding(Thickness{ 16, 12, 16, 12 });
                    card.CornerRadius(CornerRadius{ 8 });
                    card.BorderThickness(Thickness{ 1 });
                    card.BorderBrush(Application::Current().Resources().Lookup(
                        box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
                    card.Background(Application::Current().Resources().Lookup(
                        box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

                    StackPanel content;
                    content.Spacing(6);
                    auto title = CreateText(
                        pick(actionEn + L" plan · ", actionZh + L"計劃 · ") +
                            (mutation.request.package.name.empty()
                                ? mutation.request.package.id
                                : mutation.request.package.name),
                        14,
                        true);
                    AutomationProperties::SetAutomationId(
                        title,
                        ToHString(L"NativePackageMutationOperation_" + AutomationKey(mutationId)));
                    AutomationProperties::SetHelpText(
                        title,
                        ToHString(mutation.command_preview + L"\n" + mutation.diagnostic));
                    content.Children().Append(title);

                    std::wstring body = stateLabel + L" · " + mutation.request.package.manager_key;
                    if (mutation.exit_code)
                    {
                        body += L" · exit " + std::to_wstring(*mutation.exit_code);
                    }
                    if (mutation.retry_count > 0)
                    {
                        body += pick(L" · retries: ", L" · 重試：") + std::to_wstring(mutation.retry_count);
                    }
                    body += L"\n" + mutation.command_preview;
                    if (!mutation.diagnostic.empty())
                    {
                        body += L"\n" + mutation.diagnostic;
                    }
                    auto details = CreateText(TruncateForUi(body, 1800), 12.5);
                    details.TextWrapping(TextWrapping::Wrap);
                    details.IsTextSelectionEnabled(true);
                    content.Children().Append(details);

                    StackPanel actions;
                    actions.Orientation(Orientation::Vertical);
                    actions.Spacing(8);
                    using State = winforge::core::packages::PackageMutationState;
                    if (mutation.state == State::AwaitingConsent)
                    {
                        Button confirm;
                        confirm.Content(box_value(ToHString(pick(L"Confirm execution", L"確認執行"))));
                        AutomationProperties::SetAutomationId(
                            confirm,
                            ToHString(L"NativePackageMutationConfirm_" + AutomationKey(mutationId)));
                        AutomationProperties::SetName(
                            confirm,
                            ToHString(pick(
                                L"Confirm " + actionEn + L" execution for " + mutation.request.package.id,
                                L"確認" + actionZh + L"執行：" + mutation.request.package.id)));
                        AutomationProperties::SetHelpText(
                            confirm,
                            ToHString(pick(
                                L"Queues this one reviewed package command. It may only run at normal integrity and can be cancelled from this card.",
                                L"會排隊呢一個已檢視套件指令。佢只可以喺正常 integrity 執行，而且可以由呢張卡取消。")));
                        confirm.Click([this, mutationId](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                        {
                            ConfirmPackageMutation(mutationId);
                        });
                        actions.Children().Append(confirm);
                    }
                    if (mutation.state == State::AwaitingConsent || mutation.state == State::Queued || mutation.state == State::Running)
                    {
                        Button cancel;
                        cancel.Content(box_value(ToHString(pick(L"Cancel", L"取消"))));
                        AutomationProperties::SetAutomationId(
                            cancel,
                            ToHString(L"NativePackageMutationCancel_" + AutomationKey(mutationId)));
                        AutomationProperties::SetName(
                            cancel,
                            ToHString(pick(
                                L"Cancel package operation for " + mutation.request.package.id,
                                L"取消套件操作：" + mutation.request.package.id)));
                        cancel.Click([this, mutationId](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                        {
                            CancelPackageMutation(mutationId);
                        });
                        actions.Children().Append(cancel);
                    }
                    if (winforge::core::packages::IsTerminalPackageMutationState(mutation.state) &&
                        mutation.state != State::Rejected)
                    {
                        Button retry;
                        retry.Content(box_value(ToHString(pick(L"Request retry", L"要求重試"))));
                        AutomationProperties::SetAutomationId(
                            retry,
                            ToHString(L"NativePackageMutationRetry_" + AutomationKey(mutationId)));
                        AutomationProperties::SetName(
                            retry,
                            ToHString(pick(
                                L"Request a retry that will require fresh explicit consent",
                                L"要求重試；會需要重新明確確認")));
                        retry.Click([this, mutationId](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                        {
                            RetryPackageMutation(mutationId);
                        });
                        actions.Children().Append(retry);
                    }
                    if (actions.Children().Size() > 0)
                    {
                        content.Children().Append(actions);
                    }
                    card.Child(content);
                    m_packageResults.Children().Append(card);
                }

                if (!m_packageOperations.empty())
                {
                    appendCard(
                        pick(L"Preview and read-only history", L"預覽同只讀歷史"),
                        pick(L"These durable history entries are separate from the consent-gated mutation queue above.",
                            L"呢啲可保存歷史項目同上面需要確認嘅修改佇列係分開嘅。"),
                        L"NativePackagePreviewHistory");
                }
                for (std::size_t index = 0; index < m_packageOperations.size(); ++index)
                {
                    auto const& operation = m_packageOperations[index];

                    Border card;
                    card.Padding(Thickness{ 16, 12, 16, 12 });
                    card.CornerRadius(CornerRadius{ 8 });
                    card.BorderThickness(Thickness{ 1 });
                    card.BorderBrush(Application::Current().Resources().Lookup(
                        box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
                    card.Background(Application::Current().Resources().Lookup(
                        box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

                    StackPanel content;
                    content.Spacing(6);

                    auto title = CreateText(
                        L"#" + std::to_wstring(index + 1) + L" · " + operation.title,
                        14,
                        true);
                    AutomationProperties::SetAutomationId(
                        title,
                        ToHString(L"NativePackageOperation_" + std::to_wstring(index)));
                    AutomationProperties::SetHelpText(title, ToHString(operation.details));
                    content.Children().Append(title);

                    std::wstring body = operation.status;
                    body += L" · created ";
                    body += FormatRuleTime(operation.created_epoch_seconds);
                    if (operation.retry_count > 0)
                    {
                        body += L" · retry previews: ";
                        body += std::to_wstring(operation.retry_count);
                    }
                    body += L"\n";
                    body += TruncateForUi(operation.details, 600);
                    auto details = CreateText(body, 12.5);
                    details.TextWrapping(TextWrapping::Wrap);
                    details.IsTextSelectionEnabled(true);
                    content.Children().Append(details);

                    StackPanel actions;
                    actions.Orientation(Orientation::Vertical);
                    actions.Spacing(8);

                    Button runNext;
                    runNext.Content(box_value(ToHString(pick(L"Run next", L"下一個"))));
                    runNext.IsEnabled(index != 0 && !m_packageWorking);
                    AutomationProperties::SetAutomationId(
                        runNext,
                        ToHString(L"NativePackageOperationRunNext_" + std::to_wstring(index)));
                    AutomationProperties::SetName(
                        runNext,
                        ToHString(pick(L"Move preview operation to the front of the queue",
                            L"將預覽操作移到佇列最前")));
                    runNext.Click([this, index](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                    {
                        MovePackageOperation(index, true);
                    });
                    actions.Children().Append(runNext);

                    Button runLast;
                    runLast.Content(box_value(ToHString(pick(L"Run last", L"最後執行"))));
                    runLast.IsEnabled(index + 1 < m_packageOperations.size() && !m_packageWorking);
                    AutomationProperties::SetAutomationId(
                        runLast,
                        ToHString(L"NativePackageOperationRunLast_" + std::to_wstring(index)));
                    AutomationProperties::SetName(
                        runLast,
                        ToHString(pick(L"Move preview operation to the end of the queue",
                            L"將預覽操作移到佇列最後")));
                    runLast.Click([this, index](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                    {
                        MovePackageOperation(index, false);
                    });
                    actions.Children().Append(runLast);

                    Button retry;
                    retry.Content(box_value(ToHString(pick(L"Retry preview", L"重試預覽"))));
                    retry.IsEnabled(!m_packageWorking);
                    AutomationProperties::SetAutomationId(
                        retry,
                        ToHString(L"NativePackageOperationRetry_" + std::to_wstring(index)));
                    AutomationProperties::SetName(
                        retry,
                        ToHString(pick(L"Queue a preview-only retry without executing the package command",
                            L"排入只供預覽嘅重試；唔會執行套件指令")));
                    retry.Click([this, index](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                    {
                        RetryPackageOperation(index);
                    });
                    actions.Children().Append(retry);

                    content.Children().Append(actions);
                    card.Child(content);
                    m_packageResults.Children().Append(card);
                }
            }
            return;
        }

        if (m_packageView == 5)
        {
            if (m_packageIgnoredRules.empty() && m_packagePinnedRules.empty() && m_packageSnoozedRules.empty())
            {
                appendCard(
                    pick(L"No pinned, snoozed, or ignored updates", L"未有釘選、暫停或忽略更新"),
                    pick(
                        L"Run an Updates query, then use Ignore, Pin version, or Snooze on an update row to persist a local rule.",
                        L"執行更新查詢，然後喺更新資料列使用「忽略」、「釘選版本」或者「暫停」，即可保存本機規則。"),
                    L"NativePackageIgnoredEmpty");
            }
            else
            {
                auto managerNameFor = [this](std::wstring const& managerKey)
                {
                    auto const* descriptor = winforge::core::packages::FindPackageManager(managerKey);
                    return descriptor
                        ? winforge::core::LocalizedText{
                            std::wstring(descriptor->name_en), std::wstring(descriptor->name_zh) }.Pick(m_language)
                        : managerKey;
                };

                auto appendRuleCard = [this, &pick](
                    std::wstring titleText,
                    std::wstring bodyText,
                    std::wstring automationId,
                    std::wstring removeAutomationId,
                    std::wstring removeLabel,
                    std::function<void()> removeAction)
                {
                    Border card;
                    card.Padding(Thickness{ 16, 12, 16, 12 });
                    card.CornerRadius(CornerRadius{ 8 });
                    card.BorderThickness(Thickness{ 1 });
                    card.BorderBrush(Application::Current().Resources().Lookup(
                        box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
                    card.Background(Application::Current().Resources().Lookup(
                        box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

                    StackPanel content;
                    content.Spacing(6);
                    auto title = CreateText(titleText, 14, true);
                    AutomationProperties::SetAutomationId(
                        title,
                        ToHString(automationId));
                    content.Children().Append(title);

                    auto detail = CreateText(bodyText, 12.5);
                    detail.TextWrapping(TextWrapping::Wrap);
                    detail.IsTextSelectionEnabled(true);
                    content.Children().Append(detail);

                    Button remove;
                    remove.Content(box_value(ToHString(removeLabel)));
                    remove.HorizontalAlignment(HorizontalAlignment::Left);
                    AutomationProperties::SetAutomationId(
                        remove,
                        ToHString(removeAutomationId));
                    remove.Click([removeAction = std::move(removeAction)](
                        Windows::Foundation::IInspectable const&,
                        RoutedEventArgs const&)
                    {
                        removeAction();
                    });
                    content.Children().Append(remove);
                    card.Child(content);
                    m_packageResults.Children().Append(card);
                };

                for (auto const& rule : m_packageIgnoredRules)
                {
                    auto const managerName = managerNameFor(rule.manager_key);
                    std::wstring body = pick(L"Ignore all versions", L"忽略所有版本") +
                        L"  ·  " + managerName + L"  ·  " + rule.package_id;
                    if (!rule.version.empty()) body += L"  ·  " + pick(L"Last seen", L"上次見到") + L" " + rule.version;
                    auto managerKey = rule.manager_key;
                    auto packageId = rule.package_id;
                    appendRuleCard(
                        rule.package_name.empty() ? rule.package_id : rule.package_name,
                        body,
                        L"NativePackageIgnored_" + AutomationKey(rule.manager_key) + L"_" + AutomationKey(rule.package_id),
                        L"NativePackageRemoveIgnore_" + AutomationKey(rule.manager_key) + L"_" + AutomationKey(rule.package_id),
                        pick(L"Remove ignore", L"移除忽略"),
                        [this, managerKey = std::move(managerKey), packageId = std::move(packageId)]()
                        {
                            RemoveIgnoredPackage(managerKey, packageId);
                        });
                }

                for (auto const& rule : m_packagePinnedRules)
                {
                    auto const managerName = managerNameFor(rule.manager_key);
                    auto const body = pick(L"Version pin", L"版本釘選") +
                        L"  ·  " + managerName + L"  ·  " + rule.package_id + L"  ·  " + rule.version;
                    auto managerKey = rule.manager_key;
                    auto packageId = rule.package_id;
                    auto version = rule.version;
                    appendRuleCard(
                        rule.package_name.empty() ? rule.package_id : rule.package_name,
                        body,
                        L"NativePackagePinned_" + AutomationKey(rule.manager_key) + L"_" +
                            AutomationKey(rule.package_id) + L"_" + AutomationKey(rule.version),
                        L"NativePackageRemovePin_" + AutomationKey(rule.manager_key) + L"_" +
                            AutomationKey(rule.package_id) + L"_" + AutomationKey(rule.version),
                        pick(L"Remove pin", L"移除釘選"),
                        [this,
                            managerKey = std::move(managerKey),
                            packageId = std::move(packageId),
                            version = std::move(version)]()
                        {
                            RemovePinnedPackage(managerKey, packageId, version);
                        });
                }

                for (auto const& rule : m_packageSnoozedRules)
                {
                    auto const managerName = managerNameFor(rule.manager_key);
                    std::wstring body = pick(L"Snoozed until", L"暫停至") + L" " +
                        FormatRuleTime(rule.until_epoch_seconds) +
                        L"  ·  " + managerName + L"  ·  " + rule.package_id;
                    if (!rule.version.empty()) body += L"  ·  " + rule.version;
                    auto managerKey = rule.manager_key;
                    auto packageId = rule.package_id;
                    appendRuleCard(
                        rule.package_name.empty() ? rule.package_id : rule.package_name,
                        body,
                        L"NativePackageSnoozed_" + AutomationKey(rule.manager_key) + L"_" + AutomationKey(rule.package_id),
                        L"NativePackageRemoveSnooze_" + AutomationKey(rule.manager_key) + L"_" + AutomationKey(rule.package_id),
                        pick(L"Remove snooze", L"移除暫停"),
                        [this, managerKey = std::move(managerKey), packageId = std::move(packageId)]()
                        {
                            RemoveSnoozedPackage(managerKey, packageId);
                        });
                }
            }
            return;
        }

        if (m_packageView == 7)
        {
            auto appendToggleCard = [this, &pick, &appendCard](
                std::wstring_view automationId,
                std::wstring_view titleEn,
                std::wstring_view titleZh,
                std::wstring_view bodyEn,
                std::wstring_view bodyZh,
                bool& flag)
            {
                Border card;
                card.Padding(Thickness{ 16 });
                card.CornerRadius(CornerRadius{ 8 });
                card.BorderThickness(Thickness{ 1 });
                card.BorderBrush(Application::Current().Resources().Lookup(
                    box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
                card.Background(Application::Current().Resources().Lookup(
                    box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

                StackPanel content;
                content.Spacing(6);

                ToggleSwitch toggle;
                toggle.IsOn(flag);
                toggle.Header(box_value(ToHString(pick(titleEn, titleZh))));
                AutomationProperties::SetAutomationId(toggle, ToHString(automationId));
                toggle.Toggled([this, &flag](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
                {
                    flag = sender.as<ToggleSwitch>().IsOn();
                    SavePackageManagerState();
                    RenderPackageManagerView();
                });
                content.Children().Append(toggle);

                auto detail = CreateText(pick(bodyEn, bodyZh), 12.5);
                detail.TextWrapping(TextWrapping::Wrap);
                content.Children().Append(detail);
                card.Child(content);
                m_packageResults.Children().Append(card);
            };

            appendCard(
                pick(L"Native package-manager state", L"原生套件管理狀態"),
                pick(
                    L"Package view, search text, manager selections, Discover filters, and snooze duration can now be persisted locally in native JSON state.",
                    L"套件檢視、搜尋文字、管理器選擇、搜尋篩選同暫停時長而家可以用原生 JSON 狀態喺本機保存。"),
                L"NativePackageSettingsSummary");

            appendToggleCard(
                L"NativePackageRememberView",
                L"Remember package view on launch",
                L"啟動時記住套件檢視",
                L"Restore the last package-manager view the next time WinForge opens.",
                L"下次開啟 WinForge 時會還原上一個套件管理檢視。",
                m_packageRememberView);

            appendToggleCard(
                L"NativePackageRememberSearch",
                L"Remember search text",
                L"記住搜尋文字",
                L"Keep the current package search query in the local state file.",
                L"將而家嘅套件搜尋字串保留喺本機狀態檔。",
                m_packageRememberSearch);

            appendToggleCard(
                L"NativePackageRememberFilters",
                L"Remember manager and Discover filters",
                L"記住管理器同搜尋篩選",
                L"Persist selected package engines plus the Discover mode and toggle choices for the next launch.",
                L"保存下次啟動時揀咗邊啲套件引擎，同埋搜尋模式同切換選項。",
                m_packageRememberFilters);

            Border snoozeCard;
            snoozeCard.Padding(Thickness{ 16 });
            snoozeCard.CornerRadius(CornerRadius{ 8 });
            snoozeCard.BorderThickness(Thickness{ 1 });
            snoozeCard.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            snoozeCard.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

            StackPanel snoozeContent;
            snoozeContent.Spacing(6);
            snoozeContent.Children().Append(CreateText(
                pick(L"Default update snooze duration", L"預設更新暫停時長"),
                13,
                true));
            snoozeContent.Children().Append(CreateText(
                pick(
                    L"Choose how long the Updates row Snooze action hides matching packages. The selected duration is persisted in native JSON and never executes a package-manager command.",
                    L"選擇「更新」資料列嘅暫停動作會隱藏相符套件幾耐；所選時長會保存喺原生 JSON，亦唔會執行任何套件管理器指令。"),
                12.5));

            ComboBox snoozePicker;
            snoozePicker.MinWidth(180);
            AutomationProperties::SetAutomationId(snoozePicker, L"NativePackageSnoozeDays");
            AutomationProperties::SetName(
                snoozePicker,
                ToHString(pick(L"Default update snooze duration", L"預設更新暫停時長")));
            for (auto const days : { 1, 7, 14, 30 })
            {
                ComboBoxItem item;
                item.Content(box_value(ToHString(FormatSnoozeLabel(days, m_language))));
                snoozePicker.Items().Append(item);
            }
            m_packageSnoozeDays = NormalizeSnoozeDays(m_packageSnoozeDays);
            snoozePicker.SelectedIndex(SnoozeDaysIndex(m_packageSnoozeDays));
            snoozePicker.SelectionChanged([this](
                Windows::Foundation::IInspectable const& sender,
                SelectionChangedEventArgs const&)
            {
                auto const selected = sender.as<ComboBox>().SelectedIndex();
                m_packageSnoozeDays = SnoozeDaysFromIndex(selected);
                SavePackageManagerState();
                RenderPackageManagerView();
                AnnouncePackageStatus(
                    L"Default update snooze duration saved.",
                    L"預設更新暫停時長已保存。");
            });
            snoozeContent.Children().Append(snoozePicker);
            snoozeCard.Child(snoozeContent);
            m_packageResults.Children().Append(snoozeCard);

            Border resetCard;
            resetCard.Padding(Thickness{ 16 });
            resetCard.CornerRadius(CornerRadius{ 8 });
            resetCard.BorderThickness(Thickness{ 1 });
            resetCard.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            resetCard.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

            StackPanel resetContent;
            resetContent.Spacing(6);
            resetContent.Children().Append(CreateText(
                pick(L"Reset persisted package-manager state", L"重設已保存嘅套件管理狀態"),
                13,
                true));
            resetContent.Children().Append(CreateText(
                pick(
                    L"Clear the saved view, search text, manager selections, and Discover filter choices, then write a fresh default state file.",
                    L"清走已保存嘅檢視、搜尋文字、管理器選擇同搜尋篩選，然後寫入一個新嘅預設狀態檔。"),
                12.5));

            Button resetButton;
            resetButton.Content(box_value(ToHString(pick(L"Reset now", L"而家重設"))));
            resetButton.Padding(Thickness{ 12, 6, 12, 6 });
            AutomationProperties::SetAutomationId(resetButton, L"NativePackageResetState");
            resetButton.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                ResetPackageManagerState();
            });
            resetContent.Children().Append(resetButton);
            resetCard.Child(resetContent);
            m_packageResults.Children().Append(resetCard);
            return;
        }

        if (m_packageView == 3)
        {
            // Bundle state is explicit. Never fall back to the currently shown
            // query rows: that could silently export a different workspace.
            auto bundleItems = m_packageBundleItems;
            auto incompatibleItems = m_packageBundleIncompatibleItems;
            winforge::core::packages::SortPackageItems(
                bundleItems,
                static_cast<winforge::core::packages::PackageSortMode>(m_packageSortMode));
            winforge::core::packages::SortPackageItems(
                incompatibleItems,
                static_cast<winforge::core::packages::PackageSortMode>(m_packageSortMode));
            if (bundleItems.empty() && incompatibleItems.empty())
            {
                appendCard(
                    pick(L"No native bundle workspace yet", L"仲未有原生 bundle 工作區"),
                    pick(
                        L"Select cached rows from Discover or Installed, then choose Add selected to bundle. Export becomes available after the workspace has metadata; import only replaces an unsaved workspace after confirmation and no package command can run.",
                        L"先喺 Discover 或已安裝選擇快取資料列，然後撳「加入所選到 bundle」。工作區有 metadata 後先可以匯出；匯入只會喺確認後取代未儲存工作區，而且絕對唔會執行套件指令。"),
                    L"NativeBundleEmpty");
            }
            else
            {
                std::wstring summary = pick(
                    std::to_wstring(bundleItems.size()) + L" compatible package record(s) and " +
                        std::to_wstring(incompatibleItems.size()) +
                        L" incompatible audit record(s). Metadata only; no package command can run from this workspace.",
                    std::to_wstring(bundleItems.size()) + L" 個相容套件記錄同 " +
                        std::to_wstring(incompatibleItems.size()) +
                        L" 個不相容審計記錄。只限 metadata；呢個工作區唔可以執行套件指令。");
                summary += m_packageBundleDirty
                    ? pick(L" Changes are not saved yet.", L" 變更仲未儲存。")
                    : m_packageBundleSourcePath.empty()
                        ? pick(L" This is an unsaved draft.", L" 呢個係未儲存草稿。")
                        : pick(L" The workspace is saved.", L" 工作區已儲存。");
                appendCard(
                    pick(L"Native bundle workspace", L"原生 bundle 工作區"),
                    summary,
                    L"NativeBundleDraftSummary");
                if (!m_packageBundleSourcePath.empty())
                {
                    std::wstring sourceLine = pick(
                        L"Saved or loaded from " + m_packageBundleSourcePath,
                        L"已保存或者由 " + m_packageBundleSourcePath + L" 載入。");
                    appendCard(
                        pick(L"Bundle snapshot path", L"Bundle 快照路徑"),
                        TruncateForUi(sourceLine),
                        L"NativeBundleSource");
                }
                if (!m_packageBundleImportNote.empty())
                {
                    appendCard(
                        pick(L"Import safety note", L"匯入安全提示"),
                        m_packageBundleImportNote,
                        L"NativeBundleImportSafetyNote");
                }
            }

            for (auto const& package : bundleItems)
            {
                std::wstring metadata = package.manager_key;
                if (!package.version.empty())
                {
                    metadata += L" · " + package.version;
                }
                if (!package.source.empty())
                {
                    metadata += L" · " + package.source;
                }
                appendCard(
                    package.name.empty() ? package.id : package.name,
                    package.id + L"\n" + metadata,
                    L"NativeBundlePackage_" + StableAutomationSuffix(
                        BundleAutomationIdentity(package)));
            }
            for (auto const& package : incompatibleItems)
            {
                std::wstring metadata = package.manager_key.empty()
                    ? pick(L"Imported incompatible record", L"匯入嘅不相容記錄")
                    : package.manager_key;
                if (!package.version.empty())
                {
                    metadata += L" · " + package.version;
                }
                if (!package.source.empty())
                {
                    metadata += L" · " + package.source;
                }
                metadata += pick(
                    L" · retained for review only; no package command can run",
                    L" · 只保留作審核；唔可以執行套件指令");
                appendCard(
                    pick(L"Incompatible: ", L"不相容：") +
                        (package.name.empty() ? package.id : package.name),
                    package.id + L"\n" + metadata,
                    L"NativeBundleIncompatible_" + StableAutomationSuffix(
                        BundleAutomationIdentity(package)));
            }
            return;
        }

        if (!readOnlyQuery)
        {
            appendCard(
                pick(L"Evidence gate remains closed", L"證據閘門仍然關閉"),
                explanation,
                L"NativePackageViewGate");
            return;
        }

        for (auto const& state : m_packageRunStates)
        {
            auto const* descriptor = winforge::core::packages::FindPackageManager(state.manager_key);
            auto const title = descriptor
                ? winforge::core::LocalizedText{
                    std::wstring(descriptor->name_en), std::wstring(descriptor->name_zh) }.Pick(m_language)
                : state.manager_key;
            std::wstring body;
            if (state.success)
            {
                body = state.output.empty()
                    ? pick(
                        L"Query completed successfully with " + std::to_wstring(state.package_count) +
                            (state.package_count == 1 ? L" package row." : L" package rows."),
                        L"查詢成功完成，有 " + std::to_wstring(state.package_count) + L" 筆套件資料列。")
                    : state.output;
            }
            else
            {
                body = state.diagnostic.empty()
                    ? pick(L"The native query did not complete.", L"原生查詢未完成。")
                    : state.diagnostic;
            }
            if (state.requires_runtime_resolution)
            {
                body += pick(L" Additional version resolution is still required.", L" 仲需要額外版本解析。");
            }
            appendCard(
                title,
                TruncateForUi(body),
                L"NativePackageManagerState_" + AutomationKey(state.manager_key),
                state.success ? 0.94 : 1.0);
        }

        constexpr std::size_t MaximumRenderedPackages = 250;
        if (m_packageLastAction == winforge::core::packages::PackageAction::Details)
        {
            return;
        }
        auto const renderCount = std::min(visibleItems->size(), MaximumRenderedPackages);
        for (std::size_t index = 0; index < renderCount; ++index)
        {
            auto const& package = (*visibleItems)[index];
            auto const* descriptor = winforge::core::packages::FindPackageManager(package.manager_key);
            auto const managerName = descriptor
                ? winforge::core::LocalizedText{
                    std::wstring(descriptor->name_en), std::wstring(descriptor->name_zh) }.Pick(m_language)
                : package.manager_key;

            std::wstringstream metadata;
            metadata << managerName << L"  ·  " << package.id;
            if (!package.version.empty()) metadata << L"  ·  " << package.version;
            if (!package.available_version.empty()) metadata << L"  →  " << package.available_version;
            if (!package.source.empty()) metadata << L"  ·  " << package.source;

            Border card;
            card.Padding(Thickness{ 16, 12, 16, 12 });
            card.CornerRadius(CornerRadius{ 8 });
            card.BorderThickness(Thickness{ 1 });
            card.BorderBrush(Application::Current().Resources().Lookup(
                box_value(L"CardStrokeColorDefaultBrush")).as<Media::Brush>());
            card.Background(Application::Current().Resources().Lookup(
                box_value(L"CardBackgroundFillColorDefaultBrush")).as<Media::Brush>());

            StackPanel row;
            row.Spacing(5);
            if (selectionAction)
            {
                auto const selectionActionValue = *selectionAction;
                auto const selectionActionNameEn = selectionActionValue == winforge::core::packages::PackageAction::Install
                    ? std::wstring(L"install")
                    : selectionActionValue == winforge::core::packages::PackageAction::Update
                        ? std::wstring(L"update")
                        : std::wstring(L"uninstall");
                auto const selectionActionNameZh = selectionActionValue == winforge::core::packages::PackageAction::Install
                    ? std::wstring(L"安裝")
                    : selectionActionValue == winforge::core::packages::PackageAction::Update
                        ? std::wstring(L"更新")
                        : std::wstring(L"解除安裝");
                auto const selectionViewEn = selectionActionValue == winforge::core::packages::PackageAction::Install
                    ? std::wstring(L"Discover")
                    : selectionActionValue == winforge::core::packages::PackageAction::Update
                        ? std::wstring(L"Updates")
                        : std::wstring(L"Installed");
                auto const selectionViewZh = selectionActionValue == winforge::core::packages::PackageAction::Install
                    ? std::wstring(L"Discover")
                    : selectionActionValue == winforge::core::packages::PackageAction::Update
                        ? std::wstring(L"更新")
                        : std::wstring(L"已安裝");
                CheckBox selection;
                auto const selectionLabel = pick(
                    L"Select for " + selectionActionNameEn + L" preview",
                    L"揀嚟預覽" + selectionActionNameZh);
                selection.Content(box_value(ToHString(selectionLabel)));
                selection.IsChecked(m_packageSelectedKeys.contains(
                    winforge::core::packages::PackageSelectionKey(package, selectionActionValue)));
                selection.IsEnabled(!m_packageWorking);
                AutomationProperties::SetAutomationId(
                    selection,
                    ToHString(L"NativePackageSelect_" + StableAutomationSuffix(
                        winforge::core::packages::PackageSelectionKey(
                            package,
                            selectionActionValue))));
                AutomationProperties::SetName(
                    selection,
                    ToHString(selectionLabel + L": " +
                        (package.name.empty() ? package.id : package.name) +
                        (package.source.empty() ? L"" : L" · " + package.source)));
                ToolTipService::SetToolTip(
                    selection,
                    box_value(ToHString(pick(
                        L"Select this cached " + selectionViewEn + L" result for a " +
                            selectionActionNameEn + L"-plan preview. No package command will run.",
                        L"揀呢個已快取" + selectionViewZh + L"結果做" +
                            selectionActionNameZh + L"計劃預覽；唔會執行套件指令。"))));
                auto selectedPackageCopy = package;
                selection.Checked([this, selectedPackageCopy, selectionActionValue](
                    Windows::Foundation::IInspectable const&,
                    RoutedEventArgs const&)
                {
                    SetPackageSelected(selectedPackageCopy, selectionActionValue, true);
                });
                selection.Unchecked([this, selectedPackageCopy, selectionActionValue](
                    Windows::Foundation::IInspectable const&,
                    RoutedEventArgs const&)
                {
                    SetPackageSelected(selectedPackageCopy, selectionActionValue, false);
                });
                row.Children().Append(selection);
            }
            auto packageTitle = CreateText(package.name.empty() ? package.id : package.name, 15, true);
            AutomationProperties::SetAutomationId(
                packageTitle,
                ToHString(L"NativePackageResult_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
            row.Children().Append(packageTitle);
            auto details = CreateText(metadata.str(), 12);
            details.Opacity(0.72);
            details.IsTextSelectionEnabled(true);
            row.Children().Append(details);

            StackPanel actions;
            actions.Orientation(Orientation::Vertical);
            actions.Spacing(8);

            Button mutation;
            auto const actionNameEn = m_packageView == 0
                ? std::wstring(L"Install")
                : m_packageView == 1
                    ? std::wstring(L"Update")
                    : std::wstring(L"Uninstall");
            auto const actionNameZh = m_packageView == 0
                ? std::wstring(L"安裝")
                : m_packageView == 1
                    ? std::wstring(L"更新")
                    : std::wstring(L"解除安裝");
            auto const reviewLabel = pick(
                L"Review " + actionNameEn,
                L"檢視" + actionNameZh);
            mutation.Content(box_value(ToHString(reviewLabel)));
            mutation.IsEnabled(!m_packageWorking);
            mutation.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(
                mutation,
                ToHString(L"NativePackageMutation_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
            AutomationProperties::SetName(
                mutation,
                ToHString(pick(
                    L"Review " + actionNameEn + L" plan for " + (package.name.empty() ? package.id : package.name),
                    L"檢視" + actionNameZh + L"計劃：" + (package.name.empty() ? package.id : package.name))));
            ToolTipService::SetToolTip(
                mutation,
                box_value(ToHString(pick(
                    L"Review the exact native argv plan. A separate explicit Confirm execution action is required before any package command can be queued.",
                    L"檢視準確嘅原生 argv 計劃；任何套件指令要排隊之前，仲需要另一個明確「確認執行」動作。"))));
            auto packageCopy = package;
            auto action = m_packageView == 0
                ? winforge::core::packages::PackageAction::Install
                : m_packageView == 1
                    ? winforge::core::packages::PackageAction::Update
                    : winforge::core::packages::PackageAction::Uninstall;
            mutation.Click([this, packageCopy = std::move(packageCopy), action](
                Windows::Foundation::IInspectable const&,
                RoutedEventArgs const&)
            {
                RequestPackageMutation(packageCopy, action);
            });
            actions.Children().Append(mutation);

            if (m_packageView == 1)
            {
                Button ignore;
                ignore.Content(box_value(ToHString(pick(L"Ignore", L"忽略"))));
                ignore.IsEnabled(!m_packageWorking);
                ignore.HorizontalAlignment(HorizontalAlignment::Left);
                AutomationProperties::SetAutomationId(
                    ignore,
                    ToHString(L"NativePackageIgnore_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
                AutomationProperties::SetName(
                    ignore,
                    ToHString(pick(L"Ignore update for ", L"忽略更新：") + (package.name.empty() ? package.id : package.name)));
                ToolTipService::SetToolTip(
                    ignore,
                    box_value(ToHString(pick(
                        L"Persist a local ignored-update rule and hide matching rows from Updates results.",
                        L"保存本機忽略更新規則，並喺更新結果隱藏相符資料列。"))));
                auto ignoredPackageCopy = package;
                ignore.Click([this, ignoredPackageCopy = std::move(ignoredPackageCopy)](
                    Windows::Foundation::IInspectable const&,
                    RoutedEventArgs const&)
                {
                    IgnorePackageUpdate(ignoredPackageCopy);
                });
                actions.Children().Append(ignore);

                Button pin;
                pin.Content(box_value(ToHString(pick(L"Pin version", L"釘選版本"))));
                pin.IsEnabled(!m_packageWorking);
                pin.HorizontalAlignment(HorizontalAlignment::Left);
                AutomationProperties::SetAutomationId(
                    pin,
                    ToHString(L"NativePackagePin_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
                AutomationProperties::SetName(
                    pin,
                    ToHString(pick(L"Pin update version for ", L"釘選更新版本：") + (package.name.empty() ? package.id : package.name)));
                ToolTipService::SetToolTip(
                    pin,
                    box_value(ToHString(pick(
                        L"Persist a local version-pin rule and hide matching update-version rows.",
                        L"保存本機版本釘選規則，並隱藏相符更新版本資料列。"))));
                auto pinnedPackageCopy = package;
                pin.Click([this, pinnedPackageCopy = std::move(pinnedPackageCopy)](
                    Windows::Foundation::IInspectable const&,
                    RoutedEventArgs const&)
                {
                    PinPackageUpdate(pinnedPackageCopy);
                });
                actions.Children().Append(pin);

                Button snooze;
                snooze.Content(box_value(ToHString(FormatSnoozeLabel(m_packageSnoozeDays, m_language))));
                snooze.IsEnabled(!m_packageWorking);
                snooze.HorizontalAlignment(HorizontalAlignment::Left);
                AutomationProperties::SetAutomationId(
                    snooze,
                    ToHString(L"NativePackageSnooze_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
                AutomationProperties::SetName(
                    snooze,
                    ToHString(pick(L"Snooze update for ", L"暫停更新：") + (package.name.empty() ? package.id : package.name)));
                ToolTipService::SetToolTip(
                    snooze,
                    box_value(ToHString(pick(
                        L"Persist a local snooze using the configured duration and hide matching update rows until it expires.",
                        L"使用已設定時長保存本機暫停，並喺到期前隱藏相符更新資料列。"))));
                auto snoozedPackageCopy = package;
                snooze.Click([this, snoozedPackageCopy = std::move(snoozedPackageCopy)](
                    Windows::Foundation::IInspectable const&,
                    RoutedEventArgs const&)
                {
                    SnoozePackageUpdate(snoozedPackageCopy);
                });
                actions.Children().Append(snooze);
            }

            Button detailPreview;
            auto const detailLabel = pick(L"Details", L"詳細資料");
            detailPreview.Content(box_value(ToHString(detailLabel)));
            detailPreview.IsEnabled(!m_packageWorking);
            detailPreview.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(
                detailPreview,
                ToHString(L"NativePackageDetails_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
            AutomationProperties::SetName(
                detailPreview,
                ToHString(detailLabel + L" preview for " + (package.name.empty() ? package.id : package.name)));
            ToolTipService::SetToolTip(
                detailPreview,
                box_value(ToHString(pick(
                    L"Run the bounded, read-only native details query for this package. Elevated sessions still fail closed.",
                    L"執行呢個套件嘅有界、只讀原生詳細資料查詢；提升權限 session 仍然會 fail closed。"))));
            auto detailPackageCopy = package;
            detailPreview.Click([this, detailPackageCopy = std::move(detailPackageCopy)](
                Windows::Foundation::IInspectable const&,
                RoutedEventArgs const&)
            {
                StartPackageDetailsQuery(detailPackageCopy);
            });
            actions.Children().Append(detailPreview);

            row.Children().Append(actions);
            card.Child(row);
            m_packageResults.Children().Append(card);
        }

        if (visibleItems->size() > MaximumRenderedPackages)
        {
            appendCard(
                pick(L"Result rendering capped", L"結果顯示設有上限"),
                pick(L"The native query completed, but this page renders the first 250 visible rows to protect UI responsiveness.",
                    L"原生查詢已完成，但為咗保持介面流暢，呢頁只顯示頭 250 筆可見資料列。"),
                L"NativePackageResultLimit");
        }
        else if (!m_packageWorking && m_packageProbeComplete && isCachedDiscoverQuery &&
            !m_packageItems.empty() && visibleItems->empty())
        {
            appendCard(
                pick(L"No packages match the current Discover filters", L"冇套件符合目前搜尋篩選"),
                pick(
                    L"The raw Discover results are still cached. Change the local mode or toggles to reveal them without running another package query.",
                    L"原始搜尋結果仍然已快取；改本機模式或者切換按鈕即可重新顯示，唔使再執行套件查詢。"),
                L"NativePackageDiscoverFilterEmpty");
        }
        else if (!m_packageWorking && m_packageProbeComplete && m_packageItems.empty() && m_packageRunStates.empty())
        {
            appendCard(
                pick(L"Ready for a read-only query", L"可以開始只讀查詢"),
                m_packageView == 0
                    ? pick(L"Enter a package name or ID, then choose Search.", L"輸入套件名稱或者 ID，再揀搜尋。")
                    : pick(L"Choose Refresh to query the selected available engines.", L"揀重新整理，查詢已選而且可用嘅引擎。"),
                L"NativePackageReadyState");
        }
        else if (!m_packageWorking && !m_packageRunStates.empty() && m_packageItems.empty())
        {
            appendCard(
                pick(L"No package rows returned", L"冇套件資料列"),
                pick(L"Review the per-engine diagnostics above. A successful empty result is not treated as an error.",
                    L"請睇上面各引擎診斷；成功但冇結果唔會當成錯誤。"),
                L"NativePackageEmptyState");
        }
    }

    void MainWindow::RenderAllApps(std::wstring_view query)
    {
        auto const& allAppsRegexSurface = winforge::core::regex::RegexSearchSurfaceFor(
            winforge::core::regex::RegexSearchSurfaceId::AllApps);
        if (!query.empty())
        {
            m_allAppsSearchText = std::wstring(query);
        }

        auto page = CreatePage(
            L"All Apps · 所有 app",
            L"Every fixed native parity route is discoverable here. PCRE2 regex is optional and bounded; invalid patterns keep the last valid list visible. Pending entries are intentionally labelled and are not counted as ported. · 所有固定原生對等路線都可以喺呢度搵到；可以用受限制嘅 PCRE2 正規表示式，無效模式會保留上一次有效清單；未完成項目會清楚標示，唔會當成已移植。");
        AutomationProperties::SetAutomationId(page, L"NativeAllAppsPage");

        m_allAppsSearchBox = TextBox();
        m_allAppsSearchBox.PlaceholderText(m_allAppsRegexEnabled
            ? L"Filter routes with PCRE2 regex · 用 PCRE2 正規表示式篩選路線"
            : L"Filter 346 routes · 篩選 346 條路線");
        m_allAppsSearchBox.Text(ToHString(m_allAppsSearchText));
        m_allAppsSearchBox.Margin(Thickness{ 0, 0, 0, 4 });
        AutomationProperties::SetAutomationId(
            m_allAppsSearchBox,
            ToHString(allAppsRegexSurface.search_automation_id));
        AutomationProperties::SetName(m_allAppsSearchBox, L"Filter all native routes · 篩選所有原生路線");
        m_allAppsSearchBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            auto const box = sender.as<TextBox>();
            m_allAppsSearchText = ToWide(box.Text());
            PopulateAllApps(m_allAppsSearchText);
        });
        page.Children().Append(m_allAppsSearchBox);

        m_allAppsRegexMode = ToggleSwitch();
        m_allAppsRegexMode.Header(box_value(L"Use PCRE2 regex for route filter · 用 PCRE2 正規表示式篩選路線"));
        m_allAppsRegexMode.IsOn(m_allAppsRegexEnabled);
        AutomationProperties::SetAutomationId(
            m_allAppsRegexMode,
            ToHString(allAppsRegexSurface.regex_mode_automation_id));
        AutomationProperties::SetName(m_allAppsRegexMode, L"Use PCRE2 regular expressions for All Apps filtering · 用 PCRE2 正規表示式篩選所有 app");
        m_allAppsRegexMode.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            m_allAppsRegexEnabled = sender.as<ToggleSwitch>().IsOn();
            if (m_allAppsSearchBox)
            {
                m_allAppsSearchBox.PlaceholderText(m_allAppsRegexEnabled
                    ? L"Filter routes with PCRE2 regex · 用 PCRE2 正規表示式篩選路線"
                    : L"Filter 346 routes · 篩選 346 條路線");
            }
            PopulateAllApps(m_allAppsSearchText);
        });
        page.Children().Append(m_allAppsRegexMode);

        m_allAppsRegexBuilder = Button();
        m_allAppsRegexBuilder.Content(box_value(L"Build route regex · 建立路線正規表示式"));
        m_allAppsRegexBuilder.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(
            m_allAppsRegexBuilder,
            ToHString(allAppsRegexSurface.builder_automation_id));
        AutomationProperties::SetName(m_allAppsRegexBuilder, L"Open the regex builder for All Apps · 開啟所有 app 嘅正規表示式建立器");
        m_allAppsRegexBuilder.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            OpenRegexBuilder(RegexBuilderTarget::AllApps, m_allAppsSearchText);
        });
        page.Children().Append(m_allAppsRegexBuilder);

        m_allAppsRegexStatus = CreateText(
            m_allAppsRegexEnabled
                ? L"Validating PCRE2 regex. · 正在驗證 PCRE2 正規表示式。"
                : L"Literal route filter · 文字路線篩選",
            12);
        m_allAppsRegexStatus.TextWrapping(TextWrapping::Wrap);
        m_allAppsRegexStatus.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_allAppsRegexStatus, L"NativeAllAppsRegexStatus");
        AutomationProperties::SetLiveSetting(
            m_allAppsRegexStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_allAppsRegexStatus);

        m_allAppsList = ListView();
        m_allAppsList.SelectionMode(ListViewSelectionMode::Single);
        AutomationProperties::SetAutomationId(m_allAppsList, L"NativeAllAppsList");
        m_allAppsList.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            auto const view = sender.as<ListView>();
            auto const item = view.SelectedItem().try_as<ListViewItem>();
            if (!item) return;
            auto const route = unbox_value_or<hstring>(item.Tag(), L"");
            view.SelectedItem(nullptr);
            Navigate(ToWide(route));
        });

        m_allAppsCount = CreateText(L"", 12);
        page.Children().Append(m_allAppsCount);
        page.Children().Append(m_allAppsList);
        PopulateAllApps(m_allAppsSearchText);
        ShowPage(page);

        m_allAppsSearchBox.Focus(FocusState::Programmatic);
        m_allAppsSearchBox.SelectionStart(static_cast<int32_t>(m_allAppsSearchBox.Text().size()));
    }

    void MainWindow::PopulateAllApps(std::wstring_view query)
    {
        if (!m_allAppsList || !m_allAppsCount)
        {
            return;
        }

        std::shared_ptr<winforge::core::regex::SafeRegex const> expression;
        if (m_allAppsRegexEnabled)
        {
            std::wstring diagnostic;
            expression = CompileSearchRegex(
                query,
                m_allAppsRegexCaseSensitive,
                m_allAppsRegexMultiline,
                m_allAppsRegexDotMatchesNewline,
                diagnostic);
            m_allAppsRegexDiagnostic = diagnostic;
            if (!expression)
            {
                if (m_allAppsRegexStatus)
                {
                    m_allAppsRegexStatus.Text(ToHString(diagnostic));
                    AutomationProperties::SetName(m_allAppsRegexStatus, ToHString(diagnostic));
                }
                m_allAppsCount.Text(L"Previous valid route list remains visible · 保留上一次有效路線清單");
                return;
            }
        }

        if (m_allAppsRegexStatus)
        {
            auto const status = m_allAppsRegexEnabled
                ? L"PCRE2 regex is filtering local route metadata only. · PCRE2 正規表示式只會篩選本機路線 metadata。"
                : L"Literal route filter · 文字路線篩選";
            m_allAppsRegexStatus.Text(status);
            AutomationProperties::SetName(m_allAppsRegexStatus, status);
        }

        m_allAppsList.Items().Clear();
        std::size_t visible = 0;
        for (auto const& module : m_modules)
        {
            if (!Matches(module, query, expression.get())) continue;

            StackPanel row;
            row.Spacing(2);
            row.Children().Append(CreateText(Label(module), 15, true));
            auto metadata = module.id + L"  ·  " + module.kind + L"  ·  native implementation pending / 原生實作待完成";
            auto metadataText = CreateText(metadata, 11);
            metadataText.Opacity(0.68);
            row.Children().Append(metadataText);

            ListViewItem item;
            item.Content(row);
            item.Tag(box_value(ToHString(module.id)));
            item.Padding(Thickness{ 12, 9, 12, 9 });
            AutomationProperties::SetAutomationId(item, ToHString(L"NativeAllApps_" + AutomationKey(module.id)));
            m_allAppsList.Items().Append(item);
            ++visible;
        }

        std::wstringstream result;
        result << visible << L" / " << m_modules.size() << L" routes · 條路線";
        if (m_allAppsRegexEnabled)
        {
            result << L" · PCRE2 regex";
        }
        m_allAppsCount.Text(ToHString(result.str()));
    }

    void MainWindow::RenderSearch(std::wstring_view query)
    {
        auto page = CreatePage(
            L"Search results · 搜尋結果",
            m_shellRegexEnabled
                ? L"PCRE2 catalog search spans names, Cantonese labels, keywords, route ids, and aliases. Matching is local, bounded, and never uses JIT. · PCRE2 目錄搜尋涵蓋英文名、粵語名、關鍵字、路線 id 同別名；只會喺本機、受限制咁比對，而且唔用 JIT。"
                : L"Native catalog search spans names, Cantonese labels, keywords, route ids, and aliases. · 原生目錄搜尋涵蓋英文名、粵語名、關鍵字、路線 id 同別名。");

        std::shared_ptr<winforge::core::regex::SafeRegex const> expression;
        if (m_shellRegexEnabled)
        {
            std::wstring diagnostic;
            expression = CompileSearchRegex(
                query,
                m_shellRegexCaseSensitive,
                m_shellRegexMultiline,
                m_shellRegexDotMatchesNewline,
                diagnostic);
            m_shellRegexDiagnostic = diagnostic;

            InfoBar regexStatus;
            regexStatus.IsOpen(true);
            regexStatus.IsClosable(false);
            regexStatus.Severity(expression ? InfoBarSeverity::Success : InfoBarSeverity::Error);
            regexStatus.Title(expression
                ? L"PCRE2 regex search · PCRE2 正規表示式搜尋"
                : L"Regex needs correction · 正規表示式需要修正");
            regexStatus.Message(ToHString(expression
                ? L"The pattern is evaluated separately against each local catalog field with strict resource limits. · 模式會喺每個本機目錄欄位分開比對，並有嚴格資源限制。"
                : diagnostic));
            AutomationProperties::SetAutomationId(regexStatus, L"NativeSearchRegexStatus");
            AutomationProperties::SetName(
                regexStatus,
                expression
                    ? L"PCRE2 regex search · PCRE2 正規表示式搜尋"
                    : L"Regex needs correction · 正規表示式需要修正");
            page.Children().Append(regexStatus);

            if (m_shellRegexStatus)
            {
                auto const status = expression
                    ? L"PCRE2 regex search is active. · PCRE2 正規表示式搜尋已啟用。"
                    : diagnostic;
                m_shellRegexStatus.Text(ToHString(status));
                AutomationProperties::SetName(m_shellRegexStatus, ToHString(status));
            }

            if (!expression)
            {
                ShowPage(page);
                return;
            }
        }
        else if (m_shellRegexStatus)
        {
            m_shellRegexDiagnostic.clear();
            m_shellRegexStatus.Text(L"Literal catalog search · 文字目錄搜尋");
            AutomationProperties::SetName(m_shellRegexStatus, L"Literal catalog search · 文字目錄搜尋");
        }

        std::size_t matches = 0;
        for (auto const& module : m_modules)
        {
            if (!Matches(module, query, expression.get())) continue;
            page.Children().Append(CreateRouteButton(Label(module), module.id));
            if (++matches == 60) break;
        }
        if (matches == 0)
        {
            page.Children().Append(CreateText(L"No matching route · 搵唔到相符路線", 16, true));
        }
        ShowPage(page);
    }

    void MainWindow::OpenRegexBuilder(RegexBuilderTarget target, std::wstring_view initialPattern)
    {
        // Cache retention belongs to a confirmed Package Discover target
        // application, not to merely opening a builder. The user can change
        // the selected target while the wizard is open.
        m_packageRetainCachedResultsOnNextRender = false;
        m_regexBuilderTarget = target;
        m_regexBuilderStep = 0;
        m_regexBuilderPattern = std::wstring(initialPattern);

        switch (target)
        {
        case RegexBuilderTarget::ShellCatalog:
            m_regexBuilderCaseSensitive = m_shellRegexCaseSensitive;
            m_regexBuilderMultiline = m_shellRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_shellRegexDotMatchesNewline;
            break;
        case RegexBuilderTarget::AllApps:
            m_regexBuilderCaseSensitive = m_allAppsRegexCaseSensitive;
            m_regexBuilderMultiline = m_allAppsRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_allAppsRegexDotMatchesNewline;
            break;
        case RegexBuilderTarget::PackageDiscover:
            m_regexBuilderCaseSensitive = m_packageSearchCaseSensitiveValue;
            m_regexBuilderMultiline = m_packageDiscoverRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_packageDiscoverRegexDotMatchesNewline;
            break;
        case RegexBuilderTarget::TesterOnly:
        default:
            break;
        }
        Navigate(L"module.regextester");
    }

    void MainWindow::AppendRegexBuilderToken(std::wstring_view token)
    {
        m_regexBuilderPattern += token;
        if (m_regexBuilderPatternBox)
        {
            m_regexBuilderPatternBox.Text(ToHString(m_regexBuilderPattern));
            m_regexBuilderPatternBox.SelectionStart(
                static_cast<int32_t>(m_regexBuilderPatternBox.Text().size()));
        }
        RefreshRegexTesterPreview();
    }

    void MainWindow::RefreshRegexTesterPreview()
    {
        if (!m_regexBuilderStatus || !m_regexBuilderPreview)
        {
            return;
        }

        std::wstring diagnostic;
        auto const expression = CompileSearchRegex(
            m_regexBuilderPattern,
            m_regexBuilderCaseSensitive,
            m_regexBuilderMultiline,
            m_regexBuilderDotMatchesNewline,
            diagnostic);
        if (!expression)
        {
            m_regexBuilderStatus.Text(ToHString(diagnostic));
            m_regexBuilderPreview.Text(L"Preview is paused until the pattern is valid. · 模式有效之前會暫停預覽。");
            AutomationProperties::SetName(m_regexBuilderStatus, ToHString(diagnostic));
            AutomationProperties::SetName(
                m_regexBuilderPreview,
                L"Regex preview is paused until the pattern is valid · 模式有效之前會暫停正規表示式預覽");
            if (m_regexBuilderApply)
            {
                m_regexBuilderApply.IsEnabled(false);
                AutomationProperties::SetHelpText(
                    m_regexBuilderApply,
                    L"Correct the regex syntax before it can be applied to a target · 修正正規表示式語法先可以套用去目標");
            }
            return;
        }

        if (m_regexBuilderApply)
        {
            m_regexBuilderApply.IsEnabled(true);
            AutomationProperties::SetHelpText(
                m_regexBuilderApply,
                L"Apply this validated bounded PCRE2 pattern to the selected target · 將已驗證、有界 PCRE2 模式套用去所選目標");
        }

        auto const result = expression->Search(m_regexBuilderTestText, true);
        m_regexBuilderStatus.Text(
            L"PCRE2 pattern is valid with strict 10 ms / resource limits. · PCRE2 模式有效，而且有嚴格 10 ms／資源限制。");
        AutomationProperties::SetName(
            m_regexBuilderStatus,
            L"PCRE2 pattern is valid with strict interactive limits · PCRE2 模式有效，而且有嚴格互動限制");

        std::wstringstream preview;
        if (result.resource_limit_exceeded || result.input_limit_exceeded || result.invalid_utf16)
        {
            preview << L"Preview stopped safely: "
                    << (result.diagnostic.empty()
                        ? L"a configured regex safety limit was reached."
                        : result.diagnostic)
                    << L" · 預覽已安全停止。";
        }
        else if (!result.matched)
        {
            preview << L"No match in the current test text. · 目前測試文字冇符合項目。";
        }
        else
        {
            preview << L"Match at " << result.captures.front().start
                    << L" with length " << result.captures.front().length
                    << L". Captures: " << result.captures.size()
                    << L" · 符合位置 " << result.captures.front().start
                    << L"，長度 " << result.captures.front().length
                    << L"；擷取組：" << result.captures.size();
            for (std::size_t index = 0; index < result.captures.size(); ++index)
            {
                auto const& capture = result.captures[index];
                preview << L"\n#" << index << L": ";
                if (capture.matched)
                {
                    preview << L"start " << capture.start << L", length " << capture.length;
                }
                else
                {
                    preview << L"not matched · 冇符合";
                }
            }
        }
        m_regexBuilderPreview.Text(ToHString(preview.str()));
        AutomationProperties::SetName(m_regexBuilderPreview, ToHString(preview.str()));
    }

    void MainWindow::ApplyRegexBuilderTarget()
    {
        std::wstring diagnostic;
        auto const expression = CompileSearchRegex(
            m_regexBuilderPattern,
            m_regexBuilderCaseSensitive,
            m_regexBuilderMultiline,
            m_regexBuilderDotMatchesNewline,
            diagnostic);
        if (!expression)
        {
            // A builder target is stateful navigation. Never let an invalid
            // pattern replace a valid target's result list or settings.
            if (m_regexBuilderStatus)
            {
                m_regexBuilderStatus.Text(ToHString(diagnostic));
                AutomationProperties::SetName(m_regexBuilderStatus, ToHString(diagnostic));
            }
            if (m_regexBuilderApply)
            {
                m_regexBuilderApply.IsEnabled(false);
            }
            return;
        }

        // Only a verified Package Discover application can request retained
        // cached rows. This clears any stale one-shot flag when a user changes
        // targets in the wizard.
        m_packageRetainCachedResultsOnNextRender = false;
        switch (m_regexBuilderTarget)
        {
        case RegexBuilderTarget::ShellCatalog:
            m_shellRegexEnabled = true;
            m_shellRegexCaseSensitive = m_regexBuilderCaseSensitive;
            m_shellRegexMultiline = m_regexBuilderMultiline;
            m_shellRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            if (m_shellRegexMode) m_shellRegexMode.IsOn(true);
            if (m_search) m_search.Text(ToHString(m_regexBuilderPattern));
            Navigate(L"search", m_regexBuilderPattern);
            break;
        case RegexBuilderTarget::AllApps:
            m_allAppsRegexEnabled = true;
            m_allAppsRegexCaseSensitive = m_regexBuilderCaseSensitive;
            m_allAppsRegexMultiline = m_regexBuilderMultiline;
            m_allAppsRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            m_allAppsSearchText = m_regexBuilderPattern;
            Navigate(L"shell.allapps");
            break;
        case RegexBuilderTarget::PackageDiscover:
        {
            auto const retainCachedDiscover = ShouldRetainPackageDiscoverResults(
                m_packageView,
                m_packageLastAction,
                m_packageProbeComplete,
                !m_packageItems.empty());
            m_packageDiscoverRegexEnabled = true;
            m_packageSearchCaseSensitiveValue = m_regexBuilderCaseSensitive;
            m_packageDiscoverRegexMultiline = m_regexBuilderMultiline;
            m_packageDiscoverRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            m_packageDiscoverRegexPattern = m_regexBuilderPattern;
            m_packageView = static_cast<int32_t>(winforge::core::packages::PackageView::Discover);
            m_packageRetainCachedResultsOnNextRender = retainCachedDiscover;
            SavePackageManagerState();
            Navigate(L"module.packages", L"discover");
            break;
        }
        case RegexBuilderTarget::TesterOnly:
        default:
            RefreshRegexTesterPreview();
            return;
        }
    }

    void MainWindow::RenderRegexTester()
    {
        auto page = CreatePage(
            L"Regex Tester & Builder · 正規表示式測試器同建立器",
            L"A full four-step native PCRE2-16 wizard: choose a registered search target and flags, start from escaped recipes or tokens, compose groups/alternatives/assertions/quantifiers, then test captures and apply only a valid pattern. Advanced pattern editing stays available throughout. No JIT is used; patterns and matches have strict interactive limits. · 完整四步原生 PCRE2-16 精靈：揀已登記搜尋目標同旗標、由已 escape recipe 或 token 開始、建立 group／alternative／assertion／quantifier，之後測試擷取，只會套用有效模式；全程都可以直接編輯進階模式。唔用 JIT，模式同符合有嚴格互動限制。");
        AutomationProperties::SetAutomationId(page, L"NativeRegexTesterPage");

        InfoBar safety;
        safety.IsOpen(true);
        safety.IsClosable(false);
        safety.Severity(InfoBarSeverity::Informational);
        safety.Title(L"Safe native PCRE2-16 · 安全原生 PCRE2-16");
        safety.Message(L"Patterns are limited to 512 UTF-16 code units. Every match has bounded input, heap, depth, backtracking, and a 10 ms timeout callout. Package Discover application only filters its existing local cache and cannot run or receive a package-engine query. · 模式最多 512 個 UTF-16 code units；每次符合都有輸入、heap、depth、backtracking 同 10 ms timeout callout 限制。套件 Discover 套用只會篩選現有本機快取，唔可以執行或者接收套件引擎查詢。");
        AutomationProperties::SetAutomationId(safety, L"NativeRegexBuilderSafety");
        AutomationProperties::SetName(safety, L"Safe native PCRE2-16 regex builder · 安全原生 PCRE2-16 正規表示式建立器");
        page.Children().Append(safety);

        auto step = CreateText(
            L"Step " + std::to_wstring(m_regexBuilderStep + 1) + L" of 4 · 第 " +
                std::to_wstring(m_regexBuilderStep + 1) + L"／4 步",
            18,
            true);
        AutomationProperties::SetAutomationId(step, L"NativeRegexBuilderStep");
        page.Children().Append(step);

        ComboBox target;
        target.Header(box_value(L"Apply target · 套用目標"));
        for (auto const& surface : winforge::core::regex::RegexSearchSurfaces())
        {
            ComboBoxItem item;
            auto label = std::wstring(surface.target_name_en);
            label += L" · ";
            label += surface.target_name_zh;
            item.Content(box_value(ToHString(label)));
            target.Items().Append(item);
        }
        ComboBoxItem testerOnly;
        testerOnly.Content(box_value(L"Tester only · 只限測試器"));
        target.Items().Append(testerOnly);
        target.SelectedIndex(static_cast<int32_t>(m_regexBuilderTarget));
        AutomationProperties::SetAutomationId(target, L"NativeRegexBuilderTarget");
        AutomationProperties::SetName(target, L"Regex builder apply target · 正規表示式建立器套用目標");
        target.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            m_regexBuilderTarget = static_cast<RegexBuilderTarget>(
                std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 3));
        });
        page.Children().Append(target);

        m_regexBuilderPatternBox = TextBox();
        m_regexBuilderPatternBox.Header(box_value(L"PCRE2 pattern · PCRE2 模式"));
        m_regexBuilderPatternBox.PlaceholderText(L"For example: ^module\\.reactor$ or (?<name>WinForge)\\s+Native");
        m_regexBuilderPatternBox.Text(ToHString(m_regexBuilderPattern));
        m_regexBuilderPatternBox.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_regexBuilderPatternBox, L"NativeRegexPattern");
        AutomationProperties::SetName(m_regexBuilderPatternBox, L"PCRE2 regular expression pattern · PCRE2 正規表示式模式");
        m_regexBuilderPatternBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            m_regexBuilderPattern = ToWide(sender.as<TextBox>().Text());
            RefreshRegexTesterPreview();
        });
        page.Children().Append(m_regexBuilderPatternBox);

        StackPanel stepContent;
        stepContent.Spacing(8);
        AutomationProperties::SetAutomationId(stepContent, L"NativeRegexBuilderStepContent");

        if (m_regexBuilderStep == 0)
        {
            stepContent.Children().Append(CreateText(
                L"Choose matching flags. These values travel with the selected native search surface when you apply the wizard. · 揀符合旗標；套用精靈時會跟住揀好嘅原生搜尋位置。",
                13));

            auto addFlag = [this, &stepContent](std::wstring_view header, bool value, auto apply, std::wstring_view id)
            {
                ToggleSwitch flag;
                flag.Header(box_value(ToHString(header)));
                flag.IsOn(value);
                AutomationProperties::SetAutomationId(flag, ToHString(id));
                flag.Toggled([apply](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
                {
                    apply(sender.as<ToggleSwitch>().IsOn());
                });
                stepContent.Children().Append(flag);
            };
            addFlag(
                L"Case-sensitive matching · 區分大小寫",
                m_regexBuilderCaseSensitive,
                [this](bool value) { m_regexBuilderCaseSensitive = value; RefreshRegexTesterPreview(); },
                L"NativeRegexBuilderCaseSensitive");
            addFlag(
                L"Multiline anchors (^ and $) · 多行 anchors（^ 同 $）",
                m_regexBuilderMultiline,
                [this](bool value) { m_regexBuilderMultiline = value; RefreshRegexTesterPreview(); },
                L"NativeRegexBuilderMultiline");
            addFlag(
                L"Dot also matches a newline · 點號都符合換行",
                m_regexBuilderDotMatchesNewline,
                [this](bool value) { m_regexBuilderDotMatchesNewline = value; RefreshRegexTesterPreview(); },
                L"NativeRegexBuilderDotAll");
        }
        else if (m_regexBuilderStep == 1)
        {
            stepContent.Children().Append(CreateText(
                L"Start from a safe recipe or compose literal-safe pieces and common PCRE2 tokens. Literal text and character classes are escaped by the native builder before insertion. · 由安全 recipe 開始，或者建立 literal-safe 部件同常用 PCRE2 token；literal 文字同字元類別會先由原生建立器 escape。",
                13));

            TextBox literal;
            literal.Header(box_value(L"Literal text to escape · 要 escape 嘅 literal 文字"));
            literal.Text(ToHString(m_regexBuilderLiteral));
            AutomationProperties::SetAutomationId(literal, L"NativeRegexBuilderLiteral");
            literal.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderLiteral = ToWide(sender.as<TextBox>().Text());
            });
            stepContent.Children().Append(literal);

            ComboBox recipe;
            recipe.Header(box_value(L"Safe starter recipe · 安全起始 recipe"));
            for (auto const& descriptor : winforge::core::regex::RegexRecipes())
            {
                ComboBoxItem item;
                auto label = std::wstring(descriptor.name_en);
                label += L" · ";
                label += descriptor.name_zh;
                item.Content(box_value(ToHString(label)));
                recipe.Items().Append(item);
            }
            recipe.SelectedIndex(std::clamp(
                m_regexBuilderRecipeIndex,
                0,
                static_cast<int32_t>(winforge::core::regex::RegexRecipes().size()) - 1));
            AutomationProperties::SetAutomationId(recipe, L"NativeRegexBuilderRecipe");
            recipe.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
            {
                m_regexBuilderRecipeIndex = std::clamp(
                    sender.as<ComboBox>().SelectedIndex(),
                    0,
                    static_cast<int32_t>(winforge::core::regex::RegexRecipes().size()) - 1);
            });
            stepContent.Children().Append(recipe);

            Button applyRecipe;
            applyRecipe.Content(box_value(L"Replace pattern with recipe · 用 recipe 取代模式"));
            AutomationProperties::SetAutomationId(applyRecipe, L"NativeRegexBuilderApplyRecipe");
            applyRecipe.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                auto const recipes = winforge::core::regex::RegexRecipes();
                auto const index = std::clamp(
                    m_regexBuilderRecipeIndex,
                    0,
                    static_cast<int32_t>(recipes.size()) - 1);
                auto const& descriptor = recipes[static_cast<std::size_t>(index)];
                if (descriptor.requires_literal && !HasNonWhitespace(m_regexBuilderLiteral))
                {
                    if (m_regexBuilderStatus)
                    {
                        auto const message = L"Enter literal text before using this recipe · 用呢個 recipe 前請先輸入 literal 文字";
                        m_regexBuilderStatus.Text(message);
                        AutomationProperties::SetName(m_regexBuilderStatus, message);
                    }
                    return;
                }
                m_regexBuilderPattern = winforge::core::regex::BuildRegexRecipe(
                    descriptor.recipe,
                    m_regexBuilderLiteral);
                if (m_regexBuilderPatternBox)
                {
                    m_regexBuilderPatternBox.Text(ToHString(m_regexBuilderPattern));
                    m_regexBuilderPatternBox.SelectionStart(
                        static_cast<int32_t>(m_regexBuilderPatternBox.Text().size()));
                }
                RefreshRegexTesterPreview();
            });
            stepContent.Children().Append(applyRecipe);

            Button appendLiteral;
            appendLiteral.Content(box_value(L"Append escaped literal · 加入已 escape literal"));
            AutomationProperties::SetAutomationId(appendLiteral, L"NativeRegexBuilderAppendLiteral");
            appendLiteral.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                AppendRegexBuilderToken(winforge::core::regex::EscapeRegexLiteral(m_regexBuilderLiteral));
            });
            stepContent.Children().Append(appendLiteral);

            TextBox characterClass;
            characterClass.Header(box_value(L"Character-class characters · 字元類別字元"));
            characterClass.Text(ToHString(m_regexBuilderCharacterClass));
            AutomationProperties::SetAutomationId(characterClass, L"NativeRegexBuilderCharacterClass");
            characterClass.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderCharacterClass = ToWide(sender.as<TextBox>().Text());
            });
            stepContent.Children().Append(characterClass);

            Button appendClass;
            appendClass.Content(box_value(L"Append character class · 加入字元類別"));
            AutomationProperties::SetAutomationId(appendClass, L"NativeRegexBuilderAppendCharacterClass");
            appendClass.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                AppendRegexBuilderToken(winforge::core::regex::BuildRegexCharacterClass(m_regexBuilderCharacterClass));
            });
            stepContent.Children().Append(appendClass);

            auto addToken = [this, &stepContent](std::wstring_view label, std::wstring_view token, std::wstring_view id)
            {
                Button button;
                button.Content(box_value(ToHString(label)));
                AutomationProperties::SetAutomationId(button, ToHString(id));
                auto ownedToken = std::wstring(token);
                button.Click([this, ownedToken](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
                {
                    AppendRegexBuilderToken(ownedToken);
                });
                stepContent.Children().Append(button);
            };
            addToken(L"Append digit (\\d) · 加入數字", L"\\d", L"NativeRegexBuilderTokenDigit");
            addToken(L"Append word (\\w) · 加入 word", L"\\w", L"NativeRegexBuilderTokenWord");
            addToken(L"Append whitespace (\\s) · 加入空白", L"\\s", L"NativeRegexBuilderTokenWhitespace");
            addToken(L"Append any character (.) · 加入任何字元", L".", L"NativeRegexBuilderTokenAny");
            addToken(L"Append start anchor (^) · 加入開始 anchor", L"^", L"NativeRegexBuilderTokenStart");
            addToken(L"Append end anchor ($) · 加入結束 anchor", L"$", L"NativeRegexBuilderTokenEnd");
        }
        else if (m_regexBuilderStep == 2)
        {
            stepContent.Children().Append(CreateText(
                L"Wrap the current pattern in a non-capturing or named group, build an alternation or assertion, and then apply a quantifier. · 用 non-capturing 或 named group 包住現有模式、建立 alternation 或 assertion，再套用量詞。",
                13));

            TextBox captureName;
            captureName.Header(box_value(L"Optional named capture (letters, digits, underscore) · 可選 named capture（英文字母、數字、底線）"));
            captureName.Text(ToHString(m_regexBuilderCaptureName));
            AutomationProperties::SetAutomationId(captureName, L"NativeRegexBuilderCaptureName");
            captureName.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderCaptureName = ToWide(sender.as<TextBox>().Text());
            });
            stepContent.Children().Append(captureName);

            Button group;
            group.Content(box_value(L"Wrap current pattern in group · 用 group 包住目前模式"));
            AutomationProperties::SetAutomationId(group, L"NativeRegexBuilderGroup");
            group.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                if (!m_regexBuilderPattern.empty())
                {
                    m_regexBuilderPattern = winforge::core::regex::BuildRegexGroup(
                        m_regexBuilderPattern,
                        m_regexBuilderCaptureName);
                    if (m_regexBuilderPatternBox)
                    {
                        m_regexBuilderPatternBox.Text(ToHString(m_regexBuilderPattern));
                    }
                    RefreshRegexTesterPreview();
                }
            });
            stepContent.Children().Append(group);

            TextBox alternatives;
            alternatives.Header(box_value(L"Alternatives, one per line · 每行一個 alternative"));
            alternatives.AcceptsReturn(true);
            alternatives.MinHeight(72);
            alternatives.Text(ToHString(m_regexBuilderAlternatives));
            AutomationProperties::SetAutomationId(alternatives, L"NativeRegexBuilderAlternatives");
            alternatives.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderAlternatives = ToWide(sender.as<TextBox>().Text());
            });
            stepContent.Children().Append(alternatives);

            Button buildAlternation;
            buildAlternation.Content(box_value(L"Append alternation · 加入 alternation"));
            AutomationProperties::SetAutomationId(buildAlternation, L"NativeRegexBuilderAppendAlternation");
            buildAlternation.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                std::vector<std::wstring> values;
                std::wistringstream lines(m_regexBuilderAlternatives);
                std::wstring line;
                while (std::getline(lines, line))
                {
                    if (!line.empty()) values.push_back(line);
                }
                if (!values.empty())
                {
                    AppendRegexBuilderToken(winforge::core::regex::BuildRegexAlternation(values));
                }
            });
            stepContent.Children().Append(buildAlternation);

            ComboBox assertion;
            assertion.Header(box_value(L"Assertion · assertion"));
            for (auto const label : {
                L"Word boundary (\\b) · word 邊界",
                L"Not a word boundary (\\B) · 唔係 word 邊界",
                L"Positive lookahead (?=...) · 正面 lookahead",
                L"Negative lookahead (?!...) · 負面 lookahead",
                L"Positive lookbehind (?<=...) · 正面 lookbehind",
                L"Negative lookbehind (?<!...) · 負面 lookbehind" })
            {
                ComboBoxItem item;
                item.Content(box_value(label));
                assertion.Items().Append(item);
            }
            assertion.SelectedIndex(std::clamp(m_regexBuilderAssertionIndex, 0, 5));
            AutomationProperties::SetAutomationId(assertion, L"NativeRegexBuilderAssertion");
            assertion.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
            {
                m_regexBuilderAssertionIndex = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 5);
            });
            stepContent.Children().Append(assertion);

            TextBox assertionFragment;
            assertionFragment.Header(box_value(L"Lookaround PCRE2 fragment (advanced; not escaped) · lookaround PCRE2 片段（進階；唔會 escape）"));
            assertionFragment.Text(ToHString(m_regexBuilderAssertion));
            AutomationProperties::SetAutomationId(assertionFragment, L"NativeRegexBuilderAssertionFragment");
            assertionFragment.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderAssertion = ToWide(sender.as<TextBox>().Text());
            });
            stepContent.Children().Append(assertionFragment);

            Button appendAssertion;
            appendAssertion.Content(box_value(L"Append assertion · 加入 assertion"));
            AutomationProperties::SetAutomationId(appendAssertion, L"NativeRegexBuilderAppendAssertion");
            appendAssertion.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                auto const token = winforge::core::regex::BuildRegexAssertion(
                    static_cast<winforge::core::regex::RegexAssertion>(
                        std::clamp(m_regexBuilderAssertionIndex, 0, 5)),
                    m_regexBuilderAssertion);
                if (token.empty())
                {
                    if (m_regexBuilderStatus)
                    {
                        auto const message = L"Enter an advanced fragment for a lookaround assertion · lookaround assertion 請輸入進階片段";
                        m_regexBuilderStatus.Text(message);
                        AutomationProperties::SetName(m_regexBuilderStatus, message);
                    }
                    return;
                }
                AppendRegexBuilderToken(token);
            });
            stepContent.Children().Append(appendAssertion);

            ComboBox quantifier;
            quantifier.Header(box_value(L"Quantifier for current pattern · 目前模式嘅量詞"));
            for (auto const label : {
                L"Exactly once · 只限一次",
                L"Zero or more (*) · 零次或以上",
                L"One or more (+) · 一次或以上",
                L"Optional (?) · 可選",
                L"Range {min,max} · 範圍 {min,max}" })
            {
                ComboBoxItem item;
                item.Content(box_value(label));
                quantifier.Items().Append(item);
            }
            quantifier.SelectedIndex(std::clamp(m_regexBuilderQuantifierIndex, 0, 4));
            AutomationProperties::SetAutomationId(quantifier, L"NativeRegexBuilderQuantifier");
            quantifier.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
            {
                m_regexBuilderQuantifierIndex = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 4);
            });
            stepContent.Children().Append(quantifier);

            NumberBox minimum;
            minimum.Header(box_value(L"Range minimum · 範圍最小值"));
            minimum.Minimum(0);
            minimum.Maximum(1000);
            minimum.Value(m_regexBuilderRangeMinimum);
            AutomationProperties::SetAutomationId(minimum, L"NativeRegexBuilderRangeMinimum");
            minimum.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
            {
                auto const value = sender.Value();
                if (!std::isfinite(value)) return;
                m_regexBuilderRangeMinimum = std::clamp(static_cast<int32_t>(std::llround(value)), 0, 1000);
            });
            stepContent.Children().Append(minimum);

            NumberBox maximum;
            maximum.Header(box_value(L"Range maximum · 範圍最大值"));
            maximum.Minimum(0);
            maximum.Maximum(1000);
            maximum.Value(m_regexBuilderRangeMaximum);
            AutomationProperties::SetAutomationId(maximum, L"NativeRegexBuilderRangeMaximum");
            maximum.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
            {
                auto const value = sender.Value();
                if (!std::isfinite(value)) return;
                m_regexBuilderRangeMaximum = std::clamp(static_cast<int32_t>(std::llround(value)), 0, 1000);
            });
            stepContent.Children().Append(maximum);

            ToggleSwitch unbounded;
            unbounded.Header(box_value(L"Unbounded upper range {min,} · 無上限範圍 {min,}"));
            unbounded.IsOn(m_regexBuilderRangeUnbounded);
            AutomationProperties::SetAutomationId(unbounded, L"NativeRegexBuilderRangeUnbounded");
            unbounded.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                m_regexBuilderRangeUnbounded = sender.as<ToggleSwitch>().IsOn();
            });
            stepContent.Children().Append(unbounded);

            Button applyQuantifier;
            applyQuantifier.Content(box_value(L"Apply quantifier · 套用量詞"));
            AutomationProperties::SetAutomationId(applyQuantifier, L"NativeRegexBuilderApplyQuantifier");
            applyQuantifier.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                if (m_regexBuilderPattern.empty())
                {
                    return;
                }
                winforge::core::regex::RegexQuantifierSpec specification;
                specification.kind = static_cast<winforge::core::regex::RegexQuantifier>(
                    std::clamp(m_regexBuilderQuantifierIndex, 0, 4));
                specification.minimum = static_cast<std::uint32_t>(std::max(0, m_regexBuilderRangeMinimum));
                specification.maximum = static_cast<std::uint32_t>(std::max(0, m_regexBuilderRangeMaximum));
                specification.unbounded = m_regexBuilderRangeUnbounded;
                m_regexBuilderPattern = winforge::core::regex::ApplyRegexQuantifier(
                    m_regexBuilderPattern,
                    specification);
                if (m_regexBuilderPatternBox)
                {
                    m_regexBuilderPatternBox.Text(ToHString(m_regexBuilderPattern));
                }
                RefreshRegexTesterPreview();
            });
            stepContent.Children().Append(applyQuantifier);
        }
        else
        {
            stepContent.Children().Append(CreateText(
                L"Test captures before applying. The preview reports offsets and group participation without exposing the test text in accessibility status. · 套用之前先測試擷取；預覽會報告 offset 同 group 有冇符合，但唔會喺 accessibility status 顯示測試文字。",
                13));

            m_regexBuilderTestTextBox = TextBox();
            m_regexBuilderTestTextBox.Header(box_value(L"Test text · 測試文字"));
            m_regexBuilderTestTextBox.AcceptsReturn(true);
            m_regexBuilderTestTextBox.MinHeight(110);
            m_regexBuilderTestTextBox.Text(ToHString(m_regexBuilderTestText));
            AutomationProperties::SetAutomationId(m_regexBuilderTestTextBox, L"NativeRegexTestInput");
            AutomationProperties::SetName(m_regexBuilderTestTextBox, L"Regex test text · 正規表示式測試文字");
            m_regexBuilderTestTextBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderTestText = ToWide(sender.as<TextBox>().Text());
                RefreshRegexTesterPreview();
            });
            stepContent.Children().Append(m_regexBuilderTestTextBox);
        }
        page.Children().Append(stepContent);

        m_regexBuilderStatus = CreateText(L"Validating pattern. · 正在驗證模式。", 12);
        m_regexBuilderStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_regexBuilderStatus, L"NativeRegexStatus");
        AutomationProperties::SetLiveSetting(
            m_regexBuilderStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_regexBuilderStatus);

        m_regexBuilderPreview = CreateText(L"", 12);
        m_regexBuilderPreview.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_regexBuilderPreview, L"NativeRegexBuilderPreview");
        page.Children().Append(m_regexBuilderPreview);

        StackPanel navigation;
        navigation.Orientation(Orientation::Vertical);
        navigation.Spacing(6);
        AutomationProperties::SetAutomationId(navigation, L"NativeRegexBuilderNavigation");

        Button back;
        back.Content(box_value(L"Back · 返回"));
        back.IsEnabled(m_regexBuilderStep > 0);
        AutomationProperties::SetAutomationId(back, L"NativeRegexBuilderBack");
        back.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            m_regexBuilderStep = std::max(0, m_regexBuilderStep - 1);
            RenderRegexTester();
        });
        navigation.Children().Append(back);

        Button next;
        next.Content(box_value(L"Next · 下一步"));
        next.IsEnabled(m_regexBuilderStep < 3);
        AutomationProperties::SetAutomationId(next, L"NativeRegexBuilderNext");
        next.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            m_regexBuilderStep = std::min(3, m_regexBuilderStep + 1);
            RenderRegexTester();
        });
        navigation.Children().Append(next);

        m_regexBuilderApply = Button();
        m_regexBuilderApply.Content(box_value(m_regexBuilderTarget == RegexBuilderTarget::TesterOnly
            ? L"Refresh preview · 重新整理預覽"
            : L"Apply regex to target · 套用正規表示式到目標"));
        AutomationProperties::SetAutomationId(m_regexBuilderApply, L"NativeRegexBuilderApply");
        m_regexBuilderApply.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            ApplyRegexBuilderTarget();
        });
        navigation.Children().Append(m_regexBuilderApply);
        page.Children().Append(navigation);

        ShowPage(page);
        RefreshRegexTesterPreview();
    }

    void MainWindow::RenderAbout()
    {
        auto page = CreatePage(
            L"About WinForge Native · 關於 WinForge 原生版",
            L"A genuine C++20/C++/WinRT rewrite of WinForge for Windows 11. No C++/CLI, CLR hosting, or managed-app wrapper is used by this native shell. · 呢個係 Windows 11 用嘅真正 C++20/C++/WinRT 重寫；原生介面冇用 C++/CLI、CLR host 或包住受控 app。");
        page.Children().Append(CreateText(L"Architecture · 架構", 22, true));
        page.Children().Append(CreateText(
            L"WinUI 3 + Windows App SDK 2.2, self-contained and unpackaged; a canonical bilingual route catalog replaces four-way registration drift. Core logic is being separated into standard C++ libraries so it can be tested without the UI. · WinUI 3 + Windows App SDK 2.2，自包含、免封裝；單一本體雙語路線目錄取代四處重複登記，核心邏輯會拆成標準 C++ 程式庫，唔開 UI 都測到。"));
        page.Children().Append(CreateText(L"Completion rule · 完成規則", 22, true));
        page.Children().Append(CreateText(
            L"The managed application remains in the repository only as an oracle during migration. Final cutover requires every feature, companion, launcher, updater, route, test, screenshot, README, wiki page, and GitHub Pages mirror to pass, followed by a binary audit proving no CLR dependency. · 遷移期間受控版只留低做行為基準；最終切換要所有功能、伴隨 app、啟動器、更新器、路線、測試、截圖、README、wiki 同 GitHub Pages 鏡像全部通過，再用二進位審查證明冇 CLR 相依。"));
        ShowPage(page);
    }

    void MainWindow::RenderPending(winforge::core::ModuleRecord const& module)
    {
        auto page = CreatePage(Label(module), module.id + L"  ·  " + module.kind);

        InfoBar pending;
        pending.IsOpen(true);
        pending.IsClosable(false);
        pending.Severity(InfoBarSeverity::Warning);
        pending.Title(L"Native feature port not complete · 原生功能未移植完成");
        pending.Message(L"This route resolves in the C++ shell, but its controls and service behavior have not yet passed the native parity ledger. It is deliberately not presented as a working clone. · 呢條路線已經可以喺 C++ 介面解析，但控制項同服務行為未通過原生對等清單，所以唔會扮成已經可用。");
        AutomationProperties::SetAutomationId(pending, L"NativeFeaturePending");
        page.Children().Append(pending);

        if (!m_currentArgument.empty())
        {
            page.Children().Append(CreateText(L"Route argument · 路線參數", 16, true));
            page.Children().Append(CreateText(m_currentArgument));
        }
        page.Children().Append(CreateText(L"Aliases · 別名", 16, true));
        std::wstring aliases;
        for (auto const& alias : module.aliases)
        {
            if (!aliases.empty()) aliases += L", ";
            aliases += alias;
        }
        page.Children().Append(CreateText(aliases.empty() ? L"(none)" : aliases, 12));
        ShowPage(page);
    }

    void MainWindow::RenderUnknown(std::wstring_view route)
    {
        auto page = CreatePage(
            L"Unknown native route · 未知原生路線",
            L"The requested route is not present in the generated parity catalog. · 要求嘅路線唔喺產生嘅對等目錄入面。");
        page.Children().Append(CreateText(route, 16, true));
        page.Children().Append(CreateRouteButton(L"Return to Dashboard · 返回概覽", L"dashboard"));
        ShowPage(page);
    }

    void MainWindow::RenderCatalogError(std::string_view message)
    {
        auto page = CreatePage(
            L"Native startup failed · 原生啟動失敗",
            L"The generated module catalog could not be loaded. · 產生嘅模組目錄載入唔到。");
        page.Children().Append(CreateText(winrt::to_hstring(message).c_str(), 13));
        ShowPage(page);
    }

    winforge::core::ModuleRecord const* MainWindow::FindModule(std::wstring_view route) const
    {
        auto const key = winforge::core::NormalizeRouteKey(route);
        if (auto const index = m_routeIndex.FindCanonicalOrAlias(key))
        {
            return &m_modules[*index];
        }
        return nullptr;
    }

    winforge::core::ModuleRecord const* MainWindow::FindLaunchModule(std::wstring_view route) const
    {
        auto const key = winforge::core::NormalizeRouteKey(route);
        if (auto const index = m_routeIndex.FindLaunch(key))
        {
            return &m_modules[*index];
        }
        return nullptr;
    }

    std::shared_ptr<winforge::core::regex::SafeRegex const> MainWindow::CompileSearchRegex(
        std::wstring_view pattern,
        bool caseSensitive,
        bool multiline,
        bool dotMatchesNewline,
        std::wstring& diagnostic) const
    {
        winforge::core::regex::RegexOptions options;
        options.case_sensitive = caseSensitive;
        options.multiline = multiline;
        options.dot_matches_newline = dotMatchesNewline;
        auto compiled = winforge::core::regex::SafeRegex::Compile(pattern, options);
        if (compiled.Ok())
        {
            diagnostic.clear();
            return std::make_shared<winforge::core::regex::SafeRegex>(
                std::move(*compiled.expression));
        }

        auto const& issue = compiled.diagnostic;
        std::wstring category;
        switch (issue.code)
        {
        case winforge::core::regex::RegexErrorCode::PatternTooLong:
            category = L"Pattern is too long · 模式太長";
            break;
        case winforge::core::regex::RegexErrorCode::PatternNestingTooDeep:
            category = L"Pattern nesting is too deep · 模式巢狀太深";
            break;
        case winforge::core::regex::RegexErrorCode::CompiledCodeTooLarge:
            category = L"Pattern compiles to too much code · 模式編譯後太大";
            break;
        case winforge::core::regex::RegexErrorCode::UnsupportedFeature:
            category = L"Pattern uses a disabled feature · 模式使用咗已停用功能";
            break;
        case winforge::core::regex::RegexErrorCode::TooComplex:
            category = L"Pattern exceeds safe resource limits · 模式超出安全資源限制";
            break;
        case winforge::core::regex::RegexErrorCode::Syntax:
        default:
            category = L"Regex syntax needs correction · 正規表示式語法需要修正";
            break;
        }

        diagnostic = std::move(category);
        if (issue.offset > 0)
        {
            diagnostic += L" (character " + std::to_wstring(issue.offset + 1) + L")";
        }
        if (!issue.message.empty())
        {
            diagnostic += L": " + issue.message;
        }
        return {};
    }

    bool MainWindow::Matches(
        winforge::core::ModuleRecord const& module,
        std::wstring_view query,
        winforge::core::regex::SafeRegex const* expression) const
    {
        if (expression)
        {
            auto const matchesField = [expression](std::wstring_view value)
            {
                return expression->Search(value).matched;
            };
            if (matchesField(module.id) || matchesField(module.tag) ||
                matchesField(module.name.en) || matchesField(module.name.zh) ||
                matchesField(module.keywords))
            {
                return true;
            }
            for (auto const& alias : module.aliases)
            {
                if (matchesField(alias))
                {
                    return true;
                }
            }
            return false;
        }

        auto const normalized = winforge::core::NormalizeRouteKey(query);
        if (normalized.empty()) return true;

        std::wstring haystack = module.id + L" " + module.tag + L" " + module.name.en + L" " +
            module.name.zh + L" " + module.keywords;
        for (auto const& alias : module.aliases)
        {
            haystack += L" " + alias;
        }
        haystack = winforge::core::NormalizeRouteKey(haystack);
        return haystack.find(normalized) != std::wstring::npos;
    }

    StackPanel MainWindow::CreatePage(std::wstring_view title, std::wstring_view subtitle)
    {
        StackPanel page;
        page.Spacing(14);
        page.Margin(Thickness{ 36, 28, 36, 36 });
        // Keep every native page inside the measured viewport.  The old shell
        // let horizontal StackPanels measure at their unconstrained width,
        // which clipped bilingual labels and action buttons on narrow windows.
        page.HorizontalAlignment(HorizontalAlignment::Stretch);
        // Keep ordinary desktop pages within a readable content measure so
        // long bilingual text wraps before the shell's horizontal overflow
        // escape hatch becomes necessary.
        page.MaxWidth(1280);
        auto heading = CreateText(title, 32, true);
        heading.TextWrapping(TextWrapping::WrapWholeWords);
        AutomationProperties::SetAutomationId(heading, L"NativePageTitle");
        page.Children().Append(heading);
        if (!subtitle.empty())
        {
            auto subheading = CreateText(subtitle, 14);
            subheading.Opacity(0.78);
            page.Children().Append(subheading);
        }
        return page;
    }

    void MainWindow::ShowPage(UIElement const& element)
    {
        if (!m_content)
        {
            m_content = Grid();
            Root().Children().Clear();
            Root().Children().Append(m_content);
        }
        m_content.Children().Clear();
        ScrollViewer viewer;
        // Horizontal scrolling is an accessibility/overflow escape hatch for
        // long bilingual strings and dense legacy controls.  Pages still
        // stretch to the viewport, but content is never silently clipped.
        viewer.HorizontalScrollMode(ScrollMode::Auto);
        viewer.HorizontalScrollBarVisibility(ScrollBarVisibility::Auto);
        viewer.HorizontalContentAlignment(HorizontalAlignment::Stretch);
        viewer.VerticalContentAlignment(VerticalAlignment::Top);
        viewer.VerticalScrollMode(ScrollMode::Auto);
        viewer.VerticalScrollBarVisibility(ScrollBarVisibility::Auto);
        viewer.Content(element);
        m_content.Children().Append(viewer);
    }

    TextBlock MainWindow::CreateText(std::wstring_view text, double size, bool semibold) const
    {
        TextBlock block;
        block.Text(ToHString(text));
        block.FontSize(size);
        block.TextWrapping(TextWrapping::Wrap);
        block.IsTextSelectionEnabled(true);
        if (semibold)
        {
            block.FontWeight(Microsoft::UI::Text::FontWeights::SemiBold());
        }
        return block;
    }

    Button MainWindow::CreateRouteButton(std::wstring_view label, std::wstring_view route)
    {
        Button button;
        button.Content(box_value(ToHString(label)));
        button.Tag(box_value(ToHString(route)));
        button.HorizontalAlignment(HorizontalAlignment::Left);
        button.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(button, ToHString(L"NativeRoute_" + AutomationKey(route)));
        button.Click([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            auto const clicked = sender.as<Button>();
            auto const routeValue = unbox_value_or<hstring>(clicked.Tag(), L"dashboard");
            Navigate(ToWide(routeValue));
        });
        return button;
    }

    std::wstring MainWindow::Label(winforge::core::ModuleRecord const& module) const
    {
        return module.name.Pick(m_language);
    }

    void MainWindow::OnNavigationInvoked(
        NavigationView const&,
        NavigationViewItemInvokedEventArgs const& args)
    {
        if (args.IsSettingsInvoked())
        {
            Navigate(L"settings");
            return;
        }

        auto const item = args.InvokedItemContainer().try_as<NavigationViewItem>();
        if (!item) return;
        auto const route = unbox_value_or<hstring>(item.Tag(), L"");
        if (!route.empty())
        {
            Navigate(ToWide(route));
        }
    }

    void MainWindow::OnSearchSubmitted(
        AutoSuggestBox const&,
        AutoSuggestBoxQuerySubmittedEventArgs const& args)
    {
        SubmitShellSearch(ToWide(args.QueryText()));
    }

    void MainWindow::SubmitShellSearch(std::wstring_view query)
    {
        if (query.empty()) return;
        if (!m_shellRegexEnabled && FindModule(query))
        {
            Navigate(query);
        }
        else
        {
            Navigate(L"search", query);
        }
    }

    void MainWindow::OnLanguageChanged(Windows::Foundation::IInspectable const&, SelectionChangedEventArgs const&)
    {
        switch (m_languagePicker.SelectedIndex())
        {
        case 1:
            m_language = winforge::core::LanguageMode::Cantonese;
            break;
        case 2:
            m_language = winforge::core::LanguageMode::English;
            break;
        default:
            m_language = winforge::core::LanguageMode::Bilingual;
            break;
        }
        BuildPrimaryNavigation();
        SelectNavigationItem(m_currentRoute);
        RenderCurrent();
    }
}
