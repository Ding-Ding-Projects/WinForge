+# Guided Windows maintenance · 引導式 Windows 維護

Open in-app: `WinForge.exe --page doctors`

System Doctors now contains the complete audited Windows/System and Maintenance workflows. Each control stays inside WinForge, keeps English/Cantonese/bilingual localization, validates untrusted values before a Windows boundary, and separates read-only diagnosis from privileged or destructive action.

「系統醫生」而家有齊已審核 Windows／System 同 Maintenance 流程。全部控制留喺 WinForge，支援英文／粵語／雙語；未信任值會喺進入 Windows 邊界前驗證，亦會清楚分開只讀診斷、提升權限同破壞性操作。

## Windows 11 controls · Windows 11 控制

- **Storage Sense policy · 儲存空間感知政策** — enablement, low-space/daily/weekly/monthly cadence, Recycle Bin retention, and Downloads retention.
- **Filter Keys & Slow Keys · 篩選鍵同慢速鍵** — all four timings plus live `SPI_SETFILTERKEYS` application; values are bounded to 20 seconds.
- **Default app association templates · 預設程式關聯範本** — machine-wide DISM XML export/import for new profiles, with explicit scope and no protected `UserChoice` bypass.

## Maintenance controls · 維護控制

- **Windows Update pause/resume · Windows Update 暫停／恢復** — bounded 7–35 day feature/quality pause and truthful removal of every pause value.
- **Driver backup and rollback · 驅動備份同回復** — list published OEM INFs, export one/all, restore exports, and only enable conservative rollback after the exact package is backed up in the current session. No `/force`, no scheduled reboot.
- **Startup impact / Autoruns audit · 開機影響／Autoruns 審核** — read-only Run, RunOnce, Startup folders, Winlogon, AppInit, automatic-service, and boot/logon-task inventory with documented source-risk impact rather than invented timing.
- **Component-store ResetBase · 元件存放區 ResetBase** — persistent irreversible warning, acknowledgement, and final decision before DISM. This removes the ability to uninstall installed Windows updates.
- **Store-app repair · 商店 app 修復** — load and select a non-framework app, then reset its data behind a destructive decision or validate/re-register its own manifest.

## Safety and failure behavior · 安全同失敗行為

User-selected paths are passed as argument vectors, not shell fragments. Driver identities must match `oem<number>.inf`; Store identities use a restricted PackageManager-compatible character set. Privileged actions fail closed unless WinForge is already elevated. ResetBase, driver rollback, association import, and Store-data reset have explicit decision gates. Visual verification never executes those mutations.

用戶揀嘅路徑會用獨立參數向量，唔會拼入 shell。驅動身份一定要係 `oem<number>.inf`；商店身份只接受 PackageManager 相容安全字元。未提升權限會安全拒絕。ResetBase、驅動回復、關聯匯入同商店資料重設全部有明確決定閘；視覺驗證唔會執行呢啲變更。

## Verification · 驗證

```powershell
dotnet run --project tests\SystemMaintenanceCore.Tests -c Debug
powershell -ExecutionPolicy Bypass -File tools\Test-RoadmapCoreAudit.ps1
dotnet build WinForge.sln -c Debug -p:Platform=x64
```

The pure harness covers 22 validation/command cases without touching registry, DISM, drivers, updates, or app data. The strict roadmap matrix is **82/115 shipped**, including **13/13 Windows 11** and **15/15 Maintenance**.

無副作用 harness 有 22 個驗證／指令 case，唔會郁 registry、DISM、驅動、更新或 app 資料。嚴格路線圖 matrix 係 **82/115 已交付**，包括 **Windows 11 13/13** 同 **Maintenance 15/15**。

## Visual evidence · 視覺證據

Fresh capture details and hashes are recorded in the current handoff after self-contained publish and dedicated LowLevel headless inspection. Destructive controls remain uninvoked. · 最新擷取詳情同 hash 會喺自包含 publish 同專用 LowLevel headless 檢視後記錄喺目前交接；破壞性控制保持未執行。

![System Doctors](https://raw.githubusercontent.com/Ding-Ding-Projects/WinForge/main/docs/screenshot-doctors.png)

[← System & Tweaks](#/wiki/System-and-Tweaks) · [Feature records](https://github.com/Ding-Ding-Projects/WinForge/tree/main/docs/features/system-maintenance)

