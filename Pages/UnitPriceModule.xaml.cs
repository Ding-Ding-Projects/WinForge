using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 單位價格比較 · Unit-price comparison — compare items by price-per-unit, highlight the best value and
/// show how much more each costs than the best. Pure managed, never-throws, bilingual. No redirect.
/// </summary>
public sealed partial class UnitPriceModule : Page
{
    private readonly ObservableCollection<Row> _rows = new();

    public UnitPriceModule()
    {
        InitializeComponent();
        ItemsList.ItemsSource = _rows;
        Loc.I.LanguageChanged += OnLang;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_rows.Count == 0)
        {
            AddRow();
            AddRow();
        }
        Render();
        Recompute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLang;
        foreach (var r in _rows) r.PropertyChanged -= Row_PropertyChanged;
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Unit Price · 單位價格";
            HeaderBlurb.Text = P(
                "Compare items by price-per-unit. Enter each item's price, quantity and unit (g, ml, ea…) — the cheapest per unit is flagged as the best value, and everything else shows how much more it costs.",
                "用單位價格嚟格價。填每件嘢嘅價錢、數量同單位（g、ml、個…）— 每單位最平嗰個會標示為最抵，其餘會顯示貴幾多。");
            CurrencyLabel.Text = P("Currency symbol", "貨幣符號");
            AddBtn.Content = P("Add item", "加一項");
            CopyBtn.Content = P("Copy comparison", "複製比較");
            foreach (var r in _rows) r.RefreshPlaceholder(P("Item name", "項目名稱"));
            UpdateStatus();
        }
        catch { /* never throw from UI render */ }
    }

    private void AddRow()
    {
        var r = new Row { LabelPlaceholder = P("Item name", "項目名稱"), Unit = _rows.Count > 0 ? _rows[0].Unit : "" };
        r.PropertyChanged += Row_PropertyChanged;
        _rows.Add(r);
    }

    private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Recompute when an input changes; ignore our own output-property updates to avoid loops.
        if (e.PropertyName is nameof(Row.Price) or nameof(Row.Quantity) or nameof(Row.Unit) or nameof(Row.Label))
            Recompute();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        try { AddRow(); Recompute(); } catch { }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is Row r)
            {
                r.PropertyChanged -= Row_PropertyChanged;
                _rows.Remove(r);
                Recompute();
            }
        }
        catch { }
    }

    private void Currency_Changed(object sender, TextChangedEventArgs e) => Recompute();

    private void Recompute()
    {
        try
        {
            string cur = CurrencyBox?.Text ?? "";
            var pairs = new List<(double, double)>();
            foreach (var r in _rows) pairs.Add((r.Price, r.Quantity));
            var computed = UnitPriceService.Compute(pairs);

            for (int i = 0; i < _rows.Count && i < computed.Count; i++)
            {
                var r = _rows[i];
                var c = computed[i];
                r.PerUnitText = UnitPriceService.FormatPerUnit(cur, c.PerUnit, r.Unit);
                if (!c.Valid)
                {
                    r.BadgeText = "";
                    r.BadgeBrush = new SolidColorBrush(Colors.Gray);
                }
                else if (c.IsBest)
                {
                    r.BadgeText = P("★ Best value", "★ 最抵");
                    r.BadgeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 204, 113));
                }
                else
                {
                    r.BadgeText = UnitPriceService.FormatPercentMore(c.IsBest, c.PercentMore) + P(" more", " 貴啲");
                    r.BadgeBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 231, 111, 81));
                }
            }
            UpdateStatus();
        }
        catch { UpdateStatus(); }
    }

    private void UpdateStatus()
    {
        try
        {
            int valid = 0, invalid = 0;
            foreach (var r in _rows)
            {
                double q = UnitPriceService.Clean(r.Quantity);
                double p = UnitPriceService.Clean(r.Price);
                if (q > 0 && p >= 0) valid++;
                else if (q <= 0) invalid++;
            }

            if (invalid > 0)
                StatusText.Text = P($"{invalid} item(s) need a quantity greater than zero to compare.",
                    $"有 {invalid} 項嘅數量要大過零先可以比較。");
            else if (valid < 2)
                StatusText.Text = P("Add at least two items with a price and quantity to compare.",
                    "至少填兩項有價錢同數量嘅嘢先可以格價。");
            else
                StatusText.Text = P($"Comparing {valid} items — the greenest row is the best value.",
                    $"正比較緊 {valid} 項 — 綠色嗰行最抵。");
        }
        catch { }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = new List<(string, double, double, string)>();
            foreach (var r in _rows) items.Add((r.Label, r.Price, r.Quantity, r.Unit));
            string text = UnitPriceService.BuildClipboard(CurrencyBox?.Text ?? "", items);
            if (string.IsNullOrWhiteSpace(text))
            {
                StatusText.Text = P("Nothing to copy yet.", "暫時無嘢可以複製。");
                return;
            }
            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
            StatusText.Text = P("Comparison copied to clipboard.", "比較已複製到剪貼簿。");
        }
        catch
        {
            try { StatusText.Text = P("Couldn't copy to the clipboard.", "複製到剪貼簿失敗。"); } catch { }
        }
    }

    /// <summary>One comparison row (bound via classic {Binding}); notifies on input + output changes.</summary>
    public sealed class Row : INotifyPropertyChanged
    {
        private string _label = "";
        private double _price;
        private double _quantity;
        private string _unit = "";
        private string _perUnitText = "—";
        private string _badgeText = "";
        private Brush _badgeBrush = new SolidColorBrush(Colors.Gray);
        private string _labelPlaceholder = "Item name";

        public string Label { get => _label; set => Set(ref _label, value ?? ""); }
        public double Price { get => _price; set => Set(ref _price, value); }
        public double Quantity { get => _quantity; set => Set(ref _quantity, value); }
        public string Unit { get => _unit; set => Set(ref _unit, value ?? ""); }
        public string PerUnitText { get => _perUnitText; set => Set(ref _perUnitText, value); }
        public string BadgeText { get => _badgeText; set => Set(ref _badgeText, value); }
        public Brush BadgeBrush { get => _badgeBrush; set => Set(ref _badgeBrush, value); }
        public string LabelPlaceholder { get => _labelPlaceholder; set => Set(ref _labelPlaceholder, value); }

        public void RefreshPlaceholder(string placeholder) => LabelPlaceholder = placeholder;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
