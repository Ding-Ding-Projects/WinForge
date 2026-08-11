# Support Tickets · 支援工單

Support Tickets is a fictional local-only recovery desk. It creates bounded tickets with category, description, severity, a local ticket number, a canned first response, and the status progression `New → Acknowledged → InProgress → Resolved`. · 支援工單係本機虛構復原台。佢會建立有界工單，包括類別、描述、嚴重程度、本機工單號碼、罐頭第一回覆，同 `New → Acknowledged → InProgress → Resolved` 狀態流程。

The surface states plainly that nothing is sent anywhere, there is no network request, no data is collected, and no person is reading the ticket. Resolution shows the real WinForge application-data folder and asks the platform file manager to open it; the app never deletes the folder. · 介面清楚講明唔會傳送去任何地方、冇網絡請求、冇資料收集，亦冇真人睇工單。處理方法會顯示真實 WinForge application-data 資料夾並要求平台檔案管理員開啟；app 永遠唔會刪除資料夾。

Ticket descriptions are limited to 4,000 characters and storage to 500 tickets. The list has a local plain-text-first search with the shared regex builder, extended selection, select-all/inverse selection, bulk status and delete actions, and UTF-8 JSON/CSV/Markdown/HTML exports for the selected filtered records. · 工單描述上限 4,000 個字元，儲存上限 500 張工單。清單有本機純文字預設搜尋、extended 揀選、揀晒／反轉揀選、批量狀態／刪除操作，同為所選篩選紀錄提供 UTF-8 JSON／CSV／Markdown／HTML 匯出。

Source: `Services/SupportTicketService.cs`, `Pages/SupportTicketsPage.xaml`, `Pages/SupportTicketsPage.xaml.cs`; tests: `tests/SupportTickets.Tests/Program.cs` (`6/6`). Full details: [Support Tickets feature article](../features/universal/support-tickets.md). · 來源：`Services/SupportTicketService.cs`、`Pages/SupportTicketsPage.xaml`、`Pages/SupportTicketsPage.xaml.cs`；測試：`tests/SupportTickets.Tests/Program.cs`（`6/6`）。詳細資料見[支援工單功能文章](../features/universal/support-tickets.md)。

![Support Tickets recovery desk with local disclosure and bulk controls](../screenshot-support-tickets-2026-08-11.png)
