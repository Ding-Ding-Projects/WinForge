# System maintenance workflows · 系統維護流程

These guided workflows live in **System Doctors** (`WinForge.exe --page doctors`). They complete the audited Windows/System and Maintenance roadmap gaps without redirecting the user to an external settings console.

呢批引導式流程喺 **系統醫生**（`WinForge.exe --page doctors`）；唔會將用戶踢去外部設定介面，並補齊 Windows／System 同 Maintenance 審核缺口。

- [Storage Sense policy · 儲存空間感知政策](storage-sense-policy.md)
- [Filter Keys & Slow Keys · 篩選鍵同慢速鍵](filter-keys.md)
- [Default app association templates · 預設程式關聯範本](default-app-associations.md)
- [Windows Update pause/resume · Windows Update 暫停／恢復](windows-update-pause.md)
- [Driver backup and rollback · 驅動備份同回復](driver-backup-rollback.md)
- [Startup impact and Autoruns audit · 開機影響同 Autoruns 審核](startup-autoruns-audit.md)
- [Component-store ResetBase · 元件存放區 ResetBase](component-store-resetbase.md)
- [Store-app reset and re-registration · 商店 app 重設同重新註冊](store-app-repair.md)

No HTTP API is introduced, so a Postman collection is not applicable. · 呢批功能冇新增 HTTP API，所以唔適用 Postman collection。

The process-free contract is `dotnet run --project tests/SystemMaintenanceCore.Tests -c Debug`. Live mutation is deliberately excluded from the harness. · 無副作用合約係 `dotnet run --project tests/SystemMaintenanceCore.Tests -c Debug`；測試刻意唔會做真實系統變更。
