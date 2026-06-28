using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// Minecraft 世界工具管理 · Minecraft world tooling orchestration for Chunker-style converters and BlueMap.
/// Keeps external tools out-of-process, stages Chunker input into bounded batches to avoid long-run memory
/// leaks, generates BlueMap config files, and tracks a live BlueMap render/web process.
/// </summary>
public static class MinecraftWorldToolsService
{
    private const string Prefix = "mc.worldtools.";
    private static Process? _blueMapProcess;

    public sealed record BatchPlan(int Index, long Bytes, int Files, string StageDir, string OutputDir);

    public static string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinForge", "MinecraftWorldTools");

    public static string WorkDir
    {
        get
        {
            var saved = SettingsStore.Get(Prefix + "workDir", "");
            return string.IsNullOrWhiteSpace(saved) ? AppDir : saved;
        }
    }

    public static string ChunkerTool => SettingsStore.Get(Prefix + "chunkerTool", "");
    public static string ChunkerArgs => SettingsStore.Get(Prefix + "chunkerArgs",
        "--input {input} --output {output} --target {target}");
    public static string ChunkerTarget => SettingsStore.Get(Prefix + "chunkerTarget", "java");
    public static int ChunkerBatchMb => ReadInt("chunkerBatchMb", 500, 64, 4096);
    public static string BlueMapJar => SettingsStore.Get(Prefix + "blueMapJar", "");
    public static string BlueMapArgs => SettingsStore.Get(Prefix + "blueMapArgs",
        "-Xmx{memoryMb}m -jar {jar} -r -c {config} -w {world}");
    public static int BlueMapMemoryMb => ReadInt("blueMapMemoryMb", 4096, 512, 131072);
    public static int BlueMapThreads => ReadInt("blueMapThreads", Math.Max(1, Environment.ProcessorCount / 2), 1, 128);
    public static int BlueMapPort => ReadInt("blueMapPort", 8100, 1, 65535);
    public static bool BlueMapWebServer => ReadBool("blueMapWebServer", true);

    public static void SaveSettings(
        string workDir,
        string chunkerTool,
        string chunkerArgs,
        string chunkerTarget,
        int chunkerBatchMb,
        string blueMapJar,
        string blueMapArgs,
        int blueMapMemoryMb,
        int blueMapThreads,
        int blueMapPort,
        bool blueMapWebServer)
    {
        SettingsStore.Set(Prefix + "workDir", workDir ?? "");
        SettingsStore.Set(Prefix + "chunkerTool", chunkerTool ?? "");
        SettingsStore.Set(Prefix + "chunkerArgs", chunkerArgs ?? "");
        SettingsStore.Set(Prefix + "chunkerTarget", chunkerTarget ?? "java");
        SettingsStore.Set(Prefix + "chunkerBatchMb", Math.Clamp(chunkerBatchMb, 64, 4096).ToString(CultureInfo.InvariantCulture));
        SettingsStore.Set(Prefix + "blueMapJar", blueMapJar ?? "");
        SettingsStore.Set(Prefix + "blueMapArgs", blueMapArgs ?? "");
        SettingsStore.Set(Prefix + "blueMapMemoryMb", Math.Clamp(blueMapMemoryMb, 512, 131072).ToString(CultureInfo.InvariantCulture));
        SettingsStore.Set(Prefix + "blueMapThreads", Math.Clamp(blueMapThreads, 1, 128).ToString(CultureInfo.InvariantCulture));
        SettingsStore.Set(Prefix + "blueMapPort", Math.Clamp(blueMapPort, 1, 65535).ToString(CultureInfo.InvariantCulture));
        SettingsStore.Set(Prefix + "blueMapWebServer", blueMapWebServer ? "1" : "0");
    }

    public static string? FindJava() => MinecraftService.FindJava();

    public static bool IsValidWorld(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return false;
        return File.Exists(Path.Combine(folder, "level.dat")) ||
               Directory.Exists(Path.Combine(folder, "region")) ||
               Directory.Exists(Path.Combine(folder, "db"));
    }

