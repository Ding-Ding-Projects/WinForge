using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>version_manifest_v2 入面一個版本 · One entry from version_manifest_v2 (id + type + json url).</summary>
public sealed record MinecraftVersionRef(string Id, string Type, string Url, string Sha1, DateTimeOffset Released);

/// <summary>下載進度回報 · A download/launch progress report (bilingual-safe — message is pre-localized by caller).</summary>
public sealed record LauncherProgress(string Stage, int Done, int Total, string Detail = "")
{
    public double Fraction => Total <= 0 ? 0 : Math.Clamp((double)Done / Total, 0, 1);
}

/// <summary>
/// 一個獨立嘅啟動器 instance / profile · One independent launcher instance: its own version, game directory,
/// JVM path and memory. No shared/static state — every instance is self-contained and serialized as-is.
/// </summary>
public sealed class MinecraftInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Instance";
    public string VersionId { get; set; } = "";

    /// <summary>per-instance .minecraft 資料夾（隔離存檔／設定）· The isolated .minecraft game directory.</summary>
    public string GameDirectory { get; set; } = "";

    /// <summary>java.exe 路徑（空 = 用下載嘅 JRE）· Path to java.exe ("" → use a downloaded JRE).</summary>
    public string JavaPath { get; set; } = "";

    public int MaxMemoryMb { get; set; } = 2048;
    public int MinMemoryMb { get; set; } = 512;
    public string ExtraJvmArgs { get; set; } = "";

    /// <summary>呢個 instance 用邊個帳戶（auth instance id）· Which account (auth instance id) this uses.</summary>
    public string AccountInstanceId { get; set; } = "";

    public MinecraftInstance Clone() => new()
    {
        Id = Id, Name = Name, VersionId = VersionId, GameDirectory = GameDirectory, JavaPath = JavaPath,
        MaxMemoryMb = MaxMemoryMb, MinMemoryMb = MinMemoryMb, ExtraJvmArgs = ExtraJvmArgs,
        AccountInstanceId = AccountInstanceId,
    };
}

