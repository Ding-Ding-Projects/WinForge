using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// quicktype 包裝服務 · Wraps the quicktype npm CLI (glideapps/quicktype) — turns JSON, JSON Schema,
/// TypeScript, GraphQL or a Postman collection into typed source code in many languages (C#, TypeScript,
/// Python, Go, Rust, Java, Kotlin, Swift, Objective-C, C++, Dart, Ruby, Elm, JSON Schema, …).
/// 偵測 Node + quicktype，缺就提供安裝；之後將輸入寫入暫存檔、叫 CLI、擷取 stdout。
/// Detects Node + quicktype, offers install if missing, writes input to a temp file, invokes the CLI and
/// captures stdout. No WinRT pickers, no external windows — everything runs in-app. Bilingual.
/// </summary>
public static class QuickTypeService
{
    private static string? _cachedCli;

    // ---- input kinds (quicktype --srcLang) -------------------------------------------------

    /// <summary>一種輸入來源（對應 quicktype --srcLang）· One input source kind.</summary>
    public sealed record InputKind(string Key, string SrcLang, string En, string Zh, string[] Extensions);

    public static readonly IReadOnlyList<InputKind> InputKinds = new[]
    {
        new InputKind("json", "json", "JSON", "JSON", new[] { ".json" }),
        new InputKind("schema", "schema", "JSON Schema", "JSON Schema", new[] { ".json", ".schema" }),
        new InputKind("typescript", "typescript", "TypeScript", "TypeScript", new[] { ".ts" }),
        new InputKind("graphql", "graphql", "GraphQL schema", "GraphQL 結構", new[] { ".graphql", ".gql" }),
        new InputKind("postman", "postman", "Postman collection", "Postman 集合", new[] { ".json", ".postman_collection.json" }),
    };

    // ---- target languages (quicktype --lang) ----------------------------------------------

    /// <summary>一種目標語言（對應 quicktype --lang）· One target language.</summary>
    public sealed record TargetLang(string Key, string Lang, string En, string Zh, string FileExt);

    public static readonly IReadOnlyList<TargetLang> Targets = new[]
    {
        new TargetLang("cs", "csharp", "C#", "C#", ".cs"),
        new TargetLang("ts", "typescript", "TypeScript", "TypeScript", ".ts"),
        new TargetLang("tse", "typescript-effect-schema", "TypeScript (effect Schema)", "TypeScript（effect Schema）", ".ts"),
        new TargetLang("tszod", "typescript-zod", "TypeScript (Zod)", "TypeScript（Zod）", ".ts"),
        new TargetLang("py", "python", "Python", "Python", ".py"),
        new TargetLang("go", "go", "Go", "Go", ".go"),
        new TargetLang("rust", "rust", "Rust", "Rust", ".rs"),
        new TargetLang("java", "java", "Java", "Java", ".java"),
        new TargetLang("kotlin", "kotlin", "Kotlin", "Kotlin", ".kt"),
        new TargetLang("swift", "swift", "Swift", "Swift", ".swift"),
        new TargetLang("objc", "objective-c", "Objective-C", "Objective-C", ".m"),
        new TargetLang("cpp", "c++", "C++", "C++", ".hpp"),
        new TargetLang("dart", "dart", "Dart", "Dart", ".dart"),
        new TargetLang("ruby", "ruby", "Ruby", "Ruby", ".rb"),
        new TargetLang("elm", "elm", "Elm", "Elm", ".elm"),
        new TargetLang("php", "php", "PHP", "PHP", ".php"),
        new TargetLang("scala3", "scala3", "Scala 3", "Scala 3", ".scala"),
        new TargetLang("haskell", "haskell", "Haskell", "Haskell", ".hs"),
        new TargetLang("flow", "flow", "Flow", "Flow", ".js"),
        new TargetLang("js", "javascript", "JavaScript", "JavaScript", ".js"),
        new TargetLang("jsonschema", "json-schema", "JSON Schema", "JSON Schema", ".json"),
        new TargetLang("pike", "pike", "Pike", "Pike", ".pike"),
        new TargetLang("prop", "proto", "Protobuf", "Protobuf", ".proto"),
        new TargetLang("crystal", "crystal", "Crystal", "Crystal", ".cr"),
    };

