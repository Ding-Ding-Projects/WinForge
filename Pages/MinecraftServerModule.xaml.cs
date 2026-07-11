using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Minecraft 伺服器架設器 · Minecraft server setupper. Four tabs: Server (folder, Paper download, Spigot
/// BuildTools, EULA), Properties (server.properties form + raw editor + memory/start.bat), Console (live
/// server output with a stdin command box), and Plugins (build presets or any git repo from source into
/// plugins/). All in-app, FileDialogs only, no WinRT pickers. Bilingual.
/// </summary>
public sealed partial class MinecraftServerModule : Page
{
    private bool _busy;
    private CancellationTokenSource? _paperCts;
    private CancellationTokenSource? _spigotCts;
    private CancellationTokenSource? _pluginCts;

    public MinecraftServerModule()
    {
        InitializeComponent();
        // Keep the default out of XAML: typed IsOn literals are unreliable here.
        OnlineToggle.IsOn = true;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            Render();
            RefreshEngine();
            LoadPropertiesForm();
            RawBox.Text = MinecraftServerService.ReadPropertiesRaw();
            BuildPresetList();
            RefreshInstalled();
            RefreshRunState();
            EulaToggle.IsOn = MinecraftServerService.IsEulaAccepted();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            try { _paperCts?.Cancel(); } catch { }
            try { _spigotCts?.Cancel(); } catch { }
            try { _pluginCts?.Cancel(); } catch { }
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private static string Msg(TweakResult r) => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Render()
    {
        Header.Title = "Minecraft Server · Minecraft 伺服器";
        HeaderBlurb.Text = P("Stand up a Paper or Spigot server: download/build the jar, accept the EULA, edit server.properties, run it with a live console, and build plugins from git source.",
            "架設 Paper 或 Spigot 伺服器：下載／編譯 jar、接受 EULA、編輯 server.properties、用即時主控台執行，並由 git 原始碼建置外掛。");

        ServerTab.Header = P("Server", "伺服器");
        PropsTab.Header = P("Properties", "設定");
        ConsoleTab.Header = P("Console", "主控台");
        PluginsTab.Header = P("Plugins", "外掛");

        // Server tab
        FolderLbl.Text = P("Server folder", "伺服器資料夾");
        PickFolderBtn.Content = P("Choose…", "揀…");
        OpenFolderBtn.Content = P("Open folder", "開資料夾");

        PaperLbl.Text = P("Paper (download a prebuilt server)", "Paper（下載現成伺服器）");
        PaperBlurb.Text = P("Pick a Minecraft version; WinForge downloads the latest stable Paper build into server.jar via the PaperMC API.",
            "揀一個 Minecraft 版本；WinForge 會經 PaperMC API 下載最新穩定嘅 Paper build 做 server.jar。");
        PaperRefreshBtn.Content = P("Load versions", "載入版本");
        EnsurePaperControl();

        SpigotLbl.Text = P("Spigot (compile from source via BuildTools)", "Spigot（用 BuildTools 由原始碼編譯）");
        SpigotBlurb.Text = P("Spigot is built locally with BuildTools.jar (slow — minutes). Enter a version (e.g. 1.21.4) or 'latest'.",
            "Spigot 用 BuildTools.jar 喺本機建置（慢，要幾分鐘）。輸入版本（例如 1.21.4）或 'latest'。");
        EnsureBuildToolsControl();
        SpigotBuildBtn.Content = P("Build Spigot", "建置 Spigot");
        SpigotCancelBtn.Content = P("Cancel", "取消");

        EulaLbl.Text = P("Minecraft EULA", "Minecraft EULA");
        EulaBlurb.Text = P("You must accept the Minecraft EULA (https://aka.ms/MinecraftEULA) before the server will start. Turning this on writes eula=true.",
            "啟動伺服器前必須接受 Minecraft EULA（https://aka.ms/MinecraftEULA）。開啟呢個會寫入 eula=true。");

        // Properties tab
        PropsLbl.Text = P("server.properties — key fields", "server.properties — 主要欄位");
        PortLbl.Text = P("Server port", "伺服器 port");
        MotdLbl.Text = P("MOTD (message of the day)", "MOTD（每日訊息）");
        GamemodeLbl.Text = P("Game mode", "遊戲模式");
        DifficultyLbl.Text = P("Difficulty", "難度");
        MaxLbl.Text = P("Max players", "最大玩家數");
        OnlineLbl.Text = P("Online mode (verify accounts)", "線上模式（驗證帳號）");
        SeedLbl.Text = P("Level seed (optional)", "世界種子（可選）");
        PropsReloadBtn.Content = P("Reload", "重新載入");
        PropsSaveBtn.Content = P("Save properties", "儲存設定");

        MemLbl.Text = P("Memory + start script", "記憶體 + 啟動腳本");
        XmsLbl.Text = P("Min (Xms, MB)", "最小（Xms, MB）");
        XmxLbl.Text = P("Max (Xmx, MB)", "最大（Xmx, MB）");
        AikarChk.Content = P("Use Aikar's flags (tuned G1GC)", "使用 Aikar 旗標（調校 G1GC）");
        StartScriptBtn.Content = P("Generate start.bat", "產生 start.bat");

        RawLbl.Text = P("Raw server.properties editor", "server.properties 原文編輯器");
        RawReloadBtn.Content = P("Reload from disk", "由磁碟重新載入");
        RawSaveBtn.Content = P("Save raw text", "儲存原文");

        // Console tab
        StartBtn.Content = P("Start server", "啟動伺服器");
        StopBtn.Content = P("Stop (graceful)", "停止（優雅）");
        KillBtn.Content = P("Force stop", "強制停止");
        SendBtn.Content = P("Send", "送出");
        ConsoleHint.Text = P("Type a server command and press Enter or Send — it goes to the server's stdin. Examples: stop, say hi, op <player>, whitelist add <player>.",
            "輸入伺服器指令再撳 Enter 或「送出」— 會送去伺服器 stdin。例：stop、say hi、op <玩家>、whitelist add <玩家>。");

        // Plugins tab
        PluginWarnBar.Title = P("Building plugins runs untrusted code", "建置外掛會執行未經信任嘅程式碼");
        PluginWarnBar.Message = P("Building from git runs that project's Maven/Gradle scripts on your PC. Only build sources you trust.",
            "由 git 建置會喺你部電腦上執行嗰個專案嘅 Maven／Gradle 腳本。只建置你信任嘅來源。");
        PresetLbl.Text = P("Popular plugins (build from source)", "熱門外掛（由原始碼建置）");
        CustomLbl.Text = P("Build any plugin from a git repo", "由 git repo 建置任何外掛");
        CustomNameLbl.Text = P("Name", "名稱");
        CustomUrlLbl.Text = P("Git URL", "Git URL");
        CustomSysLbl.Text = P("Build system", "建置系統");
        CustomBuildBtn.Content = P("Clone & build → plugins/", "Clone 並建置 → plugins/");
        InstalledLbl.Text = P("Installed plugins", "已安裝外掛");
        InstalledRefreshBtn.Content = P("Refresh", "重新整理");
        InstalledOpenBtn.Content = P("Open plugins folder", "開 plugins 資料夾");
        PluginLogLbl.Text = P("Build log", "建置記錄");
        PluginCancelBtn.Content = P("Cancel build", "取消建置");

        BuildPresetList();
        RefreshRunState();
    }

    // ════════════════════ Server tab ════════════════════

    private void RefreshEngine()
    {
        FolderBox.Text = MinecraftServerService.ServerDir;
        var java = MinecraftServerService.FindJava();
        if (java is null)
        {
            JavaBar.Title = P("Java (JDK 21+) required", "需要 Java（JDK 21+）");
            JavaBar.Message = P("Modern Minecraft needs a JDK. Install Microsoft OpenJDK 21 to enable the server.",
                "現代 Minecraft 需要 JDK。安裝 Microsoft OpenJDK 21 以啟用伺服器。");
            JavaBar.IsOpen = true;
            // attach a rich one-click installer (real progress + live winget output + Cancel + animation)
            JavaBar.ActionButton = null;
            JavaBar.Content = EngineBars.AutoInstallProgress("Microsoft.OpenJDK.21",
                "Install JDK 21", "安裝 JDK 21",
                recheck: async () => { await Task.Yield(); RefreshEngine(); RefreshRunState(); });
        }
        else
        {
            JavaBar.IsOpen = false;
            JavaBar.ActionButton = null;
            JavaBar.Content = null;
        }

        var parts = new List<string>
        {
            java is null ? P("Java: not found", "Java：搵唔到") : $"Java: {java}",
            MinecraftServerService.HasServerJar ? P("server.jar: present", "server.jar：已存在") : P("server.jar: not downloaded", "server.jar：未下載"),
            MinecraftServerService.IsEulaAccepted() ? P("EULA: accepted", "EULA：已接受") : P("EULA: not accepted", "EULA：未接受"),
            MinecraftServerService.HasMaven() ? "Maven: ok" : P("Maven: not found (needed for Maven plugins)", "Maven：搵唔到（建置 Maven 外掛時需要）"),
            MinecraftServerService.HasGit() ? "git: ok" : P("git: not found (needed for plugin builds)", "git：搵唔到（建置外掛時需要）"),
        };
        EngineStatus.Text = string.Join("\n", parts);
    }

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose the server folder", "揀伺服器資料夾"));
        if (folder is null) return;
        MinecraftServerService.SetServerDir(folder);
        RefreshEngine();
        LoadPropertiesForm();
        RawBox.Text = MinecraftServerService.ReadPropertiesRaw();
        RefreshInstalled();
        EulaToggle.IsOn = MinecraftServerService.IsEulaAccepted();
        ServerNotify(InfoBarSeverity.Success, P("Server folder set", "已設定伺服器資料夾"), folder);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(MinecraftServerService.ServerDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = MinecraftServerService.ServerDir, UseShellExecute = true });
        }
        catch { }
    }

    private async void PaperRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        PaperRing.IsActive = true;
        PaperRefreshBtn.IsEnabled = false;
        var (ok, versions, error) = await MinecraftServerService.GetPaperVersions();
        PaperRing.IsActive = false;
        PaperRefreshBtn.IsEnabled = true;
        _busy = false;
        if (!ok)
        {
            ServerNotify(InfoBarSeverity.Error, P("Could not load versions", "載入版本失敗"), error);
            return;
        }
        PaperVersionBox.Items.Clear();
        foreach (var v in versions) PaperVersionBox.Items.Add(v);
        if (PaperVersionBox.Items.Count > 0) PaperVersionBox.SelectedIndex = 0;
        ServerNotify(InfoBarSeverity.Success, P("Versions loaded", "已載入版本"), $"{versions.Count} versions");
    }

    private InstallProgress? _paperControl;
    private InstallProgress? _buildToolsControl;

    /// <summary>建立「下載 Paper」嘅豐富進度控件 · Build the rich install-progress control for the Paper download.</summary>
    private void EnsurePaperControl()
    {
        if (_paperControl is not null) { _paperControl.SetAction(P("Download Paper", "下載 Paper"), P("Download Paper", "下載 Paper"), DownloadPaperAsync); return; }
        _paperControl = InstallProgress.Create(P("Download Paper", "下載 Paper"), P("Download Paper", "下載 Paper"), DownloadPaperAsync);
        if (PaperDownloadHost is not null) PaperDownloadHost.Content = _paperControl;
    }

    private async Task<TweakResult> DownloadPaperAsync(IProgress<InstallProgressReport> progress, CancellationToken ct)
    {
        var version = PaperVersionBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(version))
            return TweakResult.Fail("Pick a version first — load versions, then choose one.", "請先揀版本 — 先載入版本，再揀一個。");

        progress.Report(InstallProgressReport.Status($"Downloading Paper {version}…", $"下載 Paper {version} 緊…"));
        var r = await MinecraftServerService.DownloadPaper(version, (read, total) =>
        {
            if (total > 0)
                progress.Report(InstallProgressReport.Progress(read * 100.0 / total,
                    $"{read / 1024 / 1024} / {total / 1024 / 1024} MB", $"{read / 1024 / 1024} / {total / 1024 / 1024} MB"));
            else
                progress.Report(InstallProgressReport.Status($"{read / 1024 / 1024} MB", $"{read / 1024 / 1024} MB"));
        }, ct);
        DispatcherQueue.TryEnqueue(() => RefreshEngine());
        return r;
    }

    /// <summary>建立「取得 BuildTools」嘅豐富進度控件 · Build the rich install-progress control for the BuildTools download.</summary>
    private void EnsureBuildToolsControl()
    {
        if (_buildToolsControl is not null) { _buildToolsControl.SetAction(P("Get BuildTools", "取得 BuildTools"), P("Get BuildTools", "取得 BuildTools"), DownloadBuildToolsAsync); return; }
        _buildToolsControl = InstallProgress.Create(P("Get BuildTools", "取得 BuildTools"), P("Get BuildTools", "取得 BuildTools"), DownloadBuildToolsAsync);
        if (BuildToolsHost is not null) BuildToolsHost.Content = _buildToolsControl;
    }

    private async Task<TweakResult> DownloadBuildToolsAsync(IProgress<InstallProgressReport> progress, CancellationToken ct)
    {
        progress.Report(InstallProgressReport.Status("Downloading BuildTools.jar…", "下載 BuildTools.jar 緊…"));
        var r = await MinecraftServerService.DownloadBuildTools((read, total) =>
        {
            if (total > 0)
                progress.Report(InstallProgressReport.Progress(read * 100.0 / total,
                    $"{read / 1024 / 1024} / {total / 1024 / 1024} MB", $"{read / 1024 / 1024} / {total / 1024 / 1024} MB"));
            else
                progress.Report(InstallProgressReport.Status($"{read / 1024 / 1024} MB", $"{read / 1024 / 1024} MB"));
        }, ct);
        DispatcherQueue.TryEnqueue(() => AppendSpigot(Msg(r)));
        return r;
    }

    private async void SpigotBuild_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        _spigotCts = new CancellationTokenSource();
        SpigotBuildBtn.IsEnabled = false;
        SpigotCancelBtn.IsEnabled = true;
        SpigotRing.IsActive = true;
        AppendSpigot(P("Building Spigot with BuildTools… (this takes several minutes)", "用 BuildTools 建置 Spigot 緊…（要幾分鐘）"));
        var r = await MinecraftServerService.BuildSpigot(SpigotVersionBox.Text,
            line => DispatcherQueue.TryEnqueue(() => AppendSpigot(line)), _spigotCts.Token);
        SpigotRing.IsActive = false;
        SpigotBuildBtn.IsEnabled = true;
        SpigotCancelBtn.IsEnabled = false;
        _busy = false;
        AppendSpigot(Msg(r));
        ServerNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Spigot built", "Spigot 已建置") : P("Build failed", "建置失敗"), Msg(r));
        RefreshEngine();
    }

    private void SpigotCancel_Click(object sender, RoutedEventArgs e) => _spigotCts?.Cancel();

    private void Eula_Toggled(object sender, RoutedEventArgs e)
    {
        var r = MinecraftServerService.SetEula(EulaToggle.IsOn);
        ServerNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error, P("EULA", "EULA"), Msg(r));
        RefreshEngine();
        RefreshRunState();
    }

    private void AppendSpigot(string line) => Append(SpigotLog, line);

    private void ServerNotify(InfoBarSeverity sev, string title, string msg)
    {
        ServerResultBar.Severity = sev; ServerResultBar.Title = title; ServerResultBar.Message = msg; ServerResultBar.IsOpen = true;
    }

    // ════════════════════ Properties tab ════════════════════

    private void LoadPropertiesForm()
    {
        var p = MinecraftServerService.ReadProperties();
        PortBox.Value = TryInt(p, "server-port", 25565);
        MotdBox.Text = p.TryGetValue("motd", out var motd) ? motd : "A Minecraft Server";
        SelectCombo(GamemodeBox, p.TryGetValue("gamemode", out var gm) ? gm : "survival");
        SelectCombo(DifficultyBox, p.TryGetValue("difficulty", out var df) ? df : "easy");
        MaxBox.Value = TryInt(p, "max-players", 20);
        OnlineToggle.IsOn = !p.TryGetValue("online-mode", out var om) || !om.Equals("false", StringComparison.OrdinalIgnoreCase);
        SeedBox.Text = p.TryGetValue("level-seed", out var sd) ? sd : "";
    }

    private static int TryInt(Dictionary<string, string> p, string key, int fallback)
        => p.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    private static void SelectCombo(ComboBox box, string value)
    {
        foreach (var item in box.Items.OfType<ComboBoxItem>())
            if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            { box.SelectedItem = item; return; }
        box.SelectedIndex = 0;
    }

    private void PropsReload_Click(object sender, RoutedEventArgs e)
    {
        LoadPropertiesForm();
        PropsNotify(InfoBarSeverity.Informational, P("Reloaded", "已重新載入"), MinecraftServerService.PropertiesPath);
    }

    private void PropsSave_Click(object sender, RoutedEventArgs e)
    {
        var updates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["server-port"] = ((int)PortBox.Value).ToString(),
            ["motd"] = MotdBox.Text,
            ["gamemode"] = (GamemodeBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "survival",
            ["difficulty"] = (DifficultyBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "easy",
            ["max-players"] = ((int)MaxBox.Value).ToString(),
            ["online-mode"] = OnlineToggle.IsOn ? "true" : "false",
            ["level-seed"] = SeedBox.Text,
        };
        var r = MinecraftServerService.WriteProperties(updates);
        RawBox.Text = MinecraftServerService.ReadPropertiesRaw();
        PropsNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Saved", "已儲存") : P("Failed", "失敗"), Msg(r));
    }

    private void StartScript_Click(object sender, RoutedEventArgs e)
    {
        int xms = (int)XmsBox.Value, xmx = (int)XmxBox.Value;
        if (xms > xmx) (xms, xmx) = (xmx, xms);
        var r = MinecraftServerService.GenerateStartScript(xms, xmx, AikarChk.IsChecked == true);
        PropsNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("start.bat written", "已寫入 start.bat") : P("Failed", "失敗"), Msg(r));
    }

    private void RawReload_Click(object sender, RoutedEventArgs e)
    {
        RawBox.Text = MinecraftServerService.ReadPropertiesRaw();
        PropsNotify(InfoBarSeverity.Informational, P("Reloaded", "已重新載入"), "");
    }

    private void RawSave_Click(object sender, RoutedEventArgs e)
    {
        var r = MinecraftServerService.WritePropertiesRaw(RawBox.Text);
        LoadPropertiesForm();
        PropsNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Saved", "已儲存") : P("Failed", "失敗"), Msg(r));
    }

    private void PropsNotify(InfoBarSeverity sev, string title, string msg)
    {
        PropsResultBar.Severity = sev; PropsResultBar.Title = title; PropsResultBar.Message = msg; PropsResultBar.IsOpen = true;
    }

    // ════════════════════ Console tab ════════════════════

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        int xms = (int)XmsBox.Value, xmx = (int)XmxBox.Value;
        if (xms > xmx) (xms, xmx) = (xmx, xms);
        var r = MinecraftServerService.Start(xms, xmx, AikarChk.IsChecked == true,
            line => DispatcherQueue.TryEnqueue(() => AppendConsole(line)),
            () => DispatcherQueue.TryEnqueue(() => { AppendConsole(P("[server stopped]", "[伺服器已停止]")); RefreshRunState(); }));
        if (!r.Success)
            AppendConsole("[error] " + Msg(r));
        RefreshRunState();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopBtn.IsEnabled = false;
        AppendConsole(P("[stopping — sending 'stop' to the server]", "[停止緊 — 向伺服器送出 'stop']"));
        var r = await MinecraftServerService.StopGraceful();
        AppendConsole(Msg(r));
        RefreshRunState();
    }

    private void Kill_Click(object sender, RoutedEventArgs e)
    {
        var r = MinecraftServerService.Kill();
        AppendConsole(Msg(r));
        RefreshRunState();
    }

    private void Send_Click(object sender, RoutedEventArgs e) => SendCommand();

    private void Cmd_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) { SendCommand(); e.Handled = true; }
    }

    private void SendCommand()
    {
        var cmd = CmdBox.Text;
        if (string.IsNullOrWhiteSpace(cmd)) return;
        var r = MinecraftServerService.SendCommand(cmd);
        if (r.Success) { AppendConsole("> " + cmd); CmdBox.Text = ""; }
        else AppendConsole("[error] " + Msg(r));
    }

    private void RefreshRunState()
    {
        bool running = MinecraftServerService.IsRunning;
        bool canStart = !running && MinecraftServerService.HasServerJar
            && MinecraftServerService.FindJava() is not null && MinecraftServerService.IsEulaAccepted();
        StartBtn.IsEnabled = canStart;
        StopBtn.IsEnabled = running;
        KillBtn.IsEnabled = running;
        SendBtn.IsEnabled = running;
        CmdBox.IsEnabled = running;
        RunRing.IsActive = running;
        RunState.Text = running
            ? P("Running", "運行緊")
            : (!MinecraftServerService.HasServerJar ? P("No server.jar — download Paper or build Spigot", "冇 server.jar — 下載 Paper 或建置 Spigot")
                : !MinecraftServerService.IsEulaAccepted() ? P("Accept the EULA to start", "接受 EULA 先可以啟動")
                : MinecraftServerService.FindJava() is null ? P("Java not found", "搵唔到 Java")
                : P("Stopped", "已停止"));
    }

    private void AppendConsole(string line) => Append(ConsoleLog, line);

    // ════════════════════ Plugins tab ════════════════════

    private sealed class PresetRow
    {
        public string Title { get; init; } = "";
        public string Subtitle { get; init; } = "";
        public string BuildLabel { get; init; } = "";
        public int Index { get; init; }
    }

    private void BuildPresetList()
    {
        if (PresetList is null) return;
        var rows = new List<PresetRow>();
        for (int i = 0; i < MinecraftPluginCatalog.Presets.Count; i++)
        {
            var pr = MinecraftPluginCatalog.Presets[i];
            rows.Add(new PresetRow
            {
                Title = $"{pr.Name.En} · {pr.Name.Zh}  ({pr.System})",
                Subtitle = Loc.I.Pick(pr.Blurb.En, pr.Blurb.Zh),
                BuildLabel = P("Build → plugins/", "建置 → plugins/"),
                Index = i,
            });
        }
        PresetList.ItemsSource = rows;
    }

    private async void PresetBuild_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not int idx) return;
        if (idx < 0 || idx >= MinecraftPluginCatalog.Presets.Count) return;
        var pr = MinecraftPluginCatalog.Presets[idx];
        if (!await ConfirmBuild(pr.Name.En)) return;
        await BuildPlugin(pr.Name.En, pr.GitUrl, pr.System);
    }

    private async void CustomBuild_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(CustomNameBox.Text) ? "custom-plugin" : CustomNameBox.Text.Trim();
        var url = CustomUrlBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(url))
        {
            PluginNotify(InfoBarSeverity.Warning, P("Enter a git URL", "請輸入 git URL"), "");
            return;
        }
        MinecraftServerService.BuildSystem? sys = CustomSysBox.SelectedIndex switch
        {
            1 => MinecraftServerService.BuildSystem.Maven,
            2 => MinecraftServerService.BuildSystem.Gradle,
            _ => null,
        };
        if (!await ConfirmBuild(name)) return;
        await BuildPlugin(name, url, sys);
    }

    private async Task<bool> ConfirmBuild(string name)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = P("Build from untrusted source?", "由未經信任嘅來源建置？"),
            Content = P($"Building '{name}' will clone the repo and run its Maven/Gradle build scripts on your PC. Only continue if you trust this source.",
                $"建置「{name}」會 clone 個 repo 並喺你部電腦執行佢嘅 Maven／Gradle 建置腳本。只有信任呢個來源先好繼續。"),
            PrimaryButtonText = P("Build", "建置"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close,
        };
        return await dlg.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task BuildPlugin(string name, string url, MinecraftServerService.BuildSystem? sys)
    {
        if (_busy) return;
        _busy = true;
        _pluginCts = new CancellationTokenSource();
        PluginCancelBtn.IsEnabled = true;
        PluginRing.IsActive = true;
        CustomBuildBtn.IsEnabled = false;
        AppendPlugin($"=== {name} ===");
        var r = await MinecraftServerService.BuildPluginFromGit(url, name,
            line => DispatcherQueue.TryEnqueue(() => AppendPlugin(line)), sys, _pluginCts.Token);
        PluginRing.IsActive = false;
        PluginCancelBtn.IsEnabled = false;
        CustomBuildBtn.IsEnabled = true;
        _busy = false;
        AppendPlugin(Msg(r));
        PluginNotify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Plugin built", "外掛已建置") : P("Build failed", "建置失敗"), Msg(r));
        RefreshInstalled();
    }

    private void PluginCancel_Click(object sender, RoutedEventArgs e) => _pluginCts?.Cancel();

    private void InstalledRefresh_Click(object sender, RoutedEventArgs e) => RefreshInstalled();

    private void InstalledOpen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(MinecraftServerService.PluginsDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = MinecraftServerService.PluginsDir, UseShellExecute = true });
        }
        catch { }
    }

    private void RefreshInstalled()
    {
        try
        {
            var dir = MinecraftServerService.PluginsDir;
            if (!Directory.Exists(dir)) { InstalledText.Text = P("(no plugins folder yet)", "（仲未有 plugins 資料夾）"); return; }
            var jars = Directory.EnumerateFiles(dir, "*.jar", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName).ToList();
            InstalledText.Text = jars.Count == 0
                ? P("(no plugins installed)", "（未安裝任何外掛）")
                : string.Join("\n", jars);
        }
        catch (Exception ex) { InstalledText.Text = ex.Message; }
    }

    private void AppendPlugin(string line) => Append(PluginLog, line);

    private void PluginNotify(InfoBarSeverity sev, string title, string msg)
    {
        PluginResultBar.Severity = sev; PluginResultBar.Title = title; PluginResultBar.Message = msg; PluginResultBar.IsOpen = true;
    }

    // ════════════════════ shared ════════════════════

    private static void Append(TextBox box, string line)
    {
        if (box.Text.Length > 80000) box.Text = box.Text.Substring(box.Text.Length - 50000);
        box.Text += (box.Text.Length == 0 ? "" : "\n") + line;
        box.Select(box.Text.Length, 0);
    }
}