/// <summary>
/// Minecraft 安裝 + 啟動 pipeline · The download + launch pipeline, pure managed C#. Fetches
/// version_manifest_v2, the per-version JSON (libraries / asset index / client jar / javaVersion), downloads
/// everything with SHA1 verification + parallelism into the standard .minecraft layout (versions/, libraries/,
/// assets/objects/&lt;2hex&gt;/&lt;hash&gt;), extracts OS natives, builds the launch command (classpath, JVM
/// args, game args with ${...} substitution) and starts java via Process. Per handoff 54 §4b we do NOT
/// redirect stdio of the windowed game child (that can hang it / look like "won't launch"); all work runs off
/// the UI thread with a CancellationToken and never throws past a guarded boundary.
///
/// JRE NOTE: launching needs a JVM matching the version's javaVersion.majorVersion. This service does not
/// bundle a JRE; it expects the instance's JavaPath to point at a java.exe, or a JRE the UI fetched from a
/// vendor like Adoptium/Temurin into &lt;root&gt;\runtimes\&lt;major&gt;. <see cref="ResolveJavaPath"/> picks one.
/// </summary>
public static class MinecraftLauncherService
{
    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    /// <summary>WinForge 用嘅 .minecraft 根目錄 · The .minecraft root WinForge manages (instances live under it).</summary>
    public static string DefaultRoot { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft_winforge");

    // -------------------------------------------------------------- manifest

    /// <summary>下載版本清單 · Download and parse version_manifest_v2 into release/snapshot refs.</summary>
    public static async Task<IReadOnlyList<MinecraftVersionRef>> GetVersionsAsync(CancellationToken ct)
    {
        try
        {
            using var http = NewHttp();
            var json = await http.GetStringAsync(ManifestUrl, ct);
            using var doc = JsonDocument.Parse(json);
            var list = new List<MinecraftVersionRef>();
            if (doc.RootElement.TryGetProperty("versions", out var versions))
            {
                foreach (var v in versions.EnumerateArray())
                {
                    var id = Str(v, "id");
                    if (id.Length == 0) continue;
                    DateTimeOffset.TryParse(Str(v, "releaseTime"), out var rel);
                    list.Add(new MinecraftVersionRef(id, Str(v, "type"), Str(v, "url"), Str(v, "sha1"), rel));
                }
            }
            return list;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher manifest", ex); return Array.Empty<MinecraftVersionRef>(); }
    }

    // -------------------------------------------------------------- install

    /// <summary>
    /// 安裝一個版本 · Download a version's JSON, libraries, assets, client jar (all SHA1-verified, parallel)
    /// and extract natives. Returns the parsed version JSON document text for the launch step. Safe to re-run
    /// (verified files are skipped).
    /// </summary>
    public static async Task<bool> InstallVersionAsync(
        string root, MinecraftVersionRef version, IProgress<LauncherProgress>? progress, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(root);
            using var http = NewHttp();

            // 1) version JSON
            var versionDir = Path.Combine(root, "versions", version.Id);
            Directory.CreateDirectory(versionDir);
            var versionJsonPath = Path.Combine(versionDir, version.Id + ".json");
            string versionJson;
            if (File.Exists(versionJsonPath) && (version.Sha1.Length == 0 || await Sha1Async(versionJsonPath) == version.Sha1))
                versionJson = await File.ReadAllTextAsync(versionJsonPath, ct);
            else
            {
                versionJson = await http.GetStringAsync(version.Url, ct);
                await File.WriteAllTextAsync(versionJsonPath, versionJson, ct);
            }

            using var doc = JsonDocument.Parse(versionJson);
            var vroot = doc.RootElement;

            // 2) collect downloads: client jar + libraries
            var jobs = new List<(string url, string path, string sha1)>();

            if (vroot.TryGetProperty("downloads", out var dls) && dls.TryGetProperty("client", out var client))
                jobs.Add((Str(client, "url"), Path.Combine(versionDir, version.Id + ".jar"), Str(client, "sha1")));

            var nativesDir = Path.Combine(versionDir, "natives");
            var nativeArchives = new List<string>();

            if (vroot.TryGetProperty("libraries", out var libs))
            {
                foreach (var lib in libs.EnumerateArray())
                {
                    if (!RulesAllow(lib)) continue;
                    if (lib.TryGetProperty("downloads", out var ldl))
                    {
                        if (ldl.TryGetProperty("artifact", out var art))
                        {
                            var p = Path.Combine(root, "libraries", Str(art, "path").Replace('/', Path.DirectorySeparatorChar));
                            jobs.Add((Str(art, "url"), p, Str(art, "sha1")));
                        }
                        // natives classifier for this OS
                        var nativeKey = NativeClassifier(lib);
                        if (nativeKey.Length > 0 && ldl.TryGetProperty("classifiers", out var cls)
                            && cls.TryGetProperty(nativeKey, out var nat))
                        {
                            var p = Path.Combine(root, "libraries", Str(nat, "path").Replace('/', Path.DirectorySeparatorChar));
                            jobs.Add((Str(nat, "url"), p, Str(nat, "sha1")));
                            nativeArchives.Add(p);
                        }
                    }
                }
            }

            // 3) asset index + objects
            var assetsDir = Path.Combine(root, "assets");
            if (vroot.TryGetProperty("assetIndex", out var ai))
            {
                var indexId = Str(ai, "id");
                var indexPath = Path.Combine(assetsDir, "indexes", indexId + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(indexPath)!);
                string indexJson;
                var indexSha = Str(ai, "sha1");
                if (File.Exists(indexPath) && (indexSha.Length == 0 || await Sha1Async(indexPath) == indexSha))
                    indexJson = await File.ReadAllTextAsync(indexPath, ct);
                else
                {
                    indexJson = await http.GetStringAsync(Str(ai, "url"), ct);
                    await File.WriteAllTextAsync(indexPath, indexJson, ct);
                }
                using var idoc = JsonDocument.Parse(indexJson);
                if (idoc.RootElement.TryGetProperty("objects", out var objects))
                {
                    foreach (var obj in objects.EnumerateObject())
                    {
                        var hash = Str(obj.Value, "hash");
                        if (hash.Length < 2) continue;
                        var sub = hash[..2];
                        var p = Path.Combine(assetsDir, "objects", sub, hash);
                        var url = $"https://resources.download.minecraft.net/{sub}/{hash}";
                        jobs.Add((url, p, hash));
                    }
                }
            }

            // 4) parallel download with SHA1 verify + skip-if-present
            int total = jobs.Count, done = 0;
            progress?.Report(new LauncherProgress("download", 0, total));
            using var sem = new SemaphoreSlim(8);
            var tasks = jobs.Select(async job =>
            {
                await sem.WaitAsync(ct);
                try
                {
                    await DownloadVerifiedAsync(http, job.url, job.path, job.sha1, ct);
                    var d = Interlocked.Increment(ref done);
                    if (d % 16 == 0 || d == total)
                        progress?.Report(new LauncherProgress("download", d, total));
                }
                finally { sem.Release(); }
            }).ToList();
            await Task.WhenAll(tasks);
            progress?.Report(new LauncherProgress("download", total, total));

            // 5) extract natives
            if (nativeArchives.Count > 0)
            {
                Directory.CreateDirectory(nativesDir);
                foreach (var archive in nativeArchives)
                    ExtractNatives(archive, nativesDir);
            }

            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher install", ex); return false; }
    }

    // -------------------------------------------------------------- launch

    /// <summary>
    /// 啟動已安裝嘅版本 · Build the command and launch java for an installed instance. Returns the started
    /// Process, or null on failure. Stdio of the windowed game child is intentionally NOT redirected (§4b).
    /// </summary>
    public static Process? Launch(string root, MinecraftInstance instance, MinecraftAccount account, out string error)
    {
        error = "";
        try
        {
            var versionDir = Path.Combine(root, "versions", instance.VersionId);
            var versionJsonPath = Path.Combine(versionDir, instance.VersionId + ".json");
            if (!File.Exists(versionJsonPath)) { error = "version-not-installed"; return null; }

            var java = ResolveJavaPath(root, versionJsonPath, instance.JavaPath);
            if (string.IsNullOrEmpty(java) || !File.Exists(java)) { error = "no-java"; return null; }

            var versionJson = File.ReadAllText(versionJsonPath);
            using var doc = JsonDocument.Parse(versionJson);
            var vroot = doc.RootElement;

            var gameDir = string.IsNullOrWhiteSpace(instance.GameDirectory)
                ? Path.Combine(root, "instances", instance.Id)
                : instance.GameDirectory;
            Directory.CreateDirectory(gameDir);

            var nativesDir = Path.Combine(versionDir, "natives");
            var assetsDir = Path.Combine(root, "assets");
            var assetIndexId = vroot.TryGetProperty("assetIndex", out var ai) ? Str(ai, "id") : "legacy";
            var mainClass = Str(vroot, "mainClass");

            // classpath: allowed library artifacts + client jar
            var cp = new List<string>();
            if (vroot.TryGetProperty("libraries", out var libs))
            {
                foreach (var lib in libs.EnumerateArray())
                {
                    if (!RulesAllow(lib)) continue;
                    if (lib.TryGetProperty("downloads", out var ldl) && ldl.TryGetProperty("artifact", out var art))
                    {
                        var p = Path.Combine(root, "libraries", Str(art, "path").Replace('/', Path.DirectorySeparatorChar));
                        if (File.Exists(p)) cp.Add(p);
                    }
                }
            }
            cp.Add(Path.Combine(versionDir, instance.VersionId + ".jar"));
            var classpath = string.Join(Path.PathSeparator, cp);

            var subst = new Dictionary<string, string>
            {
                ["auth_player_name"] = account.Name,
                ["auth_uuid"] = account.Uuid,
                ["auth_access_token"] = account.AccessToken,
                ["auth_xuid"] = "",
                ["clientid"] = "",
                ["user_type"] = account.IsOffline ? "legacy" : "msa",
                ["user_properties"] = "{}",
                ["version_name"] = instance.VersionId,
                ["version_type"] = Str(vroot, "type"),
                ["game_directory"] = gameDir,
                ["assets_root"] = assetsDir,
                ["game_assets"] = assetsDir,
                ["assets_index_name"] = assetIndexId,
                ["natives_directory"] = nativesDir,
                ["classpath"] = classpath,
                ["launcher_name"] = "WinForge",
                ["launcher_version"] = "1.0",
            };

            var args = new List<string>();

            // JVM args (modern arguments.jvm or legacy fallback)
            if (vroot.TryGetProperty("arguments", out var arguments) && arguments.TryGetProperty("jvm", out var jvm))
                AppendArgs(args, jvm, subst);
            else
            {
                args.Add($"-Djava.library.path={nativesDir}");
                args.Add("-cp");
                args.Add(classpath);
            }

            args.Add($"-Xmx{Math.Max(512, instance.MaxMemoryMb)}M");
            args.Add($"-Xms{Math.Max(256, instance.MinMemoryMb)}M");
            if (!string.IsNullOrWhiteSpace(instance.ExtraJvmArgs))
                foreach (var a in SplitArgs(instance.ExtraJvmArgs)) args.Add(a);

            args.Add(mainClass);

            // game args (modern arguments.game or legacy minecraftArguments)
            if (vroot.TryGetProperty("arguments", out var arguments2) && arguments2.TryGetProperty("game", out var gameArgs))
                AppendArgs(args, gameArgs, subst);
            else if (vroot.TryGetProperty("minecraftArguments", out var legacy) && legacy.ValueKind == JsonValueKind.String)
                foreach (var token in (legacy.GetString() ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    args.Add(Substitute(token, subst));

            var psi = new ProcessStartInfo
            {
                FileName = java,
                WorkingDirectory = gameDir,
                UseShellExecute = false,
                // §4b: do NOT redirect stdio of the windowed child — that can hang/freeze the game.
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                CreateNoWindow = false,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            CrashLogger.Log("MinecraftLauncher launch", ex);
            error = ex.Message;
            return null;
        }
    }

    /// <summary>
    /// 揀 java.exe · Resolve a java.exe: an explicit instance path wins, else a downloaded JRE matching the
    /// version's javaVersion.majorVersion under &lt;root&gt;\runtimes, else any runtime, else "" (caller errors).
    /// </summary>
    public static string ResolveJavaPath(string root, string versionJsonPath, string explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath)) return explicitPath;
        try
        {
            int major = 17;
            if (File.Exists(versionJsonPath))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(versionJsonPath));
                if (doc.RootElement.TryGetProperty("javaVersion", out var jv)
                    && jv.TryGetProperty("majorVersion", out var mv) && mv.ValueKind == JsonValueKind.Number)
                    major = mv.GetInt32();
            }
            var runtimes = Path.Combine(root, "runtimes");
            var preferred = Path.Combine(runtimes, major.ToString(), "bin", "javaw.exe");
            if (File.Exists(preferred)) return preferred;
            var preferredJava = Path.Combine(runtimes, major.ToString(), "bin", "java.exe");
            if (File.Exists(preferredJava)) return preferredJava;
            if (Directory.Exists(runtimes))
            {
                foreach (var exe in Directory.EnumerateFiles(runtimes, "javaw.exe", SearchOption.AllDirectories))
                    return exe;
            }
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher resolve-java", ex); }
        return "";
    }

    /// <summary>呢個版本要嘅 Java 大版本 · The Java major version a version JSON requests (default 17).</summary>
    public static int RequiredJavaMajor(string versionJsonPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(versionJsonPath));
            if (doc.RootElement.TryGetProperty("javaVersion", out var jv)
                && jv.TryGetProperty("majorVersion", out var mv) && mv.ValueKind == JsonValueKind.Number)
                return mv.GetInt32();
        }
        catch { }
        return 17;
    }

