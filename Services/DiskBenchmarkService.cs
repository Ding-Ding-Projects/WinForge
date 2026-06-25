using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace WinForge.Services;

/// <summary>
/// 原生硬碟速度測試（CrystalDiskMark 風格）· Native CrystalDiskMark-style disk benchmark.
/// 純 managed C#：用 PInvoke CreateFile 開檔，加 FILE_FLAG_NO_BUFFERING + FILE_FLAG_WRITE_THROUGH
/// 繞過快取，用磁區對齊（VirtualAlloc）緩衝、多個未完成嘅 overlapped I/O 模擬佇列深度，Stopwatch 計時。
/// Pure managed C# — opens the file via PInvoke CreateFile with FILE_FLAG_NO_BUFFERING +
/// FILE_FLAG_WRITE_THROUGH to bypass the OS cache, sector-aligned page buffers, and multiple
/// outstanding overlapped operations to emulate queue depth. No external tool is launched.
/// The temp file is always deleted afterwards, even on error or cancel.
/// </summary>
public sealed class DiskBenchmarkService
{
    // ── Test definitions ───────────────────────────────────────────────────────

    /// <summary>一個測試項目嘅定義 · One test-set entry (block size, queue depth).</summary>
    public sealed record TestSpec(string Key, string En, string Zh, int BlockBytes, int QueueDepth, bool Random, bool Write)
    {
        public bool ReportIops => Random; // IOPS is meaningful for random 4K tests.
    }

    public const int SECTOR = 4096; // Alignment for FILE_FLAG_NO_BUFFERING (4K covers all modern sectors).

    /// <summary>CrystalDiskMark 既典型測試組合 · The classic CrystalDiskMark test set.</summary>
    public static IReadOnlyList<TestSpec> Tests { get; } = new[]
    {
        new TestSpec("SEQ1M-Q8T1-R", "SEQ1M Q8T1 Read",  "循序 1M Q8T1 讀取", 1 << 20, 8,  false, false),
        new TestSpec("SEQ1M-Q8T1-W", "SEQ1M Q8T1 Write", "循序 1M Q8T1 寫入", 1 << 20, 8,  false, true),
        new TestSpec("SEQ1M-Q1T1-R", "SEQ1M Q1T1 Read",  "循序 1M Q1T1 讀取", 1 << 20, 1,  false, false),
        new TestSpec("SEQ1M-Q1T1-W", "SEQ1M Q1T1 Write", "循序 1M Q1T1 寫入", 1 << 20, 1,  false, true),
        new TestSpec("RND4K-Q32T1-R","RND4K Q32T1 Read", "隨機 4K Q32T1 讀取", 4 << 10, 32, true,  false),
        new TestSpec("RND4K-Q32T1-W","RND4K Q32T1 Write","隨機 4K Q32T1 寫入", 4 << 10, 32, true,  true),
        new TestSpec("RND4K-Q1T1-R", "RND4K Q1T1 Read",  "隨機 4K Q1T1 讀取", 4 << 10, 1,  true,  false),
        new TestSpec("RND4K-Q1T1-W", "RND4K Q1T1 Write", "隨機 4K Q1T1 寫入", 4 << 10, 1,  true,  true),
    };

    /// <summary>一個測試嘅結果 · The result of one test (best-of-passes throughput).</summary>
    public sealed class TestResult
    {
        public TestSpec Spec { get; init; } = null!;
        public double MBps { get; set; }       // MB/s (MB = 1,000,000 bytes, CDM convention).
        public double Iops { get; set; }        // operations per second (random tests).
        public bool ReportIops => Spec.ReportIops;
        public string Name => Loc.I.Pick(Spec.En, Spec.Zh);
        public string MBpsText => $"{MBps:0.00} MB/s";
        public string IopsText => ReportIops ? $"{Iops:#,0} IOPS" : "";
    }

    /// <summary>實時進度回報 · Live progress callback payload.</summary>
    public sealed record Progress(TestSpec Spec, int Pass, int TotalPasses, double Fraction);

    // ── Run ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 跑全套測試 · Run the full test set against a temp file in <paramref name="targetDir"/>.
    /// </summary>
    public async Task<List<TestResult>> RunAsync(
        string targetDir, long fileBytes, int passes,
        IProgress<Progress>? progress, CancellationToken ct)
    {
        fileBytes = AlignUp(Math.Max(fileBytes, 16L << 20), SECTOR);
        passes = Math.Clamp(passes, 1, 9);
        Directory.CreateDirectory(targetDir);
        var path = Path.Combine(targetDir, $".winforge-bench-{Guid.NewGuid():N}.tmp");

        var results = new List<TestResult>();
        try
        {
            // Pre-allocate the test file with real data so reads have something to read.
            await Task.Run(() => Preallocate(path, fileBytes, ct), ct);

            foreach (var spec in Tests)
            {
                ct.ThrowIfCancellationRequested();
                double bestMBps = 0, bestIops = 0;
                for (int pass = 1; pass <= passes; pass++)
                {
                    ct.ThrowIfCancellationRequested();
                    int p = pass;
                    var run = await Task.Run(() => RunOne(path, fileBytes, spec,
                        frac => progress?.Report(new Progress(spec, p, passes, frac)), ct), ct);
                    if (run.MBps > bestMBps) { bestMBps = run.MBps; bestIops = run.Iops; }
                }
                results.Add(new TestResult { Spec = spec, MBps = bestMBps, Iops = bestIops });
            }
        }
        finally
        {
            TryDelete(path);
        }
        return results;
    }

