# Native C++ Rewrite · 原生 C++ 重寫

> Status on 2026-07-15: the first native foundation batch plus several utility slices are runnable and tested, but the whole-product rewrite is **not complete**. The managed WinUI 3 application remains the shipping behavioral reference until every ledger row passes. · 2026-07-15 狀態：第一批原生基礎加幾個 utility 批次已經可以執行同測試，但全產品重寫**未完成**。每一項清單全部通過之前，受控 WinUI 3 app 仍然係發佈同功能行為基準。

## Objective · 目標

WinForge is being rewritten as a genuine C++20/C++/WinRT WinUI 3 application. The final executable must not use C++/CLI, host the CLR, wrap the C# executable, communicate with a managed clone over IPC, or start the managed app as a subprocess. Existing C# and XAML stay in the repository only as the migration oracle until native parity and cutover are proven.

WinForge 正在重寫成真正嘅 C++20／C++/WinRT WinUI 3 app。最終 executable 唔可以用 C++/CLI、host CLR、包住 C# executable、用 IPC 駁住受控 clone，亦唔可以用 subprocess 開受控 app。現有 C# 同 XAML 喺遷移期間只會留低做行為基準，直到原生對等同正式切換有證據證實。

## Corrected coverage contract · 修正後覆蓋合約

The old 323-route smoke manifest omitted runtime-built category routes and Settings. The canonical rewrite inventory is now generated from live source and checked on every run:

舊 323-route 冒煙清單漏咗執行時建立嘅分類路線同 Settings。原生重寫嘅權威清單而家由現有來源產生，而且每次都會驗證：

| Surface · 介面 | Count · 數量 |
|---|---:|
| Registry records (Dashboard + modules) · Registry 記錄（Dashboard + 模組） | 319 |
| Runtime category routes · 執行時分類路線 | 22 |
| Additional shell routes · 額外 shell 路線 | 5 |
| Fixed routes · 固定路線 | **346** |
| Dynamic route families · 動態路線組 | **5** |
| Deep-link aliases · 深層連結別名 | **805** |

The dynamic families are `search:<query>`, `manual:<fragment>`, `module.<id>#<fragment>`, `module.packages#(discover|updates|installed)`, and `weblogin?url=<uri>`.

動態組係 `search:<query>`、`manual:<fragment>`、`module.<id>#<fragment>`、`module.packages#(discover|updates|installed)` 同 `weblogin?url=<uri>`。

Four legacy process-launch aliases intentionally collide with canonical category ids. In-app `apps`, `launcher`, `taskbar`, and `vault` open their categories; `--page` preserves the managed mappings to App Uninstaller, Command Palette, Taskbar Tweaker, and WinForge Vault. Native core regressions lock both contexts down.

四個舊 process-launch 別名會刻意同分類 id 撞名。app 內嘅 `apps`、`launcher`、`taskbar` 同 `vault` 會開分類；`--page` 就保留受控版原有映射，分別開 App Uninstaller、Command Palette、Taskbar Tweaker 同 WinForge Vault。原生核心回歸測試會鎖實兩種情境。

## Foundation delivered in this batch · 呢批已完成嘅基礎

- `WinForge.Native.sln` with a self-contained, unpackaged x64 C++/WinRT WinUI 3 executable. · `WinForge.Native.sln`，建置自包含、免封裝 x64 C++/WinRT WinUI 3 executable。
- Standard C++ route parsing, localization, module records, and context-aware route indexing. · 標準 C++ 路線解析、本地化、模組記錄同按情境解析嘅 route index。
- Generated UTF-8 bilingual catalog for all 346 fixed routes and five dynamic families. · 為 346 條固定路線同五組動態路線產生 UTF-8 雙語目錄。
- Native Dashboard, About, Settings entry, All Apps discovery/filtering, English/Cantonese/Bilingual language modes, search, and deep-link routing. · 原生 Dashboard、About、Settings 入口、所有 app 搜尋／篩選、英文／粵語／雙語模式、搜尋同深層連結 routing。
- Honest pending pages for unported features. A resolving route is not presented as an implemented feature. · 未移植功能會顯示清楚嘅 pending 頁；路線開到唔代表功能已完成。
- A parity ledger with separate static, build, test, launch, visual, behavior, side-effect, and documentation evidence dimensions. · 對等清單會分開 static、build、test、launch、visual、behavior、副作用同文件證據。
- Updated driver support for `-Native`, plus process-owned UI Automation smoke testing. · driver 已支援 `-Native`，亦加咗只控制自己 process 嘅 UI Automation 冒煙測試。

