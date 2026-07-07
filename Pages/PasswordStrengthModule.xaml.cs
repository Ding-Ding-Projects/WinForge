using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 密碼強度分析 · Password-strength analyzer. Live entropy / pool / crack-time / checklist for a typed
/// password. Everything is computed locally in memory — the password is never stored, logged or transmitted.
/// Bilingual, never throws.
/// </summary>
public sealed partial class PasswordStrengthModule : Page
{
    private bool _syncing;

    public PasswordStrengthModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Analyze();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        // Best-effort: don't leave the typed secret sitting in the controls.
        try { _syncing = true; Hidden.Password = string.Empty; Shown.Text = string.Empty; _syncing = false; } catch { }
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        Analyze();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private string CurrentPassword => RevealSwitch.IsOn ? Shown.Text : Hidden.Password;

    private void Render()
    {
        try
        {
            Header.Title = P("Password Strength", "密碼強度");
            HeaderBlurb.Text = P("Type a password to see how strong it is — character variety, entropy, estimated crack time and a checklist of good habits.",
                "打個密碼入嚟，睇下佢有幾穩陣 — 字元種類、熵值、估計破解時間，仲有一張良好習慣清單。");
            PrivacyNote.Text = P("Everything runs on this PC. Your password is never saved, logged or sent anywhere.",
                "全部喺呢部電腦度計。你嘅密碼唔會儲存、唔會記錄、亦都唔會傳去任何地方。");
            EntryLabel.Text = P("Password to test", "要測試嘅密碼");
            RevealLabel.Text = P("Show the password", "顯示密碼");
            StrengthTitle.Text = P("Strength", "強度");
            MetricsHeader.Text = P("Details", "詳細資料");
            CrackHeader.Text = P("Estimated time to crack (average, brute force)", "估計破解時間（平均，暴力破解）");
            ChecklistHeader.Text = P("Checklist", "檢查清單");
        }
        catch { }
    }

    private void Hidden_Changed(object sender, RoutedEventArgs e) { if (!_syncing) Analyze(); }

    private void Shown_Changed(object sender, TextChangedEventArgs e) { if (!_syncing) Analyze(); }

