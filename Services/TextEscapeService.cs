using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// 字串跳脫（多語言）· String escaper / unescaper for many target syntaxes (JSON, C#, JS, Java,
/// Python, XML/HTML, URL, shell single-quote, regex, CSV field, SQL string). Pure managed, never
/// throws — every method returns success + text (or a bilingual-friendly failure) so the UI stays alive.
/// </summary>
public static class TextEscapeService
{
    /// <summary>Supported target languages / syntaxes.</summary>
    public enum Lang
    {
        Json,
        CSharp,
        JavaScript,
        Java,
        Python,
        Html,
        Url,
        Shell,
        Regex,
        Csv,
        Sql,
    }

    /// <summary>Outcome of an escape/unescape. <see cref="Ok"/> false means malformed input.</summary>
    public readonly record struct Result(bool Ok, string Text);

    // ---- public entry points -------------------------------------------------

    public static Result Escape(Lang lang, string input)
    {
        input ??= string.Empty;
        try
        {
            return new Result(true, lang switch
            {
                Lang.Json => JsonEscape(input),
                Lang.CSharp => CStyleEscape(input, '"'),
                Lang.JavaScript => CStyleEscape(input, '"'),
                Lang.Java => CStyleEscape(input, '"'),
                Lang.Python => CStyleEscape(input, '"'),
                Lang.Html => System.Net.WebUtility.HtmlEncode(input),
                Lang.Url => Uri.EscapeDataString(input),
                Lang.Shell => ShellSingleQuote(input),
                Lang.Regex => Regex.Escape(input),
                Lang.Csv => CsvEscape(input),
                Lang.Sql => SqlEscape(input),
                _ => input,
            });
        }
        catch
        {
            return new Result(false, input);
        }
    }

    public static Result Unescape(Lang lang, string input)
    {
        input ??= string.Empty;
        try
        {
            return lang switch
            {
                Lang.Json => JsonUnescape(input),
                Lang.CSharp => CStyleUnescape(input),
                Lang.JavaScript => CStyleUnescape(input),
                Lang.Java => CStyleUnescape(input),
                Lang.Python => CStyleUnescape(input),
                Lang.Html => new Result(true, System.Net.WebUtility.HtmlDecode(input)),
                Lang.Url => new Result(true, Uri.UnescapeDataString(input)),
                Lang.Shell => new Result(true, ShellSingleQuoteUnwrap(input)),
                Lang.Regex => RegexUnescape(input),
                Lang.Csv => CsvUnescape(input),
                Lang.Sql => new Result(true, SqlUnescape(input)),
                _ => new Result(true, input),
            };
        }
        catch
        {
            return new Result(false, input);
        }
    }

    // ---- JSON ---------------------------------------------------------------

    private static string JsonEscape(string s)
    {
        // Serialize as a JSON string then strip the surrounding quotes.
        string quoted = JsonSerializer.Serialize(s);
        return quoted.Length >= 2 ? quoted.Substring(1, quoted.Length - 2) : quoted;
    }

    private static Result JsonUnescape(string s)
    {
        try
        {
            string body = s;
            // Tolerate input that already carries its own surrounding double quotes.
            if (!(body.Length >= 2 && body.StartsWith('"') && body.EndsWith('"')))
                body = "\"" + s.Replace("\"", "\\\"") + "\"";
            string? value = JsonSerializer.Deserialize<string>(body);
            return new Result(value is not null, value ?? s);
        }
        catch
        {
            return new Result(false, s);
        }
    }

    // ---- C / JS / Java / Python style ---------------------------------------

