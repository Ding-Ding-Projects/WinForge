using System;
using System.Globalization;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 網址別名產生器 · Slugify (URL slug generator) — turns arbitrary text into clean URL slugs.
/// Pure managed C#: NFD normalization + Unicode category checks for diacritic stripping.
/// Every method is robust and never throws.
/// </summary>
public static class SlugifyService
{
    public enum Separator { Hyphen, Underscore, Dot }
    public enum LetterCase { Lower, Upper, Keep }

    public sealed class Options
    {
        public Separator Separator { get; set; } = Separator.Hyphen;
        public LetterCase Case { get; set; } = LetterCase.Lower;
        public bool StripDiacritics { get; set; } = true;
        public int MaxLength { get; set; } // 0 = unlimited
        public bool CollapseRepeats { get; set; } = true;
        public bool KeepUnicodeLetters { get; set; } // off = ASCII only
    }

    public static char SepChar(Separator s) => s switch
    {
        Separator.Underscore => '_',
        Separator.Dot => '.',
        _ => '-',
    };

    /// <summary>Slugify a single line of text. Never throws — returns "" on any trouble.</summary>
    public static string Slugify(string? input, Options? opts)
    {
        try
        {
            opts ??= new Options();
            input ??= string.Empty;
            char sep = SepChar(opts.Separator);

            // 1) Optionally strip diacritics via NFD + drop combining marks (café -> cafe).
            string source = input;
            if (opts.StripDiacritics)
            {
                string nfd = input.Normalize(NormalizationForm.FormD);
                var strip = new StringBuilder(nfd.Length);
                foreach (char c in nfd)
                {
                    if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                        strip.Append(c);
                }
                source = strip.ToString().Normalize(NormalizationForm.FormC);
            }

            // 2) Walk chars: keep letters/digits, turn anything else into a separator boundary.
            var sb = new StringBuilder(source.Length);
            bool pendingSep = false;
            foreach (char c in source)
            {
                bool keep;
                if (c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9')
                {
                    keep = true;
                }
                else if (opts.KeepUnicodeLetters &&
                         (char.IsLetterOrDigit(c) && c > 127))
                {
                    // Keep non-ASCII letters/digits (e.g. 中文) as-is.
                    keep = true;
                }
                else
                {
                    keep = false;
                }

                if (keep)
                {
                    if (pendingSep && sb.Length > 0) sb.Append(sep);
                    pendingSep = false;
                    sb.Append(c);
                }
                else
                {
                    // Any separator/punctuation/whitespace becomes a boundary.
                    pendingSep = true;
                }
            }

            string slug = sb.ToString();

            // 3) Collapse repeated separators if requested (the boundary logic already
            //    avoids most, but a leading run or KeepUnicode edge could leave doubles).
            if (opts.CollapseRepeats && slug.Length > 0)
            {
                var col = new StringBuilder(slug.Length);
                char prev = '\0';
                foreach (char c in slug)
                {
                    if (c == sep && prev == sep) continue;
                    col.Append(c);
                    prev = c;
                }
                slug = col.ToString();
            }

            // 4) Trim stray separators from the ends.
            slug = slug.Trim(sep);

            // 5) Case.
            slug = opts.Case switch
            {
                LetterCase.Lower => slug.ToLowerInvariant(),
                LetterCase.Upper => slug.ToUpperInvariant(),
                _ => slug,
            };

            // 6) Max length (trim, then drop a trailing separator left behind).
            if (opts.MaxLength > 0 && slug.Length > opts.MaxLength)
                slug = slug.Substring(0, opts.MaxLength).Trim(sep);

            return slug;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>Slugify a multi-line block; each non-empty line becomes one slug. Never throws.</summary>
    public static string SlugifyBlock(string? input, Options? opts)
    {
        try
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            string[] lines = input.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var outLines = new StringBuilder();
            bool first = true;
            foreach (string line in lines)
            {
                if (line.Trim().Length == 0) continue;
                string slug = Slugify(line, opts);
                if (!first) outLines.Append('\n');
                outLines.Append(slug);
                first = false;
            }
            return outLines.ToString();
        }
        catch
        {
            return string.Empty;
        }
    }
}
