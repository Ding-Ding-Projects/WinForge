using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 習慣追蹤器 · Habit Tracker — a list of habits, each showing the name, 7 day-toggle CheckBoxes for the
/// current week (Mon–Sun, today highlighted), a current-streak count (consecutive days done up to today)
/// and a total-done count. Add / rename / delete habits; toggling a day persists immediately.
/// Persists to %LOCALAPPDATA%\WinForge\habits\habits.json (auto-save off-thread, guarded). Bilingual. Never throws.
/// </summary>
public sealed partial class HabitTrackerModule : Page
{
    private const string DateFmt = "yyyy-MM-dd";

    /// <summary>One day cell of the current week for a habit — a completion flag + display state.</summary>
    public sealed class DayCell : INotifyPropertyChanged
    {
        public DateTime Date { get; init; }
        public bool IsToday { get; init; }

        private string _label = "";
        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value; Notify(nameof(Label)); } }
        }

        private bool _isDone;
        public bool IsDone
        {
            get => _isDone;
            set { if (_isDone != value) { _isDone = value; Notify(nameof(IsDone)); } }
        }

        /// <summary>Subtle highlight brush for today's cell; transparent otherwise.</summary>
        public Brush TodayBackground => IsToday
            ? new SolidColorBrush(Windows.UI.Color.FromArgb(0x22, 0x4C, 0xAF, 0x50))
            : new SolidColorBrush(Colors.Transparent);

        /// <summary>Accent border only around today's cell.</summary>
        public Thickness TodayBorder => IsToday ? new Thickness(1) : new Thickness(0);

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>Observable row backing the ListView — name, this week's day cells, and derived streak/total text.</summary>
    public sealed class HabitItem : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; Notify(nameof(Name)); } }
        }

        /// <summary>All completed dates (ISO strings), the source of truth for streak/total.</summary>
        public HashSet<string> Done { get; } = new(StringComparer.Ordinal);

        /// <summary>Mon–Sun cells for the current week.</summary>
        public ObservableCollection<DayCell> Days { get; } = new();

        private string _streakText = "";
        public string StreakText
        {
            get => _streakText;
            set { if (_streakText != value) { _streakText = value; Notify(nameof(StreakText)); } }
        }

        private string _totalText = "";
        public string TotalText
        {
            get => _totalText;
            set { if (_totalText != value) { _totalText = value; Notify(nameof(TotalText)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Notify(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    private readonly ObservableCollection<HabitItem> _items = new();
    private DateTime[] _week = Array.Empty<DateTime>();
    private bool _loaded;

    public HabitTrackerModule()
    {
        InitializeComponent();
        HabitsList.ItemsSource = _items;
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
        Header.Title = "Habit Tracker · 習慣追蹤器";
        HeaderBlurb.Text = P("Build good habits by ticking them off each day. Each habit shows this week (Mon–Sun, today highlighted), your current streak and total days done — everything is saved automatically.",
            "每日打個剔，養成好習慣。每個習慣顯示今個星期（一至日，今日會突出），你嘅連續紀錄同總完成日數 — 全部自動幫你儲存。");
        NewNameBox.PlaceholderText = P("New habit name…", "新習慣名…");
        AddBtn.Content = P("Add", "新增");
        EmptyText.Text = P("No habits yet — type a name above and press Add.", "仲未有習慣 — 喺上面打個名再撳新增。");

        RebuildWeek();
        RelabelDays();
        foreach (var item in _items) UpdateDerived(item);
        UpdateEmpty();
    }

    /// <summary>Compute the current Mon–Sun week (once per Render, so it follows day rollover on reopen).</summary>
    private void RebuildWeek()
    {
        DateTime today = DateTime.Today;
        int offset = ((int)today.DayOfWeek + 6) % 7; // Monday = 0
        DateTime monday = today.AddDays(-offset);
        _week = Enumerable.Range(0, 7).Select(i => monday.AddDays(i)).ToArray();

        string range = _week[0].ToString("d MMM", CultureInfo.CurrentUICulture) + " – " +
                       _week[6].ToString("d MMM", CultureInfo.CurrentUICulture);
        WeekLabel.Text = P($"This week: {range}", $"今個星期：{range}");
    }

    private string[] DayLabels() => new[]
    {
        P("Mon", "一"), P("Tue", "二"), P("Wed", "三"),
        P("Thu", "四"), P("Fri", "五"), P("Sat", "六"), P("Sun", "日"),
    };

    /// <summary>Rebuild each habit's 7 day cells against the current week, preserving done-state.</summary>
    private void RelabelDays()
    {
        var labels = DayLabels();
        DateTime today = DateTime.Today;
        foreach (var item in _items)
        {
            item.Days.Clear();
            for (int i = 0; i < _week.Length; i++)
            {
                var d = _week[i];
                item.Days.Add(new DayCell
                {
                    Date = d,
                    IsToday = d == today,
                    Label = labels[i],
                    IsDone = item.Done.Contains(d.ToString(DateFmt)),
                });
            }
        }
    }

    // ---- persistence ----

    private async void Load()
    {
        List<HabitTrackerService.Habit> loaded;
        try
        {
            loaded = await HabitTrackerService.LoadAsync();
        }
        catch
        {
            loaded = new List<HabitTrackerService.Habit>();
        }

        _items.Clear();
        foreach (var h in loaded)
        {
            var item = new HabitItem { Name = h.Name ?? "" };
            if (h.Done != null)
                foreach (var d in h.Done)
                    if (!string.IsNullOrWhiteSpace(d)) item.Done.Add(d);
            _items.Add(item);
        }

        _loaded = true;
        RelabelDays();
        foreach (var item in _items) UpdateDerived(item);
        UpdateEmpty();
    }

    private async void Save()
    {
        if (!_loaded) return;
        var snapshot = _items.Select(i => new HabitTrackerService.Habit
        {
            Name = i.Name,
            Done = i.Done.ToList(),
        }).ToList();

        bool ok;
        try
        {
            ok = await HabitTrackerService.SaveAsync(snapshot);
        }
        catch
        {
            ok = false;
        }
        StatusText.Text = ok
            ? P("Saved.", "已儲存。")
            : P("Could not save to disk — changes stay for this session.", "無法寫入磁碟 — 今次仍會保留變更。");
    }

    // ---- day toggle ----

    private void Day_Toggled(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not DayCell cell) return;
        // Find the owning habit.
        var owner = _items.FirstOrDefault(h => h.Days.Contains(cell));
        if (owner == null) return;

        string key = cell.Date.ToString(DateFmt);
        if (cell.IsDone) owner.Done.Add(key);
        else owner.Done.Remove(key);

        UpdateDerived(owner);
        Save();
    }

    // ---- row actions ----

    private static HabitItem? ItemOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as HabitItem;

    private void Row_Menu_Click(object sender, RoutedEventArgs e)
    {
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

        var input = new TextBox { Text = item.Name, PlaceholderText = P("Habit name", "習慣名") };
        var dlg = new ContentDialog
        {
            Title = P("Rename habit", "重新命名習慣"),
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
        UpdateEmpty();
        Save();
    }

    // ---- global actions ----

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        string name = (NewNameBox.Text ?? "").Trim();
        if (name.Length == 0)
            name = P($"Habit {_items.Count + 1}", $"習慣 {_items.Count + 1}");

        var item = new HabitItem { Name = name };
        _items.Add(item);
        RelabelDaysFor(item);
        UpdateDerived(item);
        NewNameBox.Text = "";
        UpdateEmpty();
        Save();
    }

    // ---- helpers ----

    /// <summary>Build the 7 day cells for a single freshly-added habit.</summary>
    private void RelabelDaysFor(HabitItem item)
    {
        var labels = DayLabels();
        DateTime today = DateTime.Today;
        item.Days.Clear();
        for (int i = 0; i < _week.Length; i++)
        {
            var d = _week[i];
            item.Days.Add(new DayCell
            {
                Date = d,
                IsToday = d == today,
                Label = labels[i],
                IsDone = item.Done.Contains(d.ToString(DateFmt)),
            });
        }
    }

    /// <summary>Recompute current streak (consecutive days done up to today) + total-done text.</summary>
    private void UpdateDerived(HabitItem item)
    {
        int streak = 0;
        DateTime day = DateTime.Today;
        // Count back from today while each day is marked done.
        while (item.Done.Contains(day.ToString(DateFmt)))
        {
            streak++;
            day = day.AddDays(-1);
        }

        item.StreakText = P($"Streak: {streak} day{(streak == 1 ? "" : "s")}", $"連續：{streak} 日");
        item.TotalText = P($"Total: {item.Done.Count} day{(item.Done.Count == 1 ? "" : "s")}", $"總共：{item.Done.Count} 日");
    }

    private void UpdateEmpty()
    {
        EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HabitsList.Visibility = _items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}
