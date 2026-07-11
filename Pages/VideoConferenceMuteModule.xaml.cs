using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>Native global microphone mute plus opt-in camera privacy control for video conferences.</summary>
public sealed partial class VideoConferenceMuteModule : Page
{
    private bool _rendering;
    private bool _subscribed;

    public VideoConferenceMuteModule()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed) return;
        _subscribed = true;
        Loc.I.LanguageChanged += OnLanguageChanged;
        VideoConferenceMuteService.StateChanged += OnStateChanged;
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed) return;
        _subscribed = false;
        Loc.I.LanguageChanged -= OnLanguageChanged;
        VideoConferenceMuteService.StateChanged -= OnStateChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private void OnStateChanged(ConferenceMuteState state)
    {
        if (DispatcherQueue is not null) DispatcherQueue.TryEnqueue(() => Render());
    }

    private void Render()
    {
        if (!_subscribed) return;
        var state = VideoConferenceMuteService.GetState();
        _rendering = true;
        try
        {
            Header.Title = "Video Conference Mute · 視像會議靜音";
            HeaderBlurb.Text = P(
                "A native global mute utility for the default communications microphone, with an optional reversible camera privacy gate and visible hotkey confirmation.",
                "一個原生全域靜音工具，用於預設通訊咪；亦有可選、可還原嘅鏡頭私隱閘同可見快捷鍵確認。");

            EnableTitle.Text = P("Enable Video Conference Mute", "開啟視像會議靜音");
            EnableBlurb.Text = P(
                "When enabled, the three global shortcuts work even while this page is closed. No device state changes until you invoke a control.",
                "開啟之後，即使關咗呢頁三個全域快捷鍵都會運作。未呼叫控制前唔會更改裝置狀態。");
            EnableSwitch.IsOn = state.Enabled;

            StatusTitle.Text = P("Current conference state", "目前會議狀態");
            var microphone = !state.MicrophoneAvailable
                ? P("No default communications microphone is available", "未有可用嘅預設通訊咪")
                : state.MicrophoneMuted ? P("Microphone: muted", "咪：已靜音") : P("Microphone: live", "咪：開啟中");
            var camera = !state.CameraPrivacyControlEnabled
                ? P("Camera privacy: unchanged", "鏡頭私隱：未更改")
                : state.CameraMuted ? P("Camera privacy: blocked", "鏡頭私隱：已封鎖") : P("Camera privacy: allowed", "鏡頭私隱：已允許");
            StatusText.Text = $"{microphone} · {camera}";
            bool fullyMuted = (!state.MicrophoneAvailable || state.MicrophoneMuted) &&
                              (!state.CameraPrivacyControlEnabled || state.CameraMuted);
            AllButton.Content = fullyMuted ? P("Unmute conference", "解除會議靜音") : P("Mute conference", "會議靜音");
            MicrophoneButton.Content = state.MicrophoneMuted ? P("Unmute microphone", "解除咪靜音") : P("Mute microphone", "咪靜音");
            MicrophoneButton.IsEnabled = state.MicrophoneAvailable;
            CameraButton.Content = state.CameraMuted ? P("Allow camera", "允許鏡頭") : P("Block camera", "封鎖鏡頭");
            CameraButton.IsEnabled = state.CameraPrivacyControlEnabled;
            RefreshButton.Content = P("Refresh state", "重新整理狀態");

            HotkeyTitle.Text = P("Global shortcuts", "全域快捷鍵");
            AllHotkeyLabel.Text = P("Mute / unmute conference", "會議靜音／解除靜音");
            MicrophoneHotkeyLabel.Text = P("Mute / unmute microphone", "咪靜音／解除靜音");
            CameraHotkeyLabel.Text = P("Block / allow camera", "封鎖／允許鏡頭");
            AllHotkeyText.Text = VideoConferenceMuteService.AllHotkey.Text();
            MicrophoneHotkeyText.Text = VideoConferenceMuteService.MicrophoneHotkey.Text();
            CameraHotkeyText.Text = VideoConferenceMuteService.CameraHotkey.Text();
            AllHotkeyButton.Content = P("Change…", "更改…");
            MicrophoneHotkeyButton.Content = P("Change…", "更改…");
            CameraHotkeyButton.Content = P("Change…", "更改…");

            PrivacyTitle.Text = P("Privacy and tray", "私隱同系統匣");
            CameraPrivacySwitch.Header = P("Allow global camera privacy control", "允許全域鏡頭私隱控制");
            CameraPrivacySwitch.IsOn = state.CameraPrivacyControlEnabled;
            CameraPrivacyBlurb.Text = P(
                "When allowed, camera shortcuts write only your per-user webcam consent setting (Allow/Deny). This is reversible here; it never disables a device driver.",
                "開啟之後，鏡頭快捷鍵只會寫入你每用戶嘅 webcam 同意設定（Allow／Deny）。可以喺呢度還原；絕對唔會停用裝置驅動程式。");
            TraySwitch.Header = P("Show Video Conference Mute in the WinForge tray menu", "喺 WinForge 系統匣選單顯示視像會議靜音");
            TraySwitch.IsOn = VideoConferenceMuteService.ShowTrayMenuItem;
            PrivacyBar.Title = P("Opt-in safeguards", "選擇性保護");
            PrivacyBar.Message = P(
                "Microphone controls affect only the default communications capture endpoint. Camera control is off by default and requires your explicit consent above.",
                "咪控制只會影響預設通訊擷取端點。鏡頭控制預設關閉，必須喺上面由你明確同意。");
        }
        finally { _rendering = false; }
    }

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;
        VideoConferenceMuteService.SetEnabled(EnableSwitch.IsOn);
        Info(P(EnableSwitch.IsOn ? "Enabled" : "Disabled", EnableSwitch.IsOn ? "已開啟" : "已關閉"), VideoConferenceMuteService.LastEvent);
        Render();
    }

    private void All_Click(object sender, RoutedEventArgs e)
    {
        VideoConferenceMuteService.ToggleAll();
        Info(P("Conference control", "會議控制"), VideoConferenceMuteService.LastEvent);
    }

    private void Microphone_Click(object sender, RoutedEventArgs e)
    {
        VideoConferenceMuteService.ToggleMicrophone();
        Info(P("Microphone control", "咪控制"), VideoConferenceMuteService.LastEvent);
    }

    private void Camera_Click(object sender, RoutedEventArgs e)
    {
        VideoConferenceMuteService.ToggleCamera();
        Info(P("Camera privacy", "鏡頭私隱"), VideoConferenceMuteService.LastEvent);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Render();

    private async void CameraPrivacy_Toggled(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;
        if (!CameraPrivacySwitch.IsOn)
        {
            VideoConferenceMuteService.SetAllowCameraPrivacyToggle(false);
            Render();
            return;
        }

        var dialog = new ContentDialog
        {
            Title = P("Enable camera privacy control?", "開啟鏡頭私隱控制？"),
            Content = P(
                "This lets WinForge set your per-user webcam consent to Allow or Deny from the buttons and global hotkeys. It does not disable drivers and can be reversed here.",
                "呢個選項容許 WinForge 由按鈕同全域快捷鍵設定你每用戶 webcam 同意為 Allow 或 Deny。唔會停用驅動程式，亦可以喺呢度還原。"),
            PrimaryButtonText = P("Enable", "開啟"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            VideoConferenceMuteService.SetAllowCameraPrivacyToggle(true);
            Info(P("Camera privacy enabled", "鏡頭私隱已開啟"), VideoConferenceMuteService.LastEvent);
        }
        Render();
    }

    private void Tray_Toggled(object sender, RoutedEventArgs e)
    {
        if (_rendering) return;
        VideoConferenceMuteService.SetShowTrayMenuItem(TraySwitch.IsOn);
    }

    private async void AllHotkey_Click(object sender, RoutedEventArgs e)
        => await CaptureHotkey(ConferenceMuteAction.All);
    private async void MicrophoneHotkey_Click(object sender, RoutedEventArgs e)
        => await CaptureHotkey(ConferenceMuteAction.Microphone);
    private async void CameraHotkey_Click(object sender, RoutedEventArgs e)
        => await CaptureHotkey(ConferenceMuteAction.Camera);

    private async System.Threading.Tasks.Task CaptureHotkey(ConferenceMuteAction action)
    {
        var current = action switch
        {
            ConferenceMuteAction.All => VideoConferenceMuteService.AllHotkey,
            ConferenceMuteAction.Microphone => VideoConferenceMuteService.MicrophoneHotkey,
            _ => VideoConferenceMuteService.CameraHotkey,
        };
        var ctrl = new CheckBox { Content = "Ctrl", IsChecked = (current.Modifiers & 0x0002) != 0 };
        var alt = new CheckBox { Content = "Alt", IsChecked = (current.Modifiers & 0x0001) != 0 };
        var shift = new CheckBox { Content = "Shift", IsChecked = (current.Modifiers & 0x0004) != 0 };
        var win = new CheckBox { Content = "Win", IsChecked = (current.Modifiers & 0x0008) != 0 };
        var keys = new ComboBox { Header = P("Key", "按鍵") };
        foreach (var (name, vk) in HotkeyMacroService.PickableKeys)
            keys.Items.Add(new ComboBoxItem { Content = name, Tag = vk });
        for (int i = 0; i < keys.Items.Count; i++)
            if (keys.Items[i] is ComboBoxItem item && item.Tag is uint key && key == current.VirtualKey)
            { keys.SelectedIndex = i; break; }
        if (keys.SelectedIndex < 0) keys.SelectedIndex = 0;

        var modifiers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modifiers.Children.Add(ctrl);
        modifiers.Children.Add(alt);
        modifiers.Children.Add(shift);
        modifiers.Children.Add(win);
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = P("Pick modifiers and a key for this global shortcut.", "揀呢個全域快捷鍵嘅修飾鍵同按鍵。"), TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(modifiers);
        panel.Children.Add(keys);
        var title = action switch
        {
            ConferenceMuteAction.All => P("Conference shortcut", "會議快捷鍵"),
            ConferenceMuteAction.Microphone => P("Microphone shortcut", "咪快捷鍵"),
            _ => P("Camera shortcut", "鏡頭快捷鍵"),
        };
        var dialog = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = P("Save", "儲存"),
            SecondaryButtonText = P("Clear", "清除"),
            CloseButtonText = P("Cancel", "取消"),
            XamlRoot = XamlRoot,
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.None) return;
        if (result == ContentDialogResult.Secondary)
        {
            VideoConferenceMuteService.SetHotkey(action, new ConferenceMuteHotkey());
            Render();
            return;
        }

        uint mods = 0;
        if (ctrl.IsChecked == true) mods |= 0x0002;
        if (alt.IsChecked == true) mods |= 0x0001;
        if (shift.IsChecked == true) mods |= 0x0004;
        if (win.IsChecked == true) mods |= 0x0008;
        if (mods == 0)
        {
            Warn(P("Pick at least one modifier.", "至少揀一個修飾鍵。"));
            return;
        }
        var selected = keys.SelectedItem as ComboBoxItem;
        if (selected?.Tag is not uint keyValue)
        {
            Warn(P("Pick a key.", "請揀一個按鍵。"));
            return;
        }
        VideoConferenceMuteService.SetHotkey(action, new ConferenceMuteHotkey
        {
            Modifiers = mods,
            VirtualKey = keyValue,
            KeyName = selected.Content?.ToString() ?? "",
        });
        Info(P("Shortcut saved", "快捷鍵已儲存"), VideoConferenceMuteService.LastEvent);
        Render();
    }

    private void Info(string title, string message)
    {
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private void Warn(string message)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }
}
