# Screenshots · 截圖集

Canonical screenshots live in `docs/` and are embedded here through raw GitHub URLs. Entries are captured — and cropped, highlighted, annotated, and redacted — with [`winforge-shot`](https://github.com/codingmachineedge/WinForge/tree/main/tools/WinForgeShot). See the [Wiki Screenshot Workflow](Wiki-Screenshot-Workflow.md) for the full recipe.

正式截圖放喺 `docs/`，呢度用 raw GitHub URL 嵌入。截圖由 [`winforge-shot`](https://github.com/codingmachineedge/WinForge/tree/main/tools/WinForgeShot) 擷取，並裁切、加強調、標註同遮蔽。完整做法見 [Wiki 截圖工作流程](Wiki-Screenshot-Workflow.md)。

## Current Capture Status · 目前擷取狀態

**EN — Native Namespaced UUID capture is blocked (2026-07-19):** The repo-local LowLevel Computer Use MCP checkout is present, but no LowLevel tool is callable in this Codex session. The required `driver.ps1 -Native -Page uuid5 -WaitMs 16000` retry reported `CopyFromScreen unavailable; captured the window through PrintWindow instead`, then rejected the blank/near-uniform fallback. No PNG was created or retained and no root-worktree WinForge process remained. The native UUID page is honestly `capture-blocked`, not `visual-pass`; its Debug/Release **815/815** core, focused UIA **21/21**, and complete shell **469/469** are behavioral/accessibility evidence only.

**粵語 — 原生具名空間 UUID 擷取受阻（2026-07-19）：** repo 本機 LowLevel Computer Use MCP checkout 喺度，但目前 Codex session 冇可呼叫工具。必需嘅 `driver.ps1 -Native -Page uuid5 -WaitMs 16000` 重試先報 `CopyFromScreen unavailable; captured the window through PrintWindow instead`，再拒絕空白／近乎單色 fallback。冇建立或保留 PNG、冇殘留 root worktree WinForge process。原生 UUID 如實係 `capture-blocked`，唔係 `visual-pass`；Debug／Release core **815/815**、專項 UIA **21/21** 同完整 shell **469/469** 只係行為／無障礙證據。

**EN — Native Slugify capture is blocked (2026-07-19):** The repo-local LowLevel Computer Use MCP checkout is present, but no LowLevel tool is callable in this Codex session, so no LowLevel evidence is claimed. The required `driver.ps1 -Native -Page slugify -WaitMs 16000` attempt reported `CopyFromScreen unavailable; captured the window through PrintWindow instead`, then rejected the fallback with `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` No PNG was created, no worktree WinForge process remained, and no canonical or wiki image was created or replaced. The native Slugify page is honestly `capture-blocked`, not `visual-pass`; its integrated Debug/Release **801/801** core, Slugify **18/18**, focused UIA **12/12**, and full shell **441/441** results are behavioral/accessibility evidence only.

**粵語 — 原生網址別名擷取受阻（2026-07-19）：** repo 本機 LowLevel Computer Use MCP checkout 喺度，但目前 Codex session 冇可呼叫工具，所以唔會聲稱有 LowLevel 證據。必需嘅 `driver.ps1 -Native -Page slugify -WaitMs 16000` 先報 `CopyFromScreen unavailable; captured the window through PrintWindow instead`，再因空白／近乎單色 fallback 拒絕畫面。冇建立 PNG、冇 worktree WinForge process、冇建立或替換 canonical／wiki 圖。原生網址別名如實係 `capture-blocked`，唔係 `visual-pass`；合併後 Debug／Release **801/801** core、Slugify **18/18**、專項 UIA **12/12** 同完整 shell **441/441** 只係行為／無障礙證據。

**EN — Text-analysis capture is blocked (2026-07-18):** LowLevel Computer Use MCP 1.28.1 created private desktops `winforge-textstats-capture`, `winforge-wordfreq-capture`, and `winforge-stringcompare-capture`; launched canonical routes `textstats`, `wordfreq`, and `similarity`; and resolved PIDs **24016**, **20180**, and **26228** with `WinUIDesktopWin32WindowClass` HWNDs **49022214**, **49087750**, and **49153286**. Each native window was **1980×1320**. Both the full-window 1980×1320 and client-only **1958×1264** screenshots returned `rendered_ok=true`, but original visual inspection showed only the frame with white content and pixel audit found every client was exactly one color, RGB `(255,255,255)`. The repository driver then independently ran all three pages with `-Native -WaitMs 16000`; every attempt exited 1 with the exact blocker: `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` All six invalid MCP PNGs were removed, the driver retained no invalid image, each exact PID was passed to `kill_process(force=true)`, each named desktop was passed to `close_headless_desktop`, all six cleanup calls succeeded without error objects, subsequent process checks found no WinForge process, and no canonical/wiki screenshot was created or replaced. Text Statistics, Word Frequency, and String Compare are therefore honestly `capture-blocked`, not `visual-pass`. The **714/714** core, **84/84** Text Analysis, **7/7** managed tests, **40/40** focused UIA, **39/39** utility, and **388/388 (388 passed, 0 failed)** exhaustive-shell results remain behavioral/accessibility evidence only.

**粵語 — 文字分析擷取受阻（2026-07-18）：** LowLevel Computer Use MCP 1.28.1 喺三個獨立 private desktop 開啟 `textstats`、`wordfreq` 同 `similarity`，解析到 PID **24016**／**20180**／**26228**、HWND **49022214**／**49087750**／**49153286** 同三個 **1980×1320** 原生視窗。完整視窗同 **1958×1264** client 擷取雖然都回報 `rendered_ok=true`，但原圖檢查只見邊框同白色內容，pixel audit 證實每張 client 只有 RGB `(255,255,255)` 一種顏色。Repository driver 亦分別用 `-Native -WaitMs 16000` 試過三頁，每次都 exit 1 並報同一準確空白／近乎單色 fallback 錯誤。六張無效 MCP PNG 已刪、driver 冇留無效圖；三個 PID 都已強制終止、三個命名 desktop 都已關閉，六個 cleanup call 無錯，之後亦確認冇 WinForge process。冇建立或替換 canonical／wiki 截圖，所以三頁如實係 `capture-blocked`，不係 `visual-pass`。

**EN — Post-c6 Word Frequency retry (2026-07-19):** The repo-local LowLevel Computer Use MCP checkout was present, but its tools were not callable in the active Codex session, so no LowLevel evidence is claimed. The fresh native `wordfreq` repository-driver retry reached its fallback and failed with: `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` No PNG was retained, created, reused, or substituted. Word Frequency remains `capture-blocked`, not `visual-pass`.

**粵語 — c6 後詞頻統計重試（2026-07-19）：** repo 本機 LowLevel Computer Use MCP checkout 喺度，但目前 Codex session 冇可呼叫嘅工具，所以唔會聲稱有 LowLevel 證據。最新原生 `wordfreq` repository-driver 重試去到 fallback 後，以以下錯誤失敗：`CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` 冇保留、建立、重用或者替代 PNG。詞頻統計仍然係 `capture-blocked`，唔係 `visual-pass`。

> The line-processing capture record immediately below is earlier evidence. · 下面行文字處理嘅擷取紀錄係較早證據。

**EN —** On 2026-07-18, LowLevel Computer Use MCP 1.28.1 launched native Line Tools, Line Sort & Dedupe, and Text Wrap through the exact canonical commands `--page lines`, `--page textsort`, and `--page textwrap` on three separate private desktops. The first pass resolved one 852×880 `WinUIDesktopWin32WindowClass` window for each launch (PIDs 29740, 19176, and 25196). All six full/client `PrintWindow` PNGs reported `rendered_ok`, but independent inspection found composition-white invalid frames; every invalid PNG was deleted. A compositor-backed retry on the same persistent MCP process started fresh PIDs 21696, 23644, and 22048, but `show_headless_desktop` failed for each page with Win32 error 5, Access denied, before monitor capture. Each exact PID was killed and each desktop closed; Text Sort's brief 0×0 ghost disappeared during cleanup. The required run-winforge driver then attempted all three routes separately with `-Native -WaitMs 16000`; `CopyFromScreen` was unavailable, every blank or near-uniform `PrintWindow` fallback was rejected, and each command exited 1. The artifact directory is empty, no invalid image remains, and no canonical or wiki screenshot was created or replaced. The 70/70 focused core, 630/630 aggregate core, 42/42 focused UI Automation, and 348/348 full owned-shell results are behavioral/accessibility evidence only; all three routes are honestly `capture-blocked`.

**粵語 —** 2026-07-18，LowLevel Computer Use MCP 1.28.1 用準確 canonical command `--page lines`、`--page textsort` 同 `--page textwrap`，喺三個獨立 private desktop 開啟原生行工具、行排序同去重、文字換行。第一輪每次 launch 都解析到一個 852×880 `WinUIDesktopWin32WindowClass` 視窗（PID 29740、19176、25196）。六張 full／client `PrintWindow` PNG 雖然全部回報 `rendered_ok`，但獨立檢查後都係 composition-white 無效畫面；所有無效 PNG 已刪除。同一個 persistent MCP process 再用新 PID 21696、23644、22048 做 compositor-backed retry，但三頁都喺 monitor capture 之前因 Win32 錯誤 5 Access denied 未能 `show_headless_desktop`。每個指定 PID 已終止、desktop 已關閉；Text Sort 短暫出現嘅 0×0 ghost 亦喺清理期間消失。必需 run-winforge driver 之後用 `-Native -WaitMs 16000` 分別試過三條 route；`CopyFromScreen` 唔可用，每個空白／近乎單色 `PrintWindow` fallback 都被拒，三個 command 都 exit 1。Artifact directory 已清空、冇保留無效圖、冇建立或替換 canonical／wiki 截圖。Line Processing 70/70、總 core 630/630、專項 UI Automation 42/42 同完整自有 shell 348/348 只係行為／無障礙證據；三條 route 如實係 `capture-blocked`。

**EN —** On 2026-07-18, LowLevel Computer Use MCP 1.28.1 launched native `textdiff`, `aspect`, and `cssunits` from one immutable 294-file runtime snapshot on three separately named off-screen desktops. It confirmed launch PIDs 32596, 4360, and 28088, resolved exactly one `WinUIDesktopWin32WindowClass` frame at 1320×880 for each route, and returned both full-window and 1304×841 client-only captures. Independent pixel inspection found every client frame uniformly white: one color, zero standard deviation, and a zero non-white fraction. The repository driver separately launched all three pages at `-WaitMs 16000`; `CopyFromScreen` was unavailable and it rejected each blank/near-uniform `PrintWindow` fallback. All exact PIDs and desktops were closed, the immutable stage and six invalid PNGs were discarded, and no background input was sent to an unrendered client. No canonical or wiki image was created or replaced; Text Diff, Aspect Ratio, and CSS Unit Converter are honestly `capture-blocked`, while 560/560 Debug/Release core, 39/39 focused UI Automation, and 300/300 full owned-shell results remain behavioral/accessibility evidence only.

**粵語 —** 2026-07-18，LowLevel Computer Use MCP 1.28.1 用同一份不可變、294 個檔案嘅 native runtime snapshot，喺三個分開命名嘅無頭 desktop 開啟 `textdiff`、`aspect` 同 `cssunits`。佢確認 launch PID 32596、4360 同 28088，每條 route 都只解析到一個 1320×880 `WinUIDesktopWin32WindowClass` frame，並產生完整視窗同 1304×841 client-only 擷取。獨立像素檢查發現每張 client frame 都係一致白色：只有一種顏色、標準差係零、非白色比例亦係零。Repository driver 亦用 `-WaitMs 16000` 分別開過三頁；`CopyFromScreen` 唔可用，而且每個空白／近乎單色 `PrintWindow` fallback 都被拒絕。所有指定 PID 同 desktop 已關閉，不可變 stage 同六張無效 PNG 已丟棄，亦冇向未 render 嘅 client 發送 background input。冇建立或替換 canonical／wiki 圖；文字差異比對、長寬比計算同 CSS 單位換算如實係 `capture-blocked`，而 Debug／Release core 560/560、工具專項 UI Automation 39/39 同完整自有 shell 300/300 只係行為／無障礙證據。

**EN —** On 2026-07-18, the requested repo-local LowLevel MCP launched `module.unixperm` from an immutable 294-file native runtime snapshot on a uniquely named isolated desktop and resolved the exact owned WinUI window (PID 10344, class `WinUIDesktopWin32WindowClass`, 1320×880). `PrintWindow` returned `rendered_ok`, but independent inspection found an entirely white client: 22,657 sampled pixels, one color, zero standard deviation, and a zero non-white fraction. The already-high-integrity server then failed `SwitchDesktop` with Win32 error 5, so XAML composition and UI Automation remained dormant. The visible desktop was never touched, no input was sent, and the rejected PNG remains only in the smoke artifacts. No canonical or wiki screenshot was created or replaced; chmod Calculator is honestly `capture-blocked`.

**粵語 —** 2026-07-18，指定嘅 repo 本機 LowLevel MCP 用一份不可變、294 個檔案嘅原生 runtime snapshot，喺獨立命名隔離 desktop 開啟 `module.unixperm`，並準確解析自有 WinUI 視窗（PID 10344、class `WinUIDesktopWin32WindowClass`、1320×880）。`PrintWindow` 雖然回報 `rendered_ok`，但獨立檢查發現 client 完全白色：抽樣 22,657 個像素、只有一種顏色、標準差係零、非白色比例亦係零。已經係 High integrity 嘅 server 再以 Win32 錯誤 5 拒絕 `SwitchDesktop`，所以 XAML composition 同 UI Automation 保持休眠。可見 desktop 從未被觸碰，亦冇送出輸入；拒絕咗嘅 PNG 只留喺 smoke artifacts。冇建立或替換 canonical／wiki 截圖；chmod 計算機如實係 `capture-blocked`。

**EN ·** On 2026-07-16, native Symbols Palette passed the owned LowLevel MCP 238/238 UI Automation campaign. The isolated driver could not use CopyFromScreen and rejected its blank/near-uniform PrintWindow fallback; no PNG was retained. The route is honestly capture-blocked.

**粵語 ·** 2026-07-16，native Symbols Palette 通過 LowLevel MCP 238/238 UI Automation；CopyFromScreen 唔可用，PrintWindow fallback 空白／近乎單色所以被拒絕，冇保留 PNG。呢個 route 正確係 capture-blocked。

**EN —** On 2026-07-16, the changed native `regextester` page was launched only through an isolated LowLevel MCP headless desktop after the **226/226** native UI Automation campaign. The resolved 852×880 full-window capture and independently inspected 836×841 client-only capture were both blank. Both temporary PNGs were discarded. The **403/403** Debug/Release tests and **226/226** UI Automation checks are behavioral/accessibility evidence only; no stale, synthetic, blank, or managed screenshot was reused. Native Regex Tester is honestly `capture-blocked`.

**粵語 —** 2026-07-16，改過嘅原生 `regextester` 頁面只會喺隔離 LowLevel MCP 無頭 desktop 開啟，而且係 **226/226** 原生 UI Automation campaign 後。已解析嘅 852×880 full-window 擷取同獨立檢查嘅 836×841 client-only 擷取都係空白。兩個暫存 PNG 都已丟棄。**403/403** Debug／Release 測試同 **226/226** UI Automation 檢查只係行為／無障礙證據；冇舊圖、合成圖、空白圖或受控版截圖會被重用。原生 Regex Tester 如實係 `capture-blocked`。

**EN —** On 2026-07-16, the native `regexcheat` page was launched only through the isolated LowLevel MCP headless desktop after the **224/224** native UI Automation sweep. The resolved 852×880 full-window frame showed a title bar with a blank client surface; the independently inspected 836×841 client-only frame was blank as well. Both temporary PNGs were discarded. The 395/395 Debug/Release tests and 224/224 UI Automation checks are behavioral/accessibility evidence only; no stale, synthetic, blank, or managed screenshot was reused. Native Regex Cheatsheet is honestly `capture-blocked`.

**粵語 —** 2026-07-16，原生 `regexcheat` 頁面只會喺隔離 LowLevel MCP 無頭 desktop 開啟，喺 **224/224** 原生 UI Automation sweep 後先擷取。已解析嘅 852×880 full-window frame 有 title bar 但 client surface 空白；另外檢查嘅 836×841 client-only frame 亦係空白。兩個暫存 PNG 都已丟棄。395/395 Debug／Release 測試同 224/224 UI Automation 檢查只係行為／無障礙證據；冇舊圖、合成圖、空白圖或受控版截圖會被重用。原生 Regex Cheatsheet 如實係 `capture-blocked`。

**EN —** On 2026-07-16, the changed native `package-setup` route was launched only on an isolated LowLevel MCP desktop after the **216/216** UI Automation sweep. The resolved 852×880 WinUI window returned `rendered_ok`, but its inspected full-window capture had only the title bar and a blank client area; the inspected 836×841 client-only capture was also blank. Both temporary PNGs were discarded. No managed image was replaced: `docs/screenshot-packages.png` remains labelled as a managed-production reference, and native Setup is `capture-blocked`.

**粵語 —** 2026-07-16，改過嘅原生 `package-setup` route 只會喺隔離 LowLevel MCP desktop 啟動，而且係 **216/216** UI Automation sweep 之後。已解析 852×880 WinUI 視窗回報 `rendered_ok`，但檢查過嘅 full-window 擷取只得 title bar 同空白 client area；檢查過嘅 836×841 client-only 擷取亦都係空白。兩個臨時 PNG 都已丟棄。冇取代任何 managed 圖：`docs/screenshot-packages.png` 仍然標示為 managed-production reference，而 native Setup 係 `capture-blocked`。

**EN —** On 2026-07-16, the native Password Strength page was launched through the persistent LowLevel MCP headless desktop after the 224/224 native shell smoke passed. Its 852×880 full-window capture returned `rendered_ok`, but inspection found only the title bar and a blank client surface. The invalid `docs/screenshot-passwordstrength.png` was discarded; no stale, synthetic, or managed substitute is shown. Password Strength is `capture-blocked`, not visual-pass.

**粵語 —** 2026-07-16，原生 Password Strength 頁喺 224/224 原生 shell smoke 通過後經持續 LowLevel MCP 無頭 desktop 開啟。852×880 完整視窗擷取回報 `rendered_ok`，但檢查只見到 title bar 同空白 client surface。無效 `docs/screenshot-passwordstrength.png` 已經丟棄；冇展示舊、合成或者受控替代品。Password Strength 係 `capture-blocked`，唔係 visual-pass。

**EN —** On 2026-07-16, the native Password Generator page was launched through `driver.ps1 -Native -Page passgen -WaitMs 16000`. `CopyFromScreen` was unavailable and the `PrintWindow` fallback was blank or near-uniform, so the attempted image was rejected. The requested LowLevel MCP tools are not registered in the active Codex session, so no LowLevel capture is claimed. No canonical screenshot, wiki image, stale image, or managed substitute was replaced or reused; Password Generator is `capture-blocked`.

**粵語 —** 2026-07-16，原生 Password Generator 頁已用 `driver.ps1 -Native -Page passgen -WaitMs 16000` 開啟。`CopyFromScreen` 用唔到，而 `PrintWindow` fallback 係空白／接近單色，所以嘗試嘅圖片已拒絕。要求嘅 LowLevel MCP tools 未有登記喺目前 Codex session，所以唔會聲稱有 LowLevel 擷取。冇替換或者重用 canonical 截圖、wiki 圖、舊圖或者受控替代品；Password Generator 係 `capture-blocked`。

**EN —** On 2026-07-16, the changed native Dashboard, All Apps, Regex Tester, and Package Discover pages were separately launched through `driver.ps1 -Native` at `-WaitMs 16000`. `CopyFromScreen` was unavailable; every `PrintWindow` fallback was blank or near-uniform and rejected. The repo-local LowLevel MCP then created an isolated desktop, launched Regex Tester, resolved its 1980×1320 WinUI HWND, and captured an inspected full-window frame containing only the title bar and blank client surface. The invalid LowLevel PNG was discarded. No canonical screenshot, wiki image, or managed substitute was replaced or reused; all changed native regex pages are honestly `capture-blocked`.

**粵語 —** 2026-07-16，改過嘅原生 Dashboard、所有 app、Regex Tester 同 Package Discover 頁已分別用 `driver.ps1 -Native` 加 `-WaitMs 16000` 開啟。`CopyFromScreen` 用唔到；每個 `PrintWindow` fallback 都係空白／接近單色，所以已拒絕。repo 本機 LowLevel MCP 跟住建立隔離 desktop、開啟 Regex Tester、解析佢 1980×1320 嘅 WinUI HWND，並擷取一張檢查過嘅完整視窗 frame，只得 title bar 同空白 client surface。無效 LowLevel PNG 已經丟棄。冇替換、重用任何 canonical 截圖、wiki 圖或者受控替代品；全部改過嘅原生 regex 頁如實係 `capture-blocked`。

**EN —** On 2026-07-16, the native UUID v7 page was launched through `driver.ps1 -Native -Page uuidv7 -WaitMs 16000`. `CopyFromScreen` was unavailable and the `PrintWindow` fallback was blank or near-uniform, so the driver image was rejected; its `-NoCapture` launch succeeded. The requested repo-local LowLevel MCP isolated desktop separately launched UUID v7, resolved its 1980×1320 WinUI HWND, and produced inspected captures: the full frame had only the title bar and blank client surface, and the 1958×1264 client-only capture was blank. Both invalid PNGs were discarded. No canonical screenshot, wiki image, stale image, or managed substitute was replaced or reused; UUID v7 is `capture-blocked`.

**粵語 —** 2026-07-16，原生 UUID v7 頁已用 `driver.ps1 -Native -Page uuidv7 -WaitMs 16000` 開啟。`CopyFromScreen` 用唔到，而 `PrintWindow` fallback 係空白／接近單色，所以 driver 圖已拒絕；`-NoCapture` 開啟成功。按要求嘅 repo 本機 LowLevel MCP 隔離 desktop 亦獨立開啟 UUID v7、解析佢 1980×1320 WinUI HWND，並產生檢查過嘅擷取：完整 frame 只得 title bar 同空白 client surface，而 1958×1264 只限 client 擷取亦係空白。兩張無效 PNG 已經丟棄。冇替換或者重用 canonical 截圖、wiki 圖、舊圖或者受控替代品；UUID v7 係 `capture-blocked`。

**EN —** On 2026-07-16, the required native driver separately attempted `package-discover`, `package-updates`, `package-installed`, and `package-operations` at `-WaitMs 16000`. `CopyFromScreen` was unavailable and every `PrintWindow` fallback was blank or near-uniform. The repo-local LowLevel MCP then created an isolated desktop, launched all four routes, resolved their 1980×1320 WinUI HWNDs, and captured full-window plus client-only PNGs. Each inspected full-window frame contained only the title bar and blank client surface; the invalid eight LowLevel PNGs were discarded. No canonical screenshot, wiki image, or managed substitute was replaced or reused. Every changed native Package Manager view is honestly `capture-blocked`; `docs/screenshot-packages.png` remains a managed-production reference only.

**粵語 —** 2026-07-16，必需原生 driver 分別用 `-WaitMs 16000` 嘗試 `package-discover`、`package-updates`、`package-installed` 同 `package-operations`。`CopyFromScreen` 用唔到，而且每個 `PrintWindow` fallback 都係空白／接近單色。repo 本機 LowLevel MCP 跟住建立隔離 desktop、開啟四條 route、解析佢哋 1980×1320 嘅 WinUI HWND，並擷取完整視窗同只限 client PNG。每張檢查過嘅完整視窗 frame 都只得 title bar 同空白 client surface；八張無效 LowLevel PNG 已經丟棄。冇替換、重用任何 canonical 截圖、wiki 圖或者受控替代品。每個改過嘅原生 Package Manager view 如實係 `capture-blocked`；`docs/screenshot-packages.png` 仍然只係受控正式版參考。

> The Package Manager capture history immediately below is earlier evidence; the current result above is capture-blocked for every changed native Package Manager view. · 下面 Package Manager 擷取歷史係較早證據；上面目前結果係每個改過嘅原生 Package Manager view 都 capture-blocked。

**EN —** On 2026-07-15, the changed native Package Manager Bundle pages (`package-discover`, `package-installed`, and `package-bundles`) were launched separately through the repository driver at `-WaitMs 16000`. `CopyFromScreen` was unavailable; every driver's `PrintWindow` fallback was blank or near-uniform and was rejected. The repo-local LowLevel fallback then created `WinForgeBundleWorkspaceAudit`, launched all three pages, listed their native HWNDs, and captured blank client frames. No valid PNG was accepted, `docs/screenshot-packages.png` was not replaced, and that existing image remains a managed-production reference only; the native pages are `capture-blocked`.

**粵語 —** 2026-07-15 改過嘅原生 Package Manager Bundle 頁（`package-discover`、`package-installed` 同 `package-bundles`）已經分別經 repository driver 用 `-WaitMs 16000` 開啟。`CopyFromScreen` 用唔到；每個 driver 嘅 `PrintWindow` fallback 都係空白／接近單色，所以已被拒絕。repo 本機 LowLevel fallback 跟住建立 `WinForgeBundleWorkspaceAudit`、開三個頁、列出佢哋嘅原生 HWND，再擷取到空白 client frame。冇接受有效 PNG、冇替換 `docs/screenshot-packages.png`，而呢張既有圖片仍然只係受控正式版參考；原生頁面仍然係 `capture-blocked`。

**EN —** On 2026-07-13 the newly native Check Digit Validator was launched through `driver.ps1 -Native -Page checkdigit`; its required fresh capture was attempted again after the standards and accessibility hardening rendered. `CopyFromScreen` was unavailable; the `PrintWindow` fallback produced a blank or near-uniform WinUI client frame and was rejected. The route separately passed all six live UI Automation scheme checks plus localized-name and stale-detail accessibility checks, but that is behavioral—not visual—evidence. No `screenshot-checkdigit.png` was created, replaced, reused, or synthesized; the page is `capture-blocked`.

**粵語 —** 2026-07-13 新完成原生功能嘅檢查碼驗證器已經用 `driver.ps1 -Native -Page checkdigit` 開啟；標準同無障礙加固 render 後亦再次按要求嘗試新截圖。`CopyFromScreen` 用唔到；`PrintWindow` 後備產生空白／接近單色 WinUI client frame，所以已被拒絕。route 另外通過六個格式嘅即時 UI Automation 檢查、本地化名稱同清除舊 detail 無障礙檢查，但嗰啲係行為證據，唔係視覺證據。冇建立、替換、重用或者合成 `screenshot-checkdigit.png`；頁面係 `capture-blocked`。

**EN —** On 2026-07-15 the native Case Converter route was retried through `driver.ps1 -Native -Page caseconvert -WaitMs 15000` and a LowLevel headless desktop. `CopyFromScreen` was unavailable; the driver's `PrintWindow` fallback was blank or near-uniform, and the inspected LowLevel HWND capture showed only a title bar and blank client frame. No current PNG was accepted. `screenshot-caseconvert.png` and its wiki-local copy were retired rather than reused; the page is `capture-blocked`.

**粵語 —** 2026-07-15 原生 Case Converter route 重新用 `driver.ps1 -Native -Page caseconvert -WaitMs 15000` 同 LowLevel 無頭 desktop 嘗試。`CopyFromScreen` 用唔到；driver 嘅 `PrintWindow` fallback 係空白／接近單色，而檢查過嘅 LowLevel HWND 擷取只有 title bar 同空白 client frame。冇接受任何最新 PNG。`screenshot-caseconvert.png` 同 wiki 本機副本已移除，唔會重用；頁面係 `capture-blocked`。

**EN —** On 2026-07-15 the native Roman Numerals route was launched through `driver.ps1 -Native -Page romannum -WaitMs 15000`. `CopyFromScreen` was unavailable and the driver's `PrintWindow` fallback was blank or near-uniform, so no PNG was accepted. An inspected LowLevel headless-desktop HWND capture likewise showed only the WinForge title bar and a blank client frame. No `screenshot-romannum.png` was created, replaced, reused, or synthesized. The current 355/355 native tests and 148/148 UI Automation smoke still include 17 focused Roman cases and 13 Roman assertions, but those are behavior—not visual—evidence; Roman Numerals is `capture-blocked`.

**粵語 —** 2026-07-15 原生羅馬數字 route 已經用 `driver.ps1 -Native -Page romannum -WaitMs 15000` 開啟。`CopyFromScreen` 用唔到，而 driver 嘅 `PrintWindow` fallback 係空白／接近單色，所以冇接受 PNG。檢查過嘅 LowLevel 無頭 desktop HWND 擷取同樣只有 WinForge title bar 同空白 client frame。冇建立、替換、重用或者合成 `screenshot-romannum.png`。目前 355/355 原生測試同 148/148 UI Automation smoke 仍然包含 17 個羅馬數字專項案例同 13 個 Roman assertion，但嗰啲係行為而唔係視覺證據；羅馬數字係 `capture-blocked`。

**EN —** On 2026-07-13 the newly native Text to Binary route was launched through `driver.ps1 -Native -Page binarytext -WaitMs 5000`; its required current capture attempted `CopyFromScreen`, then rejected the blank or near-uniform `PrintWindow` fallback. The exact result was `CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` No `screenshot-binarytext.png` exists or was created, replaced, reused, or synthesized. The separate 59/59 UI Automation smoke exercised binary and hex conversion, Move output to input, explicit Copy, malformed-code clearing, accessibility, selected-base/input/output language-state retention, and all aliases; that is behavior evidence only. Text to Binary is `capture-blocked`, not visual-pass.

**粵語 —** 2026-07-13 新完成原生功能嘅文字轉二進位 route 已經用 `driver.ps1 -Native -Page binarytext -WaitMs 5000` 開啟；指定嘅最新截圖先試 `CopyFromScreen`，再拒絕空白／接近單色嘅 `PrintWindow` 後備。確實結果係：`CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.` 冇 `screenshot-binarytext.png`，亦冇建立、替換、重用或者合成。獨立 59/59 UI Automation smoke 會操作二進位同十六進位轉換、搬輸出去輸入、明確 Copy、錯誤碼清空、無障礙、轉語言後保留已揀進位／輸入／輸出同全部 alias；嗰啲只係行為證據。文字轉二進位係 `capture-blocked`，唔係 visual-pass。

**EN —** On 2026-07-11, a fresh self-contained Dashboard capture reproduced
`CopyFromScreen`: `The handle is invalid`. The direct `PrintWindow` fallback
returned success but its inspected 682×1311 PNG was uniformly
`ARGB #FF000000` across 3,198 samples. Windows.Graphics.Capture
`CreateForWindow` could create capture items for both WinForge and an owned
coloured diagnostic window, but neither free-threaded frame pool received a
frame within 12 seconds. Therefore this desktop session has no valid capture
fallback: no PNG was created/replaced, no stale image was substituted, and no
visual-pass result is published.

**粵語 —** 2026-07-11 嘅新 self-contained Dashboard 截圖重現咗
`CopyFromScreen`: `The handle is invalid`。直接 `PrintWindow` fallback 雖然
回傳成功，但已檢查嘅 682×1311 PNG 喺 3,198 個抽樣都係
`ARGB #FF000000`。Windows.Graphics.Capture `CreateForWindow` 雖然可以為
WinForge 同自有有色診斷視窗建立 capture item，但兩個 free-threaded frame pool
喺 12 秒內都收唔到 frame。所以呢個 desktop session 冇有效 capture fallback：
冇建立／替換 PNG、冇用舊圖頂替，亦冇發佈 visual-pass 結果。

**EN —** Batch 06 repeated the capture check against H2 Plant after its fresh
self-contained route launch. `driver.ps1 -Out` again stopped at
`CopyFromScreen`: `The handle is invalid`; a `PrintWindow` fallback attempt
then reported `ERROR: bad window rect`, while the previously successful-call
fallback output is uniformly black. No valid PNG exists for this batch, so no
canonical screenshot was replaced and no visual-pass status is claimed.

**粵語 —** Batch 06 喺新 self-contained route launch 之後，再試咗 H2 Plant
capture。`driver.ps1 -Out` 又喺 `CopyFromScreen` 報 `The handle is invalid`；
`PrintWindow` fallback 跟住報 `ERROR: bad window rect`，而之前成功 call 到嘅
fallback output 仍然係 uniform-black。呢批冇有效 PNG，所以冇換 canonical
截圖，亦唔會聲稱 visual-pass。

**EN —** The fresh self-contained Package Manager deep-link check selected Discover,
Updates, and Installed through UI Automation on 2026-07-11. Its required
`driver.ps1 -Out` screenshot attempt for `package-updates` again fell back from
`CopyFromScreen` to `PrintWindow`, then stopped because the result was a uniform
frame: `CopyFromScreen is unavailable and the PrintWindow fallback produced a uniform
frame; graphics capture is unavailable in this desktop session.` No PNG was created
or replaced, and this is not a visual-pass claim.

**粵語 —** 2026-07-11 嘅新 self-contained Package Manager 深層連結檢查用 UI
Automation 成功揀到搜尋安裝、可更新同已安裝。指定嘅 `package-updates`
`driver.ps1 -Out` 截圖再一次由 `CopyFromScreen` fallback 去 `PrintWindow`，但因為
結果係 uniform frame 而停止：`CopyFromScreen is unavailable and the PrintWindow
fallback produced a uniform frame; graphics capture is unavailable in this desktop
session.` 冇建立或者替換 PNG，亦唔係 visual-pass 聲稱。

**EN —** Batch 08 made a fresh 15-second mactools capture attempt after its
bounded launch retry had passed. CopyFromScreen was unavailable; the driver
then tried PrintWindow, detected a uniform frame, and stopped with
CopyFromScreen is unavailable and the PrintWindow fallback produced a uniform
frame; graphics capture is unavailable in this desktop session. No
mactools-default.png was saved, no canonical image was replaced or reused, and
the batch is capture-blocked, not visual-pass.

**粵語 —** Batch 08 喺受限 launch retry 通過之後，為 mactools 做咗新嘅 15 秒
capture 嘗試。CopyFromScreen 唔可用；driver 跟住試 PrintWindow、發現係 uniform
frame，再以 CopyFromScreen is unavailable and the PrintWindow fallback produced a
uniform frame; graphics capture is unavailable in this desktop session. 停止。
冇儲存 mactools-default.png、冇替換或者重用 canonical image，呢一批係
capture-blocked，唔係 visual-pass。

**EN —** Batch 07’s post-fix KeePass launch succeeded, then its fresh
15-second `driver.ps1 -Out` attempt again stopped at `CopyFromScreen`: `The
handle is invalid`. No `keepass-clipboard-safety.png` was produced, so the
existing canonical KeePass image was neither replaced nor reused as evidence;
the route has `capture-blocked`, not visual-pass, status.

**粵語 —** Batch 07 修正後嘅 KeePass launch 通過，之後新嘅 15 秒
`driver.ps1 -Out` 嘗試又喺 `CopyFromScreen` 停咗：`The handle is invalid`。
冇產生 `keepass-clipboard-safety.png`，所以既有 canonical KeePass 圖冇換、亦
冇當新證據使用；呢條 route 係 `capture-blocked`，唔係 visual-pass。
**EN —** The subsequent numeric-literal reliability audit attempted a fresh
12-second `driver.ps1 -Out` capture for each changed page: Markdown TOC, Name
Generator, Number Formatter, Scientific Notation, Subnet Calculator, and Unit
Converter. Every route reached the capture step but each `CopyFromScreen` call
returned `The handle is invalid`. No page produced a valid PNG, no stale
canonical screenshot was substituted, and these six pages are
`capture-blocked`, not visual-pass.

**粵語 —** 跟住嘅 numeric-literal reliability 審查，為每個改過頁面都用新鮮
12 秒 `driver.ps1 -Out` 試過截圖：Markdown 目錄、名稱產生器、數字格式化、科學
記數法、子網計算器同單位換算器。每條 route 都去到 capture step，但每次
`CopyFromScreen` 都回傳 `The handle is invalid`。冇一頁產生有效 PNG、冇用舊
canonical 截圖頂替，呢 6 頁係 `capture-blocked`，唔係 visual-pass。

**EN —** The Package Manager source-preservation P0 change received a fresh
`driver.ps1 -Page packages -Publish -WaitMs 15000 -Out …` attempt. The driver
reported `CopyFromScreen unavailable`; its `PrintWindow` fallback produced a
uniform frame and graphics capture was unavailable in this desktop session.
No new Package Manager PNG was produced, inspected, replaced or reused. Its
follow-up `-NoCapture` launch passed, but this evidence is `capture-blocked`,
not visual verification.

**粵語 —** Package Manager 來源保留 P0 變更已經用新嘅
`driver.ps1 -Page packages -Publish -WaitMs 15000 -Out …` 嘗試。driver 報
`CopyFromScreen unavailable`；`PrintWindow` fallback 產生 uniform frame，而
呢個 desktop session 嘅 graphics capture 亦唔可用。冇產生、檢查、替換或者重用
新嘅 Package Manager PNG。之後 `-NoCapture` launch 通過，但呢份證據係
`capture-blocked`，唔係視覺驗證。

**EN —** The Pumped-Hydro state-integrity repair is nonvisual service/code-behind work: no XAML layout or control surface changed. To avoid interfering with the active Batch 09 route sweep, no competing GUI, screenshot attempt, PNG creation/replacement, or visual-pass claim was made; screenshot replacement is not applicable.

**粵語 —** 抽水蓄能狀態完整性修正係非視覺嘅 service／code-behind 工作：冇改 XAML 排版或者控制介面。為咗唔干擾進行中嘅 Batch 09 route sweep，冇開另一個 GUI、冇試截圖、冇產生／替換 PNG，亦冇聲稱 visual-pass；唔適用截圖替換。
**EN —** Batch 09 made fresh 15-second capture attempts after the Percentage
Calculator typed-default repair, the qBittorrent lifecycle repair, and the
Pixel Editor and Proxmox safety repairs. Every changed route reached its
capture step; `CopyFromScreen` was unavailable and the `PrintWindow` fallback
produced a uniform frame, with graphics capture unavailable in this desktop
session. No PNG was created for Percentage Calculator, qBittorrent, Pixel
Editor, or Proxmox; no canonical image was replaced or reused. These are
`capture-blocked` results, never visual-pass claims.

**粵語 —** Batch 09 喺 Percentage Calculator typed-default 修正、qBittorrent
lifecycle 修正，同埋 Pixel Editor 同 Proxmox 安全修正之後，做咗新鮮 15 秒 capture
嘗試。每條改過嘅 route 都到咗 capture step；`CopyFromScreen` 唔可用，
`PrintWindow` fallback 產生 uniform frame，而呢個 desktop session 嘅 graphics
capture 亦唔可用。Percentage Calculator、qBittorrent、Pixel Editor 同 Proxmox 都冇
PNG 產生；冇 canonical image 被替換或者重用。呢啲係 `capture-blocked` 結果，
絕對唔係 visual-pass 聲稱。

**EN —** The Screen Recorder and Registry Editor reliability repair received fresh driver
attempts on 2026-07-11: `recorder` with a self-contained publish and `regedit` from that
fresh publish. Both reached the capture stage, but `CopyFromScreen` was unavailable and the
`PrintWindow` fallback produced a uniform frame; graphics capture is unavailable in this
desktop session. Launch-only follow-ups passed for both routes. No PNG was created,
inspected, replaced, or reused, so no recorder or Registry Editor canonical image is visual
evidence for this repair. These are `capture-blocked`, never visual-pass, results.

**粵語 —** 螢幕錄影同登錄編輯器可靠性修正喺 2026-07-11 收到新嘅 driver 嘗試：
`recorder` 用 self-contained publish，而 `regedit` 用嗰個新 publish。兩條都去到
capture stage，但 `CopyFromScreen` 未可用，而 `PrintWindow` fallback 產生 uniform frame；
呢個 desktop session 嘅 graphics capture 未可用。兩條 route 嘅 launch-only 後續都通過。
冇 PNG 被產生、檢查、替換或者重用，所以既有 recorder/regedit canonical image 唔係
呢個修正嘅視覺證據。呢啲係 `capture-blocked`，絕對唔係 visual-pass 結果。
**EN —** Batch 10 made fresh 15-second `driver.ps1 -Out` attempts after the
Quick Accent persistence, quicktype argument-vector, Rainmeter copy-link,
Randomizer, Screen Recorder, and Registry Editor repairs. Every changed route
reached its capture step; `CopyFromScreen` was unavailable and the
`PrintWindow` fallback produced a uniform frame while graphics capture remained
unavailable in this desktop session. No PNG was created for Quick Accent,
quicktype, Rainmeter, Randomizer, Screen Recorder, or Registry Editor; no
canonical image was replaced or reused. These are `capture-blocked` results,
never visual-pass claims.

**粵語 —** Batch 10 喺 Quick Accent persistence、quicktype argument-vector、
Rainmeter copy-link、Randomizer、Screen Recorder 同 Registry Editor 修正之後，
為每條改過 route 做咗新鮮 15 秒 `driver.ps1 -Out` 嘗試。全部都到咗 capture step；
`CopyFromScreen` 唔可用，而 `PrintWindow` fallback 產生 uniform frame，呢個
desktop session 嘅 graphics capture 仍然唔可用。Quick Accent、quicktype、
Rainmeter、Randomizer、Screen Recorder 同 Registry Editor 都冇 PNG 產生；
冇 canonical image 被替換或者重用。呢啲係 `capture-blocked` 結果，絕對唔係
visual-pass 聲稱。
**EN —** Batch 11 made a fresh self-contained Short ID capture attempt before its
25-route launch-only slice. The page window appeared, but `CopyFromScreen` was
unavailable and the `PrintWindow` fallback produced a uniform frame. No
`shortid-default.png` was saved, inspected, replaced, or reused; the entire slice is
`capture-blocked`, not visual-pass.

**粵語 —** Batch 11 喺 25-route launch-only slice 之前做咗一次新嘅 self-contained
Short ID capture 嘗試。頁面視窗有出現，但 `CopyFromScreen` 唔可用，而
`PrintWindow` fallback 產生 uniform frame。冇 `shortid-default.png` 儲存、檢查、
替換或者重用；成個 slice 係 `capture-blocked`，唔係 visual-pass。

**EN —** Batch 12 made a fresh self-contained Text Sort capture attempt before its
25-route launch-only slice. The window was reached, but `CopyFromScreen` was unavailable
and the `PrintWindow` fallback produced a uniform frame. No `textsort-default.png` was
saved, inspected, replaced, or reused; the slice is `capture-blocked`, not visual-pass.

**粵語 —** Batch 12 喺 25-route launch-only slice 之前，為文字排序做咗新嘅
self-contained capture 嘗試。視窗有去到，但 `CopyFromScreen` 唔可用，而 `PrintWindow`
fallback 產生 uniform frame。冇 `textsort-default.png` 儲存、檢查、替換或者重用；
成個 slice 係 `capture-blocked`，唔係 visual-pass。

**EN —** Batch 13 made fresh self-contained capture attempts for VirtualBox and the
repaired All Apps dialog. Both reached WinForge, but `CopyFromScreen` was unavailable
and `PrintWindow` produced a uniform frame. No `virtualbox-default.png` or
`shell-allapps-default.png` was saved, inspected, replaced, or reused; these are
`capture-blocked`, not visual-pass results.

**粵語 —** Batch 13 為 VirtualBox 同修正後嘅 All Apps dialog 做咗新嘅
self-contained capture 嘗試。兩個都有去到 WinForge，但 `CopyFromScreen` 唔可用，而
`PrintWindow` 產生 uniform frame。冇 `virtualbox-default.png` 或
`shell-allapps-default.png` 儲存、檢查、替換或者重用；呢啲係 `capture-blocked`，
唔係 visual-pass 結果。

**EN —** The exhaustive-smoke closeout preserves the same visual boundary across the
323-route campaign: every fresh driver attempt that reached capture was blocked because
CopyFromScreen is unavailable and PrintWindow is uniform. No generated, stale, or
uninspected PNG is promoted to evidence; consult the closeout record for the exact
route, source, test, and safety coverage behind this capture-blocked status.

**粵語 —** 完整冒煙測試結案喺 323-route campaign 保留同一個視覺界線：每次有去到
capture 嘅新 driver 嘗試，都因為 CopyFromScreen 唔可用同 PrintWindow uniform 而受阻。
冇 generated、stale 或未檢查 PNG 會升格為證據；請睇結案記錄入面支援呢個
capture-blocked status 嘅 route、source、test 同 safety coverage。

## Redaction Rules · 遮蔽規則

**EN —** Before adding screenshots, redact or avoid personal data: Windows usernames, home-folder paths, repo paths outside WinForge, hostnames, IPs that identify private networks, account names, emails, API keys, tokens, session cookies, vault item names, SSH profiles, and real package/source credentials. Use `winforge-shot --redact "x|y|w|h|box|blur|pixelate"` to obscure regions irreversibly; see the [Wiki Screenshot Workflow](Wiki-Screenshot-Workflow.md).

**粵語 —** 新增截圖前，請遮蔽或者避開個人資料：Windows 用戶名、home folder 路徑、WinForge 以外嘅 repo 路徑、主機名、會識別私人網絡嘅 IP、帳戶名、電郵、API key、token、session cookie、保險庫項目名、SSH profile，同真實套件／來源憑證。用 `winforge-shot --redact "x|y|w|h|box|blur|pixelate"` 不可逆咁遮蔽範圍；詳見 [Wiki 截圖工作流程](Wiki-Screenshot-Workflow.md)。

---

## System & Tweaks · 系統與調校

### Dashboard · 概覽
> Screenshot refresh is blocked in this desktop session: `CopyFromScreen` is unavailable and the `PrintWindow` fallback produces a uniform frame. The Dashboard route remains launch-verified. · 呢個桌面工作階段未能更新截圖：`CopyFromScreen` 未可用，而且 `PrintWindow` 後備方案會產生單一畫面。Dashboard 路由仍已驗證可以啟動。

### Registry Editor · 登錄編輯器
> Screenshot refresh is blocked in this desktop session: `CopyFromScreen` is unavailable and the `PrintWindow` fallback produces a uniform frame. The `regedit` route, editable full-path navigation, and in-app value editing remain launch-verified. · 呢個桌面工作階段未能更新截圖：`CopyFromScreen` 未可用，而且 `PrintWindow` 後備方案會產生單一畫面。`regedit` 路由、可編輯完整路徑導覽同 app 內值編輯仍已驗證可以啟動。

### System Doctors · 系統醫生
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-doctors.png)

### Services · 服務
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-services.png)