    // ── Pre-allocation ─────────────────────────────────────────────────────────

    private static unsafe void Preallocate(string path, long fileBytes, CancellationToken ct)
    {
        using var h = OpenAligned(path, write: true, create: true);
        const int chunk = 8 << 20; // 8 MiB writes.
        IntPtr buf = VirtualAlloc(IntPtr.Zero, (UIntPtr)chunk, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        if (buf == IntPtr.Zero) throw new IOException("VirtualAlloc failed");
        try
        {
            // Fill with pseudo-random bytes so SSDs can't cheat with compression/dedup.
            var rnd = new Random(12345);
            var span = new Span<byte>((void*)buf, chunk);
            rnd.NextBytes(span);

            long written = 0;
            while (written < fileBytes)
            {
                ct.ThrowIfCancellationRequested();
                int toWrite = (int)Math.Min(chunk, fileBytes - written);
                toWrite = (int)AlignUp(toWrite, SECTOR);
                if (!WriteFile(h, buf, (uint)toWrite, out uint done, IntPtr.Zero) || done == 0)
                    throw new IOException($"Pre-allocation write failed (Win32 {Marshal.GetLastWin32Error()})");
                written += done;
            }
            FlushFileBuffers(h);
        }
        finally { VirtualFree(buf, UIntPtr.Zero, MEM_RELEASE); }
    }

    // ── Single test run ────────────────────────────────────────────────────────

    private sealed record RunResult(double MBps, double Iops);

    private static unsafe RunResult RunOne(
        string path, long fileBytes, TestSpec spec, Action<double>? onFraction, CancellationToken ct)
    {
        int block = spec.BlockBytes;
        int qd = spec.QueueDepth;
        // How much total data to move this pass: cap so each test stays a few seconds.
        long target = spec.Random
            ? Math.Min(fileBytes, Math.Max(64L << 20, fileBytes / 4))   // random: a slice
            : fileBytes;                                                  // sequential: whole file
        long totalOps = Math.Max(qd, target / block);
        target = totalOps * block;

        long maxOffsetBlocks = Math.Max(1, fileBytes / block);
        var rnd = new Random(unchecked(spec.Key.GetHashCode() ^ Environment.TickCount));

        using var h = OpenAligned(path, write: spec.Write, create: false);

        // Allocate qd sector-aligned buffers + an OVERLAPPED per slot.
        var bufs = new IntPtr[qd];
        var events = new SafeWaitHandle[qd];
        var olPtr = new IntPtr[qd];
        for (int i = 0; i < qd; i++)
        {
            bufs[i] = VirtualAlloc(IntPtr.Zero, (UIntPtr)block, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (bufs[i] == IntPtr.Zero) throw new IOException("VirtualAlloc failed");
            if (spec.Write) new Span<byte>((void*)bufs[i], block).Fill((byte)(i + 1));
            var ev = CreateEvent(IntPtr.Zero, true, false, null);
            if (ev == IntPtr.Zero) throw new IOException("CreateEvent failed");
            events[i] = new SafeWaitHandle(ev, true);
            olPtr[i] = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
        }

        long opsIssued = 0, opsDone = 0;
        long seqOffset = 0;
        var sw = Stopwatch.StartNew();
        try
        {
            // Prime the queue.
            for (int i = 0; i < qd && opsIssued < totalOps; i++)
                Issue(h, spec, block, bufs[i], olPtr[i], events[i], maxOffsetBlocks, rnd, ref seqOffset, ref opsIssued);

            int reported = 0;
            while (opsDone < totalOps)
            {
                ct.ThrowIfCancellationRequested();
                // Wait for any outstanding op to complete.
                int idx = WaitAny(events, opsIssued > 0 ? Math.Min(qd, (int)(opsIssued - opsDone)) : 0);
                if (idx < 0) break;

                var ol = (NativeOverlapped*)olPtr[idx];
                if (!GetOverlappedResult(h, ref *ol, out _, true))
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != 0) throw new IOException($"I/O failed (Win32 {err})");
                }
                ResetEvent(events[idx].DangerousGetHandle());
                opsDone++;

                if (opsIssued < totalOps)
                    Issue(h, spec, block, bufs[idx], olPtr[idx], events[idx], maxOffsetBlocks, rnd, ref seqOffset, ref opsIssued);

                // Throttle progress reports.
                int pct = (int)(opsDone * 20 / totalOps);
                if (pct != reported) { reported = pct; onFraction?.Invoke(opsDone / (double)totalOps); }
            }
            sw.Stop();
        }
        finally
        {
            for (int i = 0; i < qd; i++)
            {
                if (bufs[i] != IntPtr.Zero) VirtualFree(bufs[i], UIntPtr.Zero, MEM_RELEASE);
                if (olPtr[i] != IntPtr.Zero) Marshal.FreeHGlobal(olPtr[i]);
                events[i]?.Dispose();
            }
        }

        double seconds = Math.Max(sw.Elapsed.TotalSeconds, 1e-6);
        long bytes = opsDone * (long)block;
        double mbps = bytes / 1_000_000.0 / seconds;     // MB/s (CDM uses decimal MB).
        double iops = opsDone / seconds;
        return new RunResult(mbps, iops);
    }

