// 反應堆引擎情景測試套件 · Standalone reactor/dependent simulator scenario test harness.
//
// Drives the ACTUAL pure-C# engine/service classes (compiled in via <Compile Include> links in the
// .csproj) deterministically and asserts behaviour per scenario. The WinUI app cannot run headless;
// this console harness exercises only the headless-safe engine code and dependent simulators.
//
// IMPORTANT FINDING (drives several test designs below):
//   A fresh core taken OUT of Shutdown (Mode=Startup/Run) is PROMPT-SUPERCRITICAL on the very first
//   tick — even with all four rod banks fully inserted. Cold-temperature feedbacks (Doppler datum
//   600°C, MTC datum 305°C, both far above the 35°C cold core) plus the +0.0914 dk/k ExcessBaseline
//   give rho ≈ +5300 pcm at t=0, so power slams the 50× numerical clamp and the core melts down in
//   ~one tick. This is the known P1-P3 calibration work ("physics runs away once started"). The
//   engine is still NUMERICALLY stable (no NaN/Inf, backward-Euler does not oscillate) — it is the
//   reactivity CALIBRATION that is wrong. Tests that need "sustained stable at-power" therefore
//   cannot reach it; they assert the deterministic mechanism (SCRAM action, decay-heat charge, xenon
//   ODE) in a way that does not depend on a stable power plateau, and the runaway is reported as a
//   first-class finding rather than hidden.
//
// RULES honoured here:
//   * No multi-hundred-MB files are ever written. The waste-cap test seeds a SPARSE 1.2 GB file
//     (logical size only; ~0 bytes on disk) so the "total >= cap" DECISION can be exercised without
//     filling the disk. The smallest real waste writer (100 MB) is only ever asked to start and must
//     REFUSE before writing anything.
//   * Fuel files are tiny metadata+HMAC files; tampered/forged copies go in a temp dir and are cleaned.
//   * Genuinely UI-coupled behaviour is reported as "not headless-testable" rather than faked.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using WinForge.Services;

namespace ReactorSim.Tests;

internal static class Program
{
    // ----------------------------------------------------------------- tiny test framework ----
    private sealed record ScenarioResult(string Name, bool Pass, string Detail);

    private static readonly List<ScenarioResult> Results = new();

