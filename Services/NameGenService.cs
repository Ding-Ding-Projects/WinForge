using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 名稱產生器 · Name generator — pure-managed, cryptographically-random name maker.
/// Modes: username, project, company/startup, fantasy, band, slug. All word lists are
/// embedded static arrays; randomness comes only from RandomNumberGenerator. Never throws.
/// </summary>
public static class NameGenService
{
    public enum Kind { Username, Project, Company, Fantasy, Band, Slug }

    private static readonly string[] Adjectives =
    {
        "swift", "brave", "silent", "cosmic", "golden", "crimson", "frozen", "electric",
        "hidden", "ancient", "lucky", "mighty", "quantum", "velvet", "rusty", "noble",
        "wild", "clever", "solar", "lunar", "iron", "shadow", "bright", "gentle",
        "fearless", "restless", "wandering", "hollow", "radiant", "stormy", "amber", "jade"
    };

    private static readonly string[] Nouns =
    {
        "falcon", "otter", "harbor", "ember", "comet", "willow", "raven", "summit",
        "cobalt", "lantern", "meadow", "phoenix", "glacier", "cinder", "maple", "harbour",
        "voyager", "beacon", "thunder", "river", "canyon", "orbit", "tundra", "prairie",
        "badger", "hornet", "walrus", "sparrow", "boulder", "nimbus", "quartz", "onyx"
    };

    // CVC fantasy syllable parts.
    private static readonly string[] Onsets =
    { "b", "d", "f", "g", "k", "l", "m", "n", "r", "s", "t", "v", "th", "sh", "dr", "gr", "br", "vy", " z" };
    private static readonly string[] Vowels =
    { "a", "e", "i", "o", "u", "ae", "ei", "ia", "ou", "yr", "ael" };
    private static readonly string[] Codas =
    { "n", "r", "l", "s", "th", "m", "x", "sk", " n", "" };

    // Company/startup portmanteau fragments.
    private static readonly string[] BlendHeads =
    { "no", "zen", "lumi", "cove", "flux", "nova", "veri", "opti", "hyper", "pana", "aero", "cala", "vibra", "octo", "sona", "meta" };
    private static readonly string[] BlendTails =
    { "ly", "ify", "wave", "sync", "hub", "labs", "flow", "grid", "kit", "loop", "forge", "scape", "mint", "verse", "pilot", "spark" };

    private static readonly string[] CodeNames =
    { "Aurora", "Nimbus", "Vertex", "Pinnacle", "Odyssey", "Zenith", "Mirage", "Catalyst",
      "Horizon", "Quicksilver", "Obsidian", "Tempest", "Lodestar", "Falcon", "Titan", "Everest" };

    /// <summary>Cryptographically-uniform int in [0, n). Falls back to 0 on any error.</summary>
    private static int Rand(int n)
    {
        try { return n <= 1 ? 0 : RandomNumberGenerator.GetInt32(n); }
        catch { return 0; }
    }

    private static string Pick(string[] arr) => arr.Length == 0 ? "" : arr[Rand(arr.Length)].Trim();

    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

    private static string OneUsername()
    {
        var sb = new StringBuilder();
        sb.Append(Cap(Pick(Adjectives))).Append(Cap(Pick(Nouns)));
        if (Rand(3) != 0) sb.Append(Rand(90) + 10); // ~2/3 get a 2-digit suffix
        return sb.ToString();
    }

    private static string OneProject()
    {
        // Codename style: "<CodeName>-<noun>" or "<adjective> <noun>".
        if (Rand(2) == 0)
            return $"{Pick(CodeNames)}-{Cap(Pick(Nouns))}";
        return $"{Cap(Pick(Adjectives))} {Cap(Pick(Nouns))}";
    }

    private static string OneCompany()
    {
        // Portmanteau / blend.
        if (Rand(2) == 0)
            return Cap(Pick(BlendHeads) + Pick(BlendTails));
        // Blend a real word with a tail.
        string root = Pick(Rand(2) == 0 ? Adjectives : Nouns);
        if (root.Length > 4) root = root.Substring(0, 4);
        return Cap(root + Pick(BlendTails));
    }

    private static string OneFantasy()
    {
        int syl = 2 + Rand(2); // 2 or 3 syllables
        var sb = new StringBuilder();
        for (int i = 0; i < syl; i++)
        {
            sb.Append(Pick(Onsets)).Append(Pick(Vowels));
            if (i == syl - 1 || Rand(3) == 0) sb.Append(Pick(Codas));
        }
        return Cap(sb.ToString());
    }

    private static string OneBand()
    {
        // "The <Adjective> <Nouns(plural-ish)>"
        string noun = Pick(Nouns);
        if (!noun.EndsWith("s")) noun += "s";
        return $"The {Cap(Pick(Adjectives))} {Cap(noun)}";
    }

    private static string OneSlug()
    {
        return $"{Pick(Adjectives)}-{Pick(Nouns)}";
    }

    private static string One(Kind kind) => kind switch
    {
        Kind.Username => OneUsername(),
        Kind.Project => OneProject(),
        Kind.Company => OneCompany(),
        Kind.Fantasy => OneFantasy(),
        Kind.Band => OneBand(),
        Kind.Slug => OneSlug(),
        _ => OneUsername()
    };

    /// <summary>Generate <paramref name="count"/> names of the given kind. Clamps count to 1..100. Never throws.</summary>
    public static List<string> Generate(Kind kind, int count)
    {
        var list = new List<string>();
        try
        {
            if (count < 1) count = 1;
            if (count > 100) count = 100;
            for (int i = 0; i < count; i++)
            {
                string name = One(kind);
                if (string.IsNullOrWhiteSpace(name)) name = "name" + (i + 1);
                list.Add(name);
            }
        }
        catch
        {
            if (list.Count == 0) list.Add("name1");
        }
        return list;
    }
}
