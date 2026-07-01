using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 本機 Ollama 管理 · In-app Ollama manager over the local REST API (http://localhost:11434):
/// detect / install / start the server, list installed models with size + quantization, pull a model
/// with a live progress bar (cancellable), delete a model (confirmed), show running models with a
/// GPU/CPU split and unload them, and a streaming chat pane with a Stop button and a tunable
/// parameter panel (temperature / top_p / top_k / num_ctx / seed / system prompt). No WebView, no CLI
/// scraping for the API parts. Everything tolerates Ollama being absent or offline. Bilingual.
/// </summary>
public sealed partial class OllamaModule : Page
{
    private readonly OllamaService _svc = new();
    private readonly ObservableCollection<WinForge.Services.OllamaModel> _models = new();
    private readonly ObservableCollection<OllamaRunningModel> _running = new();
    private readonly List<OllamaChatMessage> _history = new();
    private List<TweakDefinition>? _ops;

    private CancellationTokenSource? _pullCts;
    private CancellationTokenSource? _chatCts;
    private bool _installed;
    private bool _rowBusy; // guard so only one Operations control-row action runs at a time

    private static readonly string[] PopularModels =
        { "llama3.2", "llama3.1", "qwen2.5", "gemma2", "mistral", "phi3", "deepseek-r1" };

    public OllamaModule()
    {
        InitializeComponent();
        ModelsList.ItemsSource = _models;
        RunningList.ItemsSource = _running;
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; _pullCts?.Cancel(); _chatCts?.Cancel(); };
        ChatInput.KeyDown += ChatInput_KeyDown;
        HookSliders();
        Loaded += async (_, _) =>
        {
            UrlBox.Text = _svc.BaseUrl;
            Render();
            BuildPullSuggestions();
            PopulateOps();
            await ProbeAndRefresh();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private void OnLang(object? sender, EventArgs e) { Render(); BuildPullSuggestions(); PopulateOps(); }

    private void Render()
    {
        Header.Title = "Ollama · 本地大模型";
        Header.Subtitle = P(
            "Run local large-language models with Ollama — manage installed models, pull new ones with live progress, watch what's loaded in memory, and chat with streaming tokens and tunable parameters. Everything talks to the local API on port 11434.",
            "用 Ollama 喺本機跑大型語言模型 — 管理已安裝模型、即時進度下載新模型、睇住記憶體載咗咩、同串流式逐字聊天兼可調參數。全部都係同本機 11434 埠嘅 API 溝通。");
        RefreshCreditStatus();

        UrlLabel.Text = P("API URL · API 網址", "API 網址");
        SaveUrlBtn.Content = P("Save · 儲存", "儲存");
        RefreshBtn.Content = P("Connect · 連線", "連線");

        TabModels.Header = P("Models · 模型", "模型");
        TabPull.Header = P("Pull · 下載", "下載");
        TabRunning.Header = P("Running · 運行中", "運行中");
        TabChat.Header = P("Chat · 聊天", "聊天");
        TabOps.Header = P("Operations · 操作", "操作");

        ReloadModelsBtn.Content = P("Refresh · 重整", "重整");
        ReloadRunningBtn.Content = P("Refresh · 重整", "重整");

        PullBlurb.Text = P("Type a model name (e.g. llama3.2 or qwen2.5:7b) and pull it. Downloads can be several GB; progress streams below and you can cancel.",
            "輸入模型名（例如 llama3.2 或 qwen2.5:7b）然後下載。下載可以幾 GB；進度喺下面串流顯示，可隨時取消。");
        PullBtn.Content = P("Pull · 下載", "下載");
        CancelPullBtn.Content = P("Cancel · 取消", "取消");
        PullSuggestLabel.Text = P("Popular:", "熱門：");

        ParamTitle.Text = P("Parameters · 參數", "參數");
        SysLabel.Text = P("System prompt · 系統提示", "系統提示");
        ChatSendBtn.Content = P("Send · 傳送", "傳送");
        ChatStopBtn.Content = P("Stop · 停止", "停止");

        OpsBlurb.Text = P("Lifecycle and utility operations — start the server, query via the CLI, open the models folder or the online library.",
            "生命週期同工具操作 — 啟動伺服器、用 CLI 查詢、開模型資料夾或線上模型庫。");

        UpdateParamLabels();
        UpdateCounts();
    }

    private void UpdateParamLabels()
    {
        TempLabel.Text = P($"Temperature · 溫度: {TempSlider.Value:0.00}", $"溫度: {TempSlider.Value:0.00}");
        TopPLabel.Text = P($"top_p: {TopPSlider.Value:0.00}", $"top_p: {TopPSlider.Value:0.00}");
        TopKLabel.Text = P($"top_k: {(int)TopKSlider.Value}", $"top_k: {(int)TopKSlider.Value}");
        CtxLabel.Text = P("Context length (num_ctx) · 上下文長度", "上下文長度 (num_ctx)");
        SeedLabel.Text = P("Seed · 隨機種子 (optional)", "隨機種子（可選）");
    }

    // ── Connection / probe ────────────────────────────────────────────────────

    private void SaveUrl_Click(object sender, RoutedEventArgs e)
    {
        _svc.SaveBaseUrl(UrlBox.Text);
        UrlBox.Text = _svc.BaseUrl;
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _svc.SaveBaseUrl(UrlBox.Text);
        UrlBox.Text = _svc.BaseUrl;
        await ProbeAndRefresh();
    }

    private async Task ProbeAndRefresh()
    {
        ProbeBusy.IsActive = true;
        string? version = await _svc.GetVersionAsync();
        ProbeBusy.IsActive = false;

        bool reachable = version is not null;
        VersionPill.Visibility = reachable ? Visibility.Visible : Visibility.Collapsed;
        if (reachable) VersionText.Text = $"v{version}";

        _installed = reachable || await PackageService.IsInstalled(OllamaService.WingetId);

        if (reachable)
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
            await Task.WhenAll(LoadModels(), LoadRunning());
            SyncChatModels();
        }
        else if (!_installed)
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Ollama not found", "搵唔到 Ollama");
            EngineBar.Message = P("Click to install Ollama automatically (winget) — no restart needed.",
                "撳一下自動安裝 Ollama（winget）— 唔使重開。");
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                OllamaService.WingetId, "Install Ollama automatically", "自動安裝 Ollama",
                async () => { PackageService.RefreshProcessPath(); await ProbeAndRefresh(); }, null);
        }
        else
        {
            // Installed but the API is not reachable → offer to start `ollama serve`.
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Ollama is installed but not reachable", "Ollama 已安裝但連唔到");
            EngineBar.Message = P($"No response at {_svc.BaseUrl}. Start the server, or check the URL above.",
                $"{_svc.BaseUrl} 無回應。啟動伺服器，或者檢查上面個網址。");
            var start = new Button { Content = P("Start server · 啟動伺服器", "啟動伺服器") };
            start.Click += async (_, _) =>
            {
                start.IsEnabled = false;
                start.Content = P("Starting…", "啟動緊…");
                OllamaService.StartServe();
                await Task.Delay(2500);
                await ProbeAndRefresh();
            };
            EngineBar.ActionButton = start;
            _models.Clear(); _running.Clear(); SyncChatModels(); UpdateCounts();
        }
    }

    // ── Installed models ──────────────────────────────────────────────────────

    private async void ReloadModels_Click(object sender, RoutedEventArgs e) => await LoadModels();

    private async Task LoadModels()
    {
        ModelsBusy.IsActive = true;
        var list = await _svc.ListModelsAsync();
        ModelsBusy.IsActive = false;
        _models.Clear();
        foreach (var m in list) _models.Add(m);
        ModelsEmpty.IsOpen = list.Count == 0;
        ModelsEmpty.Message = P("No models installed yet. Use the Pull tab to download one.",
            "未安裝任何模型。去「下載」頁攞一個。");
        SyncChatModels();
        UpdateCounts();
    }

    private async void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string name || name.Length == 0) return;
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Delete model? · 刪除模型？", "刪除模型？"),
            Content = P($"This permanently deletes \"{name}\" and frees its disk space. This cannot be undone.",
                $"呢個會永久刪除「{name}」並釋放佔用嘅磁碟空間，無法復原。"),
            PrimaryButtonText = P("Delete · 刪除", "刪除"),
            CloseButtonText = P("Cancel · 取消", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var (ok, msg) = await _svc.DeleteModelAsync(name);
        if (ok) await LoadModels();
        ModelsEmpty.IsOpen = true;
        ModelsEmpty.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ModelsEmpty.Message = ok
            ? P($"Deleted {name}.", $"已刪除 {name}。")
            : P($"Delete failed: {msg}", $"刪除失敗：{msg}");
    }

    // ── Pull ──────────────────────────────────────────────────────────────────

    private void BuildPullSuggestions()
    {
        PullSuggestions.Items.Clear();
        foreach (var m in PopularModels)
        {
            var chip = new Button { Content = m, Padding = new Thickness(8, 2, 8, 2), FontSize = 12 };
            chip.Click += (_, _) => PullNameBox.Text = m;
            PullSuggestions.Items.Add(chip);
        }
    }

    private async void Pull_Click(object sender, RoutedEventArgs e)
    {
        var name = (PullNameBox.Text ?? "").Trim();
        if (name.Length == 0)
        {
            PullResult.IsOpen = true; PullResult.Severity = InfoBarSeverity.Warning;
            PullResult.Message = P("Enter a model name first.", "請先輸入模型名。");
            return;
        }
        _pullCts?.Cancel();
        _pullCts = new CancellationTokenSource();
        var ct = _pullCts.Token;

        PullBtn.IsEnabled = false; CancelPullBtn.IsEnabled = true;
        PullResult.IsOpen = false;
        PullProgressCard.Visibility = Visibility.Visible;
        PullBar.Value = 0; PullBar.IsIndeterminate = true;
        PullStatusText.Text = P($"Pulling {name}…", $"下載緊 {name}…");
        PullBytesText.Text = "";

        string? err = null; bool success = false;
        try
        {
            await foreach (var prog in _svc.PullModelAsync(name, ct))
            {
                if (prog.Failed) { err = prog.Error; break; }
                PullStatusText.Text = P($"{name}: {prog.Status}", $"{name}：{prog.Status}");
                if (prog.HasBytes)
                {
                    PullBar.IsIndeterminate = false;
                    PullBar.Value = prog.Fraction;
                    PullBytesText.Text = $"{OllamaService.HumanSize(prog.Completed)} / {OllamaService.HumanSize(prog.Total)}  ({prog.Fraction:0.0%})";
                }
                else
                {
                    PullBar.IsIndeterminate = true;
                    PullBytesText.Text = "";
                }
                if (prog.Done) { success = true; break; }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { err = ex.Message; }

        PullBtn.IsEnabled = true; CancelPullBtn.IsEnabled = false;
        PullProgressCard.Visibility = Visibility.Collapsed;
        PullResult.IsOpen = true;
        if (ct.IsCancellationRequested && !success)
        {
            PullResult.Severity = InfoBarSeverity.Informational;
            PullResult.Message = P($"Cancelled pulling {name}. Partial data is kept and resumes next time.",
                $"已取消下載 {name}。已下載部分會保留，下次續傳。");
        }
        else if (success)
        {
            PullResult.Severity = InfoBarSeverity.Success;
            PullResult.Message = P($"Pulled {name} successfully.", $"成功下載 {name}。");
            await LoadModels();
        }
        else
        {
            PullResult.Severity = InfoBarSeverity.Error;
            PullResult.Message = P($"Pull failed: {err}", $"下載失敗：{err}");
        }
    }

    private void CancelPull_Click(object sender, RoutedEventArgs e) => _pullCts?.Cancel();

    // ── Running models ────────────────────────────────────────────────────────

    private async void ReloadRunning_Click(object sender, RoutedEventArgs e) => await LoadRunning();

    private async Task LoadRunning()
    {
        RunningBusy.IsActive = true;
        var list = await _svc.ListRunningAsync();
        RunningBusy.IsActive = false;
        _running.Clear();
        foreach (var m in list) _running.Add(m);
        RunningEmpty.IsOpen = list.Count == 0;
        RunningEmpty.Message = P("No models are loaded in memory right now. Models load on first use (chat) and unload after a few minutes.",
            "依家無模型載入記憶體。模型喺第一次使用（聊天）時載入，幾分鐘後自動卸載。");
        UpdateCounts();
    }

    private async void StopModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string name || name.Length == 0) return;
        // Unload = chat with keep_alive 0 via the CLI `ollama stop`.
        await ShellRunner.Run("ollama", $"stop {name}");
        await LoadRunning();
    }

    private void UpdateCounts()
    {
        ModelsCount.Text = P($"{_models.Count} installed", $"已安裝 {_models.Count} 個");
        RunningCount.Text = P($"{_running.Count} loaded", $"載入 {_running.Count} 個");
    }

    // ── Chat ──────────────────────────────────────────────────────────────────

    private void SyncChatModels()
    {
        var current = ChatModelBox.SelectedItem as string;
        ChatModelBox.Items.Clear();
        foreach (var m in _models) ChatModelBox.Items.Add(m.Name);
        if (current is not null && _models.Any(m => m.Name == current)) ChatModelBox.SelectedItem = current;
        else if (ChatModelBox.Items.Count > 0) ChatModelBox.SelectedIndex = 0;
    }

    private async void ChatReloadModels_Click(object sender, RoutedEventArgs e) => await LoadModels();

    private void ChatClear_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        ChatLog.Children.Clear();
    }

    private void ChatInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            var ctrl = Microsoft.UI.Input.InputKeyboardSource
                .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
            if (ctrl) { e.Handled = true; ChatSend_Click(sender, e); }
        }
    }

    private async void ChatSend_Click(object sender, RoutedEventArgs e)
    {
        var model = ChatModelBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(model))
        {
            AddSystemNote(P("Pick a model first (Models tab → Pull if you have none).",
                "請先揀一個模型（無嘅話去「下載」頁攞）。"));
            return;
        }
        var text = (ChatInput.Text ?? "").Trim();
        if (text.Length == 0) return;

        var creditReady = CakeCreditService.I.CheckCanStartGeneration("Ollama", "Ollama");
        if (!creditReady.Success)
        {
            AddSystemNote(creditReady.Message.Primary);
            RefreshCreditStatus();
            return;
        }

        ChatInput.Text = "";
        AddBubble(text, isUser: true);

        // Build the request history: optional system prompt + full conversation + this turn.
        _history.Add(new OllamaChatMessage { Role = "user", Content = text });
        var request = new List<OllamaChatMessage>();
        var sys = (SysPromptBox.Text ?? "").Trim();
        if (sys.Length > 0) request.Add(new OllamaChatMessage { Role = "system", Content = sys });
        request.AddRange(_history);

        var options = new OllamaChatOptions
        {
            Temperature = TempSlider.Value,
            TopP = TopPSlider.Value,
            TopK = (int)TopKSlider.Value,
            NumCtx = (int)CtxBox.Value > 0 ? (int)CtxBox.Value : null,
            Seed = !double.IsNaN(SeedBox.Value) ? (int)SeedBox.Value : null,
        };

        var replyBlock = AddBubble("", isUser: false);
        _chatCts?.Cancel();
        _chatCts = new CancellationTokenSource();
        var ct = _chatCts.Token;

        ChatSendBtn.IsEnabled = false; ChatStopBtn.IsEnabled = true; ChatInput.IsEnabled = false;
        var assistant = new OllamaChatMessage { Role = "assistant", Content = "" };
        bool any = false; string? error = null;
        int? promptTok = null, compTok = null;
        try
        {
            await foreach (var chunk in _svc.ChatStreamAsync(model, request, options, ct))
            {
                if (chunk.Failed) { error = chunk.Error; break; }
                if (chunk.Content.Length > 0)
                {
                    any = true;
                    assistant.Content += chunk.Content;
                    replyBlock.Text = assistant.Content;
                    ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null, true);
                }
                if (chunk.PromptTokens is int pt) promptTok = pt;
                if (chunk.CompletionTokens is int ct2) compTok = ct2;
                if (chunk.Done) break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { error = ex.Message; }

        ChatSendBtn.IsEnabled = true; ChatStopBtn.IsEnabled = false; ChatInput.IsEnabled = true;

        if (error is not null)
        {
            replyBlock.Text = (any ? assistant.Content + "\n\n" : "") + P($"[error: {error}]", $"[錯誤：{error}]");
            replyBlock.Foreground = (SolidColorBrush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            // Drop the half-finished turn so history stays consistent.
            _history.RemoveAt(_history.Count - 1);
        }
        else if (ct.IsCancellationRequested && !any)
        {
            replyBlock.Text = P("[stopped]", "[已停止]");
            _history.RemoveAt(_history.Count - 1);
        }
        else
        {
            _history.Add(assistant);
            var units = CakeCreditService.GeneratedUnitsFrom(compTok, assistant.Content);
            var charge = CakeCreditService.I.TryChargeGeneratedUnits("Ollama", "Ollama", units);
            var tokenText = compTok is int c
                ? (promptTok is int p
                    ? P($"Prompt {p} tok · response {c} tok", $"提示 {p} tok · 回應 {c} tok")
                    : P($"Response {c} tokens", $"回應 {c} 個 token"))
                : P($"Estimated {CakeCreditService.FormatUnits(units)}", $"估算 {CakeCreditService.FormatUnits(units)}");
            AddSystemNote($"{tokenText} · {charge.Message.Primary}");
            RefreshCreditStatus();
        }
        ChatInput.Focus(FocusState.Programmatic);
        await LoadRunning();
    }

    private void ChatStop_Click(object sender, RoutedEventArgs e) => _chatCts?.Cancel();

    private void Credit_Click(object sender, RoutedEventArgs e)
    {
        var r = CakeCreditService.I.FeedOneCake(
            "Ollama credits",
            "Ollama 額度");
        RefreshCreditStatus();
        AddSystemNote(r.Message.Primary);
    }

    private void RefreshCreditStatus()
    {
        var s = CakeCreditService.I.Snapshot;
        CreditText.Text = CakeCreditService.FormatUnits(s.BalanceUnits);
        ToolTipService.SetToolTip(CreditBtn, P(
            $"Cake generation credits\nBalance: {CakeCreditService.FormatUnits(s.BalanceUnits)}\nPacked cakes ready: {s.CakeFilesAvailable}\nSpent: {CakeCreditService.FormatUnits(s.LifetimeSpentUnits)}\n1 cake = 1,000,000 generated units.",
            $"蛋糕生成額度\n餘額：{CakeCreditService.FormatUnits(s.BalanceUnits)}\n可用已包裝蛋糕：{s.CakeFilesAvailable}\n已使用：{CakeCreditService.FormatUnits(s.LifetimeSpentUnits)}\n1 個蛋糕 = 1,000,000 個生成單位。"));
    }

    private TextBlock AddBubble(string text, bool isUser)
    {
        var inner = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        var bubble = new Border
        {
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(8),
            MaxWidth = 560,
            HorizontalAlignment = isUser ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            Background = (SolidColorBrush)Application.Current.Resources[
                isUser ? "AccentFillColorDefaultBrush" : "CardBackgroundFillColorDefaultBrush"],
            Child = inner,
        };
        if (isUser)
            inner.Foreground = (SolidColorBrush)Application.Current.Resources["TextOnAccentFillColorPrimaryBrush"];
        ChatLog.Children.Add(bubble);
        ChatScroll.ChangeView(null, ChatScroll.ScrollableHeight, null, true);
        return inner;
    }

    private void AddSystemNote(string text)
    {
        ChatLog.Children.Add(new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (SolidColorBrush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
    }

    // Live parameter label updates (sliders are wired in XAML via ValueChanged? do it programmatically).
    // We update on demand from a single handler attached after load.
    private void HookSliders()
    {
        TempSlider.ValueChanged += (_, _) => UpdateParamLabels();
        TopPSlider.ValueChanged += (_, _) => UpdateParamLabels();
        TopKSlider.ValueChanged += (_, _) => UpdateParamLabels();
    }

    // ── Operations ────────────────────────────────────────────────────────────

    private void PopulateOps()
    {
        _ops ??= OllamaOperations.All().ToList();
        OpsPanel.Children.Clear();
        bool first = true;
        foreach (var op in _ops)
        {
            if (!first) OpsPanel.Children.Add(BuildDivider());
            first = false;
            OpsPanel.Children.Add(BuildRow(op));
        }
    }

    // ---- One clean row: bilingual title + description on the left, control on the right ----
    private FrameworkElement BuildRow(TweakDefinition op)
    {
        var grid = new Grid { Padding = new Thickness(0, 12, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var text = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };

        text.Children.Add(new TextBlock { Text = op.Title.Primary, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });

        if (!string.IsNullOrWhiteSpace(op.Title.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Title.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        if (!string.IsNullOrWhiteSpace(op.Description.Primary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Primary,
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0),
            });
        if (!string.IsNullOrWhiteSpace(op.Description.Secondary))
            text.Children.Add(new TextBlock
            {
                Text = op.Description.Secondary,
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
                TextWrapping = TextWrapping.Wrap,
            });

        Grid.SetColumn(text, 0);
        grid.Children.Add(text);

        var control = BuildControl(op);
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        return grid;
    }

    private Border BuildDivider() => new()
    {
        Height = 1,
        Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        Opacity = 0.6,
    };

    /// <summary>對應每種 Tweak 種類砌一個真控件 · Build the matching WinUI control for the tweak kind.</summary>
    private FrameworkElement BuildControl(TweakDefinition op) => op.Kind switch
    {
        TweakKind.Toggle => BuildToggle(op),
        TweakKind.Choice => BuildChoice(op),
        TweakKind.Slider => BuildSlider(op),
        TweakKind.Number => BuildNumber(op),
        TweakKind.Info => BuildInfo(op),
        _ => BuildAction(op), // Action (and any other kind) → button
    };

    // ---------------- Action → Button awaiting RunAsync ----------------
    private FrameworkElement BuildAction(TweakDefinition op)
    {
        var label = op.ActionLabel?.Get(Loc.I.Language) ?? P("Run", "執行");
        var btn = new Button { Content = label, MinWidth = 110 };
        if (op.ActionLabel is not null)
            ToolTipService.SetToolTip(btn, $"{op.ActionLabel.En} · {op.ActionLabel.Zh}");

        btn.Click += async (_, _) =>
        {
            if (_rowBusy || op.RunAsync is null) return;
            if (op.Destructive && !await ConfirmAsync(op)) return;

            _rowBusy = true;
            btn.IsEnabled = false;
            var restore = btn.Content;
            btn.Content = new ProgressRing { IsActive = true, Width = 18, Height = 18 };
            try
            {
                var result = await op.RunAsync(CancellationToken.None);
                ShowResult(result);
            }
            catch (Exception ex)
            {
                ShowError(op, ex);
            }
            finally
            {
                btn.Content = restore;
                btn.IsEnabled = true;
                _rowBusy = false;
            }
        };
        return btn;
    }

    // ---------------- Toggle → ToggleSwitch ----------------
    private FrameworkElement BuildToggle(TweakDefinition op)
    {
        var toggle = new ToggleSwitch { OnContent = "On · 開", OffContent = "Off · 熄" };
        bool suppress = true;
        try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* show as off */ }
        suppress = false;

        toggle.Toggled += (_, _) =>
        {
            if (suppress || op.SetIsOn is null) return;
            try { op.SetIsOn(toggle.IsOn); ShowApplied(op); }
            catch (Exception ex)
            {
                suppress = true;
                try { toggle.IsOn = op.GetIsOn?.Invoke() ?? false; } catch { /* ignore */ }
                suppress = false;
                ShowError(op, ex);
            }
        };
        return toggle;
    }

    // ---------------- Choice → ComboBox ----------------
    private FrameworkElement BuildChoice(TweakDefinition op)
    {
        var combo = new ComboBox { MinWidth = 170 };
        if (op.Choices is not null)
            foreach (var c in op.Choices)
                combo.Items.Add(new ComboBoxItem { Content = c.Label.Get(Loc.I.Language), Tag = c.Value });

        bool suppress = true;
        try
        {
            var cur = op.GetCurrentChoice?.Invoke();
            if (cur is not null && op.Choices is not null)
                for (int i = 0; i < op.Choices.Count; i++)
                    if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                    { combo.SelectedIndex = i; break; }
        }
        catch { /* leave unselected */ }
        suppress = false;

        combo.SelectionChanged += (_, _) =>
        {
            if (suppress || op.SetChoice is null) return;
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string val)
            {
                try { op.SetChoice(val); ShowApplied(op); }
                catch (Exception ex)
                {
                    ShowError(op, ex);
                    suppress = true;
                    try
                    {
                        var cur = op.GetCurrentChoice?.Invoke();
                        if (cur is not null && op.Choices is not null)
                            for (int i = 0; i < op.Choices.Count; i++)
                                if (string.Equals(op.Choices[i].Value, cur, StringComparison.OrdinalIgnoreCase))
                                { combo.SelectedIndex = i; break; }
                    }
                    catch { /* ignore */ }
                    suppress = false;
                }
            }
        };
        return combo;
    }

    // ---------------- Slider → Slider + value label ----------------
    private FrameworkElement BuildSlider(TweakDefinition op)
    {
        string Format(double v)
        {
            bool whole = op.Step >= 1 && Math.Abs(op.Step % 1) < 1e-9;
            string num = whole ? Math.Round(v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                               : v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return op.Unit is null ? num : $"{num} {op.Unit.Primary}";
        }
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        var slider = new Slider { Minimum = op.Min, Maximum = op.Max, StepFrequency = op.Step, Width = 160, VerticalAlignment = VerticalAlignment.Center };
        var valueText = new TextBlock
        {
            MinWidth = 56,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };

        bool suppress = true;
        try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { slider.Value = op.Min; }
        suppress = false;
        valueText.Text = Format(slider.Value);

        slider.ValueChanged += (_, e) =>
        {
            valueText.Text = Format(e.NewValue);
            if (suppress || op.SetNumber is null) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { slider.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        panel.Children.Add(slider);
        panel.Children.Add(valueText);
        return panel;
    }

    // ---------------- Number → NumberBox ----------------
    private FrameworkElement BuildNumber(TweakDefinition op)
    {
        double Clamp(double v) => Math.Max(op.Min, Math.Min(op.Max, v));
        var box = new NumberBox
        {
            Minimum = op.Min,
            Maximum = op.Max,
            SmallChange = op.Step,
            LargeChange = op.Step,
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
            MinWidth = 140,
            ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
        };
        bool suppress = true;
        try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { box.Value = op.Min; }
        suppress = false;

        box.ValueChanged += (_, e) =>
        {
            if (suppress || op.SetNumber is null || double.IsNaN(e.NewValue)) return;
            try { op.SetNumber(e.NewValue); ShowApplied(op); }
            catch (Exception ex)
            {
                ShowError(op, ex);
                suppress = true;
                try { box.Value = Clamp(op.GetNumber?.Invoke() ?? op.Min); } catch { /* ignore */ }
                suppress = false;
            }
        };
        return box;
    }

    // ---------------- Info → refreshable TextBlock ----------------
    private FrameworkElement BuildInfo(TweakDefinition op)
    {
        string Safe() { try { return op.GetInfo?.Invoke() ?? "—"; } catch { return "—"; } }

        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var info = new TextBlock
        {
            Text = Safe(),
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 300,
            HorizontalTextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var refresh = new Button { Content = new FontIcon { Glyph = "", FontSize = 14 }, Padding = new Thickness(8) };
        ToolTipService.SetToolTip(refresh, "Refresh · 重新整理");
        refresh.Click += (_, _) => info.Text = Safe();
        panel.Children.Add(info);
        panel.Children.Add(refresh);
        return panel;
    }

    // ---------------- Confirmation for destructive actions ----------------
    private async Task<bool> ConfirmAsync(TweakDefinition op)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Are you sure?", "確定嗎？"),
            Content = $"{op.Title.En}\n{op.Title.Zh}\n\n" +
                      "This action may be hard to undo.\n呢個動作可能難以復原。",
            PrimaryButtonText = P("Proceed", "繼續"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        try { return await dlg.ShowAsync() == ContentDialogResult.Primary; }
        catch { return false; }
    }

    // ---------------- Applied / error status (routes through the persistent OpsStatusBar) ----------------
    private void ShowApplied(TweakDefinition op)
    {
        string en = "Applied.", zh = "已套用。";
        switch (op.Restart)
        {
            case RestartScope.Explorer: en = "Applied. Restart Explorer to see the change."; zh = "已套用。重啟檔案總管就睇到變化。"; break;
            case RestartScope.SignOut: en = "Applied. Sign out and back in to take effect."; zh = "已套用。登出再登入後生效。"; break;
            case RestartScope.Reboot: en = "Applied. Reboot to take effect."; zh = "已套用。重新開機後生效。"; break;
        }
        ShowStatus(P(en, zh), InfoBarSeverity.Success);
    }

    private void ShowError(TweakDefinition op, Exception ex)
    {
        bool needAdmin = op.RequiresAdmin && !AdminHelper.IsElevated;
        ShowStatus(needAdmin
            ? P("This change needs administrator rights.", "呢項更改需要管理員權限。")
            : ex.Message, InfoBarSeverity.Error);
    }

    private void ShowResult(TweakResult r)
        => ShowStatus(Loc.I.IsCantonesePrimary ? (r.Message?.Zh ?? "") : (r.Message?.En ?? ""),
            r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error);

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        OpsStatusBar.IsOpen = true;
        OpsStatusBar.Severity = severity;
        OpsStatusBar.Message = message;
    }
}
