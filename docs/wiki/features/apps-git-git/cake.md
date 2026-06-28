# Cake Factory & Farm Â· è›‹ç³•å·¥å» èˆ‡è¾²å ´

**EN â€”** Feature reference generated from the WinForge module registry, navigation map, and page XAML.
**ç²µèªž â€”** å‘¢ä»½åŠŸèƒ½åƒè€ƒç”± WinForge æ¨¡çµ„ç™»è¨˜ã€å°Žè¦½åœ°åœ–åŒé é¢ XAML ç”Ÿæˆã€‚

| Field Â· æ¬„ä½ | Value Â· å€¼ |
|---|---|
| Tag Â· æ¨™ç±¤ | $(System.Collections.Specialized.OrderedDictionary["Tag"]) |
| Deep-link alias Â· æ·±å±¤é€£çµåˆ¥å | $(System.Collections.Specialized.OrderedDictionary["Alias"]) |
| Category Â· åˆ†é¡ž | Apps & Git Â· ç¨‹å¼èˆ‡ Git |
| Page class Â· é é¢é¡žåˆ¥ | $(System.Collections.Specialized.OrderedDictionary["Class"]) |
| Page XAML Â· é é¢ XAML | $(System.Collections.Specialized.OrderedDictionary["PageFile"]) |
| Button docs Â· æŒ‰éˆ•æ–‡ä»¶ | 17 |

## What It Covers Â· åŠŸèƒ½ç¯„åœ

**EN â€”** Cake Factory & Farm is registered in WinForge search and navigation with these keywords: $(System.Collections.Specialized.OrderedDictionary["Keywords"]).

**ç²µèªž â€”** è›‹ç³•å·¥å» èˆ‡è¾²å ´ å·²ç™»è¨˜å–º WinForge æœå°‹åŒå°Žè¦½ï¼Œé—œéµå­—åŒ…æ‹¬ï¼š$(System.Collections.Specialized.OrderedDictionary["Keywords"])ã€‚

## Cake file handling · 蛋糕檔處理

**EN —** Cake transfer is file-based. Use **OpenCakeFolderButton** to open the signed-cake folder in Windows Explorer, then drag/drop `.cake` files in or out before validating, trusting or eating them in WinForge.

**粵語 —** 蛋糕轉移係以檔案為本。用 **OpenCakeFolderButton** 喺 Windows Explorer 開啟已簽署蛋糕資料夾，之後拖放 `.cake` 檔入去或拎走，再返 WinForge 驗證、信任或食用。

**EN —** The cake-file panel also accepts dropped `.cake` files directly. Dropped files are copied into the Cake Factory cake folder, then the latest file can be trusted, validated or eaten. Eating a cake records its id in the eaten ledger and deletes the file when possible, so imported cakes cannot be replayed after consumption.

**粵語 —** 蛋糕檔面板亦可以直接接收拖入嘅 `.cake` 檔。拖入嘅檔案會複製到 Cake Factory 蛋糕資料夾，之後最新檔案可以信任、驗證或食用。食用蛋糕會將 cake id 寫入食用記錄，並盡量刪除檔案，所以匯入蛋糕消耗後唔可以重播。

| Control · 控制 | Behavior · 行為 |
|---|---|
| `OpenCakeFolderButton` | Opens `%LOCALAPPDATA%\WinForge\cake-factory\cakes` in Windows Explorer. |
| Cake-file panel drop target | Imports dropped `.cake` files through `CakeFileService.ImportCakeFile`. |
| `TrustCakeKeyButton` | Trusts the public key embedded in the latest cake file. |
| `ValidateCakeButton` | Validates schema, key id, signature, trust, eaten status, packed status and expiry. |
| `EatCakeButton` | Eats the latest trusted cake, appends the eaten ledger and deletes the file when possible. |

## Buttons And Controls Â· æŒ‰éˆ•èˆ‡æŽ§åˆ¶é …

| Button Â· æŒ‰éˆ• | Type Â· é¡žåž‹ | XAML name Â· åç¨± | Handler Â· è™•ç†å‡½å¼ |
|---|---|---|---|
| [OpenFullFactoryBtn](../../buttons/apps-git-git/cake/001-openfullfactorybtn.md) | `Button` | `OpenFullFactoryBtn` | `OpenFullFactory_Click` |
| [OpenReactorBtn](../../buttons/apps-git-git/cake/002-openreactorbtn.md) | `Button` | `OpenReactorBtn` | `OpenReactor_Click` |
| [ReceiveSuppliesButton](../../buttons/apps-git-git/cake/003-receivesuppliesbutton.md) | `Button` | `ReceiveSuppliesButton` | `ReceiveSupplies_Click` |
| [HarvestButton](../../buttons/apps-git-git/cake/004-harvestbutton.md) | `Button` | `HarvestButton` | `Harvest_Click` |
| [CollectButton](../../buttons/apps-git-git/cake/005-collectbutton.md) | `Button` | `CollectButton` | `Collect_Click` |
| [MillButton](../../buttons/apps-git-git/cake/006-millbutton.md) | `Button` | `MillButton` | `Mill_Click` |
| [RefineButton](../../buttons/apps-git-git/cake/007-refinebutton.md) | `Button` | `RefineButton` | `Refine_Click` |
| [ChurnButton](../../buttons/apps-git-git/cake/008-churnbutton.md) | `Button` | `ChurnButton` | `Churn_Click` |
| [ProcessCocoaButton](../../buttons/apps-git-git/cake/009-processcocoabutton.md) | `Button` | `ProcessCocoaButton` | `ProcessCocoa_Click` |
| [StartBatchButton](../../buttons/apps-git-git/cake/010-startbatchbutton.md) | `Button` | `StartBatchButton` | `StartBatch_Click` |
| [AdvanceButton](../../buttons/apps-git-git/cake/011-advancebutton.md) | `Button` | `AdvanceButton` | `Advance_Click` |
| [CleanButton](../../buttons/apps-git-git/cake/012-cleanbutton.md) | `Button` | `CleanButton` | `Clean_Click` |
| [TrustCakeKeyButton](../../buttons/apps-git-git/cake/013-trustcakekeybutton.md) | `Button` | `TrustCakeKeyButton` | `TrustCakeKey_Click` |
| [ValidateCakeButton](../../buttons/apps-git-git/cake/014-validatecakebutton.md) | `Button` | `ValidateCakeButton` | `ValidateCake_Click` |
| [EatCakeButton](../../buttons/apps-git-git/cake/015-eatcakebutton.md) | `Button` | `EatCakeButton` | `EatCake_Click` |
| [OpenCakeFolderButton](../../buttons/apps-git-git/cake/016-opencakefolderbutton.md) | `Button` | `OpenCakeFolderButton` | `OpenCakeFolder_Click` |
| [CloseFullFactoryBtn](../../buttons/apps-git-git/cake/017-closefullfactorybtn.md) | `Button` | `CloseFullFactoryBtn` | `CloseFullFactory_Click` |
