# Portable package bundles · 可攜套件清單

## Behavior · 行為

The Bundles workspace imports and exports JSON, `.ubundle`, YAML, and XML package sets. It preserves the package manager, package ID, version, source, and explicit per-package options. Manager-wide, all-update, selected-row, and imported-bundle mutations all pass through the shared package-operation coordinator so queueing, cancellation, history, source identity, option policy, and output redaction stay consistent. · Bundles 工作區可以匯入／匯出 JSON、`.ubundle`、YAML 同 XML 套件清單，保留管理器、套件 ID、版本、來源同明確逐套件選項。逐管理器、全部更新、所選列同匯入清單操作全部經共用操作協調器，確保排隊、取消、歷史、來源 identity、選項政策同輸出遮蔽一致。

Bundle saves are fail-aware and staged beside the destination. WinForge writes the complete payload to a uniquely named temporary file, atomically replaces an existing destination or moves the completed file into place, and reports success only after that swap succeeds. A failed save leaves the previous destination unchanged and keeps an editable workspace dirty. · 儲存清單會先喺目的地旁邊寫完整、唯一命名嘅暫存檔，成功後先原子取代舊檔或者移入新檔。交換成功先會報成功；失敗會保留原有目的檔，同時保持工作區未儲存狀態。

## Configuration · 設定

Per-package options use `InstallOptions`; manager defaults and overrides use the normal Package Manager settings store. A bundle does not carry proxy credentials or other DPAPI-backed secrets. · 逐套件選項用 `InstallOptions`；管理器預設同 override 經正常套件設定保存。清單唔會攜帶 proxy 認證或者其他 DPAPI 保護秘密。

## Failure modes · 故障模式

- A blank destination, missing parent directory, serialization failure, staging failure, or replacement failure returns `false`; both package UIs show a bilingual, non-blocking status and do not announce a false success. · 空白目的地、欠缺上層資料夾、序列化、暫存或交換失敗都會回傳 `false`；兩個套件介面會用雙語非阻塞狀態顯示，唔會假報成功。
- Unknown managers, unsafe package IDs, and unsafe or unsupported sources become incompatible entries and are not scripted or queued. · 未知管理器、危險套件 ID、危險或不支援來源會變成不相容項目，唔會寫入指令稿或者排隊。
- Imported custom arguments, lifecycle commands, and process-kill lists remain review-first; the security report hides command bodies that may contain secrets. · 匯入嘅自訂參數、生命週期指令同終止程序清單一定要先檢視；安全報告會隱藏可能含秘密嘅指令內容。

## Accessibility and localization · 無障礙同本地化

Package-manager section headings expose semantic heading levels. Results and bundle-workspace status text are polite live regions, so completed or failed saves are announced without opening an informational modal. Search and action controls use narrow-safe rows, and manager labels plus new failure text follow the persisted English, Cantonese, or compact bilingual mode. · 套件管理分節有語意 heading level；結果同清單工作區狀態係 polite live region，儲存成功或失敗毋須彈資訊 modal 都會被讀出。搜尋同動作控制會用窄畫面安全分行；管理器名稱同新增失敗文字會跟持久英文、粵語或精簡雙語模式。

## Preserved-stash reconciliation · 已保存 stash 對帳

The preserved `codex-temp-powertoys` stash commit `181fc231c93b2533392344a405cb18750b4eaa48` was reviewed file by file against the current managed app and deliberately not applied wholesale:

| Stashed file | Disposition |
|---|---|
| `Pages/PackageManagerModule.xaml.cs` | Its coordinator-based manager/all/batch updates are already present and extended by the current operations workspace; the old hunk is redundant. |
| `Services/BundleService.cs` | Its current-schema/legacy-alias and package-reference intent is already present with source policy, bounded options, and tests. The stashed snapshot calls helper methods it does not define and drops current source-aware command construction, so its old body is rejected. |
| `Pages/PowerToysExtrasModule.xaml.cs` | The five-tab discoverability copy is already present in corrected bilingual form. The stash contains literal `` `r`n`` text in the source line, so that malformed line is rejected. |
| `README.md`, `docs/ROADMAP.md` | Current documentation already carries the expanded PowerToys hub and richer Package Manager behavior; stale counts and narrower copy are rejected. |
| `WinForge.sln` | `PackageManagerCore.Tests` is already registered along with all newer test projects; the old solution fragment is redundant. |

Useful behavior therefore stays in the newer implementation; this reconciliation adds the atomic-save reliability fix and durable evidence without mutating or dropping the preserved stash. · 有用行為全部保留喺較新實作；今次對帳新增原子儲存可靠性修正同持久證據，冇修改或者 drop 已保存 stash。

## Verification · 驗證

`dotnet run --project tests/PackageManagerCore.Tests -c Debug` passes **28/28**, including current/legacy JSON-YAML-XML option round trips, blank/local/source policy, package-reference and structured-option rejection, source-aware queue identity, cancellation/serialization/redaction, and the new create/replace/failure-preservation save contract. The solution build, XAML literal safety, and detailed source-surface audit pass. A fresh **1049×646** LowLevel headless capture verifies the responsive Package Manager layout; the canonical and wiki-local PNGs are byte-identical with SHA-256 `AE5A0CB21847BD907FE3011CE3E68AFB5965C41E35149FD8DBECBBB4B3AC0414`. · 專項測試 **28/28** 通過，涵蓋目前／舊版三種格式、來源政策、危險輸入拒絕、來源-aware queue、取消／串行／遮蔽，同新增建立／取代／失敗保留儲存合約；solution build、XAML safety 同詳細 source audit 全過。新 **1049×646** LowLevel headless capture 已驗證響應式套件管理版面，正式同 wiki-local PNG byte-identical，SHA-256 如上。
