using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace WinForge.Services;

/// <summary>一個 Dew 歷史項目嘅固定位置 · Stable paths for one Dew history selection.</summary>
public sealed record DewProjectContext(
    IReadOnlyList<string> Paths,
    string Root,
    string ArchiveDirectory,
    string RepositoryDirectory,
    IReadOnlyList<string>? HistoricalPaths = null,
    bool IsReadOnlyImport = false,
    bool RequiresExplicitRestoreTargetConfirmation = false)
{
    public IReadOnlyList<string> RestoreTargets => HistoricalPaths ?? Paths;
}

/// <summary>複製快照嘅摘要 · Summary of a managed snapshot copy.</summary>
public sealed record DewCopySummary(int Files, int Directories, int SkippedLinks);

/// <summary>
/// Dew 檔案歷史嘅純檔案系統核心 · Pure filesystem core for Dew-compatible file history.
/// It deliberately contains no WinUI or process dependencies so copy/restore safety can be tested headlessly.
/// </summary>
public static class DewSnapshotCore
{
    public const string ArchiveDirectoryName = "Dew Encryption Archives";
    public const string RepositoryDirectoryName = ".dew-encryption-repo";
    public const string WorkingTreeDirectoryName = "files";

    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>由一個或多個現有來源建立 Dew 相容項目位置 · Resolve a compatible Dew project.</summary>
    public static DewProjectContext CreateProject(IEnumerable<string> paths)
    {
        var normalized = NormalizeSelection(paths, requireExisting: true);
        var names = normalized.Select(SourceName).ToList();
        var duplicate = names.GroupBy(n => n, PathComparer).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidOperationException(
                $"Two selected items use the same top-level name ({duplicate.Key}). Choose them separately so one cannot overwrite the other.");

        var root = SelectionRoot(normalized);
        if (normalized.Count > 1)
        {
            var nested = normalized.FirstOrDefault(path =>
                !PathComparer.Equals(TrimEndingSeparator(Path.GetDirectoryName(path) ?? ""),
                    TrimEndingSeparator(root)));
            if (nested is not null)
                throw new InvalidOperationException(
                    "Grouped Dew selections must all be direct children of the same folder so their restore paths remain unambiguous after reopening history.");
        }
        var archiveDirectory = ArchiveDirectoryForRoot(root);
        return new DewProjectContext(
            normalized,
            root,
            archiveDirectory,
            Path.Combine(archiveDirectory, RepositoryDirectoryName));
    }

