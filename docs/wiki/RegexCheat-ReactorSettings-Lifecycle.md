# Regex Cheatsheet & Reactor Settings Lifecycle · 正則速查同反應堆設定生命週期

Current correction — 2026-07-17: Regex Cheatsheet is the **fourth of six** native regex-search surfaces. The current Debug/Release core baseline is **417/417**; earlier 395/403 and 224/226 UI figures are historical. Headless WinUI visual evidence remains `capture-blocked` with no visible-desktop fallback.

目前更正 — 2026-07-17：Regex Cheatsheet 係六個 native regex-search surface 入面的**第四個**。目前 Debug/Release core 基線係 **417/417**；舊的 395/403 同 224/226 UI 數字只屬歷史。headless WinUI 視覺證據仍然 `capture-blocked`，不會回退去可見桌面。

## What changed · 改咗乜

**EN —** Regex Cheatsheet now documents `(?>a*)`, the valid .NET atomic equivalent of possessive `a*`, instead of the unsupported `*+` syntax. Reactor Settings now keeps one named live-API timer callback per page instance and balances its named language handler across every load/unload cycle.

**粵語 —** Regex Cheatsheet 而家文件寫 `(?>a*)`，即係 .NET 入面佔有 `a*` 嘅有效原子等價寫法，唔再寫唔支援嘅 `*+` 語法。Reactor Settings 而家每個 page instance 只會有一個具名 live-API timer callback，並會喺每次 load/unload 間平衡具名語言 handler。

## Safe verification · 安全驗證

```powershell
dotnet run --project tests/RegexCheatService.Tests -c Debug
dotnet run --project tests/ReactorSettingsLifecycle.Tests -c Debug
```

**EN —** Both focused harnesses passed 3/3. They prove catalog/parser correctness and lifecycle ownership without starting the reactor, Home Assistant, Windows linkage, or a real shutdown path. The Debug x64 solution build passed with 0 errors, and the XAML literal-safety guard passed.

**粵語 —** 兩個專注 harness 都 3/3 通過。佢哋會證明 catalog／parser 正確同生命週期所屬，而唔會啟動反應堆、Home Assistant、Windows 連動或者真實關機路徑。Debug x64 solution build 以 0 errors 通過，XAML literal-safety guard 都通過。

## Native C++ Regex Cheatsheet evidence · 原生 C++ Regex 速查表證據

**EN —** The native `module.regexcheat` is a separate pure-C++/C++/WinRT slice, not a claim that every managed search field has already migrated. It contains 67 bilingual reference rows in nine categories, eight Copy-only patterns, literal-default filtering, and an explicit bounded-PCRE2 mode. Invalid patterns keep the prior visible results and the full builder can return a verified pattern to the fourth native regex target. .NET-only syntax is inert documentation only; no code, process, network, or package-argument path is exposed. Debug/Release native tests passed **395/395**, catalog parity passed 346 fixed routes plus five dynamic families, and isolated LowLevel MCP UI Automation passed **224/224**.

**粵語 —** 原生 `module.regexcheat` 係一個獨立嘅純 C++／C++/WinRT 批次，唔係聲稱每個受控版搜尋欄都已經遷移。佢有 67 項、九個分類嘅雙語參考、八個 Copy-only 模式、literal 預設篩選，同明確啟用嘅有界 PCRE2 mode。無效模式會保留之前顯示嘅結果，完整 builder 可以將已驗證模式交返第四個原生 regex target。.NET 專用語法只係惰性文件；冇 code、process、network 或 package-argument 路徑。Debug／Release 原生測試係 **395/395**，catalog parity 通過 346 條固定 routes 同五組 dynamic families，而隔離 LowLevel MCP UI Automation 係 **224/224**。

## Launch and screenshots · 啟動同截圖

**EN —** The earlier managed 15-second capture attempts for `regexcheat` and `reactorsettings` remain `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback was uniform, and graphics capture was unavailable. The fresh 2026-07-16 native Regex Cheatsheet LowLevel MCP capture is also `capture-blocked`: its inspected 852×880 full window and 836×841 client frame were blank and discarded. No PNG was created or reused; UI Automation is behavioral/accessibility evidence, not visual verification. The old Reactor Settings image was removed rather than claimed as current evidence.

**粵語 —** `regexcheat` 同 `reactorsettings` 新鮮 15 秒截圖嘗試都係 `capture-blocked`：`CopyFromScreen` 唔可用、`PrintWindow` fallback 係 uniform，而 graphics capture 亦唔可用。冇 PNG 產生或者重用；兩條 route 之後喺冇操作控制項下都通過 launch-only check。舊 Reactor Settings 圖片已移除，唔會當成最新證據。

[← Developer](Developer.md) · [Reactor Safety & Integrations](Reactor-Safety-and-Integrations.md) · [Smoke Campaign](Smoke-Test-Campaign.md)
