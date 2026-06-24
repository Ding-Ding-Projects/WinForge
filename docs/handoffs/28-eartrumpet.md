# Handoff: EarTrumpet (per-app volume)

| | |
|---|---|
| **Status** | Not started |
| **Source** | github.com/File-New-Project/EarTrumpet (C#/WPF). Native clone — extend WinForge's existing `Services/AudioMixer.cs` + `Pages/VolumeMixerModule.xaml`. |
| **License** | EarTrumpet is MIT (open source). Concepts/UX are freely portable; do not paste code verbatim — WinForge's Core Audio interop is already original. |
| **Proposed module** | Volume Mixer (extend existing) · Media group · Tag `module.mixer` (existing). No new nav item required. |
| **Effort** | M — the hard part (per-app volume via WASAPI COM) already ships; this adds device enumeration, per-app/global default-device switching, stream-move, and a flyout. |

## What the user asked for
Bring EarTrumpet's per-app audio control into WinForge: independent per-app volume/mute (already done), plus per-app default playback device, moving an app's stream between output devices, and a quick taskbar-style flyout — by extending the existing Volume Mixer module rather than redirecting to EarTrumpet.

## Recommended approach
**Native C# clone (extend existing module).** Per the global strategy this is the ideal native case: EarTrumpet is itself C#, and WinForge already has working raw Core Audio (WASAPI) COM interop in `Services/AudioMixer.cs` (master + per-session level/mute on the default render endpoint). v1 extends that service and the existing page; no winget binary needed. Honest scope: per-app default device and stream-move use `IPolicyConfig` (`SetDefaultEndpoint`) and `IAudioPolicyConfigFactory.SetPersistedDefaultAudioEndpoint` — both **undocumented** COM interfaces whose vtable layout varies by Windows build; budget interop-stabilisation time and gate them behind capability checks.

## Features to implement (v1 → later)
- **v1:** enumerate all active **render endpoints** (not just default) and let the user pick which device the mixer shows; a per-device master slider; group app sessions under their device; **set a specific output device as system default** (`IPolicyConfig.SetDefaultEndpoint` for eConsole/eMultimedia/eCommunications); **move an app's stream to another device** via per-app default (`SetPersistedDefaultAudioEndpoint(processId, flow, role, deviceId)`). Keep existing per-app/master volume + mute and auto-unmute-on-drag.
- **later:** lightweight **flyout window** (compact borderless window summoned via a global hotkey / tray, mirroring EarTrumpet's panel); live updates via `IMMNotificationClient` + `IAudioSessionNotification` (replace the manual Rescan); per-app icons from the process exe; input/capture (mic) devices; remembered per-app device assignments across restarts.

## Integration plan (WinForge specifics)
- **New/changed files:** extend `Services/AudioMixer.cs` (add `GetRenderDevices()`, `GetSessionsForDevice(id)`, `SetDefaultEndpoint(id, role)`, `SetAppDefaultDevice(pid, id)`; add `IMMDeviceCollection`, `IMMEndpoint`, `IPolicyConfig`, `IAudioPolicyConfigFactory` interop). New `Services/AudioPolicyConfig.cs` for the undocumented default-device interfaces. Extend `Pages/VolumeMixerModule.xaml(.cs)` (device picker `ComboBox`, per-app "move to device" flyout/MenuFlyout). Optional `Pages/VolumeFlyoutWindow.xaml(.cs)` for the "later" flyout.
- **Nav wiring:** none new — `module.mixer` already exists in `MainWindow.xaml` (Media group), `ModuleRegistry`, and `MainWindow.xaml.cs` (`MapType` / `NavView_SelectionChanged` / `ApplyStartPage --page mixer`). Optionally add a `module.mixer` deep-link arg for the flyout.
- **Engine/install:** winget id **n/a** — fully native COM; no `EngineBars.AutoInstallButton` needed.
- **Key APIs/CLIs:** `IMMDeviceEnumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE)`; `IMMDevice.GetId` / `OpenPropertyStore` (PKEY_Device_FriendlyName); `IPolicyConfig::SetDefaultEndpoint`; `IAudioPolicyConfigFactory::SetPersistedDefaultAudioEndpoint`. Build on the existing fresh-enumerator-per-call pattern; run COM on the UI thread (STA).

## Dependencies & risks
- `IPolicyConfig` / `IAudioPolicyConfigFactory` are **undocumented and build-dependent** — vtable offsets differ across Windows 10/11 builds; wrap in try/catch and feature-detect, degrade gracefully if a call fails.
- Per-app default-device persistence can be flaky for apps that pin their own endpoint; surface a clear "may not apply until app restarts" note.
- Moving streams while a session is active can briefly stutter audio — expected.
- COM lifetime: keep releasing every interface (existing `Release` helper) to avoid leaks on repeated rescans.

## Acceptance criteria
- Builds clean (Debug + Release x64); the existing Volume Mixer module still appears in nav/search and gains the device picker.
- Core flow works: list render devices, switch system default device, set a per-app output device / move a stream, all existing volume/mute behaviour intact.
- All new user-facing strings bilingual (English + 粵語); device-switch failures show a friendly InfoBar, never crash; no WinRT pickers (use `FileDialogs` if any file UI is added).
