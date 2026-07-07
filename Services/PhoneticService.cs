using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 拼讀字母表 · Phonetic-alphabet speller. Pure managed, embedded tables — turns text into
/// spoken code words (NATO/ICAO "Alpha Bravo Charlie", LAPD/police "Adam Boy Charlie", or a
/// plain word-per-letter set). Never throws.
/// </summary>
public static class PhoneticService
{
    /// <summary>The available alphabets, in display order.</summary>
    public enum Alphabet
    {
        Nato,
        Police,
        Simple,
    }

    /// <summary>One spelled-out character: the original char and its code word.</summary>
    public sealed class SpelledChar
    {
        public string Character { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>Full result of a spelling pass.</summary>
    public sealed class SpellResult
    {
        public string Spoken { get; set; } = string.Empty;
        public List<SpelledChar> Chars { get; set; } = new();
    }

    // --- Shared digit words (NATO / ICAO radiotelephony) ---
    private static readonly Dictionary<char, string> Digits = new()
    {
        ['0'] = "Zero",
        ['1'] = "One",
        ['2'] = "Two",
        ['3'] = "Three",
        ['4'] = "Four",
        ['5'] = "Five",
        ['6'] = "Six",
        ['7'] = "Seven",
        ['8'] = "Eight",
        ['9'] = "Niner",
    };

    // NATO / ICAO international radiotelephony spelling alphabet.
    private static readonly Dictionary<char, string> Nato = new()
    {
        ['A'] = "Alpha", ['B'] = "Bravo", ['C'] = "Charlie", ['D'] = "Delta",
        ['E'] = "Echo", ['F'] = "Foxtrot", ['G'] = "Golf", ['H'] = "Hotel",
        ['I'] = "India", ['J'] = "Juliett", ['K'] = "Kilo", ['L'] = "Lima",
        ['M'] = "Mike", ['N'] = "November", ['O'] = "Oscar", ['P'] = "Papa",
        ['Q'] = "Quebec", ['R'] = "Romeo", ['S'] = "Sierra", ['T'] = "Tango",
        ['U'] = "Uniform", ['V'] = "Victor", ['W'] = "Whiskey", ['X'] = "X-ray",
        ['Y'] = "Yankee", ['Z'] = "Zulu",
    };

    // LAPD / US police radio alphabet.
    private static readonly Dictionary<char, string> Police = new()
    {
        ['A'] = "Adam", ['B'] = "Boy", ['C'] = "Charlie", ['D'] = "David",
        ['E'] = "Edward", ['F'] = "Frank", ['G'] = "George", ['H'] = "Henry",
        ['I'] = "Ida", ['J'] = "John", ['K'] = "King", ['L'] = "Lincoln",
        ['M'] = "Mary", ['N'] = "Nora", ['O'] = "Ocean", ['P'] = "Paul",
        ['Q'] = "Queen", ['R'] = "Robert", ['S'] = "Sam", ['T'] = "Tom",
        ['U'] = "Union", ['V'] = "Victor", ['W'] = "William", ['X'] = "X-ray",
        ['Y'] = "Young", ['Z'] = "Zebra",
    };

    // Simple word-per-letter set (common everyday words).
    private static readonly Dictionary<char, string> Simple = new()
    {
        ['A'] = "Apple", ['B'] = "Banana", ['C'] = "Cat", ['D'] = "Dog",
        ['E'] = "Egg", ['F'] = "Fish", ['G'] = "Goat", ['H'] = "House",
        ['I'] = "Ice", ['J'] = "Juice", ['K'] = "Kite", ['L'] = "Lion",
        ['M'] = "Moon", ['N'] = "Nose", ['O'] = "Orange", ['P'] = "Pig",
        ['Q'] = "Queen", ['R'] = "Rabbit", ['S'] = "Sun", ['T'] = "Tree",
        ['U'] = "Umbrella", ['V'] = "Violin", ['W'] = "Water", ['X'] = "Xylophone",
        ['Y'] = "Yellow", ['Z'] = "Zebra",
    };

    private static Dictionary<char, string> LettersFor(Alphabet a) => a switch
    {
        Alphabet.Police => Police,
        Alphabet.Simple => Simple,
        _ => Nato,
    };

    /// <summary>Friendly, localized-ready display name for an alphabet.</summary>
    public static string DisplayName(Alphabet a) => a switch
    {
        Alphabet.Police => "LAPD / Police",
        Alphabet.Simple => "Simple words",
        _ => "NATO / ICAO",
    };

    /// <summary>
    /// Spell <paramref name="input"/> into code words. Letters and digits map to code words;
    /// spaces become "(space)"; when <paramref name="keepPunctuation"/> is true other characters
    /// are echoed verbatim, otherwise they are dropped. <paramref name="upper"/> upper-cases the
    /// original character shown in the per-char list. Never throws.
    /// </summary>
    public static SpellResult Spell(string? input, Alphabet alphabet, bool upper, bool keepPunctuation)
    {
        var result = new SpellResult();
        try
        {
            if (string.IsNullOrEmpty(input)) return result;

            var table = LettersFor(alphabet);
            var spoken = new StringBuilder();

            foreach (char raw in input)
            {
                char shown = upper ? char.ToUpperInvariant(raw) : raw;
                char key = char.ToUpperInvariant(raw);
                string? code = null;

                if (key >= 'A' && key <= 'Z' && table.TryGetValue(key, out var w))
                    code = w;
                else if (key >= '0' && key <= '9' && Digits.TryGetValue(key, out var d))
                    code = d;
                else if (raw == ' ')
                    code = "(space)";
                else if (keepPunctuation)
                    code = raw.ToString();
                else
                    continue; // drop unspellable punctuation

                result.Chars.Add(new SpelledChar { Character = shown.ToString(), Code = code });
                if (spoken.Length > 0) spoken.Append(' ');
                spoken.Append(code);
            }

            result.Spoken = spoken.ToString();
        }
        catch
        {
            // Never throw — return whatever was built so far.
        }
        return result;
    }
}
