namespace DewEncryption.Core;

public sealed class DewDriveService
{
    public IReadOnlyList<string> CopyIntoFolder(string folder, IEnumerable<string> sourcePaths)
    {
        string destinationRoot = Path.GetFullPath(folder);
        Directory.CreateDirectory(destinationRoot);

        List<string> copied = [];
        foreach (string sourcePath in sourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            string source = Path.GetFullPath(sourcePath);
            if (File.Exists(source))
            {
                string target = AvailableTargetPath(destinationRoot, Path.GetFileName(source), isDirectory: false);
                File.Copy(source, target, overwrite: false);
                copied.Add(target);
                continue;
            }

            if (Directory.Exists(source))
            {
                if (IsSameOrDescendant(destinationRoot, source))
                {
                    throw new IOException("Choose a folder outside the Dew Drive folder.");
                }

                string target = AvailableTargetPath(destinationRoot, Path.GetFileName(source), isDirectory: true);
                CopyDirectory(source, target);
                copied.Add(target);
                continue;
            }

            throw new FileNotFoundException("Dew Drive source does not exist.", source);
        }

        return copied;
    }

    private static bool IsSameOrDescendant(string candidate, string ancestor)
    {
        string relative = Path.GetRelativePath(ancestor, candidate);
        return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
    }

    private static string AvailableTargetPath(string destinationRoot, string name, bool isDirectory)
    {
        string safeName = string.IsNullOrWhiteSpace(name) ? "item" : name;
        string candidate = Path.Combine(destinationRoot, safeName);
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        string stem = isDirectory ? safeName : Path.GetFileNameWithoutExtension(safeName);
        string extension = isDirectory ? string.Empty : Path.GetExtension(safeName);
        for (int index = 2; ; index++)
        {
            candidate = Path.Combine(destinationRoot, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? destination);
            File.Copy(file, target, overwrite: false);
        }
    }
}
