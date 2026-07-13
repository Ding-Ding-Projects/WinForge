# Native C++ Rewrite · 原生 C++ 重寫

> Status on 2026-07-13: the first native foundation batch is runnable and tested, but the whole-product rewrite is **not complete**. The managed WinUI 3 application remains the shipping behavioral reference until every ledger row passes. · 2026-07-13 狀態：第一批原生基礎已經可以執行同測試，但全產品重寫**未完成**。每一項清單全部通過之前，受控 WinUI 3 app 仍然係發佈同功能行為基準。

## Objective · 目標

WinForge is being rewritten as a genuine C++20/C++/WinRT WinUI 3 application. The final executable must not use C++/CLI, host the CLR, wrap the C# executable, communicate with a managed clone over IPC, or start the managed app as a subprocess. Existing C# and XAML stay in the repository only as the migration oracle until native parity and cutover are proven.

WinForge 正在重寫成真正嘅 C++20／C++/WinRT WinUI 3 app。最終 executable 唔可以用 C++/CLI、host CLR、包住 C# executable、用 IPC 駁住受控 clone，亦唔可以用 subprocess 開受控 app。現有 C# 同 XAML 喺遷移期間只會留低做行為基準，直到原生對等同正式切換有證據證實。

## Corrected coverage contract · 修正後覆蓋合約

The old 323-route smoke manifest omitted runtime-built category routes and Settings. The canonical native inventory has **346 fixed routes**, **five dynamic route families**, **805 deep-link aliases**, 319 registry records, and 22 runtime category routes.

舊 323-route 冒煙清單漏咗執行時建立嘅分類路線同 Settings。權威原生清單有 **346 條固定路線**、**五組動態路線**、**805 個深層連結別名**、319 個 registry 記錄同 22 條執行時分類路線。

The dynamic families are `search:<query>`, `manual:<fragment>`, `module.<id>#<fragment>`, `module.packages#(discover|updates|installed)`, and `weblogin?url=<uri>`.

## Foundation delivered · 已完成基礎

- Self-contained, unpackaged x64 C++/WinRT WinUI 3 executable in `WinForge.Native.sln`. · `WinForge.Native.sln` 內嘅自包含、免封裝 x64 C++/WinRT WinUI 3 executable。
- Standard C++ routing/localization, generated UTF-8 bilingual catalog, Dashboard, About, All Apps, language modes, search, and deep links. · 標準 C++ routing／本地化、UTF-8 雙語目錄、Dashboard、About、所有 app、語言模式、搜尋同深層連結。
- Honest pending pages for unported features; resolving a route does not count as a port. · 未移植功能會顯示 pending 頁；路線開到唔代表已移植。
- Native parity ledger plus process-owned unit and UI Automation smoke harnesses. · 原生對等清單，同只控制自己 process 嘅單元／UI Automation 冒煙測試。

Package Manager is now an honest **in-progress** native port. All nine views and 11 manager filters render; Discover, Updates, and Installed provide read-only result queries, while Sources runs a read-only command probe whose raw configuration and diagnostics are withheld until manager-specific secret redaction is proven. Every mutation plus Bundles, Ignored, Setup bootstrap, Settings, and the real Operations coordinator remains gated. The complete pinned UniGetUI snapshot is provenance and a behavior inventory, not parity or an embedded implementation. · 套件管理而家係如實標示為**進行中**嘅原生移植。全部九個檢視同 11 個管理器篩選都會顯示；Discover、Updates 同 Installed 提供只讀結果查詢，Sources 就執行只讀指令探測，但逐管理器機密遮罩未證實之前會隱藏原始設定同診斷。所有修改操作、Bundles、Ignored、Setup bootstrap、Settings 同真正 Operations 協調器仍然鎖住。完整固定 UniGetUI snapshot 只係來源依據同行為清單，唔係對等或者內嵌實作。

Check Digit Validator is the next genuine native utility slice. Its standard C++ core and native WinUI surface implement live Luhn/card-brand detection, ISBN-10/13, EAN-13, UPC-A, and bounded incremental mod-97 IBAN validation. ISBN-13 requires 978/979; IBAN enforces all 89 SWIFT Release 102 country prefixes, exact lengths, and BBAN character classes before the checksum. `checkdigit`, `luhn`, and `module.checkdigit` all reach the real page; language rerenders preserve state, while localized names, stale-detail clearing, and polite live-region events harden accessibility. Functional evidence passes without CLR or external side effects, but the ledger row remains **in progress** because fresh visual capture is `capture-blocked`. · 檢查碼驗證器係下一批真正原生 utility。標準 C++ core 同原生 WinUI 介面實作即時 Luhn／卡種識別、ISBN-10／13、EAN-13、UPC-A 同有界增量 mod-97 IBAN 驗證。ISBN-13 只接受 978／979；IBAN 會喺 checksum 之前驗證 SWIFT Release 102 全部 89 個國家 prefix、固定長度同 BBAN 字元類別。`checkdigit`、`luhn` 同 `module.checkdigit` 都會開真正頁面；轉語言時會保留狀態，亦加咗本地化名稱、清除舊 detail 同 polite live-region event。功能證據通過，唔用 CLR 或外部副作用，但最新視覺擷取係 `capture-blocked`，所以清單仍然標示**進行中**。

