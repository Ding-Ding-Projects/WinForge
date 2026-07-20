# Native Percentage Calculator · 原生百分比計算器

**EN —** The native Percentage Calculator is a local C++/WinRT page backed by standard-C++ `PercentCalc`. Its six cards match the managed percentage, change, tip, and ratio workflows while keeping parsing, rounding, state reset, localization, accessibility, and explicit-only Copy in the native app.

**粵語 —** 原生百分比計算器係一頁本機 C++/WinRT 頁面，由標準 C++ `PercentCalc` 支援。六張卡對等 managed 百分比、變化、貼士同比例流程，同時將解析、取捨、狀態重設、本地化、無障礙同只限明確 Copy 留喺原生 app。

**Evidence · 證據：** Debug/Release 0-error builds; **915/915** cores including **37/37** Percentage Calculator contracts; focused UIA **14/14**; catalog parity 346 + 5; installer contract pass; and a visually inspected current 1962×1311 driver screenshot. LowLevel MCP was not callable, but the valid driver PrintWindow fallback is retained below. The sole C++ publisher is test-gated and retry-hardened; earlier hosted API-outage repairs remain pending after the controlled push. · 唯一 C++ publisher 已加測試 gate 同 retry 加固；較早 hosted API outage repair 要等 controlled push 後完成。

![Percentage Calculator · 百分比計算器](images/screenshot-percent.png)

[← Native C++ Rewrite](Native-Cpp-Rewrite.md) · [Release reliability](Native-Release-Reliability.md) · [Feature reference](features/calculators-numbers/percent.md) · [Screenshots](Screenshots.md)
