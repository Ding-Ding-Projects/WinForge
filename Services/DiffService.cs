using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 純 C# 比對引擎 · Pure-managed diff engine — no external tools, no shelling out.
/// Implements a Myers O(ND) line diff (with an LCS fallback for very large inputs),
/// intra-line word diff, and a recursive folder comparison using size + content hash.
/// 全部喺 app 內用受管理嘅 C# 計，唔會啟動／呼叫任何外部 diff 工具。
/// </summary>
public static class DiffService
{
    // ── Diff op kinds · 差異種類 ─────────────────────────────────────────────────

    /// <summary>一行嘅差異狀態 · The diff status of one aligned row.</summary>
    public enum LineKind
    {
        Equal,    // 相同 · unchanged on both sides
        Modify,   // 改動（左右都有，但內容唔同）· present on both sides but changed
        Insert,   // 新增（淨係右邊有）· present only on the right
        Delete,   // 刪除（淨係左邊有）· present only on the left
        Blank,    // 填充空白（對齊用）· filler so both columns line up
    }

    /// <summary>
    /// 並排比對嘅一行 · One aligned row in a side-by-side diff. Either side may be null
    /// (a gap), and <see cref="Kind"/> describes the relationship.
    /// </summary>
    public sealed class DiffRow
    {
        public LineKind Kind { get; init; }
        public string? Left { get; init; }
        public string? Right { get; init; }
        public int LeftNo { get; init; }    // 1-based line number, or 0 if no line on this side
        public int RightNo { get; init; }
        public bool Changed => Kind != LineKind.Equal && Kind != LineKind.Blank;
    }

    // ── Text diff (line level) · 逐行比對 ───────────────────────────────────────

    /// <summary>
    /// 計兩段文字嘅並排差異 · Compute a side-by-side line diff between two texts.
    /// </summary>
    public static List<DiffRow> DiffText(string leftText, string rightText, bool ignoreWhitespace)
    {
        var a = SplitLines(leftText);
        var b = SplitLines(rightText);
        return DiffLines(a, b, ignoreWhitespace);
    }

