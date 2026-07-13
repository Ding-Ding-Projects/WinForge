#include "pch.h"
#include "App.xaml.h"
#include "MainWindow.xaml.h"

namespace winrt::WinForge::implementation
{
    App::App()
    {
        InitializeComponent();

#if defined _DEBUG && !defined DISABLE_XAML_GENERATED_BREAK_ON_UNHANDLED_EXCEPTION
        UnhandledException([](winrt::Windows::Foundation::IInspectable const&, winrt::Microsoft::UI::Xaml::UnhandledExceptionEventArgs const& eventArgs)
        {
            if (IsDebuggerPresent())
            {
                auto const message = eventArgs.Message();
                (void)message;
                __debugbreak();
            }
        });
#endif
    }

    void App::OnLaunched(Microsoft::UI::Xaml::LaunchActivatedEventArgs const&)
    {
        m_window = winrt::make<MainWindow>();
        m_window.Activate();
    }
}
