using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// AI 代理 · Terminal AI coding agents — install, configure and launch Claude Code, OpenAI Codex,
/// opencode, Pi, OpenClaw and Hermes Agent, all from inside WinTune. Bilingual.
/// </summary>
public sealed partial class AiAgentsModule : Page
{
    public AiAgentsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); _ = BuildCards(); };
        Loaded += async (_, _) => { Render(); WorkDirBox.Text = DefaultWorkDir(); await CheckNode(); await BuildCards(); };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private static string DefaultWorkDir()
    {
        try { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); } catch { return ""; }
    }

    private void Render()
    {
        HeaderTitle.Text = "AI Agents · AI 代理";
        HeaderBlurb.Text = P(
            "Install, configure and launch terminal AI coding agents — one click each. Most install via npm (Node.js); some via an official installer.",
            "一鍵安裝、設定同啟動終端機 AI 編程代理。大部分用 npm（Node.js）安裝，部分用官方安裝器。");
        WorkDirLabel.Text = P("Launch in folder", "啟動目錄");
        WorkDirBtn.Content = P("Browse…", "瀏覽…");
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
        if (folder is not null) WorkDirBox.Text = folder;
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
            var dir = string.IsNullOrWhiteSpace(WorkDirBox.Text) ? null : WorkDirBox.Text;
            var r = AiAgentService.Launch(agent, dir);
            ShowResult(r.Success, r);
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

        var docs = new HyperlinkButton { Content = P("Docs", "文件"), NavigateUri = SafeUri(agent.DocsUrl) };
        actions.Children.Add(docs);
        panel.Children.Add(actions);

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

    private static Uri? SafeUri(string url)
    {
        try { return new Uri(url); } catch { return null; }
    }

    private void ShowOk(string msg)
    {
        ResultBar.IsOpen = true; ResultBar.Severity = InfoBarSeverity.Success;
        ResultBar.Title = P("Done", "完成"); ResultBar.Message = msg;
    }

    private void ShowResult(bool ok, Models.TweakResult r)
    {
        ResultBar.IsOpen = true;
        ResultBar.Severity = ok ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ResultBar.Title = ok ? P("Done", "完成") : P("Failed", "失敗");
        ResultBar.Message = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? (r.Output ?? "");
    }
}
