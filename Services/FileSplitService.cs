using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 檔案切割／合併 · File split &amp; join — pure managed streaming split of any file into sequential
/// numbered parts (<c>&lt;name&gt;.001</c>, <c>.002</c>, …) and lossless rejoin back to one file.
/// 全部用 <see cref="System.IO"/> 串流，唔會一次過讀入記憶體；全部係 async 而且有進度回呼。
/// No Process.Start, no external tools — just FileStream + async I/O.
/// </summary>
public static class FileSplitService
{
    private const int BufferSize = 1 << 20; // 1 MiB streaming buffer

    /// <summary>切割結果 · Result of a split.</summary>
    public readonly record struct SplitResult(int Parts, long TotalBytes, string FirstPart);

    /// <summary>合併結果 · Result of a join (SHA256 optional, may be null).</summary>
    public readonly record struct JoinResult(int Parts, long TotalBytes, string OutputPath, string? Sha256);

    /// <summary>
    /// 將 <paramref name="sourceFile"/> 串流切成每份 <paramref name="partBytes"/> 個位元組嘅順序部件，
    /// 放喺 <paramref name="outputFolder"/>，命名 <c>&lt;原檔名&gt;.001 / .002 / …</c>。
    /// Splits a file into sequential parts of <paramref name="partBytes"/> bytes each. Reports
    /// progress in [0,1]. All work runs on a background thread; never throws to the caller thread.
    /// </summary>
    public static Task<SplitResult> SplitAsync(string sourceFile, long partBytes, string outputFolder,
        IProgress<double>? progress = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(sourceFile) || !File.Exists(sourceFile))
                throw new FileNotFoundException("Source file not found.", sourceFile);
            if (string.IsNullOrWhiteSpace(outputFolder))
                throw new DirectoryNotFoundException("No output folder chosen.");
            if (partBytes < 1) partBytes = 1;
            Directory.CreateDirectory(outputFolder);

            var name = Path.GetFileName(sourceFile);
            long total = new FileInfo(sourceFile).Length;
            long done = 0;
            int index = 0;
            var buffer = new byte[BufferSize];
            string firstPart = "";

            using var input = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read,
                BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);

            // Empty source still produces exactly one (empty) part for a clean round-trip.
            do
            {
                ct.ThrowIfCancellationRequested();
                index++;
                var partPath = Path.Combine(outputFolder, $"{name}.{index:000}");
                if (index == 1) firstPart = partPath;
                long remainingInPart = partBytes;

                using (var output = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None,
                           BufferSize, FileOptions.Asynchronous))
                {
                    while (remainingInPart > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        int want = (int)Math.Min(buffer.Length, remainingInPart);
                        int read = input.Read(buffer, 0, want);
                        if (read <= 0) break;
                        output.Write(buffer, 0, read);
                        remainingInPart -= read;
                        done += read;
                        if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
                    }
                }
            }
            while (done < total);

            progress?.Report(1.0);
            return new SplitResult(index, total, firstPart);
        }, ct);

    /// <summary>
    /// 由 <paramref name="firstPart"/>（<c>.001</c> 部件）開始，順序將 <c>.001 / .002 / …</c> 合併返一個
    /// 檔案至 <paramref name="outputFile"/>。Concatenates the sequential parts starting at the given
    /// <c>.001</c> part into one output file, optionally computing the SHA256 of the rejoined result.
    /// </summary>
    public static Task<JoinResult> JoinAsync(string firstPart, string outputFile, bool computeHash,
        IProgress<double>? progress = null, CancellationToken ct = default)
        => Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(firstPart) || !File.Exists(firstPart))
                throw new FileNotFoundException("First part (.001) not found.", firstPart);
            if (string.IsNullOrWhiteSpace(outputFile))
                throw new IOException("No output file chosen.");

            var parts = EnumerateParts(firstPart);
            if (parts.Count == 0)
                throw new FileNotFoundException("No numbered parts found next to the chosen file.");

            long total = 0;
            foreach (var p in parts) total += new FileInfo(p).Length;

            long done = 0;
            var buffer = new byte[BufferSize];
            using SHA256? sha = computeHash ? SHA256.Create() : null;

            using (var output = new FileStream(outputFile, FileMode.Create, FileAccess.Write, FileShare.None,
                       BufferSize, FileOptions.Asynchronous))
            {
                foreach (var part in parts)
                {
                    ct.ThrowIfCancellationRequested();
                    using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read,
                        BufferSize, FileOptions.SequentialScan | FileOptions.Asynchronous);
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        output.Write(buffer, 0, read);
                        sha?.TransformBlock(buffer, 0, read, null, 0);
                        done += read;
                        if (total > 0) progress?.Report(Math.Min(1.0, (double)done / total));
                    }
                }
            }

            string? hash = null;
            if (sha is not null)
            {
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                hash = sha.Hash is { } h ? Convert.ToHexString(h) : null;
            }

            progress?.Report(1.0);
            return new JoinResult(parts.Count, total, outputFile, hash);
        }, ct);

    /// <summary>
    /// 由 <c>.001</c> 部件推算出後面所有順序部件（<c>.001, .002, …</c>），直到序號斷咗為止。
    /// From a <c>&lt;stem&gt;.001</c> part, resolve every contiguous numbered part until the sequence breaks.
    /// </summary>
    private static List<string> EnumerateParts(string firstPart)
    {
        var list = new List<string>();
        var dir = Path.GetDirectoryName(firstPart) ?? ".";
        var fileName = Path.GetFileName(firstPart);
        // stem = everything up to the trailing ".NNN"
        int dot = fileName.LastIndexOf('.');
        var stem = dot > 0 ? fileName[..dot] : fileName;

        for (int i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{stem}.{i:000}");
            if (!File.Exists(candidate)) break;
            list.Add(candidate);
        }
        return list;
    }

    /// <summary>格式化位元組數 · Human-readable byte count.</summary>
    public static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} {units[u]}" : $"{v:0.##} {units[u]}";
    }
}
