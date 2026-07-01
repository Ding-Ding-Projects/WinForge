using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace WinForge.Services;

/// <summary>
/// 一個已儲存嘅 Postgres 連線（密碼用 DPAPI 加密儲存）·
/// One saved Postgres connection. The password — when the user opts to save it — is encrypted at
/// rest with DPAPI (CurrentUser scope) so it can only be decrypted by the same Windows user.
/// </summary>
public sealed class PgConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5432;
    public string Database { get; set; } = "postgres";
    public string Username { get; set; } = "postgres";
    /// <summary>DPAPI 密文（Base64）· DPAPI ciphertext (Base64). Never plaintext on disk.</summary>
    public string? EncryptedPassword { get; set; }
    /// <summary>是否記住密碼 · Whether the password is saved at all.</summary>
    public bool SavePassword { get; set; } = true;
    /// <summary>SSL 模式：Disable / Prefer / Require · The Npgsql SslMode.</summary>
    public string SslMode { get; set; } = "Prefer";

    public string Display => string.IsNullOrWhiteSpace(Name)
        ? $"{Username}@{Host}:{Port}/{Database}"
        : $"{Name} ({Username}@{Host}:{Port}/{Database})";
}

/// <summary>查詢結果（欄位、列、計時、受影響列數）· A query result: columns, rows, timing, affected rows.</summary>
public sealed class PgQueryResult
{
    public bool Success { get; init; }
    public string? ErrorEn { get; init; }
    public string? ErrorZh { get; init; }
    public List<string> Columns { get; init; } = new();
    public List<string[]> Rows { get; init; } = new();
    public int AffectedRows { get; init; }
    public bool HasResultSet { get; init; }
    public long ElapsedMs { get; init; }
    public bool Truncated { get; init; }
    /// <summary>對於非查詢語句嘅人類可讀狀態（例如 "INSERT 0 1"）· Status line for non-SELECT statements.</summary>
    public string? StatusEn { get; init; }
    public string? StatusZh { get; init; }
}

/// <summary>樹節點種類 · The kind of object-tree node.</summary>
public enum PgNodeKind { Server, Database, SchemaFolder, Schema, TableFolder, ViewFolder, Table, View }

/// <summary>
/// 原生 Postgres 用戶端服務（包住 Npgsql）· The native Postgres client service wrapping Npgsql.
/// 提供連線測試、目錄查詢（資料庫／結構描述／表／檢視）、跑 SQL 並回傳欄＋列＋計時，
/// 加上 pgAdmin 4 桌面版的偵測與啟動（後備）。全部 async、防禦式、永不擲出。
/// Provides connection testing, catalog browsing (databases / schemas / tables / views), a SQL runner
/// returning columns + rows + timing, plus detection &amp; launch of the full pgAdmin 4 desktop app
/// (fallback). Everything is async, defensive, and never throws.
/// </summary>
public static class PostgresService
{
    /// <summary>畀 Launch 後備路徑用嘅 winget 套件 ID · winget id for the pgAdmin 4 fallback.</summary>
    public const string PgAdminWingetId = "PostgreSQL.pgAdmin";

    /// <summary>
    /// 完整 PostgreSQL 伺服器套件（順帶裝埋 pgAdmin 4）· winget id for the full PostgreSQL server
    /// (this package bundles the server + pgAdmin 4). Used by the "install prerequisite" button when no
    /// local Postgres is detected.
    /// </summary>
    public const string PostgresWingetId = "PostgreSQL.PostgreSQL";

    /// <summary>預設渲染上限，避免巨大結果集塞爆 UI · Default cap on rendered rows to guard the UI.</summary>
    public const int DefaultRowCap = 1000;

    private const string SettingsKey = "pgadmin.connections";

    // ===================== 連線字串 · Connection string =====================

