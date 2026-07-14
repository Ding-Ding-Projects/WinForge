# EncodeBtn · Button

**EN —** Action/control documented from the WinUI XAML source for **Text to Binary**.
**粵語 —** 呢個動作／控制項係由 **文字轉二進位** 嘅 WinUI XAML 來源整理出嚟。

| Field · 欄位 | Value · 值 |
|---|---|
| Module · 模組 | [Text to Binary · 文字轉二進位](../../../features/markup-docs-symbols/binarytext.md) |
| Category · 分類 | Markup, Docs & Symbols · 標記文件與符號 |
| Control type · 控制類型 | <code>Button</code> |
| XAML name · XAML 名稱 | <code>EncodeBtn</code> |
| Label / tooltip · 標籤／提示 | EncodeBtn |
| Handler · 處理函式 | <code>Encode_Click</code> |
| Source · 來源 | <code>Pages/BinaryTextModule.xaml</code> |

## Operator Notes · 操作備註

**EN —** Use this control from the module page shown above. If the handler is blank, the action is represented by binding or template state rather than a direct click handler in XAML.

**粵語 —** 喺上面模組頁面使用呢個控制項。如果處理函式係空白，代表動作可能由 binding 或樣板狀態處理，而唔係 XAML 入面直接寫 click handler。

## Native C++ counterpart · 原生 C++ 對應

**EN —** The native button has automation ID `NativeBinaryTextEncode`. It converts the current input into selected-base UTF-8 byte codes (binary, decimal, octal, or uppercase hexadecimal) locally in standard C++; it neither launches a process nor writes system state.

**粵語 —** 原生按鈕嘅 automation ID 係 `NativeBinaryTextEncode`。佢會喺本機標準 C++ 將目前輸入轉成已揀進位嘅 UTF-8 位元組碼（二進位、十進位、八進位或者大楷十六進位）；唔會開 process，亦唔會寫系統狀態。
