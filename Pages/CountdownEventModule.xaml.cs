using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 事件倒數 · Event countdown — add named events with a target date + time and watch a live
/// "D days, HH:MM:SS" remaining tick down every second. Past events read "passed N ago".
/// Sorted soonest-first. Persisted to %LOCALAPPDATA%\WinForge\countdowns\events.json. Bilingual.
/// </summary>
public sealed partial class CountdownEventModule : Page
{
    /// <summary>One live row. Raises change notifications so the countdown text refreshes without rebinding.</summary>
    public sealed class CountdownItem : INotifyPropertyChanged
    {
        public string Name { get; }
        public DateTimeOffset Target { get; }

        public CountdownItem(string name, DateTimeOffset target)
        {
            Name = name;
            Target = target;
        }

        public string TargetText => Target.LocalDateTime.ToString("yyyy-MM-dd HH:mm");

        private string _countdownText = "";
        public string CountdownText
        {
            get => _countdownText;
            private set { if (_countdownText != value) { _countdownText = value; Raise(); } }
        }

        /// <summary>Recompute the remaining/elapsed text. <paramref name="pick"/> is the bilingual selector.</summary>
        public void Refresh(DateTimeOffset now, Func<string, string, string> pick)
        {
            TimeSpan diff = Target - now;
            if (diff.TotalSeconds >= 0)
            {
                int days = (int)diff.TotalDays;
                CountdownText = pick(
                    $"{days} days, {diff.Hours:00}:{diff.Minutes:00}:{diff.Seconds:00} left",
                    $"仲有 {days} 日，{diff.Hours:00}:{diff.Minutes:00}:{diff.Seconds:00}");
            }
            else
            {
                TimeSpan ago = now - Target;
                CountdownText = pick(
                    $"passed {DescribeSpan(ago, false)} ago",
                    $"已經過咗 {DescribeSpan(ago, true)}");
            }
        }

        private static string DescribeSpan(TimeSpan span, bool zh)
        {
            int days = (int)span.TotalDays;
            if (days >= 1) return zh ? $"{days} 日" : (days == 1 ? "1 day" : $"{days} days");
            int hours = (int)span.TotalHours;
            if (hours >= 1) return zh ? $"{hours} 個鐘" : (hours == 1 ? "1 hour" : $"{hours} hours");
            int mins = (int)span.TotalMinutes;
            if (mins >= 1) return zh ? $"{mins} 分鐘" : (mins == 1 ? "1 minute" : $"{mins} minutes");
            int secs = Math.Max(0, (int)span.TotalSeconds);
            return zh ? $"{secs} 秒" : (secs == 1 ? "1 second" : $"{secs} seconds");
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<CountdownItem> _items = new();
    private bool _loaded;

    public CountdownEventModule()
    {
        InitializeComponent();
        EventsList.ItemsSource = _items;
        _timer.Tick += OnTick;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Render();
        await LoadAsync();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer.Stop();
        _timer.Tick -= OnTick;
        Loc.I.LanguageChanged -= OnLang;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLang(object? sender, EventArgs e)
    {
        Render();
        TickNow();
    }

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        Header.Title = "Event Countdown · 事件倒數";
        HeaderBlurb.Text = P(
            "Add the moments that matter and watch a live countdown tick down to the second. Events are saved and reappear next time you open WinForge.",
            "加入你緊張嘅日子，睇住倒數一秒一秒咁跳。所有事件會儲低，下次開 WinForge 又見返。");
        AddTitle.Text = P("Add an event", "加一件事");
        NameBox.PlaceholderText = P("Event name (e.g. Trip to Japan)", "事件名（例如：去日本旅行）");
        AddButton.Content = P("Add event", "加入");
        ListTitle.Text = P("Your countdowns", "你嘅倒數");
        EmptyText.Text = P("No events yet — add one above to start counting down.", "仲未有事件 — 喺上面加一件就開始倒數。");
        UpdateEmpty();
    }

    private void OnTick(object? sender, object e) => TickNow();

    private void TickNow()
    {
        var now = DateTimeOffset.Now;
        foreach (var item in _items) item.Refresh(now, P);
    }

    private void UpdateEmpty()
    {
        bool any = _items.Count > 0;
        EmptyText.Visibility = any ? Visibility.Collapsed : Visibility.Visible;
        EventsList.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        string name = (NameBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(name))
        {
            StatusText.Text = P("Enter an event name first.", "先入一個事件名。");
            return;
        }
        if (DatePicker.Date is not DateTimeOffset date)
        {
            StatusText.Text = P("Pick a target date.", "揀一個目標日期。");
            return;
        }

        TimeSpan time = TimePicker.SelectedTime ?? TimeSpan.Zero;
        var local = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).Add(time);
        var target = new DateTimeOffset(local);

        InsertSorted(new CountdownItem(name, target));
        TickNow();
        UpdateEmpty();

        NameBox.Text = "";
        DatePicker.Date = null;
        TimePicker.SelectedTime = null;

        bool ok = await SaveAsync();
        StatusText.Text = ok
            ? P("Event added.", "已加入。")
            : P("Added (couldn't save to disk).", "已加入（但存唔到落磁碟）。");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CountdownItem item })
        {
            _items.Remove(item);
            UpdateEmpty();
            bool ok = await SaveAsync();
            StatusText.Text = ok
                ? P("Event removed.", "已刪除。")
                : P("Removed (couldn't save to disk).", "已刪除（但存唔到落磁碟）。");
        }
    }

    private void InsertSorted(CountdownItem item)
    {
        int i = 0;
        while (i < _items.Count && _items[i].Target <= item.Target) i++;
        _items.Insert(i, item);
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            var saved = await CountdownEventService.LoadAsync();
            _items.Clear();
            foreach (var entry in saved.OrderBy(s => s.Target))
                _items.Add(new CountdownItem(entry.Name ?? "", entry.Target));
            TickNow();
            UpdateEmpty();
        }
        catch
        {
            StatusText.Text = P("Couldn't load saved events.", "載入唔到已儲存嘅事件。");
        }
    }

    private async System.Threading.Tasks.Task<bool> SaveAsync()
    {
        var list = _items
            .Select(i => new CountdownEventService.EventEntry { Name = i.Name, Target = i.Target })
            .ToList();
        return await CountdownEventService.SaveAsync(list);
    }
}
