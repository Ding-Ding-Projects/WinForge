# DecodeBtn · Button

**EN —** Action/control documented from the WinUI XAML source for **Text to Binary**.
**粵語 —** 呢個動作／控制項係由 **文字轉二進位** 嘅 WinUI XAML 來源整理出嚟。

| Field · 欄位 | Value · 值 |
|---|---|
| Module · 模組 | [Text to Binary · 文字轉二進位](../../../features/markup-docs-symbols/binarytext.md) |
| Category · 分類 | Markup, Docs & Symbols · 標記文件與符號 |
| Control type · 控制類型 | <code>Button</code> |
| XAML name · XAML 名稱 | <code>DecodeBtn</code> |
| Label / tooltip · 標籤／提示 | DecodeBtn |
| Handler · 處理函式 | <code>Decode_Click</code> |
| Source · 來源 | <code>Pages/BinaryTextModule.xaml</code> |

## Operator Notes · 操作備註

**EN —** Use this control from the module page shown above. If the handler is blank, the action is represented by binding or template state rather than a direct click handler in XAML.

**粵語 —** 喺上面模組頁面使用呢個控制項。如果處理函式係空白，代表動作可能由 binding 或樣板狀態處理，而唔係 XAML 入面直接寫 click handler。

## Native C++ counterpart · 原生 C++ 對應

**EN —** The native button has automation ID `NativeBinaryTextDecode`. It accepts the documented separators and matching prefixes, decodes locally, and clears output atomically when any code is malformed or outside 0–255 so stale text cannot survive a failed decode.

**粵語 —** 原生按鈕嘅 automation ID 係 `NativeBinaryTextDecode`。佢接受文件列出嘅分隔同配對 prefix、喺本機解碼；任何數字碼格式錯誤或者唔喺 0–255 範圍就會原子式清空輸出，唔會喺失敗解碼後留下舊文字。
