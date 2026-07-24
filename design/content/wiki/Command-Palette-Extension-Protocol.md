# Command Palette Extension Protocol · 指令面板擴充套件協定

This document defines WinForge's optional out-of-process Command Palette host contract. It builds on the declarative extension-pack format and is designed to keep the WinForge-side integration surface small and reviewable.

## Trust model · 信任模型

- A pack is imported manually and remains disabled by default.
- A host must be a fully qualified local-drive `.exe` with a 64-character SHA-256 pin. UNC, network-share, and device paths are rejected.
- WinForge validates the executable and its hash at import. Before every action it reloads the stored manifest and enabled marker, re-checks the command/host definition, and validates the hash again.
- Hash verification holds a read-only file lease that denies writes and deletion until `CreateProcess` succeeds, closing the hash-to-launch replacement window.
- A host launches as a short-lived child process with `UseShellExecute=false`, no elevation, an explicit argument list, and an eight-second deadline.
- WinForge refuses to launch a host while WinForge itself is elevated.
- The protocol bounds response size and accepts only one response line; stderr is intentionally not surfaced because it may contain secrets.
- Host I/O is asynchronous. The Command Palette remains responsive, exposes an announced progress state, and cancelling/closing the launcher terminates its owned host process tree.

A pinned host is **not sandboxed**. If a user enables an executable, it retains the normal Windows permissions of that user. The protocol limits what that executable can ask WinForge to do, but it cannot make arbitrary native code safe.

## Manifest host declaration · 資訊檔主機宣告

```json
{
  "schema": 1,
  "id": "example.status-host",
  "name": "Example status host",
  "zh": "示範狀態主機",
  "host": {
    "executable": "C:\\Tools\\ExampleStatusHost.exe",
    "sha256": "replace-with-the-64-character-sha256-of-the-exe",
    "arguments": ["--winforge-command-palette"]
  },
  "commands": [
    {
      "id": "show-status",
      "title": "Show status",
      "zh": "顯示狀態",
      "action": "Host",
      "target": "show-status"
    }
  ]
}
```

The command target is a safe identifier that WinForge forwards as `commandTarget`; it is not a shell command or executable argument. If the pack is disabled, removed, or changes its pinned host/command after a result or structured page opened, the next action fails closed and must be reopened from the current pack.

## Transport · 傳輸

The host receives exactly one UTF-8 JSON line on standard input, writes exactly one UTF-8 JSON response line to standard output, then exits. Use no logging on stdout. Diagnostic stderr is discarded by WinForge.

```json
{
  "protocol": "winforge.command-palette.host/1",
  "requestId": "unique-request-id",
  "kind": "execute",
  "packId": "example.status-host",
  "commandId": "show-status",
  "commandTarget": "show-status",
  "pageId": null,
  "actionId": null,
  "fields": null
}
```

A page-button request uses `kind: "pageAction"`, includes `pageId` and `actionId`, and supplies the current validated field values as `fields`.

## Accepted responses · 接受回應

Every response must echo the exact `protocol` and `requestId`.

| `kind` | Required data | WinForge behavior |
| --- | --- | --- |
| `module` | `target` is a registered `module.*` tag | Opens that WinForge module. |
| `url` | `target` is an absolute HTTP(S) URL | Opens the URL. |
| `copy` | `target` is at most 4096 characters | Copies the text. |
| `page` | validated `page` object | Opens/updates WinForge's native structured-page window. |

No response can request process launch, PowerShell, scripts, arbitrary file access, elevation, HTML, JavaScript, or COM activation through WinForge.

## Structured pages · 結構化頁面

A page carries bilingual text plus up to 16 fields and eight actions, with at most one primary action. Supported field types are `Text`, `Toggle`, and `Choice`; a choice has at most 32 safe identifier values, and submitted text is bounded to 4096 characters per field. WinForge displays the data through keyboard-reachable native controls with associated accessible names, an announced status bar, vertically flowing action buttons, and the current app theme. The host cannot inject markup or bypass light/dark contrast resources.

```json
{
  "protocol": "winforge.command-palette.host/1",
  "requestId": "unique-request-id",
  "kind": "page",
  "page": {
    "id": "status.page",
    "title": "Status",
    "zh": "狀態",
    "body": "Choose what to refresh.",
    "zhBody": "揀要重新整理嘅內容。",
    "fields": [
      {
        "id": "scope",
        "label": "Scope",
        "zh": "範圍",
        "type": "Choice",
        "value": "summary",
        "options": [
          { "value": "summary", "title": "Summary", "zh": "摘要" },
          { "value": "detail", "title": "Detail", "zh": "詳細" }
        ]
      }
    ],
    "actions": [
      { "id": "refresh", "title": "Refresh", "zh": "重新整理", "primary": true }
    ]
  }
}
```

## 實作提示

主機應該把 stdout 保留畀唯一 JSON 回應、設定合理逾時、驗證傳入欄位，並且永遠唔好假設 WinForge 會執行任意命令。用戶改動主機 `.exe` 後，雜湊會唔再吻合，WinForge 會拒絕運行，直到資訊檔連同已審視嘅新 SHA-256 再次匯入。

## Verification · 驗證

Run `dotnet run --project tests/CommandPaletteExtensionHost.Tests -c Debug`. The focused harness covers canonical/local path and hash policy, unsafe arguments, disabled/elevated/undeclared commands, exact copy text, bilingual structured pages, URL and request-correlation rejection, undefined field types, multiple primary actions, response-size bounds, cancellation, and submitted-field bounds without touching a real extension pack.

執行上面 command 會用自包含 fixture host 驗證本機路徑／雜湊、危險參數、停用／提升權限／未宣告指令、準確複製文字、雙語結構頁、網址同 request 關聯拒絕、未知欄位類型、多個主要操作、回應大小、取消同欄位上限；全程唔會郁真實擴充套件。
