using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>Shared full-page and compact-panel controls, using normal WinUI themed text for light/dark contrast.</summary>
public sealed class PowerDisplayView : UserControl
{
    private readonly bool _compact;
    private readonly ScrollViewer _scroll;
    private bool _loaded;
    private bool _building;
    private string? _message;
    private InfoBarSeverity _messageSeverity;

    public PowerDisplayView(bool compact)
    {
        _compact = compact;
        _scroll = new ScrollViewer
        {
            Padding = new Thickness(20),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        Content = _scroll;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Build();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_loaded) return;
        _loaded = false;
        Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Build();

    private void Build()
    {
        if (!_loaded || _building) return;
        _building = true;
        try
        {
            var root = new StackPanel { Spacing = 14 };
            _scroll.Content = root;
            root.Children.Add(new TextBlock
            {
                Text = P("Power Display", "顯示器控制"),
                FontSize = _compact ? 24 : 30,
                FontWeight = FontWeights.SemiBold,
            });
            root.Children.Add(new TextBlock
            {
                Text = P(
                    "Control external DDC/CI monitors with saved profiles, a compact hotkey panel, tray access and optional Light Switch bindings.",
                    "用已儲存設定檔、精簡快捷鍵面板、系統匣入口同可選 Light Switch 綁定去控制外置 DDC/CI 螢幕。"),
                TextWrapping = TextWrapping.Wrap,
            });
            if (_message is not null)
                root.Children.Add(new InfoBar { IsOpen = true, Severity = _messageSeverity, Title = P("Power Display", "顯示器控制"), Message = _message });

            var enabled = new ToggleSwitch
            {
                Header = P("Enable Power Display", "開啟顯示器控制"),
                IsOn = PowerDisplayService.Enabled,
                OnContent = P("Enabled", "已開啟"),
                OffContent = P("Disabled", "已關閉"),
            };
            enabled.Toggled += (_, _) =>
            {
                PowerDisplayService.SetEnabled(enabled.IsOn);
                SetMessage(enabled.IsOn
                    ? P("DDC/CI probing and writes are enabled only for monitors you control.", "而家只會為你控制嘅螢幕開啟 DDC/CI 偵測同寫入。")
                    : P("Hardware probing and writes are disabled.", "硬件偵測同寫入已關閉。"),
                    InfoBarSeverity.Informational);
                Build();
            };
            root.Children.Add(Card(enabled));

            if (!PowerDisplayService.Enabled)
            {
                root.Children.Add(new InfoBar
                {
                    IsOpen = true,
                    Severity = InfoBarSeverity.Warning,
                    Title = P("Explicit consent required", "需要明確同意"),
                    Message = P(
                        "Enable this module before WinForge queries a monitor or changes DDC/CI values. Internal laptop panels may not expose these controls.",
                        "請先開啟呢個模組，WinForge 先會查詢螢幕或者更改 DDC/CI 數值。手提電腦內置面板未必提供呢啲控制。"),
                });
                return;
            }

            var monitors = PowerDisplayService.Discover();
            AddActions(root, monitors);
            AddProfiles(root);
            if (!_compact) AddSettings(root);
            AddMonitors(root, monitors);
        }
        finally { _building = false; }
    }

    private void AddActions(StackPanel root, IReadOnlyList<PowerDisplayMonitor> monitors)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var refresh = new Button { Content = P("Refresh monitors", "重新整理螢幕") };
        refresh.Click += (_, _) => Build();
        row.Children.Add(refresh);
        if (!_compact)
        {
            var compact = new Button { Content = P("Open compact panel", "開啟精簡面板") };
            compact.Click += (_, _) => PowerDisplayService.ShowCompactPanel();
            row.Children.Add(compact);
        }
        root.Children.Add(Card(row));
    }

