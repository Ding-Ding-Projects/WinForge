using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// YAML ↔ JSON · 轉換 — a two-way converter between a practical YAML subset and JSON, done in
/// pure managed C# (System.Text.Json + a hand-written YAML parser/emitter in <see cref="YamlJsonService"/>).
/// Pick a direction, paste input, convert, copy the result. Errors are bilingual and never throw.
/// </summary>
public sealed partial class YamlJsonModule : Page
{
    private bool _suppress;

    public YamlJsonModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Convert(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    // 0 = YAML→JSON, 1 = JSON→YAML
    private int Direction => DirectionBox.SelectedIndex < 0 ? 0 : DirectionBox.SelectedIndex;

    private void Render()
    {
        Header.Title = "YAML ↔ JSON · 轉換";
        HeaderBlurb.Text = P(
            "Convert between YAML and JSON entirely offline, in pure managed code. A practical YAML subset is supported — see the note below.",
            "完全離線、純管理代碼將 YAML 與 JSON 互轉。支援實用的 YAML 子集 — 詳見下面說明。");

        DirectionLabel.Text = P("Direction", "方向");
        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");
        ConvertBtn.Content = P("Convert", "轉換");
        CopyBtn.Content = P("Copy output", "複製輸出");
        SampleBtn.Content = P("Load sample", "載入範例");
        ClearBtn.Content = P("Clear", "清除");
        LimitsText.Text = P(YamlJsonService.LimitationsEn, YamlJsonService.LimitationsZh);

        int keep = DirectionBox.SelectedIndex;
        _suppress = true;
        DirectionBox.Items.Clear();
        DirectionBox.Items.Add(P("YAML → JSON", "YAML → JSON"));
        DirectionBox.Items.Add(P("JSON → YAML", "JSON → YAML"));
        DirectionBox.SelectedIndex = keep < 0 ? 0 : keep;
        _suppress = false;
    }

    private void Direction_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Convert();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Convert();
    }

    private void Convert_Click(object sender, RoutedEventArgs e) => Convert();

    private void Convert()
    {
        string input = InputBox?.Text ?? "";
        if (string.IsNullOrWhiteSpace(input))
        {
            OutputBox.Text = "";
            Info.IsOpen = false;
            return;
        }

        YamlJsonService.ConvertResult r = Direction == 0
            ? YamlJsonService.YamlToJson(input)
            : YamlJsonService.JsonToYaml(input);

        if (r.Ok)
        {
            OutputBox.Text = r.Output;
            ShowInfo(InfoBarSeverity.Success,
                P("Converted", "已轉換"),
                Direction == 0 ? P("YAML parsed to JSON.", "YAML 已解析為 JSON。")
                               : P("JSON serialized to YAML.", "JSON 已序列化為 YAML。"));
        }
        else
        {
            OutputBox.Text = "";
            ShowInfo(InfoBarSeverity.Error, P("Could not convert", "無法轉換"),
                FriendlyError(r.Error));
        }
    }

    private string FriendlyError(string? code)
    {
        code ??= "";
        string tail = code.Contains(':') ? code.Substring(code.IndexOf(':') + 1) : "";
        if (code.StartsWith("empty"))
            return P("The input is empty.", "輸入是空的。");
        if (code.StartsWith("invalid-json"))
            return P("That is not valid JSON. " + tail, "這不是有效的 JSON。" + tail);
        if (code.StartsWith("yaml-parse") || code.StartsWith("yaml-lex"))
            return P("That is not valid YAML (subset). " + tail, "這不是有效的 YAML（子集）。" + tail);
        return P("Conversion failed. " + tail, "轉換失敗。" + tail);
    }

    private void ShowInfo(InfoBarSeverity sev, string title, string msg)
    {
        Info.Severity = sev;
        Info.Title = title;
        Info.Message = msg;
        Info.IsOpen = true;
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? "";
            if (string.IsNullOrEmpty(text))
            {
                ShowInfo(InfoBarSeverity.Informational, P("Nothing to copy", "沒有可複製的內容"),
                    P("Convert something first.", "先轉換一下。"));
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            ShowInfo(InfoBarSeverity.Success, P("Copied", "已複製"),
                P("Output copied to the clipboard.", "輸出已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            ShowInfo(InfoBarSeverity.Error, P("Copy failed", "複製失敗"), ex.Message);
        }
    }

    private void Sample_Click(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        if (Direction == 0)
        {
            InputBox.Text =
                "# WinForge sample config\n" +
                "name: WinForge\n" +
                "version: 11\n" +
                "enabled: true\n" +
                "reactor:\n" +
                "  mode: 5\n" +
                "  coolant: \"heavy water\"\n" +
                "  rods: 121\n" +
                "modules:\n" +
                "  - awake\n" +
                "  - yamljson\n" +
                "  - reactor\n" +
                "authors:\n" +
                "  - name: Claude\n" +
                "    role: agent\n";
        }
        else
        {
            InputBox.Text =
                "{\n" +
                "  \"name\": \"WinForge\",\n" +
                "  \"version\": 11,\n" +
                "  \"enabled\": true,\n" +
                "  \"reactor\": { \"mode\": 5, \"coolant\": \"heavy water\", \"rods\": 121 },\n" +
                "  \"modules\": [\"awake\", \"yamljson\", \"reactor\"],\n" +
                "  \"authors\": [ { \"name\": \"Claude\", \"role\": \"agent\" } ]\n" +
                "}\n";
        }
        _suppress = false;
        Convert();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        _suppress = true;
        InputBox.Text = "";
        OutputBox.Text = "";
        _suppress = false;
        Info.IsOpen = false;
    }
}
