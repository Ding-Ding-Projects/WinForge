using System;
using System.Collections.Generic;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;
using Windows.System;
using WinForge.Services;

namespace WinForge;

public sealed partial class MainWindow
{
    private readonly List<DetachedTabWindow> _detachedTabWindows = new();

    private void Tabs_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
    {
        if (args.Tab is TabViewItem tab)
            DetachTab(tab);
    }

    private void DetachTab(TabViewItem tab)
    {
        if (!Tabs.TabItems.Contains(tab)) return;

        var data = TabSessionService.CloneTab(DataOf(tab));
        if (ReactorDependencyService.Requires(BaseNavKey(data.Key)))
        {
            AppNotificationService.Publish(new AppNoticeDraft(
                "Keep this powered module in a tab",
                "請將呢個供電模組留喺分頁",
                "Feature-power modules stay in the main tab strip so their nuclear/EDG source and the two EDG outlets can be enforced continuously.",
                "功能電源模組要留喺主分頁列，先可以持續執行核電／柴油來源同兩個柴油插槽限制。",
                AppNoticeSeverity.Warning,
                Key: "feature-power.detach",
                AutoDismissMs: 0));
            return;
        }

        var (type, param) = Resolve(data.Key);
        var title = string.IsNullOrWhiteSpace(data.Name) ? TitleFor(data.Key, type, param) : data.Name.Trim();
        var window = new DetachedTabWindow(title, type, param);
        TrackDetachedTabWindow(window);

        if (_featurePowerOwnerTokens.Remove(tab, out var ownerToken))
            ReactorFeaturePowerService.I.ReleaseModule(ownerToken);
        Tabs.TabItems.Remove(tab);
        if (Tabs.TabItems.Count == 0) AddTab("dashboard");
        UpdateBackButton();
        SaveSession();

        window.Activate();
    }

    private void TrackDetachedTabWindow(DetachedTabWindow window)
    {
        _detachedTabWindows.Add(window);
        window.Closed += (_, _) => _detachedTabWindows.Remove(window);
    }

    private void CloseDetachedTabs()
    {
        foreach (var window in _detachedTabWindows.ToArray())
        {
            try { window.Close(); } catch { }
        }
        _detachedTabWindows.Clear();
    }

    private sealed class DetachedTabWindow : Window
    {
        private readonly Frame _frame = new();
        private readonly string _pageTitle;

        public DetachedTabWindow(string pageTitle, Type pageType, object? parameter)
        {
            _pageTitle = string.IsNullOrWhiteSpace(pageTitle) ? "WinForge" : pageTitle.Trim();
            Title = $"{_pageTitle} - WinForge";
            try { AppWindow.SetIcon("Assets/AppIcon.ico"); } catch { }

            ExtendsContentIntoTitleBar = false;
            AppWindow.SetPresenter(OverlappedPresenter.Create());
            SizeAndPlace();

            _frame.HorizontalAlignment = HorizontalAlignment.Stretch;
            _frame.VerticalAlignment = VerticalAlignment.Stretch;
            InstallAccelerators();
            try
            {
                if (App.Shell?.Content is FrameworkElement shellRoot)
                    _frame.RequestedTheme = shellRoot.RequestedTheme;
            }
            catch { }

            Content = _frame;
            _frame.Navigate(pageType, parameter);
        }

        private void InstallAccelerators()
        {
            var close = new KeyboardAccelerator
            {
                Key = VirtualKey.W,
                Modifiers = VirtualKeyModifiers.Control,
            };
            close.Invoked += (_, e) =>
            {
                Close();
                e.Handled = true;
            };
            _frame.KeyboardAccelerators.Add(close);

            var back = new KeyboardAccelerator
            {
                Key = VirtualKey.Left,
                Modifiers = VirtualKeyModifiers.Menu,
            };
            back.Invoked += (_, e) =>
            {
                if (_frame.CanGoBack) _frame.GoBack();
                e.Handled = true;
            };
            _frame.KeyboardAccelerators.Add(back);
        }

        private void SizeAndPlace()
        {
            try
            {
                var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
                int w = Math.Min(1180, Math.Max(920, (int)(area.WorkArea.Width * 0.7)));
                int h = Math.Min(820, Math.Max(640, (int)(area.WorkArea.Height * 0.72)));
                AppWindow.Resize(new SizeInt32(w, h));
                AppWindow.Move(new PointInt32(
                    area.WorkArea.X + Math.Max(0, (area.WorkArea.Width - w) / 2),
                    area.WorkArea.Y + Math.Max(0, (area.WorkArea.Height - h) / 2)));
            }
            catch { }
        }
    }
}
