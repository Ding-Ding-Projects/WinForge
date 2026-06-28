# Wiki source · Wiki 原始檔

**EN —** This folder is the **canonical source** for the GitHub wiki at
<https://github.com/codingmachineedge/WinForge/wiki>. The wiki is a *separate* Git repo
(`WinForge.wiki.git`), so these pages are kept here in the main repo and published to the wiki.
Images are embedded with absolute `raw.githubusercontent.com/.../main/docs/...` URLs so they
resolve from the wiki repo. Keep this folder and the **Module gallery** in
[`../../README.md`](../../README.md) in sync.

**粵語 —** 呢個資料夾係 GitHub wiki（<https://github.com/codingmachineedge/WinForge/wiki>）嘅
**權威來源**。wiki 係一個*獨立*嘅 Git repo（`WinForge.wiki.git`），所以呢啲頁放喺主 repo
再發佈去 wiki。圖片用絕對 `raw.githubusercontent.com/.../main/docs/...` 連結嵌入，
咁喺 wiki repo 都顯示到。請保持呢個資料夾同 [`../../README.md`](../../README.md)
入面嘅「模組畫廊」同步。

## Publish to the GitHub wiki · 發佈到 GitHub wiki

```bash
# 1. Clone the (separate) wiki repo · 複製獨立嘅 wiki repo
git clone https://github.com/codingmachineedge/WinForge.wiki.git
# 2. Copy these pages and generated reference folders in · 複製頁面同生成參考資料夾
Remove-Item -Recurse -Force WinForge.wiki\*
Copy-Item -Recurse docs\wiki\* WinForge.wiki\
# 3. Commit & push · 提交同推送
cd WinForge.wiki
git add -A
git commit -m "Refresh module pages · 更新模組頁"
git push
```

> **Note · 注意:** GitHub wikis flatten directories — every page sits at the wiki root and is
> linked by its file name (e.g. `Cloudflare-and-Tunnel`). `Home.md` becomes the wiki landing page.
> GitHub wiki 唔分子目錄 — 每頁都喺 wiki 根目錄，用檔名連結（例如 `Cloudflare-and-Tunnel`）。
> `Home.md` 會變成 wiki 首頁。

## Pages · 頁

- `Home.md` — compact landing page · 精簡首頁
- `Module-Categories.md` — categorized index of all 139 modules · 全部 139 個模組嘅分類索引
- `Reactor-Hub.md` — reactor documentation hub · 反應堆文件中心
- `Generated-References.md` — generated feature/button reference guide · 生成功能／按鈕參考指南
- `Screenshots.md` — image inventory + capture status · 圖檔清單同擷取狀態
- `features/README.md` — generated one-page-per-feature reference · 生成嘅每功能一頁參考
- `buttons/README.md` — generated one-page-per-button/control reference · 生成嘅每按鈕／控制項一頁參考
- `Accessibility.md` — keyboard and screen-reader baseline · 鍵盤同螢幕閱讀器基本標準
- One deeper page per headline module · 每個重點模組一頁: Dashboard, Git-and-GitHub, Package-Manager, Cake-Factory-and-Farm, Open-Source-App-Hub,
  Cloudflare-and-Tunnel, AI-Agents, Media, Settings-and-Control-Panel, Clipboard, Connections,
  System-Monitor.

> **Adding a module page · 新增模組頁:** name the file after the module (e.g. `SSH-Toolset.md`), embed its
> `docs/screenshot-<key>.png` via an absolute `raw.githubusercontent.com/.../main/docs/...` URL, write a
> bilingual description, then add a link to it from `Home.md`. Keep the module list aligned with
> [`Services/ModuleRegistry.cs`](https://github.com/codingmachineedge/WinForge/blob/main/Services/ModuleRegistry.cs).
> 新增模組頁時，用模組名做檔名（例如 `SSH-Toolset.md`），用絕對 `raw.githubusercontent.com/.../main/docs/...`
> 連結嵌入 `docs/screenshot-<key>.png`，寫雙語說明，再喺 `Home.md` 加連結。模組清單請同
> [`Services/ModuleRegistry.cs`](https://github.com/codingmachineedge/WinForge/blob/main/Services/ModuleRegistry.cs) 對齊。
