using System;
using System.Collections.Generic;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// SQL 格式化 · SQL formatter / beautifier — pure-managed tokenizer + pretty-printer.
/// Handles 'strings', "identifiers", `backtick idents`, [bracket idents], -- line and
/// /* block */ comments. Puts each major clause on its own line, one column per line in
/// SELECT, optionally upper-cases keywords. Also a whitespace-collapsing minifier.
/// Never throws — callers get best-effort output.
/// </summary>
public static class SqlFormatService
{
    // Clauses that start a new (outer-indent) line.
    private static readonly HashSet<string> MajorClauses = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "FROM", "WHERE", "AND", "OR", "JOIN", "LEFT", "RIGHT", "INNER",
        "OUTER", "FULL", "CROSS", "GROUP", "ORDER", "HAVING", "LIMIT", "OFFSET",
        "UNION", "INSERT", "UPDATE", "DELETE", "VALUES", "SET", "INTO", "ON"
    };

    // Recognised keyword vocabulary for upper-casing (superset of the above).
    private static readonly HashSet<string> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT","FROM","WHERE","AND","OR","NOT","JOIN","LEFT","RIGHT","INNER","OUTER",
        "FULL","CROSS","GROUP","BY","ORDER","HAVING","LIMIT","OFFSET","UNION","ALL",
        "INSERT","UPDATE","DELETE","VALUES","SET","INTO","ON","AS","IN","IS","NULL",
        "LIKE","BETWEEN","EXISTS","CASE","WHEN","THEN","ELSE","END","DISTINCT","ASC",
        "DESC","COUNT","SUM","AVG","MIN","MAX","INNER","CREATE","TABLE","DROP","ALTER",
        "PRIMARY","KEY","FOREIGN","REFERENCES","DEFAULT","INDEX","VIEW","WITH","USING",
        "TRUE","FALSE","INT","INTEGER","VARCHAR","TEXT","DATE","DATETIME","BOOLEAN"
    };

    private enum Kind { Word, Punct, Str, Comment, Number }

    private readonly struct Token
    {
        public readonly Kind Kind;
        public readonly string Text;
        public Token(Kind k, string t) { Kind = k; Text = t; }
    }

    /// <summary>Beautify SQL. Never throws.</summary>
    public static string Format(string? sql, bool upperKeywords, int indentSize)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
            if (indentSize < 0) indentSize = 0;
            if (indentSize > 16) indentSize = 16;

            var tokens = Tokenize(sql);
            string indent = new string(' ', indentSize);
            var sb = new StringBuilder();

            bool inSelect = false;      // inside a SELECT column list
            bool atLineStart = true;
            bool prevWasOpenParen = false;

            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                string text = t.Text;

                if (t.Kind == Kind.Word && upperKeywords && Keywords.Contains(text))
                    text = text.ToUpperInvariant();

                // Multi-word clause openers.
                bool isMajor = t.Kind == Kind.Word && MajorClauses.Contains(t.Text);
                string upper = t.Text.ToUpperInvariant();

                if (isMajor)
                {
                    // Track SELECT column-list state.
                    if (upper == "SELECT") inSelect = true;
                    else if (upper is "FROM" or "WHERE" or "GROUP" or "ORDER" or "HAVING"
                             or "LIMIT" or "UNION" or "INSERT" or "UPDATE" or "DELETE"
                             or "VALUES" or "SET" or "INTO")
                        inSelect = false;

                    // Newline before the clause (unless it's the very first token, or a
                    // continuation piece of a compound keyword like LEFT JOIN / GROUP BY).
                    bool continuation = ContinuesPrevClause(tokens, i);
                    if (!atLineStart && !continuation)
                        sb.Append('\n');

                    if (continuation)
                    {
                        if (!atLineStart) sb.Append(' ');
                    }

                    sb.Append(text);
                    atLineStart = false;
                    prevWasOpenParen = false;
                    continue;
                }

                if (t.Kind == Kind.Comment)
                {
                    if (!atLineStart) sb.Append('\n');
                    sb.Append(text);
                    sb.Append('\n');
                    atLineStart = true;
                    prevWasOpenParen = false;
                    continue;
                }

                // Comma inside a SELECT list -> one column per line.
                if (t.Kind == Kind.Punct && text == "," && inSelect)
                {
                    // Trim any trailing space we may have added, then comma + newline + indent.
                    TrimTrailingSpaces(sb);
                    sb.Append(",\n");
                    sb.Append(indent);
                    atLineStart = false;
                    prevWasOpenParen = false;
                    continue;
                }

                // Indent the first item of clause bodies.
                if (atLineStart)
                {
                    // After a major clause keyword the body sits on the same line separated
                    // by a space; but the SELECT column list starts indented on a new line.
                    if (inSelect && LooksLikeFirstSelectItem(sb))
                        sb.Append(indent);
                }

                // Spacing rules.
                if (sb.Length > 0 && !atLineStart)
                {
                    char last = sb[sb.Length - 1];
                    bool noSpaceBefore = t.Kind == Kind.Punct &&
                        (text is "," or ")" or ";" or "." or "::");
                    bool afterOpen = prevWasOpenParen || last == '(' || last == '.';
                    if (last != '\n' && last != ' ' && !noSpaceBefore && !afterOpen)
                        sb.Append(' ');
                }

                // For a SELECT keyword just emitted, put its list on the next line indented.
                if (inSelect && sb.Length >= 6 && EndsWithSelectHeader(sb) && t.Kind != Kind.Punct)
                {
                    sb.Append('\n').Append(indent);
                }

                sb.Append(text);
                atLineStart = false;
                prevWasOpenParen = t.Kind == Kind.Punct && text == "(";
            }

            // Normalise line endings and trim trailing blanks per line.
            return Cleanup(sb.ToString());
        }
        catch
        {
            return sql ?? string.Empty;
        }
    }

    /// <summary>Collapse all insignificant whitespace to single spaces. Never throws.</summary>
    public static string Minify(string? sql)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sql)) return string.Empty;
            var tokens = Tokenize(sql);
            var sb = new StringBuilder();
            for (int i = 0; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Kind == Kind.Comment) continue; // minify drops comments
                string text = t.Text;
                if (sb.Length > 0)
                {
                    char last = sb[sb.Length - 1];
                    bool noSpaceBefore = t.Kind == Kind.Punct &&
                        (text is "," or ")" or ";" or "." or "::");
                    bool afterOpenOrDot = last == '(' || last == '.';
                    if (!noSpaceBefore && !afterOpenOrDot)
                        sb.Append(' ');
                }
                sb.Append(text);
            }
            return sb.ToString().Trim();
        }
        catch
        {
            return sql ?? string.Empty;
        }
    }

    // ---- helpers ---------------------------------------------------------

    private static List<Token> Tokenize(string s)
    {
        var tokens = new List<Token>();
        int i = 0, n = s.Length;
        while (i < n)
        {
            char c = s[i];

            // Whitespace.
            if (char.IsWhiteSpace(c)) { i++; continue; }

            // Line comment  --
            if (c == '-' && i + 1 < n && s[i + 1] == '-')
            {
                int start = i;
                while (i < n && s[i] != '\n') i++;
                tokens.Add(new Token(Kind.Comment, s.Substring(start, i - start).TrimEnd()));
                continue;
            }

            // Block comment  /* ... */
            if (c == '/' && i + 1 < n && s[i + 1] == '*')
            {
                int start = i;
                i += 2;
                while (i + 1 < n && !(s[i] == '*' && s[i + 1] == '/')) i++;
                i = Math.Min(n, i + 2);
                tokens.Add(new Token(Kind.Comment, s.Substring(start, i - start)));
                continue;
            }

            // Quoted string / identifier: ' " ` [ ]
            if (c is '\'' or '"' or '`')
            {
                char q = c;
                int start = i;
                i++;
                while (i < n)
                {
                    if (s[i] == q)
                    {
                        // doubled quote escape ('' or "")
                        if (i + 1 < n && s[i + 1] == q) { i += 2; continue; }
                        i++;
                        break;
                    }
                    i++;
                }
                tokens.Add(new Token(Kind.Str, s.Substring(start, i - start)));
                continue;
            }
            if (c == '[')
            {
                int start = i;
                i++;
                while (i < n && s[i] != ']') i++;
                if (i < n) i++;
                tokens.Add(new Token(Kind.Str, s.Substring(start, i - start)));
                continue;
            }

            // Number.
            if (char.IsDigit(c))
            {
                int start = i;
                while (i < n && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                tokens.Add(new Token(Kind.Number, s.Substring(start, i - start)));
                continue;
            }

            // Word (identifier / keyword) — letters, digits, _, $, @, #.
            if (char.IsLetter(c) || c is '_' or '@' or '#' or '$')
            {
                int start = i;
                while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] is '_' or '@' or '#' or '$')) i++;
                tokens.Add(new Token(Kind.Word, s.Substring(start, i - start)));
                continue;
            }

            // Two-char operators.
            if (i + 1 < n)
            {
                string two = s.Substring(i, 2);
                if (two is "<=" or ">=" or "<>" or "!=" or "||" or "::")
                {
                    tokens.Add(new Token(Kind.Punct, two));
                    i += 2;
                    continue;
                }
            }

            // Single punctuation / operator.
            tokens.Add(new Token(Kind.Punct, c.ToString()));
            i++;
        }
        return tokens;
    }

    // Is token[i] a continuation of a compound clause opened by token[i-1]?
    // e.g. LEFT JOIN, RIGHT OUTER JOIN, GROUP BY, ORDER BY, UNION ALL, INNER JOIN.
    private static bool ContinuesPrevClause(List<Token> tokens, int i)
    {
        if (i == 0) return false;
        string prev = tokens[i - 1].Text.ToUpperInvariant();
        string cur = tokens[i].Text.ToUpperInvariant();
        if (cur == "JOIN" && prev is "LEFT" or "RIGHT" or "INNER" or "OUTER" or "FULL" or "CROSS")
            return true;
        if (cur == "OUTER" && prev is "LEFT" or "RIGHT" or "FULL")
            return true;
        return false;
    }

    private static bool LooksLikeFirstSelectItem(StringBuilder sb)
    {
        // First item right after "SELECT\n"
        return sb.Length >= 1 && sb[sb.Length - 1] == '\n';
    }

    private static bool EndsWithSelectHeader(StringBuilder sb)
    {
        // ends with the word SELECT (case-insensitive), nothing after it yet on the line
        int end = sb.Length;
        int start = end;
        while (start > 0 && (char.IsLetter(sb[start - 1]))) start--;
        if (end - start != 6) return false;
        string w = sb.ToString(start, 6);
        return string.Equals(w, "SELECT", StringComparison.OrdinalIgnoreCase);
    }

    private static void TrimTrailingSpaces(StringBuilder sb)
    {
        while (sb.Length > 0 && sb[sb.Length - 1] == ' ') sb.Length--;
    }

    private static string Cleanup(string s)
    {
        var lines = s.Replace("\r\n", "\n").Split('\n');
        var outLines = new List<string>(lines.Length);
        foreach (var line in lines)
            outLines.Add(line.TrimEnd());
        // collapse leading blank lines
        var sb = new StringBuilder();
        bool started = false;
        foreach (var l in outLines)
        {
            if (!started && l.Length == 0) continue;
            started = true;
            sb.Append(l).Append('\n');
        }
        return sb.ToString().TrimEnd() ;
    }
}