### Scheduled Tasks · 排程工作
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-tasks.png)

### Devices · 裝置
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-devices.png)

### ViVeTool · 功能旗標
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vivetool.png)

### Startup Apps · 開機程式
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-startup.png)

### Environment Variables · 環境變數
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-envvars.png)

### Event Viewer · 事件檢視器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-events.png)

### System Info (Winfetch) · 系統資訊
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-winfetch.png)

### System Monitor · 系統監察
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-monitor.png)

### Process Explorer · 程序總管
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-procexp.png)

### Battery & Thermal · 電池與散熱
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-battery.png)

### Volume Mixer · 音量混合器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mixer.png)

### Context Menu · 右鍵選單
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-contextmenu.png)

### Explorer Right-Click · 檔案總管右鍵選單
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-shellmenu.png)

### Nilesoft Shell · Nilesoft 右鍵選單
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-nilesoftshell.png)

### Awake · 保持喚醒
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-awake.png)

### Settings & Control Panel · 設定與控制台
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-settingshub.png)

### Native Utilities · 原生工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-native.png)

### PowerToys Extras · PowerToys 額外工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-powertoys.png)

### Power Display · 顯示器控制
Fresh capture is pending because the current desktop capture host rejects `CopyFromScreen` with “The handle is invalid” even for a one-pixel virtual-screen test, while `PrintWindow` returns a uniform black frame. No blank or misleading screenshot is published. · 新截圖暫時未能提供，因為目前桌面擷取主機連一像素虛擬螢幕測試都會令 `CopyFromScreen` 回傳「The handle is invalid」，而 `PrintWindow` 只會回傳全黑畫面；所以唔會發佈空白或者誤導嘅截圖。

