using System;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WinForge.Services;

/// <summary>
/// 反應堆連動 Windows 系統設定（選用、可逆）· OPT-IN, fully-reversible bridge that maps the simulated
/// reactor's live state onto REAL Windows settings, so the plant visibly "powers" the PC running it.
///
/// 預設關閉 · DEFAULT OFF. When enabled it:
///   • POWER PLAN  — at-power/generating → High/Ultimate Performance; startup/low-power → Balanced;
///                   tripped/shutdown/meltdown → Power Saver. (powrprof.dll PowerGet/SetActiveScheme)
///   • ACCENT COLOUR — stable → green; warning/partial-trip → amber; SCRAM/alarms → red;
///                   meltdown → pulsing deep red. (HKCU DWM/Explorer accent registry + broadcast +
///                   DwmSetColorizationColor)
///   • SCREEN BRIGHTNESS — dim on SCRAM; pulse low↔high on meltdown; restore on recovery.
///                   (WMI root\\wmi WmiMonitorBrightnessMethods.WmiSetBrightness — laptops/integrated)
///   • SYSTEM VOLUME (optional) — raised on emergency, restored after. (WASAPI via AudioMixer)
///
/// 快照／還原 · Snapshot &amp; restore: on Enable() it snapshots the active power scheme GUID, the
/// current accent colour bytes, the panel brightness and the master volume. On Disable() or app
/// exit / page unload it restores every original. Idempotent — a setting is only re-applied when its
/// target actually changes between ticks. Every OS call is wrapped; failures (desktops, VMs, locked
/// schemes) degrade to a silent no-op and never crash.
/// </summary>
public sealed class ReactorSystemLinkService
{
    public static ReactorSystemLinkService I { get; } = new();

    private const string KEnabled = "reactor.syslink.enabled";

