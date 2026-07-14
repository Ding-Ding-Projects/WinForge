# Native C++ Rewrite · 原生 C++ 重寫

> Status on 2026-07-13: the first native foundation batch is runnable and tested, but the whole-product rewrite is **not complete**. The managed WinUI 3 application remains the shipping behavioral reference until every ledger row passes. · 2026-07-13 狀態：第一批原生基礎已經可以執行同測試，但全產品重寫**未完成**。每一項清單全部通過之前，受控 WinUI 3 app 仍然係發佈同功能行為基準。

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

Package Manager has moved from pending to an honest **in-progress** native C++ port. Its native surface exposes all nine views—Discover, Updates, Installed, Bundles, Sources, Ignored, Setup, Settings, and Operations—and all 11 Windows manager adapters: WinGet, Scoop, Chocolatey, pip, npm, .NET Tool, PowerShell Gallery, PowerShell 7, Cargo, Bun, and vcpkg. Read-only result queries are wired for Discover, Updates, and Installed; Sources runs only a read-only command probe, and its raw configuration and diagnostics are deliberately withheld until manager-specific secret redaction is proven. The runtime preserves the argument vector instead of rebuilding shell strings, constrains executable and `PATH` resolution, bounds HTTP responses and captured output, verifies process tokens, keeps sensitive source data behind the runtime boundary, publishes explicit probe/query state, fails closed for unsafe integrity, and exposes stable accessibility/UI Automation contracts.

套件管理已經由 pending 推進到如實標示為**進行中**嘅原生 C++ 移植。原生介面而家有全部九個 view——Discover、Updates、Installed、Bundles、Sources、Ignored、Setup、Settings 同 Operations——亦有全部 11 個 Windows manager adapter：WinGet、Scoop、Chocolatey、pip、npm、.NET Tool、PowerShell Gallery、PowerShell 7、Cargo、Bun 同 vcpkg。Discover、Updates 同 Installed 已經接好只讀結果查詢；Sources 只會執行只讀指令探測，逐管理器機密遮罩未證實之前會刻意隱藏原始設定同診斷。runtime 會保留原本 argument vector，唔會重新拼 shell string；亦會限制 executable 同 `PATH` 解析、為 HTTP response 同擷取輸出設上限、驗證 process token、將敏感來源資料留喺 runtime 邊界之後、清楚發佈 probe／query 狀態、遇到不安全 integrity 就 fail closed，並提供穩定 accessibility／UI Automation contract。

This is not Package Manager parity. Package mutation (install, update, and uninstall), Bundles, Ignored, Setup, Settings, the operation queue, .NET update resolution, and PyPI metadata hydration remain incomplete.

呢個唔係套件管理功能對等。套件變更（安裝、更新同解除安裝）、Bundles、Ignored、Setup、Settings、operation queue、.NET 更新解析同 PyPI metadata hydration 仍然未完成。

### Check Digit parity slice · 檢查碼對等批次

`module.checkdigit` no longer renders the pending page. Its native WinUI surface and standard C++ core cover live Luhn validation with Visa/Amex/Mastercard/Discover detection, ISBN-10 (including `X`), ISBN-13, EAN-13, UPC-A, and IBAN validation plus expected check values. Standards hardening is mirrored in the managed oracle: digit-oriented formats use ASCII digits, ISBN-13 requires an International ISBN Agency 978/979 prefix, and IBAN checks its ASCII country/check header, all 89 registered country prefixes, exact country length, and SWIFT Release 102 BBAN character pattern before incremental mod-97. Input and selected scheme survive English/Cantonese/Bilingual rerenders; `checkdigit`, `luhn`, and `module.checkdigit` all resolve to the real page.

`module.checkdigit` 已經唔再顯示 pending 頁。原生 WinUI 介面同標準 C++ core 覆蓋即時 Luhn 驗證連 Visa／Amex／Mastercard／Discover 卡種識別、ISBN-10（包括 `X`）、ISBN-13、EAN-13、UPC-A，同 IBAN 驗證及應有檢查值。標準加固亦同步到受控基準：數字格式只接受 ASCII 數字、ISBN-13 一定要用 International ISBN Agency 嘅 978／979 開頭，而 IBAN 會喺增量 mod-97 之前先檢查 ASCII 國家／檢查碼、全部 89 個已登記國家 prefix、國家固定長度，同 SWIFT Release 102 BBAN 字元格式。轉英文／粵語／雙語時會保留輸入同已揀格式；`checkdigit`、`luhn` 同 `module.checkdigit` 全部會開真正功能頁。