### Video Conference Mute · 視像會議靜音
Fresh capture is pending because the current desktop capture host rejects `CopyFromScreen` with “The handle is invalid” even for a one-pixel virtual-screen test, while `PrintWindow` returns a uniform black frame. No blank or misleading screenshot is published. · 新截圖暫時未能提供，因為目前桌面擷取主機連一像素虛擬螢幕測試都會令 `CopyFromScreen` 回傳「The handle is invalid」，而 `PrintWindow` 只會回傳全黑畫面；所以唔會發佈空白或者誤導嘅截圖。

### World Monitor · 世界監察
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-worldmonitor.png)

### Activity Timeline · 活動時間軸
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-timelens.png)

---

## Files & Disks · 檔案與磁碟

### Archives · 壓縮檔
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-archives.png)

### Batch Rename · 批次改名
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-rename.png)

### Bulk File Ops · 批次檔案操作
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-bulkops.png)

### New+ · 範本新增
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-newplus.png)

### Duplicate Finder · 重複檔案搜尋
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-duplicates.png)

### Instant File Search · 即時檔案搜尋
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-everything.png)

### File Locksmith · 檔案鎖偵測
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-filelocksmith.png)

### Disk Analyser · 磁碟分析
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-disk.png)

### Hex Editor · 十六進位編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hex.png)

