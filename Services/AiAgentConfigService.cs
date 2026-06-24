using System;
using System.IO;
using System.Text;
using System.Text.Json;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>
/// AI 代理設定檔的安全讀寫 · Safe read/write of AI agent config files.
/// 解析每個工具的設定檔路徑（~ 或 XDG_CONFIG_HOME），原子寫入（暫存檔 + 取代），
/// 自動建立父目錄，並驗證 JSON。全部防禦性寫法，永遠唔會擲例外。
/// Resolves each tool's config path (~ or XDG_CONFIG_HOME), writes atomically (temp + replace),
/// auto-creates parent dirs, and validates JSON. Defensive throughout — never throws; never logs file contents.
/// </summary>
public static class AiAgentConfigService
{
    /// <summary>使用者主目錄 · The user's home directory (~), or "" on failure.</summary>
    public static string Home()
    {
        try { return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile); }
        catch { return ""; }
    }

    /// <summary>
    /// XDG 設定基底目錄 · The XDG config base dir: $XDG_CONFIG_HOME, else ~/.config.
    /// </summary>
    public static string XdgConfigHome()
    {
        try
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (!string.IsNullOrWhiteSpace(xdg)) return xdg;
        }
        catch { }
        var home = Home();
        return string.IsNullOrEmpty(home) ? "" : Path.Combine(home, ".config");
    }

    /// <summary>
    /// 解析設定檔的絕對路徑 · Resolve a config file's absolute path, or null if base unknown.
    /// </summary>
    public static string? Resolve(AiConfigFile file)
    {
        if (file is null || string.IsNullOrWhiteSpace(file.RelativePath)) return null;
        try
        {
            var baseDir = file.UseXdgConfig ? XdgConfigHome() : Home();
            if (string.IsNullOrEmpty(baseDir)) return null;
            var rel = file.RelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(baseDir, rel));
        }
        catch { return null; }
    }

    /// <summary>設定檔存唔存在 · Does the config file exist on disk?</summary>
    public static bool Exists(AiConfigFile file)
    {
        var p = Resolve(file);
        try { return p is not null && File.Exists(p); }
        catch { return false; }
    }

    /// <summary>讀取設定檔內容 · Read the file's text. Returns (success, contents).</summary>
    public static (bool ok, string text) Read(AiConfigFile file)
    {
        var p = Resolve(file);
        if (p is null) return (false, "");
        try
        {
            if (!File.Exists(p)) return (false, "");
            return (true, File.ReadAllText(p));
        }
        catch { return (false, ""); }
    }

    /// <summary>由絕對路徑讀取 · Read text from an arbitrary absolute path (Browse fallback).</summary>
    public static (bool ok, string text) ReadPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return (false, "");
        try
        {
            if (!File.Exists(path)) return (false, "");
            return (true, File.ReadAllText(path));
        }
        catch { return (false, ""); }
    }

    /// <summary>
    /// 驗證 JSON 是否有效 · Validate a string parses as JSON (comments/trailing commas allowed).
    /// 回傳 (有效, 錯誤訊息或 null) · Returns (valid, errorMessage-or-null).
    /// </summary>
    public static (bool valid, string? error) ValidateJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (true, null); // empty = will create empty file, allowed
        try
        {
            var opts = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            };
            using var _ = JsonDocument.Parse(text, opts);
            return (true, null);
        }
        catch (JsonException ex)
        {
            // 只回傳位置／類別，唔包含檔案內容 · Surface only location/kind, never file contents.
            return (false, $"Line {ex.LineNumber + 1}, position {ex.BytePositionInLine + 1}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// 原子寫入設定檔 · Write the file atomically (temp + replace), creating parent dirs.
    /// 逐字寫返使用者嘅文字，唔會重新格式化／排序（避免損失）· Writes verbatim — never reformats.
    /// </summary>
    public static TweakResult Save(AiConfigFile file, string text)
    {
        var p = Resolve(file);
        if (p is null)
            return TweakResult.Fail("Could not resolve config path.", "無法解析設定檔路徑。");
        return SavePath(p, text);
    }

    /// <summary>原子寫入指定絕對路徑 · Write atomically to an absolute path.</summary>
    public static TweakResult SavePath(string? path, string text)
    {
        if (string.IsNullOrWhiteSpace(path))
            return TweakResult.Fail("No path.", "冇路徑。");
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            // UTF-8 無 BOM · UTF-8 without BOM.
            var enc = new UTF8Encoding(false);
            var tmp = path + ".winforge.tmp";
            File.WriteAllText(tmp, text ?? "", enc);

            if (File.Exists(path))
            {
                // File.Replace 係原子 · File.Replace is atomic on the same volume.
                try
                {
                    File.Replace(tmp, path, null);
                }
                catch
                {
                    // 後備：刪後改名 · Fallback: delete + move.
                    File.Delete(path);
                    File.Move(tmp, path);
                }
            }
            else
            {
                File.Move(tmp, path);
            }

            return TweakResult.Ok($"Saved {Path.GetFileName(path)}.", $"已儲存 {Path.GetFileName(path)}。");
        }
        catch (UnauthorizedAccessException)
        {
            return TweakResult.Fail("Access denied writing the file.", "寫入檔案時被拒絕存取。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not save: {ex.Message}", $"無法儲存：{ex.Message}");
        }
    }

    /// <summary>
    /// 喺檔案總管開啟設定檔所在資料夾（如有檔案就選中佢）· Open the file's folder in Explorer.
    /// </summary>
    public static TweakResult OpenFolder(AiConfigFile file)
    {
        var p = Resolve(file);
        if (p is null)
            return TweakResult.Fail("Could not resolve config path.", "無法解析設定檔路徑。");
        return OpenFolderPath(p);
    }

    public static TweakResult OpenFolderPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return TweakResult.Fail("No path.", "冇路徑。");
        try
        {
            if (File.Exists(path))
            {
                // 選中該檔案 · Select the file in Explorer.
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true,
                });
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir))
                    return TweakResult.Fail("No folder.", "冇資料夾。");
                Directory.CreateDirectory(dir);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true,
                });
            }
            return TweakResult.Ok("Opened folder.", "已開啟資料夾。");
        }
        catch (Exception ex)
        {
            return TweakResult.Fail($"Could not open folder: {ex.Message}", $"無法開啟資料夾：{ex.Message}");
        }
    }
}
