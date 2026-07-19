# Native Unit Price · 原生單位價格

The canonical implementation, safety, verification, and capture record is in [Native-Unit-Price.md](../Native-Unit-Price.md).

完整實作、安全、驗證同擷取紀錄喺：[Native-Unit-Price.md](../Native-Unit-Price.md)。

The native route uses standard C++ plus C++/WinRT for `priceper`, `unitprice`, and `module.unitprice`; it is local-only, uses the clipboard only after explicit Copy, and is honestly `capture-blocked` pending a valid native frame.

原生 route 用標準 C++ 加 C++/WinRT 支援 `priceper`、`unitprice` 同 `module.unitprice`；只喺本機做，只有明確 Copy 先用剪貼簿，等有效 native frame 前 visual 如實係 `capture-blocked`。

Controlled integration evidence is Debug/Release 0 errors, combined core **828/828** (Unit Price **13/13**), focused UIA **15/15**, Utility UIA **39/39** including CSS Unit Converter, catalog parity **346 + 5**, and installer-contract pass. The broad aggregate did not yield a final footer, so no full-shell result is claimed. · 受控整合證據係 Debug／Release 0 errors、合併 core **828/828**（Unit Price **13/13**）、專項 UIA **15/15**、包括 CSS 嘅 Utility UIA **39/39**、catalog parity **346 + 5** 同 installer-contract 通過。廣泛 aggregate 冇最後 footer，所以唔聲稱 full shell。
