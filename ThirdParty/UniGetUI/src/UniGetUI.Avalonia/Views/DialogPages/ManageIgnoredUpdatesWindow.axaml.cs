using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using UniGetUI.Avalonia.ViewModels;

namespace UniGetUI.Avalonia.Views;

public partial class ManageIgnoredUpdatesWindow : Window
{
    public ManageIgnoredUpdatesWindow()
    {
        var vm = new ManageIgnoredUpdatesViewModel();
        DataContext = vm;
        InitializeComponent();

        // Drop the OS title-bar strip (its background clashed with the dialog) but keep
        // the system min/max/close buttons floating over the extended client area. Default
        // WindowDecorations (Full) keeps the system buttons; extending the client area
        // merges the title-bar region into the content.
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaTitleBarHeightHint = -1;

        vm.CloseRequested += (_, _) => Close();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        Dispatcher.UIThread.Post(() =>
        {
            if (((ManageIgnoredUpdatesViewModel)DataContext!).HasEntries)
                IgnoredUpdatesGrid.Focus();
            else
                ResetButton.Focus();
        }, DispatcherPriority.Background);
    }

    private void ResetYes_Click(object? sender, RoutedEventArgs e)
    {
        ((ManageIgnoredUpdatesViewModel)DataContext!).ResetAllCommand.Execute(null);
        ResetButton.Flyout?.Hide();
    }

    private void ResetNo_Click(object? sender, RoutedEventArgs e) =>
        ResetButton.Flyout?.Hide();
}
