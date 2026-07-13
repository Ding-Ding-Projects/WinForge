#include "pch.h"
#include "MainWindow.xaml.h"

#if __has_include("MainWindow.g.cpp")
#include "MainWindow.g.cpp"
#endif

#include "CatalogLoader.h"
#include "microsoft.ui.xaml.window.h"

#include <cwctype>
#include <sstream>
#include <stdexcept>

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
