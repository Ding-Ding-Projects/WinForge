# Native ASCII Table · 原生 ASCII 表

## Controlled integration · 受控整合

ascii, asciitable, and module.asciitable now resolve to a genuine C++/WinRT page backed by the dependency-free standard-C++ AsciiTable core. It does not host the CLR, launch the managed app, or change the C++-only release workflow.

ascii、asciitable 同 module.asciitable 而家會開真正 C++/WinRT 頁面，由唔靠依賴嘅標準 C++ AsciiTable core 支援。佢唔會 host CLR、唔會開 managed app，亦冇改只限 C++ 嘅 release workflow。

## Behavior and configuration · 行為同設定

- The default local reference is inclusive code points 0–127; the explicit Latin-1 checkbox extends it through 255. Rows expose decimal, hexadecimal, octal, eight-bit binary, glyph, and localized description fields.
- C0 mnemonics, space, DEL, C1 controls, and NBSP preserve the managed distinctions. Invariant local search covers every displayed field, including Latin-1 casing edges.
- The virtualized list resets on a fresh route entry, retains filter/range state across language rerendering, and supports English, playful Cantonese, and bilingual labels.

- 預設本機參考表係 0–127（包括兩端）；只有明確剔 Latin-1 先擴到 255。每行有十進、十六進、八進、八位元二進、字元同本地化說明。
- C0 縮寫、空格、DEL、C1 控制碼同 NBSP 都保留 managed 分別。invariant 本機搜尋會查全部欄位，包括 Latin-1 大小寫邊界。
- 虛擬化清單喺 fresh route entry 重設，轉語言時保留 filter／range 狀態，並支援英文、玩味粵語同雙語標籤。

## Safety and failure modes · 安全同失敗模式

Selecting a row never writes to the clipboard. Only Copy selected character writes the raw code unit, and unavailable clipboard access produces a localized warning. Core construction/filtering failures clear local presentation rather than throwing or touching external state.

揀一行永遠唔會寫剪貼簿；只有 Copy selected character 會寫入原始 code unit，而剪貼簿不可用會有本地化警告。core 建表／篩選失敗會清本機顯示，唔會拋出錯誤或改外部狀態。

## Verification · 驗證

- Debug and Release x64 native solution builds: 0 errors.
- Debug and Release core suites: 878/878, including ASCII Table 21/21.
- Focused UI Automation: -AsciiTableRoutesOnly -AllowClipboardMutation, 16/16.
- Catalog parity: 346 fixed routes + five dynamic families, 319 registry entries, 346 ledger rows; native installer contract passes.
- A broader full-shell invocation was stopped after it stalled on the pre-existing wordfreq launch; it is not claimed as a passing aggregate result.

- Debug／Release x64 native solution build：0 errors。
- Debug／Release core suite：878/878，包括 ASCII Table 21/21。
- 專項 UI Automation：-AsciiTableRoutesOnly -AllowClipboardMutation，16/16。
- catalog parity：346 條固定 route + 五組動態家族、319 條 registry、346 條 ledger；native installer contract 通過。
- 較廣嘅 full-shell invocation 喺既有 wordfreq launch 卡住後已停止；唔會聲稱佢係 aggregate pass。

## Visual evidence · 視覺證據

The repository-local LowLevel Computer Use MCP checkout exists at C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp, but no callable headless-desktop tool is exposed in this Codex session. The fresh driver.ps1 -Native -Page asciitable -WaitMs 16000 attempt reported CopyFromScreen unavailable and rejected its blank/near-uniform PrintWindow fallback. No PNG was created or retained, and no WinForge process remained. Visual status is honestly capture-blocked, not visual-pass.

repository 本機 LowLevel Computer Use MCP checkout 喺 C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp，但今個 Codex session 冇可呼叫 headless-desktop 工具。最新 driver.ps1 -Native -Page asciitable -WaitMs 16000 報 CopyFromScreen 不可用，並拒絕空白／近乎單色 PrintWindow fallback。冇建立或保留 PNG，亦冇殘留 WinForge process；視覺狀態如實係 capture-blocked，唔係 visual-pass。
