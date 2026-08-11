# Dim-sum startup surprise · 啟動點心驚喜

## Behavior · 行為

On each eligible launch WinForge makes one fresh random draw with an exact 10% threshold after
the first usable `RootGrid.Loaded` layout. A successful draw chooses one dish whose image filename
resolves to a verified public `catalog-v1*` release partition, reads the bilingual name and alt
text from the public catalog, and shows the result through the non-blocking notification host. The
notice auto-dismisses, never takes focus, and does not delay the main window. · 每次合資格啟動
WinForge 會喺第一個可用 `RootGrid.Loaded` layout 之後重新抽一次，準確用 10% threshold。抽中後只
會揀可以解析到已驗證 public `catalog-v1*` release partition 嘅圖檔名，再由 public catalog 讀
雙語名稱同 alt text，經非阻塞通知 host 顯示。通知會自動消失，唔搶 focus，亦唔會拖慢主視窗開啟。

## Source and cache · 來源同 cache

The authoritative metadata source is
https://raw.githubusercontent.com/Ding-Ding-Projects/dim-sum-photos/main/catalog/index.json.
Published images are resolved only through an exact name-only manifest generated from the verified
public release inventories: `catalog-v1` (995 assets), `catalog-v1-part-002` (990), and
`catalog-v1-part-003` (943). The manifest contains no catalog fields or image bytes. The downloaded
catalog is digest-recorded, and selected PNGs are cached under an identity derived from the exact
release tag and asset filename. · 準確 metadata 來源係上面嘅 public catalog URL；圖片只會經由已驗證
public release inventory 生成嘅準確檔名 manifest 解決：`catalog-v1`（995 張）、`catalog-v1-part-002`
（990 張）同 `catalog-v1-part-003`（943 張）。manifest 唔含 catalog 欄位或者圖片 bytes。下載嘅
catalog 會記錄 digest，揀中 PNG 就用準確 release tag 同 asset 檔名組成 identity cache。repository
唔會保存 catalog 或圖片副本；release 亦唔會 copy 圖片。

The request path is HTTPS-only, bounded to the public catalog, GitHub release, and one exact
`release-assets.githubusercontent.com` redirect target, with no credentials or unapproved redirect
chain. Catalog and image sizes are capped, PNG bytes are decoded through the platform bitmap decoder,
and files are written through a temporary path before promotion. Malformed or unavailable source
data fails safe by leaving the application usable without a notice. · request path 只准 HTTPS，public
catalog、GitHub release 同一個準確 `release-assets.githubusercontent.com` redirect target 有界，唔准
credential 或未批准 redirect chain。catalog／圖片有大小上限，PNG bytes 會用 platform bitmap decoder
真正 decode，文件亦會先寫 temporary path 再升格。來源格式錯或者唔得時會安全唔顯示通知，但 app
照常可用。

School mode, first-run terms, minimized/tray launches, and command-line deep-link launches suppress
the draw. Before publication the service also suppresses itself when a progress, warning, or error
notification is active, covering update handoff and visible recovery work; the UI rechecks that
condition immediately before publishing. · School mode、首次 terms、最小化／tray 啟動同
command-line deep-link launch 都會抑制抽獎。通知發佈前如果有 progress、warning 或 error notification
存在，亦會停止驚喜，涵蓋 update handoff 同可見復原工作；UI 真正發佈前會再檢查一次。

## Verification · 驗證

tests/DimSumSurprise.Tests/Program.cs covers the exact 10% threshold, authoritative bilingual
metadata, exact published-asset filtering, deterministic selection, malformed-catalog fallback, PNG
signature checking, partition routing, fake-suffix rejection, and selection beyond 512 entries
(11/11). The full x64 solution build verifies the notification image fields, first-usable-layout
startup wiring, and digest/decoder source wiring with zero warnings and errors. ·
tests/DimSumSurprise.Tests/Program.cs 覆蓋準確 10% threshold、準確雙語 metadata、準確 published asset
filtering、deterministic selection、malformed catalog fallback、PNG signature、partition routing、
fake suffix 拒絕，同超過 512 項嘅 selection（11/11）。完整 x64 solution build 以零 warnings 同 errors
驗證通知圖片欄位、第一個可用 layout startup wiring，同 digest／decoder source wiring。

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
