using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 7+ Taskbar Tweaker · 工作列調校。WinForge 原生子集（真正登錄機碼／ms-settings 嘅工作列調校），
/// 加上偵測 + 啟動真正嘅 7+TT 同 Windhawk 做深層行為。
///
/// 7+ Taskbar Tweaker module. Renders every real registry / ms-settings taskbar tweak WinForge can natively
/// do (from <see cref="TaskbarTweaks"/>) through <see cref="TweakCard"/>, and detects + launches the real
/// 7+TT and Windhawk for the deep runtime behaviours that genuinely cannot be reimplemented in managed C#.
/// No external redirect beyond launching an already-installed tool; 7+TT is never bundled or auto-installed.
/// </summary>
public sealed partial class TaskbarTweakerModule : Page
{
    private TaskbarTweakerService.Detection _sevenTt = new() { Installed = false };
    private TaskbarTweakerService.Detection _windhawk = new() { Installed = false };

    public TaskbarTweakerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); RefreshDetection(); };
        Loaded += (_, _) =>
        {
            Render();
            Populate(string.Empty);
            RefreshDetection();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Taskbar Tweaker · 工作列調校";
        HeaderBlurb.Text = P(
            "Every taskbar and Start tweak WinForge can do natively, plus a launcher for the real 7+ Taskbar Tweaker and Windhawk for behaviours that need a runtime hook.",
            "WinForge 原生可以做嘅工作列同開始功能表調校，仲有可以啟動真正嘅 7+ Taskbar Tweaker 同 Windhawk，做需要執行時鈎子嘅行為。");

        FilterBox.PlaceholderText = P("Filter taskbar tweaks…", "篩選工作列調校…");

        DeepTitle.Text = P(
            "Deep behaviours need a runtime hook",
            "深層行為需要執行時鈎子");
        DeepBody.Text = P(
            "Middle-click to close, double-click to show desktop, scroll-to-switch and drag-to-reorder all work by injecting a DLL into explorer.exe and rewriting the taskbar's window procedures at runtime. They genuinely cannot be reimplemented in managed C# — use the real 7+ Taskbar Tweaker (closed-source freeware) or Windhawk mods (see handoff 29). WinForge only detects and launches them, it never bundles or auto-installs 7+TT.",
            "中鍵關閉、雙擊顯示桌面、捲動切換、拖放重排，全部都係靠注入 explorer.exe 嘅 DLL 喺執行時改寫工作列嘅視窗程序。呢啲喺 C# 託管程式碼真係做唔到 — 要用真正嘅 7+ Taskbar Tweaker（閉源免費軟件）或者 Windhawk 模組（見 handoff 29）。WinForge 只係偵測同啟動佢哋，永遠唔會綑綁或者自動安裝 7+TT。");

        RestartExplorerBtn.Content = P("Restart Explorer", "重啟檔案總管");
    }

    private void Populate(string filter)
    {
        CardsPanel.Children.Clear();

        var tweaks = TaskbarTweaks.All();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            tweaks = tweaks.Where(t => t.SearchHaystack.Contains(f)).ToList();
        }

        bool any = false;
        foreach (var t in tweaks)
        {
            var card = new TweakCard();
            card.SetTweak(t);
            CardsPanel.Children.Add(card);
            any = true;
        }

        if (!any)
        {
            CardsPanel.Children.Add(new TextBlock
            {
                Text = P("No matches.", "搵唔到。"),
                Opacity = 0.6,
                Margin = new Thickness(4, 12, 0, 0),
            });
        }
    }

    private void FilterBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            Populate(sender.Text);
    }

    // ---------------- Detection / launch ----------------

    private void RefreshDetection()
    {
        _sevenTt = TaskbarTweakerService.Detect7Tt();
        _windhawk = TaskbarTweakerService.DetectWindhawk();
        RenderSevenTtBar();
        RenderWindhawkBar();
        RenderWindhawkInstall();
    }

    private void RenderSevenTtBar()
    {
        if (_sevenTt.Installed && _sevenTt.ExecutablePath is not null)
        {
            SevenTtBar.Severity = InfoBarSeverity.Success;
            SevenTtBar.Title = P("7+ Taskbar Tweaker is installed", "已安裝 7+ Taskbar Tweaker");
            var ver = string.IsNullOrEmpty(_sevenTt.Version) ? "" : $" (v{_sevenTt.Version})";
            SevenTtBar.Message = P(
                $"Launch it for the deep runtime behaviours{ver}.",
                $"可以啟動佢嚟做深層執行時行為{ver}。");
            var btn = new Button { Content = P("Launch 7+TT", "啟動 7+TT") };
            btn.Click += (_, _) => TaskbarTweakerService.Launch(_sevenTt.ExecutablePath);
            SevenTtBar.ActionButton = btn;
            SevenTtBar.IsOpen = true;
        }
        else
        {
            SevenTtBar.Severity = InfoBarSeverity.Informational;
            SevenTtBar.Title = P("7+ Taskbar Tweaker not detected", "未偵測到 7+ Taskbar Tweaker");
            SevenTtBar.Message = P(
                "7+TT is closed-source freeware with no winget package — WinForge will not download or install it. Install it yourself if you want the deep behaviours; this page will then offer to launch it.",
                "7+TT 係閉源免費軟件，無 winget 套件 — WinForge 唔會下載或者安裝佢。想要深層行為就自己裝，裝咗之後呢頁就會畀你啟動。");
            SevenTtBar.ActionButton = null;
            SevenTtBar.IsOpen = true;
        }
    }

    private void RenderWindhawkBar()
    {
        if (_windhawk.Installed && _windhawk.ExecutablePath is not null)
        {
            WindhawkBar.Severity = InfoBarSeverity.Success;
            WindhawkBar.Title = P("Windhawk is installed", "已安裝 Windhawk");
            var ver = string.IsNullOrEmpty(_windhawk.Version) ? "" : $" (v{_windhawk.Version})";
            WindhawkBar.Message = P(
                $"Open Windhawk to install taskbar mods for the deep behaviours{ver}.",
                $"開啟 Windhawk 嚟裝工作列模組做深層行為{ver}。");
            var btn = new Button { Content = P("Launch Windhawk", "啟動 Windhawk") };
            btn.Click += (_, _) => TaskbarTweakerService.Launch(_windhawk.ExecutablePath);
            WindhawkBar.ActionButton = btn;
            WindhawkBar.IsOpen = true;
        }
        else
        {
            WindhawkBar.Severity = InfoBarSeverity.Informational;
            WindhawkBar.Title = P("Windhawk not detected", "未偵測到 Windhawk");
            WindhawkBar.Message = P(
                "Windhawk is a maintained, open mod platform that can replicate 7+TT's deep behaviours. Install it below to get started.",
                "Windhawk 係仍有維護嘅開放模組平台，可以重現 7+TT 嘅深層行為。喺下面安裝就可以開始。");
            WindhawkBar.ActionButton = null;
            WindhawkBar.IsOpen = true;
        }
    }

    private void RenderWindhawkInstall()
    {
        // Offer a one-click Windhawk install (winget) only when it is NOT already present.
        // 7+TT is deliberately never offered for auto-install (closed-source freeware, no winget id).
        if (_windhawk.Installed)
        {
            WindhawkInstallHost.Visibility = Visibility.Collapsed;
            return;
        }

        WindhawkInstallHost.Content = P("Install Windhawk", "安裝 Windhawk");
        WindhawkInstallHost.IsEnabled = true;
        WindhawkInstallHost.Visibility = Visibility.Visible;
        WindhawkInstallHost.Click -= WindhawkInstall_Click;
        WindhawkInstallHost.Click += WindhawkInstall_Click;
    }

    private async void WindhawkInstall_Click(object sender, RoutedEventArgs e)
    {
        var b = (Button)sender;
        b.IsEnabled = false;
        b.Content = P("Installing…", "安裝緊…");
        bool ok;
        try { ok = await PackageService.AutoInstall("RamenSoftware.Windhawk"); }
        catch { ok = false; }
        if (ok)
        {
            b.Content = P("Installed ✓", "已安裝 ✓");
            RefreshDetection();
        }
        else
        {
            b.Content = P("Install failed — retry", "安裝失敗 — 再試");
            b.IsEnabled = true;
        }
    }

    private async void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var b = (Button)sender;
        b.IsEnabled = false;
        await ShellRunner.RunCmd("taskkill /f /im explorer.exe & start explorer.exe");
        b.IsEnabled = true;
    }
}
