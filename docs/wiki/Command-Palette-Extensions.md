# Command Palette Extension Packs · 指令面板擴充套件

WinForge supports declarative Command Palette packs for predictable personal automation. A manifest cannot execute code by itself. A pack may separately declare a hash-pinned local host, but WinForge launches that executable only after the user explicitly enables the pack and the execution-time trust checks pass.

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
| `Host` | Sends the command to the pack's explicitly trusted extension host and accepts only the bounded responses below. |

The declarative actions do not expose `Run`, PowerShell, scripts, DLL loading, COM activation, or arbitrary command execution through WinForge. `Host` is different: it runs the exact executable the user chose to trust, so that executable is not sandboxed even though its WinForge response surface is narrow.

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

## Isolated extension host protocol · 隔離擴充套件主機協定

Extension packs can optionally declare a fully qualified local-drive `.exe` host with a required SHA-256 pin; UNC, network-share, and device paths are rejected. A `Host` command launches that executable as a **short-lived, non-elevated child process** and exchanges one JSON-lines request and one bounded JSON-lines response. The host must exit within eight seconds.

WinForge reloads the manifest and enabled marker for every action, re-checks the command and host definition, and holds a no-write/no-delete file lease from SHA-256 verification through process creation. New packs remain disabled by default, hosts fail closed while WinForge is elevated, and closing the launcher cancels the bounded request. Host output can only request a registered WinForge module, an HTTP(S) URL, bounded clipboard text, or a validated structured page with labelled text fields, toggles, choices, and buttons. WinForge never renders host HTML or script.

This is process isolation plus a narrow WinForge integration surface. It is **not** a sandbox: an executable a user explicitly enables can still act with that user's normal Windows permissions. See [Command Palette Extension Protocol](Command-Palette-Extension-Protocol.md) before trusting a host. The focused headless contract is `dotnet run --project tests/CommandPaletteExtensionHost.Tests -c Debug`.

### 粵語說明

擴充套件可以選擇宣告本機磁碟嘅完整 `.exe` 路徑，並且一定要提供 SHA-256 pin；UNC、網絡分享同裝置路徑一律拒絕。`Host` 指令會用**短生命週期、非提升權限嘅子程序**啟動主機，交換一個 JSON-lines 請求同一個有限大小嘅 JSON-lines 回應；主機要喺八秒內結束。

WinForge 每次操作都會重新載入資訊檔同啟用標記、再核對指令／主機定義，並由 SHA-256 驗證一路鎖住檔案唔畀寫入或刪除，直到建立程序。新套件預設停用；WinForge 提升權限時會 fail closed，而關閉 launcher 會取消有界請求。主機輸出只可以要求已註冊模組、HTTP(S) 網址、有限剪貼簿文字，或者用有標籤文字欄、開關、選項同按鈕組成嘅已驗證結構化頁面；絕對唔會渲染主機 HTML 或指令稿。

呢個係程序隔離加狹窄 WinForge 介面，**唔係**沙箱：用戶明確啟用嘅可執行檔仍然有該用戶嘅一般 Windows 權限。信任之前請睇完整協定；專項 headless 測試 command 亦記喺上面。
