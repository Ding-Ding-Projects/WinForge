using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 檔案總管右鍵選單整合管理 · Manager for WinForge's native Explorer right-click integration.
///
/// 每個動作一個 TweakCard 開關（即時寫／刪 HKCU 登錄），另加「全部登記／全部移除」同「分組做 WinForge flyout」。
/// One TweakCard toggle per action (writes/removes HKCU keys live), plus master Register-all / Remove-all
/// and a "group under one WinForge flyout" action. 全部介面雙語。All UI bilingual (English + Cantonese).
/// </summary>
public sealed partial class ShellMenuModule : Page
{
    public ShellMenuModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) => { Render(); BuildCards(); UpdateCount(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Explorer Right-Click · 檔案總管右鍵選單";
        HeaderBlurb.Text = P(
            "Add WinForge actions to Windows Explorer's right-click menu. Each toggle writes a per-user registry verb instantly (no admin needed). On Windows 11 these appear under \"Show more options\"; on Windows 10 they appear directly.",
            "將 WinForge 動作加入 Windows 檔案總管嘅右鍵選單。每個開關即時寫入本使用者登錄項目（免管理員）。Windows 11 喺「顯示更多選項」入面、Windows 10 直接顯示。");

        MasterHeader.Text = P("All entries", "全部項目");
        MasterBlurb.Text = P("Turn everything on or off at once, or group all enabled entries under a single \"WinForge\" submenu.",
            "一次過全開或全關，又或者將所有已啟用項目收埋入一個「WinForge」子選單。");
        RegisterAllBtn.Content = P("Register all", "全部登記");
        RemoveAllBtn.Content = P("Remove all", "全部移除");
        GroupedBtn.Content = P("Group as \"WinForge\" submenu", "收埋做「WinForge」子選單");
        RefreshBtn.Content = P("Refresh", "重新整理");

        FilesHeader.Text = P("On files (right-click any file)", "檔案（右鍵任何檔案）");
        FoldersHeader.Text = P("On folders (right-click a folder)", "資料夾（右鍵資料夾）");
        BackgroundHeader.Text = P("On folder background (right-click empty space)", "資料夾空白處（右鍵空白位）");

        FootNote.Text = P(
            "Note: these are classic shell verbs (registered per-user under HKCU\\Software\\Classes). A top-level Windows 11 entry that bypasses \"Show more options\" would require a packaged IExplorerCommand handler, which is not used here.",
            "備註：呢啲係經典 shell verb（登記喺 HKCU\\Software\\Classes，只限本使用者）。要喺 Windows 11 直接頂層顯示（唔使㩒「顯示更多選項」）就要用打包嘅 IExplorerCommand 處理常式，呢度未用。");
    }

    private void BuildCards()
    {
        FilesPanel.Children.Clear();
        FoldersPanel.Children.Clear();
        BackgroundPanel.Children.Clear();

        foreach (var a in ShellContextMenuService.Actions)
        {
            var host = a.Scope switch
            {
                ShellScope.AllFiles => FilesPanel,
                ShellScope.Directory => FoldersPanel,
                _ => BackgroundPanel,
            };
            var card = new TweakCard();
            card.SetTweak(MakeTweak(a));
            host.Children.Add(card);
        }
    }

    /// <summary>把一個 ShellAction 變成一張 Toggle TweakCard 嘅定義 · Build a Toggle TweakDefinition for one action.</summary>
    private TweakDefinition MakeTweak(ShellAction a)
    {
        string scopeEn = a.Scope switch
        {
            ShellScope.AllFiles => "Files",
            ShellScope.Directory => "Folders",
            _ => "Folder background",
        };
        string scopeZh = a.Scope switch
        {
            ShellScope.AllFiles => "檔案",
            ShellScope.Directory => "資料夾",
            _ => "資料夾空白處",
        };

        return new TweakDefinition
        {
            Id = $"shellmenu.{a.Id}",
            Title = new LocalizedText(a.En, a.Zh),
            Description = new LocalizedText(
                $"Show \"{a.En}\" in the right-click menu for {scopeEn.ToLowerInvariant()}. Routes to WinForge and acts on the selected item.",
                $"喺{scopeZh}嘅右鍵選單顯示「{a.Zh}」。會開 WinForge 並對住揀中嘅項目操作。"),
            Kind = TweakKind.Toggle,
            Restart = RestartScope.None,
            GetIsOn = () => ShellContextMenuService.IsRegistered(a),
            SetIsOn = on =>
            {
                ShellContextMenuService.SetRegistered(a, on);
                // Reflect the new total in the header pill.
                DispatcherQueue.TryEnqueue(UpdateCount);
            },
            ColoredStatus = () => ShellContextMenuService.IsRegistered(a)
                ? ("In menu", "已喺選單", StatusColor.Good)
                : ("Not shown", "未顯示", StatusColor.Neutral),
        };
    }

    private void UpdateCount()
    {
        int n = ShellContextMenuService.RegisteredCount();
        int total = ShellContextMenuService.Actions.Count;
        CountText.Text = P($"{n} / {total} active", $"{n} / {total} 啟用中");
    }

    private void RegisterAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellContextMenuService.RegisterAll();
            Show(InfoBarSeverity.Success, P("All entries registered", "已全部登記"),
                P("WinForge actions now appear in the right-click menu.", "WinForge 動作已喺右鍵選單出現。"));
            Refresh();
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void RemoveAll_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ShellContextMenuService.UnregisterAll();
            Show(InfoBarSeverity.Success, P("All entries removed", "已全部移除"),
                P("WinForge actions were removed from the right-click menu.", "已由右鍵選單移除 WinForge 動作。"));
            Refresh();
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Grouped_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (ShellScope s in Enum.GetValues(typeof(ShellScope)))
            {
                bool anyEnabled = ShellContextMenuService.Actions.Any(a => a.Scope == s && ShellContextMenuService.IsRegistered(a));
                if (anyEnabled) ShellContextMenuService.RegisterGrouped(s);
            }
            Show(InfoBarSeverity.Success, P("Grouped", "已收埋"),
                P("Enabled entries are now under a single \"WinForge\" submenu. Restart Explorer if it looks stale.",
                  "已啟用嘅項目而家收埋入一個「WinForge」子選單。如果顯示未更新，重啟檔案總管。"));
            Refresh();
        }
        catch (Exception ex) { Show(InfoBarSeverity.Error, P("Failed", "失敗"), ex.Message); }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();

    private void Refresh()
    {
        BuildCards();
        UpdateCount();
    }

    private void Show(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