### Drives · 磁碟機
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-drives.png)

### Disk Health (SMART) · 硬碟健康（SMART）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diskhealth.png)

### Disk Benchmark · 硬碟速度測試
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diskbench.png)

### TestDisk / PhotoRec Recovery · TestDisk / PhotoRec 資料救援
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-testdisk.png)

### Peek · 快速預覽
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-peek.png)

### Rich Preview · 豐富預覽
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-richpreview.png)

### Roman Numerals · 羅馬數字
> Native visual capture is `capture-blocked`: the repository driver rejected its blank/near-uniform frame, and the inspected LowLevel headless capture showed only a title bar and blank client. No substitute image is shown. · 原生視覺擷取係 `capture-blocked`：repository driver 拒絕咗空白／接近單色 frame，而檢查過嘅 LowLevel 無頭擷取只有 title bar 同空白 client；唔會展示替代圖片。

### OneDrive · OneDrive
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-onedrive.png)

### Font Manager · 字型管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-fonts.png)

### FTP / SFTP · FTP／SFTP 檔案傳輸
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-filezilla.png)

### Config & Backup · 設定與備份
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-configbackup.png)

---

## Media & Capture · 媒體與擷取

### Media · 媒體
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-media.png)

### Audio Editor · 音訊編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-audioeditor.png)