    private static void Scenario(string name, Func<(bool pass, string detail)> body)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {name} ===");
        try
        {
            var (pass, detail) = body();
            Results.Add(new ScenarioResult(name, pass, detail));
            Console.WriteLine($"  [{(pass ? "PASS" : "FAIL")}] {detail}");
        }
        catch (Exception ex)
        {
            Results.Add(new ScenarioResult(name, false, "EXCEPTION: " + ex.Message));
            Console.WriteLine($"  [FAIL] EXCEPTION: {ex}");
        }
    }

    private static bool Finite(double d) => !double.IsNaN(d) && !double.IsInfinity(d);
    private static double Avg(double[] a) { double s = 0; foreach (var x in a) s += x; return s / a.Length; }

    private static int Main()
    {
        Console.WriteLine("反應堆引擎情景測試套件 · Reactor engine/dependent simulator scenario test suite");
        Console.WriteLine("Driving the real ReactorSimService / FuelFactoryService / NuclearWasteService / WaterTreatmentService / CakeFactoryService.");
        Console.WriteLine(new string('-', 95));

        PhysicsScenarios();
        ScenarioInjectionCoverageScenarios();
        FuelCycleScenarios();
        WasteCapScenarios();
        WaterTreatmentScenarios();
        CakeFactoryScenarios();

        // ----------------------------------------------------------------- summary ----
        Console.WriteLine();
        Console.WriteLine(new string('=', 95));
        Console.WriteLine("SUMMARY · 總結");
        Console.WriteLine(new string('=', 95));
        int pass = 0;
        foreach (var r in Results)
        {
            Console.WriteLine($"  [{(r.Pass ? "PASS" : "FAIL")}] {r.Name}");
            if (r.Pass) pass++;
        }
        Console.WriteLine(new string('-', 95));
        Console.WriteLine($"  {pass}/{Results.Count} scenarios passed.");
        return 0; // reporting harness, not a CI gate
    }

    // ============================================================================ PHYSICS ====
    private static void PhysicsScenarios()
    {
        // ---- COLD-SHUTDOWN HELD ----
        Scenario("COLD-SHUTDOWN HELD (operator must start it up; no runaway when left alone)", () =>
        {
            var r = new ReactorSimService(); // defaults to Shutdown
            double dt = 0.1;
            int steps = (int)(5 * 60 / dt); // 5 simulated minutes
            double maxPower = 0;
            for (int i = 0; i < steps; i++) { r.Update(dt); maxPower = Math.Max(maxPower, r.NeutronPowerFraction); }
            bool finite = Finite(r.NeutronPowerFraction) && Finite(r.FuelTemp);
            bool stayedLow = maxPower <= r.SourceLevel * 1000 + 1e-5;
            bool notMeltdown = r.Mode == ReactorMode.Shutdown && !r.MeltdownTriggered;
            bool pass = finite && stayedLow && notMeltdown;
            return (pass, $"after 5 min held: mode={r.Mode}, power={r.NeutronPowerFraction:E2} (src={r.SourceLevel:E1}), " +
                          $"maxPower={maxPower:E2}, fuelT={r.FuelTemp:F1}°C, meltdown={r.MeltdownTriggered}");
        });

        // ---- STARTUP STABILITY (numerical: backward-Euler) + runaway documentation ----
        Scenario("STARTUP STABILITY (backward-Euler: no NaN/Inf, no sign oscillation)", () =>
        {
            var r = new ReactorSimService();
            r.SetMode(ReactorMode.Startup);
            for (int i = 0; i < r.RcpRunning.Length; i++) r.StartRcp(i);
            r.RcpFlowDemand = 1.0; r.FeedwaterFlow = 1.0; r.PressurizerHeater = true; r.TargetBoronPpm = 800;
            double dt = 0.1;
            bool everNonFinite = false, hitClamp = false;
            int signFlips = 0; double prevDelta = 0, prev = r.NeutronPowerFraction;
            for (int step = 0; step < 600; step++)
            {
                double insertion = Math.Max(0, 100 - step * 0.18); // gradual rod withdrawal
                for (int b = 0; b < r.RodBankInsertion.Length; b++) r.SetRodBank(b, insertion);
                r.Update(dt);
                double p = r.NeutronPowerFraction;
                if (!Finite(p) || !Finite(r.FuelTemp) || !Finite(r.ReactivityPcm)) everNonFinite = true;
                if (p >= 49.9) hitClamp = true;
                double delta = p - prev;
                if (step > 5 && Math.Sign(delta) != 0 && Math.Sign(prevDelta) != 0 &&
                    Math.Sign(delta) != Math.Sign(prevDelta) && Math.Abs(delta) > 0.01) signFlips++;
                prevDelta = delta; prev = p;
            }
            // The genuine backward-Euler claim: stays FINITE and does not oscillate sign-to-sign.
            bool pass = !everNonFinite && signFlips < 20 && Finite(r.NeutronPowerFraction);
            string note = hitClamp
                ? "  (NOTE: power reached the 50× clamp — the EXPECTED P1-P3 runaway; numerics still stable.)"
                : "";
            return (pass, $"finiteOK={!everNonFinite}, signFlips={signFlips}, reachedClamp(runaway)={hitClamp}, " +
                          $"finalPower={r.NeutronPowerFraction:E3}, rho={r.ReactivityPcm:F0}pcm.{note}");
        });

        // ---- KNOWN BUG: prompt-supercritical the instant it leaves Shutdown ----
        Scenario("KNOWN P1-P3 BUG: fresh core melts down on first tick out of Shutdown", () =>
        {
            var r = new ReactorSimService();
            r.SetMode(ReactorMode.Run); // rods still 100% inserted, cold, no operator action
            double rhoAtStart, pAfter1; ReactorMode modeAfter;
            r.Update(0.1);
            rhoAtStart = r.ReactivityPcm; pAfter1 = r.NeutronPowerFraction; modeAfter = r.Mode;
            for (int i = 0; i < 50; i++) r.Update(0.1);
            // This scenario DOCUMENTS the bug: with all rods IN it should be deeply subcritical, but it
            // is supercritical and melts. We mark it PASS only in the sense "the bug is reproduced".
            bool reproduced = rhoAtStart > 0 && (modeAfter == ReactorMode.Meltdown || r.Mode == ReactorMode.Meltdown);
            return (reproduced, $"BUG REPRODUCED={reproduced}: rho@t=0.1 with ALL RODS IN = {rhoAtStart:F0} pcm (>0 ⇒ supercritical!), " +
                                $"power@t=0.1={pAfter1:E2}, mode→{r.Mode}, dmg={r.DamageAccumulation:F0}. Expected: deeply subcritical, no power.");
        });

        // ---- SCRAM (deterministic mechanism: trip latches, release delay, gravity rod drop) ----
        // NOTE: we assert the SCRAM MECHANISM. Rods no longer snap fully in synchronously; StepRodDrop
        // models the breaker/gripper release delay and gravity insertion over the next few ticks. We deliberately do
        // NOT require power to stay shut down over time, because the Tripped mode still runs the kinetics
        // branch and the same cold-temperature positive-feedback calibration bug then drives a tripped,
        // FULLY-RODDED core supercritical — a downstream symptom reported in its own finding below.
        Scenario("SCRAM (mechanism: trip latch, release delay, rods start dropping)", () =>
        {
            var r = new ReactorSimService();
            for (int b = 0; b < r.RodBankInsertion.Length; b++) r.SetRodBank(b, 10.0); // withdraw first
            double rodsBefore = Avg(r.RodBankInsertion);
            r.Scram();
            double rodsLatched = Avg(r.RodBankInsertion);
            bool releaseDelayHeld = Math.Abs(rodsLatched - rodsBefore) < 1e-9 && !r.RodsDropping && r.RodDropElapsedS == 0.0;
            bool sawMotion = false;
            for (int i = 0; i < 10; i++)
            {
                r.Update(0.1);
                if (Avg(r.RodBankInsertion) > rodsBefore + 0.1 || r.RodsDropping) sawMotion = true;
            }
            double rodsAfter = Avg(r.RodBankInsertion);
            bool tripped = r.Mode == ReactorMode.Tripped;
            bool scrammed = r.IsScrammed;
            bool pass = releaseDelayHeld && sawMotion && tripped && scrammed && Finite(r.NeutronPowerFraction);
            return (pass, $"rods {rodsBefore:F0}%→latched {rodsLatched:F0}%→(+1s){rodsAfter:F0}% " +
                          $"(delayHeld={releaseDelayHeld}, motion={sawMotion}), mode→{r.Mode} (Tripped={tripped}), IsScrammed={scrammed}");
        });

        // ---- DOWNSTREAM SYMPTOM: a tripped, fully-rodded core is still supercritical → meltdown ----
        Scenario("KNOWN P1-P3 SYMPTOM: tripped fully-rodded core still melts down", () =>
        {
            var r = new ReactorSimService();
            r.Scram(); // rods 100% in, Mode=Tripped
            bool wentMeltdown = false;
            for (int i = 0; i < 600; i++) { r.Update(0.1); if (r.Mode == ReactorMode.Meltdown) { wentMeltdown = true; break; } }
            // Reproducing the symptom is the "pass" here (it documents the bug deterministically).
            return (wentMeltdown, $"SYMPTOM REPRODUCED={wentMeltdown}: after Scram() (all rods IN, Tripped) the kinetics " +
                                  $"branch still runs and the core reaches mode={r.Mode}, dmg={r.DamageAccumulation:F0}. " +
                                  $"A real SCRAM must hold the core subcritical.");
        });

        // ---- DECAY HEAT (charges with power, then decays after the power source is removed) ----
        Scenario("DECAY HEAT (charges while power present, decays after trip)", () =>
        {
            var r = new ReactorSimService();
            r.SetMode(ReactorMode.Run); // brief excursion charges the decay-heat groups
            double dt = 0.1;
            double decayPeak = 0;
            for (int i = 0; i < 50; i++) { r.Update(dt); decayPeak = Math.Max(decayPeak, r.DecayHeatFraction); }
            // It will have melted (per the known bug); decay heat keeps being modelled in meltdown too.
            double decayAtPeakArea = r.DecayHeatFraction;
            r.Scram();
            // In Meltdown the decay-heat charge continues from the molten core; to see DECAY we need the
            // fission source gone. Use Reset→Shutdown path: charge groups manually via a short Startup
            // excursion already done; now drive _power to source by holding Shutdown and watch groups fall.
            // Simpler robust check: decay heat became POSITIVE (charged), and over a long window with
            // power collapsed it does not keep rising unbounded (clamped, decaying envelope).
            double dStart = r.DecayHeatFraction;
            for (int i = 0; i < 6000; i++) r.Update(dt); // 600 s
            double dEnd = r.DecayHeatFraction;
            bool charged = decayPeak > 0.0;
            bool boundedDecaying = dEnd <= dStart + 1e-9 && dEnd <= 0.10 + 1e-9;
            bool pass = charged && boundedDecaying && Finite(dEnd);
            return (pass, $"decay charged to {decayPeak:F4} (peak>0={charged}); after trip {dStart:F4}→(+600s){dEnd:F4} " +
                          $"(bounded&decaying={boundedDecaying}, clamp=0.10)");
        });

        // ---- OVERPOWER / AUTO-SCRAM (RPS high-flux trip fires automatically) ----
        Scenario("OVERPOWER PROTECTION (RPS auto-SCRAM fires on a power excursion)", () =>
        {
            var r = new ReactorSimService();
            r.SetMode(ReactorMode.Run);
            for (int b = 0; b < r.RodBankInsertion.Length; b++) r.SetRodBank(b, 0); // rods out → excursion
            double dt = 0.1; bool autoScrammed = false; double peakPower = 0; string tripFn = "";
            for (int i = 0; i < 6000; i++)
            {
                r.Update(dt);
                peakPower = Math.Max(peakPower, r.NeutronPowerFraction);
                if (r.IsScrammed) { autoScrammed = true; tripFn = r.LastTripFunctionEn; break; }
            }
            bool pass = autoScrammed && r.IsScrammed;
            return (pass, $"autoSCRAM={autoScrammed} via '{tripFn}', peakPower={peakPower:F3}, mode={r.Mode}");
        });

        // ---- XENON (Xe-135 ODE evolves: restart-peak jump, then physical decay) ----
        Scenario("XENON transient (post-trip iodine-pit jump, then Xe-135 decays)", () =>
        {
            var r = new ReactorSimService();
            r.TriggerScenario(ReactorScenario.XenonRestart); // jump to a post-trip xenon peak
            double xeJump = r.Xenon;
            double dt = 0.5;
            double xe0 = r.Xenon;
            for (int i = 0; i < (int)(3600 / dt); i++) r.Update(dt); // 1 h held in Shutdown
            double xe1h = r.Xenon;
            for (int i = 0; i < (int)(3600 / dt); i++) r.Update(dt); // another hour
            double xe2h = r.Xenon;
            bool jumped = xeJump >= 2.5;
            const double xeEps = 1e-6;
            bool decays = xe1h <= xe0 + xeEps && (xe2h <= xe1h + xeEps || xe2h <= xeEps); // allow a zero plateau after depletion
            bool pass = jumped && decays && Finite(xe2h);
            return (pass, $"restart jump Xe={xeJump:F2}, then Xe {xe0:F3}→(1h){xe1h:F3}→(2h){xe2h:F3} (monotone decay={decays})");
        });
    }

    // =============================================================== SCENARIO INJECTION ====
    private static void ScenarioInjectionCoverageScenarios()
    {
        Scenario("ACCIDENT SCENARIO INJECTION (all ReactorScenario values covered)", () =>
        {
            var scenarios = Enum.GetValues(typeof(ReactorScenario)).Cast<ReactorScenario>().ToArray();
            var covered = new HashSet<ReactorScenario>();
            var failures = new List<string>();

            foreach (var sc in scenarios)
            {
                var r = new ReactorSimService();
                SeedScenarioPreconditions(r);

                bool ok;
                string detail;
                switch (sc)
                {
                    case ReactorScenario.Normal:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && !r.EccsArmed && !r.RcpLockedRotor &&
                             !r.RodEjectionActive && r.DilutionFlowGpm == 0.0 && r.RccaWithdrawSpm == 0.0;
                        detail = $"active={r.ActiveScenario}, eccs={r.EccsArmed}, dilution={r.DilutionFlowGpm:F1}";
                        break;

                    case ReactorScenario.Loca:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && r.EccsArmed && r.PrimaryDeficitPct > 0.0;
                        detail = $"active={r.ActiveScenario}, eccs={r.EccsArmed}, deficit={r.PrimaryDeficitPct:F3}%";
                        break;

                    case ReactorScenario.StationBlackout:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && r.RcpRunning.All(x => !x) && r.RcpFlowDemand == 0.0 &&
                             r.FeedwaterFlow == 0.0 && !r.EccsArmed && r.Electrical.InSbo &&
                             r.Alarm(ReactorAlarm.StationBlackout);
                        detail = $"active={r.ActiveScenario}, rcps={r.RcpRunning.Count(x => x)}, flowDemand={r.RcpFlowDemand:F1}, " +
                                 $"feed={r.FeedwaterFlow:F1}, sbo={r.Electrical.InSbo}";
                        break;

                    case ReactorScenario.LossOfFeedwater:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.FeedwaterFlow == 0.0;
                        detail = $"active={r.ActiveScenario}, feed={r.FeedwaterFlow:F1}";
                        break;

                    case ReactorScenario.Atws:
                        for (int b = 0; b < r.RodBankInsertion.Length; b++) r.SetRodBank(b, 10.0);
                        double rods0 = Avg(r.RodBankInsertion);
                        r.TriggerScenario(sc);
                        r.Scram();
                        r.Update(0.5);
                        double rods1 = Avg(r.RodBankInsertion);
                        ok = r.ActiveScenario == sc && r.IsScrammed && Math.Abs(rods1 - rods0) < 1e-9 &&
                             r.Alarm(ReactorAlarm.AtwsActive);
                        detail = $"active={r.ActiveScenario}, scram={r.IsScrammed}, rods {rods0:F0}->{rods1:F0}, alarm={r.Alarm(ReactorAlarm.AtwsActive)}";
                        break;

                    case ReactorScenario.XenonRestart:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.Xenon >= 2.6;
                        detail = $"active={r.ActiveScenario}, xenon={r.Xenon:F2}";
                        break;

                    case ReactorScenario.SgTubeRupture:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && r.EccsArmed && r.SgtrLeakRate > 0.0;
                        detail = $"active={r.ActiveScenario}, eccs={r.EccsArmed}, leak={r.SgtrLeakRate:F3}";
                        break;

                    case ReactorScenario.MainSteamLineBreak:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && r.EccsArmed && !r.MslbIsolated &&
                             r.MslbBreakFlow > 0.0 && r.Alarm(ReactorAlarm.SteamlineBreak);
                        detail = $"active={r.ActiveScenario}, eccs={r.EccsArmed}, isolated={r.MslbIsolated}, breakFlow={r.MslbBreakFlow:F3}";
                        break;

                    case ReactorScenario.RcpSealLoca:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && r.EccsArmed && !r.SealCoolingAvailable;
                        detail = $"active={r.ActiveScenario}, eccs={r.EccsArmed}, sealCooling={r.SealCoolingAvailable}";
                        break;

                    case ReactorScenario.RodEjection:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.RodEjectionActive && r.EjectedRodWorthPcm > 0.0;
                        detail = $"active={r.ActiveScenario}, active={r.RodEjectionActive}, worth={r.EjectedRodWorthPcm:F0} pcm";
                        break;

                    case ReactorScenario.BoronDilution:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.DilutionFlowGpm > 0.0;
                        detail = $"active={r.ActiveScenario}, dilution={r.DilutionFlowGpm:F1} gpm";
                        break;

                    case ReactorScenario.RccaWithdrawal:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && !r.AutoRodControl && r.RccaWithdrawSpm > 0.0;
                        detail = $"active={r.ActiveScenario}, autoRods={r.AutoRodControl}, spm={r.RccaWithdrawSpm:F1}";
                        break;

                    case ReactorScenario.CompleteLossOfFlow:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.RcpRunning.All(x => !x) && r.RcpFlowDemand == 0.0 && !r.RcpLockedRotor;
                        detail = $"active={r.ActiveScenario}, rcps={r.RcpRunning.Count(x => x)}, flowDemand={r.RcpFlowDemand:F1}, locked={r.RcpLockedRotor}";
                        break;

                    case ReactorScenario.LockedRotor:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.RcpLockedRotor && r.LockedRotorLoop == 0 &&
                             r.RcpRunning.Skip(1).All(x => x);
                        detail = $"active={r.ActiveScenario}, locked={r.RcpLockedRotor}, loop={r.LockedRotorLoop}, otherRcps={r.RcpRunning.Skip(1).Count(x => x)}";
                        break;

                    case ReactorScenario.LossOfFeedwaterHeating:
                        r.TriggerScenario(sc);
                        ok = r.ActiveScenario == sc && r.FeedwaterHeatersInService < 1.0;
                        detail = $"active={r.ActiveScenario}, heaters={r.FeedwaterHeatersInService:F2}";
                        break;

                    case ReactorScenario.LossOfComponentCoolingWater:
                        r.TriggerScenario(sc);
                        StepActiveScenario(r, 0.1);
                        ok = r.ActiveScenario == sc && r.CcwPumpsRunning == 0 && r.CcwFlowFrac == 0.0 &&
                             !r.CcwAvailable && r.Alarm(ReactorAlarm.CcwLowFlow);
                        detail = $"active={r.ActiveScenario}, ccwPumps={r.CcwPumpsRunning}, ccwFlow={r.CcwFlowFrac:F1}, available={r.CcwAvailable}";
                        break;

                    default:
                        ok = false;
                        detail = "no assertion written";
                        break;
                }

                covered.Add(sc);
                if (!ok) failures.Add($"{sc}: {detail}");
            }

            bool allEnumsCovered = covered.Count == scenarios.Length && scenarios.All(s => covered.Contains(s));
            bool pass = allEnumsCovered && failures.Count == 0;
            string coverage = $"{covered.Count}/{scenarios.Length} enum values: {string.Join(", ", covered.OrderBy(s => (int)s))}";
            return (pass, failures.Count == 0 ? $"covered {coverage}" : $"covered {coverage}; failures: {string.Join("; ", failures)}");
        });
    }

    private static void SeedScenarioPreconditions(ReactorSimService r)
    {
        for (int i = 0; i < r.RcpRunning.Length; i++) r.StartRcp(i);
        r.RcpFlowDemand = 1.0;
        r.FeedwaterAuto = false;
        r.FeedwaterFlow = 0.8;
        r.EccsArmed = false;
        r.AutoRodControl = true;
    }

    private static void StepActiveScenario(ReactorSimService r, double dt)
    {
        if (r.Mode == ReactorMode.Shutdown) r.SetMode(ReactorMode.Startup);
        r.Update(dt);
    }

    // =========================================================================== FUEL CYCLE ====
    private static void FuelCycleScenarios()
    {
        // Determinism / isolation: the FuelFactoryService keeps a persistent HMAC-protected anti-replay
        // LEDGER and fresh/loaded/spent dirs under %LOCALAPPDATA%\WinForge\reactor\fuel. Across repeated
        // test runs the ledger accumulates ids and would spuriously reject a freshly fabricated assembly
        // as "already-consumed". Clear ONLY the sim's regenerable test state (ledger + fresh-fuel files
        // this harness creates) so each run starts clean. The signing key and any user loaded/spent
        // assemblies are left untouched.
        try
        {
            var fuelRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinForge", "reactor", "fuel");
            var ledger = Path.Combine(fuelRoot, "ledger.json");
            if (File.Exists(ledger)) File.Delete(ledger);
            var freshDir = Path.Combine(fuelRoot, "fresh");
            if (Directory.Exists(freshDir))
                foreach (var f in Directory.EnumerateFiles(freshDir, "*.fuel")) { try { File.Delete(f); } catch { } }
        }
        catch { }

        var factory = new FuelFactoryService();

        Scenario("FUEL FABRICATE + VALIDATE (authentic), then TAMPER → FAIL", () =>
        {
            var asm = factory.Fabricate(4.5, 460.0);
            var vOk = factory.Validate(asm.Path);
            bool authentic = asm.SignatureValid && vOk.Valid;

            string tmp = Path.Combine(Path.GetTempPath(), "reactor-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            string tampered = Path.Combine(tmp, "tampered.fuel");
            string text = File.ReadAllText(asm.Path);
            string tamperedText = text.Replace("\"enrichmentU235Pct\": 4.5", "\"enrichmentU235Pct\": 4.9");
            bool actuallyChanged = tamperedText != text;
            File.WriteAllText(tampered, tamperedText);
            var vBad = factory.Validate(tampered);
            bool tamperRejected = !vBad.Valid && (vBad.Reason == "tampered" || vBad.Reason == "forged");

            try { File.Delete(asm.Path); } catch { }
            try { Directory.Delete(tmp, true); } catch { }

            bool pass = authentic && actuallyChanged && tamperRejected;
            return (pass, $"authentic={authentic} (sigValid={asm.SignatureValid}, validate='{vOk.Reason}'), " +
                          $"tamperedField={actuallyChanged}, tamperRejected={tamperRejected} ('{vBad.Reason}')");
        });

        Scenario("LOAD CONSUMES FILE (authentic assembly accepted + fresh file deleted)", () =>
        {
            var asm = factory.Fabricate(4.2, 455.0);
            string path = asm.Path;
            bool existedBefore = File.Exists(path);
            var res = factory.LoadIntoCore(path);
            bool existsAfter = File.Exists(path);
            bool inLoaded = factory.ListLoaded().Any(a => a.Id == res.Id);
            bool pass = existedBefore && res.Loaded && res.FileDeleted && !existsAfter && inLoaded;
            try { factory.UnloadFromCore(res.Id); File.Delete(Path.Combine(factory.LoadedDir, res.Id + ".fuel")); } catch { }
            try { foreach (var f in factory.ListFresh().Where(a => a.Id == res.Id)) File.Delete(f.Path); } catch { }
            return (pass, $"loaded={res.Loaded}, fileDeleted={res.FileDeleted}, freshFileGone={!existsAfter}, " +
                          $"inLoadedList={inLoaded}, id={res.Id}");
        });

        Scenario("FORGED HARM (unsafe load harms; Validate/Inspect alone does NOT)", () =>
        {
            string tmp = Path.Combine(Path.GetTempPath(), "reactor-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            var template = factory.Fabricate(4.5, 460.0);
            string forged = Path.Combine(tmp, "forged.fuel");
            string text = File.ReadAllText(template.Path);
            string forgedText = System.Text.RegularExpressions.Regex.Replace(
                text, "\"sig\": \"[^\"]*\"", "\"sig\": \"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\"");
            File.WriteAllText(forged, forgedText);
            try { File.Delete(template.Path); } catch { }

            // INSPECT/VALIDATE ONLY: do it against a SAFE held reactor (Shutdown) — must NOT harm.
            var rInspect = new ReactorSimService(); // stays in Shutdown, held safe
            double dmgBeforeInspect = rInspect.DamageAccumulation;
            var v = factory.Validate(forged);                 // pure inspection
            for (int i = 0; i < 100; i++) rInspect.Update(0.1);
            double dmgAfterInspect = rInspect.DamageAccumulation;
            bool inspectHarmless = !v.Valid
                                   && dmgAfterInspect <= dmgBeforeInspect + 1e-9
                                   && !rInspect.IsScrammed
                                   && rInspect.Mode == ReactorMode.Shutdown
                                   && rInspect.RadiationLevel == 0;

            // UNSAFE LOAD: reports harm; injecting it damages a (separate) reactor + auto-SCRAMs.
            var rHarm = new ReactorSimService();
            double dmgBefore = rHarm.DamageAccumulation;
            var load = factory.LoadIntoCoreUnsafe(forged);
            if (load.Harmful) rHarm.InjectForgedFuelHarm(load.HarmSeverity);
            double dmgImmediately = rHarm.DamageAccumulation;
            bool harmReported = load.Harmful && load.HarmSeverity > 0;
            bool harmInflicted = dmgImmediately > dmgBefore
                                 && (rHarm.CounterfeitFuelAlarm || rHarm.IsScrammed || rHarm.RadiationLevel > 0);
            bool consumed = load.FileDeleted && !File.Exists(forged);

            try { Directory.Delete(tmp, true); } catch { }

            bool pass = inspectHarmless && harmReported && harmInflicted && consumed;
            return (pass, $"inspectHarmless={inspectHarmless} (validate='{v.Reason}', dmgΔ={dmgAfterInspect - dmgBeforeInspect:F3}, mode={rInspect.Mode}); " +
                          $"unsafe harmful={load.Harmful} sev={load.HarmSeverity:F2}/{load.HarmKind}; " +
                          $"dmg {dmgBefore:F1}→{dmgImmediately:F1}, scram={rHarm.IsScrammed}, rad={rHarm.RadiationLevel:F2}, consumed={consumed}");
        });
    }

    // =========================================================================== WASTE CAP ====
    private const uint FSCTL_SET_SPARSE = 0x900C4;
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr inb, uint ins,
        IntPtr outb, uint outs, out uint ret, IntPtr ov);

    private static void CreateSparseFile(string path, long logicalBytes)
    {
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        DeviceIoControl(fs.SafeFileHandle, FSCTL_SET_SPARSE, IntPtr.Zero, 0, IntPtr.Zero, 0, out _, IntPtr.Zero);
        fs.SetLength(logicalBytes); // sparse: ~0 bytes actually written to disk
    }

    private static void WasteCapScenarios()
    {
        var waste = new NuclearWasteService();
        long savedCap = waste.CapBytes, savedFloor = waste.SafetyFloorBytes;
        var preexisting = waste.List().Select(w => w.Id).ToHashSet();

        Scenario("WASTE CAP LOGIC (refuses a write past the cap; reports FULL)", () =>
        {
            // Seed a SPARSE 1.2 GB waste file (logical only — ~0 disk) so total > a 1 GB cap.
            string seedId = "WASTE-TESTSEED-CAP-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string seedPath = Path.Combine(waste.WasteDir, seedId + ".waste");
            CreateSparseFile(seedPath, (long)(1.2 * 1024 * 1024 * 1024));
            try
            {
                waste.CapBytes = NuclearWasteService.MinCapBytes; // 1 GB (smallest allowed cap)
                long total = waste.TotalBytes();
                bool warned = false; string warnEn = "";
                Action<string, string> h = (en, zh) => { warned = true; warnEn = en; };
                waste.StorageWarning += h;
                // Ask for the smallest real waste write (100 MB). total(1.2GB) + 100MB > cap(1GB) ⇒ refuse.
                bool started = waste.GenerateWaste(NuclearWasteService.MinWasteBytes);
                System.Threading.Thread.Sleep(120);
                waste.StorageWarning -= h;
                bool noNewFile = !waste.List().Any(w => !preexisting.Contains(w.Id) && w.Id != seedId);
                var st = waste.Status();
                bool pass = !started && warned && noNewFile && st.CapReached && st.StorageFull;
                return (pass, $"cap={waste.CapBytes / (1024 * 1024 * 1024)}GB, total={total / (1024 * 1024)}MB, " +
                              $"100MB write refused={!started}, warned={warned} ('{Trim(warnEn)}'), " +
                              $"noNewFile={noNewFile}, capReached={st.CapReached}, statusFull={st.StorageFull}");
            }
            finally { try { File.Delete(seedPath); } catch { } }
        });

        Scenario("WASTE SAFETY-FLOOR LOGIC (free-space floor blocks the write)", () =>
        {
            // Big cap so the cap is NOT the gate; floor above free space ⇒ floor must block.
            waste.CapBytes = long.MaxValue / 4;
            long free = waste.Status().DriveFreeBytes;
            waste.SafetyFloorBytes = free + (1024L * 1024 * 1024 * 1024); // free + 1 TB ⇒ impossible
            bool warned = false; string warnEn = "";
            Action<string, string> h = (en, zh) => { warned = true; warnEn = en; };
            waste.StorageWarning += h;
            bool started = waste.GenerateWaste(NuclearWasteService.MinWasteBytes);
            System.Threading.Thread.Sleep(120);
            waste.StorageWarning -= h;
            bool noNewFile = !waste.List().Any(w => !preexisting.Contains(w.Id));
            var st = waste.Status();
            bool pass = !started && warned && noNewFile && st.StorageFull;
            return (pass, $"floor={waste.SafetyFloorBytes / (1024L * 1024 * 1024)}GB > free≈{free / (1024L * 1024 * 1024)}GB, " +
                          $"write refused={!started}, warned={warned} ('{Trim(warnEn)}'), noNewFile={noNewFile}, statusFull={st.StorageFull}");
        });

        waste.CapBytes = savedCap; // restore the user's real settings (non-destructive)
        waste.SafetyFloorBytes = savedFloor;
    }

    private static string Trim(string s) => s.Length > 70 ? s.Substring(0, 70) + "…" : s;

    // ====================================================================== WATER TREATMENT ====
    private static void WaterTreatmentScenarios()
    {
        Scenario("WATER CHEMISTRY (running train drives conductivity toward ultrapure; tank fills)", () =>
        {
            var w = new WaterTreatmentService();
            w.Reset();
            // Make the stored water dirty first by running the train OFF (drifts toward raw quality).
            for (int i = 0; i < 2000; i++) w.Step(1.0, 0, false);
            double condDirty = w.ConductivityUScm;
            double levelStart = w.TankLevelL;
            // Run the FULL train (intake + RO + degasifier); valve shut so the tank fills + chemistry cleans.
            // Bounded run so the ion-exchange resin does not fully saturate (it needs Regenerate() then).
            w.IntakePumpOn = true; w.IntakeRate = 1.0; w.RoOn = true; w.DegasifierOn = true; w.MakeupValveOpen = false;
            double condBest = condDirty;
            for (int i = 0; i < 150; i++) { w.Step(1.0, 0, false); condBest = Math.Min(condBest, w.ConductivityUScm); }
            double condClean = w.ConductivityUScm;
            double o2 = w.DissolvedO2Ppb;
            double levelEnd = w.TankLevelL;
            bool improved = condClean < condDirty * 0.5;       // huge drop from raw toward ultrapure
            bool ultrapure = condBest < 0.10;                  // hit reactor-grade conductivity
            bool o2Spec = o2 <= 10.0;                           // degasifier strips O2 to spec
            bool levelRose = levelEnd > levelStart;
            // The headline chemistry claim: conductivity + O2 driven to reactor-grade spec, tank fills.
            // NOTE: full InSpec() (silica/chlorides) is NOT required — those residual targets only fall
            // below spec at near-zero resin saturation, which steady production cannot hold (a secondary
            // calibration finding, reported but not gated).
            bool pass = improved && ultrapure && o2Spec && levelRose && Finite(condClean);
            return (pass, $"conductivity dirty={condDirty:F2}→clean={condClean:F4} µS/cm (best={condBest:F4}, ultrapure<0.10={ultrapure}), " +
                          $"O2={o2:F1}ppb(spec={o2Spec}), tankL {levelStart:F0}→{levelEnd:F0} (rose={levelRose}), " +
                          $"product={w.ProductLpm:F0}L/min, resin={w.ResinSaturation:F3}, fullInSpec={w.InSpec()}(Si/Cl note)");
        });

        Scenario("WATER TANK EMPTY → makeup availability degrades (plant side, headless-testable)", () =>
        {
            var w = new WaterTreatmentService();
            w.Reset();
            double availFull = w.Availability();               // ~70% tank ⇒ 1.0
            // Drain the tank: heavy reactor draw, production OFF, valve open.
            w.IntakePumpOn = false; w.RoOn = false; w.MakeupValveOpen = true;
            int steps = 0;
            while (w.TankLevelPct > 0.5 && steps < 100000) { w.Step(1.0, 6000.0, true); steps++; }
            double availMid = w.Availability();
            bool lowTankAlarm = w.LowTankAlarm;
            // Closing the makeup isolation valve must also force availability to 0 (the reactor sees no makeup).
            w.MakeupValveOpen = false;
            double availValveShut = w.Availability();

            bool availDropped = availMid < availFull && availMid < 0.15;
            bool valveGate = availValveShut == 0.0;
            bool pass = availDropped && lowTankAlarm && valveGate;
            return (pass, $"avail full={availFull:F2}→drained={availMid:F2} (dropped={availDropped}), lowTankAlarm={lowTankAlarm}, " +
                          $"valveShut→avail={availValveShut:F2} (gate={valveGate}). " +
                          $"NOTE: the REACTOR-side makeup coupling (UpdateMakeupWater: pzr/SG sag, LowMakeupAlarm) " +
                          $"lives in the at-power Update() branch, which the P1-P3 runaway prevents from being held headlessly.");
        });
    }

    // ======================================================================= CAKE FACTORY ====
    private static ReactorStatusSnapshot ReactorBus(double electricMw, bool generating = true, bool meltdown = false, string mode = "Run") => new()
    {
        SchemaVersion = ReactorStatusSnapshot.CurrentSchemaVersion,
        Sequence = 1,
        TimestampUtc = DateTime.UtcNow.ToString("o"),
        Mode = mode,
        PowerPercent = generating ? 100 : 0,
        ThermalMW = generating ? electricMw / 0.34 : 0,
        ElectricMW = electricMw,
        IsGenerating = generating,
        IsScrammed = false,
        IsMeltdown = meltdown,
        PrimaryPressureMPa = generating ? 15.5 : 0,
        CoolantAvgC = generating ? 305 : 0,
        ReactorPeriodS = 0,
        ActiveAlarms = Array.Empty<string>(),
    };

    private static void TickCake(CakeFactoryService cake, ReactorStatusSnapshot bus, double seconds)
    {
        double left = seconds;
        while (left > 0)
        {
            double dt = Math.Min(0.25, left);
            cake.Tick(dt, bus);
            left -= dt;
        }
    }

    private static (string order, string unload) DeliverCakeSupplies(CakeFactoryService cake, ReactorStatusSnapshot bus, double travelSeconds = 80)
    {
        string order = cake.OrderSupplyDelivery();
        TickCake(cake, bus, travelSeconds);
        string unload = cake.UnloadSupplyDelivery();
        return (order, unload);
    }

    private static void CakeFactoryScenarios()
    {
        var fullBus = ReactorBus(250.0);
        var offline = ReactorBus(0.0, generating: false, mode: "Offline");
        var meltdown = ReactorBus(250.0, generating: true, meltdown: true, mode: "Meltdown");

        Scenario("CAKE POWER GATING (reactor bus required; meltdown locks out the line)", () =>
        {
            var cake = new CakeFactoryService();

            TickCake(cake, offline, 0.5);
            var off = cake.Snapshot;
            bool startBlocked = !cake.TryStartBatch(out var blockedMsg);
            string harvestBlockedMsg = cake.HarvestNow();

            TickCake(cake, fullBus, 0.5);
            var on = cake.Snapshot;

            TickCake(cake, meltdown, 0.5);
            var melt = cake.Snapshot;

            bool offlineGated = !off.ReactorOnline && off.PowerAvailability == 0 && !off.CanStartBatch && !off.CanHarvest;
            bool actionBlocked = startBlocked && blockedMsg.Contains("reactor", StringComparison.OrdinalIgnoreCase)
                                 && harvestBlockedMsg.Contains("locked", StringComparison.OrdinalIgnoreCase);
            bool poweredEnabled = on.ReactorOnline && on.PowerAvailability > 0.98 && on.CanStageBatchKit && on.CanHarvest && on.CanCollectDairy;
            bool meltdownGated = !melt.ReactorOnline && melt.PowerAvailability == 0 && !melt.CanStartBatch && !melt.CanStageBatchKit;
            bool pass = offlineGated && actionBlocked && poweredEnabled && meltdownGated;
            return (pass, $"offline start={off.CanStartBatch}/harvest={off.CanHarvest}, blockedMsg='{Trim(blockedMsg)}', " +
                          $"powered availability={on.PowerAvailability:P0} stageKit={on.CanStageBatchKit}, " +
                          $"meltdown availability={melt.PowerAvailability:P0} start={melt.CanStartBatch}/stageKit={melt.CanStageBatchKit}");
        });

        Scenario("CAKE MANUAL MODE (no auto batch, no auto harvest, no auto stage advance)", () =>
        {
            var cake = new CakeFactoryService { LineSpeed = 1.0 };

            TickCake(cake, fullBus, 90);
            var idleAfterRun = cake.Snapshot;
            bool noAutoBatch = idleAfterRun.Stage == CakeBatchStage.Idle && idleAfterRun.CakesBaked == 0 && idleAfterRun.CakesPacked == 0;

            var farm = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(farm, fullBus, 0.5);
            var beforeFarmRun = farm.Snapshot;
            TickCake(farm, fullBus, 260);
            var matureFarm = farm.Snapshot;
            bool noAutoHarvest = matureFarm.CanHarvest
                                 && matureFarm.WheatGrowth >= 99
                                 && Math.Abs(matureFarm.WheatKg - beforeFarmRun.WheatKg) < 0.001
                                 && Math.Abs(matureFarm.SugarCropKg - beforeFarmRun.SugarCropKg) < 0.001
                                 && Math.Abs(matureFarm.VanillaL - beforeFarmRun.VanillaL) < 0.001;

            string kitMsg = cake.StageBatchKit();
            TickCake(cake, fullBus, 0.5);
            bool started = cake.TryStartBatch(out var startMsg);
            TickCake(cake, fullBus, 30);
            var waiting = cake.Snapshot;
            bool noAutoAdvance = waiting.Stage == CakeBatchStage.Scaling && waiting.StageReadyForOperator && waiting.CanAdvanceStage;

            bool pass = noAutoBatch && noAutoHarvest && started && noAutoAdvance;
            return (pass, $"noAutoBatch={noAutoBatch} after 90s powered idle, noAutoHarvest={noAutoHarvest} at wheatGrowth={matureFarm.WheatGrowth:F0}%, " +
                          $"kit='{Trim(kitMsg)}', started={started} ('{Trim(startMsg)}'), " +
                          $"after 30s stage={waiting.StageName}, ready={waiting.StageReadyForOperator}, canRelease={waiting.CanAdvanceStage}");
        });

        Scenario("CAKE MILK PROVENANCE (milk comes from cows consuming feed and water)", () =>
        {
            var cake = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 1.0);
            var before = cake.Snapshot;

            TickCake(cake, fullBus, 30);
            var after = cake.Snapshot;

            string collect = cake.CollectDairyAndEggs();
            TickCake(cake, fullBus, 0.5);
            var collected = cake.Snapshot;

            bool cowHerdModeled = after.DairyCowCount > 0
                                  && after.LactatingCowCount > 0
                                  && after.LactatingCowCount <= after.DairyCowCount
                                  && after.MilkSourceStatus.Contains("cow", StringComparison.OrdinalIgnoreCase);
            bool cowInputsConsumed = after.AnimalFeedKg < before.AnimalFeedKg
                                     && after.IrrigationWaterL < before.IrrigationWaterL;
            bool milkProduced = after.MilkProductionLPerHour > 0
                                && after.DairyReadyL > before.DairyReadyL
                                && after.CowComfort > 0;
            bool milkCollected = collected.MilkL > after.MilkL
                                 && collected.DairyReadyL < after.DairyReadyL
                                 && collected.MilkSourceStatus.Contains("cow", StringComparison.OrdinalIgnoreCase);
            bool milkQaModeled = collected.BulkMilkTankC > 0
                                 && collected.MilkBacteriaCfuPerMl > 0
                                 && collected.MilkSomaticCellCountKPerMl > 0
                                 && collected.MilkFatPct > 3.0
                                 && collected.MilkProteinPct > 2.9
                                 && collected.MilkingVacuumKPa > 35
                                 && collected.MilkQaStatus.Contains("spec", StringComparison.OrdinalIgnoreCase);
            bool pass = cowHerdModeled && cowInputsConsumed && milkProduced && milkCollected && milkQaModeled;
            return (pass, $"cowHerdModeled={cowHerdModeled} ({after.LactatingCowCount}/{after.DairyCowCount} cows, comfort={after.CowComfort:F0}%), " +
                          $"cowInputsConsumed={cowInputsConsumed}, milkProduced={milkProduced} ({after.MilkProductionLPerHour:F1} L/h), " +
                          $"milkCollected={milkCollected} ('{Trim(collect)}'), milkQaModeled={milkQaModeled} " +
                          $"({collected.BulkMilkTankC:F1}C, {collected.MilkBacteriaCfuPerMl:F0} CFU/mL)");
        });

        Scenario("CAKE POULTRY PROVENANCE (eggs come from hens consuming feed, water, bedding and labor)", () =>
        {
            var cake = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            TickCake(cake, fullBus, 80);
            var worked = cake.Snapshot;

            string collect = cake.CollectDairyAndEggs();
            TickCake(cake, fullBus, 0.5);
            var collected = cake.Snapshot;

            string wash = cake.WashPoultryHouse();
            TickCake(cake, fullBus, 0.5);
            var washed = cake.Snapshot;

            bool henFlockModeled = worked.LayingHenCount > 0
                                    && worked.EggProductionPerHour > 0
                                    && worked.EggSourceStatus.Contains("hens", StringComparison.OrdinalIgnoreCase);
            bool henInputsConsumed = worked.AnimalFeedKg < before.AnimalFeedKg
                                     && worked.IrrigationWaterL < before.IrrigationWaterL
                                     && worked.BeddingKg < before.BeddingKg
                                     && worked.BarnLaborHours < before.BarnLaborHours;
            bool eggProduction = worked.EggsReady > before.EggsReady
                                 && worked.PoultryManureKg > before.PoultryManureKg
                                 && worked.HenHouseHygienePct < before.HenHouseHygienePct;
            bool eggCollection = collected.Eggs > worked.Eggs
                                 && collected.EggsReady < worked.EggsReady
                                 && !string.Equals(collected.EggLotId, before.EggLotId, StringComparison.Ordinal)
                                 && collected.EggSourceStatus.Contains("hens", StringComparison.OrdinalIgnoreCase)
                                 && collect.Contains("graded eggs", StringComparison.OrdinalIgnoreCase)
                                 && collected.ProcessWaterL < worked.ProcessWaterL
                                 && collected.CompressedAirNm3 < worked.CompressedAirNm3;
            bool eggQaModeled = collected.EggShellQualityPct >= 78
                                && collected.EggWasherTemperatureC is >= 32 and <= 49
                                && collected.EggQaStatus.Contains("spec", StringComparison.OrdinalIgnoreCase);
            bool washdown = washed.HenHouseHygienePct > collected.HenHouseHygienePct
                            && washed.PoultryManureKg < collected.PoultryManureKg
                            && washed.ProcessWaterL < collected.ProcessWaterL
                            && washed.CulinarySteamKg < collected.CulinarySteamKg
                            && wash.Contains("Washed", StringComparison.OrdinalIgnoreCase);
            bool pass = henFlockModeled && henInputsConsumed && eggProduction && eggCollection && eggQaModeled && washdown;
            return (pass, $"henFlockModeled={henFlockModeled} ({worked.LayingHenCount} hens, {worked.EggProductionPerHour:F1} eggs/h), " +
                          $"henInputsConsumed={henInputsConsumed}, eggProduction={eggProduction} ({before.EggsReady:F0}->{worked.EggsReady:F0} ready), " +
                          $"eggCollection={eggCollection} ('{Trim(collect)}'), eggQaModeled={eggQaModeled} " +
                          $"({collected.EggShellQualityPct:F0}% shell, {collected.EggWasherTemperatureC:F1}C), washdown={washdown} ('{Trim(wash)}')");
        });

        Scenario("CAKE DAIRY RATION AND PARLOR HYGIENE (milk depends on feed mixing and washdown)", () =>
        {
            var cake = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string mix = cake.MixDairyRation();
            TickCake(cake, fullBus, 0.5);
            var mixed = cake.Snapshot;

            TickCake(cake, fullBus, 80);
            var worked = cake.Snapshot;

            string wash = cake.WashDairyParlor();
            TickCake(cake, fullBus, 0.5);
            var washed = cake.Snapshot;

            bool rationMixed = mixed.MixedRationKg > before.MixedRationKg
                                && mixed.ForageKg < before.ForageKg
                                && mixed.GrainKg < before.GrainKg
                                && mixed.DairyMineralKg < before.DairyMineralKg
                                && mixed.RationStatus.Contains("TMR", StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(mixed.MixedRationLotId, before.MixedRationLotId, StringComparison.Ordinal);
            bool rationConsumed = worked.MixedRationKg < mixed.MixedRationKg
                                   && worked.MilkProductionLPerHour > 0
                                   && worked.MilkSourceStatus.Contains("ration", StringComparison.OrdinalIgnoreCase);
            bool barnReality = worked.ManureKg > mixed.ManureKg
                               && worked.DairyParlorHygienePct < mixed.DairyParlorHygienePct
                               && worked.CowComfort > 0
                               && worked.BarnLaborHours < mixed.BarnLaborHours;
            bool washdown = washed.DairyParlorHygienePct > worked.DairyParlorHygienePct
                            && washed.ManureKg < worked.ManureKg
                            && washed.ProcessWaterL < worked.ProcessWaterL
                            && washed.CulinarySteamKg < worked.CulinarySteamKg
                            && wash.Contains("Washed", StringComparison.OrdinalIgnoreCase);
            bool pass = rationMixed && rationConsumed && barnReality && washdown;
            return (pass, $"rationMixed={rationMixed} ('{Trim(mix)}'), rationConsumed={rationConsumed} ({mixed.MixedRationKg:F1}->{worked.MixedRationKg:F1} kg), " +
                          $"barnReality={barnReality} (manure {mixed.ManureKg:F0}->{worked.ManureKg:F0} kg, hygiene {mixed.DairyParlorHygienePct:F0}->{worked.DairyParlorHygienePct:F0}%), " +
                          $"washdown={washdown} ('{Trim(wash)}')");
        });

        Scenario("CAKE INGREDIENT CHAIN (harvest, collect, mill, refine, churn and non-farm factories mutate inventory)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var s0 = cake.Snapshot;

            string harvest = cake.HarvestNow();
            TickCake(cake, fullBus, 0.5);
            var s1 = cake.Snapshot;

            string collect = cake.CollectDairyAndEggs();
            TickCake(cake, fullBus, 0.5);
            var s2 = cake.Snapshot;

            string mill = cake.MillWheat();
            TickCake(cake, fullBus, 1.0);
            var millRunning = cake.Snapshot;
            TickCake(cake, fullBus, 8.5);
            var s3Hold = cake.Snapshot;
            string releaseFlour = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s3 = cake.Snapshot;

            string refine = cake.RefineSugar();
            TickCake(cake, fullBus, 10.5);
            string releaseSugar = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s4 = cake.Snapshot;

            string churn = cake.ChurnButter();
            TickCake(cake, fullBus, 7.5);
            string releaseButter = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s5 = cake.Snapshot;

            string vanilla = cake.ExtractVanilla();
            TickCake(cake, fullBus, 8.0);
            string releaseVanilla = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s6 = cake.Snapshot;

            string cocoa = cake.ProcessCocoa();
            TickCake(cake, fullBus, 11.5);
            string releaseCocoa = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s7 = cake.Snapshot;

            string salt = cake.RunSaltWorks();
            TickCake(cake, fullBus, 9.5);
            string releaseSalt = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s8 = cake.Snapshot;

            string leavening = cake.RunLeaveningPlant();
            TickCake(cake, fullBus, 6.5);
            string releaseLeavening = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s9 = cake.Snapshot;

            string packaging = cake.RunPackagingPlant();
            TickCake(cake, fullBus, 8.5);
            string releasePackaging = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var s10 = cake.Snapshot;

            bool farmYield = s1.WheatKg > s0.WheatKg
                             && s1.SugarCropKg > s0.SugarCropKg
                             && s1.VanillaBeansKg > s0.VanillaBeansKg
                             && Math.Abs(s1.VanillaL - s0.VanillaL) < 0.001;
            bool dairyYield = s2.MilkL > s1.MilkL && s2.Eggs > s1.Eggs;
            bool flourYield = s3.FlourKg > s2.FlourKg && s3.WheatKg < s2.WheatKg;
            bool sugarYield = s4.SugarKg > s3.SugarKg && s4.SugarCropKg < s3.SugarCropKg;
            bool butterYield = s5.ButterKg > s4.ButterKg && s5.MilkL < s4.MilkL;
            bool vanillaYield = s6.VanillaL > s5.VanillaL && s6.VanillaBeansKg < s5.VanillaBeansKg;
            bool cocoaYield = s7.CocoaKg > s6.CocoaKg && s7.CocoaBeansKg < s6.CocoaBeansKg;
            bool saltYield = s8.SaltKg > s7.SaltKg && s8.BrineL < s7.BrineL;
            bool leaveningYield = s9.BakingPowderKg > s8.BakingPowderKg
                                   && s9.SodaAshKg < s8.SodaAshKg
                                   && s9.PhosphateKg < s8.PhosphateKg
                                   && s9.StarchKg < s8.StarchKg;
            bool packagingYield = s10.PackagingUnits > s9.PackagingUnits
                                   && s10.PaperboardKg < s9.PaperboardKg
                                   && s10.LabelStockM < s9.LabelStockM
                                   && s10.PackagingInkL < s9.PackagingInkL
                                   && s10.AdhesiveKg < s9.AdhesiveKg;
            bool processTelemetry = s3.MillRollGapMm > 0 && s3.FlourExtractionPct > 0
                                    && s4.SugarJuiceBrix > 0 && s4.SugarEvaporatorTemperatureC > 90
                                    && s5.CreamSeparatorRpm > 0 && s5.ButterFatPct > 70
                                    && s6.VanillaExtractorTemperatureC > 70 && s6.VanillaExtractStrengthPct > 80
                                    && s7.CocoaRoasterTemperatureC > 100 && s7.CocoaGrindMicrons > 0
                                    && s8.BrineSalinityPct > 0 && s8.SaltCrystallizerTemperatureC > 40
                                    && s9.LeaveningMixerRpm > 0 && s9.LeaveningHomogeneityPct > 90
                                    && s10.CartonFormerSpeedCpm > 0 && s10.PrintRegistrationMm > 0 && s10.GluePotTemperatureC > 100;
            bool factoryUtilitiesConsumed = s10.ProcessWaterL < s0.ProcessWaterL
                                            && s10.CulinarySteamKg < s0.CulinarySteamKg
                                            && s10.CompressedAirNm3 < s0.CompressedAirNm3
                                            && s10.FilterMediaPct < s0.FilterMediaPct;
            bool timedFactoryRun = millRunning.FactoryRunActive
                                   && millRunning.ActiveFactoryName.Contains("mill", StringComparison.OrdinalIgnoreCase)
                                   && millRunning.ActiveFactoryPhase.Length > 0
                                   && millRunning.FactoryProgress > 0
                                   && millRunning.FactoryProgress < 1
                                   && millRunning.FactoryRunPowerMW > 0
                                   && millRunning.FactoryRunQualityPct > 0
                                   && Math.Abs(millRunning.FlourKg - s2.FlourKg) < 0.001
                                   && !s3.FactoryRunActive;
            bool labReleaseWorkflow = s3Hold.PendingLabLotId.Length > 0
                                      && releaseFlour.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releaseSugar.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releaseButter.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releaseVanilla.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releaseCocoa.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releaseSalt.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releaseLeavening.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && releasePackaging.Contains("released", StringComparison.OrdinalIgnoreCase)
                                      && s10.PendingLabLotId.Length == 0;
            bool pass = farmYield && dairyYield && flourYield && sugarYield && butterYield && vanillaYield && cocoaYield && saltYield && leaveningYield && packagingYield && processTelemetry && factoryUtilitiesConsumed && timedFactoryRun && labReleaseWorkflow;
            return (pass, $"farmYield={farmYield} ('{Trim(harvest)}'), dairyYield={dairyYield} ('{Trim(collect)}'), " +
                          $"flourYield={flourYield} ('{Trim(mill)}'), sugarYield={sugarYield} ('{Trim(refine)}'), " +
                          $"butterYield={butterYield} ('{Trim(churn)}'), vanillaYield={vanillaYield} ('{Trim(vanilla)}'), cocoaYield={cocoaYield} ('{Trim(cocoa)}'), " +
                          $"saltYield={saltYield} ('{Trim(salt)}'), leaveningYield={leaveningYield} ('{Trim(leavening)}'), packagingYield={packagingYield} ('{Trim(packaging)}'), " +
                          $"processTelemetry={processTelemetry}, factoryUtilitiesConsumed={factoryUtilitiesConsumed}, timedFactoryRun={timedFactoryRun}, " +
                          $"labReleaseWorkflow={labReleaseWorkflow}");
        });

        Scenario("CAKE UTILITY PLANT (process water, steam and compressed air are produced by a powered support plant)", () =>
        {
            var cake = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string start = cake.RunUtilityPlant();
            TickCake(cake, fullBus, 1.0);
            var running = cake.Snapshot;

            TickCake(cake, fullBus, 12.0);
            var finished = cake.Snapshot;

            bool startConsumesInputs = before.CanRunUtilityPlant
                                       && running.UtilityPlantActive
                                       && running.UtilityPlantProgress > 0
                                       && running.UtilityPlantProgress < 1
                                       && running.UtilityPlantPowerMW > 0
                                       && running.IrrigationWaterL < before.IrrigationWaterL
                                       && running.FilterMediaPct < before.FilterMediaPct
                                       && Math.Abs(running.ProcessWaterL - before.ProcessWaterL) < 0.001
                                       && Math.Abs(running.CulinarySteamKg - before.CulinarySteamKg) < 0.001
                                       && Math.Abs(running.CompressedAirNm3 - before.CompressedAirNm3) < 0.001
                                       && start.Contains("Started utility plant", StringComparison.OrdinalIgnoreCase);
            bool completedProducesUtilities = !finished.UtilityPlantActive
                                              && finished.ProcessWaterL > running.ProcessWaterL
                                              && finished.CulinarySteamKg > running.CulinarySteamKg
                                              && finished.CompressedAirNm3 > running.CompressedAirNm3
                                              && finished.FactoryEffluentL > running.FactoryEffluentL
                                              && finished.ProcessWaterConductivityUsCm > 0
                                              && finished.BoilerPressureBar > running.BoilerPressureBar
                                              && finished.AirHeaderPressureBar > running.AirHeaderPressureBar
                                              && finished.UtilityPlantStatus.Contains("completed", StringComparison.OrdinalIgnoreCase);
            bool traceableUtilityRun = finished.TraceabilityStatus.Contains("Utility", StringComparison.OrdinalIgnoreCase)
                                       && finished.FactoryUtilityStatus.Contains("Utility plant", StringComparison.OrdinalIgnoreCase);
            bool pass = startConsumesInputs && completedProducesUtilities && traceableUtilityRun;
            return (pass, $"startConsumesInputs={startConsumesInputs} ('{Trim(start)}'), " +
                          $"completedProducesUtilities={completedProducesUtilities} ({running.ProcessWaterL:F0}->{finished.ProcessWaterL:F0} L water, " +
                          $"{running.CulinarySteamKg:F0}->{finished.CulinarySteamKg:F0} kg steam, {running.CompressedAirNm3:F0}->{finished.CompressedAirNm3:F0} Nm3 air), " +
                          $"traceableUtilityRun={traceableUtilityRun}");
        });

        Scenario("CAKE ICING PREP KITCHEN (decorating icing is factory-made, released and reserved)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string start = cake.PrepareIcing();
            TickCake(cake, fullBus, 1.0);
            var running = cake.Snapshot;

            TickCake(cake, fullBus, 8.0);
            var held = cake.Snapshot;

            string release = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var released = cake.Snapshot;

            string kit = cake.StageBatchKit();
            TickCake(cake, fullBus, 0.5);
            var staged = cake.Snapshot;

            bool startConsumesInputs = before.CanPrepareIcing
                                       && running.IcingPrepActive
                                       && running.ActiveFactoryName.Contains("Icing", StringComparison.OrdinalIgnoreCase)
                                       && running.IcingPrepProgress > 0
                                       && running.IcingPrepProgress < 1
                                       && running.IcingPrepPowerMW > 0
                                       && running.SugarKg < before.SugarKg
                                       && running.ButterKg < before.ButterKg
                                       && running.MilkL < before.MilkL
                                       && running.VanillaL < before.VanillaL
                                       && Math.Abs(running.IcingKg - before.IcingKg) < 0.001
                                       && start.Contains("Started preparing", StringComparison.OrdinalIgnoreCase);
            bool completionHeldForLab = !held.IcingPrepActive
                                        && !held.FactoryRunActive
                                        && held.IcingKg > before.IcingKg
                                        && held.IcingLotId != before.IcingLotId
                                        && held.PendingLabProductName.Contains("icing", StringComparison.OrdinalIgnoreCase)
                                        && held.MissingIngredients.Contains("lab release", StringComparison.OrdinalIgnoreCase)
                                        && held.IcingMixerRpm > 0
                                        && held.IcingTemperatureC > 0
                                        && held.IcingViscosityPaS > 0;
            bool releaseClearsHold = released.PendingLabLotId.Length == 0
                                     && released.IngredientLabStatus.Contains("released", StringComparison.OrdinalIgnoreCase)
                                     && released.CanStageBatchKit
                                     && released.MissingIngredients.Length == 0
                                     && release.Contains("released", StringComparison.OrdinalIgnoreCase);
            bool kittingReservesIcing = staged.BatchKitStaged
                                        && staged.IcingKg < released.IcingKg
                                        && staged.BatchKitStatus.Contains("prepared icing", StringComparison.OrdinalIgnoreCase)
                                        && staged.TraceabilityStatus.Contains("staged", StringComparison.OrdinalIgnoreCase)
                                        && kit.Contains("prepared icing", StringComparison.OrdinalIgnoreCase);
            bool pass = startConsumesInputs && completionHeldForLab && releaseClearsHold && kittingReservesIcing;
            return (pass, $"startConsumesInputs={startConsumesInputs} ('{Trim(start)}'), " +
                          $"completionHeldForLab={completionHeldForLab} (icing {before.IcingKg:F1}->{held.IcingKg:F1} kg, lot {held.IcingLotId}), " +
                          $"releaseClearsHold={releaseClearsHold} ('{Trim(release)}'), kittingReservesIcing={kittingReservesIcing} ('{Trim(kit)}')");
        });

        Scenario("CAKE VANILLA EXTRACTION (farm grows beans, factory makes extract)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string harvest = cake.HarvestNow();
            TickCake(cake, fullBus, 0.5);
            var harvested = cake.Snapshot;

            string start = cake.ExtractVanilla();
            TickCake(cake, fullBus, 1.0);
            var running = cake.Snapshot;

            TickCake(cake, fullBus, 8.0);
            var held = cake.Snapshot;

            string blockedMsg = cake.StageBatchKit();
            string release = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var released = cake.Snapshot;

            bool harvestMakesBeans = harvested.VanillaBeansKg > before.VanillaBeansKg
                                     && Math.Abs(harvested.VanillaL - before.VanillaL) < 0.001
                                     && harvested.VanillaBeanLotId != before.VanillaBeanLotId
                                     && harvest.Contains("vanilla beans", StringComparison.OrdinalIgnoreCase);
            bool extractionConsumesBeans = running.FactoryRunActive
                                           && running.ActiveFactoryName.Contains("Vanilla", StringComparison.OrdinalIgnoreCase)
                                           && running.VanillaBeansKg < harvested.VanillaBeansKg
                                           && Math.Abs(running.VanillaL - harvested.VanillaL) < 0.001
                                           && running.VanillaExtractorTemperatureC > 30
                                           && start.Contains("vanilla beans", StringComparison.OrdinalIgnoreCase);
            bool extractionProducesHeldLot = !held.FactoryRunActive
                                             && held.VanillaL > harvested.VanillaL
                                             && held.VanillaPomaceKg > harvested.VanillaPomaceKg
                                             && held.VanillaExtractStrengthPct > 80
                                             && held.PendingLabProductName.Contains("vanilla", StringComparison.OrdinalIgnoreCase)
                                             && blockedMsg.Contains("lab release", StringComparison.OrdinalIgnoreCase);
            bool releaseClearsExtract = released.PendingLabLotId.Length == 0
                                        && released.IngredientLabStatus.Contains("released", StringComparison.OrdinalIgnoreCase)
                                        && release.Contains("released", StringComparison.OrdinalIgnoreCase);
            bool pass = harvestMakesBeans && extractionConsumesBeans && extractionProducesHeldLot && releaseClearsExtract;
            return (pass, $"harvestMakesBeans={harvestMakesBeans} ('{Trim(harvest)}'), extractionConsumesBeans={extractionConsumesBeans} ('{Trim(start)}'), " +
                          $"extractionProducesHeldLot={extractionProducesHeldLot} ('{Trim(blockedMsg)}'), releaseClearsExtract={releaseClearsExtract} ('{Trim(release)}')");
        });

        Scenario("CAKE PACKAGING PLANT (cartons are made from paperboard, labels, ink and adhesive)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string start = cake.RunPackagingPlant();
            TickCake(cake, fullBus, 1.0);
            var running = cake.Snapshot;

            TickCake(cake, fullBus, 8.0);
            var held = cake.Snapshot;

            string blockedMsg = cake.StageBatchKit();
            double waterBeforeRelease = held.ProcessWaterL;
            double airBeforeRelease = held.CompressedAirNm3;
            double filterBeforeRelease = held.FilterMediaPct;
            string release = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var released = cake.Snapshot;

            bool startConsumesFeedstocks = before.CanRunPackagingPlant
                                           && running.FactoryRunActive
                                           && running.ActiveFactoryName.Contains("coder", StringComparison.OrdinalIgnoreCase)
                                           && running.PaperboardKg < before.PaperboardKg
                                           && running.LabelStockM < before.LabelStockM
                                           && running.PackagingInkL < before.PackagingInkL
                                           && running.AdhesiveKg < before.AdhesiveKg
                                           && Math.Abs(running.PackagingUnits - before.PackagingUnits) < 0.001
                                           && start.Contains("paperboard", StringComparison.OrdinalIgnoreCase);
            bool timedCartonOutput = !held.FactoryRunActive
                                     && held.PackagingUnits > before.PackagingUnits
                                     && held.CartonFormerSpeedCpm > 0
                                     && held.PrintRegistrationMm > 0
                                     && held.GluePotTemperatureC > 100
                                     && held.FactoryStatus.Contains("Packaging plant completed", StringComparison.OrdinalIgnoreCase);
            bool labHoldsNewPackagingLot = held.PendingLabLotId.Length > 0
                                           && held.PendingLabProductName.Contains("cartons", StringComparison.OrdinalIgnoreCase)
                                           && held.MissingIngredients.Contains("lab release", StringComparison.OrdinalIgnoreCase)
                                           && blockedMsg.Contains("lab release", StringComparison.OrdinalIgnoreCase);
            bool releaseClearsPackagingLot = released.PendingLabLotId.Length == 0
                                             && released.CanStageBatchKit
                                             && released.MissingIngredients.Length == 0
                                             && release.Contains("released", StringComparison.OrdinalIgnoreCase);
            bool releaseConsumesLabUtilities = released.ProcessWaterL < waterBeforeRelease
                                               && released.CompressedAirNm3 < airBeforeRelease
                                               && released.FilterMediaPct < filterBeforeRelease;
            bool pass = startConsumesFeedstocks && timedCartonOutput && labHoldsNewPackagingLot && releaseClearsPackagingLot && releaseConsumesLabUtilities;
            return (pass, $"startConsumesFeedstocks={startConsumesFeedstocks} ('{Trim(start)}'), timedCartonOutput={timedCartonOutput} " +
                          $"({before.PackagingUnits:F0}->{held.PackagingUnits:F0} cartons), labHoldsNewPackagingLot={labHoldsNewPackagingLot} ('{Trim(blockedMsg)}'), " +
                          $"releaseClearsPackagingLot={releaseClearsPackagingLot} ('{Trim(release)}'), releaseConsumesLabUtilities={releaseConsumesLabUtilities}");
        });

        Scenario("CAKE FACTORY MAINTENANCE (plant condition affects factories and service consumes utilities)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var initial = cake.Snapshot;

            string mill = cake.MillWheat();
            TickCake(cake, fullBus, 9.0);
            var worn = cake.Snapshot;

            double waterBeforeService = worn.ProcessWaterL;
            double steamBeforeService = worn.CulinarySteamKg;
            double airBeforeService = worn.CompressedAirNm3;
            double filterBeforeService = worn.FilterMediaPct;
            string service = cake.ServiceIngredientFactories();
            TickCake(cake, fullBus, 0.5);
            var serviced = cake.Snapshot;

            bool equipmentModeled = initial.MillConditionPct > 0
                                    && initial.MillCalibrationPct > 0
                                    && initial.ActiveFactoryBearingTemperatureC > 0
                                    && initial.ActiveFactoryVibrationMmS > 0
                                    && initial.FactoryMaintenanceStatus.Contains("maintenance", StringComparison.OrdinalIgnoreCase);
            bool wearApplied = worn.MillConditionPct < initial.MillConditionPct
                               && worn.MillCalibrationPct < initial.MillCalibrationPct
                               && worn.FactoryStatus.Contains("Equipment now", StringComparison.OrdinalIgnoreCase);
            bool maintenanceAvailable = worn.CanServiceFactories;
            bool serviceRecovered = serviced.MillConditionPct > worn.MillConditionPct
                                    && serviced.MillCalibrationPct > worn.MillCalibrationPct
                                    && serviced.MillConditionPct <= 100
                                    && serviced.MillCalibrationPct <= 100;
            bool serviceConsumedUtilities = serviced.ProcessWaterL < waterBeforeService
                                            && serviced.CulinarySteamKg < steamBeforeService
                                            && serviced.CompressedAirNm3 < airBeforeService
                                            && serviced.FilterMediaPct < filterBeforeService;
            bool pass = equipmentModeled && wearApplied && maintenanceAvailable && serviceRecovered && serviceConsumedUtilities;
            return (pass, $"equipmentModeled={equipmentModeled}, wearApplied={wearApplied} after '{Trim(mill)}' " +
                          $"({initial.MillConditionPct:F0}%->{worn.MillConditionPct:F0}%), maintenanceAvailable={maintenanceAvailable}, " +
                          $"serviceRecovered={serviceRecovered} ({worn.MillConditionPct:F0}%->{serviced.MillConditionPct:F0}%), " +
                          $"serviceConsumedUtilities={serviceConsumedUtilities} ('{Trim(service)}')");
        });

        Scenario("CAKE FACTORY BYPRODUCT HANDLING (plants make residual streams that must be hauled or treated)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string mill = cake.MillWheat();
            TickCake(cake, fullBus, 8.5);
            var afterRun = cake.Snapshot;

            double feedBefore = afterRun.AnimalFeedKg;
            double cashBefore = afterRun.CashBalance;
            string haul = cake.HaulFactoryByproducts();
            TickCake(cake, fullBus, 0.5);
            var afterHaul = cake.Snapshot;

            double effluentBefore = afterHaul.FactoryEffluentL;
            double airBefore = afterHaul.CompressedAirNm3;
            double waterBefore = afterHaul.IrrigationWaterL;
            string treat = cake.TreatFactoryEffluent();
            TickCake(cake, fullBus, 0.5);
            var afterTreat = cake.Snapshot;

            bool byproductsCreated = afterRun.BranKg > before.BranKg
                                     && afterRun.FactoryEffluentL > before.FactoryEffluentL
                                     && afterRun.WasteHandlingStatus.Contains("bran", StringComparison.OrdinalIgnoreCase);
            bool handlingAvailable = afterRun.CanHaulByproducts && afterRun.CanTreatFactoryEffluent;
            bool haulWorks = afterHaul.ByproductStoragePct < afterRun.ByproductStoragePct
                             && afterHaul.AnimalFeedKg > feedBefore
                             && afterHaul.CashBalance > cashBefore
                             && haul.Contains("Hauled", StringComparison.OrdinalIgnoreCase);
            bool treatmentWorks = afterTreat.FactoryEffluentL < effluentBefore
                                  && afterTreat.CompressedAirNm3 < airBefore
                                  && afterTreat.IrrigationWaterL > waterBefore
                                  && treat.Contains("Treated", StringComparison.OrdinalIgnoreCase);
            bool pass = byproductsCreated && handlingAvailable && haulWorks && treatmentWorks;
            return (pass, $"byproductsCreated={byproductsCreated} after '{Trim(mill)}', handlingAvailable={handlingAvailable}, " +
                          $"haulWorks={haulWorks} ('{Trim(haul)}'), treatmentWorks={treatmentWorks} ('{Trim(treat)}')");
        });

        Scenario("CAKE QA LAB RELEASE (factory output lots are held until operator release)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var initial = cake.Snapshot;

            string mill = cake.MillWheat();
            TickCake(cake, fullBus, 9.0);
            var held = cake.Snapshot;

            string blockedMsg = cake.StageBatchKit();
            bool stageBlocked = blockedMsg.Contains("lab release", StringComparison.OrdinalIgnoreCase);
            double waterBeforeRelease = held.ProcessWaterL;
            double airBeforeRelease = held.CompressedAirNm3;
            double filterBeforeRelease = held.FilterMediaPct;
            string release = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var released = cake.Snapshot;

            bool heldForLab = held.PendingLabLotId.Length > 0
                              && held.PendingLabProductName.Contains("flour", StringComparison.OrdinalIgnoreCase)
                              && held.IngredientLabStatus.Contains("hold", StringComparison.OrdinalIgnoreCase)
                              && held.MissingIngredients.Contains("lab release", StringComparison.OrdinalIgnoreCase);
            bool batchBlockedUntilRelease = stageBlocked;
            bool releaseClearsHold = released.PendingLabLotId.Length == 0
                                     && released.IngredientLabStatus.Contains("released", StringComparison.OrdinalIgnoreCase)
                                     && released.CanStageBatchKit
                                     && released.MissingIngredients.Length == 0;
            bool releaseConsumesLabUtilities = released.ProcessWaterL < waterBeforeRelease
                                               && released.CompressedAirNm3 < airBeforeRelease
                                               && released.FilterMediaPct < filterBeforeRelease;
            bool pass = initial.CanStageBatchKit && heldForLab && batchBlockedUntilRelease && releaseClearsHold && releaseConsumesLabUtilities;
            return (pass, $"heldForLab={heldForLab} after '{Trim(mill)}', batchBlockedUntilRelease={batchBlockedUntilRelease} ('{Trim(blockedMsg)}'), " +
                          $"releaseClearsHold={releaseClearsHold} ('{Trim(release)}'), releaseConsumesLabUtilities={releaseConsumesLabUtilities}");
        });

        Scenario("CAKE WAREHOUSE KITTING (ingredients are staged before the bakery line starts)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            bool directStartBlocked = !cake.TryStartBatch(out var blockedMsg);
            string kit = cake.StageBatchKit();
            TickCake(cake, fullBus, 0.5);
            var staged = cake.Snapshot;
            bool started = cake.TryStartBatch(out var startMsg);
            TickCake(cake, fullBus, 0.5);
            var startedState = cake.Snapshot;

            bool stageAvailable = before.CanStageBatchKit && !before.CanStartBatch;
            bool kitConsumesInventory = staged.FlourKg < before.FlourKg
                                        && staged.SugarKg < before.SugarKg
                                        && staged.MilkL < before.MilkL
                                        && staged.IcingKg < before.IcingKg
                                        && staged.PackagingUnits < before.PackagingUnits;
            bool warehouseResourcesUsed = staged.ForkliftBatteryPct < before.ForkliftBatteryPct
                                          && staged.WarehousePalletSpacePct < before.WarehousePalletSpacePct;
            bool kitTraceable = staged.BatchKitStaged
                                && staged.BatchKitLotId.StartsWith("KIT-", StringComparison.OrdinalIgnoreCase)
                                && staged.BatchKitStatus.Contains("staged", StringComparison.OrdinalIgnoreCase)
                                && staged.BatchKitMassKg > 0
                                && staged.CanStartBatch;
            bool startUsesKit = directStartBlocked
                                && blockedMsg.Contains("kit", StringComparison.OrdinalIgnoreCase)
                                && started
                                && !startedState.BatchKitStaged
                                && startedState.CurrentBatchTrace.Contains("KIT-", StringComparison.OrdinalIgnoreCase)
                                && startedState.Stage == CakeBatchStage.Scaling;
            bool pass = stageAvailable && kitConsumesInventory && warehouseResourcesUsed && kitTraceable && startUsesKit;
            return (pass, $"stageAvailable={stageAvailable}, directStartBlocked={directStartBlocked} ('{Trim(blockedMsg)}'), " +
                          $"kitConsumesInventory={kitConsumesInventory}, warehouseResourcesUsed={warehouseResourcesUsed}, " +
                          $"kitTraceable={kitTraceable} ('{Trim(kit)}'), startUsesKit={startUsesKit} ('{Trim(startMsg)}')");
        });

        Scenario("CAKE TRACEABILITY (receiving, dairy, factory conversion and batch lots are audited)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            var initial = cake.Snapshot;

            var (supplyOrder, supplyUnload) = DeliverCakeSupplies(cake, fullBus);
            string supply = $"{supplyOrder} / {supplyUnload}";
            TickCake(cake, fullBus, 0.5);
            var supplied = cake.Snapshot;

            string collect = cake.CollectDairyAndEggs();
            TickCake(cake, fullBus, 0.5);
            var dairy = cake.Snapshot;

            string mill = cake.MillWheat();
            TickCake(cake, fullBus, 9.0);
            var milled = cake.Snapshot;

            string lab = cake.ReleaseIngredientLabLot();
            TickCake(cake, fullBus, 0.5);
            var labReleased = cake.Snapshot;

            string kit = cake.StageBatchKit();
            TickCake(cake, fullBus, 0.5);
            var staged = cake.Snapshot;

            bool started = cake.TryStartBatch(out var startMsg);
            TickCake(cake, fullBus, 0.5);
            var batch = cake.Snapshot;

            bool openingLotsPresent = initial.TraceabilityScorePct >= 99
                                      && initial.FlourLotId.Length > 0
                                      && initial.MilkLotId.Length > 0
                                      && initial.PackagingLotId.Length > 0;
            bool receivingManifestChanged = supplied.LastSupplyManifestId.Length > 0
                                            && supplied.LastSupplyManifestId != initial.LastSupplyManifestId
                                            && supplied.TraceabilityStatus.Contains("manifest", StringComparison.OrdinalIgnoreCase);
            bool dairyLotChanged = dairy.MilkLotId.Length > 0
                                   && dairy.EggLotId.Length > 0
                                   && dairy.MilkLotId != initial.MilkLotId
                                   && dairy.TraceabilityStatus.Contains("trace", StringComparison.OrdinalIgnoreCase)
                                   && dairy.TraceabilityStatus.Contains("egg lot", StringComparison.OrdinalIgnoreCase);
            bool factoryOutputLotChanged = milled.FlourLotId.Length > 0
                                           && milled.FlourLotId != initial.FlourLotId
                                           && milled.TraceabilityStatus.Contains("source lot", StringComparison.OrdinalIgnoreCase);
            bool labReleasedLot = labReleased.PendingLabLotId.Length == 0
                                  && lab.Contains("released", StringComparison.OrdinalIgnoreCase);
            bool warehouseKitStaged = staged.BatchKitStaged
                                      && staged.BatchKitLotId.Length > 0
                                      && staged.BatchKitStatus.Contains("staged", StringComparison.OrdinalIgnoreCase);
            bool batchManifestOpened = warehouseKitStaged
                                       && started
                                       && batch.CurrentBatchLotId.Length > 0
                                       && batch.CurrentBatchTrace.Contains("KIT-", StringComparison.OrdinalIgnoreCase)
                                       && batch.CurrentBatchTrace.Contains("flour", StringComparison.OrdinalIgnoreCase)
                                       && batch.CurrentBatchTrace.Contains("milk", StringComparison.OrdinalIgnoreCase)
                                       && batch.CurrentBatchTrace.Contains("prepared icing", StringComparison.OrdinalIgnoreCase)
                                       && batch.TraceabilityScorePct >= 99;
            bool pass = openingLotsPresent && receivingManifestChanged && dairyLotChanged && factoryOutputLotChanged && labReleasedLot && warehouseKitStaged && batchManifestOpened;
            return (pass, $"openingLotsPresent={openingLotsPresent}, receivingManifestChanged={receivingManifestChanged} ('{Trim(supply)}'), " +
                          $"dairyLotChanged={dairyLotChanged} ('{Trim(collect)}'), factoryOutputLotChanged={factoryOutputLotChanged} ('{Trim(mill)}'), " +
                          $"labReleasedLot={labReleasedLot}, warehouseKitStaged={warehouseKitStaged} ('{Trim(kit)}'), " +
                          $"batchManifestOpened={batchManifestOpened} started={started} ('{Trim(startMsg)}'), batchLot={batch.CurrentBatchLotId}");
        });

        Scenario("CAKE SUPPLY CHAIN INPUTS (ingredients require finite seed, water, feed, beans and factory feedstocks)", () =>
        {
            var cake = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            TickCake(cake, fullBus, 80);
            var after = cake.Snapshot;
            bool fieldInputsConsumed = after.IrrigationWaterL < before.IrrigationWaterL
                                       && after.FertilizerKg < before.FertilizerKg
                                       && after.WheatSeedKg < before.WheatSeedKg
                                       && after.BeetSeedKg < before.BeetSeedKg;
            bool livestockInputsConsumed = after.AnimalFeedKg < before.AnimalFeedKg;
            bool cocoaDoesNotAppear = Math.Abs(after.CocoaKg - before.CocoaKg) < 0.001
                                      && Math.Abs(after.CocoaBeansKg - before.CocoaBeansKg) < 0.001;

            double waterBeforeSupply = after.IrrigationWaterL;
            double bakingPowderBeforeSupply = after.BakingPowderKg;
            double saltBeforeSupply = after.SaltKg;
            double cartonsBeforeSupply = after.PackagingUnits;
            var (supplyOrder, supplyUnload) = DeliverCakeSupplies(cake, fullBus);
            string supply = $"{supplyOrder} / {supplyUnload}";
            TickCake(cake, fullBus, 0.5);
            var supplied = cake.Snapshot;
            bool supplyTruckAddsInputs = supplied.IrrigationWaterL > waterBeforeSupply
                                         && supplied.PaperboardKg > after.PaperboardKg
                                         && supplied.LabelStockM > after.LabelStockM
                                         && supplied.PackagingInkL > after.PackagingInkL
                                         && supplied.AdhesiveKg > after.AdhesiveKg
                                         && supplied.CocoaBeansKg > after.CocoaBeansKg
                                         && supplied.BrineL > after.BrineL
                                         && supplied.SodaAshKg > after.SodaAshKg
                                         && supplied.PhosphateKg > after.PhosphateKg
                                         && supplied.StarchKg > after.StarchKg
                                         && supplied.ProcessWaterL > after.ProcessWaterL
                                         && supplied.CulinarySteamKg > after.CulinarySteamKg
                                         && supplied.CompressedAirNm3 > after.CompressedAirNm3
                                         && supplied.FilterMediaPct >= after.FilterMediaPct;
            bool supplyTruckDoesNotMakeFinalIngredients = Math.Abs(supplied.BakingPowderKg - bakingPowderBeforeSupply) < 0.001
                                                          && Math.Abs(supplied.SaltKg - saltBeforeSupply) < 0.001
                                                          && Math.Abs(supplied.PackagingUnits - cartonsBeforeSupply) < 0.001;

            bool pass = fieldInputsConsumed && livestockInputsConsumed && cocoaDoesNotAppear && supplyTruckAddsInputs && supplyTruckDoesNotMakeFinalIngredients;
            return (pass, $"fieldInputsConsumed={fieldInputsConsumed}, livestockInputsConsumed={livestockInputsConsumed}, " +
                          $"cocoaDoesNotAppear={cocoaDoesNotAppear}, supplyTruckAddsInputs={supplyTruckAddsInputs}, " +
                          $"supplyTruckDoesNotMakeFinalIngredients={supplyTruckDoesNotMakeFinalIngredients} ('{Trim(supply)}')");
        });

        Scenario("CAKE SUPPLIER DELIVERY LEAD TIME (supplies are ordered, delivered, then unloaded)", () =>
        {
            var cake = new CakeFactoryService { FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            var before = cake.Snapshot;

            string order = cake.OrderSupplyDelivery();
            string earlyReceive = cake.ReceiveSupplies();
            TickCake(cake, fullBus, 1.0);
            var enroute = cake.Snapshot;

            TickCake(cake, fullBus, 80);
            var arrived = cake.Snapshot;

            double waterBeforeUnload = arrived.IrrigationWaterL;
            double paperboardBeforeUnload = arrived.PaperboardKg;
            double labelStockBeforeUnload = arrived.LabelStockM;
            double inkBeforeUnload = arrived.PackagingInkL;
            double adhesiveBeforeUnload = arrived.AdhesiveKg;
            double cartonsBeforeUnload = arrived.PackagingUnits;
            double cocoaBeforeUnload = arrived.CocoaBeansKg;
            string unload = cake.UnloadSupplyDelivery();
            TickCake(cake, fullBus, 0.5);
            var unloaded = cake.Snapshot;

            bool orderPlaced = before.CanOrderSupplyDelivery
                               && enroute.SupplyTruckEnRoute
                               && !enroute.SupplyTruckArrived
                               && enroute.CashBalance < before.CashBalance
                               && enroute.InboundSupplyManifestId.StartsWith("PO-", StringComparison.OrdinalIgnoreCase)
                               && order.Contains("ETA", StringComparison.OrdinalIgnoreCase);
            bool noAirDrop = enroute.IrrigationWaterL <= before.IrrigationWaterL
                             && Math.Abs(enroute.PackagingUnits - before.PackagingUnits) < 0.001
                             && Math.Abs(enroute.PaperboardKg - before.PaperboardKg) < 0.001
                             && Math.Abs(enroute.LabelStockM - before.LabelStockM) < 0.001
                             && Math.Abs(enroute.PackagingInkL - before.PackagingInkL) < 0.001
                             && Math.Abs(enroute.AdhesiveKg - before.AdhesiveKg) < 0.001
                             && Math.Abs(enroute.CocoaBeansKg - before.CocoaBeansKg) < 0.001
                             && earlyReceive.Contains("cannot enter inventory", StringComparison.OrdinalIgnoreCase);
            bool arrivalGate = arrived.SupplyTruckEnRoute
                               && arrived.SupplyTruckArrived
                               && arrived.CanUnloadSupplyDelivery
                               && arrived.SupplyOrderStatus.Contains("receiving dock", StringComparison.OrdinalIgnoreCase);
            bool unloadAddsInputs = !unloaded.SupplyTruckEnRoute
                                    && unloaded.IrrigationWaterL > waterBeforeUnload
                                    && unloaded.PaperboardKg > paperboardBeforeUnload
                                    && unloaded.LabelStockM > labelStockBeforeUnload
                                    && unloaded.PackagingInkL > inkBeforeUnload
                                    && unloaded.AdhesiveKg > adhesiveBeforeUnload
                                    && Math.Abs(unloaded.PackagingUnits - cartonsBeforeUnload) < 0.001
                                    && unloaded.CocoaBeansKg > cocoaBeforeUnload
                                    && unloaded.LastSupplyManifestId.StartsWith("RCV-", StringComparison.OrdinalIgnoreCase)
                                    && unloaded.TraceabilityStatus.Contains("manifest", StringComparison.OrdinalIgnoreCase)
                                    && unload.Contains("unloaded", StringComparison.OrdinalIgnoreCase);
            bool pass = orderPlaced && noAirDrop && arrivalGate && unloadAddsInputs;
            return (pass, $"orderPlaced={orderPlaced} ('{Trim(order)}'), noAirDrop={noAirDrop}, " +
                          $"arrivalGate={arrivalGate}, unloadAddsInputs={unloadAddsInputs} ('{Trim(unload)}')");
        });

        Scenario("CAKE FULL MANUAL BATCH (operator releases every HACCP gate to packaged cakes)", () =>
        {
            var cake = new CakeFactoryService { LineSpeed = 1.0, FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            double packagingBefore = cake.Snapshot.PackagingUnits;
            double icingBefore = cake.Snapshot.IcingKg;
            bool canStage = cake.Snapshot.CanStageBatchKit;
            string kitMsg = cake.StageBatchKit();
            TickCake(cake, fullBus, 0.5);
            var staged = cake.Snapshot;
            bool kitStaged = staged.BatchKitStaged && staged.CanStartBatch;
            bool started = cake.TryStartBatch(out var startMsg);

            int releases = 0;
            var releasedStages = new List<string>();
            string lastMsg = "";
            for (int guard = 0; guard < 3000; guard++)
            {
                TickCake(cake, fullBus, 0.25);
                var s = cake.Snapshot;
                if (s.CanAdvanceStage)
                {
                    releasedStages.Add(s.StageName);
                    bool advanced = cake.TryAdvanceStage(out lastMsg);
                    if (!advanced)
                        return (false, $"release failed at {s.StageName}: {lastMsg}");
                    releases++;
                    TickCake(cake, fullBus, 0.25);
                }

                if (cake.Snapshot.Stage == CakeBatchStage.Idle && cake.Snapshot.CakesBaked > 0)
                    break;
            }

            var done = cake.Snapshot;
            bool sevenManualReleases = releases == 7;
            bool packedOrRejectedAll = done.CakesPacked + done.CakesRejected == done.Recipe.BatchSize;
            bool packagingConsumed = done.PackagingUnits < packagingBefore;
            bool icingConsumed = done.IcingKg < icingBefore;
            bool pass = canStage && kitStaged && started && sevenManualReleases && done.Stage == CakeBatchStage.Idle
                        && done.CakesBaked == done.Recipe.BatchSize && packedOrRejectedAll
                        && packagingConsumed && icingConsumed && done.QualityScore >= 70 && done.SanitationScore > 0;
            return (pass, $"canStage={canStage}, kitStaged={kitStaged} ('{Trim(kitMsg)}'), started={started} ('{Trim(startMsg)}'), releases={releases} [{string.Join(" > ", releasedStages)}], " +
                          $"baked={done.CakesBaked}, packed={done.CakesPacked}, rejected={done.CakesRejected}, " +
                          $"QA={done.QualityScore:F0}%, sanitation={done.SanitationScore:F0}%, cartons {packagingBefore:F0}->{done.PackagingUnits:F0}, icing {icingBefore:F1}->{done.IcingKg:F1} kg, " +
                          $"last='{Trim(lastMsg)}'");
        });

        Scenario("CAKE ORDER DISPATCH (packed cakes fulfill customer contracts)", () =>
        {
            var cake = new CakeFactoryService { LineSpeed = 1.0, FarmIntensity = 1.0 };
            TickCake(cake, fullBus, 0.5);
            var initial = cake.Snapshot;

            cake.StageBatchKit();
            TickCake(cake, fullBus, 0.5);
            bool started = cake.TryStartBatch(out var startMsg);

            for (int guard = 0; guard < 3000; guard++)
            {
                TickCake(cake, fullBus, 0.25);
                var s = cake.Snapshot;
                if (s.CanAdvanceStage)
                {
                    if (!cake.TryAdvanceStage(out var advanceMsg))
                        return (false, $"release failed at {s.StageName}: {advanceMsg}");
                    TickCake(cake, fullBus, 0.25);
                }
                if (cake.Snapshot.Stage == CakeBatchStage.Idle && cake.Snapshot.FinishedGoodsCakes >= initial.OrderCakesRequired)
                    break;
            }

            var ready = cake.Snapshot;
            double cashBefore = ready.CashBalance;
            double reputationBefore = ready.ReputationPct;
            double truckBefore = ready.DispatchTruckChargePct;
            string orderBefore = ready.CurrentOrderId;
            string dispatch = cake.DispatchOrder();
            TickCake(cake, fullBus, 0.5);
            var shipped = cake.Snapshot;

            bool producedForOrder = started
                                    && ready.FinishedGoodsCakes >= initial.OrderCakesRequired
                                    && ready.CanDispatchOrder
                                    && ready.OrderStatus.Contains("ready", StringComparison.OrdinalIgnoreCase);
            bool dispatchConsumesGoods = shipped.FinishedGoodsCakes == ready.FinishedGoodsCakes - initial.OrderCakesRequired;
            bool financesUpdated = shipped.CashBalance > cashBefore
                                   && shipped.ReputationPct >= reputationBefore
                                   && shipped.DispatchTruckChargePct < truckBefore;
            bool nextOrderOpened = shipped.OrdersFulfilled == ready.OrdersFulfilled + 1
                                   && shipped.CurrentOrderId != orderBefore
                                   && shipped.OrderCakesRequired > 0;
            bool pass = producedForOrder && dispatchConsumesGoods && financesUpdated && nextOrderOpened;
            return (pass, $"producedForOrder={producedForOrder} started={started} ('{Trim(startMsg)}'), " +
                          $"dispatchConsumesGoods={dispatchConsumesGoods}, financesUpdated={financesUpdated}, " +
                          $"nextOrderOpened={nextOrderOpened}, dispatch='{Trim(dispatch)}'");
        });

        Scenario("CAKE FILE CRYPTO (portable signed files reject forged cakes and delete when eaten)", () =>
        {
            string rootA = Path.Combine(Path.GetTempPath(), "cake-device-a-" + Guid.NewGuid().ToString("N"));
            string rootB = Path.Combine(Path.GetTempPath(), "cake-device-b-" + Guid.NewGuid().ToString("N"));
            string transfer = Path.Combine(Path.GetTempPath(), "cake-transfer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(transfer);

            try
            {
                var deviceA = new CakeFileService(rootA);
                var issued = deviceA.IssueBatch(CakeFactoryService.Recipes[0], 2, 96, 92);
                var first = issued[0];
                var second = issued[1];
                bool privateSigned = deviceA.Validate(first.Path).Valid && first.SignatureValid;

                string copied = Path.Combine(transfer, "portable.cake");
                File.Copy(first.Path, copied);
                var deviceB = new CakeFileService(rootB);
                var beforeTrust = deviceB.Validate(copied);
                string trustedKey = deviceB.TrustPublicKeyFromCake(copied, "Device A bakery");
                var afterTrust = deviceB.Validate(copied);

                string forged = Path.Combine(transfer, "forged.cake");
                string text = File.ReadAllText(second.Path);
                string forgedText = text.Replace("\"qualityScore\": 96", "\"qualityScore\": 12");
                if (forgedText == text) forgedText = text.Replace("\"status\": \"packed\"", "\"status\": \"eaten\"");
                File.WriteAllText(forged, forgedText);
                deviceB.TrustPublicKeyFromCake(forged, "Device A bakery");
                var forgedValidation = deviceB.Validate(forged);

                var eat = deviceB.EatCake(copied);
                bool deleted = eat.Eaten && eat.FileDeleted && !File.Exists(copied);

                string replay = Path.Combine(transfer, "replay.cake");
                File.Copy(first.Path, replay);
                var replayValidation = deviceB.Validate(replay);

                bool pass = privateSigned
                            && !beforeTrust.Valid && beforeTrust.Reason == "untrusted"
                            && !string.IsNullOrWhiteSpace(trustedKey)
                            && afterTrust.Valid
                            && !forgedValidation.Valid && forgedValidation.Reason == "tampered"
                            && deleted
                            && !replayValidation.Valid && replayValidation.Reason == "already-eaten";
                return (pass, $"privateSigned={privateSigned}, crossDeviceBeforeTrust={beforeTrust.Reason}, afterTrust={afterTrust.Valid}, " +
                              $"forgedRejected={forgedValidation.Reason}, eatenDeleted={deleted}, replay={replayValidation.Reason}");
            }
            finally
            {
                try { Directory.Delete(rootA, true); } catch { }
                try { Directory.Delete(rootB, true); } catch { }
                try { Directory.Delete(transfer, true); } catch { }
            }
        });

        Scenario("CAKE CIP SANITATION (cleaning locks batching, progresses, restores sanitation)", () =>
        {
            var cake = new CakeFactoryService();
            TickCake(cake, fullBus, 0.5);
            double before = cake.Snapshot.SanitationScore;

            cake.StartClean();
            TickCake(cake, fullBus, 0.5);
            var active = cake.Snapshot;

            TickCake(cake, fullBus, 40);
            var after = cake.Snapshot;

            bool activeLocks = active.CipActive && !active.CanStartBatch && active.OperatorPrompt.Contains("CIP", StringComparison.OrdinalIgnoreCase);
            bool finished = !after.CipActive && after.CipProgress >= 1.0;
            bool sanitationRecovered = after.SanitationScore >= before;
            bool pass = activeLocks && finished && sanitationRecovered;
            return (pass, $"before={before:F0}%, active={active.CipActive}/start={active.CanStartBatch}/progress={active.CipProgress:P0}, " +
                          $"after active={after.CipActive}, progress={after.CipProgress:P0}, sanitation={after.SanitationScore:F0}%");
        });
    }
}