    public static string DescribeWorld(string folder)
    {
        if (!Directory.Exists(folder)) return "";
        var size = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Sum(f => SafeLength(f));
        var regions = CountFiles(Path.Combine(folder, "region"), "*.mca") +
                      CountFiles(Path.Combine(folder, "DIM-1", "region"), "*.mca") +
                      CountFiles(Path.Combine(folder, "DIM1", "region"), "*.mca");
        return $"{FormatBytes(size)} · {regions} region files";
    }

    public static IReadOnlyList<BatchPlan> PreviewChunkerBatches(string world, string output)
    {
        if (!IsValidWorld(world)) return Array.Empty<BatchPlan>();
        var maxBytes = Math.Max(64, ChunkerBatchMb) * 1024L * 1024L;
        return BuildBatches(world, output, maxBytes, createDirs: false, onLog: null);
    }

    public static async Task<TweakResult> RunChunkerBatched(
        string world,
        string output,
        Action<string>? onLog,
        CancellationToken ct = default)
    {
        if (!IsValidWorld(world))
            return TweakResult.Fail("Choose a valid Minecraft world first.", "請先揀一個有效嘅 Minecraft 世界。");
        if (string.IsNullOrWhiteSpace(output))
            return TweakResult.Fail("Choose an output folder first.", "請先揀輸出資料夾。");
        if (string.IsNullOrWhiteSpace(ChunkerTool) || !File.Exists(ChunkerTool))
            return TweakResult.Fail("Configure the Chunker executable or jar first.", "請先設定 Chunker 執行檔或 jar。");

        try
        {
            Directory.CreateDirectory(output);
            Directory.CreateDirectory(WorkDir);
            var batches = BuildBatches(world, output, ChunkerBatchMb * 1024L * 1024L, createDirs: true, onLog);
            if (batches.Count == 0)
                return TweakResult.Fail("No world files were found to convert.", "搵唔到可轉換嘅世界檔案。");

            onLog?.Invoke($"Chunker batches: {batches.Count} at <= {ChunkerBatchMb} MB each.");
            foreach (var batch in batches)
            {
                ct.ThrowIfCancellationRequested();
                onLog?.Invoke($"[batch {batch.Index:000}] {batch.Files} files, {FormatBytes(batch.Bytes)}");
                var (file, args) = ResolveToolCommand(ChunkerTool, Expand(ChunkerArgs, batch.StageDir, batch.OutputDir, world, ""));
                var code = await RunProcess(file, args, WorkDir, onLog, ct);
                if (code != 0)
                    return TweakResult.Fail($"Chunker batch {batch.Index} failed with exit code {code}.",
                        $"Chunker 第 {batch.Index} 批失敗，退出碼 {code}。");
            }

            return TweakResult.Ok($"Converted {batches.Count} batches into {output}.",
                $"已轉換 {batches.Count} 批到 {output}。", output);
        }
        catch (OperationCanceledException)
        {
            return TweakResult.Fail("Chunker conversion cancelled.", "Chunker 轉換已取消。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Chunker conversion failed: {ex.Message}", $"Chunker 轉換失敗：{ex.Message}");
        }
    }

    public static TweakResult GenerateBlueMapConfig(string world, string output)
    {
        if (!IsValidWorld(world))
            return TweakResult.Fail("Choose a valid Minecraft world first.", "請先揀一個有效嘅 Minecraft 世界。");
        if (string.IsNullOrWhiteSpace(output))
            return TweakResult.Fail("Choose a BlueMap output folder first.", "請先揀 BlueMap 輸出資料夾。");

        try
        {
            var configDir = BlueMapConfigDir;
            Directory.CreateDirectory(configDir);
            Directory.CreateDirectory(Path.Combine(configDir, "maps"));
            Directory.CreateDirectory(Path.Combine(configDir, "web"));
            Directory.CreateDirectory(output);

            File.WriteAllText(Path.Combine(configDir, "core.conf"), BlueMapCoreConfig(output), Encoding.UTF8);
            File.WriteAllText(Path.Combine(configDir, "webserver.conf"), BlueMapWebConfig(), Encoding.UTF8);
            File.WriteAllText(Path.Combine(configDir, "maps", "overworld.conf"), BlueMapWorldConfig(world), Encoding.UTF8);

            return TweakResult.Ok($"BlueMap config written to {configDir}.", $"BlueMap 設定已寫入 {configDir}。", configDir);
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not write BlueMap config: {ex.Message}", $"寫入 BlueMap 設定失敗：{ex.Message}");
        }
    }

