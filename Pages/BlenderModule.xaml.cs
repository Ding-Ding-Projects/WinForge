using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// Blender（3D／算圖）· Blender (3D / Render) module. WinForge is the launcher, render-job builder and
/// progress dashboard around the installed blender.exe — it never links Blender's code. Open .blend in the
/// GUI; build a headless single-frame or animation render (output folder + name template, engine, format,
/// samples / device override) with a live progress bar and log tail; a sequential batch queue; and a
/// Python-script runner with shipped starter scripts (glTF / FBX / OBJ export, scene info). Bilingual.
/// </summary>
public sealed partial class BlenderModule : Page
{
    private List<TweakDefinition>? _ops;
    private readonly ObservableCollection<RenderJob> _queue = new();
    private readonly ObservableCollection<BlenderMcpInstance> _mcpInstances = new();
    private bool _queueRunning;
    private int _curFrame;

    public BlenderModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += (_, _) => Render();
        Loaded += async (_, _) =>
        {
            Render();
            BuildOps();
            BuildCombos();
            BuildScriptList();
            QueueList.ItemsSource = _queue;
            McpList.ItemsSource = _mcpInstances;
            RefreshMcpInstances();
            await CheckEngine();
            UpdateRunState();
            UpdateQueueEmpty();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ── Rendering of static strings ──────────────────────────────────────────

    private void Render()
    {
        Header.Title = "Blender · 3D / 算圖";
        HeaderBlurb.Text = P("Launch Blender, open .blend files, run headless renders and Python scripts, and batch-render — WinForge drives the installed blender.exe; the 3D suite itself is never reimplemented.",
            "啟動 Blender、開 .blend、跑 headless 算圖同 Python script、批次算圖 — WinForge 驅動已安裝嘅 blender.exe；3D 套件本身唔會重寫。");
        QuickHeader.Text = P("Quick actions", "快速動作");
        OpenHeader.Text = P("Open a .blend in the Blender GUI", "用 Blender GUI 開 .blend");
        OpenBlendBtn.Content = P("Open .blend…", "開 .blend…");

        RenderHeader.Text = P("Headless render job", "Headless 算圖工作");
        InputLabel.Text = P("Input .blend file", "輸入 .blend 檔");
        PickInputBtn.Content = P("Pick…", "揀…");
        OutputLabel.Text = P("Output folder & filename", "輸出資料夾同檔名");
        PickOutputBtn.Content = P("Pick folder…", "揀資料夾…");
        NameHint.Text = P("#### becomes the frame number", "#### 會變成影格號碼");

        SingleRadio.Content = P("Single frame", "單一影格");
        FrameLabel.Text = P("Frame", "影格");
        AnimRadio.Content = P("Animation range", "動畫範圍");
        StartLabel.Text = P("Start", "開始");
        EndLabel.Text = P("End", "結束");
        EngineLabel.Text = P("Engine", "引擎");
        FormatLabel.Text = P("Format", "格式");
        DeviceLabel.Text = P("Device", "裝置");
        SamplesLabel.Text = P("Samples (0 = file)", "取樣（0 = 跟檔）");

        RenderBtn.Content = P("Render now", "即刻算圖");
        QueueBtn.Content = P("Add to queue", "加入佇列");
        CancelBtn.Content = P("Cancel", "取消");
        OpenOutBtn.Content = P("Open output", "開輸出");
        ProgressText.Text = "";

        QueueHeader.Text = P("Batch queue", "批次佇列");
        RunQueueBtn.Content = P("Run queue", "執行佇列");
        ClearQueueBtn.Content = P("Clear", "清空");

        ScriptHeader.Text = P("Run a Python script", "跑 Python script");
        ScriptBlurb.Text = P("Run a script against the input .blend above (or none). Starter scripts are written to %LOCALAPPDATA%\\WinForge\\blender-scripts.",
            "對住上面嘅輸入 .blend（或者唔開檔）跑 script。內建起步 script 會寫去 %LOCALAPPDATA%\\WinForge\\blender-scripts。");
        RunScriptBtn.Content = P("Run starter", "跑起步 script");
        RunCustomScriptBtn.Content = P("Run .py file…", "跑 .py 檔…");

        McpHeader.Text = P("Blender MCP server manager", "Blender MCP server 管理");
        McpBlurb.Text = P("Deploy and manage unlimited Blender MCP instances. Each saved instance has its own name and Blender add-on port, so multiple AI agents or models can work against separate Blender sessions at the same time.",
            "部署同管理不限數量嘅 Blender MCP 實例。每個實例都有自己嘅名稱同 Blender add-on port，方便多個 AI agent 或 model 同時對住唔同 Blender session 做嘢。");
        McpCreateBtn.Content = P("Add instance", "新增實例");
        McpOpenFolderBtn.Content = P("Open MCP folder", "開 MCP 資料夾");
        McpDeployBtn.Content = P("Deploy / verify server", "部署／驗證 server");
        McpAddonBtn.Content = P("Download add-on", "下載 add-on");
        McpStartBtn.Content = P("Start instance", "啟動實例");
        McpStopBtn.Content = P("Stop instance", "停止實例");
        McpTestBtn.Content = P("Test Blender socket", "測試 Blender socket");
        McpConfigureBtn.Content = P("Add to Codex / Claude / OpenCode", "加入 Codex／Claude／OpenCode");
        McpBundleBtn.Content = P("Export config bundle", "匯出設定包");
        McpSkillBtn.Content = P("Generate agent skill", "產生 agent skill");
        McpDeleteBtn.Content = P("Delete instance", "刪除實例");

        LogHeader.Text = P("Output log", "輸出記錄");
        ClearLogBtn.Content = P("Clear", "清空");

        UpdateQueueEmpty();
    }

    private void BuildCombos()
    {
        if (EngineCombo.Items.Count > 0) return;
        AddCombo(EngineCombo, ("Use file's", "跟檔案", ""), ("Cycles", "Cycles", "CYCLES"),
            ("EEVEE", "EEVEE", "BLENDER_EEVEE"), ("Workbench", "Workbench", "BLENDER_WORKBENCH"));
        AddCombo(FormatCombo, ("Use file's", "跟檔案", ""), ("PNG", "PNG", "PNG"), ("JPEG", "JPEG", "JPEG"),
            ("OpenEXR", "OpenEXR", "OPEN_EXR"), ("TIFF", "TIFF", "TIFF"), ("WebP", "WebP", "WEBP"),
            ("Video (FFMPEG)", "影片（FFMPEG）", "FFMPEG"));
        AddCombo(DeviceCombo, ("Use file's", "跟檔案", ""), ("CPU", "CPU", "CPU"), ("GPU", "GPU", "GPU"));
        EngineCombo.SelectedIndex = 0; FormatCombo.SelectedIndex = 0; DeviceCombo.SelectedIndex = 0;
    }

    private void AddCombo(ComboBox box, params (string en, string zh, string val)[] items)
    {
        foreach (var (en, zh, val) in items)
            box.Items.Add(new ComboBoxItem { Content = P(en, zh), Tag = val });
    }

    private static string ComboVal(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

    private void BuildOps()
    {
        _ops ??= BlenderOperations.All().ToList();
        OpsPanel.Children.Clear();
        foreach (var op in _ops)
        {
            var card = new Controls.TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }

    private void BuildScriptList()
    {
        ScriptCombo.Items.Clear();
        foreach (var s in BlenderService.StarterScripts)
            ScriptCombo.Items.Add(new ComboBoxItem { Content = P(s.En, s.Zh), Tag = s.Id });
        if (ScriptCombo.Items.Count > 0) ScriptCombo.SelectedIndex = 0;
    }

    // ── Engine bar ───────────────────────────────────────────────────────────

    private async Task CheckEngine()
    {
        var (ok, en, zh) = BlenderService.Health();
        if (ok)
        {
            EngineBar.IsOpen = false;
            VersionText.Text = await BlenderService.GetVersion();
        }
        else
        {
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Warning;
            EngineBar.Title = P("Blender not found", "搵唔到 Blender");
            EngineBar.Message = P(en, zh);
            EngineBar.ActionButton = EngineBars.AutoInstallButton(
                "BlenderFoundation.Blender", "Install Blender", "安裝 Blender",
                async () => { await CheckEngine(); }, BlenderService.Rescan);
            VersionText.Text = "";
        }
    }

    // ── Open in GUI ──────────────────────────────────────────────────────────

    private async void OpenBlend_Click(object sender, RoutedEventArgs e)
    {
        var f = await FileDialogs.OpenFileAsync(".blend");
        if (f is null) return;
        Report(P("Open in Blender", "喺 Blender 開"), BlenderService.OpenGui(f));
    }

    // ── Pickers ──────────────────────────────────────────────────────────────

    private async void PickInput_Click(object sender, RoutedEventArgs e)
    {
        var f = await FileDialogs.OpenFileAsync(".blend");
        if (f is not null) InputBox.Text = f;
    }

    private async void PickOutput_Click(object sender, RoutedEventArgs e)
    {
        var d = await FileDialogs.OpenFolderAsync(P("Pick the output folder", "揀輸出資料夾"));
        if (d is not null) OutputBox.Text = d;
    }

    private void FrameMode_Changed(object sender, RoutedEventArgs e) { /* radios drive job building */ }

    // ── Build a job from the form ────────────────────────────────────────────

    private RenderJob? BuildJob()
    {
        var input = (InputBox.Text ?? "").Trim();
        if (input.Length == 0 || !File.Exists(input))
        {
            Report(P("Render", "算圖"), TweakResult.Fail("Pick an input .blend file first.", "請先揀輸入 .blend 檔。"));
            return null;
        }
        var outDir = (OutputBox.Text ?? "").Trim();
        if (outDir.Length == 0)
        {
            Report(P("Render", "算圖"), TweakResult.Fail("Pick an output folder first.", "請先揀輸出資料夾。"));
            return null;
        }
        return new RenderJob
        {
            BlendFile = input,
            OutputDir = outDir,
            OutputName = string.IsNullOrWhiteSpace(NameBox.Text) ? "frame_####" : NameBox.Text!.Trim(),
            Animation = AnimRadio.IsChecked == true,
            Frame = (int)FrameBox.Value,
            StartFrame = (int)StartBox.Value,
            EndFrame = (int)EndBox.Value,
            Engine = ComboVal(EngineCombo),
            Format = ComboVal(FormatCombo),
            Device = ComboVal(DeviceCombo),
            Samples = (int)SamplesBox.Value,
        };
    }

    // ── Render now ───────────────────────────────────────────────────────────

    private void Render_Click(object sender, RoutedEventArgs e)
    {
        if (BlenderService.IsRunning) { Report(P("Render", "算圖"), TweakResult.Fail("A render is already running.", "已經有一個算圖喺度運行。")); return; }
        var job = BuildJob();
        if (job is null) return;
        StartJob(job, P("Render complete.", "算圖完成。"), null);
    }

    private void StartJob(RenderJob job, string doneMsg, Action? after)
    {
        _curFrame = 0;
        Progress.Value = 0;
        ProgressText.Text = P("Starting…", "開始緊…");
        AppendLog($"$ blender {BlenderService.BuildRenderArgs(job)}");
        var r = BlenderService.StartRender(job,
            line => DispatcherQueue.TryEnqueue(() => OnRenderLine(line)),
            code => DispatcherQueue.TryEnqueue(() =>
            {
                AppendLog(code == 0 ? P("[render finished]", "[算圖完成]") : P($"[render exited {code}]", $"[算圖結束 {code}]"));
                if (code == 0) { Progress.Value = 1; ProgressText.Text = doneMsg; }
                UpdateRunState();
                after?.Invoke();
            }));
        if (!r.Success) { Report(P("Render", "算圖"), r); after?.Invoke(); return; }
        UpdateRunState();
    }

    private void OnRenderLine(string line)
    {
        AppendLog(line);
        var pi = BlenderService.ParseLine(line);
        if (pi.CurrentFrame is { } f) _curFrame = f;
        if (pi.Fraction is { } frac) Progress.Value = frac;
        var parts = new List<string>();
        if (_curFrame > 0) parts.Add(P($"Frame {_curFrame}", $"影格 {_curFrame}"));
        if (pi.Fraction is { } pf) parts.Add($"{pf * 100:0}%");
        if (pi.SavedPath is { } sp) parts.Add(P($"Saved {Path.GetFileName(sp)}", $"已存 {Path.GetFileName(sp)}"));
        if (parts.Count > 0) ProgressText.Text = string.Join(" · ", parts);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _queueRunning = false;
        Report(P("Cancel", "取消"), BlenderService.Cancel());
        UpdateRunState();
    }

    private void UpdateRunState()
    {
        bool running = BlenderService.IsRunning;
        CancelBtn.IsEnabled = running;
        RenderBtn.IsEnabled = !running;
        RunQueueBtn.IsEnabled = !running && _queue.Count > 0;
        Busy.IsActive = running;
    }

    private void OpenOut_Click(object sender, RoutedEventArgs e)
        => BlenderService.OpenFolder((OutputBox.Text ?? "").Trim());

    // ── Batch queue ──────────────────────────────────────────────────────────

    private void Queue_Click(object sender, RoutedEventArgs e)
    {
        var job = BuildJob();
        if (job is null) return;
        _queue.Add(job);
        UpdateQueueEmpty();
        UpdateRunState();
        Report(P("Queue", "佇列"), TweakResult.Ok($"Added: {job.Title}", $"已加入：{job.Title}"));
    }

    private void ClearQueue_Click(object sender, RoutedEventArgs e)
    {
        _queue.Clear();
        UpdateQueueEmpty();
        UpdateRunState();
    }

    private void RunQueue_Click(object sender, RoutedEventArgs e)
    {
        if (BlenderService.IsRunning || _queueRunning) return;
        if (_queue.Count == 0) return;
        _queueRunning = true;
        RunNextInQueue();
    }

    private void RunNextInQueue()
    {
        if (!_queueRunning || _queue.Count == 0)
        {
            _queueRunning = false;
            UpdateRunState();
            if (_queue.Count == 0) AppendLog(P("[queue done]", "[佇列完成]"));
            return;
        }
        var job = _queue[0];
        AppendLog(P($"[queue: {job.Title}]", $"[佇列：{job.Title}]"));
        StartJob(job, P("Queued render complete.", "佇列算圖完成。"), () =>
        {
            if (_queue.Count > 0) _queue.RemoveAt(0);
            UpdateQueueEmpty();
            RunNextInQueue();
        });
    }

    private void UpdateQueueEmpty()
    {
        QueueEmpty.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        QueueEmpty.Text = P("The queue is empty. Build a job above and press \"Add to queue\".",
            "佇列係空嘅。喺上面砌一個工作，撳「加入佇列」。");
    }

    // ── Python scripts ───────────────────────────────────────────────────────

    private void RunScript_Click(object sender, RoutedEventArgs e)
    {
        if (BlenderService.IsRunning) { Report(P("Script", "Script"), TweakResult.Fail("A render is already running.", "已經有一個算圖喺度運行。")); return; }
        var id = (ScriptCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        var s = BlenderService.StarterScripts.FirstOrDefault(x => x.Id == id);
        if (s is null) return;
        var path = BlenderService.EnsureStarterScript(s);
        RunScriptPath(path);
    }

    private async void RunCustomScript_Click(object sender, RoutedEventArgs e)
    {
        if (BlenderService.IsRunning) { Report(P("Script", "Script"), TweakResult.Fail("A render is already running.", "已經有一個算圖喺度運行。")); return; }
        var f = await FileDialogs.OpenFileAsync(".py");
        if (f is null) return;
        RunScriptPath(f);
    }

    private void RunScriptPath(string scriptPath)
    {
        var blend = (InputBox.Text ?? "").Trim();
        AppendLog($"$ blender {BlenderService.BuildScriptArgs(blend, scriptPath)}");
        ProgressText.Text = P("Running script…", "跑緊 script…");
        var r = BlenderService.StartScript(blend, scriptPath,
            line => DispatcherQueue.TryEnqueue(() => AppendLog(line)),
            code => DispatcherQueue.TryEnqueue(() =>
            {
                AppendLog(code == 0 ? P("[script finished]", "[script 完成]") : P($"[script exited {code}]", $"[script 結束 {code}]"));
                ProgressText.Text = code == 0 ? P("Script done.", "Script 完成。") : P("Script failed.", "Script 失敗。");
                UpdateRunState();
            }));
        if (!r.Success) Report(P("Script", "Script"), r);
        UpdateRunState();
    }

    // ── Blender MCP manager ─────────────────────────────────────────────────

    private void RefreshMcpInstances()
    {
        _mcpInstances.Clear();
        foreach (var inst in BlenderService.LoadMcpInstances())
        {
            inst.Notes = BlenderService.IsMcpRunning(inst) ? P("Running", "運行中") : "";
            _mcpInstances.Add(inst);
        }
        if (_mcpInstances.Count > 0 && McpList.SelectedIndex < 0) McpList.SelectedIndex = 0;
    }

    private BlenderMcpInstance? SelectedMcp()
        => McpList.SelectedItem as BlenderMcpInstance ?? _mcpInstances.FirstOrDefault();

    private void McpCreate_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var name = string.IsNullOrWhiteSpace(McpNameBox.Text) ? $"blender-{(int)McpPortBox.Value}" : McpNameBox.Text.Trim();
            var inst = BlenderService.CreateMcpInstance(name, (int)McpPortBox.Value);
            RefreshMcpInstances();
            McpList.SelectedItem = _mcpInstances.FirstOrDefault(i => i.Id == inst.Id);
            Report(P("Blender MCP", "Blender MCP"), TweakResult.Ok($"Added {inst.Display}.", $"已新增 {inst.Display}。"));
        }
        catch (Exception ex)
        {
            Report(P("Blender MCP", "Blender MCP"), TweakResult.Fail(ex.Message, $"無法新增 MCP 實例：{ex.Message}"));
        }
    }

    private void McpOpenFolder_Click(object sender, RoutedEventArgs e) => BlenderService.OpenFolder(BlenderService.McpDir);

    private async void McpDeploy_Click(object sender, RoutedEventArgs e)
    {
        Report(P("Blender MCP", "Blender MCP"), await BlenderService.DeployBlenderMcpServer());
    }

    private async void McpAddon_Click(object sender, RoutedEventArgs e)
    {
        Report(P("Blender MCP add-on", "Blender MCP add-on"), await BlenderService.DownloadMcpAddon());
    }

    private void McpStart_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        var r = BlenderService.StartMcpInstance(inst, line => DispatcherQueue.TryEnqueue(() => AppendLog(line)));
        RefreshMcpInstances();
        Report(P("Start MCP", "啟動 MCP"), r);
    }

    private void McpStop_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        var r = BlenderService.StopMcpInstance(inst);
        RefreshMcpInstances();
        Report(P("Stop MCP", "停止 MCP"), r);
    }

