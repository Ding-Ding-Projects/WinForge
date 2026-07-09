using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Controls;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>一個以「上游原語言」寫成嘅 WinForge 隨附 app · One WinForge companion app written in the
/// upstream app's LITERAL language. Two kinds: <see cref="CompanionKind.Web"/> (TypeScript/JS served in a
/// WinForge WebView2 popup — e.g. Monaco, VS Code's real editor engine) and <see cref="CompanionKind.Native"/>
/// (C++ source shipped with WinForge, compiled on demand, launched as a WinForge-branded native popup).</summary>
public enum CompanionKind { Web, Native }

/// <summary>隨附 app 規格 · Companion-app spec (data only; the service does the work).</summary>
public sealed record CompanionSpec
{
    public required string Id { get; init; }
    public required CompanionKind Kind { get; init; }
    public required string TitleEn { get; init; }
    public required string TitleZh { get; init; }
    /// <summary>Web：WebApps 下嘅資料夾名 · Web: folder name under WebApps.</summary>
    public string WebFolder { get; init; } = "";
    /// <summary>Web：係咪需要 Monaco 引擎（首次自動下載）· Web: needs the Monaco engine (auto-downloaded once).</summary>
    public bool NeedsMonaco { get; init; }
    /// <summary>Native：輸出 exe 名 · Native: output exe name.</summary>
    public string ExeName { get; init; } = "";
    /// <summary>Native：隨 app 出貨嘅 C++ 原始碼（相對 AppContext.BaseDirectory）· Native: shipped C++ source.</summary>
    public string SourceRel { get; init; } = "";
    /// <summary>Native：MinGW/clang 連結旗標 · Native: MinGW/clang link flags.</summary>
    public string MinGwLibs { get; init; } = "";
    /// <summary>Native：MSVC 連結 .lib 清單 · Native: MSVC link libraries.</summary>
    public string MsvcLibs { get; init; } = "";
}

/// <summary>
/// 隨附 app 引擎 · The engine behind "Open full features · 開啟完整功能": WinForge-authored companion apps
/// written in each upstream app's literal language, opened as WinForge-branded popups. Web companions get
/// their engine dependency (Monaco) auto-downloaded with streaming progress; native (C++) companions get
/// their dependency — a C++ TOOLCHAIN — auto-installed via winget when absent (llvm-mingw), then the shipped
/// WinForge source is compiled on demand and cached by source hash. Everything is WinForge: our source, our
/// branding; nothing here launches an upstream product. Bilingual; never throws except on cancellation.
/// </summary>
public static class CompanionAppService
{
    // ── Catalog ──────────────────────────────────────────────────────────────

    public static readonly IReadOnlyList<CompanionSpec> All = new List<CompanionSpec>
    {
        new()
        {
            Id = "codeforge", Kind = CompanionKind.Web,
            TitleEn = "WinForge Code Editor", TitleZh = "程式碼編輯器",
            WebFolder = "codeforge", NeedsMonaco = true,
        },
        new()
        {
            Id = "diagramforge", Kind = CompanionKind.Web,
            TitleEn = "WinForge Diagram Studio", TitleZh = "圖表工作室",
            WebFolder = "diagramforge",
        },
        new()
        {
            Id = "imageforge", Kind = CompanionKind.Native,
            TitleEn = "WinForge Image Editor", TitleZh = "影像編輯器",
            ExeName = "WinForgeImageEditor.exe",
            SourceRel = @"native\imageforge\main.cpp",
            MinGwLibs = "-lgdiplus -lgdi32 -lcomdlg32 -lole32 -lshell32 -lcomctl32",
            MsvcLibs = "gdiplus.lib gdi32.lib user32.lib comdlg32.lib ole32.lib shell32.lib comctl32.lib",
        },
        new()
        {
            Id = "audioforge", Kind = CompanionKind.Native,
            TitleEn = "WinForge Audio Editor", TitleZh = "音訊編輯器",
            ExeName = "WinForgeAudioEditor.exe",
            SourceRel = @"native\audioforge\main.cpp",
            MinGwLibs = "-lwinmm -lgdi32 -lcomdlg32 -lcomctl32 -lshell32 -lole32",
            MsvcLibs = "winmm.lib gdi32.lib user32.lib comdlg32.lib comctl32.lib shell32.lib ole32.lib",
        },
    };

    public static CompanionSpec? ById(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null
        : All.FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

    // ── Monaco (the web companions' engine dependency) ───────────────────────

    /// <summary>釘死版本，離線可重現 · Pinned engine version (deterministic, offline once fetched).</summary>
    private const string MonacoVersion = "0.52.2";
    private const string MonacoTgzUrl =
        "https://registry.npmjs.org/monaco-editor/-/monaco-editor-" + MonacoVersion + ".tgz";

    /// <summary>weblibs 根（虛擬主機 libs.winforge 映射呢度）· Root mapped to https://libs.winforge .</summary>
    public static string WebLibsDir
    {
        get
        {
            var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "weblibs");
            try { Directory.CreateDirectory(d); } catch { }
            return d;
        }
    }

