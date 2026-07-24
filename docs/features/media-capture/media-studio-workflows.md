# Guided Media studio workflows · 引導式 Media 工作流程

## Behavior · 行為

Open `WinForge.exe --page media`, choose a workflow input, then expand the relevant card. The Media page now exposes eleven previously missing production workflows:

用 `WinForge.exe --page media` 開啟 Media，揀工作流程輸入，再展開相應卡片。Media 而家提供原本欠缺嘅 11 個正式工作流程：

1. Measured two-pass EBU R128 normalization. Pass one parses ffmpeg's loudnorm JSON; pass two supplies all four measured values with linear mode. · 量度式兩步 EBU R128：第一步解析 loudnorm JSON，第二步帶齊四個 measured 值同 linear mode。
2. Audio-only `silenceremove` for leading, trailing, and internal silence; video containers are rejected so gap collapse cannot desynchronize picture and sound. · 只限音訊嘅 `silenceremove` 會剪走頭尾同中間靜音；影片 container 會被拒絕，避免畫面同聲音甩 sync。
3. `vidstabdetect` followed by verified-transform `vidstabtransform`. · 先 `vidstabdetect`，確認 transform 檔後再 `vidstabtransform`。
4. `cropdetect` sampling followed by a second encode using the final valid crop rectangle. · 用 `cropdetect` 抽樣，再用最後有效 crop rectangle 做第二步編碼。
5. Ordered multi-clip concat-demuxer join with `-c copy`. · 按次序用 concat demuxer 同 `-c copy` 合併多段影片。
6. NVENC discovery that requires both an encoder-list match and a real one-frame hardware probe; supported choices are `h264_nvenc`, `hevc_nvenc`, and `av1_nvenc`. · NVENC 偵測要 encoder 清單同真實一格硬件測試都通過；支援三款 NVENC codec。
7. Duration-aware two-pass x264 encoding to a target MiB cap with a separate audio-kbps input. · 先讀時長，再用兩步 x264 壓到目標 MiB，音訊 kbps 獨立設定。
8. SRT/ASS libass burn-in or toggleable soft mux; MP4 uses `mov_text` and soft tracks carry `language=yue`. · SRT／ASS 可用 libass 燒入或者軟掛；MP4 用 `mov_text`，軟字幕標示 `language=yue`。
9. ffprobe chapter extraction and bounded stream-copy splitting. · 用 ffprobe 抽章節，再有限量咁無重編碼逐章分割。
10. Bounded folder conversion of HEIC/HEIF/JPEG-XL to JPG or PNG. · 有上限咁批次將 HEIC／HEIF／JPEG-XL 轉 JPG 或 PNG。
11. EXIF/GPS/XMP/container metadata removal using stream copy. · 用 stream copy 移除 EXIF／GPS／XMP／container metadata。

## Configuration and outputs · 設定同輸出

- Single-input workflows derive a labelled output beside the source (for example `.stabilized.mp4` or `.metadata-clean.jpg`). Concat asks for an explicit destination; chapter and photo batches ask for output folders. · 單一輸入流程會喺來源旁邊產生有標記輸出；concat 會要求另存路徑，章節／相片批次會要求輸出資料夾。
- Target-size accepts 1–100,000 MiB and 32–1,536 kbps audio. The calculated video bitrate is accepted only from 100–200,000 kbps. · 目標容量接受 1–100,000 MiB，音訊接受 32–1,536 kbps；計算出嚟嘅影片位元率只接受 100–200,000 kbps。
- NVENC CQ accepts 0–51. A listed encoder is not considered usable until the live hardware probe succeeds. · NVENC CQ 接受 0–51；淨係喺清單出現唔算可用，要即時硬件測試成功先得。
- Photo batches are top-directory only, limited to 500 inputs, and require a separate output folder. Chapter splitting is limited to 200 valid intervals. · 相片批次只睇來源資料夾第一層，最多 500 張，而且輸出資料夾一定要分開；章節分割最多 200 段有效 interval。

## Safety and privacy · 安全同私隱

