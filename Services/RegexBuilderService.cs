using System;
using System.Text;
using System.Text.RegularExpressions;

namespace WinForge.Services;

/// <summary>
/// Pure .NET regular-expression construction helpers shared by the visual builder and search surfaces.
/// Values are escaped unless a builder operation explicitly accepts a sub-pattern.
/// </summary>
public static class RegexBuilderService
{
    public enum PieceKind
    {
        Literal,
        CharacterClass,
        Anchor,
        Group,
        Alternation,
        Quantifier,
    }

    public enum AnchorKind
    {
        StartOfString,
        EndOfString,
        StartOfLine,
        EndOfLine,
        WordBoundary,
        NonWordBoundary,
    }

    public enum PieceError
    {
        None,
        Unsupported,
        EmptyLiteral,
        EmptyCharacterClass,
        EmptyGroup,
        InvalidGroupName,
        MissingAlternationBranch,
        EmptyQuantifierAtom,
        InvalidQuantity,
        InvalidMinimum,
        InvalidMaximum,
    }

    public sealed record PieceResult(bool Ok, string Token, PieceError Error);

    public static PieceResult Build(
        PieceKind kind,
        string? primary,
        string? secondary = null,
        bool option = false,
        AnchorKind anchor = AnchorKind.StartOfString)
    {
        primary ??= string.Empty;
        secondary ??= string.Empty;

        return kind switch
        {
            PieceKind.Literal => Require(primary, Regex.Escape(primary), PieceError.EmptyLiteral),
            PieceKind.CharacterClass => BuildCharacterClass(primary, option),
            PieceKind.Anchor => new PieceResult(true, AnchorToken(anchor), PieceError.None),
            PieceKind.Group => BuildGroup(primary, secondary, option),
            PieceKind.Alternation => BuildAlternation(primary, secondary),
            PieceKind.Quantifier => BuildQuantifier(primary, secondary),
            _ => new PieceResult(false, string.Empty, PieceError.Unsupported),
        };
    }

    public static string InsertAtSelection(string pattern, string token, int selectionStart, int selectionLength)
    {
        pattern ??= string.Empty;
        token ??= string.Empty;
        selectionStart = Math.Clamp(selectionStart, 0, pattern.Length);
        selectionLength = Math.Clamp(selectionLength, 0, pattern.Length - selectionStart);

        if ((long)pattern.Length - selectionLength + token.Length > RegexTesterService.MaxPatternLength)
            throw new ArgumentOutOfRangeException(nameof(token),
                $"The resulting pattern exceeds {RegexTesterService.MaxPatternLength:N0} characters.");

        return string.Concat(pattern.AsSpan(0, selectionStart), token,
            pattern.AsSpan(selectionStart + selectionLength));
    }

    private static PieceResult BuildCharacterClass(string characters, bool negate)
    {
        if (characters.Length == 0)
            return new PieceResult(false, string.Empty, PieceError.EmptyCharacterClass);

        var escaped = new StringBuilder(characters.Length * 2);
        foreach (char value in characters)
        {
            if (value is '\\' or ']' or '-' or '^') escaped.Append('\\');
            escaped.Append(value);
        }

        return new PieceResult(true, $"[{(negate ? "^" : string.Empty)}{escaped}]", PieceError.None);
    }

    private static PieceResult BuildGroup(string subPattern, string name, bool nonCapturing)
    {
        if (subPattern.Length == 0)
            return new PieceResult(false, string.Empty, PieceError.EmptyGroup);

        if (nonCapturing)
            return new PieceResult(true, $"(?:{subPattern})", PieceError.None);

        if (name.Length == 0)
            return new PieceResult(true, $"({subPattern})", PieceError.None);

        if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)))
            return new PieceResult(false, string.Empty, PieceError.InvalidGroupName);

        return new PieceResult(true, $"(?<{name}>{subPattern})", PieceError.None);
    }

    private static PieceResult BuildAlternation(string left, string right)
    {
        if (left.Length == 0 || right.Length == 0)
            return new PieceResult(false, string.Empty, PieceError.MissingAlternationBranch);
        return new PieceResult(true, $"(?:{left}|{right})", PieceError.None);
    }

    private static PieceResult BuildQuantifier(string atom, string quantity)
    {
        if (atom.Length == 0)
            return new PieceResult(false, string.Empty, PieceError.EmptyQuantifierAtom);

        quantity = quantity.Trim();
        if (quantity is "*" or "+" or "?")
            return new PieceResult(true, $"(?:{atom}){quantity}", PieceError.None);

        Match parsed = Regex.Match(quantity, @"^(?<min>\d+)(?:,(?<max>\d*)?)?$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        if (!parsed.Success)
            return new PieceResult(false, string.Empty, PieceError.InvalidQuantity);

        if (!int.TryParse(parsed.Groups["min"].Value, out int min) || min > 100_000)
            return new PieceResult(false, string.Empty, PieceError.InvalidMinimum);

        string maxText = parsed.Groups["max"].Value;
        if (parsed.Groups["max"].Success && maxText.Length > 0)
        {
            if (!int.TryParse(maxText, out int max) || max > 100_000 || max < min)
                return new PieceResult(false, string.Empty, PieceError.InvalidMaximum);
        }

        return new PieceResult(true, $"(?:{atom}){{{quantity}}}", PieceError.None);
    }

    private static PieceResult Require(string value, string token, PieceError error) => value.Length == 0
        ? new PieceResult(false, string.Empty, error)
        : new PieceResult(true, token, PieceError.None);

    private static string AnchorToken(AnchorKind anchor) => anchor switch
    {
        AnchorKind.StartOfString => @"\A",
        AnchorKind.EndOfString => @"\z",
        AnchorKind.StartOfLine => "^",
        AnchorKind.EndOfLine => "$",
        AnchorKind.WordBoundary => @"\b",
        AnchorKind.NonWordBoundary => @"\B",
        _ => @"\A",
    };
}