### Audio Tagger · 音訊標籤編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-tags.png)

### Media Player · 媒體播放器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mediaplayer.png)

### Media Downloader · 媒體下載器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ytdlp.png)

### Document Converter · 文件轉換器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-libreoffice.png)

### PDF Toolkit · PDF 工具箱
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pdf.png)

### Screen Recorder · 螢幕錄影
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-recorder.png)

### Capture Studio · 擷取工作室
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-capture.png)

### Text Extractor (OCR) · 原生文字辨識
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ocr.png)

### GIF Studio · 螢幕轉 GIF
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-giflab.png)

### Crop And Lock · 裁切與鎖定
Fresh capture is pending because the current desktop capture host rejects `CopyFromScreen` with “The handle is invalid” even for a one-pixel virtual-screen test, while `PrintWindow` returns a uniform black frame. No blank or stale screenshot is published. · 新截圖暫時未能提供，因為目前桌面擷取主機連一像素虛擬螢幕測試都會令 `CopyFromScreen` 回傳「The handle is invalid」，而 `PrintWindow` 只會回傳全黑畫面；所以唔會發佈空白或者過期嘅截圖。

### ZoomIt · 螢幕放大與標註
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-zoomit.png)

### Voice & Read-Aloud · 語音朗讀
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-voice.png)

