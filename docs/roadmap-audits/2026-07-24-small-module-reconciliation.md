# Small-module roadmap reconciliation — 2026-07-24 · 細型模組路線圖對帳

## Outcome · 結果

Six stale checklist rows are now marked shipped because their complete named outcomes are executable and reachable. Color Picker remains open: system-wide click sampling and HEX/RGB/HSL copy are real, but the roadmap's HSV output and magnifying loupe are absent. No product code, layout, or screenshot changed in this audit. · 六個過時 checklist 項目而家按可執行、可達嘅完整指定結果標成已交付。Color Picker 繼續未剔：全螢幕點擊取色同 HEX／RGB／HSL 複製係真實功能，但路線圖要求嘅 HSV 輸出同放大鏡仍然冇。今次審核冇改產品 code、版面或截圖。

| Roadmap outcome | Disposition | Executable evidence |
|---|---|---|
| Hosts editor with block/redirect | Shipped · 已交付 | `HostsEditorModule`/`HostsService` load, edit, timestamp-backup, append `0.0.0.0`, save, and flush DNS; the richer `HostsEditModule` also toggles entries and writes rebuilt content. |
| Cloudflare/Google/automatic DNS | Shipped · 已交付 | `NetworkTweaks` runs `Set-DnsClientServerAddress` with Cloudflare, Google, or `-ResetServerAddresses`; `NetProTweaks` additionally applies providers to active physical adapters and clears the DNS client cache. |
| WSL distro manager | Shipped · 已交付 | `WslVmModule` reaches `WslVmService` list-online/list-installed, install, export, import, set-default, terminate/unregister, launch, embedded terminal, and shutdown paths. |
| Generated Windows Sandbox config | Shipped · 已交付 | `WslVmService` emits bounded `.wsb` XML with networking and read-only mapped-folder controls, launches `WindowsSandbox.exe`, and offers the guarded DISM feature enablement path. |
| Global hotkey → macro | Shipped · 已交付 | `HotkeyMacroService` persists JSON bindings, registers `WM_HOTKEY`, and executes the selected app/module, PowerShell, or Unicode `SendInput` action; the page also manages text-expander snippets. |
| World clock and timezone converter | Shipped · 已交付 | `WorldClockModule` enumerates OS `TimeZoneInfo` zones, maintains a one-second multi-zone board, adds/removes rows, and converts a chosen date/time across every listed zone. |
| System-wide Color Picker | Partial; remains open · 部分完成，保持未剔 | `ColorPickService` uses global mouse hooking plus GDI `GetPixel`; the page exposes HEX/RGB/HSL and explicit clipboard actions. There is no HSV formatter or `BitBlt`/magnifying loupe yet. |

## Route and static verification · Route 同靜態驗證

- Repository driver launch-only checks passed for `--page hosts`, `wsl`, `colorpicker`, `hotkeys`, and `worldclock`; every owned process was closed by the driver. · Repo driver 對五個 deep link 嘅 launch-only 檢查全部通過，而且每個自家 process 都由 driver 關閉。
- The source-surface audit passed: **337** XAML files, **2,893/2,893** declared handlers resolved, **1,937/1,937** direct action handlers resolved, zero language-subscription mismatches, and zero actionable implementation markers. · Source-surface audit 全過：337 個 XAML、2,893/2,893 handler、1,937/1,937 direct action 全部 resolve，零 language subscription mismatch，零 actionable marker。
- XAML literal safety passed with 17 protected ToggleSwitch defaults, two protected CheckBox defaults, and ten protected NumberBox defaults. · XAML literal safety 全過，包括 17 個受保護 ToggleSwitch、兩個 CheckBox 同十個 NumberBox 預設值。
- Existing canonical screenshots remain applicable because no visual tree changed. · 因為 visual tree 冇改，現有正式截圖仍然適用。

## Safety boundaries · 安全界線

Hosts and DNS writes remain explicit administrative actions; WSL/Sandbox arguments are built by the owned service and Sandbox mapped folders can be read-only. Hotkey macros are intentionally powerful, local, user-authored actions and are not a sandbox. Color sampling reads only the pixel the user explicitly clicks, while time conversion is offline OS data. · Hosts／DNS 寫入保持明確管理員操作；WSL／Sandbox argument 由自家 service 建立，Sandbox 對應資料夾可設唯讀。熱鍵巨集係刻意有能力嘅本機用戶自訂操作，唔係 sandbox。取色只讀用戶明確點擊嘅 pixel；時區換算只用離線 OS 資料。