    public static bool MonacoInstalled =>
        File.Exists(Path.Combine(WebLibsDir, "monaco", "vs", "loader.js"));

    /// <summary>
    /// 確保 Monaco 引擎喺本機（無就下載+解壓，串流進度）· Ensure the Monaco engine exists locally; if
    /// missing, download the pinned npm tarball and extract <c>package/min/vs</c> → <c>weblibs\monaco\vs</c>
    /// with streaming percent progress. Never throws except on cancellation.
    /// </summary>
    public static async Task<TweakResult> EnsureMonacoAsync(IProgress<InstallProgressReport>? progress,
        CancellationToken ct = default)
    {
        if (MonacoInstalled)
            return TweakResult.Ok("Editor engine ready.", "編輯器引擎已就緒。");

        var tmpTgz = Path.Combine(Path.GetTempPath(), $"winforge-monaco-{Guid.NewGuid():N}.tgz");
        try
        {
            progress?.Report(InstallProgressReport.Status(
                $"Downloading Monaco {MonacoVersion} (VS Code's editor engine)…",
                $"下載緊 Monaco {MonacoVersion}（VS Code 編輯器引擎）…"));

            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            using (var resp = await http.GetAsync(MonacoTgzUrl, HttpCompletionOption.ResponseHeadersRead, ct))
            {
                resp.EnsureSuccessStatusCode();
                long total = resp.Content.Headers.ContentLength ?? -1;
                await using var body = await resp.Content.ReadAsStreamAsync(ct);
                await using var file = File.Create(tmpTgz);
                var buf = new byte[81920];
                long done = 0;
                int n;
                while ((n = await body.ReadAsync(buf, ct)) > 0)
                {
                    await file.WriteAsync(buf.AsMemory(0, n), ct);
                    done += n;
                    if (total > 0)
                        progress?.Report(InstallProgressReport.Progress(done * 90.0 / total,
                            $"Downloading engine… {done / (1024 * 1024)} MB", $"下載緊引擎… {done / (1024 * 1024)} MB"));
                }
            }

            progress?.Report(InstallProgressReport.Progress(92, "Extracting engine…", "解壓緊引擎…"));

            // Extract only package/min/vs/** → weblibs\monaco\vs\**
            var vsRoot = Path.Combine(WebLibsDir, "monaco", "vs");
            var staging = vsRoot + ".staging";
            try { if (Directory.Exists(staging)) Directory.Delete(staging, true); } catch { }
            Directory.CreateDirectory(staging);

            const string prefix = "package/min/vs/";
            await using (var fs = File.OpenRead(tmpTgz))
            await using (var gz = new GZipStream(fs, CompressionMode.Decompress))
            using (var tar = new TarReader(gz))
            {
                TarEntry? entry;
                while ((entry = await tar.GetNextEntryAsync(cancellationToken: ct)) is not null)
                {
                    if (entry.EntryType != TarEntryType.RegularFile) continue;
                    var name = entry.Name.Replace('\\', '/');
                    if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    var rel = name[prefix.Length..];
                    if (rel.Length == 0 || rel.Contains("..")) continue;
                    var dest = Path.Combine(staging, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    await using var os = File.Create(dest);
                    if (entry.DataStream is not null) await entry.DataStream.CopyToAsync(os, ct);
                }
            }

            if (!File.Exists(Path.Combine(staging, "loader.js")))
                return TweakResult.Fail("The downloaded engine package looked wrong (no loader.js).",
                    "下載返嚟嘅引擎套件唔對辦（冇 loader.js）。");

            // Atomic-ish swap into place.
            try { if (Directory.Exists(vsRoot)) Directory.Delete(vsRoot, true); } catch { }
            Directory.CreateDirectory(Path.GetDirectoryName(vsRoot)!);
            Directory.Move(staging, vsRoot);

            progress?.Report(InstallProgressReport.Progress(100, "Editor engine ready.", "編輯器引擎已就緒。"));
            return TweakResult.Ok("Editor engine installed.", "編輯器引擎已安裝。");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Engine download failed: {ex.Message}", $"引擎下載失敗：{ex.Message}", ex.ToString());
        }
        finally
        {
            try { if (File.Exists(tmpTgz)) File.Delete(tmpTgz); } catch { }
        }
    }

    // ── Native (C++) companions: toolchain auto-install + compile-on-demand ──

    /// <summary>冇編譯器時自動安裝嘅工具鏈（winget 已驗證）· Toolchain auto-installed when none exists.</summary>
    private const string ToolchainWingetId = "MartinStorsjo.LLVM-MinGW.UCRT";

    private static string NativeCacheDir(CompanionSpec spec)
    {
        var d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinForge", "native", spec.Id);
        try { Directory.CreateDirectory(d); } catch { }
        return d;
    }

    /// <summary>隨 app 出貨嘅原始碼路徑 · Path of the shipped C++ source (copied next to the app).</summary>
    public static string SourcePath(CompanionSpec spec) =>
        Path.Combine(AppContext.BaseDirectory, spec.SourceRel);

    /// <summary>已可即刻啟動？（預編譯或快取命中）· Ready to launch instantly (prebuilt or cache hit)?</summary>
    public static bool NativeReady(CompanionSpec spec) => ResolveNativeExe(spec) is not null;

    /// <summary>搵可即用嘅 exe：預編譯 native\bin → 依原始碼 hash 嘅快取 · Resolve a ready exe.</summary>
    public static string? ResolveNativeExe(CompanionSpec spec)
    {
        try
        {
            var prebuilt = Path.Combine(AppContext.BaseDirectory, "native", "bin", spec.ExeName);
            if (File.Exists(prebuilt)) return prebuilt;
            var src = SourcePath(spec);
            if (!File.Exists(src)) return null;
            var cached = Path.Combine(NativeCacheDir(spec), SourceHash(src), spec.ExeName);
            return File.Exists(cached) ? cached : null;
        }
        catch { return null; }
    }

    private static string SourceHash(string srcPath)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(srcPath);
        return Convert.ToHexString(sha.ComputeHash(fs))[..16].ToLowerInvariant();
    }

