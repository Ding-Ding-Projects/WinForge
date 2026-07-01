using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// quicktype 程式碼生成模組 · quicktype code generator — paste JSON / JSON Schema / TypeScript / GraphQL /
/// Postman, pick a target language (C#, TypeScript, Python, Go, Rust, Java, Kotlin, Swift, …) and generate
/// typed source in three clicks. Wraps the quicktype npm CLI via ShellRunner; detects Node + quicktype and
/// offers one-click installs. Output has Copy + Save as…. Everything in-app, bilingual.
/// </summary>
public sealed partial class QuickTypeModule : Page
{
    private bool _busy;
    private string _lastOutput = "";

    public QuickTypeModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += async (_, _) => { BuildPickers(); Render(); SeedSample(); await Detect(); };
        Unloaded += (_, _) => Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // ---- pickers ---------------------------------------------------------------------------

    private void BuildPickers()
    {
        InputKindPicker.Items.Clear();
        foreach (var k in QuickTypeService.InputKinds)
            InputKindPicker.Items.Add(new ComboBoxItem { Content = $"{k.En} · {k.Zh}", Tag = k });
        InputKindPicker.SelectedIndex = 0;

        TargetPicker.Items.Clear();
        foreach (var t in QuickTypeService.Targets)
            TargetPicker.Items.Add(new ComboBoxItem { Content = $"{t.En} · {t.Zh}", Tag = t });
        TargetPicker.SelectedIndex = 0; // C#

        FrameworkPicker.Items.Clear();
        FrameworkPicker.Items.Add(new ComboBoxItem { Content = "System.Text.Json", Tag = "SystemTextJson" });
        FrameworkPicker.Items.Add(new ComboBoxItem { Content = "Newtonsoft.Json", Tag = "NewtonSoft" });
        FrameworkPicker.SelectedIndex = 0;
    }

    private void Render()
    {
        Header.Title = "quicktype · JSON 轉型別";
        Header.Subtitle = P(
            "Turn JSON, JSON Schema, TypeScript, GraphQL or a Postman collection into typed code in C#, TypeScript, Python, Go, Rust, Java, Swift and more.",
            "將 JSON、JSON Schema、TypeScript、GraphQL 或 Postman 集合轉成 C#、TypeScript、Python、Go、Rust、Java、Swift 等語言嘅型別程式碼。");

        InputTitle.Text = P("Input", "輸入");
        LoadFileBtn.Content = P("Load file…", "載入檔案…");
        ClearInputBtn.Content = P("Clear", "清除");
        InputKindLabel.Text = P("Input kind", "輸入種類");
        InputBox.PlaceholderText = P("Paste JSON (or your chosen input kind) here…", "喺度貼上 JSON（或你揀嘅輸入種類）…");

        OptionsTitle.Text = P("Options", "選項");
        TargetLabel.Text = P("Target language", "目標語言");
        TopLevelLabel.Text = P("Top-level type name", "頂層型別名稱");
        JustTypesCheck.Content = P("Just types (no serialization helpers)", "淨係型別（唔要序列化 helper）");
        NamespaceLabel.Text = P("Namespace", "命名空間");
        FrameworkLabel.Text = P("JSON framework", "JSON 框架");
        ListArrayCheck.Content = P("Use List<T> instead of arrays", "用 List<T> 代替陣列");
        GenerateBtn.Content = P("Generate", "生成");

        OutputTitle.Text = P("Output", "輸出");
        CopyBtn.Content = P("Copy", "複製");
        SaveBtn.Content = P("Save as…", "另存…");

        CliBar.Title = P("quicktype CLI not found", "搵唔到 quicktype CLI");
        CliBar.Message = P(
            "The quicktype command-line tool is required. Install it globally with npm, then it is detected automatically.",
            "需要 quicktype 命令列工具。用 npm 全域安裝後會自動偵測到。");
        InstallCliBtn.Content = P("Install quicktype (npm -g)", "安裝 quicktype（npm -g）");

        UpdateCSharpVisibility();
    }

