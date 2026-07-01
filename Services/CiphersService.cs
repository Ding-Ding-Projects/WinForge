using System;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 經典密碼 · Classic ciphers — pure managed transforms (ROT13, Caesar, Atbash, Vigenère,
/// A1Z26, Morse). Letters transform, other characters pass through (except Morse). Never throws:
/// callers get a friendly error via <see cref="CipherResult"/> instead of an exception.
/// </summary>
public static class CiphersService
{
    public enum Mode { Rot13, Caesar, Atbash, Vigenere, A1Z26, Morse }

    /// <summary>Outcome of a transform. <see cref="Ok"/> false ⇒ show <see cref="ErrorEn"/>/<see cref="ErrorZh"/>.</summary>
    public readonly record struct CipherResult(bool Ok, string Text, string ErrorEn, string ErrorZh)
    {
        public static CipherResult Good(string text) => new(true, text, "", "");
        public static CipherResult Fail(string en, string zh) => new(false, "", en, zh);
    }

    /// <summary>Transform <paramref name="input"/>. <paramref name="encode"/> selects direction where it matters.</summary>
    public static CipherResult Transform(Mode mode, string? input, bool encode, int shift, string? key)
    {
        try
        {
            input ??= string.Empty;
            return mode switch
            {
                Mode.Rot13    => CipherResult.Good(Caesar(input, 13)),           // self-inverse
                Mode.Caesar   => CipherResult.Good(Caesar(input, encode ? shift : -shift)),
                Mode.Atbash   => CipherResult.Good(Atbash(input)),               // self-inverse
                Mode.Vigenere => Vigenere(input, key, encode),
                Mode.A1Z26    => A1Z26(input, encode),
                Mode.Morse    => Morse(input, encode),
                _             => CipherResult.Good(input),
            };
        }
        catch (Exception ex)
        {
            return CipherResult.Fail("Could not process input: " + ex.Message, "無法處理輸入：" + ex.Message);
        }
    }

    private static char ShiftLetter(char c, int shift)
    {
        int m = ((shift % 26) + 26) % 26;
        if (c is >= 'a' and <= 'z') return (char)('a' + (c - 'a' + m) % 26);
        if (c is >= 'A' and <= 'Z') return (char)('A' + (c - 'A' + m) % 26);
        return c;
    }

    private static string Caesar(string s, int shift)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(ShiftLetter(c, shift));
        return sb.ToString();
    }

    private static string Atbash(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            if (c is >= 'a' and <= 'z') sb.Append((char)('z' - (c - 'a')));
            else if (c is >= 'A' and <= 'Z') sb.Append((char)('Z' - (c - 'A')));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    private static CipherResult Vigenere(string s, string? key, bool encode)
    {
        // Build a clean alphabetic key; blank/keyless input is a user error, not a crash.
        var kb = new StringBuilder();
        foreach (char c in key ?? string.Empty)
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') kb.Append(char.ToLowerInvariant(c));
        if (kb.Length == 0)
            return CipherResult.Fail("Enter a key made of letters (A–Z) for Vigenère.", "維吉尼亞密碼需要一個由英文字母（A–Z）組成嘅密鑰。");

        string k = kb.ToString();
        var sb = new StringBuilder(s.Length);
        int ki = 0;
        foreach (char c in s)
        {
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
            {
                int shift = k[ki % k.Length] - 'a';
                sb.Append(ShiftLetter(c, encode ? shift : -shift));
                ki++;
            }
            else sb.Append(c);
        }
        return CipherResult.Good(sb.ToString());
    }

    private static CipherResult A1Z26(string s, bool encode)
    {
        if (encode)
        {
            var sb = new StringBuilder();
            bool prevNum = false;
            foreach (char c in s)
            {
                if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z')
                {
                    if (prevNum) sb.Append('-');
                    sb.Append(char.ToLowerInvariant(c) - 'a' + 1);
                    prevNum = true;
                }
                else { sb.Append(c); prevNum = false; }
            }
            return CipherResult.Good(sb.ToString());
        }

        // Decode: numbers 1–26 → letters; runs of digits split on any non-digit separator.
        var outSb = new StringBuilder();
        var numSb = new StringBuilder();
        foreach (char c in s + "\0")
        {
            if (c is >= '0' and <= '9') { numSb.Append(c); continue; }
            if (numSb.Length > 0)
            {
                if (int.TryParse(numSb.ToString(), out int n) && n is >= 1 and <= 26)
                    outSb.Append((char)('a' + n - 1));
                else
                    return CipherResult.Fail($"'{numSb}' is not a number from 1 to 26.", $"「{numSb}」唔係 1 到 26 之間嘅數字。");
                numSb.Clear();
            }
            // '-' between numbers is a separator; keep spaces and other chars, drop the sentinel.
            if (c == '\0') break;
            if (c != '-') outSb.Append(c);
        }
        return CipherResult.Good(outSb.ToString());
    }

    // --- Morse ---
    private static readonly (char C, string M)[] MorseTable =
    {
        ('a',".-"), ('b',"-..."), ('c',"-.-."), ('d',"-.."), ('e',"."), ('f',"..-."),
        ('g',"--."), ('h',"...."), ('i',".."), ('j',".---"), ('k',"-.-"), ('l',".-.."),
        ('m',"--"), ('n',"-."), ('o',"---"), ('p',".--."), ('q',"--.-"), ('r',".-."),
        ('s',"..."), ('t',"-"), ('u',"..-"), ('v',"...-"), ('w',".--"), ('x',"-..-"),
        ('y',"-.--"), ('z',"--.."),
        ('0',"-----"), ('1',".----"), ('2',"..---"), ('3',"...--"), ('4',"....-"),
        ('5',"....."), ('6',"-...."), ('7',"--..."), ('8',"---.."), ('9',"----."),
        ('.',".-.-.-"), (',',"--..--"), ('?',"..--.."), ('\'',".----."), ('!',"-.-.--"),
        ('/',"-..-."), ('(',"-.--."), (')',"-.--.-"), ('&',".-..."), (':',"---..."),
        (';',"-.-.-."), ('=',"-...-"), ('+',".-.-."), ('-',"-....-"), ('_',"..--.-"),
        ('"',".-..-."), ('@',".--.-."),
    };

    private static CipherResult Morse(string s, bool encode)
    {
        if (encode)
        {
            var words = s.Split(' ');
            var wordOut = new StringBuilder();
            for (int w = 0; w < words.Length; w++)
            {
                if (w > 0) wordOut.Append(" / ");
                var letters = new StringBuilder();
                foreach (char c in words[w])
                {
                    string? code = LookupMorse(char.ToLowerInvariant(c));
                    if (code == null) continue; // unknown chars are dropped in Morse
                    if (letters.Length > 0) letters.Append(' ');
                    letters.Append(code);
                }
                wordOut.Append(letters);
            }
            return CipherResult.Good(wordOut.ToString().Trim());
        }

        // Decode: '/' separates words, spaces separate letters.
        var sb = new StringBuilder();
        var rawWords = s.Trim().Split('/');
        for (int w = 0; w < rawWords.Length; w++)
        {
            if (w > 0) sb.Append(' ');
            foreach (var tok in rawWords[w].Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                char? ch = LookupChar(tok);
                if (ch == null)
                    return CipherResult.Fail($"'{tok}' is not valid Morse code.", $"「{tok}」唔係有效嘅摩斯電碼。");
                sb.Append(ch.Value);
            }
        }
        return CipherResult.Good(sb.ToString());
    }

    private static string? LookupMorse(char c)
    {
        foreach (var (ch, m) in MorseTable) if (ch == c) return m;
        return null;
    }

    private static char? LookupChar(string morse)
    {
        foreach (var (ch, m) in MorseTable) if (m == morse) return ch;
        return null;
    }
}
