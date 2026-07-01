using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 密碼 / 通行短語產生器 · Password & passphrase generator. Pure managed, cryptographically
/// secure: every selection uses <see cref="RandomNumberGenerator.GetInt32(int)"/> (unbiased),
/// never System.Random. No redirect, no shelling out.
/// </summary>
public static class PassGenService
{
    public const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    public const string Uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    public const string Digits = "0123456789";
    public const string Symbols = "!@#$%^&*()-_=+[]{};:,.?/";

    /// <summary>Ambiguous / easily-confused glyphs, stripped when "avoid ambiguous" is on.</summary>
    private const string Ambiguous = "O0Il1|";

    /// <summary>Options controlling password generation.</summary>
    public sealed class PasswordOptions
    {
        public int Length = 16;
        public bool Lower = true;
        public bool Upper = true;
        public bool Digits = true;
        public bool Symbols = true;
        public bool AvoidAmbiguous;
        public bool NoRepeats;
    }

    /// <summary>Options controlling passphrase generation.</summary>
    public sealed class PassphraseOptions
    {
        public int WordCount = 4;
        public string Separator = "-";
        public bool Capitalize;
        public bool AppendDigit;
    }

    /// <summary>~200 short, common English words for passphrases (Diceware-ish, all lowercase).</summary>
    public static readonly string[] Words =
    {
        "able","acid","aged","also","area","army","away","baby","back","ball","band","bank",
        "base","bath","bear","beat","been","beer","bell","belt","best","bird","blue","boat",
        "body","bone","book","born","both","bowl","bulk","burn","bush","busy","cake","call",
        "calm","came","camp","card","care","case","cash","cast","cell","chat","chip","city",
        "clay","club","coal","coat","code","cold","come","cook","cool","cope","copy","core",
        "corn","cost","crew","crop","dark","data","date","dawn","days","dead","deal","dean",
        "dear","debt","deep","deer","desk","dial","dice","diet","disk","does","done","door",
        "dose","down","draw","drew","drop","drug","dual","duke","dust","duty","each","earn",
        "ease","east","easy","edge","else","even","ever","face","fact","fade","fail","fair",
        "fall","farm","fast","fate","fear","feed","feel","feet","fell","file","fill","film",
        "find","fine","fire","firm","fish","five","flag","flat","flow","fold","folk","food",
        "foot","ford","form","fort","four","free","from","fuel","full","fund","gain","game",
        "gate","gave","gear","gift","girl","give","glad","goal","goat","gold","golf","gone",
        "good","gray","grew","grow","gulf","hair","half","hall","hand","hang","hard","harm",
        "hate","have","head","heal","hear","heat","held","hell","help","herb","here","hero",
        "hide","high","hill","hint","hire","hold","hole","holy","home","hope","horn","host",
        "hour","huge","hunt","hurt","idea","inch","into","iron","item","jazz","join","jump",
        "jury","just","keen","keep","kick","kind","king","knee","knew","know","lace","lack",
        "lake","lamp","land","lane","last","late","lawn","lazy","lead","leaf","lean","leap",
        "left","lend","less","life","lift","like","lily","limb","line","link","lion","list",
        "live","load","loan","lock","logo","lone","long","look","loop","lord","lose","loss"
    };

    public static int DictionarySize => Words.Length;

    /// <summary>Build the character pool for the given password options (may be empty).</summary>
    public static string BuildPool(PasswordOptions o)
    {
        var sb = new StringBuilder();
        if (o.Lower) sb.Append(Lowercase);
        if (o.Upper) sb.Append(Uppercase);
        if (o.Digits) sb.Append(Digits);
        if (o.Symbols) sb.Append(Symbols);
        string pool = sb.ToString();
        if (o.AvoidAmbiguous)
        {
            var filtered = new StringBuilder(pool.Length);
            foreach (char c in pool)
                if (Ambiguous.IndexOf(c) < 0) filtered.Append(c);
            pool = filtered.ToString();
        }
        return pool;
    }