### PA Announcements · 喇叭語音廣播
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-announce.png)

### Pixel Editor · 像素畫編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pixeleditor.png)

### Image Editor · 點陣圖影像編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-imageeditor.png)

### Blender (3D / Render) · Blender（3D／算圖）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-blender.png)

---

## Developer · 開發者

### VS Code · VS Code 編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vscode.png)

### Windows Terminal · Windows 終端機
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-terminal.png)

### SSH Toolset · SSH 工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ssh.png)

### quicktype · JSON 轉型別
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-quicktype.png)

### API Client · REST API 用戶端
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-api.png)

### Diff & Merge (WinMerge) · 比對與合併
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diff.png)

### Diagram Editor · 圖表編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-diagram.png)

### .NET Decompiler · .NET 反編譯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-decompiler.png)

### Postgres Tool · Postgres 工具 / pgAdmin
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-pgadmin.png)

### SQLite Browser · SQLite 資料庫瀏覽器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-sqlite.png)

### Packer (Image Builder) · Packer（映像建置器）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packer.png)

### AWS Manager · AWS 管理中心
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aws.png)

### Website Cloner · 網站複製器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-webcloner.png)

### Resume Writer · 履歷與求職信寫手
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-resume.png)

### Regex Cheatsheet · 正則速查
> **Capture status · 截圖狀態：** Fresh `regexcheat` capture is `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback was uniform, and graphics capture was unavailable in this desktop session. The route passed a launch-only check, but no PNG was created, inspected, or claimed as visual verification. · 新嘅 `regexcheat` 截圖係 `capture-blocked`：呢個 desktop session 嘅 `CopyFromScreen` 唔可用、`PrintWindow` 後備畫面係 uniform，而 graphics capture 亦唔可用。route launch-only check 通過，但冇 PNG 產生、檢查或者當成視覺驗證。

### Password Generator · 密碼產生器
> **Capture status · 截圖狀態：** Fresh native Password Generator capture is `capture-blocked`: `CopyFromScreen` was unavailable and the `PrintWindow` fallback was blank or near-uniform; the requested LowLevel MCP tools are not registered in the active Codex session, so no LowLevel capture is claimed. No PNG or substitute is shown. · 最新原生 Password Generator 擷取係 `capture-blocked`：`CopyFromScreen` 用唔到，而 `PrintWindow` fallback 係空白／接近單色；要求嘅 LowLevel MCP tools 未有登記喺目前 Codex session，所以唔會聲稱有 LowLevel 擷取。唔會展示 PNG 或替代圖片。

### UUID v7 · UUID v7 識別碼
> **Capture status · 截圖狀態：** Fresh native UUID v7 capture is `capture-blocked`: the repository driver rejected its blank/near-uniform `PrintWindow` fallback, and the inspected LowLevel MCP isolated-desktop 1980×1320 HWND capture contained only a title bar and blank client surface; the 1958×1264 client-only frame was also blank. Both invalid PNGs were discarded and no substitute image is shown. · 最新原生 UUID v7 擷取係 `capture-blocked`：repository driver 拒絕咗空白／接近單色 `PrintWindow` fallback，而檢查過嘅 LowLevel MCP 隔離 desktop 1980×1320 HWND 擷取只得 title bar 同空白 client surface；1958×1264 只限 client frame 亦係空白。兩張無效 PNG 已丟棄，唔會展示替代圖片。

---

## Network · 網絡

### Connections · 連線
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-connections.png)

### Hosts Editor · hosts 編輯器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hosts.png)

### Packet Capture · 封包擷取
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-wireshark.png)

### Nmap Scanner · 網絡掃描
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-nmap.png)

### VPN & Mesh · VPN 與網狀網
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vpn.png)

### RustDesk · 遠端桌面
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-rustdesk.png)

### Cloudflare & Tunnel · Cloudflare 與 Tunnel
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cloudflare.png)

### Home Assistant · 家居助理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-homeassistant.png)

### In-App Login · 內置登入
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-weblogin.png)

---

## Apps, Git & Packages · 應用程式、Git 與套件

### Git & GitHub · Git 與 GitHub
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-git.png)

### Package Manager · 套件管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-packages.png)

> Managed production reference · 受控正式版參考：this image documents the shipping managed page. The changed native C++ Bundle-workspace routes are `capture-blocked`, so no managed image is substituted as native visual evidence. · 呢張圖記錄發佈中嘅受控頁。改過嘅原生 C++ Bundle 工作區 routes 係 `capture-blocked`，唔會用受控圖片當原生視覺證據。

### Cake Factory & Farm · 蛋糕工廠與農場
![](images/screenshot-cakefactory.png)

### App Uninstaller · 應用程式解除安裝
> Fresh native App Uninstaller capture is capture-blocked: CopyFromScreen was unavailable, and the owned PrintWindow fallback was blank or near-uniform, so it was rejected. The legacy managed screenshot was retired rather than used as native evidence. See [Native App Uninstaller](Native-App-Uninstaller.md).
>
> **2026-07-17 retry:** After final normal-integrity/no-local-data safety hardening, the required native driver again reported CopyFromScreen unavailable and rejected a blank/near-uniform PrintWindow frame. No candidate PNG exists; this is behavior-only evidence, not a visual pass.
>
> **2026-07-17 LowLevel headless retry:** Cheap LowLevel created an off-screen desktop and launched module.uninstall without touching the visible desktop. PrintWindow returned the native title bar plus a blank client frame; UI Automation saw no NativePageTitle after 30 seconds. The image was inspected and rejected, and the smoke did not fall back to a focus-stealing desktop.
>
> **2026-07-17 LowLevel headless 重試：** Cheap LowLevel 用 off-screen desktop 啟動 module.uninstall，完全冇觸碰可見桌面。PrintWindow 只得到 native title bar 加空白 client frame；UI Automation 等 30 秒仍然冇 NativePageTitle。圖片已檢查並拒絕，smoke 絕不回退去會搶焦點的桌面。
>
> **2026-07-17 重試：** 最終正常 integrity／不刪本機資料 hardening 後，required native driver 再次報告 CopyFromScreen 不可用，並拒絕 blank/near-uniform PrintWindow frame。冇 candidate PNG；呢個只係行為證據，唔係 visual pass。
>
> 新原生 App Uninstaller 擷取係 capture-blocked：CopyFromScreen 唔可用，而擁有嘅 PrintWindow fallback 係空白或者近乎單色，所以已拒絕。舊 managed 截圖已退休，冇當作原生證據。詳情見 [Native App Uninstaller](Native-App-Uninstaller.md)。

### Android (ADB) · Android（ADB）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-adb.png)

### Fastboot / Flasher · Fastboot／刷機
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-fastboot.png)

### Android Emulator & SDK · Android 模擬器與 SDK
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-emulator.png)

### qBittorrent · 種子下載
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-qbittorrent.png)

### Native Torrent · 原生種子下載
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-torrent.png)

### Communications · 通訊
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-comms.png)

### Mail · 電郵
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mail.png)

---

## AI · 人工智能

### AI Agents · AI 代理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ai.png)

### AI Chat · AI 聊天
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-aichat.png)

### Ollama · 本地大模型
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-ollama.png)

---

## Window Management · 視窗管理

### Window Manager · 視窗管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-windows.png)

### Workspaces · 工作區
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-workspaces.png)

### FancyZones · 視窗分區
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-fancyzones.png)

### AltSnap · Alt 拖曳視窗
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-altsnap.png)

### Komorebi (Tiling WM) · Komorebi 平鋪視窗管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-komorebi.png)

### GlazeWM Tiling · GlazeWM 平鋪視窗
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-glazewm.png)

---

## PowerToys-style Utilities · PowerToys 式工具

### Keyboard Remapper · 鍵盤重新對應
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keyboard.png)

### Hotkey & Macro Runner · 熱鍵與巨集
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-hotkeys.png)

### Shortcut Guide · 快捷鍵指南
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-shortcutguide.png)

### Command Palette · 指令面板
> Screenshot refresh is blocked in this desktop session: `CopyFromScreen` is unavailable and the `PrintWindow` fallback produces a uniform frame. The `cmdpalette` deep link, diacritic-insensitive search, direct `reg HKCU\\...` registry-path handoff, immediate native theme switching, accessible Solid/Mica/Acrylic appearance and local background images, bookmarks, credential-free Remote Desktop profiles, on-demand performance metrics, explicit command mode, Window Walker provider, and persistent Dock remain launch-verified. · 呢個桌面工作階段未能更新截圖：`CopyFromScreen` 未可用，而且 `PrintWindow` 後備方案會產生單一畫面。`cmdpalette` 深層連結、忽略重音符號搜尋、直接 `reg HKCU\\...` 登錄檔路徑交接、即時原生主題切換、容易閱讀嘅 Solid／Mica／Acrylic 外觀同本機背景圖片、書籤、冇儲存登入資料嘅遠端桌面設定檔、按需效能指標、明確指令模式、Window Walker 提供者同常駐 Dock 仍已驗證可以啟動。

### Color Picker · 螢幕取色
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-colorpicker.png)

### Screen Ruler · 螢幕間尺
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-screenruler.png)

### Mouse Utilities · 滑鼠工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mouseutils.png)

### CursorWrap · 游標環繞
> Fresh native capture is blocked in this desktop session: `CopyFromScreen` is unavailable and the driver rejected the blank / near-uniform fallback, so no `screenshot-cursorwrap.png` was created or replaced. The feature remains documented through the live Mouse Utilities page and its text entry here.

> 新原生截圖喺呢個 desktop session 受阻：`CopyFromScreen` 唔可用，而 driver 又拒絕咗空白／接近單色 fallback，所以冇建立或者替換 `screenshot-cursorwrap.png`。呢個功能仍然透過即時 Mouse Utilities 頁面同此處文字條目記錄。

### Mouse & Pointer · 滑鼠與指標
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mouse.png)

### Mouse Without Borders · 無界滑鼠
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-mwb.png)

### Quick Accent · 快速重音符
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-quickaccent.png)

### Case Converter · 大小寫轉換
> Fresh native capture is `capture-blocked`: the repository driver rejected a blank/near-uniform `PrintWindow` client frame, and the inspected LowLevel headless-desktop HWND capture was title-bar-only. The stale Case Converter images were retired rather than reused as visual evidence. · 最新原生擷取係 `capture-blocked`：repository driver 拒絕咗空白／接近單色嘅 `PrintWindow` client frame，而檢查過嘅 LowLevel 無頭 desktop HWND 擷取只有 title bar。過時嘅 Case Converter 圖片已移除，唔會重用做視覺證據。

### Command Not Found · 搵唔到指令
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-cmdnotfound.png)

### Clipboard · 剪貼簿
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-clipboard.png)

### Advanced Paste · 進階貼上
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-advancedpaste.png)

### Taskbar Tweaker · 工作列調校
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-taskbar-tweaker.png)

### Windhawk Mods · Windhawk 模組
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-windhawk.png)

### LightSwitch (Auto Dark Mode) · 自動深淺色
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-lightswitch.png)

### Rainmeter Widgets · Rainmeter 桌面小工具
> Fresh Batch 10 capture is blocked: `CopyFromScreen` is unavailable and the
> `PrintWindow` fallback produced a uniform frame. No current Rainmeter PNG was
> created, so the superseded Rainmeter screenshots were removed rather than
> reused as visual evidence. · Batch 10 新截圖受阻：`CopyFromScreen` 未可用，而
> `PrintWindow` 後備方案產生 uniform frame。冇建立最新 Rainmeter PNG，所以已移除
> 過時 Rainmeter 截圖，唔會重用做視覺證據。

### Time & Unit Tools · 時間與單位工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-time.png)

### Flashcards · 間隔重複記憶卡
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-flashcards.png)

---

## Virtualization & Containers · 虛擬化與容器

### Docker · Docker 容器管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-docker.png)

### Docker over SSH · 透過 SSH 控制 Docker
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-dockerssh.png)

### WSL & VM Launcher · WSL 與 VM 啟動器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-wsl.png)

### VirtualBox Manager · VirtualBox 管理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-virtualbox.png)

### Proxmox VE · Proxmox VE 虛擬化
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-proxmox.png)

---

## Security & Vaults · 安全與保險庫

### WinForge Vault · WinForge 保險庫
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-vault.png)

### Bitwarden Vault · Bitwarden 密碼庫
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-bitwarden.png)

### KeePass Vault · 密碼保險庫
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-keepass.png)

---

## Gaming & Emulation · 遊戲與模擬

### Minecraft World Editor (Amulet) · Minecraft 世界編輯器（Amulet）
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-amulet.png)

### Minecraft Server · Minecraft 伺服器
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-minecraftserver.png)

### ViaProxy · Minecraft 版本代理
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-viaproxy.png)

### Imaging & Game Tools · 燒錄與遊戲工具
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-imaging.png)

---

## Nuclear Reactor · 核反應堆

### Nuclear Reactor · 核反應堆
![](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-reactor.png)

### Reactor Settings · 反應堆設定
> **Capture status · 截圖狀態：** Fresh `reactorsettings` capture is `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback was uniform, and graphics capture was unavailable in this desktop session. The prior Reactor Settings image was removed rather than reused as current evidence; the route passed a no-control launch-only check. · 新嘅 `reactorsettings` 截圖係 `capture-blocked`：呢個 desktop session 嘅 `CopyFromScreen` 唔可用、`PrintWindow` 後備畫面係 uniform，而 graphics capture 亦唔可用。之前嘅 Reactor Settings 圖片已移除，唔會當成最新證據重用；route 冇操作控制項嘅 launch-only check 通過。

