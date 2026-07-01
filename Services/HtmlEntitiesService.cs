using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// HTML 實體編碼／解碼 · HTML entity encoder / decoder. Pure managed, never throws.
/// Encode escapes the HTML5 must-escape set (&amp; &lt; &gt; &quot; &#39;) and can optionally
/// escape every non-ASCII code point as a numeric &amp;#xHHHH; reference. Decode resolves named
/// entities from an embedded ~150-entry table plus decimal (&amp;#NNN;) and hex (&amp;#xHHH;) references.
/// </summary>
public static class HtmlEntitiesService
{
    /// <summary>A reference row for the UI list: entity name, the character it produces, a short description.</summary>
    public sealed class EntityRef
    {
        public string Name { get; set; } = "";        // e.g. "&amp;"
        public string Char { get; set; } = "";         // e.g. "&"
        public string Description { get; set; } = "";   // e.g. "Ampersand"
    }

    // Named entity -> unicode code point(s). ~150 common HTML named entities.
    private static readonly Dictionary<string, string> NameToChar = BuildNameToChar();

    // Reference rows shown in the UI (name -> char -> description). English descriptions kept short.
    private static readonly List<EntityRef> Reference = BuildReference();

    /// <summary>The common-entity reference list for the UI (click-to-copy).</summary>
    public static IReadOnlyList<EntityRef> ReferenceList => Reference;