The behavior, build, test, launch, static-source, no-side-effect, accessibility, and documentation dimensions pass. The route remains **in progress**, not complete, because this desktop session cannot capture a composited WinUI frame; visual evidence is honestly `capture-blocked`.

行為、建置、測試、啟動、靜態來源、無副作用、無障礙同文件維度全部通過。呢條 route 仍然標示**進行中**，唔係完成，因為呢個 desktop session 擷取唔到合成後 WinUI 畫面；視覺證據會如實標示 `capture-blocked`。

### Text to Binary parity slice · 文字轉二進位對等批次

`module.binarytext` no longer renders a pending page. Its standard-C++ core encodes UTF-8 text as space-separated binary, decimal, octal, or uppercase hexadecimal byte codes and decodes the same formats back to UTF-16 text. It preserves the managed token grammar: spaces, tabs, CR/LF, and commas separate tokens; each raw token receives the managed Unicode `Trim` treatment; matching `0b`, `0o`, and `0x` prefixes are accepted; malformed digits or a value above 255 fail the whole decode without partial output. The decoder and encoder also mirror the managed replacement-character behavior for malformed UTF-8 continuation-prefix/invalid-scalar boundaries and unpaired UTF-16. The native page preserves the selected base, input, and output across English/Cantonese/Bilingual rerenders; exposes localized UI Automation names and a polite live region with caution styling on failures; and only writes the clipboard after the operator explicitly selects Copy. `binarytext`, `textbinary`, and `module.binarytext` all reach the real page.

`module.binarytext` 已經唔再顯示 pending 頁。佢嘅標準 C++ core 會將 UTF-8 文字編碼成用空格分隔嘅二進位、十進位、八進位或者大楷十六進位位元組碼，再解碼返 UTF-16 文字。佢保留受控版 token 規則：空格、tab、CR/LF 同逗號都係分隔；每個 raw token 都會跟受控版做 Unicode `Trim`；配對嘅 `0b`、`0o` 同 `0x` prefix 可以用；錯誤數字或者大過 255 嘅值會令成次解碼失敗，唔會留下部分輸出。decoder 同 encoder 亦會跟受控版處理無效 UTF-8 continuation-prefix／無效 scalar 邊界同未配對 UTF-16 嘅替代字元。原生頁轉英文／粵語／雙語時會保留已揀進位、輸入同輸出；有本地化 UI Automation 名稱同失敗時用 caution 樣式嘅 polite live region；而且只會喺操作員明確撳 Copy 時先寫剪貼簿。`binarytext`、`textbinary` 同 `module.binarytext` 全部會開真正頁面。

The functional dimensions pass: 25 focused native cases run within each 233/233 native executable (191 core route/package-manager checks plus 42 parser checks), a linked managed-reference regression passes 18/18, and the 59/59 live shell explicitly drives Encode, Decode, Move output to input, Copy, malformed-input clear, selected-base/input/output retention, and all aliases. The route remains **in progress** solely because a fresh compositor capture is `capture-blocked`.

功能維度全部通過：25 個專項原生案例會包含喺每次 233/233 原生 executable（191 個 core route／package-manager 檢查加 42 個 parser 檢查），連結嘅受控參考回歸係 18/18，而 59/59 即時 shell 會明確操作 Encode、Decode、搬輸出去輸入、Copy、格式錯誤清空、已揀進位／輸入／輸出保留，同全部 alias。呢條 route 只係因為最新 compositor 截圖係 `capture-blocked` 先保持**進行中**。

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

