using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 一維條碼編碼器（純託管、手寫）· Pure-managed, hand-rolled 1D barcode encoders — no NuGet, no native
/// libraries. Supports Code 128 (auto code set B/C with checksum), Code 39 (with start/stop <c>*</c>) and
/// EAN-13 (left/right guards, parity, mod-10 check digit). Each encoder returns a <see cref="BarcodeResult"/>
/// carrying the module bit-pattern (true = bar, false = space), the human-readable text and a self-contained
/// SVG string. All methods are robust and never throw — bad input yields <see cref="BarcodeResult.Ok"/> =
/// false plus a bilingual error, so the UI can surface it without try/catch.
/// </summary>
public static class BarcodeService
{
    /// <summary>支援嘅符號體系 · Supported symbologies.</summary>
    public enum Symbology { Code128, Code39, Ean13 }

    /// <summary>編碼結果 · The outcome of an encode: bit pattern + metadata, or an error.</summary>
    public sealed class BarcodeResult
    {
        public bool Ok { get; init; }
        public string ErrorEn { get; init; } = "";
        public string ErrorZh { get; init; } = "";
        /// <summary>Module pattern — each bool is one narrow module; true = bar (black), false = space.</summary>
        public IReadOnlyList<bool> Modules { get; init; } = Array.Empty<bool>();
        /// <summary>Human-readable text drawn under the bars (already normalized, e.g. EAN check digit added).</summary>
        public string HumanText { get; init; } = "";
        public Symbology Symbology { get; init; }

        public static BarcodeResult Fail(string en, string zh) => new() { Ok = false, ErrorEn = en, ErrorZh = zh };
    }

    // ===== public entry =====

    /// <summary>編碼（永不擲例外）· Encode <paramref name="input"/> as <paramref name="symbology"/>. Never throws.</summary>
    public static BarcodeResult Encode(Symbology symbology, string? input)
    {
        try
        {
            input ??= "";
            return symbology switch
            {
                Symbology.Code128 => EncodeCode128(input),
                Symbology.Code39 => EncodeCode39(input),
                Symbology.Ean13 => EncodeEan13(input),
                _ => BarcodeResult.Fail("Unknown symbology.", "未知嘅條碼類型。"),
            };
        }
        catch
        {
            // Belt-and-braces: nothing above should throw, but a barcode generator must never crash the app.
            return BarcodeResult.Fail("Could not encode that input.", "呢個輸入編碼唔到。");
        }
    }

    // ===================================================================================================
    //  CODE 128  (code sets B and C, auto-switched; Start + checksum + Stop)
    // ===================================================================================================