    private void SeedSample()
    {
        if (!string.IsNullOrWhiteSpace(InputBox.Text)) return;
        InputBox.Text =
            "{\n" +
            "  \"id\": 42,\n" +
            "  \"name\": \"Ada Lovelace\",\n" +
            "  \"active\": true,\n" +
            "  \"tags\": [\"math\", \"code\"],\n" +
            "  \"profile\": { \"city\": \"London\", \"score\": 9.5 }\n" +
            "}\n";
    }

    // ---- detection / install ---------------------------------------------------------------

    private async Task Detect()
    {
        // Node check.
        bool node = await QuickTypeService.NodeAvailableAsync();
        if (!node)
        {
            NodeBar.IsOpen = true;
            NodeBar.Severity = InfoBarSeverity.Warning;
            NodeBar.Title = P("Node.js not found", "搵唔到 Node.js");
            NodeBar.Message = P("quicktype installs via npm, which needs Node.js. Install Node.js once, then install quicktype.",
                "quicktype 用 npm 安裝，npm 需要 Node.js。先裝一次 Node.js，再裝 quicktype。");
            NodeBar.ActionButton = EngineBars.AutoInstallButton(
                "OpenJS.NodeJS.LTS", "Install Node.js automatically", "自動安裝 Node.js",
                async () => { await Detect(); }, QuickTypeService.Rescan);
        }
        else
        {
            NodeBar.IsOpen = false;
            NodeBar.ActionButton = null;
        }

        // quicktype CLI check.
        bool cli = await QuickTypeService.IsAvailableAsync();
        CliBar.IsOpen = !cli;
        GenerateBtn.IsEnabled = cli && !_busy;
    }

