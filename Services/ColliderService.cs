using System;

namespace WinForge.Services;

/// <summary>
/// 粒子對撞機模擬引擎 · Particle-collider simulation engine — a HEAVY reactor-powered load. The
/// superconducting magnets need enormous power to hold a high-energy beam: required magnet power grows
/// roughly with the square of beam energy (~800 MW at the 14 TeV maximum). Beam energy ramps toward the
/// operator's target ONLY while the reactor can supply the required MW; if available power falls short,
/// energy is capped there; if the reactor stops generating, a BEAM DUMP occurs (energy → 0). While at or
/// above a collision threshold and stable, integrated luminosity and recorded events accumulate, with
/// occasional "discovery!" milestones. All state lives here; every method is best-effort and never throws.
/// Ramps are driven by an internal integer tick counter — no wall-clock / date logic.
/// </summary>
public sealed class ColliderService
{
    // ── Physical constants (tuned for a satisfying game feel, not literal physics) ──────────────────
    public const double MaxBeamTeV = 14.0;      // operator target ceiling
    public const double MaxMagnetMW = 800.0;    // magnet power at full beam energy
    public const double CollisionThresholdTeV = 3.0; // minimum energy to record collisions

    private const double RampPerTick = 0.06;    // TeV gained per tick when fully powered
    private const double DecayPerTick = 0.10;   // TeV lost per tick when starved of power

    // ── Reactor-economy integration (Watts ⚡) ──────────────────────────────────────────────────────
    /// <summary>Fixed Watts bounty awarded to the reactor economy per discovery milestone.</summary>
    public const double DiscoveryBounty = 250.0;

    /// <summary>
    /// "Priority beam time" perk (bought in the Reactor Bank): a faster energy ramp and a higher
    /// luminosity multiplier. Toggled by the UI from <c>ReactorEconomyService.I.IsUnlocked(...)</c>.
    /// </summary>
    public bool PriorityBeam { get; set; }

    private const double PriorityRampMultiplier = 1.6;   // faster ramp when priority beam time is owned
    private const double PriorityLumiMultiplier = 1.5;   // higher luminosity when priority beam time is owned

    // Number of discovery milestones already paid out to the economy — never double-pay.
    public int DiscoveriesAwarded { get; private set; }

    // Luminosity thresholds (fb^-1) at which a discovery milestone fires.
    private static readonly double[] DiscoveryMarks = { 5.0, 25.0, 75.0, 150.0, 300.0, 600.0, 1200.0 };

    // ── Operator inputs / mode ──────────────────────────────────────────────────────────────────────
    public bool Ramping { get; private set; }
    public double TargetTeV { get; private set; }

    // ── Live state ────────────────────────────────────────────────────────────────────────────────
    public double BeamEnergyTeV { get; private set; }
    public double RequiredMW { get; private set; }
    public double AvailableMW { get; private set; }
    public double IntegratedLuminosity { get; private set; } // fb^-1
    public long EventsRecorded { get; private set; }
    public int Discoveries { get; private set; }
    public string LastDiscovery { get; private set; } = "";

    public bool BeamDumped { get; private set; }
    public bool PowerStarved { get; private set; }     // wanted to ramp but reactor could not supply
    public bool Colliding { get; private set; }        // at/above threshold, stable, powered

    private long _tick;
    private int _nextDiscoveryIdx;

    /// <summary>Magnet power required to hold a given beam energy (~ energy², capped at MaxMagnetMW).</summary>
    public static double RequiredMagnetPower(double beamTeV)
    {
        double e = Clamp(beamTeV, 0, MaxBeamTeV);
        double frac = (e / MaxBeamTeV) * (e / MaxBeamTeV); // energy²
        double mw = frac * MaxMagnetMW;
        return double.IsNaN(mw) || mw < 0 ? 0 : mw;
    }

    /// <summary>Operator sets the desired beam-energy target (TeV).</summary>
    public void SetTarget(double teV)
    {
        if (double.IsNaN(teV)) teV = 0;
        TargetTeV = Clamp(teV, 0, MaxBeamTeV);
    }

    public void StartRamp() => Ramping = true;
    public void Standby() => Ramping = false;

    public void Reset()
    {
        Ramping = false;
        TargetTeV = 0;
        BeamEnergyTeV = 0;
        RequiredMW = 0;
        AvailableMW = 0;
        IntegratedLuminosity = 0;
        EventsRecorded = 0;
        Discoveries = 0;
        LastDiscovery = "";
        BeamDumped = false;
        PowerStarved = false;
        Colliding = false;
        DiscoveriesAwarded = 0;
        _tick = 0;
        _nextDiscoveryIdx = 0;
    }