    private static unsafe void Issue(
        SafeFileHandle h, TestSpec spec, int block, IntPtr buf, IntPtr olPtr, SafeWaitHandle ev,
        long maxOffsetBlocks, Random rnd, ref long seqOffset, ref long opsIssued)
    {
        long offset;
        if (spec.Random)
        {
            long b = (long)(rnd.NextDouble() * maxOffsetBlocks);
            offset = b * block;
        }
        else
        {
            offset = seqOffset;
            seqOffset += block;
            if (seqOffset + block > maxOffsetBlocks * block) seqOffset = 0;
        }

        var ol = (NativeOverlapped*)olPtr;
        *ol = default;
        ol->OffsetLow = (int)(offset & 0xFFFFFFFF);
        ol->OffsetHigh = (int)(offset >> 32);
        ol->EventHandle = ev.DangerousGetHandle();

        bool ok = spec.Write
            ? WriteFile(h, buf, (uint)block, out _, olPtr)
            : ReadFile(h, buf, (uint)block, out _, olPtr);
        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            if (err != ERROR_IO_PENDING)
                throw new IOException($"{(spec.Write ? "Write" : "Read")} failed (Win32 {err})");
        }
        opsIssued++;
    }

    private static int WaitAny(SafeWaitHandle[] events, int count)
    {
        if (count <= 0) count = events.Length;
        var handles = new IntPtr[count];
        for (int i = 0; i < count; i++) handles[i] = events[i].DangerousGetHandle();
        uint r = WaitForMultipleObjects((uint)count, handles, false, 60_000);
        if (r >= WAIT_OBJECT_0 && r < WAIT_OBJECT_0 + count) return (int)(r - WAIT_OBJECT_0);
        return -1;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static SafeFileHandle OpenAligned(string path, bool write, bool create)
    {
        uint access = write ? (GENERIC_READ | GENERIC_WRITE) : GENERIC_READ;
        uint share = FILE_SHARE_READ | FILE_SHARE_WRITE;
        uint disp = create ? CREATE_ALWAYS : OPEN_EXISTING;
        uint flags = FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH | FILE_FLAG_OVERLAPPED;
        var h = CreateFile(path, access, share, IntPtr.Zero, disp, flags, IntPtr.Zero);
        if (h.IsInvalid)
            throw new IOException($"CreateFile failed for {path} (Win32 {Marshal.GetLastWin32Error()})");
        return h;
    }

    private static long AlignUp(long v, long a) => (v + a - 1) / a * a;

    public static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    /// <summary>清走任何遺留嘅測試檔 · Sweep stale temp files from a previous crashed run.</summary>
    public static void CleanupStale(string dir)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, ".winforge-bench-*.tmp"))
                TryDelete(f);
        }
        catch { }
    }

    // ── Win32 ──────────────────────────────────────────────────────────────────

    private const uint GENERIC_READ = 0x80000000;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_READ = 0x1;
    private const uint FILE_SHARE_WRITE = 0x2;
    private const uint CREATE_ALWAYS = 2;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;
    private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    private const int ERROR_IO_PENDING = 997;
    private const uint WAIT_OBJECT_0 = 0;

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(string lpFileName, uint dwDesiredAccess,
        uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle hFile, IntPtr lpBuffer,
        uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(SafeFileHandle hFile, IntPtr lpBuffer,
        uint nNumberOfBytesToWrite, out uint lpNumberOfBytesWritten, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetOverlappedResult(SafeFileHandle hFile,
        ref NativeOverlapped lpOverlapped, out uint lpNumberOfBytesTransferred, bool bWait);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FlushFileBuffers(SafeFileHandle hFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateEvent(IntPtr lpEventAttributes, bool bManualReset,
        bool bInitialState, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ResetEvent(IntPtr hEvent);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForMultipleObjects(uint nCount, IntPtr[] lpHandles,
        bool bWaitAll, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize,
        uint flAllocationType, uint flProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);
}
