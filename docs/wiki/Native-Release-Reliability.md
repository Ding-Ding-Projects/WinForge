# Native Release Reliability · 原生發佈可靠性

**EN —** Only `native-release.yml` can publish a release, and it publishes exactly the tested native setup plus matching native ZIP. It is test-gated, uses one immutable retry tag, waits out ambiguous create visibility, reconciles only the exact SHA/assets, and lets only current-main promote Latest. Generated-data dispatches are non-canceling and retry their matching native delivery.

**粵語 —** 只有 `native-release.yml` 可以發佈，而且只會發佈已測試原生 setup 加相配 ZIP。佢一定要測試成功、用一個不可變 retry tag、會等含糊 create 變可見、只對確切 SHA／asset 做 reconciliation，而且只畀 current-main 升 Latest。生成資料 dispatch 唔會中途取消，亦會 retry 對應原生發佈。

**Current state · 目前狀態：** local workflow/installer-contract validation passes. Earlier hosted API-outage failures are pending remote repair after this hardened workflow is pushed; no managed publisher is added.

[← Native C++ Rewrite](Native-Cpp-Rewrite.md)
