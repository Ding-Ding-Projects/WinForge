using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內音量混合器 · In-app per-app volume mixer (Core Audio / WASAPI) — master + every playing app,
/// live volume sliders and mute, plus EarTrumpet-style device picking: choose which playback device the
/// mixer shows, set a device as the system default, and move an app's stream to another output device.
/// Adversarially-verified COM interop. No redirect. Bilingual.
/// </summary>
public sealed partial class VolumeMixerModule : Page
{
    private static readonly string GlyphVol = ((char)0xE767).ToString();   // Volume
    private static readonly string GlyphMute = ((char)0xE74F).ToString();  // Mute
    private bool _suppress;

    private List<AudioDeviceInfo> _devices = new();
    private string _selectedDeviceId = "";   // "" => follow the default render endpoint
    private bool _loading;

    public VolumeMixerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Build();
        Loaded += (_, _) => Build();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Refresh_Click(object sender, RoutedEventArgs e) => Build();

    private void DevicePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        if (DevicePicker.SelectedItem is ComboBoxItem item && item.Tag is string id)
        {
            _selectedDeviceId = id;
            BuildCards();
        }
    }

    private void SetDefault_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedDeviceId))
        {
            ShowInfo(InfoBarSeverity.Informational,
                P("Already the default", "已經係預設"),
                P("This device is already the system default output.", "呢個裝置已經係系統預設輸出。"));
            return;
        }
        try
        {
            AudioMixer.SetDefaultEndpoint(_selectedDeviceId);
            ShowInfo(InfoBarSeverity.Success,
                P("Default device changed", "已更改預設裝置"),
                P("This device is now the system default output for all roles.",
                  "呢個裝置已成為所有用途嘅系統預設輸出。"));
            Build();   // re-enumerate so the default flag / labels refresh
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error,
                P("Could not set default device", "無法設定預設裝置"),
                P("Windows refused the change (build-specific COM call). ", "Windows 拒絕咗呢個更改（視乎版本嘅 COM 呼叫）。") + ex.Message);
        }
    }

    private void Build()
    {
        Header.Title = "Volume Mixer · 音量混合器";
        HeaderBlurb.Text = P("Pick a playback device, set the master level and every app's volume independently, make any device the system default, or move an app's stream to another device — like EarTrumpet, in-app.",
            "揀一個播放裝置，獨立設定主音量同每個 app 嘅音量，將任何裝置設為系統預設，或者將某個 app 嘅串流移去另一個裝置 — 好似 EarTrumpet 咁，但喺 app 內。");
        RefreshText.Text = P("Rescan", "重新掃描");
        DeviceLabel.Text = P("Output device", "輸出裝置");
        SetDefaultText.Text = P("Set as default", "設為預設");

        // ---- Enumerate render devices ----
        _loading = true;
        try { _devices = AudioMixer.GetRenderDevices(); }
        catch { _devices = new List<AudioDeviceInfo>(); }

        DevicePicker.Items.Clear();
        int selectIndex = 0;
        bool stillHaveSelected = false;
        for (int i = 0; i < _devices.Count; i++)
        {
            var d = _devices[i];
            var label = d.FriendlyName + (d.IsDefault ? P("  (default)", "  （預設）") : "");
            var item = new ComboBoxItem { Content = label, Tag = d.Id };
            DevicePicker.Items.Add(item);
            if (d.Id == _selectedDeviceId) { selectIndex = i; stillHaveSelected = true; }
            else if (string.IsNullOrEmpty(_selectedDeviceId) && d.IsDefault) selectIndex = i;
        }

        if (_devices.Count == 0)
        {
            _selectedDeviceId = "";
            DevicePicker.IsEnabled = false;
            SetDefaultBtn.IsEnabled = false;
        }
        else
        {
            DevicePicker.IsEnabled = true;
            SetDefaultBtn.IsEnabled = true;
            if (!stillHaveSelected)
                _selectedDeviceId = _devices[selectIndex].Id;
            DevicePicker.SelectedIndex = selectIndex;
        }
        _loading = false;

        BuildCards();
    }

    private void BuildCards()
    {
        Root.Children.Clear();
        HintBar.IsOpen = false;

        // The currently-selected device drives both the master + the session list.
        string devId = _selectedDeviceId;
        bool selectedIsDefault = false;
        foreach (var d in _devices)
            if (d.Id == devId) { selectedIsDefault = d.IsDefault; break; }
        SetDefaultBtn.IsEnabled = _devices.Count > 0 && !selectedIsDefault;

        // ---- Master (for the selected device) ----
        try
        {
            var (mLevel, mMuted) = string.IsNullOrEmpty(devId)
                ? AudioMixer.GetMaster()
                : AudioMixer.GetMasterFor(devId);
            string mid = devId;
            Root.Children.Add(Card(P("Device master volume", "裝置主音量"), "", mLevel, mMuted, accent: true, moveTargets: null,
                onLevel: v => { try { if (string.IsNullOrEmpty(mid)) AudioMixer.SetMasterLevel(v); else AudioMixer.SetMasterLevelFor(mid, v); } catch { } },
                onMute: m => { try { if (string.IsNullOrEmpty(mid)) AudioMixer.SetMasterMute(m); else AudioMixer.SetMasterMuteFor(mid, m); } catch { } }));
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error, P("No audio endpoint", "冇音訊裝置"), ex.Message);
            return;
        }

        // ---- Per-app sessions on the selected device ----
        List<AudioSession> sessions;
        try
        {
            sessions = string.IsNullOrEmpty(devId)
                ? AudioMixer.GetSessions()
                : AudioMixer.GetSessionsForDevice(devId);
        }
        catch { sessions = new List<AudioSession>(); }

        bool canMove = AudioPolicyConfig.IsSupported() && _devices.Count > 1;

        int shown = 0;
        foreach (var s in sessions)
        {
            if (string.IsNullOrEmpty(s.SessionId)) continue;
            var id = s.SessionId;
            var pid = s.Pid;
            // Only real processes can be moved (system sounds has pid 0).
            var moveTargets = (canMove && pid > 0) ? _devices : null;
            Root.Children.Add(Card(s.DisplayName, pid > 0 ? $"PID {pid}" : "", s.Level, s.Muted, accent: false,
                moveTargets: moveTargets,
                onLevel: v => { try { AudioMixer.SetSessionLevel(id, v); } catch { } },
                onMute: m => { try { AudioMixer.SetSessionMute(id, m); } catch { } },
                onMove: targetId => MoveSession(pid, s.DisplayName, targetId)));
            shown++;
        }

        if (shown == 0)
        {
            ShowInfo(InfoBarSeverity.Informational,
                P("No apps are playing on this device", "呢個裝置冇 app 喺度播緊聲"),
                P("Start playback (or pick another device), then Rescan.", "開始播放（或者揀第二個裝置），再重新掃描。"));
        }
    }

    private void MoveSession(int pid, string appName, string targetDeviceId)
    {
        try
        {
            bool ok = AudioPolicyConfig.SetAppDefaultDevice(pid, targetDeviceId);
            if (ok)
            {
                string target = "";
                foreach (var d in _devices) if (d.Id == targetDeviceId) { target = d.FriendlyName; break; }
                ShowInfo(InfoBarSeverity.Success,
                    P("Stream moved", "已移動串流"),
                    string.Format(P("\"{0}\" now plays to \"{1}\". Some apps only pick this up after a restart.",
                                    "「{0}」而家會喺「{1}」播放。部分 app 要重新啟動先生效。"), appName, target));
                Build();
            }
            else
            {
                ShowInfo(InfoBarSeverity.Warning,
                    P("Could not move the stream", "無法移動串流"),
                    P("This Windows build did not accept the per-app routing call.", "呢個 Windows 版本唔接受逐個 app 嘅路由呼叫。"));
            }
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error, P("Move failed", "移動失敗"), ex.Message);
        }
    }

    private void ShowInfo(InfoBarSeverity sev, string title, string msg)
    {
        HintBar.Severity = sev;
        HintBar.Title = title;
        HintBar.Message = msg;
        HintBar.IsOpen = true;
    }

    private Border Card(string title, string sub, float level, bool muted, bool accent,
        IReadOnlyList<AudioDeviceInfo> moveTargets,
        Action<float> onLevel, Action<bool> onMute, Action<string> onMove = null)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // mute
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // title + slider
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); // percent
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // move (optional)

        var muteBtn = new Button { Padding = new Thickness(9), VerticalAlignment = VerticalAlignment.Center };
        var muteIcon = new FontIcon { FontSize = 16, Glyph = muted ? GlyphMute : GlyphVol };
        muteBtn.Content = muteIcon;
        bool curMuted = muted;
        ToolTipService.SetToolTip(muteBtn, P("Mute / unmute", "靜音／取消"));
        Grid.SetColumn(muteBtn, 0);

        var mid = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var titleText = new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
        mid.Children.Add(titleText);
        if (!string.IsNullOrEmpty(sub))
            mid.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });

        var slider = new Slider { Minimum = 0, Maximum = 100, StepFrequency = 1, Margin = new Thickness(0, 2, 0, 0) };
        var pct = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalTextAlignment = TextAlignment.Right };

        _suppress = true;
        slider.Value = Math.Clamp((int)Math.Round(level * 100), 0, 100);
        pct.Text = $"{(int)slider.Value}%";
        _suppress = false;

        slider.ValueChanged += (_, e) =>
        {
            pct.Text = $"{(int)e.NewValue}%";
            if (_suppress) return;
            onLevel((float)(e.NewValue / 100.0));
            // Dragging the slider auto-unmutes, like the Windows volume mixer.
            if (curMuted)
            {
                curMuted = false;
                muteIcon.Glyph = GlyphVol;
                onMute(false);
            }
        };
        mid.Children.Add(slider);
        Grid.SetColumn(mid, 1);

        Grid.SetColumn(pct, 2);

        muteBtn.Click += (_, _) =>
        {
            curMuted = !curMuted;
            muteIcon.Glyph = curMuted ? GlyphMute : GlyphVol;
            onMute(curMuted);
        };

        grid.Children.Add(muteBtn);
        grid.Children.Add(mid);
        grid.Children.Add(pct);

        // ---- "Move to device" flyout (per-app only) ----
        if (moveTargets != null && moveTargets.Count > 0 && onMove != null)
        {
            var moveBtn = new Button { Padding = new Thickness(9), VerticalAlignment = VerticalAlignment.Center };
            moveBtn.Content = new FontIcon { FontSize = 16, Glyph = ((char)0xE8AB).ToString() }; // Switch/Redo
            ToolTipService.SetToolTip(moveBtn, P("Move this app to another output device", "將呢個 app 移去另一個輸出裝置"));

            var flyout = new MenuFlyout();
            foreach (var d in moveTargets)
            {
                var tid = d.Id;
                var mi = new MenuFlyoutItem
                {
                    Text = d.FriendlyName + (d.IsDefault ? P("  (default)", "  （預設）") : "")
                };
                mi.Click += (_, _) => onMove(tid);
                flyout.Items.Add(mi);
            }
            flyout.Items.Add(new MenuFlyoutSeparator());
            var reset = new MenuFlyoutItem { Text = P("Reset to system default", "重設為系統預設") };
            reset.Click += (_, _) => onMove(null);
            flyout.Items.Add(reset);

            moveBtn.Flyout = flyout;
            Grid.SetColumn(moveBtn, 3);
            grid.Children.Add(moveBtn);
        }

        return new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            Background = (Brush)Application.Current.Resources[accent ? "CardBackgroundFillColorSecondaryBrush" : "CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
    }
}
