# Reactor Realism Review #001 (ultracode)

_Multi-agent review: 5 dimensions · 40 findings · 40 adversarially confirmed._
_Target reviewed: the **baseline** PWR engine on `feature/nuclear-reactor` (`Services/ReactorSimService.cs`, `Pages/ReactorModule.xaml.cs`). Some items may already be partially addressed by the hyper build (`feature/reactor-hyper`) — the realism loop should re-confirm against current code before implementing._

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
_Generated by the first ultracode realism review. The recurring `reactor-realism-loop` task consumes this
list, implementing one item per run against `feature/reactor-hyper` (verify each against current code first)._
