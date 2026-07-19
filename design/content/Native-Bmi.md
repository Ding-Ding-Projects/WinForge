# Native Health Calculators · 原生健康計算器

`bmi`, `health`, and `module.bmi` now resolve to a dedicated C++/WinRT page
backed by the standard-C++ `WinForge.Core/Bmi` core. It keeps managed-compatible
BMI WHO bands, Mifflin–St Jeor BMR, five TDEE activity factors, US-Navy
body-fat validation/formulae, raw metric/imperial relabelling, language-state
retention, and lifecycle reset. All calculation is local; no network, process,
file, registry, elevation, persistence, secret, or clipboard-write path exists.

`bmi`、`health` 同 `module.bmi` 而家會開專用 C++/WinRT 頁，由標準 C++
`WinForge.Core/Bmi` core 支援。保留 managed 相容 BMI WHO 分級、Mifflin–St Jeor
BMR、五個 TDEE 活動量系數、美國海軍體脂驗證／公式、原始公制／英制重標籤、語言狀態
保留同 lifecycle reset。全部計算只喺本機做；冇網絡、程序、檔案、registry、提升權限、
持久化、secret 或寫剪貼簿路徑。

Controlled Debug/Release native builds have 0 errors and each core suite is **842/842**
(BMI Calculator **14/14**). Catalog parity and installer contract pass. Focused
BMI UIA is **14/14** across `bmi`, `health`, and `module.bmi`. The local
LowLevel checkout is not callable in this session; the required `bmi` driver
rejected its blank/near-uniform PrintWindow fallback after `CopyFromScreen`
was unavailable. No PNG or process remained, so visual evidence is
`capture-blocked`.

受控 Debug／Release native build 係 0 errors，core 各 **842/842**（BMI Calculator
**14/14**）。catalog parity 同 installer contract 已通過。`bmi`、`health` 同
`module.bmi` 專項 BMI UIA 合共 **14/14**。本機 LowLevel checkout 今個 session 唔可
呼叫；`CopyFromScreen` 唔可用後，required `bmi` driver 拒絕空白／近乎單色 PrintWindow
fallback。冇 PNG／冇 process 殘留，所以 visual 係 `capture-blocked`。
