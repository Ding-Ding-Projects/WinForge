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
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); Sync(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Command Palette · 指令面板";
        HeaderBlurb.Text = P(
            "A global quick-launcher (like PowerToys Run and Command Palette). Press the hotkey anywhere to open a centered search box: launch apps, modules, files and Terminal profiles; browse local clipboard history; use time/date; type $display for Windows Settings; or manage services with service start/stop/restart <name>.",
            "全域快速啟動器（似 PowerToys Run 同 Command Palette）。喺任何地方按熱鍵就會彈出置中搜尋框：啟動程式、模組、檔案同終端機設定檔；瀏覽本機剪貼簿記錄；查時間／日期；輸入 $顯示器 開 Windows 設定；或者用 service start／stop／restart <名稱> 管理服務。");

        EnableTitle.Text = P("Enable Command Palette", "啟用指令面板");
        HotkeyLabel.Text = P("Hotkey", "熱鍵");
        OpenNowButton.Content = P("Open now", "立即打開");
        MaxLabel.Text = P("Max results", "最多結果數");
        ProvidersTitle.Text = P("Result providers", "結果提供者");
        ProvidersBlurb.Text = P("Choose which sources contribute results. Disable any you don't want.",
            "揀邊啲來源會貢獻結果。唔想用嘅可以關閉。");
        DockTitle.Text = P("Command Palette Dock", "指令面板 Dock");
        DockBlurb.Text = P(
            "Keep a compact launcher on any screen edge. In the palette, press Ctrl+P to pin or unpin the selected result; saved pins stay on the Dock.",
            "喺任何螢幕邊緣保留精簡啟動器。喺指令面板入面按 Ctrl+P 就可以釘選或者取消釘選所揀結果；已儲存嘅釘選會留喺 Dock。");
        DockSideLabel.Text = P("Dock edge", "Dock 位置");
        DockOpenButton.Content = P("Show Dock", "顯示 Dock");

        // Hotkey choices (preserve current).
        var cur = CommandPaletteService.HotkeyText;
        _suppress = true;
        HotkeyCombo.Items.Clear();
        foreach (var c in CommandPaletteService.HotkeyChoices) HotkeyCombo.Items.Add(c);
        if (!CommandPaletteService.HotkeyChoices.Contains(cur)) HotkeyCombo.Items.Add(cur);
        HotkeyCombo.SelectedItem = cur;
        _suppress = false;

        BuildProviders();
        BuildDockSides();
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
        DockSwitch.IsOn = CommandPaletteDockService.Enabled;
        SelectDockSide();
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
        DockOpenButton.IsEnabled = on;
        var side = DockSideName(CommandPaletteDockService.Side);
        DockStatus.Text = CommandPaletteDockService.Enabled && on
            ? P($"On — docked at the {side.En} edge. Ctrl+P pins palette results.", $"已開 — 停靠喺{side.Zh}邊。Ctrl+P 可以釘選指令面板結果。")
            : P("Off — enable the palette and Dock to keep the edge launcher visible.", "已關 — 啟用指令面板同 Dock 後，邊緣啟動器先會保持顯示。");
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

    private void BuildDockSides()
    {
        _suppress = true;
        DockSideCombo.Items.Clear();
        AddDockSide(CommandPaletteDockSide.Top, "Top", "頂部");
        AddDockSide(CommandPaletteDockSide.Bottom, "Bottom", "底部");
        AddDockSide(CommandPaletteDockSide.Left, "Left", "左邊");
        AddDockSide(CommandPaletteDockSide.Right, "Right", "右邊");
        SelectDockSide();
        _suppress = false;
    }

    private void AddDockSide(CommandPaletteDockSide side, string en, string zh)
        => DockSideCombo.Items.Add(new ComboBoxItem { Content = $"{en} · {zh}", Tag = side });

    private void SelectDockSide()
    {
        for (int i = 0; i < DockSideCombo.Items.Count; i++)
        {
            if (DockSideCombo.Items[i] is ComboBoxItem { Tag: CommandPaletteDockSide side } && side == CommandPaletteDockService.Side)
            {
                DockSideCombo.SelectedIndex = i;
                return;
            }
        }
    }

    private (string En, string Zh) DockSideName(CommandPaletteDockSide side) => side switch
    {
        CommandPaletteDockSide.Top => ("top", "頂部"),
        CommandPaletteDockSide.Left => ("left", "左"),
        CommandPaletteDockSide.Right => ("right", "右"),
        _ => ("bottom", "底部"),
    };

    private void Dock_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        CommandPaletteDockService.Enabled = DockSwitch.IsOn;
        CommandPaletteDockService.Reapply();
        UpdateStatus();
    }

    private void DockSide_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (DockSideCombo.SelectedItem is ComboBoxItem { Tag: CommandPaletteDockSide side })
        {
            CommandPaletteDockService.Side = side;
            CommandPaletteDockService.Reapply();
            UpdateStatus();
        }
    }

    private void DockOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!CommandPaletteService.Enabled)
        {
            CommandPaletteService.Enabled = true;
            CommandPaletteService.Reapply();
            _suppress = true;
            EnableSwitch.IsOn = true;
            _suppress = false;
        }
        CommandPaletteDockService.Enabled = true;
        _suppress = true;
        DockSwitch.IsOn = true;
        _suppress = false;
        CommandPaletteDockService.Reapply();
        UpdateStatus();
    }
}
