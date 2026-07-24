# Developer workflow workbench · 開發工作流程工作台

Open `WinForge.exe --page devterminal` for reviewed listener termination, per-shell fnm/Volta Node selection, Corepack pnpm/yarn, Defender folder exclusions, TCP range/TIME_WAIT tuning, and measured npm/pnpm/pip/Docker cache cleanup.

用 `WinForge.exe --page devterminal` 開啟：先審閱 listener 先終止、fnm／Volta 每 shell Node、Corepack pnpm／yarn、Defender 資料夾例外、TCP 範圍／TIME_WAIT，同埋先量度後清理 npm／pnpm／pip／Docker 快取。

Every mutation requires review; listener identity is re-read immediately before termination, privileged actions elevate only after confirmation, and user values remain bounded discrete arguments.

每次修改都要確認；終止前會重新核對 listener 身份，提權動作只喺確認後執行，使用者值保持有界獨立參數。

![Developer & Terminal](https://raw.githubusercontent.com/codingmachineedge/WinForge/main/docs/screenshot-devterminal.png)
