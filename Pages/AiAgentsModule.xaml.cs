using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// AI 代理 · Terminal AI coding agents — install, configure and launch Claude Code, OpenAI Codex,
/// opencode, Pi, OpenClaw and Hermes Agent, all from inside WinForge. Bilingual.
/// </summary>
public sealed partial class AiAgentsModule : Page
{
    private readonly CakeFileService _cakeFiles = new();

    public AiAgentsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); _ = BuildCards(); };
        Loaded += async (_, _) => { Render(); WorkDirBox.Text = DisplayPath(DefaultWorkDir()); await CheckNode(); await BuildCards(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static string DefaultWorkDir()
    {
        try { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } catch { return ""; }
    }

    private static string DisplayPath(string path)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
            if (!string.IsNullOrWhiteSpace(home) &&
                path.StartsWith(home, StringComparison.OrdinalIgnoreCase))
                return "%USERPROFILE%" + path[home.Length..];
        }
        catch { }
        return path;
    }

    private static string ExpandDisplayPath(string path)
    {
        if (path.StartsWith("%USERPROFILE%", StringComparison.OrdinalIgnoreCase))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd('\\');
            return home + path["%USERPROFILE%".Length..];
        }
        return Environment.ExpandEnvironmentVariables(path);
    }

    private void Render()
    {
        Header.Title = "AI Agents · AI 代理";
        HeaderBlurb.Text = P(
            "Install, configure and launch terminal AI coding agents — one click each. Most install via npm (Node.js); some via an official installer.",
            "一鍵安裝、設定同啟動終端機 AI 編程代理。大部分用 npm（Node.js）安裝，部分用官方安裝器。");
        CreditTitle.Text = P("Cake generation credits", "蛋糕生成額度");
        CreditRuleText.Text = P(
            "AI Agents, Communication AI and Ollama share this wallet. 1 packed cake deposits 1,000,000 generated units; in-app generations spend credits on usage.",
            "AI 代理、通訊 AI 同 Ollama 共用呢個錢包。1 個已包裝蛋糕會存入 1,000,000 個生成單位；App 內生成會按用量扣額。");
        FeedCreditBtn.Content = P("Feed cake", "餵蛋糕");
        YoloTitle.Text = P("Cake-gated YOLO mode", "蛋糕閘門 YOLO 模式");
        YoloText.Text = P(
            "Consumes one packed cake, backs up known agent config files, then writes best-effort permissive local settings for Claude Code, Codex, opencode, Pi and other configured agents.",
            "會消耗一個已包裝蛋糕、備份已知 agent 設定檔，然後為 Claude Code、Codex、opencode、Pi 同其他已設定 agent 寫入盡量寬鬆嘅本機設定。");
        YoloBtn.Content = P("Feed chocolate cake + enable", "餵朱古力蛋糕 + 啟用");
        WorkDirLabel.Text = P("Launch in folder", "啟動目錄");
        WorkDirBtn.Content = P("Browse…", "瀏覽…");
        RefreshCreditStatus();
    }

    private async Task CheckNode()
    {
        bool node = await AiAgentService.NodeAvailableAsync();
        if (node) { NodeBar.IsOpen = false; NodeBar.ActionButton = null; return; }
        NodeBar.IsOpen = true;
        NodeBar.Severity = InfoBarSeverity.Warning;
        NodeBar.Title = P("Node.js not found", "搵唔到 Node.js");
        NodeBar.Message = P("npm-based agents (Claude, Codex, opencode, Pi, OpenClaw) need Node.js. Install it once, then install any agent.",
            "用 npm 嘅代理（Claude、Codex、opencode、Pi、OpenClaw）需要 Node.js。裝一次之後就可以裝任何代理。");
        NodeBar.ActionButton = EngineBars.AutoInstallButton(
            "OpenJS.NodeJS.LTS", "Install Node.js automatically", "自動安裝 Node.js",
            async () => { await CheckNode(); }, null);
    }

    private async void WorkDir_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync();
        if (folder is not null) WorkDirBox.Text = DisplayPath(folder);
    }

    private async Task BuildCards()
    {
        Cards.Children.Clear();
        foreach (var agent in AiAgentService.All)
        {
            bool installed = false;
            try { installed = await AiAgentService.IsInstalledAsync(agent); } catch { }
            Cards.Children.Add(BuildCard(agent, installed));
        }
    }

    private Border BuildCard(AiAgent agent, bool installed)
    {
        var panel = new StackPanel { Spacing = 8 };

        // Title + status
        var titleRow = new Grid { ColumnSpacing = 10 };
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleText = new StackPanel { Spacing = 1 };
        titleText.Children.Add(new TextBlock { Text = agent.Name, FontWeight = FontWeights.SemiBold, FontSize = 15 });
        titleText.Children.Add(new TextBlock
        {
            Text = agent.Desc, FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        Grid.SetColumn(titleText, 0);
        titleRow.Children.Add(titleText);
        var status = new TextBlock
        {
            Text = installed ? P("Installed", "已安裝") : P("Not installed", "未安裝"),
            VerticalAlignment = VerticalAlignment.Center, FontSize = 12,
            Foreground = (Brush)Application.Current.Resources[installed ? "SystemFillColorSuccessBrush" : "TextFillColorTertiaryBrush"],
        };
        Grid.SetColumn(status, 1);
        titleRow.Children.Add(status);
        panel.Children.Add(titleRow);

        panel.Children.Add(new TextBlock
        {
            Text = agent.Cli, FontSize = 11, FontFamily = new FontFamily("Consolas"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });

        // Actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

        var launch = new Button { Content = P("Launch", "啟動"), IsEnabled = installed };
        launch.Click += (_, _) =>
        {
            if (!EnsureLaunchCredits(agent))
                return;

            var dir = string.IsNullOrWhiteSpace(WorkDirBox.Text) ? null : ExpandDisplayPath(WorkDirBox.Text);
            var r = AiAgentService.Launch(agent, dir);
            ShowResult(r.Success, r);
            RefreshCreditStatus();
        };
        actions.Children.Add(launch);

        foreach (var method in agent.InstallMethods)
        {
            var m = method;
            var btn = new Button { Content = $"{P("Install", "安裝")} ({m.Label})" };
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false; var lbl = btn.Content; btn.Content = P("Installing…", "安裝緊…");
                try
                {
                    var r = await m.Run(CancellationToken.None);
                    ShowResult(r.Success, r);
                    if (r.Success) await BuildCards();
                }
                catch (Exception ex) { ResultBar.IsOpen = true; ResultBar.Severity = InfoBarSeverity.Error; ResultBar.Title = P("Failed", "失敗"); ResultBar.Message = ex.Message; }
                finally { btn.Content = lbl; btn.IsEnabled = true; }
            };
            actions.Children.Add(btn);
        }

        var docs = new Button { Content = P("Copy docs URL", "複製文件網址") };
        docs.Click += (_, _) => CopyText(agent.DocsUrl);
        actions.Children.Add(docs);
        panel.Children.Add(actions);

        // Config editor expander
        if (agent.ConfigFiles.Count > 0)
            panel.Children.Add(BuildConfigExpander(agent));

        // API key row
        if (!string.IsNullOrEmpty(agent.EnvKey))
        {
            var keyRow = new Grid { ColumnSpacing = 8 };
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var pwd = new PasswordBox { PlaceholderText = $"{agent.EnvKey}…" };
            try { var cur = AiAgentService.GetEnvKey(agent); if (!string.IsNullOrEmpty(cur)) pwd.Password = cur; } catch { }
            Grid.SetColumn(pwd, 0);
            keyRow.Children.Add(pwd);
            var save = new Button { Content = P("Save key", "儲存金鑰") };
            save.Click += (_, _) =>
            {
                try { AiAgentService.SetEnvKey(agent, pwd.Password); ShowOk(P("Saved API key.", "已儲存 API 金鑰。")); }
                catch (Exception ex) { ResultBar.IsOpen = true; ResultBar.Severity = InfoBarSeverity.Error; ResultBar.Title = P("Failed", "失敗"); ResultBar.Message = ex.Message; }
            };
            Grid.SetColumn(save, 1);
            keyRow.Children.Add(save);
            panel.Children.Add(keyRow);
        }

        return new Border
        {
            Padding = new Thickness(16, 14, 16, 14),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Child = panel,
        };
    }

    /// <summary>
    /// 為一個代理建立「設定」展開區 · Build the per-agent "Config" expander.
    /// 每個已知設定檔一個分段；提供 Load／Save／Open folder／Browse 同 JSON 驗證。
    /// One segment per known config file; Load / Save / Open folder / Browse plus JSON validation.
    /// </summary>
    private Expander BuildConfigExpander(AiAgent agent)
    {
        var body = new StackPanel { Spacing = 10 };

        // 編輯緊時警告 · Info note: editing while the agent runs.
        var note = new InfoBar
        {
            IsOpen = true,
            IsClosable = false,
            Severity = InfoBarSeverity.Informational,
            Title = P("Heads up", "提提你"),
            Message = P(
                "Edits are saved verbatim. If the agent is running it may overwrite this file. Text is the source of truth — fields are convenience helpers only.",
                "改動會原樣儲存。如果代理正在執行，佢可能會覆寫呢個檔案。文字先係準則 — 欄位只係方便用嘅輔助。"),
        };
        body.Children.Add(note);

        // 檔案選擇分段 · File picker segment.
        var picker = new ComboBox { MinWidth = 220 };
        foreach (var f in agent.ConfigFiles)
            picker.Items.Add(new ComboBoxItem { Content = f.Label, Tag = f });
        picker.SelectedIndex = 0;

        var pathText = new TextBlock
        {
            FontSize = 11, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        };

        var pickerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        pickerRow.Children.Add(new TextBlock { Text = P("File", "檔案"), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
        pickerRow.Children.Add(picker);
        body.Children.Add(pickerRow);
        body.Children.Add(pathText);

        // 編輯器 · The monospace editor.
        var editor = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 13,
            MinHeight = 220,
            MaxHeight = 380,
            Height = 260,
            IsSpellCheckEnabled = false,
            PlaceholderText = P("Load a file or start typing to create it…", "載入檔案或者開始打字嚟建立佢…"),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(editor, ScrollBarVisibility.Auto);
        body.Children.Add(editor);

        // 狀態 · Status bar for this editor.
        var statusBar = new InfoBar { IsOpen = false, IsClosable = true };
        body.Children.Add(statusBar);

        // 動作按鈕 · Action buttons.
        var loadBtn = new Button { Content = P("Load current", "載入目前") };
        var saveBtn = new Button { Content = P("Save", "儲存"), Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var openBtn = new Button { Content = P("Open folder", "開啟資料夾") };
        var browseBtn = new Button { Content = P("Browse…", "瀏覽…") };

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        btnRow.Children.Add(loadBtn);
        btnRow.Children.Add(saveBtn);
        btnRow.Children.Add(openBtn);
        btnRow.Children.Add(browseBtn);
        body.Children.Add(btnRow);

        // 目前選中嘅檔案同覆寫路徑（Browse 後）· Currently selected file + optional browsed override path.
        AiConfigFile Current() => (AiConfigFile)((ComboBoxItem)picker.SelectedItem).Tag;
        string? browsedPath = null; // 若使用者用 Browse 揀咗檔，覆寫解析路徑 · overrides resolved path if set

        string? EffectivePath()
        {
            return browsedPath ?? AiAgentConfigService.Resolve(Current());
        }

        void RefreshPathText()
        {
            var p = EffectivePath();
            if (p is null)
            {
                pathText.Text = P("Path could not be resolved — use Browse…", "無法解析路徑 — 請用瀏覽…");
                return;
            }
            bool exists = false;
            try { exists = System.IO.File.Exists(p); } catch { }
            var tag = exists ? P("(exists)", "（已存在）") : P("(not created yet)", "（尚未建立）");
            pathText.Text = $"{p}  {tag}";
        }

        void ShowEditorStatus(bool ok, string title, string msg)
        {
            statusBar.IsOpen = true;
            statusBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
            statusBar.Title = title;
            statusBar.Message = msg;
        }

        void DoLoad()
        {
            var p = EffectivePath();
            var res = browsedPath is not null
                ? AiAgentConfigService.ReadPath(browsedPath)
                : AiAgentConfigService.Read(Current());
            if (res.ok)
            {
                editor.Text = res.text;
                ShowEditorStatus(true, P("Loaded", "已載入"), p ?? "");
            }
            else
            {
                editor.Text = "";
                ShowEditorStatus(false, P("Not created yet", "尚未建立"),
                    P("This file does not exist yet. Type contents and Save to create it.",
                      "呢個檔案仲未存在。打入內容然後撳儲存就會建立佢。"));
            }
            RefreshPathText();
        }

        void DoSave()
        {
            var file = Current();
            // JSON 驗證（儲存前提示，非阻擋）· Validate JSON before overwrite (warn, non-blocking is N/A — we block on confirm).
            if (file.Kind == AiConfigKind.Json)
            {
                var (valid, err) = AiAgentConfigService.ValidateJson(editor.Text);
                if (!valid)
                {
                    ShowEditorStatus(false, P("Invalid JSON — not saved", "JSON 無效 — 未儲存"),
                        P($"Fix the JSON and try again. {err}", $"修正 JSON 再試。{err}"));
                    return;
                }
            }
            var r = browsedPath is not null
                ? AiAgentConfigService.SavePath(browsedPath, editor.Text)
                : AiAgentConfigService.Save(file, editor.Text);
            ShowEditorStatus(r.Success,
                r.Success ? P("Saved", "已儲存") : P("Failed", "失敗"),
                (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");
            RefreshPathText();
        }

        loadBtn.Click += (_, _) => DoLoad();
        saveBtn.Click += (_, _) => DoSave();
        openBtn.Click += (_, _) =>
        {
            var r = browsedPath is not null
                ? AiAgentConfigService.OpenFolderPath(browsedPath)
                : AiAgentConfigService.OpenFolder(Current());
            if (!r.Success)
                ShowEditorStatus(false, P("Failed", "失敗"),
                    (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "");
        };
        browseBtn.Click += async (_, _) =>
        {
            var file = Current();
            var ext = file.Kind switch
            {
                AiConfigKind.Json => ".json",
                AiConfigKind.Toml => ".toml",
                AiConfigKind.Markdown => ".md",
                _ => ".txt",
            };
            var picked = await FileDialogs.OpenFileAsync(ext);
            if (picked is not null)
            {
                browsedPath = picked;
                RefreshPathText();
                DoLoad();
            }
        };

        picker.SelectionChanged += (_, _) =>
        {
            browsedPath = null; // 換檔就清除 Browse 覆寫 · clear any browse override on file change
            editor.Text = "";
            statusBar.IsOpen = false;
            RefreshPathText();
        };

        RefreshPathText();

        return new Expander
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Header = P("Config · 設定", "設定 · Config"),
            Content = body,
        };
    }

    private void ShowOk(string msg)
    {
        ResultBar.IsOpen = true; ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成"); ResultBar.Message = msg;
    }

    private bool EnsureLaunchCredits(AiAgent agent)
    {
        var snap = CakeCreditService.I.Snapshot;
        if (snap.BalanceUnits > 0)
            return true;

        var feed = CakeCreditService.I.FeedOneCake(
            $"{agent.NameEn} launch credits",
            $"{agent.NameZh} 啟動額度");
        RefreshCreditStatus();
        if (feed.Success)
        {
            ResultBar.IsOpen = true;
            ResultBar.Severity = InfoBarSeverity.Informational;
            ResultBar.Title = P("Cake fed", "已餵蛋糕");
            ResultBar.Message = P(
                $"{feed.Message.En} External terminal usage cannot be metered after launch; in-app generations still spend credits on output.",
                $"{feed.Message.Zh} 啟動外部終端機之後嘅用量無法由 WinForge 逐 token 計量；App 內生成仍然會按輸出扣額。");
            return true;
        }

        ResultBar.IsOpen = true;
        ResultBar.Severity = InfoBarSeverity.Warning;
        ResultBar.Title = P("Cake credits required", "需要蛋糕額度");
        ResultBar.Message = feed.Message.Primary;
        return false;
    }

    private async void FeedCredit_Click(object sender, RoutedEventArgs e)
    {
        var ok = await ShowCakeTransactionAsync(
            P("Feed cake credits", "餵入蛋糕額度"),
            P("Drop or bake a signed .cake file, then feed exactly one cake into the AI credit wallet.",
                "拖入或者焗好一個已簽署 .cake 檔，然後餵入剛好一個蛋糕做 AI 額度。"),
            "AI generation credits",
            "AI 生成額度");
        if (!ok) return;

        RefreshCreditStatus();
    }

    private async void Yolo_Click(object sender, RoutedEventArgs e)
    {
        var ok = await ShowCakeTransactionAsync(
            P("YOLO mode cake transaction", "YOLO 模式蛋糕交易"),
            P("This waits for a signed cake file, consumes one cake, then writes permissive best-effort agent configs with backups.",
                "呢度會等已簽署蛋糕檔，消耗一個蛋糕，然後備份並寫入寬鬆嘅代理設定。"),
            "AI agent YOLO mode",
            "AI agent YOLO 模式");
        if (!ok) return;

        var r = AiAgentService.EnableCakeGatedYoloMode(consumeCake: false);
        RefreshCreditStatus();
        ResultBar.IsOpen = true;
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = r.Success ? P("YOLO mode config written", "YOLO 模式設定已寫入") : P("YOLO mode failed", "YOLO 模式失敗");
        ResultBar.Message = Loc.I.IsCantonesePrimary ? r.ReportZh : r.ReportEn;
    }

    private async Task<bool> ShowCakeTransactionAsync(string title, string body, string reasonEn, string reasonZh)
    {
        var status = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        var openFolder = new Button
        {
            Content = P("Open cakes folder", "開蛋糕資料夾"),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        openFolder.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = _cakeFiles.CakeDir, UseShellExecute = true }); }
            catch { }
        };

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(status);
        panel.Children.Add(openFolder);

        var dlg = new ContentDialog
        {
            Title = title,
            Content = panel,
            PrimaryButtonText = P("Feed one cake", "餵一個蛋糕"),
            SecondaryButtonText = P("Refresh", "重新整理"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        void Refresh()
        {
            var snap = CakeCreditService.I.Snapshot;
            status.Text = P(
                $"Ready cakes: {snap.CakeFilesAvailable}. Current balance: {CakeCreditService.FormatUnits(snap.BalanceUnits)}. Add .cake files to: {_cakeFiles.CakeDir}",
                $"可用蛋糕：{snap.CakeFilesAvailable}。目前餘額：{CakeCreditService.FormatUnits(snap.BalanceUnits)}。請將 .cake 檔放入：{_cakeFiles.CakeDir}");
            dlg.IsPrimaryButtonEnabled = snap.CakeFilesAvailable > 0;
        }

        dlg.Opened += (_, _) => Refresh();
        dlg.SecondaryButtonClick += (s, args) =>
        {
            args.Cancel = true;
            Refresh();
        };
        dlg.PrimaryButtonClick += (s, args) =>
        {
            var deferral = args.GetDeferral();
            try
            {
                var r = CakeCreditService.I.FeedOneCake(reasonEn, reasonZh);
                RefreshCreditStatus();
                if (!r.Success)
                {
                    args.Cancel = true;
                    Refresh();
                    ResultBar.IsOpen = true;
                    ResultBar.Severity = InfoBarSeverity.Warning;
                    ResultBar.Title = P("Cake required", "需要蛋糕");
                    ResultBar.Message = r.Message.Primary;
                    return;
                }

                ResultBar.IsOpen = true;
                ResultBar.Severity = InfoBarSeverity.Success;
                ResultBar.Title = P("Cake fed", "已餵蛋糕");
                ResultBar.Message = r.Message.Primary;
            }
            finally
            {
                deferral.Complete();
            }
        };

        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private void FeedCredit_Click_Legacy(object sender, RoutedEventArgs e)
    {
        var r = CakeCreditService.I.FeedOneCake(
            "AI generation credits",
            "AI 生成額度");
        RefreshCreditStatus();
        ResultBar.IsOpen = true;
        ResultBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = r.Success ? P("Cake fed", "已餵蛋糕") : P("No cake available", "無可用蛋糕");
        ResultBar.Message = r.Message.Primary;
    }

    private void RefreshCreditStatus()
    {
        var s = CakeCreditService.I.Snapshot;
        CreditBalanceText.Text = P(
            $"Balance: {CakeCreditService.FormatUnits(s.BalanceUnits)} · packed cakes ready: {s.CakeFilesAvailable} · spent: {CakeCreditService.FormatUnits(s.LifetimeSpentUnits)}",
            $"餘額：{CakeCreditService.FormatUnits(s.BalanceUnits)} · 可用已包裝蛋糕：{s.CakeFilesAvailable} · 已使用：{CakeCreditService.FormatUnits(s.LifetimeSpentUnits)}");
        FeedCreditBtn.IsEnabled = s.CakeFilesAvailable > 0;
    }

    private void ShowResult(bool ok, Models.TweakResult r)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = ok ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? (r.Output ?? "");
    }

    private void CopyText(string text)
    {
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(text ?? "");
        Clipboard.SetContent(dp);
        Clipboard.Flush();
        ResultBar.IsOpen = true;
        ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Copied", "已複製");
        ResultBar.Message = text ?? "";
    }
}
