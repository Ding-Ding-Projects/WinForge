# Media & capture feature guides · 媒體與擷取功能指南

This category contains maintained implementation and operator guides for WinForge media/capture workflows. Generated module and button inventories remain under `docs/wiki/features/media-capture/`; these guides document behavior, safety boundaries, failure modes, and verification that cannot be inferred from XAML alone.

呢個分類收錄 WinForge 媒體／擷取工作流程嘅維護同操作指南。自動產生嘅模組／按鈕清單繼續放喺 `docs/wiki/features/media-capture/`；呢度會記錄單靠 XAML 睇唔到嘅行為、安全界線、失敗模式同驗證。

## Guides · 指南

- [Guided Media studio workflows](media-studio-workflows.md) · 引導式 Media 工作流程：EBU R128、silence、vidstab、crop、concat、NVENC、target-size、subtitles、chapters、HEIC/JXL 同 metadata privacy。
- [Screen Recorder lifecycle reliability](../media/screen-recorder.md) · 螢幕錄影 lifecycle 可靠性。

## HTTP/API disposition · HTTP／API 處置

These workflows invoke the locally installed ffmpeg/ffprobe engine and expose no HTTP API, so a Postman collection is not applicable. · 呢批工作流程只會呼叫本機 ffmpeg／ffprobe，冇 HTTP API，所以唔適用 Postman collection。
