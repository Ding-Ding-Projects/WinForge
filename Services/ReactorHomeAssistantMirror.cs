using System;
using System.Threading.Tasks;

namespace WinForge.Services;

/// <summary>
/// 真實關機致動旗標（預設關閉，唔會跨工作階段保存）· In-memory ARM flag for real shutdown on meltdown.
///
/// The Reactor Settings page sets this; <c>ReactorModule</c> reads it when a meltdown occurs to decide
/// whether to start the abortable real-shutdown countdown. Kept in memory only (never persisted) so the
/// dangerous option always defaults back to OFF on every app launch — a deliberate safety choice.
/// </summary>
public static class ReactorRealShutdownArm
{
    /// <summary>DEFAULT OFF. When true, a meltdown starts the 10-second abortable real-shutdown countdown.</summary>
    public static bool Armed { get; set; }
}

/// <summary>
/// 發電時保持電腦喚醒設定（預設開啟，會保存）· Keep-PC-awake-while-generating user setting.
///
/// Configured on the Reactor Settings page; <c>ReactorModule</c> reads <see cref="Enabled"/> each tick to
/// decide whether the simulated on-load generator should hold the real OS awake. Persisted via SettingsStore
/// (DEFAULT ON, matching the previous in-page toggle's default).
/// </summary>
public static class ReactorKeepAwakeSetting
{
    public const string Key = "reactor.keepAwake.enabled";

    /// <summary>DEFAULT ON.</summary>
    public static bool Enabled
    {
        get => SettingsStore.Get(Key, "True") == "True";
        set => SettingsStore.Set(Key, value ? "True" : "False");
    }
}

/// <summary>
/// 反應堆 ↔ Home Assistant 連動 · Mirrors the simulated reactor's state to Home Assistant.
///
/// OPT-IN, DEFAULT OFF and fully reversible. When the user enables it on the Reactor Settings page and
/// picks entities, the reactor's live tick (in <c>ReactorModule</c>) calls <see cref="Drive"/> a few
/// times a second. We translate two reactor conditions into HA service calls over the existing
/// <see cref="HomeAssistantService"/> REST client:
///   • an ALARM light entity — turned ON (red, if it is an RGB light) during SCRAM / meltdown, OFF when normal;
///   • a GENERATING switch / plug entity — ON while the generator is on-load delivering power, OFF otherwise.
///
/// Robustness: every HA call is awaited fire-and-forget inside try/catch and can NEVER throw into the
/// reactor tick. State is edge-driven (we only push a call when the desired HA state actually changes),
/// so we do not spam the REST API. Configuration (the master toggle + the two entity ids) persists via
/// <see cref="SettingsStore"/>. Turning the mirror off stops driving and (best-effort) restores both
/// entities to OFF so the house is never left lit up by a closed simulation.
///
/// 全部 HA 呼叫都包喺 try/catch，永遠唔會擲入反應堆 tick；只係喺目標狀態改變時先推送，唔會洗版。
/// </summary>
public sealed class ReactorHomeAssistantMirror
{
    public static ReactorHomeAssistantMirror I { get; } = new();

    public const string KeyEnabled = "reactor.ha.mirror.enabled";   // "True"/"False" — DEFAULT OFF
    public const string KeyAlarmLight = "reactor.ha.mirror.alarmLight";
    public const string KeyGenSwitch = "reactor.ha.mirror.genSwitch";

    // Shared, lazily-built REST client (config is read from SettingsStore in its ctor). Rebuildable so a
    // freshly-saved HA URL/token on the Home Assistant module is picked up without an app restart.
    private HomeAssistantService _ha = new();

    private bool _enabled;
    private string _alarmLightId = "";
    private string _genSwitchId = "";

    // Edge memory: last value we actually pushed (null = unknown / not yet pushed this session).
    private bool? _lastAlarmOn;
    private bool? _lastGenOn;
    private bool _busy; // single-flight guard so overlapping ticks never queue concurrent REST calls

