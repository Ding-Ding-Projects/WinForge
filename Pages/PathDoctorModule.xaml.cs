using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// PATH 醫生 · PATH Doctor — inspect and clean the User / Machine <c>Path</c> environment variables.
/// Read both scopes into editable lists, mark whether each folder exists, move/remove/add entries,
/// dedupe, drop dead paths, sort, preview before/after, then apply. Machine writes need admin; a
/// failure there shows a bilingual "needs administrator" status instead of crashing. Bilingual.
/// </summary>
public sealed partial class PathDoctorModule : Page
{
    /// <summary>One bindable PATH row (folder + exists marker).</summary>
    public sealed class Row
    {
        public string Path { get; }
        public bool DoesExist { get; }
        public string Mark => DoesExist ? "✓" : "✗"; // ✓ / ✗
        public SolidColorBrush MarkBrush => new(DoesExist ? Colors.LimeGreen : Colors.IndianRed);
        public Row(string path)
        {
            Path = path;
            DoesExist = PathDoctorService.Exists(path);
        }
    }

    private readonly ObservableCollection<Row> _rows = new();
    private List<string> _working = new();
    private string _original = string.Empty; // the on-disk PATH for the current scope
    private bool _suppress;

    public PathDoctorModule()
    {
        InitializeComponent();
        EntriesList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); LoadScope(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? s, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private PathDoctorService.Scope CurrentScope
        => ScopeCombo.SelectedIndex == 1 ? PathDoctorService.Scope.Machine : PathDoctorService.Scope.User;

    private void Render()
    {
        try
        {
            Header.Title = P("PATH Doctor", "PATH 醫生");
            HeaderBlurb.Text = P("Inspect and clean your Windows PATH — see which folders actually exist, remove duplicates and dead entries, reorder, then apply. User PATH applies right away; the system PATH needs administrator rights.",
                "檢查同清理 Windows 嘅 PATH — 睇吓邊個資料夾真係存在，移除重複同死咗嘅項目，重新排序，再套用。使用者 PATH 即刻生效；系統 PATH 要管理員權限先得。");

            _suppress = true;
            int keep = ScopeCombo.SelectedIndex < 0 ? 0 : ScopeCombo.SelectedIndex;
            ScopeCombo.Items.Clear();
            ScopeCombo.Items.Add(P("User PATH (this account)", "使用者 PATH（本帳戶）"));
            ScopeCombo.Items.Add(P("System PATH (needs admin)", "系統 PATH（需要管理員）"));
            ScopeCombo.SelectedIndex = keep;
            _suppress = false;

            ScopeLabel.Text = P("Editing", "正在編輯");
            EntriesTitle.Text = P("Entries", "項目");
            PreviewTitle.Text = P("Before / after preview", "套用前 / 後預覽");
            BeforeLabel.Text = P("Current (on disk)", "目前（已儲存）");
            AfterLabel.Text = P("After apply", "套用之後");

            UpBtn.Content = P("Move up", "上移");
            DownBtn.Content = P("Move down", "下移");
            RemoveBtn.Content = P("Remove", "移除");
            AddBtn.Content = P("Add folder…", "加入資料夾…");
            DedupeBtn.Content = P("Dedupe", "去重複");
            DeadBtn.Content = P("Remove dead", "移除死項");
            SortBtn.Content = P("Sort", "排序");
            ReloadBtn.Content = P("Reload", "重新載入");
            ApplyBtn.Content = P("Apply", "套用");

            UpdateCounts();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            SafeStatus(P("Something went wrong: ", "出咗啲問題：") + ex.Message);
        }
    }

    private void LoadScope()
    {
        try
        {
            _working = PathDoctorService.Read(CurrentScope);
            _original = PathDoctorService.Join(_working);
            RebuildRows();
            SafeStatus(P("Loaded.", "已載入。"));
        }
        catch (Exception ex)
        {
            _working = new List<string>();
            _original = string.Empty;
            RebuildRows();
            SafeStatus(P("Could not read PATH: ", "讀唔到 PATH：") + ex.Message);
        }
    }

    private void RebuildRows()
    {
        try
        {
            int sel = EntriesList.SelectedIndex;
            _rows.Clear();
            foreach (var e in _working) _rows.Add(new Row(e));
            if (sel >= 0 && sel < _rows.Count) EntriesList.SelectedIndex = sel;
            UpdateCounts();
            UpdatePreview();
        }
        catch { }
    }

    private void UpdateCounts()
    {
        try
        {
            int total = _working.Count;
            int dead = _working.Count(e => !PathDoctorService.Exists(e));
            int dupes = total - PathDoctorService.Dedupe(_working).Count;
            CountText.Text = P($"{total} entries · {dead} missing · {dupes} duplicate(s)",
                $"{total} 個項目 · {dead} 個唔存在 · {dupes} 個重複");
        }
        catch { }
    }

    private void UpdatePreview()
    {
        try
        {
            BeforeBox.Text = _original.Length == 0 ? P("(empty)", "（空）") : _original.Replace(";", ";\n");
            var after = PathDoctorService.Join(_working);
            AfterBox.Text = after.Length == 0 ? P("(empty)", "（空）") : after.Replace(";", ";\n");
        }
        catch { }
    }

    private void SafeStatus(string text)
    {
        try { StatusText.Text = text; } catch { }
    }

    private void Scope_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        LoadScope();
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int i = EntriesList.SelectedIndex;
            if (i > 0)
            {
                (_working[i - 1], _working[i]) = (_working[i], _working[i - 1]);
                RebuildRows();
                EntriesList.SelectedIndex = i - 1;
            }
        }
        catch { }
    }

    private void Down_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int i = EntriesList.SelectedIndex;
            if (i >= 0 && i < _working.Count - 1)
            {
                (_working[i + 1], _working[i]) = (_working[i], _working[i + 1]);
                RebuildRows();
                EntriesList.SelectedIndex = i + 1;
            }
        }
        catch { }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int i = EntriesList.SelectedIndex;
            if (i >= 0 && i < _working.Count)
            {
                _working.RemoveAt(i);
                RebuildRows();
                SafeStatus(P("Removed.", "已移除。"));
            }
        }
        catch { }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = await FileDialogs.OpenFolderAsync(P("Add a folder to PATH", "揀個資料夾加入 PATH"));
            if (!string.IsNullOrWhiteSpace(folder))
            {
                if (_working.Any(x => string.Equals(x.Trim().TrimEnd('\\'), folder.Trim().TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                {
                    SafeStatus(P("Already in PATH.", "已經喺 PATH 入面。"));
                    return;
                }
                _working.Add(folder);
                RebuildRows();
                EntriesList.SelectedIndex = _rows.Count - 1;
                SafeStatus(P("Added.", "已加入。"));
            }
        }
        catch (Exception ex)
        {
            SafeStatus(P("Could not add folder: ", "加唔到資料夾：") + ex.Message);
        }
    }

    private void Dedupe_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int before = _working.Count;
            _working = PathDoctorService.Dedupe(_working);
            RebuildRows();
            SafeStatus(P($"Removed {before - _working.Count} duplicate(s).", $"移除咗 {before - _working.Count} 個重複。"));
        }
        catch { }
    }

    private void Dead_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int before = _working.Count;
            _working = PathDoctorService.RemoveDead(_working);
            RebuildRows();
            SafeStatus(P($"Removed {before - _working.Count} missing folder(s).", $"移除咗 {before - _working.Count} 個唔存在嘅資料夾。"));
        }
        catch { }
    }

    private void Sort_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _working = PathDoctorService.Sort(_working);
            RebuildRows();
            SafeStatus(P("Sorted.", "已排序。"));
        }
        catch { }
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => LoadScope();

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var scope = CurrentScope;
            var result = PathDoctorService.Apply(scope, _working);
            if (result.Ok)
            {
                _original = PathDoctorService.Join(_working);
                UpdatePreview();
                SafeStatus(P("Applied. New programs and terminals will see the change.",
                    "已套用。新開嘅程式同終端機會見到更新。"));
            }
            else if (result.NeedsAdmin)
            {
                SafeStatus(P("Needs administrator — writing the system PATH requires running WinForge as administrator. Your changes are kept here; restart elevated and apply again.",
                    "需要管理員 — 寫入系統 PATH 要以管理員身分執行 WinForge。你嘅改動仲喺度；用管理員身分重開再套用。"));
            }
            else
            {
                SafeStatus(P("Could not apply: ", "套用唔到：") + (result.Error ?? ""));
            }
        }
        catch (Exception ex)
        {
            SafeStatus(P("Could not apply: ", "套用唔到：") + ex.Message);
        }
    }
}
