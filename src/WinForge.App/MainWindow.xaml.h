#pragma once

#include "MainWindow.g.h"
#include "../WinForge.Core/CommandLine.h"
#include "../WinForge.Core/ModuleRecord.h"
#include "../WinForge.Core/RouteIndex.h"

namespace winrt::WinForge::implementation
{
    struct MainWindow : MainWindowT<MainWindow>
    {
        MainWindow();

    private:
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
        Microsoft::UI::Xaml::Controls::AutoSuggestBox m_packageSearchBox{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packagePrimaryAction{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageSecondaryAction{ nullptr };
        Microsoft::UI::Xaml::Controls::Button m_packageOperationsAction{ nullptr };
        Microsoft::UI::Xaml::Controls::ProgressRing m_packageBusy{ nullptr };
        Microsoft::UI::Xaml::Controls::TextBlock m_packageResultsHeader{ nullptr };
        Microsoft::UI::Xaml::Controls::StackPanel m_packageResults{ nullptr };
        std::unordered_map<std::wstring, bool> m_packageManagersSelected;
        int32_t m_packageView{ 0 };
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
        void PopulatePackageManagerFilters(Microsoft::UI::Xaml::Controls::StackPanel const& panel);
        [[nodiscard]] int32_t PackageViewFromArgument(std::wstring_view argument) const;
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
