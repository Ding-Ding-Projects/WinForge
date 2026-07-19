# Native Health Calculators · 原生健康計算器

`bmi`, `health`, and `module.bmi` are native C++/WinRT Health Calculators over
the standard-C++ `WinForge.Core/Bmi` core: BMI WHO bands, Mifflin–St Jeor BMR,
five TDEE factors, US-Navy body-fat rules, raw unit relabelling, language
retention, and lifecycle reset all stay local.

`bmi`、`health` 同 `module.bmi` 係用標準 C++ `WinForge.Core/Bmi` core 嘅原生
C++/WinRT 健康計算器：BMI WHO 分級、Mifflin–St Jeor BMR、五個 TDEE 系數、美國海軍
體脂規則、原始單位重標籤、語言保留同 lifecycle reset 全部都喺本機。

Controlled Debug/Release builds are 0-error and the core suites are **842/842**
(BMI **14/14**); catalog parity and the installer contract pass. Focused UIA
is **14/14** across all BMI aliases. LowLevel MCP is not callable here and the
required driver rejected a blank fallback; no PNG or process remained, so
visual evidence is `capture-blocked`.

受控 Debug／Release build 係 0-error，core 各 **842/842**（BMI **14/14**）；catalog
parity 同 installer contract 已通過。全部 BMI alias 專項 UIA **14/14**。呢度冇可呼叫
LowLevel MCP，required driver 拒絕空白 fallback；冇 PNG／冇 process 殘留，所以 visual
係 `capture-blocked`。
