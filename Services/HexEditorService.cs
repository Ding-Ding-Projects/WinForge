using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 純 C# 十六進位／二進位編輯引擎 · Pure-managed hex/binary editing engine.
///
/// 大檔案唔會一次過讀入記憶體：用記憶體對應檔（memory-mapped file）做唯讀基底，
/// 修改放喺一個「覆寫圖」字典度（offset → byte）。儲存時先寫一份副本再原子換入。
/// Large files are never loaded whole: a read-only memory-mapped view backs the original
/// bytes, and edits live in a sparse overwrite map (offset → byte). Save streams a copy
/// then atomically swaps it in. Insert/delete are modelled as an ordered edit log applied
/// on top of the base when saving, keeping the in-memory footprint tiny.
/// </summary>
public sealed class HexEditorService : IDisposable
{
    public const int BytesPerRow = 16;

    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _view;
    private long _baseLength;          // length of the file on disk that backs the view
    private readonly object _gate = new();

    // Sparse overwrite map for in-place edits (offset → new value).
    private readonly Dictionary<long, byte> _overrides = new();

    // Ordered insert/delete log against the *current* logical layout.
    // For simplicity we keep a rope-free model: a list of segments describing the
    // logical file as a sequence of (source, start, length) runs.
    private List<Segment> _segments = new();
    private long _logicalLength;

    public string? FilePath { get; private set; }
    public long Length => _logicalLength;
    public bool IsOpen => _view is not null || _segments.Count > 0;
    public bool IsDirty { get; private set; }

    /// <summary>一個邏輯區段 · One logical run: bytes come either from the base view or from an inserted buffer.</summary>
    private sealed class Segment
    {
        public bool FromBase;   // true → read from memory-mapped base; false → from Inserted
        public long BaseStart;  // start offset within the base file (FromBase only)
        public byte[]? Inserted; // inserted bytes (when !FromBase)
        public long Length;
    }

    // ── Open / close ────────────────────────────────────────────────────────────

    public void Open(string path)
    {
        Close();
        var fi = new FileInfo(path);
        _baseLength = fi.Length;
        FilePath = path;
        _overrides.Clear();
        IsDirty = false;

        if (_baseLength > 0)
        {
            // Read-only mapping so we never copy the whole file into managed memory.
            _mmf = MemoryMappedFile.CreateFromFile(
                new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                mapName: null, capacity: 0, MemoryMappedFileAccess.Read,
                inheritability: System.IO.HandleInheritability.None, leaveOpen: false);
            _view = _mmf.CreateViewAccessor(0, _baseLength, MemoryMappedFileAccess.Read);
        }

        _segments = new List<Segment>();
        if (_baseLength > 0)
            _segments.Add(new Segment { FromBase = true, BaseStart = 0, Length = _baseLength });
        _logicalLength = _baseLength;
    }

    public void Close()
    {
        lock (_gate)
        {
            _view?.Dispose(); _view = null;
            _mmf?.Dispose(); _mmf = null;
            _segments = new List<Segment>();
            _overrides.Clear();
            _logicalLength = 0;
            _baseLength = 0;
            IsDirty = false;
            FilePath = null;
        }
    }

    public void Dispose() => Close();

    // ── Reading ─────────────────────────────────────────────────────────────────

    /// <summary>讀取邏輯位移 <paramref name="offset"/> 開始嘅一段位元組 · Read up to <paramref name="count"/> logical bytes from <paramref name="offset"/>.</summary>
    public int Read(long offset, byte[] buffer, int count)
    {
        if (offset < 0 || offset >= _logicalLength) return 0;
        count = (int)Math.Min(count, _logicalLength - offset);
        int written = 0;
        long pos = 0;
        foreach (var seg in _segments)
        {
            if (written >= count) break;
            long segEnd = pos + seg.Length;
            if (offset < segEnd && offset + (count - written) > pos)
            {
                long localStart = Math.Max(offset, pos) - pos;       // within segment
                long takeable = seg.Length - localStart;
                int take = (int)Math.Min(takeable, count - written);
                long logicalAt = pos + localStart;
                if (seg.FromBase)
                    ReadBase(seg.BaseStart + localStart, buffer, written, take, logicalAt);
                else
                    Array.Copy(seg.Inserted!, (int)localStart, buffer, written, take);
                written += take;
            }
            pos = segEnd;
            if (pos >= offset + count) break;
        }
        return written;
    }

    /// <summary>Read from the base view, layering any per-offset overrides on top.</summary>
    private void ReadBase(long baseStart, byte[] dest, int destOffset, int count, long logicalStart)
    {
        if (_view is null) return;
        for (int i = 0; i < count; i++)
        {
            long logical = logicalStart + i;
            dest[destOffset + i] = _overrides.TryGetValue(logical, out var ov)
                ? ov
                : _view.ReadByte(baseStart + i);
        }
    }

