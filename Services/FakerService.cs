using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 假資料產生器 · Lorem ipsum &amp; fake-data generator. Pure managed C#; all randomness comes from
/// <see cref="RandomNumberGenerator"/> (unbiased GetInt32). No I/O, no redirect, never throws.
/// </summary>
public static class FakerService
{
    // Classic lorem ipsum word bank.
    private static readonly string[] Lorem =
    {
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do",
        "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua", "enim",
        "ad", "minim", "veniam", "quis", "nostrud", "exercitation", "ullamco", "laboris", "nisi",
        "aliquip", "ex", "ea", "commodo", "consequat", "duis", "aute", "irure", "in", "reprehenderit",
        "voluptate", "velit", "esse", "cillum", "eu", "fugiat", "nulla", "pariatur", "excepteur", "sint",
        "occaecat", "cupidatat", "non", "proident", "sunt", "culpa", "qui", "officia", "deserunt",
        "mollit", "anim", "id", "est", "laborum", "at", "vero", "eos", "accusamus", "iusto", "odio",
        "dignissimos", "ducimus", "blanditiis", "praesentium", "voluptatum", "deleniti", "atque",
        "corrupti", "quos", "dolores", "quas", "molestias", "excepturi", "similique", "mollitia"
    };

    private static readonly string[] FirstNames =
    {
        "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William",
        "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah",
        "Charles", "Karen", "Christopher", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony",
        "Sandra", "Mark", "Ashley", "Donald", "Emily", "Steven", "Kimberly", "Paul", "Donna"
    };

