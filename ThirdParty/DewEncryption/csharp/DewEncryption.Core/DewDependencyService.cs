namespace DewEncryption.Core;

public static class DewDependencyService
{
    public static string? FindVeraCryptPath(string configuredPath = "")
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        string executableName = OperatingSystem.IsWindows() ? "VeraCrypt.exe" : "veracrypt";
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate = Path.Combine(directory, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string[] candidates = OperatingSystem.IsWindows()
            ?
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "VeraCrypt", "VeraCrypt.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "VeraCrypt", "VeraCrypt.exe"),
            ]
            :
            [
                "/usr/bin/veracrypt",
                "/usr/local/bin/veracrypt",
            ];

        return candidates.FirstOrDefault(File.Exists);
    }
}