### Reactor Gauges · 反應堆儀表
![](images/screenshot-reactor-gauges.png)

### Reactor Meltdown Scenario · 反應堆熔毀情境
![](images/screenshot-reactor-meltdown.png)

---

## Additional Wiki Captures · 額外 Wiki 截圖

### AltSnap · Alt 拖曳視窗
![](images/screenshot-altsnap.png)

### Annoyances · 煩擾項目
![](images/screenshot-annoyances.png)

### Battery & Thermal · 電池與散熱
![](images/screenshot-battery.png)

### Maintenance · 維護
![](images/screenshot-maintenance.png)

### Nilesoft Shell · Nilesoft 右鍵選單
![](images/screenshot-nilesoftshell.png)

### qBittorrent · 種子下載
![](images/screenshot-qbittorrent.png)

### Recipes · 配方
![](images/screenshot-recipes.png)

### Search · 搜尋
![](images/screenshot-search.png)

### Taskbar Tweaker · 工作列調校
![](images/screenshot-taskbar-tweaker.png)

### App Uninstaller · 應用程式解除安裝
> A second legacy managed App Uninstaller image was also retired. No replacement PNG is claimed until a graphics-capable isolated desktop can produce and inspect a real native frame.
>
> 第二張舊 managed App Uninstaller 圖同樣已退休；要等 graphics-capable 隔離 desktop 可以產生同檢查真正原生 frame，先會宣稱有 replacement PNG。

