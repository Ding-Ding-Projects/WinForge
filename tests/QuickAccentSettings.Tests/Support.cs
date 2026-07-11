using System.Collections.Generic;

namespace WinForge.Services;

public static class SettingsStore
{
    public static string Get(string key, string fallback) => fallback;
    public static void Set(string key, string value) { }
}

public sealed record AccentSet(string Id);

public static class QuickAccentData
{
    public static IReadOnlyList<AccentSet> All { get; } = new[] { new AccentSet("Latin") };
    public static void InvalidateCache() { }
    public static bool IsLetterKey(int vk) => false;
    public static string[] GetCharacters(int vk, IReadOnlyCollection<string> sets) => Array.Empty<string>();
}

public sealed class QuickAccentPopup
{
    public static QuickAccentPopup Instance { get; } = new();
    public void EnsureStarted() { }
    public void Hide() { }
    public void Show(string[] characters, QuickAccentPosition position) { }
    public void Select(int index) { }
}
