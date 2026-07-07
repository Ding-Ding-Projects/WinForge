using System;
using System.Collections.Generic;
using System.Globalization;

namespace WinForge.Services;

/// <summary>
/// 表達式計數機引擎 · Pure managed recursive-descent expression evaluator over <see cref="double"/>.
/// Supports + - * / % ^ (right-assoc power), unary minus, parentheses, a set of math functions,
/// and the constants pi/e. An angle mode (degrees/radians) affects the trig functions.
/// The evaluator NEVER throws to the caller — every failure is returned as a <see cref="Result"/>
/// with <c>Ok == false</c> and a bilingual-friendly (English) error tag the UI can localize/show.
/// </summary>
public static class CalculatorService
{
    /// <summary>Angle unit for trig functions.</summary>
    public enum AngleMode { Radians, Degrees }

    /// <summary>Outcome of an evaluation. Value is only meaningful when <see cref="Ok"/> is true.</summary>
    public readonly struct Result
    {
        public bool Ok { get; }
        public double Value { get; }
        public string Error { get; }

        private Result(bool ok, double value, string error)
        {
            Ok = ok;
            Value = value;
            Error = error;
        }

        public static Result Success(double v) => new(true, v, string.Empty);
        public static Result Fail(string err) => new(false, 0, err);
    }

    /// <summary>Evaluate <paramref name="expression"/>. Never throws — returns a <see cref="Result"/>.</summary>
    public static Result Evaluate(string expression, AngleMode angleMode)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return Result.Fail("empty");

