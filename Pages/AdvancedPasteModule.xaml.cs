using System;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 進階貼上（PowerToys Advanced Paste 式）控制頁 · Control page for the Advanced Paste module.
///
/// 開關全域熱鍵（預設 Win+Shift+V）、揀預設動作、勾選啟用邊啲轉換、即場對目前剪貼簿試跑任何動作並預覽，
/// 以及顯示 AI 供應商狀態。所有面板背後嘅轉換邏輯喺 AdvancedPasteService。雙語。
///
/// Toggles the global hotkey, picks the default action, enables/disables individual transforms, runs any
/// action against the current clipboard with a live preview, and surfaces AI-provider status. All logic
/// lives in AdvancedPasteService. Bilingual.
/// </summary>
public sealed partial class AdvancedPasteModule : Page
{
    private bool _suppress;
    private string _lastPreview = "";
    private CancellationTokenSource? _cts;

    public AdvancedPasteModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); BuildLists(); };
        Loaded += (_, _) => { Render(); BuildLists(); SyncFromState(); };
    }

    private static string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Advanced Paste · 進階貼上";
        HeaderBlurb.Text = P(
            "Transform whatever you copied and paste it in a different format. Press the global hotkey anywhere to open a palette near the cursor, pick a transform, and it pastes straight into the active app — or run any transform here on the current clipboard.",
            "把你複製到嘅嘢即時轉換成另一種格式再貼上。喺任何地方撳全域熱鍵，喺滑鼠附近彈出面板，揀一個轉換，就會直接貼落作用中嘅 app — 又或者喺呢度對目前剪貼簿試跑任何轉換。");

        HotkeyTitle.Text = P("Global hotkey", "全域熱鍵");
        HotkeyLabel.Text = P("Open palette with", "用咩開面板");
        DefaultLabel.Text = P("Default action (top of palette)", "預設動作（面板最頂）");

        TryTitle.Text = P("Try it on the current clipboard", "即場試跑（目前剪貼簿）");
        TryBlurb.Text = P("Pick an action and preview the result without pasting. Copy the result to the clipboard if you like it.",
            "揀一個動作，預覽結果而唔貼上。鍾意就可以複製結果落剪貼簿。");
        PreviewBtn.Content = P("Preview", "預覽");
        CopyResultBtn.Content = P("Copy result", "複製結果");
        AiInstruction.PlaceholderText = P("Describe how the AI should transform the clipboard…", "描述 AI 應該點轉換剪貼簿…");

        ActionsTitle.Text = P("Enabled actions", "啟用嘅動作");
        ActionsBlurb.Text = P("Untick an action to hide it from the palette.", "取消勾選即可喺面板隱藏該動作。");

        UpdateHotkeyStatus();
        UpdateAiBar();
    }

    private void BuildLists()
    {
        _suppress = true;

        // Hotkey combo (a few sensible global combinations).
        HotkeyCombo.Items.Clear();
        HotkeyCombo.Items.Add("Win + Shift + V");
        HotkeyCombo.SelectedIndex = 0;
        HotkeyCombo.IsEnabled = false; // single supported combo for now (see note)
        ToolTipService.SetToolTip(HotkeyCombo,
            P("The palette hotkey is fixed to Win+Shift+V in this build.", "此版本面板熱鍵固定為 Win+Shift+V。"));

        // Default-action combo (enabled non-AI actions).
        DefaultCombo.Items.Clear();
        var defaultable = AdvancedPasteService.All.Where(a => !a.RequiresAi).ToList();
        foreach (var a in defaultable)
            DefaultCombo.Items.Add(new ComboBoxItem { Content = a.Name.Display, Tag = a.Id });
        var curDef = AdvancedPasteService.DefaultActionId;
        DefaultCombo.SelectedIndex = Math.Max(0, defaultable.FindIndex(a => a.Id == curDef));

        // Try combo (all actions).
        TryCombo.Items.Clear();
        foreach (var a in AdvancedPasteService.All)
            TryCombo.Items.Add(new ComboBoxItem { Content = a.Name.Display, Tag = a.Id });
        if (TryCombo.SelectedIndex < 0) TryCombo.SelectedIndex = 0;
        TryCombo.SelectionChanged -= Try_Changed;
        TryCombo.SelectionChanged += Try_Changed;
        UpdateAiInstructionVisibility();

        // Enabled-actions checklist.
        ActionsList.Children.Clear();
        foreach (var a in AdvancedPasteService.All)
        {
            var local = a;
            var cb = new CheckBox
            {
                Content = a.Name.Display + (a.RequiresAi ? "  (AI)" : ""),
                IsChecked = AdvancedPasteService.IsActionEnabled(a.Id),
                IsEnabled = !a.RequiresAi || AdvancedPasteService.AiAvailable,
            };
            cb.Checked += (_, _) => { if (!_suppress) AdvancedPasteService.SetActionEnabled(local.Id, true); };
            cb.Unchecked += (_, _) => { if (!_suppress) AdvancedPasteService.SetActionEnabled(local.Id, false); };
            ActionsList.Children.Add(cb);
        }

        _suppress = false;
    }

    private void SyncFromState()
    {
        _suppress = true;
        HotkeySwitch.IsOn = AdvancedPasteService.HotkeyActive;
        _suppress = false;
        UpdateHotkeyStatus();
    }

    private void UpdateHotkeyStatus()
    {
        HotkeyStatus.Text = AdvancedPasteService.HotkeyActive
            ? P($"On — press {AdvancedPasteService.HotkeyText} anywhere to open the palette.", $"已開 — 喺任何地方撳 {AdvancedPasteService.HotkeyText} 開面板。")
            : P("Off — turn on to enable the palette hotkey.", "已關 — 開啟即可使用面板熱鍵。");
    }

    private void UpdateAiBar()
    {
        if (AdvancedPasteService.AiAvailable)
        {
            var p = AdvancedPasteService.ResolveAiProvider();
            AiBar.Severity = InfoBarSeverity.Success;
            AiBar.Title = P("AI transforms available", "AI 轉換可用");
            AiBar.Message = P($"Using provider \"{p?.Name}\". Configure providers in AI Chat. The \"Paste with AI\" box takes a free-text instruction.",
                $"使用供應商「{p?.Name}」。喺 AI Chat 設定供應商。「用 AI 貼上」框接受自由文字指示。");
        }
        else
        {
            AiBar.Severity = InfoBarSeverity.Informational;
            AiBar.Title = P("AI transforms disabled", "AI 轉換已停用");
            AiBar.Message = P("No AI provider configured. Add an Ollama or OpenAI-compatible provider in the AI Chat module to enable \"Paste with AI\".",
                "未設定 AI 供應商。喺 AI Chat 模組加入 Ollama 或 OpenAI 相容供應商，即可使用「用 AI 貼上」。");
        }
    }

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }

    // ===================== Handlers =====================

    private void Hotkey_Toggled(object sender, RoutedEventArgs e)
    {
        if (_suppress) return;
        if (HotkeySwitch.IsOn)
        {
            AdvancedPasteService.EnableHotkey(DispatcherQueue);
            AdvancedPasteService.HotkeyEnabledSetting = true;
            Info(InfoBarSeverity.Success, P("Hotkey on", "熱鍵已開"),
                P($"Press {AdvancedPasteService.HotkeyText} anywhere to open the Advanced Paste palette.",
                  $"喺任何地方撳 {AdvancedPasteService.HotkeyText} 開進階貼上面板。"));
        }
        else
        {
            AdvancedPasteService.DisableHotkey();
            AdvancedPasteService.HotkeyEnabledSetting = false;
            Info(InfoBarSeverity.Informational, P("Hotkey off", "熱鍵已關"),
                P("The palette hotkey is disabled.", "面板熱鍵已停用。"));
        }
        UpdateHotkeyStatus();
    }

    private void Hotkey_ComboChanged(object sender, SelectionChangedEventArgs e) { /* fixed combo for now */ }

    private void Default_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (DefaultCombo.SelectedItem is ComboBoxItem item && item.Tag is string id)
            AdvancedPasteService.DefaultActionId = id;
    }

    private void Try_Changed(object sender, SelectionChangedEventArgs e) => UpdateAiInstructionVisibility();

    private void UpdateAiInstructionVisibility()
    {
        var id = (TryCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var action = id is null ? null : AdvancedPasteService.Find(id);
        AiInstruction.Visibility = (action?.RequiresAi == true) ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Preview_Click(object sender, RoutedEventArgs e)
    {
        var id = (TryCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var action = id is null ? null : AdvancedPasteService.Find(id);
        if (action is null) return;

        if (action.RequiresAi && !AdvancedPasteService.AiAvailable)
        {
            Info(InfoBarSeverity.Warning, P("No AI provider", "冇 AI 供應商"),
                P("Configure an AI provider in AI Chat first.", "請先喺 AI Chat 設定 AI 供應商。"));
            return;
        }

        PreviewBtn.IsEnabled = false;
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            string instruction = action.RequiresAi ? AiInstruction.Text.Trim() : "";
            _lastPreview = await AdvancedPasteService.RunAsync(action, instruction, _cts.Token);
            PreviewBox.Text = _lastPreview;
            PreviewBox.Visibility = Visibility.Visible;
            if (string.IsNullOrEmpty(_lastPreview))
                Info(InfoBarSeverity.Informational, P("Empty result", "結果係空"),
                    P("The transform produced no text (clipboard empty or not applicable).", "轉換冇產生文字（剪貼簿空或者唔適用）。"));
            else
                ResultBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Transform failed", "轉換失敗"), ex.Message);
        }
        finally { PreviewBtn.IsEnabled = true; }
    }

    private void CopyResult_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastPreview))
        {
            Info(InfoBarSeverity.Informational, P("Nothing to copy", "冇嘢可複製"),
                P("Run a preview first.", "請先預覽。"));
            return;
        }
        AdvancedPasteService.SetClipboardText(_lastPreview);
        Info(InfoBarSeverity.Success, P("Copied", "已複製"),
            P("The result is on the clipboard.", "結果已放上剪貼簿。"));
    }
}
