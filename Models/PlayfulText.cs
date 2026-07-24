using System;

namespace WinForge.Models;

/// <summary>
/// Five deliberately authored tone variants for safe, non-operational copy.
/// Never use this type for destructive, financial, security, accessibility, or error text;
/// those surfaces must keep using <see cref="LocalizedText"/> so their wording stays exact.
/// </summary>
public sealed class PlayfulText
{
    private readonly string[] _english;
    private readonly string[] _cantonese;

    public PlayfulText(
        string en1, string en2, string en3, string en4, string en5,
        string zh1, string zh2, string zh3, string zh4, string zh5)
    {
        _english = Validate("English", en1, en2, en3, en4, en5);
        _cantonese = Validate("Cantonese", zh1, zh2, zh3, zh4, zh5);
    }

    public string EnglishAt(int level) => _english[Index(level)];

    public string CantoneseAt(int level) => _cantonese[Index(level)];

    private static string[] Validate(string language, params string[] values)
    {
        if (values.Length != 5)
            throw new ArgumentException($"{language} playful copy must contain exactly five levels.", nameof(values));

        for (var i = 0; i < values.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
                throw new ArgumentException($"{language} playful copy level {i + 1} is empty.", nameof(values));
        }

        return values;
    }

    private static int Index(int level)
    {
        if (level is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Funny level must be between 1 and 5.");
        return level - 1;
    }
}
