using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SharpSevenZip;

namespace WinForge.Services;

public sealed record DewHistoryEntry(string Hash, string ShortHash, string Date, string Subject)
{
    public string DisplayTitle => string.IsNullOrWhiteSpace(Subject) ? ShortHash : Subject;
    public string DisplaySubtitle => $"{ShortHash} · {Date}";
}

public sealed record DewFileChange(string Status, string Path)
{
    public string Display => $"{Status}  {Path}";
}

public sealed record DewCommitDetails(string Summary, IReadOnlyList<DewFileChange> Changes);

public sealed record DewSnapshotResult(
    string Commit,
    bool Changed,
    DewCopySummary Copy,
    string RepositoryDirectory);

public sealed record DewArchiveResult(
    string ArchivePath,
    DewSnapshotResult Snapshot,
    bool Encrypted);

public sealed class DewOperationException : Exception
{
    public DewOperationException(string message) : base(message) { }
    public DewOperationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Dew 加密／歷史引擎 · Native Dew Encryption/history engine.
/// Compatible layout: Dew Encryption Archives/.dew-encryption-repo/files/&lt;source-name&gt;.
/// Git is invoked with ProcessStartInfo.ArgumentList; encrypted archives use the installed 7z.dll in-process.
/// Secrets never enter a command line, environment variable, log, result object or persisted setting.
/// </summary>
public static class DewEncryptionService
{
    private static readonly SemaphoreSlim OperationGate = new(1, 1);
    private static readonly object SevenZipLibraryLock = new();
    private static readonly Regex CommitPattern = new("^[0-9a-fA-F]{4,64}$", RegexOptions.Compiled);
    private const int MaxImportedCommitCount = 50_000;
    private const int MaxImportedSourceNames = 10_000;
    private const int MaxImportedTreeEntries = 250_000;
    private const int MaxImportedTreeDepth = 256;
    private const int MaxToolOutputCharacters = 16 * 1024 * 1024;

    public static bool RepositoryExists(DewProjectContext project) =>
        Directory.Exists(Path.Combine(project.RepositoryDirectory, ".git"));

    /// <summary>
    /// 開現有 Dew 倉庫，並由歷史推斷原本來源（包括已刪除檔案）· Open an existing compatible
    /// repository and infer its original source targets from all commits, including deleted files.
    /// </summary>
    public static async Task<DewProjectContext> OpenExistingProjectAsync(string selectedDirectory,
        CancellationToken ct = default)
    {
        var selected = Path.GetFullPath(selectedDirectory);
        EnsureNoReparsePoint(selected, "Selected Dew path");
        string repository;
        if (Directory.Exists(Path.Combine(selected, ".git"))
            && Path.GetFileName(selected).Equals(DewSnapshotCore.RepositoryDirectoryName, StringComparison.OrdinalIgnoreCase))
            repository = selected;
        else if (Directory.Exists(Path.Combine(selected, DewSnapshotCore.RepositoryDirectoryName, ".git")))
            repository = Path.Combine(selected, DewSnapshotCore.RepositoryDirectoryName);
        else
            throw new DewOperationException(
                $"Choose either {DewSnapshotCore.RepositoryDirectoryName} or its {DewSnapshotCore.ArchiveDirectoryName} parent folder.");

        await ValidateRepositorySafetyAsync(repository, ct);

        var repositoryParent = Directory.GetParent(repository)?.FullName
            ?? throw new DewOperationException("The selected Dew repository has no parent directory.");
        EnsureNoReparsePoint(repositoryParent, "Dew repository parent directory");
        bool standardLayout = Path.GetFileName(repositoryParent)
            .Equals(DewSnapshotCore.ArchiveDirectoryName, StringComparison.OrdinalIgnoreCase);
        var archiveDirectory = repositoryParent;
        var sourceBase = standardLayout
            ? Directory.GetParent(archiveDirectory)?.FullName
                ?? throw new DewOperationException("The selected Dew archive directory has no source parent.")
            : repositoryParent;
        EnsureNoReparsePoint(sourceBase, "Dew restore base directory");

        var commitCountResult = await GitAsync(repository, ["rev-list", "--count", "HEAD"], ct);
        if (commitCountResult.ExitCode == 0)
        {
            if (!int.TryParse(commitCountResult.StdOut.Trim(), out var commitCount)
                || commitCount < 0 || commitCount > MaxImportedCommitCount)
                throw new DewOperationException(
                    $"This Dew repository exceeds the safe import limit of {MaxImportedCommitCount:N0} commits.");
        }

        var namesResult = await GitAsync(repository,
            ["log", "--name-only", "-z", "--pretty=format:", "--", DewSnapshotCore.WorkingTreeDirectoryName], ct);
        string historicalOutput;
        if (namesResult.ExitCode == 0) historicalOutput = namesResult.StdOut;
        else
        {
            var head = await GitAsync(repository, ["rev-parse", "--verify", "HEAD"], ct);
            if (head.ExitCode == 0)
                throw new DewOperationException(ToolFailure("Dew source names could not be discovered from Git history.", namesResult));
            historicalOutput = ""; // valid upstream empty-folder repo with no first commit
        }
        var historicalNames = historicalOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\r', '\n').Replace('\\', '/'))
            .Where(p => p.StartsWith(DewSnapshotCore.WorkingTreeDirectoryName + "/", StringComparison.Ordinal))
            .Select(p => p.Split('/', 3)[1])
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (historicalNames.Count > MaxImportedSourceNames)
            throw new DewOperationException(
                $"This Dew repository exceeds the safe import limit of {MaxImportedSourceNames:N0} source names.");
        var workingDirectory = Path.Combine(repository, DewSnapshotCore.WorkingTreeDirectoryName);
        var currentNames = new List<string>();
        if (Directory.Exists(workingDirectory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(workingDirectory))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(entry);
                if (!string.IsNullOrWhiteSpace(name)
                    && !currentNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                    currentNames.Add(name);
                if (currentNames.Count > MaxImportedSourceNames)
                    throw new DewOperationException(
                        $"This Dew repository exceeds the safe import limit of {MaxImportedSourceNames:N0} source names.");
            }
            currentNames.Sort(StringComparer.OrdinalIgnoreCase);
        }
        var allNames = historicalNames.Concat(currentNames).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (allNames.Count == 0)
            throw new DewOperationException("No restorable source names were found in this Dew repository.");

        var targetByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in allNames)
        {
            ct.ThrowIfCancellationRequested();
            ValidateSourceName(name);
            var member = $"{DewSnapshotCore.WorkingTreeDirectoryName}/{name}";
            bool wasDirectory = false;
            var firstPresence = await GitAsync(repository,
                ["log", "--diff-filter=AMR", "--format=%H", "--max-count=1", "--", member], ct);
            if (firstPresence.ExitCode == 0 && !string.IsNullOrWhiteSpace(firstPresence.StdOut))
            {
                var revision = firstPresence.StdOut.Replace("\r", "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (revision is not null)
                {
                    var type = await GitAsync(repository, ["cat-file", "-t", $"{revision.Trim()}:{member}"], ct);
                    wasDirectory = type.ExitCode == 0
                        && type.StdOut.Trim().Equals("tree", StringComparison.Ordinal);
                }
            }
            if (!wasDirectory)
                wasDirectory = Directory.Exists(Path.Combine(workingDirectory, name));
            var target = standardLayout && allNames.Count == 1 && wasDirectory
                && Path.GetFileName(sourceBase).Equals(name, StringComparison.OrdinalIgnoreCase)
                ? sourceBase
                : Path.Combine(sourceBase, name);
            var fullTarget = Path.GetFullPath(target);
            if (!DewSnapshotCore.IsSameOrDescendant(fullTarget, sourceBase))
                throw new DewOperationException($"Dew history contains an unsafe source target: {name}");
            targetByName[name] = fullTarget;
        }

        var currentPaths = currentNames.Select(n => targetByName[n]).ToList();
        var historicalPaths = allNames.Select(n => targetByName[n]).ToList();
        if (currentPaths.Count == 0) currentPaths.AddRange(historicalPaths);
        var root = currentPaths.Count == 1 ? currentPaths[0] : sourceBase;
        return new DewProjectContext(currentPaths, root, archiveDirectory, repository, historicalPaths,
            IsReadOnlyImport: !standardLayout,
            RequiresExplicitRestoreTargetConfirmation: allNames.Count > 1);
    }

