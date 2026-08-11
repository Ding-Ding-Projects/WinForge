using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WinForge.Services;

public enum NarratorLanguage
{
    English,
    Cantonese,
    Both,
}

/// <summary>
/// Opt-in app-event narration. Debouncing and per-category cooldowns keep routine events quiet;
/// the underlying announcement pump serializes speech and replaces an older queued event in the
/// same category. Narration is disabled by default and never stores credentials or message history.
/// </summary>
public static class NarratorService
{
    private const string EnabledKey = "narrator.enabled";
    private const string LanguageKey = "narrator.language";
    private static readonly object Gate = new();
    private static readonly Dictionary<string, DateTimeOffset> LastByCategory = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> GenerationByCategory = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    public static bool Enabled
    {
        get => string.Equals(SettingsStore.Get(EnabledKey, "False"), "True", StringComparison.OrdinalIgnoreCase);
        set => SettingsStore.Set(EnabledKey, value.ToString());
    }

    public static NarratorLanguage Language
    {
        get => Enum.TryParse(SettingsStore.Get(LanguageKey, nameof(NarratorLanguage.English)), true, out NarratorLanguage language)
            ? language
            : NarratorLanguage.English;
        set => SettingsStore.Set(LanguageKey, value.ToString());
    }

    public static void Narrate(string en, string zh, string category = "general")
    {
        if (!Enabled || UniversalSettingsService.SchoolModeEnabled) return;
        string safeCategory = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim();
        int generation;
        lock (Gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (LastByCategory.TryGetValue(safeCategory, out DateTimeOffset last) && now - last < Cooldown)
                return;
            generation = GenerationByCategory.TryGetValue(safeCategory, out int previous) ? previous + 1 : 1;
            GenerationByCategory[safeCategory] = generation;
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(Debounce).ConfigureAwait(false);
            lock (Gate)
            {
                if (!Enabled || UniversalSettingsService.SchoolModeEnabled
                    || !GenerationByCategory.TryGetValue(safeCategory, out int current)
                    || current != generation)
                    return;
                LastByCategory[safeCategory] = DateTimeOffset.UtcNow;
            }

            string spokenEn = Style(en, FunnyLevelSettings.I.EnglishLevel, english: true);
            string spokenZh = Style(zh, FunnyLevelSettings.I.CantoneseLevel, english: false);
            string key = "narrator:" + safeCategory;
            switch (Language)
            {
                case NarratorLanguage.Cantonese:
                    AnnouncementService.I.EnqueueCoalesced(spokenZh, key);
                    break;
                case NarratorLanguage.Both:
                    AnnouncementService.I.EnqueueCoalesced(spokenEn + Environment.NewLine + spokenZh, key);
                    break;
                default:
                    AnnouncementService.I.EnqueueCoalesced(spokenEn, key);
                    break;
            }
        });
    }

    private static string Style(string value, int level, bool english)
    {
        if (string.IsNullOrWhiteSpace(value) || level <= 1) return value;
        return level >= 5
            ? english ? value + " Tiny notification, big responsibility." : value + " 小小通知，都有大大責任。"
            : value;
    }
}