Package Manager has moved from pending to an honest **in-progress** native C++ port. Its native surface exposes all nine views—Discover, Updates, Installed, Bundles, Sources, Ignored, Setup, Settings, and Operations—and all 11 Windows manager adapters: WinGet, Scoop, Chocolatey, pip, npm, .NET Tool, PowerShell Gallery, PowerShell 7, Cargo, Bun, and vcpkg. Non-mutating result queries are wired for Discover, Updates, and Installed; per-row Details now runs the validated read-only details command and renders safe parsed common fields above bounded raw command output; local all-version ignores, update-version pins, and configurable 1/7/14/30-day snoozes persist in native JSON, list/remove in the Ignored tab, and hide matching Updates rows; Sources runs only a read-only command probe, and its raw configuration and diagnostics are deliberately withheld until manager-specific secret redaction is proven. Per-row Install, Update, Uninstall, and Update All still create preview-only executable/argv plans in Operations through the same validated command builders; those plans now persist as structured queue entries with legacy-history import, run-next/run-last ordering, and retry-preview markers, but they never start package mutation processes. The runtime preserves the argument vector instead of rebuilding shell strings, constrains executable and `PATH` resolution, bounds HTTP responses and captured output, verifies process tokens, keeps sensitive source data behind the runtime boundary, publishes explicit probe/query state, fails closed for unsafe integrity, and exposes stable accessibility/UI Automation contracts.

套件管理已經由 pending 推進到如實標示為**進行中**嘅原生 C++ 移植。原生介面而家有全部九個 view——Discover、Updates、Installed、Bundles、Sources、Ignored、Setup、Settings 同 Operations——亦有全部 11 個 Windows manager adapter：WinGet、Scoop、Chocolatey、pip、npm、.NET Tool、PowerShell Gallery、PowerShell 7、Cargo、Bun 同 vcpkg。Discover、Updates 同 Installed 已經接好非修改結果查詢；逐列 Details 而家會執行已驗證嘅只讀詳細資料指令，並喺有界原始指令輸出之上顯示安全已解析常用欄位；本機全部版本忽略、更新版本釘選同可設定 1／7／14／30 日暫停會保存喺原生 JSON、喺 Ignored tab 列出／移除，並隱藏相符 Updates 資料列；Sources 只會執行只讀指令探測，逐管理器機密遮罩未證實之前會刻意隱藏原始設定同診斷。逐列安裝、更新、解除安裝同全部更新仍然只會經同一套已驗證 command builder 喺 Operations 建立 executable／argv 預覽計劃；呢啲計劃而家會保存成結構化佇列項目、可匯入舊歷史、支援下一個／最後執行排序同重試預覽標記，但永遠唔會啟動套件修改 process。runtime 會保留原本 argument vector，唔會重新拼 shell string；亦會限制 executable 同 `PATH` 解析、為 HTTP response 同擷取輸出設上限、驗證 process token、將敏感來源資料留喺 runtime 邊界之後、清楚發佈 probe／query 狀態、遇到不安全 integrity 就 fail closed，並提供穩定 accessibility／UI Automation contract。

This is not Package Manager parity. Manager-specific rich Details layouts/actions, scheduler/notification integration, package mutation execution (install, update, and uninstall), Setup, broader per-manager Settings, the real mutation coordinator/output/cancellation lifecycle, .NET update resolution, and PyPI metadata hydration remain incomplete.

呢個唔係套件管理功能對等。逐管理器豐富 Details 版面／動作、排程／通知整合、套件修改執行（安裝、更新同解除安裝）、Setup、更完整逐管理器 Settings、真正修改協調器／輸出／取消生命週期、.NET 更新解析同 PyPI metadata hydration 仍然未完成。

### Check Digit parity slice · 檢查碼對等批次

`module.checkdigit` no longer renders the pending page. Its native WinUI surface and standard C++ core cover live Luhn validation with Visa/Amex/Mastercard/Discover detection, ISBN-10 (including `X`), ISBN-13, EAN-13, UPC-A, and IBAN validation plus expected check values. Standards hardening is mirrored in the managed oracle: digit-oriented formats use ASCII digits, ISBN-13 requires an International ISBN Agency 978/979 prefix, and IBAN checks its ASCII country/check header, all 89 registered country prefixes, exact country length, and SWIFT Release 102 BBAN character pattern before incremental mod-97. Input and selected scheme survive English/Cantonese/Bilingual rerenders; `checkdigit`, `luhn`, and `module.checkdigit` all resolve to the real page.

`module.checkdigit` 已經唔再顯示 pending 頁。原生 WinUI 介面同標準 C++ core 覆蓋即時 Luhn 驗證連 Visa／Amex／Mastercard／Discover 卡種識別、ISBN-10（包括 `X`）、ISBN-13、EAN-13、UPC-A，同 IBAN 驗證及應有檢查值。標準加固亦同步到受控基準：數字格式只接受 ASCII 數字、ISBN-13 一定要用 International ISBN Agency 嘅 978／979 開頭，而 IBAN 會喺增量 mod-97 之前先檢查 ASCII 國家／檢查碼、全部 89 個已登記國家 prefix、國家固定長度，同 SWIFT Release 102 BBAN 字元格式。轉英文／粵語／雙語時會保留輸入同已揀格式；`checkdigit`、`luhn` 同 `module.checkdigit` 全部會開真正功能頁。

The behavior, build, test, launch, static-source, no-side-effect, accessibility, and documentation dimensions pass. The route remains **in progress**, not complete, because this desktop session cannot capture a composited WinUI frame; visual evidence is honestly `capture-blocked`.

