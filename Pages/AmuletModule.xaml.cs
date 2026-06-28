using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Amulet Minecraft 世界編輯器 · Amulet Minecraft world editor module. Extracts/locates the bundled Amulet
/// zip into a managed app-data dir, ensures a Python runtime (one-click winget install when absent) and
/// Amulet's pip deps, lets the user pick a world folder (FileDialogs), reads its level.dat natively to show
/// name / version / edition / dimensions / size / last-played, then launches and tracks Amulet (Start /
/// Stop / live log). Also backs up a world (zip), opens the default saves folder, and re-extracts on demand.
/// Amulet runs as a separate process (GPLv3). No redirect. Bilingual.
/// </summary>
public sealed partial class AmuletModule : Page
{
    private string? _world;

    public AmuletModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => { Render(); RefreshEngines(); RefreshRunState(); BuildOps(); };
        Loaded += async (_, _) =>
        {
            Render();
            // restore last world
            var last = AmuletService.LastWorld;
            if (!string.IsNullOrWhiteSpace(last) && AmuletService.IsValidWorld(last)) SetWorld(last);
            RefreshEngines();
            RefreshRunState();
            BuildOps();
            await Task.CompletedTask;
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private string Msg(TweakResult r) => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    // ── Static text ──────────────────────────────────────────────────────────

    private void Render()
    {
        HeaderTitle.Text = "Minecraft World Editor (Amulet) · Minecraft 世界編輯器（Amulet）";
        HeaderBlurb.Text = P(
            "Bundle, install and launch the Amulet Minecraft world editor. Pick a Java/Bedrock world, preview its level.dat metadata, then open it in Amulet — with tracked Start/Stop and a live log. Amulet runs as its own process.",
            "打包、安裝同啟動 Amulet Minecraft 世界編輯器。揀一個 Java／Bedrock 世界，預覽佢嘅 level.dat 資料，然後喺 Amulet 入面開 — 有追蹤式開始／停止同即時記錄。Amulet 以獨立程序運行。");

        WorldHeader.Text = P("World", "世界");
        PickWorldBtn.Content = P("Open World…", "開啟世界…");
        OpenSavesBtn.Content = P("Browse saves…", "瀏覽存檔…");
        ClearWorldBtn.Content = P("Clear", "清除");

        MetaNameLbl.Text = P("Name", "名稱");
        MetaVersionLbl.Text = P("Version", "版本");
        MetaEditionLbl.Text = P("Edition", "版別");
        MetaDimsLbl.Text = P("Dimensions", "維度");
        MetaSizeLbl.Text = P("Size", "大小");
        MetaPlayedLbl.Text = P("Last played", "上次遊玩");

        LaunchHeader.Text = P("Launch", "啟動");
        LaunchBtn.Content = P("Launch Amulet", "啟動 Amulet");
        StopBtn.Content = P("Stop", "停止");
        BackupBtn.Content = P("Backup world…", "備份世界…");

        SetupHeader.Text = P("Setup & maintenance", "設定與維護");
        CloneSourceBtn.Content = P("Clone / update original source", "Clone／更新原始 source");
        UseSourceFolderBtn.Content = P("Use source folder…", "使用 source 資料夾…");
        LocateZipBtn.Content = P("Locate zip fallback…", "指定 zip 後備…");
        LogHeader.Text = P("Live log", "即時記錄");
        ClearLogBtn.Content = P("Clear log", "清除記錄");
    }

    // ── Engine bars (Amulet extracted? Python present?) ───────────────────────

    private void RefreshEngines()
    {
        AmuletActionRow.Children.Clear();
        PythonActionRow.Children.Clear();

        var entry = AmuletService.FindEntryPoint();
        bool extracted = entry is not null;
        AmuletBar.IsOpen = true;
        AmuletBar.IsClosable = false;
        AmuletBar.Severity = extracted ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        if (extracted)
        {
            AmuletBar.Title = P("Amulet ready", "Amulet 已就緒");
            var mode = entry!.Mode == AmuletService.LaunchMode.FrozenExe
                ? P("frozen build", "凍結版")
                : P("Python source", "Python 源碼");
            AmuletBar.Message = P($"Amulet found ({mode}) at {AmuletService.ExtractDir}.",
                                  $"喺 {AmuletService.ExtractDir} 搵到 Amulet（{mode}）。");
        }
        else
        {
            AmuletBar.Title = P("Amulet not extracted", "Amulet 未解壓");
            var zip = AmuletService.FindZip();
            AmuletBar.Message = zip is not null
                ? P($"Found zip at {zip}. Extract it to set up Amulet.", $"喺 {zip} 搵到壓縮檔。解壓即可設定 Amulet。")
                : P($"Amulet zip not found. Expected at {AmuletService.ExpectedZipPath}. Use “Locate zip…”.",
                    $"搵唔到 Amulet 壓縮檔。預期喺 {AmuletService.ExpectedZipPath}。用「指定壓縮檔…」。");

            var extractBtn = new Button { Content = P("Extract Amulet", "解壓 Amulet") };
            extractBtn.Click += async (_, _) => await DoExtract();
            AmuletActionRow.Children.Add(extractBtn);

            var locateBtn = new Button { Content = P("Locate zip…", "指定壓縮檔…") };
            locateBtn.Click += async (_, _) => await LocateZip();
            AmuletActionRow.Children.Add(locateBtn);
        }

        // Python bar only matters for the Python-source launch mode.
        bool needsPython = entry is null || entry.Mode == AmuletService.LaunchMode.PythonModule;
        if (needsPython)
        {
            bool hasPy = AmuletService.HasPython();
            PythonBar.IsOpen = true;
            PythonBar.IsClosable = false;
            PythonBar.Severity = hasPy ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
            PythonBar.Title = hasPy ? P("Python found", "搵到 Python") : P("Python not found", "搵唔到 Python");
            PythonBar.Message = hasPy
                ? P($"Using {AmuletService.FindPython()}.", $"使用 {AmuletService.FindPython()}。")
                : P("Amulet (Python source) needs Python 3. Install it to continue.", "Amulet（Python 源碼）需要 Python 3。請安裝以繼續。");
            if (!hasPy)
            {
                PythonActionRow.Children.Add(EngineBars.AutoInstallButton(
                    "Python.Python.3.12", "Install Python", "安裝 Python",
                    recheck: async () => { RefreshEngines(); BuildOps(); await Task.CompletedTask; },
                    rescan: PackageService.RefreshProcessPath));
            }
        }
        else
        {
            PythonBar.IsOpen = false;
        }
    }

    // ── Setup / maintenance ops (TweakCard-style action rows) ─────────────────

    private void BuildOps()
    {
        OpsList.Children.Clear();
        foreach (var op in AmuletOperations.All)
        {
            var card = new Controls.TweakCard();
            card.SetTweak(op);
            OpsList.Children.Add(card);
        }
    }

    private async Task DoExtract()
    {
        Busy.IsActive = true;
        AppendLog(P("[extracting Amulet…]", "[解壓 Amulet 中…]"));
        var r = await AmuletService.EnsureExtracted();
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Extracted", "已解壓") : P("Extract failed", "解壓失敗"), Msg(r));
        AppendLog(Msg(r));
        RefreshEngines();
    }

    private async Task LocateZip()
    {
        var filters = new[] { new FileDialogs.Filter("Amulet zip", "*.zip"), new FileDialogs.Filter("All files", "*.*") };
        var path = await FileDialogs.OpenFileAsync(filters, P("Locate amulet_map_editor.zip", "指定 amulet_map_editor.zip"));
        if (string.IsNullOrWhiteSpace(path)) return;
        AmuletService.SetZip(path!);
        Notify(InfoBarSeverity.Success, P("Zip set", "已設定壓縮檔"), path!);
        RefreshEngines();
    }

    private async void LocateZip_Click(object sender, RoutedEventArgs e) => await LocateZip();

    private async void CloneSource_Click(object sender, RoutedEventArgs e)
    {
        Busy.IsActive = true;
        AppendLog(P("[cloning/updating Amulet source…]", "[clone／更新 Amulet source 中…]"));
        var r = await AmuletService.CloneOrUpdateSource(line => DispatcherQueue.TryEnqueue(() => AppendLog(line)));
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Source ready", "Source 已就緒") : P("Source clone failed", "Source clone 失敗"), Msg(r));
        AppendLog(Msg(r));
        RefreshEngines();
        BuildOps();
    }

    private async void UseSourceFolder_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Pick Amulet source folder", "揀 Amulet source 資料夾"));
        if (string.IsNullOrWhiteSpace(folder)) return;
        var r = AmuletService.UseExistingSourceFolder(folder!);
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Source folder set", "Source 資料夾已設定") : P("Invalid source folder", "Source 資料夾無效"), Msg(r));
        RefreshEngines();
        BuildOps();
    }

