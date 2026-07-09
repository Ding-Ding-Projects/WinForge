# Reactor Realism Review #001 (ultracode)

> **ARCHIVAL BASELINE — NOT CURRENT DEFECT STATUS.** This review records the original `feature/nuclear-reactor` baseline before the realism work landed. Its findings and proposed fixes are preserved below as historical engineering evidence; do not treat the present-tense wording in the original review as a description of current `main`.
>
> **歷史基準——唔係現時缺陷狀態。** 呢份審查記錄 `feature/nuclear-reactor` 最初基準，時間早過後來嘅寫實度修正。下面原有發現同建議會保留作工程歷史證據；唔好將原文嘅現在式描述當成現時 `main` 嘅狀態。

_Multi-agent review: 5 dimensions · 40 findings · 40 adversarially confirmed._
_Historical target reviewed: the **baseline** PWR engine on `feature/nuclear-reactor` (`Services/ReactorSimService.cs`, `Pages/ReactorModule.xaml.cs`). Current disposition is documented immediately below; the original review follows unchanged._

## Current disposition (2026-07-09) · 現時處理狀態（2026-07-09）

**EN —** P1–P5 are resolved on current `main`. The real headless harness now passes **63/63** scenarios, including cold-shutdown hold, stable startup, fully-rodded shutdown margin, SCRAM hold, decay heat, xenon, every accident enum value, and a sustained high-power thermal-equilibrium regression. The measured high-power plateau held **0.836→0.835 RTP**, fuel **992.4→992.5 °C**, Tavg **293.4 °C**, and RCS pressure **15.46 MPa**, with no emergency cooling, SCRAM, or meltdown.

**粵語 —** 現時 `main` 已完成 P1–P5。真實無介面測試而家 **63/63** 全部通過，包括冷停堆保持、穩定起動、全棒插入停堆裕度、SCRAM 後保持次臨界、衰變熱、氙暫態、全部事故 enum，同持續高功率熱平衡回歸。量度到嘅高功率平台維持 **0.836→0.835 RTP**、燃料 **992.4→992.5 °C**、Tavg **293.4 °C**、RCS 壓力 **15.46 MPa**，期間冇應急冷卻、冇 SCRAM、冇熔毀。

| Review item · 審查項目 | Current disposition · 現時狀態 | Evidence · 證據 |
|---|---|---|
| **P1 — point kinetics · 點動力學** | **Resolved · 已解決** | Six-group backward-Euler integration is in `StepKineticsAndThermal`; startup remains finite with no sign oscillation. · `StepKineticsAndThermal` 已用六組後向歐拉；起動保持有限值，冇正負振盪。 |
| **P2 — reactivity / shutdown margin · 反應性／停堆裕度** | **Resolved · 已解決** | A fresh fully-rodded core is **−1018 pcm** and remains subcritical through startup and SCRAM tests. · 新鮮爐心全棒插入係 **−1018 pcm**，起動同 SCRAM 測試都保持次臨界。 |
| **P3 — thermal energy balance · 熱能平衡** | **Resolved · 已解決** | Fuel→coolant is **4.3 MW/°C**; aggregate SG conductance is **4 + 39·flow MW/°C**; heat capacities are **30/60 MW·s/°C**. The sustained high-power test closes the coupled balance. · 燃料→冷卻劑係 **4.3 MW/°C**；SG 總熱導係 **4 + 39·流量 MW/°C**；熱容量係 **30/60 MW·s/°C**，持續高功率測試已驗證耦合平衡。 |
| **P4 — decay heat · 衰變熱** | **Resolved · 已解決** | ANS-5.1 exponential-group decay heat charges at power and decays after trip. · ANS-5.1 指數群衰變熱會喺功率運行時累積，跳堆後衰減。 |
| **P5+ — xenon, pressurizer, protection, instruments · 氙、穩壓器、保護、儀表** | **Resolved · 已解決** | Xenon worth/transient, saturated-pressurizer pressure, wired 2-of-4 RPS, OTΔT/OPΔT, SG low-low/AFW, turbine-trip cascade, 109% overpower, engineering-unit indications, and 1/M startup guidance are implemented. · 氙價值／暫態、飽和穩壓器壓力、已接駁四取二 RPS、OTΔT／OPΔT、SG 低低水位／AFW、汽輪機跳脫連鎖、109% 超功率、工程單位顯示同 1/M 起動指引都已實作。 |

