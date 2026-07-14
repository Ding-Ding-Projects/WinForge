#include "pch.h"
#include "MainWindow.xaml.h"

#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "CatalogLoader.h"
#include "../WinForge.Core/PackageParsers.h"
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
        });

        try
        {
            ConfigureWindow();
            m_modules = winforge::app::LoadModuleCatalog();
            BuildAliasIndex();
            BuildShell();
            m_packageStatePath = PackageManagerStatePath();
            LoadPackageManagerState();

            auto const request = winforge::core::CurrentProcessLaunchRequest();
            Navigate(request.route, request.argument, true);
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

        m_navigation = NavigationView();
        m_navigation.PaneTitle(L"WinForge · 視窗調校");
        m_navigation.PaneDisplayMode(NavigationViewPaneDisplayMode::Auto);
        m_navigation.IsBackButtonVisible(NavigationViewBackButtonVisible::Collapsed);
        m_navigation.IsSettingsVisible(true);
        m_navigation.AlwaysShowHeader(false);
        AutomationProperties::SetAutomationId(m_navigation, L"NativeShellNavigation");

        m_search = AutoSuggestBox();
        m_search.PlaceholderText(L"Search every native route · 搜尋全部原生路線");
        AutomationProperties::SetAutomationId(m_search, L"NativeShellSearchBox");
        m_search.QuerySubmitted({ this, &MainWindow::OnSearchSubmitted });
        m_navigation.AutoSuggestBox(m_search);

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
        m_navigation.PaneFooter(m_languagePicker);

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

    void MainWindow::Navigate(std::wstring_view route, std::wstring_view argument, bool deepLink)
    {
        if (m_currentRoute == L"module.packages")
        {
            CancelPackageWork();
        }
        auto normalized = winforge::core::NormalizeRouteKey(route);
        if (normalized == L"search")
        {
            m_currentRoute = L"search";
            m_currentArgument = std::wstring(argument);
            RenderCurrent();
            return;
        }
        if (normalized == L"manual" && !argument.empty())
        {
            m_currentRoute = L"manual";
            m_currentArgument = std::wstring(argument);
            RenderCurrent();
            return;
        }

        auto const* module = deepLink ? FindLaunchModule(normalized) : FindModule(normalized);
        if (!module)
        {
            m_currentRoute = normalized;
            m_currentArgument = std::wstring(argument);
            RenderUnknown(normalized);
            return;
        }

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
        else if (module->id == L"module.caseconvert")
        {
            RenderCaseConvert();
        }
        else if (module->id == L"module.guidgen")
        {
            RenderGuidGen();
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
            filter.Content(box_value(ToHString(
                winforge::core::LocalizedText{
                    std::wstring(manager.name_en), std::wstring(manager.name_zh) }.Pick(m_language))));
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
            filter.Margin(Thickness{ 0, 0, 14, 0 });
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
                    entry.title = object.HasKey(L"title") ? ToWide(object.GetNamedString(L"title")) : std::wstring{};
                    entry.details = object.HasKey(L"details") ? ToWide(object.GetNamedString(L"details")) : std::wstring{};
                    entry.status = object.HasKey(L"status") ? ToWide(object.GetNamedString(L"status")) : std::wstring{};
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
                    entry.details = ToWide(value.GetString());
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
                m_packageSearchBox.Text(ToHString(m_packageSearchText));
            }
            if (m_packageSortPicker)
            {
                m_packageSortPicker.SelectedIndex(m_packageSortMode);
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
        entry.title = std::move(title);
        entry.details = std::move(details);
        entry.status = std::move(status);
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
        if (m_packageItems.empty())
        {
            AnnouncePackageStatus(
                L"No update rows are loaded yet; refresh updates before previewing Update all.",
                L"未載入可更新資料列；預覽全部更新之前請先重新整理更新。",
                true);
            return;
        }

        constexpr std::size_t MaximumPreviewedUpdates = 25;
        std::size_t previewed = 0;
        std::size_t failed = 0;
        RecordPackageOperation(
            L"Preview-only Update all plan started for " +
                std::to_wstring(m_packageItems.size()) +
                L" update row(s). No package command was executed. · 只建立全部更新預覽，冇執行套件指令。");

        for (auto const& package : m_packageItems)
        {
            auto const command = winforge::core::packages::BuildPackageActionCommand(
                package.manager_key,
                package.id,
                package.source,
                winforge::core::packages::PackageAction::Update);
            if (!command)
            {
                ++failed;
                if (previewed < MaximumPreviewedUpdates)
                {
                    RecordPackageOperation(
                        L"Preview failed for update " + package.id +
                            L" via " + package.manager_key + L": " + command.error_code +
                            L". · 更新預覽失敗。");
                    ++previewed;
                }
                continue;
            }

            if (previewed < MaximumPreviewedUpdates)
            {
                RecordPackageOperation(
                    L"Preview-only update plan for " + package.id +
                        L" via " + package.manager_key + L": " +
                        winforge::core::packages::FormatCommandPreview(*command.command) +
                        L". · 只建立更新預覽。");
                ++previewed;
            }
        }
        if (m_packageItems.size() > MaximumPreviewedUpdates)
        {
            RecordPackageOperation(
                L"Update-all preview rendered the first " +
                    std::to_wstring(MaximumPreviewedUpdates) +
                    L" rows; remaining rows are omitted from the visible history to keep the UI responsive. · 全部更新預覽只顯示頭批資料列。");
        }
        if (failed != 0)
        {
            RecordPackageOperation(
                L"Update-all preview had " + std::to_wstring(failed) +
                    L" validation failure(s). · 全部更新預覽有驗證失敗。");
        }

        m_packageView = 8;
        if (m_packageViewPicker)
        {
            m_packageViewPicker.SelectedIndex(m_packageView);
        }
        SavePackageManagerState();
        RenderPackageManagerView();
        AnnouncePackageStatus(
            L"Update-all preview added to Operations. No package command was executed.",
            L"全部更新預覽已加入操作檢視；冇執行套件指令。",
            failed != 0);
    }

    std::wstring MainWindow::BundleSnapshotToJson(
        std::vector<winforge::core::packages::PackageItem> const& items) const
    {
        std::wstring json = LR"({"export_version":1,"packages":[)";
        bool first = true;
        for (auto const& item : items)
        {
            if (!first) json += L',';
            first = false;
            json += LR"({"Id":")";
            json += EscapeJson(item.id);
            json += LR"(","Name":")";
            json += EscapeJson(item.name);
            json += LR"(","Version":")";
            json += EscapeJson(item.version);
            json += LR"(","Source":")";
            json += EscapeJson(item.source);
            json += LR"(","ManagerName":")";
            json += EscapeJson(item.manager_key);
            json += L"}";
        }
        json += LR"(],"incompatible_packages":[]})";
        return json;
    }

    bool MainWindow::SaveBundleSnapshot(std::wstring_view path) const
    {
        try
        {
            auto const sourceItems = !m_packageBundleItems.empty() ? m_packageBundleItems : m_packageItems;
            std::ofstream output(std::filesystem::path(path), std::ios::binary | std::ios::trunc);
            if (!output)
            {
                return false;
            }
            output << winrt::to_string(BundleSnapshotToJson(sourceItems));
            return static_cast<bool>(output);
        }
        catch (...)
        {
            return false;
        }
    }

    bool MainWindow::LoadBundleSnapshot(std::wstring_view path)
    {
        try
        {
            std::ifstream input(std::filesystem::path(path), std::ios::binary);
            if (!input)
            {
                return false;
            }

            std::ostringstream stream;
            stream << input.rdbuf();
            auto json = stream.str();
            if (json.empty())
            {
                return false;
            }

            auto const root = winrt::Windows::Data::Json::JsonObject::Parse(winrt::to_hstring(json));
            auto const packages = root.GetNamedArray(L"packages");
            std::vector<winforge::core::packages::PackageItem> loaded;
            loaded.reserve(packages.Size());
            for (auto const& entry : packages)
            {
                auto const obj = entry.GetObject();
                winforge::core::packages::PackageItem item;
                item.id = ToWide(obj.GetNamedString(L"Id", L""));
                item.name = ToWide(obj.GetNamedString(L"Name", L""));
                item.version = ToWide(obj.GetNamedString(L"Version", L""));
                item.source = ToWide(obj.GetNamedString(L"Source", L""));
                item.manager_key = ToWide(obj.GetNamedString(L"ManagerName", L""));
                if (!item.manager_key.empty() && !item.id.empty())
                {
                    loaded.push_back(std::move(item));
                }
            }

            m_packageBundleItems = std::move(loaded);
            m_packageBundleSourcePath = std::wstring(path);
            m_packageView = 3;
            if (m_packageViewPicker)
            {
                m_packageViewPicker.SelectedIndex(m_packageView);
            }
            RenderPackageManagerView();
            SavePackageManagerState();
            return true;
        }
        catch (...)
        {
            return false;
        }
    }

    std::wstring MainWindow::PromptBundleOpenPath() const
    {
        return PromptOpenBundlePath(nullptr);
    }

    std::wstring MainWindow::PromptBundleSavePath() const
    {
        auto const suggested = m_packageBundleItems.empty() ? L"package-bundle.json" : L"package-bundle.ubundle";
        return PromptSaveBundlePath(nullptr, suggested);
    }

    void MainWindow::RenderPackageManager()
    {
        CancelPackageWork();
        m_packageItems.clear();
        m_packageRunStates.clear();
        m_packageDetailsTarget.clear();
        m_packageLastAction = winforge::core::packages::PackageAction::Probe;
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
            L"Availability, discovery, installed-package, update, details, and source queries run through audited C++ argv builders, parsers, HTTPS transport, and a contained Win32 process runner. Mutations remain locked until the native consent coordinator is proven.",
            L"可用性、搜尋、已安裝套件、更新、詳細資料同來源查詢，會經審核嘅 C++ argv 建立器、解析器、HTTPS transport 同受控 Win32 process runner 執行；修改操作要等原生同意協調器驗證完成先會解鎖。" }.Pick(m_language)));
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
        ScrollViewer managerScroller;
        managerScroller.HorizontalScrollMode(ScrollMode::Auto);
        managerScroller.HorizontalScrollBarVisibility(ScrollBarVisibility::Auto);
        managerScroller.VerticalScrollMode(ScrollMode::Disabled);
        managerScroller.VerticalScrollBarVisibility(ScrollBarVisibility::Disabled);
        m_packageManagerFilters = StackPanel();
        m_packageManagerFilters.Orientation(Orientation::Horizontal);
        AutomationProperties::SetAutomationId(m_packageManagerFilters, L"NativePackageManagerFilters");
        PopulatePackageManagerFilters(m_packageManagerFilters);
        managerScroller.Content(m_packageManagerFilters);
        managerCardContent.Children().Append(managerScroller);
        managerCard.Child(managerCardContent);
        page.Children().Append(managerCard);

        StackPanel toolbar;
        toolbar.Orientation(Orientation::Horizontal);
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
        m_packageSearchBox.PlaceholderText(ToHString(winforge::core::LocalizedText{
            L"Search packages (for example: vscode, vlc, obs)",
            L"搜尋套件（例如 vscode、vlc、obs）" }.Pick(m_language)));
        m_packageSearchBox.Text(ToHString(m_packageSearchText));
        AutomationProperties::SetAutomationId(m_packageSearchBox, L"NativePackageSearchBox");
        m_packageSearchBox.QuerySubmitted([this](AutoSuggestBox const&, AutoSuggestBoxQuerySubmittedEventArgs const&)
        {
            if (m_packageStateApplying)
            {
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
                    if (LoadBundleSnapshot(openPath))
                    {
                        RecordPackageOperation(
                            L"Imported native bundle snapshot from " + openPath + L". · 已匯入 bundle。");
                        AnnouncePackageStatus(
                            L"Bundle snapshot imported.",
                            L"Bundle 快照已匯入。");
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

        m_packageResults = StackPanel();
        m_packageResults.Spacing(8);
        AutomationProperties::SetAutomationId(m_packageResults, L"NativePackageResults");
        page.Children().Append(m_packageResults);

        ShowPage(page);
        RenderPackageManagerView();
        StartPackageManagerProbes();
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

        auto query = m_packageSearchBox ? ToWide(m_packageSearchBox.Text()) : std::wstring{};
        if (action == PackageAction::Search && !HasNonWhitespace(query))
        {
            RenderPackageManagerView();
            return;
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

        CancelPackageWork();
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

        m_packageSearchBox.IsEnabled(m_packageView == 0 && !m_packageWorking);
        m_packagePrimaryAction.Visibility(Visibility::Visible);
        m_packageSecondaryAction.Visibility(Visibility::Collapsed);
        m_packagePrimaryAction.IsEnabled(false);
        m_packageSecondaryAction.IsEnabled(false);
        if (m_packageBusy) m_packageBusy.IsActive(m_packageWorking);

        std::wstring header;
        std::wstring explanation;
        bool readOnlyQuery = false;
        switch (m_packageView)
        {
        case 0:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Search", L"搜尋"))));
            header = pick(L"Discover packages", L"搜尋套件");
            explanation = pick(
                L"Searches every selected, available engine concurrently using validated argv or an allowlisted HTTPS endpoint. Results are read-only while install consent is being ported.",
                L"會同時搜尋所有已選而且可用嘅引擎，只會用已驗證 argv 或准許清單內 HTTPS endpoint；安裝同意流程移植完成之前，結果保持只讀。");
            readOnlyQuery = true;
            break;
        case 1:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            m_packageSecondaryAction.Content(box_value(ToHString(pick(L"Update all", L"全部更新"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Available updates", L"可用更新");
            explanation = pick(
                L"Live update enumeration runs per selected engine. Update buttons remain locked until the native consent and operation coordinator is proven.",
                L"會按已選引擎即時列出更新；原生同意同操作協調器驗證完成之前，更新按鈕保持鎖住。");
            readOnlyQuery = true;
            break;
        case 2:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            header = pick(L"Installed packages", L"已安裝套件");
            explanation = pick(
                L"Installed packages are enumerated live. Uninstall remains locked until explicit confirmation, elevation, and cancellation behavior pass their native tests.",
                L"會即時列出已安裝套件；明確確認、提升權限同取消行為通過原生測試之前，解除安裝保持鎖住。");
            readOnlyQuery = true;
            break;
        case 3:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Export bundle", L"匯出 bundle"))));
            m_packageSecondaryAction.Content(box_value(ToHString(pick(L"Import bundle", L"匯入 bundle"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Portable package bundles", L"可攜套件清單");
            explanation = pick(
                L"Native bundle snapshots can now be exported from the current package list and imported back into this tab as a preview.",
                L"原生 bundle 快照而家可以由目前套件清單匯出，亦可以匯入返嚟呢個分頁做預覽。");
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
                L"Non-destructive version probes show which engines are ready. Bootstrap installs remain disabled until the normal-integrity coordinator is complete.",
                L"非破壞性版本探測會顯示邊啲引擎可用；正常 integrity 協調器完成之前，bootstrap 安裝保持停用。");
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
                m_packageView == 8 ? pick(L"Clear history", L"清除歷史") : pick(L"Clear completed", L"清除已完成"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Operation queue and history", L"操作佇列同歷史");
            explanation = pick(L"The native coordinator will show queued, running, completed, failed, cancelled, output, retry, and cancellation state here.",
                L"原生協調器會喺呢度顯示排隊、執行中、完成、失敗、已取消、輸出、重試同取消狀態。");
            break;
        }

        auto const selectedAvailable = HasSelectedAvailablePackageManager();
        auto const searchReady = m_packageView != 0 || HasNonWhitespace(ToWide(m_packageSearchBox.Text()));
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
            m_packagePrimaryAction.IsEnabled(
                !m_packageWorking && (m_packageView != 5 ||
                    !m_packageIgnoredRules.empty() ||
                    !m_packagePinnedRules.empty() ||
                    !m_packageSnoozedRules.empty()));
            if (m_packageView == 3)
            {
                m_packageSecondaryAction.IsEnabled(!m_packageWorking);
            }
        }
        else if (m_packageView == 8)
        {
            m_packageSecondaryAction.IsEnabled(!m_packageWorking && !m_packageOperations.empty());
        }

        auto resultHeader = header;
        if (!m_packageItems.empty())
        {
            resultHeader += L" · " + std::to_wstring(m_packageItems.size()) + pick(L" results", L" 個結果");
        }
        m_packageResultsHeader.Text(ToHString(resultHeader));
        appendCard(explanation, {}, L"NativePackageViewSummary", 0.88);

        if (m_packageWorking)
        {
            appendCard(
                pick(L"Native operation in progress", L"原生操作進行中"),
                pick(L"The page stays responsive while contained processes and HTTPS requests run in the background. Starting another request cancels this generation.",
                    L"受控 process 同 HTTPS request 喺背景執行期間，頁面會保持流暢；開始另一個要求就會取消今次 generation。"),
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
            return;
        }

        if (m_packageView == 8)
        {
            appendCard(
                pick(L"Preview queue policy", L"預覽佇列政策"),
                pick(L"Operation entries are durable preview plans. Run next, run last, and retry only reorder or mark the native queue; they do not execute package-manager mutation commands.",
                    L"操作項目係可保存嘅預覽計劃。「下一個」、「最後執行」同「重試」只會重新排序或者標記原生佇列；唔會執行套件修改指令。"),
                L"NativePackageQueueSummary",
                0.9);

            if (m_packageOperations.empty())
            {
                appendCard(
                    pick(L"No native operations yet", L"暫時未有原生操作"),
                    pick(L"Availability probes, read-only queries, and preview-only install/update/uninstall plans will appear here. Mutating operation execution is not claimed yet.",
                        L"可用性探測、只讀查詢，以及只供預覽嘅安裝／更新／解除安裝計劃會喺度顯示；暫時未聲稱會執行修改操作。"),
                    L"NativePackageOperationsEmpty");
            }
            else
            {
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
                    actions.Orientation(Orientation::Horizontal);
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
                    L"Package view, search text, and manager filter selections can now be persisted locally in native JSON state.",
                    L"套件檢視、搜尋文字同管理器篩選而家可以用原生 JSON 狀態喺本機保存。"),
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
                L"Remember manager filters",
                L"記住管理器篩選",
                L"Persist which package engines are selected for the next launch.",
                L"保存下次啟動時揀咗邊啲套件引擎。",
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
                    L"Clear the saved view, search text, and manager filter choices, then write a fresh default state file.",
                    L"清走已保存嘅檢視、搜尋文字同管理器篩選，然後寫入一個新嘅預設狀態檔。"),
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
            if (m_packageBundleItems.empty())
            {
                ApplyPackageSort();
            }
            auto bundleItems = m_packageBundleItems.empty() ? m_packageItems : m_packageBundleItems;
            winforge::core::packages::SortPackageItems(
                bundleItems,
                static_cast<winforge::core::packages::PackageSortMode>(m_packageSortMode));
            if (m_packageBundleSourcePath.empty())
            {
                appendCard(
                    pick(L"No bundle snapshot loaded", L"未載入 bundle 快照"),
                    pick(
                        L"Use Export bundle to save the current package list, or Import bundle to load a native JSON/.ubundle snapshot into this tab.",
                        L"用「匯出 bundle」可以保存目前套件清單，或者用「匯入 bundle」將原生 JSON／.ubundle 快照載入到呢個分頁。"),
                    L"NativeBundleEmpty");
            }
            else
            {
                std::wstring sourceLine = pick(
                    L"Loaded from " + m_packageBundleSourcePath,
                    L"由 " + m_packageBundleSourcePath + L" 載入。");
                appendCard(
                    pick(L"Loaded bundle snapshot", L"已載入 bundle 快照"),
                    TruncateForUi(sourceLine),
                    L"NativeBundleSource");
            }

            for (std::size_t index = 0; index < bundleItems.size(); ++index)
            {
                auto const& package = bundleItems[index];
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
                    L"NativeBundlePackage_" + std::to_wstring(index));
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
        auto const renderCount = std::min(m_packageItems.size(), MaximumRenderedPackages);
        for (std::size_t index = 0; index < renderCount; ++index)
        {
            auto const& package = m_packageItems[index];
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
            actions.Orientation(Orientation::Horizontal);
            actions.Spacing(8);

            Button mutation;
            auto const actionLabel = m_packageView == 0
                ? pick(L"Install", L"安裝")
                : m_packageView == 1
                    ? pick(L"Update", L"更新")
                    : pick(L"Uninstall", L"解除安裝");
            mutation.Content(box_value(ToHString(actionLabel)));
            mutation.IsEnabled(!m_packageWorking);
            mutation.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(
                mutation,
                ToHString(L"NativePackageMutation_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
            AutomationProperties::SetName(
                mutation,
                ToHString(actionLabel + L" preview for " + (package.name.empty() ? package.id : package.name)));
            ToolTipService::SetToolTip(
                mutation,
                box_value(ToHString(pick(
                    L"Preview the exact native argv operation plan. This does not execute the package command.",
                    L"預覽準確嘅原生 argv 操作計劃；唔會執行套件指令。"))));
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
                PreviewPackageOperation(packageCopy, action);
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

        if (m_packageItems.size() > MaximumRenderedPackages)
        {
            appendCard(
                pick(L"Result rendering capped", L"結果顯示設有上限"),
                pick(L"The native query completed, but this page renders the first 250 rows to protect UI responsiveness.",
                    L"原生查詢已完成，但為咗保持介面流暢，呢頁只顯示頭 250 筆。"),
                L"NativePackageResultLimit");
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
        auto page = CreatePage(
            L"All Apps · 所有 app",
            L"Every fixed native parity route is discoverable here. Pending entries are intentionally labelled and are not counted as ported. · 所有固定原生對等路線都可以喺呢度搵到；未完成項目會清楚標示，唔會當成已移植。");

        TextBox filter;
        filter.PlaceholderText(L"Filter 346 routes · 篩選 346 條路線");
        filter.Text(ToHString(query));
        filter.Margin(Thickness{ 0, 0, 0, 8 });
        AutomationProperties::SetAutomationId(filter, L"NativeAllAppsSearchBox");
        filter.TextChanged([this](Windows::Foundation::IInspectable const& sender, TextChangedEventArgs const&)
        {
            auto const box = sender.as<TextBox>();
            PopulateAllApps(ToWide(box.Text()));
        });
        page.Children().Append(filter);

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
        PopulateAllApps(query);
        ShowPage(page);

        filter.Focus(FocusState::Programmatic);
        filter.SelectionStart(static_cast<int32_t>(filter.Text().size()));
    }

    void MainWindow::PopulateAllApps(std::wstring_view query)
    {
        if (!m_allAppsList || !m_allAppsCount)
        {
            return;
        }

        m_allAppsList.Items().Clear();
        std::size_t visible = 0;
        for (auto const& module : m_modules)
        {
            if (!Matches(module, query)) continue;

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
        m_allAppsCount.Text(ToHString(result.str()));
    }

    void MainWindow::RenderSearch(std::wstring_view query)
    {
        auto page = CreatePage(
            L"Search results · 搜尋結果",
            L"Native catalog search spans names, Cantonese labels, keywords, route ids, and aliases. · 原生目錄搜尋涵蓋英文名、粵語名、關鍵字、路線 id 同別名。");

        auto const normalized = winforge::core::NormalizeRouteKey(query);
        std::size_t matches = 0;
        for (auto const& module : m_modules)
        {
            if (!Matches(module, normalized)) continue;
            page.Children().Append(CreateRouteButton(Label(module), module.id));
            if (++matches == 60) break;
        }
        if (matches == 0)
        {
            page.Children().Append(CreateText(L"No matching route · 搵唔到相符路線", 16, true));
        }
        ShowPage(page);
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

    bool MainWindow::Matches(winforge::core::ModuleRecord const& module, std::wstring_view query) const
    {
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
        viewer.HorizontalScrollMode(ScrollMode::Disabled);
        viewer.HorizontalScrollBarVisibility(ScrollBarVisibility::Disabled);
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
        auto const query = ToWide(args.QueryText());
        if (query.empty()) return;
        if (FindModule(query))
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