    private ReactorHomeAssistantMirror()
    {
        _enabled = SettingsStore.Get(KeyEnabled, "False") == "True";
        _alarmLightId = SettingsStore.Get(KeyAlarmLight, "");
        _genSwitchId = SettingsStore.Get(KeyGenSwitch, "");
    }

    /// <summary>The shared HA REST client (reads persisted URL + token).</summary>
    public HomeAssistantService Ha => _ha;

    /// <summary>Re-read the HA URL/token from settings (call after the user saves HA config elsewhere).</summary>
    public void RefreshHaConfig() => _ha = new HomeAssistantService();

    public bool IsHaConfigured => _ha.IsConfigured;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            SettingsStore.Set(KeyEnabled, value ? "True" : "False");
            if (!value)
            {
                // Turning off: stop driving and best-effort restore both entities to OFF.
                _lastAlarmOn = null;
                _lastGenOn = null;
                RestoreOff();
            }
        }
    }

    public string AlarmLightId
    {
        get => _alarmLightId;
        set
        {
            _alarmLightId = value ?? "";
            SettingsStore.Set(KeyAlarmLight, _alarmLightId);
            _lastAlarmOn = null; // force a fresh push to the newly-selected entity
        }
    }

    public string GenSwitchId
    {
        get => _genSwitchId;
        set
        {
            _genSwitchId = value ?? "";
            SettingsStore.Set(KeyGenSwitch, _genSwitchId);
            _lastGenOn = null;
        }
    }

    /// <summary>
    /// 由反應堆 tick 驅動 · Called from the reactor tick. Pushes HA service calls only on a state edge.
    /// <paramref name="alarmActive"/> = SCRAM or meltdown; <paramref name="generating"/> = on-load output.
    /// Never throws; returns immediately if disabled / unconfigured / already mid-flight.
    /// </summary>
    public void Drive(bool alarmActive, bool generating)
    {
        if (!_enabled || _busy) return;
        if (!_ha.IsConfigured) return;

        bool needAlarm = _alarmLightId.Length > 0 && _lastAlarmOn != alarmActive;
        bool needGen = _genSwitchId.Length > 0 && _lastGenOn != generating;
        if (!needAlarm && !needGen) return;

        _busy = true;
        _ = PushAsync(needAlarm, alarmActive, needGen, generating);
    }

    private async Task PushAsync(bool needAlarm, bool alarmActive, bool needGen, bool generating)
    {
        try
        {
            if (needAlarm)
            {
                try
                {
                    if (alarmActive)
                        // Turn the alarm light on and (if it supports colour) make it red at full brightness.
                        await _ha.SetLight(_alarmLightId, 100, null, (255, 0, 0)).ConfigureAwait(false);
                    else
                        await _ha.LightOff(_alarmLightId).ConfigureAwait(false);
                    _lastAlarmOn = alarmActive;
                }
                catch { /* leave _lastAlarmOn unchanged so we retry next edge */ }
            }

            if (needGen)
            {
                try
                {
                    if (generating) await _ha.TurnOn(_genSwitchId).ConfigureAwait(false);
                    else await _ha.TurnOff(_genSwitchId).ConfigureAwait(false);
                    _lastGenOn = generating;
                }
                catch { }
            }
        }
        catch { /* never propagate into the reactor tick */ }
        finally { _busy = false; }
    }

    /// <summary>Best-effort: turn both chosen entities OFF (used when the mirror is disabled).</summary>
    private void RestoreOff()
    {
        if (!_ha.IsConfigured) return;
        _ = Task.Run(async () =>
        {
            try { if (_alarmLightId.Length > 0) await _ha.LightOff(_alarmLightId).ConfigureAwait(false); } catch { }
            try { if (_genSwitchId.Length > 0) await _ha.TurnOff(_genSwitchId).ConfigureAwait(false); } catch { }
        });
    }
}