    public static IReadOnlyList<string> ExistingWorkingTreeNames(DewProjectContext project)
    {
        var work = Path.Combine(project.RepositoryDirectory, DewSnapshotCore.WorkingTreeDirectoryName);
        if (!Directory.Exists(work)) return Array.Empty<string>();
        try
        {
            return Directory.EnumerateFileSystemEntries(work)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n!)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    public static bool HasSelectionConflict(DewProjectContext project)
    {
        var existing = ExistingWorkingTreeNames(project);
        if (existing.Count == 0) return false;
        var requested = project.Paths.Select(DewSnapshotCore.SourceName)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        return !existing.SequenceEqual(requested, StringComparer.OrdinalIgnoreCase);
    }

    public static async Task<bool> IsGitAvailableAsync(CancellationToken ct = default)
    {
        var result = await RunToolAsync(GitExecutable(), null, ["--version"], null, ct);
        return result.ExitCode == 0 && result.Output.Contains("git", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<bool> IsSevenZipAvailableAsync(CancellationToken ct = default)
    {
        var executable = FindSevenZip();
        if (executable is null) return false;
        var result = await RunToolAsync(executable, null, ["i"], null, ct);
        return result.ExitCode == 0;
    }

    /// <summary>影低來源內容；檔案複製完成先換入 Git working tree · Take one safe snapshot.</summary>
    public static async Task<DewSnapshotResult> TakeSnapshotAsync(DewProjectContext project,
        string? label = null, bool allowSelectionReplacement = false, CancellationToken ct = default)
    {
        if (project.IsReadOnlyImport)
            throw new DewOperationException(
                "This repository was opened from an extracted/nonstandard layout and is history/restore-only. Move it under Dew Encryption Archives before taking new compatible snapshots.");
        await OperationGate.WaitAsync(ct);
        try { return await TakeSnapshotLockedAsync(project, label, allowSelectionReplacement, ct); }
        finally { OperationGate.Release(); }
    }

    /// <summary>影快照再輸出 Dew 相容 7z；有密碼時同時加密檔名 · Snapshot then export a compatible 7z.</summary>
    public static async Task<DewArchiveResult> CreateArchiveAsync(DewProjectContext project, string password,
        string? label = null, bool allowSelectionReplacement = false, CancellationToken ct = default)
    {
        if (project.IsReadOnlyImport)
            throw new DewOperationException(
                "This extracted/nonstandard repository is history/restore-only and cannot publish a new compatible archive until it is adopted into Dew Encryption Archives.");
        password ??= string.Empty;
        ValidatePassword(password, requireStrong: password.Length > 0);
        await OperationGate.WaitAsync(ct);
        string? archivePath = null;
        string? temporaryArchive = null;
        try
        {
            var library = FindSevenZipLibrary()
                ?? throw new DewOperationException("7-Zip's native library was not found in a trusted install location. Install 7-Zip before exporting an archive.");
            var snapshot = await TakeSnapshotLockedAsync(project, label, allowSelectionReplacement, ct);
            Directory.CreateDirectory(project.ArchiveDirectory);
            archivePath = NextArchivePath(project.ArchiveDirectory);
            temporaryArchive = Path.Combine(project.ArchiveDirectory, $".dew-export-{Guid.NewGuid():N}.7z");

            await CreateArchiveWithLibraryAsync(library, project.RepositoryDirectory, temporaryArchive, password, ct);
            if (!File.Exists(temporaryArchive))
                throw new DewOperationException("7-Zip did not create the archive.");

            await TestArchiveWithLibraryAsync(library, temporaryArchive, password, ct);
            File.Move(temporaryArchive, archivePath, overwrite: false);
            temporaryArchive = null;
            return new DewArchiveResult(archivePath, snapshot, password.Length > 0);
        }
        catch
        {
            if (temporaryArchive is not null) TryDeleteFile(temporaryArchive);
            throw;
        }
        finally { OperationGate.Release(); }
    }

    /// <summary>測試現有 Dew 7z 完整性，亦可用密碼 · Test an existing archive without extracting it.</summary>
    public static async Task TestArchiveAsync(string archivePath, string password, CancellationToken ct = default)
    {
        password ??= string.Empty;
        ValidatePassword(password, requireStrong: false);
        var library = FindSevenZipLibrary()
            ?? throw new DewOperationException("7-Zip's native library was not found in a trusted install location.");
        await TestArchiveWithLibraryAsync(library, Path.GetFullPath(archivePath), password, ct);
    }

    public static async Task<IReadOnlyList<DewHistoryEntry>> ListHistoryAsync(DewProjectContext project,
        int limit = 100, CancellationToken ct = default)
    {
        if (!RepositoryExists(project)) return Array.Empty<DewHistoryEntry>();
        await ValidateRepositorySafetyAsync(project.RepositoryDirectory, ct);
        limit = Math.Clamp(limit, 1, 500);
        var result = await GitAsync(project.RepositoryDirectory,
            ["log", $"--max-count={limit}", "--date=iso-local", "--pretty=format:%H%x09%h%x09%ad%x09%s"], ct);
        if (result.ExitCode != 0)
        {
            var head = await GitAsync(project.RepositoryDirectory, ["rev-parse", "--verify", "HEAD"], ct);
            if (head.ExitCode != 0) return Array.Empty<DewHistoryEntry>();
            throw new DewOperationException(ToolFailure("Git history could not be read.", result));
        }

        var entries = new List<DewHistoryEntry>();
        foreach (var line in result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 4);
            if (parts.Length == 4) entries.Add(new DewHistoryEntry(parts[0], parts[1], parts[2], parts[3]));
        }
        return entries;
    }

    public static async Task<DewCommitDetails> GetCommitDetailsAsync(DewProjectContext project, string commit,
        CancellationToken ct = default)
    {
        ValidateCommit(commit);
        if (!RepositoryExists(project)) throw new DewOperationException("No Dew history repository exists for this selection.");
        await ValidateRepositorySafetyAsync(project.RepositoryDirectory, ct);
        var summary = await GitAsync(project.RepositoryDirectory,
            ["show", "--no-patch", "--date=iso-local", "--pretty=format:%H%n%ad%n%s%n%b", commit], ct);
        if (summary.ExitCode != 0)
            throw new DewOperationException(ToolFailure("The selected commit could not be read.", summary));
        var changed = await GitAsync(project.RepositoryDirectory,
            ["show", "--name-status", "--format=", commit], ct);
        if (changed.ExitCode != 0)
            throw new DewOperationException(ToolFailure("The commit file list could not be read.", changed));

        var rows = new List<DewFileChange>();
        foreach (var line in changed.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', 2);
            if (parts.Length == 2) rows.Add(new DewFileChange(parts[0], parts[1]));
        }
        return new DewCommitDetails(summary.StdOut.Trim(), rows);
    }

    /// <summary>
    /// 原位還原一個來源：先影安全快照，再由 git archive 解到私有 staging，最後 rollback-safe 換入。
    /// In-place restore for one selection, guarded by a safety snapshot and staged replacement.
    /// </summary>
    public static async Task<DewProjectContext> RestoreAsync(DewProjectContext project, string sourcePath, string commit,
        CancellationToken ct = default, bool confirmedInferredTarget = false)
    {
        ValidateCommit(commit);
        var source = Path.GetFullPath(sourcePath);
        EnsureNoReparsePoint(source, "Dew restore target");
        if (!project.RestoreTargets.Any(p => string.Equals(Path.GetFullPath(p), source, StringComparison.OrdinalIgnoreCase)))
            throw new DewOperationException("The restore target is not part of this Dew project.");
        if (project.RequiresExplicitRestoreTargetConfirmation && !confirmedInferredTarget)
            throw new DewOperationException(
                "Multi-source Dew history stores top-level names but not original absolute paths. Explicitly confirm the inferred restore target before restoring it.");
        bool isCurrentTarget = project.Paths.Any(p =>
            string.Equals(Path.GetFullPath(p), source, StringComparison.OrdinalIgnoreCase));
        if (project.IsReadOnlyImport && DewSnapshotCore.EntryExistsNoFollow(source))
            throw new DewOperationException(
                "A history/restore-only imported repository cannot overwrite existing live data because it cannot record a compatible safety snapshot. Choose an empty target or adopt the repository first.");
        if (!RepositoryExists(project)) throw new DewOperationException("No Dew history repository exists for this selection.");

        var safetyProject = project;
        if (!project.IsReadOnlyImport && !isCurrentTarget && DewSnapshotCore.EntryExistsNoFollow(source))
        {
            var safetyPaths = project.Paths.Append(source).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var duplicate = safetyPaths.Select(DewSnapshotCore.SourceName)
                .GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
                throw new DewOperationException(
                    $"The historical restore target shares the top-level name {duplicate.Key} with the current selection and cannot be safety-snapshotted into the same repository.");
            var restoreTargets = project.RestoreTargets.Append(source)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            safetyProject = new DewProjectContext(safetyPaths, project.Root, project.ArchiveDirectory,
                project.RepositoryDirectory, restoreTargets, project.IsReadOnlyImport,
                project.RequiresExplicitRestoreTargetConfirmation);
        }

        await OperationGate.WaitAsync(ct);
        var stagingRoot = Path.Combine(Path.GetTempPath(), $"winforge-dew-restore-{Guid.NewGuid():N}");
        try
        {
            if (HasSelectionConflict(project))
                throw new DewOperationException(
                    "This adjacent Dew repository belongs to a different selection. Take a confirmed snapshot for the current selection before restoring it.");
            await EnsureRepositoryAsync(project.RepositoryDirectory, ct);
            var validCommit = await GitAsync(project.RepositoryDirectory,
                ["rev-parse", "--verify", $"{commit}^{{commit}}"], ct);
            if (validCommit.ExitCode != 0)
                throw new DewOperationException(ToolFailure("The selected commit does not exist.", validCommit));

            Directory.CreateDirectory(stagingRoot);
            var sourceName = DewSnapshotCore.SourceName(source);
            var member = $"{DewSnapshotCore.WorkingTreeDirectoryName}/{sourceName}";
            var exists = await GitAsync(project.RepositoryDirectory,
                ["ls-tree", "--name-only", commit, "--", member], ct);
            if (exists.ExitCode != 0)
                throw new DewOperationException(ToolFailure("Git could not inspect the selected snapshot.", exists));
            string? stagedPath = null;
            if (!string.IsNullOrWhiteSpace(exists.StdOut))
            {
                var zipPath = Path.Combine(stagingRoot, "restore.zip");
                var archive = await GitAsync(project.RepositoryDirectory,
                    ["archive", "--format=zip", $"--output={zipPath}", commit, "--", member], ct);
                if (archive.ExitCode != 0 || !File.Exists(zipPath))
                    throw new DewOperationException(ToolFailure("Git could not stage the selected snapshot.", archive));
                ExtractZipSafely(zipPath, stagingRoot);
                stagedPath = Path.Combine(stagingRoot, DewSnapshotCore.WorkingTreeDirectoryName, sourceName);
                if (!File.Exists(stagedPath) && !Directory.Exists(stagedPath))
                    throw new DewOperationException("The staged snapshot did not contain the selected source.");
            }
            if (!project.IsReadOnlyImport)
            {
                await TakeSnapshotLockedAsync(safetyProject,
                    $"Dew safety snapshot before restore {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    allowSelectionReplacement: !ReferenceEquals(safetyProject, project), ct);
            }

            if (project.IsReadOnlyImport)
            {
                if (stagedPath is not null)
                    await Task.Run(() => DewSnapshotCore.RestoreMissingSelection(stagedPath, source, ct), ct);
                else
                    ct.ThrowIfCancellationRequested(); // the historical deletion is already satisfied by an absent target
            }
            else
                await Task.Run(() => DewSnapshotCore.RestoreSingleSelection(
                    stagedPath, source, project.ArchiveDirectory, ct), ct);
            var postRestorePaths = safetyProject.Paths.Append(source)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var postRestoreTargets = safetyProject.RestoreTargets.Append(source)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return new DewProjectContext(postRestorePaths, safetyProject.Root,
                safetyProject.ArchiveDirectory, safetyProject.RepositoryDirectory, postRestoreTargets,
                safetyProject.IsReadOnlyImport, safetyProject.RequiresExplicitRestoreTargetConfirmation);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
            OperationGate.Release();
        }
    }

    public static async Task VerifyRepositoryAsync(DewProjectContext project, CancellationToken ct = default)
    {
        if (!RepositoryExists(project)) throw new DewOperationException("No Dew history repository exists for this selection.");
        await ValidateRepositorySafetyAsync(project.RepositoryDirectory, ct);
        var result = await GitAsync(project.RepositoryDirectory, ["fsck", "--full", "--strict"], ct);
        if (result.ExitCode != 0)
            throw new DewOperationException(ToolFailure("Git repository integrity check failed.", result));
    }

    public static string? FindSevenZip()
    {
        var candidates = new List<string?>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\7-Zip");
            if (key?.GetValue("Path") is string path) candidates.Add(Path.Combine(path, "7z.exe"));
        }
        catch { }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "7-Zip", "7z.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "7-Zip", "7z.exe"));
        return candidates.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p) && Path.IsPathFullyQualified(p) && File.Exists(p));
    }