    /// <summary>Encode text. Always escapes &amp; &lt; &gt; &quot; &#39;. When
    /// <paramref name="escapeNonAscii"/> is true, every code point &gt; 0x7E (and &lt; 0x20 controls except
    /// tab/newline/carriage-return) becomes a numeric hex reference.</summary>
    public static string Encode(string? input, bool escapeNonAscii)
    {
        if (string.IsNullOrEmpty(input)) return "";
        try
        {
            var sb = new StringBuilder(input!.Length + 16);
            // Iterate by Unicode code point so astral characters (emoji, etc.) encode correctly.
            for (int i = 0; i < input.Length;)
            {
                int cp;
                if (char.IsHighSurrogate(input[i]) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                {
                    cp = char.ConvertToUtf32(input[i], input[i + 1]);
                    i += 2;
                }
                else
                {
                    cp = input[i];
                    i += 1;
                }

                switch (cp)
                {
                    case '&': sb.Append("&amp;"); continue;
                    case '<': sb.Append("&lt;"); continue;
                    case '>': sb.Append("&gt;"); continue;
                    case '"': sb.Append("&quot;"); continue;
                    case '\'': sb.Append("&#39;"); continue;
                }

                if (escapeNonAscii && (cp > 0x7E || (cp < 0x20 && cp != '\t' && cp != '\n' && cp != '\r')))
                {
                    sb.Append("&#x").Append(cp.ToString("X", CultureInfo.InvariantCulture)).Append(';');
                }
                else
                {
                    sb.Append(char.ConvertFromUtf32(cp));
                }
            }
            return sb.ToString();
        }
        catch { return input ?? ""; }
    }

    /// <summary>Decode named (&amp;copy; &amp;hearts; …), decimal (&amp;#169;) and hex (&amp;#xA9;) references.
    /// Unknown or malformed references are left untouched.</summary>
    public static string Decode(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        try
        {
            var sb = new StringBuilder(input!.Length);
            int i = 0, n = input.Length;
            while (i < n)
            {
                char c = input[i];
                if (c != '&') { sb.Append(c); i++; continue; }

                int semi = input.IndexOf(';', i + 1);
                // Only look within a reasonable window for the terminating ';'.
                if (semi < 0 || semi - i > 32) { sb.Append(c); i++; continue; }

                string body = input.Substring(i + 1, semi - i - 1);
                string? resolved = ResolveEntity(body);
                if (resolved != null) { sb.Append(resolved); i = semi + 1; }
                else { sb.Append(c); i++; }
            }
            return sb.ToString();
        }
        catch { return input ?? ""; }
    }

    private static string? ResolveEntity(string body)
    {
        if (body.Length == 0) return null;
        try
        {
            if (body[0] == '#')
            {
                if (body.Length < 2) return null;
                int code;
                if (body[1] == 'x' || body[1] == 'X')
                {
                    if (body.Length < 3) return null;
                    if (!int.TryParse(body.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) return null;
                }
                else
                {
                    if (!int.TryParse(body.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out code)) return null;
                }
                if (code < 0 || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF)) return null;
                return char.ConvertFromUtf32(code);
            }
            // Named entity — case-sensitive first (HTML names are), then a tolerant fallback.
            if (NameToChar.TryGetValue(body, out var s)) return s;
            return null;
        }
        catch { return null; }
    }

    /// <summary>Count grapheme-ish length as .NET string length (UTF-16 code units) — good enough for a UI counter.</summary>
    public static int Length(string? s) => s?.Length ?? 0;

    private static Dictionary<string, string> BuildNameToChar()
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        void A(string name, int cp) { try { d[name] = char.ConvertFromUtf32(cp); } catch { } }

        // Core must-escape / whitespace
        A("amp", 38); A("lt", 60); A("gt", 62); A("quot", 34); A("apos", 39); A("nbsp", 160);
        // Punctuation & typography
        A("iexcl", 161); A("cent", 162); A("pound", 163); A("curren", 164); A("yen", 165);
        A("brvbar", 166); A("sect", 167); A("uml", 168); A("copy", 169); A("ordf", 170);
        A("laquo", 171); A("not", 172); A("shy", 173); A("reg", 174); A("macr", 175);
        A("deg", 176); A("plusmn", 177); A("sup2", 178); A("sup3", 179); A("acute", 180);
        A("micro", 181); A("para", 182); A("middot", 183); A("cedil", 184); A("sup1", 185);
        A("ordm", 186); A("raquo", 187); A("frac14", 188); A("frac12", 189); A("frac34", 190);
        A("iquest", 191); A("times", 215); A("divide", 247);
        // Latin-1 accented uppercase
        A("Agrave", 192); A("Aacute", 193); A("Acirc", 194); A("Atilde", 195); A("Auml", 196);
        A("Aring", 197); A("AElig", 198); A("Ccedil", 199); A("Egrave", 200); A("Eacute", 201);
        A("Ecirc", 202); A("Euml", 203); A("Igrave", 204); A("Iacute", 205); A("Icirc", 206);
        A("Iuml", 207); A("ETH", 208); A("Ntilde", 209); A("Ograve", 210); A("Oacute", 211);
        A("Ocirc", 212); A("Otilde", 213); A("Ouml", 214); A("Oslash", 216); A("Ugrave", 217);
        A("Uacute", 218); A("Ucirc", 219); A("Uuml", 220); A("Yacute", 221); A("THORN", 222);
        // Latin-1 accented lowercase
        A("szlig", 223); A("agrave", 224); A("aacute", 225); A("acirc", 226); A("atilde", 227);
        A("auml", 228); A("aring", 229); A("aelig", 230); A("ccedil", 231); A("egrave", 232);
        A("eacute", 233); A("ecirc", 234); A("euml", 235); A("igrave", 236); A("iacute", 237);
        A("icirc", 238); A("iuml", 239); A("eth", 240); A("ntilde", 241); A("ograve", 242);
        A("oacute", 243); A("ocirc", 244); A("otilde", 245); A("ouml", 246); A("oslash", 248);
        A("ugrave", 249); A("uacute", 250); A("ucirc", 251); A("uuml", 252); A("yacute", 253);
        A("thorn", 254); A("yuml", 255);
        // Latin Extended / ligatures
        A("OElig", 338); A("oelig", 339); A("Scaron", 352); A("scaron", 353); A("Yuml", 376);
        A("fnof", 402);
        // Greek (common)
        A("Alpha", 913); A("Beta", 914); A("Gamma", 915); A("Delta", 916); A("Epsilon", 917);
        A("Theta", 920); A("Lambda", 923); A("Mu", 924); A("Pi", 928); A("Sigma", 931);
        A("Phi", 934); A("Omega", 937);
        A("alpha", 945); A("beta", 946); A("gamma", 947); A("delta", 948); A("epsilon", 949);
        A("zeta", 950); A("eta", 951); A("theta", 952); A("lambda", 955); A("mu", 956);
        A("pi", 960); A("rho", 961); A("sigma", 963); A("tau", 964); A("phi", 966);
        A("chi", 967); A("psi", 968); A("omega", 969);
        // General punctuation
        A("ensp", 8194); A("emsp", 8195); A("thinsp", 8201); A("zwnj", 8204); A("zwj", 8205);
        A("lrm", 8206); A("rlm", 8207); A("ndash", 8211); A("mdash", 8212); A("lsquo", 8216);
        A("rsquo", 8217); A("sbquo", 8218); A("ldquo", 8220); A("rdquo", 8221); A("bdquo", 8222);
        A("dagger", 8224); A("Dagger", 8225); A("bull", 8226); A("hellip", 8230); A("permil", 8240);
        A("prime", 8242); A("Prime", 8243); A("lsaquo", 8249); A("rsaquo", 8250); A("oline", 8254);
        A("frasl", 8260); A("euro", 8364);
        // Letterlike / arrows / math
        A("trade", 8482); A("alefsym", 8501); A("larr", 8592); A("uarr", 8593); A("rarr", 8594);
        A("darr", 8595); A("harr", 8596); A("crarr", 8629); A("lArr", 8656); A("uArr", 8657);
        A("rArr", 8658); A("dArr", 8659); A("hArr", 8660);
        A("forall", 8704); A("part", 8706); A("exist", 8707); A("empty", 8709); A("nabla", 8711);
        A("isin", 8712); A("notin", 8713); A("ni", 8715); A("prod", 8719); A("sum", 8721);
        A("minus", 8722); A("lowast", 8727); A("radic", 8730); A("prop", 8733); A("infin", 8734);
        A("ang", 8736); A("and", 8743); A("or", 8744); A("cap", 8745); A("cup", 8746);
        A("int", 8747); A("there4", 8756); A("sim", 8764); A("cong", 8773); A("asymp", 8776);
        A("ne", 8800); A("equiv", 8801); A("le", 8804); A("ge", 8805); A("sub", 8834);
        A("sup", 8835); A("sube", 8838); A("supe", 8839); A("oplus", 8853); A("otimes", 8855);
        A("perp", 8869); A("sdot", 8901); A("lceil", 8968); A("rceil", 8969); A("lfloor", 8970);
        A("rfloor", 8971); A("loz", 9674);
        // Card suits / misc symbols
        A("spades", 9824); A("clubs", 9827); A("hearts", 9829); A("diams", 9830);
        A("star", 9733); A("starf", 9733); A("check", 10003); A("cross", 10007);

        return d;
    }

