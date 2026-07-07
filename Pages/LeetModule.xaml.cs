using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 花式文字 / Unicode 風格轉換器 · Fancy-text / Unicode styler — type text and get it rendered in many
/// Unicode "font" styles (Bold, Italic, Fraktur, Circled, Fullwidth, strikethrough, leetspeak, …), each
/// a copyable row. Pure managed C#, live on TextChanged, robust & never-throws. No redirect. Bilingual.
/// </summary>
public sealed partial class LeetModule : Page
{
    /// <summary>One row in the styles list; notifies the UI when its output changes.</summary>
    public sealed class Row : INotifyPropertyChanged
    {
        private readonly LeetService.Style _style;
        private string _output = string.Empty;

        public Row(LeetService.Style style) { _style = style; }

        public string Name => _style.Name;
        public string CopyLabel { get; set; } = "Copy";
        public string Output
        {
            get => _output;
            private set { _output = value; PropertyChanged?.Invoke(this, _outArgs); }
        }

        public void Recompute(string input) => Output = _style.Apply(input);

        private static readonly PropertyChangedEventArgs _outArgs = new(nameof(Output));
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private readonly ObservableCollection<Row> _rows = new();
    private IReadOnlyList<LeetService.Style> _styles = Array.Empty<LeetService.Style>();

    public LeetModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { Render(); }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Fancy Text · 花式文字", "花式文字 · Fancy Text");
            HeaderBlurb.Text = P("Type anything and get it in dozens of Unicode styles — bold, script, fraktur, circled, fullwidth, upside-down, leetspeak and more. Click a row (or Copy) to copy it.",
                "打啲字，即刻變出幾十種 Unicode 風格 — 粗體、花體、哥德體、圓圈、全形、倒轉、火星文等等。㩒一㩒個 row（或者 Copy）就copy到。");
            InputLabel.Text = P("Your text", "你嘅文字");
            InputBox.PlaceholderText = P("Type here…", "喺度打字…");

            // Rebuild rows with (re-)localized style names, preserving current input.
            _styles = LeetService.BuildStyles((en, zh) => Loc.I.Pick(en, zh));
            string copyLabel = P("Copy", "複製");
            _rows.Clear();
            foreach (var s in _styles) _rows.Add(new Row(s) { CopyLabel = copyLabel });
            if (StylesList.ItemsSource == null) StylesList.ItemsSource = _rows;

            Recompute();
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not render: ", "無法顯示：") + ex.Message);
        }
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => Recompute();

    private void Recompute()
    {
        try
        {
            string input = InputBox?.Text ?? string.Empty;
            foreach (var r in _rows) r.Recompute(input);
            SetStatus(input.Length == 0
                ? P("Enter some text to see the styles.", "打啲字就見到各種風格。")
                : P($"{_rows.Count} styles ready — click any row to copy.", $"{_rows.Count} 種風格已備妥 — 㩒任何一 row 即 copy。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Error: ", "錯誤：") + ex.Message);
        }
    }

    private void Row_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Row r) CopyText(r.Output);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string s) CopyText(s);
    }

    private void CopyText(string? text)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(text ?? string.Empty);
            Clipboard.SetContent(pkg);
            SetStatus(P("Copied to clipboard.", "已複製到剪貼簿。"));
        }
        catch (Exception ex)
        {
            SetStatus(P("Copy failed: ", "複製失敗：") + ex.Message);
        }
    }

    private void SetStatus(string text)
    {
        if (StatusText != null) StatusText.Text = text;
    }
}
