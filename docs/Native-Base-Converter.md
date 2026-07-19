# Native Base Converter · 原生進位轉換

## Controlled native integration · 受控原生整合

`module.baseconvert` and its registered alias, `baseconvert`, are integrated in the native C++ working tree as a genuine C++/WinRT renderer over a dependency-free standard-C++ arbitrary-precision core. The slice does not host the CLR, delegate to the managed executable, or alter the C++-only release workflow; hosted release verification follows the controlled push.

`module.baseconvert` 同已登記 alias `baseconvert` 已受控整合入原生 C++ working tree，係真正 C++/WinRT renderer 加唔靠依賴嘅標準 C++ 任意精度 core。呢個 slice 冇 host CLR、冇交畀 managed executable，亦冇改只限 C++ release workflow；hosted release 驗證會跟受控 push 做。

## Behaviour parity · 行為相容

- The dependency-free standard-C++ core parses signed arbitrary-precision values in bases 2–36, accepts the managed outer-whitespace, space/underscore grouping, ASCII case-insensitive digit, and nested-sign operand contracts, and never relies on the managed executable or CLR.
- Outputs preserve grouped binary nibbles, lowercase ordinary base output, uppercase `0x`/`-0x` hexadecimal, magnitude bit length, and 64-bit signed two's-complement display only when the value fits.
- The local bitwise calculator keeps BigInteger-compatible AND, OR, XOR, NAND, NOR, left shift, and arithmetic right shift semantics, including unbounded negative two's-complement behaviour.
- The C++/WinRT page has live local conversion, explicit-only clipboard Copy, accessible labels/automation IDs, English/Cantonese/bilingual rendering, language-rerender state retention, and managed-style fresh-route reset.

- 唔靠依賴嘅標準 C++ core 會解析 2–36 進制有符號任意精度數字，保留 managed 外圍空白、space／underscore 分組、ASCII 不分大小寫數字同 nested-sign operand 合約，絕對唔靠 managed executable 或 CLR。
- 輸出保留每四位一組嘅二進制、普通小寫進制輸出、大寫 `0x`／`-0x` 十六進制、絕對值 bit length，同埋只喺數值放得入時先顯示嘅 64-bit signed two's-complement。
- 本機 bitwise 計數機保留 BigInteger 相容嘅 AND、OR、XOR、NAND、NOR、左移同 arithmetic 右移，包括無上限負數二補數行為。
- C++/WinRT 頁提供即時本機轉換、只限明確 Copy 嘅剪貼簿、accessible label／automation ID、英語／粵語／雙語顯示、轉語言時保留狀態，同 managed 一樣新 route 重設。

## Safety and failure modes · 安全同失敗模式

All calculations are in memory and local to the native process. Invalid input clears stale conversion output and reports a local diagnostic; allocation failures fail closed. Clipboard contents can change only through an explicit Copy button; the route starts no process, makes no network request, and changes no operating-system setting.

全部計算都只喺原生 process 入面嘅記憶體做。錯誤輸入會清走舊轉換輸出，再顯示本機診斷；allocation 失敗會 fail closed。剪貼簿只會由明確 Copy 按鈕改動；呢條 route 唔會開 process、唔會發網絡 request、亦唔會改作業系統設定。

## Verification checkpoint · 驗證檢查點

- Native Debug and Release x64 solution builds: exit 0, 0 errors.
- Debug and Release native core suites: **857/857** each, including **15/15** Base Converter contracts and managed Unicode `Trim()` parity for invalid-input diagnostics.
- Focused `Invoke-NativeShellSmoke.ps1 -BaseConvertRoutesOnly -AllowClipboardMutation`: **14/14** across `baseconvert` and `module.baseconvert`, covering conversion, arbitrary precision, bitwise operations, explicit Copy, localization, lifecycle, clipping, and route reset.
- Catalog parity: **346 fixed routes + 5 dynamic families**, 319 registry entries, and 346 ledger rows; the native installer contract passes. Renderer accounting is **36/346** fixed routes (**36 `in-progress` / 310 `not-started`**).
- A full aggregate shell result is not claimed for this combined checkpoint; the route-specific **14/14** focused UIA result is the behavioral UI gate.

- 原生 Debug 同 Release x64 solution build：exit 0、0 errors。
- Debug 同 Release 原生 core suite：各 **857/857**，包括 **15/15** Base Converter 合約同無效輸入診斷嘅 managed Unicode `Trim()` 對等。
- 專項 `Invoke-NativeShellSmoke.ps1 -BaseConvertRoutesOnly -AllowClipboardMutation`：`baseconvert` 同 `module.baseconvert` 合共 **14/14**，涵蓋轉換、任意精度、bitwise、明確 Copy、本地化、lifecycle、裁切同 route 重設。
- catalog parity：**346 固定 route + 5 動態家族**、319 個 registry entry 同 346 條 ledger row；native installer contract 通過。renderer 計數係 **36/346** 固定 route（**36 `in-progress` / 310 `not-started`**）。
- 呢個合併 checkpoint 唔會聲稱完整 aggregate shell 結果；route 專項 **14/14** UIA 係行為 UI gate。

## Visual evidence · 視覺證據

The repository-local LowLevel Computer Use MCP checkout is present at `C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp`, but this Codex session exposes no callable headless-desktop MCP tools. The fresh current `driver.ps1 -Native -Page baseconvert -WaitMs 16000` capture reported `CopyFromScreen unavailable; captured the window through PrintWindow instead`, then rejected the blank or near-uniform WinUI client frame. No PNG was created, retained, replaced, or promoted; the driver cleanup left no WinForge process. This route is honestly `capture-blocked`, not visual-pass.

repo 本機 LowLevel Computer Use MCP checkout 喺 `C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp`，但今個 Codex session 冇任何可呼叫嘅 headless-desktop MCP 工具。最新目前 required `driver.ps1 -Native -Page baseconvert -WaitMs 16000` 先報 `CopyFromScreen unavailable; captured the window through PrintWindow instead`，再拒絕空白／近乎單色嘅 WinUI client frame。冇建立、保留、替換或者提升任何 PNG，driver 清理後亦冇 WinForge process；呢條 route 如實係 `capture-blocked`，唔係 visual pass。
