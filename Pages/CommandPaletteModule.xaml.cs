using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 指令面板設定（PowerToys Run／Command Palette 式）· Command Palette control page — enable toggle,
/// hotkey picker, enabled-provider checkboxes and max-results. The launcher itself is a separate
/// borderless topmost window opened by the global hotkey (see CommandPaletteService / CommandPaletteWindow).
/// 全部介面文字雙語。Fully bilingual UI.
/// </summary>
public sealed partial class CommandPaletteModule : Page
{
    private bool _suppress;

    public CommandPaletteModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += (_, _) => { Render(); Sync(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Command Palette · 指令面板";
        HeaderBlurb.Text = P(
            "A global quick-launcher (like PowerToys Run). Press the hotkey anywhere to open a centered search box: launch installed apps, jump to WinForge modules, open files & folders, do quick math, run commands or URLs, trigger system actions, or search the web.",
            "全域快速啟動器（似 PowerToys Run）。喺任何地方按熱鍵就會彈出置中嘅搜尋框：啟動已安裝程式、跳去 WinForge 模組、開檔案同資料夾、快速計數、執行指令或網址、觸發系統動作，或者上網搜尋。");

        EnableTitle.Text = P("Enable Command Palette", "啟用指令面板");
        HotkeyLabel.Text = P("Hotkey", "熱鍵");
        OpenNowButton.Content = P("Open now", "立即打開");
        MaxLabel.Text = P("Max results", "最多結果數");
        ProvidersTitle.Text = P("Result providers", "結果提供者");
        ProvidersBlurb.Text = P("Choose which sources contribute results. Disable any you don't want.",
            "揀邊啲來源會貢獻結果。唔想用嘅可以關閉。");

        // Hotkey choices (preserve current).
        var cur = CommandPaletteService.HotkeyText;
        _suppress = true;
        HotkeyCombo.Items.Clear();
        foreach (var c in CommandPaletteService.HotkeyChoices) HotkeyCombo.Items.Add(c);
        if (!CommandPaletteService.HotkeyChoices.Contains(cur)) HotkeyCombo.Items.Add(cur);
        HotkeyCombo.SelectedItem = cur;
        _suppress = false;

        BuildProviders();
        UpdateStatus();
    }

    private void BuildProviders()
    {
        ProvidersPanel.Children.Clear();
        foreach (var p in CommandPaletteService.AllProviders)
        {
            var (en, zh) = CommandPaletteService.ProviderName(p);
            var chk = new CheckBox
            {
                Content = $"{en} · {zh}",
                IsChecked = CommandPaletteService.IsProviderEnabled(p),
                Tag = p,
            };
            chk.Checked += Provider_Changed;
            chk.Unchecked += Provider_Changed;
            ProvidersPanel.Children.Add(chk);
        }
    }

    private void Sync()
    {
        _suppress = true;
        EnableSwitch.IsOn = CommandPaletteService.Enabled;
        MaxBox.Value = CommandPaletteService.MaxResults;
        _suppress = false;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        bool on = CommandPaletteService.Enabled;
        EnableStatus.Text = on
            ? P($"On — press {CommandPaletteService.HotkeyText} anywhere to open.",
                $"已開 — 喺任何地方按 {CommandPaletteService.HotkeyText} 打開。")
            : P("Off — the global hotkey is not registered.", "已關 — 全域熱鍵未註冊。");
        OpenNowButton.IsEnabled = on;
    }

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        CommandPaletteService.Enabled = EnableSwitch.IsOn;
        CommandPaletteService.Reapply();
        UpdateStatus();
    }

    private void Hotkey_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (HotkeyCombo.SelectedItem is string hk && !string.IsNullOrWhiteSpace(hk))
        {
            CommandPaletteService.HotkeyText = hk;
            CommandPaletteService.Reapply();
            UpdateStatus();
        }
    }

    private void Max_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        if (!double.IsNaN(sender.Value)) CommandPaletteService.MaxResults = (int)sender.Value;
    }

    private void Provider_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (sender is CheckBox { Tag: CommandPaletteService.Provider p } chk)
            CommandPaletteService.SetProviderEnabled(p, chk.IsChecked == true);
    }

    private void OpenNow_Click(object sender, RoutedEventArgs e)
    {
        try { CommandPaletteWindow.Open(); } catch { }
    }
}
