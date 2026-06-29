using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 快速重音符設定頁 · Quick Accent settings page.
/// 控制開關、啟動鍵、候選列位置、輸入延遲、語言／符號集，並提供即時預覽。
/// Configures the enable toggle, activation key, popup position, input delay, character sets, with a live preview.
/// </summary>
public sealed partial class QuickAccentModule : Page
{
    private bool _building; // suppress event handlers while we populate controls
    private readonly Dictionary<string, CheckBox> _setChecks = new();

    public QuickAccentModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        QuickAccentService.Load();
        BuildSetsList();
        RenderTexts();
        LoadIntoControls();
        // Start the hook if it was previously enabled.
        QuickAccentService.Apply();
        UpdateStatus();
        RefreshPreview();
    }

    private void OnLang(object? s, EventArgs e)
    {
        RenderTexts();
        UpdateStatus();
        RefreshPreview();
    }

    // ===================== text / localisation =====================

    private void RenderTexts()
    {
        Header.Title = P("Quick Accent", "快速重音符");
        HeaderBlurb.Text = P(
            "Type accented and special characters by holding a base letter and pressing an activation key.",
            "按住一個基底字母再撳啟動鍵，即可輸入重音符及特殊字元。");

        EnableLabel.Text = P("Enable Quick Accent", "啟用快速重音符");
        EnableHint.Text = P("Runs a global keyboard hook in the background.", "喺背景執行一個全域鍵盤鈎。");
        HowToText.Text = P(
            "How to use: hold a letter (e.g. a), then tap the activation key (Space, or Left/Right Arrow). A small popup shows the variants. Tap the activation key or arrows to move the selection, then release the letter to insert the highlighted character.",
            "用法：按住一個字母（例如 a），然後輕撳啟動鍵（Space 或 左／右箭咀）。會彈出一個細視窗顯示變體。再撳啟動鍵或箭咀移動選擇，放手就會插入高亮咗嘅字元。");

        BehaviourHeader.Text = P("Behaviour", "行為");
        ActivationLabel.Text = P("Activation key", "啟動鍵");
        PositionLabel.Text = P("Popup position", "候選列位置");
        DelayLabel.Text = P("Input delay", "輸入延遲");
        DelayUnit.Text = P("milliseconds (hold time before the popup shows)", "毫秒（彈出前要按住嘅時間）");
        LeftStartChk.Content = P("Always start selection from the left", "永遠由最左開始揀");

        SetsHeader.Text = P("Character sets", "字元集");
        SetsHint.Text = P("Pick the languages and symbol sets whose accents you want available.", "揀你想用嘅語言同符號集。");
        AllChk.Content = P("All sets", "全部字元集");

        PreviewHeader.Text = P("Preview", "預覽");
        PreviewLabel.Text = P("Type a base letter:", "輸入一個基底字母：");

        RebuildActivationItems();
        RebuildPositionItems();
        RenderSetLabels();
    }

    private void RebuildActivationItems()
    {
        _building = true;
        int sel = ActivationBox.SelectedIndex < 0 ? (int)QuickAccentService.Settings.ActivationKey : ActivationBox.SelectedIndex;
        ActivationBox.Items.Clear();
        ActivationBox.Items.Add(P("Space + Arrows", "Space ＋ 箭咀"));     // Both
        ActivationBox.Items.Add(P("Space only", "淨係 Space"));            // Space
        ActivationBox.Items.Add(P("Left / Right Arrow only", "淨係左／右箭咀")); // LeftRightArrow
        ActivationBox.SelectedIndex = Math.Clamp(sel, 0, 2);
        _building = false;
    }

    private void RebuildPositionItems()
    {
        _building = true;
        int sel = PositionBox.SelectedIndex < 0 ? (int)QuickAccentService.Settings.Position : PositionBox.SelectedIndex;
        PositionBox.Items.Clear();
        PositionBox.Items.Add(P("Near caret / cursor", "跟住游標"));   // Caret
        PositionBox.Items.Add(P("Top centre", "螢幕頂中"));            // Top
        PositionBox.Items.Add(P("Bottom centre", "螢幕底中"));         // Bottom
        PositionBox.Items.Add(P("Screen centre", "螢幕中央"));         // Center
        PositionBox.SelectedIndex = Math.Clamp(sel, 0, 3);
        _building = false;
    }

    // ===================== character-set checkboxes =====================

    private void BuildSetsList()
    {
        var items = new List<CheckBox>();
        _setChecks.Clear();
        foreach (var lang in QuickAccentData.All
            .OrderBy(l => l.Group)
            .ThenBy(l => l.En, StringComparer.Ordinal))
        {
            var chk = new CheckBox { Tag = lang.Id, MinWidth = 200 };
            chk.Checked += SetCheck_Changed;
            chk.Unchecked += SetCheck_Changed;
            _setChecks[lang.Id] = chk;
            items.Add(chk);
        }
        SetsList.ItemsSource = items;
        RenderSetLabels();
    }

    private void RenderSetLabels()
    {
        foreach (var lang in QuickAccentData.All)
        {
            if (_setChecks.TryGetValue(lang.Id, out var chk))
                chk.Content = $"{P(lang.En, lang.Zh)}";
        }
    }

    // ===================== load / save =====================

    private void LoadIntoControls()
    {
        _building = true;
        var s = QuickAccentService.Settings;
        EnableToggle.IsOn = s.Enabled;
        ActivationBox.SelectedIndex = (int)s.ActivationKey;
        PositionBox.SelectedIndex = (int)s.Position;
        DelayBox.Value = s.InputDelayMs;
        LeftStartChk.IsChecked = s.StartSelectionFromTheLeft;

        bool all = s.SelectedSets.Any(x => x.Equals("ALL", StringComparison.OrdinalIgnoreCase));
        AllChk.IsChecked = all;
        foreach (var (id, chk) in _setChecks)
        {
            chk.IsChecked = all || s.SelectedSets.Contains(id, StringComparer.OrdinalIgnoreCase);
            chk.IsEnabled = !all;
        }
        _building = false;
    }

    private void CommitFromControls()
    {
        if (_building) return;
        var s = QuickAccentService.Settings;
        s.ActivationKey = (QuickAccentActivationKey)Math.Max(0, ActivationBox.SelectedIndex);
        s.Position = (QuickAccentPosition)Math.Max(0, PositionBox.SelectedIndex);
        s.InputDelayMs = (int)Math.Round(double.IsNaN(DelayBox.Value) ? 200 : DelayBox.Value);
        s.StartSelectionFromTheLeft = LeftStartChk.IsChecked == true;

        if (AllChk.IsChecked == true)
        {
            s.SelectedSets = new List<string> { "ALL" };
        }
        else
        {
            s.SelectedSets = _setChecks.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            if (s.SelectedSets.Count == 0) s.SelectedSets.Add("ALL"); // never empty
        }

        QuickAccentService.Save();
        RefreshPreview();
    }

    // ===================== event handlers =====================

    private void EnableToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_building) return;
        QuickAccentService.SetEnabled(EnableToggle.IsOn);
        UpdateStatus();
    }

    private void Setting_Changed(object sender, RoutedEventArgs e) => CommitFromControls();
    private void Setting_Changed(object sender, SelectionChangedEventArgs e) => CommitFromControls();

    private void Delay_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => CommitFromControls();

    private void AllChk_Changed(object sender, RoutedEventArgs e)
    {
        if (_building) return;
        bool all = AllChk.IsChecked == true;
        _building = true;
        foreach (var chk in _setChecks.Values)
        {
            if (all) chk.IsChecked = true;
            chk.IsEnabled = !all;
        }
        _building = false;
        CommitFromControls();
    }

    private void SetCheck_Changed(object sender, RoutedEventArgs e) => CommitFromControls();

    private void Preview_Changed(object sender, TextChangedEventArgs e) => RefreshPreview();

    // ===================== preview + status =====================

    private void RefreshPreview()
    {
        var text = PreviewInput.Text;
        if (string.IsNullOrEmpty(text))
        {
            PreviewResult.Text = P("(type a letter above)", "（喺上面輸入一個字母）");
            return;
        }

        var variants = QuickAccentService.PreviewFor(text[0]);
        PreviewResult.Text = variants.Length == 0
            ? P("No accents for this character in the selected sets.", "選定字元集冇呢個字元嘅重音符。")
            : string.Join("   ", variants);
    }

    private void UpdateStatus()
    {
        if (QuickAccentService.Settings.Enabled)
        {
            StatusBar.Severity = InfoBarSeverity.Success;
            StatusBar.Title = P("Active", "已啟用");
            StatusBar.Message = P("Quick Accent is listening. Hold a letter and press the activation key.",
                                  "快速重音符正在監聽。按住字母再撳啟動鍵。");
            StatusBar.IsOpen = true;
        }
        else
        {
            StatusBar.Severity = InfoBarSeverity.Informational;
            StatusBar.Title = P("Disabled", "已停用");
            StatusBar.Message = P("Turn on the toggle above to start.", "開啟上面嘅開關即可使用。");
            StatusBar.IsOpen = true;
        }
    }
}