    private async void McpTest_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        Report(P("Test Blender socket", "測試 Blender socket"), await BlenderService.TestMcpBlenderSocket(inst));
    }

    private void McpConfigure_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        var cfg = BlenderService.ConfigFor(inst);
        var results = new[]
        {
            BlenderService.ConfigureCodexBlenderMcp(cfg),
            BlenderService.ConfigureClaudeBlenderMcpJson(cfg),
            BlenderService.ConfigureOpenCodeBlenderMcp(cfg),
        };
        var failed = results.FirstOrDefault(r => !r.Success);
        Report(P("Agent MCP config", "Agent MCP 設定"), failed ?? TweakResult.Ok(
            $"Saved MCP config for Codex, Claude Code JSON and OpenCode as \"{cfg.Name}\".",
            $"已為 Codex、Claude Code JSON 同 OpenCode 儲存「{cfg.Name}」MCP 設定。"));
    }

    private async void McpBundle_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        var dir = await FileDialogs.OpenFolderAsync(P("Export MCP configs and skill", "匯出 MCP 設定同 skill"));
        if (dir is null) return;
        Report(P("Export MCP bundle", "匯出 MCP 設定包"), await BlenderService.ExportMcpAgentBundle(inst, dir));
    }

    private async void McpSkill_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        var dir = await FileDialogs.OpenFolderAsync(P("Pick skill folder", "揀 skill 資料夾"));
        if (dir is null) return;
        Report(P("Generate skill", "產生 skill"), await BlenderService.GenerateMcpSkill(inst, dir));
    }

    private void McpDelete_Click(object sender, RoutedEventArgs e)
    {
        var inst = SelectedMcp();
        if (inst is null) return;
        BlenderService.DeleteMcpInstance(inst);
        RefreshMcpInstances();
        Report(P("Delete MCP instance", "刪除 MCP 實例"), TweakResult.Ok($"Deleted {inst.Display}.", $"已刪除 {inst.Display}。"));
    }

    // ── Log ──────────────────────────────────────────────────────────────────

    private void AppendLog(string line)
    {
        if (LogText.Text.Length > 60000) LogText.Text = LogText.Text.Substring(LogText.Text.Length - 40000);
        LogText.Text += (LogText.Text.Length == 0 ? "" : "\n") + line;
        LogScroller.UpdateLayout();
        LogScroller.ChangeView(null, LogScroller.ScrollableHeight, null);
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e) => LogText.Text = "";

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void Report(string title, TweakResult r)
    {
        EngineBar.IsOpen = true;
        EngineBar.ActionButton = null;
        EngineBar.Severity = r.Success ? InfoBarSeverity.Success : InfoBarSeverity.Error;
        EngineBar.Title = title;
        EngineBar.Message = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
    }
}
