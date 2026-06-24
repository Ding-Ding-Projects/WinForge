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
# 2. Copy these pages in · 複製呢啲頁入去
cp docs/wiki/*.md WinForge.wiki/
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

- `Home.md` — module index landing page · 模組索引首頁
- `Screenshots.md` — image inventory + capture status · 圖檔清單同擷取狀態
- One page per major module · 每個主要模組一頁: Dashboard, Git-and-GitHub, Package-Manager,
  Cloudflare-and-Tunnel, AI-Agents, Media, Settings-and-Control-Panel, Clipboard, Connections,
  System-Monitor.
