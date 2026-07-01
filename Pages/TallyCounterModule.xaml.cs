using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 點數計數器 · Tally Counter — a list of named counters, each with a big value and − / + / reset,
/// plus add / rename / delete, a configurable step, a grand total and "reset all". Persists to
/// %LOCALAPPDATA%\WinForge\tally\counters.json (auto-save off-thread, guarded). Bilingual. Never throws.
/// </summary>
public sealed partial class TallyCounterModule : Page
{
    /// <summary>Observable row backing the ListView (name + value raise change notifications).</summary>
    public sealed class CounterItem : INotifyPropertyChanged
    {
        private string _name = "";
        private long _value;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; Notify(nameof(Name)); } }
        }

        public long Value
        {
            get => _value;
            set { if (_value != value) { _value = value; Notify(nameof(Value)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    private readonly ObservableCollection<CounterItem> _items = new();
    private bool _loaded;

    public TallyCounterModule()
    {
        InitializeComponent();
        CountersList.ItemsSource = _items;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        Load();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Tally Counter · 點數計數器";
        HeaderBlurb.Text = P("Keep count of anything — reps, cups of coffee, visitors, inventory. Add named counters, tap − / + to adjust by your step, and everything is saved automatically.",
            "咩都數得到 — 掌上壓、幾多杯咖啡、訪客、存貨。加個有名嘅計數器，撳 − / + 就按你設定嘅步長加減，全部自動幫你儲存。");
        NewNameBox.PlaceholderText = P("New counter name…", "新計數器名…");
        AddBtn.Content = P("Add", "新增");
        StepLabel.Text = P("Step size", "每次加減");
        EmptyText.Text = P("No counters yet — type a name above and press Add.", "仲未有計數器 — 喺上面打個名再撳新增。");
        TotalLabel.Text = P("Total across counters", "所有計數器總和");
        ResetAllBtn.Content = P("Reset all", "全部歸零");
        // Per-row flyout items ("Rename"/"Delete") are labelled lazily on open — see Row_Menu_Click.
        UpdateTotals();
    }

    // ---- persistence ----

    private async void Load()
    {
        List<TallyCounterService.Counter> loaded;
        try
        {
            loaded = await TallyCounterService.LoadAsync();
        }
        catch
        {
            loaded = new List<TallyCounterService.Counter>();
        }

        _items.Clear();
        foreach (var c in loaded)
            _items.Add(new CounterItem { Name = c.Name ?? "", Value = c.Value });

        _loaded = true;
        UpdateTotals();
    }

    private async void Save()
    {
        if (!_loaded) return;
        var snapshot = _items.Select(i => new TallyCounterService.Counter { Name = i.Name, Value = i.Value }).ToList();
        bool ok;
        try
        {
            ok = await TallyCounterService.SaveAsync(snapshot);
        }
        catch
        {
            ok = false;
        }
        StatusText.Text = ok
            ? P("Saved.", "已儲存。")
            : P("Could not save to disk — changes stay for this session.", "無法寫入磁碟 — 今次仍會保留變更。");
    }

    // ---- row actions ----

    private static CounterItem? ItemOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as CounterItem;

    private void Plus_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        item.Value += Step();
        UpdateTotals();
        Save();
    }

    private void Minus_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        item.Value -= Step();
        UpdateTotals();
        Save();
    }

    private void Row_Reset_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        if (item.Value == 0) return;
        item.Value = 0;
        UpdateTotals();
        Save();
    }

    private void Row_Menu_Click(object sender, RoutedEventArgs e)
    {
        // Re-label the flyout items each open so they follow the current language.
        if (sender is not Button btn || btn.Flyout is not MenuFlyout mf) return;
        foreach (var mfi in mf.Items.OfType<MenuFlyoutItem>())
        {
            if (mfi.Name == "RenameItem") mfi.Text = P("Rename", "重新命名");
            else if (mfi.Name == "DeleteItem") mfi.Text = P("Delete", "刪除");
        }
    }

    private async void Rename_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;

        var input = new TextBox { Text = item.Name, PlaceholderText = P("Counter name", "計數器名") };
        var dlg = new ContentDialog
        {
            Title = P("Rename counter", "重新命名計數器"),
            Content = input,
            PrimaryButtonText = P("Rename", "重新命名"),
            CloseButtonText = P("Cancel", "取消"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        try
        {
            var result = await dlg.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                string name = (input.Text ?? "").Trim();
                if (name.Length == 0) name = item.Name;
                item.Name = name;
                Save();
            }
        }
        catch
        {
            // Dialog can throw if another is already open; ignore.
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is not { } item) return;
        _items.Remove(item);
        UpdateTotals();
        Save();
    }

    // ---- global actions ----

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string name = (NewNameBox.Text ?? "").Trim();
        if (name.Length == 0)
            name = P($"Counter {_items.Count + 1}", $"計數器 {_items.Count + 1}");
        _items.Add(new CounterItem { Name = name, Value = 0 });
        NewNameBox.Text = "";
        UpdateTotals();
        Save();
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        bool changed = false;
        foreach (var item in _items)
        {
            if (item.Value != 0) { item.Value = 0; changed = true; }
        }
        if (!changed) return;
        UpdateTotals();
        Save();
    }

    // ---- helpers ----

    private long Step()
    {
        double v = StepBox.Value;
        if (double.IsNaN(v) || v < 1) return 1;
        return (long)v;
    }

    private void UpdateTotals()
    {
        long total = 0;
        foreach (var i in _items) total += i.Value;
        TotalValue.Text = total.ToString();
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountersList.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