    public static async Task<TweakResult> StartBlueMap(string world, string output, Action<string>? onLog, CancellationToken ct = default)
    {
        if (_blueMapProcess is { HasExited: false })
            return TweakResult.Fail("BlueMap is already running.", "BlueMap 已經運行緊。");
        if (string.IsNullOrWhiteSpace(BlueMapJar) || !File.Exists(BlueMapJar))
            return TweakResult.Fail("Configure BlueMap's jar first.", "請先設定 BlueMap jar。");

        var config = GenerateBlueMapConfig(world, output);
        if (!config.Success) return config;

        var java = FindJava();
        if (java is null)
            return TweakResult.Fail("Java was not found. Install JDK 21+ first.", "搵唔到 Java。請先安裝 JDK 21+。");

        try
        {
            Directory.CreateDirectory(output);
            var args = Expand(BlueMapArgs, "", output, world, BlueMapJar)
                .Replace("{config}", Quote(BlueMapConfigDir), StringComparison.OrdinalIgnoreCase)
                .Replace("{memoryMb}", BlueMapMemoryMb.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

            _blueMapProcess = StartTracked(java, args, WorkDir, onLog);
            _ = Task.Run(async () =>
            {
                try { await _blueMapProcess.WaitForExitAsync(ct); }
                catch { }
                onLog?.Invoke("[BlueMap stopped]");
            }, CancellationToken.None);

            return TweakResult.Ok("BlueMap render started.", "BlueMap 算圖已啟動。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not start BlueMap: {ex.Message}", $"啟動 BlueMap 失敗：{ex.Message}");
        }
    }

    public static TweakResult StopBlueMap()
    {
        try
        {
            if (_blueMapProcess is null || _blueMapProcess.HasExited)
                return TweakResult.Fail("BlueMap is not running.", "BlueMap 未運行。");
            _blueMapProcess.Kill(entireProcessTree: true);
            return TweakResult.Ok("BlueMap stopped.", "BlueMap 已停止。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not stop BlueMap: {ex.Message}", $"停止 BlueMap 失敗：{ex.Message}");
        }
    }

    public static bool IsBlueMapRunning => _blueMapProcess is { HasExited: false };
    public static string BlueMapConfigDir => Path.Combine(WorkDir, "bluemap-config");

    private static List<BatchPlan> BuildBatches(string world, string output, long maxBytes, bool createDirs, Action<string>? onLog)
    {
        var batchRoot = Path.Combine(WorkDir, "chunker-batches", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        var files = Directory.EnumerateFiles(world, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("session.lock", StringComparison.OrdinalIgnoreCase))
            .Select(f => new { Path = f, Relative = Path.GetRelativePath(world, f), Size = SafeLength(f) })
            .OrderBy(f => f.Relative, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var rootFiles = files.Where(f => !f.Relative.Contains(Path.DirectorySeparatorChar) && !f.Relative.Contains(Path.AltDirectorySeparatorChar)).ToList();
        var payload = files.Except(rootFiles).ToList();
        var plans = new List<BatchPlan>();
        var current = new List<dynamic>();
        long currentBytes = 0;
        int index = 1;

        void Flush()
        {
            if (current.Count == 0) return;
            var stage = Path.Combine(batchRoot, $"batch-{index:000}", "world");
            var outDir = Path.Combine(output, $"batch-{index:000}");
            if (createDirs)
            {
                Directory.CreateDirectory(stage);
                Directory.CreateDirectory(outDir);
                foreach (var f in rootFiles.Concat(current.Cast<dynamic>()))
                {
                    string source = f.Path;
                    string rel = f.Relative;
                    var dest = Path.Combine(stage, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    File.Copy(source, dest, overwrite: true);
                }
                onLog?.Invoke($"Staged batch {index:000} at {stage}");
            }
            plans.Add(new BatchPlan(index, currentBytes, current.Count, stage, outDir));
            current.Clear();
            currentBytes = 0;
            index++;
        }

        foreach (var file in payload)
        {
            if (current.Count > 0 && currentBytes + file.Size > maxBytes) Flush();
            current.Add(file);
            currentBytes += file.Size;
            if (file.Size >= maxBytes) Flush();
        }
        Flush();
        return plans;
    }

    private static (string file, string args) ResolveToolCommand(string tool, string args)
    {
        if (tool.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            var java = FindJava() ?? "java";
            return (java, $"-jar {Quote(tool)} {args}");
        }
        return (tool, args);
    }

    private static Process StartTracked(string file, string args, string workdir, Action<string>? onLog)
    {
        Directory.CreateDirectory(workdir);
        var psi = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = workdir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
        p.OutputDataReceived += (_, e) => { if (e.Data is not null) onLog?.Invoke(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLog?.Invoke(e.Data); };
        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        return p;
    }

    private static async Task<int> RunProcess(string file, string args, string workdir, Action<string>? onLog, CancellationToken ct)
    {
        using var p = StartTracked(file, args, workdir, onLog);
        await p.WaitForExitAsync(ct);
        return p.ExitCode;
    }

    private static string Expand(string template, string input, string output, string world, string jar)
        => template
            .Replace("{input}", Quote(input), StringComparison.OrdinalIgnoreCase)
            .Replace("{output}", Quote(output), StringComparison.OrdinalIgnoreCase)
            .Replace("{world}", Quote(world), StringComparison.OrdinalIgnoreCase)
            .Replace("{jar}", Quote(jar), StringComparison.OrdinalIgnoreCase)
            .Replace("{target}", ChunkerTarget, StringComparison.OrdinalIgnoreCase)
            .Replace("{threads}", BlueMapThreads.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{port}", BlueMapPort.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);

    private static string BlueMapCoreConfig(string output)
        => $$"""
           accept-download: true
           render-thread-count: {{BlueMapThreads}}
           data: "{{Slash(Path.Combine(output, "data"))}}"
           webroot: "{{Slash(Path.Combine(output, "web"))}}"
           metrics: false
           """;

    private static string BlueMapWebConfig()
        => $$"""
           enabled: {{BlueMapWebServer.ToString().ToLowerInvariant()}}
           webroot: "{{Slash(Path.Combine(WorkDir, "bluemap-web"))}}"
           port: {{BlueMapPort}}
           """;

    private static string BlueMapWorldConfig(string world)
        => $$"""
           world: "{{Slash(world)}}"
           dimension: "minecraft:overworld"
           name: "Overworld"
           enabled: true
           """;

    private static int ReadInt(string key, int fallback, int min, int max)
        => int.TryParse(SettingsStore.Get(Prefix + key, fallback.ToString(CultureInfo.InvariantCulture)), out var value)
            ? Math.Clamp(value, min, max)
            : fallback;

    private static bool ReadBool(string key, bool fallback)
    {
        var value = SettingsStore.Get(Prefix + key, fallback ? "1" : "0");
        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static long SafeLength(string file)
    {
        try { return new FileInfo(file).Length; } catch { return 0; }
    }

    private static int CountFiles(string dir, string pattern)
    {
        try { return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, pattern).Count() : 0; } catch { return 0; }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.##} {units[unit]}";
    }

    private static string Quote(string value) => string.IsNullOrEmpty(value) ? "\"\"" : $"\"{value.Replace("\"", "\\\"")}\"";
    private static string Slash(string value) => value.Replace("\\", "/");
}