### Winaero · Winaero 調校
![](images/screenshot-winaero.png)


## Command Palette extension packs capture status · 指令面板擴充套件截圖狀態

- Launch-only smoke check: `cmdpalette` started successfully after the extension-pack update.
- Capture attempt: `CopyFromScreen` was unavailable; the `PrintWindow` fallback produced a uniform frame, so graphics capture is unavailable in this desktop session.
- No replacement canonical screenshot was published, and no visual inspection is claimed for this update.

- 僅啟動測試：擴充套件更新後，`cmdpalette` 已成功啟動。
- 截圖嘗試：`CopyFromScreen` 未可用；`PrintWindow` 備援只產生單色畫面，所以呢個桌面工作階段未能擷取圖像。
- 今次冇發佈替代嘅正式截圖，亦都冇聲稱已做視覺檢查。

## Reference Text native capture status · 原生參考文字截圖狀態

- Fresh post-integration `htmlentities` capture (2026-07-19): LowLevel Computer Use MCP was not callable in this Codex session. The process-owned native driver found `CopyFromScreen` unavailable and rejected its blank or near-uniform `PrintWindow` fallback. No PNG was created or retained, no canonical screenshot changed, and the three Reference Text routes remain `capture-blocked`.
- 整合後最新 `htmlentities` 截圖（2026-07-19）：今次 Codex session 冇可呼叫嘅 LowLevel Computer Use MCP。只屬於本次程序嘅原生 driver 發現 `CopyFromScreen` 唔可用，並拒絕空白／近乎單色 `PrintWindow` fallback。冇建立或保留 PNG、冇改 canonical 截圖，三條 Reference Text route 保持 `capture-blocked`。
