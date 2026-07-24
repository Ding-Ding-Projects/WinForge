# Media · 媒體

**EN —** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**粵語 —** 呢份功能參考由 WinForge 模組登記、導覽地圖同頁面 XAML 生成。

| Field · 欄位 | Value · 值 |
|---|---|
| Tag · 標籤 | <code>module.media</code> |
| Deep-link alias · 深層連結別名 | <code>media</code> |
| Category · 分類 | Media & Capture · 媒體與擷取 |
| Page class · 頁面類別 | <code>MediaModule</code> |
| Page XAML · 頁面 XAML | <code>Pages/MediaModule.xaml</code> |
| Button docs · 按鈕文件 | 26 |

## What It Covers · 功能範圍

**EN —** Media is registered in WinForge search and navigation with these keywords: <code>ffmpeg ffprobe video audio photo convert trim gif ebu r128 loudness silence vidstab stabilize crop black bars concat join nvenc target size subtitle srt ass chapter heic heif jxl exif gps metadata 影片 音訊 相片 轉檔 響度 靜音 防震 黑邊 合併 字幕 章節 私隱</code>.

**粵語 —** 媒體 已登記喺 WinForge 搜尋同導覽，關鍵字包括：<code>ffmpeg ffprobe video audio photo convert trim gif ebu r128 loudness silence vidstab stabilize crop black bars concat join nvenc target size subtitle srt ass chapter heic heif jxl exif gps metadata 影片 音訊 相片 轉檔 響度 靜音 防震 黑邊 合併 字幕 章節 私隱</code>。

## Buttons And Controls · 按鈕與控制項

| Button · 按鈕 | Type · 類型 | XAML name · 名稱 | Handler · 處理函式 |
|---|---|---|---|
| [Choose the media workflow input file · 揀媒體工作流程輸入檔](../../buttons/media-capture/media/001-studiopickinputbtn.md) | `Button` | `StudioPickInputBtn` | `StudioPickInput_Click` |
| [Cancel active media workflow · 取消進行中媒體工作流程](../../buttons/media-capture/media/002-cancelworkflowbtn.md) | `Button` | `CancelWorkflowBtn` | `CancelWorkflow_Click` |
| [Measured EBU R128 two-pass normalization · EBU R128 量度式兩步正規化](../../buttons/media-capture/media/003-normalizer128btn.md) | `Button` | `NormalizeR128Btn` | `NormalizeR128_Click` |
| [Trim leading trailing and internal silence from audio · 剪走音訊頭尾同中間靜音](../../buttons/media-capture/media/004-trimsilencebtn.md) | `Button` | `TrimSilenceBtn` | `TrimSilence_Click` |
| [Two-pass vidstab video stabilization · 兩步 vidstab 影片防震](../../buttons/media-capture/media/005-stabilizebtn.md) | `Button` | `StabilizeBtn` | `Stabilize_Click` |
| [Detect and crop black bars · 偵測同裁走影片黑邊](../../buttons/media-capture/media/006-autocropbtn.md) | `Button` | `AutoCropBtn` | `AutoCrop_Click` |
| [Choose video clips in join order · 按合併次序揀影片片段](../../buttons/media-capture/media/007-chooseconcatbtn.md) | `Button` | `ChooseConcatBtn` | `ChooseConcat_Click` |
| [Join selected clips without re-encoding · 無重編碼合併已揀片段](../../buttons/media-capture/media/008-joinconcatbtn.md) | `Button` | `JoinConcatBtn` | `JoinConcat_Click` |
| [Detect working NVIDIA NVENC encoders · 偵測可用 NVIDIA NVENC 編碼器](../../buttons/media-capture/media/009-detectnvencbtn.md) | `Button` | `DetectNvencBtn` | `DetectNvenc_Click` |
| [Encode with selected NVENC codec · 用已揀 NVENC codec 編碼](../../buttons/media-capture/media/010-encodenvencbtn.md) | `Button` | `EncodeNvencBtn` | `EncodeNvenc_Click` |
| [Run two-pass target-size encoding · 執行兩步目標容量編碼](../../buttons/media-capture/media/011-targetsizebtn.md) | `Button` | `TargetSizeBtn` | `TargetSize_Click` |
| [Choose an SRT or ASS subtitle file · 揀 SRT 或 ASS 字幕檔](../../buttons/media-capture/media/012-picksubtitlebtn.md) | `Button` | `PickSubtitleBtn` | `PickSubtitle_Click` |
| [Burn or soft-mux the selected subtitle · 燒入或軟掛已揀字幕](../../buttons/media-capture/media/013-subtitlerunbtn.md) | `Button` | `SubtitleRunBtn` | `SubtitleRun_Click` |
| [Choose chapter output folder · 揀章節輸出資料夾](../../buttons/media-capture/media/014-pickchapterfolderbtn.md) | `Button` | `PickChapterFolderBtn` | `PickChapterFolder_Click` |
| [Read embedded media chapters · 讀取媒體內嵌章節](../../buttons/media-capture/media/015-readchaptersbtn.md) | `Button` | `ReadChaptersBtn` | `ReadChapters_Click` |
| [Split every chapter without re-encoding · 無重編碼分割全部章節](../../buttons/media-capture/media/016-splitchaptersbtn.md) | `Button` | `SplitChaptersBtn` | `SplitChapters_Click` |
| [Choose HEIC HEIF or JPEG-XL source folder · 揀 HEIC HEIF 或 JPEG-XL 來源資料夾](../../buttons/media-capture/media/017-pickphotoinputbtn.md) | `Button` | `PickPhotoInputBtn` | `PickPhotoInput_Click` |
| [Choose a separate converted-photo output folder · 揀另一個轉換相片輸出資料夾](../../buttons/media-capture/media/018-pickphotooutputbtn.md) | `Button` | `PickPhotoOutputBtn` | `PickPhotoOutput_Click` |
| [Convert the bounded photo batch · 轉換有限量相片批次](../../buttons/media-capture/media/019-convertphotosbtn.md) | `Button` | `ConvertPhotosBtn` | `ConvertPhotos_Click` |
| [Strip EXIF GPS and image metadata · 移除 EXIF GPS 同相片 metadata](../../buttons/media-capture/media/020-stripmetadatabtn.md) | `Button` | `StripMetadataBtn` | `StripMetadata_Click` |
| [InputBtn](../../buttons/media-capture/media/021-inputbtn.md) | `Button` | `InputBtn` | `PickInput_Click` |
| [OutputBtn](../../buttons/media-capture/media/022-outputbtn.md) | `Button` | `OutputBtn` | `PickOutput_Click` |
| [TrimCopyBtn](../../buttons/media-capture/media/023-trimcopybtn.md) | `Button` | `TrimCopyBtn` | `TrimCopy_Click` |
| [TrimEncodeBtn](../../buttons/media-capture/media/024-trimencodebtn.md) | `Button` | `TrimEncodeBtn` | `TrimEncode_Click` |
| [GifBtn](../../buttons/media-capture/media/025-gifbtn.md) | `Button` | `GifBtn` | `MakeGif_Click` |
| [FrameBtn](../../buttons/media-capture/media/026-framebtn.md) | `Button` | `FrameBtn` | `GrabFrame_Click` |