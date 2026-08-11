using System;
using System.Globalization;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Persisted, independent English and Cantonese tone levels. Tone variation is opt-in:
/// only <see cref="PlayfulText"/> can flow through this service, leaving ordinary and
/// safety-sensitive localization unchanged.
/// </summary>
public sealed class FunnyLevelSettings
{
    public const int MinimumLevel = 1;
    public const int MaximumLevel = 5;
    public const int DefaultEnglishLevel = 2;
    public const int DefaultCantoneseLevel = 3;
    public const string EnglishSettingKey = "tone.englishFunnyLevel";
    public const string CantoneseSettingKey = "tone.cantoneseFunnyLevel";

    private static readonly Lazy<FunnyLevelSettings> Shared = new(() =>
        new FunnyLevelSettings(SettingsStore.Get, (key, value) => SettingsStore.Set(key, value)));

    private readonly object _gate = new();
    private readonly Func<string, string, string> _get;
    private readonly Action<string, string> _set;
    private int _englishLevel;
    private int _cantoneseLevel;

    internal FunnyLevelSettings(Func<string, string, string> get, Action<string, string> set)
    {
        _get = get ?? throw new ArgumentNullException(nameof(get));
        _set = set ?? throw new ArgumentNullException(nameof(set));
        _englishLevel = ReadLevel(EnglishSettingKey, DefaultEnglishLevel);
        _cantoneseLevel = ReadLevel(CantoneseSettingKey, DefaultCantoneseLevel);
    }

    public static FunnyLevelSettings I => Shared.Value;

    public event EventHandler? Changed;

    public int EnglishLevel
    {
        get { lock (_gate) return _englishLevel; }
        set => SetLevel(EnglishSettingKey, value, isEnglish: true);
    }

    public int CantoneseLevel
    {
        get { lock (_gate) return _cantoneseLevel; }
        set => SetLevel(CantoneseSettingKey, value, isEnglish: false);
    }

    public string English(PlayfulText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.EnglishAt(EnglishLevel);
    }

    public string Cantonese(PlayfulText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.CantoneseAt(CantoneseLevel);
    }

    public string Pick(PlayfulText text, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(text);
        var en = English(text);
        var zh = Cantonese(text);
        return language switch
        {
            AppLanguage.Cantonese => zh,
            AppLanguage.English => en,
            _ => Loc.Both(en, zh),
        };
    }

    /// <summary>
    /// Styles factual status copy without changing the event, affected item, or available action.
    /// This is the bounded voice layer used by newer universal surfaces, including warnings and
    /// errors; the five variants never remove the operational facts.
    /// </summary>
    public string StyleEnglish(string factual) => Style(factual ?? string.Empty, EnglishLevel, english: true);

    public string StyleCantonese(string factual) => Style(factual ?? string.Empty, CantoneseLevel, english: false);

    /// <summary>Reload values after an explicit settings import.</summary>
    public void ReloadFromSettings()
    {
        var nextEnglish = ReadLevel(EnglishSettingKey, DefaultEnglishLevel);
        var nextCantonese = ReadLevel(CantoneseSettingKey, DefaultCantoneseLevel);
        var changed = false;

        lock (_gate)
        {
            if (_englishLevel != nextEnglish)
            {
                _englishLevel = nextEnglish;
                changed = true;
            }

            if (_cantoneseLevel != nextCantonese)
            {
                _cantoneseLevel = nextCantonese;
                changed = true;
            }
        }

        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SetLevel(string key, int value, bool isEnglish)
    {
        ValidateLevel(value);
        var changed = false;

        lock (_gate)
        {
            ref int current = ref (isEnglish ? ref _englishLevel : ref _cantoneseLevel);
            if (current != value)
            {
                current = value;
                _set(key, value.ToString(CultureInfo.InvariantCulture));
                changed = true;
            }
        }

        if (changed) Changed?.Invoke(this, EventArgs.Empty);
    }

    private int ReadLevel(string key, int fallback)
    {
        var raw = _get(key, fallback.ToString(CultureInfo.InvariantCulture));
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
               parsed is >= MinimumLevel and <= MaximumLevel
            ? parsed
            : fallback;
    }

    private static void ValidateLevel(int value)
    {
        if (value is < MinimumLevel or > MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Funny level must be between 1 and 5.");
    }

    private static string Style(string factual, int level, bool english)
    {
        if (level <= 1 || string.IsNullOrWhiteSpace(factual)) return factual;
        string suffix = english
            ? level switch
            {
                2 => " (facts unchanged)",
                3 => " — the local paper trail has arrived",
                4 => " — the tiny office goblin is taking notes",
                _ => " — maximum paperwork sparkle, same facts",
            }
            : level switch
            {
                2 => "（事實冇變）",
                3 => "——本機紙仔已經到場",
                4 => "——小小辦公室精靈幫手記低",
                _ => "——紙仔閃閃發光，但事實一樣",
            };
        return factual + suffix;
    }
}
