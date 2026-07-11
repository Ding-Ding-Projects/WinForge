namespace DewEncryption.Core;

public static class DewPathService
{
    public const string AppName = "Dew Encryption";
    public const string RepoDirName = ".dew-encryption-repo";
    public const string ArchiveDirName = "Dew Encryption Archives";
    public const string VeraCryptExtension = ".dew.hc";

    public static string ArchiveOutputDir(string source)
    {
        string fullPath = Path.GetFullPath(source);
        string basePath = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) ?? fullPath : fullPath;
        return Path.Combine(basePath, ArchiveDirName);
    }

    public static string RepoForSource(string source)
    {
        return Path.Combine(ArchiveOutputDir(source), RepoDirName);
    }

    public static string ContainerPathForSource(string source)
    {
        string fullPath = Path.GetFullPath(source);
        return Path.Combine(Path.GetDirectoryName(fullPath) ?? string.Empty, Path.GetFileName(fullPath) + VeraCryptExtension);
    }

    public static string SourcePathForContainer(string container)
    {
        string fullPath = Path.GetFullPath(container);
        string directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        string fileName = Path.GetFileName(fullPath);
        if (fileName.EndsWith(VeraCryptExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(directory, fileName[..^VeraCryptExtension.Length]);
        }

        if (string.Equals(Path.GetExtension(fileName), ".hc", StringComparison.OrdinalIgnoreCase))
        {
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName));
        }

        return Path.Combine(directory, fileName + ".decrypted");
    }

    public static string SelectedRoot(IEnumerable<string> paths)
    {
        string[] existing = paths.Select(Path.GetFullPath).Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        if (existing.Length == 0)
        {
            throw new InvalidOperationException("No existing files or folders were selected.");
        }

        if (existing.Length == 1)
        {
            return existing[0];
        }

        string[] comparablePaths = existing.Select(path => File.Exists(path) ? Path.GetDirectoryName(path) ?? path : path).ToArray();
        return Path.GetFullPath(CommonPath(comparablePaths));
    }

    private static string CommonPath(IReadOnlyList<string> paths)
    {
        string[][] splitPaths = paths.Select(path => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToArray();
        int shortest = splitPaths.Min(parts => parts.Length);
        List<string> common = [];
        for (int index = 0; index < shortest; index++)
        {
            string candidate = splitPaths[0][index];
            if (splitPaths.Any(parts => !string.Equals(parts[index], candidate, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            common.Add(candidate);
        }

        return string.Join(Path.DirectorySeparatorChar, common);
    }
}
