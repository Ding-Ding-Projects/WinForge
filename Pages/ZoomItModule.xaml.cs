using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// ZoomIt（原生克隆）控制頁 · ZoomIt control page — set the global hotkeys (default Ctrl+1 zoom,
/// Ctrl+2 draw, Ctrl+3 break), default pen colour/width and break minutes, and start each mode.
/// All work is done by <see cref="ZoomItService"/> on a pure-Win32 GDI overlay. Bilingual.
/// </summary>
public sealed partial class ZoomItModule : Page
{
    private bool _loading;

    // colour palette offered in the picker: (label en, label zh, 0xRRGGBB)
    private static readonly (string En, string Zh, int Rgb)[] Palette =
    {
        ("Red", "紅", 0xFF0000),
        ("Green", "綠", 0x00C000),
        ("Blue", "藍", 0x0078D4),
        ("Orange", "橙", 0xFF8C00),
        ("Yellow", "黃", 0xFFD400),
        ("White", "白", 0xFFFFFF),
        ("Black", "黑", 0x000000),
    };

    public ZoomItModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        ZoomItService.Fired += OnServiceFired;
        Loaded += (_, _) => { ZoomItService.Load(); ZoomItService.StartHotkeys(); PopulateKeys(); Render(); SyncFromState(); };
        Unloaded += (_, _) => ZoomItService.Fired -= OnServiceFired;
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnServiceFired()
    {
        if (DispatcherQueue is null) return;
        DispatcherQueue.TryEnqueue(() => StatusText.Text = ZoomItService.LastEvent);
    }

    private void PopulateKeys()
    {
        foreach (var combo in new[] { ZoomKey, DrawKey, BreakKey })
        {
            combo.Items.Clear();
            foreach (var (name, _) in HotkeyMacroService.PickableKeys)
                combo.Items.Add(name);
        }
    }

    private void Render()
    {
        Header.Title = "ZoomIt · 螢幕放大與標註";
        HeaderBlurb.Text = P("Zoom into any part of the screen, draw freehand or shapes on top, and run a full-screen break-timer countdown — a native clone of Sysinternals ZoomIt. Press a hotkey anywhere, even over other apps.",
            "放大螢幕任何位置、喺上面手畫或者畫圖形、仲有全螢幕小休倒數 —— Sysinternals ZoomIt 嘅原生克隆。喺任何地方（甚至蓋住其他 app）㩒熱鍵即用。");

        StartHeader.Text = P("Start a mode", "開始一個模式");
        StartBlurb.Text = P("Click a button below, or use the global hotkeys — they work even when WinForge is in the background.",
            "撳下面嘅按鈕，或者用全域熱鍵 —— 就算 WinForge 喺背景都用得。");
        ZoomBtn.Content = P("Zoom now", "立即放大");
        DrawBtn.Content = P("Draw now", "立即畫筆");
        BreakBtn.Content = P("Break timer", "小休倒數");

        HotkeyHeader.Text = P("Global hotkeys", "全域熱鍵");
        ZoomHkLabel.Text = P("Zoom (freeze + magnify)", "放大（凍結畫面 + 放大）");
        DrawHkLabel.Text = P("Draw (annotate the screen)", "畫筆（喺螢幕標註）");
        BreakHkLabel.Text = P("Break timer (countdown)", "小休倒數");

        DefaultsHeader.Text = P("Pen & break defaults", "畫筆與小休預設");
        PenColorLabel.Text = P("Default pen colour", "預設畫筆顏色");
        PenWidthLabel.Text = P("Default pen width (px)", "預設畫筆闊度（像素）");
        BreakMinLabel.Text = P("Break length (minutes)", "小休長度（分鐘）");
        SaveBtn.Content = P("Save settings", "儲存設定");

        HelpHeader.Text = P("How to use", "點樣用");
        HelpZoom.Text = P("• Zoom: move the mouse to pan, wheel or +/- to zoom in/out, drag to draw on the frozen frame, Esc to exit.",
            "• 放大：郁滑鼠平移、滾輪或 +/- 放大縮小、拖曳喺凍結畫面上畫畫、Esc 離開。");
        HelpDraw.Text = P("• Draw: drag to draw freehand on a frozen snapshot, wheel changes thickness, Esc clears then exits.",
            "• 畫筆：拖曳喺凍結快照上手畫、滾輪調粗幼、Esc 先清除再離開。");
        HelpBreak.Text = P("• Break: a big centred countdown over a dimmed screen; Esc ends it early.",
            "• 小休：暗化螢幕上一個置中嘅大倒數；Esc 提早結束。");
        HelpKeys.Text = P("Colour keys while drawing: r red · g green · b blue · o orange · y yellow.  Shape keys: E pen · K box · A arrow · H highlighter.  Right-click also exits.",
            "畫畫時嘅顏色鍵：r 紅 · g 綠 · b 藍 · o 橙 · y 黃。形狀鍵：E 筆 · K 框 · A 箭咀 · H 螢光筆。右鍵亦可離開。");

        // refresh the colour picker labels in the current language
        var sel = PenColorBox.SelectedIndex;
        PenColorBox.Items.Clear();
        foreach (var c in Palette) PenColorBox.Items.Add(P(c.En, c.Zh));
        PenColorBox.SelectedIndex = sel < 0 ? IndexOfColor(ZoomItService.PenColorRgb) : sel;

        StatusText.Text = string.IsNullOrEmpty(ZoomItService.LastEvent)
            ? P("Hotkeys are active.", "熱鍵已啟用。")
            : ZoomItService.LastEvent;
    }