    /// <summary>已生成嘅結果 · The result of a generate run.</summary>
    public sealed record GenResult(bool Success, string Code, string Error, string CommandLine);

    // ---- detection -------------------------------------------------------------------------

    /// <summary>Node（npm）喺唔喺度 · Is Node available? Runs "node --version".</summary>
    public static async Task<bool> NodeAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var r = await ShellRunner.Run("node", "--version", false, ct);
            if (r.Success) return true;
            var r2 = await ShellRunner.Run("npm", "--version", false, ct);
            return r2.Success;
        }
        catch { return false; }
    }

    /// <summary>
    /// 搵 quicktype CLI · Locate the quicktype CLI. Tries PATH (where.exe), then npm's global bin,
    /// then a "quicktype --version" probe. Caches the resolved invocation. Returns null if missing.
    /// </summary>
    public static async Task<string?> LocateCliAsync(CancellationToken ct = default)
    {
        if (_cachedCli is not null) return _cachedCli;

        // 1) On PATH (quicktype.cmd / quicktype)?
        try
        {
            var where = await ShellRunner.Capture("where.exe", "quicktype", ct);
            var first = where.Replace("\r", "").Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.EndsWith("quicktype", StringComparison.OrdinalIgnoreCase)
                                  || l.EndsWith("quicktype.cmd", StringComparison.OrdinalIgnoreCase)
                                  || l.EndsWith("quicktype.exe", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(first) && File.Exists(first))
                return _cachedCli = first;
        }
        catch { /* ignore */ }

        // 2) npm global prefix\quicktype.cmd
        try
        {
            var prefix = (await ShellRunner.Capture("npm", "prefix -g", ct)).Trim();
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                foreach (var cand in new[] { Path.Combine(prefix, "quicktype.cmd"), Path.Combine(prefix, "quicktype") })
                    if (File.Exists(cand)) return _cachedCli = cand;
            }
        }
        catch { /* ignore */ }

        // 3) Last resort: probe "quicktype --version" via cmd (resolves .cmd shims on PATH).
        try
        {
            var r = await ShellRunner.RunCmd("quicktype --version", false, ct);
            if (r.Success) return _cachedCli = "quicktype";
        }
        catch { /* ignore */ }

        return null;
    }

    /// <summary>quicktype 裝咗未 · Is the quicktype CLI available?</summary>
    public static async Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => await LocateCliAsync(ct) is not null;

    /// <summary>忘記快取嘅 CLI 路徑（安裝／PATH 變更後叫）· Drop the cached CLI path after an install / PATH change.</summary>
    public static void Rescan() => _cachedCli = null;

    /// <summary>
    /// 用 npm 全域安裝 quicktype · Globally install quicktype via npm (npm install -g quicktype),
    /// then refresh this process's PATH so the new shim resolves immediately.
    /// </summary>
    public static async Task<TweakResult> InstallViaNpmAsync(CancellationToken ct = default)
        => await InstallViaNpmAsync(null, ct);

    /// <summary>
    /// 用 npm 全域安裝 quicktype（串流版）· Streaming variant: reports each raw npm output line via
    /// <paramref name="onLine"/> so a progress control can show live status, then refreshes PATH and the
    /// cached CLI path. Surfaces the real exit code + captured output. Never throws.
    /// </summary>
    public static async Task<TweakResult> InstallViaNpmAsync(IProgress<string>? onLine, CancellationToken ct = default)
    {
        TweakResult r;
        try { r = await ShellRunner.RunCmdStreaming("npm install -g quicktype", onLine, false, ct); }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
        if (r.Success)
        {
            try { PackageService.RefreshProcessPath(); } catch { }
            Rescan();
        }
        return r;
    }

    // ---- generation ------------------------------------------------------------------------

    /// <summary>每種語言可有嘅旗標 · Per-language flags surfaced as real controls.</summary>
    public sealed class GenOptions
    {
        public InputKind Input { get; set; } = InputKinds[0];
        public TargetLang Target { get; set; } = Targets[0];

        /// <summary>頂層型別名 · Top-level type name (--top-level).</summary>
        public string TopLevelName { get; set; } = "Root";

        /// <summary>淨係型別、唔要序列化 helper · Emit just the types (--just-types).</summary>
        public bool JustTypes { get; set; }

        // C# specific.
        public string? Namespace { get; set; }            // --namespace
        public string? CSharpFramework { get; set; }      // SystemTextJson | NewtonSoft  (--framework)
        public bool CSharpArrayType { get; set; }         // --array-type list  (when true)

        // Common.
        public bool AcronymStyle { get; set; }            // --acronym-style original (kept off by default)
        public bool NoUriType { get; set; }               // --no-uri-detection-ish: we expose lenient toggles below
    }

    /// <summary>
    /// 用 quicktype 生成程式碼 · Generate code with quicktype. Writes <paramref name="source"/> to a temp file
    /// with the right extension, invokes the CLI with the chosen options, returns stdout (the generated code).
    /// </summary>
    public static async Task<GenResult> GenerateAsync(string source, GenOptions opt, CancellationToken ct = default)
    {
        var cli = await LocateCliAsync(ct);
        if (cli is null)
            return new GenResult(false, "", "quicktype CLI not found.", "");

        string tempDir = Path.Combine(Path.GetTempPath(), "WinForge-quicktype");
        Directory.CreateDirectory(tempDir);
        string ext = opt.Input.Extensions.FirstOrDefault() ?? ".json";
        // Postman collections must end in .postman_collection.json for the parser; keep it simple with .json.
        if (opt.Input.Key == "postman") ext = ".json";
        string inPath = Path.Combine(tempDir, "input_" + Guid.NewGuid().ToString("N") + ext);

        await File.WriteAllTextAsync(inPath, source ?? string.Empty, new UTF8Encoding(false), ct);

        try
        {
            var args = BuildArgs(inPath, opt);
            // Invoke through the resolved shim. If it's a bare "quicktype" we go via cmd /c so the
            // .cmd shim on PATH is found; otherwise call the resolved .cmd directly via cmd /c too
            // (Node .cmd shims need a shell host).
            string quoted = NeedsQuote(cli) ? $"\"{cli}\"" : cli;
            var r = await ShellRunner.RunCmd($"{quoted} {args}", false, ct);

            string cmdLine = $"quicktype {args}";
            if (r.Success)
                return new GenResult(true, (r.Output ?? "").TrimEnd() + "\n", "", cmdLine);

            // quicktype writes errors to stderr, which ShellRunner folds into Output on failure.
            return new GenResult(false, "", string.IsNullOrWhiteSpace(r.Output) ? "quicktype failed." : r.Output, cmdLine);
        }
        finally
        {
            try { File.Delete(inPath); } catch { /* ignore */ }
        }
    }

    private static bool NeedsQuote(string s) => s.Contains(' ') && !s.StartsWith("\"");

    /// <summary>砌 quicktype 命令列引數 · Build the quicktype CLI argument string from options.</summary>
    private static string BuildArgs(string inPath, GenOptions opt)
    {
        var sb = new StringBuilder();

        // Source language + input file.
        sb.Append("--src-lang ").Append(opt.Input.SrcLang).Append(' ');

        // Target language.
        sb.Append("--lang ").Append(Quote(opt.Target.Lang)).Append(' ');

        // Top-level type name.
        var topName = string.IsNullOrWhiteSpace(opt.TopLevelName) ? "Root" : opt.TopLevelName.Trim();
        sb.Append("--top-level ").Append(Quote(topName)).Append(' ');

        if (opt.JustTypes)
            sb.Append("--just-types ");

        // C#-specific options.
        if (opt.Target.Lang == "csharp")
        {
            if (!string.IsNullOrWhiteSpace(opt.Namespace))
                sb.Append("--namespace ").Append(Quote(opt.Namespace!.Trim())).Append(' ');
            if (!string.IsNullOrWhiteSpace(opt.CSharpFramework))
                sb.Append("--framework ").Append(opt.CSharpFramework).Append(' ');
            if (opt.CSharpArrayType)
                sb.Append("--array-type list ");
        }

        if (opt.AcronymStyle)
            sb.Append("--acronym-style original ");

        // The input file always comes last (positional).
        sb.Append(Quote(inPath));
        return sb.ToString();
    }

    private static string Quote(string s) => s.Contains(' ') ? $"\"{s}\"" : s;
}