- Every external invocation uses `ProcessStartInfo.ArgumentList`; file paths never pass through a command shell. Filter-only path syntax and concat-list syntax receive their own escaping. · 所有外部程序都用 `ProcessStartInfo.ArgumentList`，檔案路徑唔會經 command shell；filter 同 concat list 亦有各自 escaping。
- ffmpeg writes to a unique staged file in the destination folder. WinForge promotes it only after exit success and output existence checks, so a failed or cancelled run does not overwrite an existing destination. · ffmpeg 先寫目的地同資料夾嘅唯一暫存檔；成功退出兼確認檔案存在先升格，所以失敗／取消唔會覆寫原有目的地。
- vidstab transforms, concat lists, and x264 passlogs live in GUID-scoped owned workspaces below the WinForge temp root and are removed in `finally`. Cancellation terminates the child through the shared bounded runner. · vidstab transform、concat list 同 x264 passlog 放喺 WinForge temp root 下嘅 GUID 自家 workspace，`finally` 會清理；取消會經共享有界 runner 終止子程序。
- Patterns, subtitles, media, chapter metadata, and photos remain local. WinForge does not upload them. · pattern、字幕、媒體、章節 metadata 同相片全部留喺本機，WinForge 唔會上傳。

## Failure modes · 失敗模式

- Concat stream copy requires matching codecs, time bases, and stream parameters. Incompatible clips fail truthfully; WinForge does not silently re-encode them. · concat 無重編碼要求 codec、time base 同 stream 參數相容；唔相容會如實失敗，唔會靜靜重編碼。
- NVENC can be compiled into ffmpeg but unavailable because the GPU or driver is missing/unsupported. The live probe keeps those codecs out of the picker. · ffmpeg 可能編入 NVENC，但 GPU／driver 唔支援；即時測試會將呢啲 codec 排除。
- Cropdetect can return no stable rectangle, media can contain no chapters, and an extremely small target cap can produce an invalid bitrate. These stop before output promotion. · cropdetect 可能搵唔到穩定矩形、檔案可能冇章節、容量太細亦可能算出無效 bitrate；全部都會喺升格輸出前停止。
- Metadata stream copy removes container/stream metadata but intentionally does not recompress pixels; codec-specific data embedded inside pixel payloads is outside this contract. · metadata stream copy 唔會重壓像素；如果 codec 將資料藏喺 pixel payload，唔屬於呢個合約。

## Accessibility and localization · 無障礙同本地化

The four workflow cards are vertically stacked, controls stretch to the content width, buttons have at least 40-pixel height, and long English/Cantonese labels wrap instead of forcing horizontal scrolling. Controls expose bilingual automation names; language changes use the page's named subscribe/unsubscribe lifecycle.

四張工作流程卡直向排列，控制會伸展到內容闊度，按鈕最少 40px 高，長英文／粵語標籤會換行而唔係逼出橫向捲動。控制有雙語 automation name；語言切換沿用 page 嘅具名訂閱／取消訂閱 lifecycle。

## Verification · 驗證

- `dotnet run --project tests/MediaWorkflowCore.Tests -c Debug` — 17/17 deterministic cases cover every workflow plus parser, path escaping, failed-output preservation, cancellation, and scratch cleanup.
- `dotnet build WinForge.csproj -c Debug -p:Platform=x64 --no-restore` — exit 0, zero errors on the changed WinUI surface.
- XAML literal safety and source-surface audits — 2,913/2,913 handlers and 1,957/1,957 direct actions resolved, zero language-lifecycle mismatch.
- Fresh inspected process-owned capture: `docs/screenshot-media.png`, SHA-256 `F89886CE200DA522E8C956B67B363A847E8E9DC0AC2926DFF382E9E52B870900`.

LowLevel MCP headless tools were not callable in this session, so the repository driver used its DEBUG-only live WinUI visual-tree capture. No raw desktop capture was used or claimed. · 今次 session 冇可呼叫嘅 LowLevel MCP headless 工具，所以用 repo driver 嘅 DEBUG live WinUI visual-tree 擷取；冇用亦冇聲稱用 raw desktop capture。
