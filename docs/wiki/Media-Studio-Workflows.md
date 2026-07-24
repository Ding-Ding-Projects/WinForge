# Guided Media studio workflows · 引導式 Media 工作流程

WinForge's Media page now completes all 15 audited roadmap capabilities. Eleven guided, cancellable workflows join the existing GIF, trim, contact-sheet, and animated-WebP tools:

WinForge Media 頁而家完成 15 項審核能力；原有 GIF、剪裁、contact sheet 同動態 WebP 之外，再加入 11 個可取消引導流程：

- measured two-pass EBU R128 and audio-only leading/trailing/internal silence cleanup (video is rejected to prevent A/V desync); · 量度式兩步 EBU R128，同只限音訊嘅頭尾／中間靜音清理（影片會被拒絕，避免畫面聲音甩 sync）；
- two-pass vidstab, cropdetect-to-crop, and ordered concat stream copy; · 兩步 vidstab、偵測後裁黑邊、按次序無重編碼合併；
- hardware-probed H.264/HEVC/AV1 NVENC and duration-aware target-size x264; · 實測 H.264／HEVC／AV1 NVENC，同按時長計算嘅目標容量 x264；
- SRT/ASS burn-in or soft mux and ffprobe chapter read/split; · SRT／ASS 燒入／軟掛，同 ffprobe 章節讀取／分割；
- bounded HEIC/HEIF/JPEG-XL photo batches and EXIF/GPS/XMP metadata stripping. · 有上限 HEIC／HEIF／JPEG-XL 批次，同 EXIF／GPS／XMP 清理。

## Safety · 安全

File paths are passed as argument-vector entries, never through a command shell. Outputs are first written to unique same-folder staging files; only a successful exit plus output-existence check can promote them. Existing destinations survive failure/cancellation. Batches are capped at 500, chapters at 200, and owned transform/list/passlog workspaces are deleted in `finally`. · 檔案路徑用 argument vector，唔經 command shell；輸出先寫唯一暫存檔，成功兼確認存在先升格；失敗／取消保留舊檔；批次 500、章節 200 上限，自家 scratch workspace 一定清理。

## Verification · 驗證

- Focused workflow harness: **17/17**.
- Changed WinUI build: exit 0, zero errors.
- XAML/source gates: **2,913/2,913 handlers**, **1,957/1,957 direct actions**, zero lifecycle mismatches.
- Fresh inspected process-owned capture: `docs/screenshot-media.png`, SHA-256 `F89886CE200DA522E8C956B67B363A847E8E9DC0AC2926DFF382E9E52B870900`.

LowLevel MCP headless tools were not callable in this session; the screenshot is the repository driver's DEBUG live WinUI visual-tree capture, not a raw desktop image. Full configuration, failure modes, and security details are in [`docs/features/media-capture/media-studio-workflows.md`](https://github.com/Ding-Ding-Projects/WinForge/blob/main/docs/features/media-capture/media-studio-workflows.md). · 今次 LowLevel 工具不可呼叫，圖片係 repo driver 嘅 DEBUG live visual-tree 擷取；完整設定／失敗模式／安全資料請睇 repository guide。

[← Media & Capture](Media-and-Capture.md) · [Media](Media.md)