    /// <summary>
    /// 確保原生隨附 app 就緒 · Ensure the native companion is ready to launch: use a prebuilt/cached exe;
    /// otherwise find a C++ compiler (PATH g++/clang++ → winget Links → winget Packages → MSVC via vswhere),
    /// AUTO-INSTALLING llvm-mingw via winget when none exists, then compile the shipped WinForge source
    /// (streaming compiler output) and cache the exe by source hash. Returns the exe path in Output.
    /// </summary>
    public static async Task<TweakResult> EnsureNativeAsync(CompanionSpec spec,
        IProgress<InstallProgressReport>? progress, CancellationToken ct = default)
    {
        var existing = ResolveNativeExe(spec);
        if (existing is not null)
            return TweakResult.Ok("Ready.", "已就緒。", existing);

        var src = SourcePath(spec);
        if (!File.Exists(src))
            return TweakResult.Fail($"Shipped source not found: {spec.SourceRel}",
                $"搵唔到隨附原始碼：{spec.SourceRel}");

        // 1) Find (or auto-install) a compiler — the app's literal dependency.
        progress?.Report(InstallProgressReport.Progress(5,
            "Looking for a C++ toolchain…", "搵緊 C++ 工具鏈…"));
        var compiler = FindCompiler();
        if (compiler is null)
        {
            progress?.Report(InstallProgressReport.Progress(10,
                "No C++ toolchain — installing llvm-mingw automatically…",
                "冇 C++ 工具鏈 — 自動安裝緊 llvm-mingw…"));
            var onLine = new Progress<string>(l => progress?.Report(InstallProgressReport.FromLine(l)));
            var inst = await PackageService.AutoInstallDetailed(ToolchainWingetId, onLine, ct);
            if (!inst.Success)
                return TweakResult.Fail(
                    $"Couldn't install the C++ toolchain ({ToolchainWingetId}). {inst.Message?.En}",
                    $"安裝唔到 C++ 工具鏈（{ToolchainWingetId}）。{inst.Message?.Zh}", inst.Output);
            compiler = FindCompiler();
            if (compiler is null)
                return TweakResult.Fail("Toolchain installed but no g++/clang++ was found — restart WinForge and retry.",
                    "工具鏈已安裝但搵唔到 g++／clang++ — 請重開 WinForge 再試。");
        }

        // 2) Compile the shipped WinForge source.
        var outDir = Path.Combine(NativeCacheDir(spec), SourceHash(src));
        Directory.CreateDirectory(outDir);
        var outExe = Path.Combine(outDir, spec.ExeName);
        progress?.Report(InstallProgressReport.Progress(35,
            $"Compiling {spec.TitleEn} from source ({compiler.Value.kind})…",
            $"由原始碼編譯緊 {spec.TitleZh}（{compiler.Value.kind}）…"));

        var lineSink = new Progress<string>(l => progress?.Report(InstallProgressReport.FromLine(l)));
        TweakResult built = compiler.Value.kind switch
        {
            "msvc" => await CompileMsvc(compiler.Value.path, src, outExe, spec.MsvcLibs, lineSink, ct),
            _ => await CompileMinGw(compiler.Value.path, src, outExe, spec.MinGwLibs, lineSink, ct),
        };

        if (!built.Success || !File.Exists(outExe))
        {
            try { if (File.Exists(outExe)) File.Delete(outExe); } catch { }
            return TweakResult.Fail(
                $"Compilation failed. {built.Message?.En}", $"編譯失敗。{built.Message?.Zh}", built.Output);
        }

        progress?.Report(InstallProgressReport.Progress(100,
            $"{spec.TitleEn} compiled and ready.", $"{spec.TitleZh} 已編譯完成，可以啟動。"));
        return TweakResult.Ok("Compiled.", "已編譯。", outExe);
    }

