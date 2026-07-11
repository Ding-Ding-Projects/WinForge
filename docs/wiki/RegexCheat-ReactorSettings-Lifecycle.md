# Regex Cheatsheet & Reactor Settings Lifecycle · 正則速查同反應堆設定生命週期

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

## Launch and screenshots · 啟動同截圖

**EN —** Fresh 15-second capture attempts for `regexcheat` and `reactorsettings` are `capture-blocked`: `CopyFromScreen` was unavailable, the `PrintWindow` fallback was uniform, and graphics capture was unavailable. No PNG was created or reused; both routes subsequently passed launch-only checks without a control action. The old Reactor Settings image was removed rather than claimed as current evidence.

**粵語 —** `regexcheat` 同 `reactorsettings` 新鮮 15 秒截圖嘗試都係 `capture-blocked`：`CopyFromScreen` 唔可用、`PrintWindow` fallback 係 uniform，而 graphics capture 亦唔可用。冇 PNG 產生或者重用；兩條 route 之後喺冇操作控制項下都通過 launch-only check。舊 Reactor Settings 圖片已移除，唔會當成最新證據。

[← Developer](Developer.md) · [Reactor Safety & Integrations](Reactor-Safety-and-Integrations.md) · [Smoke Campaign](Smoke-Test-Campaign.md)