    // ── World picker ──────────────────────────────────────────────────────────

    private async void PickWorld_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Pick a Minecraft world folder", "揀一個 Minecraft 世界資料夾"));
        if (string.IsNullOrWhiteSpace(folder)) return;
        if (!AmuletService.IsValidWorld(folder!))
        {
            Notify(InfoBarSeverity.Error, P("Not a world", "唔係世界"),
                P("That folder has no level.dat — it isn't a Minecraft world.", "嗰個資料夾冇 level.dat — 唔係 Minecraft 世界。"));
            return;
        }
        SetWorld(folder!);
    }

    private void SetWorld(string folder)
    {
        _world = folder;
        WorldPathText.Text = folder;
        RenderMeta(AmuletService.ReadWorldMeta(folder));
        RefreshRunState();
    }

    private void RenderMeta(AmuletService.WorldMeta m)
    {
        MetaCard.Visibility = Visibility.Visible;
        MetaNameVal.Text = m.Name;
        MetaVersionVal.Text = string.IsNullOrWhiteSpace(m.Version)
            ? (m.DataVersion > 0 ? $"DataVersion {m.DataVersion}" : "—")
            : (m.DataVersion > 0 ? $"{m.Version}  (DataVersion {m.DataVersion})" : m.Version);
        MetaEditionVal.Text = string.IsNullOrWhiteSpace(m.Edition) ? "—" : m.Edition;
        MetaDimsVal.Text = m.DimensionsDisplay;
        MetaSizeVal.Text = m.SizeDisplay;
        MetaPlayedVal.Text = m.LastPlayedDisplay;
    }

    private void ClearWorld_Click(object sender, RoutedEventArgs e)
    {
        _world = null;
        WorldPathText.Text = "";
        MetaCard.Visibility = Visibility.Collapsed;
        RefreshRunState();
    }

    private void OpenSaves_Click(object sender, RoutedEventArgs e)
    {
        var saves = AmuletService.FindSavesFolder();
        if (saves is null)
        {
            Notify(InfoBarSeverity.Warning, P("No saves folder", "冇存檔資料夾"),
                P("Couldn't find %AppData%\\.minecraft\\saves. Use “Open World…” to browse manually.",
                  "搵唔到 %AppData%\\.minecraft\\saves。用「開啟世界…」手動瀏覽。"));
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = saves, UseShellExecute = true });
        }
        catch (Exception ex) { Notify(InfoBarSeverity.Error, P("Couldn't open folder", "開唔到資料夾"), ex.Message); }
    }

    // ── Launch / stop / backup ─────────────────────────────────────────────────

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        AppendLog(P("[launching Amulet…]", "[啟動 Amulet 中…]"));
        var r = AmuletService.Start(_world,
            line => DispatcherQueue.TryEnqueue(() => AppendLog(line)),
            () => DispatcherQueue.TryEnqueue(() => { AppendLog(P("[Amulet stopped]", "[Amulet 已停止]")); RefreshRunState(); }));
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Launched", "已啟動") : P("Launch failed", "啟動失敗"), Msg(r));
        AppendLog(Msg(r));
        RefreshRunState();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        var r = AmuletService.Stop();
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            r.Success ? P("Stopped", "已停止") : P("Not running", "未運行"), Msg(r));
        RefreshRunState();
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (_world is null || !AmuletService.IsValidWorld(_world))
        {
            Notify(InfoBarSeverity.Warning, P("Pick a world first", "請先揀世界"), "");
            return;
        }
        var name = System.IO.Path.GetFileName(_world.TrimEnd('\\', '/'));
        var dest = await FileDialogs.SaveFileAsync(
            $"{name}-backup-{DateTime.Now:yyyyMMdd-HHmm}.zip", ".zip");
        if (string.IsNullOrWhiteSpace(dest)) return;

        Busy.IsActive = true;
        AppendLog(P($"[backing up world to {dest}…]", $"[備份世界到 {dest}…]"));
        var r = await AmuletService.BackupWorld(_world, dest!);
        Busy.IsActive = false;
        Notify(r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Backed up", "已備份") : P("Backup failed", "備份失敗"), Msg(r));
        AppendLog(Msg(r));
    }

    // ── Run-state / log ────────────────────────────────────────────────────────

    private void RefreshRunState()
    {
        bool running = AmuletService.IsRunning;
        bool ready = AmuletService.FindEntryPoint() is not null
                     && (AmuletService.FindEntryPoint()?.Mode != AmuletService.LaunchMode.PythonModule || AmuletService.HasPython());
        LaunchBtn.IsEnabled = ready && !running;
        StopBtn.IsEnabled = running;
        BackupBtn.IsEnabled = _world is not null;
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        if (LogBox.Text.Length > 60000) LogBox.Text = LogBox.Text.Substring(LogBox.Text.Length - 40000);
        LogBox.Text += (LogBox.Text.Length == 0 ? "" : "\n") + line;
        LogBox.Select(LogBox.Text.Length, 0);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Text = "";

    private void Notify(InfoBarSeverity sev, string title, string msg)
    {
        StatusBar.IsOpen = true; StatusBar.Severity = sev; StatusBar.Title = title; StatusBar.Message = msg;
    }
}
