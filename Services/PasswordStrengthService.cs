using System;
using System.Collections.Generic;
using System.Linq;

namespace WinForge.Services;

/// <summary>
/// 密碼強度分析 · Password-strength analyzer. Pure managed, fully local — the password is analysed in
/// memory only and is never stored, logged or transmitted. Estimates character-set pool size, Shannon-style
/// length×log2(pool) entropy, a strength band, crack-time at several guess rates, a rule checklist and a small
/// embedded common-password blocklist. Never throws.
/// </summary>
public static class PasswordStrengthService
{
    public sealed class Result
    {
        public int Length;
        public int PoolSize;
        public double EntropyBits;
        public int Band;                 // 0..4  (Very weak → Very strong)
        public double Fraction;          // 0..1  for a ProgressBar
        public bool HasLower, HasUpper, HasDigit, HasSymbol, HasSpace;
        public bool NoRepeats, NoSequences;
        public bool Len8, Len12, Len16;
        public bool IsCommon;
        public double OnlineSeconds;     // 1e4 guesses/s
        public double OfflineGpuSeconds; // 1e10 guesses/s
        public double FastSeconds;       // 1e12 guesses/s
    }

    // ~100 of the most common leaked passwords (lower-cased). Kept small & embedded — no network.
    private static readonly HashSet<string> Common = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456","password","123456789","12345678","12345","1234567","qwerty","abc123","111111","123123",
        "1234567890","1234","000000","password1","qwerty123","1q2w3e4r","admin","letmein","welcome","monkey",
        "dragon","football","iloveyou","sunshine","princess","aa123456","654321","superman","666666","987654321",
        "qwertyuiop","121212","zaq12wsx","passw0rd","trustno1","master","hello","freedom","whatever","qazwsx",
        "michael","batman","shadow","baseball","soccer","hockey","killer","charlie","jordan","harley",
        "andrew","tigger","robert","daniel","hannah","jessica","thomas","summer","ashley","jennifer",
        "starwars","computer","secret","internet","service","canada","hunter","buster","soccer1","liverpool",
        "test","test123","guest","root","admin123","login","changeme","password123","p@ssw0rd","qwe123",
        "1qaz2wsx","asdfgh","asdfghjkl","zxcvbnm","q1w2e3r4","abcd1234","a1b2c3d4","987654","112233","696969",
        "555555","777777","888888","999999","google","facebook","chocolate","cheese","ninja","pokemon",
    };

    private static readonly string[] Sequences =
    {
        "abcdefghijklmnopqrstuvwxyz","zyxwvutsrqponmlkjihgfedcba",
        "0123456789","9876543210",
        "qwertyuiop","asdfghjkl","zxcvbnm","qwerty","qazwsx","1q2w3e4r",
    };

    /// <summary>Analyse a password. Returns a filled <see cref="Result"/>; never throws.</summary>
    public static Result Analyze(string? password)
    {
        var r = new Result();
        try
        {
            string pw = password ?? string.Empty;
            r.Length = pw.Length;
            if (pw.Length == 0) return r;

            foreach (char c in pw)
            {
                if (c >= 'a' && c <= 'z') r.HasLower = true;
                else if (c >= 'A' && c <= 'Z') r.HasUpper = true;
                else if (c >= '0' && c <= '9') r.HasDigit = true;
                else if (c == ' ') r.HasSpace = true;
                else r.HasSymbol = true;
            }

            int pool = 0;
            if (r.HasLower) pool += 26;
            if (r.HasUpper) pool += 26;
            if (r.HasDigit) pool += 10;
            if (r.HasSymbol) pool += 33; // printable ASCII punctuation
            if (r.HasSpace) pool += 1;
            if (pool <= 0) pool = 1;
            r.PoolSize = pool;

            r.EntropyBits = pw.Length * Math.Log2(pool);

            r.Len8 = pw.Length >= 8;
            r.Len12 = pw.Length >= 12;
            r.Len16 = pw.Length >= 16;
            r.NoRepeats = !HasRun(pw, 3);
            r.NoSequences = !HasSequence(pw);
            r.IsCommon = Common.Contains(pw.Trim());

            // Effective entropy for crack-time: penalise known-bad passwords heavily.
            double effective = r.EntropyBits;
            if (r.IsCommon) effective = Math.Min(effective, 8);
            else
            {
                if (!r.NoSequences) effective -= 8;
                if (!r.NoRepeats) effective -= 6;
                if (effective < 0) effective = 0;
            }

            // Average guesses ≈ half the keyspace (2^bits / 2 = 2^(bits-1)).
            double guesses = Math.Pow(2, Math.Max(0, effective - 1));
            r.OnlineSeconds = guesses / 1e4;
            r.OfflineGpuSeconds = guesses / 1e10;
            r.FastSeconds = guesses / 1e12;

            // Band from effective entropy (with a hard cap for common passwords).
            double e = r.IsCommon ? 0 : effective;
            r.Band = e < 28 ? 0 : e < 40 ? 1 : e < 60 ? 2 : e < 80 ? 3 : 4;
            r.Fraction = Math.Clamp(e / 100.0, 0.02, 1.0);
        }
        catch
        {
            // Never throw — return whatever we managed to fill.
        }
        return r;
    }

    private static bool HasRun(string pw, int n)
    {
        int run = 1;
        for (int i = 1; i < pw.Length; i++)
        {
            run = pw[i] == pw[i - 1] ? run + 1 : 1;
            if (run >= n) return true;
        }
        return false;
    }

    private static bool HasSequence(string pw)
    {
        if (pw.Length < 3) return false;
        string low = pw.ToLowerInvariant();
        foreach (var seq in Sequences)
            for (int i = 0; i + 3 <= seq.Length; i++)
                if (low.Contains(seq.Substring(i, 3)))
                    return true;
        return false;
    }

    /// <summary>Human-readable duration (localised) for a number of seconds. Never throws.</summary>
    public static string HumanTime(double seconds, Func<string, string, string> pick)
    {
        try
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                seconds = 0;

            if (seconds < 1) return pick("instantly", "即刻破解");

            double[] cuts = { 60, 3600, 86400, 2592000, 31536000, 3153600000, 3.1536e11 };
            if (seconds < cuts[0]) return Fmt(seconds, pick("second", "秒"), pick("seconds", "秒"), pick);
            if (seconds < cuts[1]) return Fmt(seconds / 60, pick("minute", "分鐘"), pick("minutes", "分鐘"), pick);
            if (seconds < cuts[2]) return Fmt(seconds / 3600, pick("hour", "小時"), pick("hours", "小時"), pick);
            if (seconds < cuts[3]) return Fmt(seconds / 86400, pick("day", "日"), pick("days", "日"), pick);
            if (seconds < cuts[4]) return Fmt(seconds / 2592000, pick("month", "個月"), pick("months", "個月"), pick);
            if (seconds < cuts[5]) return Fmt(seconds / 31536000, pick("year", "年"), pick("years", "年"), pick);
            if (seconds < cuts[6]) return Fmt(seconds / 3153600000, pick("century", "個世紀"), pick("centuries", "個世紀"), pick);

            double eons = seconds / 3.15576e16;
            if (eons < 1000) return Fmt(seconds / 31536000, pick("year", "年"), pick("years", "年"), pick);
            return pick("effectively forever", "近乎永遠");
        }
        catch
        {
            return pick("—", "—");
        }
    }

    private static string Fmt(double value, string one, string many, Func<string, string, string> pick)
    {
        long n = (long)Math.Round(value);
        if (n < 1) n = 1;
        // English pluralises ("1 second" vs "5 seconds"); Chinese does not, so 'many' == 'one' there.
        return pick($"{n:N0} {(n == 1 ? one : many)}", $"{n:N0} {many}");
    }
}
