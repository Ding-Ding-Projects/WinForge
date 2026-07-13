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

Package Manager and its three fragments are catalogued but its native behavior remains pending. The pinned UniGetUI snapshot is provenance, not a completed native implementation. · 套件管理同三個 fragment 已入目錄，但原生功能仍然未完成；固定 UniGetUI snapshot 係來源證明，唔係完成咗嘅原生實作。

## Verification · 驗證

```powershell
powershell -ExecutionPolicy Bypass -File .agents\skills\run-winforge\driver.ps1 -Native -Publish -Page dashboard -NoCapture
tests\native\WinForge.Core.Tests\bin\x64\Debug\WinForge.Core.Tests.exe
powershell -ExecutionPolicy Bypass -File eng\native\Test-NativeCatalogParity.ps1
powershell -ExecutionPolicy Bypass -File eng\native\Invoke-NativeShellSmoke.ps1
```

- Debug and Release x64 builds: **0 errors**. · Debug 同 Release x64 建置：**0 errors**。
- Core regressions: **21 passed, 0 failed**. · 核心回歸：**21 passed, 0 failed**。
- Live shell smoke: **20 passed, 0 failed**. · 即時 shell 冒煙：**20 passed, 0 failed**。
- Catalog parity: 346 fixed routes + five dynamic families passed exact id, alias, count, and UTF-8 bilingual checks. · 目錄對等：346 固定路線 + 五組動態路線通過精確 id、alias、數量同 UTF-8 雙語檢查。
- Foundation Release PE audit: zero COM descriptor and no `coreclr`, `hostfxr`, or `mscoree` import; this is native-binary evidence, not feature-parity evidence. · 基礎 Release PE 審查：COM descriptor 係零，而且冇 `coreclr`、`hostfxr` 或 `mscoree` import；呢項係原生 binary 證據，唔係功能對等證據。

## Screenshot status · 截圖狀態

Fresh native captures are blocked in this desktop session. `CopyFromScreen` is unavailable and `PrintWindow` returns a blank/near-uniform WinUI client frame, which the driver now rejects. No blank, stale, synthetic, or managed screenshot was substituted. UI Automation proves launch and behavior, not visual appearance; Dashboard, All Apps, and About remain `capture-blocked`.

呢個 desktop session 擷取唔到最新原生畫面。`CopyFromScreen` 用唔到，而 `PrintWindow` 只回傳空白／接近單色 WinUI client frame，driver 而家會拒絕呢種圖。冇用空白、舊、合成或者受控版截圖頂替。UI Automation 只證明 launch 同 behavior，唔代表視覺通過；Dashboard、所有 app 同 About 仍然係 `capture-blocked`。

## Cutover gate · 切換閘門

Every feature, service, companion, launcher, updater, installer, protocol, test, document, wiki/Page mirror, and changed screenshot must pass. Final binary inspection must prove there is no CLR, C++/CLI, managed assembly, managed subprocess, IPC wrapper, or C# executable dependency. · 每項功能、service、companion、launcher、updater、installer、protocol、測試、文件、wiki／Pages 鏡像同改過嘅截圖都要通過；最後 binary inspection 必須證明冇 CLR、C++/CLI、受控 assembly、受控 subprocess、IPC wrapper 或 C# executable 相依。

See the [full repository record](https://github.com/codingmachineedge/WinForge/blob/main/docs/Native-Cpp-Rewrite.md) and [machine-readable parity ledger](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json). · 詳情請睇[完整 repo 記錄](https://github.com/codingmachineedge/WinForge/blob/main/docs/Native-Cpp-Rewrite.md)同[machine-readable 對等清單](https://github.com/codingmachineedge/WinForge/blob/main/docs/cpp-port-parity.json)。
