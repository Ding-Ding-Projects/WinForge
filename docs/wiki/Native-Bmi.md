# Native Health Calculators · 原生健康計算器

`bmi`, `health`, and `module.bmi` resolve to a genuine C++/WinRT Health
Calculators page over the standard-C++ `WinForge.Core/Bmi` core. It preserves
managed BMI WHO bands, Mifflin–St Jeor BMR, five TDEE activity factors,
US-Navy body-fat validation/formulae, raw metric/imperial relabelling, and
route reset/language retention. All calculations are local and it has no
network, process, file, registry, persistence, secret, or clipboard-write
path.

`bmi`、`health` 同 `module.bmi` 會開真正嘅 C++/WinRT 健康計算器頁，用標準 C++
`WinForge.Core/Bmi` core。保留 managed BMI WHO 分級、Mifflin–St Jeor BMR、五個
TDEE 活動量系數、美國海軍體脂驗證／公式、原始公制／英制重標籤，同 route reset／語言
保留。所有計算只喺本機做，冇網絡、程序、檔案、registry、持久化、secret 或寫剪貼簿
路徑。

Controlled native Debug/Release builds both have 0 errors; the matching core
suites are **842/842** including **14/14 BMI Calculator** contracts. Catalog parity and
the installer contract pass. Focused BMI UIA is **14/14** across all three
aliases, including native controls, formulas, validation, raw-unit retention,
language modes, and route reset. LowLevel MCP is not callable in this session;
the required `bmi` driver rejected a blank/near-uniform PrintWindow fallback
after `CopyFromScreen` was unavailable. No PNG or process remained, so visual
evidence is `capture-blocked`.

受控 Native Debug／Release build 都係 0 errors；相應 core suite 各 **842/842**，包括
**14/14 BMI Calculator** contract。catalog parity 同 installer contract 已通過。
三個 alias 專項 BMI UIA 合共 **14/14**，包括 native control、公式、驗證、原始單位
保留、語言模式同 route reset。今個 session 冇可呼叫 LowLevel MCP；`CopyFromScreen`
唔可用後，required `bmi` driver 拒絕空白／近乎單色 PrintWindow fallback。冇 PNG／冇
process 殘留，所以 visual 係 `capture-blocked`。
