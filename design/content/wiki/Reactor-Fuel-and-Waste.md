# Reactor Fuel & Waste · 反應堆燃料與廢料

**EN —** The reactor has an ultra-realistic fuel factory and a real nuclear-waste store. Fuel is fabricated as authentic, cryptographically-signed assemblies; loading one **consumes** the file (it is deleted, like real fuel burned in the core); forged fuel harms the core while inspection is harmless. Nuclear waste is written as genuine junk files on disk with strict caps and disk-safety floors.

**粵語 —** 反應堆有一個超寫實燃料工廠同一個真實核廢料庫。燃料以真品、加密簽署嘅組件形式製造；入料會**消耗**檔案（被刪除，就好似真燃料喺爐心燒掉）；偽冒燃料會損壞爐心，而檢查係無害嘅。核廢料以真實垃圾檔形式寫入磁碟，附嚴格上限同磁碟安全下限。

---

## Ultra-realistic fuel factory · 超寫實燃料工廠

**EN —** Fabricate fuel assemblies modeled on real PWR fuel: a **17×17 UO₂** lattice with a chosen **enrichment**, a **lot** number, and an **HMAC signature** that authenticates the assembly. The signature ties the assembly's parameters together so any tampering (e.g. an altered enrichment) can be detected.

**粵語 —** 製造以真實 PWR 燃料為模型嘅燃料組件：**17×17 UO₂** 格陣、自選**濃度**、**批次**號，加上認證組件嘅 **HMAC 簽章**。簽章將組件參數綁埋一齊，任何竄改（例如改咗濃度）都偵測得到。

---

## Send in fuel = validate + auto-delete · 送入燃料＝驗證＋自動刪檔

**EN —** When you **send in fuel**, the reactor first **validates** the HMAC signature. An authentic assembly is accepted, loaded into the core, and the source file is **auto-deleted** — it has been *consumed* by loading, exactly as real fuel is. A fresh, valid file disappears after loading and appears in the loaded list.

**粵語 —** 當你**送入燃料**，反應堆會先**驗證** HMAC 簽章。真品組件會被接受、載入爐心，而原始檔案會被**自動刪除**——載入即被*消耗*，正如真燃料一樣。一個全新、有效嘅檔案載入後會消失，並出現喺已載入清單。

**EN —** Fuel handling stays file-realistic: open the managed fuel folder in Windows Explorer, drag/drop fuel assembly files into place, then return to the reactor to inspect or load them. This avoids a magic easy-load path and makes the consumed-file behavior visible.

**粵語 —** 燃料處理維持檔案式寫實：喺 Windows Explorer 開啟受管理燃料資料夾，將燃料組件檔拖放入去，之後返反應堆檢查或載入。咁樣唔會變成魔法式 easy-load，亦可以清楚見到檔案被消耗。

---

## Forged fuel harms — inspect is safe · 偽冒燃料損堆——檢查安全

**EN —** **Validate / inspect** is always safe: checking a file's signature does no harm to the core, whether the fuel is authentic or forged. But **loading** a forged or tampered assembly **harms the core** — it raises core damage, triggers an automatic SCRAM, raises radiation, and consumes the file. The safe path is to inspect first; only load fuel that validates clean.

**粵語 —** **驗證／檢查**永遠安全：核對檔案簽章唔會損壞爐心，無論燃料係真定假。但**載入**偽冒或竄改嘅組件會**損壞爐心**——會增加爐心損傷、觸發自動 SCRAM、提升輻射，並消耗檔案。安全做法係先檢查；只載入驗證乾淨嘅燃料。

---

## Nuclear waste — real junk files · 核廢料——真實垃圾檔

**EN —** Operating the plant generates nuclear waste, written to disk as **genuine 100 MB–2000 MB junk files**. The store enforces guards so it can never harm your machine:

- **Cap · 上限** — a default cap (custom up to **50 GB**) limits total waste; writing past the cap is refused and the store reports **FULL**.
- **Spent-fuel-pool-full → runback → mandated shutdown · 乏燃料池滿 → 功率回降 → 強制停堆** — when the pool fills, power runs back and a shutdown is mandated.
- **Dispose · 處置** — disposing waste frees the space again.
- **Disk free-space floor · 磁碟安全下限** — a safety floor refuses any write that would drop free disk space below the floor, so the disk is never filled.

**粵語 —** 運行機組會產生核廢料，以**真實 100 MB–2000 MB 垃圾檔**形式寫入磁碟。廢料庫設有防護，永遠唔會損害你部機：

- **上限** — 預設上限（可自訂至 **50 GB**）限制總廢料量；超過上限嘅寫入會被拒絕，廢料庫報告 **FULL**。
- **乏燃料池滿 → 功率回降 → 強制停堆** — 當池滿，功率會回降並強制停堆。
- **處置** — 處置廢料會再次釋放空間。
- **磁碟安全下限** — 安全下限會拒絕任何令剩餘磁碟空間低過下限嘅寫入，所以磁碟永遠唔會被填滿。

---

**EN —** Fuel-cycle and waste behavior is verified headlessly — see the [Test Report](Reactor-Test-Report.md) (scenarios 9–13). · **粵語 —** 燃料循環同廢料行為已無介面驗證——見[測試報告](Reactor-Test-Report.md)（情景 9–13）。

---

### Reactor pages · 反應堆頁面導覽
[🏠 Reactor Hub · 反應堆總覽](Nuclear-Reactor.md) · [Overview · 總覽](Reactor-Overview.md) · [Control Room · 控制室](Reactor-Control-Room.md) · [Operating Procedures · 操作程序](Reactor-Operating-Procedures.md) · [Emergencies & Scenarios · 緊急與情景](Reactor-Emergencies-and-Scenarios.md) · [Fuel & Waste · 燃料與廢料](Reactor-Fuel-and-Waste.md) · [Water Treatment · 水處理](Reactor-Water-Treatment.md) · [Safety & Integrations · 安全與整合](Reactor-Safety-and-Integrations.md) · [Operating Manual · 操作手冊](Nuclear-Reactor-Operating-Manual.md) · [Test Report · 測試報告](Reactor-Test-Report.md)

*English + 繁體中文／粵語*