    private static string? FindSevenZipLibrary()
    {
        var executable = FindSevenZip();
        if (executable is null) return null;
        var library = Path.Combine(Path.GetDirectoryName(executable)!, "7z.dll");
        return Path.IsPathFullyQualified(library) && File.Exists(library) ? library : null;
    }

    private static async Task<DewSnapshotResult> TakeSnapshotLockedAsync(DewProjectContext project,
        string? label, bool allowSelectionReplacement, CancellationToken ct)
    {
        if (!allowSelectionReplacement && HasSelectionConflict(project))
            throw new DewOperationException(
                "This adjacent Dew repository contains a different selection. Explicit confirmation is required before replacing its working tree.");
        await EnsureRepositoryAsync(project.RepositoryDirectory, ct);
        var copy = await Task.Run(() => DewSnapshotCore.ReplaceWorkingTree(project, ct), ct);
        var add = await GitAsync(project.RepositoryDirectory,
            ["add", "--sparse", "-f", "-A", "--", DewSnapshotCore.WorkingTreeDirectoryName], ct);
        if (add.ExitCode != 0) throw new DewOperationException(ToolFailure("Git could not stage the snapshot.", add));
        var staged = await GitAsync(project.RepositoryDirectory,
            ["diff", "--cached", "--name-only", "-z"], ct);
        if (staged.ExitCode != 0)
            throw new DewOperationException(ToolFailure("Git could not inspect the staged snapshot.", staged));
        var outside = staged.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(path => !path.Replace('\\', '/').StartsWith(
                DewSnapshotCore.WorkingTreeDirectoryName + "/", StringComparison.Ordinal));
        if (outside is not null)
            throw new DewOperationException(
                $"The Dew repository already has a staged path outside its managed files tree ({outside}). Unstage it before taking a snapshot.");

        var status = await GitAsync(project.RepositoryDirectory,
            ["status", "--porcelain", "--", DewSnapshotCore.WorkingTreeDirectoryName], ct);
        if (status.ExitCode != 0) throw new DewOperationException(ToolFailure("Git status failed.", status));

        bool changed = !string.IsNullOrWhiteSpace(status.StdOut);
        if (changed)
        {
            var message = string.IsNullOrWhiteSpace(label)
                ? $"Dew snapshot {DateTime.Now:yyyy-MM-dd HH:mm:ss}"
                : label.Replace('\r', ' ').Replace('\n', ' ');
            var commit = await GitAsync(project.RepositoryDirectory, ["commit", "-m", message], ct);
            if (commit.ExitCode != 0) throw new DewOperationException(ToolFailure("Git could not commit the snapshot.", commit));
        }

        var head = await GitAsync(project.RepositoryDirectory, ["rev-parse", "--short", "HEAD"], ct);
        if (head.ExitCode != 0 || string.IsNullOrWhiteSpace(head.StdOut))
        {
            if (!changed)
                return new DewSnapshotResult("", Changed: false, copy, project.RepositoryDirectory);
            throw new DewOperationException(ToolFailure("The snapshot repository has no readable commit.", head));
        }
        return new DewSnapshotResult(head.StdOut.Trim(), changed, copy, project.RepositoryDirectory);
    }

