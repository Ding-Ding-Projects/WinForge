# Dim-sum startup surprise · 啟動點心驚喜

## Behavior · 行為

On each eligible launch WinForge makes one fresh random draw with an exact 10% threshold. A
successful draw chooses one dish whose image filename is present in the verified public
catalog-v1-part-003 release asset list, reads the bilingual name and alt text from the public
catalog, and shows the result through the non-blocking notification host. The notice auto-dismisses,
never takes focus, and does not delay the main window. · 每次合資格啟動 WinForge 會重新抽一次，準確
用 10% threshold。抽中後只會揀已喺驗證過嘅 public catalog-v1-part-003 release asset list 入面
有圖檔名嘅菜式，再由 public catalog 讀雙語名稱同 alt text，經非阻塞通知 host 顯示。通知會自動
消失，唔搶 focus，亦唔會拖慢主視窗開啟。

## Source and cache · 來源同 cache

The authoritative metadata source is
https://raw.githubusercontent.com/Ding-Ding-Projects/dim-sum-photos/main/catalog/index.json.
Published images are resolved only from
https://github.com/Ding-Ding-Projects/dim-sum-photos/releases/download/catalog-v1-part-003/.
The downloaded catalog and selected PNG stay under the user's WinForge application-data cache;
no image is tracked in this repository and no release asset is copied. · 準確 metadata 來源係上面
嘅 public catalog URL；圖片只會由上面嘅 published release asset URL 解決。下載嘅 catalog 同揀中
PNG 只留喺使用者 WinForge application-data cache；repository 唔會 track 圖片，release 亦唔會
copy 圖片。

The request path is HTTPS-only, bounded to the two public hosts, rejects redirects, caps catalog and
image sizes, validates the PNG signature, and writes through a temporary file before promotion.
Malformed or unavailable source data fails safe by leaving the application usable without a notice.
· request path 只准 HTTPS、只准兩個 public host、拒絕 redirect、有 catalog／圖片大小上限、驗證 PNG
signature，同埋先寫 temporary file 再升格。來源格式錯或者唔得時會安全唔顯示通知，但 app 照常可用。

School mode and first-run terms suppress the draw. A command-line deep-link launch is also excluded
so the surprise never interrupts an automation or an in-progress task. · School mode 同首次 terms 會
抑制抽獎；command-line deep-link launch 亦會排除，避免驚喜打斷 automation 或進行中工作。

## Verification · 驗證

tests/DimSumSurprise.Tests/Program.cs covers the exact 10% threshold, authoritative bilingual
metadata, published-asset filtering, deterministic selection, malformed-catalog fallback, and PNG
signature checking (6/6). The full x64 solution build verifies the notification image fields and
startup wiring with zero warnings and errors. · tests/DimSumSurprise.Tests/Program.cs 覆蓋準確 10%
threshold、準確雙語 metadata、published asset filtering、deterministic selection、malformed catalog
fallback，同 PNG signature（6/6）。完整 x64 solution build 以零 warnings 同 errors 驗證通知圖片
欄位同 startup wiring。

## Built-artifact evidence · 真實建置證據

A dedicated dim-sum capture remains pending because the 10% draw is intentionally nondeterministic
and the first successful cache fill must come from the public release asset at runtime. The source
test and the normal notification-host captures are kept separate from that pending visual proof.
· 專門點心擷取仍然待做，因為 10% 抽獎係刻意 nondeterministic，而第一次成功 cache fill 必須喺
runtime 由 public release asset 得到。source test 同普通 notification-host 擷取會同未完成嘅
visual proof 分開記錄。

## Suggested articles · 建議文章

- [Shared settings](shared-settings.md) — School mode suppression and language behavior.
- [Notification centre](../application-shell/notification-centre.md) — the non-blocking host and local history.
- [Offline documentation](offline-documentation.md) — the other local-first content surface.