    private static readonly string[] LastNames =
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez",
        "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore",
        "Jackson", "Martin", "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark",
        "Ramirez", "Lewis", "Robinson", "Walker", "Young", "Allen", "King", "Wright", "Scott"
    };

    private static readonly string[] Cities =
    {
        "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio",
        "San Diego", "Dallas", "San Jose", "Austin", "Seattle", "Denver", "Boston", "Portland",
        "London", "Toronto", "Sydney", "Singapore", "Dublin", "Auckland", "Vancouver", "Hong Kong"
    };

    private static readonly string[] Companies =
    {
        "Acme", "Globex", "Initech", "Umbrella", "Soylent", "Hooli", "Vandelay", "Wayne", "Stark",
        "Wonka", "Cyberdyne", "Tyrell", "Aperture", "Massive", "Pied Piper", "Nakatomi", "Gekko"
    };

    private static readonly string[] CompanySuffix =
    {
        "Inc", "LLC", "Corp", "Group", "Holdings", "Systems", "Labs", "Industries", "Partners", "Co"
    };

    private static readonly string[] StreetNames =
    {
        "Main", "Oak", "Pine", "Maple", "Cedar", "Elm", "Washington", "Lake", "Hill", "Park",
        "Sunset", "Church", "River", "Spring", "Highland", "Franklin", "Union", "Broadway", "Market"
    };

    private static readonly string[] StreetTypes =
    {
        "St", "Ave", "Blvd", "Rd", "Ln", "Dr", "Way", "Ct", "Pl"
    };

    /// <summary>Field kinds available for the fake-data generator.</summary>
    public enum Field
    {
        FullName, Email, Username, Phone, StreetAddress, City, Company, Uuid, Date, Integer, Boolean, IPv4, HexColor
    }

    // ---- unbiased randomness helpers -------------------------------------------------
    private static int Rand(int maxExclusive) =>
        maxExclusive <= 1 ? 0 : RandomNumberGenerator.GetInt32(maxExclusive);

    private static int Rand(int minInclusive, int maxExclusive) =>
        maxExclusive <= minInclusive ? minInclusive : RandomNumberGenerator.GetInt32(minInclusive, maxExclusive);

    private static string Pick(string[] arr) => arr.Length == 0 ? string.Empty : arr[Rand(arr.Length)];

    // ---- lorem ipsum -----------------------------------------------------------------

    /// <summary>Lorem generation modes.</summary>
    public enum LoremMode { Paragraphs, Sentences, Words }

    /// <summary>Generate lorem text. <paramref name="count"/> is clamped to a sane range.</summary>
    public static string Lorem_(LoremMode mode, int count)
    {
        try
        {
            count = Math.Clamp(count, 1, 500);
            return mode switch
            {
                LoremMode.Words => WordsText(count),
                LoremMode.Sentences => SentencesText(count),
                _ => ParagraphsText(count)
            };
        }
        catch { return string.Empty; }
    }

    private static string Word() => Pick(Lorem);

    private static string Sentence()
    {
        int words = Rand(6, 15);
        var sb = new StringBuilder();
        for (int i = 0; i < words; i++)
        {
            string w = Word();
            if (i == 0) w = char.ToUpperInvariant(w[0]) + w[1..];
            sb.Append(w);
            if (i < words - 1) sb.Append(' ');
        }
        sb.Append('.');
        return sb.ToString();
    }

    private static string WordsText(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append(Word());
            if (i < count - 1) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static string SentencesText(int count)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append(Sentence());
            if (i < count - 1) sb.Append(' ');
        }
        return sb.ToString();
    }

    private static string ParagraphsText(int count)
    {
        var sb = new StringBuilder();
        for (int p = 0; p < count; p++)
        {
            int sentences = Rand(3, 7);
            for (int i = 0; i < sentences; i++)
            {
                sb.Append(Sentence());
                if (i < sentences - 1) sb.Append(' ');
            }
            if (p < count - 1) sb.Append("\r\n\r\n");
        }
        return sb.ToString();
    }

    // ---- fake data -------------------------------------------------------------------

    /// <summary>Generate <paramref name="count"/> values (1..500) of a field, one per line.</summary>
    public static string Generate(Field field, int count)
    {
        try
        {
            count = Math.Clamp(count, 1, 500);
            var lines = new List<string>(count);
            for (int i = 0; i < count; i++) lines.Add(One(field));
            return string.Join("\r\n", lines);
        }
        catch { return string.Empty; }
    }

    private static string One(Field field)
    {
        switch (field)
        {
            case Field.FullName:
                return $"{Pick(FirstNames)} {Pick(LastNames)}";
            case Field.Email:
            {
                string f = Pick(FirstNames).ToLowerInvariant();
                string l = Pick(LastNames).ToLowerInvariant();
                string[] hosts = { "example.com", "mail.com", "test.org", "demo.net", "inbox.io" };
                return $"{f}.{l}{Rand(1, 99)}@{Pick(hosts)}";
            }
            case Field.Username:
            {
                string f = Pick(FirstNames).ToLowerInvariant();
                string l = Pick(LastNames).ToLowerInvariant();
                return $"{f}_{l}{Rand(10, 9999)}";
            }
            case Field.Phone:
                return $"({Rand(200, 1000):000}) {Rand(200, 1000):000}-{Rand(0, 10000):0000}";
            case Field.StreetAddress:
                return $"{Rand(1, 9999)} {Pick(StreetNames)} {Pick(StreetTypes)}";
            case Field.City:
                return Pick(Cities);
            case Field.Company:
                return $"{Pick(Companies)} {Pick(CompanySuffix)}";
            case Field.Uuid:
                return Guid.NewGuid().ToString();
            case Field.Date:
            {
                var start = new DateTime(1970, 1, 1);
                int days = Rand(0, 20454); // ~ up to 2026
                return start.AddDays(days).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            case Field.Integer:
                return Rand(0, 1000000).ToString(CultureInfo.InvariantCulture);
            case Field.Boolean:
                return Rand(0, 2) == 0 ? "false" : "true";
            case Field.IPv4:
                return $"{Rand(1, 256)}.{Rand(0, 256)}.{Rand(0, 256)}.{Rand(1, 255)}";
            case Field.HexColor:
                return $"#{Rand(0, 256):X2}{Rand(0, 256):X2}{Rand(0, 256):X2}";
            default:
                return string.Empty;
        }
    }
}