    private static async Task EnsureRepositoryAsync(string repository, CancellationToken ct)
    {
        EnsureNoReparsePoint(Directory.GetParent(repository)?.FullName, "Dew archive directory");
        EnsureNoReparsePoint(repository, "Dew repository directory");
        Directory.CreateDirectory(repository);
        if (!Directory.Exists(Path.Combine(repository, ".git")))
        {
            var init = await GitAsync(repository, ["init"], ct);
            if (init.ExitCode != 0) throw new DewOperationException(ToolFailure("Git could not initialize Dew history.", init));
        }
        await ValidateRepositorySafetyAsync(repository, ct);

        // Highest-precedence repository attributes keep snapshots byte-oriented: no CRLF normalization,
        // working-tree encoding, ident expansion, or user-defined clean/smudge filters.
        var infoDirectory = Path.Combine(repository, ".git", "info");
        EnsureNoReparsePoint(infoDirectory, "Git info directory");
        Directory.CreateDirectory(infoDirectory);
        var attributesPath = Path.Combine(infoDirectory, "attributes");
        EnsureNoReparsePoint(attributesPath, "Git info attributes file");
        await WriteTrustedAttributesAsync(attributesPath, ct);
        var existingName = await GitAsync(repository, ["config", "--local", "--get", "user.name"], ct);
        if (existingName.ExitCode == 1)
        {
            var setName = await GitAsync(repository, ["config", "user.name", "Dew Encryption"], ct);
            if (setName.ExitCode != 0)
                throw new DewOperationException(ToolFailure("Git could not configure the local Dew history name.", setName));
        }
        else if (existingName.ExitCode != 0)
            throw new DewOperationException(ToolFailure("Git could not read the local Dew history name.", existingName));

        var existingEmail = await GitAsync(repository, ["config", "--local", "--get", "user.email"], ct);
        if (existingEmail.ExitCode == 1)
        {
            var setEmail = await GitAsync(repository, ["config", "user.email", "dew-encryption@local"], ct);
            if (setEmail.ExitCode != 0)
                throw new DewOperationException(ToolFailure("Git could not configure the local Dew history email.", setEmail));
        }
        else if (existingEmail.ExitCode != 0)
            throw new DewOperationException(ToolFailure("Git could not read the local Dew history email.", existingEmail));
    }