    /// <summary>正規化、去重，亦可保留已刪除路徑畀歷史快照 · Normalize and de-duplicate paths.</summary>
    public static IReadOnlyList<string> NormalizeSelection(IEnumerable<string> paths, bool requireExisting)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var result = new List<string>();
        var seen = new HashSet<string>(PathComparer);
        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var full = TrimEndingSeparator(Path.GetFullPath(raw.Trim().Trim('"')));
            var filesystemRoot = Path.GetPathRoot(full);
            if (filesystemRoot is not null && PathComparer.Equals(full, filesystemRoot))
                throw new InvalidOperationException("Filesystem-root selections are not supported. Choose a file or subfolder instead.");
            if (requireExisting && !File.Exists(full) && !Directory.Exists(full))
                throw new FileNotFoundException($"Selected path does not exist: {full}", full);
            if ((File.Exists(full) || Directory.Exists(full))
                && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"Reparse-point selections are not followed: {full}");
            if (IsUnsupportedEntryName(SourceName(full)))
                throw new InvalidOperationException($"This reserved Dew/Git metadata name cannot be selected: {full}");
            if (seen.Add(full)) result.Add(full);
        }

        if (result.Count == 0)
            throw new InvalidOperationException("Choose at least one existing file or folder.");
        return result;
    }

    /// <summary>選取項目共用嘅根 · Common root used for the adjacent archive directory.</summary>
    public static string SelectionRoot(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) throw new ArgumentException("No paths were supplied.", nameof(paths));
        if (paths.Count == 1) return paths[0];

        var containers = paths.Select(p => Directory.Exists(p) ? p : Path.GetDirectoryName(p) ?? p).ToList();
        var common = TrimEndingSeparator(Path.GetFullPath(containers[0]));
        while (containers.Any(p => !IsSameOrDescendant(p, common)))
        {
            var parent = Directory.GetParent(common)?.FullName;
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException("The selected paths do not share a filesystem root.");
            common = TrimEndingSeparator(parent);
        }
        return common;
    }

    public static string ArchiveDirectoryForRoot(string root)
    {
        var full = TrimEndingSeparator(Path.GetFullPath(root));
        var parent = File.Exists(full) ? Path.GetDirectoryName(full) : full;
        if (string.IsNullOrEmpty(parent))
            throw new InvalidOperationException($"Cannot resolve an archive directory for {root}.");
        return Path.Combine(parent, ArchiveDirectoryName);
    }

    public static string SourceName(string path)
    {
        var full = TrimEndingSeparator(Path.GetFullPath(path));
        var name = Path.GetFileName(full);
        if (!string.IsNullOrWhiteSpace(name)) return name;
        var root = Path.GetPathRoot(full)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ':');
        return string.IsNullOrWhiteSpace(root) ? "root" : root + "-drive";
    }

    /// <summary>
    /// 先喺倉庫外完整複製，再換入 files/ · Copy fully into staging, then swap the repo's files/ tree.
    /// A failed/locked copy leaves the previous working tree untouched.
    /// </summary>
    public static DewCopySummary ReplaceWorkingTree(DewProjectContext project, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        Directory.CreateDirectory(project.ArchiveDirectory);
        Directory.CreateDirectory(project.RepositoryDirectory);

        var staging = Path.Combine(project.ArchiveDirectory, $".dew-staging-{Guid.NewGuid():N}");
        var backup = Path.Combine(project.ArchiveDirectory, $".dew-files-backup-{Guid.NewGuid():N}");
        var work = Path.Combine(project.RepositoryDirectory, WorkingTreeDirectoryName);
        var stats = new MutableCopySummary();
        Directory.CreateDirectory(staging);

        try
        {
            foreach (var source in project.Paths)
            {
                ct.ThrowIfCancellationRequested();
                if (!EntryExistsNoFollow(source)) continue; // records a deletion
                CopyEntry(source, Path.Combine(staging, SourceName(source)), stats, ct,
                    excludedArchiveDirectory: project.ArchiveDirectory);
            }

            if (stats.SkippedLinks > 0)
                throw new InvalidOperationException(
                    $"Snapshot stopped because {stats.SkippedLinks} symbolic link or junction item(s) were found. Reparse points are never followed or silently omitted.");

            bool movedOld = false;
            try
            {
                if (Directory.Exists(work))
                {
                    Directory.Move(work, backup);
                    movedOld = true;
                }
                Directory.Move(staging, work);
                if (movedOld) DeleteDirectorySafe(backup);
            }
            catch
            {
                if (!Directory.Exists(work) && Directory.Exists(backup))
                    Directory.Move(backup, work);
                throw;
            }

            return new DewCopySummary(stats.Files, stats.Directories, stats.SkippedLinks);
        }
        finally
        {
            DeleteDirectorySafe(staging);
            // A failed rollback deliberately leaves backup data in place rather than deleting user data.
        }
    }

    /// <summary>
    /// 將一個已驗證嘅 staged 項目安全換入來源 · Replace one source from validated staging.
    /// The adjacent Dew archive directory is preserved, and failures roll the live source back.
    /// A null staged path represents a commit where the selected item did not exist.
    /// </summary>
    public static void RestoreSingleSelection(string? stagedPath, string sourcePath, string archiveDirectory,
        CancellationToken ct = default)
    {
        var source = TrimEndingSeparator(Path.GetFullPath(sourcePath));
        var archive = TrimEndingSeparator(Path.GetFullPath(archiveDirectory));
        Directory.CreateDirectory(archive);
        if (stagedPath is not null
            && (File.GetAttributes(stagedPath) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("A reparse-point snapshot cannot be restored.");
        ValidateTreeForRestore(source, archive, isStaged: false, ct);
        if (stagedPath is not null) ValidateTreeForRestore(stagedPath, excludedArchiveDirectory: null, isStaged: true, ct);

        if (Directory.Exists(source) && IsSameOrDescendant(archive, source))
        {
            RestoreRootDirectory(stagedPath, source, archive, ct);
            return;
        }

        var backupRoot = Path.Combine(archive, $".dew-restore-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupRoot);
        var backup = Path.Combine(backupRoot, "source");
        bool hadSource = EntryExistsNoFollow(source);
        try
        {
            ct.ThrowIfCancellationRequested();
            if (hadSource) MoveEntry(source, backup);
            if (stagedPath is not null)
            {
                var stats = new MutableCopySummary();
                CopyEntry(stagedPath, source, stats, ct, excludedArchiveDirectory: null);
            }
            DeleteDirectorySafe(backupRoot);
        }
        catch (Exception restoreError)
        {
            var recoveryErrors = new List<Exception>();
            try { DeleteEntry(source); } catch (Exception ex) { recoveryErrors.Add(ex); }
            if (hadSource && (File.Exists(backup) || Directory.Exists(backup)))
            {
                try { MoveEntry(backup, source); } catch (Exception ex) { recoveryErrors.Add(ex); }
            }
            if (recoveryErrors.Count > 0)
                throw new IOException(
                    $"Restore failed and rollback was incomplete. Original data remains under {backupRoot}.",
                    new AggregateException(new[] { restoreError }.Concat(recoveryErrors)));
            throw;
        }
        finally
        {
            if (!File.Exists(backup) && !Directory.Exists(backup)) DeleteDirectorySafe(backupRoot);
        }
    }

    /// <summary>
    /// Restore into a target that must remain absent. The validated content is first copied beside the target and
    /// then renamed without overwrite, so a concurrently-created file or folder is never replaced.
    /// </summary>
    public static void RestoreMissingSelection(string stagedPath, string sourcePath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stagedPath);
        var staged = TrimEndingSeparator(Path.GetFullPath(stagedPath));
        var source = TrimEndingSeparator(Path.GetFullPath(sourcePath));
        if (!File.Exists(staged) && !Directory.Exists(staged))
            throw new FileNotFoundException("The staged Dew source does not exist.", staged);
        if (EntryExistsNoFollow(source))
            throw new IOException("The restore target now exists and was left untouched.");

        ValidateTreeForRestore(staged, excludedArchiveDirectory: null, isStaged: true, ct);
        var parent = Path.GetDirectoryName(source)
            ?? throw new InvalidOperationException("The restore target has no parent directory.");
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException($"The restore target parent does not exist: {parent}");
        if ((File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"The restore target parent cannot be a symbolic link or junction: {parent}");

        var temporary = Path.Combine(parent, $".winforge-dew-restore-{Guid.NewGuid():N}");
        try
        {
            var stats = new MutableCopySummary();
            CopyEntry(staged, temporary, stats, ct, excludedArchiveDirectory: null);
            ct.ThrowIfCancellationRequested();
            try
            {
                if (Directory.Exists(temporary)) Directory.Move(temporary, source);
                else File.Move(temporary, source, overwrite: false);
            }
            catch (IOException ex) when (EntryExistsNoFollow(source))
            {
                throw new IOException("The restore target appeared during restore and was left untouched.", ex);
            }
        }
        finally { DeleteEntry(temporary); }
    }

    public static bool IsSameOrDescendant(string candidate, string parent)
    {
        var child = TrimEndingSeparator(Path.GetFullPath(candidate));
        var ancestor = TrimEndingSeparator(Path.GetFullPath(parent));
        if (PathComparer.Equals(child, ancestor)) return true;
        var prefix = Path.EndsInDirectorySeparator(ancestor)
            ? ancestor
            : ancestor + Path.DirectorySeparatorChar;
        return child.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Check the directory entry itself, including a dangling symbolic link or junction.</summary>
    public static bool EntryExistsNoFollow(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static void RestoreRootDirectory(string? stagedPath, string source, string archive, CancellationToken ct)
    {
        var backup = Path.Combine(archive, $".dew-restore-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backup);
        var movedNames = new List<string>();
        var insertedNames = new List<string>();
        try
        {
            foreach (var child in Directory.EnumerateFileSystemEntries(source).ToList())
            {
                ct.ThrowIfCancellationRequested();
                if (PathComparer.Equals(TrimEndingSeparator(child), archive)) continue;
                var name = Path.GetFileName(child);
                MoveEntry(child, Path.Combine(backup, name));
                movedNames.Add(name);
            }

            if (stagedPath is not null)
            {
                if (!Directory.Exists(stagedPath))
                    throw new InvalidDataException("The selected folder is a file in this commit.");
                foreach (var child in Directory.EnumerateFileSystemEntries(stagedPath))
                {
                    ct.ThrowIfCancellationRequested();
                    var name = Path.GetFileName(child);
                    var destination = Path.Combine(source, name);
                    insertedNames.Add(name); // record before copy so a partial/cancelled copy is rolled back
                    var stats = new MutableCopySummary();
                    CopyEntry(child, destination, stats, ct, excludedArchiveDirectory: null);
                }
            }
            DeleteDirectorySafe(backup);
        }
        catch (Exception restoreError)
        {
            var recoveryErrors = new List<Exception>();
            foreach (var name in insertedNames)
            {
                try { DeleteEntry(Path.Combine(source, name)); } catch (Exception ex) { recoveryErrors.Add(ex); }
            }
            foreach (var name in movedNames)
            {
                var saved = Path.Combine(backup, name);
                if (!File.Exists(saved) && !Directory.Exists(saved)) continue;
                try { MoveEntry(saved, Path.Combine(source, name)); } catch (Exception ex) { recoveryErrors.Add(ex); }
            }
            if (recoveryErrors.Count > 0)
                throw new IOException(
                    $"Restore failed and rollback was incomplete. Original data remains under {backup}.",
                    new AggregateException(new[] { restoreError }.Concat(recoveryErrors)));
            throw;
        }
        finally
        {
            if (Directory.Exists(backup) && !Directory.EnumerateFileSystemEntries(backup).Any())
                DeleteDirectorySafe(backup);
        }
    }

    private static void CopyEntry(string source, string destination, MutableCopySummary stats,
        CancellationToken ct, string? excludedArchiveDirectory)
    {
        ct.ThrowIfCancellationRequested();
        var attributes = File.GetAttributes(source);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            stats.SkippedLinks++;
            return;
        }

        if (Directory.Exists(source))
        {
            Directory.CreateDirectory(destination);
            stats.Directories++;
            foreach (var child in Directory.EnumerateFileSystemEntries(source))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(child);
                if (excludedArchiveDirectory is not null
                    && PathComparer.Equals(TrimEndingSeparator(child), TrimEndingSeparator(excludedArchiveDirectory)))
                    continue;
                if (IsUnsupportedEntryName(name))
                    throw new InvalidOperationException(
                        $"Snapshot stopped because unsupported nested metadata would be lost on restore: {child}");
                CopyEntry(child, Path.Combine(destination, name), stats, ct, excludedArchiveDirectory);
            }
            try { Directory.SetLastWriteTimeUtc(destination, Directory.GetLastWriteTimeUtc(source)); } catch { }
            return;
        }

        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        File.Copy(source, destination, overwrite: false);
        stats.Files++;
    }

    private static bool IsUnsupportedEntryName(string name) =>
        name.Equals(".git", StringComparison.OrdinalIgnoreCase)
        || name.Equals(RepositoryDirectoryName, StringComparison.OrdinalIgnoreCase)
        || name.Equals(ArchiveDirectoryName, StringComparison.OrdinalIgnoreCase);

    private static void ValidateTreeForRestore(string path, string? excludedArchiveDirectory,
        bool isStaged, CancellationToken ct)
    {
        FileAttributes attributes;
        try { attributes = File.GetAttributes(path); }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }
        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"Restore stopped because a symbolic link or junction would be lost: {path}");
        if (!Directory.Exists(path)) return;

        foreach (var child in Directory.EnumerateFileSystemEntries(path))
        {
            ct.ThrowIfCancellationRequested();
            if (excludedArchiveDirectory is not null
                && PathComparer.Equals(TrimEndingSeparator(child), TrimEndingSeparator(excludedArchiveDirectory)))
                continue;
            var name = Path.GetFileName(child);
            if (IsUnsupportedEntryName(name))
            {
                var kind = isStaged ? "staged snapshot" : "live source";
                throw new InvalidDataException(
                    $"Restore stopped because the {kind} contains unsupported nested metadata: {child}");
            }
            ValidateTreeForRestore(child, excludedArchiveDirectory, isStaged, ct);
        }
    }

    private static void MoveEntry(string source, string destination)
    {
        var parent = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination);
    }

    private static void DeleteEntry(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            return;
        }
        if (!Directory.Exists(path)) return;
        var attributes = File.GetAttributes(path);
        Directory.Delete(path, recursive: (attributes & FileAttributes.ReparsePoint) == 0);
    }

    private static void DeleteDirectorySafe(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return;
            var attributes = File.GetAttributes(path);
            Directory.Delete(path, recursive: (attributes & FileAttributes.ReparsePoint) == 0);
        }
        catch { }
    }

    private static string TrimEndingSeparator(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        if (root is not null && PathComparer.Equals(full, root)) return full;
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class MutableCopySummary
    {
        public int Files;
        public int Directories;
        public int SkippedLinks;
    }
}
