using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using WinForge.Models;

namespace WinForge.Services;

/// <summary>剪貼簿支援嘅內容類型 · The clipboard content kinds an action can consume.</summary>
[Flags]
public enum PasteContent
{
    None = 0,
    Text = 1,
    Image = 2,
}

/// <summary>一個轉換動作嘅靜態描述 · Static metadata describing one paste-transform action.</summary>
public sealed class PasteAction
{
    public string Id { get; init; } = "";
    public string Glyph { get; init; } = "";
    public LocalizedText Name { get; init; } = new("", "");
    public LocalizedText Blurb { get; init; } = new("", "");
    public PasteContent Accepts { get; init; } = PasteContent.Text;
    public bool RequiresAi { get; init; }

    /// <summary>純文字／純文字轉換（同步）· Pure text→text transform (most actions).</summary>
    public Func<string, string>? TextFn { get; init; }
}

/// <summary>
/// 進階貼上（PowerToys Advanced Paste 式）· Native clone of PowerToys Advanced Paste.
///
/// 將剪貼簿內容即時轉換成另一種格式再貼上。提供：
///   • 一籮唔使 AI 嘅直接轉換（純文字、Markdown、JSON、大小寫、URL/Base64/HTML 編解碼、OCR、CSV 轉置、排序…）。
///   • 一個全域熱鍵（預設 Win+Shift+V）開一個置頂面板，揀一個動作 → 轉換剪貼簿 → SendInput Ctrl+V 貼落作用中嘅 app。
///   • 一個「用 AI 貼上」自由文字框：把剪貼簿 + 指示送去已設定嘅供應商（重用 AiChatService），再貼結果。
///
/// Transforms clipboard content into a different format and pastes it. Pure WinRT clipboard + a
/// low-level keyboard hook + SendInput. No external tool, no redirect. Fully defensive. Bilingual.
/// </summary>
public static class AdvancedPasteService
{
    // ===================== Settings keys =====================
    private const string KeyHotkeyEnabled = "advancedpaste.hotkey.enabled";
    private const string KeyDisabledActions = "advancedpaste.disabled"; // comma-separated ids that are OFF
    private const string KeyDefaultAction = "advancedpaste.default";
    private const string KeyAiProvider = "advancedpaste.ai.provider";   // provider Id
    private const string KeyAiModel = "advancedpaste.ai.model";