    private static async Task ValidateRepositorySafetyAsync(string repository, CancellationToken ct)
    {
        EnsureNoReparsePoint(repository, "Dew repository directory");
        var gitDirectory = Path.Combine(repository, ".git");
        if (!Directory.Exists(gitDirectory))
            throw new DewOperationException("The selected Dew path is not a normal Git working repository.");
        EnsureNoReparseTree(repository, "Dew repository", ct);

        var keys = await GitAsync(repository, ["config", "--local", "--no-includes", "--name-only", "--list"], ct);
        if (keys.ExitCode != 0)
            throw new DewOperationException(ToolFailure("Git configuration safety check failed.", keys));
        var allowedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "core.repositoryformatversion",
            "core.filemode",
            "core.bare",
            "core.logallrefupdates",
            "core.symlinks",
            "core.ignorecase",
            "core.precomposeunicode",
            "extensions.objectformat",
            "extensions.compatobjectformat",
            "user.name",
            "user.email",
        };
        var dangerous = keys.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim().ToLowerInvariant())
            .Where(k => !allowedKeys.Contains(k))
            .ToList();
        if (dangerous.Count > 0)
            throw new DewOperationException(
                $"The discovered Dew repository contains unsupported local Git configuration and was not modified: {string.Join(", ", dangerous)}");