行為、建置、測試、啟動、靜態來源、無副作用、無障礙同文件維度全部通過。呢條 route 仍然標示**進行中**，唔係完成，因為呢個 desktop session 擷取唔到合成後 WinUI 畫面；視覺證據會如實標示 `capture-blocked`。

### Text to Binary parity slice · 文字轉二進位對等批次

`module.binarytext` no longer renders a pending page. Its standard-C++ core encodes UTF-8 text as space-separated binary, decimal, octal, or uppercase hexadecimal byte codes and decodes the same formats back to UTF-16 text. It preserves the managed token grammar: spaces, tabs, CR/LF, and commas separate tokens; each raw token receives the managed Unicode `Trim` treatment; matching `0b`, `0o`, and `0x` prefixes are accepted; malformed digits or a value above 255 fail the whole decode without partial output. The decoder and encoder also mirror the managed replacement-character behavior for malformed UTF-8 continuation-prefix/invalid-scalar boundaries and unpaired UTF-16. The native page preserves the selected base, input, and output across English/Cantonese/Bilingual rerenders; exposes localized UI Automation names and a polite live region with caution styling on failures; and only writes the clipboard after the operator explicitly selects Copy. `binarytext`, `textbinary`, and `module.binarytext` all reach the real page.

`module.binarytext` 已經唔再顯示 pending 頁。佢嘅標準 C++ core 會將 UTF-8 文字編碼成用空格分隔嘅二進位、十進位、八進位或者大楷十六進位位元組碼，再解碼返 UTF-16 文字。佢保留受控版 token 規則：空格、tab、CR/LF 同逗號都係分隔；每個 raw token 都會跟受控版做 Unicode `Trim`；配對嘅 `0b`、`0o` 同 `0x` prefix 可以用；錯誤數字或者大過 255 嘅值會令成次解碼失敗，唔會留下部分輸出。decoder 同 encoder 亦會跟受控版處理無效 UTF-8 continuation-prefix／無效 scalar 邊界同未配對 UTF-16 嘅替代字元。原生頁轉英文／粵語／雙語時會保留已揀進位、輸入同輸出；有本地化 UI Automation 名稱同失敗時用 caution 樣式嘅 polite live region；而且只會喺操作員明確撳 Copy 時先寫剪貼簿。`binarytext`、`textbinary` 同 `module.binarytext` 全部會開真正頁面。

The functional dimensions pass: 25 focused native cases run within each 281/281 native executable (235 core route/package-manager checks plus 46 parser checks), a linked managed-reference regression passes 18/18, and the 115/115 live shell still drives Encode, Decode, Move output to input, Copy, malformed-input clear, selected-base/input/output retention, and all aliases. The route remains **in progress** solely because a fresh compositor capture is `capture-blocked`.

功能維度全部通過：25 個專項原生案例會包含喺每次 281/281 原生 executable（235 個 core route／package-manager 檢查加 46 個 parser 檢查），連結嘅受控參考回歸係 18/18，而 115/115 即時 shell 仍然會明確操作 Encode、Decode、搬輸出去輸入、Copy、格式錯誤清空、已揀進位／輸入／輸出保留，同全部 alias。呢條 route 只係因為最新 compositor 截圖係 `capture-blocked` 先保持**進行中**。

### Case Converter parity slice · 大小寫轉換對等批次

`module.caseconvert` now opens a real native page instead of the old pending shell. The standard-C++ core tokenizes separators and camel/Pascal/digit boundaries, then emits ten ordered forms: camelCase, PascalCase, snake_case, kebab-case, CONSTANT_CASE, Title Case, Sentence case, dot.case, path/case, and Train-Case. It uses built-in Windows invariant NLS classification/casing rather than a machine-specific ICU DLL, so the behavior is available on both developer machines and hosted CI; the page keeps its bilingual input/output layout through language rerenders, and each row has a copy action with stable accessibility IDs.

`module.caseconvert` 而家會開真正原生頁，唔再係舊 pending shell。標準 C++ core 會按分隔符同 camel／Pascal／數字邊界做 token 化，再輸出十種有序格式：camelCase、PascalCase、snake_case、kebab-case、CONSTANT_CASE、Title Case、Sentence case、dot.case、path/case 同 Train-Case。佢會用內置 Windows invariant NLS 分類／大小寫處理，而唔依賴機器特定 ICU DLL，所以開發機同 hosted CI 都有一致行為；頁面會喺轉語言時保留雙語輸入／輸出版面，而且每一行都有穩定無障礙 ID 嘅複製動作。

Fresh native Case Converter capture was attempted with the repository driver and a LowLevel headless desktop. `CopyFromScreen` was unavailable; the driver's `PrintWindow` fallback and the LowLevel HWND capture both produced a blank or near-uniform WinUI client frame. `docs/screenshot-caseconvert.png` and its wiki-local copy were retired rather than reused, so the slice is `capture-blocked` for visual evidence while its launch and behavior evidence remain separate.

