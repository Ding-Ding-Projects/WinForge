using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內音量混合器 · In-app per-app volume mixer (Core Audio / WASAPI) — master + every playing app,
/// live volume sliders and mute. Adversarially-verified COM interop. No redirect. Bilingual.
/// </summary>
public sealed partial class VolumeMixerModule : Page
{
    private static readonly string GlyphVol = ((char)0xE767).ToString();   // Volume
    private static readonly string GlyphMute = ((char)0xE74F).ToString();  // Mute
    private bool _suppress;

    public VolumeMixerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Build();
        Loaded += (_, _) => Build();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Refresh_Click(object sender, RoutedEventArgs e) => Build();

    private void Build()
    {
        HeaderTitle.Text = "Volume Mixer · 音量混合器";
        HeaderBlurb.Text = P("Set the master level and every app's volume independently — like the Windows mixer, in-app. Mute or slide; it applies instantly.",
            "獨立設定主音量同每個 app 嘅音量 — 好似 Windows 混音器咁，但喺 app 內。靜音或者拉 slider，即時生效。");
        RefreshText.Text = P("Rescan", "重新掃描");
        Root.Children.Clear();

        // ---- Master ----
        try
        {
            var (mLevel, mMuted) = AudioMixer.GetMaster();
            Root.Children.Add(Card(P("Master volume", "主音量"), "", mLevel, mMuted, accent: true,
                onLevel: v => { try { AudioMixer.SetMasterLevel(v); } catch { } },
                onMute: m => { try { AudioMixer.SetMasterMute(m); } catch { } }));
        }
        catch (Exception ex)
        {
            HintBar.Severity = InfoBarSeverity.Error;
            HintBar.Title = P("No audio endpoint", "冇音訊裝置");
            HintBar.Message = ex.Message;
            HintBar.IsOpen = true;
            return;
        }

        // ---- Per-app sessions ----
        List<AudioSession> sessions;
        try { sessions = AudioMixer.GetSessions(); }
        catch { sessions = new List<AudioSession>(); }

        int shown = 0;
        foreach (var s in sessions)
        {
            if (string.IsNullOrEmpty(s.SessionId)) continue;
            var id = s.SessionId;
            Root.Children.Add(Card(s.DisplayName, s.Pid > 0 ? $"PID {s.Pid}" : "", s.Level, s.Muted, accent: false,
                onLevel: v => { try { AudioMixer.SetSessionLevel(id, v); } catch { } },
                onMute: m => { try { AudioMixer.SetSessionMute(id, m); } catch { } }));
            shown++;
        }

        if (shown == 0)
        {
            HintBar.Severity = InfoBarSeverity.Informational;
            HintBar.Title = P("No apps are playing audio", "冇 app 喺度播緊聲");
            HintBar.Message = P("Start playback in an app, then Rescan.", "喺某個 app 開始播放，再重新掃描。");
            HintBar.IsOpen = true;
        }
        else { HintBar.IsOpen = false; }
    }

    private Border Card(string title, string sub, float level, bool muted, bool accent, Action<float> onLevel, Action<bool> onMute)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // mute
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // title + slider
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) }); // percent

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