        await ValidateLocalConfigValueAsync(repository, "core.precomposeunicode",
            value => value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("false", StringComparison.OrdinalIgnoreCase), ct);
        await ValidateLocalConfigValueAsync(repository, "extensions.objectformat",
            value => value.Equals("sha1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("sha256", StringComparison.OrdinalIgnoreCase), ct);
        await ValidateLocalConfigValueAsync(repository, "extensions.compatobjectformat",
            value => value.Equals("sha1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("sha256", StringComparison.OrdinalIgnoreCase), ct);

        var bare = await GitAsync(repository, ["config", "--local", "--get", "core.bare"], ct);
        if (bare.ExitCode == 0 && !bare.StdOut.Trim().Equals("false", StringComparison.OrdinalIgnoreCase))
            throw new DewOperationException("Bare or redirected Git repositories cannot be used for Dew history.");
        if (bare.ExitCode is not 0 and not 1)
            throw new DewOperationException(ToolFailure("Git bare-repository check failed.", bare));

        var top = await GitAsync(repository, ["rev-parse", "--show-toplevel"], ct);
        if (top.ExitCode != 0
            || !Path.GetFullPath(top.StdOut.Trim()).Equals(Path.GetFullPath(repository), StringComparison.OrdinalIgnoreCase))
            throw new DewOperationException("The Git working-tree root does not match the selected Dew repository.");

        var specialIndex = await GitAsync(repository,
            ["ls-files", "-v", "-z", "--", DewSnapshotCore.WorkingTreeDirectoryName], ct);
        if (specialIndex.ExitCode != 0)
            throw new DewOperationException(ToolFailure("Git index safety check failed.", specialIndex));
        foreach (var entry in specialIndex.StdOut.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.Length > 1 && (char.IsLower(entry[0]) || entry[0] == 'S'))
                throw new DewOperationException(
                    "The Dew repository has skip-worktree or assume-unchanged index entries. Clear those flags before importing it.");
        }
    }

    private static async Task ValidateLocalConfigValueAsync(string repository, string key,
        Func<string, bool> isAllowed, CancellationToken ct)
    {
        var result = await GitAsync(repository, ["config", "--local", "--get-all", key], ct);
        if (result.ExitCode == 1) return;
        if (result.ExitCode != 0)
            throw new DewOperationException(ToolFailure($"Git configuration value {key} could not be read.", result));
        var values = result.StdOut.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length == 0 || values.Any(value => !isAllowed(value.Trim())))
            throw new DewOperationException($"The discovered Dew repository has an unsupported value for {key}.");
    }

    private static Task<ToolResult> GitAsync(string repository, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var hardened = new List<string>
        {
            "-c", "core.hooksPath=NUL",
            "-c", "commit.gpgSign=false",
            "-c", "log.showSignature=false",
            "-c", "core.fsmonitor=false",
            "-c", "core.autocrlf=false",
            "-c", "core.safecrlf=false",
            "-c", "core.quotepath=false",
            "-c", "core.excludesFile=NUL",
        };
        hardened.AddRange(arguments);
        return RunToolAsync(GitExecutable(), repository, hardened, null, ct);
    }

    private static string GitExecutable() => ShellRunner.ResolveExe("git");

    private static string NextArchivePath(string directory)
    {
        var stem = $"dew-encryption-{DateTime.Now:yyyyMMdd-HHmmss}";
        var candidate = Path.Combine(directory, stem + ".7z");
        for (int i = 2; File.Exists(candidate); i++) candidate = Path.Combine(directory, $"{stem}-{i}.7z");
        return candidate;
    }

    private static void ValidatePassword(string password, bool requireStrong)
    {
        if (password.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new DewOperationException("Archive passwords cannot contain NUL or line-break characters.");
        if (requireStrong && password.Length < 12)
            throw new DewOperationException("Use at least 12 characters for an encrypted archive password.");
    }

    private static void ValidateCommit(string commit)
    {
        if (string.IsNullOrWhiteSpace(commit) || !CommitPattern.IsMatch(commit))
            throw new DewOperationException("The selected Git commit identifier is invalid.");
    }

    private static void ValidateSourceName(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || trimmed is "." or ".."
            || !string.Equals(trimmed, name, StringComparison.Ordinal)
            || name.EndsWith('.') || name.EndsWith(' ')
            || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || name.Contains(Path.DirectorySeparatorChar) || name.Contains(Path.AltDirectorySeparatorChar)
            || Path.IsPathFullyQualified(name))
            throw new DewOperationException($"Dew history contains an invalid Windows source name: {name}");

        var device = Path.GetFileNameWithoutExtension(name).ToUpperInvariant();
        if (device is "CON" or "PRN" or "AUX" or "NUL" or "CLOCK$"
            || Regex.IsMatch(device, "^(COM|LPT)[1-9]$", RegexOptions.CultureInvariant))
            throw new DewOperationException($"Dew history contains a reserved Windows source name: {name}");
        if (name.Equals(".git", StringComparison.OrdinalIgnoreCase)
            || name.Equals(DewSnapshotCore.RepositoryDirectoryName, StringComparison.OrdinalIgnoreCase)
            || name.Equals(DewSnapshotCore.ArchiveDirectoryName, StringComparison.OrdinalIgnoreCase))
            throw new DewOperationException($"Dew history contains a reserved metadata source name: {name}");
    }

    private static void EnsureNoReparsePoint(string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var current = Path.GetFullPath(path);
        while (!string.IsNullOrWhiteSpace(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new DewOperationException($"{label} cannot traverse a symbolic link or junction: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }

            var parent = Directory.GetParent(current)?.FullName;
            if (string.IsNullOrWhiteSpace(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                break;
            current = parent;
        }
    }

    private static void EnsureNoReparseTree(string root, string label, CancellationToken ct)
    {
        EnsureNoReparsePoint(root, label);
        if (!Directory.Exists(root)) return;
        var pending = new Stack<(string Path, int Depth)>();
        pending.Push((root, 0));
        int entries = 0;
        while (pending.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var (directory, depth) = pending.Pop();
            if (depth > MaxImportedTreeDepth)
                throw new DewOperationException(
                    $"{label} exceeds the safe import depth of {MaxImportedTreeDepth:N0} directories.");
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                ct.ThrowIfCancellationRequested();
                if (++entries > MaxImportedTreeEntries)
                    throw new DewOperationException(
                        $"{label} exceeds the safe import limit of {MaxImportedTreeEntries:N0} filesystem entries.");
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                    throw new DewOperationException($"{label} cannot contain a symbolic link or junction: {entry}");
                if ((attributes & FileAttributes.Directory) != 0)
                    pending.Push((entry, depth + 1));
            }
        }
    }

    private static async Task WriteTrustedAttributesAsync(string attributesPath, CancellationToken ct)
    {
        var directory = Path.GetDirectoryName(attributesPath)
            ?? throw new DewOperationException("Git info attributes has no parent directory.");
        var temporary = Path.Combine(directory, $"attributes.winforge-{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, useAsync: true))
            await using (var writer = new StreamWriter(stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)))
            {
                await writer.WriteAsync(
                    "* -text -filter -ident -working-tree-encoding -export-ignore -export-subst\n"
                    + "** -text -filter -ident -working-tree-encoding -export-ignore -export-subst\n");
                await writer.FlushAsync(ct);
            }
            File.Move(temporary, attributesPath, overwrite: true);
        }
        finally { TryDeleteFile(temporary); }
    }

    private static void ExtractZipSafely(string zipPath, string destination)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                var normalizedEntry = entry.FullName.Replace('\\', '/');
                foreach (var component in normalizedEntry.Split('/', StringSplitOptions.RemoveEmptyEntries))
                    ValidateSourceName(component);
                var target = Path.GetFullPath(Path.Combine(destination,
                    normalizedEntry.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new DewOperationException("The staged Git archive contains an unsafe path.");
                if (!targets.Add(target))
                    throw new DewOperationException(
                        $"The staged Git archive contains colliding Windows paths: {entry.FullName}");
                int unixType = (entry.ExternalAttributes >> 16) & 0xF000;
                if (unixType == 0xA000)
                    throw new DewOperationException("Symbolic links are not restored from Dew history.");
            }
        }
        ZipFile.ExtractToDirectory(zipPath, destination, overwriteFiles: false);
    }

    private static string ToolFailure(string prefix, ToolResult result)
    {
        var detail = result.Output.Trim();
        return detail.Length == 0 ? prefix : $"{prefix}{Environment.NewLine}{detail}";
    }

    private static async Task CreateArchiveWithLibraryAsync(string library, string repository,
        string archivePath, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await Task.Run(() =>
            {
                lock (SevenZipLibraryLock)
                {
                    SharpSevenZipBase.SetLibraryPath(library);
                    var compressor = new SharpSevenZipCompressor
                    {
                        ArchiveFormat = OutArchiveFormat.SevenZip,
                        CompressionLevel = SharpSevenZip.CompressionLevel.Ultra,
                        CompressionMethod = CompressionMethod.Lzma2,
                        DirectoryStructure = true,
                        PreserveDirectoryRoot = true,
                        IncludeEmptyDirectories = true,
                        EncryptHeaders = password.Length > 0,
                    };
                    compressor.CompressDirectory(repository, archivePath, password);
                }
            }, CancellationToken.None);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            throw new DewOperationException("7-Zip could not create the archive.");
        }
        ct.ThrowIfCancellationRequested();
    }

    private static async Task TestArchiveWithLibraryAsync(string library, string archivePath,
        string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        bool valid;
        try
        {
            valid = await Task.Run(() =>
            {
                lock (SevenZipLibraryLock)
                {
                    SharpSevenZipBase.SetLibraryPath(library);
                    using var extractor = password.Length > 0
                        ? new SharpSevenZipExtractor(archivePath, password)
                        : new SharpSevenZipExtractor(archivePath);
                    return extractor.Check();
                }
            }, CancellationToken.None);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            throw new DewOperationException("Archive integrity test failed. The password may be incorrect or the archive may be damaged.");
        }
        ct.ThrowIfCancellationRequested();
        if (!valid)
            throw new DewOperationException("Archive integrity test failed. The password may be incorrect or the archive may be damaged.");
    }

    private sealed record ToolResult(int ExitCode, string StdOut, string StdErr)
    {
        public string Output => string.Join(Environment.NewLine,
            new[] { StdOut.Trim(), StdErr.Trim() }.Where(s => s.Length > 0));
    }

    private static async Task<ToolResult> RunToolAsync(string executable, string? workingDirectory,
        IReadOnlyList<string> arguments, string? standardInput, CancellationToken ct)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = standardInput is not null,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            if (standardInput is not null)
                info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true);
            if (!string.IsNullOrWhiteSpace(workingDirectory)) info.WorkingDirectory = workingDirectory;
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            foreach (var key in info.Environment.Keys
                .Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
                info.Environment.Remove(key);
            info.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
            info.Environment["GIT_CONFIG_GLOBAL"] = "NUL";
            info.Environment["GIT_TERMINAL_PROMPT"] = "0";
            info.Environment["GCM_INTERACTIVE"] = "Never";
            info.Environment["GIT_LITERAL_PATHSPECS"] = "1";

            using var process = new Process { StartInfo = info };
            ct.ThrowIfCancellationRequested();
            if (!process.Start()) return new ToolResult(-1, "", "The process could not be started.");
            var stdout = ReadBoundedAsync(process.StandardOutput, MaxToolOutputCharacters, ct);
            var stderr = ReadBoundedAsync(process.StandardError, MaxToolOutputCharacters, ct);
            try
            {
                if (standardInput is not null)
                {
                    await process.StandardInput.WriteAsync(standardInput.AsMemory(), ct);
                    await process.StandardInput.FlushAsync(ct);
                    process.StandardInput.Close();
                }
                var exitTask = process.WaitForExitAsync(ct);
                var pending = new List<Task> { exitTask, stdout, stderr };
                while (!exitTask.IsCompleted)
                {
                    var completed = await Task.WhenAny(pending);
                    await completed; // propagate a bounded-reader or cancellation failure before a child can block its pipe
                    pending.Remove(completed);
                }
                await exitTask;
            }
            catch (Exception ex)
            {
                bool exited = await TerminateProcessAsync(process);
                if (!exited)
                    return new ToolResult(-1, "",
                        $"Process {process.Id} did not exit within 10 seconds after cancellation or I/O failure.");
                string capturedOut = "", capturedErr = "";
                try
                {
                    await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2));
                    capturedOut = await stdout;
                    capturedErr = await stderr;
                }
                catch { }
                if (ex is OperationCanceledException) throw;
                return new ToolResult(-1, capturedOut,
                    ex.Message + (capturedErr.Length > 0 ? Environment.NewLine + capturedErr : ""));
            }
            return new ToolResult(process.ExitCode, await stdout, await stderr);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new ToolResult(-1, "", ex.Message); }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int characterLimit,
        CancellationToken ct)
    {
        var result = new StringBuilder(Math.Min(characterLimit, 4096));
        var buffer = new char[8192];
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), ct);
            if (read == 0) return result.ToString();
            if (result.Length > characterLimit - read)
                throw new DewOperationException(
                    $"A Dew helper produced more than {characterLimit / (1024 * 1024)} MiB of output and was stopped.");
            result.Append(buffer, 0, read);
        }
    }

    private static async Task<bool> TerminateProcessAsync(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
        try
        {
            var exitTask = process.WaitForExitAsync(CancellationToken.None);
            if (await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromSeconds(10))) != exitTask) return false;
            await exitTask;
            return true;
        }
        catch { return false; }
    }

    private static void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); } catch { }
    }
}

