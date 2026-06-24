# Handoff: pgAdmin 4 / Postgres Tool

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/pgadmin-org/pgadmin4 (Python web app) · native v1 built on Npgsql (github.com/npgsql/npgsql) |
| **License** | pgAdmin 4 is PostgreSQL License (permissive open source). Npgsql is PostgreSQL License (open source) and is the C# .NET Postgres driver we embed. No license conflict with WinForge's original C# code. |
| **Proposed module** | Postgres Tool · Apps / Databases group · Tag `module.pgadmin` |
| **Effort** | M — embedding Npgsql and building a connect + query + results-grid + tree-browse UI is moderate; we deliberately do NOT clone pgAdmin's full admin surface in v1. |

## What the user asked for
A pgAdmin 4 / PostgreSQL administration tool inside WinForge: connect to a Postgres server, browse databases / schemas / tables, run ad-hoc SQL, and view results in a grid — with a fallback to launch the full pgAdmin 4 desktop app for advanced administration.

## Recommended approach
**Hybrid (native query/admin tool + wrap as fallback).** Per the global strategy we prefer a native C# clone. pgAdmin 4 itself is a heavy Python/Flask web app (browser UI + local server) and is not worth reimplementing wholesale — but its *core daily-use loop* (connect, explore the object tree, write SQL, read results) is squarely reimplementable in WinUI using **Npgsql**, the mature C# Postgres driver. So v1 is a native lightweight Postgres client. For genuine full administration (roles, backups, server config, dashboards, ERD), we offer a one-click "Launch pgAdmin 4" via winget `PostgreSQL.pgAdmin` rather than cloning those screens. This keeps WinForge owning the common path natively while not over-investing in pgAdmin's long tail.

## Features to implement (v1 → later)
- **v1:** Connection manager (host, port, database, user, password, SSL mode) with saved connections persisted in app settings (encrypt password via DPAPI). Connect/test via Npgsql. Left tree: Databases (`pg_database`) -> Schemas (`information_schema.schemata`) -> Tables / Views (`information_schema.tables`). SQL editor (multiline textbox) with "Run" -> results `DataGrid`/`ItemsRepeater` showing columns + rows, row count, and elapsed ms. Quick "browse table" (auto `SELECT * ... LIMIT 200`). Error InfoBar on failed queries. "Launch pgAdmin 4" button (detect install, `ShellRunner.Run` the pgAdmin exe; `AutoInstallButton` if missing).
- **later:** Multi-statement / transaction execution; export results to CSV via FileDialogs; column metadata + indexes/constraints view; basic DDL helpers (create/drop table, truncate) with confirmation; query history; syntax highlighting; connection grouping; server stats (active connections from `pg_stat_activity`).

## Integration plan (WinForge specifics)
- **New files:** `Services/PostgresService.cs` (wraps Npgsql `NpgsqlConnection`/`NpgsqlCommand`; async `TestConnectionAsync`, `ListDatabasesAsync`, `ListSchemasAsync`, `ListTablesAsync`, `RunQueryAsync` returning a column list + row matrix + affected-rows; connection-string builder; saved-connection model with DPAPI password protection). `Pages/PgAdminModule.xaml` + `.xaml.cs` (connection bar / saved-connection dropdown, left object `TreeView`, SQL editor, results grid, engine InfoBar with Launch + AutoInstall). Optional `Catalog/PostgresOperations.cs` only if exposing destructive DDL helpers as TweakCards later.
- **Nav wiring:** add `NavigationViewItem Tag="module.pgadmin"` in `MainWindow.xaml` under the Apps / Databases group; add a `ModuleRegistry.All` entry (Tag=`module.pgadmin`, En="Postgres Tool", Zh="Postgres 工具 / pgAdmin", a database Glyph e.g. `0xE94D`, Keywords: `postgres postgresql pgadmin sql database query npgsql 資料庫 數據庫 查詢 表 結構描述`); wire the Tag in `MainWindow.xaml.cs` `MapType`, `NavView_SelectionChanged`, and `ApplyStartPage` for `--page module.pgadmin` deep-link.
- **Engine/install:** native path needs the **Npgsql** NuGet package (no external binary). Fallback path: winget id `PostgreSQL.pgAdmin` via `EngineBars.AutoInstallButton("PostgreSQL.pgAdmin", "Install pgAdmin 4", "安裝 pgAdmin 4", recheck, rescan)`; `rescan` re-detects the pgAdmin exe (`C:\Program Files\pgAdmin 4\...\pgAdmin4.exe`) so the Launch button lights up without restart.
- **Key APIs/CLIs to call:** Npgsql (`NpgsqlConnection.OpenAsync`, `NpgsqlCommand.ExecuteReaderAsync`/`ExecuteNonQueryAsync`); catalog queries against `pg_database`, `information_schema.schemata`, `information_schema.tables`, `pg_stat_activity`. Use `FileDialogs` (never WinRT pickers) for future CSV export.

## Dependencies & risks
- Adds the Npgsql NuGet dependency — confirm it builds cleanly on .NET 11 / WinUI 3 x64.
- Credential storage: never persist plaintext passwords; use DPAPI (`ProtectedData`) keyed to the user. Allow "do not save password" too.
- Run all queries off the UI thread (async) and guard against huge result sets (default LIMIT, cap rows rendered).
- User-supplied SQL is inherently dangerous (DROP/DELETE) — this is expected for a DB tool, but confirm destructive helper buttons; do not auto-run editor content.
- SSL/TLS: expose `SslMode` (Prefer/Require/Disable); some servers require it.
- pgAdmin install path varies by version — probe common paths + Start Menu, do not hard-code a version folder.

## Acceptance criteria
- Builds clean (Debug + Release x64); module appears in the left nav and master search; can save a connection, connect, and test it; object tree lists databases / schemas / tables; SQL editor runs a query and shows columns + rows + timing in a grid; query errors surface in a bilingual InfoBar; "Launch pgAdmin 4" works when installed and AutoInstall appears when absent; every user-facing string is bilingual (English + 粵語); passwords stored via DPAPI; no WinRT pickers used.