    private void AddProfiles(StackPanel root)
    {
        var profiles = PowerDisplayService.Profiles;
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(Title(P("Profiles", "設定檔")));
        body.Children.Add(new TextBlock
        {
            Text = P(
                "Capture the supported settings of every monitor, apply a profile in one click, and choose separate profiles for Light Switch light and dark transitions.",
                "擷取每部螢幕受支援嘅設定、一鍵套用設定檔，仲可以為 Light Switch 淺色同深色轉換揀分開設定檔。"),
            TextWrapping = TextWrapping.Wrap,
        });

        var selected = CreateProfileBox(profiles, "");
        var applyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        applyRow.Children.Add(selected);
        var apply = new Button { Content = P("Apply selected", "套用所選") };
        apply.Click += (_, _) =>
        {
            var id = ProfileId(selected);
            int writes = PowerDisplayService.ApplyProfile(id);
            SetMessage(writes > 0 ? P($"{writes} setting(s) applied.", $"已套用 {writes} 項設定。") : P("Choose a profile with compatible monitors.", "請揀一個有相容螢幕嘅設定檔。"),
                writes > 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            Build();
        };
        applyRow.Children.Add(apply);
        var delete = new Button { Content = P("Delete", "刪除") };
        delete.Click += (_, _) =>
        {
            var deleted = PowerDisplayService.DeleteProfile(ProfileId(selected));
            SetMessage(deleted ? P("Profile deleted.", "已刪除設定檔。") : P("No profile was deleted.", "未有刪除設定檔。"),
                deleted ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            Build();
        };
        applyRow.Children.Add(delete);
        body.Children.Add(applyRow);

        if (!_compact)
        {
            var name = new TextBox { PlaceholderText = P("New profile name", "新設定檔名稱"), MinWidth = 200 };
            var captureRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            captureRow.Children.Add(name);
            var capture = new Button { Content = P("Capture current", "擷取目前設定") };
            capture.Click += (_, _) =>
            {
                var profile = PowerDisplayService.CaptureProfile(name.Text);
                SetMessage(profile is null ? P("Enable Power Display first.", "請先開啟顯示器控制。") : P($"Captured {profile.Name}.", $"已擷取 {profile.Name}。"),
                    profile is null ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
                Build();
            };
            captureRow.Children.Add(capture);
            body.Children.Add(captureRow);

            var startup = CreateProfileBox(profiles, PowerDisplayService.StartupProfileId);
            var light = CreateProfileBox(profiles, PowerDisplayService.LightProfileId);
            var dark = CreateProfileBox(profiles, PowerDisplayService.DarkProfileId);
            body.Children.Add(Labeled(P("Restore at startup", "啟動時還原"), startup));
            body.Children.Add(Labeled(P("Light Switch: light profile", "Light Switch：淺色設定檔"), light));
            body.Children.Add(Labeled(P("Light Switch: dark profile", "Light Switch：深色設定檔"), dark));
            void SaveAssignments() => PowerDisplayService.SetProfileAssignments(ProfileId(startup), ProfileId(light), ProfileId(dark));
            startup.SelectionChanged += (_, _) => SaveAssignments();
            light.SelectionChanged += (_, _) => SaveAssignments();
            dark.SelectionChanged += (_, _) => SaveAssignments();
        }
        root.Children.Add(Card(body));
    }

    private void AddSettings(StackPanel root)
    {
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(Title(P("Activation and compatibility", "啟用同相容性")));
        var shortcut = new TextBox { Text = PowerDisplayService.ActivationShortcut, MinWidth = 160 };
        var setShortcut = new Button { Content = P("Set shortcut", "設定快捷鍵") };
        setShortcut.Click += (_, _) =>
        {
            var accepted = PowerDisplayService.SetActivationShortcut(shortcut.Text);
            SetMessage(accepted ? P($"Shortcut set to {shortcut.Text.Trim()}.", $"快捷鍵已設為 {shortcut.Text.Trim()}。") :
                P("Use a shortcut such as Ctrl+Alt+D or Ctrl+Shift+F8.", "請用例如 Ctrl+Alt+D 或 Ctrl+Shift+F8 嘅快捷鍵。"),
                accepted ? InfoBarSeverity.Success : InfoBarSeverity.Warning);
            Build();
        };
        var shortcutRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        shortcutRow.Children.Add(shortcut);
        shortcutRow.Children.Add(setShortcut);
        body.Children.Add(Labeled(P("Compact-panel shortcut", "精簡面板快捷鍵"), shortcutRow));

        var delay = new TextBox { Text = PowerDisplayService.RefreshDelaySeconds.ToString(CultureInfo.InvariantCulture), Width = 72 };
        body.Children.Add(Labeled(P("Refresh delay (seconds)", "重新整理延遲（秒）"), delay));
        var restore = new ToggleSwitch { Header = P("Restore selected startup profile", "還原所選啟動設定檔"), IsOn = PowerDisplayService.RestoreAtStartup };
        var tray = new ToggleSwitch { Header = P("Show Power Display in WinForge tray menu", "喺 WinForge 系統匣選單顯示顯示器控制"), IsOn = PowerDisplayService.ShowTrayMenuItem };
        var compatibility = new ToggleSwitch { Header = P("Maximum compatibility mode", "最高相容模式"), IsOn = PowerDisplayService.MaximumCompatibilityMode };
        body.Children.Add(restore);
        body.Children.Add(tray);
        body.Children.Add(compatibility);
        body.Children.Add(new TextBlock
        {
            Text = P(
                "Maximum compatibility reads only the safest brightness, contrast and power controls for fragile DDC/CI firmware.",
                "最高相容模式只會讀取最穩陣嘅亮度、對比同電源控制，照顧唔穩定嘅 DDC/CI 韌體。"),
            TextWrapping = TextWrapping.Wrap,
        });
        var custom = new TextBox
        {
            Text = PowerDisplayService.CustomVcpText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 72,
            PlaceholderText = P("0xE1;Custom control", "0xE1;自訂控制"),
        };
        ScrollViewer.SetVerticalScrollBarVisibility(custom, ScrollBarVisibility.Auto);
        body.Children.Add(Labeled(P("Custom VCP mappings (code; name per line)", "自訂 VCP 對應（每行代碼；名稱）"), custom));
        var save = new Button { Content = P("Save settings", "儲存設定") };
        save.Click += (_, _) =>
        {
            if (!int.TryParse(delay.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                SetMessage(P("Refresh delay must be a number from 1 to 20.", "重新整理延遲必須係 1 至 20 嘅數字。"), InfoBarSeverity.Warning);
                Build();
                return;
            }
            PowerDisplayService.SetRefreshDelay(seconds);
            PowerDisplayService.SetRestoreAtStartup(restore.IsOn);
            PowerDisplayService.SetShowTrayMenuItem(tray.IsOn);
            PowerDisplayService.SetMaximumCompatibilityMode(compatibility.IsOn);
            PowerDisplayService.SetCustomVcpText(custom.Text);
            SetMessage(P("Power Display settings saved.", "顯示器控制設定已儲存。"), InfoBarSeverity.Success);
            Build();
        };
        body.Children.Add(save);
        root.Children.Add(Card(body));
    }

    private void AddMonitors(StackPanel root, IReadOnlyList<PowerDisplayMonitor> monitors)
    {
        root.Children.Add(Title(P("Detected DDC/CI monitors", "已偵測 DDC/CI 螢幕")));
        if (monitors.Count == 0)
        {
            root.Children.Add(new InfoBar
            {
                IsOpen = true,
                Severity = InfoBarSeverity.Informational,
                Title = P("No controllable external monitor found", "搵唔到可控制嘅外置螢幕"),
                Message = P(
                    "Enable DDC/CI in the monitor on-screen display, then refresh. Internal laptop panels commonly use a separate brightness path.",
                    "請喺螢幕 OSD 開啟 DDC/CI，然後重新整理。手提電腦內置面板通常用另一條亮度路徑。"),
            });
            return;
        }

        foreach (var monitor in monitors)
        {
            if (_compact && !PowerDisplayService.ShowInCompactPanel(monitor.Id)) continue;
            root.Children.Add(CreateMonitorCard(monitor));
        }
        if (_compact && !monitors.Any(m => PowerDisplayService.ShowInCompactPanel(m.Id)))
            root.Children.Add(new InfoBar
            {
                IsOpen = true,
                Severity = InfoBarSeverity.Informational,
                Title = P("Choose monitors for the compact panel", "請揀精簡面板嘅螢幕"),
                Message = P("Use the full Power Display page to show a monitor here.", "請喺完整顯示器控制頁面揀選要喺呢度顯示嘅螢幕。"),
            });
    }

    private UIElement CreateMonitorCard(PowerDisplayMonitor monitor)
    {
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(Title(monitor.Description));
        body.Children.Add(new TextBlock { Text = $"{monitor.DeviceName} · {monitor.Width} x {monitor.Height}", TextWrapping = TextWrapping.Wrap });
        foreach (var pair in monitor.Values)
        {
            if (pair.Key == 0xD6)
            {
                var power = new ToggleSwitch { Header = PowerDisplayService.DisplayNameFor(pair.Key), IsOn = pair.Value.Current != 0x04 };
                power.Toggled += (_, _) => PowerDisplayService.SetValue(monitor.Id, pair.Key, power.IsOn ? 0x01u : 0x04u);
                body.Children.Add(power);
                continue;
            }
            body.Children.Add(CreateSlider(monitor, pair.Key, pair.Value));
        }
        if (!_compact)
        {
            var quick = new ToggleSwitch
            {
                Header = P("Show this monitor in compact panel", "喺精簡面板顯示呢部螢幕"),
                IsOn = PowerDisplayService.ShowInCompactPanel(monitor.Id),
            };
            quick.Toggled += (_, _) => PowerDisplayService.SetShowInCompactPanel(monitor.Id, quick.IsOn);
            body.Children.Add(quick);
        }
        return Card(body);
    }

    private UIElement CreateSlider(PowerDisplayMonitor monitor, byte code, PowerDisplayValue value)
    {
        var panel = new StackPanel { Spacing = 3 };
        var caption = new TextBlock { Text = $"{PowerDisplayService.DisplayNameFor(code)}: {value.Current}" };
        panel.Children.Add(caption);
        uint maximum = Math.Max(value.Minimum + 1, value.Maximum == 0 ? value.Current + 100 : value.Maximum);
        var slider = new Slider
        {
            Minimum = value.Minimum,
            Maximum = maximum,
            Value = Math.Min(maximum, Math.Max(value.Minimum, value.Current)),
            StepFrequency = 1,
        };
        slider.ValueChanged += (_, args) =>
        {
            if (_building) return;
            uint next = (uint)Math.Round(args.NewValue);
            caption.Text = $"{PowerDisplayService.DisplayNameFor(code)}: {next}";
            PowerDisplayService.SetValue(monitor.Id, code, next);
        };
        panel.Children.Add(slider);
        return panel;
    }

    private static ComboBox CreateProfileBox(IReadOnlyList<PowerDisplayProfile> profiles, string selectedId)
    {
        var box = new ComboBox { MinWidth = 180 };
        box.Items.Add(new ComboBoxItem { Content = P("None", "無"), Tag = "" });
        int selected = 0;
        for (int i = 0; i < profiles.Count; i++)
        {
            box.Items.Add(new ComboBoxItem { Content = profiles[i].Name, Tag = profiles[i].Id });
            if (profiles[i].Id == selectedId) selected = i + 1;
        }
        box.SelectedIndex = selected;
        return box;
    }

    private static string ProfileId(ComboBox box)
        => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    private static Border Card(UIElement child)
        => new() { Child = child, Padding = new Thickness(14), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1) };

    private static TextBlock Title(string text)
        => new() { Text = text, FontSize = 18, FontWeight = FontWeights.SemiBold };

    private static UIElement Labeled(string label, UIElement control)
    {
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(control);
        return panel;
    }

    private void SetMessage(string message, InfoBarSeverity severity)
    {
        _message = message;
        _messageSeverity = severity;
    }
}