/// <summary>單一來源、debounced、自動 Dew 歷史 · Debounced single-source auto history.</summary>
public sealed class DewHistoryWatcher : IDisposable
{
    private readonly DewProjectContext _project;
    private readonly FileSystemWatcher _watcher;
    private readonly Timer _debounce;
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _taskLock = new();
    private Task _activeTask = Task.CompletedTask;
    private int _dirty;
    private int _workerActive;
    private int _running;
    private int _disposed;

    public event EventHandler<DewAutoSnapshotEventArgs>? SnapshotCompleted;

    public DewHistoryWatcher(DewProjectContext project, TimeSpan debounce)
    {
        if (project.IsReadOnlyImport)
            throw new DewOperationException("Extracted/nonstandard Dew imports are history/restore-only and cannot be watched.");
        if (project.Paths.Count != 1)
            throw new DewOperationException("Auto history supports one selected file or folder at a time.");
        _project = project;
        var source = project.Paths[0];
        bool isDirectory = Directory.Exists(source);
        var watchDirectory = isDirectory ? source : Path.GetDirectoryName(source);
        if (string.IsNullOrWhiteSpace(watchDirectory) || !Directory.Exists(watchDirectory))
            throw new DewOperationException("The selected source cannot be watched.");

        Debounce = debounce < TimeSpan.FromMilliseconds(500) ? TimeSpan.FromMilliseconds(500) : debounce;
        _debounce = new Timer(_ => StartSnapshotTask(), null, Timeout.Infinite, Timeout.Infinite);
        _watcher = new FileSystemWatcher(watchDirectory)
        {
            Filter = isDirectory ? "*" : Path.GetFileName(source),
            IncludeSubdirectories = isDirectory,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                | NotifyFilters.Size | NotifyFilters.CreationTime,
            EnableRaisingEvents = false,
        };
        _watcher.Changed += Changed;
        _watcher.Created += Changed;
        _watcher.Deleted += Changed;
        _watcher.Renamed += Changed;
        _watcher.Error += (_, e) =>
        {
            Interlocked.Exchange(ref _dirty, 1);
            ScheduleDebounce(); // a full-tree snapshot recovers changes lost to a watcher buffer overflow
            RaiseSnapshotCompleted(new DewAutoSnapshotEventArgs(null, e.GetException()));
        };
    }