    private static int IndexOfColor(int rgb)
    {
        for (int i = 0; i < Palette.Length; i++) if (Palette[i].Rgb == rgb) return i;
        return 0;
    }

    private void SyncFromState()
    {
        _loading = true;
        SetChord(ZoomCtrl, ZoomAlt, ZoomShift, ZoomWin, ZoomKey, ZoomItService.ZoomMods, ZoomItService.ZoomVk);
        SetChord(DrawCtrl, DrawAlt, DrawShift, DrawWin, DrawKey, ZoomItService.DrawMods, ZoomItService.DrawVk);
        SetChord(BreakCtrl, BreakAlt, BreakShift, BreakWin, BreakKey, ZoomItService.BreakMods, ZoomItService.BreakVk);
        PenColorBox.SelectedIndex = IndexOfColor(ZoomItService.PenColorRgb);
        PenWidthBox.Value = ZoomItService.PenWidth;
        BreakMinBox.Value = ZoomItService.BreakMinutes;
        _loading = false;
    }

    private static void SetChord(CheckBox ctrl, CheckBox alt, CheckBox shift, CheckBox win, ComboBox key, uint mods, uint vk)
    {
        var m = (HotMod)mods;
        ctrl.IsChecked = m.HasFlag(HotMod.Control);
        alt.IsChecked = m.HasFlag(HotMod.Alt);
        shift.IsChecked = m.HasFlag(HotMod.Shift);
        win.IsChecked = m.HasFlag(HotMod.Win);
        var idx = Array.FindIndex(HotkeyMacroService.PickableKeys, k => k.Vk == vk);
        key.SelectedIndex = idx < 0 ? 0 : idx;
    }

    private static (uint mods, uint vk) ReadChord(CheckBox ctrl, CheckBox alt, CheckBox shift, CheckBox win, ComboBox key)
    {
        uint mods = 0;
        if (ctrl.IsChecked == true) mods |= (uint)HotMod.Control;
        if (alt.IsChecked == true) mods |= (uint)HotMod.Alt;
        if (shift.IsChecked == true) mods |= (uint)HotMod.Shift;
        if (win.IsChecked == true) mods |= (uint)HotMod.Win;
        uint vk = 0;
        int i = key.SelectedIndex;
        if (i >= 0 && i < HotkeyMacroService.PickableKeys.Length) vk = HotkeyMacroService.PickableKeys[i].Vk;
        return (mods, vk);
    }

    // ===================== start buttons =====================

    private void ZoomBtn_Click(object sender, RoutedEventArgs e) => ZoomItService.OpenOverlay(ZoomItMode.Zoom);
    private void DrawBtn_Click(object sender, RoutedEventArgs e) => ZoomItService.OpenOverlay(ZoomItMode.Draw);

    private void BreakBtn_Click(object sender, RoutedEventArgs e)
    {
        // pick up the latest break minutes even if not yet saved
        var v = (int)(double.IsNaN(BreakMinBox.Value) ? ZoomItService.BreakMinutes : BreakMinBox.Value);
        ZoomItService.BreakMinutes = Math.Clamp(v, 1, 240);
        ZoomItService.OpenOverlay(ZoomItMode.Break);
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_loading) return;

        var (zm, zk) = ReadChord(ZoomCtrl, ZoomAlt, ZoomShift, ZoomWin, ZoomKey);
        var (dm, dk) = ReadChord(DrawCtrl, DrawAlt, DrawShift, DrawWin, DrawKey);
        var (bm, bk) = ReadChord(BreakCtrl, BreakAlt, BreakShift, BreakWin, BreakKey);

        // basic validation: every mode needs a key
        if (zk == 0 || dk == 0 || bk == 0)
        {
            Warn(P("Pick a key for each hotkey.", "每個熱鍵都要揀一個按鍵。"));
            return;
        }
        // basic conflict check
        if ((zm == dm && zk == dk) || (zm == bm && zk == bk) || (dm == bm && dk == bk))
        {
            Warn(P("Two hotkeys are the same — give each mode a different chord.", "有兩個熱鍵一樣 —— 請畀每個模式唔同嘅組合鍵。"));
            return;
        }

        ZoomItService.ZoomMods = zm; ZoomItService.ZoomVk = zk;
        ZoomItService.DrawMods = dm; ZoomItService.DrawVk = dk;
        ZoomItService.BreakMods = bm; ZoomItService.BreakVk = bk;

        int ci = PenColorBox.SelectedIndex;
        ZoomItService.PenColorRgb = ci >= 0 && ci < Palette.Length ? Palette[ci].Rgb : 0xFF0000;
        ZoomItService.PenWidth = Math.Clamp((int)(double.IsNaN(PenWidthBox.Value) ? 6 : PenWidthBox.Value), 1, 60);
        ZoomItService.BreakMinutes = Math.Clamp((int)(double.IsNaN(BreakMinBox.Value) ? 10 : BreakMinBox.Value), 1, 240);

        ZoomItService.Save();

        ResultBar.IsOpen = true;
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Saved", "已儲存");
        ResultBar.Message = P($"Zoom {ZoomItService.ChordText(zm, zk)} · Draw {ZoomItService.ChordText(dm, dk)} · Break {ZoomItService.ChordText(bm, bk)}",
            $"放大 {ZoomItService.ChordText(zm, zk)} · 畫筆 {ZoomItService.ChordText(dm, dk)} · 小休 {ZoomItService.ChordText(bm, bk)}");
    }

    private void Warn(string msg)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Check the hotkeys", "檢查熱鍵");
        ResultBar.Message = msg;
    }
}
