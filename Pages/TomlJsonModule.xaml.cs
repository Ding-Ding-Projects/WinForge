using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// TOML ↔ JSON 轉換器 · TOML ↔ JSON converter. Pick a direction, paste in the source, convert.
/// A hand-written TOML subset parser/writer (no NuGet). Never throws — errors show in the InfoBar.
/// </summary>
public sealed partial class TomlJsonModule : Page
{
    private bool _ready;

    public TomlJsonModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); _ready = true; Convert(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private bool TomlToJsonSelected => DirectionBox.SelectedIndex != 1; // 0 = TOML→JSON (default)

    private void Render()
    {
        Header.Title = "TOML ↔ JSON · TOML ↔ JSON 轉換";
        HeaderBlurb.Text = P("Convert between TOML and JSON, both ways, fully offline. A hand-written parser covers a practical TOML subset — no external tools.",
            "TOML 同 JSON 互相轉換，兩個方向都得，全程離線。內置手寫解析器支援實用嘅 TOML 子集，唔使外部工具。");
        DirectionLabel.Text = P("Direction", "方向");
        ConvertButton.Content = P("Convert", "轉換");
        SwapButton.Content = P("Swap ⇄", "對調 ⇄");
        CopyButton.Content = P("Copy output", "複製結果");
        NotesText.Text = P("Supports comments, dotted keys, tables, [[array-of-tables]], basic/literal & multiline strings, integers (with _, hex/oct/bin), floats, booleans, arrays and inline tables. Datetimes are kept as strings; JSON null becomes an empty string in TOML.",
            "支援註解、點分鍵、表、[[表陣列]]、基本／文字／多行字串、整數（含 _、十六／八／二進制）、浮點、布林、陣列同內聯表。日期時間會保留做字串；JSON 嘅 null 喺 TOML 會變空字串。");

        int sel = DirectionBox.SelectedIndex;
        DirectionBox.Items.Clear();
        DirectionBox.Items.Add(P("TOML → JSON", "TOML → JSON"));
        DirectionBox.Items.Add(P("JSON → TOML", "JSON → TOML"));
        DirectionBox.SelectedIndex = sel < 0 ? 0 : sel;

        UpdateIoLabels();
    }

    private void UpdateIoLabels()
    {
        if (TomlToJsonSelected)
        {
            InputLabel.Text = P("TOML input", "TOML 輸入");
            OutputLabel.Text = P("JSON output", "JSON 輸出");
        }
        else
        {
            InputLabel.Text = P("JSON input", "JSON 輸入");
            OutputLabel.Text = P("TOML output", "TOML 輸出");
        }
    }

    private void Direction_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        UpdateIoLabels();
        Convert();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        Convert();
    }

    private void Convert_Click(object sender, RoutedEventArgs e) => Convert();

    private void Swap_Click(object sender, RoutedEventArgs e)
    {
        // Feed the current output back as input, and flip the direction.
        string outText = OutputBox.Text ?? string.Empty;
        DirectionBox.SelectedIndex = TomlToJsonSelected ? 1 : 0; // triggers Direction_Changed → UpdateIoLabels
        if (!string.IsNullOrWhiteSpace(outText))
            InputBox.Text = outText; // triggers Input_Changed → Convert
        else
            Convert();
    }

    private void Convert()
    {
        try
        {
            string input = InputBox.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                OutputBox.Text = string.Empty;
                Status.IsOpen = false;
                return;
            }

            TomlJsonService.Result r = TomlToJsonSelected
                ? TomlJsonService.TomlToJson(input)
                : TomlJsonService.JsonToToml(input);

            if (r.Ok)
            {
                OutputBox.Text = r.Output;
                Status.Severity = InfoBarSeverity.Success;
                Status.Title = P("Converted", "已轉換");
                Status.Message = TomlToJsonSelected
                    ? P("TOML parsed and emitted as pretty JSON.", "TOML 已解析並輸出為漂亮 JSON。")
                    : P("JSON walked and emitted as TOML.", "JSON 已遍歷並輸出為 TOML。");
                Status.IsOpen = true;
            }
            else
            {
                OutputBox.Text = string.Empty;
                Status.Severity = InfoBarSeverity.Error;
                Status.Title = P("Could not convert", "轉換唔到");
                Status.Message = r.Error ?? P("Unknown error.", "不明錯誤。");
                Status.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            OutputBox.Text = string.Empty;
            Status.Severity = InfoBarSeverity.Error;
            Status.Title = P("Could not convert", "轉換唔到");
            Status.Message = ex.Message;
            Status.IsOpen = true;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                Status.Severity = InfoBarSeverity.Informational;
                Status.Title = P("Nothing to copy", "冇嘢可以複製");
                Status.Message = P("Convert something first.", "先轉換啲嘢。");
                Status.IsOpen = true;
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            Status.Severity = InfoBarSeverity.Success;
            Status.Title = P("Copied", "已複製");
            Status.Message = P("Output copied to the clipboard.", "結果已複製到剪貼簿。");
            Status.IsOpen = true;
        }
        catch (Exception ex)
        {
            Status.Severity = InfoBarSeverity.Error;
            Status.Title = P("Copy failed", "複製失敗");
            Status.Message = ex.Message;
            Status.IsOpen = true;
        }
    }
}