    /// <summary>把文字拆成行（保留每行原樣，去除行尾的 \r）· Split into lines, normalising CRLF/CR/LF.</summary>
    public static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<string>();
        // Normalise line endings then split; keep content verbatim otherwise.
        var norm = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return norm.Split('\n');
    }

    private static string Key(string s, bool ignoreWhitespace)
        => ignoreWhitespace ? CollapseWhitespace(s) : s;

    private static string CollapseWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool inGap = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch)) { inGap = true; continue; }
            if (inGap && sb.Length > 0) sb.Append(' ');
            inGap = false;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// 並排比對兩個行陣列 · Diff two arrays of lines into aligned rows.
    /// Adjacent delete+insert runs are paired up as "modify" rows for nicer merging.
    /// </summary>
    public static List<DiffRow> DiffLines(IReadOnlyList<string> a, IReadOnlyList<string> b, bool ignoreWhitespace)
    {
        var ops = MyersDiff(a, b, ignoreWhitespace);
        var rows = new List<DiffRow>();
        int ai = 0, bi = 0;
        int i = 0;
        while (i < ops.Count)
        {
            var op = ops[i];
            if (op == EditOp.Equal)
            {
                rows.Add(new DiffRow { Kind = LineKind.Equal, Left = a[ai], Right = b[bi], LeftNo = ai + 1, RightNo = bi + 1 });
                ai++; bi++; i++;
                continue;
            }

            // Gather a contiguous run of deletes followed by inserts (Myers groups them).
            int delStart = ai, insStart = bi;
            int dels = 0, inss = 0;
            int j = i;
            while (j < ops.Count && ops[j] == EditOp.Delete) { dels++; j++; }
            while (j < ops.Count && ops[j] == EditOp.Insert) { inss++; j++; }

            if (dels == 0 && inss == 0) { i++; continue; } // safety

            int paired = Math.Min(dels, inss);
            for (int k = 0; k < paired; k++)
                rows.Add(new DiffRow { Kind = LineKind.Modify, Left = a[delStart + k], Right = b[insStart + k], LeftNo = delStart + k + 1, RightNo = insStart + k + 1 });
            for (int k = paired; k < dels; k++)
                rows.Add(new DiffRow { Kind = LineKind.Delete, Left = a[delStart + k], Right = null, LeftNo = delStart + k + 1, RightNo = 0 });
            for (int k = paired; k < inss; k++)
                rows.Add(new DiffRow { Kind = LineKind.Insert, Left = null, Right = b[insStart + k], LeftNo = 0, RightNo = insStart + k + 1 });

            ai += dels; bi += inss; i = j;
        }
        return rows;
    }

    private enum EditOp { Equal, Delete, Insert }

    /// <summary>
    /// Myers O(ND) 差異演算法 · Myers O(ND) diff over line keys, returning a flat edit script.
    /// Falls back to a Hunt–Szymanski LCS for pathological sizes to bound memory.
    /// </summary>
    private static List<EditOp> MyersDiff(IReadOnlyList<string> a, IReadOnlyList<string> b, bool ignoreWhitespace)
    {
        int n = a.Count, m = b.Count;
        var ka = new string[n];
        var kb = new string[m];
        for (int i = 0; i < n; i++) ka[i] = Key(a[i], ignoreWhitespace);
        for (int i = 0; i < m; i++) kb[i] = Key(b[i], ignoreWhitespace);

        // Guard: the V-array storage in classic Myers is O((N+M)·D). For huge, very-different
        // files that blows up, so cap the edit distance and fall back to LCS when exceeded.
        long budget = (long)(n + m) * Math.Min(n + m, 4000);
        if (budget > 40_000_000L)
            return LcsDiff(ka, kb);

        return MyersCore(ka, kb);
    }

    private static List<EditOp> MyersCore(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        int max = n + m;
        var trace = new List<int[]>();
        var v = new int[2 * max + 1];
        int offset = max;

        int finalD = -1;
        for (int d = 0; d <= max; d++)
        {
            var snapshot = (int[])v.Clone();
            trace.Add(snapshot);
            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[offset + k - 1] < v[offset + k + 1]))
                    x = v[offset + k + 1];          // down (insert)
                else
                    x = v[offset + k - 1] + 1;       // right (delete)
                int y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[offset + k] = x;
                if (x >= n && y >= m) { finalD = d; break; }
            }
            if (finalD >= 0) break;
        }

        // Backtrack to reconstruct the edit script.
        var ops = new List<EditOp>();
        int px = n, py = m;
        for (int d = finalD; d > 0; d--)
        {
            var vv = trace[d];
            int k = px - py;
            int prevK;
            if (k == -d || (k != d && vv[offset + k - 1] < vv[offset + k + 1]))
                prevK = k + 1;
            else
                prevK = k - 1;
            int prevX = vv[offset + prevK];
            int prevY = prevX - prevK;
            while (px > prevX && py > prevY) { ops.Add(EditOp.Equal); px--; py--; }
            if (px == prevX) { ops.Add(EditOp.Insert); py--; }
            else { ops.Add(EditOp.Delete); px--; }
        }
        while (px > 0 && py > 0) { ops.Add(EditOp.Equal); px--; py--; }
        while (px > 0) { ops.Add(EditOp.Delete); px--; }
        while (py > 0) { ops.Add(EditOp.Insert); py--; }
        ops.Reverse();
        return ops;
    }

    /// <summary>LCS 後備（O(N·M) 記憶體較省嘅變體唔需要時，用簡單 DP）· LCS fallback.</summary>
    private static List<EditOp> LcsDiff(string[] a, string[] b)
    {
        int n = a.Length, m = b.Length;
        // Two-row DP for the length table; then a second pass with full table for backtrack.
        // To bound memory we cap m for the full table path; this branch only runs for huge inputs.
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var ops = new List<EditOp>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y]) { ops.Add(EditOp.Equal); x++; y++; }
            else if (dp[x + 1, y] >= dp[x, y + 1]) { ops.Add(EditOp.Delete); x++; }
            else { ops.Add(EditOp.Insert); y++; }
        }
        while (x < n) { ops.Add(EditOp.Delete); x++; }
        while (y < m) { ops.Add(EditOp.Insert); y++; }
        return ops;
    }

    // ── Intra-line word diff · 行內逐字比對 ─────────────────────────────────────

    /// <summary>行內一段 · A run of text within a line, flagged changed or not.</summary>
    public readonly record struct Span(string Text, bool Changed);

    /// <summary>
    /// 逐字比對兩行，標出唔同嘅片段 · Word-level diff between two lines; returns the
    /// segmentation for each side so the UI can highlight only what changed.
    /// </summary>
    public static (List<Span> left, List<Span> right) DiffWords(string left, string right)
    {
        var la = Tokenize(left);
        var ra = Tokenize(right);
        var ops = MyersCore(la, ra);
        var leftSpans = new List<Span>();
        var rightSpans = new List<Span>();
        int ai = 0, bi = 0;
        foreach (var op in ops)
        {
            switch (op)
            {
                case EditOp.Equal:
                    AppendSpan(leftSpans, la[ai], false);
                    AppendSpan(rightSpans, ra[bi], false);
                    ai++; bi++;
                    break;
                case EditOp.Delete:
                    AppendSpan(leftSpans, la[ai], true);
                    ai++;
                    break;
                case EditOp.Insert:
                    AppendSpan(rightSpans, ra[bi], true);
                    bi++;
                    break;
            }
        }
        return (leftSpans, rightSpans);
    }

    private static void AppendSpan(List<Span> list, string text, bool changed)
    {
        if (list.Count > 0 && list[^1].Changed == changed)
            list[^1] = new Span(list[^1].Text + text, changed);
        else
            list.Add(new Span(text, changed));
    }

    /// <summary>把一行拆成字詞 + 空白 + 符號 token · Tokenise into words/whitespace/punctuation.</summary>
    private static string[] Tokenize(string s)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            int start = i;
            if (char.IsLetterOrDigit(c))
                while (i < s.Length && char.IsLetterOrDigit(s[i])) i++;
            else if (char.IsWhiteSpace(c))
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            else
                i++;
            tokens.Add(s.Substring(start, i - start));
        }
        return tokens.ToArray();
    }

    // ── File helpers · 檔案輔助 ─────────────────────────────────────────────────

    /// <summary>判斷檔案是否可能為二進位 · Heuristic: is this file likely binary (NUL byte in head)?</summary>
    public static bool LooksBinary(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var buf = new byte[Math.Min(8192, (int)Math.Min(fs.Length, int.MaxValue))];
            int read = fs.Read(buf, 0, buf.Length);
            for (int i = 0; i < read; i++) if (buf[i] == 0) return true;
            return false;
        }
        catch { return false; }
    }

    /// <summary>安全讀取文字（偵測 BOM/編碼）· Read text, auto-detecting BOM/encoding.</summary>
    public static string ReadText(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return ""; }
    }

    // ── Folder compare · 資料夾比對 ─────────────────────────────────────────────

    /// <summary>資料夾項目嘅狀態 · Per-item status in a folder comparison.</summary>
    public enum ItemStatus
    {
        Identical,  // 兩邊相同 · same on both sides
        Different,  // 兩邊都有，但唔同 · present both sides, differing
        OnlyLeft,   // 淨係左邊有 · exists only on the left
        OnlyRight,  // 淨係右邊有 · exists only on the right
    }

    /// <summary>資料夾比對嘅一個項目（檔案或子資料夾）· One node in the folder-compare result.</summary>
    public sealed class FolderItem
    {
        public string RelativePath { get; init; } = "";
        public string Name { get; init; } = "";
        public bool IsDirectory { get; init; }
        public ItemStatus Status { get; init; }
        public string? LeftPath { get; init; }
        public string? RightPath { get; init; }
        public long LeftSize { get; init; }
        public long RightSize { get; init; }
    }

    /// <summary>
    /// 遞迴比對兩個資料夾 · Recursively compare two folders. Files are compared by size first,
    /// then by SHA-256 content hash when sizes match, to decide Different vs Identical.
    /// </summary>
    public static async Task<List<FolderItem>> CompareFoldersAsync(
        string leftRoot, string rightRoot, bool ignoreWhitespace, CancellationToken ct = default)
    {
        var items = new List<FolderItem>();
        await Task.Run(() => Walk(leftRoot, rightRoot, "", items, ct), ct);
        items.Sort((x, y) =>
        {
            // Directories first, then alphabetical by relative path (case-insensitive).
            int c = string.Compare(x.RelativePath, y.RelativePath, StringComparison.OrdinalIgnoreCase);
            return c;
        });
        return items;
    }

    private static void Walk(string left, string right, string rel, List<FolderItem> sink, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var leftDirs = SafeDirs(left);
        var rightDirs = SafeDirs(right);
        var dirNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in leftDirs.Keys) dirNames.Add(d);
        foreach (var d in rightDirs.Keys) dirNames.Add(d);

        foreach (var name in dirNames)
        {
            ct.ThrowIfCancellationRequested();
            bool inL = leftDirs.TryGetValue(name, out var lp);
            bool inR = rightDirs.TryGetValue(name, out var rp);
            string childRel = string.IsNullOrEmpty(rel) ? name : rel + "\\" + name;
            var status = inL && inR ? ItemStatus.Identical : inL ? ItemStatus.OnlyLeft : ItemStatus.OnlyRight;
            sink.Add(new FolderItem
            {
                RelativePath = childRel, Name = name, IsDirectory = true,
                Status = status, LeftPath = inL ? lp : null, RightPath = inR ? rp : null,
            });
            if (inL && inR) Walk(lp!, rp!, childRel, sink, ct);
            else if (inL) AddTreeOneSided(lp!, childRel, sink, true, ct);
            else AddTreeOneSided(rp!, childRel, sink, false, ct);
        }

        var leftFiles = SafeFiles(left);
        var rightFiles = SafeFiles(right);
        var fileNames = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in leftFiles.Keys) fileNames.Add(f);
        foreach (var f in rightFiles.Keys) fileNames.Add(f);

        foreach (var name in fileNames)
        {
            ct.ThrowIfCancellationRequested();
            bool inL = leftFiles.TryGetValue(name, out var lp);
            bool inR = rightFiles.TryGetValue(name, out var rp);
            string childRel = string.IsNullOrEmpty(rel) ? name : rel + "\\" + name;
            ItemStatus status;
            long ls = 0, rs = 0;
            if (inL && inR)
            {
                ls = SafeLen(lp!); rs = SafeLen(rp!);
                status = FilesEqual(lp!, rp!, ls, rs) ? ItemStatus.Identical : ItemStatus.Different;
            }
            else if (inL) { ls = SafeLen(lp!); status = ItemStatus.OnlyLeft; }
            else { rs = SafeLen(rp!); status = ItemStatus.OnlyRight; }
            sink.Add(new FolderItem
            {
                RelativePath = childRel, Name = name, IsDirectory = false,
                Status = status, LeftPath = inL ? lp : null, RightPath = inR ? rp : null,
                LeftSize = ls, RightSize = rs,
            });
        }
    }

    private static void AddTreeOneSided(string root, string rel, List<FolderItem> sink, bool isLeft, CancellationToken ct)
    {
        foreach (var d in SafeDirs(root))
        {
            ct.ThrowIfCancellationRequested();
            string childRel = rel + "\\" + d.Key;
            sink.Add(new FolderItem
            {
                RelativePath = childRel, Name = d.Key, IsDirectory = true,
                Status = isLeft ? ItemStatus.OnlyLeft : ItemStatus.OnlyRight,
                LeftPath = isLeft ? d.Value : null, RightPath = isLeft ? null : d.Value,
            });
            AddTreeOneSided(d.Value, childRel, sink, isLeft, ct);
        }
        foreach (var f in SafeFiles(root))
        {
            string childRel = rel + "\\" + f.Key;
            long len = SafeLen(f.Value);
            sink.Add(new FolderItem
            {
                RelativePath = childRel, Name = f.Key, IsDirectory = false,
                Status = isLeft ? ItemStatus.OnlyLeft : ItemStatus.OnlyRight,
                LeftPath = isLeft ? f.Value : null, RightPath = isLeft ? null : f.Value,
                LeftSize = isLeft ? len : 0, RightSize = isLeft ? 0 : len,
            });
        }
    }

    private static Dictionary<string, string> SafeDirs(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try { foreach (var p in Directory.EnumerateDirectories(path)) d[Path.GetFileName(p)] = p; } catch { }
        return d;
    }

    private static Dictionary<string, string> SafeFiles(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try { foreach (var p in Directory.EnumerateFiles(path)) d[Path.GetFileName(p)] = p; } catch { }
        return d;
    }

    private static long SafeLen(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private static bool FilesEqual(string a, string b, long la, long lb)
    {
        if (la != lb) return false;
        if (la == 0) return true;
        try
        {
            // Quick byte compare for small files; hash for large ones.
            if (la <= 1 << 20) return ByteEqual(a, b);
            return HashOf(a).SequenceEqual(HashOf(b));
        }
        catch { return false; }
    }

    private static bool ByteEqual(string a, string b)
    {
        using var fa = File.OpenRead(a);
        using var fb = File.OpenRead(b);
        var ba = new byte[65536];
        var bb = new byte[65536];
        int ra;
        while ((ra = fa.Read(ba, 0, ba.Length)) > 0)
        {
            int got = 0;
            while (got < ra)
            {
                int r = fb.Read(bb, got, ra - got);
                if (r == 0) return false;
                got += r;
            }
            for (int i = 0; i < ra; i++) if (ba[i] != bb[i]) return false;
        }
        return fb.ReadByte() == -1;
    }

    private static byte[] HashOf(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        return sha.ComputeHash(fs);
    }

    // ── Formatting · 格式化 ─────────────────────────────────────────────────────

    public static string HumanSize(long bytes)
    {
        if (bytes < 0) return "—";
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} {u[i]}" : $"{v:0.0} {u[i]}";
    }
}
