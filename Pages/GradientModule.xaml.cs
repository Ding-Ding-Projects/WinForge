using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using WinForge.Services;

namespace WinForge.Pages;

/// <summary>
/// 漸變產生器 · CSS gradient generator — build a linear/radial CSS gradient from colour
/// stops, preview it live with a WinUI gradient brush, copy the CSS or randomise. Bilingual.
/// Pure managed C#, never-throws, self-contained. No redirect.
/// </summary>
public sealed partial class GradientModule : Page
{
    /// <summary>One editable colour-stop row bound to the ItemsControl.</summary>
    public sealed class StopVm : INotifyPropertyChanged
    {
        private readonly Action _changed;
        private string _hex;
        private double _position;

        public StopVm(string hex, double position, Action changed)
        {
            _hex = hex;
            _position = position;
            _changed = changed;
        }

        public string Hex
        {
            get => _hex;
            set { if (_hex != value) { _hex = value; OnChanged(nameof(Hex)); OnChanged(nameof(Swatch)); _changed(); } }
        }

        public double Position
        {
            get => _position;
            set { if (Math.Abs(_position - value) > 0.0001) { _position = value; OnChanged(nameof(Position)); _changed(); } }
        }

        /// <summary>A solid brush for the row swatch — transparent when the hex is invalid.</summary>
        public SolidColorBrush Swatch
        {
            get
            {
                if (GradientService.TryParseHex(Hex, out var r, out var g, out var b))
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
                return new SolidColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    private readonly ObservableCollection<StopVm> _stops = new();
    private bool _suppress;

    public GradientModule()
    {
        InitializeComponent();
        StopsList.ItemsSource = _stops;
        Loc.I.LanguageChanged += OnLanguageChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_stops.Count == 0)
        {
            _stops.Add(new StopVm("#ff0000", 0, OnStopChanged));
            _stops.Add(new StopVm("#0000ff", 100, OnStopChanged));
        }
        _suppress = true;
        if (TypeBox.SelectedIndex < 0) TypeBox.SelectedIndex = 0;
        _suppress = false;
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loc.I.LanguageChanged -= OnLanguageChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Render();

    private void OnStopChanged() => Refresh();

    private string P(string en, string zh) => Loc.I.Pick(en, zh);

    private void Render()
    {
        try
        {
            Header.Title = P("Gradient Generator", "漸變產生器");
            HeaderBlurb.Text = P("Design a CSS gradient from colour stops, preview it live and copy the CSS. Linear or radial.",
                "用色標砌一個 CSS 漸變，即時預覽並複製 CSS。可選線性或放射狀。");
            TypeLabel.Text = P("Type", "類型");
            LinearItem.Content = P("Linear", "線性");
            RadialItem.Content = P("Radial", "放射狀");
            AngleLabel.Text = P("Angle (deg)", "角度（度）");
            StopsLabel.Text = P("Colour stops", "色標");
            AddStopButton.Content = P("Add stop", "加色標");
            CopyButton.Content = P("Copy CSS", "複製 CSS");
            RandomButton.Content = P("Random", "隨機");
            Refresh();
        }
        catch { }
    }

    private GradientService.GradientKind CurrentKind()
        => TypeBox.SelectedIndex == 1 ? GradientService.GradientKind.Radial : GradientService.GradientKind.Linear;

    private void Type_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_suppress) return;
        if (AnglePanel != null)
            AnglePanel.Visibility = CurrentKind() == GradientService.GradientKind.Linear ? Visibility.Visible : Visibility.Collapsed;
        Refresh();
    }

