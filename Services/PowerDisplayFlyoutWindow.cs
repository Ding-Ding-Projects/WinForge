using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using WinForge.Pages;
using Windows.Graphics;

namespace WinForge.Services;

/// <summary>Compact, topmost Power Display panel for the activation shortcut and tray.</summary>
public static class PowerDisplayFlyoutWindow
{
    private static Window? _window;

    public static void ShowOrActivate()
    {
        if (_window is null)
        {
            var window = new Window
            {
                Title = Loc.I.Pick("Power Display", "顯示器控制"),
                Content = new PowerDisplayView(true),
            };
            window.Closed += OnClosed;
            var presenter = OverlappedPresenter.Create();
            presenter.IsAlwaysOnTop = true;
            presenter.IsMaximizable = false;
            window.AppWindow.SetPresenter(presenter);
            window.AppWindow.Resize(new SizeInt32(500, 700));
            _window = window;
        }
        _window.Activate();
    }

    private static void OnClosed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(sender, _window)) _window = null;
    }
}
