# Dim-sum startup surprise · 啟動點心驚喜

After the first usable `RootGrid.Loaded` layout, WinForge makes one fresh 10% draw on each eligible
launch and, when the selected public release asset is available, shows its exact catalog English
and Traditional Chinese name plus alt text in a non-blocking auto-dismissing notification. Metadata
comes from the public catalog; the name-only manifest maps exact filenames to the three verified
`catalog-v1*` release partitions; PNG bytes are cached only under WinForge application data. · 第一個
可用 `RootGrid.Loaded` layout 之後，WinForge 每次合資格啟動重新抽一次 10%，如果揀中嘅 public
release asset 可用，就用非阻塞自動消失通知顯示 catalog 準確英文／繁體中文名同 alt text。metadata
來自 public catalog；只含名稱嘅 manifest 將準確檔名對應到三個已驗證 `catalog-v1*` release
partition；PNG bytes 只會 cache 喺 WinForge application data。

School mode, first-run terms, minimized/deep-link launches, active update or recovery notices,
malformed catalog data, unavailable images, and undecodable PNGs suppress the notice without
blocking startup. Notification history hides the surprise while School mode is on. The work is
started off the UI thread after layout and cache entries are digest-bound to the exact release
tag and filename. `tests/DimSumSurprise.Tests` covers the pure selection, exact-manifest, partition,
and validation rules (11/11 after the full lane run). · School mode、首次 terms、最小化／deep-link
啟動、active update 或復原通知、格式錯 catalog、圖片唔得或者 PNG decode 唔到都會抑制通知，但唔會
阻塞啟動。School mode 開住時 notification history 會隱藏驚喜。工作喺 layout 後離開 UI thread
先開始，cache entry 亦會綁定準確 release tag 同檔名 digest。`tests/DimSumSurprise.Tests` 覆蓋
純 selection、準確 manifest、partition 同 validation 規則（完整 lane run 後 11/11）。