    public byte ReadByteAt(long offset)
    {
        if (offset < 0 || offset >= _logicalLength) return 0;
        var b = new byte[1];
        Read(offset, b, 1);
        return b[0];
    }

    // ── In-place edit (overwrite mode) ───────────────────────────────────────────

    /// <summary>覆寫一個位元組（不改長度）· Overwrite one byte in place (length unchanged).</summary>
    public void Overwrite(long offset, byte value)
    {
        if (offset < 0 || offset >= _logicalLength) return;
        // If the offset maps to inserted bytes, edit them directly; else use the override map.
        long pos = 0;
        foreach (var seg in _segments)
        {
            long segEnd = pos + seg.Length;
            if (offset < segEnd)
            {
                long local = offset - pos;
                if (seg.FromBase) _overrides[seg.BaseStart + local] = value;
                else seg.Inserted![(int)local] = value;
                IsDirty = true;
                return;
            }
            pos = segEnd;
        }
    }

    public bool IsModifiedByte(long offset)
    {
        long pos = 0;
        foreach (var seg in _segments)
        {
            long segEnd = pos + seg.Length;
            if (offset < segEnd)
            {
                if (!seg.FromBase) return true; // inserted bytes count as modified
                return _overrides.ContainsKey(seg.BaseStart + (offset - pos));
            }
            pos = segEnd;
        }
        return false;
    }

    // ── Insert / delete (optional structural edits) ──────────────────────────────

    /// <summary>喺位移度插入位元組 · Insert bytes at a logical offset (grows the file).</summary>
    public void Insert(long offset, byte[] data)
    {
        if (data.Length == 0) return;
        offset = Math.Clamp(offset, 0, _logicalLength);
        var rebuilt = SplitAt(offset);
        int idx = rebuilt.index;
        rebuilt.list.Insert(idx, new Segment { FromBase = false, Inserted = (byte[])data.Clone(), Length = data.Length });
        _segments = rebuilt.list;
        _logicalLength += data.Length;
        IsDirty = true;
    }

