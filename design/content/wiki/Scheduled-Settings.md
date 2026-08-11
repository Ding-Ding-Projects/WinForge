# Scheduled settings · 排程設定

WinForge's scheduled-settings editor stores versioned rules for temporary language, theme, density, accent, font, and display-name overrides. Sources are local, validated HTTPS API, or Home Assistant boolean. Matching uses the operating system time zone, explicit weekday/date rules, deterministic priority, and tested cross-midnight semantics. · WinForge 排程設定編輯器保存有版本規則，暫時改語言、主題、密度、accent、字體同顯示名稱。來源包括本機、驗證 HTTPS API 或 Home Assistant boolean。符合規則用操作系統時區、指定星期／日期、固定優先級同經測試跨午夜語義。

External values are bounded and validated. Redirects, embedded credentials, unknown fields, oversized responses, offline errors, and Home Assistant off states fail safe to the last valid or base value. Tokens stay in the Windows credential vault. · 外部數值有大小限制兼會驗證。redirect、URL 內嵌憑證、未知欄位、過大回應、離線錯誤同 Home Assistant 關閉狀態都會安全退返最後有效值或者基本值。token 只會留喺 Windows credential vault。

![Settings universal controls including scheduled-settings configuration](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-settings-universal-2026-08-11.png)