- Native Debug and Release x64 solution builds: **0 errors**. · 原生 Debug 同 Release x64 solution build：**0 errors**。
- Legacy managed solution compile check: **0 errors** with the ignored NuGet restore tree and native C++ source/generated tree explicitly excluded from the SDK item glob. · 舊受控 solution compile check：**0 errors**；SDK item glob 已明確排除忽略咗嘅 NuGet restore tree 同原生 C++ source／generated tree。
- Native test executable: **233/233 in Debug and 233/233 in Release** (191 core route/package-manager checks plus 42 parser checks). It retains every Package Manager and shell regression alongside 48 focused Check Digit cases and 25 focused Text to Binary cases, covering every numeric base, token separators/prefixes plus managed Unicode trimming, malformed/range rejection without partial output, Unicode round trips, and managed replacement behavior for malformed UTF-8 continuation-prefix/invalid-scalar boundaries and UTF-16. · 原生測試 executable：Debug **233/233**、Release **233/233**（191 個 core route／package-manager 檢查加 42 個 parser 檢查）。佢保留全部套件管理同 shell 回歸，再加 48 個檢查碼專項案例同 25 個文字轉二進位專項案例，涵蓋全部數字進位、token 分隔／prefix 加上受控 Unicode 修剪、格式錯誤／超範圍時冇部分輸出、Unicode round trip，同受控版處理無效 UTF-8 continuation-prefix／無效 scalar 邊界同 UTF-16 嘅替代字元行為。
- Managed Check Digit oracle parity: **24/24** in Release, including the same Discover boundaries, exact registry fixture, Unicode-confusable regression, standards boundaries, and alphanumeric FR/MT IBAN examples. · 受控檢查碼基準對等：Release **24/24**，包括相同 Discover 邊界、精確 registry fixture、Unicode 混淆字元回歸、標準邊界同 FR／MT 字母數字 IBAN 案例。
- Linked managed-reference Text to Binary regression: **18/18** in Release, checking all four bases, separators, managed Unicode trimming, matching prefixes, atomic malformed/range failure, UTF-8 replacement semantics, malformed UTF-16, and Unicode round trips. It is a reference regression, not an automated native-vs-managed process comparison. · 連結嘅受控參考文字轉二進位回歸：Release **18/18**，檢查全部四個進位、分隔、受控 Unicode 修剪、配對 prefix、原子式格式錯誤／範圍失敗、UTF-8 替代字元語義、錯誤 UTF-16，同 Unicode round trip。佢係參考回歸，唔係自動原生對受控 process 比較。
- Elevated process-owned shell smoke: **59/59**. It retains the package and Check Digit checks and drives Binary Text’s controls, binary/hex conversions, output-to-input move, explicit Copy, malformed-code clearing, localized names, selected-base/input/output-preserving language rerender, and all three route forms. Status changes raise a polite live-region event and failure states use a caution brush. This verifies elevated UI and safety-lock behavior, not a normal-integrity external package query. · 提權、自有 process shell 冒煙：**59/59**。佢保留套件管理同檢查碼檢查，亦會操作文字轉二進位嘅 controls、二進位／十六進位轉換、輸出搬去輸入、明確 Copy、錯誤碼清空、本地化名稱、轉語言後保留已揀進位／輸入／輸出，同三種 route form。狀態改變會觸發 polite live-region event，而失敗狀態會用 caution brush。呢項證明提權 UI 同安全鎖行為，唔係正常 integrity 外部套件查詢證據。
- Catalog verifier: 346 fixed routes, five dynamic families, 319 registry records, 22 categories, 346 ledger rows; exact alias sets and UTF-8 Cantonese checks passed. · 目錄驗證：346 固定路線、五組動態路線、319 registry 記錄、22 分類、346 清單項；精確 alias set 同 UTF-8 粵語檢查全部通過。
- Foundation Release PE audit: the COM Descriptor Directory is zero and the import table contains no `coreclr`, `hostfxr`, or `mscoree`. This proves the current shell binary is native; it does not prove feature parity. · 基礎 Release PE 審查：COM Descriptor Directory 係零，import table 冇 `coreclr`、`hostfxr` 或 `mscoree`。呢項只證明目前 shell binary 係原生，唔代表功能已對等。

The normal-integrity live external-query gate remains **blocked**. The harness launched the exact native executable through an interactive scheduled task configured with `RunLevel=Limited`, then independently inspected the resulting process token. Windows still produced an elevated token, and verification stopped with `candidate smoke token is still elevated`; the harness therefore failed closed before running an external package-manager query. The elevated 59/59 result is not substituted for this missing normal-integrity evidence.

正常 integrity 即時外部查詢閘門仍然係**受阻**。harness 用設為 `RunLevel=Limited` 嘅互動式排程工作開啟同一個原生 executable，再獨立檢查實際 process token；Windows 仍然產生提權 token，驗證以 `candidate smoke token is still elevated` 停止，所以 harness 喺執行任何外部套件管理查詢之前已經 fail closed。提權環境嘅 59/59 結果唔會頂替欠缺嘅正常 integrity 證據。

