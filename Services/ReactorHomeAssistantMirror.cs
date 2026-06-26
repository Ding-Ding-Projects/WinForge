using System;
using System.Collections.Generic;
using System.Linq;
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
///   • ALARM light entities — turned ON (red, if RGB-capable) during SCRAM / meltdown, OFF when normal;
///   • GENERATING light entities — held ON, full-bright white while the generator is on-load;
///   • GENERATING switch / plug entities — ON while the generator is on-load, OFF otherwise.
///
/// Robustness: every HA call is awaited fire-and-forget inside try/catch and can NEVER throw into the
/// reactor tick. State is edge-driven (we only push a call when the desired HA state actually changes),
/// so we do not spam the REST API. Configuration (the master toggle + selected entity ids) persists via
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
    public const string KeyGenLights = "reactor.ha.mirror.genLights";
    public const string KeyGenSwitch = "reactor.ha.mirror.genSwitch";
    private static readonly TimeSpan AssertInterval = TimeSpan.FromSeconds(30);

    // Shared, lazily-built REST client (config is read from SettingsStore in its ctor). Rebuildable so a
    // freshly-saved HA URL/token on the Home Assistant module is picked up without an app restart.
    private HomeAssistantService _ha = new();

    private bool _enabled;
    private List<string> _alarmLightIds = new();
    private List<string> _genLightIds = new();
    private List<string> _genSwitchIds = new();

    // Edge memory: last value we actually pushed (null = unknown / not yet pushed this session).
    private bool? _lastAlarmOn;
    private bool? _lastGenOn;
    private DateTime _lastAlarmAssertUtc = DateTime.MinValue;
    private DateTime _lastGenAssertUtc = DateTime.MinValue;
    private bool _busy; // single-flight guard so overlapping ticks never queue concurrent REST calls

    private ReactorHomeAssistantMirror()
    {
        _enabled = SettingsStore.Get(KeyEnabled, "False") == "True";
        _alarmLightIds = LoadIds(KeyAlarmLight);
        _genLightIds = LoadIds(KeyGenLights);
        _genSwitchIds = LoadIds(KeyGenSwitch);
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

    public IReadOnlyList<string> AlarmLightIds => _alarmLightIds;
    public IReadOnlyList<string> GenLightIds => _genLightIds;
    public IReadOnlyList<string> GenSwitchIds => _genSwitchIds;

    // Back-compat for old code/settings that used a single entity id.
    public string AlarmLightId
    {
        get => _alarmLightIds.FirstOrDefault() ?? "";
        set => SetAlarmLightIds(ToList(value));
    }

    public string GenSwitchId
    {
        get => _genSwitchIds.FirstOrDefault() ?? "";
        set => SetGenSwitchIds(ToList(value));
    }

    public void SetAlarmLightIds(IEnumerable<string> ids)
    {
        _alarmLightIds = NormalizeIds(ids);
        SaveIds(KeyAlarmLight, _alarmLightIds);
        _lastAlarmOn = null;
        _lastAlarmAssertUtc = DateTime.MinValue;
    }

    public void SetGenLightIds(IEnumerable<string> ids)
    {
        _genLightIds = NormalizeIds(ids);
        SaveIds(KeyGenLights, _genLightIds);
        _lastGenOn = null;
        _lastGenAssertUtc = DateTime.MinValue;
    }

    public void SetGenSwitchIds(IEnumerable<string> ids)
    {
        _genSwitchIds = NormalizeIds(ids);
        SaveIds(KeyGenSwitch, _genSwitchIds);
        _lastGenOn = null;
        _lastGenAssertUtc = DateTime.MinValue;
    }

    public void SetAlarmLightSelected(string id, bool selected) =>
        SetAlarmLightIds(Toggled(_alarmLightIds, id, selected));

    public void SetGenLightSelected(string id, bool selected) =>
        SetGenLightIds(Toggled(_genLightIds, id, selected));

    public void SetGenSwitchSelected(string id, bool selected) =>
        SetGenSwitchIds(Toggled(_genSwitchIds, id, selected));

    /// <summary>
    /// 由反應堆 tick 驅動 · Called from the reactor tick. Pushes HA service calls only on a state edge.
    /// <paramref name="alarmActive"/> = SCRAM or meltdown; <paramref name="generating"/> = on-load output.
    /// Never throws; returns immediately if disabled / unconfigured / already mid-flight.
    /// </summary>
    public void Drive(bool alarmActive, bool generating)
    {
        if (!_enabled || _busy) return;
        if (!_ha.IsConfigured) return;

        DateTime now = DateTime.UtcNow;
        bool needAlarm = _alarmLightIds.Count > 0 &&
            (_lastAlarmOn != alarmActive || (alarmActive && now - _lastAlarmAssertUtc >= AssertInterval));
        bool needGen = (_genLightIds.Count > 0 || _genSwitchIds.Count > 0) &&
            (_lastGenOn != generating || (generating && now - _lastGenAssertUtc >= AssertInterval));
        if (!needAlarm && !needGen) return;

        _busy = true;
        _ = PushAsync(needAlarm, alarmActive, needGen, generating, now);
    }

    private async Task PushAsync(bool needAlarm, bool alarmActive, bool needGen, bool generating, DateTime pushedAtUtc)
    {
        try
        {
            if (needAlarm)
            {
                try
                {
                    foreach (string id in _alarmLightIds)
                    {
                        if (alarmActive)
                            // Turn each alarm light on and (if it supports colour) make it red at full brightness.
                            await _ha.SetLight(id, 100, null, (255, 0, 0)).ConfigureAwait(false);
                        else
                            await _ha.LightOff(id).ConfigureAwait(false);
                    }
                    _lastAlarmOn = alarmActive;
                    if (alarmActive) _lastAlarmAssertUtc = pushedAtUtc;
                }
                catch { /* leave _lastAlarmOn unchanged so a later tick retries */ }
            }

            if (needGen)
            {
                try
                {
                    var alarmSet = alarmActive
                        ? new HashSet<string>(_alarmLightIds, StringComparer.OrdinalIgnoreCase)
                        : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string id in _genLightIds)
                    {
                        if (generating)
                            await _ha.SetLight(id, 100, null, (255, 255, 255)).ConfigureAwait(false);
                        else if (!alarmSet.Contains(id))
                            await _ha.LightOff(id).ConfigureAwait(false);
                    }

                    foreach (string id in _genSwitchIds)
                    {
                        if (generating) await _ha.TurnOn(id).ConfigureAwait(false);
                        else await _ha.TurnOff(id).ConfigureAwait(false);
                    }
                    _lastGenOn = generating;
                    if (generating) _lastGenAssertUtc = pushedAtUtc;
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
            try
            {
                foreach (string id in _alarmLightIds) await _ha.LightOff(id).ConfigureAwait(false);
            }
            catch { }
            try
            {
                foreach (string id in _genLightIds.Except(_alarmLightIds, StringComparer.OrdinalIgnoreCase))
                    await _ha.LightOff(id).ConfigureAwait(false);
            }
            catch { }
            try
            {
                foreach (string id in _genSwitchIds) await _ha.TurnOff(id).ConfigureAwait(false);
            }
            catch { }
        });
    }

    private static List<string> LoadIds(string key) =>
        NormalizeIds((SettingsStore.Get(key, "") ?? "")
            .Split(new[] { '\n', '\r', '|', ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static void SaveIds(string key, IEnumerable<string> ids) =>
        SettingsStore.Set(key, string.Join("\n", NormalizeIds(ids)));

    private static List<string> NormalizeIds(IEnumerable<string> ids) =>
        ids.Where(id => !string.IsNullOrWhiteSpace(id))
           .Select(id => id.Trim())
           .Distinct(StringComparer.OrdinalIgnoreCase)
           .ToList();

    private static IEnumerable<string> Toggled(IEnumerable<string> current, string id, bool selected)
    {
        var set = new HashSet<string>(NormalizeIds(current), StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(id)) return set;
        if (selected) set.Add(id.Trim());
        else set.Remove(id.Trim());
        return set;
    }

    private static List<string> ToList(string? id) =>
        string.IsNullOrWhiteSpace(id) ? new List<string>() : new List<string> { id.Trim() };
}
