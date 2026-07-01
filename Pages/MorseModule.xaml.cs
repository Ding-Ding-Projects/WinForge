using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI; // Color.FromArgb
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 摩斯電碼 · Morse code encoder / decoder with a visual flash preview. Text↔Morse
/// (International Morse), selectable separators, copy-out, unknown-char flagging, and a
/// DispatcherTimer-driven lamp that blinks the pattern at a chosen WPM. Bilingual, never throws.
/// </summary>
public sealed partial class MorseModule : Page
{
    private readonly DispatcherTimer _timer = new();
    private List<MorseService.Flash> _timeline = new();
    private int _flashIdx;
    private double _unitMs = 80;
    private bool _suppress;
    private bool _ready;

    // Separator presets (letter / word).
    private static readonly (string letter, string word)[] SepPresets =
    {
        (" ", " / "),   // space / slash
        (" ", "   "),   // space / triple-space
        ("  ", " / "),  // double-space / slash
    };

    private static readonly SolidColorBrush LampOn = new(Color.FromArgb(255, 80, 255, 140));
    private static readonly SolidColorBrush LampOff = new(Color.FromArgb(255, 24, 32, 28));

    public MorseModule()
    {
        InitializeComponent();
        _timer.Tick += Timer_Tick;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); _ready = true; Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; StopFlash(); };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Morse Code · 摩斯電碼";
            HeaderBlurb.Text = P(
                "Translate between text and International Morse code, then watch (or beam) the pattern as a blinking lamp. Letters split by spaces, words by \" / \".",
                "喺文字同國際摩斯電碼之間互換，仲可以睇住個訊號燈一閃一閃咁打出嚟。字母用空格分隔，字詞用「 / 」。");

            InputTitle.Text = P("Input", "輸入");
            InputSub.Text = DirectionSwitch.IsOn
                ? P("Morse → Text — paste dots and dashes.", "摩斯 → 文字 — 貼上點同劃。")
                : P("Text → Morse — type any message.", "文字 → 摩斯 — 打任何訊息。");
            ModeText2Morse.Text = P("Text→Morse", "文字→摩斯");
            ModeMorse2Text.Text = P("Morse→Text", "摩斯→文字");

            SepLabel.Text = P("Separators", "分隔符");
            OutputTitle.Text = P("Output", "輸出");
            CopyBtn.Content = P("Copy", "複製");

            FlashTitle.Text = P("Flash preview", "閃燈預覽");
            PlayBtn.Content = P("Play", "播放");
            StopBtn.Content = P("Stop", "停止");
            WpmLabel.Text = P("Speed (WPM)", "速度 (WPM)");

            RebuildSepBox();
            if (!_timer.IsEnabled) FlashStatus.Text = P("Idle.", "閒置中。");
            UpdateInputSub();
        }
        catch { /* never throw */ }
    }

    private void RebuildSepBox()
    {
        try
        {
            int keep = SepBox.SelectedIndex < 0 ? 0 : SepBox.SelectedIndex;
            _suppress = true;
            SepBox.Items.Clear();
            SepBox.Items.Add(P("Space / \" / \"  (standard)", "空格 / 「 / 」（標準）"));
            SepBox.Items.Add(P("Space / triple-space", "空格 / 三個空格"));
            SepBox.Items.Add(P("Double-space / \" / \"", "雙空格 / 「 / 」"));
            SepBox.SelectedIndex = Math.Clamp(keep, 0, SepBox.Items.Count - 1);
            _suppress = false;
        }
        catch { _suppress = false; }
    }

    private void UpdateInputSub()
    {
        InputSub.Text = DirectionSwitch.IsOn
            ? P("Morse → Text — paste dots and dashes.", "摩斯 → 文字 — 貼上點同劃。")
            : P("Text → Morse — type any message.", "文字 → 摩斯 — 打任何訊息。");
        // Separator choice only matters when encoding.
        SepPanel.Opacity = DirectionSwitch.IsOn ? 0.4 : 1.0;
        SepBox.IsEnabled = !DirectionSwitch.IsOn;
    }

    private void Direction_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        UpdateInputSub();
        Recompute();
    }

    private void Sep_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private (string letter, string word) CurrentSep()
    {
        int i = SepBox.SelectedIndex;
        if (i < 0 || i >= SepPresets.Length) i = 0;
        return SepPresets[i];
    }

    private void Recompute()
    {
        if (!_ready) return;
        try
        {
            string input = InputBox.Text ?? string.Empty;
            if (DirectionSwitch.IsOn)
            {
                // Morse → Text
                string text = MorseService.FromMorse(input);
                OutputBox.Text = text;
                bool hasUnknown = text.Contains('�') || text.Contains('#');
                if (hasUnknown)
                {
                    UnknownText.Text = P("Some symbols could not be decoded (shown as �).",
                                         "有啲符號解唔到（顯示做 �）。");
                    UnknownText.Visibility = Visibility.Visible;
                }
                else UnknownText.Visibility = Visibility.Collapsed;

                // Flash the decoded text.
                _timeline = MorseService.BuildTimeline(text);
            }
            else
            {
                // Text → Morse
                var (l, w) = CurrentSep();
                string morse = MorseService.ToMorse(input, l, w, out var unknown);
                OutputBox.Text = morse;
                if (unknown.Count > 0)
                {
                    string list = string.Join(" ", unknown.Select(c => c == ' ' ? "␠" : c.ToString()));
                    UnknownText.Text = P($"Unsupported characters (marked #): {list}",
                                         $"唔支援嘅字元（標記 #）：{list}");
                    UnknownText.Visibility = Visibility.Visible;
                }
                else UnknownText.Visibility = Visibility.Collapsed;

                _timeline = MorseService.BuildTimeline(input);
            }
        }
        catch { /* never throw */ }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string txt = OutputBox.Text ?? string.Empty;
            if (txt.Length == 0)
            {
                ShowInfo(P("Nothing to copy yet.", "暫時無嘢可以複製。"), InfoBarSeverity.Informational);
                return;
            }
            var dp = new DataPackage();
            dp.SetText(txt);
            Clipboard.SetContent(dp);
            ShowInfo(P("Copied output to the clipboard.", "已複製輸出到剪貼簿。"), InfoBarSeverity.Success);
        }
        catch
        {
            ShowInfo(P("Could not access the clipboard.", "無法存取剪貼簿。"), InfoBarSeverity.Error);
        }
    }

    private void ShowInfo(string msg, InfoBarSeverity sev)
    {
        try { Info.Message = msg; Info.Severity = sev; Info.IsOpen = true; } catch { }
    }

    // ── Flash preview ──────────────────────────────────────────────
    private void Play_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            StopFlash();
            if (_timeline.Count == 0)
            {
                FlashStatus.Text = P("Nothing to flash — enter a message first.", "無嘢可以閃 — 先輸入訊息。");
                SetLamp(false);
                return;
            }
            _unitMs = MorseService.UnitMsForWpm(WpmBox.Value);
            _flashIdx = -1;
            _timer.Interval = TimeSpan.FromMilliseconds(1); // kick immediately
            _timer.Start();
            PlayBtn.IsEnabled = false;
            StopBtn.IsEnabled = true;
            FlashStatus.Text = P("Flashing…", "閃緊…");
        }
        catch { StopFlash(); }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopFlash();
        FlashStatus.Text = P("Stopped.", "已停止。");
    }

    private void Timer_Tick(object? sender, object e)
    {
        try
        {
            _flashIdx++;
            if (_flashIdx >= _timeline.Count)
            {
                StopFlash();
                FlashStatus.Text = P("Done.", "完成。");
                return;
            }
            var f = _timeline[_flashIdx];
            SetLamp(f.On);
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, f.Units * _unitMs));
        }
        catch { StopFlash(); }
    }

    private void StopFlash()
    {
        try
        {
            _timer.Stop();
            SetLamp(false);
            PlayBtn.IsEnabled = true;
            StopBtn.IsEnabled = false;
        }
        catch { }
    }

    private void SetLamp(bool on)
    {
        try { Lamp.Background = on ? LampOn : LampOff; } catch { }
    }
}