    private static string CStyleEscape(string s, char quote)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\0': sb.Append("\\0"); break;
                case '\a': sb.Append("\\a"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\v': sb.Append("\\v"); break;
                default:
                    if (c == quote) sb.Append('\\').Append(quote);
                    else if (c < 0x20 || c == 0x7f) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static Result CStyleUnescape(string s)
    {
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c != '\\') { sb.Append(c); continue; }
            if (i + 1 >= s.Length) return new Result(false, s); // dangling backslash
            char n = s[++i];
            switch (n)
            {
                case '\\': sb.Append('\\'); break;
                case '\'': sb.Append('\''); break;
                case '"': sb.Append('"'); break;
                case '`': sb.Append('`'); break;
                case '0': sb.Append('\0'); break;
                case 'a': sb.Append('\a'); break;
                case 'b': sb.Append('\b'); break;
                case 'f': sb.Append('\f'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case 't': sb.Append('\t'); break;
                case 'v': sb.Append('\v'); break;
                case 'u':
                    if (i + 4 >= s.Length) return new Result(false, s);
                    if (!TryHex(s.Substring(i + 1, 4), out int u)) return new Result(false, s);
                    sb.Append((char)u); i += 4; break;
                case 'x':
                {
                    // 1-4 hex digits (accepts JS/C style variable-length)
                    int start = i + 1, len = 0;
                    while (len < 4 && start + len < s.Length && Uri.IsHexDigit(s[start + len])) len++;
                    if (len == 0) return new Result(false, s);
                    if (!TryHex(s.Substring(start, len), out int x)) return new Result(false, s);
                    sb.Append((char)x); i += len; break;
                }
                case 'U':
                    if (i + 8 >= s.Length) return new Result(false, s);
                    if (!TryHex(s.Substring(i + 1, 8), out int big)) return new Result(false, s);
                    if (big > 0x10FFFF) return new Result(false, s);
                    sb.Append(char.ConvertFromUtf32(big)); i += 8; break;
                default:
                    return new Result(false, s); // unknown escape
            }
        }
        return new Result(true, sb.ToString());
    }

    private static bool TryHex(string h, out int value) =>
        int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

    // ---- Shell single-quote --------------------------------------------------

    private static string ShellSingleQuote(string s)
    {
        // POSIX-safe: wrap in single quotes, closing/reopening around embedded single quotes.
        return "'" + s.Replace("'", "'\\''") + "'";
    }

    private static string ShellSingleQuoteUnwrap(string s)
    {
        // Reverse of ShellSingleQuote. Walk the string honouring quote state so the
        // '\'' construct (close-quote, escaped-quote, reopen-quote) restores a literal '.
        var sb = new StringBuilder(s.Length);
        bool inQuote = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (inQuote)
            {
                if (c == '\'') inQuote = false;
                else sb.Append(c);
            }
            else if (c == '\'')
            {
                inQuote = true;
            }
            else if (c == '\\' && i + 1 < s.Length && s[i + 1] == '\'')
            {
                sb.Append('\''); // \' outside quotes == literal '
                i++;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    // ---- Regex ---------------------------------------------------------------

    private static Result RegexUnescape(string s)
    {
        try { return new Result(true, Regex.Unescape(s)); }
        catch { return new Result(false, s); }
    }

    // ---- CSV field -----------------------------------------------------------

    private static string CsvEscape(string s)
    {
        bool needQuote = s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r');
        string body = s.Replace("\"", "\"\"");
        return needQuote ? "\"" + body + "\"" : body;
    }

    private static Result CsvUnescape(string s)
    {
        if (s.Length >= 2 && s.StartsWith('"') && s.EndsWith('"'))
        {
            string inner = s.Substring(1, s.Length - 2);
            // Every quote inside a quoted field must be doubled.
            var sb = new StringBuilder(inner.Length);
            for (int i = 0; i < inner.Length; i++)
            {
                if (inner[i] == '"')
                {
                    if (i + 1 < inner.Length && inner[i + 1] == '"') { sb.Append('"'); i++; }
                    else return new Result(false, s); // lone quote inside a quoted field
                }
                else sb.Append(inner[i]);
            }
            return new Result(true, sb.ToString());
        }
        return new Result(true, s); // unquoted field passes through
    }

    // ---- SQL string literal --------------------------------------------------

    private static string SqlEscape(string s) => s.Replace("'", "''");

    private static string SqlUnescape(string s)
    {
        string t = s;
        if (t.Length >= 2 && t.StartsWith('\'') && t.EndsWith('\''))
            t = t.Substring(1, t.Length - 2);
        return t.Replace("''", "'");
    }
}
