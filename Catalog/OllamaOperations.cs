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
/// </summary>
public static class OllamaOperations
{
    public static IEnumerable<TweakDefinition> All() => new List<TweakDefinition>
    {
        Tweak.Action("ollama.serve", "Start Ollama server", "啟動 Ollama 伺服器",
            "Launch `ollama serve` so the local API on port 11434 comes up.",
            "執行 `ollama serve`，令本機 11434 埠嘅 API 起返。",
            "Start", "啟動",
            _ => Task.FromResult(OllamaService.StartServe()
                ? TweakResult.Ok("Server starting · 伺服器啟動緊", "伺服器啟動緊")
                : TweakResult.Fail("Could not start ollama serve · 啟動唔到", "啟動唔到")),
            keywords: "serve start server 啟動 伺服器"),

        Tweak.Shell("ollama.version", "Ollama version (CLI)", "Ollama 版本（CLI）",
            "Print the installed Ollama version via the CLI.", "用 CLI 印出已安裝嘅 Ollama 版本。",
            "Check", "查睇", "ollama", "--version", keywords: "version cli 版本"),

        Tweak.Shell("ollama.list", "List models (CLI)", "列出模型（CLI）",
            "List installed models via `ollama list`.", "用 `ollama list` 列出已安裝模型。",
            "List", "列出", "ollama", "list", keywords: "list models cli 列出 模型"),

        Tweak.Shell("ollama.ps", "Running models (CLI)", "運行中模型（CLI）",
            "Show models loaded in memory via `ollama ps`.", "用 `ollama ps` 顯示載入記憶體嘅模型。",
            "Show", "顯示", "ollama", "ps", keywords: "ps running cli 運行 記憶體"),

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
}
