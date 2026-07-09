using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WinForge.Services;

/// <summary>
/// 一次隨附 app 準備／編譯嘅持久記錄 · Durable log for one companion preparation/build attempt.
/// Logging is deliberately best-effort: a disk problem must never prevent the actual companion build.
/// </summary>
internal sealed class CompanionBuildLog : IDisposable
{
    private const int DefaultRetention = 20;
    private readonly object _gate = new();
    private StreamWriter? _writer;
    private string? _error;
    private bool _disposed;

    private CompanionBuildLog(string directoryPath, string? filePath, StreamWriter? writer, string? error)
    {
        DirectoryPath = directoryPath;
        FilePath = filePath;
        _writer = writer;
        _error = error;
    }

    /// <summary>預設記錄資料夾 · Default persistent log directory.</summary>
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinForge", "logs", "companion-builds");

    public string DirectoryPath { get; }
    public string? FilePath { get; }
    public string? Error
    {
        get { lock (_gate) return _error; }
    }
    public bool IsAvailable
    {
        get { lock (_gate) return !_disposed && _writer is not null && FilePath is not null; }
    }

    /// <summary>開始一個新記錄；失敗時回傳 no-op session · Start a new log, falling back to a no-op session.</summary>
    public static CompanionBuildLog Start(string companionId, string titleEn, string titleZh,
        string sourcePath, string targetName, string? directoryPath = null, int retention = DefaultRetention)
    {
        var directory = directoryPath ?? DefaultDirectory;
        try
        {
            Directory.CreateDirectory(directory);
            var stem = SafeStem(companionId);
            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var path = Path.Combine(directory, $"{stem}-{stamp}-{suffix}.log");
            var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };
            var session = new CompanionBuildLog(directory, path, writer, null);
            session.Write("WinForge companion build log · WinForge 隨附 app 編譯記錄");
            session.Write($"Started: {DateTimeOffset.Now:O}");
            session.Write($"Companion: {titleEn} · {titleZh} ({companionId})");
            session.Write($"Source: {sourcePath}");
            session.Write($"Target: {targetName}");
            session.Write(new string('-', 72));
            // A disk-full/encoding failure in the header must not let a partial current file evict the last
            // valid retained transcript.
            if (session.IsAvailable)
                Prune(directory, stem, path, Math.Max(1, retention));
            return session;
        }
        catch (Exception ex)
        {
            return new CompanionBuildLog(directory, null, null, ex.Message);
        }
    }

    public void AppendStatus(string? en, string? zh)
    {
        en = Clean(en);
        zh = Clean(zh);
        if (string.IsNullOrWhiteSpace(en) && string.IsNullOrWhiteSpace(zh)) return;
        var text = string.Equals(en, zh, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(zh)
            ? en
            : string.IsNullOrWhiteSpace(en) ? zh : $"{en} · {zh}";
        Write($"[{DateTimeOffset.Now:HH:mm:ss.fff}] STATUS  {text}");
    }

    public void AppendOutput(string? line)
    {
        line = Clean(line);
        if (line is null) return;
        Write($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {line}");
    }

    public void Finish(string outcome, string? message = null, string? capturedOutput = null)
    {
        Write(new string('-', 72));
        Write($"Finished: {DateTimeOffset.Now:O}");
        Write($"Outcome: {Clean(outcome)}");
        if (!string.IsNullOrWhiteSpace(message)) Write($"Message: {Clean(message)}");
        if (!string.IsNullOrWhiteSpace(capturedOutput))
        {
            Write("Captured output:");
            foreach (var line in SplitLines(capturedOutput)) Write(line);
        }
    }

    private void Write(string? line)
    {
        if (line is null) return;
        lock (_gate)
        {
            if (_disposed || _writer is null) return;
            try { _writer.WriteLine(line); }
            catch (Exception ex)
            {
                // Flip availability immediately so the UI never claims an incomplete file is a full log.
                _error ??= ex.Message;
                try { _writer.Dispose(); } catch { }
                _writer = null;
            }
        }
    }

    private static string? Clean(string? value) => value?.Replace("\0", "", StringComparison.Ordinal)
        .Replace("\r", "", StringComparison.Ordinal);

    private static IEnumerable<string> SplitLines(string value) =>
        value.Replace("\r", "", StringComparison.Ordinal).Split('\n');

    internal static string SafeStem(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "companion";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
            sb.Append(invalid.Contains(ch) || ch is '.' or ' ' ? '-' : char.ToLowerInvariant(ch));
        var stem = sb.ToString().Trim('-');
        while (stem.Contains("--", StringComparison.Ordinal))
            stem = stem.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(stem) ? "companion" : stem;
    }

    private static void Prune(string directory, string stem, string currentPath, int retention)
    {
        try
        {
            var files = Directory.EnumerateFiles(directory, $"{stem}-*.log", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => string.Equals(file.FullName, currentPath,
                    StringComparison.OrdinalIgnoreCase) ? DateTime.MaxValue : file.LastWriteTimeUtc)
                .ToList();
            foreach (var old in files.Skip(retention))
            {
                try { old.Delete(); } catch { }
            }
        }
        catch { }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            try { _writer?.Dispose(); } catch { }
        }
    }
}