Current verification details: [Reactor Test Report · 反應堆測試報告](wiki/Reactor-Test-Report.md).

---

## Original review (preserved) · 原始審查（保留）

## Executive summary

The sim *looks* plausible only because it never actually operates at power. Three load-bearing
defects compound into a model that cannot reach a stable, critical, at-power steady state by physics —
it is held together by clamps and feedback choking:

1. **The integrator is numerically broken.** Point kinetics uses explicit forward-Euler at `subDt = 0.02 s`,
   but the stiff prompt mode (eigenvalue ≈ −325/s at critical) needs `h < 6.15 ms`. The step is ~3.25× over
   the stability limit, so any reactivity insertion (even +100 pcm) oscillates sign-to-sign and diverges to
   ~1e7 within ~10 substeps, hits the `_power>50` clamp, and spuriously auto-SCRAMs. The model is only
   "stable" sitting exactly at ρ=0 — i.e. **reactivity transients, the whole point of the sim, do not work.**
2. **The reactivity baseline is non-physical.** `ExcessBaseline = +9140 pcm` makes the core ~+8000 pcm
   (12 dollars) supercritical rods-out, and ~zero shutdown margin rods-in. There is no benign critical band
   in the reachable state space; the only "stabilizer" is fuel melting. The reactivity gauge pins permanently.
3. **The thermal energy balance does not close.** Fuel→coolant conductance (`0.06 MW/°C`) and SG removal
   (~0.98 MW/°C effective) are ~40–70× too small to reject 3411 MW. At true full power the lumped ODEs
   diverge toward ~60,000 °C fuel temp; sane numbers only appear because feedback chokes `_power` to a few
   percent first. Pushed to real full power the plant "melts down" from broken arithmetic, not operator error.

**Secondary gaps:** no decay-heat model (post-trip power → ~0, eliminating the rationale for ECCS/RHR/natural
circulation and every post-trip cooling scenario); xenon worth ~17× too small (−168 pcm vs −2800 pcm) from a
self-cancelling burnup constant + broken normalization; primary pressure is a stiff Tavg-linear map decoupled
from pressurizer saturation and ~1 MPa high (heater-on already exceeds the trip setpoint); protection system
missing primary trips (no OTΔT/OPΔT, no SG low-low/aux-feedwater, no turbine-trip→reactor-trip; overpower trip
set 9 points high at 118%).

**Strength:** the *constants* (Doppler, MTC, boron worth, β, nominal Tavg/pressure) are mostly in physically
plausible ranges. The failures are in *dynamics and structure*: integration stability, reactivity calibration,
energy closure, decay heat, and protection completeness.

## Prioritized fix list (implement in order; 1–3 are the foundation)

### P1 — Stabilize point-kinetics integration (backward Euler)
`ReactorSimService.StepKineticsAndThermal` (~L337–345). Replace forward-Euler with unconditionally-stable
implicit Euler (`PromptLifetime` = Λ):
```csharp
double precursorSum = 0;
for (int i = 0; i < 6; i++) precursorSum += Lambda[i] * _precursor[i];
double newPower = (_power + h * (precursorSum + SourceLevel))
                  / (1.0 - h * (rho - BetaTotal) / PromptLifetime);
for (int i = 0; i < 6; i++)
{
    _precursor[i] = (_precursor[i] + h * (Beta[i] / PromptLifetime) * newPower)
                    / (1.0 + h * Lambda[i]);
    if (_precursor[i] < 0) _precursor[i] = 0;
}
```
Denominator stays positive below prompt-critical → stable at h=0.02 s. +100 pcm then gives a smooth ramp
(~1.25 @1s, ~1.42 @5s, ~1.62 @10s) with no oscillation. **Transformational.**

