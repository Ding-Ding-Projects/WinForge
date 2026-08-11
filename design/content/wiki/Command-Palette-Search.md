# Command Palette Search · 指令面板搜尋

WinForge's code-built Command Palette uses the same shared `SearchPatternBox` as the other migrated search surfaces. Plain text remains the default; explicit regex mode adds bounded local `.NET` validation and result matching without changing provider actions. · WinForge code-built Command Palette 同其他已遷移搜尋介面共用 `SearchPatternBox`。純文字繼續係預設；明確 regex mode 會加入有界本機 `.NET` 驗證同結果配對，但唔會改 provider action。

Opening the palette focuses the nested query editor. Only that editor's `Enter` launches a result; the raw pattern, guided builder, and sample fields retain their own Enter behavior. Errors and empty results are announced in a named status surface, and language changes refresh the accessible names and status copy. · 開啟 palette 會 focus nested query editor。只有嗰個 editor 嘅 `Enter` 會啟動結果；raw pattern、guided builder 同 sample 欄位保留自己嘅 Enter 行為。Error 同空結果會喺具名 status surface 宣布，而語言變更會更新 accessible name 同 status 文案。
