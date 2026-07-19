# Native Health Calculators · 原生健康計算器

`module.bmi` is a genuine C++/WinRT route backed by the pure standard-C++
`WinForge.Core/Bmi` library. Its registered deep links `bmi`, `health`, and
`module.bmi` all resolve to the native page; it never launches, hosts, or
delegates calculations to the managed application.

`module.bmi` 而家係真正嘅 C++/WinRT route，由純標準 C++
`WinForge.Core/Bmi` library 支援。`bmi`、`health` 同 `module.bmi` 三個已登記
deep link 都會開原生頁；唔會啟動、寄宿或者交畀 managed app 做計算。

## Behaviour parity · 行為相容

- BMI uses managed-compatible kilogram-per-metre-squared arithmetic and the
  six WHO boundaries: underweight, normal weight, overweight, and obesity
  classes I–III.
- BMR uses Mifflin–St Jeor with the managed male/female offsets. TDEE applies
  the five ordered activity factors (1.200, 1.375, 1.550, 1.725, 1.900).
  Age follows managed `Math.Round` midpoint-to-even semantics before the
  1–130 validation gate.
- The US Navy body-fat estimate preserves the managed male/female
  circumference formulae, requires hips only for the female path, rejects an
  invalid circumference difference, and clamps finite results to 2–70%.
- Metric/imperial controls intentionally relabel and reinterpret the current
  raw values; they do not silently convert typed values, matching the managed
  calculator. Every output recomputes locally after an edit.
- English, Cantonese, and bilingual rerenders retain current values. Leaving
  the route releases its observable controls, and a fresh route visit resets
  the managed defaults (170 cm, 65 kg, age 30, sedentary, and the documented
  US Navy measurements).

- BMI 用相容 managed 嘅公斤／米平方運算，同六個 WHO 界線：體重過輕、正常、超重、
  肥胖一至三級。
- BMR 用 Mifflin–St Jeor 同 managed 男／女 offset；TDEE 會按次序用五個活動量系數
  （1.200、1.375、1.550、1.725、1.900）。年齡會先跟 managed `Math.Round`
  midpoint-to-even，再經 1–130 驗證。
- 美國海軍體脂估算保留 managed 男／女圍度公式；女性先需要臀圍，無效圍度差會拒絕，
  有限結果會 clamp 喺 2–70%。
- 公制／英制 control 特登只換標籤同解讀而家原始數值，唔會偷偷轉換已輸入數值，跟
  managed calculator 一樣。每次修改都只會喺本機即時重算。
- 英文、粵語同雙語重畫會保留而家數值；離開 route 會釋放 observable control，新一次
  入 route 就還原 managed 預設（170 厘米、65 公斤、30 歲、久坐同文件列明嘅海軍量度）。

## Safety and accessibility · 安全同無障礙

All arithmetic is in memory. The route has no network, process, file-system,
registry, elevation, persistence, secret, or clipboard-write path. It is a
fixed calculator form rather than a repeating collection, so no virtualized
row state is needed. Invalid or non-finite values render a local validation
message without retaining a stale calculation. Result and status fields have
live announcements disabled to avoid per-keystroke screen-reader noise, as
the managed page has no explicit announcement contract.

所有運算都只喺記憶體做。呢條 route 冇網絡、程序、檔案系統、registry、提升權限、
持久化、secret 或寫剪貼簿路徑。佢係固定 calculator form，唔係可重複 collection，
所以唔需要 virtualized row state。無效／非有限輸入只會顯示本機驗證訊息，唔會留低舊
計算結果。結果同 status field 關咗 live announcement，避免每打一下字就騷擾 screen
reader；managed page 本身冇明確 announcement contract。

## Verification · 驗證

In the controlled integration, native Debug and Release x64 solution builds
both exited with 0 errors. Debug and Release core suites each passed
**842/842**, including **14/14 BMI Calculator** contracts for
conversions, guards, WHO bands, managed rounding, BMR/TDEE, and both US Navy
formulae. Catalog parity covers 346 fixed routes plus five dynamic families,
and the native installer contract passes. Focused
`Invoke-NativeShellSmoke.ps1 -BmiRoutesOnly` UI Automation passed **14/14**
across `bmi`, `health`, and `module.bmi`, covering UIA names/bounds, defaults,
live invalid-input recovery, BMR/TDEE, both body-fat paths, raw-unit
relabel persistence, all language modes, and in-process reset-on-reentry.

喺受控整合，native Debug 同 Release x64 solution build 都係 0 errors。Debug／Release
core 各自通過 **842/842**，包括 **14/14** 個
BMI Calculator contract（換算、guard、WHO 分級、managed rounding、BMR/TDEE 同兩條
美國海軍公式）。catalog parity 覆蓋 346 條固定 route 同五組動態家族，native installer
contract 都通過。專項 `Invoke-NativeShellSmoke.ps1 -BmiRoutesOnly` UI Automation
喺 `bmi`、`health` 同 `module.bmi` 合共通過 **14/14**，涵蓋 UIA 名稱／邊界、預設、
即時無效輸入恢復、BMR/TDEE、兩條體脂路徑、原始單位重標籤保留、三種語言同 in-process
重新入 route 時重設。

## Visual evidence · 視覺證據

The repository-local LowLevel Computer Use MCP checkout exists, but this
Codex session exposes no callable headless-desktop tools. The required native
driver attempted `bmi`; `CopyFromScreen` was unavailable and its `PrintWindow`
fallback was blank or near-uniform, so it was rejected. No PNG was created or
retained and no worktree `WinForge.exe` process remained. Visual evidence is
honestly `capture-blocked`; no stale or synthetic screenshot was substituted.

repo 本機 LowLevel Computer Use MCP checkout 存在，但今個 Codex session 冇可呼叫嘅
headless-desktop tool。required native driver 試過 `bmi`；`CopyFromScreen` 唔可用，
而 `PrintWindow` fallback 係空白／近乎單色，所以已拒絕。冇建立或者保留 PNG，亦冇
worktree `WinForge.exe` process 殘留。visual 如實係 `capture-blocked`；冇用舊圖或者
假圖頂替。