    private void Reveal_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            _syncing = true;
            if (RevealSwitch.IsOn)
            {
                Shown.Text = Hidden.Password;
                Shown.Visibility = Visibility.Visible;
                Hidden.Visibility = Visibility.Collapsed;
            }
            else
            {
                Hidden.Password = Shown.Text;
                Hidden.Visibility = Visibility.Visible;
                Shown.Visibility = Visibility.Collapsed;
            }
        }
        catch { }
        finally { _syncing = false; }
    }

    private void Analyze()
    {
        try
        {
            var r = PasswordStrengthService.Analyze(CurrentPassword);

            StrengthBar.Value = r.Length == 0 ? 0 : r.Fraction;
            StrengthLabel.Text = r.Length == 0 ? P("—", "—") : BandLabel(r.Band);
            SetBarColor(r.Length == 0 ? -1 : r.Band);

            if (r.Length == 0)
            {
                StatusText.Text = P("Start typing to analyze a password.", "開始打字嚟分析密碼。");
                CommonWarn.IsOpen = false;
            }
            else
            {
                StatusText.Text = BandBlurb(r.Band);
                CommonWarn.IsOpen = r.IsCommon;
                if (r.IsCommon)
                {
                    CommonWarn.Title = P("Known common password", "常見密碼");
                    CommonWarn.Message = P("This appears in public breach lists — attackers try it first. Do not use it.",
                        "呢個出現喺公開洩漏名單度 — 攻擊者會第一時間試。唔好用。");
                }
            }

            LengthLine.Text = P($"Length:  {r.Length} characters", $"長度：  {r.Length} 個字元");
            PoolLine.Text = P($"Character pool:  {r.PoolSize} symbols", $"字元集合：  {r.PoolSize} 個符號");
            EntropyLine.Text = P($"Entropy:  {r.EntropyBits:0.0} bits", $"熵值：  {r.EntropyBits:0.0} bits");

            CrackOnline.Text = P(
                $"Online (throttled, ~10K/s):  {PasswordStrengthService.HumanTime(r.OnlineSeconds, P)}",
                $"線上（限速，約 1 萬次/秒）：  {PasswordStrengthService.HumanTime(r.OnlineSeconds, P)}");
            CrackGpu.Text = P(
                $"Offline GPU (~10B/s):  {PasswordStrengthService.HumanTime(r.OfflineGpuSeconds, P)}",
                $"離線 GPU（約 100 億次/秒）：  {PasswordStrengthService.HumanTime(r.OfflineGpuSeconds, P)}");
            CrackFast.Text = P(
                $"Fast rig (~1T/s):  {PasswordStrengthService.HumanTime(r.FastSeconds, P)}",
                $"高速機器（約 1 萬億次/秒）：  {PasswordStrengthService.HumanTime(r.FastSeconds, P)}");

            BuildChecklist(r);
        }
        catch
        {
            try { StatusText.Text = P("Could not analyze this input.", "無法分析呢個輸入。"); } catch { }
        }
    }

    private string BandLabel(int band) => band switch
    {
        0 => P("Very weak", "非常弱"),
        1 => P("Weak", "弱"),
        2 => P("Fair", "一般"),
        3 => P("Strong", "強"),
        _ => P("Very strong", "非常強"),
    };

    private string BandBlurb(int band) => band switch
    {
        0 => P("Cracked almost instantly. Make it much longer and more varied.", "幾乎即刻俾人破解。要長好多同埋更多變化。"),
        1 => P("Weak — easily guessed. Add length and character types.", "弱 — 好易估中。加長度同字元種類。"),
        2 => P("Fair — okay for low-value logins, not for anything important.", "一般 — 低價值帳戶勉強得，重要嘢就唔好。"),
        3 => P("Strong — good for most accounts.", "強 — 大部分帳戶都夠用。"),
        _ => P("Very strong — excellent for high-value accounts.", "非常強 — 高價值帳戶都好穩陣。"),
    };

    private void SetBarColor(int band)
    {
        try
        {
            Color c = band switch
            {
                -1 => Color.FromArgb(0xFF, 0x80, 0x80, 0x80),
                0 => Color.FromArgb(0xFF, 0xE8, 0x1A, 0x1A),
                1 => Color.FromArgb(0xFF, 0xE8, 0x7A, 0x1A),
                2 => Color.FromArgb(0xFF, 0xE8, 0xC8, 0x1A),
                3 => Color.FromArgb(0xFF, 0x5A, 0xC8, 0x3A),
                _ => Color.FromArgb(0xFF, 0x2E, 0xA8, 0x44),
            };
            StrengthBar.Foreground = new SolidColorBrush(c);
        }
        catch { }
    }

    private void BuildChecklist(PasswordStrengthService.Result r)
    {
        try
        {
            // Rebuild, keeping the header (first child).
            while (Checklist.Children.Count > 1)
                Checklist.Children.RemoveAt(Checklist.Children.Count - 1);

            AddCheck(r.Len8, P("At least 8 characters", "至少 8 個字元"));
            AddCheck(r.Len12, P("At least 12 characters", "至少 12 個字元"));
            AddCheck(r.Len16, P("At least 16 characters", "至少 16 個字元"));
            AddCheck(r.HasLower, P("Has lowercase letters", "有細楷字母"));
            AddCheck(r.HasUpper, P("Has uppercase letters", "有大楷字母"));
            AddCheck(r.HasDigit, P("Has digits", "有數字"));
            AddCheck(r.HasSymbol, P("Has symbols", "有符號"));
            AddCheck(r.NoRepeats, P("No 3+ repeated characters", "冇連續 3 個或以上重複字元"));
            AddCheck(r.NoSequences, P("No simple sequences (abc / 123 / qwerty)", "冇簡單序列（abc / 123 / qwerty）"));
            AddCheck(!r.IsCommon, P("Not a known common password", "唔係常見密碼"));
        }
        catch { }
    }

    private void AddCheck(bool ok, string text)
    {
        try
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var glyph = new FontIcon
            {
                Glyph = ok ? "" : "",   // CheckMark vs Cancel
                FontSize = 14,
                Foreground = new SolidColorBrush(ok
                    ? Color.FromArgb(0xFF, 0x2E, 0xA8, 0x44)
                    : Color.FromArgb(0xFF, 0xE8, 0x1A, 0x1A)),
            };
            var label = new TextBlock { Text = text, FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(glyph);
            row.Children.Add(label);
            Checklist.Children.Add(row);
        }
        catch { }
    }
}
