# Home Assistant Â· å®¶å±…åŠ©ç†

**EN â€”** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**ç²µèªž â€”** å‘¢ä»½åŠŸèƒ½åƒè€ƒç”± WinForge æ¨¡çµ„ç™»è¨˜ã€å°Žè¦½åœ°åœ–åŒé é¢ XAML ç”Ÿæˆã€‚

| Field Â· æ¬„ä½ | Value Â· å€¼ |
|---|---|
| Tag Â· æ¨™ç±¤ | $(System.Collections.Specialized.OrderedDictionary["Tag"]) |
| Deep-link alias Â· æ·±å±¤é€£çµåˆ¥å | $(System.Collections.Specialized.OrderedDictionary["Alias"]) |
| Category Â· åˆ†é¡ž | Apps & Git Â· ç¨‹å¼èˆ‡ Git |
| Page class Â· é é¢é¡žåˆ¥ | $(System.Collections.Specialized.OrderedDictionary["Class"]) |
| Page XAML Â· é é¢ XAML | $(System.Collections.Specialized.OrderedDictionary["PageFile"]) |
| Button docs Â· æŒ‰éˆ•æ–‡ä»¶ | 35 |

## What It Covers Â· åŠŸèƒ½ç¯„åœ

**EN â€”** Home Assistant is registered in WinForge search and navigation with these keywords: $(System.Collections.Specialized.OrderedDictionary["Keywords"]).

**ç²µèªž â€”** å®¶å±…åŠ©ç† å·²ç™»è¨˜å–º WinForge æœå°‹åŒå°Žè¦½ï¼Œé—œéµå­—åŒ…æ‹¬ï¼š$(System.Collections.Specialized.OrderedDictionary["Keywords"])ã€‚

## Climate and AC Defender · 冷氣與 AC Defender

**EN —** WinForge talks to Home Assistant through the REST API. For AC Defender-style cooling control, use the AC Defender tab to generate a Docker/Compose bundle, export it for an SSH Docker host, or start/stop/status the local stack through WinForge's managed Docker client. The deployment watches Home Assistant entities such as `climate.*`, `sensor.*`, `switch.*` or `notify.*` and deliberately turns HVAC off instead of auto-adjusting the set temperature.

**粵語 —** WinForge 係透過 Home Assistant REST API 溝通。要做 AC Defender 式冷氣防護，可以用 AC Defender 分頁產生 Docker/Compose bundle、匯出畀 SSH Docker 主機，或者透過 WinForge managed Docker client 本機啟停同睇狀態。部署會監察 `climate.*`、`sensor.*`、`switch.*` 或 `notify.*` 等 Home Assistant 實體，而且只會關 HVAC，唔會自動調整設定溫度。

| Step · 步驟 | Operator action · 操作 |
|---|---|
| Deploy · 部署 | Run AC Defender with Docker/Compose on the Home Assistant host or another reachable Docker host. |
| Expose entities · 暴露實體 | Confirm Home Assistant can see the AC/thermostat entity and any sensors, switches or notification targets. |
| Connect WinForge · 連接 WinForge | Save the Home Assistant base URL and a long-lived access token in this module. The token is stored DPAPI-encrypted. |
| Operate · 操作 | Use **Lights & Climate** to select a `climate.*` entity, set target temperature and set HVAC mode. Use **Notify** to load and send to Home Assistant notification targets. |

**EN —** The relevant REST calls are `climate/set_temperature`, `climate/set_hvac_mode` and `notify/<target>`. Use the Docker module or Docker over SSH module only when you need to inspect or manage the container host; the Home Assistant module only needs the HA REST endpoint.

**粵語 —** 相關 REST 呼叫係 `climate/set_temperature`、`climate/set_hvac_mode` 同 `notify/<target>`。只有需要檢查或管理容器主機時，先用 Docker 模組或 Docker over SSH 模組；Home Assistant 模組只需要 HA REST endpoint。

## Buttons And Controls Â· æŒ‰éˆ•èˆ‡æŽ§åˆ¶é …