    /// <summary>啟動原生隨附 app（自己嘅原生視窗）· Launch the native companion in its own window.</summary>
    public static TweakResult LaunchNative(CompanionSpec spec)
    {
        var exe = ResolveNativeExe(spec);
        if (exe is null)
            return TweakResult.Fail($"{spec.TitleEn} is not built yet.", $"{spec.TitleZh} 仲未編譯好。");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            });
            return TweakResult.Ok($"Launched {spec.TitleEn}.", $"已啟動 {spec.TitleZh}。", exe);
        }
        catch (Exception ex) { return TweakResult.Fail(ex.Message, $"出錯：{ex.Message}"); }
    }

    // ── Compiler discovery + invocation ──────────────────────────────────────

    /// <summary>搵編譯器 · Find a compiler: g++/clang++ on PATH → winget Links → winget Packages (llvm-mingw)
    /// → MSVC (vswhere → VsDevCmd). Returns (kind, path) or null.</summary>
    private static (string kind, string path)? FindCompiler()
    {
        // PATH + winget Links: g++ / c++ / clang++
        foreach (var stem in new[] { "g++", "clang++", "c++" })
        {
            var hit = ProbePath(stem);
            if (hit is not null) return ("mingw", hit);
        }

        // winget portable Packages dir (user + machine): MartinStorsjo.LLVM-MinGW*\...\bin\g++.exe
        foreach (var pkgRoot in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "Microsoft", "WinGet", "Packages"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinGet", "Packages"),
                 })
        {
            try
            {
                if (!Directory.Exists(pkgRoot)) continue;
                foreach (var dir in Directory.EnumerateDirectories(pkgRoot, "MartinStorsjo.LLVM-MinGW*"))
                {
                    var direct = Path.Combine(dir, "bin", "g++.exe");
                    if (File.Exists(direct)) return ("mingw", direct);
                    foreach (var sub in Directory.EnumerateDirectories(dir))
                    {
                        var nested = Path.Combine(sub, "bin", "g++.exe");
                        if (File.Exists(nested)) return ("mingw", nested);
                    }
                }
            }
            catch { }
        }

        // MSVC via vswhere → VsDevCmd.bat
        try
        {
            var vswhere = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio", "Installer", "vswhere.exe");
            if (File.Exists(vswhere))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = vswhere,
                    Arguments = "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                var outp = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit(8000);
                var install = outp.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(install))
                {
                    var devCmd = Path.Combine(install!, "Common7", "Tools", "VsDevCmd.bat");
                    if (File.Exists(devCmd)) return ("msvc", devCmd);
                }
            }
        }
        catch { }

        return null;
    }

    private static string? ProbePath(string stem)
    {
        var dirs = new List<string>();
        dirs.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "WinGet", "Links"));
        foreach (var dir in dirs)
        {
            try
            {
                var c = Path.Combine(dir.Trim(), stem + ".exe");
                if (File.Exists(c)) return c;
            }
            catch { }
        }
        return null;
    }

    private static Task<TweakResult> CompileMinGw(string gpp, string src, string outExe, string libs,
        IProgress<string> onLine, CancellationToken ct) =>
        ShellRunner.RunStreaming(gpp,
            $"-std=c++17 -O2 -municode -mwindows \"{src}\" -o \"{outExe}\" {libs}",
            onLine, elevated: false, workingDirectory: Path.GetDirectoryName(outExe), ct: ct);

    private static Task<TweakResult> CompileMsvc(string vsDevCmd, string src, string outExe, string libs,
        IProgress<string> onLine, CancellationToken ct)
    {
        // Run inside the VS developer environment; keep .obj noise in the output dir.
        var workDir = Path.GetDirectoryName(outExe)!;
        var cmd = $"\"{vsDevCmd}\" -arch=x64 -no_logo && " +
                  $"cl /nologo /EHsc /O2 /std:c++17 /DUNICODE /D_UNICODE \"{src}\" /Fe:\"{outExe}\" " +
                  $"/Fo:\"{workDir}\\\\\" /link {libs} /SUBSYSTEM:WINDOWS";
        return ShellRunner.RunStreaming("cmd.exe", $"/s /c \"{cmd}\"",
            onLine, elevated: false, workingDirectory: workDir, ct: ct);
    }
}