    // ── well-known power-scheme GUIDs ──
    private static readonly Guid GuidHighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    private static readonly Guid GuidBalanced        = new("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid GuidPowerSaver       = new("a1841308-3541-4fab-bc81-f71556f20b4a");
    private static readonly Guid GuidUltimate         = new("e9a42b02-d5df-448d-aa00-03f14749eb61");

    // ── persisted user toggle (default OFF) ──
    public static bool EnabledSetting
    {
        get => SettingsStore.Get(KEnabled, "false") == "true";
        set => SettingsStore.Set(KEnabled, value ? "true" : "false");
    }

    /// <summary>連動是否生效中 · Is the linkage currently driving real settings?</summary>
    public bool Active { get; private set; }

    // ── snapshot of the user's originals (captured on Enable) ──
    private Guid? _origScheme;
    private byte[]? _origAccentBgr;     // 0x00BBGGRR DWORD form
    private int? _origBrightness;       // 0..100, null = unsupported
    private float? _origVolume;         // 0..1, null = unsupported/skip
    private bool _processExitHooked;

    // ── last-applied state (for idempotency) ──
    private Guid _appliedScheme = Guid.Empty;
    private uint _appliedAccent = 0xFFFFFFFF;
    private int _appliedBrightness = -1;
    private float _appliedVolume = -1f;
    private double _pulsePhase;         // meltdown pulse clock

    private ReactorSystemLinkService() { }

    // =====================================================================================
    //  ENABLE / DISABLE
    // =====================================================================================

    /// <summary>
    /// 啟用連動並快照原狀 · Turn the linkage on, snapshotting all originals first so they can be
    /// restored later. Safe to call repeatedly. Returns true if it is now active.
    /// </summary>
    public bool Enable()
    {
        if (Active) return true;
        // Snapshot originals BEFORE we touch anything.
        _origScheme = TryGetActiveScheme();
        _origAccentBgr = TryGetAccentBgr();
        _origBrightness = TryGetBrightness();
        _origVolume = TryGetVolume();

        // Reset idempotency caches so the first Apply() definitely writes.
        _appliedScheme = Guid.Empty;
        _appliedAccent = 0xFFFFFFFF;
        _appliedBrightness = -1;
        _appliedVolume = -1f;
        _pulsePhase = 0;

        Active = true;
        EnabledSetting = true;

        // Restore on process exit no matter how we leave (covers app close while linked).
        if (!_processExitHooked)
        {
            try { AppDomain.CurrentDomain.ProcessExit += (_, _) => RestoreAll(); _processExitHooked = true; }
            catch { /* best effort */ }
        }
        return true;
    }

    /// <summary>停用連動並還原所有原狀 · Turn the linkage off and restore every original setting.</summary>
    public void Disable()
    {
        EnabledSetting = false;
        if (!Active) return;
        Active = false;
        RestoreAll();
    }

    /// <summary>
    /// 還原所有快照值 · Restore power scheme, accent colour, brightness and volume to the values that
    /// were captured on Enable(). Idempotent and exception-safe; clears the snapshot afterwards so a
    /// double-call can't undo a later user change.
    /// </summary>
    public void RestoreAll()
    {
        if (_origScheme is Guid g) { TrySetActiveScheme(g); }
        if (_origAccentBgr is byte[] a) { TryApplyAccentBgr(a); }
        if (_origBrightness is int b) { TrySetBrightness(b); }
        if (_origVolume is float v) { TrySetVolume(v); }

        _origScheme = null;
        _origAccentBgr = null;
        _origBrightness = null;
        _origVolume = null;
        _appliedScheme = Guid.Empty;
        _appliedAccent = 0xFFFFFFFF;
        _appliedBrightness = -1;
        _appliedVolume = -1f;
        Active = false;
    }

    // =====================================================================================
    //  TICK — map reactor state → settings (call at ~1-2 Hz from the reactor page timer)
    // =====================================================================================

    /// <summary>
    /// 依反應堆狀態更新系統設定 · Drive the real settings from the current reactor state. No-op unless
    /// Active. <paramref name="dt"/> is the elapsed seconds (for the meltdown pulse).
    /// </summary>
    public void Apply(ReactorSimService sim, double dt)
    {
        if (!Active || sim is null) return;
        try { ApplyPowerPlan(sim); } catch { }
        try { ApplyAccent(sim, dt); } catch { }
        try { ApplyBrightness(sim, dt); } catch { }
        try { ApplyVolume(sim); } catch { }
    }

    // ---- power plan ----
    private void ApplyPowerPlan(ReactorSimService sim)
    {
        Guid want = sim.Mode switch
        {
            ReactorMode.Tripped  => GuidPowerSaver,
            ReactorMode.Shutdown => GuidPowerSaver,
            ReactorMode.Meltdown => GuidPowerSaver,
            ReactorMode.Startup  => GuidBalanced,
            // Run: full performance only when actually generating; otherwise balanced.
            ReactorMode.Run      => sim.ElectricPowerMW > 1.0 ? PreferHigh() : GuidBalanced,
            _ => GuidBalanced,
        };
        if (sim.IsScrammed) want = GuidPowerSaver; // a SCRAM trumps the mode

        if (want == _appliedScheme) return;        // idempotent
        if (TrySetActiveScheme(want)) _appliedScheme = want;
    }

    /// <summary>Ultimate Performance if the OS has it, else High Performance.</summary>
    private static Guid PreferHigh()
        => SchemeExists(GuidUltimate) ? GuidUltimate : GuidHighPerformance;

    // ---- accent colour ----
    private void ApplyAccent(ReactorSimService sim, double dt)
    {
        // green stable → amber warning/partial-trip → red SCRAM/alarms → pulsing deep red meltdown.
        byte r, g, b;
        if (sim.Mode == ReactorMode.Meltdown)
        {
            _pulsePhase += dt * 3.0; // ~0.5 Hz visible pulse
            double k = 0.5 + 0.5 * Math.Sin(_pulsePhase); // 0..1
            r = (byte)(120 + 110 * k); g = 0; b = 0;       // deep red → bright red
        }
        else if (sim.IsScrammed || AnyAlarm(sim))
        {
            r = 0xE5; g = 0x39; b = 0x35;                  // red
        }
        else if (sim.DamageAccumulation > 1.0 || AnyPartialTrip(sim))
        {
            r = 0xFB; g = 0xC0; b = 0x2D;                  // amber (warning / partial trip)
        }
        else
        {
            r = 0x4C; g = 0xAF; b = 0x50;                  // green
        }

        uint key = (uint)((r << 16) | (g << 8) | b);
        if (key == _appliedAccent) return;                 // idempotent (skip pulse no-change frames)
        // DWORD accent stored in 0x00BBGGRR order.
        var bgr = new byte[] { b, g, r, 0x00 };
        if (TryApplyAccentBgr(bgr)) _appliedAccent = key;
    }

    private static bool AnyAlarm(ReactorSimService sim)
    {
        foreach (ReactorAlarm a in Enum.GetValues(typeof(ReactorAlarm)))
            if (sim.Alarm(a)) return true;
        return false;
    }

    /// <summary>Any RPS protection function showing a single-channel (partial) trip → warning state.</summary>
    private static bool AnyPartialTrip(ReactorSimService sim)
    {
        foreach (var f in sim.Rps.Functions)
            if (f.PartialTrip || f.FunctionTrip) return true;
        return false;
    }

    // ---- brightness ----
    private void ApplyBrightness(ReactorSimService sim, double dt)
    {
        if (_origBrightness is null) return;               // panel doesn't support WMI brightness
        int want;
        if (sim.Mode == ReactorMode.Meltdown)
        {
            // pulse low↔high during meltdown
            double k = 0.5 + 0.5 * Math.Sin(_pulsePhase); // shares the accent pulse clock
            want = (int)(20 + 80 * k);
        }
        else if (sim.IsScrammed)
        {
            want = 30;                                     // dim on SCRAM
        }
        else
        {
            want = _origBrightness.Value;                  // back to the user's level on recovery
        }
        want = Math.Clamp(want, 0, 100);
        if (Math.Abs(want - _appliedBrightness) < 2 && sim.Mode != ReactorMode.Meltdown) return; // idempotent
        if (TrySetBrightness(want)) _appliedBrightness = want;
    }

    // ---- volume (optional, low-risk) ----
    private void ApplyVolume(ReactorSimService sim)
    {
        if (_origVolume is null) return;
        // Only raise the floor on a genuine emergency; never lower the user below where they were.
        float want;
        if (sim.Mode == ReactorMode.Meltdown) want = Math.Max(_origVolume.Value, 0.90f);
        else if (sim.IsScrammed) want = Math.Max(_origVolume.Value, 0.70f);
        else want = _origVolume.Value;
        if (Math.Abs(want - _appliedVolume) < 0.02f) return;
        if (TrySetVolume(want)) _appliedVolume = want;
    }

    // =====================================================================================
    //  POWER PLAN — powrprof.dll PInvoke
    // =====================================================================================

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr UserRootPowerKey, out IntPtr ActivePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);

