using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace WinForge.Services;

/// <summary>資料庫物件種類 · The kind of SQLite schema object.</summary>
public enum SqliteObjectKind { Table, View, Index, Trigger }

/// <summary>一個結構描述物件（表／檢視／索引）· One schema object (table / view / index / trigger).</summary>
public sealed class SqliteObject
{
    public SqliteObjectKind Kind { get; init; }
    public string Name { get; init; } = "";
    public string Sql { get; init; } = "";
    /// <summary>檢視同索引等唔可以直接編輯資料 · Whether rows can be edited (base tables only).</summary>
    public bool IsEditable => Kind == SqliteObjectKind.Table;
}

/// <summary>一條欄位定義 · One column definition (name, type, pk, notnull, default).</summary>
public sealed class SqliteColumn
{
    public string Name { get; init; } = "";
    public string Type { get; init; } = "";
    public bool NotNull { get; init; }
    public bool Pk { get; init; }
    public string? DefaultValue { get; init; }
}

/// <summary>查詢結果（欄位、列、計時、受影響列數）· A query result: columns, rows, timing, affected rows.</summary>
public sealed class SqliteQueryResult
{
    public bool Success { get; init; }
    public string? ErrorEn { get; init; }
    public string? ErrorZh { get; init; }
    public List<string> Columns { get; init; } = new();
    public List<string?[]> Rows { get; init; } = new();
    public int AffectedRows { get; init; }
    public bool HasResultSet { get; init; }
    public long ElapsedMs { get; init; }
    public bool Truncated { get; init; }
    public string? StatusEn { get; init; }
    public string? StatusZh { get; init; }
}

/// <summary>分頁資料瀏覽結果 · A paged data-browse result for one table.</summary>
public sealed class SqlitePage
{
    public List<string> Columns { get; init; } = new();
    /// <summary>每列嘅原始 cell 值（null = SQL NULL）· Raw cell values per row (null = SQL NULL).</summary>
    public List<string?[]> Rows { get; init; } = new();
    public long TotalRows { get; init; }
    /// <summary>主鍵欄位名（用於 WHERE 寫回；空 = 只能用 rowid）· PK column names for write-back.</summary>
    public List<string> PrimaryKeys { get; init; } = new();
    /// <summary>有冇 rowid（沒主鍵時用 rowid 定位列）· Whether the table exposes a rowid.</summary>
    public bool HasRowId { get; init; }
    /// <summary>每列嘅 rowid（與 Rows 對齊；HasRowId 時有效）· rowid per row, aligned with Rows.</summary>
    public List<long> RowIds { get; init; } = new();
}

/// <summary>
/// 原生 SQLite 服務（包住 Microsoft.Data.Sqlite）· The native SQLite service.
/// 純託管包裝：Microsoft.Data.Sqlite 透過 SQLitePCLRaw 內附 SQLite 原生引擎，
/// 唔需要、亦唔會啟動任何外部程式（DB Browser、sqlite3.exe 等）。
/// MANAGED WRAPPER: Microsoft.Data.Sqlite bundles the SQLite native engine via SQLitePCLRaw.
/// No external tool (DB Browser for SQLite, sqlite3.exe, …) is ever launched, shelled, or bundled.
/// 提供開啟／建立 .db、結構描述瀏覽、分頁資料瀏覽、單元格／列編輯、跑任意 SQL、CSV 匯出。
/// All operations are async, defensive, and never throw — errors are returned as bilingual messages.
/// </summary>
public static class SqliteService
{
    /// <summary>預設渲染上限，避免巨大結果集塞爆 UI · Default cap on rendered rows for ad-hoc SQL.</summary>
    public const int DefaultRowCap = 5000;

    public const string NullToken = "(null)";

    // ===================== 連線字串 · Connection string =====================