    // 108 patterns (values 0..106 usable + 106=Stop). Each is a 6-digit run-length string:
    // widths of bar,space,bar,space,bar,space (Code 128 symbol = 11 modules across 3 bars + 3 spaces).
    private static readonly string[] C128 =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112",
    };
    private const int C128StartB = 104;
    private const int C128StartC = 105;
    private const int C128Stop = 106;

    private static BarcodeResult EncodeCode128(string input)
    {
        if (string.IsNullOrEmpty(input))
            return BarcodeResult.Fail("Enter text to encode as Code 128.", "輸入要編碼成 Code 128 嘅文字。");
        foreach (char c in input)
        {
            if (c < 32 || c > 126)
                return BarcodeResult.Fail(
                    "Code 128 supports printable ASCII only (space through ~).",
                    "Code 128 只支援可列印 ASCII（由空格到 ~）。");
        }

        // Build the value stream, switching to code-set C for runs of >=4 digits (and honoring parity).
        var codes = new List<int>();
        int startCode;
        int pos = 0;
        bool inC = StartWithC(input, 0);
        startCode = inC ? C128StartC : C128StartB;
        codes.Add(startCode);

        while (pos < input.Length)
        {
            if (inC)
            {
                if (pos + 1 < input.Length && char.IsDigit(input[pos]) && char.IsDigit(input[pos + 1]))
                {
                    int pair = (input[pos] - '0') * 10 + (input[pos + 1] - '0');
                    codes.Add(pair);
                    pos += 2;
                    // Stay in C while another full digit-pair (>=2 digits) remains; else drop to B.
                    if (!(pos + 1 < input.Length && char.IsDigit(input[pos]) && char.IsDigit(input[pos + 1])))
                    {
                        if (pos < input.Length) { codes.Add(100 /* Code B */); inC = false; }
                    }
                }
                else
                {
                    codes.Add(100); inC = false; // switch to B
                }
            }
            else
            {
                // Consider switching up to C if a long digit run starts here.
                if (StartWithC(input, pos))
                {
                    codes.Add(99 /* Code C */); inC = true; continue;
                }
                codes.Add(input[pos] - 32); // Code set B value
                pos++;
            }
        }

        // Checksum: start + sum(i * value_i) mod 103.
        long sum = startCode;
        for (int i = 1; i < codes.Count; i++) sum += (long)i * codes[i];
        int check = (int)(sum % 103);
        codes.Add(check);
        codes.Add(C128Stop);

        var bits = new List<bool>();
        AddQuiet(bits, 10);
        foreach (int v in codes) AddRunLengths(bits, C128[v]);
        AddQuiet(bits, 10);

        return new BarcodeResult { Ok = true, Modules = bits, HumanText = input, Symbology = Symbology.Code128 };
    }

    private static bool StartWithC(string s, int pos)
    {
        // Enter code set C when at least four consecutive digits begin at pos (a common, safe heuristic).
        int run = 0;
        for (int i = pos; i < s.Length && char.IsDigit(s[i]); i++) run++;
        return run >= 4;
    }

    // ===================================================================================================
    //  CODE 39  (start/stop *, 43 data chars, no check digit)
    // ===================================================================================================

    private const string C39Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-. $/+%*";

    // Each entry: 9 elements (5 bars + 4 spaces interleaved) as widths, 'n'=narrow 'w'=wide, plus a
    // narrow inter-character space appended by the caller.
    private static readonly string[] C39 =
    {
        "nnnwwnwnn","wnnwnnnnw","nnwwnnnnw","wnwwnnnnn","nnnwwnnnw", // 0-4
        "wnnwwnnnn","nnwwwnnnn","nnnwnnwnw","wnnwnnwnn","nnwwnnwnn", // 5-9
        "wnnnnwnnw","nnwnnwnnw","wnwnnwnnn","nnnnwwnnw","wnnnwwnnn", // A-E
        "nnwnwwnnn","nnnnnwwnw","wnnnnwwnn","nnwnnwwnn","nnnnwwwnn", // F-J
        "wnnnnnnww","nnwnnnnww","wnwnnnnwn","nnnnwnnww","wnnnwnnwn", // K-O
        "nnwnwnnwn","nnnnnnwww","wnnnnnwwn","nnwnnnwwn","nnnnwnwwn", // P-T
        "wwnnnnnnw","nwwnnnnnw","wwwnnnnnn","nwnnwnnnw","wwnnwnnnn", // U-Y
        "nwwnwnnnn","nwnnnnwnw","wwnnnnwnn","nwwnnnwnn","nwnnwnwnn", // Z - . space $
        "nwnwnwnnn","nwnwnnnwn","nwnnnwnwn","nnnwnwnwn",             // / + % *
    };

    private static BarcodeResult EncodeCode39(string input)
    {
        if (string.IsNullOrEmpty(input))
            return BarcodeResult.Fail("Enter text to encode as Code 39.", "輸入要編碼成 Code 39 嘅文字。");
        string upper = input.ToUpperInvariant();
        foreach (char c in upper)
        {
            if (c == '*')
                return BarcodeResult.Fail("'*' is reserved as the Code 39 start/stop character.",
                    "'*' 係 Code 39 嘅起訖字元，唔可以用。");
            if (C39Alphabet.IndexOf(c) < 0)
                return BarcodeResult.Fail(
                    "Code 39 allows A–Z, 0–9 and - . $ / + % and space.",
                    "Code 39 只支援 A–Z、0–9 同 - . $ / + % 同空格。");
        }

        var bits = new List<bool>();
        AddQuiet(bits, 10);
        // Framed by '*' start and stop.
        AddC39Char(bits, '*');
        AddNarrowSpace(bits);
        foreach (char c in upper)
        {
            AddC39Char(bits, c);
            AddNarrowSpace(bits);
        }
        AddC39Char(bits, '*');
        AddQuiet(bits, 10);

        return new BarcodeResult { Ok = true, Modules = bits, HumanText = "*" + upper + "*", Symbology = Symbology.Code39 };
    }

    private static void AddC39Char(List<bool> bits, char c)
    {
        int idx = C39Alphabet.IndexOf(c);
        if (idx < 0) return;
        string pat = C39[idx];
        bool bar = true; // patterns are bar-first, alternating
        foreach (char w in pat)
        {
            int width = w == 'w' ? 3 : 1;
            for (int i = 0; i < width; i++) bits.Add(bar);
            bar = !bar;
        }
    }

    private static void AddNarrowSpace(List<bool> bits) => bits.Add(false); // one narrow inter-character gap

    // ===================================================================================================
    //  EAN-13  (12 data digits + mod-10 check, left/right guards, L/G parity per first digit)
    // ===================================================================================================

    // L-code (odd parity) 7-module patterns for digits 0-9, as bit strings (1 = bar).
    private static readonly string[] EanL =
    {
        "0001101","0011001","0010011","0111101","0100011",
        "0110001","0101111","0111011","0110111","0001011",
    };
    // G-code (even parity) = reverse of R-code.
    private static readonly string[] EanG =
    {
        "0100111","0110011","0011011","0100001","0011101",
        "0111001","0000101","0010001","0001001","0010111",
    };
    // R-code (right half) — complement of L.
    private static readonly string[] EanR =
    {
        "1110010","1100110","1101100","1000010","1011100",
        "1001110","1010000","1000100","1001000","1110100",
    };
    // Parity of the six left digits, selected by the leading (first) digit. L = false, G = true.
    private static readonly string[] EanParity =
    {
        "LLLLLL","LLGLGG","LLGGLG","LLGGGL","LGLLGG",
        "LGGLLG","LGGGLL","LGLGLG","LGLGGL","LGGLGL",
    };

    private static BarcodeResult EncodeEan13(string input)
    {
        string digits = new string((input ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length != 12 && digits.Length != 13)
            return BarcodeResult.Fail(
                "EAN-13 needs 12 or 13 digits (a 13th check digit is optional).",
                "EAN-13 需要 12 或 13 個數字（第 13 位檢查碼可省略）。");

        if (digits.Length == 13)
        {
            int expected = Ean13Check(digits.Substring(0, 12));
            if (digits[12] - '0' != expected)
                return BarcodeResult.Fail(
                    $"Check digit should be {expected} for those 12 digits.",
                    $"呢 12 個數字嘅檢查碼應該係 {expected}。");
        }
        else
        {
            digits += (char)('0' + Ean13Check(digits));
        }

        int first = digits[0] - '0';
        string parity = EanParity[first];

        var bits = new List<bool>();
        AddQuiet(bits, 9);
        AddBitString(bits, "101"); // left guard
        for (int i = 0; i < 6; i++)
        {
            int d = digits[1 + i] - '0';
            AddBitString(bits, parity[i] == 'L' ? EanL[d] : EanG[d]);
        }
        AddBitString(bits, "01010"); // center guard
        for (int i = 0; i < 6; i++)
        {
            int d = digits[7 + i] - '0';
            AddBitString(bits, EanR[d]);
        }
        AddBitString(bits, "101"); // right guard
        AddQuiet(bits, 9);

        return new BarcodeResult { Ok = true, Modules = bits, HumanText = digits, Symbology = Symbology.Ean13 };
    }

    private static int Ean13Check(string first12)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            int d = first12[i] - '0';
            sum += (i % 2 == 0) ? d : d * 3;
        }
        int mod = sum % 10;
        return mod == 0 ? 0 : 10 - mod;
    }

    // ===== shared bit helpers =====

    private static void AddRunLengths(List<bool> bits, string runs)
    {
        // runs = alternating bar,space,bar,... widths as decimal chars, starting with a bar.
        bool bar = true;
        foreach (char ch in runs)
        {
            int w = ch - '0';
            for (int i = 0; i < w; i++) bits.Add(bar);
            bar = !bar;
        }
    }

    private static void AddBitString(List<bool> bits, string s)
    {
        foreach (char ch in s) bits.Add(ch == '1');
    }

    private static void AddQuiet(List<bool> bits, int modules)
    {
        for (int i = 0; i < modules; i++) bits.Add(false);
    }

    // ===================================================================================================
    //  SVG  (self-contained, copyable, savable)
    // ===================================================================================================

    /// <summary>
    /// 產生自足 SVG · Render the result to a standalone SVG string (black bars on white, optional human text).
    /// <paramref name="moduleWidth"/> is the narrow-module width in px; <paramref name="barHeight"/> the bar
    /// height. Never throws — returns an empty-ish valid SVG on a failed result.
    /// </summary>
    public static string ToSvg(BarcodeResult r, double moduleWidth = 2, double barHeight = 90, bool showText = true)
    {
        try
        {
            if (r is null || !r.Ok || r.Modules.Count == 0)
                return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"></svg>";

            int n = r.Modules.Count;
            double textH = showText && !string.IsNullOrEmpty(r.HumanText) ? 22 : 0;
            double w = n * moduleWidth;
            double h = barHeight + textH + 8;

            var sb = new StringBuilder();
            sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"")
              .Append(F(w)).Append("\" height=\"").Append(F(h))
              .Append("\" viewBox=\"0 0 ").Append(F(w)).Append(' ').Append(F(h)).Append("\">\n");
            sb.Append("  <rect width=\"").Append(F(w)).Append("\" height=\"").Append(F(h))
              .Append("\" fill=\"#ffffff\"/>\n");

            // Coalesce consecutive bar-modules into single rects.
            int i = 0;
            while (i < n)
            {
                if (r.Modules[i])
                {
                    int start = i;
                    while (i < n && r.Modules[i]) i++;
                    double x = start * moduleWidth;
                    double rw = (i - start) * moduleWidth;
                    sb.Append("  <rect x=\"").Append(F(x)).Append("\" y=\"0\" width=\"")
                      .Append(F(rw)).Append("\" height=\"").Append(F(barHeight)).Append("\" fill=\"#000000\"/>\n");
                }
                else i++;
            }

            if (textH > 0)
            {
                sb.Append("  <text x=\"").Append(F(w / 2)).Append("\" y=\"").Append(F(barHeight + textH))
                  .Append("\" font-family=\"Consolas,monospace\" font-size=\"18\" text-anchor=\"middle\" fill=\"#000000\">")
                  .Append(Escape(r.HumanText)).Append("</text>\n");
            }
            sb.Append("</svg>\n");
            return sb.ToString();
        }
        catch
        {
            return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"></svg>";
        }
    }

    private static string F(double d) => d.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