        try
        {
            var tokens = Tokenize(expression);
            var parser = new Parser(tokens, angleMode);
            double v = parser.ParseExpression();
            parser.ExpectEnd();

            if (double.IsNaN(v)) return Result.Fail("nan");
            if (double.IsInfinity(v)) return Result.Fail("infinity");
            return Result.Success(v);
        }
        catch (EvalException ex)
        {
            return Result.Fail(ex.Tag);
        }
        catch (Exception)
        {
            return Result.Fail("malformed");
        }
    }

    // ---- Tokenizer -------------------------------------------------------

    private enum TokType { Number, Ident, Op, LParen, RParen, End }

    private readonly struct Token
    {
        public TokType Type { get; }
        public string Text { get; }
        public double Num { get; }
        public Token(TokType t, string text, double num = 0) { Type = t; Text = text; Num = num; }
    }

    private sealed class EvalException : Exception
    {
        public string Tag { get; }
        public EvalException(string tag) { Tag = tag; }
    }

    private static List<Token> Tokenize(string s)
    {
        var list = new List<Token>();
        int i = 0;
        int n = s.Length;
        while (i < n)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || c == '.')
            {
                int start = i;
                bool seenDot = false;
                bool seenExp = false;
                while (i < n)
                {
                    char d = s[i];
                    if (char.IsDigit(d)) { i++; }
                    else if (d == '.' && !seenDot && !seenExp) { seenDot = true; i++; }
                    else if ((d == 'e' || d == 'E') && !seenExp && i > start)
                    {
                        seenExp = true;
                        i++;
                        if (i < n && (s[i] == '+' || s[i] == '-')) i++;
                    }
                    else break;
                }
                string numStr = s.Substring(start, i - start);
                if (!double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                    throw new EvalException("malformed");
                list.Add(new Token(TokType.Number, numStr, val));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < n && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
                list.Add(new Token(TokType.Ident, s.Substring(start, i - start)));
                continue;
            }

            switch (c)
            {
                case '+':
                case '-':
                case '*':
                case '/':
                case '%':
                case '^':
                    list.Add(new Token(TokType.Op, c.ToString()));
                    i++;
                    break;
                case '(':
                    list.Add(new Token(TokType.LParen, "("));
                    i++;
                    break;
                case ')':
                    list.Add(new Token(TokType.RParen, ")"));
                    i++;
                    break;
                default:
                    throw new EvalException("badchar");
            }
        }
        list.Add(new Token(TokType.End, string.Empty));
        return list;
    }

    // ---- Recursive-descent parser / evaluator ----------------------------
    //   expression := term (('+'|'-') term)*
    //   term       := power (('*'|'/'|'%') power)*
    //   power      := unary ('^' power)?            (right-associative)
    //   unary      := ('+'|'-') unary | primary
    //   primary    := number | ident | ident '(' expression ')' | '(' expression ')'

    private sealed class Parser
    {
        private readonly List<Token> _t;
        private readonly AngleMode _mode;
        private int _pos;

        public Parser(List<Token> tokens, AngleMode mode) { _t = tokens; _mode = mode; }

        private Token Cur => _t[_pos];
        private Token Next() => _t[_pos++];

        public void ExpectEnd()
        {
            if (Cur.Type != TokType.End) throw new EvalException("trailing");
        }

        public double ParseExpression()
        {
            double left = ParseTerm();
            while (Cur.Type == TokType.Op && (Cur.Text == "+" || Cur.Text == "-"))
            {
                string op = Next().Text;
                double right = ParseTerm();
                left = op == "+" ? left + right : left - right;
            }
            return left;
        }

        private double ParseTerm()
        {
            double left = ParsePower();
            while (Cur.Type == TokType.Op && (Cur.Text == "*" || Cur.Text == "/" || Cur.Text == "%"))
            {
                string op = Next().Text;
                double right = ParsePower();
                switch (op)
                {
                    case "*": left *= right; break;
                    case "/":
                        if (right == 0) throw new EvalException("divzero");
                        left /= right;
                        break;
                    default: // %
                        if (right == 0) throw new EvalException("divzero");
                        left %= right;
                        break;
                }
            }
            return left;
        }

        private double ParsePower()
        {
            double baseVal = ParseUnary();
            if (Cur.Type == TokType.Op && Cur.Text == "^")
            {
                Next();
                double exp = ParsePower(); // right-associative
                return Math.Pow(baseVal, exp);
            }
            return baseVal;
        }

        private double ParseUnary()
        {
            if (Cur.Type == TokType.Op && (Cur.Text == "+" || Cur.Text == "-"))
            {
                string op = Next().Text;
                double v = ParseUnary();
                return op == "-" ? -v : v;
            }
            return ParsePrimary();
        }

        private double ParsePrimary()
        {
            Token tok = Cur;
            switch (tok.Type)
            {
                case TokType.Number:
                    Next();
                    return tok.Num;

                case TokType.LParen:
                {
                    Next();
                    double v = ParseExpression();
                    if (Cur.Type != TokType.RParen) throw new EvalException("unbalanced");
                    Next();
                    return v;
                }

                case TokType.Ident:
                {
                    Next();
                    string name = tok.Text.ToLowerInvariant();

                    // Constant?
                    switch (name)
                    {
                        case "pi": return Math.PI;
                        case "e": return Math.E;
                    }

                    // Function call — requires parentheses.
                    if (Cur.Type == TokType.LParen)
                    {
                        Next();
                        double arg = ParseExpression();
                        if (Cur.Type != TokType.RParen) throw new EvalException("unbalanced");
                        Next();
                        return ApplyFunc(name, arg);
                    }

                    throw new EvalException("unknown:" + name);
                }

                case TokType.RParen:
                    throw new EvalException("unbalanced");

                case TokType.End:
                    throw new EvalException("incomplete");

                default:
                    throw new EvalException("malformed");
            }
        }

        private double ApplyFunc(string name, double x)
        {
            double ToRad(double a) => _mode == AngleMode.Degrees ? a * Math.PI / 180.0 : a;
            double FromRad(double a) => _mode == AngleMode.Degrees ? a * 180.0 / Math.PI : a;

            switch (name)
            {
                case "sin": return Math.Sin(ToRad(x));
                case "cos": return Math.Cos(ToRad(x));
                case "tan": return Math.Tan(ToRad(x));
                case "asin":
                    if (x < -1 || x > 1) throw new EvalException("domain");
                    return FromRad(Math.Asin(x));
                case "acos":
                    if (x < -1 || x > 1) throw new EvalException("domain");
                    return FromRad(Math.Acos(x));
                case "atan": return FromRad(Math.Atan(x));
                case "sqrt":
                    if (x < 0) throw new EvalException("domain");
                    return Math.Sqrt(x);
                case "cbrt": return Math.Cbrt(x);
                case "ln":
                    if (x <= 0) throw new EvalException("domain");
                    return Math.Log(x);
                case "log":
                    if (x <= 0) throw new EvalException("domain");
                    return Math.Log10(x);
                case "log2":
                    if (x <= 0) throw new EvalException("domain");
                    return Math.Log2(x);
                case "abs": return Math.Abs(x);
                case "round": return Math.Round(x, MidpointRounding.AwayFromZero);
                case "floor": return Math.Floor(x);
                case "ceil": return Math.Ceiling(x);
                case "exp": return Math.Exp(x);
                default: throw new EvalException("unknownfn:" + name);
            }
        }
    }
}
