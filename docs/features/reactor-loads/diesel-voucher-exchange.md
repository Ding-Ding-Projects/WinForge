# Diesel voucher exchange — cookie-bought EDG fuel · 柴油券交易 — 用曲奇買嘅應急柴油

## Purpose · 用途

**EN —** The design-basis "10-second" emergency diesel generators in `Services/ReactorElectrical.cs` now burn fuel from a shared tank that **starts empty and has no on-site refill**. Every litre is bought with cookies: **Material Cookie Clicker** mints append-only vouchers into a shared per-user ledger, and WinForge — the exchange's only consumer — redeems them into the tank. Without fuel, a loss of offsite power (LOOP) cannot be recovered by the diesels and becomes a station blackout (SBO).

**粵語 —** `Services/ReactorElectrical.cs` 入面設計基準嘅「10 秒」應急柴油發電機而家由一個**開頭係空、亦冇廠內補給**嘅共用油缸抽油。每一公升都係用曲奇買返嚟：**Material Cookie Clicker** 將只可追加嘅柴油券鑄造入共用帳本，而 WinForge（交易嘅唯一消費端）負責兌換入缸。冇油嘅話，失去廠外電源（LOOP）冇柴油機救，就會變成全廠斷電（SBO）。

## The ledger contract · 帳本合約

The full agreement lives in the Material Cookie Clicker repo (`docs/winforge-diesel-exchange.md`). The parts WinForge honours:

| Rule · 規則 | Behaviour · 行為 |
|---|---|
| Location · 位置 | `%APPDATA%\DingDingProjects\exchange\diesel-vouchers.json`, per-user. A missing file is an empty ledger, never an error. · 每用戶一份；檔案唔存在等於空帳本，唔係錯誤。 |
| Only writer of `consumedAt` · 唯一寫入者 | `Services/DieselVoucherService.cs` stamps `consumedAt` on redeemed vouchers. It never deletes, reorders, renumbers, or edits vouchers, and never creates the ledger. · 只會蓋章 `consumedAt`；永遠唔會刪除、重排、改號或者改寫券，亦唔會創建帳本。 |
| Parse failure · 解析失敗 | The file is preserved byte-for-byte and nothing is consumed; the UI reports the problem. · 檔案原封不動保留，唔會消耗任何券；UI 會如實報告。 |
| Newer `schemaVersion` · 較新版本 | Refused rather than guessed, exactly like a save from a newer build. · 拒絕解讀而唔會亂估。 |
| Atomic writes · 原子寫入 | Whole-file UTF-8, two-space-indented JSON with a trailing newline, written to a temp file in the same directory and renamed over the ledger (`MoveFileEx` `REPLACE_EXISTING`). · 全檔寫入臨時檔再單次 rename，斷電只會留低舊版或者新版，永遠唔會半份。 |

## Simulation behaviour · 模擬行為

- Tank and burn live in `ReactorElectrical`: `EdgFuelLitres` starts at **0**, each EDG burns **1.0 L/min (game-scaled) while cranking or loaded**, and an emptied tank stalls both diesels back to standby — the full 10-second start is required after refuelling. `Standby→Starting` is refused while the tank is dry. · 油缸同耗油喺 `ReactorElectrical`：`EdgFuelLitres` 由 **0** 開始，每部 EDG **啟動中或帶載時每分鐘耗油 1.0 L**（遊戲比例）；燒乾會令兩部柴油機熄火返 standby，入油後要重新等足 10 秒。缸乾時唔准 `Standby→Starting`。
- `ReactorSimService.UpdateElectrical` bridges the exchange: it seeds the persisted tank level once, injects freshly redeemed litres, and reports the burned-down level back for persistence. · `ReactorSimService.UpdateElectrical` 負責橋接：開機一次過載入已保存油量、注入新兌換嘅公升數、再將燒剩嘅油量回報保存。
- Purchased fuel is inventory, not scenario state: it **survives `Reset()` and app restarts** (persisted via `SettingsStore` under `reactor.edg.cookieFuelTankLitres` plus lifetime provenance counters). · 買返嚟嘅油係庫存，唔係情景狀態：**`Reset()` 同重啟 app 都唔會冇咗**（經 `SettingsStore` 保存，另有歷來兌換統計）。
- The SBO scenario still injects faults on both EDGs, so its definition is unchanged; fuel only matters on the recoverable LOOP path. · SBO 情景仍然對兩部 EDG 注入故障，定義不變；油量只影響可救返嘅 LOOP 路徑。

## UI · 介面

- **Reactor Settings** (`--page reactorsettings`) gains a bilingual **"Diesel voucher exchange — fuel bought with cookies · 柴油券交易 — 用曲奇買嘅燃油"** card: tank level, pending/consumed voucher counts from the ledger, a **Redeem diesel vouchers · 兌換柴油券** button, lifetime provenance ("all bought with cookies"), and truthful error lines for missing/corrupt/newer-schema ledgers. · **反應堆設定**頁新增雙語卡：油缸存量、帳本待兌／已耗券數、**兌換柴油券**掣、歷來出處統計，同埋帳本缺失／損壞／較新版本嘅如實提示。
- The reactor control room adds an **"EDG diesel (cookie vouchers) · 柴油油量（曲奇券）"** gauge beside the vital DC battery. · 控制室喺 1E 直流電池旁新增**柴油油量（曲奇券）**錶。

## Verification · 驗證

`dotnet run --project tests/ReactorSim.Tests -c Debug` is now **73/73**. The six focused scenarios cover: missing-ledger emptiness with a no-op redeem that writes nothing; byte-for-byte preservation of an unparseable ledger; newer-schema refusal; a redeem pass that stamps only pending vouchers, keeps ids/order/receipt strings, ends with a trailing newline, and is idempotent; a dry tank turning LOOP into SBO until vouchers refuel and the diesels reload after the exact 10-second start; and per-EDG burn, stall-on-exhaustion back to SBO, and fuel surviving `Reset()`. · 專項 harness 而家 **73/73**：六個新情景覆蓋空帳本無操作、損壞帳本逐 byte 保留、拒絕較新版本、只蓋章待兌券兼保留次序／收據兼幂等、乾缸令 LOOP 變 SBO 直至兌券入油後準確 10 秒重載，同每部 EDG 耗油、燒乾熄火返 SBO、`Reset()` 保留油量。
