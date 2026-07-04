using System.Collections.Generic;

namespace WinForge.Services;

// 檔案與磁碟章節嘅教學條目 · How-to entries for the Files & disks section.
public static partial class ManualContent
{
    private static List<ManualEntry> FilesEntries() => new()
    {
        new ManualEntry
        {
            Tag = "module.peek", Glyph = "",
            TitleEn = "Peek", TitleZh = "快速預覽",
            SummaryEn = "Quickly preview almost any file — images, text, code, PDFs, media and archives — without opening a heavy app.",
            SummaryZh = "唔使開個大應用程式，就快速預覽差唔多任何檔案，圖片、文字、程式碼、PDF、影片同壓縮檔都得。",
            StepsEn = new[]
            {
                "Click Pick to choose a file, or Pick folder to browse a whole folder; you can also drag a file onto the page.",
                "Read the preview in the centre surface — images zoom, text/code scroll, PDFs and Markdown render, media plays.",
                "Use the Prev / Next arrows to flip through the other files in the same folder.",
                "From the action bar use Open, Open with, Open folder or Copy path as needed.",
                "Click the hotkey button to set a global shortcut so you can Peek the selected file from anywhere.",
            },
            StepsZh = new[]
            {
                "撳 Pick 揀一個檔案，或者撳 Pick folder 睇成個資料夾；你都可以直接拖個檔案入嚟。",
                "喺中間嗰格睇預覽，圖片可以放大、文字同程式碼可以捲動、PDF 同 Markdown 會渲染出嚟、影音可以播放。",
                "撳 Prev／Next 箭咀就可以喺同一個資料夾揭其他檔案。",
                "喺下面嘅動作列，按需要撳 Open、Open with、Open folder 或者 Copy path。",
                "撳熱鍵掣設定一個全域快捷鍵，咁就可以喺任何地方對住揀咗嘅檔案開 Peek。",
            },
            TipEn = "Peek is read-only — it never modifies the file you are previewing.",
            TipZh = "Peek 淨係睇，唔會改動你預覽緊嘅檔案。",
            Keywords = "peek preview quicklook 預覽 快速預覽 熱鍵 拖放 上一個 下一個",
        },
        new ManualEntry
        {
            Tag = "module.newplus", Glyph = "",
            TitleEn = "New+", TitleZh = "範本新增",
            SummaryEn = "Create files and folders from your own templates, and add them to the Explorer right-click New menu.",
            SummaryZh = "用你自己嘅範本嚟新增檔案同資料夾，仲可以加入檔案總管右鍵嘅「新增」選單。",
            StepsEn = new[]
            {
                "Use Add (or New blank) to put a template into the templates folder; Open folder lets you drop files in by hand.",
                "Pick a template from the list on the left.",
                "On the right, set the destination folder and a name, then optionally tick the variables checkbox to expand date/path tokens.",
                "Click Create to scaffold the file or folder from that template.",
                "Click Register to add New+ templates to the Explorer right-click New menu.",
            },
            StepsZh = new[]
            {
                "撳 Add（或者 New blank）擺一個範本入範本資料夾；撳 Open folder 你就可以自己拖檔案入去。",
                "喺左邊個清單揀一個範本。",
                "喺右邊填好目的地資料夾同名，需要嘅話剔變數選項去展開日期／路徑變數。",
                "撳 Create，就會用嗰個範本整出檔案或者資料夾。",
                "撳 Register，就會將 New+ 範本加入檔案總管右鍵嘅「新增」選單。",
            },
            TipEn = "Click the variables help button to see which tokens (date, time, etc.) you can use in template names.",
            TipZh = "撳變數說明掣，就睇到範本名入面可以用邊啲變數（日期、時間等等）。",
            Keywords = "new plus newplus powertoys template 範本 新增 變數 右鍵 新增選單",
        },
        new ManualEntry
        {
            Tag = "module.archives", Glyph = "",
            TitleEn = "Archives", TitleZh = "壓縮檔",
            SummaryEn = "Create and extract 7z, ZIP, TAR and other archives, with passwords, encryption and split volumes.",
            SummaryZh = "整同解壓 7z、ZIP、TAR 等壓縮檔，仲支援密碼、加密同分卷。",
            StepsEn = new[]
            {
                "Pick or name an archive in the top card, then choose a source file or folder.",
                "Choose the format (7z, zip, tar, gzip, bzip2, xz) and compression level, and type a password if you want one.",
                "Optionally set a split volume size, or tick SFX, header encryption, solid or multithread options.",
                "Click Create to build the archive; watch the output pane for progress.",
                "Use the advanced operations list (filter with the search box) for extra extract/test tasks.",
            },
            StepsZh = new[]
            {
                "喺上面嗰格揀或者改個壓縮檔名，跟住揀來源檔案或者資料夾。",
                "揀格式（7z、zip、tar、gzip、bzip2、xz）同壓縮等級，想加密就打個密碼。",
                "有需要可以設定分卷大細，或者剔 SFX、檔頭加密、solid、多執行緒等選項。",
                "撳 Create 開始整壓縮檔，喺輸出格睇進度。",
                "下面嘅進階操作清單（用搜尋框篩選）有額外嘅解壓／測試功能。",
            },
            TipEn = "Header encryption hides the file names too, not just the contents — keep it ticked for sensitive archives.",
            TipZh = "檔頭加密會連檔名都收埋，唔淨係收內容，重要嘅壓縮檔最好剔住佢。",
            Keywords = "zip 7z tar gzip rar 壓縮 解壓 加密 密碼 分卷",
        },
        new ManualEntry
        {
            Tag = "module.bulkops", Glyph = "",
            TitleEn = "Bulk File Ops", TitleZh = "批次檔案操作",
            SummaryEn = "Match files by pattern in a folder, then copy, move, recycle, flatten or organise them in one go.",
            SummaryZh = "用樣式喺一個資料夾配對檔案，然後一次過複製、移動、丟入回收筒、攤平或者分類整理。",
            StepsEn = new[]
            {
                "Pick a source folder.",
                "Type a pattern, choose a match mode, and tick Recurse to include subfolders; the list shows what matches.",
                "Pick a target folder for copy/move operations.",
                "Click Copy, Move, Recycle, Flatten or Organize to run the action.",
                "Check the result bar and the count for what happened.",
            },
            StepsZh = new[]
            {
                "揀來源資料夾。",
                "打個樣式、揀配對模式，想連子資料夾就剔 Recurse；個清單會即時顯示邊啲檔案中。",
                "如果係複製／移動，就揀好目的地資料夾。",
                "撳 Copy、Move、Recycle、Flatten 或者 Organize 執行。",
                "睇結果列同數量就知做咗啲乜。",
            },
            TipEn = "Recycle sends files to the Recycle Bin so you can restore them, but Move and Flatten change paths directly — double-check the preview first.",
            TipZh = "Recycle 係丟入回收筒、仲救得返，但 Move 同 Flatten 會直接改路徑，做之前最好睇清楚個預覽。",
            Keywords = "bulk file move copy delete flatten organize 批次 檔案 移動 複製 回收筒",
        },
        new ManualEntry
        {
            Tag = "module.rename", Glyph = "",
            TitleEn = "Batch Rename", TitleZh = "批次改名",
            SummaryEn = "Rename many files at once with find-and-replace, including regex and an optional extension match.",
            SummaryZh = "用尋找取代一次過改好多檔案名，仲支援 regex 同可選嘅副檔名配對。",
            StepsEn = new[]
            {
                "Click Browse to pick the folder of files to rename.",
                "Type the Find and Replace text.",
                "Tick Regex, Case-sensitive or Include extension as needed; the list previews old to new names.",
                "Confirm the preview looks right (changed names are highlighted).",
                "Click Apply to rename the files.",
            },
            StepsZh = new[]
            {
                "撳 Browse 揀要改名嘅檔案資料夾。",
                "打 Find（搵咩）同 Replace（換成咩）。",
                "按需要剔 Regex、區分大小寫或者連副檔名；個清單會預覽舊名到新名。",
                "睇清楚個預覽啱唔啱（有改動嘅名會標示出嚟）。",
                "撳 Apply 就會改名。",
            },
            TipEn = "Always check the preview before clicking Apply — a renamed file can be hard to undo.",
            TipZh = "撳 Apply 之前一定要睇清楚個預覽，改咗名好難還原返。",
            Keywords = "rename bulk powerrename regex 改名 批次 尋找 取代",
        },
        new ManualEntry
        {
            Tag = "module.duplicates", Glyph = "",
            TitleEn = "Duplicate Finder", TitleZh = "重複檔案搜尋",
            SummaryEn = "Scan a folder for byte-identical duplicate files and recycle the copies you don't need.",
            SummaryZh = "掃描一個資料夾，搵出內容一模一樣嘅重複檔案，再將唔要嘅副本丟入回收筒。",
            StepsEn = new[]
            {
                "Click Browse to pick a folder, and tick Recurse to include subfolders.",
                "Click Scan to find duplicates; results are listed in groups.",
                "Tick the copies you want to remove (keep at least one per group).",
                "Click Recycle to send the ticked files to the Recycle Bin.",
            },
            StepsZh = new[]
            {
                "撳 Browse 揀個資料夾，想連子資料夾就剔 Recurse。",
                "撳 Scan 搵重複檔，結果會按組顯示。",
                "剔返你想刪嘅副本（每組至少留一個）。",
                "撳 Recycle 將剔咗嘅檔案丟入回收筒。",
            },
            TipEn = "Files go to the Recycle Bin, so you can restore them if you tick one by mistake.",
            TipZh = "檔案係丟入回收筒，剔錯都仲救得返。",
            Keywords = "duplicate hash dedupe 重複 重複檔 回收筒",
        },
        new ManualEntry
        {
            Tag = "module.filelocksmith", Glyph = "",
            TitleEn = "File Locksmith", TitleZh = "檔案鎖偵測",
            SummaryEn = "Find which process is locking a file or folder so you can unlock and delete or move it.",
            SummaryZh = "搵出邊個程序鎖住咗個檔案或者資料夾，等你可以解鎖再刪除或者移動。",
            StepsEn = new[]
            {
                "Type or paste a path, or use Pick file / Pick folder.",
                "Click Scan to list every process holding that file or folder open.",
                "Click the files icon to see exactly which files a process is locking, or open its location.",
                "Click End task to close a process and release its lock.",
                "Use Refresh to re-scan after closing things.",
            },
            StepsZh = new[]
            {
                "打或者貼一個路徑，或者撳 Pick file／Pick folder 揀。",
                "撳 Scan，就會列出所有正開住嗰個檔案或者資料夾嘅程序。",
                "撳檔案圖示睇下個程序具體鎖住邊啲檔案，或者開佢嘅位置。",
                "撳 End task 結束某個程序，釋放佢嘅鎖。",
                "關咗嘢之後撳 Refresh 重新掃描。",
            },
            TipEn = "Some system processes only show or can only be ended when running as admin — click the elevation button if prompted.",
            TipZh = "有啲系統程序要用管理員身份先睇到或者結束到，見到提示就撳一下提權掣。",
            Keywords = "file locksmith locked handle 檔案鎖 鎖住 佔用 結束工作 解鎖 邊個程序",
        },
        new ManualEntry
        {
            Tag = "module.disk", Glyph = "",
            TitleEn = "Disk Analyser", TitleZh = "磁碟分析",
            SummaryEn = "Scan a folder or drive to see what's eating your space, then drill into the biggest folders.",
            SummaryZh = "掃描一個資料夾或者磁碟，睇下啲空間畀咩食晒，再逐層揾最大嗰啲資料夾。",
            StepsEn = new[]
            {
                "Click Browse to pick a folder (or use the path bar), choose a scan mode, then click Scan.",
                "Read the list — each item shows its size, a bar and a percent of the parent.",
                "Click a folder to drill in, or use the Up button to go back a level.",
                "Use the terminal button to open a terminal at the current folder if you want.",
                "Select an item and click Recycle to send a space hog to the Recycle Bin.",
            },
            StepsZh = new[]
            {
                "撳 Browse 揀個資料夾（或者用路徑列），揀好掃描模式，跟住撳 Scan。",
                "睇個清單，每項都顯示大細、一條長條同佔上層幾多百分比。",
                "撳入一個資料夾逐層睇，或者撳 Up 掣返上一層。",
                "想嘅話可以撳終端機掣，喺而家嗰個資料夾開個終端機。",
                "揀一項再撳 Recycle，就將佔位嘅嘢丟入回收筒。",
            },
            Keywords = "disk space treemap analyse folder size 磁碟 空間 分析 回收筒",
        },
        new ManualEntry
        {
            Tag = "module.drives", Glyph = "",
            TitleEn = "Drives", TitleZh = "磁碟機",
            SummaryEn = "See all your drives and volumes with usage bars, and mount, dismount or create volumes.",
            SummaryZh = "睇晒所有磁碟機同磁碟區嘅使用情況，仲可以掛載、卸載或者新增磁碟區。",
            StepsEn = new[]
            {
                "Click Refresh to list every drive with a used/free usage bar.",
                "Select a drive, then click Mount or Dismount to attach or detach it.",
                "Click Create to make a new volume.",
                "Read the result bar for the outcome.",
            },
            StepsZh = new[]
            {
                "撳 Refresh 列出所有磁碟機，每個都有一條已用／剩餘嘅使用長條。",
                "揀一個磁碟機，再撳 Mount 或者 Dismount 掛載或者卸載佢。",
                "撳 Create 整一個新磁碟區。",
                "睇結果列就知結果。",
            },
            TipEn = "Creating, mounting or dismounting volumes can need admin rights — accept the prompt if it appears.",
            TipZh = "新增、掛載或者卸載磁碟區可能要管理員權限，彈提示就應允佢。",
            Keywords = "drive volume mount format bitlocker 磁碟機 掛載 卸載 磁碟區",
        },
        new ManualEntry
        {
            Tag = "module.testdisk", Glyph = "",
            TitleEn = "TestDisk / PhotoRec Recovery", TitleZh = "TestDisk / PhotoRec 資料救援",
            SummaryEn = "Recover lost or deleted files by carving them with PhotoRec, or scan partitions with TestDisk.",
            SummaryZh = "用 PhotoRec 雕刻救回遺失或者刪咗嘅檔案，或者用 TestDisk 掃描分割區。",
            StepsEn = new[]
            {
                "If prompted, click the download button to fetch the TestDisk / PhotoRec tools.",
                "Choose a source disk or image file, and pick an output folder on a DIFFERENT disk.",
                "Tick the file types you want to recover (or use Select all / Select none).",
                "Click Carve to run PhotoRec, or Scan to run a read-only TestDisk partition scan; watch the live log.",
                "When it finishes, click Open folder to see the recovered files.",
            },
            StepsZh = new[]
            {
                "如果有提示，撳下載掣去攞 TestDisk／PhotoRec 工具。",
                "揀來源磁碟或者映像檔，再揀一個喺另一個磁碟嘅輸出資料夾。",
                "剔返你想救嘅檔案類型（或者用 Select all／Select none）。",
                "撳 Carve 行 PhotoRec，或者撳 Scan 行 TestDisk 唯讀分割區掃描；睇住個即時記錄。",
                "做完撳 Open folder 睇救返嘅檔案。",
            },
            TipEn = "Always recover to a DIFFERENT disk than the one you're scanning, and expect to need admin rights for raw disk access — the app blocks same-disk recovery to avoid overwriting your data.",
            TipZh = "一定要救去同掃描嗰個唔同嘅磁碟，而且讀原始磁碟通常要管理員權限，個程式會擋住同盤救援，免得冚住你啲資料。",
            Keywords = "testdisk photorec recovery carve undelete partition 資料救援 救援 復原 救回 刪除",
        },
        new ManualEntry
        {
            Tag = "module.onedrive", Glyph = "",
            TitleEn = "OneDrive", TitleZh = "OneDrive",
            SummaryEn = "Manage OneDrive Files On-Demand — pin files to keep them local or dehydrate them to free up space.",
            SummaryZh = "管理 OneDrive 隨選檔案，釘選檔案留喺本機，或者脫水釋放空間。",
            StepsEn = new[]
            {
                "Click Pick to choose your OneDrive folder; use the path bar and Up to browse, Refresh to reload.",
                "Select one or more entries (each shows a state badge: online-only or local).",
                "Click Pin to keep files local, or Dehydrate to make them online-only and free up space.",
                "Use Pause / Resume to control syncing, and set a free-up threshold (days) then Apply.",
            },
            StepsZh = new[]
            {
                "撳 Pick 揀你嘅 OneDrive 資料夾；用路徑列同 Up 瀏覽，撳 Refresh 重新載入。",
                "揀一個或者多個項目（每個都有狀態標記：純線上定本機）。",
                "撳 Pin 將檔案留喺本機，或者撳 Dehydrate 變返純線上、釋放空間。",
                "用 Pause／Resume 控制同步，設定一個釋放空間嘅天數門檻再撳 Apply。",
            },
            TipEn = "Dehydrated files stay visible but download again when you open them, so don't dehydrate things you need offline.",
            TipZh = "脫咗水嘅檔案仲見到，但開嗰陣會重新下載，所以你要離線用嘅嘢就唔好脫水。",
            Keywords = "onedrive files on demand pin dehydrate online only 雲端 釘選 脫水 釋放空間 同步",
        },
        new ManualEntry
        {
            Tag = "module.richpreview", Glyph = "",
            TitleEn = "Rich Preview", TitleZh = "豐富預覽",
            SummaryEn = "Preview developer and design files — SVG, Markdown, PDF, source code, QOI and more — and enable Explorer preview handlers.",
            SummaryZh = "預覽開發同設計檔案，SVG、Markdown、PDF、原始碼、QOI 等等，仲可以開啟檔案總管嘅預覽處理器。",
            StepsEn = new[]
            {
                "Click Pick to choose a file, or drag one onto the preview area.",
                "View it rendered in the panel; PNG and other images zoom, code and Markdown render in the WebView.",
                "Use Prev / Next to move through files, and check the metadata sidebar for details.",
                "Click the settings gear to toggle which file types are handled.",
                "Use the system preview-pane buttons to enable handlers in Explorer's Preview pane and Folder Options.",
            },
            StepsZh = new[]
            {
                "撳 Pick 揀個檔案，或者拖一個入預覽區。",
                "喺面板睇渲染結果，PNG 同其他圖片可以放大，程式碼同 Markdown 會喺 WebView 渲染。",
                "用 Prev／Next 揭檔案，喺旁邊嘅中繼資料側欄睇詳情。",
                "撳設定齒輪去開關邊啲檔案類型有預覽。",
                "用系統預覽窗格嗰啲掣，去喺檔案總管嘅預覽窗格同資料夾選項開啟處理器。",
            },
            TipEn = "If a PNG or image looks blank, click it again or use Refresh — the preview decodes from the real file path and may need a moment.",
            TipZh = "如果 PNG 或者圖片睇落空白，再撳一次或者撳 Refresh，預覽係由真實檔案路徑解碼，可能要等一陣。",
            Keywords = "rich preview pane svg markdown pdf qoi source code 預覽 預覽窗格 渲染 原始碼 拖放",
        },
    };
}
