using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Windhawk mod 管理器前端 · Windhawk mod-manager front-end — detect/install the official Windhawk
/// (winget RamenSoftware.Windhawk), launch its UI, open the mods folder, and browse a curated bilingual
/// gallery of popular mods that deep-link into windhawk.net. Bilingual. No WinRT pickers. No redirect for install.
/// </summary>
public sealed partial class WindhawkModule : Page
{
    private List<TweakDefinition>? _mods;

    public WindhawkModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLang;
        Loaded += async (_, _) => { Render(); PopulateMods(string.Empty); await CheckEngine(); };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateMods(ModFilter.Text ?? string.Empty); _ = CheckEngine(); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Windhawk Mods · Windhawk 模組";
        HeaderBlurb.Text = P(
            "Windhawk is an open-source platform that customizes Windows by injecting community 'mods' — taskbar height, clock tweaks, Start-menu styling and much more. Install it, launch it, and browse a curated gallery below.",
            "Windhawk 係一個開源平台，透過注入社群「mod」嚟自訂 Windows — 工作列高度、時鐘調校、開始功能表美化等等。喺下面安裝、啟動，再瀏覽精選 mod。");

        _mods ??= WindhawkMods.All();
        GalleryHeader.Text = P($"Popular mods ({_mods.Count})", $"熱門 mod（{_mods.Count}）");
        GalleryHint.Text = P(
            "Each card opens the mod's page on windhawk.net. Click \"Open in Windhawk\" then install/configure it inside the Windhawk app — programmatic toggling is intentionally left to Windhawk itself.",
            "每張卡會開啟 windhawk.net 上嗰個 mod 嘅頁面。撳「喺 Windhawk 開」之後喺 Windhawk app 內安裝／設定 — 啟用／停用刻意交返畀 Windhawk 本身。");
        ModFilter.PlaceholderText = P("Filter mods…", "篩選 mod…");

        AboutHeader.Text = P("How it works & where files live", "運作原理同檔案位置");
        AboutBody.Text = P(
            "Windhawk runs an elevated service that compiles mods and injects them into target processes (explorer.exe, the taskbar, etc.). Installing it via winget may prompt UAC. Mod settings live under %ProgramData%\\Windhawk\\Engine. WinForge installs and launches the official binary and deep-links to its catalog — it does not fork or bundle Windhawk (GPL-3.0).",
            "Windhawk 會行一個提權服務，編譯 mod 並注入目標程序（explorer.exe、工作列等）。經 winget 安裝時可能彈 UAC。Mod 設定喺 %ProgramData%\\Windhawk\\Engine。WinForge 只係安裝同啟動官方版本並深層連結到佢嘅目錄 — 並無 fork 或內嵌 Windhawk（GPL-3.0）。");

        LaunchBtn.Content = P("Launch Windhawk", "啟動 Windhawk");
        ModsFolderBtn.Content = P("Open mods folder", "開啟 mod 資料夾");
        SiteBtn.Content = P("Browse all mods (windhawk.net)", "瀏覽全部 mod（windhawk.net）");
        RenderStatus();
    }

    private void RenderStatus()
    {
        bool installed = WindhawkService.IsInstalled();
        if (installed)
        {
            var ver = WindhawkService.Version();
            StatusText.Text = ver is null
                ? P("Windhawk is installed.", "Windhawk 已安裝。")
                : P($"Windhawk is installed (version {ver}).", $"Windhawk 已安裝（版本 {ver}）。");
        }
        else
        {
            StatusText.Text = P("Windhawk is not installed yet — install it from the bar above.",
                "尚未安裝 Windhawk — 喺上方安裝列安裝。");
        }
        LaunchBtn.IsEnabled = installed;
        ModsFolderBtn.IsEnabled = WindhawkService.EngineFolder() is not null;
    }

    private async Task CheckEngine()
    {
        bool ok = await WindhawkService.IsInstalledAsync();
        RenderStatus();
        if (ok) { EngineBar.IsOpen = false; EngineBar.ActionButton = null; return; }
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Windhawk not found", "搵唔到 Windhawk");
        EngineBar.Message = P(
            "Click to install Windhawk automatically (winget). It installs an elevated service, so Windows may prompt for UAC.",
            "撳一下自動安裝 Windhawk（winget）。佢會安裝一個提權服務，所以 Windows 可能會彈 UAC。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            WindhawkService.WingetId, "Install Windhawk automatically", "自動安裝 Windhawk",
            async () => { await CheckEngine(); }, WindhawkService.Rescan);
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
        => Report(WindhawkService.Launch());

    private void ModsFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = WindhawkService.EngineFolder();
        if (folder is null)
        {
            Report(TweakResult.Fail(
                "Windhawk's engine folder was not found — launch Windhawk once so it creates it.",
                "搵唔到 Windhawk 引擎資料夾 — 先啟動一次 Windhawk 等佢建立。"));
            return;
        }
        Report(WindhawkService.OpenFolder(folder));
    }

    private void Site_Click(object sender, RoutedEventArgs e)
        => Report(WindhawkService.OpenUrl(WindhawkService.Homepage));

    private void Report(TweakResult r)
    {
        ActionBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        ActionBar.Title = r.Success ? P("Done", "完成") : P("Failed", "失敗");
        ActionBar.Message = r.Message is null ? string.Empty : $"{r.Message.En}\n{r.Message.Zh}";
        ActionBar.IsOpen = true;
    }

    private void ModFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateMods(sender.Text ?? string.Empty);
    }

    private void PopulateMods(string filter)
    {
        _mods ??= WindhawkMods.All();
        ModsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _mods;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _mods.Where(t => t.SearchHaystack.Contains(f));
        }
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            ModsPanel.Children.Add(card);
        }
    }
}