    private static string Filter(string set, bool avoidAmbiguous)
    {
        if (!avoidAmbiguous) return set;
        var sb = new StringBuilder(set.Length);
        foreach (char c in set)
            if (Ambiguous.IndexOf(c) < 0) sb.Append(c);
        return sb.ToString();
    }

    /// <summary>One cryptographically-random character from <paramref name="set"/>.</summary>
    private static char Pick(string set) => set[RandomNumberGenerator.GetInt32(set.Length)];

    /// <summary>Fisher–Yates shuffle using the CSPRNG.</summary>
    private static void Shuffle(IList<char> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Generate one password. Guarantees at least one character from each selected set.
    /// Throws <see cref="InvalidOperationException"/> if the request is impossible (no sets,
    /// or "no repeats" with a length larger than the available pool / set count).
    /// </summary>
    public static string GeneratePassword(PasswordOptions o)
    {
        var required = new List<string>();
        if (o.Lower) required.Add(Filter(Lowercase, o.AvoidAmbiguous));
        if (o.Upper) required.Add(Filter(Uppercase, o.AvoidAmbiguous));
        if (o.Digits) required.Add(Filter(Digits, o.AvoidAmbiguous));
        if (o.Symbols) required.Add(Filter(Symbols, o.AvoidAmbiguous));

        if (required.Count == 0)
            throw new InvalidOperationException("No character sets selected.");

        string pool = BuildPool(o);
        if (pool.Length == 0)
            throw new InvalidOperationException("Character pool is empty.");
        if (o.Length < required.Count)
            throw new InvalidOperationException("Length is too short to include every selected set.");
        if (o.NoRepeats && o.Length > pool.Length)
            throw new InvalidOperationException("No-repeats needs a longer pool than the requested length.");

        var chars = new List<char>(o.Length);
        var used = new HashSet<char>();

        // One from each required set first.
        foreach (var set in required)
        {
            char c;
            int guard = 0;
            do { c = Pick(set); }
            while (o.NoRepeats && !used.Add(c) && ++guard < 10_000);
            if (!o.NoRepeats) used.Add(c);
            chars.Add(c);
        }

        // Fill the rest from the whole pool.
        while (chars.Count < o.Length)
        {
            char c = Pick(pool);
            if (o.NoRepeats && !used.Add(c)) continue;
            chars.Add(c);
        }

        Shuffle(chars);
        return new string(chars.ToArray());
    }

    /// <summary>Generate one passphrase from the embedded wordlist.</summary>
    public static string GeneratePassphrase(PassphraseOptions o)
    {
        if (o.WordCount < 1)
            throw new InvalidOperationException("Word count must be at least 1.");

        var picked = new List<string>(o.WordCount);
        for (int i = 0; i < o.WordCount; i++)
        {
            string w = Words[RandomNumberGenerator.GetInt32(Words.Length)];
            if (o.Capitalize) w = char.ToUpperInvariant(w[0]) + w.Substring(1);
            picked.Add(w);
        }

        string phrase = string.Join(o.Separator, picked);
        if (o.AppendDigit) phrase += RandomNumberGenerator.GetInt32(10).ToString();
        return phrase;
    }

    /// <summary>Estimated entropy in bits: length · log2(poolSize).</summary>
    public static double PasswordEntropyBits(int length, int poolSize)
        => poolSize <= 1 || length <= 0 ? 0 : length * Math.Log2(poolSize);

    /// <summary>Estimated entropy in bits: words · log2(dictSize) (+ ~3.3 bits for an appended digit).</summary>
    public static double PassphraseEntropyBits(int words, int dictSize, bool appendDigit)
    {
        if (dictSize <= 1 || words <= 0) return 0;
        double bits = words * Math.Log2(dictSize);
        if (appendDigit) bits += Math.Log2(10);
        return bits;
    }
}
