# CopyBtn · Button

**EN —** Action/control documented from the WinUI XAML source for **Text to Binary**.
**粵語 —** 呢個動作／控制項係由 **文字轉二進位** 嘅 WinUI XAML 來源整理出嚟。

| Field · 欄位 | Value · 值 |
|---|---|
| Module · 模組 | [Text to Binary · 文字轉二進位](../../../features/markup-docs-symbols/binarytext.md) |
| Category · 分類 | Markup, Docs & Symbols · 標記文件與符號 |
| Control type · 控制類型 | <code>Button</code> |
| XAML name · XAML 名稱 | <code>CopyBtn</code> |
| Label / tooltip · 標籤／提示 | CopyBtn |
| Handler · 處理函式 | <code>Copy_Click</code> |
| Source · 來源 | <code>Pages/BinaryTextModule.xaml</code> |

## Operator Notes · 操作備註

**EN —** Use this control from the module page shown above. If the handler is blank, the action is represented by binding or template state rather than a direct click handler in XAML.

**粵語 —** 喺上面模組頁面使用呢個控制項。如果處理函式係空白，代表動作可能由 binding 或樣板狀態處理，而唔係 XAML 入面直接寫 click handler。

## Native C++ counterpart · 原生 C++ 對應

**EN —** The native button has automation ID `NativeBinaryTextCopy`. Clipboard access is opt-in: it runs only after the operator presses this button, reports an empty-output or clipboard failure through the polite caution-styled status region, and has no process, network, file, registry, or elevation path. The process-owned native UI smoke invokes this control after a decoded output and verifies the success status; it never treats a rendered screenshot as proof of the action.

**粵語 —** 原生按鈕嘅 automation ID 係 `NativeBinaryTextCopy`。剪貼簿存取係 opt-in：只會喺操作員撳呢個掣後先執行；空輸出或者剪貼簿失敗會經有 caution 樣式嘅 polite status region 報告；完全冇 process、網絡、檔案、registry 或提升權限路徑。自有 process 原生 UI smoke 會喺解碼輸出之後操作呢個 control 同驗證成功狀態；唔會將 render 截圖當成動作證明。
