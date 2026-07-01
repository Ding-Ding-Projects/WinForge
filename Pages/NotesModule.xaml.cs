using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 便箋（持久筆記）· Scratchpad / persistent notes. A left list of note titles + a large multiline
/// editor on the right, with search, New / Rename / Delete, live word &amp; character counts and
/// debounced auto-save (~800 ms). All file IO is in <see cref="NotesService"/>, runs on a background
/// thread, and is fully guarded — a missing or locked file never crashes the page. Bilingual (粵語).
/// </summary>
public sealed partial class NotesModule : Page
{
    // All notes (source of truth) + the filtered view bound to the ListView.
    private readonly ObservableCollection<NotesService.Note> _all = new();
    private readonly ObservableCollection<NotesService.Note> _view = new();

    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private NotesService.Note? _current;
    private bool _dirty;
    private bool _suppress;   // guard event handlers during programmatic changes
    private bool _loaded;     // notes finished loading

    public NotesModule()
    {
        InitializeComponent();
        NoteList.ItemsSource = _view;
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); _ = FlushAsync(); };

        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); LoadNotes(); };
        Unloaded += (_, _) =>
        {
            Loc.I.LanguageChanged -= OnLang;
            _saveTimer.Stop();
            // Flush any pending edits synchronously-ish on the way out (fire-and-forget is fine —
            // NotesService serialises writes under a lock and never throws).
            CaptureCurrent();
            if (_dirty) _ = FlushAsync();
        };
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void OnLang(object? sender, EventArgs e) => Render();

    private void Render()
    {
        Header.Title = "Scratchpad · 便箋";
        HeaderBlurb.Text = P("A persistent scratchpad — jot notes that survive restarts. Everything is saved locally and automatically.",
            "持久便箋 — 隨手記低嘢，重開之後都仲喺度。全部自動存喺你部機度。");
        SearchBox.PlaceholderText = P("Search notes…", "搜尋筆記…");
        Editor.PlaceholderText = P("Start typing…", "開始打字…");
        NewBtn.Content = P("New", "新增");
        RenameBtn.Content = P("Rename", "改名");
        DeleteBtn.Content = P("Delete", "刪除");
        UpdateCount();
        UpdateStatus(_loaded ? P("Ready.", "準備好。") : P("Loading…", "載入緊…"));
        UpdateButtons();
    }

    private async void LoadNotes()
    {
        UpdateStatus(P("Loading…", "載入緊…"));
        var (notes, error) = await NotesService.LoadAsync();
        _suppress = true;
        try
        {
            _all.Clear();
            foreach (var n in notes) _all.Add(n);
            ApplyFilter();
        }
        finally { _suppress = false; }

        _loaded = true;
        if (_view.Count > 0) NoteList.SelectedIndex = 0;
        else SelectNote(null);

        UpdateStatus(error ?? P("Ready.", "準備好。"));
        UpdateButtons();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";
        _view.Clear();
        foreach (var n in _all)
        {
            if (q.Length == 0
                || (n.Title?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                || (n.Body?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                _view.Add(n);
            }
        }
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        var keep = _current;
        _suppress = true;
        try
        {
            ApplyFilter();
            if (keep != null && _view.Contains(keep)) NoteList.SelectedItem = keep;
            else if (_view.Count > 0) NoteList.SelectedIndex = 0;
        }
        finally { _suppress = false; }
        if (NoteList.SelectedItem is NotesService.Note sel) SelectNote(sel);
        else SelectNote(null);
    }

    private void NoteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        CaptureCurrent();
        if (_dirty) { _saveTimer.Stop(); _ = FlushAsync(); }
        SelectNote(NoteList.SelectedItem as NotesService.Note);
    }

    private void SelectNote(NotesService.Note? note)
    {
        _current = note;
        _suppress = true;
        try { Editor.Text = note?.Body ?? ""; }
        finally { _suppress = false; }
        Editor.IsEnabled = note != null;
        UpdateCount();
        UpdateButtons();
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress || _current == null) return;
        _dirty = true;
        UpdateCount();
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    // Copy the editor's text back into the current note object.
    private void CaptureCurrent()
    {
        if (_current == null) return;
        var text = Editor.Text ?? "";
        if (!string.Equals(text, _current.Body, StringComparison.Ordinal))
        {
            _current.Body = text;
            _current.Modified = DateTime.UtcNow;
            _dirty = true;
        }
    }

    private async Task FlushAsync()
    {
        CaptureCurrent();
        if (!_dirty) return;
        _dirty = false;
        var snapshot = _all.ToList();
        var error = await NotesService.SaveAsync(snapshot);
        UpdateStatus(error ?? P("Saved.", "已儲存。"));
    }

    private async void New_Click(object sender, RoutedEventArgs e)
    {
        var name = await PromptAsync(P("New note", "新增筆記"), P("Name", "名稱"),
            P("Untitled note", "未命名筆記"));
        if (name == null) return; // cancelled
        var note = new NotesService.Note
        {
            Title = string.IsNullOrWhiteSpace(name) ? P("Untitled note", "未命名筆記") : name.Trim(),
            Body = "",
            Modified = DateTime.UtcNow
        };
        _all.Insert(0, note);
        _suppress = true;
        try { ApplyFilter(); }
        finally { _suppress = false; }
        NoteList.SelectedItem = note;
        SelectNote(note);
        _dirty = true;
        await FlushAsync();
        Editor.Focus(FocusState.Programmatic);
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var name = await PromptAsync(P("Rename note", "筆記改名"), P("Name", "名稱"), _current.Title);
        if (string.IsNullOrWhiteSpace(name)) return;
        _current.Title = name.Trim();
        _current.Modified = DateTime.UtcNow;
        // Refresh the visible title (ObservableCollection re-render via re-filter).
        _suppress = true;
        try { var keep = _current; ApplyFilter(); NoteList.SelectedItem = keep; }
        finally { _suppress = false; }
        _dirty = true;
        await FlushAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var target = _current;
        var confirm = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = P("Delete note?", "刪除筆記？"),
            Content = P($"Delete “{target.Title}”? This can't be undone.", $"刪除「{target.Title}」？呢個動作冇得復原。"),
            PrimaryButtonText = P("Delete", "刪除"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Close
        };
        ContentDialogResult res;
        try { res = await confirm.ShowAsync(); }
        catch { return; }
        if (res != ContentDialogResult.Primary) return;

        _all.Remove(target);
        _current = null;
        _dirty = true;
        _suppress = true;
        try { ApplyFilter(); }
        finally { _suppress = false; }
        if (_view.Count > 0) NoteList.SelectedIndex = 0; else SelectNote(null);
        await FlushAsync();
    }

    // Small reusable text-input dialog. Returns null if cancelled, otherwise the entered text.
    private async Task<string?> PromptAsync(string title, string label, string initial)
    {
        var box = new TextBox { Text = initial ?? "", PlaceholderText = label, SelectionStart = (initial ?? "").Length };
        var dlg = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = box,
            PrimaryButtonText = P("OK", "確定"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary
        };
        try
        {
            var res = await dlg.ShowAsync();
            return res == ContentDialogResult.Primary ? box.Text : null;
        }
        catch { return null; }
    }

    private void UpdateButtons()
    {
        var has = _current != null;
        RenameBtn.IsEnabled = has;
        DeleteBtn.IsEnabled = has;
    }

    private void UpdateCount()
    {
        var text = _current?.Body ?? Editor.Text ?? "";
        int chars = text.Length;
        int words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        CountText.Text = P($"{words} words · {chars} chars", $"{words} 字 · {chars} 個字元");
    }

    private void UpdateStatus(string message) => StatusText.Text = message;
}
