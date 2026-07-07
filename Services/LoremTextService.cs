using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 假文產生器 · Lorem Ipsum / placeholder-text generator. Pure managed C#; uses
/// <see cref="RandomNumberGenerator.GetInt32(int)"/> for word/sentence shaping.
/// Never throws — every public entry point is defensively clamped and try-guarded.
/// </summary>
public static class LoremTextService
{
    /// <summary>What each count means.</summary>
    public enum Unit { Paragraphs, Sentences, Words, ListItems }

    /// <summary>Which embedded word pool to draw from.</summary>
    public enum Pool { ClassicLatin, HipsterTech }

    // The classic Latin lorem-ipsum pool.
    private static readonly string[] Latin =
    {
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
        "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
        "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud",
        "exercitation", "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo",
        "consequat", "duis", "aute", "irure", "in", "reprehenderit", "voluptate",
        "velit", "esse", "cillum", "eu", "fugiat", "nulla", "pariatur", "excepteur",
        "sint", "occaecat", "cupidatat", "non", "proident", "sunt", "culpa", "qui",
        "officia", "deserunt", "mollit", "anim", "id", "est", "laborum", "at", "vero",
        "eos", "accusamus", "iusto", "odio", "dignissimos", "ducimus", "blanditiis",
        "praesentium", "voluptatum", "deleniti", "atque", "corrupti", "quos", "dolores",
        "quas", "molestias", "excepturi", "sint", "obcaecati", "cupiditate", "similique",
        "mollitia", "animi", "dolorum", "fuga", "harum", "quidem", "rerum", "facilis",
        "expedita", "distinctio", "nam", "libero", "tempore", "soluta", "nobis",
        "eligendi", "optio", "cumque", "nihil", "impedit", "quo", "porro", "quisquam"
    };

    // A small alternative "hipster / tech" pool for playful placeholder copy.
    private static readonly string[] HipsterTech =
    {
        "artisan", "sync", "scalable", "serverless", "kubernetes", "microservice",
        "cloud", "native", "container", "pipeline", "devops", "agile", "sprint",
        "backlog", "webhook", "endpoint", "latency", "throughput", "cache", "shard",
        "kafka", "stream", "event", "driven", "reactive", "async", "await", "promise",
        "token", "oauth", "schema", "graphql", "rest", "grpc", "proto", "binary",
        "artisanal", "roast", "pour-over", "kombucha", "matcha", "hoodie", "beanie",
        "vinyl", "analog", "retro", "bespoke", "curated", "handcrafted", "small-batch",
        "disrupt", "pivot", "runway", "burn", "unicorn", "moat", "flywheel", "synergy",
        "leverage", "bandwidth", "granular", "holistic", "paradigm", "ideate",
        "wireframe", "prototype", "mvp", "ship", "iterate", "refactor", "deploy",
        "observability", "telemetry", "metrics", "traces", "logs", "dashboard",
        "edge", "mesh", "sidecar", "yaml", "config", "immutable", "idempotent",
        "distributed", "consensus", "quorum", "gossip", "eventual", "consistency"
    };

    private const string ClassicOpener =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit";

    /// <summary>Options for a single generation pass. All fields have safe defaults.</summary>
    public sealed class Options
    {
        public Unit Unit = Unit.Paragraphs;
        public int Count = 5;
        public bool StartWithClassic = true;
        public int MinSentencesPerParagraph = 3;
        public int MaxSentencesPerParagraph = 7;
        public bool HtmlWrap;
        public Pool Pool = Pool.ClassicLatin;
    }

    /// <summary>Result of a generation — the text plus quick word / character counts.</summary>
    public sealed class Result
    {
        public string Text = string.Empty;
        public int Words;
        public int Characters;
    }

    /// <summary>Generate placeholder text. Never throws — bad input yields an empty-ish result.</summary>
    public static Result Generate(Options? opts)
    {
        try
        {
            opts ??= new Options();
            int count = Clamp(opts.Count, 1, 5000);
            string[] pool = opts.Pool == Pool.HipsterTech ? HipsterTech : Latin;
            if (pool.Length == 0) pool = Latin;

            int minS = Clamp(opts.MinSentencesPerParagraph, 1, 40);
            int maxS = Clamp(opts.MaxSentencesPerParagraph, minS, 40);

            string body = opts.Unit switch
            {
                Unit.Words => GenWords(count, pool, opts.StartWithClassic),
                Unit.Sentences => GenSentences(count, pool, opts.StartWithClassic),
                Unit.ListItems => GenList(count, pool, minS, maxS, opts.StartWithClassic, opts.HtmlWrap),
                _ => GenParagraphs(count, pool, minS, maxS, opts.StartWithClassic, opts.HtmlWrap),
            };

            body ??= string.Empty;
            return new Result
            {
                Text = body,
                Words = CountWords(body),
                Characters = body.Length,
            };
        }
        catch
        {
            return new Result { Text = string.Empty, Words = 0, Characters = 0 };
        }
    }

