# Startup impact and Autoruns audit · 開機影響同 Autoruns 審核

## Behavior · 行為

The local read-only audit covers HKCU/HKLM Run and RunOnce (including 32-bit locations), both Startup folders, Winlogon, AppInit DLLs, automatic services, and boot/logon scheduled tasks. It renders source, command, and a transparent source-risk impact.

本機只讀審核涵蓋 HKCU／HKLM Run 同 RunOnce（包括 32-bit）、兩個開機資料夾、Winlogon、AppInit DLL、自動服務，同開機／登入排程工作；會顯示來源、指令同透明來源風險影響。

## Interpretation and privacy · 解讀同私隱

Impact is not invented timing telemetry: Winlogon/AppInit are critical, boot tasks/services are high, Run/RunOnce/logon tasks are medium, and Startup-folder entries are low. Commands remain local and are neither transmitted nor persisted by this workflow. At most 300 rows render at once to keep the page responsive.

影響唔係虛構時間數據：Winlogon／AppInit 係關鍵、開機工作／服務係高、Run／RunOnce／登入工作係中、開機資料夾係低。指令留喺本機，唔會傳送或由呢個流程保存；畫面最多一次顯示 300 行。

## Verification · 驗證

Focused tests lock every source-to-impact mapping. The live audit is read-only; inaccessible sources degrade to the remaining evidence instead of aborting the page.