    /// <summary>刪除一段位元組 · Delete <paramref name="count"/> bytes at <paramref name="offset"/>.</summary>
    public void Delete(long offset, long count)
    {
        if (count <= 0 || offset < 0 || offset >= _logicalLength) return;
        count = Math.Min(count, _logicalLength - offset);
        var a = SplitAt(offset);
        var list = a.list;
        // Re-split at end of range using the freshly split list.
        long pos = 0; int endIndex = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            if (pos == offset + count) { endIndex = i; break; }
            if (pos + list[i].Length > offset + count)
            {
                // split this segment
                var seg = list[i];
                long local = (offset + count) - pos;
                var left = MakeSub(seg, 0, local);
                var right = MakeSub(seg, local, seg.Length - local);
                list[i] = left; list.Insert(i + 1, right);
                endIndex = i + 1;
                break;
            }
            pos += list[i].Length;
            if (pos == offset + count) { endIndex = i + 1; break; }
        }
        // remove segments covering [offset, offset+count)
        long p = 0; int startIndex = a.index;
        // a.index points to the first segment at/after offset; recompute start index cleanly.
        p = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (p == offset) { startIndex = i; break; }
            p += list[i].Length;
        }
        list.RemoveRange(startIndex, endIndex - startIndex);
        _segments = list;
        _logicalLength -= count;
        IsDirty = true;
    }

    /// <summary>Split the segment list so a boundary exists exactly at <paramref name="offset"/>; returns the list and the index of the segment that starts at offset.</summary>
    private (List<Segment> list, int index) SplitAt(long offset)
    {
        var list = new List<Segment>(_segments.Count + 1);
        long pos = 0; int boundaryIndex = -1;
        foreach (var seg in _segments)
        {
            long segEnd = pos + seg.Length;
            if (offset > pos && offset < segEnd)
            {
                long local = offset - pos;
                list.Add(MakeSub(seg, 0, local));
                boundaryIndex = list.Count;
                list.Add(MakeSub(seg, local, seg.Length - local));
            }
            else
            {
                if (offset == pos && boundaryIndex < 0) boundaryIndex = list.Count;
                list.Add(CloneSeg(seg));
            }
            pos = segEnd;
        }
        if (boundaryIndex < 0) boundaryIndex = list.Count; // offset == logicalLength
        return (list, boundaryIndex);
    }

    private static Segment CloneSeg(Segment s) => new()
    {
        FromBase = s.FromBase, BaseStart = s.BaseStart, Inserted = s.Inserted, Length = s.Length
    };

    private static Segment MakeSub(Segment s, long localStart, long length) => s.FromBase
        ? new Segment { FromBase = true, BaseStart = s.BaseStart + localStart, Length = length }
        : new Segment { FromBase = false, Inserted = Slice(s.Inserted!, (int)localStart, (int)length), Length = length };

    private static byte[] Slice(byte[] src, int start, int len)
    {
        var r = new byte[len];
        Array.Copy(src, start, r, 0, len);
        return r;
    }

    // ── Find ────────────────────────────────────────────────────────────────────

    /// <summary>由 <paramref name="from"/> 開始向前搵 pattern；搵唔到回傳 -1 · Forward search; -1 if not found.</summary>
    public long Find(byte[] pattern, long from, CancellationToken ct = default)
    {
        if (pattern.Length == 0 || _logicalLength == 0) return -1;
        from = Math.Clamp(from, 0, Math.Max(0, _logicalLength - 1));
        const int Chunk = 1 << 20; // 1 MiB scan window
        var buf = new byte[Chunk + pattern.Length - 1];
        long pos = from;
        while (pos < _logicalLength)
        {
            ct.ThrowIfCancellationRequested();
            int got = Read(pos, buf, buf.Length);
            if (got < pattern.Length) break;
            int limit = got - pattern.Length;
            for (int i = 0; i <= limit; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                    if (buf[i + j] != pattern[j]) { match = false; break; }
                if (match) return pos + i;
            }
            pos += got - (pattern.Length - 1);
        }
        return -1;
    }

    /// <summary>由 ASCII/十六進位字串建構 pattern · Build a search pattern from text or a hex string.</summary>
    public static byte[]? ParsePattern(string input, bool asHex, Encoding? encoding = null)
    {
        if (string.IsNullOrEmpty(input)) return null;
        if (!asHex) return (encoding ?? Encoding.UTF8).GetBytes(input);
        var clean = new StringBuilder();
        foreach (var c in input)
            if (Uri.IsHexDigit(c)) clean.Append(c);
        var hex = clean.ToString();
        if (hex.Length == 0 || hex.Length % 2 != 0) return null;
        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return bytes;
    }

    // ── Save ────────────────────────────────────────────────────────────────────

    /// <summary>儲存到原檔（透過臨時檔再換入）· Save to the current path via a temp file + atomic replace.</summary>
    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (FilePath is null) throw new InvalidOperationException("No file open.");
        await SaveAsAsync(FilePath, ct);
    }

    /// <summary>另存新檔 · Save the current logical bytes to <paramref name="path"/>.</summary>
    public async Task SaveAsAsync(string path, CancellationToken ct = default)
    {
        bool overwritingSelf = FilePath is not null &&
            string.Equals(Path.GetFullPath(path), Path.GetFullPath(FilePath), StringComparison.OrdinalIgnoreCase);
        string tmp = path + ".winforge.tmp";

        await Task.Run(() =>
        {
            using (var outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var buf = new byte[1 << 20];
                long pos = 0;
                while (pos < _logicalLength)
                {
                    ct.ThrowIfCancellationRequested();
                    int got = Read(pos, buf, buf.Length);
                    if (got <= 0) break;
                    outFs.Write(buf, 0, got);
                    pos += got;
                }
                outFs.Flush(true);
            }

            if (overwritingSelf)
            {
                // Release the base view so the original file can be replaced.
                _view?.Dispose(); _view = null;
                _mmf?.Dispose(); _mmf = null;
            }

            if (File.Exists(path))
            {
                try { File.Replace(tmp, path, null); }
                catch { File.Copy(tmp, path, true); File.Delete(tmp); }
            }
            else
            {
                File.Move(tmp, path);
            }
        }, ct);

        // Re-open the saved file so the editor now reflects the persisted bytes cleanly.
        Open(path);
    }

    // ── Hashes ──────────────────────────────────────────────────────────────────

    public sealed record Hashes(string Md5, string Sha1, string Sha256);

    /// <summary>計算目前邏輯內容嘅雜湊（全部純 C#）· Compute MD5/SHA-1/SHA-256 over the current logical content.</summary>
    public async Task<Hashes> ComputeHashesAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var md5 = MD5.Create();
            using var sha1 = SHA1.Create();
            using var sha256 = SHA256.Create();
            var buf = new byte[1 << 20];
            long pos = 0;
            while (pos < _logicalLength)
            {
                ct.ThrowIfCancellationRequested();
                int got = Read(pos, buf, buf.Length);
                if (got <= 0) break;
                md5.TransformBlock(buf, 0, got, null, 0);
                sha1.TransformBlock(buf, 0, got, null, 0);
                sha256.TransformBlock(buf, 0, got, null, 0);
                pos += got;
                progress?.Report(_logicalLength == 0 ? 1 : (double)pos / _logicalLength);
            }
            md5.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return new Hashes(Hex(md5.Hash!), Hex(sha1.Hash!), Hex(sha256.Hash!));
        }, ct);
    }

    private static string Hex(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 2);
        foreach (var x in b) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    public static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes:N0} B" : $"{v:0.##} {u[i]} ({bytes:N0} B)";
    }
}