    // ---- generators ----------------------------------------------------------

    private static string GenWords(int n, string[] pool, bool classic)
    {
        var sb = new StringBuilder();
        var seed = classic ? new[] { "lorem", "ipsum", "dolor", "sit", "amet" } : null;
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            if (seed != null && i < seed.Length) sb.Append(seed[i]);
            else sb.Append(Word(pool));
        }
        // Capitalise the very first word for a tidy look.
        return Capitalise(sb.ToString());
    }

    private static string GenSentences(int n, string[] pool, bool classic)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(Sentence(pool, i == 0 && classic));
        }
        return sb.ToString();
    }

    private static string GenParagraphs(int n, string[] pool, int minS, int maxS, bool classic, bool html)
    {
        var sb = new StringBuilder();
        for (int p = 0; p < n; p++)
        {
            int sentences = Between(minS, maxS);
            var para = new StringBuilder();
            for (int s = 0; s < sentences; s++)
            {
                if (s > 0) para.Append(' ');
                para.Append(Sentence(pool, p == 0 && s == 0 && classic));
            }
            string line = html ? "<p>" + para + "</p>" : para.ToString();
            if (p > 0) sb.Append("\n\n");
            sb.Append(line);
        }
        return sb.ToString();
    }

    private static string GenList(int n, string[] pool, int minS, int maxS, bool classic, bool html)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < n; i++)
        {
            // One short sentence per list item.
            string item = Sentence(pool, i == 0 && classic);
            string line = html ? "<li>" + item + "</li>" : "• " + item;
            if (i > 0) sb.Append('\n');
            sb.Append(line);
        }
        return sb.ToString();
    }

    // ---- primitives ----------------------------------------------------------

    private static string Sentence(string[] pool, bool classicOpener)
    {
        int len = Between(6, 16); // words per sentence
        var sb = new StringBuilder();
        if (classicOpener)
        {
            sb.Append(ClassicOpener);
            int extra = Between(2, 8);
            for (int i = 0; i < extra; i++) sb.Append(' ').Append(Word(pool));
        }
        else
        {
            for (int i = 0; i < len; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Word(pool));
            }
        }
        string s = Capitalise(sb.ToString());
        // Occasional comma for rhythm.
        if (Between(0, 3) == 0)
        {
            int mid = s.Length / 2;
            int sp = s.IndexOf(' ', mid);
            if (sp > 0 && sp < s.Length - 1) s = s.Insert(sp, ",");
        }
        return s + ".";
    }

    private static string Word(string[] pool) => pool[Next(pool.Length)];

    private static string Capitalise(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + (s.Length > 1 ? s.Substring(1) : string.Empty);
    }

    private static int CountWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        int words = 0;
        bool inWord = false;
        foreach (char c in s)
        {
            bool ws = char.IsWhiteSpace(c) || c == '<' || c == '>' || c == '•';
            if (!ws && !inWord) { words++; inWord = true; }
            else if (ws) inWord = false;
        }
        return words;
    }

    // ---- randomness (never-throw wrappers) ----------------------------------

    private static int Next(int exclusiveMax)
    {
        if (exclusiveMax <= 0) return 0;
        try { return RandomNumberGenerator.GetInt32(exclusiveMax); }
        catch { return 0; }
    }

    private static int Between(int minInclusive, int maxInclusive)
    {
        if (maxInclusive < minInclusive) maxInclusive = minInclusive;
        return minInclusive + Next(maxInclusive - minInclusive + 1);
    }

    private static int Clamp(int v, int lo, int hi)
    {
        if (double.IsNaN(v)) return lo;
        if (v < lo) return lo;
        if (v > hi) return hi;
        return v;
    }

    private static int Clamp(double v, int lo, int hi)
    {
        if (double.IsNaN(v)) return lo;
        return Clamp((int)Math.Round(v), lo, hi);
    }
}
