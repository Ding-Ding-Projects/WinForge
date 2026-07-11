namespace DewEncryption.Core;

public sealed record DewContainerProfile(
    string Name,
    string Path,
    string MountPath,
    DewThemeSettings Theme,
    string HookNotes)
{
    public string DisplayLabel
    {
        get
        {
            string label = string.IsNullOrWhiteSpace(Path) ? Name : $"{Name} — {Path}";
            return string.IsNullOrWhiteSpace(MountPath) ? label : $"{label} mounted at {MountPath}";
        }
    }
}

public sealed record DewThemeSettings(
    string FontFamily = "Segoe UI",
    string Background = "#0f172a",
    string Foreground = "#e5e7eb",
    string Accent = "#38bdf8",
    int FontSize = 10);
