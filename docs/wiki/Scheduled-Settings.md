# Scheduled settings · 排程設定

WinForge now exposes a versioned scheduled-settings editor in the Settings surface. Rules can temporarily set language, theme, density, accent/seed colour, font family, font scale, font weight, or display name from local data, a validated version-1 HTTPS API, or a Home Assistant boolean entity. The result is an ephemeral override and never overwrites the base profile. · WinForge 而家喺 Settings 介面提供有版本嘅排程設定編輯器。規則可以由本機資料、驗證過嘅 version-1 HTTPS API，或者 Home Assistant boolean 暫時改語言、主題、密度、accent／seed 顏色、字體、字體比例、字體粗幼或者顯示名稱。結果只係暫時覆蓋，唔會覆蓋基本設定。

Each rule records an identifier, label, enabled state, priority, optional inclusive dates, optional times, Every day or explicit weekdays, the operating system time zone, source, and allowlisted values. Equal start/end times mean 24 hours; an earlier end crosses midnight and uses the window's start date for weekday/date matching. Higher priority wins, then later list order. · 每條規則保存識別碼、名稱、啟用狀態、優先級、可選日期、可選時間、每日／指定星期、操作系統時區、來源同 allowlist 數值。開始／結束相同代表 24 小時；結束早過開始代表跨午夜，星期／日期跟時間窗開始嗰日。優先級高嗰條勝出，再由列表後面嗰條勝出。

External refresh is bounded to five seconds, rejects redirects and embedded credentials, caps responses at 256 KiB, validates fields and schema, and fails safe to the last valid or base state. Home Assistant tokens remain in the Windows credential vault under a stable per-rule key. · 外部刷新有五秒限制，拒絕 redirect 同 URL 內嵌憑證，回應最多 256 KiB，並驗證欄位同 schema；失敗會安全保留最後有效值或者基本設定。Home Assistant token 只會用每條規則穩定 key 存喺 Windows credential vault。

Source: `Services/ScheduledSettingsService.cs`, `Pages/SettingsPage.xaml.cs`; tests: `tests/ScheduledSettings.Tests/Program.cs` (`3/3`). Full details: [scheduled-settings feature article](../features/universal/scheduled-settings.md). · 來源：`Services/ScheduledSettingsService.cs`、`Pages/SettingsPage.xaml.cs`；測試：`tests/ScheduledSettings.Tests/Program.cs`（`3/3`）。詳細資料見[排程設定功能文章](../features/universal/scheduled-settings.md)。

![Settings universal controls including scheduled-settings configuration](../screenshot-settings-universal-2026-08-11.png)
