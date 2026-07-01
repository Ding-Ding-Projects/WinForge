using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 數字格式化 · Pure-managed number formatter — turns a parsed value into a set of
/// culture-aware string variants (grouped, fixed, currency, percent, scientific,
/// accounting, zero-padded) using <see cref="CultureInfo"/> / <see cref="NumberFormatInfo"/>.
/// Never throws; every entry point degrades gracefully. No I/O, no redirect.
/// </summary>
public static class NumberFormatService
{
    /// <summary>A single labelled, copyable formatted variant.</summary>
    public readonly record struct FormatItem(string Label, string Value);

    /// <summary>A currency-culture choice for the picker.</summary>
    public readonly record struct CurrencyChoice(string Culture, string Display);

    /// <summary>Currency cultures offered in the UI (culture code + friendly label).</summary>
    public static IReadOnlyList<CurrencyChoice> Currencies { get; } = new List<CurrencyChoice>
    {
        new("en-US", "en-US · $ USD"),
        new("de-DE", "de-DE · € EUR"),
        new("en-GB", "en-GB · £ GBP"),
        new("ja-JP", "ja-JP · ¥ JPY"),
        new("zh-HK", "zh-HK · HK$ HKD"),
        new("zh-CN", "zh-CN · ¥ CNY"),
    };

    /// <summary>
    /// Try to parse user text as a number. Accepts leading/trailing whitespace, a
    /// leading +, thousands separators and a decimal point (invariant culture, with a
    /// current-culture fallback). Returns false on empty/non-numeric input.
    /// </summary>
    public static bool TryParse(string? text, out double value)
    {
        value = 0d;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim();
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if (double.TryParse(t, styles, CultureInfo.InvariantCulture, out value)) return true;
        if (double.TryParse(t, styles, CultureInfo.CurrentCulture, out value)) return true;
        value = 0d;
        return false;
    }

    /// <summary>
    /// Build all formatted variants for <paramref name="value"/>. Any individual
    /// formatter that fails yields an em-dash rather than throwing, so the list is
    /// always complete. <paramref name="labels"/> supplies the localized labels.
    /// </summary>
    public static IReadOnlyList<FormatItem> BuildAll(
        double value, int decimals, string currencyCulture, int padWidth, Labels labels)
    {
        decimals = Clamp(decimals, 0, 10);
        padWidth = Clamp(padWidth, 0, 40);
        var inv = CultureInfo.InvariantCulture;
        var items = new List<FormatItem>(8);

        items.Add(new(labels.Grouped, Safe(() => value.ToString("#,##0.###", inv))));
        items.Add(new(labels.Fixed, Safe(() => value.ToString("F" + decimals.ToString(inv), inv))));
        items.Add(new(labels.Currency, Safe(() => value.ToString("C", Culture(currencyCulture)))));
        items.Add(new(labels.Percent, Safe(() => value.ToString("P" + decimals.ToString(inv), inv))));
        items.Add(new(labels.Scientific, Safe(() => value.ToString("E" + decimals.ToString(inv), inv))));
        items.Add(new(labels.Accounting, Safe(() => Accounting(value, decimals, inv))));
        items.Add(new(labels.Padded, Safe(() => ZeroPad(value, padWidth, inv))));

        return items;
    }

    /// <summary>Localized labels for the variants (filled from the page via Loc.I.Pick).</summary>
    public readonly record struct Labels(
        string Grouped, string Fixed, string Currency, string Percent,
        string Scientific, string Accounting, string Padded);

    // Accounting: negatives wrapped in parentheses, thousands-grouped.
    private static string Accounting(double value, int decimals, CultureInfo inv)
    {
        var pattern = "#,##0." + new string('0', Clamp(decimals, 0, 10));
        if (decimals == 0) pattern = "#,##0";
        var body = Math.Abs(value).ToString(pattern, inv);
        return value < 0 ? "(" + body + ")" : body;
    }

    // Zero-pad the integer magnitude to a width, preserving a leading minus sign.
    private static string ZeroPad(double value, int width, CultureInfo inv)
    {
        bool neg = value < 0 || (value == 0 && double.IsNegative(value));
        var mag = Math.Abs(value);
        // Use the whole-number part for padding; keep any fractional part appended.
        var whole = Math.Truncate(mag).ToString("0", inv);
        var frac = (mag - Math.Truncate(mag));
        string tail = string.Empty;
        if (frac > 0)
        {
            var f = mag.ToString("0.###############", inv);
            int dot = f.IndexOf('.');
            if (dot >= 0) tail = f.Substring(dot);
        }
        var padded = whole.PadLeft(Math.Max(width, whole.Length), '0');
        return (neg ? "-" : string.Empty) + padded + tail;
    }

    private static CultureInfo Culture(string? name)
    {
        try
        {
            return string.IsNullOrWhiteSpace(name)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(name);
        }
        catch
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static string Safe(Func<string> f)
    {
        try { return f() ?? "—"; }
        catch { return "—"; }
    }

    private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
}
