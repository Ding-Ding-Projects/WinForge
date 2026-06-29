using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 「搵唔到指令」管理頁 · The Command Not Found management page — a native clone of PowerToys'
/// Command Not Found. 偵測 PowerShell 7、官方 <c>Microsoft.WinGet.CommandNotFound</c> 模組同
/// <c>$PROFILE</c> 掛鈎狀態；提供安裝／更新模組、安全咁開／關掛鈎（自動備份）、測試一個唔存在嘅
/// 指令睇建議，同埋一個 app 內嘅「邊個 winget 套件提供 X？」查詢。Bilingual throughout.
///
/// Detects PowerShell 7, the official module and the profile-hook state; installs/updates the
/// module, enables/disables the hook safely (with backup), tests a missing command to show the
/// suggestion, and offers an in-app "which winget package provides X?" lookup.
/// </summary>
public sealed partial class CmdNotFoundModule : Page
{
    private CmdNotFoundService.CnfStatus? _status;
    private CancellationTokenSource? _cts;
    private bool _busy;

    public CmdNotFoundModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) => { RenderText(); await ReloadAsync(); };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            _cts?.Cancel();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        RenderText();
        if (_status is not null) RenderStatus(_status);
    }

    // ── static text · 靜態文字 ─────────────────────────────────────────────────

    private void RenderText()
    {
        Header.Title = "Command Not Found · 搵唔到指令";
        HeaderBlurb.Text = P(
            "When a command isn't found in PowerShell 7, suggest the winget package that provides it — a native clone of PowerToys' Command Not Found. WinForge wires the suggestion module into your PowerShell profile safely (with a backup).",
            "當 PowerShell 7 搵唔到某個指令時，建議邊個 winget 套件提供佢——PowerToys「Command Not Found」嘅原生複製版。WinForge 會安全咁（連備份）將建議模組接駁入你嘅 PowerShell profile。");
        RefreshLabel.Text = P("Refresh", "重新整理");

        StatusHeader.Text = P("Status · 狀態", "狀態 · Status");

        TestHeader.Text = P("Test a missing command", "測試一個唔存在嘅指令");
        TestBlurb.Text = P(
            "Runs the command in PowerShell 7 (with your profile loaded) and shows any winget suggestion. Suggestions only appear once the hook is enabled and a fresh session is started.",
            "喺 PowerShell 7（已載入 profile）執行該指令並顯示任何 winget 建議。建議只會喺掛鈎啟用並開新 session 後出現。");
        TestBtnLabel.Text = P("Test", "測試");

        LookupHeader.Text = P("Which winget package provides…? · 邊個 winget 套件提供？", "邊個 winget 套件提供？ · winget lookup");
        LookupBlurb.Text = P(
            "Type a command or tool name and search winget directly — useful inside WinForge without leaving the app.",
            "輸入指令或工具名稱，直接搜尋 winget——喺 WinForge 入面即用，唔使離開 app。");
        LookupInput.PlaceholderText = P("e.g. ffmpeg, gh, kubectl…", "例如 ffmpeg、gh、kubectl…");
        LookupBtnLabel.Text = P("Search", "搜尋");

        ProfileHeader.Text = P("PowerShell profile · PowerShell profile", "PowerShell profile");
        OpenEditorLabel.Text = P("Copy profile path", "複製 profile 路徑");

        ExplainHeader.Text = P("How it works · 運作原理", "運作原理 · How it works");
        ExplainEn.Text =
            "English: PowerShell 7.4+ ships two experimental features (PSFeedbackProvider and PSCommandNotFoundSuggestion). With the official Microsoft.WinGet.CommandNotFound module imported in your profile, typing a command that isn't installed makes PowerShell suggest the winget package that provides it — e.g. typing 'gh' suggests 'winget install GitHub.cli'. WinForge enables the experimental features, installs the module, and adds a small, clearly-marked Import-Module block to your $PROFILE (backing it up first). Disable removes only that block.";
        ExplainZh.Text =
            "廣東話：PowerShell 7.4 或以上內置兩個實驗功能（PSFeedbackProvider 同 PSCommandNotFoundSuggestion）。當 profile 入面載入咗官方 Microsoft.WinGet.CommandNotFound 模組，打一個未安裝嘅指令時，PowerShell 會建議邊個 winget 套件提供佢——例如打「gh」會建議「winget install GitHub.cli」。WinForge 會開啟實驗功能、安裝模組，並喺你嘅 $PROFILE 加入一段有清楚標記嘅 Import-Module（事前先備份）。停用時只會移除嗰段。";
        LearnMoreLink.Content = P("Copy winget docs URL", "複製 winget 文件網址");
    }

    private void LearnMoreLink_Click(object sender, RoutedEventArgs e)
    {
        var url = "https://learn.microsoft.com/windows/package-manager/winget/";
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(url);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
        ShowResult(InfoBarSeverity.Success, P("Copied", "已複製"), url);
    }

    // ── reload / render · 重新載入與繪製 ────────────────────────────────────────

    private async Task ReloadAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SetBusy(true, P("Detecting PowerShell 7, the module and the profile hook…",
            "正在偵測 PowerShell 7、模組同 profile 掛鈎…"));
        try
        {
            var status = await CmdNotFoundService.GetStatusAsync(ct);
            if (ct.IsCancellationRequested) return;
            _status = status;
            RenderStatus(status);
            await LoadProfileTextAsync(status, ct);
            ResultBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, P($"Detection failed: {ex.Message}", $"偵測失敗：{ex.Message}"));
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RenderStatus(CmdNotFoundService.CnfStatus s)
    {
        // pwsh row
        if (!s.PwshPresent)
        {
            PwshIcon.Glyph = ""; // error circle
            PwshTitle.Text = P("PowerShell 7 (pwsh): not found", "PowerShell 7（pwsh）：搵唔到");
            PwshDetail.Text = P("Install PowerShell 7.4 or newer to use Command Not Found.",
                "請安裝 PowerShell 7.4 或更新版本先可以用「搵唔到指令」。");
            PwshActionBtn.Content = P("Install pwsh…", "安裝 pwsh…");
            PwshActionBtn.Visibility = Visibility.Visible;
        }
        else if (!s.PwshOk)
        {
            PwshIcon.Glyph = ""; // warning
            PwshTitle.Text = P($"PowerShell {s.PwshVersion}: too old", $"PowerShell {s.PwshVersion}：太舊");
            PwshDetail.Text = P("Version 7.4 or newer is required. Please update PowerShell.",
                "需要 7.4 或更新版本，請更新 PowerShell。");
            PwshActionBtn.Content = P("Update pwsh…", "更新 pwsh…");
            PwshActionBtn.Visibility = Visibility.Visible;
        }
        else
        {
            PwshIcon.Glyph = ""; // checkmark
            PwshTitle.Text = P($"PowerShell {s.PwshVersion}: ready", $"PowerShell {s.PwshVersion}：就緒");
            var feats = (s.FeedbackProviderEnabled, s.SuggestionFeatureEnabled) switch
            {
                (true, true) => P("Experimental features enabled.", "實驗功能已啟用。"),
                _ => P("Experimental features will be enabled when you enable the hook.",
                       "啟用掛鈎時會一併開啟實驗功能。"),
            };
            PwshDetail.Text = feats;
            PwshActionBtn.Visibility = Visibility.Collapsed;
        }

        // module row
        if (s.CnfModulePresent)
        {
            ModuleIcon.Glyph = "";
            ModuleTitle.Text = P("Microsoft.WinGet.CommandNotFound: installed", "Microsoft.WinGet.CommandNotFound：已安裝");
            var client = s.ClientModulePresent
                ? (s.ClientModuleUpToDate
                    ? P("WinGet client module is up to date.", "WinGet 客戶端模組已是最新。")
                    : P("WinGet client module should be updated.", "WinGet 客戶端模組建議更新。"))
                : P("WinGet client dependency missing.", "缺少 WinGet 客戶端相依模組。");
            ModuleDetail.Text = client;
            InstallModuleBtn.Visibility = Visibility.Collapsed;
            UpdateModuleBtn.Content = P("Update", "更新");
            UpdateModuleBtn.Visibility = Visibility.Visible;
        }
        else
        {
            ModuleIcon.Glyph = "";
            ModuleTitle.Text = P("Microsoft.WinGet.CommandNotFound: not installed", "Microsoft.WinGet.CommandNotFound：未安裝");
            ModuleDetail.Text = P("Install the suggestion module from the PowerShell Gallery.",
                "由 PowerShell Gallery 安裝建議模組。");
            InstallModuleBtn.Content = P("Install module", "安裝模組");
            InstallModuleBtn.Visibility = Visibility.Visible;
            UpdateModuleBtn.Visibility = Visibility.Collapsed;
        }
        InstallModuleBtn.IsEnabled = s.PwshPresent;
        UpdateModuleBtn.IsEnabled = s.PwshPresent;

        // hook row
        if (s.HookEnabled && !s.LegacyHookPresent)
        {
            HookIcon.Glyph = "";
            HookTitle.Text = P("Profile hook: enabled", "Profile 掛鈎：已啟用");
            HookDetail.Text = P("The Import-Module hook is in your profile. Open a NEW PowerShell 7 window for suggestions.",
                "Import-Module 掛鈎已喺你嘅 profile。開一個新嘅 PowerShell 7 視窗就會見到建議。");
            EnableBtn.Visibility = Visibility.Collapsed;
            DisableBtn.Content = P("Disable", "停用");
            DisableBtn.Visibility = Visibility.Visible;
        }
        else if (s.LegacyHookPresent)
        {
            HookIcon.Glyph = "";
            HookTitle.Text = P("Profile hook: legacy version", "Profile 掛鈎：舊版本");
            HookDetail.Text = P("An older hook was found. Click Enable to upgrade it to the current module.",
                "搵到舊版掛鈎。撳「啟用」升級到現行模組。");
            EnableBtn.Content = P("Enable / Upgrade", "啟用／升級");
            EnableBtn.Visibility = Visibility.Visible;
            DisableBtn.Content = P("Disable", "停用");
            DisableBtn.Visibility = Visibility.Visible;
        }
        else
        {
            HookIcon.Glyph = "";
            HookTitle.Text = P("Profile hook: disabled", "Profile 掛鈎：已停用");
            HookDetail.Text = P("The suggestion hook is not in your profile yet.",
                "建議掛鈎仲未喺你嘅 profile。");
            EnableBtn.Content = P("Enable", "啟用");
            EnableBtn.Visibility = Visibility.Visible;
            DisableBtn.Visibility = Visibility.Collapsed;
        }
        EnableBtn.IsEnabled = s.PwshPresent;
        DisableBtn.IsEnabled = s.PwshPresent;

        ProfilePathText.Text = string.IsNullOrEmpty(s.ProfilePath)
            ? P("(profile path unknown)", "（profile 路徑未知）")
            : DisplayPath(s.ProfilePath);
    }

    private async Task LoadProfileTextAsync(CmdNotFoundService.CnfStatus s, CancellationToken ct)
    {
        try
        {
            var text = await CmdNotFoundService.ReadProfileAsync(s.ProfilePath, ct);
            ProfileText.Text = string.IsNullOrEmpty(text)
                ? P("(profile file is empty or does not exist yet)", "（profile 檔案係空，或者仲未存在）")
                : text;
        }
        catch { ProfileText.Text = ""; }
    }

    // ── handlers · 事件處理 ─────────────────────────────────────────────────────

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync();

    private async void PwshAction_Click(object sender, RoutedEventArgs e)
    {
        // 經 winget 安裝 PowerShell 7 · install PowerShell 7 via winget.
        if (_busy) return;
        SetBusy(true, P("Installing PowerShell 7 via winget…", "正在用 winget 安裝 PowerShell 7…"));
        try
        {
            var r = await ShellRunner.Run("winget",
                "install --id Microsoft.PowerShell --source winget --accept-source-agreements --accept-package-agreements --disable-interactivity",
                false, _cts?.Token ?? default);
            CmdNotFoundService.Rescan();
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                P("PowerShell 7 install finished. Refreshing…", "PowerShell 7 安裝完成，正在重新整理…"),
                r.Output);
        }
        catch (Exception ex)
        {
            ShowResult(InfoBarSeverity.Error, P($"Install failed: {ex.Message}", $"安裝失敗：{ex.Message}"));
        }
        finally
        {
            SetBusy(false);
            await ReloadAsync();
        }
    }

    private async void InstallModule_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true, P("Installing the suggestion module…", "正在安裝建議模組…"));
        try
        {
            var r = await CmdNotFoundService.InstallModuleAsync(_cts?.Token ?? default);
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, ResultText(r), r.Output);
        }
        finally
        {
            SetBusy(false);
            await ReloadAsync();
        }
    }

    private async void UpdateModule_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        SetBusy(true, P("Updating modules…", "正在更新模組…"));
        try
        {
            var r = await CmdNotFoundService.UpdateModuleAsync(_cts?.Token ?? default);
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, ResultText(r), r.Output);
        }
        finally
        {
            SetBusy(false);
            await ReloadAsync();
        }
    }

    private async void Enable_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _status is null) return;
        SetBusy(true, P("Enabling the hook in your profile…", "正在喺你嘅 profile 啟用掛鈎…"));
        try
        {
            // 模組未裝就先裝 · install the module first if missing.
            if (!_status.CnfModulePresent)
            {
                ShowResult(InfoBarSeverity.Informational,
                    P("Installing the module first…", "先安裝模組…"));
                await CmdNotFoundService.InstallModuleAsync(_cts?.Token ?? default);
            }
            var r = await CmdNotFoundService.EnableAsync(_status, _cts?.Token ?? default);
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, ResultText(r), r.Output);
        }
        finally
        {
            SetBusy(false);
            await ReloadAsync();
        }
    }

    private async void Disable_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _status is null) return;
        SetBusy(true, P("Removing the hook from your profile…", "正在從你嘅 profile 移除掛鈎…"));
        try
        {
            var r = await CmdNotFoundService.DisableAsync(_status, _cts?.Token ?? default);
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, ResultText(r), r.Output);
        }
        finally
        {
            SetBusy(false);
            await ReloadAsync();
        }
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var cmd = TestInput.Text?.Trim() ?? "";
        SetBusy(true, P("Running the test in PowerShell 7…", "正在 PowerShell 7 執行測試…"));
        try
        {
            var r = await CmdNotFoundService.TestAsync(cmd, _cts?.Token ?? default);
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, ResultText(r));
            TestOutput.Text = r.Output ?? "";
            TestOutputPanel.Visibility = string.IsNullOrWhiteSpace(r.Output) ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void Lookup_Click(object sender, RoutedEventArgs e) => await DoLookupAsync();

    private async void Lookup_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) await DoLookupAsync();
    }

    private async Task DoLookupAsync()
    {
        if (_busy) return;
        var q = LookupInput.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(q))
        {
            ShowResult(InfoBarSeverity.Warning, P("Type something to search.", "請輸入內容嚟搜尋。"));
            return;
        }
        SetBusy(true, P($"Searching winget for '{q}'…", $"正在 winget 搜尋「{q}」…"));
        try
        {
            var r = await CmdNotFoundService.WingetSearchAsync(q, _cts?.Token ?? default);
            ShowResult(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning, ResultText(r));
            LookupOutput.Text = r.Output ?? "";
            LookupOutputPanel.Visibility = string.IsNullOrWhiteSpace(r.Output) ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void ViewProfile_Click(object sender, RoutedEventArgs e)
    {
        if (_status is null) return;
        await LoadProfileTextAsync(_status, _cts?.Token ?? default);
        ShowResult(InfoBarSeverity.Informational, P("Profile reloaded below.", "下面已重新載入 profile。"));
    }

    private void OpenEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_status is null) return;
        var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
        dp.SetText(_status.ProfilePath);
        Clipboard.SetContent(dp);
        Clipboard.Flush();
        ShowResult(InfoBarSeverity.Success, P("Profile path copied.", "已複製 profile 路徑。"), DisplayPath(_status.ProfilePath));
    }

    // ── small helpers · 小工具 ──────────────────────────────────────────────────

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

    private string ResultText(TweakResult r)
        => r.Message is null ? "" : P(r.Message.En, r.Message.Zh);

    private void SetBusy(bool on, string? message = null)
    {
        _busy = on;
        Busy.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        RefreshBtn.IsEnabled = !on;
        EnableBtn.IsEnabled = !on && (_status?.PwshPresent ?? false);
        DisableBtn.IsEnabled = !on && (_status?.PwshPresent ?? false);
        InstallModuleBtn.IsEnabled = !on && (_status?.PwshPresent ?? false);
        UpdateModuleBtn.IsEnabled = !on && (_status?.PwshPresent ?? false);
        TestBtn.IsEnabled = !on;
        LookupBtn.IsEnabled = !on;
        if (on && message is not null)
            ShowResult(InfoBarSeverity.Informational, message);
    }

    private void ShowResult(InfoBarSeverity severity, string message, string? detail = null)
    {
        ResultBar.Severity = severity;
        ResultBar.Message = string.IsNullOrEmpty(detail) ? message : $"{message}\n{detail}";
        ResultBar.IsOpen = true;
    }
}
