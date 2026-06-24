using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using WinForge.Models;
using WinForge.Services;

namespace WinForge.Catalog;

/// <summary>
/// Ollama 操作目錄 · A handful of lifecycle / utility operations for Ollama: start the server,
/// print the version / model list via the CLI, stop a loaded model, open the models folder and the
/// model library. Model pull / delete / chat are done natively over the REST API in the page; these
/// are the convenience CLI/lifecycle bits. Bilingual.
///
/// 顯示升級（行為不變）· Presentation upgrade (behaviour unchanged): the lifecycle / CLI cards that
/// depend on the `ollama` executable now carry a cheap, synchronous "Installed / Not found" status
/// pill driven by a read-only PATH probe. This is an honest binary-presence check — NOT the async
/// REST reachability probe in <see cref="OllamaService.IsReachableAsync"/>, which must never block the
/// UI thread. Every card Id and the exact command/body each card runs are preserved byte-for-byte.
/// </summary>
public static class OllamaOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        // 啟動伺服器 · Start the local server. Behaviour identical to the original Tweak.Action;
        // only a synchronous PATH-presence pill is added (StartServe still just spawns `ollama serve`).
        WithInstalledPill(
            Tweak.Action("ollama.serve", "Start Ollama server", "啟動 Ollama 伺服器",
                "Launch `ollama serve` so the local API on port 11434 comes up.",
                "執行 `ollama serve`，令本機 11434 埠嘅 API 起返。",
                "Start", "啟動",
                _ => Task.FromResult(OllamaService.StartServe()
                    ? TweakResult.Ok("Server starting · 伺服器啟動緊", "伺服器啟動緊")
                    : TweakResult.Fail("Could not start ollama serve · 啟動唔到", "啟動唔到")),
                keywords: "serve start server 啟動 伺服器")),

        WithInstalledPill(
            Tweak.Shell("ollama.version", "Ollama version (CLI)", "Ollama 版本（CLI）",
                "Print the installed Ollama version via the CLI.", "用 CLI 印出已安裝嘅 Ollama 版本。",
                "Check", "查睇", "ollama", "--version", keywords: "version cli 版本")),

        WithInstalledPill(
            Tweak.Shell("ollama.list", "List models (CLI)", "列出模型（CLI）",
                "List installed models via `ollama list`.", "用 `ollama list` 列出已安裝模型。",
                "List", "列出", "ollama", "list", keywords: "list models cli 列出 模型")),

        WithInstalledPill(
            Tweak.Shell("ollama.ps", "Running models (CLI)", "運行中模型（CLI）",
                "Show models loaded in memory via `ollama ps`.", "用 `ollama ps` 顯示載入記憶體嘅模型。",
                "Show", "顯示", "ollama", "ps", keywords: "ps running cli 運行 記憶體")),

        Tweak.Action("ollama.open-models", "Open models folder", "開啟模型資料夾",
            "Open the folder where Ollama stores model blobs (~/.ollama/models or $OLLAMA_MODELS).",
            "開啟 Ollama 存放模型嘅資料夾（~/.ollama/models 或 $OLLAMA_MODELS）。",
            "Open", "開啟",
            _ =>
            {
                try
                {
                    var dir = OllamaService.ModelsFolder;
                    if (!Directory.Exists(dir))
                        return Task.FromResult(TweakResult.Fail($"Folder not found: {dir}", $"搵唔到資料夾：{dir}"));
                    Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
                    return Task.FromResult(TweakResult.Ok("Opened · 已開啟", "已開啟"));
                }
                catch (System.Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
            },
            keywords: "models folder blobs open 資料夾 模型"),

        Tweak.Action("ollama.library", "Open model library", "開啟模型庫",
            "Open ollama.com/library in your browser to browse pullable models.",
            "喺瀏覽器開 ollama.com/library 揀可下載嘅模型。",
            "Open", "開啟",
            _ =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo { FileName = "https://ollama.com/library", UseShellExecute = true });
                    return Task.FromResult(TweakResult.Ok("Opened · 已開啟", "已開啟"));
                }
                catch (System.Exception ex) { return Task.FromResult(TweakResult.Fail(ex.Message, $"出錯：{ex.Message}")); }
            },
            keywords: "library browse models website 模型庫 瀏覽"),
    };

    // ======================================================================
    //  Presentation helpers (PRESENTATION + WIRING ONLY — behaviour unchanged)
    //  顯示輔助（只改顯示同接線，行為完全不變）
    // ======================================================================

    /// <summary>
    /// 將「ollama 已安裝／搵唔到」彩色藥丸掛上一張既有卡片，其 Id 同行為一律不變 ·
    /// Return a copy of <paramref name="def"/> with an added synchronous "ollama Installed / Not found"
    /// status pill. Every other member — Id, Kind, the exact <see cref="TweakDefinition.RunAsync"/>
    /// body, labels, keywords — is carried over verbatim, so the click behaviour is byte-for-byte
    /// identical; only the card gains a pill. The probe is the read-only PATH check below — cheap and
    /// synchronous — never the async REST reachability call (that must not block the UI thread).
    /// </summary>
    private static TweakDefinition WithInstalledPill(TweakDefinition def)
        => new()
        {
            Id = def.Id,
            Title = def.Title,
            Description = def.Description,
            Kind = def.Kind,
            RequiresAdmin = def.RequiresAdmin,
            Destructive = def.Destructive,
            Restart = def.Restart,
            Keywords = def.Keywords,
            ActionLabel = def.ActionLabel,
            RunAsync = def.RunAsync,
            ColoredStatus = () => OnPath("ollama")
                ? ("Installed", "已安裝", StatusColor.Good)
                : ("Not found", "搵唔到", StatusColor.Bad),
        };

    /// <summary>
    /// 喺 PATH 度搵可執行檔（純唯讀探測）· Resolve any of the given executable names on PATH
    /// (semicolon-separated). Tries each PATHEXT-style extension; a pure read-only probe that never
    /// launches anything. Mirrors the helper in DevTerminalTweaks so the "installed" pill stays
    /// consistent across the catalog.
    /// </summary>
    private static bool OnPath(string exeNames)
    {
        try
        {
            var names = exeNames.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            var exts = new[] { ".exe", ".cmd", ".bat", ".com" };
            foreach (var name in names)
                foreach (var dir in dirs)
                {
                    if (string.IsNullOrWhiteSpace(dir)) continue;
                    var d = dir.Trim();
                    // Name verbatim (covers names that already carry an extension).
                    try { if (File.Exists(Path.Combine(d, name))) return true; }
                    catch { /* skip bad PATH entry */ }
                    // Otherwise append each common executable extension.
                    if (!Path.HasExtension(name))
                        foreach (var ext in exts)
                        {
                            try { if (File.Exists(Path.Combine(d, name + ext))) return true; }
                            catch { /* skip */ }
                        }
                }
        }
        catch { /* treat as not found */ }
        return false;
    }
}
