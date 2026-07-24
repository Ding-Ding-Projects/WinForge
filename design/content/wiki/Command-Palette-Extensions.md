# Command Palette Extension Packs · 指令面板擴充套件

WinForge supports a safe, declarative extension-pack format for Command Palette. It is designed for predictable personal automation without giving an imported JSON file permission to run code.

## Trust boundary · 信任界線

- Import is explicit and happens through the Command Palette settings page.
- WinForge copies a validated manifest to `%LOCALAPPDATA%\WinForge\CommandPaletteExtensions`.
- A newly imported pack is disabled by default.
- Enablement is stored separately from the imported manifest.
- Pack and command IDs, schema version, manifest size, command count, string sizes, and targets are validated.
- Unknown or malformed manifests are ignored instead of executed.

## Supported actions · 支援操作

| Action | Behavior |
| --- | --- |
| `Module` | Opens a registered WinForge module tag such as `module.awake`. |
| `Url` | Opens an absolute `http` or `https` URL. |
| `Copy` | Copies up to 4096 characters to the clipboard. |

There is no `Run`, `PowerShell`, executable path, DLL, script, COM, or arbitrary-code action. This phase is intentionally not an out-of-process third-party extension host.

## Minimal manifest · 最小資訊檔

```json
{
  "schema": 1,
  "id": "example.quick-actions",
  "name": "Example quick actions",
  "zh": "示範快速操作",
  "description": "A safe declarative Command Palette extension pack.",
  "zhDescription": "安全、宣告式嘅 Command Palette 擴充套件。",
  "commands": [
    {
      "id": "open-awake",
      "title": "Open Awake",
      "zh": "開啟 Awake",
      "subtitle": "Open the WinForge Awake module",
      "zhSubtitle": "開啟 WinForge Awake 模組",
      "keywords": ["awake", "keep awake"],
      "aliases": ["wake"],
      "action": "Module",
      "target": "module.awake",
      "glyph": "\\uE8A7"
    }
  ]
}
```

## 使用方法

1. Open `Command Palette · 指令面板` in WinForge.
2. Select `Create template · 建立範本`, then edit the JSON with one of the supported actions.
3. Select `Import manifest · 匯入資訊檔`.
4. Review the pack row and turn on `Enabled · 已啟用` only when you trust the manifest.
5. Search by command title, Cantonese title, aliases, keywords, or pack name.

## 下一步

目前呢個基礎提供安全同可管理嘅 JSON 指令。之後嘅 parity 工作會以明確選用、隔離嘅跨程序協定加入豐富清單、詳細內容同表單頁面，而唔會放寬呢個安全界線。

### Isolated extension host protocol · 隔離擴充套件主機協定

Extension packs can optionally declare a local, absolute `.exe` host with a required SHA-256 pin. A `Host` command launches that executable as a **short-lived, non-elevated child process** and exchanges one JSON-lines request and one bounded JSON-lines response. The host must exit within eight seconds.

WinForge checks the pinned hash when importing and immediately before every launch. New packs remain disabled by default, and hosts fail closed while WinForge is elevated. Host output can only request a registered WinForge module, an HTTP(S) URL, bounded clipboard text, or a validated structured page with text, fields, choices, and buttons. WinForge never renders host HTML or script.

This is process isolation plus a narrow WinForge integration surface. It is **not** a sandbox: an executable a user explicitly enables can still act with that user's normal Windows permissions. See [Command Palette Extension Protocol](Command-Palette-Extension-Protocol.md) before trusting a host.

### 隔離擴充套件主機協定

擴充套件可以選擇宣告本機絕對 `.exe` 主機，並且一定要提供 SHA-256 pin。`Host` 指令會用**短生命週期、非提升權限嘅子程序**啟動主機，交換一個 JSON-lines 請求同一個有限大小嘅 JSON-lines 回應；主機要喺八秒內結束。

WinForge 匯入時同每次啟動前都會檢查雜湊。新套件預設會停用，而 WinForge 提升權限時主機會 fail closed。主機輸出只可以要求已註冊 WinForge 模組、HTTP(S) 網址、有限長度剪貼簿文字，或者含文字、欄位、選項同按鈕嘅已驗證結構化頁面。WinForge 唔會渲染主機 HTML 或指令稿。

呢個係程序隔離加上狹窄嘅 WinForge 整合介面，**唔係**沙箱：用戶明確啟用嘅可執行檔仍然可以使用該用戶嘅一般 Windows 權限。信任主機之前請先睇協定文件。
