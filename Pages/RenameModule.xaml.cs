using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 應用程式內批次改名（PowerRename 式，純 C#）· In-app batch rename — pick a folder, find/replace with
/// optional regex, live preview with conflict detection, then apply. No external tool, no redirect.
/// </summary>
public sealed partial class RenameModule : Page
{
    public sealed class Row
    {
        public string Old { get; init; } = "";
        public string New { get; init; } = "";
        public bool Changed { get; init; }
        public bool Conflict { get; init; }

        public Brush NewBrush => Conflict
            ? (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
            : Changed
                ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                : (Brush)Application.Current.Resources["TextFillColorTertiaryBrush"];

        public Windows.UI.Text.FontWeight NewWeight => Changed ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private string _folder = "";
    private List<string> _files = new();

    public RenameModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLanguageChanged; };
        Loaded += (_, _) =>
        {
            Render();
            // 示範用預設：用 app 自己嘅資料夾（系統 DLL，冇個人資料）· demo default using the app's own folder.
            if (string.IsNullOrEmpty(_folder))
            {
                _folder = AppContext.BaseDirectory;
                FolderBox.Text = _folder;
                LoadFiles();
                FindBox.Text = "Microsoft";
                ReplaceBox.Text = "MS";
            }
            Recompute();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private void Render()
    {
        Header.Title = "Batch Rename · 批次改名";
        HeaderBlurb.Text = P("Pick a folder, type a find & replace (plain or regex), preview, then apply.",
            "揀一個資料夾，輸入搵同換（純文字或正則），預覽，再套用。");
        FolderCap.Text = P("Folder", "資料夾");
        BrowseBtn.Content = P("Browse…", "瀏覽…");
        FindBox.PlaceholderText = P("Find…", "搵…");
        ReplaceBox.PlaceholderText = P("Replace with…", "換成…");
        RegexCheck.Content = P("Regex", "正則");
        CaseCheck.Content = P("Case sensitive", "區分大小寫");
        ExtCheck.Content = P("Include extension", "包括副檔名");
        ApplyBtn.Content = P("Apply rename", "套用改名");
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await FileDialogs.OpenFolderAsync(P("Pick a folder to rename in", "揀一個要改名嘅資料夾"));
            if (folder is not null)
            {
                _folder = folder;
                FolderBox.Text = _folder;
                LoadFiles();
                Recompute();
            }
        }
        catch (Exception ex) { ShowErr(ex.Message); }
    }

    private void LoadFiles()
    {
        try { _files = Directory.GetFiles(_folder).ToList(); }
        catch { _files = new List<string>(); }
    }

    private void Input_Changed(object sender, TextChangedEventArgs e) => Recompute();
    private void Option_Changed(object sender, RoutedEventArgs e) => Recompute();

    private void Recompute()
    {
        if (string.IsNullOrEmpty(_folder)) { CountText.Text = P("No folder", "未揀資料夾"); List.ItemsSource = null; return; }

        bool rx = RegexCheck.IsChecked == true;
        bool cs = CaseCheck.IsChecked == true;
        bool ext = ExtCheck.IsChecked == true;
        string find = FindBox.Text ?? "";
        string repl = ReplaceBox.Text ?? "";

        var oldNames = _files.Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToList();
        var computed = oldNames.Select(o => (o, n: RenameEngine.NewName(o, find, repl, rx, cs, ext))).ToList();

        // duplicate final names (case-insensitive) among the set
        var finalCounts = computed.GroupBy(c => c.n.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.Count());
        var changedOriginals = computed.Where(c => c.n != c.o).Select(c => c.o.ToLowerInvariant()).ToHashSet();

        var rows = new List<Row>();
        int changes = 0, conflicts = 0;
        foreach (var (o, n) in computed)
        {
            bool changed = n != o;
            bool conflict = false;
            if (changed)
            {
                bool invalid = !RenameEngine.IsValidName(n);
                bool dup = finalCounts.TryGetValue(n.ToLowerInvariant(), out var c) && c > 1;
                bool collidesExisting = File.Exists(Path.Combine(_folder, n)) && !changedOriginals.Contains(n.ToLowerInvariant());
                conflict = invalid || dup || collidesExisting;
                if (conflict) conflicts++; else changes++;
            }
            rows.Add(new Row { Old = o, New = n, Changed = changed, Conflict = conflict });
        }

        List.ItemsSource = rows;
        CountText.Text = P($"{changes} to rename · {conflicts} conflict(s)", $"{changes} 個要改 · {conflicts} 個衝突");
        ApplyBtn.IsEnabled = changes > 0;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_folder)) return;
        bool rx = RegexCheck.IsChecked == true, cs = CaseCheck.IsChecked == true, ext = ExtCheck.IsChecked == true;
        string find = FindBox.Text ?? "", repl = ReplaceBox.Text ?? "";

        var plan = new List<(string oldFull, string newName)>();
        var finalCounts = new Dictionary<string, int>();
        foreach (var f in _files)
        {
            var o = Path.GetFileName(f);
            var n = RenameEngine.NewName(o, find, repl, rx, cs, ext);
            if (n == o || !RenameEngine.IsValidName(n)) continue;
            finalCounts[n.ToLowerInvariant()] = finalCounts.GetValueOrDefault(n.ToLowerInvariant()) + 1;
            plan.Add((f, n));
        }
        // drop duplicates (conflicts)
        plan = plan.Where(p => finalCounts[p.newName.ToLowerInvariant()] == 1).ToList();

        int done = 0, failed = 0;
        // two-phase via temp names to avoid intra-set collisions
        var temps = new List<(string tempFull, string newName)>();
        foreach (var (oldFull, newName) in plan)
        {
            try
            {
                var temp = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".wtmp");
                File.Move(oldFull, temp);
                temps.Add((temp, newName));
            }
            catch { failed++; }
        }
        foreach (var (tempFull, newName) in temps)
        {
            try
            {
                File.Move(tempFull, Path.Combine(_folder, newName));
                done++;
            }
            catch { failed++; }
        }

        LoadFiles();
        Recompute();
        ResultBar.Severity = failed == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
        ResultBar.Title = P("Renamed", "已改名");
        ResultBar.Message = P($"{done} renamed, {failed} failed.", $"成功 {done} 個，失敗 {failed} 個。");
        ResultBar.IsOpen = true;
    }

    private void ShowErr(string msg)
    {
        ResultBar.Severity = InfoBarSeverity.Error;
        ResultBar.Title = P("Failed", "失敗");
        ResultBar.Message = msg;
        ResultBar.IsOpen = true;
    }
}