已用 repository driver 同 LowLevel 無頭 desktop 嘗試擷取最新原生 Case Converter。`CopyFromScreen` 用唔到；driver 嘅 `PrintWindow` fallback 同 LowLevel HWND 擷取都只得到空白／接近單色 WinUI client frame。`docs/screenshot-caseconvert.png` 同 wiki 本機副本已移除，唔會重用；所以呢個 slice 嘅視覺證據係 `capture-blocked`，而 launch／behavior 證據會分開記錄。

### Roman Numerals parity slice · 羅馬數字對等批次

`module.romannum` now opens a real native C++ page through `roman`, `romannum`, and its canonical route. Its pure standard-C++ core converts 1–3,999 using canonical subtractive notation and, when Extended is enabled, 1–3,999,999 using U+0305 combining-overline vinculum notation (`4,000 → I̅V̅`). It also accepts canonical parenthetical ×1000 notation such as `(IV) → 4,000`, while deliberately preserving the managed exact canonical-round-trip behavior: direct lowercase, `IIII`, `IC`, `(I)`, and `(I)V` reject. Both live conversion directions and the range toggle survive English/Cantonese/Bilingual rerenders, every input/output/copy surface has a stable UI Automation ID and localized name, and clipboard access occurs only after explicit Copy.

`module.romannum` 而家會經 `roman`、`romannum` 同本體 route 開真正原生 C++ 頁。純標準 C++ core 會用標準相減寫法轉換 1–3,999；開啟「擴充」後會用 U+0305 組合上劃線 vinculum 寫法轉換 1–3,999,999（`4,000 → I̅V̅`）。佢亦接受標準括號 ×1000 寫法，例如 `(IV) → 4,000`；同時有意保留受控版精確 canonical round-trip 行為：直接小楷、`IIII`、`IC`、`(I)` 同 `(I)V` 都會拒絕。雙向即時轉換同範圍 toggle 會喺英文／粵語／雙語重繪時保留，每個輸入／輸出／複製介面都有穩定 UI Automation ID 同本地化名稱，而剪貼簿只會喺明確 Copy 後先掂。

All 17 focused Roman native cases pass in both Debug and Release, and the 13 live Roman UI Automation assertions cover all aliases, standard and extended conversion, canonical parentheses, malformed-output clearing, copy status, state retention, accessibility, and horizontal clipping. The required repository driver and an inspected LowLevel headless desktop both produced a title bar with a blank or near-uniform WinUI client frame. No `screenshot-romannum.png` was accepted, synthesized, or reused; visual evidence is honestly `capture-blocked` while launch and behavior evidence remain separate.

17 個羅馬數字原生專項案例喺 Debug 同 Release 都通過，而 13 個即時 Roman UI Automation assertion 覆蓋全部 alias、標準同擴充轉換、標準括號、格式錯誤輸出清空、複製狀態、狀態保留、無障礙同水平裁切。指定 repository driver 同檢查過嘅 LowLevel 無頭 desktop 都只得到 title bar 加空白／接近單色 WinUI client frame。冇接受、合成或者重用 `screenshot-romannum.png`；視覺證據如實係 `capture-blocked`，launch 同 behavior 證據會分開記錄。

### GUID & ID Generator parity slice · GUID 同 ID 產生器對等批次

`module.guidgen` now opens a real native page instead of the old pending shell. The standard-C++ core generates RFC 4122 version-4 GUIDs with correct variant bits in D/N/B/P/X formats, supports uppercase output, clamps bulk generation to 1–1000 rows, creates Crockford-base32 ULIDs, creates URL-safe nano-IDs with a 4–64 length clamp, and inspects GUID byte order, version, and variant without shelling out or writing system state.

`module.guidgen` 而家會開真正原生頁，唔再係舊 pending shell。標準 C++ core 會產生 RFC 4122 version-4 GUID，variant bit 正確，支援 D/N/B/P/X 格式、大楷輸出、將批量產生限制喺 1–1000 行、產生 Crockford-base32 ULID、產生 URL-safe nano-ID（長度限制 4–64），亦會檢查 GUID byte order、version 同 variant；全程唔 shell out，亦唔寫系統狀態。

The page is reachable through `guidgen` and `module.guidgen`, keeps generated and inspected values across English/Cantonese/Bilingual rerenders, exposes localized UI Automation names plus stable automation IDs for Generate/Copy/Inspect controls, and writes the clipboard only after explicit Copy actions. Functional evidence passes: 16 focused native GUID tests are included in each 281/281 native executable, and the 115/115 live shell drives format switching, uppercase, default bulk generation, ULID, nano-ID, inspector valid/invalid states, and language rerender preservation. Fresh native visual evidence is `capture-blocked`, so the ledger row remains **in progress**.

頁面可以用 `guidgen` 同 `module.guidgen` 開啟；英文／粵語／雙語重繪時會保留已產生同已檢查值；Generate／Copy／Inspect 控制項有本地化 UI Automation 名稱同穩定 automation ID；只有明確 Copy 動作先會寫剪貼簿。功能證據通過：每次 281/281 原生 executable 都包含 16 個 GUID 專項測試，而 115/115 即時 shell 會操作格式切換、大楷、預設批量產生、ULID、nano-ID、inspector 有效／無效狀態同轉語言保留。最新原生視覺證據仍然係 `capture-blocked`，所以清單項保持**進行中**。

