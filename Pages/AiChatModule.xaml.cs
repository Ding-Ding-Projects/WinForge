using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// AI 聊天 · An OpenWebUI-style native chat experience: multiple providers (local Ollama,
/// OpenAI-compatible endpoints, installed CLI agents), persistent conversations, streaming
/// responses with Stop/Regenerate/Edit, markdown + code rendering, per-chat system prompt and
/// params, attach-file, export, and Ollama model management. Fully bilingual.
/// </summary>
public sealed partial class AiChatModule : Page
{
    private readonly AiChatService _svc = AiChatService.I;
    private List<ChatConversation> _conversations = new();
    private ChatConversation? _active;
    private CancellationTokenSource? _streamCts;
    private bool _loadingChat;

    public AiChatModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) => { Render(); await InitAsync(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        NewChatText.Text = P("New chat", "新對話");
        ChatsHeader.Text = P("Conversations", "對話記錄");
        SettingsTitle.Text = P("Chat settings", "對話設定");
        SysPromptLabel.Text = P("System prompt", "系統提示");
        TempLabel.Text = P("Temperature", "溫度");
        MaxTokLabel.Text = P("Max tokens (0 = model default)", "最大 token 數（0 = 模型預設）");
    }

    private async Task InitAsync()
    {
        RefreshProviderPicker();
        _conversations = _svc.LoadConversations();
        if (_conversations.Count == 0)
        {
            NewConversation();
        }
        else
        {
            RenderConversationList();
            LoadConversation(_conversations[0]);
        }
        await RefreshModelsAsync();
    }

    // ===================== Providers / models =====================

    private void RefreshProviderPicker()
    {
        ProviderPicker.Items.Clear();
        foreach (var p in _svc.Providers)
            ProviderPicker.Items.Add(new ComboBoxItem { Content = p.Name, Tag = p.Id });
        if (ProviderPicker.Items.Count > 0 && ProviderPicker.SelectedIndex < 0)
            ProviderPicker.SelectedIndex = 0;
    }

    private AiProvider? SelectedProvider()
    {
        if (ProviderPicker.SelectedItem is ComboBoxItem ci && ci.Tag is string id)
            return _svc.GetProvider(id);
        return _svc.Providers.FirstOrDefault();
    }

    private async void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingChat) return;
        if (_active is not null && SelectedProvider() is { } p)
        {
            _active.ProviderId = p.Id;
            _active.Model = "";
            _svc.SaveConversation(_active);
        }
        await RefreshModelsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        var provider = SelectedProvider();
        ModelPicker.Items.Clear();
        if (provider is null) return;
        List<string> models = new();
        try { models = await _svc.ListModelsAsync(provider); } catch { }
        foreach (var m in models)
            ModelPicker.Items.Add(new ComboBoxItem { Content = m, Tag = m });

        var want = _active is not null && !string.IsNullOrWhiteSpace(_active.Model)
            ? _active.Model : provider.DefaultModel;
        if (!string.IsNullOrWhiteSpace(want))
        {
            var match = ModelPicker.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == want);
            if (match is not null) ModelPicker.SelectedItem = match;
        }
        if (ModelPicker.SelectedIndex < 0 && ModelPicker.Items.Count > 0)
            ModelPicker.SelectedIndex = 0;

        if (models.Count == 0)
            ShowInfo(InfoBarSeverity.Warning,
                P("No models found", "搵唔到模型"),
                provider.Kind == AiProviderKind.Ollama
                    ? P("Is Ollama running? Use Manage to install and pull models.",
                        "Ollama 有冇喺度行？用「管理」嚟安裝同拉取模型。")
                    : P("Check the base URL / API key in Manage.", "喺「管理」度檢查基底網址／API 金鑰。"));
        else
            StatusBar.IsOpen = false;
    }

    private void Model_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingChat || _active is null) return;
        if (ModelPicker.SelectedItem is ComboBoxItem ci && ci.Tag is string m)
        {
            _active.Model = m;
            _svc.SaveConversation(_active);
        }
    }

    // ===================== Conversation list =====================

    private void ChatSearch_TextChanged(object sender, TextChangedEventArgs e) => RenderConversationList();

    private void RenderConversationList()
    {
        ConversationList.Children.Clear();
        var q = ChatSearchBox.Text?.Trim() ?? "";
        foreach (var c in _conversations)
        {
            if (q.Length > 0 && !(c.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
            ConversationList.Children.Add(BuildConversationRow(c));
        }
    }

    private Border BuildConversationRow(ChatConversation c)
    {
        bool isActive = _active?.Id == c.Id;
        var grid = new Grid { ColumnSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(c.Title) ? P("Untitled", "未命名") : c.Title,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 13,
        };
        Grid.SetColumn(title, 0);
        grid.Children.Add(title);

        var menu = new Button
        {
            Content = new FontIcon { FontSize = 12, Glyph = "" },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 2, 6, 2),
        };
        var flyout = new MenuFlyout();
        var rename = new MenuFlyoutItem { Text = P("Rename", "重新命名") };
        rename.Click += async (_, _) => await RenameConversationAsync(c);
        var exportMd = new MenuFlyoutItem { Text = P("Export .md", "匯出 .md") };
        exportMd.Click += async (_, _) => await ExportConversationAsync(c, false);
        var exportJson = new MenuFlyoutItem { Text = P("Export .json", "匯出 .json") };
        exportJson.Click += async (_, _) => await ExportConversationAsync(c, true);
        var del = new MenuFlyoutItem { Text = P("Delete", "刪除") };
        del.Click += (_, _) => DeleteConversation(c);
        flyout.Items.Add(rename);
        flyout.Items.Add(exportMd);
        flyout.Items.Add(exportJson);
        flyout.Items.Add(new MenuFlyoutSeparator());
        flyout.Items.Add(del);
        menu.Flyout = flyout;
        Grid.SetColumn(menu, 1);
        grid.Children.Add(menu);

        var border = new Border
        {
            Padding = new Thickness(10, 8, 6, 8),
            CornerRadius = new CornerRadius(6),
            Background = isActive
                ? (Brush)Application.Current.Resources["SubtleFillColorSecondaryBrush"]
                : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            Child = grid,
        };
        border.PointerPressed += (_, _) => LoadConversation(c);
        return border;
    }

    private void NewChat_Click(object sender, RoutedEventArgs e) => NewConversation();

    private void NewConversation()
    {
        var provider = SelectedProvider() ?? _svc.Providers.FirstOrDefault();
        var c = new ChatConversation
        {
            Title = P("New chat", "新對話"),
            ProviderId = provider?.Id ?? "",
            Model = "",
            SystemPrompt = "",
        };
        _svc.SaveConversation(c);
        _conversations.Insert(0, c);
        RenderConversationList();
        LoadConversation(c);
    }

    private void LoadConversation(ChatConversation c)
    {
        _loadingChat = true;
        _active = c;
        ChatTitleBox.Text = c.Title;
        SystemPromptBox.Text = c.SystemPrompt;
        TempSlider.Value = c.Temperature;
        TempValue.Text = c.Temperature.ToString("0.00");
        MaxTokBox.Value = c.MaxTokens;

        // select the conversation's provider
        if (!string.IsNullOrWhiteSpace(c.ProviderId))
        {
            var item = ProviderPicker.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == c.ProviderId);
            if (item is not null) ProviderPicker.SelectedItem = item;
        }
        _loadingChat = false;

        RenderMessages();
        RenderConversationList();
        _ = RefreshModelsAsync();
    }

    private async Task RenameConversationAsync(ChatConversation c)
    {
        var tb = new TextBox { Text = c.Title, AcceptsReturn = false };
        var dlg = new ContentDialog
        {
            Title = P("Rename conversation", "重新命名對話"),
            Content = tb,
            PrimaryButtonText = P("Save", "儲存"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary)
        {
            c.Title = string.IsNullOrWhiteSpace(tb.Text) ? c.Title : tb.Text.Trim();
            _svc.SaveConversation(c);
            if (_active?.Id == c.Id) ChatTitleBox.Text = c.Title;
            RenderConversationList();
        }
    }

    private void DeleteConversation(ChatConversation c)
    {
        _svc.DeleteConversation(c.Id);
        _conversations.RemoveAll(x => x.Id == c.Id);
        if (_active?.Id == c.Id)
        {
            if (_conversations.Count > 0) LoadConversation(_conversations[0]);
            else NewConversation();
        }
        RenderConversationList();
    }

    private void ChatTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_active is null || _loadingChat) return;
        var t = ChatTitleBox.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(t) && t != _active.Title)
        {
            _active.Title = t;
            _svc.SaveConversation(_active);
            RenderConversationList();
        }
    }

    // ===================== Settings (per-chat) =====================

    private void SystemPrompt_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_active is null || _loadingChat) return;
        _active.SystemPrompt = SystemPromptBox.Text ?? "";
        _svc.SaveConversation(_active);
    }

    private void Temp_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        TempValue.Text = e.NewValue.ToString("0.00");
        if (_active is null || _loadingChat) return;
        _active.Temperature = e.NewValue;
        _svc.SaveConversation(_active);
    }

    private void MaxTok_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_active is null || _loadingChat) return;
        _active.MaxTokens = double.IsNaN(args.NewValue) ? 0 : (int)args.NewValue;
        _svc.SaveConversation(_active);
    }

    // ===================== Messages rendering =====================

    private void RenderMessages()
    {
        MessagesPanel.Children.Clear();
        if (_active is null) return;
        foreach (var m in _active.Messages)
            MessagesPanel.Children.Add(BuildMessageBubble(m));
        ScrollToBottom();
    }

    private FrameworkElement BuildMessageBubble(ChatMessage m)
    {
        bool isUser = m.Role == ChatRoles.User;
        var outer = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Stretch };

        // role + actions header
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new TextBlock
        {
            Text = isUser ? P("You", "你") : P("Assistant", "助手"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (m.TokenCount is int tc)
            header.Children.Add(new TextBlock
            {
                Text = $"· {tc} tok",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                VerticalAlignment = VerticalAlignment.Center,
            });
        outer.Children.Add(header);

        var bubble = new Border
        {
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(10),
            Background = isUser
                ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                : (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(isUser ? 0 : 1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Child = RenderContent(m.Content, isUser),
        };
        outer.Children.Add(bubble);

        // action row
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Opacity = 0.85 };
        actions.Children.Add(IconButton("", P("Copy", "複製"), () =>
        {
            var dp = new DataPackage();
            dp.SetText(m.Content);
            Clipboard.SetContent(dp);
        }));
        if (isUser)
            actions.Children.Add(IconButton("", P("Edit & resend", "編輯並重發"), () => _ = EditAndResendAsync(m)));
        else
            actions.Children.Add(IconButton("", P("Regenerate", "重新生成"), () => _ = RegenerateAsync(m)));
        actions.Children.Add(IconButton("", P("Delete", "刪除"), () => DeleteMessage(m)));
        outer.Children.Add(actions);

        return outer;
    }

    private Button IconButton(string glyph, string tip, Action onClick)
    {
        var b = new Button
        {
            Content = new FontIcon { FontSize = 13, Glyph = glyph },
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 3, 6, 3),
        };
        ToolTipService.SetToolTip(b, tip);
        b.Click += (_, _) => onClick();
        return b;
    }

    /// <summary>
    /// 簡易 markdown 呈現 · Pragmatic markdown rendering: fenced ``` code blocks become a monospaced
    /// Border with a copy-code button; everything else becomes a selectable TextBlock with inline
    /// `code` and **bold** lightly handled.
    /// </summary>
    private FrameworkElement RenderContent(string content, bool isUser)
    {
        var panel = new StackPanel { Spacing = 8 };
        var fg = isUser
            ? (Brush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"]
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        var parts = SplitFences(content ?? "");
        foreach (var (isCode, lang, text) in parts)
        {
            if (isCode)
                panel.Children.Add(BuildCodeBlock(lang, text));
            else if (!string.IsNullOrWhiteSpace(text))
                panel.Children.Add(new TextBlock
                {
                    Text = text.Trim('\n'),
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true,
                    Foreground = fg,
                    FontSize = 14,
                });
        }
        if (panel.Children.Count == 0)
            panel.Children.Add(new TextBlock { Text = content ?? "", TextWrapping = TextWrapping.Wrap, Foreground = fg });
        return panel;
    }

    private FrameworkElement BuildCodeBlock(string lang, string code)
    {
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bar = new Grid { Padding = new Thickness(8, 4, 4, 4) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(lang) ? "code" : lang,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });
        var copyBtn = new Button
        {
            Content = P("Copy code", "複製代碼"),
            FontSize = 11,
            Padding = new Thickness(8, 2, 8, 2),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        copyBtn.Click += (_, _) =>
        {
            var dp = new DataPackage();
            dp.SetText(code);
            Clipboard.SetContent(dp);
            copyBtn.Content = P("Copied ✓", "已複製 ✓");
        };
        Grid.SetColumn(copyBtn, 1);
        bar.Children.Add(copyBtn);
        Grid.SetRow(bar, 0);
        grid.Children.Add(bar);

        var codeText = new TextBox
        {
            Text = code.TrimEnd('\n'),
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            BorderThickness = new Thickness(0),
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
            IsSpellCheckEnabled = false,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(codeText, ScrollBarVisibility.Auto);
        Grid.SetRow(codeText, 1);
        grid.Children.Add(codeText);

        return new Border
        {
            Margin = new Thickness(0, 2, 0, 2),
            Background = (Brush)Application.Current.Resources["LayerFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = grid,
        };
    }

    /// <summary>把字串拆成「文字段」同「``` 代碼段」· Split text into prose and fenced-code segments.</summary>
    private static List<(bool isCode, string lang, string text)> SplitFences(string s)
    {
        var result = new List<(bool, string, string)>();
        int i = 0;
        var prose = new StringBuilder();
        while (i < s.Length)
        {
            int fence = s.IndexOf("```", i, StringComparison.Ordinal);
            if (fence < 0) { prose.Append(s.Substring(i)); break; }
            prose.Append(s.Substring(i, fence - i));
            // find lang line end
            int langEnd = s.IndexOf('\n', fence + 3);
            if (langEnd < 0) { prose.Append(s.Substring(fence)); break; }
            string lang = s.Substring(fence + 3, langEnd - (fence + 3)).Trim();
            int close = s.IndexOf("```", langEnd + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                // unterminated fence (still streaming) — render rest as code
                if (prose.Length > 0) { result.Add((false, "", prose.ToString())); prose.Clear(); }
                result.Add((true, lang, s.Substring(langEnd + 1)));
                return result;
            }
            if (prose.Length > 0) { result.Add((false, "", prose.ToString())); prose.Clear(); }
            result.Add((true, lang, s.Substring(langEnd + 1, close - (langEnd + 1))));
            i = close + 3;
        }
        if (prose.Length > 0) result.Add((false, "", prose.ToString()));
        return result;
    }

    private void DeleteMessage(ChatMessage m)
    {
        if (_active is null) return;
        _active.Messages.RemoveAll(x => x.Id == m.Id);
        _svc.SaveConversation(_active);
        RenderMessages();
    }

    private async Task EditAndResendAsync(ChatMessage m)
    {
        if (_active is null) return;
        var tb = new TextBox { Text = m.Content, AcceptsReturn = true, Height = 140, TextWrapping = TextWrapping.Wrap };
        var dlg = new ContentDialog
        {
            Title = P("Edit & resend", "編輯並重發"),
            Content = tb,
            PrimaryButtonText = P("Resend", "重發"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        // truncate everything from this user message onward, then resend edited text
        int idx = _active.Messages.FindIndex(x => x.Id == m.Id);
        if (idx >= 0) _active.Messages.RemoveRange(idx, _active.Messages.Count - idx);
        _svc.SaveConversation(_active);
        await SendUserTextAsync(tb.Text);
    }

    private async Task RegenerateAsync(ChatMessage assistantMsg)
    {
        if (_active is null) return;
        int idx = _active.Messages.FindIndex(x => x.Id == assistantMsg.Id);
        if (idx < 0) return;
        // drop the assistant message (and any trailing) then re-run from the prior user turn
        _active.Messages.RemoveRange(idx, _active.Messages.Count - idx);
        _svc.SaveConversation(_active);
        RenderMessages();
        await StreamAssistantAsync();
    }

    // ===================== Sending =====================

    private void Input_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            var shift = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
            if (!shift)
            {
                e.Handled = true;
                _ = SendUserTextAsync(InputBox.Text);
            }
        }
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendUserTextAsync(InputBox.Text);

    private async Task SendUserTextAsync(string text)
    {
        if (_active is null || _streamCts is not null) return;
        text = (text ?? "").Trim();
        if (text.Length == 0) return;

        InputBox.Text = "";
        _active.Messages.Add(new ChatMessage { Role = ChatRoles.User, Content = text });

        // auto-title from first user message
        if (_active.Messages.Count(x => x.Role == ChatRoles.User) == 1 &&
            (string.IsNullOrWhiteSpace(_active.Title) || _active.Title == P("New chat", "新對話")))
        {
            _active.Title = text.Length > 40 ? text.Substring(0, 40) + "…" : text;
            ChatTitleBox.Text = _active.Title;
            RenderConversationList();
        }
        _svc.SaveConversation(_active);
        RenderMessages();
        await StreamAssistantAsync();
    }

    private async Task StreamAssistantAsync()
    {
        if (_active is null) return;
        var provider = SelectedProvider();
        if (provider is null)
        {
            ShowInfo(InfoBarSeverity.Error, P("No provider", "冇供應商"),
                P("Add a provider in Manage.", "喺「管理」度加一個供應商。"));
            return;
        }

        var assistant = new ChatMessage { Role = ChatRoles.Assistant, Content = "" };
        _active.Messages.Add(assistant);
        var bubble = BuildMessageBubble(assistant);
        MessagesPanel.Children.Add(bubble);
        // replace bubble content live: keep a reference to its content panel
        ScrollToBottom();

        _streamCts = new CancellationTokenSource();
        ToggleSending(true);
        var sb = new StringBuilder();
        int? promptTok = null, compTok = null;
        bool? creditOk = null;
        long? creditUnits = null;
        LocalizedText? creditMessage = null;

        try
        {
            await _svc.StreamChatAsync(_active, provider, chunk =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (!string.IsNullOrEmpty(chunk.Delta))
                    {
                        sb.Append(chunk.Delta);
                        assistant.Content = sb.ToString();
                        UpdateBubble(bubble, assistant);
                        ScrollToBottom();
                    }
                    if (chunk.PromptTokens is int pt) promptTok = pt;
                    if (chunk.CompletionTokens is int ct2) compTok = ct2;
                    if (chunk.CreditMessage is not null)
                    {
                        creditOk = chunk.CreditSuccess;
                        creditUnits = chunk.CreditUnits;
                        creditMessage = chunk.CreditMessage;
                    }
                });
            }, _streamCts.Token);
        }
        catch { }

        assistant.Content = sb.ToString();
        assistant.TokenCount = compTok;
        ToggleSending(false);
        _streamCts?.Dispose();
        _streamCts = null;

        if (string.IsNullOrWhiteSpace(assistant.Content))
            _active.Messages.RemoveAll(x => x.Id == assistant.Id);
        _svc.SaveConversation(_active);
        RenderMessages();

        if (creditMessage is not null)
        {
            var units = creditUnits ?? CakeCreditService.GeneratedUnitsFrom(compTok, assistant.Content);
            var tokenText = !string.IsNullOrWhiteSpace(assistant.Content) && compTok is int c
                ? (promptTok is int p
                    ? P($"Prompt {p} tok · response {c} tok", $"提示 {p} tok · 回應 {c} tok")
                    : P($"Response {c} tokens", $"回應 {c} 個 token"))
                : P($"Estimated {CakeCreditService.FormatUnits(units)}", $"估算 {CakeCreditService.FormatUnits(units)}");
            var detail = string.IsNullOrWhiteSpace(assistant.Content) && creditOk != true
                ? creditMessage.Primary
                : $"{tokenText} · {creditMessage.Primary}";
            ShowInfo(creditOk == true ? InfoBarSeverity.Informational : InfoBarSeverity.Warning,
                creditOk == true ? P("Done · cake credits spent", "完成 · 已使用蛋糕額度") : P("Cake credits required", "需要蛋糕額度"),
                detail);
        }
    }

    private void UpdateBubble(FrameworkElement bubbleRoot, ChatMessage m)
    {
        // bubbleRoot is the StackPanel from BuildMessageBubble; replace its 2nd child (the Border bubble)
        if (bubbleRoot is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is Border b)
            b.Child = RenderContent(m.Content, m.Role == ChatRoles.User);
    }

    private void ToggleSending(bool sending)
    {
        SendBtn.Visibility = sending ? Visibility.Collapsed : Visibility.Visible;
        StopBtn.Visibility = sending ? Visibility.Visible : Visibility.Collapsed;
        InputBox.IsEnabled = !sending;
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        try { _streamCts?.Cancel(); } catch { }
    }

    private void ScrollToBottom()
    {
        DispatcherQueue.TryEnqueue(() => MessagesScroll.ChangeView(null, MessagesScroll.ScrollableHeight, null));
    }

    // ===================== Attach / export =====================

    private async void Attach_Click(object sender, RoutedEventArgs e)
    {
        var path = await FileDialogs.OpenFileAsync(".txt", ".md", ".cs", ".json", ".log", ".xml", ".yml", ".yaml", ".py", ".js", ".ts");
        if (path is null) return;
        try
        {
            var content = await File.ReadAllTextAsync(path);
            if (content.Length > 100_000) content = content.Substring(0, 100_000) + "\n…[truncated]";
            var name = Path.GetFileName(path);
            InputBox.Text = (InputBox.Text ?? "") +
                $"\n\n--- {name} ---\n{content}\n--- end {name} ---\n";
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error, P("Attach failed", "附加失敗"), ex.Message);
        }
    }

    private async Task ExportConversationAsync(ChatConversation c, bool json)
    {
        var ext = json ? ".json" : ".md";
        var safe = string.Concat((c.Title ?? "chat").Where(ch => !Path.GetInvalidFileNameChars().Contains(ch)));
        var path = await FileDialogs.SaveFileAsync(safe + ext, ext);
        if (path is null) return;
        try
        {
            if (json)
            {
                await File.WriteAllTextAsync(path,
                    System.Text.Json.JsonSerializer.Serialize(c, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine($"# {c.Title}\n");
                if (!string.IsNullOrWhiteSpace(c.SystemPrompt))
                    sb.AppendLine($"> **System / 系統:** {c.SystemPrompt}\n");
                foreach (var m in c.Messages)
                {
                    sb.AppendLine($"### {(m.Role == ChatRoles.User ? "You · 你" : "Assistant · 助手")}\n");
                    sb.AppendLine(m.Content + "\n");
                }
                await File.WriteAllTextAsync(path, sb.ToString());
            }
            ShowInfo(InfoBarSeverity.Success, P("Exported", "已匯出"), path);
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error, P("Export failed", "匯出失敗"), ex.Message);
        }
    }

    // ===================== Manage dialog (providers + Ollama) =====================

    private async void Manage_Click(object sender, RoutedEventArgs e)
    {
        var root = new StackPanel { Spacing = 14, Width = 520 };

        // --- Providers section ---
        root.Children.Add(new TextBlock { Text = P("Providers", "供應商"), FontWeight = FontWeights.SemiBold });
        var providersPanel = new StackPanel { Spacing = 8 };
        root.Children.Add(providersPanel);

        void RefreshProvidersPanel()
        {
            providersPanel.Children.Clear();
            foreach (var p in _svc.Providers)
                providersPanel.Children.Add(BuildProviderEditor(p, RefreshProvidersPanel));
        }
        RefreshProvidersPanel();

        var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var addOllama = new Button { Content = "+ Ollama" };
        addOllama.Click += (_, _) =>
        {
            _svc.UpsertProvider(new AiProvider { Name = "Ollama", Kind = AiProviderKind.Ollama, BaseUrl = "http://localhost:11434" });
            RefreshProvidersPanel(); RefreshProviderPicker();
        };
        var addOpenAi = new Button { Content = "+ OpenAI-compatible" };
        addOpenAi.Click += (_, _) =>
        {
            _svc.UpsertProvider(new AiProvider { Name = "OpenAI-compatible", Kind = AiProviderKind.OpenAiCompatible, BaseUrl = "https://api.openai.com" });
            RefreshProvidersPanel(); RefreshProviderPicker();
        };
        var addCli = new Button { Content = "+ CLI agents" };
        addCli.Click += (_, _) =>
        {
            _svc.UpsertProvider(new AiProvider { Name = "CLI agents · CLI 代理", Kind = AiProviderKind.Cli });
            RefreshProvidersPanel(); RefreshProviderPicker();
        };
        addRow.Children.Add(addOllama);
        addRow.Children.Add(addOpenAi);
        addRow.Children.Add(addCli);
        root.Children.Add(addRow);

        // --- Ollama management section ---
        root.Children.Add(new TextBlock { Text = P("Ollama models", "Ollama 模型"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });

        var ollamaProvider = _svc.Providers.FirstOrDefault(p => p.Kind == AiProviderKind.Ollama);
        var ollamaUrl = ollamaProvider?.BaseUrl ?? "http://localhost:11434";

        var ollamaBar = new InfoBar { IsClosable = false, IsOpen = false };
        root.Children.Add(ollamaBar);

        bool running = await _svc.OllamaRunningAsync(ollamaUrl);
        if (!running)
        {
            ollamaBar.IsOpen = true;
            ollamaBar.Severity = InfoBarSeverity.Warning;
            ollamaBar.Title = P("Ollama not running", "Ollama 未運行");
            ollamaBar.Message = P("Install Ollama to run local models.", "安裝 Ollama 嚟行本機模型。");
            ollamaBar.ActionButton = EngineBars.AutoInstallButton("Ollama.Ollama",
                "Install Ollama", "安裝 Ollama", async () => { ollamaBar.IsOpen = false; }, null);
        }

        var pullRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var pullBox = new TextBox { PlaceholderText = P("model name e.g. llama3.2", "模型名 例如 llama3.2"), Width = 260 };
        var pullBtn = new Button { Content = P("Pull", "拉取") };
        pullRow.Children.Add(pullBox);
        pullRow.Children.Add(pullBtn);
        root.Children.Add(pullRow);

        var pullProgress = new ProgressBar { Minimum = 0, Maximum = 100, Visibility = Visibility.Collapsed };
        var pullStatus = new TextBlock { FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"] };
        root.Children.Add(pullProgress);
        root.Children.Add(pullStatus);

        var modelsPanel = new StackPanel { Spacing = 6 };
        root.Children.Add(modelsPanel);

        async Task RefreshModelsListAsync()
        {
            modelsPanel.Children.Clear();
            var models = await _svc.ListOllamaModelsAsync(ollamaUrl);
            foreach (var mdl in models)
            {
                var row = new Grid { ColumnSpacing = 8 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var info = new TextBlock
                {
                    Text = $"{mdl.Name}  ·  {mdl.SizeDisplay}" + (string.IsNullOrEmpty(mdl.ParameterSize) ? "" : $"  ·  {mdl.ParameterSize} {mdl.Quantization}"),
                    FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                };
                row.Children.Add(info);
                var delBtn = new Button { Content = new FontIcon { FontSize = 12, Glyph = "" } };
                var capturedName = mdl.Name;
                delBtn.Click += async (_, _) =>
                {
                    delBtn.IsEnabled = false;
                    await _svc.DeleteOllamaModelAsync(ollamaUrl, capturedName);
                    await RefreshModelsListAsync();
                    await RefreshModelsAsync();
                };
                Grid.SetColumn(delBtn, 1);
                row.Children.Add(delBtn);
                modelsPanel.Children.Add(row);
            }
            if (models.Count == 0)
                modelsPanel.Children.Add(new TextBlock { Text = P("No models installed.", "未安裝任何模型。"), FontSize = 12, Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"] });
        }
        if (running) await RefreshModelsListAsync();

        pullBtn.Click += async (_, _) =>
        {
            var name = pullBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            pullBtn.IsEnabled = false;
            pullProgress.Visibility = Visibility.Visible;
            pullProgress.IsIndeterminate = true;
            var ok = await _svc.PullOllamaModelAsync(ollamaUrl, name, (status, completed, total) =>
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    pullStatus.Text = status;
                    if (total > 0)
                    {
                        pullProgress.IsIndeterminate = false;
                        pullProgress.Value = (double)completed / total * 100;
                    }
                });
            });
            pullProgress.Visibility = Visibility.Collapsed;
            pullStatus.Text = ok ? P("Pulled ✓", "已拉取 ✓") : P("Pull failed", "拉取失敗");
            pullBtn.IsEnabled = true;
            if (ok) { await RefreshModelsListAsync(); await RefreshModelsAsync(); }
        };

        var dlg = new ContentDialog
        {
            Title = P("Providers & Ollama", "供應商與 Ollama"),
            Content = new ScrollViewer { Content = root, MaxHeight = 560 },
            CloseButtonText = P("Close", "關閉"),
            XamlRoot = this.XamlRoot,
        };
        await dlg.ShowAsync();
        RefreshProviderPicker();
        await RefreshModelsAsync();
    }

    private FrameworkElement BuildProviderEditor(AiProvider p, Action refresh)
    {
        var panel = new StackPanel { Spacing = 6 };
        var head = new Grid { ColumnSpacing = 8 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameBox = new TextBox { Text = p.Name, PlaceholderText = P("Name", "名稱") };
        nameBox.LostFocus += (_, _) => { p.Name = nameBox.Text?.Trim() ?? p.Name; _svc.UpsertProvider(p); RefreshProviderPicker(); };
        head.Children.Add(nameBox);
        var delBtn = new Button { Content = new FontIcon { FontSize = 12, Glyph = "" } };
        delBtn.Click += (_, _) => { _svc.DeleteProvider(p.Id); RefreshProviderPicker(); refresh(); };
        Grid.SetColumn(delBtn, 1);
        head.Children.Add(delBtn);
        panel.Children.Add(head);

        panel.Children.Add(new TextBlock
        {
            Text = p.Kind switch
            {
                AiProviderKind.Ollama => P("Local Ollama (REST)", "本機 Ollama（REST）"),
                AiProviderKind.OpenAiCompatible => P("OpenAI-compatible /v1", "OpenAI 相容 /v1"),
                _ => P("Installed CLI agents (one-shot)", "已安裝 CLI 代理（一次性）"),
            },
            FontSize = 11,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });

        if (p.Kind != AiProviderKind.Cli)
        {
            var urlBox = new TextBox { Text = p.BaseUrl, PlaceholderText = "https://… / http://localhost:11434", Header = P("Base URL", "基底網址") };
            urlBox.LostFocus += (_, _) => { p.BaseUrl = urlBox.Text?.Trim() ?? ""; _svc.UpsertProvider(p); };
            panel.Children.Add(urlBox);
        }

        if (p.Kind == AiProviderKind.OpenAiCompatible)
        {
            var keyBox = new PasswordBox { Password = p.ApiKey, Header = P("API key (stored encrypted · DPAPI)", "API 金鑰（加密儲存 · DPAPI）") };
            keyBox.LostFocus += (_, _) => { p.ApiKey = keyBox.Password ?? ""; _svc.UpsertProvider(p); };
            panel.Children.Add(keyBox);
        }

        var defModelBox = new TextBox { Text = p.DefaultModel, PlaceholderText = P("default model id (optional)", "預設模型 id（可選）"), Header = P("Default model", "預設模型") };
        defModelBox.LostFocus += (_, _) => { p.DefaultModel = defModelBox.Text?.Trim() ?? ""; _svc.UpsertProvider(p); };
        panel.Children.Add(defModelBox);

        return new Border
        {
            Padding = new Thickness(12),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = panel,
        };
    }

    // ===================== status =====================

    private void ShowInfo(InfoBarSeverity sev, string title, string msg)
    {
        StatusBar.IsOpen = true;
        StatusBar.Severity = sev;
        StatusBar.Title = title;
        StatusBar.Message = msg;
    }
}
