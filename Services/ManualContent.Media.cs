using System.Collections.Generic;

namespace WinForge.Services;

// 媒體與擷取章節嘅教學條目 · How-to entries for the Media & capture section.
public static partial class ManualContent
{
    private static List<ManualEntry> MediaEntries() => new()
    {
        new ManualEntry
        {
            Tag = "module.media", Glyph = "",
            TitleEn = "Media", TitleZh = "媒體",
            SummaryEn = "Convert, trim and tweak video and audio with FFmpeg — plus one-click presets, GIF making and frame grabs.",
            SummaryZh = "用 FFmpeg 轉檔、剪裁同調校影片同音訊 — 仲有一鍵預設、整 GIF 同擷取畫面格。",
            StepsEn = new[]
            {
                "Click the input button to pick a video or audio file; pick an output file if you want to choose where it saves.",
                "Tap a quick-conversion chip (e.g. to MP4, MP3, WebM) to convert in one click.",
                "To trim, type a start time and a duration, then press Trim (copy) for a fast cut or Trim (encode) for a precise one.",
                "To make a GIF, set FPS and width and press the GIF button; press the frame button to grab a single still.",
                "Search the Advanced operations box for more FFmpeg recipes.",
            },
            StepsZh = new[]
            {
                "撳輸入掣揀一個影片或者音訊檔；想自己揀儲存位置就撳輸出掣。",
                "撳一粒快速轉換按鈕（例如轉去 MP4、MP3、WebM）就一鍵轉檔。",
                "想剪裁就打開始時間同長度，再撳 Trim (copy) 快速切，或者 Trim (encode) 精準切。",
                "想整 GIF 就設定 FPS 同闊度再撳 GIF 掣；撳畫面格掣就擷取一張靜止圖。",
                "喺進階操作搜尋框搵更多 FFmpeg 配方。",
            },
            TipEn = "Trim (copy) is instant but cuts only at keyframes; Trim (encode) is frame-accurate but slower.",
            TipZh = "Trim (copy) 即時但只可以喺關鍵影格切；Trim (encode) 準到逐格但慢啲。",
            Keywords = "ffmpeg video audio convert trim gif frame 影片 音訊 轉檔 剪裁 畫面格",
        },
        new ManualEntry
        {
            Tag = "module.audioeditor", Glyph = "",
            TitleEn = "Audio Editor", TitleZh = "音訊編輯器",
            SummaryEn = "Open or record a clip, edit on the waveform, apply fades and effects, then export — Audacity is one click away for the rest.",
            SummaryZh = "開檔或者錄音、喺波形上面剪輯、加淡入淡出同效果，再匯出 — 想做更多撳一下就開 Audacity。",
            StepsEn = new[]
            {
                "Press Open to load a file, or pick a microphone and press Record to capture one.",
                "Drag on the waveform to select a range; use Play / Play selection and the transport buttons to listen.",
                "Trim, delete or silence the selection; add fade in / fade out / normalize, or adjust gain, speed and pitch.",
                "Use Mix, Concat or Reverse for whole-clip edits, then press Export to save (WAV / MP3 / FLAC).",
                "Need more? Press Launch Audacity to open the full editor.",
            },
            StepsZh = new[]
            {
                "撳 Open 載入檔案，或者揀個 mic 再撳 Record 錄音。",
                "喺波形上面拖曳揀範圍；用 Play／Play selection 同播放掣聽返。",
                "可以剪裁、刪除或者靜音揀咗嘅範圍；加淡入／淡出／正規化，或者調校增益、速度同音高。",
                "用 Mix、Concat 或者 Reverse 處理成段聲，再撳 Export 儲存（WAV／MP3／FLAC）。",
                "想做多啲？撳 Launch Audacity 開埋成個編輯器。",
            },
            TipEn = "Press Revert to undo all your edits and go back to the original clip.",
            TipZh = "撳 Revert 可以還原所有編輯，返去原本嗰段聲。",
            Keywords = "audacity waveform record fade normalize gain pitch denoise mix export 波形 錄音 淡入 降噪 混音 匯出",
        },
        new ManualEntry
        {
            Tag = "module.mediaplayer", Glyph = "",
            TitleEn = "Media Player", TitleZh = "媒體播放器",
            SummaryEn = "A built-in VLC player for files and streams — with playlist, audio/subtitle tracks, snapshots and quick transcode.",
            SummaryZh = "內置 VLC 播放器，播檔案同串流 — 有播放清單、音訊／字幕軌、截圖同快速轉檔。",
            StepsEn = new[]
            {
                "Press Open file to play a video or audio file, or paste a URL (http / rtsp / mms) and press the open button.",
                "Use the transport row to play / pause, stop, skip, change speed and volume.",
                "Pick an audio track or subtitle track on the right, or press Load subtitle to add an external one.",
                "Press the snapshot button to save a PNG of the current frame; press fullscreen to fill the screen.",
                "Add files to the playlist, then double-tap an item to play it.",
            },
            StepsZh = new[]
            {
                "撳 Open file 播影片或者音訊檔，或者貼一條 URL（http／rtsp／mms）再撳開啟掣。",
                "用下面嗰行掣播放／暫停、停止、上下一個、改速度同音量。",
                "喺右邊揀音訊軌或者字幕軌，或者撳 Load subtitle 加外掛字幕。",
                "撳截圖掣可以將目前畫面存做 PNG；撳全螢幕填滿成個畫面。",
                "將檔案加入播放清單，再 double-tap 一項就播。",
            },
            TipEn = "Use the transcode panel on the right to quickly convert the loaded media to another format.",
            TipZh = "用右邊嘅轉檔面板，可以快速將載入咗嘅媒體轉做另一個格式。",
            Keywords = "vlc play video audio stream url playlist subtitle snapshot fullscreen transcode 播放 串流 播放清單 字幕 截圖 全螢幕 轉檔",
        },
        new ManualEntry
        {
            Tag = "module.ytdlp", Glyph = "",
            TitleEn = "Media Downloader", TitleZh = "媒體下載器",
            SummaryEn = "Paste a link and download video or audio with yt-dlp — pick quality, grab subtitles, thumbnails and whole playlists.",
            SummaryZh = "貼條連結用 yt-dlp 下載影片或者音訊 — 揀畫質、攞字幕、縮圖同成個播放清單。",
            StepsEn = new[]
            {
                "Paste one or more URLs into the box (or press Paste), then press List formats to see what's available.",
                "Pick a quality preset (best, 1080p, 720p, audio only…) or type your own format; choose an audio format for audio-only.",
                "Tick the options you want — subtitles, thumbnail, metadata, SponsorBlock, download archive.",
                "Choose an output folder and filename template, then press Download and watch the progress bar.",
                "Press Update to update yt-dlp, or Cancel to stop a running download.",
            },
            StepsZh = new[]
            {
                "貼一條或者幾條 URL 入個框（或者撳 Paste），再撳 List formats 睇下有咩可以揀。",
                "揀一個畫質預設（best、1080p、720p、淨係音訊…）或者自己打格式；淨係音訊就揀音訊格式。",
                "剔你想要嘅選項 — 字幕、縮圖、metadata、SponsorBlock、下載記錄。",
                "揀個輸出資料夾同檔名範本，再撳 Download 睇住進度條。",
                "撳 Update 更新 yt-dlp，或者撳 Cancel 停止下載緊嘅嘢。",
            },
            TipEn = "For playlists, fill in the playlist-items box (e.g. 1-5,8) to download only the parts you want.",
            TipZh = "下載播放清單嘅話，喺 playlist-items 框打範圍（例如 1-5,8）就淨係下載你想要嗰幾個。",
            Keywords = "yt-dlp youtube download mp3 m4a playlist subtitles format quality sponsorblock cookies 下載 字幕 播放清單 畫質 縮圖",
        },
        new ManualEntry
        {
            Tag = "module.blender", Glyph = "",
            TitleEn = "Blender (3D / Render)", TitleZh = "Blender（3D／算圖）",
            SummaryEn = "Render .blend files headlessly — single frames or animations — queue batch jobs and run handy Python scripts.",
            SummaryZh = "無頭算圖 .blend 檔 — 單格或者動畫 — 排批次工作，仲可以行實用嘅 Python 腳本。",
            StepsEn = new[]
            {
                "Pick an input .blend, then choose an output folder and a name template (e.g. frame_####).",
                "Choose Single frame (and a frame number) or Animation (and a start/end range).",
                "Set the engine (Cycles / EEVEE), output format, device (CPU/GPU) and samples.",
                "Press Render to start, or Queue to add it to the batch and Run queue later.",
                "Use the script runner to run a built-in or custom Python script; press Open .blend to edit it in the Blender GUI.",
            },
            StepsZh = new[]
            {
                "揀一個輸入 .blend，再揀輸出資料夾同名稱範本（例如 frame_####）。",
                "揀 Single frame（同影格號碼）或者 Animation（同開始／結束範圍）。",
                "設定引擎（Cycles／EEVEE）、輸出格式、裝置（CPU／GPU）同 samples。",
                "撳 Render 開始，或者撳 Queue 加入批次，遲啲再 Run queue。",
                "用腳本執行器行內置或者自訂 Python 腳本；撳 Open .blend 喺 Blender 介面入面編輯。",
            },
            TipEn = "Leave samples at 0 to keep whatever the .blend file already has set.",
            TipZh = "Samples 留喺 0 就會用返 .blend 檔本身設定咗嘅數值。",
            Keywords = "blender 3d render cycles eevee headless animation frame gpu samples batch queue python 算圖 渲染 動畫 影格 批次 佇列",
        },
        new ManualEntry
        {
            Tag = "module.libreoffice", Glyph = "",
            TitleEn = "Document Converter", TitleZh = "文件轉換器",
            SummaryEn = "Batch-convert documents with LibreOffice — Word, Excel, PowerPoint, ODF and more, to PDF or any office format.",
            SummaryZh = "用 LibreOffice 批次轉換文件 — Word、Excel、PowerPoint、ODF 等等，轉做 PDF 或者任何 office 格式。",
            StepsEn = new[]
            {
                "Press Add files (or Add folder) to build the list of documents to convert.",
                "Pick the target format (PDF, DOCX, XLSX, ODT…); set an output folder or leave it next to the source.",
                "Tick Recurse if you added folders and want subfolders included.",
                "Press Convert and watch the per-file status and progress bar.",
                "Press Open output folder to see the converted files.",
            },
            StepsZh = new[]
            {
                "撳 Add files（或者 Add folder）整返條要轉換嘅文件清單。",
                "揀目標格式（PDF、DOCX、XLSX、ODT…）；設定輸出資料夾，或者留喺原檔隔籬。",
                "如果加咗資料夾又想連子資料夾一齊，就剔 Recurse。",
                "撳 Convert，睇住每個檔嘅狀態同進度條。",
                "撳 Open output folder 睇轉換好嘅檔案。",
            },
            TipEn = "Conversion runs LibreOffice headlessly, so keep the LibreOffice app itself closed to avoid conflicts.",
            TipZh = "轉換係用無頭模式行 LibreOffice，所以最好閂咗 LibreOffice 程式本身，免得撞。",
            Keywords = "libreoffice document convert batch pdf docx xlsx odt pptx writer calc impress headless 文件 轉換 轉檔 批次",
        },
        new ManualEntry
        {
            Tag = "module.recorder", Glyph = "",
            TitleEn = "Screen Recorder", TitleZh = "螢幕錄影",
            SummaryEn = "Record your screen to an MP4 with FFmpeg — pick the output file, set the frame rate, and hit Record.",
            SummaryZh = "用 FFmpeg 將螢幕錄成 MP4 — 揀輸出檔案、設定影格率，撳 Record 就得。",
            StepsEn = new[]
            {
                "Press Change to pick where the recording is saved.",
                "Set the frame rate (FPS) — 30 is a good default.",
                "Press Record to start; a red dot and the status text show it's running.",
                "Press Stop when you're done; the result bar shows the saved file.",
            },
            StepsZh = new[]
            {
                "撳 Change 揀錄影存喺邊。",
                "設定影格率（FPS）— 30 係好用嘅預設。",
                "撳 Record 開始；紅點同狀態文字代表錄緊。",
                "做完撳 Stop；結果列會顯示儲存咗嘅檔案。",
            },
            TipEn = "For region, window or GIF capture, use Capture Studio or GIF Studio instead.",
            TipZh = "想擷取區域、視窗或者 GIF，可以改用擷取工作室或者螢幕轉 GIF。",
            Keywords = "record screen capture gdigrab mp4 ffmpeg fps 錄影 螢幕 影格率",
        },
        new ManualEntry
        {
            Tag = "module.capture", Glyph = "",
            TitleEn = "Capture Studio", TitleZh = "擷取工作室",
            SummaryEn = "Record a region to MP4 or GIF, snip straight to the clipboard, and OCR text out of any part of the screen.",
            SummaryZh = "將區域錄成 MP4 或者 GIF、即截即入剪貼簿，仲可以喺螢幕任何位置 OCR 認字。",
            StepsEn = new[]
            {
                "In the record card, choose an output file and FPS; tick the GIF box if you want a GIF instead of MP4.",
                "Press Record, drag to select a region, then press Stop when done.",
                "To snip, press the snip button, drag a region, and it lands on your clipboard; press Save to keep it as a file.",
                "For OCR, press OCR region (drag a region) or OCR file, and the recognised text appears below — copy it out.",
            },
            StepsZh = new[]
            {
                "喺錄影卡揀輸出檔同 FPS；想要 GIF 而唔係 MP4 就剔 GIF。",
                "撳 Record，拖曳揀個區域，做完撳 Stop。",
                "想截圖就撳 snip 掣，拖個區域，就會入咗剪貼簿；撳 Save 可以存做檔案。",
                "OCR 就撳 OCR region（拖個區域）或者 OCR file，認到嘅文字會喺下面顯示 — 複製出嚟。",
            },
            TipEn = "OCR needs the right language packs installed — a warning bar appears if a language is missing.",
            TipZh = "OCR 要裝啱嘅語言包 — 如果缺咗某種語言會彈警告列。",
            Keywords = "snip screenshot region gif ocr text recognize clipboard 截圖 擷取 區域 文字辨識 認字",
        },
        new ManualEntry
        {
            Tag = "module.cropandlock", Glyph = "",
            TitleEn = "Crop And Lock", TitleZh = "裁切與鎖定",
            SummaryEn = "Pin a live thumbnail of any window — or crop a window down to one region — and keep it always on top.",
            SummaryZh = "釘住任何視窗嘅即時縮圖 — 或者將視窗裁到淨返一個範圍 — 永遠置頂。",
            StepsEn = new[]
            {
                "Turn on the Enable switch, and optionally set the thumbnail and crop hotkeys.",
                "Press Refresh, then pick a window from the list.",
                "Press Thumbnail for a live always-on-top mirror, or Crop to reparent a chosen region into its own window.",
                "Manage your pinned windows in the active list; press the close button or Close all to remove them.",
            },
            StepsZh = new[]
            {
                "開 Enable 開關，需要嘅話可以設定縮圖同裁切嘅熱鍵。",
                "撳 Refresh，再喺清單揀個視窗。",
                "撳 Thumbnail 整個即時置頂鏡像，或者撳 Crop 將揀咗嘅範圍搬入一個獨立視窗。",
                "喺使用中清單管理釘住咗嘅視窗；撳關閉掣或者 Close all 移除。",
            },
            TipEn = "A cropped window is a live view of the original — close the original and the crop goes away too.",
            TipZh = "裁切後嘅視窗係原視窗嘅即時畫面 — 閂咗原視窗，裁切嗰個都會冇埋。",
            Keywords = "crop lock window thumbnail reparent always on top topmost pin mirror 裁切 鎖定 縮圖 置頂 釘選 鏡像",
        },
        new ManualEntry
        {
            Tag = "module.giflab", Glyph = "",
            TitleEn = "GIF Studio", TitleZh = "螢幕轉 GIF",
            SummaryEn = "Record the screen to frames, edit the frame strip (delete / reorder / crop), preview, then export GIF, MP4 or APNG.",
            SummaryZh = "將螢幕錄成畫面格、編輯畫面格列（刪格／調次序／裁切）、預覽，再匯出 GIF、MP4 或者 APNG。",
            StepsEn = new[]
            {
                "Pick a source (region, window or fullscreen), set FPS and an optional max duration, then press Capture.",
                "In the frame strip, drag to reorder, or use move left/right, delete and crop to clean it up.",
                "Use the preview card to play back the animation and check it.",
                "In the export card pick a format (GIF / MP4 / APNG), set output FPS, scale and loop, then press Export.",
            },
            StepsZh = new[]
            {
                "揀來源（區域、視窗或者全螢幕），設定 FPS 同（可選）最長時間，再撳 Capture。",
                "喺畫面格列拖曳調次序，或者用左移／右移、刪除同裁切執靚佢。",
                "用預覽卡播返段動畫睇下啱唔啱。",
                "喺匯出卡揀格式（GIF／MP4／APNG），設定輸出 FPS、縮放同循環，再撳 Export。",
            },
            TipEn = "A lower FPS and smaller scale keep GIF file sizes down a lot.",
            TipZh = "FPS 低啲、縮放細啲，GIF 檔可以細好多。",
            Keywords = "screentogif screen to gif frames editor delete reorder crop export mp4 apng 螢幕轉 動畫 畫面格 刪格 調次序 匯出",
        },
        new ManualEntry
        {
            Tag = "module.zoomit", Glyph = "",
            TitleEn = "ZoomIt", TitleZh = "螢幕放大與標註",
            SummaryEn = "Zoom into the screen, draw and annotate on top of anything, and run a break timer — great for demos and presentations.",
            SummaryZh = "放大螢幕、喺任何嘢上面畫畫同標註，仲有小休倒數計時 — 演示同簡報啱用。",
            StepsEn = new[]
            {
                "Press Zoom, Draw or Break to start a mode now, or just use the hotkeys.",
                "Set each hotkey with the Ctrl / Alt / Shift / Win checkboxes plus a key.",
                "Choose a default pen colour, pen width and break-timer minutes, then press Save.",
                "While zoomed or drawing, use the mouse wheel to zoom, draw with the pen, and press Esc to exit.",
            },
            StepsZh = new[]
            {
                "撳 Zoom、Draw 或者 Break 即刻開個模式，或者直接用熱鍵。",
                "用 Ctrl／Alt／Shift／Win 剔格加一個按鍵，設定每個熱鍵。",
                "揀預設畫筆顏色、畫筆粗幼同小休倒數分鐘數，再撳 Save。",
                "放大或者畫畫嗰陣，用滾輪縮放、用筆畫嘢，撳 Esc 離開。",
            },
            TipEn = "ZoomIt is from Sysinternals; while drawing you can type letters to switch pen colours.",
            TipZh = "ZoomIt 係 Sysinternals 出嘅；畫畫嗰陣打字母可以切換畫筆顏色。",
            Keywords = "zoomit zoom magnify annotate draw pen arrow break timer countdown presentation sysinternals 放大 標註 畫筆 小休 倒數 簡報",
        },
        new ManualEntry
        {
            Tag = "module.mixer", Glyph = "",
            TitleEn = "Volume Mixer", TitleZh = "音量混合器",
            SummaryEn = "A per-app volume mixer (like EarTrumpet) — set each app's level, mute it, and switch the default playback device.",
            SummaryZh = "逐個 app 嘅音量混合器（似 EarTrumpet）— 調每個 app 嘅音量、靜音，同切換預設播放裝置。",
            StepsEn = new[]
            {
                "Pick a playback device from the dropdown to see the apps playing through it.",
                "Drag each app's slider to set its volume; use the mute button to silence just that app.",
                "Press Set default to make the selected device the system default.",
                "Press Refresh if an app or device doesn't show up.",
            },
            StepsZh = new[]
            {
                "喺下拉式選單揀個播放裝置，就會見到經佢出聲嘅 app。",
                "拖每個 app 嘅滑桿調音量；用靜音掣淨係靜咗嗰個 app。",
                "撳 Set default 將揀咗嘅裝置設做系統預設。",
                "如果有 app 或者裝置冇出現，撳 Refresh。",
            },
            Keywords = "eartrumpet volume mixer per-app mute playback device default 音量 靜音 逐個程式 播放裝置 預設",
        },
        new ManualEntry
        {
            Tag = "module.colorpicker", Glyph = "",
            TitleEn = "Color Picker", TitleZh = "螢幕取色",
            SummaryEn = "Eyedrop any pixel on screen and copy its colour as HEX, RGB or HSL — with a recent-colours history.",
            SummaryZh = "喺螢幕任何一點吸色，將顏色複製做 HEX、RGB 或者 HSL — 仲有最近顏色記錄。",
            StepsEn = new[]
            {
                "Press Pick, then move over the screen and click the pixel you want.",
                "The swatch and the HEX / RGB / HSL values update; press a copy button to copy that format.",
                "Type a value into the HEX box and press Apply to set a colour by hand.",
                "Click any swatch in the history strip to bring that colour back.",
            },
            StepsZh = new[]
            {
                "撳 Pick，再喺螢幕上面移動，撳你想要嗰一點。",
                "色塊同 HEX／RGB／HSL 數值會更新；撳複製掣就複製嗰個格式。",
                "喺 HEX 框打個數值再撳 Apply，可以自己手動設定顏色。",
                "撳記錄列入面任何一個色塊，就可以攞返嗰隻色。",
            },
            Keywords = "color picker eyedropper hex rgb hsl history 取色 顏色 吸色 記錄",
        },
        new ManualEntry
        {
            Tag = "module.screenruler", Glyph = "",
            TitleEn = "Screen Ruler", TitleZh = "螢幕間尺",
            SummaryEn = "Measure anything on screen in pixels — distance, horizontal, vertical, crosshair spacing or a bounded region.",
            SummaryZh = "用像素量度螢幕上面任何嘢 — 距離、水平、垂直、十字間距或者一個範圍。",
            StepsEn = new[]
            {
                "Pick a mode: Distance, Horizontal, Vertical, Cross or Bounds.",
                "Drag on the screen to measure; the pixel readout follows your cursor.",
                "Adjust the line colour (swatches or a HEX value) and thickness under Appearance.",
                "Press Esc to finish a measurement.",
            },
            StepsZh = new[]
            {
                "揀個模式：Distance、Horizontal、Vertical、Cross 或者 Bounds。",
                "喺螢幕上面拖曳量度；像素讀數會跟住游標。",
                "喺 Appearance 調線嘅顏色（色塊或者 HEX 值）同粗幼。",
                "撳 Esc 完成量度。",
            },
            Keywords = "screen ruler measure pixel distance horizontal vertical crosshair bounds 間尺 量度 像素 距離 水平 垂直 十字",
        },
        new ManualEntry
        {
            Tag = "module.pixeleditor", Glyph = "",
            TitleEn = "Pixel Editor", TitleZh = "像素畫編輯器",
            SummaryEn = "Draw pixel art with layers, a palette and animation frames — export to PNG or animated GIF (Aseprite for the heavy stuff).",
            SummaryZh = "用圖層、調色盤同動畫影格畫像素畫 — 匯出做 PNG 或者動畫 GIF（複雜嘢交畀 Aseprite）。",
            StepsEn = new[]
            {
                "Press New to start a canvas, or Import to open an existing image.",
                "Pick a tool from the left column, choose a colour (palette, recent, or a HEX value), and draw on the canvas.",
                "Use the zoom slider and grid toggle to work precisely; Undo / Redo as needed.",
                "Build animation in the Frames panel and stack art in the Layers panel.",
                "Press Export PNG for a still or Export GIF for an animation.",
            },
            StepsZh = new[]
            {
                "撳 New 開個畫布，或者撳 Import 開現有圖片。",
                "喺左邊揀工具、揀顏色（調色盤、最近用過，或者 HEX 值），喺畫布上面畫。",
                "用縮放滑桿同格網開關畫得準啲；需要就 Undo／Redo。",
                "喺 Frames 面板整動畫，喺 Layers 面板疊圖層。",
                "撳 Export PNG 出靜止圖，或者撳 Export GIF 出動畫。",
            },
            TipEn = "When Aseprite is installed, an Aseprite button appears to hand off for advanced editing.",
            TipZh = "裝咗 Aseprite 之後會出現 Aseprite 掣，可以交去做進階編輯。",
            Keywords = "aseprite sprite pixel art palette layers frames animation png gif pencil fill 像素畫 精靈 調色盤 圖層 影格 動畫",
        },
        new ManualEntry
        {
            Tag = "module.timeunit", Glyph = "",
            TitleEn = "Time & Unit Tools", TitleZh = "時間與單位工具",
            SummaryEn = "A world clock, a timezone converter and a unit converter — see the time anywhere and convert times and measurements.",
            SummaryZh = "世界時鐘、時區轉換器同單位轉換器 — 睇任何地方嘅時間，仲可以換算時間同度量。",
            StepsEn = new[]
            {
                "Your local clock shows at the top; add cities with the zone dropdown and Add city to build the world board.",
                "In the converter, pick a date and time, choose From and To zones, and read the converted time.",
                "Press Now to fill the current moment.",
                "Use the unit converter: pick a category, enter a value, and choose From and To units.",
            },
            StepsZh = new[]
            {
                "本地時鐘喺最頂；用時區下拉式選單同 Add city 加城市，砌個世界時鐘板。",
                "喺轉換器揀日期同時間、揀 From 同 To 時區，就睇到換算後嘅時間。",
                "撳 Now 填入而家嘅時刻。",
                "用單位轉換器：揀類別、輸入數值、揀 From 同 To 單位。",
            },
            Keywords = "time zone timezone world clock converter unit length mass temperature 時間 時區 世界時鐘 換算 單位",
        },
        new ManualEntry
        {
            Tag = "module.timelens", Glyph = "",
            TitleEn = "Activity Timeline", TitleZh = "活動時間軸",
            SummaryEn = "Track which apps were in the foreground and for how long — a private, on-device timeline and per-app totals.",
            SummaryZh = "追蹤邊個 app 喺前景、用咗幾耐 — 私隱、完全喺本機嘅時間軸同逐個 app 總計。",
            StepsEn = new[]
            {
                "Turn on the tracking switch to start logging foreground apps; press Pause to stop temporarily.",
                "Adjust the idle threshold and poll interval with the sliders.",
                "Pick a date (or press Today) to see that day's stacked timeline and per-app totals.",
                "Press Refresh to update, Export to save a CSV, or Clear to delete the data.",
            },
            StepsZh = new[]
            {
                "開追蹤開關開始記錄前景 app；撳 Pause 暫時停。",
                "用滑桿調閒置門檻同輪詢間隔。",
                "揀個日期（或者撳 Today）睇嗰日嘅堆疊時間軸同逐個 app 總計。",
                "撳 Refresh 更新、撳 Export 存做 CSV，或者撳 Clear 刪除資料。",
            },
            TipEn = "All tracking stays on your device — nothing is uploaded.",
            TipZh = "所有追蹤都留喺你部機 — 唔會上載任何嘢。",
            Keywords = "timelens activity timeline time tracking foreground window app usage idle export csv 活動 時間軸 時間追蹤 前景 使用量 閒置 匯出",
        },
    };
}