    // Used only to probe whether a scheme (e.g. Ultimate Performance) exists on this machine.
    [DllImport("powrprof.dll")]
    private static extern uint PowerReadFriendlyName(
        IntPtr RootPowerKey, ref Guid SchemeGuid, IntPtr SubGroupOfPowerSettingsGuid,
        IntPtr PowerSettingGuid, IntPtr Buffer, ref uint BufferSize);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static Guid? TryGetActiveScheme()
    {
        IntPtr p = IntPtr.Zero;
        try
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out p) != 0 || p == IntPtr.Zero) return null;
            return Marshal.PtrToStructure<Guid>(p);
        }
        catch { return null; }
        finally { if (p != IntPtr.Zero) LocalFree(p); }
    }

    private static bool TrySetActiveScheme(Guid scheme)
    {
        try
        {
            var g = scheme;
            return PowerSetActiveScheme(IntPtr.Zero, ref g) == 0;
        }
        catch { return false; }
    }

    private static bool SchemeExists(Guid scheme)
    {
        try
        {
            var g = scheme;
            uint size = 0;
            // ERROR_FILE_NOT_FOUND (2) when the scheme is absent; ERROR_MORE_DATA/SUCCESS otherwise.
            uint rc = PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref size);
            return rc != 2 /* ERROR_FILE_NOT_FOUND */;
        }
        catch { return false; }
    }

    // =====================================================================================
    //  ACCENT COLOUR — registry write + broadcast + DwmSetColorizationColor
    //  (mirrors the app's existing theme/accent registry + WM_SETTINGCHANGE approach)
    // =====================================================================================

    private const string DwmPath = @"Software\Microsoft\Windows\DWM";
    private const string AccentPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam,
        uint flags, uint timeout, out IntPtr result);

    [DllImport("dwmapi.dll", PreserveSig = false)]
    private static extern void DwmSetColorizationColor(uint colorization, bool afterglow);

    private static readonly IntPtr HWND_BROADCAST = new(0xffff);
    private const uint WM_SETTINGCHANGE = 0x001A;
    private const uint WM_DWMCOLORIZATIONCOLORCHANGED = 0x0320;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>讀取目前強調色（0x00BBGGRR）· Read the current accent colour as a 0x00BBGGRR DWORD's bytes.</summary>
    private static byte[]? TryGetAccentBgr()
    {
        try
        {
            // DWM\AccentColor is the authoritative 0xAABBGGRR DWORD; fall back to a neutral blue.
            var v = RegistryHelper.GetValue(RegRoot.HKCU, DwmPath, "AccentColor");
            uint dword = v is int i ? unchecked((uint)i) : 0xFFD77800u; // default-ish accent
            byte b = (byte)((dword >> 16) & 0xFF);
            byte g = (byte)((dword >> 8) & 0xFF);
            byte r = (byte)(dword & 0xFF);
            return new byte[] { b, g, r, 0x00 };
        }
        catch { return null; }
    }

    /// <summary>
    /// 寫入強調色並即時廣播 · Write the accent colour to the registry (the same keys Windows
    /// Personalization writes) and broadcast so it applies live. <paramref name="bgr"/> is
    /// {B, G, R, 0}.
    /// </summary>
    private static bool TryApplyAccentBgr(byte[] bgr)
    {
        try
        {
            byte b = bgr[0], g = bgr[1], r = bgr[2];
            // 0x00BBGGRR — the form DWM\AccentColor and Explorer\Accent\StartColorMenu use.
            uint bbggrr = (uint)((b << 16) | (g << 8) | r);
            int dword = unchecked((int)(0xFF000000u | bbggrr)); // 0xFFBBGGRR for the DWM colorization

            // DWM accent (title bars / borders).
            RegistryHelper.SetValue(RegRoot.HKCU, DwmPath, "AccentColor", dword, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, DwmPath, "ColorizationColor", dword, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, DwmPath, "ColorizationAfterglow", dword, RegistryValueKind.DWord);

            // Explorer accent (Start / taskbar accent tint).
            RegistryHelper.SetValue(RegRoot.HKCU, AccentPath, "AccentColorMenu", dword, RegistryValueKind.DWord);
            RegistryHelper.SetValue(RegRoot.HKCU, AccentPath, "StartColorMenu", dword, RegistryValueKind.DWord);

            // 8-entry AccentPalette (dark→light shades of the base colour) — Windows reads index 5
            // as the primary. We build a simple ramp so the Settings UI shows a coherent swatch set.
            RegistryHelper.SetValue(RegRoot.HKCU, AccentPath, "AccentPalette",
                BuildPalette(r, g, b), RegistryValueKind.Binary);

            // Live apply: DWM colorization (immediate title-bar/border tint) + broadcast for the rest.
            try { DwmSetColorizationColor(unchecked((uint)dword), false); } catch { }
            SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "ImmersiveColorSet",
                SMTO_ABORTIFHUNG, 3000, out _);
            SendMessageTimeout(HWND_BROADCAST, WM_DWMCOLORIZATIONCOLORCHANGED, IntPtr.Zero, "",
                SMTO_ABORTIFHUNG, 3000, out _);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Build the 32-byte (8×RGBA) AccentPalette ramp Windows expects, base colour at index 5.</summary>
    private static byte[] BuildPalette(byte r, byte g, byte b)
    {
        // Eight shades from dark to light; entries are RGBA.
        double[] scale = { 0.45, 0.60, 0.75, 0.90, 1.00, 1.15, 1.30, 1.50 };
        var pal = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            pal[i * 4 + 0] = (byte)Math.Clamp(r * scale[i], 0, 255);
            pal[i * 4 + 1] = (byte)Math.Clamp(g * scale[i], 0, 255);
            pal[i * 4 + 2] = (byte)Math.Clamp(b * scale[i], 0, 255);
            pal[i * 4 + 3] = 0xFF;
        }
        return pal;
    }

    // =====================================================================================
    //  BRIGHTNESS — WMI root\wmi WmiMonitorBrightnessMethods (laptops / integrated panels)
    // =====================================================================================

    private static int? TryGetBrightness()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            foreach (var mo in searcher.Get())
            {
                var v = mo["CurrentBrightness"];
                if (v != null) return Convert.ToInt32(v);
            }
        }
        catch { /* unsupported (desktop / VM) → null */ }
        return null;
    }

    private static bool TrySetBrightness(int percent)
    {
        try
        {
            percent = Math.Clamp(percent, 0, 100);
            using var searcher = new ManagementObjectSearcher(
                "root\\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
            foreach (ManagementObject mo in searcher.Get())
            {
                // WmiSetBrightness(uint Timeout, uint Brightness)
                mo.InvokeMethod("WmiSetBrightness", new object[] { (uint)1, (byte)percent });
                return true;
            }
        }
        catch { /* unsupported → no-op */ }
        return false;
    }

    // =====================================================================================
    //  VOLUME — reuse the app's WASAPI master-volume helper if present
    // =====================================================================================

    private static float? TryGetVolume()
    {
        try { return AudioMixer.GetMaster().level; }
        catch { return null; }
    }

    private static bool TrySetVolume(float level)
    {
        try { AudioMixer.SetMasterLevel(Math.Clamp(level, 0f, 1f)); return true; }
        catch { return false; }
    }
}
