using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 單位價格比較 · Unit-price ("price per unit") comparison. Pure managed, never-throws helpers:
/// compute price-per-unit for each item, find the cheapest (best value) and how much more each
/// costs than the best. No I/O, no redirect.
/// </summary>
public static class UnitPriceService
{
    /// <summary>One comparison row's computed result.</summary>
    public sealed class Computed
    {
        public bool Valid;          // price &gt; 0-ish and quantity &gt; 0
        public double PerUnit;      // price / quantity (NaN when invalid)
        public bool IsBest;         // lowest per-unit among valid rows
        public double PercentMore;  // % more expensive than the best (0 for the best; NaN when invalid)
    }

    /// <summary>Safely parse a NumberBox value; NaN/negatives are treated per caller need.</summary>
    public static double Clean(double v) => double.IsNaN(v) || double.IsInfinity(v) ? 0 : v;

    /// <summary>
    /// Compute per-unit prices for the given (price, quantity) pairs, mark the best value and the
    /// % more expensive each valid row is vs. the best. Never throws; a null/empty input yields an
    /// empty list. Rows with quantity &lt;= 0 or price &lt; 0 are flagged invalid (excluded from "best").
    /// </summary>
    public static IReadOnlyList<Computed> Compute(IEnumerable<(double price, double quantity)> rows)
    {
        var results = new List<Computed>();
        if (rows == null) return results;

        double bestPerUnit = double.PositiveInfinity;

        try
        {
            foreach (var (price, quantity) in rows)
            {
                var c = new Computed();
                double p = Clean(price);
                double q = Clean(quantity);
                if (q > 0 && p >= 0)
                {
                    c.Valid = true;
                    c.PerUnit = p / q;
                    if (c.PerUnit < bestPerUnit) bestPerUnit = c.PerUnit;
                }
                else
                {
                    c.PerUnit = double.NaN;
                    c.PercentMore = double.NaN;
                }
                results.Add(c);
            }

            if (double.IsFinite(bestPerUnit) && bestPerUnit > 0)
            {
                foreach (var c in results)
                {
                    if (!c.Valid) continue;
                    c.IsBest = c.PerUnit <= bestPerUnit * (1 + 1e-9);
                    c.PercentMore = c.IsBest ? 0 : (c.PerUnit - bestPerUnit) / bestPerUnit * 100.0;
                }
            }
            else if (double.IsFinite(bestPerUnit))
            {
                // best per-unit is 0 (free): mark all zero-per-unit rows best, others "infinitely" more.
                foreach (var c in results)
                {
                    if (!c.Valid) continue;
                    c.IsBest = c.PerUnit <= 1e-12;
                    c.PercentMore = c.IsBest ? 0 : double.PositiveInfinity;
                }
            }
        }
        catch
        {
            // never throw — return whatever we have so far
        }

        return results;
    }

    /// <summary>Format a per-unit price with the currency symbol; safe for NaN.</summary>
    public static string FormatPerUnit(string currency, double perUnit, string unit)
    {
        if (double.IsNaN(perUnit) || double.IsInfinity(perUnit)) return "—";
        string cur = string.IsNullOrEmpty(currency) ? "" : currency;
        string u = string.IsNullOrWhiteSpace(unit) ? "" : "/" + unit.Trim();
        return cur + perUnit.ToString("0.####", CultureInfo.InvariantCulture) + u;
    }

    /// <summary>Format the "% more" figure; safe for NaN/Infinity.</summary>
    public static string FormatPercentMore(bool isBest, double percentMore)
    {
        if (isBest) return "";
        if (double.IsNaN(percentMore)) return "";
        if (double.IsInfinity(percentMore)) return "∞";
        return "+" + percentMore.ToString("0.#", CultureInfo.InvariantCulture) + "%";
    }

    /// <summary>Build a plain-text comparison summary suitable for the clipboard. Never throws.</summary>
    public static string BuildClipboard(string currency, IEnumerable<(string label, double price, double quantity, string unit)> items)
    {
        var sb = new StringBuilder();
        try
        {
            var pairs = new List<(double, double)>();
            var snapshot = new List<(string label, double price, double quantity, string unit)>();
            if (items != null)
            {
                foreach (var it in items)
                {
                    snapshot.Add(it);
                    pairs.Add((it.price, it.quantity));
                }
            }
            var computed = Compute(pairs);

            for (int i = 0; i < snapshot.Count; i++)
            {
                var it = snapshot[i];
                var c = i < computed.Count ? computed[i] : new Computed();
                string label = string.IsNullOrWhiteSpace(it.label) ? "#" + (i + 1) : it.label.Trim();
                string cur = string.IsNullOrEmpty(currency) ? "" : currency;
                string priceStr = cur + Clean(it.price).ToString("0.##", CultureInfo.InvariantCulture);
                string qtyStr = Clean(it.quantity).ToString("0.####", CultureInfo.InvariantCulture);
                string unit = string.IsNullOrWhiteSpace(it.unit) ? "" : it.unit.Trim();

                sb.Append(label)
                  .Append(": ").Append(priceStr)
                  .Append(" / ").Append(qtyStr).Append(unit)
                  .Append(" = ").Append(FormatPerUnit(currency, c.PerUnit, unit));
                if (c.IsBest) sb.Append("  ★ BEST / 最抵");
                else
                {
                    string pm = FormatPercentMore(c.IsBest, c.PercentMore);
                    if (pm.Length > 0) sb.Append("  (").Append(pm).Append(')');
                }
                sb.Append('\n');
            }
        }
        catch
        {
            // never throw
        }
        return sb.ToString();
    }
}
