# SetHvacBtn Â· Button

**EN â€”** Action/control documented from the WinUI XAML source for **Home Assistant**.
**ç²µèªž â€”** å‘¢å€‹å‹•ä½œï¼æŽ§åˆ¶é …ä¿‚ç”± **å®¶å±…åŠ©ç†** å˜… WinUI XAML ä¾†æºæ•´ç†å‡ºåšŸã€‚

| Field Â· æ¬„ä½ | Value Â· å€¼ |
|---|---|
| Module Â· æ¨¡çµ„ | [Home Assistant Â· å®¶å±…åŠ©ç†](../../../features/apps-git-git/homeassistant.md) |
| Category Â· åˆ†é¡ž | Apps & Git Â· ç¨‹å¼èˆ‡ Git |
| Control type Â· æŽ§åˆ¶é¡žåž‹ | $(System.Collections.Specialized.OrderedDictionary["Type"]) |
| XAML name Â· XAML åç¨± | $(System.Collections.Specialized.OrderedDictionary["Name"]) |
| Label / tooltip Â· æ¨™ç±¤ï¼æç¤º | SetHvacBtn |
| Handler Â· è™•ç†å‡½å¼ | $(System.Collections.Specialized.OrderedDictionary["Handler"]) |
| Source Â· ä¾†æº | $(System.Collections.Specialized.OrderedDictionary["Source"]) |

## Operator Notes Â· æ“ä½œå‚™è¨»

**EN â€”** Use this control from the module page shown above. If the handler is blank, the action is represented by binding or template state rather than a direct click handler in XAML.

**ç²µèªž â€”** å–ºä¸Šé¢æ¨¡çµ„é é¢ä½¿ç”¨å‘¢å€‹æŽ§åˆ¶é …ã€‚å¦‚æžœè™•ç†å‡½å¼ä¿‚ç©ºç™½ï¼Œä»£è¡¨å‹•ä½œå¯èƒ½ç”± binding æˆ–æ¨£æ¿ç‹€æ…‹è™•ç†ï¼Œè€Œå””ä¿‚ XAML å…¥é¢ç›´æŽ¥å¯« click handlerã€‚

**EN —** Sends `POST /api/services/climate/set_hvac_mode` for the selected `climate.*` entity. Modes come from the in-app list: `off`, `heat`, `cool`, `heat_cool`, `auto`, `dry` and `fan_only`.

**粵語 —** 對已選 `climate.*` 實體送出 `POST /api/services/climate/set_hvac_mode`。模式來自 app 內清單：`off`、`heat`、`cool`、`heat_cool`、`auto`、`dry` 同 `fan_only`。
