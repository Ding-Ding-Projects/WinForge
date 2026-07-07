using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 夾錢分帳 · Expense splitter — pure managed. Given people and expenses (each with a payer &amp; amount),
/// computes per-person paid / fair-share / net balance, then a MINIMAL greedy settle-up transfer list
/// (biggest creditor pays biggest debtor). No I/O, no throws — callers pass validated-ish data and get a
/// robust result back. Bilingual text is formed by the caller via Loc.I.Pick.
/// </summary>
public static class ExpenseSplitService
{
    /// <summary>One person's computed balance line.</summary>
    public sealed class PersonBalance
    {
        public string Name { get; init; } = "";
        public double Paid { get; init; }
        public double Share { get; init; }
        public double Net { get; init; } // Paid - Share; positive = is owed, negative = owes
    }

    /// <summary>One settle-up transfer: <see cref="From"/> pays <see cref="To"/> <see cref="Amount"/>.</summary>
    public sealed class Transfer
    {
        public string From { get; init; } = "";
        public string To { get; init; } = "";
        public double Amount { get; init; }
    }

    /// <summary>Full computed result of a split.</summary>
    public sealed class SplitResult
    {
        public double GrandTotal { get; init; }
        public double FairShare { get; init; }
        public int PeopleCount { get; init; }
        public IReadOnlyList<PersonBalance> Balances { get; init; } = Array.Empty<PersonBalance>();
        public IReadOnlyList<Transfer> Transfers { get; init; } = Array.Empty<Transfer>();
    }

    private const double Epsilon = 0.005; // half a cent — below this we treat as settled

    /// <summary>
    /// Compute balances + minimal transfers. Never throws. <paramref name="paidByPerson"/> maps a person name
    /// to the total they paid; every name in <paramref name="people"/> is included even if they paid nothing.
    /// </summary>
    public static SplitResult Compute(IReadOnlyList<string> people, IReadOnlyDictionary<string, double> paidByPerson)
    {
        try
        {
            var names = (people ?? Array.Empty<string>())
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim())
                .ToList();

            if (names.Count == 0)
                return new SplitResult();

            double grand = 0;
            var paid = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var n in names) paid[n] = 0;
            if (paidByPerson != null)
            {
                foreach (var kv in paidByPerson)
                {
                    if (kv.Key == null) continue;
                    var key = kv.Key.Trim();
                    var amt = SanitizeAmount(kv.Value);
                    if (paid.ContainsKey(key)) paid[key] += amt;
                    grand += amt;
                }
            }

            double share = grand / names.Count;

            var balances = names
                .Select(n => new PersonBalance
                {
                    Name = n,
                    Paid = paid[n],
                    Share = share,
                    Net = paid[n] - share,
                })
                .ToList();

            var transfers = SettleUp(balances);

            return new SplitResult
            {
                GrandTotal = grand,
                FairShare = share,
                PeopleCount = names.Count,
                Balances = balances,
                Transfers = transfers,
            };
        }
        catch
        {
            return new SplitResult();
        }
    }

    /// <summary>Greedy minimal settle-up: repeatedly match the biggest creditor with the biggest debtor.</summary>
    private static List<Transfer> SettleUp(IReadOnlyList<PersonBalance> balances)
    {
        var result = new List<Transfer>();
        try
        {
            // Work on mutable copies keyed by name.
            var creditors = balances.Where(b => b.Net > Epsilon)
                .Select(b => (b.Name, Amt: b.Net)).ToList();
            var debtors = balances.Where(b => b.Net < -Epsilon)
                .Select(b => (b.Name, Amt: -b.Net)).ToList();

            int guard = 0;
            int maxIterations = (creditors.Count + debtors.Count) * 4 + 8;
            while (creditors.Count > 0 && debtors.Count > 0 && guard++ < maxIterations)
            {
                int ci = MaxIndex(creditors);
                int di = MaxIndex(debtors);
                var c = creditors[ci];
                var d = debtors[di];

                double pay = Math.Min(c.Amt, d.Amt);
                if (pay > Epsilon)
                {
                    result.Add(new Transfer
                    {
                        From = d.Name,
                        To = c.Name,
                        Amount = Math.Round(pay, 2, MidpointRounding.AwayFromZero),
                    });
                }

                double newC = c.Amt - pay;
                double newD = d.Amt - pay;
                if (newC > Epsilon) creditors[ci] = (c.Name, newC); else creditors.RemoveAt(ci);
                if (newD > Epsilon) debtors[di] = (d.Name, newD); else debtors.RemoveAt(di);
            }
        }
        catch
        {
            // fall through — return whatever we settled so far
        }
        return result;
    }

    private static int MaxIndex(List<(string Name, double Amt)> list)
    {
        int idx = 0;
        double best = double.NegativeInfinity;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].Amt > best) { best = list[i].Amt; idx = i; }
        }
        return idx;
    }

    private static double SanitizeAmount(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0) return 0;
        return v;
    }

    /// <summary>Format a money value with a currency symbol, e.g. "$12.50". Never throws.</summary>
    public static string Money(string symbol, double value)
    {
        try
        {
            symbol ??= "$";
            return $"{symbol}{value:0.00}";
        }
        catch { return $"{symbol}{value}"; }
    }

    /// <summary>Build a plain-text settle-up plan for the clipboard. Never throws.</summary>
    public static string BuildPlanText(SplitResult result, string symbol, Func<string, string, string> pick)
    {
        try
        {
            symbol ??= "$";
            var sb = new StringBuilder();
            sb.AppendLine(pick("Expense Splitter — settle-up plan", "夾錢分帳 — 找數方案"));
            sb.AppendLine(pick($"People: {result.PeopleCount}   Total: {Money(symbol, result.GrandTotal)}   Fair share: {Money(symbol, result.FairShare)}",
                                $"人數：{result.PeopleCount}   總數：{Money(symbol, result.GrandTotal)}   人均：{Money(symbol, result.FairShare)}"));
            sb.AppendLine();

            sb.AppendLine(pick("Balances:", "結餘："));
            foreach (var b in result.Balances)
            {
                string tag = b.Net > Epsilon ? pick("is owed", "應收")
                           : b.Net < -Epsilon ? pick("owes", "應付")
                           : pick("settled", "已平");
                sb.AppendLine($"  {b.Name}: {pick("paid", "已付")} {Money(symbol, b.Paid)}, {tag} {Money(symbol, Math.Abs(b.Net))}");
            }
            sb.AppendLine();

            sb.AppendLine(pick("Transfers:", "轉帳："));
            if (result.Transfers.Count == 0)
            {
                sb.AppendLine(pick("  Everyone is settled — nothing to transfer.", "  大家已經找清，唔使轉帳。"));
            }
            else
            {
                foreach (var t in result.Transfers)
                    sb.AppendLine($"  {pick($"{t.From} pays {t.To} {Money(symbol, t.Amount)}", $"{t.From} 俾 {t.To} {Money(symbol, t.Amount)}")}");
            }

            return sb.ToString();
        }
        catch
        {
            return pick("Unable to build plan.", "無法產生方案。");
        }
    }
}
