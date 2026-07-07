using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// .gitignore 產生器 · .gitignore generator — tick common templates (Node, Python, Visual Studio,
/// VS Code, JetBrains, Rust, Java/Maven, Go, C/C++, macOS, Windows, Linux) and get a combined,
/// de-duplicated .gitignore you can copy or save. Bilingual (粵語). Robust — never throws.
/// </summary>
public sealed partial class GitignoreModule : Page
{
    /// <summary>可揀嘅範本列 · A selectable template row bound to the ListView.</summary>
    public sealed class TemplateItem : INotifyPropertyChanged
    {
        public GitignoreService.Template Template { get; }
        public string Name => Template.Name;

        private bool _selected;
        public bool Selected
        {
            get => _selected;
            set { if (_selected != value) { _selected = value; OnChanged(); } }
        }

        public TemplateItem(GitignoreService.Template t) => Template = t;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private readonly ObservableCollection<TemplateItem> _items = new();

    public GitignoreModule()
    {
        InitializeComponent();
        foreach (var t in GitignoreService.Templates)
            _items.Add(new TemplateItem(t));
        TemplatesList.ItemsSource = _items;

        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => Render();

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Gitignore Generator", ".gitignore 產生器");
            HeaderBlurb.Text = P("Tick the tech you use and WinForge builds a combined .gitignore — each template under its own header, with duplicate rules removed. Copy it or save it straight to your repo.",
                "㨂你用嘅技術，WinForge 就會夾埋一個 .gitignore — 每個範本各有標題，重複嘅規則自動去走。可以複製或者直接存入你個 repo。");
            TemplatesLabel.Text = P("Templates", "範本");
            OutputLabel.Text = P("Combined .gitignore", "合併嘅 .gitignore");
            CopyButton.Content = P("Copy", "複製");
            SaveButton.Content = P("Save .gitignore", "儲存 .gitignore");
            Rebuild();
        }
        catch { /* never throw from UI render */ }
    }

    private void Item_Changed(object sender, RoutedEventArgs e) => Rebuild();

    private void Rebuild()
    {
        try
        {
            var chosen = _items.Where(i => i.Selected).Select(i => i.Template).ToList();
            OutputBox.Text = GitignoreService.Combine(chosen);
            StatusText.Text = chosen.Count == 0
                ? P("Pick one or more templates to start.", "㨂一個或多個範本開始。")
                : P($"{chosen.Count} template(s) combined · {CountLines(OutputBox.Text)} lines.",
                    $"合併咗 {chosen.Count} 個範本 · {CountLines(OutputBox.Text)} 行。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Could not build the file: ", "整唔到個檔案：") + ex.Message;
        }
    }

    private static int CountLines(string text) =>
        string.IsNullOrEmpty(text) ? 0 : text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').Length;

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = OutputBox.Text ?? "";
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時冇嘢可以複製。");
                return;
            }
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Copied to clipboard.", "已複製到剪貼簿。");
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Copy failed: ", "複製失敗：") + ex.Message;
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = OutputBox.Text ?? "";
            if (text.Length == 0)
            {
                StatusText.Text = P("Nothing to save yet.", "暫時冇嘢可以儲存。");
                return;
            }
            var filters = new List<FileDialogs.Filter>
            {
                new("gitignore", "*.gitignore;.gitignore"),
                new("All files", "*.*"),
            };
            var path = await FileDialogs.SaveFileAsync(".gitignore", filters, "gitignore",
                P("Save .gitignore", "儲存 .gitignore"));
            if (string.IsNullOrEmpty(path))
            {
                StatusText.Text = P("Save cancelled.", "已取消儲存。");
                return;
            }
            await System.IO.File.WriteAllTextAsync(path, text);
            StatusText.Text = P("Saved to ", "已儲存到 ") + path;
        }
        catch (Exception ex)
        {
            StatusText.Text = P("Save failed: ", "儲存失敗：") + ex.Message;
        }
    }
}
