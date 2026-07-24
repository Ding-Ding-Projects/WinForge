namespace WinForge.Services;

internal sealed class Loc
{
    internal static Loc I { get; } = new();
    internal string Pick(string en, string zh) => en;
}