    public TimeSpan Debounce { get; }
    public bool IsRunning => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _running) == 1;

    public void Start()
    {
        lock (_taskLock)
        {
            if (Volatile.Read(ref _disposed) != 0) throw new ObjectDisposedException(nameof(DewHistoryWatcher));
            Volatile.Write(ref _running, 1);
            try
            {
                // Publish the running state before native notifications are enabled so an
                // immediate change cannot be observed and then discarded by Changed().
                _watcher.EnableRaisingEvents = true;
            }
            catch
            {
                Volatile.Write(ref _running, 0);
                throw;
            }
        }
    }
    public void Stop()
    {
        lock (_taskLock)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
            Volatile.Write(ref _running, 0);
            _watcher.EnableRaisingEvents = false;
            try { _debounce.Change(Timeout.Infinite, Timeout.Infinite); } catch (ObjectDisposedException) { }
        }
    }

    private void Changed(object sender, FileSystemEventArgs e)
    {
        if (!IsRunning || DewSnapshotCore.IsSameOrDescendant(e.FullPath, _project.ArchiveDirectory)) return;
        Interlocked.Exchange(ref _dirty, 1);
        ScheduleDebounce();
    }

    private void ScheduleDebounce()
    {
        lock (_taskLock)
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _running) == 0) return;
            try { _debounce.Change(Debounce, Timeout.InfiniteTimeSpan); } catch (ObjectDisposedException) { }
        }
    }

    private void StartSnapshotTask()
    {
        if (Interlocked.CompareExchange(ref _workerActive, 1, 0) != 0)
        {
            Interlocked.Exchange(ref _dirty, 1);
            return;
        }
        lock (_taskLock)
        {
            if (Volatile.Read(ref _disposed) != 0 || Volatile.Read(ref _running) == 0)
            {
                Interlocked.Exchange(ref _workerActive, 0);
                return;
            }
            _activeTask = SnapshotAfterDebounceAsync();
        }
    }

    private async Task SnapshotAfterDebounceAsync()
    {
        try
        {
            do
            {
                Interlocked.Exchange(ref _dirty, 0);
                var result = await DewEncryptionService.TakeSnapshotAsync(_project,
                    $"Dew auto history {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    allowSelectionReplacement: false, ct: _stopping.Token);
                RaiseSnapshotCompleted(new DewAutoSnapshotEventArgs(result, null));
            }
            while (IsRunning && Interlocked.Exchange(ref _dirty, 0) == 1);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { RaiseSnapshotCompleted(new DewAutoSnapshotEventArgs(null, ex)); }
        finally
        {
            Interlocked.Exchange(ref _workerActive, 0);
            if (IsRunning && Volatile.Read(ref _dirty) == 1) ScheduleDebounce();
        }
    }

    private void RaiseSnapshotCompleted(DewAutoSnapshotEventArgs args)
    {
        var handlers = SnapshotCompleted;
        if (handlers is null) return;
        foreach (EventHandler<DewAutoSnapshotEventArgs> handler in handlers.GetInvocationList())
        {
            try { handler(this, args); } catch { }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Volatile.Write(ref _running, 0);
        _stopping.Cancel();
        Task active;
        lock (_taskLock)
        {
            try { _watcher.EnableRaisingEvents = false; } catch (ObjectDisposedException) { }
            _debounce.Dispose();
            active = _activeTask;
        }
        _watcher.Dispose();
        _ = active.ContinueWith(_ =>
        {
            _stopping.Dispose();
        }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

public sealed record DewAutoSnapshotEventArgs(DewSnapshotResult? Result, Exception? Error);