### Base32 / Base58 / Ascii85 parity slice · Base32／Base58／Ascii85 對等批次

`module.base32` now opens a real native page instead of the pending shell. Its standard-C++ codec core encodes and decodes UTF-8 bytes as padded or unpadded RFC 4648 Base32, Bitcoin Base58 with leading-zero preservation, and Adobe Ascii85 with `<~ ~>` markers and the `z` zero shortcut. The C++/WinRT page keeps codec selection, input, and output across English/Cantonese/Bilingual rerenders, exposes stable localized UI Automation IDs, reports malformed input through a polite live region, and writes the clipboard only after explicit Copy. `base32`, `base58`, and `module.base32` all reach the real page.

`module.base32` 而家會開真正原生頁，唔再係 pending shell。標準 C++ codec core 會將 UTF-8 位元組用有填充／無填充 RFC 4648 Base32、保留開頭零位元組嘅 Bitcoin Base58，同有 `<~ ~>` 標記及 `z` 零快捷字元嘅 Adobe Ascii85 編碼／解碼。C++/WinRT 頁面喺英文／粵語／雙語重繪時保留編碼方式、輸入同輸出，有穩定本地化 UI Automation ID，無效輸入會經 polite live region 報告，而剪貼簿只會喺明確 Copy 後先寫入。`base32`、`base58` 同 `module.base32` 全部會開真正頁面。

### Layout and native installer CI · 版面同原生安裝程式 CI

The shared native page host now stretches content to the measured viewport, keeps vertical scrolling enabled, and allows horizontal overflow to scroll instead of clipping. The Package Manager toolbar is vertical at narrow widths, so its search, sort, and action controls remain reachable at 100% scaling. `.github/workflows/native-release.yml` is the hosted native delivery gate: it pins the VS 2022 runner, proves/provisions the C++ v143 UWP toolset before MSBuild, restores/builds `WinForge.Native.sln` in Release x64, runs native core tests and catalog parity, packages a portable runtime, and compiles `installer/WinForge.Native.iss` into `WinForge-Native-Setup.exe`. The interactive UI Automation smoke remains a mandatory local/elevated gate because hosted service desktops cannot yield trustworthy WinUI composition or accessibility geometry. · 共用原生頁面 host 而家會將內容拉到量度到嘅 viewport，保留垂直捲動，水平溢出會捲動而唔係裁切。套件管理工具列喺窄闊度會改用直向，令搜尋、排序同動作控制喺 100% 縮放仍然可用。`.github/workflows/native-release.yml` 係 hosted 原生交付 gate：會固定使用 VS 2022 runner、喺 MSBuild 前驗證／準備 C++ v143 UWP 工具組、Release x64 還原／建置 `WinForge.Native.sln`、執行原生核心測試同目錄對等、封裝可攜 runtime，同用 `installer/WinForge.Native.iss` 編譯 `WinForge-Native-Setup.exe`。互動 UI Automation smoke 仍然係必需嘅本機／提權 gate，因為 hosted service desktop 唔會提供可信嘅 WinUI composition 或無障礙 geometry。

Focused codec tests pass 15/15; after the Roman Numerals slice the aggregate native executable now passes 313/313 in Debug and Release (267 core route/package-manager/codec/Roman checks plus 46 parser checks), and the process-owned UI Automation shell passes 141/141, including accessibility names, encode/decode, swap, malformed-output clearing, alias routing, language-rerender preservation, and horizontal-clipping checks for the codec, Roman Numerals, and Package Manager controls. Fresh native visual capture remains `capture-blocked`: the required repository driver’s `PrintWindow` fallback and the inspected LowLevel MCP off-screen desktop HWND capture each returned a blank/near-uniform WinUI client frame; no blank, stale, synthetic, or managed image was substituted.

編解碼專項測試係 15/15；羅馬數字批次之後完整原生 executable 喺 Debug 同 Release 都係 313/313（267 個 core route／package-manager／codec／羅馬數字檢查加 46 個 parser 檢查），只控制自己 process 嘅 UI Automation shell 係 141/141，涵蓋無障礙名稱、編碼／解碼、對調、錯誤輸出清空、alias routing、轉語言時保留狀態，同 codec／羅馬數字／套件管理控制項嘅水平裁切檢查。最新原生視覺擷取仍然係 `capture-blocked`：必需嘅 repository driver `PrintWindow` fallback 同檢查過嘅 LowLevel MCP 離屏 desktop HWND 擷取都得到空白／接近單色 WinUI client frame；冇用空白、舊、合成或者受控版圖片頂替。

