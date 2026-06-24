using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Catalog;
using WinForge.Controls;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// HashiCorp Packer（映像建置器）· A first-class WinUI front-end over the official <c>packer</c> CLI:
/// pick a working folder, list *.pkr.hcl / *.json templates, edit -var key/values and -var-file lists,
/// pick -only / -except build targets parsed from <c>packer inspect</c>, run init / validate / fmt / build
/// with live streamed output and cancel, auto-install the binary via winget. Fully bilingual.
/// </summary>
public sealed partial class PackerModule : Page
{
    private List<TweakDefinition>? _ops;
    private CancellationTokenSource? _cts;
    private readonly StringBuilder _log = new();

    public PackerModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; try { _cts?.Cancel(); } catch { } };
        Loaded += async (_, _) =>
        {
            Render();
            AddVarRow("", "");
            RefreshFolder();
            PopulateOps(string.Empty);
            await CheckEngine();
        };
    }

    private void OnLang(object? sender, EventArgs e) { Render(); PopulateOps(OpsFilter.Text ?? string.Empty); }
    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        HeaderTitle.Text = "Packer · 映像建置器";
        HeaderBlurb.Text = P(
            "Run HashiCorp Packer from inside WinForge: pick a working folder, edit variables, then run init / validate / fmt / build with live streaming output. WinForge only shells out to the official packer binary (BUSL-1.1).",
            "喺 WinForge 直接用 HashiCorp Packer：揀工作資料夾、編輯變數，再行 init / validate / fmt / build，即時串流輸出。WinForge 只係呼叫官方 packer binary（BUSL-1.1）。");

        FolderHeader.Text = P("Working folder & templates", "工作資料夾與範本");
        PickFolderBtn.Content = P("Pick folder…", "揀資料夾…");
        OpenFolderBtn.Content = P("Open in Explorer", "喺檔案總管開啟");
        RefreshBtn.Content = P("Refresh", "重新整理");
        TemplatesLabel.Text = P("Templates (*.pkr.hcl / *.json)", "範本（*.pkr.hcl／*.json）");
        TargetsLabel.Text = P("Build targets (-only / -except)", "建置目標（-only／-except）");
        TargetOnly.Content = P("Only selected", "只建選取");
        TargetExcept.Content = P("Except selected", "排除選取");
        InspectTargetsBtn.Content = P("Inspect →", "檢視 →");

        VarsHeader.Text = P("Variables", "變數");
        VarsBlurb.Text = P(
            "Add -var key=value pairs and pick -var-file (.pkrvars.hcl) files. These are passed to validate and build.",
            "加入 -var key=value 對，並揀 -var-file（.pkrvars.hcl）檔案。會傳畀 validate 同 build。");
        AddVarBtn.Content = P("+ Add variable", "＋ 加變數");
        VarFilesLabel.Text = P("Variable files (-var-file)", "變數檔案（-var-file）");
        AddVarFileBtn.Content = P("Add var-file…", "加變數檔…");
        RefreshVarFilesBtn.Content = P("Scan folder", "掃描資料夾");

        RunHeader.Text = P("Run", "執行");
        InitBtn.Content = P("Init", "Init");
        ValidateBtn.Content = P("Validate", "Validate");
        FmtBtn.Content = P("Format", "格式化");
        BuildBtn.Content = P("Build", "Build");
        CancelBtn.Content = P("Cancel", "取消");
        ConsoleLabel.Text = P("Console output", "主控台輸出");
        ClearConsoleBtn.Content = P("Clear", "清除");
        SaveLogBtn.Content = P("Save log…", "儲存記錄…");

        OpsHeader.Text = P("More operations", "更多操作");
        OpsFilter.PlaceholderText = P("Filter operations…", "篩選操作…");
    }

    // ===================== Engine check / auto-install =====================

    private async Task CheckEngine()
    {
        bool ok = await PackerService.IsInstalledAsync();
        if (ok)
        {
            EngineBar.IsOpen = false;
            EngineBar.ActionButton = null;
            var v = await PackerService.VersionAsync();
            if (!string.IsNullOrWhiteSpace(v))
            {
                VersionText.Text = v;
                VersionPill.Visibility = Visibility.Visible;
            }
            SetRunEnabled(true);
            return;
        }
        VersionPill.Visibility = Visibility.Collapsed;
        SetRunEnabled(false);
        EngineBar.IsOpen = true;
        EngineBar.Severity = InfoBarSeverity.Warning;
        EngineBar.Title = P("Packer not found", "搵唔到 Packer");
        EngineBar.Message = P("Click to install HashiCorp Packer automatically (winget) — no restart needed.",
            "撳一下自動安裝 HashiCorp Packer（winget）— 唔使重開。");
        EngineBar.ActionButton = EngineBars.AutoInstallButton(
            PackerService.WingetId, "Install Packer automatically", "自動安裝 Packer",
            async () => { await CheckEngine(); });
    }

    private void SetRunEnabled(bool on)
    {
        InitBtn.IsEnabled = on;
        ValidateBtn.IsEnabled = on;
        FmtBtn.IsEnabled = on;
        BuildBtn.IsEnabled = on;
        InspectTargetsBtn.IsEnabled = on;
    }

    // ===================== Working folder =====================

    private async void PickFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = await FileDialogs.OpenFolderAsync(P("Pick a Packer working folder", "揀 Packer 工作資料夾"));
        if (string.IsNullOrEmpty(dir)) return;
        PackerService.WorkingDir = dir;
        RefreshFolder();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        if (string.IsNullOrEmpty(dir)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true }); }
        catch { }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshFolder();

    private void RefreshFolder()
    {
        var dir = PackerService.WorkingDir;
        FolderPath.Text = string.IsNullOrEmpty(dir)
            ? P("(no folder selected)", "（未揀資料夾）")
            : dir;

        var templates = PackerService.ListTemplates(dir);
        TemplatesList.ItemsSource = templates.Select(Path.GetFileName).ToList();
        bool none = templates.Count == 0;
        NoTemplates.Visibility = none && !string.IsNullOrEmpty(dir) ? Visibility.Visible : Visibility.Collapsed;
        NoTemplates.Text = P("No *.pkr.hcl / *.json templates found in this folder.",
            "呢個資料夾搵唔到 *.pkr.hcl／*.json 範本。");

        var varFiles = PackerService.ListVarFiles(dir);
        VarFilesList.ItemsSource = varFiles.Select(Path.GetFileName).ToList();
        _varFilesFull = varFiles.ToList();

        TargetsList.ItemsSource = null;

        bool hasFolder = !string.IsNullOrEmpty(dir);
        OpenFolderBtn.IsEnabled = hasFolder;
    }

    private List<string> _varFilesFull = new();

    private void Templates_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private async void InspectTargets_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        if (string.IsNullOrEmpty(dir)) { Status(P("Pick a working folder first.", "請先揀工作資料夾。")); return; }
        var target = SelectedTemplatePath() ?? dir;
        Status(P("Inspecting…", "檢視緊…"));
        var targets = await PackerService.ListBuildTargetsAsync(target);
        TargetsList.ItemsSource = targets.ToList();
        Status(targets.Count == 0
            ? P("No build targets parsed (template may be invalid).", "解析唔到建置目標（範本可能無效）。")
            : P($"{targets.Count} build target(s) found.", $"搵到 {targets.Count} 個建置目標。"));
    }

    private string? SelectedTemplatePath()
    {
        var dir = PackerService.WorkingDir;
        if (string.IsNullOrEmpty(dir)) return null;
        if (TemplatesList.SelectedItem is string name)
            return Path.Combine(dir, name);
        return null;
    }

    // ===================== Variables =====================

    private void AddVar_Click(object sender, RoutedEventArgs e) => AddVarRow("", "");

    private void AddVarRow(string key, string value)
    {
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.4, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var k = new TextBox { Text = key, PlaceholderText = P("key", "鍵") };
        var v = new TextBox { Text = value, PlaceholderText = P("value", "值") };
        var del = new Button { Content = "", FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Segoe Fluent Icons") };
        del.Click += (_, _) => VarsPanel.Children.Remove(row);
        Grid.SetColumn(k, 0);
        Grid.SetColumn(v, 1);
        Grid.SetColumn(del, 2);
        row.Children.Add(k);
        row.Children.Add(v);
        row.Children.Add(del);
        VarsPanel.Children.Add(row);
    }

    private List<(string, string)> CollectVars()
    {
        var list = new List<(string, string)>();
        foreach (var child in VarsPanel.Children.OfType<Grid>())
        {
            var boxes = child.Children.OfType<TextBox>().ToList();
            if (boxes.Count >= 2)
            {
                var key = boxes[0].Text?.Trim() ?? "";
                if (key.Length > 0) list.Add((key, boxes[1].Text ?? ""));
            }
        }
        return list;
    }

    private async void AddVarFile_Click(object sender, RoutedEventArgs e)
    {
        var filters = new List<FileDialogs.Filter>
        {
            new("Packer var files", "*.pkrvars.hcl;*.pkrvars.json"),
            new("All files", "*.*"),
        };
        var path = await FileDialogs.OpenFileAsync(filters, P("Pick a variable file", "揀變數檔案"));
        if (string.IsNullOrEmpty(path)) return;
        if (!_varFilesFull.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            _varFilesFull.Add(path);
            RebindVarFiles();
            // auto-select the newly added one
            VarFilesList.SelectedItems.Add(Path.GetFileName(path));
        }
    }

    private void RefreshVarFiles_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        _varFilesFull = PackerService.ListVarFiles(dir).ToList();
        RebindVarFiles();
    }

    private void RebindVarFiles() => VarFilesList.ItemsSource = _varFilesFull.Select(Path.GetFileName).ToList();

    private List<string> CollectVarFiles()
    {
        var selectedNames = VarFilesList.SelectedItems.OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return _varFilesFull.Where(f => selectedNames.Contains(Path.GetFileName(f) ?? "")).ToList();
    }

    // ===================== Run (streamed) =====================

    private string TargetArgs()
    {
        var sel = TargetsList.SelectedItems.OfType<string>().ToList();
        if (sel.Count == 0) return "";
        var flag = TargetExcept.IsChecked == true ? "-except" : "-only";
        return $" {flag}={string.Join(",", sel)}";
    }

    private async void Init_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        if (!RequireFolder(dir)) return;
        await StreamRun($"init {PackerService.Q(dir!)}", P("packer init", "packer init"));
        // plugins may have changed — let the user inspect targets again
    }

    private async void Validate_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        if (!RequireFolder(dir)) return;
        var vars = PackerService.BuildVarArgs(CollectVars(), CollectVarFiles());
        var target = SelectedTemplatePath() ?? dir!;
        await StreamRun($"validate{vars}{TargetArgs()} {PackerService.Q(target)}", P("packer validate", "packer validate"));
    }

    private async void Fmt_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        if (!RequireFolder(dir)) return;
        await StreamRun($"fmt {PackerService.Q(dir!)}", P("packer fmt", "packer fmt"));
    }

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        var dir = PackerService.WorkingDir;
        if (!RequireFolder(dir)) return;
        var vars = PackerService.BuildVarArgs(CollectVars(), CollectVarFiles());
        var target = SelectedTemplatePath() ?? dir!;
        await StreamRun($"build{vars}{TargetArgs()} {PackerService.Q(target)}", P("packer build", "packer build"));
    }

    private bool RequireFolder(string? dir)
    {
        if (string.IsNullOrEmpty(dir))
        {
            Status(P("Pick a working folder first.", "請先揀工作資料夾。"));
            EngineBar.IsOpen = true;
            EngineBar.Severity = InfoBarSeverity.Informational;
            EngineBar.Title = P("No working folder", "未揀工作資料夾");
            EngineBar.Message = P("Pick a folder that contains your .pkr.hcl templates.", "請揀一個含有 .pkr.hcl 範本嘅資料夾。");
            EngineBar.ActionButton = null;
            return false;
        }
        return true;
    }

    private async Task StreamRun(string args, string label)
    {
        if (PackerService.IsRunning) { Status(P("Already running.", "已經喺度行緊。")); return; }
        _cts = new CancellationTokenSource();
        SetRunning(true);
        AppendLine($"$ packer {args}", header: true);
        Status(P($"Running {label}…", $"執行 {label}…"));
        TweakResult r;
        try
        {
            r = await PackerService.StreamAsync(args, PackerService.WorkingDir,
                line => DispatcherQueue.TryEnqueue(() => AppendLine(line)), _cts.Token);
        }
        catch (Exception ex)
        {
            r = TweakResult.Fail(ex.Message, $"出錯：{ex.Message}");
        }
        finally
        {
            SetRunning(false);
            _cts?.Dispose();
            _cts = null;
        }
        var msg = (Loc.I.IsCantonesePrimary ? r.Message?.Zh : r.Message?.En) ?? "";
        AppendLine(r.Success ? $"✓ {msg}" : $"✗ {msg}", header: true);
        Status((r.Success ? "✓ " : "✗ ") + msg);
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try { _cts?.Cancel(); } catch { }
        PackerService.Cancel();
        Status(P("Cancelling…", "取消緊…"));
    }

    private void SetRunning(bool on)
    {
        BusyRing.IsActive = on;
        CancelBtn.IsEnabled = on;
        InitBtn.IsEnabled = !on;
        ValidateBtn.IsEnabled = !on;
        FmtBtn.IsEnabled = !on;
        BuildBtn.IsEnabled = !on;
    }

    // ===================== Console =====================

    private void AppendLine(string line, bool header = false)
    {
        var text = header ? $"\n{line}\n" : line + "\n";
        _log.Append(text);
        // keep the on-screen buffer bounded
        var shown = ConsoleText.Text + text;
        if (shown.Length > 60000) shown = shown[^60000..];
        ConsoleText.Text = shown;
        ConsoleScroll.UpdateLayout();
        ConsoleScroll.ChangeView(null, ConsoleScroll.ScrollableHeight, null, true);
    }

    private void Status(string s) => StatusText.Text = s;

    private void ClearConsole_Click(object sender, RoutedEventArgs e)
    {
        _log.Clear();
        ConsoleText.Text = "";
        Status("");
    }

    private async void SaveLog_Click(object sender, RoutedEventArgs e)
    {
        if (_log.Length == 0) { Status(P("Nothing to save.", "冇嘢可以儲存。")); return; }
        var path = await FileDialogs.SaveFileAsync(
            $"packer-{DateTime.Now:yyyyMMdd-HHmmss}.log",
            new[] { ".log", ".txt" });
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            await File.WriteAllTextAsync(path, _log.ToString());
            Status(P($"Saved to {path}", $"已儲存到 {path}"));
        }
        catch (Exception ex) { Status(P($"Save failed: {ex.Message}", $"儲存失敗：{ex.Message}")); }
    }

    // ===================== Operations (TweakCards) =====================

    private void OpsFilter_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            PopulateOps(sender.Text ?? string.Empty);
    }

    private void PopulateOps(string filter)
    {
        _ops ??= PackerOperations.All().ToList();
        OpsPanel.Children.Clear();
        IEnumerable<TweakDefinition> shown = _ops;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            var f = filter.Trim().ToLowerInvariant();
            shown = _ops.Where(t => t.SearchHaystack.Contains(f));
        }
        foreach (var op in shown)
        {
            var card = new TweakCard();
            card.SetTweak(op);
            OpsPanel.Children.Add(card);
        }
    }
}
