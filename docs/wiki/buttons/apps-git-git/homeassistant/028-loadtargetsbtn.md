# LoadTargetsBtn Â· Button

**EN â€”** Action/control documented from the WinUI XAML source for **Home Assistant**.
**ç²µèªž â€”** å‘¢å€‹å‹•ä½œï¼æŽ§åˆ¶é …ä¿‚ç”± **å®¶å±…åŠ©ç†** å˜… WinUI XAML ä¾†æºæ•´ç†å‡ºåšŸã€‚

| Field Â· æ¬„ä½ | Value Â· å€¼ |
|---|---|
| Module Â· æ¨¡çµ„ | [Home Assistant Â· å®¶å±…åŠ©ç†](../../../features/apps-git-git/homeassistant.md) |
| Category Â· åˆ†é¡ž | Apps & Git Â· ç¨‹å¼èˆ‡ Git |
| Control type Â· æŽ§åˆ¶é¡žåž‹ | $(System.Collections.Specialized.OrderedDictionary["Type"]) |
| XAML name Â· XAML åç¨± | $(System.Collections.Specialized.OrderedDictionary["Name"]) |
| Label / tooltip Â· æ¨™ç±¤ï¼æç¤º | LoadTargetsBtn |
| Handler Â· è™•ç†å‡½å¼ | $(System.Collections.Specialized.OrderedDictionary["Handler"]) |
| Source Â· ä¾†æº | $(System.Collections.Specialized.OrderedDictionary["Source"]) |

## Operator Notes Â· æ“ä½œå‚™è¨»

**EN â€”** Use this control from the module page shown above. If the handler is blank, the action is represented by binding or template state rather than a direct click handler in XAML.

**ç²µèªž â€”** å–ºä¸Šé¢æ¨¡çµ„é é¢ä½¿ç”¨å‘¢å€‹æŽ§åˆ¶é …ã€‚å¦‚æžœè™•ç†å‡½å¼ä¿‚ç©ºç™½ï¼Œä»£è¡¨å‹•ä½œå¯èƒ½ç”± binding æˆ–æ¨£æ¿ç‹€æ…‹è™•ç†ï¼Œè€Œå””ä¿‚ XAML å…¥é¢ç›´æŽ¥å¯« click handlerã€‚

**EN —** Reads `/api/services`, extracts `notify.*` targets and fills the notification target picker. Use this after Home Assistant or an AC Defender workflow exposes mobile-app or other notification services.

**粵語 —** 讀取 `/api/services`，抽出 `notify.*` 目標並填入通知目標選單。當 Home Assistant 或 AC Defender 流程暴露 mobile-app 或其他通知服務後，就用呢個掣載入目標。
