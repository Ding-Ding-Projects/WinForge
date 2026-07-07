using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 命名色彩 · Named Colors — type a hex/rgb value (or drag R/G/B sliders), see the base swatch and the
/// NEAREST CSS/X11 named colour by RGB distance. Below, a searchable list of all 148 named colours;
/// click a row to copy its hex and load it. Pure managed, never throws, bilingual. No redirect.
/// </summary>
public sealed partial class ColorNameModule : Page
{
    /// <summary>Row model for the classic-bound ListView (Name, Hex, Brush).</summary>
    public sealed class ColorRow
    {
        public string Name { get; }
        public string Hex { get; }
        public Brush Brush { get; }
        public string LowerName { get; }
        public ColorNameService.NamedColor Source { get; }

        public ColorRow(ColorNameService.NamedColor c)
        {
            Source = c;
            Name = c.Name;
            Hex = c.Hex;
            LowerName = c.Name.ToLowerInvariant();
            Brush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, c.R, c.G, c.B));
        }
    }

    private readonly List<ColorRow> _allRows = new();
    private readonly ObservableCollection<ColorRow> _shown = new();
    private bool _suppress;

    public ColorNameModule()
    {
        InitializeComponent();
        foreach (var c in ColorNameService.All) _allRows.Add(new ColorRow(c));
        ColorList.ItemsSource = _shown;
        Loc.I.LanguageChanged += OnLang;
        Loaded += (_, _) => { Render(); Init(); };
        Unloaded += (_, _) => { Loc.I.LanguageChanged -= OnLang; };
    }

    private void OnLang(object? sender, EventArgs e) => Render();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = "Named Colors · 命名色彩";
            HeaderBlurb.Text = P("Type a colour (hex like #3498DB, or rgb(52,152,219)) or drag the sliders — WinForge shows the nearest CSS/X11 named colour. Search the full list below and click a row to copy its hex.",
                "打個色（16 進位好似 #3498DB，或者 rgb(52,152,219)）或者拉滑桿 — WinForge 會搵返最接近嘅 CSS/X11 具名色。下面搵成份清單，撳一行就複製佢個 16 進位碼。");
            InputLabel.Text = P("Colour value", "色彩值");
            RLabel.Text = "R";
            GLabel.Text = "G";
            BLabel.Text = "B";
            NearestLabel.Text = P("Nearest named colour", "最接近嘅具名色");
            ListTitle.Text = P("All named colours (148)", "全部具名色（148）");
            FilterBox.PlaceholderText = P("Search by name…", "用名搵…");
            InputBox.PlaceholderText = "#3498DB";
            RefreshFromInputsSilently();
        }
        catch { /* never throw from UI render */ }
    }

    private void Init()
    {
        try
        {
            RebuildList(string.Empty);
            _suppress = true;
            InputBox.Text = "#3498DB";
            _suppress = false;
            ApplyRgb(0x34, 0x98, 0xDB, updateInputText: false);
        }
        catch { SetStatus(P("Ready.", "準備好。")); }
    }

    // ---- input handlers -------------------------------------------------

    private void Input_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            if (ColorNameService.TryParse(InputBox.Text, out var rgb))
            {
                ApplyRgb(rgb.R, rgb.G, rgb.B, updateInputText: false, updateSliders: true);
                SetStatus(P("Parsed OK.", "解析成功。"));
            }
            else
            {
                SetStatus(P("Couldn't read that — try #RRGGBB, #RGB, rgb(r,g,b) or a colour name.",
                    "讀唔到 — 試下 #RRGGBB、#RGB、rgb(r,g,b) 或者色彩名。"));
            }
        }
        catch { SetStatus(P("Couldn't read that value.", "讀唔到呢個值。")); }
    }

    private void Slider_Changed(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            byte r = (byte)Math.Clamp((int)Math.Round(RSlider.Value), 0, 255);
            byte g = (byte)Math.Clamp((int)Math.Round(GSlider.Value), 0, 255);
            byte b = (byte)Math.Clamp((int)Math.Round(BSlider.Value), 0, 255);
            ApplyRgb(r, g, b, updateInputText: true, updateSliders: false);
        }
        catch { /* ignore */ }
    }

    private void Filter_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppress) return;
        try { RebuildList(FilterBox.Text ?? string.Empty); }
        catch { /* ignore */ }
    }

    private void ColorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        try
        {
            if (ColorList.SelectedItem is not ColorRow row) return;
            // Load as input + copy hex to clipboard.
            _suppress = true;
            InputBox.Text = row.Hex;
            _suppress = false;
            ApplyRgb(row.Source.R, row.Source.G, row.Source.B, updateInputText: false, updateSliders: true);
            CopyToClipboard(row.Hex);
            SetStatus(P($"Copied {row.Name} ({row.Hex}) to clipboard.", $"已複製 {row.Name}（{row.Hex}）去剪貼簿。"));
        }
        catch { SetStatus(P("Copy failed.", "複製失敗。")); }
    }

    // ---- core -----------------------------------------------------------

    private void RefreshFromInputsSilently()
    {
        try
        {
            byte r = (byte)Math.Clamp((int)Math.Round(RSlider.Value), 0, 255);
            byte g = (byte)Math.Clamp((int)Math.Round(GSlider.Value), 0, 255);
            byte b = (byte)Math.Clamp((int)Math.Round(BSlider.Value), 0, 255);
            ApplyRgb(r, g, b, updateInputText: false, updateSliders: false);
        }
        catch { /* ignore */ }
    }

    private void ApplyRgb(byte r, byte g, byte b, bool updateInputText, bool updateSliders = true)
    {
        try
        {
            var color = Windows.UI.Color.FromArgb(0xFF, r, g, b);
            BaseSwatch.Background = new SolidColorBrush(color);

            if (updateSliders)
            {
                _suppress = true;
                RSlider.Value = r; GSlider.Value = g; BSlider.Value = b;
                _suppress = false;
            }
            if (updateInputText)
            {
                _suppress = true;
                InputBox.Text = $"#{r:X2}{g:X2}{b:X2}";
                _suppress = false;
            }

            var near = ColorNameService.Nearest(r, g, b, out double dist);
            NearestSwatch.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, near.R, near.G, near.B));
            NearestName.Text = near.Name;
            bool exact = dist <= 0.5;
            NearestDetail.Text = exact
                ? P($"{near.Hex} · exact match", $"{near.Hex} · 完全吻合")
                : P($"{near.Hex} · Δ {dist:0.0}", $"{near.Hex} · 差距 {dist:0.0}");
        }
        catch { SetStatus(P("Couldn't render that colour.", "呢個色彩顯示唔到。")); }
    }

    private void RebuildList(string filter)
    {
        try
        {
            string f = (filter ?? string.Empty).Trim().ToLowerInvariant();
            _shown.Clear();
            foreach (var row in _allRows)
                if (f.Length == 0 || row.LowerName.Contains(f)) _shown.Add(row);
            if (_shown.Count == 0)
                SetStatus(P("No named colours match that search.", "冇具名色符合你嘅搜尋。"));
        }
        catch { /* ignore */ }
    }

    private static void CopyToClipboard(string text)
    {
        try
        {
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(text ?? string.Empty);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
        catch { /* clipboard may be busy; ignore */ }
    }

    private void SetStatus(string text)
    {
        try { StatusText.Text = text; } catch { /* ignore */ }
    }
}
