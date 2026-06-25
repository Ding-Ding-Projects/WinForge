# Reactor Test Report · 反應堆測試報告

Standalone scenario test suite for the reactor engine and the fuel / waste / water services, run headless against the real C# code (the WinUI app cannot run headless, so a console harness compiles the pure-C# engine sources directly). Harness: `tests/ReactorSim.Tests` — run with `dotnet run --project tests/ReactorSim.Tests`.

**Build:** `WinForge.sln` → 0 errors. Harness → 0 errors. **Result: 15 / 15 scenarios PASS.**

| # | Scenario · 情景 | Result | Key numbers |
|---|---|---|---|
| 1 | Cold-shutdown held · 冷停堆保持 | ✅ PASS | 5 min held: stays Shutdown, power flat at source 1e-8, fuel 35 °C, no meltdown |
| 2 | Startup integrator stability · 起動積分穩定 | ✅ PASS | finite, 0 sign-flips (backward-Euler does not oscillate) |
| 3 | Known P1–P3 bug reproduced · 重現已知缺陷 | ✅ PASS | fresh core→Run: ρ = +5335 pcm **with all rods in** at cold temp → meltdown in 1 tick |
| 4 | SCRAM mechanism · 緊急停堆機構 | ✅ PASS | rods 10%→100%, Mode=Tripped, IsScrammed latched |
| 5 | SCRAM can't hold (P1–P3 symptom) · 停堆無法壓制 | ✅ PASS | tripped fully-rodded core still runs kinetics → meltdown |
| 6 | Decay heat · 衰變熱 | ✅ PASS | charges to 0.10 clamp, bounded & decaying after trip |
| 7 | Overpower auto-SCRAM · 超功率自動停堆 | ✅ PASS | RPS auto-trips via 'Power Range Flux Hi' |
| 8 | Xenon transient · 氙暫態 | ✅ PASS | restart jump Xe 2.60 → decays 2.60→2.41→2.24 over 2 h |
| 9 | Fuel fabricate + validate / tamper · 燃料製造與驗證 | ✅ PASS | authentic→'ok'; tampered enrichment→'tampered' rejected |
| 10 | Load consumes file · 入料即刪檔 | ✅ PASS | authentic loaded, fresh file deleted, appears in loaded list |
| 11 | Forged harm vs inspect · 偽冒損堆 vs 檢查 | ✅ PASS | unsafe load sev=0.70: dmg 0→8.4, auto-SCRAM, rad=0.35, file consumed; Validate-only does NO harm |
| 12 | Waste cap logic · 廢料上限 | ✅ PASS | 1 GB cap → 100 MB write refused, CapReached, FULL (tested via sparse file) |
| 13 | Waste safety-floor · 磁碟安全下限 | ✅ PASS | floor > free → write refused, FULL (disk never filled) |
| 14 | Water chemistry · 水質 | ✅ PASS | conductivity 45→0.055 µS/cm, O₂→5 ppb, tank fills, product 889 L/min |
| 15 | Water tank empty · 水箱耗盡 | ✅ PASS | availability 1.00→0.02, low-tank alarm; valve-shut→0 |

## What the tests confirm
- The **operator-must-start-up** behavior is solid: a fresh core sits cold and subcritical indefinitely in Shutdown.
- The **backward-Euler integrator is numerically sound** (no NaN/Inf, no oscillation).
- The **fuel cycle is physical and authentic**: fabricate → validate → load-consumes-the-file → forged fuel harms the core (sim-only) while inspection is safe.
- The **waste cap / disk safety-floor logic is correct and never fills the disk** (verified with sparse files — no real multi-GB writes in tests).
- The **water treatment plant** drives chemistry to spec and the reactor degrades when its makeup tank empties.

## Known defect (tracked, expected)
Scenarios 3 & 5 deliberately reproduce the **reactivity-calibration defect** from [reactor-realism-review-001.md](reactor-realism-review-001.md): at cold temperature the Doppler (datum 600 °C) and moderator (datum 305 °C) feedbacks are large and positive, and together with `ExcessBaseline` (+0.0914 dk/k) they exceed full rod worth — so once the core leaves the held Shutdown state it is prompt-supercritical even with all rods inserted, and SCRAM cannot hold it down. This is purely a **calibration** defect (not a numerical or structural one) and is the subject of the ongoing P1–P3 realism work (`reactor-realism-loop`). Until it lands, the reactor is safe to view in Shutdown but runs away if started up.

## Not headless-testable
The reactor-side makeup-water coupling (pressurizer/SG level sag, low-makeup alarm) lives only in the at-power `Update()` path, which the P1–P3 runaway prevents reaching headlessly; the water-plant side (availability degradation) is tested instead, and the gap is documented rather than faked.