## Verification · 驗證

```powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Native -Publish -Page dashboard -NoCapture
tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe
dotnet run --project tests\CheckDigitCore.Tests\CheckDigitCore.Tests.csproj -c Release
powershell -ExecutionPolicy Bypass -File eng\native\Test-NativeCatalogParity.ps1
powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1
```

- Debug and Release x64 builds: **0 errors**. · Debug 同 Release x64 建置：**0 errors**。
- Legacy managed solution compile check: **0 errors** with ignored NuGet restore and native C++ source/generated trees excluded from the SDK item glob. · 舊受控 solution compile check：**0 errors**；SDK item glob 已排除忽略咗嘅 NuGet restore 同原生 C++ source／generated tree。
- Native core regressions: **208/208 in Debug and 208/208 in Release**, including 48 focused Check Digit cases, official Discover range boundaries, exact independent SWIFT Release 102 fixture parity, Unicode-confusable rejection, and all existing Package Manager/shell coverage; managed-oracle parity passes **24/24**. · 原生核心回歸：Debug **208/208**、Release **208/208**，包括 48 個檢查碼專項案例、官方 Discover range 邊界、獨立 SWIFT Release 102 fixture 精確對等、拒絕 Unicode 混淆字元，同全部原有套件管理／shell 覆蓋；受控基準對等 **24/24** 通過。
- Elevated process-owned shell smoke: **46/46**, including all six Check Digit schemes, accessibility cleanup, state-preserving localization, every Check Digit alias, and exact Discover/Updates/Installed alias selection. This verifies UI and fail-closed safety behavior, not normal-integrity external queries. · 提權、自有 process shell 冒煙：**46/46**，包括六個檢查碼格式、無障礙清理、轉語言保留狀態、全部檢查碼 alias，同準確揀返 Discover／Updates／Installed alias；呢項驗證 UI 同 fail-closed 安全行為，唔代表正常 integrity 外部查詢。
- Catalog parity: 346 fixed routes + five dynamic families passed exact id, alias, count, and UTF-8 bilingual checks. · 目錄對等：346 固定路線 + 五組動態路線通過精確 id、alias、數量同 UTF-8 雙語檢查。
- Foundation Release PE audit: zero COM descriptor and no `coreclr`, `hostfxr`, or `mscoree` import; this is native-binary evidence, not feature-parity evidence. · 基礎 Release PE 審查：COM descriptor 係零，而且冇 `coreclr`、`hostfxr` 或 `mscoree` import；呢項係原生 binary 證據，唔係功能對等證據。

Normal-integrity live external-query evidence remains **blocked**. Even an interactive scheduled task configured with `RunLevel=Limited` received an elevated Windows token, so the verifier stopped at `candidate smoke token is still elevated` before running a package-manager query. No live-query pass is claimed. · 正常 integrity 即時外部查詢證據仍然**受阻**。即使互動式排程工作設為 `RunLevel=Limited`，Windows 仍然回傳提權 token，所以 verifier 喺執行套件管理查詢之前以 `candidate smoke token is still elevated` 停止；唔會聲稱 live query 已通過。

## Screenshot status · 截圖狀態

Fresh native Dashboard, All Apps, About, Package Manager (`module.packages#updates`), and Check Digit (`checkdigit`) captures are blocked in this desktop session. `CopyFromScreen` is unavailable and `PrintWindow` returns a blank/near-uniform WinUI client frame, which the driver now rejects. No blank, stale, synthetic, or managed screenshot was substituted. UI Automation proves launch and behavior, not visual appearance; all five surfaces remain `capture-blocked`.

呢個 desktop session 擷取唔到最新原生 Dashboard、所有 app、About、套件管理（`module.packages#updates`）同檢查碼（`checkdigit`）畫面。`CopyFromScreen` 用唔到，而 `PrintWindow` 只回傳空白／接近單色 WinUI client frame，driver 而家會拒絕呢種圖。冇用空白、舊、合成或者受控版截圖頂替。UI Automation 只證明 launch 同 behavior，唔代表視覺通過；五個介面仍然係 `capture-blocked`。

## Cutover gate · 切換閘門

Every feature, service, companion, launcher, updater, installer, protocol, test, document, wiki/Page mirror, and changed screenshot must pass. Final binary inspection must prove there is no CLR, C++/CLI, managed assembly, managed subprocess, IPC wrapper, or C# executable dependency. · 每項功能、service、companion、launcher、updater、installer、protocol、測試、文件、wiki／Pages 鏡像同改過嘅截圖都要通過；最後 binary inspection 必須證明冇 CLR、C++/CLI、受控 assembly、受控 subprocess、IPC wrapper 或 C# executable 相依。

See the [full repository record](https://github.com/codingmachineedge/WinForge/blob/main/docs/Native-Cpp-Rewrite.md) and [machine-readable parity ledger](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json). · 詳情請睇[完整 repo 記錄](https://github.com/codingmachineedge/WinForge/blob/main/docs/Native-Cpp-Rewrite.md)同[machine-readable 對等清單](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json)。