### P2 — Recalibrate ExcessBaseline + split rod worth into regulating/shutdown banks
Constants (~L73–74), rod term (~L309–313), baseline (~L323).
1. Split lumped `-TotalRodWorth*avg` into per-bank worths: regulating bank D ~300–600 pcm (moves in normal
   control), shutdown banks A/B/C ~6000–8000 pcm parked out (insert only on scram). e.g.
   `RodBankWorth = {0.030,0.030,0.024,0.005}` (~8900 pcm total; D ~500 pcm); `rodRho = -Σ(worth_i*insert_i/100)`.
2. Set `ExcessBaseline ≈ 0.042` so at nominal boron (−1140 pcm) + equilibrium xenon (−2800 pcm) + reference
   temps + shutdown banks out + bank D ~50%, total ρ ≈ 0. Constraints: clean (no-xenon) startup stays
   sub-prompt-critical (need ~1200 ppm + partial insertion); all-banks-in gives ≤ −2000 pcm (target ~−4900).
Creates the first genuine benign critical operating band; makes shutdown margin real. **High.**

### P3 — Close the thermal energy balance
`ReactorSimService.StepThermal` (~L358–401, esp. 363, 369–370).
- Fuel→coolant: `fuelToCoolant = 4.3 * (FuelTemp - avgInternal);` (G_fc = 3411/800 ≈ 4.3 MW/°C → fuel ~1100 °C).
- SG removal: `sgRemoval = (4.0 + 39.0*CoolantFlowFraction)*Math.Max(0, avgInternal - SecondarySatTemp()); sgRemoval *= (0.3 + 0.7*FeedwaterFlow);` (drop the `*0.01`; ~43 MW/°C at full flow).
- Heat caps: `fuelHeatCap ≈ 30`, `coolantHeatCap ≈ 60` (MW·s/°C) → ~7 s fuel, ~1.4 s coolant time constants.
- Tavg stays a derived read-only property; integrate an internal avg, reconstruct Tcold/Thot as now.
Core then reaches real full power: FuelTemp ~1000–1400 °C, Tavg ~305 °C, ΔT ~33–37 °C, balance closed <1%. **High.**

### P4 — Add a decay-heat model (ANS-5.1 exponential sum)
New state + `StepThermal` fuel source (~L361) + `ThermalPowerMW` (~L131). 4-group accumulators charged while
at power, decaying after trip (~6.5–7% at trip → ~1% @2h), folded into the fuel heat source so SBO/LOFW/LOCA
heat-up emergently after SCRAM. Prerequisite for every realistic post-trip cooling scenario. **High.**

### P5+ (from the review, summarized)
Fix xenon worth (correct burnup/normalization → ~−2800 pcm equilibrium, post-trip peak ~9–10 h);
decouple primary pressure from Tavg and model pressurizer saturation (fix the ~1 MPa offset so heater-on
doesn't exceed trip); complete the protection system (OTΔT/OPΔT, SG lo-lo + aux-feedwater auto-start,
turbine-trip→reactor-trip, overpower trip to 109%); wire in the standalone `Services/ReactorRps.cs`
2-of-4 coincidence module. Engineering-unit gauge ranges/limit bands and the approach-to-criticality 1/M
startup guide are also recommended.

---
_Historical note · 歷史註記: generated by the first ultracode realism review for the original baseline. The former `reactor-realism-loop` workflow is no longer an open implementation queue; use the current disposition and the live 63/63 harness before planning further reactor changes. · 呢份文件由首次 ultracode 寫實度審查產生，針對最初基準。以前嘅 `reactor-realism-loop` 已唔再係未完成工作清單；規劃新反應堆改動前，請先睇上面現況同執行現時 63/63 測試。_
