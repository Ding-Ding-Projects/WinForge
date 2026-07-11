using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace WinForge.Services;

/// <summary>Result of loading a settings snapshot without losing the distinction between empty and damaged storage.</summary>
internal sealed class SettingsLoadResult
{
    internal SettingsLoadResult(Dictionary<string, string> values, bool canPersist)
    {
        Values = values;
        CanPersist = canPersist;
    }

    internal Dictionary<string, string> Values { get; }
    internal bool CanPersist { get; }
}

internal enum SettingsFileState
{
    Missing,
    Valid,
    Invalid,
}

/// <summary>
/// Durable, fail-closed persistence for the user settings file. Every normal replacement is
/// same-directory and atomic, with the previous complete snapshot retained as a sibling backup.
/// </summary>
internal static class SettingsStorePersistence
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    internal static string BackupPathFor(string filePath) => filePath + ".bak";

    internal static SettingsLoadResult Load(string filePath)
    {
        var primaryState = ReadSettings(filePath, out var primary);
        if (primaryState == SettingsFileState.Valid)
            return new SettingsLoadResult(primary!, canPersist: true);

        var backupPath = BackupPathFor(filePath);
        var backupState = ReadSettings(backupPath, out var backup);
        if (backupState == SettingsFileState.Valid)
        {
            // Preserve a damaged primary before restoring the good backup. If either preservation
            // or atomic restoration cannot be completed, keep the recovered values in memory but
            // deny automatic writes rather than risking an overwrite of the only evidence.
            var restored = TryRestoreFromBackup(filePath, primaryState == SettingsFileState.Invalid, backup!);
            return new SettingsLoadResult(backup!, restored);
        }

        // A genuinely first-run directory is the only empty state allowed to persist. A malformed,
        // truncated, or unreadable primary/backup must not collapse into an empty cache that Set()
        // later serializes over the user file.
        var firstRun = primaryState == SettingsFileState.Missing && backupState == SettingsFileState.Missing;
        return new SettingsLoadResult(new Dictionary<string, string>(), firstRun);
    }

    /// <summary>Write a new snapshot through a flushed same-directory temporary file and atomic replacement.</summary>
    internal static bool TryWriteAtomically(string filePath, IReadOnlyDictionary<string, string> values) =>
        TryInstallSnapshot(filePath, values, BackupPathFor(filePath));

    /// <summary>
    /// An import is an explicit user recovery choice. It is allowed to repair a fail-closed store,
    /// but any malformed primary is first retained beside the settings file for inspection.
    /// </summary>
    internal static bool TryRepairFromExplicitImport(string filePath, IReadOnlyDictionary<string, string> values)
    {
        var currentState = ReadSettings(filePath, out _);
        if (currentState == SettingsFileState.Invalid && !TryPreserveInvalidPrimary(filePath))
            return false;

        // Keep a known-good recovery backup intact while replacing a damaged primary from an
        // explicit import. Otherwise File.Replace would rotate the damaged primary into .bak.
        var backupState = ReadSettings(BackupPathFor(filePath), out _);
        return TryInstallSnapshot(filePath, values,
            backupState == SettingsFileState.Valid ? null : BackupPathFor(filePath));
    }

    private static SettingsFileState ReadSettings(string path, out Dictionary<string, string>? values)
    {
        values = null;
        if (!File.Exists(path)) return SettingsFileState.Missing;

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (parsed is null) return SettingsFileState.Invalid;
            values = parsed;
            return SettingsFileState.Valid;
        }
        catch
        {
            return SettingsFileState.Invalid;
        }
    }

    private static bool TryRestoreFromBackup(string filePath, bool primaryIsInvalid,
        IReadOnlyDictionary<string, string> backupValues)
    {
        if (primaryIsInvalid && !TryPreserveInvalidPrimary(filePath))
            return false;

        // Do not rotate the good backup while using it to recover the primary.
        return TryInstallSnapshot(filePath, backupValues, backupPath: null);
    }

    private static bool TryInstallSnapshot(string filePath, IReadOnlyDictionary<string, string> values,
        string? backupPath)
    {
        var tempPath = TryWriteTemporarySnapshot(filePath, values);
        if (tempPath is null) return false;

        try
        {
            if (File.Exists(filePath))
                File.Replace(tempPath, filePath, backupPath, ignoreMetadataErrors: true);
            else
                File.Move(tempPath, filePath);

            tempPath = null;
            return true;
        }
        catch
        {
            // Do not use delete-and-rewrite as a fallback: losing persistence is safer than losing
            // a complete settings snapshot when the filesystem cannot provide atomic replacement.
            return false;
        }
        finally
        {
            if (tempPath is not null) TryDelete(tempPath);
        }
    }

    private static string? TryWriteTemporarySnapshot(string filePath, IReadOnlyDictionary<string, string> values)
    {
        string? tempPath = null;
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory)) return null;

            Directory.CreateDirectory(directory);
            tempPath = Path.Combine(directory,
                $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            var json = JsonSerializer.Serialize(values, WriteOptions);

            using var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, options: FileOptions.WriteThrough);
            using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                bufferSize: 4096, leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
            }
            stream.Flush(flushToDisk: true);
            return tempPath;
        }
        catch
        {
            if (tempPath is not null) TryDelete(tempPath);
            return null;
        }
    }

    private static bool TryPreserveInvalidPrimary(string filePath)
    {
        string? preservedPath = null;
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory)) return false;

            preservedPath = Path.Combine(directory,
                $"{Path.GetFileName(filePath)}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}");
            using var source = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var destination = new FileStream(preservedPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, options: FileOptions.WriteThrough);
            source.CopyTo(destination);
            destination.Flush(flushToDisk: true);
            return true;
        }
        catch
        {
            if (preservedPath is not null) TryDelete(preservedPath);
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch { /* best effort cleanup only */ }
    }
}