    // ===================== Win32 =====================
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);
    [DllImport("user32.dll")] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT lpPoint);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_CONTROL = 0x11, VK_SHIFT = 0x10, VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_V = 0x56;
    private const ushort INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

    // ===================== Hotkey state =====================
    private static IntPtr _hook = IntPtr.Zero;
    private static LowLevelKeyboardProc? _proc;
    private static DispatcherQueue? _ui;
    private static readonly NativeMessagePump HookPump = new("WinForge-AdvancedPasteHook");
    private static volatile bool _injecting;

    /// <summary>熱鍵描述（顯示用）· Hotkey label shown in the UI.</summary>
    public const string HotkeyText = "Win + Shift + V";

    public static bool HotkeyActive => _hook != IntPtr.Zero;

    /// <summary>面板想開（由熱鍵觸發）· Raised on the UI thread when the hotkey wants to open the palette.</summary>
    public static event Action? PaletteRequested;

    // ===================== Action catalog =====================
    private static List<PasteAction>? _all;

    /// <summary>全部動作（順序固定）· All actions in display order.</summary>
    public static IReadOnlyList<PasteAction> All => _all ??= BuildActions();

    public static PasteAction? Find(string id) => All.FirstOrDefault(a => a.Id == id);

    /// <summary>使用者開咗嘅動作（過濾已停用）· Enabled actions only (default-action first).</summary>
    public static IReadOnlyList<PasteAction> Enabled
    {
        get
        {
            var off = DisabledIds;
            var list = All.Where(a => !off.Contains(a.Id)).ToList();
            var def = DefaultActionId;
            if (!string.IsNullOrEmpty(def))
            {
                var d = list.FirstOrDefault(a => a.Id == def);
                if (d is not null) { list.Remove(d); list.Insert(0, d); }
            }
            return list;
        }
    }

    private static List<PasteAction> BuildActions()
    {
        // 字典序排：Title-case 用 TextInfo · helpers reused below
        return new List<PasteAction>
        {
            new()
            {
                Id = "plaintext", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Paste as plain text", "純文字貼上"),
                Blurb = new("Strip all formatting; paste the raw text.", "剝走所有格式，貼純文字。"),
                TextFn = s => s,
            },
            new()
            {
                Id = "plainfromhtml", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Plain text from HTML", "由 HTML 抽純文字"),
                Blurb = new("Remove HTML tags, decode entities, keep the text.", "去除 HTML 標籤、解碼實體，只留文字。"),
                TextFn = HtmlToPlainText,
            },
            new()
            {
                Id = "markdown", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Paste as Markdown", "轉成 Markdown 貼上"),
                Blurb = new("Convert copied HTML into Markdown.", "把複製到嘅 HTML 轉成 Markdown。"),
                TextFn = HtmlToMarkdown,
            },
            new()
            {
                Id = "json", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Paste as JSON", "轉成 JSON 貼上"),
                Blurb = new("Pretty-print if already JSON, else wrap as a JSON string.", "本身係 JSON 就美化；否則包成 JSON 字串。"),
                TextFn = ToJson,
            },
            new()
            {
                Id = "uppercase", Glyph = "", Accepts = PasteContent.Text,
                Name = new("UPPERCASE", "全部大寫"),
                Blurb = new("Convert the text to upper case.", "全部轉做大寫。"),
                TextFn = s => s.ToUpperInvariant(),
            },
            new()
            {
                Id = "lowercase", Glyph = "", Accepts = PasteContent.Text,
                Name = new("lowercase", "全部細寫"),
                Blurb = new("Convert the text to lower case.", "全部轉做細寫。"),
                TextFn = s => s.ToLowerInvariant(),
            },
            new()
            {
                Id = "titlecase", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Title Case", "首字母大寫"),
                Blurb = new("Capitalise the first letter of each word.", "每個字嘅首字母大寫。"),
                TextFn = s => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant()),
            },
            new()
            {
                Id = "trim", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Trim & normalise whitespace", "修剪／正規化空白"),
                Blurb = new("Trim each line and collapse runs of spaces/tabs.", "每行修剪、合併連續空格／Tab。"),
                TextFn = NormalizeWhitespace,
            },
            new()
            {
                Id = "removeblank", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Remove blank lines", "刪除空白行"),
                Blurb = new("Drop empty / whitespace-only lines.", "刪走全空或者只得空白嘅行。"),
                TextFn = RemoveBlankLines,
            },
            new()
            {
                Id = "sortlines", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Sort lines", "排序行"),
                Blurb = new("Sort lines alphabetically (ascending).", "按字母順序（升序）排列各行。"),
                TextFn = SortLines,
            },
            new()
            {
                Id = "uniquelines", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Unique lines", "去重複行"),
                Blurb = new("Remove duplicate lines, keep first occurrence.", "刪走重複嘅行，保留第一次出現。"),
                TextFn = UniqueLines,
            },
            new()
            {
                Id = "transposecsv", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Transpose CSV", "轉置 CSV"),
                Blurb = new("Swap rows and columns of comma-separated data.", "將逗號分隔資料嘅行同列互換。"),
                TextFn = TransposeCsv,
            },
            new()
            {
                Id = "urlencode", Glyph = "", Accepts = PasteContent.Text,
                Name = new("URL-encode", "URL 編碼"),
                Blurb = new("Percent-encode the text for use in a URL.", "百分號編碼，方便放入網址。"),
                TextFn = s => Uri.EscapeDataString(s),
            },
            new()
            {
                Id = "urldecode", Glyph = "", Accepts = PasteContent.Text,
                Name = new("URL-decode", "URL 解碼"),
                Blurb = new("Decode percent-encoded text.", "解碼百分號編碼嘅文字。"),
                TextFn = s => { try { return Uri.UnescapeDataString(s); } catch { return s; } },
            },
            new()
            {
                Id = "base64encode", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Base64 encode", "Base64 編碼"),
                Blurb = new("Encode the text (UTF-8) as Base64.", "把文字（UTF-8）編成 Base64。"),
                TextFn = s => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)),
            },
            new()
            {
                Id = "base64decode", Glyph = "", Accepts = PasteContent.Text,
                Name = new("Base64 decode", "Base64 解碼"),
                Blurb = new("Decode Base64 back to UTF-8 text.", "把 Base64 解返做 UTF-8 文字。"),
                TextFn = Base64Decode,
            },
            new()
            {
                Id = "htmlencode", Glyph = "", Accepts = PasteContent.Text,
                Name = new("HTML-encode", "HTML 編碼"),
                Blurb = new("Escape &lt; &gt; &amp; \" for safe HTML.", "轉義 &lt; &gt; &amp; \" 方便放入 HTML。"),
                TextFn = HtmlEncode,
            },
            new()
            {
                Id = "htmldecode", Glyph = "", Accepts = PasteContent.Text,
                Name = new("HTML-decode", "HTML 解碼"),
                Blurb = new("Decode HTML entities back to characters.", "把 HTML 實體解返做字元。"),
                TextFn = System.Net.WebUtility.HtmlDecode,
            },
            new()
            {
                Id = "imagetotext", Glyph = "", Accepts = PasteContent.Image,
                Name = new("Image → text (OCR)", "圖片轉文字（OCR）"),
                Blurb = new("Run OCR on the copied image and paste the text.", "對複製嘅圖片做 OCR，貼出文字。"),
                // Image action: handled specially (async) — no TextFn.
            },
            new()
            {
                Id = "ai", Glyph = "", Accepts = PasteContent.Text, RequiresAi = true,
                Name = new("Paste with AI…", "用 AI 貼上…"),
                Blurb = new("Describe how to transform the clipboard; an AI rewrites it.", "用文字描述點轉換剪貼簿，AI 幫你改寫。"),
            },
        };
    }

    // ===================== Enable / settings =====================

    public static bool HotkeyEnabledSetting
    {
        get => SettingsStore.Get(KeyHotkeyEnabled, "false") == "true";
        set => SettingsStore.Set(KeyHotkeyEnabled, value ? "true" : "false");
    }

    private static HashSet<string> DisabledIds =>
        SettingsStore.Get(KeyDisabledActions, "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet();

    public static bool IsActionEnabled(string id) => !DisabledIds.Contains(id);

    public static void SetActionEnabled(string id, bool enabled)
    {
        var set = DisabledIds;
        if (enabled) set.Remove(id); else set.Add(id);
        SettingsStore.Set(KeyDisabledActions, string.Join(",", set));
    }

    public static string DefaultActionId
    {
        get => SettingsStore.Get(KeyDefaultAction, "plaintext");
        set => SettingsStore.Set(KeyDefaultAction, value ?? "");
    }

    public static string AiProviderId
    {
        get => SettingsStore.Get(KeyAiProvider, "");
        set => SettingsStore.Set(KeyAiProvider, value ?? "");
    }

    public static string AiModel
    {
        get => SettingsStore.Get(KeyAiModel, "");
        set => SettingsStore.Set(KeyAiModel, value ?? "");
    }

    /// <summary>有冇任何 AI 供應商可用 · Is at least one AI provider configured?</summary>
    public static bool AiAvailable => AiChatService.I.Providers.Count > 0;

    /// <summary>解析要用嘅 AI 供應商（設定優先，否則第一個）· Resolve the provider to use.</summary>
    public static AiProvider? ResolveAiProvider()
    {
        var id = AiProviderId;
        if (!string.IsNullOrEmpty(id))
        {
            var p = AiChatService.I.GetProvider(id);
            if (p is not null) return p;
        }
        return AiChatService.I.Providers.FirstOrDefault();
    }

    // ===================== Hotkey hook =====================

    public static void EnableHotkey(DispatcherQueue uiQueue)
    {
        _ui = uiQueue;
        HookPump.Post(InstallHotkey);
    }

    private static void InstallHotkey()
    {
        if (_hook != IntPtr.Zero || _ui is null) return;
        _proc = HookProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
    }

    public static void DisableHotkey()
    {
        HookPump.Post(() =>
        {
            if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
            _proc = null;
            _ui = null;
        });
    }

    private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && !_injecting)
        {
            int msg = (int)wParam;
            if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
            {
                var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
                bool win = (GetAsyncKeyState(VK_LWIN) & 0x8000) != 0 || (GetAsyncKeyState(VK_RWIN) & 0x8000) != 0;
                if (data.vkCode == VK_V && win && shift)
                {
                    _ui?.TryEnqueue(() => { try { PaletteRequested?.Invoke(); } catch { } });
                    return (IntPtr)1; // swallow Win+Shift+V so Windows doesn't see it
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // ===================== Cursor =====================

    /// <summary>滑鼠位置（虛擬螢幕座標）· Current cursor position in virtual-screen pixels.</summary>
    public static (int X, int Y) CursorPos()
    {
        try { return GetCursorPos(out var p) ? (p.X, p.Y) : (0, 0); }
        catch { return (0, 0); }
    }

    // ===================== Clipboard read =====================

    /// <summary>讀取剪貼簿文字 · Read clipboard text (empty when none).</summary>
    public static string ReadText()
    {
        try
        {
            var view = Clipboard.GetContent();
            if (view is null || !view.Contains(StandardDataFormats.Text)) return "";
            // Bound the wait: a clipboard momentarily locked by another app must not hang the UI thread.
            var task = view.GetTextAsync().AsTask();
            return task.Wait(TimeSpan.FromMilliseconds(600)) ? (task.Result ?? "") : "";
        }
        catch { return ""; }
    }

    public static bool HasText()
    {
        try { var v = Clipboard.GetContent(); return v is not null && v.Contains(StandardDataFormats.Text); }
        catch { return false; }
    }

    public static bool HasImage()
    {
        try { var v = Clipboard.GetContent(); return v is not null && v.Contains(StandardDataFormats.Bitmap); }
        catch { return false; }
    }

    /// <summary>剪貼簿而家有咩內容類型 · What content the clipboard currently holds.</summary>
    public static PasteContent CurrentContent()
    {
        var c = PasteContent.None;
        if (HasText()) c |= PasteContent.Text;
        if (HasImage()) c |= PasteContent.Image;
        return c;
    }

    /// <summary>把純文字放返剪貼簿 · Put plain text on the clipboard (formatting stripped).</summary>
    public static void SetClipboardText(string text)
    {
        try
        {
            var dp = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            dp.SetText(text ?? "");
            Clipboard.SetContent(dp);
            Clipboard.Flush();
        }
        catch { }
    }

    // ===================== OCR =====================

    /// <summary>對剪貼簿圖片做 OCR · Run OCR on the clipboard image, returning the recognised text.</summary>
    public static async Task<string> ClipboardImageToTextAsync(CancellationToken ct = default)
    {
        var view = Clipboard.GetContent();
        if (view is null || !view.Contains(StandardDataFormats.Bitmap))
            throw new InvalidOperationException("No image on the clipboard.");

        var bmpRef = await view.GetBitmapAsync();
        using var ras = await bmpRef.OpenReadAsync();
        var decoder = await BitmapDecoder.CreateAsync(ras);
        using var sb = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

        var engine = OcrEngine.TryCreateFromUserProfileLanguages()
            ?? OcrEngine.TryCreateFromLanguage(new Language("en-US"));
        if (engine is null)
            throw new InvalidOperationException(
                "No OCR language pack installed. Add one in Windows Settings → Time & language → Language & region.");

        ct.ThrowIfCancellationRequested();
        var result = await engine.RecognizeAsync(sb);
        return result?.Text ?? "";
    }

    // ===================== Run an action (preview, no paste) =====================

    /// <summary>
    /// 對剪貼簿執行一個動作，回傳轉換後嘅文字（淨係預覽，唔貼上）。
    /// Run an action against the current clipboard and return the transformed text (preview only, no paste).
    /// AI actions need <paramref name="aiInstruction"/>.
    /// </summary>
    public static async Task<string> RunAsync(PasteAction action, string? aiInstruction = null, CancellationToken ct = default)
    {
        if (action is null) return "";

        if (action.Id == "imagetotext")
            return await ClipboardImageToTextAsync(ct);

        if (action.RequiresAi)
            return await RunAiAsync(ReadText(), aiInstruction ?? "", ct);

        var text = ReadText();
        if (action.TextFn is not null)
        {
            try { return action.TextFn(text); }
            catch { return text; }
        }
        return text;
    }

    /// <summary>用 AI 改寫剪貼簿文字 · Rewrite clipboard text with the configured AI provider.</summary>
    public static async Task<string> RunAiAsync(string clipboard, string instruction, CancellationToken ct = default)
    {
        var provider = ResolveAiProvider();
        if (provider is null)
            throw new InvalidOperationException("No AI provider configured. Add one in AI Chat first.");
        if (string.IsNullOrWhiteSpace(instruction))
            throw new InvalidOperationException("Enter an instruction for the AI.");

        var model = !string.IsNullOrWhiteSpace(AiModel) ? AiModel : provider.DefaultModel;
        var chat = new ChatConversation
        {
            Model = model,
            Temperature = 0.2,
            SystemPrompt =
                "You transform clipboard text on request. Output ONLY the transformed text, with no preamble, " +
                "no explanation and no code fences. Preserve meaning unless asked otherwise.",
        };
        chat.Messages.Add(new ChatMessage
        {
            Role = ChatRoles.User,
            Content = $"Instruction: {instruction}\n\nClipboard content:\n{clipboard}",
        });

        var sb = new StringBuilder();
        bool? creditOk = null;
        LocalizedText? creditMessage = null;
        await AiChatService.I.StreamChatAsync(chat, provider, chunk =>
        {
            if (!string.IsNullOrEmpty(chunk.Delta)) sb.Append(chunk.Delta);
            if (chunk.CreditMessage is not null)
            {
                creditOk = chunk.CreditSuccess;
                creditMessage = chunk.CreditMessage;
            }
        }, ct);
        if (creditOk == false && creditMessage is not null)
            throw new InvalidOperationException(creditMessage.Primary);
        return sb.ToString().Trim();
    }

    // ===================== Transform → paste =====================

    /// <summary>
    /// 執行動作 → 換剪貼簿 → SendInput Ctrl+V 貼落作用中嘅 app。
    /// Run the action, replace the clipboard with the result, then synthesize Ctrl+V into the active app.
    /// Returns the transformed text (or null when nothing happened).
    /// </summary>
    public static async Task<string?> TransformAndPasteAsync(PasteAction action, string? aiInstruction = null, CancellationToken ct = default)
    {
        string result = await RunAsync(action, aiInstruction, ct);
        if (result is null) return null;
        SetClipboardText(result);
        // Give the foreground app a beat to settle (palette closed) before injecting.
        await Task.Delay(60, ct);
        InjectCtrlV();
        return result;
    }

    /// <summary>用 SendInput 模擬一次乾淨嘅 Ctrl+V · Synthesize a clean Ctrl+V via SendInput.</summary>
    public static void InjectCtrlV()
    {
        _injecting = true;
        try
        {
            var inputs = new[]
            {
                Key(VK_CONTROL, false),
                Key(VK_V, false),
                Key(VK_V, true),
                Key(VK_CONTROL, true),
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
        catch { }
        finally { _injecting = false; }
    }

    private static INPUT Key(int vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT { wVk = (ushort)vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 },
        },
    };

    // ===================== Pure transforms =====================

    private static string ToJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var trimmed = text.Trim();
        // Already JSON? → pretty-print.
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
        catch
        {
            // Not JSON → wrap as a JSON string literal.
            return JsonSerializer.Serialize(text, new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }
    }

    private static string Base64Decode(string text)
    {
        try
        {
            var bytes = Convert.FromBase64String(text.Trim());
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return text; }
    }

    private static string HtmlEncode(string text) => System.Net.WebUtility.HtmlEncode(text);

    private static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var collapsed = Regex.Replace(lines[i], "[ \t]+", " ").Trim();
            sb.Append(collapsed);
            if (i < lines.Length - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string RemoveBlankLines(string text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return string.Join("\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    private static string SortLines(string text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return string.Join("\n", lines.OrderBy(l => l, StringComparer.OrdinalIgnoreCase));
    }

    private static string UniqueLines(string text)
    {
        var lines = (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var seen = new HashSet<string>();
        var outp = new List<string>();
        foreach (var l in lines) if (seen.Add(l)) outp.Add(l);
        return string.Join("\n", outp);
    }

    private static string TransposeCsv(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var rows = (text).Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n', StringSplitOptions.None)
            .Where(r => r.Length > 0)
            .Select(r => r.Split(','))
            .ToList();
        if (rows.Count == 0) return text;
        int cols = rows.Max(r => r.Length);
        var sb = new StringBuilder();
        for (int c = 0; c < cols; c++)
        {
            for (int r = 0; r < rows.Count; r++)
            {
                sb.Append(c < rows[r].Length ? rows[r][c] : "");
                if (r < rows.Count - 1) sb.Append(',');
            }
            if (c < cols - 1) sb.Append('\n');
        }
        return sb.ToString();
    }

    // ----- HTML helpers (no external dependency) -----

    /// <summary>把剪貼簿 CF_HTML 嘅 metadata 剝走 · Strip CF_HTML clipboard metadata if present.</summary>
    private static string StripCfHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        // Many sources wrap the real fragment in <!--StartFragment--> … <!--EndFragment-->.
        const string startTag = "<!--StartFragment-->";
        const string endTag = "<!--EndFragment-->";
        int a = html.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        int b = html.IndexOf(endTag, StringComparison.OrdinalIgnoreCase);
        if (a >= 0 && b > a) return html.Substring(a + startTag.Length, b - a - startTag.Length);
        return html;
    }

    /// <summary>HTML → 純文字 · Strip tags and decode entities.</summary>
    public static string HtmlToPlainText(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        var s = StripCfHtml(html);
        s = Regex.Replace(s, "(?is)<(script|style)[^>]*>.*?</\\1>", "");
        s = Regex.Replace(s, "(?i)<br\\s*/?>", "\n");
        s = Regex.Replace(s, "(?i)</(p|div|li|tr|h[1-6])>", "\n");
        s = Regex.Replace(s, "<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, "[ \t]+\n", "\n");
        s = Regex.Replace(s, "\n{3,}", "\n\n");
        return s.Trim();
    }

    /// <summary>HTML → Markdown（輕量轉換，無外部相依）· Lightweight HTML→Markdown conversion.</summary>
    public static string HtmlToMarkdown(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;
        // If it doesn't look like HTML, return it unchanged.
        if (!Regex.IsMatch(html, "<[a-zA-Z!/]")) return html;

        var s = StripCfHtml(html);
        s = Regex.Replace(s, "(?is)<(script|style)[^>]*>.*?</\\1>", "");

        // Headings
        for (int i = 1; i <= 6; i++)
            s = Regex.Replace(s, $"(?is)<h{i}[^>]*>(.*?)</h{i}>",
                m => "\n" + new string('#', i) + " " + Inline(m.Groups[1].Value).Trim() + "\n");

        // Bold / italic
        s = Regex.Replace(s, "(?is)<(strong|b)[^>]*>(.*?)</\\1>", m => "**" + Inline(m.Groups[2].Value) + "**");
        s = Regex.Replace(s, "(?is)<(em|i)[^>]*>(.*?)</\\1>", m => "*" + Inline(m.Groups[2].Value) + "*");
        s = Regex.Replace(s, "(?is)<code[^>]*>(.*?)</code>", m => "`" + Inline(m.Groups[1].Value) + "`");

        // Links & images
        s = Regex.Replace(s, "(?is)<a[^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>",
            m => "[" + Inline(m.Groups[2].Value).Trim() + "](" + m.Groups[1].Value + ")");
        s = Regex.Replace(s, "(?is)<img[^>]*alt=\"([^\"]*)\"[^>]*src=\"([^\"]*)\"[^>]*>",
            m => "![" + m.Groups[1].Value + "](" + m.Groups[2].Value + ")");
        s = Regex.Replace(s, "(?is)<img[^>]*src=\"([^\"]*)\"[^>]*>", m => "![](" + m.Groups[1].Value + ")");

        // Lists
        s = Regex.Replace(s, "(?is)<li[^>]*>(.*?)</li>", m => "- " + Inline(m.Groups[1].Value).Trim() + "\n");
        s = Regex.Replace(s, "(?i)</?(ul|ol)[^>]*>", "\n");

        // Block-level breaks
        s = Regex.Replace(s, "(?i)<br\\s*/?>", "\n");
        s = Regex.Replace(s, "(?i)</(p|div|tr)>", "\n\n");

        // Remove anything else
        s = Regex.Replace(s, "<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, "[ \t]+\n", "\n");
        s = Regex.Replace(s, "\n{3,}", "\n\n");
        return s.Trim();

        static string Inline(string inner)
        {
            var t = Regex.Replace(inner, "<[^>]+>", "");
            return System.Net.WebUtility.HtmlDecode(t);
        }
    }
}
