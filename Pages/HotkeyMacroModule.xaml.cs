using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 全域熱鍵 + 巨集執行器 + 文字展開 · Global hotkey + macro runner + text expander.
/// Register chords (Ctrl/Alt/Shift/Win + key) that launch an app, run a PowerShell snippet, or replay
/// text via SendInput; plus a typed-trigger text expander (low-level keyboard hook). In-app, bilingual,
/// no redirect. All bindings + snippets persist via SettingsStore and keep working in the tray.
/// </summary>
public sealed partial class HotkeyMacroModule : Page
{
    public HotkeyMacroModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        HotkeyMacroService.Fired += OnFired;
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            HotkeyMacroService.Fired -= OnFired;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        HotkeyMacroService.Load();
        HotkeyMacroService.StartHotkeys();
        FillKeyBox();
        FillActionBox();
        Render();
        BindLists();
        ExpanderSwitch.IsOn = HotkeyMacroService.ExpanderEnabled;
        UpdateExpanderStatus();
    }

    private void OnLang(object? s, EventArgs e)
    {
        FillActionBox();
        Render();
        BindLists();
        UpdateExpanderStatus();
    }

    private void OnFired()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Triggered", "已觸發");
            ResultBar.Message = HotkeyMacroService.LastEvent;
            ResultBar.IsOpen = true;
        });
    }

    private void Render()
    {
        Header.Title = "Hotkey & Macro Runner · 熱鍵與巨集";
        HeaderBlurb.Text = P(
            "Register global keyboard chords that launch an app, run a PowerShell snippet, or type text. Plus a text expander that turns typed triggers into snippets. Everything runs in-app and keeps working in the tray.",
            "登記全域鍵盤組合鍵，用嚟開程式、執行 PowerShell 片段，或者自動打字。仲有文字展開：將你打嘅縮寫變成片語。全部喺 app 內運行，收入系統匣都繼續運作。");

        HotkeysHeader.Text = P("Global hotkeys · 全域熱鍵", "全域熱鍵 · Global hotkeys");
        ChordLabel.Text = P("Chord", "組合鍵");
        ActionLabel.Text = P("Action", "動作");
        NameBox.PlaceholderText = P("Name (optional)", "名稱（可選）");
        TargetBox.PlaceholderText = P("Program / file / URL", "程式／檔案／網址");
        ArgsBox.PlaceholderText = P("Arguments (optional)", "參數（可選）");
        BrowseBtn.Content = P("Browse…", "瀏覽…");
        ScriptBox.PlaceholderText = P("PowerShell command(s)", "PowerShell 指令");
        KeysBox.PlaceholderText = P("Text to type (replayed via SendInput)", "要打嘅文字（用 SendInput 重播）");
        AddBindingBtn.Content = P("Add hotkey", "加入熱鍵");

        SnippetsHeader.Text = P("Text expander · 文字展開", "文字展開 · Text expander");
        ExpanderTitle.Text = P("Expand typed triggers", "展開打字縮寫");
        TriggerLabel.Text = P("Trigger", "縮寫");
        TriggerBox.PlaceholderText = P("e.g. ;addr", "例如 ;addr");
        ExpansionBox.PlaceholderText = P("Replacement text", "展開後嘅文字");
        AddSnippetBtn.Content = P("Add snippet", "加入片語");

        NoBindings.Text = P("No hotkeys yet. Pick a chord and an action above, then Add hotkey.",
            "未有熱鍵。喺上面揀組合鍵同動作，再撳「加入熱鍵」。");
        NoSnippets.Text = P("No snippets yet. Add a trigger and its replacement text above.",
            "未有片語。喺上面加縮寫同展開文字。");
    }

    private void FillKeyBox()
    {
        KeyBox.Items.Clear();
        foreach (var (name, vk) in HotkeyMacroService.PickableKeys)
            KeyBox.Items.Add(new ComboBoxItem { Content = name, Tag = vk });
        if (KeyBox.Items.Count > 0) KeyBox.SelectedIndex = 0;
    }

    private void FillActionBox()
    {
        int sel = ActionBox.SelectedIndex;
        ActionBox.Items.Clear();
        ActionBox.Items.Add(new ComboBoxItem { Content = P("Launch an app / file / URL", "開啟程式／檔案／網址"), Tag = MacroActionKind.LaunchApp });
        ActionBox.Items.Add(new ComboBoxItem { Content = P("Run a PowerShell snippet", "執行 PowerShell 片段"), Tag = MacroActionKind.RunPowerShell });
        ActionBox.Items.Add(new ComboBoxItem { Content = P("Type text (SendInput)", "自動打字（SendInput）"), Tag = MacroActionKind.SendKeys });
        ActionBox.SelectedIndex = sel >= 0 ? sel : 0;
    }

    private void ActionBox_Changed(object sender, SelectionChangedEventArgs e)
    {
        var kind = (ActionBox.SelectedItem as ComboBoxItem)?.Tag as MacroActionKind? ?? MacroActionKind.LaunchApp;
        LaunchPanel.Visibility = kind == MacroActionKind.LaunchApp ? Visibility.Visible : Visibility.Collapsed;
        ScriptBox.Visibility = kind == MacroActionKind.RunPowerShell ? Visibility.Visible : Visibility.Collapsed;
        KeysBox.Visibility = kind == MacroActionKind.SendKeys ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BindLists()
    {
        BindingsList.ItemsSource = null;
        BindingsList.ItemsSource = HotkeyMacroService.Bindings;
        NoBindings.Visibility = HotkeyMacroService.Bindings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BindingsList.Visibility = HotkeyMacroService.Bindings.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        SnippetsList.ItemsSource = null;
        SnippetsList.ItemsSource = HotkeyMacroService.Snippets;
        NoSnippets.Visibility = HotkeyMacroService.Snippets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SnippetsList.Visibility = HotkeyMacroService.Snippets.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        BindingsList.ContainerContentChanging -= Bindings_ContainerContentChanging;
        BindingsList.ContainerContentChanging += Bindings_ContainerContentChanging;
    }

    // Populate the computed chord/action cells (can't bind to a method directly).
    private void Bindings_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.Item is not HotkeyBinding b || args.ItemContainer?.ContentTemplateRoot is not FrameworkElement root) return;
        var chord = FindChild(root, "ChordCell") as TextBlock;
        var action = FindChild(root, "ActionCell") as TextBlock;
        if (chord is not null) chord.Text = b.ChordText();
        if (action is not null) action.Text = b.ActionSummary();
    }

    private static DependencyObject? FindChild(DependencyObject parent, string name)
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement fe && fe.Name == name) return child;
            var deeper = FindChild(child, name);
            if (deeper is not null) return deeper;
        }
        return null;
    }

    // ===================== hotkeys =====================

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // Win32 COM picker (FileDialogs) instead of the WinRT FileOpenPicker, which fails silently
            // when the app runs elevated. "*" all-files filter is omitted — FileDialogs always adds All files.
            var path = await FileDialogs.OpenFileAsync();
            if (path is not null) TargetBox.Text = path;
        }
        catch (Exception ex) { Fail(ex.Message); }
    }

    private void AddBinding_Click(object sender, RoutedEventArgs e)
    {
        if ((KeyBox.SelectedItem as ComboBoxItem)?.Tag is not uint vk)
        {
            Warn(P("Pick a key for the chord.", "請揀組合鍵嘅按鍵。"));
            return;
        }
        uint mods = 0;
        if (CtrlChk.IsChecked == true) mods |= (uint)HotMod.Control;
        if (AltChk.IsChecked == true) mods |= (uint)HotMod.Alt;
        if (ShiftChk.IsChecked == true) mods |= (uint)HotMod.Shift;
        if (WinChk.IsChecked == true) mods |= (uint)HotMod.Win;
        if (mods == 0)
        {
            Warn(P("Pick at least one modifier (Ctrl/Alt/Shift/Win).", "至少揀一個修飾鍵（Ctrl／Alt／Shift／Win）。"));
            return;
        }

        var kind = (ActionBox.SelectedItem as ComboBoxItem)?.Tag as MacroActionKind? ?? MacroActionKind.LaunchApp;
        var keyName = (KeyBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

        var binding = new HotkeyBinding
        {
            Modifiers = mods,
            VirtualKey = vk,
            KeyName = keyName,
            Action = kind,
            Name = NameBox.Text?.Trim() ?? "",
            Target = TargetBox.Text?.Trim() ?? "",
            Arguments = ArgsBox.Text?.Trim() ?? "",
            Script = ScriptBox.Text ?? "",
            Keys = KeysBox.Text ?? "",
        };

        // basic validation per action
        if (kind == MacroActionKind.LaunchApp && string.IsNullOrWhiteSpace(binding.Target))
        { Warn(P("Enter a program, file or URL to launch.", "請輸入要開嘅程式、檔案或網址。")); return; }
        if (kind == MacroActionKind.RunPowerShell && string.IsNullOrWhiteSpace(binding.Script))
        { Warn(P("Enter a PowerShell command.", "請輸入 PowerShell 指令。")); return; }
        if (kind == MacroActionKind.SendKeys && string.IsNullOrEmpty(binding.Keys))
        { Warn(P("Enter the text to type.", "請輸入要打嘅文字。")); return; }

        if (HotkeyMacroService.Bindings.Any(x => x.Modifiers == mods && x.VirtualKey == vk))
        { Warn(P("That chord is already used.", "呢個組合鍵已經用咗。")); return; }

        HotkeyMacroService.AddBinding(binding);
        BindLists();

        // reset inputs
        NameBox.Text = ""; TargetBox.Text = ""; ArgsBox.Text = ""; ScriptBox.Text = ""; KeysBox.Text = "";
        Info(P("Added", "已加入"), P($"Hotkey {binding.ChordText()} registered.", $"已登記熱鍵 {binding.ChordText()}。"));
    }

    private void RemoveBinding_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HotkeyBinding b)
        {
            HotkeyMacroService.RemoveBinding(b);
            BindLists();
        }
    }

    private void BindingToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is HotkeyBinding)
            HotkeyMacroService.UpdateBinding(); // persist + re-register
    }

    // ===================== text expander =====================

    private void Expander_Toggled(object sender, RoutedEventArgs e)
    {
        if (ExpanderSwitch.IsOn) HotkeyMacroService.StartExpander();
        else HotkeyMacroService.StopExpander();
        UpdateExpanderStatus();
    }

    private void UpdateExpanderStatus()
    {
        ExpanderStatus.Text = HotkeyMacroService.ExpanderEnabled
            ? P("On — typing a trigger replaces it with its snippet anywhere in Windows.",
                "已開 — 喺 Windows 任何地方打縮寫都會自動換成片語。")
            : P("Off — install a low-level keyboard hook to watch for triggers.",
                "已關 — 開咗會裝低階鍵盤掛鈎監察縮寫。");
    }

    private void AddSnippet_Click(object sender, RoutedEventArgs e)
    {
        var trig = TriggerBox.Text?.Trim() ?? "";
        var exp = ExpansionBox.Text ?? "";
        if (string.IsNullOrEmpty(trig)) { Warn(P("Enter a trigger.", "請輸入縮寫。")); return; }
        if (string.IsNullOrEmpty(exp)) { Warn(P("Enter the replacement text.", "請輸入展開文字。")); return; }
        if (HotkeyMacroService.Snippets.Any(s => s.Trigger == trig)) { Warn(P("That trigger already exists.", "呢個縮寫已經存在。")); return; }

        HotkeyMacroService.AddSnippet(new Snippet { Trigger = trig, Expansion = exp });
        BindLists();
        TriggerBox.Text = ""; ExpansionBox.Text = "";
        Info(P("Added", "已加入"), P($"Snippet {trig} added.", $"已加入片語 {trig}。"));
    }

    private void RemoveSnippet_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Snippet s)
        {
            HotkeyMacroService.RemoveSnippet(s);
            BindLists();
        }
    }

    private void SnippetToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is Snippet)
            HotkeyMacroService.UpdateSnippet();
    }

    // ===================== InfoBar helpers =====================

    private void Info(string title, string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = title; ResultBar.Message = msg; ResultBar.IsOpen = true;
    }

    private void Warn(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Heads up", "注意"); ResultBar.Message = msg; ResultBar.IsOpen = true;
    }

    private void Fail(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗"); ResultBar.Message = msg; ResultBar.IsOpen = true;
    }
}