    /// <summary>由一個已儲存連線 + 明碼密碼建構 Npgsql 連線字串 · Build the Npgsql connection string.</summary>
    public static string BuildConnectionString(PgConnection c, string? plainPassword, string? overrideDatabase = null)
    {
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = string.IsNullOrWhiteSpace(c.Host) ? "localhost" : c.Host.Trim(),
            Port = c.Port <= 0 ? 5432 : c.Port,
            Database = string.IsNullOrWhiteSpace(overrideDatabase)
                ? (string.IsNullOrWhiteSpace(c.Database) ? "postgres" : c.Database.Trim())
                : overrideDatabase.Trim(),
            Username = string.IsNullOrWhiteSpace(c.Username) ? "postgres" : c.Username.Trim(),
            Timeout = 15,
            CommandTimeout = 60,
            ApplicationName = "WinForge",
            IncludeErrorDetail = true,
            Pooling = false,
        };
        if (!string.IsNullOrEmpty(plainPassword)) b.Password = plainPassword;
        b.SslMode = c.SslMode switch
        {
            "Disable" => SslMode.Disable,
            "Require" => SslMode.Require,
            "VerifyCA" => SslMode.VerifyCA,
            "VerifyFull" => SslMode.VerifyFull,
            _ => SslMode.Prefer,
        };
        // SslMode.Require/Prefer in Npgsql 9 already trust the server certificate without verifying the
        // chain (TrustServerCertificate is obsolete and a no-op); VerifyCA/VerifyFull validate it.
        return b.ConnectionString;
    }

    // ===================== 連線測試 · Connection test =====================

    /// <summary>測試連線（開後即關）· Test a connection by opening and immediately closing it.</summary>
    public static async Task<(bool ok, string? serverVersion, string? errorEn, string? errorZh)>
        TestConnectionAsync(PgConnection c, string? plainPassword, CancellationToken ct = default)
    {
        try
        {
            await using var conn = new NpgsqlConnection(BuildConnectionString(c, plainPassword));
            await conn.OpenAsync(ct);
            var ver = conn.PostgreSqlVersion?.ToString();
            await conn.CloseAsync();
            return (true, ver, null, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message, $"連線失敗：{ex.Message}");
        }
    }

    // ===================== 目錄瀏覽 · Catalog browsing =====================

    /// <summary>列出伺服器上所有可連線資料庫 · List all connectable databases on the server.</summary>
    public static Task<List<string>> ListDatabasesAsync(PgConnection c, string? pw, CancellationToken ct = default)
        => ScalarListAsync(c, pw, null,
            "SELECT datname FROM pg_database WHERE datistemplate = false AND datallowconn = true ORDER BY datname;", ct);

    /// <summary>列出指定資料庫嘅結構描述（排除系統 schema）· List user schemas in a database.</summary>
    public static Task<List<string>> ListSchemasAsync(PgConnection c, string? pw, string database, CancellationToken ct = default)
        => ScalarListAsync(c, pw, database,
            "SELECT schema_name FROM information_schema.schemata " +
            "WHERE schema_name NOT IN ('pg_catalog','information_schema') AND schema_name NOT LIKE 'pg_toast%' AND schema_name NOT LIKE 'pg_temp%' " +
            "ORDER BY schema_name;", ct);

    /// <summary>列出某 schema 嘅表（base table）· List base tables in a schema.</summary>
    public static Task<List<string>> ListTablesAsync(PgConnection c, string? pw, string database, string schema, CancellationToken ct = default)
        => ScalarListAsync(c, pw, database,
            $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{Esc(schema)}' AND table_type = 'BASE TABLE' ORDER BY table_name;", ct);

    /// <summary>列出某 schema 嘅檢視（view）· List views in a schema.</summary>
    public static Task<List<string>> ListViewsAsync(PgConnection c, string? pw, string database, string schema, CancellationToken ct = default)
        => ScalarListAsync(c, pw, database,
            $"SELECT table_name FROM information_schema.views WHERE table_schema = '{Esc(schema)}' ORDER BY table_name;", ct);

    /// <summary>列出某表嘅欄位（名稱 + 型別）· List columns (name + type) of a table.</summary>
    public static async Task<List<(string name, string type)>> ListColumnsAsync(
        PgConnection c, string? pw, string database, string schema, string table, CancellationToken ct = default)
    {
        var result = new List<(string, string)>();
        try
        {
            await using var conn = new NpgsqlConnection(BuildConnectionString(c, pw, database));
            await conn.OpenAsync(ct);
            var sql = "SELECT column_name, data_type FROM information_schema.columns " +
                      $"WHERE table_schema = '{Esc(schema)}' AND table_name = '{Esc(table)}' ORDER BY ordinal_position;";
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                result.Add((rd.GetString(0), rd.GetString(1)));
        }
        catch { /* best effort */ }
        return result;
    }

    private static async Task<List<string>> ScalarListAsync(
        PgConnection c, string? pw, string? database, string sql, CancellationToken ct)
    {
        var list = new List<string>();
        try
        {
            await using var conn = new NpgsqlConnection(BuildConnectionString(c, pw, database));
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var rd = await cmd.ExecuteReaderAsync(ct);
            while (await rd.ReadAsync(ct))
                if (!rd.IsDBNull(0)) list.Add(rd.GetValue(0)?.ToString() ?? "");
        }
        catch { /* surfaced via empty list; callers show tree errors separately */ }
        return list;
    }

    // ===================== 跑 SQL · Run SQL =====================

    /// <summary>
    /// 跑使用者輸入嘅 SQL 並回傳欄＋列＋計時 · Run user SQL and return columns + rows + timing.
    /// SELECT 類回傳結果集（最多 <paramref name="rowCap"/> 列）；其他語句回傳受影響列數。
    /// SELECT-like queries return a capped result set; other statements return affected-row counts.
    /// </summary>
    public static async Task<PgQueryResult> RunQueryAsync(
        PgConnection c, string? pw, string database, string sql, int rowCap = DefaultRowCap, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await using var conn = new NpgsqlConnection(BuildConnectionString(c, pw, database));
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var rd = await cmd.ExecuteReaderAsync(ct);

            // Has a result set? (SELECT / RETURNING / SHOW …)
            if (rd.FieldCount > 0)
            {
                var cols = new List<string>();
                for (int i = 0; i < rd.FieldCount; i++) cols.Add(rd.GetName(i));
                var rows = new List<string[]>();
                bool truncated = false;
                while (await rd.ReadAsync(ct))
                {
                    if (rows.Count >= rowCap) { truncated = true; break; }
                    var row = new string[rd.FieldCount];
                    for (int i = 0; i < rd.FieldCount; i++)
                        row[i] = await rd.IsDBNullAsync(i, ct) ? "(null)" : Stringify(rd.GetValue(i));
                    rows.Add(row);
                }
                sw.Stop();
                return new PgQueryResult
                {
                    Success = true,
                    HasResultSet = true,
                    Columns = cols,
                    Rows = rows,
                    ElapsedMs = sw.ElapsedMilliseconds,
                    Truncated = truncated,
                    AffectedRows = rows.Count,
                };
            }

            int affected = rd.RecordsAffected;
            sw.Stop();
            return new PgQueryResult
            {
                Success = true,
                HasResultSet = false,
                AffectedRows = affected < 0 ? 0 : affected,
                ElapsedMs = sw.ElapsedMilliseconds,
                StatusEn = $"Command OK — {(affected < 0 ? 0 : affected)} row(s) affected.",
                StatusZh = $"指令完成 — 影響 {(affected < 0 ? 0 : affected)} 列。",
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new PgQueryResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorEn = "Query cancelled.", ErrorZh = "查詢已取消。" };
        }
        catch (PostgresException pex)
        {
            sw.Stop();
            var detail = string.IsNullOrWhiteSpace(pex.Detail) ? "" : $"\n{pex.Detail}";
            var where = string.IsNullOrWhiteSpace(pex.Where) ? "" : $"\n{pex.Where}";
            return new PgQueryResult
            {
                Success = false,
                ElapsedMs = sw.ElapsedMilliseconds,
                ErrorEn = $"[{pex.SqlState}] {pex.MessageText}{detail}{where}",
                ErrorZh = $"SQL 錯誤 [{pex.SqlState}]：{pex.MessageText}{detail}{where}",
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PgQueryResult { Success = false, ElapsedMs = sw.ElapsedMilliseconds, ErrorEn = ex.Message, ErrorZh = $"出錯：{ex.Message}" };
        }
    }

    /// <summary>伺服器活躍連線（pg_stat_activity）· Server stats: active connections.</summary>
    public static Task<PgQueryResult> ServerActivityAsync(PgConnection c, string? pw, string database, CancellationToken ct = default)
        => RunQueryAsync(c, pw, database,
            "SELECT pid, usename AS user, datname AS database, client_addr, state, " +
            "substring(coalesce(query,'') for 80) AS query, backend_start " +
            "FROM pg_stat_activity ORDER BY backend_start;", DefaultRowCap, ct);

    /// <summary>自動產生 browse-table 查詢（SELECT * … LIMIT）· Build a quick browse query.</summary>
    public static string BrowseTableSql(string schema, string table, int limit = 200)
        => $"SELECT * FROM \"{schema.Replace("\"", "\"\"")}\".\"{table.Replace("\"", "\"\"")}\" LIMIT {limit};";

    // ===================== CSV 匯出 · CSV export =====================

    /// <summary>將結果集寫成 CSV · Serialize a result set to CSV text.</summary>
    public static string ToCsv(PgQueryResult r)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", r.Columns.Select(CsvCell)));
        foreach (var row in r.Rows)
            sb.AppendLine(string.Join(",", row.Select(CsvCell)));
        return sb.ToString();

        static string CsvCell(string v)
        {
            v ??= "";
            if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }
    }

    // ===================== 已儲存連線（DPAPI）· Saved connections (DPAPI) =====================

    /// <summary>讀取所有已儲存連線 · Load all saved connections.</summary>
    public static List<PgConnection> LoadConnections()
    {
        try
        {
            var json = SettingsStore.Get(SettingsKey, "");
            if (string.IsNullOrWhiteSpace(json)) return new();
            return JsonSerializer.Deserialize<List<PgConnection>>(json) ?? new();
        }
        catch { return new(); }
    }

    /// <summary>儲存所有連線 · Persist all connections.</summary>
    public static void SaveConnections(List<PgConnection> list)
    {
        try { SettingsStore.Set(SettingsKey, JsonSerializer.Serialize(list)); } catch { }
    }

    /// <summary>新增／更新一個連線（按 Id）· Upsert a connection by Id.</summary>
    public static void Upsert(PgConnection c)
    {
        var all = LoadConnections();
        var idx = all.FindIndex(x => x.Id == c.Id);
        if (idx >= 0) all[idx] = c; else all.Add(c);
        SaveConnections(all);
    }

    /// <summary>刪除一個連線 · Delete a connection by Id.</summary>
    public static void Delete(string id)
    {
        var all = LoadConnections();
        all.RemoveAll(x => x.Id == id);
        SaveConnections(all);
    }

    /// <summary>用 DPAPI（CurrentUser）加密密碼成 Base64 · Encrypt a password with DPAPI (CurrentUser).</summary>
    public static string? EncryptPassword(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        try
        {
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }
        catch { return null; }
    }

    /// <summary>解密 DPAPI 密碼 · Decrypt a DPAPI-protected password.</summary>
    public static string? DecryptPassword(string? cipherBase64)
    {
        if (string.IsNullOrEmpty(cipherBase64)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(cipherBase64), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return null; }
    }

    // ===================== pgAdmin 4 後備 · pgAdmin 4 fallback =====================

    /// <summary>偵測已安裝嘅 pgAdmin 4 exe（探測常見路徑）· Probe common install paths for pgAdmin4.exe.</summary>
    public static string? FindPgAdminExe()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "",
        };
        foreach (var root in roots.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            // pgAdmin installs under "pgAdmin 4\v<n>\runtime\pgAdmin4.exe" (version-foldered) or "pgAdmin 4\bin\pgAdmin4.exe".
            var baseDir = Path.Combine(root, "pgAdmin 4");
            if (!Directory.Exists(baseDir)) continue;
            try
            {
                var hit = Directory.EnumerateFiles(baseDir, "pgAdmin4.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null) return hit;
            }
            catch { /* permission — skip */ }
        }
        return null;
    }

    /// <summary>pgAdmin 4 裝咗未 · Whether pgAdmin 4 desktop is installed.</summary>
    public static bool IsPgAdminInstalled() => FindPgAdminExe() is not null;

    /// <summary>啟動 pgAdmin 4 桌面版 · Launch the pgAdmin 4 desktop app.</summary>
    public static async Task<bool> LaunchPgAdminAsync()
    {
        var exe = FindPgAdminExe();
        if (exe is null) return false;
        try
        {
            // pgAdmin keeps running, so launch it shell-style and treat a successful start as success.
            Process.Start(new ProcessStartInfo { FileName = exe, UseShellExecute = true });
            return await Task.FromResult(true);
        }
        catch { return false; }
    }

    // ===================== 本機 Postgres 偵測 · Local Postgres detection =====================

    private static string? _cachedPsql;

    /// <summary>清除本機偵測快取（安裝後叫）· Clear the local-detection cache (call after an install).</summary>
    public static void RescanLocal() => _cachedPsql = null;

    /// <summary>
    /// 探測本機 psql.exe（PATH + 常見安裝目錄）· Probe for a local psql.exe on PATH and in the standard
    /// "PostgreSQL\&lt;ver&gt;\bin" install locations. Cached; call <see cref="RescanLocal"/> to invalidate.
    /// </summary>
    public static string? FindPsqlExe()
    {
        if (_cachedPsql is not null && File.Exists(_cachedPsql)) return _cachedPsql;

        // 1) On PATH.
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                var cand = Path.Combine(dir.Trim(), "psql.exe");
                if (File.Exists(cand)) { _cachedPsql = cand; return cand; }
            }
            catch { /* malformed PATH entry — skip */ }
        }

        // 2) Standard installer layout: "<ProgramFiles>\PostgreSQL\<ver>\bin\psql.exe".
        foreach (var root in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                 }.Where(r => !string.IsNullOrWhiteSpace(r)))
        {
            var baseDir = Path.Combine(root, "PostgreSQL");
            if (!Directory.Exists(baseDir)) continue;
            try
            {
                var hit = Directory.EnumerateFiles(baseDir, "psql.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (hit is not null) { _cachedPsql = hit; return hit; }
            }
            catch { /* permission — skip */ }
        }
        return null;
    }

    /// <summary>有冇 postgresql 服務（無論運行與否）· Whether a "postgresql*" Windows service is registered.</summary>
    public static bool HasPostgresService()
    {
        // Read the services registry directly so we don't need the System.ServiceProcess assembly.
        try
        {
            using var svc = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services");
            if (svc is null) return false;
            return svc.GetSubKeyNames().Any(n => n.StartsWith("postgresql", StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }

    /// <summary>
    /// 本機有冇裝 PostgreSQL（psql 或服務任一命中）· Whether a local PostgreSQL server appears installed —
    /// true if psql.exe is found OR a postgresql* service exists. Cheap, offline, and never throws.
    /// </summary>
    public static bool IsLocalPostgresInstalled() => FindPsqlExe() is not null || HasPostgresService();

    // ===================== helpers =====================

    private static string Esc(string s) => s.Replace("'", "''");

    private static string Stringify(object? v)
    {
        if (v is null) return "(null)";
        switch (v)
        {
            case byte[] bytes: return "\\x" + Convert.ToHexString(bytes);
            case bool b: return b ? "true" : "false";
            case DateTime dt: return dt.ToString("yyyy-MM-dd HH:mm:ss");
            case Array arr: return string.Join(", ", arr.Cast<object?>().Select(x => x?.ToString() ?? "(null)"));
            default:
                var s = v.ToString() ?? "";
                return s.Length > 4000 ? s[..4000] + "…" : s;
        }
    }
}
