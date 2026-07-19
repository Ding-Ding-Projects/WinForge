# Native Morse Code · 原生摩斯電碼

`module.morse` is a genuine C++/WinRT route backed by the standard-C++ `WinForge.Core/Morse` library. It is reachable through both registered deep links: `morse` and `module.morse`.

`module.morse` 而家係真正嘅 C++/WinRT route，由標準 C++ `WinForge.Core/Morse` 支援；`morse` 同 `module.morse` 兩個已登記 deep link 都可以直入。

## Behaviour parity · 行為相容

- Text-to-Morse supports A–Z, 0–9, and the managed punctuation catalogue: `.,?'!/()&:;=+-_"$@`.
- Text is split only on the managed ASCII word separators (space, tab, CR, LF). Empty separator inputs restore the managed `" "` letter and `" / "` word defaults; the three UI presets remain space/slash, space/triple-space, and double-space/slash.
- The encoder uppercases each UTF-16 code unit with the shared managed-compatible invariant mapping. Unsupported units emit `#` and appear once, in encounter order, in the warning list; surrogate halves intentionally retain the managed per-unit behaviour.
- Decoding accepts `.`, middle dot, and bullet as dots; hyphen, en dash, em dash, and underscore as dashes; and `|` as a word slash. `#` remains `#`; unknown tokens become U+FFFD. Its trimming and internal-newline behaviour deliberately match `MorseService`.
- The flash preview is local-only: dot/dash/intra-letter/letter/word timing uses 1/3/1/3/7 units and `1200 / WPM`, clamped to 1–60 WPM. The dispatcher timer stops on completion, Stop, route reset, and window close.

- 文字轉摩斯支援 A–Z、0–9 同 managed 標點目錄 `.,?'!/()&:;=+-_"$@`。
- 文字只會按 managed ASCII 字詞分隔符（space、tab、CR、LF）拆開；空白 separator 會還原 managed `" "` 字母同 `" / "` 字詞預設，而三個 UI preset 仍然係空格／slash、空格／三個空格、雙空格／slash。
- encoder 用共用、相容 managed 嘅 invariant mapping 逐個 UTF-16 unit 轉大寫；唔支援 unit 會顯示 `#`，並按第一次出現順序只提示一次。surrogate half 特登保留 managed 逐 unit 行為。
- decoder 接受 `.／·／•` 做點、`-／–／—／_` 做劃、`|` 做字詞 slash；`#` 保留，而未知 token 會變成 U+FFFD。trim 同內部換行嘅細節都跟 `MorseService`。
- 閃燈 preview 完全本機做：點／劃／字母內／字母／字詞 timing 係 1/3/1/3/7 units，`1200 / WPM` 會 clamp 到 1–60 WPM。dispatcher timer 完成、Stop、離開 route 同關窗都會停。

## Safety and failure modes · 安全同失敗模式

All conversion happens in memory. The route makes no network, process, file-system, or operating-system changes. Clipboard access is opt-in through **Copy** only; an empty output reports a status without touching the clipboard. Allocation or UI failures fail closed to an empty/diagnostic result rather than escaping an exception. Route reset releases controls, timeline state, and timer references before another renderer is shown.

所有轉換都只喺記憶體入面做，唔會改網絡、程序、檔案系統或者作業系統。剪貼簿一定要明確撳 **Copy** 先會用；冇輸出時只會顯示狀態，唔會掂剪貼簿。分配或者 UI 出錯時會 fail closed，顯示空白／診斷結果而唔會拋例外；離開 route 前會釋放 controls、timeline 同 timer reference。

## Verification · 驗證

On the Morse port branch, native Debug and Release core executables each passed **741/741**, including **24/24** focused Morse tests. The focused `Invoke-NativeShellSmoke.ps1 -MorseRoutesOnly` run passed **13/13** across both aliases, covering live SOS encoding, separator presets, alias decoding, unsupported UTF-16 reporting, accessible controls, horizontal bounds, launch, and natural flash completion.

Morse port branch 上，native Debug 同 Release core 各自通過 **741/741**，包括 **24/24** 個專項 Morse 測試；`Invoke-NativeShellSmoke.ps1 -MorseRoutesOnly` 兩個 alias 合共通過 **13/13**，覆蓋 SOS 即時編碼、separator preset、alias 解碼、唔支援 UTF-16 提示、accessibility control、水平界限、launch 同自然完成嘅閃燈。

## Visual evidence · 視覺證據

The repository-local LowLevel MCP checkout exists, but this Codex session did not expose its headless-desktop tools. The required native driver launched `morse` successfully with `-NoCapture`; its capture fallback rejected the resulting blank/near-uniform WinUI client when `CopyFromScreen` was unavailable. No image was retained or promoted, so visual evidence remains `capture-blocked` rather than using a stale or synthetic screenshot.

repo 本機 LowLevel MCP checkout 存在，但今個 Codex session 冇暴露 headless-desktop 工具。required native driver 用 `-NoCapture` 成功開到 `morse`；當 `CopyFromScreen` 唔可用時，capture fallback 拒絕咗空白／近乎單色嘅 WinUI client。冇保留或者升格任何圖，所以 visual 如實維持 `capture-blocked`，唔會用舊圖或者假圖。
