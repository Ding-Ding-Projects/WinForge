using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// Bottom-right stacked notification host with a persistent, keyboard-accessible history flyout.
/// </summary>
public sealed partial class NotificationHost : UserControl
{
    private static int _automationFixturePublished;
    private readonly Flyout _historyFlyout;
    private bool _subscribed;
    private string _lastNarratedNoticeId = string.Empty;

    public NotificationHost()
    {
        InitializeComponent();
        _historyFlyout = new Flyout
        {
            Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
        };
        _historyFlyout.Opening += OnHistoryOpening;
        HistoryButton.Flyout = _historyFlyout;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            AppNotificationService.Changed += OnNotificationsChanged;
            Loc.I.LanguageChanged += OnLanguageChanged;
            UniversalSettingsService.Changed += OnUniversalSettingsChanged;
            _subscribed = true;
        }
        PublishAutomationFixtureOnce();
        _lastNarratedNoticeId = AppNotificationService.History
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => item.Id)
            .FirstOrDefault() ?? string.Empty;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed) return;
        AppNotificationService.Changed -= OnNotificationsChanged;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        UniversalSettingsService.Changed -= OnUniversalSettingsChanged;
        _subscribed = false;
    }

    private void OnNotificationsChanged(object? sender, EventArgs e)
    {
        try
        {
            var latest = AppNotificationService.Active.OrderByDescending(item => item.CreatedAt).FirstOrDefault();
            if (latest is not null && !string.Equals(latest.Id, _lastNarratedNoticeId, StringComparison.Ordinal))
            {
                _lastNarratedNoticeId = latest.Id;
                NarratorService.Narrate(latest.TitleEn + ". " + latest.BodyEn,
                    latest.TitleZh + "。" + latest.BodyZh,
                    "notification");
            }
        }
        catch { }
        try { DispatcherQueue.TryEnqueue(Refresh); } catch { }
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Refresh();

    private void OnUniversalSettingsChanged(object? sender, EventArgs e) => Refresh();

    private void OnHistoryOpening(object? sender, object e)
    {
        AppNotificationService.MarkAllViewed();
        _historyFlyout.Content = BuildHistoryContent();
    }

    private void Refresh()
    {
        ToastStack.Children.Clear();
        foreach (var notice in AppNotificationService.Active)
            ToastStack.Children.Add(BuildToast(notice));

        var unread = AppNotificationService.UnreadCount;
        UnreadText.Text = unread > 99 ? "99+" : unread.ToString();
        AutomationProperties.SetName(
            HistoryButton,
            unread == 0
                ? Loc.I.Pick("Open notification centre; no unread notifications", "開啟通知中心；冇未讀通知")
                : Loc.I.Pick(
                    $"Open notification centre; {unread} unread notification(s)",
                    $"開啟通知中心；有 {unread} 個未讀通知"));

        if (_historyFlyout.IsOpen)
            _historyFlyout.Content = BuildHistoryContent();
    }

    private InfoBar BuildToast(AppNoticeEntry notice)
    {
        var bar = new InfoBar
        {
            IsOpen = true,
            IsClosable = true,
            Severity = ToInfoBarSeverity(notice.Severity),
            Title = Decorate(notice.Severity, Pick(notice.TitleEn, notice.TitleZh)),
            Message = Decorate(notice.Severity, Pick(notice.BodyEn, notice.BodyZh)),
            MaxWidth = 400,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(
            bar,
            Loc.I.Pick(
                $"{SeverityName(notice.Severity, false)} notification: {notice.TitleEn}. {notice.BodyEn}",
                $"{SeverityName(notice.Severity, true)}通知：{notice.TitleZh}。{notice.BodyZh}"));
        AutomationProperties.SetLiveSetting(
            bar,
            notice.Severity is AppNoticeSeverity.Error or AppNoticeSeverity.Warning
                ? AutomationLiveSetting.Assertive
                : AutomationLiveSetting.Polite);
        bar.Closed += (_, _) => AppNotificationService.Dismiss(notice.Id);

        if (notice.Severity == AppNoticeSeverity.Progress)
        {
            bar.Content = new ProgressBar
            {
                IsIndeterminate = true,
                MinWidth = 160,
                Margin = new Thickness(0, 4, 0, 2),
            };
        }

        if (!string.IsNullOrWhiteSpace(notice.ImagePath) && File.Exists(notice.ImagePath))
        {
            var content = new StackPanel { Spacing = 6 };
            try
            {
                var image = new Image
                {
                    Source = new BitmapImage(new Uri(notice.ImagePath, UriKind.Absolute)),
                    Height = 120,
                    MaxWidth = 360,
                    Stretch = Stretch.UniformToFill,
                };
                AutomationProperties.SetName(image, Loc.I.Pick(
                    notice.ImageAltEn ?? notice.TitleEn,
                    notice.ImageAltZh ?? notice.TitleZh));
                content.Children.Add(image);
            }
            catch { }
            var body = Pick(notice.BodyEn, notice.BodyZh);
            if (!string.IsNullOrWhiteSpace(body))
                content.Children.Add(new TextBlock
                {
                    Text = Decorate(notice.Severity, body),
                    TextWrapping = TextWrapping.Wrap,
                });
            if (content.Children.Count > 0) bar.Content = content;
        }

        var actions = notice.Actions?.Where(CanInvoke).ToArray() ?? Array.Empty<AppNoticeAction>();
        if (actions.Length == 1)
        {
            bar.ActionButton = ActionButton(actions[0], notice.Id);
        }
        else if (actions.Length > 1)
        {
            var flyout = new MenuFlyout();
            foreach (var action in actions)
            {
                var item = new MenuFlyoutItem { Text = Pick(action.LabelEn, action.LabelZh) };
                item.Click += async (_, _) => await InvokeActionAsync(action, notice.Id);
                flyout.Items.Add(item);
            }
            bar.ActionButton = new DropDownButton
            {
                Content = Loc.I.Pick("Actions", "動作"),
                Flyout = flyout,
                MinHeight = 40,
            };
        }

        return bar;
    }

    private FrameworkElement BuildHistoryContent()
    {
        var root = new StackPanel
        {
            Width = 420,
            MaxWidth = 420,
            Spacing = 10,
            Padding = new Thickness(4),
        };
        var heading = new Grid { ColumnSpacing = 10 };
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new TextBlock
        {
            Text = Loc.I.Pick("Notification centre", "通知中心"),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center,
        };
        var clear = new Button
        {
            Content = Loc.I.Pick("Clear dismissed", "清除已關閉記錄"),
            MinHeight = 40,
            IsEnabled = AppNotificationService.History.Any(x => x.IsDismissed),
        };
        AutomationProperties.SetName(clear, Loc.I.Pick("Clear dismissed notification history", "清除已關閉通知記錄"));
        clear.Click += (_, _) => AppNotificationService.ClearDismissedHistory();
        Grid.SetColumn(clear, 1);
        heading.Children.Add(title);
        heading.Children.Add(clear);
        root.Children.Add(heading);

        var list = new StackPanel { Spacing = 8 };
        var history = AppNotificationService.History;
        if (history.Count == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = Loc.I.Pick("No notifications yet.", "暫時冇通知。"),
                TextWrapping = TextWrapping.Wrap,
                Opacity = 0.72,
                Margin = new Thickness(4, 14, 4, 14),
            });
        }
        else
        {
            foreach (var notice in history)
                list.Children.Add(BuildHistoryItem(notice));
        }

        root.Children.Add(new ScrollViewer
        {
            MaxHeight = 500,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = list,
        });
        root.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick(
                "Stored locally in this Windows profile; the newest 200 notices are retained.",
                "只會儲喺呢個 Windows 使用者設定檔；保留最新 200 個通知。"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.68,
        });
        AutomationProperties.SetName(root, Loc.I.Pick("Notification history", "通知記錄"));
        return root;
    }

    private Border BuildHistoryItem(AppNoticeEntry notice)
    {
        var panel = new StackPanel { Spacing = 3 };
        panel.Children.Add(new TextBlock
        {
            Text = Pick(notice.TitleEn, notice.TitleZh),
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(Pick(notice.BodyEn, notice.BodyZh)))
        {
            panel.Children.Add(new TextBlock
            {
                Text = Pick(notice.BodyEn, notice.BodyZh),
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 5,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        panel.Children.Add(new TextBlock
        {
            Text = Loc.I.Pick(
                $"{SeverityName(notice.Severity, false)} · {notice.CreatedAt.LocalDateTime:g}",
                $"{SeverityName(notice.Severity, true)} · {notice.CreatedAt.LocalDateTime:g}"),
            FontSize = 12,
            Opacity = 0.68,
        });

        var border = new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(12),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = panel,
        };
        AutomationProperties.SetName(
            border,
            Loc.I.Pick(
                $"{SeverityName(notice.Severity, false)} notification from {notice.CreatedAt.LocalDateTime:g}: {notice.TitleEn}",
                $"{SeverityName(notice.Severity, true)}通知，時間 {notice.CreatedAt.LocalDateTime:g}：{notice.TitleZh}"));
        return border;
    }

    private Button ActionButton(AppNoticeAction action, string noticeId)
    {
        var button = new Button
        {
            Content = Pick(action.LabelEn, action.LabelZh),
            MinHeight = 40,
        };
        AutomationProperties.SetName(button, Pick(action.LabelEn, action.LabelZh));
        button.Click += async (_, _) => await InvokeActionAsync(action, noticeId);
        return button;
    }

    private async Task InvokeActionAsync(AppNoticeAction action, string noticeId)
    {
        try
        {
            if (action.Handler is not null)
                await action.Handler();
            else if (!string.IsNullOrWhiteSpace(action.Link))
                Process.Start(new ProcessStartInfo(action.Link) { UseShellExecute = true });

            if (action.DismissOnInvoke)
                AppNotificationService.Dismiss(noticeId);
        }
        catch (Exception ex)
        {
            AppNotificationService.Publish(new AppNoticeDraft(
                "Notification action failed",
                "通知動作失敗",
                ex.Message,
                ex.Message,
                AppNoticeSeverity.Error,
                Key: "notification.action.error"));
        }
    }

    private static bool CanInvoke(AppNoticeAction action)
        => action.Handler is not null || !string.IsNullOrWhiteSpace(action.Link);

    private static string Pick(string en, string zh) => Loc.I.Pick(en, zh);

    private static string Decorate(AppNoticeSeverity severity, string text)
    {
        if (!UniversalSettingsService.EmojiDialogsEnabled || UniversalSettingsService.SchoolModeEnabled || string.IsNullOrWhiteSpace(text))
            return text;
        string emoji = severity switch
        {
            AppNoticeSeverity.Success => "✅",
            AppNoticeSeverity.Warning => "⚠️",
            AppNoticeSeverity.Error => "❌",
            AppNoticeSeverity.Progress => "⏳",
            _ => "ℹ️",
        };
        return $"{emoji} {text}";
    }

    private static InfoBarSeverity ToInfoBarSeverity(AppNoticeSeverity severity) => severity switch
    {
        AppNoticeSeverity.Success => InfoBarSeverity.Success,
        AppNoticeSeverity.Warning => InfoBarSeverity.Warning,
        AppNoticeSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational,
    };

    private static string SeverityName(AppNoticeSeverity severity, bool cantonese) => (severity, cantonese) switch
    {
        (AppNoticeSeverity.Success, false) => "Success",
        (AppNoticeSeverity.Success, true) => "成功",
        (AppNoticeSeverity.Warning, false) => "Warning",
        (AppNoticeSeverity.Warning, true) => "警告",
        (AppNoticeSeverity.Error, false) => "Error",
        (AppNoticeSeverity.Error, true) => "錯誤",
        (AppNoticeSeverity.Progress, false) => "Progress",
        (AppNoticeSeverity.Progress, true) => "進度",
        (_, false) => "Information",
        _ => "資訊",
    };

    [System.Diagnostics.Conditional("DEBUG")]
    private static void PublishAutomationFixtureOnce()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("WINFORGE_NOTIFICATION_DEMO"),
                "1",
                StringComparison.Ordinal)
            || Interlocked.Exchange(ref _automationFixturePublished, 1) != 0)
            return;

        AppNotificationService.Publish(new AppNoticeDraft(
            "Backup complete",
            "備份完成",
            "Your settings snapshot is ready. This success message closes automatically.",
            "設定快照已準備好。呢個成功通知會自動關閉。",
            AppNoticeSeverity.Success,
            Key: "automation.notification.success",
            AutoDismissMs: 120_000,
            PersistInHistory: false));
        AppNotificationService.Publish(new AppNoticeDraft(
            "Review needed",
            "需要留意",
            "Warnings remain until you dismiss them, and every notice stays reviewable in the notification centre.",
            "警告會留低直到你關閉，而且每個通知都可以喺通知中心翻查。",
            AppNoticeSeverity.Warning,
            Key: "automation.notification.warning",
            PersistInHistory: false,
            Actions:
            [
                new AppNoticeAction(
                    "Open guide",
                    "開啟指南",
                    "https://github.com/Ding-Ding-Projects/WinForge/wiki"),
            ]));
    }
}
