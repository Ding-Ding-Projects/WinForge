#include "pch.h"
#include "MainWindow.xaml.h"

#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "CatalogLoader.h"
#include "microsoft.ui.xaml.window.h"
#include <winrt/Microsoft.UI.Xaml.Automation.Peers.h>

#include <chrono>
#include <cwctype>
#include <array>
#include <future>
#include <sstream>
#include <stdexcept>
#include <utility>

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
                auto const check = sender.as<CheckBox>();
                auto const keyValue = unbox_value_or<hstring>(check.Tag(), L"");
                m_packageManagersSelected[ToWide(keyValue)] = true;
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
                auto const check = sender.as<CheckBox>();
                auto const keyValue = unbox_value_or<hstring>(check.Tag(), L"");
                m_packageManagersSelected[ToWide(keyValue)] = false;
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

    void MainWindow::RenderPackageManager()
    {
        CancelPackageWork();
        m_packageItems.clear();
        m_packageRunStates.clear();
        m_packageView = PackageViewFromArgument(m_currentArgument);

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
            L"Availability, discovery, installed-package, update, and source queries run through audited C++ argv builders, parsers, HTTPS transport, and a contained Win32 process runner. Mutations remain locked until the native consent coordinator is proven.",
            L"可用性、搜尋、已安裝套件、更新同來源查詢，會經審核嘅 C++ argv 建立器、解析器、HTTPS transport 同受控 Win32 process runner 執行；修改操作要等原生同意協調器驗證完成先會解鎖。" }.Pick(m_language)));
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
            auto const selected = m_packageViewPicker.SelectedIndex();
            m_packageView = selected < 0 ? 0 : selected;
            if (m_packageProbeComplete)
            {
                CancelPackageWork();
            }
            m_packageItems.clear();
            m_packageRunStates.clear();
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
        AutomationProperties::SetAutomationId(m_packageSearchBox, L"NativePackageSearchBox");
        m_packageSearchBox.QuerySubmitted([this](AutoSuggestBox const&, AutoSuggestBoxQuerySubmittedEventArgs const&)
        {
            if (m_packageView == 0)
            {
                StartPackageQuery();
            }
        });
        m_packageSearchBox.TextChanged([this](AutoSuggestBox const&, AutoSuggestBoxTextChangedEventArgs const&)
        {
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

        m_packagePrimaryAction = Button();
        m_packagePrimaryAction.Padding(Thickness{ 14, 8, 14, 8 });
        AutomationProperties::SetAutomationId(m_packagePrimaryAction, L"NativePackagePrimaryAction");
        m_packagePrimaryAction.Click([this](Windows::Foundation::IInspectable const&, RoutedEventArgs const&)
        {
            if (m_packageView == 6)
            {
                StartPackageManagerProbes();
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
            RenderPackageManagerView();
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
        m_packageLastAction = action;
        m_packageWorking = true;
        if (m_packageBusy) m_packageBusy.IsActive(true);

        auto const actionLabel = action == PackageAction::Search ? L"search"
            : action == PackageAction::Updates ? L"updates"
            : action == PackageAction::Installed ? L"installed"
            : L"sources";
        m_packageOperationLog.insert(
            m_packageOperationLog.begin(),
            std::wstring(L"Started native ") + actionLabel + L" query across " +
                std::to_wstring(managerKeys.size()) + L" manager(s). · 已開始原生查詢。");
        if (m_packageOperationLog.size() > 50) m_packageOperationLog.resize(50);
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

        m_packageOperationLog.insert(
            m_packageOperationLog.begin(),
            L"Availability probe: " + std::to_wstring(availableCount) + L"/" +
                std::to_wstring(winforge::core::packages::PackageManagers().size()) +
                L" managers ready. · 可用性探測完成。");
        if (m_packageOperationLog.size() > 50) m_packageOperationLog.resize(50);
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
            else
            {
                state.diagnostic = std::move(result.diagnostic);
            }
            if (state.success)
            {
                ++successfulManagers;
                for (auto& package : result.packages)
                {
                    m_packageItems.push_back(std::move(package));
                }
            }
            else
            {
                ++failedManagers;
            }
            m_packageRunStates.push_back(std::move(state));
        }
        std::sort(m_packageItems.begin(), m_packageItems.end(), [](auto const& left, auto const& right)
        {
            if (left.manager_key != right.manager_key) return left.manager_key < right.manager_key;
            if (left.name != right.name) return left.name < right.name;
            return left.id < right.id;
        });

        m_packageOperationLog.insert(
            m_packageOperationLog.begin(),
            L"Native query completed: " + std::to_wstring(m_packageItems.size()) +
                L" package row(s), " + std::to_wstring(successfulManagers) + L" manager(s) succeeded. · 原生查詢完成。");
        if (m_packageOperationLog.size() > 50) m_packageOperationLog.resize(50);
        RenderPackageManagerView();
        if (failedManagers != 0)
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
        else
        {
            AnnouncePackageStatus(
                L"Package query finished successfully with " + std::to_wstring(m_packageItems.size()) +
                    L" results.",
                L"套件查詢成功完成，有 " + std::to_wstring(m_packageItems.size()) + L" 個結果。");
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
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Export…", L"匯出…"))));
            m_packageSecondaryAction.Content(box_value(ToHString(pick(L"Import…", L"匯入…"))));
            m_packageSecondaryAction.Visibility(Visibility::Visible);
            header = pick(L"Portable package bundles", L"可攜套件清單");
            explanation = pick(L"Bundle import/export must preserve source identity and saved options; it is not enabled until format compatibility tests pass.",
                L"清單匯入／匯出一定要保留來源身份同已儲存選項；格式相容測試通過之前唔會啟用。");
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
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            header = pick(L"Ignored, pinned, and snoozed updates", L"已忽略、釘住同暫停嘅更新");
            explanation = pick(L"Ignore-rule persistence is awaiting native atomic-storage compatibility tests.",
                L"忽略規則儲存正等緊原生原子式儲存相容測試。");
            break;
        case 6:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Probe again", L"再次探測"))));
            header = pick(L"Engine setup", L"引擎設定");
            explanation = pick(
                L"Non-destructive version probes show which engines are ready. Bootstrap installs remain disabled until the normal-integrity coordinator is complete.",
                L"非破壞性版本探測會顯示邊啲引擎可用；正常 integrity 協調器完成之前，bootstrap 安裝保持停用。");
            break;
        case 7:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Open settings", L"開啟設定"))));
            header = pick(L"Package Manager settings", L"套件管理設定");
            explanation = pick(L"Scheduling, notifications, concurrency, manager paths, proxy secrets, backup, and install defaults will use native persistence and DPAPI.",
                L"排程、通知、同時操作數、管理器路徑、代理機密、備份同安裝預設會用原生儲存同 DPAPI。");
            break;
        default:
            m_packagePrimaryAction.Content(box_value(ToHString(pick(L"Refresh", L"重新整理"))));
            m_packageSecondaryAction.Content(box_value(ToHString(pick(L"Clear completed", L"清除已完成"))));
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
        }
        else if (m_packageView == 6)
        {
            m_packagePrimaryAction.IsEnabled(!m_packageWorking);
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
            if (m_packageOperationLog.empty())
            {
                appendCard(
                    pick(L"No native operations yet", L"暫時未有原生操作"),
                    pick(L"Availability probes and read-only queries will appear here. Mutating operation history is not claimed yet.",
                        L"可用性探測同只讀查詢會喺度顯示；暫時未聲稱有修改操作歷史。"),
                    L"NativePackageOperationsEmpty");
            }
            else
            {
                for (std::size_t index = 0; index < m_packageOperationLog.size(); ++index)
                {
                    appendCard(
                        pick(L"Operation event", L"操作事件"),
                        TruncateForUi(m_packageOperationLog[index], 600),
                        L"NativePackageOperation_" + std::to_wstring(index));
                }
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

            Button mutation;
            auto const actionLabel = m_packageView == 0
                ? pick(L"Install", L"安裝")
                : m_packageView == 1
                    ? pick(L"Update", L"更新")
                    : pick(L"Uninstall", L"解除安裝");
            mutation.Content(box_value(ToHString(actionLabel)));
            mutation.IsEnabled(false);
            mutation.HorizontalAlignment(HorizontalAlignment::Left);
            AutomationProperties::SetAutomationId(
                mutation,
                ToHString(L"NativePackageMutation_" + AutomationKey(package.manager_key) + L"_" + AutomationKey(package.id)));
            ToolTipService::SetToolTip(
                mutation,
                box_value(ToHString(pick(
                    L"Locked until native consent and operation coordination pass.",
                    L"原生同意同操作協調通過驗證之前保持鎖住。"))));
            row.Children().Append(mutation);
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
