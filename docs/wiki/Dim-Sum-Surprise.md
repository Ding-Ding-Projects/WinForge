# Dim-sum startup surprise · 啟動點心驚喜

WinForge makes one fresh 10% draw on each eligible launch and, when the selected public release
asset is available, shows its exact catalog English and Traditional Chinese name plus alt text in a
non-blocking auto-dismissing notification. Metadata comes from the public catalog; the PNG is cached
only under WinForge application data. · WinForge 每次合資格啟動重新抽一次 10%，如果揀中嘅 public
release asset 可用，就用非阻塞自動消失通知顯示 catalog 準確英文／繁體中文名同 alt text。metadata
來自 public catalog；PNG 只會 cache 喺 WinForge application data。

School mode, first-run terms, deep-link launches, malformed catalog data, unavailable images, and
invalid PNG signatures suppress the notice without blocking startup. tests/DimSumSurprise.Tests
covers the pure selection and validation rules (6/6). · School mode、首次 terms、deep-link 啟動、格式
錯 catalog、圖片唔得或者 PNG signature 錯都會抑制通知，但唔會阻塞啟動。tests/DimSumSurprise.Tests
覆蓋純 selection 同 validation 規則（6/6）。
