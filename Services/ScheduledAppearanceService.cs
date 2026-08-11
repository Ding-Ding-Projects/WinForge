using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace WinForge.Services;

/// <summary>
/// Applies the appearance values selected by an active scheduled rule to the live shell. The
/// values remain temporary; when no rule is active, the persisted base values are applied again.
/// </summary>
public static class ScheduledAppearanceService
{
    private const double DefaultFontSize = 14;

    public static void Apply(FrameworkElement? root, IReadOnlyDictionary<string, string>? scheduled)
    {
        if (root is null) return;
        try
        {
            string density = scheduled is not null && scheduled.TryGetValue("density", out string? scheduledDensity)
                ? scheduledDensity
                : SettingsStore.Get("density", "Comfortable");
            string accent = scheduled is not null && scheduled.TryGetValue("accent", out string? scheduledAccent)
                ? scheduledAccent
                : SettingsStore.Get("accent", "#54E07E");
            string fontFamily = scheduled is not null && scheduled.TryGetValue("fontFamily", out string? scheduledFont)
                ? scheduledFont
                : SettingsStore.Get("fontFamily", string.Empty);
            string fontScaleText = scheduled is not null && scheduled.TryGetValue("fontScale", out string? scheduledScale)
                ? scheduledScale
                : SettingsStore.Get("fontScale", "1");
            string fontWeightText = scheduled is not null && scheduled.TryGetValue("fontWeight", out string? scheduledWeight)
                ? scheduledWeight
                : SettingsStore.Get("fontWeight", "Normal");

            double densityFactor = density.Equals("Compact", StringComparison.OrdinalIgnoreCase) ? 0.9
                : density.Equals("Spacious", StringComparison.OrdinalIgnoreCase) ? 1.1 : 1.0;
            double scale = double.TryParse(fontScaleText, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedScale)
                ? Math.Clamp(parsedScale, 0.75, 2.0) : 1.0;
            double fontSize = DefaultFontSize * densityFactor * scale;

            foreach (Control control in EnumerateControls(root))
            {
                control.FontSize = fontSize;
                if (!string.IsNullOrWhiteSpace(fontFamily))
                {
                    try { control.FontFamily = new FontFamily(fontFamily); } catch { }
                }
                if (TryWeight(fontWeightText, out Windows.UI.Text.FontWeight weight)) control.FontWeight = weight;
            }

            if (TryParseColor(accent, out Color color))
            {
                var resources = Application.Current.Resources;
                resources["SystemAccentColor"] = color;
                resources["SystemAccentColorLight1"] = Lighten(color, 0.12);
                resources["SystemAccentColorDark1"] = Darken(color, 0.18);
                resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(color);
            }
        }
        catch (Exception exception)
        {
            CrashLogger.Log("scheduled-appearance.apply", exception);
        }
    }

    private static IEnumerable<Control> EnumerateControls(DependencyObject root)
    {
        if (root is Control control) yield return control;
        int count = 0;
        try { count = VisualTreeHelper.GetChildrenCount(root); } catch { }
        for (int i = 0; i < count; i++)
        {
            DependencyObject? child = null;
            try { child = VisualTreeHelper.GetChild(root, i); } catch { }
            if (child is null) continue;
            foreach (Control descendant in EnumerateControls(child)) yield return descendant;
        }
    }

    private static bool TryWeight(string value, out Windows.UI.Text.FontWeight weight)
    {
        weight = FontWeights.Normal;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numeric))
        {
            weight = new Windows.UI.Text.FontWeight { Weight = (ushort)Math.Clamp(numeric, 100, 900) };
            return true;
        }
        weight = value.Trim().ToLowerInvariant() switch
        {
            "light" => FontWeights.Light,
            "semilight" => FontWeights.SemiLight,
            "semibold" => FontWeights.SemiBold,
            "bold" => FontWeights.Bold,
            "black" => FontWeights.Black,
            _ => FontWeights.Normal,
        };
        return true;
    }

    private static bool TryParseColor(string value, out Color color)
    {
        color = default;
        string text = (value ?? string.Empty).Trim().TrimStart('#');
        if (text.Length == 6) text = "FF" + text;
        if (text.Length != 8) return false;
        try
        {
            color = Color.FromArgb(
                byte.Parse(text[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(text.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            return true;
        }
        catch { return false; }
    }

    private static Color Lighten(Color color, double amount) => Color.FromArgb(color.A,
        (byte)(color.R + (255 - color.R) * amount),
        (byte)(color.G + (255 - color.G) * amount),
        (byte)(color.B + (255 - color.B) * amount));

    private static Color Darken(Color color, double amount) => Color.FromArgb(color.A,
        (byte)(color.R * (1 - amount)), (byte)(color.G * (1 - amount)), (byte)(color.B * (1 - amount)));
}
