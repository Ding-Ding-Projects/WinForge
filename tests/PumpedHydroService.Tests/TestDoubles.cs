namespace WinForge.Services;

// The linked simulator only needs localized status text and the published mint constant.
// Keeping these test doubles platform-neutral avoids loading the user settings/economy singleton.
public sealed class Loc
{
    public static Loc I { get; } = new();
    public string Pick(string en, string zh) => en;
}

public sealed class ReactorEconomyService
{
    public const double MintPerMWSecond = 0.00001;
}