    private void Angle_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_suppress) return;
        Refresh();
    }

    private void AddStop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            double pos = _stops.Count > 0 ? Math.Clamp(_stops[^1].Position, 0, 100) : 100;
            _stops.Add(new StopVm("#ffffff", pos, OnStopChanged));
            Refresh();
        }
        catch { }
    }

    private void RemoveStop_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.Tag is StopVm vm)
            {
                if (_stops.Count <= 1)
                {
                    SetStatus(P("Keep at least one colour stop.", "至少要留一個色標。"), false);
                    return;
                }
                _stops.Remove(vm);
                Refresh();
            }
        }
        catch { }
    }

    /// <summary>Collect valid stops; reports the first bad row via status. Returns null when unusable.</summary>
    private List<GradientService.Stop>? CollectStops()
    {
        var list = new List<GradientService.Stop>();
        int i = 0;
        foreach (var vm in _stops)
        {
            i++;
            if (!GradientService.TryParseHex(vm.Hex, out var r, out var g, out var b))
            {
                SetStatus(P($"Stop {i}: '{vm.Hex}' is not a valid hex colour (e.g. #ff8800).",
                            $"色標 {i}：「{vm.Hex}」唔係有效嘅十六進位色（例如 #ff8800）。"), false);
                return null;
            }
            var pos = vm.Position;
            if (double.IsNaN(pos) || pos < 0 || pos > 100)
            {
                SetStatus(P($"Stop {i}: position must be between 0 and 100.",
                            $"色標 {i}：位置要喺 0 至 100 之間。"), false);
                return null;
            }
            list.Add(new GradientService.Stop(r, g, b, pos));
        }
        if (list.Count == 0)
        {
            SetStatus(P("Add at least one colour stop.", "加至少一個色標。"), false);
            return null;
        }
        return list;
    }

    private void Refresh()
    {
        try
        {
            var stops = CollectStops();
            if (stops == null)
            {
                CssBox.Text = string.Empty;
                Preview.Background = null;
                return;
            }

            var kind = CurrentKind();
            double angle = double.IsNaN(AngleBox.Value) ? 0 : AngleBox.Value;

            CssBox.Text = GradientService.BuildCss(kind, angle, stops);
            Preview.Background = BuildBrush(kind, angle, stops);
            SetStatus(P($"{stops.Count} stop(s). Ready — copy or tweak.",
                        $"{stops.Count} 個色標。準備好，可複製或調整。"), true);
        }
        catch (Exception ex)
        {
            SetStatus(P("Could not render gradient: ", "無法產生漸變：") + ex.Message, false);
        }
    }

    /// <summary>Build a WinUI gradient brush from the stops, matching the CSS geometry.</summary>
    private static Brush BuildBrush(GradientService.GradientKind kind, double angle, List<GradientService.Stop> stops)
    {
        if (kind == GradientService.GradientKind.Radial)
        {
            // RadialGradientBrush.GradientStops is a read-only collection — add to it, don't assign.
            var radial = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5,
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
            };
            foreach (var s in stops)
            {
                radial.GradientStops.Add(new GradientStop
                {
                    Color = Windows.UI.Color.FromArgb(255, s.R, s.G, s.B),
                    Offset = Math.Clamp(s.Position, 0, 100) / 100.0,
                });
            }
            return radial;
        }

        var gs = new GradientStopCollection();
        foreach (var s in stops)
        {
            gs.Add(new GradientStop
            {
                Color = Windows.UI.Color.FromArgb(255, s.R, s.G, s.B),
                Offset = Math.Clamp(s.Position, 0, 100) / 100.0,
            });
        }

        var (sx, sy, ex, ey) = GradientService.AnglePoints(angle);
        return new LinearGradientBrush
        {
            StartPoint = new Windows.Foundation.Point(sx, sy),
            EndPoint = new Windows.Foundation.Point(ex, ey),
            GradientStops = gs,
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
        };
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var css = CssBox.Text;
            if (string.IsNullOrWhiteSpace(css))
            {
                SetStatus(P("Nothing to copy — fix the stops first.", "無嘢可複製 — 先修正色標。"), false);
                return;
            }
            var dp = new DataPackage();
            dp.SetText(css);
            Clipboard.SetContent(dp);
            SetStatus(P("CSS copied to the clipboard.", "已複製 CSS 到剪貼簿。"), true);
        }
        catch (Exception ex)
        {
            SetStatus(P("Copy failed: ", "複製失敗：") + ex.Message, false);
        }
    }

    private void Random_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _suppress = true;
            _stops.Clear();
            int n = GradientService.RandomInt(2, 4);
            for (int i = 0; i < n; i++)
            {
                var hex = GradientService.ToHex(GradientService.RandomByte(), GradientService.RandomByte(), GradientService.RandomByte());
                double pos = n == 1 ? 0 : Math.Round(i * 100.0 / (n - 1));
                _stops.Add(new StopVm(hex, pos, OnStopChanged));
            }
            if (GradientService.RandomInt(0, 1) == 0)
            {
                TypeBox.SelectedIndex = 0;
                AngleBox.Value = GradientService.RandomInt(0, 360);
            }
            else
            {
                TypeBox.SelectedIndex = 1;
            }
            AnglePanel.Visibility = CurrentKind() == GradientService.GradientKind.Linear ? Visibility.Visible : Visibility.Collapsed;
            _suppress = false;
            Refresh();
        }
        catch
        {
            _suppress = false;
        }
    }

    private void SetStatus(string text, bool ok)
    {
        try
        {
            StatusText.Text = text;
            StatusText.Foreground = ok
                ? (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                : (Brush)Application.Current.Resources["SystemFillColorCautionBrush"];
        }
        catch { }
    }
}