## Visual evidence disposition · 視覺證據處置

Fresh native Dashboard, All Apps, About, Package Manager (`module.packages#updates`), Check Digit (`checkdigit`), and Text to Binary (`binarytext`) capture attempts were made with the required repository driver. `CopyFromScreen` was unavailable in this desktop session. `PrintWindow(PW_RENDERFULLCONTENT)` returned a title bar but a blank/near-uniform WinUI client frame, so the improved driver rejected it with:

已經用指定 repo driver 重新嘗試擷取原生 Dashboard、所有 app、About、套件管理（`module.packages#updates`）、檢查碼（`checkdigit`）同文字轉二進位（`binarytext`）。呢個 desktop session 用唔到 `CopyFromScreen`；`PrintWindow(PW_RENDERFULLCONTENT)` 雖然有 title bar，但 WinUI client frame 係空白／接近單色，所以改良後 driver 拒絕咗：

```text
CopyFromScreen is unavailable and the PrintWindow fallback produced a blank or
near-uniform WinUI client frame; graphics capture is unavailable in this desktop session.
```

No blank, stale, synthetic, or managed-app image was retained or substituted. UI Automation independently found the bilingual navigation tree, page titles, migration InfoBar, buttons, All Apps list, the Package Manager nine-view/11-manager read-only surface, live Check Digit controls/results, and the Text to Binary conversion controls/results. That is launch/behavior evidence only—not a visual pass. Dashboard, All Apps, About, native Package Manager, native Check Digit, and Text to Binary remain `capture-blocked` until a real composited frame can be captured and inspected.

冇保留或者頂替任何空白、舊、合成、或者受控版圖片。UI Automation 另外搵到雙語導覽樹、page title、遷移 InfoBar、按鈕、所有 app 清單、套件管理九 view／11 manager 唯讀介面、即時檢查碼 controls／結果，同文字轉二進位轉換 controls／結果。呢啲只係 launch／behavior 證據，唔係 visual pass。Dashboard、所有 app、About、原生套件管理、原生檢查碼同文字轉二進位要等真正 composited frame 擷取兼檢查到，先可以解除 `capture-blocked`。

## Safety and compatibility gates · 安全同相容閘門

The native port must preserve DPAPI secret compatibility, atomic persistence, normal-integrity-only update/install paths, fail-closed elevation gates, protocol compatibility, and every reactor invariant: cold-shutdown boot, default-off real shutdown, abortability, reversible real-world effects, and waste disk/cap limits.

原生移植一定要保留 DPAPI secret 相容、原子式 persistence、只限正常 integrity 嘅更新／安裝路徑、fail-closed 提權 gate、protocol 相容，同埋所有反應堆 invariant：cold-shutdown 開機、真實關機預設關閉、可以取消、真實副作用可還原、廢料磁碟／容量上限。

## Cutover definition · 正式切換定義

The rewrite is complete only when every route, feature, control, service, companion, external launcher, updater, installer, SDK protocol, test project, document, wiki page, GitHub Pages mirror, and changed screenshot has passing evidence. Final binary inspection must show no `coreclr`, `hostfxr`, managed assembly, C++/CLI metadata, managed subprocess, or wrapper dependency. Only then may the managed application be retired.

只有每條路線、每項功能、每個 control、service、companion、外部 launcher、updater、installer、SDK protocol、測試專案、文件、wiki、GitHub Pages 鏡像同改過頁面嘅截圖全部有通過證據，先叫完成。最後 binary inspection 必須證明冇 `coreclr`、`hostfxr`、受控 assembly、C++/CLI metadata、受控 subprocess 或 wrapper 相依；到嗰陣先可以退役受控 app。

Canonical machine-readable evidence: [`docs/cpp-port-parity.json`](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json). Wiki overview: [`docs/wiki/Native-Cpp-Rewrite.md`](https://github.com/codingmachineedge/WinForge/blob/main/docs/wiki/Native-Cpp-Rewrite.md).

權威 machine-readable 證據：[`docs/cpp-port-parity.json`](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json)。Wiki 概覽：[`docs/wiki/Native-Cpp-Rewrite.md`](https://github.com/codingmachineedge/WinForge/blob/main/docs/wiki/Native-Cpp-Rewrite.md)。