    private async void InstallCli_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        InstallCliBtn.IsEnabled = false;
        InstallCliBtn.Content = P("Installing…", "安裝緊…");
        Busy.IsActive = true;
        try
        {
            var r = await QuickTypeService.InstallViaNpmAsync();
            if (r.Success)
            {
                Info(InfoBarSeverity.Success, P("quicktype installed", "已安裝 quicktype"),
                    P("The quicktype CLI is ready.", "quicktype CLI 已就緒。"));
            }
            else
            {
                Info(InfoBarSeverity.Error, P("Install failed", "安裝失敗"),
                    r.Output ?? P("npm install -g quicktype failed. Is Node.js installed?",
                        "npm install -g quicktype 失敗。Node.js 裝咗未？"));
            }
        }
        finally
        {
            _busy = false;
            Busy.IsActive = false;
            InstallCliBtn.IsEnabled = true;
            InstallCliBtn.Content = P("Install quicktype (npm -g)", "安裝 quicktype（npm -g）");
        }
        await Detect();
    }

    // ---- input -----------------------------------------------------------------------------

    private async void LoadFile_Click(object sender, RoutedEventArgs e)
    {
        var kind = SelectedInputKind();
        var path = await FileDialogs.OpenFileAsync(kind.Extensions.Append(".txt").ToArray());
        if (path is null) return;
        try
        {
            InputBox.Text = await File.ReadAllTextAsync(path);
            Info(InfoBarSeverity.Informational, P("Loaded", "已載入"), Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Could not read file", "讀唔到檔案"), ex.Message);
        }
    }

    private void ClearInput_Click(object sender, RoutedEventArgs e) => InputBox.Text = "";

    private QuickTypeService.InputKind SelectedInputKind()
        => (InputKindPicker.SelectedItem as ComboBoxItem)?.Tag as QuickTypeService.InputKind
           ?? QuickTypeService.InputKinds[0];

    private QuickTypeService.TargetLang SelectedTarget()
        => (TargetPicker.SelectedItem as ComboBoxItem)?.Tag as QuickTypeService.TargetLang
           ?? QuickTypeService.Targets[0];

    private void Target_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateCSharpVisibility();

    private void UpdateCSharpVisibility()
    {
        if (CSharpOptions is null) return;
        CSharpOptions.Visibility = SelectedTarget().Lang == "csharp" ? Visibility.Visible : Visibility.Collapsed;
    }

    // ---- generate --------------------------------------------------------------------------

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var source = InputBox.Text ?? "";
        if (string.IsNullOrWhiteSpace(source))
        {
            Info(InfoBarSeverity.Warning, P("Nothing to generate", "冇嘢可以生成"),
                P("Paste or load some input first.", "請先貼上或載入輸入。"));
            return;
        }

        if (!await QuickTypeService.IsAvailableAsync())
        {
            await Detect();
            Info(InfoBarSeverity.Warning, P("quicktype not found", "搵唔到 quicktype"),
                P("Install the quicktype CLI first.", "請先安裝 quicktype CLI。"));
            return;
        }

        var opt = new QuickTypeService.GenOptions
        {
            Input = SelectedInputKind(),
            Target = SelectedTarget(),
            TopLevelName = TopLevelBox.Text ?? "Root",
            JustTypes = JustTypesCheck.IsChecked == true,
            Namespace = string.IsNullOrWhiteSpace(NamespaceBox.Text) ? null : NamespaceBox.Text,
            CSharpFramework = (FrameworkPicker.SelectedItem as ComboBoxItem)?.Tag as string,
            CSharpArrayType = ListArrayCheck.IsChecked == true,
        };

        _busy = true;
        Busy.IsActive = true;
        GenerateBtn.IsEnabled = false;
        ResultBar.IsOpen = false;
        try
        {
            var r = await QuickTypeService.GenerateAsync(source, opt);
            if (r.Success)
            {
                _lastOutput = r.Code;
                OutputBox.Text = r.Code;
                CopyBtn.IsEnabled = true;
                SaveBtn.IsEnabled = true;
                Info(InfoBarSeverity.Success, P("Generated", "已生成"),
                    P($"{opt.Target.En} code generated.", $"已生成 {opt.Target.Zh} 程式碼。"));
            }
            else
            {
                Info(InfoBarSeverity.Error, P("Generation failed", "生成失敗"),
                    string.IsNullOrWhiteSpace(r.Error) ? P("quicktype returned an error.", "quicktype 回傳錯誤。") : r.Error);
            }
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Generation failed", "生成失敗"), ex.Message);
        }
        finally
        {
            _busy = false;
            Busy.IsActive = false;
            GenerateBtn.IsEnabled = true;
        }
    }

    // ---- output ----------------------------------------------------------------------------

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastOutput)) return;
        var dp = new DataPackage();
        dp.SetText(_lastOutput);
        Clipboard.SetContent(dp);
        Info(InfoBarSeverity.Success, P("Copied", "已複製"), P("Generated code copied to clipboard.", "已複製生成嘅程式碼到剪貼簿。"));
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastOutput)) return;
        var t = SelectedTarget();
        var suggested = (string.IsNullOrWhiteSpace(TopLevelBox.Text) ? "Generated" : TopLevelBox.Text.Trim()) + t.FileExt;
        var path = await FileDialogs.SaveFileAsync(suggested, t.FileExt, ".txt");
        if (path is null) return;
        try
        {
            await File.WriteAllTextAsync(path, _lastOutput);
            Info(InfoBarSeverity.Success, P("Saved", "已儲存"), path);
        }
        catch (Exception ex)
        {
            Info(InfoBarSeverity.Error, P("Could not save", "儲存失敗"), ex.Message);
        }
    }

    // ---- helper ----------------------------------------------------------------------------

    private void Info(InfoBarSeverity sev, string title, string msg)
    {
        ResultBar.Severity = sev;
        ResultBar.Title = title;
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