    /// <summary>
    /// Advance the simulation by one tick. <paramref name="availableMW"/> is the reactor's live electric
    /// output; <paramref name="generating"/> is whether the reactor is actually producing power. Returns a
    /// discovery headline when a milestone fires this tick, otherwise <c>null</c>. Never throws.
    /// </summary>
    public string? Tick(double availableMW, bool generating)
    {
        try
        {
            _tick++;
            if (double.IsNaN(availableMW) || availableMW < 0) availableMW = 0;
            AvailableMW = availableMW;
            PowerStarved = false;
            BeamDumped = false;
            Colliding = false;

            // No reactor power → immediate beam dump.
            if (!generating || availableMW <= 1.0)
            {
                if (BeamEnergyTeV > 0.001)
                {
                    BeamDumped = true;
                    BeamEnergyTeV = 0;
                }
                RequiredMW = RequiredMagnetPower(BeamEnergyTeV);
                return null;
            }

            // Determine where we would like the beam to be this tick.
            double target = Ramping ? TargetTeV : 0.0;

            if (target > BeamEnergyTeV)
            {
                // Ramping up costs power. Work out the highest energy the reactor can currently sustain.
                double rampStep = RampPerTick * (PriorityBeam ? PriorityRampMultiplier : 1.0);
                double nextWanted = Math.Min(target, BeamEnergyTeV + rampStep);
                double neededForNext = RequiredMagnetPower(nextWanted);

                if (neededForNext <= availableMW)
                {
                    BeamEnergyTeV = nextWanted; // fully powered ramp
                }
                else
                {
                    // Cap the beam at the maximum energy this available power can hold.
                    double sustainable = MaxBeamTeV * Math.Sqrt(Clamp(availableMW / MaxMagnetMW, 0, 1));
                    PowerStarved = true;
                    if (sustainable < BeamEnergyTeV)
                        BeamEnergyTeV = Math.Max(sustainable, BeamEnergyTeV - DecayPerTick);
                    else
                        BeamEnergyTeV = Math.Min(nextWanted, sustainable);
                }
            }
            else if (target < BeamEnergyTeV)
            {
                // Standby or lowered target → controlled ramp-down.
                BeamEnergyTeV = Math.Max(target, BeamEnergyTeV - DecayPerTick);
            }

            BeamEnergyTeV = Clamp(BeamEnergyTeV, 0, MaxBeamTeV);
            RequiredMW = RequiredMagnetPower(BeamEnergyTeV);

            // Also enforce the sustainable cap when already holding (available power may have dropped).
            if (RequiredMW > availableMW + 0.5)
            {
                double sustainable = MaxBeamTeV * Math.Sqrt(Clamp(availableMW / MaxMagnetMW, 0, 1));
                if (sustainable < BeamEnergyTeV)
                {
                    PowerStarved = true;
                    BeamEnergyTeV = Math.Max(sustainable, BeamEnergyTeV - DecayPerTick);
                    BeamEnergyTeV = Clamp(BeamEnergyTeV, 0, MaxBeamTeV);
                    RequiredMW = RequiredMagnetPower(BeamEnergyTeV);
                }
            }

            // Accumulate physics only when at/above threshold and powered.
            if (BeamEnergyTeV >= CollisionThresholdTeV && RequiredMW <= availableMW + 0.5)
            {
                Colliding = true;
                // Luminosity/event rate scale with beam energy above threshold.
                double over = (BeamEnergyTeV - CollisionThresholdTeV) / (MaxBeamTeV - CollisionThresholdTeV);
                over = Clamp(over, 0, 1);
                double lumiRate = 0.05 + 0.45 * over;      // fb^-1 per tick
                if (PriorityBeam) lumiRate *= PriorityLumiMultiplier; // priority beam time → higher luminosity
                IntegratedLuminosity += lumiRate;
                EventsRecorded += (long)Math.Round(1000 + 90000 * over);

                if (_nextDiscoveryIdx < DiscoveryMarks.Length &&
                    IntegratedLuminosity >= DiscoveryMarks[_nextDiscoveryIdx])
                {
                    _nextDiscoveryIdx++;
                    Discoveries++;
                    LastDiscovery = DiscoveryName(Discoveries);
                    return LastDiscovery;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Claim the Watts bounty for any discovery milestones that have fired but not yet been paid to the
    /// reactor economy. Returns the total <c>⚡</c> owed this call (bounty × newly-earned discoveries) and
    /// marks them as awarded so they are never double-paid. Returns 0 when nothing is owed.
    /// </summary>
    public double ClaimDiscoveryBounty()
    {
        int owed = Discoveries - DiscoveriesAwarded;
        if (owed <= 0) return 0;
        DiscoveriesAwarded = Discoveries;
        return owed * DiscoveryBounty;
    }

    private static string DiscoveryName(int n)
    {
        // Deterministic sequence of playful "particle" names; never depends on wall clock.
        string[] names =
        {
            "unknown resonance",
            "exotic hadron",
            "long-lived boson",
            "heavy lepton",
            "dark-sector candidate",
            "sterile neutrino hint",
            "beyond-standard-model signal",
        };
        int idx = (n - 1) % names.Length;
        return idx >= 0 && idx < names.Length ? names[idx] : "new particle";
    }

    private static double Clamp(double v, double lo, double hi)
        => v < lo ? lo : (v > hi ? hi : v);
}
