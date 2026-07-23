using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// JSON 扁平化／還原 · JSON flatten / unflatten. Nested JSON ⇄ single flat object of
/// dotted/bracketed keys (e.g. {"a":{"b":1},"arr":[10]} → {"a.b":1,"arr[0]":10}).
/// Live re-render on any change; pretty-printed output; Copy; distinct bilingual errors;
/// never throws. Pure managed System.Text.Json via JsonFlattenService.
/// </summary>
public sealed partial class JsonFlattenModule : Page
{
    private bool _suppress;

    public JsonFlattenModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "JSON Flatten · JSON 扁平化";
        HeaderBlurb.Text = P(
            "Turn nested JSON into a single flat object of dotted keys (arr[0], a.b), or rebuild the nested shape from a flat one. Everything stays on your PC.",
            "將巢狀 JSON 變做一個由點分隔鍵組成嘅扁平物件（arr[0]、a.b），或者由扁平嘅還原返巢狀結構。全部喺你部電腦度做，唔會上網。");

        DirectionLabel.Text = P("Direction", "方向");
        SeparatorLabel.Text = P("Separator", "分隔符");
        InputLabel.Text = P("Input JSON", "輸入 JSON");
        OutputLabel.Text = P("Output JSON", "輸出 JSON");
        CopyButton.Content = P("Copy", "複製");

        _suppress = true;
        int keep = DirectionBox.SelectedIndex < 0 ? 0 : DirectionBox.SelectedIndex;
        DirectionBox.Items.Clear();
        DirectionBox.Items.Add(P("Flatten", "扁平化"));
        DirectionBox.Items.Add(P("Unflatten", "還原"));
        DirectionBox.SelectedIndex = keep;
        _suppress = false;

        Recompute();
    }

    private void Direction_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Separator_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        Recompute();
    }

    private void Recompute()
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            string sep = SeparatorBox?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(sep)) sep = ".";

            bool unflatten = DirectionBox?.SelectedIndex == 1;
            var r = unflatten
                ? JsonFlattenService.Unflatten(input, sep)
                : JsonFlattenService.Flatten(input, sep);

            if (r.Ok)
            {
                OutputBox.Text = r.Output;
                StatusText.Text = string.IsNullOrWhiteSpace(input)
                    ? P("Waiting for input…", "等緊你輸入…")
                    : P("Done.", "搞掂。");
            }
            else
            {
                OutputBox.Text = string.Empty;
                StatusText.Text = P(r.ErrorEn ?? "Something went wrong.", r.ErrorZh ?? "出咗啲問題。");
            }
        }
        catch (Exception ex)
        {
            // Belt-and-braces: the service never throws, but the UI stays alive regardless.
            OutputBox.Text = string.Empty;
            StatusText.Text = P("Unexpected error — " + ex.Message, "非預期錯誤 — " + ex.Message);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = OutputBox?.Text ?? string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時無嘢可以複製。");
                return;
            }
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Copied to clipboard.", "已經複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not copy — " + ex.Message, "複製唔到 — " + ex.Message);
        }
    }
}
