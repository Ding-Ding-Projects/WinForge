# Native Unit Price · 原生單位價格

`module.unitprice` is a genuine C++/WinRT route backed by the pure standard-C++ `WinForge.Core/UnitPrice` library. The registered deep links `priceper`, `unitprice`, and `module.unitprice` all resolve to the native page; it never launches, hosts, or delegates to the managed app.

`module.unitprice` 而家係真正嘅 C++/WinRT route，由純標準 C++ `WinForge.Core/UnitPrice` library 支援。`priceper`、`unitprice` 同 `module.unitprice` 三個已登記 deep link 都會開原生頁；唔會啟動、寄宿或者交畀 managed app 做。

## Behaviour parity · 行為相容

- A comparison row has an optional label, price, quantity, and unit. The native core ignores non-finite or non-positive price/quantity values when deciding the best purchase, matching the managed validation path.
- A zero-price valid item is a free best value and reports the managed infinity percentage for non-free competitors. Equal per-unit prices use the managed tolerance and receive tie treatment rather than a misleading winner.
- Rows update locally as values change. **Add item** copies the first row's unit; removing a row releases its controls and leaves no stale accessible element. A fresh route visit starts with the managed `$` currency and two blank rows.
- English, Cantonese, and bilingual rerenders preserve the current comparison. Leaving the route releases its observable controls; reopening it resets managed defaults.

- 每一行都有可選名稱、價格、數量同單位。原生 core 喺揀最抵時會略過非有限、零或者負數價格／數量，跟 managed 驗證路徑一致。
- 有效嘅零價項目係免費最抵；其他項目會顯示 managed 相容嘅 infinity 百分比。每單位價格相同會用 managed 容差當平手，唔會亂揀贏家。
- 改數值會即時喺本機重算。**Add item** 會複製第一行單位；刪行會釋放 control，唔留舊 accessibility element。每次全新入 route 都係 managed `$` 貨幣同兩行空白預設。
- 英文、粵語同雙語重畫會保留而家比較；離開 route 會釋放 observable control，重新開就還原 managed 預設。

## Safety and failure modes · 安全同失敗模式

All arithmetic and formatting are in memory. The route has no network, process, file-system, registry, elevation, persistence, or secret path. Clipboard mutation is opt-in: **Copy comparison** is the only clipboard write, and an empty/invalid comparison reports status without modifying it. Invalid quantities are shown as invalid rather than being silently used in a best-value decision.

全部運算同格式化只喺記憶體做。呢條 route 冇網絡、程序、檔案系統、registry、提升權限、持久化或者 secret 路徑。剪貼簿改動係 opt-in：只有 **Copy comparison** 會寫入；空白／無效比較只顯示狀態，唔會改剪貼簿。無效數量會清楚標示，唔會暗中用嚟揀最抵。

## Verification · 驗證

The controlled integration has Debug and Release x64 native solution builds with 0 errors. Both combined core suites pass **828/828**, including **13/13 Unit Price** contracts; focused `Invoke-NativeShellSmoke.ps1 -UnitPriceRoutesOnly -AllowClipboardMutation` passes **15/15** across all aliases; Utility UIA passes **39/39** including CSS Unit Converter; catalog parity remains **346 fixed routes + five dynamic families** with 319 registry records and 346 ledger rows; and the installer contract passes. A broad aggregate exercised the Unit Price assertions but did not return a captured final footer, so this record does not claim a completed full-shell result.

受控整合嘅 Debug／Release x64 native solution build 都係 0 errors。合併後 core 各 **828/828**，包括 **13/13 Unit Price** contract；專項 `Invoke-NativeShellSmoke.ps1 -UnitPriceRoutesOnly -AllowClipboardMutation` 喺三個 alias 合共 **15/15**；包括 CSS Unit Converter 嘅 Utility UIA **39/39**；catalog parity 仍然係 **346 fixed routes + five dynamic families**、319 registry 同 346 ledger；installer contract 亦通過。廣泛 aggregate 跑過 Unit Price assertion，但冇最後 footer，所以唔聲稱係完成嘅 full-shell 結果。

## Visual evidence · 視覺證據

The local LowLevel Computer Use MCP checkout exists, but this Codex session exposes no callable headless-desktop tools. The required native driver attempted `unitprice`; `CopyFromScreen` was unavailable and its `PrintWindow` fallback was blank or near-uniform, so it was rejected. No PNG was retained, promoted, or used in place of a valid native screenshot. Visual evidence is honestly `capture-blocked`.

本機 LowLevel Computer Use MCP checkout 存在，但今個 Codex session 冇可呼叫嘅 headless-desktop tool。required native driver 試過 `unitprice`；`CopyFromScreen` 唔可用，而 `PrintWindow` fallback 係空白／近乎單色，已拒絕。冇保留、升格或者用任何 PNG 頂替有效原生截圖；visual 如實係 `capture-blocked`。
