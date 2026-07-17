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