`ThirdParty/UniGetUI` is the complete 1,002-file tracked-source snapshot of Devolutions/UniGetUI `main` at commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e`, dated 2026-07-10 and preserved under the MIT license. It remains excluded from build and publish output. The snapshot is exact provenance and a behavior reference only—not an embedded runtime, copied product identity, or evidence of parity.

`ThirdParty/UniGetUI` 係 Devolutions/UniGetUI `main` 喺 commit `21116375c8299d1db38a3c3b4c2eb7e18bc97c4e` 嘅完整 1,002 檔 tracked-source snapshot，日期係 2026-07-10，並按 MIT license 保留；建置同發佈輸出仍然會排除佢。呢份 snapshot 只係精確來源證明同功能參考，唔係內嵌 runtime、複製產品身份，亦唔係對等證據。

## Build and verification · 建置同驗證

Requirements: MSVC with x64 C++ UWP tools, Windows SDK 10.0.26100.0 or newer, and restored packages from `packages.config`. The repository driver discovers v143 or a newer installed toolset.

需求：MSVC x64 C++ UWP tools、Windows SDK 10.0.26100.0 或更新版本，同埋由 `packages.config` 還原嘅 packages。repo driver 會自動搵 v143 或更新嘅已安裝 toolset。

```powershell
# Restore and build through the repository driver · 用 repo driver 還原同建置
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 `
  -Native -Publish -Page dashboard -NoCapture

# Native unit regressions · 原生單元回歸
tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe

# Managed-oracle parity for the shared standards rules · 受控基準標準規則對等
dotnet run --project tests\CheckDigitCore.Tests\CheckDigitCore.Tests.csproj -c Release
dotnet run --project tests\BinaryTextCore.Tests\BinaryTextCore.Tests.csproj -c Release

# Catalog/ledger parity · 目錄／清單對等
powershell -ExecutionPolicy Bypass -File eng\native\Test-NativeCatalogParity.ps1

# Live process-owned shell smoke · 即時、自有 process shell 冒煙測試
powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1
```

Evidence for this batch:

呢批證據：

- Current aggregate after the Roman Numerals slice: native Debug and Release tests **313/313** (267 core route/package-manager/codec/Roman checks plus 46 parser checks, including 17 focused Roman cases) and elevated process-owned UI Automation **141/141** (including 13 live Roman assertions). Earlier per-slice totals below remain historical evidence for those slices. · 羅馬數字批次之後目前整體：原生 Debug／Release 測試 **313/313**（267 個 core route／package-manager／codec／羅馬數字檢查加 46 個 parser，包括 17 個羅馬數字專項案例）同提權、自有 process UI Automation **141/141**（包括 13 個即時 Roman assertion）。下面較舊逐批次總數保留作歷史證據。

- Native Debug x64 solution build: **0 warnings, 0 errors**. Native Release x64 solution build: **0 errors** with one environment warning (`ROSLYNCODETASKFACTORYCSHARPCOMPILER CS1668`) for a missing ATL/MFC LIB search path. · 原生 Debug x64 solution build：**0 warnings, 0 errors**。原生 Release x64 solution build：**0 errors**，另有一個環境 warning（`ROSLYNCODETASKFACTORYCSHARPCOMPILER CS1668`），原因係 ATL/MFC LIB 搜尋路徑不存在。
- Legacy managed solution compile check: **0 errors** with the ignored NuGet restore tree and native C++ source/generated tree explicitly excluded from the SDK item glob. · 舊受控 solution compile check：**0 errors**；SDK item glob 已明確排除忽略咗嘅 NuGet restore tree 同原生 C++ source／generated tree。
- Native test executable: **313/313 in Debug and 313/313 in Release** (267 core route/package-manager/codec/Roman checks plus 46 parser checks). It retains every Package Manager and shell regression, adds safe common Details-field parsing plus unsafe Details-query fail-closed coverage, and keeps the 48 focused Check Digit, 25 focused Text to Binary, 10 Case Converter, 16 GUID Generator, 15 codec, and 17 Roman Numerals cases green. · 原生測試 executable：Debug **313/313**、Release **313/313**（267 個 core route／package-manager／codec／羅馬數字檢查加 46 個 parser 檢查）。佢保留全部套件管理同 shell 回歸，新增安全常用詳細資料欄位解析同唔安全詳細資料查詢 fail-closed 覆蓋，並保持 48 個檢查碼、25 個文字轉二進位、10 個大小寫轉換、16 個 GUID 產生器、15 個編解碼同 17 個羅馬數字專項案例通過。
- Managed Check Digit oracle parity: **24/24** in Release, including the same Discover boundaries, exact registry fixture, Unicode-confusable regression, standards boundaries, and alphanumeric FR/MT IBAN examples. · 受控檢查碼基準對等：Release **24/24**，包括相同 Discover 邊界、精確 registry fixture、Unicode 混淆字元回歸、標準邊界同 FR／MT 字母數字 IBAN 案例。
- Linked managed-reference Text to Binary regression: **18/18** in Release, checking all four bases, separators, managed Unicode trimming, matching prefixes, atomic malformed/range failure, UTF-8 replacement semantics, malformed UTF-16, and Unicode round trips. It is a reference regression, not an automated native-vs-managed process comparison. · 連結嘅受控參考文字轉二進位回歸：Release **18/18**，檢查全部四個進位、分隔、受控 Unicode 修剪、配對 prefix、原子式格式錯誤／範圍失敗、UTF-8 替代字元語義、錯誤 UTF-16，同 Unicode round trip。佢係參考回歸，唔係自動原生對受控 process 比較。
- Elevated process-owned shell smoke: **141/141**. It retains the package, Check Digit, Binary Text, Case Converter, GUID Generator, codec, and Roman Numerals checks, including exact Package Manager view aliases, all 11 manager filters, nine-view routing, native update-rule state, the custom snooze-duration picker, preview queue policy, run-next/run-last controls, retry-preview controls, GUID format/bulk/ULID/nano-ID/inspector behavior, Roman standard/extended/canonical-parenthesis/copy/status/rerender behavior, fail-closed safety behavior, and localized accessibility contracts. This verifies elevated UI and safety-lock behavior, not a normal-integrity external package query. · 提權、自有 process shell 冒煙：**141/141**。佢保留套件管理、檢查碼、文字轉二進位、大小寫轉換、GUID 產生器、編解碼同羅馬數字檢查，包括準確套件管理檢視 alias、全部 11 個管理器篩選、九檢視 routing、原生更新規則狀態、自訂暫停時長選擇器、預覽佇列政策、下一個／最後執行控制、重試預覽控制、GUID 格式／批量／ULID／nano-ID／inspector 行為、羅馬標準／擴充／標準括號／複製／狀態／轉語言行為、fail-closed 安全行為同本地化無障礙 contract。呢項證明提權 UI 同安全鎖行為，唔係正常 integrity 外部套件查詢證據。
- Catalog verifier: 346 fixed routes, five dynamic families, 319 registry records, 22 categories, 346 ledger rows; exact alias sets and UTF-8 Cantonese checks passed. · 目錄驗證：346 固定路線、五組動態路線、319 registry 記錄、22 分類、346 清單項；精確 alias set 同 UTF-8 粵語檢查全部通過。
- Foundation Release PE audit: the COM Descriptor Directory is zero and the import table contains no `coreclr`, `hostfxr`, or `mscoree`. This proves the current shell binary is native; it does not prove feature parity. · 基礎 Release PE 審查：COM Descriptor Directory 係零，import table 冇 `coreclr`、`hostfxr` 或 `mscoree`。呢項只證明目前 shell binary 係原生，唔代表功能已對等。