    private static List<EntityRef> BuildReference()
    {
        // Curated common subset for the click-to-copy reference list (kept readable, ~40 rows).
        (string name, int cp, string desc)[] rows =
        {
            ("amp", 38, "Ampersand"), ("lt", 60, "Less-than"), ("gt", 62, "Greater-than"),
            ("quot", 34, "Double quote"), ("apos", 39, "Apostrophe"), ("nbsp", 160, "Non-breaking space"),
            ("copy", 169, "Copyright"), ("reg", 174, "Registered"), ("trade", 8482, "Trademark"),
            ("cent", 162, "Cent"), ("pound", 163, "Pound sterling"), ("yen", 165, "Yen"),
            ("euro", 8364, "Euro"), ("sect", 167, "Section"), ("para", 182, "Pilcrow"),
            ("deg", 176, "Degree"), ("plusmn", 177, "Plus-minus"), ("times", 215, "Multiplication"),
            ("divide", 247, "Division"), ("frac12", 189, "One half"), ("frac14", 188, "One quarter"),
            ("micro", 181, "Micro"), ("middot", 183, "Middle dot"), ("bull", 8226, "Bullet"),
            ("hellip", 8230, "Ellipsis"), ("ndash", 8211, "En dash"), ("mdash", 8212, "Em dash"),
            ("lsquo", 8216, "Left single quote"), ("rsquo", 8217, "Right single quote"),
            ("ldquo", 8220, "Left double quote"), ("rdquo", 8221, "Right double quote"),
            ("dagger", 8224, "Dagger"), ("laquo", 171, "Left guillemet"), ("raquo", 187, "Right guillemet"),
            ("larr", 8592, "Left arrow"), ("rarr", 8594, "Right arrow"), ("uarr", 8593, "Up arrow"),
            ("darr", 8595, "Down arrow"), ("hearts", 9829, "Heart"), ("spades", 9824, "Spade"),
            ("clubs", 9827, "Club"), ("diams", 9830, "Diamond"), ("star", 9733, "Star"),
            ("check", 10003, "Check mark"), ("infin", 8734, "Infinity"), ("ne", 8800, "Not equal"),
            ("le", 8804, "Less or equal"), ("ge", 8805, "Greater or equal"), ("sum", 8721, "Summation"),
            ("radic", 8730, "Square root"),
        };

        var list = new List<EntityRef>(rows.Length);
        foreach (var (name, cp, desc) in rows)
        {
            string ch;
            try { ch = char.ConvertFromUtf32(cp); } catch { ch = ""; }
            list.Add(new EntityRef { Name = "&" + name + ";", Char = ch, Description = desc });
        }
        return list;
    }
}
