# Native Release Reliability · 原生發佈可靠性

Only `native-release.yml` publishes releases, with exactly the tested C++/WinRT setup and matching native ZIP. The release job is test-gated; a retry uses one immutable version/tag; an ambiguous create is re-read until visible without blindly creating again; and only a current-main source can own Latest. Site-data dispatches are non-canceling and retry their matching delivery. · 只有 `native-release.yml` 發佈 release，而且只會有已測試 C++/WinRT setup 加相配原生 ZIP。release job 一定要測試成功；retry 用一個不可變 version/tag；含糊 create 會等到可見再讀，唔會盲目再 create；亦只有 current-main source 可以擁有 Latest。site-data dispatch 唔會中途取消，會 retry 對應發佈。

Local YAML/PowerShell and installer-contract validation pass. Earlier hosted GitHub API-outage failures are pending remote repair after this hardened workflow reaches `main`; no managed publisher is introduced. · 本機 YAML／PowerShell 同 installer-contract 驗證通過。較早 hosted GitHub API outage 失敗要等加固 workflow 到 `main` 後遙距 repair；冇加入 managed publisher。