| Button Â· æŒ‰éˆ• | Type Â· é¡žåž‹ | XAML name Â· åç¨± | Handler Â· è™•ç†å‡½å¼ |
|---|---|---|---|
| [SaveCfgBtn](../../buttons/apps-git-git/homeassistant/001-savecfgbtn.md) | `Button` | `SaveCfgBtn` | `SaveCfg_Click` |
| [TestBtn](../../buttons/apps-git-git/homeassistant/002-testbtn.md) | `Button` | `TestBtn` | `Test_Click` |
| [TplRunBtn](../../buttons/apps-git-git/homeassistant/003-tplrunbtn.md) | `Button` | `TplRunBtn` | `TplRun_Click` |
| [CheckCfgBtn](../../buttons/apps-git-git/homeassistant/004-checkcfgbtn.md) | `Button` | `CheckCfgBtn` | `CheckCfg_Click` |
| [RestartBtn](../../buttons/apps-git-git/homeassistant/005-restartbtn.md) | `Button` | `RestartBtn` | `Restart_Click` |
| [ReloadDomainBtn](../../buttons/apps-git-git/homeassistant/006-reloaddomainbtn.md) | `Button` | `ReloadDomainBtn` | `ReloadDomain_Click` |
| [ReloadEntryBtn](../../buttons/apps-git-git/homeassistant/007-reloadentrybtn.md) | `Button` | `ReloadEntryBtn` | `ReloadEntry_Click` |
| [LoadEntitiesBtn](../../buttons/apps-git-git/homeassistant/008-loadentitiesbtn.md) | `Button` | `LoadEntitiesBtn` | `LoadEntities_Click` |
| [HistBtn](../../buttons/apps-git-git/homeassistant/009-histbtn.md) | `Button` | `HistBtn` | `Hist_Click` |
| [SetStateBtn](../../buttons/apps-git-git/homeassistant/010-setstatebtn.md) | `Button` | `SetStateBtn` | `SetState_Click` |
| [SceneBtn](../../buttons/apps-git-git/homeassistant/011-scenebtn.md) | `Button` | `SceneBtn` | `Scene_Click` |
| [ReloadScenesBtn](../../buttons/apps-git-git/homeassistant/012-reloadscenesbtn.md) | `Button` | `ReloadScenesBtn` | `ReloadScenes_Click` |
| [ScriptBtn](../../buttons/apps-git-git/homeassistant/013-scriptbtn.md) | `Button` | `ScriptBtn` | `Script_Click` |
| [EventBtn](../../buttons/apps-git-git/homeassistant/014-eventbtn.md) | `Button` | `EventBtn` | `Event_Click` |
| [IntentBtn](../../buttons/apps-git-git/homeassistant/015-intentbtn.md) | `Button` | `IntentBtn` | `Intent_Click` |
| [RefreshTogglesBtn](../../buttons/apps-git-git/homeassistant/016-refreshtogglesbtn.md) | `Button` | `RefreshTogglesBtn` | `RefreshToggles_Click` |
| [AllLightsOnBtn](../../buttons/apps-git-git/homeassistant/017-alllightsonbtn.md) | `Button` | `AllLightsOnBtn` | `AllLightsOn_Click` |
| [AllLightsOffBtn](../../buttons/apps-git-git/homeassistant/018-alllightsoffbtn.md) | `Button` | `AllLightsOffBtn` | `AllLightsOff_Click` |
| [param($m) "[icon U+$($m.Groups[1].Value.ToUpperInvariant())]"](../../buttons/apps-git-git/homeassistant/019-rowon-click.md) | `Button` | `` | `RowOn_Click` |
| [param($m) "[icon U+$($m.Groups[1].Value.ToUpperInvariant())]"](../../buttons/apps-git-git/homeassistant/020-rowoff-click.md) | `Button` | `` | `RowOff_Click` |
| [binding:ApplyBrightnessLabel](../../buttons/apps-git-git/homeassistant/021-rowapplybrightness-click.md) | `Button` | `` | `RowApplyBrightness_Click` |
| [param($m) "[icon U+$($m.Groups[1].Value.ToUpperInvariant())]"](../../buttons/apps-git-git/homeassistant/022-rowon-click.md) | `Button` | `` | `RowOn_Click` |
| [param($m) "[icon U+$($m.Groups[1].Value.ToUpperInvariant())]"](../../buttons/apps-git-git/homeassistant/023-rowoff-click.md) | `Button` | `` | `RowOff_Click` |
| [LightOnBtn](../../buttons/apps-git-git/homeassistant/024-lightonbtn.md) | `Button` | `LightOnBtn` | `LightOn_Click` |
| [LightOffBtn](../../buttons/apps-git-git/homeassistant/025-lightoffbtn.md) | `Button` | `LightOffBtn` | `LightOff_Click` |
| [SetTempBtn](../../buttons/apps-git-git/homeassistant/026-settempbtn.md) | `Button` | `SetTempBtn` | `SetTemp_Click` |
| [SetHvacBtn](../../buttons/apps-git-git/homeassistant/027-sethvacbtn.md) | `Button` | `SetHvacBtn` | `SetHvac_Click` |
| [LoadTargetsBtn](../../buttons/apps-git-git/homeassistant/028-loadtargetsbtn.md) | `Button` | `LoadTargetsBtn` | `LoadTargets_Click` |
| [NotifyBtn](../../buttons/apps-git-git/homeassistant/029-notifybtn.md) | `Button` | `NotifyBtn` | `Notify_Click` |
| [SnapBtn](../../buttons/apps-git-git/homeassistant/030-snapbtn.md) | `Button` | `SnapBtn` | `Snap_Click` |
| [SaveSnapBtn](../../buttons/apps-git-git/homeassistant/031-savesnapbtn.md) | `Button` | `SaveSnapBtn` | `SaveSnap_Click` |
| [LoadCalsBtn](../../buttons/apps-git-git/homeassistant/032-loadcalsbtn.md) | `Button` | `LoadCalsBtn` | `LoadCals_Click` |
| [TodayBtn](../../buttons/apps-git-git/homeassistant/033-todaybtn.md) | `Button` | `TodayBtn` | `Today_Click` |
| [TailBtn](../../buttons/apps-git-git/homeassistant/034-tailbtn.md) | `Button` | `TailBtn` | `Tail_Click` |
| [CopyLogBtn](../../buttons/apps-git-git/homeassistant/035-copylogbtn.md) | `Button` | `CopyLogBtn` | `CopyLog_Click` |
