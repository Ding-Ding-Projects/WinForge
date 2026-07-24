# Driver backup and rollback · 驅動備份同回復

## Behavior · 行為

The page lists real `%WINDIR%\INF\oem*.inf` published identities. It can export one package or all third-party packages with `pnputil /export-driver`, restore exported INFs recursively with `/add-driver ... /subdirs /install`, and roll back a selected package with `/delete-driver <oem#.inf> /uninstall`.

頁面會列出真實 `%WINDIR%\INF\oem*.inf` 身份；可以用 `pnputil` 匯出一個／全部第三方套件、遞迴還原已匯出 INF，或者解除安裝所選套件畀相容已暫存驅動接手。

## Safety gate · 安全閘

Rollback remains disabled until that exact package exports successfully in the current session. The command never uses `/force` or schedules `/reboot`; a decision dialog names the package and retained backup folder. Elevation is required, and identities must match `oem<number>.inf`.

同一套件未喺今次工作階段成功匯出之前，回復掣會保持停用。指令永遠唔用 `/force` 或安排 `/reboot`；決定對話框會列明套件同保留備份資料夾。需要管理員，身份只接受 `oem<number>.inf`。

## Verification · 驗證

Focused tests cover identity injection rejection, spaced-folder argument boundaries, export/restore switches, and the no-force/no-reboot contract. Live driver removal is unsafe for smoke verification and is not executed.
