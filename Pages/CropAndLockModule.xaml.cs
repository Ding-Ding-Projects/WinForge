using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 裁切與鎖定 · Crop And Lock — a native clone of PowerToys Crop And Lock. Pick a top-level window and
/// drag a region, then spawn a small always-on-top floating window that either live-mirrors that region
/// (Thumbnail mode, via DWM thumbnails), shows a safe reparent-style crop, or preserves a frozen Screenshot.
/// The spawned windows are movable, resizable, closable and stay on top. A configurable global hotkey can trigger
/// every flow. This control page enables the module, edits the three hotkeys, lists open windows to pick
/// from, and tracks the active cropped windows with close buttons. Bilingual throughout.
/// </summary>
public sealed partial class CropAndLockModule : Page
{
    public CropAndLockModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        CropAndLockService.Changed += OnServiceChanged;
        CropAndLockService.HotkeyPickRequested += OnHotkeyPick;
        Loaded += (_, _) =>
        {
            if (CropAndLockService.Enabled) CropAndLockService.EnsureStarted();
            Render();
            ReloadWindows();
            RefreshActive();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            CropAndLockService.Changed -= OnServiceChanged;
            CropAndLockService.HotkeyPickRequested -= OnHotkeyPick;
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnServiceChanged()
    {
        if (DispatcherQueue is null) return;
        DispatcherQueue.TryEnqueue(() => { RenderHotkeys(); RefreshActive(); });
    }

    private void Render()
    {
        Header.Title = "Crop And Lock · 裁切與鎖定";
        HeaderBlurb.Text = P(
            "Pick a window and drag a region, then float an always-on-top thumbnail, safe cropped view, or frozen screenshot. The floating windows are movable, resizable and closable.",
            "揀一個視窗、拖一個範圍，就會浮出一個置頂縮圖、安全裁切檢視或者靜態截圖。浮窗可以移動、縮放同關閉。");

        EnableTitle.Text = P("Enable Crop And Lock", "啟用裁切與鎖定");
        EnableBlurb.Text = P("Turn on the background engine and the global hotkeys below.",
            "開啟背景引擎同下面嘅全域熱鍵。");
        EnableSwitch.IsOn = CropAndLockService.Enabled;

        ThumbHkLabel.Text = P("Thumbnail hotkey", "縮圖熱鍵");
        CropHkLabel.Text = P("Reparent / crop hotkey", "重新指定父視窗／裁切熱鍵");
        ScreenshotHkLabel.Text = P("Screenshot hotkey", "截圖熱鍵");
        ThumbHkBtn.Content = P("Change…", "更改…");
        CropHkBtn.Content = P("Change…", "更改…");
        ScreenshotHkBtn.Content = P("Change…", "更改…");

        PickHeader.Text = P("Pick a window & region", "揀視窗同範圍");
        PickBlurb.Text = P("Select an open window on the left, then choose a live thumbnail, safe crop, or frozen screenshot. You'll drag a rectangle over that window to set the region.",
            "喺左邊揀一個開住嘅視窗，再揀即時縮圖、安全裁切或者靜態截圖。之後喺嗰個視窗上面拖一個方框去定範圍。");
        RefreshBtn.Content = P("Refresh", "重新整理");

        ThumbBtn.Content = P("Thumbnail of region", "範圍縮圖");
        ThumbDesc.Text = P("Live always-on-top mirror of the region — great for watching a window behind others.",
            "範圍嘅即時置頂鏡像 — 適合監察被遮住嘅視窗。");
        CropBtn.Content = P("Crop to region", "裁切到範圍");
        CropDesc.Text = P("A live, safe reparent-style cropped view without fragile cross-process SetParent calls.",
            "即時、安全、重新指定父視窗風格嘅裁切檢視，唔會做脆弱嘅跨程序 SetParent。");
        ScreenshotBtn.Content = P("Screenshot of region", "範圍截圖");
        ScreenshotDesc.Text = P("A frozen snapshot of the selected pixels. It stays on top but does not update with the source.",
            "所選像素嘅靜態快照。會保持置頂，但唔會跟來源更新。");

        ActiveHeader.Text = P("Active windows", "活躍視窗");
        ActiveBlurb.Text = P("Cropped and thumbnail windows you've created. Close them here or from their own title bar.",
            "你建立咗嘅裁切同縮圖視窗。可以喺呢度或者佢哋自己嘅標題列關閉。");
        CloseAllBtn.Content = P("Close all", "全部關閉");
        EmptyActive.Text = P("None yet. Pick a window and a mode above.", "暫時冇。喺上面揀視窗同模式。");

        LimitBar.Title = P("How it works", "運作方式");
        LimitBar.Message = P(
            "Thumbnail and Crop use DWM thumbnails (DwmRegisterThumbnail) and stay live. True cross-process reparenting is fragile, so Crop remains a safe DWM-thumbnail view; Screenshot uses an in-process GDI capture and is intentionally frozen.",
            "縮圖同裁切會用 DWM 縮圖（DwmRegisterThumbnail）並保持即時。真正跨程序重新指定父視窗好脆弱，所以裁切保持安全嘅 DWM 縮圖檢視；截圖會用 app 內 GDI 擷取並刻意保持靜態。");

        RenderHotkeys();
    }

    private void RenderHotkeys()
    {
        ThumbHkText.Text = CropAndLockService.ThumbHotkey.Text();
        CropHkText.Text = CropAndLockService.CropHotkey.Text();
        ScreenshotHkText.Text = CropAndLockService.ScreenshotHotkey.Text();
    }

    // ===================== enable + hotkeys =====================

    private void Enable_Toggled(object sender, RoutedEventArgs e)
    {
        CropAndLockService.Enabled = EnableSwitch.IsOn;
        if (EnableSwitch.IsOn) CropAndLockService.EnsureStarted();
        Info(EnableSwitch.IsOn ? P("Enabled", "已啟用") : P("Disabled", "已停用"),
             EnableSwitch.IsOn ? P("Crop And Lock is on. Use the hotkeys or the buttons below.", "裁切與鎖定已開。可用熱鍵或下面嘅按鈕。")
                               : P("The global hotkeys are unregistered.", "全域熱鍵已取消登記。"));
    }

    private async void ThumbHk_Click(object sender, RoutedEventArgs e) => await CaptureHotkey(CropLockMode.Thumbnail);
    private async void CropHk_Click(object sender, RoutedEventArgs e) => await CaptureHotkey(CropLockMode.Reparent);
    private async void ScreenshotHk_Click(object sender, RoutedEventArgs e) => await CaptureHotkey(CropLockMode.Screenshot);

    private async System.Threading.Tasks.Task CaptureHotkey(CropLockMode mode)
    {
        var current = mode switch
        {
            CropLockMode.Thumbnail => CropAndLockService.ThumbHotkey,
            CropLockMode.Reparent => CropAndLockService.CropHotkey,
            _ => CropAndLockService.ScreenshotHotkey,
        };

        var ctrl = new CheckBox { Content = "Ctrl", IsChecked = (current.Modifiers & 0x0002) != 0 };
        var alt = new CheckBox { Content = "Alt", IsChecked = (current.Modifiers & 0x0001) != 0 };
        var shift = new CheckBox { Content = "Shift", IsChecked = (current.Modifiers & 0x0004) != 0 };
        var win = new CheckBox { Content = "Win", IsChecked = (current.Modifiers & 0x0008) != 0 };

        var keyCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, Header = P("Key", "按鍵") };
        foreach (var (name, vk) in HotkeyMacroService.PickableKeys)
            keyCombo.Items.Add(new ComboBoxItem { Content = name, Tag = vk });
        // preselect the current key
        for (int i = 0; i < keyCombo.Items.Count; i++)
            if (keyCombo.Items[i] is ComboBoxItem it && (uint)it.Tag == current.VirtualKey) { keyCombo.SelectedIndex = i; break; }
        if (keyCombo.SelectedIndex < 0) keyCombo.SelectedIndex = 0;

        var modRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        modRow.Children.Add(ctrl); modRow.Children.Add(alt); modRow.Children.Add(shift); modRow.Children.Add(win);

        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = P("Pick the modifier keys and a key for this hotkey.", "揀呢個熱鍵嘅修飾鍵同按鍵。"),
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(modRow);
        panel.Children.Add(keyCombo);

        var dlg = new ContentDialog
        {
            Title = mode switch
            {
                CropLockMode.Thumbnail => P("Thumbnail hotkey", "縮圖熱鍵"),
                CropLockMode.Reparent => P("Reparent / crop hotkey", "重新指定父視窗／裁切熱鍵"),
                _ => P("Screenshot hotkey", "截圖熱鍵"),
            },
            Content = panel,
            PrimaryButtonText = P("Save", "儲存"),
            SecondaryButtonText = P("Clear", "清除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };

        var res = await dlg.ShowAsync();
        if (res == ContentDialogResult.None) return; // cancel

        if (res == ContentDialogResult.Secondary)
        {
            CropAndLockService.SetHotkey(mode, new CropAndLockService.Chord());
            Info(P("Cleared", "已清除"), P("Hotkey removed.", "已移除熱鍵。"));
            return;
        }

        uint mods = 0;
        if (ctrl.IsChecked == true) mods |= 0x0002;
        if (alt.IsChecked == true) mods |= 0x0001;
        if (shift.IsChecked == true) mods |= 0x0004;
        if (win.IsChecked == true) mods |= 0x0008;

        var selected = keyCombo.SelectedItem as ComboBoxItem;
        uint vkey = selected?.Tag is uint t ? t : 0;
        string keyName = selected?.Content?.ToString() ?? "";

        if (mods == 0)
        {
            Warn(P("Pick at least one modifier (Ctrl/Alt/Shift/Win).", "至少揀一個修飾鍵（Ctrl／Alt／Shift／Win）。"));
            return;
        }

        CropAndLockService.SetHotkey(mode, new CropAndLockService.Chord { Modifiers = mods, VirtualKey = vkey, KeyName = keyName });
        if (!CropAndLockService.IsRunning && CropAndLockService.Enabled) CropAndLockService.EnsureStarted();
        Info(P("Saved", "已儲存"), P("Hotkey updated.", "已更新熱鍵。"));
    }

    // ===================== window list =====================

    private void Refresh_Click(object sender, RoutedEventArgs e) => ReloadWindows();

    private void ReloadWindows()
    {
        var wins = WindowManager.List();
        WinList.ItemsSource = wins;
        CountText.Text = P($"{wins.Count} windows", $"{wins.Count} 個視窗");
    }

    private bool SelectedWindow(out WinInfo? info)
    {
        info = WinList.SelectedItem as WinInfo;
        if (info is null)
        {
            Warn(P("Select a window on the left first.", "請先喺左邊揀一個視窗。"));
            return false;
        }
        return true;
    }

    // ===================== pick + spawn =====================

    private void Thumb_Click(object sender, RoutedEventArgs e) => StartPick(CropLockMode.Thumbnail);
    private void Crop_Click(object sender, RoutedEventArgs e) => StartPick(CropLockMode.Reparent);
    private void Screenshot_Click(object sender, RoutedEventArgs e) => StartPick(CropLockMode.Screenshot);

    private void StartPick(CropLockMode mode)
    {
        if (!SelectedWindow(out var info) || info is null) return;
        PickRegionAndSpawn(info.Handle, info.Title, mode);
    }

    /// <summary>由熱鍵觸發：用前景視窗（或揀清單第一個）開始流程 · Hotkey flow — use the foreground/selected window.</summary>
    private void OnHotkeyPick(CropLockMode mode)
    {
        // Prefer the currently selected window; otherwise refresh and use the first.
        var info = WinList.SelectedItem as WinInfo;
        if (info is null)
        {
            ReloadWindows();
            info = (WinList.ItemsSource as System.Collections.Generic.List<WinInfo>)?.FirstOrDefault();
        }
        if (info is null)
        {
            Warn(P("No window to crop. Open one and try again.", "冇視窗可裁切。開一個再試。"));
            return;
        }
        PickRegionAndSpawn(info.Handle, info.Title, mode);
    }

    private async void PickRegionAndSpawn(IntPtr source, string title, CropLockMode mode)
    {
        // Bring the target window forward so the user can see what they're cropping, then drag a region.
        try { WindowManager.Focus(source); } catch { }

        var rect = RegionSelector.PickRegion();
        if (rect is null) { return; } // cancelled (Esc / right-click)

        // Await the async create so the UI thread never blocks waiting for the STA pump thread.
        var ok = await CropAndLockService.CreateFromScreenRectAsync(source, title, rect.Value, mode);
        if (ok)
            Info(mode switch
                 {
                     CropLockMode.Thumbnail => P("Thumbnail created", "已建立縮圖"),
                     CropLockMode.Reparent => P("Cropped view created", "已建立裁切檢視"),
                     _ => P("Screenshot created", "已建立截圖"),
                 },
                 CropAndLockService.LastEvent);
        else
            Warn(CropAndLockService.LastEvent);
    }

    // ===================== active list =====================

    private void RefreshActive()
    {
        var items = CropAndLockService.Active.ToList();
        ActiveList.ItemsSource = items;
        EmptyActive.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CloseAllBtn.IsEnabled = items.Count > 0;
    }

    private void CloseOne_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is CropLockEntry entry)
            CropAndLockService.Close(entry);
    }

    private void CloseAll_Click(object sender, RoutedEventArgs e) => CropAndLockService.CloseAll();

    // ===================== feedback =====================

    private void Info(string title, string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = title;
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
