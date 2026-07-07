using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// INI 編輯器／解析器 · INI editor / parser — paste or Load an .ini, parse into a
/// [section] / key=value / comment model, round-trip list ⇄ raw canonical text, add /
/// remove / edit keys, get a value by section+key, and Save. Robust on malformed lines
/// (skipped + annotated), never throws, bilingual status. No redirect.
/// </summary>
public sealed partial class IniEditModule : Page
{
    private readonly ObservableCollection<IniEditService.IniEntry> _entries = new();

    public IniEditModule()
    {
        InitializeComponent();
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        EntriesList.ItemsSource = _entries;
        if (string.IsNullOrEmpty(RawBox.Text))
            RawBox.Text = "; WinForge sample\n[General]\nName=WinForge\nMode=Bilingual\n\n[Reactor]\nStartMode=5";
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => Loc.I.LanguageChanged -= OnLang;

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "INI Editor · INI 編輯器";
        HeaderBlurb.Text = P("Parse and edit INI configuration text — sections, key=value pairs and comments. Paste or load a .ini file, round-trip between raw text and a structured list, add or remove keys, look up a value, and save.",
            "解析同編輯 INI 設定文字 — 分區、key=value、註解。貼上或者載入 .ini 檔，喺原文同結構清單之間來回轉換，加減鍵、查值，然後儲存。");
        RawTitle.Text = P("Raw INI text", "INI 原文");
        ParseBtn.Content = P("Parse → list", "解析 → 清單");
        LoadBtn.Content = P("Load .ini…", "載入 .ini…");
        ClipBtn.Content = P("Copy raw", "複製原文");
        ListTitle.Text = P("Entries (section / key / value / note)", "記錄（分區 / 鍵 / 值 / 備註）");
        ToRawBtn.Content = P("List → raw", "清單 → 原文");
        SaveBtn.Content = P("Save .ini…", "儲存 .ini…");
        EditTitle.Text = P("Add · edit · remove · get", "加 · 改 · 刪 · 查");
        SectionBox.PlaceholderText = P("Section (blank = global)", "分區（留空 = 全域）");
        KeyBox.PlaceholderText = P("Key", "鍵");
        ValueBox.PlaceholderText = P("Value", "值");
        SetBtn.Content = P("Set / add", "設定 / 加入");
        RemoveBtn.Content = P("Remove", "移除");
        GetBtn.Content = P("Get value", "查值");
        SetStatus(P("Ready.", "準備就緒。"));
    }

    private void SetStatus(string msg) => StatusText.Text = msg;

    private void Parse_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var list = IniEditService.Parse(RawBox.Text);
            _entries.Clear();
            int bad = 0;
            foreach (var it in list) { _entries.Add(it); if (!string.IsNullOrEmpty(it.Note)) bad++; }
            SetStatus(bad == 0
                ? P($"Parsed {_entries.Count} entries.", $"已解析 {_entries.Count} 條記錄。")
                : P($"Parsed {_entries.Count} entries ({bad} malformed, annotated).", $"已解析 {_entries.Count} 條記錄（{bad} 條格式錯誤，已標註）。"));
        }
        catch { SetStatus(P("Could not parse.", "解析失敗。")); }
    }

    private void ToRaw_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RawBox.Text = IniEditService.Serialize(_entries);
            SetStatus(P("Serialized list back to canonical INI.", "已將清單序列化返標準 INI。"));
        }
        catch { SetStatus(P("Could not serialize.", "序列化失敗。")); }
    }

    private async void Load_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FileDialogs.OpenFileAsync(".ini", ".cfg", ".conf", ".txt");
            if (string.IsNullOrEmpty(path)) { SetStatus(P("Load cancelled.", "已取消載入。")); return; }
            var (text, err) = await IniEditService.ReadFileAsync(path);
            if (err is not null) { SetStatus(P($"Load failed: {err}", $"載入失敗：{err}")); return; }
            RawBox.Text = text ?? "";
            SetStatus(P("Loaded. Press Parse → list.", "已載入。撳「解析 → 清單」。"));
        }
        catch { SetStatus(P("Load failed.", "載入失敗。")); }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var text = IniEditService.Serialize(_entries);
            if (string.IsNullOrEmpty(text)) text = RawBox.Text ?? "";
            var path = await FileDialogs.SaveFileAsync("config.ini", ".ini");
            if (string.IsNullOrEmpty(path)) { SetStatus(P("Save cancelled.", "已取消儲存。")); return; }
            var err = await IniEditService.WriteFileAsync(path, text);
            SetStatus(err is null ? P("Saved.", "已儲存。") : P($"Save failed: {err}", $"儲存失敗：{err}"));
        }
        catch { SetStatus(P("Save failed.", "儲存失敗。")); }
    }

    private void Clip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var pkg = new DataPackage();
            pkg.SetText(RawBox.Text ?? "");
            Clipboard.SetContent(pkg);
            SetStatus(P("Copied raw text to clipboard.", "已複製原文到剪貼簿。"));
        }
        catch { SetStatus(P("Copy failed.", "複製失敗。")); }
    }

    private void Set_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(KeyBox.Text)) { SetStatus(P("Enter a key first.", "請先輸入鍵。")); return; }
            var updated = IniEditService.SetValue(ToList(), SectionBox.Text, KeyBox.Text, ValueBox.Text);
            RefreshFromInternal();
            SetStatus(updated ? P("Updated existing key.", "已更新現有鍵。") : P("Added new key.", "已加入新鍵。"));
        }
        catch { SetStatus(P("Set failed.", "設定失敗。")); }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(KeyBox.Text)) { SetStatus(P("Enter a key to remove.", "請輸入要移除嘅鍵。")); return; }
            var n = IniEditService.Remove(ToList(), SectionBox.Text, KeyBox.Text);
            RefreshFromInternal();
            SetStatus(n > 0 ? P($"Removed {n}.", $"已移除 {n} 條。") : P("Key not found.", "搵唔到鍵。"));
        }
        catch { SetStatus(P("Remove failed.", "移除失敗。")); }
    }

    private void Get_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var v = IniEditService.GetValue(_entries, SectionBox.Text, KeyBox.Text);
            GetResult.Text = v is null
                ? P("(not found)", "（搵唔到）")
                : $"[{SectionBox.Text}] {KeyBox.Text} = {v}";
            SetStatus(v is null ? P("No such section+key.", "冇呢個分區＋鍵。") : P("Value retrieved.", "已取得值。"));
        }
        catch { SetStatus(P("Get failed.", "查值失敗。")); }
    }

    // Copy the observable collection into a plain list for the service to mutate, then reflect back.
    private System.Collections.Generic.List<IniEditService.IniEntry> ToList()
    {
        var l = new System.Collections.Generic.List<IniEditService.IniEntry>();
        foreach (var e in _entries) l.Add(e);
        _scratch = l;
        return l;
    }

    private System.Collections.Generic.List<IniEditService.IniEntry>? _scratch;

    private void RefreshFromInternal()
    {
        if (_scratch is null) return;
        _entries.Clear();
        foreach (var e in _scratch) _entries.Add(e);
        _scratch = null;
    }
}
