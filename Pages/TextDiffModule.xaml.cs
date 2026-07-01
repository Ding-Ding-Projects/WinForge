using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 文字差異比對 · Text Diff — paste two blocks of text, get a line-level diff (LCS) with added/removed/
/// unchanged lines colour-coded, ignore-whitespace / ignore-case options, live re-diff and a unified-diff copy.
/// Pure managed C#, bilingual, never throws. Mirrors the Awake module shell.
/// </summary>
public sealed partial class TextDiffModule : Page
{
    private static readonly SolidColorBrush AddedBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0x3F, 0xB9, 0x50));
    private static readonly SolidColorBrush RemovedBrush =
        new(Windows.UI.Color.FromArgb(0xFF, 0xE0, 0x4B, 0x4B));

    /// <summary>View-model row for the diff ListView (classic {Binding}, no x:Bind).</summary>
    public sealed class DiffRow
    {
        public string Prefix { get; init; } = " ";
        public string Text { get; init; } = "";
        public Brush? Foreground { get; init; }
    }

    private readonly ObservableCollection<DiffRow> _rows = new();
    private TextDiffService.DiffResult _last = new();

    public TextDiffModule()
    {
        InitializeComponent();
        DiffList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Recompute(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Text Diff · 文字差異比對";
            HeaderBlurb.Text = P("Paste two blocks of text and see a line-by-line diff. Added lines are green, removed lines are red. Compares live as you type.",
                "貼兩段文字，逐行睇差異。加咗嘅係綠色、刪咗嘅係紅色。你打字嗰陣即時比對。");
            LabelA.Text = P("A (original)", "A（原本）");
            LabelB.Text = P("B (changed)", "B（改咗）");
            WsLabel.Text = P("Ignore whitespace", "忽略空白");
            CaseLabel.Text = P("Ignore case", "忽略大小寫");
            CopyButton.Content = P("Copy unified diff", "複製統一差異");
            UpdateCounts();
        }
        catch { }
    }

    private void Input_TextChanged(object sender, TextChangedEventArgs e) => Recompute();

    private void Option_Toggled(object sender, RoutedEventArgs e) => Recompute();

    private void Recompute()
    {
        try
        {
            var result = TextDiffService.Compute(
                InputA?.Text, InputB?.Text,
                WsSwitch?.IsOn == true, CaseSwitch?.IsOn == true);
            _last = result;

            _rows.Clear();
            foreach (var line in result.Lines)
            {
                Brush? brush = line.Kind switch
                {
                    TextDiffService.ChangeKind.Added => AddedBrush,
                    TextDiffService.ChangeKind.Removed => RemovedBrush,
                    _ => null,
                };
                _rows.Add(new DiffRow { Prefix = line.Prefix, Text = line.Text, Foreground = brush });
            }
            UpdateCounts();

            StatusText.Text = result.Truncated
                ? P("Input very large — showing a simplified diff.", "輸入好大 — 顯示咗簡化版差異。")
                : "";
        }
        catch
        {
            StatusText.Text = P("Could not compute the diff.", "計唔到差異。");
        }
    }

    private void UpdateCounts()
    {
        try
        {
            CountsText.Text = P(
                $"+{_last.Added} added   -{_last.Removed} removed   {_last.Unchanged} unchanged",
                $"+{_last.Added} 加   -{_last.Removed} 減   {_last.Unchanged} 無變");
        }
        catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string text = TextDiffService.ToUnifiedDiff(_last);
            var pkg = new DataPackage();
            pkg.SetText(text);
            Clipboard.SetContent(pkg);
            StatusText.Text = P("Unified diff copied to the clipboard.", "統一差異已複製到剪貼簿。");
        }
        catch
        {
            StatusText.Text = P("Could not copy to the clipboard.", "複製唔到去剪貼簿。");
        }
    }
}
