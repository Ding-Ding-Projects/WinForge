using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON 同 XML 工具 · JSON &amp; XML formatter / validator — pretty-print, minify, validate,
/// escape / unescape JSON string literals and (optionally) sort object keys. Pure managed,
/// no external processes. All output goes through <see cref="JsonToolsService"/> which never
/// throws to the UI. Bilingual (English + 粵語).
/// </summary>
public sealed partial class JsonToolsModule : Page
{
    private bool IsJson => ModeCombo.SelectedIndex != 1;
    private bool SortKeys => SortSwitch.IsOn;

    public JsonToolsModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            if (ModeCombo.SelectedIndex < 0) ModeCombo.SelectedIndex = 0;
            Render();
        };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON & XML Tools · JSON 同 XML 工具";
        HeaderBlurb.Text = P("Pretty-print, minify and validate JSON or XML — plus escape / unescape JSON string literals. Everything runs locally in WinForge; nothing leaves your PC.",
            "美化、壓縮同驗證 JSON 或者 XML — 仲可以轉義／還原 JSON 字串。全部喺 WinForge 本機處理，冇資料離開你部電腦。");
        ModeLabel.Text = P("Mode", "模式");
        SortLabel.Text = P("Sort keys", "排序鍵");
        InputLabel.Text = P("Input", "輸入");
        OutputLabel.Text = P("Output", "輸出");
        FormatBtn.Content = P("Format", "格式化");
        MinifyBtn.Content = P("Minify", "壓縮");
        ValidateBtn.Content = P("Validate", "驗證");
        EscapeBtn.Content = P("Escape string", "轉義字串");
        UnescapeBtn.Content = P("Unescape string", "還原字串");
        CopyBtn.Content = P("Copy output", "複製輸出");
        UpdateModeHints();
    }

    private void Mode_Changed(object sender, SelectionChangedEventArgs e) => UpdateModeHints();

    private void UpdateModeHints()
    {
        // Escape/Unescape only makes sense for JSON string literals.
        if (EscapeRow is not null)
            EscapeRow.Visibility = IsJson ? Visibility.Visible : Visibility.Collapsed;
        if (SortLabel is not null)
        {
            SortLabel.Opacity = IsJson ? 1.0 : 0.5;
            if (SortSwitch is not null) SortSwitch.IsEnabled = IsJson;
        }
    }

    private void Apply(JsonToolsService.ToolResult r)
    {
        if (r.Ok)
        {
            OutputBox.Text = r.Output;
            StatusText.Text = r.Message;
            StatBox.Text = P($"{r.NodeCount} node(s) · {r.Output.Length} char(s)",
                             $"{r.NodeCount} 個節點 · {r.Output.Length} 個字元");
        }
        else
        {
            // Keep prior output; surface the friendly/error message only.
            StatusText.Text = r.Message;
        }
    }

    private void Format_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        Apply(IsJson
            ? JsonToolsService.FormatJson(input, SortKeys, P)
            : JsonToolsService.FormatXml(input, P));
    }

    private void Minify_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        Apply(IsJson
            ? JsonToolsService.MinifyJson(input, SortKeys, P)
            : JsonToolsService.MinifyXml(input, P));
    }

    private void Validate_Click(object sender, RoutedEventArgs e)
    {
        var input = InputBox.Text;
        Apply(IsJson
            ? JsonToolsService.ValidateJson(input, P)
            : JsonToolsService.ValidateXml(input, P));
    }

    private void Escape_Click(object sender, RoutedEventArgs e)
        => Apply(JsonToolsService.EscapeJson(InputBox.Text, P));

    private void Unescape_Click(object sender, RoutedEventArgs e)
        => Apply(JsonToolsService.UnescapeJson(InputBox.Text, P));

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = OutputBox.Text ?? string.Empty;
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy — the output is empty.", "冇嘢可以複製 — 輸出係空嘅。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied output to the clipboard.", "已將輸出複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
    }
}