    // -------------------------------------------------------------- helpers

    private static async Task DownloadVerifiedAsync(HttpClient http, string url, string path, string sha1, CancellationToken ct)
    {
        if (File.Exists(path) && (sha1.Length == 0 || await Sha1Async(path) == sha1)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".part";
        using (var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct))
        {
            resp.EnsureSuccessStatusCode();
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None);
            await src.CopyToAsync(dst, ct);
        }
        if (sha1.Length > 0)
        {
            var got = await Sha1Async(tmp);
            if (!string.Equals(got, sha1, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(tmp); } catch { }
                throw new IOException($"SHA1 mismatch for {Path.GetFileName(path)}");
            }
        }
        try { if (File.Exists(path)) File.Delete(path); } catch { }
        File.Move(tmp, path);
    }

    private static async Task<string> Sha1Async(string path)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var sha = SHA1.Create();
        var hash = await sha.ComputeHashAsync(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ExtractNatives(string archivePath, string targetDir)
    {
        try
        {
            using var zip = ZipFile.OpenRead(archivePath);
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;
                if (entry.FullName.StartsWith("META-INF", StringComparison.OrdinalIgnoreCase)) continue;
                if (!entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                    && !entry.Name.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
                    && !entry.Name.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)) continue;
                var dest = Path.Combine(targetDir, entry.Name);
                if (File.Exists(dest)) continue;
                entry.ExtractToFile(dest, overwrite: true);
            }
        }
        catch (Exception ex) { CrashLogger.Log("MinecraftLauncher natives", ex); }
    }

    /// <summary>評估 library 嘅 rules（OS 過濾）· Evaluate a library's rules to decide if it applies on Windows.</summary>
    private static bool RulesAllow(JsonElement element)
    {
        if (!element.TryGetProperty("rules", out var rules) || rules.ValueKind != JsonValueKind.Array) return true;
        bool allowed = false;
        foreach (var rule in rules.EnumerateArray())
        {
            bool matches = true;
            if (rule.TryGetProperty("os", out var os) && os.TryGetProperty("name", out var osName))
                matches = string.Equals(osName.GetString(), "windows", StringComparison.OrdinalIgnoreCase);
            if (matches)
                allowed = string.Equals(Str(rule, "action"), "allow", StringComparison.OrdinalIgnoreCase);
        }
        return allowed;
    }

    private static string NativeClassifier(JsonElement lib)
    {
        if (lib.TryGetProperty("natives", out var natives) && natives.TryGetProperty("windows", out var win))
            return (win.GetString() ?? "").Replace("${arch}", Environment.Is64BitOperatingSystem ? "64" : "32");
        return "";
    }

    private static void AppendArgs(List<string> outArgs, JsonElement argArray, Dictionary<string, string> subst)
    {
        foreach (var el in argArray.EnumerateArray())
        {
            if (el.ValueKind == JsonValueKind.String)
                outArgs.Add(Substitute(el.GetString() ?? "", subst));
            else if (el.ValueKind == JsonValueKind.Object)
            {
                if (!RulesAllow(el)) continue;
                if (el.TryGetProperty("value", out var val))
                {
                    if (val.ValueKind == JsonValueKind.String) outArgs.Add(Substitute(val.GetString() ?? "", subst));
                    else if (val.ValueKind == JsonValueKind.Array)
                        foreach (var v in val.EnumerateArray())
                            if (v.ValueKind == JsonValueKind.String) outArgs.Add(Substitute(v.GetString() ?? "", subst));
                }
            }
        }
    }

    private static string Substitute(string token, Dictionary<string, string> subst)
    {
        foreach (var kv in subst)
            token = token.Replace("${" + kv.Key + "}", kv.Value);
        return token;
    }

    private static IEnumerable<string> SplitArgs(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static HttpClient NewHttp()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("WinForge-Launcher/1.0");
        return http;
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}

/// <summary>
/// 啟動器 instance 清單（per-instance，無 static 現用狀態）· The persisted list of launcher instances,
/// stored at %APPDATA%\.minecraft_winforge\instances.json. Each instance is fully independent.
/// </summary>
public static class MinecraftInstanceStore
{
    private static readonly string FilePath = Path.Combine(MinecraftLauncherService.DefaultRoot, "instances.json");
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Opts = new() { WriteIndented = true };

    private static List<MinecraftInstance> _cache = Load();

    private static List<MinecraftInstance> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<List<MinecraftInstance>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    private static void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(MinecraftLauncherService.DefaultRoot);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(_cache, Opts));
        }
        catch { }
    }

    public static List<MinecraftInstance> All()
    {
        lock (Gate) return _cache.Select(i => i.Clone()).ToList();
    }

    public static void Save(MinecraftInstance instance)
    {
        lock (Gate)
        {
            var idx = _cache.FindIndex(i => i.Id == instance.Id);
            if (idx >= 0) _cache[idx] = instance.Clone();
            else _cache.Add(instance.Clone());
            SaveLocked();
        }
    }

    public static void Delete(string id)
    {
        lock (Gate) { _cache.RemoveAll(i => i.Id == id); SaveLocked(); }
    }
}
