using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 時間與單位工具 · Time &amp; Unit Tools — a live world-clock board (updates each second), a
/// timezone converter (TimeZoneInfo.ConvertTimeBySystemTimeZoneId), and offline unit conversions.
/// All data comes from the OS (TimeZoneInfo). No network, no redirect. Bilingual.
/// </summary>
public sealed partial class TimeUnitModule : Page
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly List<string> _boardIds = new();
    private readonly List<TimeZoneInfo> _zones = TimeZoneService.All.ToList();
    private bool _ready;

    public TimeUnitModule()
    {
        InitializeComponent();
        _boardIds.AddRange(TimeZoneService.DefaultBoardIds);
        _timer.Tick += (_, _) => Tick();
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += (_, _) =>
        {
            BuildZoneCombos();
            BuildUnitCombos();
            Render();
            RebuildBoard();
            _ready = true;
            ConvertZone();
            ConvertUnit();
            Tick();
            _timer.Start();
        };
        Unloaded += (_, _) => { _timer.Stop(); Loc.I.LanguageChanged -= OnLanguageChanged; };
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private string ZoneText(TimeZoneInfo z) => $"{TimeZoneService.OffsetLabel(z)}  {z.DisplayName}";

    private void Render()
    {
        Header.Title = "Time & Unit Tools · 時間與單位工具";
        HeaderBlurb.Text = P("A live world clock, a timezone converter and handy unit conversions — all offline, straight from Windows' own time-zone data.",
            "即時世界時鐘、時區換算同常用單位轉換 — 全部離線，直接用 Windows 自己嘅時區資料。");

        LocalCaption.Text = P("This PC's time zone", "呢部電腦嘅時區");
        BoardTitle.Text = P("World clock", "世界時鐘");
        AddCityBtn.Content = P("Add city", "加城市");
        ConvTitle.Text = P("Timezone converter", "時區換算");
        ConvWhenLabel.Text = P("When", "時間");
        ConvFromLabel.Text = P("From", "由");
        ConvToLabel.Text = P("To", "去");
        NowBtn.Content = P("Now", "而家");
        UnitTitle.Text = P("Unit converter", "單位換算");
        UnitValueLabel.Text = P("Value", "數值");
        UnitFromLabel.Text = P("From", "由");
        UnitToLabel.Text = P("To", "去");

        // Refresh combo display labels for current language (unit category names are localized).
        if (_ready)
        {
            RefreshUnitCategoryLabels();
            UpdateLocalClock();
            RebuildBoard();
            ConvertZone();
            ConvertUnit();
        }
    }

    // ---------- Time zones ----------

    private void BuildZoneCombos()
    {
        foreach (var combo in new[] { ConvFrom, ConvTo, AddZoneBox })
        {
            combo.Items.Clear();
            foreach (var z in _zones)
                combo.Items.Add(new ComboBoxItem { Content = ZoneText(z), Tag = z.Id });
        }

        SelectZone(ConvFrom, TimeZoneService.Local.Id);
        var firstOther = _zones.FirstOrDefault(z => z.Id != TimeZoneService.Local.Id) ?? TimeZoneService.Local;
        SelectZone(ConvTo, firstOther.Id);
        if (AddZoneBox.Items.Count > 0) AddZoneBox.SelectedIndex = 0;

        var now = DateTimeOffset.Now;
        ConvDate.Date = now;
        ConvTime.Time = now.TimeOfDay;
    }

    private static void SelectZone(ComboBox combo, string id)
    {
        for (int i = 0; i < combo.Items.Count; i++)
            if (combo.Items[i] is ComboBoxItem it && (string)it.Tag == id) { combo.SelectedIndex = i; return; }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private static string? SelectedZoneId(ComboBox combo)
        => (combo.SelectedItem as ComboBoxItem)?.Tag as string;

    private void UpdateLocalClock()
    {
        var z = TimeZoneService.Local;
        var now = TimeZoneService.Now(z);
        LocalZone.Text = ZoneText(z) + (TimeZoneService.IsDst(z) ? P("  (DST)", "（夏令時間）") : "");
        LocalTime.Text = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var culture = CultureInfo.GetCultureInfo(Loc.I.PickSingle("en-US", "zh-HK"));
        LocalDate.Text = now.ToString("dddd, dd MMM yyyy", culture);
    }

    private void AddCity_Click(object sender, RoutedEventArgs e)
    {
        var id = (AddZoneBox.SelectedItem as ComboBoxItem)?.Tag as string;
        if (id is null || _boardIds.Contains(id)) return;
        _boardIds.Add(id);
        RebuildBoard();
    }

    private void RebuildBoard()
    {
        Board.Items.Clear();
        foreach (var id in _boardIds.ToList())
        {
            var z = TimeZoneService.Find(id);
            if (z is null) { _boardIds.Remove(id); continue; }
            Board.Items.Add(BuildCityCard(z));
        }
        UpdateBoardTimes();
    }

    private Border BuildCityCard(TimeZoneInfo z)
    {
        var grid = new Grid { ColumnSpacing = 10 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        var name = new TextBlock { Text = z.DisplayName, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
        var offset = new TextBlock
        {
            Text = TimeZoneService.OffsetLabel(z) + (TimeZoneService.IsDst(z) ? P("  · DST", "　·　夏令") : ""),
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        left.Children.Add(name);
        left.Children.Add(offset);
        Grid.SetColumn(left, 0);

        var time = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"),
            FontSize = 22,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = "time",
        };
        Grid.SetColumn(time, 1);

        var remove = new Button
        {
            Content = new FontIcon { Glyph = "", FontSize = 12 },
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Tag = z.Id,
        };
        remove.Click += Remove_Click;
        ToolTipService.SetToolTip(remove, P("Remove", "移除"));
        Grid.SetColumn(remove, 2);

        grid.Children.Add(left);
        grid.Children.Add(time);
        grid.Children.Add(remove);

        return new Border
        {
            Padding = new Thickness(14, 10, 12, 10),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Tag = z.Id,
            Child = grid,
        };
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string id)
        {
            _boardIds.Remove(id);
            RebuildBoard();
        }
    }

    private void UpdateBoardTimes()
    {
        var utc = DateTimeOffset.UtcNow;
        foreach (var item in Board.Items)
        {
            if (item is not Border border || border.Tag is not string id) continue;
            var z = TimeZoneService.Find(id);
            if (z is null) continue;
            var local = TimeZoneInfo.ConvertTime(utc, z);
            if (border.Child is Grid g)
                foreach (var c in g.Children)
                    if (c is TextBlock tb && (tb.Tag as string) == "time")
                        tb.Text = local.ToString("HH:mm:ss") + "\n" + local.ToString("ddd dd MMM");
        }
    }

    private void Tick()
    {
        UpdateLocalClock();
        UpdateBoardTimes();
    }

    private void Now_Click(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.Now;
        ConvDate.Date = now;
        ConvTime.Time = now.TimeOfDay;
        ConvertZone();
    }

    private void Conv_Changed(object sender, SelectionChangedEventArgs e) => ConvertZone();

    private void ConvertZone()
    {
        if (!_ready) return;
        var fromId = SelectedZoneId(ConvFrom);
        var toId = SelectedZoneId(ConvTo);
        if (fromId is null || toId is null) return;

        var date = ConvDate.Date?.DateTime ?? DateTime.Now;
        var time = ConvTime.Time;
        var source = date.Date + time;

        try
        {
            var result = TimeZoneService.Convert(source, fromId, toId);
            var fromZ = TimeZoneService.Find(fromId)!;
            var toZ = TimeZoneService.Find(toId)!;
            ConvResult.Text =
                $"{source:HH:mm  ddd dd MMM yyyy}  ({TimeZoneService.OffsetLabel(fromZ)})\n" +
                $"= {result:HH:mm  ddd dd MMM yyyy}  ({TimeZoneService.OffsetLabel(toZ)})";
        }
        catch (Exception ex)
        {
            ConvResult.Text = P("Could not convert: ", "換算失敗：") + ex.Message;
        }
    }

    // ---------- Units ----------

    private void BuildUnitCombos()
    {
        UnitCategoryBox.Items.Clear();
        foreach (var cat in UnitConvertService.Categories)
            UnitCategoryBox.Items.Add(new ComboBoxItem { Content = cat.Label(Loc.I), Tag = cat.Id });
        UnitCategoryBox.SelectedIndex = 0;
        PopulateUnits();
    }

    private void RefreshUnitCategoryLabels()
    {
        for (int i = 0; i < UnitCategoryBox.Items.Count; i++)
            if (UnitCategoryBox.Items[i] is ComboBoxItem it && it.Tag is string id)
                it.Content = UnitConvertService.Category(id)?.Label(Loc.I) ?? (string)it.Content;
        // Re-label unit combos too.
        var cat = SelectedCategory();
        if (cat is not null)
        {
            foreach (var combo in new[] { UnitFrom, UnitTo })
                for (int i = 0; i < combo.Items.Count && i < cat.Units.Count; i++)
                    if (combo.Items[i] is ComboBoxItem it) it.Content = cat.Units[i].Label(Loc.I);
        }
    }

    private UnitCategory? SelectedCategory()
        => (UnitCategoryBox.SelectedItem as ComboBoxItem)?.Tag is string id ? UnitConvertService.Category(id) : null;

    private void PopulateUnits()
    {
        var cat = SelectedCategory();
        if (cat is null) return;
        foreach (var combo in new[] { UnitFrom, UnitTo })
        {
            combo.Items.Clear();
            foreach (var u in cat.Units)
                combo.Items.Add(new ComboBoxItem { Content = u.Label(Loc.I), Tag = u.Id });
        }
        UnitFrom.SelectedIndex = 0;
        UnitTo.SelectedIndex = cat.Units.Count > 1 ? 1 : 0;
    }

    private void UnitCategory_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) { PopulateUnits(); return; }
        PopulateUnits();
        ConvertUnit();
    }

    private void Unit_Changed(object sender, object e) => ConvertUnit();

    private void ConvertUnit()
    {
        if (!_ready) return;
        var cat = SelectedCategory();
        if (cat is null) return;
        var fromId = (UnitFrom.SelectedItem as ComboBoxItem)?.Tag as string;
        var toId = (UnitTo.SelectedItem as ComboBoxItem)?.Tag as string;
        var from = cat.Units.FirstOrDefault(u => u.Id == fromId);
        var to = cat.Units.FirstOrDefault(u => u.Id == toId);
        if (from is null || to is null) return;

        double value = double.IsNaN(UnitValueBox.Value) ? 0 : UnitValueBox.Value;
        double result = UnitConvertService.Convert(value, from, to);

        UnitResult.Text =
            $"{value.ToString("0.######", CultureInfo.InvariantCulture)} {from.Label(Loc.I)}\n" +
            $"= {result.ToString("0.##########", CultureInfo.InvariantCulture)} {to.Label(Loc.I)}";
    }
}
