using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內鍵盤重新對應（SharpKeys 式）· In-app keyboard remapper — build the HKLM Scancode Map to
/// remap or disable keys. Needs admin + reboot. No external tool, no redirect. Bilingual.
/// </summary>
public sealed partial class KeyboardModule : Page
{
    private List<KeyMap> _maps = new();

    public KeyboardModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); FillCombos(); RefreshList(); };
        Loaded += (_, _) => { Render(); FillCombos(); _maps = KeyboardRemapper.GetCurrent(); RefreshList(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Keyboard Remapper · 鍵盤重新對應";
        HeaderBlurb.Text = P("Remap one key to another, or disable it entirely. Writes the system Scancode Map — needs administrator and a reboot to take effect.",
            "將一個鍵改做另一個，或者完全停用佢。會寫入系統 Scancode Map — 需要管理員權限，重新開機先生效。");
        FromCap.Text = P("Map", "對應");
        AddBtn.Content = P("Add", "加入");
        ApplyBtn.Content = P("Apply (reboot)", "套用（重啟）");
        ClearBtn.Content = P("Clear all", "全部清除");
    }

    private void FillCombos()
    {
        int fromSel = FromBox.SelectedIndex, toSel = ToBox.SelectedIndex;
        FromBox.Items.Clear();
        ToBox.Items.Clear();
        foreach (var k in KeyboardRemapper.Keys)
        {
            FromBox.Items.Add(new ComboBoxItem { Content = $"{k.En} · {k.Zh}", Tag = k.Scancode });
            ToBox.Items.Add(new ComboBoxItem { Content = $"{k.En} · {k.Zh}", Tag = k.Scancode });
        }
        ToBox.Items.Add(new ComboBoxItem { Content = P("✕ Disable key", "✕ 停用此鍵"), Tag = (ushort)0 });
        FromBox.SelectedIndex = fromSel >= 0 ? fromSel : 0;
        ToBox.SelectedIndex = toSel >= 0 ? toSel : ToBox.Items.Count - 1; // default to Disable
    }

    private void RefreshList()
    {
        List.ItemsSource = null;
        List.ItemsSource = _maps;
        CountText.Text = P($"{_maps.Count} mapping(s)", $"{_maps.Count} 個對應");
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if ((FromBox.SelectedItem as ComboBoxItem)?.Tag is not ushort src) return;
        if ((ToBox.SelectedItem as ComboBoxItem)?.Tag is not ushort tgt) return;
        if (src == tgt) { Warn(P("Source and target are the same.", "來源同目標一樣。")); return; }

        _maps.RemoveAll(m => m.Source == src); // one mapping per source key
        _maps.Add(new KeyMap { Source = src, Target = tgt, SourceName = KeyboardRemapper.NameOf(src), TargetName = KeyboardRemapper.NameOf(tgt) });
        RefreshList();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is KeyMap m)
        {
            _maps.RemoveAll(x => x.Source == m.Source);
            RefreshList();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            KeyboardRemapper.Apply(_maps);
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Saved", "已儲存");
            ResultBar.Message = P($"{_maps.Count} mapping(s) written. Reboot for them to take effect.", $"已寫入 {_maps.Count} 個對應。重新開機後生效。");
            ResultBar.IsOpen = true;
        }
        catch (UnauthorizedAccessException) { NeedAdmin(); }
        catch (Exception ex) { Fail(ex.Message); }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            KeyboardRemapper.ClearAll();
            _maps.Clear();
            RefreshList();
            ResultBar.Severity = InfoBarSeverity.Success;
            ResultBar.Title = P("Cleared", "已清除");
            ResultBar.Message = P("All remaps removed. Reboot to restore default keys.", "已移除所有對應。重新開機回復預設鍵。");
            ResultBar.IsOpen = true;
        }
        catch (UnauthorizedAccessException) { NeedAdmin(); }
        catch (Exception ex) { Fail(ex.Message); }
    }

    private void NeedAdmin()
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = P("Writing the Scancode Map needs administrator rights — relaunch WinForge as admin.",
            "寫入 Scancode Map 需要管理員權限 — 請以管理員身分重開 WinForge。");
        ResultBar.IsOpen = true;
    }

    private void Fail(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