The normal-integrity live external-query gate remains **blocked**. The harness launched the exact native executable through an interactive scheduled task configured with `RunLevel=Limited`, then independently inspected the resulting process token. Windows still produced an elevated token, and verification stopped with `candidate smoke token is still elevated`; the harness therefore failed closed before running an external package-manager query. The elevated 141/141 result is not substituted for this missing normal-integrity evidence. LowLevel MCP is present at `C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp`; this Codex session still exposes no callable LowLevel MCP namespace, but the repo-local `lowlevel-computer-use-cheap.exe` fallback successfully created `WinSta0\WinForgeRomanAudit`, launched `WinForge.exe --page romannum`, listed the headless WinUI window, and captured its HWND. That inspected LowLevel screenshot was a title bar plus a blank/near-uniform WinUI client frame, so it is launch/headless evidence only, not visual proof.

正常 integrity 即時外部查詢閘門仍然係**受阻**。harness 用設為 `RunLevel=Limited` 嘅互動式排程工作開啟同一個原生 executable，再獨立檢查實際 process token；Windows 仍然產生提權 token，驗證以 `candidate smoke token is still elevated` 停止，所以 harness 喺執行任何外部套件管理查詢之前已經 fail closed。提權環境嘅 141/141 結果唔會頂替欠缺嘅正常 integrity 證據。LowLevel MCP checkout 存在於 `C:\Users\Administrator\Documents\GitHub\lowlevel-computer-use-mcp`；呢個 Codex session 仍然冇曝露可直接呼叫嘅 LowLevel MCP namespace，但 repo 本機 `lowlevel-computer-use-cheap.exe` fallback 已成功建立 `WinSta0\WinForgeRomanAudit`、開啟 `WinForge.exe --page romannum`、列出無頭 WinUI 視窗，並以 HWND 擷取。檢查過嘅 LowLevel 截圖只有 title bar 同空白／接近單色 WinUI client frame，所以只係 launch／headless 證據，唔係 visual proof。

## Visual evidence disposition · 視覺證據處置

Fresh native Dashboard, All Apps, About, Package Manager (`module.packages#updates`, `package-ignored`, `package-settings`, `package-discover`, and the latest `package-operations` preview-queue slice), Check Digit (`checkdigit`), Text to Binary (`binarytext`), Base32 / 58 / 85 (`base32`), GUID Generator (`guidgen`), Case Converter (`caseconvert`), and Roman Numerals (`romannum`) capture attempts were made with the required repository driver. The Roman attempt used `-Native -Page romannum -WaitMs 15000` and failed before accepting a PNG. The same page was also retried through a LowLevel headless desktop, but its inspected HWND capture was title-bar-only with a blank client frame. All changed native surfaces remain blocked in this desktop session. `CopyFromScreen` was unavailable. `PrintWindow(PW_RENDERFULLCONTENT)` returned a title bar but a blank/near-uniform WinUI client frame, so the improved driver rejected it with:

已經用指定 repo driver 重新嘗試擷取原生 Dashboard、所有 app、About、套件管理（`module.packages#updates`、`package-ignored`、`package-settings`、`package-discover` 同最新 `package-operations` 預覽佇列批次）、檢查碼（`checkdigit`）、文字轉二進位（`binarytext`）、Base32／58／85（`base32`）、GUID 產生器（`guidgen`）、大小寫轉換（`caseconvert`）同羅馬數字（`romannum`）。羅馬數字 driver 嘗試使用 `-Native -Page romannum -WaitMs 15000`，並喺接受 PNG 之前失敗；同一頁亦喺 LowLevel 無頭 desktop 重新嘗試過，但檢查過嘅 HWND 擷取只有 title bar 同空白 client frame。所有已改原生介面喺呢個 desktop session 仍然受阻。`CopyFromScreen` 用唔到；`PrintWindow(PW_RENDERFULLCONTENT)` 雖然有 title bar，但 WinUI client frame 係空白／接近單色，所以改良後 driver 拒絕咗：

```text
CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or
near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.
```

No blank, stale, synthetic, or managed-app image was retained or substituted. UI Automation independently found the bilingual navigation tree, page titles, migration InfoBar, buttons, All Apps list, the Package Manager nine-view/11-manager surface, live Check Digit controls/results, Text to Binary/Case Converter conversion controls/results, Base32 / 58 / 85 codec controls/results, GUID Generator format/bulk/ULID/nano-ID/inspector controls/results, and Roman Numerals standard/extended/canonical-parenthesis/copy/rerender controls and results. That is launch/behavior evidence only—not a visual pass. Dashboard, All Apps, About, native Package Manager, native Check Digit, Text to Binary, Case Converter, Base32 / 58 / 85, GUID Generator, and Roman Numerals remain `capture-blocked` until a real composited frame can be captured and inspected.

冇保留或者頂替任何空白、舊、合成、或者受控版圖片。UI Automation 另外搵到雙語導覽樹、page title、遷移 InfoBar、按鈕、所有 app 清單、套件管理九 view／11 manager 介面、即時檢查碼 controls／結果、文字轉二進位／大小寫轉換 controls／結果、Base32／58／85 codec controls／結果、GUID 產生器格式／批量／ULID／nano-ID／inspector controls／結果，同羅馬數字標準／擴充／標準括號／複製／重繪 controls／結果。呢啲只係 launch／behavior 證據，唔係 visual pass。Dashboard、所有 app、About、原生套件管理、原生檢查碼、文字轉二進位、大小寫轉換、Base32／58／85、GUID 產生器同羅馬數字要等真正 composited frame 擷取兼檢查到，先可以解除 `capture-blocked`。

## Safety and compatibility gates · 安全同相容閘門

The native port must preserve DPAPI secret compatibility, atomic persistence, normal-integrity-only update/install paths, fail-closed elevation gates, protocol compatibility, and every reactor invariant: cold-shutdown boot, default-off real shutdown, abortability, reversible real-world effects, and waste disk/cap limits.

原生移植一定要保留 DPAPI secret 相容、原子式 persistence、只限正常 integrity 嘅更新／安裝路徑、fail-closed 提權 gate、protocol 相容，同埋所有反應堆 invariant：cold-shutdown 開機、真實關機預設關閉、可以取消、真實副作用可還原、廢料磁碟／容量上限。

## Cutover definition · 正式切換定義

The rewrite is complete only when every route, feature, control, service, companion, external launcher, updater, installer, SDK protocol, test project, document, wiki page, GitHub Pages mirror, and changed screenshot has passing evidence. Final binary inspection must show no `coreclr`, `hostfxr`, managed assembly, C++/CLI metadata, managed subprocess, or wrapper dependency. Only then may the managed application be retired.

只有每條路線、每項功能、每個 control、service、companion、外部 launcher、updater、installer、SDK protocol、測試專案、文件、wiki、GitHub Pages 鏡像同改過頁面嘅截圖全部有通過證據，先叫完成。最後 binary inspection 必須證明冇 `coreclr`、`hostfxr`、受控 assembly、C++/CLI metadata、受控 subprocess 或 wrapper 相依；到嗰陣先可以退役受控 app。

Canonical machine-readable evidence: [`docs/cpp-port-parity.json`](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json). Wiki overview: [`docs/wiki/Native-Cpp-Rewrite.md`](https://github.com/codingmachineedge/WinForge/blob/main/docs/wiki/Native-Cpp-Rewrite.md).

權威 machine-readable 證據：[`docs/cpp-port-parity.json`](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json)。Wiki 概覽：[`docs/wiki/Native-Cpp-Rewrite.md`](https://github.com/codingmachineedge/WinForge/blob/main/docs/wiki/Native-Cpp-Rewrite.md)。
