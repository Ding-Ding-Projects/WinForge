using Microsoft.UI.Xaml.Controls;

namespace WinForge.Pages;

public sealed partial class PowerDisplayModule : Page
{
    public PowerDisplayModule()
    {
        InitializeComponent();
        ContentRoot.Children.Add(new PowerDisplayView(false));
    }
}
