#include "pch.h"
#include "MainWindow.xaml.h"

#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "CatalogLoader.h"
#include "../WinForge.Core/PackageParsers.h"
#include "../WinForge.Core/RegexBuilder.h"
#include "../WinForge.Core/RegexCheat.h"
#include "microsoft.ui.xaml.window.h"
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.ApplicationModel.DataTransfer.h>
#include <winrt/Windows.Data.Json.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Storage.h>
#include <winrt/Microsoft.UI.Xaml.Automation.Peers.h>

#include <chrono>
#include <algorithm>
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

    // UI Automation supplies multiline TextBox values with lone CR separators,
    // while a newly rendered WinUI TextBox expects LF for programmatic text.
    // Normalize only the presentation value so the native engines retain their
    // exact CR/LF/CRLF contracts and state survives a language rerender.
    std::wstring TextBoxPresentation(std::wstring_view value)
    {
        std::wstring normalized;
        normalized.reserve(value.size() + 8);
        for (std::size_t index{}; index < value.size(); ++index)
        {
            auto const ch = value[index];
            if (ch == L'\r')
            {
                normalized.push_back(L'\n');
                if (index + 1 < value.size() && value[index + 1] == L'\n') ++index;
            }
            else
            {
                normalized.push_back(ch);
            }
        }
        return normalized;
    }

    Border MakeNativeCard()
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
    }

    Button MakeNativeButton(std::wstring_view label, std::wstring_view automationId)
    {
        Button button;
        button.Content(box_value(ToHString(label)));
        button.HorizontalAlignment(HorizontalAlignment::Stretch);
        button.HorizontalContentAlignment(HorizontalAlignment::Center);
        AutomationProperties::SetAutomationId(button, ToHString(automationId));
        AutomationProperties::SetName(button, ToHString(label));
        return button;
    }

    void RaisePoliteLiveRegion(TextBlock const& status)
    {
        if (!status) return;
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(status);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(status);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility reporting never interrupts local text processing.
        }
    }

    std::wstring FormatAspectNumber(double value, int digits)
    {
        if (std::isnan(value)) return L"NaN";
        if (std::isinf(value))
        {
            wchar_t symbol[16]{};
            auto const localeField = std::signbit(value) ? LOCALE_SNEGINFINITY : LOCALE_SPOSINFINITY;
            if (GetLocaleInfoEx(
                LOCALE_NAME_USER_DEFAULT,
                localeField,
                symbol,
                static_cast<int>(std::size(symbol))) > 1)
            {
                return symbol;
            }
            return std::signbit(value) ? L"-∞" : L"∞";
        }
        wchar_t separator[8]{};
        auto const separatorLength = GetLocaleInfoEx(
            LOCALE_NAME_USER_DEFAULT,
            LOCALE_SDECIMAL,
            separator,
            static_cast<int>(std::size(separator)));
        auto const decimalSeparator = separatorLength > 1
            ? std::wstring_view{ separator, static_cast<std::size_t>(separatorLength - 1) }
            : std::wstring_view{ L"." };
        return winforge::core::aspectratio::FormatDisplayNumber(value, digits, decimalSeparator);
    }

    std::wstring FormatCurrentCount(int value)
    {
        auto const raw = std::to_wstring(value);
        wchar_t decimalSeparator[8]{ L'.', L'\0' };
        wchar_t groupSeparator[8]{ L',', L'\0' };
        wchar_t groupingText[32]{ L'3', L';', L'0', L'\0' };
        static_cast<void>(GetLocaleInfoEx(
            LOCALE_NAME_USER_DEFAULT,
            LOCALE_SDECIMAL,
            decimalSeparator,
            static_cast<int>(std::size(decimalSeparator))));
        static_cast<void>(GetLocaleInfoEx(
            LOCALE_NAME_USER_DEFAULT,
            LOCALE_STHOUSAND,
            groupSeparator,
            static_cast<int>(std::size(groupSeparator))));
        static_cast<void>(GetLocaleInfoEx(
            LOCALE_NAME_USER_DEFAULT,
            LOCALE_SGROUPING,
            groupingText,
            static_cast<int>(std::size(groupingText))));

        UINT grouping{};
        for (auto const ch : groupingText)
        {
            if (ch == L'\0') break;
            if (ch >= L'1' && ch <= L'9') grouping = grouping * 10 + static_cast<UINT>(ch - L'0');
        }
        if (grouping == 0) grouping = 3;
        DWORD negativeOrder{ 1 };
        static_cast<void>(GetLocaleInfoEx(
            LOCALE_NAME_USER_DEFAULT,
            LOCALE_INEGNUMBER | LOCALE_RETURN_NUMBER,
            reinterpret_cast<wchar_t*>(&negativeOrder),
            static_cast<int>(sizeof(negativeOrder) / sizeof(wchar_t))));
        NUMBERFMTW format{};
        format.NumDigits = 0;
        format.LeadingZero = 1;
        format.Grouping = grouping;
        format.lpDecimalSep = decimalSeparator;
        format.lpThousandSep = groupSeparator;
        format.NegativeOrder = negativeOrder;
        wchar_t output[96]{};
        auto const written = GetNumberFormatEx(
            LOCALE_NAME_USER_DEFAULT,
            0,
            raw.c_str(),
            &format,
            output,
            static_cast<int>(std::size(output)));
        return written > 1 ? std::wstring{ output } : raw;
    }

    std::wstring FormatCurrentOneDecimal(double value)
    {
        if (!std::isfinite(value)) return L"NaN";
        wchar_t separator[8]{};
        auto const separatorLength = GetLocaleInfoEx(
            LOCALE_NAME_USER_DEFAULT,
            LOCALE_SDECIMAL,
            separator,
            static_cast<int>(std::size(separator)));
        auto const decimalSeparator = separatorLength > 1
            ? std::wstring_view{ separator, static_cast<std::size_t>(separatorLength - 1) }
            : std::wstring_view{ L"." };
        auto output = winforge::core::aspectratio::FormatDisplayNumber(value, 1, decimalSeparator);
        if (output.find(decimalSeparator) == std::wstring::npos)
        {
            output.append(decimalSeparator);
            output.push_back(L'0');
        }
        return output;
    }

    std::wstring FormatInvariantOneDecimal(double value)
    {
        if (!std::isfinite(value)) return L"NaN";
        auto output = winforge::core::aspectratio::FormatDisplayNumber(value, 1, L".");
        if (output.find(L'.') == std::wstring::npos) output += L".0";
        return output;
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

    // Deep cleanup is deliberately unavailable until the native port has a
    // handle-relative deletion primitive with stable identities. Package removal
    // never deletes LocalAppData content in this migration slice.

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
                diagnostic,
                m_shellRegexIgnorePatternWhitespace,
                m_shellRegexExplicitCapture);
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
                    diagnostic,
                    m_shellRegexIgnorePatternWhitespace,
                    m_shellRegexExplicitCapture);
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

    void MainWindow::ReleaseTextAnalysisRouteState(std::wstring_view nextRoute)
    {
        if (m_currentRoute == L"module.textstats" && nextRoute != L"module.textstats")
        {
            if (m_textStatsFrequencyList)
            {
                m_textStatsFrequencyList.Items().Clear();
            }
            m_textStatsInputBox = nullptr;
            m_textStatsStopWordsBox = nullptr;
            m_textStatsStatus = nullptr;
            for (auto& row : m_textStatsMetricRows)
            {
                row = nullptr;
            }
            m_textStatsEaseHint = nullptr;
            m_textStatsFrequencyList = nullptr;
            m_textStatsFrequencyEmpty = nullptr;
            std::wstring{}.swap(m_textStatsInput);
            m_textStatsRendering = false;
        }
        else if (m_currentRoute == L"module.wordfreq" && nextRoute != L"module.wordfreq")
        {
            if (m_wordFreqResults)
            {
                m_wordFreqResults.Items().Clear();
            }
            m_wordFreqInputBox = nullptr;
            m_wordFreqModeBox = nullptr;
            m_wordFreqMinLengthBox = nullptr;
            m_wordFreqCaseBox = nullptr;
            m_wordFreqPunctuationBox = nullptr;
            m_wordFreqStopWordsBox = nullptr;
            m_wordFreqTotals = nullptr;
            m_wordFreqCopyButton = nullptr;
            m_wordFreqResults = nullptr;
            std::wstring{}.swap(m_wordFreqInput);
            decltype(m_wordFreqLast.rows){}.swap(m_wordFreqLast.rows);
            m_wordFreqLast.totalTokens = 0;
            m_wordFreqLast.uniqueTokens = 0;
            m_wordFreqLast.diversity = 0.0;
            m_wordFreqRendering = false;
        }
        else if (m_currentRoute == L"module.stringcompare" && nextRoute != L"module.stringcompare")
        {
            if (m_stringCompareMetrics)
            {
                m_stringCompareMetrics.Children().Clear();
            }
            m_stringCompareInputA = nullptr;
            m_stringCompareInputB = nullptr;
            m_stringCompareCaseSwitch = nullptr;
            m_stringCompareWhitespaceSwitch = nullptr;
            m_stringCompareMetrics = nullptr;
            m_stringCompareStatus = nullptr;
            std::wstring{}.swap(m_stringCompareA);
            std::wstring{}.swap(m_stringCompareB);
            decltype(m_stringCompareLastRows){}.swap(m_stringCompareLastRows);
            m_stringCompareTruncationWarningActive = false;
            m_stringCompareRendering = false;
        }
    }

    void MainWindow::ReleaseReferenceTextRouteState(std::wstring_view nextRoute)
    {
        if (m_currentRoute == L"module.phonetic" && nextRoute != L"module.phonetic")
        {
            if (m_phoneticRows)
            {
                m_phoneticRows.Items().Clear();
            }
            m_phoneticInputBox = nullptr;
            m_phoneticAlphabetBox = nullptr;
            m_phoneticUpperBox = nullptr;
            m_phoneticPunctuationBox = nullptr;
            m_phoneticSpokenBox = nullptr;
            m_phoneticRows = nullptr;
            m_phoneticStatus = nullptr;
            std::wstring{}.swap(m_phoneticInput);
            std::wstring{}.swap(m_phoneticSpoken);
            m_phoneticRendering = false;
        }
        else if (m_currentRoute == L"module.boxtext" && nextRoute != L"module.boxtext")
        {
            m_boxTextInputBox = nullptr;
            m_boxTextStyleBox = nullptr;
            m_boxTextAlignmentBox = nullptr;
            m_boxTextPaddingBox = nullptr;
            m_boxTextTitleBox = nullptr;
            m_boxTextOutputBox = nullptr;
            m_boxTextStatus = nullptr;
            std::wstring{}.swap(m_boxTextInput);
            std::wstring{}.swap(m_boxTextTitle);
            std::wstring{}.swap(m_boxTextOutput);
            m_boxTextRendering = false;
        }
        else if (m_currentRoute == L"module.htmlentities" && nextRoute != L"module.htmlentities")
        {
            if (m_htmlEntitiesReferenceRows)
            {
                m_htmlEntitiesReferenceRows.Children().Clear();
            }
            m_htmlEntitiesModeBox = nullptr;
            m_htmlEntitiesNonAsciiBox = nullptr;
            m_htmlEntitiesInputBox = nullptr;
            m_htmlEntitiesOutputBox = nullptr;
            m_htmlEntitiesInputCount = nullptr;
            m_htmlEntitiesOutputCount = nullptr;
            m_htmlEntitiesReferenceRows = nullptr;
            m_htmlEntitiesStatus = nullptr;
            std::wstring{}.swap(m_htmlEntitiesInput);
            std::wstring{}.swap(m_htmlEntitiesOutput);
            m_htmlEntitiesRendering = false;
        }
    }

    void MainWindow::ReleaseUuidV5RouteState(std::wstring_view nextRoute)
    {
        if (m_currentRoute == L"module.uuidv5" && nextRoute != L"module.uuidv5")
        {
            m_uuidV5NamespacePicker = nullptr;
            m_uuidV5CustomNamespaceInput = nullptr;
            m_uuidV5VersionPicker = nullptr;
            m_uuidV5NameInput = nullptr;
            m_uuidV5ResultOutput = nullptr;
            m_uuidV5BulkInput = nullptr;
            m_uuidV5BulkOutput = nullptr;
            m_uuidV5Status = nullptr;
            m_uuidV5Rendering = false;
        }
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
            ReleaseTextAnalysisRouteState(L"search");
            ReleaseReferenceTextRouteState(L"search");
            ReleaseUuidV5RouteState(L"search");
            cancelMutationIfLeavingPackages(L"search");
            m_currentRoute = L"search";
            m_currentArgument = std::wstring(argument);
            RenderCurrent();
            return;
        }
        if (normalized == L"manual" && !argument.empty())
        {
            ReleaseTextAnalysisRouteState(L"manual");
            ReleaseReferenceTextRouteState(L"manual");
            ReleaseUuidV5RouteState(L"manual");
            cancelMutationIfLeavingPackages(L"manual");
            m_currentRoute = L"manual";
            m_currentArgument = std::wstring(argument);
            RenderCurrent();
            return;
        }

        auto const* module = deepLink ? FindLaunchModule(normalized) : FindModule(normalized);
        if (!module)
        {
            ReleaseTextAnalysisRouteState(normalized);
            ReleaseReferenceTextRouteState(normalized);
            ReleaseUuidV5RouteState(normalized);
            cancelMutationIfLeavingPackages(normalized);
            m_currentRoute = normalized;
            m_currentArgument = std::wstring(argument);
            RenderUnknown(normalized);
            return;
        }

        ReleaseTextAnalysisRouteState(module->id);
        ReleaseReferenceTextRouteState(module->id);
        ReleaseUuidV5RouteState(module->id);

        // Managed navigation constructs a fresh Page for these stateless local
        // tools. Reset only on an actual navigation; language rerenders call
        // RenderCurrent directly and intentionally retain the active edits.
        if (module->id == L"module.textdiff")
        {
            m_textDiffA.clear();
            m_textDiffB.clear();
            m_textDiffUnified = L"--- A\n+++ B\n";
            m_textDiffIgnoreWhitespace = false;
            m_textDiffIgnoreCase = false;
        }
        else if (module->id == L"module.linetools")
        {
            m_lineToolsInput.clear();
            m_lineToolsPrefix.clear();
            m_lineToolsSuffix.clear();
            m_lineToolsDelimiter = L", ";
            m_lineToolsOutput.clear();
            m_lineToolsStatusEn = L"Ready.";
            m_lineToolsStatusZh = L"準備就緒。";
        }
        else if (module->id == L"module.textsort")
        {
            m_textSortInput.clear();
            m_textSortOutput.clear();
            m_textSortMode = 1;
            m_textSortCaseInsensitive = false;
            m_textSortDeduplicate = false;
            m_textSortTrimBeforeCompare = false;
            m_textSortReverse = false;
            m_textSortShuffle = false;
            m_textSortRemoveBlank = false;
            m_textSortTrimEach = false;
        }
        else if (module->id == L"module.textwrap")
        {
            m_textWrapInput.clear();
            m_textWrapOutput.clear();
            m_textWrapWidth = 72.0;
            m_textWrapBreakLongWords = false;
            m_textWrapPrefix = L"> ";
            m_textWrapIndent = 4.0;
            m_textWrapStatusEn = L"Ready.";
            m_textWrapStatusZh = L"準備就緒。";
        }
        else if (module->id == L"module.textstats")
        {
            std::wstring{}.swap(m_textStatsInput);
            m_textStatsIgnoreStopWords = false;
        }
        else if (module->id == L"module.wordfreq")
        {
            std::wstring{}.swap(m_wordFreqInput);
            m_wordFreqMode = 0;
            m_wordFreqMinLength = 1.0;
            m_wordFreqCaseInsensitive = true;
            m_wordFreqStripPunctuation = true;
            m_wordFreqRemoveStopWords = false;
            decltype(m_wordFreqLast.rows){}.swap(m_wordFreqLast.rows);
            m_wordFreqLast.totalTokens = 0;
            m_wordFreqLast.uniqueTokens = 0;
            m_wordFreqLast.diversity = 0.0;
        }
        else if (module->id == L"module.stringcompare")
        {
            std::wstring{}.swap(m_stringCompareA);
            std::wstring{}.swap(m_stringCompareB);
            m_stringCompareIgnoreCase = false;
            m_stringCompareIgnoreWhitespace = false;
            decltype(m_stringCompareLastRows){}.swap(m_stringCompareLastRows);
            m_stringCompareTruncationWarningActive = false;
        }
        else if (module->id == L"module.phonetic")
        {
            m_phoneticInput.clear();
            m_phoneticAlphabet = 0;
            m_phoneticUpper = false;
            m_phoneticKeepPunctuation = true;
            m_phoneticSpoken.clear();
        }
        else if (module->id == L"module.boxtext")
        {
            m_boxTextInput.clear();
            m_boxTextStyle = 0;
            m_boxTextAlignment = 0;
            m_boxTextPadding = 1.0;
            m_boxTextTitle.clear();
            m_boxTextOutput.clear();
        }
        else if (module->id == L"module.htmlentities")
        {
            m_htmlEntitiesInput.clear();
            m_htmlEntitiesOutput.clear();
            m_htmlEntitiesDecode = false;
            m_htmlEntitiesEscapeNonAscii = false;
        }
        else if (module->id == L"module.uuidv5")
        {
            m_uuidV5NamespaceIndex = 0;
            m_uuidV5VersionIndex = 0;
            m_uuidV5CustomNamespaceValue.clear();
            m_uuidV5NameValue.clear();
            m_uuidV5ResultValue.clear();
            m_uuidV5BulkInputValue.clear();
            m_uuidV5BulkOutputValue.clear();
        }
        else if (module->id == L"module.aspectratio")
        {
            m_aspectWidthValue = 1920.0;
            m_aspectHeightValue = 1080.0;
            m_aspectRatioWidth = 16.0;
            m_aspectRatioHeight = 9.0;
            m_aspectTargetWidthValue = 1280.0;
            m_aspectTargetHeightValue = 720.0;
            m_aspectPresetIndex = 0;
        }
        else if (module->id == L"module.cssunits")
        {
            m_cssInputValue = L"16";
            m_cssUnitIndex = 0;
            m_cssContextValues = { 16.0, 16.0, 1920.0, 1080.0, 1000.0 };
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
        else if (module->id == L"module.uninstall")
        {
            RenderAppUninstaller();
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
        else if (module->id == L"module.uuidv5")
        {
            RenderUuidV5();
        }
        else if (module->id == L"module.romannum")
        {
            RenderRomanNum();
        }
        else if (module->id == L"module.unixperm")
        {
            RenderUnixPerm();
        }
        else if (module->id == L"module.textdiff")
        {
            RenderTextDiff();
        }
        else if (module->id == L"module.linetools")
        {
            RenderLineTools();
        }
        else if (module->id == L"module.textsort")
        {
            RenderTextSort();
        }
        else if (module->id == L"module.textwrap")
        {
            RenderTextWrap();
        }
        else if (module->id == L"module.textstats")
        {
            RenderTextStats();
        }
        else if (module->id == L"module.wordfreq")
        {
            RenderWordFrequency();
        }
        else if (module->id == L"module.stringcompare")
        {
            RenderStringCompare();
        }
        else if (module->id == L"module.phonetic")
        {
            RenderPhonetic();
        }
        else if (module->id == L"module.boxtext")
        {
            RenderBoxText();
        }
        else if (module->id == L"module.htmlentities")
        {
            RenderHtmlEntities();
        }
        else if (module->id == L"module.aspectratio")
        {
            RenderAspectRatio();
        }
        else if (module->id == L"module.cssunits")
        {
            RenderCssUnits();
        }
        else if (module->id == L"module.regextester")
        {
            RenderRegexTester();
        }
        else if (module->id == L"module.regexcheat")
        {
            RenderRegexCheatsheet();
        }
        else if (module->id == L"module.symbols")
        {
            RenderSymbolsPalette();
        }
        else if (module->id == L"about")
        {
            RenderAbout();
        }
        else if (winforge::core::HasNativeRenderer(module->id))
        {
            RenderCatalogError("The route is marked as implemented but has no native renderer dispatch.");
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

    void MainWindow::RenderUnixPerm()
    {
        using namespace winforge::core::unixperm;
        m_unixPermRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"chmod Calculator", L"chmod 計算機" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Build a Unix file mode from owner, group, and other permissions, or type octal and symbolic modes directly. The command is a local preview and is never executed.",
                L"用擁有者、群組同其他人權限砌 Unix 檔案模式，亦可以直接輸入八進位或者符號模式。指令只係本機預覽，絕對唔會執行。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeUnixPermPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native permission calculator", L"全原生權限計算機" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"All 4,096 modes, special s/S/t/T semantics, two-way parsing, and command previews run in standard C++. Only an explicit Copy button writes to the clipboard.",
            L"全部 4,096 個模式、特殊 s/S/t/T 語義、雙向解析同指令預覽都喺標準 C++ 執行；只有明確撳 Copy 先會寫入剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeUnixPermImplementationStatus");
        page.Children().Append(implementation);

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

        Border matrixCard = makeCard();
        StackPanel matrix;
        matrix.Spacing(10);
        matrix.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Permission bits", L"權限位" }.Pick(m_language), 15, true));

        struct PermissionColumn
        {
            std::wstring_view en;
            std::wstring_view zh;
            std::wstring_view automation;
        };
        constexpr std::array<PermissionColumn, 3> columns{{
            { L"Read", L"讀", L"Read" },
            { L"Write", L"寫", L"Write" },
            { L"Execute", L"執行", L"Execute" },
        }};
        constexpr std::array<Mode, 9> permissionBits{
            OwnerR, OwnerW, OwnerX,
            GroupR, GroupW, GroupX,
            OtherR, OtherW, OtherX,
        };
        struct PermissionRow
        {
            std::wstring_view en;
            std::wstring_view zh;
            std::wstring_view automation;
        };
        constexpr std::array<PermissionRow, 3> rows{{
            { L"Owner", L"擁有者", L"Owner" },
            { L"Group", L"群組", L"Group" },
            { L"Other", L"其他", L"Other" },
        }};

        for (std::size_t rowIndex{}; rowIndex < rows.size(); ++rowIndex)
        {
            StackPanel row;
            row.Orientation(Orientation::Horizontal);
            row.Spacing(10);
            row.HorizontalAlignment(HorizontalAlignment::Left);
            auto rowLabel = CreateText(
                winforge::core::LocalizedText{
                    std::wstring(rows[rowIndex].en), std::wstring(rows[rowIndex].zh) }.Pick(m_language),
                13.5,
                true);
            rowLabel.Width(105);
            rowLabel.VerticalAlignment(VerticalAlignment::Center);
            AutomationProperties::SetAutomationId(
                rowLabel,
                ToHString(L"NativeUnixPerm" + std::wstring(rows[rowIndex].automation) + L"Label"));
            row.Children().Append(rowLabel);

            for (std::size_t columnIndex{}; columnIndex < columns.size(); ++columnIndex)
            {
                auto const index = rowIndex * columns.size() + columnIndex;
                auto& check = m_unixPermChecks[index];
                check = CheckBox();
                check.Content(box_value(ToHString(winforge::core::LocalizedText{
                    std::wstring(columns[columnIndex].en), std::wstring(columns[columnIndex].zh) }.Pick(m_language))));
                check.IsChecked((m_unixPermMode & permissionBits[index]) != 0);
                check.MinWidth(135);
                check.VerticalAlignment(VerticalAlignment::Center);
                auto const accessible = winforge::core::LocalizedText{
                    std::wstring(rows[rowIndex].en) + L" " + std::wstring(columns[columnIndex].en),
                    std::wstring(rows[rowIndex].zh) + std::wstring(columns[columnIndex].zh) }.Pick(m_language);
                AutomationProperties::SetAutomationId(
                    check,
                    ToHString(L"NativeUnixPerm" + std::wstring(rows[rowIndex].automation) +
                        std::wstring(columns[columnIndex].automation)));
                AutomationProperties::SetName(check, ToHString(accessible));
                row.Children().Append(check);
            }
            matrix.Children().Append(row);
        }

        auto specialTitle = CreateText(
            winforge::core::LocalizedText{ L"Special bits", L"特殊權限位" }.Pick(m_language), 13.5, true);
        specialTitle.Margin(Thickness{ 0, 4, 0, 0 });
        matrix.Children().Append(specialTitle);
        struct SpecialRow
        {
            Mode bit;
            std::wstring_view en;
            std::wstring_view zh;
            std::wstring_view automation;
        };
        constexpr std::array<SpecialRow, 3> specials{{
            { SetUid, L"setuid (4000)", L"setuid（4000）", L"SetUid" },
            { SetGid, L"setgid (2000)", L"setgid（2000）", L"SetGid" },
            { Sticky, L"sticky (1000)", L"sticky 黏著位（1000）", L"Sticky" },
        }};
        for (std::size_t index{}; index < specials.size(); ++index)
        {
            auto& check = m_unixPermChecks[9 + index];
            check = CheckBox();
            check.Content(box_value(ToHString(winforge::core::LocalizedText{
                std::wstring(specials[index].en), std::wstring(specials[index].zh) }.Pick(m_language))));
            check.IsChecked((m_unixPermMode & specials[index].bit) != 0);
            AutomationProperties::SetAutomationId(
                check,
                ToHString(L"NativeUnixPerm" + std::wstring(specials[index].automation)));
            AutomationProperties::SetName(
                check,
                ToHString(winforge::core::LocalizedText{
                    std::wstring(specials[index].en), std::wstring(specials[index].zh) }.Pick(m_language)));
            matrix.Children().Append(check);
        }

        auto permissionChanged = [this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_unixPermRendering) return;
            m_unixPermMode = ReadUnixPermMode();
            RefreshUnixPerm(true, true, false, true);
        };
        for (auto const& check : m_unixPermChecks)
        {
            check.Checked(permissionChanged);
            check.Unchecked(permissionChanged);
        }
        matrixCard.Child(matrix);
        page.Children().Append(matrixCard);

        Border valueCard = makeCard();
        StackPanel values;
        values.Spacing(10);
        values.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Representations", L"表示方式" }.Pick(m_language), 15, true));

        auto octalLabel = CreateText(
            winforge::core::LocalizedText{ L"Octal mode", L"八進位模式" }.Pick(m_language), 13.5, true);
        values.Children().Append(octalLabel);
        StackPanel octalRow;
        octalRow.Orientation(Orientation::Horizontal);
        octalRow.Spacing(10);
        m_unixPermOctalInput = TextBox();
        m_unixPermOctalInput.Text(ToHString(m_unixPermOctalInputValue));
        m_unixPermOctalInput.MinWidth(220);
        m_unixPermOctalInput.FontFamily(Media::FontFamily(L"Consolas"));
        m_unixPermOctalInput.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_unixPermOctalInput, L"NativeUnixPermOctalInput");
        AutomationProperties::SetName(m_unixPermOctalInput, ToHString(winforge::core::LocalizedText{
            L"Octal permission mode", L"八進位權限模式" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_unixPermOctalInput, octalLabel);
        m_unixPermOctalInput.TextChanged([this](
            Windows::Foundation::IInspectable const& sender,
            TextChangedEventArgs const&)
        {
            if (m_unixPermRendering) return;
            m_unixPermOctalInputValue = ToWide(sender.as<TextBox>().Text());
            Mode parsed{};
            if (TryParseOctal(m_unixPermOctalInputValue, parsed))
            {
                m_unixPermMode = parsed;
                RefreshUnixPerm(false, true, true, true);
            }
            else
            {
                AnnounceUnixPermStatus(winforge::core::LocalizedText{
                    L"Invalid octal — use one to four digits from 0–7 (for example 755 or 4755).",
                    L"八進位唔啱 — 請用一至四個 0–7 數字（例如 755 或 4755）。" }.Pick(m_language), true);
            }
        });
        octalRow.Children().Append(m_unixPermOctalInput);
        Button copyOctal;
        copyOctal.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(copyOctal, L"NativeUnixPermCopyOctal");
        AutomationProperties::SetName(copyOctal, ToHString(winforge::core::LocalizedText{
            L"Copy octal mode", L"複製八進位模式" }.Pick(m_language)));
        copyOctal.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUnixPermValue(ToOctal(m_unixPermMode));
        });
        octalRow.Children().Append(copyOctal);
        values.Children().Append(octalRow);

        auto symbolicLabel = CreateText(
            winforge::core::LocalizedText{ L"Symbolic mode", L"符號模式" }.Pick(m_language), 13.5, true);
        values.Children().Append(symbolicLabel);
        StackPanel symbolicRow;
        symbolicRow.Orientation(Orientation::Horizontal);
        symbolicRow.Spacing(10);
        m_unixPermSymbolicInput = TextBox();
        m_unixPermSymbolicInput.Text(ToHString(m_unixPermSymbolicInputValue));
        m_unixPermSymbolicInput.MinWidth(220);
        m_unixPermSymbolicInput.FontFamily(Media::FontFamily(L"Consolas"));
        m_unixPermSymbolicInput.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_unixPermSymbolicInput, L"NativeUnixPermSymbolicInput");
        AutomationProperties::SetName(m_unixPermSymbolicInput, ToHString(winforge::core::LocalizedText{
            L"Symbolic permission mode", L"符號權限模式" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_unixPermSymbolicInput, symbolicLabel);
        m_unixPermSymbolicInput.TextChanged([this](
            Windows::Foundation::IInspectable const& sender,
            TextChangedEventArgs const&)
        {
            if (m_unixPermRendering) return;
            m_unixPermSymbolicInputValue = ToWide(sender.as<TextBox>().Text());
            Mode parsed{};
            if (TryParseSymbolic(m_unixPermSymbolicInputValue, parsed))
            {
                m_unixPermMode = parsed;
                RefreshUnixPerm(true, false, true, true);
            }
            else
            {
                AnnounceUnixPermStatus(winforge::core::LocalizedText{
                    L"Invalid symbolic mode — use nine characters like rwxr-xr-x (s/S/t/T are allowed).",
                    L"符號模式唔啱 — 請用九個字元，例如 rwxr-xr-x（可以用 s/S/t/T）。" }.Pick(m_language), true);
            }
        });
        symbolicRow.Children().Append(m_unixPermSymbolicInput);
        Button copySymbolic;
        copySymbolic.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(copySymbolic, L"NativeUnixPermCopySymbolic");
        AutomationProperties::SetName(copySymbolic, ToHString(winforge::core::LocalizedText{
            L"Copy symbolic mode", L"複製符號模式" }.Pick(m_language)));
        copySymbolic.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUnixPermValue(ToSymbolic(m_unixPermMode));
        });
        symbolicRow.Children().Append(copySymbolic);
        values.Children().Append(symbolicRow);

        auto commandLabel = CreateText(
            winforge::core::LocalizedText{ L"Command preview (not executed)", L"指令預覽（唔會執行）" }.Pick(m_language),
            13.5,
            true);
        values.Children().Append(commandLabel);
        StackPanel commandRow;
        commandRow.Orientation(Orientation::Horizontal);
        commandRow.Spacing(10);
        m_unixPermCommandOutput = TextBox();
        m_unixPermCommandOutput.IsReadOnly(true);
        m_unixPermCommandOutput.IsSpellCheckEnabled(false);
        m_unixPermCommandOutput.MinWidth(320);
        m_unixPermCommandOutput.FontFamily(Media::FontFamily(L"Consolas"));
        AutomationProperties::SetAutomationId(m_unixPermCommandOutput, L"NativeUnixPermCommandOutput");
        AutomationProperties::SetName(m_unixPermCommandOutput, ToHString(winforge::core::LocalizedText{
            L"chmod command preview", L"chmod 指令預覽" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_unixPermCommandOutput, commandLabel);
        commandRow.Children().Append(m_unixPermCommandOutput);
        Button copyCommand;
        copyCommand.Content(box_value(ToHString(winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(copyCommand, L"NativeUnixPermCopyCommand");
        AutomationProperties::SetName(copyCommand, ToHString(winforge::core::LocalizedText{
            L"Copy chmod command", L"複製 chmod 指令" }.Pick(m_language)));
        copyCommand.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUnixPermValue(L"chmod " + ToChmodOctal(m_unixPermMode) + L" file");
        });
        commandRow.Children().Append(copyCommand);
        values.Children().Append(commandRow);

        valueCard.Child(values);
        page.Children().Append(valueCard);

        m_unixPermStatus = CreateText(L"", 12.5);
        m_unixPermStatus.Opacity(0.84);
        m_unixPermStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_unixPermStatus, L"NativeUnixPermStatus");
        AutomationProperties::SetLiveSetting(
            m_unixPermStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_unixPermStatus);

        ShowPage(page);
        m_unixPermRendering = false;
        RefreshUnixPerm(false, false, false, true);
    }

    winforge::core::unixperm::Mode MainWindow::ReadUnixPermMode() const
    {
        using namespace winforge::core::unixperm;
        constexpr std::array<Mode, 12> bits{
            OwnerR, OwnerW, OwnerX,
            GroupR, GroupW, GroupX,
            OtherR, OtherW, OtherX,
            SetUid, SetGid, Sticky,
        };
        Mode mode{};
        for (std::size_t index{}; index < bits.size(); ++index)
        {
            auto const value = m_unixPermChecks[index].IsChecked();
            if (value && value.Value())
            {
                mode = static_cast<Mode>(mode | bits[index]);
            }
        }
        return mode;
    }

    void MainWindow::RefreshUnixPerm(
        bool refreshOctal,
        bool refreshSymbolic,
        bool refreshChecks,
        bool announceMode)
    {
        using namespace winforge::core::unixperm;
        auto const previousRendering = m_unixPermRendering;
        m_unixPermRendering = true;

        if (refreshOctal)
        {
            m_unixPermOctalInputValue = ToOctal(m_unixPermMode);
            if (m_unixPermOctalInput)
            {
                m_unixPermOctalInput.Text(ToHString(m_unixPermOctalInputValue));
            }
        }
        if (refreshSymbolic)
        {
            m_unixPermSymbolicInputValue = ToSymbolic(m_unixPermMode);
            if (m_unixPermSymbolicInput)
            {
                m_unixPermSymbolicInput.Text(ToHString(m_unixPermSymbolicInputValue));
            }
        }
        if (refreshChecks)
        {
            constexpr std::array<Mode, 12> bits{
                OwnerR, OwnerW, OwnerX,
                GroupR, GroupW, GroupX,
                OtherR, OtherW, OtherX,
                SetUid, SetGid, Sticky,
            };
            for (std::size_t index{}; index < bits.size(); ++index)
            {
                if (m_unixPermChecks[index])
                {
                    m_unixPermChecks[index].IsChecked((m_unixPermMode & bits[index]) != 0);
                }
            }
        }

        auto const command = L"chmod " + ToChmodOctal(m_unixPermMode) + L" file";
        if (m_unixPermCommandOutput)
        {
            m_unixPermCommandOutput.Text(ToHString(command));
            AutomationProperties::SetHelpText(m_unixPermCommandOutput, ToHString(command));
        }
        m_unixPermRendering = previousRendering;

        if (announceMode)
        {
            auto const octal = ToOctal(m_unixPermMode);
            auto const symbolic = ToSymbolic(m_unixPermMode);
            AnnounceUnixPermStatus(winforge::core::LocalizedText{
                L"Mode " + octal + L" — " + symbolic,
                L"模式 " + octal + L" — " + symbolic }.Pick(m_language));
        }
    }

    void MainWindow::CopyUnixPermValue(std::wstring_view value)
    {
        try
        {
            Windows::ApplicationModel::DataTransfer::DataPackage package;
            package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
            package.SetText(ToHString(value));
            Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
            AnnounceUnixPermStatus(winforge::core::LocalizedText{
                L"Copied: " + std::wstring(value),
                L"已複製：" + std::wstring(value) }.Pick(m_language));
        }
        catch (...)
        {
            AnnounceUnixPermStatus(winforge::core::LocalizedText{
                L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
        }
    }

    void MainWindow::AnnounceUnixPermStatus(std::wstring_view message, bool warning)
    {
        if (!m_unixPermStatus) return;
        m_unixPermStatus.Text(ToHString(message));
        m_unixPermStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_unixPermStatus, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_unixPermStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_unixPermStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_unixPermStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility reporting must not interrupt a local calculation.
        }
    }

    void MainWindow::RenderTextDiff()
    {
        using namespace winforge::core::textdiff;
        m_textDiffRendering = true;

        // UI Automation supplies multiline TextBox values with lone CR
        // separators. WinUI accepts them during an edit, but a newly rendered
        // TextBox expects LF separators for programmatic assignment. Normalize
        // only the presentation value; the diff engine retains its exact
        // managed CR/LF splitting contract.
        auto textBoxText = [](std::wstring_view value)
        {
            std::wstring normalized;
            normalized.reserve(value.size() + 8);
            for (std::size_t index = 0; index < value.size(); ++index)
            {
                auto const ch = value[index];
                if (ch == L'\r')
                {
                    normalized.push_back(L'\n');
                    if (index + 1 < value.size() && value[index + 1] == L'\n') ++index;
                }
                else if (ch == L'\n')
                {
                    normalized.push_back(L'\n');
                }
                else
                {
                    normalized.push_back(ch);
                }
            }
            return normalized;
        };

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Text Diff", L"文字差異比對" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Paste two blocks of text and compare them line by line. Added lines are green, removed lines are red, and the comparison updates as you type.",
                L"貼兩段文字，逐行睇差異。加咗嘅係綠色、刪咗嘅係紅色，打字嗰陣會即時比對。" }.Pick(m_language));
        page.MaxWidth(980);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeTextDiffPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native bounded text diff", L"全原生受限制文字差異比對" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Line splitting, normalization, bounded LCS, counts, and unified-diff output run locally in standard C++. Clipboard access only happens after the explicit Copy button.",
            L"分行、正規化、受限制 LCS、統計同統一差異輸出全部喺本機標準 C++ 執行；只有明確撳 Copy 先會存取剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeTextDiffImplementationStatus");
        page.Children().Append(implementation);

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

        Border inputsCard = makeCard();
        StackPanel inputs;
        inputs.Spacing(10);
        inputs.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Compare text", L"比對文字" }.Pick(m_language), 15, true));

        auto labelA = CreateText(
            winforge::core::LocalizedText{ L"A (original)", L"A（原本）" }.Pick(m_language), 13.5, true);
        inputs.Children().Append(labelA);
        m_textDiffInputA = TextBox();
        m_textDiffInputA.AcceptsReturn(true);
        m_textDiffInputA.Text(ToHString(textBoxText(m_textDiffA)));
        m_textDiffInputA.TextWrapping(TextWrapping::Wrap);
        m_textDiffInputA.MinHeight(180);
        m_textDiffInputA.MaxHeight(320);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textDiffInputA, ScrollBarVisibility::Auto);
        m_textDiffInputA.FontFamily(Media::FontFamily(L"Consolas"));
        m_textDiffInputA.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_textDiffInputA, L"NativeTextDiffInputA");
        AutomationProperties::SetName(m_textDiffInputA, ToHString(winforge::core::LocalizedText{
            L"Original text A", L"原本文字 A" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textDiffInputA, labelA);
        m_textDiffInputA.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_textDiffRendering) return;
            m_textDiffA = ToWide(sender.as<TextBox>().Text());
            RefreshTextDiff();
        });
        inputs.Children().Append(m_textDiffInputA);

        auto labelB = CreateText(
            winforge::core::LocalizedText{ L"B (changed)", L"B（改咗）" }.Pick(m_language), 13.5, true);
        inputs.Children().Append(labelB);
        m_textDiffInputB = TextBox();
        m_textDiffInputB.AcceptsReturn(true);
        m_textDiffInputB.Text(ToHString(textBoxText(m_textDiffB)));
        m_textDiffInputB.TextWrapping(TextWrapping::Wrap);
        m_textDiffInputB.MinHeight(180);
        m_textDiffInputB.MaxHeight(320);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textDiffInputB, ScrollBarVisibility::Auto);
        m_textDiffInputB.FontFamily(Media::FontFamily(L"Consolas"));
        m_textDiffInputB.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_textDiffInputB, L"NativeTextDiffInputB");
        AutomationProperties::SetName(m_textDiffInputB, ToHString(winforge::core::LocalizedText{
            L"Changed text B", L"改咗嘅文字 B" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textDiffInputB, labelB);
        m_textDiffInputB.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_textDiffRendering) return;
            m_textDiffB = ToWide(sender.as<TextBox>().Text());
            RefreshTextDiff();
        });
        inputs.Children().Append(m_textDiffInputB);

        StackPanel options;
        options.Orientation(Orientation::Horizontal);
        options.Spacing(22);
        m_textDiffWhitespace = ToggleSwitch();
        m_textDiffWhitespace.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Ignore whitespace", L"忽略空白" }.Pick(m_language))));
        m_textDiffWhitespace.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
        m_textDiffWhitespace.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
        m_textDiffWhitespace.IsOn(m_textDiffIgnoreWhitespace);
        AutomationProperties::SetAutomationId(m_textDiffWhitespace, L"NativeTextDiffIgnoreWhitespace");
        m_textDiffWhitespace.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_textDiffRendering) return;
            m_textDiffIgnoreWhitespace = sender.as<ToggleSwitch>().IsOn();
            RefreshTextDiff();
        });
        options.Children().Append(m_textDiffWhitespace);

        m_textDiffCase = ToggleSwitch();
        m_textDiffCase.Header(box_value(ToHString(winforge::core::LocalizedText{
            L"Ignore case", L"忽略大小寫" }.Pick(m_language))));
        m_textDiffCase.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
        m_textDiffCase.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
        m_textDiffCase.IsOn(m_textDiffIgnoreCase);
        AutomationProperties::SetAutomationId(m_textDiffCase, L"NativeTextDiffIgnoreCase");
        m_textDiffCase.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_textDiffRendering) return;
            m_textDiffIgnoreCase = sender.as<ToggleSwitch>().IsOn();
            RefreshTextDiff();
        });
        options.Children().Append(m_textDiffCase);
        inputs.Children().Append(options);
        inputsCard.Child(inputs);
        page.Children().Append(inputsCard);

        Border resultCard = makeCard();
        StackPanel result;
        result.Spacing(10);
        StackPanel heading;
        heading.Orientation(Orientation::Horizontal);
        heading.Spacing(16);
        heading.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Line-level diff", L"逐行差異" }.Pick(m_language), 15, true));
        Button copy;
        copy.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Copy unified diff", L"複製統一差異" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(copy, L"NativeTextDiffCopy");
        AutomationProperties::SetName(copy, ToHString(winforge::core::LocalizedText{
            L"Copy unified diff", L"複製統一差異" }.Pick(m_language)));
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_textDiffUnified));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceTextDiffStatus(winforge::core::LocalizedText{
                    L"Unified diff copied to the clipboard.", L"統一差異已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceTextDiffStatus(winforge::core::LocalizedText{
                    L"Could not copy to the clipboard.", L"複製唔到去剪貼簿。" }.Pick(m_language), true);
            }
        });
        heading.Children().Append(copy);
        result.Children().Append(heading);

        m_textDiffCounts = CreateText(L"", 13, true);
        AutomationProperties::SetAutomationId(m_textDiffCounts, L"NativeTextDiffCounts");
        result.Children().Append(m_textDiffCounts);
        m_textDiffRows = ListView();
        m_textDiffRows.SelectionMode(ListViewSelectionMode::None);
        m_textDiffRows.MaxHeight(420);
        m_textDiffRows.HorizontalContentAlignment(HorizontalAlignment::Stretch);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textDiffRows, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollBarVisibility(m_textDiffRows, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollMode(m_textDiffRows, ScrollMode::Enabled);
        AutomationProperties::SetAutomationId(m_textDiffRows, L"NativeTextDiffRows");
        m_textDiffRows.ContainerContentChanging([](
            ListViewBase const&,
            ContainerContentChangingEventArgs const& args)
        {
            if (args.InRecycleQueue()) return;
            auto const container = args.ItemContainer();
            if (!container || !args.Item()) return;
            auto const value = unbox_value_or<hstring>(args.Item(), L"");
            auto const text = ToWide(value);
            container.Padding(Thickness{ 8, 1, 8, 1 });
            container.MinHeight(0);
            container.HorizontalContentAlignment(HorizontalAlignment::Stretch);
            container.FontFamily(Media::FontFamily(L"Consolas"));
            if (!text.empty() && text.front() == L'+')
            {
                container.Foreground(Media::SolidColorBrush(Windows::UI::Color{ 0xFF, 0x3F, 0xB9, 0x50 }));
            }
            else if (!text.empty() && text.front() == L'-')
            {
                container.Foreground(Media::SolidColorBrush(Windows::UI::Color{ 0xFF, 0xE0, 0x4B, 0x4B }));
            }
            else
            {
                container.Foreground(Application::Current().Resources().Lookup(
                    box_value(L"TextFillColorPrimaryBrush")).as<Media::Brush>());
            }
            AutomationProperties::SetAutomationId(
                container,
                ToHString(L"NativeTextDiffLine" + std::to_wstring(args.ItemIndex())));
            AutomationProperties::SetName(container, value);
        });
        result.Children().Append(m_textDiffRows);
        resultCard.Child(result);
        page.Children().Append(resultCard);

        m_textDiffStatus = CreateText(L"", 12.5);
        m_textDiffStatus.Opacity(0.84);
        m_textDiffStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_textDiffStatus, L"NativeTextDiffStatus");
        AutomationProperties::SetLiveSetting(
            m_textDiffStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_textDiffStatus);

        ShowPage(page);
        m_textDiffRendering = false;
        RefreshTextDiff();
    }

    void MainWindow::RefreshTextDiff()
    {
        using namespace winforge::core::textdiff;
        if (!m_textDiffRows || !m_textDiffCounts || !m_textDiffStatus) return;

        try
        {
            auto const diff = Compute(
                m_textDiffA,
                m_textDiffB,
                m_textDiffIgnoreWhitespace,
                m_textDiffIgnoreCase);
            m_textDiffUnified = ToUnifiedDiff(diff);
            m_textDiffRows.Items().Clear();

            for (auto const& line : diff.lines)
            {
                m_textDiffRows.Items().Append(box_value(ToHString(
                    std::wstring(1, line.prefix) + L" " + line.text)));
            }

            auto const counts = winforge::core::LocalizedText{
                L"+" + std::to_wstring(diff.added) + L" added   -" + std::to_wstring(diff.removed) +
                    L" removed   " + std::to_wstring(diff.unchanged) + L" unchanged",
                L"+" + std::to_wstring(diff.added) + L" 加   -" + std::to_wstring(diff.removed) +
                    L" 減   " + std::to_wstring(diff.unchanged) + L" 無變" }.Pick(m_language);
            m_textDiffCounts.Text(ToHString(counts));
            AutomationProperties::SetName(m_textDiffCounts, ToHString(counts));

            AnnounceTextDiffStatus(diff.truncated
                ? winforge::core::LocalizedText{
                    L"Input very large — showing a simplified diff.", L"輸入好大 — 顯示咗簡化版差異。" }.Pick(m_language)
                : L"");
        }
        catch (...)
        {
            AnnounceTextDiffStatus(winforge::core::LocalizedText{
                L"Could not compute the diff.", L"計唔到差異。" }.Pick(m_language), true);
        }
    }

    void MainWindow::AnnounceTextDiffStatus(std::wstring_view message, bool warning)
    {
        if (!m_textDiffStatus) return;
        m_textDiffStatus.Text(ToHString(message));
        m_textDiffStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_textDiffStatus, ToHString(message));
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_textDiffStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_textDiffStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility reporting must not interrupt the local comparison.
        }
    }

    void MainWindow::RenderLineTools()
    {
        m_lineToolsRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Line Tools", L"行工具" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Transform text line by line: number, prefix, quote, join, split, sort, deduplicate, shuffle, and more. Everything stays on this PC.",
                L"一行行處理文字：加編號、前綴、引號、合併、拆分、排序、去重同打亂等等。全程留喺你部電腦。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeLineToolsPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native line processing", L"全原生行文字處理" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"All fifteen transforms, managed-compatible line parsing, Unicode-aware cleanup, cryptographic shuffle, counts, and explicit clipboard actions run locally in standard C++.",
            L"全部十五項轉換、相容 managed 版嘅分行、Unicode 清理、密碼學打亂、統計同明確剪貼簿動作都用標準 C++ 喺本機執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeLineToolsImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(10);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input", L"輸入" }.Pick(m_language), 15, true);
        input.Children().Append(inputLabel);
        m_lineToolsInputBox = TextBox();
        m_lineToolsInputBox.AcceptsReturn(true);
        m_lineToolsInputBox.Text(ToHString(TextBoxPresentation(m_lineToolsInput)));
        m_lineToolsInputBox.TextWrapping(TextWrapping::Wrap);
        m_lineToolsInputBox.MinHeight(180);
        m_lineToolsInputBox.MaxHeight(320);
        m_lineToolsInputBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_lineToolsInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_lineToolsInputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_lineToolsInputBox, L"NativeLineToolsInput");
        AutomationProperties::SetName(m_lineToolsInputBox, ToHString(winforge::core::LocalizedText{
            L"Line Tools input", L"行工具輸入" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_lineToolsInputBox, inputLabel);
        m_lineToolsInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_lineToolsRendering) return;
            m_lineToolsInput = ToWide(sender.as<TextBox>().Text());
            RefreshLineToolsCount();
        });
        input.Children().Append(m_lineToolsInputBox);

        m_lineToolsCount = CreateText(L"", 12.5);
        m_lineToolsCount.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_lineToolsCount, L"NativeLineToolsCounts");
        input.Children().Append(m_lineToolsCount);

        auto appendField = [this, &input](
            std::wstring_view labelText,
            std::wstring_view accessibleName,
            std::wstring_view automationId,
            TextBox& box,
            std::wstring const& value,
            auto&& changed)
        {
            StackPanel field;
            field.Spacing(3);
            auto label = CreateText(labelText, 12.5, true);
            field.Children().Append(label);
            box = TextBox();
            box.Text(ToHString(value));
            box.IsSpellCheckEnabled(false);
            AutomationProperties::SetAutomationId(box, ToHString(automationId));
            AutomationProperties::SetName(box, ToHString(accessibleName));
            AutomationProperties::SetLabeledBy(box, label);
            box.TextChanged(std::forward<decltype(changed)>(changed));
            field.Children().Append(box);
            input.Children().Append(field);
        };
        appendField(
            winforge::core::LocalizedText{ L"Prefix", L"前綴" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Prefix to add", L"要加嘅前綴" }.Pick(m_language),
            L"NativeLineToolsPrefix", m_lineToolsPrefixBox, m_lineToolsPrefix,
            [this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                if (!m_lineToolsRendering) m_lineToolsPrefix = ToWide(sender.as<TextBox>().Text());
            });
        appendField(
            winforge::core::LocalizedText{ L"Suffix", L"後綴" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Suffix to add", L"要加嘅後綴" }.Pick(m_language),
            L"NativeLineToolsSuffix", m_lineToolsSuffixBox, m_lineToolsSuffix,
            [this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                if (!m_lineToolsRendering) m_lineToolsSuffix = ToWide(sender.as<TextBox>().Text());
            });
        appendField(
            winforge::core::LocalizedText{ L"Delimiter (join / split)", L"分隔符（合併 / 拆分）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Join and split delimiter", L"合併同拆分分隔符" }.Pick(m_language),
            L"NativeLineToolsDelimiter", m_lineToolsDelimiterBox, m_lineToolsDelimiter,
            [this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                if (!m_lineToolsRendering) m_lineToolsDelimiter = ToWide(sender.as<TextBox>().Text());
            });
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border actionsCard = MakeNativeCard();
        StackPanel actionStack;
        actionStack.Spacing(10);
        actionStack.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Transforms", L"轉換" }.Pick(m_language), 15, true));
        Grid actions;
        actions.ColumnSpacing(8);
        actions.RowSpacing(8);
        for (int column{}; column < 2; ++column)
        {
            ColumnDefinition definition;
            definition.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
            actions.ColumnDefinitions().Append(definition);
        }
        int actionIndex{};
        auto appendAction = [this, &actions, &actionIndex](
            std::wstring_view label,
            std::wstring_view automationId,
            LineToolAction action)
        {
            auto const rowIndex = actionIndex / 2;
            auto const columnIndex = actionIndex % 2;
            if (columnIndex == 0) actions.RowDefinitions().Append(RowDefinition());
            auto button = MakeNativeButton(label, automationId);
            button.Click([this, action](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                ApplyLineTool(action);
            });
            Grid::SetRow(button, rowIndex);
            Grid::SetColumn(button, columnIndex);
            actions.Children().Append(button);
            ++actionIndex;
        };
        appendAction(winforge::core::LocalizedText{ L"Number (1.)", L"編號 (1.)" }.Pick(m_language), L"NativeLineToolsNumberDot", LineToolAction::NumberDot);
        appendAction(winforge::core::LocalizedText{ L"Number (1))", L"編號 (1))" }.Pick(m_language), L"NativeLineToolsNumberParen", LineToolAction::NumberParen);
        appendAction(winforge::core::LocalizedText{ L"Remove numbers", L"移除編號" }.Pick(m_language), L"NativeLineToolsRemoveNumbers", LineToolAction::RemoveNumbers);
        appendAction(winforge::core::LocalizedText{ L"Wrap in quotes", L"加引號" }.Pick(m_language), L"NativeLineToolsQuotes", LineToolAction::WrapQuotes);
        appendAction(winforge::core::LocalizedText{ L"Add prefix", L"加前綴" }.Pick(m_language), L"NativeLineToolsAddPrefix", LineToolAction::AddPrefix);
        appendAction(winforge::core::LocalizedText{ L"Add suffix", L"加後綴" }.Pick(m_language), L"NativeLineToolsAddSuffix", LineToolAction::AddSuffix);
        appendAction(winforge::core::LocalizedText{ L"Join lines", L"合併行" }.Pick(m_language), L"NativeLineToolsJoin", LineToolAction::Join);
        appendAction(winforge::core::LocalizedText{ L"Split on delimiter", L"按分隔符拆分" }.Pick(m_language), L"NativeLineToolsSplit", LineToolAction::Split);
        appendAction(winforge::core::LocalizedText{ L"Reverse chars", L"反轉字元" }.Pick(m_language), L"NativeLineToolsReverseChars", LineToolAction::ReverseCharacters);
        appendAction(winforge::core::LocalizedText{ L"Sort A→Z", L"排序 A→Z" }.Pick(m_language), L"NativeLineToolsSort", LineToolAction::Sort);
        appendAction(winforge::core::LocalizedText{ L"Reverse order", L"反轉次序" }.Pick(m_language), L"NativeLineToolsReverseOrder", LineToolAction::ReverseOrder);
        appendAction(winforge::core::LocalizedText{ L"Shuffle", L"打亂" }.Pick(m_language), L"NativeLineToolsShuffle", LineToolAction::Shuffle);
        appendAction(winforge::core::LocalizedText{ L"Deduplicate", L"去重複" }.Pick(m_language), L"NativeLineToolsDedupe", LineToolAction::Deduplicate);
        appendAction(winforge::core::LocalizedText{ L"Remove empty", L"移除空行" }.Pick(m_language), L"NativeLineToolsRemoveEmpty", LineToolAction::RemoveEmpty);
        appendAction(winforge::core::LocalizedText{ L"Trim lines", L"修剪空白" }.Pick(m_language), L"NativeLineToolsTrim", LineToolAction::Trim);
        actionStack.Children().Append(actions);
        actionsCard.Child(actionStack);
        page.Children().Append(actionsCard);

        Border outputCard = MakeNativeCard();
        StackPanel output;
        output.Spacing(10);
        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Output", L"輸出" }.Pick(m_language), 15, true);
        output.Children().Append(outputLabel);
        m_lineToolsOutputBox = TextBox();
        m_lineToolsOutputBox.AcceptsReturn(true);
        m_lineToolsOutputBox.IsReadOnly(true);
        m_lineToolsOutputBox.Text(ToHString(TextBoxPresentation(m_lineToolsOutput)));
        m_lineToolsOutputBox.TextWrapping(TextWrapping::Wrap);
        m_lineToolsOutputBox.MinHeight(180);
        m_lineToolsOutputBox.MaxHeight(320);
        m_lineToolsOutputBox.FontFamily(Media::FontFamily(L"Consolas"));
        ScrollViewer::SetVerticalScrollBarVisibility(m_lineToolsOutputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_lineToolsOutputBox, L"NativeLineToolsOutput");
        AutomationProperties::SetName(m_lineToolsOutputBox, ToHString(winforge::core::LocalizedText{
            L"Line Tools output", L"行工具輸出" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_lineToolsOutputBox, outputLabel);
        output.Children().Append(m_lineToolsOutputBox);

        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy output", L"複製輸出" }.Pick(m_language),
            L"NativeLineToolsCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_lineToolsOutput.empty())
            {
                m_lineToolsStatusEn = L"Nothing to copy yet.";
                m_lineToolsStatusZh = L"暫時冇嘢可以複製。";
                AnnounceLineToolsStatus(winforge::core::LocalizedText{
                    m_lineToolsStatusEn, m_lineToolsStatusZh }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_lineToolsOutput));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                Windows::ApplicationModel::DataTransfer::Clipboard::Flush();
                m_lineToolsStatusEn = L"Output copied to the clipboard.";
                m_lineToolsStatusZh = L"已複製輸出到剪貼簿。";
                AnnounceLineToolsStatus(winforge::core::LocalizedText{
                    m_lineToolsStatusEn, m_lineToolsStatusZh }.Pick(m_language));
            }
            catch (...)
            {
                m_lineToolsStatusEn = L"Could not access the clipboard.";
                m_lineToolsStatusZh = L"用唔到剪貼簿。";
                AnnounceLineToolsStatus(winforge::core::LocalizedText{
                    m_lineToolsStatusEn, m_lineToolsStatusZh }.Pick(m_language), true);
            }
        });
        output.Children().Append(copy);

        m_lineToolsStatus = CreateText(L"", 12.5);
        m_lineToolsStatus.Opacity(0.84);
        m_lineToolsStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_lineToolsStatus, L"NativeLineToolsStatus");
        AutomationProperties::SetLiveSetting(
            m_lineToolsStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        output.Children().Append(m_lineToolsStatus);
        outputCard.Child(output);
        page.Children().Append(outputCard);

        ShowPage(page);
        m_lineToolsRendering = false;
        RefreshLineToolsCount();
        AnnounceLineToolsStatus(winforge::core::LocalizedText{
            m_lineToolsStatusEn, m_lineToolsStatusZh }.Pick(m_language));
    }

    void MainWindow::RefreshLineToolsCount()
    {
        if (!m_lineToolsCount) return;
        auto const count = winforge::core::lineprocessing::Count(m_lineToolsInput);
        auto const value = winforge::core::LocalizedText{
            std::to_wstring(count.lines) + L" lines · " + std::to_wstring(count.words) +
                L" words · " + std::to_wstring(count.characters) + L" chars",
            std::to_wstring(count.lines) + L" 行 · " + std::to_wstring(count.words) +
                L" 個字 · " + std::to_wstring(count.characters) + L" 個字元" }.Pick(m_language);
        m_lineToolsCount.Text(ToHString(value));
        AutomationProperties::SetName(m_lineToolsCount, ToHString(value));
    }

    void MainWindow::ApplyLineTool(LineToolAction action)
    {
        using namespace winforge::core::lineprocessing;
        std::wstring_view nameEn;
        std::wstring_view nameZh;
        switch (action)
        {
        case LineToolAction::NumberDot:
            m_lineToolsOutput = NumberLines(m_lineToolsInput, false);
            nameEn = L"Numbered"; nameZh = L"已加編號"; break;
        case LineToolAction::NumberParen:
            m_lineToolsOutput = NumberLines(m_lineToolsInput, true);
            nameEn = L"Numbered"; nameZh = L"已加編號"; break;
        case LineToolAction::RemoveNumbers:
            m_lineToolsOutput = RemoveLineNumbers(m_lineToolsInput);
            nameEn = L"Removed line numbers"; nameZh = L"已移除編號"; break;
        case LineToolAction::WrapQuotes:
            m_lineToolsOutput = WrapQuotes(m_lineToolsInput);
            nameEn = L"Quoted"; nameZh = L"已加引號"; break;
        case LineToolAction::AddPrefix:
            m_lineToolsOutput = AddPrefix(m_lineToolsInput, m_lineToolsPrefix);
            nameEn = L"Prefixed"; nameZh = L"已加前綴"; break;
        case LineToolAction::AddSuffix:
            m_lineToolsOutput = AddSuffix(m_lineToolsInput, m_lineToolsSuffix);
            nameEn = L"Suffixed"; nameZh = L"已加後綴"; break;
        case LineToolAction::Join:
            m_lineToolsOutput = JoinLines(m_lineToolsInput, m_lineToolsDelimiter);
            nameEn = L"Joined"; nameZh = L"已合併"; break;
        case LineToolAction::Split:
            m_lineToolsOutput = SplitOn(m_lineToolsInput, m_lineToolsDelimiter);
            nameEn = L"Split"; nameZh = L"已拆分"; break;
        case LineToolAction::ReverseCharacters:
            m_lineToolsOutput = ReverseCharacters(m_lineToolsInput);
            nameEn = L"Reversed chars"; nameZh = L"已反轉字元"; break;
        case LineToolAction::Sort:
            m_lineToolsOutput = SortLines(m_lineToolsInput);
            nameEn = L"Sorted"; nameZh = L"已排序"; break;
        case LineToolAction::ReverseOrder:
            m_lineToolsOutput = ReverseOrder(m_lineToolsInput);
            nameEn = L"Reversed order"; nameZh = L"已反轉次序"; break;
        case LineToolAction::Shuffle:
            m_lineToolsOutput = ShuffleLines(m_lineToolsInput);
            nameEn = L"Shuffled"; nameZh = L"已打亂"; break;
        case LineToolAction::Deduplicate:
            m_lineToolsOutput = Deduplicate(m_lineToolsInput);
            nameEn = L"Deduplicated"; nameZh = L"已去重"; break;
        case LineToolAction::RemoveEmpty:
            m_lineToolsOutput = RemoveEmpty(m_lineToolsInput);
            nameEn = L"Removed empty lines"; nameZh = L"已移除空行"; break;
        case LineToolAction::Trim:
            m_lineToolsOutput = TrimLines(m_lineToolsInput);
            nameEn = L"Trimmed"; nameZh = L"已修剪"; break;
        }

        if (m_lineToolsOutputBox)
        {
            m_lineToolsOutputBox.Text(ToHString(TextBoxPresentation(m_lineToolsOutput)));
        }
        auto const lines = Count(m_lineToolsOutput).lines;
        m_lineToolsStatusEn = std::wstring(nameEn) + L" — " + std::to_wstring(lines) + L" line(s) out.";
        m_lineToolsStatusZh = std::wstring(nameZh) + L" — 輸出 " + std::to_wstring(lines) + L" 行。";
        AnnounceLineToolsStatus(winforge::core::LocalizedText{
            m_lineToolsStatusEn, m_lineToolsStatusZh }.Pick(m_language));
    }

    void MainWindow::AnnounceLineToolsStatus(std::wstring_view message, bool warning)
    {
        if (!m_lineToolsStatus) return;
        m_lineToolsStatus.Text(ToHString(message));
        m_lineToolsStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_lineToolsStatus, ToHString(message));
        RaisePoliteLiveRegion(m_lineToolsStatus);
    }

    void MainWindow::RenderTextSort()
    {
        m_textSortRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Line Sort & Dedupe", L"行排序同去重" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Sort, deduplicate, reverse, shuffle, and clean pasted lines. Natural order keeps file2 before file10, and the output updates live.",
                L"排序、去重、反轉、打亂同清理貼上嘅行。自然排序會保持 file2 喺 file10 前面，輸出會即時更新。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeTextSortPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native line sorting", L"全原生行排序" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Managed-compatible comparison, natural numeric order, filtering, deduplication, cryptographic shuffling, and explicit clipboard actions run locally in standard C++.",
            L"相容 managed 版嘅比較、自然數字順序、過濾、去重、密碼學打亂同明確剪貼簿動作都用標準 C++ 喺本機執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeTextSortImplementationStatus");
        page.Children().Append(implementation);

        Border optionsCard = MakeNativeCard();
        StackPanel options;
        options.Spacing(10);
        auto modeLabel = CreateText(
            winforge::core::LocalizedText{ L"Sort mode", L"排序模式" }.Pick(m_language), 13.5, true);
        options.Children().Append(modeLabel);
        m_textSortModeBox = ComboBox();
        for (auto const& label : std::array<winforge::core::LocalizedText, 4>{
            winforge::core::LocalizedText{ L"No sort (keep order)", L"唔排序（保留原順序）" },
            winforge::core::LocalizedText{ L"Sort A → Z", L"排序 A → Z" },
            winforge::core::LocalizedText{ L"Sort Z → A", L"排序 Z → A" },
            winforge::core::LocalizedText{ L"Natural sort (file2 < file10)", L"自然排序（file2 < file10）" },
        })
        {
            m_textSortModeBox.Items().Append(box_value(ToHString(label.Pick(m_language))));
        }
        m_textSortModeBox.SelectedIndex(std::clamp(m_textSortMode, 0, 3));
        // Match the managed picker: keep a useful minimum without stretching
        // to an unbounded width inside the shell's horizontal ScrollViewer.
        m_textSortModeBox.MinWidth(220);
        m_textSortModeBox.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_textSortModeBox, L"NativeTextSortMode");
        AutomationProperties::SetName(m_textSortModeBox, ToHString(winforge::core::LocalizedText{
            L"Line sort mode", L"行排序模式" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textSortModeBox, modeLabel);
        m_textSortModeBox.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_textSortRendering) return;
            m_textSortMode = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 3);
            RefreshTextSort();
        });
        options.Children().Append(m_textSortModeBox);

        auto appendToggle = [this, &options](
            std::wstring_view label,
            std::wstring_view automationId,
            bool value,
            bool MainWindow::* state)
        {
            ToggleSwitch toggle;
            toggle.Header(box_value(ToHString(label)));
            toggle.OnContent(box_value(ToHString(winforge::core::LocalizedText{ L"On", L"開" }.Pick(m_language))));
            toggle.OffContent(box_value(ToHString(winforge::core::LocalizedText{ L"Off", L"關" }.Pick(m_language))));
            toggle.IsOn(value);
            AutomationProperties::SetAutomationId(toggle, ToHString(automationId));
            AutomationProperties::SetName(toggle, ToHString(label));
            toggle.Toggled([this, state](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_textSortRendering) return;
                this->*state = sender.as<ToggleSwitch>().IsOn();
                RefreshTextSort();
            });
            options.Children().Append(toggle);
        };
        appendToggle(winforge::core::LocalizedText{ L"Case-insensitive", L"唔分大細楷" }.Pick(m_language),
            L"NativeTextSortCaseInsensitive", m_textSortCaseInsensitive, &MainWindow::m_textSortCaseInsensitive);
        appendToggle(winforge::core::LocalizedText{ L"Remove duplicates", L"移除重複行" }.Pick(m_language),
            L"NativeTextSortDedupe", m_textSortDeduplicate, &MainWindow::m_textSortDeduplicate);
        appendToggle(winforge::core::LocalizedText{ L"Trim before comparing (dedupe)", L"比較前先修剪（去重）" }.Pick(m_language),
            L"NativeTextSortTrimCompare", m_textSortTrimBeforeCompare, &MainWindow::m_textSortTrimBeforeCompare);
        appendToggle(winforge::core::LocalizedText{ L"Reverse lines", L"反轉行順序" }.Pick(m_language),
            L"NativeTextSortReverse", m_textSortReverse, &MainWindow::m_textSortReverse);
        appendToggle(winforge::core::LocalizedText{ L"Shuffle (random)", L"隨機打亂" }.Pick(m_language),
            L"NativeTextSortShuffle", m_textSortShuffle, &MainWindow::m_textSortShuffle);
        appendToggle(winforge::core::LocalizedText{ L"Remove blank lines", L"移除空白行" }.Pick(m_language),
            L"NativeTextSortRemoveBlank", m_textSortRemoveBlank, &MainWindow::m_textSortRemoveBlank);
        appendToggle(winforge::core::LocalizedText{ L"Trim each line", L"修剪每一行" }.Pick(m_language),
            L"NativeTextSortTrimEach", m_textSortTrimEach, &MainWindow::m_textSortTrimEach);

        Grid actions;
        actions.ColumnSpacing(8);
        actions.RowSpacing(8);
        for (int column{}; column < 2; ++column)
        {
            ColumnDefinition definition;
            definition.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
            actions.ColumnDefinitions().Append(definition);
        }
        for (int row{}; row < 2; ++row) actions.RowDefinitions().Append(RowDefinition());
        auto appendAction = [&actions](Button const& button, int index)
        {
            Grid::SetRow(button, index / 2);
            Grid::SetColumn(button, index % 2);
            actions.Children().Append(button);
        };
        auto apply = MakeNativeButton(
            winforge::core::LocalizedText{ L"Apply", L"套用" }.Pick(m_language), L"NativeTextSortApply");
        apply.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { RefreshTextSort(); });
        appendAction(apply, 0);
        auto reshuffle = MakeNativeButton(
            winforge::core::LocalizedText{ L"Re-shuffle", L"再打亂" }.Pick(m_language), L"NativeTextSortReshuffle");
        reshuffle.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&) { RefreshTextSort(); });
        appendAction(reshuffle, 1);
        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy output", L"複製結果" }.Pick(m_language), L"NativeTextSortCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_textSortOutput));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceTextSortStatus(winforge::core::LocalizedText{
                    L"Copied to clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceTextSortStatus(winforge::core::LocalizedText{
                    L"Copy failed.", L"複製失敗。" }.Pick(m_language), true);
            }
        });
        appendAction(copy, 2);
        auto clear = MakeNativeButton(
            winforge::core::LocalizedText{ L"Clear", L"清除" }.Pick(m_language), L"NativeTextSortClear");
        clear.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            m_textSortInput.clear();
            if (m_textSortInputBox)
            {
                m_textSortRendering = true;
                m_textSortInputBox.Text(L"");
                m_textSortRendering = false;
            }
            RefreshTextSort();
        });
        appendAction(clear, 3);
        options.Children().Append(actions);

        m_textSortStats = CreateText(L"", 12.5);
        m_textSortStats.Opacity(0.84);
        m_textSortStats.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_textSortStats, L"NativeTextSortStats");
        AutomationProperties::SetLiveSetting(
            m_textSortStats,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        options.Children().Append(m_textSortStats);
        optionsCard.Child(options);
        page.Children().Append(optionsCard);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(8);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input", L"輸入" }.Pick(m_language), 15, true);
        input.Children().Append(inputLabel);
        m_textSortInputBox = TextBox();
        m_textSortInputBox.AcceptsReturn(true);
        m_textSortInputBox.Text(ToHString(TextBoxPresentation(m_textSortInput)));
        m_textSortInputBox.TextWrapping(TextWrapping::NoWrap);
        m_textSortInputBox.MinHeight(220);
        m_textSortInputBox.MaxHeight(360);
        m_textSortInputBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_textSortInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textSortInputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollBarVisibility(m_textSortInputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollMode(m_textSortInputBox, ScrollMode::Enabled);
        AutomationProperties::SetAutomationId(m_textSortInputBox, L"NativeTextSortInput");
        AutomationProperties::SetName(m_textSortInputBox, ToHString(winforge::core::LocalizedText{
            L"Text Sort input", L"文字排序輸入" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textSortInputBox, inputLabel);
        m_textSortInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_textSortRendering) return;
            m_textSortInput = ToWide(sender.as<TextBox>().Text());
            RefreshTextSort();
        });
        input.Children().Append(m_textSortInputBox);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border outputCard = MakeNativeCard();
        StackPanel output;
        output.Spacing(8);
        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Output", L"輸出" }.Pick(m_language), 15, true);
        output.Children().Append(outputLabel);
        m_textSortOutputBox = TextBox();
        m_textSortOutputBox.AcceptsReturn(true);
        m_textSortOutputBox.IsReadOnly(true);
        m_textSortOutputBox.Text(ToHString(TextBoxPresentation(m_textSortOutput)));
        m_textSortOutputBox.TextWrapping(TextWrapping::NoWrap);
        m_textSortOutputBox.MinHeight(220);
        m_textSortOutputBox.MaxHeight(360);
        m_textSortOutputBox.FontFamily(Media::FontFamily(L"Consolas"));
        ScrollViewer::SetVerticalScrollBarVisibility(m_textSortOutputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollBarVisibility(m_textSortOutputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollMode(m_textSortOutputBox, ScrollMode::Enabled);
        AutomationProperties::SetAutomationId(m_textSortOutputBox, L"NativeTextSortOutput");
        AutomationProperties::SetName(m_textSortOutputBox, ToHString(winforge::core::LocalizedText{
            L"Text Sort output", L"文字排序輸出" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textSortOutputBox, outputLabel);
        output.Children().Append(m_textSortOutputBox);
        outputCard.Child(output);
        page.Children().Append(outputCard);

        ShowPage(page);
        m_textSortRendering = false;
        RefreshTextSort();
    }

    void MainWindow::RefreshTextSort()
    {
        using namespace winforge::core::lineprocessing;
        if (!m_textSortOutputBox || !m_textSortStats) return;
        TextSortOptions options;
        switch (m_textSortMode)
        {
        case 0: options.mode = SortMode::None; break;
        case 2: options.mode = SortMode::Descending; break;
        case 3: options.mode = SortMode::Natural; break;
        default: options.mode = SortMode::Ascending; break;
        }
        options.caseInsensitive = m_textSortCaseInsensitive;
        options.removeDuplicates = m_textSortDeduplicate;
        options.trimBeforeCompare = m_textSortTrimBeforeCompare;
        options.reverse = m_textSortReverse;
        options.shuffle = m_textSortShuffle;
        options.removeBlank = m_textSortRemoveBlank;
        options.trimEach = m_textSortTrimEach;
        auto const result = TransformTextSort(m_textSortInput, options);
        m_textSortOutput = result.text;
        m_textSortOutputBox.Text(ToHString(TextBoxPresentation(m_textSortOutput)));
        auto const stats = winforge::core::LocalizedText{
            L"Lines in: " + std::to_wstring(result.linesIn) + L"   ·   Lines out: " +
                std::to_wstring(result.linesOut) + L"   ·   Duplicates removed: " +
                std::to_wstring(result.duplicatesRemoved),
            L"輸入行數：" + std::to_wstring(result.linesIn) + L"   ·   輸出行數：" +
                std::to_wstring(result.linesOut) + L"   ·   移除重複：" +
                std::to_wstring(result.duplicatesRemoved) }.Pick(m_language);
        AnnounceTextSortStatus(stats);
    }

    void MainWindow::AnnounceTextSortStatus(std::wstring_view message, bool warning)
    {
        if (!m_textSortStats) return;
        m_textSortStats.Text(ToHString(message));
        m_textSortStats.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_textSortStats, ToHString(message));
        RaisePoliteLiveRegion(m_textSortStats);
    }

    void MainWindow::RenderTextWrap()
    {
        m_textWrapRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Text Wrap", L"文字換行" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Wrap, unwrap, or reflow plain text to a fixed width for code comments, commit messages, email, and README files. Blank lines keep paragraphs apart.",
                L"將純文字換行、拉直或重排到固定闊度，適合程式註解、commit 訊息、電郵同 README。空白行會分開段落。" }.Pick(m_language));
        page.MaxWidth(820);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeTextWrapPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native text wrapping", L"全原生文字換行" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Paragraph parsing, managed-compatible whitespace handling, hard wrap, unwrap, reflow, prefix, hanging indent, measurements, and explicit copy run locally in standard C++.",
            L"段落剖析、相容 managed 版嘅空白處理、硬換行、拉直、重排、前綴、懸掛縮排、測量同明確複製都用標準 C++ 喺本機執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeTextWrapImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(8);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input", L"輸入" }.Pick(m_language), 15, true);
        input.Children().Append(inputLabel);
        m_textWrapInputBox = TextBox();
        m_textWrapInputBox.AcceptsReturn(true);
        m_textWrapInputBox.Text(ToHString(TextBoxPresentation(m_textWrapInput)));
        m_textWrapInputBox.TextWrapping(TextWrapping::Wrap);
        m_textWrapInputBox.MinHeight(180);
        m_textWrapInputBox.MaxHeight(320);
        m_textWrapInputBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_textWrapInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textWrapInputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_textWrapInputBox, L"NativeTextWrapInput");
        AutomationProperties::SetName(m_textWrapInputBox, ToHString(winforge::core::LocalizedText{
            L"Text Wrap input", L"文字換行輸入" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textWrapInputBox, inputLabel);
        m_textWrapInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_textWrapRendering) return;
            m_textWrapInput = ToWide(sender.as<TextBox>().Text());
            RefreshTextWrapReadout();
        });
        input.Children().Append(m_textWrapInputBox);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border optionsCard = MakeNativeCard();
        StackPanel options;
        options.Spacing(10);
        auto widthLabel = CreateText(
            winforge::core::LocalizedText{ L"Width (columns)", L"闊度（字元）" }.Pick(m_language), 12.5, true);
        options.Children().Append(widthLabel);
        m_textWrapWidthBox = NumberBox();
        m_textWrapWidthBox.Minimum(1);
        m_textWrapWidthBox.Maximum(2000);
        m_textWrapWidthBox.Value(m_textWrapWidth);
        m_textWrapWidthBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Compact);
        m_textWrapWidthBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        AutomationProperties::SetAutomationId(m_textWrapWidthBox, L"NativeTextWrapWidth");
        AutomationProperties::SetName(m_textWrapWidthBox, ToHString(winforge::core::LocalizedText{
            L"Text wrapping width in columns", L"文字換行寬度（字元）" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textWrapWidthBox, widthLabel);
        m_textWrapWidthBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (m_textWrapRendering) return;
            m_textWrapWidth = sender.Value();
            RefreshTextWrapReadout();
        });
        options.Children().Append(m_textWrapWidthBox);

        m_textWrapReadout = CreateText(L"", 12.5);
        m_textWrapReadout.Opacity(0.82);
        m_textWrapReadout.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_textWrapReadout, L"NativeTextWrapReadout");
        options.Children().Append(m_textWrapReadout);

        m_textWrapBreakLongBox = CheckBox();
        m_textWrapBreakLongBox.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Break words longer than the width", L"斬開超過闊度嘅長字" }.Pick(m_language))));
        m_textWrapBreakLongBox.IsChecked(
            box_value(m_textWrapBreakLongWords).as<Windows::Foundation::IReference<bool>>());
        AutomationProperties::SetAutomationId(m_textWrapBreakLongBox, L"NativeTextWrapBreakLong");
        AutomationProperties::SetName(m_textWrapBreakLongBox, ToHString(winforge::core::LocalizedText{
            L"Break words longer than the wrapping width", L"斬開超過換行寬度嘅長字" }.Pick(m_language)));
        m_textWrapBreakLongBox.Click([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_textWrapRendering) return;
            m_textWrapBreakLongWords = unbox_value_or<bool>(sender.as<CheckBox>().IsChecked(), false);
        });
        options.Children().Append(m_textWrapBreakLongBox);

        auto prefixLabel = CreateText(
            winforge::core::LocalizedText{ L"Prefix", L"前綴" }.Pick(m_language), 12.5, true);
        options.Children().Append(prefixLabel);
        m_textWrapPrefixBox = TextBox();
        m_textWrapPrefixBox.Text(ToHString(m_textWrapPrefix));
        m_textWrapPrefixBox.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_textWrapPrefixBox, L"NativeTextWrapPrefix");
        AutomationProperties::SetName(m_textWrapPrefixBox, ToHString(winforge::core::LocalizedText{
            L"Prefix for every line", L"每行前綴" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textWrapPrefixBox, prefixLabel);
        m_textWrapPrefixBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (!m_textWrapRendering) m_textWrapPrefix = ToWide(sender.as<TextBox>().Text());
        });
        options.Children().Append(m_textWrapPrefixBox);

        auto indentLabel = CreateText(
            winforge::core::LocalizedText{ L"Indent spaces", L"縮排空格" }.Pick(m_language), 12.5, true);
        options.Children().Append(indentLabel);
        m_textWrapIndentBox = NumberBox();
        m_textWrapIndentBox.Minimum(0);
        m_textWrapIndentBox.Maximum(200);
        m_textWrapIndentBox.Value(m_textWrapIndent);
        m_textWrapIndentBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Compact);
        m_textWrapIndentBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        AutomationProperties::SetAutomationId(m_textWrapIndentBox, L"NativeTextWrapIndent");
        AutomationProperties::SetName(m_textWrapIndentBox, ToHString(winforge::core::LocalizedText{
            L"Hanging indent spaces", L"懸掛縮排空格" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textWrapIndentBox, indentLabel);
        m_textWrapIndentBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (!m_textWrapRendering) m_textWrapIndent = sender.Value();
        });
        options.Children().Append(m_textWrapIndentBox);

        Grid actions;
        actions.ColumnSpacing(8);
        actions.RowSpacing(8);
        for (int column{}; column < 2; ++column)
        {
            ColumnDefinition definition;
            definition.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
            actions.ColumnDefinitions().Append(definition);
        }
        int actionIndex{};
        auto appendAction = [this, &actions, &actionIndex](
            std::wstring_view label,
            std::wstring_view automationId,
            TextWrapAction action)
        {
            auto const rowIndex = actionIndex / 2;
            auto const columnIndex = actionIndex % 2;
            if (columnIndex == 0) actions.RowDefinitions().Append(RowDefinition());
            auto button = MakeNativeButton(label, automationId);
            button.Click([this, action](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                ApplyTextWrap(action);
            });
            Grid::SetRow(button, rowIndex);
            Grid::SetColumn(button, columnIndex);
            actions.Children().Append(button);
            ++actionIndex;
        };
        appendAction(winforge::core::LocalizedText{ L"Hard-wrap", L"硬換行" }.Pick(m_language),
            L"NativeTextWrapHardWrap", TextWrapAction::HardWrap);
        appendAction(winforge::core::LocalizedText{ L"Unwrap", L"拉直" }.Pick(m_language),
            L"NativeTextWrapUnwrap", TextWrapAction::Unwrap);
        appendAction(winforge::core::LocalizedText{ L"Reflow", L"重排" }.Pick(m_language),
            L"NativeTextWrapReflow", TextWrapAction::Reflow);
        appendAction(winforge::core::LocalizedText{ L"Add prefix", L"加前綴" }.Pick(m_language),
            L"NativeTextWrapAddPrefix", TextWrapAction::AddPrefix);
        appendAction(winforge::core::LocalizedText{ L"Hanging indent", L"懸掛縮排" }.Pick(m_language),
            L"NativeTextWrapHangingIndent", TextWrapAction::HangingIndent);
        options.Children().Append(actions);
        optionsCard.Child(options);
        page.Children().Append(optionsCard);

        Border outputCard = MakeNativeCard();
        StackPanel output;
        output.Spacing(8);
        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Output", L"輸出" }.Pick(m_language), 15, true);
        output.Children().Append(outputLabel);
        m_textWrapOutputBox = TextBox();
        m_textWrapOutputBox.AcceptsReturn(true);
        m_textWrapOutputBox.IsReadOnly(true);
        m_textWrapOutputBox.Text(ToHString(TextBoxPresentation(m_textWrapOutput)));
        m_textWrapOutputBox.TextWrapping(TextWrapping::NoWrap);
        m_textWrapOutputBox.MinHeight(180);
        m_textWrapOutputBox.MaxHeight(320);
        m_textWrapOutputBox.FontFamily(Media::FontFamily(L"Consolas"));
        ScrollViewer::SetVerticalScrollBarVisibility(m_textWrapOutputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollBarVisibility(m_textWrapOutputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollMode(m_textWrapOutputBox, ScrollMode::Enabled);
        AutomationProperties::SetAutomationId(m_textWrapOutputBox, L"NativeTextWrapOutput");
        AutomationProperties::SetName(m_textWrapOutputBox, ToHString(winforge::core::LocalizedText{
            L"Text Wrap output", L"文字換行輸出" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textWrapOutputBox, outputLabel);
        output.Children().Append(m_textWrapOutputBox);

        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy output", L"複製輸出" }.Pick(m_language),
            L"NativeTextWrapCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_textWrapOutput.empty())
            {
                m_textWrapStatusEn = L"Nothing to copy.";
                m_textWrapStatusZh = L"冇嘢可以複製。";
                AnnounceTextWrapStatus(winforge::core::LocalizedText{
                    m_textWrapStatusEn, m_textWrapStatusZh }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_textWrapOutput));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                m_textWrapStatusEn = L"Copied to clipboard.";
                m_textWrapStatusZh = L"已複製到剪貼簿。";
                AnnounceTextWrapStatus(winforge::core::LocalizedText{
                    m_textWrapStatusEn, m_textWrapStatusZh }.Pick(m_language));
            }
            catch (...)
            {
                m_textWrapStatusEn = L"Copy failed.";
                m_textWrapStatusZh = L"複製失敗。";
                AnnounceTextWrapStatus(winforge::core::LocalizedText{
                    m_textWrapStatusEn, m_textWrapStatusZh }.Pick(m_language), true);
            }
        });
        output.Children().Append(copy);

        m_textWrapStatus = CreateText(L"", 12.5);
        m_textWrapStatus.Opacity(0.84);
        m_textWrapStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_textWrapStatus, L"NativeTextWrapStatus");
        AutomationProperties::SetLiveSetting(
            m_textWrapStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        output.Children().Append(m_textWrapStatus);
        outputCard.Child(output);
        page.Children().Append(outputCard);

        ShowPage(page);
        m_textWrapRendering = false;
        RefreshTextWrapReadout();
        AnnounceTextWrapStatus(winforge::core::LocalizedText{
            m_textWrapStatusEn, m_textWrapStatusZh }.Pick(m_language));
    }

    void MainWindow::RefreshTextWrapReadout()
    {
        if (!m_textWrapReadout) return;
        auto const width = std::isfinite(m_textWrapWidth) && m_textWrapWidth >= 1.0
            ? std::clamp(static_cast<int>(m_textWrapWidth), 1, 2000)
            : 72;
        auto const& source = m_textWrapOutput.empty() ? m_textWrapInput : m_textWrapOutput;
        auto const measure = winforge::core::lineprocessing::MeasureText(source);
        auto const value = winforge::core::LocalizedText{
            L"Target " + std::to_wstring(width) + L" cols · longest line " +
                std::to_wstring(measure.longestLine) + L" · " + std::to_wstring(measure.lines) +
                L" lines · " + std::to_wstring(measure.characters) + L" chars",
            L"目標 " + std::to_wstring(width) + L" 字元 · 最長一行 " +
                std::to_wstring(measure.longestLine) + L" · " + std::to_wstring(measure.lines) +
                L" 行 · " + std::to_wstring(measure.characters) + L" 個字元" }.Pick(m_language);
        m_textWrapReadout.Text(ToHString(value));
        AutomationProperties::SetName(m_textWrapReadout, ToHString(value));
    }

    void MainWindow::ApplyTextWrap(TextWrapAction action)
    {
        using namespace winforge::core::lineprocessing;
        auto const width = std::isfinite(m_textWrapWidth) && m_textWrapWidth >= 1.0
            ? std::clamp(static_cast<int>(m_textWrapWidth), 1, 2000)
            : 72;
        auto const indent = std::isfinite(m_textWrapIndent) && m_textWrapIndent >= 0.0
            ? std::clamp(static_cast<int>(m_textWrapIndent), 0, 2000)
            : 0;
        switch (action)
        {
        case TextWrapAction::HardWrap:
            m_textWrapOutput = HardWrap(m_textWrapInput, width, m_textWrapBreakLongWords);
            m_textWrapStatusEn = L"Hard-wrapped.";
            m_textWrapStatusZh = L"已硬換行。";
            break;
        case TextWrapAction::Unwrap:
            m_textWrapOutput = Unwrap(m_textWrapInput);
            m_textWrapStatusEn = L"Unwrapped.";
            m_textWrapStatusZh = L"已拉直。";
            break;
        case TextWrapAction::Reflow:
            m_textWrapOutput = Reflow(m_textWrapInput, width, m_textWrapBreakLongWords);
            m_textWrapStatusEn = L"Reflowed.";
            m_textWrapStatusZh = L"已重排。";
            break;
        case TextWrapAction::AddPrefix:
        {
            auto const& basis = m_textWrapOutput.empty() ? m_textWrapInput : m_textWrapOutput;
            m_textWrapOutput = AddPrefixEveryLine(basis, m_textWrapPrefix);
            m_textWrapStatusEn = L"Prefix added to each line.";
            m_textWrapStatusZh = L"已為每行加前綴。";
            break;
        }
        case TextWrapAction::HangingIndent:
        {
            auto const& basis = m_textWrapOutput.empty() ? m_textWrapInput : m_textWrapOutput;
            m_textWrapOutput = HangingIndent(basis, indent);
            m_textWrapStatusEn = L"Hanging indent applied.";
            m_textWrapStatusZh = L"已套用懸掛縮排。";
            break;
        }
        }

        if (m_textWrapOutputBox)
        {
            m_textWrapOutputBox.Text(ToHString(TextBoxPresentation(m_textWrapOutput)));
        }
        RefreshTextWrapReadout();
        AnnounceTextWrapStatus(winforge::core::LocalizedText{
            m_textWrapStatusEn, m_textWrapStatusZh }.Pick(m_language));
    }

    void MainWindow::AnnounceTextWrapStatus(std::wstring_view message, bool warning)
    {
        if (!m_textWrapStatus) return;
        m_textWrapStatus.Text(ToHString(message));
        m_textWrapStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_textWrapStatus, ToHString(message));
        RaisePoliteLiveRegion(m_textWrapStatus);
    }

    void MainWindow::RenderTextStats()
    {
        m_textStatsRendering = true;

        auto page = CreatePage(
            L"Text Statistics · 文字統計",
            winforge::core::LocalizedText{
                L"Paste or type any text to get live counts, reading and speaking time, and readability scores — no data leaves your PC.",
                L"貼上或者打任何文字，即時睇字數、閱讀同朗讀時間，仲有可讀性評分 — 全部喺你部機度計，唔會外傳。" }.Pick(m_language));
        page.MaxWidth(820);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeTextStatsPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native text statistics", L"全原生文字統計" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"UTF-16 counts, managed-compatible tokenization, readability, timings, and top-word ranking run locally in standard C++.",
            L"UTF-16 統計、相容 managed 版嘅斷詞、可讀性、時間同常用字排名全部用標準 C++ 喺本機執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeTextStatsImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(10);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Your text", L"你嘅文字" }.Pick(m_language), 15, true);
        input.Children().Append(inputLabel);
        m_textStatsInputBox = TextBox();
        m_textStatsInputBox.AcceptsReturn(true);
        m_textStatsInputBox.Text(ToHString(TextBoxPresentation(m_textStatsInput)));
        m_textStatsInputBox.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"Type or paste text here…", L"喺呢度打字或者貼上文字…" }.Pick(m_language)));
        m_textStatsInputBox.TextWrapping(TextWrapping::Wrap);
        m_textStatsInputBox.Height(200);
        m_textStatsInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textStatsInputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_textStatsInputBox, L"NativeTextStatsInput");
        AutomationProperties::SetName(m_textStatsInputBox, ToHString(winforge::core::LocalizedText{
            L"Text Statistics input", L"文字統計輸入" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_textStatsInputBox, inputLabel);
        m_textStatsInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_textStatsRendering) return;
            m_textStatsInput = ToWide(sender.as<TextBox>().Text());
            RefreshTextStats();
        });
        input.Children().Append(m_textStatsInputBox);

        m_textStatsStopWordsBox = CheckBox();
        m_textStatsStopWordsBox.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Ignore common stop-words in frequency", L"字頻忽略常見虛詞" }.Pick(m_language))));
        m_textStatsStopWordsBox.IsChecked(
            box_value(m_textStatsIgnoreStopWords).as<Windows::Foundation::IReference<bool>>());
        AutomationProperties::SetAutomationId(m_textStatsStopWordsBox, L"NativeTextStatsIgnoreStopWords");
        AutomationProperties::SetName(m_textStatsStopWordsBox, ToHString(winforge::core::LocalizedText{
            L"Ignore common stop-words in frequency", L"字頻忽略常見虛詞" }.Pick(m_language)));
        m_textStatsStopWordsBox.Click([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_textStatsRendering) return;
            m_textStatsIgnoreStopWords = unbox_value_or<bool>(sender.as<CheckBox>().IsChecked(), false);
            RefreshTextStats();
        });
        input.Children().Append(m_textStatsStopWordsBox);

        m_textStatsStatus = CreateText(L"", 12);
        m_textStatsStatus.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_textStatsStatus, L"NativeTextStatsStatus");
        input.Children().Append(m_textStatsStatus);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border statsCard = MakeNativeCard();
        StackPanel stats;
        stats.Spacing(10);
        stats.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Statistics", L"統計" }.Pick(m_language), 15, true));
        Grid metrics;
        metrics.ColumnSpacing(18);
        metrics.RowSpacing(6);
        for (int column{}; column < 2; ++column)
        {
            ColumnDefinition definition;
            definition.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
            metrics.ColumnDefinitions().Append(definition);
        }
        for (int row{}; row < 6; ++row) metrics.RowDefinitions().Append(RowDefinition());
        constexpr std::array<std::wstring_view, 12> metricIds{
            L"NativeTextStatsCharacters",
            L"NativeTextStatsCharactersNoSpaces",
            L"NativeTextStatsWords",
            L"NativeTextStatsUniqueWords",
            L"NativeTextStatsSentences",
            L"NativeTextStatsParagraphs",
            L"NativeTextStatsAverageWordLength",
            L"NativeTextStatsAverageSentenceLength",
            L"NativeTextStatsReadingTime",
            L"NativeTextStatsSpeakingTime",
            L"NativeTextStatsReadingEase",
            L"NativeTextStatsGrade",
        };
        for (std::size_t index{}; index < m_textStatsMetricRows.size(); ++index)
        {
            auto row = CreateText(L"", 14);
            row.TextWrapping(TextWrapping::Wrap);
            AutomationProperties::SetAutomationId(row, ToHString(metricIds[index]));
            Grid::SetRow(row, static_cast<int32_t>(index / 2));
            Grid::SetColumn(row, static_cast<int32_t>(index % 2));
            metrics.Children().Append(row);
            m_textStatsMetricRows[index] = row;
        }
        stats.Children().Append(metrics);
        m_textStatsEaseHint = CreateText(L"", 12);
        m_textStatsEaseHint.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_textStatsEaseHint, L"NativeTextStatsEaseHint");
        stats.Children().Append(m_textStatsEaseHint);
        statsCard.Child(stats);
        page.Children().Append(statsCard);

        Border frequencyCard = MakeNativeCard();
        StackPanel frequency;
        frequency.Spacing(8);
        frequency.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Top words", L"最常用字" }.Pick(m_language), 15, true));
        m_textStatsFrequencyList = ListView();
        m_textStatsFrequencyList.SelectionMode(ListViewSelectionMode::None);
        m_textStatsFrequencyList.MaxHeight(320);
        m_textStatsFrequencyList.HorizontalContentAlignment(HorizontalAlignment::Stretch);
        ScrollViewer::SetVerticalScrollBarVisibility(m_textStatsFrequencyList, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_textStatsFrequencyList, L"NativeTextStatsFrequencyList");
        AutomationProperties::SetName(m_textStatsFrequencyList, ToHString(winforge::core::LocalizedText{
            L"Top words", L"最常用字" }.Pick(m_language)));
        frequency.Children().Append(m_textStatsFrequencyList);
        m_textStatsFrequencyEmpty = CreateText(L"", 12);
        m_textStatsFrequencyEmpty.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_textStatsFrequencyEmpty, L"NativeTextStatsFrequencyEmpty");
        frequency.Children().Append(m_textStatsFrequencyEmpty);
        frequencyCard.Child(frequency);
        page.Children().Append(frequencyCard);

        ShowPage(page);
        m_textStatsRendering = false;
        RefreshTextStats();
    }

    void MainWindow::RefreshTextStats()
    {
        if (!m_textStatsStatus || !m_textStatsFrequencyList) return;
        using namespace winforge::core::textanalysis;
        auto const value = AnalyzeTextStats(m_textStatsInput, m_textStatsIgnoreStopWords, 10);
        auto const pick = [this](std::wstring en, std::wstring zh)
        {
            return winforge::core::LocalizedText{ std::move(en), std::move(zh) }.Pick(m_language);
        };
        auto const count = [](int number) { return FormatCurrentCount(number); };
        std::array<std::wstring, 12> rows{
            pick(L"Characters: " + count(value.characters), L"字元：" + count(value.characters)),
            pick(L"Characters (no spaces): " + count(value.charactersNoSpaces), L"字元（不含空格）：" + count(value.charactersNoSpaces)),
            pick(L"Words: " + count(value.words), L"字數：" + count(value.words)),
            pick(L"Unique words: " + count(value.uniqueWords), L"不重複字：" + count(value.uniqueWords)),
            pick(L"Sentences: " + count(value.sentences), L"句數：" + count(value.sentences)),
            pick(L"Paragraphs: " + count(value.paragraphs), L"段落：" + count(value.paragraphs)),
            pick(L"Avg word length: " + FormatCurrentOneDecimal(value.avgWordLength), L"平均字長：" + FormatCurrentOneDecimal(value.avgWordLength)),
            pick(L"Avg sentence length: " + FormatCurrentOneDecimal(value.avgSentenceLength) + L" words", L"平均句長：" + FormatCurrentOneDecimal(value.avgSentenceLength) + L" 字"),
            pick(L"Reading time (~200 wpm): " + FormatDuration(value.readingMinutes), L"閱讀時間（約 200 字/分）：" + FormatDuration(value.readingMinutes)),
            pick(L"Speaking time (~130 wpm): " + FormatDuration(value.speakingMinutes), L"朗讀時間（約 130 字/分）：" + FormatDuration(value.speakingMinutes)),
            pick(L"Flesch Reading Ease: " + FormatCurrentOneDecimal(value.fleschReadingEase), L"Flesch 易讀度：" + FormatCurrentOneDecimal(value.fleschReadingEase)),
            pick(L"Flesch–Kincaid grade: " + FormatCurrentOneDecimal(value.fleschKincaidGrade), L"Flesch–Kincaid 年級：" + FormatCurrentOneDecimal(value.fleschKincaidGrade)),
        };
        for (std::size_t index{}; index < rows.size(); ++index)
        {
            if (!m_textStatsMetricRows[index]) continue;
            m_textStatsMetricRows[index].Text(ToHString(rows[index]));
            AutomationProperties::SetName(m_textStatsMetricRows[index], ToHString(rows[index]));
        }

        std::wstring hint;
        if (value.words > 0)
        {
            if (value.fleschReadingEase >= 90) hint = pick(L"Very easy — 5th grade.", L"非常易讀 — 約小五程度。");
            else if (value.fleschReadingEase >= 70) hint = pick(L"Easy — 6th–7th grade.", L"易讀 — 約小六至中一程度。");
            else if (value.fleschReadingEase >= 60) hint = pick(L"Plain English — 8th–9th grade.", L"淺白 — 約中二至中三程度。");
            else if (value.fleschReadingEase >= 50) hint = pick(L"Fairly hard — 10th–12th grade.", L"略難 — 約高中程度。");
            else if (value.fleschReadingEase >= 30) hint = pick(L"Difficult — college level.", L"困難 — 約大學程度。");
            else hint = pick(L"Very difficult — graduate level.", L"非常困難 — 約研究生程度。");
        }
        m_textStatsEaseHint.Text(ToHString(hint));
        AutomationProperties::SetName(m_textStatsEaseHint, ToHString(hint));

        m_textStatsFrequencyList.Items().Clear();
        for (std::size_t index{}; index < value.topWords.size(); ++index)
        {
            auto const& entry = value.topWords[index];
            Grid row;
            ColumnDefinition termColumn;
            termColumn.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
            row.ColumnDefinitions().Append(termColumn);
            row.ColumnDefinitions().Append(ColumnDefinition());
            auto term = CreateText(entry.word, 14);
            term.FontWeight(Microsoft::UI::Text::FontWeights::Normal());
            Grid::SetColumn(term, 0);
            row.Children().Append(term);
            auto total = CreateText(std::to_wstring(entry.count), 14, true);
            total.Opacity(0.78);
            Grid::SetColumn(total, 1);
            row.Children().Append(total);
            ListViewItem item;
            item.Content(row);
            item.Padding(Thickness{ 8, 4, 8, 4 });
            item.HorizontalContentAlignment(HorizontalAlignment::Stretch);
            auto const name = entry.word + L" · " + std::to_wstring(entry.count);
            AutomationProperties::SetAutomationId(
                item,
                ToHString(L"NativeTextStatsTopWord" + std::to_wstring(index)));
            AutomationProperties::SetName(item, ToHString(name));
            m_textStatsFrequencyList.Items().Append(item);
        }
        auto const empty = value.topWords.empty()
            ? pick(L"No words yet — start typing above.", L"仲未有字 — 喺上面開始打字。")
            : L"";
        m_textStatsFrequencyEmpty.Text(ToHString(empty));
        AutomationProperties::SetName(m_textStatsFrequencyEmpty, ToHString(empty));

        auto const status = value.words > 0
            ? pick(L"Updated live.", L"已即時更新。")
            : pick(L"Ready.", L"準備就緒。");
        m_textStatsStatus.Text(ToHString(status));
        AutomationProperties::SetName(m_textStatsStatus, ToHString(status));
    }

    void MainWindow::RenderWordFrequency()
    {
        m_wordFreqRendering = true;
        auto const title = m_language == winforge::core::LanguageMode::Cantonese
            ? std::wstring{ L"詞頻統計 · Word Frequency" }
            : std::wstring{ L"Word Frequency · 詞頻統計" };
        auto page = CreatePage(
            title,
            winforge::core::LocalizedText{
                L"Paste any text and see which words, word-pairs or characters appear most. Ranked with counts, bars and percentages — copy the table as CSV.",
                L"貼入任何文字，睇下邊啲詞、詞組或者字元出現得最多。附上次數、長條同百分比排名 — 可以複製成 CSV。" }.Pick(m_language));
        page.MaxWidth(820);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeWordFreqPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native frequency analysis", L"全原生詞頻分析" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Word, bigram, and Unicode-scalar character tokenization, ranking, percentages, and CSV generation run locally in standard C++. Clipboard access only follows the explicit Copy action.",
            L"詞語、詞組同 Unicode scalar 字元斷詞、排名、百分比同 CSV 生成全部用標準 C++ 喺本機執行；只有明確撳 Copy 先會存取剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeWordFreqImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(12);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Text to analyze", L"要分析嘅文字" }.Pick(m_language), 15, true);
        input.Children().Append(inputLabel);
        m_wordFreqInputBox = TextBox();
        m_wordFreqInputBox.AcceptsReturn(true);
        m_wordFreqInputBox.Text(ToHString(TextBoxPresentation(m_wordFreqInput)));
        m_wordFreqInputBox.TextWrapping(TextWrapping::Wrap);
        m_wordFreqInputBox.MinHeight(140);
        m_wordFreqInputBox.MaxHeight(240);
        m_wordFreqInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_wordFreqInputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_wordFreqInputBox, L"NativeWordFreqInput");
        AutomationProperties::SetName(m_wordFreqInputBox, ToHString(winforge::core::LocalizedText{
            L"Word Frequency input", L"詞頻統計輸入" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_wordFreqInputBox, inputLabel);
        m_wordFreqInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_wordFreqRendering) return;
            m_wordFreqInput = ToWide(sender.as<TextBox>().Text());
            RefreshWordFrequency();
        });
        input.Children().Append(m_wordFreqInputBox);

        Grid modeRow;
        modeRow.ColumnSpacing(10);
        ColumnDefinition modeLabelColumn;
        modeLabelColumn.Width(GridLengthHelper::Auto());
        modeRow.ColumnDefinitions().Append(modeLabelColumn);
        modeRow.ColumnDefinitions().Append(ColumnDefinition());
        auto modeLabel = CreateText(
            winforge::core::LocalizedText{ L"Count by", L"統計對象" }.Pick(m_language), 14);
        modeLabel.VerticalAlignment(VerticalAlignment::Center);
        Grid::SetColumn(modeLabel, 0);
        modeRow.Children().Append(modeLabel);
        m_wordFreqModeBox = ComboBox();
        m_wordFreqModeBox.MinWidth(220);
        m_wordFreqModeBox.HorizontalAlignment(HorizontalAlignment::Left);
        m_wordFreqModeBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Words", L"詞語" }.Pick(m_language))));
        m_wordFreqModeBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Bigrams (word pairs)", L"詞組（兩字）" }.Pick(m_language))));
        m_wordFreqModeBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Characters", L"字元" }.Pick(m_language))));
        m_wordFreqModeBox.SelectedIndex(m_wordFreqMode);
        AutomationProperties::SetAutomationId(m_wordFreqModeBox, L"NativeWordFreqMode");
        AutomationProperties::SetName(m_wordFreqModeBox, ToHString(winforge::core::LocalizedText{
            L"Frequency counting mode", L"詞頻統計模式" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_wordFreqModeBox, modeLabel);
        m_wordFreqModeBox.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_wordFreqRendering) return;
            m_wordFreqMode = std::max(0, sender.as<ComboBox>().SelectedIndex());
            RefreshWordFrequency();
        });
        Grid::SetColumn(m_wordFreqModeBox, 1);
        modeRow.Children().Append(m_wordFreqModeBox);
        input.Children().Append(modeRow);

        Grid lengthRow;
        lengthRow.ColumnSpacing(10);
        ColumnDefinition lengthLabelColumn;
        lengthLabelColumn.Width(GridLengthHelper::Auto());
        lengthRow.ColumnDefinitions().Append(lengthLabelColumn);
        lengthRow.ColumnDefinitions().Append(ColumnDefinition());
        auto minLengthLabel = CreateText(
            winforge::core::LocalizedText{ L"Minimum length", L"最短長度" }.Pick(m_language), 14);
        minLengthLabel.VerticalAlignment(VerticalAlignment::Center);
        Grid::SetColumn(minLengthLabel, 0);
        lengthRow.Children().Append(minLengthLabel);
        m_wordFreqMinLengthBox = NumberBox();
        m_wordFreqMinLengthBox.Minimum(1);
        m_wordFreqMinLengthBox.Maximum(20);
        m_wordFreqMinLengthBox.Value(m_wordFreqMinLength);
        m_wordFreqMinLengthBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_wordFreqMinLengthBox.MinWidth(130);
        m_wordFreqMinLengthBox.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_wordFreqMinLengthBox, L"NativeWordFreqMinLength");
        AutomationProperties::SetName(m_wordFreqMinLengthBox, ToHString(winforge::core::LocalizedText{
            L"Minimum token length", L"最短 token 長度" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_wordFreqMinLengthBox, minLengthLabel);
        m_wordFreqMinLengthBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (m_wordFreqRendering) return;
            m_wordFreqMinLength = sender.Value();
            RefreshWordFrequency();
        });
        Grid::SetColumn(m_wordFreqMinLengthBox, 1);
        lengthRow.Children().Append(m_wordFreqMinLengthBox);
        input.Children().Append(lengthRow);

        auto appendOption = [this, &input](
            CheckBox& target,
            bool value,
            std::wstring_view text,
            std::wstring_view automationId,
            bool MainWindow::* state)
        {
            target = CheckBox();
            target.Content(box_value(ToHString(text)));
            target.IsChecked(box_value(value).as<Windows::Foundation::IReference<bool>>());
            AutomationProperties::SetAutomationId(target, ToHString(automationId));
            AutomationProperties::SetName(target, ToHString(text));
            target.Click([this, state](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_wordFreqRendering) return;
                this->*state = unbox_value_or<bool>(sender.as<CheckBox>().IsChecked(), false);
                RefreshWordFrequency();
            });
            input.Children().Append(target);
        };
        appendOption(
            m_wordFreqCaseBox,
            m_wordFreqCaseInsensitive,
            winforge::core::LocalizedText{
                L"Case-insensitive (fold to lowercase)", L"唔分大細楷（轉細楷）" }.Pick(m_language),
            L"NativeWordFreqCaseInsensitive",
            &MainWindow::m_wordFreqCaseInsensitive);
        appendOption(
            m_wordFreqPunctuationBox,
            m_wordFreqStripPunctuation,
            winforge::core::LocalizedText{ L"Strip punctuation", L"去除標點" }.Pick(m_language),
            L"NativeWordFreqStripPunctuation",
            &MainWindow::m_wordFreqStripPunctuation);
        appendOption(
            m_wordFreqStopWordsBox,
            m_wordFreqRemoveStopWords,
            winforge::core::LocalizedText{
                L"Remove common stop-words (English)", L"移除常見停用詞（英文）" }.Pick(m_language),
            L"NativeWordFreqRemoveStopWords",
            &MainWindow::m_wordFreqRemoveStopWords);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Grid summary;
        summary.ColumnSpacing(12);
        summary.ColumnDefinitions().Append(ColumnDefinition());
        ColumnDefinition copyColumn;
        copyColumn.Width(GridLengthHelper::Auto());
        summary.ColumnDefinitions().Append(copyColumn);
        m_wordFreqTotals = CreateText(L"", 13);
        m_wordFreqTotals.Opacity(0.78);
        m_wordFreqTotals.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_wordFreqTotals, L"NativeWordFreqTotals");
        // Managed parity: this named summary stays queryable to assistive
        // technology without announcing a polite live region on every edit.
        Grid::SetColumn(m_wordFreqTotals, 0);
        summary.Children().Append(m_wordFreqTotals);
        m_wordFreqCopyButton = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy as CSV", L"複製為 CSV" }.Pick(m_language),
            L"NativeWordFreqCopy");
        m_wordFreqCopyButton.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            try
            {
                auto const csv = winforge::core::textanalysis::ToWordFrequencyCsv(m_wordFreqLast);
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.SetText(ToHString(csv));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                auto const copied = winforge::core::LocalizedText{
                    L"Copied!", L"已複製！" }.Pick(m_language);
                m_wordFreqCopyButton.Content(box_value(ToHString(copied)));
                AutomationProperties::SetName(m_wordFreqCopyButton, ToHString(copied));
            }
            catch (...)
            {
                // Managed parity: a clipboard failure does not interrupt analysis.
            }
        });
        Grid::SetColumn(m_wordFreqCopyButton, 1);
        summary.Children().Append(m_wordFreqCopyButton);
        page.Children().Append(summary);

        Border resultCard = MakeNativeCard();
        resultCard.Padding(Thickness{ 4 });
        m_wordFreqResults = ListView();
        m_wordFreqResults.SelectionMode(ListViewSelectionMode::None);
        m_wordFreqResults.MaxHeight(520);
        m_wordFreqResults.HorizontalContentAlignment(HorizontalAlignment::Stretch);
        ScrollViewer::SetVerticalScrollBarVisibility(m_wordFreqResults, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_wordFreqResults, L"NativeWordFreqResults");
        AutomationProperties::SetName(m_wordFreqResults, ToHString(winforge::core::LocalizedText{
            L"Ranked frequency results", L"詞頻排名結果" }.Pick(m_language)));
        m_wordFreqResults.ContainerContentChanging([this](ListViewBase const&, ContainerContentChangingEventArgs const& args)
        {
            if (args.InRecycleQueue()) return;
            auto const index = static_cast<std::size_t>(args.ItemIndex());
            auto const container = args.ItemContainer();
            if (!container || index >= m_wordFreqLast.rows.size()) return;
            auto const& item = m_wordFreqLast.rows[index];
            Grid row;
            row.Padding(Thickness{ 6, 4, 6, 4 });
            row.ColumnSpacing(10);
            for (auto const width : std::array<double, 2>{ 40.0, 150.0 })
            {
                ColumnDefinition definition;
                definition.Width(GridLengthHelper::FromPixels(width));
                row.ColumnDefinitions().Append(definition);
            }
            row.ColumnDefinitions().Append(ColumnDefinition());
            for (auto const width : std::array<double, 2>{ 56.0, 62.0 })
            {
                ColumnDefinition definition;
                definition.Width(GridLengthHelper::FromPixels(width));
                row.ColumnDefinitions().Append(definition);
            }
            auto rank = CreateText(std::to_wstring(item.rank), 13);
            rank.Opacity(0.75);
            Grid::SetColumn(rank, 0);
            row.Children().Append(rank);
            auto term = CreateText(item.term, 14, true);
            term.TextTrimming(TextTrimming::CharacterEllipsis);
            term.TextWrapping(TextWrapping::NoWrap);
            Grid::SetColumn(term, 1);
            row.Children().Append(term);
            ProgressBar bar;
            bar.Minimum(0);
            bar.Maximum(220);
            bar.Value(item.barWidth);
            bar.Height(12);
            bar.VerticalAlignment(VerticalAlignment::Center);
            bar.HorizontalAlignment(HorizontalAlignment::Stretch);
            Grid::SetColumn(bar, 2);
            row.Children().Append(bar);
            auto total = CreateText(std::to_wstring(item.count), 13);
            total.HorizontalAlignment(HorizontalAlignment::Right);
            Grid::SetColumn(total, 3);
            row.Children().Append(total);
            auto percent = CreateText(item.percent, 13);
            percent.Opacity(0.75);
            percent.HorizontalAlignment(HorizontalAlignment::Right);
            Grid::SetColumn(percent, 4);
            row.Children().Append(percent);
            container.Content(row);
            container.Padding(Thickness{});
            container.MinHeight(0);
            container.HorizontalContentAlignment(HorizontalAlignment::Stretch);
            AutomationProperties::SetAutomationId(
                container,
                ToHString(L"NativeWordFreqRow" + std::to_wstring(index)));
            AutomationProperties::SetName(
                container,
                ToHString(std::to_wstring(item.rank) + L" · " + item.term + L" · " +
                    std::to_wstring(item.count) + L" · " + item.percent));
        });
        resultCard.Child(m_wordFreqResults);
        page.Children().Append(resultCard);

        ShowPage(page);
        m_wordFreqRendering = false;
        RefreshWordFrequency();
    }

    void MainWindow::RefreshWordFrequency()
    {
        if (!m_wordFreqTotals || !m_wordFreqResults) return;
        using namespace winforge::core::textanalysis;
        auto const minLength = std::isfinite(m_wordFreqMinLength)
            ? std::clamp(static_cast<int>(m_wordFreqMinLength), 1, 20)
            : 1;
        WordFrequencyOptions options;
        options.mode = m_wordFreqMode == 1
            ? FrequencyMode::Bigrams
            : (m_wordFreqMode == 2 ? FrequencyMode::Characters : FrequencyMode::Words);
        options.caseInsensitive = m_wordFreqCaseInsensitive;
        options.minLength = minLength;
        options.stripPunctuation = m_wordFreqStripPunctuation;
        options.removeStopWords = m_wordFreqRemoveStopWords;
        m_wordFreqLast = AnalyzeWordFrequency(m_wordFreqInput, options);

        auto const diversity = FormatCurrentOneDecimal(m_wordFreqLast.diversity * 100.0);
        auto const totals = winforge::core::LocalizedText{
            L"Total: " + FormatCurrentCount(m_wordFreqLast.totalTokens) + L"   Unique: " +
                FormatCurrentCount(m_wordFreqLast.uniqueTokens) + L"   Lexical diversity: " + diversity + L"%",
            L"總數：" + FormatCurrentCount(m_wordFreqLast.totalTokens) + L"   不重複：" +
                FormatCurrentCount(m_wordFreqLast.uniqueTokens) + L"   詞彙多樣性：" + diversity + L"%" }.Pick(m_language);
        m_wordFreqTotals.Text(ToHString(totals));
        AutomationProperties::SetName(m_wordFreqTotals, ToHString(totals));

        m_wordFreqResults.Items().Clear();
        for (std::size_t index{}; index < m_wordFreqLast.rows.size(); ++index)
        {
            m_wordFreqResults.Items().Append(box_value(static_cast<int32_t>(index)));
        }
    }

    void MainWindow::RenderStringCompare()
    {
        m_stringCompareRendering = true;
        auto page = CreatePage(
            L"String Compare · 字串相似度",
            winforge::core::LocalizedText{
                L"Compare two strings and see how similar they are — edit distance, similarity %, and several classic string metrics. Everything is computed locally, live as you type.",
                L"比較兩段文字，睇下佢哋有幾似 — 編輯距離、相似度百分比同幾個經典字串指標。全部喺本機即時計算，一邊打一邊出。" }.Pick(m_language));
        page.MaxWidth(760);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeStringComparePage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native string comparison", L"全原生字串比較" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Normalization, Levenshtein, Damerau–Levenshtein, Hamming, Jaro–Winkler, longest-common metrics, and report generation run locally in standard C++.",
            L"正規化、Levenshtein、Damerau–Levenshtein、Hamming、Jaro–Winkler、最長共同指標同報告生成全部用標準 C++ 喺本機執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeStringCompareImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(12);
        auto appendInput = [this, &input](
            TextBox& target,
            std::wstring const& value,
            std::wstring_view labelText,
            std::wstring_view accessibleName,
            std::wstring_view automationId,
            std::wstring MainWindow::* state)
        {
            StackPanel group;
            group.Spacing(4);
            auto label = CreateText(labelText, 13, true);
            group.Children().Append(label);
            target = TextBox();
            target.AcceptsReturn(true);
            target.Text(ToHString(TextBoxPresentation(value)));
            target.TextWrapping(TextWrapping::Wrap);
            target.MinHeight(72);
            target.IsSpellCheckEnabled(false);
            ScrollViewer::SetVerticalScrollBarVisibility(target, ScrollBarVisibility::Auto);
            AutomationProperties::SetAutomationId(target, ToHString(automationId));
            AutomationProperties::SetName(target, ToHString(accessibleName));
            AutomationProperties::SetLabeledBy(target, label);
            target.TextChanged([this, state](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                if (m_stringCompareRendering) return;
                this->*state = ToWide(sender.as<TextBox>().Text());
                RefreshStringCompare();
            });
            group.Children().Append(target);
            input.Children().Append(group);
        };
        appendInput(
            m_stringCompareInputA,
            m_stringCompareA,
            winforge::core::LocalizedText{ L"String A", L"字串 A" }.Pick(m_language),
            winforge::core::LocalizedText{ L"String A", L"字串 A" }.Pick(m_language),
            L"NativeStringCompareInputA",
            &MainWindow::m_stringCompareA);
        appendInput(
            m_stringCompareInputB,
            m_stringCompareB,
            winforge::core::LocalizedText{ L"String B", L"字串 B" }.Pick(m_language),
            winforge::core::LocalizedText{ L"String B", L"字串 B" }.Pick(m_language),
            L"NativeStringCompareInputB",
            &MainWindow::m_stringCompareB);

        Grid toggles;
        toggles.ColumnSpacing(16);
        for (int column{}; column < 2; ++column)
        {
            ColumnDefinition definition;
            definition.Width(GridLengthHelper::FromValueAndType(1.0, GridUnitType::Star));
            toggles.ColumnDefinitions().Append(definition);
        }
        auto appendToggle = [this, &toggles](
            ToggleSwitch& target,
            bool value,
            std::wstring_view header,
            std::wstring_view automationId,
            int column,
            bool MainWindow::* state)
        {
            target = ToggleSwitch();
            target.Header(box_value(ToHString(header)));
            target.OnContent(box_value(L""));
            target.OffContent(box_value(L""));
            target.IsOn(value);
            AutomationProperties::SetAutomationId(target, ToHString(automationId));
            AutomationProperties::SetName(target, ToHString(header));
            target.Toggled([this, state](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_stringCompareRendering) return;
                this->*state = sender.as<ToggleSwitch>().IsOn();
                RefreshStringCompare();
            });
            Grid::SetColumn(target, column);
            toggles.Children().Append(target);
        };
        appendToggle(
            m_stringCompareCaseSwitch,
            m_stringCompareIgnoreCase,
            winforge::core::LocalizedText{ L"Case-insensitive", L"唔理大小寫" }.Pick(m_language),
            L"NativeStringCompareIgnoreCase",
            0,
            &MainWindow::m_stringCompareIgnoreCase);
        appendToggle(
            m_stringCompareWhitespaceSwitch,
            m_stringCompareIgnoreWhitespace,
            winforge::core::LocalizedText{ L"Ignore whitespace", L"唔理空白字元" }.Pick(m_language),
            L"NativeStringCompareIgnoreWhitespace",
            1,
            &MainWindow::m_stringCompareIgnoreWhitespace);
        input.Children().Append(toggles);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border metricsCard = MakeNativeCard();
        StackPanel content;
        content.Spacing(12);
        Grid heading;
        heading.ColumnDefinitions().Append(ColumnDefinition());
        ColumnDefinition actionColumn;
        actionColumn.Width(GridLengthHelper::Auto());
        heading.ColumnDefinitions().Append(actionColumn);
        auto metricsTitle = CreateText(
            winforge::core::LocalizedText{ L"Metrics", L"指標" }.Pick(m_language), 15, true);
        metricsTitle.VerticalAlignment(VerticalAlignment::Center);
        Grid::SetColumn(metricsTitle, 0);
        heading.Children().Append(metricsTitle);
        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy report", L"複製報告" }.Pick(m_language),
            L"NativeStringCompareCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            try
            {
                auto const report = winforge::core::textanalysis::BuildStringComparisonReport(
                    m_stringCompareLastRows);
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.SetText(ToHString(report));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceStringCompareStatus(winforge::core::LocalizedText{
                    L"Report copied to the clipboard.", L"報告已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceStringCompareStatus(winforge::core::LocalizedText{
                    L"Could not copy the report.", L"複製唔到報告。" }.Pick(m_language), true);
            }
        });
        Grid::SetColumn(copy, 1);
        heading.Children().Append(copy);
        content.Children().Append(heading);
        m_stringCompareMetrics = StackPanel();
        m_stringCompareMetrics.Spacing(2);
        AutomationProperties::SetAutomationId(m_stringCompareMetrics, L"NativeStringCompareMetrics");
        content.Children().Append(m_stringCompareMetrics);
        m_stringCompareStatus = CreateText(L"", 12);
        m_stringCompareStatus.Opacity(0.78);
        m_stringCompareStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_stringCompareStatus, L"NativeStringCompareStatus");
        content.Children().Append(m_stringCompareStatus);
        metricsCard.Child(content);
        page.Children().Append(metricsCard);

        ShowPage(page);
        m_stringCompareRendering = false;
        RefreshStringCompare();
    }

    void MainWindow::RefreshStringCompare()
    {
        if (!m_stringCompareMetrics || !m_stringCompareStatus) return;
        using namespace winforge::core::textanalysis;
        auto const value = ComputeStringComparison(
            m_stringCompareA,
            m_stringCompareB,
            m_stringCompareIgnoreCase,
            m_stringCompareIgnoreWhitespace);
        auto const pick = [this](std::wstring en, std::wstring zh)
        {
            return winforge::core::LocalizedText{ std::move(en), std::move(zh) }.Pick(m_language);
        };
        m_stringCompareLastRows.clear();
        std::vector<std::wstring> ids;
        auto append = [this, &ids](std::wstring id, std::wstring label, std::wstring metric)
        {
            ids.push_back(std::move(id));
            m_stringCompareLastRows.emplace_back(std::move(label), std::move(metric));
        };
        append(
            L"NativeStringCompareLength",
            pick(L"Length A / B", L"長度 A / B"),
            std::to_wstring(value.lenA) + L" / " + std::to_wstring(value.lenB));
        if (value.truncated)
        {
            append(
                L"NativeStringCompareLevenshtein",
                pick(L"Levenshtein distance", L"Levenshtein 編輯距離"),
                pick(L"skipped", L"已略過"));
            append(
                L"NativeStringCompareSimilarity",
                pick(L"Similarity", L"相似度"),
                L"n/a");
            append(
                L"NativeStringCompareDamerau",
                pick(L"Damerau–Levenshtein", L"Damerau–Levenshtein 距離"),
                pick(L"skipped", L"已略過"));
            auto const announceWarning = !m_stringCompareTruncationWarningActive;
            m_stringCompareTruncationWarningActive = true;
            AnnounceStringCompareStatus(pick(
                L"One or both strings exceed 2,000 characters — the distance metrics are skipped to stay responsive. Hamming and Jaro–Winkler are still shown.",
                L"其中一段（或兩段）超過 2,000 個字元 — 為咗保持流暢，略過咗距離指標。Hamming 同 Jaro–Winkler 照樣顯示。"), true, announceWarning);
        }
        else
        {
            m_stringCompareTruncationWarningActive = false;
            append(
                L"NativeStringCompareLevenshtein",
                pick(L"Levenshtein distance", L"Levenshtein 編輯距離"),
                std::to_wstring(value.levenshtein));
            append(
                L"NativeStringCompareSimilarity",
                pick(L"Similarity", L"相似度"),
                std::isnan(value.similarityPct)
                    ? L"n/a"
                    : FormatInvariantOneDecimal(value.similarityPct) + L"%");
            append(
                L"NativeStringCompareDamerau",
                pick(L"Damerau–Levenshtein", L"Damerau–Levenshtein 距離"),
                std::to_wstring(value.damerau));
            AnnounceStringCompareStatus(pick(
                L"Computed locally — nothing leaves your PC.",
                L"喺本機計算 — 冇任何資料離開你部電腦。"), false, false);
        }
        append(
            L"NativeStringCompareHamming",
            pick(L"Hamming distance", L"Hamming 距離"),
            value.hamming < 0
                ? pick(L"n/a (lengths differ)", L"n/a（長度唔同）")
                : std::to_wstring(value.hamming));
        append(
            L"NativeStringCompareJaroWinkler",
            pick(L"Jaro–Winkler similarity", L"Jaro–Winkler 相似度"),
            std::isnan(value.jaroWinkler)
                ? L"n/a"
                : FormatInvariantOneDecimal(value.jaroWinkler * 100.0) + L"%");
        if (!value.truncated)
        {
            append(
                L"NativeStringCompareLongestSubstring",
                pick(L"Longest common substring", L"最長共同子字串"),
                std::to_wstring(value.longestCommonSubstring));
            append(
                L"NativeStringCompareLongestSubsequence",
                pick(L"Longest common subsequence", L"最長共同子序列"),
                std::to_wstring(value.longestCommonSubsequence));
        }

        m_stringCompareMetrics.Children().Clear();
        for (std::size_t index{}; index < m_stringCompareLastRows.size(); ++index)
        {
            auto const& [labelValue, metricValue] = m_stringCompareLastRows[index];
            Grid row;
            row.Padding(Thickness{ 0, 3, 0, 3 });
            row.ColumnDefinitions().Append(ColumnDefinition());
            ColumnDefinition valueColumn;
            valueColumn.Width(GridLengthHelper::Auto());
            row.ColumnDefinitions().Append(valueColumn);
            auto label = CreateText(labelValue, 14);
            label.Opacity(0.78);
            label.TextWrapping(TextWrapping::Wrap);
            AutomationProperties::SetAutomationId(label, ToHString(ids[index]));
            AutomationProperties::SetName(label, ToHString(labelValue + L": " + metricValue));
            Grid::SetColumn(label, 0);
            row.Children().Append(label);
            auto metric = CreateText(metricValue, 14, true);
            metric.FontFamily(Media::FontFamily(L"Consolas"));
            metric.Margin(Thickness{ 12, 0, 0, 0 });
            Grid::SetColumn(metric, 1);
            row.Children().Append(metric);
            m_stringCompareMetrics.Children().Append(row);
        }
    }

    void MainWindow::AnnounceStringCompareStatus(
        std::wstring_view message,
        bool warning,
        bool announce)
    {
        if (!m_stringCompareStatus) return;
        AutomationProperties::SetLiveSetting(
            m_stringCompareStatus,
            announce
                ? Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite
                : Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Off);
        m_stringCompareStatus.Text(ToHString(message));
        m_stringCompareStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_stringCompareStatus, ToHString(message));
        if (announce)
        {
            RaisePoliteLiveRegion(m_stringCompareStatus);
        }
    }

    void MainWindow::RenderPhonetic()
    {
        using namespace winforge::core::referencetext;
        m_phoneticRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Phonetic Speller", L"拼讀字母表" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Spell text with NATO/ICAO, LAPD police, or simple code words. Letters, digits, spaces, punctuation, and explicit clipboard copy stay on this PC.",
                L"用北約／ICAO、LAPD 警察或者簡單代碼字逐個拼讀文字。字母、數字、空格、標點同明確複製全部留喺呢部電腦。" }.Pick(m_language));
        page.MaxWidth(820);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativePhoneticPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native phonetic spelling", L"全原生拼讀" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"All alphabet tables and transformations run in standard C++; clipboard access only follows the Copy button.",
            L"全部字母表同轉換都用標準 C++ 執行；只會喺你撳複製掣之後先存取剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativePhoneticImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(12);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Text to spell", L"要拼讀嘅文字" }.Pick(m_language), 14, true);
        input.Children().Append(inputLabel);
        m_phoneticInputBox = TextBox();
        m_phoneticInputBox.Text(ToHString(m_phoneticInput));
        m_phoneticInputBox.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"e.g. ABC-123", L"例如 ABC-123" }.Pick(m_language)));
        m_phoneticInputBox.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_phoneticInputBox, L"NativePhoneticInput");
        AutomationProperties::SetLabeledBy(m_phoneticInputBox, inputLabel);
        m_phoneticInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_phoneticRendering) return;
            m_phoneticInput = ToWide(sender.as<TextBox>().Text());
            RefreshPhonetic();
        });
        input.Children().Append(m_phoneticInputBox);

        auto alphabetLabel = CreateText(
            winforge::core::LocalizedText{ L"Alphabet", L"字母表" }.Pick(m_language), 13, true);
        input.Children().Append(alphabetLabel);
        m_phoneticAlphabetBox = ComboBox();
        m_phoneticAlphabetBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_phoneticAlphabetBox.Items().Append(box_value(L"NATO / ICAO"));
        m_phoneticAlphabetBox.Items().Append(box_value(L"LAPD / Police"));
        m_phoneticAlphabetBox.Items().Append(box_value(winforge::core::LocalizedText{
            L"Simple words", L"簡單英文字" }.Pick(m_language)));
        m_phoneticAlphabetBox.SelectedIndex(std::clamp(m_phoneticAlphabet, 0, 2));
        AutomationProperties::SetAutomationId(m_phoneticAlphabetBox, L"NativePhoneticAlphabet");
        AutomationProperties::SetLabeledBy(m_phoneticAlphabetBox, alphabetLabel);
        m_phoneticAlphabetBox.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_phoneticRendering) return;
            m_phoneticAlphabet = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 2);
            RefreshPhonetic();
        });
        input.Children().Append(m_phoneticAlphabetBox);

        m_phoneticUpperBox = CheckBox();
        m_phoneticUpperBox.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Upper-case the displayed characters", L"顯示字元轉大寫" }.Pick(m_language))));
        m_phoneticUpperBox.IsChecked(box_value(m_phoneticUpper).as<Windows::Foundation::IReference<bool>>());
        AutomationProperties::SetAutomationId(m_phoneticUpperBox, L"NativePhoneticUpper");
        m_phoneticUpperBox.Click([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_phoneticRendering) return;
            m_phoneticUpper = unbox_value_or<bool>(sender.as<CheckBox>().IsChecked(), false);
            RefreshPhonetic();
        });
        input.Children().Append(m_phoneticUpperBox);

        m_phoneticPunctuationBox = CheckBox();
        m_phoneticPunctuationBox.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Keep punctuation and symbols", L"保留標點同符號" }.Pick(m_language))));
        m_phoneticPunctuationBox.IsChecked(
            box_value(m_phoneticKeepPunctuation).as<Windows::Foundation::IReference<bool>>());
        AutomationProperties::SetAutomationId(m_phoneticPunctuationBox, L"NativePhoneticKeepPunctuation");
        m_phoneticPunctuationBox.Click([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_phoneticRendering) return;
            m_phoneticKeepPunctuation = unbox_value_or<bool>(sender.as<CheckBox>().IsChecked(), false);
            RefreshPhonetic();
        });
        input.Children().Append(m_phoneticPunctuationBox);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border outputCard = MakeNativeCard();
        StackPanel output;
        output.Spacing(10);
        auto spokenLabel = CreateText(
            winforge::core::LocalizedText{ L"Spoken", L"讀法" }.Pick(m_language), 15, true);
        output.Children().Append(spokenLabel);
        m_phoneticSpokenBox = TextBox();
        m_phoneticSpokenBox.IsReadOnly(true);
        m_phoneticSpokenBox.AcceptsReturn(true);
        m_phoneticSpokenBox.TextWrapping(TextWrapping::Wrap);
        m_phoneticSpokenBox.MinHeight(72);
        AutomationProperties::SetAutomationId(m_phoneticSpokenBox, L"NativePhoneticSpoken");
        AutomationProperties::SetLabeledBy(m_phoneticSpokenBox, spokenLabel);
        output.Children().Append(m_phoneticSpokenBox);

        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy spoken", L"複製讀法" }.Pick(m_language),
            L"NativePhoneticCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            auto const value = m_phoneticSpoken.empty()
                ? winforge::core::LocalizedText{ L"(nothing to spell yet)", L"（暫時冇嘢拼讀）" }.Pick(m_language)
                : m_phoneticSpoken;
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(value));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnouncePhoneticStatus(winforge::core::LocalizedText{
                    L"Spoken text copied to the clipboard.", L"讀法已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnouncePhoneticStatus(winforge::core::LocalizedText{
                    L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
            }
        });
        output.Children().Append(copy);

        m_phoneticRows = ListView();
        m_phoneticRows.SelectionMode(ListViewSelectionMode::None);
        m_phoneticRows.MaxHeight(360);
        AutomationProperties::SetAutomationId(m_phoneticRows, L"NativePhoneticRows");
        output.Children().Append(m_phoneticRows);
        m_phoneticStatus = CreateText(L"", 12.5);
        m_phoneticStatus.Opacity(0.82);
        m_phoneticStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_phoneticStatus, L"NativePhoneticStatus");
        output.Children().Append(m_phoneticStatus);
        outputCard.Child(output);
        page.Children().Append(outputCard);

        ShowPage(page);
        m_phoneticRendering = false;
        RefreshPhonetic();
    }

    void MainWindow::RefreshPhonetic()
    {
        using namespace winforge::core::referencetext;
        if (!m_phoneticSpokenBox || !m_phoneticRows || !m_phoneticStatus) return;
        auto const alphabet = static_cast<PhoneticAlphabet>(std::clamp(m_phoneticAlphabet, 0, 2));
        auto const result = SpellPhonetic(
            m_phoneticInput,
            alphabet,
            m_phoneticUpper,
            m_phoneticKeepPunctuation);
        m_phoneticSpoken = result.spoken;
        auto const display = result.spoken.empty()
            ? winforge::core::LocalizedText{ L"(nothing to spell yet)", L"（暫時冇嘢拼讀）" }.Pick(m_language)
            : result.spoken;
        m_phoneticSpokenBox.Text(ToHString(display));
        m_phoneticRows.Items().Clear();
        for (auto const& item : result.characters)
        {
            m_phoneticRows.Items().Append(box_value(ToHString(item.character + L": " + item.code)));
        }
        auto const status = winforge::core::LocalizedText{
            std::to_wstring(result.characters.size()) + L" item(s) spelled locally.",
            L"已喺本機拼讀 " + std::to_wstring(result.characters.size()) + L" 個項目。" }.Pick(m_language);
        AnnouncePhoneticStatus(status, false, false);
    }

    void MainWindow::AnnouncePhoneticStatus(
        std::wstring_view message,
        bool warning,
        bool announce)
    {
        if (!m_phoneticStatus) return;
        AutomationProperties::SetLiveSetting(
            m_phoneticStatus,
            announce
                ? Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite
                : Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Off);
        m_phoneticStatus.Text(ToHString(message));
        m_phoneticStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_phoneticStatus, ToHString(message));
        if (announce)
        {
            RaisePoliteLiveRegion(m_phoneticStatus);
        }
    }

    void MainWindow::RenderBoxText()
    {
        using namespace winforge::core::referencetext;
        m_boxTextRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Box & Banner Text", L"文字方框" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Wrap multiline text in a drawn box or comment banner with selectable borders, alignment, padding, and an optional title.",
                L"將多行文字包成方框或者註解橫幅，可以揀邊框、對齊、內距同可選標題。" }.Pick(m_language));
        page.MaxWidth(860);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeBoxTextPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native box rendering", L"全原生方框產生" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Line normalization, Unicode-aware display width, eight border styles, alignment, title bars, and comment blocks run locally in standard C++.",
            L"換行正規化、Unicode 顯示闊度、八種邊框、對齊、標題列同註解區塊全部喺本機用標準 C++ 執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeBoxTextImplementationStatus");
        page.Children().Append(implementation);

        Border inputCard = MakeNativeCard();
        StackPanel input;
        input.Spacing(10);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Text to box", L"要入框嘅文字" }.Pick(m_language), 14, true);
        input.Children().Append(inputLabel);
        m_boxTextInputBox = TextBox();
        m_boxTextInputBox.AcceptsReturn(true);
        m_boxTextInputBox.TextWrapping(TextWrapping::Wrap);
        m_boxTextInputBox.MinHeight(120);
        m_boxTextInputBox.Text(ToHString(TextBoxPresentation(m_boxTextInput)));
        m_boxTextInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_boxTextInputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_boxTextInputBox, L"NativeBoxTextInput");
        AutomationProperties::SetLabeledBy(m_boxTextInputBox, inputLabel);
        m_boxTextInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_boxTextRendering) return;
            m_boxTextInput = ToWide(sender.as<TextBox>().Text());
            RefreshBoxText();
        });
        input.Children().Append(m_boxTextInputBox);

        auto styleLabel = CreateText(
            winforge::core::LocalizedText{ L"Border style", L"邊框樣式" }.Pick(m_language), 13, true);
        input.Children().Append(styleLabel);
        m_boxTextStyleBox = ComboBox();
        m_boxTextStyleBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        std::array<std::wstring, 8> const styleLabels{
            winforge::core::LocalizedText{ L"ASCII (+ - |)", L"ASCII（+ - |）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Single (─ │)", L"單線（─ │）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Double (═ ║)", L"雙線（═ ║）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Rounded (╭ ╮)", L"圓角（╭ ╮）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Heavy (━ ┃)", L"粗線（━ ┃）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Stars (*)", L"星號（*）" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Comment /* … */", L"註解 /* … */" }.Pick(m_language),
            winforge::core::LocalizedText{ L"Comment ### … ###", L"註解 ### … ###" }.Pick(m_language),
        };
        for (auto const& label : styleLabels) m_boxTextStyleBox.Items().Append(box_value(ToHString(label)));
        m_boxTextStyleBox.SelectedIndex(std::clamp(m_boxTextStyle, 0, 7));
        AutomationProperties::SetAutomationId(m_boxTextStyleBox, L"NativeBoxTextStyle");
        AutomationProperties::SetLabeledBy(m_boxTextStyleBox, styleLabel);
        m_boxTextStyleBox.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_boxTextRendering) return;
            m_boxTextStyle = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 7);
            RefreshBoxText();
        });
        input.Children().Append(m_boxTextStyleBox);

        auto alignmentLabel = CreateText(
            winforge::core::LocalizedText{ L"Alignment", L"對齊" }.Pick(m_language), 13, true);
        input.Children().Append(alignmentLabel);
        m_boxTextAlignmentBox = ComboBox();
        m_boxTextAlignmentBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_boxTextAlignmentBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Left", L"靠左" }.Pick(m_language))));
        m_boxTextAlignmentBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Center", L"置中" }.Pick(m_language))));
        m_boxTextAlignmentBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Right", L"靠右" }.Pick(m_language))));
        m_boxTextAlignmentBox.SelectedIndex(std::clamp(m_boxTextAlignment, 0, 2));
        AutomationProperties::SetAutomationId(m_boxTextAlignmentBox, L"NativeBoxTextAlignment");
        AutomationProperties::SetLabeledBy(m_boxTextAlignmentBox, alignmentLabel);
        m_boxTextAlignmentBox.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_boxTextRendering) return;
            m_boxTextAlignment = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 2);
            RefreshBoxText();
        });
        input.Children().Append(m_boxTextAlignmentBox);

        auto paddingLabel = CreateText(
            winforge::core::LocalizedText{ L"Horizontal padding", L"水平內距" }.Pick(m_language), 13, true);
        input.Children().Append(paddingLabel);
        m_boxTextPaddingBox = NumberBox();
        m_boxTextPaddingBox.Minimum(0);
        m_boxTextPaddingBox.Maximum(40);
        m_boxTextPaddingBox.Value(m_boxTextPadding);
        m_boxTextPaddingBox.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_boxTextPaddingBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        AutomationProperties::SetAutomationId(m_boxTextPaddingBox, L"NativeBoxTextPadding");
        AutomationProperties::SetLabeledBy(m_boxTextPaddingBox, paddingLabel);
        m_boxTextPaddingBox.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (m_boxTextRendering) return;
            m_boxTextPadding = sender.Value();
            RefreshBoxText();
        });
        input.Children().Append(m_boxTextPaddingBox);

        auto titleLabel = CreateText(
            winforge::core::LocalizedText{ L"Title (optional)", L"標題（可選）" }.Pick(m_language), 13, true);
        input.Children().Append(titleLabel);
        m_boxTextTitleBox = TextBox();
        m_boxTextTitleBox.Text(ToHString(m_boxTextTitle));
        m_boxTextTitleBox.IsSpellCheckEnabled(false);
        AutomationProperties::SetAutomationId(m_boxTextTitleBox, L"NativeBoxTextTitle");
        AutomationProperties::SetLabeledBy(m_boxTextTitleBox, titleLabel);
        m_boxTextTitleBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_boxTextRendering) return;
            m_boxTextTitle = ToWide(sender.as<TextBox>().Text());
            RefreshBoxText();
        });
        input.Children().Append(m_boxTextTitleBox);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border outputCard = MakeNativeCard();
        StackPanel output;
        output.Spacing(10);
        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Result", L"結果" }.Pick(m_language), 15, true);
        output.Children().Append(outputLabel);
        m_boxTextOutputBox = TextBox();
        m_boxTextOutputBox.IsReadOnly(true);
        m_boxTextOutputBox.AcceptsReturn(true);
        m_boxTextOutputBox.TextWrapping(TextWrapping::NoWrap);
        m_boxTextOutputBox.FontFamily(Media::FontFamily(L"Consolas"));
        m_boxTextOutputBox.MinHeight(180);
        m_boxTextOutputBox.MaxHeight(360);
        ScrollViewer::SetVerticalScrollBarVisibility(m_boxTextOutputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollBarVisibility(m_boxTextOutputBox, ScrollBarVisibility::Auto);
        ScrollViewer::SetHorizontalScrollMode(m_boxTextOutputBox, ScrollMode::Enabled);
        AutomationProperties::SetAutomationId(m_boxTextOutputBox, L"NativeBoxTextOutput");
        AutomationProperties::SetLabeledBy(m_boxTextOutputBox, outputLabel);
        output.Children().Append(m_boxTextOutputBox);
        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy", L"複製" }.Pick(m_language),
            L"NativeBoxTextCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_boxTextOutput.empty())
            {
                AnnounceBoxTextStatus(winforge::core::LocalizedText{
                    L"Nothing to copy yet.", L"暫時冇嘢可以複製。" }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_boxTextOutput));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceBoxTextStatus(winforge::core::LocalizedText{
                    L"Copied to the clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceBoxTextStatus(winforge::core::LocalizedText{
                    L"Copy failed.", L"複製失敗。" }.Pick(m_language), true);
            }
        });
        output.Children().Append(copy);
        m_boxTextStatus = CreateText(L"", 12.5);
        m_boxTextStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_boxTextStatus, L"NativeBoxTextStatus");
        output.Children().Append(m_boxTextStatus);
        outputCard.Child(output);
        page.Children().Append(outputCard);

        ShowPage(page);
        m_boxTextRendering = false;
        RefreshBoxText();
    }

    void MainWindow::RefreshBoxText()
    {
        using namespace winforge::core::referencetext;
        if (!m_boxTextOutputBox || !m_boxTextStatus) return;
        auto const padding = std::isfinite(m_boxTextPadding)
            ? std::clamp(static_cast<int>(m_boxTextPadding), 0, 40)
            : 0;
        m_boxTextOutput = winforge::core::referencetext::RenderBoxText(
            m_boxTextInput,
            static_cast<BoxBorderStyle>(std::clamp(m_boxTextStyle, 0, 7)),
            padding,
            static_cast<BoxAlignment>(std::clamp(m_boxTextAlignment, 0, 2)),
            m_boxTextTitle);
        m_boxTextOutputBox.Text(ToHString(TextBoxPresentation(m_boxTextOutput)));
        auto const lines = m_boxTextOutput.empty()
            ? std::size_t{}
            : static_cast<std::size_t>(std::count(m_boxTextOutput.begin(), m_boxTextOutput.end(), L'\n') + 1);
        auto const status = winforge::core::LocalizedText{
            std::to_wstring(m_boxTextOutput.size()) + L" chars · " + std::to_wstring(lines) + L" lines",
            std::to_wstring(m_boxTextOutput.size()) + L" 個字元 · " + std::to_wstring(lines) + L" 行" }.Pick(m_language);
        AnnounceBoxTextStatus(status, false, false);
    }

    void MainWindow::AnnounceBoxTextStatus(
        std::wstring_view message,
        bool warning,
        bool announce)
    {
        if (!m_boxTextStatus) return;
        AutomationProperties::SetLiveSetting(
            m_boxTextStatus,
            announce
                ? Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite
                : Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Off);
        m_boxTextStatus.Text(ToHString(message));
        m_boxTextStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_boxTextStatus, ToHString(message));
        if (announce)
        {
            RaisePoliteLiveRegion(m_boxTextStatus);
        }
    }

    void MainWindow::RenderHtmlEntities()
    {
        using namespace winforge::core::referencetext;
        m_htmlEntitiesRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"HTML Entities", L"HTML 實體" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Encode HTML-sensitive text, optionally escape every non-ASCII scalar, or decode named and numeric entities. Everything runs locally.",
                L"將 HTML 敏感文字編碼、可選擇跳脫全部非 ASCII scalar，或者解碼具名同數字實體。全部喺本機執行。" }.Pick(m_language));
        page.MaxWidth(880);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeHtmlEntitiesPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native entity conversion", L"全原生實體轉換" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"HTML escaping, Unicode-scalar numeric references, common named entities, malformed-input preservation, and the reference table run in standard C++.",
            L"HTML 跳脫、Unicode scalar 數字參照、常用具名實體、保留格式錯誤輸入同參考表全部用標準 C++ 執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeHtmlEntitiesImplementationStatus");
        page.Children().Append(implementation);

        Border conversionCard = MakeNativeCard();
        StackPanel conversion;
        conversion.Spacing(10);
        auto modeLabel = CreateText(
            winforge::core::LocalizedText{ L"Mode", L"模式" }.Pick(m_language), 13, true);
        conversion.Children().Append(modeLabel);
        m_htmlEntitiesModeBox = ComboBox();
        m_htmlEntitiesModeBox.HorizontalAlignment(HorizontalAlignment::Stretch);
        m_htmlEntitiesModeBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Encode → entities", L"編碼 → 實體" }.Pick(m_language))));
        m_htmlEntitiesModeBox.Items().Append(box_value(ToHString(winforge::core::LocalizedText{
            L"Decode → text", L"解碼 → 文字" }.Pick(m_language))));
        m_htmlEntitiesModeBox.SelectedIndex(m_htmlEntitiesDecode ? 1 : 0);
        AutomationProperties::SetAutomationId(m_htmlEntitiesModeBox, L"NativeHtmlEntitiesMode");
        AutomationProperties::SetLabeledBy(m_htmlEntitiesModeBox, modeLabel);
        m_htmlEntitiesModeBox.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_htmlEntitiesRendering) return;
            m_htmlEntitiesDecode = sender.as<ComboBox>().SelectedIndex() == 1;
            RefreshHtmlEntities();
        });
        conversion.Children().Append(m_htmlEntitiesModeBox);

        m_htmlEntitiesNonAsciiBox = CheckBox();
        m_htmlEntitiesNonAsciiBox.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Also escape every non-ASCII character (as &#xHHHH;)",
            L"連所有非 ASCII 字元都跳脫（變成 &#xHHHH;）" }.Pick(m_language))));
        m_htmlEntitiesNonAsciiBox.IsChecked(
            box_value(m_htmlEntitiesEscapeNonAscii).as<Windows::Foundation::IReference<bool>>());
        m_htmlEntitiesNonAsciiBox.Visibility(
            m_htmlEntitiesDecode ? Visibility::Collapsed : Visibility::Visible);
        AutomationProperties::SetAutomationId(m_htmlEntitiesNonAsciiBox, L"NativeHtmlEntitiesEscapeNonAscii");
        m_htmlEntitiesNonAsciiBox.Click([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_htmlEntitiesRendering) return;
            m_htmlEntitiesEscapeNonAscii = unbox_value_or<bool>(sender.as<CheckBox>().IsChecked(), false);
            RefreshHtmlEntities();
        });
        conversion.Children().Append(m_htmlEntitiesNonAsciiBox);

        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Input", L"輸入" }.Pick(m_language), 14, true);
        conversion.Children().Append(inputLabel);
        m_htmlEntitiesInputBox = TextBox();
        m_htmlEntitiesInputBox.AcceptsReturn(true);
        m_htmlEntitiesInputBox.TextWrapping(TextWrapping::Wrap);
        m_htmlEntitiesInputBox.MinHeight(120);
        m_htmlEntitiesInputBox.Text(ToHString(TextBoxPresentation(m_htmlEntitiesInput)));
        m_htmlEntitiesInputBox.IsSpellCheckEnabled(false);
        ScrollViewer::SetVerticalScrollBarVisibility(m_htmlEntitiesInputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_htmlEntitiesInputBox, L"NativeHtmlEntitiesInput");
        AutomationProperties::SetLabeledBy(m_htmlEntitiesInputBox, inputLabel);
        m_htmlEntitiesInputBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_htmlEntitiesRendering) return;
            m_htmlEntitiesInput = ToWide(sender.as<TextBox>().Text());
            RefreshHtmlEntities();
        });
        conversion.Children().Append(m_htmlEntitiesInputBox);
        m_htmlEntitiesInputCount = CreateText(L"", 12);
        m_htmlEntitiesInputCount.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_htmlEntitiesInputCount, L"NativeHtmlEntitiesInputCount");
        conversion.Children().Append(m_htmlEntitiesInputCount);

        auto outputLabel = CreateText(
            winforge::core::LocalizedText{ L"Output", L"輸出" }.Pick(m_language), 14, true);
        conversion.Children().Append(outputLabel);
        m_htmlEntitiesOutputBox = TextBox();
        m_htmlEntitiesOutputBox.AcceptsReturn(true);
        m_htmlEntitiesOutputBox.TextWrapping(TextWrapping::Wrap);
        m_htmlEntitiesOutputBox.MinHeight(120);
        m_htmlEntitiesOutputBox.IsReadOnly(true);
        ScrollViewer::SetVerticalScrollBarVisibility(m_htmlEntitiesOutputBox, ScrollBarVisibility::Auto);
        AutomationProperties::SetAutomationId(m_htmlEntitiesOutputBox, L"NativeHtmlEntitiesOutput");
        AutomationProperties::SetLabeledBy(m_htmlEntitiesOutputBox, outputLabel);
        conversion.Children().Append(m_htmlEntitiesOutputBox);
        m_htmlEntitiesOutputCount = CreateText(L"", 12);
        m_htmlEntitiesOutputCount.Opacity(0.78);
        AutomationProperties::SetAutomationId(m_htmlEntitiesOutputCount, L"NativeHtmlEntitiesOutputCount");
        conversion.Children().Append(m_htmlEntitiesOutputCount);

        auto copy = MakeNativeButton(
            winforge::core::LocalizedText{ L"Copy output", L"複製輸出" }.Pick(m_language),
            L"NativeHtmlEntitiesCopy");
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_htmlEntitiesOutput.empty())
            {
                AnnounceHtmlEntitiesStatus(winforge::core::LocalizedText{
                    L"Nothing to copy yet.", L"暫時冇嘢可以複製。" }.Pick(m_language), true);
                return;
            }
            try
            {
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(m_htmlEntitiesOutput));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceHtmlEntitiesStatus(winforge::core::LocalizedText{
                    L"Output copied to the clipboard.", L"已將輸出複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceHtmlEntitiesStatus(winforge::core::LocalizedText{
                    L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
            }
        });
        conversion.Children().Append(copy);
        m_htmlEntitiesStatus = CreateText(L"", 12.5);
        m_htmlEntitiesStatus.Opacity(0.82);
        m_htmlEntitiesStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_htmlEntitiesStatus, L"NativeHtmlEntitiesStatus");
        conversion.Children().Append(m_htmlEntitiesStatus);
        conversionCard.Child(conversion);
        page.Children().Append(conversionCard);

        Border referenceCard = MakeNativeCard();
        StackPanel reference;
        reference.Spacing(8);
        reference.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Common entities", L"常用實體" }.Pick(m_language), 15, true));
        auto referenceBlurb = CreateText(
            winforge::core::LocalizedText{
                L"Choose a row to copy its entity name.",
                L"揀一行就可以複製個實體名稱。" }.Pick(m_language), 12.5);
        referenceBlurb.Opacity(0.78);
        referenceBlurb.TextWrapping(TextWrapping::Wrap);
        reference.Children().Append(referenceBlurb);
        m_htmlEntitiesReferenceRows = StackPanel();
        m_htmlEntitiesReferenceRows.Spacing(4);
        auto const& rows = HtmlEntityReferenceList();
        for (std::size_t index{}; index < rows.size(); ++index)
        {
            auto const& item = rows[index];
            auto const localizedDescription = winforge::core::LocalizedText{
                item.description_en, item.description_zh }.Pick(m_language);
            Button rowButton;
            rowButton.HorizontalAlignment(HorizontalAlignment::Stretch);
            rowButton.HorizontalContentAlignment(HorizontalAlignment::Stretch);
            rowButton.Padding(Thickness{ 12, 8, 12, 8 });
            Grid row;
            row.ColumnSpacing(12);
            ColumnDefinition nameColumn;
            nameColumn.Width(GridLengthHelper::FromPixels(160));
            row.ColumnDefinitions().Append(nameColumn);
            ColumnDefinition characterColumn;
            characterColumn.Width(GridLengthHelper::FromPixels(48));
            row.ColumnDefinitions().Append(characterColumn);
            row.ColumnDefinitions().Append(ColumnDefinition());
            auto name = CreateText(item.name, 14, true);
            name.FontFamily(Media::FontFamily(L"Consolas"));
            Grid::SetColumn(name, 0);
            row.Children().Append(name);
            auto character = CreateText(item.character, 18);
            Grid::SetColumn(character, 1);
            row.Children().Append(character);
            auto description = CreateText(localizedDescription, 13);
            description.Opacity(0.78);
            description.TextWrapping(TextWrapping::Wrap);
            Grid::SetColumn(description, 2);
            row.Children().Append(description);
            rowButton.Content(row);
            AutomationProperties::SetAutomationId(
                rowButton,
                ToHString(L"NativeHtmlEntitiesReference" + std::to_wstring(index)));
            AutomationProperties::SetName(
                rowButton, ToHString(item.name + L" " + localizedDescription));
            rowButton.Click([this, entity = item.name](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                try
                {
                    Windows::ApplicationModel::DataTransfer::DataPackage package;
                    package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                    package.SetText(ToHString(entity));
                    Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                    AnnounceHtmlEntitiesStatus(winforge::core::LocalizedText{
                        L"Copied " + entity + L" to the clipboard.",
                        L"已複製 " + entity + L" 到剪貼簿。" }.Pick(m_language));
                }
                catch (...)
                {
                    AnnounceHtmlEntitiesStatus(winforge::core::LocalizedText{
                        L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
                }
            });
            m_htmlEntitiesReferenceRows.Children().Append(rowButton);
        }
        ScrollViewer referenceScroll;
        referenceScroll.MaxHeight(360);
        referenceScroll.VerticalScrollBarVisibility(ScrollBarVisibility::Auto);
        referenceScroll.VerticalScrollMode(ScrollMode::Enabled);
        AutomationProperties::SetAutomationId(referenceScroll, L"NativeHtmlEntitiesReferenceRows");
        referenceScroll.Content(m_htmlEntitiesReferenceRows);
        reference.Children().Append(referenceScroll);
        referenceCard.Child(reference);
        page.Children().Append(referenceCard);

        ShowPage(page);
        m_htmlEntitiesRendering = false;
        RefreshHtmlEntities();
    }

    void MainWindow::RefreshHtmlEntities()
    {
        using namespace winforge::core::referencetext;
        if (!m_htmlEntitiesOutputBox || !m_htmlEntitiesInputCount ||
            !m_htmlEntitiesOutputCount || !m_htmlEntitiesStatus) return;
        m_htmlEntitiesOutput = m_htmlEntitiesDecode
            ? DecodeHtmlEntities(m_htmlEntitiesInput)
            : EncodeHtmlEntities(m_htmlEntitiesInput, m_htmlEntitiesEscapeNonAscii);
        m_htmlEntitiesOutputBox.Text(ToHString(TextBoxPresentation(m_htmlEntitiesOutput)));
        if (m_htmlEntitiesNonAsciiBox)
        {
            m_htmlEntitiesNonAsciiBox.Visibility(
                m_htmlEntitiesDecode ? Visibility::Collapsed : Visibility::Visible);
        }
        auto const inputLength = HtmlEntityUtf16Length(m_htmlEntitiesInput);
        auto const outputLength = HtmlEntityUtf16Length(m_htmlEntitiesOutput);
        auto const inputCount = winforge::core::LocalizedText{
            std::to_wstring(inputLength) + L" characters in",
            L"輸入 " + std::to_wstring(inputLength) + L" 個字元" }.Pick(m_language);
        auto const outputCount = winforge::core::LocalizedText{
            std::to_wstring(outputLength) + L" characters out",
            L"輸出 " + std::to_wstring(outputLength) + L" 個字元" }.Pick(m_language);
        m_htmlEntitiesInputCount.Text(ToHString(inputCount));
        m_htmlEntitiesOutputCount.Text(ToHString(outputCount));
        AutomationProperties::SetName(m_htmlEntitiesInputCount, ToHString(inputCount));
        AutomationProperties::SetName(m_htmlEntitiesOutputCount, ToHString(outputCount));
        auto const status = winforge::core::LocalizedText{
            L"Converted locally — nothing leaves your PC.",
            L"已喺本機轉換 — 冇任何資料離開你部電腦。" }.Pick(m_language);
        AnnounceHtmlEntitiesStatus(status, false, false);
    }

    void MainWindow::AnnounceHtmlEntitiesStatus(
        std::wstring_view message,
        bool warning,
        bool announce)
    {
        if (!m_htmlEntitiesStatus) return;
        AutomationProperties::SetLiveSetting(
            m_htmlEntitiesStatus,
            announce
                ? Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite
                : Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Off);
        m_htmlEntitiesStatus.Text(ToHString(message));
        m_htmlEntitiesStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_htmlEntitiesStatus, ToHString(message));
        if (announce)
        {
            RaisePoliteLiveRegion(m_htmlEntitiesStatus);
        }
    }

    void MainWindow::RenderAspectRatio()
    {
        using namespace winforge::core::aspectratio;
        m_aspectRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{ L"Aspect Ratio", L"長寬比計算" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Simplify a resolution to its aspect ratio, or scale a size while keeping the ratio locked. Decimal ratio and megapixels are shown live.",
                L"將解析度化簡做長寬比，或者鎖住比例嚟縮放尺寸；小數比同百萬像素會即時顯示。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeAspectRatioPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native aspect-ratio math", L"全原生長寬比運算" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Midpoint-to-even simplification, GCD reduction, nine presets, scaling, megapixels, and copy formatting run locally in standard C++.",
            L"中點取偶數化簡、最大公因數約簡、九個預設、縮放、百萬像素同複製格式全部喺本機標準 C++ 執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeAspectRatioImplementationStatus");
        page.Children().Append(implementation);

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

        Border simplifyCard = makeCard();
        StackPanel simplify;
        simplify.Spacing(10);
        simplify.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Simplify a resolution", L"化簡解析度" }.Pick(m_language), 15, true));

        StackPanel dimensions;
        dimensions.Orientation(Orientation::Horizontal);
        dimensions.Spacing(10);
        auto widthLabel = CreateText(
            winforge::core::LocalizedText{ L"Width", L"闊度" }.Pick(m_language), 13.5, true);
        widthLabel.MinWidth(70);
        widthLabel.VerticalAlignment(VerticalAlignment::Center);
        dimensions.Children().Append(widthLabel);
        m_aspectWidth = NumberBox();
        m_aspectWidth.Minimum(0);
        m_aspectWidth.Maximum(100000);
        m_aspectWidth.Value(m_aspectWidthValue);
        m_aspectWidth.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_aspectWidth.MinWidth(150);
        AutomationProperties::SetAutomationId(m_aspectWidth, L"NativeAspectRatioWidth");
        AutomationProperties::SetName(m_aspectWidth, ToHString(winforge::core::LocalizedText{
            L"Resolution width", L"解析度闊度" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_aspectWidth, widthLabel);
        m_aspectWidth.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (m_aspectRendering) return;
            auto const value = sender.Value();
            m_aspectWidthValue = value;
            RefreshAspectRatio(true);
        });
        dimensions.Children().Append(m_aspectWidth);

        auto heightLabel = CreateText(
            winforge::core::LocalizedText{ L"Height", L"高度" }.Pick(m_language), 13.5, true);
        heightLabel.MinWidth(70);
        heightLabel.VerticalAlignment(VerticalAlignment::Center);
        dimensions.Children().Append(heightLabel);
        m_aspectHeight = NumberBox();
        m_aspectHeight.Minimum(0);
        m_aspectHeight.Maximum(100000);
        m_aspectHeight.Value(m_aspectHeightValue);
        m_aspectHeight.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
        m_aspectHeight.MinWidth(150);
        AutomationProperties::SetAutomationId(m_aspectHeight, L"NativeAspectRatioHeight");
        AutomationProperties::SetName(m_aspectHeight, ToHString(winforge::core::LocalizedText{
            L"Resolution height", L"解析度高度" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_aspectHeight, heightLabel);
        m_aspectHeight.ValueChanged([this](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
        {
            if (m_aspectRendering) return;
            auto const value = sender.Value();
            m_aspectHeightValue = value;
            RefreshAspectRatio(true);
        });
        dimensions.Children().Append(m_aspectHeight);
        simplify.Children().Append(dimensions);

        m_aspectRatio = CreateText(L"—", 22, true);
        AutomationProperties::SetAutomationId(m_aspectRatio, L"NativeAspectRatioRatio");
        simplify.Children().Append(m_aspectRatio);
        m_aspectDetail = CreateText(L"", 12.5);
        m_aspectDetail.Opacity(0.82);
        m_aspectDetail.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_aspectDetail, L"NativeAspectRatioDetail");
        simplify.Children().Append(m_aspectDetail);
        Button copy;
        copy.Content(box_value(ToHString(winforge::core::LocalizedText{
            L"Copy result", L"複製結果" }.Pick(m_language))));
        AutomationProperties::SetAutomationId(copy, L"NativeAspectRatioCopy");
        AutomationProperties::SetName(copy, ToHString(winforge::core::LocalizedText{
            L"Copy aspect-ratio result", L"複製長寬比結果" }.Pick(m_language)));
        copy.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            std::int64_t width{};
            std::int64_t height{};
            if (!winforge::core::aspectratio::Simplify(
                m_aspectWidthValue, m_aspectHeightValue, width, height))
            {
                AnnounceAspectStatus(winforge::core::LocalizedText{
                    L"Nothing to copy — enter a valid resolution.",
                    L"冇嘢可以複製 — 請輸入有效解析度。" }.Pick(m_language), true);
                return;
            }
            try
            {
                auto const decimal = FormatAspectNumber(
                    winforge::core::aspectratio::DecimalRatio(m_aspectWidthValue, m_aspectHeightValue), 4);
                auto const megapixels = FormatAspectNumber(
                    winforge::core::aspectratio::Megapixels(m_aspectWidthValue, m_aspectHeightValue), 2);
                auto const text = FormatAspectNumber(m_aspectWidthValue, 0) + L"×" +
                    FormatAspectNumber(m_aspectHeightValue, 0) +
                    L"  =  " + std::to_wstring(width) + L":" + std::to_wstring(height) +
                    L"  (" + decimal + L", " + megapixels + L" MP)";
                Windows::ApplicationModel::DataTransfer::DataPackage package;
                package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                package.SetText(ToHString(text));
                Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                AnnounceAspectStatus(winforge::core::LocalizedText{
                    L"Copied to clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
            }
            catch (...)
            {
                AnnounceAspectStatus(winforge::core::LocalizedText{
                    L"Could not copy to the clipboard.", L"複製到剪貼簿失敗。" }.Pick(m_language), true);
            }
        });
        simplify.Children().Append(copy);
        simplifyCard.Child(simplify);
        page.Children().Append(simplifyCard);

        Border presetCard = makeCard();
        StackPanel preset;
        preset.Spacing(10);
        preset.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Common presets", L"常用預設" }.Pick(m_language), 15, true));
        auto presetLabel = CreateText(
            winforge::core::LocalizedText{ L"Seed a ratio", L"載入比例" }.Pick(m_language), 13.5, true);
        preset.Children().Append(presetLabel);
        m_aspectPreset = ComboBox();
        static constexpr std::array<std::wstring_view, 9> labels{
            L"16:9", L"16:10", L"4:3", L"21:9", L"32:9", L"1:1", L"3:2", L"2:3", L"9:16" };
        for (auto const label : labels) m_aspectPreset.Items().Append(box_value(ToHString(label)));
        m_aspectPreset.SelectedIndex(m_aspectPresetIndex);
        m_aspectPreset.MinWidth(180);
        m_aspectPreset.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(m_aspectPreset, L"NativeAspectRatioPreset");
        AutomationProperties::SetName(m_aspectPreset, ToHString(winforge::core::LocalizedText{
            L"Common aspect-ratio preset", L"常用長寬比預設" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_aspectPreset, presetLabel);
        m_aspectPreset.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_aspectRendering) return;
            static constexpr std::array<std::pair<double, double>, 9> ratios{
                std::pair{ 16.0, 9.0 }, std::pair{ 16.0, 10.0 }, std::pair{ 4.0, 3.0 },
                std::pair{ 21.0, 9.0 }, std::pair{ 32.0, 9.0 }, std::pair{ 1.0, 1.0 },
                std::pair{ 3.0, 2.0 }, std::pair{ 2.0, 3.0 }, std::pair{ 9.0, 16.0 } };
            auto const index = sender.as<ComboBox>().SelectedIndex();
            if (index < 0 || static_cast<std::size_t>(index) >= ratios.size()) return;
            m_aspectPresetIndex = index;
            m_aspectRatioWidth = ratios[index].first;
            m_aspectRatioHeight = ratios[index].second;
            RefreshAspectScale();
            auto const label = std::to_wstring(static_cast<int>(ratios[index].first)) + L":" +
                std::to_wstring(static_cast<int>(ratios[index].second));
            AnnounceAspectStatus(winforge::core::LocalizedText{
                L"Ratio seeded to " + label + L".", L"已載入比例 " + label + L"。" }.Pick(m_language));
        });
        preset.Children().Append(m_aspectPreset);
        presetCard.Child(preset);
        page.Children().Append(presetCard);

        Border scaleCard = makeCard();
        StackPanel scale;
        scale.Spacing(10);
        scale.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Scale (ratio locked)", L"縮放（鎖定比例）" }.Pick(m_language), 15, true));
        auto scaleNote = CreateText(winforge::core::LocalizedText{
            L"Type a target width to get the matching height, or a target height to get the width — using the locked ratio above.",
            L"輸入目標闊度會計出對應高度，輸入目標高度就會計出闊度 — 用上面鎖定嘅比例。" }.Pick(m_language), 12);
        scaleNote.Opacity(0.82);
        scaleNote.TextWrapping(TextWrapping::Wrap);
        scale.Children().Append(scaleNote);

        auto addScaleRow = [this, &scale](
            NumberBox& box,
            TextBlock& output,
            double value,
            std::wstring_view label,
            std::wstring_view automation,
            bool widthInput)
        {
            StackPanel row;
            row.Orientation(Orientation::Horizontal);
            row.Spacing(10);
            auto caption = CreateText(label, 13.5, true);
            caption.MinWidth(130);
            caption.VerticalAlignment(VerticalAlignment::Center);
            row.Children().Append(caption);
            box = NumberBox();
            box.Minimum(0);
            box.Maximum(100000);
            box.Value(value);
            box.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Inline);
            box.MinWidth(150);
            AutomationProperties::SetAutomationId(box, ToHString(automation));
            AutomationProperties::SetName(box, ToHString(label));
            AutomationProperties::SetLabeledBy(box, caption);
            box.ValueChanged([this, widthInput](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
            {
                if (m_aspectRendering) return;
                auto const value = sender.Value();
                if (widthInput) m_aspectTargetWidthValue = value;
                else m_aspectTargetHeightValue = value;
                RefreshAspectScale();
            });
            row.Children().Append(box);
            output = CreateText(L"—", 13.5, true);
            output.VerticalAlignment(VerticalAlignment::Center);
            AutomationProperties::SetAutomationId(output, ToHString(std::wstring(automation) + L"Result"));
            row.Children().Append(output);
            scale.Children().Append(row);
        };
        addScaleRow(
            m_aspectTargetWidth,
            m_aspectScaledHeight,
            m_aspectTargetWidthValue,
            winforge::core::LocalizedText{ L"Target width", L"目標闊度" }.Pick(m_language),
            L"NativeAspectRatioTargetWidth",
            true);
        addScaleRow(
            m_aspectTargetHeight,
            m_aspectScaledWidth,
            m_aspectTargetHeightValue,
            winforge::core::LocalizedText{ L"Target height", L"目標高度" }.Pick(m_language),
            L"NativeAspectRatioTargetHeight",
            false);
        scaleCard.Child(scale);
        page.Children().Append(scaleCard);

        m_aspectStatus = CreateText(L"", 12.5);
        m_aspectStatus.Opacity(0.84);
        m_aspectStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_aspectStatus, L"NativeAspectRatioStatus");
        AutomationProperties::SetLiveSetting(
            m_aspectStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_aspectStatus);

        ShowPage(page);
        m_aspectRendering = false;
        RefreshAspectRatio(true);
    }

    void MainWindow::RefreshAspectRatio(bool adoptSimplifiedRatio)
    {
        using namespace winforge::core::aspectratio;
        if (!m_aspectRatio || !m_aspectDetail || !m_aspectStatus) return;

        std::int64_t width{};
        std::int64_t height{};
        if (!Simplify(m_aspectWidthValue, m_aspectHeightValue, width, height))
        {
            m_aspectRatio.Text(L"—");
            auto const detail = winforge::core::LocalizedText{
                L"Enter a positive width and height.", L"請輸入正數嘅闊度同高度。" }.Pick(m_language);
            m_aspectDetail.Text(ToHString(detail));
            AutomationProperties::SetName(m_aspectRatio, L"—");
            AutomationProperties::SetName(m_aspectDetail, ToHString(detail));
            AnnounceAspectStatus(winforge::core::LocalizedText{
                L"Waiting for a valid width and height.", L"等緊有效嘅闊度同高度。" }.Pick(m_language), true);
            RefreshAspectScale();
            return;
        }

        if (adoptSimplifiedRatio)
        {
            m_aspectRatioWidth = static_cast<double>(width);
            m_aspectRatioHeight = static_cast<double>(height);
            static constexpr std::array<std::pair<std::int64_t, std::int64_t>, 9> ratios{
                std::pair<std::int64_t, std::int64_t>{ 16, 9 }, { 16, 10 }, { 4, 3 },
                { 21, 9 }, { 32, 9 }, { 1, 1 }, { 3, 2 }, { 2, 3 }, { 9, 16 } };
            auto match = -1;
            for (std::size_t index{}; index < ratios.size(); ++index)
            {
                if (ratios[index].first == width && ratios[index].second == height)
                {
                    match = static_cast<int32_t>(index);
                    break;
                }
            }
            m_aspectPresetIndex = match;
            if (m_aspectPreset)
            {
                auto const previous = m_aspectRendering;
                m_aspectRendering = true;
                m_aspectPreset.SelectedIndex(match);
                m_aspectRendering = previous;
            }
        }

        auto const ratio = std::to_wstring(width) + L":" + std::to_wstring(height);
        m_aspectRatio.Text(ToHString(ratio));
        AutomationProperties::SetName(m_aspectRatio, ToHString(ratio));

        auto const decimal = FormatAspectNumber(
            DecimalRatio(m_aspectWidthValue, m_aspectHeightValue), 4);
        auto const megapixels = FormatAspectNumber(
            Megapixels(m_aspectWidthValue, m_aspectHeightValue), 2);
        auto const detail = winforge::core::LocalizedText{
            L"Decimal " + decimal + L" · " + megapixels + L" MP (" +
                FormatAspectNumber(m_aspectWidthValue, 0) + L"×" +
                FormatAspectNumber(m_aspectHeightValue, 0) + L")",
            L"小數 " + decimal + L" · " + megapixels + L" 百萬像素（" +
                FormatAspectNumber(m_aspectWidthValue, 0) + L"×" +
                FormatAspectNumber(m_aspectHeightValue, 0) + L"）" }.Pick(m_language);
        m_aspectDetail.Text(ToHString(detail));
        AutomationProperties::SetName(m_aspectDetail, ToHString(detail));
        AnnounceAspectStatus(winforge::core::LocalizedText{
            L"Ratio simplified.", L"比例已化簡。" }.Pick(m_language));
        RefreshAspectScale();
    }

    void MainWindow::RefreshAspectScale()
    {
        using namespace winforge::core::aspectratio;
        if (!m_aspectScaledHeight || !m_aspectScaledWidth) return;
        auto const height = HeightForWidth(
            m_aspectRatioWidth, m_aspectRatioHeight, m_aspectTargetWidthValue);
        auto const width = WidthForHeight(
            m_aspectRatioWidth, m_aspectRatioHeight, m_aspectTargetHeightValue);
        auto const heightText = std::isnan(height)
            ? std::wstring{ L"—" }
            : winforge::core::LocalizedText{
                L"→ height " + FormatAspectNumber(height, 2),
                L"→ 高度 " + FormatAspectNumber(height, 2) }.Pick(m_language);
        auto const widthText = std::isnan(width)
            ? std::wstring{ L"—" }
            : winforge::core::LocalizedText{
                L"→ width " + FormatAspectNumber(width, 2),
                L"→ 闊度 " + FormatAspectNumber(width, 2) }.Pick(m_language);
        m_aspectScaledHeight.Text(ToHString(heightText));
        m_aspectScaledWidth.Text(ToHString(widthText));
        AutomationProperties::SetName(m_aspectScaledHeight, ToHString(heightText));
        AutomationProperties::SetName(m_aspectScaledWidth, ToHString(widthText));
    }

    void MainWindow::AnnounceAspectStatus(std::wstring_view message, bool warning)
    {
        if (!m_aspectStatus) return;
        m_aspectStatus.Text(ToHString(message));
        m_aspectStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_aspectStatus, ToHString(message));
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_aspectStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_aspectStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility reporting must not interrupt local ratio math.
        }
    }

    void MainWindow::RenderCssUnits()
    {
        using namespace winforge::core::cssunits;
        m_cssRendering = true;

        auto page = CreatePage(
            winforge::core::LocalizedText{
                L"CSS Unit Converter", L"CSS 單位換算" }.Pick(m_language),
            winforge::core::LocalizedText{
                L"Convert one CSS length to every other supported unit. Absolute units use the CSS 96-DPI reference; relative units resolve against the local context below.",
                L"一次過將一個 CSS 長度換算成其他支援單位。絕對單位用 CSS 96-DPI 標準；相對單位就跟下面嘅本機內容脈絡計。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeCssUnitsPage");

        InfoBar implementation;
        implementation.IsOpen(true);
        implementation.IsClosable(false);
        implementation.Severity(InfoBarSeverity::Success);
        implementation.Title(ToHString(winforge::core::LocalizedText{
            L"Fully native CSS conversion", L"全原生 CSS 換算" }.Pick(m_language)));
        implementation.Message(ToHString(winforge::core::LocalizedText{
            L"Invariant parsing, all eleven CSS units, 96-DPI constants, five relative contexts, four-decimal formatting, and explicit copy actions run locally in standard C++.",
            L"不變文化剖析、全部十一個 CSS 單位、96-DPI 常數、五個相對內容脈絡、四位小數格式同明確複製動作全部喺本機標準 C++ 執行。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(implementation, L"NativeCssUnitsImplementationStatus");
        page.Children().Append(implementation);

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

        Border inputCard = makeCard();
        StackPanel input;
        input.Spacing(10);
        auto inputLabel = CreateText(
            winforge::core::LocalizedText{ L"Value to convert", L"要換算嘅數值" }.Pick(m_language), 15, true);
        input.Children().Append(inputLabel);
        StackPanel inputRow;
        inputRow.Orientation(Orientation::Horizontal);
        inputRow.Spacing(10);
        m_cssValue = TextBox();
        m_cssValue.Text(ToHString(m_cssInputValue));
        m_cssValue.MinWidth(220);
        m_cssValue.IsSpellCheckEnabled(false);
        m_cssValue.FontFamily(Media::FontFamily(L"Consolas"));
        AutomationProperties::SetAutomationId(m_cssValue, L"NativeCssUnitsValueInput");
        AutomationProperties::SetName(m_cssValue, ToHString(winforge::core::LocalizedText{
            L"CSS numeric value", L"CSS 數值" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_cssValue, inputLabel);
        m_cssValue.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_cssRendering) return;
            m_cssInputValue = ToWide(sender.as<TextBox>().Text());
            RefreshCssUnits();
        });
        inputRow.Children().Append(m_cssValue);

        m_cssUnit = ComboBox();
        for (auto const unit : Units) m_cssUnit.Items().Append(box_value(ToHString(unit)));
        m_cssUnit.SelectedIndex(m_cssUnitIndex);
        m_cssUnit.MinWidth(120);
        AutomationProperties::SetAutomationId(m_cssUnit, L"NativeCssUnitsUnitPicker");
        AutomationProperties::SetName(m_cssUnit, ToHString(winforge::core::LocalizedText{
            L"Source CSS unit", L"來源 CSS 單位" }.Pick(m_language)));
        m_cssUnit.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_cssRendering) return;
            auto const index = sender.as<ComboBox>().SelectedIndex();
            if (index < 0 || static_cast<std::size_t>(index) >= winforge::core::cssunits::Units.size()) return;
            m_cssUnitIndex = index;
            RefreshCssUnits();
        });
        inputRow.Children().Append(m_cssUnit);
        input.Children().Append(inputRow);
        inputCard.Child(input);
        page.Children().Append(inputCard);

        Border contextCard = makeCard();
        StackPanel context;
        context.Spacing(10);
        context.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Context", L"內容脈絡" }.Pick(m_language), 15, true));
        auto contextNote = CreateText(winforge::core::LocalizedText{
            L"em uses the element font-size; rem the root; % the container; vw and vh the viewport.",
            L"em 用元素字級；rem 用根字級；% 用容器；vw 同 vh 用視窗大細。" }.Pick(m_language), 12);
        contextNote.Opacity(0.82);
        contextNote.TextWrapping(TextWrapping::Wrap);
        context.Children().Append(contextNote);

        static const std::array<winforge::core::LocalizedText, 5> labels{
            winforge::core::LocalizedText{ L"Root font-size (px) — rem", L"根字級（px）— rem" },
            winforge::core::LocalizedText{ L"Element font-size (px) — em", L"元素字級（px）— em" },
            winforge::core::LocalizedText{ L"Viewport width (px) — vw", L"視窗闊度（px）— vw" },
            winforge::core::LocalizedText{ L"Viewport height (px) — vh", L"視窗高度（px）— vh" },
            winforge::core::LocalizedText{ L"Container size (px) — %", L"容器大細（px）— %" },
        };
        static constexpr std::array<std::wstring_view, 5> automation{
            L"NativeCssUnitsRoot", L"NativeCssUnitsElement", L"NativeCssUnitsViewportWidth",
            L"NativeCssUnitsViewportHeight", L"NativeCssUnitsContainer" };
        for (std::size_t index{}; index < m_cssContextBoxes.size(); ++index)
        {
            StackPanel field;
            field.Spacing(3);
            auto label = CreateText(labels[index].Pick(m_language), 12.5, true);
            field.Children().Append(label);
            auto& box = m_cssContextBoxes[index];
            box = NumberBox();
            box.Minimum(0);
            box.Value(m_cssContextValues[index]);
            box.SmallChange(index < 2 ? 1.0 : 10.0);
            box.SpinButtonPlacementMode(NumberBoxSpinButtonPlacementMode::Compact);
            box.MinWidth(230);
            box.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(box, ToHString(automation[index]));
            AutomationProperties::SetName(box, ToHString(labels[index].Pick(m_language)));
            AutomationProperties::SetLabeledBy(box, label);
            box.ValueChanged([this, index](NumberBox const& sender, NumberBoxValueChangedEventArgs const&)
            {
                if (m_cssRendering) return;
                auto const value = sender.Value();
                m_cssContextValues[index] = value;
                RefreshCssUnits();
            });
            field.Children().Append(box);
            context.Children().Append(field);
        }
        contextCard.Child(context);
        page.Children().Append(contextCard);

        Border resultsCard = makeCard();
        StackPanel results;
        results.Spacing(8);
        results.Children().Append(CreateText(
            winforge::core::LocalizedText{ L"Converted", L"換算結果" }.Pick(m_language), 15, true));
        auto hint = CreateText(winforge::core::LocalizedText{
            L"Select a result row to copy its complete CSS value.",
            L"揀一行結果就可以複製完整 CSS 數值。" }.Pick(m_language), 12);
        hint.Opacity(0.82);
        hint.TextWrapping(TextWrapping::Wrap);
        results.Children().Append(hint);
        m_cssResults = StackPanel();
        m_cssResults.Spacing(5);
        AutomationProperties::SetAutomationId(m_cssResults, L"NativeCssUnitsResults");
        results.Children().Append(m_cssResults);
        resultsCard.Child(results);
        page.Children().Append(resultsCard);

        m_cssStatus = CreateText(L"", 12.5);
        m_cssStatus.Opacity(0.84);
        m_cssStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_cssStatus, L"NativeCssUnitsStatus");
        AutomationProperties::SetLiveSetting(
            m_cssStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_cssStatus);

        ShowPage(page);
        m_cssRendering = false;
        RefreshCssUnits();
    }

    void MainWindow::RefreshCssUnits()
    {
        using namespace winforge::core::cssunits;
        if (!m_cssResults || !m_cssStatus) return;

        Context context;
        auto const contextValue = [this](std::size_t index, double fallback)
        {
            auto const value = m_cssContextValues[index];
            return std::isfinite(value) ? value : fallback;
        };
        context.rootFontPx = contextValue(0, 16.0);
        context.elementFontPx = contextValue(1, 16.0);
        context.viewportWidthPx = contextValue(2, 1920.0);
        context.viewportHeightPx = contextValue(3, 1080.0);
        context.containerPx = contextValue(4, 1000.0);
        auto const unitIndex = std::clamp<int32_t>(
            m_cssUnitIndex, 0, static_cast<int32_t>(Units.size() - 1));
        auto const unit = Units[static_cast<std::size_t>(unitIndex)];
        auto const value = Parse(m_cssInputValue);
        auto const rows = ConvertAll(value, unit, &context);
        m_cssResults.Children().Clear();

        auto automationSuffix = [](std::wstring_view unitValue)
        {
            if (unitValue == L"%") return std::wstring{ L"Percent" };
            auto result = std::wstring(unitValue);
            if (!result.empty()) result[0] = static_cast<wchar_t>(std::towupper(result[0]));
            return result;
        };
        for (auto const& converted : rows)
        {
            Button row;
            row.HorizontalAlignment(HorizontalAlignment::Stretch);
            row.HorizontalContentAlignment(HorizontalAlignment::Stretch);
            row.Padding(Thickness{ 12, 8, 12, 8 });
            StackPanel content;
            content.Orientation(Orientation::Horizontal);
            content.Spacing(18);
            auto unitText = CreateText(converted.unit, 13.5, true);
            unitText.MinWidth(72);
            content.Children().Append(unitText);
            auto valueText = CreateText(converted.value, 13.5);
            valueText.FontFamily(Media::FontFamily(L"Consolas"));
            content.Children().Append(valueText);
            row.Content(content);
            row.IsEnabled(!converted.combined.empty());
            AutomationProperties::SetAutomationId(
                row,
                ToHString(L"NativeCssUnitsResult" + automationSuffix(converted.unit)));
            AutomationProperties::SetName(row, ToHString(converted.combined.empty()
                ? winforge::core::LocalizedText{
                    converted.unit + L" unavailable",
                    converted.unit + L" 無法換算" }.Pick(m_language)
                : winforge::core::LocalizedText{
                    L"Copy " + converted.combined,
                    L"複製 " + converted.combined }.Pick(m_language)));
            row.Click([this, copyValue = converted.combined](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                if (copyValue.empty()) return;
                try
                {
                    Windows::ApplicationModel::DataTransfer::DataPackage package;
                    package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                    package.SetText(ToHString(copyValue));
                    Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                    AnnounceCssStatus(winforge::core::LocalizedText{
                        L"Copied " + copyValue, L"已複製 " + copyValue }.Pick(m_language));
                }
                catch (...)
                {
                    AnnounceCssStatus(winforge::core::LocalizedText{
                        L"Could not access the clipboard.", L"無法存取剪貼簿。" }.Pick(m_language), true);
                }
            });
            m_cssResults.Children().Append(row);
        }

        AnnounceCssStatus(!std::isfinite(value)
            ? winforge::core::LocalizedText{
                L"Enter a valid invariant number to convert.", L"請輸入有效嘅不變文化數字嚟換算。" }.Pick(m_language)
            : winforge::core::LocalizedText{
                L"Select a result row to copy it.", L"揀一行結果就可以複製。" }.Pick(m_language),
            !std::isfinite(value));
    }

    void MainWindow::AnnounceCssStatus(std::wstring_view message, bool warning)
    {
        if (!m_cssStatus) return;
        m_cssStatus.Text(ToHString(message));
        m_cssStatus.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_cssStatus, ToHString(message));
        try
        {
            auto peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::FromElement(
                m_cssStatus);
            if (!peer)
            {
                peer = Microsoft::UI::Xaml::Automation::Peers::FrameworkElementAutomationPeer::CreatePeerForElement(
                    m_cssStatus);
            }
            if (peer)
            {
                peer.RaiseAutomationEvent(
                    Microsoft::UI::Xaml::Automation::Peers::AutomationEvents::LiveRegionChanged);
            }
        }
        catch (...)
        {
            // Accessibility reporting must not interrupt local CSS conversion.
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

    void MainWindow::RenderUuidV5()
    {
        using winforge::core::LocalizedText;

        m_uuidV5Rendering = true;
        auto page = CreatePage(
            LocalizedText{ L"Namespaced UUID", L"具名空間 UUID" }.Pick(m_language),
            LocalizedText{
                L"Generate deterministic RFC 4122 name-based UUIDs. The same namespace, UTF-8 name, and version always produce the same local result.",
                L"產生穩定嘅 RFC 4122 具名 UUID；同一個命名空間、UTF-8 名同版本，永遠會喺本機得出同一結果。" }.Pick(m_language));
        page.MaxWidth(900);
        page.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(page, L"NativeUuidV5Page");

        InfoBar nativeStatus;
        nativeStatus.IsOpen(true);
        nativeStatus.IsClosable(false);
        nativeStatus.Severity(InfoBarSeverity::Success);
        nativeStatus.Title(ToHString(LocalizedText{
            L"Fully native name-based UUID generation", L"全原生具名 UUID 產生" }.Pick(m_language)));
        nativeStatus.Message(ToHString(LocalizedText{
            L"C++ hashes RFC network-order namespace bytes plus the name's UTF-8 bytes with SHA-1 (v5) or MD5 (v3), then sets the RFC version and variant bits. Results stay local and the clipboard changes only after an explicit Copy.",
            L"C++ 會將 RFC network-order 命名空間位元組加名稱嘅 UTF-8 位元組，用 SHA-1（v5）或者 MD5（v3）雜湊，再設定 RFC 版本同變體位元。結果只留喺本機，只有明確撳 Copy 先會改剪貼簿。" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(nativeStatus, L"NativeUuidV5ImplementationStatus");
        page.Children().Append(nativeStatus);

        Border optionsCard = MakeNativeCard();
        StackPanel options;
        options.Spacing(10);
        options.Children().Append(CreateText(
            LocalizedText{ L"Namespace and name", L"命名空間同名稱" }.Pick(m_language), 15, true));

        auto namespaceLabel = CreateText(
            LocalizedText{ L"Namespace", L"命名空間" }.Pick(m_language), 14, true);
        options.Children().Append(namespaceLabel);
        m_uuidV5NamespacePicker = ComboBox();
        m_uuidV5NamespacePicker.MinWidth(280);
        m_uuidV5NamespacePicker.MaxWidth(760);
        m_uuidV5NamespacePicker.HorizontalAlignment(HorizontalAlignment::Stretch);
        for (auto const& option : std::array<LocalizedText, 5>{
            LocalizedText{ L"DNS  ·  6ba7b810-9dad-11d1-80b4-00c04fd430c8", L"DNS  ·  6ba7b810-9dad-11d1-80b4-00c04fd430c8" },
            LocalizedText{ L"URL  ·  6ba7b811-9dad-11d1-80b4-00c04fd430c8", L"URL  ·  6ba7b811-9dad-11d1-80b4-00c04fd430c8" },
            LocalizedText{ L"OID  ·  6ba7b812-9dad-11d1-80b4-00c04fd430c8", L"OID  ·  6ba7b812-9dad-11d1-80b4-00c04fd430c8" },
            LocalizedText{ L"X500  ·  6ba7b814-9dad-11d1-80b4-00c04fd430c8", L"X500  ·  6ba7b814-9dad-11d1-80b4-00c04fd430c8" },
            LocalizedText{ L"Custom namespace", L"自訂命名空間" },
        })
        {
            m_uuidV5NamespacePicker.Items().Append(box_value(ToHString(option.Pick(m_language))));
        }
        m_uuidV5NamespacePicker.SelectedIndex(std::clamp(m_uuidV5NamespaceIndex, 0, 4));
        AutomationProperties::SetAutomationId(m_uuidV5NamespacePicker, L"NativeUuidV5Namespace");
        AutomationProperties::SetName(m_uuidV5NamespacePicker, ToHString(LocalizedText{
            L"UUID namespace", L"UUID 命名空間" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV5NamespacePicker, namespaceLabel);
        options.Children().Append(m_uuidV5NamespacePicker);

        m_uuidV5CustomNamespaceInput = TextBox();
        m_uuidV5CustomNamespaceInput.IsSpellCheckEnabled(false);
        m_uuidV5CustomNamespaceInput.FontFamily(Media::FontFamily(L"Consolas"));
        m_uuidV5CustomNamespaceInput.PlaceholderText(L"00000000-0000-0000-0000-000000000000");
        m_uuidV5CustomNamespaceInput.Text(ToHString(m_uuidV5CustomNamespaceValue));
        m_uuidV5CustomNamespaceInput.Visibility(m_uuidV5NamespaceIndex == 4
            ? Visibility::Visible
            : Visibility::Collapsed);
        AutomationProperties::SetAutomationId(m_uuidV5CustomNamespaceInput, L"NativeUuidV5CustomNamespace");
        AutomationProperties::SetName(m_uuidV5CustomNamespaceInput, ToHString(LocalizedText{
            L"Custom UUID namespace", L"自訂 UUID 命名空間" }.Pick(m_language)));
        options.Children().Append(m_uuidV5CustomNamespaceInput);

        auto versionLabel = CreateText(
            LocalizedText{ L"Version", L"版本" }.Pick(m_language), 14, true);
        options.Children().Append(versionLabel);
        m_uuidV5VersionPicker = ComboBox();
        m_uuidV5VersionPicker.MinWidth(220);
        m_uuidV5VersionPicker.MaxWidth(420);
        m_uuidV5VersionPicker.HorizontalAlignment(HorizontalAlignment::Left);
        m_uuidV5VersionPicker.Items().Append(box_value(ToHString(LocalizedText{
            L"v5  ·  SHA-1", L"v5  ·  SHA-1" }.Pick(m_language))));
        m_uuidV5VersionPicker.Items().Append(box_value(ToHString(LocalizedText{
            L"v3  ·  MD5", L"v3  ·  MD5" }.Pick(m_language))));
        m_uuidV5VersionPicker.SelectedIndex(std::clamp(m_uuidV5VersionIndex, 0, 1));
        AutomationProperties::SetAutomationId(m_uuidV5VersionPicker, L"NativeUuidV5Version");
        AutomationProperties::SetName(m_uuidV5VersionPicker, ToHString(LocalizedText{
            L"Name-based UUID version", L"具名 UUID 版本" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV5VersionPicker, versionLabel);
        options.Children().Append(m_uuidV5VersionPicker);

        auto nameLabel = CreateText(
            LocalizedText{ L"Name", L"名稱" }.Pick(m_language), 14, true);
        options.Children().Append(nameLabel);
        m_uuidV5NameInput = TextBox();
        m_uuidV5NameInput.Text(ToHString(m_uuidV5NameValue));
        m_uuidV5NameInput.TextWrapping(TextWrapping::Wrap);
        m_uuidV5NameInput.PlaceholderText(ToHString(LocalizedText{
            L"For example: www.example.com", L"例如：www.example.com" }.Pick(m_language)));
        AutomationProperties::SetAutomationId(m_uuidV5NameInput, L"NativeUuidV5Name");
        AutomationProperties::SetName(m_uuidV5NameInput, ToHString(LocalizedText{
            L"Name to hash into a UUID", L"要雜湊成 UUID 嘅名稱" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV5NameInput, nameLabel);
        options.Children().Append(m_uuidV5NameInput);

        auto resultLabel = CreateText(
            LocalizedText{ L"Deterministic result", L"穩定結果" }.Pick(m_language), 14, true);
        options.Children().Append(resultLabel);
        m_uuidV5ResultOutput = TextBox();
        m_uuidV5ResultOutput.IsReadOnly(true);
        m_uuidV5ResultOutput.IsSpellCheckEnabled(false);
        m_uuidV5ResultOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_uuidV5ResultOutput.Text(ToHString(m_uuidV5ResultValue));
        AutomationProperties::SetAutomationId(m_uuidV5ResultOutput, L"NativeUuidV5Result");
        AutomationProperties::SetName(m_uuidV5ResultOutput, ToHString(LocalizedText{
            L"Generated namespaced UUID", L"已產生嘅具名 UUID" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV5ResultOutput, resultLabel);
        options.Children().Append(m_uuidV5ResultOutput);

        auto copyResult = Button();
        copyResult.Content(box_value(ToHString(LocalizedText{ L"Copy", L"複製" }.Pick(m_language))));
        copyResult.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(copyResult, L"NativeUuidV5Copy");
        AutomationProperties::SetName(copyResult, ToHString(LocalizedText{
            L"Copy generated namespaced UUID", L"複製已產生嘅具名 UUID" }.Pick(m_language)));
        copyResult.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUuidV5Value(
                m_uuidV5ResultValue,
                winforge::core::LocalizedText{ L"Copied to clipboard.", L"已複製到剪貼簿。" }.Pick(m_language));
        });
        options.Children().Append(copyResult);
        optionsCard.Child(options);
        page.Children().Append(optionsCard);

        Border bulkCard = MakeNativeCard();
        StackPanel bulk;
        bulk.Spacing(10);
        bulk.Children().Append(CreateText(
            LocalizedText{ L"Bulk mode", L"批量模式" }.Pick(m_language), 15, true));
        bulk.Children().Append(CreateText(
            LocalizedText{
                L"One name per line. Blank lines are skipped; each remaining name uses the namespace and version above.",
                L"每行一個名；空白行會略過，其餘每個名都會用上面嘅命名空間同版本。" }.Pick(m_language), 12));

        auto bulkInputLabel = CreateText(
            LocalizedText{ L"Names", L"名稱" }.Pick(m_language), 14, true);
        bulk.Children().Append(bulkInputLabel);
        m_uuidV5BulkInput = TextBox();
        m_uuidV5BulkInput.AcceptsReturn(true);
        m_uuidV5BulkInput.TextWrapping(TextWrapping::Wrap);
        m_uuidV5BulkInput.MinHeight(112);
        m_uuidV5BulkInput.MaxHeight(200);
        m_uuidV5BulkInput.Text(ToHString(TextBoxPresentation(m_uuidV5BulkInputValue)));
        AutomationProperties::SetAutomationId(m_uuidV5BulkInput, L"NativeUuidV5BulkInput");
        AutomationProperties::SetName(m_uuidV5BulkInput, ToHString(LocalizedText{
            L"One UUID name per line", L"每行一個 UUID 名稱" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV5BulkInput, bulkInputLabel);
        bulk.Children().Append(m_uuidV5BulkInput);

        auto generateBulk = Button();
        generateBulk.Content(box_value(ToHString(LocalizedText{ L"Generate", L"生成" }.Pick(m_language))));
        generateBulk.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(generateBulk, L"NativeUuidV5BulkGenerate");
        AutomationProperties::SetName(generateBulk, ToHString(LocalizedText{
            L"Generate one namespaced UUID per nonblank line", L"每個非空白行生成一個具名 UUID" }.Pick(m_language)));
        generateBulk.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            GenerateUuidV5Bulk();
        });
        bulk.Children().Append(generateBulk);

        auto bulkOutputLabel = CreateText(
            LocalizedText{ L"Generated rows", L"已生成列" }.Pick(m_language), 14, true);
        bulk.Children().Append(bulkOutputLabel);
        m_uuidV5BulkOutput = TextBox();
        m_uuidV5BulkOutput.IsReadOnly(true);
        m_uuidV5BulkOutput.IsSpellCheckEnabled(false);
        m_uuidV5BulkOutput.AcceptsReturn(true);
        m_uuidV5BulkOutput.TextWrapping(TextWrapping::NoWrap);
        m_uuidV5BulkOutput.FontFamily(Media::FontFamily(L"Consolas"));
        m_uuidV5BulkOutput.Height(200);
        m_uuidV5BulkOutput.Text(ToHString(TextBoxPresentation(m_uuidV5BulkOutputValue)));
        AutomationProperties::SetAutomationId(m_uuidV5BulkOutput, L"NativeUuidV5BulkOutput");
        AutomationProperties::SetName(m_uuidV5BulkOutput, ToHString(LocalizedText{
            L"Generated UUID bulk rows", L"已產生嘅 UUID 批量列" }.Pick(m_language)));
        AutomationProperties::SetLabeledBy(m_uuidV5BulkOutput, bulkOutputLabel);
        bulk.Children().Append(m_uuidV5BulkOutput);

        auto copyBulk = Button();
        copyBulk.Content(box_value(ToHString(LocalizedText{ L"Copy all", L"全部複製" }.Pick(m_language))));
        copyBulk.HorizontalAlignment(HorizontalAlignment::Left);
        AutomationProperties::SetAutomationId(copyBulk, L"NativeUuidV5BulkCopy");
        AutomationProperties::SetName(copyBulk, ToHString(LocalizedText{
            L"Copy every generated UUID row", L"複製所有已產生 UUID 列" }.Pick(m_language)));
        copyBulk.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            CopyUuidV5Value(
                m_uuidV5BulkOutputValue,
                winforge::core::LocalizedText{ L"All rows copied.", L"已全部複製。" }.Pick(m_language));
        });
        bulk.Children().Append(copyBulk);
        bulkCard.Child(bulk);
        page.Children().Append(bulkCard);

        m_uuidV5Status = CreateText(L"", 12.5);
        m_uuidV5Status.TextWrapping(TextWrapping::Wrap);
        m_uuidV5Status.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_uuidV5Status, L"NativeUuidV5Status");
        AutomationProperties::SetLiveSetting(
            m_uuidV5Status,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Off);
        page.Children().Append(m_uuidV5Status);

        m_uuidV5NamespacePicker.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_uuidV5Rendering) return;
            m_uuidV5NamespaceIndex = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 4);
            if (m_uuidV5CustomNamespaceInput)
            {
                m_uuidV5CustomNamespaceInput.Visibility(m_uuidV5NamespaceIndex == 4
                    ? Visibility::Visible
                    : Visibility::Collapsed);
            }
            RefreshUuidV5();
        });
        m_uuidV5VersionPicker.SelectionChanged([this](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_uuidV5Rendering) return;
            m_uuidV5VersionIndex = std::clamp(sender.as<ComboBox>().SelectedIndex(), 0, 1);
            RefreshUuidV5();
        });
        m_uuidV5CustomNamespaceInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_uuidV5Rendering) return;
            m_uuidV5CustomNamespaceValue = ToWide(sender.as<TextBox>().Text());
            if (m_uuidV5NamespaceIndex == 4)
            {
                RefreshUuidV5();
            }
        });
        m_uuidV5NameInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_uuidV5Rendering) return;
            m_uuidV5NameValue = ToWide(sender.as<TextBox>().Text());
            RefreshUuidV5();
        });
        m_uuidV5BulkInput.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_uuidV5Rendering) return;
            m_uuidV5BulkInputValue = ToWide(sender.as<TextBox>().Text());
        });

        ShowPage(page);
        m_uuidV5Rendering = false;
        RefreshUuidV5();
    }

    void MainWindow::RefreshUuidV5()
    {
        if (!m_uuidV5ResultOutput || !m_uuidV5Status)
        {
            return;
        }

        winforge::core::uuidv5::NamespaceBytes nameSpace{};
        bool namespaceIsValid = true;
        switch (m_uuidV5NamespaceIndex)
        {
        case 0:
            nameSpace = winforge::core::uuidv5::NamespaceDns;
            break;
        case 1:
            nameSpace = winforge::core::uuidv5::NamespaceUrl;
            break;
        case 2:
            nameSpace = winforge::core::uuidv5::NamespaceOid;
            break;
        case 3:
            nameSpace = winforge::core::uuidv5::NamespaceX500;
            break;
        default:
            namespaceIsValid = winforge::core::uuidv5::TryParseNamespace(
                m_uuidV5CustomNamespaceValue,
                nameSpace);
            break;
        }

        if (!namespaceIsValid)
        {
            m_uuidV5ResultValue.clear();
            m_uuidV5ResultOutput.Text(L"");
            AutomationProperties::SetHelpText(m_uuidV5ResultOutput, L"");
            AnnounceUuidV5Status(winforge::core::LocalizedText{
                L"Enter a valid custom namespace GUID (e.g. 00000000-0000-0000-0000-000000000000).",
                L"請輸入有效嘅自訂命名空間 GUID（例如 00000000-0000-0000-0000-000000000000）。" }.Pick(m_language), true);
            return;
        }

        auto const version = m_uuidV5VersionIndex == 1
            ? winforge::core::uuidv5::Version::V3
            : winforge::core::uuidv5::Version::V5;
        auto const computed = winforge::core::uuidv5::Compute(nameSpace, m_uuidV5NameValue, version);
        if (!computed.ok)
        {
            m_uuidV5ResultValue.clear();
            m_uuidV5ResultOutput.Text(L"");
            AutomationProperties::SetHelpText(m_uuidV5ResultOutput, L"");
            AnnounceUuidV5Status(winforge::core::LocalizedText{
                L"Could not compute — check the inputs.",
                L"整唔到 — 請檢查輸入。" }.Pick(m_language), true);
            return;
        }

        m_uuidV5ResultValue = computed.uuid;
        m_uuidV5ResultOutput.Text(ToHString(m_uuidV5ResultValue));
        AutomationProperties::SetHelpText(m_uuidV5ResultOutput, ToHString(m_uuidV5ResultValue));
        auto const versionText = version == winforge::core::uuidv5::Version::V3 ? L"3" : L"5";
        AnnounceUuidV5Status(winforge::core::LocalizedText{
            L"UUID v" + std::wstring(versionText) + L" — deterministic for this namespace + name.",
            L"UUID v" + std::wstring(versionText) + L" — 呢個命名空間加名嘅穩定結果。" }.Pick(m_language));
    }

    void MainWindow::GenerateUuidV5Bulk()
    {
        winforge::core::uuidv5::NamespaceBytes nameSpace{};
        bool namespaceIsValid = true;
        switch (m_uuidV5NamespaceIndex)
        {
        case 0:
            nameSpace = winforge::core::uuidv5::NamespaceDns;
            break;
        case 1:
            nameSpace = winforge::core::uuidv5::NamespaceUrl;
            break;
        case 2:
            nameSpace = winforge::core::uuidv5::NamespaceOid;
            break;
        case 3:
            nameSpace = winforge::core::uuidv5::NamespaceX500;
            break;
        default:
            namespaceIsValid = winforge::core::uuidv5::TryParseNamespace(
                m_uuidV5CustomNamespaceValue,
                nameSpace);
            break;
        }

        if (!namespaceIsValid)
        {
            m_uuidV5BulkOutputValue.clear();
            if (m_uuidV5BulkOutput)
            {
                m_uuidV5BulkOutput.Text(L"");
                AutomationProperties::SetHelpText(m_uuidV5BulkOutput, L"");
            }
            AnnounceUuidV5Status(winforge::core::LocalizedText{
                L"Enter a valid custom namespace GUID first.",
                L"請先輸入有效嘅自訂命名空間 GUID。" }.Pick(m_language), true, true);
            return;
        }

        try
        {
            auto const version = m_uuidV5VersionIndex == 1
                ? winforge::core::uuidv5::Version::V3
                : winforge::core::uuidv5::Version::V5;
            auto const rows = winforge::core::uuidv5::ComputeBulk(
                nameSpace,
                m_uuidV5BulkInputValue,
                version);
            m_uuidV5BulkOutputValue.clear();
            for (std::size_t index{}; index < rows.size(); ++index)
            {
                if (index > 0)
                {
                    m_uuidV5BulkOutputValue += L"\r\n";
                }
                m_uuidV5BulkOutputValue += rows[index];
            }
            if (m_uuidV5BulkOutput)
            {
                m_uuidV5BulkOutput.Text(ToHString(TextBoxPresentation(m_uuidV5BulkOutputValue)));
                AutomationProperties::SetHelpText(m_uuidV5BulkOutput, ToHString(m_uuidV5BulkOutputValue));
            }
            AnnounceUuidV5Status(winforge::core::LocalizedText{
                L"Generated " + std::to_wstring(rows.size()) + L" UUID(s).",
                L"已生成 " + std::to_wstring(rows.size()) + L" 個 UUID。" }.Pick(m_language), false, true);
        }
        catch (...)
        {
            m_uuidV5BulkOutputValue.clear();
            if (m_uuidV5BulkOutput)
            {
                m_uuidV5BulkOutput.Text(L"");
                AutomationProperties::SetHelpText(m_uuidV5BulkOutput, L"");
            }
            AnnounceUuidV5Status(winforge::core::LocalizedText{
                L"Bulk generation failed.", L"批量生成失敗。" }.Pick(m_language), true, true);
        }
    }

    void MainWindow::CopyUuidV5Value(std::wstring_view value, std::wstring_view successMessage)
    {
        if (value.empty())
        {
            return;
        }

        try
        {
            Windows::ApplicationModel::DataTransfer::DataPackage package;
            package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
            package.SetText(ToHString(value));
            Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
            AnnounceUuidV5Status(successMessage, false, true);
        }
        catch (...)
        {
            AnnounceUuidV5Status(winforge::core::LocalizedText{
                L"Copy failed.", L"複製失敗。" }.Pick(m_language), true, true);
        }
    }

    void MainWindow::AnnounceUuidV5Status(std::wstring_view message, bool warning, bool announce)
    {
        if (!m_uuidV5Status) return;

        m_uuidV5Status.Text(ToHString(message));
        m_uuidV5Status.Foreground(Application::Current().Resources().Lookup(
            box_value(warning ? L"SystemFillColorCautionBrush" : L"TextFillColorSecondaryBrush")).as<Media::Brush>());
        AutomationProperties::SetName(m_uuidV5Status, ToHString(message));
        AutomationProperties::SetLiveSetting(
            m_uuidV5Status,
            announce
                ? Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite
                : Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Off);
        if (announce)
        {
            RaisePoliteLiveRegion(m_uuidV5Status);
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
        m_packageDiscoverRegexIgnorePatternWhitespace = false;
        m_packageDiscoverRegexExplicitCapture = false;
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
                m_packageDiscoverRegexIgnorePatternWhitespace = root.GetNamedBoolean(
                    L"discoverRegexIgnorePatternWhitespace",
                    false);
                m_packageDiscoverRegexExplicitCapture = root.GetNamedBoolean(
                    L"discoverRegexExplicitCapture",
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
                L"discoverRegexIgnorePatternWhitespace",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(
                    m_packageDiscoverRegexIgnorePatternWhitespace));
            root.SetNamedValue(
                L"discoverRegexExplicitCapture",
                winrt::Windows::Data::Json::JsonValue::CreateBooleanValue(
                    m_packageDiscoverRegexExplicitCapture));
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
            m_packageDiscoverRegexIgnorePatternWhitespace = false;
            m_packageDiscoverRegexExplicitCapture = false;
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
                    diagnostic,
                    m_packageDiscoverRegexIgnorePatternWhitespace,
                    m_packageDiscoverRegexExplicitCapture);
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
                    diagnostic,
                    m_packageDiscoverRegexIgnorePatternWhitespace,
                    m_packageDiscoverRegexExplicitCapture);
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
                diagnostic,
                m_packageDiscoverRegexIgnorePatternWhitespace,
                m_packageDiscoverRegexExplicitCapture);
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
                    diagnostic,
                    m_packageDiscoverRegexIgnorePatternWhitespace,
                    m_packageDiscoverRegexExplicitCapture);
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
                diagnostic,
                m_packageDiscoverRegexIgnorePatternWhitespace,
                m_packageDiscoverRegexExplicitCapture);
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
            confirm.Content(box_value(ToHString(pick(L"Confirm uninstall", L"Confirm \u89e3\u9664\u5b89\u88dd"))));
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
                diagnostic,
                m_allAppsRegexIgnorePatternWhitespace,
                m_allAppsRegexExplicitCapture);
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
            auto const implementation = winforge::core::HasNativeRenderer(module.id)
                ? L"native implementation available / 原生實作可用"
                : L"native implementation pending / 原生實作待完成";
            auto metadata = module.id + L"  ·  " + module.kind + L"  ·  " + implementation;
            auto metadataText = CreateText(metadata, 11);
            metadataText.Opacity(0.68);
            row.Children().Append(metadataText);

            ListViewItem item;
            item.Content(row);
            item.Tag(box_value(ToHString(module.id)));
            item.Padding(Thickness{ 12, 9, 12, 9 });
            AutomationProperties::SetAutomationId(item, ToHString(L"NativeAllApps_" + AutomationKey(module.id)));
            AutomationProperties::SetName(
                item,
                ToHString(Label(module) + L" · " + metadata));
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
                diagnostic,
                m_shellRegexIgnorePatternWhitespace,
                m_shellRegexExplicitCapture);
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

    void MainWindow::RenderSymbolsPalette()
    {
        auto const pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };

        m_symbolsRendering = true;
        auto page = CreatePage(
            pick(L"Symbols Palette", L"\u7279\u6B8A\u7B26\u865F\u8ABF\u8272\u76E4"),
            pick(
                L"A local palette of arrows, maths, currency, Greek, box-drawing and more. Choose a category or search by name, then explicitly copy one symbol.",
                L"\u672C\u6a5f\u7279\u6b8a\u7b26\u865f\u8abf\u8272\u76e4\uff0c\u6709\u7bad\u5634\u3001\u6578\u5b78\u3001\u8ca8\u5e63\u3001\u5e0c\u81d8\u5b57\u6bcd\u3001\u6846\u7dda\u7b49\u7b49\u3002\u63c0\u500b\u5206\u985e\u6216\u8005\u7528\u540d\u641c\u5c0b\uff0c\u518d\u64f3\u8907\u88fd\u3002"));
        AutomationProperties::SetAutomationId(page, L"NativeSymbolsPage");

        InfoBar safety;
        safety.IsOpen(true);
        safety.IsClosable(false);
        safety.Severity(InfoBarSeverity::Informational);
        safety.Title(ToHString(pick(L"Static local catalog", L"\u975c\u614b\u672c\u6a5f\u76ee\u9304")));
        safety.Message(ToHString(pick(
            L"Search and Regex mode inspect only the returned in-memory package cache. A row action opens a review; no package changes until Confirm. Removal is disabled at elevated or unsafe integrity, and this migration slice never deletes local data folders.",
            L"\u641c\u5c0b\u540c Regex \u6a21\u5f0f\u53ea\u6703\u6aa2\u67e5\u5df2\u8fd4\u56de\u7684\u672c\u6a5f\u8a18\u61b6\u9ad4\u5957\u4ef6\u7de9\u5b58\u3002\u64f3\u5217\u52d5\u4f5c\u53ea\u6703\u958b\u8986\u6838\uff1b\u672a\u6309 Confirm \u4e4b\u524d\u5514\u6703\u6539\u8b8a\u5957\u4ef6\u3002\u5982\u679c\u5df2\u63d0\u5347\u6216 integrity \u4e0d\u5b89\u5168\u5c31\u6703\u7981\u7528\u79fb\u9664\uff0c\u800c\u4e14\u9019\u500b migration slice \u7d55\u4e0d\u6703\u522a\u9664\u672c\u6a5f\u8cc7\u6599\u76ee\u9304\u3002")));
        AutomationProperties::SetAutomationId(safety, L"NativeSymbolsSafety");
        page.Children().Append(safety);

        m_symbolsSearchBox = TextBox();
        m_symbolsSearchBox.Header(box_value(ToHString(pick(L"Search symbols by name or glyph", L"\u7528\u540d\u6216\u7b26\u865f\u641c\u5c0b"))));
        m_symbolsSearchBox.PlaceholderText(ToHString(pick(
            L"Literal text by default; enable Regex mode for bounded PCRE2",
            L"\u9810\u8a2d\u4fc2\u6587\u5b57\u641c\u5c0b\uff1b\u958b Regex \u6a21\u5f0f\u5148\u6703\u7528\u9650\u5236\u5de6\u7684 PCRE2")));
        m_symbolsSearchBox.Text(ToHString(m_symbolsSearchText));
        AutomationProperties::SetAutomationId(m_symbolsSearchBox, L"NativeSymbolsSearch");
        AutomationProperties::SetName(m_symbolsSearchBox, ToHString(pick(L"Symbols Palette local search", L"\u7279\u6b8a\u7b26\u865f\u8abf\u8272\u76e4\u672c\u6a5f\u641c\u5c0b")));
        m_symbolsSearchBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            if (m_symbolsRendering) return;
            m_symbolsSearchText = ToWide(sender.as<TextBox>().Text());
            RefreshSymbolsPaletteEntries();
        });
        page.Children().Append(m_symbolsSearchBox);

        m_symbolsRegexMode = ToggleSwitch();
        m_symbolsRegexMode.Header(box_value(ToHString(pick(
            L"Regex mode (bounded PCRE2 local filter)",
            L"Regex \u6a21\u5f0f\uff08\u6709\u9650\u5236\u7684 PCRE2 \u672c\u6a5f\u7be9\u9078\uff09"))));
        m_symbolsRegexMode.IsOn(m_symbolsRegexEnabled);
        AutomationProperties::SetAutomationId(m_symbolsRegexMode, L"NativeSymbolsRegexMode");
        m_symbolsRegexMode.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            if (m_symbolsRendering) return;
            m_symbolsRegexEnabled = sender.as<ToggleSwitch>().IsOn();
            RefreshSymbolsPaletteEntries();
        });
        page.Children().Append(m_symbolsRegexMode);

        m_symbolsCategoryPicker = ComboBox();
        m_symbolsCategoryPicker.Header(box_value(ToHString(pick(L"Symbol category", L"\u7b26\u865f\u5206\u985e"))));
        ComboBoxItem allCategories;
        allCategories.Content(box_value(ToHString(pick(L"All categories", L"\u5168\u90e8\u5206\u985e"))));
        m_symbolsCategoryPicker.Items().Append(allCategories);
        auto const categories = winforge::core::symbols::SymbolsCategories();
        int32_t categoryIndex = 0;
        for (std::size_t index = 0; index < categories.size(); ++index)
        {
            auto const& category = categories[index];
            ComboBoxItem item;
            item.Content(box_value(ToHString(pick(category.name_en, category.name_zh))));
            m_symbolsCategoryPicker.Items().Append(item);
            if (category.key == m_symbolsCategoryKey)
            {
                categoryIndex = static_cast<int32_t>(index + 1);
            }
        }
        m_symbolsCategoryPicker.SelectedIndex(categoryIndex);
        AutomationProperties::SetAutomationId(m_symbolsCategoryPicker, L"NativeSymbolsCategory");
        AutomationProperties::SetName(m_symbolsCategoryPicker, ToHString(pick(L"Symbols Palette category", L"\u7279\u6b8a\u7b26\u865f\u8abf\u8272\u76e4\u5206\u985e")));
        m_symbolsCategoryPicker.SelectionChanged([this, categories](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            if (m_symbolsRendering) return;
            auto const index = sender.as<ComboBox>().SelectedIndex();
            if (index <= 0 || static_cast<std::size_t>(index) > categories.size())
            {
                m_symbolsCategoryKey.clear();
            }
            else
            {
                m_symbolsCategoryKey = categories[static_cast<std::size_t>(index - 1)].key;
            }
            RefreshSymbolsPaletteEntries();
        });
        page.Children().Append(m_symbolsCategoryPicker);

        m_symbolsRegexBuilder = Button();
        m_symbolsRegexBuilder.Content(box_value(ToHString(pick(L"Open full Regex Builder", L"\u958b\u555f\u5b8c\u6574 Regex \u5efa\u7acb\u5668"))));
        m_symbolsRegexBuilder.HorizontalAlignment(HorizontalAlignment::Left);
        m_symbolsRegexBuilder.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(m_symbolsRegexBuilder, L"NativeSymbolsRegexBuilder");
        AutomationProperties::SetName(m_symbolsRegexBuilder, ToHString(pick(
            L"Open the full safe native regex builder for Symbols Palette",
            L"\u958b\u555f\u7279\u6b8a\u7b26\u865f\u8abf\u8272\u76e4\u7528\u7684\u5b8c\u6574\u5b89\u5168 Regex \u5efa\u7acb\u5668")));
        m_symbolsRegexBuilder.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            OpenRegexBuilder(RegexBuilderTarget::SymbolsPalette, m_symbolsSearchText);
        });
        page.Children().Append(m_symbolsRegexBuilder);

        m_symbolsRegexStatus = CreateText(L"", 12);
        m_symbolsRegexStatus.TextWrapping(TextWrapping::Wrap);
        m_symbolsRegexStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_symbolsRegexStatus, L"NativeSymbolsStatus");
        AutomationProperties::SetLiveSetting(
            m_symbolsRegexStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_symbolsRegexStatus);

        m_symbolsResultCount = CreateText(L"", 13, true);
        AutomationProperties::SetAutomationId(m_symbolsResultCount, L"NativeSymbolsResultCount");
        page.Children().Append(m_symbolsResultCount);

        m_symbolsCopyStatus = CreateText(L"", 12);
        m_symbolsCopyStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_symbolsCopyStatus, L"NativeSymbolsCopyStatus");
        AutomationProperties::SetLiveSetting(
            m_symbolsCopyStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_symbolsCopyStatus);

        auto listHeading = CreateText(pick(L"Symbols", L"\u7b26\u865f"), 20, true);
        AutomationProperties::SetAutomationId(listHeading, L"NativeSymbolsList");
        page.Children().Append(listHeading);
        m_symbolsEntryList = StackPanel();
        m_symbolsEntryList.Spacing(10);
        page.Children().Append(m_symbolsEntryList);

        m_symbolsRendering = false;
        ShowPage(page);
        RefreshSymbolsPaletteEntries();
    }

    void MainWindow::RefreshSymbolsPaletteEntries()
    {
        if (!m_symbolsEntryList || !m_symbolsResultCount || !m_symbolsRegexStatus)
        {
            return;
        }

        auto const pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };
        auto query = std::wstring_view(m_symbolsSearchText);
        while (!query.empty() && std::iswspace(query.front())) query.remove_prefix(1);
        while (!query.empty() && std::iswspace(query.back())) query.remove_suffix(1);

        std::shared_ptr<winforge::core::regex::SafeRegex const> expression;
        if (m_symbolsRegexEnabled && !query.empty())
        {
            std::wstring diagnostic;
            expression = CompileSearchRegex(
                query,
                m_symbolsRegexCaseSensitive,
                m_symbolsRegexMultiline,
                m_symbolsRegexDotMatchesNewline,
                diagnostic,
                m_symbolsRegexIgnorePatternWhitespace,
                m_symbolsRegexExplicitCapture);
            if (!expression)
            {
                m_symbolsRegexDiagnostic = diagnostic;
                auto const status = pick(
                    L"Invalid PCRE2 filter; previous symbol results remain visible. " + diagnostic,
                    L"PCRE2 \u7be9\u9078\u7121\u6548\uff1b\u4e4b\u524d\u7b26\u865f\u7d50\u679c\u6703\u4fdd\u7559\u3002" + diagnostic);
                m_symbolsRegexStatus.Text(ToHString(status));
                AutomationProperties::SetName(m_symbolsRegexStatus, ToHString(status));
                return;
            }
        }

        m_symbolsRegexDiagnostic.clear();
        auto const status = m_symbolsRegexEnabled
            ? pick(
                L"PCRE2 local symbol filter is active with strict interactive safety limits.",
                L"PCRE2 \u672c\u6a5f\u7b26\u865f\u7be9\u9078\u5df2\u958b\u555f\uff0c\u7528\u56b4\u683c\u4ea4\u4e92\u5b89\u5168\u9650\u5236\u3002")
            : pick(
                L"Literal local symbol filter is active (case-insensitive).",
                L"\u5df2\u958b\u555f\u6587\u5b57\u672c\u6a5f\u7b26\u865f\u7be9\u9078\uff08\u5514\u5206\u5927\u5c0f\u5beb\uff09\u3002");
        m_symbolsRegexStatus.Text(ToHString(status));
        AutomationProperties::SetName(m_symbolsRegexStatus, ToHString(status));

        auto const matchesRegex = [expression](winforge::core::symbols::SymbolEntry const& entry)
        {
            if (!expression) return false;
            auto const matches = [expression](std::wstring_view field)
            {
                return expression->Search(field).matched;
            };
            return matches(entry.glyph) || matches(entry.name_en) || matches(entry.name_zh) ||
                matches(entry.category_en) || matches(entry.category_zh);
        };

        m_symbolsEntryList.Children().Clear();
        std::size_t resultCount = 0;
        for (auto const& entry : winforge::core::symbols::SymbolsEntries())
        {
            auto const categoryMatches = m_symbolsCategoryKey.empty() ||
                entry.category_key == m_symbolsCategoryKey;
            if (!categoryMatches)
            {
                continue;
            }
            auto const match = m_symbolsRegexEnabled
                ? (query.empty() || matchesRegex(entry))
                : winforge::core::symbols::SymbolsMatchesLiteral(entry, m_symbolsCategoryKey, query);
            if (!match)
            {
                continue;
            }

            ++resultCount;
            auto const code = std::to_wstring(static_cast<unsigned int>(entry.glyph.front()));
            auto const entryId = L"NativeSymbolsEntry_" + code + L"_" + std::wstring(entry.category_key);
            Border card;
            card.Padding(Thickness{ 12, 10, 12, 10 });
            card.Margin(Thickness{ 0, 0, 0, 2 });
            StackPanel content;
            content.Spacing(5);
            auto glyph = CreateText(entry.glyph, 28, true);
            glyph.FontFamily(Microsoft::UI::Xaml::Media::FontFamily(L"Segoe UI Symbol"));
            AutomationProperties::SetAutomationId(glyph, ToHString(entryId));
            AutomationProperties::SetName(glyph, ToHString(pick(entry.name_en, entry.name_zh)));
            content.Children().Append(glyph);
            auto name = CreateText(
                std::wstring(entry.name_en) + L" \u00B7 " + std::wstring(entry.name_zh),
                14,
                true);
            name.TextWrapping(TextWrapping::Wrap);
            content.Children().Append(name);
            auto category = CreateText(pick(entry.category_en, entry.category_zh), 12);
            category.Opacity(0.76);
            content.Children().Append(category);
            Button copy;
            copy.Content(box_value(ToHString(pick(L"Copy symbol", L"\u8907\u88fd\u7b26\u865f"))));
            copy.HorizontalAlignment(HorizontalAlignment::Left);
            copy.Padding(Thickness{ 12, 6, 12, 6 });
            AutomationProperties::SetAutomationId(copy, ToHString(L"NativeSymbolsCopy_" + code + L"_" + std::wstring(entry.category_key)));
            auto const copyName = pick(entry.name_en, entry.name_zh);
            AutomationProperties::SetName(copy, ToHString(pick(
                L"Copy " + copyName + L" symbol",
                L"\u8907\u88fd " + copyName + L" \u7b26\u865f")));
            copy.Click([this, value = std::wstring(entry.glyph)](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                try
                {
                    Windows::ApplicationModel::DataTransfer::DataPackage package;
                    package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                    package.SetText(ToHString(value));
                    Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                    ++m_symbolsCopyCount;
                    auto const message = winforge::core::LocalizedText{
                        L"Copied \"" + value + L"\" \u00D7" + std::to_wstring(m_symbolsCopyCount),
                        L"\u5df2\u8907\u88fd\u300c" + value + L"\u300d \u00D7" + std::to_wstring(m_symbolsCopyCount) }.Pick(m_language);
                    if (m_symbolsCopyStatus)
                    {
                        m_symbolsCopyStatus.Text(ToHString(message));
                        AutomationProperties::SetName(m_symbolsCopyStatus, ToHString(message));
                    }
                }
                catch (hresult_error const&)
                {
                    auto const message = winforge::core::LocalizedText{
                        L"Clipboard is unavailable; nothing was copied.",
                        L"\u526a\u8cbc\u7c3f\u4e0d\u53ef\u7528\uff1b\u6c92\u6709\u8907\u88fd\u3002" }.Pick(m_language);
                    if (m_symbolsCopyStatus)
                    {
                        m_symbolsCopyStatus.Text(ToHString(message));
                        AutomationProperties::SetName(m_symbolsCopyStatus, ToHString(message));
                    }
                }
            });
            content.Children().Append(copy);
            card.Child(content);
            m_symbolsEntryList.Children().Append(card);
        }

        auto const count = pick(
            std::to_wstring(resultCount) + L" of " +
                std::to_wstring(winforge::core::symbols::SymbolsEntries().size()) + L" symbols",
            std::to_wstring(resultCount) + L" / " +
                std::to_wstring(winforge::core::symbols::SymbolsEntries().size()) + L" \u500b\u7b26\u865f");
        m_symbolsResultCount.Text(ToHString(count));
        AutomationProperties::SetName(m_symbolsResultCount, ToHString(count));
        if (resultCount == 0)
        {
            auto empty = CreateText(pick(
                L"No symbols match the current local filter.",
                L"\u6c92\u6709\u7b26\u865f\u7b26\u5408\u73fe\u5728\u672c\u6a5f\u7be9\u9078\u3002"), 14, true);
            AutomationProperties::SetAutomationId(empty, L"NativeSymbolsEmpty");
            m_symbolsEntryList.Children().Append(empty);
        }
    }

    void MainWindow::RenderAppUninstaller()
    {
        auto const pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };

        m_appUninstallerRendering = true;
        auto page = CreatePage(
            pick(L"App Uninstaller", L"\u61C9\u7528\u7A0B\u5F0F\u89E3\u9664\u5B89\u88DD"),
            pick(
                L"Review and remove current-user Store/UWP packages with native Windows package APIs. Shared frameworks and resource packages are excluded before they reach this local cache.",
                L"\u7528\u539F\u751F Windows \u5957\u4EF6 API \u6AA2\u8996\u53CA\u79FB\u9664\u73FE\u6709\u4F7F\u7528\u8005\u7684 Store/UWP \u5957\u4EF6\u3002\u5171\u7528 framework \u53CA resource \u5957\u4EF6\u6703\u55BA\u9032\u5165\u672C\u6A5F\u7DE9\u5B58\u524D\u88AB\u6392\u9664\u3002"));
        AutomationProperties::SetAutomationId(page, L"NativeAppUninstallerPage");

        InfoBar safety;
        safety.IsOpen(true);
        safety.IsClosable(false);
        safety.Severity(InfoBarSeverity::Informational);
        safety.Title(ToHString(pick(
            L"Reviewed current-user removal",
            L"\u8986\u6838\u5F8C\u5148\u6703\u79FB\u9664\u73FE\u6709\u4F7F\u7528\u8005\u7684\u5957\u4EF6")));
        safety.Message(ToHString(pick(
            L"Search and Regex mode inspect only the returned in-memory package cache. A row action opens a review; no package changes until Confirm. Removal is disabled at elevated or unsafe integrity, and this migration slice never deletes local data folders.",
            L"\u641c\u5c0b\u540c Regex \u6a21\u5f0f\u53ea\u6703\u6aa2\u67e5\u5df2\u8fd4\u56de\u7684\u672c\u6a5f\u8a18\u61b6\u9ad4\u5957\u4ef6\u7de9\u5b58\u3002\u64f3\u5217\u52d5\u4f5c\u53ea\u6703\u958b\u8986\u6838\uff1b\u672a\u6309 Confirm \u4e4b\u524d\u5514\u6703\u6539\u8b8a\u5957\u4ef6\u3002\u5982\u679c\u5df2\u63d0\u5347\u6216 integrity \u4e0d\u5b89\u5168\u5c31\u6703\u7981\u7528\u79fb\u9664\uff0c\u800c\u4e14\u9019\u500b migration slice \u7d55\u4e0d\u6703\u522a\u9664\u672c\u6a5f\u8cc7\u6599\u76ee\u9304\u3002")));
        AutomationProperties::SetAutomationId(safety, L"NativeAppUninstallerSafety");
        AutomationProperties::SetName(
            safety,
            L"Native App Uninstaller safety: normal integrity required; local data deletion unavailable.");
        page.Children().Append(safety);

        m_appUninstallerSearchBox = TextBox();
        m_appUninstallerSearchBox.Header(box_value(ToHString(pick(
            L"Search the cached Store/UWP inventory",
            L"\u641C\u5C0B\u5DF2\u7DE9\u5B58\u7684 Store/UWP \u76EE\u9304"))));
        m_appUninstallerSearchBox.PlaceholderText(ToHString(pick(
            L"Literal text by default; enable Regex mode for bounded PCRE2",
            L"\u9810\u8A2D\u4FC2\u6587\u5B57\u641C\u5C0B\uFF1B\u958B Regex \u6A21\u5F0F\u5148\u6703\u7528\u6709\u9650\u5236\u7684 PCRE2")));
        m_appUninstallerSearchBox.Text(ToHString(m_appUninstallerSearchText));
        AutomationProperties::SetAutomationId(
            m_appUninstallerSearchBox,
            L"NativeAppUninstallerSearch");
        AutomationProperties::SetName(m_appUninstallerSearchBox, ToHString(pick(
            L"App Uninstaller local cached package search",
            L"\u61C9\u7528\u7A0B\u5F0F\u89E3\u9664\u5B89\u88DD\u672C\u6A5F\u7DE9\u5B58\u5957\u4EF6\u641C\u5C0B")));
        m_appUninstallerSearchBox.TextChanged(
            [this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                if (m_appUninstallerRendering)
                {
                    return;
                }
                m_appUninstallerSearchText = ToWide(sender.as<TextBox>().Text());
                RefreshAppUninstallerEntries();
            });
        page.Children().Append(m_appUninstallerSearchBox);

        m_appUninstallerRegexMode = ToggleSwitch();
        m_appUninstallerRegexMode.Header(box_value(ToHString(pick(
            L"Regex mode (bounded PCRE2 local cache filter)",
            L"Regex \u6A21\u5F0F\uFF08\u6709\u9650\u5236\u7684 PCRE2 \u672C\u6A5F\u7DE9\u5B58\u7BE9\u9078\uFF09"))));
        m_appUninstallerRegexMode.IsOn(m_appUninstallerRegexEnabled);
        AutomationProperties::SetAutomationId(
            m_appUninstallerRegexMode,
            L"NativeAppUninstallerRegexMode");
        AutomationProperties::SetName(m_appUninstallerRegexMode, ToHString(pick(
            L"Enable bounded Regex filtering of the local App Uninstaller cache",
            L"\u958B\u555F\u61C9\u7528\u7A0B\u5F0F\u89E3\u9664\u5B89\u88DD\u672C\u6A5F\u7DE9\u5B58\u7684\u6709\u9650\u5236 Regex \u7BE9\u9078")));
        m_appUninstallerRegexMode.Toggled(
            [this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
            {
                if (m_appUninstallerRendering)
                {
                    return;
                }
                m_appUninstallerRegexEnabled = sender.as<ToggleSwitch>().IsOn();
                RefreshAppUninstallerEntries();
            });
        page.Children().Append(m_appUninstallerRegexMode);

        m_appUninstallerRegexBuilder = Button();
        m_appUninstallerRegexBuilder.Content(box_value(ToHString(pick(
            L"Open full Regex Builder",
            L"\u958B\u555F\u5B8C\u6574 Regex \u5EFA\u7ACB\u5668"))));
        m_appUninstallerRegexBuilder.HorizontalAlignment(HorizontalAlignment::Left);
        m_appUninstallerRegexBuilder.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(
            m_appUninstallerRegexBuilder,
            L"NativeAppUninstallerRegexBuilder");
        m_appUninstallerRegexBuilder.Click(
            [this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                OpenRegexBuilder(
                    RegexBuilderTarget::AppUninstaller,
                    m_appUninstallerSearchText);
            });
        page.Children().Append(m_appUninstallerRegexBuilder);

        m_appUninstallerRefresh = Button();
        m_appUninstallerRefresh.Content(box_value(ToHString(pick(
            L"Refresh Store/UWP inventory",
            L"\u91CD\u65B0\u6574\u7406 Store/UWP \u76EE\u9304"))));
        m_appUninstallerRefresh.HorizontalAlignment(HorizontalAlignment::Left);
        m_appUninstallerRefresh.Padding(Thickness{ 14, 8, 14, 8 });
        m_appUninstallerRefresh.IsEnabled(!m_appUninstallerWorking);
        AutomationProperties::SetAutomationId(
            m_appUninstallerRefresh,
            L"NativeAppUninstallerRefresh");
        AutomationProperties::SetName(m_appUninstallerRefresh, ToHString(pick(
            L"Refresh the current-user Store and UWP package inventory",
            L"\u91CD\u65B0\u6574\u7406\u73FE\u6709\u4F7F\u7528\u8005\u7684 Store \u540C UWP \u5957\u4EF6\u76EE\u9304")));
        m_appUninstallerRefresh.Click(
            [this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                StartAppUninstallerRefresh();
            });
        page.Children().Append(m_appUninstallerRefresh);

        m_appUninstallerBusy = ProgressRing();
        m_appUninstallerBusy.IsActive(m_appUninstallerWorking);
        m_appUninstallerBusy.Visibility(
            m_appUninstallerWorking ? Visibility::Visible : Visibility::Collapsed);
        AutomationProperties::SetAutomationId(m_appUninstallerBusy, L"NativeAppUninstallerBusy");
        page.Children().Append(m_appUninstallerBusy);

        m_appUninstallerStatus = CreateText(L"", 12);
        m_appUninstallerStatus.TextWrapping(TextWrapping::Wrap);
        m_appUninstallerStatus.Opacity(0.84);
        AutomationProperties::SetAutomationId(m_appUninstallerStatus, L"NativeAppUninstallerStatus");
        AutomationProperties::SetLiveSetting(
            m_appUninstallerStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_appUninstallerStatus);

        m_appUninstallerResultCount = CreateText(L"", 13, true);
        AutomationProperties::SetAutomationId(
            m_appUninstallerResultCount,
            L"NativeAppUninstallerResultCount");
        AutomationProperties::SetLiveSetting(
            m_appUninstallerResultCount,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_appUninstallerResultCount);

        auto list_heading = CreateText(pick(
            L"Reviewed package inventory",
            L"\u8986\u6838\u5957\u4EF6\u76EE\u9304"), 20, true);
        AutomationProperties::SetAutomationId(list_heading, L"NativeAppUninstallerList");
        page.Children().Append(list_heading);

        m_appUninstallerEntryList = StackPanel();
        m_appUninstallerEntryList.Spacing(10);
        page.Children().Append(m_appUninstallerEntryList);

        m_appUninstallerRendering = false;
        ShowPage(page);
        RefreshAppUninstallerEntries();
        if (!m_appUninstallerLoaded && !m_appUninstallerWorking)
        {
            StartAppUninstallerRefresh();
        }
    }

    void MainWindow::RefreshAppUninstallerEntries()
    {
        if (!m_appUninstallerEntryList || !m_appUninstallerResultCount ||
            !m_appUninstallerStatus || !m_appUninstallerBusy ||
            !m_appUninstallerRefresh)
        {
            return;
        }

        auto const pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };

        auto query = std::wstring_view(m_appUninstallerSearchText);
        while (!query.empty() && std::iswspace(query.front()))
        {
            query.remove_prefix(1);
        }
        while (!query.empty() && std::iswspace(query.back()))
        {
            query.remove_suffix(1);
        }

        std::shared_ptr<winforge::core::regex::SafeRegex const> expression;
        if (m_appUninstallerRegexEnabled && !query.empty())
        {
            std::wstring diagnostic;
            expression = CompileSearchRegex(
                query,
                m_appUninstallerRegexCaseSensitive,
                m_appUninstallerRegexMultiline,
                m_appUninstallerRegexDotMatchesNewline,
                diagnostic,
                m_appUninstallerRegexIgnorePatternWhitespace,
                m_appUninstallerRegexExplicitCapture);
            if (!expression)
            {
                m_appUninstallerRegexDiagnostic = diagnostic;
                auto const status = pick(
                    L"Invalid PCRE2 filter; prior local package results remain visible. " + diagnostic,
                    L"PCRE2 \u7BE9\u9078\u7121\u6548\uFF1B\u4E4B\u524D\u672C\u6A5F\u5957\u4EF6\u7D50\u679C\u6703\u4FDD\u7559\u3002" + diagnostic);
                m_appUninstallerStatus.Text(ToHString(status));
                AutomationProperties::SetName(m_appUninstallerStatus, ToHString(status));
                return;
            }
        }

        if (m_appUninstallerCompletionLost.exchange(false, std::memory_order_acq_rel))
        {
            m_appUninstallerWorking = false;
            m_appUninstallerReviewPackage.reset();
            m_appUninstallerStatusMessage = winforge::core::LocalizedText{
                L"The native worker ended after its UI completion queue closed. Package state was not refreshed; refresh before another action.",
                L"\u539f\u751f worker \u55ba UI completion queue \u95dc\u9589\u5f8c\u7d50\u675f\u3002\u5957\u4ef6\u72c0\u614b\u672a\u91cd\u65b0\u6574\u7406\uff1b\u8acb\u5148\u91cd\u65b0\u6574\u7406\u5148\u518d\u64cd\u4f5c\u3002" }.Pick(m_language);
        }

        m_appUninstallerRegexDiagnostic.clear();
        winforge::core::uninstall::AppUninstallerFilterOptions filter_options;
        filter_options.case_sensitive =
            m_appUninstallerRegexEnabled && m_appUninstallerRegexCaseSensitive;
        filter_options.expression = expression;
        m_appUninstallerVisiblePackages = winforge::core::uninstall::FilterAppPackages(
            m_appUninstallerPackages,
            query,
            filter_options);

        auto const normal_integrity =
            winforge::core::packages::IsNormalIntegrityProcess();
        auto const filter_status = m_appUninstallerRegexEnabled
            ? pick(
                L"Bounded PCRE2 filters only the already-returned local package cache.",
                L"\u6709\u9650\u5236\u7684 PCRE2 \u53EA\u6703\u7BE9\u9078\u5DF2\u8FD4\u56DE\u7684\u672C\u6A5F\u5957\u4EF6\u7DE9\u5B58\u3002")
            : pick(
                L"Literal filtering is local and case-insensitive.",
                L"\u6587\u5B57\u7BE9\u9078\u4FC2\u672C\u6A5F\u7684\uFF0C\u5514\u5206\u5927\u5C0F\u5BEB\u3002");
        auto status = m_appUninstallerStatusMessage.empty()
            ? filter_status
            : m_appUninstallerStatusMessage + L" " + filter_status;
        if (m_appUninstallerWorking)
        {
            status += L" " + pick(
                L"A native package operation is in progress; controls are disabled.",
                L"\u539F\u751F\u5957\u4EF6\u64CD\u4F5C\u6B63\u5728\u9032\u884C\uFF1B\u63A7\u4EF6\u5DF2\u7981\u7528\u3002");
        }
        if (!m_appUninstallerWorking && !normal_integrity)
        {
            status += L" " + pick(
                L"Removal is disabled while WinForge is elevated or token inspection is unavailable.",
                L"WinForge \u5df2\u63d0\u5347\u6216 token \u7121\u6cd5\u5b89\u5168\u6aa2\u67e5\uff0c\u79fb\u9664\u5df2\u7981\u7528\u3002");
        }
        m_appUninstallerStatus.Text(ToHString(status));
        AutomationProperties::SetName(m_appUninstallerStatus, ToHString(status));

        auto const count = pick(
            std::to_wstring(m_appUninstallerVisiblePackages.size()) + L" / " +
                std::to_wstring(m_appUninstallerPackages.size()) + L" Store/UWP apps",
            std::to_wstring(m_appUninstallerVisiblePackages.size()) + L" / " +
                std::to_wstring(m_appUninstallerPackages.size()) + L" \u500B Store/UWP \u61C9\u7528\u7A0B\u5F0F");
        m_appUninstallerResultCount.Text(ToHString(count));
        AutomationProperties::SetName(m_appUninstallerResultCount, ToHString(count));
        m_appUninstallerBusy.IsActive(m_appUninstallerWorking);
        m_appUninstallerBusy.Visibility(
            m_appUninstallerWorking ? Visibility::Visible : Visibility::Collapsed);
        m_appUninstallerRefresh.IsEnabled(!m_appUninstallerWorking);

        m_appUninstallerEntryList.Children().Clear();
        if (m_appUninstallerReviewPackage)
        {
            auto const& package = *m_appUninstallerReviewPackage;
            Border review;
            review.Padding(Thickness{ 14, 12, 14, 12 });
            StackPanel content;
            content.Spacing(7);
            auto heading = CreateText(pick(
                L"Review uninstall",
                L"\u8986\u6838\u89e3\u9664\u5b89\u88dd"), 18, true);
            AutomationProperties::SetAutomationId(heading, L"NativeAppUninstallerReview");
            content.Children().Append(heading);
            auto identity = CreateText(
                winforge::core::uninstall::AppPackageDisplayName(package) + L"\n" +
                    package.name + L"\n" + package.package_full_name,
                13);
            identity.TextWrapping(TextWrapping::Wrap);
            content.Children().Append(identity);
            auto policy = CreateText(pick(
                L"Confirm removes this current-user package through PackageManager. This native migration slice never deletes local data folders.",
                L"Confirm \u6703\u900f\u904e PackageManager \u79fb\u9664\u9019\u500b\u73fe\u6709\u4f7f\u7528\u8005\u5957\u4ef6\u3002\u9019\u500b\u539f\u751f migration slice \u7d55\u4e0d\u6703\u522a\u9664\u672c\u6a5f\u8cc7\u6599\u76ee\u9304\u3002"), 12);
            policy.TextWrapping(TextWrapping::Wrap);
            content.Children().Append(policy);

            Button confirm;
            confirm.Content(box_value(ToHString(pick(L"Confirm uninstall", L"Confirm \u89e3\u9664\u5b89\u88dd"))));
            confirm.HorizontalAlignment(HorizontalAlignment::Left);
            confirm.Padding(Thickness{ 14, 8, 14, 8 });
            confirm.IsEnabled(!m_appUninstallerWorking && normal_integrity);
            AutomationProperties::SetAutomationId(confirm, L"NativeAppUninstallerConfirm");
            confirm.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                StartAppUninstallerRemoval();
            });
            content.Children().Append(confirm);

            Button cancel;
            cancel.Content(box_value(ToHString(pick(L"Cancel review", L"\u53D6\u6D88\u8986\u6838"))));
            cancel.HorizontalAlignment(HorizontalAlignment::Left);
            cancel.Padding(Thickness{ 14, 8, 14, 8 });
            cancel.IsEnabled(!m_appUninstallerWorking);
            AutomationProperties::SetAutomationId(cancel, L"NativeAppUninstallerCancel");
            cancel.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                if (m_appUninstallerWorking)
                {
                    return;
                }
                m_appUninstallerReviewPackage.reset();
                m_appUninstallerStatusMessage = winforge::core::LocalizedText{
                    L"Removal review cancelled; no package or local data changed.",
                    L"\u5DF2\u53D6\u6D88\u79FB\u9664\u8986\u6838\uFF1B\u5957\u4EF6\u53CA\u672C\u6A5F\u8CC7\u6599\u90FD\u5514\u6703\u6539\u8B8A\u3002" }.Pick(m_language);
                RefreshAppUninstallerEntries();
            });
            content.Children().Append(cancel);
            review.Child(content);
            m_appUninstallerEntryList.Children().Append(review);
        }

        if (m_appUninstallerVisiblePackages.empty())
        {
            auto empty = CreateText(
                m_appUninstallerLoaded
                    ? pick(
                        L"No Store/UWP apps match the current local filter.",
                        L"\u6C92\u6709 Store/UWP \u61C9\u7528\u7A0B\u5F0F\u7B26\u5408\u73FE\u5728\u672C\u6A5F\u7BE9\u9078\u3002")
                    : pick(
                        L"Loading the current-user Store/UWP inventory.",
                        L"\u6B63\u5728\u6574\u7406\u73FE\u6709\u4F7F\u7528\u8005\u7684 Store/UWP \u76EE\u9304\u3002"),
                14,
                true);
            AutomationProperties::SetAutomationId(empty, L"NativeAppUninstallerEmpty");
            m_appUninstallerEntryList.Children().Append(empty);
            return;
        }

        for (auto const& package : m_appUninstallerVisiblePackages)
        {
            auto const suffix = StableAutomationSuffix(package.package_full_name);
            Border card;
            card.Padding(Thickness{ 12, 10, 12, 10 });
            StackPanel content;
            content.Spacing(5);

            auto display = CreateText(
                winforge::core::uninstall::AppPackageDisplayName(package),
                17,
                true);
            display.TextWrapping(TextWrapping::Wrap);
            AutomationProperties::SetAutomationId(
                display,
                ToHString(L"NativeAppUninstallerRow_" + suffix));
            AutomationProperties::SetName(display, ToHString(pick(
                L"Reviewed package " + winforge::core::uninstall::AppPackageDisplayName(package),
                L"\u8986\u6838\u5957\u4EF6 " + winforge::core::uninstall::AppPackageDisplayName(package))));
            content.Children().Append(display);

            auto package_name = CreateText(package.name, 13);
            package_name.TextWrapping(TextWrapping::Wrap);
            content.Children().Append(package_name);
            auto details = CreateText(
                pick(L"Publisher: ", L"\u767C\u4F48\u8005\uFF1A") +
                    package.publisher + L"  |  " +
                    pick(L"Version: ", L"\u7248\u672C\uFF1A") + package.version,
                12);
            details.TextWrapping(TextWrapping::Wrap);
            details.Opacity(0.80);
            content.Children().Append(details);
            auto family = CreateText(
                pick(L"Package family: ", L"\u5957\u4EF6 family\uFF1A") +
                    package.package_family_name,
                12);
            family.TextWrapping(TextWrapping::Wrap);
            family.Opacity(0.72);
            content.Children().Append(family);

            Button review_remove;
            review_remove.Content(box_value(ToHString(pick(
                L"Review remove",
                L"\u8986\u6838\u79FB\u9664"))));
            review_remove.HorizontalAlignment(HorizontalAlignment::Left);
            review_remove.Padding(Thickness{ 12, 6, 12, 6 });
            review_remove.IsEnabled(!m_appUninstallerWorking && normal_integrity);
            AutomationProperties::SetAutomationId(
                review_remove,
                ToHString(L"NativeAppUninstallerReviewRemove_" + suffix));
            review_remove.Click([this, package](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                if (m_appUninstallerWorking)
                {
                    return;
                }
                m_appUninstallerReviewPackage = package;
                m_appUninstallerStatusMessage = winforge::core::LocalizedText{
                    L"Removal review opened. No package has changed.",
                    L"\u5DF2\u958B\u555F\u79FB\u9664\u8986\u6838\u3002\u5957\u4EF6\u4ECD\u7136\u672A\u6709\u6539\u8B8A\u3002" }.Pick(m_language);
                RefreshAppUninstallerEntries();
            });
            content.Children().Append(review_remove);

            auto const deep_cleanup_message = pick(
                L"Deep cleanup is intentionally unavailable until handle-relative deletion is implemented. Package removal never deletes local data.",
                L"\u6df1\u5c64\u6e05\u7406\u8981\u7b49 handle-relative deletion \u5b8c\u6210\u624d\u6703\u958b\u653e\u3002\u5957\u4ef6\u79fb\u9664\u7d55\u4e0d\u6703\u522a\u9664\u672c\u6a5f\u8cc7\u6599\u3002");
            auto deep_cleanup_note = CreateText(deep_cleanup_message, 12);
            deep_cleanup_note.TextWrapping(TextWrapping::Wrap);
            deep_cleanup_note.Opacity(0.72);
            AutomationProperties::SetAutomationId(
                deep_cleanup_note,
                ToHString(L"NativeAppUninstallerDeepCleanupStatus_" + suffix));
            AutomationProperties::SetName(deep_cleanup_note, ToHString(deep_cleanup_message));
            content.Children().Append(deep_cleanup_note);

            card.Child(content);
            m_appUninstallerEntryList.Children().Append(card);
        }
    }

    void MainWindow::StartAppUninstallerRefresh()
    {
        if (m_appUninstallerWorking)
        {
            return;
        }

        m_appUninstallerWorking = true;
        m_appUninstallerReviewPackage.reset();
        auto const generation = ++m_appUninstallerGeneration;
        m_appUninstallerStatusMessage = winforge::core::LocalizedText{
            L"Refreshing the current-user Store/UWP package cache with native Windows APIs.",
            L"\u6B63\u5728\u7528\u539F\u751F Windows API \u91CD\u65B0\u6574\u7406\u73FE\u6709\u4F7F\u7528\u8005\u7684 Store/UWP \u5957\u4EF6\u7DE9\u5B58\u3002" }.Pick(m_language);
        if (m_currentRoute == L"module.uninstall")
        {
            RefreshAppUninstallerEntries();
        }

        try
        {
            auto lifetime = get_strong();
            auto dispatcher = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
            std::thread worker([lifetime, dispatcher, generation]() mutable
            {
                std::vector<winforge::core::uninstall::AppPackage> packages;
                bool completed = false;
                std::wstring diagnostic;
                bool apartment_initialized = false;
                try
                {
                    winrt::init_apartment(winrt::apartment_type::multi_threaded);
                    apartment_initialized = true;
                    Windows::Management::Deployment::PackageManager manager;
                    for (auto const& package : manager.FindPackagesForUser(L""))
                    {
                        try
                        {
                            if (package.IsFramework() || package.IsResourcePackage())
                            {
                                continue;
                            }
                        }
                        catch (hresult_error const&)
                        {
                            continue;
                        }

                        winforge::core::uninstall::AppPackage record;
                        try
                        {
                            auto const id = package.Id();
                            record.name = ToWide(id.Name());
                            record.package_full_name = ToWide(id.FullName());
                            record.package_family_name = ToWide(id.FamilyName());
                            auto const version = id.Version();
                            record.version = std::to_wstring(version.Major) + L"." +
                                std::to_wstring(version.Minor) + L"." +
                                std::to_wstring(version.Build) + L"." +
                                std::to_wstring(version.Revision);
                        }
                        catch (hresult_error const&)
                        {
                            continue;
                        }
                        if (record.package_full_name.empty())
                        {
                            continue;
                        }
                        try
                        {
                            record.display_name = ToWide(package.DisplayName());
                        }
                        catch (hresult_error const&)
                        {
                        }
                        try
                        {
                            record.publisher = ToWide(package.PublisherDisplayName());
                        }
                        catch (hresult_error const&)
                        {
                        }
                        try
                        {
                            record.install_location = ToWide(package.InstalledLocation().Path());
                        }
                        catch (hresult_error const&)
                        {
                        }
                        packages.push_back(std::move(record));
                    }
                    completed = true;
                }
                catch (hresult_error const&)
                {
                    diagnostic = L"The Windows package inventory API was unavailable; no package was changed.";
                }
                catch (...)
                {
                    diagnostic = L"The native package inventory refresh failed closed; no package was changed.";
                }
                if (apartment_initialized)
                {
                    winrt::uninit_apartment();
                }

                try
                {
                    bool queued = false;
                    if (dispatcher)
                    {
                        queued = dispatcher.TryEnqueue(
                            [lifetime, generation, packages = std::move(packages),
                                completed, diagnostic = std::move(diagnostic)]() mutable
                            {
                                if (generation != lifetime->m_appUninstallerGeneration)
                                {
                                    return;
                                }
                                lifetime->m_appUninstallerWorking = false;
                                lifetime->m_appUninstallerLoaded = true;
                                if (completed)
                                {
                                    lifetime->m_appUninstallerPackages = std::move(packages);
                                    lifetime->m_appUninstallerStatusMessage =
                                        winforge::core::LocalizedText{
                                            L"Current-user Store/UWP inventory refreshed. Framework and resource packages remain excluded.",
                                            L"\u73FE\u6709\u4F7F\u7528\u8005\u7684 Store/UWP \u76EE\u9304\u5DF2\u91CD\u65B0\u6574\u7406\u3002framework \u540C resource \u5957\u4EF6\u4ECD\u7136\u88AB\u6392\u9664\u3002" }.Pick(lifetime->m_language);
                                }
                                else
                                {
                                    lifetime->m_appUninstallerStatusMessage =
                                        winforge::core::LocalizedText{
                                            L"Package inventory refresh failed closed; existing cached rows were retained. " + diagnostic,
                                            L"\u5957\u4EF6\u76EE\u9304\u91CD\u65B0\u6574\u7406\u5DF2 fail closed\uFF1B\u4FDD\u7559\u5DF2\u5B58\u7684\u7DE9\u5B58\u5217\u3002" + diagnostic }.Pick(lifetime->m_language);
                                }
                                if (lifetime->m_currentRoute == L"module.uninstall")
                                {
                                    lifetime->RefreshAppUninstallerEntries();
                                }
                            });
                    }
                    if (!queued)
                    {
                        lifetime->m_appUninstallerCompletionLost.store(
                            true, std::memory_order_release);
                    }
                }
                catch (...)
                {
                    lifetime->m_appUninstallerCompletionLost.store(
                        true, std::memory_order_release);
                }
            });
            worker.detach();
        }
        catch (...)
        {
            m_appUninstallerWorking = false;
            m_appUninstallerStatusMessage = winforge::core::LocalizedText{
                L"Could not start the native package inventory worker; no package was changed.",
                L"\u7121\u6CD5\u958B\u59CB\u539F\u751F\u5957\u4EF6\u76EE\u9304 worker\uFF1B\u6C92\u6709\u5957\u4EF6\u88AB\u6539\u8B8A\u3002" }.Pick(m_language);
            if (m_currentRoute == L"module.uninstall")
            {
                RefreshAppUninstallerEntries();
            }
        }
    }

    void MainWindow::StartAppUninstallerRemoval()
    {
        if (m_appUninstallerWorking || !m_appUninstallerReviewPackage ||
            m_appUninstallerReviewPackage->package_full_name.empty())
        {
            return;
        }

        if (!winforge::core::packages::IsNormalIntegrityProcess())
        {
            m_appUninstallerReviewPackage.reset();
            m_appUninstallerStatusMessage = winforge::core::LocalizedText{
                L"Package removal is disabled while WinForge is elevated or token inspection is unavailable. No package changed.",
                L"WinForge \u5df2\u63d0\u5347\u6216 token \u7121\u6cd5\u5b89\u5168\u6aa2\u67e5\uff0c\u5957\u4ef6\u79fb\u9664\u5df2\u7981\u7528\u3002\u6c92\u6709\u5957\u4ef6\u88ab\u6539\u8b8a\u3002" }.Pick(m_language);
            RefreshAppUninstallerEntries();
            return;
        }


        auto const package = *m_appUninstallerReviewPackage;
        m_appUninstallerWorking = true;
        auto const generation = ++m_appUninstallerGeneration;
        m_appUninstallerStatusMessage = winforge::core::LocalizedText{
            L"Confirmed native package removal is running. No other package is queued.",
            L"\u5df2\u78ba\u8a8d\u7684\u539f\u751f\u5957\u4ef6\u79fb\u9664\u6b63\u5728\u904b\u884c\u3002\u6c92\u6709\u5176\u4ed6\u5957\u4ef6\u88ab\u6392\u968a\u3002" }.Pick(m_language);
        if (m_currentRoute == L"module.uninstall")
        {
            RefreshAppUninstallerEntries();
        }

        try
        {
            auto lifetime = get_strong();
            auto dispatcher = Microsoft::UI::Dispatching::DispatcherQueue::GetForCurrentThread();
            std::thread worker([lifetime, dispatcher, generation, package]() mutable
            {
                bool removed = false;
                bool apartment_initialized = false;
                try
                {
                    winrt::init_apartment(winrt::apartment_type::multi_threaded);
                    apartment_initialized = true;
                    Windows::Management::Deployment::PackageManager manager;
                    auto const result = manager.RemovePackageAsync(
                        ToHString(package.package_full_name)).get();
                    removed = static_cast<HRESULT>(result.ExtendedErrorCode()) == S_OK;
                }
                catch (hresult_error const&)
                {
                    // Provider diagnostics may expose unbounded package-specific text.
                    // Keep the reviewed UI outcome bounded and fail closed.
                    removed = false;
                }
                catch (...)
                {
                    removed = false;
                }
                if (apartment_initialized)
                {
                    winrt::uninit_apartment();
                }

                try
                {
                    bool queued = false;
                    if (dispatcher)
                    {
                        queued = dispatcher.TryEnqueue(
                            [lifetime, generation, package = std::move(package), removed]() mutable
                            {
                                if (generation != lifetime->m_appUninstallerGeneration)
                                {
                                    return;
                                }
                                lifetime->m_appUninstallerWorking = false;
                                lifetime->m_appUninstallerReviewPackage.reset();
                                if (removed)
                                {
                                    lifetime->m_appUninstallerPackages.erase(
                                        std::remove_if(
                                            lifetime->m_appUninstallerPackages.begin(),
                                            lifetime->m_appUninstallerPackages.end(),
                                            [&package](winforge::core::uninstall::AppPackage const& item)
                                            {
                                                return item.package_full_name ==
                                                    package.package_full_name;
                                            }),
                                        lifetime->m_appUninstallerPackages.end());
                                    lifetime->m_appUninstallerStatusMessage =
                                        winforge::core::LocalizedText{
                                            L"Package removal completed for the current user. No local data folder was deleted.",
                                            L"\u73fe\u6709\u4f7f\u7528\u8005\u7684\u5957\u4ef6\u79fb\u9664\u5df2\u5b8c\u6210\u3002\u6c92\u6709\u522a\u9664\u4efb\u4f55\u672c\u6a5f\u8cc7\u6599\u76ee\u9304\u3002" }.Pick(lifetime->m_language);
                                }
                                else
                                {
                                    lifetime->m_appUninstallerStatusMessage =
                                        winforge::core::LocalizedText{
                                            L"Package removal failed; no local data folder was deleted and no other package changed.",
                                            L"\u5957\u4ef6\u79fb\u9664\u5931\u6557\uff1b\u6c92\u6709\u522a\u9664\u672c\u6a5f\u8cc7\u6599\u76ee\u9304\uff0c\u4ea6\u6c92\u6709\u5176\u4ed6\u5957\u4ef6\u88ab\u6539\u8b8a\u3002" }.Pick(lifetime->m_language);
                                }
                                if (lifetime->m_currentRoute == L"module.uninstall")
                                {
                                    lifetime->RefreshAppUninstallerEntries();
                                }
                            });
                    }
                    if (!queued)
                    {
                        lifetime->m_appUninstallerCompletionLost.store(
                            true, std::memory_order_release);
                    }
                }
                catch (...)
                {
                    lifetime->m_appUninstallerCompletionLost.store(
                        true, std::memory_order_release);
                }
            });
            worker.detach();
        }
        catch (...)
        {
            m_appUninstallerWorking = false;
            m_appUninstallerStatusMessage = winforge::core::LocalizedText{
                L"Could not start the confirmed native removal worker; no package changed.",
                L"\u7121\u6cd5\u958b\u59cb\u5df2\u78ba\u8a8d\u7684\u539f\u751f\u79fb\u9664 worker\uff1b\u6c92\u6709\u5957\u4ef6\u88ab\u6539\u8b8a\u3002" }.Pick(m_language);
            if (m_currentRoute == L"module.uninstall")
            {
                RefreshAppUninstallerEntries();
            }
        }
    }

    void MainWindow::RenderRegexCheatsheet()
    {
        auto const pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };

        auto page = CreatePage(
            pick(L"Regex Cheatsheet", L"Regex 速查表"),
            pick(
                L"A native, local-only regex reference. Literal matching remains the default; enable Regex mode to filter the static reference catalog through the bounded PCRE2 layer, or hand a verified pattern to the full builder.",
                L"呢個係原生、只讀本機嘅 regex 參考。預設用文字比對；開啟 Regex 模式先會用有嚴格限制嘅 PCRE2 篩選靜態參考內容，亦可以交畀完整建構精靈。"));
        AutomationProperties::SetAutomationId(page, L"NativeRegexCheatPage");

        InfoBar safety;
        safety.IsOpen(true);
        safety.IsClosable(false);
        safety.Severity(InfoBarSeverity::Informational);
        safety.Title(ToHString(pick(L"Reference-only and local", L"只作參考兼只喺本機運作")));
        safety.Message(ToHString(pick(
            L"Rows are copied only after an explicit action. Some entries document .NET-only syntax or replacement text; they are never executed here. Regex mode searches static text only and never reaches a command line, package engine, network, or process.",
            L"只有明確按複製先會寫入剪貼簿。有啲項目係 .NET 專用語法或者取代文字，只作說明、唔會喺呢度執行。Regex 模式只會搜尋靜態文字，唔會交去命令列、套件引擎、網絡或者程序。")));
        AutomationProperties::SetAutomationId(safety, L"NativeRegexCheatSafety");
        page.Children().Append(safety);

        m_regexCheatSearchBox = TextBox();
        m_regexCheatSearchBox.Header(box_value(ToHString(pick(L"Find a token, description, example, or category", L"搵 token、說明、例子或者分類"))));
        m_regexCheatSearchBox.PlaceholderText(ToHString(pick(L"Literal text by default; enable Regex mode for bounded PCRE2", L"預設文字搜尋；開 Regex 模式先用有嚴格限制嘅 PCRE2")));
        m_regexCheatSearchBox.Text(ToHString(m_regexCheatSearchText));
        AutomationProperties::SetAutomationId(m_regexCheatSearchBox, L"NativeRegexCheatSearchBox");
        AutomationProperties::SetName(m_regexCheatSearchBox, ToHString(pick(L"Regex Cheatsheet local reference search", L"Regex 速查表本機參考搜尋")));
        m_regexCheatSearchBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            m_regexCheatSearchText = ToWide(sender.as<TextBox>().Text());
            RefreshRegexCheatsheetEntries();
        });
        page.Children().Append(m_regexCheatSearchBox);

        m_regexCheatRegexMode = ToggleSwitch();
        m_regexCheatRegexMode.Header(box_value(ToHString(pick(L"Regex mode (bounded PCRE2 local filter)", L"Regex 模式（有限制嘅 PCRE2 本機篩選）"))));
        m_regexCheatRegexMode.IsOn(m_regexCheatRegexEnabled);
        AutomationProperties::SetAutomationId(m_regexCheatRegexMode, L"NativeRegexCheatRegexMode");
        m_regexCheatRegexMode.Toggled([this](Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&)
        {
            m_regexCheatRegexEnabled = sender.as<ToggleSwitch>().IsOn();
            RefreshRegexCheatsheetEntries();
        });
        page.Children().Append(m_regexCheatRegexMode);

        m_regexCheatCategoryPicker = ComboBox();
        m_regexCheatCategoryPicker.Header(box_value(ToHString(pick(L"Reference category", L"參考分類"))));
        ComboBoxItem allCategories;
        allCategories.Content(box_value(ToHString(pick(L"All reference entries", L"全部參考項目"))));
        m_regexCheatCategoryPicker.Items().Append(allCategories);
        auto const categories = winforge::core::regex::RegexCheatCategories();
        int32_t categoryIndex = 0;
        for (std::size_t index = 0; index < categories.size(); ++index)
        {
            auto const& category = categories[index];
            ComboBoxItem item;
            item.Content(box_value(ToHString(pick(category.name_en, category.name_zh))));
            m_regexCheatCategoryPicker.Items().Append(item);
            if (category.key == m_regexCheatCategoryKey)
            {
                categoryIndex = static_cast<int32_t>(index + 1);
            }
        }
        m_regexCheatCategoryPicker.SelectedIndex(categoryIndex);
        AutomationProperties::SetAutomationId(m_regexCheatCategoryPicker, L"NativeRegexCheatCategory");
        m_regexCheatCategoryPicker.SelectionChanged([this, categories](Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&)
        {
            auto const index = sender.as<ComboBox>().SelectedIndex();
            if (index <= 0 || static_cast<std::size_t>(index) > categories.size())
            {
                m_regexCheatCategoryKey.clear();
            }
            else
            {
                m_regexCheatCategoryKey = categories[static_cast<std::size_t>(index - 1)].key;
            }
            RefreshRegexCheatsheetEntries();
        });
        page.Children().Append(m_regexCheatCategoryPicker);

        m_regexCheatRegexBuilder = Button();
        m_regexCheatRegexBuilder.Content(box_value(ToHString(pick(L"Open full Regex Builder", L"開啟完整 Regex 建構精靈"))));
        m_regexCheatRegexBuilder.HorizontalAlignment(HorizontalAlignment::Left);
        m_regexCheatRegexBuilder.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(m_regexCheatRegexBuilder, L"NativeRegexCheatRegexBuilder");
        AutomationProperties::SetName(m_regexCheatRegexBuilder, ToHString(pick(L"Open the full safe native regex builder for the Cheatsheet filter", L"為速查表篩選開啟完整而安全嘅原生 regex 建構精靈")));
        m_regexCheatRegexBuilder.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            OpenRegexBuilder(RegexBuilderTarget::RegexCheatsheet, m_regexCheatSearchText);
        });
        page.Children().Append(m_regexCheatRegexBuilder);

        m_regexCheatRegexStatus = CreateText(L"", 12);
        m_regexCheatRegexStatus.TextWrapping(TextWrapping::Wrap);
        m_regexCheatRegexStatus.Opacity(0.82);
        AutomationProperties::SetAutomationId(m_regexCheatRegexStatus, L"NativeRegexCheatRegexStatus");
        AutomationProperties::SetLiveSetting(
            m_regexCheatRegexStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_regexCheatRegexStatus);

        m_regexCheatResultCount = CreateText(L"", 13, true);
        AutomationProperties::SetAutomationId(m_regexCheatResultCount, L"NativeRegexCheatResultCount");
        page.Children().Append(m_regexCheatResultCount);

        m_regexCheatCopyStatus = CreateText(L"", 12);
        m_regexCheatCopyStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(m_regexCheatCopyStatus, L"NativeRegexCheatCopyStatus");
        AutomationProperties::SetLiveSetting(
            m_regexCheatCopyStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_regexCheatCopyStatus);

        auto referenceHeading = CreateText(pick(L"Reference entries", L"參考項目"), 20, true);
        // StackPanel itself has no dependable UIA peer. Put the region's
        // stable landmark on its exposed heading while the live rows remain
        // in the adjacent panel.
        AutomationProperties::SetAutomationId(referenceHeading, L"NativeRegexCheatEntryList");
        page.Children().Append(referenceHeading);
        m_regexCheatEntryList = StackPanel();
        m_regexCheatEntryList.Spacing(12);
        page.Children().Append(m_regexCheatEntryList);

        auto recipeHeading = CreateText(pick(L"Ready-made patterns", L"現成配對模式"), 20, true);
        AutomationProperties::SetAutomationId(recipeHeading, L"NativeRegexCheatRecipeList");
        page.Children().Append(recipeHeading);
        auto recipes = StackPanel();
        recipes.Spacing(10);
        for (auto const& recipe : winforge::core::regex::RegexCheatRecipes())
        {
            Border card;
            card.Padding(Thickness{ 12, 10, 12, 10 });
            card.Margin(Thickness{ 0, 0, 0, 2 });
            StackPanel content;
            content.Spacing(5);
            content.Children().Append(CreateText(pick(recipe.name_en, recipe.name_zh), 15, true));
            auto pattern = CreateText(recipe.pattern, 12);
            pattern.FontFamily(Microsoft::UI::Xaml::Media::FontFamily(L"Consolas"));
            content.Children().Append(pattern);
            Button copy;
            copy.Content(box_value(ToHString(pick(L"Copy pattern", L"複製模式"))));
            copy.HorizontalAlignment(HorizontalAlignment::Left);
            copy.Padding(Thickness{ 12, 6, 12, 6 });
            AutomationProperties::SetAutomationId(copy, ToHString(L"NativeRegexCheatCopyRecipe_" + std::wstring(recipe.key)));
            auto const copyName = pick(recipe.name_en, recipe.name_zh);
            AutomationProperties::SetName(copy, ToHString(pick(L"Copy " + copyName + L" pattern", L"複製 " + copyName + L" 模式")));
            copy.Click([this, value = std::wstring(recipe.pattern)](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                try
                {
                    Windows::ApplicationModel::DataTransfer::DataPackage package;
                    package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                    package.SetText(ToHString(value));
                    Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                    auto const message = winforge::core::LocalizedText{
                        L"Pattern copied to clipboard.", L"模式已複製去剪貼簿。" }.Pick(m_language);
                    if (m_regexCheatCopyStatus)
                    {
                        m_regexCheatCopyStatus.Text(ToHString(message));
                        AutomationProperties::SetName(m_regexCheatCopyStatus, ToHString(message));
                    }
                }
                catch (hresult_error const&)
                {
                    auto const message = winforge::core::LocalizedText{
                        L"Clipboard is unavailable; nothing was copied.", L"剪貼簿暫時唔可用；未有複製任何內容。" }.Pick(m_language);
                    if (m_regexCheatCopyStatus)
                    {
                        m_regexCheatCopyStatus.Text(ToHString(message));
                        AutomationProperties::SetName(m_regexCheatCopyStatus, ToHString(message));
                    }
                }
            });
            content.Children().Append(copy);
            card.Child(content);
            recipes.Children().Append(card);
        }
        page.Children().Append(recipes);

        ShowPage(page);
        RefreshRegexCheatsheetEntries();
    }

    void MainWindow::RefreshRegexCheatsheetEntries()
    {
        if (!m_regexCheatEntryList || !m_regexCheatResultCount || !m_regexCheatRegexStatus)
        {
            return;
        }

        auto const pick = [this](std::wstring_view en, std::wstring_view zh)
        {
            return winforge::core::LocalizedText{ std::wstring(en), std::wstring(zh) }.Pick(m_language);
        };
        auto query = std::wstring_view(m_regexCheatSearchText);
        while (!query.empty() && std::iswspace(query.front())) query.remove_prefix(1);
        while (!query.empty() && std::iswspace(query.back())) query.remove_suffix(1);

        std::shared_ptr<winforge::core::regex::SafeRegex const> expression;
        if (m_regexCheatRegexEnabled && !query.empty())
        {
            std::wstring diagnostic;
            expression = CompileSearchRegex(
                query,
                m_regexCheatRegexCaseSensitive,
                m_regexCheatRegexMultiline,
                m_regexCheatRegexDotMatchesNewline,
                diagnostic,
                m_regexCheatRegexIgnorePatternWhitespace,
                m_regexCheatRegexExplicitCapture);
            if (!expression)
            {
                m_regexCheatRegexDiagnostic = diagnostic;
                auto const status = pick(
                    L"Invalid PCRE2 filter; previous reference results remain visible. " + diagnostic,
                    L"PCRE2 篩選式無效；會保留之前嘅參考結果。" + diagnostic);
                m_regexCheatRegexStatus.Text(ToHString(status));
                AutomationProperties::SetName(m_regexCheatRegexStatus, ToHString(status));
                return;
            }
        }

        m_regexCheatRegexDiagnostic.clear();
        auto const status = m_regexCheatRegexEnabled
            ? pick(
                L"PCRE2 local reference filter is active with strict interactive safety limits.",
                L"PCRE2 本機參考篩選已開啟，設有嚴格互動安全限制。")
            : pick(
                L"Literal local reference filter is active (case-insensitive).",
                L"文字本機參考篩選已開啟（唔分大細楷）。");
        m_regexCheatRegexStatus.Text(ToHString(status));
        AutomationProperties::SetName(m_regexCheatRegexStatus, ToHString(status));

        auto const matchesRegex = [expression](winforge::core::regex::RegexCheatEntry const& entry)
        {
            if (!expression)
            {
                return false;
            }
            auto const matches = [expression](std::wstring_view field)
            {
                return expression->Search(field).matched;
            };
            return matches(entry.token) || matches(entry.description_en) ||
                matches(entry.description_zh) || matches(entry.example) ||
                matches(entry.category_en) || matches(entry.category_zh);
        };

        m_regexCheatEntryList.Children().Clear();
        std::size_t resultCount = 0;
        for (auto const& entry : winforge::core::regex::RegexCheatEntries())
        {
            auto const categoryMatches = m_regexCheatCategoryKey.empty() ||
                entry.category_key == m_regexCheatCategoryKey;
            if (!categoryMatches)
            {
                continue;
            }
            auto const match = m_regexCheatRegexEnabled
                ? (query.empty() || matchesRegex(entry))
                : winforge::core::regex::RegexCheatMatchesLiteral(entry, m_regexCheatCategoryKey, query);
            if (!match)
            {
                continue;
            }

            ++resultCount;
            Border card;
            card.Padding(Thickness{ 12, 10, 12, 10 });
            card.Margin(Thickness{ 0, 0, 0, 2 });
            StackPanel content;
            content.Spacing(5);
            auto token = CreateText(entry.token, 16, true);
            token.FontFamily(Microsoft::UI::Xaml::Media::FontFamily(L"Consolas"));
            content.Children().Append(token);
            auto category = CreateText(pick(entry.category_en, entry.category_zh), 12, true);
            category.Opacity(0.76);
            content.Children().Append(category);
            content.Children().Append(CreateText(pick(entry.description_en, entry.description_zh), 13));
            auto example = CreateText(pick(L"Example: " + std::wstring(entry.example), L"例子：" + std::wstring(entry.example)), 12);
            example.Opacity(0.82);
            example.TextWrapping(TextWrapping::Wrap);
            content.Children().Append(example);
            Button copy;
            copy.Content(box_value(ToHString(pick(L"Copy token", L"複製 token"))));
            copy.HorizontalAlignment(HorizontalAlignment::Left);
            copy.Padding(Thickness{ 12, 6, 12, 6 });
            auto suffix = std::wstring(entry.token == L"(?>a*)" ? L"atomic" : AutomationKey(entry.token));
            AutomationProperties::SetAutomationId(copy, ToHString(L"NativeRegexCheatCopyEntry_" + suffix));
            AutomationProperties::SetName(copy, ToHString(pick(L"Copy regex reference token", L"複製 regex 參考 token")));
            copy.Click([this, value = std::wstring(entry.token)](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
            {
                try
                {
                    Windows::ApplicationModel::DataTransfer::DataPackage package;
                    package.RequestedOperation(Windows::ApplicationModel::DataTransfer::DataPackageOperation::Copy);
                    package.SetText(ToHString(value));
                    Windows::ApplicationModel::DataTransfer::Clipboard::SetContent(package);
                    auto const message = winforge::core::LocalizedText{
                        L"Reference token copied to clipboard.", L"參考 token 已複製去剪貼簿。" }.Pick(m_language);
                    if (m_regexCheatCopyStatus)
                    {
                        m_regexCheatCopyStatus.Text(ToHString(message));
                        AutomationProperties::SetName(m_regexCheatCopyStatus, ToHString(message));
                    }
                }
                catch (hresult_error const&)
                {
                    auto const message = winforge::core::LocalizedText{
                        L"Clipboard is unavailable; nothing was copied.", L"剪貼簿暫時唔可用；未有複製任何內容。" }.Pick(m_language);
                    if (m_regexCheatCopyStatus)
                    {
                        m_regexCheatCopyStatus.Text(ToHString(message));
                        AutomationProperties::SetName(m_regexCheatCopyStatus, ToHString(message));
                    }
                }
            });
            content.Children().Append(copy);
            card.Child(content);
            m_regexCheatEntryList.Children().Append(card);
        }

        auto const count = pick(
            std::to_wstring(resultCount) + L" of " +
                std::to_wstring(winforge::core::regex::RegexCheatEntries().size()) + L" reference rows",
            L"共 " + std::to_wstring(resultCount) + L" / " +
                std::to_wstring(winforge::core::regex::RegexCheatEntries().size()) + L" 項參考內容");
        m_regexCheatResultCount.Text(ToHString(count));
        AutomationProperties::SetName(m_regexCheatResultCount, ToHString(count));
        if (resultCount == 0)
        {
            auto empty = CreateText(pick(L"No reference entries match the current local filter.", L"而家嘅本機篩選冇配對到參考項目。"), 14, true);
            AutomationProperties::SetAutomationId(empty, L"NativeRegexCheatEmpty");
            m_regexCheatEntryList.Children().Append(empty);
        }
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
            m_regexBuilderIgnorePatternWhitespace = m_shellRegexIgnorePatternWhitespace;
            m_regexBuilderExplicitCapture = m_shellRegexExplicitCapture;
            break;
        case RegexBuilderTarget::AllApps:
            m_regexBuilderCaseSensitive = m_allAppsRegexCaseSensitive;
            m_regexBuilderMultiline = m_allAppsRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_allAppsRegexDotMatchesNewline;
            m_regexBuilderIgnorePatternWhitespace = m_allAppsRegexIgnorePatternWhitespace;
            m_regexBuilderExplicitCapture = m_allAppsRegexExplicitCapture;
            break;
        case RegexBuilderTarget::PackageDiscover:
            m_regexBuilderCaseSensitive = m_packageSearchCaseSensitiveValue;
            m_regexBuilderMultiline = m_packageDiscoverRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_packageDiscoverRegexDotMatchesNewline;
            m_regexBuilderIgnorePatternWhitespace = m_packageDiscoverRegexIgnorePatternWhitespace;
            m_regexBuilderExplicitCapture = m_packageDiscoverRegexExplicitCapture;
            break;
        case RegexBuilderTarget::RegexCheatsheet:
            m_regexBuilderCaseSensitive = m_regexCheatRegexCaseSensitive;
            m_regexBuilderMultiline = m_regexCheatRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_regexCheatRegexDotMatchesNewline;
            m_regexBuilderIgnorePatternWhitespace = m_regexCheatRegexIgnorePatternWhitespace;
            m_regexBuilderExplicitCapture = m_regexCheatRegexExplicitCapture;
            break;
        case RegexBuilderTarget::SymbolsPalette:
            m_regexBuilderCaseSensitive = m_symbolsRegexCaseSensitive;
            m_regexBuilderMultiline = m_symbolsRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_symbolsRegexDotMatchesNewline;
            m_regexBuilderIgnorePatternWhitespace = m_symbolsRegexIgnorePatternWhitespace;
            m_regexBuilderExplicitCapture = m_symbolsRegexExplicitCapture;
            break;
        case RegexBuilderTarget::AppUninstaller:
            m_regexBuilderCaseSensitive = m_appUninstallerRegexCaseSensitive;
            m_regexBuilderMultiline = m_appUninstallerRegexMultiline;
            m_regexBuilderDotMatchesNewline = m_appUninstallerRegexDotMatchesNewline;
            m_regexBuilderIgnorePatternWhitespace =
                m_appUninstallerRegexIgnorePatternWhitespace;
            m_regexBuilderExplicitCapture = m_appUninstallerRegexExplicitCapture;
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

        if (m_regexBuilderPattern.empty())
        {
            auto const message = L"Enter a PCRE2 pattern to begin the bounded tester. / 請輸入 PCRE2 pattern 開始受限測試器。";
            m_regexBuilderStatus.Text(message);
            m_regexBuilderPreview.Text(L"No pattern: zero matches and no replacement preview. / 未輸入 pattern：0 個相符及沒有 replacement 預覽。");
            AutomationProperties::SetName(m_regexBuilderStatus, message);
            AutomationProperties::SetName(m_regexBuilderPreview, m_regexBuilderPreview.Text());
            if (m_regexBuilderMatchSummary)
            {
                m_regexBuilderMatchSummary.Text(L"0 matches / 0 個相符");
                AutomationProperties::SetName(m_regexBuilderMatchSummary, m_regexBuilderMatchSummary.Text());
            }
            if (m_regexBuilderMatchList)
            {
                m_regexBuilderMatchList.Children().Clear();
            }
            if (m_regexBuilderReplacementStatus)
            {
                m_regexBuilderReplacementStatus.Text(L"Replacement preview waits for a pattern. / replacement 預覽等待 pattern。 ");
            }
            if (m_regexBuilderReplacementPreview)
            {
                m_regexBuilderReplacementPreview.Text(L"");
            }
            if (m_regexBuilderApply)
            {
                m_regexBuilderApply.IsEnabled(false);
            }
            return;
        }

        std::wstring diagnostic;
        auto const expression = CompileSearchRegex(
            m_regexBuilderPattern,
            m_regexBuilderCaseSensitive,
            m_regexBuilderMultiline,
            m_regexBuilderDotMatchesNewline,
            diagnostic,
            m_regexBuilderIgnorePatternWhitespace,
            m_regexBuilderExplicitCapture);
        if (!expression)
        {
            if (m_regexBuilderMatchSummary)
            {
                m_regexBuilderMatchSummary.Text(L"Pattern needs correction before matches can be listed. / 請先修正 pattern 才可列出 match。 ");
                AutomationProperties::SetName(m_regexBuilderMatchSummary, m_regexBuilderMatchSummary.Text());
            }
            if (m_regexBuilderMatchList)
            {
                m_regexBuilderMatchList.Children().Clear();
            }
            if (m_regexBuilderReplacementStatus)
            {
                m_regexBuilderReplacementStatus.Text(L"Replacement preview is paused until the pattern is valid. / pattern 有效前 replacement 預覽會暫停。 ");
            }
            if (m_regexBuilderReplacementPreview)
            {
                m_regexBuilderReplacementPreview.Text(L"");
            }
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

        auto const matchSet = expression->FindAll(m_regexBuilderTestText, true);
        if (m_regexBuilderMatchList)
        {
            m_regexBuilderMatchList.Children().Clear();
        }
        auto const matchSetStopped = matchSet.resource_limit_exceeded
            || matchSet.input_limit_exceeded
            || matchSet.invalid_utf16
            || matchSet.result_limit_exceeded;
        if (matchSetStopped)
        {
            auto const message = L"Match set stopped safely: "
                + (matchSet.diagnostic.empty()
                    ? L"a configured regex safety limit was reached."
                    : matchSet.diagnostic);
            m_regexBuilderPreview.Text(ToHString(message));
            AutomationProperties::SetName(m_regexBuilderPreview, ToHString(message));
            if (m_regexBuilderMatchSummary)
            {
                m_regexBuilderMatchSummary.Text(ToHString(message));
                AutomationProperties::SetName(m_regexBuilderMatchSummary, ToHString(message));
            }
            if (m_regexBuilderReplacementStatus)
            {
                m_regexBuilderReplacementStatus.Text(
                    L"Replacement preview is withheld while the match set is capped or stopped safely. / match set 受限或安全停止時不會產生 replacement 預覽。 ");
            }
            if (m_regexBuilderReplacementPreview)
            {
                m_regexBuilderReplacementPreview.Text(L"");
            }
            return;
        }

        auto const matchSummary = std::to_wstring(matchSet.matches.size())
            + L" non-overlapping match(es), bounded to 100. / "
            + std::to_wstring(matchSet.matches.size()) + L" 個不重疊相符（最多 100 個）。";
        m_regexBuilderPreview.Text(ToHString(matchSummary));
        AutomationProperties::SetName(m_regexBuilderPreview, ToHString(matchSummary));
        if (m_regexBuilderMatchSummary)
        {
            m_regexBuilderMatchSummary.Text(ToHString(matchSummary));
            AutomationProperties::SetName(m_regexBuilderMatchSummary, ToHString(matchSummary));
        }
        if (m_regexBuilderMatchList)
        {
            for (std::size_t matchIndex = 0; matchIndex < matchSet.matches.size(); ++matchIndex)
            {
                auto const& occurrence = matchSet.matches[matchIndex];
                std::wstringstream row;
                row << L"Match " << (matchIndex + 1)
                    << L": index " << occurrence.start
                    << L", length " << occurrence.length;
                bool hasCaptures{};
                for (std::size_t captureIndex = 1;
                     captureIndex < occurrence.captures.size();
                     ++captureIndex)
                {
                    auto const& capture = occurrence.captures[captureIndex];
                    if (!capture.matched)
                    {
                        continue;
                    }
                    row << (hasCaptures ? L"; " : L". Captures: ");
                    row << (capture.name.empty()
                        ? L"#" + std::to_wstring(captureIndex)
                        : L"${" + capture.name + L"}");
                    row << L" at " << capture.start << L", length " << capture.length;
                    hasCaptures = true;
                }
                if (!hasCaptures && occurrence.captures.size() > 1)
                {
                    row << L". Captures: none participated.";
                }
                auto detail = CreateText(row.str(), 12);
                AutomationProperties::SetAutomationId(
                    detail,
                    ToHString(L"NativeRegexMatch_" + std::to_wstring(matchIndex + 1)));
                AutomationProperties::SetName(detail, ToHString(row.str()));
                m_regexBuilderMatchList.Children().Append(detail);
            }
        }

        auto const replacement = expression->ReplaceAll(
            m_regexBuilderTestText,
            m_regexBuilderReplacement);
        if (m_regexBuilderReplacementPreview)
        {
            m_regexBuilderReplacementPreview.Text(ToHString(
                replacement.output_limit_exceeded || replacement.invalid_replacement
                    || replacement.resource_limit_exceeded || replacement.input_limit_exceeded
                    || replacement.invalid_utf16 || replacement.result_limit_exceeded
                    ? L""
                    : replacement.output));
            AutomationProperties::SetName(
                m_regexBuilderReplacementPreview,
                L"Local bounded replacement preview output / 本機受限 replacement 預覽輸出");
        }
        if (m_regexBuilderReplacementStatus)
        {
            std::wstring replacementStatus;
            if (replacement.output_limit_exceeded || replacement.invalid_replacement
                || replacement.resource_limit_exceeded || replacement.input_limit_exceeded
                || replacement.invalid_utf16 || replacement.result_limit_exceeded)
            {
                replacementStatus = L"Replacement preview was not generated: "
                    + (replacement.diagnostic.empty()
                        ? L"a configured local safety or syntax rule was reached."
                        : replacement.diagnostic);
            }
            else
            {
                replacementStatus = std::to_wstring(replacement.substitutions)
                    + L" replacement substitution(s). Supports $$, $0-$99, and ${name}; local preview only. / "
                    + std::to_wstring(replacement.substitutions)
                    + L" 個 replacement；支援 $$、$0-$99 及 ${name}，只作本機預覽。";
            }
            m_regexBuilderReplacementStatus.Text(ToHString(replacementStatus));
            AutomationProperties::SetName(
                m_regexBuilderReplacementStatus,
                ToHString(replacementStatus));
        }
    }

    void MainWindow::ApplyRegexBuilderTarget()
    {
        std::wstring diagnostic;
        auto const expression = CompileSearchRegex(
            m_regexBuilderPattern,
            m_regexBuilderCaseSensitive,
            m_regexBuilderMultiline,
            m_regexBuilderDotMatchesNewline,
            diagnostic,
            m_regexBuilderIgnorePatternWhitespace,
            m_regexBuilderExplicitCapture);
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
            m_shellRegexIgnorePatternWhitespace = m_regexBuilderIgnorePatternWhitespace;
            m_shellRegexExplicitCapture = m_regexBuilderExplicitCapture;
            if (m_shellRegexMode) m_shellRegexMode.IsOn(true);
            if (m_search) m_search.Text(ToHString(m_regexBuilderPattern));
            Navigate(L"search", m_regexBuilderPattern);
            break;
        case RegexBuilderTarget::AllApps:
            m_allAppsRegexEnabled = true;
            m_allAppsRegexCaseSensitive = m_regexBuilderCaseSensitive;
            m_allAppsRegexMultiline = m_regexBuilderMultiline;
            m_allAppsRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            m_allAppsRegexIgnorePatternWhitespace = m_regexBuilderIgnorePatternWhitespace;
            m_allAppsRegexExplicitCapture = m_regexBuilderExplicitCapture;
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
            m_packageDiscoverRegexIgnorePatternWhitespace = m_regexBuilderIgnorePatternWhitespace;
            m_packageDiscoverRegexExplicitCapture = m_regexBuilderExplicitCapture;
            m_packageDiscoverRegexPattern = m_regexBuilderPattern;
            m_packageView = static_cast<int32_t>(winforge::core::packages::PackageView::Discover);
            m_packageRetainCachedResultsOnNextRender = retainCachedDiscover;
            SavePackageManagerState();
            Navigate(L"module.packages", L"discover");
            break;
        }
        case RegexBuilderTarget::RegexCheatsheet:
            m_regexCheatRegexEnabled = true;
            m_regexCheatRegexCaseSensitive = m_regexBuilderCaseSensitive;
            m_regexCheatRegexMultiline = m_regexBuilderMultiline;
            m_regexCheatRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            m_regexCheatRegexIgnorePatternWhitespace = m_regexBuilderIgnorePatternWhitespace;
            m_regexCheatRegexExplicitCapture = m_regexBuilderExplicitCapture;
            m_regexCheatSearchText = m_regexBuilderPattern;
            Navigate(L"module.regexcheat");
            break;
        case RegexBuilderTarget::SymbolsPalette:
            m_symbolsRegexEnabled = true;
            m_symbolsRegexCaseSensitive = m_regexBuilderCaseSensitive;
            m_symbolsRegexMultiline = m_regexBuilderMultiline;
            m_symbolsRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            m_symbolsRegexIgnorePatternWhitespace = m_regexBuilderIgnorePatternWhitespace;
            m_symbolsRegexExplicitCapture = m_regexBuilderExplicitCapture;
            m_symbolsSearchText = m_regexBuilderPattern;
            Navigate(L"module.symbols");
            break;
        case RegexBuilderTarget::AppUninstaller:
            m_appUninstallerRegexEnabled = true;
            m_appUninstallerRegexCaseSensitive = m_regexBuilderCaseSensitive;
            m_appUninstallerRegexMultiline = m_regexBuilderMultiline;
            m_appUninstallerRegexDotMatchesNewline = m_regexBuilderDotMatchesNewline;
            m_appUninstallerRegexIgnorePatternWhitespace =
                m_regexBuilderIgnorePatternWhitespace;
            m_appUninstallerRegexExplicitCapture = m_regexBuilderExplicitCapture;
            m_appUninstallerSearchText = m_regexBuilderPattern;
            Navigate(L"module.uninstall");
            break;
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
        m_regexBuilderTestTextBox = nullptr;
        m_regexBuilderReplacementBox = nullptr;
        m_regexBuilderMatchSummary = nullptr;
        m_regexBuilderMatchList = nullptr;
        m_regexBuilderReplacementPreview = nullptr;
        m_regexBuilderReplacementStatus = nullptr;

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
                std::clamp(
                    sender.as<ComboBox>().SelectedIndex(),
                    0,
                    static_cast<int32_t>(winforge::core::regex::RegexSearchSurfaces().size())));
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
            addFlag(
                L"Ignore pattern whitespace (x) / 忽略 pattern 空白 (x)",
                m_regexBuilderIgnorePatternWhitespace,
                [this](bool value) { m_regexBuilderIgnorePatternWhitespace = value; RefreshRegexTesterPreview(); },
                L"NativeRegexBuilderIgnorePatternWhitespace");
            addFlag(
                L"Named captures only (n) / 只保留命名 capture (n)",
                m_regexBuilderExplicitCapture,
                [this](bool value) { m_regexBuilderExplicitCapture = value; RefreshRegexTesterPreview(); },
                L"NativeRegexBuilderExplicitCapture");
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

            m_regexBuilderReplacementBox = TextBox();
            m_regexBuilderReplacementBox.Header(box_value(
                L"Replacement preview ($$, $0-$99, ${name}) / ??? preview"));
            m_regexBuilderReplacementBox.PlaceholderText(
                L"For example: ${name}-$1-$$ (local preview only)");
            m_regexBuilderReplacementBox.Text(ToHString(m_regexBuilderReplacement));
            m_regexBuilderReplacementBox.TextWrapping(TextWrapping::Wrap);
            AutomationProperties::SetAutomationId(
                m_regexBuilderReplacementBox,
                L"NativeRegexReplacementInput");
            AutomationProperties::SetName(
                m_regexBuilderReplacementBox,
                L"Local bounded regex replacement preview input / 本機受限 regex replacement 預覽輸入");
            m_regexBuilderReplacementBox.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
            {
                m_regexBuilderReplacement = ToWide(sender.as<TextBox>().Text());
                RefreshRegexTesterPreview();
            });
            stepContent.Children().Append(m_regexBuilderReplacementBox);
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

        m_regexBuilderMatchSummary = CreateText(L"", 12, true);
        m_regexBuilderMatchSummary.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(
            m_regexBuilderMatchSummary,
            L"NativeRegexMatchSummary");
        AutomationProperties::SetLiveSetting(
            m_regexBuilderMatchSummary,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_regexBuilderMatchSummary);

        m_regexBuilderMatchList = StackPanel();
        m_regexBuilderMatchList.Spacing(4);
        AutomationProperties::SetAutomationId(m_regexBuilderMatchList, L"NativeRegexMatchList");
        page.Children().Append(m_regexBuilderMatchList);

        m_regexBuilderReplacementStatus = CreateText(L"", 12, true);
        m_regexBuilderReplacementStatus.TextWrapping(TextWrapping::Wrap);
        AutomationProperties::SetAutomationId(
            m_regexBuilderReplacementStatus,
            L"NativeRegexReplacementStatus");
        AutomationProperties::SetLiveSetting(
            m_regexBuilderReplacementStatus,
            Microsoft::UI::Xaml::Automation::Peers::AutomationLiveSetting::Polite);
        page.Children().Append(m_regexBuilderReplacementStatus);

        m_regexBuilderReplacementPreview = TextBox();
        m_regexBuilderReplacementPreview.Header(box_value(
            L"Replacement preview output (local only) / replacement 預覽輸出（只限本機）"));
        m_regexBuilderReplacementPreview.IsReadOnly(true);
        m_regexBuilderReplacementPreview.AcceptsReturn(true);
        m_regexBuilderReplacementPreview.TextWrapping(TextWrapping::Wrap);
        m_regexBuilderReplacementPreview.MinHeight(84);
        m_regexBuilderReplacementPreview.MaxHeight(180);
        AutomationProperties::SetAutomationId(
            m_regexBuilderReplacementPreview,
            L"NativeRegexReplacementPreview");
        AutomationProperties::SetName(
            m_regexBuilderReplacementPreview,
            L"Local bounded replacement preview output / 本機受限 replacement 預覽輸出");
        page.Children().Append(m_regexBuilderReplacementPreview);

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
        std::wstring& diagnostic,
        bool ignorePatternWhitespace,
        bool explicitCapture) const
    {
        winforge::core::regex::RegexOptions options;
        options.case_sensitive = caseSensitive;
        options.multiline = multiline;
        options.dot_matches_newline = dotMatchesNewline;
        options.ignore_pattern_whitespace = ignorePatternWhitespace;
        options.explicit_capture = explicitCapture;
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
