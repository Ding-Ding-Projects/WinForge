using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

public sealed partial class MinecraftWorldToolsModule : Page
{
    private CancellationTokenSource? _chunkerCts;
    private CancellationTokenSource? _blueMapCts;

    public MinecraftWorldToolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            LoadSettings();
            Render();
            RefreshWorldMeta();
            RefreshRunState();
        };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLanguageChanged;
            try { _chunkerCts?.Cancel(); } catch { }
            try { _blueMapCts?.Cancel(); } catch { }
        };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);
    private static string Msg(TweakResult r) => (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";

    private void Render()
    {
        Header.Title = "Minecraft World Tools · Minecraft 世界工具";
        HeaderBlurb.Text = P(
            "Manage Chunker-style world conversion in bounded 500 MB batches and run BlueMap renders from a full in-app control surface.",
            "喺完整介面管理 Chunker 類世界轉換（預設 500 MB 分批，避開記憶體漏）同 BlueMap 地圖算圖。");
        ChunkerTab.Header = "Chunker";
        BlueMapTab.Header = "BlueMap";
        SettingsTab.Header = P("Settings", "設定");
        LogTab.Header = P("Log", "記錄");

        ChunkerWorldLbl.Text = P("World folder", "世界資料夾");
        PickWorldBtn.Content = P("Choose…", "揀…");
        OpenWorldBtn.Content = P("Open", "開啟");
        ChunkerOutputLbl.Text = P("Chunker output folder", "Chunker 輸出資料夾");
        PickChunkerOutputBtn.Content = P("Choose…", "揀…");
        PreviewBatchesBtn.Content = P("Preview batches", "預覽分批");
        RunChunkerBtn.Content = P("Convert in batches", "分批轉換");
        CancelChunkerBtn.Content = P("Cancel", "取消");

        BlueMapOutputLbl.Text = P("BlueMap output folder", "BlueMap 輸出資料夾");
        PickBlueMapOutputBtn.Content = P("Choose…", "揀…");
        OpenBlueMapOutputBtn.Content = P("Open", "開啟");
        GenerateBlueMapConfigBtn.Content = P("Generate config", "產生設定");
        StartBlueMapBtn.Content = P("Start render", "開始算圖");
        StopBlueMapBtn.Content = P("Stop", "停止");

        WorkDirLbl.Text = P("Work folder", "工作資料夾");
        ChunkerToolLbl.Text = P("Chunker tool (.exe/.jar)", "Chunker 工具（.exe/.jar）");
        ChunkerArgsLbl.Text = P("Chunker arguments", "Chunker 參數");
        ChunkerTargetLbl.Text = P("Target", "目標");
        ChunkerBatchLbl.Text = P("Batch size (MB)", "分批大小（MB）");
        BlueMapJarLbl.Text = P("BlueMap jar", "BlueMap jar");
        BlueMapArgsLbl.Text = P("BlueMap JVM arguments", "BlueMap JVM 參數");
        BlueMapMemoryLbl.Text = P("Memory (MB)", "記憶體（MB）");
        BlueMapThreadsLbl.Text = P("Render threads", "算圖執行緒");
        BlueMapPortLbl.Text = P("Web port", "網頁 port");
        BlueMapWebToggle.Header = P("Enable BlueMap web server", "啟用 BlueMap 網頁伺服器");
        PickWorkDirBtn.Content = P("Choose…", "揀…");
        PickChunkerToolBtn.Content = P("Choose…", "揀…");
        PickBlueMapJarBtn.Content = P("Choose…", "揀…");
        SaveSettingsBtn.Content = P("Save settings", "儲存設定");
        ReloadSettingsBtn.Content = P("Reload", "重新載入");
        ClearLogBtn.Content = P("Clear log", "清除記錄");
    }

    private void LoadSettings()
    {
        WorkDirBox.Text = MinecraftWorldToolsService.WorkDir;
        ChunkerToolBox.Text = MinecraftWorldToolsService.ChunkerTool;
        ChunkerArgsBox.Text = MinecraftWorldToolsService.ChunkerArgs;
        ChunkerTargetBox.Text = MinecraftWorldToolsService.ChunkerTarget;
        ChunkerBatchBox.Value = MinecraftWorldToolsService.ChunkerBatchMb;
        BlueMapJarBox.Text = MinecraftWorldToolsService.BlueMapJar;
        BlueMapArgsBox.Text = MinecraftWorldToolsService.BlueMapArgs;
        BlueMapMemoryBox.Value = MinecraftWorldToolsService.BlueMapMemoryMb;
        BlueMapThreadsBox.Value = MinecraftWorldToolsService.BlueMapThreads;
        BlueMapPortBox.Value = MinecraftWorldToolsService.BlueMapPort;
        BlueMapWebToggle.IsOn = MinecraftWorldToolsService.BlueMapWebServer;
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Notify(SettingsBar, InfoBarSeverity.Success, P("Settings saved", "設定已儲存"),
            P("Chunker and BlueMap settings are ready.", "Chunker 同 BlueMap 設定已就緒。"));
    }

    private void SaveSettings()
    {
        MinecraftWorldToolsService.SaveSettings(
            WorkDirBox.Text,
            ChunkerToolBox.Text,
            ChunkerArgsBox.Text,
            ChunkerTargetBox.Text,
            (int)ChunkerBatchBox.Value,
            BlueMapJarBox.Text,
            BlueMapArgsBox.Text,
            (int)BlueMapMemoryBox.Value,
            (int)BlueMapThreadsBox.Value,
            (int)BlueMapPortBox.Value,
            BlueMapWebToggle.IsOn);
    }

    private void ReloadSettings_Click(object sender, RoutedEventArgs e) => LoadSettings();

    private async void PickWorld_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose a Minecraft world", "揀 Minecraft 世界"));
        if (folder is null) return;
        if (!MinecraftWorldToolsService.IsValidWorld(folder))
        {
            Notify(ChunkerBar, InfoBarSeverity.Error, P("Not a world", "唔係世界"),
                P("That folder does not look like a Java or Bedrock world.", "嗰個資料夾唔似 Java 或 Bedrock 世界。"));
            return;
        }
        WorldBox.Text = folder;
        RefreshWorldMeta();
    }

    private async void PickChunkerOutput_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose Chunker output folder", "揀 Chunker 輸出資料夾"));
        if (folder is not null) ChunkerOutputBox.Text = folder;
    }

    private async void PickBlueMapOutput_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose BlueMap output folder", "揀 BlueMap 輸出資料夾"));
        if (folder is not null) BlueMapOutputBox.Text = folder;
    }

    private async void PickWorkDir_Click(object sender, RoutedEventArgs e)
    {
        var folder = await FileDialogs.OpenFolderAsync(P("Choose work folder", "揀工作資料夾"));
        if (folder is not null) WorkDirBox.Text = folder;
    }

    private async void PickChunkerTool_Click(object sender, RoutedEventArgs e)
    {
        var file = await FileDialogs.OpenFileAsync(new[]
        {
            new FileDialogs.Filter("Tools", "*.exe;*.jar"),
            new FileDialogs.Filter("All files", "*.*"),
        }, P("Choose Chunker executable or jar", "揀 Chunker 執行檔或 jar"));
        if (file is not null) ChunkerToolBox.Text = file;
    }

    private async void PickBlueMapJar_Click(object sender, RoutedEventArgs e)
    {
        var file = await FileDialogs.OpenFileAsync(new[]
        {
            new FileDialogs.Filter("Java archives", "*.jar"),
            new FileDialogs.Filter("All files", "*.*"),
        }, P("Choose BlueMap jar", "揀 BlueMap jar"));
        if (file is not null) BlueMapJarBox.Text = file;
    }

    private void PreviewBatches_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        BatchList.Items.Clear();
        var batches = MinecraftWorldToolsService.PreviewChunkerBatches(WorldBox.Text, ChunkerOutputBox.Text);
        foreach (var b in batches)
            BatchList.Items.Add($"#{b.Index:000} · {b.Files} files · {b.Bytes / 1024d / 1024d:0.##} MB");
        Notify(ChunkerBar, batches.Count > 0 ? InfoBarSeverity.Informational : InfoBarSeverity.Warning,
            P("Batch preview", "分批預覽"),
            batches.Count > 0 ? P($"{batches.Count} batches planned.", $"已規劃 {batches.Count} 批。") : P("No batches found.", "搵唔到分批。"));
    }

    private async void RunChunker_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        _chunkerCts = new CancellationTokenSource();
        SetChunkerBusy(true);
        AppendLog(P("[Chunker started]", "[Chunker 已啟動]"));
        var r = await MinecraftWorldToolsService.RunChunkerBatched(WorldBox.Text, ChunkerOutputBox.Text,
            line => DispatcherQueue.TryEnqueue(() => AppendLog(line)), _chunkerCts.Token);
        SetChunkerBusy(false);
        Notify(ChunkerBar, r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Chunker complete", "Chunker 完成") : P("Chunker failed", "Chunker 失敗"), Msg(r));
        AppendLog(Msg(r));
    }

    private void CancelChunker_Click(object sender, RoutedEventArgs e) => _chunkerCts?.Cancel();

    private void GenerateBlueMapConfig_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        var r = MinecraftWorldToolsService.GenerateBlueMapConfig(WorldBox.Text, BlueMapOutputBox.Text);
        Notify(BlueMapBar, r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("Config generated", "設定已產生") : P("Config failed", "設定失敗"), Msg(r));
    }

    private async void StartBlueMap_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        _blueMapCts = new CancellationTokenSource();
        BlueMapRing.IsActive = true;
        var r = await MinecraftWorldToolsService.StartBlueMap(WorldBox.Text, BlueMapOutputBox.Text,
            line => DispatcherQueue.TryEnqueue(() => AppendLog(line)), _blueMapCts.Token);
        BlueMapRing.IsActive = MinecraftWorldToolsService.IsBlueMapRunning;
        RefreshRunState();
        Notify(BlueMapBar, r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error,
            r.Success ? P("BlueMap started", "BlueMap 已啟動") : P("BlueMap failed", "BlueMap 失敗"), Msg(r));
    }

    private void StopBlueMap_Click(object sender, RoutedEventArgs e)
    {
        _blueMapCts?.Cancel();
        var r = MinecraftWorldToolsService.StopBlueMap();
        BlueMapRing.IsActive = false;
        RefreshRunState();
        Notify(BlueMapBar, r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
            r.Success ? P("BlueMap stopped", "BlueMap 已停止") : P("BlueMap not running", "BlueMap 未運行"), Msg(r));
    }

    private void OpenWorld_Click(object sender, RoutedEventArgs e) => OpenFolder(WorldBox.Text);
    private void OpenBlueMapOutput_Click(object sender, RoutedEventArgs e) => OpenFolder(BlueMapOutputBox.Text);
    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogBox.Text = "";

    private void RefreshWorldMeta()
    {
        WorldMetaText.Text = MinecraftWorldToolsService.IsValidWorld(WorldBox.Text)
            ? MinecraftWorldToolsService.DescribeWorld(WorldBox.Text)
            : P("No world selected.", "未揀世界。");
    }

    private void RefreshRunState()
    {
        var running = MinecraftWorldToolsService.IsBlueMapRunning;
        StartBlueMapBtn.IsEnabled = !running;
        StopBlueMapBtn.IsEnabled = running;
        BlueMapRing.IsActive = running;
    }

    private void SetChunkerBusy(bool busy)
    {
        ChunkerRing.IsActive = busy;
        RunChunkerBtn.IsEnabled = !busy;
        PreviewBatchesBtn.IsEnabled = !busy;
        CancelChunkerBtn.IsEnabled = busy;
    }

    private void AppendLog(string line)
    {
        LogBox.Text += (LogBox.Text.Length == 0 ? "" : Environment.NewLine) + line;
        LogBox.SelectionStart = LogBox.Text.Length;
    }

    private static void OpenFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private static void Notify(InfoBar bar, InfoBarSeverity severity, string title, string message)
    {
        bar.Severity = severity;
        bar.Title = title;
        bar.Message = message;
        bar.IsOpen = true;
    }
}
