using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Controls;

/// <summary>
/// Native, reusable confirmation surface for destructive actions. The action cannot proceed until
/// two independently entered keys enable a full-range slider. Escape and the close button are
/// always safe cancellation paths; this is a UX guard, not a security boundary.
/// </summary>
public static class SuperConfirmationDialog
{
    public static async Task<bool> ShowAsync(
        XamlRoot root,
        string titleEn,
        string titleZh,
        string actionEn,
        string actionZh,
        string detailEn,
        string detailZh,
        string keyTwo,
        string keyOne = "DELETE")
    {
        keyOne = keyOne.Trim();
        keyTwo = keyTwo.Trim();
        if (keyOne.Length == 0 || keyTwo.Length == 0) return false;

        var first = new TextBox
        {
            Header = Loc.I.Pick("Key 1", "第一條匙"),
            PlaceholderText = keyOne,
            MaxLength = Math.Min(keyOne.Length, 128),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var second = new TextBox
        {
            Header = Loc.I.Pick("Key 2", "第二條匙"),
            PlaceholderText = keyTwo,
            MaxLength = Math.Min(keyTwo.Length, 128),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            StepFrequency = 1,
            IsEnabled = false,
            Header = Loc.I.Pick("Slide fully to authorize", "完整滑到盡頭先可以授權"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var panel = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = Loc.I.Pick(detailEn, detailZh), TextWrapping = TextWrapping.Wrap },
                new TextBlock
                {
                    Text = Loc.I.Pick(
                        "This is a local user-experience guard, not encryption or a security boundary.",
                        "呢個只係本機用戶體驗 guard，唔係加密，亦唔係安全邊界。"),
                    TextWrapping = TextWrapping.Wrap,
                },
                first,
                second,
                slider,
                progress,
                status,
            },
        };

        bool authorized = false;
        void UpdateKeys(object? _, TextChangedEventArgs __)
        {
            slider.IsEnabled = string.Equals(first.Text, keyOne, StringComparison.Ordinal) &&
                               string.Equals(second.Text, keyTwo, StringComparison.Ordinal);
            if (!slider.IsEnabled)
            {
                slider.Value = 0;
                progress.Value = 0;
                status.Text = Loc.I.Pick("Enter both keys to enable authorization.", "輸入兩條匙先可以授權。");
            }
            else
            {
                status.Text = Loc.I.Pick("Both keys match. Slide the control fully to continue.", "兩條匙啱晒；完整滑到盡頭先可以繼續。");
            }
        }

        first.TextChanged += UpdateKeys;
        second.TextChanged += UpdateKeys;
        slider.ValueChanged += (_, args) =>
        {
            progress.Value = args.NewValue;
            status.Text = args.NewValue >= 100
                ? Loc.I.Pick("Authorization range complete. Press the action button to finish.", "授權範圍完成；撳動作按鈕先完成。")
                : Loc.I.Pick($"{args.NewValue:0}% authorized.", $"已授權 {args.NewValue:0}%。");
        };

        AutomationProperties.SetName(first, Loc.I.Pick("Super confirmation key one", "超級確認第一條匙"));
        AutomationProperties.SetName(second, Loc.I.Pick("Super confirmation key two", "超級確認第二條匙"));
        AutomationProperties.SetName(slider, Loc.I.Pick("Full-range destructive action authorization slider", "破壞性動作完整範圍授權滑桿"));
        AutomationProperties.SetName(progress, Loc.I.Pick("Destructive action authorization progress", "破壞性動作授權進度"));
        status.Text = Loc.I.Pick("Enter both keys to enable authorization.", "輸入兩條匙先可以授權。");

        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = Loc.I.Pick(titleEn, titleZh),
            Content = panel,
            PrimaryButtonText = Loc.I.Pick(actionEn, actionZh),
            CloseButtonText = Loc.I.Pick("Emergency exit", "緊急離開"),
            DefaultButton = ContentDialogButton.Close,
        };
        dialog.PrimaryButtonClick += (_, args) =>
        {
            if (!slider.IsEnabled || slider.Value < 100)
            {
                args.Cancel = true;
                status.Text = Loc.I.Pick(
                    "Both keys and the full slider are required; nothing was changed.",
                    "兩條匙同完整滑桿都必須完成；冇改到任何嘢。");
                return;
            }

            authorized = true;
            progress.Value = 100;
            status.Text = Loc.I.Pick("Authorization complete.", "授權完成。");
        };

        try
        {
            ContentDialogResult result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary && authorized;
        }
        catch
        {
            return false;
        }
    }
}