    public static string BuildConnectionString(string path, bool createIfMissing)
        => new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = createIfMissing ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Cache = SqliteCacheMode.Default,
            ForeignKeys = true,
        }.ConnectionString;

    /// <summary>開啟一個連線（必要時建立檔案）· Open a connection, creating the file if asked.</summary>
    private static SqliteConnection Open(string path, bool createIfMissing = false)
    {
        var conn = new SqliteConnection(BuildConnectionString(path, createIfMissing));
        conn.Open();
        return conn;
    }

    // ===================== 開啟 / 建立 · Open / create =====================

    /// <summary>測試／開啟一個 .db 檔（確認可讀）· Open and validate a database file.</summary>
    public static async Task<(bool ok, string? sqliteVersion, string? errEn, string? errZh)>
        OpenDatabaseAsync(string path, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(path))
                return (false, null, "File not found.", "搵唔到檔案。");
            return await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT sqlite_version();";
                var ver = cmd.ExecuteScalar()?.ToString();
                return (true, ver, (string?)null, (string?)null);
            }, ct);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message, $"開啟失敗：{ex.Message}");
        }
    }

    /// <summary>建立一個全新空白資料庫檔 · Create a brand-new empty database file.</summary>
    public static async Task<(bool ok, string? errEn, string? errZh)> CreateDatabaseAsync(string path, CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var conn = Open(path, createIfMissing: true);
                // Touch the database so a real, valid file lands on disk (header written on first write).
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA user_version;";
                cmd.ExecuteScalar();
                return (true, (string?)null, (string?)null);
            }, ct);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, $"建立失敗：{ex.Message}");
        }
    }

    // ===================== 結構描述 · Schema =====================

    /// <summary>列出所有結構描述物件 · List all schema objects (tables, views, indexes, triggers).</summary>
    public static async Task<List<SqliteObject>> ListObjectsAsync(string path, CancellationToken ct = default)
    {
        var list = new List<SqliteObject>();
        try
        {
            await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT type, name, COALESCE(sql,'') FROM sqlite_master " +
                    "WHERE name NOT LIKE 'sqlite_%' AND type IN ('table','view','index','trigger') " +
                    "ORDER BY type, name;";
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    var type = rd.GetString(0);
                    list.Add(new SqliteObject
                    {
                        Kind = type switch
                        {
                            "view" => SqliteObjectKind.View,
                            "index" => SqliteObjectKind.Index,
                            "trigger" => SqliteObjectKind.Trigger,
                            _ => SqliteObjectKind.Table,
                        },
                        Name = rd.GetString(1),
                        Sql = rd.GetString(2),
                    });
                }
            }, ct);
        }
        catch { /* surfaced as empty list */ }
        return list;
    }

    /// <summary>列出一個表／檢視嘅欄位 · List columns of a table or view via PRAGMA table_info.</summary>
    public static async Task<List<SqliteColumn>> ListColumnsAsync(string path, string objectName, CancellationToken ct = default)
    {
        var cols = new List<SqliteColumn>();
        try
        {
            await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                // table_info takes a quoted identifier; double-quote and escape embedded quotes.
                cmd.CommandText = $"PRAGMA table_info({QuoteId(objectName)});";
                using var rd = cmd.ExecuteReader();
                while (rd.Read())
                {
                    cols.Add(new SqliteColumn
                    {
                        Name = rd.IsDBNull(1) ? "" : rd.GetString(1),
                        Type = rd.IsDBNull(2) ? "" : rd.GetString(2),
                        NotNull = !rd.IsDBNull(3) && rd.GetInt32(3) != 0,
                        DefaultValue = rd.IsDBNull(4) ? null : rd.GetValue(4)?.ToString(),
                        Pk = !rd.IsDBNull(5) && rd.GetInt32(5) != 0,
                    });
                }
            }, ct);
        }
        catch { /* best effort */ }
        return cols;
    }

    // ===================== 分頁資料瀏覽 · Paged data browse =====================

    /// <summary>讀取一個表嘅一頁資料 · Read one page of rows from a table.</summary>
    public static async Task<SqlitePage> BrowsePageAsync(
        string path, string table, int pageSize, int pageIndex, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var conn = Open(path);

            // Determine PK columns + whether a usable rowid exists (WITHOUT ROWID tables have none).
            var pks = new List<string>();
            bool hasRowId = false;
            using (var pragma = conn.CreateCommand())
            {
                pragma.CommandText = $"PRAGMA table_info({QuoteId(table)});";
                using var prd = pragma.ExecuteReader();
                while (prd.Read())
                {
                    bool pk = !prd.IsDBNull(5) && prd.GetInt32(5) != 0;
                    if (pk) pks.Add(prd.GetString(1));
                }
            }
            hasRowId = ProbeRowId(conn, table);

            long total = 0;
            using (var countCmd = conn.CreateCommand())
            {
                countCmd.CommandText = $"SELECT COUNT(*) FROM {QuoteId(table)};";
                total = Convert.ToInt64(countCmd.ExecuteScalar() ?? 0L);
            }

            var page = new SqlitePage { TotalRows = total, PrimaryKeys = pks, HasRowId = hasRowId };

            using var cmd = conn.CreateCommand();
            var selectCols = hasRowId ? "rowid AS _wf_rowid, *" : "*";
            cmd.CommandText = $"SELECT {selectCols} FROM {QuoteId(table)} LIMIT $take OFFSET $skip;";
            cmd.Parameters.AddWithValue("$take", pageSize);
            cmd.Parameters.AddWithValue("$skip", (long)pageIndex * pageSize);
            using var rd = cmd.ExecuteReader();

            int startCol = hasRowId ? 1 : 0;
            for (int i = startCol; i < rd.FieldCount; i++) page.Columns.Add(rd.GetName(i));

            while (rd.Read())
            {
                if (hasRowId) page.RowIds.Add(rd.IsDBNull(0) ? 0L : rd.GetInt64(0));
                var row = new string?[page.Columns.Count];
                for (int i = startCol; i < rd.FieldCount; i++)
                    row[i - startCol] = rd.IsDBNull(i) ? null : Stringify(rd.GetValue(i));
                page.Rows.Add(row);
            }
            return page;
        }, ct);
    }

    /// <summary>嘗試查詢 rowid，失敗即代表 WITHOUT ROWID 表 · Probe whether the table has a rowid.</summary>
    private static bool ProbeRowId(SqliteConnection conn, string table)
    {
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT rowid FROM {QuoteId(table)} LIMIT 0;";
            using var rd = cmd.ExecuteReader();
            return true;
        }
        catch { return false; }
    }

    // ===================== 編輯：更新 / 插入 / 刪除 · Edit: update / insert / delete =====================

    /// <summary>
    /// 更新一個單元格（用 rowid 或主鍵定位）· Update one cell, located by rowid (preferred) or PK values.
    /// pkValues 對應 page.PrimaryKeys 嘅原始字串值 · pkValues map to page.PrimaryKeys raw string values.
    /// </summary>
    public static async Task<(bool ok, string? errEn, string? errZh)> UpdateCellAsync(
        string path, string table, string column, string? newValue,
        bool hasRowId, long rowId, IReadOnlyList<string> pkColumns, IReadOnlyList<string?> pkValues,
        CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                var (whereSql, bindWhere) = BuildLocator(hasRowId, rowId, pkColumns, pkValues, cmd);
                if (whereSql is null)
                    return (false, "This row cannot be located for editing (no primary key or rowid).",
                        "呢一列無法定位以作編輯（無主鍵亦無 rowid）。");
                cmd.CommandText = $"UPDATE {QuoteId(table)} SET {QuoteId(column)} = $val WHERE {whereSql};";
                BindValue(cmd, "$val", newValue);
                bindWhere();
                int n = cmd.ExecuteNonQuery();
                if (n == 0) return (false, "No row was updated.", "無列被更新。");
                return (true, (string?)null, (string?)null);
            }, ct);
        }
        catch (Exception ex) { return (false, ex.Message, $"更新失敗：{ex.Message}"); }
    }

    /// <summary>插入一列（values 對齊 columns；null = SQL NULL）· Insert one row.</summary>
    public static async Task<(bool ok, string? errEn, string? errZh)> InsertRowAsync(
        string path, string table, IReadOnlyList<string> columns, IReadOnlyList<string?> values, CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                var colSql = string.Join(", ", columns.Select(QuoteId));
                var paramSql = string.Join(", ", columns.Select((_, i) => "$p" + i));
                cmd.CommandText = $"INSERT INTO {QuoteId(table)} ({colSql}) VALUES ({paramSql});";
                for (int i = 0; i < columns.Count; i++)
                    BindValue(cmd, "$p" + i, values[i]);
                cmd.ExecuteNonQuery();
                return (true, (string?)null, (string?)null);
            }, ct);
        }
        catch (Exception ex) { return (false, ex.Message, $"插入失敗：{ex.Message}"); }
    }

    /// <summary>刪除一列（用 rowid 或主鍵定位）· Delete one row, located by rowid or PK values.</summary>
    public static async Task<(bool ok, string? errEn, string? errZh)> DeleteRowAsync(
        string path, string table, bool hasRowId, long rowId,
        IReadOnlyList<string> pkColumns, IReadOnlyList<string?> pkValues, CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                var (whereSql, bindWhere) = BuildLocator(hasRowId, rowId, pkColumns, pkValues, cmd);
                if (whereSql is null)
                    return (false, "This row cannot be located for deletion (no primary key or rowid).",
                        "呢一列無法定位以作刪除（無主鍵亦無 rowid）。");
                cmd.CommandText = $"DELETE FROM {QuoteId(table)} WHERE {whereSql};";
                bindWhere();
                int n = cmd.ExecuteNonQuery();
                if (n == 0) return (false, "No row was deleted.", "無列被刪除。");
                return (true, (string?)null, (string?)null);
            }, ct);
        }
        catch (Exception ex) { return (false, ex.Message, $"刪除失敗：{ex.Message}"); }
    }

    /// <summary>建構一行定位 WHERE 子句（rowid 優先，否則主鍵）· Build the row-locator WHERE clause.</summary>
    private static (string? whereSql, Action bind) BuildLocator(
        bool hasRowId, long rowId, IReadOnlyList<string> pkColumns, IReadOnlyList<string?> pkValues, SqliteCommand cmd)
    {
        if (hasRowId)
        {
            return ("rowid = $wf_rowid", () => cmd.Parameters.AddWithValue("$wf_rowid", rowId));
        }
        if (pkColumns.Count > 0 && pkColumns.Count == pkValues.Count)
        {
            var parts = pkColumns.Select((c, i) => $"{QuoteId(c)} = $w{i}");
            return (string.Join(" AND ", parts), () =>
            {
                for (int i = 0; i < pkColumns.Count; i++) BindValue(cmd, "$w" + i, pkValues[i]);
            });
        }
        return (null, () => { });
    }

    // ===================== 跑任意 SQL · Run arbitrary SQL =====================

    public static async Task<SqliteQueryResult> RunSqlAsync(
        string path, string sql, int rowCap = DefaultRowCap, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                using var rd = cmd.ExecuteReader();

                if (rd.FieldCount > 0)
                {
                    var cols = new List<string>();
                    for (int i = 0; i < rd.FieldCount; i++) cols.Add(rd.GetName(i));
                    var rows = new List<string?[]>();
                    bool truncated = false;
                    while (rd.Read())
                    {
                        if (rows.Count >= rowCap) { truncated = true; break; }
                        var row = new string?[rd.FieldCount];
                        for (int i = 0; i < rd.FieldCount; i++)
                            row[i] = rd.IsDBNull(i) ? null : Stringify(rd.GetValue(i));
                        rows.Add(row);
                    }
                    sw.Stop();
                    return new SqliteQueryResult
                    {
                        Success = true, HasResultSet = true, Columns = cols, Rows = rows,
                        ElapsedMs = sw.ElapsedMilliseconds, Truncated = truncated, AffectedRows = rows.Count,
                    };
                }

                int affected = rd.RecordsAffected;
                sw.Stop();
                return new SqliteQueryResult
                {
                    Success = true, HasResultSet = false,
                    AffectedRows = affected < 0 ? 0 : affected, ElapsedMs = sw.ElapsedMilliseconds,
                    StatusEn = $"Command OK — {(affected < 0 ? 0 : affected)} row(s) affected.",
                    StatusZh = $"指令完成 — 影響 {(affected < 0 ? 0 : affected)} 列。",
                };
            }, ct);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new SqliteQueryResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorEn = "Query cancelled.", ErrorZh = "查詢已取消。" };
        }
        catch (SqliteException sx)
        {
            sw.Stop();
            return new SqliteQueryResult
            {
                Success = false, ElapsedMs = sw.ElapsedMilliseconds,
                ErrorEn = $"[SQLITE {sx.SqliteErrorCode}] {sx.Message}",
                ErrorZh = $"SQL 錯誤 [{sx.SqliteErrorCode}]：{sx.Message}",
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new SqliteQueryResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorEn = ex.Message, ErrorZh = $"出錯：{ex.Message}" };
        }
    }

    // ===================== CSV 匯出 · CSV export =====================

    /// <summary>將任意結果集寫成 CSV · Serialize an ad-hoc result set to CSV text.</summary>
    public static string ToCsv(IReadOnlyList<string> columns, IReadOnlyList<string?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", columns.Select(CsvCell)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(v => CsvCell(v ?? ""))));
        return sb.ToString();
    }

    /// <summary>將一個完整表匯出成 CSV（串流，無上限）· Stream an entire table to CSV (no row cap).</summary>
    public static async Task<(bool ok, long rows, string? errEn, string? errZh)> ExportTableCsvAsync(
        string path, string table, string destFile, CancellationToken ct = default)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var conn = Open(path);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"SELECT * FROM {QuoteId(table)};";
                using var rd = cmd.ExecuteReader();
                using var sw = new StreamWriter(destFile, false, new UTF8Encoding(true));
                var cols = new List<string>();
                for (int i = 0; i < rd.FieldCount; i++) cols.Add(rd.GetName(i));
                sw.WriteLine(string.Join(",", cols.Select(CsvCell)));
                long n = 0;
                while (rd.Read())
                {
                    var cells = new string[rd.FieldCount];
                    for (int i = 0; i < rd.FieldCount; i++)
                        cells[i] = rd.IsDBNull(i) ? "" : CsvCell(Stringify(rd.GetValue(i)));
                    sw.WriteLine(string.Join(",", cells));
                    n++;
                }
                return (true, n, (string?)null, (string?)null);
            }, ct);
        }
        catch (Exception ex) { return (false, 0L, ex.Message, $"匯出失敗：{ex.Message}"); }
    }

    private static string CsvCell(string v)
    {
        v ??= "";
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }

    // ===================== helpers =====================

    /// <summary>雙引號安全包裹識別碼 · Double-quote-escape an identifier.</summary>
    public static string QuoteId(string id) => "\"" + (id ?? "").Replace("\"", "\"\"") + "\"";

    /// <summary>綁定一個值（null 字串 → DBNull）· Bind a value, mapping null to SQL NULL.</summary>
    private static void BindValue(SqliteCommand cmd, string name, string? value)
    {
        cmd.Parameters.AddWithValue(name, (object?)value ?? DBNull.Value);
    }

    private static string Stringify(object? v)
    {
        if (v is null) return "";
        switch (v)
        {
            case byte[] bytes: return "x'" + Convert.ToHexString(bytes) + "'";
            case bool b: return b ? "1" : "0";
            case DateTime dt: return dt.ToString("yyyy-MM-dd HH:mm:ss");
            case double d: return d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case float f: return f.ToString(System.Globalization.CultureInfo.InvariantCulture);
            default:
                var s = v.ToString() ?? "";
                return s.Length > 8000 ? s[..8000] + "…" : s;
        }
    }
}
